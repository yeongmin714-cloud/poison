using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 182-P2] Farm Pack 밭 타일 승격 계약 — src기반+리소스 3건(FarmPlotVisualsTests 선례 스타일).
    ///
    ///  ① Field_1_1 GLB 리소스 존재 — Resources.Load<GameObject>("Models/UserProvided/Field_1_1")
    ///     IsNotNull(glTFast 임포트 — herb_* UserProvided 선례 동일 폴더·동일 방식).
    ///  ② 게이트 소스계약 — FarmPlotVisuals.cs에 팩 타일 로드 경로 + Grass=기존 잔디 큐브 유지
    ///     (팩 타일 비표시) + Tilled=큐브·팩 타일 이중 비표시 게이트 존재.
    ///  ③ 폴백 소스계약 — 팩 프리팹 로드 실패(null) 시 기존 큐브 경로 유지(연출 중단 없음 계약).
    ///
    /// EditMode 제약: 기존 테스트 무수정(기존 핀은 Cube 고정 핀이 없어 무충돌 — 실측 완료).
    /// </summary>
    public class FarmPackSoilTests
    {
        // ===== ① Field 타일 GLB 리소스 존재 =====

        [Test]
        public void FieldTileGlb_ResourcesKeyExists()
        {
            var glb = Resources.Load<GameObject>("Models/UserProvided/Field_1_1");
            Assert.IsNotNull(glb,
                "[무번호 182-P2] Farm Pack Field 타일 GLB 리소스 존재 — glTFast 임포트(herb_* 선례 동일 방식)");
        }

        // ===== ② 게이트 소스계약 — 로드 경로 + Grass 큐브 유지/Tilled 이중 비표시 =====

        [Test]
        public void PackTile_GrassKeepsProceduralCube_TilledHidesBoth()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlotVisuals.cs");

            // 팩 밭 타일 로드 경로 — Resources.UserProvided 위치 계약(herb_* 선례 동일)
            Assert.That(src, Does.Contain("Models/UserProvided/Field_1_1"),
                "[무번호 182-P2] 팩 밭 타일 Resources 로드 경로 존재");

            // Grass — 팩 타일 비표시 게이트(기존 잔디 큐브 현상 유지)
            Assert.That(src, Does.Contain("plot.Phase != FarmPlot.CropPhase.Grass"),
                "[무번호 182-P2] Grass에서 팩 타일 비표시 게이트 — 기존 잔디 큐브 현상 유지");

            // Tilled(및 팩 타일 표시 상태) — 기존 큐브 비표시 게이트 확장(큐브·팩 타일 이중 비표시)
            Assert.That(src, Does.Contain("!tilled && !showPackTile"),
                "[무번호 182-P2] 기존 큐브 게이트 확장 — Tilled/팩 표시 상태에서 큐브·팩 타일 이중 비표시(구멍만)");
        }

        // ===== ③ 폴백 소스계약 — 로드 실패 시 기존 큐브 경로 유지 =====

        [Test]
        public void PackTile_MissingResource_FallsBackToCube()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlotVisuals.cs");

            // 로드 실패(null) → 기존 큐브 경로 폴백(연출 중단 없음 계약)
            Assert.That(src, Does.Contain("if (prefab == null) return null;"),
                "[무번호 182-P2] 팩 타일 프리팹 부재 시 null 반환 — 전 상태 기존 큐브 경로 폴백");

            // 팩 타일 부재(null) 시 기존 !tilled 게이트가 그대로 성립하는 확장 구조
            Assert.That(src, Does.Contain("plot._soilVisual.gameObject.SetActive(!tilled && !showPackTile)"),
                "[무번호 182-P2] 팩 타일 부재 시 기존 큐브 경로 유지 — 현상 그대로(폴백 계약)");
        }
    }
}
