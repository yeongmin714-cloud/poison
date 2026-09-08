using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 퀵슬롯 핫바 UI (Phase M1) — 8슬롯, 1~8키, 장비 장착 토글.
    ///
    /// [스펙 (예시 스크린샷 분석)]
    /// - 1080p 하단 중앙, 다크 반투명 배경 + 라운드 코너
    /// - 슬롯 96px, 하단에 숫자(1~8) 소형 박스
    /// - 선택(장착 중) 슬롯 = 흰 두꺼운 테두리 하이라이트
    /// - 우상단 스택 수량 소형 숫자 (소비 아이템)
    ///
    /// [구현 방식]
    /// - InventoryWindow와 같은 "코드 생성 UI" 방식 (프리팹 불필요).
    ///   InventoryWindow는 IMGUI이지만, 핫바는 상시 노출 + 외곽 프레임 표현을 위해
    ///   uGUI(Canvas 하위, 런타임 코드 생성)로 구성. Canvas는 씬 편집 없이 부트스트랩에서 자동 생성.
    /// - 라운드 코너: 9-Slice용 라운드 스프라이트를 코드로 프로시저럴 생성 (에셋 의존성 0).
    ///
    /// [v1 슬롯 구성 (고정)]
    ///   [0] 검  steel_sword  → WeaponEquipManager.Equip("steel", player, Sword) / 재입력 시 Unequip
    ///   [1] 활  crystal_bow  → WeaponEquipManager.Equip("crystal", player, Bow) / 재입력 시 Unequip (M4)
    ///   [2] 창  wood_spear   → WeaponEquipManager.Equip("wood", player, Spear) / 재입력 시 Unequip (M4)
    ///   [3] 폭탄 bomb(소비)   → 인벤 수량 확인 후 투척 모드 진입 — PlayerWeaponModeBridge.ThrowSelected (M4)
    ///   [4~7] 빈 슬롯 (확장 예약)
    ///
    /// [소유권 주의] WeaponEquipManager는 타 에이전트 소유 — 본 파일은 호출만 수행(수정 금지).
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static HotbarUI _instance;

        /// <summary>
        /// 씬 편집 없이 상시 핫바를 띄우기 위한 셀프 부트스트랩.
        /// 선례: UI/DynamicEventUI.cs의 RuntimeInitializeOnLoadMethod 패턴.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var existing = Object.FindFirstObjectByType<HotbarUI>();
            if (existing != null) { _instance = existing; return; }

            var go = new GameObject("HotbarUI");
            _instance = go.AddComponent<HotbarUI>();
        }

        // ===== 슬롯 정의 (v1 고정 8슬롯) =====
        private enum SlotKind { Weapon, Consumable, Empty }

        private class SlotDef
        {
            public SlotKind kind;
            public string specId;    // 스펙상 아이템 id (예: steel_sword)
            public string label;     // 슬롯 중앙 표시 텍스트 (v1: 아이콘 미구현 → 글자 대체)
            public string equipId;   // WeaponEquipManager.Equip에 넘길 id (검만: {id}_sword GLB)
            public WeaponType weaponType; // M4: 장착 타입 — 매니저가 타입별 GLB 접미사(_sword/_bow/_spear) 결정
        }

        private static readonly SlotDef[] SlotDefs = new SlotDef[8]
        {
            new SlotDef{ kind = SlotKind.Weapon,     specId = "steel_sword", label = "검", equipId = "steel",   weaponType = WeaponType.Sword },
            new SlotDef{ kind = SlotKind.Weapon,     specId = "crystal_bow", label = "활", equipId = "crystal", weaponType = WeaponType.Bow   }, // M4: {crystal}_bow GLB
            new SlotDef{ kind = SlotKind.Weapon,     specId = "wood_spear",  label = "창", equipId = "wood",    weaponType = WeaponType.Spear }, // M4: {wood}_spear GLB
            new SlotDef{ kind = SlotKind.Consumable, specId = "bomb",        label = "폭", equipId = null     }, // M4: 투척 모드 — PlayerWeaponModeBridge 연결
            new SlotDef{ kind = SlotKind.Empty }, // [4] 확장 예약
            new SlotDef{ kind = SlotKind.Empty }, // [5] 확장 예약
            new SlotDef{ kind = SlotKind.Empty }, // [6] 확장 예약
            new SlotDef{ kind = SlotKind.Empty }, // [7] 확장 예약
        };

        // ===== 레이아웃 상수 (1080p 디자인 기준 — CanvasScaler가 해상도 스케일) =====
        private const int   SlotCount      = 8;
        private const float SlotSize       = 96f;   // 슬롯 한 변
        private const float SlotGap        = 10f;   // 슬롯 간격
        private const float PanelPadX      = 14f;   // 패널 좌우 여백
        private const float NumBoxHeight   = 24f;   // 하단 숫자 박스 높이
        private const float NumBoxGap      = 6f;    // 슬롯~숫자박스 간격
        private const float PanelPadTop    = 12f;
        private const float PanelPadBottom = 12f;
        private const float BottomMargin   = 24f;   // 화면 하단에서 패널까지 거리

        private const float PanelWidth  = PanelPadX * 2f + SlotSize * SlotCount + SlotGap * (SlotCount - 1); // 866
        private const float PanelHeight = PanelPadTop + SlotSize + NumBoxGap + NumBoxHeight + PanelPadBottom; // 150

        // ===== 다크 테마 색상 (InventoryWindow 톤 유지) =====
        private static readonly Color ColorPanelBg   = new Color(0.06f, 0.06f, 0.08f, 0.72f); // 패널 다크 반투명
        private static readonly Color ColorSlotBg    = new Color(0.13f, 0.13f, 0.16f, 0.88f); // 슬롯 배경
        private static readonly Color ColorSlotEmpty = new Color(0.13f, 0.13f, 0.16f, 0.40f); // 빈 슬롯(어둡게)
        private static readonly Color ColorSelect    = new Color(1f, 1f, 1f, 0.95f);          // 흰 두꺼운 선택 테두리
        private static readonly Color ColorKeyBox    = new Color(0.10f, 0.10f, 0.12f, 0.90f); // 숫자 박스
        private static readonly Color ColorText      = new Color(0.88f, 0.88f, 0.88f, 1f);
        private static readonly Color ColorTextDim   = new Color(0.70f, 0.70f, 0.74f, 0.9f);
        private static readonly Color ColorCount     = new Color(1f, 0.95f, 0.75f, 1f);       // 수량(연금색 소형)

        // ===== 런타임 상태 =====
        private Canvas _canvas;
        private Font _font;
        private Sprite _roundedSprite;

        private readonly Image[] _slotBgs       = new Image[SlotCount];
        private readonly Image[] _selectBorders = new Image[SlotCount]; // 흰 두꺼운 테두리 (장착 중만 표시)
        private readonly Text[]  _countTexts    = new Text[SlotCount];  // 우상단 수량
        private float _countRefreshTimer;

        private Transform _cachedPlayerT;
        private float _playerCacheTime;
        private string _lastSyncedEquipId = "__none__"; // CurrentId 변화 감지용

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

            // 씬 전환에도 핫바 유지 (상시 UI)
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            HandleNumberKeys();
            SyncSelectionHighlight();
            RefreshCountsPeriodically();
        }

        // ===== 입력: Alpha1~Alpha8 =====
        private void HandleNumberKeys()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                // KeyCode.Alpha1(49)부터 연속 배치 → Alpha1 + i
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    ActivateSlot(i);
            }
        }

        /// <summary>슬롯 실행 (키 1~8 / 추후 클릭 확장)</summary>
        public void ActivateSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;
            SlotDef def = SlotDefs[index];

            switch (def.kind)
            {
                case SlotKind.Weapon:
                    ActivateWeaponSlot(index, def);
                    break;

                case SlotKind.Consumable:
                    ActivateConsumableSlot(index, def);
                    break;

                case SlotKind.Empty:
                default:
                    Debug.Log($"[HotbarUI] 슬롯 {index + 1}: 빈 슬롯 (확장 예약, Phase M4)");
                    break;
            }
        }

        /// <summary>
        /// 장비 슬롯 — WeaponEquipManager.Equip(equipId, player, weaponType) 호출, 다시 누르면 Unequip(토글).
        /// equipId는 GLB 경로 결합용 기본 이름(steel/crystal/wood) — 매니저가 타입별 접미사 _sword/_bow/_spear를 붙임.
        /// </summary>
        private void ActivateWeaponSlot(int index, SlotDef def)
        {
            if (string.IsNullOrEmpty(def.equipId))
            {
                Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{def.specId}': 장착 id 미정 — 스킵");
                return;
            }

            // M4: 무기 장착 시 투척 모드 해제 (모드 상호배타)
            ProjectName.Systems.PlayerWeaponModeBridge.ThrowSelected = false;

            // 토글: 같은 무기가 이미 장착 중이면 해제
            if (WeaponEquipManager.CurrentId == def.equipId && WeaponEquipManager.IsEquipped)
            {
                WeaponEquipManager.Unequip();
                Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{def.specId}': 장착 해제 (토글)");
                SyncSelectionHighlight();
                return;
            }

            Transform playerT = GetPlayerTransform();
            if (playerT == null)
            {
                Debug.LogWarning("[HotbarUI] 플레이어를 찾지 못해 장착 스킵");
                return;
            }

            WeaponEquipManager.Equip(def.equipId, playerT, def.weaponType);
            SyncSelectionHighlight();
        }

        /// <summary>
        /// 소비 슬롯 — 인벤 수량 확인 후 사용/투척 준비.
        /// v1은 로그 전용 (투척은 BombThrower/ConsumableSystem 연결이 필요 → Phase M4).
        /// </summary>
        private void ActivateConsumableSlot(int index, SlotDef def)
        {
            // TODO(M4): BombThrower.Throw() / ConsumableSystem.Use 연결
            var inv = PlayerInventory.Instance;
            int count = inv != null ? inv.GetItemCount(def.specId) : 0;

            if (count <= 0)
            {
                Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{def.specId}': 보유 수량 0 — 사용 불가");
                return;
            }

            // M4: 폭탄 투척 모드 진입 — 폭탄 소모는 발사 시점에 처리 예정
            ProjectName.Systems.PlayerWeaponModeBridge.ThrowSelected = true;
            ProjectName.Systems.WeaponEquipManager.Unequip(); // 다른 무기 해제(모드 상호배타)
            Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{def.specId}': 투척 모드 진입 (보유 x{count}) — 폭탄 소모는 발사 시점에 처리 예정");
        }

        // ===== 장착 상태 ↔ 하이라이트 동기화 =====
        private void SyncSelectionHighlight()
        {
            string currentId = WeaponEquipManager.IsEquipped ? WeaponEquipManager.CurrentId : null;
            if (currentId == _lastSyncedEquipId) return;
            _lastSyncedEquipId = currentId;

            for (int i = 0; i < SlotCount; i++)
            {
                var border = _selectBorders[i];
                if (border == null) continue;

                SlotDef def = SlotDefs[i];
                bool selected = def.kind == SlotKind.Weapon
                                && !string.IsNullOrEmpty(def.equipId)
                                && def.equipId == currentId;
                border.enabled = selected;
            }
        }

        // ===== 수량 텍스트 갱신 (0.5초 폴링 — 이벤트 시스템 미구현) =====
        private void RefreshCountsPeriodically()
        {
            _countRefreshTimer += Time.unscaledDeltaTime;
            if (_countRefreshTimer < 0.5f) return;
            _countRefreshTimer = 0f;

            var inv = PlayerInventory.Instance;
            for (int i = 0; i < SlotCount; i++)
            {
                var txt = _countTexts[i];
                if (txt == null) continue;

                SlotDef def = SlotDefs[i];
                if (def.kind == SlotKind.Consumable && inv != null)
                {
                    int count = inv.GetItemCount(def.specId);
                    txt.text = count > 0 ? $"x{count}" : string.Empty;
                }
                else
                {
                    txt.text = string.Empty;
                }
            }
        }

        // ===== 플레이어 Transform (InventoryWindow.GetPlayerTransform 선례 동일) =====
        private Transform GetPlayerTransform()
        {
            if (_cachedPlayerT != null && Time.unscaledTime - _playerCacheTime < 1f)
                return _cachedPlayerT;

            _cachedPlayerT = null;
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) _cachedPlayerT = pm.transform;
            if (_cachedPlayerT == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) _cachedPlayerT = tagged.transform;
            }
            _playerCacheTime = Time.unscaledTime;
            return _cachedPlayerT;
        }

        // =====================================================================
        //  UI 빌드 (코드 생성 — 프리팹/씬 편집 불필요)
        // =====================================================================

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("HotbarCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // 상시 UI (IMGUI 윈도우가 항상 위에 그려짐)

            // 1080p 디자인 해상도 기준 스케일
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // 라운드 코너 스프라이트 (프로시저럴, 에셋 의존성 0)
            _roundedSprite = CreateRoundedSprite(64, 16);
            _font = LoadBuiltinFont();
        }

        private void BuildPanel()
        {
            RectTransform panel = CreateImage(
                _canvas.transform, "HotbarPanel", _roundedSprite, ColorPanelBg,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BottomMargin), new Vector2(PanelWidth, PanelHeight));
            panel.GetComponent<Image>().type = Image.Type.Sliced;

            float slotTop = PanelPadTop;
            float slotBottom = slotTop + SlotSize;
            float numBoxY = slotBottom + NumBoxGap;

            for (int i = 0; i < SlotCount; i++)
            {
                SlotDef def = SlotDefs[i];
                bool isEmpty = def.kind == SlotKind.Empty;

                float x = PanelPadX + i * (SlotSize + SlotGap);

                // ① 흰 두꺼운 선택 테두리 (슬롯보다 8px 크게 뒤에 깔림 → 장착 중일 때만 표시)
                RectTransform border = CreateImage(
                    panel, $"Slot{i}_Select", _roundedSprite, ColorSelect,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(x - 8f, slotTop - 8f), new Vector2(SlotSize + 16f, SlotSize + 16f));
                var borderImg = border.GetComponent<Image>();
                borderImg.type = Image.Type.Sliced;
                borderImg.raycastTarget = false;
                borderImg.enabled = false; // 초기 비장착
                _selectBorders[i] = borderImg;

                // ② 슬롯 배경 (다크 반투명, 라운드)
                RectTransform bg = CreateImage(
                    panel, $"Slot{i}_Bg", _roundedSprite, isEmpty ? ColorSlotEmpty : ColorSlotBg,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(x, slotTop), new Vector2(SlotSize, SlotSize));
                var bgImg = bg.GetComponent<Image>();
                bgImg.type = Image.Type.Sliced;
                bgImg.raycastTarget = false;
                _slotBgs[i] = bgImg;

                // ③ 중앙 라벨 (v1: 아이콘 대체 텍스트)
                if (!isEmpty)
                {
                    RectTransform label = CreateText(
                        bg, $"Slot{i}_Label", def.label, 34, ColorText,
                        TextAnchor.MiddleCenter, new Vector2(SlotSize * 0.5f, SlotSize * 0.5f),
                        new Vector2(SlotSize, SlotSize));
                    label.GetComponent<Text>().raycastTarget = false;
                }

                // ④ 우상단 수량 (소비 아이템만 갱신됨)
                if (def.kind == SlotKind.Consumable)
                {
                    RectTransform count = CreateText(
                        bg, $"Slot{i}_Count", string.Empty, 20, ColorCount,
                        TextAnchor.UpperRight, new Vector2(-6f, -4f),
                        new Vector2(70f, 24f), new Vector2(1f, 1f), new Vector2(1f, 1f));
                    count.GetComponent<Text>().raycastTarget = false;
                    _countTexts[i] = count.GetComponent<Text>();
                }

                // ⑤ 하단 숫자 박스 (1~8)
                float numX = x + SlotSize * 0.5f;
                RectTransform keyBox = CreateImage(
                    panel, $"Slot{i}_KeyBox", _roundedSprite, ColorKeyBox,
                    new Vector2(0f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(numX - 13f, numBoxY), new Vector2(26f, NumBoxHeight));
                var keyBoxImg = keyBox.GetComponent<Image>();
                keyBoxImg.type = Image.Type.Sliced;
                keyBoxImg.raycastTarget = false;

                RectTransform keyText = CreateText(
                    keyBox, $"Slot{i}_KeyText", (i + 1).ToString(), 16, ColorTextDim,
                    TextAnchor.MiddleCenter, new Vector2(13f, NumBoxHeight * 0.5f),
                    new Vector2(26f, NumBoxHeight));
                keyText.GetComponent<Text>().raycastTarget = false;
            }
        }

        // ===== 빌드 헬퍼 =====

        private static RectTransform CreateImage(
            Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = Vector2.zero; // anchoredPos = 좌하단 모서리 기준
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
            rt.anchorMin = rt.anchorMax = anchorPoint; // default (0,0) = 부모 좌하단 기준
            rt.pivot = pivot;                          // default (0.5,0.5) = anchoredPos가 중심
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

        /// <summary>9-Slice용 라운드 사각 스프라이트 생성 (라운드 코너 + 슬라이스 확장).</summary>
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
                    bool inside = IsInsideRoundedRect(px, py, size, radiusF);
                    // 코너 경계 1px 안티앨리어싱 (부드러운 라운드)
                    float alpha = inside ? 255f : 0f;
                    if (!inside && radiusF > 0f)
                    {
                        // 코너 바깥 1px 내부는 부분 알파로 부드럽게
                        float dist = DistanceToRoundedRect(px, py, size, radiusF);
                        if (dist < 1f) alpha = (1f - dist) * 255f;
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            int b = radius; // 9-Slice 보더 = 코너 반경
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                size, 0u, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static bool IsInsideRoundedRect(float px, float py, float size, float radius)
        {
            return DistanceToRoundedRect(px, py, size, radius) <= 0f;
        }

        /// <summary>라운드 사각형 "바깥"까지의 거리(내부/경계는 0 이하) — 부드러운 코너용.</summary>
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
                Debug.LogWarning("[HotbarUI] 빌트인 폰트 로드 실패 — 텍스트 미표시");
                return null;
            }
        }
    }
}
