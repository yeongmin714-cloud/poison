using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase O8 (C-O8-01/02/05): 성 영지 건설 관리자 — HOUSE_BUILDING.md 벤치마크.
    /// 흐름: 설계도 선택 → 위치 검증(영지 경계/겹침/지형) → 골드 소비 → 시공 진행 → 완성.
    /// 벤치 원칙: "검증 실패 시 아이템 유지(소비 없음)", 성공 시에만 소비 — 골드 싱크.
    /// Awake 미실행 환경 대응 — FindAnyObjectByType 폴백(O7 확립 패턴).
    /// </summary>
    public class ConstructionManager : MonoBehaviour
    {
        public static ConstructionManager Instance { get; private set; }

        /// <summary>건설 구조물 1건 (설계도+위치+진행).</summary>
        public struct StructureEntry
        {
            public string structureId;
            public string blueprintId;
            public string territoryId;
            public Vector3 pos;
            public float progress;   // 0~1
            public bool isComplete;
            public long completedAtUnix;
        }

        /// <summary>완성 시 발화 (연출/효과 구독용).</summary>
        public static event System.Action<StructureEntry> StructureCompleted;

        /// <summary>영지 반경 기본값 — TerritoryDatabase에 별도 반경 API 없음(주석: 향후 데이터화).</summary>
        public const float TerritoryRadius = 60f;

        private readonly List<StructureEntry> _structures = new List<StructureEntry>();

        public IReadOnlyList<StructureEntry> Structures => _structures;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── 배치 (설계도 사용) ──

        /// <summary>
        /// 설계도 배치 — 순서: 정의 조회 → 위치 검증 → 지형 검증 → 골드 소비 → 시공 등록.
        /// 벤치 원칙: 검증 실패 시 소비 없음(사유 메시지 반환).
        /// </summary>
        public string TryPlace(string territoryId, string blueprintId, Vector3 pos)
        {
            if (!BlueprintData.TryGet(blueprintId, out var def))
                return "알 수 없는 설계도";

            // 영지 정의 조회 (nation None = 미등록 키)
            var territoryDb = TerritoryDatabase.Instance;
            var region = territoryDb != null ? territoryDb.GetDefinition(territoryId) : new TerritoryDefinition();
            if (region.nation == NationType.None)
                return "영지 없음";

            // 위치 검증 (순수)
            if (!BlueprintData.ValidatePlacement(region.worldPosition, TerritoryRadius, pos, def.footprintRadius, GetFootprints()))
                return "영지 경계 밖 또는 기존 건설물과 겹침";

            // 지형 검증 (런타임 전용 — 에디터 테스트에서는 Raycast 회피)
            if (Application.isPlaying && !BlueprintData.TerrainFlatCheck(pos))
                return "경사진 지형";

            // 골드 소비 (성공 시에만 — 벤치 원칙)
            var stats = PlayerStats.Instance ?? FindAnyObjectByType<PlayerStats>();
            if (stats == null || !stats.SpendGold(def.goldCost, "construction"))
                return $"골드 부족 (필요 {def.goldCost}G)";

            var entry = new StructureEntry
            {
                structureId = $"struct_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}",
                blueprintId = blueprintId,
                territoryId = territoryId,
                pos = pos,
                progress = 0f,
                isComplete = false,
            };
            _structures.Add(entry);

            Debug.Log($"[Construction] 🏗️ 건설 시작: {def.displayName} @ {territoryId} (예상 {def.buildTimeSeconds}초)");
            return $"건설 시작! {def.displayName} (예상 {def.buildTimeSeconds}초)";
        }

        private List<(Vector3 pos, float radius)> GetFootprints()
        {
            var list = new List<(Vector3, float)>();
            foreach (var s in _structures)
            {
                if (BlueprintData.TryGet(s.blueprintId, out var def))
                    list.Add((s.pos, def.footprintRadius));
            }
            return list;
        }

        // ── 시공 진행 ──

        private void Update()
        {
            if (_structures.Count == 0) return;

            // [O8 규약 수리] foreach 반복 변수 수정 금지(CS1654) — 인덱스 루프로 진행 갱신
            for (int i = 0; i < _structures.Count; i++)
            {
                if (_structures[i].isComplete) continue;
                if (!BlueprintData.TryGet(_structures[i].blueprintId, out var def)) continue;

                float advance = Time.deltaTime / Mathf.Max(1f, def.buildTimeSeconds);
                var s = _structures[i];
                s.progress = Mathf.Clamp01(s.progress + advance);

                if (s.progress >= 1f)
                {
                    CompleteStructure(s);
                    _structures[i] = s;
                }
                else
                {
                    _structures[i] = s;
                }
            }
        }

        /// <summary>구조물 완료 처리 — 이벤트 발화 + 효과 적용 + 시각물 생성(런타임).</summary>
        private void CompleteStructure(StructureEntry s)
        {
            s.isComplete = true;
            s.completedAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 효과 적용
            if (BlueprintData.TryGet(s.blueprintId, out var def))
            {
                if (def.effectId == "warehouse_ext")
                {
                    var ws = WarehouseSystem.Instance ?? FindAnyObjectByType<WarehouseSystem>();
                    ws?.AddExpansionLevel(s.territoryId);
                }
                Debug.Log($"[Construction] ✅ 건설 완료: {def.displayName} @ {s.territoryId}");
            }

            BuildVisual(s);
            StructureCompleted?.Invoke(s);
        }

        /// <summary>테스트/강제 완료 — progress 1 승격 (Play 없이 효과 검증용).</summary>
        public void ForceComplete(string structureId)
        {
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s.structureId == structureId && !s.isComplete)
                {
                    s.progress = 1f;
                    CompleteStructure(s);
                    _structures[i] = s;
                    return;
                }
            }
        }

        // ── 조회/효과 확인 ──

        /// <summary>해당 영지에 해당 effectId의 "완료된" 구조물 존재? (대장간/축사 확인용)</summary>
        public bool HasStructure(string territoryId, string effectId)
        {
            foreach (var s in _structures)
            {
                if (s.territoryId != territoryId || !s.isComplete) continue;
                if (BlueprintData.TryGet(s.blueprintId, out var def) && def.effectId == effectId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// [O8 C-O8-03] 영지 무관 완료 구조물 존재 여부 — 전역 효과 판정용(대장간 수리비 할인 등).
        /// 현재 영지 추적이 미구성(후속 과제)이라 전역 스코프로 판정한다.
        /// </summary>
        public bool HasAnyCompletedStructure(string effectId)
        {
            foreach (var s in _structures)
            {
                if (!s.isComplete) continue;
                if (BlueprintData.TryGet(s.blueprintId, out var def) && def.effectId == effectId)
                    return true;
            }
            return false;
        }

        // ── 해체 (환불 50%) ──

        /// <summary>구조물 해체 — 골드 환불 50%(원장 태그 construction_refund). 벤치: 철거 검증 후 소유자만.</summary>
        public string Demolish(string structureId)
        {
            for (int i = 0; i < _structures.Count; i++)
            {
                if (_structures[i].structureId != structureId) continue;

                var s = _structures[i];
                if (BlueprintData.TryGet(s.blueprintId, out var def))
                {
                    int refund = def.goldCost / 2;
                    var stats = PlayerStats.Instance ?? FindAnyObjectByType<PlayerStats>();
                    stats?.AddGold(refund, "construction_refund");
                    _structures.RemoveAt(i);
                    return $"해체 완료 (환불 {refund}G)";
                }
                _structures.RemoveAt(i);
                return "해체 완료";
            }
            return "구조물 없음";
        }

        // ── 시각물 (절차 프리미티브 — 런타임만) ──

        private readonly Dictionary<string, GameObject> _visuals = new Dictionary<string, GameObject>();

        private void BuildVisual(StructureEntry s)
        {
            if (!Application.isPlaying || _visuals.ContainsKey(s.structureId)) return;
            if (!BlueprintData.TryGet(s.blueprintId, out var def)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Structure_{def.displayName}";
            float height = def.footprintRadius * 2f;
            go.transform.position = s.pos + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(def.footprintRadius * 2f, height, def.footprintRadius * 2f);
            _visuals[s.structureId] = go;
        }

        // ── 저장 접합 (C-O8-05) ──

        /// <summary>SaveManager 수집용 — 현재 구조물 목록.</summary>
        public List<ConstructionSaveEntry> GetSaveEntries()
        {
            var list = new List<ConstructionSaveEntry>();
            foreach (var s in _structures)
            {
                list.Add(new ConstructionSaveEntry
                {
                    structureId = s.structureId,
                    blueprintId = s.blueprintId,
                    territoryId = s.territoryId,
                    posX = s.pos.x, posY = s.pos.y, posZ = s.pos.z,
                    progress = s.progress,
                    isComplete = s.isComplete,
                });
            }
            return list;
        }

        /// <summary>SaveManager 복원용 — 구형 세이브(빈 리스트) 호환. [O8 간편 규칙] 미완료 건은 즉시 완료 승격(오프라인 경과 단순화).</summary>
        public void ApplySaveEntries(List<ConstructionSaveEntry> entries)
        {
            _structures.Clear();
            if (entries == null) return;

            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.structureId)) continue;

                var s = new StructureEntry
                {
                    structureId = e.structureId,
                    blueprintId = e.blueprintId,
                    territoryId = e.territoryId,
                    pos = new Vector3(e.posX, e.posY, e.posZ),
                    progress = e.progress,
                    isComplete = e.isComplete,
                };

                // 오프라인 간편 규칙: 저장 시점에 미완료였던 건은 로드 시 완료 승격
                if (!s.isComplete)
                {
                    s.progress = 1f;
                    s.isComplete = true;
                }
                _structures.Add(s);
            }
        }
    }
}
