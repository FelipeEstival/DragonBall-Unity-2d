using System;
using System.Collections.Generic;
using UnityEngine;
using DragonBall.Core;

namespace DragonBall.Player
{
    /// <summary>
    /// Modular 2D side-view character controller for Goku.
    /// Handles horizontal locomotion, grounded jumping (with coyote time and jump buffer),
    /// automatic sprite flipping, and Animator synchronization.
    /// Designed with clean extension hooks for combat, ki blasts, flight, and health systems.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class GokuController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform groundCheckPoint;

        [Header("Horizontal Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 7.5f;
        [SerializeField, Min(0f)] private float groundAcceleration = 75f;
        [SerializeField, Min(0f)] private float groundDeceleration = 65f;
        [SerializeField, Min(0f)] private float airAcceleration = 45f;
        [SerializeField, Min(0f)] private float airDeceleration = 20f;

        [Header("Jump & Gravity")]
        [SerializeField, Min(0f)] private float jumpForce = 14.5f;
        [SerializeField, Range(0.1f, 1f)] private float jumpCutMultiplier = 0.5f;
        [SerializeField, Min(0f)] private float baseGravityScale = 3.5f;
        [SerializeField, Min(1f)] private float fallGravityMultiplier = 1.45f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.1f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;

        [Header("Ground Check")]
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.75f, 0.12f);
        [SerializeField] private LayerMask groundLayer;

        [Header("Visuals & Animator")]
        [SerializeField] private bool spritesFaceRightByDefault = true;
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string isGroundedParam = "IsGrounded";
        [SerializeField] private string verticalVelocityParam = "VerticalVelocity";

        private IFighterInput inputSource;
        private Collider2D characterCollider;
        private readonly HashSet<int> availableAnimatorParams = new HashSet<int>();

        private int speedParamHash;
        private int isGroundedParamHash;
        private int verticalVelocityParamHash;

        private float coyoteTimer;
        private float jumpBufferTimer;
        private bool wasGrounded;
        private bool jumpCutApplied;

        public int FacingDirection { get; private set; } = 1;
        public bool IsGrounded { get; private set; }
        public bool IsMovementLocked { get; private set; }
        public FighterState CurrentState { get; private set; } = FighterState.Idle;
        public Rigidbody2D Body => body;

        public event Action OnJumped;
        public event Action OnLanded;
        public event Action<int> OnFacingChanged;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            characterCollider = GetComponent<Collider2D>();
            inputSource = GetComponent<IFighterInput>();

            // Ensure Z rotation is always locked for 2D side-view fighting physics
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.gravityScale = baseGravityScale;

            // Auto-resolve Ground layer if not assigned in the Inspector
            if (groundLayer.value == 0)
            {
                int resolvedLayer = LayerMask.NameToLayer("Ground");
                if (resolvedLayer != -1)
                {
                    groundLayer = 1 << resolvedLayer;
                }
                else
                {
                    Debug.LogWarning("[GokuController] Layer 'Ground' not found. Assign Ground Layer in the Inspector.", this);
                }
            }

            CacheAnimatorParameters();
            ApplySpriteFlip();
        }

        private void Update()
        {
            if (inputSource == null)
            {
                return;
            }

            if (!IsMovementLocked)
            {
                if (inputSource.JumpPressed)
                {
                    jumpBufferTimer = jumpBufferTime;
                }

                // Variable jump height when releasing jump key early while rising
                if (!inputSource.JumpHeld && body.linearVelocity.y > 0f && !jumpCutApplied)
                {
                    body.linearVelocity = new Vector2(
                        body.linearVelocity.x,
                        body.linearVelocity.y * jumpCutMultiplier
                    );
                    jumpCutApplied = true;
                }

                if (Mathf.Abs(inputSource.MoveX) > 0.05f)
                {
                    FaceDirection(inputSource.MoveX);
                }
            }

            if (jumpBufferTimer > 0f)
            {
                jumpBufferTimer -= Time.deltaTime;
            }
        }

        private void FixedUpdate()
        {
            UpdateGroundStatus();

            if (!IsMovementLocked)
            {
                ApplyHorizontalMovement();
                TryExecuteBufferedJump();
            }

            ApplyDynamicGravity();
            UpdateState();
            UpdateAnimator();
        }

        /// <summary>
        /// Locks or unlocks player locomotion (useful during melee combos, ki blasts, hitstun, or cutscenes).
        /// </summary>
        public void SetMovementLocked(bool locked, bool stopHorizontalVelocity = true)
        {
            IsMovementLocked = locked;

            if (locked)
            {
                jumpBufferTimer = 0f;
                if (stopHorizontalVelocity)
                {
                    body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                }
            }
        }

        /// <summary>
        /// Sets the fighter's horizontal facing direction (+1 right, -1 left) and updates SpriteRenderer.flipX.
        /// </summary>
        public void FaceDirection(float horizontalInput)
        {
            if (Mathf.Abs(horizontalInput) < 0.01f)
            {
                return;
            }

            int newDirection = horizontalInput > 0f ? 1 : -1;
            if (newDirection == FacingDirection)
            {
                return;
            }

            FacingDirection = newDirection;
            ApplySpriteFlip();
            OnFacingChanged?.Invoke(FacingDirection);
        }

        /// <summary>
        /// Applies an instant knockback impulse (ready for enemy hit reactions).
        /// </summary>
        public void ApplyKnockback(Vector2 velocity)
        {
            body.linearVelocity = velocity;
        }

        private void UpdateGroundStatus()
        {
            wasGrounded = IsGrounded;
            IsGrounded = CheckGrounded();

            if (IsGrounded)
            {
                coyoteTimer = coyoteTime;
                jumpCutApplied = false;

                if (!wasGrounded)
                {
                    OnLanded?.Invoke();
                }
            }
            else if (coyoteTimer > 0f)
            {
                coyoteTimer -= Time.fixedDeltaTime;
            }
        }

        private void ApplyHorizontalMovement()
        {
            float moveInput = inputSource != null ? inputSource.MoveX : 0f;
            float targetVelocityX = moveInput * moveSpeed;

            bool hasInput = Mathf.Abs(moveInput) > 0.01f;
            float rate = IsGrounded
                ? (hasInput ? groundAcceleration : groundDeceleration)
                : (hasInput ? airAcceleration : airDeceleration);

            float nextVelocityX = Mathf.MoveTowards(
                body.linearVelocity.x,
                targetVelocityX,
                rate * Time.fixedDeltaTime
            );

            body.linearVelocity = new Vector2(nextVelocityX, body.linearVelocity.y);
        }

        private void TryExecuteBufferedJump()
        {
            if (jumpBufferTimer <= 0f || coyoteTimer <= 0f)
            {
                return;
            }

            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            IsGrounded = false;
            jumpCutApplied = false;

            OnJumped?.Invoke();
        }

        private void ApplyDynamicGravity()
        {
            body.gravityScale = body.linearVelocity.y < -0.05f
                ? baseGravityScale * fallGravityMultiplier
                : baseGravityScale;
        }

        private bool CheckGrounded()
        {
            if (groundLayer.value == 0)
            {
                return false;
            }

            Vector2 center = GetGroundCheckCenter();
            return Physics2D.OverlapBox(center, groundCheckSize, 0f, groundLayer) != null;
        }

        private Vector2 GetGroundCheckCenter()
        {
            if (groundCheckPoint != null)
            {
                return groundCheckPoint.position;
            }

            if (characterCollider != null)
            {
                Bounds bounds = characterCollider.bounds;
                return new Vector2(bounds.center.x, bounds.min.y);
            }

            return transform.position;
        }

        private void ApplySpriteFlip()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.flipX = spritesFaceRightByDefault
                ? FacingDirection < 0
                : FacingDirection > 0;
        }

        private void UpdateState()
        {
            if (IsMovementLocked)
            {
                return;
            }

            if (!IsGrounded)
            {
                CurrentState = body.linearVelocity.y >= 0f
                    ? FighterState.Jumping
                    : FighterState.Falling;
            }
            else if (Mathf.Abs(body.linearVelocity.x) > 0.1f)
            {
                CurrentState = FighterState.Running;
            }
            else
            {
                CurrentState = FighterState.Idle;
            }
        }

        private void CacheAnimatorParameters()
        {
            availableAnimatorParams.Clear();

            speedParamHash = Animator.StringToHash(speedParam);
            isGroundedParamHash = Animator.StringToHash(isGroundedParam);
            verticalVelocityParamHash = Animator.StringToHash(verticalVelocityParam);

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                availableAnimatorParams.Add(param.nameHash);
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (availableAnimatorParams.Count == 0 && animator.parameterCount > 0)
            {
                CacheAnimatorParameters();
            }

            if (availableAnimatorParams.Contains(speedParamHash))
            {
                animator.SetFloat(speedParamHash, Mathf.Abs(body.linearVelocity.x));
            }

            if (availableAnimatorParams.Contains(isGroundedParamHash))
            {
                animator.SetBool(isGroundedParamHash, IsGrounded);
            }

            if (availableAnimatorParams.Contains(verticalVelocityParamHash))
            {
                animator.SetFloat(verticalVelocityParamHash, body.linearVelocity.y);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(GetGroundCheckCenter(), groundCheckSize);
        }
    }
}
