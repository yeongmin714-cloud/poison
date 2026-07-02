using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Data;

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
        public readonly struct GuideData
        {
            public readonly string id;
            public readonly string title;
            public readonly string description;
            public readonly string actionTrigger;

            public GuideData(string id, string title, string description, string actionTrigger)
            {
                this.id = id;
                this.title = title;
                this.description = description;
                this.actionTrigger = actionTrigger;
            }

            public GuideData(GuideEntry entry)
            {
                id = entry.Id;
                title = entry.Title;
                description = entry.Description;
                actionTrigger = entry.ActionTrigger;
            }
        }

        // T4: 기본 조작 설명창 11종 — ID는 TutorialGuideData 상수와 일치
        private static readonly GuideData[] BasicGuides = new[]
        {
            new GuideData(TutorialGuideData.ID_01_MOVEMENT,   "이동",     "WASD 키로 이동합니다.\nShift 키로 달리기.",                        TutorialGuideData.ID_01_MOVEMENT),
            new GuideData(TutorialGuideData.ID_02_CAMERA,     "시점",     "마우스 우클릭 드래그로\n시점을 회전합니다.",                       TutorialGuideData.ID_02_CAMERA),
            new GuideData(TutorialGuideData.ID_03_ATTACK,     "공격",     "좌클릭으로 적을 공격합니다.\n커서 방향의 가장 가까운 적을 자동 조준합니다.",  TutorialGuideData.ID_03_ATTACK),
            new GuideData(TutorialGuideData.ID_04_DASH,       "달리기",   "Shift 키를 누르면 빠르게 달립니다.",                              TutorialGuideData.ID_04_DASH),
            new GuideData(TutorialGuideData.ID_05_ROLL,       "구르기",   "Space 키로 구를 수 있습니다\n(무적 판정).",                        TutorialGuideData.ID_05_ROLL),
            new GuideData(TutorialGuideData.ID_06_CHOP_TREE,  "나무 채집","E 키로 나무를 캐서\n재료를 얻으세요.",                              TutorialGuideData.ID_06_CHOP_TREE),
            new GuideData(TutorialGuideData.ID_07_MINE_STONE, "돌 채집",  "E 키로 돌을 캐서\n재료를 얻으세요.",                              TutorialGuideData.ID_07_MINE_STONE),
            new GuideData(TutorialGuideData.ID_08_HERB_PICK,  "약초 채집","E 키로 약초를 채집합니다.\n획득한 약초는 인벤토리에서 확인 가능합니다.",   TutorialGuideData.ID_08_HERB_PICK),
            new GuideData(TutorialGuideData.ID_09_INVENTORY,  "인벤토리", "I 키로 인벤토리를 엽니다.\n획득한 아이템을 확인할 수 있습니다.",        TutorialGuideData.ID_09_INVENTORY),
            new GuideData(TutorialGuideData.ID_10_CRAFT,      "제작",     "크래프트 테이블에서 E 키로\n아이템을 제작할 수 있습니다.",              TutorialGuideData.ID_10_CRAFT),
            new GuideData(TutorialGuideData.ID_11_RECIPE_BOOK,"레시피",   "R 키로 레시피 북을 엽니다.\n발견한 조합법을 확인할 수 있습니다.",        TutorialGuideData.ID_11_RECIPE_BOOK),
        };

        // T6: 영지 최초 액션 설명창 — ID는 TutorialGuideData 상수와 일치
        private static readonly GuideData[] TerritoryGuides = new[]
        {
            new GuideData(TutorialGuideData.ID_12_GUARD_INTERACT, "병사 상호작용", "E 키로 병사에게 말을 걸 수 있습니다.\n호감도에 따라 대화/음식/약물 제공이 가능합니다.", TutorialGuideData.ID_12_GUARD_INTERACT),
            new GuideData(TutorialGuideData.ID_13_GUARD_INFO,     "병사 정보",     "병사 정보창에서 레벨,\n호감도, 중독도를 확인하세요.",                      TutorialGuideData.ID_13_GUARD_INFO),
            new GuideData(TutorialGuideData.ID_14_GUARD_EQUIP,    "장비 지급",     "병사에게 장비를 지급하면\n전투력이 상승합니다.",                          TutorialGuideData.ID_14_GUARD_EQUIP),
            new GuideData(TutorialGuideData.ID_15_GASMASK,        "방독면",        "방독면을 장착하면\n독안개를 막을 수 있습니다.",                          TutorialGuideData.ID_15_GASMASK),
            new GuideData(TutorialGuideData.ID_16_GAS_SPRAYER,    "가스 분사기",  "가스 분사기를 Back 슬롯에\n장착해 사용하세요.",                           TutorialGuideData.ID_16_GAS_SPRAYER),
            new GuideData(TutorialGuideData.ID_17_GUARD_MISSION,  "병사 임무",    "병사에게 특사/정보원/약초꾼/사냥꾼/광부 임무를\n지시할 수 있습니다.",       TutorialGuideData.ID_17_GUARD_MISSION),
            new GuideData(TutorialGuideData.ID_18_SHOP,           "상점",         "상점에서 무기/방어구/물약을 구매하고\n불필요한 아이템을 판매할 수 있습니다.",      TutorialGuideData.ID_18_SHOP),
            new GuideData(TutorialGuideData.ID_19_WORLD_MAP,      "월드맵",       "M 키로 월드맵을 열어\n전체 영지를 확인하세요.",                           TutorialGuideData.ID_19_WORLD_MAP),
            new GuideData(TutorialGuideData.ID_20_STATUS,         "스테이터스",   "C 키로 캐릭터 정보와\n장비창을 확인할 수 있습니다.",                     TutorialGuideData.ID_20_STATUS),
            new GuideData(TutorialGuideData.ID_22_BUILDING_ENTER, "실내 진입",    "건물 문 앞에서 E 키로\n건물 내부에 진입할 수 있습니다.",                 TutorialGuideData.ID_22_BUILDING_ENTER),
        };

        // 상태 관리
        private readonly Queue<GuideData> _guideQueue = new Queue<GuideData>();
        private GuideData? _currentGuide;
        private bool _isShowingGuide;
        private float _guideShowTime;

        [SerializeField] private float _guideDuration = 5f;

        // 캐시된 GUIStyle 인스턴스 (프레임당 할당 방지)
        private GUIStyle _titleStyle;
        private GUIStyle _descStyle;
        private GUIStyle _closeStyle;
        private bool _stylesInitialized;

        /// <summary>가이드가 표시/숨김될 때 발생하는 이벤트</summary>
        public Action<string, bool> OnGuideProcessed;
        
        /// <summary>해당 가이드가 이미 PlayerPrefs에 기록되었는지 (과거에 본 적이 있는지) 확인</summary>
        public bool HasGuideBeenShown(string guideId)
        {
            return PlayerPrefs.HasKey($"guide_{guideId}");
        }

        /// <summary>TutorialActionDetector 등에서 호출 — 특정 액션 가이드 표시 요청</summary>
        public void ShowGuide(string actionId)
        {
            TriggerGuide(actionId);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
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

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            float subScale = ProjectName.Systems.AccessibilityManager.SubtitleScale;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20 * subScale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.yellow }
            };

            _descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14 * subScale),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            _closeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11 * subScale),
                alignment = TextAnchor.LowerRight,
                normal = { textColor = Color.gray }
            };

            _stylesInitialized = true;
        }

        private void Update()
        {
            // 가이드 자동 표시
            if (!_isShowingGuide && _guideQueue.Count > 0)
            {
                _currentGuide = _guideQueue.Dequeue();
                _isShowingGuide = true;
                _guideShowTime = _guideDuration;
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

            GuideData? match = FindGuideById(actionId);

            if (match.HasValue)
            {
                PlayerPrefs.SetInt($"guide_{match.Value.id}", 1);
                PlayerPrefs.Save();
                _guideQueue.Enqueue(match.Value);
            }
        }

        /// <summary>
        /// ID로 가이드 데이터를 검색합니다.
        /// BasicGuides → TerritoryGuides → TutorialGuideData.AllGuides 순으로 찾습니다.
        /// </summary>
        private static GuideData? FindGuideById(string actionId)
        {
            foreach (var g in BasicGuides)
            {
                if (g.id == actionId) return g;
            }

            foreach (var g in TerritoryGuides)
            {
                if (g.id == actionId) return g;
            }

            // TutorialGuideData.AllGuides에서 찾기 (특수 가이드 포함)
            var entry = TutorialGuideData.FindById(actionId);
            if (entry.HasValue)
            {
                return new GuideData(entry.Value);
            }

            return null;
        }

        public void HideGuide()
        {
            if (_currentGuide.HasValue)
            {
                PlayerPrefs.SetInt($"guide_{_currentGuide.Value.id}", 1);
                PlayerPrefs.Save();
            }
            _isShowingGuide = false;
            var lastId = _currentGuide?.id ?? "";
            bool wasEscPressed = Input.GetKeyDown(KeyCode.Escape);
            _currentGuide = null;
            OnGuideProcessed?.Invoke(lastId, wasEscPressed);
        }

        /// <summary>OnGUI로 가이드 팝업 렌더링 (Unity 자동 호출)</summary>
        private void OnGUI()
        {
            if (!_isShowingGuide || !_currentGuide.HasValue) return;

            var guide = _currentGuide.Value;
            float boxW = 350f;
            float boxH = 180f;
            float x = (Screen.width - boxW) / 2f;
            float y = (Screen.height - boxH) / 2f;

            // 1회성 스타일 초기화
            InitializeStyles();

            // 반투명 배경
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Box(new Rect(x, y, boxW, boxH), "");

            // 타이틀
            GUI.color = Color.yellow;
            GUI.Label(new Rect(x, y + 8, boxW, 28), guide.title, _titleStyle);

            // 설명
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 15, y + 40, boxW - 30, 100), guide.description, _descStyle);

            // 닫기 안내
            GUI.color = Color.gray;
            GUI.Label(new Rect(x + boxW - 120, y + boxH - 20, 110, 16), "ESC 키로 닫기", _closeStyle);

            GUI.color = Color.white;
        }
    }
}
