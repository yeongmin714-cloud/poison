using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 64] 센서 진실성 + 팬 밴드 텍스처 계약 — 58-P2의 "화면 99.98% 순백"이 센서 공백 RT
    /// (58_scan_114414 = 57,597px 균일 ≥247)였고 같은 순간 실화면 캡처는 흰 194px뿐이었다는
    /// 픽셀 실측에 따른 근본 수리: ①공백 프레임 가드 ②RenderRequest 풀 파이프라인 렌더
    /// ③히트 동기 긴급 스캔(단명 FX 샘플링 사각 해소) ④FXBandingRing _MainTex 기본 "white"
    /// 하드엣지 스트릭의 절차 페더 밴드 직접 바인딩.
    /// </summary>
    public class SensorTruthfulness_64Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        [Test]
        public void Scan_64_BlankFrameGuard()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            // 1) 균일 프레임 판정기 존재 + 98% 임계(11:44 실측 99.98% 재발 차단)
            Assert.That(src, Does.Contain("private static bool IsBlankFrame(Color32[] px)"), "[64] 공백 프레임 판정기.");
            Assert.That(src, Does.Contain("top >= total * 98 / 100"), "[64] 균일 임계 98%(8비트 버킷 최다 색 비율).");
            // 2) 보고 전 가드 배치 — GetPixels32 이후, 클러스터 카운트 이전(스킵이 보고보다 선행)
            int iPx = src.IndexOf("var px = _scanTex.GetPixels32();");
            int iGuard = src.IndexOf("if (IsBlankFrame(px))");
            int iCount = src.IndexOf("int magCount = 0");
            Assert.That(iGuard, Is.GreaterThan(iPx).And.LessThan(iCount), "[64] 가드는 픽셀 판독 후 카운트 전 배치.");
            // 3) 1회 로그 — 스팸 금지(48차 교훈 승계)
            Assert.That(src, Does.Contain("[64-SCAN] ⚠️ 스캔 RT 공백"), "[64] 공백 감지 1급 로그(1회).");
            Assert.That(src, Does.Contain("_blankRtLogged"), "[64] 공백 로그 1회 플래그.");
        }

        [Test]
        public void Scan_64_RenderRequestFullPipeline()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            // 풀 파이프라인 렌더 우선(공백/과노출 동시 제소) + 실패 시 수동 재렌더 폴백 승계
            Assert.That(src, Does.Contain("RenderPipeline.SubmitRenderRequest(_scanCam, req)"), "[64] RenderRequest 풀 파이프라인 렌더.");
            Assert.That(src, Does.Contain("new RenderPipeline.StandardRequest { destination = _scanRt }"), "[64] StandardRequest(6000.4 실측: RenderPipeline 중첩·destination 필드)+RT 바인딩.");
            Assert.That(src, Does.Contain("_renderRequestBroken"), "[64] 미지원 환경 1회 전환 플래그(재시도 스팸 금지).");
            int iReq = src.IndexOf("SubmitRenderRequest");
            int iManual = src.IndexOf("_scanCam.Render();");
            Assert.That(iManual, Is.GreaterThan(iReq), "[64] 수동 재렌더는 폴백으로만 존재(우선순위 계약).");
        }

        [Test]
        public void Scan_64_UrgentHitSyncScan()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            // 스폰 등록 → +0.12s 긴급 스캔 예약 → 러너가 정기 스윕보다 우선 실행
            Assert.That(src, Does.Contain("_urgentScanPending"), "[64] 긴급 스캔 예약 플래그.");
            Assert.That(src, Does.Contain("_urgentScanAt = Time.unscaledTime + 0.12f"), "[64] 피크 가시점 0.12s 예약(단명 FX 수명 0.1~0.7s 대응).");
            int iReg = src.IndexOf("internal static void RegisterFxSource");
            int iSched = src.IndexOf("_urgentScanPending = true;");
            Assert.That(iSched, Is.GreaterThan(iReg), "[64] 예약은 FX 스폰 등록 경로 내부.");
            Assert.That(src, Does.Contain("internal static void UrgentScan()"), "[64] 긴급 스캔 진입점.");
            Assert.That(src, Does.Contain("if (_urgentScanPending && Time.unscaledTime >= _urgentScanAt)"), "[64] 러너 Update 우선 처리.");
        }

        [Test]
        public void Fan_64_BandTextureDirectBinding()
        {
            string fx = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            // FanRunner 머티리얼 — _MainTex 슬롯 직접 바인딩(셰이더 기본 "white" 하드엣지 스트릭 차단)
            // 주의: 동일 시그니처의 SparkRunner.Init가 파일 앞에 존재 — ParryDeflectFanMat 앵커로 FanRunner만 슬라이스.
            int iMat = fx.IndexOf("_mat.name = \"ParryDeflectFanMat\"");
            Assert.That(iMat, Is.GreaterThan(0), "[64-P2] FanRunner 머티리얼 앵커.");
            int iEnd = fx.IndexOf("_fragments = new Fragment[count];", iMat);
            Assert.That(iEnd, Is.GreaterThan(iMat), "[64-P2] FanRunner 초기화 블록 경계.");
            string body = fx.Substring(iMat, iEnd - iMat);
            Assert.That(body, Does.Contain("GetFanBandTexture()"), "[64-P2] 팬 머티리얼에 절차 페더 밴드 바인딩.");
            Assert.That(body, Does.Contain("HasProperty(\"_MainTex\")"), "[64-P2] 슬롯 존재 확인 게이트(49차 HasProperty 선례).");
        }

        [Test]
        public void Fan_64_BandTextureDeterministic()
        {
            string lib = Read("Assets/Scripts/Systems/FxMaterialLibrary.cs");
            Assert.That(lib, Does.Contain("public static Texture2D GetFanBandTexture()"), "[64-P2] 밴드 텍스처 생성기.");
            Assert.That(lib, Does.Contain("if (_fanBand != null) return _fanBand;"), "[64-P2] 정적 캐시 1회(결정론 — Random 0건 계약).");
            Assert.That(lib, Does.Contain("Mathf.Exp(-v * v * 4.5f)"), "[64-P2] v방향 가우시안 소프트닝(하드엣지 제거).");
            Assert.That(lib, Does.Contain("TextureWrapMode.Clamp"), "[64-P2] 클램프 랩(밴드 러닝 금지).");
        }
    }
}
