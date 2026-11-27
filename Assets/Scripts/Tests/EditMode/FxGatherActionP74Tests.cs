using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 74] 행동 인터페이스 VFX G계 계약테스트 — G1 채널 리듬 확산 링 / G2 벌목 쓰러짐 시퀀스 / G3 완료 파편 폭산.
    /// 계획서: docs/plans/2026-11-18_no74-action-interface-vfx-proposal.md
    /// 소스 계약 패턴(FxEnvReactionP73Tests 선례) — EditMode 안전(런타임 쿼리·스폰 없음).
    /// </summary>
    public class FxGatherActionP74Tests
    {
        private static string Read(string path) => System.IO.File.ReadAllText(path);

        // ── G1 채널 리듬 확산 링 ─────────────────────────────────

        /// <summary>[G1] PlayImpact 직후 링 배선 + 쿨다운 가드 + 종별 색 상수.</summary>
        [Test]
        public void G1_ChannelRippleRing_Contracts()
        {
            var src = Read("Assets/Scripts/Systems/ResourceNode.cs");
            Assert.IsTrue(src.Contains("[무번호 74-G1]"), "G1 마커");
            Assert.IsTrue(src.Contains(
                "ShockwaveRingFX.Spawn(transform.position + Vector3.up * 1f, 0.35f, _isWood ? WoodRippleColor : MineRippleColor, 0.4f);"),
                "Spawn 배선(반경 0.35m·0.4s — 기존 파티클 위 타격 파장)");
            Assert.IsTrue(src.Contains("private static float _lastRippleTime;"),
                "쿨다운 정적 필드(전 노드 공유 — 결정론)");
            Assert.IsTrue(src.Contains("RippleIntervalSec = 0.5f"),
                "리듬 간격 쿨다운 0.5s(X2 링 예산 침범 방지)");
            Assert.IsTrue(src.Contains("new Color(0.78f, 0.62f, 0.42f, 0.5f)"), "Wood 링 색 상수");
            Assert.IsTrue(src.Contains("new Color(0.75f, 0.75f, 0.78f, 0.5f)"), "Mine 링 색 상수");

            // OnChannelImpact 지역 슬라이스 — 기존 PlayImpact 유지 + 링은 그 직후.
            int begin = src.IndexOf("private void OnChannelImpact()");
            Assert.That(begin, Is.GreaterThan(0), "[133차] OnChannelImpact 존재");
            int end = src.IndexOf("private void ApplyWoodSway()", begin);
            string body = end > begin ? src.Substring(begin, end - begin) : src.Substring(begin);
            Assert.That(body, Does.Contain("GatherFeedbackSystem.PlayImpact"), "기존 타격 피드백 유지(133차 무손상)");
            Assert.That(body.IndexOf("GatherFeedbackSystem.PlayImpact"),
                Is.LessThan(body.IndexOf("ShockwaveRingFX.Spawn")), "링은 PlayImpact 직후 배선");
            Assert.That(body, Does.Contain("RippleIntervalSec"), "쿨다운 가드가 채널 타격 핸들러 내 존재");
        }

        // ── G2 벌목 완료 쓰러짐 시퀀스 ────────────────────────────

        /// <summary>[G2] Trigger 배선 + 콜백 내 기존 경로(그루터기→숨김→리스폰) 보존.</summary>
        [Test]
        public void G2_FellSequence_Wiring_Contracts()
        {
            var node = Read("Assets/Scripts/Systems/ResourceNode.cs");
            Assert.IsTrue(node.Contains("[무번호 74-G2]"), "G2 마커");
            Assert.IsTrue(node.Contains(
                "FellTreeSequence.Trigger(_visual, _player != null ? _player.position : transform.position, onComplete:"),
                "Trigger 배선(쓰러짐 방향 기준점 = 플레이어, 부재 시 노드 위치)");

            // 콜백 이동 리팩터링 — EnsureStump는 Trigger 토큰 이후(콜백 내부)로 이동, Invoke(Respawn)도 콜백 안.
            int trigger = node.IndexOf("FellTreeSequence.Trigger(");
            Assert.That(trigger, Is.GreaterThan(0), "Trigger 호출 존재");
            int callbackStump = node.IndexOf("EnsureStump();", trigger);
            Assert.That(callbackStump, Is.GreaterThan(trigger),
                "콜백 내 EnsureStump(그루터기 잔존 계약 — [28차] 무손상)");
            int invoke = node.IndexOf("Invoke(nameof(Respawn), _respawnTime);", callbackStump);
            Assert.That(invoke, Is.GreaterThan(callbackStump),
                "Invoke(Respawn)도 콜백 안으로(리스폰 타이밍 = 쓰러짐 완료 후)");
            Assert.IsTrue(node.Contains("if (this == null) return;"),
                "쓰러짐 중 노드 파괴 방어(fake-null 가드)");
            // 병사 자동 채광 경로 오염 금지 — TryAutoMine 본문에 FellTreeSequence 무배선.
            int autoMine = node.IndexOf("public bool TryAutoMine(");
            int roll = node.IndexOf("public bool TryRollRareBonus(", autoMine);
            string autoBody = roll > autoMine ? node.Substring(autoMine, roll - autoMine) : node.Substring(autoMine);
            Assert.That(autoBody, Does.Not.Contain("FellTreeSequence"),
                "병사 TryAutoMine — 쓰러짐 무배선(플레이어 경로 전용, 130차 선례)");
        }

        /// <summary>[G2] FellTreeSequence 설계 계약 — 고정 시그니처·절차 회전·가속 이징·먼지·자가 파괴.</summary>
        [Test]
        public void G2_FellSequence_Design_Contracts()
        {
            var fell = Read("Assets/Scripts/Systems/FellTreeSequence.cs");
            Assert.IsTrue(fell.Contains("[무번호 74-G2]"), "FellTreeSequence 마커");
            Assert.IsTrue(fell.Contains(
                "public static void Trigger(Transform visual, Vector3 playerPos, Action onComplete)"),
                "고정 시그니처(계획서 설계 준수)");
            Assert.IsTrue(fell.Contains("if (visual == null)"), "visual 부재 안전 폴백(기존 동작 보존)");
            Assert.IsTrue(fell.Contains("Vector3.Cross(Vector3.up, dir)"), "축 = Cross(up, dir)");
            Assert.IsTrue(fell.Contains("Quaternion.AngleAxis"), "절차 회전(물리엔진 미사용)");
            Assert.IsTrue(fell.Contains("FellAngleDeg * (t * t)"), "가속 이징(90°×t²)");
            Assert.IsTrue(fell.Contains("0.9f"), "쓰러짐 소요 0.9s(계획서 수치)");
            Assert.IsTrue(fell.Contains("_baseRotation = visual.rotation;"), "기본 회전 시작 시 캡처");
            Assert.IsTrue(fell.Contains("_visual.rotation = _baseRotation;"), "완료 시 원복(리스폰 직립 보존)");
            Assert.IsTrue(fell.Contains("ShockwaveRingFX.SpawnMoveTrailPuff(groundPos);"), "지면 충격 먼지 1회");
            Assert.IsTrue(fell.Contains("Destroy(gameObject);"), "완료 후 자가 파괴(드라이버 누출 금지)");
            Assert.That(fell, Does.Not.Contain("Rigidbody"), "물리엔진 미사용(FxDeathRelax W2 동일)");
            Assert.That(fell, Does.Not.Contain("Random"), "결정론 — 난수 0건");
            Assert.IsFalse(fell.Contains("_activeRingCount"), "링 예산 카운터 직접 접촉 금지(X2 계약 — Spawn 경유만)");
        }

        // ── G3 완료 파편 폭산 ─────────────────────────────────────

        /// <summary>[G3] intensity 선택 파라미터(기본 1f) + 스폰 지점 배율 + 완료 호출부 1.6f.</summary>
        [Test]
        public void G3_IntensityBurst_Contracts()
        {
            var src = Read("Assets/Scripts/Systems/GatherFeedbackSystem.cs");
            Assert.IsTrue(src.Contains("[무번호 74-G3]"), "G3 마커");
            Assert.IsTrue(src.Contains(
                "public static void PlayImpact(GatherFeedbackKind kind, Vector3 position, bool shake = true, float intensity = 1f)"),
                "선택 파라미터 확장(기본 1f — 기존 호출부 전부 무영향)");
            Assert.IsTrue(src.Contains("SpawnVfx(kind, position, intensity);"), "스폰 지점 배율 전달");
            Assert.IsTrue(src.Contains("main.startSizeMultiplier *= intensity;"), "파편 크기 배율");
            Assert.IsTrue(src.Contains("main.startSpeedMultiplier *= intensity;"), "드리프트 확대 배율");
            Assert.IsTrue(src.Contains("var bursts = new ParticleSystem.Burst[burstCount];"),
                "버스트 수 배율(수 1.6배 등가 — 에셋 무변경, 스폰 인스턴스 한정)");

            var node = Read("Assets/Scripts/Systems/ResourceNode.cs");
            Assert.IsTrue(node.Contains("intensity: 1.6f"),
                "완료 타격 1.6배 전달(채널 리듬 타격은 기본값 유지)");
        }

        // ── 기존 잠금 보존(무손상 회귀 가드) ──────────────────────

        /// <summary>[74] 기존 계약 무손상 — X2 링 카운터 2곳·133차 시그니처 접두·배선 잠금 보존.</summary>
        [Test]
        public void P74_Locks_Preserved()
        {
            var ring = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.AreEqual(2,
                ring.Split(new[] { "_activeRingCount++" }, System.StringSplitOptions.None).Length - 1,
                "X2 증가 지점 2곳 무변경(G1은 Spawn 경유 소비 — 카운터 무접촉)");

            var gather = Read("Assets/Scripts/Systems/GatherFeedbackSystem.cs");
            Assert.IsTrue(gather.Contains(
                "public static void PlayImpact(GatherFeedbackKind kind, Vector3 position, bool shake"),
                "[133차] PlayImpact 시그니처 접두 보존(GatherFeedbackContractTests 잠금 무손상)");

            var node = Read("Assets/Scripts/Systems/ResourceNode.cs");
            Assert.IsTrue(node.Contains("GatherFeedbackSystem.PlayImpact"),
                "[133차] ResourceNode 타격 배선 무손상");
            Assert.IsFalse(node.Contains("MaxConcurrentRings"),
                "X2 상수 무참조(예산 가드는 Spawn 내부만)");
            Assert.IsFalse(node.Contains("_activeRingCount"),
                "X2 카운터 무접촉(G1 — Spawn 경유 소비만)");
        }
    }
}
