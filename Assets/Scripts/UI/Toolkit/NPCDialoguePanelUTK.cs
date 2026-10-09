using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI;

namespace ProjectName.UI.Toolkit
{
    /// <summary>Compact NPC dialogue panel with dialogue-only and three-choice modes.</summary>
    public class NPCDialoguePanelUTK : UTKWindowBase
    {
        private const float PanelWidth = 440f;
        private const float DialogueHeight = 124f;
        private const float ChoiceHeight = 144f;

        private static readonly string[] DefaultChoices =
        {
            "대요정님, 저는 드류예요.",
            "오늘은 어때요?",
            "오늘은 무슨 일이시죠?"
        };

        private static NPCDialoguePanelUTK _instance;

        private readonly Label _nameLabel;
        private readonly Label _npcLineOne;
        private readonly Label _npcLineTwo;
        private readonly Label _playerLine;
        private readonly VisualElement _choiceGrid;
        private readonly Label _actionHint;

        public static NPCDialoguePanelUTK Instance => _instance;

        private NPCDialoguePanelUTK() : base("NPC 대화", new Vector2(PanelWidth, DialogueHeight), UTKWindowChrome.Frameless)
        {
            style.width = PanelWidth;
            style.height = DialogueHeight;
            style.backgroundColor = new StyleColor(new Color32(0x16, 0x1B, 0x22, 0xF2));
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new StyleColor(new Color32(0x3A, 0x42, 0x4D, 0xFF));
            style.borderTopLeftRadius = style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = style.borderBottomRightRadius = 8f;

            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 12f;
            _content.style.paddingRight = 12f;
            _content.style.paddingTop = 12f;
            _content.style.paddingBottom = 12f;

            var nameRow = new VisualElement { name = "NpcNameRow" };
            nameRow.style.height = 13f;
            nameRow.style.minHeight = 13f;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            var indicator = new VisualElement { name = "NpcIndicator" };
            indicator.style.width = 6f;
            indicator.style.height = 6f;
            indicator.style.marginRight = 6f;
            indicator.style.backgroundColor = new StyleColor(new Color32(0xE3, 0xB3, 0x41, 0xFF));
            indicator.style.borderTopLeftRadius = indicator.style.borderTopRightRadius = 3f;
            indicator.style.borderBottomLeftRadius = indicator.style.borderBottomRightRadius = 3f;
            nameRow.Add(indicator);

            _nameLabel = new Label("NPC") { name = "NpcName" };
            _nameLabel.style.fontSize = 12f;
            _nameLabel.style.color = new StyleColor(new Color32(0xF0, 0xF6, 0xFC, 0xFF));
            nameRow.Add(_nameLabel);
            _content.Add(nameRow);

            var separator = new VisualElement { name = "Separator" };
            separator.style.height = 1f;
            separator.style.minHeight = 1f;
            separator.style.marginTop = 7f;
            separator.style.marginBottom = 7f;
            separator.style.backgroundColor = new StyleColor(new Color32(0x3A, 0x42, 0x4D, 0xFF));
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
            dialogue.Add(_npcLineOne);
            _npcLineTwo = CreateDialogueLabel("NpcDialogueLine2");
            _npcLineTwo.style.marginTop = 6f;
            dialogue.Add(_npcLineTwo);
            _content.Add(dialogue);

            _choiceGrid = new VisualElement { name = "ChoiceGrid" };
            _choiceGrid.style.height = 24f;
            _choiceGrid.style.minHeight = 24f;
            _choiceGrid.style.flexDirection = FlexDirection.Row;
            _choiceGrid.style.justifyContent = Justify.SpaceBetween;
            _choiceGrid.style.display = DisplayStyle.None;
            _content.Add(_choiceGrid);

            _actionHint = new Label("계속하려면 클릭 ▶") { name = "ActionHint" };
            _actionHint.style.height = 12f;
            _actionHint.style.minHeight = 12f;
            _actionHint.style.marginTop = 6f;
            _actionHint.style.fontSize = 12f;
            _actionHint.style.color = new StyleColor(new Color32(0x8B, 0x94, 0x9E, 0xFF));
            _actionHint.style.unityTextAlign = TextAnchor.MiddleRight;
            _content.Add(_actionHint);

            _actionHint.RegisterCallback<ClickEvent>(_ => Close());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureAttached();
        }

        private static void EnsureAttached()
        {
            if (_instance == null)
                _instance = new NPCDialoguePanelUTK();

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && _instance.parent == null)
                root.Add(_instance);
        }

        /// <summary>Open the compact panel for an NPC; defaults to the three-choice version.</summary>
        public static void Open(NPCInstance npc)
        {
            EnsureAttached();
            _instance.SetNpc(npc);
            _instance.ShowWithChoices(DefaultChoices);
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
            _playerLine.text = "나: " + choiceText;
            _playerLine.style.display = DisplayStyle.Flex;
            _npcLineTwo.style.display = DisplayStyle.None;
            _choiceGrid.Clear();
            _choiceGrid.style.display = DisplayStyle.None;
            _actionHint.style.display = DisplayStyle.Flex;
            style.height = DialogueHeight;
        }

        private static Label CreateDialogueLabel(string elementName)
        {
            var label = new Label { name = elementName };
            label.style.height = 15f;
            label.style.minHeight = 15f;
            label.style.fontSize = 12f;
            label.style.color = new StyleColor(new Color32(0xD8, 0xDE, 0xE8, 0xFF));
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        private static Button CreateChoiceCard(int number, string text, System.Action onClick)
        {
            var button = UTKButton.Create("", onClick, UTKButton.Variant.Secondary);
            button.name = "ChoiceCard" + number;
            button.style.width = new Length(33.333f, LengthUnit.Percent);
            button.style.height = 24f;
            button.style.minHeight = 24f;
            button.style.marginRight = number < 3 ? 4f : 0f;
            button.style.paddingLeft = 5f;
            button.style.paddingRight = 5f;
            button.style.paddingTop = 2f;
            button.style.paddingBottom = 2f;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.FlexStart;
            button.style.justifyContent = Justify.Center;

            var title = new Label("선택 " + number);
            title.style.fontSize = 12f;
            title.style.height = 12f;
            title.style.minHeight = 12f;
            title.style.color = new StyleColor(new Color32(0x8B, 0x94, 0x9E, 0xFF));
            var body = new Label(text);
            body.style.fontSize = 13.2f;
            body.style.height = 13f;
            body.style.minHeight = 13f;
            body.style.color = new StyleColor(new Color32(0xF0, 0xF6, 0xFC, 0xFF));
            body.style.overflow = Overflow.Hidden;
            body.style.whiteSpace = WhiteSpace.NoWrap;
            button.Add(title);
            button.Add(body);
            return button;
        }
    }
}
