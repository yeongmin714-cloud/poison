using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.Core.Utils;
using UnityEngine;

namespace ProjectName.UI
{
    [System.Serializable]
    public struct NPCInstance
    {
        public string NpcId;
        public string NpcName;
        public NPCData.NPCAgeType AgeType;
        public string Greeting;
        public string QuestOfferLine;
        public List<string> QuestIds;
        public List<string> Dialogues;
        public string TerritoryId;
        public int NpcIndex;

        public bool HasQuests => QuestIds != null && QuestIds.Count > 0;
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

        // 캐싱: 부모 오브젝트를 매번 Find하지 않음
        private static GameObject _cachedParent;

        public static List<NPCInstance> GenerateNPCs(string territoryId, int tier)
        {
            int npcCount = tier switch { 1 => 2, 2 => 3, 3 => 4, 4 => 4, 5 => 5, _ => 2 };

            var npcs = new List<NPCInstance>(npcCount);

            for (int i = 0; i < npcCount; i++)
            {
                string npcId = $"{territoryId}_npc_{i:D2}";
                NPCData.NPCAgeType ageType = NPCData.PickAgeType(territoryId, i, tier);
                string npcName = NPCData.PickName(territoryId, i, ageType);
                string greeting = NPCData.PickGreeting(territoryId, i, ageType);
                string questOffer = NPCData.PickQuestOffer(territoryId, i, ageType);

                var questIds = TerritoryQuestDefinitions.PickQuestIdsForNPC(territoryId, i, tier);

                var npc = new NPCInstance
                {
                    NpcId = npcId,
                    NpcName = npcName,
                    AgeType = ageType,
                    Greeting = greeting,
                    QuestOfferLine = questOffer,
                    QuestIds = questIds,
                    Dialogues = new List<string>
                    {
                        greeting,
                        questOffer,
                        "(NPC가 조용히 생각에 잠겼다.)"
                    },
                    TerritoryId = territoryId,
                    NpcIndex = i
                };

                npcs.Add(npc);
            }

            return npcs;
        }

        public static GameObject SpawnNPC(NPCInstance npc, Vector3 position)
        {
            if (_cachedParent == null)
            {
                _cachedParent = GameObject.Find(ParentObjectName);
                if (_cachedParent == null)
                {
                    _cachedParent = new GameObject(ParentObjectName);
                    Object.DontDestroyOnLoad(_cachedParent);
                }
            }

            GameObject npcGO = new GameObject(npc.NpcName);
            npcGO.transform.SetParent(_cachedParent.transform);
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
            Color bodyColor = GetColorForNPC(npc.NpcId);
            Color skinColor = new Color(1.0f, 0.8f, 0.6f); // 기본 살색
            const float scale = 1.0f;

            // 몸통 (Cube)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(npcGO.transform, false);
            body.transform.localPosition = new Vector3(0, 0.5f * scale, 0);
            body.transform.localScale = new Vector3(0.6f * scale, 0.6f * scale, 0.4f * scale);
            body.GetComponent<MeshRenderer>().material = MaterialHelper.CreateLitMaterial(bodyColor, npc.NpcName + "_Body");

            // 머리 (Sphere)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(npcGO.transform, false);
            head.transform.localPosition = new Vector3(0, 1.05f * scale, 0);
            head.transform.localScale = new Vector3(0.35f * scale, 0.35f * scale, 0.35f * scale);
            head.GetComponent<MeshRenderer>().material = MaterialHelper.CreateLitMaterial(skinColor, npc.NpcName + "_Head");

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
            string npcType = npcTypes[npc.NpcIndex % npcTypes.Length];
            switch (npcType)
            {
                case "Man1": return "npc_man1_rigged";
                case "Man2": return "npc_man2_rigged";
                case "Girl1": return "npc_girl1_rigged";
                case "Girl2": return "npc_girl2_rigged";
                case "Girl3": return "npc_girl3_rigged";
                case "Oldman1": return "npc_oldman1_rigged";
                case "Oldman2": return "npc_oldman2_rigged";
                default: return "npc_man1_rigged";
            }
        }

        private static Color GetColorForNPC(string npcId)
        {
            int seed = StableHash(npcId) ^ 0x3C5EED1F;
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
            int seed = StableHash(seedKey) ^ unchecked((int)0x5EED_1234);
            var rng = new System.Random(seed);
            float angle = (float)(rng.NextDouble() * 360.0);
            float distance = (float)(3.0 + rng.NextDouble() * 5.0);

            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * distance;

            return territoryCenter + offset;
        }

        /// <summary>
        /// 애플리케이션 재시작 간에도 동일한 결과를 보장하는 안정적인 해시 함수.
        /// .NET string.GetHashCode()는 문서화되지 않은 알고리즘을 사용하므로 대체.
        /// </summary>
        private static int StableHash(string str)
        {
            if (str == null) return 0;
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < str.Length; i++)
                {
                    hash = hash * 31 + str[i];
                }
                return hash;
            }
        }
    }

    public class TerritoryNPCBehaviour : MonoBehaviour
    {
        [SerializeField] private NPCInstance _npcData;

        public NPCInstance NPCData => _npcData;

        public void Initialize(NPCInstance data)
        {
            _npcData = data;
            gameObject.name = data.NpcName;
        }

        public void Interact()
        {
            Debug.Log("[TerritoryNPC] " + _npcData.NpcName + ": " + _npcData.Greeting);
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