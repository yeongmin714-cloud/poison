using UnityEngine;

namespace TimeWarp.Utils
{
    /// <summary>
    /// Skrypt pozwalający na podmianę wielu materiałów z poziomu Unity Events,
    /// zachowując wygodę przeciągania materiału bezpośrednio do okienka Inspektora.
    /// </summary>
    [AddComponentMenu("TimeWarp/Utils/Direct Material Event Helper")]
    public class TimeWarpDirectMaterialHelper : MonoBehaviour
    {
        [Tooltip("Renderer, na którym chcemy zmieniać materiały.")]
        public Renderer TargetRenderer;
        
        private Material[] _originalMaterials;
        private bool _isSwapped = false;

        private void Awake()
        {
            if (TargetRenderer == null) TargetRenderer = GetComponent<Renderer>();
            
            // Zapisujemy całą oryginalną tablicę materiałów na starcie
            if (TargetRenderer != null)
            {
                _originalMaterials = TargetRenderer.sharedMaterials;
            }
        }

        // ==========================================
        // FUNKCJE POD UNITY EVENTS
        // Wybierasz je w Inspektorze i obok przeciągasz materiał
        // ==========================================

        public void SwapMaterial_Index0(Material newMat) => ApplySwap(0, newMat);
        public void SwapMaterial_Index1(Material newMat) => ApplySwap(1, newMat);
        public void SwapMaterial_Index2(Material newMat) => ApplySwap(2, newMat);
        public void SwapMaterial_Index3(Material newMat) => ApplySwap(3, newMat);
        public void SwapMaterial_Index4(Material newMat) => ApplySwap(4, newMat);
        public void SwapMaterial_Index5(Material newMat) => ApplySwap(5, newMat);

        /// <summary>
        /// Wraca wszystkie zmienione materiały do oryginału za jednym zamachem.
        /// </summary>
        public void RestoreAllOriginalMaterials()
        {
            if (_isSwapped && TargetRenderer != null && _originalMaterials != null)
            {
                TargetRenderer.materials = _originalMaterials;
                _isSwapped = false;
            }
        }

        // ==========================================
        // WEWNĘTRZNA LOGIKA
        // ==========================================
        private void ApplySwap(int index, Material newMat)
        {
            if (TargetRenderer == null || newMat == null || _originalMaterials == null) return;
            
            Material[] mats = TargetRenderer.materials;
            if (index >= 0 && index < mats.Length)
            {
                mats[index] = newMat;
                TargetRenderer.materials = mats; // Aktualizacja grafiki
                _isSwapped = true;
            }
        }
    }
}