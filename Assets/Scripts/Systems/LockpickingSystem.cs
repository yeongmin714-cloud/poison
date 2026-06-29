using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 35: 자물쇠 따기 미니게임 코어 시스템.
    /// 핀 픽 미니게임: 각 핀마다 목표 높이 + 현재 높이, 상하 이동, 성공/실패 판정.
    /// </summary>
    public static class LockpickingSystem
    {
        /// <summary>
        /// 자물쇠 난이도 등급.
        /// </summary>
        public enum LockDifficulty
        {
            Easy,       // 쉬움
            Medium,     // 보통
            Hard,       // 어려움
            VeryHard,   // 매우 어려움
            Legendary   // 전설
        }

        /// <summary>
        /// 픽 도구 등급.
        /// </summary>
        public enum PickGrade
        {
            Basic,      // 기본
            Advanced,   // 고급
            Master      // 마스터
        }

        /// <summary>
        /// 각 난이도별 핀 개수와 제한 시간 설정.
        /// </summary>
        public static (int pinCount, float timeLimitSeconds) GetDifficultyConfig(LockDifficulty difficulty)
        {
            switch (difficulty)
            {
                case LockDifficulty.Easy:     return (3, 30f);
                case LockDifficulty.Medium:   return (4, 25f);
                case LockDifficulty.Hard:     return (5, 20f);
                case LockDifficulty.VeryHard: return (6, 15f);
                case LockDifficulty.Legendary:return (7, 10f);
                default:                      return (3, 30f);
            }
        }

        /// <summary>
        /// 픽 도구 등급이 해당 난이도를 딸 수 있는지 확인.
        /// </summary>
        public static bool CanPick(PickGrade grade, LockDifficulty difficulty)
        {
            return (int)grade >= (int)difficulty;
        }

        /// <summary>
        /// 픽 도구 최대 내구도.
        /// </summary>
        public static int GetMaxDurability(PickGrade grade)
        {
            switch (grade)
            {
                case PickGrade.Basic:    return 5;
                case PickGrade.Advanced: return 10;
                case PickGrade.Master:   return 20;
                default:                 return 5;
            }
        }

        /// <summary>
        /// 핀 상태: 목표 높이와 현재 높이.
        /// </summary>
        [System.Serializable]
        public struct PinState
        {
            public int index;
            public float targetHeight;   // 목표 높이 (0~1)
            public float currentHeight;  // 현재 높이 (0~1)
            public bool isSet;           // 목표 위치에 고정되었는가
            public float pinSpeed;       // 상승/하강 속도

            public PinState(int idx, float target, float speed)
            {
                index = idx;
                targetHeight = target;
                currentHeight = 0f;
                isSet = false;
                pinSpeed = speed;
            }
        }

        /// <summary>
        /// 미니게임 세션 데이터.
        /// </summary>
        public class LockpickingSession
        {
            public LockDifficulty difficulty;
            public PickGrade pickGrade;
            public int pinCount;
            public float timeRemaining;
            public float timeLimit;
            public PinState[] pins;
            public int currentPinIndex;
            public bool isActive;
            public bool isCompleted;
            public bool isSuccess;
            public int failCount;        // 현재 세션 실패 횟수

            public int consecutiveFails; // 게임 전체 연속 실패 카운트 (LockpickingSystem 전역)
            public string locationId;    // 자물쇠 위치 ID (경보 위치 식별용)
        }

        // 전역 세션
        private static LockpickingSession _currentSession;
        private static int _globalConsecutiveFails = 0;

        /// <summary>
        /// 현재 활성 세션 반환 (null 가능).
        /// </summary>
        public static LockpickingSession CurrentSession => _currentSession;

        /// <summary>
        /// 전역 연속 실패 카운트.
        /// </summary>
        public static int GlobalConsecutiveFails => _globalConsecutiveFails;

        /// <summary>
        /// 연속 실패 카운트 리셋.
        /// </summary>
        public static void ResetConsecutiveFails()
        {
            _globalConsecutiveFails = 0;
            Debug.Log("[LockpickingSystem] 전역 연속 실패 카운트 리셋");
        }

        /// <summary>
        /// 새 미니게임 세션 시작.
        /// </summary>
        public static LockpickingSession StartSession(LockDifficulty difficulty, PickGrade pickGrade, string locationId)
        {
            var config = GetDifficultyConfig(difficulty);
            int pinCount = config.pinCount;
            float timeLimit = config.timeLimitSeconds;

            var session = new LockpickingSession
            {
                difficulty = difficulty,
                pickGrade = pickGrade,
                pinCount = pinCount,
                timeRemaining = timeLimit,
                timeLimit = timeLimit,
                currentPinIndex = 0,
                isActive = true,
                isCompleted = false,
                isSuccess = false,
                failCount = 0,
                locationId = locationId
            };

            // 핀 상태 초기화
            session.pins = new PinState[pinCount];
            for (int i = 0; i < pinCount; i++)
            {
                float target = Random.Range(0.15f, 0.85f);
                float speed = 0.3f + Random.Range(0f, 0.3f);
                session.pins[i] = new PinState(i, target, speed);
            }

            _currentSession = session;
            Debug.Log($"[LockpickingSystem] 세션 시작: 난이도={difficulty}, 핀={pinCount}개, 시간={timeLimit}초, 위치={locationId}");
            return session;
        }

        /// <summary>
        /// 매 프레임 호출: 핀 자동 상승/하강 및 타이머 감소.
        /// </summary>
        public static void UpdateSession(float deltaTime)
        {
            if (_currentSession == null || !_currentSession.isActive || _currentSession.isCompleted)
                return;

            // 타이머 감소
            _currentSession.timeRemaining -= deltaTime;
            if (_currentSession.timeRemaining <= 0f)
            {
                _currentSession.timeRemaining = 0f;
                FailSession("시간 초과");
                return;
            }

            // 아직 고정되지 않은 핀 자동 상승/하강
            for (int i = 0; i < _currentSession.pins.Length; i++)
            {
                if (_currentSession.pins[i].isSet) continue;

                // 핀이 목표 위치 근처면 천천히 움직이고, 아니면 빠르게
                float diff = _currentSession.pins[i].targetHeight - _currentSession.pins[i].currentHeight;
                float speed = _currentSession.pins[i].pinSpeed;

                // 목표에 가까울수록 속도 감소 (미세 조정 어려움)
                float moveAmount = speed * deltaTime;
                if (Mathf.Abs(diff) < 0.1f)
                    moveAmount *= 0.3f; // 근처에서 느리게

                if (diff > 0)
                    _currentSession.pins[i].currentHeight = Mathf.Min(_currentSession.pins[i].targetHeight + 0.02f, _currentSession.pins[i].currentHeight + moveAmount);
                else
                    _currentSession.pins[i].currentHeight = Mathf.Max(-0.02f, _currentSession.pins[i].currentHeight - moveAmount);

                // 범위 클램프
                _currentSession.pins[i].currentHeight = Mathf.Clamp01(_currentSession.pins[i].currentHeight);
            }
        }

        /// <summary>
        /// 현재 선택된 핀 조작 (상승/하강). UI 입력에서 호출.
        /// </summary>
        public static void AdjustCurrentPin(float delta)
        {
            if (_currentSession == null || !_currentSession.isActive || _currentSession.isCompleted)
                return;

            int idx = _currentSession.currentPinIndex;
            if (idx < 0 || idx >= _currentSession.pins.Length) return;

            var pin = _currentSession.pins[idx];
            if (pin.isSet) return;

            pin.currentHeight = Mathf.Clamp01(pin.currentHeight + delta);
            _currentSession.pins[idx] = pin;
        }

        /// <summary>
        /// 현재 핀을 목표 위치에 고정 시도.
        /// </summary>
        public static bool TrySetCurrentPin()
        {
            if (_currentSession == null || !_currentSession.isActive || _currentSession.isCompleted)
                return false;

            int idx = _currentSession.currentPinIndex;
            if (idx < 0 || idx >= _currentSession.pins.Length) return false;

            var pin = _currentSession.pins[idx];
            if (pin.isSet) return false;

            // 목표 위치와 현재 위치 차이 계산
            float diff = Mathf.Abs(pin.currentHeight - pin.targetHeight);

            // 난이도에 따른 허용 오차
            float tolerance = GetTolerance(_currentSession.difficulty);

            if (diff <= tolerance)
            {
                // 성공: 핀 고정
                pin.isSet = true;
                _currentSession.pins[idx] = pin;
                Debug.Log($"[LockpickingSystem] 핀 {idx} 고정 성공! (diff={diff:F3}, tolerance={tolerance:F3})");

                // 다음 핀으로 이동
                _currentSession.currentPinIndex++;

                // 모든 핀이 고정되었으면 성공
                if (_currentSession.currentPinIndex >= _currentSession.pins.Length)
                {
                    CompleteSession(true);
                }

                return true;
            }
            else
            {
                // 실패: 핀 튕겨나감 (failCount 증가)
                _currentSession.failCount++;
                Debug.Log($"[LockpickingSystem] 핀 {idx} 고정 실패! (diff={diff:F3}, tolerance={tolerance:F3})");

                // 내구도 소모 체크는 LockpickingUI에서 처리
                return false;
            }
        }

        /// <summary>
        /// 난이도별 허용 오차.
        /// </summary>
        private static float GetTolerance(LockDifficulty difficulty)
        {
            switch (difficulty)
            {
                case LockDifficulty.Easy:     return 0.20f;
                case LockDifficulty.Medium:   return 0.15f;
                case LockDifficulty.Hard:     return 0.10f;
                case LockDifficulty.VeryHard: return 0.07f;
                case LockDifficulty.Legendary:return 0.05f;
                default:                      return 0.20f;
            }
        }

        /// <summary>
        /// 세션 실패 처리.
        /// </summary>
        private static void FailSession(string reason)
        {
            if (_currentSession == null) return;

            _currentSession.isActive = false;
            _currentSession.isCompleted = true;
            _currentSession.isSuccess = false;

            // 연속 실패 카운트 증가
            _globalConsecutiveFails++;

            Debug.Log($"[LockpickingSystem] 세션 실패: {reason} (연속실패={_globalConsecutiveFails})");

            // 경보 시스템 트리거
            if (_currentSession.difficulty >= LockDifficulty.Hard)
            {
                TriggerAlarm(_currentSession.locationId, _currentSession.difficulty);
            }

            // 3회 연속 실패 → 모든 병사 경계
            if (_globalConsecutiveFails >= 3)
            {
                TriggerGlobalAlert(_currentSession.locationId);
            }

            OnSessionEnded?.Invoke(_currentSession, false);
        }

        /// <summary>
        /// 세션 성공 처리.
        /// </summary>
        private static void CompleteSession(bool success)
        {
            if (_currentSession == null) return;

            _currentSession.isActive = false;
            _currentSession.isCompleted = true;
            _currentSession.isSuccess = success;

            if (success)
            {
                // 성공 시 연속 실패 리셋
                _globalConsecutiveFails = 0;
                Debug.Log("[LockpickingSystem] 세션 성공! 문이 열렸습니다.");
            }

            OnSessionEnded?.Invoke(_currentSession, success);
        }

        // ===== 이벤트 =====

        /// <summary>
        /// 세션 종료 시 호출 (성공/실패).
        /// </summary>
        public static event System.Action<LockpickingSession, bool> OnSessionEnded;

        /// <summary>
        /// 경보 발생 시 호출.
        /// </summary>
        public static event System.Action<string, LockDifficulty> OnAlarmTriggered;

        /// <summary>
        /// 전역 경계 발동 시 호출.
        /// </summary>
        public static event System.Action<string> OnGlobalAlert;

        /// <summary>
        /// 경보 트리거: 어려움 이상 실패 시.
        /// AlarmSystem.TriggerAlarm 호출.
        /// </summary>
        private static void TriggerAlarm(string locationId, LockDifficulty difficulty)
        {
            Debug.Log($"[LockpickingSystem] ⚠️ 경보 발생! 위치={locationId}, 난이도={difficulty}");
            OnAlarmTriggered?.Invoke(locationId, difficulty);
            AlarmSystem.TriggerAlarm(locationId, difficulty);
        }

        /// <summary>
        /// 전역 경계: 3회 연속 실패 시 모든 병사 경계 상태.
        /// AlarmSystem.TriggerGlobalAlert 호출.
        /// </summary>
        private static void TriggerGlobalAlert(string locationId)
        {
            Debug.Log($"[LockpickingSystem] 🚨 전역 경계 발동! 위치={locationId}, 모든 병사가 경계 상태에 돌입합니다.");
            OnGlobalAlert?.Invoke(locationId);
            AlarmSystem.TriggerGlobalAlert(locationId);
        }

        /// <summary>
        /// 마스터 키 사용 가능 여부.
        /// </summary>
        public static bool CanUseMasterKey(LockDifficulty difficulty)
        {
            // 마스터 키는 모든 난이도 사용 가능 (UI에서 체크)
            return true;
        }

        /// <summary>
        /// 마스터 키로 즉시 오픈.
        /// </summary>
        public static bool MasterKeyOpen(LockDifficulty difficulty, string locationId)
        {
            var session = new LockpickingSession
            {
                difficulty = difficulty,
                pickGrade = PickGrade.Master,
                pinCount = 0,
                timeRemaining = 0,
                timeLimit = 0,
                isActive = false,
                isCompleted = true,
                isSuccess = true,
                locationId = locationId
            };

            _currentSession = session;
            _globalConsecutiveFails = 0;

            Debug.Log($"[LockpickingSystem] 🔑 마스터 키 사용! {difficulty} 자물쇠 즉시 오픈.");
            OnSessionEnded?.Invoke(session, true);
            return true;
        }

        /// <summary>
        /// 세션 강제 종료 (UI 닫기 등).
        /// </summary>
        public static void AbortSession()
        {
            if (_currentSession == null) return;
            _currentSession.isActive = false;
            _currentSession.isCompleted = true;
            _currentSession.isSuccess = false;
            _currentSession = null;
        }
    }
}
