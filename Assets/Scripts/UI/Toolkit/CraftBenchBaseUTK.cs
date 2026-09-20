using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.UI;   // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P18-C2 — 마인크래프트식 제작대 베이스 (UTK).
    /// [구성] 상단: 상태 라벨 / 중단: 재료 슬롯 N개 + → + 결과 슬롯 / 하단: 레시피 북(스크롤).
    /// [조작]
    ///   재료 슬롯 좌클릭 = 인벤 재료 순환 배치 / 우클릭 = 슬롯 비우기
    ///   레시피 행 클릭 = 재료 자동 배치(보유 시)
    ///   결과 슬롯 클릭 = 제작 실행(매칭 성공 시)
    /// [규약] placed는 UI 선택 상태일 뿐 — 실제 소모/지급은 TryCraft(Core)에서 1회.
    /// </summary>
    public abstract class CraftBenchBaseUTK : UTKWindowBase
    {
        public struct BenchRecipe
        {
            public string ResultId;      // 제작 함수 식별자(무기=아이템ID / 요리=meatId|herbId / 물약=herb1|herb2)
            public string ResultName;
            public string[] MatIds;      // 슬롯 수와 동일 길이 (빈 칸 null)
            public string Note;          // 레벨 요구/효과 등 보조 표기
            public ItemRarity rarity;    // [Milestone A/D] 성공률 희귀도 페널티·표시용
        }

        /// <summary>[Milestone D] 레시피 발견 여부 — 미발견이면 "?" 블라인드(이름/아이콘/확률 숨김).
        /// 무기/요리/물약 벤치가 RecipeDiscoverySystem으로 오버라이드(성공 시 MarkDiscovered → 공개).</summary>
        protected virtual bool IsDiscovered(BenchRecipe r) => true;

        /// <summary>[Milestone D] 레시피 성공률·운 표시 문자열 (발견 시에만 부가). 구체 벤치가 오버라이드.</summary>
        protected virtual string RateHint(BenchRecipe r) => "";

        protected readonly int SlotCount;
        private readonly UTKSlot[] _slots;
        private readonly string[] _placed;          // 슬롯별 배치된 itemId (null=빈칸)
        private readonly Label _status;
        private UTKSlot _resultSlot;
        private Label _resultLabel;
        private ScrollView _book;
        private bool _craftable;
        private BenchRecipe _matched;

        protected CraftBenchBaseUTK(string title, int slotCount, Vector2 size) : base(title, size)
        {
            SlotCount = slotCount;
            _slots = new UTKSlot[slotCount];
            _placed = new string[slotCount];

            _status = new Label("재료를 배치하거나 레시피를 클릭하세요.");
            _status.style.fontSize = 14f;
            _status.style.color = new StyleColor(UTKColor.TextSecondary);
            _status.style.marginLeft = 10f;
            _status.style.marginTop = 2f;
            _status.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_status);

            // ── 제작 행: 재료 슬롯 + → + 결과 ──
            var craftRow = new VisualElement();
            craftRow.style.flexDirection = FlexDirection.Row;
            craftRow.style.alignItems = Align.Center;
            craftRow.style.justifyContent = Justify.Center;
            craftRow.style.marginTop = 8f;
            craftRow.style.marginBottom = 8f;
            _content.Add(craftRow);

            for (int i = 0; i < slotCount; i++)
            {
                int idx = i;
                var slot = new UTKSlot();
                slot.name = "BenchSlot_" + idx;
                slot.style.width = 56f;
                slot.style.height = 56f;
                slot.style.marginLeft = 4f;
                slot.style.marginRight = 4f;
                slot.SetRank("common");
                slot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0) CyclePlace(idx);
                    else if (evt.button == 1) ClearSlot(idx);
                });
                _slots[idx] = slot;
                craftRow.Add(slot);

                if (i == slotCount - 1)
                {
                    var arrow = new Label("  ➜  ");
                    arrow.style.fontSize = 22f;
                    arrow.style.color = new StyleColor(UTKColor.BorderBronze);
                    craftRow.Add(arrow);

                    _resultSlot = new UTKSlot();
                    _resultSlot.name = "BenchResult";
                    _resultSlot.style.width = 64f;
                    _resultSlot.style.height = 64f;
                    _resultSlot.style.marginLeft = 4f;
                    _resultSlot.style.marginRight = 4f;
                    _resultSlot.SetRank("common");
                    _resultSlot.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button == 0 && _craftable) ExecuteCraft();
                    });
                    craftRow.Add(_resultSlot);

                    _resultLabel = new Label("");
                    _resultLabel.style.fontSize = 13f;
                    _resultLabel.style.color = new StyleColor(UTKColor.AccentRare);
                    _resultLabel.style.marginLeft = 8f;
                    _resultLabel.style.maxWidth = 220f;
                    _resultLabel.style.whiteSpace = WhiteSpace.Normal;
                    craftRow.Add(_resultLabel);
                }
            }

            // ── 레시피 북 ──
            var bookTitle = new Label("📖 레시피 북 — 클릭하면 재료가 자동 배치됩니다");
            bookTitle.style.fontSize = 14f;
            bookTitle.style.color = new StyleColor(UTKColor.TextSecondary);
            bookTitle.style.marginLeft = 10f;
            _content.Add(bookTitle);

            _book = new ScrollView(ScrollViewMode.Vertical);
            _book.style.flexGrow = 1f;
            _book.style.marginTop = 4f;
            _book.style.marginLeft = 8f;
            _book.style.marginRight = 8f;
            _book.style.marginBottom = 6f;
            _content.Add(_book);

            ApplyUIToolkitFont(this);
        }

        // ─────────────────────────── 서브클래스 계약 ───────────────────────────

        /// <summary>레시피 목록 (벤치별 소스).</summary>
        protected abstract IReadOnlyList<BenchRecipe> Recipes { get; }

        /// <summary>실제 제작 실행 — Core 제작 함수 호출(소모/지급/성공률 포함). 성공 시 true.</summary>
        protected abstract bool TryCraft(BenchRecipe recipe, List<string> placedIds, out string message);

        /// <summary>제작 성공 후 정리 (슬롯 비움 등).</summary>
        protected virtual void OnCraftSuccess() { ClearAllSlots(); }

        // ─────────────────────────── 표시/갱신 ───────────────────────────

        protected override void OnWindowOpen()
        {
            RebuildBook();
            RefreshMatch();
        }

        private void RebuildBook()
        {
            _book.Clear();
            var inv = PlayerInventory.Instance;
            foreach (var r in Recipes)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.height = 40f;
                row.style.marginBottom = 2f;
                row.style.paddingLeft = 4f;
                row.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.03f));

                var icon = new VisualElement();
                icon.style.width = 30f;
                icon.style.height = 30f;
                var item = PlayerInventory.GetItemById(r.ResultId);
                // [Milestone D] 미발견 레시피는 아이콘 감춤("?" 블라인드)
                bool discovered = IsDiscovered(r);
                icon.style.backgroundImage = discovered
                    ? UTKTextureSafe.ToBackground(item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null)
                    : null;
                row.Add(icon);

                var have1 = inv != null && !string.IsNullOrEmpty(r.MatIds[0])
                    ? inv.GetItemCount(r.MatIds[0]) : 0;
                int have2 = inv != null && r.MatIds.Length > 1 && !string.IsNullOrEmpty(r.MatIds[1])
                    ? inv.GetItemCount(r.MatIds[1]) : 0;
                int need1 = CountOf(r, 0);
                int need2 = r.MatIds.Length > 1 && !string.IsNullOrEmpty(r.MatIds[1]) ? 1 : 0;

                bool enough = have1 >= need1 && (need2 == 0 || have2 >= 1);
                string matText = DescribeMats(r);
                // [Milestone D] 발견: 이름+확률+재료 / 미발견: "?"+재료(실험 힌트)
                string nameText = discovered
                    ? $"{r.ResultName} {RateHint(r)}  [{matText}]" + (enough ? "" : "  (재료부족)") + (string.IsNullOrEmpty(r.Note) ? "" : $"  {r.Note}")
                    : $"?  [{matText}]" + (enough ? "" : "  (재료부족)");
                var nameL = new Label(nameText);
                nameL.style.fontSize = 13f;
                nameL.style.flexGrow = 1f;
                nameL.style.color = new StyleColor(enough ? UTKColor.TextPrimary : UTKColor.TextSecondary);
                row.Add(nameL);

                var captured = r;
                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0) AutoFill(captured);
                });
                _book.Add(row);
            }
        }

        private static int CountOf(BenchRecipe r, int matIndex)
        {
            // 슬롯 배치는 "재료 1개 = 슬롯 1칸" 방식이라 필요 수 = 그 재료가 등장하는 칸 수
            int count = 0;
            foreach (var m in r.MatIds)
                if (m == r.MatIds[matIndex]) count++;
            return count;
        }

        private string DescribeMats(BenchRecipe r)
        {
            var sb = new System.Text.StringBuilder();
            var seen = new List<string>();
            foreach (var m in r.MatIds)
            {
                if (string.IsNullOrEmpty(m) || seen.Contains(m)) continue;
                seen.Add(m);
                int need = 0;
                foreach (var x in r.MatIds) if (x == m) need++;
                var item = PlayerInventory.GetItemById(m);
                string n = item != null ? item.displayName : m;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{n} x{need}");
            }
            return sb.Length > 0 ? sb.ToString() : "재료 없음";
        }

        // ─────────────────────────── 슬롯 조작 ───────────────────────────

        /// <summary>슬롯 좌클릭 — 인벤의 배치 가능 아이템 순환(재료 후보 = 레시피에 등장하는 아이템).</summary>
        private void CyclePlace(int idx)
        {
            var candidates = IngredientCandidates();
            if (candidates.Count == 0) { _status.text = "배치할 재료가 인벤토리에 없습니다."; return; }

            string current = _placed[idx];
            int start = current != null ? candidates.FindIndex(c => c == current) + 1 : 0;
            for (int k = 0; k < candidates.Count; k++)
            {
                string cand = candidates[(start + k) % candidates.Count];
                _placed[idx] = cand;
                var item = PlayerInventory.GetItemById(cand);
                _slots[idx].SetIcon(item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null);
                RefreshMatch();
                return;
            }
        }

        private void ClearSlot(int idx)
        {
            _placed[idx] = null;
            _slots[idx].SetIcon(null);
            RefreshMatch();
        }

        private void ClearAllSlots()
        {
            for (int i = 0; i < SlotCount; i++) ClearSlot(i);
        }

        /// <summary>배치 가능 아이템 = 레시피 북에 등장하는 재료 중 인벤에 있는 것 (ID 중복 제거).</summary>
        private List<string> IngredientCandidates()
        {
            var inv = PlayerInventory.Instance;
            var set = new List<string>();
            foreach (var r in Recipes)
            {
                foreach (var m in r.MatIds)
                {
                    if (string.IsNullOrEmpty(m) || set.Contains(m)) continue;
                    if (inv != null && inv.GetItemCount(m) > 0) set.Add(m);
                }
            }
            return set;
        }

        /// <summary>레시피 클릭 — 필요 재료를 인벤에서 차감 없이 자동 배치.</summary>
        private void AutoFill(BenchRecipe r)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;
            ClearAllSlots();
            for (int i = 0; i < r.MatIds.Length && i < SlotCount; i++)
            {
                if (string.IsNullOrEmpty(r.MatIds[i])) continue;
                if (inv.GetItemCount(r.MatIds[i]) <= 0)
                {
                    _status.text = $"재료 부족 — {DescribeMats(r)}";
                    continue;
                }
                _placed[i] = r.MatIds[i];
                var item = PlayerInventory.GetItemById(r.MatIds[i]);
                _slots[i].SetIcon(item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null);
            }
            RefreshMatch();
        }

        // ─────────────────────────── 매칭/제작 ───────────────────────────

        private void RefreshMatch()
        {
            var placed = new List<string>();
            for (int i = 0; i < SlotCount; i++)
                if (!string.IsNullOrEmpty(_placed[i])) placed.Add(_placed[i]);

            _craftable = false;
            _matched = default;
            _resultSlot.SetIcon(null);
            _resultSlot.SetRank("common");
            _resultLabel.text = "";

            if (placed.Count == 0) { _status.text = "재료를 배치하거나 레시피를 클릭하세요."; return; }

            foreach (var r in Recipes)
            {
                var need = new List<string>();
                foreach (var m in r.MatIds) if (!string.IsNullOrEmpty(m)) need.Add(m);
                if (need.Count != placed.Count) continue;

                var pool = new List<string>(placed);
                bool all = true;
                foreach (var n in need)
                {
                    int idx = pool.FindIndex(p => p == n);
                    if (idx < 0) { all = false; break; }
                    pool.RemoveAt(idx);
                }
                if (all && pool.Count == 0)
                {
                    _matched = r;
                    _craftable = true;
                    var item = PlayerInventory.GetItemById(r.ResultId);
                    // [Milestone D] 발견: 아이콘/이름/확률 표시 / 미발견: "?" 블라인드(성공 시 레시피 획득)
                    if (IsDiscovered(r))
                    {
                        _resultSlot.SetIcon(item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null);
                        _resultSlot.SetRank(item != null ? UTKRarity.ClassForIndex((int)item.rarity) : "common");
                        _resultLabel.text = r.ResultName
                            + (string.IsNullOrEmpty(r.Note) ? "" : $"\n{r.Note}")
                            + (string.IsNullOrEmpty(RateHint(r)) ? "" : $"  {RateHint(r)}");
                        _status.text = $"제작 가능 — 클릭하여 {r.ResultName} 제작 ({RateHint(r)})";
                    }
                    else
                    {
                        _resultSlot.SetIcon(null);
                        _resultSlot.SetRank("common");
                        _resultLabel.text = "?  정체불명의 조합 — 제작 성공 시 레시피를 획득합니다.";
                        _status.text = "제작 가능 — 성공 시 레시피를 획득합니다";
                    }
                    return;
                }
            }
            _status.text = "일치하는 제작법이 없습니다.";
        }

        private void ExecuteCraft()
        {
            if (!_craftable) return;
            var placed = new List<string>();
            for (int i = 0; i < SlotCount; i++)
                if (!string.IsNullOrEmpty(_placed[i])) placed.Add(_placed[i]);

            if (TryCraft(_matched, placed, out string msg))
            {
                _status.text = msg;
                OnCraftSuccess();
            }
            else
            {
                _status.text = msg;
                // 실패(재료 소모 포함) — 슬롯 상태를 실제 인벤 기준으로 재정리
                RefreshMatch();
            }
            RebuildBook();
        }
    }
}
