using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [고스트 체력바] GhostHpDisplay 결정론 EditMode 테스트 (78차).
    /// 검증: 타격 지연+서서히 추격 / 앞줄 즉시 / 힐 즉시 동기 / 추격 중 앞줄 이탈 금지 /
    ///   연속 타격 누적 / Reset 동기화 / dt 0 무시.
    /// </summary>
    public sealed class GhostHpDisplayTests
    {
        [Test]
        public void Drop_FrontInstant_TrailHoldsThenCatches()
        {
            var g = new GhostHpDisplay();

            g.Tick(0.8f, 0.016f);
            Assert.AreEqual(0.8f, g.Front, 0.0001f, "앞줄 — 타격 즉시");
            Assert.AreEqual(1f, g.Trail, 0.0001f, "뒷줄 — 지연 동안 이전 값 유지");

            // 지연(0.35s) 동안 유지
            for (int i = 0; i < 10; i++) g.Tick(0.8f, 0.016f);
            Assert.AreEqual(1f, g.Trail, 0.0001f, "지연 0.16s 경과 — 여전히 유지");

            // 지연 경과 후 — 서서히 감소
            for (int i = 0; i < 30; i++) g.Tick(0.8f, 0.016f); // 누적 ~0.64s — 지연 종료
            Assert.Less(g.Trail, 1f, "지연 종료 — 추격 시작");
            Assert.GreaterOrEqual(g.Trail, 0.8f, "뒷줄은 앞줄 아래로 못 내려감");

            // 충분한 시간 — 수렴
            for (int i = 0; i < 400; i++) g.Tick(0.8f, 0.016f);
            Assert.AreEqual(0.8f, g.Trail, 0.003f, "추격 완료 — 앞줄과 일치");
        }

        [Test]
        public void Heal_TrailSyncsInstantly()
        {
            var g = new GhostHpDisplay();
            g.Tick(0.5f, 0.016f);
            // 추격 중이어도
            g.Tick(0.9f, 0.016f);
            Assert.AreEqual(0.9f, g.Front, 0.0001f, "앞줄 즉시 상승");
            Assert.AreEqual(0.9f, g.Trail, 0.0001f, "힐 — 뒷줄 지연 없이 동기화");
        }

        [Test]
        public void RepeatedHits_TrailAccumulates_NeverBelowFront()
        {
            var g = new GhostHpDisplay();
            float dt = 0.016f;
            // 연속 타격(짧은 간격 — 지연 내) 시뮬레이션 — 1.0 → 0.9 → 0.7 → 0.4
            for (int i = 0; i < 5; i++) g.Tick(0.9f, dt);
            for (int i = 0; i < 5; i++) g.Tick(0.7f, dt);
            for (int i = 0; i < 5; i++) g.Tick(0.4f, dt);

            Assert.AreEqual(0.4f, g.Front, 0.0001f, "앞줄 최종값");
            Assert.GreaterOrEqual(g.Trail, 0.4f, "뒷줄 ≥ 앞줄");
            Assert.Greater(g.Trail, 0.4f, "누적 흔적 — 아직 추격 중");
            // 최종 수렴
            for (int i = 0; i < 600; i++) g.Tick(0.4f, dt);
            Assert.AreEqual(0.4f, g.Trail, 0.003f, "최종 수렴");
        }

        [Test]
        public void Reset_SyncsBoth_NoDelay()
        {
            var g = new GhostHpDisplay();
            g.Tick(0.3f, 0.016f);
            g.Reset(1f);
            Assert.AreEqual(1f, g.Front, 0.0001f);
            Assert.AreEqual(1f, g.Trail, 0.0001f, "대상 교체 — 즉시 동기");
        }

        [Test]
        public void Tick_ZeroDt_SyncsFrontOnly()
        {
            var g = new GhostHpDisplay();
            g.Tick(0.7f, 0f);
            Assert.AreEqual(0.7f, g.Front, 0.0001f, "dt 0 — 앞줄 동기화");
            Assert.AreEqual(1f, g.Trail, 0.0001f, "dt 0 — 뒷줄 무이동(일시정지)");
        }

        [Test]
        public void Tick_ClampsTarget()
        {
            var g = new GhostHpDisplay();
            g.Tick(-0.5f, 0.016f);
            Assert.AreEqual(0f, g.Front, 0.0001f, "음수 클램프");
            g.Reset(1f);
            g.Tick(1.5f, 0.016f);
            Assert.AreEqual(1f, g.Front, 0.0001f, "1 초과 클램프");
        }

        [Test]
        public void IsAnimating_TracksCatchup()
        {
            var g = new GhostHpDisplay();
            g.Tick(0.5f, 0.016f);
            Assert.IsTrue(g.IsAnimating, "지연 유지 중 — 애니메이션 중");
            for (int i = 0; i < 800; i++) g.Tick(0.5f, 0.016f);
            Assert.IsFalse(g.IsAnimating, "수렴 — 애니메이션 종료");
        }
    }
}
