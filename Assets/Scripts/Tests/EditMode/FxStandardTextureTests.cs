using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 28-U2] 텍스처 표준 상향 계약테스트 — Source 잠금 스타일
    ///   (ArmorVisualFitGuardTests/ArmorBodyMeasureSpaceTests 관행).
    ///   대상: Assets/Scripts/Systems/ShockwaveRingFX.cs — 밴드 텍스처 64×16 → 256×64(2옥타브→4옥타브),
    ///   먼지 텍스처 128×128 3옥타브 → 256×256 4옥타브.
    ///   계약 보존: 결정론 해시(43758.5453 선례, Random 0건), FXBandingRing 속성 계약(_DepthFadeDist),
    ///   X2 예산 가드(기존 CombatFx* 잠금 무손상), 캐시 1회 생성 구조 유지.
    ///   기존 CombatFx* 테스트 무손상 — 본 파일은 신규 전용(기존 파일 건드리지 않음).
    /// </summary>
    public class FxStandardTextureTests
    {
        private const string FxPath = "Assets/Scripts/Systems/ShockwaveRingFX.cs";

        /// <summary>[T1] 밴드 텍스처 256×64 표준 상향 — 구현이 쓴 실제 상수 리터럴 잠금.</summary>
        [Test]
        public void T1_Source_BandRes()
        {
            var src = System.IO.File.ReadAllText(FxPath);
            Assert.IsTrue(src.Contains("const int W = 256, H = 64;   // [무번호 28-U2] 64×16 → 256×64 표준 상향"),
                "밴드 텍스처 256×64 상수(무번호 28-U2)");
            Assert.IsFalse(src.Contains("const int W = 64, H = 16;"),
                "구 64×16 상수 잔존 금지(표준 상향 완료)");
        }

        /// <summary>[T2] 밴드 4옥타브 구조 — 3·4옥타브 셀(96셀/32셀) 상수 잠금(기존 24셀/8셀 무손상).</summary>
        [Test]
        public void T2_Source_BandOctaves()
        {
            var src = System.IO.File.ReadAllText(FxPath);
            Assert.IsTrue(src.Contains("float cellA = Mathf.Floor(u * 24f);") && src.Contains("float cellB = Mathf.Floor(u * 8f);"),
                "1·2옥타브(24셀/8셀) 원형 보존 — 무번호 22-P1 잠금");
            Assert.IsTrue(src.Contains("float cellC = Mathf.Floor(u * 96f);") && src.Contains("float cellD = Mathf.Floor(u * 32f);"),
                "3·4옥타브(96셀/32셀) 확장 — 무번호 28-U2");
        }

        /// <summary>[T3] 먼지 256×256 + 4옥타브 — 상수/4옥타브 합성 잠금(기존 3옥타브 합성 무손상).</summary>
        [Test]
        public void T3_Source_DustRes()
        {
            var src = System.IO.File.ReadAllText(FxPath);
            Assert.IsTrue(src.Contains("const int S = 256;   // [무번호 28-U2] 128×128 → 256×256 표준 상향"),
                "먼지 텍스처 256×256 상수(무번호 28-U2)");
            Assert.IsFalse(src.Contains("const int S = 128;"),
                "구 128×128 상수 잔존 금지(표준 상향 완료)");
            Assert.IsTrue(src.Contains("PuffNoise(ux * 5f, uy * 5f, 1f)") && src.Contains("PuffNoise(ux * 23f, uy * 23f, 13f)"),
                "기존 3옥타브 합성(5/11/23 셀) 원형 보존 — 무번호 22-Q1 잠금");
            Assert.IsTrue(src.Contains("PuffNoise(ux * 47f, uy * 47f, 29f)"),
                "4옥타브 합성(47셀) 확장 — 무번호 28-U2");
            Assert.IsTrue(src.Contains("사각 금지 계약"), "사각 금지 계약 유지");
        }

        /// <summary>[T4] 결정론 — 텍스처 빌더 블록 내 Random 부재(부정 잠금).
        ///   밴드(EnsureBandTexture)/먼지(EnsurePuffTexture) 빌더 함수 범위에서 "Random." 부재 —
        ///   결정론 해시(43758.5453 선례)만 사용. 파일 단위는 스파크 코드에 Random.Range 선례가 있으므로
        ///   빌더 블록 스코프로 부정 잠금(FanRunner 결정론 잠금 관행 동일).</summary>
        [Test]
        public void T4_Source_Deterministic()
        {
            var src = System.IO.File.ReadAllText(FxPath);
            int bandStart = src.IndexOf("private static Texture2D EnsureBandTexture()");
            int bandEnd = src.IndexOf("private static void ApplyEmissionBoost");
            int puffStart = src.IndexOf("private static Texture2D EnsurePuffTexture()");
            int puffEnd = src.IndexOf("private static float PuffNoise(float x, float y, float seed)");
            Assert.IsTrue(bandStart > 0 && bandEnd > bandStart, "밴드 빌더 블록 범위 산출");
            Assert.IsTrue(puffStart > 0 && puffEnd > puffStart, "먼지 빌더 블록 범위 산출");
            string bandBlock = src.Substring(bandStart, bandEnd - bandStart);
            string puffBlock = src.Substring(puffStart, puffEnd - puffStart);
            Assert.IsFalse(bandBlock.Contains("Random."), "밴드 빌더 결정론 — Random 0건(무번호 28-U2)");
            Assert.IsFalse(puffBlock.Contains("Random."), "먼지 빌더 결정론 — Random 0건(무번호 28-U2)");
            Assert.IsTrue(bandBlock.Contains("43758.5453f"), "밴드 결정론 해시 상수(선례) 유지");
            Assert.IsTrue(src.Contains("return Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f, 1f);"),
                "PuffHash 결정론 해시 그대로(PuffNoise/PuffHash 유지 — 옥타브만 추가)");
            Assert.IsTrue(src.Contains("const int S = 256;   // [무번호 28-U2] 128×128 → 256×256 표준 상향 — 기존 _puffTex 캐시 필드 재사용(1회 생성)"),
                "캐시 구조 유지(기존 캐시 필드 재사용 — 1회 생성)");
        }

        /// <summary>[T5] FXBandingRing 속성 계약 무손상 — _DepthFadeDist 매핑 리터럴 유지.</summary>
        [Test]
        public void T5_Source_ContractUnchanged()
        {
            var src = System.IO.File.ReadAllText(FxPath);
            Assert.IsTrue(src.Contains("Shader.Find(\"ProjectName/FXBandingRing\")"),
                "FXBandingRing 1순위 셰이더 경로(무번호 26-DF3 잠금)");
            Assert.IsTrue(src.Contains("if (mat.HasProperty(\"_DepthFadeDist\"))"),
                "속성 계약 _DepthFadeDist HasProperty 가드 유지");
            Assert.IsTrue(src.Contains("mat.SetFloat(\"_DepthFadeDist\", ImpactDepthFadeDist);"),
                "_DepthFadeDist 매핑 배선 유지");
            Assert.IsTrue(src.Contains("internal const float ImpactDepthFadeDist = 0.35f;"),
                "깊이 페이드 0.35 상수 유지");
        }

        /// <summary>[T6] 마커 — [무번호 28-U2] 마커 존재(밴드/먼지 빌더 양쪽).</summary>
        [Test]
        public void T6_Source_Marker()
        {
            var src = System.IO.File.ReadAllText(FxPath);
            Assert.IsTrue(src.Contains("[무번호 28-U2] 표준 상향 — 64×16 → 256×64, 2옥타브 → 4옥타브"),
                "밴드 빌더 28-U2 마커");
            Assert.IsTrue(src.Contains("[무번호 28-U2] 표준 상향 — 128×128 3옥타브 → 256×256 4옥타브"),
                "먼지 빌더 28-U2 마커");
        }
    }
}
