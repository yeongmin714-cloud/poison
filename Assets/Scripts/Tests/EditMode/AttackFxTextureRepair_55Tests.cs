using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 56] MagicHit 머티리얼 URP 정규화 계약 — 공격씬 9·10 실측 "피격시마다 흰·검 사각 덩어리"의
    /// 진짜 뿌리 = Built-in "Particles/Standard Unlit" 셰이더의 URP 렌더(알파 무시·불투명 단색 쿼드).
    /// 55차 "텍스처 소실"은 오진으로 폐기(프로브 실측: 4종 머티리얼 _MainTex = 팩 텍스처 1-Point/1-Crit_/
    /// 1-Shockwave 2 512×512 정상 바인딩). 39차 확립 해법(URP Particles/Unlit 투명 표준화)을 적용한다.
    /// </summary>
    public class AttackFxTextureRepair_55Tests
    {
        private static string ReadSource(string fileName)
        {
            string path = "Assets/Scripts/Systems/" + fileName;
            Assert.IsTrue(File.Exists(path), $"소스 파일 존재: {path}");
            return File.ReadAllText(path);
        }

        // ── ① URP 정규화 소스계약 — SlashVFXRunner ───────────────────

        [Test]
        public void MagicHit_UrpNormalization_SourceContract()
        {
            string src = ReadSource("SlashVFXRunner.cs");

            // 메서드+양 경로 호출(피격·크로스 공용) — 런타임 가드.
            Assert.That(src, Does.Contain("internal static void NormalizeMagicHitMaterials(GameObject instance)"),
                "[56-P1] URP 정규화 메서드 존재.");
            Assert.That(src, Does.Contain("if (Application.isPlaying) NormalizeMagicHitMaterials(instance);"),
                "[56-P1] 런타임 가드 호출(피격·크로스 공용).");

            // 폴백 계약 — URP 셰이더 미발견 시 built-in 유지(39차 선례).
            Assert.That(src, Does.Contain("Shader.Find(\"Universal Render Pipeline/Particles/Unlit\")"),
                "[56-P1] URP Particles/Unlit 1순위.");
            Assert.That(src, Does.Contain("if (urp == null) return;"),
                "[56-P1] URP 셰이더 미발견 시 무변경 폴백.");

            // 멱등 — 이미 URP인 머티리얼 무손상.
            Assert.That(src, Does.Contain("if (src.shader == urp) continue;"),
                "[56-P1] 이미 URP인 시스템 무손상(멱등).");

            // 승계 — 41-b 클론(스무스 서클+틴트)/에셋 팩 텍스처 전부 승계.
            Assert.That(src, Does.Contain("Texture tex = src.mainTexture;"),
                "[56-P1] 기존 텍스처 승계(스무스 서클/팩 텍스처).");
            Assert.That(src, Does.Contain("Color col = src.color;"),
                "[56-P1] 기존 색 승계(41-b 틴트/에셋 HDR).");

            // 39차 투명 표준 세트 — 알파 무시(불투명 사각) 근원 차단.
            Assert.That(src, Does.Contain("inst.SetFloat(\"_Surface\", 1f)"), "[56-P1] Surface=Transparent.");
            Assert.That(src, Does.Contain("inst.SetFloat(\"_Blend\", 0f)"), "[56-P1] Blend=Alpha.");
            Assert.That(src, Does.Contain("inst.EnableKeyword(\"_SURFACE_TYPE_TRANSPARENT\")"), "[56-P1] 투명 키워드.");
            Assert.That(src, Does.Contain("inst.SetOverrideTag(\"RenderType\", \"Transparent\")"), "[56-P1] RenderType 태그.");
            Assert.That(src, Does.Contain("BlendMode.OneMinusSrcAlpha"), "[56-P1] SrcAlpha/OneMinusSrcAlpha.");
            Assert.That(src, Does.Contain("inst.SetFloat(\"_ZWrite\", 0f)"), "[56-P1] ZWrite off.");
            Assert.That(src, Does.Contain("inst.renderQueue = 3000"), "[56-P1] 투명 큐.");

            // 클론 유한 파괴 — 누출 금지(46차 유령 GO 교훈). sharedMaterial 판독(48차 게터 교훈).
            Assert.That(src, Does.Contain("Object.Destroy(inst, 8f)"), "[56-P1] 클론 유한 파괴.");
            Assert.That(src, Does.Contain("var src = pr.sharedMaterial;"), "[56-P1] sharedMaterial 무인스턴스화 판독.");

            // [계약 진화 167-A1] 41-b 딥퍼플 직지정+40 스무스 서클 클론은 임팩트 경로에서 폐기 —
            // Magic Hit 2 에셋 원형 렌더(사용자 지시 11-21). 폐기 사유 주석만 보존(65-P1 선례).
            Assert.That(src, Does.Not.Contain("new Color(0.45f, 0.25f, 0.85f, 1f)"),
                "[167-A1] 41-b 딥 퍼플 직지정 소멸 — 에셋 원형 렌더.");
            Assert.That(src, Does.Not.Contain("name = \"MagicHitCircleSmooth\""),
                "[167-A1] 스무스 서클 클론 소멸 — 에셋 머티리얼 원형.");
            Assert.That(src, Does.Contain("[167-A1]"), "[167-A1] 에셋 원형 복원 마커.");
        }

        // ── ② 오진 정정 기록 — 55차 "소실" 전제 폐기 ─────────────────

        [Test]
        public void RepairMethod_DoesNotGateOnNullTexture()
        {
            // 55차의 `mainTexture != null` 게이트는 Unity 미해결 GUID가 기본 텍스처(비-null)로 반환되어
            // 수리가 발화하지 않은 원인 — 정규화 방식(게이트 없음, 멱등 skip만)으로 대체됐음을 고정.
            string src = ReadSource("SlashVFXRunner.cs");
            Assert.That(src, Does.Not.Contain("RepairMissingParticleTextures"),
                "[56-P1] 55차 소실 게이트 수리 폐기 — 게이트가 발화를 막았던 실측.");
            Assert.That(src, Does.Not.Contain("MagicHitSoftBurst"),
                "[56-P1] 55차 소프트 버스트 클론명 폐기(텍스처는 실존 — 프로브 실측).");
        }
    }
}
