using System.Collections.Generic;
using ProjectName.Core; // [156차 P5] WeaponData/PlayerHealth — Core 참조(Systems→Core 허용 방향)
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [156차 P5] 이계 스킬 8종 — 자동발동(쿨 기반, 키 계약 무접촉) + 런 한정.
    /// - 적 판정은 AfterlifeSurvivalSystem.AliveEnemies 읽기 전용 뷰(물리 쿼리 0 — 결정론/EditMode 안전)
    /// - 적 슬로우는 적 로컬 계수(EnemySlowFactor — 전역 Time.timeScale 무접촉, ParadoxTimeStop 소유권 분리 계약 준수)
    /// - VFX는 ShockwaveRingFX 프리미티브 재사용(벡터메시 금지 계약 준수)
    ///   [208차] 사용자 승인 VFX 프리팹 1종(VFX_Trail_Dark — 회전 참격 앵커 트레일, Resources 경로)
    /// - 혼백 활 자동 발사는 화살 소모 없는 직접 판정(ArrowManager 미경유 — 인벤 화살 무영향)
    /// </summary>
    public static class AfterlifeSkillSystem
    {
        // ===== 시계 (EditMode 주입 — 73차 카메라 오버라이드 선례) =====
        internal static System.Func<float> UnscaledTimeOverride { get; set; }
        private static float Now => UnscaledTimeOverride != null ? UnscaledTimeOverride() : Time.unscaledTime;

        /// <summary>테스트/오프라인용 활성 오버라이드 (런 게이트 우회 — 테스트 finally에서 반드시 null 복원).</summary>
        internal static bool? TestActiveOverride { get; set; }
        /// <summary>[156차 P5-수리] 플레이어 트랜스폼 오버라이드 — EditMode에선 AddComponent해도 Awake 미호출로 PlayerHealth.Instance가 null이다(실측). 테스트가 주입, finally에서 null 복원.</summary>
        internal static Transform PlayerOverride { get; set; }

        // ===== 상태 =====
        private static readonly Dictionary<string, float> _nextReadyAt = new Dictionary<string, float>();
        private static float _enemySlowUntil;
        private static float _enemySlowFactor = 1f;
        private static bool _fireZoneActive;
        private static Vector3 _fireZonePos;
        private static float _fireZoneEndsAt;
        private static float _fireZoneNextTickAt;
        private static float _soulBowNextAt;

        /// <summary>적 이속 계수 — 시간 찌그러짐 발동 중 0.4 (AfterlifeEnemy 이동 훅이 소비).</summary>
        public static float EnemySlowFactor => Now < _enemySlowUntil ? _enemySlowFactor : 1f;
        /// <summary>혼불 장판 활성 여부 (표시/VFX 확장용).</summary>
        public static bool FireZoneActive => _fireZoneActive;

        private static float CooldownOf(string id)
        {
            switch (id)
            {
                case "s_spin_slash": return 8f;
                case "s_charge": return 12f;
                case "s_shockwave": return 15f;
                case "s_holy_cross": return 20f;
                case "s_fire_zone": return 15f;
                case "s_time_warp": return 25f;
                case "s_tentacle": return 12f;
                case "s_bone_shield": return 10f;
                default: return 10f;
            }
        }

        /// <summary>런 경계 리셋 — SurvivalSystem BeginRun/EndRun 3곳에서 호출.</summary>
        public static void ResetState()
        {
            AfterlifeSpinAnchor.DestroyAll(); // [208차] 회전 참격 앵커 전멸(런 경계 — 앵커 라이프사이클)
            _nextReadyAt.Clear();
            _enemySlowUntil = 0f;
            _enemySlowFactor = 1f;
            _fireZoneActive = false;
            _fireZoneEndsAt = 0f;
            _fireZoneNextTickAt = 0f;
            _soulBowNextAt = 0f;
        }

        /// <summary>드라이버 틱 — 스킬 자동발동 + 혼불 장판 틱 + 혼백 활 자동 발사.</summary>
        public static void Tick()
        {
            bool active = TestActiveOverride ?? AfterlifeSurvivalSystem.IsActive;
            if (!active) return;
            if (AfterlifeAugmentSystem.IsPending || AfterlifeCardSystem.IsPending) return; // 3택 대기 — 아레나 정지 계약

            float now = Now;

            var skills = AfterlifeCardSystem.OwnedSkills;
            for (int i = 0; i < skills.Count; i++)
            {
                var s = skills[i];
                float next;
                if (!_nextReadyAt.TryGetValue(s.Id, out next) || now >= next)
                {
                    Fire(s, now);
                    _nextReadyAt[s.Id] = now + CooldownOf(s.Id); // 발화 시점 기준 쿨 시작(적중 무관)
                }
            }

            // [156차 P5] 혼백 활 — 활성 무기 = w_soul_bow 6초마다 가장 가까운 적 자동 타격
            if (AfterlifeCardSystem.ActiveWeaponId == "w_soul_bow" && now >= _soulBowNextAt)
            {
                _soulBowNextAt = now + 6f;
                int lv = AfterlifeCardSystem.WeaponLevel("w_soul_bow");
                int arrows = lv >= 3 ? 3 : (lv >= 2 ? 2 : 1); // 강화: 2화살/3화살
                FireAutoArrows(arrows);
            }

            TickFireZone(now);
        }

        private static void Fire(CardDef skill, float now)
        {
            Transform playerT = ResolvePlayerTransform();
            if (playerT == null) return; // 씬 부재 — 발화 조건 미충족(쿨은 기록, 피해 0 — EditMode 안전)
            Vector3 origin = playerT.position;

            switch (skill.Id)
            {
                case "s_spin_slash": // [208차] 회전 참격 — 궤도 접촉피해 앵커(즉발 반경 피해 대체 — 피해원 단일)
                    AfterlifeSpinAnchor.EnsureRunning(playerT); // 8s 발화 시 앵커 스폰/재사용 — 접촉 피해는 앵커가 담당
                    ShockwaveRingFX.Spawn(origin, 3.5f, new Color(1f, 0.55f, 0.2f), 0.4f);
                    break;

                case "s_charge": // 망자의 돌진 — 최근접 20m
                {
                    var target = NearestEnemy(origin, 20f);
                    if (target != null)
                    {
                        Vector3 flat = Flat(target.transform.position - origin);
                        target.TakeDamage(25f, flat.normalized, "afterlife_skill");
                        ShockwaveRingFX.SpawnDirectionalSpark(origin, flat.normalized, new Color(1f, 0.4f, 0.2f), 0.3f);
                    }
                    break;
                }

                case "s_shockwave": // 충격파 — 6m + 히트스톱
                    DamageRadius(origin, 6f, 20f);
                    HitStopManager.RequestHitStop(0.03f, 0.12f);
                    ShockwaveRingFX.Spawn(origin, 6f, new Color(1f, 0.8f, 0.3f), 0.45f);
                    break;

                case "s_holy_cross": // 성화의 십자가 — 5m + 자가 힐
                    DamageRadius(origin, 5f, 18f);
                    {
                        var hp = PlayerHealth.Instance;
                        if (hp != null) hp.Heal(10f);
                    }
                    ShockwaveRingFX.Spawn(origin, 5f, new Color(1f, 0.95f, 0.6f), 0.5f);
                    break;

                case "s_fire_zone": // 혼불 장판 — 발동 위치 3초
                    _fireZoneActive = true;
                    _fireZonePos = origin;
                    _fireZoneEndsAt = now + 3f;
                    _fireZoneNextTickAt = now;
                    ShockwaveRingFX.Spawn(origin, 2.5f, new Color(1f, 0.35f, 0.1f), 0.5f);
                    break;

                case "s_time_warp": // 시간 찌그러짐 — 적 로컬 슬로우 2초
                    _enemySlowFactor = 0.4f;
                    _enemySlowUntil = now + 2f;
                    break;

                case "s_tentacle": // 흡혈 촉수 — 가장 먼 오브 끌어당김 + 경로 피해
                {
                    var far = FarthestOrb(origin);
                    if (far != null)
                    {
                        Vector3 from = far.transform.position;
                        Vector3 flatFrom = Flat(from - origin);
                        Vector3 pulled = origin + flatFrom.normalized * 2f; // 플레이어 2m 앞으로
                        far.transform.position = new Vector3(pulled.x, from.y, pulled.z);
                        DamageSegment(from, pulled, 1.5f, 8f);
                        ShockwaveRingFX.SpawnDirectionalSpark(from, -flatFrom.normalized, new Color(0.6f, 0.2f, 0.9f), 0.3f);
                    }
                    break;
                }

                case "s_bone_shield": // 뼈 방패 — 1회 충전
                    AfterlifeCardSystem.ChargeBoneShield();
                    break;
            }
        }

        // ===== 적 판정 헬퍼 (결정론 — 물리 쿼리 0) =====

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>[156차 P5-수리] 플레이어 위치 해석 — PlayerOverride 우선, 폴백 PlayerHealth.Instance(EditMode null 대비).</summary>
        private static Transform ResolvePlayerTransform()
        {
            if (PlayerOverride != null) return PlayerOverride;
            var hp = PlayerHealth.Instance;
            return hp != null ? hp.transform : null;
        }

        private static int DamageRadius(Vector3 origin, float radius, float damage)
        {
            int hits = 0;
            var enemies = AfterlifeSurvivalSystem.AliveEnemies;
            float r2 = radius * radius;
            for (int i = enemies.Count - 1; i >= 0; i--) // 역순 — Die 중 리스트 수정 대응
            {
                var e = enemies[i];
                if (e == null || e.IsDead) continue;
                Vector3 flat = Flat(e.transform.position - origin);
                if (flat.sqrMagnitude > r2) continue;
                e.TakeDamage(damage, flat.normalized, "afterlife_skill");
                hits++;
            }
            return hits;
        }

        private static AfterlifeEnemy NearestEnemy(Vector3 origin, float maxDist)
        {
            var enemies = AfterlifeSurvivalSystem.AliveEnemies;
            AfterlifeEnemy best = null;
            float bestD = maxDist * maxDist;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || e.IsDead) continue;
                Vector3 flat = Flat(e.transform.position - origin);
                float d = flat.sqrMagnitude;
                if (d <= bestD) { bestD = d; best = e; }
            }
            return best;
        }

        private static AfterlifeOrb FarthestOrb(Vector3 origin)
        {
            var orbs = AfterlifeOrb.Live;
            AfterlifeOrb far = null;
            float best = 0f;
            for (int i = 0; i < orbs.Count; i++)
            {
                var o = orbs[i];
                if (o == null) continue;
                float d = Flat(o.transform.position - origin).sqrMagnitude;
                if (d > best) { best = d; far = o; }
            }
            return far;
        }

        private static void DamageSegment(Vector3 a, Vector3 b, float radius, float damage)
        {
            var enemies = AfterlifeSurvivalSystem.AliveEnemies;
            float r2 = radius * radius;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (e == null || e.IsDead) continue;
                Vector3 p = e.transform.position; p.y = 0f;
                Vector3 aa = a; aa.y = 0f;
                Vector3 bb = b; bb.y = 0f;
                Vector3 ab = bb - aa;
                float t = ab.sqrMagnitude > 0.0001f
                    ? Mathf.Clamp01(Vector3.Dot(p - aa, ab) / ab.sqrMagnitude)
                    : 0f;
                Vector3 closest = aa + ab * t;
                if ((p - closest).sqrMagnitude <= r2)
                    e.TakeDamage(damage, (p - closest).normalized, "afterlife_skill");
            }
        }

        private static void FireAutoArrows(int count)
        {
            Transform playerT = ResolvePlayerTransform();
            if (playerT == null) return;
            Vector3 origin = playerT.position;
            var enemies = AfterlifeSurvivalSystem.AliveEnemies;

            // [156차 P5] v1은 1타깃 자동 조준 — 2/3화살 강화는 이후 확장 슬롯(데미지 가산 없음)
            var target = NearestEnemy(origin, 25f);
            if (target != null)
            {
                Vector3 flat = Flat(target.transform.position - origin);
                target.TakeDamage(WeaponData.Bow.damage * 0.8f, flat.normalized, "afterlife_skill");
                ShockwaveRingFX.SpawnDirectionalSpark(origin, flat.normalized, new Color(0.6f, 0.9f, 1f), 0.25f);
            }
        }

        private static void TickFireZone(float now)
        {
            if (!_fireZoneActive) return;
            if (now >= _fireZoneEndsAt) { _fireZoneActive = false; return; }
            int guard = 0;
            while (now >= _fireZoneNextTickAt && guard++ < 8) // 프레임 스파이크 방지 상한
            {
                _fireZoneNextTickAt += 0.5f;
                DamageRadius(_fireZonePos, 2.5f, 6f);
            }
        }

    }
}
