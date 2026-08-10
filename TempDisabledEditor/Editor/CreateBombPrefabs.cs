using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ProjectName.Systems;

public static class CreateBombPrefabs
{
    [MenuItem("Tools/Create Bomb Prefabs")]
    public static void Create()
    {
        string bombFolder = "Assets/Resources/Bombs";
        if (!AssetDatabase.IsValidFolder(bombFolder))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Bombs");
        }

        // Define bomb types and their properties
        var bombTypes = new List<BombType> { BombType.Explosive, BombType.PoisonGas, BombType.Smoke, BombType.Molotov };
        var colors = new Dictionary<BombType, Color>
        {
            { BombType.Explosive, Color.red },
            { BombType.PoisonGas, Color.green },
            { BombType.Smoke, Color.grey },
            { BombType.Molotov, new Color(1f, 0.5f, 0f) } // orange
        };

        foreach (BombType type in bombTypes)
        {
            // Create prefab name
            string prefabName = $"Bomb_{type}";
            string prefabPath = $"{bombFolder}/{prefabName}.prefab";

            // Create a new GameObject
            GameObject go = new GameObject(prefabName);
            go.layer = LayerMask.NameToLayer("Default");

            // Add components
            Transform tr = go.transform;
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.useGravity = true;

            Bomb bomb = go.AddComponent<Bomb>();
            bomb.bombType = type;
            bomb.explosionRadius = 3f;
            bomb.explosionDelay = 0.5f;
            bomb.targetLayers = Physics.DefaultRaycastLayers; // everything
            bomb.explosionForce = 500f;
            // effect prefabs left null for now

            // Add a sphere mesh for visibility in editor
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mf.mesh = Resources.GetBuiltinResource(typeof(Mesh), "Sphere") as Mesh;
            mr.material = new Material(Shader.Find("Standard"));
            mr.material.color = colors[type];

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);

            Debug.Log($"Created prefab: {prefabPath}");
        }

        AssetDatabase.Refresh();
    }
}