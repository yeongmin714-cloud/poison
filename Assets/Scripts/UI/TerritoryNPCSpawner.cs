using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.Core.Utils;
using ProjectName.Systems.Animation;
using UnityEngine;

namespace ProjectName.UI
{
    [System.Serializable]
    public struct NPCInstance
    {
        public string NpcId;
        public string NpcName;
        public NPCData.NPCAgeType AgeType;
        public string Greeting;
        public string QuestOfferLine;
        public List<string> QuestIds;
        public List<string> Dialogues;
        public string TerritoryId;
        public int NpcIndex;

        public bool HasQuests => QuestIds != null && QuestIds.Count > 0;
    }

    public static class TerritoryNPCSpawner
    {
        public const string ParentObjectName = "___TerritoryNPCs___";

        // 12가지 인간 옷감 색상 팔레트
        private static readonly Color[] _npcPalette = new Color[]
        {
            new Color(0.20f, 0.40f, 0.80f), // 파랑
            new Color(0.80f, 0.25f, 0.25f), // 빨강
            new Color(0.15f, 0.60f, 0.30f), // 초록
            new Color(0.90f, 0.70f, 0.10f), // 노랑
            new Color(0.60f, 0.30f, 0.70f), // 보라
            new Color(1.00f, 0.55f, 0.00f), // 주황
            new Color(0.20f, 0.60f, 0.70f), // 청록
            new Color(0.75f, 0.40f, 0.15f), // 갈색
            new Color(0.90f, 0.50f, 0.70f), // 분홍
            new Color(0.30f, 0.30f, 0.30f), // 회색
            new Color(0.50f, 0.70f, 0.90f), // 하늘
            new Color(0.70f, 0.50f, 0.30f), // 카키
        };

        // 캐싱: 부모 오브젝트를 매번 Find하지 않음
        private static GameObject _cachedParent;

        public static List<NPCInstance> GenerateNPCs(string territoryId, int tier)
        {
            int npcCount = tier switch { 1 => 2, 2 => 3, 3 => 4, 4 => 4, 5 => 5, _ => 2 };

            var npcs = new List<NPCInstance>(npcCount);

            for (int i = 0; i < npcCount; i++)
            {
                string npcId = $"{territoryId}_npc_{i:D2}";
                NPCData.NPCAgeType ageType = NPCData.PickAgeType(territoryId, i, tier);
                string npcName = NPCData.PickName(territoryId, i, ageType);
                string greeting = NPCData.PickGreeting(territoryId, i, ageType);
                string questOffer = NPCData.PickQuestOffer(territoryId, i, ageType);

                var questIds = TerritoryQuestDefinitions.PickQuestIdsForNPC(territoryId, i, tier);

                var npc = new NPCInstance
                {
                    NpcId = npcId,
                    NpcName = npcName,
                    AgeType = ageType,
                    Greeting = greeting,
                    QuestOfferLine = questOffer,
                    QuestIds = questIds,
                    Dialogues = new List<string>
                    {
                        greeting,
                        questOffer,
                        "(NPC가 조용히 생각에 잠겼다.)"
                    },
                    TerritoryId = territoryId,
                    NpcIndex = i
                };

                npcs.Add(npc);
            }

            return npcs;
        }

        public static GameObject SpawnNPC(NPCInstance npc, Vector3 position)
        {
            if (_cachedParent == null)
            {
                _cachedParent = GameObject.Find(ParentObjectName);
                if (_cachedParent == null)
                {
                    _cachedParent = new GameObject(ParentObjectName);
                    Object.DontDestroyOnLoad(_cachedParent);
                }
            }

            GameObject npcGO = new GameObject(npc.NpcName);
            npcGO.transform.SetParent(_cachedParent.transform);
            npcGO.transform.position = position;

            var behaviour = npcGO.AddComponent<TerritoryNPCBehaviour>();
            behaviour.Initialize(npc);

            // Try to load real NPC GLB model
            string npcKey = GetNPCKey(npc);
            if (RuntimeModelLoader.TryGetModel(npcKey, out var npcModel))
            {
                // 2026-09-11: NPC도 병사와 동일 경로로 교체(Humanoid FBX 골격 + SoldierShield_AC + HumanoidClipDriver(Soldier)).
                // NPC GLB는 Humanoid avatar가 없어(Phase 1 스캔: animator=NULL, avatar=NULL, animIsHuman=False)
                // Player_AC/HumanoidClipDriver 직결이 불가능하므로, 검증된 병사 Humanoid FBX 리그
                // (TestTerritoryCombatSetup.CreateGuardVisual 674~730행 패턴)를 빌려 쓰고,
                // CopyMaterialsFromGlb로 NPC GLB 재질을 이식해 외형은 NPC 그대로 유지한다(사람 사지 Idle/걷기).
                // NPC는 공격하지 않으므로 드라이버는 걷기/대기만 사용(TriggerAttack 불필요 — 부착은 해도 무해).
                // 실패/예외 시 기존 GLB+ModelAnimatorAssigner.ForceBiped 경로로 폴백(회귀 0, 크래시 금지).
                try
                {
                    if (TryAttachSoldierHumanoidBody(npcGO, npc.NpcName, npcKey))
                        return npcGO;   // FBX 골격 교체 + GLB 재질 이식 성공
                    Debug.LogWarning($"[TerritoryNPCSpawner] ⚠️ {npc.NpcName} Humanoid FBX 교체 실패 — 기존 GLB 경로로 폴백");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[TerritoryNPCSpawner] ⚠️ {npc.NpcName} Humanoid FBX 교체 예외 — 기존 GLB 경로로 폴백: {ex.Message}");
                    // 부분 장착 잔재 정리 후 폴백(중복 본체 방지)
                    var leftoverBody = npcGO.transform.Find(npc.NpcName + "_Body");
                    if (leftoverBody != null) Object.Destroy(leftoverBody.gameObject);
                    var leftoverDriver = npcGO.GetComponent<HumanoidClipDriver>();
                    if (leftoverDriver != null) Object.Destroy(leftoverDriver);
                }

                // 기존 GLB 경로(폴백) — ForceBiped 프로시저럴 (회귀 없음)
                var instance = Object.Instantiate(npcModel, npcGO.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                // NEW: ModelAnimatorAssigner 부착 (ForceBiped)
                var assigner = instance.AddComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();
                assigner.ForceBiped(true);
                return npcGO;
            }

            // NPC ID 기반 시드로 고유 색상 선택 (같은 NPC = 항상 같은 색상)
            Color bodyColor = GetColorForNPC(npc.NpcId);
            Color skinColor = new Color(1.0f, 0.8f, 0.6f); // 기본 살색
            const float scale = 1.0f;

            // 몸통 (Cube)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(npcGO.transform, false);
            body.transform.localPosition = new Vector3(0, 0.5f * scale, 0);
            body.transform.localScale = new Vector3(0.6f * scale, 0.6f * scale, 0.4f * scale);
            body.GetComponent<MeshRenderer>().material = MaterialHelper.CreateLitMaterial(bodyColor, npc.NpcName + "_Body");

            // 머리 (Sphere)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(npcGO.transform, false);
            head.transform.localPosition = new Vector3(0, 1.05f * scale, 0);
            head.transform.localScale = new Vector3(0.35f * scale, 0.35f * scale, 0.35f * scale);
            head.GetComponent<MeshRenderer>().material = MaterialHelper.CreateLitMaterial(skinColor, npc.NpcName + "_Head");

            return npcGO;
        }

        /// <summary>
        /// NPC 본체를 병사와 동일한 Humanoid FBX 골격으로 교체한다(2026-09-11).
        /// 검증 패턴: TestTerritoryCombatSetup.CreateGuardVisual 674~730행(병사 경로)과 동일 계약.
        ///  1) Resources에서 공통 병사 FBX(soldier_lv1-20_rigged — Humanoid avatar 포함) 로드
        ///  2) npcGO 하위에 인스턴스 부착(이름 "{npcName}_Body", localPosition zero/rotation identity/scale one)
        ///  3) FBX 하위 Collider 전부 제거(루트 BoxCollider 없음이면 그대로)
        ///  4) 레거시 프로시저럴 계열 정리(TestPlayerAnimatorBoot.StripLegacyAnimation 패턴)
        ///  5) Animator + SoldierShield_AC 부착(applyRootMotion=false, AlwaysAnimate)
        ///  6) HumanoidClipDriver(mode=Soldier) npcGO 루트에 부착(걷기/대기만)
        ///  7) CopyMaterialsFromGlb로 NPC GLB 재질 이식(외형 유지)
        /// 성공 시 fbxBody 반환, 실패 시 null 반환 → 호출부가 기존 GLB+ForceBiped 경로로 폴백.
        /// 주의: GroundModelToY는 적용하지 않음 — 현재 NPC는 y=position 고정 계약(상태 유지).
        /// </summary>
        /// <param name="npcGO">NPC 루트 오브젝트</param>
        /// <param name="npcName">로그/본체 명명용 NPC 이름</param>
        /// <param name="glbAliasKey">CopyMaterialsFromGlb에 넘길 GLB 리소스 키.
        /// 런타임모델로더(RuntimeModelLoader) 소문자 alias("npc_man1_rigged" 등)를 그대로 사용한다.
        /// 실제 파일명은 NPC_Man1_Rigged.glb(대소문자 혼용)이지만, RuntimeModelLoader.LoadModelByKey가
        /// "Models/UserProvided/"+소문자키 조합으로 로드 성공이 확인된 값이므로(Windows 에디터 대소문자
        /// 무관 해석) 동일 키를 쓰는 것이 파일명 대소문자 불일치 리스크가 가장 낮음. 여기에
        /// "Models/UserProvided/" 접두사를 붙여 전체 리소스 경로로 전달한다.</param>
        /// <returns>부착된 FBX 본체(성공) 또는 null(실패)</returns>
        private static GameObject TryAttachSoldierHumanoidBody(GameObject npcGO, string npcName, string glbAliasKey)
        {
            // 1) 공통 병사 Humanoid FBX 로드(NPC는 남/여/노인 혼합이므로 lv1 공통 FBX 사용 — 지침 고정 경로)
            var fbxPrefab = Resources.Load<GameObject>("Models/UserProvided/fbx/soldier_lv1-20_rigged");
            if (fbxPrefab == null)
            {
                Debug.LogWarning("[TerritoryNPCSpawner] 병사 Humanoid FBX 미로드 — GLB 폴백 대상: Models/UserProvided/fbx/soldier_lv1-20_rigged");
                return null;
            }

            // 2) FBX 골격 부착
            var fbxBody = Object.Instantiate(fbxPrefab, npcGO.transform);
            fbxBody.name = $"{npcName}_Body";
            fbxBody.transform.localPosition = Vector3.zero;
            fbxBody.transform.localRotation = Quaternion.identity;
            fbxBody.transform.localScale = Vector3.one;

            // 3) FBX 하위 Collider 전부 제거(루트 BoxCollider 없음이면 그대로 — 병사 699~701행 패턴)
            var cols = fbxBody.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c != null) Object.DestroyImmediate(c);
            }

            // 4) 레거시 프로시저럴 계열 정리(병사 StripLegacyAnimation 패턴 — FBX 신규 인스턴스엔 원래 없음, npcGO 루트 포함 방어적 제거)
            StripLegacyAnimation(npcGO.transform);

            // 5) Animator + SoldierShield_AC(방패병사 컨트롤러 — 병사 704~709행과 동일)
            var anim = fbxBody.GetComponent<Animator>();
            if (anim == null) anim = fbxBody.AddComponent<Animator>();
            var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // (c) avatar 유효성/매핑 확인 로그(병사 711~720행 판별 기준과 동일 — Humanoid 임포트 FBX는
            //     루트 Animator에 imported humanoid avatar가 함께 온다). 무효면 Soldier_AC 재생 불가.
            //     병사 로직처럼 로그 후 계속 진행(재질 이식/드라이버는 유효).
            bool avatarOk = anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman;
            Debug.Log($"[TerritoryNPCSpawner] 🧍 {npcName} avatar={(anim.avatar != null ? anim.avatar.name : "NULL")}"
                + $" isValid={(anim.avatar != null ? anim.avatar.isValid.ToString() : "-")}"
                + $" isHuman={(anim.avatar != null ? anim.avatar.isHuman.ToString() : "-")}"
                + $" controller={(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}");
            if (!avatarOk)
                Debug.LogWarning($"[TerritoryNPCSpawner] ⚠️ {npcName} Humanoid avatar 무효 — Soldier_AC 재생 불가(T포즈) 가능성");

            // 6) 병사 모드 드라이버(NPC 루트에 부착 — 병사 723~724행. 공격 없음 → 걷기/대기만 사용)
            var driver = npcGO.AddComponent<HumanoidClipDriver>();
            driver.mode = HumanoidClipDriver.DriveMode.Soldier;

            // 7) NPC GLB 재질 이식(외형 유지) — fbxBody에는 FBX 인스턴스를 넘기고, GLB는
            //    CopyMaterialsFromGlb 내부의 Resources.Load용 프리팹 경로를 넘긴다(인스턴스 재생성 불필요).
            HumanoidClipDriver.CopyMaterialsFromGlb(fbxBody, "Models/UserProvided/" + glbAliasKey);

            Debug.Log($"[TerritoryNPCSpawner] ✅ NPC Humanoid FBX 부착: {npcName} ← soldier_lv1-20_rigged (SoldierShield_AC+드라이버, GLB 재질 이식={glbAliasKey})");
            return fbxBody;
        }

        /// <summary>
        /// 레거시 애니 컴포넌트 전부 제거(TestPlayerAnimatorBoot.StripLegacyAnimation 패턴 — 루트 포함 전체 자식).
        /// 런타임이므로 Destroy 사용(프레임 말 일괄 파괴 — RequireComponent 의존 무관 제거 가능).
        /// ProceduralBoneMap은 여러 컴포넌트의 RequireComponent 의존 대상이므로 마지막에 제거.
        /// </summary>
        private static void StripLegacyAnimation(Transform root)
        {
            DestroyAll<ProjectName.Systems.Animation.ModelAnimatorAssigner>(root);
            DestroyAll<ProjectName.Systems.Animation.Procedural.ProceduralAnimationController>(root);
            DestroyAll<ProjectName.Systems.Animation.Neural.NeuralAnimationController>(root);
            DestroyAll<ProjectName.Systems.Animation.Neural.HybridAnimationController>(root);
            DestroyAll<ProjectName.Systems.Animation.Procedural.Bones.ProceduralBoneMap>(root); // 마지막
        }

        private static void DestroyAll<T>(Transform root) where T : Component
        {
            var comps = root.GetComponentsInChildren<T>(true);
            foreach (var c in comps)
            {
                if (c != null) Object.Destroy(c);
            }
        }

        /// <summary>
        /// NPC 인스턴스에서 사용할 GLB 모델 키를 반환합니다.
        /// NPC 인덱스를 기반으로 다양한 NPC 외형을 순환합니다.
        /// </summary>
        private static string GetNPCKey(NPCInstance npc)
        {
            // Use npcIndex to cycle through NPC visual types
            string[] npcTypes = { "Man1", "Man2", "Girl1", "Girl2", "Girl3", "Oldman1", "Oldman2" };
            string npcType = npcTypes[npc.NpcIndex % npcTypes.Length];
            switch (npcType)
            {
                case "Man1": return "npc_man1_rigged";
                case "Man2": return "npc_man2_rigged";
                case "Girl1": return "npc_girl1_rigged";
                case "Girl2": return "npc_girl2_rigged";
                case "Girl3": return "npc_girl3_rigged";
                case "Oldman1": return "npc_oldman1_rigged";
                case "Oldman2": return "npc_oldman2_rigged";
                default: return "npc_man1_rigged";
            }
        }

        private static Color GetColorForNPC(string npcId)
        {
            int seed = StableHash(npcId) ^ 0x3C5EED1F;
            var rng = new System.Random(seed);
            int idx = rng.Next(0, _npcPalette.Length);

            // 약간의 밝기 변형
            float brightnessVariation = (float)(rng.NextDouble() * 0.2f - 0.1f);
            Color c = _npcPalette[idx];
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            v = Mathf.Clamp01(v + brightnessVariation);
            return Color.HSVToRGB(h, s, v);
        }

        public static Vector3 GetSpawnPosition(string territoryId, int npcIndex, Vector3 territoryCenter)
        {
            string seedKey = territoryId + "_npc" + npcIndex + "_spawn";
            int seed = StableHash(seedKey) ^ unchecked((int)0x5EED_1234);
            var rng = new System.Random(seed);
            float angle = (float)(rng.NextDouble() * 360.0);
            float distance = (float)(3.0 + rng.NextDouble() * 5.0);

            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * distance;

            return territoryCenter + offset;
        }

        /// <summary>
        /// 애플리케이션 재시작 간에도 동일한 결과를 보장하는 안정적인 해시 함수.
        /// .NET string.GetHashCode()는 문서화되지 않은 알고리즘을 사용하므로 대체.
        /// </summary>
        private static int StableHash(string str)
        {
            if (str == null) return 0;
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < str.Length; i++)
                {
                    hash = hash * 31 + str[i];
                }
                return hash;
            }
        }
    }

    public class TerritoryNPCBehaviour : MonoBehaviour
    {
        [SerializeField] private NPCInstance _npcData;

        public NPCInstance NPCData => _npcData;

        public void Initialize(NPCInstance data)
        {
            _npcData = data;
            gameObject.name = data.NpcName;
        }

        public void Interact()
        {
            Debug.Log("[TerritoryNPC] " + _npcData.NpcName + ": " + _npcData.Greeting);
            if (NPCDialogueWindow.Instance != null)
            {
                NPCDialogueWindow.Instance.ShowDialogue(_npcData);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
    }
}