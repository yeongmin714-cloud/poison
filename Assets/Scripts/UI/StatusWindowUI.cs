using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 캐릭터 정보 창 v2 (2026-09-09, 계획서 P4) — P키로 열기/닫기, ESC로 닫기.
    ///
    /// [레이아웃 — 유저 제공 예시(동양풍 MMORPG 캐릭터정보 창) 재현]
    /// - 인벤토리 창과 동일한 화면 중앙 위치 (1180x900 @1080p 기준).
    /// - 좌측: 플레이어 3D 뷰포트(RenderTexture+전용 카메라, 드래그 회전) 둘레로 ㄷ자 장비슬롯 6개.
    /// - 우측: 주스탯(힘/민첩/지능/체력 + [+] 분배 버튼, 레벨업당 5포인트) → 전투 스탯 → 캐릭터 정보.
    /// - 장비 착용분은 EquipmentStatBonus를 통해 파생 스탯에 합산되며, 보너스 내역도 표시.
    ///
    /// [구현 방식]
    /// - HotbarUI와 동일한 "코드 생성 UI" (프리팹/씬 편집 불필요, 씬 의존 0).
    /// - UI 표준: legacy UnityEngine.UI.Text (TMP 혼합 금지), 한글 라벨.
    /// - 3D 뷰포트는 창이 열려 있을 때만 활성(저사양 배려): RT 256x320 + 전용 카메라(레이어 31만 렌더),
    ///   플레이어 모델 클론(스크립트 전면 제거, y=-2000 격리)을 렌더. 닫으면 즉시 파괴.
    ///
    /// [소유권 주의]
    /// - PlayerStats / PlayerHealth / DrugEffectSystem / EquipmentManager는 타 소유 — 공개 API 소비만.
    /// </summary>
    public class StatusWindowUI : MonoBehaviour
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static StatusWindowUI _instance;
        public static StatusWindowUI Instance => _instance;

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
        [SerializeField] private KeyCode _toggleKey = KeyCode.P; // 은신(C)과 충돌 회피 → P
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

        // ===== 레이아웃 상수 (1080p 기준) =====
        private const float WinW = 1180f;
        private const float WinH = 900f;
        private const float TitleH = 48f;
        private const float SlotSize = 92f;
        private const float StatRowH = 30f;

        // ===== 테마 색상 (예시 톤: 다크 프레임 + 버건디 포인트 + 하늘색 수치) =====
        private static readonly Color ColorPanelBg  = new Color(0.07f, 0.06f, 0.07f, 0.92f);
        private static readonly Color ColorTitleBar = new Color(0.32f, 0.10f, 0.12f, 1f);   // 버건디
        private static readonly Color ColorZoneBg   = new Color(0.13f, 0.11f, 0.12f, 0.85f);
        private static readonly Color ColorSlotBg   = new Color(0.20f, 0.16f, 0.13f, 0.95f);
        private static readonly Color ColorLabel    = new Color(0.92f, 0.88f, 0.80f, 1f);   // 항목명 백색
        private static readonly Color ColorValue    = new Color(0.55f, 0.90f, 1f, 1f);      // 수치 하늘색
        private static readonly Color ColorDim      = new Color(0.55f, 0.52f, 0.48f, 1f);
        private static readonly Color ColorGold     = new Color(1f, 0.85f, 0.30f, 1f);
        private static readonly Color ColorPlusOn   = new Color(0.25f, 0.60f, 0.30f, 1f);
        private static readonly Color ColorPlusOff  = new Color(0.28f, 0.28f, 0.28f, 0.8f);
        private static readonly Color ColorGaugeBg  = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ColorGaugeExp = new Color(0.35f, 0.65f, 1f, 0.95f);
        private static readonly Color ColorGaugeHP  = new Color(0.35f, 0.85f, 0.40f, 0.95f);
        private static readonly Color ColorViewport = new Color(0.08f, 0.08f, 0.11f, 1f);

        // ===== 런타임 상태 =====
        private Canvas _canvas;
        private Font _font;
        private Sprite _whiteSprite;
        private Sprite _roundedSprite;

        private GameObject _panelRoot;
        private Text _expValueText, _hpValueText, _pendingText;
        private readonly Text[] _statRowTexts = new Text[8];   // 전투/정보 섹션 값
        private readonly Text[] _statEquipBonusTexts = new Text[8];
        private readonly Text[] _allocTexts = new Text[4];     // 힘/민첩/지능/체력 할당치
        private readonly Button[] _allocButtons = new Button[4];
        private readonly Text[] _equipSlotTexts = new Text[6]; // 장비슬롯 아이템명
        private Text _bonusListText;
        private Text _addictionText;
        private RectTransform _expGaugeFill, _hpGaugeFill;
        private RawImage _viewportImage;
        private RectTransform _viewportRect;

        private static readonly PlayerStats.StatKind[] _statKinds =
            { PlayerStats.StatKind.Str, PlayerStats.StatKind.Agi, PlayerStats.StatKind.Int, PlayerStats.StatKind.Vit };
        private static readonly string[] _statKindNames = { "힘", "민첩", "지능", "체력" };
        private static readonly string[] _statKindEffects =
            { "공격 +2/pt", "치명+0.5% 속도+0.05 /pt", "연금·요리 +0.5% /pt", "최대체력 +10 /pt" };

        // 3D 프리뷰
        private GameObject _previewClone;
        private GameObject _previewCamGO;
        private RenderTexture _previewRT;
        private float _previewYaw;
        private const int PreviewLayer = 31;

        // 레벨업 팝업
        private GameObject _levelUpPopup;
        private Coroutine _levelUpFadeCo;

        private bool _isOpen;
        private float _refreshTimer;
        private PlayerStats _subscribedStats;
        private EquipmentManager _subscribedEquip;

        // ===== 생명주기 =====
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            try
            {
                BuildCanvas();
                BuildPanel();
                Debug.Log("[StatusWindowUI] 초기화 완료 — P키로 토글");
            }
            catch (System.Exception e)
            {
                // 원인 진단용: 예외가 나도 컴포넌트는 살려서 재시도 경로 확보
                Debug.LogError($"[StatusWindowUI] 초기화 예외: {e}");
                _panelRoot = null;
            }
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_subscribedStats != null)
            {
                _subscribedStats.OnLevelChanged -= OnStatsLevelChanged;
                _subscribedStats = null;
            }
            UnsubscribeEquipment();
            TeardownPreview();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey)) Toggle();
            if (_isOpen && Input.GetKeyDown(_closeKey)) Close();

            // 씬 재로드 대비 재구독 (PlayerStats/EquipmentManager 모두 씬 소속일 수 있음)
            EnsureLevelSubscription();
            EnsureEquipmentSubscription();

            // 3D 뷰포트 드래그 회전 (EventSystem 불필요 — 마우스 오버 폴링)
            if (_isOpen) HandleViewportDrag();

            if (!_isOpen) return;
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < 0.25f) return;
            _refreshTimer = 0f;
            RefreshDisplay();
        }

        // =====================================================================
        //  열기/닫기/토글
        // =====================================================================

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            // 자가 복구: 초기화 중 예외로 패널이 없으면 재빌드
            if (_panelRoot == null)
            {
                Debug.LogWarning("[StatusWindowUI] 패널 미생성 — 재빌드 시도");
                BuildPanel();
            }
            _refreshTimer = 1f;
            if (_panelRoot != null) _panelRoot.SetActive(true);
            EnsureLevelSubscription();
            EnsureEquipmentSubscription();
            SetupPreview();
            RefreshDisplay();
            Debug.Log("[StatusWindowUI] 캐릭터 정보 창 열림 (키: P)");
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);
            TeardownPreview(); // 저사양: 창 닫히면 프리뷰 카메라/클론 즉시 해제
            Debug.Log("[StatusWindowUI] 캐릭터 정보 창 닫힘");
        }

        public void Toggle() { if (_isOpen) Close(); else Open(); }
        public bool IsOpen => _isOpen;

        // =====================================================================
        //  구독 관리 (씬 전환 위생)
        // =====================================================================

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

        private void EnsureEquipmentSubscription()
        {
            var em = EquipmentManager.Instance;
            if (ReferenceEquals(_subscribedEquip, em)) return;
            if (_subscribedEquip != null)
                _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
            _subscribedEquip = em;
            if (_subscribedEquip != null)
                _subscribedEquip.OnEquipmentChanged += OnEquipmentChanged;
        }

        private void OnStatsLevelChanged(int newLevel, int oldLevel)
        {
            if (_isOpen) RefreshDisplay();
            ShowLevelUpPopup(newLevel);
        }

        private void OnEquipmentChanged(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            if (_isOpen) RefreshDisplay();
        }

        private void UnsubscribeEquipment()
        {
            if (_subscribedEquip != null)
            {
                _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribedEquip = null;
            }
        }

        // =====================================================================
        //  데이터 갱신
        // =====================================================================

        private void RefreshDisplay()
        {
            EnsureLevelSubscription();
            EnsureEquipmentSubscription();

            PlayerStats stats = PlayerStats.Instance;
            if (stats == null)
            {
                if (_expValueText != null) _expValueText.text = "-";
                if (_hpValueText != null) _hpValueText.text = "-";
                return;
            }

            // --- 경험치 ---
            int level = stats.Level;
            if (level >= PlayerStats.MaxLevel)
            {
                if (_expValueText != null) _expValueText.text = "MAX";
                if (_expGaugeFill != null) _expGaugeFill.sizeDelta = new Vector2(ExpGaugeW, 0f);
            }
            else
            {
                int curExp = stats.CurrentEXP;
                int nextExp = stats.GetExpForLevel(level + 1);
                int needExp = Mathf.Max(0, nextExp - curExp);
                if (_expValueText != null)
                    _expValueText.text = $"{curExp:N0} / {nextExp:N0}  (남은 {needExp:N0})";
                int prevExp = stats.GetExpForLevel(level);
                int span = Mathf.Max(1, nextExp - prevExp);
                float ratio = Mathf.Clamp01((curExp - prevExp) / (float)span);
                if (_expGaugeFill != null) _expGaugeFill.sizeDelta = new Vector2(ExpGaugeW * ratio, 0f);
            }

            // --- 체력 ---
            PlayerHealth health = PlayerHealth.Instance;
            float maxHP = health != null ? health.MaxHP : stats.HPBase;
            float curHP = health != null ? health.CurrentHP : maxHP;
            if (_hpValueText != null) _hpValueText.text = $"{curHP:F0} / {stats.HPBase:F0}";
            if (_hpGaugeFill != null)
                _hpGaugeFill.sizeDelta = new Vector2(ExpGaugeW * (stats.HPBase > 0 ? Mathf.Clamp01(curHP / stats.HPBase) : 0f), 0f);

            // --- 주스탯 + [+] 버튼 ---
            bool canAllocate = stats.PendingStatPoints > 0;
            if (_pendingText != null)
            {
                _pendingText.text = $"남은 포인트: {stats.PendingStatPoints}";
                _pendingText.color = stats.PendingStatPoints > 0 ? ColorGold : ColorDim;
            }
            for (int i = 0; i < 4; i++)
            {
                if (_allocTexts[i] != null)
                {
                    int alloc = stats.GetAllocatedStat(_statKinds[i]);
                    _allocTexts[i].text = $"{_statKindNames[i]} {alloc}";
                }
                if (_allocButtons[i] != null)
                    _allocButtons[i].interactable = canAllocate;
            }

            // --- 전투/정보 행 ---
            SetRow(0, $"{stats.FinalAttackDamage:F1}");
            SetRow(1, $"{stats.FinalDefense:F1}");
            SetRow(2, $"{stats.FinalCritChance * 100f:F1}%");
            SetRow(3, $"{stats.FinalMoveSpeed:F1}");
            SetRow(4, $"+{stats.FinalAlchemyBonus * 100f:F1}%");
            SetRow(5, $"+{stats.FinalCookingBonus * 100f:F1}%");
            SetRow(6, $"+{stats.SpeechAffinityBonus}");
            SetRow(7, $"{stats.Gold:N0} G");

            SetBonusNote(0, $"힘 {stats.AllocatedStr} · 공격 보정 +{stats.AllocatedStr * 2f:F0}");
            SetBonusNote(1, $"민첩 {stats.AllocatedAgi} — 치명 +{stats.AllocatedAgi * 0.5f:F1}% / 속도 +{stats.AllocatedAgi * 0.05f:F2}");
            SetBonusNote(2, $"지능 {stats.AllocatedInt} — 연금·요리 +{stats.AllocatedInt * 0.5f:F1}%");
            SetBonusNote(3, $"체력 {stats.AllocatedVit} — 최대HP +{stats.AllocatedVit * 10}");
            SetBonusNote(4, $"장비 공격 +{EquipmentStatBonusApplier.GetAttackBonus():F0} / 방어 +{EquipmentStatBonusApplier.GetDefenseBonus():F0}");
            SetBonusNote(5, $"장비 치명 +{EquipmentStatBonusApplier.GetCritBonus() * 100f:F1}% / 속도 +{EquipmentStatBonusApplier.GetSpeedBonus():F1}");
            SetBonusNote(6, $"레벨 보정 — 공격 +{level * 0.5f:F1}, 방어 +{level * 0.2f:F1}, 치명 +{level * 0.5f:F1}%");
            SetBonusNote(7, $"전투 보너스 +{stats.CombatDamageBonus * 100f:F0}% / 화술 +{stats.SpeechAffinityBonus}");

            // --- 장비슬롯 아이템명 ---
            var em = EquipmentManager.Instance;
            for (int i = 0; i < 6; i++)
            {
                if (_equipSlotTexts[i] == null) continue;
                string itemId = null;
                if (em != null)
                {
                    var data = em.GetSlotData((EquipmentManager.EquipmentSlot)i);
                    if (data != null) itemId = data.itemId;
                }
                _equipSlotTexts[i].text = string.IsNullOrEmpty(itemId) ? "—" : EquipmentStatBonusApplier.DisplayName(itemId);
            }

            // --- 장비 보너스 내역 ---
            if (_bonusListText != null)
            {
                var labels = EquipmentStatBonusApplier.GetActiveBonusLabels();
                _bonusListText.text = labels.Count > 0 ? string.Join("\n", labels) : "착용 장비 보너스 없음";
            }

            // --- 중독 (정보 섹션) ---
            if (_addictionText != null)
                _addictionText.text = $"중독: {DrugEffectSystem.DrugAddictionLevel:F0}% ({DrugEffectSystem.GetAddictionLabel()})";
        }

        private void SetRow(int i, string value)
        {
            if (i < 0 || i >= _statRowTexts.Length) return;
            if (_statRowTexts[i] != null) _statRowTexts[i].text = value;
        }

        private void SetBonusNote(int i, string note)
        {
            if (i < 0 || i >= _statEquipBonusTexts.Length) return;
            if (_statEquipBonusTexts[i] != null) _statEquipBonusTexts[i].text = note;
        }

        // =====================================================================
        //  3D 프리뷰 (뷰포트)
        // =====================================================================

        private void SetupPreview()
        {
            TeardownPreview();

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[StatusWindowUI] Player 태그 오브젝트 없음 — 뷰포트 빈 상태");
                return;
            }

            // 클론 생성 → 스크립트/충돌/오디오 전면 제거 (렌더 전용), 전용 레이어 배치
            _previewClone = Instantiate(player);
            _previewClone.name = "StatusPreviewClone";
            var mbs = _previewClone.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in mbs) Destroy(mb);
            foreach (var col in _previewClone.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in _previewClone.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (var ps in _previewClone.GetComponentsInChildren<ParticleSystem>(true)) { var r = ps.GetComponent<ParticleSystemRenderer>(); if (r != null) r.enabled = false; }
            foreach (var audio in _previewClone.GetComponentsInChildren<AudioSource>(true)) Destroy(audio);
            foreach (var canvas in _previewClone.GetComponentsInChildren<Canvas>(true)) Destroy(canvas);
            SetLayerRecursive(_previewClone, PreviewLayer);
            _previewClone.transform.position = new Vector3(0f, -2000f, 0f); // 메인 카메라 시야 밖
            _previewClone.transform.rotation = Quaternion.Euler(0f, 180f + _previewYaw, 0f);

            // RT + 전용 카메라 (레이어 31만 렌더)
            _previewRT = new RenderTexture(256, 320, 16);
            _previewCamGO = new GameObject("StatusPreviewCam");
            var cam = _previewCamGO.AddComponent<Camera>();
            cam.cullingMask = 1 << PreviewLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColorViewport;
            cam.fieldOfView = 32f;
            cam.targetTexture = _previewRT;
            _previewCamGO.transform.position = new Vector3(0f, -1997.2f, -4.2f);
            _previewCamGO.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

            if (_viewportImage != null) _viewportImage.texture = _previewRT;
        }

        private void TeardownPreview()
        {
            if (_previewClone != null) { Destroy(_previewClone); _previewClone = null; }
            if (_previewCamGO != null) { Destroy(_previewCamGO); _previewCamGO = null; }
            if (_previewRT != null) { _previewRT.Release(); Destroy(_previewRT); _previewRT = null; }
            if (_viewportImage != null) _viewportImage.texture = null;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform)
                SetLayerRecursive(t.gameObject, layer);
        }

        private void HandleViewportDrag()
        {
            if (_previewClone == null || _viewportRect == null) return;
            if (!Input.GetMouseButton(0)) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_viewportRect, Input.mousePosition, null))
                return;
            float dx = Input.GetAxis("Mouse X");
            if (Mathf.Abs(dx) > 0.001f)
            {
                _previewYaw += dx * 3f;
                _previewClone.transform.rotation = Quaternion.Euler(0f, 180f + _previewYaw, 0f);
            }
        }

        // =====================================================================
        //  레벨업 팝업 (v1 계승)
        // =====================================================================

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
            if (_levelUpPopup != null) Destroy(_levelUpPopup);
            if (_levelUpFadeCo != null) StopCoroutine(_levelUpFadeCo);

            var rt = CreateText(_canvas.transform, "LevelUpPopup",
                $"LEVEL UP!  Lv.{newLevel}   (+{PlayerStats.StatPointsPerLevel} 포인트)", 44, ColorGold,
                TextAnchor.MiddleCenter, new Vector2(0f, -110f), new Vector2(800f, 70f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

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
            float elapsed = 0f;
            Color baseColor = text != null ? text.color : ColorGold;
            while (elapsed < 1.4f) { elapsed += Time.unscaledDeltaTime; yield return null; }
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
        //  UI 빌드
        // =====================================================================

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("StatusCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;

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
            // 인벤토리 창과 동일한 화면 중앙 (InventoryWindow: 1180x1040 중앙 → 본 창 1180x900 중앙)
            RectTransform panel = CreateImage(
                _canvas.transform, "StatusPanel", _roundedSprite, ColorPanelBg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(WinW, WinH), new Vector2(0.5f, 0.5f));
            panel.pivot = new Vector2(0.5f, 0.5f);
            var panelImg = panel.GetComponent<Image>();
            panelImg.type = Image.Type.Sliced;

            _panelRoot = panel.gameObject;
            _panelRoot.SetActive(false);

            // [앵커 규약] 패널 자식: 좌상단 앵커(0,1) + pivot(0,1), pos=(x, -y) 위→아래 배치.
            float y = 0f;

            // --- 타이틀 바 (버건디) ---
            CreateImage(panel, "TitleBar", _whiteSprite, ColorTitleBar,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(WinW, TitleH), new Vector2(0f, 1f));
            CreateText(panel, "TitleText", "  캐릭터 정보", 26, ColorLabel,
                TextAnchor.MiddleLeft, new Vector2(20f, -TitleH * 0.5f), new Vector2(400f, 36f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));

            var closeBtn = CreateButton(panel, "CloseBtn", "X", 20, ColorLabel, ColorPlusOff,
                new Vector2(WinW - 54f, 0f), new Vector2(44f, 36f), new Vector2(0f, 1f));
            closeBtn.onClick.AddListener(Close);

            y = TitleH + 10f;

            // --- 좌측 존 배경 (장비 + 뷰포트) ---
            CreateImage(panel, "LeftZone", _whiteSprite, new Color(1f, 1f, 1f, 0.03f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -y), new Vector2(442f, 640f), new Vector2(0f, 1f));

            // --- 3D 뷰포트 (FIX: Image가 있는 오브젝트에 RawImage AddComponent → 유니티가 거부해 null 반환 = NRE 원인.
            //     Graphic 2중 구성 금지 — RawImage 전용 오브젝트로 직접 생성) ---
            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RawImage));
            viewportGO.transform.SetParent(panel, false);
            var viewportRt = viewportGO.GetComponent<RectTransform>();
            viewportRt.anchorMin = new Vector2(0f, 1f);
            viewportRt.anchorMax = new Vector2(0f, 1f);
            viewportRt.pivot = new Vector2(0f, 1f);
            viewportRt.anchoredPosition = new Vector2(122f, -(y + 20f));
            viewportRt.sizeDelta = new Vector2(226f, 300f);
            _viewportImage = viewportGO.GetComponent<RawImage>();
            _viewportImage.texture = null;
            _viewportImage.color = ColorViewport;
            _viewportImage.raycastTarget = true; // 드래그 대상
            _viewportRect = viewportRt;

            CreateText(panel, "ViewportHint", "드래그: 회전", 14, ColorDim,
                TextAnchor.MiddleCenter, new Vector2(122f, -(y + 330f)), new Vector2(226f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            // --- 장비슬롯 ㄷ자 배치 (좌3: 투구/상의/무기, 우3: 장갑/신발/등) ---
            BuildEquipSlot(0, EquipmentManager.EquipmentSlot.Helmet, "투구", 24f, y + 20f);
            BuildEquipSlot(1, EquipmentManager.EquipmentSlot.Armor, "상의", 24f, y + 130f);
            BuildEquipSlot(2, EquipmentManager.EquipmentSlot.Weapon, "무기", 24f, y + 240f);
            BuildEquipSlot(3, EquipmentManager.EquipmentSlot.Gloves, "장갑", 354f, y + 20f);
            BuildEquipSlot(4, EquipmentManager.EquipmentSlot.Shoes, "신발", 354f, y + 130f);
            BuildEquipSlot(4, EquipmentManager.EquipmentSlot.Back, "등", 354f, y + 240f);

            // --- 장비 보너스 내역 (좌측 존 하단) ---
            CreateText(panel, "BonusHeader", "장비 보너스", 16, ColorGold,
                TextAnchor.MiddleLeft, new Vector2(24f, -(y + 380f)), new Vector2(420f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _bonusListText = CreateText(panel, "BonusList", "", 14, ColorValue,
                TextAnchor.UpperLeft, new Vector2(24f, -(y + 406f)), new Vector2(420f, 220f), new Vector2(0f, 1f), new Vector2(0f, 1f)).GetComponent<Text>();

            // --- 우측 존: 주스탯 + [+] 분배 ---
            float rx = 470f;
            float pendingW = 300f;
            _pendingText = CreateText(panel, "PendingText", "남은 포인트: 0", 18, ColorDim,
                TextAnchor.MiddleRight, new Vector2(rx + 660f - pendingW, -(y + 4f)), new Vector2(pendingW, 26f), new Vector2(0f, 1f), new Vector2(1f, 0.5f)).GetComponent<Text>();

            float sy = y + 36f;
            for (int i = 0; i < 4; i++)
            {
                float rowY = sy + i * 44f;
                var kind = _statKinds[i];

                CreateText(panel, $"Stat{i}_Name", _statKindNames[i], 19, ColorLabel,
                    TextAnchor.MiddleLeft, new Vector2(rx, -rowY), new Vector2(64f, 28f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));

                _allocTexts[i] = CreateText(panel, $"Stat{i}_Alloc", "5", 21, ColorValue,
                    TextAnchor.MiddleLeft, new Vector2(rx + 70f, -rowY), new Vector2(60f, 28f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).GetComponent<Text>();

                CreateText(panel, $"Stat{i}_Desc", _statKindEffects[i], 12, ColorDim,
                    TextAnchor.MiddleLeft, new Vector2(rx + 136f, -rowY), new Vector2(230f, 28f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));

                var btn = CreateButton(panel, $"Stat{i}_Plus", "+", 20, Color.white, ColorPlusOn,
                    new Vector2(rx + 386f, -rowY), new Vector2(40f, 30f), new Vector2(0f, 1f));
                var capturedKind = kind;
                btn.onClick.AddListener(() =>
                {
                    var st = PlayerStats.Instance;
                    if (st != null && st.AllocateStat(capturedKind)) RefreshDisplay();
                });
                _allocButtons[i] = btn;
            }

            // --- 전투 스탯 8행 (값 + 계산 근거 노트) ---
            float ty = sy + 190f;
            string[] rowNames = { "공격력", "방어력", "치명타", "이동속도", "연금술 성공", "요리 성공", "화술", "골드" };
            for (int i = 0; i < 8; i++)
            {
                float rowY = ty + i * StatRowH;
                CreateText(panel, $"Row{i}_Name", rowNames[i], 16, ColorLabel,
                    TextAnchor.MiddleLeft, new Vector2(rx, -rowY), new Vector2(110f, 24f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
                _statRowTexts[i] = CreateText(panel, $"Row{i}_Value", "-", 16, ColorValue,
                    TextAnchor.MiddleRight, new Vector2(rx + 250f, -rowY), new Vector2(120f, 24f), new Vector2(0f, 1f), new Vector2(1f, 0.5f)).GetComponent<Text>();
                _statEquipBonusTexts[i] = CreateText(panel, $"Row{i}_Note", "", 12, ColorDim,
                    TextAnchor.MiddleLeft, new Vector2(rx + 380f, -rowY), new Vector2(280f, 24f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).GetComponent<Text>();
            }

            // --- 경험치/체력 게이지 + 중독 (우측 하단) ---
            float gy = ty + 8 * StatRowH + 14f;
            CreateText(panel, "HPLabel", "체력", 16, ColorLabel,
                TextAnchor.MiddleLeft, new Vector2(rx, -gy), new Vector2(110f, 22f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            _hpValueText = CreateText(panel, "HPValue", "-", 15, ColorValue,
                TextAnchor.MiddleRight, new Vector2(rx + 250f, -gy), new Vector2(120f, 22f), new Vector2(0f, 1f), new Vector2(1f, 0.5f)).GetComponent<Text>();
            _hpGaugeFill = CreateGauge(panel, "HPGauge", ColorGaugeHP, rx, gy + 24f);

            CreateText(panel, "ExpLabel", "경험치", 16, ColorLabel,
                TextAnchor.MiddleLeft, new Vector2(rx, -(gy + 36f)), new Vector2(110f, 22f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            _expValueText = CreateText(panel, "ExpValue", "-", 15, ColorValue,
                TextAnchor.MiddleRight, new Vector2(rx + 250f, -(gy + 36f)), new Vector2(220f, 22f), new Vector2(0f, 1f), new Vector2(1f, 0.5f)).GetComponent<Text>();
            _expGaugeFill = CreateGauge(panel, "ExpGauge", ColorGaugeExp, rx, gy + 36f + 24f);

            _addictionText = CreateText(panel, "AddictionText",
                "", 14, ColorDim,
                TextAnchor.MiddleLeft, new Vector2(rx, -(gy + 100f)), new Vector2(360f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f)).GetComponent<Text>();
        }

        /// <summary>장비슬롯 1개 (박스 + 슬롯명 + 아이템명).</summary>
        private void BuildEquipSlot(int index, EquipmentManager.EquipmentSlot slot, string label, float x, float topY)
        {
            CreateImage(_panelRoot.transform, $"Equip_{slot}", _roundedSprite, ColorSlotBg,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -topY), new Vector2(SlotSize, SlotSize), new Vector2(0f, 1f));
            CreateText(_panelRoot.transform, $"Equip_{slot}_Label", label, 14, ColorDim,
                TextAnchor.MiddleCenter, new Vector2(x, -(topY + SlotSize + 2f)), new Vector2(SlotSize, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _equipSlotTexts[(int)slot] = CreateText(_panelRoot.transform, $"Equip_{slot}_Item", "—", 13, ColorValue,
                TextAnchor.MiddleCenter, new Vector2(x - 6f, -(topY + SlotSize + 20f)), new Vector2(SlotSize + 12f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f)).GetComponent<Text>();
        }

        /// <summary>게이지(배경+fill). fill의 sizeDelta.x = 폭*비율로 갱신.</summary>
        private RectTransform CreateGauge(Transform parent, string name, Color fillColor, float x, float topY)
        {
            const float GaugeW = 380f, GaugeH = 10f;
            CreateImage(parent, name + "_Bg", _whiteSprite, ColorGaugeBg,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -topY), new Vector2(GaugeW, GaugeH), new Vector2(0f, 1f));
            var fill = CreateImage(parent, name + "_Fill", _whiteSprite, fillColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + 2f, -(topY + 2f)), new Vector2(0f, GaugeH - 4f), new Vector2(0f, 1f));
            return fill;
        }

        private const float ExpGaugeW = 396f; // 게이지 배경 380 - 좌우 인셋 4

        // =====================================================================
        //  버튼 헬퍼
        // =====================================================================

        private Button CreateButton(Transform parent, string name, string label, int fontSize,
            Color textColor, Color bgColor, Vector2 pos, Vector2 size, Vector2 pivotTopLeft)
        {
            var rt = CreateImage(parent, name, _roundedSprite, bgColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size, pivotTopLeft);
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.disabledColor = ColorPlusOff;
            btn.colors = colors;

            var textRt = CreateText(rt, name + "_Text", label, fontSize, label != "X" ? Color.white : ColorLabel,
                TextAnchor.MiddleCenter, new Vector2(size.x * 0.5f, -size.y * 0.5f), size, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            textRt.GetComponent<Text>().raycastTarget = false;
            return btn;
        }

        // =====================================================================
        //  저수준 헬퍼 (HotbarUI 선례)
        // =====================================================================

        private RectTransform CreateImage(Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private RectTransform CreateText(Transform parent, string name, string content, int fontSize, Color color,
            TextAnchor alignment, Vector2 anchoredPos, Vector2 size, Vector2 pivotMin, Vector2 pivotMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            // 점앵커 강제: anchorMin == anchorMax (스트레치 모드 방지 — sizeDelta가 앵커 rect에 가산되는 사고 차단)
            rt.anchorMin = pivotMin;
            rt.anchorMax = pivotMin;
            rt.pivot = pivotMin;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.font = _font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return rt;
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var px = new Color32[4];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateRoundedSprite(int size, int corner)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float r = corner;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 라운드 코너: 4 코너 원 판정
                    float dx = Mathf.Min(x, size - 1 - x);
                    float dy = Mathf.Min(y, size - 1 - y);
                    bool inside = (dx >= r || dy >= r) || (dx * dx + dy * dy) <= r * r;
                    px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Font LoadBuiltinFont()
        {
            try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { /* 폴백 */ }
            try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { /* 최종 폴백: 씬 내 기존 Text의 폰트 */ }
            var anyText = Object.FindFirstObjectByType<Text>();
            return anyText != null ? anyText.font : null;
        }
    }
}
