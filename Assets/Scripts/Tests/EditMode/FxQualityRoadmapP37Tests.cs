using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 37] VFX 계약 — F1 MagicHit 보라 재색 / S3 참격 플립북 / M1 예고 수렴 눈금 / R2 명령 눈금 링.
    /// </summary>
    public class FxQualityRoadmapP37Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── F1R: 보라 지면 링 = 의도적 시그니처(사용자 재판정 — 37-F1 재색 폐기) ──
        [Test]
        public void Source_F1R_PurpleSignatureRestored()
        {
            string src = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            Assert.That(src, Does.Contain("[무번호 38-F1R]"), "[38-F1R] 복원 마커.");
            Assert.That(src, Does.Not.Contain("37-F1] 오염 틴트 수리"), "[38-F1R] 37-F1 재색 블록 잔존 금지(사용자 재판정 — 폐기).");
            // [무번호 40 갱신] 재색 경로(MinMaxGradient(Edge))만 금지 — 판별식은 40 서클 다듬기(텍스처 교체)가 정당 재사용.
            Assert.That(src, Does.Not.Contain("new ParticleSystem.MinMaxGradient(FXPalette.Edge)"), "[38-F1R] 보라 재색 경로 잔존 금지.");
        }

        // ── S3: 참격 플립북 아틀라스 + 발화 ──
        [Test]
        public void Source_S3_SlashFlipbook()
        {
            string gen = Read("Assets/Scripts/Systems/FxFlipbookGenerator.cs");
            Assert.That(gen, Does.Contain("[무번호 37-S3]"), "[37-S3] 마커.");
            Assert.That(gen, Does.Contain("public static Texture2D GetSlashArcFlipbook()"), "[37-S3] 진입점.");
            Assert.That(gen, Does.Contain("Mathf.Lerp(-60f, 60f, e)"), "[80-P1 갱신] 스윕 진행 아크(ease-out — 후반 감속).");
            Assert.That(gen, Does.Contain("(ArcRows - 1 - (f / ArcColumns)) * ArcCellSize"), "[80-P1 갱신] top-down 행 정합(29-D1 계약 — 아크 전용 상수).");
            Assert.That(gen, Does.Contain("(Rows - 1 - (f / Columns)) * CellSize"), "[37-S3] top-down 행 정합(29-D1 계약 — 연기 파이프라인 유지).");
            // [무번호 80-P1 갱신] 크리 전용 아틀라스(B안 분리) — 512px 셀·4×4·10프레임(2048×2048 pow2), 정상과 동일 페이싱.
            Assert.That(gen, Does.Contain("public static Texture2D GetCritSlashArcFlipbook()"), "[80-P1] 크리 아틀라스 진입점.");
            Assert.That(gen, Does.Contain("const int CritCellSize = 512;"), "[80-P1] 크리 셀 512px(B안 — 크리만 고품질).");
            Assert.That(gen, Does.Contain("const int CritFrames = 10;"), "[80-P1] 크리 10프레임(정상과 동일 페이싱 — frameOverTime 전순환 계약 76-S1).");
            Assert.That(gen, Does.Contain("Mathf.Lerp(-75f, 75f, e)"), "[80-P1] 크리 스윕 −75°→+75°(정상 ±60°와 구분된 형상, ease-out 동일).");
            Assert.That(gen, Does.Contain("Mathf.Lerp(0.64f, 0.44f, t)"), "[80-P1] 크리 장반경 위계(정상 0.44 대비).");
            Assert.That(gen, Does.Contain("CritAtlasColumns => CritColumns"), "[80-P1] 크리 그리드 접근자.");
            Assert.That(gen, Does.Contain("const int ArcCellSize = 256;"), "[80-P1 갱신] 정상 아크 256px 셀(141차 표준 갱신 — Q1 결정).");
            Assert.That(gen, Does.Contain("const int ArcFrames = 10;"), "[80-P1 갱신] 정상 아크 10프레임(6→10 격상 — 스윕 계단 제거).");
            Assert.That(gen, Does.Contain("ArcAtlasColumns => ArcColumns"), "[80-P1 갱신] 정상 아크 그리드 접근자(연기 AtlasColumns와 분리 — DeathVFX UV 보존).");
            Assert.That(gen, Does.Contain("core *= core;"), "[80-P1 갱신] 코어-에지 이중 알파 구조(고알파 코어 + 페더 에지).");
            string flash = Read("Assets/Scripts/Systems/FxSlashFlipbookFlash.cs");
            // [무번호 80-P2 갱신] B안(분리) — 크리 전용 머티리얼 캐시 + 그리드 분기(37-S3 시트 그리드 문자열 진화).
            Assert.That(flash, Does.Contain("EnsureMaterials(out var mat, out var tilesX, out var tilesY, isCrit);"), "[80-P2] 아틀라스 분기 진입.");
            Assert.That(flash, Does.Contain("sheet.numTilesX = tilesX;"), "[80-P2 갱신] 시트 애니 그리드(정상/크리 분기).");
            Assert.That(flash, Does.Contain("_critArcMat = FxMaterialLibrary.CreateSoftParticle(\"SlashArcCritMat\""), "[80-P2] 크리 머티리얼 세션 캐시(48-T3 선례).");
            Assert.That(flash, Does.Contain("public static void Warmup()"), "[80-P1] 부팅 선생성 진입점.");
            // [무번호 79 갱신] 머티리얼 유한 수명 → 세션 캐시 진화. 히트마다 신규 생성(셰이더 변형 컴파일 채널)이
            //   사용자 세션에서 알파 강제 1(불투명 웜화이트 셀·계단형 합성 — F7 킬스위치로 근원 확정 11-19 실측)을
            //   낳는 경쟁 상태를 만든다. 48-T3 수리 캐시 선례(수리마다 인스턴스 생성 금지)로 세션 1회 생성 고정 —
            //   누출 아님(1머티리얼/세션), 발화마다 파괴 금지.
            Assert.That(flash, Does.Contain("_slashArcMat = FxMaterialLibrary.CreateSoftParticle(\"SlashArcMat\""),
                "[79 갱신] 머티리얼 세션 캐시(48-T3 선례 — 히트마다 생성/파괴 금지).");
            Assert.That(flash, Does.Contain("if (!Application.isPlaying) return;"), "[37-S3] EditMode 가드.");
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            // [무번호 70-U3 갱신] 같은 변경세트 정밀 갱신 — 크리 색 위계 isCrit 전달 계약(식별자 도피 금지).
            Assert.That(gate, Does.Contain("FxSlashFlipbookFlash.Play(position, isCrit ? 1.5f : 1f, isCrit);"),
                "[37-S3] 히트 훅(Guz 위 보완 레이어 — 크리 1.5배).");
        }

        // ── M1: 예고 수렴 눈금 ──
        [Test]
        public void Source_M1_ConvergeChevrons()
        {
            string src = Read("Assets/Scripts/Systems/AttackTelegraphIndicator.cs");
            Assert.That(src, Does.Contain("[무번호 37-M1]"), "[37-M1] 마커.");
            Assert.That(src, Does.Contain("UpdateConvergeChevrons();"), "[37-M1] 갱신 배선.");
            Assert.That(src, Does.Contain("Mathf.Lerp(_baseScale * 0.38f, _baseScale * 0.22f, t)"),
                "[37-M1] 수렴(반경 0.38→0.22 — 남은 시간 비례).");
            Assert.That(src, Does.Contain("remain <= 0.15f"), "[37-M1] 종료 플래시(0.12s) 겹침 방지 정리.");
            Assert.That(src, Does.Contain("CommandMarker.SharedChevronMaterial()"),
                "[37-M1] 체브런 텍스처/머티리얼 단일 소스 재사용(35-R1).");
            Assert.That(src, Does.Contain("bool _concludeFlashSpawned;"), "[37-M1] 기존 22-S1 필드 무손상.");
        }

        // ── R2: 명령 링 이중화+회전 ──
        [Test]
        public void Source_R2_TickRing()
        {
            string src = Read("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.That(src, Does.Contain("[무번호 37-R2]"), "[37-R2] 마커.");
            Assert.That(src, Does.Contain("if (_persistent && Application.isPlaying)"), "[37-R2] 지속형+Play 가드.");
            Assert.That(src, Does.Contain("go.transform.localScale = new Vector3(0.62f, 0.62f, 1f);"),
                "[37-R2] 링 내부(반경 62%) 레이어.");
            Assert.That(src, Does.Contain("_tickLayer.Rotate(0f, -220f * Time.deltaTime, 0f, Space.Self);"),
                "[37-R2] 회전 피드백.");
            Assert.That(src, Does.Contain("int ticks = 16;"), "[37-R2] 절차 16눈금 텍스처(결정론).");
            Assert.That(src, Does.Contain("internal static Material SharedChevronMaterial()"),
                "[37-M1/R2] 체브런 머티리얼 접근자(단일 소스).");
        }

        // ── 전체 결정론 스윕 ──
        [Test]
        public void Source_37_AllDeterministic()
        {
            Assert.That(Read("Assets/Scripts/Systems/SlashVFXRunner.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[37] SlashVFXRunner Random 0건.");
            Assert.That(Read("Assets/Scripts/Systems/FxSlashFlipbookFlash.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[37] FxSlashFlipbookFlash Random 0건.");
            Assert.That(Read("Assets/Scripts/Systems/CommandMarker.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[37] CommandMarker Random 0건.");
            Assert.That(Read("Assets/Scripts/Systems/AttackTelegraphIndicator.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[37] AttackTelegraphIndicator Random 0건.");
        }
    }
}
