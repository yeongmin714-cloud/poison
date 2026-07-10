using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 길 잃은 영주 NPC — 튜토리얼 퀘스트.
    /// 배고프다 → 음식 만들기 → 설사약 선택지 → 음식 건네기 → 10초 행동불능 → 처형 → 영지 증서
    /// </summary>
    public class TutorialQuestNPC : MonoBehaviour
    {
        public enum QuestState
        {
            NotStarted,     // 아직 말 안 건넴
            AskingFood,     // "배고프다, 음식을 만들어줘"
            HasPoison,      // 독든 음식을 건네받음
            Poisoned,       // 독에 걸림 (10초 행동불능)
            Dead,           // 처형됨
            Complete        // 퀘스트 완료
        }

        [Header("설정")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private float _poisonDuration = 10f;

        [Header("대사 — 수정은 docs/NPC_DIALOGUES.md 참고")]
        [SerializeField] private string[] _dialogueInit = new[] {
            "어이, 젊은이! 나는 이 지역의 영주다.",
            "길을 잃었는데... 배가 몹시 고프구나.",
            "무언가 먹을 것을 구해다 주겠나?",
            "이 근처에 토끼와 멧돼지가 있으니 사냥을 해보게.",
            "고기로 요리를 만들어 오게. 난 크래프트 테이블에서 기다리겠다."
        };
        [SerializeField] private string[] _dialogueHasFood = new[] {
            "오! 그 냄새! 정말 맛있어 보이는구나!",
            "...잠깐, 이 음식에 뭔가 들어있지 않나?",
            "(당신은 설사약을 넣은 것을 떠올린다)",
            "뭐... 아무것도 아니야. 자, 어서 먹게나."
        };
        [SerializeField] private string[] _dialoguePoisoned = new[] {
            "윽... 배가...!!!",
            "이... 이 음식에 무슨 짓을 한 거냐!",
            "(10초 동안 행동 불능)",
        };
        [SerializeField] private string[] _dialogueAfterDeath = new[] {
            "(영주가 쓰러져 있다)",
            "영지 증서를 가져가자..."
        };

        private Transform _player;
        private QuestState _state = QuestState.NotStarted;
        private int _dialogueIndex = 0;
        private bool _inDialogue = false;
        private float _poisonTimer = 0f;
        private bool _dialogueDismissed = false;

        // Rig animation
        private RigAnimationController _rigAnim;

        private void Awake()
        {
            _rigAnim = GetComponent<RigAnimationController>();
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
            }
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;

            // 기본 Idle 애니메이션
            if (_rigAnim != null) _rigAnim.SetStateImmediate(ProjectName.Systems.AnimationState.Idle);
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            bool nearby = dist <= _interactRange;

            if (_state == QuestState.Poisoned)
            {
                _poisonTimer -= Time.deltaTime;
                if (_poisonTimer <= 0)
                {
                    // 독 효과 끝 → 플레이어가 처형 가능
                    _state = QuestState.Dead;
                    if (_rigAnim != null) _rigAnim.SetStateImmediate(ProjectName.Systems.AnimationState.Idle);
                    Debug.Log("[TutorialQuestNPC] 영주가 쓰러졌다! E 키로 영지 증서 획득");
                }
                return;
            }

            if (nearby && !_inDialogue && Input.GetKeyDown(KeyCode.E))
            {
                if (_state == QuestState.Dead || _state == QuestState.Complete)
                {
                    // 영지 증서 획득
                    if (PlayerInventory.Instance != null)
                    {
                        PlayerInventory.Instance.AddItem(PlayerInventory.EstateDeed, 1);
                        Debug.Log("[TutorialQuestNPC] 영지 증서 획득! 퀘스트 완료!");
                    }
                    _state = QuestState.Complete;
                    return;
                }

                StartDialogue();
            }

            if (_inDialogue && Input.GetKeyDown(KeyCode.E))
                AdvanceDialogue();
        }

        private void StartDialogue()
        {
            _inDialogue = true;
            _dialogueIndex = 0;
            _dialogueDismissed = false;
            ShowDialogueLine();
        }

        private void AdvanceDialogue()
        {
            string[] currentDialogue = GetCurrentDialogue();

            if (_dialogueIndex < currentDialogue.Length - 1)
            {
                _dialogueIndex++;
                ShowDialogueLine();
            }
            else
            {
                // 대화 종료
                _inDialogue = false;
                _dialogueDismissed = true;
                HideDialogueUI();
                OnDialogueEnd();
            }
        }

        private string[] GetCurrentDialogue()
        {
            return _state switch
            {
                QuestState.NotStarted => _dialogueInit,
                QuestState.AskingFood => _dialogueHasFood,
                QuestState.HasPoison => _dialoguePoisoned,
                QuestState.Poisoned => _dialoguePoisoned,
                QuestState.Dead => _dialogueAfterDeath,
                QuestState.Complete => _dialogueAfterDeath,
                _ => _dialogueInit,
            };
        }

        private void ShowDialogueLine()
        {
            string[] dialogue = GetCurrentDialogue();
            if (_dialogueIndex < dialogue.Length)
            {
                string text = dialogue[_dialogueIndex];
                Debug.Log($"[NPC 영주] {text}");
                
                // NPCDialogueWindow가 있으면 연동
                if (NPCDialogueWindow.Instance != null)
                {
                    // 간단한 텍스트 표시를 위해 라인 구성
                    var lines = new System.Collections.Generic.List<string>();
                    lines.Add($"\"{text}\"");
                    // TODO: Phase 2 — NPCDialogueWindow와 통합
                }
                
                _currentDialogueText = text;
                _showDialogueText = true;
                _dialogueTextTimer = 0f;
            }
        }

        private void HideDialogueUI()
        {
            _showDialogueText = false;
            _currentDialogueText = "";
        }

        // IMGUI 대화 텍스트 표시 (NPCDialogueWindow가 없을 때 폴백)
        private string _currentDialogueText = "";
        private bool _showDialogueText = false;
        private float _dialogueTextTimer = 0f;

        private void OnGUI()
        {
            if (!_showDialogueText || string.IsNullOrEmpty(_currentDialogueText)) return;

            // 화면 하단 중앙에 말풍선 표시
            float labelWidth = Mathf.Min(700, Screen.width * 0.8f);
            float labelHeight = 80f;
            float x = (Screen.width - labelWidth) / 2f;
            float y = Screen.height - labelHeight - 60f;

            // 배경 상자
            GUI.Box(new Rect(x, y, labelWidth, labelHeight), "");

            // NPC 이름
            GUI.Label(new Rect(x + 10, y + 5, labelWidth - 20, 25),
                "👑 길 잃은 영주", new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                    alignment = TextAnchor.MiddleLeft
                });

            // 대화 텍스트
            GUI.Label(new Rect(x + 10, y + 30, labelWidth - 20, 45),
                _currentDialogueText, new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft
                });
        }

        private void OnDialogueEnd()
        {
            switch (_state)
            {
                case QuestState.NotStarted:
                    // 첫 대화 완료 → 음식 요구 단계
                    _state = QuestState.AskingFood;
                    Debug.Log("[TutorialQuestNPC] 퀘스트 업데이트: 음식을 만들어 영주에게 가져가라");
                    break;

                case QuestState.AskingFood:
                    // 플레이어가 음식을 가져왔다고 가정
                    // 영주가 음식을 받고 설사약을 눈치챔
                    _state = QuestState.HasPoison;
                    // 음식 받는 애니메이션 (Gather 재사용)
                    if (_rigAnim != null) _rigAnim.SetState(ProjectName.Systems.AnimationState.Gather);
                    Debug.Log("[TutorialQuestNPC] 영주가 음식을 받았다. (다음 대화에서 설사약 의심)");
                    break;

                case QuestState.HasPoison:
                    // 설사약 의심 대화 종료 → 독 효과 진행
                    _state = QuestState.Poisoned;
                    _poisonTimer = _poisonDuration;
                    // 중독 애니메이션 (비틀거림 = Kneel)
                    if (_rigAnim != null) _rigAnim.SetState(ProjectName.Systems.AnimationState.Kneel);
                    Debug.Log($"[TutorialQuestNPC] 영주가 독에 걸렸다! {_poisonDuration}초 행동불능");
                    break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}