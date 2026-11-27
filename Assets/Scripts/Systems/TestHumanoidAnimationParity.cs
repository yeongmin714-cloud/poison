// 2026-10-25 (125차 후속29 후속2): 전 씬 휴머노이드 애니 패리티 게이트 — 사용자 계약
// "앞으로 모든 씬에서 애니메이션이 구동되어야 하는 부분은 테스트씬에서도 정상 재생(병사/몬스터/플레이어 전부)".
// 본 게이트는 씬 로드마다 플레이어의 클립 애니 체계를 검증하고, 구형 레거시 경로(GLB+ForceBiped 절차 애니 —
// T포즈/무공격애니의 뿌리)를 검증된 TestPlayerAnimatorBoot 부트로 승격한다.
// 본씬 보호: ①드라이버 존재 시 스킵 ②컨트롤러 존재 시 스킵 ③부트 자체 멱등 가드(후속29 후속2 추가).
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Systems
{
    /// <summary>
    /// [125차 후속29 후속2] 전 씬 플레이어 애니 패리티 게이트 — sceneLoaded 구동.
    /// 씬 로드마다(Additive 포함) 1.5s 후 검증(플레이어 미발견 시 4s에 1회 재시도):
    ///   ①플레이어 부재 → 무일(캐릭터 없는 씬 — UI/지형/UI 테스트 등)
    ///   ②루트 하위에 HumanoidClipDriver 존재 → 패리티 OK(씬별 로그 1회)
    ///   ③루트 하위 Animator 중 컨트롤러 보유(본씬 Player_AC 경로) → OK(로그 1회)
    ///   ④그 외(레거시 GLB+절차 경로) → TestPlayerAnimatorBoot 부착(검증 부트 — 스왑/정리/Player_AC/드라이버).
    /// 몬스터는 MonsterSpawner+AnimalAI가 씬 무관 표준 경로(할당자 자동분기+속도 공급)라 게이트 대상 아님.
    /// 병사는 스폰 사이트 계약으로 커버(ArenaBettingSystem 표준 병사 경로 / TestTerritoryCombatSetup·
    /// TerritoryNPCSpawner·TerritoryBuilder 검증 FBX 패턴).
    /// </summary>
    public static class TestHumanoidAnimationParity
    {
        private static bool _subscribed;
        private static ParityRunner _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_subscribed) return;
            _subscribed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            QueueCheck("초기씬");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => QueueCheck(scene.name);

        private static void QueueCheck(string sceneName)
        {
            if (_runner == null)
            {
                var host = new GameObject("TestAnimParityRunner");
                Object.DontDestroyOnLoad(host);
                _runner = host.AddComponent<ParityRunner>();
            }
            _runner.Queue(sceneName);
        }

        /// <summary>게이트 실행기 — 지연 검증 코루틴 호스트(정적 클래스는 코루틴 불가 → 영속 호스트).</summary>
        private class ParityRunner : MonoBehaviour
        {
            private readonly System.Collections.Generic.Queue<string> _pending = new();
            private readonly System.Collections.Generic.HashSet<string> _loggedOk = new();
            private bool _checking;

            public void Queue(string sceneName)
            {
                _pending.Enqueue(sceneName);
                if (!_checking) StartCoroutine(CheckLoop());
            }

            private System.Collections.IEnumerator CheckLoop()
            {
                _checking = true;
                while (_pending.Count > 0)
                {
                    string sceneName = _pending.Dequeue();
                    yield return new WaitForSeconds(1.5f);
                    yield return CheckOnce(sceneName, retry: true);
                }
                _checking = false;
            }

            private System.Collections.IEnumerator CheckOnce(string sceneName, bool retry)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    if (retry)   // 지연 스폰 씬 대비 1회 재시도(부트스트랩 직후 스폰 타이밍 레이스 흡수)
                    {
                        yield return new WaitForSeconds(2.5f);
                        yield return CheckOnce(sceneName, retry: false);
                    }
                    yield break;   // ① 캐릭터 없는 씬 — 무일
                }

                // ② 드라이버 존재(루트 하위 전체, 비활성 포함) → 패리티 OK
                if (player.GetComponentInChildren<HumanoidClipDriver>(true) != null)
                {
                    if (_loggedOk.Add(sceneName))
                        Debug.Log($"[TestAnimParity] ✅ {sceneName} — 플레이어 클립 드라이버 기존(애니 패리티 OK)");
                    yield break;
                }

                // ③ 본씬 경로 보호 — 루트 하위 Animator 중 컨트롤러 보유(빈 RequireComponent Animator 무시)
                foreach (var a in player.GetComponentsInChildren<Animator>(true))
                {
                    if (a != null && a.runtimeAnimatorController != null)
                    {
                        if (_loggedOk.Add(sceneName))
                            Debug.Log($"[TestAnimParity] ✅ {sceneName} — 플레이어 클립 컨트롤러 기존({a.runtimeAnimatorController.name}, 패리티 OK)");
                        yield break;
                    }
                }

                // ④ 레거시 경로(GLB+절차 애니) → 검증 부트 승격(부트 내부 멱등 가드가 이중 부착도 방어)
                if (player.GetComponent<TestPlayerAnimatorBoot>() == null)
                {
                    player.AddComponent<TestPlayerAnimatorBoot>();
                    Debug.Log($"[TestAnimParity] 🔧 {sceneName} — 레거시 플레이어 애니 경로 감지 → TestPlayerAnimatorBoot 승격(표준 클립 경로)");
                }
            }
        }
    }
}
