using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-10 Phase D3 + wood 가시화 수리] 플레이어 성 내부 하프팀버 벽 골조 계약.
    /// - D3(사용자 승인): 벽 중간 수직 기둥 + 천장 연결 상단 보 — 렌더러 전용(무충돌).
    ///   [10-10 2차 피드백] 대각 지지대 제거 확정 — 세로 기둥만, 기둥 폭 0.40.
    /// - D2 잔여 수리: StonePillar도 wood_frame(계획 D2 명시 — 구현 누락분).
    /// - wood 가시화 수리: CreateUnitCubeMesh UV 부재(모든 면이 텍스처 (0,0) 한 텍셀 샘플링)로
    ///   wood_frame을 입혀도 단색 슬래브로 보이던 근본 원인 — UV 부여 계약.
    /// </summary>
    public sealed class PlayerCastleInteriorTimberFrameTests
    {
        private const string BuilderPath = "Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs";

        [Test]
        public void BuilderSource_CallsTimberFrameAfterPortalTrim()
        {
            string src = System.IO.File.ReadAllText(BuilderPath);
            int portalCall = src.IndexOf("AddNonCollidingPortalStonework(room, GetTopologyWalls()", System.StringComparison.Ordinal);
            int timberCall = src.IndexOf("AddTimberWallFrame(room, GetTopologyWalls()", System.StringComparison.Ordinal);
            Assert.That(portalCall, Is.GreaterThanOrEqualTo(0), "포탈 트림 호출부가 존재해야 한다(선례).");
            Assert.That(timberCall, Is.GreaterThan(portalCall),
                "D3 팀버 프레임은 포탈 트림(스케일 패스 후 벽 로컬 미터) 다음에 호출돼야 한다(문설주 선례 패턴).");
        }

        [Test]
        public void BuilderSource_ConvertsStonePillarToWoodFrame()
        {
            // [D2 잔여 수리] 계획 D2가 StonePillar를 wood_frame로 교체하도록 명시 — 구현 누락분 계약.
            string src = System.IO.File.ReadAllText(BuilderPath);
            Assert.That(src, Does.Contain("Material pillarMat = IndoorMaterialFactory.CreateWoodFrame()"),
                "StonePillar 머티리얼은 wood_frame(하프팀버)이어야 한다(계획 D2 범위).");
            Assert.That(src, Does.Not.Contain("pillarMat.color = new Color(0.40f, 0.38f, 0.35f)"),
                "구 회색 석재 기둥 색상 잔여가 없어야 한다.");
        }

        [Test]
        public void BuilderSource_UnitCubeMeshCarriesPerFaceUVs()
        {
            // [wood 가시화 수리] UV 부재 큐브는 wood_frame을 단색으로 렌더링했다 — 면별 UV 0..1 계약.
            string src = System.IO.File.ReadAllText(BuilderPath);
            Assert.That(src, Does.Contain("mesh.SetUVs(0, uvs);"),
                "CreateUnitCubeMesh는 면별 UV를 반드시 기록해야 한다(목재 나뭇결 가시화).");
        }

        [Test]
        public void BuiltInterior_TimberFrameCoversExteriorAndEntranceWalls()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                string[] wallNames = { "Exterior_North", "Exterior_West", "Exterior_East", "Entrance_South" };
                foreach (string wallName in wallNames)
                {
                    Transform frame = room.transform.Find(wallName + "_TimberFrame");
                    Assert.That(frame, Is.Not.Null, wallName + " 벽에 팀버 프레임(수직 기둥+상단 보)이 존재해야 한다(D3).");

                    var filter = frame.GetComponent<MeshFilter>();
                    var renderer = frame.GetComponent<MeshRenderer>();
                    Assert.That(filter, Is.Not.Null, wallName + " 팀버 프레임은 병합 메시(MeshFilter)여야 한다.");
                    Assert.That(renderer, Is.Not.Null, wallName + " 팀버 프레임은 렌더러를 가져야 한다.");
                    Assert.That(filter.sharedMesh, Is.Not.Null);
                    Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(120),
                        wallName + " 팀버 프레임은 기둥+상단 보+지지대 이상의 병합 지오메트리를 가져야 한다.");

                    // 벽 길이 방향(bounds.x)은 벽 길이와 일치, 상단 보까지 y 커버.
                    Vector3 boundsSize = filter.sharedMesh.bounds.size;
                    float wallLength = wallName == "Exterior_West" || wallName == "Exterior_East"
                        ? PlayerCastleInteriorBuilder.RoomDepth
                        : PlayerCastleInteriorBuilder.RoomWidth;
                    Assert.That(boundsSize.x, Is.EqualTo(wallLength).Within(0.8f),
                        wallName + " 상단 보는 벽 전체 길이를 커버해야 한다(기둥 돌출 0.3×2 공차 — 두께 증가).");
                    Assert.That(boundsSize.y, Is.GreaterThanOrEqualTo(PlayerCastleInteriorBuilder.RoomHeight - 0.1f),
                        wallName + " 팀버 프레임은 천장(상단 보 top)까지 올라가야 한다.");
                }
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void BuiltInterior_TimberFramesAreRendererOnly_NoCollider()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                int timberFrames = 0;
                foreach (Transform child in room.transform)
                {
                    if (!child.name.EndsWith("_TimberFrame")) continue;
                    timberFrames++;
                    Assert.That(child.GetComponent<Collider>(), Is.Null,
                        child.name + " — 팀버 프레임은 장식(레이아웃 벽 골조)이라 이동을 절대 막으면 안 된다(무충돌 계약).");
                }
                Assert.That(timberFrames, Is.GreaterThanOrEqualTo(20),
                    "토폴로지 벽(~22개) 대부분에 팀버 프레임이 세워져야 한다(D3).");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void BuiltInterior_TimberFrameKeepsSouthDoorwayClear()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                Transform frame = room.transform.Find("Entrance_South_TimberFrame");
                Assert.That(frame, Is.Not.Null, "남입구 벽 팀버 프레임이 존재해야 한다.");

                Mesh mesh = frame.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                // 남입구 포탈(폭 4m, 중앙)의 기둥 회피 계약 — 포탈 통로(|x|<1.7, 기둥 높이)에
                // 팀버 정점이 들어오면 시각적으로 통로를 막는다(콜라이더가 없어도). 상단 보(y>5.5)는 예외.
                foreach (Vector3 v in mesh.vertices)
                {
                    bool inDoorwayColumn = Mathf.Abs(v.x) < 1.7f && v.y < 5.5f;
                    Assert.That(inDoorwayColumn, Is.False,
                        "남입구 포탈 통로 열에 수직 기둥/지지대 정점이 있으면 안 된다(포탈 통로 보장).");
                }
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void BuiltInterior_InteriorLightingDimmedPerUserRequest()
        {
            // [2026-10-11 20차 예시 정합 ×2 점프] 상한 완화 1.30→2.60
            //   (신규 최대 강도 2.4 = 천장 랜턴/작업풀 — 내부씬 예시 mean 37 목표).
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                foreach (Light light in room.GetComponentsInChildren<Light>(true))
                {
                    Assert.That(light.intensity, Is.LessThanOrEqualTo(2.60f),
                        light.name + " — 10-11 20차 예시 정합 ×2 점프 후 실내 포인트라이트 강도 상한은 2.60이어야 한다.");
                }
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }
    }
}
