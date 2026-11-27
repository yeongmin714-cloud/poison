using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [115차] 주간 세금 시스템 — 7일마다 자동으로 세금을 계산/누적하고, 권장세금 UI에 데이터를 제공한다.
    /// 마을 상전 NPC(ShopNPC)의 "세금제안" 액션과 연계됨 (InteractionPanelUTK → RecommendedTaxWindowUTK).
    ///
    /// 규약:
    ///   - 세율 소스: TerritoryDifficultyProfileSystem.Resolve(...).TaxPercent (프로필 권장세율 — 115차 계획).
    ///   - 세액 산식: VillageEconomySystem.GetWeeklyCollectedTax (마을 경제 계수 반영, half-up 규약 준용).
    ///   - 과세 대상: PlayerOwned 영지 전부 (LordOwned는 AITerritoryEconomySystem이 이미 처리 — 수정 금지 계약).
    ///   - 주기: TimeManager.OnDayStart 훅 + 앵커일부터 7일마다 1회 (AITerritoryEconomySystem 주급 규약 동일).
    ///   - 골드 지급: PlayerStats.AddGold(source="tax") — gold-wallet-single-source 계약.
    /// </summary>
    public class TaxManager
    {
        // ===== 싱글턴 =====
        private static TaxManager _instance;
        public static TaxManager Instance => _instance ??= new TaxManager();

        // ===== 주간 세금 상수 =====
        /// <summary>세수 주기(게임일). 7일마다 1회 과세 (AITerritoryEconomySystem.TAX_INTERVAL_DAYS 동일 규약).</summary>
        public const int TAX_INTERVAL_DAYS = 7;
        /// <summary>세금 앵커일 — 이 일자부터 7일마다 과세.</summary>
        public const int TAX_ANCHOR_DAY = 1;

        // ===== 상태 =====
        private int _lastTaxDay = -1;
        private int _pendingTax = 0;
        private int _recommendedTax = 0;
        private bool _initialized;

        /// <summary>미수금(아직 수취하지 않은 세금). 권장세금 UI에 표시되며, 수금 시 0으로 감소.</summary>
        public int PendingTax => _pendingTax;
        /// <summary>권장 세금. 마지막 과세 시 계산된 제안액.</summary>
        public int RecommendedTax => _recommendedTax;
        /// <summary>마지막 과세일.</summary>
        public int LastTaxDay => _lastTaxDay;

        // ===== 부트스트랩 — TimeManager.OnDayStart 훅 =====

        /// <summary>씬 로드 후 1회 — TimeManager를 보장하고 OnDayStart를 구독해 7일 자동 과세를 가동한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Initialize();
        }

        /// <summary>훅 구독 (멱등). 세이브 매니저 등 외부에서도 호출 가능.</summary>
        public static void Initialize()
        {
            if (Instance._initialized) return;
            var tm = TimeManager.GetOrCreate();
            if (tm != null)
                tm.OnDayStart += Instance.OnDayStart;
            Instance._initialized = true;
            Debug.Log("[TaxManager] 초기화 — TimeManager.OnDayStart 구독 (7일 자동 과세 가동)");
        }

        private void OnDayStart()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;
            ApplyWeeklyTax(tm.CurrentDay);
        }

        // ===== 주간 세금 자동 발생 =====

        /// <summary>
        /// 매일 호출 가능. 주 경계일(TAX_ANCHOR_DAY + n × TAX_INTERVAL_DAYS)이 아니면 무시하고,
        /// 경계일에 도달하면 PlayerOwned 영지 전부에 과세해 미수금에 누적한다.
        /// 멱등 — 같은 경계일에 반복 호출해도 1회만 과세 (AITerritoryEconomySystem.ApplyDailyTax 선례).
        /// </summary>
        /// <param name="day">현재 게임일.</param>
        /// <returns>이번 호출에서 과세가 발생했는지 여부.</returns>
        public bool ApplyWeeklyTax(int day)
        {
            if (day < TAX_ANCHOR_DAY) return false;
            if ((day - TAX_ANCHOR_DAY) % TAX_INTERVAL_DAYS != 0) return false;

            int week = (day - TAX_ANCHOR_DAY) / TAX_INTERVAL_DAYS;
            int lastWeek = _lastTaxDay >= TAX_ANCHOR_DAY ? (_lastTaxDay - TAX_ANCHOR_DAY) / TAX_INTERVAL_DAYS : -1;
            if (week <= lastWeek) return false;

            _lastTaxDay = day;
            CollectIntoPending();
            // [117차 R2] 자동 징수 — 피그마 244:281 "매주 월요일 00:00 자동 징수" 계약.
            //   수금 버튼 폐기(피그마 정합)에 따라 주 경계일에 미수금을 즉시 지급한다(CollectPendingTax 재사용).
            int autoCollected = CollectPendingTax();
            Debug.Log($"[TaxManager] 주간 세금 자동 부과 — day {day} week {week}, 징수 합계 {_recommendedTax}g, 자동 징수 {autoCollected}g");
            return true;
        }

        // ===== 주간 세금 계산 =====

        /// <summary>
        /// 권장 세금 계산 — 프로필 세율(Resolve.TaxPercent)과 마을 경제도를 반영.
        /// VillageEconomySystem.GetWeeklyCollectedTax 규약 준용 (half-up 정수).
        /// </summary>
        public int CalculateRecommendedTax(
            TerritoryDifficulty difficulty, int taxPercent, NationType nation, int territoryIndex)
        {
            int tax = VillageEconomySystem.GetWeeklyCollectedTax(difficulty, taxPercent, nation, territoryIndex);
            return tax;
        }

        /// <summary>PlayerOwned 영지 전부에 과세해 미수금에 누적. TerritoryDatabase 부재 시 무시(멱등 안전).</summary>
        private void CollectIntoPending()
        {
            int total = 0;
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                foreach (var definition in db.GetAllDefinitions())
                {
                    if (definition.id.nation == default && definition.id.index <= 0) continue; // 빈 정의 방어.
                    var state = db.GetState(definition.id.nation, definition.id.index);
                    if (state == null) continue;
                    if (state.ownership != TerritoryOwnership.PlayerOwned) continue;

                    // [117차 R2] 실제 징수 세율 = 영지별 설정 세율(기본=권장세율) — 피그마 "다음 징수부터 적용".
                    //   TerritoryDifficultyProfile는 readonly struct — null 비교 불가(CS0019), Resolve는 항상 유효값 반환.
                    var profile = TerritoryDifficultyProfileSystem.Resolve(
                        definition.id.index, definition.id.nation, definition.difficulty);
                    int recommendedRate = profile.TaxPercent;
                    int rate = GetTaxRate(definition.id.nation, definition.id.index);

                    int weekly = CalculateRecommendedTax(
                        definition.difficulty, rate, definition.id.nation, definition.id.index);
                    total += weekly;

                    // [117차 R2] 호감도 하락 — 권장 세율 초과 1%p당 −1/주 (피그마 계산 기준 문구와 동일 산식).
                    int loss = Mathf.Max(0, rate - recommendedRate);
                    if (loss > 0)
                    {
                        string favKey = TerritoryKey(definition.id.nation, definition.id.index);
                        int favor = Mathf.Clamp(GetFavor(definition.id.nation, definition.id.index) - loss, 0, 100);
                        _favor[favKey] = favor;
                        Debug.Log($"[TaxManager] 호감도 하락 — {favKey} 세율 {rate}% > 권장 {recommendedRate}% → −{loss}/주 ({favor}/100)");
                    }
                }
            }
            _recommendedTax = total;
            _pendingTax += total;
        }

        // ===== [117차 R2] 영지별 세율/호감도 상태 — 피그마 244:281 데이터 소스 =====

        /// <summary>세율 슬라이더 하한(%) — 피그마 "최소 세금 0%".</summary>
        public const int MinTaxPercent = 0;
        /// <summary>세율 슬라이더 상한(%) — 피그마 "최대 세금 30%".</summary>
        public const int MaxTaxPercent = 30;
        /// <summary>호감도 기본값 — 피그마 "72 / 100" 표기의 분모 계약(0~100).</summary>
        public const int DefaultFavor = 100;

        private readonly System.Collections.Generic.Dictionary<string, int> _taxRates = new();
        private readonly System.Collections.Generic.Dictionary<string, int> _favor = new();

        private static string TerritoryKey(NationType nation, int territoryIndex) => $"{nation}_{territoryIndex:D2}";

        /// <summary>영지 권장 세율(%) = 프로필 Resolve().TaxPercent. 정의 부재 시 10 폴백(표시 전용).</summary>
        public int GetRecommendedRate(NationType nation, int territoryIndex)
        {
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                var definition = db.GetDefinition(nation, territoryIndex);
                if (definition.id.nation == nation && definition.id.index == territoryIndex)
                {
                    var profile = TerritoryDifficultyProfileSystem.Resolve(
                        territoryIndex, nation, definition.difficulty);
                    return profile.TaxPercent;
                }
            }
            return 10;
        }

        /// <summary>영지 설정 세율(%) — 설정 이력 없으면 권장 세율(기본).</summary>
        public int GetTaxRate(NationType nation, int territoryIndex)
        {
            if (_taxRates.TryGetValue(TerritoryKey(nation, territoryIndex), out int rate))
                return Mathf.Clamp(rate, MinTaxPercent, MaxTaxPercent);
            return GetRecommendedRate(nation, territoryIndex);
        }

        /// <summary>영지 세율 설정 — 슬라이더 값(0~30) 클램프 저장. 다음 주간 징수부터 적용.</summary>
        public void SetTaxRate(NationType nation, int territoryIndex, int percent)
        {
            int clamped = Mathf.Clamp(percent, MinTaxPercent, MaxTaxPercent);
            _taxRates[TerritoryKey(nation, territoryIndex)] = clamped;
            Debug.Log($"[TaxManager] 세율 설정 — {TerritoryKey(nation, territoryIndex)} = {clamped}% (다음 징수부터 적용)");
        }

        /// <summary>영지 호감도(0~100) — 설정 이력 없으면 DefaultFavor.</summary>
        public int GetFavor(NationType nation, int territoryIndex)
            => _favor.TryGetValue(TerritoryKey(nation, territoryIndex), out int favor) ? favor : DefaultFavor;

        /// <summary>영지 호감도 직접 설정(테스트/복원용 — 클램프 0~100).</summary>
        public void SetFavor(NationType nation, int territoryIndex, int value)
            => _favor[TerritoryKey(nation, territoryIndex)] = Mathf.Clamp(value, 0, 100);

        /// <summary>영지 예상 주간 징수액 — 지정 세율로 계산(피그마 "예상 주간 징수액" 데이터 소스).</summary>
        public int EstimateWeeklyTax(NationType nation, int territoryIndex, int ratePercent)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return 0;
            var definition = db.GetDefinition(nation, territoryIndex);
            if (definition.id.nation != nation || definition.id.index != territoryIndex) return 0;
            return CalculateRecommendedTax(
                definition.difficulty, Mathf.Clamp(ratePercent, MinTaxPercent, MaxTaxPercent), nation, territoryIndex);
        }

        /// <summary>세이브 복원 — 영지별 세율/호감도(항목 누락 시 무영향). SaveManager 연동은 후속(기존 pending도 미연동 상태 유지).</summary>
        public void ImportRateFavorState(System.Collections.Generic.Dictionary<string, int> rates, System.Collections.Generic.Dictionary<string, int> favor)
        {
            if (rates != null)
                foreach (var kv in rates) _taxRates[kv.Key] = Mathf.Clamp(kv.Value, MinTaxPercent, MaxTaxPercent);
            if (favor != null)
                foreach (var kv in favor) _favor[kv.Key] = Mathf.Clamp(kv.Value, 0, 100);
        }

        public (System.Collections.Generic.Dictionary<string, int> rates, System.Collections.Generic.Dictionary<string, int> favor) ExportRateFavorState()
            => (new System.Collections.Generic.Dictionary<string, int>(_taxRates),
                new System.Collections.Generic.Dictionary<string, int>(_favor));

        // ===== 수금 (권장세금 UI [수금] 버튼 연계) =====

        /// <summary>
        /// 미수금 수취 — 플레이어 골드에 지급하고 미수금을 0으로 만든다.
        /// RecommendedTaxWindowUTK의 [수금] 버튼에서 호출.
        /// </summary>
        /// <returns>수취한 금액. 미수금이 없거나 지급 실패 시 0.</returns>
        public int CollectPendingTax()
        {
            if (_pendingTax <= 0) return 0;

            var stats = PlayerStats.Instance;
            if (stats == null)
            {
                Debug.LogWarning("[TaxManager] PlayerStats 부재 — 골드 지급 실패, 미수금 보존");
                return 0;
            }

            int collected = _pendingTax;
            _pendingTax = 0;
            stats.AddGold(collected, "tax");
            Debug.Log($"[TaxManager] 세금 수취 — +{collected}g (source=tax)");
            return collected;
        }

        // ===== 세이브/로드 =====

        /// <summary>미수금/마지막 과세일 복원 — 구세이브는 0/-1 폴백 (113차 선례).</summary>
        public void ImportSaveData(int pendingTax, int lastTaxDay)
        {
            _pendingTax = Mathf.Max(pendingTax, 0);
            _lastTaxDay = lastTaxDay;
        }

        public (int pendingTax, int lastTaxDay) ExportSaveData()
        {
            return (_pendingTax, _lastTaxDay);
        }
    }
}
