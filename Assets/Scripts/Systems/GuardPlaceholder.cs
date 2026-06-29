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

        // 캐시된 플레이어 참조 (매 프레임 Find 방지)
        private GameObject _playerCache;

        private const float HP_BAR_WIDTH = 150f;
        private const float LABEL_WIDTH = 80f;

        private void Awake()
        {
            _rigAnim = GetComponent<RigAnimationController>();
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
            }

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

            float panelW = 320f;
            float panelH = 250f;
            float x = (Screen.width - panelW) / 2f;
            float y = Screen.height - panelH - 20f;
            float cy = y + 10f;

            GUI.Box(new Rect(x, y, panelW, panelH), "");

            // 타이틀
            string roleStr = GuardStatusSystem.GetRoleName(_role);
            GUI.Label(new Rect(x + 10, cy, panelW - 20, 24), $"⚔️ [{nation}] {roleStr} Lv.{level}", _styleTitle);
            cy += 30f;

            GUI.Label(new Rect(x + 10, cy, LABEL_WIDTH, 20), "❤️ 체력:", _styleLabel);
            float hpRatio = _currentHP / _maxHP;
            DrawBar(x + 10 + LABEL_WIDTH, cy, HP_BAR_WIDTH, 18, hpRatio, Color.green, Color.red);
            GUI.Label(new Rect(x + 10 + LABEL_WIDTH + HP_BAR_WIDTH + 5, cy, 60, 20), $"{(int)(hpRatio * 100)}%", _styleValue);
            cy += 24f;

            GUI.Label(new Rect(x + 10, cy, LABEL_WIDTH, 20), "🤝 호감도:", _styleLabel);
            DrawBar(x + 10 + LABEL_WIDTH, cy, HP_BAR_WIDTH, 18, _loyalty / 100f, Color.blue, Color.gray);
            GUI.Label(new Rect(x + 10 + LABEL_WIDTH + HP_BAR_WIDTH + 5, cy, 60, 20), $"{(int)_loyalty}%", _styleValue);
            cy += 24f;

            GUI.Label(new Rect(x + 10, cy, LABEL_WIDTH, 20), "💊 중독도:", _styleLabel);
            DrawBar(x + 10 + LABEL_WIDTH, cy, HP_BAR_WIDTH, 18, _addiction / 100f, Color.magenta, Color.gray);
            GUI.Label(new Rect(x + 10 + LABEL_WIDTH + HP_BAR_WIDTH + 5, cy, 60, 20), $"{(int)_addiction}%", _styleValue);
            cy += 30f;

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUI.Label(new Rect(x + 10, cy, panelW - 20, 20), _statusMessage, _styleMsg);
                cy += 24f;
            }
            // 메뉴 버튼들
            float btnW = (panelW - 50f) / 5f;
            float btnY = y + panelH - 40f;

            if (GUI.Button(new Rect(x + 8, btnY, btnW, 30), "🗣️ 말걸기")) OnTalk();
            if (GUI.Button(new Rect(x + 8 + btnW + 4, btnY, btnW, 30), "🥩 음식주기"))
            {
                _selectionMode = SelectionMode.SelectingFood;
                _invScrollPos = Vector2.zero;
            }
            if (GUI.Button(new Rect(x + 8 + (btnW + 4) * 2, btnY, btnW, 30), "💊 약주기"))
            {
                _selectionMode = SelectionMode.SelectingDrug;
                _invScrollPos = Vector2.zero;
            }
            if (GUI.Button(new Rect(x + 8 + (btnW + 4) * 3, btnY, btnW, 30), "🤝 포섭"))
            {
                OnRecruit();
            }
            if (GUI.Button(new Rect(x + 8 + (btnW + 4) * 4, btnY, btnW, 30), "🔙 닫기")) _showInfo = false;
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
            GUI.Label(new Rect(x + 10, y + 10, popupW - 20, 24), title, _styleTitle);

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

        private void DrawBar(float x, float y, float width, float height, float ratio, Color fillColor, Color bgColor)
        {
            if (_whitePixelTex == null)
            {
                _whitePixelTex = new Texture2D(1, 1);
                _whitePixelTex.SetPixel(0, 0, Color.white);
                _whitePixelTex.Apply();
            }

            var prevColor = GUI.color;
            GUI.color = bgColor;
            GUI.DrawTexture(new Rect(x, y, width, height), _whitePixelTex);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(ratio), height), _whitePixelTex);
            GUI.color = prevColor;
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

        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleMsg;

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;
            _styleTitle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _styleLabel = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            _styleValue = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };
            _styleMsg = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Italic, normal = { textColor = Color.cyan } };
        }

        // ===== IDamageable =====
        public bool IsAlive => !_isDead;
        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            if (_isDead) return;
            _currentHP -= amount;
            if (_currentHP <= 0) Die();
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // 사망 애니메이션 (Idle 즉시 적용)
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // 사망 이벤트 발생 (GuardResurrectionSystem 등에서 구독)
            OnAnyGuardDied?.Invoke(this);

            // GuardManager에서 제거
            if (GuardManager.Instance != null)
            {
                GuardManager.Instance.OnGuardDiedInGame(this);
            }

            LootBasket basket = LootBasket.Create(transform.position);
            DropTable dropTable = DropTableManager.Instance.GetSoldierTable();
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

            // 비활성화 (Destroy 대신 — GuardResurrectionSystem에서 부활 가능)
            gameObject.SetActive(false);
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
        public float MaxHP => _maxHP;
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
        private bool _isSelected = false;
        private Vector3 _commandTargetPos;
        private bool _hasCommand = false;
        private bool _isAttackCommand = false;

        public void SetSelected(bool selected) { _isSelected = selected; }
        public bool IsSelected => _isSelected;
        public void SetCommandTarget(Vector3 t, bool a) { _commandTargetPos = t; _isAttackCommand = a; _hasCommand = true; }
        public void ClearCommand() { _hasCommand = false; _isAttackCommand = false; }
        public bool HasCommand => _hasCommand;
        public Vector3 CommandTarget => _commandTargetPos;
        public bool IsAttackCommand => _isAttackCommand;

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