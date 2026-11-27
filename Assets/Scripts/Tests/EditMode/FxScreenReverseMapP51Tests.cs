using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 51] 화면 픽셀 역매핑 스캐너 + 셰이더 컴파일 전수 스캔 계약.
    /// 근거(11-16 00:38 Play — 사용자 재보고): 런타임 계측 전부 깨끗(SENTINEL 감지 0·MATGUARD 0·
    /// Unknown error 0·예외 0)인데 시각 결함 잔존 보고 — 센티널이 볼 수 없는 경로(컴파일 실패 GPU 마젠타,
    /// UI/후처리/스카이박스 등)를 원인 불문 커버하는 최종 관문이 필요.
    /// </summary>
    public class FxScreenReverseMapP51Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void ScreenReverseMap_ScanAndIdentifyCoveringRenderers()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            Assert.That(src, Does.Contain("[무번호 51-a] 화면 픽셀 역매핑 스캔"),
                "[51-a] 화면 직접 탐지 관문.");
            Assert.That(src, Does.Contain("private static void ScreenScan()"),
                "[51-a] 스캔 구현 존재.");
            Assert.That(src, Does.Contain("ReadPixels(new Rect(0, 0, ScanW, ScanH), 0, 0)"),
                "[51-a] 화면 픽셀 실측(320×180 다운샘플).");
            Assert.That(src, Does.Contain("[51-SCREEN] 🟣 화면 마젠타 클러스터 실측"),
                "[51-a] 마젠타 클러스터 1급 보고.");
            Assert.That(src, Does.Contain("[51-SCREEN] ⬜ 화면 흰 클러스터 실측"),
                "[51-a] 흰 클러스터 1급 보고.");
            Assert.That(src, Does.Contain("DescribeCoveringRenderers"),
                "[51-a] 덮는 렌더러 역산 — 근원을 이름으로 특정.");
            Assert.That(src, Does.Contain("해당 렌더러 없음(데칼/UI/후처리/스카이박스 경로)"),
                "[51-a→52-c] 렌더러 밖 경로 판정 보고 — 후보 0건도 정보(데칼 커버 추가로 확장).");
            // 스윕 배선 확인 — 매 스윕에서 화면 스캔이 구동된다.
            Assert.That(src.IndexOf("ScreenScan();", System.StringComparison.Ordinal),
                Is.GreaterThan(src.IndexOf("private static void Sweep()", System.StringComparison.Ordinal)),
                "[51-a] Sweep() 이후 배선.");
        }

        [Test]
        public void ShaderCompileSweep_CoversAllLoadedMaterials()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            Assert.That(src, Does.Contain("[무번호 51-b] 셰이더 컴파일 전수 스캔"),
                "[51-b] GPU 마젠타(컴파일 실패) 감지 관문.");
            Assert.That(src, Does.Contain("UnityEditor.ShaderUtil.ShaderHasError(sh)"),
                "[51-b] 에디터 진실원 적용.");
            Assert.That(src, Does.Contain("Resources.FindObjectsOfTypeAll<Material>()"),
                "[51-b] 전 로드 머티리얼 커버.");
            Assert.That(src, Does.Contain("[51-SHADER] ⚠️ 컴파일 에러 셰이더 실측"),
                "[51-b] 에러 셰이더 1급 보고.");
        }

        [Test]
        public void GroundGrassMat_RestoredAndStable()
        {
            string src = Read("Assets/URP/Ground_Grass_Mat.mat");
            Assert.That(src.StartsWith("%YAML 1.1"), Is.True, "[49→51] 자산 복구 유지(재손상 감시).");
            Assert.That(src.Contains("\0"), Is.False, "[49→51] NUL 잔존 금지.");
        }
    }
}
