using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// 몬스터 22종 GLB 리깅 판별 진단 도구 (Phase 1).
    ///
    /// 실행: Unity 배치모드
    ///   -batchmode -nographics -quit -projectPath C:\Unity\code
    ///   -executeMethod ProjectName.EditorTools.MonsterRigScanTool.ScanAll
    ///   -logFile C:\Unity\code\TestOutput\monster_rig_scan_log.txt
    ///
    /// 각 모델에 대해 실제 프리팹 로드 기반으로 확정한다 (주석 추측 금지 원칙):
    ///  (a) RuntimeModelLoader.GetModelType(modelKey) — 기존 탐지기 판정
    ///  (b) 프리팹 Animator.avatar 유무 / isValid / isHuman — Player_AC(Humanoid 전용) 재생 가능 여부의
    ///      Unity 공식 판정 기준
    ///  (c) 골격 본 이름 휴머노이드 시그니처(Rigify/Mixamo 계열) — avatar가 없어도 골격이 사람형이면
    ///      런타임 Avatar 빌드(AvatarBuilder.BuildHumanAvatar)로 Player_AC 구동이 가능한 후보
    ///  (d) GLB 내장 AnimationClip / runtimeAnimatorController 보유 여부
    /// 결과는 로그 + TestOutput/monster_rig_scan.txt 파일로 동시 기록.
    /// </summary>
    public static class MonsterRigScanTool
    {
        /// <summary>MonsterSpawner.GetMonsterModelPath 매핑과 1:1 동일한 monsterId → GLB 키 (2026-09-11 기준).</summary>
        private static readonly string[][] MonsterMap =
        {
            new[] { "rabbit",             "Rabbit_Rigged" },
            new[] { "wolf",               "Wolf_Rigged" },
            new[] { "boar",               "Boar_Rigged" },
            new[] { "deer",               "Deer_Rigged" },
            new[] { "poison_snake",       "Snake_Rigged" },
            new[] { "bat",                "Bat_Rigged" },
            new[] { "giant_rat",          "Big_Mouse_Rigged" },
            new[] { "crow",               "Crow_Rigged" },
            new[] { "slime",              "Slime_Rigged" },
            new[] { "stone_golem",        "Golem_Rigged" },
            new[] { "fire_lizard",        "Fire_Lizard_Rigged" },
            new[] { "electric_porcupine", "Electric_Spine_Hedgehog_Rigged" },
            new[] { "swamp_croc",         "Swamp_Alligator_Rigged" },
            new[] { "forest_spirit",      "Wooden Forest Spirit" },
            new[] { "wild_troll",         "Wild_Troll_Rigged" },
            new[] { "ogre",               "Swamp_Ogre_Rigged" },
            new[] { "banshee",            "Banshee_Rigged" },
            new[] { "griffin",            "Griffon_Rigged" },
            new[] { "minotaur",           "Minotaur_Rigged" },
            new[] { "manticore",          "Manticore_Rigged" },
            new[] { "salamander",         "Salamander_Rigged" },
            new[] { "shadow_assassin",    "Shadow_Assassin_Rigged" },
        };

        /// <summary>캘리브레이션용 컨트롤 그룹 (판별기 정합성 검증 — 병사/플레이어/NPC).</summary>
        private static readonly string[][] CalibrationMap =
        {
            new[] { "soldier_fbx(병사 Humanoid FBX)", "fbx/soldier_lv1-20_rigged" },
            new[] { "player_glb(비휴머노이드 실증)",  "Player_Rigged" },
            new[] { "player_fbx(Humanoid FBX)",       "fbx/Player_Rigged_Heat" },
            new[] { "npc_lord_glb",                   "Npc_Lord_Rigged" },
        };

        private static readonly StringBuilder Report = new StringBuilder();

        /// <summary>배치모드 진입점 — 22종 몬스터 + 캘리브레이션 4종을 전부 스캔한다.</summary>
        public static void ScanAll()
        {
            try
            {
                Emit("=== Phase 1: 몬스터 리깅 판별 진단 (실제 프리팹 로드 기반) ===");
                Emit($"Unity={Application.unityVersion} Time={DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                int humanoidCount = 0;
                var humanoidIds = new List<string>();

                foreach (var pair in MonsterMap)
                {
                    bool humanoid = ScanOne(pair[0], pair[1]);
                    if (humanoid)
                    {
                        humanoidCount++;
                        humanoidIds.Add(pair[0]);
                    }
                }

                Emit("=== 캘리브레이션 (판별기 정합성 검증) ===");
                foreach (var pair in CalibrationMap)
                {
                    ScanOne(pair[0], pair[1]);
                }

                Emit($"SUMMARY humanoid={humanoidCount}/22 ids={string.Join(",", humanoidIds)}");
                Emit("SCAN_DONE");

                // 결과 파일 저장 (로그 파싱 실수 방지용 이중 기록)
                string outPath = Path.Combine(Application.dataPath, "..", "TestOutput", "monster_rig_scan.txt");
                File.WriteAllText(outPath, Report.ToString());
                Debug.Log($"[MonsterRigScan] 결과 저장: {outPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MonsterRigScan] 스캔 치명 오류: {ex}");
                Emit($"FATAL {ex.GetType().Name}: {ex.Message}");
                string outPath = Path.Combine(Application.dataPath, "..", "TestOutput", "monster_rig_scan.txt");
                try { File.WriteAllText(outPath, Report.ToString()); } catch { }
            }
        }

        /// <summary>모델 1개 스캔. 반환값 = 구동 가능 휴머노이드 여부(avatar.isHuman 또는 휴머노이드 골격).</summary>
        private static bool ScanOne(string id, string glbKey)
        {
            string prefabPath = "Models/UserProvided/" + glbKey;

            // (a) 기존 탐지기 판정 — RuntimeModelLoader 내부 캐시/지연로드 사용
            ModelType loaderType = ModelType.Static;
            bool loaderHas = false;
            try
            {
                loaderType = RuntimeModelLoader.GetModelType(glbKey);
                loaderHas = RuntimeModelLoader.HasModel(glbKey);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MonsterRigScan] GetModelType 실패({glbKey}): {ex.Message}");
            }

            // (b) 프리팹 직접 로드 → Animator 상태
            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Emit($"ROW|id={id}|glb={glbKey}|프리팹로드실패 path=Resources/{prefabPath}|loaderType={loaderType}({loaderHas})|humanoid=NO(로드실패)");
                return false;
            }

            Animator anim = prefab.GetComponentInChildren<Animator>(true);
            string avatarTxt = "NULL";
            bool avatarHuman = false;
            bool avatarValid = false;
            if (anim != null)
            {
                var av = anim.avatar;
                if (av != null)
                {
                    avatarValid = av.isValid;
                    avatarHuman = av.isHuman;
                    avatarTxt = $"{av.name}(valid={avatarValid},human={avatarHuman})";
                }
            }
            bool animIsHuman = anim != null && anim.isHuman;
            string ctrl = (anim != null && anim.runtimeAnimatorController != null)
                ? anim.runtimeAnimatorController.name : "NULL";

            // (c) 골격 휴머노이드 시그니처 — 본 이름 기반 구조 판정
            var bones = CollectBoneNames(prefab.transform);
            var sig = DetectHumanoidSignature(bones);

            // (d) GLB 내장 클립
            int clipCount = 0;
            string clipNames = "-";
            try
            {
                string assetPath = "Assets/Resources/Models/UserProvided/" + glbKey + ".glb";
                var clips = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var names = new List<string>();
                foreach (var o in clips)
                {
                    if (o is AnimationClip c && !(c is UnityEditor.Animations.AnimatorController)) { clipCount++; if (names.Count < 6) names.Add(c.name); }
                }
                clipNames = clipCount > 0 ? string.Join(",", names) : "-";
            }
            catch { }

            bool driveable = avatarHuman || sig.HumanoidSkeleton;
            Emit($"ROW|id={id}|glb={glbKey}|loaderType={loaderType}({loaderHas})|animator={(anim != null ? "OK" : "NULL")}|avatar={avatarTxt}|animIsHuman={animIsHuman}|ctrl={ctrl}|bones={bones.Count}|rigSig={sig.Tag}|sigHits={sig.Hits}|humanoid={(driveable ? "O" : "X")}|clips={clipCount}[{clipNames}]");
            return driveable;
        }

        /// <summary>프리팹 내 모든 Transform 이름(본 후보) 수집.</summary>
        private static List<string> CollectBoneNames(Transform root)
        {
            var list = new List<string>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root) list.Add(t.name);
            }
            return list;
        }

        /// <summary>휴머노이드 골격 시그니처 판정 결과.</summary>
        private struct SigResult
        {
            public string Tag;   // HumanoidSkeleton / MixamoSkeleton / BoneNumbered / 기타
            public string Hits;  // 매칭된 그룹 나열
            public bool HumanoidSkeleton => Tag == "HumanoidSkeleton" || Tag == "MixamoSkeleton";
        }

        /// <summary>
        /// 본 이름 집합에서 휴머노이드 골격을 구조 판정한다.
        /// 기준: 좌우 쌍 팔(upper_arm+forearm+hand) + 좌우 다리(thigh+shin+foot) + spine 체인 존재.
        /// 실증 근거: golem/troll/banshee/shadow_assassin GLB는 "spine/shoulder.L/upper_arm.L/forearm.L/hand.L/thigh.L/shin.L/foot.L" Rigify 계열 명명.
        /// </summary>
        private static SigResult DetectHumanoidSignature(List<string> boneNames)
        {
            var low = new HashSet<string>();
            foreach (var b in boneNames) low.Add(b.ToLowerInvariant());

            bool HasAny(params string[] keys)
            {
                foreach (var k in keys)
                    foreach (var b in low)
                        if (b.Contains(k)) return true;
                return false;
            }
            // 좌우 쌍 존재 검사 — ".l"/".l" 접미사 또는 "_l" 접미사
            bool HasPair(string leftKey, string rightKey)
            {
                return HasAny(leftKey) && HasAny(rightKey);
            }

            var hits = new List<string>();
            bool spineChain = HasAny("spine");
            bool shoulder = HasPair("shoulder.l", "shoulder.r");
            bool upperArm = HasPair("upper_arm.l", "upper_arm.r") || HasPair("upperarm_l", "upperarm_r") || HasPair("mixamorig:leftarm", "mixamorig:rightarm");
            bool lowerArm = HasPair("forearm.l", "forearm.r") || HasPair("lowerarm_l", "lowerarm_r") || HasPair("mixamorig:leftforearm", "mixamorig:rightforearm");
            bool hand = HasPair("hand.l", "hand.r") || HasPair("mixamorig:lefthand", "mixamorig:righthand");
            bool thigh = HasPair("thigh.l", "thigh.r") || HasPair("upperleg_l", "upperleg_r") || HasPair("mixamorig:leftupleg", "mixamorig:rightupleg");
            bool shin = HasPair("shin.l", "shin.r") || HasPair("lowerleg_l", "lowerleg_r") || HasPair("mixamorig:leftleg", "mixamorig:rightleg");
            bool foot = HasPair("foot.l", "foot.r") || HasPair("mixamorig:leftfoot", "mixamorig:rightfoot");
            bool mixamoPrefix = HasAny("mixamorig:");

            if (spineChain) hits.Add("spine");
            if (shoulder) hits.Add("shoulder");
            if (upperArm) hits.Add("upperArm");
            if (lowerArm) hits.Add("forearm");
            if (hand) hits.Add("hand");
            if (thigh) hits.Add("thigh");
            if (shin) hits.Add("shin");
            if (foot) hits.Add("foot");

            int limbScore = (shoulder ? 1 : 0) + (upperArm ? 1 : 0) + (lowerArm ? 1 : 0) + (hand ? 1 : 0)
                          + (thigh ? 1 : 0) + (shin ? 1 : 0) + (foot ? 1 : 0);

            if (mixamoPrefix && limbScore >= 6)
                return new SigResult { Tag = "MixamoSkeleton", Hits = string.Join(",", hits) };
            if (limbScore >= 6)   // 팔3 + 다리3 + (shoulder 부수) — 사람형 사지 완비
                return new SigResult { Tag = "HumanoidSkeleton", Hits = string.Join(",", hits) };

            // bone_N 넘버링 골격(비이름 기반) 판정
            int numbered = 0;
            foreach (var b in low)
                if (b.StartsWith("bone_") || b.StartsWith("bone") || System.Text.RegularExpressions.Regex.IsMatch(b, @"^neutral_bone\d*$")) numbered++;
            if (numbered >= boneNames.Count * 0.6 && boneNames.Count >= 10)
                return new SigResult { Tag = "BoneNumbered", Hits = $"numbered={numbered}/{boneNames.Count}" };

            return new SigResult { Tag = "기타", Hits = string.Join(",", hits) };
        }

        /// <summary>리포트 1행 기록 (로그 + 파일 이중화).</summary>
        private static void Emit(string line)
        {
            Report.AppendLine(line);
            Debug.Log("[MonsterRigScan] " + line);
        }
    }
}
