using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 33-D1] 강타격 파편 데브리 — 결정론 포물선(외부 난수 미사용 — InteriorQualityBaseline.StableHash 시드)
    /// + 지면 1회 바운스. 비주얼 전용(콜라이더 부착 금지), 유한 수명 자가 파괴(누출 금지),
    /// 동시 버스트 예산 가드(상한 4 — 23-X2 선례). 색=FXPalette 단일 소스 유도(Core 감쇠 — 석편 중성 톤).
    /// </summary>
    public static class FxDebrisSpawner
    {
        /// <summary>[33-D1] 동시 버스트 예산 상한 — 초과 시 조용 스킵(대규모 전투 안전망).</summary>
        public const int MaxConcurrentBursts = 4;
        private static int _activeBursts;

        /// <summary>[33-D1] 파편 버스트 — count=시드 기반 3~5결정론, 방향/크기=해시 기반(재현 가능).</summary>
        public static void Burst(Vector3 position, int seed)
        {
            if (!Application.isPlaying) return;                       // EditMode 무동작 가드
            if (_activeBursts >= MaxConcurrentBursts) return;         // 예산 가드 — 초과 조용 스킵
            try
            {
                int count = 3 + (Mathf.Abs(seed) % 3);                // 3~5 — 결정론(시드 기반)
                for (int i = 0; i < count; i++)
                {
                    int h = InteriorQualityBaseline.StableHash($"debris_{seed}_{i}");
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(go.GetComponent<Collider>());      // 비주얼 전용 — 충돌 부착 금지
                    go.name = $"FxDebris_{i}";
                    go.transform.position = position + Vector3.up * 0.6f;
                    float s = 0.05f + ((h & 0x3F) / 63f) * 0.06f;     // 0.05~0.11m — 해시 기반 결정론
                    // [무번호 41-a] 흰 정육면체 → 샤드(파편 조각): 축 신장(2.6×)+결정론 회전+워먼 톤.
                    //   기존=무회전 흰 정육면체(영상 실측 "흰 사각형" 근원) → 의도적 파편 룩.
                    go.transform.localScale = new Vector3(s * 0.55f, s * 0.55f, s * 2.6f);
                    go.transform.rotation = Quaternion.Euler(
                        (h % 360) * 1.0f, ((h >> 8) % 360) * 1.0f, ((h >> 16) % 360) * 1.0f);
                    var mr = go.GetComponent<MeshRenderer>();
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(FXPalette.AttackPrimary.r * 0.7f, FXPalette.AttackPrimary.g * 0.5f, FXPalette.AttackPrimary.b * 0.4f, 1f);   // [41-a 갱신 79-R20] 워먼 석편 톤(통일색 경유 — 흰색 폐기)
                    mr.material = mat;
                    var runner = go.AddComponent<DebrisRunner>();
                    runner.Init(h, mat);
                    _activeBursts++;
                }
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 방해 금지
        }

        private static void Release()
        {
            _activeBursts = Mathf.Max(0, _activeBursts - 1);
        }

        /// <summary>[33-D1] 파편 러너 — 수동 포물선 적분(중력) + 지면 1회 바운스(감쇠 0.45) + 말기 축소 소멸.
        ///  소멸 카운트는 OnDestroy 단일 경로(이중 감산 방지 — FxImpactLight 선례).</summary>
        sealed class DebrisRunner : MonoBehaviour
        {
            private const float Gravity = 9.8f;
            private Vector3 _vel;
            private Material _mat;
            private float _t;
            private float _life = 0.9f;
            private float _groundY;
            private Vector3 _baseScale;
            private bool _bounced;

            public void Init(int hash, Material mat)
            {
                _mat = mat;
                _baseScale = transform.localScale;
                _groundY = transform.position.y - _baseScale.y * 0.5f;
                float azimuth = ((hash & 0xFF) / 255f) * Mathf.PI * 2f;           // 방위 — 해시 기반
                float speed = 2.0f + ((hash >> 8) & 0x0F) / 15f * 1.2f;           // 2.0~3.2 m/s
                float up = 2.2f + ((hash >> 12) & 0x07) / 7f * 0.8f;              // 2.2~3.0 m/s
                _vel = new Vector3(Mathf.Cos(azimuth) * speed, up, Mathf.Sin(azimuth) * speed);
            }

            private void Update()
            {
                _t += Time.unscaledDeltaTime;   // unscaled — 히트스톱 중에도 재생
                if (_t >= _life) { Destroy(gameObject); return; }
                _vel.y -= Gravity * Time.unscaledDeltaTime;
                var p = transform.position + _vel * Time.unscaledDeltaTime;
                if (!_bounced && p.y <= _groundY && _vel.y < 0f)
                {
                    p.y = _groundY;                     // 지면 1회 바운스 — 감쇠 반사
                    _vel.y = -_vel.y * 0.45f;
                    _bounced = true;
                }
                transform.position = p;
                transform.rotation = Quaternion.Euler(_t * 240f, _t * 190f, 0f);   // 회전 굴럽 — 결정론 상수 각속도
                float remain = _life - _t;
                if (remain < 0.25f) transform.localScale = _baseScale * Mathf.Max(0.01f, remain / 0.25f);   // 말기 축소
            }

            private void OnDestroy()
            {
                if (_mat != null) Object.Destroy(_mat);   // 머티리얼 인스턴스 파괴(누출 금지)
                FxDebrisSpawner.Release();               // 카운트 정합(단일 감산 경로)
            }
        }
    }

    /// <summary>
    /// [무번호 33-D2] 소멸 불티 — 디졸브 지점 상승 불티 버스트(0.6s 방출 창, 입자 수명 0.9s).
    ///  - 색=FXPalette.Edge 단일 소스(사망 불티 — 공격 이펙트 아님, 79-R20 통일 대상 외), 머티리얼=FXPalette.ParticleMaterial 정규화 단일 소스(45차 계약).
    ///  - 유한 수명 자가 파괴, EditMode 무동작 가드, 결정론 — 외부 난수 미사용(에미터 상수 전용).
    /// </summary>
    public static class FxEmberRise
    {
        /// <summary>[33-D2] 불티 상승 버스트 — 사망 디졸브 지점 등에서 발화(음수 중력 모디파이어로 상승).</summary>
        public static void Burst(Vector3 position)
        {
            if (!Application.isPlaying) return;   // EditMode 무동작 가드
            try
            {
                var go = new GameObject("FxEmberRise");
                go.transform.position = position + Vector3.up * 0.8f;
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // [무번호 48-T1] 자동 재생 정지 — "재생 중 duration 설정 불가" 에러 차단(47차 실측)
                var main = ps.main;
                main.duration = 0.6f; main.loop = false;
                main.startLifetime = 0.9f; main.startSpeed = 1.4f;
                main.startSize = 0.08f;
                main.startColor = new ParticleSystem.MinMaxGradient(FXPalette.Edge);   // 색 단일 소스(사망 불티 — 79-R20 통일 대상 외)
                main.gravityModifier = -0.12f;   // 음수 중력 — 불티 상승
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission;
                em.rateOverTime = new ParticleSystem.MinMaxCurve(0f);   // 버스트 전용 — 연속 방출 없음
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.35f;
                var rnd = go.GetComponent<ParticleSystemRenderer>();
                // [무번호 48-T1] 중복 AddComponent 제거 — AddComponent<ParticleSystem>()가 렌더러를 자동
                //   부착하므로 다시 AddComponent하면 "Can't add component" 에러 + null 반환(47차 Play 실측) →
                //   ApplyTo 미적용 → null 머티리얼 마젠타 빌보드. GetComponent로 자동 부착본 사용.
                FXPalette.ApplyTo(rnd);   // 정규화 머티리얼 단일 소스(45차 계약) — 47-a 가드 경유
                rnd.renderMode = ParticleSystemRenderMode.Billboard;
                ps.Emit(10);
                Object.Destroy(go, 1.2f);   // 유한 수명 — 자가 파괴(누출 금지)
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 방해 금지
        }
    }
}
