using UnityEngine;
using System.Collections.Generic;

public class StringUtils : MonoBehaviour
{
    public static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength) + "...";
    }
    
    public static bool IsNullOrEmpty(string text)
    {
        return string.IsNullOrEmpty(text);
    }
}