// [수조 통일] IndoorDioramaKit 계약 테스트 — 천장 제거+남측 벽 시각 절단(콜라이더 유지)+멱등+훅 배선.
// 패턴 소스: IndoorMinimapTests(146차 소스계약+행위 혼합), Arena 스크랩 선례.
using System.IO;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class IndoorDioramaKitTests
    {
        private GameObject _room;

        [TearDown]
        public void TearDown()
        {
            if (_room != null) Object.DestroyImmediate(_room);
        }

        /// <summary>미니 테스트 룸 — Floor/Ceiling/Wall_Back(남측)/Wall_Front(북측)/Door 구성(CreateRoom·Barn 이름 계약 준용).</summary>
        private static GameObject BuildTestRoom()
        {
            var room = new GameObject("Room");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(room.transform);
            floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.5f, 6f);

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(room.transform);
            ceiling.transform.localPosition = new Vector3(0f, 3f, 0f);
            ceiling.transform.localScale = new Vector3(8f, 0.2f, 6f);

            var south = GameObject.CreatePrimitive(PrimitiveType.Cube);
            south.name = "Wall_Back";   // 남측(−z) — 카메라 측
            south.transform.SetParent(room.transform);
            south.transform.localPosition = new Vector3(0f, 1.5f, -3f);
            south.transform.localScale = new Vector3(8f, 3f, 0.2f);

            var north = GameObject.CreatePrimitive(PrimitiveType.Cube);
            north.name = "Wall_Front";  // 북측(+z)
            north.transform.SetParent(room.transform);
            north.transform.localPosition = new Vector3(0f, 1.5f, 3f);
            north.transform.localScale = new Vector3(8f, 3f, 0.2f);

            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";         // 남측이지만 이름 게이트 — 유지 대상
            door.transform.SetParent(room.transform);
            door.transform.localPosition = new Vector3(2f, 1.2f, -2.95f);
            door.transform.localScale = new Vector3(1.2f, 2.4f, 0.15f);

            return room;
        }

        [Test]
        public void Apply_RemovesCeiling()
        {
            _room = BuildTestRoom();
            IndoorDioramaKit.Apply(_room);
            Assert.IsNull(_room.transform.Find("Ceiling"), "수조 계약 ① — 천장 제거");
        }

        [Test]
        public void Apply_DisablesSouthWallRenderer_KeepsCollider()
        {
            _room = BuildTestRoom();
            IndoorDioramaKit.Apply(_room);
            Transform south = _room.transform.Find("Wall_Back");
            Assert.IsNotNull(south, "GO 유지 — 파괴가 아니라 시각 절단");
            Assert.IsFalse(south.GetComponent<Renderer>().enabled, "수조 계약 ② — 남측 벽 시각 절단");
            Collider col = south.GetComponent<Collider>();
            Assert.IsNotNull(col, "벽 콜라이더 존재");
            Assert.IsTrue(col.enabled, "콜라이더 유지 계약 — 탈출 불가");
            Assert.IsTrue(south.gameObject.activeSelf, "GO 활성 유지 — SetActive(false) 금지");
        }

        [Test]
        public void Apply_KeepsNorthWallAndDoor()
        {
            _room = BuildTestRoom();
            IndoorDioramaKit.Apply(_room);
            Assert.IsTrue(_room.transform.Find("Wall_Front").GetComponent<Renderer>().enabled,
                "북벽 유지 — 디오라마 배경 벽");
            Assert.IsTrue(_room.transform.Find("Door").GetComponent<Renderer>().enabled,
                "수조 계약 ④ — 문 시각 유지(이름 게이트, 남측이라도)");
        }

        [Test]
        public void Apply_IsIdempotent()
        {
            _room = BuildTestRoom();
            IndoorDioramaKit.Apply(_room);
            Assert.DoesNotThrow(() => IndoorDioramaKit.Apply(_room), "멱등 — 재호출 안전");
            Assert.IsFalse(_room.transform.Find("Wall_Back").GetComponent<Renderer>().enabled,
                "재호출 후에도 절단 상태 유지");
        }

        [Test]
        public void Apply_NullRoomOrCutOff_NoThrow()
        {
            Assert.DoesNotThrow(() => IndoorDioramaKit.Apply(null), "null 룸 무음");
            _room = BuildTestRoom();
            Assert.DoesNotThrow(() => IndoorDioramaKit.Apply(_room, cutSouthWalls: false));
            Assert.IsTrue(_room.transform.Find("Wall_Back").GetComponent<Renderer>().enabled,
                "cutSouthWalls=false — 벽 유지(관전/특수 룸 경로)");
            Assert.IsNull(_room.transform.Find("Ceiling"), "cutSouth=false여도 천장 제거는 수행");
        }

        // ── 소스계약 ──

        [Test]
        public void SourceContract_KitShape()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/IndoorDioramaKit.cs");
            StringAssert.Contains("namespace ProjectName.Systems", src);
            StringAssert.Contains("public static void Apply(GameObject room, bool cutSouthWalls = true)", src,
                "시그니처 계약 — 빌더 훅 1줄 호출 형태");
            StringAssert.Contains("r.enabled = false", src, "시각 절단 방식 계약");
            StringAssert.DoesNotContain(".SetActive(false)", src,
                "콜라이더 유지 계약 — GO 비활성 금지");
            StringAssert.Contains("DestroyImmediate", src, "천장 제거 동프레임 계약(여관/성 선례)");
            StringAssert.Contains("catch (System.Exception", src, "실패 무음 계약 — 크래시 금지");
        }

        [Test]
        public void SourceContract_HookWiring_AllTargetsOneLine()
        {
            string[] hooked =
            {
                "Assets/Scripts/Systems/HouseInteriorBuilder.cs",
                "Assets/Scripts/UI/ShopInteriorBuilder.cs",
                "Assets/Scripts/UI/ChurchInteriorBuilder.cs",
                "Assets/Scripts/UI/CraftHouseInteriorBuilder.cs",
                "Assets/Scripts/Systems/RestaurantInteriorBuilder.cs",
                "Assets/Scripts/Systems/TavernInteriorBuilder.cs",
                "Assets/Scripts/Systems/BarnInteriorBuilder.cs",
                "Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs",
            };
            foreach (string f in hooked)
                StringAssert.Contains("IndoorDioramaKit.Apply(", File.ReadAllText(f), f + " 훅 배선");
        }

        [Test]
        public void SourceContract_Exclusions_AreWired()
        {
            StringAssert.DoesNotContain("IndoorDioramaKit",
                File.ReadAllText("Assets/Scripts/Systems/CaveInteriorBuilder.cs"),
                "던전 제외 확정 — 호출부 부재");
            StringAssert.DoesNotContain("IndoorDioramaKit",
                File.ReadAllText("Assets/Scripts/Systems/AfterlifeInteriorBuilder.cs"),
                "이계 제외 확정 — 호출부 부재");
            StringAssert.DoesNotContain("IndoorDioramaKit",
                File.ReadAllText("Assets/Scripts/Systems/ArenaPitInteriorBuilder.cs"),
                "투기장 관중석 제외(관전 ortho 특수) — 호출부 부재");
            StringAssert.DoesNotContain("IndoorDioramaKit",
                File.ReadAllText("Assets/Scripts/Systems/ArenaInteriorBuilder.cs"),
                "여관 제외(이미 수조 정합 — 중복 훅 금지) — 호출부 부재");
        }
    }
}
