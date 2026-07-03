using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 📊 게임 통계 수집기 — 정적 클래스, PlayerPrefs 저장/로드.
    /// 각 이벤트에서 TrackXxx()를 호출하여 통계를 누적합니다.
    /// GameStatsWindow에서 읽어서 표시합니다.
    /// </summary>
    public static class GameStatsCollector
    {
        // ======================================================================
        // PlayerPrefs 키 상수
        // ======================================================================
        private const string PREFS_PREFIX = "Stat_";
        private const string PREFS_PLAYTIME = "Stat_PlayTime";
        private const string PREFS_KILLS = "Stat_Kills";
        private const string PREFS_DEATHS = "Stat_Deaths";
        private const string PREFS_GOLD_EARNED = "Stat_GoldEarned";
        private const string PREFS_GOLD_SPENT = "Stat_GoldSpent";
        private const string PREFS_DISTANCE = "Stat_Distance";
        private const string PREFS_FISH_CAUGHT = "Stat_FishCaught";
        private const string PREFS_ARENA_WINS = "Stat_ArenaWins";
        private const string PREFS_ARENA_LOSSES = "Stat_ArenaLosses";
        private const string PREFS_ARENA_BEST_STREAK = "Stat_ArenaBestStreak";
        private const string PREFS_WAR_PARTICIPATIONS = "Stat_WarParticipations";

        // ======================================================================
        // 통계 필드 (메모리 내 캐시)
        // ======================================================================
        private static float _playTime;              // ⏱️ 총 플레이 시간 (초)
        private static int _kills;                   // 🧟 처치한 몬스터 수
        private static int _deaths;                  // 💀 사망 횟수
        private static int _goldEarned;              // 💰 획득 골드
        private static int _goldSpent;               // 💰 사용 골드
        private static float _distanceTraveled;      // 🐴 이동 거리
        private static int _fishCaught;              // 🐟 획득 물고기
        private static int _arenaWins;               // 🏟️ 아레나 승리
        private static int _arenaLosses;             // 🏟️ 아레나 패배
        private static int _arenaBestStreak;         // 🏟️ 아레나 최고 연승
        private static int _warParticipations;       // ⚔️ 전쟁 참여 횟수

        // ======================================================================
        // 런타임 트래킹용
        // ======================================================================
        private static float _sessionStartTime;      // 현재 세션 시작 시간 (Time.realtimeSinceStartup)
        private static Vector3 _lastPosition;         // 마지막 위치 (이동 거리 계산용)
        private static bool _hasLastPosition;
        // private static bool _loaded;

        // ======================================================================
        // 초기화
        // ======================================================================

        /// <summary>
        /// PlayerPrefs에서 모든 통계를 로드합니다.
        /// 게임 시작 시 한 번 호출하세요.
        /// </summary>
        public static void LoadStats()
        {
            _playTime = PlayerPrefs.GetFloat(PREFS_PLAYTIME, 0f);
            _kills = PlayerPrefs.GetInt(PREFS_KILLS, 0);
            _deaths = PlayerPrefs.GetInt(PREFS_DEATHS, 0);
            _goldEarned = PlayerPrefs.GetInt(PREFS_GOLD_EARNED, 0);
            _goldSpent = PlayerPrefs.GetInt(PREFS_GOLD_SPENT, 0);
            _distanceTraveled = PlayerPrefs.GetFloat(PREFS_DISTANCE, 0f);
            _fishCaught = PlayerPrefs.GetInt(PREFS_FISH_CAUGHT, 0);
            _arenaWins = PlayerPrefs.GetInt(PREFS_ARENA_WINS, 0);
            _arenaLosses = PlayerPrefs.GetInt(PREFS_ARENA_LOSSES, 0);
            _arenaBestStreak = PlayerPrefs.GetInt(PREFS_ARENA_BEST_STREAK, 0);
            _warParticipations = PlayerPrefs.GetInt(PREFS_WAR_PARTICIPATIONS, 0);

            _sessionStartTime = Time.realtimeSinceStartup;
            // _loaded = true;

            Debug.Log($"[GameStatsCollector] 통계 로드 완료: 플레이타임 {FormatTime(_playTime)}, 처치 {_kills}, 사망 {_deaths}");
        }

        /// <summary>
        /// 현재 통계를 PlayerPrefs에 저장합니다.
        /// OnApplicationQuit 또는 주기적으로 호출하세요.
        /// </summary>
        public static void SaveStats()
        {
            // 현재 세션 플레이타임 반영
            float sessionElapsed = Time.realtimeSinceStartup - _sessionStartTime;
            float totalPlayTime = _playTime + sessionElapsed;

            PlayerPrefs.SetFloat(PREFS_PLAYTIME, totalPlayTime);
            PlayerPrefs.SetInt(PREFS_KILLS, _kills);
            PlayerPrefs.SetInt(PREFS_DEATHS, _deaths);
            PlayerPrefs.SetInt(PREFS_GOLD_EARNED, _goldEarned);
            PlayerPrefs.SetInt(PREFS_GOLD_SPENT, _goldSpent);
            PlayerPrefs.SetFloat(PREFS_DISTANCE, _distanceTraveled);
            PlayerPrefs.SetInt(PREFS_FISH_CAUGHT, _fishCaught);
            PlayerPrefs.SetInt(PREFS_ARENA_WINS, _arenaWins);
            PlayerPrefs.SetInt(PREFS_ARENA_LOSSES, _arenaLosses);
            PlayerPrefs.SetInt(PREFS_ARENA_BEST_STREAK, _arenaBestStreak);
            PlayerPrefs.SetInt(PREFS_WAR_PARTICIPATIONS, _warParticipations);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 모든 통계를 0으로 초기화하고 PlayerPrefs에서 삭제합니다.
        /// </summary>
        public static void ResetAllStats()
        {
            _playTime = 0f;
            _kills = 0;
            _deaths = 0;
            _goldEarned = 0;
            _goldSpent = 0;
            _distanceTraveled = 0f;
            _fishCaught = 0;
            _arenaWins = 0;
            _arenaLosses = 0;
            _arenaBestStreak = 0;
            _warParticipations = 0;

            _sessionStartTime = Time.realtimeSinceStartup;
            _hasLastPosition = false;

            PlayerPrefs.DeleteKey(PREFS_PLAYTIME);
            PlayerPrefs.DeleteKey(PREFS_KILLS);
            PlayerPrefs.DeleteKey(PREFS_DEATHS);
            PlayerPrefs.DeleteKey(PREFS_GOLD_EARNED);
            PlayerPrefs.DeleteKey(PREFS_GOLD_SPENT);
            PlayerPrefs.DeleteKey(PREFS_DISTANCE);
            PlayerPrefs.DeleteKey(PREFS_FISH_CAUGHT);
            PlayerPrefs.DeleteKey(PREFS_ARENA_WINS);
            PlayerPrefs.DeleteKey(PREFS_ARENA_LOSSES);
            PlayerPrefs.DeleteKey(PREFS_ARENA_BEST_STREAK);
            PlayerPrefs.DeleteKey(PREFS_WAR_PARTICIPATIONS);
            PlayerPrefs.Save();

            Debug.Log("[GameStatsCollector] 모든 통계 초기화 완료");
        }

        // ======================================================================
        // 트래커 메서드 (외부 시스템에서 호출)
        // ======================================================================

        /// <summary>🧟 몬스터 처치 기록</summary>
        public static void TrackKill(int count = 1)
        {
            _kills += count;
        }

        /// <summary>💀 사망 기록</summary>
        public static void TrackDeath(int count = 1)
        {
            _deaths += count;
        }

        /// <summary>💰 골드 획득 기록</summary>
        public static void TrackGoldEarned(int amount)
        {
            if (amount > 0)
                _goldEarned += amount;
        }

        /// <summary>💰 골드 사용 기록</summary>
        public static void TrackGoldSpent(int amount)
        {
            if (amount > 0)
                _goldSpent += amount;
        }

        /// <summary>🐴 이동 거리 기록 (미터)</summary>
        public static void TrackDistance(float meters)
        {
            if (meters > 0f)
                _distanceTraveled += meters;
        }

        /// <summary>🐟 물고기 획득 기록</summary>
        public static void TrackFishCaught(int count = 1)
        {
            _fishCaught += count;
        }

        /// <summary>🏟️ 아레나 승리 기록</summary>
        public static void TrackArenaWin()
        {
            _arenaWins++;
        }

        /// <summary>🏟️ 아레나 패배 기록</summary>
        public static void TrackArenaLoss()
        {
            _arenaLosses++;
        }

        /// <summary>🏟️ 아레나 최고 연승 기록 (ArenaSystem에서 호출)</summary>
        public static void TrackArenaBestStreak(int streak)
        {
            if (streak > _arenaBestStreak)
                _arenaBestStreak = streak;
        }

        /// <summary>⚔️ 전쟁 참여 기록</summary>
        public static void TrackWarParticipation(int count = 1)
        {
            _warParticipations += count;
        }

        /// <summary>세션 시작 시간 재설정 (로드 시 호출)</summary>
        public static void ResetSessionTime()
        {
            _sessionStartTime = Time.realtimeSinceStartup;
        }

        /// <summary>이동 거리 트래킹용 위치 설정 (플레이어 움직임에서 호출)</summary>
        public static void SetLastPosition(Vector3 position)
        {
            _lastPosition = position;
            _hasLastPosition = true;
        }

        /// <summary>프레임마다 이동 거리 계산 (GameStatsWindow.Update 등에서 호출)</summary>
        public static void UpdateDistanceTracking(Vector3 currentPosition)
        {
            if (!_hasLastPosition)
            {
                _lastPosition = currentPosition;
                _hasLastPosition = true;
                return;
            }

            float dist = Vector3.Distance(_lastPosition, currentPosition);
            if (dist > 0.01f) // 노이즈 방지
            {
                _distanceTraveled += dist;
                _lastPosition = currentPosition;
            }
        }

        // ======================================================================
        // 읽기 전용 프로퍼티 (GameStatsWindow에서 사용)
        // ======================================================================

        /// <summary>⏱️ 총 플레이 시간 (초, 현재 세션 포함)</summary>
        public static float PlayTime => _playTime + (Time.realtimeSinceStartup - _sessionStartTime);

        /// <summary>🧟 처치한 몬스터 수</summary>
        public static int Kills => _kills;

        /// <summary>💀 사망 횟수</summary>
        public static int Deaths => _deaths;

        /// <summary>💰 획득 골드</summary>
        public static int GoldEarned => _goldEarned;

        /// <summary>💰 사용 골드</summary>
        public static int GoldSpent => _goldSpent;

        /// <summary>💰 순수익</summary>
        public static int GoldNet => _goldEarned - _goldSpent;

        /// <summary>🐴 총 이동 거리 (미터)</summary>
        public static float DistanceTraveled => _distanceTraveled;

        /// <summary>🐟 획득 물고기 수</summary>
        public static int FishCaught => _fishCaught;

        /// <summary>🏟️ 아레나 승리</summary>
        public static int ArenaWins => _arenaWins;

        /// <summary>🏟️ 아레나 패배</summary>
        public static int ArenaLosses => _arenaLosses;

        /// <summary>🏟️ 아레나 최고 연승</summary>
        public static int ArenaBestStreak => _arenaBestStreak;

        /// <summary>🏟️ 아레나 총 전적</summary>
        public static int ArenaTotal => _arenaWins + _arenaLosses;

        /// <summary>⚔️ 전쟁 참여 횟수</summary>
        public static int WarParticipations => _warParticipations;

        // ======================================================================
        // 외부 시스템 조회 기반 통계
        // ======================================================================

        /// <summary>🏆 완료한 퀘스트 수 (QuestManager 조회)</summary>
        public static int CompletedQuests
        {
            get
            {
                try
                {
                    return QuestManager.GetCompletedQuests()?.Count ?? 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>👑 점령 영지 수 (TerritoryDatabase 조회)</summary>
        public static int OwnedTerritories
        {
            get
            {
                try
                {
                    var db = TerritoryDatabase.Instance;
                    if (db == null) return 0;

                    int count = 0;
                    var defs = db.GetAllDefinitions();
                    if (defs == null) return 0;

                    foreach (var def in defs)
                    {
                        if (def.id.nation == NationType.None) continue;
                        var state = db.GetState(def.id);
                        if (state != null && state.ownership == TerritoryOwnership.PlayerOwned)
                            count++;
                    }
                    return count;
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>🗡️ 암살한 영주 수 (RevengeListManager 조회)</summary>
        public static int CompletedRevenge
        {
            get
            {
                try
                {
                    var mgr = RevengeListManager.Instance;
                    return mgr?.GetCompletionCount() ?? 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        // ======================================================================
        // 포맷 헬퍼
        // ======================================================================

        /// <summary>초 → "HH:MM:SS" 문자열 변환</summary>
        public static string FormatTime(float totalSeconds)
        {
            int hours = (int)(totalSeconds / 3600f);
            int minutes = (int)((totalSeconds % 3600f) / 60f);
            int seconds = (int)(totalSeconds % 60f);
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        /// <summary>미터 → "X,XXX.XX m" 또는 "X.XX km" 변환</summary>
        public static string FormatDistance(float meters)
        {
            if (meters >= 1000f)
                return $"{meters / 1000f:F2} km";
            return $"{meters:F1} m";
        }

        /// <summary>골드 포맷</summary>
        public static string FormatGold(int gold)
        {
            return $"{gold:N0} G";
        }
    }
}
