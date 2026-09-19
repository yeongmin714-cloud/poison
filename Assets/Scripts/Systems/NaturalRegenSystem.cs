using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// C-O4-01: 자연 재생 시스템 (OpenMMO COMBAT.md 벤치마크 — docs/reference/openmmo/COMBAT.md L137-167).
    /// - 16초 주기(RegenRules.CycleSeconds) 틱마다 플레이어를 재생
    /// - 재생량 = RegenRules.ComputeAmount(Lv, VIT 점수) — 최소 1
    /// - 조건: 생존 + 만전 미만 + 마지막 피격 10초 경과 (RegenRules.CanRegen)
    /// - 병사(GuardManager)는 기존 30초 회복 체계 유지(특수 퇴각 로직 존재) — 이 시스템은 플레이어만 재생하며
    ///   병사 회복 로직을 절대 건드리지 않는다.
    /// </summary>
    public class NaturalRegenSystem : MonoBehaviour
    {
        public static NaturalRegenSystem Instance { get; private set; }

        [SerializeField] private float _tickSeconds = RegenRules.CycleSeconds;
        private float _timer;

        private void Awake()
        {
            // === 싱글톤: 첫 번째가 주인 (EconomyAuditSystem 패턴 동일) ===
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            while (_timer >= _tickSeconds)
            {
                _timer -= _tickSeconds;
                Tick();
            }
        }

        /// <summary>재생 틱 1회 — 플레이어만 대상(병사는 GuardManager 기존 30초 회복 체계 유지).</summary>
        private void Tick()
        {
            var ph = PlayerHealth.Instance;
            var stats = PlayerStats.Instance;
            if (ph == null || stats == null) return;

            bool alive = ph.IsAlive;
            bool belowMax = ph.CurrentHP < ph.MaxHP;
            if (!RegenRules.CanRegen(alive, belowMax, ph.SecondsSinceLastDamage)) return;

            // [O10] 허기 게이트 — 쇠약(Hunger<30) 시 자연 재생 정지 (HUNGER.md 벤치; RegenRules.CanRegen 미수정).
            var hs = HungerSystem.Instance ?? FindAnyObjectByType<HungerSystem>();
            if (hs != null && hs.BlocksNaturalRegen()) return;

            // VIT 점수 환산: 기본 10 + 할당 VIT (간단 환산 — 장비 보너스 등 정밀 환산은 추후 확장)
            int vitScore = 10 + stats.AllocatedVit;
            int amount = RegenRules.ComputeAmount(stats.Level, vitScore);
            ph.Heal(amount);
            Debug.Log($"[NaturalRegen] Lv{stats.Level} +{amount} 재생");
        }
    }
}
