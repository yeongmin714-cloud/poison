using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>대상 영주를 향한 심문/처분 대화 패널 (침략 encounter 전용).</summary>
    public sealed class InvadedLordDialogueUTK : UTKWindowBase
    {
        private const float Width = 520f;
        private const float Height = 300f;
        private static InvadedLordDialogueUTK _instance;

        private readonly Label _lordLabel;
        private readonly Label _line;
        private readonly VisualElement _choices;
        private InvadedLordEncounter _encounter;

        private InvadedLordDialogueUTK() : base("영주와 대화", new Vector2(Width, Height))
        {
            style.width = Width;
            style.minWidth = Width;
            style.height = Height;
            style.minHeight = Height;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 18f;
            _content.style.paddingRight = 18f;
            _content.style.paddingTop = 16f;
            _content.style.paddingBottom = 16f;
            _lordLabel = new Label("영주") { name = "InvadedLordName" };
            _lordLabel.style.fontSize = 22f;
            _lordLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _lordLabel.style.color = new StyleColor(UTKTheme.Gold);
            _content.Add(_lordLabel);

            _line = new Label("") { name = "InvadedLordDialogueLine" };
            _line.style.marginTop = 14f;
            _line.style.whiteSpace = WhiteSpace.Normal;
            _line.style.fontSize = 16f;
            _line.style.flexGrow = 1f;
            _line.style.color = new StyleColor(UTKTheme.TextMain);
            _content.Add(_line);

            _choices = new VisualElement { name = "InvadedLordChoices" };
            _choices.style.flexDirection = FlexDirection.Row;
            _choices.style.justifyContent = Justify.SpaceBetween;
            _choices.style.marginTop = 14f;
            _content.Add(_choices);
        }

        public static InvadedLordDialogueUTK Open(InvadedLordEncounter encounter)
        {
            if (encounter == null) return null;
            if (_instance == null) _instance = new InvadedLordDialogueUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && _instance.parent == null) root.Add(_instance);
            _instance._encounter = encounter;
            _instance._lordLabel.text = "👑 " + encounter.LordName;
            _instance.BuildChoices();
            _instance.UpdateDialogue();
            _instance.Show();
            return _instance;
        }

        private void BuildChoices()
        {
            _choices.Clear();
            AddChoice("처형하기", OnExecute, UTKButton.Variant.Danger);
            AddChoice("추궁하기", OnInterrogate, UTKButton.Variant.Secondary);
            AddChoice("풀어주기", OnRelease, UTKButton.Variant.Primary);
        }

        private void AddChoice(string text, System.Action callback, UTKButton.Variant variant)
        {
            var button = UTKButton.Create(text, callback, variant);
            button.name = "InvadedLordChoice_" + text;
            button.style.width = 148f;
            button.style.height = 42f;
            _choices.Add(button);
        }

        private void OnExecute()
        {
            if (!CanResolve()) return;
            if (_encounter.Resolve(true))
            {
                _line.text = _encounter.LordName + "의 처형이 집행되었다.";
                SetChoicesEnabled(false);
                Debug.Log("[InvadedLordDialogueUTK] ExecuteLord — " + _encounter.TerritoryKey);
            }
        }

        private void OnRelease()
        {
            if (!CanResolve()) return;
            if (_encounter.Resolve(false))
            {
                _line.text = _encounter.LordName + "을(를) 풀어주었다.";
                SetChoicesEnabled(false);
                Debug.Log("[InvadedLordDialogueUTK] SpareLord — " + _encounter.TerritoryKey);
            }
        }

        private void OnInterrogate()
        {
            if (_encounter == null || _encounter.State == InvadedLordEncounter.EncounterState.Resolved) return;
            string line = _encounter.State == InvadedLordEncounter.EncounterState.Defeated
                ? _encounter.LordName + "은(는) 무너진 채 자신의 선택에 대해 답했다. (추궁 결과는 대화에만 반영됨)"
                : _encounter.LordName + "은(는) " + (_encounter.IsInCombat ? "전투 중에는" : "아직 제압되지 않아") + " 추궁에 응하지 않는다.";
            _line.text = line;
            Debug.Log("[InvadedLordDialogueUTK] Interrogate (scripted only) — " + _encounter.TerritoryKey);
        }

        private bool CanResolve()
        {
            if (_encounter == null || _encounter.State == InvadedLordEncounter.EncounterState.Resolved) return false;
            if (_encounter.State == InvadedLordEncounter.EncounterState.Defeated) return true;
            _line.text = _encounter.LordName + "은(는) 아직 제압되지 않았다. 먼저 공격해 굴복시켜야 한다.";
            return false;
        }

        private void SetChoicesEnabled(bool enabled)
        {
            foreach (var child in _choices.Children())
                if (child is Button button) button.SetEnabled(enabled);
        }

        private void UpdateDialogue()
        {
            if (_encounter == null) return;
            _line.text = _encounter.State == InvadedLordEncounter.EncounterState.Defeated
                ? _encounter.LordName + "은(는) 패배했다. 처분을 선택하거나 추궁할 수 있다."
                : "이 영주의 처분을 결정하거나, 공격하기를 선택해 결투를 시작할 수 있다.";
        }

        public override void Hide()
        {
            base.Hide();
            _encounter = null;
        }
    }
}
