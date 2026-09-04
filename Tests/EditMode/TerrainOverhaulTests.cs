using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// 🌄 지형 개편 4방위 스타일라이즈드 (2026-09-04, Phase T-R0) 기준선 골격 테스트.
    ///
    /// 이 파일은 Phase T-R2~R4 구현 후 완성되는 테스트 골격이며,
    /// "현재 동작으로 즉시 통과 가능한 것"은 골격 단계에서도 통과해야 한다.
    ///   a) GetHeightAt 결정론          → 현재도 통과 (동일 좌표 = 동일 값)
    ///   b) 경계 블렌드 연속성          → 기존 4방위 크로스페이드만으로 이미 통과(1m당 |Δh|<0.5m)
    ///                                    R2에서 방위별 고도 재설계 후에도 유지되어야 함
    ///   c) 스폰 평탄 (반경 30m <0.3m)  → R2에서 스폰 반경 30m 평탄 보장 시 통과 목표
    ///                                    (현재 스폰 평탄화는 저주파 완만화라 절대 평탄이 아님 → 기준선에선 실패 예상)
    ///   d) 방위별 진폭 분포 (std>0)    → 현재도 통과 (방위별 고유 진폭/시드)
    /// </summary>
    public class TerrainOverhaulTests
    {
        const int SEED = 42;
        const float GROUND_BASE = 1f; // Ground_Inner 월드 y 기저 (GetHeightAt + 1f)

        // ── a) 결정론: 같은 좌표 2회 샘플 = 동일 값 (현재 통과) ──────────────
        [Test]
        public void GetHeightAt_Deterministic_SameCoordSameValue()
        {
            for (int i = 0; i < 20; i++)
            {
                float x = -900f + i * 90f;
                float z = 700f - i * 60f;
                float h1 = TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, SEED);
                float h2 = TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, SEED);
                Assert.AreEqual(h1, h2, 0.00001f, $"결정론 위반 @ ({x:F1},{z:F1})");
            }
        }

        // ── b) 경계 블렌드 연속성 ────────────────────────────────────────────
        // 4방위 중심각: 동=0°/북=90°/서=180°/남=270°. 그 사이 각도 경계는 45/135/225/315°.
        // 반경 300m 지점에서 경계를 가로지르며 1m(호) 간격 샘플 → 인접 샘플 |Δh| < 0.5m.
        // (기존 TRANSITION_WIDTH=120m 크로스페이드 + R2 결과 보간으로 단차 없음 보증)
        [Test]
        public void BoundaryBlend_Continuous_Across4AzimuthBoundaries()
        {
            const float radius = 300f;
            const float arcSpanDeg = 4f;      // 경계 중심 양옆 2° 스캔 (경계 양쪽 국가 교차)
            float stepRad = 1f / radius;      // 1m 호(arc) 간격

            float[] boundaryAngles = { 45f, 135f, 225f, 315f };
            foreach (float theta in boundaryAngles)
            {
                float startDeg = theta - arcSpanDeg / 2f;
                float endDeg = theta + arcSpanDeg / 2f;
                bool first = true;
                float prev = 0f;
                float maxDelta = 0f;
                for (float aDeg = startDeg; aDeg <= endDeg; aDeg += stepRad * Mathf.Rad2Deg)
                {
                    float a = aDeg * Mathf.Deg2Rad;
                    float x = radius * Mathf.Cos(a);
                    float z = radius * Mathf.Sin(a);
                    float h = TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, SEED) + GROUND_BASE;
                    if (!first)
                        maxDelta = Mathf.Max(maxDelta, Mathf.Abs(h - prev));
                    prev = h;
                    first = false;
                }
                Assert.Less(maxDelta, 0.5f, $"경계 {theta}°(반경 {radius}m) 인접 샘플 최대 |Δh| = {maxDelta:F3}m");
            }
        }

        // ── c) 스폰 평탄: 전역 스폰 좌표 반경 30m 내 5샘플 편차 < 0.3m ─────────
        // 전역 스폰 = PlayerSpawnConfig.SpawnPosition (728, 0.24, -529).
        // T-R2에서 스폰 반경 30m 평탄(절대 평탄) 보장 시 통과 목표.
        [Test]
        public void SpawnArea_Flat_Within30m()
        {
            Vector3 spawn = PlayerSpawnConfig.SpawnPosition;
            Vector3[] samples =
            {
                spawn,
                spawn + new Vector3(30f, 0f, 0f),
                spawn + new Vector3(-30f, 0f, 0f),
                spawn + new Vector3(0f, 0f, 30f),
                spawn + new Vector3(0f, 0f, -30f),
            };

            float minH = float.MaxValue;
            float maxH = float.MinValue;
            foreach (var p in samples)
            {
                float h = TerrainGenerator.GetHeightAt(p.x, p.z, BiomeType.Plains, SEED);
                minH = Mathf.Min(minH, h);
                maxH = Mathf.Max(maxH, h);
            }
            Assert.Less(maxH - minH, 0.3f,
                $"스폰({spawn.x:F0},{spawn.z:F0}) 반경 30m 내 고도 편차 = {maxH - minH:F3}m (R2 스폰 30m 평탄 목표)");
        }

        // ── d) 방위별 진폭 분포: 각 방위 50샘플 std>0 ─────────────────────────
        // 각 방위(동0/북90/서180/남270) 중심 부근에서 시드 결정론 오프셋 50샘플의 표준편차.
        // 현재도 방위별 고유 진폭/시드로 std>0 보장. R2에서 방위별 차별화가 의미 완성.
        [Test]
        public void AzimuthHeights_NonZeroSpread_AllDirections()
        {
            const float radius = 700f;
            float[] centers = { 0f, 90f, 180f, 270f };
            foreach (float theta in centers)
            {
                float[] hs = new float[50];
                for (int i = 0; i < 50; i++)
                {
                    float a = (theta + (i - 25f) * 1.2f) * Mathf.Deg2Rad; // 반원 ~60° 스캔
                    float x = radius * Mathf.Cos(a);
                    float z = radius * Mathf.Sin(a);
                    hs[i] = TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, SEED);
                }
                float std = StdDev(hs);
                Assert.Greater(std, 0f, $"방위 {theta}° {radius}m 50샘플 표준편차 = {std:F4}");
            }
        }

        // ── W1) 호수 수면-지형 정합: 수변 밴드 포함 전 구역 역전 0 ──────────
        // Phase W1 (지형이 물에 맞춘다): 수역(0~1.0r) depth 카브 + 수변 밴드(1.0r~1.45r)
        // waterLevel-0.4m smoothstep 수렴 + 분지 안전가드(≤ waterLevel-0.2).
        // 호수 6개 × {중심, 0.5r, 0.9r, 1.2r, 1.4r} × 2방위 샘플에서 지형 < waterLevel 100%.
        // 역전(수면 위 솟는 지형)이 있으면 Enforce=물 올리기가 발동해 "치솟은 판"이 재발한다.
        [Test]
        public void LakeBasins_NoInversion_AtShoreRing()
        {
            var lakes = TerrainGenerator.Lakes;
            Assert.GreaterOrEqual(lakes.Count, 6, "호수 6개 이상 필요");

            float[] fractions = { 0f, 0.5f, 0.9f, 1.2f, 1.4f };   // 중심 + 수역 2 + 수변 밴드 2
            float[] angles = { 0f, 90f * Mathf.Deg2Rad };          // 두 방위로 구릉 방향성 커버
            int total = 0;
            int violations = 0;
            string worst = "";

            for (int i = 0; i < lakes.Count; i++)
            {
                var lake = lakes[i];
                float wl = lake.waterLevel;
                foreach (float frac in fractions)
                {
                    if (frac <= 0f)
                    {
                        // 중심 — 가장 깊은 카브 지점
                        total++;
                        float h = TerrainGenerator.GetHeightAt(lake.center.x, lake.center.z, BiomeType.Plains, SEED);
                        if (!(h < wl)) { violations++; worst += $"  L{i} 중심 h={h:F3} ≥ wl={wl:F3}\n"; }
                        continue;
                    }
                    foreach (float ang in angles)
                    {
                        float x = lake.center.x + Mathf.Cos(ang) * lake.radius * frac;
                        float z = lake.center.z + Mathf.Sin(ang) * lake.radius * frac;
                        total++;
                        float h = TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, SEED);
                        if (!(h < wl)) { violations++; worst += $"  L{i} {frac:F1}r h={h:F3} ≥ wl={wl:F3}\n"; }
                    }
                }
            }

            Assert.Zero(violations,
                $"호수 수면 역전 {violations}/{total} (계획 요구 0).\n{worst}");
        }

        // ── X2) 흰 알파 마스크 감지 가드 ─────────────────────────────────
        // Idyllic 잔디 카드 마스크(RGB 흰색+0/255 이분형 알파)가 알베도로 유입되어
        // 지면이 순백이 된 사고(스크린샷 43) 재발 방지용 IsWhiteAlphaMask 검증.
        // 임의 생성한 흰색+이분형 알파 텍스처는 true, 실물 색 알베도(Grass)는 false여야 함.
        [Test]
        public void WhiteMaskDetection_IdentifiesMasks()
        {
            // ── 1) 흰색 RGB + 0/255 이분형 알파 체커보드 → 마스크로 판정 (true) ──
            var mask = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    bool full = ((x + y) % 2) == 0;
                    mask.SetPixel(x, y, new Color(1f, 1f, 1f, full ? 1f : 0f));
                }
            }
            mask.Apply();
            Assert.IsTrue(TerrainTextureApplier.IsWhiteAlphaMask(mask),
                "순백 RGB + 0/255 이분형 알파 텍스처는 알파 마스크로 판정되어야 한다.");

            // ── 2) 실물 색 알베도 → false (마스크 오판 금지) ──
            // X1으로 실물 알베도로 교체된 동/북 잔디를 Resources 로드 경유로 검증.
            // (EditMode에서 Resources 폴더 밖 Idyllic 원본 Grass_Albedo는 직접 로드 불가 →
            //   Resources 하위 real albedo로 검증, 실패 시 색 합성 텍스처로 폴백)
            Texture2D albedo = Resources.Load<Texture2D>("Models/UserProvided/terrain/textures_idyllic/north_grass_albedo");
            if (albedo != null)
            {
                Assert.IsFalse(TerrainTextureApplier.IsWhiteAlphaMask(albedo),
                    $"실물 색 알베도({albedo.name})는 알파 마스크로 오판해서는 안 된다.");
            }
            else
            {
                // Resources 로드 불가 시 색 있는 합성 텍스처로 폴백 검증 (알파 전부 255 → 비이분형)
                var fallback = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        fallback.SetPixel(x, y, new Color(0.3f, 0.6f, 0.2f, 1f));
                fallback.Apply();
                Assert.IsFalse(TerrainTextureApplier.IsWhiteAlphaMask(fallback),
                    "색 있는 알베도는 알파 마스크로 오판해서는 안 된다.");
            }
        }

        static float StdDev(float[] vals)
        {
            float mean = 0f;
            foreach (var v in vals) mean += v;
            mean /= vals.Length;
            float acc = 0f;
            foreach (var v in vals) { float d = v - mean; acc += d * d; }
            return Mathf.Sqrt(acc / vals.Length);
        }
    }
}