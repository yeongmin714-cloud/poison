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

        // 3-3. ArmorVisualAttachSystem 생성 (없는 경우) — 방어구 슬롯 GLB 비주얼 부착 (2026-09-13 P6)
        EnsureArmorVisualAttachSystem();

        // 3-3-1. GuardVisualAttachSystem 생성 (없는 경우) — 병사 방어구 GLB 비주얼 본 부착 (ADDITIVE)
        EnsureGuardVisualAttachSystem();

        // 3-5. RTS 인터랙션 커서/호버/명령 (ADDITIVE) — OS커서 Ctrl표시·컸텍스트 아이콘(검/곡갱이/삽)·좌클릭 병사명령
        ContextCommandRouter.Ensure();

        // 3-6. 농사(경지·약초) / 채집(풀) (ADDITIVE) — 파종→성장→수확 재화·채집 리스폰
        FarmingSystem.Ensure();
        GatheringSystem.Ensure();

        // 3-6-1. GuardTaskSystem 생성 (없는 경우) — 영지 병사 역할(사냥/공격동행/수비문지기/채집/농경) 자동 수행
        GuardTaskSystem.Ensure();

        // 3-4. EquipmentManager 생성 (없는 경우) — 장비 슬롯 관리 (2026-09-14: 장착 안 되는 버그 수정)
        EnsureEquipmentManager();

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
    /// ArmorVisualAttachSystem 싱글톤 보장 — 방어구 슬롯(Helmet/Armor/Shoes/Gloves/Back) GLB 비주얼 부착
    /// EquipmentManager.OnEquipmentChanged 구독 → 플레이어(Tag Player) 본에 장착 GLB 부착/파괴.
    /// 무기 슬롯은 WeaponEquipManager 소유이므로 이 시스템이 건드리지 않는다.
    /// </summary>
    private void EnsureArmorVisualAttachSystem()
    {
        try
        {
            // 중복 가드 — 씬에 이미 배치(비활성 포함)된 경우 생성하지 않음
            var existing = FindAnyObjectByType<ArmorVisualAttachSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[CoreSystemsBootstrap] ArmorVisualAttachSystem 이미 존재");
                return;
            }

            var go = new GameObject("ArmorVisualAttachSystem");
            go.AddComponent<ArmorVisualAttachSystem>();
            Debug.Log("[CoreSystemsBootstrap] ArmorVisualAttachSystem 생성됨 — 방어구 비주얼 부착 활성화");
        }
        catch (System.Exception e)
        {
            // 단일 시스템 생성 실패가 전체 부트를 깨지 않도록 격리
            Debug.LogError($"[CoreSystemsBootstrap] ArmorVisualAttachSystem 생성 실패: {e.Message}");
        }
    }

    /// <summary>
    /// GuardVisualAttachSystem 싱글톤 보장 — 병사(GuardPlaceholder) 착용 방어구 GLB를 본 릭에 부착.
    /// 플레이어 ArmorVisualAttachSystem과 무관한 분리 시스템. 티어 기본 폴백으로 병사가 항상 방어구를 입음.
    /// </summary>
    private void EnsureGuardVisualAttachSystem()
    {
        try
        {
            var existing = FindAnyObjectByType<GuardVisualAttachSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[CoreSystemsBootstrap] GuardVisualAttachSystem 이미 존재");
                return;
            }

            var go = new GameObject("GuardVisualAttachSystem");
            go.AddComponent<GuardVisualAttachSystem>();
            Debug.Log("[CoreSystemsBootstrap] GuardVisualAttachSystem 생성됨 — 병사 방어구 비주얼 부착 활성화");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CoreSystemsBootstrap] GuardVisualAttachSystem 생성 실패: {e.Message}");
        }
    }

    /// <summary>
    /// EquipmentManager 싱글톤 보장 — 장비 슬롯(Helmet/Armor/Weapon/Shoes/Gloves/Back) 관리
    /// 인벤토리 우클릭/드래그 장착, 장비창 UI, 방어구 비주얼 부착 시스템 등이 모두 이 인스턴스를 참조.
    /// </summary>
    private void EnsureEquipmentManager()
    {
        try
        {
            // 중복 가드 — 씬에 이미 배치(비활성 포함)된 경우 생성하지 않음
            var existing = FindAnyObjectByType<EquipmentManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[CoreSystemsBootstrap] EquipmentManager 이미 존재");
                return;
            }

            var go = new GameObject("EquipmentManager");
            go.AddComponent<EquipmentManager>();
            Debug.Log("[CoreSystemsBootstrap] EquipmentManager 생성됨 — 장비 슬롯 관리 활성화");
        }
        catch (System.Exception e)
        {
            // 단일 시스템 생성 실패가 전체 부트를 깨지 않도록 격리
            Debug.LogError($"[CoreSystemsBootstrap] EquipmentManager 생성 실패: {e.Message}");
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

        // [P31-C] 24개 마을 절차 건물 배치 + 대표 마을 실외 상점 (VillagePlacementSystem 좌표 기반)
        VillageBuilder.BuildAllVillages();
        // [P31-D] 마을 주민 NPC 10~15명/마을 배치 (건물 완성 후)
        VillageNpcSpawner.BuildAllVillageNPCs();
    }
}