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
        private Vector2 _scrollPos;
        private Vector2 _buffScrollPos;
        private bool _showRolePanel = false;

        // ===== 스타일 (lazy init) =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleSlotLabel;
        private GUIStyle _styleBuffLabel;
        private GUIStyle _styleHeader;

        // ===== 캐시된 스타일 (GC 방지: 매 프레임 new GUIStyle() 지양) =====
        private GUIStyle _styleBuffPositive;
        private GUIStyle _styleBuffNegative;
        private GUIStyle _styleDetail;
        private GUIStyle _styleRarityCommon;
        private GUIStyle _styleRarityUncommon;
        private GUIStyle _styleRarityRare;
        private GUIStyle _styleRarityEpic;
        private GUIStyle _styleRarityLegendary;

        // ===== 캐시된 데이터 (GC 방지: 매 프레임 new List<T>() 지양) =====
        private readonly List<BuffInfo> _cachedGuardBuffs = new List<BuffInfo>(8);
        private readonly List<BuffInfo> _cachedMercenaryBuffs = new List<BuffInfo>(8);

        // ===== 캐시된 텍스처 (GC 방지: 매 프레임 new Texture2D() 지양) =====
        private static Texture2D _cachedWhiteTex;

        // ===== 상수 =====
        private const float WINDOW_WIDTH = 780f;
        private const float WINDOW_HEIGHT = 900f;
        private const float HP_BAR_WIDTH = 300f;
        private const float SLOT_ICON_SIZE = 80f;

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
                DrawRoleSection(x, ref cy);
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
            GUI.Label(new Rect(x + 15, cy, 120, 22), "Lv.", _styleLabel);
            GUI.Label(new Rect(x + 95, cy, 90, 22), $"{guard.Level}", _styleValue);
            string nationDisplay = !string.IsNullOrEmpty(guard.Nation) ? $" ({guard.Nation})" : "";
            GUI.Label(new Rect(x + 155, cy, 180, 22), nationDisplay, _styleLabel);
            cy += 26f;

            // --- HP 프로그레스바 ---
            GUI.Label(new Rect(x + 15, cy, 90, 22), "❤️ HP:", _styleLabel);
            float hpRatio = guard.HP / guard.MaxHP;
            DrawBar(x + 80, cy, HP_BAR_WIDTH, 20, hpRatio, Color.green, Color.red);
            GUI.Label(new Rect(x + 80 + HP_BAR_WIDTH + 8, cy, 120, 22),
                $"{(int)guard.HP}/{(int)guard.MaxHP}", _styleValue);
            cy += 28f;

            // --- 전투력 ---
            float combatPower = 0f;
            if (GuardEquipmentSystem.Instance != null)
                combatPower = GuardEquipmentSystem.Instance.CalculateGuardCombatPower(guard);
            GUI.Label(new Rect(x + 15, cy, 120, 22), "⚡ 전투력:", _styleLabel);
            GUI.Label(new Rect(x + 95, cy, 120, 22), $"{combatPower:F0}", _styleValue);
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
            if (merc.data.id == null) { Close(); return; }

            var data = merc.data;

            // --- 헤더: 이름/등급/종류 ---
            string mercType = GetMercenaryTypeLabel(data.jobType);
            GUI.Label(new Rect(x + 15, cy, WINDOW_WIDTH - 30, 28),
                $"⚔️ {data.mercenaryName} {data.GradeStars} | {mercType}", _styleTitle);
            cy += 32f;

            // --- 레벨 (용병은 기본 Lv.1 표시) ---
            GUI.Label(new Rect(x + 15, cy, 120, 22), "Lv.", _styleLabel);
            GUI.Label(new Rect(x + 95, cy, 90, 22), "1", _styleValue);
            GUI.Label(new Rect(x + 155, cy, 180, 22), $"{data.grade}", _styleLabel);
            cy += 26f;

            // --- HP 프로그레스바 ---
            GUI.Label(new Rect(x + 15, cy, 90, 22), "❤️ HP:", _styleLabel);
            float hpRatio = merc.currentHP / data.maxHP;
            DrawBar(x + 80, cy, HP_BAR_WIDTH, 20, hpRatio, Color.green, Color.red);
            GUI.Label(new Rect(x + 80 + HP_BAR_WIDTH + 8, cy, 120, 22),
                $"{(int)merc.currentHP}/{(int)data.maxHP}", _styleValue);
            cy += 28f;

            // --- 전투력 ---
            float combatPower = 0f;
            if (GuardEquipmentSystem.Instance != null)
                combatPower = GuardEquipmentSystem.Instance.CalculateMercenaryCombatPower(_currentMercenaryId);
            GUI.Label(new Rect(x + 15, cy, 120, 22), "⚡ 전투력:", _styleLabel);
            GUI.Label(new Rect(x + 95, cy, 120, 22), $"{combatPower:F0}", _styleValue);
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
                GUI.Label(new Rect(x + 15, cy, 150, 22), "✨ 특수능력:", _styleLabel);
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
            GUI.Label(new Rect(slotX + 8, slotY + 4, 105, 20), label, _styleSlotLabel);

            if (item != null && item.itemData != null)
            {
                // 장착된 아이템 정보
                string itemName = item.itemData.displayName;
                string durabilityStr = item.itemData.maxDurability > 0
                    ? $"내구도 {item.currentDurability}/{item.itemData.maxDurability}"
                    : "내구도 ∞";

                GUIStyle style = GetRarityStyle(item.itemData.rarity);
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
                _buffScrollPos = GUI.BeginScrollView(
                    new Rect(x + 20, cy, WINDOW_WIDTH - 40, 80f),
                    _buffScrollPos,
                    new Rect(0, 0, WINDOW_WIDTH - 60, buffs.Count * 22f)
                );

                for (int i = 0; i < buffs.Count; i++)
                {
                    var buff = buffs[i];
                    string buffText = $"{buff.icon} {buff.name}: {buff.description}";
                    GUIStyle style = buff.isPositive ? _styleBuffPositive : _styleBuffNegative;
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
                _buffScrollPos = GUI.BeginScrollView(
                    new Rect(x + 20, cy, WINDOW_WIDTH - 40, 80f),
                    _buffScrollPos,
                    new Rect(0, 0, WINDOW_WIDTH - 60, buffs.Count * 22f)
                );

                for (int i = 0; i < buffs.Count; i++)
                {
                    var buff = buffs[i];
                    string buffText = $"{buff.icon} {buff.name}: {buff.description}";
                    GUIStyle style = buff.isPositive ? _styleBuffPositive : _styleBuffNegative;
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
            var buffs = _cachedGuardBuffs;
            buffs.Clear();

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
            var buffs = _cachedMercenaryBuffs;
            buffs.Clear();

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
            GUI.Label(new Rect(x + 15, cy, 165, 22), label, _styleLabel);
            GUI.Label(new Rect(x + 125, cy, 150, 22), value, _styleValue);

            if (!string.IsNullOrEmpty(detail))
            {
                GUI.Label(new Rect(x + 220, cy, WINDOW_WIDTH - 240, 22), detail, _styleDetail);
            }

            cy += 24f;
        }

        private void DrawBar(float x, float y, float width, float height, float ratio, Color fillColor, Color bgColor)
        {
            EnsureWhiteTex();
            GUI.color = bgColor;
            GUI.DrawTexture(new Rect(x, y, width, height), _cachedWhiteTex);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(ratio), height), _cachedWhiteTex);
            GUI.color = Color.white;
        }

        private static void EnsureWhiteTex()
        {
            if (_cachedWhiteTex == null)
            {
                _cachedWhiteTex = new Texture2D(1, 1);
                _cachedWhiteTex.SetPixel(0, 0, Color.white);
                _cachedWhiteTex.Apply();
            }
        }

        private void OnDestroy()
        {
            // 에디터 종료 시 정리 (선택 사항, 누수 방지)
            if (_cachedWhiteTex != null)
            {
                Destroy(_cachedWhiteTex);
                _cachedWhiteTex = null;
            }
        }

        private GUIStyle GetRarityStyle(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return _styleRarityCommon;
                case ItemRarity.Uncommon: return _styleRarityUncommon;
                case ItemRarity.Rare: return _styleRarityRare;
                case ItemRarity.Epic: return _styleRarityEpic;
                case ItemRarity.Legendary: return _styleRarityLegendary;
                default: return _styleRarityCommon;
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

        // ===== 역할 변경 UI =====

        /// <summary>역할 변경 섹션 그리기 (DrawGuardInfo 후 호출)</summary>
        private void DrawRoleSection(float x, ref float cy)
        {
            var guard = _currentGuard;
            if (guard == null) return;

            // 구분선
            cy += 6f;
            GUI.Box(new Rect(x + 15, cy, WINDOW_WIDTH - 30, 2f), "");
            cy += 10f;

            // 현재 역할 표시
            string currentRoleName = GuardStatusSystem.GetRoleName(guard.Role);
            string currentRoleDesc = GuardStatusSystem.GetRoleDescription(guard.Role);
            GUI.Label(new Rect(x + 15, cy, 120, 22), "🎯 현재 역할:", _styleLabel);
            GUI.Label(new Rect(x + 115, cy, 250, 22), currentRoleName, _styleValue);
            cy += 22f;
            GUI.Label(new Rect(x + 25, cy, WINDOW_WIDTH - 40, 20), currentRoleDesc, _styleDetail);
            cy += 26f;

            // 역할 변경 버튼 (토글)
            if (!_showRolePanel)
            {
                if (GUI.Button(new Rect(x + 15, cy, 180, 30), "🔄 역할 변경"))
                {
                    _showRolePanel = true;
                }
                cy += 36f;
            }
            else
            {
                if (GUI.Button(new Rect(x + 15, cy, 180, 30), "🔽 접기"))
                {
                    _showRolePanel = false;
                }
                cy += 36f;

                DrawRoleGrid(x, ref cy, guard);
            }
        }

        /// <summary>5개 역할 선택 그리드</summary>
        private void DrawRoleGrid(float x, ref float cy, GuardPlaceholder guard)
        {
            // 역할 정의: (role, 필요레벨)
            var roleOptions = new (GuardRole role, int requiredLevel)[]
            {
                (GuardRole.Soldier, 1),
                (GuardRole.Miner, 3),
                (GuardRole.Herbalist, 3),
                (GuardRole.Hunter, 3),
                (GuardRole.Informant, 5)
            };

            float gridStartY = cy;
            float cardW = (WINDOW_WIDTH - 60f) / 2f; // 2열
            float cardH = 80f;
            float spacing = 6f;

            for (int i = 0; i < roleOptions.Length; i++)
            {
                var (role, reqLv) = roleOptions[i];
                bool isCurrentRole = guard.Role == role;
                bool canChange = guard.Level >= reqLv;
                bool meetsLevelReq = canChange;

                int col = i % 2;
                int row = i / 2;
                float cx = x + 15 + col * (cardW + spacing);
                float cardY = gridStartY + row * (cardH + spacing);

                // 카드 배경
                Color originalBg = GUI.backgroundColor;
                if (isCurrentRole)
                    GUI.backgroundColor = new Color(0.2f, 0.5f, 0.2f, 0.8f); // 현재 역할 강조
                else if (!meetsLevelReq)
                    GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f); // 잠금

                GUI.Box(new Rect(cx, cardY, cardW, cardH), "");
                GUI.backgroundColor = originalBg;

                // 역할명 + 아이콘
                string roleName = GuardStatusSystem.GetRoleName(role);
                string lockIcon = meetsLevelReq ? "" : " 🔒";
                GUIStyle nameStyle = meetsLevelReq ? _styleValue : _styleDetail;
                GUI.Label(new Rect(cx + 8, cardY + 4, cardW - 16, 22),
                    $"{roleName}{lockIcon}", nameStyle);

                // 설명
                string desc = GuardStatusSystem.GetRoleDescription(role);
                GUI.Label(new Rect(cx + 8, cardY + 26, cardW - 16, 20), desc, _styleDetail);

                // 레벨 요구사항
                string reqText = reqLv <= 1 ? "기본" : $"Lv.{reqLv} 필요";
                GUI.Label(new Rect(cx + 8, cardY + 46, cardW - 16, 16), reqText, _styleTimestamp);

                // 현재 역할 표시
                if (isCurrentRole)
                {
                    GUI.Label(new Rect(cx + cardW - 70, cardY + 48, 65, 18), "✔ 현재", _styleValue);
                }
                // 변경 버튼 (잠금 상태는 비활성화 느낌)
                else if (meetsLevelReq)
                {
                    Color origColor = GUI.color;
                    GUI.color = new Color(0.6f, 0.9f, 0.6f);
                    if (GUI.Button(new Rect(cx + cardW - 80, cardY + 44, 72, 24), "변경"))
                    {
                        TryChangeRole(guard, role);
                    }
                    GUI.color = origColor;
                }
                else
                {
                    GUI.Label(new Rect(cx + cardW - 80, cardY + 46, 72, 18), "레벨 부족", _styleTimestamp);
                }
            }

            // 그리드가 차지한 높이 계산
            int totalRows = (roleOptions.Length + 1) / 2; // 올림 나눗셈
            cy = gridStartY + totalRows * (cardH + spacing);
        }

        /// <summary>역할 변경 시도</summary>
        private void TryChangeRole(GuardPlaceholder guard, GuardRole newRole)
        {
            if (guard == null) return;
            if (guard.Role == newRole) return;

            // 레벨 요구사항 검사
            int requiredLevel = 1;
            switch (newRole)
            {
                case GuardRole.Miner:
                case GuardRole.Herbalist:
                case GuardRole.Hunter:
                    requiredLevel = 3;
                    break;
                case GuardRole.Informant:
                    requiredLevel = 5;
                    break;
                default:
                    requiredLevel = 1;
                    break;
            }

            if (guard.Level < requiredLevel)
            {
                Debug.Log($"[GuardInfoWindow] {guard.GuardName}의 레벨({guard.Level})이 {newRole} 요구 레벨({requiredLevel})에 미달합니다.");
                return;
            }

            guard.Role = newRole;
            _showRolePanel = false;
            Debug.Log($"[GuardInfoWindow] {guard.GuardName}의 역할이 {newRole}(으)로 변경되었습니다.");
        }

        private GUIStyle _styleTimestamp;

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
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.6f, 0.9f, 1f) } // 밝은 하늘색
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _styleSlotLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _styleBuffLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.green }
            };

            // ===== 캐시된 파생 스타일 (GC 방지) =====
            _styleBuffPositive = new GUIStyle(_styleBuffLabel)
            {
                normal = { textColor = Color.green }
            };

            _styleBuffNegative = new GUIStyle(_styleBuffLabel)
            {
                normal = { textColor = Color.red }
            };

            _styleDetail = new GUIStyle(_styleLabel)
            {
                fontSize = 12,
                normal = { textColor = Color.gray }
            };

            _styleRarityCommon = new GUIStyle(_styleValue) { normal = { textColor = Color.white } };
            _styleRarityUncommon = new GUIStyle(_styleValue) { normal = { textColor = Color.green } };
            _styleRarityRare = new GUIStyle(_styleValue) { normal = { textColor = Color.blue } };
            var epicColor = new Color(0.7f, 0.2f, 0.9f);
            _styleRarityEpic = new GUIStyle(_styleValue) { normal = { textColor = epicColor } };
            var legendaryColor = new Color(1f, 0.84f, 0f);
            _styleRarityLegendary = new GUIStyle(_styleValue) { normal = { textColor = legendaryColor } };

            _styleTimestamp = new GUIStyle(_styleLabel)
            {
                fontSize = 11,
                normal = { textColor = Color.gray }
            };
        }
    }
}