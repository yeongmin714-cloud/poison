using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>Connects consumable use to the deployed gas cloud while a loaded sprayer is equipped.</summary>
    public static class GasCloudLauncher
    {
        private const float CloudDuration = 4f;
        public static bool IsRegistered { get; private set; }

        /// <summary>Registers the Core.Data delegate hook. Repeated calls are harmless.</summary>
        public static void RegisterHook()
        {
            if (IsRegistered && ConsumableSystem.PreUseOverride == TryDeployCloud)
                return;

            ConsumableSystem.PreUseOverride = TryDeployCloud;
            IsRegistered = true;
        }

        private static bool TryDeployCloud(PlayerInventory.ItemData item)
        {
            GasSprayerController ctrl = GasSprayerController.Instance;
            if (ctrl == null || !ctrl.IsEquipped || item == null)
                return false;

            if (item.category != PlayerInventory.ItemCategory.Potion && item.category != PlayerInventory.ItemCategory.Drug)
                return false;
            if (string.IsNullOrEmpty(ctrl.LoadedPotionId) || ctrl.LoadedPotionCount <= 0)
                return false;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
                return false;

            PotionType type = GasSprayer.ClassifyPotion(ctrl.LoadedPotionId);
            if (type == PotionType.None)
                return false;

            GasSprayerData sprayerData = ctrl.GetCurrentSprayerData();
            float radius = sprayerData.sprayRange > 0f ? sprayerData.sprayRange : 4f;
            GameObject cloudObject = GasCloudField.Spawn(player.transform, Vector3.zero, radius, CloudDuration, type);
            if (cloudObject == null)
                return false;

            GasCloudField field = cloudObject.GetComponent<GasCloudField>();
            GasCloudFieldEffect effects = cloudObject.AddComponent<GasCloudFieldEffect>();
            effects.Initialize(field, radius, CloudDuration);

            // Keep the legacy one-cloud-per-loaded-item contract, but synchronize the new active-dose timer.
            // Older saved/runtime states can contain multiple loaded items; each cloud consumes exactly one.
            int remainingPotionCount = Mathf.Max(0, ctrl.LoadedPotionCount - 1);
            ctrl.SetLoadedPotionDose(remainingPotionCount > 0 ? ctrl.LoadedPotionId : string.Empty, remainingPotionCount);

            if (!sprayerData.isUnlimited)
                ctrl.CurrentSprayTimeRemaining = Mathf.Max(0f, ctrl.CurrentSprayTimeRemaining - 3f);

            Debug.Log($"[GasCloudLauncher] {type} 가스 구름 전개 (반경 {radius:0.0}m, 남은 장전 {ctrl.LoadedPotionCount})");
            return true;
        }
    }

    /// <summary>Applies periodic gameplay effects while its GasCloudField remains alive.</summary>
    [DisallowMultipleComponent]
    public sealed class GasCloudFieldEffect : MonoBehaviour
    {
        private const float TickInterval = 0.4f;
        private GasCloudField _field;
        private float _radius;
        private float _duration;
        private float _elapsed;
        private float _tickTimer;
        private readonly HashSet<int> _affectedThisTick = new HashSet<int>();

        public void Initialize(GasCloudField field, float radius, float duration)
        {
            _field = field;
            _radius = Mathf.Max(0.1f, radius);
            _duration = Mathf.Max(0.1f, duration);
            _tickTimer = TickInterval;
        }

        private void Update()
        {
            if (_field == null)
            {
                Destroy(this);
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
                return;

            _tickTimer += Time.deltaTime;
            while (_tickTimer >= TickInterval)
            {
                _tickTimer -= TickInterval;
                ApplyFieldEffects(_field, _field.AffectingType);
            }
        }

        private void ApplyFieldEffects(GasCloudField field, PotionType type)
        {
            Collider[] hits = Physics.OverlapSphere(field.transform.position, _radius);
            _affectedThisTick.Clear();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                    continue;

                Transform targetTransform = hit.transform;
                PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
                GuardPlaceholder guard = hit.GetComponentInParent<GuardPlaceholder>();
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                int targetId = health != null ? health.GetInstanceID()
                    : guard != null ? guard.GetInstanceID()
                    : damageable is Component component ? component.GetInstanceID()
                    : hit.GetInstanceID();
                if (!_affectedThisTick.Add(targetId))
                    continue;

                bool isPlayer = health != null || hit.CompareTag("Player") || targetTransform.root.CompareTag("Player");
                bool isAlly = isPlayer || (guard != null && guard.IsAlly);
                bool protectedByMask = isAlly && GasMaskSystem.IsActive;

                switch (type)
                {
                    case PotionType.Poison:
                        if (protectedByMask || damageable == null)
                            break;
                        float damage = Random.Range(6f, 10f);
                        Vector3 direction = (hit.transform.position - field.transform.position).normalized;
                        damageable.TakeDamage(damage, direction, "GasCloud_Poison");
                        break;

                    case PotionType.Mental:
                        if (protectedByMask || damageable == null)
                            break;
                        if (BuffManager.Instance != null)
                        {
                            BuffManager.Instance.AddBuff("Slowness", 0.3f, 3f);
                            BuffManager.Instance.AddBuff("Confusion", 1f, 3f);
                        }
                        break;

                    case PotionType.Heal:
                        if (health != null && !health.IsDead)
                            health.Heal(10f);
                        else if (guard != null && guard.IsAlly && !guard.IsDead)
                            guard.SetHP(Mathf.Min(guard.MaxHP, guard.CurrentHP + 10f));
                        break;

                    case PotionType.Buff:
                        if (!isAlly)
                            break;
                        if (isPlayer && BuffManager.Instance != null)
                        {
                            BuffManager.Instance.AddBuff("DefenseUp", 10f, 5f);
                            BuffManager.Instance.AddBuff("AttackUp", 5f, 5f);
                        }
                        else if (guard != null && guard.IsAlly)
                        {
                            guard.ApplyGasCloudBuff(5f, 10f, 5f);
                        }
                        break;
                }
            }
        }
    }
}
