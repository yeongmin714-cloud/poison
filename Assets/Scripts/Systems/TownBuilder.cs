using System;
using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지 구성 요소 타입
    /// [5.1] TownBuilder — 영지 건물/오브젝트 배치
    /// </summary>
    public enum TownComponent
    {
        Plaza,       // 광장 (중앙)
        Shop,        // 상점
        CraftHouse,  // 크래프트 하우스
        Church,      // 교회
        NPCHouse     // NPC 주택
    }

    /// <summary>
    /// [5.1] 영지별 건물 구성/배치 좌표를 저장하는 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "TownLayout_New", menuName = "Territory/Town Layout Data")]
    public class TownLayoutData : ScriptableObject
    {
        [Header("영지 식별")]
        public string territoryId = "East_01";
        public string displayName = "리카드 영지";

        [Header("배치 중심")]
        public Vector3 centerPosition = Vector3.zero;
        public Vector3 plazaSize = new Vector3(8f, 0.2f, 8f);

        [Header("건물 목록")]
        public List<TownBuildingEntry> buildings = new List<TownBuildingEntry>();

        [Header("입구 병사")]
        public Vector3 entrancePosition = new Vector3(0f, 0f, 12f);
        public int guardCount = 3;
        public string guardNamePrefix = "리카드 병사";
        public int guardMinLevel = 1;
        public int guardMaxLevel = 3;

        /// <summary>
        /// 기본 영지 레이아웃을 자동 생성합니다.
        /// </summary>
        public void GenerateDefaultLayout()
        {
            buildings.Clear();

            // Plaza는 배치 중심 (광장 바닥)
            // 주변 건물 링 배치
            buildings.Add(new TownBuildingEntry
            {
                componentType = TownComponent.Shop,
                label = "상점",
                position = new Vector3(-6f, 0f, 0f),
                scale = new Vector3(3f, 2.5f, 3f),
                color = new Color(0.8f, 0.6f, 0.2f) // 노란빛
            });

            buildings.Add(new TownBuildingEntry
            {
                componentType = TownComponent.CraftHouse,
                label = "크래프트 하우스",
                position = new Vector3(6f, 0f, 0f),
                scale = new Vector3(3f, 2.5f, 3f),
                color = new Color(0.6f, 0.8f, 0.2f) // 연두색
            });

            buildings.Add(new TownBuildingEntry
            {
                componentType = TownComponent.Church,
                label = "교회",
                position = new Vector3(0f, 0f, -6f),
                scale = new Vector3(3f, 2.5f, 3f),
                color = new Color(0.2f, 0.6f, 0.8f) // 파란색
            });

            // NPC 주택 4채
            buildings.Add(new TownBuildingEntry
            {
                componentType = TownComponent.NPCHouse,
                label = "NPC 주택 1",
                position = new Vector3(-6f, 0f, -6f),
                scale = new Vector3(2.5f, 2f, 2.5f),
                color = new Color(0.6f, 0.3f, 0.1f)
            });

            buildings.Add(new TownBuildingEntry
            {
                componentType = TownComponent.NPCHouse,
                label = "NPC 주택 2",
                position = new Vector3(6f, 0f, -6f),
                scale = new Vector3(2.5f, 2f, 2.5f),
                color = new Color(0.6f, 0.3f, 0.1f)
            });

            buildings.Add(new TownBuildingEntry
            {
                componentType = TownComponent.NPCHouse,
                label = "NPC 주택 3",
                position = new Vector3(-6f, 0f, 6f),
                scale = new Vector3(2.5f, 2f, 2.5f),
                color = new Color(0.6f, 0.3f, 0.1f)
            });
        }
    }

    /// <summary>
    /// 단일 건물 배치 정보
    /// </summary>
    [Serializable]
    public struct TownBuildingEntry
    {
        public TownComponent componentType;
        public string label;
        public Vector3 position;
        public Vector3 scale;
        public Color color;
    }

    /// <summary>
    /// [5.1] TownBuilder — Procedural 영지 건물/병사 배치 시스템
    /// 
    /// TownLayoutData ScriptableObject를 기반으로 씬에 건물과 병사를 생성합니다.
    /// - 광장 중앙 (Plaza) → 주변 건물 링 배치
    /// - 입구에 병사 3명 배치 (GuardPlaceholder 생성)
    /// - 건물: Cube+Quad placeholder (추후 GLB 교체)
    /// </summary>
    public static class TownBuilder
    {
        /// <summary>
        /// TownLayoutData 기반으로 영지 건물과 병사를 생성합니다.
        /// 이미 같은 이름의 오브젝트가 존재하면 건너뜁니다 (중복 방지).
        /// </summary>
        /// <param name="layout">영지 레이아웃 데이터</param>
        public static void BuildTown(TownLayoutData layout)
        {
            if (layout == null)
            {
                Debug.LogError("[TownBuilder] ❌ TownLayoutData가 null입니다!");
                return;
            }

            string rootName = $"Town_{layout.territoryId}";

            // 중복 방지: 이미 생성된 영지가 있는지 확인
            var existingRoot = GameObject.Find(rootName);
            if (existingRoot != null)
            {
                Debug.Log($"[TownBuilder] ⚠️ '{rootName}' 이미 존재합니다. BuildTown 건너뜀.");
                return;
            }

            // 루트 GameObject 생성
            var root = new GameObject(rootName);
            root.transform.position = layout.centerPosition;

            Debug.Log($"[TownBuilder] 🏗️ '{layout.displayName}' ({layout.territoryId}) 건설 시작");

            // 1. 광장 (Plaza) 바닥 생성
            CreatePlaza(layout, root.transform);

            // 2. 건물 링 배치
            foreach (var entry in layout.buildings)
            {
                CreateBuilding(entry, root.transform);
            }

            // 3. 입구 병사 배치
            CreateEntranceGuards(layout, root.transform);

            Debug.Log($"[TownBuilder] ✅ '{layout.displayName}' 건설 완료! " +
                      $"건물 {layout.buildings.Count}개, 병사 {layout.guardCount}명");
        }

        /// <summary>
        /// territoryId 문자열로 BuildTown을 호출합니다.
        /// 기본 레이아웃을 생성한 후 배치합니다.
        /// </summary>
        public static void BuildTown(string territoryId)
        {
            var layout = ScriptableObject.CreateInstance<TownLayoutData>();
            layout.territoryId = territoryId;
            layout.displayName = territoryId;
            layout.GenerateDefaultLayout();
            BuildTown(layout);
        }

        /// <summary>
        /// 영지 내부의 모든 TownBuilder 생성 오브젝트를 제거합니다.
        /// </summary>
        public static void ClearAll()
        {
            // Unity 6000: FindObjectsByType 사용 (FindObjectsOfType<GameObject>()보다 효율적)
#if UNITY_EDITOR
            var roots = GameObject.FindObjectsByType<GameObject>();
#else
            var roots = GameObject.FindObjectsByType<GameObject>();
#endif
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name.StartsWith("Town_"))
                {
                    // 에디터 모드에서는 DestroyImmediate로 즉시 제거 (씬 저장 반영)
                    // 런타임에서는 안전하게 Destroy 사용
                    if (Application.isPlaying)
                        GameObject.Destroy(roots[i]);
                    else
                        GameObject.DestroyImmediate(roots[i]);

                    Debug.Log($"[TownBuilder] 🗑️ '{roots[i].name}' 제거됨");
                }
            }
        }

        // ================================================================
        // Private Helpers
        // ================================================================

        /// <summary>
        /// 광장 (Plaza) 바닥 — 평평한 Quad
        /// </summary>
        private static void CreatePlaza(TownLayoutData layout, Transform parent)
        {
            var plazaGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plazaGo.name = "Plaza_Floor";
            plazaGo.transform.SetParent(parent);
            plazaGo.transform.localPosition = Vector3.zero;
            plazaGo.transform.localScale = layout.plazaSize;
            // Quad를 바닥으로 회전 (기본 Quad는 XY 평면 → XZ 평면으로)
            plazaGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var renderer = plazaGo.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = MaterialHelper.CreateLitMaterial(
                    new Color(0.7f, 0.6f, 0.4f), "Plaza_Mat");
            }

            // Plaza 태그
            plazaGo.tag = "Untagged";

            // Plaza 라벨
            var labelGo = new GameObject("Plaza_Label");
            labelGo.transform.SetParent(plazaGo.transform);
            labelGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var textMesh = labelGo.AddComponent<TextMesh>();
            textMesh.text = "⛲ 광장";
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.characterSize = 0.1f;
            textMesh.color = Color.white;
            textMesh.fontSize = 28;

            Debug.Log($"[TownBuilder] ⛲ 광장 바닥 생성 완료 (크기: {layout.plazaSize})");
        }

        /// <summary>
        /// 단일 건물 Placeholder 생성
        /// Cube 본체 + Quad 지붕 + TextMesh 라벨
        /// </summary>
        private static void CreateBuilding(TownBuildingEntry entry, Transform parent)
        {
            // Try real building model
            string buildingKey = GetBuildingModelKey(entry.componentType);
            if (RuntimeModelLoader.TryGetModel(buildingKey, out var buildingModel))
            {
                var modelInstance = UnityEngine.Object.Instantiate(buildingModel, parent);
                modelInstance.transform.localPosition = entry.position;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.name = entry.label + "_Model";
                // Continue with label creation (skip primitive creation)
                // BuildingPlaceholder 컴포넌트 부착 (기존 시스템 연동)
                var bhPlaceholder = modelInstance.AddComponent<BuildingPlaceholder>();
                bhPlaceholder.buildingName = entry.label;
                bhPlaceholder.buildingType = MapToBuildingType(entry.componentType);
                // TextMesh 라벨
                var labelGo2 = new GameObject($"{entry.label}_Label");
                labelGo2.transform.SetParent(modelInstance.transform);
                labelGo2.transform.localPosition = new Vector3(0f, entry.scale.y * 0.5f + 0.6f, 0f);
                var textMesh2 = labelGo2.AddComponent<TextMesh>();
                textMesh2.text = GetLabelText(entry.componentType, entry.label);
                textMesh2.anchor = TextAnchor.MiddleCenter;
                textMesh2.characterSize = 0.08f;
                textMesh2.color = Color.white;
                textMesh2.fontSize = 22;
                Debug.Log($"[TownBuilder] 🏠 '{entry.label}' ({entry.componentType}) GLB 모델 생성 @ {entry.position}");
                return;
            }

            // 본체 (Cube)
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = entry.label;
            go.transform.SetParent(parent);
            go.transform.localPosition = entry.position;
            go.transform.localScale = entry.scale;
            go.tag = "Untagged";

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = MaterialHelper.CreateLitMaterial(
                    entry.color, $"{entry.label}_Mat");
            }

            // 콜라이더 유지 (물리적 블로킹)
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;

            // BuildingPlaceholder 컴포넌트 부착 (기존 시스템 연동)
            var placeholder = go.AddComponent<BuildingPlaceholder>();
            placeholder.buildingName = entry.label;
            placeholder.buildingType = MapToBuildingType(entry.componentType);

            // Quad 지붕
            var roofGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            roofGo.name = $"{entry.label}_Roof";
            roofGo.transform.SetParent(go.transform);
            roofGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            roofGo.transform.localScale = new Vector3(1.1f, 1f, 1.1f);
            roofGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var roofRenderer = roofGo.GetComponent<MeshRenderer>();
            if (roofRenderer != null)
            {
                // 지붕은 약간 어둡게
                Color roofColor = entry.color * 0.7f;
                roofColor.a = 1f;
                roofRenderer.material = MaterialHelper.CreateLitMaterial(
                    roofColor, $"{entry.label}_Roof_Mat");
            }

            // TextMesh 라벨
            var labelGo = new GameObject($"{entry.label}_Label");
            labelGo.transform.SetParent(go.transform);
            labelGo.transform.localPosition = new Vector3(0f, entry.scale.y * 0.5f + 0.6f, 0f);
            var textMesh = labelGo.AddComponent<TextMesh>();
            textMesh.text = GetLabelText(entry.componentType, entry.label);
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.characterSize = 0.08f;
            textMesh.color = Color.white;
            textMesh.fontSize = 22;

            Debug.Log($"[TownBuilder] 🏠 '{entry.label}' ({entry.componentType}) 생성 @ {entry.position}");
        }

        /// <summary>
        /// 입구 병사 3명 배치 (GuardPlaceholder 생성)
        /// </summary>
        private static void CreateEntranceGuards(TownLayoutData layout, Transform parent)
        {
            int count = Mathf.Clamp(layout.guardCount, 1, 5);
            Vector3 basePos = layout.entrancePosition;

            for (int i = 0; i < count; i++)
            {
                // 병사를 일렬로 배치
                float spacing = 2.5f;
                float half = (count - 1) * spacing * 0.5f;
                Vector3 offset = new Vector3((i * spacing) - half, 0f, 0f);
                Vector3 spawnPos = basePos + offset;

                string guardName = $"{layout.guardNamePrefix} {i + 1}";

                // GLB "soldier" 모델 우선, 없으면 Capsule
                GameObject guardGo;
                if (RuntimeModelLoader.TryGetModel("soldier", out var soldierPrefab))
                {
                    guardGo = UnityEngine.Object.Instantiate(soldierPrefab, parent);
                    guardGo.name = $"Guard_{layout.territoryId}_{i + 1}";
                    guardGo.transform.position = spawnPos;
                }
                else
                {
                    guardGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    guardGo.name = $"Guard_{layout.territoryId}_{i + 1}";
                    guardGo.transform.position = spawnPos;
                    guardGo.transform.localScale = new Vector3(1.5f, 2f, 1.5f);

                    var guardRenderer = guardGo.GetComponent<MeshRenderer>();
                    if (guardRenderer != null)
                    {
                        guardRenderer.material = MaterialHelper.CreateLitMaterial(
                            new Color(0.2f, 0.4f, 0.8f), $"Guard_{i + 1}_Mat");
                    }
                }

                guardGo.transform.SetParent(parent);
                guardGo.tag = "Untagged";

                // GuardPlaceholder 부착
                var placeholder = guardGo.AddComponent<GuardPlaceholder>();
                int level = UnityEngine.Random.Range(layout.guardMinLevel, layout.guardMaxLevel + 1);
                placeholder.SetGuardInfo(guardName, level, NationType.East);
                placeholder.JobTitle = "병사";

                // 라벨
                var labelGo = new GameObject($"GuardLabel_{i + 1}");
                labelGo.transform.SetParent(guardGo.transform);
                labelGo.transform.localPosition = new Vector3(0f, 2f, 0f);
                var textMesh = labelGo.AddComponent<TextMesh>();
                textMesh.text = $"{guardName} Lv.{level}";
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.characterSize = 0.07f;
                textMesh.color = Color.white;
                textMesh.fontSize = 20;

                Debug.Log($"[TownBuilder] 🗡️ '{guardName}' (Lv.{level}) 배치 @ {spawnPos}");
            }
        }

        /// <summary>
        /// TownComponent → GLB 모델 키 매핑
        /// </summary>
        private static string GetBuildingModelKey(TownComponent component)
        {
            switch (component)
            {
                case TownComponent.Shop:       return "shop";
                case TownComponent.CraftHouse: return "craft_equip";
                case TownComponent.Church:     return "church";
                case TownComponent.NPCHouse:   return "hut";
                default:                       return "hut";
            }
        }

        /// <summary>
        /// TownComponent → BuildingPlaceholder.BuildingType 매핑
        /// </summary>
        private static BuildingPlaceholder.BuildingType MapToBuildingType(TownComponent component)
        {
            switch (component)
            {
                case TownComponent.Plaza:
                    return BuildingPlaceholder.BuildingType.Other;
                case TownComponent.Shop:
                    return BuildingPlaceholder.BuildingType.Shop;
                case TownComponent.CraftHouse:
                    return BuildingPlaceholder.BuildingType.CraftHouse;
                case TownComponent.Church:
                    return BuildingPlaceholder.BuildingType.Church;
                case TownComponent.NPCHouse:
                    return BuildingPlaceholder.BuildingType.NPCHouse;
                default:
                    return BuildingPlaceholder.BuildingType.Other;
            }
        }

        /// <summary>
        /// 건물 타입에 따른 표시 라벨 텍스트
        /// </summary>
        private static string GetLabelText(TownComponent component, string fallback)
        {
            switch (component)
            {
                case TownComponent.Plaza:      return "⛲ 광장";
                case TownComponent.Shop:       return "🏪 상점";
                case TownComponent.CraftHouse: return "🔨 크래프트";
                case TownComponent.Church:     return "⛪ 교회";
                case TownComponent.NPCHouse:   return "🏠 " + fallback;
                default:                       return fallback;
            }
        }
    }
}