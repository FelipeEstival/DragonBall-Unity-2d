#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DragonBall.Player;

namespace DragonBall.EditorTools
{
    /// <summary>
    /// One-click Editor utility to rebuild Goku's Animator/clips and ensure the Namek base scene
    /// is properly wired with valid asset references inside Unity.
    /// Menu: Tools -> Dragon Ball -> Setup Namek Base Scene
    /// </summary>
    public static class DragonBallSceneSetup
    {
        private const string GokuSheetPath = "Assets/Sprites/GOKU-SPRITES.png";
        private const string NamekBgPath = "Assets/Sprites/NAMEKUZEI-BACKGROUND.jpg";
        private const string GroundSpritePath = "Assets/Sprites/NOVO-CHAO-NAMEKUZEI.png";
        private const string AnimationsFolder = "Assets/Animations/Goku";
        private const string AnimatorControllerPath = "Assets/Animations/Goku/Goku.controller";

        [MenuItem("Tools/Dragon Ball/Setup Namek Base Scene")]
        public static void SetupNamekBaseScene()
        {
            AnimatorController controller = RebuildGokuAnimator();

            Sprite[] gokuSprites = AssetDatabase.LoadAllAssetsAtPath(GokuSheetPath).OfType<Sprite>().ToArray();
            Sprite bgSprite = AssetDatabase.LoadAllAssetsAtPath(NamekBgPath).OfType<Sprite>().FirstOrDefault();
            Sprite groundSprite = AssetDatabase.LoadAllAssetsAtPath(GroundSpritePath).OfType<Sprite>().FirstOrDefault();

            Sprite idleSprite = gokuSprites.FirstOrDefault(s => s.name == "Goku_Idle_0") ?? gokuSprites.FirstOrDefault();

            // 1. Main Camera (2D Side View)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.orthographic = true;
                mainCam.orthographicSize = 5f;
                mainCam.transform.position = new Vector3(0f, 0f, -10f);
            }

            // 2. Background
            GameObject bgObj = GameObject.Find("NamekBackground") ?? new GameObject("NamekBackground");
            bgObj.transform.position = new Vector3(0f, 0.3f, 10f);
            bgObj.transform.localScale = new Vector3(2.45f, 2.45f, 1f);
            SpriteRenderer bgRenderer = GetOrAdd<SpriteRenderer>(bgObj);
            bgRenderer.sprite = bgSprite;
            bgRenderer.sortingOrder = -10;

            // 3. Visual & Physical Ground aligned to Namek floor (3 Simple tiles for guaranteed rendering)
            GameObject groundObj = GameObject.Find("Ground") ?? new GameObject("Ground");
            groundObj.transform.position = new Vector3(0f, -3.65f, 0f);
            groundObj.transform.localScale = Vector3.one;
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer != -1)
            {
                groundObj.layer = groundLayer;
            }
            TrySetTag(groundObj, "Ground");

            SpriteRenderer legacyGroundRenderer = groundObj.GetComponent<SpriteRenderer>();
            if (legacyGroundRenderer != null)
            {
                Object.DestroyImmediate(legacyGroundRenderer);
            }

            float tileWidth = groundSprite != null ? groundSprite.bounds.size.x : 6.55f;
            string[] tileNames = { "GroundTile_Left", "GroundTile_Center", "GroundTile_Right" };
            for (int i = 0; i < tileNames.Length; i++)
            {
                Transform child = groundObj.transform.Find(tileNames[i]);
                GameObject tileObj = child != null ? child.gameObject : new GameObject(tileNames[i]);
                tileObj.transform.SetParent(groundObj.transform);
                tileObj.transform.localPosition = new Vector3((i - 1) * tileWidth, 0f, 0f);
                tileObj.transform.localScale = Vector3.one;
                if (groundLayer != -1)
                {
                    tileObj.layer = groundLayer;
                }

                SpriteRenderer tileRenderer = GetOrAdd<SpriteRenderer>(tileObj);
                tileRenderer.sprite = groundSprite;
                tileRenderer.drawMode = SpriteDrawMode.Simple;
                tileRenderer.sortingOrder = 0;
            }

            BoxCollider2D groundCollider = GetOrAdd<BoxCollider2D>(groundObj);
            groundCollider.size = new Vector2(22f, 2.15f);
            groundCollider.offset = new Vector2(0f, -0.325f);

            // 4. Goku Player
            GameObject gokuObj = GameObject.Find("Goku") ?? new GameObject("Goku");
            gokuObj.transform.position = new Vector3(-3f, -2.9f, 0f);
            gokuObj.transform.localScale = Vector3.one;
            TrySetTag(gokuObj, "Player");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1)
            {
                gokuObj.layer = playerLayer;
            }

            SpriteRenderer gokuRenderer = GetOrAdd<SpriteRenderer>(gokuObj);
            gokuRenderer.sprite = idleSprite;
            gokuRenderer.sortingOrder = 10;

            Rigidbody2D rb = GetOrAdd<Rigidbody2D>(gokuObj);
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 3.5f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            CapsuleCollider2D capsule = GetOrAdd<CapsuleCollider2D>(gokuObj);
            capsule.size = new Vector2(0.85f, 1.95f);
            capsule.offset = new Vector2(0f, 0.975f);

            Animator anim = GetOrAdd<Animator>(gokuObj);
            anim.runtimeAnimatorController = controller;

            Transform groundCheck = gokuObj.transform.Find("GroundCheck");
            if (groundCheck == null)
            {
                GameObject checkObj = new GameObject("GroundCheck");
                checkObj.transform.SetParent(gokuObj.transform);
                groundCheck = checkObj.transform;
            }
            groundCheck.localPosition = Vector3.zero;

            GetOrAdd<PlayerInputHandler>(gokuObj);
            GokuController gokuController = GetOrAdd<GokuController>(gokuObj);

            SerializedObject so = new SerializedObject(gokuController);
            so.FindProperty("body").objectReferenceValue = rb;
            so.FindProperty("spriteRenderer").objectReferenceValue = gokuRenderer;
            so.FindProperty("animator").objectReferenceValue = anim;
            so.FindProperty("groundCheckPoint").objectReferenceValue = groundCheck;
            if (groundLayer != -1)
            {
                so.FindProperty("groundLayer").intValue = 1 << groundLayer;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[DragonBallSceneSetup] Namek base scene and Goku Animator configured successfully!");
        }

        [MenuItem("Tools/Dragon Ball/Rebuild Goku Animator")]
        public static AnimatorController RebuildGokuAnimator()
        {
            if (!Directory.Exists(AnimationsFolder))
            {
                Directory.CreateDirectory(AnimationsFolder);
            }

            Dictionary<string, Sprite> spriteMap = AssetDatabase
                .LoadAllAssetsAtPath(GokuSheetPath)
                .OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);

            AnimationClip idleClip = CreateSpriteClip(
                $"{AnimationsFolder}/Goku_Idle.anim",
                GetSprites(spriteMap, "Goku_Idle_0", "Goku_Idle_1", "Goku_Idle_2", "Goku_Idle_1"),
                frameRate: 6f,
                loop: true
            );

            AnimationClip runClip = CreateSpriteClip(
                $"{AnimationsFolder}/Goku_Run.anim",
                GetSprites(spriteMap, "Goku_Run_0", "Goku_Run_1", "Goku_Run_2", "Goku_Run_1"),
                frameRate: 10f,
                loop: true
            );

            AnimationClip jumpClip = CreateSpriteClip(
                $"{AnimationsFolder}/Goku_Jump.anim",
                GetSprites(spriteMap, "Goku_Jump_0", "Goku_Jump_1"),
                frameRate: 8f,
                loop: false
            );

            AnimationClip fallClip = CreateSpriteClip(
                $"{AnimationsFolder}/Goku_Fall.anim",
                GetSprites(spriteMap, "Goku_Fall_0"),
                frameRate: 6f,
                loop: true
            );

            if (File.Exists(AnimatorControllerPath))
            {
                AssetDatabase.DeleteAsset(AnimatorControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idleState = sm.AddState("Idle");
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState runState = sm.AddState("Run");
            runState.motion = runClip;

            AnimatorState jumpState = sm.AddState("Jump");
            jumpState.motion = jumpClip;

            AnimatorState fallState = sm.AddState("Fall");
            fallState.motion = fallClip;

            // Idle <-> Run
            AddInstantTransition(idleState, runState, ("Speed", AnimatorConditionMode.Greater, 0.1f));
            AddInstantTransition(runState, idleState, ("Speed", AnimatorConditionMode.Less, 0.1f));

            // Grounded -> Jump / Fall
            AddInstantTransition(idleState, jumpState, ("IsGrounded", AnimatorConditionMode.IfNot, 0f), ("VerticalVelocity", AnimatorConditionMode.Greater, 0.1f));
            AddInstantTransition(runState, jumpState, ("IsGrounded", AnimatorConditionMode.IfNot, 0f), ("VerticalVelocity", AnimatorConditionMode.Greater, 0.1f));
            AddInstantTransition(idleState, fallState, ("IsGrounded", AnimatorConditionMode.IfNot, 0f), ("VerticalVelocity", AnimatorConditionMode.Less, -0.1f));
            AddInstantTransition(runState, fallState, ("IsGrounded", AnimatorConditionMode.IfNot, 0f), ("VerticalVelocity", AnimatorConditionMode.Less, -0.1f));

            // Jump -> Fall
            AddInstantTransition(jumpState, fallState, ("VerticalVelocity", AnimatorConditionMode.Less, -0.1f));

            // Airborne -> Grounded (Idle / Run)
            AddInstantTransition(jumpState, idleState, ("IsGrounded", AnimatorConditionMode.If, 0f), ("Speed", AnimatorConditionMode.Less, 0.1f));
            AddInstantTransition(jumpState, runState, ("IsGrounded", AnimatorConditionMode.If, 0f), ("Speed", AnimatorConditionMode.Greater, 0.1f));
            AddInstantTransition(fallState, idleState, ("IsGrounded", AnimatorConditionMode.If, 0f), ("Speed", AnimatorConditionMode.Less, 0.1f));
            AddInstantTransition(fallState, runState, ("IsGrounded", AnimatorConditionMode.If, 0f), ("Speed", AnimatorConditionMode.Greater, 0.1f));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return controller;
        }

        private static Sprite[] GetSprites(Dictionary<string, Sprite> map, params string[] names)
        {
            List<Sprite> list = new List<Sprite>();
            foreach (string name in names)
            {
                if (map.TryGetValue(name, out Sprite sprite))
                {
                    list.Add(sprite);
                }
            }
            return list.ToArray();
        }

        private static AnimationClip CreateSpriteClip(string path, Sprite[] sprites, float frameRate, bool loop)
        {
            AnimationClip clip = new AnimationClip { frameRate = frameRate };
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (sprites != null && sprites.Length > 0)
            {
                EditorCurveBinding binding = new EditorCurveBinding
                {
                    type = typeof(SpriteRenderer),
                    path = string.Empty,
                    propertyName = "m_Sprite"
                };

                int keyCount = sprites.Length > 1 ? sprites.Length + 1 : 1;
                ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[keyCount];
                for (int i = 0; i < sprites.Length; i++)
                {
                    keys[i] = new ObjectReferenceKeyframe
                    {
                        time = i / frameRate,
                        value = sprites[i]
                    };
                }

                if (keyCount > sprites.Length)
                {
                    keys[sprites.Length] = new ObjectReferenceKeyframe
                    {
                        time = sprites.Length / frameRate,
                        value = loop ? sprites[0] : sprites[sprites.Length - 1]
                    };
                }

                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }

            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void AddInstantTransition(
            AnimatorState from,
            AnimatorState to,
            params (string param, AnimatorConditionMode mode, float threshold)[] conditions)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            t.hasFixedDuration = true;

            foreach (var (param, mode, threshold) in conditions)
            {
                t.AddCondition(mode, threshold, param);
            }
        }

        private static T GetOrAdd<T>(GameObject obj) where T : Component
        {
            T comp = obj.GetComponent<T>();
            return comp != null ? comp : obj.AddComponent<T>();
        }

        private static void TrySetTag(GameObject obj, string tag)
        {
            try
            {
                obj.tag = tag;
            }
            catch
            {
                Debug.LogWarning($"[DragonBallSceneSetup] Tag '{tag}' not registered in TagManager yet.", obj);
            }
        }
    }
}
#endif
