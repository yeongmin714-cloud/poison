using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Utils
{
    public static class TimeUtils
    {
        public static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60);
            int secs = Mathf.FloorToInt(seconds % 60);
            return string.Format("{0:00}:{1:00}", minutes, secs);
        }
        
        public static string FormatTimeWithHours(float seconds)
        {
            int hours = Mathf.FloorToInt(seconds / 3600);
            int minutes = Mathf.FloorToInt((seconds % 3600) / 60);
            int secs = Mathf.FloorToInt(seconds % 60);
            return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, secs);
        }
        
        public static float GetTimeRemaining(float startTime, float duration)
        {
            return duration - (Time.time - startTime);
        }
    }
}