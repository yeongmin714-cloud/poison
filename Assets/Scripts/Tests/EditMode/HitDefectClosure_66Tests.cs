using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 66] 세션 의존 불투명 파티클 종결 — 테스트 13 실측(웜화이트 계단식 베이지 사각·암흑 사각·
    /// ~8s 순백 서클, 배치 프로브에서는 정상 = 세션 의존 렌더)에 따른 결정론 고정 계약.
    /// </summary>
    public class HitDefectClosure_66Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        [Test]
        public void SoftParticle_66_KeywordRemoved()
        {
            string lib = Read("Assets/Scripts/Systems/FxMaterialLibrary.cs");
            // [66-P1] _SOFTPARTICLES_ON 제거 — 깊이 상태가 세션마다 다른 에디터 Play에서 알파 강제 1(불투명
            //   사각)의 뿌리. CreateSoftParticle은 알파 블렌드만으로 결정론 렌더.
            Assert.That(lib, Does.Not.Contain("_SOFTPARTICLES_ON"), "[66-P1] 소프트 파티클 키워드 소멸(세션 의존 렌더 뿌리).");
            Assert.That(lib, Does.Not.Contain("_CameraFadeParams"), "[66-P1] 깊이 페이드 파라미터 잔재 소멸.");
            // 39차 투명 표준 세트 무손상
            Assert.That(lib, Does.Contain("_SURFACE_TYPE_TRANSPARENT"), "[66] 투명 표준 유지.");
        }

        [Test]
        public void MagicHit_66_InheritedColorLdrCap()
        {
            string src = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            // [66-P2] NormalizeMagicHitMaterials 상속색 saturate 캡 — 에셋 HDR 틴트(2.6×) 승계에 의한
            //   순백 원형판(~8s 화이트 서클) 차단. 54차 알파 캡·65-P2 saturate 캡과 동일 구조 계약.
            Assert.That(src, Does.Contain("col = new Color(Mathf.Clamp01(col.r), Mathf.Clamp01(col.g), Mathf.Clamp01(col.b), Mathf.Clamp01(col.a));"),
                "[66-P2] 상속색 LDR 캡(순백 포화 차단).");
            Assert.That(src, Does.Contain("[무번호 66-P2] 상속색 LDR 캡"), "[66-P2] 근거 주석 보존.");
            Assert.That(src, Does.Not.Contain("new Color(1.0f, 0.85f, 1.2f, 1f)"), "[66-P2] 41-b 틴트 HDR 성분(b=1.2) 소멸.");
        }
    }
}
