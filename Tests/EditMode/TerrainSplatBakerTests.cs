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
    }
}
