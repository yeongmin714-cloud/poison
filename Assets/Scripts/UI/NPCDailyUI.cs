using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 🏘️ NPC 일상 사이클 월드스페이스 UI — NPC 위에 말풍선으로 현재 상태 표시.
    /// IMGUI singleton, Camera.WorldToScreenPoint 사용.
    ///
    /// 표시 내용 (NPCDailyCycle 시간대 기반):
    ///   - 낮: "🛒 영업 중" (상점) / "🚶 일하는 중" (일반)
    ///   - 밤: "😴 잠자는 중"
    ///   - 저녁: "🏠 귀가 중"
    ///   - 새벽: "🔰 준비 중" (상점) / "🌅 출근 중" (일반)
    /// </summary>
    [RequireComponent(typeof(NPCDailyCycle))]
    public class NPCDailyUI : MonoBehaviour
    {
        public static NPCDailyUI Instance { get; private set; }

        [Header("표시 설정")]
        [SerializeField] private float _bubbleOffsetY = 2.5f; // NPC 머리 위 오프셋
        [SerializeField] private float _bubbleWidth = 140f;
        [SerializeField] private float _bubbleHeight = 28f;
        [SerializeField] private float _maxDrawDistance = 30f; // 최대 표시 거리
        [SerializeField] private bool _showAlways;            // 디버그: 거리 무시

        [Header("스타일")]
        [SerializeField] private Color _bubbleBgColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color _bubbleTextColor = Color.white;
        [SerializeField] private int _bubbleFontSize = 12;

        // 캐시
        private NPCDailyCycle _dailyCycle;
        private Camera _mainCamera;
        private Transform _player;
        private GUIStyle _bubbleStyle;
        private Texture2D _bgTex;
        private bool _stylesInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _dailyCycle = GetComponent<NPCDailyCycle>();
            if (_dailyCycle == null)
            {
                Debug.LogWarning("[NPCDailyUI] NPCDailyCycle이 없습니다. 비활성화합니다.");
                enabled = false;
                return;
            }

            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogWarning("[NPCDailyUI] Camera.main을 찾을 수 없습니다. 비활성화합니다.");
                enabled = false;
            }

            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void OnDestroy()
        {
            if (_bgTex != null)
            {
                Destroy(_bgTex);
                _bgTex = null;
            }

            if (Instance == this)
                Instance = null;
        }

        // ================================================================
        // GUIStyle 초기화 (GC-safe)
        // ================================================================

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            // 배경 텍스처
            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, Color.white);
            _bgTex.Apply();

            _bubbleStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = _bubbleFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = _bubbleTextColor,
                    background = _bgTex
                },
                border = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(4, 2, 2, 2)
            };
        }

        // ================================================================
        // OnGUI — 월드스페이스 말풍선 렌더링
        // ================================================================

        private void OnGUI()
        {
            if (_dailyCycle == null || _mainCamera == null) return;
            if (!_stylesInitialized) EnsureStyles();

            // 현재 시간대가 Night면 말풍선 표시 안 함 (NPC가 비활성화 상태)
            if (_dailyCycle.CurrentPeriod == NPCDailyCycle.TimePeriod.Night && !_showAlways)
                return;

            var allNPCs = _dailyCycle.GetAllNPCs();
            if (allNPCs.Count == 0) return;

            // 캐시된 플레이어 참조 갱신
            if (_player == null || !_player.gameObject.activeInHierarchy)
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;

            foreach (GameObject npcObj in allNPCs)
            {
                if (npcObj == null) continue;

                // 비활성화된 NPC는 말풍선 스킵
                if (!npcObj.activeInHierarchy && !_showAlways)
                    continue;

                // 거리 체크
                if (_player != null && !_showAlways)
                {
                    float dist = Vector3.Distance(npcObj.transform.position, _player.position);
                    if (dist > _maxDrawDistance)
                        continue;
                }

                // 월드 → 스크린 좌표 변환
                Vector3 worldPos = npcObj.transform.position + Vector3.up * _bubbleOffsetY;
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

                // 카메라 뒤쪽에 있으면 스킵
                if (screenPos.z < 0)
                    continue;

                // 상태 텍스트 가져오기
                string statusText = _dailyCycle.GetNPCStatusTextFor(npcObj);
                if (string.IsNullOrEmpty(statusText))
                    continue;

                // 화면 좌표 계산 (Y는 Unity 하단 기준 → IMGUI 상단 기준 변환)
                float x = screenPos.x - _bubbleWidth * 0.5f;
                float y = Screen.height - screenPos.y - _bubbleHeight * 0.5f;

                // 배경색 적용
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = _bubbleBgColor;

                // 말풍선 그리기
                GUI.Box(new Rect(x, y, _bubbleWidth, _bubbleHeight), statusText, _bubbleStyle);

                GUI.backgroundColor = prevBg;
            }
        }
    }
}