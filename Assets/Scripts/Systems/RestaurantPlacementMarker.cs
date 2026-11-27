using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
using UnityEngine.InputSystem;   // [88차] Keyboard.current.eKey — ShopPlaceholder/RestaurantPropInteractable 동일 E키 폴링

namespace ProjectName.Systems
{
    /// <summary>
    /// [88차 A2] 마을 음식점 배치 지점 마커/VFX + E키 배치 — 런타임 AddComponent 전용 컴포넌트
    /// (프리팹/씬 직렬화 대상 아님 — RestaurantDailyTicker 규약 준용, 부트는 RuntimeInitializeOnLoadMethod).
    ///
    /// 동작:
    ///  · Update 폴링 — 플레이어(tag "Player") 캐싱(ShopPlaceholder 47~52행 선례), sqrMagnitude 비교.
    ///  · 최근접 마을(반경+VillagePlacementMargin 내) 스캔(0.5s 스로틀) → RestaurantPlacementSpotSystem.
    ///    TryGetValidSpots 유효 지점 산출. (링 VFX는 [142차 A] 폐기 — 골드 링 상시 노출이 잔존물로
    ///    오인되는 사용자 지시에 따라 비표출. E키 배치 프롬프트/기능은 유지.)
    ///  · 표시 범위 — 유효 스팟에서 25m 이내 접근 시 해당 마을의 스팟 링 표시, 이탈 시 숨김
    ///    (파괴 아님 — 풀 재사용).
    ///  · VFX 색 — 골드 앰버 #E3B341(RTS 파랑 선택링과 구분, "건설 가능" 관례), 반경 2.5m
    ///    (bp_restaurant footprint 일치), collider 없음(컨트롤러 계약).
    ///  · E 프롬프트 — 최근접 유효 스팟 앵커에 EInteractionPromptBridge.RaiseRegister("E: 음식점 배치", 3f),
    ///    이탈/숨김 시 RaiseUnregister.
    ///  · E키 → 배치 — 유효성 재확인 후 ConstructionManager.TryPlace(territoryId, "bp_restaurant", spotPos) →
    ///    반환 메시지로 PlacementAttempted(bool, string) 정적 이벤트 발화(성공="건설 시작! ...", 실패=사유).
    ///    이벤트 구독은 UI(Phase B) 담당 — Systems→UI 순환참조 회피 정적 이벤트 규약, 무구독이면 로그만.
    /// </summary>
    public class RestaurantPlacementMarker : MonoBehaviour
    {
        /// <summary>
        /// [88차] 배치 시도 결과 — UI(Phase B)가 구독해 토스트 표시. success=true는 "건설 시작!" 경로,
        /// false는 TryPlace 사유 메시지(골드/재료/허가세/겹침 등). 무구독 시 로그만 남긴다.
        /// </summary>
        public static event System.Action<bool, string> PlacementAttempted;

        /// <summary>E 프롬프트 문구 — [88차] 스펙 고정 진실치.</summary>
        public const string PromptLabel = "E: 음식점 배치";

        /// <summary>E 프롬프트/상호작용 반경 (m) — RestaurantPropInteractable.InteractRange 동일 기본값.</summary>
        public const float PromptRange = 3f;

        /// <summary>링 표시 범위 (m) — 유효 스팟에서 이 거리 이내 접근 시 링 표시, 이탈 시 숨김.</summary>
        public const float ShowRange = 25f;

        /// <summary>마을/스팟 재스캔 주기 (초) — 씬 질의(Renderers) 비용 스로틀.</summary>
        public const float ScanIntervalSeconds = 0.5f;

        /// <summary>
        /// [88차] 배치 링 색 — 골드 앰버 #E3B341 (RTS 파랑 선택링과 구분, "건설 가능" 관례).
        /// </summary>
        public static readonly Color PlacementRingColor = new Color(0.890f, 0.702f, 0.255f, 1f); // #E3B341

        private const string SingletonName = "RestaurantPlacementMarker_Root";
        private const string RingName = "RestaurantSpotRing";
        private const string PromptAnchorName = "RestaurantPlacementPromptAnchor";
        private const float RingGroundLift = 0.03f; // 지면 z-fight 방지 리프트(AttackTargetRingManager 동일 톤)

        private static RestaurantPlacementMarker _instance;

        private Transform _player;                       // 캐싱된 플레이어 참조 — 매 프레임 Find 방지(ShopPlaceholder 동일)
        private readonly List<Vector3> _validSpots = new List<Vector3>();
        private VillagePlacementSystem.VillageInfo _nearVillage; // 최근접 마을 캐시(nation None = 부재)
        private bool _hasNearVillage;
        private float _nextScanTime;
        private bool _promptRegistered;
        private readonly List<GameObject> _rings = new List<GameObject>(); // 링 풀 — 파괴 아님, 재사용
        private GameObject _promptAnchor;
        private Material _sharedRingMaterial;            // 골드 링 공유 머티리얼(마커 수명 1 인스턴스)

        /// <summary>
        /// 부트 — 씬 로드 후 1회 싱글턴 생성(구독 전 해제 패턴 동일 정신: 정적 캐시 선정리 후 보장).
        /// DontDestroyOnLoad로 씬 전환 간 재사용(RestaurantPropBuilder는 이벤트 구독형이라 대조적 —
        /// 본 컴포넌트는 Update 폴링 오브젝트라 DDOL 유지).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Application.isPlaying)
                return; // 런타임 전용 — EditMode 무동작(RestaurantPropBuilder.Initialize 규약)
            if (_instance != null)
                return; // 도메인 리로드/재진입 중복 방지 — 기존 싱글턴 재사용

            var root = new GameObject(SingletonName);
            _instance = root.AddComponent<RestaurantPlacementMarker>();
            Object.DontDestroyOnLoad(root);
            Debug.Log("[RestaurantPlacementMarker] ✅ 배치 지점 마커 부트 완료(싱글턴)");
        }

        private void Update()
        {
            // 플레이어 참조 캐싱 (씬 전환 시 재탐색 — ShopPlaceholder Update 47~52행 동일)
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (_player == null)
                {
                    HideSpots();
                    return;
                }
            }

            // 주기 스캔 — 마을 탐색 + 유효 스팟(씬 질의)은 0.5s 스로틀
            if (Time.unscaledTime >= _nextScanTime)
            {
                _nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
                ScanNearbyVillage();
            }

            UpdateRingsVisibility();  // 25m 표시/이탈 숨김(파괴 아님 — 재사용)
            UpdatePromptAndEKey();    // 프롬프트 등록/해제 + E키 배치
        }

        /// <summary>최근접 마을 스캔(반경+VillagePlacementMargin 내) → 유효 스팟 갱신(점유 마을은 빈 목록).</summary>
        private void ScanNearbyVillage()
        {
            Vector3 playerPos = _player.position;
            VillagePlacementSystem.VillageInfo best = default;
            float bestSqr = float.MaxValue;
            bool found = false;
            foreach (VillagePlacementSystem.VillageInfo village in VillagePlacementSystem.GetAllVillages())
            {
                float dx = village.center.x - playerPos.x;
                float dz = village.center.z - playerPos.z;
                float range = village.radius + ConstructionManager.VillagePlacementMargin; // 반경+5m
                float sqr = dx * dx + dz * dz;
                if (sqr <= range * range && sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = village;
                    found = true;
                }
            }

            if (!found)
            {
                _hasNearVillage = false;
                _validSpots.Clear();
                return;
            }

            _nearVillage = best;
            _hasNearVillage = true;
            // 유효 스팟 재판정 — 점유 변화(타 경로 건설/세이브 로드)를 스캔 주기로 반영.
            // 점유 마을(restaurantBuilt/bp_restaurant 구조물)은 빈 목록 반환 → 링/프롬프트 숨김.
            RestaurantPlacementSpotSystem.TryGetValidSpots(best, _validSpots);
        }

        /// <summary>링 표시 판정 — 유효 스팛 중 하나라도 25m 이내면 해당 마을 스팟 링 전체 표시.</summary>
        private void UpdateRingsVisibility()
        {
            int visibleCount = 0;
            if (_hasNearVillage)
            {
                float showSqr = ShowRange * ShowRange;
                foreach (Vector3 spot in _validSpots)
                {
                    float dx = spot.x - _player.position.x;
                    float dz = spot.z - _player.position.z;
                    if (dx * dx + dz * dz <= showSqr)
                    {
                        visibleCount = _validSpots.Count; // 스팟 하나라도 접근 시 마을 스팟 전체 표시
                        break;
                    }
                }
            }
            SetRingsActive(visibleCount);
        }

        /// <summary>프롬프트 등록/해제 + E키 배치 — 최근접 유효 스팟 3m 이내 기준.</summary>
        private void UpdatePromptAndEKey()
        {
            if (!_hasNearVillage || !TryGetNearestSpot(PromptRange, out Vector3 nearestSpot))
            {
                if (_promptRegistered)
                {
                    EInteractionPromptBridge.RaiseUnregister(GetPromptAnchor().transform);
                    _promptRegistered = false;
                }
                return;
            }

            Transform anchor = GetPromptAnchor().transform;
            anchor.position = nearestSpot;
            // [88차] 공용 E 월드 프롬프트 — 최근접 유효 스팟 앵커에만 등록, 이탈/숨김 시 해제
            EInteractionPromptBridge.RaiseRegister(anchor, PromptLabel, PromptRange);
            _promptRegistered = true;

            // E키로 배치 시도 (Input System — ShopPlaceholder 65~69행 동일)
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                TryPlaceAtNearest(nearestSpot);
        }

        /// <summary>유효 스팟 중 플레이어 최근접 지점(range 이내) 조회 — sqrMagnitude 비교(선례 규약).</summary>
        private bool TryGetNearestSpot(float range, out Vector3 nearest)
        {
            nearest = default;
            float rangeSqr = range * range;
            float bestSqr = float.MaxValue;
            bool found = false;
            foreach (Vector3 spot in _validSpots)
            {
                float dx = spot.x - _player.position.x;
                float dz = spot.z - _player.position.z;
                float sqr = dx * dx + dz * dz;
                if (sqr <= rangeSqr && sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = spot;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// [88차] E키 → 배치 — TryPlace 재호출 전 유효성 재확인(스팟 점유 변화 대응) 후
        /// ConstructionManager.TryPlace(territoryId, "bp_restaurant", spotPos) → PlacementAttempted 발화.
        /// </summary>
        private void TryPlaceAtNearest(Vector3 spotPos)
        {
            if (!_hasNearVillage)
                return;

            // TryPlace 재호출 전 유효성 재확인 — 점유 변화 시 조용히 취소(다음 스캔에서 반영)
            var revalidated = new List<Vector3>();
            if (!RestaurantPlacementSpotSystem.TryGetValidSpots(_nearVillage, revalidated))
            {
                HideSpots();
                return;
            }
            bool stillValid = false;
            foreach (Vector3 spot in revalidated)
            {
                if ((spot - spotPos).sqrMagnitude <= 0.01f) // 동일 스팟 판정 — 0.1m 허용 오차
                {
                    stillValid = true;
                    break;
                }
            }
            if (!stillValid)
            {
                _validSpots.Clear(); // 스팟 무효화 — 다음 스캔에서 재산출
                return;
            }

            ConstructionManager mgr = ConstructionManager.Instance;
            if (mgr == null)
            {
                const string failMessage = "건설 관리자 미존재 — 배치 불가";
                Debug.LogWarning("[RestaurantPlacementMarker] ConstructionManager 미존재 — 배치 실패");
                PlacementAttempted?.Invoke(false, failMessage);
                return;
            }

            // [88차] territoryId 조립 — 마을→영지 1:1(villageIndex 0~19 ↔ 영지 1~20),
            //        TestTerritoryCombatSetup 실측 조립(new TerritoryId(nation, index + 1))과 동일.
            string territoryId = new TerritoryId(_nearVillage.nation, _nearVillage.index + 1).ToString();
            string message = mgr.TryPlace(territoryId, RestaurantPropBuilder.RestaurantBlueprintId, spotPos);
            // TryPlace 계약 — 성공 메시지는 "건설 시작! ..." 고정, 실패는 사유 문구(골드/재료/허가세/겹침)
            bool success = !string.IsNullOrEmpty(message)
                && message.StartsWith("건설 시작!", System.StringComparison.Ordinal);

            PlacementAttempted?.Invoke(success, message ?? string.Empty);
            if (PlacementAttempted == null)
                Debug.Log($"[RestaurantPlacementMarker] 배치 시도(무구독 로그): success={success}, message={message}");

            if (success)
            {
                // 건설 시작 → 스팟 점유 전환 — 즉시 숨김(다음 스캔에서 점유 판정 확정)
                _validSpots.Clear();
                SetRingsActive(0);
                if (_promptRegistered)
                {
                    EInteractionPromptBridge.RaiseUnregister(GetPromptAnchor().transform);
                    _promptRegistered = false;
                }
            }
        }

        // ===== 링 VFX 풀 (파괴 아님 — 재사용) =====

        /// <summary>링 풀 가시 전환 — 필요 수만큼 지연 생성, 위치 갱신, 잔여는 비활성.</summary>
        private void SetRingsActive(int count)
        {
            // [142차 A] 골드 배치 스팟 링 폐기(사용자 지시 — "음식점이 지어질 자리의 골든링 제거").
            // 배치 안내 골드 링(#E3B341, r=2.5m)이 상시 노출돼 잔존 표시로 오인됨 → 링 VFX 전면 비표출
            // (풀에 있던 링은 전부 비활성 — 생성 루프도 미동작). E키 배치 프롬프트/스캔/배치 기능은 유지.
            count = 0;
            while (_rings.Count < count)
                _rings.Add(CreateRing());

            for (int i = 0; i < _rings.Count; i++)
            {
                GameObject ring = _rings[i];
                if (ring == null)
                {
                    // 씬 언로드 등으로 파괴된 경우 — 풀 슬롯 재생성(방어)
                    _rings[i] = CreateRing();
                    ring = _rings[i];
                }
                bool active = i < count;
                if (ring.activeSelf != active)
                    ring.SetActive(active);
                if (active)
                    ring.transform.position = _validSpots[i] + Vector3.up * RingGroundLift;
            }
        }

        /// <summary>
        /// [88차] 배치 링 1개 생성 — SelectionRingController(84/85차 수제 링, colliderless 지면 데칼) +
        /// CreateRingMaterial 골드 앰버 머티리얼. 반경 = footprint(2.5m) 일치.
        /// </summary>
        private GameObject CreateRing()
        {
            var go = new GameObject(RingName);
            // [88차] 링 스케일 — 컨트롤러 쿼드는 Figma 400:120 타원형(계약)이라 원형 footprint 링을 위해
            //        역비 스케일로 등방성을 회복하고, 셰이더 링 반경 계수(ShaderRingRadius)로 가시 지름을 맞춘다.
            float aspect = CommandRingPresentation.FigmaCoreRingAspect;
            float quadDiameter = (RestaurantPlacementSpotSystem.RestaurantFootprintRadius * 2f)
                / CommandRingPresentation.ShaderRingRadius;
            go.transform.localScale = new Vector3(
                quadDiameter / Mathf.Sqrt(aspect),
                quadDiameter * Mathf.Sqrt(aspect),
                1f);
            go.transform.SetParent(transform, true);

            // [88차] 수제 링 부착 — AddComponent 시 Awake에서 쿼드/머티리얼 생성(시작 스케일 캡처)
            SelectionRingController ring = go.AddComponent<SelectionRingController>();
            ring.autoFadeOut = false; // 상시 표시 링 — 자동 소멸 없음(SelectionRingFadeTests 계약 보호)
            ring.SetColor(PlacementRingColor);

            // [88차] 골드 앰버 머티리얼 — SelectionRingController.CreateRingMaterial 정적 헬퍼 산출물을
            //        마커 수명 동안 공유 1 인스턴스로 적용(파랑 기본 틴트 교체).
            Renderer ringRenderer = go.GetComponentInChildren<Renderer>();
            if (ringRenderer != null)
                ringRenderer.sharedMaterial = GetOrCreateRingMaterial();
            return go;
        }

        /// <summary>골드 링 공유 머티리얼 지연 생성 — SelectionRingController.CreateRingMaterial 경유.</summary>
        private Material GetOrCreateRingMaterial()
        {
            if (_sharedRingMaterial == null)
                _sharedRingMaterial = SelectionRingController.CreateRingMaterial(PlacementRingColor);
            return _sharedRingMaterial;
        }

        /// <summary>스팟 숨김 — 링 전체 비활성 + 프롬프트 해제(유효 스팟 캐시 유지 여부는 호출부 결정).</summary>
        private void HideSpots()
        {
            SetRingsActive(0);
            if (_promptRegistered && _promptAnchor != null)
            {
                EInteractionPromptBridge.RaiseUnregister(_promptAnchor.transform);
                _promptRegistered = false;
            }
        }

        /// <summary>프롬프트 앵커 지연 생성 — 마커 자식 오브젝트(씬 직렬화 대상 아님).</summary>
        private GameObject GetPromptAnchor()
        {
            if (_promptAnchor == null)
            {
                _promptAnchor = new GameObject(PromptAnchorName);
                _promptAnchor.transform.SetParent(transform, false);
            }
            return _promptAnchor;
        }

        private void OnDisable()
        {
            // 프롬프트 잔존 방지 — 비활성화 직전 해제(RestaurantPropInteractable.OnDisable 동일 방어)
            if (_promptAnchor != null)
                EInteractionPromptBridge.RaiseUnregister(_promptAnchor.transform);
            _promptRegistered = false;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (_sharedRingMaterial != null)
            {
                Object.Destroy(_sharedRingMaterial);
                _sharedRingMaterial = null;
            }
        }
    }
}
