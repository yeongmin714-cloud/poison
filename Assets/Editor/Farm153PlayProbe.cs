using System;
using System.IO;
using System.Reflection;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [153차 P5-PLAY] 농경 시각+획득 FX 헤드리스 렌더 프로브 — Play 승인 게이트 증거 수집용.
    /// 실행(후속 단계): Unity.exe -batchmode -projectPath "C:\Unity\code" -executeMethod ProjectName.EditorTools.Farm153PlayProbe.RunAll -quit
    /// 산출: Screenshots/farm153_1_tilled_hole.png / farm153_2_crop_glb.png / farm153_3_acq_fx.png / farm153_4_acq_fx_side.png
    /// 원칙: 시각 구성은 실제 프로덕션 코드 경로(FarmPlotVisuals/AcquisitionCinema)만 사용 — 복제 구현 금지,
    /// private는 리플렉션 경유. 캡처는 프로젝트 검증 패턴(RT+cam.Render+ReadPixels — GblItemIconRenderer 34차 선례).
    /// </summary>
    public static class Farm153PlayProbe
    {
        private const int RTW = 1024;
        private const int RTH = 768;

        public static void RunAll()
        {
            Debug.Log("[153차 P5-PLAY] === 프로브 시작 ===");

            // ── 공통 라이팅 — 탑다운 음영 판독용 기본 광원 ──
            var lightGo = new GameObject("Farm153ProbeLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;   // [153차 P5-PLAY 수리] 1차 캡처가 어두움 — 라이팅 강화 재촬영
            lightGo.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            RenderSettings.ambientLight = new Color(1.3f, 1.3f, 1.3f, 1f);   // 갈색 밭 판독 가능 밝기

            var camGo = new GameObject("Farm153ProbeCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            cam.cullingMask = ~0;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.fieldOfView = 40f;
            var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;

            // ── 컷 1: 굉이질 오목(Tilled) — 실제 FarmPlot 빌더 경로 ──
            try
            {
                var plotGo = new GameObject("Farm153PlotProbe");
                plotGo.transform.position = Vector3.zero;
                var plot = plotGo.AddComponent<FarmPlot>();
                InvokeInstance(plot, "BuildSoilVisual");
                InvokeInstance(plot, "BuildHoleVisual");
                InvokeInstance(plot, "BuildCropVisual");
                SetPhase(plot, FarmPlot.CropPhase.Tilled);
                InvokeInstance(plot, "RefreshVisuals");
                Debug.Log("[153차 P5-PLAY] 컷1 구성 완료 — Tilled 오목(정점 내림+AO 내벽)");
                Snap(cam, new Vector3(0f, 3.2f, 0.35f), Vector3.zero);
                Capture(cam, "farm153_1_tilled_hole.png");
            }
            catch (Exception e) { Debug.LogError($"[153차 P5-PLAY] 컷1 실패 — {e}"); }

            // ── 컷 2: 작물 GLB(Growing — herb_red.glb) ──
            try
            {
                var plot = UnityEngine.Object.FindObjectOfType<FarmPlot>();
                SetPhase(plot, FarmPlot.CropPhase.Growing);
                InvokeInstance(plot, "RefreshVisuals");
                Debug.Log("[153차 P5-PLAY] 컷2 구성 완료 — 작물 GLB 표시");
                Snap(cam, new Vector3(0.75f, 1.05f, 1.35f), new Vector3(0f, 0.3f, 0f));
                Capture(cam, "farm153_2_crop_glb.png");
            }
            catch (Exception e) { Debug.LogError($"[153차 P5-PLAY] 컷2 실패 — {e}"); }

            // ── 컷 3/4: 획득 FX(부유 GLB+빛기둥+헤일로+스파클) — AcquisitionCinema 실제 경로 ──
            try
            {
                var player = new GameObject("Farm153ProbePlayer");
                player.transform.position = new Vector3(0f, 0f, 6f);
                var data = new PlayerInventory.ItemData
                {
                    id = "herb_green",
                    displayName = "피어리",
                    description = "153차 Play 프로브 더미 데이터",
                    rarity = ItemRarity.Rare,
                };
                // [153차 P5-PLAY 수리] Publish 직접 호출은 오디오 징글(EnsurePool→DontDestroyOnLoad)을 유발해
                // 에디터 비플레이 컨텍스트에서 예외 — 부유 FX 검증에는 브리지 상태만 필요하므로
                // private setter 리플렉션으로 상태만 세팅(AttachFloatItem 실제 경로는 그대로 경유).
                SetBridgeState(HarvestResultBridge.HarvestKind.Gathering, "피어리", ItemRarity.Rare, 1, data);

                var m = typeof(AcquisitionCinema).GetMethod("AttachFloatItem",
                    BindingFlags.NonPublic | BindingFlags.Static);
                m.Invoke(null, new object[] { player });
                Debug.Log("[153차 P5-PLAY] 컷3 구성 완료 — 부유 GLB+AcqFloatFX(빛기둥/헤일로/스파클) 등급 Rare");

                // 정면 상반신 뷰 — 부유 앵커 1.85m가 프레임 중앙
                Snap(cam, new Vector3(0f, 1.6f, 4.3f), new Vector3(0f, 1.6f, 6f));
                Capture(cam, "farm153_3_acq_fx.png");

                Snap(cam, new Vector3(2.0f, 1.7f, 4.8f), new Vector3(0f, 1.7f, 6f));
                Capture(cam, "farm153_4_acq_fx_side.png");
            }
            catch (Exception e) { Debug.LogError($"[153차 P5-PLAY] 컷3/4 실패 — {e}"); }

            Debug.Log("[153차 P5-PLAY] DONE");
            rt.Release();
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
        }

        /// <summary>카메라 배치+주시.</summary>
        private static void Snap(Camera cam, Vector3 pos, Vector3 lookAt)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(lookAt);
        }

        /// <summary>프로젝트 검증 캡처 패턴(34차 선례) — RT 렌더+ReadPixels+PNG 저장.</summary>
        private static void Capture(Camera cam, string fileName)
        {
            cam.Render();
            var rt = cam.targetTexture;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var png = tex.EncodeToPNG();
            string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, png);
            Debug.Log($"[153차 P5-PLAY] 캡처 완료 — {fileName} ({png.Length / 1024}KB)");
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>private 인스턴스 메서드 리플렉션 호출(시각 위임 경유 — 실제 프로덕션 경로 유지).</summary>
        private static void InvokeInstance(object target, string methodName)
        {
            var m = target.GetType().GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) throw new MissingMethodException(target.GetType().Name, methodName);
            m.Invoke(target, null);
        }

        /// <summary>FarmPlot._phase private 필드 세팅.</summary>
        private static void SetPhase(FarmPlot plot, FarmPlot.CropPhase phase)
        {
            var f = typeof(FarmPlot).GetField("_phase", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) throw new MissingFieldException(nameof(FarmPlot), "_phase");
            f.SetValue(plot, phase);
        }

        /// <summary>[153차 P5-PLAY 수리] HarvestResultBridge private setter 리플렉션 세팅 — 오디오 징글 경로 우회(에디터 컨텍스트 안전).</summary>
        private static void SetBridgeState(HarvestResultBridge.HarvestKind kind, string itemName,
            ItemRarity rarity, int count, PlayerInventory.ItemData data)
        {
            var t = typeof(HarvestResultBridge);
            t.GetProperty("LatestKind").SetValue(null, kind, null);
            t.GetProperty("LatestItemName").SetValue(null, itemName, null);
            t.GetProperty("LatestRarity").SetValue(null, rarity, null);
            t.GetProperty("LatestCount").SetValue(null, count, null);
            t.GetProperty("LatestItemData").SetValue(null, data, null);
        }
    }
}
