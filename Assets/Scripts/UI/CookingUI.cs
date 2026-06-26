     1|using UnityEngine;
     2|using ProjectName.Core;
     3|using ProjectName.Systems;
     4|using ProjectName.Core.Data;
     5|using ProjectName.UI.Themes;
     6|
     7|namespace ProjectName.UI
     8|{
     9|    /// <summary>
    10|    /// 요리 제작 UI (IMGUI, UIWindow 기반)
    11|    /// 고기 + 약초 조합으로 요리 제작
    12|    /// 성공/실패 시스템 적용
    13|    /// </summary>
    14|    public class CookingUI : UIWindow
    15|    {
    16|        protected virtual void Start()
    17|        {
    18|            ApplyTheme(Phase33_Themes.CreateMedievalCraftingTheme());
    19|        }
    20|
    21|        [Header("Cooking UI Settings")]
    22|        [SerializeField] private int _windowWidth = 1500;
    23|        [SerializeField] private int _windowHeight = 1305;
    24|
    25|        // ── 상태 ──
    26|        private PlayerInventory.ItemData _selectedMeat;
    27|        private PlayerInventory.ItemData _selectedHerb;
    28|        private string _resultMessage = "";
    29|        private string _resultItemName = "";
    30|        private string _resultEffect = "";
    31|        private string _resultGrade = "";
    32|        private bool _hasResult = false;
    33|        private Vector2 _meatScrollPos;
    34|        private Vector2 _herbScrollPos;
    35|        private int _gridColumns = 4;
    36|
    37|        // ── 스타일 ──
    38|        private GUIStyle _titleStyle;
    39|        private GUIStyle _slotStyle;
    40|        private GUIStyle _resultStyle;
    41|        private GUIStyle _categoryHeaderStyle;
    42|        private bool _stylesInitialized;
    43|
    44|        protected override void OnShow()
    45|        {
    46|            base.OnShow();
    47|            _selectedMeat = null;
    48|            _selectedHerb = null;
    49|            _resultMessage = "";
    50|            _hasResult = false;
    51|            _stylesInitialized = false;
    52|            Debug.Log("[CookingUI] 요리 테이블 열림");
    53|        }
    54|
    55|        protected override void OnHide()
    56|        {
    57|            base.OnHide();
    58|            Debug.Log("[CookingUI] 요리 테이블 닫힘");
    59|        }
    60|
    61|        public void Open()
    62|        {
    63|            if (!_isOpen)
    64|                Show();
    65|        }
    66|
    67|        private void InitializeStyles()
    68|        {
    69|            if (_stylesInitialized) return;
    70|
    71|            _titleStyle = new GUIStyle(GUI.skin.label)
    72|            {
    73|                fontSize = 72,
    74|                fontStyle = FontStyle.Bold,
    75|                alignment = TextAnchor.MiddleLeft,
    76|                normal = { textColor = Color.white }
    77|            };
    78|
    79|            _slotStyle = new GUIStyle(GUI.skin.box)
    80|            {
    81|                fontSize = 48,
    82|                alignment = TextAnchor.MiddleCenter,
    83|                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f, 0.9f)) }
    84|            };
    85|
    86|            _resultStyle = new GUIStyle(GUI.skin.box)
    87|            {
    88|                fontSize = 52,
    89|                alignment = TextAnchor.MiddleLeft,
    90|                normal = { textColor = new Color(0.7f, 1f, 0.7f), background = MakeTexture(1, 1, new Color(0.1f, 0.25f, 0.1f, 0.8f)) }
    91|            };
    92|
    93|            _categoryHeaderStyle = new GUIStyle(GUI.skin.label)
    94|            {
    95|                fontSize = 44,
    96|                fontStyle = FontStyle.Bold,
    97|                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
    98|            };
    99|
   100|            _stylesInitialized = true;
   101|        }
   102|
   103|        private Texture2D MakeTexture(int w, int h, Color color)
   104|        {
   105|            var tex = new Texture2D(w, h);
   106|            for (int x = 0; x < w; x++)
   107|                for (int y = 0; y < h; y++)
   108|                    tex.SetPixel(x, y, color);
   109|            tex.Apply();
   110|            return tex;
   111|        }
   112|
   113|        private void OnGUI()
   114|        {
   115|            if (!_isOpen) return;
   116|
   117|            // G3-05: 통일 스타일 — 딤드 오버레이 + 배경 + 타이틀 + 닫기 버튼
   118|            UIStyleManager.DrawDimOverlay();
   119|            float _winX = (Screen.width - _windowWidth) / 2f;
   120|            float _winY = (Screen.height - _windowHeight) / 2f;
   121|            Rect _winRect = new Rect(_winX, _winY, _windowWidth, _windowHeight);
   122|            UIStyleManager.DrawWindowBackground(_winRect);
   123|            UIStyleManager.DrawTitle(_winRect, "  🍳 요리 테이블");
   124|            if (UIStyleManager.DrawCloseButton(_winRect))
   125|            {
   126|                Hide();
   127|                return;
   128|            }
   129|
   130|            InitializeStyles();
   131|
   132|            // 메인 윈도우 영역 (중앙)
   133|            float x = _winX;
   134|            float y = _winY;
   135|            Rect windowRect = _winRect;
   136|
   137|            GUILayout.BeginArea(windowRect, GUI.skin.box);
   138|
   139|            // ── 제목 표시줄 ──
   140|            GUILayout.BeginHorizontal();
   141|            GUILayout.Label("  🍳 요리 테이블", _titleStyle);
   142|            GUILayout.FlexibleSpace();
   143|            if (GUILayout.Button("닫기 X", GUILayout.Width(90), GUILayout.Height(36)))
   144|            {
   145|                Hide();
   146|                return;
   147|            }
   148|            GUILayout.EndHorizontal();
   149|
   150|            GUILayout.Space(8);
   151|
   152|            // ── 재료 슬롯 2개 ──
   153|            GUILayout.BeginHorizontal(GUILayout.Height(120));
   154|            DrawIngredientSlot("고기", ref _selectedMeat, Color.green);
   155|            GUILayout.FlexibleSpace();
   156|            DrawIngredientSlot("약초", ref _selectedHerb, Color.cyan);
   157|            GUILayout.EndHorizontal();
   158|
   159|            GUILayout.Space(6);
   160|
   161|            // ── 요리하기 버튼 ──
   162|            bool canCook = _selectedMeat != null && _selectedHerb != null;
   163|            GUI.enabled = canCook;
   164|            if (GUILayout.Button("▼ 요리하기 ▼", GUILayout.Height(54)))
   165|            {
   166|                TryCook();
   167|            }
   168|            GUI.enabled = true;
   169|
   170|            GUILayout.Space(6);
   171|
   172|            // ── 결과 표시 ──
   173|            if (_hasResult)
   174|            {
   175|                string resultText = string.IsNullOrEmpty(_resultItemName)
   176|                    ? $"{_resultMessage}"
   177|                    : $"{_resultMessage}\n효과: {_resultEffect}\n등급: {_resultGrade}";
   178|                GUILayout.Box(resultText, _resultStyle, GUILayout.Height(90));
   179|            }
   180|            else if (!string.IsNullOrEmpty(_resultMessage))
   181|            {
   182|                var style = new GUIStyle(GUI.skin.box)
   183|                {
   184|                    fontSize = 52,
   185|                    alignment = TextAnchor.MiddleCenter,
   186|                    normal = { textColor = new Color(1f, 0.6f, 0.4f), background = MakeTexture(1, 1, new Color(0.25f, 0.1f, 0.1f, 0.8f)) }
   187|                };
   188|                GUILayout.Box(_resultMessage, style, GUILayout.Height(60));
   189|            }
   190|            else
   191|            {
   192|                GUILayout.Box("고기와 약초를 선택하고 요리해보세요.", GUILayout.Height(54));
   193|            }
   194|
   195|            GUILayout.Space(4);
   196|
   197|            // ── 구분선 ──
   198|            GUILayout.Label("─── 인벤토리 (고기) ───", _categoryHeaderStyle);
   199|
   200|            // ── 고기 인벤토리 그리드 ──
   201|            float availableWidth = _windowWidth - 30;
   202|            float itemSlotSize = Mathf.Min(80, (availableWidth - (_gridColumns - 1) * 6) / _gridColumns);
   203|            float gridHeight = (_windowHeight - 340) / 2f;
   204|            if (gridHeight < 60) gridHeight = 60;
   205|
   206|            _meatScrollPos = GUILayout.BeginScrollView(_meatScrollPos, GUILayout.Height(gridHeight));
   207|
   208|            var inventory = PlayerInventory.Instance;
   209|            if (inventory != null)
   210|            {
   211|                var allSlots = inventory.GetAllSlots();
   212|                int cols = Mathf.Max(1, (int)(availableWidth / (itemSlotSize + 6)));
   213|                if (cols < 1) cols = 1;
   214|
   215|                GUILayout.BeginVertical();
   216|                int idx = 0;
   217|                foreach (var slot in allSlots)
   218|                {
   219|                    if (slot == null || slot.item == null || slot.count <= 0)
   220|                    {
   221|                        idx++;
   222|                        continue;
   223|                    }
   224|                    if (slot.item.category != PlayerInventory.ItemCategory.Meat)
   225|                    {
   226|                        idx++;
   227|                        continue;
   228|                    }
   229|
   230|                    if (idx % cols == 0)
   231|                        GUILayout.BeginHorizontal();
   232|
   233|                    DrawInventoryFoodSlot(slot, itemSlotSize, true);
   234|
   235|                    idx++;
   236|                    if (idx % cols == 0)
   237|                        GUILayout.EndHorizontal();
   238|                }
   239|                if (idx % cols != 0)
   240|                    GUILayout.EndHorizontal();
   241|                GUILayout.EndVertical();
   242|            }
   243|
   244|            GUILayout.EndScrollView();
   245|
   246|            GUILayout.Space(2);
   247|
   248|            // ── 구분선 ──
   249|            GUILayout.Label("─── 인벤토리 (약초) ───", _categoryHeaderStyle);
   250|
   251|            // ── 약초 인벤토리 그리드 ──
   252|            _herbScrollPos = GUILayout.BeginScrollView(_herbScrollPos, GUILayout.Height(gridHeight));
   253|
   254|            if (inventory != null)
   255|            {
   256|                var allSlots = inventory.GetAllSlots();
   257|                int cols = Mathf.Max(1, (int)(availableWidth / (itemSlotSize + 6)));
   258|                if (cols < 1) cols = 1;
   259|
   260|                GUILayout.BeginVertical();
   261|                int idx = 0;
   262|                foreach (var slot in allSlots)
   263|                {
   264|                    if (slot == null || slot.item == null || slot.count <= 0)
   265|                    {
   266|                        idx++;
   267|                        continue;
   268|                    }
   269|                    if (slot.item.category != PlayerInventory.ItemCategory.Herb)
   270|                    {
   271|                        idx++;
   272|                        continue;
   273|                    }
   274|
   275|                    if (idx % cols == 0)
   276|                        GUILayout.BeginHorizontal();
   277|
   278|                    DrawInventoryFoodSlot(slot, itemSlotSize, false);
   279|
   280|                    idx++;
   281|                    if (idx % cols == 0)
   282|                        GUILayout.EndHorizontal();
   283|                }
   284|                if (idx % cols != 0)
   285|                    GUILayout.EndHorizontal();
   286|                GUILayout.EndVertical();
   287|            }
   288|
   289|            GUILayout.EndScrollView();
   290|
   291|            GUILayout.EndArea();
   292|        }
   293|
   294|        /// <summary>
   295|        /// 재료 슬롯 (고기/약초 선택)
   296|        /// </summary>
   297|        private void DrawIngredientSlot(string label, ref PlayerInventory.ItemData slot, Color accentColor)
   298|        {
   299|            GUILayout.BeginVertical(GUILayout.Width(240), GUILayout.Height(120));
   300|
   301|            GUILayout.Label(label, GUILayout.Height(27));
   302|
   303|            Rect slotRect = GUILayoutUtility.GetRect(120, 54);
   304|            var slotBg = MakeTexture(1, 1, new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f, 0.7f));
   305|            GUI.Box(slotRect, "", _slotStyle);
   306|            GUI.DrawTexture(slotRect, slotBg);
   307|
   308|            if (slot != null)
   309|            {
   310|                GUI.Label(new Rect(slotRect.x + 4, slotRect.y + 4, slotRect.width - 8, 30), slot.displayName);
   311|
   312|                // 우클릭 감지 → 슬롯 비우기
   313|                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && slotRect.Contains(Event.current.mousePosition))
   314|                {
   315|                    slot = null;
   316|                    _hasResult = false;
   317|                    _resultMessage = "";
   318|                    Event.current.Use();
   319|                }
   320|
   321|                if (GUI.Button(new Rect(slotRect.xMax - 20, slotRect.y + 4, 24, 24), "X"))
   322|                {
   323|                    slot = null;
   324|                    _hasResult = false;
   325|                    _resultMessage = "";
   326|                }
   327|            }
   328|            else
   329|            {
   330|                GUI.Label(slotRect, "  [비어있음]\n  (아이템 클릭)", _slotStyle);
   331|            }
   332|
   333|            GUILayout.EndVertical();
   334|        }
   335|
   336|        /// <summary>
   337|        /// 인벤토리 아이템 슬롯 (고기 또는 약초)
   338|        /// </summary>
   339|        private void DrawInventoryFoodSlot(PlayerInventory.ItemSlot invSlot, float size, bool isMeat)
   340|        {
   341|            var item = invSlot.item;
   342|            string label = $"{item.displayName}\nx{invSlot.count}";
   343|
   344|            Rect rect = GUILayoutUtility.GetRect(size, size);
   345|
   346|            // 배경
   347|            Color bgColor = isMeat
   348|                ? new Color(0.5f, 0.25f, 0.1f, 0.8f)
   349|                : new Color(0.15f, 0.4f, 0.15f, 0.8f);
   350|
   351|            // 이미 선택된 아이템이면 하이라이트
   352|            bool isSelected = isMeat
   353|                ? (_selectedMeat != null && _selectedMeat.id == item.id)
   354|                : (_selectedHerb != null && _selectedHerb.id == item.id);
   355|            if (isSelected)
   356|                bgColor = isMeat ? new Color(0.6f, 0.4f, 0.2f, 0.9f) : new Color(0.3f, 0.6f, 0.3f, 0.9f);
   357|
   358|            var slotBg = MakeTexture(1, 1, bgColor);
   359|            GUI.DrawTexture(rect, slotBg);
   360|
   361|            // 아이콘 (색상 사각형 fallback)
   362|            Color iconColor = isMeat ? new Color(0.8f, 0.4f, 0.2f) : new Color(0.2f, 0.7f, 0.2f);
   363|            GUI.color = iconColor;
   364|            GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, size - 8, size - 24), Texture2D.whiteTexture);
   365|            GUI.color = Color.white;
   366|
   367|            // 이름 + 개수
   368|            GUI.Label(new Rect(rect.x + 2, rect.y + size - 22, rect.width - 4, 30), label, new GUIStyle(GUI.skin.label)
   369|            {
   370|                fontSize = 40,
   371|                alignment = TextAnchor.MiddleCenter,
   372|                normal = { textColor = Color.white }
   373|            });
   374|
   375|            // 클릭 감지
   376|            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
   377|            {
   378|                if (isMeat)
   379|                    _selectedMeat = item;
   380|                else
   381|                    _selectedHerb = item;
   382|                _resultMessage = "";
   383|                _hasResult = false;
   384|                Event.current.Use();
   385|            }
   386|        }
   387|
   388|        /// <summary>
   389|        /// 요리 시도
   390|        /// </summary>
   391|        private void TryCook()
   392|        {
   393|            if (_selectedMeat == null || _selectedHerb == null)
   394|            {
   395|                _resultMessage = "고기와 약초를 선택해주세요.";
   396|                _hasResult = false;
   397|                return;
   398|            }
   399|
   400|            var inventory = PlayerInventory.Instance;
   401|            if (inventory == null)
   402|            {
   403|                _resultMessage = "인벤토리를 찾을 수 없습니다.";
   404|                _hasResult = false;
   405|                return;
   406|            }
   407|
   408|            // 인벤토리에 재료 확인
   409|            if (!inventory.HasItem(_selectedMeat.id) || inventory.GetItemCount(_selectedMeat.id) < 1)
   410|            {
   411|                _resultMessage = $"'{_selectedMeat.displayName}'이(가) 인벤토리에 없습니다.";
   412|                _hasResult = false;
   413|                return;
   414|            }
   415|            if (!inventory.HasItem(_selectedHerb.id) || inventory.GetItemCount(_selectedHerb.id) < 1)
   416|            {
   417|                _resultMessage = $"'{_selectedHerb.displayName}'이(가) 인벤토리에 없습니다.";
   418|                _hasResult = false;
   419|                return;
   420|            }
   421|
   422|            // CookingDatabase에서 레시피 검색 (displayName 기반)
   423|            var cookingResult = CookingDatabase.GetCooking(_selectedMeat.displayName, _selectedHerb.displayName);
   424|
   425|            if (cookingResult.HasValue)
   426|            {
   427|                var result = cookingResult.Value;
   428|
   429|                // 재료 등급 추정
   430|                string grade1 = CraftSuccessSystem.GetGradeFromItemId(_selectedMeat.id);
   431|                string grade2 = CraftSuccessSystem.GetGradeFromItemId(_selectedHerb.id);
   432|
   433|                // 성공/실패 판정 (요리 = false)
   434|                CraftResult craftResult = CraftSuccessSystem.ExecuteCraft(false, grade1, grade2);
   435|
   436|                switch (craftResult)
   437|                {
   438|                    case CraftResult.Success:
   439|                        // 재료 차감
   440|                        inventory.RemoveItem(_selectedMeat.id, 1);
   441|                        inventory.RemoveItem(_selectedHerb.id, 1);
   442|
   443|                        // 요리 아이템 생성 및 지급
   444|                        var dishItem = DishDatabase.GetItemData(result.DishName);
   445|                        if (dishItem != null)
   446|                            inventory.AddItem(dishItem, 1);
   447|
   448|                        // 발견 등록
   449|                        RecipeDiscoverySystem.MarkDiscovered(result.DishName);
   450|
   451|                        // 미식 등급 조회 (DishInfo에서)
   452|                        var dishInfo = DishDatabase.GetDishInfoByName(result.DishName);
   453|                        string gradeStr = "";
   454|                        if (dishInfo != null && dishInfo.StarRating > 0)
   455|                        {
   456|                            var gourmetGrade = GourmetDatabase.GetGrade(dishInfo.StarRating);
   457|                            gradeStr = gourmetGrade.HasValue
   458|                                ? $"★{dishInfo.StarRating} {gourmetGrade.Value.GradeName}"
   459|                                : $"★{dishInfo.StarRating}";
   460|                        }
   461|                        else
   462|                        {
   463|                            gradeStr = "일반";
   464|                        }
   465|
   466|                        _resultMessage = $"🟢 요리 성공! '{result.DishName}' 획득!";
   467|                        _resultItemName = result.DishName;
   468|                        _resultEffect = result.Effect;
   469|                        _resultGrade = gradeStr;
   470|                        _hasResult = true;
   471|
   472|                        // 경험치 획득 (요리 성공 시 +5~15)
   473|                        if (PlayerStats.Instance != null)
   474|                            PlayerStats.Instance.AddExp(Random.Range(5, 16));
   475|
   476|                        Debug.Log($"[CookingUI] 요리 성공: {_selectedMeat.displayName} + {_selectedHerb.displayName} → {result.DishName}");
   477|
   478|                        // 슬롯 비우기
   479|                        _selectedMeat = null;
   480|                        _selectedHerb = null;
   481|                        break;
   482|
   483|                    case CraftResult.Fail_MaterialPreserved:
   484|                        _resultMessage = "🟡 요리 실패... 재료가 보존되었다.";
   485|                        _hasResult = false;
   486|                        _resultItemName = "";
   487|                        _resultEffect = "";
   488|                        _resultGrade = "";
   489|                        Debug.Log($"[CookingUI] 요리 실패 (재료보존): {_selectedMeat.displayName} + {_selectedHerb.displayName}");
   490|                        break;
   491|
   492|                    case CraftResult.Fail_MaterialDestroyed:
   493|                        bool destroyMeat = Random.value < 0.5f;
   494|                        string destroyedName = destroyMeat ? _selectedMeat.displayName : _selectedHerb.displayName;
   495|                        string destroyedId = destroyMeat ? _selectedMeat.id : _selectedHerb.id;
   496|                        inventory.RemoveItem(destroyedId, 1);
   497|
   498|                        _resultMessage = $"🔴 요리 실패! '{destroyedName}'이(가) 소멸했다!";
   499|                        _hasResult = false;
   500|                        _resultItemName = "";
   501|