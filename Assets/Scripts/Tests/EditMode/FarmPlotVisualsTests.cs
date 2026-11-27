using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [153차 P1/P3] FarmPlotVisuals 계약 — docs/153_FARMING_QUALITY_ACQ_VFX_PLAN.md §3 P1/P3.
    /// FarmPlotLifecycleTests 규약 1:1(네임스페이스/using/NUnit 표기 동일).
    ///
    /// 판정 대상(모두 public static — MonoBehaviour 상태 없는 결정론):
    ///  ① 절차 밭흙 텍스처 캐시 결정론 — 동일 호출 2회 동일 인스턴스.
    ///  ② 수분 밝기 경계 판정 — 촉촉(수분 1) ×0.75 어둠 ↔ 마름(수분 0) ×1.1 밝음, 중간 선형 보간.
    ///  ③ HerbType→GLB 경로 매핑 결정론 — resolver 선례(Yellow→herb_gold, Silver→herb_blue 스왑 포함).
    ///  ④ 소스계약 — FarmPlot.cs에 FarmPlotVisuals 위임 마커 존재(시각부 분리 판정).
    ///  ⑤ 구멍 격자 메시 정점 범위 — 실제 오목 존재 + 플롯 시각 깊이 −0.1m 이내 계약.
    ///  ⑥ [154차 P2] 굉이질 구멍 수리 계약 — 탄젠트 어트리뷰트(루트 수리 핀) + HoleMeshLiftY 2mm
    ///     + 그림자 수신 해제 + AO 프리멀티 텍스처 결정론/명암 순서(중앙 최어두).
    ///
    /// EditMode 제약: 오브젝트 정리는 DestroyImmediate만 사용(Object.Destroy 금지 — EditMode 금지 API).
    /// 상태머신/카탈로그·enum 항목 추가 금지 계약 — 본 테스트는 항목 수를 건드리지 않는다.
    /// </summary>
    public class FarmPlotVisualsTests
    {
        // ===== ① 절차 밭흙 텍스처 캐시 결정론 =====

        [Test]
        public void SoilTexture_Cache_ReturnsSameInstanceTwice()
        {
            var t1 = FarmPlotVisuals.GetOrCreateSoilTexture();
            var t2 = FarmPlotVisuals.GetOrCreateSoilTexture();
            Assert.IsNotNull(t1, "절차 텍스처 첫 생성 성공");
            Assert.That(ReferenceEquals(t1, t2), Is.True, "동일 호출 2회 → 동일 인스턴스(static 캐시 결정론)");
        }

        [Test]
        public void SoilTexture_SpecSizeIsDeterministic()
        {
            var t = FarmPlotVisuals.GetOrCreateSoilTexture();
            Assert.AreEqual(256, t.width, "텍스처 가로 — 스펙 256");
            Assert.AreEqual(256, t.height, "텍스처 세로 — 스펙 256");
        }

        // ===== ② 수분 밝기 경계 판정 (촉촉 ×0.75 어둠 ↔ 마름 ×1.1 밝음) =====

        [Test]
        public void SoilBrightness_FullyMoist_IsDarkBoundary()
        {
            Assert.AreEqual(0.75f, FarmPlotVisuals.ComputeSoilBrightness(1f), 0.0001f,
                "수분 1(촉촉) → 기본 흙색 ×0.75 어둠 경계");
        }

        [Test]
        public void SoilBrightness_FullyDry_IsBrightBoundary()
        {
            Assert.AreEqual(1.1f, FarmPlotVisuals.ComputeSoilBrightness(0f), 0.0001f,
                "수분 0(마름) → 기본 흙색 ×1.1 밝음 경계");
        }

        [Test]
        public void SoilBrightness_HalfMoist_InterpolatesBetweenBoundaries()
        {
            float mid = FarmPlotVisuals.ComputeSoilBrightness(0.5f);
            Assert.That(mid, Is.GreaterThan(0.75f).And.LessThan(1.1f),
                "중간 수분 → 두 경계(0.75~1.1) 사이 보간");
            Assert.AreEqual(0.925f, mid, 0.0001f, "중간 수분 → (0.75+1.1)/2 선형");
        }

        [Test]
        public void SoilBrightness_OutOfRange_ClampsToBoundary()
        {
            Assert.AreEqual(1.1f, FarmPlotVisuals.ComputeSoilBrightness(-1f), 0.0001f, "음수 → 마름 경계 클램프");
            Assert.AreEqual(0.75f, FarmPlotVisuals.ComputeSoilBrightness(2f), 0.0001f, "1 초과 → 촉촉 경계 클램프");
        }

        // ===== ③ HerbType → GLB 경로 매핑 결정론 (resolver 선례) =====

        [Test]
        public void CropGlbKey_MapsAllFiveHerbColors()
        {
            Assert.AreEqual("herb_red", FarmPlotVisuals.ResolveCropGlbKey(HerbPickup.HerbType.Red), "Red → herb_red");
            Assert.AreEqual("herb_purple", FarmPlotVisuals.ResolveCropGlbKey(HerbPickup.HerbType.Purple), "Purple → herb_purple");
            Assert.AreEqual("herb_gold", FarmPlotVisuals.ResolveCropGlbKey(HerbPickup.HerbType.Yellow), "Yellow → herb_gold(resolver 선례 스왑)");
            Assert.AreEqual("herb_blue", FarmPlotVisuals.ResolveCropGlbKey(HerbPickup.HerbType.Silver), "Silver → herb_blue(resolver 선례 스왑)");
            Assert.AreEqual("herb_green", FarmPlotVisuals.ResolveCropGlbKey(HerbPickup.HerbType.Green), "Green → herb_green");
        }

        [Test]
        public void CropGlbResourcesPath_IsDeterministicFullPath()
        {
            Assert.AreEqual("Models/UserProvided/herb_gold",
                FarmPlotVisuals.CropGlbResourcesPath(HerbPickup.HerbType.Yellow), "Resources 로드 경로 결정론 — herb_gold 포함");
            Assert.AreEqual("Models/UserProvided/herb_blue",
                FarmPlotVisuals.CropGlbResourcesPath(HerbPickup.HerbType.Silver), "Resources 로드 경로 결정론 — herb_blue 포함");
        }

        // ===== ④ 소스계약 — FarmPlot 시각부 위임 마커 =====

        [Test]
        public void FarmPlotSource_ContainsFarmPlotVisualsDelegation()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("FarmPlotVisuals"),
                "[153차 P1] 시각부 위임 마커 — FarmPlotVisuals 분리 존재");
            Assert.That(src, Does.Contain("FarmPlotVisuals.RefreshVisuals"),
                "[153차 P1] RefreshVisuals 시각 갱신 위임 경유");
        }

        [Test]
        public void FarmPlotSource_ContainsSwayTickHook()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("FarmPlotVisuals.Tick"),
                "[153차 P3] Update 스웨이 틱 훅 1줄 존재");
        }

        // ===== ⑤ 구멍 격자 메시 — 정점 범위/결정론/깊이 계약 =====

        [Test]
        public void TillHoleMesh_BuildTwice_IsDeterministic()
        {
            Mesh m1 = FarmPlotVisuals.BuildTillHoleMesh();
            Mesh m2 = FarmPlotVisuals.BuildTillHoleMesh();
            try
            {
                Assert.IsNotNull(m1, "구멍 메시 빌드 성공");
                Assert.AreEqual(m1.vertexCount, m2.vertexCount, "결정론 — 두 빌드 정점 수 동일");
                float minY1 = MinVertexY(m1);
                float minY2 = MinVertexY(m2);
                Assert.AreEqual(minY1, minY2, 0.00001f, "결정론 — 두 빌드 최소 높이 동일");
            }
            finally
            {
                Object.DestroyImmediate(m1);   // EditMode — DestroyImmediate만 허용
                Object.DestroyImmediate(m2);
            }
        }

        [Test]
        public void TillHoleMesh_HasRealConcavityWithinDepthContract()
        {
            Mesh m = FarmPlotVisuals.BuildTillHoleMesh();
            try
            {
                Assert.That(m.vertexCount, Is.GreaterThan(80), "9×9 상면 격자 이상의 정점");
                Assert.That(m.triangles.Length, Is.GreaterThan(0), "삼각형 존재");

                float minY = MinVertexY(m);
                Assert.That(minY, Is.LessThan(0f), "구멍은 실제로 파인다(오목 존재 — 탑다운 격차 판독)");
                Assert.That(minY, Is.GreaterThan(-0.1005f),
                    "플롯 시각 깊이 −0.1m 이내 계약(중앙 내림 최대 −0.08)");
            }
            finally
            {
                Object.DestroyImmediate(m);
            }
        }

        private static float MinVertexY(Mesh mesh)
        {
            float minY = float.MaxValue;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
                if (verts[i].y < minY) minY = verts[i].y;
            return minY;
        }

        // ===== ⑥ [154차 P2] 굉이질 구멍 수리 계약 =====

        [Test]
        public void TillHoleMesh_HasTangentVertexAttribute()
        {
            Mesh m = FarmPlotVisuals.BuildTillHoleMesh();
            try
            {
                Assert.That(m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent), Is.True,
                    "[154차 P2] 루트 수리 핀 — RecalculateTangents로 탄젠트 버텍스 어트리뷰트 보유" +
                    "(탄젠트 부재 + _NORMALMAP = TBN 퇴화 = 검정 렌더)");
            }
            finally
            {
                Object.DestroyImmediate(m);   // EditMode — DestroyImmediate만 허용
            }
        }

        [Test]
        public void HoleMeshLiftY_Contract_IsTwoMillimeterLift()
        {
            Assert.AreEqual(0.002f, FarmPlotVisuals.HoleMeshLiftY, 0.0000001f,
                "[154차 P2] 구멍 루트 상승 계약 — 사용자 승인 2mm(동일 평면/지면 접촉 보험)");
        }

        [Test]
        public void HoleRendererSource_ReceiveShadowsDisabled_Contract()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlotVisuals.cs");
            Assert.That(src, Does.Contain("mr.receiveShadows = false"),
                "[154차 P2] 구멍 렌더러 그림자 수신 해제 계약(실내 그림자 광원 대비 보험 — 렌더 비용 0)");
        }

        [Test]
        public void HoleTexture_Cache_ReturnsSameInstanceTwice()
        {
            var t1 = FarmPlotVisuals.GetOrCreateHoleTexture();
            var t2 = FarmPlotVisuals.GetOrCreateHoleTexture();
            Assert.IsNotNull(t1, "AO 프리멀티 텍스처 첫 생성 성공");
            Assert.That(ReferenceEquals(t1, t2), Is.True,
                "동일 호출 2회 → 동일 인스턴스(static 캐시 결정론 — GetOrCreateSoilTexture 패턴 준용)");
        }

        [Test]
        public void HoleTexture_Recreate_IsDeterministicAverageColor()
        {
            var t1 = FarmPlotVisuals.GetOrCreateHoleTexture();
            Color32 avg1 = AverageColor32(t1);

            // 캐시 리셋 후 재생성(리플렉션 — probe SetPhase 선례) — 동일 시드 재생산 결정론 판정
            var field = typeof(FarmPlotVisuals).GetField("_holeTexture",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(field, "_holeTexture static 캐시 필드 존재");
            field.SetValue(null, null);
            Object.DestroyImmediate(t1);              // 구 인스턴스 정리(캐시에서 이미 떼어냄)
            var t2 = FarmPlotVisuals.GetOrCreateHoleTexture();   // 재생성 — 캐시에 재적재됨
            Color32 avg2 = AverageColor32(t2);

            Assert.AreEqual(avg1.r, avg2.r, "평균 R 동일 — 고정 시드 재생산 결정론");
            Assert.AreEqual(avg1.g, avg2.g, "평균 G 동일 — 고정 시드 재생산 결정론");
            Assert.AreEqual(avg1.b, avg2.b, "평균 B 동일 — 고정 시드 재생산 결정론");
        }

        [Test]
        public void HoleTexture_CenterIsDarkest_VsBermRing()
        {
            var t = FarmPlotVisuals.GetOrCreateHoleTexture();
            // 중앙 = 링0(AO 0.40) 영역 평균 / 둑 = 링3(AO 1.0) 밴드 평균 — 단일 픽셀 대신 영역 평균(노이즈 견내기).
            float center = RegionBrightness(t, 0.44f, 0.56f, 0.44f, 0.56f);
            float berm = RegionBrightness(t, 0.08f, 0.18f, 0.45f, 0.55f);
            Assert.That(berm, Is.GreaterThan(0.05f),
                "[154차 P2] 둑 밴드 비검정 전제 — 검정이면 흙 텍스처 생성 근원 별도 조사(본 판정 전제 붕괴)");
            Assert.That(center, Is.LessThan(berm),
                "[154차 P2] 중앙 바닥(AO 0.40)이 흙둑(AO 1.0)보다 어두움 — 탑다운 음영 판독 계약");
        }

        /// <summary>UV 사각 영역 평균 밝기(0..1) — 정수 누산(결정론).</summary>
        private static float RegionBrightness(Texture2D t, float u0, float u1, float v0, float v1)
        {
            int x0 = Mathf.RoundToInt(u0 * (t.width - 1)), x1 = Mathf.RoundToInt(u1 * (t.width - 1));
            int y0 = Mathf.RoundToInt(v0 * (t.height - 1)), y1 = Mathf.RoundToInt(v1 * (t.height - 1));
            long sum = 0; int n = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Color32 c = t.GetPixel(x, y);
                    sum += c.r + c.g + c.b;
                    n++;
                }
            }
            return sum / (float)(n * 3 * 255);
        }

        [Test]
        public void SoilTexture_AverageMatchesBaseColor_UnitFix_154()
        {
            // [154차 P2-수리] 정규화 단위 혼용 수리 핀 — 텍스처 채널 평균이 베이스색 바이트값과 일치해야 한다.
            //   구 결함: 0.42÷107(≈0.0039) 배수로 전 픽셀이 0으로 스케일링 — 흙 텍스처 전체 검정
            //   (인게임 굉이질 오목 (1,1,1) 실측, EditMode AO 판독 0.0, 153차 프로브 무채색 — 전부 본 결함 귀속).
            var t = FarmPlotVisuals.GetOrCreateSoilTexture();
            Color32 avg = AverageColor32(t);
            int expR = Mathf.RoundToInt(FarmPlotVisuals.SoilBaseColor.r * 255f);
            int expG = Mathf.RoundToInt(FarmPlotVisuals.SoilBaseColor.g * 255f);
            int expB = Mathf.RoundToInt(FarmPlotVisuals.SoilBaseColor.b * 255f);
            Assert.That(avg.r, Is.EqualTo(expR).Within(2),
                "[154차 P2] 흙 텍스처 평균 R = 베이스색 ×255(단위 혼용 수리 — 검정 스케일링 소멸)");
            Assert.That(avg.g, Is.EqualTo(expG).Within(2),
                "[154차 P2] 흙 텍스처 평균 G = 베이스색 ×255");
            Assert.That(avg.b, Is.EqualTo(expB).Within(2),
                "[154차 P2] 흙 텍스처 평균 B = 베이스색 ×255");
        }

        /// <summary>텍스처 전체 채널 평균(Color32 정수 누산 — ComputeChannelAverage 선례 동일, 결정론).</summary>
        private static Color32 AverageColor32(Texture2D tex)
        {
            var px = tex.GetPixels32();
            long r = 0, g = 0, b = 0;
            for (int i = 0; i < px.Length; i++)
            {
                r += px[i].r;
                g += px[i].g;
                b += px[i].b;
            }
            return new Color32((byte)(r / px.Length), (byte)(g / px.Length), (byte)(b / px.Length), 255);
        }

        /// <summary>명암 판정용 밝기(0~1) — RGB 산술 평균.</summary>
        private static float Brightness01(Color32 c)
        {
            return (c.r + c.g + c.b) / (3f * 255f);
        }
    }
}
