using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// DoubleL RPG팩(Assets/DoubleL/Demo/Anim) Humanoid 클립으로 4개 Animator Controller를 생성한다.
    /// 이 팩에 있는 애니(Idle/Walk/Run/Attack/Jump/Hit)는 pack: 프리픽스로 팩 클립을 사용하고,
    /// 이 팩에 없는 애니(Death, Roll, 활 Attack 등)는 믹사모 FBX 클립을 유지한다.
    /// Tools > Anim > Build Mixamo Controllers
    /// 출력: Assets/Resources/Animation/Controllers/*.controller (런타임 Resources.Load 가능)
    /// 파라미터 계약: Speed(float) / 트리거 Attack, AttackCombo, Hit, Death (+Player_AC: Roll, Jump)
    /// </summary>
    public static class MixamoControllerBuilder
    {
        const string MixamoDir = "Assets/Animations/Mixamo";
        const string PackDir = "Assets/DoubleL/Demo/Anim"; // DoubleL RPG팩 .anim 폴더 (전부 Humanoid 리그라 자동 리타겟)
        const string OutDir = "Assets/Resources/Animation/Controllers";

        [MenuItem("Tools/Anim/Build Mixamo Controllers")]
        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(OutDir);
            BuildPlayer();
            BuildSoldier("SoldierShield", new[]
            {
                ("Idle", "pack:OneHand_Up_Idle"),
                ("Move", "pack:OneHand_Up_Run_B"),
                ("Attack", "pack:OneHand_Up_Attack_1"),
                ("Hit", "pack:Hit_F_1"),
                ("Death", "Sword And Shield Death.fbx"), // 팩에 Death 없음 → 믹사모 유지
            }, player: false);
            BuildSoldier("SoldierGreatSword", new[]
            {
                ("Idle", "pack:OneHand_Up_Idle"),   // 대검 전용 클립 없음 → 한손검 계열 통일
                ("Move", "pack:OneHand_Up_Run_B"),
                ("Attack", "pack:OneHand_Up_Attack_1"),
                ("Hit", "pack:Hit_F_1"),
                ("Death", "Two Handed Sword Death.fbx"), // 팩에 Death 없음 → 믹사모 유지
            }, player: false);
            BuildSoldier("SoldierArcher", new[]
            {
                ("Idle", "pack:OneHand_Up_Idle"),   // 활 전용 .anim 부재 → 사람형 Idle은 OneHand 계열
                ("Move", "pack:OneHand_Up_Walk_B"),
                ("Attack", "Standing Draw Arrow.fbx"), // 활 Attack은 .anim 부재 → 믹사모 유지
                ("Hit", "pack:Hit_F_1"),
                ("Death", "Standing Death Backward 01.fbx"), // 팩에 Death 없음 → 믹사모 유지
            }, player: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MixamoControllers] 4개 컨트롤러 생성 완료 → " + OutDir);
        }

        static AnimationClip Clip(string fileName)
        {
            // "pack:<이름>" → DoubleL RPG팩 .anim 클립 (guid 지도와 동일 에셋)
            if (fileName.StartsWith("pack:"))
            {
                var packPath = $"{PackDir}/{fileName.Substring(5)}.anim";
                var pc = AssetDatabase.LoadAssetAtPath<AnimationClip>(packPath);
                if (pc == null)
                    Debug.LogWarning($"[MixamoControllers] 클립 없음: {packPath}");
                return pc;
            }
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
            var idle = AddState(sm, "Idle", Clip("pack:OneHand_Up_Idle"), true);
            var walk = AddState(sm, "Walk", Clip("pack:OneHand_Up_Walk_B"));
            var run = AddState(sm, "Run", Clip("pack:OneHand_Up_Run_B"));
            var roll = AddState(sm, "Roll", Clip("Quick Roll To Run.fbx")); // 팩에 구르기 없음 → 믹사모 유지
            var attack = AddState(sm, "Attack", Clip("pack:OneHand_Up_Attack_1"));
            var combo = AddState(sm, "AttackCombo", Clip("pack:OneHand_Up_Attack_1"));
            var jump = AddState(sm, "Jump", Clip("pack:OneHand_Up_Jump_B"));
            var hit = AddState(sm, "Hit", Clip("pack:Hit_F_1"));
            var death = AddState(sm, "Death", Clip("Standing Death Backward 01.fbx")); // 팩에 Death 없음 → 믹사모 유지

            // 이동: Idle ↔ Walk ↔ Run (Speed 기반) — 히스테리시스: Idle→Walk는 0.55, Walk→Idle은 0.35로 분리
            // (지형/경사로 속도가 0 근처로 순간 떨어질 때 Idle로 떨어졌다 복귀하는 "끊김+멈춤" 방지)
            T(sm, idle, walk, "Speed", AnimatorConditionMode.Greater, 0.55f);
            T(sm, walk, idle, "Speed", AnimatorConditionMode.Less, 0.35f);
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
            var path = $"{OutDir}/{name}_AC.controller";
            // ★ 덮어쓰기 보장: CreateAnimatorControllerAtPath는 기존 에셋이 있으면
            //   덮어쓰지 않고 "Player_AC_AC" 같은 중복을 새로 만들어 버린다.
            //   (09-03 사고: 팩 클립 컨트롤러가 Player_AC_AC.controller로 저장돼
            //    GameSetup이 로드하는 Player_AC.controller엔 믹사모 클립이 남음)
            //   → 기존 컨트롤러를 먼저 삭제하고 항상 단일 Player_AC.controller로 재생성.
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
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
            t.duration = 0.05f; // 이동 전환은 빠르게(0.15→0.05) — 멈춤/출발 모션 지연 감소
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
