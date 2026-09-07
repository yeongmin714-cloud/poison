using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// DoubleL RPG팩(Assets/DoubleL/Demo/Anim) Humanoid 클립 + 믹사모 FBX 클립으로 4개 Animator Controller를 생성한다.
    /// 플레이어: 로코모션=믹사모(자연 이동), 전투=팩 OneHand(검 부착 전제).
    /// 병사: 검 든 유닛이므로 팩 OneHand 유지(Death, Roll, 활 Attack 등 팩에 없는 애니만 믹사모 유지).
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
            ConfigureMixamoClipLoop();   // 믹사모 클립 Loop Time 활성(비루프 클립은 1회 재생 후 마지막 프레임 동결)
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
            // 로코모션=믹사모(자연 이동), 전투=팩 OneHand(검 부착 전제)
            var idle = AddState(sm, "Idle", Clip("Idle.fbx"), true);
            var walk = AddState(sm, "Walk", Clip("Walking.fbx"));
            var run = AddState(sm, "Run", Clip("Running.fbx"));
            // Run 클립 재생속도 = Speed×0.28 — 믹사모 Running 자연 페이스 ~3.5m/s이므로 5m/s서 1.4배속(발 미끄러짐 방지). walk/idle은 미바인딩
            run.speedParameter = "Speed";
            run.speedParameterActive = true; // 미활성화 시 Speed 파라미터 바인딩 무시(고정 0.28배속) → 반드시 활성
            run.speed = 0.28f;
            var roll = AddState(sm, "Roll", Clip("Quick Roll To Run.fbx")); // 팩에 구르기 없음 → 믹사모 유지
            var attack = AddState(sm, "Attack", Clip("pack:OneHand_Up_Attack_1"));
            var combo = AddState(sm, "AttackCombo", Clip("pack:OneHand_Up_Attack_1"));
            var jump = AddState(sm, "Jump", Clip("Standing Jump.fbx"));
            var hit = AddState(sm, "Hit", Clip("pack:Hit_F_1"));
            var death = AddState(sm, "Death", Clip("Standing Death Backward 01.fbx")); // 팩에 Death 없음 → 믹사모 유지

            // 이동: Idle ↔ Walk ↔ Run (Speed 기반) — 히스테리시스: Idle→Walk는 0.55, Walk→Idle은 0.35로 분리
            // (지형/경사로 속도가 0 근처로 순간 떨어질 때 Idle로 떨어졌다 복귀하는 "끊김+멈춤" 방지)
            T(sm, idle, walk, "Speed", AnimatorConditionMode.Greater, 0.55f);
            T(sm, walk, idle, "Speed", AnimatorConditionMode.Less, 0.35f);
            // Walk→Run 임계 4.5 — 플레이어 일반 이동속도 5.0이므로 5.5였으면 Run 진입 불가(2026-09-05 DD판정). Run→Walk 2.0과 히스테리시스 유지
            T(sm, walk, run, "Speed", AnimatorConditionMode.Greater, 4.5f);
            // Run→Walk 임계 2.0 — 일반 이동 5.0의 경사 딥(3.5~4.8)이 임계(기존 4.0)와 겹쳐 진동 — 실제 정지(2.0 이하)만 Walk 복귀
            T(sm, run, walk, "Speed", AnimatorConditionMode.Less, 2f);

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

        /// <summary>
        /// 믹사모 FBX 클립의 Loop Time을 활성화한다.
        /// Mixamo FBX 임포트 기본값은 loopTime=false → Idle/Walk/Run이 1회 재생 후
        /// 마지막 프레임에 동결(56 포즈 증상). Roll/Death는 1회성 모션이므로 제외.
        /// </summary>
        static void ConfigureMixamoClipLoop()
        {
            string[] loopClips =
            {
                "Idle.fbx", "Walking.fbx", "Running.fbx", "Standing Jump.fbx",
            };
            int changed = 0;
            foreach (var f in loopClips)
            {
                string p = $"{MixamoDir}/{f}";
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null)
                {
                    Debug.LogWarning($"[MixamoControllers] 임포터 없음: {p}");
                    continue;
                }
                var clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    Debug.LogWarning($"[MixamoControllers] 클립 없음: {p}");
                    continue;
                }
                bool dirty = false;
                foreach (var c in clips)
                {
                    if (!c.loopTime) { c.loopTime = true; dirty = true; }
                }
                if (dirty)
                {
                    imp.clipAnimations = clips;
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            Debug.Log($"[MixamoControllers] 믹사모 클립 Loop Time 설정: {changed}개 FBX 재임포트");
        }

        static AnimatorController Create(string name, (string, AnimatorControllerParameterType)[] pars)
        {
            var path = $"{OutDir}/{name}_AC.controller";
            // ★ 덮어쓰기 보장(파일 레벨 스왑): CreateAnimatorControllerAtPath는 기존 에셋이 있으면
            //   덮어쓰지 않고 "Player_AC_AC" 같은 중복을 새로 만들어 버린다.
            //   AssetDatabase.DeleteAsset은 조용히 실패해 중복이 반복됨(09-03/09-07 사고)
            //   → 에셋DB 의존 제거: 파일+meta를 직접 삭제한 뒤 항상 단일 Player_AC.controller로 재생성.
            string fp = path;
            if (System.IO.File.Exists(fp))
            {
                System.IO.File.Delete(fp);
            }
            if (System.IO.File.Exists(fp + ".meta"))
            {
                System.IO.File.Delete(fp + ".meta");
            }
            // ★ 결정적: 파일 삭제 후 Refresh 없이 CreateAnimatorControllerAtPath를 호출하면
            //   에셋DB가 여전히 구 에셋을 "존재한다"고 판단해 Player_AC_AC 중복을 다시 만든다
            //   (09-07 10:04 사고 — 파일 삭제했음에도 재발). Refresh로 DB를 디스크와 동기화 필수.
            AssetDatabase.Refresh();
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            AssetDatabase.ImportAsset(path);
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
            t.duration = 0.12f; // 로코모션 전환 블렌드 0.12s — Idle↔Walk↔Run 클립 전환 끊김 완화(즉발 0.05는 애니 팝 유발)
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
