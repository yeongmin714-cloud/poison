using NUnit.Framework;
using System.IO;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [146차 P1] 동굴 P1 수리 3건 소스계약 — CaveLevelDirector 티어 동기화 /
    /// CaveInteriorBuilder 스폰 스케일·위치 / PlayerMovement 실내 접지 게이트.
    /// EditMode 소스 어서션 원칙(CaveInteriorSceneTests 선례) — 런타임 검증은 Play 게이트(부모 몫).
    /// 계약 축: ①SyncContext 존재 + 빌더 Core 초반 호출(부트 2인자 경유 누락 수리)
    /// ②ClampToGroundByHeight/ApplySlopeAlignment 실내 게이트(월드 공식 클램프 금지)
    /// ③GetFloorSpawnPosition 2인자 시그니처 + [147차 P-B0→148차 P2] tierScale 고정값 2(방 ×2, 면적 4배) + F2 스폰-포탈 이격(재상승 바운스 차단).
    /// </summary>
    public sealed class Cave146P1ContractTests
    {
        private const string DirectorPath = "Assets/Scripts/Systems/CaveLevelDirector.cs";
        private const string BuilderPath = "Assets/Scripts/Systems/CaveInteriorBuilder.cs";
        private const string PlayerMovementPath = "Assets/Scripts/Systems/PlayerMovement.cs";

        // ── 1. SyncContext 존재 + 빌더 Core 초반 호출 ──

        [Test]
        public void Director_HasSyncContext_And_BuilderCore_CallsIt_Early()
        {
            string director = ReadSource(DirectorPath);
            Assert.That(director, Does.Contain("internal static void SyncContext(string territoryId, int tier, int floor)"),
                "CaveLevelDirector.SyncContext 3인자 시그니처 존재 — 부트(Test_21) 2인자 직접 빌드 경유의 티어 컨텍스트 누락 수리 계약.");
            Assert.That(director, Does.Contain("CurrentFloor = Mathf.Clamp(floor, MinFloor, MaxFloor);"),
                "SyncContext는 floor를 Clamp 세팅(멱등 계약 — 동일 인자 재호출 상태 불변).");

            string builder = ReadSource(BuilderPath);
            int layout = builder.IndexOf("ApplyFloorLayout(floorIndex)");
            int sync = builder.IndexOf("CaveLevelDirector.SyncContext(territoryId, tier, floorIndex + 1);");
            Assert.That(layout, Is.GreaterThanOrEqualTo(0), "빌더 Core의 ApplyFloorLayout 호출 존재(초반 앵커).");
            Assert.That(sync, Is.GreaterThan(layout),
                "SyncContext 호출은 ApplyFloorLayout 직후 Core 초반에 위치한다(빌드마다 재동기화 계약).");
        }

        // ── 2. 실내 접지 게이트 — 월드 지형 공식 클램프 금지 ──

        [Test]
        public void PlayerMovement_GatesGroundFormulaClamps_OnIndoorActive()
        {
            string src = ReadSource(PlayerMovementPath);
            const string gate = "if (ProjectName.Core.UITransitionState.IndoorActive) return;";
            Assert.That(CountOccurrences(src, gate), Is.EqualTo(2),
                "ClampToGroundByHeight/ApplySlopeAlignment 두 경로 모두 실내 게이트(월드=false 무영향 계약).");

            // 게이트는 컨트롤러 null 가드 직후(메서드 최상단) — 후속 가드보다 앞서는 순서 계약.
            int clamp = src.IndexOf("private void ClampToGroundByHeight()");
            int slope = src.IndexOf("private void ApplySlopeAlignment()");
            Assert.That(clamp, Is.GreaterThanOrEqualTo(0), "ClampToGroundByHeight 메서드 존재.");
            Assert.That(slope, Is.GreaterThan(clamp), "ApplySlopeAlignment 메서드 존재(ClampToGroundByHeight 이후).");
            Assert.That(src.IndexOf(gate, clamp), Is.LessThan(src.IndexOf("if (_isJumping) return;", clamp)),
                "ClampToGroundByHeight 게이트는 점프 가드보다 앞서 최상단에 위치한다.");
            Assert.That(src.IndexOf(gate, slope),
                Is.LessThan(src.IndexOf("if (_isJumping || !_isGrounded || _smoothedPlanarSpeed < 0.1f)", slope)),
                "ApplySlopeAlignment 게이트는 개입조건 가드보다 앞서 최상단에 위치한다.");
        }

        // ── 3. F2 스폰 — [147차 P-B0] tierScale 고정 + 상승 포탈 이격 ──

        [Test]
        public void FloorSpawn_TwoArg_CellmapDirect_And_F2Spawn_ClearsUpPortal()
        {
            string builder = ReadSource(BuilderPath);
            int sig = builder.IndexOf("public static Vector3 GetFloorSpawnPosition(int floor, int tier)");
            Assert.That(sig, Is.GreaterThanOrEqualTo(0), "2인자 GetFloorSpawnPosition 존재(P9 1단 계약).");
            string body = builder.Substring(sig);

            Assert.That(body, Does.Contain("tier = InteriorRandomizer.ClampTier(tier);"),
                "tier 클램프 적용(요터 래퍼 단일 소스 계약 — [147차 P-B0] 고정 후에도 파라미터 검증 유지).");
            // [156차 셀맵] tierScale 폐기 — 크기는 셀맵에 내재화(본 명세 변경, 사유: 직각 꺽임 재설계).
            Assert.That(body.Contains("tierScale"), Is.False,
                "[156차 셀맵] GetFloorSpawnPosition 본문에 tierScale 잔존 금지 — 셀맵 좌표 직접 반환.");
            Assert.That(body.Contains("1f + 0.25f * (tier - 1)"), Is.False,
                "[147차 P-B0] 구 ×1.25 스케일 수식 잔존 금지 — 기하 적용 폐기 계약.");
            Assert.That(body, Does.Contain("new Vector3(-36f, 1.5f, -6f)"),
                "F2 스폰 셀 (6,15) → 월드 (−36, 1.5, −6) — [156차 셀맵] 스폰 테이블.");
            Assert.That(body.Contains("new Vector3(-8.0f, 1.5f, 0.6f)"), Is.False,
                "구 F2 좌표 (−8.0, 1.5, 0.6) 잔존 금지 — 상승 포탈 판정 반경 내부 착지 원인 제거.");

            // 이격 근거 고정 — F2 상승 포탈 셀 (2,15) → 월드 (−44, −6), 스폰(−36, −6)과 x축 8.0m > 판정 반경 2.2m.
            Assert.That(body, Does.Contain("8.0m 이격"),
                "[156차 셀맵] F2 스폰-포탈 이격 근거 주석 고정(146차 이격 계약 승계).");
            Assert.That(builder, Does.Contain("GetFloorSpawnPosition(floor, 1)"),
                "1인자 오버로드는 tier=1 위임 유지(시그니처 계약 — 호출부 전환은 Director 측).");

            // Director 텔레포트 경로 — 1인자 고정 위임이 아닌 _tier 동행 2인자 호출.
            string director = ReadSource(DirectorPath);
            Assert.That(director, Does.Contain("GetFloorSpawnPosition(CurrentFloor, _tier)"),
                "TeleportPlayerToFloorSpawn은 (CurrentFloor, _tier) 2인자 호출 — 층 스폰 스케일 동행 계약.");
        }

        // ── 공용 헬퍼 ──

        private static string ReadSource(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"소스 파일 존재: {path}");
            return File.ReadAllText(path);
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(token, idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += token.Length;
            }
            return count;
        }
    }
}
