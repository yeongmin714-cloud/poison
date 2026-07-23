using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using ProjectName.Systems.Animation.Procedural;

namespace ProjectName.Systems.Animation.Neural
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  AnimationPolicy
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// High-level animation policy categories for neural policy selection.
    /// Priority order (highest to lowest): Combat > React > Fly/Swim > Mount > Climb > Locomotion > Interact
    /// </summary>
    public enum AnimationPolicy
    {
        /// <summary>Highest priority: Combat attacks, blocks, parries, dodges.</summary>
        Combat = 0,

        /// <summary>High priority: Reaction to impacts, stagger, dodge rolls, hit reactions.</summary>
        React = 1,

        /// <summary>High priority: Flying locomotion and aerial maneuvers.</summary>
        Fly = 2,

        /// <summary>High priority: Swimming and underwater locomotion.</summary>
        Swim = 3,

        /// <summary>Medium priority: Mounted movement (riding mounts, vehicles).</summary>
        Mount = 4,

        /// <summary>Medium priority: Climbing, vaulting, ladder movement.</summary>
        Climb = 5,

        /// <summary>Base priority: Ground locomotion (walk, run, strafe, turn).</summary>
        Locomotion = 6,

        /// <summary>Lowest priority: Interactions (gather, open, talk, emote).</summary>
        Interact = 7,

        /// <summary>Fallback: No active policy, fallback to default locomotion.</summary>
        None = 99
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  AnimationState
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// High-level animation state machine states.
    /// Maps to ProceduralAnimStateMachine.State and NeuralAnimationController.PolicyType.
    /// </summary>
    public enum AnimationState
    {
        /// <summary>Ground locomotion (idle, walk, run, strafe, turn).</summary>
        Locomotion,

        /// <summary>Jump takeoff and initial ascent.</summary>
        Jump,

        /// <summary>Airborne state (falling, gliding, mid-air control).</summary>
        Airborne,

        /// <summary>Landing impact and recovery.</summary>
        Landing,

        /// <summary>Combat attack animations.</summary>
        Attack,

        /// <summary>Blocking, parrying, defensive reactions.</summary>
        Defend,

        /// <summary>Dodge roll, sidestep, evasive maneuvers.</summary>
        Dodge,

        /// <summary>Gathering, interacting with objects.</summary>
        Interact,

        /// <summary>Climbing, vaulting, ladder movement.</summary>
        Climb,

        /// <summary>Mounted movement (riding).</summary>
        Mount,

        /// <summary>Flying locomotion.</summary>
        Fly,

        /// <summary>Swimming and underwater movement.</summary>
        Swim,

        /// <summary>Hit reaction, stagger, knockback.</summary>
        Stagger,

        /// <summary>Death state.</summary>
        Death
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  SelectorAvatarType
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Avatar classification for policy routing. Separate from AnimationPolicy.AvatarType
    /// to distinguish Biped (two-legged) from Humanoid (which includes MultiLeg).
    /// </summary>
    public enum SelectorAvatarType
    {
        /// <summary>Two-legged humanoid (player, NPCs, soldiers).</summary>
        Biped = 0,

        /// <summary>Four-legged creature (wolf, horse, boar).</summary>
        Quadruped = 1,

        /// <summary>Flying creature (bird, dragon, bat).</summary>
        Flying = 2,

        /// <summary>Swimming creature (fish, shark, aquatic).</summary>
        Swimming = 3,

        /// <summary>Large monster with custom skeleton (boss, dragon, giant).</summary>
        LargeMonster = 4
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  CombatContext
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Combat context for policy selection priority evaluation.
    /// </summary>
    [Serializable]
    public struct CombatContext
    {
        /// <summary>Currently performing an attack action.</summary>
        public bool isAttacking;

        /// <summary>Currently blocking, parrying, or defending.</summary>
        public bool isDefending;

        /// <summary>Currently dodging or evading.</summary>
        public bool isDodging;

        /// <summary>Currently staggering from hit reaction.</summary>
        public bool isStaggered;

        /// <summary>Weapon type for combat policy variant selection.</summary>
        public WeaponType weaponType;

        /// <summary>Distance to current combat target in meters.</summary>
        public float targetDistance;

        /// <summary>Time since last attack action.</summary>
        public float timeSinceLastAttack;

        /// <summary>Time since last hit taken.</summary>
        public float timeSinceLastHit;

        /// <summary>Current combo count for combo policies.</summary>
        public int comboCount;

        /// <summary>Whether a target is currently locked/acquired.</summary>
        public bool hasTarget;

        /// <summary>Threat level (0-1) for reactive policy intensity.</summary>
        [Range(0f, 1f)]
        public float threatLevel;

        public static CombatContext Default => new CombatContext
        {
            isAttacking = false,
            isDefending = false,
            isDodging = false,
            isStaggered = false,
            weaponType = WeaponType.Unarmed,
            targetDistance = 100f,
            timeSinceLastAttack = 999f,
            timeSinceLastHit = 999f,
            comboCount = 0,
            hasTarget = false,
            threatLevel = 0f
        };
    }

    /// <summary>
    /// Weapon classification for combat policy variant selection.
    /// </summary>
    public enum WeaponType
    {
        Unarmed,
        Sword,
        Axe,
        Mace,
        Dagger,
        Spear,
        Bow,
        Crossbow,
        Staff,
        Wand,
        Gun,
        Shield,
        TwoHanded,
        DualWield
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  TransitionConfig
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configuration for smooth policy transitions with latent space blending.
    /// </summary>
    [Serializable]
    public struct TransitionConfig
    {
        /// <summary>Duration of policy blend transition in seconds (default 0.3s).</summary>
        [Range(0.05f, 2f)]
        public float blendDuration;

        /// <summary>Enable latent space interpolation between policy style embeddings.</summary>
        public bool useLatentBlend;

        /// <summary>Fallback policy when target policy is unavailable.</summary>
        public AnimationPolicy fallbackPolicy;

        /// <summary>Blend curve for policy weight interpolation.</summary>
        public AnimationCurve blendCurve;

        /// <summary>Minimum time before another transition can be requested (cooldown).</summary>
        [Range(0f, 1f)]
        public float transitionCooldown;

        /// <summary>Whether to cross-fade latent embeddings during transition.</summary>
        public bool crossFadeLatent;

        /// <summary>Latent blend weight curve (x = normalized time, y = latent weight).</summary>
        public AnimationCurve latentBlendCurve;

        public static TransitionConfig Default => new TransitionConfig
        {
            blendDuration = 0.3f,
            useLatentBlend = true,
            fallbackPolicy = AnimationPolicy.Locomotion,
            blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            transitionCooldown = 0.1f,
            crossFadeLatent = true,
            latentBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
        };

        public static TransitionConfig Fast => new TransitionConfig
        {
            blendDuration = 0.15f,
            useLatentBlend = true,
            fallbackPolicy = AnimationPolicy.Locomotion,
            blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            transitionCooldown = 0.05f,
            crossFadeLatent = true,
            latentBlendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
        };

        public static TransitionConfig Slow => new TransitionConfig
        {
            blendDuration = 0.6f,
            useLatentBlend = true,
            fallbackPolicy = AnimationPolicy.Locomotion,
            blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            transitionCooldown = 0.2f,
            crossFadeLatent = true,
            latentBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
        };

        public static TransitionConfig Instant => new TransitionConfig
        {
            blendDuration = 0f,
            useLatentBlend = false,
            fallbackPolicy = AnimationPolicy.Locomotion,
            blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
            transitionCooldown = 0f,
            crossFadeLatent = false,
            latentBlendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
        };
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  PolicySelectionContext
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Complete context for policy selection evaluation.
    /// </summary>
    public readonly struct PolicySelectionContext
    {
        public readonly AnimationState currentState;
        public readonly SelectorAvatarType avatarType;
        public readonly CombatContext combatContext;
        public readonly bool isGrounded;
        public readonly bool isInWater;
        public readonly bool isFlying;
        public readonly bool isMounted;
        public readonly bool isClimbing;
        public readonly float moveSpeed;
        public readonly float verticalSpeed;
        public readonly Vector3 moveDirection;
        public readonly float timeInState;

        public PolicySelectionContext(
            AnimationState currentState,
            SelectorAvatarType avatarType,
            CombatContext combatContext,
            bool isGrounded,
            bool isInWater,
            bool isFlying,
            bool isMounted,
            bool isClimbing,
            float moveSpeed,
            float verticalSpeed,
            Vector3 moveDirection,
            float timeInState)
        {
            this.currentState = currentState;
            this.avatarType = avatarType;
            this.combatContext = combatContext;
            this.isGrounded = isGrounded;
            this.isInWater = isInWater;
            this.isFlying = isFlying;
            this.isMounted = isMounted;
            this.isClimbing = isClimbing;
            this.moveSpeed = moveSpeed;
            this.verticalSpeed = verticalSpeed;
            this.moveDirection = moveDirection;
            this.timeInState = timeInState;
        }

        public static PolicySelectionContext Default => new PolicySelectionContext(
            AnimationState.Locomotion,
            SelectorAvatarType.Biped,
            CombatContext.Default,
            true, false, false, false, false,
            0f, 0f, Vector3.forward, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  PolicySelectionResult
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Result of policy selection containing selected policy and transition info.
    /// </summary>
    public readonly struct PolicySelectionResult
    {
        public readonly AnimationPolicy selectedPolicy;
        public readonly AnimationPolicy previousPolicy;
        public readonly bool policyChanged;
        public readonly TransitionConfig transitionConfig;
        public readonly float[] latentEmbeddingFrom;
        public readonly float[] latentEmbeddingTo;
        public readonly float transitionPriority;
        public readonly string reason;

        public PolicySelectionResult(
            AnimationPolicy selectedPolicy,
            AnimationPolicy previousPolicy,
            bool policyChanged,
            TransitionConfig transitionConfig,
            float[] latentEmbeddingFrom,
            float[] latentEmbeddingTo,
            float transitionPriority,
            string reason)
        {
            this.selectedPolicy = selectedPolicy;
            this.previousPolicy = previousPolicy;
            this.policyChanged = policyChanged;
            this.transitionConfig = transitionConfig;
            this.latentEmbeddingFrom = latentEmbeddingFrom;
            this.latentEmbeddingTo = latentEmbeddingTo;
            this.transitionPriority = transitionPriority;
            this.reason = reason;
        }

        public static PolicySelectionResult NoChange(AnimationPolicy currentPolicy) => new PolicySelectionResult(
            currentPolicy, currentPolicy, false, TransitionConfig.Default, null, null, 0f, "No change");

        public static PolicySelectionResult Fallback(TransitionConfig config, string reason) => new PolicySelectionResult(
            config.fallbackPolicy, AnimationPolicy.None, true, config, null, null, 0f, reason);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  LatentEmbedding
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Latent style embedding for policy blending in latent space.
    /// </summary>
    [Serializable]
    public struct LatentEmbedding
    {
        /// <summary>Embedding vector (typically 8-32 dimensions).</summary>
        public float[] values;

        /// <summary>Policy this embedding belongs to.</summary>
        public AnimationPolicy policy;

        /// <summary>Avatar type this embedding is for.</summary>
        public SelectorAvatarType avatarType;

        /// <summary>Timestamp when embedding was captured.</summary>
        public float timestamp;

        public static LatentEmbedding Zero(AnimationPolicy policy, SelectorAvatarType avatarType, int embeddingSize = 8) => new LatentEmbedding
        {
            values = new float[embeddingSize],
            policy = policy,
            avatarType = avatarType,
            timestamp = Time.time
        };

        /// <summary>
        /// Linear interpolation between two latent embeddings.
        /// </summary>
        public static float[] Lerp(float[] from, float[] to, float t)
        {
            if (from == null || to == null) return from ?? to ?? Array.Empty<float>();
            if (from.Length != to.Length)
            {
                Debug.LogWarning($"[LatentEmbedding] Dimension mismatch: from={from.Length}, to={to.Length}");
                return from;
            }

            float[] result = new float[from.Length];
            for (int i = 0; i < from.Length; i++)
            {
                result[i] = math.lerp(from[i], to[i], t);
            }
            return result;
        }

        /// <summary>
        /// Spherical linear interpolation for normalized embeddings.
        /// </summary>
        public static float[] Slerp(float[] from, float[] to, float t)
        {
            if (from == null || to == null) return from ?? to ?? Array.Empty<float>();
            if (from.Length != to.Length) return Lerp(from, to, t);

            float dot = 0f;
            for (int i = 0; i < from.Length; i++) dot += from[i] * to[i];

            // Clamp dot product
            dot = math.clamp(dot, -1f, 1f);

            float theta = math.acos(dot) * t;
            float sinTheta = math.sin(theta);
            float sinTheta0 = math.sin(math.acos(dot));

            if (sinTheta0 < 1e-6f) return Lerp(from, to, t);

            float[] result = new float[from.Length];
            float scaleFrom = math.sin(math.acos(dot) - theta) / sinTheta0;
            float scaleTo = sinTheta / sinTheta0;

            for (int i = 0; i < from.Length; i++)
            {
                result[i] = from[i] * scaleFrom + to[i] * scaleTo;
            }
            return result;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  PolicySelector (Static Class)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Static policy selection system with priority-based policy switching and latent space blending.
    /// Integrates with NeuralAnimationController.SwitchPolicy() for smooth transitions.
    /// </summary>
    public static class PolicySelector
    {
        // ─────────────────────────────────────────────────────────────────────────────
        //  Static State
        // ─────────────────────────────────────────────────────────────────────────────

        private static AnimationPolicy s_CurrentPolicy = AnimationPolicy.Locomotion;
        private static AnimationPolicy s_TargetPolicy = AnimationPolicy.Locomotion;
        private static AnimationPolicy s_PreviousPolicy = AnimationPolicy.Locomotion;
        private static float s_TransitionTimer = 0f;
        private static float s_TransitionDuration = 0.3f;
        private static bool s_IsTransitioning = false;
        private static TransitionConfig s_CurrentTransitionConfig = TransitionConfig.Default;
        private static float s_LastTransitionTime = -999f;
        private static float[] s_CurrentLatentEmbedding = null;
        private static float[] s_TargetLatentEmbedding = null;
        private static AnimationPolicy s_LatentFromPolicy = AnimationPolicy.Locomotion;
        private static AnimationPolicy s_LatentToPolicy = AnimationPolicy.Locomotion;
        private static Dictionary<AnimationPolicy, float[]> s_PolicyLatentEmbeddings = new Dictionary<AnimationPolicy, float[]>();
        private static Dictionary<SelectorAvatarType, Dictionary<AnimationPolicy, float[]>> s_AvatarPolicyEmbeddings = new Dictionary<SelectorAvatarType, Dictionary<AnimationPolicy, float[]>>();

        // ─────────────────────────────────────────────────────────────────────────────
        //  Public Properties
        // ─────────────────────────────────────────────────────────────────────────────

        public static AnimationPolicy CurrentPolicy => s_CurrentPolicy;
        public static AnimationPolicy TargetPolicy => s_TargetPolicy;
        public static AnimationPolicy PreviousPolicy => s_PreviousPolicy;
        public static bool IsTransitioning => s_IsTransitioning;
        public static float TransitionProgress => s_TransitionDuration > 0f ? s_TransitionTimer / s_TransitionDuration : 1f;
        public static float TransitionTimeRemaining => math.max(0f, s_TransitionDuration - s_TransitionTimer);
        public static TransitionConfig CurrentTransitionConfig => s_CurrentTransitionConfig;
        public static float[] CurrentLatentEmbedding => s_CurrentLatentEmbedding;
        public static float[] TargetLatentEmbedding => s_TargetLatentEmbedding;

        // ─────────────────────────────────────────────────────────────────────────────
        //  Public API: Policy Selection
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Select the appropriate animation policy based on current state and context.
        /// </summary>
        /// <param name="context">Full policy selection context.</param>
        /// <param name="transitionConfig">Transition configuration (optional, uses default if null).</param>
        /// <returns>Policy selection result with transition info.</returns>
        public static PolicySelectionResult SelectPolicy(PolicySelectionContext context, TransitionConfig? transitionConfig = null)
        {
            var config = transitionConfig ?? TransitionConfig.Default;

            // Check transition cooldown
            if (Time.time - s_LastTransitionTime < config.transitionCooldown && s_IsTransitioning)
            {
                return PolicySelectionResult.NoChange(s_CurrentPolicy);
            }

            // Evaluate policy priority based on context
            AnimationPolicy selectedPolicy = EvaluatePolicyPriority(context);

            // Check if policy actually changed
            if (selectedPolicy == s_CurrentPolicy && !s_IsTransitioning)
            {
                return PolicySelectionResult.NoChange(s_CurrentPolicy);
            }

            // Validate policy is available for avatar type
            if (!IsPolicyAvailableForAvatar(selectedPolicy, context.avatarType))
            {
                selectedPolicy = GetFallbackPolicy(context.avatarType, config.fallbackPolicy);
            }

            // Prepare transition
            var result = PrepareTransition(selectedPolicy, s_CurrentPolicy, config, context, $"Priority selection: {GetSelectionReason(context, selectedPolicy)}");

            // Apply transition
            ApplyTransition(result);

            return result;
        }

        /// <summary>
        /// Simplified policy selection with minimal context (backwards compatibility).
        /// </summary>
        public static PolicySelectionResult SelectPolicy(
            AnimationState currentState,
            SelectorAvatarType avatarType,
            CombatContext combatCtx,
            TransitionConfig? transitionConfig = null)
        {
            var context = new PolicySelectionContext(
                currentState,
                avatarType,
                combatCtx,
                currentState != AnimationState.Airborne && currentState != AnimationState.Jump,
                currentState == AnimationState.Swim,
                currentState == AnimationState.Fly,
                currentState == AnimationState.Mount,
                currentState == AnimationState.Climb,
                0f, 0f, Vector3.forward, 0f);
            return SelectPolicy(context, transitionConfig);
        }

        /// <summary>
        /// Force a specific policy transition with custom config.
        /// </summary>
        public static PolicySelectionResult ForcePolicy(
            AnimationPolicy policy,
            TransitionConfig? transitionConfig = null,
            string reason = "Forced")
        {
            var config = transitionConfig ?? TransitionConfig.Default;
            var result = PrepareTransition(policy, s_CurrentPolicy, config, PolicySelectionContext.Default, reason);
            ApplyTransition(result);
            return result;
        }

        /// <summary>
        /// Request a policy transition (queued, respects cooldown).
        /// </summary>
        public static bool RequestPolicy(AnimationPolicy policy, TransitionConfig? config = null)
        {
            if (Time.time - s_LastTransitionTime < (config ?? TransitionConfig.Default).transitionCooldown)
                return false;

            s_TargetPolicy = policy;
            s_CurrentTransitionConfig = config ?? TransitionConfig.Default;
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Public API: Latent Embedding Management
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Register a latent embedding for a policy/avatar combination.
        /// Call this when a policy is loaded to capture its style embedding.
        /// </summary>
        public static void RegisterLatentEmbedding(AnimationPolicy policy, SelectorAvatarType avatarType, float[] embedding)
        {
            if (embedding == null || embedding.Length == 0) return;

            if (!s_AvatarPolicyEmbeddings.ContainsKey(avatarType))
                s_AvatarPolicyEmbeddings[avatarType] = new Dictionary<AnimationPolicy, float[]>();

            s_AvatarPolicyEmbeddings[avatarType][policy] = embedding;
            s_PolicyLatentEmbeddings[policy] = embedding;
        }

        /// <summary>
        /// Get the latent embedding for a policy/avatar combination.
        /// </summary>
        public static float[] GetLatentEmbedding(AnimationPolicy policy, SelectorAvatarType avatarType)
        {
            if (s_AvatarPolicyEmbeddings.TryGetValue(avatarType, out var policyDict) &&
                policyDict.TryGetValue(policy, out var embedding))
            {
                return embedding;
            }

            if (s_PolicyLatentEmbeddings.TryGetValue(policy, out var fallbackEmbedding))
                return fallbackEmbedding;

            // Return zero embedding as fallback
            return new float[8]; // Default embedding size
        }

        /// <summary>
        /// Capture current latent embedding from active policy (call during inference).
        /// </summary>
        public static void CaptureCurrentLatentEmbedding(float[] embedding)
        {
            s_CurrentLatentEmbedding = embedding?.Clone() as float[];
        }

        /// <summary>
        /// Get interpolated latent embedding during transition.
        /// </summary>
        public static float[] GetBlendedLatentEmbedding(float t)
        {
            if (!s_IsTransitioning || s_CurrentLatentEmbedding == null || s_TargetLatentEmbedding == null)
                return s_CurrentLatentEmbedding;

            if (s_CurrentTransitionConfig.useLatentBlend)
            {
                if (s_CurrentTransitionConfig.crossFadeLatent)
                {
                    return LatentEmbedding.Slerp(s_CurrentLatentEmbedding, s_TargetLatentEmbedding, t);
                }
                return LatentEmbedding.Lerp(s_CurrentLatentEmbedding, s_TargetLatentEmbedding, t);
            }

            return s_CurrentLatentEmbedding;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Public API: Transition Control
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Update transition state (call from Update or FixedUpdate).
        /// </summary>
        public static void UpdateTransition(float deltaTime)
        {
            if (!s_IsTransitioning) return;

            s_TransitionTimer += deltaTime;
            float progress = math.clamp(s_TransitionTimer / s_TransitionDuration, 0f, 1f);

            // Apply blend curve
            float curvedProgress = s_CurrentTransitionConfig.blendCurve.Evaluate(progress);

            // Update blended latent embedding
            if (s_CurrentTransitionConfig.useLatentBlend && s_CurrentLatentEmbedding != null && s_TargetLatentEmbedding != null)
            {
                if (s_CurrentTransitionConfig.crossFadeLatent)
                {
                    s_CurrentLatentEmbedding = LatentEmbedding.Slerp(s_CurrentLatentEmbedding, s_TargetLatentEmbedding, curvedProgress);
                }
                else
                {
                    s_CurrentLatentEmbedding = LatentEmbedding.Lerp(s_CurrentLatentEmbedding, s_TargetLatentEmbedding, curvedProgress);
                }
            }

            // Check transition complete
            if (progress >= 1f)
            {
                CompleteTransition();
            }
        }

        /// <summary>
        /// Immediately complete the current transition.
        /// </summary>
        public static void CompleteTransitionImmediate()
        {
            if (s_IsTransitioning)
            {
                s_CurrentPolicy = s_TargetPolicy;
                s_CurrentLatentEmbedding = s_TargetLatentEmbedding?.Clone() as float[];
                s_IsTransitioning = false;
                s_TransitionTimer = 0f;
            }
        }

        /// <summary>
        /// Cancel current transition and revert to previous policy.
        /// </summary>
        public static void CancelTransition()
        {
            if (s_IsTransitioning)
            {
                s_TargetPolicy = s_CurrentPolicy;
                s_TargetLatentEmbedding = s_CurrentLatentEmbedding?.Clone() as float[];
                s_IsTransitioning = false;
                s_TransitionTimer = 0f;
            }
        }

        /// <summary>
        /// Reset selector to default state.
        /// </summary>
        public static void Reset()
        {
            s_CurrentPolicy = AnimationPolicy.Locomotion;
            s_TargetPolicy = AnimationPolicy.Locomotion;
            s_PreviousPolicy = AnimationPolicy.Locomotion;
            s_IsTransitioning = false;
            s_TransitionTimer = 0f;
            s_TransitionDuration = 0.3f;
            s_LastTransitionTime = -999f;
            s_CurrentLatentEmbedding = null;
            s_TargetLatentEmbedding = null;
            s_CurrentTransitionConfig = TransitionConfig.Default;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Internal: Policy Priority Evaluation
        // ─────────────────────────────────────────────────────────────────────────────

        private static AnimationPolicy EvaluatePolicyPriority(PolicySelectionContext ctx)
        {
            // Priority 0: Combat (highest)
            if (ctx.combatContext.isAttacking || ctx.combatContext.isDefending || ctx.combatContext.isDodging)
            {
                if (ctx.combatContext.isDodging) return AnimationPolicy.React; // Dodge = React
                if (ctx.combatContext.isDefending) return AnimationPolicy.Combat; // Block/Parry = Combat
                return AnimationPolicy.Combat; // Attack = Combat
            }

            // Priority 1: React (hit reaction, stagger)
            if (ctx.combatContext.isStaggered || ctx.combatContext.timeSinceLastHit < 0.5f)
            {
                return AnimationPolicy.React;
            }

            // Priority 2: Fly/Swim (movement mode overrides)
            if (ctx.isFlying || ctx.currentState == AnimationState.Fly)
                return AnimationPolicy.Fly;

            if (ctx.isInWater || ctx.currentState == AnimationState.Swim)
                return AnimationPolicy.Swim;

            // Priority 3: Mount
            if (ctx.isMounted || ctx.currentState == AnimationState.Mount)
                return AnimationPolicy.Mount;

            // Priority 4: Climb
            if (ctx.isClimbing || ctx.currentState == AnimationState.Climb)
                return AnimationPolicy.Climb;

            // Priority 5: Locomotion (base ground movement)
            if (ctx.isGrounded && (ctx.currentState == AnimationState.Locomotion ||
                ctx.currentState == AnimationState.Jump ||
                ctx.currentState == AnimationState.Airborne ||
                ctx.currentState == AnimationState.Landing))
            {
                // Check for combat threat
                if (ctx.combatContext.hasTarget && ctx.combatContext.threatLevel > 0.5f)
                    return AnimationPolicy.Combat;

                return AnimationPolicy.Locomotion;
            }

            // Priority 6: Interact
            if (ctx.currentState == AnimationState.Interact)
                return AnimationPolicy.Interact;

            // Priority 7: React (high threat but not active combat)
            if (ctx.combatContext.hasTarget && ctx.combatContext.threatLevel > 0.3f)
                return AnimationPolicy.React;

            // Default fallback
            return AnimationPolicy.Locomotion;
        }

        private static string GetSelectionReason(PolicySelectionContext ctx, AnimationPolicy policy)
        {
            switch (policy)
            {
                case AnimationPolicy.Combat:
                    if (ctx.combatContext.isAttacking) return "Attacking";
                    if (ctx.combatContext.isDefending) return "Defending";
                    if (ctx.combatContext.isDodging) return "Dodging";
                    return "Combat Threat";
                case AnimationPolicy.React:
                    if (ctx.combatContext.isStaggered) return "Staggered";
                    if (ctx.combatContext.timeSinceLastHit < 0.5f) return "Recent Hit";
                    return "Threat Reaction";
                case AnimationPolicy.Fly: return "Flying";
                case AnimationPolicy.Swim: return "Swimming";
                case AnimationPolicy.Mount: return "Mounted";
                case AnimationPolicy.Climb: return "Climbing";
                case AnimationPolicy.Locomotion: return "Locomotion";
                case AnimationPolicy.Interact: return "Interacting";
                default: return "Default";
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Internal: Transition Management
        // ─────────────────────────────────────────────────────────────────────────────

        private static PolicySelectionResult PrepareTransition(
            AnimationPolicy newPolicy,
            AnimationPolicy oldPolicy,
            TransitionConfig config,
            PolicySelectionContext context,
            string reason)
        {
            // Get latent embeddings
            float[] fromEmbedding = GetLatentEmbedding(oldPolicy, context.avatarType);
            float[] toEmbedding = GetLatentEmbedding(newPolicy, context.avatarType);

            // Capture current embedding if available
            if (s_CurrentLatentEmbedding != null)
                fromEmbedding = s_CurrentLatentEmbedding;

            float priority = GetPolicyPriority(newPolicy);

            return new PolicySelectionResult(
                newPolicy,
                oldPolicy,
                newPolicy != oldPolicy,
                config,
                fromEmbedding,
                toEmbedding,
                priority,
                reason);
        }

        private static void ApplyTransition(PolicySelectionResult result)
        {
            if (!result.policyChanged) return;

            s_PreviousPolicy = s_CurrentPolicy;
            s_CurrentPolicy = result.selectedPolicy;
            s_TargetPolicy = result.selectedPolicy;
            s_TransitionDuration = result.transitionConfig.blendDuration;
            s_TransitionTimer = 0f;
            s_IsTransitioning = s_TransitionDuration > 0f;
            s_CurrentTransitionConfig = result.transitionConfig;
            s_LastTransitionTime = Time.time;

            // Set up latent embeddings for blending
            s_LatentFromPolicy = result.previousPolicy;
            s_LatentToPolicy = result.selectedPolicy;
            s_CurrentLatentEmbedding = result.latentEmbeddingFrom?.Clone() as float[];
            s_TargetLatentEmbedding = result.latentEmbeddingTo?.Clone() as float[];

            // Notify NeuralAnimationController
            NeuralAnimationController.RequestPolicySwitch(PolicyTypeFromAnimationPolicy(result.selectedPolicy), result.transitionConfig.blendDuration);
        }

        private static void CompleteTransition()
        {
            s_CurrentPolicy = s_TargetPolicy;
            s_CurrentLatentEmbedding = s_TargetLatentEmbedding?.Clone() as float[];
            s_IsTransitioning = false;
            s_TransitionTimer = 0f;

            // Update cached embeddings
            if (s_TargetLatentEmbedding != null)
            {
                s_PolicyLatentEmbeddings[s_CurrentPolicy] = s_TargetLatentEmbedding.Clone() as float[];
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Internal: Policy Validation & Fallback
        // ─────────────────────────────────────────────────────────────────────────────

        private static bool IsPolicyAvailableForAvatar(AnimationPolicy policy, SelectorAvatarType avatarType)
        {
            // Define policy-avatar compatibility matrix
            switch (avatarType)
            {
                case SelectorAvatarType.Biped:
                    return policy != AnimationPolicy.Fly && policy != AnimationPolicy.Swim;

                case SelectorAvatarType.Quadruped:
                    return policy != AnimationPolicy.Fly && policy != AnimationPolicy.Swim &&
                           policy != AnimationPolicy.Mount; // Quadrupeds don't mount typically

                case SelectorAvatarType.Flying:
                    return policy == AnimationPolicy.Fly || policy == AnimationPolicy.Locomotion ||
                           policy == AnimationPolicy.Combat || policy == AnimationPolicy.React;

                case SelectorAvatarType.Swimming:
                    return policy == AnimationPolicy.Swim || policy == AnimationPolicy.Locomotion ||
                           policy == AnimationPolicy.Combat || policy == AnimationPolicy.React;

                case SelectorAvatarType.LargeMonster:
                    return policy != AnimationPolicy.Mount && policy != AnimationPolicy.Climb &&
                           policy != AnimationPolicy.Fly && policy != AnimationPolicy.Swim;

                default:
                    return true;
            }
        }

        private static AnimationPolicy GetFallbackPolicy(SelectorAvatarType avatarType, AnimationPolicy preferredFallback)
        {
            // Check if preferred fallback is valid
            if (IsPolicyAvailableForAvatar(preferredFallback, avatarType))
                return preferredFallback;

            // Avatar-specific fallbacks
            switch (avatarType)
            {
                case SelectorAvatarType.Biped: return AnimationPolicy.Locomotion;
                case SelectorAvatarType.Quadruped: return AnimationPolicy.Locomotion;
                case SelectorAvatarType.Flying: return AnimationPolicy.Fly;
                case SelectorAvatarType.Swimming: return AnimationPolicy.Swim;
                case SelectorAvatarType.LargeMonster: return AnimationPolicy.Combat;
                default: return AnimationPolicy.Locomotion;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Internal: Policy Mapping & Priority
        // ─────────────────────────────────────────────────────────────────────────────

        private static float GetPolicyPriority(AnimationPolicy policy)
        {
            // Lower value = higher priority
            switch (policy)
            {
                case AnimationPolicy.Combat: return 0f;
                case AnimationPolicy.React: return 1f;
                case AnimationPolicy.Fly: return 2f;
                case AnimationPolicy.Swim: return 3f;
                case AnimationPolicy.Mount: return 4f;
                case AnimationPolicy.Climb: return 5f;
                case AnimationPolicy.Locomotion: return 6f;
                case AnimationPolicy.Interact: return 7f;
                default: return 99f;
            }
        }

        /// <summary>
        /// Convert AnimationPolicy to NeuralAnimationController.PolicyType.
        /// </summary>
        public static NeuralAnimationController.PolicyType PolicyTypeFromAnimationPolicy(AnimationPolicy policy)
        {
            switch (policy)
            {
                case AnimationPolicy.Combat: return NeuralAnimationController.PolicyType.Combat;
                case AnimationPolicy.React: return NeuralAnimationController.PolicyType.React;
                case AnimationPolicy.Fly: return NeuralAnimationController.PolicyType.Fly;
                case AnimationPolicy.Swim: return NeuralAnimationController.PolicyType.Swim;
                case AnimationPolicy.Mount: return NeuralAnimationController.PolicyType.Locomotion; // Mount uses locomotion policy
                case AnimationPolicy.Climb: return NeuralAnimationController.PolicyType.React; // Climb uses react policy
                case AnimationPolicy.Locomotion: return NeuralAnimationController.PolicyType.Locomotion;
                case AnimationPolicy.Interact: return NeuralAnimationController.PolicyType.Interact;
                default: return NeuralAnimationController.PolicyType.Locomotion;
            }
        }

        /// <summary>
        /// Convert NeuralAnimationController.PolicyType to AnimationPolicy.
        /// </summary>
        public static AnimationPolicy AnimationPolicyFromPolicyType(NeuralAnimationController.PolicyType policyType)
        {
            switch (policyType)
            {
                case NeuralAnimationController.PolicyType.Combat: return AnimationPolicy.Combat;
                case NeuralAnimationController.PolicyType.React: return AnimationPolicy.React;
                case NeuralAnimationController.PolicyType.Fly: return AnimationPolicy.Fly;
                case NeuralAnimationController.PolicyType.Swim: return AnimationPolicy.Swim;
                case NeuralAnimationController.PolicyType.Locomotion: return AnimationPolicy.Locomotion;
                case NeuralAnimationController.PolicyType.Interact: return AnimationPolicy.Interact;
                default: return AnimationPolicy.Locomotion;
            }
        }

        /// <summary>
        /// Convert AnimationState to AnimationPolicy.
        /// </summary>
        public static AnimationPolicy PolicyFromState(AnimationState state, SelectorAvatarType avatarType, CombatContext combatCtx)
        {
            var context = new PolicySelectionContext(state, avatarType, combatCtx, true, false, false, false, false, 0f, 0f, Vector3.forward, 0f);
            return EvaluatePolicyPriority(context);
        }

        /// <summary>
        /// Convert ProceduralAnimStateMachine.State to AnimationState.
        /// </summary>
        public static AnimationState AnimationStateFromProceduralState(ProceduralAnimStateMachine.State state)
        {
            switch (state)
            {
                case ProceduralAnimStateMachine.State.Locomotion: return AnimationState.Locomotion;
                case ProceduralAnimStateMachine.State.Jump: return AnimationState.Jump;
                case ProceduralAnimStateMachine.State.Airborne: return AnimationState.Airborne;
                case ProceduralAnimStateMachine.State.Landing: return AnimationState.Landing;
                case ProceduralAnimStateMachine.State.Attack: return AnimationState.Attack;
                case ProceduralAnimStateMachine.State.Gather: return AnimationState.Interact;
                case ProceduralAnimStateMachine.State.Roll: return AnimationState.Dodge;
                case ProceduralAnimStateMachine.State.Climb: return AnimationState.Climb;
                case ProceduralAnimStateMachine.State.Stagger: return AnimationState.Stagger;
                case ProceduralAnimStateMachine.State.Death: return AnimationState.Death;
                default: return AnimationState.Locomotion;
            }
        }

        /// <summary>
        /// Convert AnimationState to NeuralAnimationController.PolicyType.
        /// </summary>
        public static NeuralAnimationController.PolicyType PolicyTypeFromAnimationState(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Attack:
                case AnimationState.Defend:
                    return NeuralAnimationController.PolicyType.Combat;
                case AnimationState.Dodge:
                case AnimationState.Stagger:
                    return NeuralAnimationController.PolicyType.React;
                case AnimationState.Fly:
                    return NeuralAnimationController.PolicyType.Fly;
                case AnimationState.Swim:
                    return NeuralAnimationController.PolicyType.Swim;
                case AnimationState.Interact:
                    return NeuralAnimationController.PolicyType.Interact;
                case AnimationState.Mount:
                    return NeuralAnimationController.PolicyType.Locomotion;
                case AnimationState.Climb:
                    return NeuralAnimationController.PolicyType.React;
                default:
                    return NeuralAnimationController.PolicyType.Locomotion;
            }
        }
    }
}