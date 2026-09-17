using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round A-2 — 영지 상세 정보 팝업 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/TerritoryInfoPopup.cs (458줄) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 영지 상세: 이름/국가/난이도/소유주/병사/영주/지병/상태/재화(추정) — 원본 데이터 경로 실측 연동.
    ///  ② 재화는 TerritoryState에 treasury 공개 필드가 없어 난이도 링 기반 추정 지수 표기
    ///     (원본 GuardTaskSystem.AppendTreasuryInfo / TerritoryInfoPopup 방식과 동일 추정).
    ///  ③ 닫기 버튼 + ESC(UTKWindowManager)로 닫힘.
    ///  ④ static Open(territoryId) — 호출 시 특정 영지로 포커스 갱신.
    ///  각 경로에 [TerrInfoUTK] Debug.Log 실측 로그.
    /// [진입점] static Open(TerritoryId) / Ensure(). 순수 VisualElement 트리, 1회성 표시(g리드 없음).
    /// </summary>
    public class TerritoryInfoPopupUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static TerritoryInfoPopupUTK _instance;
        public static TerritoryInfoPopupUTK Instance => _instance;

        /// <summary>팩토리 — UIRoot 중앙 배치. 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new TerritoryInfoPopupUTK();
        }

        /// <summary>영지 상세 팝업 열기. 특정 영지로 포커스 전환 (원본 TerritoryInfoPopup.Show 대응).</summary>
        public static void Open(TerritoryId territoryId)
        {
            Ensure();
            _instance.SetTerritory(territoryId);
            _instance.Show();
        }

        /// <summary>현재 표시 중인 영지 (원본 CurrentTerritoryId 대응).</summary>
        public static TerritoryId CurrentTerritoryId => _instance != null ? _instance._territoryId : default;

        // ===== 설정 =====
        private const float WinW = 420f;
        private const float WinH = 430f;

        // ===== 상태 =====
        private TerritoryId _territoryId;

        // ===== 레퍼런스 =====
        private readonly VisualElement _rows;

        private TerritoryInfoPopupUTK() : base("영지 상세 정보", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _rows = new VisualElement();
            _rows.name = "TerrInfoRows";
            _rows.style.flexGrow = 1f;
            _rows.style.flexDirection = FlexDirection.Column;
            _content.Add(_rows);

            ApplyUIToolkitFont(this);

            // 위치는 Show에서 중앙 배치.
            style.display = DisplayStyle.None;
        }

        public void SetTerritory(TerritoryId territoryId)
        {
            _territoryId = territoryId;
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            // 화면 중앙 배치 (원본 창 중앙 관례)
            float w = WinW, h = WinH;
            style.left = Mathf.Max(4f, (root != null ? root.resolvedStyle.width : 1600f) * 0.5f - w * 0.5f);
            style.top = Mathf.Max(4f, (root != null ? root.resolvedStyle.height : 900f) * 0.5f - h * 0.5f);
            RefreshRows();
            Debug.Log($"[TerrInfoUTK] ℹ️ 영지 정보 열림: {_territoryId}");
        }

        public override void Hide()
        {
            base.Hide();
            Debug.Log("[TerrInfoUTK] 영지 정보 팝업 닫힘");
        }

        // ===== 내용 갱신 =====

        private void RefreshRows()
        {
            _rows.Clear();

            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                _rows.Add(MakeRow("데이터:", "데이터베이스 없음"));
                AddCloseButton();
                return;
            }

            TerritoryDefinition def = db.GetDefinition(_territoryId);
            TerritoryState state = db.GetState(_territoryId);

            if (def.id.nation == NationType.None)
            {
                _rows.Add(MakeRow("정보:", "영지 데이터 없음"));
                AddCloseButton();
                return;
            }

            _rows.Add(MakeTitle(GetTitle(def)));

            _rows.Add(MakeRow("국가:", GetNationDisplay(def.nation)));
            _rows.Add(MakeRow("난이도:", GetDifficultyDisplay(def.difficulty)));
            _rows.Add(MakeRow("소유주:", GetOwnerDisplay(def, state)));
            _rows.Add(MakeRow("병사:", GetGuardDisplay(def, state)));
            _rows.Add(MakeRow("영주:", $"{def.lord.lordName}  (입맛: {def.lord.preferredFood})"));
            _rows.Add(MakeRow("지병:", string.IsNullOrEmpty(def.lord.chronicDisease) ? "없음" : def.lord.chronicDisease));
            _rows.Add(MakeRow("상태:", GetStatusDisplay(def, state)));
            _rows.Add(MakeRow("재화:", GetTreasuryEstimate(def, state)));

            AddCloseButton();
        }

        private void AddCloseButton()
        {
            var closeRow = new VisualElement();
            closeRow.style.marginTop = 10f;
            closeRow.style.flexDirection = FlexDirection.Row;
            closeRow.style.justifyContent = Justify.FlexEnd;
            closeRow.Add(UTKButton.Create("닫기", () => Hide(), UTKButton.Variant.Danger));
            _rows.Add(closeRow);
        }

        // ===== 표시 헬퍼 =====

        private static VisualElement MakeTitle(string text)
        {
            var l = new Label(text);
            l.AddToClassList("utk-title-label");
            l.style.fontSize = 18f;
            l.style.color = new StyleColor(UTKColor.AccentRare);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = 6f;
            return l;
        }

        private static VisualElement MakeRow(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 2f;

            var lab = new Label(label);
            lab.style.width = 70f;
            lab.style.fontSize = 13f;
            lab.style.color = new StyleColor(UTKColor.TextSecondary);

            var val = new Label(value ?? "");
            val.style.flexGrow = 1f;
            val.style.fontSize = 13f;
            val.style.color = new StyleColor(UTKColor.TextPrimary);
            val.style.whiteSpace = WhiteSpace.Normal;

            row.Add(lab);
            row.Add(val);
            return row;
        }

        private static string GetTitle(TerritoryDefinition def)
        {
            string prefix = def.nation switch
            {
                NationType.East => "동",
                NationType.West => "서",
                NationType.South => "남",
                NationType.North => "북",
                NationType.Empire => "황제국",
                NationType.Dracula => "드라큘라",
                _ => "?"
            };
            return $"[{prefix}] {def.territoryName}";
        }

        private static string GetNationDisplay(NationType nation)
        {
            return nation switch
            {
                NationType.East => "🏁 동 (East)",
                NationType.West => "🏁 서 (West)",
                NationType.South => "🏁 남 (South)",
                NationType.North => "🏁 북 (North)",
                NationType.Empire => "👑 황제국 (Empire)",
                NationType.Dracula => "🧛 드라큘라",
                _ => "알 수 없음"
            };
        }

        private static string GetDifficultyDisplay(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => "⭐ (Ring 1)",
                TerritoryDifficulty.Ring2 => "⭐⭐ (Ring 2)",
                TerritoryDifficulty.Ring3 => "⭐⭐⭐ (Ring 3)",
                TerritoryDifficulty.Ring4 => "⭐⭐⭐⭐ (Ring 4)",
                TerritoryDifficulty.Empire => "👑 (Empire)",
                _ => "알 수 없음"
            };
        }

        private static string GetOwnerDisplay(TerritoryDefinition def, TerritoryState state)
        {
            if (state == null) return "미점령";
            return state.ownership switch
            {
                TerritoryOwnership.Unoccupied => "미점령",
                TerritoryOwnership.PlayerOwned => "👤 플레이어",
                TerritoryOwnership.LordOwned => $"🔴 {def.lord.lordName}",
                TerritoryOwnership.Contested => "⚔️ 전쟁 중",
                _ => "알 수 없음"
            };
        }

        private static string GetGuardDisplay(TerritoryDefinition def, TerritoryState state)
        {
            int baseCount = def.guardCount;
            if (state == null) return $"{baseCount}명";

            float aliveRatio = state.guardAliveRatio;
            int alive = Mathf.Max(0, Mathf.RoundToInt(baseCount * aliveRatio));

            string levelRange = def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => "Lv.1~10",
                TerritoryDifficulty.Ring2 => "Lv.11~20",
                TerritoryDifficulty.Ring3 => "Lv.21~30",
                TerritoryDifficulty.Ring4 => "Lv.31~40",
                TerritoryDifficulty.Empire => "Lv.50",
                _ => "Lv.?"
            };

            return aliveRatio < 1f ? $"{alive}~{baseCount}명 ({levelRange})" : $"{baseCount}명 ({levelRange})";
        }

        private static string GetStatusDisplay(TerritoryDefinition def, TerritoryState state)
        {
            if (state == null) return "평화";
            if (state.isUnderAttack) return "⚔️ 전쟁 중";
            if (state.ownership == TerritoryOwnership.Contested) return "⚔️ 분쟁 중";

            if (state.ownership == TerritoryOwnership.PlayerOwned)
            {
                float loyalty = state.loyaltyToPlayer;
                if (loyalty < 30f) return "⚠️ 불안정 (충성도 낮음)";
                if (loyalty < 70f) return "🔶 보통 (충성도 중간)";
                return "✅ 안정 (충성도 높음)";
            }
            return "평화";
        }

        /// <summary>
        /// 재화 추정 지수 — TerritoryState에 treasury 공개 필드가 없어
        /// 난이도 링 기반 추정 표기 (원본 GuardTaskSystem.AppendTreasuryInfo 방식 동일).
        /// </summary>
        private static string GetTreasuryEstimate(TerritoryDefinition def, TerritoryState state)
        {
            int index = 1 + (int)def.difficulty; // Ring1=1 ~ Empire=5
            return $"유력 (추정 지수 {index})";
        }
    }
}