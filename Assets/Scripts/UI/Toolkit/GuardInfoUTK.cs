using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // GuardPlaceholder, GuardEquipmentSystem
using ProjectName.Core;      // PlayerInventory, PotionBuffData, PotionBuffEffect
using ProjectName.UI;        // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// 병사 정보 창 v2 — UI Toolkit 포팅 (Phase U5 Round 1-A).
    /// 원본: Assets/Scripts/UI/GuardInfoWindow.cs (IMGUI, 972줄) — 병사(Guard) 2분할 창만 이식.
    /// 원본은 절대 수정하지 않는다.
    ///
    /// [정보 계층 — 원본 2-Pane과 동일]
    ///  - 좌측 (외형 + 장착 장비): 이름/직합/국가 + 장비 6슬롯(무기/투구/갑옷/신발/장갑/방패).
    ///  - 우측 (능력치 + 물약 버프): HP바, 전투력, 공격/방어/최대체력/민첩(+기본/장비 분해), 물약 버프 표.
    ///
    /// [진입점]
    ///  원본 GuardInfoWindow.OpenForGuard(guard) → 본 정적 Open(GuardPlaceholder guard) 패리티.
    ///  원본의 우클릭/정보 버튼 경로가 OpenForGuard를 호출하므로, 후속 라운드에서 해당 호출부를
    ///  GuardInfoUTK.Open(guard)로 교체하면 동일 데이터가 그대로 전달된다.
    ///
    /// [갱신]
    ///  250ms 폴링 (UTKWindowBase.Show 훅에서 schedule.Execute().Every() 시작, Hide에서 Pause).
    ///  HP 등 실시간 값은 폴링으로 반영. 장비 변경/역할 변경 이벤트 구독은 폴링이 대체(단순화).
    ///
    /// [규약] foreach만 / 클래스 public(CS0050) / UnityEngine.Debug /
    ///        IStyle 4면 개별 속성(Top/Bottom/Left/Right) / static 진입점.
    /// </summary>
    public class GuardInfoUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static GuardInfoUTK _instance;
        public static GuardInfoUTK Instance => _instance;

        /// <summary>팩토리 — 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new GuardInfoUTK();
        }

        /// <summary>병사 정보창 열기 (원본 OpenForGuard 패리티).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapBridge()
        {
            // [U8 배선] F키 병사 상호작용 브리지 구독 — Systems 이벤트 → UTK 정보창
            SoldierInteractBridge.OnSoldierInfoRequested += guard => Open(guard);
        }

        public static void Open(GuardPlaceholder guard)
        {
            if (guard == null) return;
            Ensure();
            _instance.OpenForGuard(guard);
        }

        // ===== 설정 =====
        private const float WinW = 780f;
        private const float WinH = 560f;
        private const long RefreshMs = 250L;
        private const float SlotSize = 56f;
        private const float LeftW = 300f;

        // ===== 장비 슬롯 정의 (원본 좌측 6슬롯 순서) =====
        private class GearDef
        {
            public string label;   // 무기/투구/...
            public System.Func<GuardPlaceholder, PlayerInventory.ItemData> get;
        }

        private static readonly GearDef[] GearDefs = new GearDef[]
        {
            new GearDef { label = "무기",  get = g => g.WeaponItem },
            new GearDef { label = "투구",  get = g => g.HelmetItem },
            new GearDef { label = "갑옷",  get = g => g.ArmorItem },
            new GearDef { label = "신발",  get = g => g.BootsItem },
            new GearDef { label = "장갑",  get = g => g.GlovesItem },
            new GearDef { label = "방패",  get = g => g.ShieldItem },
        };

        // ===== 갱신 대상 =====
        private GuardPlaceholder _currentGuard;
        private Label _nameLabel;
        private Label _levelNationLabel;
        private readonly Label[] _gearNameLabels = new Label[GearDefs.Length];
        private readonly UTKSlot[] _gearSlotViews = new UTKSlot[GearDefs.Length];
        private Label _hpValueLabel;
        private VisualElement _hpFill;
        private Label _combatLabel;
        private readonly Label[] _statValueLabels = new Label[4];   // 공격/방어/최대체력/민첩
        private readonly Label[] _statNoteLabels = new Label[4];
        private Label _buffLabel;
        private IVisualElementScheduledItem _refreshTask;

        private GuardInfoUTK() : base("병사 정보", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Row;
            BuildLeftZone();
            BuildRightZone();
            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  콘텐츠 빌드
        // =====================================================================

        private void BuildLeftZone()
        {
            var left = new VisualElement();
            left.name = "LeftZone";
            left.style.width = LeftW;
            left.style.flexShrink = 0f;
            left.style.flexGrow = 0f;
            left.style.flexDirection = FlexDirection.Column;
            left.style.paddingRight = 14f;
            _content.Add(left);

            // ── 모습 + 이름/직합 ──
            var face = new VisualElement();
            face.name = "Face";
            face.AddToClassList("utk-slot");
            face.style.height = 120f;
            face.style.justifyContent = Justify.Center;
            var faceLabel = MkLabel("🧍 병사 외형", 16, UTKColor.TextPrimary, TextAnchor.MiddleCenter);
            face.Add(faceLabel);
            left.Add(face);

            _nameLabel = MkLabel("", 20, UTKColor.AccentMagic, TextAnchor.MiddleLeft);
            _nameLabel.style.marginTop = 10f;
            left.Add(_nameLabel);

            _levelNationLabel = MkLabel("", 14, UTKColor.TextSecondary, TextAnchor.MiddleLeft);
            _levelNationLabel.style.whiteSpace = WhiteSpace.Normal;
            left.Add(_levelNationLabel);

            // ── 장착 장비 6슬롯 ──
            var gearHeader = MkLabel("📦 장착 장비", 16, UTKColor.AccentRare, TextAnchor.MiddleLeft);
            gearHeader.style.marginTop = 14f;
            gearHeader.style.marginBottom = 6f;
            left.Add(gearHeader);

            var grid = new VisualElement();
            grid.name = "GearGrid";
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            left.Add(grid);

            for (int i = 0; i < GearDefs.Length; i++)
            {
                var wrap = new VisualElement();
                wrap.style.flexDirection = FlexDirection.Column;
                wrap.style.alignItems = Align.Center;
                wrap.style.marginRight = 8f;
                wrap.style.marginBottom = 8f;

                var slot = new UTKSlot();
                slot.name = "GearSlot_" + GearDefs[i].label;
                slot.style.width = SlotSize;
                slot.style.height = SlotSize;
                slot.style.flexShrink = 0f;
                _gearSlotViews[i] = slot;
                wrap.Add(slot);

                var nameLabel = new Label("—");
                nameLabel.name = "GearName_" + GearDefs[i].label;
                nameLabel.style.fontSize = 11f;
                nameLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                nameLabel.style.whiteSpace = WhiteSpace.Normal;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                nameLabel.style.width = 62f;
                _gearNameLabels[i] = nameLabel;
                wrap.Add(nameLabel);

                grid.Add(wrap);
            }
        }

        private void BuildRightZone()
        {
            var right = new VisualElement();
            right.name = "RightZone";
            right.style.flexGrow = 1f;
            right.style.flexDirection = FlexDirection.Column;
            right.style.paddingLeft = 6f;
            _content.Add(right);

            // ── HP 바 ──
            var header = MkLabel("📊 능력치", 16, UTKColor.AccentRare, TextAnchor.MiddleLeft);
            right.Add(header);

            var gauge = BuildGaugeRow("❤️ HP:");
            _hpValueLabel = gauge.value;
            _hpFill = gauge.fill;
            right.Add(gauge.root);
            right.Add(AddSep(8f, 2f));

            // ── 전투력 ──
            _combatLabel = MkLabel("⚡ 전투력:", 16, UTKColor.TextPrimary, TextAnchor.MiddleLeft);
            _combatLabel.style.marginTop = 4f;
            right.Add(_combatLabel);
            right.Add(AddSep(10f, 2f));

            // ── 스탯 4행 (공격/방어/최대체력/민첩) ──
            string[] statNames = { "⚔️ 공격력:", "🛡️ 방어력:", "💚 최대체력:", "💨 민첩:" };
            for (int i = 0; i < 4; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 4f;

                var name = MkLabel(statNames[i], 15, UTKColor.TextSecondary, TextAnchor.MiddleLeft);
                name.style.width = 100f;
                row.Add(name);

                _statValueLabels[i] = MkLabel("-", 16, UTKColor.TextPrimary, TextAnchor.MiddleRight);
                _statValueLabels[i].style.width = 90f;
                row.Add(_statValueLabels[i]);

                _statNoteLabels[i] = MkLabel("", 12, UTKColor.TextSecondary, TextAnchor.MiddleLeft);
                _statNoteLabels[i].style.flexGrow = 1f;
                _statNoteLabels[i].style.whiteSpace = WhiteSpace.Normal;
                row.Add(_statNoteLabels[i]);

                right.Add(row);
            }

            right.Add(AddSep(10f, 2f));

            // ── 물약 버프 표 ──
            var buffHeader = MkLabel("💊 물약 버프", 16, UTKColor.AccentRare, TextAnchor.MiddleLeft);
            buffHeader.style.marginTop = 6f;
            right.Add(buffHeader);

            _buffLabel = MkLabel("", 12, UTKColor.TextPrimary, TextAnchor.UpperLeft);
            _buffLabel.style.whiteSpace = WhiteSpace.Normal;
            _buffLabel.style.flexGrow = 1f;
            right.Add(_buffLabel);
        }

        private static GaugeParts BuildGaugeRow(string labelName)
        {
            var parts = new GaugeParts();

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginTop = 6f;
            root.style.marginRight = 8f;
            parts.root = root;

            var name = MkLabel(labelName, 15, UTKColor.TextSecondary, TextAnchor.MiddleLeft);
            name.style.width = 90f;
            root.Add(name);

            parts.value = MkLabel("-", 14, UTKColor.TextPrimary, TextAnchor.MiddleRight);
            parts.value.style.width = 130f;
            root.Add(parts.value);

            var gaugeBg = new VisualElement();
            gaugeBg.style.flexGrow = 1f;
            gaugeBg.style.height = 10f;
            gaugeBg.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.65f));
            gaugeBg.style.borderTopWidth = 1f;
            gaugeBg.style.borderBottomWidth = 1f;
            gaugeBg.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            gaugeBg.style.borderBottomColor = new StyleColor(UTKColor.IronLine);

            parts.fill = new VisualElement();
            parts.fill.style.height = new Length(100f, LengthUnit.Percent);
            parts.fill.style.width = new Length(0f, LengthUnit.Percent);
            parts.fill.style.backgroundColor = new StyleColor(UTKColor.GuildGreen);
            gaugeBg.Add(parts.fill);

            root.Add(gaugeBg);
            return parts;
        }

        private sealed class GaugeParts
        {
            public VisualElement root;
            public Label value;
            public VisualElement fill;
        }

        private static VisualElement AddSep(float marginTop, float marginBottom)
        {
            var sep = new VisualElement();
            sep.style.height = 1f;
            sep.style.backgroundColor = new StyleColor(UTKColor.IronLine);
            sep.style.marginTop = marginTop;
            sep.style.marginBottom = marginBottom;
            return sep;
        }

        // =====================================================================
        //  생명주기
        // =====================================================================

        public void OpenForGuard(GuardPlaceholder guard)
        {
            if (guard == null) return;
            _currentGuard = guard;
            Show();
        }

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            CenterOnParent();
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[GuardInfoUTK] 병사 정보창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            _currentGuard = null;
            Debug.Log("[GuardInfoUTK] 병사 정보창 닫힘");
        }

        // =====================================================================
        //  폴링 (250ms)
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshDisplay();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        private void CenterOnParent()
        {
            var par = parent;
            if (par == null) return;
            float pw = par.resolvedStyle.width;
            float ph = par.resolvedStyle.height;
            if (pw <= 0f || ph <= 0f) return;
            style.left = Mathf.Max(0f, (pw - resolvedStyle.width) * 0.5f);
            style.top = Mathf.Max(0f, (ph - resolvedStyle.height) * 0.5f);
        }

        // =====================================================================
        //  데이터 갱신 — 원본 데이터 경로 그대로
        // =====================================================================

        private void RefreshDisplay()
        {
            var guard = _currentGuard;
            if (guard == null)
            {
                if (IsOpen) Close();
                return;
            }

            // --- 좌측: 이름/직합/국가/레벨 ---
            string gradeStr = GetGuardGradeString(guard);
            string typeStr = GetGuardTypeLabel(guard);
            _nameLabel.text = $"⚔️ {guard.GuardName} {gradeStr} | {typeStr}";
            string nationDisplay = string.IsNullOrEmpty(guard.Nation) ? "" : $" ({guard.Nation})";
            _levelNationLabel.text = $"Lv.{guard.Level} | {guard.JobTitle}{nationDisplay} | 호감도 {guard.Loyalty:F0}/100";

            // --- 좌측: 장비 6슬롯 ---
            for (int i = 0; i < GearDefs.Length; i++)
            {
                var item = GearDefs[i].get(guard);
                if (item != null && !string.IsNullOrEmpty(item.id))
                {
                    _gearSlotViews[i].SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                    _gearSlotViews[i].SetRank(UTKRarity.ClassForIndex((int)item.rarity));
                    _gearNameLabels[i].text = item.displayName;
                }
                else
                {
                    _gearSlotViews[i].SetIcon(null);
                    _gearSlotViews[i].SetRank("common");
                    _gearNameLabels[i].text = "—";
                }
            }

            // --- 우측: HP 바 ---
            float maxHp = guard.GetMaxHP();
            float hpRatio = maxHp > 0f ? Mathf.Clamp01(guard.HP / maxHp) : 0f;
            _hpValueLabel.text = $"{(int)guard.HP}/{(int)maxHp}";
            SetGaugeFill(_hpFill, hpRatio);

            // --- 우측: 전투력 ---
            float combatPower = 0f;
            if (GuardEquipmentSystem.Instance != null)
                combatPower = GuardEquipmentSystem.Instance.CalculateGuardCombatPower(guard);
            _combatLabel.text = $"⚡ 전투력: {combatPower:F0} (장비 반영)";

            // --- 우측: 스탯 + 기본/장비 분해 ---
            int rawAtk = guard.GetStatAttack();
            int totalAtk = guard.GetAttack();
            int gearAtk = totalAtk - rawAtk;
            int rawDef = guard.GetStatDefense();
            int totalDef = guard.GetDefense();
            int gearDef = totalDef - rawDef;

            _statValueLabels[0].text = totalAtk.ToString();
            _statNoteLabels[0].text = gearAtk > 0 ? $"기본 {rawAtk} + 무기 {gearAtk}" : $"기본 {rawAtk}";

            _statValueLabels[1].text = totalDef.ToString();
            _statNoteLabels[1].text = gearDef > 0 ? $"기본 {rawDef} + 장비 {gearDef}" : $"기본 {rawDef}";

            _statValueLabels[2].text = maxHp.ToString("F0");
            _statNoteLabels[2].text = $"기본 {guard.MaxHP:F0} + 체력 {guard.GetStatVitality() * 2}";

            _statValueLabels[3].text = guard.GetAgility().ToString();
            _statNoteLabels[3].text = $"기본 {guard.GetStatAgility()}";

            // --- 우측: 물약 버프 표 ---
            var buffLines = new System.Collections.Generic.List<string>(PotionBuffData.AllEffects.Count);
            foreach (var kv in PotionBuffData.AllEffects)
                buffLines.Add(DescribePotionBuff(kv.Key, kv.Value));
            _buffLabel.text = buffLines.Count > 0 ? string.Join("\n", buffLines) : "(물약 버프 없음)";
        }

        private static string DescribePotionBuff(string potionId, PotionBuffEffect effect)
        {
            if (effect.healFlat > 0f)
                return $"{potionId}: 체력 즉시 +{effect.healFlat:0}";
            if (effect.healPercent > 0f)
                return $"{potionId}: 체력 +{effect.healPercent * 100f:0}%";
            var parts = new System.Collections.Generic.List<string>(3);
            if (effect.attackBuff > 0f)  parts.Add($"공격+{effect.attackBuff:0}");
            if (effect.defenseBuff > 0f) parts.Add($"방어+{effect.defenseBuff:0}");
            if (effect.agilityBuff > 0f) parts.Add($"민첩+{effect.agilityBuff:0}");
            if (parts.Count == 0) return $"{potionId}: (효과 없음)";
            return $"{potionId}: {string.Join(" / ", parts.ToArray())} ({effect.buffSeconds:0}초)";
        }

        // ===== 헬퍼 =====

        private static void SetGaugeFill(VisualElement fill, float ratio)
        {
            if (fill == null) return;
            fill.style.width = new Length(Mathf.Clamp01(ratio) * 100f, LengthUnit.Percent);
        }

        private static string GetGuardTypeLabel(GuardPlaceholder guard)
        {
            if (guard == null) return "병사";
            string job = guard.JobTitle;
            if (string.IsNullOrEmpty(job) || job == "병사") return "병사";
            return job;
        }

        private static string GetGuardGradeString(GuardPlaceholder guard)
        {
            int lv = guard.Level;
            if (lv >= 35) return "★★★★";
            if (lv >= 20) return "★★★";
            if (lv >= 10) return "★★";
            return "★";
        }

        private static Label MkLabel(string text, float size, Color color, TextAnchor align)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            l.style.unityTextAlign = align;
            return l;
        }
    }
}