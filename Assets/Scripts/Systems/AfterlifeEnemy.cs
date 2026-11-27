using ProjectName.Core;
using ProjectName.Core.Utils;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [A2] 이계 적 — 이계 생존 웨이브 전용 적 (SkeletonGuardPlaceholder의 데미지 부재를 보완한 독립 컴포넌트).
    /// - 추격 + 사정거리 내 쿨다운 접촉 공격 (PlayerHealth.TakeDamage)
    /// - IDamageable — 플레이어 무기가 정상 타격 (기존 전투 파이프라인 재사용)
    /// - 시각: 절차 해골 병사(구형 머리/몸통) + 어둠에 맞는 진홍 눈빛 — GLB 의존 없음
    /// - 사망 시 AfterlifeSurvivalSystem에 처치 통보 (수확 골드 집계)
    /// </summary>
    public class AfterlifeEnemy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float _maxHP = 60f;
        [SerializeField] private float _attackDamage = 12f;
        [SerializeField] private float _moveSpeed = 2.6f;
        [SerializeField] private float _detectRange = 60f;   // 이계는 폐쇄 아레나 — 전체 탐지
        [SerializeField] private float _attackRange = 1.8f;
        [SerializeField] private float _attackCooldown = 1.2f;
        [SerializeField] private int _level = 1;

        [Header("Visual")]
        [SerializeField] private Color _bodyColor = new Color(0.16f, 0.14f, 0.18f); // 어두운 보라빛 회색
        [SerializeField] private Color _eyeColor = new Color(1f, 0.25f, 0.2f);      // 진홍 눈빛

        private float _currentHP;
        private float _lastAttackTime;
        private Transform _player;
        private bool _isDead;

        // ==== [95차 F5] 적 공격 텔레그래프 (예고→타격 2페이즈) — 상태는 전부 인스턴스 필드(정적 상태 금지) ====
        // [156차 P5] 이계의 부적인 — 보유 시 +0.1s(런 외부 0 무영향). const→static 프로퍼티(코루틴/Spawn 사용처 시그니처 무변경).
        private static float TelegraphWindupSeconds => 0.5f + AfterlifeCardSystem.TelegraphBonusSeconds;    // 예고 페이즈 길이 (패링 창 0.45s 커버)
        private const float TelegraphScalePulsePeak = 1.12f;  // 선동작 스케일 펄스 첨두치 (1.0→1.12→1.0)
        private bool _telegraphActive;             // 텔레그래프 상태 머신 플래그 (인스턴스)
        private Coroutine _telegraphRoutine;       // 진행 중 예고 코루틴
        private GameObject _telegraphIndicator;    // 발밑 경고 인디케이터(붉은 링 쿼드)
        private Vector3 _telegraphArcDir = Vector3.forward;   // [169-P6] 호 텔레그래프 방향(예고 시작 시점 고정)
        private Vector3 _telegraphBaseScale = Vector3.one; // 펄스 복원용 기저 스케일

        // ==== [무번호 28-M4] 정예 리듬 오라 — 정예(Level >= 5) 생존 중 발밑 리듬 링(1.2s 간격·반경 교대) ====
        private float _nextEliteAuraAt;   // 다음 오라 발화 시각 (Time.time 기준 — 발화 시 += 1.2f 리듬)
        private int _eliteAuraStep;       // 반경 교대 카운터 — 홀/짝 결정론 교대(0.9/0.55)
        private GameObject _eliteRim;     // [무번호 29-M4-2] 정예 오라 림 오버레이(FxRimOverlay 소유 인스턴스)

        public float MaxHP => _maxHP;
        public float CurrentHP => _currentHP;
        public int Level => _level;
        public bool IsDead => _isDead;
        public bool IsAlive => !_isDead;

        /// <summary>웨이브 스폰 직후 외부 스탯 주입 (리플렉션 없는 명시 경로 — SerializeField 기본값 유지용).</summary>
        public void Configure(float maxHP, float damage, float speed, int level)
        {
            _maxHP = Mathf.Max(1f, maxHP);
            _currentHP = _maxHP;
            _attackDamage = Mathf.Max(1f, damage);
            _moveSpeed = Mathf.Max(0.5f, speed);
            _level = Mathf.Max(1, level);
        }

        private void Awake()
        {
            _currentHP = _maxHP;
            gameObject.tag = "Untagged";
        }

        private void Start()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null)
                playerGo = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>()?.gameObject;
            if (playerGo != null) _player = playerGo.transform;

            BuildVisual();
        }

        private void BuildVisual()
        {
            // 몸통 (캡슐)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(transform);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.85f, 0.4f);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) UnityEngine.Object.Destroy(bodyCol);
            var bodyRenderer = body.GetComponent<MeshRenderer>();
            if (bodyRenderer != null)
                bodyRenderer.material = MaterialHelper.CreateLitMaterial(_bodyColor, "AfterlifeEnemy_Body");

            // 머리 (구) + 진홍 눈빛(Emissive 유사 — 밝은 색)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(transform);
            head.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            head.transform.localScale = Vector3.one * 0.32f;
            var headCol = head.GetComponent<Collider>();
            if (headCol != null) UnityEngine.Object.Destroy(headCol);
            var headRenderer = head.GetComponent<MeshRenderer>();
            if (headRenderer != null)
            {
                var eyeMat = MaterialHelper.CreateLitMaterial(_eyeColor, "AfterlifeEnemy_Head");
                if (eyeMat.HasProperty("_EmissionColor")) eyeMat.SetColor("_EmissionColor", _eyeColor * 1.5f);
                if (eyeMat.HasProperty("_EmissionColor"))
                {
                    eyeMat.EnableKeyword("_EMISSION");
                }
                headRenderer.material = eyeMat;
            }
        }

        private void Update()
        {
            // [무번호 28-M4] 정예 리듬 오라 — 생존 정예만 리듬 발화(아래 얼리 리턴과 무관한 최우선 게이트).
            // 사망/파괴 시 이 가드로 자동 중단(별도 해제 코드 불요).
            if (Level >= 5 && !_isDead && Time.time >= _nextEliteAuraAt && Application.isPlaying)
                SpawnEliteAuraRing();

            // [무번호 29-M4-2] 정예 오라 림 — 틴트 동기(ElitAura 단일 소스) + 펄스 이징(FXMotion.SpawnPulse — 1.2s 리듬 위상).
            //   림 불가 환경=기존 오라 링만(폴백 계약). 사망/비정예 전환 시 해제(머티리얼 누출 금지).
            if (Level >= 5 && !_isDead && Application.isPlaying)
            {
                if (_eliteRim == null)
                {
                    var rimSmr = GetComponentInChildren<SkinnedMeshRenderer>();
                    if (rimSmr != null) _eliteRim = FxRimOverlay.AttachRim(rimSmr, FXPalette.ElitAura, 2.8f, 0.7f);
                }
                if (_eliteRim != null)
                {
                    float phase = Mathf.Repeat(Time.time / 1.2f, 1f);
                    FxRimOverlay.SetRimIntensity(_eliteRim, 0.5f + 0.5f * FXMotion.SpawnPulse(phase));
                }
            }
            else if (_eliteRim != null)
            {
                FxRimOverlay.Release(_eliteRim); _eliteRim = null;
            }

            if (_isDead || _player == null) return;
            if (AfterlifeAugmentSystem.IsPending) return; // [A3] 3택1 대기 — 아레나 정지(VS식)

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > _detectRange) return;
            if (toPlayer.sqrMagnitude < 0.0001f) return;

            Vector3 dir = toPlayer.normalized;

            if (dist > _attackRange)
            {
                // 추격
                transform.position += dir * _moveSpeed * Time.deltaTime * AfterlifeSkillSystem.EnemySlowFactor * AfterlifeCardSystem.EnemyMoveFactor; // [156차 P5] 시간 찌그러짐+봉인의 쇠사슬 — 적 로컬 계수(전역 시간 무접촉)
                transform.rotation = Quaternion.LookRotation(dir);
            }
            else
            {
                // 공격 — 쿨다운 게이트 (기존 밸런스 무변경)
                if (Time.time - _lastAttackTime >= _attackCooldown)
                {
                    _lastAttackTime = Time.time;
                    // [95차 F5] 즉발 TakeDamage 대신 예고→타격 2페이즈 진입
                    BeginMeleeTelegraph();
                }
            }
        }

        /// <summary>
        /// [무번호 28-M4] 정예 리듬 오라 링 — 정예(Level >= 5) 생존 중 발밑 리듬 링. 간격 1.2s,
        /// 반경은 홀/짝 결정론 교대(0.9/0.55 — counter 필드). elite:true로 SpawnImpactRing
        /// B3 정예 승계 계약 사용(EliteColor 패밀리). 색은 FXPalette.ElitAura 단일 소스.
        /// X2 예산 가드가 동시 상한을 관리(리듬 1.2s 간격은 여유). 기존 TelegraphScalePulse 연출 무손상.
        /// [195차 P1] 링 발화 철거 — 사용자 지시 몬스터 링 시각 폐지(발밑 오라 림 오버레이는 링이 아니므로 무수정).
        ///   리듬 북키핑(step/타이머)만 유지 — 게이트·주기 로직 무손상.
        /// </summary>
        private void SpawnEliteAuraRing()
        {
            // [195차 P1] 오라 링(지면 임팩트 링) 스폰 철거 — 반경 교대/색/승계 인자는 링 전용 인자라 동반 걷어냄.
            _eliteAuraStep++;
            _nextEliteAuraAt += 1.2f;
        }

        /// <summary>예고 페이즈 진입 — 발밑 붉은 링 인디케이터 스폰 + 절차 스케일 펄스 시작. [95차 F5]</summary>
        private void BeginMeleeTelegraph()
        {
            // 이중 진입 방지 — 진행 중 예고가 있으면 안전 정리 후 재시작 (다중 인스턴스 안전)
            CancelMeleeTelegraph();

            _telegraphActive = true;
            _telegraphBaseScale = transform.localScale;

            // ②발밑 경고 인디케이터 — [190차 제거] 몬스터 적색링(공격 텔레그래프 호) 폐지 — 사용자 지시
            //   ("몬스터 적색링은 아직 안 없어진거 같던데"). 스폰 삭제 — _telegraphIndicator 영구 null,
            //   발동 플래시/인디케이터 정리 null 가드 무영향. [169-P6] 호 판정(방향 고정)은 유지.
            Vector3 arcDir = Vector3.forward;
            if (PlayerHealth.Instance != null)
            {
                Vector3 d = PlayerHealth.Instance.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) arcDir = d.normalized;
            }
            _telegraphArcDir = arcDir;
            _telegraphIndicator = null;   // [190차] 적색링 제거 — 스폰 삭제(판정·윈드업 로직 무영향)

            _telegraphRoutine = StartCoroutine(MeleeTelegraphRoutine());
        }

        /// <summary>예고 코루틴 — 0.5s 스케일 펄스(선동작) 후 거리 재확인 타격. 예고 중 사망 시 안전 정리. [95차 F5]</summary>
        private System.Collections.IEnumerator MeleeTelegraphRoutine()
        {
            float elapsed = 0f;
            while (elapsed < TelegraphWindupSeconds)
            {
                if (_isDead)          // 예고 중 사망 — 무타격 보장 후 정리
                {
                    CancelMeleeTelegraph();
                    yield break;
                }

                // ①선동작 — 절차 스케일 펄스 1.0→1.12→1.0 (사인 반파, 애니 에셋/클립 불변)
                float t = Mathf.Clamp01(elapsed / TelegraphWindupSeconds);
                float pulse = 1f + (TelegraphScalePulsePeak - 1f) * Mathf.Sin(t * Mathf.PI);
                transform.localScale = _telegraphBaseScale * pulse;

                elapsed += Time.deltaTime;
                yield return null;
            }

            ConcludeMeleeTelegraph();
        }

        /// <summary>예고 종료 — 플레이어 존재 재확인 후 기존 공격 경로 그대로(AttackPlayer 내 거리 재확인). [95차 F5]</summary>
        private void ConcludeMeleeTelegraph()
        {
            RestoreTelegraphVisuals();
            // [무번호 20-H3] 발동 순간 공격 방향 부채꼴 플래시 — 플레이어 방향 예고(AnimalAI 선례 동일).
            if (_telegraphIndicator != null && PlayerHealth.Instance != null)
            {
                var telegraphComp = _telegraphIndicator.GetComponent<TelegraphArcIndicator>();
                if (telegraphComp != null)
                {
                    Vector3 flashDir = PlayerHealth.Instance.transform.position - transform.position;
                    flashDir.y = 0f;
                    telegraphComp.FlashConclude(flashDir.sqrMagnitude > 0.0001f ? flashDir.normalized : transform.forward);
                }
            }
            AttackPlayer(); // 기존 TakeDamage 시그니처 그대로 — 내부에서 거리 재확인(빈 스윙)
        }

        /// <summary>예고 시각 요소 정리 — 스케일 복원 + 인디케이터 제거 + 상태 초기화(코루틴 정지 포함). [95차 F5]</summary>
        private void CancelMeleeTelegraph()
        {
            if (_telegraphRoutine != null)
            {
                StopCoroutine(_telegraphRoutine);
                _telegraphRoutine = null;
            }
            RestoreTelegraphVisuals();
        }

        /// <summary>선동작 스케일 복원 + 인디케이터 제거 + 플래그 해제 (파괴 진행 중 호출 안전). [95차 F5]</summary>
        private void RestoreTelegraphVisuals()
        {
            _telegraphActive = false;
            _telegraphRoutine = null;
            if (this != null && transform != null)
                transform.localScale = _telegraphBaseScale;
            if (_telegraphIndicator != null)
            {
                Destroy(_telegraphIndicator);
                _telegraphIndicator = null;
            }
        }

        private void AttackPlayer()
        {
            var health = PlayerHealth.Instance;
            if (health == null || health.IsDead) return;
            var dir = (_player.position - transform.position).normalized;
            // [95차 F5] 예고 종료 후 거리 재확인 — 플레이어가 빠졌으면 공격 공백(빈 스윙), 쿨다운/데미지 밸런스 무변경
            Vector3 flatToPlayer = _player.position - transform.position;
            flatToPlayer.y = 0f;
            if (flatToPlayer.magnitude > _attackRange)
            {
                Debug.Log($"[AfterlifeEnemy] 빈 스윙 — 예고 중 플레이어 이탈 (거리 {flatToPlayer.magnitude:F2} > 사정거리 {_attackRange:F2}) [95차 F5]");
                return;
            }
            // [169-P6] 호 밖 이탈 재판정 — 부채꼴 판정(표시=판정 정합).
            if (!TelegraphArcIndicator.IsInFrontArc(transform.position, _telegraphArcDir, _player.position, _attackRange))
            {
                Debug.Log($"[AfterlifeEnemy] 빈 스윙 — 예고 중 호 밖 이탈 [169-P6]");
                return;
            }
            health.TakeDamage(_attackDamage, dir, "afterlife");
        }

        // ===== IDamageable =====

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            if (_isDead) return;
            amount *= AfterlifeCardSystem.DamageMult; // [156차 P4] 무기×패시브 데미지 배율 — 런 외부 1f 무영향
            if (AfterlifeCardSystem.DeathScytheActive)
            {
                bool isElite = _level >= 5; // 정예 판별 — 웨이브 레벨 기반(스폰 level=1+wave×2, wave4부터 정예)
                if (!isElite && UnityEngine.Random.value * 100f < AfterlifeCardSystem.DeathScytheBasePercent)
                {
                    Die(); // 사신 즉사 — 결정론 테스트는 RollDeathScytheInstakill 순수 함수 경유
                    return;
                }
                if (isElite)
                    amount += amount * AfterlifeCardSystem.DeathScytheEliteBonusPercent / 100f; // 정예는 가산 피해
            }
            _currentHP -= amount;
            if (_currentHP <= 0f)
                Die();
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            TakeDamage(damageInfo.amount, damageInfo.knockback.normalized, "melee");
        }

        public void Die()
        {
            if (_isDead) return;
            _isDead = true;
            if (_eliteRim != null) { FxRimOverlay.Release(_eliteRim); _eliteRim = null; }   // [무번호 29-M4-2] 사망 림 해제(누출 금지)

            // [95차 F5] 예고 중 사망 — 진행 중 텔레그래프 안전 정리(코루틴 정지+인디케이터 제거+스케일 복원), 무타격 보장
            CancelMeleeTelegraph();

            CombatLog.AddEntry($"이계의 망자 처치!", LogType.Kill);
            AfterlifeSurvivalSystem.NotifyEnemyKilled(this);

            // 사망 연출 — 가라앉은 뒤 제거 (Destroy는 다음 프레임 안전)
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            enabled = false;
            // [QA 수리] 편집 모드 Destroy 금지 에러 방지 — 즉시 정리
            if (Application.isPlaying) Destroy(gameObject, 2f);
            else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            // [95차 F5] 예고 중 파괴 — 코루틴/인디케이터 안전 정리 (파괴 후에도 링 쿼드 잔존 방지)
            CancelMeleeTelegraph();
        }
    }
}
