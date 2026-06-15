using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    /// <summary>
    /// 병사Placeholder - 사장님이 GLB를 제공하기 전까지 사용할 임시 병사 모델.
    /// C9-08: E키 상호작용 → 병사 정보 HUD 표시 + 메뉴 (말걸기/음식주기/약주기)
    /// </summary>
    public class GuardPlaceholder : MonoBehaviour, IDamageable
    {
        [Header("설정")]
        [SerializeField] private string guardName = "경비병";
        [SerializeField] private int level = 1;
        [SerializeField] private string nation = "동";
        [SerializeField] private string jobTitle = "병사"; // 직업 (창병/검병/궁병)

        [Header("상호작용")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private float _maxHP = 10f;
        private float _currentHP;
        private bool _isDead = false;

        [Header("호감도/중독 (C9-08)")]
        [SerializeField] private float _loyalty = 50f;   // 0~100
        [SerializeField] private float _addiction = 0f;  // 0~100

        // 상호작용 상태
        private bool _playerNearby = false;
        private bool _showInfo = false;
        private Vector2 _scrollPos;
        private string _statusMessage = "";

        private const float HP_BAR_WIDTH = 150f;
        private const float LABEL_WIDTH = 80f;

        /// <summary>
        /// 병사 정보 설정 (TerritoryBuilder에서 호출)
        /// </summary>
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
            Debug.Log($"[GuardPlaceholder] {guardName} Lv.{level} ({nation}) 생성됨");
        }

        private void Update()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            _playerNearby = dist <= _interactRange;

            // E키로 정보 토글
            if (_playerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                _showInfo = !_showInfo;
                if (_showInfo)
                {
                    _statusMessage = $"{guardName} Lv.{level} ({nation})";
                    Debug.Log($"[GuardInteraction] {_statusMessage} — 말 걸기");
                }
            }

            // 멀어지면 자동 닫기
            if (_showInfo && dist > _interactRange * 1.5f)
            {
                _showInfo = false;
            }
        }

        // ===== IMGUI 병사 정보 HUD =====
        private void OnGUI()
        {
            if (!_showInfo || _isDead) return;

            // 화면 중앙에 정보 패널 표시
            float panelW = 320f;
            float panelH = 250f;
            float x = (Screen.width - panelW) / 2f;
            float y = Screen.height - panelH - 20f; // 하단에 표시

            // 배경 박스
            GUI.Box(new Rect(x, y, panelW, panelH), "");

            float cy = y + 10f;

            // 타이틀
            GUI.Label(new Rect(x + 10, cy, panelW - 20, 24), $"⚔️ [{nation}] {jobTitle} Lv.{level}", _styleTitle);
            cy += 30f;

            // 체력 바
            GUI.Label(new Rect(x + 10, cy, LABEL_WIDTH, 20), "❤️ 체력:", _styleLabel);
            float hpRatio = _currentHP / _maxHP;
            DrawBar(x + 10 + LABEL_WIDTH, cy, HP_BAR_WIDTH, 18, hpRatio, Color.green, Color.red);
            GUI.Label(new Rect(x + 10 + LABEL_WIDTH + HP_BAR_WIDTH + 5, cy, 60, 20),
                $"{(int)(hpRatio * 100)}%", _styleValue);
            cy += 24f;

            // 호감도 바
            GUI.Label(new Rect(x + 10, cy, LABEL_WIDTH, 20), "🤝 호감도:", _styleLabel);
            DrawBar(x + 10 + LABEL_WIDTH, cy, HP_BAR_WIDTH, 18, _loyalty / 100f, Color.blue, Color.gray);
            GUI.Label(new Rect(x + 10 + LABEL_WIDTH + HP_BAR_WIDTH + 5, cy, 60, 20),
                $"{(int)_loyalty}%", _styleValue);
            cy += 24f;

            // 중독도 바
            GUI.Label(new Rect(x + 10, cy, LABEL_WIDTH, 20), "💊 중독도:", _styleLabel);
            DrawBar(x + 10 + LABEL_WIDTH, cy, HP_BAR_WIDTH, 18, _addiction / 100f, Color.magenta, Color.gray);
            GUI.Label(new Rect(x + 10 + LABEL_WIDTH + HP_BAR_WIDTH + 5, cy, 60, 20),
                $"{(int)_addiction}%", _styleValue);
            cy += 30f;

            // 상태 메시지
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUI.Label(new Rect(x + 10, cy, panelW - 20, 20), _statusMessage, _styleMsg);
                cy += 24f;
            }

            // 메뉴 버튼들
            float btnW = (panelW - 40f) / 4f;
            float btnY = y + panelH - 40f;

            if (GUI.Button(new Rect(x + 8, btnY, btnW, 30), "🗣️ 말걸기"))
            {
                OnTalk();
            }
            if (GUI.Button(new Rect(x + 8 + btnW + 4, btnY, btnW, 30), "🥩 음식주기"))
            {
                OnGiveFood();
            }
            if (GUI.Button(new Rect(x + 8 + (btnW + 4) * 2, btnY, btnW, 30), "💊 약주기"))
            {
                OnGiveDrug();
            }
            if (GUI.Button(new Rect(x + 8 + (btnW + 4) * 3, btnY, btnW, 30), "🔙 닫기"))
            {
                _showInfo = false;
            }
        }

        private void DrawBar(float x, float y, float width, float height, float ratio, Color fillColor, Color bgColor)
        {
            // 배경
            var bgTex = MakeTex(1, 1, bgColor);
            GUI.DrawTexture(new Rect(x, y, width, height), bgTex);

            // 채움
            var fillTex = MakeTex(1, 1, fillColor);
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(ratio), height), fillTex);
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

        // ===== 메뉴 액션 =====
        private void OnTalk()
        {
            _statusMessage = $"{guardName}: \"무슨 일이냐?\"";
            Debug.Log($"[GuardInteraction] {guardName} 말걸기");
        }

        private void OnGiveFood()
        {
            // TODO: C9-11 구현 — 인벤토리에서 음식 선택 후 병사에게 지급
            _statusMessage = $"{guardName}: \"음식을 주겠다고?\" (미구현)";
            Debug.Log($"[GuardInteraction] {guardName} 음식주기 (TODO: C9-11)");
        }

        private void OnGiveDrug()
        {
            // TODO: C9-11 구현 — 인벤토리에서 약 선택 후 병사에게 지급
            _statusMessage = $"{guardName}: \"약을 주겠다고?\" (미구현)";
            Debug.Log($"[GuardInteraction] {guardName} 약주기 (TODO: C9-11)");
        }

        // ===== GUIStyle 캐시 =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleMsg;

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;
            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.yellow }
            };
            _styleMsg = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.cyan }
            };
        }

        // ===== IDamageable =====
        public bool IsAlive => !_isDead;

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            if (_isDead) return;
            _currentHP -= amount;
            Debug.Log($"[GuardPlaceholder] {guardName}가(이) {amount} 데미지! HP={_currentHP}/{_maxHP}");
            if (_currentHP <= 0) Die();
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            Debug.Log($"[GuardPlaceholder] {guardName} 사망!");

            LootBasket basket = LootBasket.Create(transform.position);
            DropTable dropTable = DropTableManager.Instance.GetSoldierTable();
            if (dropTable != null)
            {
                dropTable.ApplyToBasket(basket);
            }
            else
            {
                int goldAmount = level * 10;
                PlayerInventory.ItemData goldItem = new PlayerInventory.ItemData
                {
                    id = "gold",
                    displayName = "금",
                    description = "통화",
                    category = PlayerInventory.ItemCategory.Material,
                    maxStack = 99
                };
                basket.AddItem(goldItem, goldAmount);
                if (Random.value < 0.5f)
                    basket.AddItem(PlayerInventory.RabbitFur, 1);
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }

        // ===== 퍼블릭 API (테스트/외부 연동) =====
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
    }
}