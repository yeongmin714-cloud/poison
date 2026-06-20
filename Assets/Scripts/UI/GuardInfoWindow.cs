using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 26: 병사/용병 통합 스탯창 UI (IMGUI).
    /// 
    /// 표시: 이름/등급/종류(병사/용병/바드), Lv, HP 프로그레스바, 전투력,
    ///       공격/방어/이속, 호감도(용병), 장비 슬롯(무기/방어구/액세서리), 버프 목록
    /// 
    /// 우클릭 또는 "정보" 버튼으로 열기
    /// ESC 닫기
    /// 싱글톤 GuardInfoWindow.Instance
    /// </summary>
    public class GuardInfoWindow : MonoBehaviour
    {
        public static GuardInfoWindow Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

        // ===== 표시 대상 =====
        private GuardPlaceholder _currentGuard;
        private string _currentMercenaryId;
        private bool _isMercenaryMode;

        // ===== 창 상태 =====
        private bool _isVisible = false;
        public bool IsOpen => _isVisible;
        private Vector2 _scrollPos;
        private Vector2 _buffScrollPos;

        // ===== 스타일 (lazy init) =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleSlotLabel;
        private GUIStyle _styleBuffLabel;
        private GUIStyle _styleHeader;

        // ===== 상수 =====
        private const float WINDOW_WIDTH = 420f;
        private const float WINDOW_HEIGHT = 560f;
        private const float HP_BAR_WIDTH = 180f;
        private const float SLOT_ICON_SIZE = 50f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // ESC 키로 닫기
            if (_isVisible && Input.GetKeyDown(_closeKey))
            {
                Close();
            }
        }

        // ===== 퍼블릭 API =====

        /// <summary>병사 정보창 열기</summary>
        public void OpenForGuard(GuardPlaceholder guard)
        {
            if (guard == null) return;
            _currentGuard = guard;
            _currentMercenaryId = null;
            _isMercenaryMode = false;
            _isVisible = true;
            _scrollPos = Vector2.zero;
            _buffScrollPos = Vector2.zero;
        }

        /// <summary>용병 정보창 열기</summary>
        public void OpenForMercenary(string mercenaryId)
        {
            if (string.IsNullOrEmpty(mercenaryId)) return;
            _currentGuard = null;
            _currentMercenaryId = mercenaryId;
            _isMercenaryMode = true;
            _isVisible = true;
            _scrollPos = Vector2.zero;
            _buffScrollPos = Vector2.zero;
        }

        /// <summary>정보창 닫기</summary>
        public void Close()
        {
            _isVisible = false;
            _currentGuard = null;
            _currentMercenaryId = null;
        }

        /// <summary>정보창 표시 여부</summary>
        public bool IsVisible => _isVisible;

        // ===== IMGUI =====

        private void OnGUI()
        {
            if (!_isVisible) return;

            EnsureStyles();

            float x = (Screen.width - WINDOW_WIDTH) / 2f;
            float y = (Screen.height - WINDOW_HEIGHT) / 2f;

            // 배경 상자
            GUI.Box(new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT), "");
            // 테두리
            GUI.Box(new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT), "");

            // 닫기 버튼 (우측 상단)
            if (GUI.Button(new Rect(x + WINDOW_WIDTH - 30, y + 5, 24, 24), "X"))
            {
                Close();
                return;
            }

            float cy = y + 15f;

            if (_isMercenaryMode)
            {
                DrawMercenaryInfo(x, ref cy);
            }
            else if (_currentGuard != null)
            {
                DrawGuardInfo(x, ref cy);
            }
        }

        // ===== 병사 정보 그리기 =====

        private void DrawGuardInfo(float x, ref float cy)
        {
            var guard = _currentGuard;
            if (guard == null) { Close(); return; }

            // --- 헤더: 이름/등급/종류 ---
            string guardType = GetGuardTypeLabel(guard);
            string gradeStr = GetGuardGradeString(guard);
            GUI.Label(new Rect(x + 15, cy, WINDOW_WIDTH - 30, 28),
                $"⚔️ {guard.GuardName} {gradeStr} | {guardType}", _styleTitle);
            cy += 32f;

            // --- 레벨 ---
            GUI.Label(new Rect(x + 15, cy, 80, 22), "Lv.", _styleLabel);
            GUI.Label(new Rect(x + 95, cy, 60, 22), $"{guard.Level}", _styleValue);
            string nationDisplay = !string.IsNullOrEmpty(guard.Nation) ? $" ({guard.Nation})" : "";
            GUI.Label(new Rect(x + 155, cy, 120, 22), nationDisplay, _styleLabel);
            cy += 26f;

            // --- HP 프로그레스바 ---
            GUI.Label(new Rect(x + 15, cy, 60, 22), "❤️ HP:", _styleLabel);
            float hpRatio = guard.HP / guard.MaxHP;
            DrawBar(x + 80, cy, HP_BAR_WIDTH, 20, hpRatio, Color.green, Color.red);
            GUI.Label(new Rect(x + 80 + HP_BAR_WIDTH + 8, cy, 80, 22),
                $"{(int)guard.HP}/{(int)guard.MaxHP}", _styleValue);
            cy += 28f;

            // --- 전투력 ---
            float combatPower = 0f;
            if (GuardEquipmentSystem.Instance != null)
                combatPower = GuardEquipmentSystem.Instance.CalculateGuardCombatPower(guard);
            GUI.Label(new Rect(x + 15, cy, 80, 22), "⚡ 전투력:", _styleLabel);
            GUI.Label(new Rect(x + 95, cy, 80, 22), $"{combatPower:F0}", _styleValue);
            cy += 26f;

            // --- 공격/방어/이속 ---
            float baseAtk = GuardLevelSystem.CalculateDamage(guard.Level);
            float baseDef = GuardLevelSystem.CalculateDefense(guard.Level);

            float equipAtk = GuardEquipmentSystem.Instance?.GetGuardEquipmentAttackBonus(guard) ?? 0f;
            float equipDef = GuardEquipmentSystem.Instance?.GetGuardEquipmentDefenseBonus(guard) ?? 0f;

            DrawStatRow(x, ref cy, "⚔️ 공격력:", $"{baseAtk + equipAtk:F1}", $"기본 {baseAtk:F1} + 장비 {equipAtk:F1}");
            DrawStatRow(x, ref cy, "🛡️ 방어력:", $"{baseDef + equipDef:F1}", $"기본 {baseDef:F1} + 장비 {equipDef:F1}");
            DrawStatRow(x, ref cy, "💨 이동속도:", "4.0", "기본 이동 속도");
            cy += 4f;

            // --- 호감도 (병사도 표시) ---
            DrawStatRow(x, ref cy, "🤝 호감도:", $"{guard.Loyalty:F0}/100", GuardLoyaltySystem.GetLoyaltyTag(guard.Loyalty));
            cy += 4f;

            // --- 장비 슬롯 ---
            DrawEquipmentSlots(x, ref cy, guard: guard);

            // --- 버프 목록 ---
            DrawGuardBuffs(x, ref cy, guard);
        }

        // ===== 용병 정보 그리기 =====

        private void DrawMercenaryInfo(float x, ref float cy)
        {
            var mercManager = MercenaryManager.Instance;
            if (mercManager == null) return;

            var merc = mercManager.GetHiredMercenary(_currentMercenaryId);
            if (merc.data.id == null || mercManager == null) { Close(); return; }

            var data = merc.data;

            // --- 헤더: 이름/등급/종류 ---
            string mercType = GetMercenaryTypeLabel(data.jobType);
            GUI.Label(new Rect(x + 15, cy, WINDOW_WIDTH - 30, 28),
                $"⚔️ {data.mercenaryName} {data.GradeStars} | {mercType}", _styleTitle);
            cy += 32f;

            // --- 레벨 (용병은 기본 Lv.1 표시) ---
            GUI.Label(new Rect(x + 15, cy, 80, 22), "Lv.", _styleLabel);
            GUI.Label(new Rect(x + 95, cy, 60, 22), "1", _styleValue);
            GUI.Label(new Rect(x + 155, cy, 120, 22), $"{data.grade}", _styleLabel);
            cy += 26f;

            // --- HP 프로그레스바 ---
            GUI.Label(new Rect(x + 15, cy, 60, 22), "❤️ HP:", _styleLabel);
            float hpRatio = merc.currentHP / data.maxHP;
            DrawBar(x + 80, cy, HP_BAR_WIDTH, 20, hpRatio, Color.green, Color.red);
            GUI.Label(new Rect(x + 80 + HP_BAR_WIDTH + 8, cy, 80, 22),
                $"{(int)merc.currentHP}/{(int)data.maxHP}", _styleValue);
            cy += 28f;

            // --- 전투력 ---
            float combatPower = 0f;
            if (GuardEquipmentSystem.Instance != null)
                combatPower = GuardEquipmentSystem.Instance.CalculateMercenaryCombatPower(_currentMercenaryId);
            GUI.Label(new Rect(x + 15, cy, 80, 22), "⚡ 전투력:", _styleLabel);
            GUI.Label(new Rect(x + 95, cy, 80, 22), $"{combatPower:F0}", _styleValue);
            cy += 26f;

            // --- 공격/방어/이속 ---
            float equipAtk = GuardEquipmentSystem.Instance?.GetMercenaryEquipmentAttackBonus(_currentMercenaryId) ?? 0f;
            float equipDef = GuardEquipmentSystem.Instance?.GetMercenaryEquipmentDefenseBonus(_currentMercenaryId) ?? 0f;
            float equipSpd = GuardEquipmentSystem.Instance?.GetMercenaryEquipmentSpeedBonus(_currentMercenaryId) ?? 0f;
            float affinityBonus = merc.AffinityBonus;

            float totalAtk = (data.attack + equipAtk) * (1f + affinityBonus);
            float totalDef = (data.defense + equipDef) * (1f + affinityBonus);
            float totalSpd = (data.moveSpeed + equipSpd) * (1f + affinityBonus * 0.5f);

            DrawStatRow(x, ref cy, "⚔️ 공격력:", $"{totalAtk:F1}", $"기본 {data.attack:F1} + 장비 {equipAtk:F1} + 호감도 {affinityBonus * 100:F0}%");
            DrawStatRow(x, ref cy, "🛡️ 방어력:", $"{totalDef:F1}", $"기본 {data.defense:F1} + 장비 {equipDef:F1} + 호감도 {affinityBonus * 100:F0}%");
            DrawStatRow(x, ref cy, "💨 이동속도:", $"{totalSpd:F1}", $"기본 {data.moveSpeed:F1} + 장비 {equipSpd:F1}");
            cy += 4f;

            // --- 호감도 ---
            DrawStatRow(x, ref cy, "🤝 호감도:", $"{merc.affinity:F0}/100", GetAffinityTag(merc.affinity));
            cy += 4f;

            // --- 특수 능력 ---
            if (!string.IsNullOrEmpty(data.specialAbility))
            {
                GUI.Label(new Rect(x + 15, cy, 100, 22), "✨ 특수능력:", _styleLabel);
                GUI.Label(new Rect(x + 115, cy, WINDOW_WIDTH - 130, 22), data.specialAbility, _styleValue);
                cy += 26f;
            }

            // --- 장비 슬롯 ---
            DrawEquipmentSlots(x, ref cy, mercenaryId: _currentMercenaryId);

            // --- 버프 목록 ---
            DrawMercenaryBuffs(x, ref cy, _currentMercenaryId);
        }

        // ===== 장비 슬롯 UI =====

        private void DrawEquipmentSlots(float x, ref float cy, GuardPlaceholder guard = null, string mercenaryId = null)
        {
            GUI.Label(new Rect(x + 15, cy, WINDOW_WIDTH - 30, 22), "📦 장비 슬롯", _styleHeader);
            cy += 28f;

            var system = GuardEquipmentSystem.Instance;

            // 기본 슬롯 (무기/방어구/액세서리)
            DrawSingleSlot(x, ref cy, GuardEquipmentSystem.EquipSlot.Weapon, "⚔️ 무기",
                guard != null ? system?.GetGuardEquipped(guard, GuardEquipmentSystem.EquipSlot.Weapon) : null,
                mercenaryId != null ? system?.GetMercenaryEquipped(mercenaryId, GuardEquipmentSystem.EquipSlot.Weapon) : null);

            DrawSingleSlot(x, ref cy, GuardEquipmentSystem.EquipSlot.Armor, "🛡️ 방어구",
                guard != null ? system?.GetGuardEquipped(guard, GuardEquipmentSystem.EquipSlot.Armor) : null,
                mercenaryId != null ? system?.GetMercenaryEquipped(mercenaryId, GuardEquipmentSystem.EquipSlot.Armor) : null);

            DrawSingleSlot(x, ref cy, GuardEquipmentSystem.EquipSlot.Accessory, "💍 액세서리",
                guard != null ? system?.GetGuardEquipped(guard, GuardEquipmentSystem.EquipSlot.Accessory) : null,
                mercenaryId != null ? system?.GetMercenaryEquipped(mercenaryId, GuardEquipmentSystem.EquipSlot.Accessory) : null);

            // 바드 전용 악기 슬롯
            if (mercenaryId != null)
            {
                var mercData = MercenaryManager.Instance?.GetMercenaryData(mercenaryId);
                if (mercData.HasValue && mercData.Value.jobType == "Bard")
                {
                    DrawSingleSlot(x, ref cy, GuardEquipmentSystem.EquipSlot.Instrument, "🎵 악기",
                        null,
                        system?.GetMercenaryEquipped(mercenaryId, GuardEquipmentSystem.EquipSlot.Instrument));
                }
            }
        }

        private void DrawSingleSlot(float x, ref float cy, GuardEquipmentSystem.EquipSlot slot, string label,
            GuardEquipmentSystem.EquippedItem guardItem, GuardEquipmentSystem.EquippedItem mercItem)
        {
            var item = guardItem ?? mercItem;

            // 슬롯 배경
            float slotX = x + 20;
            float slotY = cy;
            float slotW = WINDOW_WIDTH - 40;
            float slotH = 50f;

            GUI.Box(new Rect(slotX, slotY, slotW, slotH), "");

            // 슬롯 라벨
            GUI.Label(new Rect(slotX + 8, slotY + 4, 70, 20), label, _styleSlotLabel);

            if (item != null && item.itemData != null)
            {
                // 장착된 아이템 정보
                string itemName = item.itemData.displayName;
                string durabilityStr = item.itemData.maxDurability > 0
                    ? $"내구도 {item.currentDurability}/{item.itemData.maxDurability}"
                    : "내구도 ∞";

                Color rarityColor = GetRarityColor(item.itemData.rarity);
                var style = new GUIStyle(_styleValue) { normal = { textColor = rarityColor } };
                GUI.Label(new Rect(slotX + 80, slotY + 4, slotW - 120, 22), itemName, style);
                GUI.Label(new Rect(slotX + 80, slotY + 26, slotW - 120, 18), durabilityStr, _styleLabel);

                // 내구도 바
                if (item.itemData.maxDurability > 0)
                {
                    float durRatio = item.DurabilityRatio;
                    float barX = slotX + slotW - 75;
                    float barY = slotY + 28;
                    DrawBar(barX, barY, 60, 12, durRatio, Color.yellow, Color.gray);
                }

                // 해제 버튼
                if (GUI.Button(new Rect(slotX + slotW - 35, slotY + 12, 28, 24), "X"))
                {
                    if (guardItem != null && GuardEquipmentSystem.Instance != null)
                        GuardEquipmentSystem.Instance.UnequipGuard(_currentGuard, slot);
                    else if (mercItem != null && GuardEquipmentSystem.Instance != null)
                        GuardEquipmentSystem.Instance.UnequipMercenary(_currentMercenaryId, slot);
                }
            }
            else
            {
                // 빈 슬롯
                GUI.Label(new Rect(slotX + 80, slotY + 14, slotW - 100, 20), "(비어 있음)", _styleLabel);
            }

            cy += slotH + 4f;
        }

        // ===== 버프 목록 =====

        private void DrawGuardBuffs(float x, ref float cy, GuardPlaceholder guard)
        {
            GUI.Label(new Rect(x + 15, cy, WINDOW_WIDTH - 30, 22), "✨ 버프 효과", _styleHeader);
            cy += 26f;

            var buffs = GetGuardBuffs(guard);
            if (buffs.Count == 0)
            {
                GUI.Label(new Rect(x + 25, cy, WINDOW_WIDTH - 50, 20), "적용된 버프 없음", _styleLabel);
                cy += 24f;
            }
            else
            {
                float listHeight = Mathf.Min(buffs.Count * 22f, 80f);
                _buffScrollPos = GUI.BeginScrollView(
                    new Rect(x + 20, cy, WINDOW_WIDTH - 40, 80f),
                    _buffScrollPos,
                    new Rect(0, 0, WINDOW_WIDTH - 60, buffs.Count * 22f)
                );

                for (int i = 0; i < buffs.Count; i++)
                {
                    var buff = buffs[i];
                    string buffText = $"{buff.icon} {buff.name}: {buff.description}";
                    Color buffColor = buff.isPositive ? Color.green : Color.red;
                    var style = new GUIStyle(_styleBuffLabel) { normal = { textColor = buffColor } };
                    GUI.Label(new Rect(0, i * 22f, WINDOW_WIDTH - 60, 20), buffText, style);
                }

                GUI.EndScrollView();
                cy += 84f;
            }
        }

        private void DrawMercenaryBuffs(float x, ref float cy, string mercenaryId)
        {
            GUI.Label(new Rect(x + 15, cy, WINDOW_WIDTH - 30, 22), "✨ 버프 효과", _styleHeader);
            cy += 26f;

            var buffs = GetMercenaryBuffs(mercenaryId);
            if (buffs.Count == 0)
            {
                GUI.Label(new Rect(x + 25, cy, WINDOW_WIDTH - 50, 20), "적용된 버프 없음", _styleLabel);
                cy += 24f;
            }
            else
            {
                float listHeight = Mathf.Min(buffs.Count * 22f, 80f);
                _buffScrollPos = GUI.BeginScrollView(
                    new Rect(x + 20, cy, WINDOW_WIDTH - 40, 80f),
                    _buffScrollPos,
                    new Rect(0, 0, WINDOW_WIDTH - 60, buffs.Count * 22f)
                );

                for (int i = 0; i < buffs.Count; i++)
                {
                    var buff = buffs[i];
                    string buffText = $"{buff.icon} {buff.name}: {buff.description}";
                    Color buffColor = buff.isPositive ? Color.green : Color.red;
                    var style = new GUIStyle(_styleBuffLabel) { normal = { textColor = buffColor } };
                    GUI.Label(new Rect(0, i * 22f, WINDOW_WIDTH - 60, 20), buffText, style);
                }

                GUI.EndScrollView();
                cy += 84f;
            }
        }

        // ===== 버프 데이터 =====

        private struct BuffInfo
        {
            public string name;
            public string description;
            public string icon;
            public bool isPositive;
        }

        private List<BuffInfo> GetGuardBuffs(GuardPlaceholder guard)
        {
            var buffs = new List<BuffInfo>();

            // 바드 버프 확인
            if (BardBuffManager.Instance != null && BardBuffManager.Instance.TryGetBuff(guard, out var bardBuff))
            {
                buffs.Add(new BuffInfo
                {
                    name = "🎵 바드의 노래",
                    description = $"공격+{bardBuff.attackBonus * 100:F0}% 방어+{bardBuff.defenseBonus * 100:F0}% 이속+{bardBuff.speedBonus * 100:F0}%",
                    icon = "🎵",
                    isPositive = true
                });
            }

            // 포섭 상태 표시
            if (guard.IsRecruited)
            {
                buffs.Add(new BuffInfo
                {
                    name = "🤝 포섭됨",
                    description = "플레이어 영지 소속",
                    icon = "🤝",
                    isPositive = true
                });
            }

            // 사망 상태
            if (!guard.IsAlive)
            {
                buffs.Add(new BuffInfo
                {
                    name = "💀 사망",
                    description = "전사 상태",
                    icon = "💀",
                    isPositive = false
                });
            }

            return buffs;
        }

        private List<BuffInfo> GetMercenaryBuffs(string mercenaryId)
        {
            var buffs = new List<BuffInfo>();

            // 호감도 버프
            var merc = MercenaryManager.Instance?.GetHiredMercenary(mercenaryId);
            if (merc != null && merc.Value.data.id != null && merc.Value.affinity > 50f)
            {
                float bonus = merc.Value.AffinityBonus;
                buffs.Add(new BuffInfo
                {
                    name = "🤝 호감도 보너스",
                    description = $"모든 능력치 +{bonus * 100:F0}%",
                    icon = "🤝",
                    isPositive = true
                });
            }

            // 특수 능력 버프
            if (merc != null && merc.Value.data.id != null && !string.IsNullOrEmpty(merc.Value.data.specialAbility))
            {
                string abbr = merc.Value.data.specialAbility.Length > 30
                    ? merc.Value.data.specialAbility.Substring(0, 30) + "..."
                    : merc.Value.data.specialAbility;
                buffs.Add(new BuffInfo
                {
                    name = "✨ 특수 능력",
                    description = abbr,
                    icon = "✨",
                    isPositive = true
                });
            }

            return buffs;
        }

        // ===== 헬퍼 =====

        private void DrawStatRow(float x, ref float cy, string label, string value, string detail = "")
        {
            GUI.Label(new Rect(x + 15, cy, 110, 22), label, _styleLabel);
            GUI.Label(new Rect(x + 125, cy, 100, 22), value, _styleValue);

            if (!string.IsNullOrEmpty(detail))
            {
                var detailStyle = new GUIStyle(_styleLabel) { fontSize = 10, normal = { textColor = Color.gray } };
                GUI.Label(new Rect(x + 220, cy, WINDOW_WIDTH - 240, 22), detail, detailStyle);
            }

            cy += 24f;
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

        private Color GetRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return Color.white;
                case ItemRarity.Uncommon: return Color.green;
                case ItemRarity.Rare: return Color.blue;
                case ItemRarity.Epic: return new Color(0.7f, 0.2f, 0.9f); // 보라
                case ItemRarity.Legendary: return new Color(1f, 0.84f, 0f); // 금
                default: return Color.white;
            }
        }

        private string GetGuardTypeLabel(GuardPlaceholder guard)
        {
            if (guard == null) return "병사";

            // 임시: GuardPlaceholder에 type 필드가 없으므로 JobTitle로 판단
            string job = guard.JobTitle;
            if (string.IsNullOrEmpty(job) || job == "병사") return "병사";
            return job;
        }

        private string GetMercenaryTypeLabel(string jobType)
        {
            switch (jobType)
            {
                case "Soldier": return "용병";
                case "Bard": return "🎵 바드";
                case "Archer": return "🏹 궁수";
                case "Mage": return "🔮 마법사";
                default: return "용병";
            }
        }

        private string GetGuardGradeString(GuardPlaceholder guard)
        {
            // 병사는 등급 정보가 없으므로 레벨 기반 표시
            int lv = guard.Level;
            if (lv >= 35) return "★★★★";
            if (lv >= 20) return "★★★";
            if (lv >= 10) return "★★";
            return "★";
        }

        private string GetAffinityTag(float affinity)
        {
            if (affinity >= 90) return "충성";
            if (affinity >= 70) return "우호적";
            if (affinity >= 40) return "보통";
            if (affinity >= 20) return "냉담";
            return "적대";
        }

        // ===== 스타일 초기화 =====

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _styleHeader = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.6f, 0.9f, 1f) } // 밝은 하늘색
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };

            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _styleSlotLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _styleBuffLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.green }
            };
        }
    }
}