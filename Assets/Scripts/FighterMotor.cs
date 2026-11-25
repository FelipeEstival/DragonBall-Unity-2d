using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FighterMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [Header("Ground movement")]
    [SerializeField] private float baseGravityScale = 3f;
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 35f;
    [SerializeField] private float jumpSpeed = 12f;

    [Header("Flying")]
    [SerializeField] private float flightSpeed = 6f;

    [Header("Teleport")]
    [SerializeField] private float teleportDistance = 4f;
    [SerializeField] private float teleportCooldown = 3f;

    [Header("Optional Animator parameters")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string groundedParameter = "Grounded";
    [SerializeField] private string flyingParameter = "Flying";
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string teleportTrigger = "Teleport";

    private Collider2D playerCollider;
    private LayerMask groundLayers;

    private Vector2 moveInput;
    private bool jumpQueued;
    private bool flying;
    private float nextTeleportTime;

    // Valores para zonas de baixa gravidade.
    private float zoneGravityMultiplier = 1f;
    private float zoneDecelerationMultiplier = 1f;

    public float FacingDirection { get; private set; } = 1f;
    public bool IsFlying => flying;
    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        playerCollider = GetComponent<Collider2D>();

        int groundLayer = LayerMask.NameToLayer("Ground");

        if (groundLayer == -1)
        {
            Debug.LogError("A layer named 'Ground' was not found.", this);
        }
        else
        {
            groundLayers = 1 << groundLayer;
        }

        if (playerCollider == null)
            Debug.LogError("FighterMotor requires a Collider2D on the player.", this);

        body.gravityScale = baseGravityScale;
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
        FaceTowards(moveInput.x);
    }

    public void FaceTowards(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) < 0.05f)
            return;

        FacingDirection = Mathf.Sign(horizontalDirection);

        // Os sprites devem estar virados à direita por padrão.
        if (spriteRenderer != null)
            spriteRenderer.flipX = FacingDirection < 0f;
    }

    public void RequestJump()
    {
        jumpQueued = true;
    }

    public void SetFlying(bool shouldFly)
    {
        flying = shouldFly;
    }

    public void ToggleFlying()
    {
        SetFlying(!flying);
    }

    public bool TryTeleport() // Ainda é necessário criar um check para que o teletransporte não atravesse paredes.
    {
        if (Time.time < nextTeleportTime)
            return false;

        nextTeleportTime = Time.time + teleportCooldown;
        body.position += Vector2.right * FacingDirection * teleportDistance;
        PlayAnimationTrigger(teleportTrigger);
        return true;
    }

    public void SetZoneEffects(float gravityMultiplier, float decelerationMultiplier)
    {
        zoneGravityMultiplier = Mathf.Max(0f, gravityMultiplier);
        zoneDecelerationMultiplier = Mathf.Max(0f, decelerationMultiplier);
    }

    public void ResetZoneEffects()
    {
        zoneGravityMultiplier = 1f;
        zoneDecelerationMultiplier = 1f;
    }

    public void PlayAnimationTrigger(string triggerName)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
            animator.SetTrigger(triggerName);
    }

    private void FixedUpdate()
    {
        IsGrounded = CheckGrounded();

        body.gravityScale = flying
            ? 0f
            : baseGravityScale * zoneGravityMultiplier;

        float stopRate = deceleration * zoneDecelerationMultiplier;

        if (flying)
        {
            Vector2 targetVelocity = moveInput * flightSpeed;
            float rate = moveInput.sqrMagnitude > 0.01f ? acceleration : stopRate;

            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                targetVelocity,
                rate * Time.fixedDeltaTime
            );
        }
        else
        {
            float targetX = moveInput.x * moveSpeed;
            float newX;

            if (IsGrounded && Mathf.Abs(moveInput.x) > 0.01f)
            {
                // Sem fricção inicial após aterrisar de um pulo.
                newX = targetX;
            }
            else
            {
                float rate = Mathf.Abs(moveInput.x) > 0.01f
                    ? acceleration
                    : stopRate;

                newX = Mathf.MoveTowards(
                    body.linearVelocity.x,
                    targetX,
                    rate * Time.fixedDeltaTime
                );
            }

            body.linearVelocity = new Vector2(newX, body.linearVelocity.y);

            if (jumpQueued && IsGrounded)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed);
                IsGrounded = false;
                PlayAnimationTrigger(jumpTrigger);
            }
        }

        jumpQueued = false;
        UpdateAnimator();
    }

    private bool CheckGrounded()
    {
        if (playerCollider == null || groundLayers.value == 0)
            return false;

        Bounds bounds = playerCollider.bounds;

        const float probeDepth = 0.04f;
        const float horizontalInset = 0.05f;

        Vector2 probeSize = new Vector2(
            Mathf.Max(0.01f, bounds.size.x - horizontalInset * 2f),
            probeDepth
        );

        Vector2 probeCenter = new Vector2(
            bounds.center.x,
            bounds.min.y - probeDepth * 0.5f
        );

        return Physics2D.OverlapBox(
            probeCenter,
            probeSize,
            0f,
            groundLayers
        ) != null;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        if (!string.IsNullOrWhiteSpace(speedParameter))
            animator.SetFloat(speedParameter, Mathf.Abs(body.linearVelocity.x));

        if (!string.IsNullOrWhiteSpace(groundedParameter))
            animator.SetBool(groundedParameter, IsGrounded);

        if (!string.IsNullOrWhiteSpace(flyingParameter))
            animator.SetBool(flyingParameter, flying);
    }
}