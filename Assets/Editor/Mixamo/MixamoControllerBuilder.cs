using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// DoubleL RPG팩(Assets/DoubleL/Demo/Anim) Humanoid 클립 + 믹사모 FBX 클립으로 4개 Animator Controller를 생성한다.
    /// 플레이어: 로코모션=유저 제공 FBX(MixamoUser, Heat 동일 리그→표준 본명 리네임, 2026-09-08 시험 부착), 전투=팩 OneHand(검 부착 전제).
    /// 병사: 검 든 유닛이므로 팩 OneHand 유지(Death, Roll, 활 Attack 등 팩에 없는 애니만 믹사모 유지).
    /// Tools > Anim > Build Mixamo Controllers
    /// 출력: Assets/Resources/Animation/Controllers/*.controller (런타임 Resources.Load 가능)
    /// 파라미터 계약: Speed(float) / 트리거 Attack, AttackCombo, Hit, Death (+Player_AC: Roll, Jump)
    /// </summary>
    public static class MixamoControllerBuilder
    {
        const string MixamoDir = "Assets/Animations/Mixamo";
        const string UserAnimDir = "Assets/Animations/MixamoUser"; // 유저 제공 로코모션 FBX(표준 Humanoid 본명 리네임 완료)
        const string MeshyUserDir = "Assets/Animations/MeshyUser"; // T-D3+: Meshy biped 60클립(표준 본명 리네임+클립 전용)
        const string PackDir = "Assets/DoubleL/Demo/Anim"; // DoubleL RPG팩 .anim 폴더 (전부 Humanoid 리그라 자동 리타겟)
        const string OutDir = "Assets/Resources/Animation/Controllers";

        [MenuItem("Tools/Anim/Build Mixamo Controllers")]
        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(OutDir);
            ConfigureMixamoClipLoop();   // 믹사모 클립 Loop Time 활성(비루프 클립은 1회 재생 후 마지막 프레임 동결)
            ConfigureUserAnimImports();  // 유저 제공 로코모션 FBX(MixamoUser) Humanoid 임포트 + Loop Time 보정
            ConfigureMeshyImports();     // T-D3+: Meshy 60클립 Humanoid 임포트 + Loop Time(규칙 기반)
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
            // "user:<파일>" → 유저 제공 FBX(MixamoUser) 내장 클립(표준 본명 리네임 완료) — 기존 믹사모 분기와 동일 패턴
            if (fileName.StartsWith("user:"))
            {
                var upath = $"{UserAnimDir}/{fileName.Substring(5)}";
                var uclips = AssetDatabase.LoadAllAssetsAtPath(upath);
                foreach (var a in uclips)
                    if (a is AnimationClip uc && !uc.name.StartsWith("__"))
                        return uc;
                Debug.LogWarning($"[MixamoControllers] 클립 없음: {upath}");
                return null;
            }
            // "meshy:<파일>" → Meshy 유저 FBX(MeshyUser, biped→표준 본명 리네임, 클립 전용)
            if (fileName.StartsWith("meshy:"))
            {
                var mpath = $"{MeshyUserDir}/{fileName.Substring(6)}";
                var mclips = AssetDatabase.LoadAllAssetsAtPath(mpath);
                foreach (var a in mclips)
                    if (a is AnimationClip mc && !mc.name.StartsWith("__"))
                        return mc;
                Debug.LogWarning($"[MixamoControllers] 클립 없음: {mpath}");
                return null;
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
            var ac = Create("Player", new[]
            {
                ("Speed", AnimatorControllerParameterType.Float),
                ("Attack", AnimatorControllerParameterType.Trigger),
                ("AttackCombo", AnimatorControllerParameterType.Trigger),
                ("Roll", AnimatorControllerParameterType.Trigger),
                ("Jump", AnimatorControllerParameterType.Trigger),
                ("Hit", AnimatorControllerParameterType.Trigger),
                ("Death", AnimatorControllerParameterType.Trigger),
                ("Harvest", AnimatorControllerParameterType.Trigger),
                ("HitLight", AnimatorControllerParameterType.Trigger),
                ("Stun", AnimatorControllerParameterType.Trigger),
            });
            var sm = ac.layers[0].stateMachine;
            // 로코모션=유저 제공 FBX(MixamoUser, Heat 동일 리그→표준 본명 리네임, 2026-09-08 시험 부착), 전투=팩 OneHand(검 부착 전제)
            var idle = AddState(sm, "Idle", Clip("meshy:Idle_02.fbx"), true); // T-D3+: 로코 4종 전량 Meshy(구 믹사모 로코는 MixamoUser에 보존)
            var walk = AddState(sm, "Walk", Clip("meshy:Walking.fbx"));
            var run = AddState(sm, "Run", Clip("meshy:Running.fbx"));
            // Run 클립 재생속도 = Speed×0.28 — 믹사모 Running 자연 페이스 ~3.5m/s이므로 5m/s서 1.4배속(발 미끄러짐 방지). walk/idle은 미바인딩
            run.speedParameter = "Speed";
            run.speedParameterActive = true; // 미활성화 시 Speed 파라미터 바인딩 무시(고정 0.28배속) → 반드시 활성
            run.speed = 0.28f;
            var roll = AddState(sm, "Roll", Clip("meshy:Roll_Dodge.fbx")); // T-D3+: Meshy 전투 클립(기존 믹사모/팩 클립은 병사가 계속 사용)
            var attack = AddState(sm, "Attack", Clip("meshy:Right_Hand_Sword_Slash.fbx"));
            var combo = AddState(sm, "AttackCombo", Clip("meshy:Double_Combo_Attack.fbx"));
            var jump = AddState(sm, "Jump", Clip("meshy:Regular_Jump.fbx")); // 신규 다운로드 Regular_Jump로 교체
            var hit = AddState(sm, "Hit", Clip("meshy:Hit_Reaction.fbx"));
            var death = AddState(sm, "Death", Clip("meshy:Dead.fbx"));
            // T-D3+: 신규 등록 상태(클립 준비 완료) — 발화는 각 시스템에서 SetTrigger(채집 UI/경직 판정) 연결 필요
            var harvest = AddState(sm, "Harvest", Clip("meshy:Pull_Radish.fbx"));
            var hitLight = AddState(sm, "HitLight", Clip("meshy:Slap_Reaction.fbx"));
            var stun = AddState(sm, "Stun", Clip("meshy:Electrocution_Reaction.fbx"));

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
            AnyState(sm, harvest, "Harvest");
            ExitTo(sm, harvest, idle);
            AnyState(sm, hitLight, "HitLight");
            ExitTo(sm, hitLight, idle);
            AnyState(sm, stun, "Stun");
            ExitTo(sm, stun, idle);

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

        /// <summary>
        /// 유저 제공 로코모션 FBX(Heat 동일 리그→표준 본명 리네임 완료, 2026-09-08)의 임포트를 보정한다.
        /// 1) animationType이 Humanoid가 아니면 Humanoid로 강제 — 표준 본명(Hips/Spine/Head/LeftUpperLeg...)이므로 자동매핑 성공.
        /// 2) clipAnimations loopTime: idle/walk/run/back_run/back_walk/jump=true, 좌우 방향전환=false(1회성 모션).
        /// ConfigureMixamoClipLoop와 동일 구현 패턴.
        /// </summary>
        /// <summary>
        /// T-D3+: Meshy 유저 FBX(MeshyUser, 60클립) 전량 Humanoid 임포트 + Loop Time 규칙 적용.
        /// loop=true: 파일명에 walk/run_/running/swim/crawl/carry/sneaky/spear/idle_turn 포함(순환 동작).
        /// loop=false: transition/toss/pitching 및 1회성 동작(공격/피격/사망/구르기/상호작용).
        /// 빈 clipAnimations → defaultClipAnimations 폴백(ConfigureUserAnimImports와 동일 패턴).
        /// </summary>
        static void ConfigureMeshyImports()
        {
            string[] loopKeys = { "walk", "run_", "running", "swim", "crawl", "carry", "sneaky", "spear", "idle_turn", "idle" };
            string[] noLoopKeys = { "transition", "toss", "pitching" };
            int changed = 0, scanned = 0;
            foreach (var fp in System.IO.Directory.GetFiles(MeshyUserDir, "*.fbx"))
            {
                string p = fp.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) { Debug.LogWarning($"[MixamoControllers] 임포터 없음: {p}"); continue; }
                scanned++;
                bool dirty = false;
                if (imp.animationType != ModelImporterAnimationType.Human)
                {
                    imp.animationType = ModelImporterAnimationType.Human;
                    dirty = true;
                }
                var clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0)
                    clips = imp.defaultClipAnimations;   // 테이크 자동 생성 정의 폴백
                if (clips == null || clips.Length == 0) { Debug.LogWarning($"[MixamoControllers] 클립 없음: {p}"); continue; }
                string low = System.IO.Path.GetFileName(p).ToLowerInvariant();
                bool loop = false;
                foreach (var k in loopKeys) if (low.Contains(k)) { loop = true; break; }
                foreach (var k in noLoopKeys) if (low.Contains(k)) { loop = false; break; }
                foreach (var c in clips)
                    if (c.loopTime != loop) { c.loopTime = loop; dirty = true; }
                if (dirty) { imp.clipAnimations = clips; imp.SaveAndReimport(); changed++; }
            }
            Debug.Log($"[MixamoControllers] Meshy 임포트 보정: {scanned}개 검사, {changed}개 재임포트");
        }

        static void ConfigureUserAnimImports()
        {
            (string fbx, bool loop)[] userClips =
            {
                ("idle.fbx", true),
                ("walk.fbx", true),
                ("run.fbx", true),
                ("back_run.fbx", true),
                ("back_walk.fbx", true),
                ("jump.fbx", true),
                ("left_change_direction.fbx", false),
                ("right_change_direction.fbx", false),
            };
            int changed = 0;
            foreach (var (fbx, loop) in userClips)
            {
                string p = $"{UserAnimDir}/{fbx}";
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null)
                {
                    Debug.LogWarning($"[MixamoControllers] 임포터 없음: {p}");
                    continue;
                }
                bool dirty = false;
                // 주의: Unity 6000.4에서는 ModelImporterAnimationType.Humanoid가 Human으로 리네임됨(같은 의미)
                if (imp.animationType != ModelImporterAnimationType.Human)
                {
                    imp.animationType = ModelImporterAnimationType.Human;
                    dirty = true;
                }
                var clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0)
                    clips = imp.defaultClipAnimations;   // 테이크 자동 생성 정의로 폴백(서브에셋 클립은 존재함 — 09-08 진단)
                if (clips == null || clips.Length == 0)
                {
                    Debug.LogWarning($"[MixamoControllers] 클립 없음: {p}");
                    continue;
                }
                foreach (var c in clips)
                {
                    if (c.loopTime != loop) { c.loopTime = loop; dirty = true; }
                }
                if (dirty)
                {
                    imp.clipAnimations = clips;
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            Debug.Log($"[MixamoControllers] 유저 FBX(MixamoUser) 임포트 보정: {changed}개 재임포트");
        }

        static AnimatorController Create(string name, (string, AnimatorControllerParameterType)[] pars)
        {
            var path = $"{OutDir}/{name}_AC.controller";
            // ★ 덮어쓰기 보장: CreateAnimatorControllerAtPath는 기존 에셋이 있으면 덮어쓰지 않고
            //   "Player_AC_AC" 같은 중복을 새로 만들어 버린다(09-03/09-07/09-08 사고).
            // 09-08 배치모드 Player_AC_AC 중복 재발 수리: 파일삭제+Refresh 단독 불충분 → 잔존 검증 루프 + 최종 경로 검증 로깅
            //   배치모드 R1에서 파일 삭제+Refresh 후에도 에셋DB가 구 에셋을 잔존 판정해 중복 생성됨
            //   → 삭제 후 LoadMainAssetAtPath로 잔존 검증, 잔존 시 AssetDatabase.DeleteAsset로 강제 제거(최대 3회).
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
                if (System.IO.File.Exists(path + ".meta"))
                    System.IO.File.Delete(path + ".meta");
                AssetDatabase.Refresh();
                var stale = AssetDatabase.LoadMainAssetAtPath(path);
                if (stale == null)
                    break;
                Debug.LogWarning($"[MixamoControllers] 기존 에셋 잔존(시도 {attempt}) → AssetDatabase.DeleteAsset: {path}");
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            AssetDatabase.ImportAsset(path);
            // ★ 생성 직후 경로 검증: 중복 생성(Player_AC_AC 등)이 발생하면 즉시 로그로 확정
            var finalPath = AssetDatabase.GetAssetPath(ac);
            if (finalPath != path)
                Debug.LogError($"[MixamoControllers] 컨트롤러 경로 불일치! expected={path} actual={finalPath} (중복 생성)");
            else
                Debug.Log($"[MixamoControllers] 컨트롤러 생성 경로 확인: {finalPath}");
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
