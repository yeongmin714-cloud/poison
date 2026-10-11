using MagicPigGames.Shared;
using UnityEditor;
using UnityEngine;

namespace MagicPigGames.MagicTime
{
    [CustomEditor(typeof(LocalTimeScale), true)]
    public class LocalTimeScaleEditor : MagicPigEditor
    {
        public LocalTimeScale Target => (LocalTimeScale)target;

        public override void OnInspectorGUI()
        {
            Header();
            base.OnInspectorGUI();
        }
        
        private void Header()
        {
            StartRow();
            Label($"{textHightlight}MAGIC TIME{textColorEnd}", 150, true, false, true);
            LinkToDocs(LocalTimeEditorUtilities.DocsUrl);
            BackgroundColor(Color.cyan);
            if (Button($"Discord {symbolCircleArrow}"
                    , "This will open the Discord."))
                Application.OpenURL("https://discord.com/invite/cmZY2tH");
            ResetColor();
            EndRow();
            GreyLine();
            Header2($"{Target.name}", true);
            LabelGrey($"{Target.GetType()}");
        }
    }
}