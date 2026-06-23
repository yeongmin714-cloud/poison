using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// C9-19: 장비 수리 스테이션 UI (IMGUI)
    /// 크래프트 테이블 근접 시 'R'키로 열리는 수리 창.
    /// 파손된 장비 목록을 표시하고 골드 소모 수리를 진행합니다.
    /// </summary>
    public class RepairStationUI : UIWindow
    {
        protected virtual void Start()
        {
            ApplyTheme(Phase33_Themes.CreateRepairTheme());
        }

        [Header("Repair Station Settings")]
        [SerializeField] private int _windowWidth = 1395
        [SerializeField] private int _windowHeight = 1125

        // ── 상태 ──
        private string _statusMessage = "";
        private Color _statusColor = Color.white;
        private float _statusTimer = 0f;
        private Vector2 _scrollPosition;

        // ── 스타일 ──
        private GUIStyle _titleStyle;
        private GUIStyle _itemRowStyle;
        private GUIStyle _repairButtonStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _categoryHeaderStyle;
        private bool _stylesInitialized;

        protected override void OnShow()
        {
            base.OnShow();
            _statusMessage = "";
            _stylesInitialized = false;
            Debug.Log("[RepairStationUI] 수리 스테이션 열림");
        }

        protected override void OnHide()
        {
            base.OnHide();
            Debug.Log("[RepairStationUI] 수리 스테이션 닫힘");
        }

        /// <summary>
        /// 외부에서 강제로 열기 (CraftingStation에서 호출)
        /// </summary>
        public void Open()
        {
            if (!_isOpen)
                Show();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 72,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _itemRowStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f, 0.9f)) }
            };

            _repairButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.2f, 0.6f, 0.2f, 1f)) },
                hover = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.3f, 0.8f, 0.3f, 1f)) }
            };

            _statusStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 52,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.15f, 0.15f, 0.15f, 0.85f)) }
            };

            _categoryHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _stylesInitialized = true;
        }

        private Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (!_isOpen) return;
            InitializeStyles();

            // 배경 딤드
            Rect dimRect = new Rect(0, 0, Screen.width, Screen.height);
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(dimRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 메인 윈도우 영역 (중앙)
            float x = (Screen.width - _windowWidth) / 2f;
            float y = (Screen.height - _windowHeight) / 2f;
            Rect windowRect = new Rect(x, y, _windowWidth, _windowHeight);

            GUILayout.BeginArea(windowRect, GUI.skin.box);

            // ── 제목 표시줄 ──
            GUILayout.BeginHorizontal();
            GUILayout.Label("  🔧 장비 수리 스테이션", _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기 X", GUILayout.Width(90), GUILayout.Height(36)))
            {
                Hide();
                return;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ── 골드 보유량 표시 ──
            int playerGold = 0;
            if (PlayerInventory.Instance != null)
                playerGold = PlayerInventory.Instance.GetItemCount("gold");
            GUILayout.Label($"💰 보유 골드: {playerGold}G", _categoryHeaderStyle);

            GUILayout.Space(4);

            // ── 수리 상태 메시지 ──
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                var msgStyle = new GUIStyle(_statusStyle)
                {
                    normal = { textColor = _statusColor }
                };
                GUILayout.Box(_statusMessage, msgStyle, GUILayout.Height(54));
                GUILayout.Space(4);
            }
            else
            {
                GUILayout.Box("파손된 장비를 선택하여 수리하세요.", GUILayout.Height(42));
                GUILayout.Space(2);
            }

            // ── 구분선 ──
            GUILayout.Label("─── 파손된 장비 목록 ───", _categoryHeaderStyle);

            // ── 장비 목록 (스크롤) ──
            float listHeight = _windowHeight - 150;
            if (listHeight < 60) listHeight = 60;

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(listHeight));

            var inventory = PlayerInventory.Instance;
            if (inventory != null)
            {
                var damagedItems = GetDamagedEquipment(inventory);
                if (damagedItems.Count == 0)
                {
                    GUILayout.Label("  수리할 장비가 없습니다.", _categoryHeaderStyle);
                }
                else
                {
                    foreach (var entry in damagedItems)
                    {
                        DrawRepairItemRow(entry, playerGold);
                        GUILayout.Space(2);
                    }
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(4);

            // ── 안내 문구 ──
            GUILayout.Label("R 키를 다시 누르면 창이 닫힙니다.", new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            });

            GUILayout.EndArea();
        }

        /// <summary>
        /// 파손된 장비 목록을 가져옵니다.
        /// </summary>
        private List<DamagedEquipmentEntry> GetDamagedEquipment(PlayerInventory inventory)
        {
            var list = new List<DamagedEquipmentEntry>();
            var slots = inventory.GetAllSlots();

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.item == null || slot.count <= 0)
                    continue;

                // 수리 가능한 장비만
                if (!EquipmentRepairSystem.CanRepair(slot.item.id))
                    continue;

                // 내구도가 손상된 경우만 (완전 충전은 제외)
                if (slot.currentDurability >= slot.item.maxDurability)
                    continue;
                if (slot.item.maxDurability <= 0)
                    continue;

                int cost = EquipmentRepairSystem.GetRepairCost(slot);
                list.Add(new DamagedEquipmentEntry
                {
                    slotIndex = i,
                    itemData = slot.item,
                    currentDurability = slot.currentDurability,
                    maxDurability = slot.item.maxDurability,
                    repairCost = cost
                });
            }

            return list;
        }

        /// <summary>
        /// 장비 수리 행 하나를 그립니다.
        /// </summary>
        private void DrawRepairItemRow(DamagedEquipmentEntry entry, int playerGold)
        {
            GUILayout.BeginHorizontal(_itemRowStyle, GUILayout.Height(66));

            // ── 장비 정보 ──
            float duraRatio = (float)entry.currentDurability / entry.maxDurability;
            string duraColor = duraRatio <= 0.25f ? "🔴" : (duraRatio <= 0.5f ? "🟡" : "🟢");
            string itemInfo = $"{duraColor} {entry.itemData.displayName}  |  내구도: {entry.currentDurability}/{entry.maxDurability}";

            GUILayout.Label(itemInfo, GUILayout.Width(360), GUILayout.Height(60));

            GUILayout.FlexibleSpace();

            // ── 수리비 ──
            GUILayout.Label($"💰 {entry.repairCost}G", GUILayout.Width(90), GUILayout.Height(60));

            // ── 수리 버튼 ──
            bool canAfford = playerGold >= entry.repairCost;
            Color originalColor = GUI.color;

            if (!canAfford)
            {
                GUI.color = Color.red;
                GUI.enabled = false;
            }

            string btnLabel = canAfford ? "수리" : "골드부족";
            if (GUILayout.Button(btnLabel, GUILayout.Width(105), GUILayout.Height(48)))
            {
                TryRepairSlot(entry.slotIndex);
            }

            GUI.color = originalColor;
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 특정 슬롯의 장비 수리 시도
        /// </summary>
        private void TryRepairSlot(int slotIndex)
        {
            var result = EquipmentRepairSystem.RepairInventorySlot(slotIndex);
            _statusMessage = result.message;
            _statusColor = result.success ? Color.green : Color.red;

            if (result.success)
            {
                Debug.Log($"[RepairStationUI] {result.message}");
                // 사운드
                SoundManager.Instance?.PlaySFX("craft_success");
            }
            else
            {
                Debug.Log($"[RepairStationUI] 수리 실패: {result.message}");
                SoundManager.Instance?.PlaySFX("craft_fail");
            }
        }

        /// <summary>
        /// 파손된 장비 항목 데이터
        /// </summary>
        private struct DamagedEquipmentEntry
        {
            public int slotIndex;
            public PlayerInventory.ItemData itemData;
            public int currentDurability;
            public int maxDurability;
            public int repairCost;
        }
    }
}