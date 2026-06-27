#pragma warning disable 0414
#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Linq;

/// <summary>
/// Phase G2-01: Bloom + Tonemapping + Color Grading post-processing setup.
/// Configures URP Global Volume profile with Bloom, Tonemapping, ColorAdjustments,
/// LiftGammaGain, WhiteBalance, and Vignette overrides.
/// Coexists with existing overrides (SSAO / Shadow from Phase G1).
/// Menu: Tools/Phase G2/Apply Post-Processing
/// </summary>
public static class PhaseG2_PostProcessingSetup
{
    private const string VolumeProfilePath = "Assets/DefaultVolumeProfile.asset";

    // ================================================================
    //  Apply Post-Processing
    // ================================================================

    /// <summary>
    /// Applies Bloom, Tonemapping, Color Grading, and Vignette to the
    /// existing Global Volume profile. Adds missing overrides if needed.
    /// </summary>
    [MenuItem("Tools/Phase G2/Apply Post-Processing")]
    public static void ApplyPostProcessing()
    {
        // Use delayCall to ensure URP assets are fully loaded
        EditorApplication.delayCall += () =>
        {
            ConfigureVolumeProfile();
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG2] ✅ Post-processing settings applied.");
            EditorUtility.DisplayDialog("Phase G2-01", "Post-processing settings applied successfully.", "OK");
        };
    }

    [MenuItem("Tools/Phase G2/Apply Post-Processing", true)]
    private static bool ValidateApplyPostProcessing() => true;

    // ================================================================
    //  Reset Post-Processing
    // ================================================================

    /// <summary>
    /// Resets all post-processing overrides to their default inactive state.
    /// </summary>
    [MenuItem("Tools/Phase G2/Reset Post-Processing")]
    public static void ResetPostProcessing()
    {
        EditorApplication.delayCall += () =>
        {
            ResetVolumeProfile();
            AssetDatabase.SaveAssets();

            Debug.Log("[PhaseG2] ✅ Post-processing defaults reset.");
            EditorUtility.DisplayDialog("Phase G2-01", "Post-processing defaults reset.", "OK");
        };
    }

    [MenuItem("Tools/Phase G2/Reset Post-Processing", true)]
    private static bool ValidateResetPostProcessing() => true;

    // ================================================================
    //  Volume Profile Configuration
    // ================================================================

    /// <summary>
    /// Loads or creates the Volume profile and configures all overrides.
    /// Preserves any existing overrides (e.g. SSAO-related volume components
    /// are not touched).
    /// </summary>
    private static void ConfigureVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            Debug.Log($"[PhaseG2] Volume profile not found at '{VolumeProfilePath}'. Creating new profile.");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            Debug.Log($"[PhaseG2] Created new Volume profile at '{VolumeProfilePath}'.");
        }

        // --- Bloom ---
        ConfigureBloom(profile);

        // --- Tonemapping (ACES) ---
        ConfigureTonemapping(profile);

        // --- Color Adjustments ---
        ConfigureColorAdjustments(profile);

        // --- Lift / Gamma / Gain ---
        ConfigureLiftGammaGain(profile);

        // --- White Balance ---
        ConfigureWhiteBalance(profile);

        // --- Vignette ---
        ConfigureVignette(profile);

        EditorUtility.SetDirty(profile);
        Debug.Log("[PhaseG2] ✅ Volume profile configured with Bloom, Tonemapping, Color Grading, and Vignette.");
    }

    /// <summary>
    /// Removes all post-processing overrides added by this phase, restoring
    /// the profile to a clean baseline.
    /// </summary>
    private static void ResetVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[PhaseG2] Volume profile not found at '{VolumeProfilePath}'. Nothing to reset.");
            return;
        }

        bool changed = false;

        if (profile.Has<Bloom>())
        {
            profile.Remove<Bloom>();
            changed = true;
        }

        if (profile.Has<Tonemapping>())
        {
            profile.Remove<Tonemapping>();
            changed = true;
        }

        if (profile.Has<ColorAdjustments>())
        {
            profile.Remove<ColorAdjustments>();
            changed = true;
        }

        if (profile.Has<LiftGammaGain>())
        {
            profile.Remove<LiftGammaGain>();
            changed = true;
        }

        if (profile.Has<WhiteBalance>())
        {
            profile.Remove<WhiteBalance>();
            changed = true;
        }

        if (profile.Has<Vignette>())
        {
            profile.Remove<Vignette>();
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(profile);
            Debug.Log("[PhaseG2] ✅ Post-processing overrides removed from volume profile.");
        }
        else
        {
            Debug.Log("[PhaseG2] No post-processing overrides found to remove.");
        }
    }

    // ================================================================
    //  Individual Override Configuration
    // ================================================================

    /// <summary>
    /// Configures Bloom: Intensity=1.0, Threshold=0.9, Scatter=0.7, Tint=white.
    /// </summary>
    private static void ConfigureBloom(VolumeProfile profile)
    {
        Bloom bloom;
        if (!profile.TryGet(out bloom))
        {
            bloom = profile.Add<Bloom>(overrides: true);
            Debug.Log("[PhaseG2] Added Bloom override.");
        }

        bloom.intensity.overrideState = true;
        bloom.intensity.value = 1.0f;

        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.9f;

        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.7f;

        bloom.tint.overrideState = true;
        bloom.tint.value = Color.white;

        bloom.highQualityFiltering.overrideState = true;
        bloom.highQualityFiltering.value = false;

        bloom.downscale.overrideState = true;
        bloom.downscale.value = BloomDownscaleMode.Half;

        bloom.maxIterations.overrideState = true;
        bloom.maxIterations.value = 6;

        // skipIterations was removed in Unity 6 — maxIterations handles iteration count

        bloom.clamp.overrideState = true;
        bloom.clamp.value = 65472f;

        Debug.Log("[PhaseG2] ✅ Bloom configured: Intensity=1.0, Threshold=0.9, Scatter=0.7.");
    }

    /// <summary>
    /// Configures Tonemapping: Mode=ACES.
    /// </summary>
    private static void ConfigureTonemapping(VolumeProfile profile)
    {
        Tonemapping tonemapping;
        if (!profile.TryGet(out tonemapping))
        {
            tonemapping = profile.Add<Tonemapping>(overrides: true);
            Debug.Log("[PhaseG2] Added Tonemapping override.");
        }

        tonemapping.mode.overrideState = true;
        // TonemappingMode: Neutral=0, ACES=1, Custom=2, External=3
        tonemapping.mode.value = TonemappingMode.ACES;

        Debug.Log("[PhaseG2] ✅ Tonemapping configured: Mode=ACES.");
    }

    /// <summary>
    /// Configures Color Adjustments: PostExposure=0, Contrast=10, Saturation=5.
    /// </summary>
    private static void ConfigureColorAdjustments(VolumeProfile profile)
    {
        ColorAdjustments colorAdj;
        if (!profile.TryGet(out colorAdj))
        {
            colorAdj = profile.Add<ColorAdjustments>(overrides: true);
            Debug.Log("[PhaseG2] Added ColorAdjustments override.");
        }

        colorAdj.postExposure.overrideState = true;
        colorAdj.postExposure.value = 0f;

        colorAdj.contrast.overrideState = true;
        colorAdj.contrast.value = 10f;

        colorAdj.saturation.overrideState = true;
        colorAdj.saturation.value = 5f;

        colorAdj.hueShift.overrideState = true;
        colorAdj.hueShift.value = 0f;

        colorAdj.colorFilter.overrideState = true;
        colorAdj.colorFilter.value = Color.white;

        Debug.Log("[PhaseG2] ✅ ColorAdjustments configured: PostExposure=0, Contrast=10, Saturation=5.");
    }

    /// <summary>
    /// Configures Lift / Gamma / Gain with identity values (all 1,1,1,1).
    /// </summary>
    private static void ConfigureLiftGammaGain(VolumeProfile profile)
    {
        LiftGammaGain liftGammaGain;
        if (!profile.TryGet(out liftGammaGain))
        {
            liftGammaGain = profile.Add<LiftGammaGain>(overrides: true);
            Debug.Log("[PhaseG2] Added LiftGammaGain override.");
        }

        liftGammaGain.lift.overrideState = true;
        liftGammaGain.lift.value = new Vector4(1f, 1f, 1f, 0f);

        liftGammaGain.gamma.overrideState = true;
        liftGammaGain.gamma.value = new Vector4(1f, 1f, 1f, 0f);

        liftGammaGain.gain.overrideState = true;
        liftGammaGain.gain.value = new Vector4(1f, 1f, 1f, 0f);

        Debug.Log("[PhaseG2] ✅ LiftGammaGain configured (identity values).");
    }

    /// <summary>
    /// Configures White Balance: Temperature=0, Tint=0.
    /// </summary>
    private static void ConfigureWhiteBalance(VolumeProfile profile)
    {
        WhiteBalance whiteBalance;
        if (!profile.TryGet(out whiteBalance))
        {
            whiteBalance = profile.Add<WhiteBalance>(overrides: true);
            Debug.Log("[PhaseG2] Added WhiteBalance override.");
        }

        whiteBalance.temperature.overrideState = true;
        whiteBalance.temperature.value = 0f;

        whiteBalance.tint.overrideState = true;
        whiteBalance.tint.value = 0f;

        Debug.Log("[PhaseG2] ✅ WhiteBalance configured: Temperature=0, Tint=0.");
    }

    /// <summary>
    /// Configures Vignette: Intensity=0.3, Smoothness=0.5, Color=black.
    /// </summary>
    private static void ConfigureVignette(VolumeProfile profile)
    {
        Vignette vignette;
        if (!profile.TryGet(out vignette))
        {
            vignette = profile.Add<Vignette>(overrides: true);
            Debug.Log("[PhaseG2] Added Vignette override.");
        }

        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.3f;

        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.5f;

        vignette.color.overrideState = true;
        vignette.color.value = Color.black;

        vignette.center.overrideState = true;
        vignette.center.value = new Vector2(0.5f, 0.5f);

        vignette.rounded.overrideState = true;
        vignette.rounded.value = false;

        Debug.Log("[PhaseG2] ✅ Vignette configured: Intensity=0.3, Smoothness=0.5, Color=black.");
    }
}

#endif // UNITY_EDITOR