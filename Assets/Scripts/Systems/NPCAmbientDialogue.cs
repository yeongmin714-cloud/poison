using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 37-03: NPC 일상 루틴 대사 — NPC 주변에서 E키로 상호작용하거나
    /// 일정 시간대에 자동으로 대사를 출력합니다.
    /// TimeManager 시간대와 WeatherManager 날씨에 따라 대사가 변화합니다.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class NPCAmbientDialogue : MonoBehaviour
    {
        [Header("NPC 대사 데이터")]
        [SerializeField] private NpcDialogueData _dialogueData;

        [Header("상호작용 설정")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [SerializeField] private float _interactRadius = 2.5f;
        [SerializeField] private LayerMask _playerLayer = 1;

        [Header("자동 대사 설정")]
        [SerializeField] private bool _enableAutoDialogue = true;
        [SerializeField] private float _autoDialogueIntervalMin = 30f;
        [SerializeField] private float _autoDialogueIntervalMax = 90f;
        [SerializeField] private float _autoDialogueRange = 8f;

        [Header("대사 표시")]
        [SerializeField] private float _dialogueDisplayDuration = 4f;
        [SerializeField] private Color _dialogueTextColor = Color.white;
        [SerializeField] private int _dialogueFontSize = 16;

        [Header("사운드")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _talkSound;

        // ===== NPC 이름 =====
        private string _npcDisplayName;

        // ===== 상태 =====
        private SphereCollider _sphereCollider;
        private bool _playerInRange;
        private Transform _playerTransform;
        private float _autoDialogueTimer;
        private string _currentDialogue;
        private float _dialogueTimer;

        // 마지막 대사 인덱스 (중복 방지)
        // private int _lastDialogueIndex = -1;

        // IMGUI 표시
        private bool _showingBubble;
        private Vector3 _worldOffset = new Vector3(0, 1.8f, 0); // NPC 머리 위

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            _sphereCollider = GetComponent<SphereCollider>();
            _sphereCollider.isTrigger = true;
            _sphereCollider.radius = _interactRadius;
        }

        private void Start()
        {
            if (_dialogueData != null)
            {
                _npcDisplayName = _dialogueData.NpcName;
            }
            else
            {
                _npcDisplayName = gameObject.name;
            }

            // 자동 대사 타이머 초기화
            _autoDialogueTimer = Random.Range(_autoDialogueIntervalMin, _autoDialogueIntervalMax);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_playerInRange && ((1 << other.gameObject.layer) & _playerLayer) != 0)
            {
                _playerInRange = true;
                _playerTransform = other.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & _playerLayer) != 0)
            {
                _playerInRange = false;
                _playerTransform = null;
            }
        }

        private void Update()
        {
            if (_dialogueData == null) return;

            // E키 상호작용
            if (_playerInRange && Input.GetKeyDown(_interactKey))
            {
                TryInteractDialogue();
            }

            // 자동 대사
            if (_enableAutoDialogue)
            {
                UpdateAutoDialogue();
            }

            // 대사 타이머
            if (_dialogueTimer > 0f)
            {
                _dialogueTimer -= Time.deltaTime;
                if (_dialogueTimer <= 0f)
                {
                    _currentDialogue = string.Empty;
                    _showingBubble = false;
                }
            }
        }

        private void OnGUI()
        {
            if (!_showingBubble || string.IsNullOrEmpty(_currentDialogue) || Camera.main == null)
                return;

            // 월드 좌표 → 스크린 좌표
            Vector3 worldPos = transform.position + _worldOffset;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // 카메라 뒤에 있으면 표시 안함
            if (screenPos.z < 0) return;

            float bubbleWidth = 300f;
            float bubbleHeight = 60f;

            Rect bubbleRect = new Rect(
                screenPos.x - bubbleWidth * 0.5f,
                Screen.height - screenPos.y - bubbleHeight * 0.5f,
                bubbleWidth,
                bubbleHeight
            );

            // 말풍선 배경
            GUI.Box(bubbleRect, "");

            // NPC 이름
            float nameHeight = 20f;
            Rect nameRect = new Rect(bubbleRect.x, bubbleRect.y, bubbleRect.width, nameHeight);
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.8f, 0.5f) }
            };
            GUI.Label(nameRect, $"<{_npcDisplayName}>", nameStyle);

            // 대사 텍스트
            float textY = bubbleRect.y + nameHeight;
            Rect textRect = new Rect(bubbleRect.x + 10, textY, bubbleRect.width - 20, bubbleRect.height - nameHeight - 10);
            GUIStyle dialogueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _dialogueFontSize,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = _dialogueTextColor }
            };
            GUI.Label(textRect, $"\"{_currentDialogue}\"", dialogueStyle);
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// NPC와 대화 (E키 상호작용).
        /// </summary>
        public void TryInteractDialogue()
        {
            if (_dialogueData == null) return;

            string dialogue = _dialogueData.GetRandomInteractionDialogue();
            ShowDialogue(dialogue);

            // 사운드
            if (_audioSource != null && _talkSound != null)
            {
                _audioSource.PlayOneShot(_talkSound);
            }
        }

        /// <summary>
        /// 특정 대사를 강제로 표시합니다 (외부 호출용).
        /// </summary>
        public void ShowDialogue(string dialogue)
        {
            if (string.IsNullOrEmpty(dialogue)) return;

            _currentDialogue = dialogue;
            _dialogueTimer = _dialogueDisplayDuration;
            _showingBubble = true;

            Debug.Log($"[NPCAmbientDialogue] {_npcDisplayName}: \"{dialogue}\" ");
        }

        /// <summary>
        /// 현재 NPC의 대사 데이터를 반환합니다.
        /// </summary>
        public NpcDialogueData GetDialogueData() => _dialogueData;

        // ================================================================
        // [89차] F키 상호작용 — 표시명/대사/대화 자세 (docs/89_NPC_FKEY_PLAYER_ANIM_PLAN.md §4)
        // ================================================================

        /// <summary>[89차] NPC 표시명 — NpcDialogueData.NpcName 우선, 데이터 미구성 시 gameObject.name 폴백.</summary>
        public string DisplayName
        {
            get
            {
                var data = GetDialogueData();
                if (data != null && !string.IsNullOrEmpty(data.NpcName))
                    return data.NpcName;
                return gameObject.name;   // [89차] 데이터 미구성 폴백 — 오브젝트명
            }
        }

        /// <summary>
        /// [89차] F키 상호작용 1행 대사 — 데이터 구성 시 GetRandomInteractionDialogue(),
        /// 데이터 부재(null)/미구성("...") 시 고정 폴백 3종 중 결정론 선택.
        /// 위조 금지 표기: 폴백 3종은 데이터 미구성 폴백(스펙 §4 — 실제 대사 데이터 아님).
        /// </summary>
        public string GetInteractionLine()
        {
            var data = GetDialogueData();
            if (data != null)
            {
                string line = data.GetRandomInteractionDialogue();
                if (!string.IsNullOrEmpty(line) && line != "...")   // "..." = 데이터 미구성 마커(NpcDialogueData 계약)
                    return line;
            }

            // 데이터 미구성 폴백 — 고정 3종 중 오브젝트명 해시 결정론 선택(VillageNpcSpawner.GetHash 동일 알고리즘).
            string[] fallbacks =
            {
                "안녕하세요, 여행자님.",
                "오늘 날씨가 참 좋네요.",
                "마을 구경 잘 하고 가세요.",
            };
            int hash = 17;
            foreach (char c in gameObject.name)
                hash = hash * 31 + c;
            return fallbacks[Mathf.Abs(hash) % fallbacks.Length];
        }

        /// <summary>
        /// [89차] 대화 자세 진입 — VillagerWanderer.PauseForDialogue(배회 정지+Speed 0) + Animator "Talk" 트리거
        /// (Player_AC TalkA 상태 — HumanoidClipDriver.TriggerTalkA 동일 트리거명). DialogueEnded(카메라 복원) 구독
        /// (구독 전 해제 — 1회성)으로 EndDialoguePose 자동 복귀.
        /// [128-2 P2] Talk 단일화 — [103차 P3] 토크 변형(TalkA/Talk2 홀짝) 폐기, 항상 "Talk" 트리거 단일화
        /// (TalkB 슬롯이 캐릭터 talk/idle 클립으로 오버라이드되므로 Talk2 직접 트리거는 SetTalkBTrigger 경로만 유지).
        /// </summary>
        public void BeginDialoguePose()
        {
            var wanderer = GetComponent<VillagerWanderer>();
            if (wanderer != null) wanderer.PauseForDialogue();

            var anim = GetComponent<Animator>();   // 취득 방식 기존 유지(GetComponent — PauseForDialogue/Rebind 경로와 동일)
            if (anim != null)
            {
                // [128-2 P2] 대화 자세 = Idle 강제 진입 — Walk 프레임 동결 원천 제거(스펙 §2-4). Talk 트리거는 단일화(토크 변형 폐기).
                var st = anim.GetCurrentAnimatorStateInfo(0);
                bool wasWalk = st.IsName("Walk");   // EditMode 평가 안전 — IsName은 state info 기반이라 예외 없음
                anim.CrossFade("Idle", 0.15f);
                anim.SetTrigger("Talk");
                Debug.Log($"[128-2 P2] 대화자세 — {DisplayName} 진입전state={(wasWalk ? "Walk" : "기타")}");
            }

            // [89차] DialogueEnded 1회성 구독 — 구독 전 해제(중복 방지), 카메라 복원 시 대화 자세 해제.
            DialogueCameraDirector.DialogueEnded -= OnDialogueEndedForPose;
            DialogueCameraDirector.DialogueEnded += OnDialogueEndedForPose;
        }

        /// <summary>[89차] 대화 자세 해제 — DialogueEnded 구독 해제 + VillagerWanderer.ResumeAfterDialogue(배회 재개).
        /// [98차 F1] F1-4 재개 봉합 — 위임 경로(카메라 End → DialogueEnded → 본 메서드) 가시화 로그 추가.
        /// 근거: 마을씬 테스트 9 Editor.log — 배회 재개 로그 전무 + 위임 경로 로그 부재로 성패 판별 불가
        /// (위임 수신처가 죽으면 영구 정지 — Director End 폴백 재개/[98차 F1]가 안전망 이중화).</summary>
        public void EndDialoguePose()
        {
            DialogueCameraDirector.DialogueEnded -= OnDialogueEndedForPose;

            var wanderer = GetComponent<VillagerWanderer>();
            if (wanderer != null) wanderer.ResumeAfterDialogue();
            // [98차 F1] 위임 수신처 재개 로그 — 재개 성패 판별 가능화(테스트 9 판별 불가 원인 봉합).
            Debug.Log($"[98차 F1] 대화 종료 재개 — {DisplayName}");
        }

        /// <summary>[89차] DialogueEnded 수신 — 대화 카메라 복원 시 대화 자세 해제(1회성 — EndDialoguePose가 구독 해제).</summary>
        private void OnDialogueEndedForPose(Component target)
        {
            EndDialoguePose();
        }

        /// <summary>[89차] TalkB 트리거 — Player_AC TalkB 상태(트리거 "Talk2", HasExitTime 0.92 → 자동 Idle 복귀).
        /// NPCDialoguePanelUTK.OpenForVillager 선택지 응답 경유.</summary>
        public void SetTalkBTrigger()
        {
            var anim = GetComponent<Animator>();
            if (anim != null) anim.SetTrigger("Talk2");
        }

        // ================================================================
        // 내부 메서드
        // ================================================================

        private void UpdateAutoDialogue()
        {
            _autoDialogueTimer -= Time.deltaTime;

            if (_autoDialogueTimer <= 0f)
            {
                _autoDialogueTimer = Random.Range(_autoDialogueIntervalMin, _autoDialogueIntervalMax);

                // 플레이어가 근처에 있을 때만 자동 대사
                if (_playerTransform != null)
                {
                    float dist = Vector3.Distance(transform.position, _playerTransform.position);
                    if (dist <= _autoDialogueRange)
                    {
                        // 시간대별 대사
                        int currentHour = GetCurrentHour();
                        string timeDialogue = _dialogueData.GetRandomDialogueForTime(currentHour);
                        if (!string.IsNullOrEmpty(timeDialogue))
                        {
                            ShowDialogue(timeDialogue);
                            return;
                        }

                        // 날씨 대사 (fallback)
                        string weatherDialogue = GetWeatherDialogue();
                        if (!string.IsNullOrEmpty(weatherDialogue))
                        {
                            ShowDialogue(weatherDialogue);
                        }
                    }
                }
            }
        }

        private int GetCurrentHour()
        {
            if (TimeManager.Instance != null)
            {
                return Mathf.FloorToInt(TimeManager.Instance.GameTime / 3600f) % 24;
            }
            // TimeManager가 없으면 시스템 시간 사용
            return System.DateTime.Now.Hour;
        }

        private string GetWeatherDialogue()
        {
            if (_dialogueData == null) return string.Empty;

            if (WeatherManager.Instance != null)
            {
                // WeatherManager의 현재 날씨를 가져올 수 없으므로,
                // 리플렉션으로 현재 날씨 필드를 확인 (간접 참조)
                var weatherType = WeatherManager.Instance.GetType().GetField("_currentWeatherType",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (weatherType != null)
                {
                    var value = weatherType.GetValue(WeatherManager.Instance);
                    if (value is WeatherManager.WeatherType wt)
                    {
                        return _dialogueData.GetWeatherDialogue(wt);
                    }
                }
            }

            return string.Empty;
        }

        // ================================================================
        // Gizmos
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
            Gizmos.DrawSphere(transform.position, _interactRadius);

            Gizmos.color = new Color(1f, 0.8f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, _autoDialogueRange);
        }
    }
}