using UnityEngine;

/*
 * Add this to any Projectile that you want to be affected by the Magic Time system. It will handle
 * the timeScale values for any Trail Renderers on the object.
 */

namespace MagicPigGames.MagicTime
{
    [System.Serializable]
    public class MagicTimeUserTrailRenderer : MagicTimeMonoBehaviour
    {
        public MagicTimeUser magicTimeUser;
        public TrailRenderer[] trailRenderers;
        private float[] _cachedTrailTime;
        
        protected float _cachedTimeScale;

        public void Awake()
        {
            OnValidate();
        }
        
        public virtual void Update()
        {
            // If the timeScale hasn't changed, no need to update
            if (Mathf.Approximately(magicTimeUser.TimeScale, _cachedTimeScale))
                return;

            _cachedTimeScale = magicTimeUser.TimeScale;

            for (var i = 0; i < trailRenderers.Length; i++)
            {
                var tr = trailRenderers[i];
                SetTrailRendererTime(tr, _cachedTrailTime[i], magicTimeUser.InverseTimeScale);
            }
        }

        private void OnEnable()
        {
            // Reset the time and minVertexDistance values when the object is enabled
            for (var i = 0; i < trailRenderers.Length; i++)
                SetTrailRendererTime(trailRenderers[i], _cachedTrailTime[i], 1);
        }

        /// <summary>
        /// Adjusts the time property of the TrailRenderer based on the proportional time scale.
        /// </summary>
        /// <param name="tr"></param>
        /// <param name="originalTime"></param>
        /// <param name="timeScale"></param>
        protected virtual void SetTrailRendererTime(TrailRenderer tr, float originalTime, float timeScale)
        {
            // Inversely scale the trail time, so the trail lasts longer as time slows down.
            tr.time = originalTime / timeScale;
        }
        
        /// <summary>
        /// Automatically populate TrailRenderers with all in children.
        /// </summary>
        public virtual void OnValidate()
        {
            // Automatically find the TrailRenderers in children
            if (trailRenderers == null || trailRenderers.Length == 0)
                trailRenderers = GetComponentsInChildren<TrailRenderer>();

            // Ensure LocalTimeScaleUser reference is populated
            if (magicTimeUser == null)
                magicTimeUser = GetComponent<MagicTimeUser>();
            
            // Cache the initial time and minVertexDistance values
            _cachedTrailTime = new float[trailRenderers.Length];
            for (var i = 0; i < trailRenderers.Length; i++)
                _cachedTrailTime[i] = trailRenderers[i].time;
        }
    }
}