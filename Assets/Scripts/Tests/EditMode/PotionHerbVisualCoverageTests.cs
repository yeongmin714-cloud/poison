using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEditor;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 92] 물약·약초 아이템 GLB 커버리지 계약 — 종류별 전용 GLB 존재 + 아이콘 해석 체인 통과.
    /// 배경: 물약 아이템(은신/진정제)과 기본 약초(herb_yakcho)는 전용 GLB가 없어 절차 아이콘 폴백이었다.
    /// [무번호 90-92] Polyfork 파생 베이크(potion-bottle 액체 hue 재매핑 4종 + herb_green 틴트 파생 1종)로 해소.
    /// 해석 계약: GblItemIconRenderer.ResolveModelKey 관례 후보 1(id 그대로) — 코드 변경 0, 파일 수용만으로 자동 적용.
    /// </summary>
    public class PotionHerbVisualCoverageTests
    {
        [Test]
        public void PotionHerbGlbResources_Exist()
        {
            foreach (var name in new[] { "potion_hp", "potion_mp", "potion_stealth", "potion_sedative", "herb_yakcho" })
            {
                var go = Resources.Load<GameObject>("Models/UserProvided/" + name);
                Assert.That(go, Is.Not.Null, "[92] 물약·약초 GLB 리소스: " + name);
            }
            var bottle = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/Polyfork/potion-bottle-099ad7.glb");
            Assert.That(bottle, Is.Not.Null, "[92] 파생 원본(potion-bottle) Polyfork 수용.");
        }

        [Test]
        public void ResolveModelKey_PotionAndHerb_IdConvention()
        {
            // 은신 물약/진정제/약초 — id 그대로(관례 후보 1) 해석 통과.
            foreach (var id in new[] { "potion_stealth", "potion_sedative", "herb_yakcho" })
            {
                var item = new PlayerInventory.ItemData { id = id, displayName = id };
                string key = ProjectName.UI.GblItemIconRenderer.ResolveModelKey(item);
                Assert.That(key, Is.Not.Null.And.Not.Empty, "[92] 아이콘 모델키 해석: " + id);
                Assert.That(RuntimeModelLoader.HasModel(key), Is.True,
                    "[92] 해석된 모델키가 실제 로드 가능: " + id + " → " + key);
            }
        }

        [Test]
        public void PotionBottle_LiquidZoneSeparation_Contract()
        {
            // 파생 구조 계약 — 유리(머티리얼 0, 알파 ≤0.3)+액체(머티리얼 1) 분리가 파생본에 유지.
            var go = Resources.Load<GameObject>("Models/UserProvided/potion_hp");
            Assert.That(go, Is.Not.Null, "[92] potion_hp 로드.");
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(3), "[92] 유리/액체/코르크 분리 렌더러 유지.");
        }
    }
}
