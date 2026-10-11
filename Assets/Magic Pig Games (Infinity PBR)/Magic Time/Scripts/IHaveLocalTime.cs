using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Any object which is affected by Local Time Scale should implement this interface. However, the Monobehaviour
 * LocalTimeScaleUser already does. You may find it easier to simply derive your classes from LocalTimeScaleUser instead.
 */

namespace MagicPigGames.MagicTime
{
    public interface IHaveLocalTime
    {
        /// <summary>
        /// The value of the local time scale specific to this LocalTimeScaleUser.
        /// </summary>
        float LocalTimeScaleValue { get; set; }

        /// <summary>
        /// Returns the LocalTimeScale that is specific to this LocalTimeScaleUser.
        /// </summary>
        public LocalTimeScale LocalTimeScale { get; }
        
        /// <summary>
        /// Subscribes to a LocalTimeScale.
        /// </summary>
        /// <param name="myTimeScale">The LocalTimeScale to subscribe to.</param>
        void SubscribeToLocalTimeScale(LocalTimeScale myTimeScale);

        /// <summary>
        /// Unsubscribes from a LocalTimeScale.
        /// </summary>
        /// <param name="timeScale">The LocalTimeScale to unsubscribe from.</param>
        void UnsubscribeFromLocalTimeScale(LocalTimeScale timeScale);

        /// <summary>
        /// The list of currently subscribed LocalTimeScales.
        /// </summary>
        List<LocalTimeScale> SubscribedTimeScales { get; }

        /// <summary>
        /// The combined TimeScale of all subscribed LocalTimeScales.
        /// </summary>
        float TimeScale { get;  }
        
        /// <summary>
        /// The combined Time since startup of all subscribed LocalTimeScales, scaled by the combined LocalTimeScales.
        /// </summary>
        float Time { get;  }
        
        /// <summary>
        /// The Time since startup, unscaled by Time.timeScale
        /// </summary>
        float UnscaledTime { get;  }
        
        /// <summary>
        /// DeltaTime adjusted by the combined LocalTimeScales.
        /// </summary>
        float DeltaTime { get; }

        /// <summary>
        /// UnscaledDeltaTime adjusted by the combined LocalTimeScales.
        /// </summary>
        float UnscaledDeltaTime { get; }

        /// <summary>
        /// FixedDeltaTime adjusted by the combined LocalTimeScales.
        /// </summary>
        float FixedDeltaTime { get; }
        
        /// <summary>
        /// Called when a subscribed LocalTimeScale is destroyed.
        /// </summary>
        /// <param name="timeScale">The destroyed LocalTimeScale.</param>
        void OnTimeScaleDestroyed(LocalTimeScale timeScale);

        /// <summary>
        /// Used to subscribe to the initial time scales, often called from Awake();
        /// </summary>
        IEnumerator SubscribeToInitialTimeScales();

        /// <summary>
        /// Called when the TimeScale is changed.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="localTimeScale"></param>
        void OnTimeScaleChanged(float value, LocalTimeScale localTimeScale);
        
        /// <summary>
        /// A reference to the GameObject that this LocalTimeScaleUser is attached to.
        /// </summary>
        GameObject GameObject { get; }
    }
}