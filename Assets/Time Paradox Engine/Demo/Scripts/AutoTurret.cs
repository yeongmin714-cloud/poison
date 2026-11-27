using UnityEngine;
using System.Collections;

namespace TimeWarp.Gameplay
{
    public class AutoTurret : MonoBehaviour
    {
        [Header("Targeting Settings")]
        public float attackRange = 15f;
        public string enemyTag = "Enemy";
        
        [Tooltip("The point from which the aiming vector is calculated (e.g., the center of the turret head or barrel). Solves the problem of aiming into space.")]
        public Transform aimOrigin;
        
        // MODIFICATION: Made public so the Listener can clear it and restart targeting
        public Transform currentTarget; 

        [Header("Rotation Settings")]
        public Transform partToRotate;
        public float rotationSpeed = 10f;
        
        [Tooltip("Maximum barrel tilt downwards (negative value).")]
        public float minPitch = -30f;
        [Tooltip("Maximum barrel tilt upwards (positive value).")]
        public float maxPitch = 60f;

        [Header("Recoil Settings")]
        public Transform barrelToRecoil;
        public float recoilDistance = 0.3f;
        
        [Tooltip("How fast the barrel moves back during a shot (kickback).")]
        public float recoilKickSpeed = 50f;
        [Tooltip("How fast the barrel returns to its original position after recoil.")]
        public float recoilReturnSpeed = 10f;

        private Vector3 originalBarrelLocalPos;
        private Vector3 currentRecoilOffset;
        private Vector3 targetRecoilOffset;

        [Header("Shooting Settings")]
        public GameObject bulletPrefab;
        public Transform firePoint;
        
        [Space(10)]
        public bool useBurstFire = true;
        public float fireRate = 1f;
        public int bulletsPerBurst = 3;
        public float timeBetweenBurstShots = 0.1f;
        public float burstDelay = 1.5f;

        [Header("Visual Effects & Lights")]
        [Tooltip("A separate point where the muzzle flash effect will appear.")]
        public Transform muzzleFlashPoint;
        public ParticleSystem muzzleFlash;
        
        [Space(10)]
        [Tooltip("Point lights that react to shooting.")]
        public Light[] shootLights;
        public float maxLightIntensity = 2f;
        
        [Space(10)]
        [Tooltip("Model parts that will glow when shooting.")]
        public Renderer[] glowingParts;
        [ColorUsage(true, true)] public Color glowColor = Color.red * 2f; // HDR color
        
        [Header("TimeWarp & Animation Timings")]
        [Tooltip("Total time sum: FadeIn + Stay + FadeOut.")]
        public float totalFlashDuration = 0.17f; 
        public float fadeInNormalized = 0.1f;  // Percentage of time to brighten (0.1 = 10%)
        public float stayNormalized = 0.3f;    // Percentage of time to hold maximum intensity
        // The rest is FadeOut

        // MODIFICATION: Made public so Bridge and Listener can access them
        public float flashProgress = -1f; 
        public float fireCountdown = 0f;
        
        public bool isShootingBurst = false;
        public int burstShotsFired = 0;
        public float burstShotTimer = 0f;

        void Start()
        {
            InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);

            if (barrelToRecoil != null)
            {
                originalBarrelLocalPos = barrelToRecoil.localPosition;
                currentRecoilOffset = Vector3.zero;
                targetRecoilOffset = Vector3.zero;
            }

            // Prepare lights and emission on start
            foreach (var l in shootLights)
            {
                if (l != null) 
                {
                    l.enabled = false;
                    l.intensity = 0f;
                }
            }

            foreach (var r in glowingParts)
            {
                if (r != null)
                {
                    // Force enable emission on the material
                    r.material.EnableKeyword("_EMISSION");
                    r.material.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        void Update()
        {
            // You can eventually hook up EffectiveTimeScale from your Manager here (e.g., dt = identity.EffectiveTimeScale * Time.deltaTime)
            float dt = Time.deltaTime; 

            // 1. VISUAL HANDLING
            HandleVisualFlash(dt);

            // 2. RECOIL
            if (barrelToRecoil != null)
            {
                targetRecoilOffset = Vector3.Lerp(targetRecoilOffset, Vector3.zero, dt * recoilReturnSpeed);
                currentRecoilOffset = Vector3.Lerp(currentRecoilOffset, targetRecoilOffset, dt * recoilKickSpeed);
                barrelToRecoil.localPosition = originalBarrelLocalPos + currentRecoilOffset;
            }

            // 3. AIMING (Only if we have a target)
            if (currentTarget != null)
            {
                LockOnTarget(dt);
            }

            // 4. SHOOTING (Fully in Update - timers always tick)
            HandleShooting(dt);
        }

        void HandleShooting(float dt)
        {
            // 1. FAILSAFE: If a burst was ongoing but the target disappeared (died/escaped) - interrupt the burst!
            if (isShootingBurst && currentTarget == null)
            {
                isShootingBurst = false;
                fireCountdown = burstDelay; // Reset cooldown, the turret needs to breathe
            }

            if (isShootingBurst)
            {
                // Burst in progress
                burstShotTimer -= dt;
                
                if (burstShotTimer <= 0f)
                {
                    ShootSingle();
                    
                    burstShotsFired++;
                    
                    if (burstShotsFired >= bulletsPerBurst)
                    {
                        // End of burst
                        isShootingBurst = false;
                        fireCountdown = burstDelay;
                    }
                    else
                    {
                        // Wait for the next shot in the burst
                        burstShotTimer = timeBetweenBurstShots;
                    }
                }
            }
            else
            {
                // 2. WAITING TO SHOOT (Always counts down, even if the turret doesn't see anyone)
                fireCountdown -= dt;

                // We start shooting ONLY when the timer reaches zero AND we have a target in sights
                if (fireCountdown <= 0f && currentTarget != null)
                {
                    if (useBurstFire)
                    {
                        // Start a new burst
                        isShootingBurst = true;
                        burstShotsFired = 1; // Count the first shot we are about to fire
                        burstShotTimer = timeBetweenBurstShots;
                        ShootSingle(); // Instantly fire the first bullet
                    }
                    else
                    {
                        // Single shot
                        ShootSingle();
                        fireCountdown = 1f / fireRate;
                    }
                }
            }
        }

        // MODIFICATION: Made public so the Listener can redraw visuals during time rewind!
        public void HandleVisualFlash(float dt)
        {
            // FIX: If we are in negative time (before the shot), TURN OFF the light!
            if (flashProgress < 0) 
            {
                UpdateVisuals(0f);
                return;
            }

            flashProgress += dt / totalFlashDuration;

            float intensityMultiplier = 0f;

            if (flashProgress <= fadeInNormalized)
            {
                intensityMultiplier = flashProgress / fadeInNormalized;
            }
            else if (flashProgress <= fadeInNormalized + stayNormalized)
            {
                intensityMultiplier = 1f;
            }
            else if (flashProgress <= 1f)
            {
                float fadeOutPos = (flashProgress - (fadeInNormalized + stayNormalized)) / (1f - (fadeInNormalized + stayNormalized));
                intensityMultiplier = 1f - fadeOutPos;
            }
            else
            {
                flashProgress = -1f;
                intensityMultiplier = 0f;
            }

            UpdateVisuals(intensityMultiplier);
        }

        void UpdateVisuals(float multiplier)
        {
            Color currentEmission = Color.Lerp(Color.black, glowColor, multiplier);
            float currentIntensity = maxLightIntensity * multiplier;

            foreach (var l in shootLights)
            {
                if (l != null)
                {
                    l.enabled = multiplier > 0.01f;
                    l.intensity = currentIntensity;
                }
            }

            foreach (var r in glowingParts)
            {
                if (r != null) r.material.SetColor("_EmissionColor", currentEmission);
            }
        }

        void ShootSingle()
        {
            // Reset flash animation to the beginning
            flashProgress = 0f; 

            if (muzzleFlash != null && muzzleFlashPoint != null)
            {
                muzzleFlash.transform.position = muzzleFlashPoint.position;
                muzzleFlash.transform.rotation = muzzleFlashPoint.rotation;
                muzzleFlash.Play();
            }
            
            ApplyRecoil();
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        }

        void ApplyRecoil()
        {
            if (barrelToRecoil != null)
                targetRecoilOffset -= Vector3.forward * recoilDistance;
        }

        // MODIFICATION: Made public so the Listener can resume searching for a target!
        public void UpdateTarget()
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
            float shortestDistance = Mathf.Infinity;
            GameObject nearestEnemy = null;
            Vector3 checkPos = aimOrigin != null ? aimOrigin.position : transform.position;

            foreach (GameObject enemy in enemies)
            {
                float dist = Vector3.Distance(checkPos, enemy.transform.position);
                if (dist < shortestDistance) { shortestDistance = dist; nearestEnemy = enemy; }
            }
            
            currentTarget = (nearestEnemy != null && shortestDistance <= attackRange) ? nearestEnemy.transform : null;
        }

        void LockOnTarget(float dt)
        {
            Transform origin = aimOrigin != null ? aimOrigin : partToRotate;
            Vector3 dir = currentTarget.position - origin.position;
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            
            Vector3 euler = lookRotation.eulerAngles;
            float pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;
            
            euler.x = Mathf.Clamp(pitch, minPitch, maxPitch);
            
            partToRotate.rotation = Quaternion.Lerp(partToRotate.rotation, Quaternion.Euler(euler), dt * rotationSpeed);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Transform origin = aimOrigin != null ? aimOrigin : transform;
            Gizmos.DrawWireSphere(origin.position, attackRange);
        }
    }
}