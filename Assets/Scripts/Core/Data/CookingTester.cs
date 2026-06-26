using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Simple test to verify cooking recipes are loaded correctly.
    /// Attach to any GameObject in the scene (e.g., via GameManager).
    /// </summary>
    public class CookingTester : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[CookingTester] Awake");
            TestRecipes();
        }

        private void TestRecipes()
        {
            // Force initialization
            var all = CookingDatabase.AllRecipes;
            Debug.Log($"[CookingTester] Total recipes loaded: {all.Count}");

            // Define some known examples from the table to verify
            var testCases = new[]
            {
                new { meat = "토끼 고기", herb = "회복꽃", expectedDish = "토끼 허브 구이" },
                new { meat = "늑대 고기", herb = "쓴풀", expectedDish = "야성적인 육포" },
                new { meat = "멧돼지 고기", herb = "끈끈이풀", expectedDish = "끈적한 돼지 수육" },
                new { meat = "사슴 고기", herb = "향기꽃", expectedDish = "꽃향기 스테이크" },
                new { meat = "뱀 고기", herb = "신경마비꽃", expectedDish = "마비 뱀 꼬치" },
                new { meat = "박쥐 고기", herb = "안개꽃", expectedDish = "박쥐 날개 튀김" },
                new { meat = "거대 쥐 고기", herb = "썩은덩굴", expectedDish = "불결한 고기 스튜" },
                new { meat = "까마귀 고기", herb = "환각포자", expectedDish = "환각 까마귀 구이" },
                new { meat = "곰 고기", herb = "근육강화풀", expectedDish = "곰 쓸개 탕" },
                new { meat = "여우 고기", herb = "시야확장풀", expectedDish = "여우 눈알 요리" },
            };

            foreach (var tc in testCases)
            {
                var result = CookingDatabase.GetCooking(tc.meat, tc.herb);
                if (result.HasValue)
                {
                    var r = result.Value;
                    Debug.Log($"[CookingTester] {tc.meat} + {tc.herb} -> {r.dishName} (effect: {r.effect})");
                    if (!r.dishName.Equals(tc.expectedDish))
                    {
                        Debug.LogWarning($"[CookingTester] Mismatch: expected {tc.expectedDish}, got {r.dishName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[CookingTester] No recipe found for {tc.meat} + {tc.herb}");
                }
            }

            // Also test a few random ones to ensure lookup works
            Debug.Log("[CookingTester] Testing reverse lookup (herb + meat) should return null (order matters).");
            var rev = CookingDatabase.GetCooking("회복꽃", "토끼 고기");
            if (rev.HasValue)
                Debug.Log($"[CookingTester] Reverse lookup gave: {rev.Value.dishName} (unexpected)");
            else
                Debug.Log("[CookingTester] Reverse lookup correctly returned null.");
        }
    }
}
