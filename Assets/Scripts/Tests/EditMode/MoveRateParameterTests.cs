using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [66차] 병사 걷기 배속 정합 계약 — 3종 병사 컨트롤러의 Move state가 MoveRate 파라미터를
    /// 배속으로 사용하려면 컨트롤러 파라미터 목록에 MoveRate(float, 기본 1)가 정의되어 있어야 한다.
    /// Play 실측 결함: m_SpeedParameter: MoveRate만 설정되고 파라미터 정의가 없어
    /// "State Move in Controller SoldierShield_AC uses parameter MoveRate which does not exist" 에러 →
    /// 배속 정규화(61차 의도)가 무효화됨(슬라이드/보행 불일치).
    /// </summary>
    public class MoveRateParameterTests
    {
        private static readonly string[] Controllers =
        {
            "Assets/Resources/Animation/Controllers/SoldierShield_AC.controller",
            "Assets/Resources/Animation/Controllers/SoldierArcher_AC.controller",
            "Assets/Resources/Animation/Controllers/SoldierGreatSword_AC.controller",
        };

        [Test]
        public void SoldierControllers_DeclareMoveRateParameter()
        {
            foreach (var path in Controllers)
            {
                var yaml = System.IO.File.ReadAllText(path);
                Assert.That(yaml, Does.Contain("- m_Name: MoveRate"),
                    $"{path} — MoveRate 파라미터가 m_AnimatorParameters에 정의되어야 한다(부재 시 유니티 에러 + 배속 무효).");
                Assert.That(yaml, Does.Contain("m_SpeedParameter: MoveRate"),
                    $"{path} — Move state가 MoveRate를 배속 파라미터로 사용해야 한다(61차 계약).");
            }
        }

        [Test]
        public void SoldierControllers_MoveStateHasSpeedParameterActive()
        {
            foreach (var path in Controllers)
            {
                var yaml = System.IO.File.ReadAllText(path);
                Assert.That(yaml, Does.Contain("m_SpeedParameterActive: 1"),
                    $"{path} — Move state 배속 파라미터 활성 플래그 유지.");
            }
        }

        [Test]
        public void HumanoidClipDriver_SuppliesMoveRate()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(src, Does.Contain("SetFloat(\"MoveRate\""),
                "드라이버가 이동속도 정규화 배속을 MoveRate로 공급해야 한다(61차 계약).");
        }
    }
}
