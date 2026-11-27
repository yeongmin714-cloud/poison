using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [187차 후속] 핫바 폭탄 무장 배선 계약테스트 — 실제 투척은 Play 영역, EditMode는 소스계약만.
    /// 근원: 폭탄 무장 경로가 은퇴한 QuickSlotUI(UtkActive 가드)에만 존재 → UTK 핫바 "들기"로 폭탄을 들어도
    /// 무장되지 않아 좌클릭이 근접공격으로 소비(사용자 실측 — Editor.log [BombArm] 0건).
    ///  ① 들기=무장 — ItemHold case 폭탄 분기에서 BombArmController.Ensure().Arm(item)
    ///  ② 재입력=내려놓기 — ToggleOff case 폭탄 분기에서 Ensure().Disarm()
    ///  ③ 단일 손 홀드 정합 — 무기 장착/비폭탄 홀드 전환 시 폭탄 무장 해제
    ///  ④ 활성 판정 — IsSlotItemActive 폭탄 분기(BombArmController.ArmedBomb.id 동일 시 동일 재입력 토글 트리거)
    ///  ⑤ 폭탄은 모델키 해석 전 분기 — ResolveModelKey 실패로 무장 불가 회귀 방지
    /// </summary>
    public class HotbarBombArmTests
    {
        private const string SrcPath = "Assets/Scripts/UI/Toolkit/HotbarUIUTK.cs";

        [Test]
        public void HotbarBomb_HoldArmsBomb_ToggleOffDisarms()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Contain("BombArmController.Ensure().Arm(item)"),
                "폭탄 들기 = 무장 — ItemHold case 폭탄 분기에서 Arm(좌클릭 투척 연결).");
            Assert.That(src, Does.Contain("BombArmController.Ensure().Disarm()"),
                "폭탄 내려놓기 — ToggleOff case 폭탄 분기에서 Disarm(재입력 토글).");
            Assert.That(src, Does.Contain("bam.ArmedBomb.id == itemId"),
                "활성 판정 — 무장 중 같은 폭탄이면 동일 재입력 토글(ToggleOff) 트리거.");
        }

        [Test]
        public void HotbarBomb_SingleHandConsistency_AutoDisarmOnOtherHolds()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            int disarmCount = CountOccurrences(src, "BombArmController.Instance?.Disarm()");
            Assert.That(disarmCount, Is.GreaterThanOrEqualTo(2),
                "단일 손 홀드 정합 — 무기 장착/비폭탄 홀드 전환 시 폭탄 무장 해제(2소 이상).");
            Assert.That(src, Does.Contain("private static bool IsBombItem(PlayerInventory.ItemData item)"),
                "폭탄 판정 헬퍼(null 안전 래퍼) 존재.");
        }

        [Test]
        public void HotbarBomb_BranchesBeforeModelKeyResolve_NoModelRegression()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            string body = Slice(src, "public void TryActivateHoldSlot(int index, bool fHeld)",
                "/// <summary>[68차 P3] 슬롯 아이템이 현재 활성");
            int bombArm = body.IndexOf("BombArmController.Ensure().Arm(item)", System.StringComparison.Ordinal);
            int modelKey = body.IndexOf("GblItemIconRenderer.ResolveModelKey(item)", System.StringComparison.Ordinal);
            Assert.That(bombArm, Is.GreaterThanOrEqualTo(0), "ItemHold 폭탄 무장 분기 존재.");
            Assert.That(modelKey, Is.GreaterThan(bombArm),
                "폭탄은 모델키 해석 전에 분기 — GLB 부재 모델키 회귀로 무장 불가 방지.");
        }

        /// <summary>구간 추출 헬퍼 — 미발견 시 빈 문자열.</summary>
        private static string Slice(string src, string startMarker, string endMarker)
        {
            int start = src.IndexOf(startMarker, System.StringComparison.Ordinal);
            int end = start >= 0 ? src.IndexOf(endMarker, start, System.StringComparison.Ordinal) : -1;
            return start >= 0 && end > start ? src.Substring(start, end - start) : string.Empty;
        }

        /// <summary>문자열 발생 횟수 카운트 — 다중 소 계약용.</summary>
        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int idx = haystack.IndexOf(needle, System.StringComparison.Ordinal);
            while (idx >= 0)
            {
                count++;
                idx = haystack.IndexOf(needle, idx + needle.Length, System.StringComparison.Ordinal);
            }
            return count;
        }
    }
}
