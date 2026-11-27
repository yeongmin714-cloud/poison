using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [189차] 아이콘 원격 베이크 자리 분리 계약 — 소스 문자열 어서션(BombExplosionSfx189Tests 관례).
    /// 배경: "아이콘이 초록색으로 깨져서" 뜨는 근원 — InventoryWindow 3D 캐릭터 프리뷰 본체
    /// (Player_Rigged_Heat — 청록 플레이어 모델)가 아이템 아이콘 동기베이크 원격 위치
    /// (10000,1000,10000 — GblItemIconRenderer)과 **동일 좌표를 상주 점유** → 인벤을 열어둔 상태에서
    /// 베이크되는 핫바/인벤 아이콘에 청록 본체가 겹쳐 찍힘(farClip 20 안에 포함).
    /// 계약:
    ///  ① InventoryWindow 프리뷰 본체는 전용 자리 (11000,1000,11000) — 아이템 베이크 자리와 분리.
    ///  ② InventoryWindow에 아이템 베이크 자리 좌표 잔존 0건(재발 방지 핀).
    ///  ③ GblItemIconRenderer는 (10000,1000,10000) 유지 — 관례 좌표 동결 핀.
    ///  ④ GuardIconRenderer는 (10500,1000,10500) 유지 — 3자리 상호 분리 도식 고정.
    /// 격리 논거: 원격 좌표 상호 거리 — 아이템↔본체 1414m / 아이템↔병사 707m / 병사↔본체 707m —
    /// 모든 아이콘 카메라 farClip 20보다 원거리로 겹침 구조적으로 불가.
    /// </summary>
    public sealed class IconBakeSpot189Tests
    {
        private const string SrcInv = "Assets/Scripts/UI/InventoryWindow.cs";
        private const string SrcGbl = "Assets/Scripts/UI/GblItemIconRenderer.cs";
        private const string SrcGuard = "Assets/Scripts/UI/GuardIconRenderer.cs";

        private static string S(string path) => System.IO.File.ReadAllText(path);

        // ── ① 프리뷰 본체 전용 자리 (11000,1000,11000) ──
        [Test]
        public void InventoryPreviewBody_Uses_Dedicated_Remote_Spot()
        {
            Assert.That(S(SrcInv), Does.Contain("new Vector3(11000f, 1000f, 11000f);"),
                "[189] 인벤 프리뷰 본체 전용 원격 자리(11000,1000,11000) 필수 — 아이템 베이크 자리와 분리.");
        }

        // ── ② InventoryWindow에 아이템 베이크 자리 좌표 잔존 0건 ──
        [Test]
        public void InventoryWindow_No_Longer_Occupies_Item_Bake_Spot()
        {
            Assert.That(S(SrcInv), Does.Not.Contain("new Vector3(10000f, 1000f, 10000f);"),
                "[189] InventoryWindow의 (10000,1000,10000) 상주 점유 제거 확인 — 아이콘 겹침 재발 방지 핀.");
        }

        // ── ③ GblItemIconRenderer 관례 좌표 동결 ──
        [Test]
        public void GblItemIconRenderer_Keeps_Convention_Spot()
        {
            Assert.That(S(SrcGbl), Does.Contain("new Vector3(10000f, 1000f, 10000f)"),
                "[189] 아이템 아이콘 베이크 관례 자리(10000,1000,10000) 동결 핀.");
        }

        // ── ④ GuardIconRenderer 병사 촬영 자리 동결 — 3자리 상호 분리 도식 ──
        [Test]
        public void GuardIconRenderer_Keeps_Its_Own_Spot()
        {
            Assert.That(S(SrcGuard), Does.Contain("new Vector3(10500f, 1000f, 10500f)"),
                "[189] 병사 아이콘 촬영 자리(10500,1000,10500) 동결 핀 — 10000/10500/11000 3자리 분리 도식 고정.");
        }
    }
}
