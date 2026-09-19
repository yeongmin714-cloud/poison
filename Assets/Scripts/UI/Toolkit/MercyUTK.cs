// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Core.Data;   // TerritoryDatabase, TerritoryDefinition
using ProjectName.Systems;     // LordSurrenderSystem, TerritoryDifficulty?

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round B — 자비(처형/살려주기) 선택 창 (UTKWindowBase 파생).
    /// 원본: Assets/Scripts/Systems/MercyUI.cs (363줄, IMGUI) — 선택 콜백 경로를 그대로 실측해
    /// 처형/살려주기 2버튼 + 보상 팝업을 UTK로 재현. 원본은 절대 수정하지 않는다.
    ///
    /// [실측 콜백 — 원본 경로 동일]
    ///   정보: LordSurrenderSystem.GetLordData(territoryId) → LordData {personality, preferredFood}
    ///         LordSurrenderSystem.GetPersonalityName(personality)
    ///         TerritoryDatabase.Instance.GetDefinition(id) → TerritoryDefinition {territoryName, difficulty}
    ///   처형: LordSurrenderSystem.ExecuteLord(id) → 골드 GetExecuteGoldReward(difficulty)
    ///   살려주기: LordSurrenderSystem.SpareLord(id) → 골드 GetSpareGoldReward(difficulty), 충성도 +30
    ///
    /// [MercyUTK] 로그.
    /// </summary>
    public class MercyUTK : UTKWindowBase
    {
        private static MercyUTK _instance;

        private const float WinW = 440f;
        private const float WinH = 310f;

        private TerritoryId _territoryId;
        private string _lordName = "";

        private Label _msgLabel;
        private Label _lordLabel;
        private VisualElement _choiceHost;
        private VisualElement _rewardHost;
        private Label _rewardLabel;

        public static MercyUTK Instance => _instance;
        public static bool IsVisible => _instance != null && _instance.IsOpen;

        private MercyUTK() : base("🏳️ 영주 항복", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 16f;
            _content.style.paddingRight = 16f;
            _content.style.paddingTop = 10f;

            _lordLabel = new Label("");
            _lordLabel.style.fontSize = 20f;
            _lordLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _lordLabel.style.color = new StyleColor(Color.white);
            _lordLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _content.Add(_lordLabel);

            _msgLabel = new Label("영주가 항복했습니다. 처형하시겠습니까? 살려주시겠습니까?");
            _msgLabel.style.fontSize = 15f;
            _msgLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            _msgLabel.style.whiteSpace = WhiteSpace.Normal;
            _msgLabel.style.marginTop = 12f;
            _content.Add(_msgLabel);

            _choiceHost = new VisualElement();
            _choiceHost.style.flexDirection = FlexDirection.Row;
            _choiceHost.style.justifyContent = Justify.Center;
            _choiceHost.style.marginTop = 24f;
            _content.Add(_choiceHost);

            var executeBtn = UTKButton.Create("⚔️ 처형", OnExecute, UTKButton.Variant.Danger);
            executeBtn.style.width = 155f;
            executeBtn.style.height = 50f;
            executeBtn.style.marginRight = 18f;
            _choiceHost.Add(executeBtn);

            var spareBtn = UTKButton.Create("🤝 살려주기", OnSpare, UTKButton.Variant.Primary);
            spareBtn.style.width = 155f;
            spareBtn.style.height = 50f;
            _choiceHost.Add(spareBtn);

            var personality = new Label("");
            personality.name = "MercyInfo";
            personality.style.fontSize = 13f;
            personality.style.color = new StyleColor(UTKColor.TextSecondary);
            personality.style.marginTop = 18f;
            personality.style.unityTextAlign = TextAnchor.MiddleCenter;
            _content.Add(personality);

            // 보상 팝업 오버레이
            _rewardHost = new VisualElement();
            _rewardHost.style.position = Position.Absolute;
            _rewardHost.style.left = 0; _rewardHost.style.right = 0; _rewardHost.style.top = 0; _rewardHost.style.bottom = 0;
            _rewardHost.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));
            _rewardHost.style.alignItems = Align.Center;
            _rewardHost.style.justifyContent = Justify.Center;
            _rewardHost.style.display = DisplayStyle.None;
            Add(_rewardHost);

            var rewardBox = new VisualElement();
            rewardBox.style.width = 340f;
            rewardBox.style.height = 180f;
            rewardBox.style.backgroundColor = new StyleColor(UTKColor.BgPanel);
            rewardBox.style.borderTopWidth = 2; rewardBox.style.borderBottomWidth = 2;
            rewardBox.style.borderLeftWidth = 2; rewardBox.style.borderRightWidth = 2;
            rewardBox.style.borderTopColor = rewardBox.style.borderBottomColor =
                rewardBox.style.borderLeftColor = rewardBox.style.borderRightColor = new StyleColor(UTKColor.BorderGold);
            _rewardHost.Add(rewardBox);

            _rewardLabel = new Label("");
            _rewardLabel.style.fontSize = 15f;
            _rewardLabel.style.color = new StyleColor(new Color(0.8f, 1f, 0.8f));
            _rewardLabel.style.whiteSpace = WhiteSpace.Normal;
            _rewardLabel.style.marginTop = 16f;
            _rewardLabel.style.marginLeft = 16f;
            _rewardLabel.style.marginRight = 16f;
            rewardBox.Add(_rewardLabel);

            var okBtn = UTKButton.Create("확인", HideReward, UTKButton.Variant.Primary);
            okBtn.style.alignSelf = Align.Center;
            okBtn.style.width = 120f;
            okBtn.style.height = 40f;
            okBtn.style.marginTop = 12f;
            rewardBox.Add(okBtn);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            // 배경 딤드
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));
        }

        // ===== 공개 API =====

        /// <summary>싱글톤 생성 (멱등).</summary>
        public static MercyUTK Ensure()
        {
            if (_instance != null) return _instance;
            _instance = new MercyUTK();
            Debug.Log("[MercyUTK] 인스턴스 생성");
            return _instance;
        }

        /// <summary>영주 항복 패널 표시 — 원본 MercyUI.Show(territoryId, lordName) 동일.</summary>
        public static void Open(TerritoryId territoryId, string lordName)
        {
            var i = Ensure();
            i._territoryId = territoryId;
            i._lordName = lordName ?? "";
            i.HideReward();
            i.RefreshChoice();
            i.Show();
            Debug.Log($"[MercyUTK] 영주 항복 패널 표시: {lordName} (영지: {territoryId})");
        }

        /// <summary>간편 호출 — 원본 MercyUI.Show(lordName) 동일 (영지 미지정).</summary>
        public static void Open(string lordName = "")
        {
            var i = Ensure();
            i._territoryId = default;
            i._lordName = lordName ?? "";
            i.HideReward();
            i.RefreshChoice();
            i.Show();
            Debug.Log($"[MercyUTK] 영주 항복 패널 표시 (단순 모드): {lordName}");
        }

        /// <summary>패널 숨기기 — 원본 MercyUI.Hide() 동일.</summary>
        public static void CloseUI() => _instance?.Hide();

        /// <summary>토글 — IsOpen 분기 필수.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Hide(); return; }
            Debug.Log("[MercyUTK] Toggle: 열기 — Open() 호출 필요");
        }

        // ===== 선택 화면 갱신 =====

        private void RefreshChoice()
        {
            _lordLabel.text = "🏳️ 영주 항복!";
            var lord = LordSurrenderSystem.GetLordData(_territoryId);
            string info;
            if (lord.lordId != null)
            {
                string personalityStr = LordSurrenderSystem.GetPersonalityName(lord.personality);
                info = $"성격: {personalityStr} | 선호: {(lord.preferredFood ?? "알 수 없음")}";
            }
            else info = $"영주: {_lordName}";
            var infoLabel = _content.Q<Label>("MercyInfo");
            if (infoLabel != null) infoLabel.text = info;
        }

        // ===== 콜백 (원본 OnExecute/OnSpare 경로 실측) =====

        private void OnExecute()
        {
            Debug.Log($"[MercyUTK] ⚔️ 처형 선택: {_lordName}");
            LordSurrenderSystem.ExecuteLord(_territoryId);

            string territoryName = "알 수 없는 영지";
            TerritoryDifficulty difficulty = TerritoryDifficulty.Ring1;
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                var def = db.GetDefinition(_territoryId);
                if (!string.IsNullOrEmpty(def.territoryName))
                {
                    territoryName = def.territoryName ?? "알 수 없는 영지";
                    difficulty = def.difficulty;
                }
            }

            int goldReward = GetExecuteGoldReward(difficulty);
            int itemCount = Random.Range(1, 4);
            ShowReward($"✔️ {territoryName}을(를) 점령했습니다!\n💰 골드 +{goldReward}\n📦 전리품 +{itemCount}개\n💀 {_lordName} 영주 처형됨");
        }

        private void OnSpare()
        {
            Debug.Log($"[MercyUTK] 🤝 살려주기 선택: {_lordName}");
            LordSurrenderSystem.SpareLord(_territoryId);

            string territoryName = "알 수 없는 영지";
            TerritoryDifficulty difficulty = TerritoryDifficulty.Ring1;
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                var def = db.GetDefinition(_territoryId);
                if (!string.IsNullOrEmpty(def.territoryName))
                {
                    territoryName = def.territoryName ?? "알 수 없는 영지";
                    difficulty = def.difficulty;
                }
            }

            int goldReward = GetSpareGoldReward(difficulty);
            float loyaltyBonus = 30f;
            ShowReward($"✔️ {territoryName}을(를) 점령했습니다!\n💰 골드 +{goldReward}\n🤝 충성도 +{loyaltyBonus:F0}\n🕊️ {_lordName} 영주 생존 — 동맹 유지");
        }

        private void ShowReward(string message)
        {
            _choiceHost.visible = false;
            _rewardLabel.text = message;
            _rewardHost.style.display = DisplayStyle.Flex;
        }

        private void HideReward()
        {
            _rewardHost.style.display = DisplayStyle.None;
            _choiceHost.visible = true;
            Hide();
        }

        // ===== 보상 계산 (원본 GetExecuteGoldReward/GetSpareGoldReward 실측) =====

        private static int GetExecuteGoldReward(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 100;
                case TerritoryDifficulty.Ring2: return 250;
                case TerritoryDifficulty.Ring3: return 500;
                case TerritoryDifficulty.Ring4: return 1000;
                case TerritoryDifficulty.Empire: return 2000;
                default: return 100;
            }
        }

        private static int GetSpareGoldReward(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 50;
                case TerritoryDifficulty.Ring2: return 150;
                case TerritoryDifficulty.Ring3: return 300;
                case TerritoryDifficulty.Ring4: return 600;
                case TerritoryDifficulty.Empire: return 1200;
                default: return 50;
            }
        }
    }
}