using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Core
{
    /// <summary>
    /// Base game manager — entry point for game initialization.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>C20-01: Current game difficulty level (0=Easy, 1=Normal, 2=Hard).</summary>
        public static int CurrentDifficulty { get; set; } = 0;

        [SerializeField] private bool _debugMode = false;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            gameObject.AddComponent<HerbTester>();
            gameObject.AddComponent<ComboTester>();
            gameObject.AddComponent<CookingTester>();
            gameObject.AddComponent<DishTester>();
            gameObject.AddComponent<ConsumableTester>();
            gameObject.AddComponent<BuffManager>();
        }

        private void Start()
        {
            if (_debugMode)
                Debug.Log("[GameManager] Game initialized in debug mode");
            else
                Debug.Log("[GameManager] Game initialized");

            InitializeSystems();
            EnsureTerritoryManager();
        }

        private void EnsureTerritoryManager()
        {
            // Use reflection to access Systems types (avoid circular reference)
            var tmType = System.Type.GetType("ProjectName.Systems.TerritoryManager");
            if (tmType == null)
                tmType = FindTypeInAssemblies("TerritoryManager");

            var instanceField = tmType?.GetField("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var instance = instanceField?.GetValue(null);

            if (instance == null && tmType != null)
            {
                var go = new GameObject("TerritoryManager");
                go.AddComponent(tmType);

                var tbType = System.Type.GetType("ProjectName.Systems.TerritoryBuilder");
                if (tbType == null)
                    tbType = FindTypeInAssemblies("TerritoryBuilder");
                if (tbType != null)
                    go.AddComponent(tbType);

                Debug.Log("[GameManager] TerritoryManager 자동 생성됨");
            }
        }

        private static System.Type FindTypeInAssemblies(string typeName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(typeName);
                if (type != null) return type;
                type = asm.GetType("ProjectName.Systems." + typeName);
                if (type != null) return type;
            }
            return null;
        }

        private void InitializeSystems()
        {
            // TODO: Initialize game systems
        }
    }
}