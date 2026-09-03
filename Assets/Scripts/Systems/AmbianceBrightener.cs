using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// P-2: 밝은 판타지 자연 분위기 전환 (런타임).
    /// GameSetup.Start() → BootstrapTerrainDeco() 이후 AddComponent로 부착됨.
    ///
    /// [P-2 QA 충돌 해결] DayNightCycle이 활성인 씬에서는 이 컴포넌트의 1회성
    /// RenderSettings 설정이 DNC.Update()의 매 프레임 Lerp로 무효화된다.
    /// → DNC를 감지하면 RenderSettings를 직접 쓰지 않고, DNC의 낮(day) 팔레트를
    ///   밝은 값으로 교체(ApplyBrightDayPalette)한다. 그러면 DNC이 밝은 낮 기준으로
    ///   매 프레임 Lerp하므로 밝기가 유지되고, 야간 어둡기 사이클도 그대로 유지된다.
    /// → DNC가 없거나 비활성이면 기존대로 Start에서 1회 직접 적용한다.
    /// 2026-09-03: 과노출 화이트아웃 수정(스크린샷 39) — 앰비언트/Sun 하향
    /// </summary>
    public class AmbianceBrightener : MonoBehaviour
    {
        // DNC 연동 시 DNC의 day 팔레트에 적용할 밝은 값 (정적 경로와 동일 톤).
        private static readonly Color BrightDayAmbient = new Color(0.44f, 0.50f, 0.60f);
        private static readonly Color BrightFogColor = new Color(0.66f, 0.72f, 0.82f);
        private const float BrightFogDensity = 0.00025f;
        private const float BrightNoonIntensity = 0.8f;

        // [T-R6] 방위(국가) 기반 분위기 틴트 — 색상만 미세 변주, 밝기 값(Sun 0.8 / sky 0.52) 불변.
        // 남=따뜻한 붉은기, 북=차가운 청기. 각 채널 ±4% 이하라 클리핑을 만들지 않음.
        private Color _nationTint = Color.white;

        // [T-R6] 국가별 미세 색상 배율 (≈1.0, 휘도 보존 → 클리핑 안전)
        private static readonly Color TintEast   = new Color(1.00f, 1.00f, 0.98f); // 떠오르는 태양·밝음
        private static readonly Color TintWest   = new Color(1.02f, 0.99f, 0.93f); // 따뜻한 오후·금빛
        private static readonly Color TintSouth  = new Color(1.03f, 0.97f, 0.92f); // 불꽃·따뜻한 붉은기
        private static readonly Color TintNorth  = new Color(0.94f, 0.98f, 1.04f); // 눈·차가운 청기
        private static readonly Color TintEmpire = new Color(1.00f, 1.00f, 1.00f); // 대리석·중립

        private void Start()
        {
            // [T-R6] 참조 위치(Player→카메라→this)의 국가로 미세 틴트 확정 (정적 경로/디엔시 경로 공용)
            _nationTint = GetNationTintAt(GetReferencePosition());

            // ── 우선 경로: DayNightCycle 활성 시 day 팔레트 오버라이드 ──
            // 같은 네임스페이스(ProjectName.Systems)이므로 직접 참조 (리플렉션 불필요).
            var dnc = FindAnyObjectByType<DayNightCycle>();
            if (dnc != null && dnc.isActiveAndEnabled)
            {
                dnc.ApplyBrightDayPalette(Multiply(BrightDayAmbient, _nationTint), BrightFogColor, BrightFogDensity, BrightNoonIntensity);
                Debug.Log("[AmbianceBrightener] ✅ DayNightCycle 활성 감지 — 낮 팔레트를 밝은 값으로 오버라이드 " +
                          $"(ambient={BrightDayAmbient}, fog={BrightFogColor}, density={BrightFogDensity}, noonI={BrightNoonIntensity}). " +
                          $"[T-R6] 국가 틴트={_nationTint} 적용(색상만, 밝기 불변). " +
                          "RenderSettings 직접 쓰기는 DNC에 위임 (매 프레임 덮어쓰기 충돌 제거).");
                return;
            }

            // ── DNC 부재/비활성: 기존 1회 정적 적용 ──────────────────────
            ApplyStaticBrightAmbiance();
        }

        /// <summary>DayNightCycle이 없을 때의 1회성 밝은 분위기 적용 (Trilight 앰비언트).</summary>
        private void ApplyStaticBrightAmbiance()
        {
            // ── 안개: fog 플래그는 유지, 색/밀도만 밝고 옅게 ──────────────
            RenderSettings.fogColor = BrightFogColor;
            RenderSettings.fogDensity = BrightFogDensity;

            // ── 앰비언트: Trilight (하늘/적도/지면 3단 보간) ──────────────
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            // [T-R6] 밝기(sky 값)는 계획 원칙대로 유지하되, 국가 틴트만 색상에 곱함(±4% 이하).
            RenderSettings.ambientSkyColor = Multiply(_nationTint, new Color(0.52f, 0.58f, 0.68f));
            RenderSettings.ambientEquatorColor = Multiply(_nationTint, new Color(0.42f, 0.47f, 0.55f));
            RenderSettings.ambientGroundColor = Multiply(_nationTint, new Color(0.30f, 0.34f, 0.40f));

            // ── Directional Light 강도 조정 ──────────────────────────────
            // "Directional Light" 태그 우선 (미등록 태그 예외 대비 try-catch),
            // 실패 시 이름 "Directional Light (Sun)" 폴백.
            // 이름에 "Moon" 포함 시 0.05로 낮추고, 그 외(Sun)는 BrightNoonIntensity로 밝힘.
            GameObject[] dirLights = null;
            try
            {
                dirLights = GameObject.FindGameObjectsWithTag("Directional Light");
            }
            catch (UnityException)
            {
                // 태그 미등록 — 폴백으로 진행
            }

            if (dirLights == null || dirLights.Length == 0)
            {
                var sun = GameObject.Find("Directional Light (Sun)");
                if (sun != null) dirLights = new GameObject[] { sun };
            }

            int sunCount = 0, moonCount = 0;
            if (dirLights != null)
            {
                foreach (var go in dirLights)
                {
                    if (go == null) continue;
                    var light = go.GetComponent<Light>();
                    if (light == null || light.type != LightType.Directional) continue;

                    if (go.name.Contains("Moon"))
                    {
                        light.intensity = 0.05f;
                        moonCount++;
                    }
                    else
                    {
                        light.intensity = BrightNoonIntensity;
                        // [T-R6] 태양 색상은 미세 국가 틴트만 곱함 (강도 0.8/밝기 불변 → 클리핑 안전)
                        light.color = Multiply(_nationTint, light.color);
                        sunCount++;
                    }
                }
            }

            Debug.Log($"[AmbianceBrightener] ✅ 밝은 판타지 분위기 적용(정적): fog=밝은 하늘색({BrightFogDensity}), Trilight 앰비언트, Sun {sunCount}개→{BrightNoonIntensity} / Moon {moonCount}개→0.05. [T-R6] 국가 틴트={_nationTint} 적용(색상만).");
        }

        // ================================================================
        // [T-R6 helpers] 방위 기반 분위기 틴트
        // ================================================================

        /// <summary>참조 위치(Player → 카메라 → this)를 결정한다.</summary>
        private Vector3 GetReferencePosition()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) return player.transform.position;
            var cam = Camera.main;
            if (cam != null) return cam.transform.position;
            return transform.position;
        }

        /// <summary>위치의 국가를 판정해 미세 색상 배율(≈1.0)을 돌려준다.</summary>
        private static Color GetNationTintAt(Vector3 pos)
        {
            try
            {
                switch (NationTerrainController.GetNationFromPosition(pos))
                {
                    case NationType.East:   return TintEast;
                    case NationType.West:   return TintWest;
                    case NationType.South:  return TintSouth;
                    case NationType.North:  return TintNorth;
                    default:                return TintEmpire; // Empire/기타 = 중립
                }
            }
            catch (System.Exception)
            {
                return Color.white; // 국가 판정 실패 시 무틴트
            }
        }

        /// <summary>색상 컴포넌트별 곱 (멀티플라이). 배율≈1.0이므로 밝기 보존.</summary>
        private static Color Multiply(Color a, Color b)
        {
            return new Color(
                a.r * b.r,
                a.g * b.g,
                a.b * b.b,
                a.a * b.a);
        }

        // ================================================================
        // [T-R6] 클리핑 안전성 계산 (과거 화이트아웃 39/40 사고 재발 방지):
        //   지면 선형 휘도 상한 ≈ 앰비언트sky(≤0.52) + Sun(0.8 × lambert 0.7 × albedo 0.9)
        //                     = 0.52 + 0.65 = ~1.17 linear (밝은 태향 지면)
        //   → ACES 톤매핑이 <1.0으로 압축 → 표면 클리핑 없음.
        //   국가 틴트 배율은 채널당 ±4% 이하(최대 Sun 0.8×1.03, sky 0.52×1.04)라
        //   위 상한이 1.17→~1.19(≈+2%)로만 증가 → 여전히 ACES에서 서-클립, 클리핑 <10% 유지.
        //   (skybox 노출 0.5→0.75 상향은 skybox 배경 렌더에만 영향 — 지면/표면 휘도 불변)
        // ================================================================
    }
}
