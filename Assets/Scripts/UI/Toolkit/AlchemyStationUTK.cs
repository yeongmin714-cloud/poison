// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory, PlayerStats
using ProjectName.Core.Data;       // HerbDatabase, HerbInfo, HerbComboDatabase, HerbComboResult
using ProjectName.Systems;         // CraftSuccessSystem, CraftResult

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U3 Round C — 연금술 스테이션 창 (AlchemyStation 101줄 / AlchemyUI 555줄 uGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/AlchemyUI.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 작은 창: 약초1 + 약초2 선택(◀ ▶ 순환) → 제작 버튼.
    ///   ② 데이터 소스 재사용 — HerbDatabase.AllHerbs(40종), HerbComboDatabase.GetCombo(id1,id2).
    ///   ③ 성공/실패 판정 — CraftSuccessSystem.ExecuteCraft(true, grade1, grade2) (원본 CraftingSystem 경로 재사용).
    ///   ④ 성공 시 재료 차감 + 결과 지급 + EXP.
    ///   [AlchemyUTK] 로그.
    ///
    /// 250ms 폴링 갱신 (재료 수량 반영).
    /// </summary>
    public class AlchemyStationUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static AlchemyStationUTK _instance;
        public static AlchemyStationUTK Instance => _instance;

        /// <summary>팩토리 — 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new AlchemyStationUTK();
        }

        /// <summary>연금 창 열기(팩토리 겸용).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>닫혀있으면 열고, 열려있으면 닫음.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
            _instance.Show();
        }

        // ===== 설정 (원본 AlchemyUI 파라미터 실측) =====
        private const float WinW = 440f;
        private const float WinH = 300f;
        private const int ExpReward = 25;            // 원본 expReward
        private const long RefreshMs = 250L;

        // ===== 레퍼런스 =====
        private readonly Label _herb1Name;
        private readonly Label _herb2Name;
        private readonly Label _countLabel;
        private readonly Label _resultLabel;
        private IVisualElementScheduledItem _refreshTask;
        private IReadOnlyList<HerbInfo> _herbs;

        private int _herb1Index;
        private int _herb2Index;

        private AlchemyStationUTK() : base("연금술 테이블", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _herbs = HerbDatabase.AllHerbs;
            if (_herbs == null || _herbs.Count == 0)
                _herbs = new List<HerbInfo>();
            _herb1Index = 0;
            _herb2Index = 0;

            var hint = new Label("약초를 선택하고 제조 버튼을 누르세요.");
            hint.style.fontSize = 13f;
            hint.style.color = new StyleColor(UTKColor.TextSecondary);
            _content.Add(hint);

            _herb1Name = BuildHerbRow("약초 1", 1);
            _herb2Name = BuildHerbRow("약초 2", 2);

            _countLabel = new Label("");
            _countLabel.style.fontSize = 13f;
            _countLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _countLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_countLabel);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 10f;
            _content.Add(btnRow);

            var craftBtn = UTKButton.Create("제조하기", OnCraftClicked, UTKButton.Variant.Primary);
            craftBtn.style.flexGrow = 1f;
            btnRow.Add(craftBtn);

            var resetBtn = UTKButton.Create("초기화", () => { _resultLabel.text = ""; RefreshCounts(); });
            resetBtn.style.flexGrow = 1f;
            btnRow.Add(resetBtn);

            _resultLabel = new Label("");
            _resultLabel.style.fontSize = 15f;
            _resultLabel.style.color = new StyleColor(UTKColor.AccentRare);
            _resultLabel.style.whiteSpace = WhiteSpace.Normal;
            _resultLabel.style.marginTop = 10f;
            _content.Add(_resultLabel);

            ApplyUIToolkitFont(this);
            RefreshCounts();
        }

        // ≡≡≡ UI 벌드 헬퍼 ≡≡≡

        /// <summary>약초 선택 행 (라벨 + ◀ 이름 ▶). slotId=1/2 대상 인덱스.</summary>
        private Label BuildHerbRow(string title, int slotId)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8f;
            _content.Add(row);

            var titleLabel = new Label(title + "  ");
            titleLabel.style.fontSize = 15f;
            titleLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            titleLabel.style.width = 62f;
            titleLabel.style.flexShrink = 0f;
            row.Add(titleLabel);

            var prev = UTKButton.Create("◀", () => CycleHerb(slotId, -1), UTKButton.Variant.Secondary);
            prev.style.width = 34f;
            prev.style.height = 30f;
            prev.style.flexShrink = 0f;
            row.Add(prev);

            var nameLabel = new Label("");
            nameLabel.style.fontSize = 15f;
            nameLabel.style.color = new StyleColor(UTKColor.AccentRare);
            nameLabel.style.flexGrow = 1f;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(nameLabel);

            var next = UTKButton.Create("▶", () => CycleHerb(slotId, +1), UTKButton.Variant.Secondary);
            next.style.width = 34f;
            next.style.height = 30f;
            next.style.flexShrink = 0f;
            row.Add(next);

            return nameLabel;
        }

        /// <summary>약초 순환 (slotId=1 약초1, slotId=2 약초2).</summary>
        private void CycleHerb(int slotId, int step)
        {
            int count = _herbs != null ? _herbs.Count : 0;
            if (count == 0) return;
            if (slotId == 1)
                _herb1Index = Mod(_herb1Index + step, count);
            else
                _herb2Index = Mod(_herb2Index + step, count);
            RefreshCounts();
        }

        private static int Mod(int value, int mod)
        {
            int r = value % mod;
            return r < 0 ? r + mod : r;
        }

        // =====================================================================
        //  데이터 소스 — HerbDatabase / HerbComboDatabase (원본 AlchemyUI 연동)
        // =====================================================================

        private HerbInfo CurrentHerb(int index)
        {
            if (_herbs == null || _herbs.Count == 0) return default(HerbInfo);
            if (index < 0) index = 0;
            if (index >= _herbs.Count) index = _herbs.Count - 1;
            return _herbs[index];
        }

        private void RefreshCounts()
        {
            var inv = PlayerInventory.Instance;
            HerbInfo h1 = CurrentHerb(_herb1Index);
            HerbInfo h2 = CurrentHerb(_herb2Index);
            string name1 = string.IsNullOrEmpty(h1.displayName) ? "?" : h1.displayName;
            string name2 = string.IsNullOrEmpty(h2.displayName) ? "?" : h2.displayName;
            int c1 = inv != null && !string.IsNullOrEmpty(h1.id) ? inv.GetItemCount(h1.id) : 0;
            int c2 = inv != null && !string.IsNullOrEmpty(h2.id) ? inv.GetItemCount(h2.id) : 0;

            _herb1Name.text = name1;
            _herb2Name.text = name2;
            _countLabel.text = $"{name1} 보유 x{c1}   +   {name2} 보유 x{c2}";
        }

        // =====================================================================
        //  제조 — 원본 PerformAlchemyCraft (AlchemyUI 462~538) 패리티
        // =====================================================================

        private void OnCraftClicked()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                SetResult("인벤토리를 찾을 수 없습니다.", UTKColor.HealthRed);
                return;
            }

            HerbInfo h1 = CurrentHerb(_herb1Index);
            HerbInfo h2 = CurrentHerb(_herb2Index);
            if (string.IsNullOrEmpty(h1.id) || string.IsNullOrEmpty(h2.id))
            {
                SetResult("약초를 선택해주세요.", UTKColor.HealthRed);
                return;
            }

            var comboResult = HerbComboDatabase.GetCombo(h1.id, h2.id);
            if (!comboResult.HasValue)
            {
                SetResult($"{h1.displayName} + {h2.displayName} → 조합법이 없습니다.", UTKColor.TextSecondary);
                return;
            }
            var combo = comboResult.Value;

            // 재료 보유 확인 (원본: HasItem)
            if (!inv.HasItem(h1.id) || !inv.HasItem(h2.id))
            {
                SetResult("재료가 부족합니다.", UTKColor.HealthRed);
                return;
            }

            // 제작 판정 — CraftSuccessSystem (isAlchemy=true)
            string grade1 = CraftSuccessSystem.GetGradeFromItemId(h1.id);
            string grade2 = CraftSuccessSystem.GetGradeFromItemId(h2.id);
            CraftResult craft = CraftSuccessSystem.ExecuteCraft(true, grade1, grade2);

            if (craft == CraftResult.Success)
            {
                inv.RemoveItem(h1.id, 1);
                inv.RemoveItem(h2.id, 1);
                PlayerInventory.ItemData resultItem = BuildResultItem(combo);
                if (resultItem != null)
                    inv.AddItem(resultItem, 1);
                if (PlayerStats.Instance != null)
                    PlayerStats.Instance.AddEXP(ExpReward);

                SetResult($"🟢 {h1.displayName} + {h2.displayName} → {combo.resultName}\n제조 성공! 경험치 {ExpReward} 획득",
                    UTKColor.GuildGreen);
                float rate = CraftSuccessSystem.GetFinalSuccessRate(true, grade1, grade2);
                Debug.Log($"[AlchemyUTK] 제조 성공: {h1.displayName}+{h2.displayName} → {combo.resultName} (성공률 {Mathf.RoundToInt(rate * 100f)}%)");
            }
            else if (craft == CraftResult.Fail_MaterialPreserved)
            {
                SetResult($"🟡 {h1.displayName} + {h2.displayName} → 제조 실패 (재료 보존)", UTKColor.TextSecondary);
                Debug.Log($"[AlchemyUTK] 제조 실패(재료보존): {h1.displayName}+{h2.displayName}");
            }
            else if (craft == CraftResult.Fail_MaterialDestroyed)
            {
                inv.RemoveItem(h1.id, 1);   // 주재료 소실 (원본 부분실패)
                SetResult($"🔴 {h1.displayName} + {h2.displayName} → 제조 실패! 주재료 소실", UTKColor.HealthRed);
                Debug.Log($"[AlchemyUTK] 제조 실패(재료소멸): {h1.displayName}+{h2.displayName}");
            }
            else // Fail_Burned
            {
                inv.RemoveItem(h1.id, 1);
                inv.RemoveItem(h2.id, 1);
                SetResult("🔥 제조 대실패! 모든 재료 소실", UTKColor.HealthRed);
                Debug.Log($"[AlchemyUTK] 제조 대실패(전소): {h1.displayName}+{h2.displayName}");
            }

            RecipeDiscoverySystem.MarkDiscovered(combo.resultName);
            RefreshCounts();
        }

        /// <summary>조합 결과 → 인벤 등록용 ItemData (원본 CreateItemDataFromComboResult 축약).</summary>
        private static PlayerInventory.ItemData BuildResultItem(HerbComboResult combo)
        {
            PlayerInventory.ItemCategory category = PlayerInventory.ItemCategory.Potion;
            string name = combo.resultName;
            bool isMaterial = name.Contains("접착제") || name.Contains("코팅제") || name.Contains("도구")
                || name.Contains("재료") || name.Contains("합금제") || name.Contains("방패")
                || name.Contains("트랩") || name.Contains("장비") || name.Contains("용액");
            bool isPotion = name.Contains("물약") || name.Contains("약") || name.Contains("진액")
                || name.Contains("오일") || name.Contains("엘릭서") || name.Contains("제제");
            if (isMaterial) category = PlayerInventory.ItemCategory.Material;
            else if (isPotion) category = PlayerInventory.ItemCategory.Potion;

            return new PlayerInventory.ItemData
            {
                id = "combo_" + combo.resultId,
                displayName = combo.resultName,
                description = combo.description,
                category = category,
                maxStack = 99
            };
        }

        private void SetResult(string message, Color color)
        {
            _resultLabel.text = message;
            _resultLabel.style.color = new StyleColor(color);
        }

        // =====================================================================
        //  생명주기 — UTKWindowBase 훅
        // =====================================================================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 40f;
            style.top = 120f;
            _resultLabel.text = "";
            RefreshCounts();
            StartRefreshLoop();
            Debug.Log("[AlchemyUTK] 연금술 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[AlchemyUTK] 연금술 창 닫힘");
        }

        // =====================================================================
        //  폴링 갱신
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshCounts();
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
    }
}