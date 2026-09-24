using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P31-D: 마을 주민 NPC 스폰러 (2026-09-24) — 마을당 10~15명.
    ///
    /// 사용자 요청: "10~15명 정도 있는 마을 + 거기에 상점도 배치".
    /// VillageBuilder.BuildAllVillages() 직후 CoreSystemsBootstrap에서 BuildAllVillageNPCs() 호출.
    ///
    /// 설계:
    ///  · 24개 마을(4국가×6) 각각에 결정론적으로 10~15명 주민을 배치(광장 중심 반경 5~30m 링).
    ///  · 주민 모델 = RuntimeModelLoader rigged NPC(man1/man2/girl1~3/oldman1~2) + shop_npc(대표 마을).
    ///  · 배치 시드 = VillageBuilder와 동일한 결정론 문자열 해시 System.Random(프로세스 재시작 간 동일).
    ///    UnityEngine.Random 사용 금지.
    ///  · NPC에 NPCAmbientDialogue 부착 → HoverTargetClassifier가 대화 NPC로 분류(E키·근접 대화).
    ///  · 접지 = TerrainGenerator.GetHeightAt + GROUND_BASE(1f) 단일소스(영지/마을 계약).
    ///  · 빌드 멱등: "Villages_Root" 존재 시 마을 빌드와 함께 스킵(마을 빌드가 NPC도 함께 생성).
    ///
    /// ⚠ Systems ↔ UI: 화살표/정보창 등 UI 컴포넌트는 Systems에서 부착하지 않는다(순환참조 회피).
    ///   NPCAmbientDialogue는 Systems 소속이라 직접 부착 가능.
    /// </summary>
    public static class VillageNpcSpawner
    {
        private const float GROUND_BASE = 1f;
        private const int TerrainSeed = 42;
        private const int SeedBase = 9130;
        private const float MinSpacing = 3.0f;

        /// <summary>주민 NPC GLB 키 풀 (RuntimeModelLoader alias → Resources NPC_*_Rigged.glb).</summary>
        static readonly string[] VillagerKeys =
        {
            "man1", "man2", "girl1", "girl2", "girl3", "oldman1", "oldman2",
        };

        /// <summary>마을당 주민 수 (10~15).</summary>
        private const int MinNpcs = 10;
        private const int MaxNpcs = 15;

        /// <summary>
        /// 전체 24개 마을 주민 배치. Villages_Root가 없거나 이미 NPC 생성돼 있으면 스킵(멱등).
        /// VillageBuilder.BuildAllVillages() 직후(CoreSystemsBootstrap.BuildAllTerritories) 호출.
        /// </summary>
        public static void BuildAllVillageNPCs()
        {
            if (!Application.isPlaying) return;

            if (GameObject.Find("Villages_Root") == null)
            {
                Debug.LogWarning("[VillageNpcSpawner] Villages_Root 없음 — 마을 빌드가 선행돼야 합니다. 스킵.");
                return;
            }
            if (GameObject.Find("VillageNpcs_Root") != null)
            {
                Debug.Log("[VillageNpcSpawner] 마을 주민 이미 생성됨 — 스킵 (중복 방지)");
                return;
            }

            var root = new GameObject("VillageNpcs_Root");
            var villages = VillagePlacementSystem.GetAllVillages();
            int total = 0;
            int shopKeepers = 0;

            foreach (var village in villages)
            {
                int count = SpawnVillageNPCs(root.transform, village, out bool shopKeeper);
                total += count;
                if (shopKeeper) shopKeepers++;
            }

            Debug.Log($"[VillageNpcSpawner] 전체 마을 주민 배치 완료: villages={villages.Count} npcs={total} shopKeepers={shopKeepers}");
        }

        /// <summary>단일 마을 주민 배치. 주민 수와 상점주인 배치 여부 반환.</summary>
        private static int SpawnVillageNPCs(Transform root, VillagePlacementSystem.VillageInfo village, out bool shopKeeper)
        {
            shopKeeper = false;
            string parentName = $"Npcs_{village.nation}_{village.index:D2}";
            int seedIndex = GetHash($"{village.nation}_{village.index}_villagenpcs");
            var rng = new System.Random(SeedBase + seedIndex);

            var parentGo = new GameObject(parentName);
            parentGo.transform.SetParent(root, false);
            parentGo.transform.position = village.center;

            var placed = new List<Vector3>();

            // 대표 마을(index 0, isRepresentative) → 상점주인 1명 (상점 건물 근처 8~12m)
            if (village.isRepresentative)
            {
                Vector3 spot = RandomSpotAround(village.center, 8f, 12f, rng);
                spot = EnforceSpacing(spot, village.center, placed);
                if (SpawnNpc(parentGo.transform, "shop_npc", spot, village.center, "ShopKeeper"))
                    placed.Add(spot);
                shopKeeper = true;
            }

            // 주민 10~15명 — 반지름 5~30m 링, 균등 분산 + 결정론 지터
            int npcCount = MinNpcs + rng.Next(MaxNpcs - MinNpcs + 1); // 10~15
            int spawned = 0;
            for (int i = 0; i < npcCount; i++)
            {
                float slotAngle = (360f / npcCount) * i + (float)rng.NextDouble() * 8f; // 지터
                float radius = 5f + (float)rng.NextDouble() * 25f;  // 5~30m
                float angleRad = slotAngle * Mathf.Deg2Rad;
                Vector3 spot = village.center + new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);
                spot = EnforceSpacing(spot, village.center, placed);
                placed.Add(spot);

                string key = VillagerKeys[i % VillagerKeys.Length];
                if (SpawnNpc(parentGo.transform, key, spot, village.center, $"Villager_{i + 1}"))
                    spawned++;
            }

            parentGo.transform.SetParent(root, true);
            return spawned;
        }

        private static bool SpawnNpc(Transform parent, string modelKey, Vector3 pos, Vector3 center, string npcName)
        {
            float groundY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;

            if (!RuntimeModelLoader.TryGetModel(modelKey, out var npcPrefab))
            {
                Debug.LogWarning($"[VillageNpcSpawner] NPC 모델 '{modelKey}' 로드 실패 — 스킵 ({npcName})");
                return false;
            }

            GameObject npc = Object.Instantiate(npcPrefab);
            npc.name = npcName;
            npc.transform.position = new Vector3(pos.x, groundY + 0.05f, pos.z);

            // 광장(center)을 향하도록
            Vector3 face = new Vector3(pos.x - center.x, 0f, pos.z - center.z);
            if (face.sqrMagnitude > 0.0001f)
                npc.transform.rotation = Quaternion.LookRotation(-face, Vector3.up);

            // NPC 이름표/대화 — nameplate는 이름표 컴포넌트가 UI에서 처리, 여기선 대화 데이터만.
            // NPCAmbientDialogue 부착 → HoverTargetClassifier가 대화 NPC로 분류(E키 근접 대화).
            if (npc.GetComponent<NPCAmbientDialogue>() == null)
                npc.AddComponent<NPCAmbientDialogue>();

            // 동적 콜라이더(접근 E키 판정/선택용) — 자식/자신에 없으면 추가
            if (npc.GetComponent<Collider>() == null &&
                npc.GetComponentInChildren<Collider>() == null)
            {
                var col = npc.AddComponent<CapsuleCollider>();
                col.height = 1.8f; col.radius = 0.35f; col.center = new Vector3(0f, 0.9f, 0f);
            }

            npc.transform.SetParent(parent, true);
            return true;
        }

        /// <summary>광장 중심 기준 ring spot (radius 범위 내 균등 + 지터).</summary>
        private static Vector3 RandomSpotAround(Vector3 center, float minR, float maxR, System.Random rng)
        {
            float angle = (float)rng.NextDouble() * 360f;
            float radius = minR + (float)rng.NextDouble() * (maxR - minR);
            float angleRad = angle * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);
        }

        /// <summary>후보가 기배치 NPC/건물과 MinSpacing 미만이면 광장 밖으로 결정론적으로 밀어낸다.</summary>
        private static Vector3 EnforceSpacing(Vector3 candidate, Vector3 center, List<Vector3> placed)
        {
            for (int iter = 0; iter < 6; iter++)
            {
                float minDist = float.MaxValue;
                for (int i = 0; i < placed.Count; i++)
                {
                    float d = Vector3.Distance(candidate, placed[i]);
                    if (d < minDist) minDist = d;
                }
                if (minDist >= MinSpacing) break;
                Vector3 flat = candidate - center;
                float r = flat.magnitude;
                Vector3 dir = r > 0.01f ? flat / r : new Vector3(1f, 0f, 0f);
                candidate = candidate + dir * (MinSpacing - minDist + 0.5f);
            }
            return candidate;
        }

        /// <summary>결정론 문자열 해시 — VillageBuilder/VillagePlacementSystem과 동일 알고리즘.</summary>
        private static int GetHash(string input)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }
    }
}