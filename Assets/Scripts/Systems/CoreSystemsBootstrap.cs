using System.Linq;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

/// <summary>
/// 씬 시작 시 핵심 시스템 강제 초기화
/// GameManager reflection 실패 대비 보장용
/// Edit 모드에서도 실행되어 씬 열자마자 시스템 초기화
/// </summary>
[ExecuteInEditMode]
public class CoreSystemsBootstrap : MonoBehaviour
{
    [Header("초기화 설정")]
    [SerializeField] private bool _initializeOnAwake = true;
    [SerializeField] private bool _buildAllTerritories = true;

    private void Awake()
    {
        if (!_initializeOnAwake) return;

        // 1. TerritoryDatabase 강제 초기화 (Lazy<T> 인스턴스 생성)
        ForceInitializeTerritoryDatabase();

        // 2. TerritoryManager 생성 (없는 경우)
        EnsureTerritoryManager();

        // 3. TerritoryBuilder 생성 (없는 경우)
        EnsureTerritoryBuilder();

        // 3-1. RTSCommandSystem 생성 (없는 경우) — 우클릭 공격/이동·Ctrl 일제·H 중단
        EnsureRTSCommandSystem();

        // 3-2. GuardSelectionManager 생성 (없는 경우) — 좌클릭 드래그 병사 선택
        EnsureGuardSelectionManager();

        // 4. 영지 전체 빌드
        if (_buildAllTerritories)
        {
            BuildAllTerritories();
        }

        Debug.Log("[CoreSystemsBootstrap] 핵심 시스템 초기화 완료");
    }

    /// <summary>
    /// TerritoryDatabase 인스턴스 강제 생성
    /// </summary>
    private void ForceInitializeTerritoryDatabase()
    {
        try
        {
            var db = TerritoryDatabase.Instance; // Lazy<T>.Value 접근으로 인스턴스 생성 강제
            Debug.Log($"[CoreSystemsBootstrap] TerritoryDatabase 초기화됨: {db.GetAllDefinitions().Count()}개 영지");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CoreSystemsBootstrap] TerritoryDatabase 초기화 실패: {e.Message}");
        }
    }

    /// <summary>
    /// TerritoryManager 싱글톤 보장
    /// </summary>
    private void EnsureTerritoryManager()
    {
        var existing = FindAnyObjectByType<TerritoryManager>();
        if (existing != null)
        {
            Debug.Log("[CoreSystemsBootstrap] TerritoryManager 이미 존재");
            return;
        }

        var go = new GameObject("TerritoryManager");
        var tm = go.AddComponent<TerritoryManager>();
        Debug.Log("[CoreSystemsBootstrap] TerritoryManager 생성됨");
    }

    /// <summary>
    /// TerritoryBuilder 싱글톤 보장 (TerritoryManager와 같은 GO에)
    /// </summary>
    private void EnsureTerritoryBuilder()
    {
        var tm = FindAnyObjectByType<TerritoryManager>();
        if (tm == null) return;

        var existing = tm.GetComponent<TerritoryBuilder>();
        if (existing != null)
        {
            Debug.Log("[CoreSystemsBootstrap] TerritoryBuilder 이미 존재");
            return;
        }

        tm.gameObject.AddComponent<TerritoryBuilder>();
        Debug.Log("[CoreSystemsBootstrap] TerritoryBuilder 생성됨");
    }

    /// <summary>
    /// RTSCommandSystem 싱글톤 보장 — 우클릭 공격/이동, Ctrl+우클릭 일제 공격, H키 중단
    /// </summary>
    private void EnsureRTSCommandSystem()
    {
        try
        {
            // 중복 가드 — 씬에 이미 배치(비활성 포함)된 경우 생성하지 않음
            var existing = FindAnyObjectByType<RTSCommandSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[CoreSystemsBootstrap] RTSCommandSystem 이미 존재");
                return;
            }

            var go = new GameObject("RTSCommandSystem");
            go.AddComponent<RTSCommandSystem>();
            Debug.Log("[CoreSystemsBootstrap] RTSCommandSystem 생성됨 — 우클릭 공격/이동·Ctrl 일제·H 중단 활성화");
        }
        catch (System.Exception e)
        {
            // 단일 시스템 생성 실패가 전체 부트를 깨지 않도록 격리
            Debug.LogError($"[CoreSystemsBootstrap] RTSCommandSystem 생성 실패: {e.Message}");
        }
    }

    /// <summary>
    /// GuardSelectionManager 싱글톤 보장 — 좌클릭 드래그(10px 이상)로 자기 소속 병사만 선택
    /// 단순 좌클릭(10px 미만)은 무시되므로 PlayerCombat 좌클릭 공격과 자연 분리
    /// </summary>
    private void EnsureGuardSelectionManager()
    {
        try
        {
            // 중복 가드 — 씬에 이미 배치(비활성 포함)된 경우 생성하지 않음
            var existing = FindAnyObjectByType<GuardSelectionManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[CoreSystemsBootstrap] GuardSelectionManager 이미 존재");
                return;
            }

            var go = new GameObject("GuardSelectionManager");
            go.AddComponent<GuardSelectionManager>();
            Debug.Log("[CoreSystemsBootstrap] GuardSelectionManager 생성됨 — 좌클릭 드래그 병사 선택 활성화");
        }
        catch (System.Exception e)
        {
            // 단일 시스템 생성 실패가 전체 부트를 깨지 않도록 격리
            Debug.LogError($"[CoreSystemsBootstrap] GuardSelectionManager 생성 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 전체 영지 건물/병사 생성
    /// </summary>
    private void BuildAllTerritories()
    {
        var builder = FindAnyObjectByType<TerritoryBuilder>();
        if (builder != null)
        {
            builder.BuildAllTerritories();
            Debug.Log("[CoreSystemsBootstrap] BuildAllTerritories 호출됨");
        }
        else
        {
            Debug.LogWarning("[CoreSystemsBootstrap] TerritoryBuilder가 없어 빌드 불가");
        }
    }
}