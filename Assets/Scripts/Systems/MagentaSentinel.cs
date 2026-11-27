using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 43-SENTINEL] 마젠타 렌더 상시 감시·자가 수리 런타임 센티널.
    /// 뿌리(실측): 공격씬 테스트 4(11-15 18:13)·화살 테스트 5 영상에서 순수 마젠타(#FF00FF 불투명, 픽셀 실측)
    /// 렌더가 사용자 환경에서만 재현 — 배치 3회 전면 재현(씬 로드+병사 스폰+GLB 재질 이식+강제 피격)은 전부
    /// 렌더러 25개 / 파손 0건. 정적 자산 전수(.mat 362 / 모델 1,264 / 씬·프리팹 1,850)도 결손 0.
    /// → 사용자 세션에서만 발생하는 런타임 셰이더 파손(InternalErrorShader/null 머티리얼 슬롯)으로 판정하고,
    ///    본 센티널이 Play 중 1.5s 주기로 전 렌더러를 감시한다:
    ///    ① 파손 즉시 계층 경로·머티리얼·셰이더명 LogWarning(근원 특정 1급 증거)
    ///    ② 즉시 자가 수리(URP/Lit 폴백 인스턴스 교체 — 42차 런타임 인스턴스 수리 계약 승계, 에셋 무변경)
    ///    ③ Projector·TrailRenderer 등 렌더러 외 경로도 감시(센티널 미발화 = 근원이 렌더러 외부 — 판정 정보)
    /// Random 0건(결정론 계약), Play 전용, DontDestroyOnLoad 단일 인스턴스.
    /// </summary>
    public static class MagentaSentinel
    {
        private const float SweepInterval = 1.5f;
        private const string ErrorShaderName = "Hidden/InternalErrorShader";

        // [무번호 79] SlashArcFlash 킬스위치 — 사용자 세션 F7 토글(1급 로그). 베이지 사각 근원 확정 진단:
        //   ON 시 FxSlashFlipbookFlash.Play 즉시 반환 → Play 중 베이지가 소멸하면 근원 확정.
        //   사용자 세션에서만 알파 강제 1(불투명)이 되는 세션 의존 렌더(66-P1 동계열)의 확정 관문.
        public static bool KillSlashArcFlash;

        // [무번호 79-R15] 아틀라스 마젠타 스왑 — F10 토글. 베이지 근원 렌더러 특정 실험:
        //   웜화이트 아틀라스 RGB를 마젠타로 교체(알파 보존) → 아크 본체와 얼리어싱 소비처가 전부 마젠타 렌더 →
        //   [79-FILE-SCAN] 마젠타 클러스터의 커버 렌더러 역산([79-MAG-COVER])이 근원을 이름으로 지목.
        public static bool AtlasMagentaSwap;

        /// <summary>[무번호 79-R15] 아틀라스 RGB 스왑 — 마젠타↔웜화이트(알파·형상 무변경, 판독 가능 텍스처 직접 재작성).</summary>
        public static void ApplyAtlasMagenta()
        {
            var atlas = FxFlipbookGenerator.GetSlashArcFlipbook();
            if (atlas == null) { Debug.LogWarning("[79-MAG] 아틀라스 미생성 — 먼저 아크 발화 필요"); return; }
            var px = atlas.GetPixels32();
            if (AtlasMagentaSwap)
            {
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32(255, 0, 255, px[i].a);   // RGB 마젠타, 알파(아크 형상) 보존
            }
            else
            {
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32(255, 244, 214, px[i].a);   // 원복 — 웜화이트
            }
            atlas.SetPixels32(px);
            atlas.Apply(false, false);
            Debug.LogWarning($"[79-MAG] 아틀라스 RGB {(AtlasMagentaSwap ? "마젠타" : "웜화이트")} 적용 — {atlas.width}x{atlas.height} ({px.Length}px)");
        }

        private static GameObject _host;
        private static float _nextSweep;
        private static readonly HashSet<int> _repaired = new HashSet<int>();

        // [무번호 44-SENTINEL] 감시 확장 상태 — 텍스처 프로브 캐시/예산, 캡처 쿨다운, VFX 인벤토리 1회.
        private static readonly HashSet<int> _textureProbed = new HashSet<int>();
        private static float _nextCapture;
        private static bool _vfxInventoryLogged;

        // [무번호 48-T3] 파티클 수리용 소프트 파티클 머티리얼 정적 캐시 — 수리마다 인스턴스 생성 금지(누출 금지).
        private static Material _particleFallback;

        // [무번호 51-a] 화면 픽셀 역매핑 스캔 버퍼 — 센티널이 볼 수 없는 경로(셰이더 정상+내용물 이상 등)까지
        //   화면에서 직접 탐지하고, 클러스터를 덮는 렌더러를 역산해 근원을 이름으로 보고하는 최종 관문.
        private static Camera _scanCam;
        private static RenderTexture _scanRt;
        private static Texture2D _scanTex;
        // [무번호 53] 스크린 스캔 자가 차단 플래그 — Camera.Render가 렌더그래프 예외로 반복 실패 시
        //   스윕마다 동일 에러 스팸(11-16 09:37 Play 실측 44건)을 막기 위해 1회 실패 후 스캔 비활성.
        private static bool _screenScanDisabled;
        // [무번호 64] 센서 진실성 — 11:44 실측(58_scan_114414.png = 57,597px ≥247 균일) 대비 같은 순간
        //   실화면 캡처(44_magenta_114414.png)는 흰 194px뿐 = 수동 cam.Render() 재렌더가 공백 프레임을
        //   생산해 58-P2의 "화면 99.98% 순백" 오판 + 무고 렌더러(ScorchMark) 지목을 낳았다.
        //   ①RenderRequest(풀 파이프라인 렌더 — 포스트 포함) 우선, 실패 시 cam.Render() 폴백
        //   ②공백(균일) 프레임 가드 — 균일 RT는 렌더 결함이 아니라 센서 자체 결함이므로 보고 스킵
        private static bool _renderRequestBroken;
        private static bool _blankRtLogged;
        private const int ScanW = 320;
        private const int ScanH = 180;
        private const int MagentaClusterPixels = 24;    // 320×180의 0.04% — 실측 마젠타 덩어리는 수백 px급
        // [무번호 54-P0] 700→150 — 공격씬 8 실측(10:07 Play): 화면 0.3~2.2% 흰 덩어리가 320×180 스캔에서
        //   150~400px급(960px 화면 기준 1,400~3,500px 환산)인데 700 임계는 전부 미달 → [51-SCREEN] 침묵.
        //   UI 노이즈(이름판·텍스트) 기저는 20~40px — 150은 노이즈 상단 여유를 두고 실측 덩어리를 커버.
        private const int WhiteClusterPixels = 150;     // 320×180의 0.26% — 불투명 흰 사각 덩어리 기준(54차 하향)

        // [무번호 51-b] 셰이더 컴파일 전수 스캔 1회 플래그 — GPU 마젠타(컴파일 실패)는 렌더러 셰이더명이
        //   정상으로 보여 기존 감지를 통과한다. 에디터 ShaderUtil 진실원을 전 로드 머티리얼에 적용한다.
        private static bool _shaderCompileSwept;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_host != null) return;
            _host = new GameObject("MagentaSentinelHost");
            Object.DontDestroyOnLoad(_host);
            _host.hideFlags = HideFlags.HideAndDontSave;
            _host.AddComponent<SentinelRunner>();
        }

        private sealed class SentinelRunner : MonoBehaviour
        {
            private void Update()
            {
                // [무번호 69-R5] 종료-안전 게이트 — ExitPlayMode 무한 대기("Application.ExitPlayMode
                //   Waiting for Unity's code to finish executing" — 영상 15 t=2.3 실측, 사용자 "자꾸 팅겨서
                //   꺼져" 보고)는 종료 진행 중 렌더 요청(스캔/UI 캡처/바이섹션)이 붕괴 중 파이프라인에
                //   진입하며 매달리는 패턴. 종료 신호 감지 시 모든 진단을 즉시 중단한다.
                if (_shuttingDown) return;
                // [무번호 69-R5] 중량 진단(게임뷰 캡처·바이섹션)은 기본 OFF — F9 토글(1급 로그).
                //   상시 가동이 종료 Hang·부하·크래시 위험을 만들었다. 평시 Play는 경량 티어만.
                if (Input.GetKeyDown(KeyCode.F9))
                {
                    _diagHeavy = !_diagHeavy;
                    Debug.LogWarning($"[69-DIAG] 중량 진단 모드 {_diagHeavy} 전환 (F9) — 게임뷰 UI 캡처+런타임 바이섹션");
                }
                // [무번호 79] SlashArcFlash 킬스위치 — F7 토글(1급 로그). 베이지 근원 확정 진단.
                //   [F8→F7 이동] F8은 실내 이동 토글(IndoorDebugEnterExit)과 충돌 — 사용자 확인 실패 실측(11-19)으로 재배치.
                if (Input.GetKeyDown(KeyCode.F7))
                {
                    MagentaSentinel.KillSlashArcFlash = !MagentaSentinel.KillSlashArcFlash;
                    Debug.LogWarning($"[79-KILL] SlashArcFlash 킬스위치 {MagentaSentinel.KillSlashArcFlash} 전환 (F7) — 베이지 근원 확정 진단");
                }
                // [무번호 79-R15] 아틀라스 마젠타 스왑 — F10 토글(1급 로그). 베이지 근원 렌더러 특정 실험.
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    MagentaSentinel.AtlasMagentaSwap = !MagentaSentinel.AtlasMagentaSwap;
                    MagentaSentinel.ApplyAtlasMagenta();
                    Debug.LogWarning($"[79-MAG] 아틀라스 마젠타 스왑 {MagentaSentinel.AtlasMagentaSwap} 전환 (F10) — 베이지 근원 특정 실험");
                }
                // [무번호 79] 파일 캡처 지연 스캔 — 캡처 예약이 있으면 3프레임 후 판독+근원 덤프.
                if (MagentaSentinel._fileScanPending != null)
                {
                    StartCoroutine(MagentaSentinel.FileCaptureScanRoutine(MagentaSentinel._fileScanPending));
                    MagentaSentinel._fileScanPending = null;
                }
                // [무번호 69-R1] 런타임 바이섹션 스텝 — 중량 진단 ON일 때만.
                if (_diagHeavy) BisectTick();
                // [무번호 69-R2] UI 포함 게임뷰 베이지 스캔 — 중량 진단 ON일 때만.
                if (_diagHeavy) UiBeigeProbeTick();
                // [무번호 64] 히트 동기 긴급 스캔 — 정기 스윕보다 우선 처리(+0.12s 피크 샘플링).
                if (_urgentScanPending && Time.unscaledTime >= _urgentScanAt)
                {
                    _urgentScanPending = false;
                    UrgentScan();
                }
                // [무번호 79] 히트 동기 베이지 캡처 — 결함 순간 파일 캡처(+0.12s) → 지연 판독.
                if (_beigeCapPending && Time.unscaledTime >= _beigeCapAt)
                {
                    _beigeCapPending = false;
                    BeigeCaptureTick();
                }
                if (Time.unscaledTime < _nextSweep) return;
                _nextSweep = Time.unscaledTime + SweepInterval;
                Sweep();
            }

            private void OnApplicationQuit() => ShutdownGate();
            private void OnDisable() => ShutdownGate();

            private static void ShutdownGate()
            {
                if (_shuttingDown) return;
                _shuttingDown = true;
                _uiCapturePending = false;
                _bisectKind = null;
                Debug.LogWarning("[69-DIAG] 종료 감지 — 진단 전면 게이트 오프(ExitPlayMode Hang 차단)");
            }
        }

        private static void Sweep()
        {
            // ① 렌더러 전수(비활성 포함 — 머티리얼 파손은 비활성 오브젝트에서도 기록된다)
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            bool anyBroken = false;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    bool isFallback = m != null && m.name == "MagentaSentinel_Fallback";
                    bool broken = m == null || m.shader == null || m.shader.name == ErrorShaderName;
                    if (!broken && !isFallback) continue;

                    // 로그 1회/렌더러 — 반복 스윕 스팸 방지(재수리도 스킵)
                    if (!_repaired.Add(r.GetInstanceID()))
                        continue;

                    LogBroken(r, m, i);
                    anyBroken = true;
                    if (Application.isPlaying && !isFallback)
                    {
                        // 42차 계약 승계 — 런타임 인스턴스 폴백 교체(에셋 파일 무변경, 누출 방지: 1회만 생성)
                        // [무번호 46-T3] 파티클 렌더러 수리 머티리얼 교체 — 기존 URP/Lit 불투명 웜그레이는
                        //   파티클 빌보드에서 불투명 흰 사각으로 렌더(11-15 45차 캡처 실측 — 마젠타를 흰 사각으로
                        //   바꿔놓는 2차 결함). 파티클은 검증 소프트 파티클(투명+연기 아틀라스)로 수리해 연기
                        //   형태를 유지한다. 정적 캐시 1회 생성(수리마다 인스턴스 누출 금지). 그 외 렌더러는
                        //   기존 웜그레이 유지(파손 자리 식별용 — 메시 계열엔 흰 사각 문제 없음).
                        Material fallback;
                        if (r is ParticleSystemRenderer)
                        {
                            if (_particleFallback == null)
                                _particleFallback = FxMaterialLibrary.CreateFlipbookSmoke();
                            fallback = _particleFallback;
                            if (fallback == null)
                                fallback = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MagentaSentinel_Fallback" };
                        }
                        else
                        {
                            fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                            {
                                name = "MagentaSentinel_Fallback"
                            };
                            fallback.color = new Color(0.85f, 0.82f, 0.75f, 1f);   // 웜 그레이 — 파손 자리 식별용
                        }
                        if (fallback != null)
                        {
                            mats[i] = fallback;
                            changed = true;
                        }
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }

            // [무번호 44-SENTINEL] 파손 감지 시 근원 시각 증거 캡처(쿨다운 내장).
            if (anyBroken) TryCaptureEvidence();

            // ② Projector(데칼 프로젝터) — 렌더러 외 마젠타 경로 감시(수리는 로그만 — 프로젝터 수리는 근원 확인 후)
            var projectors = Object.FindObjectsByType<Projector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Projector p in projectors)
            {
                if (p == null || p.material == null) continue;
                if (p.material.shader == null || p.material.shader.name == ErrorShaderName)
                    LogBroken(p.transform, p.material, -1);
            }

            // ②-b [무번호 52-c] URP DecalProjector 전수 감시 — 커맨드 링/혈흔/그을음 등 데칼의 렌더는
            //    Renderer가 아니라 DecalProjector 경로라 기존 감지를 완전히 통과했다(사용자 보고 "마젠타·
            //    흰 사각링"의 잔여 뿌리 — DecalSpawner의 URP/Lit 데칼은 투영 패스가 없어 불투명 사각 렌더).
            //    ① 머티리얼/셰이더 null·InternalErrorShader ② 텍스처 평균 마젠타/순백 검사(베이크 실패 감지).
            var decalProjectors = Object.FindObjectsByType<DecalProjector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var dp in decalProjectors)
            {
                if (dp == null) continue;
                var m = dp.material;
                if (m == null)
                {
                    Debug.LogWarning("[52-SENTINEL] ⚠️ 데칼 프로젝터 머티리얼 null — path=" +
                        (dp.transform != null ? dp.transform.name : "?"));
                    TryCaptureEvidence();
                    continue;
                }
                if (m.shader == null || m.shader.name == ErrorShaderName)
                {
                    Debug.LogWarning($"[52-SENTINEL] ⚠️ 데칼 프로젝터 파손 셰이더 — mat='{m.name}' shader='{(m.shader != null ? m.shader.name : "<null>")}'");
                    TryCaptureEvidence();
                    continue;
                }
                // 데칼 텍스처 내용물 검사 — 베이크 실패(순백/마젠타) 감지(수리 없음·보고 전용).
                if (!_textureProbed.Add(m.GetInstanceID())) continue;
                if (m.HasProperty("_BaseMap") || m.HasProperty("_MainTex"))
                {
                    var tex = m.mainTexture;
                    if (tex is Texture2D dt && dt.width > 0)
                    {
                        var avg = SampleTextureAverage(dt);
                        if (IsPureMagenta(avg) || (avg.r > 0.97f && avg.g > 0.97f && avg.b > 0.97f))
                            Debug.LogWarning($"[52-SENTINEL] ⚠️ 데칼 텍스처 이상 — mat='{m.name}' tex='{dt.name}' " +
                                $"avgRGB=({avg.r:F2},{avg.g:F2},{avg.b:F2}) — 베이크 실패 후보(보고 전용)");
                    }
                }
            }

            // ③ [무번호 44-SENTINEL] VFX Graph 인벤토리(세션 1회) — Renderer 계열 밖 경로 존재 여부를
            //    1회 기록해 "센티널 미발화인데 마젠타 보고" 시 VFX Graph 후보를 판별한다(수리 없음).
            if (!_vfxInventoryLogged)
            {
                _vfxInventoryLogged = true;
                VisualEffect[] vfxs = Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                Debug.Log($"[44-SENTINEL] VFX Graph 인벤토리 — 활성 VisualEffect {vfxs.Length}건" +
                    (vfxs.Length > 0 ? $" ({vfxs[0].name} 등)" : ""));
            }

            // ④ [무번호 44-SENTINEL] 텍스처 마젠타 프로브 — 셰이더는 정상인데 런타임 생성 텍스처(플립북 아틀라스 등)가
            //    순수 마젠타로 채워진 케이스 감지. 43차 "사용자 세션 한정" 판정의 미커버 영역(성장형 구름 덩어리 관측 —
            //    겹친 빌보드 파티클 형태 실측). 보고 전용(수리 없음 — 근원 확정 후 별도 수리). 신규 머티리얼만 1회, 스윕당 예산 8.
            int probeBudget = 8;
            foreach (Renderer r in renderers)
            {
                if (r == null || probeBudget <= 0) continue;
                Material[] mats = r != null ? r.sharedMaterials : null;
                if (mats == null) continue;
                foreach (Material m in mats)
                {
                    if (m == null || probeBudget <= 0) continue;
                    if (!_textureProbed.Add(m.GetInstanceID())) continue;
                    probeBudget--;
                    // [무번호 48-T3] 텍스처 프로퍼티 부재 셰이더 스킵 — mainTexture 게터가 "doesn't have a
                    //   texture property '_MainTex'" 스팸을 뿌린다(SelectionRing/TelegraphRing/FresnelRim 실측).
                    if (!m.HasProperty("_MainTex") && !m.HasProperty("_BaseMap")) continue;
                    Texture mainTex = m.mainTexture;
                    if (!(mainTex is Texture2D tex) || tex.width == 0 || tex.height == 0) continue;
                    Color avg = SampleTextureAverage(tex);
                    if (IsPureMagenta(avg))
                    {
                        LogBroken(r, m, -2);   // slot=-2 = 텍스처 마젠타 신호(머티리얼 슬롯 아님)
                        Debug.LogWarning($"[44-SENTINEL] 🟣 마젠타 텍스처 감지 — mat='{m.name}' tex='{tex.name}' " +
                            $"{tex.width}x{tex.height} avgRGB=({avg.r:F2},{avg.g:F2},{avg.b:F2}) — 셰이더 '{m.shader.name}'는 정상, 내용물 마젠타(수리 없음·보고 전용)");
                    }
                }
            }

            // [무번호 51-b] 셰이더 컴파일 전수 스캔(세션 1회) — GPU 마젠타(컴파일 실패)는 렌더러 셰이더명이
            //   정상으로 보여 기존 감지를 통과한다. 에디터 진실원(ShaderUtil.ShaderHasError) 적용.
#if UNITY_EDITOR
            SweepShaderCompileErrors();
#endif

            // [무번호 51-a] 화면 픽셀 역매핑 스캔 — 원인 불문 화면 이상 색(마젠타/불투명 흰 덩어리)을 화면에서
            //   직접 탐지하고, 클러스터를 덮는 렌더러를 역산해 근원을 이름으로 보고하는 최종 관문.
            ScreenScan();
        }

        // [무번호 51-b] 셰이더 컴파일 전수 스캔(세션 1회) — 컴파일 실패 셰이더는 GPU에서 마젠타로 렌더되지만
        //   렌더러 셰이더명이 정상이라 기존 감지를 통과한다. 에디터 진실원(ShaderUtil.ShaderHasError) 적용.
#if UNITY_EDITOR
        private static void SweepShaderCompileErrors()
        {
            if (_shaderCompileSwept) return;
            _shaderCompileSwept = true;
            var seen = new HashSet<string>();
            var allMats = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var m in allMats)
            {
                if (m == null) continue;
                var sh = m.shader;
                if (sh == null)
                {
                    Debug.LogWarning($"[51-SHADER] 셰이더 null 머티리얼 — {m.name}");
                    continue;
                }
                if (!seen.Add(sh.name)) continue;
                if (UnityEditor.ShaderUtil.ShaderHasError(sh))
                    Debug.LogWarning($"[51-SHADER] ⚠️ 컴파일 에러 셰이더 실측 — {sh.name} (예시 머티리얼: {m.name})");
            }
            Debug.Log($"[51-SHADER] 셰이더 컴파일 전수 스캔 완료 — 로드된 셰이더 {seen.Count}종(에러는 위 로그만)");
        }
#endif

        // [무번호 51-a] 화면 픽셀 역매핑 스캔 — 원인 불문 화면 이상 색(마젠타/불투명 흰 덩어리)을 화면에서
        // 직접 탐지하고, 클러스터를 덮는 렌더러를 역산해 근원을 이름으로 보고하는 최종 관문.
        private static void ScreenScan()
        {
            if (_screenScanDisabled) return;   // [무번호 53] 1회 실패 후 자가 차단 — 스팸 방지
            if (_scanCam == null) _scanCam = Camera.main;
            if (_scanCam == null) return;
            if (_scanRt == null)
            {
                // [무번호 53] 깊이 버퍼 24비트 필수 — URP RenderGraph는 Output Texture에 깊이 포맷을 요구한다
                //   ("the output Render Texture must have a depth buffer ... Depth Stencil Format must be set
                //   to a value other than None" — 11-16 Play 실측 경고). 0비트 RT였던 기존 생성은
                //   DBufferCopyDepthPass(데칼 프리패스)의 CreateRenderGraphTexture "no format" 예외를
                //   스윕마다 발생시켰고, 예외가 ReadPixels 이전에 흐름을 끊어 [51-SCREEN] 보고가 0건이었다.
                var desc = new RenderTextureDescriptor(ScanW, ScanH, RenderTextureFormat.ARGB32, 24);
                _scanRt = new RenderTexture(desc) { name = "MagentaSentinel_ScanRT" };
                _scanTex = new Texture2D(ScanW, ScanH, TextureFormat.RGBA32, false);
            }
            var prevActive = RenderTexture.active;
            var prevTarget = _scanCam.targetTexture;
            try
            {
                // [무번호 64] 풀 파이프라인 렌더 우선 — RenderRequest는 URP 전 경로(포스트·톤매핑 포함)로
                //   그려 공백 프레임/과노출(톤매핑 미적용) 두 결함을 동시에 제거한다. 미지원 환경은 1회
                //   전환 후 기존 수동 재렌더 폴백(동작 보장 — 기존 계약 승계).
                bool rendered = false;
                if (!_renderRequestBroken)
                {
                    try
                    {
                        // [64] StandardRequest는 Unity 6000.4 실측상 RenderPipeline의 중첩 타입
                        //    (dnfile 어셈블리 파싱 확정: enclosing=UnityEngine.Rendering.RenderPipeline,
                        //     필드=destination/mipLevel/face/slice — outputTexture는 6000.4에 없음)
                        var req = new RenderPipeline.StandardRequest { destination = _scanRt };
                        RenderPipeline.SubmitRenderRequest(_scanCam, req);
                        rendered = true;
                    }
                    catch (System.Exception)
                    {
                        _renderRequestBroken = true;   // [64] 1회 전환 — 재시도 스팸 금지(48차 교훈)
                    }
                }
                if (!rendered)
                {
                    _scanCam.targetTexture = _scanRt;
                    _scanCam.Render();   // 진단 전용 재렌더 — 1.5s 캐듯스(에디터 Play 한정 비용 허용)
                    _scanCam.targetTexture = prevTarget;
                }
                RenderTexture.active = _scanRt;
                _scanTex.ReadPixels(new Rect(0, 0, ScanW, ScanH), 0, 0);
                _scanTex.Apply(false);
            }
            catch (System.Exception ex)
            {
                // [무번호 53] 렌더그래프 예외 등 실패 시 — 1급 로그 1회 + 이후 스캔 자가 차단(에러 스팸 금지).
                _screenScanDisabled = true;
                _scanCam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                Debug.LogError($"[53-SCREEN] ⛔ 화면 픽셀 스캔 실패 — 이후 스윕 자가 차단(에러 스팸 방지): {ex.Message}");
                return;
            }
            RenderTexture.active = prevActive;

            var px = _scanTex.GetPixels32();
            // [무번호 64] 공백 프레임 가드 — 균일 프레임(한 색이 ~전부)은 렌더 결함 증거가 아니라
            //   센서 자체 결함(재렌더 실패/초기화 RT)이다. 보고 스킵 + 1회 로그로 센서 이상을 알린다.
            if (IsBlankFrame(px))
            {
                if (!_blankRtLogged)
                {
                    _blankRtLogged = true;
                    Debug.LogWarning("[64-SCAN] ⚠️ 스캔 RT 공백(균일) 프레임 — 클러스터 보고 스킵(센서 자체 결함, 무고 지목 방지)");
                }
                return;
            }
            int magCount = 0, whiteCount = 0, semiWhiteCount = 0, blackCount = 0, beigeCount = 0;
            int magX = 0, magY = 0, whiteX = 0, whiteY = 0, semiX = 0, semiY = 0, blackX = 0, blackY = 0, beigeX = 0, beigeY = 0;
            for (int y = 0; y < ScanH; y++)
            {
                for (int x = 0; x < ScanW; x++)
                {
                    var c = px[y * ScanW + x];
                    if (IsPureMagenta(new Color32(c.r, c.g, c.b, 255)))
                    {
                        magCount++; magX += x; magY += y;
                    }
                    else if (c.r >= 246 && c.g >= 246 && c.b >= 246)
                    {
                        whiteCount++; whiteX += x; whiteY += y;
                    }
                    // [무번호 58-P1] 준백색 티어 — 57차까지 임계(≥246)만 써서 230~245대 준백 덩어리를 놓쳤다
                    //   (11:30 세션 [51-SCREEN] 0건 + 사용자 육안 잔존 보고의 사각지대 후보). 크림 갑옷
                    //   (249,236,160)·창백청은 B채널 미달로 배제되는 전채널 안전 임계.
                    else if (c.r >= 232 && c.g >= 232 && c.b >= 232)
                    {
                        semiWhiteCount++; semiX += x; semiY += y;
                    }
                    // [무번호 58-P1] 검정 클러스터 감지기 신설 — 흰/마젠타만 스캔해 "검은 덩어리"는 완전 사각지대였다.
                    else if (c.r <= 14 && c.g <= 14 && c.b <= 14)
                    {
                        blackCount++; blackX += x; blackY += y;
                    }
                    // [무번호 69-R1] 베이지 티어 — 크림 슬라임 색 계열 솔리드 박스(250,201,176 실측)는 기존
                    //   4티어 전부 미달(흰 b≥246·준백 전채널≥232·검≤14 어디에도 없음) = 스캐너 사각지대였다.
                    //   크림 갑옷(249,236,160)은 g=236로 배제 — g≤222 상한으로 오경고 차단.
                    else if (c.r >= 232 && c.g >= 178 && c.g <= 222 && c.b >= 148 && c.b <= 205 && c.r > c.g && c.g > c.b)
                    {
                        beigeCount++; beigeX += x; beigeY += y;
                    }
                }
            }
            if (magCount >= MagentaClusterPixels)
            {
                var center = new Vector2((float)magX / magCount / ScanW, (float)magY / magCount / ScanH);
                Debug.LogWarning($"[51-SCREEN] 🟣 화면 마젠타 클러스터 실측 — {magCount}px 중심({center.x:F2},{center.y:F2}) " +
                    $"덮는 렌더러: {DescribeCoveringRenderers(center)} | 근접 FX(3s·8m): {DescribeNearbyFx(center)}");
                TryCaptureEvidence();
                TrySaveScanRt();
            }
            if (whiteCount >= WhiteClusterPixels)
            {
                var center = new Vector2((float)whiteX / whiteCount / ScanW, (float)whiteY / whiteCount / ScanH);
                Debug.LogWarning($"[51-SCREEN] ⬜ 화면 흰 클러스터 실측 — {whiteCount}px 중심({center.x:F2},{center.y:F2}) " +
                    $"덮는 렌더러: {DescribeCoveringRenderers(center)} | 근접 FX(3s·8m): {DescribeNearbyFx(center)}");
                TryCaptureEvidence();
                TrySaveScanRt();
            }
            // [무번호 58-P1] 준백색 — 순백 티어 미달 시에만 중복 보고 방지(whiteCount가 이미 임계 초과면 스킵).
            if (semiWhiteCount >= SemiWhiteClusterPixels && whiteCount < WhiteClusterPixels)
            {
                var center = new Vector2((float)semiX / semiWhiteCount / ScanW, (float)semiY / semiWhiteCount / ScanH);
                Debug.LogWarning($"[51-SCREEN] 🔲 화면 준백 클러스터 실측 — {semiWhiteCount}px 중심({center.x:F2},{center.y:F2}) " +
                    $"덮는 렌더러: {DescribeCoveringRenderers(center)} | 근접 FX(3s·8m): {DescribeNearbyFx(center)}");
                TryCaptureEvidence();
                TrySaveScanRt();   // [58-P1] 검출 시에만 저장 — 오버레이 UI 없는 스캔 RT 원본
            }
            if (blackCount >= BlackClusterPixels)
            {
                var center = new Vector2((float)blackX / blackCount / ScanW, (float)blackY / blackCount / ScanH);
                Debug.LogWarning($"[51-SCREEN] ⬛ 화면 검정 클러스터 실측 — {blackCount}px 중심({center.x:F2},{center.y:F2}) " +
                    $"덮는 렌더러: {DescribeCoveringRenderers(center)} | 근접 FX(3s·8m): {DescribeNearbyFx(center)}");
                TryCaptureEvidence();
                TrySaveScanRt();
                TryStartBisect("black", blackCount, center);   // [69-R1]
            }
            // [무번호 69-R1] 베이지 보고 + 런타임 바이섹션 — 사용자 세션 안에서 근원 렌더러를 이름으로 특정.
            if (beigeCount >= BeigeClusterPixels)
            {
                var center = new Vector2((float)beigeX / beigeCount / ScanW, (float)beigeY / beigeCount / ScanH);
                Debug.LogWarning($"[69-BEIGE] 🟤 화면 베이지 클러스터 실측 — {beigeCount}px 중심({center.x:F2},{center.y:F2}) " +
                    $"덮는 렌더러: {DescribeCoveringRenderers(center)} | 근접 FX(3s·8m): {DescribeNearbyFx(center)}");
                TryCaptureEvidence();
                TrySaveScanRt();
                TryStartBisect("beige", beigeCount, center);
            }
            // 흰/마젠타/준백 티어에도 바이섹션 개시(검/베이지와 동일 근원 특정 혜택).
            if (whiteCount >= WhiteClusterPixels)
                TryStartBisect("white", whiteCount, new Vector2((float)whiteX / whiteCount / ScanW, (float)whiteY / whiteCount / ScanH));
        }

        // [무번호 69-R1] 베이지 임계 — 슬라임 크림 박스 실측 8,945px(화면 720×444) → 스캔 스케일 환산
        //   ~1,800px. 300 = 잡음 상회·실측 하회 여유. 크림 갑옷(g=236)은 판별식 g≤222로 배제.
        private const int BeigeClusterPixels = 300;

        // ================================================================
        // [무번호 69-R1] 런타임 바이섹션 — 사용자 세션 안에서 결함 근원 렌더러 특정
        //   (66차 계약: 프로브가 재현 못 하는 결함은 센서 확장이 먼저. 배치 프로브 클린 +
        //   사용자 세션 잔존 = 세션 의존/미발화 경로 → 세션 안에서 직접 마검한다.)
        //   동작: 티어 검출 → 클러스터 중심 레이의 FX 후보(최대 12)를 1개씩 억제 → 다음 프레임
        //   스캔 RT 재측정 → 해당 티어 픽셀 총량 붕괴 여부로 근원 판정 → 복원. 프레임당 1스텝.
        // ================================================================
        private static string _bisectKind;                  // 진행 중 티어(null=유휨)
        private static int _bisectBaseline;
        private static int _bisectIdx;
        private static int _bisectPhase;                    // 0=억제 대기 1=측정 대기
        private static bool _bisectSaved;
        private static float _nextBisect;
        // [무번호 69-R5] 종료-안전 게이트 + 중량 진단 토글 — 상시 가동 진단(게임뷰 캡처·Update 내
        //   렌더 요청)이 ExitPlayMode Hang("Waiting for Unity's code to finish executing" 실측)과
        //   세션 불안정을 만들었다. 종료 감지 시 전면 게이트 오프, 중량 진단은 F9 토글(기본 OFF).
        private static bool _shuttingDown;
        private static bool _diagHeavy;
        private static readonly System.Collections.Generic.List<(Renderer r, string path, bool on)> _bisectList
            = new System.Collections.Generic.List<(Renderer, string, bool)>(16);

        private static void TryStartBisect(string kind, int count, Vector2 center)
        {
            if (!Application.isPlaying || _bisectKind != null) return;
            if (Time.unscaledTime < _nextBisect) return;             // 10s 쿨다운 — 스팸 금지
            if (count < 300) return;                                 // 미세 클러스터 스킵
            var cam = _scanCam != null ? _scanCam : Camera.main;
            if (cam == null) return;
            var ray = cam.ViewportPointToRay(new Vector3(center.x, center.y, 0f));
            _bisectList.Clear();
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in all)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                float tE, tX;
                if (!IntersectRayBounds(r.bounds, ray, out tE, out tX)) continue;
                if (tE > 25f) continue;
                bool isFx = r is ParticleSystemRenderer;
                var mf = r.GetComponent<MeshFilter>();
                if (!isFx && mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Quad") isFx = true;
                if (!isFx) continue;
                // [69-R7] 카메라 부착 오버레이 "후보 제외" 폐지 → 태그 부착 전환. 근거: R6 스코치
                //   차단 후에도 사용자 세션 베이지 잔존(13:36 세션 실측) — 의도 오버레이(비네트 계열
                //   카메라 자식 쿼드)의 별칭 텍스처 렌더가 진범일 수 있고, 제외 규칙은 식별자를
                //   장님이게 만들었다(스코치 오지목 2회 실측). 이제 [CAM-CHILD] 태그로 보고된다.
                bool isCamChild = r.transform.IsChildOf(cam.transform);   // 카메라 부착 스크린 오버레이 판정(계약테스트 핀 문자열 유지)
                _bisectList.Add((r, (isCamChild ? "[CAM-CHILD] " : "") + RendererPath(r.transform, 3), r.enabled));
                if (_bisectList.Count >= 12) break;
            }
            if (_bisectList.Count == 0) return;
            _bisectKind = kind;
            _bisectBaseline = count;
            _bisectIdx = 0;
            _bisectPhase = 0;
            _nextBisect = Time.unscaledTime + 10f;
            Debug.LogWarning($"[69-BISECT] 개시 — {kind} baseline={count} 후보 {_bisectList.Count}건");
        }

        /// <summary>SentinelRunner.Update에서 매 프레임 호출 — 억제/측정 교대 1스텝.</summary>
        private static void BisectTick()
        {
            if (_bisectKind == null) return;
            if (_bisectIdx >= _bisectList.Count)
            {
                Debug.LogWarning("[69-BISECT] DONE — 후보 전수 측정 완료, 상태 복원됨");
                _bisectKind = null;
                return;
            }
            var (r, path, on) = _bisectList[_bisectIdx];
            if (r == null)
            {
                Debug.LogWarning($"[69-BISECT] #{_bisectIdx} [{path}] — 소멸(수명 만료) 스킵");
                _bisectIdx++;
                return;
            }
            if (_bisectPhase == 0)
            {
                _bisectSaved = r.enabled;
                r.enabled = false;
                // [69-R2] beige-ui 티어 — 억제 상태의 UI 포함 캡처를 프레임 말에 요청(다음 스텝 판독).
                if (_bisectKind == "beige-ui")
                {
                    EnsureUiRt();
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(_uiRt);
                    _uiCapturePending = true;
                }
                _bisectPhase = 1;            // 다음 프레임(억제 렌더 반영 후) 측정
                return;
            }
            // 측정 스텝
            int px;
            if (_bisectKind == "beige-ui")
            {
                px = _uiCapturePending ? ReadBeigeUi() : -1;
                _uiCapturePending = false;
            }
            else
            {
                px = CountKindPixels(_bisectKind);
            }
            r.enabled = _bisectSaved;
            _bisectPhase = 0;
            _bisectIdx++;
            if (px < 0) { Debug.LogWarning($"[69-BISECT] #{_bisectIdx - 1} [{path}] — 측정 실패(스캔 예외)"); return; }
            string verdict = px <= _bisectBaseline * 0.15f ? " ← ★결함 근원" : (px < _bisectBaseline * 0.8f ? " ← 기여" : "");
            Debug.LogWarning($"[69-BISECT] #{_bisectIdx - 1} 억제 [{path}] → {_bisectKind}px={px} (baseline {_bisectBaseline}){verdict}");
            // [69-R4] 근원 판정 순간 — 범인 머티리얼/텍스처의 라이브 상태 덤프 + 증거 페어 저장.
            //   (R3 신선 텍스처로도 잔존한 사용자 세션 실측 대응 — 셰이더/슬롯/텍스처 내용의 진실원 확보.)
            if (verdict.Contains("★") && r != null)
            {
                DumpCulpritState(r, path);
                TrySaveScanRt();
                TrySaveUiEvidence();
            }
        }

        private static int CountKindPixels(string kind)
        {
            try
            {
                var prevActive = RenderTexture.active;
                var prevTarget = _scanCam.targetTexture;
                bool rendered = false;
                if (!_renderRequestBroken)
                {
                    try
                    {
                        var req = new RenderPipeline.StandardRequest { destination = _scanRt };
                        RenderPipeline.SubmitRenderRequest(_scanCam, req);
                        rendered = true;
                    }
                    catch (System.Exception) { _renderRequestBroken = true; }
                }
                if (!rendered)
                {
                    _scanCam.targetTexture = _scanRt;
                    _scanCam.Render();
                    _scanCam.targetTexture = prevTarget;
                }
                RenderTexture.active = _scanRt;
                _scanTex.ReadPixels(new Rect(0, 0, ScanW, ScanH), 0, 0);
                _scanTex.Apply(false);
                RenderTexture.active = prevActive;
                var px = _scanTex.GetPixels32();
                int cnt = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (kind == "beige")
                    {
                        if (c.r >= 232 && c.g >= 178 && c.g <= 222 && c.b >= 148 && c.b <= 205 && c.r > c.g && c.g > c.b) cnt++;
                    }
                    else if (kind == "white")
                    {
                        if (c.r >= 246 && c.g >= 246 && c.b >= 246) cnt++;
                    }
                    else if (kind == "black")
                    {
                        if (c.r <= 14 && c.g <= 14 && c.b <= 14) cnt++;
                    }
                    else if (kind == "darkred")
                    {
                        if (c.r < 100 && c.g < 60 && c.b < 60 && c.r >= c.g + 30) cnt++;
                    }
                    else
                    {
                        if (IsPureMagenta(new Color32(c.r, c.g, c.b, 255))) cnt++;
                    }
                }
                return cnt;
            }
            catch (System.Exception)
            {
                return -1;   // 스캔 예외 — 무음 스킵(스팸 금지, 53차 계약)
            }
        }

        private static bool IntersectRayBounds(Bounds b, Ray ray, out float tEnter, out float tExit)
        {
            tEnter = 0f; tExit = 0f;
            var o = ray.origin; var d = ray.direction; var mn = b.min; var mx = b.max;
            float tmin = float.NegativeInfinity, tmax = float.PositiveInfinity;
            for (int i = 0; i < 3; i++)
            {
                float ro = i == 0 ? o.x : i == 1 ? o.y : o.z;
                float rd = i == 0 ? d.x : i == 1 ? d.y : d.z;
                float lo = i == 0 ? mn.x : i == 1 ? mn.y : mn.z;
                float hi = i == 0 ? mx.x : i == 1 ? mx.y : mx.z;
                if (Mathf.Abs(rd) < 1e-8f)
                {
                    if (ro < lo || ro > hi) return false;
                    continue;
                }
                float t1 = (lo - ro) / rd, t2 = (hi - ro) / rd;
                if (t1 > t2) { var tmp = t1; t1 = t2; t2 = tmp; }
                tmin = Mathf.Max(tmin, t1); tmax = Mathf.Min(tmax, t2);
                if (tmin > tmax) return false;
            }
            tEnter = tmin < 0f ? 0f : tmin; tExit = tmax;
            return tmax >= Mathf.Max(0f, tmin);
        }

        private static string RendererPath(Transform tr, int maxDepth)
        {
            string path = tr.name;
            var p = tr.parent;
            int d = 0;
            while (p != null && d < maxDepth) { path = p.name + "/" + path; p = p.parent; d++; }
            return path;
        }

        // ================================================================
        // [무번호 69-R2] UI 포함 게임뷰 베이지 스캔 — 카메라 RT(월드 전용)에 안 잡히는
        //   스크린 레이어(UTK/IMGUI 오버레이) 결함 커버. CaptureScreenshotIntoRenderTexture는
        //   프레임 말 반영이므로 요청→다음 틱 판독 2단계 상태 기계. 실패 3회 시 자가 차단(53차 계약).
        // ================================================================
        private static RenderTexture _uiRt;
        private static Texture2D _uiTex;
        private static bool _uiCapturePending;
        private static bool _uiProbeDisabled;
        private static float _nextUiProbe;
        private static int _uiProbeFails;

        private static void UiBeigeProbeTick()
        {
            if (!Application.isPlaying || _uiProbeDisabled) return;
            if (_bisectKind != null) return;   // 바이섹션 진행 중 간섭 금지(UI RT 공유)
            try
            {
                if (_uiCapturePending)
                {
                    _uiCapturePending = false;
                    int cnt = ReadBeigeUi();
                    if (cnt >= 2000)
                    {
                        long sx = 0, sy = 0;
                        // 중심 재계산 — 판독 시점 픽셀 재순회 최소화를 위해 CountBeige와 통합된 값 사용
                        var c2 = LastBeigeUiCenter();
                        Debug.LogWarning($"[69-BEIGE-UI] 🟤 게임뷰(UI 포함) 베이지 클러스터 — {cnt}px 중심({c2.x:F2},{c2.y:F2}) rt={_uiRt.width}x{_uiRt.height}");
                        TrySaveUiEvidence();
                        TryStartBisect("beige-ui", cnt, c2);
                    }
                    return;
                }
                if (Time.unscaledTime < _nextUiProbe) return;
                _nextUiProbe = Time.unscaledTime + 0.5f;   // 0.5s 케이던스 — 짧은 박스 창 커버
                EnsureUiRt();
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_uiRt);
                _uiCapturePending = true;
            }
            catch (System.Exception ex)
            {
                _uiProbeFails++;
                if (_uiProbeFails >= 3)
                {
                    _uiProbeDisabled = true;
                    Debug.LogWarning($"[69-R2] UI 게임뷰 스캔 자가 차단(3회 실패): {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static void EnsureUiRt()
        {
            if (_uiRt != null) return;
            int w = Mathf.Clamp(Screen.width, 320, 1280);
            int h = Mathf.Clamp(w * Mathf.Max(1, Screen.height) / Mathf.Max(1, Screen.width), 180, 800);
            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0);
            _uiRt = new RenderTexture(desc) { name = "SentinelUiProbeRT" };
            _uiTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        }

        private static long _beigeSumX, _beigeSumY;
        private static int _beigeUiCount;

        /// <summary>UI RT 판독 + 베이지 카운트(중심 합산 동시 수행).</summary>
        private static int ReadBeigeUi()
        {
            var prevActive = RenderTexture.active;
            RenderTexture.active = _uiRt;
            _uiTex.ReadPixels(new Rect(0, 0, _uiRt.width, _uiRt.height), 0, 0);
            _uiTex.Apply(false);
            RenderTexture.active = prevActive;
            var px = _uiTex.GetPixels32();
            int w = _uiRt.width, h = _uiRt.height;
            int cnt = 0;
            _beigeSumX = 0; _beigeSumY = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    if (c.r >= 232 && c.g >= 178 && c.g <= 222 && c.b >= 148 && c.b <= 205 && c.r > c.g && c.g > c.b)
                    { cnt++; _beigeSumX += x; _beigeSumY += y; }
                }
            }
            _beigeUiCount = cnt;
            return cnt;
        }

        private static Vector2 LastBeigeUiCenter()
        {
            int cnt = Mathf.Max(1, _beigeUiCount);
            return new Vector2((float)_beigeSumX / cnt / _uiRt.width, (float)_beigeSumY / cnt / _uiRt.height);
        }

        private static void TrySaveUiEvidence()
        {
            try
            {
                var png = _uiTex.EncodeToPNG();
                var path = $"Screenshots/69_uibeige_{System.DateTime.Now:HHmmss}.png";
                System.IO.File.WriteAllBytes(path, png);
                Debug.LogWarning($"[69-R2] UI 캡처 저장: {path}");
            }
            catch (System.Exception) { }
        }

        /// <summary>[무번호 69-R4] 근원 렌더러의 머티리얼/텍스처 라이브 상태 덤프 — 세션 의존 렌더의
        ///   진실원. R3 신선 텍스처로도 잔존한 사용자 세션 베이지 박스(Decal_ScorchMark 쿼드 —
        ///   런타임 바이섹션 9,819→40px 실측)의 셰이더/슬롯/텍스처 내용을 판정 순간에 확정한다.</summary>
        private static void DumpCulpritState(Renderer r, string path)
        {
            try
            {
                var sb = new System.Text.StringBuilder($"[69-CULPRIT] ★ {path}");
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { sb.Append("\n  mat=null"); continue; }
                    string shader = m.shader != null ? m.shader.name : "null";
                    string col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString("F2") : (m.HasProperty("_Color") ? m.GetColor("_Color").ToString("F2") : "n/a");
                    var t = m.mainTexture;
                    string texInfo = "null";
                    if (t != null)
                    {
                        string dim = t is Texture2D ? ((Texture2D)t).width + "x" + ((Texture2D)t).height : "RT";
                        texInfo = t.name + " " + dim;
                        var t2d = t as Texture2D;
                        if (t2d != null && t2d.isReadable)
                        {
                            try
                            {
                                var cpx = t2d.GetPixels32();
                                int n = cpx.Length; long sr = 0, sg = 0, sbl = 0, sa = 0;
                                for (int i = 0; i < n; i++) { sr += cpx[i].r; sg += cpx[i].g; sbl += cpx[i].b; sa += cpx[i].a; }
                                string avg = $"({sr / n},{sg / n},{sbl / n},{sa / n})";
                                texInfo += " avg=" + avg;
                            }
                            catch (System.Exception) { texInfo += " avg=unreadable"; }
                        }
                        else texInfo += " (not-readable)";
                    }
                    string surf = m.HasProperty("_Surface") ? m.GetFloat("_Surface").ToString("F1") : "-";
                    string blend = m.HasProperty("_Blend") ? m.GetFloat("_Blend").ToString("F1") : "-";
                    string kw = string.Join(",", m.shaderKeywords);
                    sb.Append($"\n  mat[{m.name}] shader={shader} q={m.renderQueue} col={col} surf={surf} blend={blend} tex={texInfo}");
                    sb.Append($"\n    keywords=[{kw}]");
                }
                Debug.LogWarning(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[69-CULPRIT] 덤프 실패: {ex.Message}");
            }
        }

        // [무번호 58-P1] 준백/검정 임계 — 준백=순백 미달(230~245) 덩어리 커버, 검=완흑(≤14) 250px.
        //   검 임계 근거: 세계 스페이스 HP바(이름판) ≈ 250~350px — 이상 결함 덩어리(수백~천px)와 구분 위해
        //   400으로 상향하고, 검출 시 DescribeNearbyFx가 명확화한다(오경고는 진단 보고일 뿐 무해).
        private const int SemiWhiteClusterPixels = 250;
        private const int BlackClusterPixels = 400;

        /// <summary>[무번호 58-P1] 스캔 RT(320×180 — 오버레이 UI 미포함)를 PNG로 저장 — 화면 캡처(UI 포함)와
        ///   별개로 센티널이 본 원본 픽셀을 남겨 오경고/오판 검증 근거로 삼는다(10s 캡처 쿨다운 공유).</summary>
        private static float _nextScanSave;

        private static void TrySaveScanRt()
        {
            if (!Application.isPlaying || Time.unscaledTime < _nextScanSave) return;
            _nextScanSave = Time.unscaledTime + 10f;   // [58-P1] 스캔 RT 별도 쿨다운(스크린 캡처와 분리)
            try
            {
                var copy = new Texture2D(ScanW, ScanH, TextureFormat.RGBA32, false);
                copy.SetPixels32(_scanTex.GetPixels32());
                copy.Apply(false);
                System.IO.File.WriteAllBytes(
                    $"Screenshots/58_scan_{System.DateTime.Now:HHmmss}.png", ImageConversion.EncodeToPNG(copy));
                Object.Destroy(copy);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[58-SCAN] 스캔 RT 저장 실패(진단 무영향): {e.Message}");
            }
        }

        /// <summary>[무번호 64] 균일 프레임 판정 — 8픽셀 스트라이드 샘플링 후 최다 색(8비트 양자화 버킷) 비율이
        ///   98% 이상이면 공백 RT로 판정한다. 11:44 실측(57,597/57,600 = 99.98% 균일) 재발 차단용.</summary>
        private static bool IsBlankFrame(Color32[] px)
        {
            var counts = new System.Collections.Generic.Dictionary<int, int>(64);
            int total = 0;
            for (int i = 0; i < px.Length; i += 8)
            {
                var c = px[i];
                int key = (c.r >> 3 << 10) | (c.g >> 3 << 5) | (c.b >> 3);
                counts.TryGetValue(key, out var n);
                counts[key] = n + 1;
                total++;
            }
            int top = 0;
            foreach (var kv in counts.Values) if (kv > top) top = kv;
            return total > 0 && top >= total * 98 / 100;
        }

        // ── [무번호 57-P2] FX 스폰 레지스트리 — 일시 FX(수명 < 스캔 주기 1.5s)는 역산 시점에 이미 소멸해
        // "해당 렌더러 없음"으로 빠진다(공격씬 9·10 실측 4/7건). 스폰 순간 (kind, pos, time)을 링 버퍼에
        // 기록하고, 클러스터 검출 시 레이캐스트 지점 반경 내 최근 FX를 나열해 일시 근원도 이름으로 특정한다.
        private struct FxSpawnEntry { public float Time; public string Kind; public Vector3 Pos; }
        private static readonly FxSpawnEntry[] _fxRing = new FxSpawnEntry[32];
        private static int _fxCursor;

        // [무번호 64] 히트 동기 긴급 스캔 — 단명 FX(수명 0.1~0.7s)는 1.5s 스캔 주기 사이에 소멸해
        //   스캔이 영원히 본다(샘플링 사각). FX 스폰 등록 시 +0.12s(피크 가시점) 1회 긴급 스캔을 예약해
        //   스폰 사건마다 화면을 실측한다. 보고 임계·쿨다운은 기존 계약 승계.
        internal static bool _urgentScanPending;
        internal static float _urgentScanAt;

        // [무번호 79] 히트 동기 베이지 파일 캡처 상태 — 2.5s 쿨다운(히트 스팸 방지).
        internal static bool _beigeCapPending;
        internal static float _beigeCapAt;
        private static float _nextBeigeCap;

        /// <summary>[무번호 79] 히트 동기 베이지 캡처 — ScreenCapture 파일 표준(결함 순간 포착) → [79-FILE-SCAN] 판독.</summary>
        private static void BeigeCaptureTick()
        {
            if (!Application.isPlaying || Time.unscaledTime < _nextBeigeCap) return;
            _nextBeigeCap = Time.unscaledTime + 2.5f;
            string fileName = $"Screenshots/79_beige_{System.DateTime.Now:HHmmss}.png";
            ScreenCapture.CaptureScreenshot(fileName);
            _fileScanPending = fileName;
            Debug.LogWarning($"[79-BEIGE-CAP] 히트 동기 캡처 예약 — {fileName} (+0.12s 결함 창)");
        }

        /// <summary>[무번호 64] 긴급 스캔 1회 실행 — 스폰 피크 시점 화면 실측 + UI 포함 캡처 페어 저장.</summary>
        internal static void UrgentScan()
        {
            if (!Application.isPlaying) return;
            ScreenScan();
            TryCaptureEvidence();   // [64] UI 포함 실화면 캡처 — RT(순월드) vs 화면(UI 포함) 페어 판정 근거
        }

        /// <summary>[무번호 57-P2] FX 스폰 등록 — 각 FX 스폰 경로(아크/링/스파크/임팩트/데스)에서 1행 호출.
        ///   [무번호 64] 등록 시 +0.12s 긴급 스캔을 예약한다(단명 FX 샘플링 사각 해소).</summary>
        internal static void RegisterFxSource(string kind, Vector3 pos)
        {
            _fxRing[_fxCursor % _fxRing.Length] = new FxSpawnEntry { Time = Time.time, Kind = kind, Pos = pos };
            _fxCursor++;
            if (Application.isPlaying)
            {
                // [무번호 79] 히트 동기 베이지 파일 캡처 — 결함 순간(스폰+0.12s)을 파일 표준으로 캡처·판독·근원 덤프.
                //   RT 경로 블라인드(실측) + TryCaptureEvidence 10s 쿨다운은 결함 순간을 놓침(11-19 세션 실측).
                //   [79-CULPRIT]이 결함 순간 머티리얼/스트림 상태를 이름으로 고정한다.
                _urgentScanPending = true;
                _urgentScanAt = Time.unscaledTime + 0.12f;
                _beigeCapPending = true;
                _beigeCapAt = Time.unscaledTime + 0.12f;
            }
        }

        /// <summary>[무번호 57-P2] 클러스터 중심(u,v)을 스캔 카메라 레이캐스트로 세계좌표화하고, 3초 내·8m 반경의
        /// 최근 FX 스폰을 거리순 최대 5건 나열 — 일시 FX(이미 소멸)의 근원 특정용.</summary>
        private static string DescribeNearbyFx(Vector2 uv01)
        {
            var cam = _scanCam;
            if (cam == null) return "카메라 없음";
            var ray = cam.ViewportPointToRay(new Vector3(uv01.x, uv01.y, 0f));
            Vector3 point = ray.GetPoint(10f);
            if (Physics.Raycast(ray, out var hit, 80f, ~0, UnityEngine.QueryTriggerInteraction.Ignore))
                point = hit.point;
            float now = Time.time;
            var found = new System.Collections.Generic.List<(float d, string s)>();
            for (int i = 0; i < _fxRing.Length; i++)
            {
                var e = _fxRing[i];
                if (e.Time <= 0f || now - e.Time > 3f) continue;
                float d = (e.Pos - point).sqrMagnitude;
                if (d > 64f) continue;   // 8m
                found.Add((d, $"{e.Kind}@{e.Time:F1}s({(Mathf.Sqrt(d)):F1}m)"));
            }
            if (found.Count == 0) return "3초 내 근접 FX 없음";
            found.Sort((a, b) => a.d.CompareTo(b.d));
            var top = new System.Collections.Generic.List<string>();
            for (int i = 0; i < found.Count && i < 5; i++) top.Add(found[i].s);
            return string.Join(", ", top);
        }

        /// <summary>[무번호 51-a] 화면 점(u,v 0~1)을 덮는 렌더러 역산 — 렌더러 바운드 8코너를 화면 AABB로
        /// 투영해 점 포함 여부 판정. 후보 최대 4건(path|type|mat|shader). 화면 이상 색의 근원을 이름으로 특정.</summary>
        private static string DescribeCoveringRenderers(Vector2 uv01)
        {
            var cam = _scanCam ?? Camera.main;   // [무번호 79] 결함 덤프 경로에서도 동작(_scanCam 미초기화 대비)
            if (cam == null) return "카메라 없음";
            var cands = new List<string>();
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.isVisible) continue;
                var b = r.bounds;
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                bool anyInFront = false;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + new Vector3(
                        (i & 1) == 0 ? -b.extents.x : b.extents.x,
                        (i & 2) == 0 ? -b.extents.y : b.extents.y,
                        (i & 4) == 0 ? -b.extents.z : b.extents.z);
                    var vp = cam.WorldToViewportPoint(corner);
                    if (vp.z <= 0) continue;
                    anyInFront = true;
                    if (vp.x < minX) minX = vp.x;
                    if (vp.x > maxX) maxX = vp.x;
                    if (vp.y < minY) minY = vp.y;
                    if (vp.y > maxY) maxY = vp.y;
                }
                if (!anyInFront) continue;
                if (uv01.x >= minX && uv01.x <= maxX && uv01.y >= minY && uv01.y <= maxY)
                {
                    string path = r.name;
                    var t = r.transform;
                    while (t != null && t.parent != null) { t = t.parent; path = t.name + "/" + path; }
                    var m = r.sharedMaterial;
                    cands.Add($"{path}[{r.GetType().Name}|{(m != null ? m.name : "null")}|{(m != null && m.shader != null ? m.shader.name : "null")}]");
                    if (cands.Count >= 4) break;
                }
            }
            // [무번호 52-c] 데칼 프로젝터 후보 추가 — Renderer 탐색 밖 경로(커맨드 링/혈흔/그을음 데칼) 커버.
            CollectDecalCandidates(uv01, cam, cands);
            return cands.Count > 0 ? string.Join(", ", cands) : "해당 렌더러 없음(데칼/UI/후처리/스카이박스 경로)";
        }

        /// <summary>[무번호 52-c] 데칼 프로젝터 역산 — Renderer 탐색에서 누락되는 URP DecalProjector를
        /// 화면 후보에 포함한다(위 DescribeCoveringRenderers의 후보 0건 보완). 데칼 박스 8코너 투영 판정.</summary>
        private static void CollectDecalCandidates(Vector2 uv01, Camera cam, List<string> cands)
        {
            if (cam == null || cands.Count >= 4) return;
            var dps = Object.FindObjectsByType<DecalProjector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var dp in dps)
            {
                if (dp == null || !dp.enabled || !dp.gameObject.activeInHierarchy) continue;
                var t = dp.transform;
                var center = t.position + t.rotation * new Vector3(0f, 0f, dp.pivot.z - 0.5f) * dp.size.z;
                var half = dp.size * 0.5f;
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                bool anyInFront = false;
                for (int i = 0; i < 8; i++)
                {
                    var corner = center + t.rotation * new Vector3(
                        (i & 1) == 0 ? -half.x : half.x,
                        (i & 2) == 0 ? -half.y : half.y,
                        (i & 4) == 0 ? -half.z : half.z);
                    var vp = cam.WorldToViewportPoint(corner);
                    if (vp.z <= 0) continue;
                    anyInFront = true;
                    if (vp.x < minX) minX = vp.x;
                    if (vp.x > maxX) maxX = vp.x;
                    if (vp.y < minY) minY = vp.y;
                    if (vp.y > maxY) maxY = vp.y;
                }
                if (!anyInFront) continue;
                if (uv01.x >= minX && uv01.x <= maxX && uv01.y >= minY && uv01.y <= maxY)
                {
                    string path = dp.name;
                    var walk = t;
                    while (walk != null && walk.parent != null) { walk = walk.parent; path = walk.name + "/" + path; }
                    var m = dp.material;
                    cands.Add($"{path}[DecalProjector|{(m != null ? m.name : "null")}|{(m != null && m.shader != null ? m.shader.name : "null")}]");
                    if (cands.Count >= 4) break;
                }
            }
        }

        /// <summary>[무번호 44-SENTINEL] 순수 마젠타 판정 — InternalErrorShader/마젠타 채움 텍스처 공통 임계.
        /// 딥 퍼플 FX(0.45,0.25,0.85 — 41-b 의도 색)와 화이트/녹색을 오탐 없이 배제하는 엄격 임계.</summary>
        public static bool IsPureMagenta(Color c)
        {
            return c.r > 0.85f && c.b > 0.85f && c.g < 0.15f;
        }

        /// <summary>[무번호 44-SENTINEL] 텍스처 5점 샘플 평균(중앙+4구석 근방) — 비가독 텍스처는 무시(예산 소진 없음).</summary>
        private static Color SampleTextureAverage(Texture2D tex)
        {
            try
            {
                Color sum = Color.clear;
                int w = tex.width, h = tex.height;
                int[] xs = { w / 2, w / 4, (3 * w) / 4, w / 4, (3 * w) / 4 };
                int[] ys = { h / 2, h / 4, h / 4, (3 * h) / 4, (3 * h) / 4 };
                for (int i = 0; i < 5; i++) sum += tex.GetPixel(xs[i], ys[i]);
                return sum / 5f;
            }
            catch (UnityException)
            {
                return Color.clear;   // 비가독 텍스처 — 판정 불가(조용히 스킵)
            }
        }

        /// <summary>[무번호 44-SENTINEL] 감지 직후 화면 캡처 — 근원 시각 증거. 세션 최초 1회 후 10s 쿨다운.</summary>
        private static void TryCaptureEvidence()
        {
            if (!Application.isPlaying || Time.unscaledTime < _nextCapture) return;
            _nextCapture = Time.unscaledTime + 10f;
            string fileName = $"Screenshots/44_magenta_{System.DateTime.Now:HHmmss}.png";
            ScreenCapture.CaptureScreenshot(fileName);
            _fileScanPending = fileName;
            Debug.Log($"[44-SENTINEL] 📸 화면 캡처 예약 — {fileName}(프레임 말 기록)");
        }

        // [무번호 79] 파일 캡처 지연 스캔 — RT 경로가 결함을 보지 못하는 실측(R1 배치·사용자 세션 [69-BEIGE-UI] 0건
        // while 파일 캡처 4.4k~22.9k beige) 대응: ScreenCapture.CaptureScreenshot 파일을 3프레임 후 판독해
        // 베이지/블랙 클러스터를 파일 표준으로 산정하고, 결함 시 근원(화면 매칭 렌더러·머티리얼 투명 상태)을 1급 덤프.
        internal static string _fileScanPending;

        internal static System.Collections.IEnumerator FileCaptureScanRoutine(string rel)
        {
            for (int i = 0; i < 3; i++) yield return null;
            try
            {
                if (!System.IO.File.Exists(rel)) { Debug.LogWarning("[79-FILE-SCAN] 파일 미생성 " + rel); yield break; }
                var tex = new Texture2D(2, 2);
                if (!tex.LoadImage(System.IO.File.ReadAllBytes(rel))) { Object.Destroy(tex); yield break; }
                var px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                int bc = 0; long bsx = 0, bsy = 0;
                int kc = 0; long kx = 0, ky = 0;
                int mc = 0; long mx = 0, my = 0;
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[row + x];
                        if (c.r >= 232 && c.g >= 178 && c.g <= 222 && c.b >= 148 && c.b <= 205 && c.r > c.g && c.g > c.b)
                        { bc++; bsx += x; bsy += y; }
                        else if (c.r <= 18 && c.g <= 18 && c.b <= 18) { kc++; kx += x; ky += y; }
                        else if (c.r > 240 && c.g < 30 && c.b > 240) { mc++; mx += x; my += y; }
                    }
                }
                float bcx = bc > 0 ? (float)bsx / bc / w : 0f;
                float bcy = bc > 0 ? 1f - (float)bsy / bc / h : 0f;
                float mcx = mc > 0 ? (float)mx / mc / w : 0f;
                float mcy = mc > 0 ? 1f - (float)my / mc / h : 0f;
                Debug.LogWarning($"[79-FILE-SCAN] {rel} beige={bc} black={kc} mag={mc} beigeVP=({bcx:F2},{bcy:F2}) magVP=({mcx:F2},{mcy:F2}) size={w}x{h}");
                // [무번호 79-R15] 마젠타 클러스터 — 아틀라스 스왑 실험의 근원 렌더러 역산(이름 지목).
                if (mc >= 1500)
                {
                    string magCover = DescribeCoveringRenderers(new Vector2(mcx, mcy));
                    Debug.LogWarning($"[79-MAG-COVER] magVP=({mcx:F2},{mcy:F2}) 마젠타 커버 렌더러: {magCover}");
                    DumpCulpritsAtCluster(mcx, mcy);
                }
                if (bc >= 2000) DumpCulpritsAtCluster(bcx, bcy);
                Object.Destroy(tex);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[79-FILE-SCAN] 판독 실패 " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>[무번호 79] 결함 순간 근원 덤프 — 클러스터 중심 화면 좌표와 겹치는 렌더러의 머티리얼 투명 상태 직독.
        ///   사용자 세션에서만 알파 강제 1(66-P1 동계열)이면 surf/kw/blend가 렌더와 어긋나는 순간을 이 덤프가 고정한다.</summary>
        private static void DumpCulpritsAtCluster(float cx, float cy)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            // [무번호 79-R12] 매칭 반경 0.35→1.2vp — 카메라 이동으로 캡처 시점과 덤프 시점 위치가 어긋나
            //   결함 순간 근원을 놓친 실측(11:56 세션 4건 전부 '없음') 교정. 거리순 정렬.
            var psrs = Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var matched = new List<(float err, ParticleSystemRenderer pr, Vector3 vp)>();
            foreach (var pr in psrs)
            {
                if (pr == null || !pr.enabled) continue;
                var v = cam.WorldToViewportPoint(pr.transform.position);
                if (v.z < 0) continue;
                float err = Mathf.Abs(v.x - cx) + Mathf.Abs(v.y - cy);
                if (err > 1.2f) continue;
                matched.Add((err, pr, v));
            }
            matched.Sort((a, b) => a.err.CompareTo(b.err));
            int dumped = 0;
            foreach (var (err, pr, v) in matched)
            {
                if (dumped >= 8) break;
                var m = pr.sharedMaterial;
                string surf = m != null && m.HasProperty("_Surface") ? m.GetFloat("_Surface").ToString("F0") : "?";
                string kw = m != null && m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") ? "T" : "F";
                string blend = m != null && m.HasProperty("_Blend") ? m.GetFloat("_Blend").ToString("F0") : "?";
                string srcb = m != null && m.HasProperty("_SrcBlend") ? m.GetFloat("_SrcBlend").ToString("F0") : "?";
                string dstb = m != null && m.HasProperty("_DstBlend") ? m.GetFloat("_DstBlend").ToString("F0") : "?";
                Texture t = m != null ? m.mainTexture : null;
                string tex = t == null ? "NONE" : t.name + " " + t.width + "x" + t.height;
                var ps = pr.GetComponent<ParticleSystem>();
                int pc = ps != null ? ps.particleCount : -1;
                // [무번호 79] 버텍스 스트림 순서 — 69-P2 UV 붕괴(스트림 어긋남 → TEXCOORD0 미바인딩 → UV 상수화 →
                //   솔리드 박스)와 동일 증상의 사용자 세션 상태 고정. 정상=[Position, Normal, Color, UV, AnimFrame].
                var streamBuf = new List<ParticleSystemVertexStream>();
                pr.GetActiveVertexStreams(streamBuf);
                var sb2 = new System.Text.StringBuilder();
                for (int i = 0; i < streamBuf.Count; i++) sb2.Append(streamBuf[i]).Append(i < streamBuf.Count - 1 ? ">" : "");
                var tr = pr.transform;
                string parent = tr.parent != null ? tr.parent.name : "-";
                Debug.LogWarning($"[79-CULPRIT] PSR '{tr.name}' parent={parent} vp=({v.x:F2},{v.y:F2}) err={err:F2}"
                    + $" mat={(m != null ? m.name : "null")}#{(m != null ? m.GetInstanceID().ToString() : "?")}"
                    + $" tex={tex} surf={surf} kw={kw} blend={blend}"
                    + $" src={srcb} dst={dstb} q={(m != null ? m.renderQueue : -1)}"
                    + $" pc={pc} streams={sb2}");
                dumped++;
            }
            // [무번호 79] SlashArcFlash 전용 스윕 — 근원 확정(F7) 대상의 결함 순간 상태 전수 기록.
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (tr == null || !tr.name.StartsWith("SlashArcFlash")) continue;
                var ps = tr.GetComponent<ParticleSystem>();
                var pr2 = tr.GetComponent<ParticleSystemRenderer>();
                var m2 = pr2 != null ? pr2.sharedMaterial : null;
                var v2 = cam.WorldToViewportPoint(tr.position);
                Debug.LogWarning($"[79-ARC] '{tr.name}' vp=({v2.x:F2},{v2.y:F2}) pc={(ps != null ? ps.particleCount : -1)}"
                    + $" psTime={(ps != null ? ps.time.ToString("F2") : "?")}"
                    + $" psrEnabled={(pr2 != null ? pr2.enabled.ToString() : "?")}"
                    + $" visible={(pr2 != null ? pr2.isVisible.ToString() : "?")}"
                    + $" mat={(m2 != null ? m2.name : "null")}#{(m2 != null ? m2.GetInstanceID().ToString() : "?")}"
                    + $" matNull={(m2 == null ? "Y" : "N")} src={_slashArcMatId}");
            }
            // [무번호 79-R12] 커버 렌더러 역산 — PSR 외 근원(메시 쿼드/데칼 프로젝터/카메라 자식)까지 전수 매칭.
            string cover = DescribeCoveringRenderers(new Vector2(cx, cy));
            Debug.LogWarning($"[79-COVER] beigeVP=({cx:F2},{cy:F2}) 커버 렌더러: {cover}");
            // [무번호 79-R13] 아틀라스 바인딩 전수 — 결함 순간 웜화이트 아틀라스를 바인딩 중인 렌더러 전수 기록.
            //   아크 본체 외에 아틀라스를 쥔 렌더러(얼리어싱 근원)가 있으면 이 줄이 이름으로 지목한다.
            var atlas = FxFlipbookGenerator.GetSlashArcFlipbook();
            if (atlas != null)
            {
                int atlasId = atlas.GetInstanceID();
                int bound = 0;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    var mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (var mm in mats)
                    {
                        if (mm == null || mm.mainTexture == null) continue;
                        if (mm.mainTexture.GetInstanceID() != atlasId) continue;
                        var tp = r.transform;
                        string path = tp.name;
                        while (tp != null && tp.parent != null) { tp = tp.parent; path = tp.name + "/" + path; }
                        Debug.LogWarning($"[79-ATLASBIND] '{r.name}' path={path} type={r.GetType().Name}"
                            + $" mat={mm.name} shader={(mm.shader != null ? mm.shader.name : "null")}"
                            + $" enabled={r.enabled} visible={r.isVisible}");
                        bound++;
                        break;
                    }
                    if (bound >= 10) break;
                }
                if (bound == 0) Debug.LogWarning("[79-ATLASBIND] 아틀라스 바인딩 렌더러 0건(바인딩 해제 상태)");
            }
            if (dumped == 0)
                Debug.LogWarning($"[79-CULPRIT] 1.2vp 내 PSR 없음 — 근원이 PSR 외부 또는 카메라 이동 누적 편차. beigeVP=({cx:F2},{cy:F2})");
        }

        private static int _slashArcMatId
        {
            get
            {
                var m = FxSlashFlipbookFlash.DebugCachedMat;
                return m != null ? m.GetInstanceID() : 0;
            }
        }

        private static void LogBroken(Object source, Material m, int slot)
        {
            var sb = new StringBuilder("[43-SENTINEL] ⚠️ 마젠타 렌더 감지·자가 수리 — ");
            Transform t = source is Component c ? c.transform : (source as Transform);
            string path = t != null ? t.name : source.name;
            while (t != null && t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            // [무번호 44-SENTINEL] 렌더러 종류 동반 기록 — Trail/Line/Particle/Skinned 등 종별 근원 판별.
            string typeLabel = source is Renderer rr ? rr.GetType().Name : source.GetType().Name;
            sb.Append($"type={typeLabel} path={path} slot={slot} mat='{(m == null ? "<null>" : m.name)}'");
            sb.Append($" shader='{(m == null || m.shader == null ? "<null>" : m.shader.name)}'");
            sb.Append($" repaired={_repaired.Count + 1}");
            Debug.LogWarning(sb.ToString());
        }
    }
}
