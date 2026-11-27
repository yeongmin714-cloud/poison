using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 69] 피격 흰/검 사각 최종 뿌리 — URP 파티클 버텍스 스트림 정합 계약.
    /// 근거: 69 배치 플레이모드 프로브 바이섹션 실측(①MagicHit Shockwave 억제 시 결함 10,653→67px
    /// ②SlashArcFlash 억제 시 1,344→61px) + MagicHit.prefab YAML 14675/19579(빌트인 Standard Unlit용
    /// 커스텀 스트림 [Position,UV,Color] — Normal 없음). 56-P1 셰이더 교체만으로는 ①TEXCOORD0 미바인딩
    /// = 솔리드 블랙 사각 ②시트애니 AnimFrame 미바인딩 = 가비지 타일(블랙/베이지 박스)이 남는다.
    /// </summary>
    public class HitDefectClosure_69Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        [Test]
        public void StreamAlign_69_HelperExists()
        {
            string lib = Read("Assets/Scripts/Systems/FxMaterialLibrary.cs");
            // [69-P2] 공용 헬퍼 — URP 표준 레이아웃 + 플립북 AnimFrame 조건부 추가, 멱등.
            Assert.That(lib, Does.Contain("public static bool AlignParticleVertexStreams(ParticleSystemRenderer pr)"), "[69-P2] 헬퍼 시그니처.");
            Assert.That(lib, Does.Contain("ParticleSystemVertexStream.AnimFrame"), "[69-P2] 플립북 AnimFrame 스트림.");
            Assert.That(lib, Does.Contain("ps.textureSheetAnimation.enabled"), "[69-P2] 시트애니 게이트.");
            Assert.That(lib, Does.Contain("pr.SetActiveVertexStreams(streams);"), "[69-P2] 재정합 호출.");
            // 표준 순서 잠금 — Position → Normal → Color → UV (URP Particles/Unlit 바인딩 계약)
            int p = lib.IndexOf("ParticleSystemVertexStream.Position,", System.StringComparison.Ordinal);
            int n = lib.IndexOf("ParticleSystemVertexStream.Normal,", System.StringComparison.Ordinal);
            int c = lib.IndexOf("ParticleSystemVertexStream.Color,", System.StringComparison.Ordinal);
            int u = lib.IndexOf("ParticleSystemVertexStream.UV,", System.StringComparison.Ordinal);
            Assert.That(p, Is.GreaterThanOrEqualTo(0), "[69-P2] Position 항목 존재.");
            Assert.That(n, Is.GreaterThan(p), "[69-P2] Normal이 Position 뒤(표준 순서).");
            Assert.That(c, Is.GreaterThan(n), "[69-P2] Color가 Normal 뒤(표준 순서).");
            Assert.That(u, Is.GreaterThan(c), "[69-P2] UV가 Color 뒤(표준 순서).");
            Assert.That(lib, Does.Contain("[무번호 69-P2] URP 파티클 버텍스 스트림 정합"), "[69-P2] 근거 주석 보존.");
        }

        [Test]
        public void MagicHit_69_NormalizeWiresAlign()
        {
            string src = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            // [69-P1] NormalizeMagicHitMaterials가 머티리얼 교체 후 스트림 재정합을 호출해야 한다 —
            //   에셋 Shockwave/Flash 커스텀 스트림(빌트인용) 그대로 URP 셰이더에 남기면 블랙 사각 재발.
            Assert.That(src, Does.Contain("if (FxMaterialLibrary.AlignParticleVertexStreams(pr)) streamFixed++;"), "[69-P1] 정규화 경로 배선.");
            Assert.That(src, Does.Contain("[무번호 69-P1/P2] 커스텀 버텍스 스트림 재정합"), "[69-P1] 근거 주석 보존.");
            Assert.That(src, Does.Contain("[69-P1] 커스텀 버텍스 스트림 재정합 {streamFixed}건"), "[69-P1] 1급 로그 계산식.");
        }

        [Test]
        public void FlipbookUsers_69_AlignWired()
        {
            // [69-P2] 시트애니 활성 파티클 생성 경로 전수 배선(호출부 전수 감사 — 65차 재발 교훈).
            // 실측: textureSheetAnimation.enabled = true 는 FxSlashFlipbookFlash/DeathVFX 2곳뿐.
            string slash = Read("Assets/Scripts/Systems/FxSlashFlipbookFlash.cs");
            Assert.That(slash, Does.Contain("FxMaterialLibrary.AlignParticleVertexStreams(rnd);"), "[69-P2] 슬래시 플립북 배선.");
            string death = Read("Assets/Scripts/Systems/DeathVFX.cs");
            Assert.That(death, Does.Contain("FxMaterialLibrary.AlignParticleVertexStreams(rnd);"), "[69-P2] 사망 연기 배선.");
        }
        [Test]
        public void Sentinel_69_BeigeTierAndRuntimeBisect()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            // [69-R1] 베이지 티어 — 크림 박스(250,201,176)는 기존 4티어 전부 미달 = 스캐너 사각지대.
            //   크림 갑옷(249,236,160) 오경고 차단을 위한 g≤222 상한까지 잠금.
            Assert.That(src, Does.Contain("beigeCount++; beigeX += x; beigeY += y;"), "[69-R1] 베이지 티어 픽셀 카운트.");
            Assert.That(src, Does.Contain("[69-BEIGE] 🟤 화면 베이지 클러스터 실측"), "[69-R1] 베이지 1급 보고.");
            Assert.That(src, Does.Contain("private const int BeigeClusterPixels = 300;"), "[69-R1] 베이지 임계 상수.");
            // [69-R1] 런타임 바이섹션 — 사용자 세션 안에서 근원 렌더러를 이름으로 특정(66차 계약 확장).
            Assert.That(src, Does.Contain("private static void TryStartBisect(string kind, int count, Vector2 center)"), "[69-R1] 바이섹션 개시 시그니처.");
            Assert.That(src, Does.Contain("BisectTick();"), "[69-R1] SentinelRunner.Update 스텝 배선.");
            Assert.That(src, Does.Contain("← ★결함 근원"), "[69-R1] 근원 판정 로그.");
            // 카메라 부착 의도 오버레이(비네트) 후보 제외 — 무고 지목 방지.
            Assert.That(src, Does.Contain("r.transform.IsChildOf(cam.transform)"), "[69-R1] 스크린 오버레이 제외 가드.");
            // [69-R2] UI 포함 게임뷰 스캔 — 카메라 RT 미관측 스크린 레이어 결함 커버(베이지 박스가
            //   스캔 RT에 안 잡히는 실측 대응). CaptureScreenshotIntoRenderTexture 2단계 상태 기계.
            Assert.That(src, Does.Contain("private static void UiBeigeProbeTick()"), "[69-R2] UI 게임뷰 스캔 진입.");
            Assert.That(src, Does.Contain("ScreenCapture.CaptureScreenshotIntoRenderTexture(_uiRt);"), "[69-R2] 게임뷰 캡처 API 배선.");
            Assert.That(src, Does.Contain("[69-BEIGE-UI] 🟤 게임뷰(UI 포함) 베이지 클러스터"), "[69-R2] UI 티어 1급 보고.");
            Assert.That(src, Does.Contain("if (_bisectKind == \"beige-ui\")"), "[69-R2] 바이섹션 UI 측정 경로.");
        }

        [Test]
        public void Decal_69_FreshTexturePerInstance()
        {
            // [69-R3] 데칼 신선 텍스처 — 공유 런타임 텍스처의 샘플러 별칭(사용자 세션 베이지 박스,
            //   런타임 바이섹션으로 Decal_ScorchMark 쿼드 특정) 원천 차단. 스폰별 신선 생성+바인딩+수명 말 파괴.
            string src = Read("Assets/Scripts/Systems/DecalSpawner.cs");
            Assert.That(src, Does.Contain("public System.Func<Texture2D> textureGenerator;"), "[69-R3] 생성기 델리게이트.");
            Assert.That(src, Does.Contain("_scorchMarkConfig.textureGenerator = CreateScorchTexture;"), "[69-R3] 스코치 생성기 배선.");
            Assert.That(src, Does.Contain("_bloodSplatConfig.textureGenerator = CreateBloodSplatTexture;"), "[69-R3] 혈흔 생성기 배선.");
            Assert.That(src, Does.Contain("instanceMat.SetTexture(\"_BaseMap\", fresh);"), "[69-R3] 인스턴스 슬롯 재바인딩.");
            Assert.That(src, Does.Contain("public Texture2D instanceTexture;"), "[69-R3] 인스턴스 텍스처 보관.");
            Assert.That(src, Does.Contain("Destroy(instance.instanceTexture);"), "[69-R3] 수명 말 동반 파괴(누출 금지).");
        }

        [Test]
        public void Decal_69_R6_ScorchKillSwitch()
        {
            // [69-R6] 스코치 킬스위치 — R3(신선 텍스처)+R4(방어 틴트) 후에도 사용자 세션(13:07 에디터
            //   세션, R5 종료 게이트 발동 = 컴파일 확인)에서 베이지 잔존 보고. 바이섹션 2회 연속 근원
            //   [Decal_ScorchMark_0/Decal_Quad]을 "아예 스폰하지 않음"으로 단일화 — 존재하지 않는
            //   오브젝트는 렌더되지 않음(확정성 최대 원칙).
            var field = typeof(ProjectName.Systems.DecalSpawner).GetField(
                "ScorchMarkSpawnEnabled",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "[69-R6] 킬스위치 상수 존재.");
            Assert.That((bool)field.GetValue(null), Is.False, "[69-R6] 스코치 스폰 차단 기본값.");

            string src = Read("Assets/Scripts/Systems/DecalSpawner.cs");
            int gate = src.IndexOf("if (!ScorchMarkSpawnEnabled && type == \"ScorchMark\")", System.StringComparison.Ordinal);
            int create = src.IndexOf("new GameObject($\"Decal_{type}", System.StringComparison.Ordinal);
            Assert.That(gate, Is.GreaterThanOrEqualTo(0), "[69-R6] 스폰 게이트 존재.");
            Assert.That(create, Is.GreaterThan(gate), "[69-R6] 게이트가 오브젝트 생성보다 앞(차단 보장).");
            // 근거 주석 보존 — 재개 조건(CULPRIT 숫자 확정 후) 명시.
            Assert.That(src, Does.Contain("[무번호 69-R6] 스코치 데칼 킬스위치"), "[69-R6] 근거 주석.");
            Assert.That(src, Does.Contain("[69-CULPRIT] 덤프로 근원 렌더 경로가 숫자로 확정된 후"), "[69-R6] 재개 조건 주석.");
        }

        [Test]
        public void ScreenVignette_69_R7_FreshTexturePerPulse()
        {
            // [69-R7] 비네트 정적 공유 텍스처 폐기 — R6(스코치 차단) 후에도 사용자 세션 베이지 잔존
            //   (13:36 세션, [69-R6] 차단 로그 3회 실측) → 스코치 무죄. 잔여 공유 런타임 텍스처 보유자
            //   중 카메라 자식 풀스크린 쿼드 = ScreenVignetteFX(R1 바이섹션 "카메라 자식 제외" 맹점).
            //   별칭 시 아크 아틀라스 웜화이트 샘플 × 적색 틴트×α0.22 = (249,196,172) ≈ 실측 베이지
            //   (249,201,173) 정확 합치 + 120×88px ≈ 8×6 아틀라스 셀(120×90) 크기 합치.
            string src = Read("Assets/Scripts/Systems/ScreenVignetteFX.cs");
            Assert.That(src, Does.Not.Contain("private static Texture2D _vignetteTex;"), "[69-R7] 정적 공유 텍스처 필드 폐기.");
            Assert.That(src, Does.Contain("CreateVignetteTextureFresh()"), "[69-R7] 펄스별 신선 텍스처 생성.");
            Assert.That(src, Does.Contain("pulse.BindFreshTexture(freshTex);"), "[69-R7] 러너 텍스처 등록.");
            Assert.That(src, Does.Contain("if (_tex != null) Destroy(_tex);"), "[69-R7] 펄스 종료 텍스처 파괴(누출 금지).");
            Assert.That(src, Does.Contain("if (_mat != null) Destroy(_mat);"), "[69-R7] 펄스 종료 머티리얼 파괴(기존 누출 수리).");
            Assert.That(src, Does.Contain("[69-R7] 비네트 펄스"), "[69-R7] 펄스별 1급 로그(세션 상관 데이터).");
            // 공개 시그니처 불변 — 기존 계약테스트(UVWX/ScreenChromaticV3) 호환 유지.
            Assert.That(src, Does.Contain("public static VignettePulse Begin(Camera cam, Color color, float peakAlpha, float fadeIn, float hold, float fadeOut)"), "[69-R7] Begin 시그니처 불변.");
            Assert.That(src, Does.Contain("public static VignettePulse BeginManual(Camera cam, Color color, float peakAlpha, float fadeIn)"), "[69-R7] BeginManual 시그니처 불변.");
            // 바이섹션 맹점 제거 — 카메라 자식 제외→태그 전환.
            string sentinel = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            Assert.That(sentinel, Does.Contain("[CAM-CHILD] "), "[69-R7] 카메라 자식 후보 태그 보고.");
            Assert.That(sentinel, Does.Contain("r.transform.IsChildOf(cam.transform)"), "[69-R1] 판정 문자열 유지(제외→태그 전환).");
        }
    }
}
