using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;   // SelectionRingController

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [67차] 푸른 선택 링 자동 소멸 계약 — 선택 직후 표시 → 수명 경과 후 페이드 소멸.
    /// 사용자 요구: "이동 UI의 푸른 링이 계속 남아있지 않고, 처음만 표시하고 시간이 지나면 자동 소멸".
    /// 공격 타겟 링(AttackTargetRingManager)은 autoFadeOut 기본값 false로 상시 유지 보호.
    /// </summary>
    public class SelectionRingFadeTests
    {
        [Test]
        public void AutoFade_DefaultOff_ProtectsAttackTargetRings()
        {
            var go = new GameObject("ring_probe");
            var ring = go.AddComponent<SelectionRingController>();
            Assert.That(ring.autoFadeOut, Is.False,
                "autoFadeOut 기본값 false — 공격 타겟 링 등 상시 링이 자동 소멸되면 안 된다.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Defaults_68thCycle_AutoFadeOff_Lifetime1s_Fade03()
        {
            // [68차 P2] 기본값 규약 — 선택 링(명시 세팅 경유)은 1초 유지 → 0.3s 페이드,
            //   컨트롤러 기본 autoFadeOut=false는 공격 타겟 링 상시 유지 보호(무영향).
            var go = new GameObject("ring_probe");
            var ring = go.AddComponent<SelectionRingController>();
            Assert.That(ring.autoFadeOut, Is.False,
                "기본 autoFadeOut=false — 공격 타겟 링 무영향 유지.");
            Assert.That(ring.lifetimeSeconds, Is.EqualTo(1f),
                "68차: 선택 링 표시 유지 5s → 1s.");
            Assert.That(ring.fadeSeconds, Is.EqualTo(0.3f),
                "68차: 페이드 0.8s → 0.3s.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Ring_FadesOut_AfterLifetime()
        {
            var go = new GameObject("ring_probe");
            var ring = go.AddComponent<SelectionRingController>();
            go.transform.localScale = Vector3.one;   // [EditMode] AddComponent Awake 미호출 — 시작 스케일 수동 시드
            ring.autoFadeOut = true;
            ring.lifetimeSeconds = 2f;
            ring.fadeSeconds = 1f;

            // 수명 내 — 소멸 없음(스케일 유지)
            ring.UpdateVisual(1f);
            ring.UpdateVisual(0.9f);
            Assert.That(go.transform.localScale, Is.Not.EqualTo(Vector3.zero),
                "수명 경과 전에는 링이 유지되어야 한다.");

            // 수명 경과 → 페이드 진입 → 축소
            ring.UpdateVisual(0.2f);   // elapsed 2.1s → 페이드 시작
            Vector3 fadeStart = go.transform.localScale;
            // 페이드 진입 후 축소 확인 — 완료/소멸은 Expired 테스트에서 별도 검증
            ring.UpdateVisual(0.5f);
            Vector3 midScale = go.transform.localScale;
            Assert.That(midScale.magnitude, Is.LessThan(fadeStart.magnitude),
                "페이드 중 스케일이 축소되어야 한다.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Expired_FiresExactlyOnce()
        {
            var go = new GameObject("ring_probe");
            var ring = go.AddComponent<SelectionRingController>();
            go.transform.localScale = Vector3.one;   // [EditMode] AddComponent Awake 미호출 — 시작 스케일 수동 시드
            ring.autoFadeOut = true;
            ring.lifetimeSeconds = 0f;
            ring.fadeSeconds = 0f;

            int fired = 0;
            ring.Expired += () => fired++;

            // 즉시 수명 도달 → 페이드 진입(첫 틱) → 다음 틱에 페이드 완료(fadeSeconds=0 → 즉완) → Expired 1회
            ring.UpdateVisual(0.016f);
            ring.UpdateVisual(0.016f);

            Assert.That(fired, Is.EqualTo(1), "Expired는 정확히 1회만 발화해야 한다.");
            Assert.That(go.transform.localScale.magnitude, Is.LessThan(0.0001f),
                "소멸 완료 시 링이 완전히 축소되어야 한다(편집 모드 — 파괴 대신 0스케일).");
            Object.DestroyImmediate(go);   // 정리 — 편집 모드에선 내부 파괴가 없으므로 여기서 수거
        }

        [Test]
        public void Manager_GatesRecreation_UntilReselection()
        {
            // 소스 계약 어서션(HudBottomAnchorTests 선례) — 자동 소멸 후 무한 재생성 차단.
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardSelectionManager.cs");
            Assert.That(src, Does.Contain("_ringShown.Contains(g)"),
                "동기화 루프는 만료 게이트로 링 재생성을 건너뛰어야 한다.");
            Assert.That(src, Does.Contain("_ringShown.Clear()"),
                "선택 해제 시 게이트를 초기화해 재선택 시 링이 다시 표시되어야 한다.");
            Assert.That(src, Does.Contain("ring.autoFadeOut = true"),
                "선택 링 생성 시 자동 소멸을 활성화해야 한다.");
            Assert.That(src, Does.Contain("ring.lifetimeSeconds = 1f"),
                "68차: 선택 링 표시 유지 1초(5s에서 단축).");
            Assert.That(src, Does.Contain("ring.fadeSeconds = 0.3f"),
                "68차: 소멸 페이드 0.3초.");
        }
    }
}
