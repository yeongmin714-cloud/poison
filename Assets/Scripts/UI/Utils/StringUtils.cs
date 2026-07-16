using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Utils
{
    public static class StringUtils
    {
        public static string TruncateString(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || maxLength <= 0)
                return input;
            
            if (input.Length <= maxLength)
                return input;
            
            return input.Substring(0, maxLength) + "...";
        }
        
        public static string FormatNumber(int number)
        {
            if (number >= 1000000)
                return (number / 1000000f).ToString("F1") + "M";
            else if (number >= 1000)
                return (number / 1000f).ToString("F1") + "K";
            else
                return number.ToString();
        }
        
        public static string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            
            return char.ToUpper(input[0]) + input.Substring(1);
        }
        
        public static string JoinWithComma(List<string> items)
        {
            return string.Join(", ", items);
        }
    }
}