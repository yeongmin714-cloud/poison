using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 미식 등급 (별점 1~5) — GAME_DATA.md section 4.2
    /// </summary>
    public struct GourmetGrade
    {
        public int Stars { get; }          // 1~5
        public string GradeName { get; }   // e.g., "서민", "평민", "중급", "상급", "왕실"
        public string Description { get; } // 특징 설명

        public GourmetGrade(int stars, string gradeName, string description)
        {
            Stars = stars;
            GradeName = gradeName;
            Description = description;
        }
    }

    /// <summary>
    /// Loads gourmet rating data from GAME_DATA.md section 4.2.
    /// Thread-safe on initialization.
    /// </summary>
    public static class GourmetDatabase
    {
        private static Dictionary<int, GourmetGrade> _grades = new Dictionary<int, GourmetGrade>();
        private static bool _initialized;
        private static readonly object _lock = new object();

        private static readonly Regex TableRowRegex = new Regex(@"^\s*\|.*\|.*\|.*\|");
        private static readonly Regex SeparatorRegex = new Regex(@":---");

        private static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;
                _initialized = true;
            }

            TextAsset txt = Resources.Load<TextAsset>("GAME_DATA");
            if (txt == null)
            {
                Debug.LogError("[GourmetDatabase] Failed to load GAME_DATA.md.");
                return;
            }

            string content = txt.text;

            // Find section 4.2
            const string startMarker = "### 4.2 미식 등급 (별점 시스템)";
            int startIdx = content.IndexOf(startMarker);
            if (startIdx < 0)
            {
                Debug.LogError("[GourmetDatabase] Could not find gourmet section (4.2).");
                return;
            }

            int pos = startIdx + startMarker.Length;
            string[] lines = content.Substring(pos).Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            foreach (string lineRaw in lines)
            {
                string trimmed = lineRaw.Trim();
                if (trimmed.StartsWith("---") || trimmed.StartsWith("## "))
                    break;

                if (SeparatorRegex.IsMatch(trimmed))
                    continue;

                if (TableRowRegex.IsMatch(trimmed))
                {
                    // Split by |
                    string[] parts = trimmed.Split('|');
                    if (parts.Length >= 4)
                    {
                        string starStr = parts[1].Trim();
                        string gradeName = parts[2].Trim();
                        string desc = parts[3].Trim();

                        // Skip header — both columns must match header labels
                        if (starStr.Equals("별점") && gradeName.Equals("등급"))
                            continue;

                        // Count ★ to determine star rating
                        int stars = 0;
                        foreach (char c in starStr)
                            if (c == '★') stars++;

                        if (stars >= 1 && stars <= 5)
                        {
                            _grades[stars] = new GourmetGrade(stars, gradeName, desc);
                        }
                    }
                }
            }

            Debug.Log($"[GourmetDatabase] Loaded {_grades.Count} gourmet grades (★1~★{_grades.Count}).");
        }

        public static GourmetGrade? GetGrade(int stars)
        {
            Initialize();
            if (_grades.TryGetValue(stars, out var grade))
                return grade;
            return null;
        }

        public static IReadOnlyDictionary<int, GourmetGrade> All
        {
            get
            {
                Initialize();
                return new ReadOnlyDictionary<int, GourmetGrade>(_grades);
            }
        }
    }
}