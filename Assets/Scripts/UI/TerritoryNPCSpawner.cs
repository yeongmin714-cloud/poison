using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.UI;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    public struct NPCInstance
    {
        public string npcId;
        public string npcName;
        public NPCData.NPCAgeType ageType;
        public string greeting;
        public string questOfferLine;
        public List<string> questIds;
        public List<string> dialogues;
        public string territoryId;
        public int npcIndex;

        public bool HasQuests => questIds != null && questIds.Count > 0;
    }

    public static class TerritoryNPCSpawner
    {
        public const string ParentObjectName = "___TerritoryNPCs___";

        // 12가지 인간 옷감 색상 팔레트
        private static readonly Color[] _npcPalette = new Color[]
        {
            new Color(0.20f, 0.40f, 0.80f), // 파랑
            new Color(0.80f, 0.25f, 0.25f), // 빨강
            new Color(0.15f, 0.60f, 0.30f), // 초록
            new Color(0.90f, 0.70f, 0.10f), // 노랑
            new Color(0.60f, 0.30f, 0.70f), // 보라
            new Color(1.00f, 0.55f, 0.00f), // 주황
            new Color(0.20f, 0.60f, 0.70f), // 청록
            new Color(0.75f, 0.40f, 0.15f), // 갈색
            new Color(0.90f, 0.50f, 0.70f), // 분홍
            new Color(0.30f, 0.30f, 0.30f), // 회색
            new Color(0.50f, 0.70f, 0.90f), // 하늘
            new Color(0.70f, 0.50f, 0.30f), // 카키
        };

        public static List<NPCInstance> GenerateNPCs(string territoryId, int tier)
        {
            int npcCount = tier switch { 1 => 2, 2 => 3, 3 => 4, 4 => 4, 5 => 5, _ => 2 };

            var npcs = new List<NPCInstance>();

            for (int i = 0; i < npcCount; i++)
            {
                string npcId = $"{territoryId}_npc_{i:D2}";
                NPCData.NPCAgeType ageType = NPCData.PickAgeType(territoryId, i, tier);
                string npcName = NPCData.PickName(territoryId, i, ageType);

                var questIds = TerritoryQuestDefinitions.PickQuestIdsForNPC(territoryId, i, tier);

                var npc = new NPCInstance
                {
                    npcId = npcId,
                    npcName = npcName,
                    ageType = ageType,
                    greeting = NPCData.PickGreeting(territoryId, i, ageType),
                    questOfferLine = NPCData.PickQuestOffer(territoryId, i, ageType),
                    questIds = questIds,
                    dialogues = new List<string>
                    {
                        NPCData.PickGreeting(territoryId, i, ageType),
                        NPCData.PickQuestOffer(territoryId, i, ageType),
                        "(NPC가 조용히 생각에 잠겼다.)"
                    },
                    territoryId = territoryId,
                    npcIndex = i
                };

                npcs.Add(npc);
            }

            return npcs;
        }

        public static GameObject SpawnNPC(NPCInstance npc, Vector3 position)
        {
            GameObject parent = GameObject.Find(ParentObjectName);
            if (parent == null)
            {
                parent = new GameObject(ParentObjectName);
                Object.DontDestroyOnLoad(parent);
            }

            GameObject npcGO = new GameObject(npc.npcName);
            npcGO.transform.SetParent(parent.transform);
            npcGO.transform.position = position;

            var behaviour = npcGO.AddComponent<TerritoryNPCBehaviour>();
            behaviour.Initialize(npc);

            // Try to load real NPC GLB model
            string npcKey = GetNPCKey(npc);
            if (RuntimeModelLoader.TryGetModel(npcKey, out var npcModel))
            {
                var instance = Object.Instantiate(npcModel, npcGO.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                ModelAnimatorAssigner.AssignController(instance, npcKey);
                return npcGO;
            }

            // NPC ID 기반 시드로 고유 색상 선택 (같은 NPC = 항상 같은 색상)
            Color bodyColor = GetColorForNPC(npc.npcId);
            Color skinColor = new Color(1.0f, 0.8f, 0.6f); // 기본 살색
            const float scale = 1.0f;

            // 몸통 (Cube)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(npcGO.transform, false);
            body.transform.localPosition = new Vector3(0, 0.5f * scale, 0);
            body.transform.localScale = new Vector3(0.6f * scale, 0.6f * scale, 0.4f * scale);
            var bodyRenderer = body.GetComponent<MeshRenderer>();
            if (bodyRenderer != null)
                bodyRenderer.material = MaterialHelper.CreateLitMaterial(bodyColor, npc.npcName + "_Body");

            // 머리 (Sphere)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(npcGO.transform, false);
            head.transform.localPosition = new Vector3(0, 1.05f * scale, 0);
            head.transform.localScale = new Vector3(0.35f * scale, 0.35f * scale, 0.35f * scale);
            var headRenderer = head.GetComponent<MeshRenderer>();
            if (headRenderer != null)
                headRenderer.material = MaterialHelper.CreateLitMaterial(skinColor, npc.npcName + "_Head");

            return npcGO;
        }

        /// <summary>
        /// NPC 인스턴스에서 사용할 GLB 모델 키를 반환합니다.
        /// NPC 인덱스를 기반으로 다양한 NPC 외형을 순환합니다.
        /// </summary>
        private static string GetNPCKey(NPCInstance npc)
        {
            // Use npcIndex to cycle through NPC visual types
            string[] npcTypes = { "Man1", "Man2", "Girl1", "Girl2", "Girl3", "Oldman1", "Oldman2" };
            string npcType = npcTypes[npc.npcIndex % npcTypes.Length];
            switch (npcType)
            {
                case "Lord": return "npc_lord_rigged";
                case "King": return "npc_king_rigged";
                case "Shop": return "npc_shop_rigged";
                case "Man1": return "npc_man1_rigged";
                case "Man2": return "npc_man2_rigged";
                case "Girl1": return "npc_girl1_rigged";
                case "Girl2": return "npc_girl2_rigged";
                case "Girl3": return "npc_girl3_rigged";
                case "Oldman1": return "npc_oldman1_rigged";
                case "Oldman2": return "npc_oldman2_rigged";
                case "Dracula": return "npc_dracula_rigged";
                default: return "npc_man1_rigged";
            }
        }

        private static Color GetColorForNPC(string npcId)
        {
            int seed = npcId.GetHashCode() ^ 0x3C5EED1F;
            var rng = new System.Random(seed);
            int idx = rng.Next(0, _npcPalette.Length);

            // 약간의 밝기 변형
            float brightnessVariation = (float)(rng.NextDouble() * 0.2f - 0.1f);
            Color c = _npcPalette[idx];
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            v = Mathf.Clamp01(v + brightnessVariation);
            return Color.HSVToRGB(h, s, v);
        }

        public static Vector3 GetSpawnPosition(string territoryId, int npcIndex, Vector3 territoryCenter)
        {
            string seedKey = territoryId + "_npc" + npcIndex + "_spawn";
            int seed = seedKey.GetHashCode() ^ unchecked((int)0x5EED_1234);
            var rng = new System.Random(seed);
            float angle = (float)(rng.NextDouble() * 360.0);
            float distance = (float)(3.0 + rng.NextDouble() * 5.0);

            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * distance;

            return territoryCenter + offset;
        }
    }

    public class TerritoryNPCBehaviour : MonoBehaviour
    {
        [SerializeField] private NPCInstance _npcData;

        public NPCInstance NPCData => _npcData;

        public void Initialize(NPCInstance data)
        {
            _npcData = data;
            gameObject.name = data.npcName;
        }

        public void Interact()
        {
            Debug.Log("[TerritoryNPC] " + _npcData.npcName + ": " + _npcData.greeting);
            if (NPCDialogueWindow.Instance != null)
            {
                NPCDialogueWindow.Instance.ShowDialogue(_npcData);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
    }
}