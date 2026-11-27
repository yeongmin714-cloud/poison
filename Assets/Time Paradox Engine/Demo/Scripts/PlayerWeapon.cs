using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Data;
using TimeWarp.Modules.Gameplay; 
using TimeWarp.Modules.Paradox;
using TimeWarp.Modules.Zones; // Wymagane, aby broń widziała klasy stref!

namespace YourGame.Weapons 
{
    public class PlayerWeapon : MonoBehaviour, IGhostPreserve, IGhostEventHandler
    {
        [Header("Weapon Settings")]
        [Tooltip("Wstaw dokładnie 4 typy amunicji (indeksy 0, 1, 2, 3)")]
        public GameObject[] ProjectilePrefabs; 
        public Transform FirePoint;
        public float ShootForce = 20f;

        [Header("TimeWarp Zones")]
        [Tooltip("Przeciągnij tutaj strefę ze SCENY. Broń przekaże ją każdemu wystrzelonemu pociskowi.")]
        public TimeWarpPermissionZone ZoneForBullets; 

        private bool _isGhost; 
        private int _currentAmmoIndex = 0;

        // ==========================================
        // NOWE ZMIENNE: Kontrola kółka myszy (Cooldown)
        // ==========================================
        private float _scrollCooldown = 0.15f; // Zmień tę wartość, jeśli chcesz szybszego/wolniejszego przewijania
        private float _nextScrollTime = 0f;

        private void Start()
        {
            // Sprawdzamy, czy ten konkretny obiekt jest Duchem
            _isGhost = GetComponentInParent<TimeEchoGhost>() != null; 
        }

        private void Update()
        {
            if (_isGhost) return; 

            // ==========================================
            // 1. ZMIANA AMUNICJI (ZAKTUALIZOWANA LOGIKA)
            // ==========================================
            if (Time.time >= _nextScrollTime)
            {
                // Używamy GetAxis zamiast mouseScrollDelta.y dla lepszego wygładzania
                float scroll = Input.GetAxis("Mouse ScrollWheel"); 
                
                // Używamy marginesu 0.05f (martwej strefy), by ignorować "mikro-ruchy" myszki
                if (scroll > 0.05f) 
                {
                    _currentAmmoIndex++;
                    if (_currentAmmoIndex >= ProjectilePrefabs.Length) _currentAmmoIndex = 0;
                    
                    _nextScrollTime = Time.time + _scrollCooldown; // Ustawienie opóźnienia
                }
                else if (scroll < -0.05f)
                {
                    _currentAmmoIndex--;
                    if (_currentAmmoIndex < 0) _currentAmmoIndex = ProjectilePrefabs.Length - 1;
                    
                    _nextScrollTime = Time.time + _scrollCooldown; // Ustawienie opóźnienia
                }
            }

            // 2. STRZELANIE I NAGRYWANIE
            if (Input.GetMouseButtonDown(0))
            {
                PerformShoot(FirePoint.forward, ShootForce, _currentAmmoIndex);

                if (TryGetComponent(out TimeEventRecorder recorder)) 
                {
                    TimeEventPayload payload = new TimeEventPayload("Shoot"); 
                    payload.Vector1 = FirePoint.forward;
                    payload.Float1 = ShootForce;
                    payload.Int1 = _currentAmmoIndex; 
                    
                    recorder.RecordEvent(payload); 
                }
            }
        }

        /// <summary>
        /// UNIWERSALNA METODA FIZYCZNA - używana przez Gracza i Ducha!
        /// </summary>
        private void PerformShoot(Vector3 direction, float force, int ammoIndex)
        {
            if (ProjectilePrefabs == null || ProjectilePrefabs.Length == 0 || FirePoint == null) return;

            int safeIndex = Mathf.Clamp(ammoIndex, 0, ProjectilePrefabs.Length - 1);
            GameObject prefabToShoot = ProjectilePrefabs[safeIndex];

            if (prefabToShoot == null) return;

            // Tworzenie pocisku
            GameObject bullet = Instantiate(prefabToShoot, FirePoint.position, Quaternion.LookRotation(direction));
            
            // =================================================================
            // MAGIA OBEJŚCIA: Wstrzykiwanie strefy ze sceny do prefaba w locie
            // =================================================================
            if (ZoneForBullets != null && bullet.TryGetComponent(out TimeWarpIdentity bulletIdentity))
            {
                // Dodajemy strefę do filtrów nagrywania pocisku
                bulletIdentity.ActionFilters.RecordingZones.Add(ZoneForBullets);
                
                // Jeśli chcesz, by strefa wpływała też na przewijanie i slow-mo pocisku, odkomentuj:
                // bulletIdentity.ActionFilters.RewindZones.Add(ZoneForBullets);
                // bulletIdentity.ActionFilters.SlowMoZones.Add(ZoneForBullets);
            }

            // Aplikowanie fizyki (zawsze 100% siły, TimeBody zwolni to samo)
            if (bullet.TryGetComponent(out Rigidbody rb))
            {
                rb.AddForce(direction * force, ForceMode.VelocityChange);
            }
        }

        // 3. ODBIORNIK ZDARZEŃ HISTORII (Dla Duchów)
        public void OnGhostEvent(TimeEventPayload payload) 
        {
            if (payload.EventHash == Animator.StringToHash("Shoot"))
            {
                PerformShoot(payload.Vector1, payload.Float1, payload.Int1);
            }
        }
    }
}