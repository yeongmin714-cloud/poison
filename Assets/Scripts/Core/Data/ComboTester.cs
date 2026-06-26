using UnityEngine;

namespace ProjectName.Core.Data
{
    [AddComponentMenu("")]
    public class ComboTester : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[ComboTester] Awake");
            // Ensure databases are initialized
            var dummy = HerbDatabase.AllHerbs.Count;
            var combos = HerbComboDatabase.AllCombos.Count;
            Debug.Log($"[ComboTester] Herbs: {dummy}, Combos: {combos}");

            // Test a few known combos from the data
            TestCombo("A1", "A2"); // 쓴풀 + 가시덤불 -> 독성 가시액
            TestCombo("A4", "M1"); // 독가시꽃 + 향기꽃 -> 마비 환각제
            TestCombo("H1", "H2"); // 회복꽃 + 생명수뿌리 -> 만능 치유액
            TestCombo("P1", "P2"); // 잡초 + 맑은잎 -> 기초 접착제
            // Test reverse order
            TestCombo("A2", "A1");
            // Test non-existent
            TestCombo("A1", "Z99");
        }

        private void TestCombo(string id1, string id2)
        {
            var result = HerbComboDatabase.GetCombo(id1, id2);
            if (result.HasValue)
            {
                var r = result.Value;
                Debug.Log($"[ComboTester] {id1} + {id2} -> {r.resultName} ({r.effect})");
            }
            else
            {
                Debug.LogWarning($"[ComboTester] No combo found for {id1} + {id2}");
            }
        }
    }
}