using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 19-D2] 패링 FX Phase D 계약테스트 — 반격 히트 플래시(화이트 프레임) 실카메라 가시성 수리.
    /// 근거: 프로브 cfx_08 픽셀 계량 순수흰색(>235 채널) 비율 0.00% + 참조 영상("패링 공격씬 테스트.mp4" 51s)
    /// 에서도 미표출 → URP/Lit transparent surface 핸드튜닝이 실카메라 SubmitRenderRequest 경유에서 알파 반영 실패.
    /// 수리: Phase A 별섬광 수리 선례(Sprites/Default — 프로젝트 표준 투명 셰이더)로 전환. 위조 배선 금지.
    /// </summary>
    public class CombatFxParryFlashPhaseDTests
    {
        // ── T1. D2 수리 — ParryFlashFader 셰이더 Sprites/Default 우선(URP/Lit 폴백 유지) ──
        [Test]
        public void T1_ParryFlashFader_Shader_SpritesDefault_First()
        {
            var shader = Shader.Find("Sprites/Default");
            Assert.IsNotNull(shader, "Sprites/Default 셰이더가 프로젝트에 존재해야 한다(Phase A 별섬광 수리 선례).");

            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ParryFlashFader.cs");
            int idxSprites = src.IndexOf("Shader.Find(\"Sprites/Default\")");
            int idxUrp = src.IndexOf("Shader.Find(\"Universal Render Pipeline/Lit\")");
            Assert.GreaterOrEqual(idxSprites, 0, "Sprites/Default 우선 배선 존재.");
            Assert.GreaterOrEqual(idxUrp, 0, "URP/Lit 폴백 유지(존재하지 않는 환경 대비).");
            Assert.Less(idxSprites, idxUrp, "D2 수리: Sprites/Default가 URP/Lit보다 먼저 시도되어야 한다.");
        }

        // ── T2. D2 수리 — ParryFlashFader Init 시그니처·duration 체계 유지 ──
        [Test]
        public void T2_ParryFlashFader_InitSignature_UnscaledTime_Preserved()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ParryFlashFader.cs");
            Assert.That(src, Does.Contain("public void Init(Color color, float peakAlpha, float duration)"),
                "D2 수리: Init 시그니처 유지(호출부 PlayerCombat.SpawnParryScreenFlash 파라미터 불변 계약).");
            Assert.That(src, Does.Contain("Time.unscaledDeltaTime"),
                "D2 수리: unscaled 시간 기준 유지(히트스톱 동기 물림 계약).");
            Assert.That(src, Does.Contain("_mat.SetColor(\"_Color\""),
                "Sprites/Default는 _Color 속성 기반 — 페이드 알파 갱신이 _Color에도 배선되어야 한다.");
        }

        // ── T3. [205차 갱신] PlayerCombat.SpawnParryScreenFlash 파라미터(흰색·피크 1.0 오파크 / 0.07s) ──
        //   [205차] 청백 왜곡 잔여 근원 = 피크 0.85(<1.0) 씬 블리딩 — 순백 계약으로 피크 1.0 갱신(알파 용도별 조정 전제, 171차).
        //   히트스톱 0.07s 동기 물림과 흰색 단일 소스는 무손상.
        [Test]
        public void T3_SpawnParryScreenFlash_Parameters_Preserved()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("fader.Init(Color.white, 1f, 0.07f)"),
                "[205차] 순백 계약: 화이트 프레임 피크 1.0(오파크) — 구 피크 0.85는 씬 블리딩 청백 왜곡 근원. 히트스톱 0.07s 동기 물림 유지.");
            Assert.That(src, Does.Contain("ParryFlashFader"),
                "D2 수리: ParryFlashFader 참조 유지.");
        }

        // ── T4. D2 수리 — 화이트 프레임은 PERFECT 등급만(기존 계약) ──
        [Test]
        public void T4_WhiteFrame_PerfectOnly_Preserved()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            int idxParry = src.IndexOf("private void TryParryImpactFX");
            int idxFlash = src.IndexOf("SpawnParryScreenFlash();");
            int idxPerfect = src.IndexOf("grade == ParryGrade.Perfect", idxParry);
            Assert.GreaterOrEqual(idxParry, 0, "TryParryImpactFX 존재.");
            Assert.Greater(idxFlash, idxPerfect, "화이트 프레임은 PERFECT 등급 분기 내부에만 있다(기존 계약).");
        }

        // ── T5. D2 수리 — 검증된 Phase A 별섬광 Sprites/Default 선례와 정합(중복 수리 아님) ──
        [Test]
        public void T5_StarFlare_SpritesDefault_Precedent_Alignment()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ArrowProjectile.cs");
            Assert.That(src, Does.Contain("Shader.Find(\"Sprites/Default\")"),
                "Phase A 별섬광 수리 선례(Sprites/Default) 유지 — D2 수리는 동일 프로젝트 표준 셰이더를 따른다.");
        }
    }
}
