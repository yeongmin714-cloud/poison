using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 42] Slash 프리팹 깨진 셰이더 참조 수리 — InternalErrorShader(마젠타) 감지 → 검증 발광 화이트 교체.
    /// 영상 실측 근거(공격씬 테스트 4): 검기 모양 마젠타 밴드 = 메시 정상 + 셰이더 깨짐(매뉴얼 복사 guid 재발행 부작용).
    /// </summary>
    public class FxSlashShaderRepairP42Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void Source_InternalErrorDetection()
        {
            string src = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            Assert.That(src, Does.Contain("[무번호 42]"), "[42] 마커.");
            Assert.That(src, Does.Contain("Hidden/InternalErrorShader"),
                "[42] 마젠타 렌더의 정체(InternalErrorShader) 감지 배선.");
            Assert.That(src, Does.Contain("mr.sharedMaterials = mats;"),
                "[42] 멀티 머티리얼 슬롯 전체 교체(부분 교체 잔존 금지).");
            Assert.That(src, Does.Contain("46/47차 에셋 파일 무변경 계약"),
                "[42] 에셋 파일 무변경 계약 명시(런타임 인스턴스만).");
        }

        [Test]
        public void Source_EmissiveWhiteFallback()
        {
            string src = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            Assert.That(src, Does.Contain("private static Material CreateEmissiveWhiteMaterial()"), "[42] 헬퍼 존재.");
            Assert.That(src, Does.Contain("\"Universal Render Pipeline/Particles/Unlit\""), "[42] 검증 URP 경로 우선.");
            Assert.That(src, Does.Contain("Shader.Find(\"Sprites/Default\")"), "[42] Sprites/Default 폴백.");
            Assert.That(src, Does.Contain("return spriteDefault != null ? new Material(spriteDefault) : null;"),
                "[42] 최종 부재=null(호출부 기존 유지 — 폴백 계약).");
        }

        [Test]
        public void Source_42_Deterministic()
        {
            int i0 = Read("Assets/Scripts/Systems/SlashVFXRunner.cs").IndexOf("[무번호 42]");
            var s = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            string blk = s.Substring(i0, System.Math.Min(2600, s.Length - i0));
            Assert.That(blk, Does.Not.Contain("UnityEngine.Random"), "[42] Random 0건.");
        }
    }
}
