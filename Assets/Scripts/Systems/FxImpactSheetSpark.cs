using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 76-S1] 임팩트 시트애니 스파크 — 크리/강타격 지점 확산 폭발 보완 레이어(라인 3본 39-T2 대체 아님).
    /// 계약: 절차 시트 텍스처 256×512(4열×2행 8프레임) 결정론 해시(Random 0건 — FxFlipbookGenerator PuffHash 선례),
    /// 사각 금지(smoothstep 방사 마스크), 캐시 1회, 색 = FXPalette.AttackPrimary 단일 소스(공격 이펙트 통일색 — HDR 0 — LDR 고정 66 계약),
    /// 동시 스폰 상한 6 + Time.time 쿨다운 0.12s(정적 필드 — 결정론, 초과 시 조용히 스킵),
    /// 머티리얼 인스턴스 유한 파괴(FxSlashFlipbookFlash 21-K1 수리 선례).
    /// 링 예산(X2) 미소비 — 링 계열 예산 문자열 0건.
    /// </summary>
    public static class FxImpactSheetSpark
    {
        private const float Lifetime = 0.28f;        // 시트애니 1패스 지속(계획 계약)
        private const int MaxConcurrentSparks = 6;   // 동시 스폰 상한(소형 예산 — 초과 시 조용히 스킵)
        private const float CooldownSec = 0.12f;     // 연발 쿨다운(Time.time 정적 필드 — 결정론)
        private const int Seed = 20261118;           // 결정론 해시 시드 — 동일 입력=동일 시트

        private static Texture2D _sheet;
        private static float _lastBurstTime = -999f;
        // 활성 카운터 — 수명 창 프루닝 방식(파괴 경유 역감산 없음, 누출 불가 — 결정론)
        private static readonly List<float> _recentBurstTimes = new List<float>();

        /// <summary>크리/강타격 지점 시트애니 스파크 발화 — CombatFXGate 강타격 게이트 밴드(isCrit || damage >= 15f)에서 호출.</summary>
        public static void Burst(Vector3 position, bool isCrit)
        {
            if (!Application.isPlaying) return;   // EditMode 가드 — 스폰 0건(소스 계약만)
            if (Time.time - _lastBurstTime < CooldownSec) return;   // 연발 쿨다운(정적 필드 — 결정론)
            PruneBudget();
            if (_recentBurstTimes.Count >= MaxConcurrentSparks) return;   // 상한 6 초과 — 조용히 스킵

            var mat = FxMaterialLibrary.CreateSoftParticle("ImpactSheetSparkMat", new Color(1f, 1f, 1f, 1f), EnsureSheetTexture());
            if (mat == null) return;

            var go = new GameObject("ImpactSheetSpark");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // [무번호 48-T1] 자동 재생 정지 선례
            var main = ps.main;
            main.duration = Lifetime; main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.startSize = 1.2f * (isCrit ? 1.5f : 1f);   // 크리 1.5배 위계(70-U3 준용)
            // [무번호 76-S1 갱신 79-R20] 색 단일 소스 — FXPalette.AttackPrimary(공격 이펙트 통일색 — HDR 0 — LDR 고정 66 계약). 알파 1 정규화(이중 감쇠 방지).
            var uni = FXPalette.AttackPrimary;
            main.startColor = new Color(uni.r, uni.g, uni.b, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var sheet = ps.textureSheetAnimation; sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 4;   // 4열×2행 8프레임(시트 절차 생성과 정합)
            sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 1f);   // 수명 전체 8프레임 순환(1패스 — FxSlashFlipbookFlash 선례)
            var rnd = go.GetComponent<ParticleSystemRenderer>();
            // [무번호 69-P2] 시트애니 활성 파티클 AnimFrame 스트림 정합(미바인딩 = 가비지 타일 샘플링 방지).
            FxMaterialLibrary.AlignParticleVertexStreams(rnd);
            // [무번호 47-b] 가드 경로 — 실패 시 GO는 유한 수명 파괴로 정리(유령 GO 금지).
            FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, "FxImpactSheetSpark");
            rnd.renderMode = ParticleSystemRenderMode.Billboard;
            rnd.alignment = ParticleSystemRenderSpace.View;
            rnd.sortMode = ParticleSystemSortMode.Distance;
            ps.Emit(1);   // 단발 파티클 1개 — 시트애니가 확산 연출 담당
            _lastBurstTime = Time.time;
            _recentBurstTimes.Add(Time.time);
            if (Application.isPlaying) { Object.Destroy(go, Lifetime + 0.1f); Object.Destroy(mat, Lifetime + 0.1f); }
            else { Object.DestroyImmediate(go); Object.DestroyImmediate(mat); }   // 21-K1 가드 선례
        }

        private static void PruneBudget()
        {
            float now = Time.time;
            _recentBurstTimes.RemoveAll(t => now - t >= Lifetime + 0.1f);
        }

        /// <summary>절차 시트 텍스처 256×512(4열×2행 8프레임) — 1회 생성 후 정적 캐시(재생성 금지).
        /// 프레임별 방사 스파크 확산(진행 k=frame/7): 0=중심 점화 소체 → 1~3=방사 링대 확대 →
        /// 4~6=8방향 사선 파편(결정론 해시 각도) → 7=소멸 잔광. 사각 금지 — smoothstep 방사 마스크만 사용.
        /// 타일 64×256(비대칭) — 빌보드 타일 종횡비 유지 정합 보정(스크린 원형 = 보정 좌표계 원).</summary>
        private static Texture2D EnsureSheetTexture()
        {
            if (_sheet != null) return _sheet;
            var tex = new Texture2D(256, 512, TextureFormat.RGBA32, false);   // 4열×2행 8프레임(pow2 — 파티클 용도 mipChain=false)
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            const int cw = 64, ch = 256;   // 셀 폭×높이(256/4 × 512/2)
            var px = new Color32[256 * 512];
            for (int f = 0; f < 8; f++)
            {
                int x0 = (f % 4) * cw;
                int y0 = (1 - f / 4) * ch;   // top-down 행 순서(Unity 시트애니 row-major 정합 — 29-D1 선례)
                float k = f / 7f;            // 프레임별 진행 0→1
                for (int y = 0; y < ch; y++)
                {
                    for (int x = 0; x < cw; x++)
                    {
                        float uu = (x + 0.5f) / cw - 0.5f;
                        float vv = (y + 0.5f) / ch - 0.5f;
                        float v4 = vv * 4f;                       // 타일 종횡비 보정 — 스크린 원형 좌표계
                        float rc = Mathf.Sqrt(uu * uu + v4 * v4); // 보정 방사 반경(0.5 = 셀 폭 절반)
                        float r01 = Mathf.Clamp01(rc / 0.5f);
                        float angDeg = Mathf.Atan2(v4, uu) * Mathf.Rad2Deg;
                        float a;
                        if (f == 0)
                        {
                            // 중심 점화 소체 — smoothstep 방사(사각 금지)
                            a = Mathf.SmoothStep(1f, 0f, r01);
                        }
                        else if (f <= 3)
                        {
                            // 방사 링대 확대 — 반경 진행(k 비례) + 밴드 완화
                            float ringR = Mathf.Lerp(0.10f, 0.44f, k);
                            float width = Mathf.Lerp(0.07f, 0.13f, k);
                            float band = 1f - Mathf.Abs(rc - ringR) / width;
                            a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(band)) * (1f - 0.25f * k);
                        }
                        else if (f <= 6)
                        {
                            // 8방향 사선 파편 — 결정론 해시 각도(Random 0건, 프레임별 재편)
                            a = 0f;
                            for (int d = 0; d < 8; d++)
                            {
                                float shardAng = d * 45f + (Hash(d, f, Seed) - 0.5f) * 20f;
                                float dAng = Mathf.Abs(Mathf.DeltaAngle(angDeg, shardAng));
                                float angFall = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(dAng / 9f));
                                float rIn = Mathf.Lerp(0.06f, 0.12f, k);
                                float rOut = Mathf.Lerp(0.24f, 0.5f, k);
                                float radial = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rOut - rc)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rc - rIn));
                                a = Mathf.Max(a, angFall * radial);
                            }
                            a *= 1f - 0.15f * (f - 4);
                        }
                        else
                        {
                            // 소멸 잔광 — 확산 후 광대 잔류(0.28 고정)
                            a = Mathf.SmoothStep(0.28f, 0f, r01);
                        }
                        px[(y0 + y) * 256 + (x0 + x)] = new Color32(255, 250, 236, (byte)(Mathf.Clamp01(a) * 255f));
                    }
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _sheet = tex;
            return tex;
        }

        /// <summary>정수 격자 해시 — 결정론(동일 입력=동일 출력, Random 0건 — FxFlipbookGenerator 선례 동일식).</summary>
        private static float Hash(int ix, int iy, int seed)
        {
            float s = Mathf.Sin(ix * 127.1f + iy * 311.7f + seed * 0.113f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
