using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-D] 세밀 묘사 셰이더 자산 계약 — D1 절차 플립북/D2 소프트 파티클/D3 프레넬 림.
    /// Source 잠금 + 리소스 존재 검증. Random 0건, 폴백 경로 보존, 머티리얼 누출 금지.
    /// </summary>
    public class FxShaderAssetsDTests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── T1. D1 플립북 생성기 — 결정론(해시 노이즈) + 캐시 + Random 0건 ──
        [Test]
        public void Source_D1_FlipbookDeterministic()
        {
            string src = Read("Assets/Scripts/Systems/FxFlipbookGenerator.cs");
            Assert.That(src, Does.Contain("[무번호 29-D1]"), "[29-D1] 마커.");
            Assert.That(src, Does.Contain("public static Texture2D GetSmokeFlipbook()"),
                "[29-D1] 플립북 아틀라스 진입점.");
            Assert.That(src, Does.Contain("private static Texture2D _smoke;"),
                "[29-D1] 생성 1회 캐시(재생성 금지).");
            Assert.That(src, Does.Contain("43758.5453f"),
                "[29-D1] 해시 기반 value noise(156차 GetStarTexture 결정론 선례).");
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[29-D1] Random 0건 — 결정론 계약.");
            Assert.That(src, Does.Contain("TextureFormat.RGBA32, false"),
                "[29-D1] mipChain=false(파티클 용도 — 141차 생성해상도 계약 계열).");
        }

        // ── T2. D2 소프트 파티클 빌더 — URP 우선 + Sprites/Default 폴백 보존 ──
        [Test]
        public void Source_D2_SoftParticleFallback()
        {
            string src = Read("Assets/Scripts/Systems/FxMaterialLibrary.cs");
            Assert.That(src, Does.Contain("[무번호 29-D2"), "[29-D2] 마커.");
            Assert.That(src, Does.Contain("\"Universal Render Pipeline/Particles/Unlit\""),
                "[29-D2] URP 파티클 셰이더 우선.");
            // [계약 진화 66-P1] 소프트 파티클 키워드 **폐기** — 깊이 상태가 세션마다 다른 에디터 Play에서
            //   알파 강제 1(불투명 사각)의 뿌리(테스트 12·13 실측). 알파 블렌드만으로 결정론 렌더 고정.
            Assert.That(src, Does.Not.Contain("_SOFTPARTICLES_ON"),
                "[66-P1] 소프트 파티클 키워드 소멸(세션 의존 불투명 렌더 뿌리 제거).");
            Assert.That(src, Does.Contain("new Material(Shader.Find(\"Sprites/Default\"))"),
                "[29-D2] 폴백 경로 보존 — 셰이더 미발견 환경 무손상(계획 주의항).");
            Assert.That(src, Does.Contain("HasProperty"),
                "[29-D2] 속성 부재 방어(비URP 변형 환경 폴백).");
        }

        // ── T3. D2 스왑 — ShockwaveRingFX 먼지/불티 경로 교체 + EnsurePuffTexture 잠금 보존 ──
        [Test]
        public void Source_D2_SwapSites()
        {
            string src = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.That(src, Does.Contain("FxMaterialLibrary.CreateSoftParticle(\"DustPuffMat\""),
                "[29-D2] 먼지 머티리얼이 소프트 파티클 빌더 경유.");
            Assert.That(src, Does.Contain("FxMaterialLibrary.CreateSoftParticle(\"ScorchEmberMat\""),
                "[29-D2] 불티 머티리얼이 소프트 파티클 빌더 경유.");
            Assert.That(src, Does.Contain("private static Texture2D EnsurePuffTexture()"),
                "[29-D2] 기존 잠금(PhaseRKLM T) 보존 — 텍스처 소스 유지.");
            Assert.That(src, Does.Contain("[무번호 29-D2]"), "[29-D2] 스왑 마커.");
        }

        // ── T4. D3 프레넬 림 셰이더 — URP 파이프라인 태그 + 가산 블렌드 + CBuffer ──
        [Test]
        public void Source_D3_FresnelShaderAsset()
        {
            string src = Read("Assets/Shaders/FxFresnelRim.shader");
            Assert.That(src, Does.Contain("Shader \"ProjectName/FX/FresnelRim\""), "[29-D3] 셰이더 이름 계약.");
            Assert.That(src, Does.Contain("\"RenderPipeline\" = \"UniversalPipeline\""), "[29-D3] URP 파이프라인.");
            Assert.That(src, Does.Contain("Blend SrcAlpha One"), "[29-D3] 가산 블렌드.");
            Assert.That(src, Does.Contain("ZWrite Off"), "[29-D3] 깊이 쓰기 off(오버레이).");
            Assert.That(src, Does.Contain("_RimColor"), "[29-D3] 림 색 속성.");
            Assert.That(src, Does.Contain("CBUFFER_START(UnityPerMaterial)"), "[29-D3] SRP Batcher 호환.");
            Assert.That(src, Does.Contain("com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"),
                "[29-D3] URP Core.hlsl 포함.");
        }

        // ── T5. D3 림 오버레이 유틸 — 본 공유 + 인스턴스 소유 + 폴백 null ──
        [Test]
        public void Source_D3_RimOverlayUtility()
        {
            string src = Read("Assets/Scripts/Systems/FxRimOverlay.cs");
            Assert.That(src, Does.Contain("[무번호 29-D3]"), "[29-D3] 마커.");
            Assert.That(src, Does.Contain("smr.bones = source.bones;"),
                "[29-D3] 본 계열 공유(스키닝 동기).");
            Assert.That(src, Does.Contain("smr.rootBone = source.rootBone;"),
                "[29-D3] rootBone 공유.");
            Assert.That(Read("Assets/Scripts/Systems/FxMaterialLibrary.cs"), Does.Contain("if (sh == null) return null;"),
                "[29-D3] 림 셰이더 미지원=null 폴백 계약(호출부 기존 emissive 유지) — 빌더 측 계약.");
            Assert.That(src, Does.Contain("DestroyImmediate(smr.sharedMaterial);"),
                "[29-D3] 머티리얼 인스턴스 해제(누출 금지 — EditMode 가드 21-K1 선례).");
            Assert.That(src, Does.Contain("UnityEngine.Rendering.ShadowCastingMode.Off"),
                "[29-D3] 그림자 비활성(순수 발광 오버레이).");
        }

        // ── T6. 신규 3파일 전체 결정론 스윕 ──
        [Test]
        public void Source_D_AllNewFiles_NoRandom()
        {
            foreach (var p in new[] {
                "Assets/Scripts/Systems/FxFlipbookGenerator.cs",
                "Assets/Scripts/Systems/FxMaterialLibrary.cs",
                "Assets/Scripts/Systems/FxRimOverlay.cs" })
            {
                // [무번호 29-D] 결정론 — 실사용 난수 API만 검사(주석 문자열 오탐 방지 — 100차 함정 선례).
                string txt = Read(p);
                Assert.That(txt, Does.Not.Contain("UnityEngine.Random.value"), $"[29-D] {p} — Random.value 0건.");
                Assert.That(txt, Does.Not.Contain("UnityEngine.Random.Range"), $"[29-D] {p} — Random.Range 0건.");
                Assert.That(txt, Does.Not.Contain("new System.Random"), $"[29-D] {p} — System.Random 0건.");
            }
        }
    }
}
