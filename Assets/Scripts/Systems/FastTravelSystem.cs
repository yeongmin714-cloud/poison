using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// ⚡ 빠른 이동 시스템 — 플레이어가 소유한 영지 사이를 즉시 이동합니다.
    /// Ring 거리에 따라 골드를 소모하며, 3초 로딩 화면 후 텔레포트됩니다.
    /// </summary>
    [DefaultExecutionOrder(-40)] // AutoMoveManager(-50)보다 약간 늦게
    public class FastTravelSystem : MonoBehaviour
    {
        private static FastTravelSystem _instance;
        public static FastTravelSystem Instance => _instance;

        [Header("Fast Travel Settings")]
        [SerializeField] private float _loadingDuration = 3f;

        // ===== 이벤트 =====
        public event Action OnFastTravelStart;
        public event Action OnFastTravelComplete;

        // ===== 생명주기 =====
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ===== 공개 API =====

        /// <summary>
        /// 플레이어가 소유한 모든 영지 목록을 반환합니다. (현재 위치한 영지는 제외)
        /// </summary>
        public List<TerritoryDefinition> GetPlayerOwnedTerritories()
        {
            var owned = new List<TerritoryDefinition>();
            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                Debug.LogError("[FastTravelSystem] TerritoryDatabase.Instance가 null입니다.");
                return owned;
            }

            // 현재 영지 ID
            TerritoryId currentId = default;
            if (TerritoryManager.Instance != null)
                currentId = TerritoryManager.Instance.CurrentTerritoryId;

            foreach (var def in db.GetAllDefinitions())
            {
                // 유효하지 않은 영지는 건너뜀
                if (def.id.nation == NationType.None)
                    continue;

                // 현재 영지는 제외
                if (def.id.nation == currentId.nation && def.id.index == currentId.index)
                    continue;

                var state = db.GetState(def.id);
                if (state != null && state.ownership == TerritoryOwnership.PlayerOwned)
                {
                    owned.Add(def);
                }
            }

            return owned;
        }

        /// <summary>
        /// Ring 난이도에 따른 이동 비용을 반환합니다.
        /// Ring1=5G, Ring2=10G, Ring3=15G, Ring4=20G, Empire=25G
        /// </summary>
        public int GetTravelCost(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => 5,
                TerritoryDifficulty.Ring2 => 10,
                TerritoryDifficulty.Ring3 => 15,
                TerritoryDifficulty.Ring4 => 20,
                TerritoryDifficulty.Empire => 25,
                _ => 5
            };
        }

        /// <summary>
        /// 플레이어가 지정된 비용을 지불할 수 있는지 확인합니다.
        /// </summary>
        public bool CanAffordTravel(int cost)
        {
            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[FastTravelSystem] PlayerInventory.Instance가 null입니다.");
                return false;
            }
            return PlayerInventory.Instance.GetItemCount("gold") >= cost;
        }

        /// <summary>
        /// 빠른 이동을 실행합니다.
        /// 골드 차감 → 3초 로딩 화면 → 플레이어 텔레포트 → 완료 이벤트
        /// </summary>
        /// <param name="targetId">이동할 목표 영지 ID</param>
        public void ExecuteFastTravel(TerritoryId targetId)
        {
            // DB 확인
            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                Debug.LogError("[FastTravelSystem] TerritoryDatabase.Instance가 null입니다.");
                return;
            }

            // 영지 정의 확인
            var def = db.GetDefinition(targetId);
            if (def.id.nation == NationType.None)
            {
                Debug.LogError($"[FastTravelSystem] 유효하지 않은 영지 ID: {targetId}");
                return;
            }

            // 소유 확인
            var state = db.GetState(targetId);
            if (state == null || state.ownership != TerritoryOwnership.PlayerOwned)
            {
                Debug.LogWarning($"[FastTravelSystem] 소유하지 않은 영지로 이동 시도: {def.territoryName}");
                return;
            }

            // 현재 영지 확인 (같은 영지로 이동 방지)
            if (TerritoryManager.Instance != null)
            {
                var currentId = TerritoryManager.Instance.CurrentTerritoryId;
                if (currentId.nation == targetId.nation && currentId.index == targetId.index)
                {
                    Debug.Log("[FastTravelSystem] 이미 해당 영지에 있습니다.");
                    return;
                }
            }

            // 비용 계산
            int cost = GetTravelCost(def.difficulty);

            // 골드 확인
            if (!CanAffordTravel(cost))
            {
                int currentGold = PlayerInventory.Instance != null
                    ? PlayerInventory.Instance.GetItemCount("gold")
                    : 0;
                Debug.Log($"[FastTravelSystem] ⚠️ 골드 부족! 필요: {cost}G, 보유: {currentGold}G");
                return;
            }

            // === 실행 ===
            OnFastTravelStart?.Invoke();
            Debug.Log($"[FastTravelSystem] 🚀 빠른 이동 시작: {def.territoryName} (비용: {cost}G, Ring: {def.difficulty})");

            // 골드 차감
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.RemoveItem("gold", cost);

            // 로딩 화면 후 텔레포트 코루틴 시작
            if (gameObject.activeInHierarchy)
                StartCoroutine(FastTravelCoroutine(targetId, def.territoryName));
        }

        // ===== 내부 메서드 =====

        /// <summary>
        /// 빠른 이동 코루틴 — 로딩 화면 표시 후 텔레포트합니다.
        /// </summary>
        private IEnumerator FastTravelCoroutine(TerritoryId targetId, string territoryName)
        {
            // 로딩 화면 시작
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.StartLoading(0.3f);
            }
            else
            {
                Debug.LogWarning("[FastTravelSystem] LoadingManager.Instance가 없습니다. 로딩 화면 없이 진행합니다.");
            }

            // 3초 로딩 진행률 시뮬레이션
            float elapsed = 0f;
            while (elapsed < _loadingDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _loadingDuration);
                if (LoadingManager.Instance != null)
                    LoadingManager.Instance.SetProgress(progress);
                yield return null;
            }

            // 진행률 100%
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.SetProgress(1f);

            // 플레이어 텔레포트
            TeleportPlayer(targetId);

            // 잠시 대기 후 로딩 완료
            yield return new WaitForSeconds(0.3f);

            if (LoadingManager.Instance != null)
                LoadingManager.Instance.CompleteLoading(0.3f);

            Debug.Log($"[FastTravelSystem] ✅ 빠른 이동 완료: {territoryName}");
            OnFastTravelComplete?.Invoke();
        }

        /// <summary>
        /// 플레이어를 목표 영지 중심 위치로 텔레포트합니다.
        /// </summary>
        private void TeleportPlayer(TerritoryId targetId)
        {
            // 1. 플레이어 GameObject 찾기
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                var pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null)
                    player = pm.gameObject;
            }

            if (player == null)
            {
                Debug.LogError("[FastTravelSystem] 플레이어를 찾을 수 없어 텔레포트할 수 없습니다!");
                return;
            }

            // 2. 목표 위치 계산
            Vector3 targetPos = Vector3.zero;

            // TerritoryManager의 GetTerritoryCenter 사용 (현재 영지의 건물 중심)
            if (TerritoryManager.Instance != null)
            {
                targetPos = TerritoryManager.Instance.GetTerritoryCenter(targetId);
            }

            // 중심 위치가 0이면 대략적 위치 사용
            if (targetPos == Vector3.zero)
            {
                // 인덱스 기반 대략적 위치 (실제 게임에 맞게 조정 필요)
                float x = targetId.index * 8f;
                float z = (int)targetId.nation * 12f;
                targetPos = new Vector3(x, 1f, z);
            }

            // 3. CharacterController 비활성화 후 위치 변경 (충돌 방지)
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            player.transform.position = targetPos;

            // 4. AutoMoveManager가 있으면 현재 이동 취소
            if (AutoMoveManager.Instance != null && AutoMoveManager.Instance.IsMoving)
                AutoMoveManager.Instance.CancelAutoMove("빠른 이동");

            if (cc != null)
                cc.enabled = true;

            Debug.Log($"[FastTravelSystem] 플레이어 텔레포트 완료 → {targetPos}");

            // 5. TerritoryManager 현재 영지 업데이트
            UpdateCurrentTerritory(targetId);
        }

        /// <summary>
        /// TerritoryManager의 현재 영지 정보를 업데이트합니다.
        /// (private 필드이므로 리플렉션 사용)
        /// </summary>
        private static void UpdateCurrentTerritory(TerritoryId targetId)
        {
            if (TerritoryManager.Instance == null) return;

            try
            {
                var type = typeof(TerritoryManager);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;

                var nationField = type.GetField("_currentNation", flags);
                var indexField = type.GetField("_currentTerritoryIndex", flags);

                if (nationField != null)
                    nationField.SetValue(TerritoryManager.Instance, targetId.nation);

                if (indexField != null)
                    indexField.SetValue(TerritoryManager.Instance, targetId.index);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastTravelSystem] TerritoryManager 필드 업데이트 실패: {ex.Message}");
            }
        }
    }
}