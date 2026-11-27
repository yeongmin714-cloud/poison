using System;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [125차] 훈련소 — 병사 훈련 로직 코어 (P3).
    /// - 정적 레지스트리 + 300ms 폴링(ArenaSystem/InteriorStationPlacer 규약).
    /// - StartTraining(GuardPlaceholder, territoryKey): 가드 체인 ①소속 ②중복 ③정원 ④골드(레벨×20G).
    /// - 능력치 상승: 시작 시점에 결정론 랜덤 확정(System.Random(guard.StableId+훈련횟수 시드) — 재현 가능).
    /// - 세션/완료 상태는 런타임 전용(세이브 미영속 — 병사 레벨/능력치 자체는 AIHiredGuardSaveEntry 경로 자동 영속).
    /// </summary>
    public static class TrainingHallSystem
    {
        public const int Capacity = 20;             // 동시 훈련 정원 (Figma 문구)
        public const int TrainingDurationSeconds = 3600;   // 게임시간 1시간
        public const int CostPerLevel = 20;         // 골드 비용 = Lv × 20G

        public const string SourceGold = "training_hall";

        // ===== [125차] 진행률/남은 시간 계약 (순수 산식 — EditMode 검증 대상) =====

        /// <summary>훈련 비용 = 레벨 × 20G (음수/0 레벨 가드).</summary>
        public static int CostForLevel(int level) => Mathf.Max(0, level) * CostPerLevel;

        /// <summary>진행률 0..1 — GameTime 랩(86400) 대응: elapsed = ((now - start) % 86400 + 86400) % 86400.</summary>
        public static float GetProgress01(TrainingSession s, float nowGameTime)
        {
            if (s == null) return 0f;
            float elapsed = ElapsedSeconds(s, nowGameTime);
            return Mathf.Clamp01(elapsed / TrainingDurationSeconds);
        }

        /// <summary>남은 시간(초, 올림 없음 — HH:MM:SS 포맷용 절삭).</summary>
        public static int GetRemainingSeconds(TrainingSession s, float nowGameTime)
        {
            if (s == null) return 0;
            int remaining = TrainingDurationSeconds - (int)ElapsedSeconds(s, nowGameTime);
            return Mathf.Max(0, remaining);
        }

        /// <summary>경과 초 — GameTime 랩 대응 modulo 산식(86400 = TimeManager 하루).</summary>
        private static float ElapsedSeconds(TrainingSession s, float nowGameTime)
        {
            float d = (nowGameTime - s.StartGameTime) % 86400f;
            if (d < 0f) d += 86400f;
            return d;
        }

        /// <summary>HH:MM:SS 포맷 (남은 시간 표기 계약).</summary>
        public static string FormatRemaining(int seconds)
        {
            if (seconds < 0) seconds = 0;
            return string.Format("{0:D2}:{1:D2}:{2:D2}", seconds / 3600, (seconds % 3600) / 60, seconds % 60);
        }

        // ===== [125차] 레지스트리 (런타임 전용 — 세이브 미영속) =====

        private static readonly List<TrainingSession> _sessions = new List<TrainingSession>();
        private static readonly List<CompletedTraining> _completed = new List<CompletedTraining>();
        private static readonly Dictionary<string, int> _tiers = new Dictionary<string, int>();   // [125차] 누적 훈련 완료 횟수(병사별 — 훈련 티어 표기용)

        /// <summary>현재 훈련 중 세션 목록 (직접 수정 금지 — 읽기 전용 뷰).</summary>
        public static IReadOnlyList<TrainingSession> ActiveSessions => _sessions;

        /// <summary>완료 대기(수령 전) 목록.</summary>
        public static IReadOnlyList<CompletedTraining> Completed => _completed;

        /// <summary>훈련 완료 이벤트 — TrainingHallWindowUTK 갱신용.</summary>
        public static event Action<CompletedTraining> OnTrainingCompleted;

        /// <summary>세션 슬롯 남은 개수 (0이면 정원 초과).</summary>
        public static int SlotsFree => Capacity - _sessions.Count;

        public static bool IsTraining(string stableId) => FindSession(stableId) != null;

        public static TrainingSession FindSession(string stableId)
        {
            for (int i = 0; i < _sessions.Count; i++)
                if (_sessions[i].StableId == stableId) return _sessions[i];
            return null;
        }

        // ===== [125차] StartTraining (가드 체인 + 결정론 랜덤) =====

        /// <summary>
        /// 훈련 시작 — 가드 체인: ①소속 판정 ②중복 방지 ③정원 20명 ④골드(레벨×20G).
        /// 통과 시 세션 등록(능력치 가산치는 시작 시점에 결정론 랜덤 확정 — 재현 가능).
        /// 거부 사유 반환 문자열 (InteriorStationPlacer.TryPlace 패턴 — null은 성공이 아니라 "성공" 마커로 사용 안 함,
        /// 성공 여부는 out 세션으로 판정).
        /// </summary>
        public static string StartTraining(GuardPlaceholder guard, string territoryKey, out TrainingSession session)
        {
            session = null;
            if (guard == null) return "병사를 선택하세요";
            if (string.IsNullOrEmpty(territoryKey)) return "영지를 선택하세요";

            // ① 소속 판정 — GuardManager.GetGuardsInTerritory 소속 확인
            if (!IsGuardInTerritory(guard, territoryKey)) return "해당 영지 소속 병사가 아닙니다";

            // ② 중복 방지
            if (IsTraining(guard.StableId)) return "이미 훈련 중입니다";

            // ③ 정원 20명
            if (_sessions.Count >= Capacity) return "훈련 정원이 가득 찼습니다";

            // ④ 골드 — PlayerStats.SpendGold("training_hall")
            int cost = CostForLevel(guard.Level);
            var ps = PlayerStats.Instance;
            if (ps == null) return "플레이어 스탯을 찾을 수 없습니다";
            if (ps.Gold < cost) return $"골드가 부족합니다 (필요 {cost}G)";
            if (!ps.SpendGold(cost, SourceGold)) return "골드 소비 실패";

            // 결정론 랜덤 확정 — 시드 = StableId + 훈련횟수(세션 등록 순서)
            int seed = SeedFor(guard.StableId, _sessions.Count);
            (int atkGain, int defGain, int vitGain) = RollGains(seed);

            session = new TrainingSession
            {
                StableId = guard.StableId,
                Guard = guard,
                GuardName = guard.GuardName,
                TerritoryKey = territoryKey,
                LevelAtStart = guard.Level,
                CostGold = cost,
                StartGameTime = TimeManager.Instance != null ? TimeManager.Instance.GameTime : 0f,
                EndGameTime = 0f,
                AtkGain = atkGain,
                DefGain = defGain,
                VitGain = vitGain,
            };
            session.EndGameTime = session.StartGameTime + TrainingDurationSeconds;
            _sessions.Add(session);
            return null; // 성공 (사유 없음)
        }

        /// <summary>훈련 취소 — 비용 환불 없음(기본안), 세션 제거.</summary>
        public static bool CancelTraining(string stableId)
        {
            var s = FindSession(stableId);
            if (s == null) return false;
            _sessions.Remove(s);
            return true;
        }

        /// <summary>[125차] 훈련 티어 — 누적 훈련 완료 횟수 + 1 (Figma 좌측 행 소형 "Lv.N" 표기 기본안).</summary>
        public static int GetTier(string stableId)
            => _tiers.TryGetValue(stableId ?? string.Empty, out int n) ? n + 1 : 1;

        /// <summary>[125차] 영지 소속 병사 목록 — GuardManager.GetGuardsInTerritory(훈련 UI 좌측 명부 소스).
        /// Instance null/파싱 실패 시 빈 목록(테스트·오프라인 폴백).</summary>
        public static List<GuardPlaceholder> GetGuardsForTerritory(string territoryKey)
        {
            var result = new List<GuardPlaceholder>();
            if (GuardManager.Instance == null || !TryParseTerritoryKey(territoryKey, out var tid))
                return result;
            var guards = GuardManager.Instance.GetGuardsInTerritory(tid);
            if (guards != null) result.AddRange(guards);
            return result;
        }

        /// <summary>[125차] 즉시 완료 — 남은 시간 무시하고 확정(좌측 훈련 중 행 [완료] 버튼/Enter 계약).
        /// 비용 환불 없음(취소 규약 동일). 완료 경로는 PollCompletion과 동일(CompleteSession 재사용).</summary>
        public static bool CompleteNow(string stableId)
        {
            var s = FindSession(stableId);
            if (s == null) return false;
            _sessions.Remove(s);
            CompleteSession(s, TimeManager.Instance != null ? TimeManager.Instance.GameTime : s.EndGameTime);
            return true;
        }

        // ===== [125차] 완료 폴링 =====

        /// <summary>GameTime ≥ endGameTime → 완료 목록 이동 + ApplyTrainingLevelUp 호출.</summary>
        public static void PollCompletion(float nowGameTime)
        {
            for (int i = _sessions.Count - 1; i >= 0; i--)
            {
                var s = _sessions[i];
                if (nowGameTime < s.EndGameTime) continue;
                _sessions.RemoveAt(i);
                CompleteSession(s, nowGameTime);
            }
        }

        private static void CompleteSession(TrainingSession s, float nowGameTime)
        {
            // [125차 수리] 세션 보관 참조 우선(Unity 파괴 시 fake-null로 자동 폴백) — FindGuard는 보조
            var guard = s.Guard != null ? s.Guard : FindGuard(s.StableId);
            if (guard != null)
                guard.ApplyTrainingLevelUp(s.AtkGain, s.DefGain, s.VitGain);
            _tiers[s.StableId] = _tiers.TryGetValue(s.StableId, out int prev) ? prev + 1 : 1;   // [125차] 훈련 티어 누적
            Debug.Log($"[125차] 훈련 완료 — {s.GuardName} Lv.{(guard != null ? guard.Level : s.LevelAtStart + 1)} (atk+{s.AtkGain} def+{s.DefGain} vit+{s.VitGain})");

            var done = new CompletedTraining
            {
                StableId = s.StableId,
                GuardName = s.GuardName,
                TerritoryKey = s.TerritoryKey,
                LevelAtStart = s.LevelAtStart,
                NewLevel = guard != null ? guard.Level : s.LevelAtStart + 1,
                AtkGain = s.AtkGain,
                DefGain = s.DefGain,
                VitGain = s.VitGain,
                EndGameTime = s.EndGameTime,
                CostGold = s.CostGold,
                Deployed = false,
            };
            _completed.Add(done);
            OnTrainingCompleted?.Invoke(done);
        }

        /// <summary>완료 병사 수령(배치) — Deployed 플래그 갱신.</summary>
        public static bool MarkDeployed(string stableId)
        {
            for (int i = 0; i < _completed.Count; i++)
                if (_completed[i].StableId == stableId)
                {
                    _completed[i].Deployed = true;
                    return true;
                }
            return false;
        }

        // ===== [125차] 결정론 랜덤 — 시드/산식 (재현 가능 QA/세이브 정합) =====

        /// <summary>시드 = StableId + 훈련횟수 (WandererDriver GetInstanceID 시드 선례 준용).</summary>
        public static int SeedFor(string stableId, int trainingCount)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in stableId ?? string.Empty)
                    h = h * 31 + c;
                return h ^ (trainingCount * 1013904223);
            }
        }

        /// <summary>3종 각각 0~2 랜덤, 합계 최소 1 보장(0,0,0 방지 — 결정론 랜덤 재설정 루프).</summary>
        public static (int atk, int def, int vit) RollGains(int seed)
        {
            var rng = new System.Random(seed);
            int atk = rng.Next(0, 3);
            int def = rng.Next(0, 3);
            int vit = rng.Next(0, 3);
            if (atk + def + vit == 0)
            {
                // 합계 0 방지 — 동일 시드 재설정 루프(결정론 유지)
                atk = 1;
                vit = rng.Next(0, 3);
                if (vit == 0) vit = 1; // 최소 (1,0,0) 이상
            }
            return (atk, def, vit);
        }

        // ===== [125차] GuardManager 접점 =====

        private static bool IsGuardInTerritory(GuardPlaceholder guard, string territoryKey)
        {
            // [125차] GuardManager 접점 — territoryKey("East_01") 파싱 후 GetGuardsInTerritory 소속 판정.
            // 테스트/오프라인 폴백: Instance null이면 소속 판정 통과(런타임에선 존재).
            if (GuardManager.Instance == null) return true;
            if (!TryParseTerritoryKey(territoryKey, out var tid)) return false;
            var guards = GuardManager.Instance.GetGuardsInTerritory(tid);
            for (int i = 0; i < guards.Count; i++)
                if (guards[i].StableId == guard.StableId) return true;
            return false;
        }

        /// <summary>territoryKey("East_01") → TerritoryId — GuardManager.TryParseTerritoryKey 선례 준용.</summary>
        private static bool TryParseTerritoryKey(string key, out TerritoryId id)
        {
            id = default;
            if (string.IsNullOrEmpty(key)) return false;
            string[] parts = key.Split('_');
            if (parts.Length != 2) return false;
            if (!System.Enum.TryParse(parts[0], out NationType nation)) return false;
            if (!int.TryParse(parts[1], out int index) || index < 1) return false;
            id = new TerritoryId(nation, index);
            return true;
        }

        private static GuardPlaceholder FindGuard(string stableId)
        {
            if (GuardManager.Instance == null) return null;
            foreach (var kvp in GuardManager.Instance.GetAllRegisteredGuards())
                if (kvp.Value.StableId == stableId) return kvp.Value;
            return null;
        }

        // ===== [125차] 드라이버 (300ms 폴링) =====

        private static Driver _driver;

        /// <summary>드라이버 보장 — AfterlifeSurvivalSystem.EnsureDriver 선례 (DDOL — 씬 언로드 생존).</summary>
        public static void EnsureDriver()
        {
            if (_driver != null) return;
            var go = new GameObject("[TrainingHallDriver]");
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(go); // 편집 모드 DDOL 금지 — TimeManager QA 수리 동일 패턴
            _driver = go.AddComponent<Driver>();
        }

        /// <summary>300ms 폴링 드라이버 — PollCompletion(TimeManager.GameTime) 주기 호출.</summary>
        public class Driver : MonoBehaviour
        {
            private const float PollInterval = 0.3f;
            private float _nextPoll;

            private void Update()
            {
                if (Time.unscaledTime < _nextPoll) return;
                _nextPoll = Time.unscaledTime + PollInterval;
                float now = TimeManager.Instance != null ? TimeManager.Instance.GameTime : 0f;
                PollCompletion(now);
            }
        }

        /// <summary>[125차] EditMode 테스트 격리 — 레지스트리/이벤트 초기화 (TaxManager 등 선례).</summary>
        public static void ResetForTest()
        {
            _sessions.Clear();
            _completed.Clear();
            _tiers.Clear();
            OnTrainingCompleted = null;
        }

        // ===== 세션 DTO =====

        public class TrainingSession
        {
            public string StableId;
            public GuardPlaceholder Guard;   // [125차 수리] 가드 참조 직접 보관 — FindGuard 실패(EditorMode 미등록) 완료 누락 방지
            public string GuardName;
            public string TerritoryKey;
            public int LevelAtStart;
            public int CostGold;
            public float StartGameTime;
            public float EndGameTime;
            public int AtkGain;
            public int DefGain;
            public int VitGain;
        }

        public class CompletedTraining
        {
            public string StableId;
            public string GuardName;
            public string TerritoryKey;
            public int LevelAtStart;
            public int NewLevel;
            public int AtkGain;
            public int DefGain;
            public int VitGain;
            public float EndGameTime;
            public int CostGold;
            public bool Deployed;
        }
    }
}
