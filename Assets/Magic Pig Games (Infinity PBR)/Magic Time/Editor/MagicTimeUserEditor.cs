using System.Collections.Generic;
using MagicPigGames.Shared;
using UnityEditor;
using UnityEngine;

namespace MagicPigGames.MagicTime
{
    [CustomEditor(typeof(MagicTimeUser), true)]
    public class MagicTimeUserEditor : MagicPigEditor
    {
        public MagicTimeUser Target => (MagicTimeUser)target;

        public override void OnInspectorGUI()
        {
            Header();
            base.OnInspectorGUI();
            BlackLine();
            ShowRuntimeInformation();
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

        private void ShowRuntimeInformation()
        {
            Label("In Game TimeScales", true, true, true);
            // If not in play mode, don't show runtime information
            if (!Application.isPlaying)
            {
                MessageBox($"When in play mode, this will show the current TimeScales this subscribes to.");
                return;
            }

            Label($"There are {Target.SubscribedTimeScales.Count} TimeScales affecting this object.", false, true, true);

            StartRow();
            Label($"TimeScale", 200, true);
            Label($"Value", 60, true);
            Label($"", 300);
            EndRow();
            if (Target.ObjectTimeScale != null)
                ShowTimeScale("This Object", Target.ObjectTimeScale, "LocalTimeScale on this object.");

            foreach (var timeScale in Target.SubscribedTimeScales)
            {
                if (timeScale == null || timeScale == Target.ObjectTimeScale) continue;
                ShowTimeScale(timeScale.name, timeScale);
            }

            Space();
            StartRow();
            Label($"Final TimeScale: {Target.TimeScale}", 350, true);
            EndRow();
        }

        private void ShowTimeScale(string label, LocalTimeScale timeScale, string additional = "")
        {
            if (timeScale == null) return;
            StartRow();
            Label($"{label}", 200);
            Label($"{timeScale.Value}", 60);
            Label($"<i>{textMuted}{additional}{textColorEnd}</i>", 300, false, true, true);
            EndRow();
        }
    }
}