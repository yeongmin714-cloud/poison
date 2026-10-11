using MagicPigGames.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static MagicPigGames.MagicTime.LocalTimeEditorUtilities;

namespace MagicPigGames.MagicTime
{
    [CustomEditor(typeof(TimeZone), true)]
    public class TimeZoneEditor : MagicPigEditor
    {
        TimeZone Target => (TimeZone) target;
        
        public override void OnInspectorGUI()
        {
            Header();
            
            ShowOptions();
            
            BlackLine();
            ShowEvents();
            BlackLine();
            DrawDefaultInspectorToggle("Time Zone DefaultInspector");
            BlackLine();
            
            ShowRuntimeInformation();
        }

        private void ShowOptions()
        {
            Undo.RecordObject(Target, "Update Create New Local Time Scale");
            Target.createLocalTime = LeftCheck($"Create new Local Time Scale {symbolInfo}", "If checked, this will create a new LocalTimeScale " +
                "object when the Time Zone is created or instantiated.", Target.createLocalTime, 200);
            
            if (Target.createLocalTime && Target.TimeScale != null)
            {
                Target.TimeScale.SetValue(Target.TimeScaleValue);
                EditorUtility.SetDirty(Target);
            }
            
            Undo.RecordObject(Target, "Update Time Scale");
            ShowTimeScale();
            
            Undo.RecordObject(Target, "Update Time Scale Value");
            ShowTimeScaleValue();
        }

        private void ShowTimeScaleValue()
        {
            if (!Target.createLocalTime) return;
            
            StartRow();
            Label($"Time Scale Value {symbolInfo}", "This is the default value of the LocalTime object when it is" +
                                                    " created.", 150);
            var value = DelayedFloat(Target.TimeScaleValue, 50);
            if (!Mathf.Approximately(value, Target.TimeScaleValue))
            {
                Target.SetTimeScaleStartingValue(value);
                EditorUtility.SetDirty(Target);
            }
            EndRow();
            
            LabelGrey($"A new LocalTime will be created with starting value {Target.TimeScaleValue}.", 400,false, true, true);
        }

        private void ShowEvents()
        {
            EditorGUILayout.HelpBox("Events pass TimeZoneEventData containing:\n• GameObject - The object that triggered the event\n• Collider - The collider component involved\n• Vector3 contactPoint - The closest point on the collider to the zone center", MessageType.Info);
            Space();
            Undo.RecordObject(Target, "Update Events");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnObjectEnter"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnObjectExit"));
            serializedObject.ApplyModifiedProperties();
        }

        private void ShowTimeScale()
        {
            if (Target.createLocalTime) return;
            
            StartRow();
            Label($"Time Scale Object {symbolInfo}", "Assign a LocalTime Scriptable Object. A new instantiation of this object will " +
                                              "be created. Note: Each TimeZone using the same object will have its own version unique" +
                                              " to it.", 150);
            var cachedObject = Target.timeScaleToUse;
            Target.timeScaleToUse = Object(Target.timeScaleToUse, typeof(LocalTimeScale), 200, false) as LocalTimeScale;
            if (cachedObject != Target.timeScaleToUse)
                EditorUtility.SetDirty(Target);
            EndRow();
            if (Target.timeScaleToUse == null)
                LabelError($"No LocalTimeScale assigned. A new LocalTimeScale will be created with starting value {Target.TimeScaleValue}.", 400, true, true, true);
            else
                LabelGrey($"A new instance of the LocalTimeScale \"{Target.timeScaleToUse.name}\" will be created.", 400,false, true, true);
        }

        private void Header()
        {
            StartRow();
            Label($"{textHightlight}MAGIC TIME{textColorEnd}", 150, true, false, true);
            LinkToDocs(DocsUrl);
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
            Label("In Game Subscribers", true, true, true);
            
            // If not in play mode, don't show runtime information
            if (!Application.isPlaying)
            {
                MessageBox($"When in play mode, this will show the current Subscribers to this Time Zone.");
                return;
            }
            
            Label($"There are {Target.TimeScale.SubscriberCount} Subscribers to this Time Zone.", false, true, true);
            Space();
            
            foreach (var subscriber in Target.TimeScale.SubscriberGameObjects)
            {
                StartRow();
                PingButton(subscriber);
                Label(subscriber.name, 250, false, true, true);
                if (Button($"Remove", 75))
                    subscriber.GetComponent<IHaveLocalTime>().UnsubscribeFromLocalTimeScale(Target.TimeScale);
                EndRow();
            }
        }
    }

}