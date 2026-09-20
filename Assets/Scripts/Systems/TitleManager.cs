using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase O6 (C-O6-01/02): 칭호 관리자 — 카운터 누적 → 임계 도달 시 발급 → 장착/저장.
    /// OpenMMO TITLES.md: 칭호는 경제와 비접촉(골드/아이템 보상 없음, 표시 전용 명성).
    /// 구독 계통: TerritoryDatabase.OwnershipChanged(PlayerOwned) → "conquests",
    ///           EnvoySystem 암살 성공 → "assassinations". 처형/세트/제작 훅은 후속 배선.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        public static TitleManager Instance { get; private set; }

        /// <summary>칭호 발급 시 발화 (UI 팝업 구독용).</summary>
        public static event System.Action<TitleDef> TitleUnlocked;

        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private readonly HashSet<string> _unlocked = new HashSet<string>();
        private string _equippedTitleId = "";

        // PlayerPrefs 키 (v1 — 직렬화: "source:count|source:count...")
        private const string KeyCounts = "titles_counts_v1";
        private const string KeyUnlocked = "titles_unlocked_v1";
        private const string KeyEquipped = "titles_equipped_v1";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
            ProjectName.Core.Data.TerritoryDatabase.OwnershipChanged += OnOwnershipChanged;
            LordSurrenderSystem.OnLordExecuted += OnLordExecuted; // [P2a] 처형 → executions 카운터
            CraftingHelper.CraftSucceeded += OnCraftSucceeded;    // [P2b] 제작 → crafts 카운터
        }

        private void OnDestroy()
        {
            ProjectName.Core.Data.TerritoryDatabase.OwnershipChanged -= OnOwnershipChanged;
            LordSurrenderSystem.OnLordExecuted -= OnLordExecuted;
            CraftingHelper.CraftSucceeded -= OnCraftSucceeded;
            if (Instance == this) Instance = null;
        }

        // ── [P2a/P2b] 훅 핸들러 ──

        private void OnLordExecuted(ProjectName.Core.Data.TerritoryId id, LordSurrenderSystem.LordData lord)
        {
            RecordEvent("executions");
        }

        private void OnCraftSucceeded(string resultItemId)
        {
            RecordEvent("crafts");
        }

        // ── [P2c] 세트 완성 직접 해금 — 카운터가 아닌 종류 기반 (중복 무시) ──

        private readonly HashSet<string> _completedSets = new HashSet<string>();

        /// <summary>
        /// [P2c] 장착 목록에서 완성된 세트를 검사해 신규 종류만 해금.
        /// set_leather(가죽)/set_plate(판금) 직접 해금, 3종 모두 → set_all3.
        /// </summary>
        public void EvaluateSetCompletion(System.Collections.Generic.List<PlayerInventory.ItemData> equippedItems)
        {
            if (equippedItems == null || equippedItems.Count == 0) return;

            foreach (var kind in EquipmentTierSet.GetAllSetKinds())
            {
                string kindKey = kind.ToString();
                if (_completedSets.Contains(kindKey)) continue;
                if (EquipmentTierSet.CountSetPieces(kind, equippedItems) >= 4)
                {
                    _completedSets.Add(kindKey);
                    Debug.Log($"[TitleManager] 🧵 세트 완성 감지: {kindKey}");
                    if (kind == EquipmentSetKind.Leather && !_unlocked.Contains("set_leather")) Unlock(FindTitleDef("set_leather"));
                    if (kind == EquipmentSetKind.Plate && !_unlocked.Contains("set_plate")) Unlock(FindTitleDef("set_plate"));
                    if (_completedSets.Count >= 3 && !_unlocked.Contains("set_all3")) Unlock(FindTitleDef("set_all3"));
                    Save();
                }
            }
        }

        private TitleDef FindTitleDef(string id)
        {
            ProjectName.Core.Data.TitleData.TryGet(id, out var def);
            return def;
        }

        // ── 카운터 & 발급 ──

        /// <summary>이벤트 카운터 누적 + 임계 도달 칭호 발급.</summary>
        public void RecordEvent(string sourceType, int amount = 1)
        {
            if (string.IsNullOrEmpty(sourceType) || amount <= 0) return;

            _counts.TryGetValue(sourceType, out int current);
            _counts[sourceType] = current + amount;
            Save();

            foreach (var def in ProjectName.Core.Data.TitleData.GetBySource(sourceType))
            {
                if (_unlocked.Contains(def.id)) continue;
                if (_counts[sourceType] >= def.threshold)
                    Unlock(def);
            }
        }

        /// <summary>
        /// 순수 평가 — 소스 카운터가 newCount일 때 해금되는 칭호 id 배열 (테스트/UI용).
        /// </summary>
        public static string[] EvaluateUnlocks(string sourceType, int newCount)
        {
            var defs = ProjectName.Core.Data.TitleData.GetBySource(sourceType);
            var result = new List<string>();
            foreach (var def in defs)
            {
                if (newCount >= def.threshold)
                    result.Add(def.id);
            }
            return result.ToArray();
        }

        private void Unlock(TitleDef def)
        {
            _unlocked.Add(def.id);
            Save();
            Debug.Log($"[TitleManager] 🏅 칭호 획득: {def.displayName} ({def.id})");
            TitleUnlocked?.Invoke(def);
        }

        public int GetCount(string sourceType)
        {
            return _counts.TryGetValue(sourceType, out int c) ? c : 0;
        }

        public bool IsUnlocked(string titleId)
        {
            return !string.IsNullOrEmpty(titleId) && _unlocked.Contains(titleId);
        }

        public string[] GetUnlockedIds()
        {
            var arr = new string[_unlocked.Count];
            _unlocked.CopyTo(arr);
            return arr;
        }

        // ── 장착 ──

        /// <summary>칭호 장착 — 미발급이면 무시(경고).</summary>
        public void EquipTitle(string titleId)
        {
            if (string.IsNullOrEmpty(titleId) || !_unlocked.Contains(titleId))
            {
                Debug.LogWarning($"[TitleManager] 미발급 칭호 장착 시도: {titleId}");
                return;
            }
            _equippedTitleId = titleId;
            Save();
        }

        public string EquippedTitleId => _equippedTitleId;

        /// <summary>표시용 칭호 텍스트 (미장착/미발급 = 빈 문자열).</summary>
        public string GetTitleText()
        {
            if (!string.IsNullOrEmpty(_equippedTitleId)
                && ProjectName.Core.Data.TitleData.TryGet(_equippedTitleId, out var def))
                return def.displayName;
            return "";
        }

        private void OnOwnershipChanged(ProjectName.Core.Data.TerritoryId id, ProjectName.Core.Data.TerritoryOwnership ownership)
        {
            if (ownership == ProjectName.Core.Data.TerritoryOwnership.PlayerOwned)
                RecordEvent("conquests");
        }

        // ── 저장 ──

        public void Save()
        {
            var cb = new System.Text.StringBuilder();
            foreach (var kvp in _counts)
                cb.Append($"{kvp.Key}:{kvp.Value}|");
            PlayerPrefs.SetString(KeyCounts, cb.ToString().TrimEnd('|'));

            var ub = new System.Text.StringBuilder();
            foreach (var id in _unlocked)
                ub.Append($"{id}|");
            PlayerPrefs.SetString(KeyUnlocked, ub.ToString().TrimEnd('|'));

            PlayerPrefs.SetString(KeyEquipped, _equippedTitleId ?? "");
            PlayerPrefs.Save();
        }

        public void Load()
        {
            _counts.Clear();
            _unlocked.Clear();
            _equippedTitleId = "";

            string counts = PlayerPrefs.GetString(KeyCounts, "");
            foreach (var pair in counts.Split('|'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                var parts = pair.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int c))
                    _counts[parts[0]] = c;
            }

            string unlocked = PlayerPrefs.GetString(KeyUnlocked, "");
            foreach (var id in unlocked.Split('|'))
            {
                if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
            }

            _equippedTitleId = PlayerPrefs.GetString(KeyEquipped, "");
        }

        /// <summary>테스트/리셋용 — 저장 포함 전체 초기화.</summary>
        public void ResetAll()
        {
            _counts.Clear();
            _unlocked.Clear();
            _equippedTitleId = "";
            PlayerPrefs.DeleteKey(KeyCounts);
            PlayerPrefs.DeleteKey(KeyUnlocked);
            PlayerPrefs.DeleteKey(KeyEquipped);
            PlayerPrefs.Save();
        }
    }
}
