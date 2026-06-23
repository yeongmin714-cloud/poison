using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// 레시피 북 (R 키) — 발견한 레시피와 미발견 레시피를 보여줍니다.
    /// </summary>
    public class RecipeWindow : UIWindow
    {
        protected virtual void Start()
        {
            ApplyTheme(Phase33_Themes.CreateRecipeTheme());
        }

        [Header("Recipe Window")]
        [SerializeField] private Transform _recipeGridContainer;
        [SerializeField] private GameObject _recipeEntryPrefab; // Optional: assign in editor

        // Tab state
        private bool _showAlchemy = true;
        private Text _tabButtonText;
        private Text _countText;
        private Button _craftButton;

        protected override void OnShow()
        {
            Debug.Log("[RecipeWindow] 열림 — 레시피 북 표시");
            RefreshRecipeList();
        }

        protected override void OnHide()
        {
            Debug.Log("[RecipeWindow] 닫힘");
        }

        /// <summary>
        /// 레시피 목록 갱신 (발견/미발견 모두 표시)
        /// </summary>
        public void RefreshRecipeList()
        {
            if (_recipeGridContainer == null)
            {
                // Create container dynamically if not set
                CreateRecipeContainer();
            }

            // Clear existing entries
            foreach (Transform child in _recipeGridContainer)
            {
                Destroy(child.gameObject);
            }

            // Determine which recipes to show
            if (_showAlchemy)
                ShowAlchemyRecipes();
            else
                ShowCookingRecipes();

            // Update count display
            int total = _showAlchemy ? HerbComboDatabase.AllCombos.Count : CookingDatabase.AllRecipes.Count;
            int discovered = RecipeDiscoverySystem.DiscoveredCount;
            string tabName = _showAlchemy ? "연금술" : "요리";
                        Debug.Log($"[RecipeWindow] {tabName} 레시피: 발견 {discovered}/{total}");
        }

        private void ShowAlchemyRecipes()
        {
            var allCombos = HerbComboDatabase.AllCombos;
            foreach (var kv in allCombos)
            {
                var combo = kv.Value;
                bool discovered = RecipeDiscoverySystem.IsDiscovered(combo.resultName);
                CreateRecipeEntry(
                    discovered ? combo.resultName : "???",
                    discovered ? combo.effect : "아직 발견하지 못한 조합법입니다",
                    discovered
                );
            }
        }

        private void ShowCookingRecipes()
        {
            var allDishes = DishDatabase.All;
            foreach (var dish in allDishes)
            {
                bool discovered = RecipeDiscoverySystem.IsDiscovered(dish.DisplayName);
                CreateRecipeEntry(
                    discovered ? dish.DisplayName : "???",
                    discovered ? dish.Effect : "아직 발견하지 못한 요리법입니다",
                    discovered,
                    dish
                );
            }
        }

        private void CreateRecipeEntry(string name, string effect, bool discovered, DishInfo dish = null)
        {
            GameObject entry = new GameObject($"Recipe_{name}");
            entry.transform.SetParent(_recipeGridContainer, false);

            // Horizontal layout
            HorizontalLayoutGroup layout = entry.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(4, 4, 2, 2);
            layout.childForceExpandWidth = false;

            // Background
            Image bg = entry.AddComponent<Image>();
            bg.color = discovered ? new Color(0.2f, 0.3f, 0.2f, 0.8f) : new Color(0.15f, 0.15f, 0.15f, 0.8f);

            // Icon (ProceduralIconGenerator)
            if (discovered)
            {
                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(entry.transform, false);
                Image iconImage = iconGo.AddComponent<Image>();
                // Try to find the item icon from the recipe's result
                PlayerInventory.ItemData recipeItem = FindRecipeItem(name, dish);
                if (recipeItem != null && recipeItem.icon != null)
                {
                    iconImage.sprite = recipeItem.icon;
                }
                else
                {
                    // Fallback: colored square
                    iconImage.color = new Color(0.5f, 0.8f, 0.5f, 0.9f);
                }
                iconImage.preserveAspect = true;
                RectTransform iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(28, 28);
            }

            // Name label
            GameObject nameGo = new GameObject("Name");
            nameGo.transform.SetParent(entry.transform, false);
            Text nameText = nameGo.AddComponent<Text>();
            nameText.text = discovered ? name : $"<color=grey>???</color>";
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize = 26;
            nameText.color = discovered ? Color.white : Color.grey;
            nameText.alignment = TextAnchor.MiddleLeft;
            RectTransform nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(160, 24);

            // Effect label
            GameObject effectGo = new GameObject("Effect");
            effectGo.transform.SetParent(entry.transform, false);
            Text effectText = effectGo.AddComponent<Text>();
            effectText.text = discovered ? $"{effect}" : "";
            effectText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            effectText.fontSize = 22;
            effectText.color = new Color(0.8f, 0.8f, 0.8f);
            effectText.alignment = TextAnchor.MiddleLeft;
            RectTransform effectRect = effectGo.GetComponent<RectTransform>();
            effectRect.sizeDelta = new Vector2(140, 24);

            // Craft button (only for discovered)
            if (discovered)
            {
                GameObject btnGo = new GameObject("CraftBtn");
                btnGo.transform.SetParent(entry.transform, false);
                Image btnBg = btnGo.AddComponent<Image>();
                btnBg.color = new Color(0.3f, 0.6f, 0.3f, 0.9f);
                Button btn = btnGo.AddComponent<Button>();
                Text btnText = btnGo.AddComponent<Text>();
                btnText.text = "제작";
                btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                btnText.fontSize = 22;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.color = Color.white;
                RectTransform btnRect = btnGo.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(50, 22);

                string recipeName = name; // capture for closure
                btn.onClick.AddListener(() => TryCraftFromBook(recipeName, dish));
            }
        }

        private void TryCraftFromBook(string recipeName, DishInfo dish = null)
        {
            if (dish != null)
            {
                // Cooking recipe — use existing crafting logic
                Debug.Log($"[RecipeWindow] 요리 제작 시도: {recipeName}");
                return;
            }

            // Alchemy recipe — find the combo and craft
            foreach (var kv in HerbComboDatabase.AllCombos)
            {
                if (kv.Value.resultName == recipeName)
                {
                    // Key format: "herbId1_herbId2" (ordered alphabetically)
                    string key = kv.Key;
                    string[] ids = key.Split('_');
                    if (ids.Length >= 2)
                    {
                        Debug.Log($"[RecipeWindow] 연금술 제작: {recipeName} ({ids[0]}+{ids[1]})");
                        bool success = CraftingHelper.CraftAlchemy(ids[0], ids[1]);
                        if (success)
                            RefreshRecipeList(); // Refresh to show updated inventory
                        return;
                    }
                }
            }
            Debug.LogWarning($"[RecipeWindow] 레시피 '{recipeName}'를 찾을 수 없습니다.");
        }

        // Tab toggle: switch between Alchemy / Cooking
        public void ToggleTab()
        {
            _showAlchemy = !_showAlchemy;
            RefreshRecipeList();
            if (_tabButtonText != null)
                _tabButtonText.text = _showAlchemy ? "레시피 (연금술)" : "레시피 (요리)";
        }

        private void CreateRecipeContainer()
        {
            GameObject container = new GameObject("RecipeGrid");
            container.transform.SetParent(_windowRoot?.transform ?? transform, false);
            _recipeGridContainer = container.transform;

            VerticalLayoutGroup vLayout = container.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 2;
            vLayout.padding = new RectOffset(8, 8, 8, 8);
            vLayout.childForceExpandWidth = true;

            ContentSizeFitter fitter = container.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, 0);
            rect.sizeDelta = new Vector2(0, 0);
        }

        // ===================================================================
        // 레시피 결과 아이템 찾기 (아이콘 표시용)
        // ===================================================================
        private PlayerInventory.ItemData FindRecipeItem(string recipeName, DishInfo dish)
        {
            // Cooking dish: use DishDatabase
            if (dish != null)
            {
                // Search all PlayerInventory static fields for matching displayName
                var fields = typeof(PlayerInventory).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(PlayerInventory.ItemData))
                    {
                        var item = field.GetValue(null) as PlayerInventory.ItemData;
                        if (item != null && item.displayName == recipeName)
                            return item;
                    }
                }
                return null;
            }

            // Alchemy recipe: search all PlayerInventory static fields
            var allFields = typeof(PlayerInventory).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var field in allFields)
            {
                if (field.FieldType == typeof(PlayerInventory.ItemData))
                {
                    var item = field.GetValue(null) as PlayerInventory.ItemData;
                    if (item != null && item.displayName == recipeName)
                        return item;
                }
            }
            return null;
        }
    }
}