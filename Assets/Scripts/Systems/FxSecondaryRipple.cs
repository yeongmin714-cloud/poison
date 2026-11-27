using System.Collections;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 77] 크리 2차 리플 에코 스케줄러 — 디스토션 히트 웨이브(71-L2) 뒤 2차 저강도 리플(잔광).
    ///  - 지연 발화 전용: WaitForSecondsRealtime(delaySec) — 히트스톱 프리즈 중에도 시간 언어가
    ///    이어지는 23-X1 unscaled 규약 준용(디스토션 웨이브 자체도 unscaled — 71-L2).
    ///  - 재사용 경로: 발화는 FxDistortionWave.Burst(position, scale) 1회 위임 — FxDistortionWave.cs
    ///    무수정(71-L2 잠금 문자열 보존). 예산은 Burst 내부 동시 상한(MaxConcurrentWaves 4) 단일 소유 —
    ///    별도 카운터 신설 금지, 초과 시 조용 스킵(기존 계약 승계).
    ///  - 동일 지점 중복 스케줄 가드: 정적 마지막 스케줄 시각 + CooldownSec(결정론 — Time.time 기반 필드).
    ///  - 크리 전용 게이트(CombatFXGate if (isCrit) 배선) — 일반 타격 미발화(76-S3와 다른 밴드 — 크리 시그니처의 여운).
    ///  - 결정론 — 난수 경로 0건, 물리 경로 0건. 파라미터: scale 0.55f(1차 대비 저강도·소형),
    ///    delay 0.12f(1차 확장 중반부에 뒤따르는 잔광) — CombatFXGate 호출부 결정론 상수.
    /// </summary>
    public static class FxSecondaryRipple
    {
        /// <summary>[무번호 77] 동일 지점 중복 스케줄 쿨다운 — 결정론 상수(스케줄 스팸 방지).</summary>
        private const float CooldownSec = 0.12f;

        /// <summary>[무번호 77] 마지막 스케줄 시각(Time.time 기반 — 결정론). -999f = 초기 무발화.</summary>
        private static float _lastScheduleTime = -999f;

        /// <summary>[무번호 77] 크리 2차 리플 예약 — delaySec 후 FxDistortionWave.Burst(position, scale) 1회.</summary>
        public static void Schedule(Vector3 position, float scale, float delaySec)
        {
            if (!Application.isPlaying) return;   // EditMode 무동작 가드 — Burst 내부 가드와 이중 안전
            if (Time.time - _lastScheduleTime < CooldownSec) return;   // 중복 스케줄 가드 — 결정론 쿨다운
            _lastScheduleTime = Time.time;

            // Host — HideAndDontSave(하이어라키 비표시 + 씬 잔존 방지 — 71-L2 호스트 선례 동일).
            var host = new GameObject("SecondaryRipple") { hideFlags = HideFlags.HideAndDontSave };
            host.AddComponent<SecondaryRippleRunner>().Init(position, scale, delaySec);
        }

        /// <summary>[무번호 77] 리플 러너 — unscaled 지연 후 Burst 1회 → 자가 파괴(유한 수명 — 누출 금지).</summary>
        private sealed class SecondaryRippleRunner : MonoBehaviour
        {
            public void Init(Vector3 position, float scale, float delaySec)
            {
                StartCoroutine(Run(position, scale, delaySec));
            }

            private IEnumerator Run(Vector3 position, float scale, float delaySec)
            {
                yield return new WaitForSecondsRealtime(delaySec);   // [23-X1] unscaled 규약 — 히트스톱 중에도 진행
                FxDistortionWave.Burst(position, scale);   // [무번호 77] 1차 웨이브 잔광 — 예산(Burst 내부 상한 4) 공유, 초과 조용 스킵
                if (Application.isPlaying) Object.Destroy(gameObject);   // 자가 파괴(Play)
                else Object.DestroyImmediate(gameObject);                // EditMode(21-K1 선례)
            }

            private void OnDestroy()
            {
                // [무번호 77] 안전망 — 발화 전 조기 파괴(씬 전환 등) 시에도 누출 0:
                //   예산은 Burst 내부(동시 상한 4) 단일 소유 — 러너 측 감산 경로 없음(이중 감산 방지),
                //   대기 중 코루틴은 오브젝트 파괴와 함께 자동 정지(잔여 발화 차단).
                StopAllCoroutines();
            }
        }
    }
}
