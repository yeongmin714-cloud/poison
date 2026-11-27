using System.Collections.Generic;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;   // [100차] RestaurantDemandMath — Core/Utils 순수 수학
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [100차] 음식점 손님 방문 시각화(RCT식) — 혼합 계약: 실시간 구매는 '연출 전용'.
    ///
    ///  · 영업 중(TimeManager.Hour 8≤h&lt;22 — RestaurantWindowUTK 영업 배지 실측 계약) +
    ///    재고 있음 + 음식점 건설 마을의 주민 일부를 방문자로 선정 → VillagerWanderer.SetVisitTarget.
    ///  · 방문 비율 = RestaurantDemandMath.VisitorRatio(주민 수, VisitorDemandFactor(판매가, basePrice))
    ///    — 가격이 기준가보다 낮을수록 방문자 증가([100차] 수요 곡선 연동).
    ///  · 결제 연출: 방문 도착(VillagerWanderer.VisitArrived) → PurchaseVisualized 정적 이벤트 발화 →
    ///    RestaurantCoinPopupOverlayUTK가 구독해 NPC 머리 위 코인 아이콘+금액 표시.
    ///  · ⚠ 정산 진실원천 불변 — 재고/골드/정산 상태에 전혀 접촉하지 않는다
    ///    (재고 소모·적립·골드 지급 API 전부 미사용 — 밤 RunDailySale 일괄 정산 계약 유지).
    ///  · 부트: RuntimeInitializeOnLoadMethod(AfterSceneLoad) 런타임 AddComponent 전용
    ///    (프리팹/씬 직렬화 대상 아님 — RestaurantDailyTicker/RestaurantPlacementMarker 규약 준용).
    /// </summary>
    public static class RestaurantVisitorSystem
    {
        /// <summary>[100차] 구매 연출 이벤트 — (국가, 마을 인덱스, NPC 월드 위치, 결제 금액 G).</summary>
        public static event System.Action<NationType, int, Vector3, int> PurchaseVisualized;

        private const float ScanIntervalSeconds = 2.5f;   // 마을 스캔 주기(초) — 프로덕션 계약 유지
        /// <summary>[151차 P1] Livelihood 씬(Test_15/20) 한정 스캔 간격(초) — '바로' 구매 체감 지연 축소.
        ///  씬 이름 게이트 분기 전용 상수(프로덕션 씬은 ScanIntervalSeconds 2.5s 무영향).</summary>
        internal const float LivelihoodScanIntervalSeconds = 1f;
        private const float MinLingerSeconds = 6f;        // 방문 머무름 하한(초)
        private const float MaxLingerSeconds = 12f;       // 방문 머무름 상한(초)
        private const int OpenHourMin = 8;                // 영업 시작 — RestaurantWindowUTK 실측 8≤h 계약
        private const int OpenHourMax = 22;               // 영업 종료(미만) — 실측 h<22 계약
        private const int VisitSeedBase = 472;            // 결정론 시드 기저 — VillageBuilder/스팟 시스템 동일 규약
        private const float VisitRingMin = 1.5f;          // 음식점 앞 대기 링 반경 하한(m) — 다수 손님 겹침 방지
        private const float VisitRingMax = 3.0f;          // 대기 링 반경 상한(m)

        /// <summary>방문자 추적 — 도착 이벤트에서 마을/금액 환산 + 만료 정리용.</summary>
        private sealed class VisitRecord
        {
            public VillagerWanderer npc;
            public NationType nation;
            public int villageIndex;
            public int amount;
            public bool fired;

            /// <summary>방문 지점 도착(구매 순간) — 구매 연출 이벤트 발화. 재고/골드 접촉 없음.</summary>
            public void OnArrived()
            {
                fired = true;
                Vector3 pos = npc != null ? npc.transform.position : Vector3.zero;
                PurchaseVisualized?.Invoke(nation, villageIndex, pos, amount);
            }
        }

        private static readonly List<VisitRecord> _visits = new List<VisitRecord>();

        // ===== 순수 헬퍼 (EditMode 테스트 대상 — 결정론) =====

        /// <summary>[100차] 방문자 선정 수 — 비율×주민 수 반올림, 하한 0(음수/NaN 방어). 결정론.</summary>
        public static int ComputeVisitorCount(int npcCount, float ratio)
        {
            if (npcCount <= 0) return 0;
            if (float.IsNaN(ratio)) return 0;
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Clamp01(ratio) * npcCount));
        }

        /// <summary>
        /// [100차] 결제 금액 산출 — 정산 산식과 동일 규약. 마약 재고 우선(마을 등록가 → 재고 basePrice →
        /// DefaultFoodBasePrice 폴백), 일반은 normalRegisteredPrice → 재고 basePrice. 스냅샷/재고 부재 시 0.
        /// </summary>
        public static int ResolvePurchaseAmount(VillageDrugSnapshot snapshot, List<RestaurantStockItem> stock)
        {
            bool hasDrugged = false;
            int drugBase = 0;
            int normalBase = 0;
            if (stock != null)
            {
                foreach (RestaurantStockItem s in stock)
                {
                    if (s.count <= 0) continue;
                    if (s.isDrugged)
                    {
                        hasDrugged = true;
                        if (s.basePrice > drugBase) drugBase = s.basePrice;   // 최고 basePrice — 보수적 규약
                    }
                    else if (normalBase == 0) normalBase = s.basePrice;        // 첫 슬롯 — 다중 슬롯 매핑 규약
                }
            }
            if (hasDrugged)
            {
                if (snapshot.registeredPrice > 0) return snapshot.registeredPrice;
                return drugBase > 0 ? drugBase : RestaurantSystem.DefaultFoodBasePrice;
            }
            if (snapshot.normalRegisteredPrice > 0) return snapshot.normalRegisteredPrice;
            return normalBase;
        }

        /// <summary>[100차] 결정론 시드 — 해시 17·31 문자열 해시(RestaurantSystem.DeriveVillageSeed 동일 알고리즘).</summary>
        public static int DeriveVisitSeed(int daySeed, NationType nation, int villageIndex)
        {
            unchecked
            {
                int hash = 17;
                string input = $"visitor_{daySeed}_{(int)nation}_{villageIndex}";
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        // ===== 스캔/배정 (런타임 전용) =====

        /// <summary>스캔 1회 — 전 마을 순회 → 영업/재고 게이트 → 방문자 선정/배정. 예외 격리(마을 단위).</summary>
        internal static void ScanAndAssign()
        {
            TimeManager tm = TimeManager.Instance;
            if (tm == null) return;
            int hour = tm.Hour;
            if (hour < OpenHourMin || hour >= OpenHourMax) return;   // 영업 외 — 진행 중 방문은 기존 머무름 만료에 위임

            List<VillagePlacementSystem.VillageInfo> villages = VillagePlacementSystem.GetAllVillages();
            if (villages == null) return;
            ConstructionManager cm = ConstructionManager.Instance;

            foreach (VillagePlacementSystem.VillageInfo village in villages)
            {
                try
                {
                    AssignVisitorsForVillage(cm, tm.CurrentDay, village);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[100차] 손님 방문 스캔 예외 격리 — {village.nation}#{village.index}: {ex.Message}");
                }
            }
            CleanupVisits();
        }

        private static void AssignVisitorsForVillage(ConstructionManager cm, int daySeed, VillagePlacementSystem.VillageInfo village)
        {
            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(village.nation, village.index);
            if (!snapshot.restaurantBuilt || string.IsNullOrEmpty(snapshot.assignedGuardId)) return;

            List<RestaurantStockItem> stock = VillageDrugState.GetStock(village.nation, village.index);
            int stockTotal = 0;
            if (stock != null)
                foreach (RestaurantStockItem s in stock)
                    stockTotal += Mathf.Max(0, s.count);
            if (stockTotal <= 0) return;

            GameObject villageRoot = GameObject.Find($"Village_{village.nation}_{village.index:D2}");   // VillageBuilder 112행 이름 규약
            if (villageRoot == null) return;

            List<VillagerWanderer> candidates = new List<VillagerWanderer>();
            foreach (Transform child in villageRoot.transform)
            {
                VillagerWanderer w = child.GetComponent<VillagerWanderer>();
                if (w == null || w.IsVisiting) continue;   // [100차] 방문 중/예정 중복 선정 방지
                candidates.Add(w);
            }
            if (candidates.Count == 0) return;

            int basePrice = ResolveStockBasePrice(stock);
            int salePrice = ResolveSalePrice(snapshot, stock, basePrice);
            float factor = RestaurantDemandMath.VisitorDemandFactor(salePrice, Mathf.Max(basePrice, 1));
            float ratio = RestaurantDemandMath.VisitorRatio(candidates.Count, factor);
            int count = ComputeVisitorCount(candidates.Count, ratio);
            // [149차 P5] Test_15 고객 검증 하한 — 수요 비율(0.12×factor, 클램프 [0.03,0.5])은 후보 1명에서
            //   반올림 결과 0이 될 수 있어 전용 고객(Test15Customer)이 영원히 선발되지 않는다. Livelihood 씬
            //   (주민 0명 — 148차 계약)의 구매 액션 관측을 위해 1명 하한을 보장한다. 선발 이후 경로(방문 이동/
            //   코인 연출/정산 불변 계약)는 무변경 — 프로덕션 수요 곡선 영향 0(씬 이름 게이트).
            if (count <= 0 && candidates.Count > 0 &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Livelihood"))
                count = 1;
            if (count <= 0) return;

            Vector3 restaurantPos = FindRestaurantPos(cm, village);
            var rng = new System.Random(DeriveVisitSeed(daySeed, village.nation, village.index));
            int amount = ResolvePurchaseAmount(snapshot, stock);

            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int pick = rng.Next(candidates.Count);
                VillagerWanderer npc = candidates[pick];
                candidates.RemoveAt(pick);
                if (npc == null) continue;

                // 음식점 앳 대기 링 — 다수 손님 겹침 방지(결정론 각도/반경)
                double ang = rng.NextDouble() * 360.0;
                float ringR = VisitRingMin + (float)rng.NextDouble() * (VisitRingMax - VisitRingMin);
                Vector3 visitPoint = restaurantPos + new Vector3(
                    Mathf.Cos((float)(ang * Mathf.Deg2Rad)) * ringR, 0f,
                    Mathf.Sin((float)(ang * Mathf.Deg2Rad)) * ringR);
                float linger = MinLingerSeconds + (float)rng.NextDouble() * (MaxLingerSeconds - MinLingerSeconds);

                var record = new VisitRecord
                {
                    npc = npc,
                    nation = village.nation,
                    villageIndex = village.index,
                    amount = amount,
                };
                npc.VisitArrived += record.OnArrived;
                _visits.Add(record);
                npc.SetVisitTarget(visitPoint, linger);
            }
        }

        /// <summary>재고 기준 basePrice — 마약 우선 최고 basePrice, 없으면 첫 일반 슬롯(정산 다중 슬롯 규약 동일).</summary>
        private static int ResolveStockBasePrice(List<RestaurantStockItem> stock)
        {
            int drugBase = 0;
            int normalBase = 0;
            if (stock != null)
            {
                foreach (RestaurantStockItem s in stock)
                {
                    if (s.count <= 0) continue;
                    if (s.isDrugged) { if (s.basePrice > drugBase) drugBase = s.basePrice; }
                    else if (normalBase == 0) normalBase = s.basePrice;
                }
            }
            return drugBase > 0 ? drugBase : normalBase;
        }

        /// <summary>현재 판매가 — 정산(RunDailySale)과 동일 규약: 마약 재고 있으면 등록가(없으면 basePrice), 없으면 일반 판매가.</summary>
        private static int ResolveSalePrice(VillageDrugSnapshot snapshot, List<RestaurantStockItem> stock, int basePrice)
        {
            bool hasDrugged = false;
            if (stock != null)
                foreach (RestaurantStockItem s in stock)
                    if (s.count > 0 && s.isDrugged) { hasDrugged = true; break; }
            if (hasDrugged)
                return snapshot.registeredPrice > 0 ? snapshot.registeredPrice : Mathf.Max(basePrice, RestaurantSystem.DefaultFoodBasePrice);
            return snapshot.normalRegisteredPrice > 0 ? snapshot.normalRegisteredPrice : basePrice;
        }

        /// <summary>
        /// 음식점 실물 위치 — ConstructionManager 완료 구조물(bp_restaurant) 중 해당 마을 매칭 최근접.
        /// 미발견(진행 중/레지스트리 부재) 시 마을 중심 폴백(방문 자체는 유효 — 광장 대기).
        /// </summary>
        private static Vector3 FindRestaurantPos(ConstructionManager cm, VillagePlacementSystem.VillageInfo village)
        {
            Vector3 best = village.center;
            float bestDist = float.MaxValue;
            if (cm != null)
            {
                foreach (ConstructionManager.StructureEntry entry in cm.Structures)
                {
                    if (entry.blueprintId != "bp_restaurant" || !entry.isComplete) continue;
                    if (!ConstructionManager.TryFindRestaurantVillage(entry.pos, out NationType n, out int vi))
                        continue;
                    if (n != village.nation || vi != village.index) continue;
                    float d = (entry.pos - village.center).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = entry.pos; }
                }
            }
            return best;
        }

        /// <summary>만료 정리 — 발화 완료/파괴된 방문 기록 제거(구독 유출 방지).</summary>
        private static void CleanupVisits()
        {
            for (int i = _visits.Count - 1; i >= 0; i--)
            {
                VisitRecord r = _visits[i];
                if (r.npc == null)
                {
                    _visits.RemoveAt(i);   // NPC 파괴 — 구독 대상 소멸
                }
                else if (r.fired)
                {
                    r.npc.VisitArrived -= r.OnArrived;
                    _visits.RemoveAt(i);
                }
            }
        }

        /// <summary>[100차] 테스트/진단용 — 현재 추적 중 방문 기록 수(정리 후 잔존분).</summary>
        public static int TrackedVisitCount => _visits.Count;

        /// <summary>스캔 주기 노출 — 티커 소비(internal, 같은 어셈블리).</summary>
        internal static float ScanIntervalAccess => ScanIntervalSeconds;

        /// <summary>[100차] 구매 연출 발화 — VisitRecord 경유 단일 진입점(테스트에서도 사용).</summary>
        internal static void FirePurchase(NationType nation, int villageIndex, Vector3 worldPos, int amount)
            => PurchaseVisualized?.Invoke(nation, villageIndex, worldPos, amount);

        /// <summary>[100차] 도메인 위생 — 추적 목록 전량 해제(구독 유출 방지). 티커 OnDestroy/도메인 리로드용.</summary>
        internal static void ResetForDomainReload()
        {
            foreach (VisitRecord r in _visits)
            {
                if (r.npc != null) r.npc.VisitArrived -= r.OnArrived;
            }
            _visits.Clear();
        }
    }

    /// <summary>
    /// [100차] 음식점 손님 방문 티커 — 2.5s 주기 스캔(런타임 전용). RestaurantDailyTicker/RestaurantPlacementMarker
    /// 부트 규약 준용(런타임 AddComponent 전용, DDOL, OnDestroy 정리).
    /// [151차 P1] Livelihood 씬(Test_15/20) 한정 스캔 간격 1s 단축 — '바로' 구매 체감 지연 축소
    /// (씬 이름 게이트 분기 — 프로덕션 씬은 기존 2.5s 계약 무영향).
    /// </summary>
    public sealed class RestaurantVisitorTicker : MonoBehaviour
    {
        private float _nextScan;

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (Time.unscaledTime < _nextScan) return;
            // [151차 P1] 씬 게이트 분기 — Livelihood 씬(Test_15/20)만 1s, 프로덕션은 기존 2.5s(ScanIntervalAccess) 유지.
            bool livelihoodScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Livelihood");
            _nextScan = Time.unscaledTime + (livelihoodScene
                ? RestaurantVisitorSystem.LivelihoodScanIntervalSeconds
                : RestaurantVisitorSystem.ScanIntervalAccess);
            RestaurantVisitorSystem.ScanAndAssign();
        }

        private void OnDestroy()
        {
            // [100차] 도메인 위생 — 티커 소멸 시 추적 목록 정리(구독 유출 방지).
            RestaurantVisitorSystem.ResetForDomainReload();
        }
    }
}
