using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P31-D: 마을 주민 NPC 스폰러 (2026-09-24) — 마을 등급별 주민 수(일반 6~10 / 촌락 3~5, T3-2).
    ///
    /// 사용자 요청: "10~15명 정도 있는 마을 + 거기에 상점도 배치".
    /// VillageBuilder.BuildAllVillages() 직후 CoreSystemsBootstrap에서 BuildAllVillageNPCs() 호출.
    ///
    /// 설계:
    ///  · 각 마을에 결정론적으로 주민을 배치(광장 중심 반경 5~30m 링) — 일반 6~10명 / 촌락(isHamlet) 3~5명(T3-2).
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

        // [99차 F-D] 접지 레이캐스트 마스크 — VillagerWanderer.ObstacleMask와 동일 근거: 마을씬 물리 레이어는
        //   Default(0) 단일 체계(데이터 기반 배치 — 레이어 마스크 부재 실측). 지형/건물 모두 Default.
        internal const int GroundSnapMask = 1 << 0;

        /// <summary>주민 NPC GLB 키 풀 (RuntimeModelLoader alias → Resources NPC_*_Rigged.glb).</summary>
        /// [128-2 P1] 24종 확장·소비처 무수정 기록 (카테고리 교차로 인접 NPC 외형 다양화)
        static readonly string[] VillagerKeys =
        {
            "man1", "girl1", "boy1", "oldman1", "oldwoman1",
            "man2", "girl2", "boy2", "oldman2", "oldwoman2",
            "man3", "girl3", "boy3", "oldman3", "oldwoman3",
            "man4", "girl4", "boy4", "oldwoman4",
            "man5", "girl5", "boy5", "oldwoman5",
            "girl6",
        };

        /// <summary>마을 등급별 주민 수 (T3-2) — 일반 6~10 / 촌락(isHamlet) 3~5. 시드 체계는 기존 유지.</summary>
        private const int MinNpcsNormal = 6;
        private const int MaxNpcsNormal = 10;
        private const int MinNpcsHamlet = 3;
        private const int MaxNpcsHamlet = 5;

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

        /// <summary>[81차] 단일 마을 주민 배치(테스트씬 진입용 — Test_15 마을+마약 경영 검증).
        /// BuildAllVillageNPCs와 동일 SpawnVillageNPCs 경로(메인씬 주민과 같은 결정론 배치/모델).
        /// VillageNpcs_Root가 없으면 생성. 멱등 — 같은 마을 부모(Npcs_*)가 이미 있으면 스킵.</summary>
        public static bool SpawnForVillage(NationType nation, int villageIndex)
        {
            if (!Application.isPlaying) return false;

            string parentName = $"Npcs_{nation}_{villageIndex:D2}";
            if (GameObject.Find(parentName) != null)
            {
                Debug.Log($"[VillageNpcSpawner] {parentName} 이미 존재 — 단일 스폰 스킵 (멱등)");
                return false;
            }

            var villages = VillagePlacementSystem.GetVillages(nation);
            if (villageIndex < 0 || villageIndex >= villages.Count) return false;

            var rootGO = GameObject.Find("VillageNpcs_Root");
            if (rootGO == null) rootGO = new GameObject("VillageNpcs_Root");

            int count = SpawnVillageNPCs(rootGO.transform, villages[villageIndex], out bool shopKeeper);
            Debug.Log($"[VillageNpcSpawner] 단일 마을 주민 배치: {nation} #{villageIndex} npcs={count} shopKeeper={shopKeeper}");
            return true;
        }

        /// <summary>[149차 P5] 주민 1명 단독 스폰 — 음식점 고객 NPC(Test_15 전용 검증용).
        ///   SpawnNpc 전체 파이프라인(모델/텍스처/높이/애니/접지/Wanderer)을 그대로 재사용 — 신규 생성 로직 0.
        ///   동명 NPC가 이미 존재하면 스킵(멱등 — Awake 재실행 대비).</summary>
        public static bool SpawnSingleVillager(Transform parent, string modelKey, Vector3 pos, Vector3 center, string npcName, float villageRadius)
        {
            if (GameObject.Find(npcName) != null) return true;   // 멱등 가드
            return SpawnNpc(parent, modelKey, pos, center, npcName, villageRadius);
        }

        /// <summary>
        /// [D4-B] 주민 이탈 이벤트에 따른 주민 NPC 제거 (VillagerDepartureSystem → count=1 호출).
        /// "VillageNpcs_Root" 아래 마을 부모("Npcs_{nation}_{index:D2}")의 자식 중 뒤쪽 count명을 제거한다
        /// (생성 순서상 뒤쪽이 일반 주민 — 상점주인(shop_npc)은 먼저 생성됨). NPC 목록 추적 구조가
        /// 없어 [실측] 부모 이름 조회로 접근한다(AITerritoryEconomySystem.GetAIGuards의 GameObject.Find 패턴 준용).
        /// 부모가 없으면(미빌드/이미 전부 제거) 로그만 남기고 종료 — 이탈 상태 기록은 호출부
        /// (VillageDrugState.RecordDeparture) 책임이다.
        /// </summary>
        public static void RemoveVillageNPCs(NationType nation, int villageIndex, int count)
        {
            if (count <= 0) return;

            GameObject root = GameObject.Find("VillageNpcs_Root");
            if (root == null)
            {
                Debug.LogWarning("[VillageNpcSpawner] VillageNpcs_Root 없음 — 주민 NPC 제거 스킵 (상태 기록은 호출부 책임)");
                return;
            }

            Transform parent = root.transform.Find($"Npcs_{nation}_{villageIndex:D2}");
            if (parent == null)
            {
                Debug.LogWarning($"[VillageNpcSpawner] {nation}_v{villageIndex} 주민 부모 없음 — 주민 NPC 제거 스킵 (상태 기록은 호출부 책임)");
                return;
            }

            int removable = Mathf.Min(count, parent.childCount);
            for (int i = 0; i < removable; i++)
            {
                Transform last = parent.GetChild(parent.childCount - 1);
                Object.Destroy(last.gameObject);
            }

            if (removable < count)
                Debug.Log($"[VillageNpcSpawner] {nation}_v{villageIndex} 제거 가능 주민 부족 — {removable}/{count} 제거");
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

            // 대표 마을(isRepresentative) → 상점주인 1명 (상점 건물 근처 8~12m)
            // 촌락(isHamlet)에는 상점 건물이 없으므로 상점주인도 배치하지 않는다(T3-2 — 기정 배타 조건의 방어 가드).
            if (village.isRepresentative && !village.isHamlet)
            {
                Vector3 spot = RandomSpotAround(village.center, 8f, 12f, rng);
                spot = EnforceSpacing(spot, village.center, placed);
                if (SpawnNpc(parentGo.transform, "shop_npc", spot, village.center, "ShopKeeper", village.radius))
                    placed.Add(spot);
                shopKeeper = true;
            }

            // 주민 수 — 마을 등급별(T3-2): 일반 6~10 / 촌락(isHamlet) 3~5. 시드/링 로직은 기존 그대로.
            int minNpcs = village.isHamlet ? MinNpcsHamlet : MinNpcsNormal;
            int maxNpcs = village.isHamlet ? MaxNpcsHamlet : MaxNpcsNormal;
            int npcCount = minNpcs + rng.Next(maxNpcs - minNpcs + 1); // 일반 6~10 / 촌락 3~5
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
                if (SpawnNpc(parentGo.transform, key, spot, village.center, $"Villager_{i + 1}", village.radius))
                    spawned++;
            }

            parentGo.transform.SetParent(root, true);
            return spawned;
        }

        /// <summary>[116차 R4] 1.8m 캘리브레이션+재접지 — SpawnNpc/SpawnVillageNpc(Test_13/14/15) 공용.
        ///   본문은 [87차] 검증 블록 그대로(MeasureStandingHeight → 균등 스케일 → bounds.min.y 재접지).</summary>
        public static void EnsureVillageNpcHeight(GameObject npc, float groundY, string npcName)
        {
            // [87차] 주민 키 = 플레이어/병사 1.8m 고정계약(51차) — 실측 균등 스케일 자가수리.
            //   스케일 후 bounds.min.y를 groundY에 재접지(피벗이 발밑이 아닌 GLB 대응).
            float measuredH = TerritoryBuilder.MeasureStandingHeight(npc, false);
            if (measuredH > 0.01f && Mathf.Abs(measuredH - 1.8f) > 0.02f)
            {
                npc.transform.localScale *= (1.8f / measuredH);
                var rends = npc.GetComponentsInChildren<Renderer>();
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int ri = 1; ri < rends.Length; ri++) b.Encapsulate(rends[ri].bounds);
                    Vector3 rp = npc.transform.position;
                    rp.y += groundY - b.min.y;
                    npc.transform.position = rp;
                }
                Debug.Log($"[VillageNpcSpawner] 키 캘리브레이션: {npcName} {measuredH:F2}m → 1.80m (×{1.8f / measuredH:F2})");
            }
            else
            {
                // [117차 R4] 스폰 키실측 — 계약 충족(스케일 스킵)도 실측 로그로 남긴다(부동/크기 체감 판별 데이터).
                Debug.Log($"[VillageNpcSpawner] 키실측(스폰): {npcName} {measuredH:F2}m — 1.8m 계약 충족(스케일 스킵)");
            }
        }

        /// <summary>
        /// [140차 P3-b] 메시 셰이딩 데이터 정합 수리 — 피부 얼룩(Lit 전용 아티팩트) 뿌리 수리.
        /// 근거(139차 A/B 감별 체인): Unlit(base_color 직결)=완전 청결 / Lit=전 구성 얼룩 —
        /// 그림자·노멀맵·AO·메탈릭·UV·알베도·중복 지오메트리 전부 소거. 확정 결함:
        /// **tangents 배열 길이 != vertices**(verts 4165 vs tangents 4164 — 임포트 Calculate 후에도
        /// 4453/4452 재현 = Meshy 익스포트 메시 데이터 결함). 수리: SMR 메시를 인스턴스 복제해
        /// RecalculateNormals/RecalculateTangents로 정점 수 기준 배열을 강제 일치 재생성(에셋 무손상).
        /// Unlit 렌더가 증명한 UV/알베도는 그대로 유지되고 Lit 셰이딩 데이터만 정합화된다.
        /// </summary>
        public static void EnsureVillageNpcMeshData(GameObject npc, string npcName)
        {
            foreach (var smr in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null) continue;
                Mesh src = smr.sharedMesh;
                int v = src.vertexCount;
                int norms = src.normals != null ? src.normals.Length : 0;
                int tans = src.tangents != null ? src.tangents.Length : 0;
                int uvs = src.uv != null ? src.uv.Length : 0;
                var tri = src.triangles;
                int maxIdx = 0;
                for (int i = 0; i < tri.Length; i++) if (tri[i] > maxIdx) maxIdx = tri[i];
                bool inconsistent = norms != v || tans != v || uvs != v || maxIdx >= v;
                if (!inconsistent) continue;   // 정합 — 무수정

                Mesh fixedMesh = Object.Instantiate(src);   // 인스턴스 복제(에셋 무손상 — [126차 규약])
                fixedMesh.name = src.name + "_MeshFixed";
                if (maxIdx >= v)
                {
                    // 인덱스가 정점 범위 밖 — 범위 내 3각만 유지(파손 트리 제거).
                    var kept = new List<int>(tri.Length);
                    for (int i = 0; i + 2 < tri.Length; i += 3)
                    {
                        int a = tri[i], b = tri[i + 1], c = tri[i + 2];
                        if (a < v && b < v && c < v) { kept.Add(a); kept.Add(b); kept.Add(c); }
                    }
                    fixedMesh.triangles = kept.ToArray();
                    Debug.Log($"[140차 P3-b] 인덱스 클램프 — {npcName} '{src.name}' 제거 삼각형 {(tri.Length - kept.Count) / 3}개");
                }
                if (uvs != v)
                {
                    // UV 길이 불일치 — RecalculateTangents가 요구하는 정점수 배열로 영 채움(텍스처 단일 격자 폴백).
                    fixedMesh.uv = new Vector2[v];
                    Debug.LogWarning($"[140차 P3-b] UV 길이 불일치 수리 — {npcName} '{src.name}' uv {uvs}→{v}");
                }
                fixedMesh.RecalculateNormals();     // 정점 수 기준 정확한 배열 재생성
                fixedMesh.RecalculateTangents();    // uv+normals 기반 — 길이 정합 보장
                fixedMesh.RecalculateBounds();
                smr.sharedMesh = fixedMesh;
                Debug.Log($"[140차 P3-b] 메시 셰이딩 데이터 수리 — {npcName} '{src.name}' verts={v} norms={norms} tans={tans} uv={uvs} maxIdx={maxIdx} → 재생성 일치(얼룩 뿌리 수리)");
            }
        }

        /// <summary>[116차 R4] 주민 애니 부착 공용 — Player_AC+믹사모 Idle 오버라이드+AlwaysAnimate(89/91/92차 계약 보존)
        ///   + [99차 F-C] 골격정합 프로브/리맵 일괄 이관(anim 소비 — SpawnNpc 후속 스코프 정리).</summary>
        public static void EnsureVillageNpcAnimation(GameObject npc, string modelKey, string npcName)
        {
            // [140차 P3-b] 메시 셰이딩 데이터 정합 수리(피부 얼룩 뿌리 수리 — 스폰 경로 전체 적용).
            EnsureVillageNpcMeshData(npc, npcName);

            // ── 애니메이션: NPC GLB는 병사/플레이어와 동일한 humanoid metarig 리그라
            //    [89차] 플레이어 애니 부착 — Resources "Animation/Controllers/Player_AC"로 교체(주민이 플레이어와
            //    동일 idle/walk 보행: Idle=MeshyUser/Idle_02, Walk=MeshyUser/Walking, Speed 계약 >0.55 Walk 진입/
            //    <0.35 Idle 복귀). TalkA/TalkB 상태 = 트리거 "Talk"/"Talk2"(HasExitTime 0.92 → 자동 Idle 복귀).
            //    HumanoidClipDriver는 PlayerMovement/PlayerCombat에 의존하므로 마을 주민엔 붙이지 않는다(무동작 방지).
            //    [91차 F6] girls T-pose 근본 수리 — 프리팹 Animator 재사용(GetComponentInChildren — 루트 전용
            //    GetComponent 폐기) + 아바타 무효 시 AvatarBuilder.BuildHumanAvatar 부트스트랩(RigifyHumanMap
            //    GLB 실측 매핑). 아바타 없는 Animator는 휴머노이드 클립 리타겟 불가 = T-pose(girl 계열 3명 실측).
            var anim = npc.GetComponentInChildren<Animator>();   // [91차 F6] ① 프리팹 내 Animator 재사용(어디든)
            bool avatarOk = anim != null && anim.avatar != null
                            && anim.avatar.isValid && anim.avatar.isHuman;   // [91차 F6] ② 유효 휴머노이드 아바타 판정
            bool avatarBuilt = false;
            if (!avatarOk)
            {
                // [91차 F6] ③ 무효 — 루트 Animator 확보(부재 시 AddComponent) + Rigify 매핑 아바타 빌드.
                if (anim == null) anim = npc.AddComponent<Animator>();
                var builtAvatar = AvatarBuilder.BuildHumanAvatar(npc, CreateRigifyHumanDescription(npc));
                if (builtAvatar != null && builtAvatar.isValid && builtAvatar.isHuman)
                {
                    anim.avatar = builtAvatar;   // [91차 F6] 빌드 아바타 부착
                    avatarBuilt = true;
                }
                else
                {
                    Debug.LogWarning($"[VillageNpcSpawner] 휴머노이드 아바타 빌드 실패 — 무아바타 리그로 폴백 ({npcName})");
                }
            }
            anim.applyRootMotion = false;
            // [92차 후속 F-B] 애니 컬링 오판 근절 — 기본 CullUpdateTransforms는 렌더러 bounds로 오프스크린을
            //   판정하는데 주민 SkinnedMeshRenderer bounds 비대(center.y≈6.8·size.y≈13m 추정 — 렌더 상태
            //   Begin 로그 3세션 실측)가 화면 안 NPC를 오프스크린으로 오판 → 애니 스킵·transform 이동 지속 =
            //   걷기 포즈 동결+슬라이드(영상 10~18s 실측). Player_AC 사용 계약(플레이어)과 동일 수준의
            //   AlwaysAnimate 고정 — bounds 비대 근본 수리(GLB 임포트, 별도 후속) 전까지 유지.
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var playerCtrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/Player_AC");
            if (playerCtrl != null)
            {
                // [92차] 믹사모 클립 전환 — Player_AC 베이스 AnimatorOverrideController 런타임 스왑(에셋 무수정).
                //   클립 로드 실패(둘 중 하나라도 null) → 오버라이드 컨트롤러 사용 안 함(기존 Player_AC 그대로 폴백).
                ResolveMixamoOverrideController(playerCtrl);
                // [103차 F-구현] 주민별 오버라이드 인스턴스 — 기존 공유 단일 폐기(각 주민 다른 애니 계약).
                //   idle 슬롯 패리티 다양성: 시드 짝수=mixamo Idle 오버라이드 / 홀수=null → Player_AC 원본(Armature|Idle_02) 유지.
                //   (BuildMixamoOverrideController의 idleClip != null 가드 재사용 — 신규 분기 없음)
                var overrideCtrl = BuildVillagerOverride(playerCtrl, npc, modelKey, out float varSeed);
                anim.runtimeAnimatorController = overrideCtrl != null
                    ? (RuntimeAnimatorController)overrideCtrl
                    : playerCtrl;
            }
            else
            {
                // [89차] 로드 실패 폴백 — 기존 SoldierShield_AC 유지(경고 로그).
                var soldierCtrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
                if (soldierCtrl != null)
                    anim.runtimeAnimatorController = soldierCtrl;
                else
                    Debug.LogWarning($"[VillageNpcSpawner] Player_AC/SoldierShield_AC 컨트롤러 로드 실패 — NPC 애니메이션 없음 ({npcName})");
            }
            if (avatarBuilt)
                anim.Rebind();   // [91차 F6] 빌드 아바타 부착 후 리바인드(휴머노이드 리타겟 활성화)

            // [91차 F6] ④ NPC별 진단 로그 1회 — girls 수리 증명(다음 Play 로그 판별용).
            string animatorSrc = anim.transform == npc.transform ? "root" : "child";
            string avatarLabel = anim.avatar == null
                ? "없음"
                : (avatarBuilt ? "신규빌드" : anim.avatar.name);
            Debug.Log($"[VillageNpcSpawner] 애니 부착: {npcName} animator={animatorSrc} " +
                      $"avatar={avatarLabel} isHuman={(anim.avatar != null && anim.avatar.isHuman)} " +
                      $"controller={(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "없음")}");

            // ── [99차 F-C] C-1/C-2 스폰 시 골격정합 프로브 + 이름 기반 리맵 수리 ──
            //   근거: [98차 F1] 애니 진단 — Animator 골격은 구동(Walk 루프+스트라이드 정합 성공)되나 영상
            //   픽셀 실측에서 표시 메시 포즈 완전 고정 → SkinnedMeshRenderer가 Animator와 다른(중복) 골격에
            //   바인딩 의심(GLB 리그/91차 신규빌드 아바타 경로). 스폰 시 전수 프로브 → 불일치 시 리맵.
            var skelProbe = ProbeSkeletonAlignment(npc, anim);
            Debug.Log($"[99차 F-C] 골격정합 — model={modelKey} smr={skelProbe.SmrCount} 불일치={skelProbe.MismatchCount} {skelProbe.Detail}");
            if (skelProbe.HasMismatch)
            {
                int remapped = RemapSmrToAnimator(npc, anim);
                if (remapped > 0)
                    Debug.Log($"[99차 F-C] 골격 리맵 — smr={skelProbe.SmrCount} 교체본={remapped} ({npcName})");
                else
                    Debug.LogWarning($"[99차 F-C] 골격 리맵 불가 — 동명 본 부재 ({npcName})");
            }
        }

        private static bool SpawnNpc(Transform parent, string modelKey, Vector3 pos, Vector3 center, string npcName, float villageRadius)
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

            EnsureNpcTexture(npc, modelKey);   // [134차 A] 피부 텍스처 자가 부착 — Instantiate 직후(높이 캘리브레이션 전 무방)
            EnsureNpcShadowTuning(npc, npcName);   // [140차 P3-c] 그림자 맵 아크네 수리 — 인게임 얼굴 스크린샷 판정 근거

            EnsureVillageNpcHeight(npc, groundY, npcName);   // [116차 R4] 추출 — [87차] 검증 블록 공용화

            // 광장(center)을 향하도록
            Vector3 face = new Vector3(pos.x - center.x, 0f, pos.z - center.z);
            if (face.sqrMagnitude > 0.0001f)
                npc.transform.rotation = Quaternion.LookRotation(-face, Vector3.up);

            // NPC 이름표/대화 — nameplate는 이름표 컴포넌트가 UI에서 처리, 여기선 대화 데이터만.
            // NPCAmbientDialogue 부착 → HoverTargetClassifier가 대화 NPC로 분류(E키 근접 대화).
            if (npc.GetComponent<NPCAmbientDialogue>() == null)
                npc.AddComponent<NPCAmbientDialogue>();

            EnsureVillageNpcAnimation(npc, modelKey, npcName);   // [116차 R4] 추출 — Player_AC+믹사모 공용화

            // ── [99차 F-D] D-1 스폰/키 캘리브레이션 후 접지 스냅 — 수직 하향 지면 레이캐스트로 발 기준 y 스냅.
            //   VillagerWanderer.Init이 현재 position에서 _yOffset을 기록하므로 스냅은 Wanderer 부착 전에 실행.
            float preGroundY = npc.transform.position.y;
            float groundShift = SnapToGroundRay(npc, 2f, 12f);
            Debug.Log($"[99차 F-D] 접지 — model={modelKey} y={preGroundY:F3}→{npc.transform.position.y:F3} Δ={groundShift:F3}");

            // 동적 콜라이더(접근 E키 판정/선택용) — 자식/자신에 없으면 추가
            if (npc.GetComponent<Collider>() == null &&
                npc.GetComponentInChildren<Collider>() == null)
            {
                var col = npc.AddComponent<CapsuleCollider>();
                col.height = 1.8f; col.radius = 0.35f; col.center = new Vector3(0f, 0.9f, 0f);
            }

            npc.transform.SetParent(parent, true);

            // [87차] 마을 배회 — 플레이어형 이동/애니([89차] Player_AC Speed 계약 >0.55/<0.35) + 마을 반경 내 랜덤 배회.
            //   시드 = 모델키+이름 해시(결정론 — 재실행 시 동일 궤적).
            var wanderer = npc.AddComponent<VillagerWanderer>();
            wanderer.Init(center, villageRadius, SeedBase + GetHash($"{modelKey}_{npcName}"));
            wanderer.SetWalkModel(modelKey);   // [92차] 모델별 보폭-속도 정합 — walkSpeed 테이블(girl 계열 보정) 전달
            // [128-2 R] 러너 배선 — 결정론 25% 롤. run 클립 부재 시 러너 해제(기존 보행 유지).
            bool runner = IsVillagerRunner(modelKey, npcName);
            var ccForRunner = ResolveCharClips(modelKey);   // 캐시 히트 — EnsureVillageNpcAnimation에서 해석됨
            if (runner && ccForRunner.run == null) runner = false;   // run 클립 부재 → 보행 유지
            wanderer.SetRunner(runner);
            return true;
        }

        /// <summary>
        /// [140차 P3-c] NPC 그림자 수신 튜닝 — 그림자 맵 아크네(피부 얼룩) 표준 수리.
        /// 근거: 인게임 얼굴 스크린샷(Screenshots/npc 얼굴.PNG 13:57) 판정 — 얼룩이 명암 경계(터미네이터)의
        /// 그림자 쪽에 각진 블록으로 집중 = 자기 그림자 바이어스 부족의 전형. 프로브 환경은 그림자가
        /// 렌더되지 않아(그림자 거리 미설정 배치) on/off가 동일했던 것이 139차 무효과의 원인.
        /// 수리: SMR receiveShadows=false — 캐릭터 표면이 그림자 맵을 샘플링하지 않아 아크네 소멸.
        /// 캐릭터가 지면에 그리는 그림자(castShadows)는 유지 — 월드 그림자 무손상.
        /// </summary>
        public static void EnsureNpcShadowTuning(GameObject npc, string npcName)
        {
            int changed = 0;
            foreach (var smr in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                if (smr.receiveShadows)
                {
                    smr.receiveShadows = false;
                    changed++;
                }
            }
            if (changed > 0)
                Debug.Log($"[140차 P3-c] 그림자 수신 튜닝 — {npcName} SMR {changed}개 receiveShadows=false (아크네 수리, castShadows 유지)");
        }

        // ═══════════════ [134차 A] NPC 피부 텍스처 자가 부착 ═══════════════

        /// <summary>
        /// [134차 A] NPC 피부 텍스처 자가 부착 — Npc2026 FBX 임베드 텍스처 서브어셋을 SMR 머티리얼에 부착.
        /// 근거(계획 항목 A): Npc2026 FBX 텍스처 2장 임베드 확인됐으나 일부만 하양 = 머티리얼-diffuse
        /// 바인딩이 파일별 불균일(Meshy 익스포트 특성) → 스폰 직후 런타임 자가 수리.
        ///  · 대상 한정: RuntimeModelLoader.TryGetResourceKey 성공 && rk가 "Npc2026/" 프리픽스
        ///    (구 GLB 키/man1 등 구 별칭은 기존 경로 무수정).
        ///  · 텍스처 선택: 이름에 "base_color|BaseColor|albedo|diffuse" 포함 우선, 없으면 normal 아닌
        ///    첫 텍스처(PickNpcSkinTexture — 런타임에 TextureImporter 접근 불가라 이름 기반 판정).
        ///  · 부착 판정(SMR 전수): material.mainTexture == null 또는 material.color 전 채널 >= 0.98
        ///    (순백 근접 = 텍스처 미반영 하양 피부 징후). 이미 유효 텍스처+비순백이면 스킵.
        ///    [126차 규약] new Material(renderer.sharedMaterial) 명시적 인스턴스 — 에셋 머티리얼 무손상.
        ///  · 기존 머티리얼 색/셰이더 무수정(텍스처만 교체). 로그 NPC당 1회.
        /// </summary>
        private static void EnsureNpcTexture(GameObject npc, string modelKey)
        {
            string skipReason = "";
            Texture2D chosen = null;
            Texture2D selfNormal = null;
            string folder = "";
            if (RuntimeModelLoader.TryGetResourceKey(modelKey, out string rk)
                && !string.IsNullOrEmpty(rk) && rk.StartsWith("Npc2026/"))
            {
                string[] parts = rk.Split('/');
                folder = parts.Length >= 2 ? parts[1] : "";
                // 런타임 Resources.LoadAll 계약(ResolveCharClips과 동일 경로 체계) — 에디터 전용
                // 에디터 전용 어셋 API 대신 동일 결과의 런타임 API(서브어셋 텍스처 수집).
                var textures = Resources.LoadAll<Texture2D>("Models/UserProvided/" + rk);
                chosen = PickNpcSkinTexture(textures);
                if (chosen == null)
                {
                    // [134차 A-2] 폴백 2차 시도 — 에디터 배치 수리(Npc2026MaterialFixSetup)가
                    // ExtractTextures로 추출한 캐릭터별 서브폴더 텍스처 보조 로드.
                    // 실측: 임베드 텍스처명이 "base_color"/"normal"로 캐릭터명 미포함 → 폴더 분리로 소유 확정.
                    // rk = "Npc2026/<Folder>/<Folder>_rigged" ([128-2 P2] 폴더/스템 도출 계약 준용)
                    if (!string.IsNullOrEmpty(folder))
                    {
                        var extracted = Resources.LoadAll<Texture2D>(
                            "Models/UserProvided/Npc2026/Textures/" + folder);
                        chosen = PickNpcSkinTexture(extracted);
                        if (chosen != null) skipReason = "";
                    }
                }
                if (chosen == null) skipReason = "텍스처 서브어셋 부재";

                // [141차 B1] 노멀맵 교차바인딩 교정 — _BumpMap도 캐릭터별 추출 normal로 자기바인딩.
                // 실측(141차): 전 rigged FBX 머티리얼의 _BumpMap이 Npc_Boy_1/normal.png로 교차바인딩
                // → 자기 UV로 타캐릭터 아틀라스(머리/천) 텍셀 샘플링 = Lit 전용 선형 얼룩
                //  (웹 프리뷰 청결·텍스처 수리 무반응·Unlit 청결을 전부 설명 — VARCO 원본은 정상).
                selfNormal = string.IsNullOrEmpty(folder) ? null
                    : Resources.Load<Texture2D>("Models/UserProvided/Npc2026/Textures/" + folder + "/normal");
            }
            else
            {
                skipReason = "Npc2026 키 아님";
            }

            bool applied = false;
            if (chosen != null)
            {
                int smrCount = 0;
                foreach (var smr in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr == null) continue;
                    var shared = smr.sharedMaterials;
                    if (shared == null || shared.Length == 0) continue;
                    smrCount++;
                    var mats = new Material[shared.Length];   // [134차 A3] 다중 머티리얼 슬롯 전수(③)
                    bool slotApplied = false;
                    for (int slot = 0; slot < shared.Length; slot++)
                    {
                        var srcMat = shared[slot];
                        if (srcMat == null) { mats[slot] = null; continue; }
                        bool texMissing = srcMat.mainTexture == null;
                        Color c = srcMat.color;
                        bool nearWhite = c.r >= 0.98f && c.g >= 0.98f && c.b >= 0.98f;
                        // [141차 B1] asset 레벨 교차바인딩 판정 — mainTexture가 자기 추출 텍스처와 참조 불일치면
                        // "유효해 보여도" 교정 대상(B2 사후에는 자기 바인딩이므로 자동 스킵 — 멱등 유지).
                        bool albedoCross = chosen != null && srcMat.mainTexture != null && srcMat.mainTexture != chosen;
                        if (!texMissing && !nearWhite && !albedoCross) { mats[slot] = srcMat; continue; }   // 기존 바인딩 존중(스킵)
                        var mat = new Material(srcMat);   // [126차 규약] 명시적 인스턴스
                        mat.mainTexture = chosen;   // 색/셰이더 무수정 — 텍스처만
                        // [134차 A3] URP 경로 이중 보장(②) — mainTexture가 _MainTex(레거시 경유)에만 기록되고
                        // _BaseMap이 비면 URP 셰이더는 흰색으로 샘플링(A3 유력 후보 ①) → _BaseMap에도 동일 텍스처 대입.
                        if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") == null)
                            mat.SetTexture("_BaseMap", chosen);
                        // [134차 A3] UV 스케일 교정(②) — (1,1) 아닌 타일링은 패딩색 샘플링(A3 후보 ② "깨짐") 원인 → (1,1) 강제.
                        if (mat.HasProperty("_MainTex"))
                        {
                            Vector2 mts = mat.GetTextureScale("_MainTex");
                            if (mts != Vector2.one)
                            {
                                mat.SetTextureScale("_MainTex", Vector2.one);
                                Debug.Log($"[134차 A3] UV 스케일 교정 — {npc.name} slot={slot} _MainTex ({mts.x:F2},{mts.y:F2})→(1.00,1.00)");
                            }
                        }
                        if (mat.HasProperty("_BaseMap"))
                        {
                            Vector2 bts = mat.GetTextureScale("_BaseMap");
                            if (bts != Vector2.one)
                            {
                                mat.SetTextureScale("_BaseMap", Vector2.one);
                                Debug.Log($"[134차 A3] UV 스케일 교정 — {npc.name} slot={slot} _BaseMap ({bts.x:F2},{bts.y:F2})→(1.00,1.00)");
                            }
                        }
                        // [141차 B1] 노멀맵 자기바인딩 교정 — 교차바인딩 _BumpMap(타캐릭터 normal)이
                        // 자기 UV로 타 아틀라스를 샘플링해 얼룩을 만듦(141차 확정 원인).
                        if (mat.HasProperty("_BumpMap") && selfNormal != null && mat.GetTexture("_BumpMap") != selfNormal)
                        {
                            mat.SetTexture("_BumpMap", selfNormal);
                            Debug.Log($"[141차 B1] 노멀맵 자기바인딩 교정 — {npc.name} slot={slot} → {folder}/normal");
                        }
                        // [134차 A3] 샘플링 진단(①) — 부착 직후 셰이더/맵 바인딩 실측(전신 순백=샘플링 레벨 판별 데이터).
                        bool hasBaseMap = mat.HasProperty("_BaseMap");
                        Texture baseMap = hasBaseMap ? mat.GetTexture("_BaseMap") : null;
                        bool hasMainTex = mat.HasProperty("_MainTex");
                        Texture mainTex = hasMainTex ? mat.GetTexture("_MainTex") : null;
                        Vector2 diagScale = hasMainTex ? mat.GetTextureScale("_MainTex") : Vector2.one;
                        Debug.Log($"[134차 A3] 샘플링 진단 — {npc.name} slots={shared.Length} slot={slot} " +
                                  $"shader={mat.shader.name} baseMap={(baseMap != null ? baseMap.name : (hasBaseMap ? "null" : "n/a"))} " +
                                  $"mainTex={(mainTex != null ? mainTex.name : (hasMainTex ? "null" : "n/a"))} " +
                                  $"scale=({diagScale.x:F2},{diagScale.y:F2})");
                        mats[slot] = mat;
                        slotApplied = true;
                    }
                    if (slotApplied)
                    {
                        smr.sharedMaterials = mats;   // [134차 A3] 슬롯 배열 재할당(SetMaterials 동등 — 인스턴스만 교체)
                        applied = true;
                    }
                }
                if (!applied) skipReason = smrCount == 0 ? "대상 SMR 부재" : "기존 텍스처 유효";
            }
            // [134차 A4] UV/스키닝 진단 프로브 — 첫 SMR만(NPC당 1회, 2중 루프 과다 로그 방지).
            // 근거(§9 A4 확정): 머티리얼 바인딩 완전 정상 → 남은 후보 ①UV 어긋남(아틀라스-UV 불일치 → 얼룩/찢김)
            // ②스키닝 가중치 파손(관절 늘어짐). 정지/이동 구분은 사용자 스크린샷 판정용 —
            // uvBounds(아틀라스 범위 이탈/꼬임 진단)·verts/tris(메시 규모)·badWeights(합계 0 또는 >1.01 카운트).
            var probeSmr = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (probeSmr != null && probeSmr.sharedMesh != null)
            {
                Mesh probeMesh = probeSmr.sharedMesh;
                Vector2[] probeUv = probeMesh.uv;
                bool hasUv = probeUv != null && probeUv.Length > 0;
                Vector2 uvMin = Vector2.zero, uvMax = Vector2.zero;
                if (hasUv)
                {
                    uvMin = probeUv[0];
                    uvMax = probeUv[0];
                    for (int i = 1; i < probeUv.Length; i++)
                    {
                        uvMin = Vector2.Min(uvMin, probeUv[i]);
                        uvMax = Vector2.Max(uvMax, probeUv[i]);
                    }
                }
                int badWeights = 0;
                BoneWeight[] bw = probeMesh.boneWeights;
                for (int i = 0; i < bw.Length; i++)
                {
                    float wSum = bw[i].weight0 + bw[i].weight1 + bw[i].weight2 + bw[i].weight3;
                    if (wSum < 0.0001f || wSum > 1.01f) badWeights++;
                }
                string uvStr = hasUv
                    ? $"({uvMin.x:F2},{uvMin.y:F2})~({uvMax.x:F2},{uvMax.y:F2})"
                    : "empty";
                Debug.Log($"[134차 A4] 메시 진단 — {npc.name} smr={probeSmr.name} uvBounds={uvStr} " +
                          $"verts={probeMesh.vertexCount} tris={probeMesh.triangles.Length / 3} badWeights={badWeights}");
            }
            Debug.Log($"[134차 A] 텍스처 자가 부착 — {npc.name} tex='{(chosen != null ? chosen.name : "null")}' 적용={applied} 스킵사유={skipReason}");
        }

        /// <summary>[134차 A] 스킨 텍스처 선택 — base_color/BaseColor/albedo/diffuse 우선,
        ///   없으면 normal 아닌 첫 텍스처(후순위 폴백). 전부 normal이면 null(부착 스킵 — 무해).</summary>
        private static Texture2D PickNpcSkinTexture(Texture2D[] textures)
        {
            if (textures == null) return null;
            Texture2D fallback = null;
            foreach (var t in textures)
            {
                if (t == null) continue;
                string n = t.name.ToLowerInvariant();
                bool isNormal = n.Contains("normal");
                if (!isNormal && fallback == null) fallback = t;   // normal 아닌 첫 텍스처
                if (n.Contains("base_color") || n.Contains("basecolor") || n.Contains("base color")
                    || n.Contains("albedo") || n.Contains("diffuse"))
                    return t;   // 우선 매칭 — 즉시 채택
            }
            return fallback;
        }

        // ═══════════════ [134차 C2-2] NPC 비주얼 180° 교정 ═══════════════


        // ═══════════════ [92차] 믹사모 FBX 클립 — Player_AC 런타임 오버라이드(AnimatorOverrideController) ═══════════════

        /// <summary>
        /// [92차] 믹사모 walk/idle 클립 정적 캐시 + Player_AC 오버라이드 컨트롤러(최초 스폰 1회 해석).
        /// 소스 = Assets/Resources/Animations/Villagers_Mixamo(Walking/Idle FBX — 런타임 Resources 로드 계약).
        /// 매칭: 클립명 lowercase에 "walk" 포함→walk / "idle" 포함→idle(모호 시 첫 번째 폴백).
        /// ⚠ 실측: 믹사모 FBX 내부 테이크명은 "mixamo.com"(파일명과 무관) → 폴더 레벨 키워드 매칭이
        /// 실패(null) 또는 모호(walk==idle 동일 객체)면 소스 FBX 파일명 기준 이차 판정(Walking/Idle 개별 LoadAll).
        /// [128-2 P2] TalkA/TalkB 슬롯도 캐릭터별 Talk/idle로 오버라이드하는 것으로 정책 변경됨(캐릭터별 Talk 부재 시 자기 idle로 폴백).
        /// </summary>
        private static AnimationClip _mixamoWalk;
        private static AnimationClip _mixamoIdle;
        private static bool _mixamoResolved;

        // [128-2 P2] 캐릭터별 클립 캐시 — key=alias(man1 등), 값=(walk,idle,talk). 파일별 LoadAll 계약([92차 이차판정 원리의 파일경로 확정판] — 내부 테이크명 불일치 무관).
        // [128-2 R] 캐시 값 4원소 확장(walk,idle,talk,run) — run 클립은 Walk 슬롯 오버라이드(러너 주민) 전용.
        private static readonly Dictionary<string, (AnimationClip walk, AnimationClip idle, AnimationClip talk, AnimationClip run)> CharClipCache = new Dictionary<string, (AnimationClip, AnimationClip, AnimationClip, AnimationClip)>();

        /// <summary>[92차 실측] Player_AC Walk/Idle 슬롯 클립명(컨트롤러 YAML guid 역조회) —
        /// Walk 상태 모션 = MeshyUser/Walking.fbx "Armature|Walking_withSkin",
        /// Idle 상태 모션 = MeshyUser/Idle_02.fbx "Armature|Idle_02". 이 2슬롯만 교체.
        /// 정확 일치 사용 사유: Player_AC에 "Walk_Turn_*"/"Walk_Backward_*" 등 유사 클립 다수 —
        /// 부분문자열 매칭은 오검출 리스크가 있어 슬롯 식별을 클립명 전체 일치로 확정.</summary>
        private const string PlayerAcWalkSlotClipName = "Armature|Walking_withSkin";
        private const string PlayerAcIdleSlotClipName = "Armature|Idle_02";

        /// <summary>[92차] 믹사모 클립 로드+해석(정적 캐시 1회) — [103차] 컨트롤러 생성은 호출부(주민별 인스턴스)로 이관.
        /// → 스폰 경로가 기존 Player_AC를 그대로 쓴다(폴백 계약 — 89차 동작 유지).</summary>
        private static void ResolveMixamoOverrideController(RuntimeAnimatorController baseCtrl)
        {
            if (_mixamoResolved || baseCtrl == null) return;   // [92차] 1회 해석 + 베이스 컨트롤러 가드
            _mixamoResolved = true;

            // [92차] 폴더 레벨 LoadAll → 클립명 키워드 매칭(리임포트로 클립명이 파일명처럼 돌아온 경우 커버).
            var clips = Resources.LoadAll<AnimationClip>("Animations/Villagers_Mixamo");
            _mixamoWalk = FirstClipNameContaining(clips, "walk");
            _mixamoIdle = FirstClipNameContaining(clips, "idle");

            // [92차 실측] 믹사모 테이크명 "mixamo.com" → 키워드 매칭 불가/모호(동일 객체) 시
            // 소스 FBX 파일명(Walking/Idle)으로 이차 판정 — 파일명이 곧 클립 소스 계약.
            // [98차 F1] F1-1 객체 동일성 강제 검증 — `_mixamoWalk == _mixamoIdle`(참조 동일)면 폴더
            // LoadAll 결과를 폐기하고 Walking/Idle 파일별 LoadAll 재해석으로 확정(근거: Editor.log 2775행
            // walk='mixamo.com' idle='mixamo.com' — 로그만으로 객체 동일성 판별 불가 → Walk 상태가 Idle
            // 클립 재생 = 서있는 포즈+이동 증상의 유력 원인).
            if (_mixamoWalk == null || _mixamoIdle == null || _mixamoWalk == _mixamoIdle)
            {
                var walkFileClips = Resources.LoadAll<AnimationClip>("Animations/Villagers_Mixamo/Walking");
                var idleFileClips = Resources.LoadAll<AnimationClip>("Animations/Villagers_Mixamo/Idle");
                _mixamoWalk = walkFileClips != null && walkFileClips.Length > 0 ? walkFileClips[0] : _mixamoWalk;
                _mixamoIdle = idleFileClips != null && idleFileClips.Length > 0 ? idleFileClips[0] : _mixamoIdle;
            }

            // [98차 F1] F1-1 재해석 후에도 참조 동일이면 오버라이드 폐기 — Walk 상태가 Idle 클립을 재생하는
            // 증상(포즈 고정+이동)을 사전 차단. 폐기 시 기존 Player_AC 폴백 계약(: playerCtrl)으로 이어짐.
            if (_mixamoWalk != null && _mixamoWalk == _mixamoIdle)
            {
                Debug.LogWarning($"[VillagerWanderer] 믹사모 walk/idle 동일 객체(instanceID={_mixamoWalk.GetInstanceID()}) — 오버라이드 폐기, 기존 Player_AC 유지");
                _mixamoWalk = null;
                _mixamoIdle = null;
                return;
            }

            // [92차] 폴백 가드 — 클립 둘 중 하나라도 누락(null)이면 오버라이드 컨트롤러 미사용
            // (기존 Player_AC 그대로 — 스폰 경로의 playerCtrl 폴백 분기로 이어짐).
            if (_mixamoWalk == null || _mixamoIdle == null)
            {
                Debug.LogWarning("[VillagerWanderer] 믹사모 클립 로드 실패 — 오버라이드 컨트롤러 미사용, 기존 Player_AC 유지");
                return;
            }

            // [92차 → 98차 F1] 선택 클립 해석 로그(정적 캐시 = 세션당 1회) — Play 판별 마커.
            //   [98차 F1] F1-1: instanceID/isLooping/length 포함 — walk/idle 서로 다른 객체 + isLooping=true 실측
            //   (테이크명 'mixamo.com' 동일로 로그만 판별 불가했던 92차 문제 봉합).
            Debug.Log($"[VillagerWanderer] 믹사모 클립 — walk='{_mixamoWalk.name}' idle='{_mixamoIdle.name}' " +
                      $"walkID={_mixamoWalk.GetInstanceID()} idleID={_mixamoIdle.GetInstanceID()} " +
                      $"동일객체={_mixamoWalk == _mixamoIdle} walkLoop={_mixamoWalk.isLooping} idleLoop={_mixamoIdle.isLooping} " +
                      $"walkLen={_mixamoWalk.length:F2}s idleLen={_mixamoIdle.length:F2}s");
        }

        /// <summary>[128-2 P2][128-2 R] 캐릭터별 클립 해석(alias → walk/idle/talk/run) — 파일별 LoadAll 계약.
        /// 감사 실측 폴백: Girl_5 idle 부재 시 Girl_1 idle, Man_5 walk 부재 시 Man_1 walk 사용.
        /// [128-2 R] run 클립도 같은 패턴으로 로드 — OW_3 run 스택 0이므로 OW_1 run으로 폴백(감사 진실치).
        /// 구 GLB 키(Npc2026 프리픽스 미포함)는 (null,null,null,null) 반환 → 기존 믹사모 폴백 경로.</summary>
        internal static (AnimationClip walk, AnimationClip idle, AnimationClip talk, AnimationClip run) ResolveCharClips(string modelKey)
        {
            if (string.IsNullOrEmpty(modelKey)) return (null, null, null, null);
            if (CharClipCache.TryGetValue(modelKey, out var cached)) return cached;

            if (!RuntimeModelLoader.TryGetResourceKey(modelKey, out string rk)) return (null, null, null, null);
            if (rk == null || !rk.StartsWith("Npc2026/")) return (null, null, null, null);   // 구 GLB 키 → 폴백 경로

            // [128-2 P2] 폴더/스템 도출: rk = "Npc2026/<Folder>/<Folder>_rigged"
            //   → folder = "Models/UserProvided/Npc2026/<Folder>", stem = "<Folder>"
            int folderIdx = rk.LastIndexOf('/');
            if (folderIdx < 0) return (null, null, null, null);
            string folder = "Models/UserProvided/" + rk.Substring(0, folderIdx);
            string stemFull = rk.Substring(folderIdx + 1);
            string stem = stemFull.EndsWith("_rigged") ? stemFull.Substring(0, stemFull.Length - "_rigged".Length) : stemFull;

            AnimationClip walk = null, idle = null, talk = null, run = null;   // [128-2 R] run 4원소
            var walkClips = Resources.LoadAll<AnimationClip>(folder + "/" + stem + "_walk");
            if (walkClips != null && walkClips.Length > 0) walk = walkClips[0];
            var idleClips = Resources.LoadAll<AnimationClip>(folder + "/" + stem + "_idle");
            if (idleClips != null && idleClips.Length > 0) idle = idleClips[0];
            var talkClips = Resources.LoadAll<AnimationClip>(folder + "/" + stem + "_talk");
            if (talkClips != null && talkClips.Length > 0) talk = talkClips[0];
            // [128-2 R] run 클립 로드 — walk와 동일 파일별 LoadAll 계약(스텝 + "_run"). 부재 시 null.
            var runClips = Resources.LoadAll<AnimationClip>(folder + "/" + stem + "_run");
            if (runClips != null && runClips.Length > 0) run = runClips[0];
            if (run == null && stem == "Npc_OldWoman_3")
            {
                // [128-2 R] 감사 실측 폴백 — OW_3 run 스택 0(진실치). OW_1 run은 정상 존재.
                var fallbackRun = Resources.LoadAll<AnimationClip>("Models/UserProvided/Npc2026/Npc_OldWoman_1/Npc_OldWoman_1_run");
                if (fallbackRun != null && fallbackRun.Length > 0) run = fallbackRun[0];
            }

            // [128-2 P2] 감사 실측 폴백표(결정론 상수 — TestOutput/npc_audit_128_2_report.json 진실치)
            if (idle == null && stem == "Npc_Girl_5")
            {
                var fallbackIdle = Resources.LoadAll<AnimationClip>("Models/UserProvided/Npc2026/Npc_Girl_1/Npc_Girl_1_idle");
                if (fallbackIdle != null && fallbackIdle.Length > 0) idle = fallbackIdle[0];
            }
            if (walk == null && stem == "Npc_Man_5")
            {
                var fallbackWalk = Resources.LoadAll<AnimationClip>("Models/UserProvided/Npc2026/Npc_Man_1/Npc_Man_1_walk");
                if (fallbackWalk != null && fallbackWalk.Length > 0) walk = fallbackWalk[0];
            }
            // [138차] 감사 실측 폴백 — OW_1 talk 스택 0(진실치, AnimStack 0 — 요각 패치 불가 자산 공백).
            //   OW_2 talk는 정상 테이크 존재(138차 전수 스캔) — Man_5 walk 폴백 선례와 동일 패턴.
            if (talk == null && stem == "Npc_OldWoman_1")
            {
                var fallbackTalk = Resources.LoadAll<AnimationClip>("Models/UserProvided/Npc2026/Npc_OldWoman_2/Npc_OldWoman_2_talk");
                if (fallbackTalk != null && fallbackTalk.Length > 0) talk = fallbackTalk[0];
            }

            CharClipCache[modelKey] = (walk, idle, talk, run);
            Debug.Log($"[128-2 P2] 캐릭터 클립 — {modelKey} walk='{(walk != null ? walk.name : "null")}' idle='{(idle != null ? idle.name : "null")}' talk='{(talk != null ? talk.name : "null")}' run='{(run != null ? run.name : "null")}'");
            return (walk, idle, talk, run);
        }

        /// <summary>
        /// [98차 F1] F1-3 프리징 자가수리 지원(같은 어셈블리 — VillagerWanderer 소비) → [103차] 시그니처 확장:
        /// 주민별 인스턴스 재구성(Rebind 후 재적용 경로 — 참조 동일성 불요, F1-3 셀프체크 계약 유지).
        /// 반환 = baseCtrl 폴백(클립 미해석 시 기존 컨트롤러 유지 계약).
        /// </summary>
        internal static RuntimeAnimatorController GetResolvedMixamoOverride(RuntimeAnimatorController baseCtrl, string npcName)
        {
            ResolveMixamoOverrideController(baseCtrl);
            if (_mixamoWalk == null || _mixamoIdle == null) return baseCtrl;   // 폴백 계약
            // [103차] 주민별 인스턴스 재구성(Rebind 후 재적용 경로 — 참조 동일성 불요, F1-3 셀프체크 계약 유지).
            var ctrl = BuildMixamoOverrideController(baseCtrl, _mixamoWalk, _mixamoIdle, null);
            ctrl.name = $"VillagerOverride_{npcName}";
            return ctrl;
        }

        /// <summary>[128-2 R] 러너 판정 — 시드 해시 결정론 25%(hash & 3 == 0). 같은 주민은 항상 동일(저장 불요).</summary>
        internal static bool IsVillagerRunner(string modelKey, string npcName)
        {
            if (string.IsNullOrEmpty(modelKey) || string.IsNullOrEmpty(npcName)) return false;
            int hash = SeedBase + GetHash(modelKey + "_" + npcName);
            return (hash & 3) == 0;
        }

        /// <summary>
        /// [103차 F-구현] 주민별 오버라이드 인스턴스 빌드 — 시드 패리티로 idle 슬롯 다양화.
        /// 시드 = 기존 스폰 계약과 동일 소스(SeedBase + GetHash($"{modelKey}_{npc.name}")).
        /// 짝수=mixamo Idle 오버라이드 / 홀수=null(→ Player_AC 원본 Armature|Idle_02 유지).
        /// 실패 시 null 반환(기존 폴백 계약 — 호출부가 playerCtrl 유지).
        /// </summary>
        /// [103차 P3] 테스트 접근 internal 승격 — VillagerVariety103Tests(EditMode) 직접 호출 계약.
        internal static AnimatorOverrideController BuildVillagerOverride(
            RuntimeAnimatorController baseCtrl, GameObject npc, string modelKey, out float varSeed)
        {
            varSeed = 0f;
            ResolveMixamoOverrideController(baseCtrl);
            if (_mixamoWalk == null || _mixamoIdle == null) return null;   // 기존 폴백 계약

            // [128-2 P2][128-2 R] 캐릭터별 클립 해석 — 폴백 우선순위: 캐릭터 클립 → 구 믹사모 공용 → null(원본 유지)
            var charClips = ResolveCharClips(modelKey);

            int hash = SeedBase + GetHash($"{modelKey}_{npc.name}");
            varSeed = hash;
            bool evenIdle = ((hash & 1) == 0);
            // [128-2 R] 러너 주민은 Walk 슬롯에 자기 run 클립을 오버라이드(Player_AC Run 상태는 MoveX 전투 전용 —
            // 컨트롤러 무수정, Speed 임계 체계 계약 유지). run 부재 시 기존 walk 우선순위 계약 유지.
            bool runner = IsVillagerRunner(modelKey, npc.name);
            AnimationClip walkSlot = runner && charClips.run != null ? charClips.run : (charClips.walk != null ? charClips.walk : _mixamoWalk);
            AnimationClip idleOverride = evenIdle ? (charClips.idle != null ? charClips.idle : _mixamoIdle) : null;   // 홀수=idle 슬롯 원본 유지
            // [128-2 P2] talk 부재 캐릭터는 자기 idle로 TalkA/TalkB 슬롯 오버라이드(idle폴백 계약) — idle도 없으면 null(슬롯 원본 유지)
            AnimationClip talkOverride = charClips.talk != null ? charClips.talk : (charClips.idle != null ? charClips.idle : null);
            var ctrl = BuildMixamoOverrideController(baseCtrl, walkSlot, idleOverride, talkOverride);
            ctrl.name = $"VillagerOverride_{npc.name}";
            Debug.Log($"[128-2 P2][128-2 R] 주민별 오버라이드 — {npc.name} idle={(evenIdle ? "mixamo" : "원본(Idle_02)")} walkClip='{(walkSlot != null ? walkSlot.name : "null")}' runner={runner} talkClip='{(talkOverride != null ? talkOverride.name : "null")}' instanceID={ctrl.GetInstanceID()}");
            return ctrl;
        }

        /// <summary>[92차] 클립명 키워드 매칭(lowercase contains — 모호 시 첫 번째 폴백).</summary>
        private static AnimationClip FirstClipNameContaining(AnimationClip[] clips, string keyword)
        {
            if (clips == null) return null;
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (clip.name.ToLowerInvariant().Contains(keyword)) return clip;
            }
            return null;
        }

        /// <summary>
        /// [92차] Player_AC 베이스 AnimatorOverrideController(런타임 API — 컨트롤러 에셋 무수정).
        /// GetOverrides/ApplyOverrides로 Walk/Idle 슬롯(실측 클립명 2종)만 믹사모 클립으로 교체 —
        /// [128-2 P2] TalkA/TalkB 슬롯 오버라이드 적용(talkClip 파라미터 추가 — 캐릭터별 Talk/idle로 교체).
        /// 슬롯 미발견 시 교체 0 건 = 동작상 Player_AC와 동일(안전 폴백 — 컨트롤러 클립명 변경 시에도 깨지지 않는 설계).
        /// </summary>
        private static AnimatorOverrideController BuildMixamoOverrideController(
            RuntimeAnimatorController baseCtrl, AnimationClip walkClip, AnimationClip idleClip, AnimationClip talkClip)
        {
            var overrideCtrl = new AnimatorOverrideController(baseCtrl);   // [92차] 런타임 API 생성(에셋 수정 금지 계약)
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideCtrl.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                AnimationClip origin = pairs[i].Key;
                if (origin == null) continue;
                if (origin.name == PlayerAcWalkSlotClipName && walkClip != null)
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(origin, walkClip);
                else if (origin.name == PlayerAcIdleSlotClipName && idleClip != null)
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(origin, idleClip);
                else if (origin.name.ToLowerInvariant().Contains("talk") && talkClip != null)
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(origin, talkClip);
                // [128-2 P2] TalkA/TalkB 슬롯 오버라이드 적용 — talkClip != null일 때만 교체(캐릭터별 Talk/idle로 교체)
            }
            overrideCtrl.ApplyOverrides(pairs);
            return overrideCtrl;
        }

        // ═══════════════ [91차 F6] 휴머노이드 아바타 부트스트랩 (girls T-pose 수리) ═══════════════

        /// <summary>
        /// [91차 F6] Rigify→휴머노이드 슬롯 매핑 — GLB 7종(man1/man2/girl1~3/oldman1~2) 공용 동일 리그
        /// (실측 단일소스). GLB 본 27개 체인 실측: thigh ← spine ← Root, shoulder ← spine.003.
        /// humanSlot = HumanTrait.BoneName 규약, boneName = GLB Rigify 본명(.L/.R 좌우).
        /// </summary>
        private static readonly (string humanSlot, string boneName)[] RigifyHumanMap =
        {
            ("Hips", "spine"),
            ("Spine", "spine.001"),
            ("Chest", "spine.002"),
            ("UpperChest", "spine.003"),
            ("Neck", "spine.004"),
            ("Head", "spine.005"),
            ("LeftShoulder", "shoulder.L"),
            ("RightShoulder", "shoulder.R"),
            ("LeftUpperArm", "upper_arm.L"),
            ("RightUpperArm", "upper_arm.R"),
            ("LeftLowerArm", "forearm.L"),
            ("RightLowerArm", "forearm.R"),
            ("LeftHand", "hand.L"),
            ("RightHand", "hand.R"),
            ("LeftUpperLeg", "thigh.L"),
            ("RightUpperLeg", "thigh.R"),
            ("LeftLowerLeg", "shin.L"),
            ("RightLowerLeg", "shin.R"),
            ("LeftFoot", "foot.L"),
            ("RightFoot", "foot.R"),
            ("LeftToes", "toe.L"),
            ("RightToes", "toe.R"),
        };

        /// <summary>
        /// [91차 F6] RigifyHumanMap → HumanDescription(런타임 API — AvatarBuilder.BuildHumanAvatar 입력).
        /// human = HumanBone[](boneName→humanName), skeleton = SkeletonBone[](전 Transform 현재 로컬
        /// TRS 스냅샷 — T-pose 기준). 트위스트/스트레치는 임포터 기본치(0.5/0.05), hasTranslationDofs 등
        /// 나머지는 기본값.
        /// </summary>
        private static HumanDescription CreateRigifyHumanDescription(GameObject root)
        {
            var humanBones = new HumanBone[RigifyHumanMap.Length];
            for (int i = 0; i < RigifyHumanMap.Length; i++)
            {
                humanBones[i] = new HumanBone
                {
                    boneName = RigifyHumanMap[i].boneName,
                    humanName = RigifyHumanMap[i].humanSlot,
                    limit = new HumanLimit { useDefaultValues = true },
                };
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            var skeletonBones = new SkeletonBone[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                skeletonBones[i] = new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale,
                };
            }

            return new HumanDescription
            {
                human = humanBones,
                skeleton = skeletonBones,
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                rootMotionBoneName = string.Empty,
            };
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

        // ═══════════════ [99차 F-C] 골격정합 프로브 / 이름 리맵 ═══════════════

        /// <summary>[99차 F-C] 골격정합 프로브 결과 — SMR 전수 카운트 + 불일치 SMR 수 + SMR별 상세.</summary>
        internal sealed class SkeletonProbe
        {
            public int SmrCount;
            public int MismatchCount;
            public string Detail = "";
            public bool HasMismatch => MismatchCount > 0;
        }

        /// <summary>[99차 F-C] hips 대표 본명 — RigifyHumanMap(91차 F6) Hips 슬롯 = "spine" 계약 재사용.
        /// SMR 골격과 Animator 골격의 월드 불일치 판정 기준 본.</summary>
        internal const string ProbeHipsBoneName = "spine";

        /// <summary>
        /// [99차 F-C] C-1 골격정합 프로브 — SkinnedMeshRenderer 전수 실측: ① rootBone 계층 경로
        /// ② bones[]가 animator 루트 하위인지(외부 본 카운트) ③ SMR hips vs Animator hips 월드좌표
        /// 불일치 거리. 불일치 판정 = 외부 본 존재 또는 hips 거리 > 0.01m.
        /// [98차 F1] 진단의 확장(폐기 아님) — 본은 움직여도 렌더 메시가 동결되는 골격 불일치를 계량한다.
        /// </summary>
        internal static SkeletonProbe ProbeSkeletonAlignment(GameObject root, Animator anim)
        {
            var probe = new SkeletonProbe();
            if (root == null || anim == null)
            {
                probe.Detail = "(animator 부재 — 프로브 불가)";
                return probe;
            }
            Transform animRoot = anim.transform;
            var animBoneNames = new HashSet<string>();
            Transform animHips = null;
            foreach (var t in animRoot.GetComponentsInChildren<Transform>(true))
            {
                animBoneNames.Add(t.name);
                if (animHips == null && t.name == ProbeHipsBoneName) animHips = t;
            }
            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            probe.SmrCount = smrs.Length;
            var sb = new System.Text.StringBuilder();
            foreach (var smr in smrs)
            {
                var bones = smr.bones;
                int outside = 0;
                Transform smrHips = null;
                foreach (var b in bones)
                {
                    if (b == null) continue;
                    if (!b.IsChildOf(animRoot)) outside++;
                    if (smrHips == null && b.name == ProbeHipsBoneName) smrHips = b;
                }
                string rootPath = smr.rootBone != null ? GetHierarchyPath(smr.rootBone) : "없음";
                float hipsDist = animHips != null && smrHips != null
                    ? Vector3.Distance(animHips.position, smrHips.position)
                    : -1f;
                bool mismatch = outside > 0 || (hipsDist >= 0f && hipsDist > 0.01f);
                if (mismatch) probe.MismatchCount++;
                sb.Append($"[{smr.name} root={rootPath} 외부본={outside} " +
                          $"hipsΔ={(hipsDist >= 0f ? hipsDist.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "측정불가")}m" +
                          $"{(mismatch ? " 불일치" : "")}]");
            }
            probe.Detail = sb.ToString();
            return probe;
        }

        /// <summary>[99차 F-C] C-3 자가수리(F1-3) 보강용 — SMR hips vs Animator hips 월드 불일치 거리(m).
        /// 측정 불가(본 부재) 시 -1f. VillagerWanderer가 진단 틱에서 소비한다.</summary>
        internal static float MeasureHipsMismatch(GameObject root, Animator anim)
        {
            if (root == null || anim == null) return -1f;
            Transform animHips = null;
            foreach (var t in anim.transform.GetComponentsInChildren<Transform>(true))
                if (t.name == ProbeHipsBoneName) { animHips = t; break; }
            if (animHips == null) return -1f;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var b in smr.bones)
                    if (b != null && b.name == ProbeHipsBoneName)
                        return Vector3.Distance(animHips.position, b.position);
            return -1f;
        }

        /// <summary>
        /// [99차 F-C] C-2 이름 기반 리맵 — SMR bones[]를 animator 골격의 동명 본으로 재바인딩 +
        /// rootBone 교체 + animator.Rebind(). 교체 성공 본 수 반환(0 = 동명 본 부재/불필요).
        /// </summary>
        internal static int RemapSmrToAnimator(GameObject root, Animator anim)
        {
            if (root == null || anim == null) return 0;
            var nameMap = new Dictionary<string, Transform>();
            foreach (var t in anim.transform.GetComponentsInChildren<Transform>(true))
                if (!nameMap.ContainsKey(t.name)) nameMap.Add(t.name, t);
            int replaced = 0;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = smr.bones;
                int local = 0;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == null) continue;
                    if (nameMap.TryGetValue(bones[i].name, out var nb) && nb != bones[i])
                    {
                        bones[i] = nb;
                        local++;
                    }
                }
                if (smr.rootBone != null && nameMap.TryGetValue(smr.rootBone.name, out var nr) && nr != smr.rootBone)
                {
                    smr.rootBone = nr;
                    local++;
                }
                if (local > 0)
                {
                    smr.bones = bones;
                    replaced += local;
                }
            }
            if (replaced > 0) anim.Rebind();   // [99차 F-C] 리맵 후 골격 재바인딩 계약
            return replaced;
        }

        /// <summary>[99차 F-C] 계층 경로 문자열(프로브 로그용) — 상위부터 "/" 구분.</summary>
        private static string GetHierarchyPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            var p = t.parent;
            int guard = 0;
            while (p != null && guard++ < 32)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }

        // ═══════════════ [99차 F-D] 접지 스냅 ═══════════════


        // ═══════════════ [104차 F3] 루트↔가시 신체 괴리 순수 헬퍼 ═══════════════
        // 근거(실측 — Editor.log [103차 P1] 12건): 스폰 스냅 Δ≈-0.25~-0.51(정합) → 첫 Idle 평가 후 발 본이
        // 루트 기준 +3.5~4.1m 부상(footY=5.62~6.27 vs hitY=2.12) → [99차 F-D] 배회 스냅이 루트를 지하
        // -1.5~-2.1까지 침강(루트 지하/가시 지면 = 3.7m 괴리 — 스냅 대상 루트와 가시 신체 불일치).
        // 스냅 대상(SnapToGroundRay가 움직이는 트랜스폼) = NPC 루트 GO, 가시 신체 = 골격 루트(루트 GO의
        // 직계 자식 — 실측 "Villager_N/Root") 하위 본 체인. 일관 수리 = 괴리 초과분을 골격 루트에서 상쇄
        // (VillagerWanderer.LateUpdate 보상)하여 스냅 shift가 바인드 오프셋 범위(≤0.5m)로 수렴.

        /// <summary>[104차 F3] 발 본 ↔ 루트 수직 오프셋(m) — 양수 = 가시 신체가 루트 위.</summary>
        internal static float ComputeFootRootOffset(float footY, float rootY) => footY - rootY;

        /// <summary>
        /// [104차 F3] 루트↔가시 신체 괴리 초과분(m) — (발y − 루트y) − (바인드 오프셋 + 보행 발 들어올림
        /// 여유). 양수 = 가시 신체가 정상 범위를 초과해 떠 있음(골격루트 구동 과잉) → 상쇄 대상. 음수/0 =
        /// 정상(보행 들어올림·Idle 동요 포함 — 존중, 무보상). 실측 연동: footY=6.06/rootY=2.019/bind=0.14/
        /// allowance=0.35 → excess=3.55 — 상쇄 후 스냅 shift ≈ -0.5 이내([103차 P1] shift=-3.9 해소).
        /// </summary>
        internal static float ComputeBodyDivergenceExcess(float footY, float rootY, float bindOffset, float liftAllowance)
            => (footY - rootY) - (bindOffset + liftAllowance);
        /// <summary>
        /// [99차 F-D] 수직 하향 지면 레이캐스트 접지 스냅 — 자기 계층 히트는 제외하고 최근접 지면 히트의
        /// point.y에 "Foot" 포함 최하단 발 본을 맞춘다(y만 보정 — 렌더러 bounds 비대 실측(92차 후속 F-B)
        /// 때문에 bounds 기반 접지는 부정확 → 본 실측 계약). 발 본 부재/히트 부재 시 0(스냅 불가, 무해).
        /// 반환 = 적용 y 이동량.
        /// </summary>
        internal static float SnapToGroundRay(GameObject root, float rayUp, float rayDist)
        {
            if (root == null) return 0f;
            var hits = Physics.RaycastAll(root.transform.position + Vector3.up * rayUp,
                Vector3.down, rayUp + rayDist, GroundSnapMask, QueryTriggerInteraction.Ignore);
            RaycastHit best = default;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(root.transform)) continue;   // 자기 몸통/자식 히트 제외
                if (!found || h.distance < best.distance) { best = h; found = true; }
            }
            if (!found) return 0f;
            float footY = GetLowestFootY(root);
            if (float.IsNaN(footY)) return 0f;   // 발 기준 부재 — 스냅 불가(무해)
            float shift = best.point.y - footY;
            if (Mathf.Abs(shift) < 0.005f) return 0f;
            // [103차 P1] 접지 근원 진단 — |shift|>0.5m 대소송(접지 과소/과다) 시 레이캐스트 히트 근원 기록.
            if (Mathf.Abs(shift) > 0.5f)
            {
                float heightAt = TerrainGenerator.GetHeightAt(root.transform.position.x, root.transform.position.z, ProjectName.Core.Data.BiomeType.Plains, TerrainSeed);
                Debug.Log($"[103차 P1] 접지근원 — root={root.name} 히트={hits.Length} 최근접={best.transform.name} layer={best.transform.gameObject.layer} hitY={best.point.y:F2} footY={footY:F2} heightAt={heightAt:F2} shift={shift:F3}");
            }
            var p = root.transform.position;
            p.y += shift;
            root.transform.position = p;
            return shift;
        }

        /// <summary>[99차 F-D] 발 기준 최하단 y — "Foot" 포함 본 최소 월드 y. 부재 시 NaN.</summary>
        internal static float GetLowestFootY(GameObject root)
        {
            float minY = float.PositiveInfinity;
            bool found = false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (t.position.y < minY) { minY = t.position.y; found = true; }
            }
            return found ? minY : float.NaN;
        }
    }

}
