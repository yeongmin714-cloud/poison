using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// P30-E: 영주 Placeholder 상호작용 훅 — 음식주기(E키).
    /// LordSurrenderSystem.SpawnLordPlaceholder가 영주 큐브([Lord] ...)에 부착.
    ///
    /// - 접근 범위 내 E키 → 문 개방 영지(IsLordDoorOpen — 병사/영주 중독 임계 이상)에서만
    ///   OnFeedRequested 이벤트 발화 → LordFeedWindowUTK(UI/Toolkit)가 구독해
    ///   음식(Food) 선택/지급 창을 연다.
    /// - 문이 잠긴 영지에서는 상호작용이 거절되고 로그로 안내한다.
    /// </summary>
    public class LordFeedTarget : MonoBehaviour
    {
        /// <summary>영주 음식주기 요청 (territoryKey, lordName) — LordFeedWindowUTK(UI)가 구독.</summary>
        public static event System.Action<string, string> OnFeedRequested;

        [Header("영주 식별 (SpawnLordPlaceholder에서 설정)")]
        [SerializeField] private string _territoryKey = "";
        [SerializeField] private string _lordName = "영주";

        [Header("상호작용 설정")]
        [SerializeField] private float _interactRange = 4f;

        private Transform _player;
        private Camera _mainCamera;

        /// <summary>소속 영지 키 ("East_01" 형식 string).</summary>
        public string TerritoryKey
        {
            get => _territoryKey;
            set => _territoryKey = value ?? string.Empty;
        }

        /// <summary>영주 이름 (이름표/창 제목 표기).</summary>
        public string LordName
        {
            get => _lordName;
            set => _lordName = string.IsNullOrEmpty(value) ? "영주" : value;
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning("[LordFeedTarget] Player 태그 오브젝트 없음 — 음식주기 상호작용 불가");

            _mainCamera = Camera.main;
            if (_mainCamera == null)
                Debug.LogWarning("[LordFeedTarget] MainCamera 태그 오브젝트 없음 — 거리 판정은 정상 동작");
        }

        private void Update()
        {
            if (_player == null) return;
            if (string.IsNullOrEmpty(_territoryKey)) return;

            // 창이 열려 있으면 상호작용 금지 (U8 규약 — 창 입력 중복 방지)
            if (UITransitionState.AnyWindowOpen) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > _interactRange) return;

            bool requested = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F);
            if (!requested) return;

            // 문 개방 게이트 — 영주 중독도(=영지 오염도)가 임계 이상(문 열림)일 때만
            if (!TerritoryLordDoorSystem.IsLordDoorOpen(_territoryKey))
            {
                float lordAddiction = TerritoryDrugSystem.GetLordAddiction(_territoryKey);
                Debug.Log($"[LordFeed] 🔒 {_lordName}: 영주실 문이 잠겨 있어 음식을 받을 수 없다. " +
                          $"(영주 중독도 {lordAddiction:F1} < {TerritoryLordDoorSystem.DOOR_OPEN_ADDICTION_THRESHOLD})");
                return;
            }

            Debug.Log($"[LordFeed] E키 — 영주 음식주기 창 요청: {_lordName} ({_territoryKey})");
            OnFeedRequested?.Invoke(_territoryKey, _lordName);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = TerritoryLordDoorSystem.IsLordDoorOpen(_territoryKey) ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}
