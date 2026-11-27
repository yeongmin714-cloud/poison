using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [139차 P2] 캐릭터 키 균일 계약 — 병사/주민/플레이어 전 씬 1.8m(사용자 계약).
    /// 근거(139차 P2 진단 프로브 TestOutput/npc139_heights_diag.txt):
    ///  · 병사 SMR 본체는 NormalizeSoldierHeight 후 1.800 정상 — 3.289m은 월드공간 이름표 렌더러가
    ///    bounds에 끼인 측정 오염(본 테스트는 SMR-only 측정으로 오염 배제).
    ///  · lv20+ 덩치 배율(GetSoldierSizeMultiplier ×1.8)이 정규화 위에 곱산 = 3.24m 거인 → [139차 P2] 폐기(1.0).
    ///  · 플레이어 캘리브레이션 적용 후 1.800 ✓ — 주민 EnsureVillageNpcHeight 경로 1.8 ✓.
    /// </summary>
    public class CharacterHeightUniformTests
    {
        private const float TargetHeight = 1.8f;
        private const float Tolerance = 0.05f;

        [Test]
        public void SoldierSizeMultiplier_IsUniformOne_AllLevels_139P2()
        {
            int[] levels = { 1, 10, 19, 20, 25, 40, 50 };
            foreach (int lv in levels)
            {
                Assert.AreEqual(1.0f, GuardManager.GetSoldierSizeMultiplier(lv), 0.001f,
                    $"[139차 P2] lv{lv} 병사 배율은 균일 계약(1.0)이어야 한다 — 덩치 배율 폐기.");
            }
        }

        [Test]
        public void SoldierNormalized_StandsAt1Point8_SmrOnly_139P2()
        {
            var prefab = Resources.Load<GameObject>("Models/UserProvided/fbx/soldier_lv1-20_rigged");
            if (prefab == null)
                prefab = Resources.Load<GameObject>("Models/UserProvided/Soldier_Lv1-20_Rigged");
            if (prefab == null)
            {
                Assert.Inconclusive("[139차 P2] 병사 FBX 프리팹 부재 — 리소스 경로 확인 필요");
                return;
            }
            var go = Object.Instantiate(prefab);
            try
            {
                TerritoryBuilder.NormalizeSoldierHeight(go, TerritoryBuilder.SoldierTargetHeight);
                float h = MeasureSmrOnlyHeight(go);
                Assert.AreEqual(TargetHeight, h, Tolerance,
                    $"[139차 P2] 정규화 후 병사 SMR 키는 1.8m 계약(실측 {h:F3}m).");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void VillagerCalibrated_StandsAt1Point8_139P2()
        {
            var prefab = Resources.Load<GameObject>("Models/UserProvided/Npc2026/Npc_Girl_1/Npc_Girl_1_rigged");
            if (prefab == null)
            {
                Assert.Inconclusive("[139차 P2] Npc_Girl_1_rigged 프리팹 부재 — 리소스 경로 확인 필요");
                return;
            }
            var go = Object.Instantiate(prefab);
            try
            {
                VillageNpcSpawner.EnsureVillageNpcHeight(go, 0f, "T139Test");
                float h = MeasureSmrOnlyHeight(go);
                Assert.AreEqual(TargetHeight, h, Tolerance,
                    $"[139차 P2] 캘리브레이션 후 주민 키는 1.8m 계약(실측 {h:F3}m).");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>[139차 P2] SMR-only 키 측정 — 이름표/UI 렌더러 오염 배제(3.289m 아티팩트 재발 방지).</summary>
        private static float MeasureSmrOnlyHeight(GameObject go)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || !smr.enabled) continue;
                Bounds b = smr.bounds;
                if (b.min.y < min) min = b.min.y;
                if (b.max.y > max) max = b.max.y;
            }
            if (min > max) return 0f;
            return max - min;
        }
    }
}
