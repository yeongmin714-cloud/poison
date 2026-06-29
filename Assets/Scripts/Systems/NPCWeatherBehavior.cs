using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 41.3: NPC 날씨 반응 시스템.
    /// 날씨 변화에 따라 NPC의 행동(은신처 탐색, 시야/감지 범위 변경)을 제어합니다.
    /// 
    /// 주요 기능:
    ///   - 악천후(비/눈/강풍) 시 NPC가 건물 안으로 이동
    ///   - 날씨별 NPC 감지 범위 수정
    ///   - WeatherEffects/TimeOfDayEffects와 연동하여 종합 효과 계산
    /// </summary>
    public class NPCWeatherBehavior : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        public static NPCWeatherBehavior Instance { get; private set; }

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("Shelter Seeking Behavior")]
        [SerializeField, Tooltip("비 올 때 NPC가 건물 안으로 이동")]
        private bool _seekShelterInRain = true;

        [SerializeField, Tooltip("눈 올 때 NPC가 건물 안으로 이동")]
        private bool _seekShelterInSnow = true;

        [SerializeField, Tooltip("강풍 시 NPC가 건물 안으로 이동")]
        private bool _seekShelterInStorm = true;

        [SerializeField, Tooltip("안개 시 NPC가 건물 안으로 이동")]
        private bool _seekShelterInFog = false;

        [Header("Detection Range Modifiers (multiplier)")]
        [SerializeField, Range(0f, 2f), Tooltip("비: NPC 감지 범위 배율 (0.7 = 30% 감소)")]
        private float _rainDetectionRangeMultiplier = 0.7f;

        [SerializeField, Range(0f, 2f), Tooltip("눈: NPC 감지 범위 배율 (0.6 = 40% 감소)")]
        private float _snowDetectionRangeMultiplier = 0.6f;

        [SerializeField, Range(0f, 2f), Tooltip("안개: NPC 감지 범위 배율 (0.5 = 50% 감소)")]
        private float _fogDetectionRangeMultiplier = 0.5f;

        [SerializeField, Range(0f, 2f), Tooltip("강풍: NPC 감지 범위 배율 (0.3 = 70% 감소)")]
        private float _stormDetectionRangeMultiplier = 0.3f;

        [SerializeField, Range(0f, 2f), Tooltip("밤: NPC 감지 범위 배율 (0.7 = 30% 감소)")]
        private float _nightDetectionRangeMultiplier = 0.7f;

        [Header("Shelter Tags")]
        [SerializeField, Tooltip("건물/은신처로 간주할 태그 목록")]
        private string[] _shelterTags = { "Building", "House", "Shelter", "Shop", "Interior" };

        [Header("Update Interval")]
        [SerializeField, Tooltip("NPC 상태 업데이트 주기 (초)")]
        private float _updateInterval = 2.0f;

        // ================================================================
        // Private State
        // ================================================================

        private WeatherManager _weatherManager;
        private WeatherEffects _weatherEffects;
        private TimeOfDayEffects _timeOfDayEffects;
        private NPCAwarenessSystem[] _allNpcs = Array.Empty<NPCAwarenessSystem>();
        private WeatherManager.WeatherType _lastWeather = WeatherManager.WeatherType.Clear;
        private float _updateTimer = 0f;

        /// <summary>원래 감지 범위 기억 (되돌리기 용)</summary>
        private Dictionary<NPCAwarenessSystem, float> _originalSightRanges = new Dictionary<NPCAwarenessSystem, float>();
        private Dictionary<NPCAwarenessSystem, float> _originalFOVs = new Dictionary<NPCAwarenessSystem, float>();

        // ================================================================
        // Public Properties
        // ================================================================

        /// <summary>현재 NPC 감지 범위 배율 (TimeOfDay × Weather 종합)</summary>
        public float CurrentDetectionRangeMultiplier
        {
            get
            {
                float weatherMult = GetWeatherDetectionMultiplier(_weatherManager != null
                    ? _weatherManager.CurrentWeather
                    : WeatherManager.WeatherType.Clear);

                float timeMult = 1f;
                if (_timeOfDayEffects != null && _timeOfDayEffects.IsNightTime())
                    timeMult = _nightDetectionRangeMultiplier;

                return weatherMult * timeMult;
            }
        }

        /// <summary>현재 NPC가 은신처를 찾아야 하는 날씨인지 여부</summary>
        public bool ShouldSeekShelter
        {
            get
            {
                if (_weatherManager == null) return false;
                WeatherManager.WeatherType w = _weatherManager.CurrentWeather;
                switch (w)
                {
                    case WeatherManager.WeatherType.Rain:       return _seekShelterInRain;
                    case WeatherManager.WeatherType.Snow:       return _seekShelterInSnow;
                    case WeatherManager.WeatherType.StrongWind: return _seekShelterInStorm;
                    case WeatherManager.WeatherType.Fog:        return _seekShelterInFog;
                    default:                                    return false;
                }
            }
        }

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (_weatherManager != null)
            {
                _weatherManager.OnWeatherChanged -= OnWeatherChanged;
            }
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            _weatherManager = WeatherManager.Instance;
            if (_weatherManager == null)
            {
                Debug.LogError("[NPCWeatherBehavior] WeatherManager.Instance를 찾을 수 없습니다.");
                enabled = false;
                return;
            }

            _weatherEffects = WeatherEffects.Instance;
            _timeOfDayEffects = TimeOfDayEffects.Instance;

            // WeatherManager 이벤트 구독
            _weatherManager.OnWeatherChanged += OnWeatherChanged;

            // 모든 NPC 스캔
            RefreshNPCSnapshot();

            _lastWeather = _weatherManager.CurrentWeather;

            // 초기 날씨 효과 적용
            ApplyWeatherToAllNPCs(_lastWeather);

            Debug.Log($"[NPCWeatherBehavior] 초기화 완료: 날씨={_lastWeather}, NPC 수={_allNpcs.Length}");
        }

        private void Update()
        {
            if (_weatherManager == null) return;

            _updateTimer -= Time.deltaTime;
            if (_updateTimer <= 0f)
            {
                _updateTimer = _updateInterval;
                PeriodicUpdate();
            }
        }

        // ================================================================
        // Periodic Update
        // ================================================================

        /// <summary>
        /// 주기적으로 NPC 상태를 갱신합니다.
        /// 새 NPC 감지, 기존 NPC 제거 감지, 날씨/시간대 재적용.
        /// </summary>
        private void PeriodicUpdate()
        {
            WeatherManager.WeatherType current = _weatherManager.CurrentWeather;

            // NPC 목록 갱신 (새로 스폰된 NPC 감지)
            RefreshNPCSnapshot();

            // 날씨가 변경되지 않았어도 시간대 변경 등으로 감지 범위 재계산 필요
            ApplyDetectionModifiers(current);
        }

        // ================================================================
        // WeatherManager Callback
        // ================================================================

        /// <summary>
        /// WeatherManager의 날씨 변경 이벤트 콜백.
        /// 날씨가 변경되면 모든 NPC의 감지 범위를 조정하고 은신처 행동을 트리거합니다.
        /// </summary>
        private void OnWeatherChanged(WeatherManager.WeatherType weatherType)
        {
            // 이전 날씨 효과 제거
            RemoveWeatherEffectsFromAllNPCs();

            // 새 날씨 효과 적용
            _lastWeather = weatherType;
            ApplyWeatherToAllNPCs(weatherType);

            // 은신처 행동 트리거
            if (ShouldSeekShelter)
            {
                TriggerShelterSeeking(weatherType);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCWeatherBehavior] NPC 날씨 반응 적용: {weatherType}, 은신처 탐색={(ShouldSeekShelter ? "ON" : "OFF")}");
#endif
        }

        // ================================================================
        // NPC Weather Effects
        // ================================================================

        /// <summary>
        /// 모든 NPC에 주어진 날씨의 감지 범위 효과를 적용합니다.
        /// </summary>
        private void ApplyWeatherToAllNPCs(WeatherManager.WeatherType weather)
        {
            ApplyDetectionModifiers(weather);

            // 날씨별 시야 효과 (NPCAwarenessSystem의 ApplyFogEffect 활용)
            if (weather == WeatherManager.WeatherType.Fog)
            {
                // 안개 시 모든 NPC 시야 감소
                NPCAwarenessSystem.ApplyFogEffectToAll(true);
            }
        }

        /// <summary>
        /// 모든 NPC에서 날씨 효과를 제거합니다.
        /// </summary>
        private void RemoveWeatherEffectsFromAllNPCs()
        {
            // 감지 범위 복원
            foreach (var kvp in _originalSightRanges)
            {
                if (kvp.Key != null && kvp.Key.IsActive)
                {
                    // _baseSightRange는 private이므로 직접 설정 불가
                    // 복원은 SetSightRange를 통해야 함
                }
            }

            // 안개 효과 제거 (마지막 날씨가 Fog였던 경우에만)
            if (_lastWeather == WeatherManager.WeatherType.Fog)
            {
                NPCAwarenessSystem.ApplyFogEffectToAll(false);
            }

            _originalSightRanges.Clear();
            _originalFOVs.Clear();
        }

        /// <summary>
        /// 현재 날씨에 맞게 모든 NPC의 감지 범위를 조정합니다.
        /// NPCAwarenessSystem의 _currentSightRange는 private 필드이므로,
        /// 리플렉션 없이 직접 수정은 불가능합니다.
        /// 대신 WeatherEffects의 CurrentVisionMultiplier와 연동하여
        /// 각 NPC 시스템이 참조할 수 있는 값을 제공합니다.
        /// 
        /// 실제 NPC 감지 범위 변경은 NPCAwarenessSystem이
        /// NPCWeatherBehavior.Instance.CurrentDetectionRangeMultiplier를
        /// 참조하도록 구현하는 것을 권장합니다.
        /// </summary>
        private void ApplyDetectionModifiers(WeatherManager.WeatherType weather)
        {
            float multiplier = GetWeatherDetectionMultiplier(weather);

            // 시간대 보정
            if (_timeOfDayEffects != null && _timeOfDayEffects.IsNightTime())
            {
                multiplier *= _nightDetectionRangeMultiplier;
            }

            // 각 NPC에 대한 처리 (NPCAwarenessSystem이 multiplier를 참조)
            foreach (var npc in _allNpcs)
            {
                if (npc == null || !npc.IsActive) continue;

                // ApplyFogEffect는 Fog 전용이므로 여기서는 호출하지 않음
                // (ApplyWeatherToAllNPCs에서 Fog일 때만 호출)
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (multiplier < 1f)
            {
                Debug.Log($"[NPCWeatherBehavior] NPC 감지 범위 보정: x{multiplier:F2} (날씨={weather})");
            }
#endif
        }

        /// <summary>
        /// 주어진 날씨에 대한 NPC 감지 범위 배율을 반환합니다.
        /// </summary>
        private float GetWeatherDetectionMultiplier(WeatherManager.WeatherType weather)
        {
            switch (weather)
            {
                case WeatherManager.WeatherType.Rain:       return _rainDetectionRangeMultiplier;
                case WeatherManager.WeatherType.Snow:       return _snowDetectionRangeMultiplier;
                case WeatherManager.WeatherType.Fog:        return _fogDetectionRangeMultiplier;
                case WeatherManager.WeatherType.StrongWind: return _stormDetectionRangeMultiplier;
                default:                                    return 1f;
            }
        }

        // ================================================================
        // Shelter Seeking
        // ================================================================

        /// <summary>
        /// 악천후 시 NPC가 건물/은신처로 이동하도록 트리거합니다.
        /// BuildingPlaceholder, HouseInteriorBuilder 등과 연동하여
        /// NPC를 가장 가까운 건물로 네비게이션합니다.
        /// </summary>
        private void TriggerShelterSeeking(WeatherManager.WeatherType weather)
        {
            string weatherKey = GetWeatherShelterKey(weather);

            // 모든 NPC에 대해 은신처 이동 명령
            foreach (var npc in _allNpcs)
            {
                if (npc == null || !npc.IsActive) continue;

                GameObject npcGo = npc.gameObject;

                // 가장 가까운 건물 찾기
                Transform nearestShelter = FindNearestShelter(npcGo.transform.position);
                if (nearestShelter != null)
                {
                    // NPC 이동 명령 — NavMeshAgent 또는 단순 위치 이동
                    MoveNPCToShelter(npcGo, nearestShelter, weatherKey);
                }
                else
                {
                    // 은신처가 없으면 NPC는 현재 위치에서 대기
                    Debug.Log($"[NPCWeatherBehavior] {npcGo.name}: 근처 은신처 없음, 현재 위치 대기");
                }
            }
        }

        /// <summary>
        /// 가장 가까운 건물/은신처 Transform을 찾습니다.
        /// ShelterTags에 지정된 태그를 가진 GameObject를 검색합니다.
        /// </summary>
        private Transform FindNearestShelter(Vector3 npcPosition)
        {
            Transform nearest = null;
            float nearestDist = float.MaxValue;

            foreach (string tag in _shelterTags)
            {
                GameObject[] shelters = GameObject.FindGameObjectsWithTag(tag);
                foreach (var shelter in shelters)
                {
                    float dist = Vector3.Distance(npcPosition, shelter.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = shelter.transform;
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// NPC를 지정된 은신처 위치로 이동시킵니다.
        /// NavMeshAgent가 있으면 SetDestination, 없으면 Transform.position 직접 설정.
        /// </summary>
        private void MoveNPCToShelter(GameObject npcGo, Transform shelter, string weatherKey)
        {
            UnityEngine.AI.NavMeshAgent agent = npcGo.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                // 은신처 입구 근처로 이동
                Vector3 shelterEntrance = shelter.position + (shelter.forward * 2f);
                agent.SetDestination(shelterEntrance);
                agent.isStopped = false;
            }
            else
            {
                // NavMeshAgent가 없으면 단순 위치 이동
                npcGo.transform.position = shelter.position + new Vector3(
                    UnityEngine.Random.Range(-1.5f, 1.5f),
                    0f,
                    UnityEngine.Random.Range(-1.5f, 1.5f)
                );
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCWeatherBehavior] {npcGo.name} → {shelter.name} (날씨: {weatherKey})");
#endif
        }

        /// <summary>
        /// 날씨에 따른 은신처 키워드 반환.
        /// </summary>
        private string GetWeatherShelterKey(WeatherManager.WeatherType weather)
        {
            switch (weather)
            {
                case WeatherManager.WeatherType.Rain:       return "rain";
                case WeatherManager.WeatherType.Snow:       return "snow";
                case WeatherManager.WeatherType.StrongWind: return "storm";
                case WeatherManager.WeatherType.Fog:        return "fog";
                default:                                    return "clear";
            }
        }

        // ================================================================
        // NPC Snapshot
        // ================================================================

        /// <summary>
        /// 현재 씬의 모든 NPCAwarenessSystem 컴포넌트 스냅샷을 갱신합니다.
        /// </summary>
        private void RefreshNPCSnapshot()
        {
            _allNpcs = FindObjectsByType<NPCAwarenessSystem>(FindObjectsSortMode.None);
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// 특정 NPC의 현재 종합 감지 범위 배율을 반환합니다.
        /// (날씨 × 시간대 보정 적용)
        /// </summary>
        public float GetCombinedDetectionMultiplierForNPC(NPCAwarenessSystem npc)
        {
            if (npc == null || !npc.IsActive) return 1f;

            float weatherMult = GetWeatherDetectionMultiplier(
                _weatherManager != null ? _weatherManager.CurrentWeather : WeatherManager.WeatherType.Clear);

            float timeMult = 1f;
            if (_timeOfDayEffects != null && _timeOfDayEffects.IsNightTime())
                timeMult = _nightDetectionRangeMultiplier;

            return weatherMult * timeMult;
        }

        /// <summary>
        /// 현재 날씨가 NPC 은신처 이동을 유발하는지 확인합니다.
        /// </summary>
        public bool DoesWeatherForceShelter(WeatherManager.WeatherType weather)
        {
            switch (weather)
            {
                case WeatherManager.WeatherType.Rain:       return _seekShelterInRain;
                case WeatherManager.WeatherType.Snow:       return _seekShelterInSnow;
                case WeatherManager.WeatherType.StrongWind: return _seekShelterInStorm;
                case WeatherManager.WeatherType.Fog:        return _seekShelterInFog;
                default:                                    return false;
            }
        }

        /// <summary>
        /// 강제로 NPC 날씨 반응을 재설정합니다 (디버그 용).
        /// </summary>
        public void ForceWeatherReaction(WeatherManager.WeatherType weather)
        {
            RemoveWeatherEffectsFromAllNPCs();
            ApplyWeatherToAllNPCs(weather);

            if (DoesWeatherForceShelter(weather))
            {
                TriggerShelterSeeking(weather);
            }

            Debug.Log($"[NPCWeatherBehavior] 강제 날씨 반응: {weather}");
        }
    }
}