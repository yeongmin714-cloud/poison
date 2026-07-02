using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ProjectName.Systems
{
    /// <summary>
    /// 컨트롤러 진동(햅틱 피드백)을 관리하는 정적 클래스.
    /// Gamepad.current.SetMotorSpeeds()를 사용하여 게임패드 럼블을 제어합니다.
    /// 
    /// 🔊 Phase 3.10: 컨트롤러 진동
    /// </summary>
    public static class HapticFeedback
    {
        /// <summary>
        /// 미리 정의된 럼블 프리셋
        /// </summary>
        public enum RumblePreset
        {
            /// <summary>약한 진동 (기본 공격 hit, 몬스터 사망)</summary>
            Light,
            /// <summary>중간 진동 (강한 공격, 피격 큰 데미지)</summary>
            Medium,
            /// <summary>강한 진동 (폭발, 암살)</summary>
            Heavy,
            /// <summary>짧은 진동 (UI 상호작용)</summary>
            Short,
            /// <summary>긴 저주파 진동 (차량 질주 등)</summary>
            Long,
            /// <summary>사용자 정의 (Play() 직접 호출)</summary>
            Custom
        }

        /// <summary>
        /// 게임패드 연결 여부 (Gamepad.current != null)
        /// </summary>
        public static bool IsGamepadConnected => Gamepad.current != null;

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// 미리 정의된 프리셋으로 진동을 재생합니다.
        /// </summary>
        /// <param name="preset">재생할 럼블 프리셋</param>
        public static void PlayPreset(RumblePreset preset)
        {
            if (!IsGamepadConnected) return;
            if (Gamepad.current == null) return;

            switch (preset)
            {
                case RumblePreset.Light:
                    // 기본 공격 hit (0.2s) / 몬스터 사망 (0.15s)
                    PlayInternal(0.25f, 0.25f, 0.2f);
                    break;

                case RumblePreset.Medium:
                    // 강한 공격 / 피격 큰 데미지 (0.3s)
                    PlayInternal(0.5f, 0.5f, 0.3f);
                    break;

                case RumblePreset.Heavy:
                    // 폭발 (0.4s) / 암살 (0.5s)
                    PlayInternal(1.0f, 1.0f, 0.45f);
                    break;

                case RumblePreset.Short:
                    // UI 상호작용 (0.05s) — 매우 짧은 틱
                    PlayInternal(0.3f, 0.3f, 0.05f);
                    break;

                case RumblePreset.Long:
                    // 차량 질주 등 — 저주파 연속 진동
                    PlayInternal(0.1f, 0.1f, 3.0f);
                    break;
            }
        }

        /// <summary>
        /// 사용자 정의 진동을 재생합니다.
        /// </summary>
        /// <param name="lowFreq">저주파 모터 강도 (0.0 ~ 1.0)</param>
        /// <param name="highFreq">고주파 모터 강도 (0.0 ~ 1.0)</param>
        /// <param name="duration">진동 지속 시간 (초)</param>
        public static void Play(float lowFreq, float highFreq, float duration)
        {
            if (!IsGamepadConnected) return;
            if (Gamepad.current == null) return;

            PlayInternal(lowFreq, highFreq, duration);
        }

        /// <summary>
        /// 모든 모터를 즉시 정지합니다.
        /// </summary>
        public static void StopRumble()
        {
            if (Gamepad.current == null) return;

            Gamepad.current.SetMotorSpeeds(0f, 0f);
            _stopPending = false;
        }

        // =====================================================================
        // Internal
        // =====================================================================

        // 코루틴 실행을 위한 숨은 MonoBehaviour
        private static HapticCoroutineRunner _runner;
        private static bool _stopPending = false;

        /// <summary>
        /// 실제 모터 설정 + 자동 정지 코루틴 실행
        /// </summary>
        private static void PlayInternal(float lowFreq, float highFreq, float duration)
        {
            if (Gamepad.current == null) return;

            // 이전 진동 정지
            if (_stopPending)
            {
                StopRumble();
            }

            // 모터 설정
            Gamepad.current.SetMotorSpeeds(lowFreq, highFreq);
            _stopPending = true;

            // 자동 정지 코루틴 시작
            EnsureRunner();
            _runner.StartCoroutine(StopAfterDelay(duration));
        }

        /// <summary>
        /// 지정된 시간 후 모터를 정지하는 코루틴
        /// </summary>
        private static IEnumerator StopAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (Gamepad.current != null)
            {
                Gamepad.current.SetMotorSpeeds(0f, 0f);
                _stopPending = false;
            }
        }

        /// <summary>
        /// 코루틴 실행을 위한 숨은 게임 오브젝트를 생성/확보합니다.
        /// </summary>
        private static void EnsureRunner()
        {
            if (_runner != null) return;

            var go = new GameObject("[HapticCoroutineRunner]");
            GameObject.DontDestroyOnLoad(go);
            _runner = go.AddComponent<HapticCoroutineRunner>();
        }

        /// <summary>
        /// HapticFeedback의 코루틴을 실행하기 위한 내부 MonoBehaviour.
        /// 씬 전환에도 유지됩니다 (DontDestroyOnLoad).
        /// </summary>
        private class HapticCoroutineRunner : MonoBehaviour
        {
            private void Awake()
            {
                // 씬 전환 시 파괴 방지
                DontDestroyOnLoad(gameObject);
            }

            private void OnDestroy()
            {
                // 러너가 파괴될 때 모터 정리
                if (Gamepad.current != null)
                {
                    Gamepad.current.SetMotorSpeeds(0f, 0f);
                }
                _stopPending = false;
                _runner = null;
            }
        }
    }
}