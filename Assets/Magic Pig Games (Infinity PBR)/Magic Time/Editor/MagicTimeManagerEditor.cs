using System.Collections.Generic;
using System.Runtime.InteropServices;
using MagicPigGames.Shared;
using UnityEditor;
using UnityEngine;

namespace MagicPigGames.MagicTime
{
    [CustomEditor(typeof(MagicTimeManager), true)]
    public class MagicTimeManagerEditor : MagicPigEditor
    {
        public MagicTimeManager Target => (MagicTimeManager)target;

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
                MessageBox($"When in play mode, this will show the current TimeScales in the MagicTimeManager.");
                return;
            }

            Label($"There are {Target.TimeScales.Count} TimeScales in the MagicTimeManager.", false, true, true);
            Label($"<i>Local Time Scales will not be shown here.</i>", false, true, true);

            Space();
            foreach (var timeScale in Target.TimeScales)
            {
                Label(timeScale.Key, true, true, true);
                Label($"Value: {timeScale.Value.Value}", false, true, true);
                Label($"Subscribers: {timeScale.Value.SubscriberCount}", false, true, true);
                Space();
            }
        }

        private void OnEnable()
        {
            // Ensure we are not in play mode to avoid runtime modifications
            if (Application.isPlaying)
                return;

            PopulateAllTimeScales();
        }

        private void PopulateAllTimeScales()
        {
            // Initialize the list if it's null
            Target.allTimeScales ??= new List<LocalTimeScale>();
            Target.allTimeScales.Clear();

            // Populate _allTimeScales using the utility method
            Target.allTimeScales.AddRange(MagicTimeEditorUtilities.FindAllLocalTimeScalesInProject());
            EditorUtility.SetDirty(Target);
        }
    }
}