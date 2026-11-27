// 2026-10-25 (125차 후속25): 투기장 미가시 뿌리 확정용 결정 감사 프로브 — 1회성(label별 1회).
// RT 캡처(화면 공간 UI가 배제된 "순수 카메라 출력" — 오버레이 가설 vs 씬 렌더 가설을 물리적으로 분리)
// + 씬 루트 전수(살아남은 Ground/잔존 룸 적발) + 라이트 전수(달빛/횃불 실측)
// + 스카이박스/Canvas 목록 + 포그 강제원 컴포넌트 존재 덤프.
// 감사 전용 — 렌더/게임플레이 무영향(캡처 후 targetTexture 복원).
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.UI
{
    public static class RenderAuditProbe
    {
        private static readonly System.Collections.Generic.HashSet<string> _done = new System.Collections.Generic.HashSet<string>();

        /// <summary>[125차 후속25] 라벨별 1회성 전면 감사 — RT 캡처 + 씬/라이트/카메라/포그강제원 덤프.</summary>
        public static void Run(string label)
        {
            if (!_done.Add(label)) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[RenderAuditProbe] [{label}] [125차 후속25] ─────────────");

                // A. RenderSettings — ambient/fog 실측
                var amb = RenderSettings.ambientLight;
                sb.AppendLine($"  RS: ambientMode={RenderSettings.ambientMode} ambient=({amb.r:F2},{amb.g:F2},{amb.b:F2}) fog={RenderSettings.fog} fogColor=({RenderSettings.fogColor.r:F2},{RenderSettings.fogColor.g:F2},{RenderSettings.fogColor.b:F2}) density={RenderSettings.fogDensity:F4}");

                // B. 씬 루트 전수 — Test_18_Arena Ground 공존/잔존 룸 적발(원점 충돌 가설)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (!s.isLoaded) continue;
                    sb.AppendLine($"  SCENE '{s.name}' loaded roots={s.rootCount}");
                    foreach (var root in s.GetRootGameObjects())
                        sb.AppendLine($"    ROOT '{root.name}' active={root.activeInHierarchy} pos={root.transform.position} children={root.transform.childCount}");
                }

                // C. 라이트 전수 — 달빛 방향광 존재·강도 실측(기존 감사의 구조적 공백)
                var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int dirActive = 0;
                foreach (var l in lights)
                    if (l.type == LightType.Directional && l.enabled && l.gameObject.activeInHierarchy) dirActive++;
                sb.AppendLine($"  LIGHTS total={lights.Length} (활성 방향광 {dirActive})");
                foreach (var l in lights)
                {
                    string range = l.type == LightType.Directional ? "-" : l.range.ToString("F1");
                    sb.AppendLine($"    LIGHT '{l.name}' type={l.type} act={l.gameObject.activeInHierarchy} en={l.enabled} I={l.intensity:F2} R={range} c=({l.color.r:F2},{l.color.g:F2},{l.color.b:F2}) p={l.transform.position}");
                }

                // D. 카메라 near/far + RT 캡처(첫 활성 카메라)
                var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Camera captureCam = null;
                foreach (var c in cams)
                {
                    if (c == null) continue;
                    bool act = c.gameObject.activeInHierarchy && c.enabled;
                    var eul = c.transform.rotation.eulerAngles;
                    sb.AppendLine($"    CAM '{c.name}' act={act} near={c.nearClipPlane:F2} far={c.farClipPlane:F0} scene={c.gameObject.scene.name} ortho={c.orthographic}/{c.orthographicSize:0.0} depth={c.depth} euler=({eul.x:F0},{eul.y:F0},{eul.z:F0}) pos={c.transform.position}");
                    if (act && captureCam == null) captureCam = c;
                }
                if (captureCam != null) CaptureRenderTarget(captureCam, label);
                else sb.AppendLine("  RT 캡처 스킵 — 활성 카메라 없음");

                // E. 스카이박스 + 활성 Canvas 목록(화면 덮개 가설 보조)
                sb.AppendLine($"  SKYBOX = {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL")}");
                var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                sb.AppendLine($"  CANVAS 활성 {canvases.Length}개");
                foreach (var cv in canvases)
                    sb.AppendLine($"    CANVAS '{cv.name}' order={cv.sortingOrder} en={cv.enabled}");

                // F. 포그 강제원 존재 덤프 — fog=true 강제 소스(BiomeEffect/DNC/Dracula/SkyboxSetup 등)의 생존 판정
                string[] forcerTypes =
                {
                    "ProjectName.Systems.BiomeEffectController",
                    "ProjectName.Systems.DayNightCycle",
                    "ProjectName.Systems.TestDraculaSetup",
                    "ProjectName.Systems.PhaseG2_SkyboxSetup",
                    "ProjectName.Systems.TestTimeWeatherSetup",
                    "ProjectName.Systems.WeatherManager",
                };
                foreach (var tn in forcerTypes)
                {
                    var t = System.Type.GetType(tn + ",Assembly-CSharp");
                    if (t == null) continue;
                    var objs = Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None);
                    if (objs.Length > 0)
                    {
                        var comp = objs[0] as Component;
                        if (comp != null)
                            sb.AppendLine($"  FOG-FORCER '{tn}' x{objs.Length} active={comp.gameObject.activeInHierarchy} scene={comp.gameObject.scene.name}");
                    }
                }

                var overlay = Object.FindAnyObjectByType<ProjectName.UI.Toolkit.NameplateOverlayUTK>();
                sb.AppendLine($"  NAMEPLATE_OVERLAY = {(overlay != null ? "존재" : "부재!")}");
                // [125차 후속28 F1/F3/F6] 결투선수 실측 — 존재/위치/월드 높이(bounds)/HP + 스폰 타이밍 판별
                var duelists = Object.FindObjectsByType<ProjectName.Systems.ArenaDuelFighterTag>(FindObjectsSortMode.None);
                sb.AppendLine($"  DUELIST {duelists.Length}기");
                foreach (var f in duelists)
                {
                    var rends = f.GetComponentsInChildren<Renderer>();
                    Bounds db = default; bool dhas = false;
                    foreach (var r in rends) { if (!dhas) { db = r.bounds; dhas = true; } else db.Encapsulate(r.bounds); }
                    sb.AppendLine($"    DUELIST '{f.DisplayName}' pos={f.transform.position} worldHeight={(dhas ? db.size.y.ToString("F2") : "-")} alive={f.IsAlive} hp={f.CurrentHP:F0}/{f.MaxHP:F0}");
                }
                Debug.Log(sb.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RenderAuditProbe] [{label}] 실패: {e.Message}");
            }
        }

        /// <summary>[125차 후속25 R3-2] 지연 감사 — 빌드 직후가 아닌 N초 후 상태(회색 화면 시점)를 캡처.</summary>
        public static void RunDelayed(string label, float delay)
        {
            var go = new GameObject("RenderAuditProbeTimer_" + label);
            var timer = go.AddComponent<RenderAuditProbeTimer>();
            timer.Schedule(label, delay);
        }

        /// <summary>텍스처 실측 요약 — Blit 기반 32×32 샘플 평균 밝기(비리드블 텍스처도 측정 가능).</summary>
        public static string DescribeTexture(Texture t)
        {
            if (t == null) return "NULL";
            try
            {
                var rt = RenderTexture.GetTemporary(32, 32, 0);
                var prev = RenderTexture.active;
                Graphics.Blit(t, rt);
                RenderTexture.active = rt;
                var tmp = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
                tmp.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                var px = tmp.GetPixels();
                float sum = 0f;
                foreach (var p in px) sum += (p.r + p.g + p.b) / 3f;
                return $"{t.name} {t.width}x{t.height} avg={sum / px.Length:F3}";
            }
            catch (System.Exception e)
            {
                return $"{t.name} {t.width}x{t.height} 샘플실패({e.Message})";
            }
        }

        /// <summary>[125차 후속25] 활성 카메라 → RT 960×540 → PNG 저장 + 픽셀 샘플 로그(파일 없이도 1차 판독).</summary>
        private static void CaptureRenderTarget(Camera cam, string label)
        {
            try
            {
                const int w = 960, h = 540;
                var rt = RenderTexture.GetTemporary(w, h, 24);
                var prevTarget = cam.targetTexture;
                var prevActive = RenderTexture.active;
                // [125차 후속28 F5] 캡처 직전 관전 프레이밍 강제 — Update 이동자의 중간 상태 오염 제거
                //   (이전 세션 RT 검정 아티팩트의 원인: 이동자가 (0,0,0)을 쓴 직후 캡처됨 — 캡처=사용자 뷰 계약)
                if (label == "pit" || label == "pit_late")
                {
                    // [125차 후속31 M1] 캡처=사용자 뷰 계약 — 사선 관전 프레이밍 동기(단일 소스 공유, 네임스페이스 한정)
                    cam.transform.position = ProjectName.Systems.ArenaSpectatorCameraLock.SpectatorPos;
                    cam.transform.rotation = Quaternion.Euler(ProjectName.Systems.ArenaSpectatorCameraLock.SpectatorPitchDeg, 0f, 0f);
                }
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;

                string path = label == "pit"
                    ? "TestOutput/pit_audit_rt.png"
                    : $"TestOutput/{label}_audit_rt.png";
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

                // 즉시 판독용 픽셀 샘플 — 중앙 + 4분면 평균 밝기
                var cx = tex.GetPixel(w / 2, h / 2);
                float quad = 0f;
                var qpts = new[]
                {
                    new Vector2Int(w / 4, h / 2), new Vector2Int(3 * w / 4, h / 2),
                    new Vector2Int(w / 2, h / 4), new Vector2Int(w / 2, 3 * h / 4),
                };
                foreach (var p in qpts)
                {
                    var pc = tex.GetPixel(p.x, p.y);
                    quad += (pc.r + pc.g + pc.b) / 3f;
                }
                quad *= 0.25f;
                Debug.Log($"[RenderAuditProbe] [{label}] RT 캡처 — {path} / 중앙 RGB=({cx.r:F2},{cx.g:F2},{cx.b:F2}) 4분면 평균밝기={quad:F3}");

                RenderTexture.ReleaseTemporary(rt);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RenderAuditProbe] [{label}] RT 캡처 실패: {e.Message}");
            }
        }
    }

    /// <summary>[125차 후속25 R3-2] 지연 감사 타이머 — 정적 클래스에서 코루틴 지연을 대행하는 최상위 MonoBehaviour
    ///   (Unity는 중첩 MonoBehaviour AddComponent 불가 계약).</summary>
    internal class RenderAuditProbeTimer : MonoBehaviour
    {
        private string _label;
        private float _delay;

        public void Schedule(string label, float delay)
        {
            _label = label;
            _delay = delay;
            DontDestroyOnLoad(gameObject);   // 씬 전환(투기장 리빌드) 중에도 타이머 생존
        }

        private System.Collections.IEnumerator Start()
        {
            yield return new WaitForSeconds(_delay);
            RenderAuditProbe.Run(_label);
            Destroy(gameObject);
        }
    }
}
