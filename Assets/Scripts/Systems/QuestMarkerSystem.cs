using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 활성 퀘스트의 대상 영지 마커를 관리하는 싱글톤 시스템.
    /// 매 프레임 QuestManager에서 활성 퀘스트를 읽어 대상 영지의 월드 위치를 계산하고
    /// HUD(QuestMarkerHUD)에서 읽을 수 있는 마커 리스트를 제공합니다.
    /// </summary>
    public class QuestMarkerSystem : MonoBehaviour
    {
        // ===== 싱글톤 =====
        public static QuestMarkerSystem Instance { get; private set; }

        // ===== 상수: 국가별 방향 벡터 (MinimapUI와 동일한 로직) =====
        private static readonly Dictionary<NationType, Vector3> NationDirections = new Dictionary<NationType, Vector3>
        {
            { NationType.North, new Vector3(0f, 0f, 1f) },
            { NationType.East,  new Vector3(1f, 0f, 0f) },
            { NationType.South, new Vector3(0f, 0f, -1f) },
            { NationType.West,  new Vector3(-1f, 0f, 0f) },
            { NationType.Empire, Vector3.zero },
            { NationType.Dracula, Vector3.zero },
        };

        // ===== 런타임 상태 =====
        private Transform _playerTransform;
        private readonly List<QuestMarkerData> _activeMarkers = new List<QuestMarkerData>();

        // 캐싱: 매 프레임 new List 방지
        private readonly List<QuestData> _questBuffer = new List<QuestData>();

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 플레이어 Transform 찾기
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
            }
            else
            {
                Debug.LogWarning("[QuestMarkerSystem] Player를 찾을 수 없습니다! Transform 기본값 사용.");
            }
        }

        private void Update()
        {
            RecalculateMarkers();
        }

        /// <summary>
        /// 매 프레임 호출 — 활성 퀘스트를 읽어 마커 리스트를 갱신합니다.
        /// </summary>
        private void RecalculateMarkers()
        {
            _activeMarkers.Clear();

            // QuestManager에서 활성 퀘스트 목록 조회
            List<QuestData> activeQuests = QuestManager.GetActiveQuests();
            if (activeQuests == null || activeQuests.Count == 0)
                return;

            Vector3 playerPos = _playerTransform != null ? _playerTransform.position : Vector3.zero;

            for (int i = 0; i < activeQuests.Count; i++)
            {
                QuestData quest = activeQuests[i];

                // targetTerritoryId가 없는 퀘스트는 건너뜀
                if (string.IsNullOrEmpty(quest.targetTerritoryId))
                    continue;

                // 대상 영지의 월드 위치 계산
                Vector3 worldPos = GetTerritoryWorldPosition(quest.targetTerritoryId);

                // 거리 계산 (Y 무시)
                float distance = Vector3.Distance(
                    new Vector3(playerPos.x, 0f, playerPos.z),
                    new Vector3(worldPos.x, 0f, worldPos.z)
                );

                // 마커 색상 (questId 기반 결정론적)
                Color markerColor = GetMarkerColor(quest.questId);

                _activeMarkers.Add(new QuestMarkerData
                {
                    questName = quest.questName,
                    worldPos = worldPos,
                    markerColor = markerColor,
                    distanceFromPlayer = distance,
                });
            }
        }

        /// <summary>
        /// 대상 영지 ID 문자열을 파싱하여 월드 위치를 반환합니다.
        /// ID 형식: "NationType_Index" (예: "East_01", "North_12")
        /// MinimapUI.GetTerritoryWorldPosition과 동일한 로직을 사용합니다.
        /// </summary>
        private Vector3 GetTerritoryWorldPosition(string territoryIdStr)
        {
            if (string.IsNullOrEmpty(territoryIdStr))
                return Vector3.zero;

            // TerritoryId 파싱
            string[] parts = territoryIdStr.Split('_');
            if (parts.Length != 2)
                return Vector3.zero;

            if (!System.Enum.TryParse<NationType>(parts[0], out NationType nation))
                return Vector3.zero;

            if (!int.TryParse(parts[1], out int index))
                return Vector3.zero;

            // TerritoryDatabase에서 정의 조회
            TerritoryDatabase db = TerritoryDatabase.Instance;
            if (db == null)
                return Vector3.zero;

            TerritoryDefinition def = db.GetDefinition(nation, index);
            if (def.id.nation == NationType.None)
                return Vector3.zero;

            return GetTerritoryWorldPositionFromDef(def);
        }

        /// <summary>
        /// 영지 정의로부터 가상 월드 위치를 계산합니다.
        /// MinimapUI.GetTerritoryWorldPosition과 동일한 로직.
        /// </summary>
        private Vector3 GetTerritoryWorldPositionFromDef(TerritoryDefinition def)
        {
            // NationDirections 딕셔너리에 없는 국가는 안전 폴백
            if (!NationDirections.TryGetValue(def.nation, out Vector3 dir))
                return Vector3.zero;

            // Empire는 중앙
            if (def.nation == NationType.Empire)
                return Vector3.zero;

            // Ring 난이도에 따른 거리
            float distance = def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => 15f,
                TerritoryDifficulty.Ring2 => 30f,
                TerritoryDifficulty.Ring3 => 45f,
                TerritoryDifficulty.Ring4 => 60f,
                _ => 20f,
            };

            // 같은 Ring 내 영지끼리 약간 퍼뜨리기
            float spreadAngle = (def.id.index % 5) * 18f;
            Vector3 spreadDir = Quaternion.Euler(0f, spreadAngle, 0f) * dir;
            if (spreadDir.sqrMagnitude < 0.01f)
                spreadDir = dir;

            return spreadDir.normalized * distance;
        }

        /// <summary>
        /// 퀘스트 ID 기반 결정론적 마커 색상.
        /// </summary>
        private static Color GetMarkerColor(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return Color.yellow;

            int hash = 0;
            foreach (char c in questId)
                hash = hash * 31 + c;

            // HSL 기반으로 다양한 색상 생성 (GC 방지, 매번 new Color 방지)
            float hue = (hash & 0x7FFFFFFF) % 360 / 360f;
            return Color.HSVToRGB(hue, 0.8f, 1.0f);
        }

        // ===== Public API =====

        /// <summary>
        /// 현재 활성 퀘스트 마커 목록을 반환합니다.
        /// 각 마커에는 퀘스트 이름, 월드 위치, 색상, 플레이어로부터의 거리가 포함됩니다.
        /// </summary>
        public List<QuestMarkerData> GetActiveQuestMarkers()
        {
            return new List<QuestMarkerData>(_activeMarkers);
        }

        /// <summary>
        /// 가장 가까운 활성 퀘스트 마커를 반환합니다.
        /// 활성 마커가 없으면 default(QuestMarkerData)를 반환합니다.
        /// </summary>
        public QuestMarkerData GetNearestQuestMarker()
        {
            if (_activeMarkers == null || _activeMarkers.Count == 0)
                return default;

            QuestMarkerData nearest = _activeMarkers[0];
            for (int i = 1; i < _activeMarkers.Count; i++)
            {
                if (_activeMarkers[i].distanceFromPlayer < nearest.distanceFromPlayer)
                    nearest = _activeMarkers[i];
            }
            return nearest;
        }

        /// <summary>
        /// 활성 마커의 총 개수를 반환합니다.
        /// </summary>
        public int ActiveMarkerCount => _activeMarkers != null ? _activeMarkers.Count : 0;
    }

    /// <summary>
    /// 퀘스트 마커 데이터 구조체.
    /// HUD에서 화면에 표시할 정보를 담고 있습니다.
    /// </summary>
    public struct QuestMarkerData
    {
        /// <summary>퀘스트 이름 (한글)</summary>
        public string questName;
        /// <summary>대상 영지의 월드 위치</summary>
        public Vector3 worldPos;
        /// <summary>마커 색상</summary>
        public Color markerColor;
        /// <summary>플레이어로부터의 거리 (미터)</summary>
        public float distanceFromPlayer;
    }
}