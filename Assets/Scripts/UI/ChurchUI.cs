using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.UI
{
    /// <summary>
    /// 성당 UI — IMGUI 기반. 기부, 친밀도 표시.
    /// UIManager를 통해 열기/닫기.
    /// </summary>
    public class ChurchUI : UIWindow
    {
        private string _currentTerritoryId = "default";
        private string _statusMessage = "";
        private double _messageTimer;

        public void SetTerritory(string territoryId)
        {
            _currentTerritoryId = territoryId ?? "default";
        }

        public override void Show()
        {
            base.Show();
            _statusMessage = "";
        }

        protected override void DrawWindowContent()
        {
            if (ChurchSystem.Instance == null)
            {
                GUILayout.Label("ChurchSystem이 없습니다.");
                return;
            }

            int favor = ChurchSystem.Instance.GetFavor();

            GUILayout.Label($"⛪ 성당 — {_currentTerritoryId}", GUI.skin.box);

            // 친밀도 프로그레스바
            GUILayout.Space(10);
            GUILayout.Label($"친밀도: {favor}/100");
            DrawFavorBar(favor);
            GUILayout.Label(ChurchSystem.Instance.GetFavorLevelText());

            // 혜택 설명
            GUILayout.Space(5);
            GUILayout.Box(ChurchSystem.Instance.GetFavorBenefitsText());

            // 기부 버튼
            GUILayout.Space(10);
            GUILayout.Label("— 금화 기부 —", GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("10골드\n(+1 친밀도)"))
                Donate(10);
            if (GUILayout.Button("50골드\n(+5 친밀도)"))
                Donate(50);
            if (GUILayout.Button("100골드\n(+10 친밀도)"))
                Donate(100);
            GUILayout.EndHorizontal();

            // 영주 대면 버튼 (조건부)
            GUILayout.Space(10);
            bool canAudience = ChurchSystem.Instance.CanRequestAudience();
            if (canAudience)
            {
                if (GUILayout.Button("👑 영주 대면 요청 (친밀도 80+)"))
                {
                    // TODO: Phase 5.7.5 영주 대면 UI
                    Debug.Log($"[Church] 영주 대면 요청: {_currentTerritoryId}");
                }
            }
            else
            {
                GUILayout.Label("(영주 대면: 친밀도 80 필요)", GUI.skin.box);
            }

            // 상태 메시지 (3초 표시)
            if (_messageTimer > 0 && Time.time < _messageTimer)
            {
                GUILayout.Space(5);
                GUILayout.Label(_statusMessage);
            }
        }

        private void DrawFavorBar(int favor)
        {
            var rect = GUILayoutUtility.GetRect(200, 20);
            GUI.Box(rect, "");

            float pct = favor / 100f;
            Color barColor;
            if (favor >= 80) barColor = Color.green;
            else if (favor >= 60) barColor = Color.yellow;
            else if (favor >= 40) barColor = new Color(1, 0.6f, 0);
            else barColor = Color.red;

            Color old = GUI.color;
            GUI.color = barColor;
            GUI.Box(new Rect(rect.x, rect.y, rect.width * pct, rect.height), "");
            GUI.color = old;
        }

        private void Donate(int amount)
        {
            int spent = ChurchSystem.Instance.DonateGold(amount);
            if (spent > 0)
            {
                _statusMessage = $"✅ {spent}골드 기부 완료! 친밀도 +{spent / 10}";
                _messageTimer = Time.time + 3;
            }
            else
            {
                _statusMessage = "❌ 골드가 부족합니다.";
                _messageTimer = Time.time + 3;
            }
        }
    }
}