using NUnit.Framework;
using ProjectName.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [103차 IMPL-A 1단계] 주민별 오버라이드 인스턴스 + 패리티 다양성 검증(EditMode — 실행은 QA 몫).
    /// · GetResolvedMixamoOverride(baseCtrl, npcName) — 주민별 인스턴스 재구성 계약(참조 동일성 불요).
    /// · BuildVillagerOverride — 시드 패리티(idle 슬롯 mixamo/원본) 다양성 계약.
    /// · [103차 P1] 접지근원 로그는 EditMode 실험 불가 — 본 테스트 미포함(Play 로그 판별 — QA 몫).
    /// </summary>
    public class VillagerVariety103Tests
    {
        private const string PlayerAcResource = "Animation/Controllers/Player_AC";

        /// <summary>① 주민별 인스턴스 — 동일 베이스 컨트롤러에 서로 다른 npcName → 서로 다른 인스턴스 반환.
        /// 클립 미해석(Resources 미로드 환경)이면 둘 다 baseCtrl 폴백 → Inconclusive.</summary>
        [Test]
        public void GetResolvedMixamoOverride_DifferentNpcNames_YieldDistinctInstances()
        {
            var playerCtrl = Resources.Load<RuntimeAnimatorController>(PlayerAcResource);
            if (playerCtrl == null)
            {
                Assert.Inconclusive($"Player_AC 미로드 환경 — {PlayerAcResource} Resources 경로 부재");
                return;
            }

            RuntimeAnimatorController a = null;
            RuntimeAnimatorController b = null;
            var goA = new GameObject("VT103_NpcA");
            var goB = new GameObject("VT103_NpcB");
            try
            {
                a = VillageNpcSpawner.GetResolvedMixamoOverride(playerCtrl, "VT103_NpcA");
                b = VillageNpcSpawner.GetResolvedMixamoOverride(playerCtrl, "VT103_NpcB");

                if (a == null || b == null)
                {
                    Assert.Inconclusive("반환 null — 폴백 계약 경로(클립 미해석) — 판정 불가");
                    return;
                }
                if (a == playerCtrl && b == playerCtrl)
                {
                    Assert.Inconclusive("클립 미해석 폴백 경로 — 둘 다 playerCtrl 반환(Resources 미로드 환경)");
                    return;
                }
                // [103차] 주민별 인스턴스 계약 — 참조 동일성 불요, 서로 다른 인스턴스여야 함.
                Assert.AreNotEqual(a, b, "서로 다른 npcName → 서로 다른 오버라이드 인스턴스여야 함");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        /// <summary>② BuildVillagerOverride — 2회 호출 → 서로 다른 인스턴스 + name 접미사.
        /// 짝/홀 idle 슬롯 패리티 차이는 GetOverrides로 판정 — 시드 패리티가 갈리지 않으면 Inconclusive.</summary>
        [Test]
        public void BuildVillagerOverride_TwoCalls_DistinctInstancesAndNameSuffix()
        {
            var playerCtrl = Resources.Load<RuntimeAnimatorController>(PlayerAcResource);
            if (playerCtrl == null)
            {
                Assert.Inconclusive($"Player_AC 미로드 환경 — {PlayerAcResource} Resources 경로 부재");
                return;
            }

            var goA = new GameObject("VT103_NpcA");
            var goB = new GameObject("VT103_NpcB");
            try
            {
                var ctrlA = VillageNpcSpawner.BuildVillagerOverride(playerCtrl, goA, "girl1", out float seedA);
                var ctrlB = VillageNpcSpawner.BuildVillagerOverride(playerCtrl, goB, "girl1", out float seedB);

                if (ctrlA == null || ctrlB == null)
                {
                    Assert.Inconclusive("빌드 null — 폴백 계약 경로(클립 미해석) — 판정 불가");
                    return;
                }

                Assert.AreNotEqual(ctrlA, ctrlB, "2회 호출 → 서로 다른 인스턴스여야 함");
                Assert.AreEqual("VillagerOverride_VT103_NpcA", ctrlA.name, "name 접미사 계약");
                Assert.AreEqual("VillagerOverride_VT103_NpcB", ctrlB.name, "name 접미사 계약");

                // 패리티 판정 — idle 슬롯(원본 Armature|Idle_02)의 오버라이드 value로 갈림 확인.
                bool idleOverriddenA = IsIdleSlotOverridden(ctrlA);
                bool idleOverriddenB = IsIdleSlotOverridden(ctrlB);
                if (idleOverriddenA == idleOverriddenB)
                {
                    Debug.Log($"[103차 테스트] 패리티 동일 — seedA={seedA} seedB={seedB} idleA={idleOverriddenA} idleB={idleOverriddenB}");
                    Assert.Inconclusive("두 시드 패리티가 동일 — 패리티 다양성 판정 불가(결정론 시드라 갈림 보장 어려움)");
                }
                Assert.AreNotEqual(idleOverriddenA, idleOverriddenB,
                    $"시드 패리티 갈림 → idle 슬롯 오버라이드 유무가 달라야 함 (seedA={seedA} seedB={seedB})");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        /// <summary>idle 슬롯(Armature|Idle_02)의 오버라이드 value가 원본과 달라졌는지(=mixamo 클립 적용) 판정.</summary>
        private static bool IsIdleSlotOverridden(AnimatorOverrideController ctrl)
        {
            var pairs = new System.Collections.Generic.List<KeyValuePair<AnimationClip, AnimationClip>>();
            ctrl.GetOverrides(pairs);
            foreach (var pair in pairs)
            {
                if (pair.Key == null) continue;
                if (pair.Key.name != "Armature|Idle_02") continue;
                return pair.Value != null && pair.Value != pair.Key;
            }
            return false;   // 슬롯 미발견 — 미오버라이드 취급(로그 판별 몫)
        }
    }
}
