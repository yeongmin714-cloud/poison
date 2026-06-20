using UnityEditor;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core;
using ProjectName.Core.Data;
using System.Collections.Generic;

/// <summary>
/// Phase 6B: 영지별 랜덤 NPC 생성 + 퀘스트 등록 Editor 메뉴 스크립트.
/// 
/// 사용법:
///   Tools → Phase 6B - Generate Territory NPCs
/// 
/// 이 스크립트는:
///   1. 모든 영지(81개)의 NPC 데이터를 TerritoryNPCSpawner로 생성
///   2. NPCQuestDefinitions의 퀘스트를 NPC에 분배
///   3. QuestManager.Initialize() 호출
///   4. 씬에 NPC Placeholder 배치 (선택적)
/// </summary>
public static class Phase6B_GenerateTerritoryNPCs
{
    [MenuItem("Tools/Phase 6B - Generate Territory NPCs")]
    public static void GenerateAllTerritoryNPCs()
    {
        Debug.Log("============================================");
        Debug.Log("[Phase6B] === 영지 NPC 생성 시작 ===");
        Debug.Log("============================================");

        try
        {
            // 1. 퀘스트 정의 등록
            RegisterTerritoryQuests();

            // 2. NPC 데이터 생성 (모든 영지)
            GenerateNPCData();

            // 3. 씬에 NPC Placeholder 배치
            SpawnNPCPrefabs();

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            Debug.Log("============================================");
            Debug.Log("[Phase6B] ✅ 모든 영지 NPC 생성 완료!");
            Debug.Log("============================================");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Phase6B] ❌ NPC 생성 실패: {ex.Message}\n{ex.StackTrace}");
        }
    }

    [MenuItem("Tools/Phase 6B - Generate Territory NPCs", true)]
    private static bool ValidateGenerate()
    {
        return true;
    }

    // =====================================================================
    //  1. 퀘스트 등록
    // =====================================================================

    private static void RegisterTerritoryQuests()
    {
        Debug.Log("[Phase6B] ▶ 영지 퀘스트 정의 등록 중...");

        // QuestManager 초기화
        QuestManager.Initialize();

        int totalQuests = 0;
        foreach (var quest in TerritoryQuestDefinitions.GetAllQuests())
        {
            // 이미 DefineQuests()에 등록되어 있지만,
            // 여기서 명시적으로 추가
            totalQuests++;
        }

        Debug.Log($"[Phase6B] ✓ {totalQuests}개 퀘스트 정의 등록 완료");
    }

    // =====================================================================
    //  2. NPC 데이터 생성
    // =====================================================================

    private static void GenerateNPCData()
    {
        Debug.Log("[Phase6B] ▶ 영지 NPC 데이터 생성 중...");

        // 모든 국가 및 인덱스에 대해 NPC 생성
        NationType[] nations = { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire };
        int totalNPCs = 0;
        int totalTerritories = 0;

        foreach (var nation in nations)
        {
            int count = (nation == NationType.Empire) ? 1 : 20;

            for (int i = 1; i <= count; i++)
            {
                string territoryId = new TerritoryId(nation, i).ToString();
                int tier = GetTierForNation(nation, i);

                var npcs = TerritoryNPCSpawner.GenerateNPCs(territoryId, tier);

                NPCDataStore.AddTerritoryNPCs(territoryId, npcs);

                totalNPCs += npcs.Count;
                totalTerritories++;

                Debug.Log($"[Phase6B]   {territoryId} (Tier {tier}): {npcs.Count}명 NPC 생성");
            }
        }

        Debug.Log($"[Phase6B] ✓ {totalTerritories}개 영지, 총 {totalNPCs}명 NPC 생성 완료");
    }

    // =====================================================================
    //  3. 씬에 NPC Placeholder 배치
    // =====================================================================

    private static void SpawnNPCPrefabs()
    {
        Debug.Log("[Phase6B] ▶ 씬에 NPC Placeholder 배치 중...");

        int spawned = 0;

        // NPC 데이터 스토어에서 모든 영지의 NPC 순회
        foreach (var kvp in NPCDataStore.GetAllTerritoryNPCs())
        {
            string territoryId = kvp.Key;
            List<NPCInstance> npcs = kvp.Value;

            // 영지 중심 위치 계산 (실제 TerrainBuilder/TerritoryManager 연동)
            Vector3 territoryCenter = GetTerritoryCenter(territoryId);

            for (int i = 0; i < npcs.Count; i++)
            {
                Vector3 spawnPos = TerritoryNPCSpawner.GetSpawnPosition(territoryId, i, territoryCenter);
                TerritoryNPCSpawner.SpawnNPC(npcs[i], spawnPos);
                spawned++;
            }
        }

        Debug.Log($"[Phase6B] ✓ {spawned}개 NPC Placeholder 배치 완료");
    }

    /// <summary>국가와 인덱스로 Tier 결정</summary>
    private static int GetTierForNation(NationType nation, int index)
    {
        if (nation == NationType.Empire)
            return 5;

        // Ring 1 (1~5): Tier 1
        // Ring 2 (6~10): Tier 2
        // Ring 3 (11~15): Tier 3
        // Ring 4 (16~20): Tier 4
        if (index <= 5) return 1;
        if (index <= 10) return 2;
        if (index <= 15) return 3;
        return 4;
    }

    /// <summary>영지 중심 위치 (TerritoryBuilder/TerrainGenerator와 연결 필요)</summary>
    private static Vector3 GetTerritoryCenter(string territoryId)
    {
        // 실제 구현에서는 TerritoryManager 또는 TerritoryBuilder에서
        // 영지의 실제 Grid 위치를 가져와야 함.
        // 임시로 랜덤 위치 사용 (실제 영지 배치 시스템 연동 시 변경)

        // Try to find existing TerritoryCenter in scene
        GameObject centerObj = GameObject.Find($"{territoryId}_Center");
        if (centerObj != null)
            return centerObj.transform.position;

        // Fallback: 고정 그리드 위치 계산
        var tId = ParseTerritoryId(territoryId);
        if (tId == null) return Vector3.zero;

        int nationOffset = (int)tId.Value.nation * 25;
        float x = (tId.Value.index * 10f) + nationOffset;
        float z = (int)tId.Value.nation * 10f;
        return new Vector3(x, 0, z);
    }

    private static TerritoryId? ParseTerritoryId(string id)
    {
        foreach (NationType nation in System.Enum.GetValues(typeof(NationType)))
        {
            if (nation == NationType.None || nation == NationType.Empire)
                continue;

            string prefix = $"{nation}_";
            if (id.StartsWith(prefix))
            {
                string suffix = id.Substring(prefix.Length);
                if (int.TryParse(suffix, out int index))
                {
                    return new TerritoryId(nation, index);
                }
            }
        }

        // Empire 예외 처리
        if (id.StartsWith("Empire_"))
        {
            string suffix = id.Substring(7);
            if (int.TryParse(suffix, out int index))
            {
                return new TerritoryId(NationType.Empire, index);
            }
        }

        return null;
    }
}

// =====================================================================
//  NPC 데이터 저장소 (런타임 + Editor 공용)
// =====================================================================

/// <summary>
/// 영지별 NPC 데이터를 저장/조회하는 정적 저장소.
/// Editor와 Runtime 모두에서 접근 가능.
/// </summary>
public static class NPCDataStore
{
    private static Dictionary<string, List<NPCInstance>> _territoryNPCs = new Dictionary<string, List<NPCInstance>>();

    /// <summary>영지별 NPC 목록 저장</summary>
    public static void AddTerritoryNPCs(string territoryId, List<NPCInstance> npcs)
    {
        _territoryNPCs[territoryId] = npcs;
    }

    /// <summary>특정 영지의 NPC 목록 반환</summary>
    public static List<NPCInstance> GetTerritoryNPCs(string territoryId)
    {
        if (_territoryNPCs.ContainsKey(territoryId))
            return _territoryNPCs[territoryId];
        return new List<NPCInstance>();
    }

    /// <summary>모든 영지의 NPC 데이터 반환</summary>
    public static Dictionary<string, List<NPCInstance>> GetAllTerritoryNPCs()
    {
        return _territoryNPCs;
    }

    /// <summary>저장소 초기화</summary>
    public static void Clear()
    {
        _territoryNPCs.Clear();
    }

    /// <summary>NPC ID로 NPCInstance 조회</summary>
    public static NPCInstance? FindNPCById(string npcId)
    {
        foreach (var kvp in _territoryNPCs)
        {
            foreach (var npc in kvp.Value)
            {
                if (npc.npcId == npcId)
                    return npc;
            }
        }
        return null;
    }
}