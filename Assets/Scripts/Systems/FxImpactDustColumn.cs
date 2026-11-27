using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 76-S2] 충격 먼지 컬럼 — 강타격 지점에서 위로 솟는 노이즈 먼지 기둥(보완 레이어).
    /// 계약: 전용 절차 텍스처 64×256 세로 스트립 1회 캐시 — 기존 퍼프 텍스처 재사용 경로는 private 잠금(74 확정 — 무참조),
    /// 세로 value-noise 그라데이션(하단 진·상단 투명)+하단 방사 마스크(사각 금지 — 결정론 해시, Random 0건),
    /// 파티클 4개 수직 에미션(위로 1.2~1.8m, lifetime 0.5s, 세로 스트레치 빌보드),
    /// 색 흙톤(0.78,0.72,0.62,0.35) — TrailDustColor 선례 정합, 사운드 0건(72-W3 히트 사운드 위계 무손상),
    /// 쿨다운 0.12s(스파크와 독립 카운터)·수명 유한·EditMode 즉시 정리 가드(21-K1 선례).
    /// </summary>
    public static class FxImpactDustColumn
    {
        private const float Lifetime = 0.5f;      // 상승 수명(계획 계약)
        private const float CooldownSec = 0.12f;  // 스파크와 독립 카운터(Time.time 정적 필드 — 결정론)
        private const int ParticleCount = 4;      // 수직 에미션 4개
        private const int Seed = 761118;          // 결정론 해시 시드

        private static Texture2D _strip;
        private static float _lastSpawnTime = -999f;

        /// <summary>강타격 지점 먼지 컬럼 발화 — CombatFXGate 강타격 게이트 밴드(isCrit || damage >= 15f)에서 호출.</summary>
        public static void Spawn(Vector3 position)
        {
            if (!Application.isPlaying) return;   // EditMode 가드 — 스폰 0건
            if (Time.time - _lastSpawnTime < CooldownSec) return;   // 독립 쿨다운(정적 필드 — 결정론)

            var mat = FxMaterialLibrary.CreateSoftParticle("ImpactDustColumnMat", new Color(1f, 1f, 1f, 1f), EnsureColumnTexture());
            if (mat == null) return;

            var go = new GameObject("ImpactDustColumn");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // [무번호 48-T1] 자동 재생 정지 선례
            var main = ps.main;
            main.duration = Lifetime; main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;   // 초기 속도는 EmitParams 결정론 벤치로 위임
            main.startSize = 1.4f;
            main.gravityModifier = -0.04f;   // 미세 부양(먼지 떠오름)
            main.maxParticles = 8;
            // 흙톤 — ShockwaveRingFX.TrailDustColor(0.78,0.72,0.62,0.30) 선례 정합(α 0.35 — 임팩트 가시성).
            main.startColor = new Color(0.78f, 0.72f, 0.62f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var rnd = go.GetComponent<ParticleSystemRenderer>();
            FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, "FxImpactDustColumn");
            rnd.renderMode = ParticleSystemRenderMode.Stretch;   // 세로 스트레치 빌보드 — 상승 기둥 형태
            rnd.lengthScale = 2.2f;
            rnd.velocityScale = 0.2f;
            // 결정론 벤치 — 속도 2.4~3.6m/s(해시 배분) × 0.5s = 위로 1.2~1.8m(Random 0건)
            // [무번호 76 수리] Unity 6000.4 배치에서 Emit(EmitParams) 오버로드 미해소(CS1503) →
            //   안정 5인자 오버로드 Emit(pos, vel, size, lifetime, color)로 교체 — 값 동일 이식.
            for (int i = 0; i < ParticleCount; i++)
            {
                var p0 = new Vector3((Hash(i, 1, Seed) - 0.5f) * 0.5f, 0.05f, (Hash(i, 2, Seed) - 0.5f) * 0.5f);
                var v0 = new Vector3(0f, 2.4f + 1.2f * Hash(i, 3, Seed), 0f);
                ps.Emit(p0, v0, 1.4f, Lifetime, new Color(0.78f, 0.72f, 0.62f, 0.35f));
            }
            _lastSpawnTime = Time.time;
            if (Application.isPlaying) { Object.Destroy(go, Lifetime + 0.1f); Object.Destroy(mat, Lifetime + 0.1f); }
            else { Object.DestroyImmediate(go); Object.DestroyImmediate(mat); }   // 21-K1 가드 선례
        }

        /// <summary>전용 절차 텍스처 64×256 세로 스트립 — 1회 생성 후 정적 캐시.
        /// 세로 value-noise 그라데이션(하단 진·상단 투명)+하단 방사 마스크(수평 가우시안 — 사각 금지).
        /// 기존 퍼프 텍스처 재사용 경로(ShockwaveRingFX private 잠금 — 74 확정) 무참조 — 전용 파이프라인.</summary>
        private static Texture2D EnsureColumnTexture()
        {
            if (_strip != null) return _strip;
            const int w = 64, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;   // 0=하단 .. 1=상단
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    // 하단 진·상단 투명 — smoothstep 세로 그라데이션(사각 금지)
                    float vert = Mathf.SmoothStep(1f, 0f, v);
                    // 하단 방사 마스크 — 수평 중심 가우시안(사각 금지)
                    float du = u - 0.5f;
                    float mask = Mathf.Exp(-(du * du) / (2f * 0.18f * 0.18f));
                    // 결정론 value noise(해시 기반 — Random 0건): 세로 방향 흐름 결
                    float n = Fbm(u * 3f + Seed * 0.0001f, v * 2.2f, Seed);
                    float a = Mathf.Clamp01(vert * mask * (0.55f + 0.75f * n));
                    px[y * w + x] = new Color32(214, 208, 196, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _strip = tex;
            return tex;
        }

        /// <summary>3옥타브 FBM value noise — 해시 기반 결정론(FxFlipbookGenerator 선례 동일식).</summary>
        private static float Fbm(float x, float y, int seed)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int o = 0; o < 3; o++)
            {
                sum += amp * ValueNoise(x * freq, y * freq, seed + o * 131);
                amp *= 0.5f; freq *= 2.07f;
            }
            return sum;
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float su = fx * fx * (3f - 2f * fx);
            float sv = fy * fy * (3f - 2f * fy);
            float n00 = Hash(ix, iy, seed), n10 = Hash(ix + 1, iy, seed);
            float n01 = Hash(ix, iy + 1, seed), n11 = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, su), Mathf.Lerp(n01, n11, su), sv);
        }

        /// <summary>정수 격자 해시 — 결정론(동일 입력=동일 출력, Random 0건).</summary>
        private static float Hash(int ix, int iy, int seed)
        {
            float s = Mathf.Sin(ix * 127.1f + iy * 311.7f + seed * 0.113f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
