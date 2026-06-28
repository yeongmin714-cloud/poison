using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using ProjectName.Core.Utils;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// 연금술 UI - 두 가지 약초를 선택하여 제조합니다.
    /// </summary>
    public class AlchemyUI : MonoBehaviour
    {
        // Crafting parameters - adjust these to balance gameplay
        [Header("Alchemy Crafting Parameters")]
        [Tooltip("Base success rate for all alchemy combinations (0-100)")]
        [SerializeField] private int baseSuccessRate = 60;
        [Tooltip("Difficulty penalty applied to all combinations (can be made dynamic)")]
        [SerializeField] private int baseDifficultyPenalty = 0;
        [Tooltip("Required player level to perform alchemy")]
        [SerializeField] private int requiredLevel = 1;
        [Tooltip("Experience reward for successful alchemy crafting")]
        [SerializeField] private int expReward = 25;

        [Header("Phase 33 Theme")]
        [SerializeField] private UIDesignTheme _theme;

        private Dropdown herbDropdown1;
        private Dropdown herbDropdown2;
        private Button craftButton;
        private Button resetButton;
        private Text resultText;
        private Image herbIconImage1;
        private Image herbIconImage2;
        private GameObject _panel;

        /// <summary>현재 AlchemyUI가 열려있는지 여부</summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            // Phase 33: create alchemy theme
            if (_theme == null)
                _theme = Phase33_Themes.CreateAlchemyTheme();

            CreateUI();
            PopulateDropdowns();
            SetupDropdownListeners();
            // Initialize icons to first option
            if (herbDropdown1.options.Count > 0)
                UpdateHerbIcon1(herbDropdown1.options[0].text);
            if (herbDropdown2.options.Count > 0)
                UpdateHerbIcon2(herbDropdown2.options[0].text);
            // Start hidden — UIManager.Show() will activate
            if (_panel != null)
                _panel.SetActive(false);
        }

        /// <summary>AlchemyUI를 화면에 표시합니다.</summary>
        public void Show()
        {
            if (_panel == null)
            {
                CreateUI();
                PopulateDropdowns();
                SetupDropdownListeners();
                if (herbDropdown1.options.Count > 0)
                    UpdateHerbIcon1(herbDropdown1.options[0].text);
                if (herbDropdown2.options.Count > 0)
                    UpdateHerbIcon2(herbDropdown2.options[0].text);
            }
            _panel.SetActive(true);
            IsOpen = true;
            Debug.Log("[AlchemyUI] 연금술 UI 열림");
        }

        /// <summary>AlchemyUI를 화면에서 숨깁니다.</summary>
        public void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
            IsOpen = false;
            Debug.Log("[AlchemyUI] 연금술 UI 닫힘");
        }

        private void CreateUI()
        {
            // Create a Canvas if not already present in the scene
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("AlchemyCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create a panel for our UI
            GameObject panel = new GameObject("AlchemyPanel");
            _panel = panel;
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900, 788);
            panelRect.anchoredPosition = Vector2.zero;

            // Add background image with theme colors
            Image bg = panel.AddComponent<Image>();
            bg.color = _theme != null ? _theme.BgColor : new Color(0f, 0f, 0f, 0.7f);

            // Create layout
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 22;
            layout.childForceExpandHeight = false;

            // Herb 1 row
            GameObject herbRow1 = CreateRowWithIcon(panel, "약초 1:", out _, out herbIconImage1, out herbDropdown1);
            // Herb 2 row
            GameObject herbRow2 = CreateRowWithIcon(panel, "약초 2:", out _, out herbIconImage2, out herbDropdown2);

            // Craft button
            GameObject craftGo = CreateButton(panel, "제조하기", out craftButton);
            craftButton.onClick.AddListener(OnCraftClicked);

            // Reset button
            GameObject resetGo = CreateButton(panel, "초기화", out resetButton);
            resetButton.onClick.AddListener(OnResetClicked);

            // Result text
            GameObject resultGo = CreateLabel(panel, "", out resultText);
            resultText.alignment = TextAnchor.MiddleCenter;
            resultText.fontSize = 64;
            resultText.color = _theme != null ? _theme.AccentColor : Color.yellow;
        }

        private void PopulateDropdowns()
        {
            // Populate both dropdowns with all herbs
            var allHerbs = HerbDatabase.AllHerbs;
            if (allHerbs == null || allHerbs.Count == 0)
            {
                Debug.LogWarning("[AlchemyUI] No herbs loaded from HerbDatabase.");
                return;
            }

            herbDropdown1.ClearOptions();
            herbDropdown2.ClearOptions();

            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            foreach (var herb in allHerbs)
            {
                options.Add(new Dropdown.OptionData(herb.displayName));
            }

            herbDropdown1.AddOptions(options);
            herbDropdown2.AddOptions(options);
        }

        private void SetupDropdownListeners()
        {
            herbDropdown1.onValueChanged.AddListener(delegate { UpdateHerbIcon1(herbDropdown1.options[herbDropdown1.value].text); });
            herbDropdown2.onValueChanged.AddListener(delegate { UpdateHerbIcon2(herbDropdown2.options[herbDropdown2.value].text); });
        }

        // Helper to create a row with label, icon, dropdown
        private GameObject CreateRowWithIcon(GameObject parent, string labelText, out Text labelOut, out Image iconImageOut, out Dropdown dropdownOut)
        {
            GameObject row = new GameObject($"{labelText}Row");
            row.transform.SetParent(parent.transform, false);
            HorizontalLayoutGroup hLayout = row.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(0, 0, 0, 0);
            hLayout.spacing = 15;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            // Label
            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(row.transform, false);
            Text labelTxt = labelGo.AddComponent<Text>();
            labelTxt.text = labelText;
            labelTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelTxt.alignment = TextAnchor.MiddleLeft;
            labelTxt.fontSize = 56;
            labelTxt.color = Color.white;
            labelOut = labelTxt;

            // Icon placeholder
            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            Image iconImg = iconGo.AddComponent<Image>();
            iconImg.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // gray placeholder
            iconImg.preserveAspect = true;
            RectTransform iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(68, 68);
            iconImageOut = iconImg;

            // Dropdown
            GameObject dropdownGo = CreateDropdown(row, out Dropdown dropdown);
            dropdownOut = dropdown;

            return row;
        }

        private GameObject CreateLabel(GameObject parent, string text, out Text txtComponent)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent.transform, false);
            Text txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleLeft;
            txt.fontSize = 56;
            txt.color = Color.white;
            txtComponent = txt;
            return go;
        }

        private GameObject CreateButton(GameObject parent, string text, out Button buttonOut)
        {
            GameObject go = new GameObject("Button");
            go.transform.SetParent(parent.transform, false);
            Image bg = go.AddComponent<Image>();
            bg.color = _theme != null ? _theme.AccentColor : new Color(0.2f, 0.6f, 0.2f, 0.9f);
            buttonOut = go.AddComponent<Button>();
            Text txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 64;
            txt.color = Color.white;
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 90);
            return go;
        }

        private GameObject CreateDropdown(GameObject parentRow, out Dropdown dropdown)
        {
            GameObject go = new GameObject("Dropdown");
            go.transform.SetParent(parentRow.transform, false);
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            dropdown = go.AddComponent<Dropdown>();
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(450, 68);
            return go;
        }

        private void UpdateHerbIcon1(string herbName)
        {
            if (herbIconImage1 == null) return;
            // Try to load sprite from Resources/Icons/Herb/{herbName}
            string path = $"Icons/Herb/{herbName}";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                herbIconImage1.sprite = sprite;
                herbIconImage1.enabled = true;
            }
            else
            {
                // fallback to placeholder color
                herbIconImage1.sprite = null;
                herbIconImage1.color = new Color(0.6f, 0.2f, 0.2f, 0.8f); // reddish placeholder
                herbIconImage1.enabled = true;
            }
        }

        private void UpdateHerbIcon2(string herbName)
        {
            if (herbIconImage2 == null) return;
            // Try to load sprite from Resources/Icons/Herb/{herbName}
            string path = $"Icons/Herb/{herbName}";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                herbIconImage2.sprite = sprite;
                herbIconImage2.enabled = true;
            }
            else
            {
                // fallback to placeholder color
                herbIconImage2.sprite = null;
                herbIconImage2.color = new Color(0.6f, 0.2f, 0.2f, 0.8f); // reddish placeholder
                herbIconImage2.enabled = true;
            }
        }

        private void OnCraftClicked()
        {
            string herbName1 = herbDropdown1.options[herbDropdown1.value].text;
            string herbName2 = herbDropdown2.options[herbDropdown2.value].text;

            // Get herb info by display name
            var herbInfo1 = HerbDatabase.GetHerbInfoByDisplayName(herbName1);
            var herbInfo2 = HerbDatabase.GetHerbInfoByDisplayName(herbName2);

            if (string.IsNullOrEmpty(herbInfo1.id) || string.IsNullOrEmpty(herbInfo2.id))
            {
                resultText.text = "<b>오류:</b> 선택한 약초를 찾을 수 없습니다.";
                return;
            }

            // Get combination result
            var comboResult = HerbComboDatabase.GetCombo(herbInfo1.id, herbInfo2.id);
            if (!comboResult.HasValue)
            {
                resultText.text = $"<b>{herbName1}</b> + <b>{herbName2}</b> → 조합법이 없습니다.";
                return;
            }

            var result = comboResult.Value;

            // Create ItemData for herbs (required items)
            var herbItem1 = CreateItemDataFromHerbInfo(herbInfo1);
            var herbItem2 = CreateItemDataFromHerbInfo(herbInfo2);

            // Create ItemData for result
            var resultItem = CreateItemDataFromComboResult(result);

            // Create Recipe object
            var recipe = CreateRecipe(herbItem1, herbItem2, resultItem, herbName1, herbName2, result);

            // Perform crafting
            bool success = PerformAlchemyCraft(recipe);
            int awardedExp = recipe.expReward;

            // Runtime-created ScriptableObjects must be destroyed to prevent memory leaks
            Destroy(recipe);

            string msg = success
                ? $"<b>{herbName1}</b> + <b>{herbName2}</b> → <b>{result.resultName}</b>\nEffect: {result.effect}\n제조 성공! 경험치 {awardedExp} 획득"
                : $"<b>{herbName1}</b> + <b>{herbName2}</b> → <b>{result.resultName}</b>\nEffect: {result.effect}\n제조 실패. 재료가 소실되었습니다.";

            resultText.text = msg;
            Debug.Log($"[AlchemyUI] {msg.Replace("<b>", "").Replace("</b>", "")}");
        }

        private PlayerInventory.ItemData CreateItemDataFromHerbInfo(HerbInfo herbInfo)
        {
            return new PlayerInventory.ItemData
            {
                id = herbInfo.id,
                displayName = herbInfo.displayName,
                description = herbInfo.description,
                category = PlayerInventory.ItemCategory.Herb,
                icon = null, // Could load from Resources/Icons/Herb/{herbInfo.displayName} if needed
                maxStack = 20
            };
        }

        private PlayerInventory.ItemData CreateItemDataFromComboResult(HerbComboResult result)
        {
            // Determine category based on result name characteristics
            PlayerInventory.ItemCategory category = PlayerInventory.ItemCategory.Potion; // Default to potion
            
            string name = result.resultName;
            
            // Strong material indicators
            bool isStrongMaterial = name.Contains("접착제") ||  // adhesive
                                   name.Contains("코팅제") ||  // coating
                                   name.Contains("도구") ||    // tool
                                   name.Contains("재료") ||    // material
                                   name.Contains("합금제") ||  // alloy
                                   name.Contains("방패") ||    // shield
                                   name.Contains("트랩") ||    // trap
                                   name.Contains("장비") ||    // equipment
                                   (name.Contains("용액") && !name.Contains("생명")) ||  // solution (except life water related)
                                   (name.Contains("액") && 
                                    (name.Contains("강화") ||  // strengthening liquid
                                     name.Contains("보호") || // protection liquid
                                     name.Contains("내성") || // resistance liquid
                                     name.Contains("방어") || // defense liquid
                                     name.Contains("공격"))); // attack liquid
            
            // Strong consumable/potion indicators
            bool isStrongPotion = name.Contains("약") ||      // medicine/drug
                                 name.Contains("약물") ||    // medicine
                                 name.Contains("물약") ||    // explicit potion
                                 name.Contains("향수") ||    // perfume
                                 name.Contains("에이전트") || // agent
                                 name.Contains("제제") ||    // agent
                                 name.Contains("진액") ||    // essence
                                 name.Contains("추출물") ||  // extract
                                 name.Contains("오일") ||    // oil
                                 name.Contains("엑기스") ||   // concentrated extract
                                 name.Contains("수") && name.Length > 2 && !name.Contains("이슬") && !name.Contains("불") // water-like but not obvious compounds
                                 || (name.Contains("엘릭서") || name.Contains("육수")); // elixir, broth
            
            // Special cases that are clearly materials despite sounding like potions
            bool isForcedMaterial = name.Contains("불사의 장비") ||  // immortal equipment
                                   name.Contains("생명 보호막") ||    // life protection barrier (equipment buff)
                                   name.Contains("영웅의 신속");     // hero's swiftness (equipment buff)
            
            // Special cases that are clearly consumables despite material-like names
            bool isForcedPotion = name.Contains("괴력 물약") ||  // strongman potion (explicit)
                                 name.Contains("만능 치유액") || // panacea healing liquid
                                 name.Contains("순수한 생명수");  // pure life water
            
            if (isForcedMaterial || (isStrongMaterial && !isStrongPotion))
            {
                category = PlayerInventory.ItemCategory.Material;
            }
            else if (isForcedPotion || (isStrongPotion && !isStrongMaterial))
            {
                category = PlayerInventory.ItemCategory.Potion;
            }
            // Otherwise keep default (Potion) for borderline cases
            
            return new PlayerInventory.ItemData
            {
                id = $"combo_{result.resultId}",
                displayName = result.resultName,
                description = result.description,
                category = category,
                icon = null, // Could load from Resources/Items/{result.resultName} if available
                maxStack = 99
            };
        }

        private Recipe CreateRecipe(PlayerInventory.ItemData requiredItem1, PlayerInventory.ItemData requiredItem2, PlayerInventory.ItemData resultItem, 
                                  string herbName1, string herbName2, HerbComboResult result)
        {
            // Create a new Recipe instance (not a ScriptableObject, just a runtime object)
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.displayName = result.resultName;
            recipe.description = $"<b>{herbName1}</b> + <b>{herbName2}</b> → {result.resultName}\n효과: {result.effect}";
            recipe.requiredItem1 = requiredItem1;
            recipe.requiredItem2 = requiredItem2;
            recipe.resultItem = resultItem;
            recipe.baseSuccessRate = baseSuccessRate;
            recipe.difficultyPenalty = baseDifficultyPenalty;
            recipe.requiredLevel = requiredLevel;
            recipe.expReward = expReward;
            recipe.recipeType = Recipe.RecipeType.Alchemy;
            
            return recipe;
        }

        private bool PerformAlchemyCraft(Recipe recipe)
        {
            // Check if player meets level requirements
            if (!recipe.CanCraft())
            {
                Debug.Log($"[AlchemyUI] 플레이어 레벨이 부족합니다. 필요: {recipe.requiredLevel}");
                return false;
            }

            // Check if player has required items
            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogError("[AlchemyUI] PlayerInventory not found.");
                return false;
            }

            bool hasItem1 = inventory.HasItem(recipe.requiredItem1.id);
            bool hasItem2 = recipe.requiredItem2 == null || inventory.HasItem(recipe.requiredItem2.id);

            if (!hasItem1 || !hasItem2)
            {
                Debug.Log($"[AlchemyUI] 재료가 부족합니다. 필요: {recipe.requiredItem1.displayName} x1, {(recipe.requiredItem2 != null ? recipe.requiredItem2.displayName : "없음")} x1");
                return false;
            }

            // Calculate success rate using Recipe's centralized calculation
            int successRate = recipe.CalculateSuccessRate();
            Debug.Log($"[AlchemyUI] 최종 성공률: {successRate}% (기준: {recipe.baseSuccessRate}%, 레시피 타입: {recipe.recipeType}, 난이도: {recipe.difficultyPenalty})");

            // Roll for success
            bool success = Random.Range(0, 100) < successRate;

            if (success)
            {
                // Consume materials
                inventory.RemoveItem(recipe.requiredItem1.id, 1);
                if (recipe.requiredItem2 != null)
                    inventory.RemoveItem(recipe.requiredItem2.id, 1);

                // Give result item
                inventory.AddItem(recipe.resultItem, 1);

                // Award EXP
                if (PlayerStats.Instance != null)
                    PlayerStats.Instance.AddEXP(recipe.expReward);
                else
                    Debug.LogWarning("[AlchemyUI] PlayerStats.Instance is null, cannot award EXP.");
                
                Debug.Log($"[AlchemyUI] 🎉 제조 성공! {recipe.resultItem.displayName} 획득 및 {recipe.expReward} EXP 획득");
                RecipeDiscoverySystem.MarkDiscovered(recipe.resultItem.displayName);
            }
            else
            {
                // Failure: determine failure type (same as CraftingStation)
                float failureRoll = Random.value;
                bool preserve = false; // whether materials are preserved
                bool loseOne = false;  // lose one random material
                bool loseAll = false;  // lose all materials

                if (failureRoll < 0.3f) // 30% preserve materials
                {
                    preserve = true;
                    Debug.Log("[AlchemyUI] 제조 실패 but 재료 보존 (30%)");
                }
                else if (failureRoll < 0.8f) // 50% lose one random material
                {
                    loseOne = true;
                    Debug.Log("[AlchemyUI] 제조 실패: 랜덤 재료 하나 손실 (50%)");
                }
                else // 20% lose all materials
                {
                    loseAll = true;
                    Debug.Log("[AlchemyUI] 제조 실패: 모든 재료 손실 (20%)");
                }

                // Apply failure effects
                if (!preserve)
                {
                    if (loseAll)
                    {
                        inventory.RemoveItem(recipe.requiredItem1.id, 1);
                        if (recipe.requiredItem2 != null)
                            inventory.RemoveItem(recipe.requiredItem2.id, 1);
                    }
                    else if (loseOne)
                    {
                        // Randomly choose one of the required items to lose
                        // Build a list of available non-null items
                        var availableItems = new List<PlayerInventory.ItemData>();
                        if (recipe.requiredItem1 != null) availableItems.Add(recipe.requiredItem1);
                        if (recipe.requiredItem2 != null) availableItems.Add(recipe.requiredItem2);

                        if (availableItems.Count > 0)
                        {
                            var lostItem = availableItems[Random.Range(0, availableItems.Count)];
                            inventory.RemoveItem(lostItem.id, 1);
                        }
                    }
                }

                // No EXP awarded on failure
                Debug.Log("[AlchemyUI] 제조 실패. 경험치 획득 없음.");
            }

            return success;
        }

        private void OnResetClicked()
        {
            // Reset to first options
            if (herbDropdown1.options.Count > 0)
            {
                herbDropdown1.value = 0;
                UpdateHerbIcon1(herbDropdown1.options[0].text);
            }
            if (herbDropdown2.options.Count > 0)
            {
                herbDropdown2.value = 0;
                UpdateHerbIcon2(herbDropdown2.options[0].text);
            }
            resultText.text = "";
        }

        private void OnDestroy()
        {
            // Clean up listeners to prevent memory leaks
            if (herbDropdown1 != null)
                herbDropdown1.onValueChanged.RemoveAllListeners();
            if (herbDropdown2 != null)
                herbDropdown2.onValueChanged.RemoveAllListeners();
            if (craftButton != null)
                craftButton.onClick.RemoveAllListeners();
            if (resetButton != null)
                resetButton.onClick.RemoveAllListeners();
        }
    }
}