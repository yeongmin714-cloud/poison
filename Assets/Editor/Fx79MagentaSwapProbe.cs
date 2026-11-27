using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using ProjectName.Core;        // NationType
using ProjectName.Core.Data;   // BiomeType
using ProjectName.Systems;     // FxSlashFlipbookFlash, MagentaSentinel, FxFlipbookGenerator, TerrainGenerator
using System.Collections.Generic;
using System.IO;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호 79-R16] F10 아틀라스 마젠타 스왑 검증 배치 프로브 — 셀프 종료.
    ///
    /// 목적: 사용자 세션 실험(Play → F10 → 전투 1~2분 → 베이지 박스가 마젠타로 바뀌는지)을
    /// 배치에서 동일 조건으로 실행해 정량 판정한다.
    ///   Phase BEFORE — 아크 플립북(ArcFlash) 지속 발화, 파일 캡처 스캔으로 베이지 기저 확보
    ///   F10 SWAP — MagentaSentinel.ApplyAtlasMagenta(true) (F10 핸들러와 동일 경로 직접 발화)
    ///   Phase AFTER — 동일 발화 재개, 베이지 잔존 여부 + 마젠타 출현 여부 정량 판정
    ///
    /// 판정 계약 (솔직 보고):
    ///   - BEFORE에서 베이지 미재현 → "배치 미재현" 판정 (F10 실험의 배치 판정 불가 명시)
    ///   - AFTER 베이지 ≥ 임계 → "베이지 잔존" (아틀라스 스왑을 따르지 않는 근원 = 아틀라스 외부)
    ///   - AFTER 베이지 소멸 + 마젠타 ≥ 임계 → "베이지 → 마젠타 전환" (근원이 아틀라스 소비처)
    ///   - AFTER 둘 다 미달 → "둘 다 미관측" (아크 미발화/캡처 이상 — 숫자 그대로 보고)
    ///
    /// 안전 계약 (Fx79BisectProbe 승계): -executeMethod 태그 무장, 배치 전용, 씬 전투 차단,
    ///   파일 캡처 표준(RT 감지 금지 — 64차 교훈), 요약 파일 기록 후 Exit.
    /// </summary>
    [InitializeOnLoad]
    public static class Fx79MagentaSwapProbe
    {
        const string PhaseKey = "Fx79MagentaSwapProbePhase";
        const string ScenePath = "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity";
        internal const string SummaryPath = "TestOutput/no79_magswap.txt";
        const string EntryTag = "Fx79MagentaSwapProbe";

        static Fx79MagentaSwapProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
        }

        static bool Armed
        {
            get
            {
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                    if (args[i] != null && args[i].Contains(EntryTag)) return true;
                return false;
            }
        }

        public static void Run()
        {
            if (!Armed) { Debug.LogWarning("[79M-PROBE] 미무장 — executeMethod 태그 불일치, 무시"); return; }
            if (Phase != 0) { Debug.LogWarning("[79M-PROBE] 이미 진행 중(phase=" + Phase + ") — 무시"); return; }
            Debug.Log("[79M-PROBE] Run — 씬 개봉: " + ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Phase = 1;
        }

        static int Phase { get { return SessionState.GetInt(PhaseKey, 0); } set { SessionState.SetInt(PhaseKey, value); } }

        static void Tick()
        {
            if (!Armed) return;
            int phase = Phase;
            if (phase == 1 && !EditorApplication.isPlaying)
            {
                Phase = 1;
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.Log("[79M-PROBE] Play 진입 요청");
                    EditorApplication.isPlaying = true;
                }
            }
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!Armed) return;
            if (state == PlayModeStateChange.EnteredPlayMode && Phase < 2)
            {
                Phase = 2;
                var hostGo = new GameObject("Fx79MagentaSwapProbeHost") { hideFlags = HideFlags.HideAndDontSave };
                hostGo.AddComponent<Fx79MagentaSwapProbeHost>();
                Debug.Log("[79M-PROBE] 호스트 생성 — F10 스왑 시퀀스 개시");
            }
            else if (state == PlayModeStateChange.EnteredEditMode && Phase >= 3)
            {
                Phase = 0;
                Debug.Log("[79M-PROBE] FINISH — 편집모드 복귀, Exit(0)");
                EditorApplication.Exit(0);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && Phase == 2)
            {
                Phase = 3;
            }
        }
    }

    /// <summary>플레이 모드 구동부 — BEFORE 발화 → F10 스왑 → AFTER 발화 → 판정.</summary>
    public class Fx79MagentaSwapProbeHost : MonoBehaviour
    {
        const int BeigeMinCount = 300;      // Fx79BisectProbe R1 임계 승계 (2px 스텝 샘플)
        const int MagMinCount = 300;
        const float SpawnInterval = 1.2f;   // 아크 발화 주기 — 실전 히트 빈도 근사
        const float CaptureInterval = 2.0f;
        const int CapturesPerPhase = 18;    // 18×2s ≈ 36s/페이즈 — 전투 1~2분 근사
        const float PhaseWindow = 40f;

        RenderTexture _rt;
        float _nextSpawn;
        float _nextCapture;
        float _phaseStart;
        int _seq;
        int _captures;
        string _phase = "boot";        // boot → before → swap → after → exitreq
        float _bootWait;
        Camera _cam;
        readonly List<string> _summary = new List<string>();

        // 페이즈별 최대 클러스터 — 판정 원료
        int _beforeMaxBeige, _beforeMaxMag, _beforeMaxBlack;
        int _afterMaxBeige, _afterMaxMag, _afterMaxBlack;
        int _beforeBeigeSeen, _afterBeigeSeen;   // 캡처 중 베이지>0 캡처 수

        void Start()
        {
            Debug.Log("[79M-PROBE] 호스트 Start — 부팅 대기");
            _bootWait = 2.5f;
            _phase = "boot";
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
                    else if (_bootWait < -5f) FailExit("카메라 부재 — 부팅 실패");
                    return;
                }
                if (_phase == "before") TickPhase("before");
                else if (_phase == "after") TickPhase("after");
                else if (_phase == "swap") DoSwap();
                else if (_phase == "exitreq") { /* RequestExit이 처리 */ }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[79M-ERR] " + e);
                FailExit(e.Message);
            }
        }

        void BeginRun()
        {
            Debug.Log("[79M-PROBE] 런 개시 — cam=" + (_cam != null ? _cam.name : "NULL"));
            _summary.Add("== no79 F10 magenta swap — " + System.DateTime.Now.ToString("HH:mm:ss") + " ==");
            _summary.Add("scene=Test_10 cam=" + (_cam != null ? _cam.name : "NULL"));
            if (_rt == null)
            {
                _rt = new RenderTexture(960, 540, 24);
                _rt.name = "Fx79MRT";
            }
            // 씬 자체 전투 차단 — FX 근원을 프로브 발화로 한정(Fx79BisectProbe 계약 승계)
            DisableSceneCombat();
            _summary.Add("MagSWAP_initial=" + MagentaSentinel.AtlasMagentaSwap);
            StartPhase("before");
        }

        void StartPhase(string p)
        {
            _phase = p;
            _phaseStart = Time.time;
            _nextSpawn = Time.time + 0.3f;
            _nextCapture = Time.time + 1.0f;
            _captures = 0;
            Debug.Log("[79M-PROBE] 페이즈 개시 " + p);
        }

        void TickPhase(string p)
        {
            float now = Time.time;
            if (now - _phaseStart > PhaseWindow || _captures >= CapturesPerPhase)
            {
                EndPhase(p);
                return;
            }
            if (now >= _nextSpawn)
            {
                SpawnArc(p);
                _nextSpawn = now + SpawnInterval;
            }
            if (now >= _nextCapture)
            {
                DoFileCapture(p);
                _nextCapture = now + CaptureInterval;
            }
        }

        void EndPhase(string p)
        {
            if (p == "before")
            {
                _summary.Add("BEFORE maxBeige=" + _beforeMaxBeige + " maxMag=" + _beforeMaxMag + " maxBlack=" + _beforeMaxBlack
                    + " beigeSeen=" + _beforeBeigeSeen + "/" + _captures);
                _phase = "swap";   // 다음 Update에서 DoSwap
                Debug.Log("[79M-PROBE] BEFORE 종료 — F10 스왑 준비");
            }
            else if (p == "after")
            {
                _summary.Add("AFTER maxBeige=" + _afterMaxBeige + " maxMag=" + _afterMaxMag + " maxBlack=" + _afterMaxBlack
                    + " beigeSeen=" + _afterBeigeSeen + "/" + _captures);
                Verdict();
                RequestExit();
            }
        }

        /// <summary>F10 핸들러와 동일 경로 — AtlasMagentaSwap=true + ApplyAtlasMagenta + 아틀라스 직독 검증.</summary>
        void DoSwap()
        {
            MagentaSentinel.AtlasMagentaSwap = true;
            MagentaSentinel.ApplyAtlasMagenta();
            Debug.LogWarning("[79M-PROBE] F10 스왑 경로 발화 완료 — AtlasMagentaSwap=" + MagentaSentinel.AtlasMagentaSwap);
            VerifyAtlas("post-swap");
            _summary.Add("F10_swap=ON at " + Time.time.ToString("F1"));
            StartPhase("after");
        }

        /// <summary>아크 플립북 발화 — 사용자 세션 F10 실험과 동일 대상(SlashArcFlash). 스폰점은 화면 중앙 투영.</summary>
        void SpawnArc(string p)
        {
            Vector3 pos = SpawnPoint();
            float scale = (_seq % 2 == 0) ? 1f : 1.5f;
            _seq++;
            // 비네트 펄스 — Fx79BisectProbe와 동일 조건(웜화이트 쿼드×적색 틴트)
            ScreenVignetteFX.Begin(_cam, new Color(0.9f, 0.12f, 0.1f), 0.22f, 0.05f, 1.8f, 0.5f);
            FxSlashFlipbookFlash.Play(pos, scale, false);
            Vector3 vp = _cam != null ? _cam.WorldToViewportPoint(pos) : Vector3.zero;
            Debug.Log("[79M-SPAWN] " + p + " #" + _seq + " pos=" + pos.ToString("F1") + " scale=" + scale.ToString("F1")
                + " vp=" + vp.x.ToString("F2") + "," + vp.y.ToString("F2"));
        }

        /// <summary>씬 자체 전투 차단 — AnimalAI + Guard/RecruitedSoldier 태그 비활성(Fx79BisectProbe 동일).</summary>
        void DisableSceneCombat()
        {
            int ai = 0, guard = 0;
            foreach (var aiComp in Object.FindObjectsOfType<AnimalAI>())
                if (aiComp != null && aiComp.gameObject.activeInHierarchy) { aiComp.gameObject.SetActive(false); ai++; }
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (go.tag == "Guard" || go.tag == "RecruitedSoldier") { go.SetActive(false); guard++; }
            }
            Debug.Log("[79M-COMBAT] 씬 전투 차단 — AnimalAI " + ai + "건, Guard태그 " + guard + "건 비활성");
        }

        /// <summary>화면 중앙 투영 지면 스폰점 — Fx79BisectProbe R2 교정본 승계.</summary>
        Vector3 SpawnPoint()
        {
            if (_cam == null) return Vector3.zero;
            Ray r = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float bestErr = float.MaxValue;
            Vector3 bestP = r.origin + r.direction * 5f;
            for (float dist = 2f; dist <= 18f; dist += 0.5f)
            {
                Vector3 cand = r.origin + r.direction * dist;
                float gy = 1f + TerrainGenerator.GetHeightAt(cand.x, cand.z, BiomeType.Plains, 42);
                Vector3 gp = new Vector3(cand.x, gy + 0.1f, cand.z);
                Vector3 vp = _cam.WorldToViewportPoint(gp);
                if (vp.z <= 0.1f) continue;
                float err = (vp.x - 0.5f) * (vp.x - 0.5f) + (vp.y - 0.5f) * (vp.y - 0.5f);
                if (err < bestErr) { bestErr = err; bestP = gp; }
            }
            return bestP;
        }

        /// <summary>아틀라스 직독 — 셀 중심 색으로 스왑 적용 여부를 GPU 원료와 독립 검증.</summary>
        void VerifyAtlas(string tag)
        {
            try
            {
                var tex = FxFlipbookGenerator.GetSlashArcFlipbook();
                if (tex == null) { Debug.Log("[79M-ATLAS] " + tag + " slashArc NULL"); _summary.Add("ATLAS_" + tag + "=NULL"); return; }
                string info = "[79M-ATLAS] " + tag + " slashArc " + tex.width + "x" + tex.height + " readable=" + tex.isReadable;
                if (tex.isReadable)
                {
                    int cw = tex.width / 4, ch = tex.height / 2;
                    Color32 c0 = tex.GetPixel(cw / 2, ch + ch / 2);
                    Color32 c6 = tex.GetPixel(2 * cw + cw / 2, ch / 2);
                    info += " cell0center=" + c0 + " cell6center=" + c6;
                }
                Debug.Log(info);
                _summary.Add("ATLAS_" + tag + "=" + info);
            }
            catch (System.Exception e) { Debug.LogWarning("[79M-ATLAS] " + tag + " 검증 실패 " + e.Message); }
        }

        // ────────────────────────── 캡처/스캔 ──────────────────────────
        void DoFileCapture(string p)
        {
            string rel = "TestOutput/no79m_" + p + "_" + _seq + "_" + _captures + ".png";
            ScreenCapture.CaptureScreenshot(rel, 1);
            StartCoroutine(ReadFileNext(p, rel));
        }

        System.Collections.IEnumerator ReadFileNext(string p, string rel)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", rel));
            for (int i = 0; i < 8; i++) yield return null;
            if (!File.Exists(full)) { Debug.LogWarning("[79M-SCAN] 파일 미생성 " + rel); yield break; }
            byte[] bytes = File.ReadAllBytes(full);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(bytes)) { Debug.LogWarning("[79M-SCAN] 로드 실패 " + rel); Object.Destroy(tex); yield break; }
            ScanAndLog(p, rel, tex);
            Object.Destroy(tex);
        }

        void ScanAndLog(string p, string tag, Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            int beige = 0, black = 0, white = 0, mag = 0;
            int bx0 = -1, by0 = -1, bx1 = -1, by1 = -1;
            for (int y = 0; y < h; y += 2)
            {
                int row = y * w;
                for (int x = 0; x < w; x += 2)
                {
                    Color32 c = px[row + x];
                    int r = c.r, g = c.g, bl = c.b;
                    if (r >= 232 && g >= 178 && g <= 222 && bl >= 148 && bl <= 205 && r > g && g > bl)
                    {
                        beige++;
                        if (bx0 < 0) { bx0 = bx1 = x; by0 = by1 = y; }
                        else { if (x < bx0) bx0 = x; if (x > bx1) bx1 = x; if (y < by0) by0 = y; if (y > by1) by1 = y; }
                    }
                    else if (r < 12 && g < 12 && bl < 12) black++;
                    else if (r > 240 && g < 30 && bl > 240) mag++;
                    else if (r > 252 && g > 252 && bl > 252) white++;
                }
            }
            string line = "[79M-SCAN] " + p + " " + tag + " beige=" + beige + " black=" + black + " white=" + white + " mag=" + mag;
            if (beige > 0) line += " beigeBox=(" + bx0 + "," + by0 + ")-(" + bx1 + "," + by1 + ")";
            Debug.Log(line);
            _summary.Add("  " + line);
            if (p == "before")
            {
                if (beige > _beforeMaxBeige) _beforeMaxBeige = beige;
                if (mag > _beforeMaxMag) _beforeMaxMag = mag;
                if (black > _beforeMaxBlack) _beforeMaxBlack = black;
                if (beige > 0) _beforeBeigeSeen++;
            }
            else
            {
                if (beige > _afterMaxBeige) _afterMaxBeige = beige;
                if (mag > _afterMaxMag) _afterMaxMag = mag;
                if (black > _afterMaxBlack) _afterMaxBlack = black;
                if (beige > 0) _afterBeigeSeen++;
            }
        }

        // ────────────────────────── 판정/종료 ──────────────────────────
        void Verdict()
        {
            string v;
            if (_beforeMaxBeige < BeigeMinCount)
            {
                v = "VERDICT: BEFORE 베이지 미재현(maxBeige=" + _beforeMaxBeige + " < " + BeigeMinCount
                    + ") — 배치에서 결함 재현 실패. F10 실험의 배치 판정 불가 → 사용자 세션 Play+F10로 확인 필요.";
            }
            else if (_afterMaxBeige >= BeigeMinCount)
            {
                v = "VERDICT: 베이지 잔존 — F10 스왑 후에도 베이지 유지(maxBeige=" + _afterMaxBeige
                    + ", maxMag=" + _afterMaxMag + "). 근원이 아틀라스 소비처가 아님(아틀라스 외부 경로).";
            }
            else if (_afterMaxMag >= MagMinCount)
            {
                v = "VERDICT: 베이지 → 마젠타 전환 확인 — F10 스왑 후 베이지 소멸(maxBeige=" + _afterMaxBeige
                    + ") + 마젠타 출현(maxMag=" + _afterMaxMag + "). 근원 = 아틀라스 소비처.";
            }
            else
            {
                v = "VERDICT: 둘 다 미관측 — AFTER 베이지 " + _afterMaxBeige + " / 마젠타 " + _afterMaxMag
                    + " (임계 " + BeigeMinCount + "). 아크 미발화 또는 캡처 이상 — 숫자 원본 확인 필요.";
            }
            Debug.LogWarning("[79M-VERDICT] " + v);
            _summary.Add(v);
        }

        void RequestExit()
        {
            if (_phase == "exitreq") return;
            _phase = "exitreq";
            WriteSummary();
            Debug.Log("[79M-PROBE] 시퀀스 완료 — Play 종료 요청");
            foreach (var s in _summary)
                if (s.StartsWith("==") || s.StartsWith("BEFORE") || s.StartsWith("AFTER") || s.StartsWith("VERDICT") || s.StartsWith("ATLAS"))
                    Debug.Log("[79M-SUMMARY] " + s);
            EditorApplication.isPlaying = false;   // 편집모드 복귀 시 Exit(0)
        }

        void FailExit(string why)
        {
            _summary.Add("FAIL: " + why);
            WriteSummary();
            Debug.LogError("[79M-PROBE] FAIL — " + why + " — Exit(1)");
            EditorApplication.Exit(1);
        }

        void WriteSummary()
        {
            try
            {
                string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Fx79MagentaSwapProbe.SummaryPath));
                File.WriteAllLines(full, _summary.ToArray());
                Debug.Log("[79M-PROBE] 요약 기록 — " + full);
            }
            catch (System.Exception e) { Debug.LogError("[79M-ERR] 요약 기록 실패 " + e.Message); }
        }
    }
}
