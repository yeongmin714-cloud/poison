using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [80차] 이름표 2행 정합 — 소스 문자열 어서션(진실치 마커 검증).
    /// ①이름 행/바 행 폭 통일(PlateW 96) — "두 줄 크기가 안맞아 어색" 해소(HUD 영상 실측: 병사 이름 행 114 vs 바 78).
    /// ②사망 유닛 이름표 즉시 숨김 — 빈 공중 부유(최대 0.3s) 제거.
    /// </summary>
    public sealed class NameplatePlateAlignTests
    {
        private const string Src = "Assets/Scripts/UI/Toolkit/NameplateOverlayUTK.cs";

        private static string S() => System.IO.File.ReadAllText(Src);

        [Test]
        public void PlateWidth_Unifies_NameRow_And_BarRow()
        {
            var src = S();
            Assert.That(src, Does.Contain("private const float  PlateW            = 96f;")
                .Or.Contain("private const float PlateW = 96f;"),
                "[80차] PlateW 96 상수 — 두 행 통일 폭");
            Assert.That(src, Does.Contain("NameBadgeReserve"),
                "[80차] 배지 예약 폭 상수 — 몬스터 라벨 채움 계산");
        }

        [Test]
        public void BarRow_Uses_Plateral_Width_Not_Figma78()
        {
            var src = S();
            Assert.That(src, Does.Contain("slot.barWrap.style.width = PlateW;"),
                "[80차] 바 행 폭 = PlateW(구 BarW 78 폐기)");
            Assert.That(src, Does.Not.Contain("slot.barWrap.style.width = BarW;"),
                "[80차] 구 Figma 78 바 폭 잔존 금지");
        }

        [Test]
        public void NameLabel_Width_Plateau_Guard_And_Npc()
        {
            var src = S();
            Assert.That(src, Does.Contain("slot.nameLabel.style.width = PlateW;   // [80차] 이름 행 폭 = 바 행 폭 통일"),
                "[80차] EnsureElements — 라벨 기본 폭 PlateW");
            // 병사/바인드 경로 재설정(풀 재사용 오염 방지)
            Assert.That(src, Does.Contain("slot.nameRow.style.width = PlateW;                 // [80차] 두 행 폭 통일(풀 재사용 오염 방지 재설정)"),
                "[80차] BindGuard — 행/라벨 폭 재설정");
            Assert.That(src, Does.Contain("slot.nameRow.style.width = PlateW;                 // [80차] NPC 이름 pill도 동일 폭(시각 일관성)"),
                "[80차] BindNpc — 행/라벨 폭 재설정");
            // 구 114(SlotW-6) 잔존 금지
            Assert.That(src, Does.Not.Contain("slot.nameLabel.style.width = SlotW - 6f;"),
                "[80차] 구 이름 행 114px 잔존 금지");
        }

        [Test]
        public void Monster_NameRow_FixedWidth_With_Badge_Reserve()
        {
            var src = S();
            Assert.That(src, Does.Contain("slot.nameRow.style.width = PlateW;")
                .And.Contain("slot.nameLabel.style.width = PlateW - NameBadgeReserve;"),
                "[80차] 몬스터 — 행 96 + 라벨 96-24(배지 20+마진4) 채움");
            Assert.That(src, Does.Not.Contain("slot.nameLabel.style.width = StyleKeyword.Auto;"),
                "[80차] 몬스터 라벨 auto 폭 폐기 — 행 폭 들쭉날쭉 원인");
        }

        [Test]
        public void Dead_Unit_Slot_Hides_Immediately()
        {
            var src = S();
            Assert.That(src, Does.Contain("if (!SlotAlive(slot)) { slot.used = false; Hide(slot); return; }"),
                "[80차] 사망/파괴 유닛 — 즉시 Hide(빈 공중 부유 제거)");
        }

        [Test]
        public void Monster_HpFill_Uses_Soldier_Gradient_Contract_179()
        {
            // [무번호 179] 몬스터 체력바 = 병사 체력바 동일 계약 — RefreshMonster의 fill 배경이
            // RefreshGuard와 동일한 병사 그라디언트(청록·초록 + HP<30% 주황·적 경고)를 사용한다.
            var src = S();
            Assert.That(src, Does.Contain(
                    "// [무번호 179] 몬스터 체력바 = 병사 체력바 동일 계약 — 청록·초록 그라디언트 + HP<30% 주황·적 경고"),
                "[179] 몬스터 체력바 병사 동일 계약 마커 주석");
            int monsterIdx = src.IndexOf("private void RefreshMonster", System.StringComparison.Ordinal);
            int refreshNpcIdx = src.IndexOf("private void RefreshNpc", System.StringComparison.Ordinal);
            Assert.That(monsterIdx, Is.GreaterThanOrEqualTo(0), "RefreshMonster 존재");
            Assert.That(refreshNpcIdx, Is.GreaterThan(monsterIdx), "RefreshNpc 순서 확인용");
            string monsterBody = src.Substring(monsterIdx, refreshNpcIdx - monsterIdx);
            // 병사와 동일한 경고 전환 이원화(_gradGuardWarn / _gradGuard)
            Assert.That(monsterBody, Does.Contain("ratio < 0.3f"), "[179] 몬스터 경고 임계 동일(HP<30%)");
            Assert.That(monsterBody, Does.Contain("UTKTextureSafe.ToBackground(_gradGuardWarn)"), "[179] 경고 그라디언트 = 병사 Warn 공용");
            Assert.That(monsterBody, Does.Contain("UTKTextureSafe.ToBackground(_gradGuard)"), "[179] 정상 그라디언트 = 병사 Guard 공용");
            // 구 플랫 적색 잔존 금지
            Assert.That(monsterBody, Does.Not.Contain("0xE5 / 255f"), "[179] 구 플랫 #E53E3E 잔존 금지");
            Assert.That(src, Does.Not.Contain("_gradMonster"), "[179] 소비처 없는 몬스터 적색 그라디언트 사자산 제거");
        }
    }
}
