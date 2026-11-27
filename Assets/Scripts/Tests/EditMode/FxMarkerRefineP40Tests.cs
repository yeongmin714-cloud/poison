using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 40] 마커/서클 다듬기 — ①데칼 텍스처 바인딩 정밀화(노말맵 회피 — 불투명 사각 뿌리 수리)
    /// ②스무스 매직 서클(픽셀화 보라 서클 교체) + 40 프로브 유지.
    /// </summary>
    public class FxMarkerRefineP40Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void Source_DecalPreciseBinding()
        {
            string src = Read("Assets/Scripts/Systems/CommandMarker.cs");
            // [계약 진화 65-P1] [40] 마커는 데칼 경로 소멸과 함께 폐기 — 아래 폐기 근거 주석으로 승계
            // [계약 진화 65-P1] [40] 바인딩 정밀화는 데칼 경로 폐기로 함께 소멸 — 폐기 사유 주석만 보존
            Assert.That(src, Does.Contain("[무번호 65-P1] TrySetupDecal **삭제**"), "[65-P1] 폐기 근거 주석(58-P2 프로브+테스트12 실측).");
            Assert.That(src, Does.Not.Contain("AddComponent<DecalProjector>"), "[65-P1] 데칼 부착 소멸(검은 사각 뿌리).");
            Assert.That(src, Does.Contain("if (_persistent) SetupPersistent();"), "[65-P1] 쿼드 경로 단일 소스.");
        }

        [Test]
        public void Source_SmoothMagicCircle()
        {
            string lib = Read("Assets/Scripts/Systems/FxMaterialLibrary.cs");
            Assert.That(lib, Does.Contain("[무번호 40] 스무스 매직 서클 텍스처"), "[40] 마커.");
            Assert.That(lib, Does.Contain("public static Texture2D GetSmoothMagicCircle()"), "[40] 진입점.");
            Assert.That(lib, Does.Contain("Mathf.Max(a, 1f - Mathf.Abs(r - 0.92f) / 0.035f)"), "[40] 외곽 링 smoothstep AA.");
            Assert.That(lib, Does.Not.Contain("UnityEngine.Random"), "[40] Random 0건.");
            string runner = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            // [계약 진화 167-A1] [40/40-b/41-b] 스무스 서클 교체·틴트 강등·딥퍼플 직지정 전부 임팩트 경로에서 폐기 —
            // Magic Hit 2 에셋 원형 렌더(사용자 지시 11-21). 텍스처 생성기(FxMaterialLibrary)는 자산으로 보존.
            Assert.That(runner, Does.Not.Contain("FxMaterialLibrary.GetSmoothMagicCircle()"), "[167-A1] [40] 서클 교체 배선 소멸.");
            Assert.That(runner, Does.Not.Contain("Object.Destroy(inst, 4f);"), "[167-A1] [40] 임팩트 경로 클론 파괴 소멸(교체 경로 폐기).");
            Assert.That(runner, Does.Not.Contain("inst.color = new Color(1.0f, 0.85f, 1.0f, 1f);"), "[167-A1] [40-b] 틴트 강등 소멸.");
            Assert.That(runner, Does.Not.Contain("inst.SetColor(\"_BaseColor\", inst.color);"), "[167-A1] [40-b] URP _BaseColor 동기 소멸.");
            Assert.That(runner, Does.Not.Contain("new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.25f, 0.85f, 1f))"), "[167-A1] [41-b] 딥퍼플 직지정 소멸.");
            Assert.That(runner, Does.Contain("[167-A1]"), "[167-A1] 에셋 원형 복원 마커.");
        }
    }
}
