using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [Milestone F] GLB 부재 목록 산출 도구.
    /// 등록된 모든 아이템(id 보유 ItemData)에 대해 RuntimeModelLoader.HasModel(ResolveModelKey 관례)로
    /// GLB 모델 존재 여부를 판정해, GLB가 없는 아이템 목록을 텍스트 파일로 출력한다.
    ///
    /// - 메뉴: Tools/Item Audit/GLB 부재 목록 산출
    /// - 출력: Assets/ItemGLB_MissingList.txt (길면 Editor.log에도 요약)
    /// </summary>
    public static class ItemGLBAudit
    {
        [MenuItem("Tools/Item Audit/GLB 부재 목록 산출")]
        public static void AuditMissingGLB()
        {
            // PlayerInventory의 모든 정적 ItemData 수집
            var items = new List<PlayerInventory.ItemData>();
            var fields = typeof(PlayerInventory).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var seen = new HashSet<string>();
            foreach (var f in fields)
            {
                if (f.FieldType != typeof(PlayerInventory.ItemData)) continue;
                var item = f.GetValue(null) as PlayerInventory.ItemData;
                if (item != null && !string.IsNullOrEmpty(item.id) && seen.Add(item.id))
                    items.Add(item);
            }

            var missing = new List<PlayerInventory.ItemData>();
            var present = new List<PlayerInventory.ItemData>();
            foreach (var it in items)
            {
                if (ResolveHasModel(it.id)) present.Add(it);
                else missing.Add(it);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== 아이템 GLB 부재 목록 (총 {items.Count} 등록 / GLB 없음 {missing.Count}) ===");
            sb.AppendLine();
            foreach (var m in missing)
            {
                sb.AppendLine($"[{m.category}] {m.id}  —  {m.displayName}");
            }

            string path = "Assets/ItemGLB_MissingList.txt";
            System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[ItemGLBAudit] GLB 부재 목록 산출 완료: 총 {items.Count}개 중 GLB 없음 {missing.Count}개 → {path}");
            Debug.Log(sb.ToString());
        }

        /// <summary>GblItemIconRenderer.ResolveModelKey 관례와 동일한 1차 판정 — item.id 직접 + 접두 제거.</summary>
        private static bool ResolveHasModel(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (RuntimeModelLoader.HasModel(itemId)) return true;
            string stripped = StripPrefix(itemId);
            if (!string.IsNullOrEmpty(stripped) && stripped != itemId && RuntimeModelLoader.HasModel(stripped)) return true;
            return false;
        }

        private static string StripPrefix(string id)
        {
            // weapon_/armor_/helmet_/boots_/gloves_/tool_/mat_/ring_/necklace_ 등 접두 제거 시도
            foreach (var p in new[] { "weapon_", "armor_", "helmet_", "boots_", "gloves_", "tool_", "mat_", "ring_", "necklace_" })
            {
                if (id.StartsWith(p)) return id.Substring(p.Length);
            }
            return null;
        }
    }
}
