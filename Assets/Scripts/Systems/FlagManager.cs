using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 34: 국기 관리자 싱글톤.
    /// 모든 깃대(FlagPoleDisplay) 인스턴스와 영지 소유권 변경에 따른
    /// 국기 표시를 관리합니다.
    /// </summary>
    public class FlagManager : MonoBehaviour
    {
        public static FlagManager Instance { get; private set; }

        /// <summary>모든 깃대 표시 인스턴스</summary>
        private readonly List<GameObject> _flagPoles = new List<GameObject>();

        /// <summary>영지별 국기 높이 상태 (true = 반기/contested)</summary>
        private readonly Dictionary<string, bool> _contestedStates = new Dictionary<string, bool>();

        /// <summary>영지별 현재 소유 국가</summary>
        private readonly Dictionary<string, NationType> _territoryOwners = new Dictionary<string, NationType>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 깃대 표시를 등록합니다.
        /// </summary>
        public void RegisterFlagPole(GameObject flagPole)
        {
            if (flagPole != null && !_flagPoles.Contains(flagPole))
            {
                _flagPoles.Add(flagPole);
            }
        }

        /// <summary>
        /// 깃대 표시를 제거합니다.
        /// </summary>
        public void UnregisterFlagPole(GameObject flagPole)
        {
            if (flagPole != null)
            {
                _flagPoles.Remove(flagPole);
            }
        }

        /// <summary>
        /// 영지 소유권 변경 시 호출됩니다.
        /// 소유권에 따라 해당 영지의 국기를 업데이트합니다.
        /// </summary>
        public void OnTerritoryOwnershipChanged(string territoryId, NationType newOwner)
        {
            if (string.IsNullOrEmpty(territoryId))
                return;

            _territoryOwners[territoryId] = newOwner;

            // 해당 영지의 깃발 업데이트
            NationFlagDefinition flag = GetNationFlag(newOwner);
            Debug.Log($"[FlagManager] 영지 {territoryId}의 소유권이 {newOwner}로 변경됨 — " +
                      $"깃발: {flag.symbolEmoji} {flag.colorName}");

            // TerritoryBannerSystem과 연동하여 ChangeOwnership 호출
            if (TerritoryBannerSystem.Instance != null)
            {
                string territoryName = GetTerritoryName(territoryId);
                bool isPlayer = newOwner == NationType.East; // 임시 플레이어 소유 판별
                TerritoryBannerSystem.Instance.ChangeOwnership(territoryId, territoryName, newOwner, isPlayer);
            }

            // 모든 깃대 업데이트
            UpdateAllFlagDisplays();
        }

        /// <summary>
        /// 특정 영지의 Contested(전쟁 중) 상태를 설정합니다.
        /// true이면 국기가 반기(절반 높이) 상태가 됩니다.
        /// </summary>
        public void SetContestedState(string territoryId, bool isContested)
        {
            if (string.IsNullOrEmpty(territoryId))
                return;

            _contestedStates[territoryId] = isContested;

            if (isContested)
            {
                Debug.Log($"[FlagManager] 영지 {territoryId}가 전쟁 중입니다 — 국기 반기");
            }
            else
            {
                Debug.Log($"[FlagManager] 영지 {territoryId}의 전쟁 상태가 해제되었습니다 — 국기 정상 게양");
            }

            UpdateAllFlagDisplays();
        }

        /// <summary>
        /// 특정 국가의 국기 정의를 반환합니다.
        /// </summary>
        public NationFlagDefinition GetNationFlag(NationType nation)
        {
            return NationFlagDatabase.GetFlag(nation);
        }

        /// <summary>
        /// 모든 깃대 표시를 현재 상태에 맞게 업데이트합니다.
        /// </summary>
        private void UpdateAllFlagDisplays()
        {
            // 깃대 시각 업데이트 (향후 확장: 깃발 색상, 높이, 메테리얼 변경)
            foreach (var pole in _flagPoles)
            {
                if (pole != null)
                {
                    // 깃발 시각 업데이트 로직 (향후 구현)
                    // 현재는 로그만 출력
                }
            }
        }

        /// <summary>
        /// territoryId로 영지 이름을 조회합니다.
        /// </summary>
        private static string GetTerritoryName(string territoryId)
        {
            if (TerritoryDatabase.Instance != null)
            {
                var def = TerritoryDatabase.Instance.GetDefinition(territoryId);
                if (!string.IsNullOrEmpty(def.territoryName))
                    return def.territoryName;
            }
            return territoryId;
        }

        /// <summary>
        /// 현재 깃대 개수를 반환합니다.
        /// </summary>
        public int FlagPoleCount => _flagPoles.Count;

        /// <summary>
        /// 등록된 영지 소유권 수를 반환합니다.
        /// </summary>
        public int TerritoryOwnerCount => _territoryOwners.Count;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}