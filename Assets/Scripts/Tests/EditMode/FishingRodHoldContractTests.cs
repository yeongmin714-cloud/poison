using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [165b차] 낚싯대 손장착 배선 계약 — ItemHoldManager per-key 구성(HoldConfig) +
    /// FishingSystem 출조 Hold / 종료 3경로(StopFishing·FinishMinigame·TryCatch) Unhold 배선 소스 어서션
    /// + GblItemIconRenderer 거치대 은닉 재사용.
    /// 행동 레벨 회귀(Hold no-op/교체/상호배타 등)는 기존 HotbarHoldKeyTests가 담당 — 중복 금지.
    /// </summary>
    public class FishingRodHoldContractTests
    {
        // ── [165b차] per-key 홀드 구성 ──

        [Test]
        public void HoldConfig_FishingRod()
        {
            var cfg = ItemHoldManager.GetHoldConfig("fishing_rod");
            Assert.That(cfg.HasValue, Is.True, "낚싯대 per-key 홀드 구성이 등록되어야 한다.");

            Assert.That(cfg.Value.MaxDim, Is.EqualTo(1.0f).Within(0.0001f),
                "0.22m 기본 캡이면 2.227m 낚싯대가 장난감 막대로 축소 — 1.0m 목표 오버라이드.");
            Assert.That(cfg.Value.HideNodeNames, Does.Contain("stick"),
                "Y자 거치대(stick) 노드는 홀드/아이콘 경로에서 은닉해야 한다.");
            Assert.That(cfg.Value.GripAlign, Is.True,
                "propped 사선(26.6°) 베이크 보정 — bounds 그립 정렬 활성.");

            // 미등록 키는 null — 기존 0.22m 규약 유지(오버라이드 없음).
            Assert.That(ItemHoldManager.GetHoldConfig("herb_blue"), Is.Null,
                "미등록 키는 기본 규약 유지 — per-key 오버라이드 없음.");
            Assert.That(ItemHoldManager.GetHideNodeNames("herb_blue"), Is.Null,
                "미등록 키는 은닉 노드 없음.");
        }

        // ── 배선 소스 어서션 (HotbarHoldKeyTests 선례) ──

        [Test]
        public void FishingSystem_StartHoldsRod()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FishingSystem.cs");
            Assert.That(src, Does.Contain("ItemHoldManager.Hold(\"fishing_rod\""),
                "출조 시작(TryStartFishing) 시 낚싯대를 오른손에 홀드해야 한다.");
            Assert.That(src, Does.Contain("[165b차]"),
                "165b차 훅 마커 주석이 있어야 한다.");
        }

        [Test]
        public void FishingSystem_AllExitsUnhold()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FishingSystem.cs");

            // helper 정의는 1회만 (중복 정의 금지).
            int defs = 0, idx = 0;
            while ((idx = src.IndexOf("private static void UnholdRodIfHeld()", idx, System.StringComparison.Ordinal)) >= 0)
            { defs++; idx++; }
            Assert.That(defs, Is.EqualTo(1), "UnholdRodIfHeld helper 정의는 1회만.");

            // 종료 3경로 각 블록 내 호출 — 블록 추출로 정확히 검증.
            var stopBody = Body(src, "public void StopFishing()", "public void CancelFishing()");
            var finishBody = Body(src, "private void FinishMinigame(bool success)", "public void TryCatch()");
            var catchBody = Body(src, "public void TryCatch()", "private static void UnholdRodIfHeld()");

            Assert.That(stopBody, Does.Contain("UnholdRodIfHeld();"),
                "StopFishing(라인 해제 직후)에 Unhold 훅이 있어야 한다.");
            Assert.That(finishBody, Does.Contain("UnholdRodIfHeld();"),
                "FinishMinigame(라인 해제 직후)에 Unhold 훅이 있어야 한다.");
            Assert.That(catchBody, Does.Contain("UnholdRodIfHeld();"),
                "TryCatch(상태 리셋 블록 직후)에 Unhold 훅이 있어야 한다.");

            Assert.That(System.Text.RegularExpressions.Regex.Matches(src, @"UnholdRodIfHeld\(\);").Count,
                Is.EqualTo(3), "Unhold 호출은 3경로 딱 3회.");
        }

        [Test]
        public void IconRenderer_HidesStandNodes()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/GblItemIconRenderer.cs");
            Assert.That(src, Does.Contain("ItemHoldManager.GetHideNodeNames"),
                "아이콘 베이크(TryMount) 경로도 per-key 은닉을 재사용해야 한다 — 아이콘에 거치대 포함 방지.");
        }

        /// <summary>소스에서 startMarker~endMarker 구간(메서드 블록) 추출 — 미발견 시 빈 문자열.</summary>
        private static string Body(string src, string startMarker, string endMarker)
        {
            int start = src.IndexOf(startMarker, System.StringComparison.Ordinal);
            int end = start >= 0 ? src.IndexOf(endMarker, start, System.StringComparison.Ordinal) : -1;
            return start >= 0 && end > start ? src.Substring(start, end - start) : string.Empty;
        }
    }
}
