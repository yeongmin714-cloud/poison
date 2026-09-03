using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P-2: 밝은 판타지 자연 분위기 전환 (런타임).
    /// Start()에서 안개 색/밀도, Trilight 앰비언트, Directional Light 강도를 밝게 설정한다.
    /// GameSetup.Start() → BootstrapTerrainDeco() 이후 AddComponent로 부착됨.
    /// </summary>
    public class AmbianceBrightener : MonoBehaviour
    {
        private void Start()
        {
            // ── 안개: fog 플래그는 유지, 색/밀도만 밝고 옅게 ──────────────
            RenderSettings.fogColor = new Color(0.85f, 0.88f, 0.95f);
            RenderSettings.fogDensity = 0.00025f;

            // ── 앰비언트: Trilight (하늘/적도/지면 3단 보간) ──────────────
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.92f, 0.95f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.7f, 0.8f, 0.9f);
            RenderSettings.ambientGroundColor = new Color(0.5f, 0.55f, 0.6f);

            // ── Directional Light 강도 조정 ──────────────────────────────
            // "Directional Light" 태그 우선 (미등록 태그 예외 대비 try-catch),
            // 실패 시 이름 "Directional Light (Sun)" 폴백.
            // 이름에 "Moon" 포함 시 0.05로 낮추고, 그 외(Sun)는 1.2로 밝힘.
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
                        light.intensity = 1.2f;
                        sunCount++;
                    }
                }
            }

            Debug.Log($"[AmbianceBrightener] ✅ 밝은 판타지 분위기 적용: fog=밝은 하늘색(0.00025), Trilight 앰비언트, Sun {sunCount}개→1.2 / Moon {moonCount}개→0.05");
        }
    }
}
