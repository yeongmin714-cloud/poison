using System.Collections.Generic;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 🏘️ NPC 일상 사이클 — 시간대 기반 NPC 활성화/비활성화 관리.
    /// DayNightCycle.OnTimeOfDayChanged 대신 TimeManager.OnTimeChanged를 직접 구독하여
    /// 시간대(Dawn/Day/Evening/Night)를 계산하고 NPC 상태를 제어합니다.
    ///
    /// 시간대:
    ///   - Dawn  (04~06): NPC 기상, 상점 준비
    ///   - Day   (06~18): NPC 활동 (원래 위치에서 대기)
    ///   - Evening (18~20): NPC 귀가
    ///   - Night (20~04): NPC 비활성화 (수면)
    /// </summary>
    public class NPCDailyCycle : MonoBehaviour
    {
        public static NPCDailyCycle Instance { get; private set; }

        [Header("시간 설정")]
        [SerializeField] private bool _verbose;

        [Header("NPC 태그 (자동 탐색)")]
        [SerializeField] private string _npcTag = "NPC";

        /// <summary>
        /// 시간대 열거형
        /// </summary>
        public enum TimePeriod
        {
            Dawn,    // 04~06: 기상
            Day,     // 06~18: 활동
            Evening, // 18~20: 귀가
            Night    // 20~04: 수면
        }

        /// <summary>현재 시간대</summary>
        public TimePeriod CurrentPeriod { get; private set; } = TimePeriod.Day;

        /// <summary>현재 시간대 변경 이벤트</summary>
        public event System.Action<TimePeriod> OnTimePeriodChanged;

        // 등록된 NPC 데이터
        private readonly List<NPCData> _allNPCs = new List<NPCData>();

        private TimeManager _timeManager;

        /// <summary>
        /// NPC 내부 데이터 — 원래 위치(home)와 상태 정보
        /// </summary>
        private class NPCData
        {
            public GameObject gameObject;
            public Vector3 homePosition;   // 원래 위치 (집)
            public string npcName;
            public bool isShopNPC;         // 상점 NPC 여부

            public NPCData(GameObject obj, Vector3 home, string name, bool isShop)
            {
                gameObject = obj;
                homePosition = home;
                npcName = name;
                isShopNPC = isShop;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _timeManager = TimeManager.Instance;
            if (_timeManager == null)
            {
                Debug.LogError("[NPCDailyCycle] TimeManager.Instance가 없습니다. 비활성화합니다.");
                enabled = false;
                return;
            }

            // 씬의 모든 NPC 탐색
            FindAllNPCs();

            // TimeManager 이벤트 구독
            _timeManager.OnTimeChanged += OnGameTimeChanged;

            // 초기 시간대 적용
            int currentHour = _timeManager.Hour;
            TimePeriod initialPeriod = CalculatePeriod(currentHour);
            ApplyPeriodToAll(initialPeriod);
            CurrentPeriod = initialPeriod;

            if (_verbose)
                Debug.Log($"[NPCDailyCycle] 초기화 완료. NPC {_allNPCs.Count}명 등록. 현재 시간대: {initialPeriod}");
        }

        private void OnDestroy()
        {
            if (_timeManager != null)
                _timeManager.OnTimeChanged -= OnGameTimeChanged;

            if (Instance == this)
                Instance = null;
        }

        // ================================================================
        // NPC 탐색
        // ================================================================

        /// <summary>
        /// 씬에서 모든 NPC를 찾아 등록합니다.
        /// 태그 "NPC"가 설정된 오브젝트 + 알려진 NPC 스크립트가 붙은 오브젝트를 수집합니다.
        /// </summary>
        private void FindAllNPCs()
        {
            _allNPCs.Clear();
            var found = new HashSet<GameObject>();

            // 1. 태그 "NPC"로 찾기
            GameObject[] taggedNPCs = GameObject.FindGameObjectsWithTag(_npcTag);
            foreach (var go in taggedNPCs)
            {
                if (go != null && !found.Contains(go))
                {
                    found.Add(go);
                    bool isShop = IsShopNPCByScript(go);
                    _allNPCs.Add(new NPCData(go, go.transform.position, go.name, isShop));
                }
            }

            // 2. 알려진 NPC 스크립트 타입으로 찾기
            FindNPCsByScriptType<TutorialQuestNPC>(found, isShop: false);
            FindNPCsByScriptType<ChurchNPCInteraction>(found, isShop: false);
            FindNPCsByScriptType<FestivalNPC>(found, isShop: false);
            FindNPCsByScriptType<GuardPlaceholder>(found, isShop: false);
            FindNPCsByScriptType<GateGuardPlaceholder>(found, isShop: false);
            FindNPCsByScriptType<SkeletonGuardPlaceholder>(found, isShop: false);

            if (_verbose)
            {
                Debug.Log($"[NPCDailyCycle] NPC 목록 ({_allNPCs.Count}명):");
                foreach (var npc in _allNPCs)
                    Debug.Log($"  - {npc.npcName} (위치: {npc.homePosition}, 상점: {npc.isShopNPC})");
            }
        }

        /// <summary>특정 스크립트 타입이 붙은 오브젝트를 찾아 등록</summary>
        private void FindNPCsByScriptType<T>(HashSet<GameObject> found, bool isShop) where T : MonoBehaviour
        {
            T[] components = FindObjectsByType<T>(FindObjectsSortMode.None);
            foreach (var comp in components)
            {
                if (comp != null && comp.gameObject != null && !found.Contains(comp.gameObject))
                {
                    found.Add(comp.gameObject);
                    _allNPCs.Add(new NPCData(
                        comp.gameObject,
                        comp.transform.position,
                        comp.gameObject.name,
                        isShop
                    ));
                }
            }
        }

        /// <summary>NPC가 상점 NPC인지 확인 (이름/컴포넌트 기반)</summary>
        private static bool IsShopNPCByScript(GameObject go)
        {
            // ShopWindow를 참조하는 NPC가 있다면 추가 (현재는 이름 기반)
            string name = go.name.ToLower();
            return name.Contains("shop") || name.Contains("상점") || name.Contains("상인") || name.Contains("merchant");
        }

        // ================================================================
        // 시간 계산
        // ================================================================

        /// <summary>시간(hour)으로 시간대 계산</summary>
        public static TimePeriod CalculatePeriod(int hour)
        {
            if (hour >= 4 && hour < 6)
                return TimePeriod.Dawn;      // 04~06: 기상
            if (hour >= 6 && hour < 18)
                return TimePeriod.Day;       // 06~18: 활동
            if (hour >= 18 && hour < 20)
                return TimePeriod.Evening;   // 18~20: 귀가
            return TimePeriod.Night;         // 20~04: 수면
        }

        /// <summary>시간대의 한글 이름 반환</summary>
        public static string GetPeriodName(TimePeriod period)
        {
            return period switch
            {
                TimePeriod.Dawn    => "🌅 새벽",
                TimePeriod.Day     => "☀️ 낮",
                TimePeriod.Evening => "🌆 저녁",
                TimePeriod.Night   => "🌙 밤",
                _                  => "❓ 알 수 없음"
            };
        }

        /// <summary>시간대별 상태 문자열 (말풍선용)</summary>
        public static string GetNPCStatusText(TimePeriod period, bool isShopNPC)
        {
            return period switch
            {
                TimePeriod.Dawn    => isShopNPC ? "🔰 준비 중" : "🌅 출근 중",
                TimePeriod.Day     => isShopNPC ? "🛒 영업 중" : "🚶 일하는 중",
                TimePeriod.Evening => "🏠 귀가 중",
                TimePeriod.Night   => "😴 잠자는 중",
                _                  => ""
            };
        }

        // ================================================================
        // 이벤트 처리
        // ================================================================

        private void OnGameTimeChanged(int hour, int minute)
        {
            TimePeriod newPeriod = CalculatePeriod(hour);
            if (newPeriod != CurrentPeriod)
            {
                TimePeriod oldPeriod = CurrentPeriod;
                CurrentPeriod = newPeriod;

                if (_verbose)
                    Debug.Log($"[NPCDailyCycle] 시간대 변경: {GetPeriodName(oldPeriod)} → {GetPeriodName(newPeriod)}");

                ApplyPeriodToAll(newPeriod);
                OnTimePeriodChanged?.Invoke(newPeriod);
            }
        }

        // ================================================================
        // NPC 상태 적용
        // ================================================================

        /// <summary>
        /// 현재 시간대를 모든 NPC에 적용합니다.
        /// - Dawn: NPC 활성화 (기상)
        /// - Day: NPC 활성화, 원래 위치 유지
        /// - Evening: NPC 활성화 (귀가 중 — 간단히 원래 위치 유지)
        /// - Night: NPC 비활성화 (수면)
        /// </summary>
        private void ApplyPeriodToAll(TimePeriod period)
        {
            bool isActive = period != TimePeriod.Night;

            foreach (var npc in _allNPCs)
            {
                if (npc.gameObject == null) continue;

                // 밤에는 비활성화
                npc.gameObject.SetActive(isActive);

                if (_verbose && npc.gameObject.activeSelf != isActive)
                    Debug.Log($"[NPCDailyCycle] {npc.npcName}: {(isActive ? "활성화" : "비활성화")} ({GetPeriodName(period)})");
            }
        }

        // ================================================================
        // 외부 API
        // ================================================================

        /// <summary>
        /// 등록된 모든 NPC 게임오브젝트 반환 (읽기 전용)
        /// </summary>
        public IReadOnlyList<GameObject> GetAllNPCs()
        {
            var list = new List<GameObject>(_allNPCs.Count);
            foreach (var npc in _allNPCs)
            {
                if (npc.gameObject != null)
                    list.Add(npc.gameObject);
            }
            return list;
        }

        /// <summary>
        /// NPC의 현재 상태 텍스트 반환 (말풍선 등에 사용)
        /// </summary>
        public string GetNPCStatusTextFor(GameObject npcObj)
        {
            if (npcObj == null) return "";

            foreach (var npc in _allNPCs)
            {
                if (npc.gameObject == npcObj)
                    return GetNPCStatusText(CurrentPeriod, npc.isShopNPC);
            }
            return "";
        }

        /// <summary>
        /// 수동으로 NPC 새로고침 (런타임에 NPC가 추가/제거된 경우 호출)
        /// </summary>
        public void RefreshAllNPCs()
        {
            FindAllNPCs();
            ApplyPeriodToAll(CurrentPeriod);
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!_verbose) return;

            // 우측 상단에 현재 시간대 표시 (디버그)
            string text = $"[NPCDailyCycle] {GetPeriodName(CurrentPeriod)} (NPC: {_allNPCs.Count}명)";
            GUI.Label(new Rect(Screen.width - 280, 10, 270, 24), text);
        }
#endif
    }
}
