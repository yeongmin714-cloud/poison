using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [80차] 플레이어 넉백 — 소스 문자열 어서션(진실치 마커 검증).
    /// ①PlayerHealth.KnockbackRequested 이벤트(구독자 없으면 기존 즉시 Move 폴백)
    /// ②AnimalAI 근접 타격 방향 전달(구: 방향 누락 → 넉백 미발동)
    /// ③이동 스크립트 2종(월드/실내) 구독 + 감속 커브 임펄스 적용
    /// ④무기 타입별 강도 표(기존 계약 유지) — 근접 2 / 가스 3 / 폭발 6
    /// </summary>
    public sealed class PlayerKnockbackTests
    {
        private static string Read(string rel) => System.IO.File.ReadAllText(rel);

        [Test]
        public void PlayerHealth_Has_Knockback_Event_With_Fallback()
        {
            var src = Read("Assets/Scripts/Core/PlayerHealth.cs");
            Assert.That(src, Does.Contain("public static event System.Action<Vector3, float> KnockbackRequested;"),
                "[80차] 넉백 요청 정적 이벤트 — Core→Systems 역참조 회피");
            Assert.That(src, Does.Contain("kbHandler.Invoke(hitDirection.normalized, force);"),
                "[80차] 구독자 있음 — 정규화 방향+강도 전달");
            Assert.That(src, Does.Contain("// 폴백 — 구독자 없음: 기존 즉시 이동 유지"),
                "[80차] 구독자 없음 — 기존 CharacterController 즉시 Move 폴백 유지");
        }

        [Test]
        public void KnockbackForce_Table_Unchanged()
        {
            var src = Read("Assets/Scripts/Core/PlayerHealth.cs");
            Assert.That(src, Does.Contain("if (t.StartsWith(\"explosion\")) return 6f;"),
                "기존 계약 유지 — 폭발 강도 6");
            Assert.That(src, Does.Contain("if (t.StartsWith(\"gas\") || t.StartsWith(\"poison\")) return 3f;"),
                "기존 계약 유지 — 가스 강도 3");
            Assert.That(src, Does.Contain("return 2f;                                      // 근접 등 기존"),
                "기존 계약 유지 — 근접 기본 2");
        }

        [Test]
        public void AnimalAI_Melee_Passes_Hit_Direction()
        {
            var src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("Vector3 playerHitDir = PlayerHealth.Instance.transform.position - transform.position;"),
                "[80차] 공격자→플레이어 방향 산출");
            Assert.That(src, Does.Contain("PlayerHealth.Instance.TakeDamage(_attackDamage, kbDir, \"melee\");"),
                "[80차] 근접 타격에 방향 전달(구: 무방향 TakeDamage(_attackDamage) 폐기)");
            Assert.That(src, Does.Not.Contain("PlayerHealth.Instance.TakeDamage(_attackDamage);"),
                "[80차] 무방향 근접 호출 잔존 금지");
        }

        [Test]
        public void PlayerMovement_Subscribes_And_Applies_Decay_Impulse()
        {
            var src = Read("Assets/Scripts/Systems/PlayerMovement.cs");
            Assert.That(src, Does.Contain("ProjectName.Core.PlayerHealth.KnockbackRequested += HandleKnockbackRequested;")
                .And.Contain("ProjectName.Core.PlayerHealth.KnockbackRequested -= HandleKnockbackRequested;"),
                "[80차] OnEnable/OnDisable 구독·해제 짝(씬 전환 누수 방지)");
            Assert.That(src, Does.Contain("public void AddImpulse(Vector3 direction, float force)"),
                "[80차] 임펄스 주입 공개 API");
            Assert.That(src, Does.Contain("motion += _knockbackDir * (_knockbackSpeed * (_knockbackTimer / KnockbackDuration));"),
                "[80차] MovePlayer — 감속 커브(선형) 가산");
            Assert.That(src, Does.Contain("TickKnockback(Time.deltaTime);"),
                "[80차] 이동 적용 후 타이머 감소");
            Assert.That(src, Does.Contain("private const float KnockbackDuration = 0.25f;")
                .And.Contain("private const float KnockbackSpeedPerForce = 4f;"),
                "[80차] 임펄스 상수 — 0.25s / 강도×4(근접 2→8m/s→약 1m)");
        }

        [Test]
        public void IndoorSetups_UsePlayerMovement_KnockbackCovered()
        {
            // [이동통일 B1] 구 실내 전용 컨트롤러 폐기 — 실내 3셋업도 PlayerMovement 넉백 구독 단일 계약으로 커버.
            string[] setups =
            {
                "Assets/Scripts/Systems/PlayerCastleInteriorPlaytest.cs",
                "Assets/Scripts/Systems/IndoorWarTestSetup.cs",
                "Assets/Scripts/Systems/LordInteractionTestSetup.cs",
            };
            foreach (var path in setups)
            {
                var src = Read(path);
                Assert.That(src, Does.Contain("AddComponent<PlayerMovement>()"),
                    $"{path} — [이동통일 B1] PlayerMovement 부착(넉백 구독은 PM 단일 계약).");
                Assert.That(src, Does.Not.Contain("PlayerCastleInteriorTestMovement"),
                    $"{path} — 구 전용 컨트롤러 잔존 금지(폐기).");
            }
        }
    }
}
