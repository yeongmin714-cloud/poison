using System.Collections.Generic;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using UnityEngine;
using UnityEngine.InputSystem;   // [D5-B] Keyboard.current.eKey — 상점(ShopPlaceholder) 동일 E키 폴링

namespace ProjectName.Systems
{
    /// <summary>
    /// [D4-A] 음식점 실물 시각 빌더 — ConstructionManager.StructureCompleted 이벤트 구독형.
    ///
    /// 설계 (VillageBuilder hut 폴백 패턴 준용):
    ///  · bp_restaurant 구조물 완성 시 마을 pos에 음식점 실물을 절차 생성한다.
    ///    - 프리미티브 폴백: 몸통 박스 + 사면(4경사면) 지붕 + 출입문 — VillageBuilder 큐브 조합 패턴 재사용.
    ///    - 국가 틴트: VillageBuilder.GetNationBuildingTint 규약 동일(동=녹갈, 서=황토, 남=적갈, 북=회백).
    ///    - 지면 접지: TerrainGenerator.GetHeightAt(..., BiomeType.Plains, 42) + GROUND_BASE(1f) — VillageBuilder와
    ///      동일 기저 보정(TerritoryBuilder.TrySpawnModelOrPlaceholder 준용).
    ///  · 완성 시 VillageDrugState.SetRestaurantBuilt(nation, villageIndex, true) — 마을 매칭은
    ///    ConstructionManager.TryFindRestaurantVillage(pos) 재사용(pos 최근접 마을, 결정론).
    ///  · 배선 불요: MonoBehaviour 아님 — RuntimeInitializeOnLoadMethod로 이벤트 구독(도메인 리로드
    ///    안전을 위해 구독 전 해제). ConstructionManager가 자체 Update로 시공 완료 처리한다.
    ///  · 기본 큐브 이중 생성 방지: ConstructionManager.BuildVisual은 effectId=="restaurant"를 스킵한다.
    ///
    /// ※ 세이브 복원 경로(ApplySaveEntries의 미완료→완료 승격)는 이벤트가 발화되지 않으므로
    ///   [D5-A] RebuildFromSave(ConstructionManager.ApplySaveEntries 로드 훅에서 호출)가
    ///   실물/플래그를 복원한다 — 런타임 완성 경로와 동일 로직(TryFindRestaurantVillage 매칭) 재사용.
    /// </summary>
    public static class RestaurantPropBuilder
    {
        /// <summary>지형 기저 보정 (Ground_Inner 월드 y=1) — VillageBuilder와 동일.</summary>
        private const float GROUND_BASE = 1f;

        /// <summary>지형 높이 조회 시드 — VillageBuilder와 동일.</summary>
        private const int TerrainSeed = 42;

        /// <summary>음식점 설계도 id — BlueprintData.bp_restaurant.id와 동일 값(정적 클래스라 상수 공유 불가). [D5-A] ConstructionManager 복원 훅에서도 참조.</summary>
        public const string RestaurantBlueprintId = "bp_restaurant";

        /// <summary>실물 부모 컨테이너 이름 (중복 생성 방지용).</summary>
        private const string RootName = "RestaurantProps_Root";

        /// <summary>생성된 실물 캐시 — structureId 중복 생성 방지 (구조물 1건 = 실물 1개).</summary>
        private static readonly Dictionary<string, GameObject> _props = new Dictionary<string, GameObject>();

        /// <summary>이벤트 구독 — 씬 로드 후 1회 (구독 전 해제로 도메인 리로드/재진입 중복 방지).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            ConstructionManager.StructureCompleted -= OnStructureCompleted;
            ConstructionManager.StructureCompleted += OnStructureCompleted;
        }

        // ===== [D5-A] 세이브 복원 경로 =====

        /// <summary>
        /// 세이브 로드 직후 음식점 실물 복원 — ConstructionManager.ApplySaveEntries 마지막 훅에서 호출.
        /// ApplySaveEntries는 StructureCompleted 이벤트를 발화하지 않으므로(기존 규약), 로드 직후에는
        /// 완료된 음식점의 실물이 생성되지 않는 갭이 있었다. 본 메서드가 완료된 bp_restaurant 구조물을
        /// 순회해 런타임 완성 경로와 동일 로직(TryFindRestaurantVillage 마을 매칭 → SetRestaurantBuilt →
        /// BuildProp)으로 복원한다. structureId 캐시(_props)로 중복 생성을 방지한다.
        /// 호출 순서 규약: SaveManager.Load에서 VillageDrugState.ImportSaveEntries(전면 클리어)가
        /// 선행된 뒤 호출된다 — 플래그 클리어 간섭 방지.
        /// </summary>
        public static void RebuildFromSave()
        {
            if (!Application.isPlaying) return; // 런타임 전용 — OnStructureCompleted 동일 규약(EditMode 무동작)

            var mgr = ConstructionManager.Instance;
            if (mgr == null) return; // Awake 미실행 환경 — 안전 무동작(O7 폴백 패턴 취급)

            foreach (ConstructionManager.StructureEntry entry in mgr.Structures)
            {
                if (entry.blueprintId != RestaurantBlueprintId || !entry.isComplete)
                    continue; // 완료된 음식점만 대상 (미완료 건은 ApplySaveEntries가 완료 승격 후 목록에 있음)

                if (_props.ContainsKey(entry.structureId))
                    continue; // 실물 이미 존재 — structureId 캐시 중복 생성 방지

                // 런타임 완성 경로(OnStructureCompleted)와 동일한 마을 매칭/플래그/실물 생성 재사용
                bool matched = ConstructionManager.TryFindRestaurantVillage(entry.pos, out NationType nation, out int villageIndex);
                if (matched)
                {
                    VillageDrugState.SetRestaurantBuilt(nation, villageIndex, true);
                    Debug.Log($"[RestaurantPropBuilder] ✅ 세이브 복원 음식점 플래그: {nation} 마을#{villageIndex} (id={entry.structureId})");
                }
                else
                {
                    Debug.LogWarning($"[RestaurantPropBuilder] 세이브 복원 — pos 최근접 마을 미발견, 플래그 생략 (id={entry.structureId} pos={entry.pos})");
                }

                // [D5-B] 매칭 결과 캡처 — 미매칭(-1)은 상호작용 부착 생략(창을 열 마을 키 부재)
                BuildProp(entry.structureId, entry.pos, nation, matched ? villageIndex : -1);
            }
        }

        /// <summary>
        /// [105차 A] 결함5 — 구조물 해체 시 실물 파괴. ConstructionManager.Demolish에서 호출한다.
        /// _props 캐시에서 제거 + Destroy(go). 캐시 부재 시 false 반환(로그 없음 — 미생성/이미 파괴 무해).
        /// 런타임 전용 — EditMode 무동작(RebuildFromSave의 Application.isPlaying 규약 동일).
        /// </summary>
        public static bool TryDestroyProp(string structureId)
        {
            if (!Application.isPlaying) return false; // 런타임 전용 — EditMode 무동작
            if (string.IsNullOrEmpty(structureId) || !_props.TryGetValue(structureId, out GameObject go))
                return false;
            _props.Remove(structureId);
            if (go != null) Object.Destroy(go);
            return true;
        }

        /// <summary>완성 이벤트 처리 — 음식점만 필터링, 플래그 설정 + 실물 생성.</summary>
        private static void OnStructureCompleted(ConstructionManager.StructureEntry entry)
        {
            if (!Application.isPlaying) return;                      // EditMode/에디터에서 무동작 (런타임 전용)
            if (entry.blueprintId != RestaurantBlueprintId) return;   // bp_restaurant만 대상

            // 마을 매칭 — pos 최근접 마을 (결정론). TryPlace 검증을 통과했으므로 실패는 비정상 경로.
            // [D5-B] 매칭 결과 캡처 — 미매칭(-1)은 상호작용 부착 생략(창을 열 마을 키 부재).
            bool matched = ConstructionManager.TryFindRestaurantVillage(entry.pos, out NationType nation, out int villageIndex);
            if (matched)
            {
                VillageDrugState.SetRestaurantBuilt(nation, villageIndex, true);
                Debug.Log($"[RestaurantPropBuilder] ✅ 음식점 완공 플래그: {nation} 마을#{villageIndex} (id={entry.structureId})");
            }
            else
            {
                Debug.LogWarning($"[RestaurantPropBuilder] pos 최근접 마을 미발견 — 플래그 생략 (id={entry.structureId} pos={entry.pos})");
            }

            BuildProp(entry.structureId, entry.pos, nation, matched ? villageIndex : -1);
        }

        /// <summary>
        /// 음식점 실물 생성 (프리미티브 폴백 — VillageBuilder hut 큐브 조합 패턴 준용).
        /// [D5-B] villageIndex — 매칭된 마을 인덱스(0~19). 미매칭은 -1로 상호작용 컴포넌트를 부착하지 않는다.
        /// </summary>
        private static void BuildProp(string structureId, Vector3 pos, NationType nation, int villageIndex)
        {
            if (_props.ContainsKey(structureId)) return; // 중복 생성 방지

            // 지면 접지 — VillageBuilder와 동일 기저 (TerrainGenerator 결정론 높이 + GROUND_BASE)
            float groundY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;

            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);

            var go = new GameObject($"Restaurant_{structureId}");
            go.transform.position = new Vector3(pos.x, groundY, pos.z);

            // [170차 P0] 시각 블록 — BuildRestaurantVisualParts 단일 소스로 이동(순수 재배치, 실물 경로 무변경)
            BuildRestaurantVisualParts(go.transform, nation);

            // 건물 표식 — BuildingPlaceholder(Other, "마을 음식점"). E 상호작용은 하단 [D5-B] 컴포넌트가 담당.
            var placeholder = go.AddComponent<BuildingPlaceholder>();
            placeholder.buildingType = BuildingPlaceholder.BuildingType.Other;
            placeholder.buildingName = "마을 음식점";

            // [D5-B] 상호작용 부착 — 상점 인터랙터블 패턴(ShopPlaceholder E 폴링)을 음식점 실물에 적용.
            //        매칭된 마을(nation, villageIndex)을 캡처해 개방 콜백으로 전달한다(세이브 복원 경로 포함).
            //        미매칭(villageIndex < 0)은 창을 열 마을 키가 없으므로 부착 생략(안전).
            if (villageIndex >= 0)
            {
                var interactable = go.AddComponent<RestaurantPropInteractable>();
                interactable.Configure(nation, villageIndex);
            }

            go.transform.SetParent(root.transform, true);
            // [116차 R3] 가시성 진단 — 실물 월드 y vs 지형 높이(매몰 판정).
            float diagTerrainY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;
            Debug.Log($"[RestaurantPropBuilder] 진단: worldY={go.transform.position.y:F2} terrainY={diagTerrainY:F2}");

            _props[structureId] = go;
            Debug.Log($"[RestaurantPropBuilder] 🏠 음식점 실물 생성: pos=({pos.x:F1},{pos.z:F1}) nation={nation}");
        }

        /// <summary>지붕 경사판 1장 생성 — 위치/회전/스케일 지정, 국가 틴트 기반 지붕색.</summary>
        private static void CreateRoofSlab(Transform parent, string name, Vector3 localPos, Quaternion rot, Vector3 scale, Color roofColor)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, false);
            slab.transform.localPosition = localPos;
            slab.transform.localRotation = rot;
            slab.transform.localScale = scale;
            slab.tag = "Untagged";
            var r = slab.GetComponent<MeshRenderer>();
            if (r != null)
                r.material = MaterialHelper.CreateLitMaterial(roofColor, "RestaurantRoof_Mat");
        }

        /// <summary>지붕 경사각 (rad) — 용마루 높이와 수평 반폭 기준.</summary>
        private static float Angle(float ridgeH, float halfSpan) => Mathf.Atan2(ridgeH, halfSpan);

        /// <summary>지붕 경사면 길이 — 피타고라스 (용마루 높이, 수평 반폭).</summary>
        private static float SlopeLen(float ridgeH, float halfSpan) => Mathf.Sqrt(halfSpan * halfSpan + ridgeH * ridgeH);

        /// <summary>건물 국가 틴트 — VillageBuilder.GetNationBuildingTint와 동일 규약 (동=녹갈, 서=황토, 남=적갈, 북=회백).</summary>
        private static Color GetNationBuildingTint(NationType nation) => nation switch
        {
            NationType.East  => new Color(0.42f, 0.34f, 0.22f), // 동 — 녹갈 토
            NationType.West  => new Color(0.50f, 0.38f, 0.22f), // 서 — 황토
            NationType.South => new Color(0.48f, 0.31f, 0.20f), // 남 — 적갈
            NationType.North => new Color(0.40f, 0.40f, 0.38f), // 북 — 회백 설토
            _                => new Color(0.50f, 0.48f, 0.45f)  // 기타 — 회색
        };

        /// <summary>[170차 P0] 음식점 시각 파트 생성 — 실물 경로와 홀로그램 경로가 공유하는 단일 소스.
        /// BuildProp의 시각 블록(색상+몸통+지붕4판+문)을 그대로 이동 — 실물 경로 동작 무변경(순수 재배치).</summary>
        private static void BuildRestaurantVisualParts(Transform parent, NationType nation)
        {
            Color nationTint = GetNationBuildingTint(nation);
            Color wallColor = Color.Lerp(nationTint, Color.white, 0.30f);  // 음식점 — 집보다 밝은 벽체
            Color roofColor = Color.Lerp(nationTint, Color.black, 0.40f);

            // 몸통 박스 (4.2 × 2.4 × 3.6 — 마을 집(2.2)보다 큰 요리 판매용 건물)
            const float bodyW = 4.2f, bodyH = 2.4f, bodyD = 3.6f;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, bodyH * 0.5f, 0f);
            body.transform.localScale = new Vector3(bodyW, bodyH, bodyD);
            body.tag = "Untagged";
            var br = body.GetComponent<MeshRenderer>();
            if (br != null)
                br.material = MaterialHelper.CreateLitMaterial(wallColor, "RestaurantWall_Mat");

            // 사면(4경사면) 지붕 — 박스 4장을 각 경사각으로 회전 배치 (결정론 기하)
            const float ridgeH = 1.1f;   // 처마에서 용마루까지 높이
            const float eave = 0.35f;    // 처마 내밈
            const float t = 0.12f;       // 지붕판 두께
            float baseY = bodyH;         // 지붕 시작 높이 (몸통 상단)
            CreateRoofSlab(parent, "Roof_Front",
                new Vector3(0f, baseY + ridgeH * 0.5f, bodyD * 0.25f), Quaternion.Euler(Angle(ridgeH, bodyD), 0f, 0f),
                new Vector3(bodyW + eave * 2f, t, SlopeLen(ridgeH, bodyD) + eave), roofColor);
            CreateRoofSlab(parent, "Roof_Back",
                new Vector3(0f, baseY + ridgeH * 0.5f, -bodyD * 0.25f), Quaternion.Euler(-Angle(ridgeH, bodyD), 0f, 0f),
                new Vector3(bodyW + eave * 2f, t, SlopeLen(ridgeH, bodyD) + eave), roofColor);
            CreateRoofSlab(parent, "Roof_Right",
                new Vector3(bodyW * 0.25f, baseY + ridgeH * 0.5f, 0f), Quaternion.Euler(0f, 0f, -Angle(ridgeH, bodyW)),
                new Vector3(SlopeLen(ridgeH, bodyW) + eave, t, bodyD + eave * 2f), roofColor);
            CreateRoofSlab(parent, "Roof_Left",
                new Vector3(-bodyW * 0.25f, baseY + ridgeH * 0.5f, 0f), Quaternion.Euler(0f, 0f, Angle(ridgeH, bodyW)),
                new Vector3(SlopeLen(ridgeH, bodyW) + eave, t, bodyD + eave * 2f), roofColor);

            // 출입문 — 마을 광장 쪽 전면(+Z)에 박은 얇은 판 (어두운 목재)
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";
            door.transform.SetParent(parent, false);
            door.transform.localPosition = new Vector3(0f, 0.8f, bodyD * 0.5f + 0.04f);
            door.transform.localScale = new Vector3(0.9f, 1.6f, 0.08f);
            door.tag = "Untagged";
            var dr = door.GetComponent<MeshRenderer>();
            if (dr != null)
                dr.material = MaterialHelper.CreateLitMaterial(new Color(0.30f, 0.20f, 0.12f), "RestaurantDoor_Mat");
        }

        /// <summary>[170차 P0] 홀로그램용 음식점 시각 생성 — BuildRestaurantVisualParts 재사용(단일 소스).
        /// 콜라이더 제거(isPlaying 가드), E키 상호작용/BuildingPlaceholder 배제. nation은 시그니처 유지용
        /// (형태는 nation 무관 — 최종 색은 홀로그램 틴트가 덮음).</summary>
        public static GameObject BuildHologramVisual(string blueprintId, NationType nation)
        {
            if (blueprintId != RestaurantBlueprintId) return null;
            var go = new GameObject("Hologram_bp_restaurant");
            BuildRestaurantVisualParts(go.transform, nation);
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            return go;
        }
    }

    /// <summary>
    /// [D5-B] 음식점 실물 E 상호작용 컴포넌트 — 상점 인터랙터블 패턴(ShopPlaceholder) 준용:
    ///   · Update 근접 폴링 → EInteractionPromptBridge.RaiseRegister/Unregister(프롬프트 "E: 음식점")
    ///   · E키(Input System) → OpenRequestedUTK 이벤트 발화 — 부착 시 캡처한 (nation, villageIndex) 전달.
    ///   · UI 구독은 RestaurantWindowUTK.Bootstrap(RuntimeInitializeOnLoadMethod)이 담당 —
    ///     Systems→UI 순환참조 회피 정적 이벤트(UTKWireUp 동일 정신, 무구독 시 무효 — 되돌리기 용이).
    /// ※ 런타임 AddComponent 전용(RestaurantPropBuilder.BuildProp 부착) — 프리팹/씬 직렬화 대상 아님
    ///   (RestaurantSystem.cs의 RestaurantDailyTicker 동일 규약: 런타임 생성 컴포넌트의 동일 파일 배치 선례).
    ///   프롬프트 문구/범위는 Figma 프레임 후속 동기화 가능(확정 시 갱신).
    /// </summary>
    public class RestaurantPropInteractable : MonoBehaviour
    {
        /// <summary>음식점 개방 요청 — UI(RestaurantWindowUTK)가 구독. 파라미터 = 매칭 캡처 (nation, villageIndex).</summary>
        public static event System.Action<NationType, int> OpenRequestedUTK;

        private const float InteractRange = 3f;   // 상점(ShopPlaceholder._interactRange) 동일 기본값

        private NationType _nation;
        private int _villageIndex = -1;           // -1 = 미구성 — 무동작 가드

        private Transform _player;                // 캐싱된 플레이어 참조 (매 프레임 Find 방지 — ShopPlaceholder 동일)

        /// <summary>마을 캡처 구성 — BuildProp 부착 직후 1회 호출.</summary>
        public void Configure(NationType nation, int villageIndex)
        {
            _nation = nation;
            _villageIndex = villageIndex;
        }

        private void Update()
        {
            if (_villageIndex < 0)
                return; // 미구성 — 안전 무동작(부착 규약 위반 방어)

            // 플레이어 참조 캐싱 (씬 전환 시 재탐색 — ShopPlaceholder Update 47~52행 동일)
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (_player == null) return;
            }

            // sqrMagnitude로 거리 비교 (Distance보다 GC/성능 우수 — ShopPlaceholder 54~56행 동일)
            float sqrDist = (transform.position - _player.position).sqrMagnitude;
            bool nearby = sqrDist <= InteractRange * InteractRange;

            // [43차] 공용 E 월드 프롬프트 — 근접 시 등록, 이탈 시 해제(창 열림 시 UI가 자동 숨김).
            if (nearby)
                EInteractionPromptBridge.RaiseRegister(transform, "E: 음식점", InteractRange);
            else
                EInteractionPromptBridge.RaiseUnregister(transform);
            if (!nearby) return;

            // E키로 상호작용 (Input System — ShopPlaceholder 65~69행 동일)
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                OpenRequestedUTK?.Invoke(_nation, _villageIndex);
            }
        }

        private void OnDisable()
        {
            // 프롬프트 잔존 방지 — 비활성화 직전 해제(방어. anchor null은 브리지 내부에서 가드).
            EInteractionPromptBridge.RaiseUnregister(transform);
        }
    }
}
