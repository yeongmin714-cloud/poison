using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    /// <summary>
    /// 병사Placeholder - 사장님이 GLB를 제공하기 전까지 사용할 임시 병사 모델.
    /// C9-08: E키 상호작용 → 병사 정보 HUD 표시 + 메뉴 (말걸기/음식주기/약주기)
    /// C9-11: 음식/약주기 — 인벤토리 선택 → 아이템 지급 → 호감도/중독도 변화
    /// </summary>
    public class GuardPlaceholder : MonoBehaviour, IDamageable
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

        private enum SelectionMode { None, SelectingFood, SelectingDrug }
        private SelectionMode _selectionMode = SelectionMode.None;

        private bool _playerNearby = false;
        private bool _showInfo = false;
        private Vector2 _scrollPos;
        private string _statusMessage = "";
        private Vector2 _invScrollPos;

        private const float HP_BAR_WIDTH = 150f;
        private const float LABEL_WIDTH = 80f;

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
        }

        private void Update()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
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
                    GUI.Label(new Rect(10, iy + 2, 180, 20), pair.item.displayName, _styleLabel);
                    GUI.Label(new Rect(10, iy + 20, 80, 16), $"x{pair.count}", _styleValue);

                    if (GUI.Button(new Rect(popupW - 160, iy + 5, 100, 28), "주기"))
                    {
                        GiveItemToGuard(pair.item);
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
                    _statusMessage = $"{guardName}: \"음식 고맙다!\" ❤️ 호감도 UP";
                    break;

                case PlayerInventory.ItemCategory.Potion:
                    _currentHP = Mathf.Min(_maxHP, _currentHP + 10f);
                    GuardLoyaltySystem.GiveGift(this, 50);
                    _statusMessage = $"{guardName}: \"약을 주다니 고맙군!\" ❤️ 호감도 UP";
                    break;

                case PlayerInventory.ItemCategory.Drug:
                    GuardLoyaltySystem.GiveDrug(this, 2);
                    _statusMessage = $"{guardName}: \"어.. 뭔가 이상한 기분이...\" 💊 중독+10";
                    break;
            }
        }

        private void DrawBar(float x, float y, float width, float height, float ratio, Color fillColor, Color bgColor)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), MakeTex(1, 1, bgColor));
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(ratio), height), MakeTex(1, 1, fillColor));
        }

        private Texture2D MakeTex(int w, int h, Color c)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    tex.SetPixel(i, j, c);
            tex.Apply();
            return tex;
        }

        private void OnTalk()
        {
            _statusMessage = guardName + ": \"무슨 일이냐?\"";
        }

        // ===== C9-15: 포섭 =====
        private void OnRecruit()
        {
            if (_isRecruited)
            {
                _statusMessage = $"{guardName}: \"이미 영지에 소속되어 있네.\"";
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
            LootBasket basket = LootBasket.Create(transform.position);
            DropTable dropTable = DropTableManager.Instance.GetSoldierTable();
            if (dropTable != null)
                dropTable.ApplyToBasket(basket);
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
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }

        // ===== 퍼블릭 API =====
        public string GuardName => guardName;
        public int Level => level;
        public string Nation => nation;
        public string JobTitle { get => jobTitle; set => jobTitle = value; }
        public float HP => _currentHP;
        public float MaxHP => _maxHP;
        public float Loyalty { get => _loyalty; set => _loyalty = Mathf.Clamp(value, 0, 100); }
        public float Addiction { get => _addiction; set => _addiction = Mathf.Clamp(value, 0, 100); }
        public bool IsPlayerNearby => _playerNearby;
        public bool IsShowingInfo => _showInfo;
        public bool IsSelectingItem => _selectionMode != SelectionMode.None;

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

        public void SetInCombat(bool combat) { _isInCombat = combat; _combatTimer = 0f; }
        public bool IsInCombat => _isInCombat;
        public float CombatTimer => _combatTimer;
        public void UpdateCombatTimer(float delta) { if (_isInCombat) _combatTimer += delta; }
        public void ResetCombatTimer() { _combatTimer = 0f; }
        public bool IsRecruited => _isRecruited;
        public GuardRole Role { get => _role; set => _role = value; }
        public string StatusSummary => GuardStatusSystem.GetStatusSummary(this);
    }
}