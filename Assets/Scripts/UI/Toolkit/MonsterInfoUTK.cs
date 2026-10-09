using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;        // MonsterDatabase, MonsterDef, MonsterTier
using ProjectName.Core.Data;   // MonsterDataReader
using ProjectName.Systems;     // AnimalAI, SoldierInteractBridge

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [P28] 몬스터 정보창 (UI Toolkit) — Ctrl+좌클릭으로 몬스터 관찰 시 표시.
    ///
    /// [진입점]
    ///  Ctrl+좌클릭(단순 클릭) → Systems(ContextCommandRouter) → SoldierInteractBridge.RaiseMonsterInfo(ai)
    ///       → 본 창 Open (Systems→UI 순환참조 회피 이벤트 경유 — SoldierInteractBridge 동일 패턴).
    ///
    /// [표시]
    ///  - 헤더: 몬스터 이름 + 티어(등급) + Lv
    ///  - HP바: 현재/최대 (비율색 ≥0.6 초록 / ≥0.3 노랑 / 빨강)
    ///  - 스탯: 공격력 / 속도 / 티어
    ///  - 설명 (MonsterDef.description)
    ///  - 드랍 아이템 (MonsterDataReader.GetMonsterInfoByName → DropItems)
    ///
    /// [자동 닫기] 250ms 폴링 — 대상 null / 사망(!IsAlive) / 비활성 시 Close + 대상 해제.
    ///
    /// [규약] foreach만(읽기 전용) / public 멤버(CS0050) / UnityEngine.Debug /
    ///        IStyle 4면 개별 속성 / IMGUI(GUI.xxx) 금지 / USS gradient·url 절대경로 금지.
    ///
    /// 공용 UTKTheme 토큰과 Theme.uss의 monster-info 스타일을 사용한다.
    /// 기능 로직(MonsterDatabase/Level/HP/드랍 표시, 250ms 폴링)은 무수정.
    /// </summary>
    public class MonsterInfoUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static MonsterInfoUTK _instance;
        public static MonsterInfoUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance == null)
                _instance = new MonsterInfoUTK();

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && _instance.parent != root)
                root.Add(_instance);
        }

        /// <summary>몬스터 정보창 열기 — 정적 진입점 (ContextCommandRouter 브리지 구독 경유).</summary>
        public static void Open(AnimalAI monster)
        {
            if (monster == null || !monster.IsAlive) return;
            Ensure();
            _instance.OpenForMonster(monster);
        }

        /// <summary>[P28 배선] Ctrl+좌클릭 몬스터 정보 — Systems 이벤트 → UTK 정보창.
        /// GuardInfoUTK.BootstrapBridge와 동일 AfterSceneLoad 패턴.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapBridge()
        {
            SoldierInteractBridge.OnMonsterInfoRequested += monster => Open(monster);
        }

        // ===== 설정 =====
        private const float WinW = 400f;
        private const float WinH = 440f;
        private const long RefreshMs = 250L;   // HP/상태 실시간 폴링


        // 티어색 (NameplateOverlayUTK 몬스터 팔레트 계승)
        private static readonly Color TierBeginner     = new Color(0.30f, 0.85f, 0.40f);   // 초반 초록
        private static readonly Color TierIntermediate = new Color(1.00f, 0.80f, 0.25f);   // 중반 노랑
        private static readonly Color TierAdvanced     = new Color(0.90f, 0.25f, 0.25f);   // 후반 빨강
        private static readonly Color TierBoss         = new Color(0.95f, 0.55f, 0.10f);   // 보스 주황

        // ===== 상태 =====
        private AnimalAI _monster;
        private IVisualElementScheduledItem _refreshTask;

        // ===== UI 참조 =====
        private Label _nameLabel;        // 이름 + 티어라벨 (티어색)
        private Label _levelLabel;       // Lv.
        private Label _hpValueLabel;     // 현재/최대
        private VisualElement _hpFill;   // HP 채움
        private Label _atkLabel;
        private Label _speedLabel;
        private Label _tierLabel;
        private Label _descLabel;
        private VisualElement _dropList;

        private MonsterInfoUTK() : base("몬스터 정보", new Vector2(WinW, WinH))
        {
            BuildContent();
            UTKTheme.ApplyWindowChrome(this, this.Q("TitleBar"), _content);
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  콘텐츠 빌드
        // =====================================================================

        private void BuildContent()
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 14f;
            _content.style.paddingRight = 14f;

            // ── 이름 + 티어라벨 ──
            _nameLabel = new Label("");
            _nameLabel.name = "MonsterName";
            _nameLabel.style.fontSize = 21.6f;
            _nameLabel.AddToClassList("monster-info__name");
            _nameLabel.style.color = new StyleColor(UTKTheme.TextMain);
            _nameLabel.style.marginTop = 8f;
            _nameLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_nameLabel);

            // ── Lv + 티어 ──
            _levelLabel = new Label("");
            _levelLabel.name = "MonsterLevel";
            _levelLabel.style.fontSize = 14.4f;
            _levelLabel.AddToClassList("monster-info__secondary");
            _levelLabel.style.color = new StyleColor(UTKTheme.TextSub);
            _levelLabel.style.marginTop = 2f;
            _levelLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_levelLabel);

            _content.Add(Sep());

            // ── HP 바 ──
            var hpWrap = new VisualElement();
            hpWrap.name = "MonsterHpBar";
            hpWrap.AddToClassList("monster-info__hp-row");
            hpWrap.style.flexDirection = FlexDirection.Row;
            hpWrap.style.alignItems = Align.Center;
            hpWrap.style.marginTop = 6f;

            var hpName = new Label("❤️ 체력");
            hpName.AddToClassList("monster-info__secondary");
            hpName.style.fontSize = 14.4f;
            hpName.style.color = new StyleColor(UTKTheme.TextSub);
            hpName.style.width = 60f;
            hpName.style.flexShrink = 0f;
            hpWrap.Add(hpName);

            // 바 프레임
            var barFrame = new VisualElement();
            barFrame.name = "MonsterHpFrame";
            barFrame.AddToClassList("monster-info__hp-frame");
            barFrame.style.height = 18f;
            barFrame.style.flexGrow = 1f;
            barFrame.style.marginLeft = 6f;
            barFrame.style.marginRight = 4f;
            barFrame.style.borderTopWidth = 1f; barFrame.style.borderBottomWidth = 1f;
            barFrame.style.borderLeftWidth = 1f; barFrame.style.borderRightWidth = 1f;
            barFrame.style.borderTopColor = new StyleColor(UTKTheme.Stroke);
            barFrame.style.borderBottomColor = new StyleColor(UTKTheme.Stroke);
            barFrame.style.borderLeftColor = new StyleColor(UTKTheme.Stroke);
            barFrame.style.borderRightColor = new StyleColor(UTKTheme.Stroke);
            barFrame.style.overflow = Overflow.Hidden;
            barFrame.style.backgroundColor = new StyleColor(UTKTheme.BgBase);
            barFrame.style.borderTopLeftRadius = 4f;
            barFrame.style.borderTopRightRadius = 4f;
            barFrame.style.borderBottomLeftRadius = 4f;
            barFrame.style.borderBottomRightRadius = 4f;   // 작은배지 r4
            hpWrap.Add(barFrame);

            _hpFill = new VisualElement();
            _hpFill.name = "MonsterHpFill";
            _hpFill.style.backgroundColor = new StyleColor(UTKTheme.Danger);   // 초기색 — RefreshAll에서 비율색으로 덮어씀
            barFrame.Add(_hpFill);
            hpWrap.Add(barFrame);

            _hpValueLabel = new Label("- / -");
            _hpValueLabel.name = "MonsterHpValue";
            _hpValueLabel.AddToClassList("monster-info__value");
            _hpValueLabel.style.fontSize = 13.2f;
            _hpValueLabel.style.color = new StyleColor(UTKTheme.TextMain);
            _hpValueLabel.style.width = 84f;
            _hpValueLabel.style.flexShrink = 0f;
            _hpValueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _hpValueLabel.style.paddingLeft = 6f;
            hpWrap.Add(_hpValueLabel);

            _content.Add(hpWrap);
            _content.Add(Sep());

            // ── 스탯 3행 (공격/속도/티어) ──
            _atkLabel = MkStatRow("⚔️ 공격력");
            _speedLabel = MkStatRow("💨 속도");
            _tierLabel = MkStatRow("🏷️ 등급");

            _content.Add(Sep());

            // ── 설명 ──
            var descHeader = new Label("📖 설명");
            descHeader.AddToClassList("monster-info__section-title");
            descHeader.style.fontSize = 15.6f;
            descHeader.style.color = new StyleColor(UTKTheme.Gold);
            descHeader.style.marginTop = 6f;
            _content.Add(descHeader);

            _descLabel = new Label("");
            _descLabel.name = "MonsterDesc";
            _descLabel.AddToClassList("monster-info__secondary");
            _descLabel.style.fontSize = 13.2f;
            _descLabel.style.color = new StyleColor(UTKTheme.TextSub);
            _descLabel.style.whiteSpace = WhiteSpace.Normal;
            _descLabel.style.marginTop = 4f;
            _descLabel.style.flexGrow = 1f;
            _content.Add(_descLabel);

            _content.Add(Sep());

            // ── 드랍 아이템 ──
            var dropHeader = new Label("💎 드랍 아이템");
            dropHeader.AddToClassList("monster-info__section-title");
            dropHeader.style.fontSize = 15.6f;
            dropHeader.style.color = new StyleColor(UTKTheme.Gold);
            dropHeader.style.marginTop = 6f;
            _content.Add(dropHeader);

            _dropList = new VisualElement();
            _dropList.name = "MonsterDrops";
            _dropList.style.marginTop = 4f;
            _content.Add(_dropList);
        }

        /// <summary>스탯 라벨 행 생성 — 라벨 참조 보관 후 반환.</summary>
        private Label MkStatRow(string name)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 3f;

            var n = new Label(name);
            n.AddToClassList("monster-info__secondary");
            n.style.fontSize = 14.4f;
            n.style.color = new StyleColor(UTKTheme.TextSub);
            n.style.width = 90f;
            row.Add(n);

            var value = new Label("-");
            value.AddToClassList("monster-info__value");
            value.style.fontSize = 15.6f;
            value.style.color = new StyleColor(UTKTheme.TextMain);
            value.style.flexGrow = 1f;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(value);

            _content.Add(row);
            return value;
        }

        /// <summary>섹션 구분선 (세로 여백 + 얇은 라인).</summary>
        private static VisualElement Sep()
        {
            var sep = new VisualElement();
            sep.style.height = 2f;
            sep.style.marginTop = 10f;
            sep.style.marginBottom = 2f;
            sep.AddToClassList("monster-info__separator");
            sep.style.backgroundColor = new StyleColor(UTKTheme.Stroke);
            return sep;
        }


        // =====================================================================
        //  생명주기
        // =====================================================================

        /// <summary>대상 지정 후 열기 — 이미 열려있으면 대상만 교체해 즉시 갱신.</summary>
        public void OpenForMonster(AnimalAI monster)
        {
            if (monster == null) return;
            _monster = monster;
            if (IsOpen)
            {
                RefreshAll();
            }
            Show();
        }

        /// <summary>Attach to the persistent UI root before opening so the window is visible.</summary>
        public override void Show()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent != root)
                root.Add(this);
            base.Show();
        }

        /// <summary>열기 훅 — 폴링 시작.</summary>
        protected override void OnWindowOpen()
        {
            _refreshTask = schedule.Execute(RefreshAll).Every(RefreshMs);
            RefreshAll();
            Debug.Log("[MonsterInfoUTK] 몬스터 정보창 열림: " + DisplayName());
        }

        /// <summary>닫기 훅 — 폴링 중지 + 대상 해제.</summary>
        protected override void OnWindowClosed()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
            _monster = null;
            Debug.Log("[MonsterInfoUTK] 몬스터 정보창 닫힘");
        }

        // =====================================================================
        //  폴링 갱신 (250ms)
        // =====================================================================

        private void RefreshAll()
        {
            if (!IsOpen) return;

            AnimalAI m = _monster;
            // 대상 무효 (null / 사망 / 비활성) → 자동 닫기
            if (m == null || !m.IsAlive || !m.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            var def = MonsterDatabase.Get(m.MonsterId);

            // ── 이름 + 티어색 ──
            string name = def != null && !string.IsNullOrEmpty(def.displayName)
                ? def.displayName : m.MonsterId;
            MonsterTier tier = def != null ? def.tier : MonsterTier.Beginner;
            _nameLabel.text = "🐾 " + name;

            // ── Lv ──
            _levelLabel.text = $"Lv.{m.Level}   {MonsterDatabase.GetTierLabel(tier)}";

            // ── HP 바 (실시간) ──
            float cur = m.CurrentHP;
            float max = m.MaxHP;
            float ratio = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
            Color barColor = ratio >= 0.6f ? UTKTheme.Health
                : ratio >= 0.3f ? UTKTheme.Gold : UTKTheme.Danger;
            _hpFill.style.backgroundColor = new StyleColor(barColor);
            _hpFill.style.width = new Length(Mathf.Max(ratio * 100f, 0f), LengthUnit.Percent);
            _hpFill.style.height = 18f;
            _hpValueLabel.text = $"{ToStringI(cur)} / {ToStringI(max)}";

            // ── 스탯 ──
            _atkLabel.text = def != null ? ToStringI(def.baseDamage) : "?";
            _speedLabel.text = Def(m.CurrentSpeed);
            _tierLabel.text = MonsterDatabase.GetTierLabel(tier);

            // ── 설명 ──
            _descLabel.text = def != null && !string.IsNullOrEmpty(def.description)
                ? def.description : "설명 없음.";

            // ── 드랍 아이템 ──
            RefreshDrops(name);
        }

        private void RefreshDrops(string displayName)
        {
            if (_dropList.childCount > 0)
                _dropList.Clear();

            var info = MonsterDataReader.GetMonsterInfoByName(displayName);
            if (info == null || info.DropItems == null || info.DropItems.Length == 0)
            {
                var empty = new Label("정보 없음");
                empty.AddToClassList("monster-info__secondary");
                empty.style.fontSize = 13.2f;
                empty.style.color = new StyleColor(UTKTheme.TextSub);
                _dropList.Add(empty);
                return;
            }

            foreach (string dropName in info.DropItems)
            {
                var row = new Label("• " + dropName);
                row.AddToClassList("monster-info__value");
                row.style.fontSize = 13.2f;
                row.style.color = new StyleColor(UTKTheme.TextMain);
                row.style.marginBottom = 2f;
                row.style.whiteSpace = WhiteSpace.Normal;
                _dropList.Add(row);
            }
        }

        // =====================================================================
        //  헬퍼
        // =====================================================================

        private string DisplayName()
        {
            if (_monster == null) return "?";
            var def = MonsterDatabase.Get(_monster.MonsterId);
            return def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName : _monster.MonsterId;
        }

        private static string ToStringI(float v) => $"{v:F0}";

        private static string Def(float v) => $"{v:F1}";
    }
}