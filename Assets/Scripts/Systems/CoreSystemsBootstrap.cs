using System.Linq;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

/// <summary>
/// 씬 시작 시 핵심 시스템 강제 초기화
/// GameManager reflection 실패 대비 보장용
/// </summary>
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