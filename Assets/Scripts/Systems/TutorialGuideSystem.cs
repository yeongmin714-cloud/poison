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
        public struct GuideData
        {
            public string id;
            public string title;
            public string description;
            public string actionTrigger; // TutorialGuideData ID (e.g. "01_movement")
        }

        // T4: 기본 조작 설명창 11종 — ID는 TutorialGuideData 상수와 일치
        public static readonly GuideData[] BasicGuides = new[]
        {
            new GuideData { id = TutorialGuideData.ID_01_MOVEMENT,   title = "이동",     description = "WASD 키로 이동합니다.\nShift 키로 달리기.",                        actionTrigger = TutorialGuideData.ID_01_MOVEMENT },
            new GuideData { id = TutorialGuideData.ID_02_CAMERA,     title = "시점",     description = "마우스 우클릭 드래그로\n시점을 회전합니다.",                       actionTrigger = TutorialGuideData.ID_02_CAMERA },
            new GuideData { id = TutorialGuideData.ID_03_ATTACK,     title = "공격",     description = "좌클릭으로 적을 공격합니다.\n커서 방향의 가장 가까운 적을 자동 조준합니다.",  actionTrigger = TutorialGuideData.ID_03_ATTACK },
            new GuideData { id = TutorialGuideData.ID_04_DASH,       title = "달리기",   description = "Shift 키를 누르면 빠르게 달립니다.",                              actionTrigger = TutorialGuideData.ID_04_DASH },
            new GuideData { id = TutorialGuideData.ID_05_ROLL,       title = "구르기",   description = "Space 키로 구를 수 있습니다\n(무적 판정).",                        actionTrigger = TutorialGuideData.ID_05_ROLL },
            new GuideData { id = TutorialGuideData.ID_06_CHOP_TREE,  title = "나무 채집",description = "E 키로 나무를 캐서\n재료를 얻으세요.",                              actionTrigger = TutorialGuideData.ID_06_CHOP_TREE },
            new GuideData { id = TutorialGuideData.ID_07_MINE_STONE, title = "돌 채집",  description = "E 키로 돌을 캐서\n재료를 얻으세요.",                              actionTrigger = TutorialGuideData.ID_07_MINE_STONE },
            new GuideData { id = TutorialGuideData.ID_08_HERB_PICK,  title = "약초 채집",description = "E 키로 약초를 채집합니다.\n획득한 약초는 인벤토리에서 확인 가능합니다.",   actionTrigger = TutorialGuideData.ID_08_HERB_PICK },
            new GuideData { id = TutorialGuideData.ID_09_INVENTORY,  title = "인벤토리", description = "I 키로 인벤토리를 엽니다.\n획득한 아이템을 확인할 수 있습니다.",        actionTrigger = TutorialGuideData.ID_09_INVENTORY },
            new GuideData { id = TutorialGuideData.ID_10_CRAFT,      title = "제작",     description = "크래프트 테이블에서 E 키로\n아이템을 제작할 수 있습니다.",              actionTrigger = TutorialGuideData.ID_10_CRAFT },
            new GuideData { id = TutorialGuideData.ID_11_RECIPE_BOOK,title = "레시피",   description = "R 키로 레시피 북을 엽니다.\n발견한 조합법을 확인할 수 있습니다.",        actionTrigger = TutorialGuideData.ID_11_RECIPE_BOOK },
        };

        // T6: 영지 최초 액션 설명창 — ID는 TutorialGuideData 상수와 일치
        public static readonly GuideData[] TerritoryGuides = new[]
        {
            new GuideData { id = TutorialGuideData.ID_12_GUARD_INTERACT, title = "병사 상호작용", description = "E 키로 병사에게 말을 걸 수 있습니다.\n호감도에 따라 대화/음식/약물 제공이 가능합니다.", actionTrigger = TutorialGuideData.ID_12_GUARD_INTERACT },
            new GuideData { id = TutorialGuideData.ID_13_GUARD_INFO,     title = "병사 정보",     description = "병사 정보창에서 레벨,\n호감도, 중독도를 확인하세요.",                      actionTrigger = TutorialGuideData.ID_13_GUARD_INFO },
            new GuideData { id = TutorialGuideData.ID_14_GUARD_EQUIP,    title = "장비 지급",     description = "병사에게 장비를 지급하면\n전투력이 상승합니다.",                          actionTrigger = TutorialGuideData.ID_14_GUARD_EQUIP },
            new GuideData { id = TutorialGuideData.ID_16_GAS_SPRAYER,    title = "가스 분사기",  description = "가스 분사기를 Back 슬롯에\n장착해 사용하세요.",                           actionTrigger = TutorialGuideData.ID_16_GAS_SPRAYER },
            new GuideData { id = TutorialGuideData.ID_17_GUARD_MISSION,  title = "병사 임무",    description = "병사에게 특사/정보원/약초꾼/사냥꾼/광부 임무를\n지시할 수 있습니다.",       actionTrigger = TutorialGuideData.ID_17_GUARD_MISSION },
            new GuideData { id = TutorialGuideData.ID_18_SHOP,           title = "상점",         description = "상점에서 무기/방어구/물약을 구매하고\n불필요한 아이템을 판매할 수 있습니다.",      actionTrigger = TutorialGuideData.ID_18_SHOP },
            new GuideData { id = TutorialGuideData.ID_20_STATUS,         title = "스테이터스",   description = "C 키로 캐릭터 정보와\n장비창을 확인할 수 있습니다.",                     actionTrigger = TutorialGuideData.ID_20_STATUS },
            new GuideData { id = TutorialGuideData.ID_22_BUILDING_ENTER, title = "실내 진입",    description = "건물 문 앞에서 E 키로\n건물 내부에 진입할 수 있습니다.",                 actionTrigger = TutorialGuideData.ID_22_BUILDING_ENTER },
        };

        // 상태 관리
        private Queue<GuideData> _guideQueue = new Queue<GuideData>();
        private GuideData? _currentGuide;
        private bool _isShowingGuide;
        private float _guideShowTime;

        [SerializeField] private float _guideDuration = 5f;

        /// <summary>가이드가 표시/숨김될 때 발생하는 이벤트</summary>
        public System.Action<string, bool> OnGuideProcessed;
        
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

            GuideData? match = null;
            foreach (var g in BasicGuides) { if (g.id == actionId) { match = g; break; } }
            if (match == null)
            {
                foreach (var g in TerritoryGuides) { if (g.id == actionId) { match = g; break; } }
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
            bool wasEscPressed = Input.GetKeyDown(KeyCode.Escape);
            _currentGuide = null;
            OnGuideProcessed?.Invoke(lastId, wasEscPressed);
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
