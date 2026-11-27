using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// [101차 F-5] 테스트씬 15(Test_15_Livelihood) 건설 재료 지급 부트 — 실제 배치 테스트 준비용.
    ///  · 자가부트: [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] — FishingUTK/EnsureFishingSystem 선례
    ///    (TestTerritoryCombatSetup.SetupFishingArea 패턴 — 씬 파일 직접 수정 최소화 부트 자가부트).
    ///  · 씬 가드: SceneManager.GetActiveScene().name == "Test_15_Livelihood" 에서만 가동 —
    ///    마을씬/세이브 무영향(15씬 한정 계약).
    ///  · 지급: PlayerInventory.AddItem — mat_wood/mat_stone/iron_ore 각 99(건설 재료 실측 id 계약 —
    ///    BlueprintData materialWood/Stone/IronCost · PlayerInventory.Mat_Wood/Mat_Stone/IronOre 단일소스).
    ///  · 중복 지급 가드 — 정적 플래그 세션 1회(도메인 리로드 시 초기화 = 플레이 세션 계약).
    ///  · PlayerInventory.Instance는 씬 Player 구성(SetupPlayer → PlayerInventory.Awake) 경유 지연 확보 —
    ///    폴링 부트로 Instance 확보 후 1회 지급(부트 GO는 즉시 파괴 — 씬 잔존 없음).
    /// </summary>
    public class Test15MaterialGrantBoot : MonoBehaviour
    {
        private const string LivelihoodSceneName = "Test_15_Livelihood";   // 씬 가드 대상(정확 일치)
        private const int GrantCount = 99;                                  // 재료별 지급 수량(스펙 지정)
        private static bool _granted;   // 세션당 1회 가드(정적 플래그 — 도메인 리로드 시 초기화)

        /// <summary>[101차 F-5] 자가부트 진입 — AfterSceneLoad(씬 구성 후). 씬 가드 미충족 시 무가동(마을씬 무영향).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_granted) return;   // 세션당 1회 — 재진입(씬 재진입 포함) 무지급
            if (SceneManager.GetActiveScene().name != LivelihoodSceneName) return;   // 씬 가드 — 15씬 외 무가동
            var go = new GameObject("Test15MaterialGrantBoot");
            go.AddComponent<Test15MaterialGrantBoot>();   // 폴링 부트 — PlayerInventory 구성 대기 후 1회 지급
        }

        private void Update()
        {
            if (_granted)
            {
                Destroy(gameObject);   // 이미 지급됨 — 부트 해체(잔존 방지)
                return;
            }
            var inv = PlayerInventory.Instance;
            if (inv == null) return;   // Player 구성(PlayerInventory.Awake) 대기 — 폴링(테스트 씬 전용 저비용)

            _granted = true;   // 세션당 1회 플래그 — 지급 시도 개시와 동시에 확정(부분 지급 반복 방지)
            bool wood = inv.AddItem(PlayerInventory.Mat_Wood, GrantCount);
            bool stone = inv.AddItem(PlayerInventory.Mat_Stone, GrantCount);
            bool iron = inv.AddItem(PlayerInventory.IronOre, GrantCount);
            Debug.Log($"[101차 F-5] 테스트 재료 지급 — mat_wood={(wood ? GrantCount : 0)} " +
                      $"mat_stone={(stone ? GrantCount : 0)} iron_ore={(iron ? GrantCount : 0)} " +
                      $"(Test_15_Livelihood 전용 — 세션 1회 가드)");
            Destroy(gameObject);   // 지급 완료 — 부트 즉시 해체
        }
    }
}
