using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI;

// [154차 P1] UI 어셈블리 internal → EditMode 테스트 개방 — ComputeBottomCenterLayout 순수 배치 산식
//   직접판정용(Systems NPCDialogueAdapter 어셈블리 선언 선례 — DialogueGaze104Tests internal 헬퍼 동일 패턴).
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ProjectName.Tests.EditMode")]

namespace ProjectName.UI.Toolkit
{
    /// <summary>Compact NPC dialogue panel with dialogue-only and three-choice modes.</summary>
    public class NPCDialoguePanelUTK : UTKWindowBase
    {
        private const float PanelWidth = 528f;          // [74차] 피그마 163:23/187:14 진실치 — 440 폐기
        private const float DialogueHeight = 148.8f;    // 대화 전용 (163:23)
        private const float ChoiceHeight = 171.8f;      // 선택지 포함 (187:14)

        private static readonly string[] DefaultChoices =
        {
            "대요정님, 저는 드류예요.",
            "오늘은 어때요?",
            "오늘은 무슨 일이시죠?"
        };

        // [151차 P2 → 154차 P1] 대화 패널 항상 중앙 하단 고정 — 구 앵커 투영 배치 체인(74차 대상 앞 배치 →
        //   92차 클램프·프록시 → 150차 P2 투영 무효 폴백+재배치 루프) 전부 폐기 일원화(사용자 지시 "대화판넬은
        //   항상 중앙 하단에만 배치"). 근거: Play 로그 `[150차 P2] 패널 투영 무효 cam=(0.0,0.0,0.0)` 반복 —
        //   앵커 투영 자체가 불안정. [154차 P1] y축 이중 반전 수리 — 구 입력 Screen.width * 0.5f, Screen.height * 0.85f는
        //   ApplyAnchorLayout의 투영용 y 반전을 거쳐 0.15(상단)가 되어 획득 패널/농경 트랜지언트/NPC 대화가
        //   전부 상단에 고정된 결함(영상 증거) → bottom-up 0.15 입력으로 교체. 배치 산식은 순수 함수
        //   ComputeBottomCenterLayout 단일소스(회귀 핀 = DialoguePanel_BottomCenterComputed_154).
        private static bool _dialogueEndedSubscribed;   // [125차 후속10] 대화 종료→닫힘 구독 1회 가드(EnsureAttached 다중 진입 멱등)

        // [151차 P3] 농경 상태 메시지 트랜지언트 표출 상태 — FarmPlot.FarmStatusBroadcast 구독(UI→Systems 단방향).
        private static bool _farmStatusSubscribed;      // 구독 1회 가드(멱등)
        private static bool _playerNotifySubscribed;    // [158차 P2] PlayerNotify 구독 가드(멱등)
        private static bool _transientActive;           // 트랜지언트(농경 메시지) 표시 중
        private static float _transientUntil;           // 자동 닫힘 시각(Time.time 기준 +3.5s — FarmPlot 창 계약 승계)
        private static string _transientMessage;        // 스팸 가드 — 동일 메시지 연속 재발화 시 타이머 연장만
        private IVisualElementScheduledItem _transientCloseItem;   // 패널 스케줄러 자동 닫힘 핸들

        private static NPCDialoguePanelUTK _instance;

        private readonly Label _nameLabel;
        private readonly Label _npcLineOne;
        private readonly Label _npcLineTwo;
        private readonly Label _playerLine;
        private readonly VisualElement _choiceGrid;
        private readonly Label _actionHint;
        private ProjectName.Systems.NPCAmbientDialogue _villagerNpc;   // [89차] 주민 모드 대상 — 선택지 응답 갱신/Talk2/재배치(null=비주민 경로)

        public static NPCDialoguePanelUTK Instance => _instance;

        private NPCDialoguePanelUTK() : base("NPC 대화", new Vector2(PanelWidth, DialogueHeight), UTKWindowChrome.Frameless)
        {
            style.width = PanelWidth;
            style.height = DialogueHeight;
            // [144차 P1-DLG] 피그마 163:23 재실측 — bg #161B22@0.45(74차 #151B21 정정), 테두리 #30363D@0.3 w1.2(존재 재확인), r9.6.
            style.backgroundColor = new StyleColor(new Color(0.086f, 0.106f, 0.133f, 0.45f));   // #161B22/45%
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1.2f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor =
                new StyleColor(new Color(0.188f, 0.212f, 0.239f, 0.3f));   // #30363D/30%
            style.borderTopLeftRadius = style.borderTopRightRadius = 9.6f;
            style.borderBottomLeftRadius = style.borderBottomRightRadius = 9.6f;

            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 14.4f;   // [144차 P1-DLG #8] 패딩 12→14.4
            _content.style.paddingRight = 14.4f;
            _content.style.paddingTop = 14.4f;
            _content.style.paddingBottom = 14.4f;

            var nameRow = new VisualElement { name = "NpcNameRow" };
            nameRow.style.height = 15f;   // [144차 P1-DLG #10] 13→15
            nameRow.style.minHeight = 15f;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            var indicator = new VisualElement { name = "NpcIndicator" };
            // [74차] 피그마 진실치 — 원 7.2×7.2 #33E5D8 (구 6px 골드 폐기)
            indicator.style.width = 7.2f;
            indicator.style.height = 7.2f;
            indicator.style.marginRight = 8f;
            indicator.style.backgroundColor = new StyleColor(new Color(0.2f, 0.898f, 0.851f));   // [144차 P2-DLG #12] #33E5D9 (D8 LSB 정정)
            indicator.style.borderTopLeftRadius = indicator.style.borderTopRightRadius = 3.6f;
            indicator.style.borderBottomLeftRadius = indicator.style.borderBottomRightRadius = 3.6f;
            nameRow.Add(indicator);

            _nameLabel = new Label("NPC") { name = "NpcName" };
            _nameLabel.style.fontSize = 13.2f;
            _nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _nameLabel.style.color = new StyleColor(new Color(0.2f, 0.898f, 0.851f));   // [144차 P2-DLG #15] #33E5D9 (D8 LSB 정정)
            nameRow.Add(_nameLabel);
            _content.Add(nameRow);

            var separator = new VisualElement { name = "Separator" };
            separator.style.height = 1.2f;
            separator.style.minHeight = 1.2f;
            separator.style.marginTop = 9.6f;   // [144차 P1-DLG #9] 세로 간격 gap 9.6 체계
            separator.style.marginBottom = 9.6f;
            separator.style.backgroundColor = new StyleColor(new Color(0.188f, 0.212f, 0.239f, 0.6f));   // [144차] #30363D/60% (74차 #2F363C 정정)
            _content.Add(separator);

            var dialogue = new VisualElement { name = "DialogueContent" };
            dialogue.style.flexGrow = 1f;
            dialogue.style.flexDirection = FlexDirection.Column;
            dialogue.style.minHeight = 0f;

            _playerLine = CreateDialogueLabel("PlayerReply");
            _playerLine.style.color = new StyleColor(new Color32(0x79, 0xC0, 0xFF, 0xFF));
            _playerLine.style.display = DisplayStyle.None;
            dialogue.Add(_playerLine);

            _npcLineOne = CreateDialogueLabel("NpcDialogueLine1");
            _npcLineOne.style.color = new StyleColor(new Color(0.922f, 0.929f, 0.941f));   // [144차 P2-DLG #19] 1행 #EBEDF0 (EAEDEF LSB 정정)
            dialogue.Add(_npcLineOne);
            _npcLineTwo = CreateDialogueLabel("NpcDialogueLine2");
            _npcLineTwo.style.color = new StyleColor(UTKTheme.TextSub);   // [74차] 2행 #8B949E
            _npcLineTwo.style.marginTop = 6f;
            dialogue.Add(_npcLineTwo);
            _content.Add(dialogue);

            _choiceGrid = new VisualElement { name = "ChoiceGrid" };
            // [74차] 피그마 187:14 — ChoiceGrid (15.6,127.4) 496.8×28.8 — 3열 고정 카드(1행)
            _choiceGrid.style.height = 28.8f;
            _choiceGrid.style.minHeight = 28.8f;
            _choiceGrid.style.flexDirection = FlexDirection.Row;
            _choiceGrid.style.flexWrap = Wrap.NoWrap;
            _choiceGrid.style.display = DisplayStyle.None;

            _actionHint = new Label("계속하려면 클릭 ▶") { name = "ActionHint" };
            _actionHint.style.height = 14f;
            _actionHint.style.minHeight = 14f;
            _actionHint.style.marginTop = 9.6f;   // [144차 P1-DLG #9] 세로 간격 gap 9.6 체계
            // [144차 P2-DLG #24] ContinueHint — 12/500(Bold 근사) #58A6FF(74차 #57A6FF LSB 정정), 우측 정렬
            _actionHint.style.fontSize = 12f;
            _actionHint.style.unityFontStyleAndWeight = FontStyle.Bold;
            _actionHint.style.color = new StyleColor(new Color(0.345f, 0.651f, 1f));   // #58A6FF
            _actionHint.style.unityTextAlign = TextAnchor.MiddleRight;
            _actionHint.style.alignSelf = Align.FlexEnd;
            _actionHint.style.width = 96f;   // [144차 P1-DLG #23] 박스 폭 96px 고정(96% 폐기) — MiddleRight로 우측 위치 유지
            _actionHint.style.marginRight = 0f;
            _content.Add(_actionHint);

            // [144차 P1-DLG #27] 187:14 요소 순서 — DialogueContent→ActionRow(힌트)→ChoiceGrid(그리드 최하단).
            _content.Add(_choiceGrid);

            _actionHint.RegisterCallback<ClickEvent>(_ => Close());

            // [144차 P1-DLG #42/#43] 코너 데칼 — 공용 어휘(UTKTheme.AddCornerDecals)로 교체:
            //   코너 밖 걸침 + #58A6FF + 1.8 (구 로컬 구현: 안쪽 4px + #3A424D + 1px 폐기).
            UTKTheme.AddCornerDecals(this, 9.6f, 1.8f);
        }

        // [144차 P1-DLG #42/#43] 구 로컬 AddCornerDecals(안쪽 4px·#3A424D·1px) 제거 —
        //   공용 UTKTheme.AddCornerDecals(코너 밖·#58A6FF·1.8)로 일원화(상단 ctor 호출).

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureAttached();
        }

        private static void EnsureAttached()
        {
            // [125차 후속10] 대화 종료 → 대화창 자동 닫힘(1회 구독 — 멱등). 근거: 플레이 영상 실측 —
            //   대화 종료(카메라 복원) 후에도 대화창이 엉뚱한 화면 위치에 잔류(배치는 개방 시 1회 계약이라
            //   카메라 복원 후 좌표가 어긋남). Test_15 통일 계약 — 대화 종료 = 대화창 닫힘.
            //   DialogueEnded는 Director.End 말미 발화(멱등) — 병사/주민/정적 NPC 전 경로 공통 적용.
            if (!_dialogueEndedSubscribed)
            {
                _dialogueEndedSubscribed = true;
                ProjectName.Systems.DialogueCameraDirector.DialogueEnded += _ =>
                {
                    if (_instance != null && _instance.IsOpen) _instance.Close();
                };
            }

            // [151차 P3] 농경 상태 메시지 구독(멱등) — Systems 이벤트를 UI가 구독(역참조 금지 계약 준용).
            if (!_farmStatusSubscribed)
            {
                _farmStatusSubscribed = true;
                ProjectName.Systems.FarmPlot.FarmStatusBroadcast += ShowTransientMessage;
            }

            // [158차 P2] 플레이어 알림 통일 구독(멱등) — PlayerNotify.Broadcast(Systems 정적 버스)를
            //   대화 패널 트랜지언트로 라우팅. 상자 게이트 차단/용병 고용 차단/물뿌리개 충전/채광·채집
            //   도구 차단 등 전 시스템 발화가 "대화판넬 표출" 계약으로 일원화(FarmStatusBroadcast 동일 패턴).
            if (!_playerNotifySubscribed)
            {
                _playerNotifySubscribed = true;
                ProjectName.Systems.PlayerNotify.Broadcast += ShowTransientMessage;
            }

            // [97차 F2] 대화 카메라 수명 연동 — 패널 개방 실측을 Director에 공급(Systems asmdef는 UI 역참조
            //   금지(UI→Systems 단방향 계약)이므로 UI 측 등록 방식). 위임은 항상 살아 있는 IsOpen 폴링
            //   (Instance 부재=닫힘) — Show/Hide별 등록 불요(멱등). EnsureAttached는 Bootstrap(씬 로드)+
            //   전 Open 진입점 공통 경로 — 어떤 대화 개방보다 항상 선행 등록 보장.
            // [98차 F2] F2-1 폴링 강화(단일소스 계약 유지 — 위임 람다 내부에서 실측): 기존 IsOpen 판정에
            //   실측표시 폴백을 OR로 확장 — 실측표시 = parent 부착 + resolvedStyle.display != None.
            //   근거: 마을씬 테스트 9 — 패널은 OpenForVillager로 실제 개방됐는데(IsOpen 폴링 결과와
            //   실제 표시 상태 불일치) 카메라만 조기 End(36s 실측) → 플래그와 UI 실측 불일치 방어.
            ProjectName.Systems.DialogueCameraDirector.DialoguePanelOpenOverride = () =>
            {
                if (!(_instance != null && _instance.IsOpen))
                {
                    // [98차 F2] 실측표시 폴백 — 플래그가 닫힘을 보고해도 UI가 실제 표시 중(parent 부착 +
                    // resolvedStyle.display != None)이면 열림 판정(조기 End 방어).
                    return _instance != null && _instance.parent != null
                           && _instance.resolvedStyle.display != DisplayStyle.None;
                }
                return true;
            };
            // [98차 F2] F2-2 UI 실측 진단 공급 — Director End 원인 로그용(_instance/IsOpen/display/parent).
            //   Systems 역참조 금지 계약 — UI 측 등록(위 등록과 동일 방식/멱등).
            ProjectName.Systems.DialogueCameraDirector.DialoguePanelOpenDiagnostics = () =>
            {
                if (_instance == null) return "_instance=부재";
                return $"_instance=존재 IsOpen={_instance.IsOpen} display={_instance.resolvedStyle.display} " +
                       $"parent={(_instance.parent != null ? "부착" : "미부착")}";
            };
            if (_instance == null)
                _instance = new NPCDialoguePanelUTK();

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && _instance.parent == null)
                root.Add(_instance);
        }

        /// <summary>Open the compact panel for an NPC; defaults to the three-choice version.</summary>
        public static void Open(NPCInstance npc) => Open(npc, null);

        /// <summary>[74차 → 151차 P2] NPC 대화 개방 — anchor 파라미터는 시그니처 호환용(무시), 패널은 항상 중앙 하단 고정.</summary>
        public static void Open(NPCInstance npc, Transform anchor)
        {
            EnsureAttached();
            _transientActive = false;   // [151차 P3] 정상 개설 — 트랜지언트 상태 해제(만료가 NPC 대화를 닫지 않게)
            _instance._villagerNpc = null;   // [89차] 주민 모드 해제 — NPCInstance 경로 계약 불변
            _instance.SetNpc(npc);
            _instance.ShowWithChoices(DefaultChoices);
            _instance.PlaceAtAnchor(anchor);   // [74차] 대상 앞 배치(개방 시 1회 — anchor null이면 기존 위치)
        }

        /// <summary>
        /// [74차] 병사 대화 진입점 — 피그마 npc-dialogue-panel 계약으로 병사 대상 대화.
        /// 대사 1행 = 병사 StatusMessage(현재 응답), 2행 = 호감 단계 안내(실측), 선택지 3개 클릭 시
        /// 병사 응답(SetStatusMessage → GuardTalkBubble 말풍선 자동).
        /// </summary>
        public static void OpenForGuard(ProjectName.Systems.GuardPlaceholder guard)
        {
            if (guard == null) return;
            EnsureAttached();
            _transientActive = false;   // [151차 P3] 정상 개설 — 트랜지언트 상태 해제
            _instance._villagerNpc = null;   // [89차] 주민 모드 해제 — 병사 경로 계약 불변
            var name = string.IsNullOrEmpty(guard.GuardName) ? "병사" : guard.GuardName;
            _instance._nameLabel.text = name;
            string talk = guard.StatusMessage;
            _instance._npcLineOne.text = string.IsNullOrEmpty(talk) ? "..." : talk;
            // 2행 — 호감 단계 안내(실측)
            var loyalty = guard.Loyalty;
            string grade = ProjectName.Systems.GuardLoyaltySystem.GetAffinityGradeName(loyalty);
            _instance._npcLineTwo.text = $"체감 분위기: {grade} — 호감도 {loyalty:F0}";
            _instance._npcLineTwo.style.display = DisplayStyle.Flex;
            _instance.ShowWithChoices(GuardChoices);
            _instance.PlaceAtAnchor(guard.transform);   // 대상 바로 앞
        }

        /// <summary>[74차] 병사 대화 선택지 — 피그마 187:14 선택 카드 3개 계약 계열.</summary>
        private static readonly string[] GuardChoices =
        {
            "안녕하세요.",
            "요즘 어떻게 지내세요?",
            "일해라 병사!",
        };

        /// <summary>
        /// [89차] 마을 주민 대화 진입점 — OpenForGuard 패턴 준용(Open/OpenForGuard 계약·시그니처 불변).
        /// 이름행 = DisplayName(NpcDialogueData.NpcName 우선), 1행 = GetInteractionLine()(1행 NPC색 #EAEDEF 계약),
        /// 2행 = "마을 주민 — 잡담" 보조행(#8B949E), 선택지 3개(안녕하세요./근처 소식 있어?/잘 지내?).
        /// 선택지 클릭 → 1행 응답 갱신(GetInteractionLine 재호출) + 주민 Talk2 트리거(Player_AC TalkB) + 재배치(항상 중앙 하단 — [151차 P2]).
        /// </summary>
        public static void OpenForVillager(ProjectName.Systems.NPCAmbientDialogue npc)
        {
            if (npc == null) return;
            EnsureAttached();
            _transientActive = false;   // [151차 P3] 정상 개설 — 트랜지언트 상태 해제
            _instance._villagerNpc = npc;
            _instance._nameLabel.text = npc.DisplayName;
            _instance._npcLineOne.text = npc.GetInteractionLine();
            _instance._npcLineTwo.text = "마을 주민 — 잡담";   // [89차] 2행 보조행(#8B949E — UTKTheme.TextSub 계약)
            _instance._npcLineTwo.style.display = DisplayStyle.Flex;
            _instance.ShowWithChoices(VillagerChoices);
            _instance.PlaceAtAnchor(npc.transform);   // 대상 바로 앞
        }

        /// <summary>[89차] 마을 주민 대화 선택지 — 피그마 187:14 선택 카드 3개 계약 계열(GuardChoices 동일 패턴).</summary>
        private static readonly string[] VillagerChoices =
        {
            "안녕하세요.",
            "근처 소식 있어?",
            "잘 지내?",
        };

        /// <summary>
        /// [125차 후속9] 정적 NPC(제네릭) 대화 진입점 — OpenForGuard/OpenForVillager 패턴 준용.
        /// Test_15 동일 계열 통일용: NPCInstance 비대상 정적 NPC(아레나 베팅 관리인/영주 딸 등)의
        /// 대화창 개방. 이름행 = displayName, 1행 = npcLine(인사 대사, NPC색 #EAEDEF 계약),
        /// 2행 = subLine(보조정보, null/빈값=숨김), 선택지 3개(StaticChoices).
        /// 선택지 클릭 → SelectChoice 비주민 경로(플레이어 응답 표시 후 대화 전용 전환 —
        /// Open(NPCInstance) 계약 동일). 배치는 항상 중앙 하단 고정([151차 P2] — target.transform은 시그니처 호환용 무시).
        /// </summary>
        public static void OpenForStatic(Component target, string displayName, string npcLine, string subLine = null)
        {
            if (target == null) return;
            EnsureAttached();
            _transientActive = false;   // [151차 P3] 정상 개설 — 트랜지언트 상태 해제
            _instance._villagerNpc = null;   // [125차 후속9] 주민 모드 해제 — 정적 NPC 경로 계약 불변
            _instance._nameLabel.text = string.IsNullOrEmpty(displayName) ? "NPC" : displayName;
            _instance._npcLineOne.text = string.IsNullOrEmpty(npcLine) ? "..." : npcLine;
            _instance._npcLineTwo.text = subLine ?? "";
            _instance._npcLineTwo.style.display = string.IsNullOrEmpty(subLine) ? DisplayStyle.None : DisplayStyle.Flex;
            _instance.ShowWithChoices(StaticChoices);
            _instance.PlaceAtAnchor(target.transform);   // 대상 바로 앞
        }

        /// <summary>
        /// [130차] 획득 연출 전용 진입점(동물의 숲식 나레이션) — 선택지 없는 1행 패널.
        /// 화자명 미표시(빈 이름행), OpenForStatic의 StaticChoices 카드 미생성(ShowDialogueOnly 경로).
        /// 패널 = 입력 게이트 겸 세션 수명 소스 — AcquisitionCinema가 CheerHold 후 Hide.
        /// </summary>
        public static void OpenForAcquisition(Component target, string line)
        {
            if (target == null) return;
            EnsureAttached();
            _transientActive = false;   // [151차 P3] 정상 개설 — 트랜지언트 상태 해제
            _instance._villagerNpc = null;   // 주민 모드 해제 — OpenForStatic 계약 동일
            _instance._nameLabel.text = "";   // [130차] 나레이션 톤 — 화자 미표시
            _instance._npcLineOne.text = string.IsNullOrEmpty(line) ? "..." : line;
            _instance._npcLineTwo.text = "";
            _instance._npcLineTwo.style.display = DisplayStyle.None;
            _instance.ShowDialogueOnly();
            _instance.PlaceAtAnchor(target.transform);   // 대상(플레이어) 바로 앞 — OpenForStatic 계약 동일
            // [151차 P2] 구 [150차 P2] 재배치 루프(스케줄 블록) 폐기 — 항상 중앙 하단 고정이라 무의미
            //   (PlaceAtAnchor 본문 폴백 산식 일원화).
        }

        /// <summary>[125차 후속9] 정적 NPC 공용 선택지 — GuardChoices/VillagerChoices 동일 패턴
        /// (DefaultChoices 첫 카드 "대요정님…"은 NPCInstance 전용 — 정적 NPC에는 비개인화 문안 사용).</summary>
        private static readonly string[] StaticChoices =
        {
            "안녕하세요.",
            "오늘은 어때요?",
            "오늘은 무슨 일이시죠?",
        };

        /// <summary>
        /// [151차 P2] 배치 일원화 — 항상 중앙 하단 고정(사용자 지시 "대화판넬은 항상 중앙 하단에만 배치").
        /// 구 앵커 투영/클램프/프록시 경로(74차 대상 앞 배치 → 92차 결함2/후속 F-C → 150차 P2 폴백) 전부 폐기 —
        /// 근거: Play 로그 `[150차 P2] 패널 투영 무효 cam=(0.0,0.0,0.0)` 반복(앵커 투영 자체 불안정).
        /// 시그니처 PlaceAtAnchor(Transform)는 유지(호출부 5곳 무수정 — 호환성), anchor는 무시(null 포함 항상 배치).
        /// 배치 산식 = [154차 P1] 중앙 하단 고정: bottom-up 스크린 px(50%, 15% = 위에서 85% 지점) →
        ///   ApplyAnchorLayout(투영용 y 반전 1회) → 순수 헬퍼 ComputeBottomCenterLayout(캔버스 환산+ScaleRect+클램프).
        /// </summary>
        private void PlaceAtAnchor(Transform anchor)
        {
            var root = UIToolkitBootstrap.UIRoot;
            float rootW = root != null ? root.resolvedStyle.width : Screen.width;
            float rootH = root != null ? root.resolvedStyle.height : Screen.height;
            if (rootW <= 0f || rootH <= 0f) return;
            ApplyBottomCenterPlacement(rootW, rootH);   // [151차 P2] 폴백 산식 상시화 — 투영/클램프/프록시 경로 소멸
        }

        /// <summary>[92차 후속 F-C] 루트 배치 — 화면 px → 캔버스(1920×1080) → 루트 px 변환+클램프
        /// (InteractionPanelUTK 165-166 산식 역방향). [154차 P1] 세계 투영 좌표용 y 반전은 이 메서드에서
        /// 1회만 수행(Unity Screen 좌표는 아래가 0 → top-origin 스크린 y로 뒤집음) — 캔버스 환산+ScaleRect+
        /// 클램프 체인은 순수 헬퍼 ComputeBottomCenterLayout 단일소스(동작 중복 제거, 시그니처 불변).</summary>
        private void ApplyAnchorLayout(Vector2 screen, float rootW, float rootH)
        {
            // [154차 P1] 투영용 y 반전(1회) — Unity Screen 좌표는 아래가 0. 고정 배치(ApplyBottomCenterPlacement)는
            //   bottom-up 좌표를 넘기므로 여기서 반전해 top-origin으로 정규화한 뒤 순수 헬퍼에 넘긴다(이중 반전 금지).
            float screenTopX = screen.x;
            float screenTopY = Screen.height - screen.y;
            float panelW = PanelWidth * rootW / FigmaCanvasLayout.CanvasWidth;
            float panelH = style.height.value.value;
            (float left, float top) = ComputeBottomCenterLayout(
                screenTopX, screenTopY, Screen.width, Screen.height, panelW, panelH, rootW, rootH);
            style.position = Position.Absolute;
            style.left = left;
            // [91차 F4] 패널 바닥 중앙 = 배치 지점 산식 승계([154차 P1] 순수 헬퍼 이전) — 구 적용행
            //   style.top = Mathf.Clamp(scaled.y - panelH, 0f, Mathf.Max(0f, rootH - panelH))
            //   는 동일 산식+클램프를 헬퍼가 계산해 반환한 top을 대입하는 형태로 승계(산식 무변경).
            style.top = top;
        }

        /// <summary>
        /// [154차 P1] 중앙 하단 고정 배치 순수 산식 — 캔버스 환산 + ScaleRect + 루트 클램프 체인 전체.
        /// 입력 (screenX, screenY) = top-origin 스크린 px(위에서 0 — 투영용 y 반전은 ApplyAnchorLayout 호출부
        /// 몫이므로 여기서 반전하면 이중 반전 결함 재발). (screenW, screenH) = 입력 좌표의 스크린 프레임.
        /// panelW = 루트 환산 패널 폭, panelH = 스타일 높이 값(무환산 — [91차 F4] 패널 바닥 중앙 = 앵커 승계).
        /// 산출 (left, top) = 패널 좌상단 루트 px. 순수 함수 — Screen.*·요소 상태 미의존(EditMode 직접 검증).
        /// </summary>
        internal static (float left, float top) ComputeBottomCenterLayout(
            float screenX, float screenY, float screenW, float screenH,
            float panelW, float panelH, float rootW, float rootH)
        {
            // top-origin 스크린 → 캔버스(1920×1080) 비례 환산 — 반전 없음(반전은 호출부 몫).
            float canvasX = screenX * FigmaCanvasLayout.CanvasWidth / screenW;
            float canvasY = screenY * FigmaCanvasLayout.CanvasHeight / screenH;
            Rect scaled = FigmaCanvasLayout.ScaleRect(
                new Rect(canvasX, canvasY, 0f, 0f), new Vector2(rootW, rootH));
            // [92차 결함2] 루트 좌표 클램프 후 배치 — 패널 전체가 화면 안(바닥 중앙 기준 — 패널 크기 고정).
            float left = Mathf.Clamp(scaled.x - panelW * 0.5f, 0f, Mathf.Max(0f, rootW - panelW));
            float top = Mathf.Clamp(scaled.y - panelH, 0f, Mathf.Max(0f, rootH - panelH));   // [91차 F4] 패널 바닥 중앙 = 앵커
            return (left, top);
        }

        /// <summary>[151차 P2 → 154차 P1] 상시 배치 — 화면 중앙 하단 고정(디자인 기준 위에서 85% 지점) + 진단 로그.
        ///   [154차 P1] y축 이중 반전 수리 — ApplyAnchorLayout은 투영용 y 반전을 수행하므로 고정 배치는
        ///   bottom-up 좌표(Screen.height*0.15 = Unity Screen 좌표 기준 위에서 85% 지점)를 넘긴다. 구 입력
        ///   0.85는 반전 후 0.15(상단)가 되어 획득 패널/농경 트랜지언트/NPC 대화가 전부 상단 고정된 결함 원인.
        ///   구 [150차 P2] 폴백(투영 무효 시 한정)+재배치 루프(500ms×12)의 항상 고정 일원화 계약은 유지.</summary>
        private void ApplyBottomCenterPlacement(float rootW, float rootH)
        {
            Debug.Log("[154차 P1] 대화 패널 — 항상 중앙 하단 고정 배치(bottom-up 0.15 = 위에서 85% 지점)");
            ApplyAnchorLayout(new Vector2(Screen.width * 0.5f, Screen.height * 0.15f), rootW, rootH);
        }

        /// <summary>
        /// [151차 P3] 농경 상태 메시지 트랜지언트 표출 — 패널(항상 중앙 하단 고정 [151차 P2])에 3.5s 자동 닫힘.
        /// 규칙: ①NPC 대화가 열려 있으면(트랜지언트 아님) 덮어쓰지 않고 스킵 ②동일 메시지 연속 재발화는 타이머 연장만
        /// ③정상 개설 경로(Open/ForGuard/ForVillager/ForStatic/ForAcquisition/SelectChoice)는 _transientActive=false로
        ///   해제 — 트랜지언트 만료가 NPC 대화를 닫는 사고 방지.
        /// </summary>
        public static void ShowTransientMessage(string message)
        {
            if (!Application.isPlaying) return;   // [158차 P5] EditMode 무동작 — 정적 버스 잔존 구독이 에디터에서 UI 발화(UTKWindowManager.DontDestroyOnLoad 예외)하는 회귀 차단(RestaurantPlacementMarker 선례)
            if (string.IsNullOrEmpty(message)) return;
            EnsureAttached();
            if (_instance == null) return;
            if (_instance.IsOpen && !_transientActive) return;   // NPC 대화 보호 — 스킵
            bool same = _transientActive && _transientMessage == message;
            _instance._villagerNpc = null;
            _instance._nameLabel.text = "";
            _instance._npcLineOne.text = message;
            _instance._npcLineTwo.text = "";
            _instance._npcLineTwo.style.display = DisplayStyle.None;
            _instance.ShowDialogueOnly();
            _instance._actionHint.style.display = DisplayStyle.None;   // 조작 힌트 대신 자동 닫힘
            _transientActive = true;
            _transientMessage = message;
            _transientUntil = Time.time + 3.5f;
            if (!same)
                Debug.Log($"[151차 P3] 농경 메시지 → 대화판넬 표출: {message}");
            _instance.ScheduleTransientClose();
        }

        /// <summary>[151차 P3] 트랜지언트 자동 닫힘 스케줄 — 패널 스케줄러(부착 후 유효), 매 틱 만료 판정.</summary>
        private void ScheduleTransientClose()
        {
            _transientCloseItem?.Pause();
            if (parent == null) return;
            _transientCloseItem = schedule.Execute(() =>
            {
                if (!_transientActive || Time.time < _transientUntil) return;
                _transientActive = false;
                _transientCloseItem?.Pause();
                Close();
            });
        }

        /// <summary>Show the compact panel with the NPC's two dialogue lines and no choices.</summary>
        public void ShowDialogueOnly()
        {
            _playerLine.style.display = DisplayStyle.None;
            _npcLineOne.style.display = DisplayStyle.Flex;
            _npcLineTwo.style.display = string.IsNullOrEmpty(_npcLineTwo.text) ? DisplayStyle.None : DisplayStyle.Flex;
            _choiceGrid.Clear();
            _choiceGrid.style.display = DisplayStyle.None;
            _actionHint.style.display = DisplayStyle.Flex;
            style.height = DialogueHeight;
            Show();
        }

        /// <summary>Show three clickable choice cards.</summary>
        public void ShowWithChoices(string[] choices)
        {
            _playerLine.style.display = DisplayStyle.None;
            _npcLineOne.style.display = DisplayStyle.Flex;
            _npcLineTwo.style.display = string.IsNullOrEmpty(_npcLineTwo.text) ? DisplayStyle.None : DisplayStyle.Flex;
            _choiceGrid.Clear();
            _choiceGrid.style.display = DisplayStyle.Flex;
            _choiceGrid.style.justifyContent = Justify.FlexStart;   // [피그마] 3열 고정 좌측 정렬(74차 "2열" 주석 오기 정정)
            _actionHint.style.display = DisplayStyle.Flex;
            style.height = ChoiceHeight;

            for (int i = 0; i < 3; i++)
            {
                string choiceText = choices != null && i < choices.Length && !string.IsNullOrEmpty(choices[i])
                    ? choices[i]
                    : "선택지 없음";
                _choiceGrid.Add(CreateChoiceCard(i + 1, choiceText, () => SelectChoice(choiceText)));
            }

            Show();
        }

        private void SetNpc(NPCInstance npc)
        {
            _nameLabel.text = string.IsNullOrEmpty(npc.NpcName) ? "NPC" : npc.NpcName;
            _npcLineOne.text = string.IsNullOrEmpty(npc.Greeting) ? "..." : npc.Greeting;

            string secondLine = null;
            if (npc.Dialogues != null && npc.Dialogues.Count > 0)
                secondLine = npc.Dialogues[0];
            if (string.IsNullOrEmpty(secondLine))
                secondLine = npc.QuestOfferLine;
            _npcLineTwo.text = string.IsNullOrEmpty(secondLine) ? "" : secondLine;
            _npcLineTwo.style.display = string.IsNullOrEmpty(secondLine) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void SelectChoice(string choiceText)
        {
            _transientActive = false;   // [151차 P3] 선택지 진행 — 트랜지언트 상태 해제
            _playerLine.text = "나: " + choiceText;
            _playerLine.style.display = DisplayStyle.Flex;
            _npcLineTwo.style.display = DisplayStyle.None;
            _choiceGrid.Clear();
            _choiceGrid.style.display = DisplayStyle.None;
            _actionHint.style.display = DisplayStyle.Flex;
            style.height = DialogueHeight;

            // [89차] 주민 모드 — 1행 응답 갱신(GetInteractionLine 재호출) + npc SetTalkBTrigger(Animator "Talk2")
            //   + PlaceAtAnchor(npc.transform) 재배치([151차 P2] 항상 중앙 하단 고정).
            if (_villagerNpc != null)
            {
                _npcLineOne.text = _villagerNpc.GetInteractionLine();
                _villagerNpc.SetTalkBTrigger();
                PlaceAtAnchor(_villagerNpc.transform);
            }
        }

        private static Label CreateDialogueLabel(string elementName)
        {
            var label = new Label { name = elementName };
            // [74차] 피그마 진실치 — DialogueLine 15.6px, 높이 18(구 12px/15 폐기). 색은 호출부에서 1행/2행 분기.
            label.style.height = 18f;
            label.style.minHeight = 18f;
            label.style.fontSize = 15.6f;
            label.style.color = new StyleColor(new Color(0.918f, 0.929f, 0.937f));   // #EAEDEF 기본(1행)
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        private static Button CreateChoiceCard(int number, string text, System.Action onClick)
        {
            var button = UTKButton.Create("", onClick, UTKButton.Variant.Secondary);
            button.name = "ChoiceCard" + number;
            // [74차 — 피그마 187:14 ChoiceCard 진실치] 카드 159.2×28.8(3열 고정 — x 15.6/184.4/353.3, gap 9.6),
            //   bg #FFFFFF/3% 반경 7.2, "선택 N" 12/700 #58A6FF, 본문 13.2/500 #EBEDF0.
            button.style.width = 159.2f;
            button.style.height = 28.8f;
            button.style.minHeight = 28.8f;
            button.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.031f));   // [144차 P2-DLG #30] 0.03→0.031
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 7.2f;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 7.2f;
            // [144차 P1-DLG #31] 카드 테두리 #30363D@0.302 w1.2 추가.
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 1.2f;
            button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor =
                new StyleColor(new Color(0.188f, 0.212f, 0.239f, 0.302f));   // #30363D/30.2%
            button.style.marginRight = number < 3 ? 9.6f : 0f;
            button.style.paddingLeft = 9.6f;   // [144차 P1-DLG #33] pl/pr 12→9.6
            button.style.paddingRight = 9.6f;
            button.style.paddingTop = 4.8f;    // [144차 P1-DLG #33] pt/pb 0→4.8
            button.style.paddingBottom = 4.8f;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.FlexStart;

            // 번호 라벨 — 12/700 #58A6FF.
            var title = new Label("선택 " + number);
            title.style.fontSize = 12f;
            title.style.height = 14f;
            title.style.minHeight = 14f;
            title.style.color = new StyleColor(new Color(0.345f, 0.651f, 1f));   // #58A6FF
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginRight = 8f;
            // 본문 대화 텍스트 — 13.2/500 #EBEDF0.
            var body = new Label(text);
            body.style.fontSize = 13.2f;
            body.style.height = 15f;
            body.style.minHeight = 15f;
            body.style.color = new StyleColor(new Color(0.922f, 0.929f, 0.941f));   // #EBEDF0
            body.style.overflow = Overflow.Hidden;
            body.style.whiteSpace = WhiteSpace.NoWrap;
            body.style.flexShrink = 1f;
            button.Add(title);
            button.Add(body);
            return button;
        }
    }
}
