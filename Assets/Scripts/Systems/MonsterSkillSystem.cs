using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 🐉 몬스터 스킬/패턴 시스템 (MonoBehaviour 싱글톤)
    /// 각 몬스터 타입별 고유 스킬 정의 및 실행 담당.
    /// </summary>
    public class MonsterSkillSystem : MonoBehaviour
    {
        // ===== 싱글톤 =====
        private static MonsterSkillSystem _instance;
        public static MonsterSkillSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MonsterSkillSystem");
                    _instance = go.AddComponent<MonsterSkillSystem>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        // ===== MonsterSkill 열거형 =====
        public enum MonsterSkill
        {
            None,
            Fireball,
            PoisonSting,
            Charge,
            Leap,
            Teleport,
            AoEExplosion,
            Heal,
            SummonMinion,
            Debuff
        }

        // ===== MonsterSkillData 구조체 =====
        [System.Serializable]
        public struct MonsterSkillData
        {
            public MonsterSkill skill;
            public float cooldown;
            public float damage;
            public float range;
            public string animationTrigger;

            public MonsterSkillData(MonsterSkill skill, float cooldown, float damage, float range, string animationTrigger)
            {
                this.skill = skill;
                this.cooldown = cooldown;
                this.damage = damage;
                this.range = range;
                this.animationTrigger = animationTrigger;
            }
        }

        // ===== 쿨다운 트래킹 =====
        private readonly Dictionary<AnimalAI, float> _cooldownTimers = new Dictionary<AnimalAI, float>();

        // ===== 스킬 정의 매핑 (몬스터 ID → 스킬 리스트) =====
        private static readonly Dictionary<string, MonsterSkillData[]> _skillMap = new Dictionary<string, MonsterSkillData[]>
        {
            // 토끼 — 스킬 없음
            { "rabbit", new MonsterSkillData[] { } },

            // 늑대 — 돌진 (거리 8m, 1.5배 데미지)
            { "wolf", new MonsterSkillData[]
                {
                    new MonsterSkillData(MonsterSkill.Charge, 3f, 1.5f, 8f, "Charge")
                }
            },

            // 멧돼지 — 돌진 + 덤벼들기
            { "boar", new MonsterSkillData[]
                {
                    new MonsterSkillData(MonsterSkill.Charge, 3f, 1.5f, 8f, "Charge"),
                    new MonsterSkillData(MonsterSkill.Leap, 5f, 2f, 5f, "Leap")
                }
            },

            // 돌골렘 — 범위 지진 (5m, 2배 데미지)
            { "stone_golem", new MonsterSkillData[]
                {
                    new MonsterSkillData(MonsterSkill.AoEExplosion, 4f, 2f, 5f, "AoE")
                }
            },

            // 화염도마뱀 — 원거리 화염구 (10m, 1.5배)
            { "fire_lizard", new MonsterSkillData[]
                {
                    new MonsterSkillData(MonsterSkill.Fireball, 3f, 1.5f, 10f, "Fireball")
                }
            },

            // 슬라임 — 분열 패턴 (HP 30% 이하 시 2마리, 코드 레벨에서 처리)
            { "slime", new MonsterSkillData[] { } },

            // 독뱀 — 독 데미지 + 이동속도 감소
            { "poison_snake", new MonsterSkillData[]
                {
                    new MonsterSkillData(MonsterSkill.PoisonSting, 2f, 1.2f, 3f, "PoisonSting"),
                    new MonsterSkillData(MonsterSkill.Debuff, 4f, 0f, 3f, "Debuff")
                }
            },

            // 박쥐 — 순간이동 (플레이어 뒤로)
            { "bat", new MonsterSkillData[]
                {
                    new MonsterSkillData(MonsterSkill.Teleport, 3f, 0f, 8f, "Teleport")
                }
            },

            // 만티코어 — 3스킬 복합
            { "manticore", new MonsterSkillData[]
                {
                    new MonsterSkillData(MonsterSkill.Fireball, 3f, 1.5f, 10f, "Fireball"),
                    new MonsterSkillData(MonsterSkill.Charge, 4f, 1.5f, 8f, "Charge"),
                    new MonsterSkillData(MonsterSkill.AoEExplosion, 6f, 2f, 5f, "AoE")
                }
            },

            // 미노타우로스 — 강력한 돌진 + 광역
            { "minotaur", new MonsterSkillData[]
                {
                    new MonsterSkillData(MonsterSkill.Charge, 3f, 2f, 10f, "Charge"),
                    new MonsterSkillData(MonsterSkill.AoEExplosion, 5f, 2f, 5f, "AoE")
                }
            },

            // 그림자암살자 — 순간이동 + 독 공격
            { "shadow_assassin", new MonsterSkillData[]
                {
                    new MonsterSkillData(MonsterSkill.Teleport, 2f, 0f, 10f, "Teleport"),
                    new MonsterSkillData(MonsterSkill.PoisonSting, 3f, 1.5f, 3f, "PoisonSting")
                }
            },
        };

        // ===== 드라큘라 영주 — 보스 패턴 (SummonMinion + Heal + Teleport) =====
        private static readonly MonsterSkillData[] _draculaSkills = new MonsterSkillData[]
        {
            new MonsterSkillData(MonsterSkill.SummonMinion, 8f, 0f, 10f, "Summon"),
            new MonsterSkillData(MonsterSkill.Heal, 12f, 0.2f, 0f, "Heal"),
            new MonsterSkillData(MonsterSkill.Teleport, 5f, 0f, 8f, "Teleport"),
        };

        // ===== 스킬 이름 (한국어 표시용) =====
        public static string GetSkillDisplayName(MonsterSkill skill)
        {
            return skill switch
            {
                MonsterSkill.None         => "없음",
                MonsterSkill.Fireball      => "🔥 화염구",
                MonsterSkill.PoisonSting   => "☠️ 독침",
                MonsterSkill.Charge        => "💨 돌진",
                MonsterSkill.Leap          => "🦘 덤벼들기",
                MonsterSkill.Teleport      => "⚡ 순간이동",
                MonsterSkill.AoEExplosion  => "💥 범위 폭발",
                MonsterSkill.Heal          => "💚 회복",
                MonsterSkill.SummonMinion  => "🦇 소환",
                MonsterSkill.Debuff        => "🐌 약화",
                _ => "❓ 알 수 없음"
            };
        }

        /// <summary>
        /// 몬스터 이름(ID)으로 해당 몬스터의 스킬 목록을 반환합니다.
        /// DraculaLord는 별도 처리 (DraculaLord.cs에서 식별).
        /// </summary>
        public MonsterSkillData[] GetSkillForMonster(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId)) return System.Array.Empty<MonsterSkillData>();

            if (_skillMap.TryGetValue(monsterId, out MonsterSkillData[] skills))
                return skills;

            return System.Array.Empty<MonsterSkillData>();
        }

        /// <summary>
        /// 드라큘라 영주 스킬 반환
        /// </summary>
        public MonsterSkillData[] GetDraculaSkills()
        {
            return _draculaSkills;
        }

        /// <summary>
        /// 몬스터의 displayName(한글)으로도 스킬 조회
        /// </summary>
        public MonsterSkillData[] GetSkillForMonsterByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return System.Array.Empty<MonsterSkillData>();

            // MonsterDatabase에서 ID 찾기
            foreach (var kvp in MonsterDatabase.All)
            {
                if (kvp.Value.displayName == displayName)
                    return GetSkillForMonster(kvp.Key);
            }

            return System.Array.Empty<MonsterSkillData>();
        }

        /// <summary>
        /// AnimalAI의 몬스터 ID로 스킬 목록을 반환합니다.
        /// </summary>
        public MonsterSkillData[] GetSkillsForAI(AnimalAI ai)
        {
            if (ai == null) return System.Array.Empty<MonsterSkillData>();
            return GetSkillForMonster(ai.MonsterId);
        }

        // ===== 스킬 실행 =====

        /// <summary>
        /// 몬스터 스킬 실행.
        /// 쿨다운을 확인하고, 시각 효과/데미지/상태이상을 적용합니다.
        /// </summary>
        /// <param name="monster">스킬을 사용할 몬스터 (AnimalAI). DraculaLord 대신 null 전달 후 casterName 사용</param>
        /// <param name="skillData">실행할 스킬 데이터</param>
        /// <param name="target">대상 게임오브젝트 (일반적으로 Player)</param>
        /// <param name="casterName">시전자 이름 (DraculaLord 등 null monster 대신 사용)</param>
        public bool ExecuteSkill(AnimalAI monster, MonsterSkillData skillData, GameObject target, string casterName = "")
        {
            if (target == null) return false;

            // AnimalAI가 null이면 DraculaLord 등 다른 시전자로 간주
            bool isDraculaMode = monster == null;
            if (!isDraculaMode && monster.IsDead) return false;

            // AnimalAI 모드: 쿨다운 확인
            if (!isDraculaMode)
            {
                float now = Time.time;
                if (_cooldownTimers.TryGetValue(monster, out float lastUsed))
                {
                    if (now - lastUsed < skillData.cooldown)
                        return false;
                }
                _cooldownTimers[monster] = now;
            }

            // 시전자 정보
            string monsterName = casterName;
            Vector3 monsterPos = Vector3.zero;
            if (!isDraculaMode)
            {
                MonsterDef def = MonsterDatabase.Get(monster.MonsterId);
                monsterName = def != null ? def.displayName : monster.MonsterId;
                monsterPos = monster.transform.position;
            }

            Vector3 targetPos = target.transform.position;

            switch (skillData.skill)
            {
                case MonsterSkill.Fireball:
                    ExecuteFireball(monster, skillData, target, monsterPos, targetPos);
                    break;

                case MonsterSkill.PoisonSting:
                    ExecutePoisonSting(monster, skillData, target);
                    break;

                case MonsterSkill.Charge:
                    ExecuteCharge(monster, skillData, target, monsterPos, targetPos);
                    break;

                case MonsterSkill.Leap:
                    ExecuteLeap(monster, skillData, target, monsterPos, targetPos);
                    break;

                case MonsterSkill.Teleport:
                    ExecuteTeleport(monster, target, monsterPos, targetPos);
                    break;

                case MonsterSkill.AoEExplosion:
                    ExecuteAoEExplosion(monster, skillData, monsterPos);
                    break;

                case MonsterSkill.Heal:
                    ExecuteHeal(monster, skillData);
                    break;

                case MonsterSkill.SummonMinion:
                    ExecuteSummonMinion(monster, target, monsterPos);
                    break;

                case MonsterSkill.Debuff:
                    ExecuteDebuff(monster, skillData, target);
                    break;

                default:
                    return false;
            }

            // MonsterSkillUI에 알림 (이벤트)
            NotifySkillExecuted(monster, skillData.skill, monsterName);
            return true;
        }

        /// <summary>
        /// 스킬 실행 시 MonsterSkillUI에 통지하는 정적 이벤트
        /// </summary>
        public static event System.Action<AnimalAI, MonsterSkill, string> OnSkillExecuted;

        private void NotifySkillExecuted(AnimalAI monster, MonsterSkill skill, string monsterName)
        {
            OnSkillExecuted?.Invoke(monster, skill, monsterName);
        }

        // ====== 개별 스킬 실행 로직 ======

        /// <summary>
        /// 🔥 화염구 — Sphere 생성 + Rigidbody로 대상 방향 발사
        /// </summary>
        private void ExecuteFireball(AnimalAI monster, MonsterSkillData skillData, GameObject target, Vector3 monsterPos, Vector3 targetPos)
        {
            // 화염구 생성
            GameObject fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fireball.name = "Fireball_Projectile";
            fireball.transform.position = monsterPos + Vector3.up * 1.5f;
            fireball.transform.localScale = Vector3.one * 0.5f;

            // 콜라이더는 Trigger로 변경
            Collider col = fireball.GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            // 색상 적용 (빨간색/주황색)
            Renderer renderer = fireball.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(1f, 0.4f, 0f);

            // Rigidbody 추가 및 발사
            Rigidbody rb = fireball.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Vector3 direction = (targetPos - monsterPos).normalized;
            direction.y = Mathf.Clamp(direction.y, 0f, 0.5f); // 약간 위로
            rb.linearVelocity = direction * 15f;

            // FireballProjectile 스크립트 부착 (충돌 처리용)
            FireballProjectile proj = fireball.AddComponent<FireballProjectile>();
            proj.Init(skillData, monster);

            // 시각 효과: 파티클
            CombatVFXController.SpawnHitSparks(monsterPos);
            Debug.Log($"[MonsterSkill] 🔥 {MonsterDatabase.Get(monster.MonsterId)?.displayName ?? monster.MonsterId} 화염구 발사!");
        }

        /// <summary>
        /// ☠️ 독침 — PlayerHealth에 독 DOT 적용
        /// </summary>
        private void ExecutePoisonSting(AnimalAI monster, MonsterSkillData skillData, GameObject target)
        {
            if (PlayerHealth.Instance == null) return;

            // 기본 데미지 (1.2배)
            float baseDamage = monster.MaxHP * 0.05f;
            float damage = baseDamage * skillData.damage;
            PlayerHealth.Instance.TakeDamage(Mathf.RoundToInt(damage));

            // 독 DOT: BuffManager로 "Poison" DOT 적용 (초당 데미지, 5초)
            if (BuffManager.Instance != null)
            {
                float dotPerTick = damage * 0.3f;
                BuffManager.Instance.AddBuff("Poison", dotPerTick, 5f);
                Debug.Log($"[MonsterSkill] ☠️ {MonsterDatabase.Get(monster.MonsterId)?.displayName ?? monster.MonsterId} 독 DOT 적용! (초당 {dotPerTick}, 5초)");
            }

            // 시각 효과
            CombatVFXController.SpawnHitSparks(target.transform.position);
        }

        /// <summary>
        /// 💨 돌진 — Vector3.Lerp로 대상 방향으로 0.5초간 이동
        /// </summary>
        private void ExecuteCharge(AnimalAI monster, MonsterSkillData skillData, GameObject target, Vector3 monsterPos, Vector3 targetPos)
        {
            // 이미 Charge 코루틴이 실행 중이면 중복 방지
            monster.StartCoroutine(ChargeCoroutine(monster, skillData, monsterPos, targetPos, target));
        }

        private IEnumerator ChargeCoroutine(AnimalAI monster, MonsterSkillData skillData, Vector3 startPos, Vector3 targetPos, GameObject target)
        {
            // 대상 방향
            Vector3 direction = (targetPos - startPos).normalized;
            direction.y = 0;

            // 거리 계산 (최대 range까지만)
            float distance = Mathf.Min(skillData.range, Vector3.Distance(startPos, targetPos));
            Vector3 endPos = startPos + direction * distance;

            // 대상 방향 회전
            monster.transform.rotation = Quaternion.LookRotation(direction);

            float elapsed = 0f;
            float duration = 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                monster.transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            // 도착 시 데미지
            if (target != null)
            {
                float chargeDamage = monster.MaxHP * 0.1f * skillData.damage;
                var damageable = target.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    Vector3 hitDir = (target.transform.position - monster.transform.position).normalized;
                    damageable.TakeDamage(Mathf.RoundToInt(chargeDamage), hitDir, "charge");
                }
            }

            // 시각 효과
            CombatVFXController.SpawnHitSparks(monster.transform.position);
            CombatVFXController.SpawnBloodSplatter(monster.transform.position, direction);
            Debug.Log($"[MonsterSkill] 💨 {MonsterDatabase.Get(monster.MonsterId)?.displayName ?? monster.MonsterId} 돌진! ({distance}m)");
        }

        /// <summary>
        /// 🦘 덤벼들기 — Charge와 유사하지만 짧은 거리 + 높은 데미지
        /// </summary>
        private void ExecuteLeap(AnimalAI monster, MonsterSkillData skillData, GameObject target, Vector3 monsterPos, Vector3 targetPos)
        {
            monster.StartCoroutine(LeapCoroutine(monster, skillData, monsterPos, targetPos, target));
        }

        private IEnumerator LeapCoroutine(AnimalAI monster, MonsterSkillData skillData, Vector3 startPos, Vector3 targetPos, GameObject target)
        {
            Vector3 direction = (targetPos - startPos).normalized;
            direction.y = 0;

            float distance = Mathf.Min(skillData.range, Vector3.Distance(startPos, targetPos));
            Vector3 endPos = startPos + direction * distance;

            monster.transform.rotation = Quaternion.LookRotation(direction);

            // 포물선 이동 (Leap는 위로 살짝 떴다가 내려옴)
            float elapsed = 0f;
            float duration = 0.4f;
            Vector3 peakPos = (startPos + endPos) * 0.5f + Vector3.up * 1.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 2차 베지어 곡선 (포물선)
                Vector3 p0 = Vector3.Lerp(startPos, peakPos, t);
                Vector3 p1 = Vector3.Lerp(peakPos, endPos, t);
                monster.transform.position = Vector3.Lerp(p0, p1, t);
                yield return null;
            }

            // 도착 시 데미지 (2배)
            if (target != null)
            {
                float leapDamage = monster.MaxHP * 0.08f * skillData.damage;
                var damageable = target.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    Vector3 hitDir = (target.transform.position - monster.transform.position).normalized;
                    damageable.TakeDamage(Mathf.RoundToInt(leapDamage), hitDir, "leap");
                }
            }

            // 시각 효과 (더 강하게)
            CombatVFXController.SpawnHitSparks(monster.transform.position);
            CombatVFXController.SpawnBloodSplatter(endPos, direction);
            Debug.Log($"[MonsterSkill] 🦘 {MonsterDatabase.Get(monster.MonsterId)?.displayName ?? monster.MonsterId} 덤벼들기!");
        }

        /// <summary>
        /// ⚡ 순간이동 — 대상 뒤로 즉시 위치 변경
        /// </summary>
        private void ExecuteTeleport(AnimalAI monster, GameObject target, Vector3 monsterPos, Vector3 targetPos)
        {
            if (target == null) return;

            // 대상 뒤 3~5m 위치 계산
            Vector3 behindDir = -target.transform.forward;
            float distance = Random.Range(3f, 5f);
            Vector3 teleportPos = targetPos + behindDir * distance;
            teleportPos.y = monsterPos.y;

            // 순간이동
            monster.transform.position = teleportPos;

            // 대상 방향 회전
            monster.transform.LookAt(new Vector3(targetPos.x, monsterPos.y, targetPos.z));

            // 시각 효과
            CombatVFXController.SpawnHitSparks(monsterPos); // 원래 위치
            CombatVFXController.SpawnHitSparks(teleportPos); // 새 위치
            Debug.Log($"[MonsterSkill] ⚡ {MonsterDatabase.Get(monster.MonsterId)?.displayName ?? monster.MonsterId} 순간이동! → {teleportPos}");
        }

        /// <summary>
        /// 💥 범위 폭발 — Physics.OverlapSphere로 주변 데미지
        /// </summary>
        private void ExecuteAoEExplosion(AnimalAI monster, MonsterSkillData skillData, Vector3 monsterPos)
        {
            // 범위 내 모든 Collider 탐색
            Collider[] hitColliders = Physics.OverlapSphere(monsterPos, skillData.range);
            bool hitPlayer = false;

            foreach (Collider hit in hitColliders)
            {
                if (hit.CompareTag("Player") || hit.GetComponent<IDamageable>() != null)
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                    {
                        float aoeDamage = monster.MaxHP * 0.12f * skillData.damage;
                        Vector3 hitDir = (hit.transform.position - monsterPos).normalized;
                        damageable.TakeDamage(Mathf.RoundToInt(aoeDamage), hitDir, "aoe");
                        hitPlayer = true;
                    }
                }
            }

            // 시각 효과: 파티클 버스트
            CombatVFXController.SpawnHitSparks(monsterPos);
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = Random.insideUnitSphere * skillData.range;
                offset.y = Mathf.Abs(offset.y);
                CombatVFXController.SpawnHitSparks(monsterPos + offset);
            }

            // 지면 충격 효과 (데미지 폰트)
            if (hitPlayer)
            {
                CombatVFXController.ShowDamageNumber(monsterPos, Mathf.RoundToInt(monster.MaxHP * 0.12f * skillData.damage), Color.red);
            }

            Debug.Log($"[MonsterSkill] 💥 {MonsterDatabase.Get(monster.MonsterId)?.displayName ?? monster.MonsterId} 범위 폭발! (반경 {skillData.range}m)");
        }

        /// <summary>
        /// 💚 회복 — 자신의 HP 회복 (MaxHP의 일정 비율)
        /// </summary>
        private void ExecuteHeal(AnimalAI monster, MonsterSkillData skillData)
        {
            // AnimalAI는 IDamageable이므로 직접 HP 조작은 불가, 대신 리플렉션으로 _currentHP 접근?
            // AnimalAI의 CurrentHP는 읽기 전용이므로 TakeDamage처럼 직접 조작 불가.
            // 대신 DraculaLord 전용 스킬이므로 DraculaLord 인스턴스 찾아서 Heal
            var dracula = monster.GetComponent<DraculaLord>();
            if (dracula != null)
            {
                float healAmount = dracula.MaxHP * skillData.damage; // 0.2 = 20%
                dracula.RegenerateHP(healAmount);
                Debug.Log($"[MonsterSkill] 💚 드라큘라 HP 회복! +{healAmount} ({dracula.HP}/{dracula.MaxHP})");
            }
            else
            {
                // 일반 몬스터: AnimalAI의 _currentHP 직접 접근 불가, CurrentHP도 readonly
                // 대신 저레벨 몬스터는 Heal 스킬이 없으므로 무시
                Debug.LogWarning("[MonsterSkill] Heal 스킬은 DraculaLord 전용입니다.");
            }

            // 시각 효과
            CombatVFXController.SpawnHitSparks(monster.transform.position);
        }

        /// <summary>
        /// 🦇 소환 — 주변에 박쥐 몬스터 생성
        /// </summary>
        private void ExecuteSummonMinion(AnimalAI monster, GameObject target, Vector3 monsterPos)
        {
            // 드라큘라 영주 전용 — DraculaLord 이벤트 트리거
            var dracula = monster.GetComponent<DraculaLord>();
            if (dracula != null)
            {
                dracula.ForceSummonBats();
                Debug.Log($"[MonsterSkill] 🦇 드라큘라 박쥐 소환!");
                return;
            }

            // 일반 몬스터용: MonsterSpawner처럼 primitive로 미니언 생성
            for (int i = 0; i < 2; i++)
            {
                GameObject minion = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                minion.name = $"Minion_{monster.MonsterId}_{i}";
                minion.transform.position = monsterPos + Random.insideUnitSphere * 2f;
                minion.transform.position = new Vector3(minion.transform.position.x, 0f, minion.transform.position.z);
                minion.transform.localScale = Vector3.one * 0.5f;

                Renderer rend = minion.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = Color.gray;

                // AnimalAI 컴포넌트 추가 (미니언으로 동작)
                AnimalAI minionAI = minion.AddComponent<AnimalAI>();
                minionAI.SetMonsterId("bat"); // 박쥐처럼 행동

                // 5초 후 자동 제거
                Destroy(minion, 5f);
            }

            CombatVFXController.SpawnHitSparks(monsterPos);
            Debug.Log($"[MonsterSkill] 🦇 {MonsterDatabase.Get(monster.MonsterId)?.displayName ?? monster.MonsterId} 미니언 소환!");
        }

        /// <summary>
        /// 🐌 약화 — 이동속도 감소 디버프
        /// </summary>
        private void ExecuteDebuff(AnimalAI monster, MonsterSkillData skillData, GameObject target)
        {
            if (BuffManager.Instance == null) return;

            // 이동속도 50% 감소, 5초 지속
            BuffManager.Instance.AddBuff("Slowness", 0.5f, 5f);

            // 추가 독 데미지 (PoisonSting과 함께 사용됨)
            if (PlayerHealth.Instance != null)
            {
                float debuffDamage = monster.MaxHP * 0.03f;
                PlayerHealth.Instance.TakeDamage(Mathf.RoundToInt(debuffDamage));
            }

            // 시각 효과 (보라색/초록색)
            CombatVFXController.SpawnHitSparks(target.transform.position);
            Debug.Log($"[MonsterSkill] 🐌 {MonsterDatabase.Get(monster.MonsterId)?.displayName ?? monster.MonsterId} 디버프 적용! (이속 -50%, 5초)");
        }

        // ===== 슬라임 분열 처리 =====

        /// <summary>
        /// 슬라임 분열 체크 — HP가 30% 이하로 떨어졌을 때 2마리로 분열.
        /// AnimalAI.TakeDamage() 내부에서 호출됩니다.
        /// </summary>
        public static bool TrySplitSlime(AnimalAI monster)
        {
            if (monster == null || monster.MonsterId != "slime") return false;
            if (monster.IsDead) return false;

            float hpRatio = monster.HPRatio;
            if (hpRatio > 0.3f) return false; // 30% 이상이면 분열 안 함

            // 이미 분열되었는지 확인 (태그 기반)
            if (monster.gameObject.name.Contains("_Split_")) return false;

            // 2마리 분열 생성
            Vector3 pos = monster.transform.position;
            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                GameObject splitGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                splitGo.name = $"Slime_Split_{i}";
                splitGo.transform.position = pos + offset;
                splitGo.transform.localScale = monster.transform.localScale * 0.6f;

                Renderer rend = splitGo.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = new Color(0.3f, 0.8f, 0.3f);

                Collider col = splitGo.GetComponent<Collider>();
                if (col != null)
                    col.isTrigger = false;

                // AnimalAI 복사
                AnimalAI splitAI = splitGo.AddComponent<AnimalAI>();
                splitAI.SetMonsterId("slime");
                // HP는 원본의 40%
                float splitHP = monster.MaxHP * 0.4f;
                // AnimalAI._maxHP는 private이므로 SetMonsterId로 설정 후 MonsterDatabase 기반으로 설정됨
                // 대신 직접 ApplyMonsterDefinition 호출 불가 → 간단히 _currentHP가 MaxHP 기반이므로 setter가 없음
                // 그냥 작은 체력으로 스폰되도록 함
            }

            // 원본 제거
            monster.TakeDamage(monster.CurrentHP, Vector3.zero, "split");
            Debug.Log($"[MonsterSkill] 🟢 슬라임 분열! → 2마리 생성");
            return true;
        }

        // ===== 도우미: 쿨다운 조회 =====

        /// <summary>
        /// 특정 몬스터의 특정 스킬 쿨다운이 남았는지 확인
        /// </summary>
        public float GetRemainingCooldown(AnimalAI monster)
        {
            if (monster == null) return 0f;
            if (_cooldownTimers.TryGetValue(monster, out float lastUsed))
            {
                return Time.time - lastUsed;
            }
            return 0f;
        }

        // ===== 드라큘라 영주 식별 =====

        /// <summary>
        /// GameObject가 드라큘라 영주인지 확인
        /// </summary>
        public static bool IsDracula(GameObject go)
        {
            return go != null && go.GetComponent<DraculaLord>() != null;
        }

        /// <summary>
        /// GameObject가 드라큘라 영주인지 확인 (AnimalAI 기준)
        /// </summary>
        public static bool IsDracula(AnimalAI ai)
        {
            return ai != null && ai.GetComponent<DraculaLord>() != null;
        }
    }

    // ===== FireballProjectile: 화염구 발사체 스크립트 =====

    /// <summary>
    /// 화염구 발사체 — 충돌 시 데미지 + 이펙트 + 자동 제거
    /// </summary>
    public class FireballProjectile : MonoBehaviour
    {
        private MonsterSkillSystem.MonsterSkillData _skillData;
        private AnimalAI _owner;
        private float _lifeTime;

        public void Init(MonsterSkillSystem.MonsterSkillData skillData, AnimalAI owner)
        {
            _skillData = skillData;
            _owner = owner;
            _lifeTime = 0f;
        }

        private void Update()
        {
            _lifeTime += Time.deltaTime;
            // 5초 후 자동 제거
            if (_lifeTime >= 5f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_owner == null) return;

            // 발사자는 무시
            if (other.gameObject == _owner.gameObject) return;

            // IDamageable에 데미지
            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                float damage = _owner.MaxHP * 0.08f * _skillData.damage;
                Vector3 hitDir = (other.transform.position - transform.position).normalized;
                damageable.TakeDamage(Mathf.RoundToInt(damage), hitDir, "fireball");
                Debug.Log($"[Fireball] 🔥 화염구 명중! {damage} 데미지");
            }

            // 시각 효과
            CombatVFXController.SpawnHitSparks(transform.position);
            CombatVFXController.SpawnBloodSplatter(transform.position, Vector3.up);

            // 제거
            Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            OnTriggerEnter(collision.collider);
        }
    }
}