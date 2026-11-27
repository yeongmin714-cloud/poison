using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 33-A] 임팩트 실시간 라이트 펄스 — 전투 FX 최초의 동적 광원 계층.
    ///  - 실측 근거: 전투 FX 전체가 발광 쿼드/링뿐, 실제 라이트 반응 0건(유일 선례 = LootSpawnFX.SpawnGoldLight).
    ///  - LootSpawnFX 라이트 러너 패턴 승계: transient GO + Point 라이트 + 유한 수명 선형 소멸 + 자가 파괴(누출 금지).
    ///  - 동시 라이트 예산 가드(상한 3 — 초과 조용 스킵, 23-X2 FX 예산 가드 선례).
    ///  - unscaled 시간(히트스톱 중에도 재생 — VignettePulse 선례), EditMode 무동작 가드, 결정론 — 외부 난수 미사용.
    /// </summary>
    public static class FxImpactLight
    {
        /// <summary>[33-A1] 동시 임팩트 라이트 예산 — 초과 시 조용 스킵(대규모 전투 안전망).</summary>
        public const int MaxConcurrentLights = 3;
        private static int _activeCount;

        /// <summary>[33-A1] 임팩트 라이트 펄스 — 즉시 peak → duration 동안 선형 소멸 → 자가 파괴.
        ///  color는 FXPalette 단일 소스 경유(호출부에서 전달). 예산 초과/편집 모드는 무음 스킵.</summary>
        public static void Spike(Vector3 position, Color color, float intensity, float range, float duration)
        {
            if (!Application.isPlaying) return;                       // EditMode 무동작 가드
            if (_activeCount >= MaxConcurrentLights) return;          // 예산 가드 — 초과 조용 스킵(X2 선례)
            try
            {
                var go = new GameObject("FxImpactLight");
                go.transform.position = position + Vector3.up * 0.5f;   // 지면 언더플로 방지 반올림
                var runner = go.AddComponent<ImpactLightRunner>();
                runner.Init(color, intensity, range, duration);
                _activeCount++;
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 흐름 절대 방해 금지
        }

        private static void Release()
        {
            _activeCount = Mathf.Max(0, _activeCount - 1);
        }

        /// <summary>[33-A] 라이트 러너 — 즉시 peak → duration 선형 소멸 → 자가 파괴.
        ///  소멸 카운트는 OnDestroy 단일 경로(Update 파괴 + 씬 전환 조기 소멸 모두 커버 — 이중 감산 방지).</summary>
        sealed class ImpactLightRunner : MonoBehaviour
        {
            private Light _light;
            private float _base;
            private float _duration;
            private float _t;

            public void Init(Color color, float intensity, float range, float duration)
            {
                _light = gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = color;
                _light.range = Mathf.Max(1f, range);
                _light.intensity = Mathf.Max(0f, intensity);
                _light.shadows = LightShadows.None;   // 단명 라이트 — 섀도우 비용 낭비 방지(LootSpawnFX 선례)
                _base = _light.intensity;
                _duration = Mathf.Max(0.05f, duration);
                _t = 0f;
            }

            private void Update()
            {
                _t += Time.unscaledDeltaTime;   // unscaled — 히트스톱 중에도 재생
                if (_light == null) { Destroy(gameObject); return; }
                if (_t >= _duration) { Destroy(gameObject); return; }
                _light.intensity = _base * (1f - Mathf.Clamp01(_t / _duration));   // 즉시 peak → 선형 소멸
            }

            private void OnDestroy()
            {
                FxImpactLight.Release();   // 유한 수명 종료/조기 소멸 공용 — 카운트 정합(단일 감산 경로)
            }
        }
    }
}
