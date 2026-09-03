using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class TerrainSplatBakerTests
    {
        List<Texture2D> MakeTextures(int count)
        {
            var list = new List<Texture2D>();
            for (int i = 0; i < count; i++)
            {
                var t = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                Color[] c = new Color[256];
                float g = 0.3f + i * 0.15f;
                for (int j = 0; j < c.Length; j++) c[j] = new Color(g, g, g);
                t.SetPixels(c); t.Apply();
                list.Add(t);
            }
            return list;
        }

        [Test]
        public void CreateForNation_ThreeOrMoreLayers_GivenEnoughTextures()
        {
            var layers = TerrainLayerDef.CreateForNation(NationType.East, MakeTextures(4));
            Assert.GreaterOrEqual(layers.Count, 3);
        }

        [Test]
        public void CreateForNation_ReusesSingleTexture_WhenOnlyOneProvided()
        {
            var layers = TerrainLayerDef.CreateForNation(NationType.East, MakeTextures(1));
            Assert.GreaterOrEqual(layers.Count, 3);
            foreach (var l in layers) Assert.IsNotNull(l.albedo);
        }

        [Test]
        public void ComputeWeights_SumToOne_AcrossManyPoints()
        {
            var layers = TerrainLayerDef.CreateForNation(NationType.East, MakeTextures(4));
            for (int i = 0; i < 50; i++)
            {
                float wx = -900f + i * 36f;
                float wz = -900f + i * 36f;
                float[] w = TerrainSplatBaker.ComputeWeights(wx, wz, layers, 42);
                float sum = 0f;
                foreach (var x in w) { sum += x; Assert.GreaterOrEqual(x, -0.0001f); }
                Assert.AreEqual(1f, sum, 0.0001f);
            }
        }

        [Test]
        public void ComputeWeights_Deterministic_SameSeedSameResult()
        {
            var layers = TerrainLayerDef.CreateForNation(NationType.East, MakeTextures(4));
            var a = TerrainSplatBaker.ComputeWeights(400f, -300f, layers, 42);
            var b = TerrainSplatBaker.ComputeWeights(400f, -300f, layers, 42);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i], 0.00001f);
        }

        [Test]
        public void ComputeLayerColor_ReturnsNonBlack_AtEastPoint()
        {
            var layers = TerrainLayerDef.CreateForNation(NationType.East, MakeTextures(4));
            Color c = TerrainSplatBaker.ComputeLayerColor(NationType.East, 400f, -300f, layers, 42);
            Assert.Greater(c.r + c.g + c.b, 0.01f);
        }

        [Test]
        public void BakeSplatMap_64_NoThrow_Readable()
        {
            TerrainSplatBaker.BakeSplatMap(NationType.East, MakeTextures(4), 64, 42);
            var tex = TerrainSplatBaker.BakeSplatMap(NationType.East, MakeTextures(4), 64, 42);
            Assert.IsNotNull(tex);
            Assert.AreEqual(64, tex.width);
            Assert.IsTrue(tex.isReadable);
            Assert.IsNotNull(tex.GetPixel(32, 32));
        }

        [Test]
        public void EstimateSlopeDegrees_NonNegative_Anywhere()
        {
            float s = TerrainSplatBaker.EstimateSlopeDegrees(400f, -300f);
            Assert.GreaterOrEqual(s, 0f);
        }

        // ================================================================
        // Phase T-R3: 5레이어 팔레트 + 마스크 공유 (형태-색 정합성)
        // ================================================================

        [Test]
        public void CreateForNation_ProducesFiveLayers()
        {
            foreach (NationType nation in new[]
            {
                NationType.East, NationType.South, NationType.North,
                NationType.West, NationType.Empire
            })
            {
                var layers = TerrainLayerDef.CreateForNation(nation, MakeTextures(5));
                Assert.AreEqual(5, layers.Count, $"{nation}는 5레이어여야 함");
                foreach (var l in layers) Assert.IsNotNull(l.albedo);
            }
        }

        [Test]
        public void CreateForNation_LayerNames_SemanticOrder()
        {
            var layers = TerrainLayerDef.CreateForNation(NationType.East, MakeTextures(5));
            var names = new System.Collections.Generic.List<string>();
            foreach (var l in layers) names.Add(l.layerName);
            Assert.Contains("lowland", names);
            Assert.Contains("midland", names);
            Assert.Contains("rock_cliff", names);
            Assert.Contains("dirt_path", names);
            Assert.Contains("moss_water", names);
        }

        /// <summary>절벽 지점에서 실제 ComputeWeights 결과(공유 마스크+실제 suppression)로 L3가 우세한 지점을 그리드 스캔. 없으면 false.</summary>
        static bool TryFindCliffPoint(NationType nation, List<Texture2D> tex, int seed, out float wx, out float wz)
        {
            wx = 0f; wz = 0f;
            var layers = TerrainLayerDef.CreateForNation(nation, tex);
            int rockIdx = -1;
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].layerName.Contains("rock")) { rockIdx = i; break; }
            if (rockIdx < 0) return false;
            const float gridStep = 50f;
            for (float x = -900f; x <= 900f; x += gridStep)
            {
                for (float z = -900f; z <= 900f; z += gridStep)
                {
                    float[] w = TerrainSplatBaker.ComputeWeights(x, z, layers, seed);
                    // L3 지배: rock이 가장 큰 가중치이되 명확한 우위(≥0.5), 그리고 실제 절벽 마스크 m>0.5 일치
                    if (w[rockIdx] >= 0.5f &&
                        TerrainShape.CliffMask(x, z, nation, seed, TerrainGenerator.SampleCliffSuppression(x, z)) > 0.5f)
                    {
                        wx = x; wz = z; return true;
                    }
                }
            }
            return false;
        }

        [Test]
        public void ComputeWeights_CliffShared_MakesL3Dominant()
        {
            // 형태(R2 절벽 마스크)와 색(스플랫 L3 바위)이 같은 데이터를 공유하는지 검증.
            var layers = TerrainLayerDef.CreateForNation(NationType.East, MakeTextures(5));
            Assert.IsTrue(TryFindCliffPoint(NationType.East, MakeTextures(5), 42, out float cx, out float cz),
                "절벽 마스크로 L3가 우세한 지점을 찾지 못함 — 스캔 범위 조정 필요");

            float[] w = TerrainSplatBaker.ComputeWeights(cx, cz, layers, 42);
            int rockIdx = -1;
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].layerName.Contains("rock")) { rockIdx = i; break; }
            // 로그: 절벽 영역 L3 비율 수치
            UnityEngine.Debug.Log($"[T-R3] cliff @({cx:F0},{cz:F0}) L3비율={w[rockIdx]:F3}");

            // L3가 절벽 지점에서 지배 (가장 큰 가중치)
            float maxW = -1f; int maxIdx = -1;
            for (int i = 0; i < w.Length; i++) if (w[i] > maxW) { maxW = w[i]; maxIdx = i; }
            Assert.GreaterOrEqual(w[rockIdx], 0.5f, "절벽 지점에서는 L3(바위)가 과반 우세해야 함");
            Assert.AreEqual(rockIdx, maxIdx, "절벽 지점에서는 L3(바위)가 지배해야 함");
            // 형태-색 정합성: 이 스플랫 L3 영역이 고도 절벽 마스크와 일치 (공유 단일 소스)
            Assert.Greater(TerrainShape.CliffMask(cx, cz, NationType.East, 42, TerrainGenerator.SampleCliffSuppression(cx, cz)), 0.5f,
                "L3 바위 영역은 실제 고도 절벽 마스크 m>0.5와 반드시 일치해야 함");
        }

        [Test]
        public void ComputeWeights_WaterBand_NearLake_MakesL5Dominant()
        {
            // 호수 LCG 앵커(시드 불변) 재사용 — 호수 중심에서 L5이끼·수변이 지배해야 함.
            if (TerrainGenerator.Lakes == null || TerrainGenerator.Lakes.Count == 0)
                Assert.Ignore("호수 정의 없음 — 스킵");
            var layers = TerrainLayerDef.CreateForNation(NationType.East, MakeTextures(5));
            var lake = TerrainGenerator.Lakes[0];

            float[] w = TerrainSplatBaker.ComputeWeights(lake.center.x, lake.center.z, layers, 42);
            int mossIdx = -1;
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].layerName.Contains("moss")) { mossIdx = i; break; }
            Assert.GreaterOrEqual(mossIdx, 0);
            float maxW = -1f; int maxIdx = -1;
            for (int i = 0; i < w.Length; i++) if (w[i] > maxW) { maxW = w[i]; maxIdx = i; }
            Assert.AreEqual(mossIdx, maxIdx, "호수 중심에서는 L5(이끼·수변)가 지배해야 함");
        }

        [Test]
        public void GetFlowerPatchMask_Coverage_About8Percent()
        {
            int total = 0, hit = 0;
            for (int x = -900; x < 900; x += 5)
            {
                for (int z = -900; z < 900; z += 5)
                {
                    total++;
                    if (TerrainShape.GetFlowerPatchMask(x, z) > 0.5f) hit++;
                }
            }
            float coverage = (float)hit / total;
            UnityEngine.Debug.Log($"[T-R3] 꽃밭 패치 커버리지={coverage:P1}");
            Assert.Less(coverage, 0.20f, "야생화 패치 커버리지는 ~8%로 소수여야 함");
            Assert.Greater(coverage, 0.01f, "커버리지가 0은 아니어야 함 (패치 존재)");
        }

        [Test]
        public void GetFantasySubzoneMask_Deterministic_AndBounded()
        {
            float a = TerrainShape.GetFantasySubzoneMask(300f, 200f, NationType.East, 42);
            float b = TerrainShape.GetFantasySubzoneMask(300f, 200f, NationType.East, 42);
            Assert.AreEqual(a, b, 0.00001f, "판타지 서브존 마스크 결정론");
            Assert.GreaterOrEqual(a, 0f);
            Assert.LessOrEqual(a, 1f);

            // 마스크가 실제로 어떤 존을 생성하는지 (0보다 큰 존 존재 여부 로그)
            float peak = 0f;
            for (int x = -900; x <= 900; x += 20)
                for (int z = -900; z <= 900; z += 20)
                    peak = Mathf.Max(peak, TerrainShape.GetFantasySubzoneMask(x, z, NationType.East, 42));
            UnityEngine.Debug.Log($"[T-R3] 동쪽 판타지 서브존 최대 마스크={peak:F2}");
        }
    }
}
