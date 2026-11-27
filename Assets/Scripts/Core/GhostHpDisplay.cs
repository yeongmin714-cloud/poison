using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// [고스트 체력바] 두 줄(앞줄/뒷줄) HP 표시 스무딩 상태 — 순수 수학(UI/Systems 공용, EditMode 검증).
    ///
    /// 동작 (사용자 지시 2026-10-15):
    ///  - 앞줄(Front) = 실제 HP 비율 — 타격 즉시 반영 (한 번에 줄어듦)
    ///  - 뒷줄(Trail) = 타격 직후 이전 값 유지(지연) → 지연 후 서서히 앞줄까지 추격 (부드러운 하강)
    ///  - 회복/상승 — 지연 없이 뒷줄 즉시 동기화 (피격에만 지연 — 힐에서 버벅임 방지)
    ///  - 뒷줄은 항상 앞줄보다 길거나 같음 (연속 타격 누적 흔적 유지)
    ///
    /// 소비처: HUDUTK(원형 게이지 TrailFraction) / NameplateOverlayUTK(이름표 바) /
    ///   MonsterInfoUTK / ArenaBattleUTK — 각 렌더가 Tick(target, dt) 후 Front/Trail로 채움.
    /// </summary>
    public sealed class GhostHpDisplay
    {
        /// <summary>타격 후 뒷줄이 유지되는 지연 (초).</summary>
        public const float TrailDelaySec = 0.35f;
        /// <summary>지연 후 추격 최소 속도 (비율/초 — 전체 바 기준).</summary>
        public const float TrailCatchPerSec = 1.4f;
        /// <summary>거리 비례 추격 계수 (지수 감쇠 — 큰 타격일수록 빠르게 따라옴).</summary>
        public const float TrailCatchProportional = 4.5f;

        private float _front = 1f;   // 앞줄 표시값
        private float _trail = 1f;   // 뒷줄(추격) 표시값
        private float _delay;        // 남은 유지 지연

        public float Front => _front;
        public float Trail => _trail;
        /// <summary>뒷줄이 앞줄을 따라잡는 중(애니메이션 중)인가 — 렌더 스킵 최적화용.</summary>
        public bool IsAnimating => !Mathf.Approximately(_trail, _front);

        /// <summary>
        /// 매 프레임/틱 갱신. target = 실제 HP 비율(0~1), dt = 경과 시간(초).
        /// </summary>
        public void Tick(float target, float dt)
        {
            target = Mathf.Clamp01(target);
            float prevFront = _front;
            _front = target;   // 앞줄 — 즉시
            if (dt <= 0f) return;   // dt 0(일시정지 등) — 앞줄만 동기화

            if (target > prevFront + 0.0001f)
            {
                // 상승(힐/부활) — 지연 없이 즉시 동기화
                _trail = target;
                _delay = 0f;
                return;
            }

            // 동일(target≈prevFront — 무변경 틱) or 하강(피격 프레임) — 아래 유지/추격 로직
            if (target < prevFront - 0.0001f && _delay <= 0f)
                _delay = TrailDelaySec;   // 피격 프레임 — 유지 지연 시작(연속 타격 시 흔적 누적, 지연 중 재연장 없음)

            if (_delay > 0f)
            {
                _delay -= dt;
                return;
            }

            // 지연 종료 — 서서히 추격 (거리 비례 + 최소 속도)
            float gap = _trail - target;
            float step = Mathf.Max(gap * TrailCatchProportional, TrailCatchPerSec) * dt;
            _trail = Mathf.MoveTowards(_trail, target, step);
            if (_trail - target < 0.002f) _trail = target;   // 잔차 스냅
        }

        /// <summary>값 급변 주입(대상 교체/리스폰/초기화) — 지연 없이 양쪽 동기화.</summary>
        public void Reset(float value)
        {
            _front = Mathf.Clamp01(value);
            _trail = _front;
            _delay = 0f;
        }
    }
}
