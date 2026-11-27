using NUnit.Framework;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 54] 아크 알파 블렌드 진화 + 블롭 섀도우 투명 정규화 + 센티널 흰 클러스터 임계 하향 계약.
    /// 근거: 공격씬 테스트 7·8 실측(11-16) — ①밝은 지면 위 additive 흰 코어 포화 = "텍스처 없는 불투명 흰 덩어리"
    /// ②캐릭터 발밑 검은 원형 블롭 그림자 부재(비전 판독) ③실측 흰 덩어리 150~400px@320×180가 700 임계 미달로
    /// [51-SCREEN] 침묵. 소스계약(문자열 핀) + 리플렉션 상수 검증으로 고정한다.
    /// </summary>
    public class AttackArcBlendShadowContract_54Tests
    {
        private static string ReadSource(string fileName)
        {
            // [147차 선례] 계약테스트는 에셋 경로 직접 판독(편집 모드에서도 안전).
            string path = "Assets/Scripts/Systems/" + fileName;
            Assert.IsTrue(File.Exists(path), $"소스 파일 존재: {path}");
            return File.ReadAllText(path);
        }

        // ── ① 센티널 흰 클러스터 임계 150 (700 폐기) ──────────────────

        [Test]
        public void Sentinel_WhiteClusterThreshold_150()
        {
            string src = ReadSource("MagentaSentinel.cs");
            Assert.That(src, Does.Contain("private const int WhiteClusterPixels = 150;"),
                "[54-P0] 흰 클러스터 임계 150px@320×180 — 실측 덩어리(150~400px) 커버.");
            Assert.That(src, Does.Not.Contain("WhiteClusterPixels = 700"),
                "[54-P0] 구 임계 700 폐기 — 700은 실측 덩어리를 전부 미달시켜 [51-SCREEN] 침묵시켰다.");
            Assert.That(src, Does.Contain("private const int MagentaClusterPixels = 24;"),
                "[54-P0] 마젠타 임계 24 유지 — 마젠타는 이미 소멸(영상 0픽셀) 감시 유지용.");

            // 스캔 RT 깊이 버퍼 24(53차 수리) 무손상 — URP RenderGraph Output Texture 깊이 포맷 필수.
            Assert.That(src, Does.Contain("new RenderTextureDescriptor(ScanW, ScanH, RenderTextureFormat.ARGB32, 24)"),
                "[53-P0 승계] 스캔 RT 깊이 24비트 — 0비트는 DBufferCopyDepthPass \"no format\" 예외를 냈다.");
            Assert.That(src, Does.Contain("_screenScanDisabled"),
                "[53-P0 승계] 스캔 실패 자가 차단 플래그 유지 — 에러 스팸 금지.");
        }

        // ── ② 블롭 섀도우 URP 투명 정규화 ────────────────────────────

        [Test]
        public void BlobShadow_TransparentNormalization_Contract()
        {
            string src = ReadSource("BlobShadow.cs");
            Assert.That(src, Does.Contain("Shader.Find(\"Universal Render Pipeline/Unlit\")"),
                "[54-P0] URP/Unlit 1순위 계약 유지.");
            Assert.That(src, Does.Contain("mat.EnableKeyword(\"_SURFACE_TYPE_TRANSPARENT\")"),
                "[54-P0] 투명 표면 키워드 강제 — 부재 시 URP/Unlit 불투명 변형으로 컴파일 위험(52-b 데칼 선례).");
            Assert.That(src, Does.Contain("SetOverrideTag(\"RenderType\", \"Transparent\")"),
                "[54-P0] RenderType Transparent 태그.");
            Assert.That(src, Does.Contain("BlendMode.SrcAlpha"),
                "[54-P0] SrcAlpha 블렌드.");
            Assert.That(src, Does.Contain("BlendMode.OneMinusSrcAlpha"),
                "[54-P0] OneMinusSrcAlpha 블렌드 — 검정 원형 그라디언트가 알파 페더로 렌더.");
            Assert.That(src, Does.Contain("mat.SetInt(\"_ZWrite\", 0)"),
                "[54-P0] ZWrite off 유지.");
            Assert.That(src, Does.Contain("mat.renderQueue = 2985"),
                "[54-P0] 지면 위 투명 큐 유지.");

            // 텍스처는 검정+알파 그라디언트(흰 사각 방지) — 색상 계약.
            Assert.That(src, Does.Contain("new Color(0f, 0f, 0f, alpha * a)"),
                "[54-P0] 그라디언트 텍스처 = 검정·알파 감쇠(흰색 금지).");
            Assert.That(src, Does.Contain("mat.color = new Color(0f, 0f, 0f, 1f)"),
                "[54-P0] 머티리얼 베이스 컬러 = 검정.");
        }

        // ── ③ 아크/트레일 알파 블렌드 진화 — 런타임 머티리얼 실측 ─────

        [Test]
        public void ArcTrail_Materials_AlphaBlend_Runtime()
        {
            // WeaponSwingTrail.CreateTrailMaterialShared(공개 팩토리) → 실제 머티리얼 블렌드 상태 실측.
            MethodInfo factory = typeof(ProjectName.Systems.WeaponSwingTrail).GetMethod(
                "CreateTrailMaterialShared",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(float) },
                null);
            Assert.IsNotNull(factory, "WeaponSwingTrail.CreateTrailMaterialShared 공개 팩토리 존재.");
            var mat = factory.Invoke(null, new object[] { 1f }) as Material;
            Assert.IsNotNull(mat, "트레일 머티리얼 생성 성공(URP 존재 환경).");

            Assert.That(mat.GetFloat("_Surface"), Is.EqualTo(1f).Within(0.01f),
                "[54-P1] 트레일 Surface=Transparent.");
            Assert.That(mat.GetFloat("_Blend"), Is.EqualTo(0f).Within(0.01f),
                "[54-P1] 트레일 Blend=Alpha(0) — Additive(2) 포화 폐기.");
            Assert.That(mat.GetFloat("_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.5f),
                "[54-P1] 트레일 DstBlend=OneMinusSrcAlpha.");
            Assert.That(mat.GetFloat("_ZWrite"), Is.EqualTo(0f).Within(0.01f),
                "[54-P1] 트레일 ZWrite off.");

            // 53차 승계 — 슬래시 텍스처 바인딩 유지.
            Assert.IsNotNull(mat.mainTexture, "[53-P2 승계] 트레일 _MainTex 바인딩 유지.");

            if (Application.isPlaying)
                Object.Destroy(mat);
            else
                Object.DestroyImmediate(mat);
        }
    }
}
