using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Shows one red ground ring for each living monster that is the resolved target of a
    /// selected guard's explicit attack command. Hover state and ordinary move commands
    /// are intentionally not consulted. Rings are colliderless SelectionRingController quads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackTargetRingManager : MonoBehaviour
    {
        private readonly Dictionary<AnimalAI, GameObject> _rings = new Dictionary<AnimalAI, GameObject>();
        private readonly List<AnimalAI> _removeBuffer = new List<AnimalAI>();

        private void LateUpdate()
        {
            var selection = GuardSelectionManager.Instance;
            var targets = CollectDesignatedMonsterTargets(selection != null ? selection.SelectedGuards : null);

            // PlayerCombat's automatic target is a real combat target rather than a hover
            // candidate. Include it only while alive; guard-command targets remain strictly
            // limited to selected guards' explicit attack orders.
            var playerTarget = PlayerCombat.Instance != null ? PlayerCombat.Instance.CurrentTarget as Component : null;
            if (playerTarget != null)
            {
                var playerMonster = playerTarget.GetComponentInParent<AnimalAI>();
                if (playerMonster != null && playerMonster.IsAlive && playerMonster.gameObject.activeInHierarchy)
                    targets.Add(playerMonster);
            }

            _removeBuffer.Clear();
            foreach (var pair in _rings)
            {
                if (pair.Key == null || pair.Value == null || !targets.Contains(pair.Key))
                    _removeBuffer.Add(pair.Key);
            }

            foreach (var target in _removeBuffer)
            {
                if (_rings.TryGetValue(target, out var ring) && ring != null)
                    Destroy(ring);
                _rings.Remove(target);
            }

            foreach (var target in targets)
            {
                if (target == null) continue;
                if (_rings.TryGetValue(target, out var existing) && existing != null)
                {
                    existing.transform.position = target.transform.position + Vector3.up * 0.035f;
                    continue;
                }
                _rings[target] = CreateRing(target);
            }
        }

        /// <summary>
        /// Deduplicates resolved attack targets from the selected-guard set.
        /// CommandTarget must be a valid designated point, and CurrentAttackTarget must
        /// resolve to a living AnimalAI; an attack point alone never guesses a nearby monster.
        /// </summary>
        public static HashSet<AnimalAI> CollectDesignatedMonsterTargets(IReadOnlyList<GuardPlaceholder> selectedGuards)
        {
            var resolvedTargets = new List<Component>();
            if (selectedGuards == null) return CollectMonsterTargets(resolvedTargets);

            for (int i = 0; i < selectedGuards.Count; i++)
            {
                var guard = selectedGuards[i];
                if (!IsEligibleAttackCommand(guard)) continue;

                var resolvedTarget = guard.CurrentAttackTarget;
                if (resolvedTarget != null) resolvedTargets.Add(resolvedTarget);
            }
            return CollectMonsterTargets(resolvedTargets);
        }

        /// <summary>Resolves target components to parent monsters and aggregates exactly one entry per monster.</summary>
        public static HashSet<AnimalAI> CollectMonsterTargets(IEnumerable<Component> resolvedTargets)
        {
            var monsters = new HashSet<AnimalAI>();
            if (resolvedTargets == null) return monsters;

            foreach (var resolvedTarget in resolvedTargets)
            {
                if (resolvedTarget == null) continue;
                var monster = resolvedTarget.GetComponentInParent<AnimalAI>();
                if (monster != null && monster.IsAlive && monster.gameObject.activeInHierarchy)
                    monsters.Add(monster);
            }
            return monsters;
        }

        /// <summary>Pure command/position eligibility check, exposed for edit-mode verification.</summary>
        public static bool IsEligibleAttackCommand(GuardPlaceholder guard)
        {
            if (guard == null || !guard.IsAlive || !guard.gameObject.activeInHierarchy
                || !guard.HasCommand || !guard.IsAttackCommand)
                return false;

            Vector3 point = guard.CommandTarget;
            return IsFinite(point.x) && IsFinite(point.y) && IsFinite(point.z);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static GameObject CreateRing(AnimalAI target)
        {
            // Parent at world root to keep exactly the shared ring scale/rotation;
            // explicitly follow its position every frame below.
            var ringObject = new GameObject("AttackTargetRing_" + target.name);
            ringObject.transform.position = target.transform.position + Vector3.up * 0.035f;
            ringObject.transform.rotation = Quaternion.identity;
            ringObject.transform.localScale = Vector3.one * CommandRingPresentation.BaseScale;

            var ring = ringObject.AddComponent<SelectionRingController>();
            ring.SetColor(CommandRingPresentation.ExplicitTargetColor);
            return ringObject;
        }

        private void OnDisable() => ClearRings();
        private void OnDestroy() => ClearRings();

        private void ClearRings()
        {
            foreach (var pair in _rings)
                if (pair.Value != null) Destroy(pair.Value);
            _rings.Clear();
            _removeBuffer.Clear();
        }
    }
}
