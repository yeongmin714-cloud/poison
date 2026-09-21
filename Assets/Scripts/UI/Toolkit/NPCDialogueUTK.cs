// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round A — NPC 대화 윈도우 (UTK).
    /// 원본: Assets/Scripts/UI/NPCDialogueWindow.cs (445줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 대화 진행 — NPCInstance.Greeting / QuestOfferLine 순차 표시 (NPCInstance 실측 API 직접 사용).
    ///  ② 퀘스트 목록 — NPCInstance.QuestIds → QuestManager.GetQuest/GetQuestState 실측 조회,
    ///     상태별 수락(Available)/진행(Active)/보상(Completed) 처리.
    ///  ③ 보상 — QuestManager.TryCompleteQuest 실측 호출 후 목록 새로고침.
    ///  ④ 닫기 — 기본 UTKWindowBase 닫기 버튼 + "닫기 ✕" 버튼.
    ///  각 경로에 [NPCDialogUTK] UnityEngine.Debug 로그.
    /// [진입점] static Ensure() / Open() / OpenNPC(NPCInstance) / Toggle(). 순수 VisualElement 트리, 400ms 폴링.
    /// </summary>
    public class NPCDialogueUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static NPCDialogueUTK _instance;
        public static NPCDialogueUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new NPCDialogueUTK();
        }

        /// <summary>빈 NPC 대화 창 열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>주어진 NPC로 대화 창 열기 (원본 NPCDialogueWindow.ShowDialogue 대응).</summary>
        public static void OpenNPC(NPCInstance npc)
        {
            Ensure();
            _instance.BeginDialogue(npc);
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
        }

        // ===== 설정 =====
        private const float WinW = 560f;
        private const float WinH = 460f;
        private const long RefreshMs = 400L;

        private enum Mode { Dialogue, QuestList, Info, Gift }

        // ===== 상태 =====
        private NPCInstance _currentNPC;
        private Mode _mode = Mode.Dialogue;
        private readonly List<string> _dialogueLines = new List<string>();
        private int _currentLine;

        // ===== 레퍼런스 =====
        private readonly VisualElement _list;
        private Label _summaryLabel;
        private Label _contentLabel;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private NPCDialogueUTK() : base("💬 NPC 대화", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _summaryLabel = new Label("💬 NPC 대화");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 18f;
            _content.Add(_summaryLabel);

            _contentLabel = MakeLabel("", UTKColor.TextPrimary);
            _contentLabel.style.marginTop = 6f;
            _content.Add(_contentLabel);

            _list = new VisualElement();
            _list.name = "NPCDialogueList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 340f;
            style.top = 180f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 340f;
            style.top = 180f;
            StartRefreshLoop();
            Debug.Log("[NPCDialogUTK] NPC 대화 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[NPCDialogUTK] NPC 대화 창 닫힘");
        }

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                Refresh();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        // ===== 대화 시작 =====

        private void BeginDialogue(NPCInstance npc)
        {
            if (string.IsNullOrEmpty(npc.NpcName))
            {
                Debug.LogWarning("[NPCDialogUTK] 유효하지 않은 NPC 데이터");
                return;
            }

            _currentNPC = npc;
            _currentLine = 0;
            _mode = Mode.Dialogue;

            _dialogueLines.Clear();
            _dialogueLines.Add("\"" + npc.Greeting + "\"");
            if (npc.HasQuests)
            {
                _dialogueLines.Add("\"" + npc.QuestOfferLine + "\"");
                _dialogueLines.Add("---");
                _dialogueLines.Add("(NPC가 퀘스트를 줄 준비가 되었다.)");
            }
            else
            {
                _dialogueLines.Add("(NPC는 할 말이 없는 것 같다.)");
            }

            Show();
            Refresh();
        }

        // ===== 진입점 =====

        public void OpenForNPC(NPCInstance npc) => BeginDialogue(npc);

        // ===== 렌더링 =====

        private void Refresh()
        {
            _list.Clear();
            switch (_mode)
            {
                case Mode.Dialogue: DrawDialogue(); break;
                case Mode.QuestList: DrawQuestList(); break;
                case Mode.Info: DrawInfo(); break;
                case Mode.Gift: DrawGift(); break;
            }
        }

        private void DrawDialogue()
        {
            string ageIcon = AgeIcon(_currentNPC.AgeType);
            string questBadge = _currentNPC.HasQuests ? " ❓" : "";
            _summaryLabel.text = ageIcon + " " + _currentNPC.NpcName + questBadge;

            string line = (_currentLine >= 0 && _currentLine < _dialogueLines.Count)
                ? _dialogueLines[_currentLine]
                : "";
            _contentLabel.text = line;

            if (_currentNPC.HasQuests)
                _list.Add(UTKButton.Create("📋 퀘스트 목록 보기", GoToQuestList, UTKButton.Variant.Primary));

            _list.Add(UTKButton.Create("📋 정보보기", GoToInfo, UTKButton.Variant.Secondary));
            _list.Add(UTKButton.Create("💝 선물주기", GoToGift, UTKButton.Variant.Secondary));
            _list.Add(UTKButton.Create("다음 ▶", Advance, UTKButton.Variant.Secondary));
            _list.Add(UTKButton.Create("닫기 ✕", Close, UTKButton.Variant.Danger));
        }

        private void GoToInfo()
        {
            _mode = Mode.Info;
            _contentLabel.text = "";
            Refresh();
            Debug.Log("[NPCDialogUTK] NPC 정보 표시");
        }

        private void GoToGift()
        {
            _mode = Mode.Gift;
            _contentLabel.text = "";
            Refresh();
            Debug.Log("[NPCDialogUTK] NPC 선물 표시");
        }

        // ===== P30-D: NPC 정보보기 (중독도·이름) =====

        private void DrawInfo()
        {
            _summaryLabel.text = "📋 " + _currentNPC.NpcName + " 정보";

            _list.Add(MakeLabel("이름: " + _currentNPC.NpcName, UTKColor.TextPrimary));
            if (!string.IsNullOrEmpty(_currentNPC.TerritoryId))
            {
                string terrName = TerritoryDatabase.Instance.GetDefinition(_currentNPC.TerritoryId).territoryName;
                if (string.IsNullOrEmpty(terrName)) terrName = _currentNPC.TerritoryId;
                _list.Add(MakeLabel("소속 영지: " + terrName, UTKColor.TextSecondary));

                float cont = TerritoryDrugSystem.GetTerritoryContamination(_currentNPC.TerritoryId);
                float lord = TerritoryDrugSystem.GetLordAddiction(_currentNPC.TerritoryId);
                _list.Add(MakeLabel(cont > 0f
                    ? $"💊 마약 중독도: {cont:F0}/100 (영주 {lord:F0}/100)"
                    : "💊 마약 중독도: 없음", UTKColor.AccentMagic));
            }
            else
            {
                _list.Add(MakeLabel("(소속 영지 정보 없음)", UTKColor.TextSecondary));
            }

            _list.Add(UTKButton.Create("← 대화로 돌아가기", () =>
            {
                _mode = Mode.Dialogue;
                _currentLine = _dialogueLines.Count - 1;
                Refresh();
            }, UTKButton.Variant.Secondary));
        }

        // ===== P30-D: NPC 선물주기 (선물→호감도↑, 마약→중독도↑) =====

        private void DrawGift()
        {
            _summaryLabel.text = "💝 " + _currentNPC.NpcName + "에게 선물";
            _contentLabel.text = "선물(음식)은 호감도, 마약은 중독도를 올립니다.";

            var slots = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetAllSlots() : null;
            if (slots == null)
            {
                _list.Add(MakeLabel("(인벤토리 없음)", UTKColor.TextSecondary));
                return;
            }

            bool any = false;
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null || slot.count <= 0) continue;
                var cat = slot.item.category;
                if (cat != PlayerInventory.ItemCategory.Food && cat != PlayerInventory.ItemCategory.Drug) continue;
                any = true;
                _list.Add(BuildGiftRow(slot));
            }

            if (!any)
                _list.Add(MakeLabel("(선물할 음식/마약이 없습니다.)", UTKColor.TextSecondary));

            _list.Add(UTKButton.Create("← 대화로 돌아가기", () =>
            {
                _mode = Mode.Dialogue;
                _currentLine = _dialogueLines.Count - 1;
                Refresh();
            }, UTKButton.Variant.Secondary));
        }

        private VisualElement BuildGiftRow(PlayerInventory.ItemSlot slot)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;

            bool isDrug = slot.item.category == PlayerInventory.ItemCategory.Drug;
            var name = MakeLabel((isDrug ? "💊 " : "🍗 ") + slot.item.displayName + " x" + slot.count,
                isDrug ? UTKColor.AccentMagic : UTKColor.TextPrimary);
            name.style.flexGrow = 1f;
            row.Add(name);

            row.Add(UTKButton.Create("선물", () => GiveGift(slot), UTKButton.Variant.Primary));
            return row;
        }

        private void GiveGift(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null || PlayerInventory.Instance == null) return;

            bool isDrug = slot.item.category == PlayerInventory.ItemCategory.Drug;

            if (!string.IsNullOrEmpty(_currentNPC.TerritoryId))
            {
                var state = TerritoryDatabase.Instance.GetState(_currentNPC.TerritoryId);
                if (isDrug)
                {
                    // 마약 → 영지 중독도 상승
                    TerritoryDrugSystem.AddDrug(_currentNPC.TerritoryId, Mathf.Clamp((int)slot.item.rarity, 0, 5));
                    Debug.Log("[NPCDialogUTK] 💊 NPC에 마약 선물 — 영지 중독도 상승");
                }
                else if (state != null)
                {
                    // 선물(음식) → 호감도(영지 충성도) 상승
                    state.loyaltyToPlayer = Mathf.Clamp(state.loyaltyToPlayer + 5f, 0f, 100f);
                    Debug.Log("[NPCDialogUTK] 🍗 NPC에 선물 — 호감도(영지 충성도) 상승");
                }
            }

            PlayerInventory.Instance.RemoveItem(slot.item.id, 1);
            Refresh();
        }

        private void DrawQuestList()
        {
            _summaryLabel.text = "--- " + _currentNPC.NpcName + "의 퀘스트 ---";

            if (_currentNPC.QuestIds == null || _currentNPC.QuestIds.Count == 0)
            {
                _list.Add(MakeLabel("(사용 가능한 퀘스트가 없습니다.)", UTKColor.TextSecondary));
            }
            else
            {
                foreach (var questId in _currentNPC.QuestIds)
                {
                    DrawQuestEntry(questId);
                }
            }

            _list.Add(UTKButton.Create("← 대화로 돌아가기", () =>
            {
                _mode = Mode.Dialogue;
                _currentLine = _dialogueLines.Count - 1;
                Refresh();
            }, UTKButton.Variant.Secondary));
        }

        private void DrawQuestEntry(string questId)
        {
            QuestData quest = QuestManager.GetQuest(questId);
            QuestState state = QuestManager.GetQuestState(questId);

            if (string.IsNullOrEmpty(quest.questId) || string.IsNullOrEmpty(quest.questName))
            {
                _list.Add(MakeLabel("[알 수 없는 퀘스트: " + questId + "]", UTKColor.HealthRed));
                return;
            }

            string stateIcon = StateIcon(state);
            var info = MakeLabel(stateIcon + " " + quest.questName + " (Lv." + quest.requiredLevel + ")", UTKColor.TextPrimary);
            info.style.fontSize = 14f;
            _list.Add(info);

            if (state == QuestState.Available)
            {
                _list.Add(UTKButton.Create("수락", () =>
                {
                    AcceptQuest(questId);
                }, UTKButton.Variant.Primary));
            }
            else if (state == QuestState.Active)
            {
                _list.Add(MakeLabel("진행: " + GetQuestProgress(quest), UTKColor.TextSecondary));
            }
            else if (state == QuestState.Completed)
            {
                _list.Add(UTKButton.Create("보상", () =>
                {
                    ClaimReward(questId);
                }, UTKButton.Variant.Secondary));
            }
        }

        // ===== 동작 =====

        private void Advance()
        {
            if (_currentLine < _dialogueLines.Count - 1)
            {
                _currentLine++;
                Refresh();
                return;
            }

            if (_currentNPC.HasQuests)
                GoToQuestList();
            else
                Close();
        }

        private void GoToQuestList()
        {
            _mode = Mode.QuestList;
            _contentLabel.text = "";
            Refresh();
            Debug.Log("[NPCDialogUTK] 퀘스트 목록 표시");
        }

        private void AcceptQuest(string questId)
        {
            if (QuestManager.AcceptQuest(questId))
            {
                Debug.Log("[NPCDialogUTK] 퀘스트 수락됨: " + questId);
                Refresh();
            }
        }

        private void ClaimReward(string questId)
        {
            if (QuestManager.TryCompleteQuest(questId))
            {
                Debug.Log("[NPCDialogUTK] 보상 수령: " + questId);
                Refresh();
            }
        }

        private string GetQuestProgress(QuestData quest)
        {
            if (quest.objectives == null || quest.objectives.Count == 0)
                return "";

            int completed = 0;
            int total = quest.objectives.Count;
            foreach (var obj in quest.objectives)
            {
                if (obj.IsMet) completed++;
            }
            return completed + "/" + total;
        }

        // ===== 헬퍼 =====

        private static string AgeIcon(NPCData.NPCAgeType ageType)
        {
            switch (ageType)
            {
                case NPCData.NPCAgeType.Child: return "🧒";
                case NPCData.NPCAgeType.Elderly: return "👴";
                default: return "🧑";
            }
        }

        private static string StateIcon(QuestState state)
        {
            switch (state)
            {
                case QuestState.Available: return "📋";
                case QuestState.Active: return "⏳";
                case QuestState.Completed: return "✅";
                case QuestState.Locked: return "🔒";
                default: return "❓";
            }
        }

        private static Label MakeLabel(string text, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = 13f;
            l.style.color = new StyleColor(color);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }
    }
}