using UnityEngine;
using TimeWarp.Core;

namespace TimeWarp.Modules.Cameras
{
    /// <summary>
    /// DEPRECATED ALIAS - kept only for backwards compatibility.
    ///
    /// This component used to be a near-verbatim copy of TimeCinemachineAdapter.
    /// Shipping two near-identical components meant a user could easily pick the one
    /// that still called CinemachineBrain.ManualUpdate(), which double-updates the
    /// camera on the default Brain update method.
    ///
    /// The behaviour now lives in TimeCinemachineAdapter and this class simply inherits
    /// it, so scenes and prefabs that already reference "TimeCinemachineBrain" keep
    /// working with no missing-script errors. It is intentionally hidden from the
    /// Add Component menu so new setups only ever find the canonical component.
    ///
    /// For new work use TimeCinemachineAdapter.
    /// </summary>
    [RequireComponent(typeof(TimeWarpIdentity))]
    // No [AddComponentMenu] on purpose: hidden from the Add Component menu.
    public class TimeCinemachineBrain : TimeCinemachineAdapter
    {
        // No members. All behaviour is inherited from TimeCinemachineAdapter.
    }
}
