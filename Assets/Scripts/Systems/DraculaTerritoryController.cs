using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// ND-02: 드라큘라 영지 야간 출현 관리자.
    /// TimeManager와 연동하여 밤에만 드라큘라 영지를 활성화/비활성화합니다.
    /// </summary>
    public class DraculaTerritoryController : MonoBehaviour
    {
        public static DraculaTerritoryController Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool _verbose;

        private TerritoryState _draculaState;
        private TerritoryDefinition _draculaDef;
        private bool _wasNight;
        private bool _isConquered;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            var db = TerritoryDatabase.Instance;
            _draculaDef = db.GetDefinition(NationType.Dracula, 1);
            _draculaState = db.GetState(NationType.Dracula, 1);

            if (_draculaState != null)
            {
                // 저장된 상태에서 점령 여부 복원 (save/load 대응)
                _isConquered = _draculaState.ownership == TerritoryOwnership.PlayerOwned
                            || _draculaState.lordDefeated;

                _draculaState.isActive = _isConquered; // 점령 상태면 항상 활성, 아니면 기본 비활성
            }

            _wasNight = false;
        }

        private void Update()
        {
            if (_draculaState == null) return;
            if (_isConquered) return; // 점령 후에는 항상 활성

            var tm = TimeManager.Instance;
            if (tm == null) return;

            bool isNight = tm.IsNight;
            if (isNight != _wasNight)
            {
                _wasNight = isNight;
                if (isNight)
                    ActivateTerritory();
                else
                    DeactivateTerritory();
            }
        }

        /// <summary>
        /// 드라큘라 영지 활성화 (밤)
        /// </summary>
        public void ActivateTerritory()
        {
            if (_draculaState == null) return;
            _draculaState.isActive = true;
            if (_verbose)
                Debug.Log($"[DraculaTerritoryController] 영지 활성화: {_draculaDef.territoryName ?? "(알 수 없음)"}");
        }

        /// <summary>
        /// 드라큘라 영지 비활성화 (낮)
        /// </summary>
        public void DeactivateTerritory()
        {
            if (_draculaState == null) return;
            _draculaState.isActive = false;
            if (_verbose)
                Debug.Log($"[DraculaTerritoryController] 영지 비활성화: {_draculaDef.territoryName ?? "(알 수 없음)"}");
        }

        /// <summary>
        /// 드라큘라 영주가 사망하면 점령 상태로 전환 (낮에도 접근 가능)
        /// </summary>
        public void OnDraculaLordDefeated()
        {
            _isConquered = true;
            if (_draculaState != null)
            {
                _draculaState.isActive = true;
                _draculaState.ownership = TerritoryOwnership.PlayerOwned;
                _draculaState.lordDefeated = true;
                // NOTE: lordSurrendered는 별도 항복 시에만 true — 패배(처치)와는 다른 개념
            }
            if (_verbose)
                Debug.Log("[DraculaTerritoryController] 드라큘라 점령 완료! 영지 영구 활성화.");
        }

        /// <summary>
        /// 현재 드라큘라 영지가 활성화 상태인지 확인
        /// </summary>
        public bool IsTerritoryActive()
        {
            return _draculaState != null && _draculaState.isActive;
        }

        /// <summary>
        /// 점령되었는지 확인
        /// </summary>
        public bool IsConquered => _isConquered;
    }
}