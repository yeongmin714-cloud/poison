using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 크래프트 타입: 연금술, 요리, 제작
    /// </summary>
    public enum CraftType
    {
        Alchemy,
        Cooking,
        Crafting
    }

    /// <summary>
    /// 크래프트 프리셋 데이터 구조체
    /// </summary>
    [System.Serializable]
    public struct CraftPreset
    {
        public string presetName;
        public List<string> ingredientIds;
        public string resultId;
        public CraftType type;

        public CraftPreset(string name, List<string> ingredientIds, string resultId, CraftType type)
        {
            this.presetName = name;
            this.ingredientIds = ingredientIds ?? new List<string>();
            this.resultId = resultId ?? string.Empty;
            this.type = type;
        }
    }

    /// <summary>
    /// 크래프트 프리셋 및 즐겨찾기 관리자 (MonoBehaviour singleton)
    /// PlayerPrefs에 JSON 직렬화하여 저장 (최대 20개 프리셋)
    /// </summary>
    public class CraftPresetManager : MonoBehaviour
    {
        public static CraftPresetManager Instance { get; private set; }

        private const string PRESET_PREFIX = "CraftPreset_";
        private const string FAVORITES_KEY = "CraftFavorites";
        private const int MAX_PRESETS = 20;

        private List<CraftPreset> _presets;
        private List<string> _favoriteRecipeIds;
        private bool _initialized;

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

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            LoadPresetsInternal();
            LoadFavoritesInternal();
        }

        // ── 프리셋 저장/불러오기 ──

        /// <summary>
        /// 프리셋을 저장합니다. 최대 20개까지 저장 가능합니다.
        /// </summary>
        public void SavePreset(string name, List<string> ingredientIds, string resultId, CraftType type)
        {
            if (Instance == null)
            {
                Debug.LogError("[CraftPresetManager] Instance is null!");
                return;
            }

            Instance.Initialize();

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[CraftPresetManager] 프리셋 이름이 비어있습니다.");
                return;
            }

            // 같은 이름이 있으면 수정
            int existingIndex = Instance._presets.FindIndex(p => p.presetName == name);
            var preset = new CraftPreset(name, ingredientIds, resultId, type);

            if (existingIndex >= 0)
            {
                Instance._presets[existingIndex] = preset;
            }
            else
            {
                if (Instance._presets.Count >= MAX_PRESETS)
                {
                    Debug.LogWarning($"[CraftPresetManager] 프리셋 최대 {MAX_PRESETS}개를 초과했습니다.");
                    return;
                }
                Instance._presets.Add(preset);
            }

            Instance.SavePresetsInternal();
            Debug.Log($"[CraftPresetManager] 프리셋 저장: {name} (재료 {ingredientIds?.Count ?? 0}개, 결과: {resultId})");
        }

        /// <summary>
        /// 저장된 모든 프리셋을 불러옵니다.
        /// </summary>
        public List<CraftPreset> LoadPresets()
        {
            if (Instance == null)
            {
                Debug.LogError("[CraftPresetManager] Instance is null!");
                return new List<CraftPreset>();
            }

            Instance.Initialize();
            return new List<CraftPreset>(Instance._presets);
        }

        /// <summary>
        /// 프리셋을 삭제합니다.
        /// </summary>
        public void DeletePreset(string name)
        {
            if (Instance == null)
            {
                Debug.LogError("[CraftPresetManager] Instance is null!");
                return;
            }

            Instance.Initialize();
            int removed = Instance._presets.RemoveAll(p => p.presetName == name);
            if (removed > 0)
            {
                Instance.SavePresetsInternal();
                Debug.Log($"[CraftPresetManager] 프리셋 삭제: {name}");
            }
        }

        /// <summary>
        /// 이름으로 프리셋을 조회합니다. 없으면 null을 반환합니다.
        /// </summary>
        public CraftPreset? GetPreset(string name)
        {
            if (Instance == null)
            {
                Debug.LogError("[CraftPresetManager] Instance is null!");
                return null;
            }

            Instance.Initialize();
            int index = Instance._presets.FindIndex(p => p.presetName == name);
            if (index >= 0)
                return Instance._presets[index];
            return null;
        }

        // ── 내부 저장/불러오기 ──

        private void LoadPresetsInternal()
        {
            _presets = new List<CraftPreset>(MAX_PRESETS);
            for (int i = 0; i < MAX_PRESETS; i++)
            {
                string key = PRESET_PREFIX + i;
                string json = PlayerPrefs.GetString(key, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var preset = JsonUtility.FromJson<CraftPreset>(json);
                        if (!string.IsNullOrEmpty(preset.presetName))
                        {
                            _presets.Add(preset);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[CraftPresetManager] 프리셋 로드 실패 (슬롯 {i}): {ex.Message}");
                    }
                }
            }
            Debug.Log($"[CraftPresetManager] 프리셋 {_presets.Count}개 로드됨");
        }

        private void SavePresetsInternal()
        {
            // 기존 데이터 초기화
            for (int i = 0; i < MAX_PRESETS; i++)
            {
                PlayerPrefs.DeleteKey(PRESET_PREFIX + i);
            }

            // 현재 프리셋 저장
            for (int i = 0; i < _presets.Count && i < MAX_PRESETS; i++)
            {
                string json = JsonUtility.ToJson(_presets[i]);
                PlayerPrefs.SetString(PRESET_PREFIX + i, json);
            }

            PlayerPrefs.Save();
            Debug.Log($"[CraftPresetManager] 프리셋 {_presets.Count}개 저장됨");
        }

        // ── 즐겨찾기 ──

        private void LoadFavoritesInternal()
        {
            _favoriteRecipeIds = new List<string>();
            string saved = PlayerPrefs.GetString(FAVORITES_KEY, string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                string[] ids = saved.Split('|');
                foreach (string id in ids)
                {
                    string trimmed = id.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        _favoriteRecipeIds.Add(trimmed);
                }
            }
        }

        private void SaveFavoritesInternal()
        {
            string joined = string.Join("|", _favoriteRecipeIds);
            PlayerPrefs.SetString(FAVORITES_KEY, joined);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 레시피 즐겨찾기를 토글합니다.
        /// </summary>
        public void ToggleFavorite(string recipeId)
        {
            if (Instance == null)
            {
                Debug.LogError("[CraftPresetManager] Instance is null!");
                return;
            }

            Instance.Initialize();

            if (string.IsNullOrEmpty(recipeId))
                return;

            if (Instance._favoriteRecipeIds.Contains(recipeId))
            {
                Instance._favoriteRecipeIds.Remove(recipeId);
                Debug.Log($"[CraftPresetManager] 즐겨찾기 해제: {recipeId}");
            }
            else
            {
                Instance._favoriteRecipeIds.Add(recipeId);
                Debug.Log($"[CraftPresetManager] 즐겨찾기 추가: {recipeId}");
            }

            Instance.SaveFavoritesInternal();
        }

        /// <summary>
        /// 레시피가 즐겨찾기되었는지 확인합니다.
        /// </summary>
        public bool IsFavorite(string recipeId)
        {
            if (Instance == null || string.IsNullOrEmpty(recipeId))
                return false;

            Instance.Initialize();
            return Instance._favoriteRecipeIds.Contains(recipeId);
        }

        /// <summary>
        /// 즐겨찾기된 모든 레시피 ID 목록을 반환합니다.
        /// </summary>
        public List<string> GetFavorites()
        {
            if (Instance == null)
                return new List<string>();

            Instance.Initialize();
            return new List<string>(Instance._favoriteRecipeIds);
        }

        /// <summary>
        /// 게임 종료 시 PlayerPrefs 저장 보장
        /// </summary>
        private void OnApplicationQuit()
        {
            PlayerPrefs.Save();
        }
    }
}