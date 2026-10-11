#if UNITY_EDITOR
using System.Text;
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [21차 게이지 재판정] 배치 Play 캡처 드라이버 — SessionState 플래그가 있을 때만 활동.
    /// UI Toolkit 패널 포함 게임뷰 PNG + UIRoot 자식 순서/게이지 worldBound 덤프.
    /// 프로덕션 무영향: SessionState["pcap_active"] 미설정 시 즉시 무시(에디터 배치 캡처 전용).
    /// </summary>
    public class PlayCaptureDriver : MonoBehaviour
    {
        private int _frame;
        private bool _shot;
        private const string OutPath = "C:/tmp/r21_play.png";
        private const int CaptureFrame = 120;
        private const int QuitFrame = 126;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!SessionState.GetBool("pcap_active", false)) return;
            var go = new GameObject("PlayCaptureDriver");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayCaptureDriver>();
            Debug.Log("[PlayCapture] 드라이버 부착");
        }

        private void Update()
        {
            _frame++;
            if (_frame == CaptureFrame && !_shot)
            {
                _shot = true;
                DumpUI();
                // ReadPixels 방식 — ScreenCapture.CaptureScreenshot은 배치모드에서 파일이 생기지 않는 실측
                StartCoroutine(CaptureNextFrame());
            }

            if (_frame >= QuitFrame)
            {
                SessionState.EraseBool("pcap_active");
                EditorApplication.isPlaying = false;
                EditorApplication.Exit(0);
            }
        }

        private System.Collections.IEnumerator CaptureNextFrame()
        {
            // 프레임 끝(백버퍼 완성 후)에 전체 화면을 읽어 PNG로 저장
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(OutPath, tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log($"[PlayCapture] PNG 저장 {OutPath} {Screen.width}x{Screen.height}");
        }

        private static void DumpUI()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) { Debug.LogError("[PlayCapture] UIRoot 없음"); return; }
            Debug.Log($"[PlayCapture] screen={Screen.width}x{Screen.height} panelRoot={root.resolvedStyle.width:0}x{root.resolvedStyle.height:0}");

            var sb = new StringBuilder("[PlayCapture] UIRoot children (index=페인트 순서, 뒤가 위):\n");
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root[i];
                sb.AppendFormat("  [{0}] {1} wb={2} vis={3} disp={4} op={5:0.00} bgA={6:0.00}\n",
                    i, c.name, RectStr(c.worldBound), c.resolvedStyle.visibility, c.resolvedStyle.display,
                    c.resolvedStyle.opacity, c.resolvedStyle.backgroundColor.a);
            }
            Debug.Log(sb.ToString());

            var hud = root.Q<VisualElement>("HotbarGauges");
            if (hud == null) { Debug.LogError("[PlayCapture] HotbarGauges 호스트 없음!"); return; }
            Debug.Log($"[PlayCapture] gaugeHost worldBound={RectStr(hud.worldBound)} 부모인덱스={hud.parent.IndexOf(hud)}/{hud.parent.childCount}");

            var hpArc = hud.Q<VisualElement>("HpGaugeArc");
            if (hpArc != null)
                Debug.Log($"[PlayCapture] HpGaugeArc worldBound={RectStr(hpArc.worldBound)} vis={hpArc.resolvedStyle.visibility} disp={hpArc.resolvedStyle.display}");

            // 레이어별 서브트리 덤프 — hpRoot(게이지 루트) 직계 전부
            var hpRoot = hud.Q<VisualElement>("HpGauge");
            if (hpRoot != null)
            {
                var sb2 = new StringBuilder("[PlayCapture] HpGauge layers:\n");
                sb2.AppendFormat("  root wb={0} bgA={1:0.00}\n", RectStr(hpRoot.worldBound), hpRoot.resolvedStyle.backgroundColor.a);
                for (int i = 0; i < hpRoot.childCount; i++)
                {
                    var c = hpRoot[i];
                    sb2.AppendFormat("  [{0}] {1} wb={2} vis={3} disp={4} bgA={5:0.00} bgImg={6}\n",
                        i, c.name, RectStr(c.worldBound), c.resolvedStyle.visibility, c.resolvedStyle.display,
                        c.resolvedStyle.backgroundColor.a, c.style.backgroundImage.keyword != StyleKeyword.Undefined);
                }
                Debug.Log(sb2.ToString());
            }

            var gb = hud.worldBound;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root[i];
                if (c == hud.parent) continue;
                if (c.worldBound.Overlaps(gb) && c.worldBound.width > 100f)
                    Debug.Log($"[PlayCapture] 겹침 후보: [{i}] {c.name} wb={RectStr(c.worldBound)} bgA={c.resolvedStyle.backgroundColor.a:0.00}");
            }
        }

        private static string RectStr(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0}) {r.width:0}x{r.height:0}";
#endif
    }
}