using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// 월드맵 셀 배경색 계약 테스트 — [2026-10-21 사용자 요구] 동결.
    /// 내 영지(PlayerOwned)=초록, 적 영지(LordOwned)=빨강, 전쟁 중(Contested)=주황,
    /// Unoccupied=난이도색 유지(소유주색 null).
    /// 색상 값은 MapWindow.GetOwnershipBackgroundColor 단일 계약에서만 관리.
    /// </summary>
    public class MapWindowColorContractTests
    {
        [Test]
        public void GetOwnershipBackgroundColor_PlayerOwned_ReturnsGreen()
        {
            Color? c = MapWindow.GetOwnershipBackgroundColor(TerritoryOwnership.PlayerOwned);
            Assert.IsTrue(c.HasValue, "PlayerOwned는 소유주 배경색 지정 필요");
            Assert.AreEqual(0.15f, c.Value.r, 0.001f);
            Assert.AreEqual(0.7f, c.Value.g, 0.001f);
            Assert.AreEqual(0.2f, c.Value.b, 0.001f);
        }

        [Test]
        public void GetOwnershipBackgroundColor_LordOwned_ReturnsRed()
        {
            Color? c = MapWindow.GetOwnershipBackgroundColor(TerritoryOwnership.LordOwned);
            Assert.IsTrue(c.HasValue, "LordOwned는 소유주 배경색 지정 필요");
            Assert.AreEqual(0.75f, c.Value.r, 0.001f);
            Assert.AreEqual(0.12f, c.Value.g, 0.001f);
            Assert.AreEqual(0.12f, c.Value.b, 0.001f);
        }

        [Test]
        public void GetOwnershipBackgroundColor_Contested_ReturnsOrange()
        {
            Color? c = MapWindow.GetOwnershipBackgroundColor(TerritoryOwnership.Contested);
            Assert.IsTrue(c.HasValue, "Contested는 소유주 배경색 지정 필요");
            Assert.AreEqual(0.75f, c.Value.r, 0.001f);
            Assert.AreEqual(0.55f, c.Value.g, 0.001f);
            Assert.AreEqual(0.1f, c.Value.b, 0.001f);
        }

        [Test]
        public void GetOwnershipBackgroundColor_Unoccupied_ReturnsNull_KeepsDifficultyColor()
        {
            Assert.IsFalse(
                MapWindow.GetOwnershipBackgroundColor(TerritoryOwnership.Unoccupied).HasValue,
                "Unoccupied는 난이도 배경색 유지 — 소유주색 미지정(null)");
        }
    }
}
