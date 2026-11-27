using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [카메라통일/이동통일] 전 씬 조작 통일 계약 — Test_10(PlayerMovement) 단일 경로.
    /// - 카메라: PlayerMovement.ApplyFollowCamera 단일 기록자(기본 피치 57°), 실내 셋업 6종 직교/IndoorCameraFollow 폐기.
    /// - 아레나(ArenaTestSetup)는 사용자 지시(2026-11-07)로 제외 — 기존 직교+IndoorCameraFollow+듀엔 관전 계약 유지(제외 증거 핀).
    /// - Test_23 관람 모드는 관람 전용 예외 유지. 구 실내 전용 이동 컨트롤러는 폐기.
    /// 소스 계약(문자열 어서션) 원칙 — InteriorInteractionTests 선례.
    /// </summary>
    public class SceneControlUnifyTests
    {
        private static string Read(string path) => System.IO.File.ReadAllText(path);

        private static readonly string[] UnifiedSetups =
        {
            "Assets/Scripts/Systems/CaveInteriorTestSetup.cs",
            "Assets/Scripts/Systems/AfterlifeCemeteryTestSetup.cs",
            "Assets/Scripts/Systems/LordPeaceVisitTestSetup.cs",
            "Assets/Scripts/Systems/LordInteractionTestSetup.cs",
            "Assets/Scripts/Systems/IndoorWarTestSetup.cs",
            "Assets/Scripts/Systems/PlayerCastleInteriorPlaytest.cs",
        };

        [Test]
        public void T1_IndoorSetups_UsePlayerMovementCamera_NoOrthoFollow()
        {
            foreach (var path in UnifiedSetups)
            {
                string src = Read(path);
                Assert.That(src, Does.Contain("AddComponent<PlayerMovement>"),
                    $"{path} — [카메라통일 A2] PlayerMovement 부착(카메라 기록자 겸 이동 단일 경로).");
                Assert.That(src, Does.Not.Contain("AddComponent<IndoorCameraFollow>"),
                    $"{path} — [카메라통일 A2] IndoorCameraFollow 부착 폐기(PM 단일 기록자).");
                Assert.That(src, Does.Not.Contain("orthographic = true"),
                    $"{path} — [카메라통일 A2] 직교 카메라 폐기(PM 원근 오빗).");
            }
        }

        [Test]
        public void T2_ArenaExcluded_KeepsOrthoFollowContract()
        {
            // 사용자 지시(2026-11-07): 아레나 외부씬 제외 — 직교+IndoorCameraFollow+듀엔 관전 계약 유지.
            string src = Read("Assets/Scripts/UI/ArenaTestSetup.cs");
            Assert.That(src, Does.Contain("AddComponent<IndoorCameraFollow>"),
                "아레나는 제외 대상 — IndoorCameraFollow 부착 존재가 제외 이행의 증거.");
            Assert.That(src, Does.Contain("AddComponent<PlayerMovement>"),
                "아레나 이동은 기존 PlayerMovement(점프/구르기/커서 조준 기존 발동).");
        }

        [Test]
        public void T3_PlayerMovement_Pitch57_GlobalDefault()
        {
            string src = Read("Assets/Scripts/Systems/PlayerMovement.cs");
            Assert.That(src, Does.Contain("private float _camPitch = 57f;"),
                "[카메라통일 A1] 전 씬 기본 피치 57° — 9/20 실내 예시(50~60°) 정렬, 사용자 확정.");
            Assert.That(src, Does.Not.Contain("_camPitch = 65f"),
                "구 65° 기본값 잔존 금지(각도 변경 무효화 방지).");
        }

        [Test]
        public void T4_LegacyInteriorMovementController_Removed()
        {
            Assert.That(System.IO.File.Exists("Assets/Scripts/Systems/PlayerCastleInteriorTestMovement.cs"), Is.False,
                "[이동통일 B4] 구 실내 전용 이동 컨트롤러 폐기 — PlayerMovement 단일 경로.");
        }

        [Test]
        public void T5_IndoorSceneTransition_NoPmSuppression()
        {
            string src = Read("Assets/Scripts/UI/IndoorSceneTransition.cs");
            Assert.That(src, Does.Not.Contain("CameraFollowSuppressed = true"),
                "[카메라통일 A4] 실내 진입 PM 억제 제거 — PM이 실내에서도 추적.");
            Assert.That(src, Does.Contain("카메라통일 A4"),
                "실내 PM 원근 카메라 거리 계약(진입 11m 세팅/복귀 직전 값 복원) 마커.");
        }

        [Test]
        public void T6_TourMode_Preserved()
        {
            string src = Read("Assets/Scripts/UI/IndoorTourMode.cs");
            Assert.That(src, Does.Contain("ResolvePitch"),
                "[카메라통일 A5] Test_23 관람 모드는 관람 전용 예외 유지(IndoorCameraFollow 경로 보존).");
        }

        [Test]
        public void T7_IndoorPlayBoundsClamp_AttachedInSwappedSetups()
        {
            // [이동통일 B3] 벽 콜라이더 부재 실내 3셋업 — 구 컨트롤러 발지 클램프 계약 승계.
            string clampSrc = Read("Assets/Scripts/Systems/IndoorPlayBoundsClamp.cs");
            Assert.That(clampSrc, Does.Contain("PlayerCastleInteriorBuilder.RoomWidth"),
                "클램프 경계 = 성 전체 발지(RoomWidth 117.6 × RoomDepth 88.2) 계약 승계.");
            foreach (var path in new[]
                     {
                         "Assets/Scripts/Systems/PlayerCastleInteriorPlaytest.cs",
                         "Assets/Scripts/Systems/IndoorWarTestSetup.cs",
                         "Assets/Scripts/Systems/LordInteractionTestSetup.cs",
                     })
            {
                string src = Read(path);
                Assert.That(src, Does.Contain("AddComponent<IndoorPlayBoundsClamp>"),
                    $"{path} — [이동통일 B3] 발지 클램프 부착.");
            }
        }
    }
}
