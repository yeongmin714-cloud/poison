using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 폭탄 폭발 시각 효과 — Sprite 스파크 버스트 + 주황 섬광.
    /// Bomb.Explode가 폭탄 오브젝트를 Destroy(gameObject)할 때 이 컴포넌트의
    /// OnDisable이 발화하여 CombatVFXController.SpawnHitSparks(골드화이트 직선 스파크)를
    /// 폭발 위치에 생성한다. 폭탄 오브젝트에 addComponent로 함께 부착한다.
    /// [205차] 폭발 다단 VFX 고품질화 — 발화 순서: A 플래시 퀵 → B 화구 → C 기존 화염(VFX_Fire_01_Big,
    /// FirePath/FireLifetime 계약 무손상) → E 연기 페이드.
    /// [206차] 충격링 제거 — 사용자 결정(구 D 충격 링 스테이지 호출부 철수, ShockwaveRingFX 공용 유틸 무수정).
    /// 전부 절차 생성(신규 에셋 불필요), 신규 스테이지는 Application.isPlaying 가드(21-K1 선례 —
    /// EditMode 스폰 0건), 과도 오브젝트 수명 소멸(누수 0 — 영속화 불사용).
    /// </summary>
    public class BombExplosionVisual : MonoBehaviour
    {
        private bool _spawned; // 폭발/파괴 직전 1회만 발화

        private const string FirePath = "FX/Fire/VFX_Fire_01_Big";
        private const float FireLifetime = 2.5f; // 프리팹 stopAction:0(자가소멸 없음)이라 수동 Destroy 필요

        // ────── [205차] 폭발 다단 상수(트림 대기 — Play 실측 후 조정) ──────
        // 스테이지 마커 상수 — 계약테스트(BombExplosionVFX205Tests) 소스 핀용
        internal const string StageFlashMarker = "[205차] A 플래시 퀵";
        internal const string StageFireballMarker = "[205차] B 화구";
        internal const string StageSmokeMarker = "[205차] E 연기 페이드";
        // [206차] 충격링 제거 — 사용자 결정(구 StageShockRingMarker/D 스테이지 상수 철수)

        private const float DefaultExplosionRadius = 3f;   // Bomb.explosionRadius 실측 폴백(BombArmController 기본값 동일)
        private const float FlashLifetime = 0.05f;         // A 플래시 수명(초)
        private const float FlashScaleRatio = 0.8f;        // A 플래시 스케일 = 폭발반경 × 0.8
        private const float FireballGrowTime = 0.15f;      // B 화구 스케일 펄스 시간(초)
        private const float FireballScaleRatio = 0.7f;     // B 화구 목표 스케일 = 폭발반경 × 0.7
        private const float FireballLifetime = 0.5f;       // B 화구 전체 수명(초) — 0.15s 펄스 후 페이드
        private const float SmokeLifetime = 1.2f;          // E 연기 수명(초)
        private const int SmokePuffCount = 3;              // E 연기 퍼프 개수(스펙 2~3개)
        private const int SmokeSeed = 205205;              // 결정론 해시 시드(Random 0건)

        private static readonly Color FlashColorFrom = new Color(1f, 0.92f, 0.70f, 1f);      // 순백-주황 사이 코어
        private static readonly Color FlashColorTo = new Color(1f, 0.55f, 0.15f, 0f);        // 가장자리 주황(알파 0 페이드)
        private static readonly Color FireballColorFrom = new Color(1f, 0.5f, 0.12f, 1f);    // 화구 주황
        private static readonly Color FireballColorTo = new Color(0.12f, 0.09f, 0.07f, 0f);  // 검정 유사(알파 0 페이드)
        private static readonly Color SmokeColor = new Color(0.30f, 0.25f, 0.20f, 0.55f);    // 암갈색 연기

        private void OnDisable()
        {
            // 게임 셧다운/씬 전환 시에도 방어적으로 1회만 처리
            if (_spawned) return;
            if (transform == null) return;

            Vector3 pos = transform.position;
            _spawned = true;

            // [205차] 폭발 반경 실측 — Bomb.explosionRadius(컴포넌트 부재/무효값 시 기본값 폴백)
            float radius = DefaultExplosionRadius;
            var bomb = GetComponent<Bomb>();
            if (bomb != null && bomb.explosionRadius > 0f)
                radius = bomb.explosionRadius;

            // 골드화이트 직선 스파크 버스트 (기존 CombatVFXController 재사용)
            CombatVFXController.SpawnHitSparks(pos);

            // [무번호 73-E2] 폭발 → 반경·강도 확대 오브젝트 스윙(37-E2 계층3 — 폭발 트리거).
            FxEnvReaction.TriggerExplosionSway(pos);

            // [205차 A 플래시 퀵] 카메라 빌보드 쿼드 — Unlit 가산 순백→주황, 수명 0.05s, 스케일 폭발반경×0.8.
            //   신규 스테이지 전부 Play 가드(21-K1 선례) — EditMode에서 스폰 0건.
            if (Application.isPlaying)
                SpawnFlashQuick(pos, radius);

            // [205차 B 화구] 스피어 프리미티브 — 절차 머티리얼(주황→검정 그라디언트 유사),
            //   스케일 펄스 0→폭발반경×0.7(0.15s) 후 알파 페이드.
            if (Application.isPlaying)
                SpawnFireball(pos, radius);

            // 불꽃 VFX — Resources에서 로드해 폭발 위치에 생성, 일정 시간 후 수동 Destroy
            //   (기존 C 스테이지 — [205차] FirePath/FireLifetime 계약 무손상, 다단과 공존)
            var firePrefab = Resources.Load<GameObject>(FirePath);
            if (firePrefab != null)
            {
                var fire = GameObject.Instantiate(firePrefab, pos, Quaternion.identity);
                fire.transform.localScale = Vector3.one * 1.4f;
                // [189차] 불 VFX에 묻어있는 불 소리 차단 — 폭발음으로 일원화 (Molotov 경로 무영향).
                // OnDisable은 Play 중 스폰 인스턴스에서만 발화하므로 Object.Destroy 안전(프리팹 에셋 무손상).
                foreach (var a in fire.GetComponentsInChildren<AudioSource>(true)) { if (a != null) Object.Destroy(a); }
                GameObject.Destroy(fire, FireLifetime);
            }

            // [206차] 충격링 제거 — 사용자 결정(구 D 스테이지 호출부 철수 — ShockwaveRingFX 공용 유틸은 무수정)

            // [205차 E 연기 페이드] 암갈색 연기 퍼프 3개 상승 — 수명 1.2s 알파 페이드(결정론 해시 배치).
            if (Application.isPlaying)
                SpawnSmokePuffs(pos, radius);
        }

        // ================================================================
        // [205차] 폭발 다단 VFX — 절차 생성(신규 에셋 불필요), 누수 0(자가 소멸)
        // ================================================================

        /// <summary>
        /// [205차] A 플래시 퀵 — 카메라 빌보드 쿼드, Unlit 가산 순백→주황, 수명 FlashLifetime(0.05s),
        /// 스케일 폭발반경×0.8. 프리미티브 생성(기존 BombFlash 선례) + 러너 트윈 후 자기 파괴(누수 0).
        /// 셰이더/렌더러 부재 시 무음 폴백(null 가드만 — 트라이캐치 불필요).
        /// </summary>
        private static void SpawnFlashQuick(Vector3 pos, float radius)
        {
            var flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flash.name = "Bomb205_Flash";
            var col = flash.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);   // 시각 전용 — 기존 BombFlash 선례
            flash.transform.position = pos + FlashCameraOffset(pos);
            flash.transform.localScale = Vector3.one * Mathf.Max(0.2f, radius * FlashScaleRatio);

            var ren = flash.GetComponent<MeshRenderer>();
            if (ren == null) { Object.Destroy(flash); return; }   // 렌더러 부재 — 무음 폴백+즉시 정리
            var mat = CreateStageMaterial("Bomb205FlashMat", additive: true);
            if (mat == null) { Object.Destroy(flash); return; }   // 셰이더 부재 — 무음 폴백+즉시 정리
            if (!FxMaterialLibrary.TryAssignSharedMaterials(ren, new Material[] { mat }, "Bomb205Flash"))
            {
                Object.Destroy(mat);   // 대입 실패 — 머티리얼 즉시 정리(누수 방지), GO는 기본 머티리얼로 수명 소멸
                mat = null;
            }
            TransientStageRunner.Attach(flash, mat, FlashColorFrom, FlashColorTo,
                FlashLifetime, growTime: 0f, targetScale: 0f, billboard: true, pulse: false);
        }

        /// <summary>
        /// [205차] B 화구 — 스피어 프리미티브 + 절차 머티리얼(주황→검정 그라디언트 유사),
        /// 스케일 펄스 0→폭발반경×0.7(FireballGrowTime 0.15s) 후 알파 페이드(FireballLifetime 0.5s).
        /// </summary>
        private static void SpawnFireball(Vector3 pos, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bomb205_Fireball";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);   // 시각 전용
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.05f;   // 펄스 시작 — 러너가 0.15s 동안 목표 스케일로 확장

            var ren = go.GetComponent<MeshRenderer>();
            if (ren == null) { Object.Destroy(go); return; }      // 렌더러 부재 — 무음 폴백+즉시 정리
            var mat = CreateStageMaterial("Bomb205FireballMat", additive: false);
            if (mat == null) { Object.Destroy(go); return; }      // 셰이더 부재 — 무음 폴백+즉시 정리
            if (!FxMaterialLibrary.TryAssignSharedMaterials(ren, new Material[] { mat }, "Bomb205Fireball"))
            {
                Object.Destroy(mat);   // 대입 실패 — 머티리얼 즉시 정리(누수 방지)
                mat = null;
            }
            TransientStageRunner.Attach(go, mat, FireballColorFrom, FireballColorTo,
                FireballLifetime, FireballGrowTime, Mathf.Max(0.2f, radius * FireballScaleRatio),
                billboard: false, pulse: true);
        }

        /// <summary>
        /// [205차] E 연기 페이드 — 암갈색 연기 퍼프 SmokePuffCount(3)개 상승, 수명 SmokeLifetime(1.2s) 알파 페이드.
        /// 절차 PS(FxImpactDustColumn 선례 — 결정론 Emit 벤치, Random 0건) + 유한 수명 자가 파괴(누수 0).
        /// </summary>
        private static void SpawnSmokePuffs(Vector3 pos, float radius)
        {
            var mat = FxMaterialLibrary.CreateSoftParticle("Bomb205SmokeMat", SmokeColor, EnsureSmokePuffTexture());
            if (mat == null) return;   // 머티리얼 부재 — 무음 폴백

            var go = new GameObject("Bomb205_Smoke");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            if (ps == null) { Object.Destroy(go); Object.Destroy(mat); return; }   // 파티클 부재 — 무음 폴백+즉시 정리
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // [무번호 48-T1] 자동 재생 정지 선례
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = SmokeLifetime;
            main.startSpeed = 0f;   // 속도는 Emit 벤치로 위임(결정론)
            main.startSize = 1f;
            main.maxParticles = SmokePuffCount * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // 월드 고정 — 폭탄 소멸 무관
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(grad);
            var rnd = go.GetComponent<ParticleSystemRenderer>();
            if (!FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, "Bomb205Smoke"))
            {
                Object.Destroy(mat);   // 대입 실패 — 머티리얼 즉시 정리(누수 방지)
                mat = null;
            }

            // 결정론 벤치 — 퍼프 3개 해시 배치 + 상승 속도(Random 0건 — FxImpactDustColumn 5인자 Emit 선례)
            float spread = Mathf.Max(0.5f, radius * 0.4f);
            for (int i = 0; i < SmokePuffCount; i++)
            {
                float h1 = Hash01(i, 1, SmokeSeed);
                float h2 = Hash01(i, 2, SmokeSeed);
                float h3 = Hash01(i, 3, SmokeSeed);
                var p0 = pos + new Vector3((h1 - 0.5f) * spread, 0.3f + 0.6f * h2, (h3 - 0.5f) * spread);
                var v0 = new Vector3((h2 - 0.5f) * 0.4f, 0.8f + 0.6f * h1, (h3 - 0.5f) * 0.4f);   // 상승 0.8~1.4 m/s
                ps.Emit(p0, v0, 0.9f + 0.4f * h2, SmokeLifetime, Color.white);
            }
            Object.Destroy(go, SmokeLifetime + 0.2f);   // 자가 파괴 — 누수 0
            Object.Destroy(mat, SmokeLifetime + 0.2f);  // 스폰별 머티리얼 동행 소멸
        }

        /// <summary>[205차] 플래시 카메라 쪽 마이크로 오프셋(0.1m) — 화구/링과 z-fight 방지. 카메라 부재 시 무오프셋.</summary>
        private static Vector3 FlashCameraOffset(Vector3 pos)
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            Vector3 dir = pos - cam.transform.position;
            if (dir.sqrMagnitude < 0.0001f) return Vector3.zero;
            return dir.normalized * 0.1f;
        }

        /// <summary>
        /// [205차] 스테이지 절차 머티리얼 — URP/Unlit 우선, 없으면 Sprites/Default 폴백, 둘 다 없으면 null
        /// (호출부 무음 폴백 — BombArmController.CreateUnlitTransparentMaterial 선례).
        /// additive=true 가산(플래시)/false 알파 블렌드(화구).
        /// </summary>
        private static Material CreateStageMaterial(string matName, bool additive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;   // 셰이더 부재 가드 — null 머티리얼 허용

            var mat = new Material(shader) { name = matName };
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);   // 1 = Transparent
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive)
            {
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            else
            {
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        private static Texture2D _smokePuffTex;   // 연기 퍼프 텍스처 캐시(1회 생성 — 세션 생존)

        /// <summary>[205차] 연기 퍼프 절차 텍스처 64×64 — smoothstep 방사 소프트 서클(사각 금지 계약 — 156차 선례),
        /// 결정론(Random 0건), 정적 캐시 1회 생성.</summary>
        private static Texture2D EnsureSmokePuffTexture()
        {
            if (_smokePuffTex != null) return _smokePuffTex;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.name = "Bomb205SmokePuffTex";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[S * S];
            float c = S * 0.5f;
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d));   // 방사 소프트 마스크 — 사각 금지
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            _smokePuffTex = tex;
            return tex;
        }

        /// <summary>[205차] 결정론 격자 해시 — PuffHash(ShockwaveRingFX) 동일식(랜덤 0건).</summary>
        private static float Hash01(float x, float y, int seed)
        {
            return Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f, 1f);
        }

        /// <summary>
        /// [205차] 다단 과도 오브젝트 러너 — 플래시(카메라 빌보드+색 페이드)·화구(스케일 펄스+주황→검정 유사 페이드) 트윈.
        /// 수명 종료 시 Destroy(gameObject) 자기 파괴(누수 0) — 영속화 불사용.
        /// 스폰별 머티리얼 인스턴스는 OnDestroy에서 정리(ShockwaveRingFX.SparkRunner 선례).
        /// </summary>
        private class TransientStageRunner : MonoBehaviour
        {
            private Material _mat;
            private Color _from;
            private Color _to;
            private float _life;
            private float _grow;
            private float _targetScale;
            private float _elapsed;
            private bool _billboard;
            private bool _pulse;

            public static void Attach(GameObject host, Material mat, Color from, Color to,
                float life, float growTime, float targetScale, bool billboard, bool pulse)
            {
                if (host == null) return;
                var r = host.AddComponent<TransientStageRunner>();
                if (r == null) return;   // 러너 부재 — GO는 생성부 수명 Destroy 경로 유지
                r._mat = mat;
                r._from = from;
                r._to = to;
                r._life = Mathf.Max(0.01f, life);
                r._grow = Mathf.Max(0.01f, growTime);
                r._targetScale = Mathf.Max(0.05f, targetScale);
                r._billboard = billboard;
                r._pulse = pulse;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _life);
                if (_billboard)
                {
                    var cam = Camera.main;
                    if (cam != null)
                        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
                }
                if (_pulse)
                {
                    // [205차] 스케일 펄스 — 0→목표 Ease-Out(FireballGrowTime 0.15s), 이후 목표 유지
                    float gt = Mathf.Clamp01(_elapsed / _grow);
                    float eased = 1f - (1f - gt) * (1f - gt);
                    transform.localScale = Vector3.one * Mathf.Max(0.05f, eased * _targetScale);
                }
                if (_mat != null)
                    _mat.color = Color.Lerp(_from, _to, t);   // A 순백→주황 / B 주황→검정 유사(알파 동시 페이드)
                if (t >= 1f)
                    Destroy(gameObject);   // 자기 파괴 — 누수 0
            }

            private void OnDestroy()
            {
                // 조기 파괴(씬 전환 등) 시 스폰별 머티리얼 누수 방지 — SparkRunner 선례
                if (_mat != null)
                    Object.Destroy(_mat);
            }
        }
    }
}
