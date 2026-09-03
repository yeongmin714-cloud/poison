using UnityEngine;

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
        private static readonly Color BrightDayAmbient = new Color(0.52f, 0.58f, 0.68f);
        private static readonly Color BrightFogColor = new Color(0.72f, 0.78f, 0.88f);
        private const float BrightFogDensity = 0.00025f;
        private const float BrightNoonIntensity = 0.95f;

        private void Start()
        {
            // ── 우선 경로: DayNightCycle 활성 시 day 팔레트 오버라이드 ──
            // 같은 네임스페이스(ProjectName.Systems)이므로 직접 참조 (리플렉션 불필요).
            var dnc = FindAnyObjectByType<DayNightCycle>();
            if (dnc != null && dnc.isActiveAndEnabled)
            {
                dnc.ApplyBrightDayPalette(BrightDayAmbient, BrightFogColor, BrightFogDensity, BrightNoonIntensity);
                Debug.Log("[AmbianceBrightener] ✅ DayNightCycle 활성 감지 — 낮 팔레트를 밝은 값으로 오버라이드 " +
                          $"(ambient={BrightDayAmbient}, fog={BrightFogColor}, density={BrightFogDensity}, noonI={BrightNoonIntensity}). " +
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
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.56f, 0.65f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.42f, 0.48f);

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
                        sunCount++;
                    }
                }
            }

            Debug.Log($"[AmbianceBrightener] ✅ 밝은 판타지 분위기 적용(정적): fog=밝은 하늘색({BrightFogDensity}), Trilight 앰비언트, Sun {sunCount}개→{BrightNoonIntensity} / Moon {moonCount}개→0.05");
        }
    }
}
