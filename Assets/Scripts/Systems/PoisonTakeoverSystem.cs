using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-12: 3경로 독살 점령 시스템 — EnvoySystem + SpySystem + LordSurrenderSystem 통합.
    /// 
    /// 세 가지 점령 경로를 제공합니다:
    ///   a) 특사 경로: 특사가 독살 선물을 전달 → 영주 사망 → 영지 점령
    ///   b) 대치 경로: 플레이어가 모든 병사를 격파 → 영주 항복 (C10-10 LordSurrenderSystem)
    ///   c) 정보원 경로: 정보원이 첩보 수집 → 약점 노출 → 더 쉬운 점령
    /// 
    /// 사용법:
    ///   PoisonTakeoverSystem.TryPoisonTakeover(territoryId);
    ///   PoisonTakeoverSystem.TrySpyTakeover(territoryId);
    /// </summary>
    public static class PoisonTakeoverSystem
    {
        // ===== 이벤트 =====

        /// <summary>영주가 독살되었을 때 발생 (territoryId)</summary>
        public static event System.Action<TerritoryId> OnLordPoisoned;

        /// <summary>독살 시도가 발각되었을 때 발생 (territoryId, envoyName)</summary>
        public static event System.Action<TerritoryId, string> OnPoisonDetected;

        /// <summary>정보원 경로로 약점이 발견되었을 때 발생 (territoryId, weaknessDescription)</summary>
        public static event System.Action<TerritoryId, string> OnWeaknessFound;

        /// <summary>점령 완료되었을 때 발생 (territoryId, pathType)</summary>
        public static event System.Action<TerritoryId, TakeoverPath> OnTakeoverComplete;

        // ===== 상수 =====

        /// <summary>특사 독살 성공 후 독살 플래그 유지 시간 (게임 시간 일)</summary>
        public const float POISON_FLAG_DURATION_DAYS = 3f;

        /// <summary>정보원 약점 발견 시 독살 성공 확률 보너스</summary>
        public const float SPY_WEAKNESS_BONUS = 0.3f;

        /// <summary>정보원 경로 없이 기본 독살 성공 확률</summary>
        public const float BASE_POISON_SUCCESS_CHANCE = 0.5f;

        /// <summary>정보원 약점 발견 후 독살 성공 확률</summary>
        public const float WEAKNESS_POISON_SUCCESS_CHANCE = 0.85f;

        // ===== 열거형 =====

        /// <summary>점령 경로 유형</summary>
        public enum TakeoverPath
        {
            /// <summary>특사 독살 선물 경로</summary>
            PoisonGift,
            /// <summary>대치 후 항복 경로</summary>
            Confrontation,
            /// <summary>정보원 첩보 후 약점 공략 경로</summary>
            SpyExploit
        }

        /// <summary>독살 시도 결과</summary>
        public struct PoisonResult
        {
            /// <summary>성공 여부</summary>
            public bool success;
            /// <summary>결과 메시지</summary>
            public string message;
            /// <summary>발각 여부</summary>
            public bool detected;
            /// <summary>사용된 경로</summary>
            public TakeoverPath path;
        }

        // ===== 내부 상태 =====

        private static readonly Dictionary<TerritoryId, bool> _poisonFlags = new Dictionary<TerritoryId, bool>();
        private static readonly Dictionary<TerritoryId, float> _poisonTimers = new Dictionary<TerritoryId, float>();
        private static readonly Dictionary<TerritoryId, string> _spyWeaknesses = new Dictionary<TerritoryId, string>();
        private static readonly HashSet<TerritoryId> _envoySent = new HashSet<TerritoryId>();
        private static readonly HashSet<TerritoryId> _takenOver = new HashSet<TerritoryId>();

        // ===== 메인 퍼블릭 메서드 =====

        /// <summary>
        /// 특사 경로: 특사가 독살 선물을 전달하여 영주를 암살합니다.
        /// EnvoySystem.ExecuteAssassinateMission과 연동되며, 성공 시 영지가 플레이어 소유로 변경됩니다.
        /// </summary>
        /// <param name="envoy">파견할 특사 (GuardPlaceholder)</param>
        /// <param name="territoryId">대상 영지 ID</param>
        /// <param name="path">점령 경로 (기본값 PoisonGift)</param>
        /// <returns>독살 시도 결과</returns>
        public static PoisonResult TryPoisonTakeover(GuardPlaceholder envoy, TerritoryId territoryId, TakeoverPath path = TakeoverPath.PoisonGift)
        {
            if (_takenOver.Contains(territoryId))
                return new PoisonResult { success = false, message = "이미 점령된 영지입니다.", path = path };

            var db = TerritoryDatabase.Instance;
            var state = db.GetState(territoryId);
            if (state == null)
                return new PoisonResult { success = false, message = "영지 상태를 찾을 수 없습니다.", path = path };

            // 이미 처리된 영지 확인
            if (state.lordExecuted || state.lordSurrendered || state.ownership == TerritoryOwnership.PlayerOwned)
                return new PoisonResult { success = false, message = "이미 처리된 영지입니다.", path = path };

            // 특사 유효성 검사
            if (envoy == null || !envoy.IsAlive)
                return new PoisonResult { success = false, message = "특사가 없거나 사망했습니다.", path = path };

            if (!envoy.IsRecruited)
                return new PoisonResult { success = false, message = "포섭된 병사만 특사로 파견할 수 있습니다.", path = path };

            // 레벨 체크 (EnvoySystem.ASSASSINATE_REQUIRED_LEVEL 사용)
            if (envoy.Level < EnvoySystem.ASSASSINATE_REQUIRED_LEVEL)
                return new PoisonResult
                {
                    success = false,
                    message = $"특사 레벨 부족! 필요: Lv.{EnvoySystem.ASSASSINATE_REQUIRED_LEVEL}, 현재: Lv.{envoy.Level}",
                    path = path
                };

            // 독살 시도
            _envoySent.Add(territoryId);

            // 성공 확률 계산: 정보원 약점 발견 시 확률 증가
            float successChance = _spyWeaknesses.ContainsKey(territoryId)
                ? WEAKNESS_POISON_SUCCESS_CHANCE
                : BASE_POISON_SUCCESS_CHANCE;

            // 호감도 기반 추가 조정 (호감도가 높으면 성공 확률 증가)
            successChance += state.loyaltyToPlayer * 0.002f;
            successChance = Mathf.Clamp01(successChance);

            bool success = Random.value < successChance;

            if (success)
            {
                // 발각 확률 계산 (EnvoySystem.CalculateDetectChance 사용)
                float detectChance = EnvoySystem.CalculateDetectChance(envoy, territoryId);
                bool detected = Random.value < detectChance;

                if (detected)
                {
                    // 발각: 특사 사망 처리
                    envoy.TakeDamage(9999f, Vector3.zero, "Poison detected");
                    state.loyaltyToPlayer = 0;

                    OnPoisonDetected?.Invoke(territoryId, envoy.GuardName);

                    return new PoisonResult
                    {
                        success = false,
                        message = $"💀 발각! {envoy.GuardName} 처형됨. 독살 시도가 들통났습니다!",
                        detected = true,
                        path = path
                    };
                }

                // 성공: 영주 독살 완료
                ExecutePoisonTakeover(territoryId, path, envoy);

                return new PoisonResult
                {
                    success = true,
                    message = $"☠️ {db.GetDefinition(territoryId).territoryName} 영주 독살 성공! 영지가 점령되었습니다.",
                    detected = false,
                    path = path
                };
            }
            else
            {
                // 실패: 독살 시도가 실패했지만 발각되지는 않음
                return new PoisonResult
                {
                    success = false,
                    message = "독살 시도가 실패했습니다. 특사가 기회를 찾지 못했습니다.",
                    detected = false,
                    path = path
                };
            }
        }

        /// <summary>
        /// 대치 경로: 모든 병사를 격파한 후 영주 항복 처리를 진행합니다.
        /// LordSurrenderSystem.TrySummonLord를 호출하고, 영주 처형 시 영지를 획득합니다.
        /// </summary>
        /// <param name="territoryId">대상 영지 ID</param>
        /// <returns>항복/처형 성공 여부</returns>
        public static bool TryConfrontationTakeover(TerritoryId territoryId)
        {
            if (_takenOver.Contains(territoryId))
                return false;

            var db = TerritoryDatabase.Instance;
            var state = db.GetState(territoryId);
            if (state == null)
                return false;

            if (state.lordExecuted || state.ownership == TerritoryOwnership.PlayerOwned)
                return false;

            // LordSurrenderSystem 호출하여 영주 소환 및 항복
            bool summoned = LordSurrenderSystem.TrySummonLord(territoryId);
            if (!summoned)
            {
                // 아직 조건이 충족되지 않음 (병사가 아직 살아있음 등)
                return false;
            }

            // 영주 처형 (영지 획득)
            LordSurrenderSystem.ExecuteLord(territoryId);

            // 점령 완료 처리
            _takenOver.Add(territoryId);
            OnTakeoverComplete?.Invoke(territoryId, TakeoverPath.Confrontation);

            Debug.Log($"[PoisonTakeoverSystem] ⚔️ 대치 경로 점령 완료: {territoryId}");
            return true;
        }

        /// <summary>
        /// 정보원 경로: 정보원이 약점을 발견하면 더 쉬운 점령이 가능합니다.
        /// SpySystem.InfiltrateMission 결과를 활용하여 영주의 약점(선호 음식, 지병 등)을 기록합니다.
        /// </summary>
        /// <param name="spy">파견할 정보원 (GuardPlaceholder)</param>
        /// <param name="territoryId">대상 영지 ID</param>
        /// <returns>정보 수집 결과 (약점 발견 여부)</returns>
        public static PoisonResult TrySpyTakeover(GuardPlaceholder spy, TerritoryId territoryId)
        {
            if (_takenOver.Contains(territoryId))
                return new PoisonResult { success = false, message = "이미 점령된 영지입니다.", path = TakeoverPath.SpyExploit };

            var db = TerritoryDatabase.Instance;
            var state = db.GetState(territoryId);
            if (state == null)
                return new PoisonResult { success = false, message = "영지 상태를 찾을 수 없습니다.", path = TakeoverPath.SpyExploit };

            // 정보원 유효성 검사
            if (spy == null || !spy.IsAlive)
                return new PoisonResult { success = false, message = "정보원이 없거나 사망했습니다.", path = TakeoverPath.SpyExploit };

            if (!spy.IsRecruited)
                return new PoisonResult { success = false, message = "포섭된 병사만 정보원으로 파견할 수 있습니다.", path = TakeoverPath.SpyExploit };

            // Infiltrate 임무로 영주 정보 수집
            var spyResult = SpySystem.SendSpy(spy, territoryId, SpySystem.SpyMission.Infiltrate);
            if (!spyResult.success)
            {
                return new PoisonResult
                {
                    success = false,
                    message = spyResult.message,
                    detected = spyResult.detected,
                    path = TakeoverPath.SpyExploit
                };
            }

            // 약점 정보 저장
            var def = db.GetDefinition(territoryId);
            string weakness = BuildWeaknessDescription(def, spyResult.infoGathered);
            _spyWeaknesses[territoryId] = weakness;

            OnWeaknessFound?.Invoke(territoryId, weakness);

            Debug.Log($"[PoisonTakeoverSystem] 🕵️ 정보원 경로 약점 발견: {territoryId} — {weakness}");

            return new PoisonResult
            {
                success = true,
                message = $"🕵️ 약점 발견: {weakness} — 이제 독살 성공 확률이 대폭 상승합니다!",
                path = TakeoverPath.SpyExploit
            };
        }

        /// <summary>
        /// 정보원이 발견한 약점을 활용하여 독살 점령을 시도합니다.
        /// TryPoisonTakeover보다 높은 성공 확률을 가집니다.
        /// </summary>
        /// <param name="envoy">파견할 특사</param>
        /// <param name="territoryId">대상 영지 ID</param>
        /// <returns>독살 시도 결과</returns>
        public static PoisonResult TrySpyAssistedPoisonTakeover(GuardPlaceholder envoy, TerritoryId territoryId)
        {
            if (!_spyWeaknesses.ContainsKey(territoryId))
                return new PoisonResult
                {
                    success = false,
                    message = "먼저 정보원을 파견하여 약점을 발견해야 합니다.",
                    path = TakeoverPath.SpyExploit
                };

            return TryPoisonTakeover(envoy, territoryId, TakeoverPath.SpyExploit);
        }

        /// <summary>
        /// 특정 영지에 특사가 이미 파견되었는지 확인합니다.
        /// </summary>
        public static bool IsEnvoySent(TerritoryId territoryId)
        {
            return _envoySent.Contains(territoryId);
        }

        /// <summary>
        /// 특정 영지에 독살 플래그가 활성화되어 있는지 확인합니다.
        /// </summary>
        public static bool IsLordPoisoned(TerritoryId territoryId)
        {
            return _poisonFlags.TryGetValue(territoryId, out bool poisoned) && poisoned;
        }

        /// <summary>
        /// 특정 영지의 약점 정보를 반환합니다. 정보원이 발견하지 않았다면 빈 문자열.
        /// </summary>
        public static string GetSpyWeakness(TerritoryId territoryId)
        {
            _spyWeaknesses.TryGetValue(territoryId, out var weakness);
            return weakness ?? "";
        }

        /// <summary>
        /// 영지가 이미 점령되었는지 확인합니다.
        /// </summary>
        public static bool IsTerritoryTaken(TerritoryId territoryId)
        {
            return _takenOver.Contains(territoryId);
        }

        /// <summary>
        /// 독살 타이머를 업데이트합니다. POISON_FLAG_DURATION_DAYS 이상 경과 시 플래그가 만료됩니다.
        /// TimeManager 또는 코루틴에서 매 게임 일 단위로 호출합니다.
        /// </summary>
        public static void UpdatePoisonTimers(float deltaDays)
        {
            var expired = new List<TerritoryId>();

            foreach (var kvp in _poisonTimers)
            {
                float newTime = kvp.Value + deltaDays;
                _poisonTimers[kvp.Key] = newTime;

                if (newTime >= POISON_FLAG_DURATION_DAYS)
                {
                    expired.Add(kvp.Key);
                }
            }

            foreach (var id in expired)
            {
                _poisonFlags.Remove(id);
                _poisonTimers.Remove(id);
                Debug.Log($"[PoisonTakeoverSystem] ⏳ 독살 플래그 만료: {id}");
            }
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용)
        /// </summary>
        public static void ResetAll()
        {
            _poisonFlags.Clear();
            _poisonTimers.Clear();
            _spyWeaknesses.Clear();
            _envoySent.Clear();
            _takenOver.Clear();
        }

        // ===== 내부 메서드 =====

        /// <summary>
        /// 독살 점령 실행 — 영지 소유권을 PlayerOwned로 변경하고 이벤트를 발생시킵니다.
        /// </summary>
        private static void ExecutePoisonTakeover(TerritoryId territoryId, TakeoverPath path, GuardPlaceholder envoy)
        {
            var db = TerritoryDatabase.Instance;
            var state = db.GetState(territoryId);
            if (state == null) return;

            // 영지 소유권 변경
            state.ownership = TerritoryOwnership.PlayerOwned;
            state.lordExecuted = true;
            state.lordDefeated = true;
            state.lordSurrendered = true;

            // 독살 플래그 설정
            _poisonFlags[territoryId] = true;
            _poisonTimers[territoryId] = 0f;

            // 점령 완료 등록
            _takenOver.Add(territoryId);

            // 특사 처리 (임무 완료, 특사는 생존)
            Debug.Log($"[PoisonTakeoverSystem] ☠️ 특사 {envoy.GuardName}가 {db.GetDefinition(territoryId).territoryName} 영주를 독살했습니다.");

            // 이벤트 발생
            OnLordPoisoned?.Invoke(territoryId);
            OnTakeoverComplete?.Invoke(territoryId, path);
        }

        /// <summary>
        /// 영주 정보와 첩보 데이터를 바탕으로 약점 설명 문자열을 생성합니다.
        /// </summary>
        private static string BuildWeaknessDescription(TerritoryDefinition def, string spyInfo)
        {
            if (!string.IsNullOrEmpty(def.lord.preferredFood))
                return $"영주 {def.lord.lordName}는 {def.lord.preferredFood}을(를) 좋아합니다. 독살하기 쉽습니다.";
            if (!string.IsNullOrEmpty(def.lord.chronicDisease))
                return $"영주 {def.lord.lordName}는 {def.lord.chronicDisease} 지병이 있습니다. 약을 독으로 대체할 수 있습니다.";

            return $"영주 {def.lord.lordName}의 정보를 입수했습니다. ({spyInfo})";
        }
    }
}