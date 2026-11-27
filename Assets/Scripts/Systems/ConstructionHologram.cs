using System.Collections.Generic;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [IMPL-A 104차 F1] 건설 홀로그램 배치 시스템.
    /// Handles the ghost root placement flow:
    /// - BeginPlacement(blueprintId) -> spawns primitive hologram based on recipe.
    /// - LateUpdate: tracks XZ mouse position, snaps Y to Terrain height + 1.
    /// - Left Click: Resolves territory, attempts TryPlace. Success -> Confirmed pulse. Fail -> Toast + Following.
    /// - Esc: Cancels placement.
    /// </summary>
    public class ConstructionHologram : MonoBehaviour
    {
        public static ConstructionHologram Instance { get; private set; }
        // [105차 A] 결함1 — 게이트 = 배치 중(Following)에만. 구 판정(!= Inactive)은 Confirmed(시공 중)까지
        // 활성으로 취급해 시공 45~150초 동안 이동 명령(CommandMarker.Spawn/RTSCommandSystem)이 전면 차단됐다.
        public static bool IsPlacementActive => Instance != null && Instance._mode == PlacementMode.Following;

        /// <summary>Systems→UI 순환참조 회피용 토스트 요청 브리지 — UI(ConstructionWindowUTK)가 구독해 UTKToastService로 라우팅.</summary>
        public static event System.Action<string> ToastRequested;

        private enum PlacementMode
        {
            Inactive,
            Following,
            Confirmed
        }

        private PlacementMode _mode = PlacementMode.Inactive;
        private string _blueprintId;

        // [105차 A] 결함2 — 확정(Confirm) 성공 시점의 설계도/위치 기록. StructureCompleted 수신 시
        // 이 기록과 일치하는 구조물에만 홀로그램을 파괴한다(불일치 무시 → 시공 중 2번째 홀로그램 보호).
        private string _confirmedBlueprintId;
        private Vector3 _confirmedPos;

        // [105차 A] 결함6 — 시공 중 Esc 무시 로그 1회성 스팸 방지 플래그(배치 시작/취소 시 리셋).
        private bool _escIgnoreLogged;
        private GameObject _ghostRoot;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private float _inputCooldown = 0.25f;

        // Colors for hologram tint
        private static readonly Color ValidColor = new Color(0.35f, 0.9f, 1f);
        private static readonly Color InvalidColor = new Color(1f, 0.25f, 0.25f);

        /// <summary>
        /// Initializes the hologram placement mode. Creates a ghost root if it doesn't exist.
        /// </summary>
        public static void BeginPlacement(string blueprintId)
        {
            if (Instance == null)
            {
                var go = new GameObject("ConstructionHologram_Root");
                Instance = go.AddComponent<ConstructionHologram>();
            }

            if (Instance != null)
            {
                Instance.StartPlacementInternal(blueprintId);
            }
        }

        private void StartPlacementInternal(string blueprintId)
        {
            if (string.IsNullOrEmpty(blueprintId) || !BlueprintData.TryGet(blueprintId, out var def)) return;

            _blueprintId = blueprintId;
            _mode = PlacementMode.Following;
            _inputCooldown = 0.25f; // Prevent instant confirm from the triggering UI click
            // [105차 A] 결함2 — 새 배치 시작은 이전 확정 기록을 무효화한다(미확인 2번째 배치가 Following인
            // 상태에서 이전 구조물 완료 이벤트가 홀로그램을 파괴하는 결함 시나리오 차단).
            _confirmedBlueprintId = null;
            _escIgnoreLogged = false;

            if (_ghostRoot == null)
            {
                _ghostRoot = new GameObject("Hologram_Ghost");
                _ghostRoot.transform.SetParent(transform, false);
            }
            _ghostRoot.SetActive(true);

            // Clear previous ghosts
            foreach (Transform child in _ghostRoot.transform)
            {
                Destroy(child.gameObject);
            }
            _renderers.Clear();

            // Build recipe based on effectId/id (Simulated 5-recipe mapping)
            BuildHologramPrimitives(def);

            foreach (var r in _renderers)
            {
                SetHologramMaterial(r);
            }

            // [105차 A] 결함4 — 고스트 콜라이더 전부 비활성화(Following/Confirmed 공통, 생성 직후 1회면 충분).
            DisableGhostColliders();
        }

        private void BuildHologramPrimitives(BlueprintData.BlueprintDef def)
        {
            // [170차 P1] 실물 형태 패리티 — 시각 소스가 있는 설계도는 실물 빌더의 시각 파트를 그대로 공유한다
            // (단일 소스 계약 — 실물 형태 변경이 홀로그램에 자동 반영). 색은 기존 홀로그램 계약(유효/무효 틴트)이 덮는다.
            GameObject shape = null;
            if (def.id == RestaurantPropBuilder.RestaurantBlueprintId)
            {
                shape = RestaurantPropBuilder.BuildHologramVisual(def.id, NationType.East);   // 형태는 nation 무관 — 홀로그램 틴트가 덮임
            }
            else if (FacilityPropBuilder.IsFacilityBlueprintId(def.id))
            {
                shape = FacilityPropBuilder.BuildHologramVisual(def.id, NationType.East);
            }

            if (shape != null)
            {
                shape.name = "Hologram_Shape";
                shape.transform.SetParent(_ghostRoot.transform, false);
                foreach (var r in shape.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null) _renderers.Add(r);
                }

                // 부지 표시 — 기존 footprint 바닥판 유지 계약([170차 확인2] 사용자 기본값 확정).
                var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseGo.name = "Hologram_Footprint";
                baseGo.transform.SetParent(_ghostRoot.transform, false);
                baseGo.transform.localScale = new Vector3(def.footprintRadius * 2f, 0.2f, def.footprintRadius * 2f);
                var br = baseGo.GetComponent<Renderer>();
                if (br != null) _renderers.Add(br);

                // [203차] 프로젝션 레이 VFX — shape 성공 경로 전용(폴백 자리표시자 경로 미적용).
                // ghostRoot 하위 소속 → 접지 이동·소멸(StructureCompleted) 자동 추종. 로드 실패 시 조용한 no-op.
                var vfxPrefab = Resources.Load<GameObject>("Holograms/HologramVFX");
                if (vfxPrefab != null)
                {
                    var vfxGo = Object.Instantiate(vfxPrefab, _ghostRoot.transform, false);
                    vfxGo.name = "Hologram_VFX";
                    vfxGo.transform.localPosition = Vector3.zero;
                    vfxGo.transform.localScale = Vector3.one * Mathf.Max(1f, def.footprintRadius / 2.5f);
                }
                return;
            }

            // [170차 P1] 시각 소스 없는 설계도(건설창 미제공 레거시 정의) — 기존 자리표시자 유지(방어 경로 계약).
            var baseGo2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGo2.name = "Hologram_Footprint";
            baseGo2.transform.SetParent(_ghostRoot.transform, false);
            baseGo2.transform.localScale = new Vector3(def.footprintRadius * 2f, 0.2f, def.footprintRadius * 2f);
            var r2 = baseGo2.GetComponent<Renderer>();
            if (r2 != null) _renderers.Add(r2);

            // Constructing a basic vertical ghost to signify the structure
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Hologram_Pillar";
            pillar.transform.SetParent(_ghostRoot.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 1f, 0f);
            pillar.transform.localScale = new Vector3(def.footprintRadius * 0.5f, 2f, def.footprintRadius * 0.5f);
            var pr = pillar.GetComponent<Renderer>();
            if (pr != null) _renderers.Add(pr);
        }

        /// <summary>
        /// [105차 A] 결함4 — 고스트 프리미티브(GameObject.CreatePrimitive 자동 콜라이더 포함)의
        /// 콜라이더를 전부 비활성화한다. 방치 시 LateUpdate 지면 레이캐스트가 자기 고스트에 히트해
        /// 추적 지터가 발생하고, Confirmed 고스트 콜라이더가 마우스 클릭을 가로챈다.
        /// </summary>
        private void DisableGhostColliders()
        {
            if (_ghostRoot == null) return;
            foreach (var col in _ghostRoot.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        // [203차] 홀로그램 에셋 틴트 알파 상수 — 반투명 queue 3000 계약과 짝을 이루는 투명도 핀.
        private const float HologramTintAlpha = 0.35f;
        private const float HologramTextureAlpha = 0.2f;

        /// <summary>
        /// [203차] 사용자 임포트 "Unity Hologram VFX" 에셋 머티리얼 통합 — 3단 폴백 체인:
        /// 1차 Resources("Holograms/ConstructionHologram_Mat") 인스턴스 → 2차 Shader.Find 신규 생성
        /// → 3차 null 반환(호출자가 기존 URP Lit 경로 유지). 모든 속성 접근은 HasProperty 가드
        /// (셰이더 미발견 시 서브그래프 속성 부재). renderQueue 3000 = 기존 반투명 계약 유지.
        /// </summary>
        private static Material CreateHologramAssetMaterial(Color tint)
        {
            var loaded = Resources.Load<Material>("Holograms/ConstructionHologram_Mat");
            Material mat = loaded != null ? Object.Instantiate(loaded) : null;
            if (mat == null)
            {
                var shader = Shader.Find("Shader Graphs/Template_Hologram");
                if (shader != null) mat = new Material(shader);
            }
            if (mat == null) return null; // 3차 폴백 — 호출자가 기존 URP Lit 경로 유지

            if (mat.HasProperty("_Hologram_Color"))
                mat.SetColor("_Hologram_Color", new Color(tint.r, tint.g, tint.b, HologramTintAlpha));
            if (mat.HasProperty("_Texture_Tint_Color"))
                mat.SetColor("_Texture_Tint_Color", new Color(tint.r, tint.g, tint.b, HologramTextureAlpha));
            if (mat.HasProperty("_Fresnel_Power")) mat.SetFloat("_Fresnel_Power", 1.5f);
            if (mat.HasProperty("_Emission_Scale")) mat.SetFloat("_Emission_Scale", 2f);
            if (mat.HasProperty("_Pattern_Density")) mat.SetFloat("_Pattern_Density", 250f);
            mat.renderQueue = 3000; // 기존 반투명 queue 계약 유지
            return mat;
        }

        private void SetHologramMaterial(Renderer renderer)
        {
            // [203차] 에셋 머티리얼 우선 — 실패(null) 시 기존 URP Lit 반투명 경로 유지(폴백 계약).
            var assetMat = CreateHologramAssetMaterial(ValidColor);
            if (assetMat != null)
            {
                renderer.sharedMaterial = assetMat; // 렌더러 전용 인스턴스 — 로그/파괴 처리 불필요
                return;
            }

            var mat = MaterialHelper.CreateLitMaterial(ValidColor, "Hologram_Mat");
            if (mat != null)
            {
                // Configure URP Lit for transparent hologram look
                if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1);
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1); // Alpha blending
                mat.renderQueue = 3000;
                renderer.sharedMaterial = mat;
            }
        }

        private void LateUpdate()
        {
            if (_mode != PlacementMode.Following) return;

            // Raycast from camera to ground
            var cam = Camera.main;
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            // [106차 B 수리] 결함1 — 전역 무마스크 레이캐스트(Physics.Raycast(ray, hit, 1000f))는 커서가
            // 건물/플레이어/NPC 위를 지날 때 그 콜라이더 표면으로 마커를 순간이동시켰다(마을씬 테스트 15
            // 영상 43~44s 실측). 마을씬 물리 레이어는 Default(0) 단일 체계(VillageNpcSpawner.GroundSnapMask
            // 동일 근거)라 마스크로 지면/건물 분리 불가 → RaycastAll 후 '지면류'(Ground/Terrain) 히트를
            // 선호하고, 지면류가 없으면(커서가 건물/플레이어 위) 마커를 현재 위치에 유지한다(점프 방지).
            // 결함2 — Y 하드코딩(Plains+42) 폴백: 지면류 히트가 절차 지형(Terrain)이면 기존 GetHeightAt
            // 경로를 유지하고, 메시 지면(Ground_Inner 등)이면 히트 지표면 + 1f 오프셋으로 정확 접지.
            var hits = Physics.RaycastAll(ray, 1000f, VillageNpcSpawner.GroundSnapMask, QueryTriggerInteraction.Ignore);
            RaycastHit groundHit = default;
            bool hasGroundHit = false;
            foreach (var h in hits)
            {
                if (_ghostRoot != null && h.transform.IsChildOf(_ghostRoot.transform)) continue;
                string n = h.transform.name;
                if (n.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!hasGroundHit || h.distance < groundHit.distance) { groundHit = h; hasGroundHit = true; }
            }

            if (hasGroundHit)
            {
                float x = groundHit.point.x;
                float z = groundHit.point.z;
                float y = groundHit.transform.name.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42) + 1f   // 절차 지형 — 기존 GetHeightAt 계약 경로
                    : groundHit.point.y + 1f;                                        // 메시 지면 — 히트 지표면 정확 접지(부유/침하 수리)

                if (_ghostRoot != null)
                {
                    _ghostRoot.transform.position = new Vector3(x, y, z);
                }

                // Validate placement
                bool isValid = ValidatePlacementAt(new Vector3(x, y, z));
                UpdateTint(isValid);
            }
            // [106차 B] 지면류 히트 없음(건물/NPC/하늘 위) — 마커 현재 위치 유지(순간이동 차단), 틴트 유지.
        }

        private bool ValidatePlacementAt(Vector3 pos)
        {
            // Pure logic execution using the established ProjectName patterns
            var db = TerritoryDatabase.Instance;
            if (db == null) return false;

            var resolved = db.ResolveTerritoryAt(pos);
            if (resolved == null) return false;
            string territoryId = resolved.Value.ToString();

            var region = db.GetDefinition(territoryId);
            if (region.nation == NationType.None) return false;

            var existing = new List<(Vector3 pos, float radius)>();
            var mgr = ConstructionManager.Instance;
            if (mgr != null)
            {
                foreach (var entry in mgr.Structures)
                {
                    if (BlueprintData.TryGet(entry.blueprintId, out var eDef))
                        existing.Add((entry.pos, eDef.footprintRadius));
                }
            }

            if (!BlueprintData.TryGet(_blueprintId, out var def)) return false;
            return BlueprintData.ValidatePlacement(region.worldPosition, ConstructionManager.TerritoryRadius, pos, def.footprintRadius, existing);
        }

        private void UpdateTint(bool isValid)
        {
            Color target = isValid ? ValidColor : InvalidColor;
            foreach (var r in _renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                // [203차] 에셋 머티리얼 우선 — 홀로그램 셰이더 속성을 직접 드라이브(자산 속성 우선 계약).
                if (r.sharedMaterial.HasProperty("_Hologram_Color"))
                {
                    r.sharedMaterial.SetColor("_Hologram_Color", new Color(target.r, target.g, target.b, HologramTintAlpha));
                    r.sharedMaterial.SetColor("_Texture_Tint_Color", new Color(target.r, target.g, target.b, HologramTextureAlpha));
                    continue;
                }
                if (r.sharedMaterial.HasProperty("_BaseColor")) r.sharedMaterial.SetColor("_BaseColor", target);
                else if (r.sharedMaterial.HasProperty("_Color")) r.sharedMaterial.SetColor("_Color", target);
            }
        }

        private void Update()
        {
            if (_mode == PlacementMode.Inactive) return;

            if (_inputCooldown > 0f)
            {
                _inputCooldown -= Time.deltaTime;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // [105차 A] 결함6 — Esc 취소는 배치 중(Following)에서만 수행한다. Confirmed(시공 중)
                // 취소는 고스트 소멸 → 완료 전 실물 부재 창을 유발하므로 무시(로그 1회성 — 매 프레임 스팸 금지).
                if (_mode == PlacementMode.Following)
                {
                    CancelPlacement();
                    return;
                }
                if (!_escIgnoreLogged)
                {
                    Debug.Log("[105차 A] 시공 중 — Esc 무시(고스트 유지)");
                    _escIgnoreLogged = true;
                }
            }

            // Left click to confirm
            if (Input.GetMouseButtonDown(0) && _mode == PlacementMode.Following && _inputCooldown <= 0f)
            {
                ConfirmPlacement();
            }
        }

        private void ConfirmPlacement()
        {
            if (_ghostRoot == null) return;

            var pos = _ghostRoot.transform.position;
            bool confirmed = TryConfirmPlacement(pos, out string placeResult);

            if (confirmed)
            {
                _mode = PlacementMode.Confirmed;
                // [105차 A] 결함2 — 확정 성공 시점의 설계도/위치 기록. StructureCompleted 수신 시 이
                // 기록과 일치하는 구조물에만 파괴한다(ε 0.5m — TryPlace 등록 pos와 동일 값 전달).
                _confirmedBlueprintId = _blueprintId;
                _confirmedPos = pos;
                // [IMPL-A 104차 F1] 성공 = Confirmed 펄스 + StructureCompleted 파괴.
                // 홀로그램은 시공 완료(StructureCompleted) 시점까지 잔존하며 완성 이벤트 수신 시 소멸한다.
                // 구독은 ConfirmPlacement 성공 시 1회 유지(-= 후 += 기존 패턴 — 중복 구독 없음).
                ConstructionManager.StructureCompleted -= OnStructureCompletedForDestroy;
                ConstructionManager.StructureCompleted += OnStructureCompletedForDestroy;
            }
            else
            {
                // [105차 A] 결함3 — 실패 = 실제 사유 토스트(UI 브리지) + Following 유지 (재배치 시도 허용).
                // 구 고정 문구는 폐기(사유 비노출 결함) — 실제 사유는 out result로 전달된다.
                ToastRequested?.Invoke(placeResult);
                Debug.Log($"[ConstructionHologram] 배치 실패 — Following 유지 (사유: {placeResult})");
            }
        }

        /// <summary>시공 완료 이벤트 수신 — 확정 기록과 일치하는 구조물에만 홀로그램 소멸(실물 시각으로 교체됨).</summary>
        private void OnStructureCompletedForDestroy(ConstructionManager.StructureEntry entry)
        {
            // [105차 A] 결함2 — 불일치 완료 이벤트는 무시한다(구 코드: entry 무시·무조건 DestroyHologram →
            // 시공 중 2번째 홀로그램이 1번째 완료 시점에 소멸). 기록 미설정(null)도 불일치 취급 — 안전 무시.
            if (string.IsNullOrEmpty(_confirmedBlueprintId) ||
                entry.blueprintId != _confirmedBlueprintId ||
                (entry.pos - _confirmedPos).magnitude > 0.5f)
            {
                Debug.Log($"[105차 A] 완료 이벤트 불일치 무시 — entry={entry.blueprintId} @ ({entry.pos.x:F1},{entry.pos.z:F1}) / 확정 기록={_confirmedBlueprintId ?? "(없음)"} @ ({_confirmedPos.x:F1},{_confirmedPos.z:F1})");
                return;
            }
            DestroyHologram();
        }

        private void DestroyHologram()
        {
            ConstructionManager.StructureCompleted -= OnStructureCompletedForDestroy;
            ClearGhost();
            Destroy(gameObject);
        }

        /// <summary>
        /// 배치 확정 시도 — TryPlace 결과를 그대로 전달한다.
        /// [105차 A] 결함3 — out result로 실제 사유("이미 음식점이 있는 마을" / "마을 반경 밖
        /// (마을 중심 반경+5m 내에서만 건설 가능)" / "재료 부족" 등)를 반환한다. 성공 반환은
        /// "건설 시작! ..." 형식(ConstructionManager.TryPlace 실측) — StartsWith 판정 유지.
        /// </summary>
        private bool TryConfirmPlacement(Vector3 pos, out string result)
        {
            result = null;
            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                result = "영지 없음";
                return false;
            }

            var resolved = db.ResolveTerritoryAt(pos);
            if (resolved == null)
            {
                result = "영지 없음";
                return false;
            }
            string territoryId = resolved.Value.ToString();

            if (ConstructionManager.Instance == null)
            {
                result = "건설 관리자 없음";
                return false;
            }

            string placeResult = ConstructionManager.Instance.TryPlace(territoryId, _blueprintId, pos);
            result = placeResult;
            if (!string.IsNullOrEmpty(placeResult) && placeResult.StartsWith("건설 시작"))
            {
                return true;
            }
            return false;
        }

        public void CancelPlacement()
        {
            ClearGhost();
            _mode = PlacementMode.Inactive;
            _blueprintId = null;
            _ghostRoot = null;
        }

        private void ClearGhost()
        {
            if (_ghostRoot != null)
            {
                Destroy(_ghostRoot);
                _ghostRoot = null;
            }
            _renderers.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
