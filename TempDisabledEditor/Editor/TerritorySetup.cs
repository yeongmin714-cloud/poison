using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;

/// <summary>
/// Phase 5.1: 첫 번째 영지 구성용 Placeholder 생성기
/// 사장님이 GLB를 제공하기 전까지 사용할 임시 건물과 병사를 생성합니다.
/// </summary>
public static class TerritorySetup
{
    [MenuItem("Tools/Territory - Create First Territory")]
    public static void CreateFirstTerritory()
    {
        // 현재 씬 열기
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");

        // 1. 플레이어가 있는 위치 찾기 (기준점)
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        if (player == null)
        {
            Debug.LogWarning("[TerritorySetup] Player 오브젝트를 찾을 수 없습니다! 원점에 생성합니다.");
            playerPos = Vector3.zero;
        }

        // 2. 건물 placeholder 생성
        CreateBuildingPlaceholders(playerPos);
        // 3. 입구 경비병 생성
        CreateGuardPlaceholders(playerPos);
        // 4. 영지 관리자 생성
        CreateTerritoryManager(playerPos);

        Debug.Log("[TerritorySetup] 첫 번째 영지 구성 Placeholder 생성 완료!");
        Debug.Log("[TerritorySetup] GLB 파일을 Assets/Resources/Models/UserProvided/ 에 넣으면 자동 교체됩니다.");
    }

    private static void CreateBuildingPlaceholders(Vector3 center)
    {
        // 상점
        CreateBuilding("Shop", BuildingPlaceholder.BuildingType.Shop, center + new Vector3(-5, 0, 0));
        // 제작소
        CreateBuilding("CraftHouse", BuildingPlaceholder.BuildingType.CraftHouse, center + new Vector3(5, 0, 0));
        // 교회
        CreateBuilding("Church", BuildingPlaceholder.BuildingType.Church, center + new Vector3(0, 0, -5));
        // NPC 집 1
        CreateBuilding("NPCHouse1", BuildingPlaceholder.BuildingType.NPCHouse, center + new Vector3(-5, 0, -5));
        // NPC 집 2
        CreateBuilding("NPCHouse2", BuildingPlaceholder.BuildingType.NPCHouse, center + new Vector3(5, 0, -5));
        // NPC 집 3
        CreateBuilding("NPCHouse3", BuildingPlaceholder.BuildingType.NPCHouse, center + new Vector3(-5, 0, 5));
        // NPC 집 4
        CreateBuilding("NPCHouse4", BuildingPlaceholder.BuildingType.NPCHouse, center + new Vector3(5, 0, 5));
    }

    private static void CreateBuilding(string name, BuildingPlaceholder.BuildingType type, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(3, 2, 3); // 넓이 3, 높이 2, 깊이 3

        var placeholder = go.AddComponent<BuildingPlaceholder>();
        placeholder.buildingType = type;
        placeholder.buildingName = name;

        // 건물을 위한 간단한 콜라이더 (트리거 아님)
        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        // 건물 위에 라벨 표시용 빈 오브젝트 (선택 사항)
        var labelGo = new GameObject($"{name}Label");
        labelGo.transform.SetParent(go.transform);
        labelGo.transform.localPosition = new Vector3(0, 1.5f, 0);
        var textMesh = labelGo.AddComponent<TextMesh>();
        textMesh.text = name;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.characterSize = 0.1f;
        textMesh.color = Color.white;
    }

    private static void CreateGuardPlaceholders(Vector3 center)
    {
        // 입구 경비병 2명 (문 양쪽)
        CreateGuard("GuardLeft", center + new Vector3(-2, 0, 2));
        CreateGuard("GuardRight", center + new Vector3(2, 0, 2));

        // 추가 경비병 (주변 순찰용)
        CreateGuard("GuardPatrol1", center + new Vector3(0, 0, 8));
        CreateGuard("GuardPatrol2", center + new Vector3(8, 0, 0));
        CreateGuard("GuardPatrol3", center + new Vector3(-8, 0, 0));
        CreateGuard("GuardPatrol4", center + new Vector3(0, 0, -8));
    }

    private static void CreateGuard(string name, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(1.5f, 2f, 1.5f); // 약간 인간형 모양

        var placeholder = go.AddComponent<GuardPlaceholder>();
        // 간단한 이름 설정
        placeholder.GetType().GetField("guardName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(placeholder, name);
        placeholder.GetType().GetField("level", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(placeholder, 1);
        placeholder.GetType().GetField("nation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(placeholder, "동");

        // 캡슐 콜라이더는 트리거로 설정하지 않음 (물리적 충돌 가능)
        // 필요하다면 나중에 스크립트로 트리거 영역 추가
    }

    private static void CreateTerritoryManager(Vector3 center)
    {
        var go = new GameObject("TerritoryManager");
        go.transform.position = center;
        go.AddComponent<TerritoryManager>();
        Debug.Log("[TerritorySetup] TerritoryManager 오브젝트 생성됨");
    }

    [MenuItem("Tools/Territory - Create First Territory", true)]
    private static bool Validate()
    {
        return true;
    }
}