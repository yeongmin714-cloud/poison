using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [95차 F5] 적 공격 텔레그래프(예고→타격 2페이즈) — 소스 문자열 어서션(기존 EditMode 테스트 선례: 진실치 마커 검증).
    /// ①텔레그래프 상태 머신: 조건 충족 → 예고 진입(인디케이터 스폰/스케일 펄스 시작) → 거리 재확인 후 TakeDamage / 이탈 시 미호출(빈 스윙)
    /// ②기존 전투 계약 무손상: 쿨다운 게이트/데미지 값/TakeDamage 시그니처/슬로우 디버프/넉백 방향 전달 유지
    /// ③다중 인스턴스 안전: 텔레그래프 상태는 전부 인스턴스 필드(정적 상태 금지)
    /// ④코루틴 안전 정리: Die/OnDestroy 가드
    /// </summary>
    public sealed class AttackTelegraphTests
    {
        private static string Read(string rel) => System.IO.File.ReadAllText(rel);

        // ── AnimalAI: 예고→타격 상태 머신 ──

        [Test]
        public void AnimalAI_Melee_Telegraph_StateMachine_Markers()
        {
            var src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("[95차 F5]"),
                "[95차 F5] 마커 존재");
            Assert.That(src, Does.Contain("private static float TelegraphWindupSeconds => 0.5f + AfterlifeCardSystem.TelegraphBonusSeconds;"),
                "[95차 F5] 예고 페이즈 0.5s (패링 창 0.45s 커버)");
            Assert.That(src, Does.Contain("private const float TelegraphScalePulsePeak = 1.12f;"),
                "[95차 F5] 선동작 스케일 펄스 첨두치 1.12 (1.0→1.12→1.0)");
            Assert.That(src, Does.Contain("Mathf.Sin(t * Mathf.PI)"),
                "[95차 F5] 절차 스케일 펄스 — 사인 반파(애니 에셋/클립 불변, localScale 펄스만)");
            Assert.That(src, Does.Contain("transform.localScale = _telegraphBaseScale * pulse;"),
                "[95차 F5] 펄스는 기저 스케일 배수 — 스폰 스케일 보존");
        }

        [Test]
        public void AnimalAI_TryAttack_Enters_Telegraph_Not_Instant_Damage()
        {
            var src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            // 조건 충족(사정거리 진입+쿨다운 경과) → 즉발 TakeDamage 대신 예고 진입
            Assert.That(src, Does.Contain("BeginMeleeTelegraph(aggroDamageable);"),
                "[95차 F5] 어그로 대상 경로 — 예고 진입");
            Assert.That(src, Does.Contain("BeginMeleeTelegraph(null);"),
                "[95차 F5] 플레이어 경로 — 예고 진입(null=플레이어)");
            // 예고 코루틴: 0.5s 경과 후 결판
            Assert.That(src, Does.Contain("private System.Collections.IEnumerator MeleeTelegraphRoutine()"),
                "[95차 F5] 예고 코루틴 존재");
            Assert.That(src, Does.Contain("while (elapsed < TelegraphWindupSeconds)"),
                "[95차 F5] 예고 시간 만큼만 선동작 지속");
            Assert.That(src, Does.Contain("ConcludeMeleeTelegraph();"),
                "[95차 F5] 예고 종료 → 타격 결판 진입");
        }

        [Test]
        public void AnimalAI_Conclude_Rechecks_Distance_Then_Existing_TakeDamage_Signature()
        {
            var src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            // 거리 재확인 — 사정거리 유지 시에만 타격
            Assert.That(src, Does.Contain("if (playerDist <= _attackRange)"),
                "[95차 F5] 예고 종료 후 플레이어 거리 재확인");
            Assert.That(src, Does.Contain("if (aggroDist <= _attackRange)"),
                "[95차 F5] 예고 종료 후 어그로 대상 거리 재확인");
            // 이탈 시 공격 공백(빈 스윙) — TakeDamage 미호출 경로
            Assert.That(src, Does.Contain("빈 스윙 — 예고 중 플레이어 이탈"),
                "[95차 F5] 플레이어 이탈 시 빈 스윙(공격 공백)");
            Assert.That(src, Does.Contain("빈 스윙 — 예고 중 대상 이탈"),
                "[95차 F5] 어그로 대상 이탈 시 빈 스윙(공격 공백)");
            // 기존 TakeDamage 시그니처 그대로 (80차 넉백 방향 전달 계약 유지)
            Assert.That(src, Does.Contain("PlayerHealth.Instance.TakeDamage(_attackDamage, kbDir, \"melee\");"),
                "기존 계약 유지 — 근접 타격 방향 전달 [80차]");
            Assert.That(src, Does.Contain("aggroTarget.TakeDamage(_attackDamage, hitDirection, \"melee\");"),
                "기존 계약 유지 — 어그로 오버로드 TakeDamage(float, Vector3, string)");
            Assert.That(src, Does.Contain("BuffManager.Instance.AddBuff(\"Slowness\", 0.5f, 5f);"),
                "기존 계약 유지 — 슬로우 디버프 무변경");
        }

        [Test]
        public void AnimalAI_Telegraph_Instance_Only_State_And_Cleanup_Guards()
        {
            var src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            // 다중 인스턴스 안전 — 텔레그래프 상태는 인스턴스 필드만 (정적 상태 금지)
            Assert.That(src, Does.Contain("private bool _telegraphActive;"),
                "[95차 F5] 상태 플래그는 인스턴스 필드");
            Assert.That(src, Does.Contain("private Coroutine _telegraphRoutine;"),
                "[95차 F5] 코루틴 핸들은 인스턴스 필드");
            Assert.That(src, Does.Contain("private GameObject _telegraphIndicator;"),
                "[95차 F5] 인디케이터 참조는 인스턴스 필드");
            Assert.That(src.Contains("static bool _telegraph") || src.Contains("static Coroutine _telegraph") || src.Contains("static GameObject _telegraph"),
                Is.False, "[95차 F5] 정적 텔레그래프 상태 금지");
            // 코루틴 안전 정리 — 사망/파괴 가드
            Assert.That(src, Does.Contain("CancelMeleeTelegraph();"),
                "[95차 F5] 안전 정리 경로 존재");
            Assert.That(src, Does.Contain("StopCoroutine(_telegraphRoutine);"),
                "[95차 F5] 코루틴 정지 정리");
            var dieIdx = src.IndexOf("private void Die()");
            var onDestIdx = src.IndexOf("private void OnDestroy()");
            var dieBlock = src.Substring(dieIdx, onDestIdx - dieIdx);
            Assert.That(dieBlock, Does.Contain("CancelMeleeTelegraph();"),
                "[95차 F5] Die — 예고 중 사망 시 무타격 정리");
            var onDestroyBlock = src.Substring(onDestIdx);
            Assert.That(onDestroyBlock, Does.Contain("CancelMeleeTelegraph();"),
                "[95차 F5] OnDestroy — 파괴 중 인디케이터/코루틴 정리");
        }

        [Test]
        public void AnimalAI_Combat_Contract_Cooldown_Damage_Unchanged()
        {
            var src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("[SerializeField] private float _attackCooldown = 1.5f;"),
                "기존 전투 계약 — 기본 공격 쿨다운 무변경");
            Assert.That(src, Does.Contain("if (Time.time - _lastAttackTime < _attackCooldown) return;"),
                "기존 전투 계약 — 쿨다운 게이트 선두 유지(예고 추가로 인한 게이트 변경 없음)");
            Assert.That(src, Does.Contain("public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = \"melee\")"),
                "기존 전투 계약 — 수격 TakeDamage 시그니처 무변경");
        }

        // ── AfterlifeEnemy: 쿨다운 접촉 공격 텔레그래프 ──

        [Test]
        public void AfterlifeEnemy_Telegraph_StateMachine_Markers()
        {
            var src = Read("Assets/Scripts/Systems/AfterlifeEnemy.cs");
            Assert.That(src, Does.Contain("[95차 F5]"),
                "[95차 F5] 마커 존재");
            Assert.That(src, Does.Contain("private static float TelegraphWindupSeconds => 0.5f + AfterlifeCardSystem.TelegraphBonusSeconds;"),
                "[95차 F5] 예고 페이즈 0.5s");
            Assert.That(src, Does.Contain("BeginMeleeTelegraph();"),
                "[95차 F5] 쿨다운 게이트 통과 후 예고 진입(즉발 AttackPlayer 대체)");
            Assert.That(src, Does.Contain("Mathf.Sin(t * Mathf.PI)"),
                "[95차 F5] 절차 스케일 펄스 — 사인 반파");
            Assert.That(src, Does.Contain("if (flatToPlayer.magnitude > _attackRange)"),
                "[95차 F5] 예고 종료 후 거리 재확인");
            Assert.That(src, Does.Contain("빈 스윙 — 예고 중 플레이어 이탈"),
                "[95차 F5] 이탈 시 빈 스윙(공격 공백)");
            Assert.That(src, Does.Contain("health.TakeDamage(_attackDamage, dir, \"afterlife\");"),
                "기존 계약 유지 — 데미지/무기 타입(\"afterlife\") 시그니처 무변경");
            Assert.That(src, Does.Contain("[SerializeField] private float _attackCooldown = 1.2f;"),
                "기존 전투 계약 — 쿨다운 1.2s 무변경");
            Assert.That(src.Contains("static bool _telegraph") || src.Contains("static Coroutine _telegraph") || src.Contains("static GameObject _telegraph"),
                Is.False, "[95차 F5] 정적 텔레그래프 상태 금지");
        }

        // ── 인디케이터: 붉은 반투명 링 쿼드 펄스 (월드 스페이스, BlobShadow 선례) ──

        [Test]
        public void Indicator_Red_Translucent_Ring_Quad_WorldSpace_With_Fallback()
        {
            var src = Read("Assets/Scripts/Systems/AttackTelegraphIndicator.cs");
            Assert.That(src, Does.Contain("[95차 F5→프리미엄]"),
                "[95차 F5] 마커 존재(프리미엄 확장 표기 포함)");
            Assert.That(src, Does.Contain("PrimitiveType.Quad"),
                "BlobShadow 쿼드 스폰 선례 재사용");
            Assert.That(src, Does.Contain("Quaternion.Euler(90f, 0f, 0f)"),
                "세로 Quad → 수평(발밑 링, BlobShadow 선례 동일 규약)");
            Assert.That(src, Does.Contain("new Color(1f, 0.30f, 0.16f, ALPHA)"),
                "[텔레그래프P1-E] 경고 주황-적 링 컬러(순수 적색 폐기 — 팔레트 조화)");
            Assert.That(src, Does.Contain("ProjectName/TelegraphRing"),
                "[텔레그래프P1] 프리미엄 절차 셰이더 경로(TelegraphRing.shader) 사용");
            Assert.That(src, Does.Contain("SetFloat(\"_Fill01\", t)"),
                "[텔레그래프P1] 방사형 채움 진행(_Fill01 0→1) 갱신");
            Assert.That(src, Does.Contain("Universal Render Pipeline/Unlit"),
                "URP Unlit 투명 머티리얼 (BlobShadow 선례)");
            Assert.That(src, Does.Contain("Unlit/Color"),
                "텍스처 없으면 셰이더 기본 UNIFORM 컬러 쿼드 폴백");
            Assert.That(src, Does.Contain("Object.Destroy(quad.GetComponent<Collider>());"),
                "충돌 없음 — 순수 시각 요소");
            Assert.That(src, Does.Contain("_concluding = true;"),
                "[텔레그래프P1-D] 예고 종료 → 종료 플래시 페이즈 진입");
            Assert.That(src, Does.Contain("Destroy(gameObject); // 플래시 소멸 후 자가 파괴"),
                "종료 플래시(0.12s) 소멸 후 자가 파괴 — 스폰 측 정리 의존 없음");
        }
    }
}
