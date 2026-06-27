using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-10: 영주 항복 시스템 — 영지의 모든 병사가 패배하면 영주가 등장하여 항복합니다.
    /// 
    /// 각 영지별로 LordData를 추적하며, TrySummonLord가 호출되면
    /// 영주 Placeholder를 성 위치에 스폰하고 항복 대화를 표시합니다.
    /// 여러 영지의 상태를 독립적으로 관리합니다.
    /// </summary>
    public static class LordSurrenderSystem
    {
        /// <summary>
        /// 영주 데이터 구조체
        /// </summary>
        public struct LordData
        {
            /// <summary>영주 고유 ID</summary>
            public string lordId;
            /// <summary>소속 영지 ID</summary>
            public TerritoryId territoryId;
            /// <summary>영주 체력</summary>
            public float health;
            /// <summary>최대 체력</summary>
            public float maxHealth;
            /// <summary>생존 여부</summary>
            public bool isAlive;
            /// <summary>항복 여부</summary>
            public bool hasSurrendered;
            /// <summary>영주 이름</summary>
            public string lordName;
            /// <summary>영주 성격</summary>
            public LordPersonality personality;
            /// <summary>선호 음식</summary>
            public string preferredFood;
            /// <summary>지병</summary>
            public string chronicDisease;
            /// <summary>소속 국가</summary>
            public NationType nation;
        }

        /// <summary>영주가 항복했을 때 발생 (territoryId, lordData)</summary>
        public static event System.Action<TerritoryId, LordData> OnLordSurrendered;

        /// <summary>영주가 처형되었을 때 발생 (territoryId, lordData)</summary>
        public static event System.Action<TerritoryId, LordData> OnLordExecuted;

        /// <summary>영주가 살려졌을 때 발생 (territoryId, lordData)</summary>
        public static event System.Action<TerritoryId, LordData> OnLordSpared;

        /// <summary>영주가 소환되었을 때 발생 (territoryId, lordData)</summary>
        public static event System.Action<TerritoryId, LordData> OnLordSummoned;

        // ===== 내부 상태 =====

        private static readonly Dictionary<TerritoryId, LordData> _lords = new Dictionary<TerritoryId, LordData>();
        private static readonly Dictionary<TerritoryId, GameObject> _lordObjects = new Dictionary<TerritoryId, GameObject>();

        /// <summary>
        /// 특정 영지의 LordData 반환
        /// </summary>
        public static LordData GetLordData(TerritoryId territoryId)
        {
            if (_lords.TryGetValue(territoryId, out var data))
                return data;
            return default;
        }

        /// <summary>
        /// 영지의 모든 경비병이 패배했는지 확인하고,
        /// 조건이 충족되면 영주를 소환하여 항복을 받습니다.
        /// GateGuardPlaceholder.AllGuardsDefeated 또는 모든 GuardPlaceholder가 사망했을 때 호출합니다.
        /// </summary>
        /// <param name="territoryId">대상 영지 ID</param>
        /// <returns>영주가 성공적으로 소환/항복했으면 true</returns>
        public static bool TrySummonLord(TerritoryId territoryId)
        {
            // 이미 처리된 영주 — 사망했거나 이미 항복함
            if (_lords.TryGetValue(territoryId, out var existing))
            {
                if (!existing.isAlive || existing.hasSurrendered)
                    return false;
            }

            // 영지 데이터 확인
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(territoryId);
            if (def.territoryName == null)
            {
                Debug.LogWarning($"[LordSurrenderSystem] 영지 정의 없음: {territoryId}");
                return false;
            }

            var state = db.GetState(territoryId);
            if (state == null)
            {
                Debug.LogWarning($"[LordSurrenderSystem] 영지 상태 없음: {territoryId}");
                return false;
            }

            // 이미 항복 또는 처형된 경우
            if (state.lordSurrendered || state.lordDefeated || state.lordExecuted)
                return false;

            // 영주 데이터 생성
            LordData lord = new LordData
            {
                lordId = $"lord_{territoryId}",
                territoryId = territoryId,
                health = 100f,
                maxHealth = 100f,
                isAlive = true,
                hasSurrendered = false,
                lordName = def.lord.lordName,
                personality = def.lord.personality,
                preferredFood = def.lord.preferredFood,
                chronicDisease = def.lord.chronicDisease,
                nation = def.nation
            };

            _lords[territoryId] = lord;

            // 영주 Placeholder 스폰 (성 위치)
            GameObject lordGo = SpawnLordPlaceholder(territoryId, lord);
            if (lordGo != null)
            {
                _lordObjects[territoryId] = lordGo;
            }

            // 성격 기반 항복 텍스트
            string surrenderText = GetSurrenderText(lord);

            // 항복 처리
            lord.hasSurrendered = true;
            _lords[territoryId] = lord;
            state.lordSurrendered = true;
            state.lordDefeated = true;

            Debug.Log($"[LordSurrenderSystem] 🏳️ 영주 항복! 영지:{territoryId} {lord.lordName}: \"{surrenderText}\"");

            OnLordSummoned?.Invoke(territoryId, lord);
            OnLordSurrendered?.Invoke(territoryId, lord);

            return true;
        }

        /// <summary>
        /// 영주를 성 위치에 스폰합니다. (Placeholder 큐브)
        /// </summary>
        private static GameObject SpawnLordPlaceholder(TerritoryId territoryId, LordData lord)
        {
            // 성 위치 찾기: TerritoryManager의 건물 중 Castle 타입, 또는 랜덤 위치
            Vector3 castlePosition = FindCastlePosition(territoryId);

            GameObject lordGo = new GameObject($"[Lord] {lord.lordName}");
            lordGo.transform.position = castlePosition;

            // 간단한 시각적 표시를 위한 큐브
            var renderer = lordGo.AddComponent<MeshRenderer>();
            var filter = lordGo.AddComponent<MeshFilter>();
            var cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (cubeMesh == null)
            {
                Debug.LogWarning("[LordSurrenderSystem] Built-in Cube.fbx를 찾을 수 없습니다. 큐브 없이 Placeholder 생성합니다.");
            }
            filter.sharedMesh = cubeMesh;
            renderer.material.color = GetLordColor(lord.personality);
            lordGo.transform.localScale = new Vector3(1.5f, 2f, 1.5f);

            // 왕관 표시
            var crownGo = new GameObject("Crown");
            crownGo.transform.SetParent(lordGo.transform);
            crownGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var crownRenderer = crownGo.AddComponent<MeshRenderer>();
            var crownFilter = crownGo.AddComponent<MeshFilter>();
            var crownMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (crownMesh == null)
            {
                Debug.LogWarning("[LordSurrenderSystem] Crown용 Built-in Cube.fbx를 찾을 수 없습니다.");
            }
            crownFilter.sharedMesh = crownMesh;
            crownRenderer.material.color = Color.yellow;
            crownGo.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);

            Debug.Log($"[LordSurrenderSystem] 영주 Placeholder 생성: {lord.lordName} at {castlePosition}");

            return lordGo;
        }

        /// <summary>
        /// 성 위치 찾기 — 이름에 "Castle"이 포함된 건물, 없으면 Other 타입 중 가장 큰 건물
        /// </summary>
        private static Vector3 FindCastlePosition(TerritoryId territoryId)
        {
            // 우선 TerritoryManager의 캐시된 건물 목록에서 Castle 검색 (현재 영지)
            if (TerritoryManager.Instance != null)
            {
                foreach (var name in TerritoryManager.Instance.BuildingNames)
                {
                    var b = TerritoryManager.Instance.GetBuilding(name);
                    if (b != null && b.buildingName != null &&
                        b.buildingName.ToLowerInvariant().Contains("castle"))
                    {
                        return b.transform.position + Vector3.up * 0.5f;
                    }
                }
            }

            // TerritoryManager에 없으면 전역 검색 (fallback — 성능 주의)
            var buildings = UnityEngine.Object.FindObjectsOfType<BuildingPlaceholder>();
            foreach (var b in buildings)
            {
                if (b != null && b.buildingName != null &&
                    b.buildingName.ToLowerInvariant().Contains("castle"))
                {
                    return b.transform.position + Vector3.up * 0.5f;
                }
            }

            // TerritoryManager 영지 중심 사용
            if (TerritoryManager.Instance != null)
                return TerritoryManager.Instance.GetTerritoryCenter() + Vector3.forward * 3f;

            // 기본 위치
            return new Vector3(0f, 0.5f, 5f);
        }

        /// <summary>
        /// 영주 성격에 따른 항복 대화 텍스트 반환
        /// </summary>
        public static string GetSurrenderText(LordData lord)
        {
            switch (lord.personality)
            {
                case LordPersonality.Cowardly:
                    return "제발 목숨만 살려주시오! 항복하겠소!";
                case LordPersonality.Brave:
                    return "...네놈의 실력을 인정하겠다. 항복한다.";
                case LordPersonality.Greedy:
                    return "영지는 가지시오. 대신 내 목숨만은 살려주시오!";
                case LordPersonality.Suspicious:
                    return "뭐... 벌써 병사들이 전멸하다니... 항복하겠소.";
                case LordPersonality.Wise:
                    return "이미 승부는 났소. 항복하겠소. 영지를 내주리다.";
                case LordPersonality.Cruel:
                    return "크흑... 네놈... 다음에 만날 때는 죽여주마! ...항복이다.";
                case LordPersonality.Neutral:
                default:
                    return "모든 병사가 쓰러졌다... 항복하겠소. 영지는 네 것이오.";
            }
        }

        /// <summary>
        /// 영주 처형 — 영지를 플레이어 소유로 변경하고 영주 사망 처리
        /// </summary>
        public static void ExecuteLord(TerritoryId territoryId)
        {
            if (!_lords.TryGetValue(territoryId, out var lord) || !lord.isAlive)
                return;

            var db = TerritoryDatabase.Instance;
            var state = db.GetState(territoryId);
            if (state == null) return;

            // 영주 사망
            lord.isAlive = false;
            lord.health = 0f;
            _lords[territoryId] = lord;

            // 영지 상태 업데이트
            state.ownership = TerritoryOwnership.PlayerOwned;
            state.lordExecuted = true;
            state.lordSurrendered = true;

            // Lord Placeholder 제거
            DestroyLordObject(territoryId);

            Debug.Log($"[LordSurrenderSystem] ⚔️ 영주 처형: {lord.lordName} — 영지 {territoryId} 플레이어 소유");

            OnLordExecuted?.Invoke(territoryId, lord);
        }

        /// <summary>
        /// 영주 살려주기 — 영지를 플레이어 소유로 변경하고 충성도 보너스
        /// </summary>
        public static void SpareLord(TerritoryId territoryId)
        {
            if (!_lords.TryGetValue(territoryId, out var lord) || !lord.isAlive)
                return;

            var db = TerritoryDatabase.Instance;
            var state = db.GetState(territoryId);
            if (state == null) return;

            // 영주 생존
            lord.isAlive = true;
            lord.hasSurrendered = true;
            _lords[territoryId] = lord;

            // 영지 상태 업데이트 — 높은 충성도 보너스
            state.ownership = TerritoryOwnership.PlayerOwned;
            state.lordSpared = true;
            state.lordSurrendered = true;
            state.loyaltyToPlayer = Mathf.Clamp(state.loyaltyToPlayer + 30f, 0f, 100f);

            // Lord Placeholder 제거 (또는 유지 — 영주가 영지에 남음)
            DestroyLordObject(territoryId);

            Debug.Log($"[LordSurrenderSystem] 🤝 영주 살려줌: {lord.lordName} — 영지 {territoryId} 플레이어 소유 (충성도 +30)");

            OnLordSpared?.Invoke(territoryId, lord);
        }

        /// <summary>
        /// 영주 Placeholder GameObject 제거
        /// </summary>
        private static void DestroyLordObject(TerritoryId territoryId)
        {
            if (_lordObjects.TryGetValue(territoryId, out var go) && go != null)
            {
                UnityEngine.Object.Destroy(go);
                _lordObjects.Remove(territoryId);
            }
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용)
        /// </summary>
        public static void ResetAll()
        {
            foreach (var kvp in _lordObjects)
            {
                if (kvp.Value != null)
                    UnityEngine.Object.DestroyImmediate(kvp.Value);
            }

            _lords.Clear();
            _lordObjects.Clear();
        }

        /// <summary>
        /// 영주 성격에 따른 색상 반환
        /// </summary>
        private static Color GetLordColor(LordPersonality personality)
        {
            switch (personality)
            {
                case LordPersonality.Cowardly: return Color.gray;
                case LordPersonality.Brave: return Color.red;
                case LordPersonality.Greedy: return new Color(0.8f, 0.6f, 0f);
                case LordPersonality.Suspicious: return new Color(0.5f, 0f, 0.5f);
                case LordPersonality.Wise: return Color.blue;
                case LordPersonality.Cruel: return new Color(0.3f, 0f, 0f);
                case LordPersonality.Neutral:
                default: return Color.white;
            }
        }

        /// <summary>
        /// 성격 한글 이름 반환
        /// </summary>
        public static string GetPersonalityName(LordPersonality personality)
        {
            switch (personality)
            {
                case LordPersonality.Neutral: return "보통";
                case LordPersonality.Greedy: return "탐욕스러움";
                case LordPersonality.Suspicious: return "의심 많음";
                case LordPersonality.Brave: return "용감함";
                case LordPersonality.Cowardly: return "겁많음";
                case LordPersonality.Wise: return "현명함";
                case LordPersonality.Cruel: return "잔인함";
                default: return "알 수 없음";
            }
        }
    }
}