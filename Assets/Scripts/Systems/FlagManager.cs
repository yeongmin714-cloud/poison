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
        private readonly List<FlagPoleDisplay> _flagPoles = new List<FlagPoleDisplay>();

        /// <summary>영지 ID -> 깃대 표시 매핑 (직접 조회 최적화)</summary>
        private readonly Dictionary<string, FlagPoleDisplay> _flagPoleByTerritory = new Dictionary<string, FlagPoleDisplay>();

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
        /// 깃대 표시를 영지 ID와 함께 등록합니다.
        /// </summary>
        /// <param name="territoryId">깃대가 속한 영지 ID</param>
        /// <param name="flagPole">등록할 FlagPoleDisplay 컴포넌트</param>
        public void RegisterFlagPole(string territoryId, FlagPoleDisplay flagPole)
        {
            if (flagPole == null || string.IsNullOrEmpty(territoryId))
                return;

            if (!_flagPoles.Contains(flagPole))
            {
                _flagPoles.Add(flagPole);
                _flagPoleByTerritory[territoryId] = flagPole;
                _territoryOwners.TryAdd(territoryId, NationType.None);
                _contestedStates.TryAdd(territoryId, false);
            }
        }

        /// <summary>
        /// 깃대 표시를 제거합니다.
        /// </summary>
        /// <param name="territoryId">제거할 깃대의 영지 ID</param>
        public void UnregisterFlagPole(string territoryId)
        {
            if (string.IsNullOrEmpty(territoryId))
                return;

            if (_flagPoleByTerritory.TryGetValue(territoryId, out var pole))
            {
                _flagPoles.Remove(pole);
                _flagPoleByTerritory.Remove(territoryId);
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

            // 해당 영지의 깃대 직접 업데이트
            UpdateFlagDisplay(territoryId, newOwner);
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

            // 해당 영지의 깃대 반기/정상 게양 업데이트
            if (_flagPoleByTerritory.TryGetValue(territoryId, out var pole) && pole != null)
            {
                pole.SetHalfMast(isContested);
            }
        }

        /// <summary>
        /// 특정 영지의 깃대를 현재 소유권에 맞게 업데이트합니다.
        /// </summary>
        private void UpdateFlagDisplay(string territoryId, NationType owner)
        {
            if (_flagPoleByTerritory.TryGetValue(territoryId, out var pole) && pole != null)
            {
                bool isPlayer = owner == NationType.East; // 임시 플레이어 소유 판별
                pole.FadeTransition(owner, isPlayer);

                // 반기 상태도 함께 적용
                if (_contestedStates.TryGetValue(territoryId, out bool isContested) && isContested)
                {
                    pole.SetHalfMast(true);
                }
            }
        }

        /// <summary>
        /// 특정 국가의 국기 정의를 반환합니다.
        /// </summary>
        public NationFlagDefinition GetNationFlag(NationType nation)
        {
            return NationFlagDatabase.GetFlag(nation);
        }

        /// <summary>
        /// 모든 깃대 표시를 현재 상태에 맞게 일괄 업데이트합니다.
        /// 씬 로드/리로드 시 전체 동기화에 사용합니다.
        /// </summary>
        public void RefreshAllFlagDisplays()
        {
            foreach (var kvp in _flagPoleByTerritory)
            {
                string territoryId = kvp.Key;
                FlagPoleDisplay pole = kvp.Value;

                if (pole == null) continue;

                if (_territoryOwners.TryGetValue(territoryId, out var owner))
                {
                    bool isPlayer = owner == NationType.East;
                    pole.SetOwner(owner, isPlayer);
                }

                if (_contestedStates.TryGetValue(territoryId, out bool isContested) && isContested)
                {
                    pole.SetHalfMast(true);
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

        /// <summary>
        /// 등록된 contested 상태 수를 반환합니다.
        /// </summary>
        public int ContestedStateCount => _contestedStates.Count;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}