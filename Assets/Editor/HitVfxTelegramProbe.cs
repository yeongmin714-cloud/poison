using System.Collections;
using ProjectName.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [11-16 사용자 요청] 피격 VFX 실카메라 캡처 프로브 — Test_10_TerritoryCombat에서
    ///   피격 체인(CombatFXGate.PlayHitFX + DecalSpawner ScorchMark 지면 쿼드)을 생산 경로 1:1로 발화하고
    ///   실카메라 RT 캡처 4컷을 찍는다. 목적: 구 프로젝터 경로의 "흰 사각형" 소멸 검증(58-P2 대체본).
    /// 계약(무번호 16-A 선례 1:1): -quit/-nographics/-batchmode 금지, SessionState+InitializeOnLoad 펌프 재구독,
    ///   몬스터 AI 비활성, 실내 잔존 가드, pxhash 자기검증, 프로브 자가 Exit.
    /// [11-16 1차 런 실측 수리] 실카메라가 캡처 시점에 (0,0,0) 원점 리셋 → 전 컷 동일 해시(FX 시야 밖).
    ///   수리 = 캡처마다 PM 공식 좌표를 실카메라에 직접 강제 적용(동기 순차 — 리셋 경합 없음).
    ///   DECDUMP는 데칼 GO 이름 접두 검색(실명 "Decal_ScorchMark_0" — Count 접미).
    /// 실행: bash run_hitvfx_probe.sh (ENTRY=ProjectName.EditorTools.HitVfxTelegramProbe.RunAll).
    /// </summary>
    public static class HitVfxTelegramProbe
    {
        private const string Tag = "[히트VFX프로브]";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity";
        private const string SessionKey = "hitvfx_probe_active";
        private const float SetupTimeoutSec = 30f;
        private const float OverallTimeoutSec = 240f;

        internal static int StepErrors;

        private enum Phase { WaitingPlay, WaitingSetup, Running, Finished }
        private static Phase _phase = Phase.WaitingPlay;
        private static float _runStartRealtime = -1f;
        private static bool _pumpSubscribed;
        private static HitVfxRunner _runner;

        /// <summary>배치 런 진입점 — 씬 오픈 → Play 진입 요청(펌프/러너가 이어받는다).</summary>
        public static void RunAll()
        {
            Debug.Log($"{Tag} === 피격 VFX 실카메라 캡처 프로브 시작 (흰 사각형 소멸 검증) ===");
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
                        if (player == null)
                        {
                            if (now - _runStartRealtime > SetupTimeoutSec)
                                FailFatal($"셋업 타임아웃({SetupTimeoutSec}s) — Player 미발견");
                            return;
                        }
                        if (_runner == null)
                        {
                            var host = new GameObject("HitVfxProbeRunnerHost");
                            _runner = host.AddComponent<HitVfxRunner>();
                            _phase = Phase.Running;
                            Debug.Log($"{Tag} 러너 기동 — 피격 체인 발화+실카메라 캡처 시작");
                        }
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

    /// <summary>피격 체인 발화 러너 — 생산 경로 1:1(PlayHitFX+ScorchMark) 실카메라 캡처(코루틴 순차).</summary>
    public class HitVfxRunner : MonoBehaviour
    {
        private const string Tag = "[히트VFX프로브]";
        private const int RTW = 1024;
        private const int RTH = 768;
        private int _shot;
        private Vector3? _pmPos;         // [11-16 수리] 캡처마다 강제 적용하는 카메라 좌표
        private Quaternion? _pmRot;
        private string _firstHash;       // 전 컷 동일 해시 = 정지 뷰/렌더 동결 자기발각
        private int _hashCollisions;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);   // 셋업/스폰 안착 정착

            // [16-A 선례] 몬스터 AI 비활성 — 어그로→사망→이계 전환 차단
            var ais = Object.FindObjectsByType<AnimalAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int disabled = 0;
            foreach (var m in ais)
                if (m != null && m.enabled) { m.enabled = false; disabled++; }
            Debug.Log($"{Tag} 몬스터 AI 비활성 — {disabled}체(사망→이계 전환 차단)");

            // [16-A 선례] 실내 잔존 가드
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "IndoorScene")
            {
                Debug.LogError($"{Tag} FAIL — 부트 직후 실내 상태(이계 전환 잔존) — 재런 필요");
                HitVfxTelegramProbe.StepErrors++;
                HitVfxTelegramProbe.Finish(HitVfxTelegramProbe.StepErrors);
                yield break;
            }

            Camera cam = FindRealCamera();
            if (cam == null)
            {
                Debug.LogError($"{Tag} FAIL — 실카메라(Tag MainCamera) 미발견");
                HitVfxTelegramProbe.StepErrors++;
                HitVfxTelegramProbe.Finish(HitVfxTelegramProbe.StepErrors);
                yield break;
            }
            Debug.Log($"{Tag} 실카메라 확정 — name={cam.name} pos={cam.transform.position:F2} euler={cam.transform.eulerAngles:F1} fov={cam.fieldOfView:F1}");

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError($"{Tag} FAIL — Player 미발견");
                HitVfxTelegramProbe.StepErrors++;
                HitVfxTelegramProbe.Finish(HitVfxTelegramProbe.StepErrors);
                yield break;
            }
            Vector3 pPos = player.transform.position;
            Vector3 pFwd = player.transform.forward; pFwd.y = 0f;
            if (pFwd.sqrMagnitude < 0.001f) pFwd = Vector3.forward;
            pFwd.Normalize();
            Debug.Log($"{Tag} 플레이어 pos={pPos:F2} forward={pFwd:F2}");

            // [11-16 수리] PM 공식 좌표 확정 — 카메라가 캡처 시점마다 원점으로 리셋되는 실측(1차 런)에 따라
            //   실카메라 위치를 코루틴 흐름과 무관하게 만듦. 매 컷 Capture 직전 강제 적용(동기 순차).
            //   월드 FX(히트 임팩트/지면 데칼) 전용 시야 — A3 계약상 카메라 공간 쿼드(화이트 프레임)는 대상 아님.
            {
                const float PM_PITCH = 57.6f, PM_DIST = 12.2f;
                Vector3 pmCamPos = pPos + new Vector3(0f, 1.4f, 0f)
                                 + Quaternion.Euler(PM_PITCH, 0f, 0f) * new Vector3(0f, 0f, -PM_DIST);
                _pmPos = pmCamPos;
                _pmRot = Quaternion.LookRotation(pPos + new Vector3(0f, 1.2f, 0f) - pmCamPos);
                Debug.Log($"{Tag} PM 공식 좌표 확정 pos={pmCamPos:F2} euler={_pmRot.Value.eulerAngles:F1} — 캡처마다 강제 적용");
            }

            // ── 피격 체인 발화(생산 경로 1:1) ──
            // [몬스터→플레이어 히트 경로 1:1] AnimalAI.ConcludeMeleeTelegraph의 두 발화점:
            //   ①스코치 데칼 = 대상 발밑 지면(y 0.02, DecalSpawner "ScorchMark") — 흰 사각형의 직접 후보
            //   ②플레이어 측 피격 VFX = CombatFXGate.PlayHitFX(흡수점 y+0.9) — Guz 임팩트/크랙 데칼/데미지 숫자
            Vector3 hitGround = new Vector3(pPos.x + pFwd.x * 2.2f, 0.02f, pPos.z + pFwd.z * 2.2f);
            Vector3 hitChest = hitGround + new Vector3(0f, 0.88f, 0f);

            string scorchSpawned = "NO";
            try
            {
                if (DecalSpawner.Instance != null)
                {
                    DecalSpawner.Instance.SpawnDecal("ScorchMark", hitGround, Quaternion.identity);
                    scorchSpawned = "YES";
                }
            }
            catch (System.Exception e) { Debug.LogError($"{Tag} ScorchMark 스폰 예외: {e.GetType().Name} {e.Message}"); HitVfxTelegramProbe.StepErrors++; }
            Debug.Log($"{Tag} ①ScorchMark 데칼 스폰={scorchSpawned} at {hitGround:F2} (생산 호출 1:1)");
            if (scorchSpawned != "YES") HitVfxTelegramProbe.StepErrors++;

            try
            {
                CombatFXGate.PlayHitFX(hitChest, pFwd, CombatHitType.Organic, false, 20f, Color.white);
                Debug.Log($"{Tag} ②PlayHitFX 발화 at {hitChest:F2} damage=20(≥15 — 크랙 데칼 게이트 충족)");
            }
            catch (System.Exception e) { Debug.LogError($"{Tag} PlayHitFX 예외: {e.GetType().Name} {e.Message}"); HitVfxTelegramProbe.StepErrors++; }

            // 발화 데칼 상태 덤프 — 셰이더/텍스처/스케일 진실원(흰 사각 뿌리 판별 근거)
            // [11-16 수리] 실명은 "Decal_ScorchMark_0"(Count 접미) — 접두 검색으로 수정(1차 런 미발견 원인).
            yield return null; yield return null;
            var scorch = FindDecalByPrefix("Decal_ScorchMark");
            if (scorch != null)
            {
                foreach (var r in scorch.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        Texture2D baseTex = null;
                        Color baseCol = new Color(0f, 0f, 0f, 0f);
                        if (m != null)
                        {
                            if (m.HasProperty("_BaseMap")) baseTex = m.GetTexture("_BaseMap") as Texture2D;
                            else baseTex = m.mainTexture as Texture2D;
                            if (m.HasProperty("_BaseColor")) baseCol = m.GetColor("_BaseColor");
                            else baseCol = m.color;
                        }
                        Debug.Log($"{Tag} [DECDUMP] ScorchMat name={(m != null ? m.name : "NULL")} " +
                                  $"shader={(m != null && m.shader != null ? m.shader.name : "NULL_SHADER")} " +
                                  $"baseMap={(baseTex != null ? $"{baseTex.name}({baseTex.width}x{baseTex.height})" : "NULL")} " +
                                  $"_BaseColor={baseCol} queue={(m != null ? m.renderQueue : -1)} " +
                                  $"scale={r.transform.localScale:F3} worldPos={r.transform.position:F2}");
                    }
                }
            }
            else
            {
                Debug.LogError($"{Tag} [DECDUMP] Decal_ScorchMark GO 접두 검색 실패(스폰 실패 또는 즉시 정리)");
                HitVfxTelegramProbe.StepErrors++;
            }

            // ── 실카메라 캡처 4컷 — 임팩트 순간부터 데칼 안착까지 ──
            _shot = 1;
            yield return Capture(cam, "hitvfx_01_impact", "발화 직후(+2프레임) — Guz 임팩트/아크/스파이크");
            yield return new WaitForSeconds(0.3f);
            _shot = 2;
            yield return Capture(cam, "hitvfx_02_early", "0.3s — 데미지 숫자+크랙 데칼+파편");
            yield return new WaitForSeconds(0.7f);
            _shot = 3;
            yield return Capture(cam, "hitvfx_03_mid", "1.0s — 크랙 페이드 중+스코치 데칼");
            yield return new WaitForSeconds(2.0f);
            _shot = 4;
            yield return Capture(cam, "hitvfx_04_decal", "3.0s — 임팩트 소멸 후 지면 흔적(스코치) 안착");

            Debug.Log($"{Tag} 해시 자기검증 — 충돌={_hashCollisions} (0이어야 함 — 전 컷 동일 해시=정지 뷰 결함)");
            if (_hashCollisions >= 3)
            {
                Debug.LogError($"{Tag} FAIL — 전 컷 해시 충돌 {_hashCollisions}건 — 원점 리셋/렌더 동결 재발(무효 근거)");
                HitVfxTelegramProbe.StepErrors++;
            }

            Debug.Log($"{Tag} ===RUNNER_DONE=== StepErrors={HitVfxTelegramProbe.StepErrors}");
            HitVfxTelegramProbe.Finish(HitVfxTelegramProbe.StepErrors);
        }

        /// <summary>데칼 GO 접두 검색 — 실명은 Decal_ScorchMark_0(Count 접미)이므로 StartsWith 매칭.</summary>
        private static GameObject FindDecalByPrefix(string prefix)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith(prefix)) return t.gameObject;
            return null;
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

        /// <summary>실카메라 RT 캡처 — [11-16 수리] 캡처 직전 PM 공식 좌표 강제 적용(원점 리셋 무시) 후
        ///   URP SingleCameraRequest 정식 수단 + pxhash 자기검증(16-A 계약 1:1).</summary>
        private IEnumerator Capture(Camera cam, string fileName, string label)
        {
            yield return null;   // 프레임 진행(FX 갱신 반영)
            // [11-16 수리] 렌더 직전 동기 순차로 시야 강제 — 원점 리셋 경합 제거
            if (_pmPos.HasValue && _pmRot.HasValue)
            {
                cam.transform.position = _pmPos.Value;
                cam.transform.rotation = _pmRot.Value;
            }
            var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
            string hash = "";
            try
            {
                bool isUrp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                if (isUrp)
                {
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
                var png = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes($"C:/Unity/code/TestOutput/{fileName}.png", png);
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    var bytes = md5.ComputeHash(png);
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < 4; i++) sb.Append(bytes[i].ToString("x2"));
                    hash = sb.ToString();
                    Debug.Log($"{Tag} ===CAPTURE=== shot={_shot} {fileName} — {label} pxhash={hash}");
                }
                Object.Destroy(tex);
            }
            finally
            {
                RenderTexture.active = null;
                rt.Release();
                Object.Destroy(rt);
            }
            if (_firstHash == null) _firstHash = hash;
            else if (hash == _firstHash) _hashCollisions++;
            Debug.Log($"{Tag} CAPTURE_CAMPOS {fileName} cam={cam.transform.position:F2} euler={cam.transform.eulerAngles:F1}");
        }
    }

    /// <summary>Play 진입 도메인 리로드 후 펌프 재구독 부트 — 146/156/16-A 선례 1:1.</summary>
    [InitializeOnLoad]
    internal static class HitVfxProbeBoot
    {
        static HitVfxProbeBoot()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("hitvfx_probe_active", false))
                    HitVfxTelegramProbe.ResubscribeAfterReload();
            };
        }
    }
}
