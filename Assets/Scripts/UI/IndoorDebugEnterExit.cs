using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// DEBUG-ONLY: F8 핫키로 실내(IndoorScene) 진입/퇴출 토글.
    /// 성 게이트까지 걸어가지 않고 Play 모드에서 즉시 실내 전환을 검증하기 위한 개발자 편의 도구.
    ///
    /// [동작]
    /// - F8 첫 입력(IndoorScene 미로드): IndoorSceneTransition.EnterBuilding("castle", "Empire", true, null)
    ///   → 실제 플레이어 성 진입 경로와 동일 (OnIndoorSceneLoaded 콜백이 원점 스폰/카메라 스왑/
    ///     플레이어 하이어라키 이동/셸·fixture 생성까지 전부 처리 — 로직 중복 없음)
    /// - F8 재입력(IndoorScene 로드됨): IndoorSceneTransition.ExitBuilding() → 월드 씬 복귀
    ///
    /// [등록 방식]
    /// EquipmentStatBonusApplier 선례(HotbarUI 패턴)에 따라 [RuntimeInitializeOnLoadMethod]로
    /// 런타임 자가 부트 → DontDestroyOnLoad 오브젝트에 상주하므로 씬 편집/수동 배치가 필요 없음.
    /// 빌드 제외 없이 포함되지만 순수 디버그 도구로 게임 로직에 개입하지 않음.
    /// </summary>
    public class IndoorDebugEnterExit : MonoBehaviour
    {
        private const KeyCode TOGGLE_KEY = KeyCode.F8;
        private const string DEBUG_TAG = "[IndoorDebug]";

        private static IndoorDebugEnterExit _instance;

        /// <summary>씬 편집 없이 Play 모드에서 상시 부트 (EquipmentStatBonusApplier 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var existing = FindAnyObjectByType<IndoorDebugEnterExit>();
            if (existing != null) { _instance = existing; return; }

            var go = new GameObject("[IndoorDebugEnterExit]");
            _instance = go.AddComponent<IndoorDebugEnterExit>();
            DontDestroyOnLoad(go); // 씬 전환(월드↔실내) 간에도 핫키 생존
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(TOGGLE_KEY)) return;

            bool indoorLoaded = IndoorSceneTransition.IsIndoorSceneLoaded();
            Debug.Log($"{DEBUG_TAG} F8: 실내 전환 토글 → {(indoorLoaded ? "실내 퇴출(ExitBuilding)" : "실내 진입(EnterBuilding: castle / Empire / playerOwned)")}");

            try
            {
                if (indoorLoaded)
                {
                    IndoorSceneTransition.ExitBuilding();
                }
                else
                {
                    // 실제 플레이어 성 진입 경로와 동일한 파라미터 (BuildingTrigger → BuildingEvents 경로와 같은 메서드 호출)
                    IndoorSceneTransition.EnterBuilding("castle", "Empire", true, null);
                }
            }
            catch (System.Exception ex)
            {
                // 알려진 이력: 빌더 NRE(호수 NRE로 GameSetup.Start 중단)가 Play 세션 전체를
                // 크래시시킬 수 있음 → 로그만 남기고 계속 진행
                Debug.LogError($"{DEBUG_TAG} 실내 전환 중 예외 발생(Play 세션 유지): {ex}");
            }
        }
    }
}
