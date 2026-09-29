// [테스트 42] 크리티컬/특수 히트 국소 슬로우모션(Bullet Time) — 전역 Time.timeScale을
// 잠시 낮췄다 복원해 임팩트를 강조한다. (0.4배 디핑 → 홀드 → 1회복)
// Magic Pig Games 'Magic Time' 에셋의 전역 시간 팩터 활용 대안으로, 선별(오브제별) 슬로우는
// 추후 MagicTimeUser 구독으로 확장 가능. 본 파일은 단순·견고한 전역 시간 배율 램프만 담당.
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 전역 시간 배율을 디핑→홀드→복원하는 슬로우모션 편의 유틸. 싱글톤(멱등), DontDestroyOnLoad.
    /// 사용: BulletTime.Get().Apply(0.4f, 0.10f, 0.12f) — 0.1s 동안 0.4배로 디핑 후 0.12s 홀드 후 1배 복원.
    /// </summary>
    public class BulletTime : MonoBehaviour
    {
        private static BulletTime _instance;

        public static BulletTime Get()
        {
            if (_instance == null)
            {
                var go = new GameObject("BulletTime");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<BulletTime>();
            }
            return _instance;
        }

        // 시간 배율 램프 상태 머신
        private enum Phase { Idle, Dip, Hold, Restore }
        private Phase _phase = Phase.Idle;
        private float _targetScale = 1f;
        private float _timer = 0f;
        private float _dipDur = 0f;
        private float _holdDur = 0f;
        private float _restoreDur = 0.12f;
        private float _phaseStartScale = 1f;

        /// <summary>새 슬로우모션 적용(기존 진행 중이어도 재시작). dipDur 동안 targetScale로 디핑, holdDur 홀드, 복원.</summary>
        public void Apply(float targetScale, float dipDur, float holdDur)
        {
            _phaseStartScale = Time.timeScale;
            _targetScale = Mathf.Clamp(targetScale, 0.05f, 1f);
            _dipDur = Mathf.Max(0.01f, dipDur);
            _holdDur = Mathf.Max(0f, holdDur);
            _restoreDur = 0.12f;
            _phase = Phase.Dip;
            _timer = 0f;
        }

        /// <summary>즉시 정상 속도로 복원(중단).</summary>
        public void Reset()
        {
            _phase = Phase.Idle;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (_phase == Phase.Idle) return;
            _timer += Time.unscaledDeltaTime;

            switch (_phase)
            {
                case Phase.Dip:
                {
                    float p = Mathf.Clamp01(_timer / _dipDur);
                    Time.timeScale = Mathf.Lerp(_phaseStartScale, _targetScale, p);
                    if (p >= 1f) { _phase = _holdDur > 0f ? Phase.Hold : Phase.Restore; _timer = 0f; }
                    break;
                }
                case Phase.Hold:
                {
                    Time.timeScale = _targetScale;
                    if (_timer >= _holdDur) { _phase = Phase.Restore; _timer = 0f; }
                    break;
                }
                case Phase.Restore:
                {
                    float p = Mathf.Clamp01(_timer / _restoreDur);
                    Time.timeScale = Mathf.Lerp(_targetScale, 1f, p);
                    if (p >= 1f) { Time.timeScale = 1f; _phase = Phase.Idle; }
                    break;
                }
                default: break;
            }
        }
    }
}