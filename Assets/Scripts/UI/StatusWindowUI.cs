using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;

namespace ProjectName.UI
{
    /// <summary>
    /// 플레이어 스탯 창 (Phase Stats) — P키로 열기/닫기, ESC로 닫기.
    ///
    /// [구현 방식]
    /// - HotbarUI와 동일한 "코드 생성 UI" 방식 (프리팹/씬 편집 불필요, 씬 의존 0).
    ///   셀프 부트스트랩(RuntimeInitializeOnLoadMethod) → 전용 Canvas(StatusCanvas) 자동 생성.
    /// - UI 표준: legacy UnityEngine.UI.Text (TMP 혼합 금지), 한글 라벨.
    /// - 핫바(하단 중앙)와 겹치지 않도록 좌측 상단 고정 패널, 다크 반투명 테마.
    ///
    /// [표시 항목]
    ///   레벨 / 경험치(게이지) / 체력(게이지) / 골드 / 공격력·방어력·치명타·이동속도(Final*)
    ///   / 연금술·요리 보너스% / 화술 호감도 / 전투 보너스% / 중독도(DrugEffectSystem)
    ///
    /// [소유권 주의]
    /// - PlayerStats / PlayerHealth / DrugEffectSystem는 타 에이전트 소유(Core) — 본 파일은 공개 API 소비만 수행(수정 금지).
    /// - KeyBindings의 "Status" 액션(기본키 P)과 동일 키를 본 창이 직접 입력 처리(UIManager 라우팅 없는 셀프 토글).
    /// </summary>
    public class StatusWindowUI : MonoBehaviour
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static StatusWindowUI _instance;
        public static StatusWindowUI Instance => _instance;

        /// <summary>
        /// 씬 편집 없이 스탯 창을 사용하기 위한 셀프 부트스트랩.
        /// 선례: UI/HotbarUI.cs의 RuntimeInitializeOnLoadMethod 패턴.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var existing = Object.FindFirstObjectByType<StatusWindowUI>();
            if (existing != null) { _instance = existing; return; }

            var go = new GameObject("StatusWindowUI");
            _instance = go.AddComponent<StatusWindowUI>();
        }

        // ===== 설정 =====
        [Header("Window Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.P; // KeyBindings "Status"와 동일 (C는 은신(PlayerMovement)과 충돌 → P로 이동)
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

        // ===== 레이아웃 상수 (1080p 디자인 기준 — CanvasScaler가 해상도 스케일) =====
        private const float PanelMargin   = 12f;  // 화면 좌상단 여백
        private const float PanelWidth    = 330f;
        private const float PanelPad      = 12f;  // 패널 내부 여백
        private const float TitleHeight   = 34f;
        private const float RowHeight     = 26f;
        private const float GaugeHeight   = 6f;   // EXP/HP 게이지 높이
        private const float GaugeGap      = 4f;   // 게이지와 아래 행 사이 간격
        private const float FooterHeight  = 22f;

        // 표시 행 수 (레벨/경험치/체력/골드/공격/방어/치명/이속/연금/요리/화술/전투/중독 = 13)
        private const int   ValueRowCount = 13;
        // 게이지 행(경험치, 체력)만큼 추가 높이
        private const float PanelHeight = PanelPad + TitleHeight
                                        + RowHeight * ValueRowCount
                                        + (GaugeHeight + GaugeGap) * 2
                                        + FooterHeight + PanelPad;

        // ===== 다크 테마 색상 (HotbarUI 톤 유지) =====
        private static readonly Color ColorPanelBg   = new Color(0.06f, 0.06f, 0.08f, 0.80f);
        private static readonly Color ColorRowBg     = new Color(0.13f, 0.13f, 0.16f, 0.55f);
        private static readonly Color ColorGaugeBg   = new Color(0f, 0f, 0f, 0.60f);
        private static readonly Color ColorGaugeExp  = new Color(0.35f, 0.65f, 1f, 0.95f);  // 경험치 파랑
        private static readonly Color ColorGaugeHP   = new Color(0.35f, 0.85f, 0.40f, 0.95f); // 체력 초록
        private static readonly Color ColorTitle     = new Color(1f, 0.85f, 0.30f, 1f);     // 타이틀 골드
        private static readonly Color ColorLabel     = new Color(0.78f, 0.78f, 0.82f, 1f);
        private static readonly Color ColorValue     = new Color(0.55f, 1f, 0.60f, 1f);
        private static readonly Color ColorFooter    = new Color(0.62f, 0.62f, 0.68f, 0.95f);
        private static readonly Color ColorLevelUp   = new Color(1f, 0.85f, 0.30f, 1f);

        // ===== 런타임 상태 =====
        private Canvas _canvas;
        private Font _font;
        private Sprite _whiteSprite;
        private Sprite _roundedSprite;

        private GameObject _panelRoot;
        private Text _expValueText;
        private Text _hpValueText;
        private RectTransform _expGaugeFill;
        private RectTransform _hpGaugeFill;
        private const float GaugeWidth = PanelWidth - PanelPad * 2f;
        private const float GaugeInnerWidth = GaugeWidth - 16f; // 게이지 배경 실제 너비(fill 최대폭) — 좌우 8px 인셋

        private GameObject _levelUpPopup; // "LEVEL UP! Lv.N" 팝업 (2초 페이드)
        private Coroutine _levelUpFadeCo;

        private bool _isOpen;
        private float _refreshTimer;
        private PlayerStats _subscribedStats; // OnLevelChanged 구독 중인 인스턴스 (씬 전환 대비)

        // ===== 생명주기 =====
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            BuildCanvas();
            BuildPanel();

            // 씬 전환에도 유지 (GameStatsWindow/HotbarUI 선례와 동일)
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            // 구독 해제 누수 방지 (계획서 P4 정적 검증 항목)
            if (_subscribedStats != null)
            {
                _subscribedStats.OnLevelChanged -= OnStatsLevelChanged;
                _subscribedStats = null;
            }
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // 토글 키 (GameStatsWindow의 Update 키 체크→Toggle 패턴 동일)
            if (Input.GetKeyDown(_toggleKey))
                Toggle();

            // ESC: 열려있을 때만 닫기
            if (_isOpen && Input.GetKeyDown(_closeKey))
                Close();

            // 씬 재로드 대비: PlayerStats는 DontDestroyOnLoad가 아니므로 씬 전환 시 인스턴스 교체.
            // 창이 닫혀 있어도 새 인스턴스를 재구독해야 OnLevelChanged(레벨업 팝업)를 놓치지 않음.
            // (ReferenceEquals 단락으로 매 프레임 비용 무시 가능)
            EnsureLevelSubscription();

            // 열려있을 때 0.25초 폴링 갱신 (HP/EXP 변화는 이벤트가 없어 폴링으로 실시간 표시)
            if (!_isOpen) return;
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < 0.25f) return;
            _refreshTimer = 0f;
            RefreshDisplay();
        }

        // =====================================================================
        //  공개 메서드
        // =====================================================================

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _refreshTimer = 1f; // 다음 프레임 즉시 갱신
            if (_panelRoot != null) _panelRoot.SetActive(true);
            EnsureLevelSubscription();
            RefreshDisplay();
            Debug.Log($"[StatusWindowUI] 스탯 창 열림 (키: {_toggleKey})");
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);
            Debug.Log("[StatusWindowUI] 스탯 창 닫힘");
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public bool IsOpen => _isOpen;

        // =====================================================================
        //  데이터 갱신 (PlayerStats/PlayerHealth null 가드 필수)
        // =====================================================================

        /// <summary>
        /// PlayerStats.Instance 변화(씬 재로드 등) 감지 후 OnLevelChanged 구독 유지.
        /// 창이 닫혀 있어도 구독은 유지 → 어디서 레벨업해도 팝업 표시 가능.
        /// </summary>
        private void EnsureLevelSubscription()
        {
            var stats = PlayerStats.Instance;
            if (ReferenceEquals(_subscribedStats, stats)) return;

            if (_subscribedStats != null)
                _subscribedStats.OnLevelChanged -= OnStatsLevelChanged;

            _subscribedStats = stats;
            if (_subscribedStats != null)
                _subscribedStats.OnLevelChanged += OnStatsLevelChanged;
        }

        private void OnStatsLevelChanged(int newLevel, int oldLevel)
        {
            if (_isOpen) RefreshDisplay();
            ShowLevelUpPopup(newLevel); // 창이 닫혀 있어도 레벨업 피드백 표시
        }

        private void RefreshDisplay()
        {
            EnsureLevelSubscription();

            PlayerStats stats = PlayerStats.Instance;
            if (stats == null)
            {
                // PlayerStats 미존재 씬(메인 메뉴 등) — 플레이스홀더
                if (_expValueText != null) _expValueText.text = "-";
                if (_hpValueText != null) _hpValueText.text = "-";
                if (_expGaugeFill != null) _expGaugeFill.sizeDelta = new Vector2(0f, 0f);
                if (_hpGaugeFill != null) _hpGaugeFill.sizeDelta = new Vector2(0f, 0f);
                return;
            }

            // --- 경험치 (게이지) ---
            int level = stats.Level;
            if (level >= PlayerStats.MaxLevel)
            {
                if (_expValueText != null) _expValueText.text = "MAX";
                if (_expGaugeFill != null) _expGaugeFill.sizeDelta = new Vector2(GaugeInnerWidth, 0f);
            }
            else
            {
                int curExp = stats.CurrentEXP;
                int nextExp = stats.GetExpForLevel(level + 1); // 다음 레벨 도달 필요 누적 경험치
                int needExp = Mathf.Max(0, nextExp - curExp);
                if (_expValueText != null)
                    _expValueText.text = $"{curExp:N0} / {nextExp:N0} (+{needExp:N0})";

                int prevExp = stats.GetExpForLevel(level); // 현재 레벨 도달 누적치
                int span = Mathf.Max(1, nextExp - prevExp);
                float expRatio = Mathf.Clamp01((curExp - prevExp) / (float)span);
                if (_expGaugeFill != null) _expGaugeFill.sizeDelta = new Vector2(GaugeInnerWidth * expRatio, 0f);
            }

            // --- 체력 (게이지) — PlayerHealth가 실제 HP, 없으면 스탯 파생 HPBase ---
            PlayerHealth health = PlayerHealth.Instance;
            float maxHP = health != null ? health.MaxHP : stats.HPBase;
            float curHP = health != null ? health.CurrentHP : maxHP;
            if (_hpValueText != null)
                _hpValueText.text = $"{curHP:F0} / {maxHP:F0}";
            if (_hpGaugeFill != null)
                _hpGaugeFill.sizeDelta = new Vector2(GaugeInnerWidth * (maxHP > 0f ? Mathf.Clamp01(curHP / maxHP) : 0f), 0f);

            // --- 나머지 행 값 갱신 ---
            SetRowValue(0,  $"Lv.{level}");
            SetRowValue(3,  $"{stats.Gold:N0} G");
            SetRowValue(4,  $"{stats.FinalAttackDamage:F1}");
            SetRowValue(5,  $"{stats.FinalDefense:F1}");
            SetRowValue(6,  $"{stats.FinalCritChance * 100f:F1}%");
            SetRowValue(7,  $"{stats.FinalMoveSpeed:F1}");
            SetRowValue(8,  $"+{stats.AlchemySuccessBonus * 100f:F0}%");
            SetRowValue(9,  $"+{stats.CookingSuccessBonus * 100f:F0}%");
            SetRowValue(10, $"+{stats.SpeechAffinityBonus}");
            SetRowValue(11, $"+{stats.CombatDamageBonus * 100f:F0}%");
            SetRowValue(12, $"{DrugEffectSystem.DrugAddictionLevel:F0}% ({DrugEffectSystem.GetAddictionLabel()})");
        }

        private void SetRowValue(int rowIndex, string value)
        {
            if (rowIndex < 0 || rowIndex >= _valueTexts.Length) return;
            var txt = _valueTexts[rowIndex];
            if (txt != null) txt.text = value;
        }

        private readonly Text[] _valueTexts = new Text[ValueRowCount];

        // =====================================================================
        //  레벨업 팝업 — "LEVEL UP! Lv.N" 화면 중앙 상단, 2초 페이드
        // =====================================================================

        /// <summary>레벨업 팝업 발화 (OnStatsLevelChanged에서 호출). 씬 어디서든 정적 호출 가능.</summary>
        public static void ShowLevelUpPopup(int newLevel)
        {
            if (_instance == null)
            {
                Bootstrap();
                if (_instance == null) return;
            }
            _instance.BuildLevelUpPopup(newLevel);
        }

        private void BuildLevelUpPopup(int newLevel)
        {
            if (_canvas == null) return;

            // 이전 팝업 정리 (연속 레벨업 대비)
            if (_levelUpPopup != null) Destroy(_levelUpPopup);
            if (_levelUpFadeCo != null) StopCoroutine(_levelUpFadeCo);

            var rt = CreateText(_canvas.transform, "LevelUpPopup",
                $"LEVEL UP!  Lv.{newLevel}", 44, ColorLevelUp,
                TextAnchor.MiddleCenter, new Vector2(0f, -110f), new Vector2(700f, 70f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            // 가독성용 외곽선
            var text = rt.GetComponent<Text>();
            text.fontStyle = FontStyle.Bold;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            _levelUpPopup = rt.gameObject;
            _levelUpFadeCo = StartCoroutine(FadeOutLevelUpPopup(text));
        }

        private IEnumerator FadeOutLevelUpPopup(Text text)
        {
            // 1.4초 유지 후 0.6초 페이드 (총 2초 연출)
            float elapsed = 0f;
            Color baseColor = text != null ? text.color : ColorLevelUp;

            while (elapsed < 1.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float fade = 0f;
            while (fade < 0.6f)
            {
                fade += Time.unscaledDeltaTime;
                if (text != null)
                    text.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - fade / 0.6f);
                yield return null;
            }

            if (_levelUpPopup != null) Destroy(_levelUpPopup);
            _levelUpPopup = null;
            _levelUpFadeCo = null;
        }

        // =====================================================================
        //  UI 빌드 (코드 생성 — 프리팹/씬 편집 불필요)
        // =====================================================================

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("StatusCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200; // 핫바(100)보다 위, 팝업 포함 최상위 UI

            // 1080p 디자인 해상도 기준 스케일 (HotbarUI와 동일)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            _whiteSprite = CreateWhiteSprite();
            _roundedSprite = CreateRoundedSprite(64, 16);
            _font = LoadBuiltinFont();
        }

        private void BuildPanel()
        {
            // 좌측 상단 고정 패널 (핫바는 하단 중앙 → 안 겹침)
            RectTransform panel = CreateImage(
                _canvas.transform, "StatusPanel", _roundedSprite, ColorPanelBg,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(PanelMargin, -PanelMargin), new Vector2(PanelWidth, PanelHeight));
            panel.pivot = new Vector2(0f, 1f); // 좌상단 기준
            var panelImg = panel.GetComponent<Image>();
            panelImg.type = Image.Type.Sliced;

            _panelRoot = panel.gameObject;
            _panelRoot.SetActive(false); // 초기 닫힘

            float y = PanelPad;

            // [앵커 규약] 패널 자식 전부 좌상단 앵커(0,1) + 음수 y (위→아래 배치).
            // 라벨 pivot(0,0.5) 좌측정렬 / 값 pivot(1,0.5) 우측정렬 / 중앙 요소 pivot(0.5,0.5).

            // --- 타이틀 ---
            RectTransform title = CreateText(
                panel, "Title", "플레이어 스탯", 24, ColorTitle,
                TextAnchor.MiddleCenter, new Vector2(PanelWidth * 0.5f, -(y + TitleHeight * 0.5f)),
                new Vector2(PanelWidth - PanelPad * 2f, TitleHeight),
                new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
            title.GetComponent<Text>().fontStyle = FontStyle.Bold;
            y += TitleHeight;

            // --- 값 행 13개 (라벨 좌측 / 값 우측) ---
            string[] labels =
            {
                "레벨", "경험치", "체력", "골드",
                "공격력", "방어력", "치명타", "이동속도",
                "연금술 보너스", "요리 보너스", "화술 호감도", "전투 보너스",
                "중독도",
            };

            for (int i = 0; i < ValueRowCount; i++)
            {
                // 행 배경 (짝수 행만 은은한 줄무늬)
                if (i % 2 == 0)
                {
                    RectTransform rowBg = CreateImage(
                        panel, $"Row{i}_Bg", _whiteSprite, ColorRowBg,
                        new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(PanelPad, -(y + RowHeight * 0.5f)), new Vector2(PanelWidth - PanelPad * 2f, RowHeight));
                    rowBg.pivot = new Vector2(0f, 0.5f);
                    rowBg.GetComponent<Image>().raycastTarget = false;
                }

                // 라벨 (좌측)
                RectTransform label = CreateText(
                    panel, $"Row{i}_Label", labels[i], 16, ColorLabel,
                    TextAnchor.MiddleLeft, new Vector2(PanelPad + 8f, -(y + RowHeight * 0.5f)),
                    new Vector2(PanelWidth * 0.5f, RowHeight),
                    new Vector2(0f, 1f), new Vector2(0f, 0.5f));
                label.GetComponent<Text>().raycastTarget = false;

                // 값 (우측) — 경험치/체력은 별도 캐싱
                bool isGaugeRow = (i == 1 || i == 2);
                RectTransform value = CreateText(
                    panel, $"Row{i}_Value", "-", 16, ColorValue,
                    TextAnchor.MiddleRight, new Vector2(-(PanelPad + 8f), -(y + RowHeight * 0.5f)),
                    new Vector2(PanelWidth * 0.5f, RowHeight),
                    new Vector2(1f, 1f), new Vector2(1f, 0.5f));
                value.GetComponent<Text>().raycastTarget = false;
                _valueTexts[i] = value.GetComponent<Text>();
                if (i == 1) _expValueText = _valueTexts[i];
                if (i == 2) _hpValueText = _valueTexts[i];

                y += RowHeight;

                // --- 게이지 (경험치/체력 행 바로 아래) ---
                if (isGaugeRow)
                {
                    RectTransform gaugeBg = CreateImage(
                        panel, $"Gauge{i}_Bg", _whiteSprite, ColorGaugeBg,
                        new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(PanelPad + 8f, -(y + GaugeHeight * 0.5f)),
                        new Vector2(GaugeWidth - 16f, GaugeHeight));
                    gaugeBg.pivot = new Vector2(0f, 0.5f);
                    gaugeBg.GetComponent<Image>().raycastTarget = false;

                    // fill: 좌측 고정 앵커 → sizeDelta.x로 비율 표현
                    var fillGO = new GameObject($"Gauge{i}_Fill");
                    fillGO.transform.SetParent(gaugeBg, false);
                    var fillRT = fillGO.AddComponent<RectTransform>();
                    fillRT.anchorMin = new Vector2(0f, 0f);
                    fillRT.anchorMax = new Vector2(0f, 1f);
                    fillRT.pivot = new Vector2(0f, 0.5f);
                    fillRT.anchoredPosition = Vector2.zero;
                    fillRT.sizeDelta = new Vector2(0f, 0f);

                    var fillImg = fillGO.AddComponent<Image>();
                    fillImg.sprite = _whiteSprite;
                    fillImg.color = (i == 1) ? ColorGaugeExp : ColorGaugeHP;
                    fillImg.raycastTarget = false;

                    if (i == 1) _expGaugeFill = fillRT;
                    else _hpGaugeFill = fillRT;

                    y += GaugeHeight + GaugeGap;
                }
            }

            // --- 푸터 (조작 안내) ---
            RectTransform footer = CreateText(
                panel, "Footer", $"[{_toggleKey}] 열기/닫기   [ESC] 닫기", 14, ColorFooter,
                TextAnchor.MiddleCenter, new Vector2(PanelWidth * 0.5f, -(y + FooterHeight * 0.5f)),
                new Vector2(PanelWidth - PanelPad * 2f, FooterHeight),
                new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
            footer.GetComponent<Text>().raycastTarget = false;
        }

        // ===== 빌드 헬퍼 =====

        private RectTransform CreateImage(
            Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private RectTransform CreateText(
            Transform parent, string name, string text, int fontSize, Color color,
            TextAnchor alignment, Vector2 anchoredPos, Vector2 size,
            Vector2 anchorPoint = default, Vector2 pivot = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchorPoint;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.font = _font;
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return rt;
        }

        /// <summary>단색 흰색 스프라이트 (게이지/행 배경용).</summary>
        private static Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[4 * 4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        }

        /// <summary>9-Slice용 라운드 사각 스프라이트 (패널 다크 배경용 — HotbarUI와 동일 방식).</summary>
        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float radiusF = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dist = DistanceToRoundedRect(px, py, size, radiusF);
                    bool inside = dist <= 0f;
                    // 코너 경계 1px 안티앨리어싱
                    float alpha = inside ? 255f : 0f;
                    if (!inside && radiusF > 0f && dist < 1f)
                        alpha = (1f - dist) * 255f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            int b = radius; // 9-Slice 보더 = 코너 반경
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                size, 0u, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>라운드 사각형 "바깥"까지의 거리(내부/경계는 0 이하).</summary>
        private static float DistanceToRoundedRect(float px, float py, float size, float radius)
        {
            float maxX = size - radius;
            float cx = Mathf.Clamp(px, radius, maxX);
            float cy = Mathf.Clamp(py, radius, maxX);
            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - radius;
        }

        /// <summary>빌트인 폰트 로드 (Unity 2022+: LegacyRuntime.ttf, 구버전: Arial.ttf).</summary>
        private static Font LoadBuiltinFont()
        {
            try
            {
                var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (f != null) return f;
            }
            catch { /* 구버전 Unity 폴백 */ }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                Debug.LogWarning("[StatusWindowUI] 빌트인 폰트 로드 실패 — 텍스트 미표시");
                return null;
            }
        }
    }
}
