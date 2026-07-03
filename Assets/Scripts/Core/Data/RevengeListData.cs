using System;
using System.Collections.Generic;
using System.Linq;
using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core.Data
{
    /// <summary>
    /// C14-01: 복수명부 엔트리 — 영주별 복수 정보
    /// </summary>
    [Serializable]
    public struct RevengeListEntry
    {
        public string territoryId;           // 영지 ID (TerritoryId.ToString())
        public string lordName;              // 영주 이름
        public string revengeReason;         // 복수 이유 (숨김, isRevealed 시 공개)
        public bool isRevealed;              // 이유 공개됨 (영주 사망 시)
        public bool isCompleted;             // 복수 완료
        public bool isPoisonConspirator;     // 독살 공모자 (10명)
    }

    /// <summary>
    /// C14-02: 복수명부 저장 데이터 — SaveData 직렬화용
    /// </summary>
    [Serializable]
    public class RevengeListSaveData
    {
        public List<string> revealedTerritories = new List<string>();   // 공개된 영지 ID 목록
        public List<string> completedTerritories = new List<string>();  // 완료된 영지 ID 목록
    }

    /// <summary>
    /// C14-01/C14-02/C14-04: 복수명부 관리자 싱글톤
    /// 81개 영주의 복수 정보 관리, 저장/로드, 이벤트
    /// </summary>
    public class RevengeListManager
    {
        // ======================================================================
        // 싱글톤
        // ======================================================================
        private static RevengeListManager _instance;
        public static RevengeListManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new RevengeListManager();
                return _instance;
            }
        }

        // ======================================================================
        // C14-04: 20가지 복수 이유
        // ======================================================================
        public static readonly string[] RevengeReasons = new string[]
        {
            "왕의 독살에 직접 가담했다",       // 0 — 독살 공모자 전용 (10명)
            "왕의 음식을 직접 준비했다",       // 1
            "암살자 고용 자금을 지원했다",      // 2
            "왕의 최측근을 매수했다",          // 3
            "왕의 개인 경호를 해체시켰다",     // 4
            "왕의 병을 더욱 악화시켰다",       // 5
            "반역자 정보를 숨겼다",            // 6
            "황제국의 기밀을 적에게 팔았다",   // 7
            "왕의 군대를 분열시켰다",          // 8
            "왕의 식량 보급을 차단했다",       // 9
            "왕의 재산을 횡령했다",            // 10
            "충성스러운 신하를 모함했다",      // 11
            "왕의 편지를 위조했다",            // 12
            "왕의 치유를 방해했다",            // 13
            "적국과 내통했다",                 // 14
            "왕의 가족을 위협했다",            // 15
            "왕의 성벽 수리를 거부했다",       // 16
            "왕의 사절단을 습격했다",          // 17
            "왕에게 거짓 보고를 올렸다",       // 18
            "왕의 칙령을 무시했다"             // 19
        };

        // ======================================================================
        // 데이터
        // ======================================================================
        private readonly List<RevengeListEntry> _entries = new List<RevengeListEntry>();
        private bool _initialized = false;

        // ======================================================================
        // 이벤트
        // ======================================================================
        public event Action<string> OnEntryRevealed;      // territoryId
        public event Action<string> OnEntryCompleted;     // territoryId
        public event Action AllPoisonFound;               // 모든 독살 공모자 발견

        // ======================================================================
        // 속성
        // ======================================================================
        public IReadOnlyList<RevengeListEntry> Entries => _entries.AsReadOnly();
        public bool IsInitialized => _initialized;

        // ======================================================================
        // 생성자 (private — 싱글톤)
        // ======================================================================
        private RevengeListManager() { }

        /// <summary>
        /// C14-10: 테스트 초기화용 — 싱글톤 내부 상태 리셋
        /// </summary>
        public void Reset()
        {
            _entries.Clear();
            _initialized = false;
        }

        // ======================================================================
        // 초기화 (C14-02)
        // 81개 엔트리 생성, 10명 독살 공모자 선정, 복수 이유 할당
        // ======================================================================
        public void Initialize()
        {
            _entries.Clear();
            _initialized = false;

            var db = TerritoryDatabase.Instance;
            var definitions = db.GetAllDefinitions().ToList();

            if (definitions.Count == 0)
            {
                Debug.LogWarning("[RevengeListManager] TerritoryDatabase에 정의된 영지가 없습니다.");
                return;
            }

            // 1) 81개 엔트리 생성 (영지 데이터 기반)
            foreach (var def in definitions)
            {
                string key = def.id.ToString();
                var entry = new RevengeListEntry
                {
                    territoryId = key,
                    lordName = def.lord.lordName,
                    revengeReason = "",
                    isRevealed = false,
                    isCompleted = false,
                    isPoisonConspirator = false
                };
                _entries.Add(entry);
            }

            // 2) 고정 시드 설정
            //    - UNITY_EDITOR: 고정 시드 (테스트 재현성)
            //    - 빌드: 현재 시간 기반
#if UNITY_EDITOR
            UnityEngine.Random.InitState(42);
#else
            UnityEngine.Random.InitState(DateTime.Now.GetHashCode());
#endif

            // 3) 10명 독살 공모자 랜덤 선정
            int totalCount = _entries.Count;
            var indices = Enumerable.Range(0, totalCount).ToList();
            var shuffled = indices.OrderBy(_ => UnityEngine.Random.value).ToList();

            int conspiratorCount = Mathf.Min(10, totalCount);
            for (int i = 0; i < conspiratorCount; i++)
            {
                int idx = shuffled[i];
                var entry = _entries[idx];
                entry.isPoisonConspirator = true;
                entry.revengeReason = RevengeReasons[0]; // "왕의 독살에 직접 가담했다"
                _entries[idx] = entry;
            }

            // 4) 나머지 영주에게 2~20번 이유 랜덤 할당 (중복 허용)
            for (int i = 0; i < totalCount; i++)
            {
                var entry = _entries[i];
                if (!entry.isPoisonConspirator)
                {
                    int reasonIdx = UnityEngine.Random.Range(1, RevengeReasons.Length); // 1~19
                    entry.revengeReason = RevengeReasons[reasonIdx];
                    _entries[i] = entry;
                }
            }

            _initialized = true;
            Debug.Log($"[RevengeListManager] 초기화 완료: {_entries.Count}개 엔트리, {conspiratorCount}명 독살 공모자");
        }

        // ======================================================================
        // 메서드
        // ======================================================================

        /// <summary>
        /// 영주 사망 시 이유 공개
        /// </summary>
        public void RevealReason(string territoryId)
        {
            int idx = FindEntryIndex(territoryId);
            if (idx < 0) return;

            var entry = _entries[idx];
            if (entry.isRevealed) return; // 이미 공개됨

            entry.isRevealed = true;
            _entries[idx] = entry;

            Debug.Log($"[RevengeListManager] 이유 공개: {entry.lordName} — {entry.revengeReason}");
            OnEntryRevealed?.Invoke(territoryId);

            // 모든 독살 공모자 발견 체크
            if (AllPoisonConspiratorsFound)
            {
                Debug.Log("[RevengeListManager] 🎉 모든 독살 공모자 발견!");
                AllPoisonFound?.Invoke();
            }
        }

        /// <summary>
        /// 복수 완료 처리
        /// </summary>
        public void CompleteEntry(string territoryId)
        {
            int idx = FindEntryIndex(territoryId);
            if (idx < 0) return;

            var entry = _entries[idx];
            if (entry.isCompleted) return; // 이미 완료

            // 아직 공개되지 않았으면 자동 공개
            if (!entry.isRevealed)
            {
                entry.isRevealed = true;
            }

            entry.isCompleted = true;
            _entries[idx] = entry;

            Debug.Log($"[RevengeListManager] 복수 완료: {entry.lordName}");
            OnEntryCompleted?.Invoke(territoryId);
        }

        /// <summary>
        /// 10명 독살 공모자 목록 반환
        /// </summary>
        public List<RevengeListEntry> GetPoisonConspirators()
        {
            return _entries.Where(e => e.isPoisonConspirator).ToList();
        }

        /// <summary>
        /// 완료된 엔트리 수
        /// </summary>
        public int GetCompletionCount()
        {
            return _entries.Count(e => e.isCompleted);
        }

        /// <summary>
        /// C14-06: 추궁 — 영주 추궁 성공 시 이유 공개
        /// 성공 확률 = (PlayerStats.Instance.Level * 2 + SpeechAffinityBonus) %
        /// </summary>
        /// <param name="territoryId">영지 ID</param>
        /// <returns>추궁 성공 여부</returns>
        public bool Interrogate(string territoryId)
        {
            int idx = FindEntryIndex(territoryId);
            if (idx < 0) return false;

            var entry = _entries[idx];
            if (entry.isRevealed) return true; // 이미 공개됨

            var stats = PlayerStats.Instance;
            if (stats == null) return false;

            // 성공 확률 = Level * 2 + SpeechAffinityBonus (%)
            int successChance = stats.Level * 2 + stats.SpeechAffinityBonus;
            bool success = UnityEngine.Random.Range(0, 100) < successChance;

            if (success)
            {
                entry.isRevealed = true;
                _entries[idx] = entry;

                Debug.Log($"[RevengeListManager] 추궁 성공: {entry.lordName} — {entry.revengeReason}");
                OnEntryRevealed?.Invoke(territoryId);

                // 모든 독살 공모자 발견 체크
                if (AllPoisonConspiratorsFound)
                {
                    Debug.Log("[RevengeListManager] 🎉 모든 독살 공모자 발견! (추궁을 통해)");
                    AllPoisonFound?.Invoke();
                }

                return true;
            }

            Debug.Log($"[RevengeListManager] 추궁 실패: {entry.lordName} — 침묵을 지킴");
            return false;
        }

        /// <summary>
        /// 공개된 독살 공모자 수
        /// </summary>
        public int GetRevealedPoisonConspiratorCount()
        {
            return _entries.Count(e => e.isPoisonConspirator && e.isRevealed);
        }

        /// <summary>
        /// 모든 독살 공모자 발견 여부
        /// </summary>
        public bool AllPoisonConspiratorsFound
        {
            get
            {
                return _entries.Count(e => e.isPoisonConspirator) > 0
                    && _entries.Where(e => e.isPoisonConspirator).All(e => e.isRevealed);
            }
        }

        /// <summary>
        /// 모든 81명 완료?
        /// </summary>
        public bool IsFullyComplete()
        {
            return _entries.Count > 0 && _entries.All(e => e.isCompleted);
        }

        /// <summary>
        /// 특정 영지의 엔트리 조회
        /// </summary>
        /// <returns>엔트리, 없으면 default (territoryId == null 로 판별 가능)</returns>
        public RevengeListEntry GetEntry(string territoryId)
        {
            int idx = FindEntryIndex(territoryId);
            return idx >= 0 ? _entries[idx] : default;
        }

        /// <summary>
        /// 특정 영지의 엔트리 조회 — TryGet 패턴
        /// </summary>
        /// <returns>발견 시 true, entry에 값 할당</returns>
        public bool TryGetEntry(string territoryId, out RevengeListEntry entry)
        {
            int idx = FindEntryIndex(territoryId);
            if (idx >= 0)
            {
                entry = _entries[idx];
                return true;
            }
            entry = default;
            return false;
        }

        // ======================================================================
        // 저장/로드 (SaveData 연동)
        // ======================================================================

        /// <summary>
        /// 현재 상태를 RevengeListSaveData로 변환
        /// </summary>
        public RevengeListSaveData SaveState()
        {
            var data = new RevengeListSaveData();
            data.revealedTerritories = _entries
                .Where(e => e.isRevealed)
                .Select(e => e.territoryId)
                .ToList();
            data.completedTerritories = _entries
                .Where(e => e.isCompleted)
                .Select(e => e.territoryId)
                .ToList();
            return data;
        }

        /// <summary>
        /// 저장된 상태를 현재 엔트리에 적용
        /// </summary>
        public void LoadState(RevengeListSaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[RevengeListManager] LoadState: data가 null입니다.");
                return;
            }

            // revealedTerritories 적용
            foreach (var tid in data.revealedTerritories)
            {
                int idx = FindEntryIndex(tid);
                if (idx >= 0)
                {
                    var entry = _entries[idx];
                    entry.isRevealed = true;
                    _entries[idx] = entry;
                }
            }

            // completedTerritories 적용 (완료 시 자동 공개)
            foreach (var tid in data.completedTerritories)
            {
                int idx = FindEntryIndex(tid);
                if (idx >= 0)
                {
                    var entry = _entries[idx];
                    entry.isRevealed = true;
                    entry.isCompleted = true;
                    _entries[idx] = entry;
                }
            }

            Debug.Log($"[RevengeListManager] 상태 로드 완료: {data.revealedTerritories.Count}개 공개, {data.completedTerritories.Count}개 완료");
        }

        // ======================================================================
        // 내부
        // ======================================================================

        /// <summary>territoryId로 엔트리 인덱스 찾기</summary>
        private int FindEntryIndex(string territoryId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].territoryId == territoryId)
                    return i;
            }
            Debug.LogWarning($"[RevengeListManager] 영지 없음: {territoryId}");
            return -1;
        }
    }
}