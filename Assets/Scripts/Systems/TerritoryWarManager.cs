using System;
using System.Collections.Generic;
using System.Linq;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
using Random = UnityEngine.Random;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 3.6: AI 국가 간 전쟁 시스템.
    /// 30초~2분 간격으로 AI 국가 간 전쟁을 자동으로 발생시키고,
    /// 전투력 계산, 승패 판정, 영지 소유권 변경 및 국기 교체를 수행합니다.
    /// 
    /// AI 행동 패턴:
    /// - 동(East): 방어적 — 자국 방어 집중
    /// - 서(West): 기회주의적 — 혼란한 영지 우선 공격
    /// - 남(South): 공격적 — 적극적 확장
    /// - 북(North): 강경 — 가장 강력한 공격
    /// 
    /// 플레이어 영향 시스템:
    /// - 영주 암살 → 병사 50% 감소
    /// - 병사 지원 → 방어력 +30%
    /// - 플레이어 점령 영지는 AI 전쟁 대상 제외
    /// </summary>
    public class TerritoryWarManager : MonoBehaviour
    {
        public static TerritoryWarManager Instance { get; private set; }

        [Header("전쟁 주기 설정")]
        [SerializeField, Tooltip("최소 전쟁 간격 (초)")]
        private float _warIntervalMin = 30f;
        [SerializeField, Tooltip("최대 전쟁 간격 (초)")]
        private float _warIntervalMax = 120f;

        [Header("전투 계수")]
        [SerializeField, Tooltip("승리 조건 배수: 공격군 전투력 > 방어군 전투력 × 이 값")]
        private float _victoryMultiplier = 1.2f;
        [SerializeField, Tooltip("패배 시 공격군 병사 손실률")]
        private float _attackerLossRatio = 0.3f;
        [SerializeField, Tooltip("전투 시 방어군 병사 손실률")]
        private float _defenderLossRatio = 0.2f;
        [SerializeField, Tooltip("신규 점령지 방어력 배수 (60초간)")]
        private float _newConquestDefenseBoost = 2f;
        [SerializeField, Tooltip("신규 점령지 방어력 부스트 지속 시간 (초)")]
        private float _newConquestBoostDuration = 60f;

        [Header("플레이어 영향")]
        [SerializeField, Tooltip("영주 암살 시 병사 감소율")]
        private float _assassinationSoldierReduction = 0.5f;
        [SerializeField, Tooltip("병사 지원 시 방어력 증가율")]
        private float _supportDefenseBoost = 0.3f;

        // ===== 내부 상태 =====

        /// <summary>다음 전쟁 발생 예정 시간</summary>
        private float _nextWarTime;

        /// <summary>영지 ID → 현재 소유 국가 (전쟁을 통해 동적으로 변경됨)</summary>
        private readonly Dictionary<string, NationType> _currentNationOwners = new Dictionary<string, NationType>();

        /// <summary>신규 점령지 방어력 부스트 타이머 (영지 ID → 남은 시간)</summary>
        private readonly Dictionary<string, float> _conquestBoostTimers = new Dictionary<string, float>();

        // ===== 인접 국가 관계 =====
        // 동(East) ↔ 북(North), 남(South)
        // 서(West) ↔ 북(North), 남(South)
        // 남(South) ↔ 동(East), 서(West)
        // 북(North) ↔ 동(East), 서(West)
        // 황제국(Empire) ↔ 모든 국가
        private static readonly Dictionary<NationType, NationType[]> _adjacentNations = new Dictionary<NationType, NationType[]>
        {
            { NationType.East, new[] { NationType.North, NationType.South } },
            { NationType.West, new[] { NationType.North, NationType.South } },
            { NationType.South, new[] { NationType.East, NationType.West } },
            { NationType.North, new[] { NationType.East, NationType.West } },
            { NationType.Empire, new[] { NationType.East, NationType.West, NationType.South, NationType.North } }
        };

        // ===== 이벤트 =====

        /// <summary>영지 정복 시 발생 (territoryId, oldOwner, newOwner)</summary>
        public event Action<string, NationType, NationType> OnTerritoryConquered;

        /// <summary>전쟁 시작 시 발생 (territoryId, attacker, defender)</summary>
        public event Action<string, NationType, NationType> OnWarStart;

        /// <summary>전쟁 종료 시 발생 (territoryId, attacker, defender, attackerWon)</summary>
        public event Action<string, NationType, NationType, bool> OnWarEnd;

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
            _nextWarTime = Time.time + Random.Range(_warIntervalMin, _warIntervalMax);
            InitializeNationOwners();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            UpdateConquestBoosts();

            if (Time.time >= _nextWarTime)
            {
                ScheduleNextWar();
                TryTriggerWar();
            }
        }

        // ================================================================
        // 내부 상태 관리
        // ================================================================

        /// <summary>
        /// 초기 국가별 영지 소유권 설정 (TerritoryDefinition의 nation 필드 기반)
        /// </summary>
        private void InitializeNationOwners()
        {
            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                Debug.LogError("[TerritoryWarManager] TerritoryDatabase.Instance is null");
                return;
            }

            foreach (var def in db.GetAllDefinitions())
            {
                string key = def.id.ToString();
                _currentNationOwners[key] = def.nation;
            }

            Debug.Log($"[TerritoryWarManager] 초기화 완료: {_currentNationOwners.Count}개 영지 소유권 등록");
        }

        /// <summary>
        /// 다음 전쟁 시간을 예약합니다.
        /// </summary>
        private void ScheduleNextWar()
        {
            _nextWarTime = Time.time + Random.Range(_warIntervalMin, _warIntervalMax);
        }

        /// <summary>
        /// 신규 점령지 방어력 부스트 타이머를 업데이트합니다.
        /// </summary>
        private void UpdateConquestBoosts()
        {
            if (_conquestBoostTimers.Count == 0)
                return;

            var expiredKeys = new List<string>();
            foreach (var kvp in _conquestBoostTimers)
            {
                _conquestBoostTimers[kvp.Key] = kvp.Value - Time.deltaTime;
                if (_conquestBoostTimers[kvp.Key] <= 0f)
                    expiredKeys.Add(kvp.Key);
            }

            foreach (var key in expiredKeys)
            {
                _conquestBoostTimers.Remove(key);
                Debug.Log($"[TerritoryWarManager] ⏳ {key} 방어력 부스트 만료");
            }
        }

        // ================================================================
        // 전쟁 트리거
        // ================================================================

        /// <summary>
        /// AI 국가 간 전쟁을 발생시킵니다.
        /// 공격 국가 선정 → 대상 영지 선정 → 전투 수행
        /// </summary>
        private void TryTriggerWar()
        {
            // AI 국가 리스트 (동/서/남/북 — Empire와 Dracula는 AI 전쟁 미참여)
            var aiNations = new[] { NationType.East, NationType.West, NationType.South, NationType.North };

            // 1. 공격 국가 선정 (행동 패턴 반영)
            NationType attacker = SelectAttacker(aiNations);
            if (attacker == NationType.None)
            {
                Debug.Log("[TerritoryWarManager] 공격 가능한 국가 없음 — 전쟁 스킵");
                return;
            }

            // 2. 공격자의 인접 적국 영지 중 공격 대상 선정
            if (!TrySelectTarget(attacker, out string targetTerritoryId, out NationType defender))
            {
                Debug.Log($"[TerritoryWarManager] {attacker}의 공격 대상 없음 — 전쟁 스킵");
                return;
            }

            // 3. 전투 수행
            ExecuteWar(attacker, defender, targetTerritoryId);
        }

        // ================================================================
        // 공격 국가 선정
        // ================================================================

        /// <summary>
        /// AI 행동 패턴에 따라 공격 국가를 선정합니다.
        /// 가중치 기반 랜덤 선택으로 각 국가의 성향을 반영합니다.
        /// </summary>
        private NationType SelectAttacker(NationType[] aiNations)
        {
            var candidates = new List<NationType>();
            var weights = new List<float>();

            foreach (var nation in aiNations)
            {
                // 영토가 하나도 없으면 공격 불가
                if (GetNationTerritoryCount(nation) == 0)
                    continue;

                float weight = GetAggressionWeight(nation);
                if (weight > 0.01f)
                {
                    candidates.Add(nation);
                    weights.Add(weight);
                }
            }

            if (candidates.Count == 0)
                return NationType.None;

            // 가중치 기반 랜덤 선택
            float totalWeight = weights.Sum();
            float randomValue = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                if (randomValue <= cumulative)
                    return candidates[i];
            }

            return candidates[^1];
        }

        /// <summary>
        /// 국가별 행동 패턴 기반 공격 가중치를 반환합니다.
        /// 각 국가의 성향과 현재 군사력/영토 상황을 고려합니다.
        /// </summary>
        private float GetAggressionWeight(NationType nation)
        {
            int territoryCount = GetNationTerritoryCount(nation);
            float totalSoldiers = GetNationTotalSoldiers(nation);

            // ===== 기본 공격 가중치 (행동 패턴 기반) =====
            float baseWeight = nation switch
            {
                NationType.East => 0.3f,    // 동: 방어적 — 낮은 공격성
                NationType.West => 0.6f,    // 서: 기회주의적 — 중간 공격성
                NationType.South => 0.9f,   // 남: 공격적 — 높은 공격성
                NationType.North => 0.7f,   // 북: 강경 — 높은 공격성
                _ => 0.5f
            };

            // ===== 동(East): 방어적 =====
            // 자국 영토가 적을수록 방어에 집중 (공격 가중치 감소)
            if (nation == NationType.East)
            {
                if (territoryCount < 5)
                    baseWeight *= 0.3f; // 거의 공격하지 않음
                else if (territoryCount < 10)
                    baseWeight *= 0.6f;
                else
                    baseWeight *= 1.2f; // 영토가 많으면 방어선 확보 차원에서 공격
            }

            // ===== 서(West): 기회주의적 =====
            // 적은 병력으로 많은 영토를 가졌으면 기회 포착 (가중치 증가)
            if (nation == NationType.West)
            {
                float soldierPerTerritory = territoryCount > 0 ? totalSoldiers / territoryCount : 0f;
                if (soldierPerTerritory < 3f && territoryCount > 8)
                    baseWeight *= 1.8f; // 약한 상대 공략 기회
                else if (soldierPerTerritory > 10f)
                    baseWeight *= 0.7f; // 병력이 충분하면 신중
            }

            // ===== 남(South): 공격적 =====
            // 병력이 많을수록 더 공격적
            if (nation == NationType.South)
            {
                float strengthRatio = Mathf.Clamp(totalSoldiers / 50f, 0.3f, 2.5f);
                baseWeight *= strengthRatio;
            }

            // ===== 북(North): 강경 =====
            // 병력이 많을수록 가중치 증가, 적어도 공격적 성향 유지
            if (nation == NationType.North)
            {
                float strengthRatio = Mathf.Clamp(totalSoldiers / 40f, 0.6f, 2.0f);
                baseWeight *= strengthRatio;
            }

            return baseWeight;
        }

        // ================================================================
        // 대상 영지 선정
        // ================================================================

        /// <summary>
        /// 공격 대상 영지를 선정합니다.
        /// 인접한 적국 영지 중 방어력이 약한 영지를 우선으로 하되,
        /// 국가별 행동 패턴을 반영한 점수로 선정합니다.
        /// </summary>
        private bool TrySelectTarget(NationType attacker, out string targetTerritoryId, out NationType defender)
        {
            targetTerritoryId = null;
            defender = NationType.None;

            var db = TerritoryDatabase.Instance;
            if (db == null) return false;

            // 공격자의 모든 영지 ID 수집
            var attackerTerritories = GetNationTerritoryIds(attacker);
            if (attackerTerritories.Count == 0) return false;

            // 인접한 적국 영지 수집 및 점수 계산
            var candidateTargets = new List<(string id, NationType owner, float score)>();

            foreach (string atkTerritoryId in attackerTerritories)
            {
                var adjTerritories = GetAdjacentTerritories(atkTerritoryId);
                foreach (string adjId in adjTerritories)
                {
                    if (string.IsNullOrEmpty(adjId)) continue;

                    NationType adjOwner = GetCurrentNationOwner(adjId);

                    // 같은 국가, 미소속, 황제국은 공격 대상 제외
                    if (adjOwner == attacker || adjOwner == NationType.None)
                        continue;

                    // 플레이어 점령 영지는 AI 전쟁 대상 제외
                    var state = db.GetState(adjId);
                    if (state != null && state.ownership == TerritoryOwnership.PlayerOwned)
                        continue;

                    float score = CalculateTargetScore(adjId, adjOwner, attacker);
                    candidateTargets.Add((adjId, adjOwner, score));
                }
            }

            // 인접 적국이 없으면 Empire 공격 시도
            if (candidateTargets.Count == 0)
            {
                return TrySelectEmpireTarget(attacker, out targetTerritoryId, out defender);
            }

            // 점수 기반 선택 (높은 점수 = 공격 우선순위 높음)
            candidateTargets.Sort((a, b) => b.score.CompareTo(a.score));

            // 상위 3개 또는 전체 중 랜덤 선택 (약간의 변동성)
            int selectCount = Mathf.Min(3, candidateTargets.Count);
            int selectedIndex = Random.Range(0, selectCount);

            var selected = candidateTargets[selectedIndex];
            targetTerritoryId = selected.id;
            defender = selected.owner;

            Debug.Log($"[TerritoryWarManager] 🎯 {attacker} → {defender} {targetTerritoryId} (점수: {selected.score:F1})");
            return true;
        }

        /// <summary>
        /// Empire(황제국) 공격 시도.
        /// Ring4 영지를 가진 국가만 Empire와 인접하므로 공격 가능합니다.
        /// </summary>
        private bool TrySelectEmpireTarget(NationType attacker, out string targetId, out NationType defender)
        {
            targetId = null;
            defender = NationType.None;

            // Empire는 자국 영토를 공격하지 않음
            if (attacker == NationType.Empire)
                return false;

            // Ring4 영지(인덱스 16-20)를 가진 경우에만 Empire 인접
            for (int i = 16; i <= 20; i++)
            {
                string territoryKey = new TerritoryId(attacker, i).ToString();
                var adjTerritories = GetAdjacentTerritories(territoryKey);
                foreach (string adjId in adjTerritories)
                {
                    if (string.IsNullOrEmpty(adjId)) continue;

                    NationType owner = GetCurrentNationOwner(adjId);
                    if (owner == NationType.Empire)
                    {
                        targetId = adjId;
                        defender = NationType.Empire;
                        Debug.Log($"[TerritoryWarManager] 🎯 {attacker} → Empire {targetId} (Empire 공격!)");
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 공격 대상 영지의 점수를 계산합니다.
        /// 방어력이 약할수록 높은 점수를 부여하며, 국가별 행동 패턴을 반영합니다.
        /// </summary>
        private float CalculateTargetScore(string targetId, NationType targetOwner, NationType attacker)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return 0f;

            var def = db.GetDefinition(targetId);
            var state = db.GetState(targetId);
            if (def.id.nation == NationType.None) return 0f;

            float currentSoldiers = def.guardCount * (state?.guardAliveRatio ?? 1f);
            float defense = CalculateDefensePower(targetId, targetOwner);

            // 신규 점령지 방어력 부스트 고려
            bool hasBoost = _conquestBoostTimers.ContainsKey(targetId);
            float effectiveDefense = hasBoost ? defense * _newConquestDefenseBoost : defense;

            // 기본 점수: 방어력에 반비례 (약할수록 높은 점수)
            float baseScore = 100f / Mathf.Max(effectiveDefense, 1f);

            // ===== 동(East): 방어적 =====
            // 공격 점수 자체가 낮게 유지됨 (GetAggressionWeight에서 처리)
            if (attacker == NationType.East)
            {
                // 방어력이 매우 약한 영지만 공격
                if (effectiveDefense > 15f)
                    baseScore *= 0.3f;
            }

            // ===== 서(West): 기회주의적 =====
            // 병사 손실이 많은 영지(guardAliveRatio 낮음) 우선 공격
            if (attacker == NationType.West)
            {
                float lossRatio = 1f - (state?.guardAliveRatio ?? 1f);
                // 병사가 많이 죽은 영지에 가중치
                baseScore *= (1f + lossRatio * 3f);

                // 방어력 부스트 없는 영지 선호
                if (!hasBoost)
                    baseScore *= 1.2f;
            }

            // ===== 남(South): 공격적 =====
            // 적극적 확장 — 전방위 공격, 약간의 랜덤성 추가
            if (attacker == NationType.South)
            {
                baseScore *= 1.5f; // 전반적 공격성 증가
                // 방어 부스트 무시 (공격적이라 덜 신경씀)
                if (hasBoost)
                    baseScore *= 0.8f; // 약간만 페널티
            }

            // ===== 북(North): 강경 =====
            // 가장 강력한 공격 — 방어력 높은 영지도 공격 (점수 페널티 감소)
            if (attacker == NationType.North)
            {
                baseScore *= 1.4f; // 높은 공격 의지
                // 방어 부스트 거의 무시
                if (hasBoost)
                    baseScore *= 0.9f;
            }

            return baseScore;
        }

        // ================================================================
        // 전투 실행
        // ================================================================

        /// <summary>
        /// 전투를 실행합니다.
        /// 전투력 계산 → 승패 판정 → 결과 처리
        /// </summary>
        private void ExecuteWar(NationType attacker, NationType defender, string territoryId)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var def = db.GetDefinition(territoryId);
            var state = db.GetState(territoryId);
            if (def.id.nation == NationType.None || state == null)
            {
                Debug.LogWarning($"[TerritoryWarManager] 유효하지 않은 영지: {territoryId}");
                return;
            }

            string territoryName = def.territoryName;

            // ===== 전쟁 시작 =====
            Debug.Log($"[TerritoryWarManager] ⚔️ {attacker} → {defender}: {territoryName}({territoryId}) 침공!");

            // 전쟁 중 상태 플래그 설정
            state.isUnderAttack = true;

            // 국기 반기(半旗) 상태 설정
            if (FlagManager.Instance != null)
            {
                FlagManager.Instance.SetContestedState(territoryId, true);
            }

            // 이벤트 발생
            OnWarStart?.Invoke(territoryId, attacker, defender);

            // 전쟁 알림
            WarNotificationUI.ShowNotification(
                $"⚔️ {attacker} → {defender}: {territoryName} 침공!",
                WarNotificationUI.NotificationType.WarStart);

            // ===== 전투력 계산 =====
            float attackPower = CalculateAttackPower(attacker);
            float defensePower = CalculateDefensePower(territoryId, defender);

            // 신규 점령지 방어력 2배 부스트 적용
            if (_conquestBoostTimers.ContainsKey(territoryId))
            {
                defensePower *= _newConquestDefenseBoost;
                Debug.Log($"[TerritoryWarManager] 🛡️ {territoryId} 방어력 부스트 적용 중! (x{_newConquestDefenseBoost})");
            }

            Debug.Log($"[TerritoryWarManager] 전투력: {attacker} 공격 {attackPower:F1} vs {defender} 방어 {defensePower:F1} (조건: {attackPower:F1} > {defensePower * _victoryMultiplier:F1})");

            // ===== 승패 판정 =====
            // 승리 조건: 공격군 전투력 > 방어군 전투력 × 1.2
            bool attackerWins = attackPower > defensePower * _victoryMultiplier;

            if (attackerWins)
            {
                HandleVictory(territoryId, territoryName, attacker, defender, state, def);
            }
            else
            {
                HandleDefeat(territoryId, territoryName, attacker, defender, state, def);
            }
        }

        // ================================================================
        // 전투력 계산
        // ================================================================

        /// <summary>
        /// 공격군 전투력을 계산합니다.
        /// 공격 국가의 모든 영지 평균 병사 수 + 영주 능력치
        /// </summary>
        private float CalculateAttackPower(NationType attacker)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return 0f;

            float totalPower = 0f;
            int territoryCount = 0;

            foreach (var def in db.GetAllDefinitions())
            {
                string key = def.id.ToString();
                if (GetCurrentNationOwner(key) == attacker)
                {
                    var state = db.GetState(def.id);
                    float currentSoldiers = def.guardCount * (state?.guardAliveRatio ?? 1f);

                    // 영주 능력치 (충성도 기반, 0~2 범위)
                    float lordPower = (def.lord.loyalty / 100f) * 2f;

                    // 영주 성격 보정
                    lordPower += def.lord.personality switch
                    {
                        LordPersonality.Brave => 1.5f,
                        LordPersonality.Wise => 1.0f,
                        LordPersonality.Cruel => 1.0f,
                        LordPersonality.Greedy => 0.5f,
                        LordPersonality.Suspicious => 0.3f,
                        LordPersonality.Cowardly => -0.5f,
                        _ => 0f
                    };

                    totalPower += currentSoldiers + lordPower;
                    territoryCount++;
                }
            }

            // 평균 전투력 반환 (영토 수로 나누어 대표값 산출)
            return territoryCount > 0 ? totalPower / territoryCount : 0f;
        }

        /// <summary>
        /// 방어군 전투력을 계산합니다.
        /// 대상 영지의 병사 수 + 영주 능력치 + 방어 보너스
        /// </summary>
        private float CalculateDefensePower(string territoryId, NationType defender)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return 0f;

            var def = db.GetDefinition(territoryId);
            var state = db.GetState(territoryId);
            if (def.id.nation == NationType.None) return 0f;

            // 병사 수 (생존 비율 반영)
            float currentSoldiers = def.guardCount * (state?.guardAliveRatio ?? 1f);

            // 영주 능력치 (충성도 기반, 0~2 범위)
            float lordPower = (def.lord.loyalty / 100f) * 2f;

            // 영주 성격 보정 (방어 시)
            lordPower += def.lord.personality switch
            {
                LordPersonality.Brave => 2.0f,    // 용감: 방어에 강함
                LordPersonality.Wise => 1.5f,     // 현명: 전략적 방어
                LordPersonality.Suspicious => 1.0f, // 의심 많음: 경계 강화
                LordPersonality.Cowardly => -1.0f,  // 겁많음: 방어 약화
                _ => 0f
            };

            // 방어 보너스 (Ring 내부일수록 요새화)
            float defenseBonus = def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => 1f,
                TerritoryDifficulty.Ring2 => 2f,
                TerritoryDifficulty.Ring3 => 4f,
                TerritoryDifficulty.Ring4 => 6f,
                TerritoryDifficulty.Empire => 12f,  // 황제국은 막강한 방어
                _ => 0f
            };

            return currentSoldiers + lordPower + defenseBonus;
        }

        // ================================================================
        // 전투 결과 처리
        // ================================================================

        /// <summary>
        /// 승리 처리: 영지 소유권 변경 + 국기 교체 + 방어력 부스트
        /// </summary>
        private void HandleVictory(string territoryId, string territoryName,
            NationType attacker, NationType defender,
            TerritoryState state, TerritoryDefinition def)
        {
            // ===== 공격군 병사 손실 =====
            // 승리에도 20% 손실 (전투의 대가)
            // 공격 국가의 무작위 영지 하나에 손실 적용
            var attackerTerritories = GetNationTerritoryIds(attacker);
            if (attackerTerritories.Count > 0)
            {
                var db = TerritoryDatabase.Instance;
                if (db != null)
                {
                    string randomTerritory = attackerTerritories[Random.Range(0, attackerTerritories.Count)];
                    var attackerState = db.GetState(randomTerritory);
                    if (attackerState != null)
                    {
                        attackerState.guardAliveRatio = Mathf.Max(0.2f, attackerState.guardAliveRatio - 0.2f);
                    }
                }
            }

            // ===== 영지 소유권 변경 =====
            NationType oldOwner = GetCurrentNationOwner(territoryId);
            _currentNationOwners[territoryId] = attacker;
            state.ownership = TerritoryOwnership.LordOwned;

            // 전쟁 상태 해제
            state.isUnderAttack = false;

            // ===== 국기 즉시 교체 =====
            if (FlagManager.Instance != null)
            {
                // 소속 국가의 국기로 교체
                FlagManager.Instance.OnTerritoryOwnershipChanged(territoryId, attacker);
                // 반기 상태 해제 (정상 게양)
                FlagManager.Instance.SetContestedState(territoryId, false);
            }

            // ===== 신규 점령지 방어력 2배 버프 (60초) =====
            _conquestBoostTimers[territoryId] = _newConquestBoostDuration;

            // ===== 알림 =====
            Debug.Log($"[TerritoryWarManager] 🏁 승리! {attacker}가 {defender}의 {territoryName}을(를) 정복했습니다! ({territoryId})");
            WarNotificationUI.ShowNotification(
                $"🏁 {attacker} → {defender}: {territoryName} 정복!",
                WarNotificationUI.NotificationType.TerritoryGained);

            // 이벤트 발생
            OnWarEnd?.Invoke(territoryId, attacker, defender, true);
            OnTerritoryConquered?.Invoke(territoryId, oldOwner, attacker);
        }

        /// <summary>
        /// 패배 처리: 공격군 병사 30% 손실 + 방어군 병사 20% 손실
        /// </summary>
        private void HandleDefeat(string territoryId, string territoryName,
            NationType attacker, NationType defender,
            TerritoryState state, TerritoryDefinition def)
        {
            var db = TerritoryDatabase.Instance;

            // ===== 공격군 병사 30% 손실 =====
            // 공격 국가의 무작위 영지에 손실 적용
            var attackerTerritories = GetNationTerritoryIds(attacker);
            if (attackerTerritories.Count > 0 && db != null)
            {
                // 1~2개 영지에 손실 분산
                int lossCount = Mathf.Min(2, attackerTerritories.Count);
                for (int i = 0; i < lossCount; i++)
                {
                    string targetTerritory = attackerTerritories[Random.Range(0, attackerTerritories.Count)];
                    var attackerState = db.GetState(targetTerritory);
                    if (attackerState != null)
                    {
                        attackerState.guardAliveRatio = Mathf.Max(0.1f, attackerState.guardAliveRatio - _attackerLossRatio / lossCount);
                    }
                }
            }

            // ===== 방어군 병사 20% 손실 =====
            // 방어 국가의 모든 영지에 손실 적용 (전쟁의 대가)
            if (db != null)
            {
                foreach (var defEntry in db.GetAllDefinitions())
                {
                    string key = defEntry.id.ToString();
                    if (GetCurrentNationOwner(key) == defender)
                    {
                        var defenderState = db.GetState(defEntry.id);
                        if (defenderState != null)
                        {
                            defenderState.guardAliveRatio = Mathf.Max(0.2f, defenderState.guardAliveRatio - _defenderLossRatio);
                        }
                    }
                }
            }

            // ===== 전쟁 상태 해제 =====
            state.isUnderAttack = false;

            // ===== 국기 정상 게양 (반기 해제) =====
            if (FlagManager.Instance != null)
            {
                FlagManager.Instance.SetContestedState(territoryId, false);
            }

            // ===== 알림 =====
            Debug.Log($"[TerritoryWarManager] ❌ 패배! {attacker}의 {territoryName} 공격 실패! (방어군 {defender})");
            WarNotificationUI.ShowNotification(
                $"❌ {attacker} → {defender}: {territoryName} 공격 실패!",
                WarNotificationUI.NotificationType.WarEnd);

            // 이벤트 발생
            OnWarEnd?.Invoke(territoryId, attacker, defender, false);
        }

        // ================================================================
        // 플레이어 영향 시스템
        // ================================================================

        /// <summary>
        /// 영주 암살 시 해당 영지 병사 50% 감소.
        /// 암살로 인한 혼란으로 병사들이 이탈합니다.
        /// </summary>
        /// <param name="territoryId">암살이 발생한 영지 ID</param>
        public void OnLordAssassinated(string territoryId)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var state = db.GetState(territoryId);
            if (state == null)
            {
                Debug.LogWarning($"[TerritoryWarManager] OnLordAssassinated: 유효하지 않은 영지 {territoryId}");
                return;
            }

            // 병사 50% 감소
            state.guardAliveRatio *= (1f - _assassinationSoldierReduction);

            Debug.Log($"[TerritoryWarManager] 🗡️ 영주 암살! {territoryId} 병사 {_assassinationSoldierReduction * 100}% 감소 → 생존율 {state.guardAliveRatio * 100:F0}%");
            WarNotificationUI.ShowNotification(
                $"🗡️ {territoryId} 영주 암살 — 병사 {_assassinationSoldierReduction * 100}% 감소!",
                WarNotificationUI.NotificationType.Info);
        }

        /// <summary>
        /// 병사 지원 시 해당 영지 방어력 +30%.
        /// guardAliveRatio를 증가시켜 시뮬레이션합니다.
        /// </summary>
        /// <param name="territoryId">지원받은 영지 ID</param>
        public void OnReinforceTerritory(string territoryId)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var state = db.GetState(territoryId);
            if (state == null)
            {
                Debug.LogWarning($"[TerritoryWarManager] OnReinforceTerritory: 유효하지 않은 영지 {territoryId}");
                return;
            }

            // 병사 비율 증가로 방어력 향상 시뮬레이션
            state.guardAliveRatio = Mathf.Min(1f, state.guardAliveRatio + _supportDefenseBoost);

            Debug.Log($"[TerritoryWarManager] 🛡️ {territoryId} 병사 지원 — 방어력 +{_supportDefenseBoost * 100}% → 생존율 {state.guardAliveRatio * 100:F0}%");
            WarNotificationUI.ShowNotification(
                $"🛡️ {territoryId} 병사 지원 — 방어력 +{_supportDefenseBoost * 100}%!",
                WarNotificationUI.NotificationType.Info);
        }

        /// <summary>
        /// 플레이어가 영지를 점령했을 때 호출하여 AI 전쟁 대상에서 제외합니다.
        /// </summary>
        /// <param name="territoryId">플레이어가 점령한 영지 ID</param>
        public void RegisterPlayerTerritory(string territoryId)
        {
            if (string.IsNullOrEmpty(territoryId))
                return;

            // TrySelectTarget에서 state.ownership == PlayerOwned 체크로 제외됨
            Debug.Log($"[TerritoryWarManager] 👤 플레이어 영지 등록: {territoryId} — AI 전쟁 대상에서 제외");

            // 플레이어가 점령했으므로 소유권 업데이트
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                var state = db.GetState(territoryId);
                if (state != null)
                {
                    state.ownership = TerritoryOwnership.PlayerOwned;
                }
            }
        }

        // ================================================================
        // 헬퍼 메서드
        // ================================================================

        /// <summary>
        /// 현재 영지의 소유 국가를 반환합니다.
        /// </summary>
        public NationType GetCurrentNationOwner(string territoryId)
        {
            if (!string.IsNullOrEmpty(territoryId) && _currentNationOwners.TryGetValue(territoryId, out var owner))
                return owner;
            return NationType.None;
        }

        /// <summary>
        /// 특정 국가의 모든 영지 ID 목록을 반환합니다.
        /// </summary>
        public List<string> GetNationTerritoryIds(NationType nation)
        {
            var result = new List<string>();
            foreach (var kvp in _currentNationOwners)
            {
                if (kvp.Value == nation)
                    result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// 특정 국가의 현재 영토 수를 반환합니다.
        /// </summary>
        public int GetNationTerritoryCount(NationType nation)
        {
            int count = 0;
            foreach (var kvp in _currentNationOwners)
            {
                if (kvp.Value == nation)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 특정 국가의 총 병사 수를 반환합니다. (실시간 생존율 반영)
        /// </summary>
        public float GetNationTotalSoldiers(NationType nation)
        {
            float total = 0f;
            var db = TerritoryDatabase.Instance;
            if (db == null) return 0f;

            foreach (var def in db.GetAllDefinitions())
            {
                string key = def.id.ToString();
                if (GetCurrentNationOwner(key) == nation)
                {
                    var state = db.GetState(def.id);
                    float ratio = state?.guardAliveRatio ?? 1f;
                    total += def.guardCount * ratio;
                }
            }
            return total;
        }

        // ================================================================
        // 인접 영지 시스템
        // ================================================================

        /// <summary>
        /// 특정 영지의 인접 영지 목록을 반환합니다.
        /// 
        /// 인접 규칙:
        /// 1. 같은 국가 내: 같은 Ring 내 ±1 인덱스, ±5 인덱스 (다음/이전 Ring)
        /// 2. 인접 국가: 동일 Ring 레벨의 모든 영지
        /// 3. Ring4 ↔ Empire: Ring4 영지는 Empire와 인접
        /// </summary>
        public List<string> GetAdjacentTerritories(string territoryId)
        {
            var result = new List<string>();

            if (string.IsNullOrEmpty(territoryId))
                return result;

            var db = TerritoryDatabase.Instance;
            if (db == null) return result;

            var def = db.GetDefinition(territoryId);
            if (def.id.nation == NationType.None) return result;

            NationType nation = def.id.nation;
            int index = def.id.index;

            // Empire(인덱스 1)의 인접: 모든 국가의 Ring4 영지
            if (nation == NationType.Empire)
            {
                foreach (var adjNation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
                {
                    for (int i = 16; i <= 20; i++)
                    {
                        result.Add(new TerritoryId(adjNation, i).ToString());
                    }
                }
                return result;
            }

            // ===== 1. 같은 국가 내 인접 =====
            int ringStart = ((index - 1) / 5) * 5 + 1;
            int ringEnd = ringStart + 4;

            // 같은 Ring 내 좌우
            if (index > ringStart)
                result.Add(new TerritoryId(nation, index - 1).ToString());
            if (index < ringEnd)
                result.Add(new TerritoryId(nation, index + 1).ToString());

            // 다음 Ring (내부로)
            if (index + 5 <= 20)
                result.Add(new TerritoryId(nation, index + 5).ToString());
            // 이전 Ring (외부로)
            if (index - 5 >= 1)
                result.Add(new TerritoryId(nation, index - 5).ToString());

            // ===== 2. 인접 국가의 동일 Ring 영지 =====
            int ringLevel = (index - 1) / 5; // 0=Ring1, 1=Ring2, 2=Ring3, 3=Ring4
            if (_adjacentNations.TryGetValue(nation, out var adjNations))
            {
                foreach (var adjNation in adjNations)
                {
                    int adjStart = ringLevel * 5 + 1;
                    int adjEnd = adjStart + 4;
                    for (int adjIdx = adjStart; adjIdx <= adjEnd; adjIdx++)
                    {
                        result.Add(new TerritoryId(adjNation, adjIdx).ToString());
                    }
                }
            }

            // ===== 3. Ring4 영지는 Empire와 인접 =====
            if (ringLevel == 3)
            {
                result.Add(new TerritoryId(NationType.Empire, 1).ToString());
            }

            return result;
        }

        // ================================================================
        // 디버그 / 테스트 헬퍼
        // ================================================================

        /// <summary>
        /// 강제로 전쟁을 트리거합니다. (디버그/테스트용)
        /// </summary>
        public void ForceWar()
        {
            _nextWarTime = Time.time;
            Debug.Log("[TerritoryWarManager] ⚡ 강제 전쟁 트리거!");
        }

        /// <summary>
        /// 다음 전쟁까지 남은 시간을 반환합니다.
        /// </summary>
        public float GetTimeUntilNextWar()
        {
            return Mathf.Max(0f, _nextWarTime - Time.time);
        }

        /// <summary>
        /// 특정 영지의 신규 점령 방어력 부스트 남은 시간을 반환합니다.
        /// </summary>
        public float GetConquestBoostRemaining(string territoryId)
        {
            if (!string.IsNullOrEmpty(territoryId) && _conquestBoostTimers.TryGetValue(territoryId, out float remaining))
                return Mathf.Max(0f, remaining);
            return 0f;
        }

        /// <summary>
        /// 특정 영지가 신규 점령 방어력 부스트 중인지 확인합니다.
        /// </summary>
        public bool HasConquestBoost(string territoryId)
        {
            return !string.IsNullOrEmpty(territoryId) && _conquestBoostTimers.ContainsKey(territoryId);
        }

        /// <summary>
        /// 현재 모든 국가의 영토 수와 병력 현황을 문자열로 반환합니다. (디버그용)
        /// </summary>
        public string GetNationStatusReport()
        {
            var report = new System.Text.StringBuilder();
            var nations = new[] { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire };

            foreach (var nation in nations)
            {
                int territories = GetNationTerritoryCount(nation);
                float soldiers = GetNationTotalSoldiers(nation);
                report.AppendLine($"{nation}: 영지 {territories}개, 병사 {soldiers:F0}명");
            }

            return report.ToString();
        }
    }
}