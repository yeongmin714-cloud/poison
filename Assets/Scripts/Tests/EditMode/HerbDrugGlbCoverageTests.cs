using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [162차] 약초/마약/조합물약/버프물약 전종 파생 GLB 존재 계약 — 파일 수용만으로 아이콘 자동해결(관례후보1) 검증.
    /// 배경: 137종(약초 40 + 마약 10 + 조합 82 + 버프 5)에 파생 GLB 베이크(Tools/derivative_bake.py + map_162_*.json).
    /// 계약:
    ///  ① 약초 40종 — id(A1~P10) 그대로 모델키 해석 + Resources 로드.
    ///  ② 마약 10종 — DrugEffectSystem 실측 id 체계 drug_{stage:D2} ↔ 파일명 정합.
    ///  ③ 조합 82종 — HerbComboDatabase resultId(combo_{재료1id}_{재료2id}) ↔ GLB 파일명 1:1 (Phase 4 id 정비와 정합).
    ///  ④ 버프 5종 — PotionBuffData 표준 id ↔ GLB 파일명 1:1.
    ///  ⑤ 조합 파생 병 구조 — 유리/액체/코르크 분리 렌더러 유지([92차] 계약 동일 기준).
    /// </summary>
    public class HerbDrugGlbCoverageTests
    {
        private static bool HasModel(string key) => RuntimeModelLoader.HasModel(key);

        [Test]
        public void HerbGlb_AllForty_ExistAndResolveById()
        {
            // [162차] 약초 id 직결 — HerbDatabase 전종(40) 기준.
            var herbs = HerbDatabase.AllHerbs;
            Assert.That(herbs, Is.Not.Null.And.Not.Empty, "[162] HerbDatabase 로드");

            foreach (var herb in herbs)
            {
                var item = new PlayerInventory.ItemData { id = herb.id, displayName = herb.displayName };
                string key = GblItemIconRenderer.ResolveModelKey(item);
                Assert.That(key, Is.Not.Null.And.Not.Empty,
                    "[162] 약초 아이콘 모델키 해석(관례후보1 — id 그대로): " + herb.id);
                Assert.That(HasModel(key), Is.True,
                    "[162] 약초 GLB 리소스 로드 가능: " + herb.id + " → " + key);
            }
            Assert.That(herbs.Count, Is.EqualTo(40), "[162] 약초 전종 수");
        }

        [Test]
        public void DrugGlb_AllTen_ExistMatchingDrugStageId()
        {
            // [162차] 마약 — DrugEffectSystem 실측 체계 drug_{stage:D2} (Core/Utils/CookingDrugHelper.cs 동일).
            for (int stage = 1; stage <= 10; stage++)
            {
                string id = $"drug_{stage:D2}";
                Assert.That(HasModel(id), Is.True, "[162] 마약 GLB 리소스: " + id);
                var go = Resources.Load<GameObject>("Models/UserProvided/" + id.ToLowerInvariant());
                Assert.That(go, Is.Not.Null, "[162] 마약 GLB 직접 로드: " + id);
            }
        }

        [Test]
        public void ComboGlb_AllEightyTwo_ExistMatchingResultId()
        {
            // [162차] 조합 — resultId(combo_{A1}_{P2}) ↔ 파일명 combo_a1_p2.glb 소문자 정합.
            var combos = HerbComboDatabase.AllCombos;
            Assert.That(combos, Is.Not.Null.And.Not.Empty, "[162] HerbComboDatabase 로드");
            Assert.That(combos.Count, Is.GreaterThanOrEqualTo(82),
                "[162] 조합 종수(2.2 정신성 22행 포함 — GAME_DATA 표기 80종 불일치와 무관, 실제 행 기준)");

            foreach (var kv in combos)
            {
                string id = kv.Value.resultId;
                Assert.That(id, Does.Match(@"^combo_[A-Z]\d{1,2}_[A-Z]\d{1,2}$"),
                    "[162] 조합 id 결정론 형식(Phase 4 정비): " + id);
                Assert.That(HasModel(id), Is.True, "[162] 조합 GLB 리소스: " + id);
            }
        }

        [Test]
        public void BuffGlb_AllFive_ExistMatchingPotionBuffId()
        {
            // [162차] 버프 물약 — PotionBuffData 표준 id ↔ GLB 1:1.
            foreach (var kv in PotionBuffData.AllEffects)
            {
                Assert.That(HasModel(kv.Key), Is.True, "[162] 버프 GLB 리소스: " + kv.Key);
                var go = Resources.Load<GameObject>("Models/UserProvided/" + kv.Key);
                Assert.That(go, Is.Not.Null, "[162] 버프 GLB 직접 로드: " + kv.Key);
            }
            Assert.That(PotionBuffData.AllEffects.Count, Is.EqualTo(5), "[162] 표준 버프 종수");
        }

        [Test]
        public void ComboDerivative_LiquidGlassSeparation_Contract()
        {
            // [162차] 조합 파생 병 구조 계약 — 유리/액체/코르크 분리 렌더러 유지([92차]와 동일 기준).
            var go = Resources.Load<GameObject>("Models/UserProvided/combo_a1_a2");
            Assert.That(go, Is.Not.Null, "[162] 조합 파생 GLB 로드(combo_a1_a2)");
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(3),
                "[162] 파생 병 유리/액체/코르크 분리 렌더러 유지");
        }

        [Test]
        public void HerbShape_MeshBounds_AllFortyUnique()
        {
            // [162-2차] 허브 40종 모양 유니크 계약 — 임포트된 메시 bounds(size)로
            // (높이, 최대폭) 서명을 4자리 정규화해 전종 유니크여야 한다(중복 시 id 목록 노출).
            var sigs = new System.Collections.Generic.Dictionary<string, string>();
            var dups = new System.Text.StringBuilder();
            int count = 0;
            foreach (var herb in HerbDatabase.AllHerbs)
            {
                var go = Resources.Load<GameObject>("Models/UserProvided/" + herb.id.ToLowerInvariant());
                Assert.That(go, Is.Not.Null, "[162-2차] GLB 프리팹: " + herb.id);
                var mf = go.GetComponentInChildren<MeshFilter>(true);
                Assert.That(mf, Is.Not.Null, "[162-2차] MeshFilter: " + herb.id);
                Assert.That(mf.sharedMesh, Is.Not.Null, "[162-2차] sharedMesh: " + herb.id);
                var s = mf.sharedMesh.bounds.size;
                Assert.That(s.y > 0f && Mathf.Max(s.x, s.z) > 0f, Is.True,
                    "[162-2차] 비정형 크기(0) 방지: " + herb.id);
                string sig = $"h={s.y:F4} w={Mathf.Max(s.x, s.z):F4}";
                count++;
                if (sigs.TryGetValue(sig, out var other))
                    dups.AppendLine($"{herb.id} == {other} ({sig})");
                else
                    sigs[sig] = herb.id;
            }
            Assert.That(dups.Length, Is.EqualTo(0),
                "[162-2차] 허브 40종 모양 서명 유니크 위반:\n" + dups);
            Assert.That(count, Is.EqualTo(40), "[162-2차] 허브 전종 수");
        }
    }
}
