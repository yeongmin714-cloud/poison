using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using ProjectName.Core;        // WeaponType
using ProjectName.Core.Data;   // BiomeType
using ProjectName.Systems;     // SlashVFXRunner, FxSlashFlipbookFlash, AttackArcVFX, TerrainGenerator
using ProjectName.UI.Toolkit;  // UIToolkitBootstrap
using System.Collections.Generic;
using System.IO;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호 79-R0-B] 베이지/검정 사각 근원 특정 바이섹션 프로브 — 배치 전용, 셀프 종료.
    ///
    /// 안전 계약 (Fx69BlobProbe R5 교훈 반영):
    ///  - 커맨드라인 -executeMethod 에 "Fx79BisectProbe" 문자열이 있을 때만 무장 — 사용자 에디터 세션(평시 기동)에서는
    ///    Tick이 즉시 반환(아무 오브젝트 생성/구독 동작/렌더 요청 없음). 세션 상주 오염원 아님.
    ///  - 런타임 인스턴스 억제 바이섹션(코드/에셋 무수정) — 후보 GO 이름 매칭 렌더러 OFF 후 클러스터 붕괴율 측정.
    ///  - 이중 캡처 판별: 파일 캡처(CaptureScreenshot = 게임뷰 전체 incl. 화면공간 UI) vs RT 캡처
    ///    (CaptureScreenshotIntoRenderTexture = 카메라 레이어) — 게임뷰 전용(오버레이 계열) 여부를 숫자로 분리.
    ///  - 종료: Play 종료 → 편집모드 복귀 → 요약 파일 기록 → EditorApplication.Exit. 잔존 구독/GO 0.
    /// </summary>
    [InitializeOnLoad]
    public static class Fx79BisectProbe
    {
        const string PhaseKey = "Fx79BisectProbePhase";   // SessionState — 도메인 리로드 불문, 프로세스 종료 시 소멸
        const string ScenePath = "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity";
        internal const string SummaryPath = "TestOutput/no79_bisect.txt";   // 호스트에서 참조 — internal
        const string EntryTag = "Fx79BisectProbe";

        static Fx79BisectProbe()
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

        /// <summary>배치 진입점 — 씬 개봉 + Play 진입 요청. 이후 Tick/OnPlayModeChanged 상태머신이 이어받는다.</summary>
        public static void Run()
        {
            if (!Armed) { Debug.LogWarning("[79-PROBE] 미무장 — executeMethod 태그 불일치, 무시"); return; }
            if (Phase != 0) { Debug.LogWarning("[79-PROBE] 이미 진행 중(phase=" + Phase + ") — 무시"); return; }
            Debug.Log("[79-PROBE] Run — 씬 개봉: " + ScenePath);
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
                Phase = 1;   // 유지 — isPlaying 대입 후 리로드에서 2로 진행
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.Log("[79-PROBE] Play 진입 요청");
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
                var hostGo = new GameObject("Fx79ProbeHost") { hideFlags = HideFlags.HideAndDontSave };
                hostGo.AddComponent<Fx79ProbeHost>();
                Debug.Log("[79-PROBE] 호스트 생성 — 바이섹션 시퀀스 개시");
            }
            else if (state == PlayModeStateChange.EnteredEditMode && Phase >= 3)
            {
                Phase = 0;
                Debug.Log("[79-PROBE] FINISH — 편집모드 복귀, Exit(0)");
                EditorApplication.Exit(0);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && Phase == 2)
            {
                Phase = 3;   // 종료 경유 표시 — 편집모드 복귀 시 Exit 트리거
            }
        }
    }

    /// <summary>플레이 모드 구동부 — 스폰/캡처/스캔/덤프/억제 바이섹션 전담.</summary>
    public class Fx79ProbeHost : MonoBehaviour
    {
        // ── 스캔 판별식(R1 승계) ──
        const int BeigeMinCount = 300;      // 스텝2 샘플 기준 — 사가 R1 임계 승계
        const int BlackMinCount = 300;

        class Burst
        {
            public string label;            // "BASE" 또는 후보명
            public string matchPrefix;      // 억제 대상 GO 이름 접두/포함 (null=무억제)
            public bool utkHide;            // UTK 레이어 억제("NameplateOverlay" 또는 전체)
            public bool exactMatch;         // true=이름 정확 일치만(하위 미포함) — emitter 분리용
            public string fxMode;           // ALL/Simbol/Flash/Shockwave/Root/ArcFlash/VignetteOnly — 단독 발화 모드
            public float t0;
            public float lastSpawnTime;     // 마지막 스폰 시각 — 플립북 프레임 역산용
            public int capturesDone;
            public int fileCaptures;        // 예약된 파일 캡처 수
            public int rtCaptures;          // 예약된 RT 캡처 수
            public int spawns;
            public List<string> scanLines = new List<string>();
            public int maxBeige, maxBlack;  // 파일 캡처 기준 최대 클러스터
            public int maxBeigeRt, maxBlackRt;
            public bool dumped;
        }

        readonly List<Burst> _bursts = new List<Burst>();
        readonly List<Burst> _pending = new List<Burst>();
        Burst _cur;
        RenderTexture _rt;
        float _nextAction;
        int _seq;
        string _mode = "boot";          // boot→run→rtphase→exitreq
        float _bootWait;
        Camera _cam;
        readonly List<string> _summary = new List<string>();
        int _dumpCount;

        struct SuppressState { public List<Renderer> off; public VisualElement utkEl; }

        void Start()
        {
            Debug.Log("[79-PROBE] 호스트 Start — 부팅 대기");
            _bootWait = 2.5f;
            _mode = "boot";
        }

        void Update()
        {
            try
            {
                if (_mode == "boot")
                {
                    _bootWait -= Time.deltaTime;
                    if (_cam == null) _cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
                    if (_bootWait <= 0f && _cam != null) BeginRun();
                    else if (_bootWait < -5f) FailExit("카메라 부재 — 부팅 실패");
                    return;
                }
                if (_mode == "run") TickRun();
                else if (_mode == "exitreq") { /* TickRun에서 전환됨 — 대기 */ }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[79-ERR] " + e);
                FailExit(e.Message);
            }
        }

        void BeginRun()
        {
            Debug.Log("[79-PROBE] 런 개시 — cam=" + (_cam != null ? _cam.name : "NULL"));
            _summary.Add("== no79 bisect — " + System.DateTime.Now.ToString("HH:mm:ss") + " ==");
            _summary.Add("scene=Test_10 cam=" + (_cam != null ? _cam.name : "NULL"));
            if (_rt == null)
            {
                _rt = new RenderTexture(960, 540, 24);
                _rt.name = "Fx79RT";
            }

            // ── 79-R8: 수리 효과 재검증 — RealChain(블랙 재현기)×3 + Control. [79-ATLAS]로 빈 셀 수리 확인 ──
            DisableSceneCombat();

            _pending.Add(new Burst { label = "RealChain", fxMode = "RealChain", fileCaptures = 6, rtCaptures = 0 });
            _pending.Add(new Burst { label = "RealChain2", fxMode = "RealChain", fileCaptures = 6, rtCaptures = 0 });
            _pending.Add(new Burst { label = "RealChain3", fxMode = "RealChain", fileCaptures = 6, rtCaptures = 0 });
            _pending.Add(new Burst { label = "Control", fxMode = "ALL", fileCaptures = 6, rtCaptures = 0 });

            VerifyAtlas();

            _mode = "run";
            _nextAction = Time.time + 0.5f;
            _summary.Add("bursts=" + _pending.Count);
        }

        void TickRun()
        {
            float now = Time.time;

            // 다음 버스트 개시
            if (_cur == null)
            {
                if (_pending.Count == 0) { RequestExit(); return; }
                _cur = _pending[0];
                _pending.RemoveAt(0);
                _cur.t0 = now;
                _nextAction = now + 0.15f;
                Debug.Log("[79-PROBE] 버스트 개시 " + _cur.label);
                return;
            }

            bool windowOver = now - _cur.t0 > 6f;
            bool capsDone = _cur.capturesDone >= _cur.fileCaptures + _cur.rtCaptures;
            bool spawnsDone = _cur.spawns >= 3;

            if (now >= _nextAction && !capsDone)
            {
                _nextAction = now + 0.28f;
                // 스폰 페이즈(버스트당 3회) — 억제 상태 유지하면서 임팩트 발화
                if (_cur.spawns < 3)
                {
                    SpawnImpact(_cur);
                    _cur.spawns++;
                    _nextAction = now + 0.45f;
                    return;
                }
                // 캡처 페이즈
                bool useFile = _cur.fileCaptures > _cur.rtCaptures || (_cur.fileCaptures > 0 && _cur.capturesDone % 2 == 0);
                if (useFile && _cur.fileCaptures > 0) DoFileCapture(_cur);
                else if (_cur.rtCaptures > 0) DoRtCapture(_cur);
                else DoFileCapture(_cur);
                _cur.capturesDone++;
                return;
            }

            if ((capsDone && spawnsDone) || windowOver)
            {
                // 억제 복구 후 종료 처리
                RestoreAll();
                FinalizeBurst(_cur);
                _cur = null;
                _nextAction = now + 0.3f;
            }
        }

        // ────────────────────────── 스폰 ──────────────────────────
        /// <summary>화면 중앙에 투영되는 지면 스폰점 — 중앙 레이 거리 샘플 → 지면 높이 → 역투영 오차 최소점 선택.
        ///   [79-R2 결함 교정] 구현은 레이 5m점의 XZ에 지면 Y를 치환해 스폰이 화면 밖(y≈3.0)으로 나갔다.</summary>
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

        void SpawnImpact(Burst b)
        {
            Vector3 p = SpawnPoint();
            float scale = (_seq % 2 == 0) ? 1f : 1.5f;
            _seq++;
            string mode = string.IsNullOrEmpty(b.fxMode) ? "ALL" : b.fxMode;

            // 비네트 펄스 — 베이지 재현 조건(웜화이트 쿼드×적색 틴트). 전 모드 발화.
            // [79-R5 교정] hold 1.8s — 캡처 창(0.45~2.0s) 전체에 비네트 활성 유지(구 0.43s는 캡처 시점에 이미 소멸).
            ScreenVignetteFX.Begin(_cam, new Color(0.9f, 0.12f, 0.1f), 0.22f, 0.05f, 1.8f, 0.5f);

            if (mode == "ALL" || mode == "Simbol" || mode == "Flash" || mode == "Shockwave" || mode == "Root")
            {
                Transform newInst = SpawnMagicHitTracked(p, scale);
                if (newInst != null && mode != "ALL")
                {
                    int kept = 0, off = 0;
                    foreach (var psr in newInst.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        bool isRoot = psr.transform == newInst.transform;
                        bool keep = mode == "Root" ? isRoot : (!isRoot && psr.transform.name == mode);
                        psr.enabled = keep;
                        if (keep) kept++; else off++;
                    }
                    Debug.Log("[79-ISOLATE] " + mode + " 유지 " + kept + " / OFF " + off);
                }
            }
            else if (mode == "ArcFlash")
            {
                FxSlashFlipbookFlash.Play(p, scale, false);
            }
            else if (mode == "OpaqueArcFlash")
            {
                var beforeArc = SnapshotIds("SlashArcFlash");
                FxSlashFlipbookFlash.Play(p, scale, false);
                Transform nf = FindNewByPrefix("SlashArcFlash", p, beforeArc);
                if (nf != null) ForceOpaque(nf); else Debug.LogWarning("[79-OPAQUE] SlashArcFlash 미식별");
            }
            else if (mode == "OpaqueMagicHit")
            {
                Transform inst = SpawnMagicHitTracked(p, scale);
                if (inst != null) ForceOpaque(inst);
            }
            else if (mode == "OpaqueBoth")
            {
                var beforeArc2 = SnapshotIds("SlashArcFlash");
                FxSlashFlipbookFlash.Play(p, scale, false);
                Transform nf = FindNewByPrefix("SlashArcFlash", p, beforeArc2);
                if (nf != null) ForceOpaque(nf);
                Transform inst = SpawnMagicHitTracked(p, scale);
                if (inst != null) ForceOpaque(inst);
            }
            else if (mode == "RealChain")
            {
                Vector3 dir = (p - _cam.transform.position).normalized;
                CombatFXGate.PlayHitFX(p, dir, CombatHitType.Organic, false, 12f, Color.white);
            }
            // VignetteOnly — FX 스폰 없음(비네트만)

            b.lastSpawnTime = Time.time;
            if (b.matchPrefix != null || b.utkHide) ApplySuppress(b);
            Vector3 vpx = _cam != null ? _cam.WorldToViewportPoint(p) : Vector3.zero;
            Debug.Log("[79-SPAWN] " + b.label + " #" + b.spawns + " mode=" + mode + " pos=" + p.ToString("F1")
                + " scale=" + scale.ToString("F1") + " vp=" + vpx.x.ToString("F2") + "," + vpx.y.ToString("F2"));
        }

        /// <summary>씬 자체 전투 차단 — AnimalAI GO + Guard/RecruitedSoldier 태그 GO 비활성(결함 근원을 프로브 스폰으로 한정).</summary>
        void DisableSceneCombat()
        {
            int ai = 0, guard = 0;
            foreach (var aiComp in Object.FindObjectsOfType<AnimalAI>())
            {
                if (aiComp != null && aiComp.gameObject.activeInHierarchy) { aiComp.gameObject.SetActive(false); ai++; }
            }
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (go.tag == "Guard" || go.tag == "RecruitedSoldier") { go.SetActive(false); guard++; }
            }
            Debug.Log("[79-COMBAT] 씬 전투 차단 — AnimalAI " + ai + "건, Guard태그 " + guard + "건 비활성");
        }

        /// <summary>MagicHit 스폰 + 신규 인스턴스 추적 — 스폰 전후 Transform ID 차집합.</summary>
        Transform SpawnMagicHitTracked(Vector3 p, float scale)
        {
            var before = new HashSet<int>();
            foreach (var t in Object.FindObjectsOfType<Transform>())
                if (t.name.StartsWith("ImpactVFX_")) before.Add(t.GetInstanceID());
            SlashVFXRunner.PlayImpactMulti(p, CombatHitType.Organic, scale);
            return FindNewByPrefix("ImpactVFX_", p, before);
        }

        Transform FindNewByPrefix(string prefix, Vector3 nearP)
        {
            return FindNewByPrefix(prefix, nearP, SnapshotIds(prefix));
        }

        Transform FindNewByPrefix(string prefix, Vector3 nearP, HashSet<int> before)
        {
            Transform found = null;
            float best = float.MaxValue;
            foreach (var t in Object.FindObjectsOfType<Transform>())
            {
                if (!t.name.StartsWith(prefix) || before.Contains(t.GetInstanceID())) continue;
                float d = Vector3.Distance(t.position, nearP);
                if (d < best) { best = d; found = t; }
            }
            return found;
        }

        HashSet<int> SnapshotIds(string namePrefix)
        {
            var s = new HashSet<int>();
            foreach (var t in Object.FindObjectsOfType<Transform>())
                if (t.name.StartsWith(namePrefix)) s.Add(t.GetInstanceID());
            return s;
        }

        /// <summary>머티리얼 불투명 강제 — surf=0+투명 키워드 OFF+Opaque 큐 (세션 의존 알파=1 가설의 배치 재현).</summary>
        void ForceOpaque(Transform root)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null) continue;
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = 2000;
                n++;
            }
            Debug.Log("[79-OPAQUE] '" + root.name + "' 불투명 강제 " + n + "건");
        }

        /// <summary>아틀라스 런타임 검증 — 빈 셀(6·7)과 셀 0 내용을 직독 (79-R1 구조 판독의 실행 확인).</summary>
        void VerifyAtlas()
        {
            try
            {
                var tex = FxFlipbookGenerator.GetSlashArcFlipbook();
                if (tex == null) { Debug.Log("[79-ATLAS] slashArc NULL"); return; }
                string info = "[79-ATLAS] slashArc " + tex.width + "x" + tex.height + " readable=" + tex.isReadable;
                if (tex.isReadable)
                {
                    int cw = tex.width / 4, ch = tex.height / 2;
                    Color32 c0 = tex.GetPixel(cw / 2, ch + ch / 2);          // 셀 0(좌상단) 중심
                    Color32 c6 = tex.GetPixel(2 * cw + cw / 2, ch / 2);      // 셀 6(하단 x2) 중심
                    Color32 c7 = tex.GetPixel(3 * cw + cw / 2, ch / 2);      // 셀 7(하단 x3) 중심
                    info += " cell0center=" + c0 + " cell6center=" + c6 + " cell7center=" + c7;
                }
                Debug.Log(info);
            }
            catch (System.Exception e) { Debug.LogWarning("[79-ATLAS] 검증 실패 " + e.Message); }
        }

        // ────────────────────────── 캡처 ──────────────────────────
        void DoFileCapture(Burst b)
        {
            string rel = "TestOutput/no79_f_" + b.label.Replace(":", "_") + "_" + _seq + "_" + b.capturesDone + ".png";
            float capT = Time.time;
            ScreenCapture.CaptureScreenshot(rel, 1);
            StartCoroutine(ReadFileNext(b, rel, capT));
        }

        System.Collections.IEnumerator ReadFileNext(Burst b, string rel, float capT)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", rel));
            for (int i = 0; i < 8; i++) yield return null;
            if (!File.Exists(full)) { Debug.LogWarning("[79-SCAN] 파일 미생성 " + rel); yield break; }
            byte[] bytes = File.ReadAllBytes(full);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(bytes)) { Debug.LogWarning("[79-SCAN] 로드 실패 " + rel); Object.Destroy(tex); yield break; }
            ScanAndLog(b, rel, tex, false, capT);
            Object.Destroy(tex);
        }

        void DoRtCapture(Burst b)
        {
            if (_rt == null || _cam == null) return;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_rt);
            StartCoroutine(ReadRtNext(b));
        }

        System.Collections.IEnumerator ReadRtNext(Burst b)
        {
            yield return null; yield return null;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            ScanAndLog(b, "RT", tex, true);
            Object.Destroy(tex);
        }

        // ────────────────────────── 스캔/판정 ──────────────────────────
        void ScanAndLog(Burst b, string tag, Texture2D tex, bool isRt, float capT = -1f)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            int beige = 0, black = 0, white = 0, mag = 0;
            int bx0 = -1, by0 = -1, bx1 = -1, by1 = -1;
            int kx0 = -1, ky0 = -1, kx1 = -1, ky1 = -1;
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
                    else if (r < 12 && g < 12 && bl < 12)
                    {
                        black++;
                        if (kx0 < 0) { kx0 = kx1 = x; ky0 = ky1 = y; }
                        else { if (x < kx0) kx0 = x; if (x > kx1) kx1 = x; if (y < ky0) ky0 = y; if (y > ky1) ky1 = y; }
                    }
                    else if (r > 240 && g < 30 && bl > 240) mag++;
                    else if (r > 252 && g > 252 && bl > 252) white++;
                }
            }
            string line = "[79-SCAN] " + b.label + " " + tag + " beige=" + beige + " black=" + black + " white=" + white + " mag=" + mag;
            if (beige > 0) line += " beigeBox=(" + bx0 + "," + by0 + ")-(" + bx1 + "," + by1 + ")";
            if (black > 0) line += " blackBox=(" + kx0 + "," + ky0 + ")-(" + kx1 + "," + ky1 + ")";
            if (capT > 0 && b.lastSpawnTime > 0)
            {
                float elapsed = capT - b.lastSpawnTime;
                int frame = Mathf.Clamp((int)(elapsed / 0.7f * 8f), 0, 7);
                line += " t=" + elapsed.ToString("F2") + " frame=" + frame;
            }
            Debug.Log(line);
            b.scanLines.Add(line);
            if (!isRt)
            {
                if (beige > b.maxBeige) b.maxBeige = beige;
                if (black > b.maxBlack) b.maxBlack = black;
            }
            else
            {
                if (beige > b.maxBeigeRt) b.maxBeigeRt = beige;
                if (black > b.maxBlackRt) b.maxBlackRt = black;
            }
            if (!b.dumped && (beige >= BeigeMinCount || black >= BlackMinCount))
            {
                b.dumped = true;
                DumpCulprits(b, tag, beige, black, bx0, by0, bx1, by1, w, h);
            }
        }

        /// <summary>클러스터 발견 시 근원 후보 덤프 — 클러스터 중심 레이 교차 + FX 유사 렌더러 상태.</summary>
        void DumpCulprits(Burst b, string tag, int beige, int black, int x0, int y0, int x1, int y1, int w, int h)
        {
            if (_dumpCount >= 8) { Debug.Log("[79-DUMP] 한도 초과 — 스킵"); return; }
            _dumpCount++;
            Debug.Log("[79-DUMP] === " + b.label + " " + tag + " beige=" + beige + " black=" + black + " ===");
            Vector3 spawn = SpawnPoint();
            int printed = 0;
            foreach (var pr in Object.FindObjectsOfType<ParticleSystemRenderer>())
            {
                if (pr == null || !pr.enabled || pr.gameObject.activeInHierarchy == false) continue;
                var tr = pr.transform;
                float d = Vector3.Distance(tr.position, spawn);
                if (d > 8f) continue;
                Material m = pr.sharedMaterial;
                var ps = pr.GetComponent<ParticleSystem>();
                int pcount = ps != null ? ps.particleCount : -1;
                float ss = -1f;
                try { if (ps != null) ss = ps.main.startSize.constant; } catch { }
                string texInfo = DescribeTexture(m);
                Vector3 vp = _cam != null ? _cam.WorldToViewportPoint(tr.position) : Vector3.zero;
                Bounds bd = pr.bounds;
                Debug.Log("[79-DUMP] PSR '" + tr.name + "' parent=" + (tr.parent != null ? tr.parent.name : "-")
                    + " d=" + d.ToString("F1") + " mat=" + (m != null ? m.name : "null")
                    + " tex=" + texInfo
                    + " vp=" + vp.x.ToString("F2") + "," + vp.y.ToString("F2")
                    + " bounds=" + bd.size.x.ToString("F2") + "x" + bd.size.y.ToString("F2")
                    + " pcount=" + pcount + " startSize=" + ss.ToString("F2")
                    + " " + MatState(m));
                printed++;
                if (printed > 16) break;
            }
            foreach (var r in Object.FindObjectsOfType<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var tr = r.transform;
                float d = Vector3.Distance(tr.position, spawn);
                if (d > 8f) continue;
                Material m = r.sharedMaterial;
                string texInfo = DescribeTexture(m);
                Vector3 vp = _cam != null ? _cam.WorldToViewportPoint(tr.position) : Vector3.zero;
                Bounds bd = r.bounds;
                Debug.Log("[79-DUMP] R '" + tr.name + "' parent=" + (tr.parent != null ? tr.parent.name : "-")
                    + " d=" + d.ToString("F1") + " mat=" + (m != null ? m.name : "null")
                    + " tex=" + texInfo
                    + " vp=" + vp.x.ToString("F2") + "," + vp.y.ToString("F2")
                    + " bounds=" + bd.size.x.ToString("F2") + "x" + bd.size.y.ToString("F2")
                    + " " + MatState(m));
                printed++;
                if (printed > 24) break;
            }
            // 카메라 자식(비네트/플래시 오버레이 계열)
            if (_cam != null)
            {
                int cc = 0;
                foreach (Transform t in _cam.transform)
                {
                    var rr = t.GetComponent<Renderer>();
                    if (rr == null) continue;
                    Material m = rr.sharedMaterial;
                    Debug.Log("[79-DUMP] CAMCHILD '" + t.name + "' mat=" + (m != null ? m.name : "null")
                        + " tex=" + DescribeTexture(m) + " enabled=" + rr.enabled + " scale=" + t.localScale.ToString("F2"));
                    cc++;
                    if (cc > 12) break;
                }
            }
        }

        /// <summary>텍스처 상세 판독 — 이름·크기·읽기 가능성·중심 픽셀(GPU 슬롯 내용 간접 확인).</summary>
        string DescribeTexture(Material m)
        {
            if (m == null) return "noMat";
            Texture t = m.mainTexture;
            if (t == null) return "NONE";
            var t2 = t as Texture2D;
            string info = t.name + " " + t.width + "x" + t.height;
            if (t2 != null)
            {
                info += " readable=" + t2.isReadable;
                try
                {
                    if (t2.isReadable)
                    {
                        Color32 c = t2.GetPixel(t2.width / 2, t2.height / 2);
                        info += " center=(" + c.r + "," + c.g + "," + c.b + "," + c.a + ")";
                    }
                }
                catch { info += " (readfail)"; }
            }
            return info;
        }

        /// <summary>머티리얼 투명 상태 — 키워드/서페이스/큐 (불투명 렌더 가설 직접 검증).</summary>
        string MatState(Material m)
        {
            if (m == null) return "matNull";
            string s = "surf=" + (m.HasProperty("_Surface") ? m.GetFloat("_Surface").ToString("F0") : "?")
                + " kw=" + (m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") ? "T" : "F")
                + " blend=" + (m.HasProperty("_Blend") ? m.GetFloat("_Blend").ToString("F0") : "?")
                + " q=" + m.renderQueue;
            return s;
        }

        // ────────────────────────── 억제 ──────────────────────────
        readonly List<Renderer> _offRenderers = new List<Renderer>();
        VisualElement _hiddenUtk;

        void ApplySuppress(Burst b)
        {
            RestoreAll();
            if (b.matchPrefix == "CAMCHILD")
            {
                if (_cam != null)
                    foreach (Transform t in _cam.transform)
                        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                            if (r.enabled) { r.enabled = false; _offRenderers.Add(r); }
                return;
            }
            if (b.utkHide)
            {
                var root = ProjectName.UI.Toolkit.UIToolkitBootstrap.UIRoot;
                if (root != null)
                {
                    VisualElement target = root.Q("NameplateOverlay");
                    if (target == null) target = root;
                    target.style.display = DisplayStyle.None;
                    _hiddenUtk = target;
                    Debug.Log("[79-SUP] UTK 숨김 — " + (target == root ? "UIRoot전체" : "NameplateOverlay"));
                }
                return;
            }
            string prefix = b.matchPrefix;
            foreach (var r in Object.FindObjectsOfType<Renderer>())
            {
                if (r == null) continue;
                string n = r.transform.name;
                string pn = r.transform.parent != null ? r.transform.parent.name : "";
                bool hit = b.exactMatch ? n == prefix : (n.Contains(prefix) || pn.Contains(prefix));
                if (hit)
                {
                    if (r.enabled) { r.enabled = false; _offRenderers.Add(r); }
                }
            }
            Debug.Log("[79-SUP] '" + prefix + "'" + (b.exactMatch ? "(exact)" : "") + " 억제 렌더러 " + _offRenderers.Count + "건");
        }

        void RestoreAll()
        {
            foreach (var r in _offRenderers) if (r != null) r.enabled = true;
            _offRenderers.Clear();
            if (_hiddenUtk != null) { _hiddenUtk.style.display = DisplayStyle.Flex; _hiddenUtk = null; }
        }

        // ────────────────────────── 마무리 ──────────────────────────
        void FinalizeBurst(Burst b)
        {
            string line = b.label + " fileMax beige=" + b.maxBeige + " black=" + b.maxBlack
                + " | rtMax beige=" + b.maxBeigeRt + " black=" + b.maxBlackRt
                + (b.dumped ? " [DUMPED]" : "");
            Debug.Log("[79-BURST] " + line);
            _summary.Add(line);
            foreach (var l in b.scanLines) _summary.Add("  " + l);
        }

        void RequestExit()
        {
            if (_mode == "exitreq") return;
            _mode = "exitreq";
            RestoreAll();
            WriteSummary();
            Debug.Log("[79-PROBE] 시퀀스 완료 — Play 종료 요청");
            // 판정 요약 로그
            foreach (var s in _summary) if (s.StartsWith("==") || s.StartsWith("BASE") || s.StartsWith("SUP") || s.StartsWith("LAYER") || s.StartsWith("scene"))
                    Debug.Log("[79-SUMMARY] " + s);
            EditorApplication.isPlaying = false;   // 편집모드 복귀 시 OnPlayModeChanged가 Exit(0)
        }

        void FailExit(string why)
        {
            _summary.Add("FAIL: " + why);
            WriteSummary();
            Debug.LogError("[79-PROBE] FAIL — " + why + " — Exit(1)");
            EditorApplication.Exit(1);
        }

        void WriteSummary()
        {
            try
            {
                string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Fx79BisectProbe.SummaryPath));
                File.WriteAllLines(full, _summary.ToArray());
                Debug.Log("[79-PROBE] 요약 기록 — " + full);
            }
            catch (System.Exception e) { Debug.LogError("[79-ERR] 요약 기록 실패 " + e.Message); }
        }
    }
}
