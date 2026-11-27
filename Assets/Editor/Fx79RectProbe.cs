using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호 79-R18] 베이지 사각 원인 특정 배치 프로브 — 셀프 종료.
    ///
    /// 배경: R17(grad)에서 그라디언트 RGB가 사각 영역에 보임 → UV 정상, RGB 샘플링 정상 확인.
    ///   사각 형상(블록)은 알파가 형상을 만들지 않는 것 → 알파 채널 판별 실험 추가.
    ///
    /// 가설:
    ///   H2a — 텍스처 알파가 GPU에서 255로 강제(업로드/포맷 문제) → 알파를 0으로 만들어도 사각 유지
    ///   H2b — 셰이더가 텍스처 알파를 무시(버텍스 알파만 사용) → 알파를 0으로 만들어도 사각 유지
    ///   H2c — 알파는 정상 샘플링(사각의 원인이 알파 값 자체) → 알파를 0으로 만들면 사각 소멸
    ///
    /// 판별법: 아틀라스 알파를 전부 0으로 스왑(RGB=그라디언트 유지) 후 발화.
    ///   사각 영역에 그라디언트 시그니처(G∈[118,138], B≤25) 픽셀이 다수 관측 → 알파 무시(H2a/H2b)
    ///   시그니처 미관측(사각 소멸) → 알파 정상 샘플링(H2c — 알파 값 자체가 문제)
    ///
    /// 페이즈:
    ///   prod  — 원본 아틀라스, 생산 경로 발화, 베이지 기저 + 스트림/머티리얼 덤프
    ///   grad  — 그라디언트 RGB 스왑, R 편차 판정(UV 정상 확인)
    ///   alpha0 — 그라디언트 RGB + 알파=0 스왑, 사각 소멸 여부 판정
    ///   복구 — 원본 아틀라스 복원 후 Exit
    /// </summary>
    [InitializeOnLoad]
    public static class Fx79RectProbe
    {
        const string PhaseKey = "Fx79RectProbePhase";
        const string ScenePath = "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity";
        internal const string SummaryPath = "TestOutput/no79rect.txt";
        const string EntryTag = "Fx79RectProbe";

        static Fx79RectProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
        }

        static bool Armed
        {
            get
            {
                var args = System.Environment.GetCommandLineArgs();
                foreach (var a in args) if (a != null && a.Contains(EntryTag)) return true;
                return false;
            }
        }

        static int Phase
        {
            get { return SessionState.GetInt(PhaseKey, 0); }
            set { SessionState.SetInt(PhaseKey, value); }
        }

        public static void Run()
        {
            if (!Armed) { Debug.LogWarning("[79R-PROBE] 미무장 — 무시"); return; }
            if (Phase != 0) { Debug.LogWarning("[79R-PROBE] 진행 중 phase=" + Phase); return; }
            Debug.Log("[79R-PROBE] Run — 씬 개봉: " + ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Phase = 1;
        }

        static void Tick()
        {
            if (!Armed) return;
            if (Phase == 1 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!Armed) return;   // [무번호 81] 배치 무장 게이트 — 미무장 세션(사용자 Play)에서 호스트 생성/Exit(0) 차단 (Bisect/Magenta/Pigeon 3형제 동일 계약, 2026-11-20 세션 유출 사후)
            if (state == PlayModeStateChange.EnteredPlayMode && Phase < 2)
            {
                Phase = 2;
                var go = new GameObject("Fx79RectProbeHost") { hideFlags = HideFlags.HideAndDontSave };
                go.AddComponent<Fx79RectProbeHost>();
                Debug.Log("[79R-PROBE] 호스트 생성");
            }
            else if (state == PlayModeStateChange.EnteredEditMode && Phase >= 2)
            {
                Phase = 0;
                Debug.Log("[79R-PROBE] FINISH — 편집모드 복귀, Exit(0)");
                EditorApplication.Exit(0);
            }
        }
    }

    public class Fx79RectProbeHost : MonoBehaviour
    {
        const float SpawnInterval = 1.2f;
        const float CaptureInterval = 2.0f;
        const int CapturesPerPhase = 6;
        const float PhaseWindow = 16f;
        const float GradMinSpread = 80f;
        const float GradMaxSpread = 30f;
        const int Alpha0ShapeThreshold = 100;   // 그라디언트 시그니처 픽셀 수 임계

        float _nextSpawn, _nextCapture, _phaseStart;
        int _seq, _captures;
        string _phase = "boot";
        float _bootWait = 2.5f;
        Camera _cam;
        readonly List<string> _summary = new List<string>();

        Color32[] _origPixels;
        bool _atlasSaved;

        int _prodMaxBeige, _gradMaxBeige, _alpha0MaxSig;
        float _prodBestSpread, _gradBestSpread;
        string _gradVerdict = "미판정";
        string _alpha0Verdict = "미판정";

        void Start()
        {
            Debug.Log("[79R-HOST] Start — 부팅 대기");
        }

        void Update()
        {
            try
            {
                if (_phase == "boot")
                {
                    _bootWait -= Time.deltaTime;
                    if (_cam == null) _cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
                    if (_bootWait <= 0f && _cam != null) BeginRun();
                    else if (_bootWait < -5f) FailExit("카메라 부재");
                    return;
                }
                if (_phase == "prod" || _phase == "grad" || _phase == "alpha0") TickPhase(_phase);
                else if (_phase == "restore") DoRestore();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[79R-ERR] " + e);
                FailExit(e.Message);
            }
        }

        void BeginRun()
        {
            _summary.Add("== no79rect " + System.DateTime.Now.ToString("HH:mm:ss") + " ==");
            _summary.Add("cam=" + (_cam != null ? _cam.name : "NULL"));
            DisableSceneCombat();
            StartPhase("prod");
        }

        void StartPhase(string p)
        {
            _phase = p;
            _phaseStart = Time.time;
            _nextSpawn = Time.time + 0.3f;
            _nextCapture = Time.time + 1.0f;
            _captures = 0;
            Debug.Log("[79R-HOST] 페이즈 개시 " + p);
        }

        void TickPhase(string p)
        {
            if (Time.time - _phaseStart > PhaseWindow || _captures >= CapturesPerPhase)
            {
                EndPhase(p);
                return;
            }
            if (Time.time >= _nextSpawn)
            {
                SpawnArc(p);
                _nextSpawn = Time.time + SpawnInterval;
            }
            if (Time.time >= _nextCapture)
            {
                DoCapture(p);
                _nextCapture = Time.time + CaptureInterval;
            }
        }

        void EndPhase(string p)
        {
            if (p == "prod")
            {
                _summary.Add("PROD maxBeige=" + _prodMaxBeige + " bestSpread=" + _prodBestSpread.ToString("F0"));
                SwapAtlasGradient();
                StartPhase("grad");
            }
            else if (p == "grad")
            {
                _summary.Add("GRAD maxBeige=" + _gradMaxBeige + " bestSpread=" + _gradBestSpread.ToString("F0"));
                _summary.Add("GRAD_VERDICT: " + _gradVerdict);
                SwapAtlasAlphaZero();
                StartPhase("alpha0");
            }
            else if (p == "alpha0")
            {
                _summary.Add("ALPHA0 maxSig=" + _alpha0MaxSig);
                _summary.Add("ALPHA0_VERDICT: " + _alpha0Verdict);
                _phase = "restore";
            }
        }

        void DoRestore()
        {
            RestoreAtlas();
            WriteSummary();
            Debug.Log("[79R-SUMMARY] 기록 완료 — Exit 요청");
            _phase = "exitreq";
            EditorApplication.isPlaying = false;
        }

        void FailExit(string why)
        {
            _summary.Add("FAIL: " + why);
            WriteSummary();
            _phase = "exitreq";
            EditorApplication.isPlaying = false;
        }

        void WriteSummary()
        {
            try
            {
                string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Fx79RectProbe.SummaryPath));
                File.WriteAllLines(full, _summary.ToArray());
                Debug.Log("[79R-SUMMARY] " + string.Join(" | ", _summary.ToArray()));
            }
            catch (System.Exception e) { Debug.LogError("[79R-ERR] 요약 기록 실패 " + e.Message); }
        }

        // ───────── 스폰 ─────────

        Vector3 SpawnPoint()
        {
            if (_cam == null) return Vector3.zero;
            Ray r = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(r, out var hit, 30f))
                return hit.point;
            return r.origin + r.direction * 6f;
        }

        void SpawnArc(string p)
        {
            Vector3 pos = SpawnPoint();
            float scale = (_seq % 2 == 0) ? 1f : 1.5f;
            _seq++;
            FxSlashFlipbookFlash.Play(pos, scale, false);
            Vector3 vp = _cam != null ? _cam.WorldToViewportPoint(pos) : Vector3.zero;
            Debug.Log("[79R-SPAWN] " + p + " #" + _seq + " pos=" + pos.ToString("F1")
                + " vp=" + vp.x.ToString("F2") + "," + vp.y.ToString("F2"));
        }

        // ───────── 캡처/스캔 ─────────

        void DoCapture(string p)
        {
            string rel = "TestOutput/no79r_" + p + "_" + _seq + "_" + _captures + ".png";
            ScreenCapture.CaptureScreenshot(rel, 1);
            StartCoroutine(ReadFileNext(p, rel, _captures));
            _captures++;
        }

        System.Collections.IEnumerator ReadFileNext(string p, string rel, int capIdx)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", rel));
            for (int i = 0; i < 10; i++) yield return null;
            if (!File.Exists(full)) { Debug.LogWarning("[79R-SCAN] 파일 미생성 " + rel); yield break; }
            byte[] bytes = File.ReadAllBytes(full);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(bytes)) { Debug.LogWarning("[79R-SCAN] 로드 실패 " + rel); Object.Destroy(tex); yield break; }
            ScanAndLog(p, rel, tex, capIdx);
            Object.Destroy(tex);
        }

        void ScanAndLog(string p, string tag, Texture2D tex, int capIdx)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            int beige = 0, gradSig = 0;
            int bx0 = -1, by0 = -1, bx1 = -1, by1 = -1;
            for (int y = 0; y < h; y += 2)
            {
                int row = y * w;
                for (int x = 0; x < w; x += 2)
                {
                    Color32 c = px[row + x];
                    if (c.r >= 232 && c.g >= 178 && c.g <= 222 && c.b >= 148 && c.b <= 205 && c.r > c.g && c.g > c.b)
                    {
                        beige++;
                        if (bx0 < 0) { bx0 = bx1 = x; by0 = by1 = y; }
                        else
                        {
                            if (x < bx0) bx0 = x; if (x > bx1) bx1 = x;
                            if (y < by0) by0 = y; if (y > by1) by1 = y;
                        }
                    }
                    // 그라디언트 시그니처 — G≈128, B≈0 (아틀라스 G=128, B=0, 알파 무시 시 렌더)
                    if (c.g >= 118 && c.g <= 138 && c.b <= 25 && c.r >= 60)
                        gradSig++;
                }
            }
            string line = "[79R-SCAN] " + p + " " + tag + " beige=" + beige + " gradSig=" + gradSig;
            if (beige > 0) line += " beigeBox=(" + bx0 + "," + by0 + ")-(" + bx1 + "," + by1 + ")";

            float spread = -1f;
            if (beige >= 60 && bx1 > bx0)
            {
                int rmin = 255, rmax = 0;
                for (int y = by0; y <= by1; y += 2)
                {
                    int row = y * w;
                    for (int x = bx0; x <= bx1; x += 2)
                    {
                        Color32 c = px[row + x];
                        if (c.r >= 232 && c.g >= 178 && c.g <= 222 && c.b >= 148 && c.b <= 205 && c.r > c.g && c.g > c.b)
                        {
                            if (c.r < rmin) rmin = c.r;
                            if (c.r > rmax) rmax = c.r;
                        }
                    }
                }
                spread = rmax - rmin;
                line += " rSpread=" + spread.ToString("F0");
            }
            Debug.Log(line);
            _summary.Add("  " + line);

            if (p == "prod")
            {
                if (beige > _prodMaxBeige) _prodMaxBeige = beige;
                if (spread > _prodBestSpread) _prodBestSpread = spread;
            }
            else if (p == "grad")
            {
                if (beige > _gradMaxBeige) _gradMaxBeige = beige;
                if (spread > _gradBestSpread) _gradBestSpread = spread;
                if (spread >= GradMinSpread) _gradVerdict = "UV정상-그라디언트보임(spread=" + spread.ToString("F0") + ")";
                else if (spread > 0 && spread <= GradMaxSpread) _gradVerdict = "UV붕괴-균일색(spread=" + spread.ToString("F0") + ")";
            }
            else if (p == "alpha0")
            {
                if (gradSig > _alpha0MaxSig) _alpha0MaxSig = gradSig;
                if (gradSig >= Alpha0ShapeThreshold)
                    _alpha0Verdict = "알파무시-사각유지(gradSig=" + gradSig + " — 알파=0인데도 렌더)";
                else
                    _alpha0Verdict = "알파정상샘플-사각소멸(gradSig=" + gradSig + " — 알파=0이면 투명)";
            }

            if (capIdx % 3 == 2) DumpRenderState(p);
        }

        void DumpRenderState(string p)
        {
            try
            {
                // internal 접근자 — 에디터 어셈블리에서 리플렉션으로 획득
                var pi = typeof(FxSlashFlipbookFlash).GetProperty("DebugCachedMat",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var mat = pi != null ? pi.GetValue(null) as Material : null;
                string mline = "[79R-DUMP] " + p + " mat=";
                if (mat == null) { mline += "NULL"; }
                else
                {
                    mline += mat.name + " shader=" + (mat.shader != null ? mat.shader.name : "NULL")
                        + " keywords=[" + string.Join(",", mat.shaderKeywords) + "]"
                        + " queue=" + mat.renderQueue;
                    if (mat.HasProperty("_BaseColor")) mline += " _BaseColor=" + mat.GetColor("_BaseColor");
                    var map = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") as Texture2D : null;
                    if (map != null) mline += " _BaseMap=" + map.width + "x" + map.height;
                }
                Debug.Log(mline);
                _summary.Add("  " + mline);

                var rnd = Object.FindObjectOfType<ParticleSystemRenderer>();
                if (rnd != null && rnd.gameObject.name == "SlashArcFlash")
                {
                    var streams = new List<ParticleSystemVertexStream>();
                    rnd.GetActiveVertexStreams(streams);
                    string sline = "[79R-DUMP] " + p + " streams=[" + string.Join(",", streams) + "]";
                    var ps = rnd.GetComponent<ParticleSystem>();
                    if (ps != null) sline += " alive=" + ps.IsAlive(true) + " n=" + ps.particleCount;
                    Debug.Log(sline);
                    _summary.Add("  " + sline);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[79R-DUMP] 실패 " + e.Message); }
        }

        // ───────── 아틀라스 스왑 ─────────

        void SwapAtlasGradient()
        {
            var tex = FxFlipbookGenerator.GetSlashArcFlipbook();
            if (tex == null) { Debug.LogError("[79R-ATLAS] slashArc NULL"); return; }
            if (!_atlasSaved) { _origPixels = tex.GetPixels32(); _atlasSaved = true; }
            int cw = tex.width / 4, ch = tex.height / 2;
            var px = tex.GetPixels32();
            for (int f = 0; f < 8; f++)
            {
                int cx = (f % 4) * cw, cy = (1 - (f / 4)) * ch;
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        byte r = (byte)(x * 255f / (cw - 1));
                        px[(cy + y) * tex.width + (cx + x)] = new Color32(r, 128, 0, px[(cy + y) * tex.width + (cx + x)].a);
                    }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            Debug.Log("[79R-ATLAS] 그라디언트 스왑 완료 — R=셀로컬x, G=128, B=0");
            _summary.Add("ATLAS_GRADIENT=ON");
        }

        void SwapAtlasAlphaZero()
        {
            var tex = FxFlipbookGenerator.GetSlashArcFlipbook();
            if (tex == null) { Debug.LogError("[79R-ATLAS] slashArc NULL"); return; }
            if (!_atlasSaved) { _origPixels = tex.GetPixels32(); _atlasSaved = true; }
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(px[i].r, px[i].g, px[i].b, 0);   // 알파 전부 0
            tex.SetPixels32(px);
            tex.Apply(false, false);
            Debug.Log("[79R-ATLAS] 알파=0 스왑 완료 — RGB=그라디언트 유지");
            _summary.Add("ATLAS_ALPHA0=ON");
        }

        void RestoreAtlas()
        {
            if (!_atlasSaved || _origPixels == null) return;
            var tex = FxFlipbookGenerator.GetSlashArcFlipbook();
            if (tex == null) return;
            tex.SetPixels32(_origPixels);
            tex.Apply(false, false);
            Debug.Log("[79R-ATLAS] 원본 복원 완료");
            _summary.Add("ATLAS_RESTORED=ON");
        }

        void DisableSceneCombat()
        {
            int ai = 0, guard = 0;
            foreach (var comp in Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (comp is AnimalAI) { comp.gameObject.SetActive(false); ai++; }
            }
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.CompareTag("Guard") || go.CompareTag("RecruitedSoldier")) { go.SetActive(false); guard++; }
            }
            Debug.Log("[79R-COMBAT] 씬 전투 차단 — AI " + ai + ", 병사 " + guard);
            _summary.Add("combatOff ai=" + ai + " guard=" + guard);
        }
    }
}
