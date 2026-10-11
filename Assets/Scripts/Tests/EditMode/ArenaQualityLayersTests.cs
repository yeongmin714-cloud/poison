using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-10 계획 v4 — 아레나 실내 품질 계층 이식] 계약 테스트.
    /// 성 내부 H1/H2/H5에서 승인된 품질 언어(벽 톱면 캡/접지 블롭/마모 스탬프)를
    /// 베팅 로비(ArenaInteriorBuilder)와 투기장(ArenaPitInteriorBuilder)에 동일 적용.
    /// - I1: 벽 톱면 돌 캡(쿼드 벽 4면 + 로비 입구 대체 벽 2개), 렌더러 전용.
    /// - I2: 접지 블롭 섀도우(베팅 데스크/GLB/관중석 발판), 렌더러 전용.
    /// - I3: 마모 스탬프(로비 6/투기장 8 — 무대 위 4+바닥 4, 색 변화 없음).
    /// - I4: IndoorScene 화광(FlameLight) 3개 실시간 섀도우 None — 디더링 근원 제거.
    /// </summary>
    public sealed class ArenaQualityLayersTests
    {
        private const string ScenePath = "Assets/Scenes/IndoorScene.unity";

        // ======================================================================
        // I1 — 베팅 로비 벽 캡
        // ======================================================================

        [Test]
        public void I1_ArenaLobby_WallTopCaps_Exist_RendererOnly()
        {
            GameObject room = ArenaInteriorBuilder.BuildArenaInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                int caps = 0;
                foreach (Transform child in room.transform)
                {
                    if (!child.name.EndsWith("_TopCap")) continue;
                    caps++;
                    Assert.That(child.GetComponent<Collider>(), Is.Null,
                        child.name + " — 벽 캡은 렌더러 전용(무콜라이더)이어야 한다.");
                    var r = child.GetComponent<MeshRenderer>();
                    Assert.That(r, Is.Not.Null, child.name + " — 메시 렌더러 필수.");
                    Assert.That(child.localPosition.y,
                        Is.EqualTo(ArenaInteriorBuilder.ROOM_HEIGHT + 0.075f).Within(0.02f),
                        child.name + " — 캡은 벽 상단(8.0)에 얹혀야 한다.");
                }
                Assert.That(caps, Is.GreaterThanOrEqualTo(5),
                    "쿼드 벽 3면(전/좌/우 — 후면은 입구 개구부) + 입구 대체 벽 2개 캡.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void I1_ArenaPit_WallTopCaps_Exist_RendererOnly()
        {
            GameObject room = ArenaPitInteriorBuilder.BuildArenaPitInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                int caps = 0;
                foreach (Transform child in room.transform)
                {
                    if (!child.name.EndsWith("_TopCap")) continue;
                    caps++;
                    Assert.That(child.GetComponent<Collider>(), Is.Null,
                        child.name + " — 벽 캡은 렌더러 전용이어야 한다.");
                }
                Assert.That(caps, Is.GreaterThanOrEqualTo(4), "투기장 4면 벽 캡.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ======================================================================
        // I2 — 접지 블롭 섀도우
        // ======================================================================

        [Test]
        public void I2_ArenaLobby_DeskBlob_Exists_RendererOnly()
        {
            GameObject room = ArenaInteriorBuilder.BuildArenaInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                Transform blob = room.transform.Find("BlobShadow_-6.0_26.0");
                Assert.That(blob, Is.Not.Null, "베팅 데스크 위치(-6,26) 접지 블롭이 존재해야 한다.");
                Assert.That(blob.GetComponent<Collider>(), Is.Null, "블롭은 콜라이더 없음(시각 전용).");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void I2_ArenaPit_SpectatorSeatBlobs_Exist()
        {
            GameObject room = ArenaPitInteriorBuilder.BuildArenaPitInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                int blobs = 0;
                foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("BlobShadow_")) blobs++;
                    if (t.name == "ArenaBettingDesk" || t.name.StartsWith("ArenaPit_Seat_"))
                        Assert.That(t.GetComponent<MeshRenderer>(), Is.Not.Null);
                }
                Assert.That(blobs, Is.EqualTo(12),
                    "관중석 발판 12개(3링×4면) 접지 블롭 — 떠 있는 판이 아니라 놓인 발판으로 읽히게.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ======================================================================
        // I3 — 마모 스탬프
        // ======================================================================

        [Test]
        public void I3_ArenaLobby_WearStamps_Six_RendererOnly()
        {
            GameObject room = ArenaInteriorBuilder.BuildArenaInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                int wear = 0;
                foreach (Transform child in room.transform)
                {
                    if (!child.name.StartsWith("ArenaWear_")) continue;
                    wear++;
                    Assert.That(child.GetComponent<Collider>(), Is.Null,
                        child.name + " — 마모 스탬프는 렌더러 전용이어야 한다.");
                    Assert.That(child.localPosition.y, Is.EqualTo(0.02f).Within(0.005f));
                }
                Assert.That(wear, Is.EqualTo(6), "로비 마모 스탬프 6장.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void I3_ArenaPit_WearStamps_StageAndFloor()
        {
            GameObject room = ArenaPitInteriorBuilder.BuildArenaPitInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                int stageWear = 0;
                int floorWear = 0;
                foreach (Transform child in room.transform)
                {
                    if (child.name.StartsWith("ArenaPitWearStage_"))
                    {
                        stageWear++;
                        Assert.That(child.localPosition.y, Is.EqualTo(0.17f).Within(0.01f),
                            child.name + " — 무대 위 마모는 무대 톱(0.15) 위 얇은 높이.");
                        // 무대 반경(5m) 내부 유지 — 스탬프가 무대 밖에 떠 있으면 금지.
                        float r = new Vector2(child.localPosition.x, child.localPosition.z).magnitude;
                        Assert.That(r, Is.LessThan(4.6f), child.name + " — 무대 위 스탬프는 반경 내부.");
                    }
                    if (child.name.StartsWith("ArenaPitWearFloor_"))
                    {
                        floorWear++;
                        Assert.That(child.localPosition.y, Is.EqualTo(0.02f).Within(0.005f));
                    }
                    Assert.That(child.name.StartsWith("ZoneMat_") || child.name.EndsWith("_WallAO"),
                        Is.False, "금지 원칙 — 존 매트/벽 AO 재추가 금지.");
                }
                Assert.That(stageWear, Is.EqualTo(4), "무대 위 마모 4장.");
                Assert.That(floorWear, Is.EqualTo(4), "무대 주변 바닥 마모 4장.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ======================================================================
        // I4 — IndoorScene 화광 섀도우 None
        // ======================================================================

        [Test]
        public void I4_IndoorScene_FlameLights_HaveNoRealtimeShadows()
        {
            string src = System.IO.File.ReadAllText(ScenePath);
            int flameCount = 0;
            int noneCount = 0;
            int idx = 0;
            while (true)
            {
                idx = src.IndexOf("m_Name: FlameLight", idx, System.StringComparison.Ordinal);
                if (idx < 0) break;
                flameCount++;
                int shadows = src.IndexOf("m_Shadows:", idx, System.StringComparison.Ordinal);
                Assert.That(shadows, Is.GreaterThanOrEqualTo(0), "FlameLight에 m_Shadows 블록 필요.");
                string seg = src.Substring(shadows, System.Math.Min(80, src.Length - shadows));
                if (seg.Contains("m_Type: 0")) noneCount++;
                else Assert.Fail("FlameLight 실시간 섀도우 잔존(Soft) — 디더링 재발. m_Type: 0 유지 계약.");
                idx += 10;
            }
            Assert.That(flameCount, Is.EqualTo(3), "IndoorScene 화광 3개.");
            Assert.That(noneCount, Is.EqualTo(3), "화광 3개 전부 섀도우 None(실내씬 7/8 점무늬 디더링 제거).");
        }
    }
}
