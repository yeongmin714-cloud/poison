using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// T-Cycle-01: 튜토리얼 가이드 시스템.
    /// 최초 액션 감지 시 해당 설명창 표시.
    /// PlayerPrefs로 확인한 액션 저장 (재접속 시 스킵).
    /// </summary>
    public class TutorialGuideSystem : MonoBehaviour
    {
        public static TutorialGuideSystem Instance { get; private set; }

        [System.Serializable]
        public struct GuideData
        {
            public string id;
            public string title;
            public string description;
            public string actionTrigger; // "W" "E" "LeftClick" "I" 등
        }

        // T4: 기본 조작 설명창 11종
        public static readonly GuideData[] BasicGuides = new[]
        {
            new GuideData { id = "t_move",     title = "이동",     description = "WASD 키로 이동합니다.\nShift 키로 달리기.",                        actionTrigger = "W" },
            new GuideData { id = "t_camera",   title = "시점",     description = "마우스를 화면 가장자리로 이동하여\n시점을 회전합니다.",                 actionTrigger = "MouseEdge" },
            new GuideData { id = "t_attack",   title = "공격",     description = "좌클릭으로 적을 공격합니다.\n커서 방향의 가장 가까운 적을 자동 조준합니다.", actionTrigger = "LeftClick" },
            new GuideData { id = "t_sprint",   title = "달리기",   description = "Shift 키를 누르면 빠르게 달립니다.",                              actionTrigger = "Shift" },
            new GuideData { id = "t_jump",     title = "점프",     description = "Space 키로 점프합니다.",                                         actionTrigger = "Space" },
            new GuideData { id = "t_interact", title = "상호작용", description = "E 키로 약초 채집, NPC 대화,\n크래프트 테이블을 사용합니다.",          actionTrigger = "E" },
            new GuideData { id = "t_inventory",title = "인벤토리", description = "I 키로 인벤토리를 엽니다.\n획득한 아이템을 확인할 수 있습니다.",       actionTrigger = "I" },
            new GuideData { id = "t_recipe",   title = "레시피",   description = "R 키로 레시피 북을 엽니다.\n발견한 조합법을 확인할 수 있습니다.",       actionTrigger = "R" },
            new GuideData { id = "t_zoom",     title = "줌",       description = "마우스 휠로 줌 인/아웃합니다.",                                    actionTrigger = "ScrollWheel" },
            new GuideData { id = "t_map",      title = "지도",     description = "M 키로 월드맵을 엽니다.\n점령한 영지와 현재 위치를 확인할 수 있습니다.", actionTrigger = "M" },
            new GuideData { id = "t_quest",    title = "퀘스트",   description = "Q 키로 퀘스트 창을 엽니다.",                                        actionTrigger = "Q" },
        };

        // T6: 영지 최초 액션 설명창
        public static readonly GuideData[] TerritoryGuides = new[]
        {
            new GuideData { id = "t_guard",     title = "병사 상호작용", description = "E 키로 병사에게 말을 걸 수 있습니다.\n호감도에 따라 대화/음식/약물 제공이 가능합니다.", actionTrigger = "GuardInteraction" },
            new GuideData { id = "t_equip",     title = "장비",         description = "인벤토리에서 병사에게 장비를 지급할 수 있습니다.",                      actionTrigger = "Equipment" },
            new GuideData { id = "t_sprayer",   title = "가스 분사기",  description = "가스 분사기를 등에 장착하여\n독/치료/마약 안개를 분사할 수 있습니다.",      actionTrigger = "GasSprayer" },
            new GuideData { id = "t_guardmission", title = "병사 임무", description = "병사에게 특사/정보원/약초꾼/사냥꾼/광부 임무를\n지시할 수 있습니다.",       actionTrigger = "GuardMission" },
            new GuideData { id = "t_shop",      title = "상점",         description = "상점에서 무기/방어구/물약을 구매하고\n불필요한 아이템을 판매할 수 있습니다.",      actionTrigger = "Shop" },
            new GuideData { id = "t_status",    title = "스테이터스",   description = "C 키로 캐릭터 정보와\n장비창을 확인할 수 있습니다.",                     actionTrigger = "Status" },
            new GuideData { id = "t_indoor",    title = "실내 진입",    description = "건물 문 앞에서 E 키로\n건물 내부에 진입할 수 있습니다.",                 actionTrigger = "IndoorScene" },
            new GuideData { id = "t_repair",    title = "장비 수리",    description = "크래프트 테이블에서\n손상된 장비를 수리할 수 있습니다.",                actionTrigger = "Repair" },
        };

        // 상태 관리
        private Queue<GuideData> _guideQueue = new Queue<GuideData>();
        private GuideData? _currentGuide;
        private bool _isShowingGuide;
        private float _guideShowTime;

        [SerializeField] private float _guideDuration = 5f;

        /// <summary>가이드가 표시/숨김될 때 발생하는 이벤트</summary>
        public System.Action<string, bool> OnGuideProcessed;
        
        /// <summary>현재 가이드가 표시 중인가?</summary>
        public bool IsGuideShown(string guideId)
        {
            return PlayerPrefs.HasKey($"guide_{guideId}");
        }

        /// <summary>TutorialActionDetector에서 호출 — 특정 액션 가이드 표시</summary>
        public void ShowGuide(string actionId)
        {
            TriggerGuide(actionId);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // 기본 조작 가이드 큐에 추가 (아직 확인 안된 것만)
            foreach (var guide in BasicGuides)
            {
                if (!PlayerPrefs.HasKey($"guide_{guide.id}"))
                {
                    _guideQueue.Enqueue(guide);
                }
            }
        }

        private void Update()
        {
            // 가이드 자동 표시
            if (!_isShowingGuide && _guideQueue.Count > 0)
            {
                _currentGuide = _guideQueue.Dequeue();
                _isShowingGuide = true;
                _guideShowTime = _guideDuration;
                OnGuideProcessed?.Invoke(_currentGuide?.id ?? "", false);
            }

            if (_isShowingGuide)
            {
                _guideShowTime -= Time.deltaTime;
                if (_guideShowTime <= 0f || Input.GetKeyDown(KeyCode.Escape))
                {
                    HideGuide();
                }
            }
        }

        /// <summary>특정 액션을 감지하여 트리거된 가이드 표시</summary>
        public void TriggerGuide(string actionId)
        {
            if (PlayerPrefs.HasKey($"guide_{actionId}")) return; // 이미 확인 완료

            GuideData? match = null;
            foreach (var g in BasicGuides) { if (g.actionTrigger == actionId) { match = g; break; } }
            if (match == null)
            {
                foreach (var g in TerritoryGuides) { if (g.actionTrigger == actionId) { match = g; break; } }
            }

            if (match.HasValue)
            {
                PlayerPrefs.SetInt($"guide_{match.Value.id}", 1);
                PlayerPrefs.Save();
                _guideQueue.Enqueue(match.Value);
            }
        }

        private void HideGuide()
        {
            if (_currentGuide.HasValue)
            {
                PlayerPrefs.SetInt($"guide_{_currentGuide.Value.id}", 1);
                PlayerPrefs.Save();
            }
            _isShowingGuide = false;
            var lastId = _currentGuide?.id ?? "";
            _currentGuide = null;
            OnGuideProcessed?.Invoke(lastId, true);
        }

        /// <summary>OnGUI로 가이드 팝업 렌더링</summary>
        public void OnGuideGUI()
        {
            if (!_isShowingGuide || !_currentGuide.HasValue) return;

            var guide = _currentGuide.Value;
            float boxW = 350f;
            float boxH = 180f;
            float x = (Screen.width - boxW) / 2f;
            float y = (Screen.height - boxH) / 2f;

            // 반투명 배경
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Box(new Rect(x, y, boxW, boxH), "");

            // 타이틀
            GUI.color = Color.yellow;
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect(x, y + 8, boxW, 28), guide.title, titleStyle);

            // 설명
            GUI.color = Color.white;
            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(x + 15, y + 40, boxW - 30, 100), guide.description, descStyle);

            // 닫기 안내
            GUI.color = Color.gray;
            var closeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.LowerRight,
                normal = { textColor = Color.gray }
            };
            GUI.Label(new Rect(x + boxW - 120, y + boxH - 20, 110, 16), "ESC 키로 닫기", closeStyle);

            GUI.color = Color.white;
        }
    }
}
