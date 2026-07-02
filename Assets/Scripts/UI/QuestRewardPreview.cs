using System.Text;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 🌟 퀘스트 보상 미리보기 — IMGUI 정적 헬퍼
    /// QuestJournalUI / QuestWindow에서 보상 요약 및 툴팁 표시에 사용합니다.
    /// </summary>
    public static class QuestRewardPreview
    {
        private static StringBuilder _sb = new StringBuilder(128);

        /// <summary>
        /// 한 줄 요약 문자열 (예: "💰50 ⭐100")
        /// 보상이 없으면 "???" 반환
        /// </summary>
        public static string GetRewardSummary(QuestData quest)
        {
            if (quest.reward.IsEmpty)
                return "???";

            _sb.Clear();

            if (quest.reward.gold > 0)
                _sb.Append($"💰{quest.reward.gold} ");

            if (quest.reward.exp > 0)
                _sb.Append($"⭐{quest.reward.exp} ");

            if (quest.reward.affinity > 0)
                _sb.Append($"📈+{quest.reward.affinity} ");

            if (quest.reward.items != null && quest.reward.items.Count > 0)
                _sb.Append($"🎁{quest.reward.items.Count}종 ");

            string result = _sb.ToString().TrimEnd();
            return string.IsNullOrEmpty(result) ? "???" : result;
        }

        /// <summary>
        /// questId로 보상 요약 조회 (QuestManager lookup)
        /// </summary>
        public static string GetRewardSummary(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return "???";
            QuestData qd = QuestManager.GetQuest(questId);
            return GetRewardSummary(qd);
        }

        /// <summary>
        /// 상세 툴팁 문자열 (여러 줄)
        /// </summary>
        public static string GetRewardDetail(QuestData quest)
        {
            if (quest.reward.IsEmpty)
                return "──── 보상 ────\n???\n(보상 정보 없음)";

            _sb.Clear();
            _sb.AppendLine("──── 보상 ────");

            var reward = quest.reward;

            if (reward.gold > 0)
                _sb.AppendLine($"💰 골드: {reward.gold}");
            else
                _sb.AppendLine($"💰 골드: ???");

            if (reward.exp > 0)
                _sb.AppendLine($"⭐ 경험치: {reward.exp}");
            else
                _sb.AppendLine($"⭐ 경험치: ???");

            if (reward.items != null && reward.items.Count > 0)
            {
                for (int i = 0; i < reward.items.Count; i++)
                {
                    var item = reward.items[i];
                    string itemName = item != null && !string.IsNullOrEmpty(item.displayName)
                        ? item.displayName
                        : (item != null && !string.IsNullOrEmpty(item.id) ? item.id : "???");
                    _sb.AppendLine($"🎁 아이템: {itemName}");
                }
            }
            else
            {
                _sb.AppendLine($"🎁 아이템: ???");
            }

            if (reward.affinity > 0)
                _sb.AppendLine($"📈 호감도: 영주 +{reward.affinity}");
            else
                _sb.AppendLine($"📈 호감도: ???");

            return _sb.ToString().TrimEnd();
        }

        /// <summary>
        /// questId로 보상 상세 조회 (QuestManager lookup)
        /// </summary>
        public static string GetRewardDetail(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return "──── 보상 ────\n???";
            QuestData qd = QuestManager.GetQuest(questId);
            return GetRewardDetail(qd);
        }

        /// <summary>
        /// IMGUI 툴팁 박스 그리기 — 마우스 위치 근처에 표시
        /// </summary>
        public static void DrawTooltip(string text, Vector2 mousePos, float maxWidth = 280f)
        {
            if (string.IsNullOrEmpty(text)) return;

            float lineHeight = 20f;
            string[] lines = text.Split('\n');
            float width = maxWidth;
            float height = lines.Length * lineHeight + 10f;

            float tx = mousePos.x + 12f;
            float ty = mousePos.y + 12f;

            // 화면 밖으로 나가지 않도록 조정
            if (tx + width > Screen.width)
                tx = mousePos.x - width - 12f;
            if (ty + height > Screen.height)
                ty = mousePos.y - height - 12f;

            // 배경
            Color prevColor = GUI.color;
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            GUI.Box(new Rect(tx, ty, width, height), "");
            GUI.color = prevColor;

            // 텍스트
            Rect labelRect = new Rect(tx + 6f, ty + 4f, width - 12f, height - 8f);
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = true,
                wordWrap = true,
                normal = { textColor = new Color(0.92f, 0.92f, 0.95f, 1f) }
            };

            GUI.Label(labelRect, text, style);
        }
    }
}