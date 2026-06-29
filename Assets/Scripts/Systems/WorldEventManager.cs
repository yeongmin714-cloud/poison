using System;
using System.Collections.Generic;
using System.Linq;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI;
using UnityEngine;
using Random = UnityEngine.Random;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 36: 다이내믹 월드 이벤트 시스템.
    /// 30초~3분 간격으로 8종의 랜덤 이벤트를 발생시킵니다.
    /// 영지 상태(방어력/병사 수/시간/확률)를 조건으로 이벤트를 선정하고,
    /// 플레이어 위치 기반 지역 이벤트를 생성합니다.
    /// 성공/실패 결과에 따라 호감도/금화/아이템이 변화합니다.
    /// </summary>
    public class WorldEventManager : MonoBehaviour
    {
        public static WorldEventManager Instance { get; private set; }

        // ===== 이벤트 종류 =====
        public enum EventType
        {
            MonsterRaid,          // 🐺 몬스터 습격
            TravelingMerchant,    // 🎪 방랑 상인 행렬
            Plague,               // 🦠 역병 발생
            FireFestival,         // 🌾 풍년 축제
            AssassinationContract,// 🪧 암살 의뢰
            Fire,                 // 🏚️ 화재 발생
            RoyalEnvoy,           // 📬 왕실 사절
            Storm                 // 🌪️ 악천후
        }

        /// <summary>이벤트 상태</summary>
        public enum EventPhase
        {
            Inactive,    // 미발생
            Active,      // 진행 중 (플레이어 선택 가능)
            Resolved,    // 해결됨 (성공/실패 처리 완료)
            Expired      // 만료됨 (시간 초과 무시)
        }

        /// <summary>활성 이벤트 데이터</summary>
        public class ActiveEvent
        {
            public EventType type;
            public string territoryId;
            public string territoryName;
            public string description;
            public float startTime;
            public float duration;       // 이벤트 지속 시간 (초)
            public EventPhase phase;
            public bool playerAccepted;  // 플레이어가 수락했는지
            public bool succeeded;       // 성공 여부

            /// <summary>남은 시간 (초)</summary>
            public float RemainingTime => Mathf.Max(0f, duration - (Time.time - startTime));
            /// <summary>만료 여부</summary>
            public bool IsExpired => Time.time - startTime >= duration;
            /// <summary>진행률 (0~1)</summary>
            public float Progress => Mathf.Clamp01((Time.time - startTime) / duration);
        }

        // ===== 설정 =====
        [Header("이벤트 주기")]
        [SerializeField, Tooltip("최소 이벤트 체크 간격 (초)")]
        private float _checkIntervalMin = 30f;
        [SerializeField, Tooltip("최대 이벤트 체크 간격 (초)")]
        private float _checkIntervalMax = 180f; // 3분

        [Header("이벤트 기본 확률")]
        [SerializeField, Tooltip("이벤트 체크 시 이벤트 발생 기본 확률 (0~1)")]
        private float _baseEventChance = 0.4f;
        [SerializeField, Tooltip("동시 활성 최대 이벤트 수")]
        private int _maxActiveEvents = 3;
        [SerializeField, Tooltip("이벤트 기본 지속 시간 (초)")]
        private float _defaultEventDuration = 300f; // 5분

        [Header("몬스터 습격 조건")]
        [SerializeField, Tooltip("방어력 임계값: 이 값 이하이면 습격 확률 증가")]
        private float _raidDefenseThreshold = 15f;
        [SerializeField, Tooltip("병사 수 임계값: 이 값 이하이면 습격 확률 증가")]
        private int _raidSoldierThreshold = 10;

        [Header("보상/패널티")]
        [SerializeField, Tooltip("몬스터 습격 성공: 호감도 증가")]
        private int _monsterRaidAffinityReward = 20;
        [SerializeField, Tooltip("몬스터 습격 실패: 호감도 감소")]
        private int _monsterRaidAffinityPenalty = -20;
        [SerializeField, Tooltip("몬스터 습격 실패: 병사 손실 비율")]
        private float _monsterRaidSoldierLoss = 0.3f;

        [SerializeField, Tooltip("역병 치료 성공: 호감도 증가")]
        private int _plagueAffinityReward = 30;
        [SerializeField, Tooltip("역병 치료 실패: 호감도 감소")]
        private int _plagueAffinityPenalty = -30;
        [SerializeField, Tooltip("역병 치료 성공: 금화 보상")]
        private int _plagueGoldReward = 50;

        [SerializeField, Tooltip("화재 진화 성공: 호감도 증가")]
        private int _fireAffinityReward = 15;
        [SerializeField, Tooltip("화재 진화 실패: 호감도 감소")]
        private int _fireAffinityPenalty = -15;

        [SerializeField, Tooltip("방랑 상인 할인율 (0~1)")]
        private float _merchantDiscountRate = 0.2f;

        // ===== 내부 상태 =====
        private float _nextCheckTime;
        private readonly List<ActiveEvent> _activeEvents = new List<ActiveEvent>();

        // 플레이어 위치 참조
        private Transform _playerTransform;

        // 활성 이벤트 변경 이벤트
        public event Action<ActiveEvent> OnEventStarted;
        public event Action<ActiveEvent> OnEventResolved;
        public event Action<ActiveEvent> OnEventExpired;

        /// <summary>현재 활성 이벤트 목록 (읽기 전용)</summary>
        public IReadOnlyList<ActiveEvent> ActiveEvents => _activeEvents.AsReadOnly();

        /// <summary>최근에 해결된 이벤트 목록 (UI 표시용)</summary>
        public List<ActiveEvent> RecentResolvedEvents { get; } = new List<ActiveEvent>();

        // ================================================================
        // 생명주기
        // ================================================================

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

        private void Start()
        {
            _nextCheckTime = Time.time + Random.Range(_checkIntervalMin, _checkIntervalMax);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // 플레이어 위치 업데이트
            if (_playerTransform == null)
                TryFindPlayer();

            // 활성 이벤트 업데이트 (만료 체크)
            UpdateActiveEvents();

            // 정기 이벤트 체크
            if (Time.time >= _nextCheckTime)
            {
                _nextCheckTime = Time.time + Random.Range(_checkIntervalMin, _checkIntervalMax);
                TryTriggerEvent();
            }
        }

        // ================================================================
        // 이벤트 체크
        // ================================================================

        /// <summary>
        /// 이벤트 발생을 시도합니다.
        /// 기본 확률 체크 → 조건에 맞는 이벤트 선정 → 실행
        /// </summary>
        private void TryTriggerEvent()
        {
            // 동시 활성 이벤트 최대 개수 초과 시 스킵
            int activeCount = _activeEvents.Count(e => e.phase == EventPhase.Active);
            if (activeCount >= _maxActiveEvents)
            {
                return;
            }

            // 기본 확률 체크
            if (Random.value > _baseEventChance)
                return;

            // 영지 데이터베이스 확인
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var allDefinitions = db.GetAllDefinitions().ToList();
            if (allDefinitions == null || allDefinitions.Count == 0) return;

            // 가능한 이벤트 목록 수집
            var candidateEvents = new List<(EventType type, string territoryId, float weight)>();

            foreach (var def in allDefinitions)
            {
                string key = def.id.ToString();
                var state = db.GetState(key);
                if (state == null) continue;

                float currentSoldiers = def.guardCount * (state.guardAliveRatio);

                // 1. 몬스터 습격: 방어력 낮거나 병사 수 적은 영지
                float defensePower = CalculateDefensePower(def, state);
                if (defensePower <= _raidDefenseThreshold || currentSoldiers <= _raidSoldierThreshold)
                {
                    float weight = Mathf.Max(0.5f, (_raidDefenseThreshold - defensePower) / _raidDefenseThreshold);
                    weight += Mathf.Max(0.3f, (_raidSoldierThreshold - currentSoldiers) / _raidSoldierThreshold);
                    candidateEvents.Add((EventType.MonsterRaid, key, weight * 2f));
                }

                // 2. 화재: 모든 영지 5% 기본 확률
                if (Random.value < 0.05f)
                {
                    candidateEvents.Add((EventType.Fire, key, 1f));
                }
            }

            // 3. 방랑 상인: 모든 영지 15% 확률 (전역 이벤트)
            if (Random.value < 0.15f)
            {
                string randomTerritory = allDefinitions[Random.Range(0, allDefinitions.Count)].id.ToString();
                candidateEvents.Add((EventType.TravelingMerchant, randomTerritory, 1.5f));
            }

            // 4. 역병: 위생 관련 (랜덤)
            if (Random.value < 0.08f)
            {
                string randomTerritory = allDefinitions[Random.Range(0, allDefinitions.Count)].id.ToString();
                candidateEvents.Add((EventType.Plague, randomTerritory, 1.2f));
            }

            // 5. 풍년 축제: 점령 7일+ 영지 (게임 시간 시뮬레이션: 420초 = 7분)
            foreach (var def in allDefinitions)
            {
                string key = def.id.ToString();
                var state = db.GetState(key);
                if (state == null) continue;

                // 점령된 영지에 대해 축제 가능
                if (state.ownership == TerritoryOwnership.PlayerOwned || state.ownership == TerritoryOwnership.LordOwned)
                {
                    if (Random.value < 0.03f) // 3% 확률
                    {
                        candidateEvents.Add((EventType.FireFestival, key, 0.8f));
                    }
                }
            }

            // 6. 암살 의뢰: 적대 관계 영지 (호감도 낮음)
            foreach (var def in allDefinitions)
            {
                string key = def.id.ToString();
                var state = db.GetState(key);
                if (state == null) continue;

                if (state.loyaltyToPlayer < 30f && state.ownership == TerritoryOwnership.LordOwned)
                {
                    if (Random.value < 0.06f) // 6% 확률
                    {
                        candidateEvents.Add((EventType.AssassinationContract, key, 1.0f));
                    }
                }
            }

            // 7. 왕실 사절: 황제국 미점령 시
            foreach (var def in allDefinitions)
            {
                string key = def.id.ToString();
                if (def.nation == NationType.Empire)
                {
                    var state = db.GetState(key);
                    if (state != null && state.ownership != TerritoryOwnership.PlayerOwned)
                    {
                        if (Random.value < 0.05f) // 5% 확률
                        {
                            candidateEvents.Add((EventType.RoyalEnvoy, key, 1.0f));
                        }
                    }
                }
            }

            // 8. 악천후: 계절 전환 시 (30% 확률)
            if (Random.value < 0.08f)
            {
                string randomTerritory = allDefinitions[Random.Range(0, allDefinitions.Count)].id.ToString();
                candidateEvents.Add((EventType.Storm, randomTerritory, 0.5f));
            }

            // 후보 중 가중치 기반 선택
            if (candidateEvents.Count == 0)
                return;

            // 중복 제거 (같은 영지 + 같은 이벤트는 하나로)
            var deduplicated = candidateEvents
                .GroupBy(c => (c.type, c.territoryId))
                .Select(g => g.First())
                .ToList();

            float totalWeight = deduplicated.Sum(c => c.weight);
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var candidate in deduplicated)
            {
                cumulative += candidate.weight;
                if (roll <= cumulative)
                {
                    StartEvent(candidate.type, candidate.territoryId);
                    return;
                }
            }
        }

        // ================================================================
        // 이벤트 실행
        // ================================================================

        /// <summary>
        /// 이벤트를 시작합니다.
        /// </summary>
        private void StartEvent(EventType type, string territoryId)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var def = db.GetDefinition(territoryId);
            if (def.id.nation == NationType.None) return;

            string name = def.territoryName;
            string description = GetEventDescription(type, name, def.nation);

            var evt = new ActiveEvent
            {
                type = type,
                territoryId = territoryId,
                territoryName = name,
                description = description,
                startTime = Time.time,
                duration = _defaultEventDuration,
                phase = EventPhase.Active,
                playerAccepted = false,
                succeeded = false
            };

            _activeEvents.Add(evt);
            OnEventStarted?.Invoke(evt);

            Debug.Log($"[WorldEventManager] 🌍 이벤트 시작! {GetEventEmoji(type)} {type} — {name}({territoryId}): {description}");

            // DynamicEventUI에 알림
            DynamicEventUI.ShowEvent(evt);
        }

        /// <summary>
        /// 활성 이벤트를 업데이트합니다 (만료 체크).
        /// </summary>
        private void UpdateActiveEvents()
        {
            var expiredEvents = new List<ActiveEvent>();

            foreach (var evt in _activeEvents)
            {
                if (evt.phase == EventPhase.Active && evt.IsExpired)
                {
                    evt.phase = EventPhase.Expired;

                    // 플레이어가 무시한 경우 실패 처리
                    if (!evt.playerAccepted)
                    {
                        HandleEventFailure(evt);
                    }

                    expiredEvents.Add(evt);
                    OnEventExpired?.Invoke(evt);
                    Debug.Log($"[WorldEventManager] ⏰ 이벤트 만료: {GetEventEmoji(evt.type)} {evt.type} — {evt.territoryName}");
                }
            }

            foreach (var evt in expiredEvents)
            {
                _activeEvents.Remove(evt);
                RecentResolvedEvents.Add(evt);
                if (RecentResolvedEvents.Count > 10)
                    RecentResolvedEvents.RemoveAt(0);
            }
        }

        // ================================================================
        // 플레이어 액션
        // ================================================================

        /// <summary>
        /// 플레이어가 이벤트에 참여하기로 선택했습니다.
        /// [이동하기] 버튼 대응.
        /// </summary>
        public void AcceptEvent(ActiveEvent evt)
        {
            if (evt == null || evt.phase != EventPhase.Active) return;

            evt.playerAccepted = true;

            Debug.Log($"[WorldEventManager] 👤 플레이어 이벤트 수락: {GetEventEmoji(evt.type)} {evt.type}");

            // 이벤트 유형별 처리
            switch (evt.type)
            {
                case EventType.MonsterRaid:
                    // 몬스터 습격 방어 — 자동 성공 (추후 전투 시스템 연동 가능)
                    HandleMonsterRaidSuccess(evt);
                    break;

                case EventType.Plague:
                    // 역병 치료 — 자동 성공 (추후 퀘스트 연동 가능)
                    HandlePlagueSuccess(evt);
                    break;

                case EventType.Fire:
                    // 화재 진화 — 자동 성공
                    HandleFireSuccess(evt);
                    break;

                case EventType.TravelingMerchant:
                    // 방랑 상인 — 특별 UI 표시 (할인 상점)
                    Debug.Log($"[WorldEventManager] 🎪 방랑 상인 할인 상점 오픈! ({_merchantDiscountRate * 100}% 할인)");
                    evt.phase = EventPhase.Resolved;
                    evt.succeeded = true;
                    OnEventResolved?.Invoke(evt);
                    break;

                case EventType.FireFestival:
                    // 풍년 축제 — 버프 적용
                    Debug.Log($"[WorldEventManager] 🌾 {evt.territoryName} 풍년 축제 시작! 전쟁 중단 + 상점 할인");
                    evt.phase = EventPhase.Resolved;
                    evt.succeeded = true;
                    OnEventResolved?.Invoke(evt);
                    break;

                case EventType.AssassinationContract:
                    // 암살 의뢰 — 성공 (추후 암살 시스템 연동)
                    HandleAssassinationSuccess(evt);
                    break;

                case EventType.RoyalEnvoy:
                    // 왕실 사절 — 협력 수락
                    HandleRoyalEnvoySuccess(evt);
                    break;

                case EventType.Storm:
                    // 악천후 — 대비 (자연 현상, 큰 영향 없음)
                    evt.phase = EventPhase.Resolved;
                    evt.succeeded = true;
                    OnEventResolved?.Invoke(evt);
                    Debug.Log($"[WorldEventManager] 🌪️ 악천후 대비 완료. {evt.territoryName} 지역");
                    break;

                default:
                    evt.phase = EventPhase.Resolved;
                    evt.succeeded = true;
                    OnEventResolved?.Invoke(evt);
                    break;
            }
        }

        /// <summary>
        /// 플레이어가 이벤트를 무시했습니다.
        /// [무시] 버튼 대응.
        /// </summary>
        public void IgnoreEvent(ActiveEvent evt)
        {
            if (evt == null || evt.phase != EventPhase.Active) return;

            evt.playerAccepted = false;
            HandleEventFailure(evt);
            evt.phase = EventPhase.Resolved;
            _activeEvents.Remove(evt);
            RecentResolvedEvents.Add(evt);

            Debug.Log($"[WorldEventManager] 👤 플레이어 이벤트 무시: {GetEventEmoji(evt.type)} {evt.type} — {evt.territoryName}");
            OnEventResolved?.Invoke(evt);
        }

        // ================================================================
        // 성공/실패 처리
        // ================================================================

        /// <summary>
        /// 몬스터 습격 성공: 호감도 +20
        /// </summary>
        private void HandleMonsterRaidSuccess(ActiveEvent evt)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var state = db.GetState(evt.territoryId);
            if (state != null)
            {
                state.loyaltyToPlayer += _monsterRaidAffinityReward;
                Debug.Log($"[WorldEventManager] 🐺 몬스터 습격 방어 성공! {evt.territoryName} 호감도 +{_monsterRaidAffinityReward}");
            }

            // WarNotificationUI에 알림
            WarNotificationUI.ShowNotification(
                $"🐺 {evt.territoryName} 몬스터 습격 방어 성공! 호감도 +{_monsterRaidAffinityReward}",
                WarNotificationUI.NotificationType.Info);

            evt.phase = EventPhase.Resolved;
            evt.succeeded = true;
            OnEventResolved?.Invoke(evt);
        }

        /// <summary>
        /// 몬스터 습격 실패: 병사 30% 사망, 호감도 -20
        /// </summary>
        private void HandleMonsterRaidFailure(ActiveEvent evt)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var state = db.GetState(evt.territoryId);
            var def = db.GetDefinition(evt.territoryId);
            if (state != null)
            {
                // 병사 30% 사망
                state.guardAliveRatio = Mathf.Max(0.1f, state.guardAliveRatio - _monsterRaidSoldierLoss);
                // 호감도 감소
                state.loyaltyToPlayer = Mathf.Max(0f, state.loyaltyToPlayer + _monsterRaidAffinityPenalty);

                Debug.Log($"[WorldEventManager] 🐺 몬스터 습격 방어 실패! {evt.territoryName} 병사 {_monsterRaidSoldierLoss * 100}% 감소, 호감도 {_monsterRaidAffinityPenalty}");
            }

            WarNotificationUI.ShowNotification(
                $"🐺 {evt.territoryName} 몬스터 습격 피해! 병사 {_monsterRaidSoldierLoss * 100}% 손실, 호감도 {_monsterRaidAffinityPenalty}",
                WarNotificationUI.NotificationType.WarEnd);
        }

        /// <summary>
        /// 역병 치료 성공: 호감도 +30, 금화 50
        /// </summary>
        private void HandlePlagueSuccess(ActiveEvent evt)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var state = db.GetState(evt.territoryId);
            if (state != null)
            {
                state.loyaltyToPlayer += _plagueAffinityReward;
                Debug.Log($"[WorldEventManager] 🦠 역병 치료 성공! {evt.territoryName} 호감도 +{_plagueAffinityReward}, 금화 +{_plagueGoldReward}");
            }

            // 금화 지급 (인벤토리)
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.AddItem(PlayerInventory.Gold, _plagueGoldReward);
            }

            WarNotificationUI.ShowNotification(
                $"🦠 {evt.territoryName} 역병 치료 완료! 호감도 +{_plagueAffinityReward}, 금화 +{_plagueGoldReward}",
                WarNotificationUI.NotificationType.Info);

            evt.phase = EventPhase.Resolved;
            evt.succeeded = true;
            OnEventResolved?.Invoke(evt);
        }

        /// <summary>
        /// 역병 치료 실패: 호감도 -30
        /// </summary>
        private void HandlePlagueFailure(ActiveEvent evt)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var state = db.GetState(evt.territoryId);
            if (state != null)
            {
                state.loyaltyToPlayer = Mathf.Max(0f, state.loyaltyToPlayer + _plagueAffinityPenalty);
                Debug.Log($"[WorldEventManager] 🦠 역병 치료 실패! {evt.territoryName} 호감도 {_plagueAffinityPenalty}");
            }

            WarNotificationUI.ShowNotification(
                $"🦠 {evt.territoryName} 역병 확산! 호감도 {_plagueAffinityPenalty}",
                WarNotificationUI.NotificationType.WarEnd);
        }

        /// <summary>
        /// 화재 진화 성공: 호감도 +15
        /// </summary>
        private void HandleFireSuccess(ActiveEvent evt)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var state = db.GetState(evt.territoryId);
            if (state != null)
            {
                state.loyaltyToPlayer += _fireAffinityReward;
                Debug.Log($"[WorldEventManager] 🏚️ 화재 진화 성공! {evt.territoryName} 호감도 +{_fireAffinityReward}, 복구비 절약");
            }

            WarNotificationUI.ShowNotification(
                $"🏚️ {evt.territoryName} 화재 진화 완료! 호감도 +{_fireAffinityReward}",
                WarNotificationUI.NotificationType.Info);

            evt.phase = EventPhase.Resolved;
            evt.succeeded = true;
            OnEventResolved?.Invoke(evt);
        }

        /// <summary>
        /// 화재 진화 실패: 호감도 -15
        /// </summary>
        private void HandleFireFailure(ActiveEvent evt)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var state = db.GetState(evt.territoryId);
            if (state != null)
            {
                state.loyaltyToPlayer = Mathf.Max(0f, state.loyaltyToPlayer + _fireAffinityPenalty);
                Debug.Log($"[WorldEventManager] 🏚️ 화재 진화 실패! {evt.territoryName} 호감도 {_fireAffinityPenalty}, 복구비 2배");
            }

            WarNotificationUI.ShowNotification(
                $"🏚️ {evt.territoryName} 화재 피해! 호감도 {_fireAffinityPenalty}",
                WarNotificationUI.NotificationType.WarEnd);
        }

        /// <summary>
        /// 암살 의뢰 성공: 금화 + 희귀 아이템
        /// </summary>
        private void HandleAssassinationSuccess(ActiveEvent evt)
        {
            // 금화 보상
            int goldReward = Random.Range(30, 80);
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.AddItem(PlayerInventory.Gold, goldReward);
            }

            // 희귀 아이템 지급
            var rareItem = new PlayerInventory.ItemData
            {
                id = "event_assassin_reward",
                displayName = "암살 증표",
                description = "암살 의뢰 완료 증명. 특정 NPC에게 제시 시 추가 보상.",
                category = PlayerInventory.ItemCategory.Quest,
                maxStack = 1
            };

            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.AddItem(rareItem, 1);
            }

            Debug.Log($"[WorldEventManager] 🪧 암살 의뢰 완료! 금화 +{goldReward}, 희귀 아이템 획득");

            WarNotificationUI.ShowNotification(
                $"🪧 {evt.territoryName} 암살 의뢰 완료! 금화 +{goldReward}, 희귀 아이템 획득",
                WarNotificationUI.NotificationType.Info);

            evt.phase = EventPhase.Resolved;
            evt.succeeded = true;
            OnEventResolved?.Invoke(evt);
        }

        /// <summary>
        /// 왕실 사절 협력 성공: 정보 + 귀중품
        /// </summary>
        private void HandleRoyalEnvoySuccess(ActiveEvent evt)
        {
            int goldReward = Random.Range(40, 100);
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.AddItem(PlayerInventory.Gold, goldReward);
            }

            // 귀중품 아이템
            var treasureItem = new PlayerInventory.ItemData
            {
                id = "event_royal_seal",
                displayName = "황제국 인장",
                description = "황제국 사절이 남긴 인장. 귀중한 거래 증표.",
                category = PlayerInventory.ItemCategory.Quest,
                maxStack = 1,
                rarity = ItemRarity.Rare
            };

            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.AddItem(treasureItem, 1);
            }

            Debug.Log($"[WorldEventManager] 📬 왕실 사절 협력 수락! 금화 +{goldReward}, 황제국 인장 획득");

            WarNotificationUI.ShowNotification(
                $"📬 황제국 사절 협력! 금화 +{goldReward}, 귀중품 획득",
                WarNotificationUI.NotificationType.Info);

            evt.phase = EventPhase.Resolved;
            evt.succeeded = true;
            OnEventResolved?.Invoke(evt);
        }

        /// <summary>
        /// 이벤트 유형별 실패 처리를 수행합니다.
        /// </summary>
        private void HandleEventFailure(ActiveEvent evt)
        {
            switch (evt.type)
            {
                case EventType.MonsterRaid:
                    HandleMonsterRaidFailure(evt);
                    break;
                case EventType.Plague:
                    HandlePlagueFailure(evt);
                    break;
                case EventType.Fire:
                    HandleFireFailure(evt);
                    break;
                case EventType.AssassinationContract:
                    // 암살 의뢰 취소 (패널티 없음)
                    Debug.Log($"[WorldEventManager] 🪧 암살 의뢰 취소 — 기회 상실");
                    WarNotificationUI.ShowNotification(
                        "🪧 암살 의뢰 기한 만료 — 의뢰 취소됨",
                        WarNotificationUI.NotificationType.Info);
                    break;
                case EventType.RoyalEnvoy:
                    // 왕실 사절 거절 (기회 상실)
                    Debug.Log($"[WorldEventManager] 📬 왕실 사절 거절 — 기회 상실");
                    WarNotificationUI.ShowNotification(
                        "📬 왕실 사절이 떠났습니다 — 기회 상실",
                        WarNotificationUI.NotificationType.Info);
                    break;
                case EventType.TravelingMerchant:
                case EventType.FireFestival:
                case EventType.Storm:
                    // 자연 현상/혜택은 실패 없음
                    break;
                default:
                    break;
            }

            evt.succeeded = false;
        }

        // ================================================================
        // 헬퍼 메서드
        // ================================================================

        /// <summary>
        /// 영지 방어력을 계산합니다. (TerritoryWarManager와 유사한 로직)
        /// </summary>
        private float CalculateDefensePower(TerritoryDefinition def, TerritoryState state)
        {
            float currentSoldiers = def.guardCount * state.guardAliveRatio;

            float lordPower = (def.lord.loyalty / 100f) * 2f;
            lordPower += def.lord.personality switch
            {
                LordPersonality.Brave => 2.0f,
                LordPersonality.Wise => 1.5f,
                LordPersonality.Suspicious => 1.0f,
                LordPersonality.Cowardly => -1.0f,
                _ => 0f
            };

            float defenseBonus = def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => 1f,
                TerritoryDifficulty.Ring2 => 2f,
                TerritoryDifficulty.Ring3 => 4f,
                TerritoryDifficulty.Ring4 => 6f,
                TerritoryDifficulty.Empire => 12f,
                _ => 0f
            };

            return currentSoldiers + lordPower + defenseBonus;
        }

        /// <summary>
        /// 이벤트 유형별 설명을 반환합니다.
        /// </summary>
        private string GetEventDescription(EventType type, string territoryName, NationType nation)
        {
            return type switch
            {
                EventType.MonsterRaid =>
                    $"🐺 늑대 무리가 {territoryName}을(를) 습격했습니다! 빨리 방어하세요!",
                EventType.TravelingMerchant =>
                    $"🎪 방랑 상인 행렬이 {territoryName}에 도착했습니다! 특수 아이템을 20% 할인된 가격에 구매할 기회!",
                EventType.Plague =>
                    $"🦠 {territoryName}에 역병이 발생했습니다! 주민들이 위험에 빠졌습니다 — 치료가 필요합니다!",
                EventType.FireFestival =>
                    $"🌾 {territoryName}에서 풍년 축제가 열립니다! 전쟁이 잠시 중단되고, 상점이 할인됩니다!",
                EventType.AssassinationContract =>
                    $"🪧 {territoryName}의 영주를 암살해 달라는 의뢰가 들어왔습니다. 거액의 보상이 걸려 있습니다.",
                EventType.Fire =>
                    $"🏚️ {territoryName}에서 화재가 발생했습니다! 건물이 불타고 있습니다 — 진화가 필요합니다!",
                EventType.RoyalEnvoy =>
                    $"📬 황제국에서 사절이 도착했습니다. {nation} 지역 협력에 관한 제안을 가지고 있습니다.",
                EventType.Storm =>
                    $"🌪️ {territoryName}에 강력한 폭풍이 접근 중입니다! 이동이 어려워지고, 실내에 머무는 것이 좋습니다.",
                _ => $"🌍 {territoryName}에서 알 수 없는 이벤트가 발생했습니다."
            };
        }

        /// <summary>
        /// 이벤트 유형별 이모지를 반환합니다.
        /// </summary>
        public static string GetEventEmoji(EventType type)
        {
            return type switch
            {
                EventType.MonsterRaid => "🐺",
                EventType.TravelingMerchant => "🎪",
                EventType.Plague => "🦠",
                EventType.FireFestival => "🌾",
                EventType.AssassinationContract => "🪧",
                EventType.Fire => "🏚️",
                EventType.RoyalEnvoy => "📬",
                EventType.Storm => "🌪️",
                _ => "🌍"
            };
        }

        /// <summary>
        /// 이벤트 유형별 국문 이름을 반환합니다.
        /// </summary>
        public static string GetEventDisplayName(EventType type)
        {
            return type switch
            {
                EventType.MonsterRaid => "몬스터 습격",
                EventType.TravelingMerchant => "방랑 상인 행렬",
                EventType.Plague => "역병 발생",
                EventType.FireFestival => "풍년 축제",
                EventType.AssassinationContract => "암살 의뢰",
                EventType.Fire => "화재 발생",
                EventType.RoyalEnvoy => "왕실 사절",
                EventType.Storm => "악천후",
                _ => "알 수 없는 이벤트"
            };
        }

        /// <summary>
        /// 플레이어 Transform을 찾습니다.
        /// </summary>
        private void TryFindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        /// <summary>
        /// 현재 플레이어가 위치한 영지 ID를 반환합니다. (없으면 null)
        /// </summary>
        public string GetCurrentPlayerTerritory()
        {
            if (_playerTransform == null)
                return null;

            // TODO: 실제 영지 영역 충돌 체크 연동
            // 임시: 첫 번째 활성 이벤트 영지 반환
            foreach (var evt in _activeEvents)
            {
                if (evt.phase == EventPhase.Active)
                    return evt.territoryId;
            }
            return null;
        }

        /// <summary>
        /// 방랑 상인 할인율을 반환합니다.
        /// </summary>
        public float GetMerchantDiscountRate() => _merchantDiscountRate;

        // ================================================================
        // 테스트/디버그
        // ================================================================

        /// <summary>
        /// 특정 이벤트를 강제로 트리거합니다. (디버그/테스트용)
        /// </summary>
        public void ForceTriggerEvent(EventType type, string territoryId = null)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            if (string.IsNullOrEmpty(territoryId))
            {
                var allDefs = db.GetAllDefinitions().ToList();
                if (allDefs.Count == 0) return;
                territoryId = allDefs[Random.Range(0, allDefs.Count)].id.ToString();
            }

            StartEvent(type, territoryId);
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용)
        /// </summary>
        public void ResetAll()
        {
            _activeEvents.Clear();
            RecentResolvedEvents.Clear();
            _nextCheckTime = Time.time + Random.Range(_checkIntervalMin, _checkIntervalMax);
        }
    }
}
