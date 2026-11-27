using System.Collections;
using System.Linq;
using ProjectName.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호 16-A] 전투 FX 실카메라 검증 프로브 — Phase A(A1~A4) 근거 수집.
    /// 배경: 무번호 5 프리미엄 FX(텔레그래프 셰이더/패링 링/화이트 프레임)가 진단 전용 카메라에서만
    ///   검증됐고 실게임 팔로우 카메라에서는 미가시/녹색 깨짐(사용자 영상 13s/25s 판독 — "AttackPremiumProbe
    ///   자체 카메라 vs 실카메라 조건 불일치"가 체감 "수정 없음"의 뿌리).
    /// 검증: ①대조 컷(무FX 지형 — "녹색 띠" 근원 판별) ②텔레그래프 링(fill 0.33/0.83) ③패링 창 개방 청색 링(D1)
    ///   ④PERFECT 판정 FX(금색 2겹) ⑤명령 링(CommandMarker) — 전부 실게임 카메라(Tag MainCamera) RT 캡처.
    /// 덤프: 카메라 전수(pos/euler/fov/near/far/cullingMask/clearFlags) + FX GO(layer/renderer/mat/shader/scale)
    ///   + Shader.Find 결과 + 렌더 파이프라인명 — A1/A2 근거.
    /// 계약: -quit/-nographics 금지(Play 모드 배치 — Cave156 선례), 결정론(Random 0건), StepErrors>0 → Exit(1).
    /// 실행: bash run_cfx_probe.sh (ENTRY=ProjectName.EditorTools.CombatFxRealCamProbe.RunAll).
    /// </summary>
    public static class CombatFxRealCamProbe
    {
        private const string Tag = "[CFX프로브]";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity";
        private const string SessionKey = "cfx_probe_active";
        private const int RTW = 1024;
        private const int RTH = 768;
        private const float SetupTimeoutSec = 25f;
        private const float OverallTimeoutSec = 180f;

        internal static int StepErrors;

        private enum Phase { WaitingPlay, WaitingSetup, Running, Finished }
        private static Phase _phase = Phase.WaitingPlay;
        private static float _runStartRealtime = -1f;
        private static bool _pumpSubscribed;
        private static CombatFxRunner _runner;

        /// <summary>배치 런 진입점 — 씬 오픈 → Play 진입 요청(펌프/런너가 이어받는다).</summary>
        public static void RunAll()
        {
            Debug.Log($"{Tag} === 전투 FX 실카메라 검증 프로브 시작 ===");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetBool(SessionKey, true);
                EditorApplication.isPlaying = true;
                SubscribePump();
                Debug.Log($"{Tag} Play 진입 요청 — {ScenePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{Tag} FAIL setup — 씬 오픈/Play 진입 실패");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
        }

        private static void SubscribePump()
        {
            if (_pumpSubscribed) return;
            _pumpSubscribed = true;
            EditorApplication.update += Pump;
        }

        internal static void ResubscribeAfterReload()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            _runStartRealtime = (float)EditorApplication.timeSinceStartup;
            SubscribePump();
            Debug.Log($"{Tag} 펌프 재구독 완료 — 도메인 리로드 후 이어받기");
        }

        private static void Pump()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (_runStartRealtime < 0f) _runStartRealtime = (float)EditorApplication.timeSinceStartup;
                float now = (float)EditorApplication.timeSinceStartup;
                if (now - _runStartRealtime > OverallTimeoutSec)
                {
                    FailFatal($"전체 감시 초과({OverallTimeoutSec}s) — 무한 대기 차단");
                    return;
                }
                switch (_phase)
                {
                    case Phase.WaitingPlay:
                        if (!EditorApplication.isPlaying) return;
                        _phase = Phase.WaitingSetup;
                        _runStartRealtime = (float)EditorApplication.timeSinceStartup;
                        Debug.Log($"{Tag} Play 진입 확인 — 러너 기동 대기");
                        break;

                    case Phase.WaitingSetup:
                        var player = GameObject.FindGameObjectWithTag("Player");
                        var pc = player != null ? player.GetComponent<PlayerCombat>() : null;
                        if (player == null || pc == null)
                        {
                            if (now - _runStartRealtime > SetupTimeoutSec)
                                FailFatal($"셋업 타임아웃({SetupTimeoutSec}s) — Player/PlayerCombat 미발견");
                            return;
                        }
                        var host = new GameObject("CombatFxProbeRunner");
                        _runner = host.AddComponent<CombatFxRunner>();
                        _phase = Phase.Running;
                        Debug.Log($"{Tag} 러너 기동 — FX 상태 순차 검증 시작");
                        break;

                    case Phase.Running:
                        break;   // 러너 코루틴이 Finish를 호출한다

                    case Phase.Finished:
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                FailFatal($"펌프 예외: {e.GetType().Name}");
            }
        }

        internal static void Finish(int errors)
        {
            if (_phase == Phase.Finished) return;
            _phase = Phase.Finished;
            Debug.Log($"{Tag} FINISH — StepErrors={errors}");
            SessionState.SetBool(SessionKey, false);
            _pumpSubscribed = false;
            EditorApplication.update -= Pump;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(errors > 0 ? 1 : 0);
        }

        private static void FailFatal(string reason)
        {
            Debug.LogError($"{Tag} FAIL — {reason}");
            StepErrors++;
            Finish(StepErrors);
        }
    }

    /// <summary>실카메라 캡처 러너 — FX 상태별 RT 캡처 + 상태 덤프(코루틴 순차).</summary>
    public class CombatFxRunner : MonoBehaviour
    {
        private const string Tag = "[CFX프로브]";
        private const int RTW = 1024;
        private const int RTH = 768;
        private int _shot;
        private Camera _probeCam;   // [무번호 16-A 폴백] 실카메라가 매 프레임 리셋될 때 CopyFrom 복제 카메라

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);   // 셋업/스폰 안착 정착

            // [무번호 16-A 수리] 몬스터 AI 비활성 — 프로브 중 플레이어 사망→afterlife 전환 방지
            //   (1차 런 실측: 어그로 1체→사망→AfterlifeSurvivalSystem이 EnterBuilding("afterlife") → 세계 교체).
            var ais = Object.FindObjectsByType<AnimalAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int disabled = 0;
            foreach (var m in ais)
                if (m != null && m.enabled) { m.enabled = false; disabled++; }
            Debug.Log($"{Tag} 몬스터 AI 비활성 — {disabled}체(사망→이계 전환 차단)");

            // [무번호 16-A 수리] Editor 어셈블리는 UI 미참조 — 실내 상태는 활성 씬명으로 판정(IndoorSceneTransition.IsIndoor 우회).
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "IndoorScene")
            {
                Debug.LogError($"{Tag} FAIL — 부트 직후 실내 상태(이계 전환 잔존) — 재런 필요");
                CombatFxRealCamProbe.StepErrors++;
                CombatFxRealCamProbe.Finish(CombatFxRealCamProbe.StepErrors);
                yield break;
            }

            Camera cam = FindRealCamera();
            DumpCameras();
            DumpPipeline();
            if (cam == null)
            {
                Debug.LogError($"{Tag} FAIL — 실카메라(Tag MainCamera) 미발견");
                CombatFxRealCamProbe.StepErrors++;
                CombatFxRealCamProbe.Finish(CombatFxRealCamProbe.StepErrors);
                yield break;
            }
            Debug.Log($"{Tag} 실카메라 확정 — name={cam.name} pos={cam.transform.position:F2} euler={cam.transform.eulerAngles:F1} " +
                      $"fov={cam.fieldOfView:F1} near={cam.nearClipPlane:F2} cullingMask=0x{cam.cullingMask:X8} ortho={cam.orthographic}");

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            var pc = player != null ? player.GetComponent<PlayerCombat>() : null;
            if (pc == null)
            {
                Debug.LogError($"{Tag} FAIL — PlayerCombat 미발견");
                CombatFxRealCamProbe.StepErrors++;
                CombatFxRealCamProbe.Finish(CombatFxRealCamProbe.StepErrors);
                yield break;
            }
            Vector3 pPos = player.transform.position;
            Debug.Log($"{Tag} 플레이어 pos={pPos:F2} forward={player.transform.forward:F2}");

            // [무번호 16-A 수리] 팔로우 정착 대기 — 카메라가 원점 고정이면 FX가 시야 밖(지면 스트립 뷰)이 된다.
            //   3런 실측: 전 컷 pxhash 동일 = 카메라 미이동 지지. 정착 확인 후 캡처한다(최대 6s).
            float settleStart = Time.realtimeSinceStartup;
            while ((cam.transform.position - Vector3.zero).sqrMagnitude < 0.01f
                   && Time.realtimeSinceStartup - settleStart < 6f)
                yield return null;
            Debug.Log($"{Tag} 캡처 전 카메라 실측 pos={cam.transform.position:F2} euler={cam.transform.eulerAngles:F1} " +
                      $"정착={(cam.transform.position != Vector3.zero ? "OK" : "FAIL(원점 고정)")}");
            if (cam.transform.position == Vector3.zero)
            {
                // [무번호 16-A 수리] 프로브 부트 순서에서만 PM 참조 불일치(실게임은 정상 추적 — 사용자 영상 근거).
                //   PM 단일기록자 공식을 그대로 적용해 실카메라 지오메트리를 확정한다(57° 오빗, PM 1605행 1:1).
                //   PM 캐시 참조 vs 실카메라 인스턴스 대조 로그 — 실버그 여부 판별 근거.
                var pmFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var pmCamField = typeof(PlayerMovement).GetField("_cameraTransform", pmFlags);
                var pmCam = pmCamField != null ? pmCamField.GetValue(pc.GetComponent<PlayerMovement>()) as Transform : null;
                int pmId = pmCam != null ? pmCam.GetInstanceID() : -1;
                Debug.Log($"{Tag} 참조 대조 PM._cameraTransform id={pmId} vs 실카메라 id={cam.GetInstanceID()} " +
                          $"동일={pmId == cam.GetInstanceID()}");
                const float PM_PITCH = 57.6f, PM_DIST = 12.2f;
                Vector3 pmCamPos = pPos + new Vector3(0f, 1.4f, 0f)
                                 + Quaternion.Euler(PM_PITCH, 0f, 0f) * new Vector3(0f, 0f, -PM_DIST);
                cam.transform.position = pmCamPos;
                cam.transform.rotation = Quaternion.LookRotation(pPos + new Vector3(0f, 1.2f, 0f) - pmCamPos);
                Debug.Log($"{Tag} 프로브 카메라 PM 공식 적용 pos={cam.transform.position:F2} euler={cam.transform.eulerAngles:F1}");

                // [무번호 16-A] PM 캐시 카메라 생존/상세 + 실카메라 컴포넌트 전수 + 5프레임 리셋 추적
                Debug.Log($"{Tag} PMCAM 상세 name={(pmCam != null ? pmCam.name : "NULL")} alive={pmCam != null} " +
                          $"active={(pmCam != null ? pmCam.gameObject.activeInHierarchy.ToString() : "-")} pos={(pmCam != null ? pmCam.position.ToString("F2") : "-")}");
                var comps = string.Join(",", cam.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name));
                Debug.Log($"{Tag} 실카메라GO 컴포넌트: {comps}");
                for (int i = 0; i < 5; i++)
                {
                    yield return null;
                    Debug.Log($"{Tag} 프레임추적#{i} cam.pos={cam.transform.position:F2}");
                }
                if (cam.transform.position == Vector3.zero)
                {
                    // [무번호 16-A 폴백] 매 프레임 리셋 자가 존재 — 실설정 복제 카메라로 실카메라 뷰 대체 캡처.
                    //   CopyFrom=URP 설정(포스트프로세싱/볼륨/마스크) 1:1 복제 + PM 공식 자세 — 실게임 뷰 등가.
                    var pcGO = new GameObject("CFXProbeCam");
                    var pcCam = pcGO.AddComponent<Camera>();
                    pcCam.CopyFrom(cam);
                    pcCam.transform.position = pmCamPos;
                    pcCam.transform.rotation = Quaternion.LookRotation(pPos + new Vector3(0f, 1.2f, 0f) - pmCamPos);
                    _probeCam = pcCam;
                    Debug.Log($"{Tag} 폴백 복제 카메라 기동 — CopyFrom({cam.name}) pos={pcCam.transform.position:F2}");
                }
                if (_probeCam != null) cam = _probeCam;   // 이후 전 컷 폴백 카메라로 캡처
            }

            // ── 컷 1: 대조(무FX 지형 — "녹색 띠" 근원 판별용) ──
            _shot = 1;
            yield return Capture(cam, "cfx_01_control_terrain", "대조 무FX 지형");
            yield return null;

            // ── 컷 2/3: 텔레그래프 링(fill 0.33 / 0.83) — 실카메라 가시성 + 절차 링 품질 ──
            _shot = 2;
            var tele = AttackTelegraphIndicator.Spawn(pPos + player.transform.forward * 2.2f, 2.2f, 1.2f);
            if (tele == null)
            {
                Debug.LogError($"{Tag} FAIL — 텔레그래프 스폰 실패");
                CombatFxRealCamProbe.StepErrors++;
            }
            else
            {
                DumpFxObject(tele, "telegraph");
                yield return new WaitForSeconds(0.4f);   // fill≈0.33
                _shot = 3;
                yield return Capture(cam, "cfx_03_telegraph_early", "텔레그래프 fill 0.33");
                yield return new WaitForSeconds(0.5f);   // fill≈0.83
                _shot = 4;
                yield return Capture(cam, "cfx_04_telegraph_late", "텔레그래프 fill 0.83");
                Destroy(tele);   // 다음 컷 오염 방지(종료 플래시 스킵 — 프로브 제어)
            }
            yield return null;

            // ── 컷 4: 패링 창 개방 청색 링(D1) — TryStartParry 리플렉션 발화 ──
            _shot = 5;
            var parryMi = typeof(PlayerCombat).GetMethod("TryStartParry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool opened = parryMi != null && (bool)parryMi.Invoke(pc, null);
            Debug.Log($"{Tag} TryStartParry 결과={opened}");
            if (!opened)
            {
                Debug.LogError($"{Tag} FAIL — 패링 창 개방 실패(PointerOverUI/기존 창)");
                CombatFxRealCamProbe.StepErrors++;
            }
            yield return new WaitForSeconds(0.08f);   // 청색 링 스폰 프레임 확보
            _shot = 6;
            yield return Capture(cam, "cfx_06_parry_open_ring", "패링 창 개방 청색 링");
            yield return new WaitForSeconds(0.5f);    // 창 경과(페이드) 소멸 대기

            // ── 컷 5: PERFECT 판정 FX — TryParryImpactFX 리플렉션 발화 ──
            _shot = 7;
            var impactMi = typeof(PlayerCombat).GetMethod("TryParryImpactFX",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gradeType = impactMi.GetParameters()[0].ParameterType;
            object perfect = System.Enum.Parse(gradeType, "PERFECT", true);   // [수리] 대소문자 무시 — 실측 멤버명 "Perfect"
            Vector3 kbDir = player.transform.forward;
            impactMi.Invoke(pc, new[] { perfect, kbDir });
            Debug.Log($"{Tag} TryParryImpactFX(PERFECT) 발화");
            // [무번호 19-D2 진단] 화이트 프레임(ParryWhiteFrame) 쿼드 상태 덤프 — 플래시 미노출 원인 계량
            foreach (var fw in cam.GetComponentsInChildren<Transform>(true))
            {
                if (fw.name != "ParryWhiteFrame") continue;
                var mr = fw.GetComponent<Renderer>();
                var fm = mr != null ? mr.sharedMaterial : null;
                var fcol = fm != null && fm.HasProperty("_Color") ? fm.GetColor("_Color") : new Color(0f, 0f, 0f, 0f);
                var bcol = fm != null && fm.HasProperty("_BaseColor") ? fm.GetColor("_BaseColor") : new Color(0f, 0f, 0f, 0f);
                Debug.Log($"{Tag} [D2] ParryWhiteFrame pos={fw.transform.position:F3} local={fw.transform.localPosition:F3} " +
                          $"scale={fw.transform.localScale:F3} localRot={fw.transform.localRotation.eulerAngles:F1} " +
                          $"activeSelf={fw.gameObject.activeSelf} activeInHierarchy={fw.gameObject.activeInHierarchy} " +
                          $"camNear={cam.nearClipPlane:F2} camFar={cam.farClipPlane:F1} " +
                          $"shader={(fm != null && fm.shader != null ? fm.shader.name : "NULL")} " +
                          $"_Color={fcol} _BaseColor={bcol} enabled={(mr != null ? mr.enabled.ToString() : "NO_REN")}");
            }
            // [무번호 19-G1] 화이트 프레임 실카메라 직접 캡처 — D2 실측 수리(플래시 쿼드=실카메라 부모 정상 부착,
            //   폴백 복제 카메라 뷰 밖 = 판정 절차 문제로 확정). A3 계약 — FX가 렌더되는 실카메라 자체 뷰로 판정.
            {
                Camera flashCam = null;
                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    foreach (Transform child in c.transform)
                        if (child.name == "ParryWhiteFrame") { flashCam = c; break; }
                    if (flashCam != null) break;
                }
                if (flashCam == null)
                {
                    Debug.LogError($"{Tag} FAIL — [G1] ParryWhiteFrame 부모 실카메라 미발견");
                    CombatFxRealCamProbe.StepErrors++;
                }
                else
                {
                    Debug.Log($"{Tag} [G1] 실카메라 직접 캡처 — {flashCam.name} pos={flashCam.transform.position:F2}");
                    // [무번호 19-G1 2차] 0.07s 수명 < 캡처 지연 실측(cfx_17 = +1프레임 시점 이미 소멸 — 판독 결론).
                    //   발화 동프레임 직접 렌더 요청 — 수명 내 실측(SubmitRenderRequest 동기 렌더 — 기존 Capture 파이프라인
                    //   이 submit 직후 ReadPixels 성공 실측과 동일 수단). 미지원 환경은 SKIP 로그(구조적 한계 기록).
                    try
                    {
                        var rt17 = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
                        var req17 = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt17 };
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(flashCam, req17);
                        RenderTexture.active = rt17;
                        var tex17 = new Texture2D(1024, 768, TextureFormat.RGB24, false);
                        tex17.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                        tex17.Apply();
                        var png17 = tex17.EncodeToPNG();
                        System.IO.File.WriteAllBytes("C:/Unity/code/TestOutput/cfx_17b_whiteframe_sameframe.png", png17);
                        using (var md5 = System.Security.Cryptography.MD5.Create())
                        {
                            var hash = md5.ComputeHash(png17);
                            var sb = new System.Text.StringBuilder();
                            for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2"));
                            Debug.Log($"{Tag} ===CAPTURE=== shot=18 cfx_17b_whiteframe_sameframe — [무번호 19-G1] 화이트 프레임 동프레임 직접 캡처 pxhash={sb}");
                        }
                        RenderTexture.active = null;
                        Object.Destroy(tex17);
                        Object.Destroy(rt17);
                    }
                    catch (System.Exception e17)
                    {
                        Debug.Log($"{Tag} [G1] 동프레임 렌더 요청 미지원 — SKIP({e17.Message})");
                    }
                    // [무번호 19 재배열] cfx_08/09를 G1 동프레임 렌더 직후(+1프레임)로 — FX 신선 시점 캡처 복원
                    //   (기존 순서는 G1 블록 지연으로 링/버스트 소진 후 촬영 — 판독 불가 원인)
                    _shot = 8;
                    yield return Capture(cam, "cfx_08_parry_perfect", "PERFECT 판정 FX(발화 직후)");
                    yield return new WaitForSeconds(0.15f);
                    _shot = 9;
                    yield return Capture(cam, "cfx_09_parry_perfect_mid", "PERFECT 판정 FX(0.15s 경과)");
                    _shot = 18;
                    yield return Capture(flashCam, "cfx_17_whiteframe_realcam", "[무번호 19-G1] 화이트 프레임 실카메라 직접 캡처");
                    // [무번호 19-G1 결정 구분] +1프레임 시점 — 플래시 생존/알파 실측.
                    //   소멸=배치 펌프 지연>0.07s(구조적 — Play 판정 대상) / 생존+고알파+미표출=렌더 결함(수리 대상).
                    yield return null;
                    var fwCheck = GameObject.Find("ParryWhiteFrame");
                    if (fwCheck == null)
                    {
                        Debug.Log($"{Tag} [G1] 경과 후 플래시 소멸 확인 — 배치 펌프 지연 > 0.07s 수명(구조적 한계 — Play 판정 대상)");
                    }
                    else
                    {
                        var fwMr = fwCheck.GetComponent<Renderer>();
                        var fwMat = fwMr != null ? fwMr.sharedMaterial : null;
                        float fwA = 0f;
                        if (fwMat != null && fwMat.HasProperty("_Color")) fwA = fwMat.GetColor("_Color").a;
                        else if (fwMat != null && fwMat.HasProperty("_BaseColor")) fwA = fwMat.GetColor("_BaseColor").a;
                        Debug.Log($"{Tag} [G1] +1프레임 플래시 생존=True alpha={fwA:F3} shader={(fwMat != null && fwMat.shader != null ? fwMat.shader.name : "NULL")}");
                        _shot = 19;
                        yield return Capture(flashCam, "cfx_17c_whiteframe_nextframe", "[무번호 19-G1] 화이트 프레임 +1프레임(알파 상승 후) 캡처");
                    }
                }
            }
            yield return new WaitForSeconds(0.5f);

            // ── 컷 6: 명령 링(CommandMarker) — RTS 지정 링 실카메라 품질 ──
            _shot = 10;
            var marker = CommandMarker.Spawn(pPos + player.transform.forward * 2.5f);
            if (marker == null)
            {
                Debug.LogError($"{Tag} FAIL — CommandMarker 스폰 실패(IsPlacementActive 등)");
                CombatFxRealCamProbe.StepErrors++;
            }
            else
            {
                DumpFxObject(marker.gameObject, "command");
                yield return new WaitForSeconds(0.1f);
                _shot = 11;
                yield return Capture(cam, "cfx_11_command_ring", "명령 링(스폰 0.1s)");
            }
            yield return new WaitForSeconds(0.5f);

            // ── [무번호 16-B5] 지면 프로젝터(데칼) 방식 vs 현재 퀄 방식 비교 실측 컷 ──
            // ── [무번호 16-B1~B3] 부채꼴 게이지 링(위험도 색) 비교 컷 ─────
            _shot = 13;
            var wedge = AttackTelegraphIndicator.Spawn(pPos + player.transform.forward * 2.2f, 2.2f, 1.2f, false, true, player.transform.forward);
            if (wedge == null)
            {
                Debug.LogError($"{Tag} FAIL — 부채꼴 게이지 오버로드 스폰 실패");
                CombatFxRealCamProbe.StepErrors++;
            }
            else
            {
                yield return new WaitForSeconds(0.4f);
                _shot = 14;
                yield return Capture(cam, "cfx_13_wedge_early", "부채꼴 게이지 fill 0.33(일반=적색)");
                yield return new WaitForSeconds(0.5f);
                _shot = 15;
                yield return Capture(cam, "cfx_14_wedge_late", "부채꼴 게이지 fill 0.83(일반=적색)");
                Destroy(wedge);
            }
            // 정예/강공격 = 주황(B3 색 계약)
            _shot = 16;
            var wedgeElite = AttackTelegraphIndicator.Spawn(pPos + player.transform.forward * 2.2f, 2.2f, 1.2f, true, true, player.transform.forward);
            if (wedgeElite == null)
            {
                Debug.LogError($"{Tag} FAIL — 부채꼴 정예(주황) 스폰 실패");
                CombatFxRealCamProbe.StepErrors++;
            }
            else
            {
                yield return new WaitForSeconds(0.3f);
                _shot = 17;
                yield return Capture(cam, "cfx_15_wedge_elite", "부채꼴 게이지 fill 0.25(정예=주황)");
                Destroy(wedgeElite);
            }

            _shot = 12;
            yield return CaptureDecalProjector(cam);

            // ── 컷 [무번호 19-F3]: 이동 링 데칼 부착 — 지속형(그룹 소유) 스폰 → 데칼 경로 곡면 밀착 실측 ──
            //   B5 근거(cfx_11 쿼드 부유 vs cfx_12 데칼 밀착)로 전환 확정 → 실카메라 재검증 컷.
            _shot = 15;
            var guardForDecal = Object.FindAnyObjectByType<ProjectName.Systems.GuardPlaceholder>();
            if (guardForDecal == null)
            {
                Debug.LogError($"{Tag} FAIL — 데칼 컷용 GuardPlaceholder 미발견");
                CombatFxRealCamProbe.StepErrors++;
            }
            else
            {
                var moveGroup = new System.Collections.Generic.List<ProjectName.Systems.GuardPlaceholder> { guardForDecal };
                var moveMarker = CommandMarker.Spawn(pPos + player.transform.forward * 3.5f,
                    CommandRingPresentation.MoveTargetColor, null, CommandRingPresentation.MoveMarkerScale, moveGroup);
                if (moveMarker == null)
                {
                    Debug.LogError($"{Tag} FAIL — 이동 링(데칼) 스폰 실패(IsPlacementActive 등)");
                    CombatFxRealCamProbe.StepErrors++;
                }
                else
                {
                    DumpFxObject(moveMarker.gameObject, "move_decal");
                    yield return new WaitForSeconds(0.1f);
                    _shot = 16;
                    yield return Capture(cam, "cfx_16_move_ring_decal", "[무번호 19-F3] 이동 링 데칼 부착(곡면 밀착)");
                }
            }
            yield return new WaitForSeconds(0.4f);

            // ─── Shader/material diagnostic dump (A1) ─────
            _shot = 12;
            var teleShader = Shader.Find("ProjectName/TelegraphRing");
            Debug.Log($"{Tag} TelegraphRing Shader.Find={(teleShader != null ? teleShader.name : "NULL")}" +
                      (teleShader != null ? $" supported={(teleShader.isSupported ? "true" : "FALSE")}" : ""));
            Debug.Log($"{Tag} 파이프라인={UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name ?? "Builtin"}");

            Debug.Log($"{Tag} ===RUNNER_DONE=== StepErrors={CombatFxRealCamProbe.StepErrors}");
            CombatFxRealCamProbe.Finish(CombatFxRealCamProbe.StepErrors);
        }

        private static Camera FindRealCamera()
        {
            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled) return main;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (c.isActiveAndEnabled && c.CompareTag("MainCamera")) return c;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (c.isActiveAndEnabled) return c;
            return null;
        }

        private static void DumpCameras()
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in cams)
            {
                Debug.Log($"{Tag} CAMDUMP name={c.name} active={c.isActiveAndEnabled} tag={(c.CompareTag("MainCamera") ? "MainCamera" : "-")} " +
                          $"pos={c.transform.position:F2} euler={c.transform.eulerAngles:F1} fov={c.fieldOfView:F1} near={c.nearClipPlane:F2} far={c.farClipPlane:F1} " +
                          $"mask=0x{c.cullingMask:X8} clear={c.clearFlags} ortho={c.orthographic} targetRT={(c.targetTexture != null ? c.targetTexture.name : "null")}");
            }
        }

        private static void DumpPipeline()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            Debug.Log($"{Tag} PIPELINE={(rp != null ? rp.name : "BuiltinSRP")} fog={RenderSettings.fog} ambient={RenderSettings.ambientMode}");
        }

        /// <summary>FX GO 진단 덤프 — 레이어/렌더러/머티리얼 셰이더/스케일(가시성 불일치 좁히기용).</summary>
        private static void DumpFxObject(GameObject go, string label)
        {
            if (go == null) { Debug.Log($"{Tag} FXDUMP[{label}] go=NULL"); return; }
            Debug.Log($"{Tag} FXDUMP[{label}] name={go.name} layer={go.layer}({LayerMask.LayerToName(go.layer)}) pos={go.transform.position:F2} scale={go.transform.lossyScale:F2}");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                string mats = "";
                foreach (var m in r.sharedMaterials)
                    mats += (m != null ? $"[{m.name}/{(m.shader != null ? m.shader.name : "NULL_SHADER")}]" : "[NULL]") + ";";
                Debug.Log($"{Tag} FXDUMP[{label}] renderer={r.name} enabled={r.enabled} layer={r.gameObject.layer} mats={mats}");
            }
        }

        /// <summary>
        /// [무번호 16-B5] 지면 프로젝터(데칼) 방식 vs 쿼드 방식 비교 실측 — URP Decal Projector 스폰 후 실카메라 캡처.
        /// 데칼은 곡면 지형 대응(공굴림 없는 지면에서도 그림자처럼 붙는 링) — 쿼드 방식과 비교 근거.
        /// 데칼 머티리얼은 URP 패키지 기본 데칼 셰이더가 있으면 절차 생성, 없으면 폴백 쿼드(비교 근거 소실).
        /// 결정론(Random 0건), 스폰 후 자가 파괴(씬 오염 방지).
        /// </summary>
        private IEnumerator CaptureDecalProjector(Camera cam)
        {
            var decalType = System.Type.GetType("UnityEngine.Rendering.Universal.DecalProjector, Unity.RenderPipelines.Universal.Runtime");
            if (decalType == null)
            {
                Debug.Log($"{Tag} [B5] URP DecalProjector 타입 미발견 — 데칼 방식 비교 실측 불가(쿼드 방식 유지 판정 근거)");
                yield break;
            }
            var decalShader = Shader.Find("Shader Graphs/Decal");
            if (decalShader == null) decalShader = Shader.Find("Universal Render Pipeline/Decal");
            if (decalShader == null)
            {
                Debug.Log($"{Tag} [B5] URP 데칼 셰이더 미발견(Shader Graphs/Decal·URP/Decal) — 데칼 방식 비교 실측 불가(쿼드 방식 유지 판정 근거)");
                yield break;
            }
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 pPos = player.transform.position;
            Vector3 decalPos = pPos + player.transform.forward * 2.2f + Vector3.up * 0.06f;
            var go = new GameObject("B5_DecalProjector");
            go.transform.position = decalPos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var proj = go.AddComponent(decalType);
            // DecalProjector — size Vector3(radius*2, radius*2, 1) + material 세팅(리플렉션)
            var sizeProp = decalType.GetProperty("size");
            var matProp = decalType.GetProperty("material");
            var sizeVal = System.Activator.CreateInstance(typeof(Vector3), 2.2f, 2.2f, 1f);
            if (sizeProp != null) sizeProp.SetValue(proj, sizeVal);
            var decalMat = new Material(decalShader);
            // [무번호 18-B5 수리] 데칼 머티리얼에 _BaseMap 텍스처 필수 — URP/Decal 셰이더그래프는 텍스처 없으면 렌더 불가(빈 화면 원인)
            var tex = new Texture2D(256, 256, TextureFormat.ARGB32, false);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float dx = x - 128f, dy = y - 128f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 128f;
                    bool ring = r > 0.82f && r < 0.95f;
                    Color c = ring ? new Color(0.1f, 0.85f, 0.9f, 1f) : new Color(0.1f, 0.85f, 0.9f, 0.25f);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            bool texAssigned = false;
            for (int i = 0; i < decalShader.GetPropertyCount(); i++)
            {
                if (decalShader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    decalMat.SetTexture(decalShader.GetPropertyName(i), tex);
                    texAssigned = true;
                    break;
                }
            }
            Debug.Log($"{Tag} [B5] 데칼 머티리얼 텍스처 할당={(texAssigned ? decalShader.GetPropertyName(0) : "FAILED")} — {decalShader.name}");
            if (matProp != null) matProp.SetValue(proj, decalMat);
            yield return new WaitForSeconds(0.2f);
            // [B5 실측] 데칼 스폰 성공 → 실카메라 캡처
            yield return Capture(cam, "cfx_12_decal_projector", "B5 데칼 프로젝터 방식(곡면 지형 대응 비교)");
            Object.Destroy(go);
            yield return null;
        }

        /// <summary>실카메라 RT 캡처 — [무번호 16-A 근본 수리] URP는 Camera.Render() 미지원(초기 스냅샷 동결 — 실측
        ///   3런 pxhash 동일) → 정식 수단 RenderPipeline.SubmitRenderRequest(StandardRequest)로 교체.
        ///   Builtin 폴백은 cam.Render() 유지(URP 계약 위반만 동결 — 폴백은 미작동 환경용).</summary>
        private IEnumerator Capture(Camera cam, string fileName, string label)
        {
            yield return null;   // 프레임 진행(팔로우 카메라 LateUpdate 반영)
            var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
            try
            {
                bool isUrp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                if (isUrp)
                {
                    // Unity 6 정식 수단 — UniversalRenderPipeline.SingleCameraRequest(destination) (URP 패키지 UniversalRenderPipeline.cs:2501 실측)
                    var req = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
                }
                else
                {
                    var prev = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = prev;
                }
                RenderTexture.active = rt;
                var tex = new Texture2D(RTW, RTH, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, RTW, RTH), 0, 0);
                tex.Apply();
                // [무번호 16-A 수리] 캡처별 해시 자기검증 — 렌더 동결(md5 동일) 즉시 발각 계약
                var png = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes($"C:/Unity/code/TestOutput/{fileName}.png", png);
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    var hash = md5.ComputeHash(png);
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2"));
                    Debug.Log($"{Tag} ===CAPTURE=== shot={_shot} {fileName} — {label} pxhash={sb}");
                }
                Object.Destroy(tex);
            }
            finally
            {
                RenderTexture.active = null;
                rt.Release();
                Object.Destroy(rt);
            }
            Debug.Log($"{Tag} CAPTURE_CAMPOS {fileName} cam={cam.transform.position:F2} euler={cam.transform.eulerAngles:F1}");
        }
    }

    /// <summary>[무번호 16-A] Play 진입 도메인 리로드 후 펌프 재구독 부트 — 146/156차 선례 1:1.</summary>
    [InitializeOnLoad]
    internal static class CombatFxProbeBoot
    {
        static CombatFxProbeBoot()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("cfx_probe_active", false))
                    CombatFxRealCamProbe.ResubscribeAfterReload();
            };
        }
    }
}
