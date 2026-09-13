using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine.InputSystem;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 병사Placeholder - 사장님이 GLB를 제공하기 전까지 사용할 임시 병사 모델.
    /// C9-08: E키 상호작용 → 병사 정보 HUD 표시 + 메뉴 (말걸기/음식주기/약주기)
    /// C9-11: 음식/약주기 — 인벤토리 선택 → 아이템 지급 → 호감도/중독도 변화
    /// Phase 34: NPCAwarenessSystem 연동 + 시야각 120° + 암살
    /// 2026-09-12: 상호작용 패널 확대·고급화 — 다크네이비 플랫 패널 + 아바타(실제 3D 아이콘 → 국적색 원형 폴백)
    ///              + 체력바 확대 + [E] 가이드 (E키 동작/데이터 불변)
    /// </summary>
    public class GuardPlaceholder : MonoBehaviour, IDamageable, IWorldSpaceHUD
    {
        [Header("설정")]
        [SerializeField] private string guardName = "경비병";
        [SerializeField] private int level = 1;
        [SerializeField] private string nation = "동";
        [SerializeField] private string jobTitle = "병사";

        [Header("상호작용")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private float _maxHP = 10f;
        private float _currentHP;
        private bool _isDead = false;

        [Header("호감도/중독")]
        [SerializeField] private float _loyalty = 50f;
        [SerializeField] private float _addiction = 0f;

        [Header("포섭 (C9-15)")]
        [SerializeField] private bool _isRecruited = false;

        [Header("역할 (C9-16)")]
        [SerializeField] private GuardRole _role = GuardRole.Soldier; // 플레이어에게 포섭되었는가

        // ===== Phase 34: NPCAwarenessSystem =====
        [Header("Phase 34 — 경계 AI")]
        [SerializeField] private float _sightRange = 12f;
        [SerializeField][Range(1f, 180f)] private float _fieldOfView = 120f; // 시야각 120°
        private NPCAwarenessSystem _awareness;

        // ===== 사망 이벤트 (GuardResurrectionSystem 연동) =====
        public static event System.Action<GuardPlaceholder> OnAnyGuardDied;

        private enum SelectionMode { None, SelectingFood, SelectingDrug }
        private SelectionMode _selectionMode = SelectionMode.None;

        private bool _playerNearby = false;
        private bool _showInfo = false;
        private string _statusMessage = "";
        private Vector2 _invScrollPos;
        // Rig animation
        private RigAnimationController _rigAnim;

        // 캐시된 텍스처 (메모리 누수 방지)
        private static Texture2D _whitePixelTex;
        private static Texture2D _circleTex; // 국적색 원형 아바타 폴백 (지연 1회 생성)

        // 실제 3D 아바타 (GuardIconRenderer 오프스크린 베이크) — 0.5초 폴링 재조회 (GuardSquadHotbar 동일 패턴)
        private Texture2D _avatarIcon;
        private float _avatarNextPoll;

        // 캐시된 플레이어 참조 (매 프레임 Find 방지)
        private GameObject _playerCache;

        // ===== 정보 패널 스펙 팔레트 (다크네이비 반투명 / 회백 테두리 / 스카이블루 강조) =====
        private static readonly Color ColorPanelBg       = new Color(0.063f, 0.086f, 0.133f, 0.88f); // 패널 배경
        private static readonly Color ColorPanelBorder   = new Color(0.62f, 0.70f, 0.78f, 1f);       // 패널/테두리 회백
        private static readonly Color ColorAccent        = new Color(0.35f, 0.65f, 0.90f, 1f);       // 스카이블루 강조
        private static readonly Color ColorBarBg         = new Color(0.04f, 0.055f, 0.09f, 1f);      // 바 배경 — 플랫 다크네이비
        private static readonly Color ColorKeyBadgeBg    = new Color(0.10f, 0.13f, 0.19f, 1f);       // [E] 배지 배경
        private static readonly Color ColorBtnHover      = new Color(0.16f, 0.21f, 0.30f, 1f);       // 버튼 호버 틴트
        private static readonly Color ColorLoyaltyFill   = new Color(0.30f, 0.56f, 0.95f, 1f);       // 호감도 채움 (기존 블루 계열 유지)
        private static readonly Color ColorAddictionFill = new Color(0.95f, 0.35f, 0.85f, 1f);       // 중독도 채움 (기존 마젠타 계열 유지)

        // 국적색 (GuardSquadHotbar.GetNationColor 동일 값 — 동=빨강, 서=파랑, 남=초록, 북=보라, 기타=회색)
        private static readonly Color ColorNationEast    = new Color(0.92f, 0.28f, 0.26f, 1f);
        private static readonly Color ColorNationWest    = new Color(0.30f, 0.56f, 0.95f, 1f);
        private static readonly Color ColorNationSouth   = new Color(0.30f, 0.78f, 0.40f, 1f);
        private static readonly Color ColorNationNorth   = new Color(0.64f, 0.42f, 0.90f, 1f);
        private static readonly Color ColorNationDefault = new Color(0.62f, 0.62f, 0.66f, 1f);

        private void Awake()
        {
            _rigAnim = GetComponent<RigAnimationController>();
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
            }

            // C9-20: Rigidbody 캐싱 (있으면 MovePosition으로 이동 우회, 없으면 transform 직접 이동)
            _rb = GetComponent<Rigidbody>();

            // Phase 34: NPCAwarenessSystem 캐싱 (없으면 자동 추가)
            _awareness = GetComponent<NPCAwarenessSystem>();
            if (_awareness == null)
            {
                _awareness = gameObject.AddComponent<NPCAwarenessSystem>();
            }
        }

        public void SetGuardInfo(string name, int lvl, NationType nationType)
        {
            guardName = name;
            level = lvl;
            nation = NationTypeToKorean(nationType);
        }

        private static string NationTypeToKorean(NationType type)
        {
            switch (type)
            {
                case NationType.East: return "동";
                case NationType.West: return "서";
                case NationType.South: return "남";
                case NationType.North: return "북";
                case NationType.Empire: return "황제국";
                default: return "무소속";
            }
        }

        private void Start()
        {
            _currentHP = _maxHP;

            // 플레이어 캐싱
            _playerCache = GameObject.FindGameObjectWithTag("Player");

            // 기본 Idle 애니메이션
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // C32-04~06: 병사 장비 자동 생성 및 장착
            GuardEquipmentSpawner.SpawnEquipment(gameObject, level);
        }

        private void Update()
        {
            // 캐시된 참조 갱신 (null이거나 비활성화된 경우 재탐색)
            if (_playerCache == null || !_playerCache.activeInHierarchy)
                _playerCache = GameObject.FindGameObjectWithTag("Player");
            var player = _playerCache;

            // C9-20/C9-21: 명령 실행 루프 — 플레이어 부재와 무관하게 RTS/전투 명령(이동·공격)을 수행한다
            ExecuteMovement();

            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            _playerNearby = dist <= _interactRange;

            if (_playerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && _selectionMode == SelectionMode.None)
            {
                _showInfo = !_showInfo;
            }

            if (_showInfo && dist > _interactRange * 1.5f)
            {
                _showInfo = false;
                _selectionMode = SelectionMode.None;
            }

            // C9-12: 중독도 처리 (생존 중일 때만)
            if (!_isDead && _addiction > 0)
            {
                GuardAddictionSystem.ProcessDecay(this, Time.deltaTime);
                GuardAddictionSystem.ProcessPoisonDamage(this, Time.deltaTime);
                GuardAddictionSystem.CheckOverdose(this);
            }

            // 전투 타이머 갱신
            UpdateCombatTimer(Time.deltaTime);

            // Phase 34: NPCAwarenessSystem 연동 — 시야각 120° 체크
            UpdateAwareness(player, dist);

            // Phase 34: 은신 상태 NPC 뒤에서 좌클릭 → 암살
            TryAssassinateGuard(player, dist);

            // C9-21: 동행 병사 전투 AI (매 Update 말미 — playerTransform은 캐시된 _playerCache.transform)
            GuardCombatAI.UpdateGuardBehavior(this, player.transform);
        }

        private void OnGUI()
        {
            if (!_showInfo || _isDead) return;

            if (_selectionMode != SelectionMode.None)
            {
                DrawItemSelectionPopup();
                return;
            }

            EnsureStyles();

            EnsureAvatarIcon(); // 0.5초 폴링 — 실제 3D 아이콘 재조회 (GuardSquadHotbar 동일 패턴)

            // 패널: 기존(320x250)의 1.6배 확대 (≥1.5배 규격)
            float panelW = 520f;
            float panelH = 380f;
            float x = (Screen.width - panelW) / 2f;
            float y = Screen.height - panelH - 20f;

            // 플랫 패널 — 회백 2px 테두리 + ColorPanelBg 단색 배경 (절차 Rect 패턴)
            DrawFlatRect(x, y, panelW, panelH, ColorPanelBorder);
            DrawFlatRect(x + 2f, y + 2f, panelW - 4f, panelH - 4f, ColorPanelBg);
            // 하단 스카이블루 2px 타이틀 라인
            DrawFlatRect(x + 2f, y + panelH - 4f, panelW - 4f, 2f, ColorAccent);

            // ===== 좌측 아바타 96px (실제 3D 아이콘 → 국적색 원형+이니셜 폴백) =====
            Rect avatarRect = new Rect(x + 16f, y + 20f, 96f, 96f);
            if (_avatarIcon != null)
            {
                GUI.DrawTexture(avatarRect, _avatarIcon, ScaleMode.ScaleToFit, true);
            }
            else
            {
                EnsureCircleTex();
                Color prevColor = GUI.color;
                GUI.color = GetNationColor(nation);
                GUI.DrawTexture(avatarRect, _circleTex);
                GUI.color = prevColor;
                GUI.Label(avatarRect, GetInitial(guardName), _styleAvatarInitial);
            }

            // ===== 우측 정보 열 =====
            float rx = x + 128f;             // 아바타 열(16+96+16) 우측 기준
            float rw = panelW - 128f - 16f;  // 376
            float barW = panelW * 0.7f;      // 체력바 너비 = 패널의 70%
            float cy = y + 18f;

            // 타이틀 [국가] 역할 Lv.N (흰색 24px)
            string roleStr = GuardStatusSystem.GetRoleName(_role);
            GUI.Label(new Rect(rx, cy, rw, 32f), $"[{nation}] {roleStr} Lv.{level}", _styleTitle);
            cy += 44f;

            // 체력바 (높이 14px — ColorBarBg 배경 + 스카이블루 채움, hpRatio 기존 로직)
            float hpRatio = _currentHP / _maxHP;
            cy = DrawStatBar(rx, cy, barW, "❤️ 체력", $"{(int)(hpRatio * 100)}%", hpRatio, ColorAccent);

            // 호감도/중독도 바 (기존 표기 유지 — 팔레트 채움색 활용)
            cy = DrawStatBar(rx, cy, barW, "🤝 호감도", $"{(int)_loyalty}%", _loyalty / 100f, ColorLoyaltyFill);
            cy = DrawStatBar(rx, cy, barW, "💊 중독도", $"{(int)_addiction}%", _addiction / 100f, ColorAddictionFill);

            // 하단 [E] 배지 (ColorKeyBadgeBg 배경 + "[E] 상호작용" 13px)
            Rect badgeRect = new Rect(rx, cy + 6f, 160f, 30f);
            DrawFlatRect(badgeRect.x, badgeRect.y, badgeRect.width, badgeRect.height, ColorKeyBadgeBg);
            GUI.Label(badgeRect, "[E] 상호작용", _styleBadge);

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUI.Label(new Rect(x + 16f, y + panelH - 76f, panelW - 32f, 20f), _statusMessage, _styleMsg);
            }

            // 메뉴 버튼들
            float btnW = (panelW - 32f - 24f) / 5f;
            float btnY = y + panelH - 52f;

            if (FlatButton(new Rect(x + 16f, btnY, btnW, 36f), "🗣️ 말걸기")) OnTalk();
            if (FlatButton(new Rect(x + 16f + (btnW + 6f), btnY, btnW, 36f), "🥩 음식주기"))
            {
                _selectionMode = SelectionMode.SelectingFood;
                _invScrollPos = Vector2.zero;
            }
            if (FlatButton(new Rect(x + 16f + (btnW + 6f) * 2f, btnY, btnW, 36f), "💊 약주기"))
            {
                _selectionMode = SelectionMode.SelectingDrug;
                _invScrollPos = Vector2.zero;
            }
            if (FlatButton(new Rect(x + 16f + (btnW + 6f) * 3f, btnY, btnW, 36f), "🤝 포섭"))
            {
                OnRecruit();
            }
            if (FlatButton(new Rect(x + 16f + (btnW + 6f) * 4f, btnY, btnW, 36f), "🔙 닫기")) _showInfo = false;
        }

        // ===== C9-11: 아이템 선택 팝업 =====
        private void DrawItemSelectionPopup()
        {
            EnsureStyles();

            float popupW = 400f;
            float popupH = 350f;
            float x = (Screen.width - popupW) / 2f;
            float y = (Screen.height - popupH) / 2f;

            GUI.Box(new Rect(x, y, popupW, popupH), "");

            string title = _selectionMode == SelectionMode.SelectingFood ? "🥩 음식 선택" : "💊 약 선택";
            GUI.Label(new Rect(x + 10, y + 10, popupW - 20, 32), title, _styleTitle);

            var items = GetInventoryItemsByMode();
            float listY = y + 40f;
            float listH = popupH - 90f;

            GUI.BeginGroup(new Rect(x + 10, listY, popupW - 20, listH));

            if (items.Count == 0)
            {
                GUI.Label(new Rect(0, 0, popupW - 20, 24), "보유한 아이템이 없습니다.", _styleMsg);
            }
            else
            {
                float itemH = 40f;
                float viewH = items.Count * itemH;

                _invScrollPos = GUI.BeginScrollView(
                    new Rect(0, 0, popupW - 20, listH),
                    _invScrollPos,
                    new Rect(0, 0, popupW - 40, viewH)
                );

                for (int i = 0; i < items.Count; i++)
                {
                    var pair = items[i];
                    float iy = i * itemH;
                    GUI.Box(new Rect(0, iy, popupW - 40, itemH - 2), "");
                    GUI.Label(new Rect(10, iy + 2, 180, 20), pair.Key.displayName, _styleLabel);
                    GUI.Label(new Rect(10, iy + 20, 80, 16), $"x{pair.Value}", _styleValue);

                    if (GUI.Button(new Rect(popupW - 160, iy + 5, 100, 28), "주기"))
                    {
                        GiveItemToGuard(pair.Key);
                        _selectionMode = SelectionMode.None;
                        return;
                    }
                }
                GUI.EndScrollView();
            }
            GUI.EndGroup();

            if (GUI.Button(new Rect(x + popupW / 2 - 50, y + popupH - 40, 100, 30), "취소"))
            {
                _selectionMode = SelectionMode.None;
            }
        }

        private List<KeyValuePair<PlayerInventory.ItemData, int>> GetInventoryItemsByMode()
        {
            var result = new List<KeyValuePair<PlayerInventory.ItemData, int>>();
            if (PlayerInventory.Instance == null) return result;

            var slots = PlayerInventory.Instance.GetAllSlots();
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null || slot.count <= 0) continue;
                bool matches = _selectionMode == SelectionMode.SelectingFood
                    ? slot.item.category == PlayerInventory.ItemCategory.Food
                    : slot.item.category == PlayerInventory.ItemCategory.Potion
                      || slot.item.category == PlayerInventory.ItemCategory.Drug;
                if (matches) result.Add(new KeyValuePair<PlayerInventory.ItemData, int>(slot.item, slot.count));
            }
            return result;
        }

        // ===== C9-11: 아이템 지급 처리 =====
        private void GiveItemToGuard(PlayerInventory.ItemData item)
        {
            if (PlayerInventory.Instance == null || !PlayerInventory.Instance.HasItem(item.id))
            {
                _statusMessage = "아이템이 부족합니다.";
                return;
            }

            PlayerInventory.Instance.RemoveItem(item.id);

            switch (item.category)
            {
                case PlayerInventory.ItemCategory.Food:
                    float heal = 5f + item.displayName.Length * 0.5f;
                    _currentHP = Mathf.Min(_maxHP, _currentHP + heal);
                    GuardLoyaltySystem.GiveGift(this, 30);
                    _statusMessage = $"{guardName}: \\\"음식 고맙다!\\\" ❤️ 호감도 UP";
                    break;

                case PlayerInventory.ItemCategory.Potion:
                    _currentHP = Mathf.Min(_maxHP, _currentHP + 10f);
                    GuardLoyaltySystem.GiveGift(this, 50);
                    _statusMessage = $"{guardName}: \\\"약을 주다니 고맙군!\\\" ❤️ 호감도 UP";
                    break;

                case PlayerInventory.ItemCategory.Drug:
                    GuardLoyaltySystem.GiveDrug(this, 2);
                    _statusMessage = $"{guardName}: \\\"어.. 뭔가 이상한 기분이...\\\" 💊 중독+10";
                    break;
            }
        }

        // ===== 플랫 렌더 헬퍼 (기존 절차 Rect 패턴 — 1px 흰색 텍스처 + GUI.color 틴트) =====
        private static void DrawFlatRect(float x, float y, float w, float h, Color color)
        {
            if (_whitePixelTex == null)
            {
                _whitePixelTex = new Texture2D(1, 1);
                _whitePixelTex.SetPixel(0, 0, Color.white);
                _whitePixelTex.Apply();
            }
            Color prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, h), _whitePixelTex);
            GUI.color = prevColor;
        }

        /// <summary>캡션+수치 라인과 14px 플랫 바(배경 ColorBarBg)를 그린다 — 다음 행 y 반환.</summary>
        private float DrawStatBar(float bx, float by, float bw, string caption, string valueText, float ratio, Color fill)
        {
            GUI.Label(new Rect(bx, by, bw * 0.6f, 16f), caption, _styleLabel);
            GUI.Label(new Rect(bx + bw - 64f, by, 64f, 16f), valueText, _styleValueRight);
            DrawFlatRect(bx, by + 18f, bw, 14f, ColorBarBg);
            DrawFlatRect(bx, by + 18f, bw * Mathf.Clamp01(ratio), 14f, fill);
            return by + 46f;
        }

        /// <summary>국적색 원형 아바타 폴백용 96px 원형 텍스처 (지연 1회 생성 — GuardSquadHotbar 원형 스프라이트 동일 수식).</summary>
        private static void EnsureCircleTex()
        {
            if (_circleTex != null) return;
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = size * 0.5f;
            float radius = size * 0.5f - 1f; // 1px 여백 (클램프 블리딩 방지)
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float dx = px + 0.5f - center;
                    float dy = py + 0.5f - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f); // 1px 안티앨리어싱
                    tex.SetPixel(px, py, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _circleTex = tex;
        }

        // ===== GuardIconRenderer 리플렉션 캐시 (asmdef 경계 — Systems는 UI 직접 참조 불가, Core→Systems TryGetBedSpawnPoint 선례) =====
        private static System.Type _guardIconRendererType;
        private static System.Reflection.MethodInfo _getOrCreateIconMethod;

        /// <summary>
        /// GuardIconRenderer.GetOrCreateIcon(g)를 리플렉션으로 호출한다 (asmdef 경계 — 정규화 직접 참조 대체).
        /// Type/MethodInfo는 static 캐시로 GC 최소화. 실패 시 null (국적색 원형 폴백).
        /// </summary>
        private static Texture2D TryGetGuardIcon(GuardPlaceholder g)
        {
            try
            {
                if (_getOrCreateIconMethod == null)
                {
                    _guardIconRendererType = System.Type.GetType("ProjectName.UI.GuardIconRenderer, ProjectName.UI");
                    if (_guardIconRendererType == null) return null;
                    _getOrCreateIconMethod = _guardIconRendererType.GetMethod("GetOrCreateIcon",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (_getOrCreateIconMethod == null) return null;
                }
                return _getOrCreateIconMethod.Invoke(null, new object[] { g }) as Texture2D;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GuardPlaceholder] GuardIconRenderer 리플렉션 호출 실패: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 실제 3D 아이콘을 0.5초 폴링으로 재조회 (GuardSquadHotbar 동일 패턴).
        /// GetOrCreateIcon은 캐시 히트 시 즉시 반환, 미베이크 시 큐 등록 후 null.
        /// </summary>
        private void EnsureAvatarIcon()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _avatarNextPoll) return;
            _avatarNextPoll = now + 0.5f;
            try
            {
                _avatarIcon = TryGetGuardIcon(this);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GuardPlaceholder] 실제 아이콘 조회 실패 — 국적색 원형 폴백: " + e.Message);
                _avatarIcon = null;
            }
        }

        /// <summary>국적 → 아바타 색상 (동=빨강, 서=파랑, 남=초록, 북=보라, 황제국/무소속/기타=회색 — GuardSquadHotbar 동일).</summary>
        private static Color GetNationColor(string nationName)
        {
            if (string.IsNullOrEmpty(nationName)) return ColorNationDefault;
            if (nationName.Contains("동")) return ColorNationEast;
            if (nationName.Contains("서")) return ColorNationWest;
            if (nationName.Contains("남")) return ColorNationSouth;
            if (nationName.Contains("북")) return ColorNationNorth;
            return ColorNationDefault;
        }

        /// <summary>이름 첫 글자(이니셜) — 서러게이트 쌍은 2 코드유닛으로 안전 절단, 빈 이름은 "?".</summary>
        private static string GetInitial(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            int len = (char.IsSurrogate(name[0]) && name.Length >= 2) ? 2 : 1;
            return name.Substring(0, len);
        }

        /// <summary>플랫 버튼 — ColorKeyBadgeBg 배경 + 회백 1px 테두리 + 호버 틴트 (클릭 로직은 호출부 기존 그대로).</summary>
        private bool FlatButton(Rect r, string text)
        {
            bool hovered = r.Contains(Event.current.mousePosition);
            DrawFlatRect(r.x, r.y, r.width, r.height, hovered ? ColorBtnHover : ColorKeyBadgeBg);
            DrawFlatRect(r.x, r.y, r.width, 1f, ColorPanelBorder);
            DrawFlatRect(r.x, r.y + r.height - 1f, r.width, 1f, ColorPanelBorder);
            DrawFlatRect(r.x, r.y, 1f, r.height, ColorPanelBorder);
            DrawFlatRect(r.x + r.width - 1f, r.y, 1f, r.height, ColorPanelBorder);
            GUI.Label(r, text, _styleBtn);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        private void OnTalk()
        {
            _statusMessage = guardName + ": \\\"무슨 일이냐?\\\"";
        }

        // ===== C9-15: 포섭 =====
        private void OnRecruit()
        {
            if (_isRecruited)
            {
                _statusMessage = $"{guardName}: \\\"이미 영지에 소속되어 있네.\\\"";
                return;
            }

            var result = GuardRecruitSystem.AttemptRecruit(this);
            if (result.success)
            {
                _isRecruited = true;
                _statusMessage = result.message;
            }
            else
            {
                _statusMessage = result.message;
            }
        }

        // ===== GUI 스타일 캐시 (static 1회 생성 — OnGUI 내 new 금지) =====
        private static GUIStyle _styleTitle;         // 타이틀 (흰색 24px)
        private static GUIStyle _styleLabel;         // 바 캡션 (14px)
        private static GUIStyle _styleValue;         // 수치 (14px 볼드)
        private static GUIStyle _styleValueRight;    // 수치 (우측정렬)
        private static GUIStyle _styleMsg;           // 상태 메시지 (13px 이탤릭)
        private static GUIStyle _styleBadge;         // [E] 배지 (13px)
        private static GUIStyle _styleBtn;           // 메뉴 버튼 (14px)
        private static GUIStyle _styleAvatarInitial; // 아바타 이니셜 (34px)

        private static void EnsureStyles()
        {
            if (_styleTitle != null) return;
            _styleTitle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _styleLabel = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            _styleValue = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };
            _styleValueRight = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, normal = { textColor = Color.yellow } };
            _styleMsg = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Italic, normal = { textColor = Color.cyan } };
            _styleBadge = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _styleBtn = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _styleAvatarInitial = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        // ===== IDamageable =====
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsDead => _isDead;
        public bool IsAlive => !_isDead;

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            if (_isDead) return;
            _currentHP -= amount;

            // Phase 3: 통합 피격 VFX (Organic, 흰색 데미지 숫자)
            CombatFXGate.PlayHitFX(gameObject, hitDirection, CombatHitType.Organic, false, amount, Color.white);
            Debug.Log($"[GuardPlaceholder] HitFX played (dmg={amount}, hp={_currentHP})");

            if (_currentHP <= 0) Die();
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            TakeDamage(damageInfo.amount, damageInfo.knockback.normalized, "melee");
        }

        private void Die()
        {
            if (_isDead) return;
            // ⏱️ 전투 로그: 병사 처치 기록
            CombatLog.AddEntry($"{guardName} 처치!", LogType.Kill);

            // 경험치: 병사 레벨 기반 (level × 5 × 난수 0.8~1.2)
            int exp = Mathf.Max(1, Mathf.RoundToInt(level * 5f * Random.Range(0.8f, 1.2f)));
            PlayerStats.Instance?.AddEXP(exp);
            CombatLog.AddEntry($"병사 처치 경험치 +{exp}", LogType.Kill);
            Debug.Log($"[GuardPlaceholder] 병사 처치 경험치 +{exp} (Lv.{level} × 5 × 난수 0.8~1.2)");
            _isDead = true;

            // Phase 3: 사망 카메라 연출 (킬 이펙트)
            CombatCameraEffects.PlayKill();

            // 사망 애니메이션 (Idle 즉시 적용)
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // 사망 이벤트 발생 (GuardResurrectionSystem 등에서 구독)
            OnAnyGuardDied?.Invoke(this);

            // GuardManager에서 제거
            if (GuardManager.Instance != null)
            {
                GuardManager.Instance.OnGuardDiedInGame(this);
            }

            // ===== 전리품/드랍 처리 (try-catch 격리 — 드랍 실패가 사망 로직을 중단하지 않도록) =====
            try
            {
                LootBasket basket = LootBasket.Create(transform.position);
                // DropTableManager.Instance null 가드 (NRE 방지 — null이면 아래 폴백 골드 블록 실행)
                DropTable dropTable = DropTableManager.Instance != null ? DropTableManager.Instance.GetSoldierTable() : null;
                if (dropTable != null)
                    dropTable.ApplyToBasket(basket, MonsterLevelManager.Instance?.GetDropRateBonus(level) ?? 0f);
                else
                {
                    PlayerInventory.ItemData goldItem = new PlayerInventory.ItemData
                    {
                        id = "gold", displayName = "금", description = "통화",
                        category = PlayerInventory.ItemCategory.Material, maxStack = 99
                    };
                    basket.AddItem(goldItem, level * 10);
                    if (Random.value < 0.5f) basket.AddItem(PlayerInventory.RabbitFur, 1);
                }

                // ===== 장착 장비 전리품 드랍 (무기/방패/투구/갑옷) =====
                // 장비는 항상 100% 드랍하며, '실제 장착된'(null이 아닌) 아이템만 떨어뜨린다.
                // 빈 슬롯은 위의 SoldierDropTable/폴백 드랍에 그대로 맡긴다.
                DropEquippedItems(basket);

                // ===== 사망 드랍 최소 보장 =====
                // 장비 + 드롭 테이블(및 폴백) 모두로 바구니가 비었으면 최소 1개(금)는 반드시 떨어뜨린다.
                if (basket.IsEmpty)
                {
                    basket.AddItem(PlayerInventory.Gold, 1);
                    Debug.Log($"[GuardPlaceholder] {guardName} 죽: 드랍 최소 보장 — 금 1개 전리품 드랍");
                }
            }
            catch (System.Exception ex)
            {
                // 드랍 실패가 사망 처리(비활성화/부활 시스템)를 중단하지 않도록 격리
                Debug.LogWarning($"[GuardPlaceholder] 드랍 처리 실패(사망 자체는 계속): {ex.Message}");
            }

            // 비활성화 (Destroy 대신 — GuardResurrectionSystem에서 부활 가능)
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 장착된 장비 슬롯(무기/방패/투구/갑옷)을 전리품 바구니에 드랍합니다.
        /// null이 아닌(장착된) 아이템만 1개씩 드랍하며, 각 드랍 시 한국어 로그를 남깁니다.
        /// </summary>
        private void DropEquippedItems(LootBasket basket)
        {
            if (basket == null) return;
            DropEquippedSlot(basket, WeaponItem);
            DropEquippedSlot(basket, ShieldItem);
            DropEquippedSlot(basket, HelmetItem);
            DropEquippedSlot(basket, ArmorItem);
        }

        /// <summary>단일 장비 슬롯 드랍 처리 (null이 아니면 100% 드랍)</summary>
        private void DropEquippedSlot(LootBasket basket, PlayerInventory.ItemData item)
        {
            if (item == null) return; // 빈 슬롯: 기존 SoldierDropTable 폴백 드랍에 맡긴다
            basket.AddItem(item, 1);
            Debug.Log($"[GuardPlaceholder] {guardName} 죽: {item.displayName} 전리품 드랍");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactRange);

            // Phase 34: 시야 원뿔 (Gizmos)
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Vector3 forward = transform.forward;
            float halfFOV = _fieldOfView * 0.5f;
            Vector3 leftDir = Quaternion.Euler(0, -halfFOV, 0) * forward;
            Vector3 rightDir = Quaternion.Euler(0, halfFOV, 0) * forward;
            Gizmos.DrawLine(transform.position, transform.position + leftDir * _sightRange);
            Gizmos.DrawLine(transform.position, transform.position + rightDir * _sightRange);
        }

        // ===== Phase 34: NPCAwareness 연동 =====
        /// <summary>
        /// NPC 시야각 120° 기반 플레이어 감지 및 NPCAwarenessSystem 상태 업데이트.
        /// </summary>
        private void UpdateAwareness(GameObject player, float distance)
        {
            if (_isDead || _awareness == null) return;
            if (player == null) return;

            // 사망 시 강제 평화 상태
            if (!gameObject.activeInHierarchy)
            {
                _awareness.ForcePeace();
                return;
            }

            // 플레이어가 시야 범위 내에 있는가
            if (distance > _sightRange)
                return; // 너무 멀면 체크 불필요

            // 시야 방향 계산
            Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            // 시야각 120° (절반 60°)
            bool inSightCone = angle < (_fieldOfView * 0.5f);

            if (inSightCone)
            {
                // Raycast로 시야 차단 확인
                if (!Physics.Raycast(transform.position + Vector3.up * 1.5f, dirToPlayer, out RaycastHit hit, distance))
                {
                    // 플레이어 발견 → Detected
                    if (_awareness.CurrentAwarenessState != NPCAwarenessSystem.AwarenessState.Detected)
                    {
                        _awareness.SetDetected(player);
                        SetInCombat(true);
                    }
                }
                else
                {
                    // 차단된 오브젝트가 플레이어 본인인지 확인
                    if (hit.collider.gameObject == player)
                    {
                        if (_awareness.CurrentAwarenessState != NPCAwarenessSystem.AwarenessState.Detected)
                        {
                            _awareness.SetDetected(player);
                            SetInCombat(true);
                        }
                    }
                    else
                    {
                        // 장애물 뒤 — Suspicious
                        _awareness.SetSuspicious(player.transform.position);
                    }
                }
            }
            else
            {
                // 시야 밖 — 은신 상태 체크할 필요 없음 (NPCAwarenessSystem 자체 처리)
            }
        }

        /// <summary>
        /// 은신 상태 + NPC 뒤에서 좌클릭 시 StealthAssassination.TryAssassinate 호출.
        /// </summary>
        private void TryAssassinateGuard(GameObject player, float distance)
        {
            if (_isDead) return;
            if (player == null) return;

            // 암살 시스템 확인
            if (StealthAssassination.Instance == null) return;
            if (StealthAssassination.IsPerformingAssassination) return;

            // 은신 상태 확인
            if (StealthSystem.Instance == null || !StealthSystem.Instance.IsStealthed) return;

            // 거리 체크 (암살 가능 거리)
            if (distance > 2.5f) return;

            // 좌클릭 감지
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                // NPC 뒤에서만 암살 가능
                Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, dirToPlayer);
                // dot < -0.3 = 뒤쪽 (≈ 120° 범위)
                if (dot < -0.3f)
                {
                    // 암살 시도
                    bool success = StealthAssassination.Instance.TryAssassinate(gameObject);
                    if (success)
                    {
                        // 구독자에게 암살 알림
                        Debug.Log($"[GuardPlaceholder] {guardName} 암살당함!");
                    }
                }
            }
        }

        // ===== 퍼블릭 API =====
        public string GuardName => guardName;
        public int Level => level;
        public string Nation => nation;
        public string JobTitle { get => jobTitle; set => jobTitle = value; }
        public float HP => _currentHP;
        public float Loyalty { get => _loyalty; set => _loyalty = Mathf.Clamp(value, -100, 100); }
        public float Addiction { get => _addiction; set => _addiction = Mathf.Clamp(value, 0, GuardAddictionSystem.MAX_ADDICTION); }
        public bool IsPlayerNearby => _playerNearby;
        public bool IsShowingInfo => _showInfo;
        public bool IsSelectingItem => _selectionMode != SelectionMode.None;

        // ===== IWorldSpaceHUD 구현 =====
        public Vector3 WorldPosition => transform.position + Vector3.up * 2.5f; // 머리 위
        public bool ShouldShowHUD => !_isDead && gameObject.activeInHierarchy;
        public int HUDLevel => level;
        public float HUDLoyalty => _loyalty;
        public float HUDAddiction => _addiction;
        public string HUDName => guardName;

        // ===== C9-20: RTS =====
        [Header("RTS 이동")]
        [SerializeField] private float _moveSpeed = 3f;
        private bool _isSelected = false;
        private Vector3 _commandTargetPos;
        private bool _hasCommand = false;
        private bool _isAttackCommand = false;

        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            // Phase 41-2: Selection Outline 표시/제거
            if (SpecialEffectsController.Instance != null)
            {
                if (selected)
                    SpecialEffectsController.Instance.AddSelectionOutline(this);
                else
                    SpecialEffectsController.Instance.RemoveSelectionOutline(this);
            }
        }
        public bool IsSelected => _isSelected;
        public void SetCommandTarget(Vector3 t, bool a) { _commandTargetPos = t; _isAttackCommand = a; _hasCommand = true; }
        public void ClearCommand() { _hasCommand = false; _isAttackCommand = false; }
        public bool HasCommand => _hasCommand;
        public Vector3 CommandTarget => _commandTargetPos;
        public bool IsAttackCommand => _isAttackCommand;

        // ===== C9-20/C9-21: 명령 실행 루프 =====
        private const float MOVE_CLEAR_RADIUS = 1.0f;       // 이동 명령 해제 반경(m)
        private const float MOVE_STOP_RADIUS = 0.6f;        // 이동 정지 판정 반경(m) — 이내 접근 금지(목표 넘어감 방지)
        private const float ATTACK_ARRIVE_RADIUS = 1.5f;    // 공격 명령 도달 반경(m) — 공격 모션 개시
        private const float ATTACK_MELEE_RANGE = 2.2f;      // 근접 공격 유효 거리(m)
        private const float ATTACK_COOLDOWN_SECONDS = 1.2f; // 공격 쿨다운(초)
        private const float ROTATION_SPEED = 8f;            // 회전 보간 속도 (Slerp 계수)
        private const float TARGET_SEARCH_RADIUS = 2.5f;    // 명령 지점 주변 적 탐색 반경(m)

        private Component _attackTarget;                    // 공격 명령 대상 (IDamageable 구현 컴포넌트 캐시)
        private float _attackCooldown = 0f;                 // 공격 쿨다운 잔여 시간(초)
        private HumanoidClipDriver _clipDriver;             // 공격 모션 드라이버 (지연 캐싱)
        private Rigidbody _rb;                              // Rigidbody (없으면 transform 직접 이동)

        /// <summary>
        /// C9-20/C9-21: 명령 실행 루프 — RTS/전투 명령을 실제 이동·공격으로 수행한다.
        /// GuardPlaceholder는 Rigidbody 없는 단순 생성 프리팹(cube)이므로 transform 이동을 허용하되,
        /// Rigidbody가 존재하면 MovePosition으로 우회한다. 사망 시 어떤 행동도 하지 않는다.
        /// </summary>
        private void ExecuteMovement()
        {
            if (_isDead || !_hasCommand) return;

            float delta = Time.deltaTime;

            // 공격 쿨다운 감소
            if (_attackCooldown > 0f) _attackCooldown -= delta;

            Vector3 current = transform.position;
            Vector3 target = _commandTargetPos;

            // 수평(XZ) 거리 기준 판정 — y는 지형 보정에 따라 흔들리므로 판정에서 제외
            Vector3 toTarget = target - current; toTarget.y = 0f;
            float distXZ = toTarget.magnitude;

            if (_isAttackCommand)
            {
                // ----- 공격 명령: 목표지점 도달(1.5m) 시 공격 모션 + 근접 데미지 -----
                if (!ValidateAttackTarget())
                {
                    // 대상 재탐색 (명령 지점 주변 유효 적)
                    ResolveAttackTarget();
                    if (_attackTarget == null)
                    {
                        // 대상이 죽었거나 유효한 적이 없으면 명령 해제
                        Debug.Log($"[GuardPlaceholder] {guardName} 공격 대상 상실 → 명령 해제");
                        ClearCommand();
                        return;
                    }
                }

                if (distXZ > ATTACK_ARRIVE_RADIUS)
                {
                    // 미도달 → 목표지점으로 이동
                    StepToward(current, target, distXZ, delta);
                }
                else
                {
                    // 도달 → 대상 방향 회전 후 쿨다운 게이트 공격
                    FaceToward(_attackTarget.transform.position - current, delta);

                    Vector3 dirToTarget = _attackTarget.transform.position - current; dirToTarget.y = 0f;
                    if (_attackCooldown <= 0f && dirToTarget.magnitude <= ATTACK_MELEE_RANGE)
                        PerformAttack((IDamageable)_attackTarget);
                }
            }
            else
            {
                // ----- 이동 명령: 도달 반경 1.0m 진입 시 명령 해제 -----
                if (distXZ <= MOVE_CLEAR_RADIUS)
                {
                    ClearCommand();
                    if (_rigAnim != null) _rigAnim.SetState(AnimationState.Idle);
                }
                else
                {
                    StepToward(current, target, distXZ, delta);
                }
            }
        }

        /// <summary>목표를 향해 1프레임 이동 (속도 * delta, 목표 넘어감 방지 클램프 + 지형 y 보정 + 회전).</summary>
        private void StepToward(Vector3 current, Vector3 target, float distXZ, float delta)
        {
            Vector3 dirXZ = target - current; dirXZ.y = 0f;
            if (dirXZ.sqrMagnitude < 0.0001f) return;
            dirXZ.Normalize();

            // 이동량 = _moveSpeed * delta, 목표를 넘어가지 않도록 클램프 (정지 반경 0.6m 유지)
            float step = Mathf.Min(_moveSpeed * delta, Mathf.Max(0f, distXZ - MOVE_STOP_RADIUS));
            if (step <= 0f) return;

            Vector3 next = current + dirXZ * step;
            // 지형 계약: 이동 y는 표면(1 + GetHeightAt)으로 보정 (FarmPlot.TryGetSurfaceY와 동일 수식)
            next.y = TryGetGroundY(next.x, next.z, current.y);

            // Rigidbody가 있으면 MovePosition, 없으면 transform 직접 이동
            if (_rb != null && !_rb.isKinematic) _rb.MovePosition(next);
            else transform.position = next;

            // 이동 방향으로 회전 (Slerp 8f * delta)
            FaceToward(dirXZ, delta);

            // 이동 애니메이션 (SetState는 동일 상태 재호출 시 early-return 하므로 매 프레임 호출 안전)
            if (_rigAnim != null) _rigAnim.SetState(AnimationState.Walk);
        }

        /// <summary>수평 방향으로 부드럽게 회전 (Quaternion.Slerp, ROTATION_SPEED * delta).</summary>
        private void FaceToward(Vector3 dirXZ, float delta)
        {
            dirXZ.y = 0f;
            if (dirXZ.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(dirXZ.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, ROTATION_SPEED * delta);
        }

        /// <summary>
        /// 공격 모션 트리거(HumanoidClipDriver 우선, 없으면 RigAnimationController 폴백)
        /// + 근접 데미지 적용 (공격력 = level * 1.5f, 쿨다운 1.2s).
        /// </summary>
        private void PerformAttack(IDamageable target)
        {
            if (_clipDriver == null) _clipDriver = GetComponent<HumanoidClipDriver>();
            if (_clipDriver != null) _clipDriver.TriggerAttack();
            else if (_rigAnim != null) _rigAnim.SetState(AnimationState.Attack);

            // 데미지 적용 (대상은 ValidateAttackTarget에서 유효성 검증 완료 상태)
            float damage = level * 1.5f;
            Vector3 dir = transform.forward;
            if (_attackTarget is Component at)
            {
                Vector3 toTarget = at.transform.position - transform.position; toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f) dir = toTarget.normalized;
            }

            target.TakeDamage(damage, dir, "melee");
            _attackCooldown = ATTACK_COOLDOWN_SECONDS;

            string targetName = (_attackTarget as Component) != null ? (_attackTarget as Component).name : "?";
            Debug.Log($"[GuardPlaceholder] {guardName} 근접 공격! 대상={targetName} dmg={damage:F1}");
        }

        /// <summary>
        /// 공격 대상 유효성 검사 — 살아있는 적 IDamageable만 허용.
        /// 자기 자신, 다른 병사(GuardPlaceholder), 플레이어는 공격 금지.
        /// </summary>
        private bool ValidateAttackTarget()
        {
            if (_attackTarget == null) return false;

            var dmg = _attackTarget as IDamageable;
            if (dmg == null || !dmg.IsAlive)
            {
                _attackTarget = null;
                return false;
            }

            GameObject go = _attackTarget.gameObject;
            // 자기 자신 또는 다른 병사(GuardPlaceholder)는 공격 금지
            if (go == gameObject || go.GetComponentInParent<GuardPlaceholder>() != null)
            {
                _attackTarget = null;
                return false;
            }
            // 플레이어(및 플레이어 하위 오브젝트)는 공격 금지
            if (_playerCache != null && go.transform.IsChildOf(_playerCache.transform))
            {
                _attackTarget = null;
                return false;
            }
            return true;
        }

        /// <summary>명령 지점 주변(TARGET_SEARCH_RADIUS)에서 가장 가까운 유효한 적 IDamageable을 탐색해 캐싱한다.</summary>
        private void ResolveAttackTarget()
        {
            _attackTarget = null;

            Collider[] hits = Physics.OverlapSphere(_commandTargetPos, TARGET_SEARCH_RADIUS);
            float bestDist = float.MaxValue;
            Component bestComp = null;

            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var dmg = hit.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;

                Component comp = dmg as Component;
                if (comp == null) continue;

                GameObject go = comp.gameObject;
                if (go == gameObject || go.GetComponentInParent<GuardPlaceholder>() != null) continue; // 자기 자신/병사 제외
                if (_playerCache != null && go.transform.IsChildOf(_playerCache.transform)) continue;  // 플레이어 제외

                float d = Vector3.Distance(_commandTargetPos, comp.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestComp = comp;
                }
            }

            _attackTarget = bestComp;
        }

        /// <summary>
        /// 지형 계약 (FarmPlot.TryGetSurfaceY와 동일 수식): 월드 표면 y = 1 + GetHeightAt(x, z, Plains, 42).
        /// TerrainGenerator 미초기화 등 예외 시 fallbackY(현재 y)를 유지해 텔레포트를 방지한다.
        /// </summary>
        private static float TryGetGroundY(float x, float z, float fallbackY)
        {
            try { return 1f + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42); }
            catch { return fallbackY; }
        }

        // ===== C9-21: 전투 AI =====
        private bool _isInCombat = false;
        private float _combatTimer = 0f;

        public void SetInCombat(bool combat) { _isInCombat = combat; _combatTimer = 0f; if (_rigAnim != null) _rigAnim.SetState(combat ? AnimationState.Attack : AnimationState.Idle); }
        public bool IsInCombat => _isInCombat;
        public float CombatTimer => _combatTimer;
        public void UpdateCombatTimer(float delta) { if (_isInCombat) _combatTimer += delta; }
        public void ResetCombatTimer() { _combatTimer = 0f; }
        public bool IsRecruited => _isRecruited;
        public GuardRole Role { get => _role; set => _role = value; }
        public string StatusSummary => GuardStatusSystem.GetStatusSummary(this);

        /// <summary>포섭 상태 설정 (GuardManager 등에서 호출)</summary>
        public void SetRecruited(bool recruited) { _isRecruited = recruited; }

        /// <summary>체력 직접 설정 (GuardManager 부활/회복)</summary>
        public void SetHP(float hp) { _currentHP = Mathf.Clamp(hp, 0, _maxHP); }

        /// <summary>부활 처리 (GuardResurrectionSystem에서 호출)</summary>
        public void Resurrect(float hpPercent = 0.1f)
        {
            _isDead = false;
            _currentHP = _maxHP * Mathf.Clamp01(hpPercent);
            gameObject.SetActive(true);
            _showInfo = false;
            _selectionMode = SelectionMode.None;
        }

        // ===== 장비 슬롯 (WeaponPartsSystem 연동) =====
        public PlayerInventory.ItemData WeaponItem { get; set; }
        public PlayerInventory.ItemData ShieldItem { get; set; }
        public PlayerInventory.ItemData HelmetItem { get; set; }
        public PlayerInventory.ItemData ArmorItem { get; set; }

        /// <summary>장비 외형 업데이트 (WeaponPartsSystem 연동)</summary>
        public void UpdateVisual() { }

        /// <summary>리스폰 처리 (TerritoryBattleManager 연동)</summary>
        public void Respawn() { Resurrect(0.1f); }
    }
}