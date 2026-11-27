using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [191차] 던전 고품질화 계약 — 팩 석재 바닥 타일 통일 + 포탈 계단굴. 소스 문자열 어서션(형제 테스트 관례).
    /// 배경: 사용자 지시 "바닥 지형을 통일하고 / 지하로 가거나 올라올때 계단 지형도 있으면 좋겠어".
    /// 전제 실측: 던전 팩 Floor GLB 전종 2.0×0.127×2.0m — CELL_SIZE(2m) 1:1 정합(팩이 2m 셀 던전용 설계).
    /// 계약:
    ///  ① 바닥 타일 통일 — Floor_1 시리즈 6변형만 사용(Floor_2/4/5 시리즈 잠금 — 통일성 계약).
    ///  ② 결정론 변형 로테이션 — (cell.x*31 + cell.y*17) 공식, Random 0건.
    ///  ③ 돌판 경로 셀 배제 — BFS 흙 낟길 유지(대비 가독성 계약 158차 무손상).
    ///  ④ 스태틱 결합 — StaticBatchingUtility.Combine(드로우콜 통합).
    ///  ⑤ 타일 비콜라이더 — StripColliders, 병합 쿼드 MeshCollider 충돌 단일소스 유지(흙 바닥 계약 무손상).
    ///  ⑥ 계단굴 — 하강포탈 2셀 굴(주바닥 쿼드 제외+굴 바닥 쿼드 y=−1.5+MeshCollider+박스 벽 3면),
    ///     Stairs_1A ×0.5 하위 메시 MeshCollider(CC 접지 이동 가능), 아치 굴 바닥 이전(CreatePortalFrame 5인자).
    ///  ⑦ 상승포탈 무변경 — U 포탈 기하 유지(수직 적층 금지·리빌드 계약 무손상).
    /// </summary>
    public sealed class DungeonQuality191Tests
    {
        private const string SrcBuilder = "Assets/Scripts/Systems/CaveInteriorBuilder.cs";

        private static string S(string path) => System.IO.File.ReadAllText(path);

        // ── ① 바닥 타일 통일 — Floor_1 시리즈 6변형만 ──
        [Test]
        public void Floor_Tiles_Use_Only_Floor1_Series()
        {
            string src = S(SrcBuilder);
            for (int i = 1; i <= 6; i++)
            {
                Assert.That(src, Does.Contain($"\"Floor_1_{i}A\""),
                    $"[191] 바닥 타일 변형 Floor_1_{i}A 등록 존재.");
            }
            Assert.That(src, Does.Not.Contain("\"Floor_2_1A\""),
                "[191] Floor_2 시리즈 미사용 — 통일성 계약(단일 시리즈).");
            Assert.That(src, Does.Not.Contain("\"Floor_4_1\""),
                "[191] Floor_4 시리즈 미사용 — 통일성 계약(단일 시리즈).");
            Assert.That(src, Does.Not.Contain("\"Floor_5_1\""),
                "[191] Floor_5 시리즈 미사용 — 통일성 계약(단일 시리즈).");
        }

        // ── ② 결정론 변형 로테이션 ──
        [Test]
        public void Tile_Variant_Rotation_Is_Deterministic()
        {
            string src = S(SrcBuilder);
            Assert.That(src, Does.Contain("(cell.x * 31 + cell.y * 17)"),
                "[191] 변형 선택 공식 — 셀 좌표 해시 결정론(Random 0건 프로젝트 규약).");
            Assert.That(src, Does.Contain("% CellTileVariantKeys.Length"),
                "[191] 변형 배열 길이 모듈로 순환.");
        }

        // ── ③ 돌판 경로 셀 배제 — 흙 낟길 유지 ──
        [Test]
        public void StonePath_Cells_Skip_Tiles()
        {
            string src = S(SrcBuilder);
            int tileFn = src.IndexOf("private static void CreateCellFloorTiles", System.StringComparison.Ordinal);
            Assert.That(tileFn, Is.GreaterThanOrEqualTo(0), "[191] 타일 생성 함수 존재.");
            string body = src.Substring(tileFn);
            Assert.That(body, Does.Contain("s_pathCells.Contains(cell)"),
                "[191] 돌판 경로(BFS 낟길) 셀은 타일 배제 — 흙 낟길 대비 유지(158차 P3-C 무손상).");
        }

        // ── ④ 스태틱 결합 ──
        [Test]
        public void Floor_Tiles_Static_Batched()
        {
            Assert.That(S(SrcBuilder), Does.Contain("StaticBatchingUtility.Combine(tilesRoot);"),
                "[191] 타일 루트 스태틱 결합 — 드로우콜 통합(벽 런 머징 계열 성능 계약).");
        }

        // ── ⑤ 타일 비콜라이더 + 흙 바닥 단일소스 무손상 ──
        [Test]
        public void Tiles_Are_Visual_Only_And_Dirt_Collider_Contract_Intact()
        {
            string src = S(SrcBuilder);
            int tileFn = src.IndexOf("private static void CreateCellFloorTiles", System.StringComparison.Ordinal);
            int pitFn = src.IndexOf("private static bool CreatePortalStairwell", System.StringComparison.Ordinal);
            string tileBody = pitFn > tileFn ? src.Substring(tileFn, pitFn - tileFn) : src.Substring(tileFn);
            Assert.That(tileBody, Does.Contain("StripColliders(visual);"),
                "[191] 타일 비주얼 전용 — 콜라이더 없음(병합 쿼드 충돌 단일소스).");
            Assert.That(tileBody, Does.Not.Contain("AddComponent<MeshCollider>()"),
                "[191] 타일 경로에 MeshCollider 추가 금지 — 충돌 단일소스 계약.");
            Assert.That(src, Does.Contain("mr.material = CaveMaterialFactory.CreateDirtFloor();"),
                "[156차] 흙 바닥 머티리얼 계약 무손상(타일 아래 베이스).");
            Assert.That(src, Does.Contain("floor.AddComponent<MeshCollider>();"),
                "[156차] 바닥 충돌 단일소스 계약 무손상.");
        }

        // ── ⑥ 계단굴 — 하강포탈 ──
        [Test]
        public void Descend_Portal_Has_Walkable_Stairwell_Pit()
        {
            string src = S(SrcBuilder);
            Assert.That(src, Does.Contain("if (!s_portalPitCells.Contains(cell)) quads.Add(cell);"),
                "[191] 굴 셀은 주바닥 병합 쿼드에서 제외 — 실제 구멍(아래층 연결 지형).");
            Assert.That(src, Does.Contain("pitFloor.AddComponent<MeshCollider>();"),
                "[191] 굴 바닥 쿼드 — MeshCollider(주바닥과 동일 충돌 방식).");
            Assert.That(src, Does.Contain("meshFilter.gameObject.AddComponent<MeshCollider>().sharedMesh = meshFilter.sharedMesh;"),
                "[191] 계단 GLB 하위 메시 MeshCollider — CC 접지 이동 가능(계단 지형 계약).");
            Assert.That(src, Does.Contain("CreatePortalFrame(room, downName, archXz, 90f, archY);"),
                "[191] 하강 아치 굴 바닥 재배치 — CreatePortalFrame y 지정 오버로드 사용.");
            Assert.That(src, Does.Contain("AlignVisualBottomToWorldY(visual, y);"),
                "[191] 굴 아치 밑면 == 굴 바닥 월드 y 정렬.");
        }

        // ── ⑦ 상승포탈 무변경 ──
        [Test]
        public void Ascend_Portal_Geometry_Unchanged()
        {
            string src = S(SrcBuilder);
            Assert.That(src, Does.Contain("CreatePortalFrame(room, upName, CellWorld(s_portalUpCells[0]), 270f);"),
                "[191] 상승포탈 — 기본 y=0 오버로드(기하 무변경 계약).");
        }
    }
}
