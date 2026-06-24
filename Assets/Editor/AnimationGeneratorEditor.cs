#nullable disable
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;

namespace ProjectName.Editor
{
    /// <summary>
    /// Phase FIX: 애니메이션 생성 에디터.
    /// Player_Rigged.glb의 27개 본 구조를 분석하여 Idle/Walk/Run/Attack/Jump/Gather 애니메이션 클립을 생성합니다.
    /// </summary>
    public class AnimationGeneratorEditor : EditorWindow
    {
        private static readonly string ANIM_PATH = "Assets/Animations/";
        private static readonly string CONTROLLER_PATH = "Assets/Animations/";

        // ================================================================
        // Bone list (Player_Rigged.glb 기준 27 bones)
        // ================================================================
        private static readonly string[] BONES = new string[]
        {
            "Root", "spine", "spine.001", "spine.002", "spine.003",
            "spine.004", "spine.005",
            "shoulder.L", "upper_arm.L", "forearm.L", "hand.L",
            "shoulder.R", "upper_arm.R", "forearm.R", "hand.R",
            "breast.L", "breast.R",
            "pelvis.L", "pelvis.R",
            "thigh.L", "shin.L", "foot.L", "toe.L",
            "thigh.R", "shin.R", "foot.R", "toe.R"
        };

        [MenuItem("Tools/Generate Character Animations")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<AnimationGeneratorEditor>(false, "Animation Generator");
        }

        // Batch-mode entry point: Unity.exe -quit -batchmode -executeMethod ProjectName.Editor.AnimationGeneratorEditor.BatchGenerate
        public static void BatchGenerate()
        {
            Debug.Log("=== AnimationGeneratorEditor: Batch Generate Started ===");
            var window = ScriptableObject.CreateInstance<AnimationGeneratorEditor>();
            window.GenerateAllAnimations();
            Debug.Log("=== Animation Generator: Batch Generate Complete ===");
        }

        private void OnGUI()
        {
            GUILayout.Label("Character Animation Generator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            if (GUILayout.Button("Generate All Animations", GUILayout.Height(40)))
            {
                GenerateAllAnimations();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Generate Player Animations Only"))
            {
                GeneratePlayerAnimations();
            }
            if (GUILayout.Button("Generate Soldier Animations Only"))
            {
                GenerateSoldierAnimations();
            }
            if (GUILayout.Button("Generate Monster Animations Only"))
            {
                GenerateMonsterAnimations();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Create Animator Controllers"))
            {
                CreateAllControllers();
            }
        }

        private void GenerateAllAnimations()
        {
            GeneratePlayerAnimations();
            GenerateSoldierAnimations();
            GenerateMonsterAnimations();
            CreateAllControllers();
            AssetDatabase.Refresh();
            Debug.Log("✅ All animations generated successfully!");
        }

        // ================================================================
        // Player Animations (27 bone humanoid)
        // ================================================================
        private void GeneratePlayerAnimations()
        {
            Directory.CreateDirectory(ANIM_PATH);
            CreateClip("Player_Idle", CreatePlayerIdle, true);
            CreateClip("Player_Walk", CreatePlayerWalk, true);
            CreateClip("Player_Run", CreatePlayerRun, true);
            CreateClip("Player_Attack", CreatePlayerAttack, false);
            CreateClip("Player_Jump", CreatePlayerJump, false);
            CreateClip("Player_Gather", CreatePlayerGather, false);
            Debug.Log("✅ Player animations created");
        }

        private void GenerateSoldierAnimations()
        {
            Directory.CreateDirectory(ANIM_PATH);
            CreateClip("Soldier_Idle", CreateSoldierIdle, true);
            CreateClip("Soldier_Walk", CreateSoldierWalk, true);
            CreateClip("Soldier_Run", CreateSoldierRun, true);
            CreateClip("Soldier_Attack", CreateSoldierAttack, false);
            Debug.Log("✅ Soldier animations created");
        }

        private void GenerateMonsterAnimations()
        {
            Directory.CreateDirectory(ANIM_PATH);
            CreateClip("Monster_Idle", CreateMonsterIdle, true);
            CreateClip("Monster_Walk", CreateMonsterWalk, true);
            CreateClip("Monster_Run", CreateMonsterRun, true);
            CreateClip("Monster_Attack", CreateMonsterAttack, false);
            Debug.Log("✅ Monster animations created");
        }

        // ================================================================
        // Animation Clip Factory
        // ================================================================
        private void CreateClip(string name, System.Action<AnimationClip> fillMethod, bool loop)
        {
            var clip = new AnimationClip();
            clip.name = name;
            clip.frameRate = 30;
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Default;

            // AnimationClipSettings
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            settings.loopBlendOrientation = loop;
            settings.loopBlendPositionY = loop;
            settings.loopBlendPositionXZ = loop;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            fillMethod(clip);

            string path = ANIM_PATH + name + ".anim";
            AssetDatabase.CreateAsset(clip, path);
        }

        // ================================================================
        // Curve Helpers
        // ================================================================
        private static void SetCurve(AnimationClip clip, string bonePath, string property, AnimationCurve curve)
        {
            clip.SetCurve(bonePath, typeof(Transform), property, curve);
        }

        private static AnimationCurve MakeSine(float amplitude, float frequency, float phase = 0f, float offset = 0f)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= 30; i++)
            {
                float t = i / 30f;
                float val = offset + amplitude * Mathf.Sin((t * frequency + phase) * Mathf.PI * 2f);
                curve.AddKey(t, val);
            }
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }
            return curve;
        }

        private static AnimationCurve MakeStep(float amplitude, float frequency, float phase = 0f)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= 30; i++)
            {
                float t = i / 30f;
                float phase2 = (t * frequency + phase) * Mathf.PI * 2f;
                float val = amplitude * (Mathf.Sin(phase2) * 0.5f + Mathf.Cos(phase2 * 0.5f) * 0.5f);
                curve.AddKey(t, val * 0.5f);
            }
            return curve;
        }

        private static AnimationCurve MakeBounce(float amplitude)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, 0f);
            curve.AddKey(0.15f, amplitude);
            curve.AddKey(0.25f, amplitude * 0.5f);
            curve.AddKey(0.5f, amplitude * 0.1f);
            curve.AddKey(1f, 0f);
            return curve;
        }

        private static AnimationCurve MakeCurve(params Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }
            return curve;
        }

        private static void SetRotationCurve(AnimationClip clip, string bone, Vector3 euler, float time = 0f)
        {
            var curveX = new AnimationCurve(new Keyframe(time, euler.x));
            var curveY = new AnimationCurve(new Keyframe(time, euler.y));
            var curveZ = new AnimationCurve(new Keyframe(time, euler.z));
            SetCurve(clip, bone, "localEulerAnglesBaked.x", curveX);
            SetCurve(clip, bone, "localEulerAnglesBaked.y", curveY);
            SetCurve(clip, bone, "localEulerAnglesBaked.z", curveZ);
        }

        private static void SetRotationAnim(AnimationClip clip, string bone, 
            System.Func<float, Vector3> rotationFunc, float duration = 1f)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();
            int frameCount = Mathf.RoundToInt(duration * 30f);
            for (int i = 0; i <= frameCount; i++)
            {
                float t = i / (float)frameCount * duration;
                Vector3 rot = rotationFunc(t);
                curveX.AddKey(t, rot.x);
                curveY.AddKey(t, rot.y);
                curveZ.AddKey(t, rot.z);
            }
            SetCurve(clip, bone, "localEulerAnglesBaked.x", curveX);
            SetCurve(clip, bone, "localEulerAnglesBaked.y", curveY);
            SetCurve(clip, bone, "localEulerAnglesBaked.z", curveZ);
        }

        private static void SetPositionAnim(AnimationClip clip, string bone,
            System.Func<float, Vector3> posFunc, float duration = 1f)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();
            int frameCount = Mathf.RoundToInt(duration * 30f);
            for (int i = 0; i <= frameCount; i++)
            {
                float t = i / (float)frameCount * duration;
                Vector3 pos = posFunc(t);
                curveX.AddKey(t, pos.x);
                curveY.AddKey(t, pos.y);
                curveZ.AddKey(t, pos.z);
            }
            SetCurve(clip, "Root", "localPosition.x", curveX);
            SetCurve(clip, "Root", "localPosition.y", curveY);
            SetCurve(clip, "Root", "localPosition.z", curveZ);
        }

        // ================================================================
        // Player Idle — subtle breathing + slight sway
        // ================================================================
        private void CreatePlayerIdle(AnimationClip clip)
        {
            float dur = 2f;
            // Breathing — spine.001 rotates slightly
            SetRotationAnim(clip, "spine.001", t => new Vector3(
                1.5f * Mathf.Sin(t * Mathf.PI / dur),
                0.5f * Mathf.Sin(t * Mathf.PI / dur * 0.7f),
                0.3f * Mathf.Sin(t * Mathf.PI / dur * 0.5f)
            ), dur);

            SetRotationAnim(clip, "spine.002", t => new Vector3(
                1f * Mathf.Sin(t * Mathf.PI / dur),
                0.3f * Mathf.Sin(t * Mathf.PI / dur * 0.7f),
                0.2f * Mathf.Sin(t * Mathf.PI / dur * 0.5f)
            ), dur);

            SetRotationAnim(clip, "spine.003", t => new Vector3(
                0.5f * Mathf.Sin(t * Mathf.PI / dur),
                0, 0
            ), dur);

            // Head slight look-around
            SetRotationAnim(clip, "spine.005", t => new Vector3(
                0, 2f * Mathf.Sin(t * Mathf.PI / dur * 0.3f), 0
            ), dur);

            // Arms slight sway
            SetRotationAnim(clip, "shoulder.L", t => new Vector3(
                0, 0, 1f * Mathf.Sin(t * Mathf.PI / dur * 0.5f)
            ), dur);
            SetRotationAnim(clip, "shoulder.R", t => new Vector3(
                0, 0, -1f * Mathf.Sin(t * Mathf.PI / dur * 0.5f)
            ), dur);
        }

        // ================================================================
        // Player Walk — full body walk cycle, 1 second per step
        // ================================================================
        private void CreatePlayerWalk(AnimationClip clip)
        {
            float dur = 1f; // 1 sec = 2 steps
            float stride = 20f;

            // Root vertical bobbing
            SetPositionAnim(clip, "Root", t => new Vector3(
                0,
                0.15f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0
            ), dur);

            // Spine counter-rotation
            SetRotationAnim(clip, "spine", t => new Vector3(
                0, 0, -3f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "spine.001", t => new Vector3(
                2f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, -2f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "spine.003", t => new Vector3(
                -3f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                4f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0
            ), dur);

            // Head
            SetRotationAnim(clip, "spine.005", t => new Vector3(
                2f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, 0
            ), dur);

            // LEFT LEG — forward swing (positive when t=0 is heel strike)
            SetRotationAnim(clip, "thigh.L", t => new Vector3(
                stride * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, 2f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "shin.L", t => new Vector3(
                -15f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f / dur)) + 5f,
                0, 0
            ), dur);
            SetRotationAnim(clip, "foot.L", t => new Vector3(
                -5f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, 0
            ), dur);

            // RIGHT LEG — opposite phase
            SetRotationAnim(clip, "thigh.R", t => new Vector3(
                stride * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                0, -2f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "shin.R", t => new Vector3(
                -15f * Mathf.Abs(Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)) + 5f,
                0, 0
            ), dur);
            SetRotationAnim(clip, "foot.R", t => new Vector3(
                -5f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                0, 0
            ), dur);

            // LEFT ARM — swing opposite to legs
            SetRotationAnim(clip, "shoulder.L", t => new Vector3(
                5f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                2f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                5f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "upper_arm.L", t => new Vector3(
                -10f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, 0
            ), dur);
            SetRotationAnim(clip, "forearm.L", t => new Vector3(
                -10f + 5f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f / dur)),
                0, 0
            ), dur);

            // RIGHT ARM — opposite phase
            SetRotationAnim(clip, "shoulder.R", t => new Vector3(
                5f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                -2f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                -5f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "upper_arm.R", t => new Vector3(
                -10f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                0, 0
            ), dur);
            SetRotationAnim(clip, "forearm.R", t => new Vector3(
                -10f + 5f * Mathf.Abs(Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)),
                0, 0
            ), dur);

            // Pelvis slight tilt
            SetRotationAnim(clip, "pelvis.L", t => new Vector3(
                3f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, 0
            ), dur);
            SetRotationAnim(clip, "pelvis.R", t => new Vector3(
                3f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                0, 0
            ), dur);
        }

        // ================================================================
        // Player Run — faster, more exaggerated
        // ================================================================
        private void CreatePlayerRun(AnimationClip clip)
        {
            float dur = 0.6f;
            float stride = 35f;

            // Root — more vertical bounce
            SetPositionAnim(clip, "Root", t => new Vector3(
                0,
                0.3f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0
            ), dur);

            // Spine — more forward lean
            SetRotationAnim(clip, "spine", t => new Vector3(
                -5f, 0, -5f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "spine.001", t => new Vector3(
                5f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, -3f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "spine.003", t => new Vector3(
                -5f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                6f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0
            ), dur);

            // Head
            SetRotationAnim(clip, "spine.005", t => new Vector3(
                3f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, 0
            ), dur);

            // LEGS — bigger stride
            SetRotationAnim(clip, "thigh.L", t => new Vector3(
                stride * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, 3f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "shin.L", t => new Vector3(
                -20f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f / dur)) + 8f,
                0, 0
            ), dur);
            SetRotationAnim(clip, "foot.L", t => new Vector3(
                -8f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, 0
            ), dur);

            SetRotationAnim(clip, "thigh.R", t => new Vector3(
                stride * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                0, -3f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "shin.R", t => new Vector3(
                -20f * Mathf.Abs(Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)) + 8f,
                0, 0
            ), dur);
            SetRotationAnim(clip, "foot.R", t => new Vector3(
                -8f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                0, 0
            ), dur);

            // ARMS — more aggressive pumping
            SetRotationAnim(clip, "shoulder.L", t => new Vector3(
                10f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                3f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                8f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "upper_arm.L", t => new Vector3(
                -15f * Mathf.Sin(t * Mathf.PI * 2f / dur),
                0, 0
            ), dur);
            SetRotationAnim(clip, "forearm.L", t => new Vector3(
                -15f + 8f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f / dur)),
                0, 0
            ), dur);

            SetRotationAnim(clip, "shoulder.R", t => new Vector3(
                10f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                -3f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                -8f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "upper_arm.R", t => new Vector3(
                -15f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur),
                0, 0
            ), dur);
            SetRotationAnim(clip, "forearm.R", t => new Vector3(
                -15f + 8f * Mathf.Abs(Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)),
                0, 0
            ), dur);

            // Pelvis
            SetRotationAnim(clip, "pelvis.L", t => new Vector3(
                5f * Mathf.Sin(t * Mathf.PI * 2f / dur), 0, 0
            ), dur);
            SetRotationAnim(clip, "pelvis.R", t => new Vector3(
                5f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur), 0, 0
            ), dur);
        }

        // ================================================================
        // Player Attack — sword swing right-to-left
        // ================================================================
        private void CreatePlayerAttack(AnimationClip clip)
        {
            float dur = 0.5f;

            // Spine twist
            SetRotationAnim(clip, "spine", t => {
                float p = t / dur;
                return new Vector3(-5f * p, 20f * Mathf.Sin(p * Mathf.PI) * (1 - p), -10f * p);
            }, dur);
            SetRotationAnim(clip, "spine.001", t => {
                float p = t / dur;
                return new Vector3(-10f * p, 15f * Mathf.Sin(p * Mathf.PI) * (1 - p), -5f * p);
            }, dur);

            // Right arm — big swing
            SetRotationAnim(clip, "shoulder.R", t => {
                float p = t / dur;
                return new Vector3(-30f * p, -40f * p + 20f * Mathf.Sin(p * Mathf.PI), 20f * Mathf.Sin(p * Mathf.PI));
            }, dur);
            SetRotationAnim(clip, "upper_arm.R", t => {
                float p = t / dur;
                return new Vector3(-20f * p, -10f * Mathf.Sin(p * Mathf.PI), 0);
            }, dur);
            SetRotationAnim(clip, "forearm.R", t => {
                float p = t / dur;
                return new Vector3(-30f * p, 0, 0);
            }, dur);

            // Left arm — brace
            SetRotationAnim(clip, "shoulder.L", t => {
                float p = t / dur;
                return new Vector3(10f * p, -10f * p, -5f * p);
            }, dur);
            SetRotationAnim(clip, "forearm.L", t => {
                float p = t / dur;
                return new Vector3(-20f * p, 0, 0);
            }, dur);

            // Legs — slight lunge
            SetRotationAnim(clip, "thigh.L", t => {
                float p = t / dur;
                return new Vector3(15f * p, 0, 5f * p);
            }, dur);
            SetRotationAnim(clip, "shin.L", t => {
                float p = t / dur;
                return new Vector3(-10f * p, 0, 0);
            }, dur);
            SetRotationAnim(clip, "thigh.R", t => {
                float p = t / dur;
                return new Vector3(-10f * p, 0, -5f * p);
            }, dur);
            SetRotationAnim(clip, "shin.R", t => {
                float p = t / dur;
                return new Vector3(5f * p, 0, 0);
            }, dur);

            // Head
            SetRotationAnim(clip, "spine.005", t => {
                float p = t / dur;
                return new Vector3(-5f * p, 20f * Mathf.Sin(p * Mathf.PI), 0);
            }, dur);
        }

        // ================================================================
        // Player Jump
        // ================================================================
        private void CreatePlayerJump(AnimationClip clip)
        {
            float dur = 0.8f;

            // Root — up then down
            SetPositionAnim(clip, "Root", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(0, 0, 0); // crouch
                if (p < 0.5f) return new Vector3(0, 0.7f * (p - 0.3f) / 0.2f, 0); // up
                if (p < 0.8f) return new Vector3(0, 0.7f - 0.7f * (p - 0.5f) / 0.3f, 0); // down
                return Vector3.zero; // land
            }, dur);

            // Crouch then jump
            SetRotationAnim(clip, "spine", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(10f * (1 - p / 0.3f), 0, 0);
                if (p < 0.5f) return new Vector3(0, 0, 0);
                return new Vector3(-5f * (p - 0.5f) / 0.3f, 0, 0);
            }, dur);

            // Legs — tuck then extend
            SetRotationAnim(clip, "thigh.L", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(20f * p / 0.3f, 0, 0);
                if (p < 0.5f) return new Vector3(20f - 40f * (p - 0.3f) / 0.2f, 0, 0);
                return new Vector3(-20f * (p - 0.5f) / 0.3f, 0, 0);
            }, dur);
            SetRotationAnim(clip, "shin.L", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(-30f * p / 0.3f, 0, 0);
                if (p < 0.5f) return new Vector3(-30f + 40f * (p - 0.3f) / 0.2f, 0, 0);
                return new Vector3(10f * (p - 0.5f) / 0.3f, 0, 0);
            }, dur);

            SetRotationAnim(clip, "thigh.R", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(20f * p / 0.3f, 0, 0);
                if (p < 0.5f) return new Vector3(20f - 40f * (p - 0.3f) / 0.2f, 0, 0);
                return new Vector3(-20f * (p - 0.5f) / 0.3f, 0, 0);
            }, dur);
            SetRotationAnim(clip, "shin.R", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(-30f * p / 0.3f, 0, 0);
                if (p < 0.5f) return new Vector3(-30f + 40f * (p - 0.3f) / 0.2f, 0, 0);
                return new Vector3(10f * (p - 0.5f) / 0.3f, 0, 0);
            }, dur);

            // Arms — up for balance
            SetRotationAnim(clip, "shoulder.L", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(0, 0, 0);
                if (p < 0.5f) return new Vector3(20f * (p - 0.3f) / 0.2f, 0, 10f * (p - 0.3f) / 0.2f);
                return new Vector3(20f - 20f * (p - 0.5f) / 0.3f, 0, 10f - 10f * (p - 0.5f) / 0.3f);
            }, dur);
            SetRotationAnim(clip, "shoulder.R", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(0, 0, 0);
                if (p < 0.5f) return new Vector3(20f * (p - 0.3f) / 0.2f, 0, -10f * (p - 0.3f) / 0.2f);
                return new Vector3(20f - 20f * (p - 0.5f) / 0.3f, 0, -10f + 10f * (p - 0.5f) / 0.3f);
            }, dur);
        }

        // ================================================================
        // Player Gather — bend down, reach, pick up
        // ================================================================
        private void CreatePlayerGather(AnimationClip clip)
        {
            float dur = 1.2f;

            // Bend forward at waist
            SetRotationAnim(clip, "spine", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(30f * p / 0.3f, 0, 0);
                if (p < 0.7f) return new Vector3(30f, 0, 0);
                return new Vector3(30f - 30f * (p - 0.7f) / 0.3f, 0, 0);
            }, dur);
            SetRotationAnim(clip, "spine.001", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(20f * p / 0.3f, 0, 0);
                if (p < 0.7f) return new Vector3(20f, 0, 0);
                return new Vector3(20f - 20f * (p - 0.7f) / 0.3f, 0, 0);
            }, dur);
            SetRotationAnim(clip, "spine.003", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(15f * p / 0.3f, 0, 0);
                if (p < 0.7f) return new Vector3(15f, 0, 0);
                return new Vector3(15f - 15f * (p - 0.7f) / 0.3f, 0, 0);
            }, dur);

            // Right arm — reach down
            SetRotationAnim(clip, "shoulder.R", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(30f * p / 0.3f, 20f * p / 0.3f, 0);
                if (p < 0.7f) return new Vector3(30f, 20f, 0);
                return new Vector3(30f - 30f * (p - 0.7f) / 0.3f, 20f - 20f * (p - 0.7f) / 0.3f, 0);
            }, dur);
            SetRotationAnim(clip, "upper_arm.R", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(-40f * p / 0.3f, 0, 0);
                if (p < 0.7f) return new Vector3(-40f, 0, 0);
                return new Vector3(-40f + 40f * (p - 0.7f) / 0.3f, 0, 0);
            }, dur);
            SetRotationAnim(clip, "forearm.R", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(-50f * p / 0.3f, 0, 0);
                if (p < 0.7f) return new Vector3(-50f, 0, 0);
                return new Vector3(-50f + 50f * (p - 0.7f) / 0.3f, 0, 0);
            }, dur);

            // Slight knee bend
            SetRotationAnim(clip, "thigh.L", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(15f * p / 0.3f, 0, 0);
                if (p < 0.7f) return new Vector3(15f, 0, 0);
                return new Vector3(15f - 15f * (p - 0.7f) / 0.3f, 0, 0);
            }, dur);
            SetRotationAnim(clip, "shin.L", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(-20f * p / 0.3f, 0, 0);
                if (p < 0.7f) return new Vector3(-20f, 0, 0);
                return new Vector3(-20f + 20f * (p - 0.7f) / 0.3f, 0, 0);
            }, dur);
            SetRotationAnim(clip, "thigh.R", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(15f * p / 0.3f, 0, 0);
                if (p < 0.7f) return new Vector3(15f, 0, 0);
                return new Vector3(15f - 15f * (p - 0.7f) / 0.3f, 0, 0);
            }, dur);
            SetRotationAnim(clip, "shin.R", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(-20f * p / 0.3f, 0, 0);
                if (p < 0.7f) return new Vector3(-20f, 0, 0);
                return new Vector3(-20f + 20f * (p - 0.7f) / 0.3f, 0, 0);
            }, dur);
        }

        // ================================================================
        // Soldier animations (same bone structure as player)
        // ================================================================
        private void CreateSoldierIdle(AnimationClip clip)
        {
            // Soldier stands at attention — minimal movement
            SetRotationAnim(clip, "spine.001", t => new Vector3(
                0.5f * Mathf.Sin(t * Mathf.PI * 1f), 0, 0
            ), 2f);
            SetRotationAnim(clip, "spine.005", t => new Vector3(
                0, 1f * Mathf.Sin(t * Mathf.PI * 0.5f), 0
            ), 2f);
            // Arms at sides
            SetRotationAnim(clip, "shoulder.L", t => new Vector3(
                0, 0, 0.5f * Mathf.Sin(t * Mathf.PI * 0.7f)
            ), 2f);
            SetRotationAnim(clip, "shoulder.R", t => new Vector3(
                0, 0, -0.5f * Mathf.Sin(t * Mathf.PI * 0.7f)
            ), 2f);
        }

        private void CreateSoldierWalk(AnimationClip clip)
        {
            CreatePlayerWalk(clip); // Same walk but more stiff (smaller amplitudes)
        }

        private void CreateSoldierRun(AnimationClip clip)
        {
            CreatePlayerRun(clip); // Same run cycle
        }

        private void CreateSoldierAttack(AnimationClip clip)
        {
            CreatePlayerAttack(clip); // Same attack
        }

        // ================================================================
        // Monster animations (quadruped — simplified with fewer bones)
        // ================================================================
        private void CreateMonsterIdle(AnimationClip clip)
        {
            float dur = 2f;
            // Body sway
            SetRotationAnim(clip, "spine", t => new Vector3(
                0, 0, 1f * Mathf.Sin(t * Mathf.PI / dur)
            ), dur);
            SetRotationAnim(clip, "spine.001", t => new Vector3(
                1f * Mathf.Sin(t * Mathf.PI / dur), 0, 0.5f * Mathf.Sin(t * Mathf.PI / dur)
            ), dur);
            // Head look
            SetRotationAnim(clip, "spine.005", t => new Vector3(
                0, 3f * Mathf.Sin(t * Mathf.PI / dur * 0.5f), 0
            ), dur);
        }

        private void CreateMonsterWalk(AnimationClip clip)
        {
            float dur = 0.8f;
            float stride = 15f;

            // Body bob
            SetPositionAnim(clip, "Root", t => new Vector3(
                0, 0.1f * Mathf.Sin(t * Mathf.PI * 2f / dur), 0
            ), dur);

            // Body sway
            SetRotationAnim(clip, "spine", t => new Vector3(
                0, 3f * Mathf.Sin(t * Mathf.PI * 2f / dur), -2f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "spine.001", t => new Vector3(
                2f * Mathf.Sin(t * Mathf.PI * 2f / dur), 0, 0
            ), dur);
            SetRotationAnim(clip, "spine.005", t => new Vector3(
                2f * Mathf.Sin(t * Mathf.PI * 2f / dur), 0, 0
            ), dur);

            // Front legs (thigh.L = front left, thigh.R = front right)
            SetRotationAnim(clip, "thigh.L", t => new Vector3(
                stride * Mathf.Sin(t * Mathf.PI * 2f / dur), 0, 0
            ), dur);
            SetRotationAnim(clip, "shin.L", t => new Vector3(
                -10f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f / dur)) + 3f, 0, 0
            ), dur);

            SetRotationAnim(clip, "thigh.R", t => new Vector3(
                stride * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur), 0, 0
            ), dur);
            SetRotationAnim(clip, "shin.R", t => new Vector3(
                -10f * Mathf.Abs(Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)) + 3f, 0, 0
            ), dur);

            // Hind legs (pelvis + thigh equivalent)
            SetRotationAnim(clip, "shoulder.L", t => new Vector3(
                stride * Mathf.Sin((t + dur * 0.25f) * Mathf.PI * 2f / dur), 0, 0
            ), dur);
            SetRotationAnim(clip, "shoulder.R", t => new Vector3(
                stride * Mathf.Sin((t + dur * 0.75f) * Mathf.PI * 2f / dur), 0, 0
            ), dur);
        }

        private void CreateMonsterRun(AnimationClip clip)
        {
            float dur = 0.5f;
            float stride = 25f;

            SetPositionAnim(clip, "Root", t => new Vector3(
                0, 0.2f * Mathf.Sin(t * Mathf.PI * 2f / dur), 0
            ), dur);

            SetRotationAnim(clip, "spine", t => new Vector3(
                -5f, 5f * Mathf.Sin(t * Mathf.PI * 2f / dur), -3f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "spine.001", t => new Vector3(
                3f * Mathf.Sin(t * Mathf.PI * 2f / dur), 0, 0
            ), dur);
            SetRotationAnim(clip, "spine.005", t => new Vector3(
                3f * Mathf.Sin(t * Mathf.PI * 2f / dur), 0, 0
            ), dur);

            SetRotationAnim(clip, "thigh.L", t => new Vector3(
                stride * Mathf.Sin(t * Mathf.PI * 2f / dur), 0, 3f * Mathf.Sin(t * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "shin.L", t => new Vector3(
                -15f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f / dur)) + 5f, 0, 0
            ), dur);
            SetRotationAnim(clip, "thigh.R", t => new Vector3(
                stride * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur), 0, -3f * Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)
            ), dur);
            SetRotationAnim(clip, "shin.R", t => new Vector3(
                -15f * Mathf.Abs(Mathf.Sin((t + dur * 0.5f) * Mathf.PI * 2f / dur)) + 5f, 0, 0
            ), dur);

            SetRotationAnim(clip, "shoulder.L", t => new Vector3(
                stride * Mathf.Sin((t + dur * 0.25f) * Mathf.PI * 2f / dur), 0, 0
            ), dur);
            SetRotationAnim(clip, "shoulder.R", t => new Vector3(
                stride * Mathf.Sin((t + dur * 0.75f) * Mathf.PI * 2f / dur), 0, 0
            ), dur);
        }

        private void CreateMonsterAttack(AnimationClip clip)
        {
            float dur = 0.6f;

            // Lunge forward
            SetPositionAnim(clip, "Root", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(0, 0, 0.3f * p / 0.3f);
                if (p < 0.5f) return new Vector3(0, 0, 0.3f);
                return new Vector3(0, 0, 0.3f - 0.3f * (p - 0.5f) / 0.3f);
            }, dur);

            // Spine — rear up
            SetRotationAnim(clip, "spine", t => {
                float p = t / dur;
                if (p < 0.2f) return new Vector3(-10f * p / 0.2f, 0, 0);
                if (p < 0.5f) return new Vector3(-10f, 10f * Mathf.Sin((p - 0.2f) / 0.3f * Mathf.PI), 0);
                return new Vector3(-10f + 10f * (p - 0.5f) / 0.3f, 10f * Mathf.Sin(1f) - 10f * (p - 0.5f) / 0.3f, 0);
            }, dur);

            // Head — bite
            SetRotationAnim(clip, "spine.005", t => {
                float p = t / dur;
                if (p < 0.2f) return new Vector3(0, 0, 0);
                if (p < 0.5f) return new Vector3(-20f * (p - 0.2f) / 0.3f, 0, 0);
                return new Vector3(-20f + 20f * (p - 0.5f) / 0.3f, 0, 0);
            }, dur);

            // Front legs — swipe
            SetRotationAnim(clip, "thigh.L", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(20f * p / 0.3f, 0, 0);
                return new Vector3(20f - 20f * (p - 0.3f) / 0.3f, 0, 0);
            }, dur);
            SetRotationAnim(clip, "thigh.R", t => {
                float p = t / dur;
                if (p < 0.3f) return new Vector3(20f * p / 0.3f, 0, 0);
                return new Vector3(20f - 20f * (p - 0.3f) / 0.3f, 0, 0);
            }, dur);
        }

        // ================================================================
        // Animator Controllers
        // ================================================================
        private void CreateAllControllers()
        {
            CreatePlayerController();
            CreateGenericController("Soldier", "Soldier_Idle", "Soldier_Walk", "Soldier_Run", "Soldier_Attack");
            CreateGenericController("Monster", "Monster_Idle", "Monster_Walk", "Monster_Run", "Monster_Attack");
        }

        private void CreatePlayerController()
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH + "Player_Animator.controller");
            var rootStateMachine = controller.layers[0].stateMachine;

            // States
            var idleState = rootStateMachine.AddState("Idle");
            var walkState = rootStateMachine.AddState("Walk");
            var runState = rootStateMachine.AddState("Run");
            var attackState = rootStateMachine.AddState("Attack");
            var jumpState = rootStateMachine.AddState("Jump");
            var gatherState = rootStateMachine.AddState("Gather");

            // Assign clips
            idleState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + "Player_Idle.anim");
            walkState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + "Player_Walk.anim");
            runState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + "Player_Run.anim");
            attackState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + "Player_Attack.anim");
            jumpState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + "Player_Jump.anim");
            gatherState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + "Player_Gather.anim");

            // Parameters
            controller.AddParameter("State", AnimatorControllerParameterType.Int);
            controller.AddParameter("AttackTrigger", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("JumpTrigger", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("GatherTrigger", AnimatorControllerParameterType.Trigger);

            // Default state is Idle
            rootStateMachine.defaultState = idleState;

            // Transitions from Any State for triggers
            var anyAttack = rootStateMachine.AddAnyStateTransition(attackState);
            anyAttack.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "AttackTrigger");
            anyAttack.duration = 0.1f;

            var anyJump = rootStateMachine.AddAnyStateTransition(jumpState);
            anyJump.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "JumpTrigger");
            anyJump.duration = 0.1f;

            var anyGather = rootStateMachine.AddAnyStateTransition(gatherState);
            anyGather.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "GatherTrigger");
            anyGather.duration = 0.1f;

            // State-based transitions (Idle/Walk/Run)
            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 1, "State");
            idleToWalk.duration = 0.15f;

            var idleToRun = idleState.AddTransition(runState);
            idleToRun.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 2, "State");
            idleToRun.duration = 0.15f;

            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 0, "State");
            walkToIdle.duration = 0.15f;

            var walkToRun = walkState.AddTransition(runState);
            walkToRun.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 2, "State");
            walkToRun.duration = 0.2f;

            var runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 0, "State");
            runToIdle.duration = 0.2f;

            var runToWalk = runState.AddTransition(walkState);
            runToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 1, "State");
            runToWalk.duration = 0.2f;

            // Exit from Attack/Jump/Gather back to Idle
            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 0, "State");
            attackToIdle.duration = 0.1f;
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.9f;

            var jumpToIdle = jumpState.AddTransition(idleState);
            jumpToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 0, "State");
            jumpToIdle.duration = 0.1f;
            jumpToIdle.hasExitTime = true;
            jumpToIdle.exitTime = 0.9f;

            var gatherToIdle = gatherState.AddTransition(idleState);
            gatherToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 0, "State");
            gatherToIdle.duration = 0.1f;
            gatherToIdle.hasExitTime = true;
            gatherToIdle.exitTime = 0.9f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("✅ Player Animator Controller created");
        }

        private void CreateGenericController(string prefix, string idleClip, string walkClip, string runClip, string attackClip)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH + prefix + "_Animator.controller");
            var rootStateMachine = controller.layers[0].stateMachine;

            var idleState = rootStateMachine.AddState("Idle");
            var walkState = rootStateMachine.AddState("Walk");
            var runState = rootStateMachine.AddState("Run");
            var attackState = rootStateMachine.AddState("Attack");

            idleState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + idleClip + ".anim");
            walkState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + walkClip + ".anim");
            runState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + runClip + ".anim");
            attackState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_PATH + attackClip + ".anim");

            controller.AddParameter("State", AnimatorControllerParameterType.Int);
            controller.AddParameter("AttackTrigger", AnimatorControllerParameterType.Trigger);

            rootStateMachine.defaultState = idleState;

            var anyAttack = rootStateMachine.AddAnyStateTransition(attackState);
            anyAttack.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "AttackTrigger");
            anyAttack.duration = 0.1f;

            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 1, "State");
            idleToWalk.duration = 0.15f;

            var idleToRun = idleState.AddTransition(runState);
            idleToRun.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 2, "State");
            idleToRun.duration = 0.15f;

            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 0, "State");
            walkToIdle.duration = 0.15f;

            var walkToRun = walkState.AddTransition(runState);
            walkToRun.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 2, "State");
            walkToRun.duration = 0.2f;

            var runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 0, "State");
            runToIdle.duration = 0.2f;

            var runToWalk = runState.AddTransition(walkState);
            runToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 1, "State");
            runToWalk.duration = 0.2f;

            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, 0, "State");
            attackToIdle.duration = 0.1f;
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.9f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"✅ {prefix} Animator Controller created");
        }
    }
}