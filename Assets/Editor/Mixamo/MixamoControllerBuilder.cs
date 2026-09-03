using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// 믹사모 FBX 클립으로 4개 Animator Controller를 생성한다.
    /// Tools > Anim > Build Mixamo Controllers
    /// 출력: Assets/Resources/Animation/Controllers/*.controller (런타임 Resources.Load 가능)
    /// 파라미터 계약: Speed(float) / 트리거 Attack, AttackCombo, Hit, Death (+Player_AC: Roll, Jump)
    /// </summary>
    public static class MixamoControllerBuilder
    {
        const string MixamoDir = "Assets/Animations/Mixamo";
        const string OutDir = "Assets/Resources/Animation/Controllers";

        [MenuItem("Tools/Anim/Build Mixamo Controllers")]
        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(OutDir);
            BuildPlayer();
            BuildSoldier("SoldierShield", new[]
            {
                ("Idle", "Sword And Shield Block Idle.fbx"),
                ("Move", "Sword And Shield Run.fbx"),
                ("Attack", "Sword And Shield Slash.fbx"),
                ("Hit", "Sword And Shield Impact.fbx"),
                ("Death", "Sword And Shield Death.fbx"),
            }, player: false);
            BuildSoldier("SoldierGreatSword", new[]
            {
                ("Idle", "Sword And Shield Block Idle.fbx"),   // 대검 전용 Idle 미다운로드 → 공용
                ("Move", "Great Sword Run.fbx"),
                ("Attack", "Great Sword Slash.fbx"),
                ("Hit", "Great Sword Impact.fbx"),
                ("Death", "Two Handed Sword Death.fbx"),
            }, player: false);
            BuildSoldier("SoldierArcher", new[]
            {
                ("Idle", "Standing Aim Idle 02 Looking.fbx"),
                ("Move", "Standing Aim Walk Forward.fbx"),
                ("Attack", "Standing Draw Arrow.fbx"),
                ("Hit", "Standing React Small From Right.fbx"),
                ("Death", "Standing Death Backward 01.fbx"),
            }, player: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MixamoControllers] 4개 컨트롤러 생성 완료 → " + OutDir);
        }

        static AnimationClip Clip(string fileName)
        {
            var path = $"{MixamoDir}/{fileName}";
            var clips = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in clips)
                if (a is AnimationClip c && !c.name.StartsWith("__"))
                    return c;
            Debug.LogWarning($"[MixamoControllers] 클립 없음: {path}");
            return null;
        }

        static void BuildPlayer()
        {
            var ac = Create("Player_AC", new[]
            {
                ("Speed", AnimatorControllerParameterType.Float),
                ("Attack", AnimatorControllerParameterType.Trigger),
                ("AttackCombo", AnimatorControllerParameterType.Trigger),
                ("Roll", AnimatorControllerParameterType.Trigger),
                ("Jump", AnimatorControllerParameterType.Trigger),
                ("Hit", AnimatorControllerParameterType.Trigger),
                ("Death", AnimatorControllerParameterType.Trigger),
            });
            var sm = ac.layers[0].stateMachine;
            var idle = AddState(sm, "Idle", Clip("Idle.fbx"), true);
            var walk = AddState(sm, "Walk", Clip("Walking.fbx"));
            var run = AddState(sm, "Run", Clip("Running.fbx"));
            var roll = AddState(sm, "Roll", Clip("Quick Roll To Run.fbx"));
            var attack = AddState(sm, "Attack", Clip("Standing Melee Attack Horizontal.fbx"));
            var combo = AddState(sm, "AttackCombo", Clip("One Hand Sword Combo.fbx"));
            var jump = AddState(sm, "Jump", Clip("Standing Jump.fbx"));
            var hit = AddState(sm, "Hit", Clip("Standing React Small From Right.fbx"));
            var death = AddState(sm, "Death", Clip("Standing Death Backward 01.fbx"));

            // 이동: Idle ↔ Walk ↔ Run (Speed 기반)
            T(sm, idle, walk, "Speed", AnimatorConditionMode.Greater, 0.5f);
            T(sm, walk, idle, "Speed", AnimatorConditionMode.Less, 0.1f);
            T(sm, walk, run, "Speed", AnimatorConditionMode.Greater, 5.5f);
            // Run→Walk 임계를 4.0으로 낮춰 히스테리시스 확대 — 스프린트 중 상태 플리커(갑자기 멈춤) 방지
            T(sm, run, walk, "Speed", AnimatorConditionMode.Less, 4f);

            // 트리거 상태: Any State → 상태 (canTransitionToSelf=false) → Idle 복귀(exit time)
            AnyState(sm, roll, "Roll");
            ExitTo(sm, roll, idle);
            AnyState(sm, jump, "Jump");
            ExitTo(sm, jump, idle);
            AnyState(sm, hit, "Hit");
            ExitTo(sm, hit, idle);
            AnyState(sm, attack, "Attack");
            ExitTo(sm, attack, idle);
            AnyState(sm, combo, "AttackCombo");
            ExitTo(sm, combo, idle);
            AnyState(sm, death, "Death"); // 사망은 유지 (복귀 없음)

            AssetDatabase.SaveAssets();
            Debug.Log("[MixamoControllers] Player_AC 생성 완료");
        }

        static void BuildSoldier(string name, (string slot, string file)[] slots, bool player)
        {
            var ac = Create(name, new[]
            {
                ("Speed", AnimatorControllerParameterType.Float),
                ("Attack", AnimatorControllerParameterType.Trigger),
                ("Hit", AnimatorControllerParameterType.Trigger),
                ("Death", AnimatorControllerParameterType.Trigger),
            });
            var sm = ac.layers[0].stateMachine;
            var idle = AddState(sm, "Idle", Clip(SlotFile(slots, "Idle")), true);
            var move = AddState(sm, "Move", Clip(SlotFile(slots, "Move")));
            var attack = AddState(sm, "Attack", Clip(SlotFile(slots, "Attack")));
            var hit = AddState(sm, "Hit", Clip(SlotFile(slots, "Hit")));
            var death = AddState(sm, "Death", Clip(SlotFile(slots, "Death")));

            T(sm, idle, move, "Speed", AnimatorConditionMode.Greater, 0.5f);
            T(sm, move, idle, "Speed", AnimatorConditionMode.Less, 0.1f);
            AnyState(sm, attack, "Attack");
            ExitTo(sm, attack, idle);
            AnyState(sm, hit, "Hit");
            ExitTo(sm, hit, idle);
            AnyState(sm, death, "Death");

            AssetDatabase.SaveAssets();
            Debug.Log($"[MixamoControllers] {name}_AC 생성 완료");
        }

        static string SlotFile((string slot, string file)[] slots, string slotName)
        {
            foreach (var (slot, file) in slots)
                if (slot == slotName) return file;
            return slots[0].file;
        }

        static AnimatorController Create(string name, (string, AnimatorControllerParameterType)[] pars)
        {
            var ac = AnimatorController.CreateAnimatorControllerAtPath($"{OutDir}/{name}_AC.controller");
            foreach (var (p, t) in pars)
                ac.AddParameter(p, t);
            return ac;
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, bool isDefault = false)
        {
            var st = sm.AddState(name);
            st.motion = motion;
            st.writeDefaultValues = true;
            if (isDefault) sm.defaultState = st;
            return st;
        }

        static void T(AnimatorStateMachine sm, AnimatorState from, AnimatorState to,
            string param, AnimatorConditionMode mode, float threshold)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(mode, threshold, param);
        }

        static void AnyState(AnimatorStateMachine sm, AnimatorState to, string trigger)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }

        static void ExitTo(AnimatorStateMachine sm, AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.92f;
            t.duration = 0.12f;
        }
    }
}
