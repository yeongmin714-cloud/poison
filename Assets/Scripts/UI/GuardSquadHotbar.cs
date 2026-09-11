using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 병사 부대 핫바 (RTS 제어그룹) — Tab 키로 아이템 핫바(HotbarUI)와 토글 전환.
    ///
    /// [스펙]
    /// - Tab 키: 하단 중앙 동일 자리에 아이템 핫바(HotbarUI) ↔ 병사 부대 핫바 교체 표시
    /// - 8슬롯(1~8):
    ///     · Ctrl+숫자   — 박스 드래그로 선택한 병사(그룹)를 해당 슬롯에 등록(덮어쓰기), 즉시 아바타 표시
    ///     · 숫자(부대 모드) — 슬롯에 등록된 병사들(생존자만)을 GuardSelectionManager로 RTS 선택
    ///                       (파란 원 표시 — 이후 우클릭 공격/이동 명령은 기존 RTSCommandSystem 경로 사용)
    /// - 아바타: 생존 대표 병사의 실제 3D 외형 아이콘(GuardIconRenderer 오프스크린 베이크) 우선 표시.
    ///           베이크 전/실패 시 기존 절차 아바타(국적색 원형 + 이니셜 + "Lv{N}" 텍스트)로 폴백.
    ///           대표 병사가 죽었으면(IsAlive false) 아이콘 대신 기존 회색 절차 아바타.
    ///           그룹 인원 수는 우상단 "x{생존수}" 소형 표시.
    ///
    /// [국적 색상] 동=빨강, 서=파랑, 남=초록, 북=보라, 황제국/무소속/기타=회색
    ///
    /// [구현 방식]
    /// - HotbarUI와 동일한 "코드 생성 uGUI" 패턴 (셀프 부트스트랩 + 싱글턴 + 프로시저럴 스프라이트, 에셋 의존성 0).
    /// - 루트 GO는 상시 활성(Tab 리스닝 유지), 패널만 활성/비활성 전환.
    /// - 부대 모드 진입 시 HotbarUI.SetVisible(false)로 아이템 핫바 GO를 비활성화 → 화면 숨김과
    ///   동시에 아이템 핫바의 1~8 숫자키 처리(HandleNumberKeys)도 자연 차단된다(Update 정지).
    /// - 등록 그룹은 씬 재진입 시 파괴된 참조(null)로 무시하며, 사망 병사는 선택/아바타 갱신에서 제외.
    /// </summary>
    public class GuardSquadHotbar : MonoBehaviour
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static GuardSquadHotbar _instance;

        /// <summary>씬 편집 없이 부대 핫바를 띄우기 위한 셀프 부트스트랩 (HotbarUI 선례 동일).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var existing = Object.FindAnyObjectByType<GuardSquadHotbar>();
            if (existing != null) { _instance = existing; return; }

            var go = new GameObject("GuardSquadHotbar");
            _instance = go.AddComponent<GuardSquadHotbar>();
            Debug.Log("[GuardSquadHotbar] 생성됨 — Tab: 아이템/부대 핫바 토글, Ctrl+1~8: 부대 등록, 1~8: 부대 선택");
        }

        // ===== 레이아웃 상수 (HotbarUI와 동일 — 하단 중앙 동일 자리 공유) =====
        private const int   SlotCount      = 8;
        private const float SlotSize       = 96f;   // 슬롯 한 변
        private const float SlotGap        = 10f;   // 슬롯 간격
        private const float PanelPadX      = 14f;   // 패널 좌우 여백
        private const float NumBoxHeight   = 24f;   // 하단 숫자 박스 높이
        private const float NumBoxGap      = 6f;    // 슬롯~숫자박스 간격
        private const float PanelPadTop    = 12f;
        private const float PanelPadBottom = 12f;
        private const float BottomMargin   = 12f;   // 화면 하단에서 패널까지 거리
        private const float AvatarSize     = 56f;   // 원형 아바타 지름

        private const float PanelWidth  = PanelPadX * 2f + SlotSize * SlotCount + SlotGap * (SlotCount - 1); // 866 (HotbarUI와 동일)
        private const float PanelHeight = PanelPadTop + SlotSize + NumBoxGap + NumBoxHeight + PanelPadBottom; // 150

        // ===== 다크 테마 색상 (HotbarUI 톤 유지) =====
        private static readonly Color ColorPanelBg = new Color(0.06f, 0.06f, 0.08f, 0.72f); // 패널 다크 반투명
        private static readonly Color ColorSlotBg    = new Color(0.13f, 0.13f, 0.16f, 0.88f); // 등록된 슬롯 배경
        private static readonly Color ColorSlotEmpty = new Color(0.13f, 0.13f, 0.16f, 0.40f); // 빈 슬롯(어둡게)
        private static readonly Color ColorKeyBox    = new Color(0.10f, 0.10f, 0.12f, 0.90f); // 숫자 박스
        private static readonly Color ColorText      = new Color(0.88f, 0.88f, 0.88f, 1f);
        private static readonly Color ColorTextDim   = new Color(0.70f, 0.70f, 0.74f, 0.9f);
        private static readonly Color ColorCount     = new Color(1f, 0.95f, 0.75f, 1f);       // 인원 수(연금색 소형)

        // 국적색 (동=빨강, 서=파랑, 남=초록, 북=보라, 기타=회색)
        private static readonly Color ColorNationEast   = new Color(0.92f, 0.28f, 0.26f, 1f);
        private static readonly Color ColorNationWest   = new Color(0.30f, 0.56f, 0.95f, 1f);
        private static readonly Color ColorNationSouth  = new Color(0.30f, 0.78f, 0.40f, 1f);
        private static readonly Color ColorNationNorth  = new Color(0.64f, 0.42f, 0.90f, 1f);
        private static readonly Color ColorNationDefault = new Color(0.62f, 0.62f, 0.66f, 1f);

        // 사망/빈 슬롯 처리
        private static readonly Color ColorAvatarDead  = new Color(0.42f, 0.42f, 0.46f, 0.85f); // 사망 — 회색 원
        private static readonly Color ColorAvatarEmpty = new Color(0.30f, 0.30f, 0.34f, 0.35f); // 미등록(숨김 상태와 동일 톤)
        private static readonly Color ColorTextDead    = new Color(0.55f, 0.55f, 0.58f, 1f);    // 사망 — 회색 글자

        // ===== 런타임 상태 =====
        private Canvas _canvas;
        private Font _font;
        private Sprite _roundedSprite;  // 9-Slice 라운드 (패널/슬롯/숫자박스)
        private Sprite _circleSprite;   // 원형 아바타용 흰색 스프라이트 (Image.color로 국적색 틴트)
        private GameObject _panelGO;    // 패널 (Tab 토글 대상 — 루트 GO는 Tab 리스닝용으로 상시 활성)

        private bool _squadMode;                                        // false=모드 A(아이템), true=모드 B(부대)
        private readonly GuardPlaceholder[][] _slots = new GuardPlaceholder[SlotCount][]; // 부대 그룹 등록 (씬 재진입 시 null 무시)

        private readonly Image[] _slotBgs      = new Image[SlotCount];
        private readonly Image[] _avatarImages = new Image[SlotCount]; // 국적색 원형 (폴백 절차 아바타)
        private readonly Sprite[]    _slotIconSprites  = new Sprite[SlotCount];    // 실제 3D 아이콘 Sprite (슬롯별 캐시 — 매 갱신 Sprite.Create 방지)
        private readonly Texture2D[] _slotIconTextures = new Texture2D[SlotCount]; // 슬롯이 참조 중인 원본 텍스처 (교체/파괴 감지용)
        private readonly Text[]  _initialTexts = new Text[SlotCount];  // 이름 이니셜
        private readonly Text[]  _levelTexts   = new Text[SlotCount];  // "Lv{N}"
        private readonly Text[]  _countTexts   = new Text[SlotCount];  // "x{생존수}"
        private float _refreshTimer;                                   // 사망 폴링 타이머

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

            // 기본은 모드 A(아이템 핫바) — 부대 핫바 패널은 숨긴 채 시작
            if (_panelGO != null) _panelGO.SetActive(false);

            // 씬 전환에도 유지 (상시 UI — HotbarUI 선례 동일)
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            // 부대 모드에서 파괴되면 아이템 핫바를 반드시 복원 (숨겨진 채 잔류 방지)
            if (_squadMode) HotbarUI.SetVisible(true);

            // 실제 아이콘 스프라이트 해제 (원본 텍스처는 GuardIconRenderer 캐시 소유 — 여기서 파괴하지 않음)
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slotIconSprites[i] != null) { Destroy(_slotIconSprites[i]); _slotIconSprites[i] = null; }
                _slotIconTextures[i] = null;
            }

            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            HandleTabKey();         // Tab: 모드 토글 (상시)
            HandleCtrlAssignKeys(); // Ctrl+1~8: 부대 등록 (상시 — 아이템 모드에서도 무해)
            if (_squadMode)
                HandleSelectKeys(); // 1~8: 부대 선택 (부대 모드에서만)
            RefreshAvatarsPeriodically(); // 사망/파괴 반영 폴링
        }

        // ===== Tab 토글 =====
        private void HandleTabKey()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                ToggleMode();
        }

        /// <summary>아이템 핫바 ↔ 병사 부대 핫바 모드 전환.</summary>
        public void ToggleMode()
        {
            SetSquadMode(!_squadMode);
        }

        private void SetSquadMode(bool squadMode)
        {
            if (_squadMode == squadMode) return;
            _squadMode = squadMode;

            // 아이템 핫바 표시 전환 (정적 헬퍼 — 인스턴스 미존재 시 안전 no-op)
            HotbarUI.SetVisible(!squadMode);

            // 부대 핫바 패널 표시 전환
            if (_panelGO != null) _panelGO.SetActive(squadMode);

            Debug.Log($"[GuardSquadHotbar] 모드 전환: {(squadMode ? "병사 부대" : "아이템")} 핫바 (Tab으로 되돌아감)");
        }

        /// <summary>현재 부대 모드인지 (디버그/연동용).</summary>
        public static bool IsSquadMode => _instance != null && _instance._squadMode;

        // ===== Ctrl+1~8: 선택된 병사 그룹 슬롯 등록 (덮어쓰기) =====
        private void HandleCtrlAssignKeys()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrl) return;

            for (int i = 0; i < SlotCount; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    RegisterSelectedGroupToSlot(i);
            }
        }

        private void RegisterSelectedGroupToSlot(int index)
        {
            var mgr = GuardSelectionManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning("[GuardSquadHotbar] GuardSelectionManager가 없어 부대 등록 스킵");
                return;
            }
            if (mgr.SelectedCount <= 0)
            {
                Debug.Log("[GuardSquadHotbar] 선택된 병사 없음 — 박스 드래그로 병사 선택 후 Ctrl+숫자");
                return;
            }

            var members = new List<GuardPlaceholder>(mgr.SelectedCount);
            foreach (var guard in mgr.SelectedGuards)
            {
                if (guard != null) members.Add(guard);
            }

            _slots[index] = members.ToArray(); // 덮어쓰기
            RefreshSlotVisual(index);          // 등록 즉시 아바타 표시
            Debug.Log($"[GuardSquadHotbar] 슬롯 {index + 1}에 병사 {members.Count}명 등록 (덮어쓰기)");
        }

        // ===== 1~8 (부대 모드): 등록 그룹 RTS 선택 =====
        private void HandleSelectKeys()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    SelectSlotGroup(i);
            }
        }

        private void SelectSlotGroup(int index)
        {
            var members = _slots[index];
            if (members == null || members.Length == 0)
            {
                Debug.Log($"[GuardSquadHotbar] 슬롯 {index + 1}: 빈 슬롯 — Ctrl+숫자로 먼저 등록");
                return;
            }

            // 생존 병사만 선택 대상 (사망/파괴 참조 제외)
            var alive = new List<GuardPlaceholder>(members.Length);
            foreach (var guard in members)
            {
                if (guard == null) continue;   // 씬 재진입 등으로 파괴된 참조 무시
                if (!guard.IsAlive) continue;  // 사망 병사 제외
                alive.Add(guard);
            }

            if (alive.Count == 0)
            {
                Debug.Log($"[GuardSquadHotbar] 슬롯 {index + 1}: 등록 병사 전원 사망 — 선택 스킵");
                return;
            }

            var mgr = GuardSelectionManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning("[GuardSquadHotbar] GuardSelectionManager가 없어 선택 스킵");
                return;
            }

            mgr.SelectGroup(alive); // 파란 원 표시 + 이후 우클릭 명령은 기존 경로
            Debug.Log($"[GuardSquadHotbar] 슬롯 {index + 1}: 등록 병사 {alive.Count}명 RTS 선택");
        }

        // ===== 아바타 갱신 (0.5초 폴링 — 사망/파괴 반영, HotbarUI 수량 폴링 선례 동일) =====
        private void RefreshAvatarsPeriodically()
        {
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < 0.5f) return;
            _refreshTimer = 0f;

            for (int i = 0; i < SlotCount; i++)
                RefreshSlotVisual(i);
        }

        /// <summary>
        /// 슬롯 아바타 갱신 — 대표 병사(등록 순서상 첫 생존 병사, 전원 사망 시 첫 등록 병사) 기준.
        /// 사망(IsAlive false)이면 원/글자를 회색 처리. 등록 정보가 모두 파괴됐으면 빈 슬롯으로 복원.
        /// </summary>
        private void RefreshSlotVisual(int index)
        {
            var members = _slots[index];

            // 등록 상태에 따른 슬롯 배경 밝기
            bool hasMembers = false;
            if (members != null)
            {
                foreach (var g in members) { if (g != null) { hasMembers = true; break; } }
            }
            if (_slotBgs[index] != null)
                _slotBgs[index].color = hasMembers ? ColorSlotBg : ColorSlotEmpty;

            // 대표 병사 선정 + 생존 인원 집계
            GuardPlaceholder lead = null;
            int aliveCount = 0;
            int total = 0;
            if (members != null)
            {
                foreach (var g in members)
                {
                    if (g == null) continue;
                    total++;
                    if (g.IsAlive) { aliveCount++; if (lead == null) lead = g; }
                }
                if (lead == null)
                {
                    foreach (var g in members) { if (g != null) { lead = g; break; } }
                }
            }

            bool registered = lead != null;
            bool dead = registered && !lead.IsAlive;

            // 실제 3D 외형 아이콘 시도 (생존 대표 병사만) — null이면 기존 절차 아바타 폴백.
            // GetOrCreateIcon은 캐시 히트 시 즉시 반환, 미베이크 시 큐 등록 후 null
            // (0.5초 폴링이 재호출하므로 몇 프레임 후 아이콘이 자동 반영된다).
            bool useRealIcon = false;
            if (registered && !dead && _avatarImages[index] != null)
            {
                try
                {
                    Texture2D iconTex = GuardIconRenderer.GetOrCreateIcon(lead);
                    if (iconTex != null)
                    {
                        _avatarImages[index].sprite = GetOrCreateSlotIconSprite(index, iconTex);
                        _avatarImages[index].color = Color.white; // 아이콘 자체 색상 사용 (국적색 틴트 미적용)
                        useRealIcon = true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[GuardSquadHotbar] 실제 아이콘 적용 실패 — 절차 아바타 폴백: " + e.Message);
                }
            }

            if (_avatarImages[index] != null)
            {
                // 실제 아이콘 미사용 시 절차 원형으로 복원 (아이콘 → 사망/미등록 전환 대응)
                if (!useRealIcon && _avatarImages[index].sprite != _circleSprite)
                    _avatarImages[index].sprite = _circleSprite;

                _avatarImages[index].enabled = registered;
                if (!useRealIcon)
                    _avatarImages[index].color = !registered ? ColorAvatarEmpty
                        : dead ? ColorAvatarDead
                        : GetNationColor(lead.Nation);
            }

            if (_initialTexts[index] != null)
            {
                // 실제 아이콘 표시 중에는 이니셜 숨김 (캐릭터 실루엣 위 글자 겹침 방지)
                _initialTexts[index].text = registered && !useRealIcon ? GetInitial(lead.GuardName) : string.Empty;
                _initialTexts[index].color = dead ? ColorTextDead : ColorText;
            }

            if (_levelTexts[index] != null)
            {
                _levelTexts[index].text = registered ? $"Lv{lead.Level}" : string.Empty;
                _levelTexts[index].color = dead ? ColorTextDead : ColorTextDim;
            }

            if (_countTexts[index] != null)
                _countTexts[index].text = registered && total > 1 ? $"x{aliveCount}" : string.Empty;
        }

        /// <summary>
        /// 베이크된 Texture2D를 슬롯 표시용 Sprite로 변환 (슬롯별 캐시 — 갱신마다 Sprite.Create 방지).
        /// 원본 텍스처는 GuardIconRenderer 캐시가 소유하므로 Sprite만 교체/파괴한다.
        /// </summary>
        private Sprite GetOrCreateSlotIconSprite(int index, Texture2D tex)
        {
            Sprite existing = _slotIconSprites[index];
            if (existing != null && _slotIconTextures[index] == tex && tex != null)
                return existing;

            if (existing != null)
                Destroy(existing); // Sprite만 해제 — 원본 텍스처는 렌더러 캐시 소유
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _slotIconSprites[index] = sprite;
            _slotIconTextures[index] = tex;
            return sprite;
        }

        /// <summary>이름 첫 글자(이니셜) — 서러게이트 쌍(이모지 등)은 2 코드유닛으로 안전 잘라냄.</summary>
        private static string GetInitial(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int len = (char.IsSurrogate(name[0]) && name.Length >= 2) ? 2 : 1;
            return name.Substring(0, len);
        }

        /// <summary>국적 → 아바타 색상 (동=빨강, 서=파랑, 남=초록, 북=보라, 황제국/무소속/기타=회색).</summary>
        private static Color GetNationColor(string nation)
        {
            if (string.IsNullOrEmpty(nation)) return ColorNationDefault;
            if (nation.Contains("동")) return ColorNationEast;
            if (nation.Contains("서")) return ColorNationWest;
            if (nation.Contains("남")) return ColorNationSouth;
            if (nation.Contains("북")) return ColorNationNorth;
            return ColorNationDefault;
        }

        // =====================================================================
        //  UI 빌드 (코드 생성 — 프리팹/씬 편집 불필요, HotbarUI 컨벤션 동일)
        // =====================================================================

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("GuardSquadCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 101; // HotbarUI(100)와 동일 자리 공유 — 부대 핫바를 한 단계 위에

            // 1080p 디자인 해상도 기준 스케일 (HotbarUI 동일)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // 프로시저럴 스프라이트 (에셋 의존성 0)
            _roundedSprite = CreateRoundedSprite(64, 16);
            _circleSprite = CreateCircleSprite(96);
            _font = LoadBuiltinFont();
        }

        private void BuildPanel()
        {
            // 하단 중앙 앵커 — HotbarUI와 동일한 자리/크기 (866 x 150)
            RectTransform panel = CreateImage(
                _canvas.transform, "SquadHotbarPanel", _roundedSprite, ColorPanelBg,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BottomMargin), new Vector2(PanelWidth, PanelHeight));
            panel.pivot = new Vector2(0.5f, 0f); // 하단 중앙 기준 (HotbarUI 선례 동일 — CreateImage 기본 pivot(0,0) 보정)
            panel.GetComponent<Image>().type = Image.Type.Sliced;
            _panelGO = panel.gameObject;

            float slotTop = PanelPadTop;
            float slotBottom = slotTop + SlotSize;
            float numBoxY = slotBottom + NumBoxGap;

            for (int i = 0; i < SlotCount; i++)
            {
                float x = PanelPadX + i * (SlotSize + SlotGap);

                // ① 슬롯 배경 (미등록=어둡게, 등록 시 밝게 — RefreshSlotVisual에서 갱신)
                RectTransform bg = CreateImage(
                    panel, $"SquadSlot{i}_Bg", _roundedSprite, ColorSlotEmpty,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(x, slotTop), new Vector2(SlotSize, SlotSize));
                var bgImg = bg.GetComponent<Image>();
                bgImg.type = Image.Type.Sliced;
                bgImg.raycastTarget = false;
                _slotBgs[i] = bgImg;

                // ② 원형 아바타 (국적색 틴트 — 등록 시 표시)
                float avatarOff = (SlotSize - AvatarSize) * 0.5f;
                RectTransform avatar = CreateImage(
                    bg, $"SquadSlot{i}_Avatar", _circleSprite, ColorAvatarEmpty,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(avatarOff, avatarOff), new Vector2(AvatarSize, AvatarSize));
                var avatarImg = avatar.GetComponent<Image>();
                avatarImg.raycastTarget = false;
                avatarImg.enabled = false; // 미등록 슬롯은 아바타 숨김
                _avatarImages[i] = avatarImg;

                // ③ 이니셜 (원 중앙)
                var initial = CreateText(
                    bg, $"SquadSlot{i}_Initial", string.Empty, 24, ColorText,
                    TextAnchor.MiddleCenter, new Vector2(SlotSize * 0.5f, SlotSize * 0.5f + 4f),
                    new Vector2(AvatarSize, AvatarSize));
                _initialTexts[i] = initial.GetComponent<Text>();

                // ④ Lv 텍스트 (슬롯 하단)
                var level = CreateText(
                    bg, $"SquadSlot{i}_Level", string.Empty, 14, ColorTextDim,
                    TextAnchor.MiddleCenter, new Vector2(SlotSize * 0.5f, 9f),
                    new Vector2(SlotSize - 8f, 16f));
                _levelTexts[i] = level.GetComponent<Text>();

                // ⑤ 우상단 그룹 생존 인원 (그룹 2명 이상일 때 "x{생존수}")
                var count = CreateText(
                    bg, $"SquadSlot{i}_Count", string.Empty, 18, ColorCount,
                    TextAnchor.UpperRight, new Vector2(-5f, -3f),
                    new Vector2(60f, 22f), new Vector2(1f, 1f), new Vector2(1f, 1f));
                _countTexts[i] = count.GetComponent<Text>();

                // ⑥ 하단 숫자 박스 (1~8) — HotbarUI와 동일한 점앵커 방식
                float numX = x + SlotSize * 0.5f;
                RectTransform keyBox = CreateImage(
                    panel, $"SquadSlot{i}_KeyBox", _roundedSprite, ColorKeyBox,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(numX - 13f, numBoxY), new Vector2(26f, NumBoxHeight));
                var keyBoxImg = keyBox.GetComponent<Image>();
                keyBoxImg.type = Image.Type.Sliced;
                keyBoxImg.raycastTarget = false;

                var keyText = CreateText(
                    keyBox, $"SquadSlot{i}_KeyText", (i + 1).ToString(), 16, ColorTextDim,
                    TextAnchor.MiddleCenter, new Vector2(13f, NumBoxHeight * 0.5f),
                    new Vector2(26f, NumBoxHeight));
            }
        }

        // ===== 빌드 헬퍼 (HotbarUI 컨벤션 동일 — private이므로 복제) =====

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

        /// <summary>9-Slice용 라운드 사각 스프라이트 생성 (HotbarUI.CreateRoundedSprite 동일).</summary>
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
                    bool inside = DistanceToRoundedRect(px, py, size, radiusF) <= 0f;
                    // 경계 1px 안티앨리어싱
                    float alpha = inside ? 255f : 0f;
                    if (!inside && radiusF > 0f)
                    {
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

        private static float DistanceToRoundedRect(float px, float py, float size, float radius)
        {
            float maxX = size - radius;
            float cx = Mathf.Clamp(px, radius, maxX);
            float cy = Mathf.Clamp(py, radius, maxX);
            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - radius;
        }

        /// <summary>
        /// 원형 스프라이트 프로시저럴 생성 (흰색 원 — Image.color로 국적색 틴트, 에셋 의존성 0).
        /// 경계 1px 안티앨리어싱으로 부드러운 원형.
        /// </summary>
        private static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float center = size * 0.5f;
            float radius = size * 0.5f - 1f; // 1px 여백 (클램프 블리딩 방지)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f); // 1px 안티앨리어싱
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0u, SpriteMeshType.FullRect);
        }

        /// <summary>폰트 로드 — HotbarUI.LoadBuiltinFont와 동일 (한글 폰트 우선, 빌트인 폴백).</summary>
        private static Font LoadBuiltinFont()
        {
            var uiFont = ProjectName.UI.UIFont.Load(); // NotoSansKR → malgun → 빌트인 (static 캐시)
            if (uiFont != null) return uiFont;

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
                Debug.LogWarning("[GuardSquadHotbar] 빌트인 폰트 로드 실패 — 텍스트 미표시");
                return null;
            }
        }
    }
}