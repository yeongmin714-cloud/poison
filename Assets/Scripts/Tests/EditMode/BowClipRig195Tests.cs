using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [195차 P3] 활쏘기 애니 리그·배선 수리 계약 — BowRigFix195 원샷 적용 결과 고정.
    /// 근원(실측): Player_AC.ArcheryShot 모션이 병사 활쏘기.fbx(467cd2e6)로 교차 배선 + 양쪽 FBX가
    /// Generic(animationType: 2) 임포트라 휴머노이드 리타깃 불가 → 상태 재생·시각 변화 0.
    /// 본 테스트는 BowRigFix195.Run195 적용 후의 에셋 상태를 고정한다(체인 순서: 리그 수리 → QA).
    /// </summary>
    public class BowClipRig195Tests
    {
        static string S(string path) => File.ReadAllText(path);

        [Test]
        public void R1_ShotFbxMeta_HumanoidImport()
        {
            Assert.That(S("Assets/플레이어 애니메이션/플레이어 활쏘기.fbx.meta"), Does.Contain("animationType: 3"),
                "[195차 P3] 플레이어 활쏘기.fbx는 Humanoid 임포트여야 한다(제네릭 클립은 휴머노이드 리타깃 불가).");
            Assert.That(S("Assets/Animations/병사 활쏘기.fbx.meta"), Does.Contain("animationType: 3"),
                "[195차 P3] 병사 활쏘기.fbx도 Humanoid 임포트.");
        }

        [Test]
        public void R2_PlayerAC_ArcheryShot_PointsAtPlayerShotFbx()
        {
            string ctrl = S("Assets/Resources/Animation/Controllers/Player_AC.controller");
            Assert.That(ctrl, Does.Contain("772a9018cdb8e5d4b97f98af822798d6"),
                "[195차 P3] Player_AC는 플레이어 활쏘기.fbx(772a9018)를 참조해야 한다.");
            Assert.That(ctrl, Does.Not.Contain("467cd2e6169ce0844b4a2a865e960377"),
                "[195차 P3] 플레이어 컨트롤러에 병사 활쏘기.fbx(467cd2e6) 교차 참조 잔존 금지.");
        }

        [Test]
        public void R3_GuardArcherAC_PointsAtGuardShotFbx()
        {
            string ctrl = S("Assets/Resources/Animation/Controllers/SoldierArcher_AC.controller");
            Assert.That(ctrl, Does.Contain("467cd2e6169ce0844b4a2a865e960377"),
                "[195차 P3] SoldierArcher_AC는 병사 활쏘기.fbx(467cd2e6)를 참조해야 한다.");
        }

        [Test]
        public void R4_RigFixScript_Contract()
        {
            string src = S("Assets/Editor/BowRigFix195.cs");
            Assert.That(src, Does.Contain("(ModelImporterAnimationType)3"),
                "[195차 P3] 리그 전환 경로 존재(Unity 6 Humanoid 열거 멤버=원시값 3 — 정수 캐스팅 계약).");
            Assert.That(src, Does.Contain("RewireMotion(PlayerCtrl, \"ArcheryShot\""),
                "[195차 P3] 플레이어 ArcheryShot 모션 교정 배선.");
            Assert.That(src, Does.Contain("RewireMotion(GuardCtrl, \"Attack\""),
                "[195차 P3] 병사 Attack 모션 교정 배선.");
        }

        [Test]
        public void P205_Probe_Script_Exists()
        {
            Assert.That(System.IO.File.Exists("Assets/Editor/BowProbe205.cs"), "[205차 배치A-3] BowProbe205 프로브 존재.");
            string src = System.IO.File.ReadAllText("Assets/Editor/BowProbe205.cs");
            Assert.That(src, Does.Contain("ArcheryShot"), "[205차 배치A-3] ArcheryShot 실측 배선 핀.");
            Assert.That(src, Does.Contain("humanMotion"), "[205차 배치A-3] humanMotion 로그 핀.");
        }
    }
}
