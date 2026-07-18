using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectName.UI.Core.Transitions
{
   public class ColorTransition : Transition
   {
       [Header("Color Transition Settings")]
       public Color fromColor = Color.clear;
       public Color toColor = Color.clear;
       public Graphic targetGraphic;
       
       protected override IEnumerator DoTransition()
       {
           if (targetGraphic == null)
           {
               Debug.LogError("Target Graphic not assigned for ColorTransition");
               yield break;
           }
           
           float elapsedTime = 0f;
           Color startColor = fromColor;
           
           while (elapsedTime < duration)
           {
               elapsedTime += Time.deltaTime;
               float t = curve.Evaluate(elapsedTime / duration);
               Color newColor = Color.Lerp(startColor, toColor, t);
               
               // Apply color to target
               targetGraphic.color = newColor;
               
               yield return null;
           }
           
           // Ensure final color is applied
           targetGraphic.color = toColor;
       }
   }
}