using UnityEditor;
using UnityEngine;
using ProjectName.Core;
using System.IO;

/// <summary>
/// Phase 1.6: Drop Table ScriptableObject 4개를 자동 생성합니다.
/// Tools/Phase 1.6 - Create Drop Tables 메뉴에서 실행.
/// </summary>
public static class Phase1C_CreateDropTables
{
    private const string MenuPath = "Tools/Phase 1.6 - Create Drop Tables";
    private const string DropTablesDir = "Assets/Resources/DropTables";

    [MenuItem(MenuPath)]
    public static void CreateAllDropTables()
    {
        Debug.Log("========================================");
        Debug.Log("[Phase1.6] Drop Table 생성 시작...");
        Debug.Log("========================================");

        // 디렉토리 생성
        Directory.CreateDirectory(DropTablesDir);

        // 4개 DropTable 생성
        CreateEarlyMonsterDropTable();
        CreateMidMonsterDropTable();
        CreateLateMonsterDropTable();
        CreateSoldierDropTable();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 생성된 에셋 로드 테스트
        TestLoadDropTables();

        Debug.Log("========================================");
        Debug.Log("[Phase1.6] ✅ Drop Table 4개 생성 완료!");
        Debug.Log("========================================");
    }

    // ================================================================
    //  a) EarlyMonsterDropTable.asset (Beginner 티어)
    // ================================================================
    private static void CreateEarlyMonsterDropTable()
    {
        string path = $"{DropTablesDir}/EarlyMonsterDropTable.asset";
        if (File.Exists(path))
        {
            Debug.Log($"[Phase1.6] Already exists: {path}");
            return;
        }

        var table = ScriptableObject.CreateInstance<DropTable>();
        table.entries = new DropTable.DropEntry[]
        {
            new DropTable.DropEntry
            {
                item = PlayerInventory.RabbitMeat,
                minCount = 1,
                maxCount = 2,
                dropChance = 0.80f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.RabbitFur,
                minCount = 1,
                maxCount = 1,
                dropChance = 0.50f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.Gold,
                minCount = 1,
                maxCount = 3,
                dropChance = 0.30f
            }
        };

        AssetDatabase.CreateAsset(table, path);
        EditorUtility.SetDirty(table);
        Debug.Log($"[Phase1.6] ✅ Created: {path}");
    }

    // ================================================================
    //  b) MidMonsterDropTable.asset (Intermediate 티어)
    // ================================================================
    private static void CreateMidMonsterDropTable()
    {
        string path = $"{DropTablesDir}/MidMonsterDropTable.asset";
        if (File.Exists(path))
        {
            Debug.Log($"[Phase1.6] Already exists: {path}");
            return;
        }

        var table = ScriptableObject.CreateInstance<DropTable>();
        table.entries = new DropTable.DropEntry[]
        {
            new DropTable.DropEntry
            {
                item = PlayerInventory.BoarMeat,
                minCount = 2,
                maxCount = 3,
                dropChance = 0.85f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.BoarLeather,
                minCount = 1,
                maxCount = 2,
                dropChance = 0.60f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.BoarTusk,
                minCount = 1,
                maxCount = 1,
                dropChance = 0.20f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.Gold,
                minCount = 2,
                maxCount = 5,
                dropChance = 0.40f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.WolfTooth,
                minCount = 1,
                maxCount = 1,
                dropChance = 0.15f
            }
        };

        AssetDatabase.CreateAsset(table, path);
        EditorUtility.SetDirty(table);
        Debug.Log($"[Phase1.6] ✅ Created: {path}");
    }

    // ================================================================
    //  c) LateMonsterDropTable.asset (Advanced 티어)
    // ================================================================
    private static void CreateLateMonsterDropTable()
    {
        string path = $"{DropTablesDir}/LateMonsterDropTable.asset";
        if (File.Exists(path))
        {
            Debug.Log($"[Phase1.6] Already exists: {path}");
            return;
        }

        var table = ScriptableObject.CreateInstance<DropTable>();
        table.entries = new DropTable.DropEntry[]
        {
            new DropTable.DropEntry
            {
                item = PlayerInventory.WolfMeat,
                minCount = 3,
                maxCount = 5,
                dropChance = 0.90f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.WolfFur,
                minCount = 1,
                maxCount = 2,
                dropChance = 0.50f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.WolfTooth,
                minCount = 1,
                maxCount = 2,
                dropChance = 0.30f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.Gold,
                minCount = 5,
                maxCount = 10,
                dropChance = 0.60f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.SwordWood,
                minCount = 1,
                maxCount = 1,
                dropChance = 0.10f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.ClothArmor,
                minCount = 1,
                maxCount = 1,
                dropChance = 0.10f
            }
        };

        AssetDatabase.CreateAsset(table, path);
        EditorUtility.SetDirty(table);
        Debug.Log($"[Phase1.6] ✅ Created: {path}");
    }

    // ================================================================
    //  d) SoldierDropTable.asset
    // ================================================================
    private static void CreateSoldierDropTable()
    {
        string path = $"{DropTablesDir}/SoldierDropTable.asset";
        if (File.Exists(path))
        {
            Debug.Log($"[Phase1.6] Already exists: {path}");
            return;
        }

        var table = ScriptableObject.CreateInstance<DropTable>();
        table.entries = new DropTable.DropEntry[]
        {
            new DropTable.DropEntry
            {
                item = PlayerInventory.Gold,
                minCount = 1,
                maxCount = 5,
                dropChance = 0.90f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.SwordWood,
                minCount = 1,
                maxCount = 1,
                dropChance = 0.30f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.SpearWood,
                minCount = 1,
                maxCount = 1,
                dropChance = 0.20f
            },
            new DropTable.DropEntry
            {
                item = PlayerInventory.LeatherArmor,
                minCount = 1,
                maxCount = 1,
                dropChance = 0.10f
            }
        };

        AssetDatabase.CreateAsset(table, path);
        EditorUtility.SetDirty(table);
        Debug.Log($"[Phase1.6] ✅ Created: {path}");
    }

    // ================================================================
    //  생성된 DropTable 로드 테스트
    // ================================================================
    private static void TestLoadDropTables()
    {
        Debug.Log("[Phase1.6] === DropTable 로드 테스트 시작 ===");

        string[] names = new string[]
        {
            "EarlyMonsterDropTable",
            "MidMonsterDropTable",
            "LateMonsterDropTable",
            "SoldierDropTable"
        };

        bool allLoaded = true;
        foreach (string name in names)
        {
            var loaded = Resources.Load<DropTable>($"DropTables/{name}");
            if (loaded != null)
            {
                Debug.Log($"[Phase1.6] ✅ Resources.Load 성공: DropTables/{name} (entries: {loaded.entries?.Length ?? 0})");
            }
            else
            {
                Debug.LogError($"[Phase1.6] ❌ Resources.Load 실패: DropTables/{name}");
                allLoaded = false;
            }
        }

        if (allLoaded)
        {
            Debug.Log("[Phase1.6] ✅ 모든 DropTable 로드 테스트 통과!");
        }
        else
        {
            Debug.LogWarning("[Phase1.6] ⚠ 일부 DropTable 로드 실패. AssetDatabase.Refresh() 후 다시 시도하세요.");
        }
    }

    /// <summary>
    /// EditorAutoSetup에서 호출할 통합 생성 메서드.
    /// </summary>
    public static void AutoCreateDropTables()
    {
        Debug.Log("[AutoSetup] DropTable 생성 단계...");
        Directory.CreateDirectory(DropTablesDir);
        CreateEarlyMonsterDropTable();
        CreateMidMonsterDropTable();
        CreateLateMonsterDropTable();
        CreateSoldierDropTable();
        Debug.Log("[AutoSetup] DropTable 생성 완료");
    }
}