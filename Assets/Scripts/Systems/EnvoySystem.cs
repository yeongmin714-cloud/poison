using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-24: 특사 파견 시스템 — 외교, 선물, 독살
    /// 
    /// 포섭된 병사를 특사로 선택하여 인근 영지에 파견합니다.
    /// 영지 호감도 변경, 동맹 제안, 독살 등 다양한 외교 행동 가능.
    /// </summary>
    public static class EnvoySystem
    {
        // 특사 임무 타입
        public enum EnvoyMission
        {
            Gift,           // 선물 전달 (+5~15 호감도)
            Friendship,     // 우호 제안 (+20 호감도, 일시적 휴전)
            Alliance,       // 동맹 제안 (국가 단위)
            Assassinate,    // 독살 선물 (영주 암살)
            Intel           // 📋 정보 수집 — 7일 주기 정보 스크롤 보고 (병사 수 + 영주 입맛)
        }

        // 임무별 필요 레벨
        public const int GIFT_REQUIRED_LEVEL = 5;
        public const int FRIENDSHIP_REQUIRED_LEVEL = 10;
        public const int ALLIANCE_REQUIRED_LEVEL = 20;
        public const int ASSASSINATE_REQUIRED_LEVEL = 15;

        // 📋 정보 수집 (Intel) — 7일 주기 정보 스크롤 보고 [2026-10-21 사용자 요구]
        public const int INTEL_REQUIRED_LEVEL = 5;       // 필요 레벨 (영주 정보 Lv.5와 동일 — SpySystem.LordInfo 기준)
        public const int INTEL_REPORT_INTERVAL_DAYS = 7; // 보고 주기 (게임 일차)

        // 발각 확률 관련
        public const float BASE_DETECT_CHANCE = 0.2f;
        public const float LOYALTY_DETECT_REDUCTION = 0.01f; // 호감도 1당 -1%
        public const float LEVEL_DETECT_REDUCTION = 0.005f; // 특사 레벨 1당 -0.5%

        // 결과
        public struct EnvoyResult
        {
            public bool success;
            public string message;
            public int loyaltyChange;
            public bool detected;
        }

        /// <summary>
        /// 특사 파견 시도
        /// </summary>
        public static EnvoyResult SendEnvoy(GuardPlaceholder envoy, TerritoryId targetTerritory, EnvoyMission mission)
        {
            if (envoy == null || !envoy.IsAlive)
                return Fail("특사가 없거나 사망했습니다.");

            if (!envoy.IsRecruited)
                return Fail("포섭된 병사만 특사로 파견할 수 있습니다.");

            // 레벨 체크
            int requiredLevel = GetRequiredLevel(mission);
            if (envoy.Level < requiredLevel)
                return Fail($"특사 레벨 부족! 필요: Lv.{requiredLevel}, 현재: Lv.{envoy.Level}");

            // 대상 영지 정보
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(targetTerritory);
            if (def.territoryName == null)
                return Fail("대상 영지를 찾을 수 없습니다.");

            switch (mission)
            {
                case EnvoyMission.Gift:
                    return ExecuteGiftMission(envoy, targetTerritory, def);
                case EnvoyMission.Friendship:
                    return ExecuteFriendshipMission(envoy, targetTerritory, def);
                case EnvoyMission.Alliance:
                    return ExecuteAllianceMission(envoy, targetTerritory, def);
                case EnvoyMission.Assassinate:
                    return ExecuteAssassinateMission(envoy, targetTerritory, def);
                case EnvoyMission.Intel:
                    return ExecuteIntelMission(envoy, targetTerritory, def);
                default:
                    return Fail("알 수 없는 임무입니다.");
            }
        }

        private static EnvoyResult ExecuteGiftMission(GuardPlaceholder envoy, TerritoryId targetId, TerritoryDefinition def)
        {
            int loyaltyGain = Random.Range(5, 16);
            int currentLoyalty = GetTerritoryLoyalty(targetId);
            SetTerritoryLoyalty(targetId, currentLoyalty + loyaltyGain);

            return Success($"{def.territoryName}에 선물 전달 성공! 호감도 +{loyaltyGain}", loyaltyGain, false);
        }

        private static EnvoyResult ExecuteFriendshipMission(GuardPlaceholder envoy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.");

            int currentLoyalty = state.loyaltyToPlayer > 0 ? (int)state.loyaltyToPlayer : 0;
            if (currentLoyalty < 30)
                return Fail($"우호 제안 실패! 현재 호감도가 너무 낮습니다 ({currentLoyalty}). 30 이상 필요.");

            int gain = 20;
            SetTerritoryLoyalty(targetId, currentLoyalty + gain);
            return Success($"{def.territoryName}와 우호 관계! 호감도 +{gain}", gain, false);
        }

        private static EnvoyResult ExecuteAllianceMission(GuardPlaceholder envoy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.");

            if (state.loyaltyToPlayer < 60)
                return Fail($"동맹 제안 실패! 호감도 부족 ({(int)state.loyaltyToPlayer}/60)");

            int gain = 30;
            SetTerritoryLoyalty(targetId, (int)state.loyaltyToPlayer + gain);
            return Success($"🎉 {def.nation}와 동맹 체결! 호감도 +{gain}", gain, false);
        }

        private static EnvoyResult ExecuteAssassinateMission(GuardPlaceholder envoy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.");

            // 발각 확률 계산
            float detectChance = BASE_DETECT_CHANCE;
            detectChance -= state.loyaltyToPlayer * LOYALTY_DETECT_REDUCTION; // 호감도 높으면 덜 발각
            detectChance -= envoy.Level * LEVEL_DETECT_REDUCTION;             // 레벨 높으면 덜 발각
            detectChance = Mathf.Clamp01(detectChance);

            bool detected = Random.value < detectChance;

            if (detected)
            {
                // 발각
                SetTerritoryLoyalty(targetId, 0);
                // 특사 사망 처리
                envoy.TakeDamage(9999f, Vector3.zero, "Executed");
                return Fail($"💀 발각! {envoy.GuardName} 처형됨. 호감도 0");
            }
            else
            {
                // 성공: 영주 암살
                SetTerritoryLoyalty(targetId, Mathf.Max(0, (int)state.loyaltyToPlayer - 30));

                // 영주 사망 처리 — TerritoryState에 플래그 (현재는 간단히)
                // [O6 C-O6-02] 암살 성공 → 칭호 카운터
                TitleManager.Instance?.RecordEvent("assassinations");
                return Success($"☠️ {def.territoryName} 영주 암살 성공! (미발각)", -30, false);
            }
        }

        // ===== 📋 정보 수집 (Intel) — 7일 주기 정보 스크롤 보고 [2026-10-21] =====

        /// <summary>
        /// 정보 특사 배정 — 대상 영지별 활성 특사 추적.
        /// [2026-10-21 사용자 요구] 특사 파견 후 7일 주기로 대상 영지의 병사 수 + 영주 입맛을
        /// 정보 스크롤로 창고에 전달한다. 특사 사망 시 보고 중단.
        /// </summary>
        private sealed class IntelAssignment
        {
            public GuardPlaceholder envoy;  // 파견된 정보 특사
            public TerritoryId target;      // 정찰 대상 영지
            public int startDay;            // 마지막 보고 기준일 (TimeManager.CurrentDay)
        }

        /// <summary>활성 정보 특사 목록 — 같은 대상 영지에 재파견 시 기존 배정 교체.</summary>
        private static readonly System.Collections.Generic.List<IntelAssignment> _intelAssignments =
            new System.Collections.Generic.List<IntelAssignment>();

        /// <summary>OnDayStart 구독 여부 — 1회만 구독 (멱등).</summary>
        private static bool _intelDayHookInstalled;

        /// <summary>OnDayStart 구독 보장. TimeManager가 씬에 없으면(EditMode 등) 다음 파견 시 재시도.</summary>
        private static void EnsureIntelDayHook()
        {
            if (_intelDayHookInstalled) return;
            var tm = TimeManager.GetOrCreate();
            if (tm == null) return;
            tm.OnDayStart += OnIntelDayStart;
            _intelDayHookInstalled = true;
        }

        private static void OnIntelDayStart()
        {
            TickIntelReports();
        }

        /// <summary>
        /// 정보 특사 주기 점검 — 자정(OnDayStart)마다 호출되며, 보고 주기(7일) 경과 시
        /// 대상 영지 정보 스크롤을 창고에 전달한다. [테스트 훅] EditMode에서 직접 호출 가능.
        /// </summary>
        public static void TickIntelReports()
        {
            var tm = TimeManager.Instance;
            if (tm == null || _intelAssignments.Count == 0) return;
            int today = tm.CurrentDay;

            // 역순 순회 — 사망/소멸 제거에 대응
            for (int i = _intelAssignments.Count - 1; i >= 0; i--)
            {
                var a = _intelAssignments[i];
                if (a.envoy == null || !a.envoy.IsAlive)
                {
                    _intelAssignments.RemoveAt(i);
                    continue;
                }
                if (today - a.startDay >= INTEL_REPORT_INTERVAL_DAYS)
                {
                    DeliverIntelReport(a, today);
                    a.startDay = today; // 다음 주기 기준 갱신
                }
            }
        }

        /// <summary>
        /// 정보 스크롤 생성 → 플레이어 현재 영지 창고에 전달 → 알림.
        /// 스크롤: 유니크 id(스택 불가), description에 병사 수 + 영주 입맛 기록. 창고 가득 시 다음 주기 재시도.
        /// </summary>
        private static void DeliverIntelReport(IntelAssignment a, int today)
        {
            var def = TerritoryDatabase.Instance.GetDefinition(a.target);
            if (string.IsNullOrEmpty(def.territoryName))
            {
                _intelAssignments.Remove(a); // 대상 소멸 → 배정 해제
                return;
            }

            string food = string.IsNullOrEmpty(def.lord.preferredFood) ? "알 수 없음" : def.lord.preferredFood;
            // [113차 B] 재료 힌트 — 선호 음식의 주재료·획득처를 LordFoodCatalog 단일소스에서 조립 (기존 라인 불변 + 라인 추가)
            string foodHint = LordFoodCatalog.BuildSourceHint(def.lord.preferredFood);
            string info = $"📋 {def.territoryName} 정찰 보고 ({today}일차)\n" +
                          $"병력: {def.guardCount}명\n" +
                          $"영주: {def.lord.lordName}\n" +
                          $"영주 입맛: {food}\n" +
                          $"🥄 {foodHint}";

            // 보고서마다 고유 id → 스택 안 됨 (보고 일차별 별도 슬롯)
            var scroll = new PlayerInventory.ItemData
            {
                id = $"intel_scroll_{a.target}_{today}",
                displayName = $"정보 스크롤 — {def.territoryName}",
                description = info,
                category = PlayerInventory.ItemCategory.Quest,
                rarity = ItemRarity.Uncommon,
                maxStack = 1
            };

            string warehouseKey = TerritoryManager.Instance != null
                ? TerritoryManager.Instance.CurrentTerritoryId.ToString()
                : "East_01";
            var warehouse = WarehouseSystem.GetOrCreate(); // [EditMode 대응] Instance는 Awake 의존 → EditMode에서 null. GetOrCreate로 보장.
            if (warehouse != null && warehouse.AddItem(warehouseKey, scroll, 1))
            {
                WarNotificationUI.ShowNotification(
                    $"📬 특사 보고서 도착: {def.territoryName} (창고에서 확인)",
                    WarNotificationUI.NotificationType.Info);
                Debug.Log($"[EnvoySystem] 📋 정보 스크롤 전달: {warehouseKey} ← {scroll.id}");
            }
            else
            {
                // 창고 가득/시스템 부재 — 배정 유지, 다음 주기 재시도
                Debug.LogWarning($"[EnvoySystem] 정보 스크롤 미전달 (창고 가득?) — {def.territoryName}, 다음 주기 재시도");
            }
        }

        /// <summary>
        /// 📋 정보 수집 임무 — 파견 성공 시 7일 주기 정보 스크롤 보고 시작.
        /// 발각 시 특사 처형 + 배정 실패 (기존 CalculateDetectChance 재사용).
        /// </summary>
        private static EnvoyResult ExecuteIntelMission(GuardPlaceholder envoy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.");

            // 파견 시점 발각 판정 — 기존 CalculateDetectChance 재사용
            float detectChance = CalculateDetectChance(envoy, targetId);
            if (Random.value < detectChance)
            {
                envoy.TakeDamage(9999f, Vector3.zero, "Executed");
                return Fail($"💀 발각! {envoy.GuardName} 처형됨. 정보 수집 취소");
            }

            // 배정 등록 — 같은 대상에 재파견 시 기존 배정 교체 (특사 1명 유지)
            _intelAssignments.RemoveAll(a => a.target.Equals(targetId));
            int startDay = TimeManager.GetOrCreate().CurrentDay;
            _intelAssignments.Add(new IntelAssignment { envoy = envoy, target = targetId, startDay = startDay });
            EnsureIntelDayHook();

            return Success($"📋 {def.territoryName} 정보 특사 파견! 7일 후부터 창고에 보고서 도착", 0, false);
        }

        // ===== 헬퍼 =====

        public static string GetMissionName(EnvoyMission mission)
        {
            switch (mission)
            {
                case EnvoyMission.Gift: return "🎁 선물 전달";
                case EnvoyMission.Friendship: return "🤝 우호 제안";
                case EnvoyMission.Alliance: return "🕊️ 동맹 제안";
                case EnvoyMission.Assassinate: return "☠️ 독살 선물";
                case EnvoyMission.Intel: return "📋 정보 수집";
                default: return "알 수 없음";
            }
        }

        public static string GetMissionDescription(EnvoyMission mission)
        {
            switch (mission)
            {
                case EnvoyMission.Gift: return "대상 영지에 선물을 전달하여 호감도를 높입니다";
                case EnvoyMission.Friendship: return "우호 관계를 제안하여 일시적 휴전을 얻습니다";
                case EnvoyMission.Alliance: return "국가 단위 동맹을 제안합니다";
                case EnvoyMission.Assassinate: return "음식에 독을 넣어 영주를 암살합니다 (발각 위험)";
                case EnvoyMission.Intel: return "대상 영지의 병력 수와 영주 입맛을 수집하여 7일마다 창고에 정보 스크롤을 전달합니다";
                default: return "";
            }
        }

        public static int GetRequiredLevel(EnvoyMission mission)
        {
            switch (mission)
            {
                case EnvoyMission.Gift: return GIFT_REQUIRED_LEVEL;
                case EnvoyMission.Friendship: return FRIENDSHIP_REQUIRED_LEVEL;
                case EnvoyMission.Alliance: return ALLIANCE_REQUIRED_LEVEL;
                case EnvoyMission.Assassinate: return ASSASSINATE_REQUIRED_LEVEL;
                case EnvoyMission.Intel: return INTEL_REQUIRED_LEVEL;
                default: return 99;
            }
        }

        /// <summary>
        /// 특사로 파견 가능한 병사 목록 반환 (포섭 + 생존 + 전투가능 or 역할 상관없음)
        /// </summary>
        public static List<GuardPlaceholder> GetAvailableEnvoys()
        {
            var result = new List<GuardPlaceholder>();
            var guards = Object.FindObjectsByType<GuardPlaceholder>();
            foreach (var g in guards)
            {
                if (g.IsAlive && g.IsRecruited)
                    result.Add(g);
            }
            return result;
        }

        public static float CalculateDetectChance(GuardPlaceholder envoy, TerritoryId targetId)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null) return 0.5f;

            float chance = BASE_DETECT_CHANCE;
            chance -= state.loyaltyToPlayer * LOYALTY_DETECT_REDUCTION;
            chance -= envoy.Level * LEVEL_DETECT_REDUCTION;
            return Mathf.Clamp01(chance);
        }

        private static int GetTerritoryLoyalty(TerritoryId id)
        {
            var state = TerritoryDatabase.Instance.GetState(id);
            return state != null ? (int)state.loyaltyToPlayer : 0;
        }

        private static void SetTerritoryLoyalty(TerritoryId id, int value)
        {
            var state = TerritoryDatabase.Instance.GetState(id);
            if (state != null)
                state.loyaltyToPlayer = Mathf.Clamp(value, 0, 100);
        }

        private static EnvoyResult Success(string msg, int loyaltyChange, bool detected)
            => new EnvoyResult { success = true, message = msg, loyaltyChange = loyaltyChange, detected = detected };
        private static EnvoyResult Fail(string msg)
            => new EnvoyResult { success = false, message = msg, loyaltyChange = 0, detected = false };
    }
}