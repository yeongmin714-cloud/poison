using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-16: 영토 차지 효과 시스템 — 영지 점령 시 시각적 효과를 처리합니다.
    /// 
    /// 제공 효과:
    ///   a) 국기 교체: 영지 중심에 플레이어 색상의 깃발 표시
    ///   b) 영지 경계선 색상 변경: 지도에서 영지 테두리 색상 변경
    ///   c) 파티클 효과: 점령 시 컬러 큐브 플래시 효과 (Placeholder)
    /// 
    /// 사용법:
    ///   TerritoryCaptureSystem.OnTerritoryCaptured(territoryId, newOwner);
    ///   TerritoryCaptureSystem.UpdateTerritoryVisuals(territoryId);
    /// </summary>
    public static class TerritoryCaptureSystem
    {
        // ===== 이벤트 =====

        /// <summary>영지 시각적 업데이트가 완료되었을 때 발생 (territoryId)</summary>
        public static event System.Action<TerritoryId> OnVisualsUpdated;

        // ===== 데이터 =====

        /// <summary>영지 깃발 데이터</summary>
        public struct TerritoryFlag
        {
            /// <summary>영지 ID</summary>
            public TerritoryId territoryId;
            /// <summary>깃발 색상</summary>
            public Color flagColor;
            /// <summary>깃발 위치 (영지 중심)</summary>
            public Vector3 flagPosition;
            /// <summary>깃발 GameObject 참조</summary>
            public GameObject flagObject;
        }

        // ===== 상수 =====

        /// <summary>깃발 큐브 크기</summary>
        public const float FLAG_CUBE_SIZE = 0.8f;

        /// <summary>깃발 높이 (지면 위)</summary>
        public const float FLAG_HEIGHT = 2.5f;

        /// <summary>깃발 폴 높이</summary>
        public const float FLAG_POLE_HEIGHT = 3.0f;

        /// <summary>파티클 효과 지속 시간 (초)</summary>
        public const float PARTICLE_DURATION = 1.5f;

        /// <summary>파티클 큐브 크기</summary>
        public const float PARTICLE_CUBE_SIZE = 0.5f;

        /// <summary>파티클 큐브 개수</summary>
        public const int PARTICLE_CUBE_COUNT = 8;

        /// <summary>경계선 색상 알파</summary>
        public const float BORDER_ALPHA = 0.6f;

        // ===== 내부 상태 =====

        private static readonly Dictionary<TerritoryId, TerritoryFlag> _flags = new Dictionary<TerritoryId, TerritoryFlag>();
        private static readonly Dictionary<TerritoryId, Color> _borderColors = new Dictionary<TerritoryId, Color>();
        private static readonly List<GameObject> _activeParticles = new List<GameObject>();

        /// <summary>캐시된 Cube 메시 (Resources.GetBuiltinResource 호출 최소화)</summary>
        private static Mesh _cubeMesh;

        /// <summary>모든 깃발 데이터 (읽기 전용 복사본)</summary>
        public static IReadOnlyDictionary<TerritoryId, TerritoryFlag> Flags => _flags;

        /// <summary>모든 경계선 색상 (읽기 전용 복사본)</summary>
        public static IReadOnlyDictionary<TerritoryId, Color> BorderColors => _borderColors;

        // ===== 메인 퍼블릭 메서드 =====

        /// <summary>
        /// 영지가 점령되었을 때 호출합니다. 깃발 교체, 경계선 색상 변경, 파티클 효과를 실행합니다.
        /// LordSurrenderSystem.OnLordExecuted, PoisonTakeoverSystem.OnLordPoisoned,
        /// AssassinationCutscene.OnAssassinationExecuted에서 호출됩니다.
        /// </summary>
        /// <param name="territoryId">점령된 영지 ID</param>
        /// <param name="newOwner">새 소유주 (NationType)</param>
        /// <param name="isPlayerCapture">플레이어 점령 경로 여부 — true면 문장 시스템 색상 사용 (F1)</param>
        public static void OnTerritoryCaptured(TerritoryId territoryId, NationType newOwner, bool isPlayerCapture = false)
        {
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(territoryId);
            if (string.IsNullOrEmpty(def.territoryName))
            {
                Debug.LogWarning($"[TerritoryCaptureSystem] 영지 정의 없음: {territoryId}");
                return;
            }

            // F1: 색상 1회 산정 — 플레이어 점령은 문장 주색, 그 외는 국가색
            Color captureColor = isPlayerCapture ? GetPlayerFlagColor() : GetOwnerColor(newOwner);

            // [F4] 실내 경계 — 실내 씬 좌표계 오염 방지. 월드 시각 요소(깃발/파티클/야외 스테이션)는
            //   월드 복귀 시 FlushPendingVisuals로 일괄 적용. 경계색/상태는 데이터라 즉시 반영.
            if (IsIndoorScene())
            {
                SetBorderColor(territoryId, captureColor);
                var indoorState = db.GetState(territoryId);
                if (indoorState != null)
                    indoorState.flagRaised = true;
                _pendingVisuals.Add(new PendingCaptureVisual { id = territoryId, playerCapture = isPlayerCapture });
                Debug.Log($"[TerritoryCaptureSystem] 🏁 영지 점령 효과 (실내 — 월드 복귀 후 게양 예정): {def.territoryName} → {(isPlayerCapture ? "플레이어(문장색)" : newOwner.ToString())}");
                OnVisualsUpdated?.Invoke(territoryId);
                return;
            }

            // a) 깃발 교체
            SpawnFlag(territoryId, captureColor, isPlayerCapture);

            // b) 경계선 색상 변경
            SetBorderColor(territoryId, captureColor);

            // c) 파티클 효과
            SpawnCaptureParticles(territoryId, captureColor);

            // [F4] 야외 크래프트 스테이션 — 플레이어 점령 시 성 앞 즉석 제작 배치 (멱등)
            if (isPlayerCapture)
                SpawnOutdoorCraftStationIfMissing(territoryId);

            // d) TerritoryState 동기화
            var state = db.GetState(territoryId);
            if (state != null)
                state.flagRaised = true;

            Debug.Log($"[TerritoryCaptureSystem] 🏁 영지 점령 효과: {def.territoryName} → {(isPlayerCapture ? "플레이어(문장색)" : newOwner.ToString())}");

            OnVisualsUpdated?.Invoke(territoryId);
        }

        // ===== [F4] 실내 펜딩 시각 요소 + 야외 크래프트 =====

        private struct PendingCaptureVisual
        {
            public TerritoryId id;
            public bool playerCapture;
        }

        /// <summary>실내에서 점령/상실된 영지의 월드 시각 요소 대기열 (월드 복귀 시 일괄 적용).</summary>
        private static readonly List<PendingCaptureVisual> _pendingVisuals = new List<PendingCaptureVisual>();
        /// <summary>야외 크래프트 스테이션 이미 배치된 영지 키 (멱등 가드 — TerritoryBuilder 레지스트리와 이중 보험).</summary>
        private static readonly HashSet<string> _outdoorStationsSpawned = new HashSet<string>();

        /// <summary>현재 실내 씬 여부 — IndoorSceneTransition.IsIndoor 리플렉션 조회 (Systems→UI 순환 회피).</summary>
        private static bool IsIndoorScene()
        {
            var t = System.Type.GetType("ProjectName.UI.IndoorSceneTransition, ProjectName.UI");
            if (t == null) return false;
            try
            {
                return (bool)t.GetProperty("IsIndoor")?.GetValue(null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>[F4] 야외 크래프트 스테이션 1회 배치 (영지 정의 worldPosition 기준 — 성 앞 슬롯).</summary>
        private static void SpawnOutdoorCraftStationIfMissing(TerritoryId territoryId)
        {
            string key = territoryId.ToString();
            if (_outdoorStationsSpawned.Contains(key)) return;

            var def = TerritoryDatabase.Instance.GetDefinition(territoryId);
            var station = TerritoryBuilder.SpawnOutdoorCraftStation(def.worldPosition + Vector3.forward * 4f, key);
            if (station != null)
                _outdoorStationsSpawned.Add(key);
        }

        /// <summary>
        /// [F4] 실내에서 대기 중인 시각 요소 일괄 적용 — 월드 복귀 시점(IndoorSceneTransition.FinishExit)에서 호출.
        /// 점령 항목 = 깃발+파티클+야외 스테이션, 상실 항목 = 깃발 회수+야외 스테이션 제거.
        /// </summary>
        public static void FlushPendingVisuals()
        {
            if (_pendingVisuals.Count == 0) return;

            var items = new List<PendingCaptureVisual>(_pendingVisuals);
            _pendingVisuals.Clear();

            var db = TerritoryDatabase.Instance;
            foreach (var item in items)
            {
                var def = db.GetDefinition(item.id);
                if (string.IsNullOrEmpty(def.territoryName)) continue;

                if (item.playerCapture)
                {
                    Color captureColor = GetPlayerFlagColor();
                    SpawnFlag(item.id, captureColor, true);
                    SetBorderColor(item.id, captureColor);
                    SpawnCaptureParticles(item.id, captureColor);
                    SpawnOutdoorCraftStationIfMissing(item.id);
                    var state = db.GetState(item.id);
                    if (state != null) state.flagRaised = true;
                    Debug.Log($"[TerritoryCaptureSystem] 🏁 (월드 복귀 플러시) 깃발 게양: {def.territoryName}");
                }
                else
                {
                    Color ownerColor = GetOwnerColor(def.nation);
                    SpawnFlag(item.id, ownerColor, false);
                    SetBorderColor(item.id, ownerColor);
                    TerritoryBuilder.RemoveOutdoorCraftStation(item.id.ToString());
                    _outdoorStationsSpawned.Remove(item.id.ToString());
                    var state = db.GetState(item.id);
                    if (state != null) state.flagRaised = false;
                    Debug.Log($"[TerritoryCaptureSystem] 🏴 (월드 복귀 플러시) 깃발 회수: {def.territoryName}");
                }
                OnVisualsUpdated?.Invoke(item.id);
            }
        }

        /// <summary>
        /// [F1] 플레이어 깃발 색 — 문장 시스템 주색 (국가색 하드코딩 대체).
        /// EmblemManager 부재 시 기존 East=blue 폴백 (제외가드).
        /// </summary>
        public static Color GetPlayerFlagColor()
        {
            var emblem = EmblemManager.Instance;
            if (emblem == null)
            {
                Debug.LogWarning("[TerritoryCaptureSystem] EmblemManager 부재 — East 파랑 폴백");
                return Color.blue;
            }
            return EmblemManager.GetEmblemColor(emblem.CurrentEmblem.primaryColor);
        }

        /// <summary>[F1] 플레이어 문양 유니코드 (문장 부재 시 null).</summary>
        public static string GetPlayerFlagSymbol()
        {
            var emblem = EmblemManager.Instance;
            if (emblem == null) return null;
            return EmblemManager.GetEmblemSymbol(emblem.CurrentEmblem.shape);
        }

        /// <summary>
        /// [F3] 영지 상실 — 배신/AI 재탈환으로 플레이어 소유를 잃었을 때 호출.
        /// 깃발을 원래 국가색으로 되돌리고(문양 제거) 경계색을 갱신한다. 점령 축하 파티클은 없음.
        /// </summary>
        public static void OnTerritoryLost(TerritoryId territoryId)
        {
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(territoryId);
            if (string.IsNullOrEmpty(def.territoryName))
            {
                Debug.LogWarning($"[TerritoryCaptureSystem] 영지 정의 없음: {territoryId}");
                return;
            }

            Color ownerColor = GetOwnerColor(def.nation);
            SetBorderColor(territoryId, ownerColor);

            var state = db.GetState(territoryId);
            if (state != null)
                state.flagRaised = false;

            // [F4] 실내 경계 — 깃발 회수/야외 스테이션 제거는 월드 복귀 후 일괄 적용
            if (IsIndoorScene())
            {
                _pendingVisuals.Add(new PendingCaptureVisual { id = territoryId, playerCapture = false });
                Debug.Log($"[TerritoryCaptureSystem] 🏴 영지 상실 (실내 — 월드 복귀 후 회수 예정): {def.territoryName}");
                OnVisualsUpdated?.Invoke(territoryId);
                return;
            }

            SpawnFlag(territoryId, ownerColor, isPlayerCapture: false);
            TerritoryBuilder.RemoveOutdoorCraftStation(territoryId.ToString());
            _outdoorStationsSpawned.Remove(territoryId.ToString());

            Debug.Log($"[TerritoryCaptureSystem] 🏴 영지 상실: {def.territoryName} — 깃발 {def.nation}색 회수");
            OnVisualsUpdated?.Invoke(territoryId);
        }

        /// <summary>
        /// 특정 영지의 모든 시각적 요소를 새로고침합니다.
        /// 깃발 위치 조정, 색상 재설정 등에 사용합니다.
        /// </summary>
        /// <param name="territoryId">새로고침할 영지 ID</param>
        public static void UpdateTerritoryVisuals(TerritoryId territoryId)
        {
            // 깃발 위치 업데이트
            if (_flags.TryGetValue(territoryId, out TerritoryFlag flag))
            {
                Vector3 center = GetTerritoryCenterPosition(territoryId);
                if (flag.flagObject != null)
                {
                    flag.flagObject.transform.position = center + Vector3.up * FLAG_HEIGHT;
                }
                flag.flagPosition = center;
                _flags[territoryId] = flag;
            }

            OnVisualsUpdated?.Invoke(territoryId);
        }

        /// <summary>
        /// 특정 영지의 깃발 데이터를 반환합니다. 없으면 기본값.
        /// </summary>
        public static TerritoryFlag GetFlag(TerritoryId territoryId)
        {
            if (_flags.TryGetValue(territoryId, out var flag))
                return flag;
            return default;
        }

        /// <summary>
        /// 특정 영지의 경계선 색상을 반환합니다. 없으면 기본 회색.
        /// </summary>
        public static Color GetBorderColor(TerritoryId territoryId)
        {
            if (_borderColors.TryGetValue(territoryId, out var color))
                return color;
            return Color.gray;
        }

        /// <summary>
        /// 모든 파티클 효과를 즉시 제거합니다.
        /// </summary>
        public static void ClearParticles()
        {
            foreach (var go in _activeParticles)
            {
                if (go != null)
                {
                    // [QA 수리] 편집 모드 Destroy 금지 에러 방지 — ResetAll 경유로 테스트에서도 호출됨
                    if (Application.isPlaying) Object.Destroy(go);
                    else Object.DestroyImmediate(go);
                }
            }
            _activeParticles.Clear();
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용)
        /// </summary>
        public static void ResetAll()
        {
            // 깃발 제거
            foreach (var kvp in _flags)
            {
                if (kvp.Value.flagObject != null)
                    Object.DestroyImmediate(kvp.Value.flagObject);
            }
            _flags.Clear();
            _borderColors.Clear();
            ClearParticles();
        }

        // ===== 내부 메서드 =====

        /// <summary>
        /// 캐시된 큐브 메시 반환 (Resources.GetBuiltinResource 최소화)
        /// </summary>
        private static Mesh GetCubeMesh()
        {
            if (_cubeMesh == null)
                _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return _cubeMesh;
        }

        /// <summary>
        /// 영지 중심에 깃발을 생성합니다.
        /// </summary>
        /// <param name="territoryId">영지 ID</param>
        /// <param name="color">깃발 색상</param>
        /// <param name="isPlayerCapture">플레이어 점령 여부 — true면 문양 이름표 부착 (F1)</param>
        private static void SpawnFlag(TerritoryId territoryId, Color color, bool isPlayerCapture = false)
        {
            // 기존 깃발 제거 — [QA 수리] 편집 모드 Destroy 금지 에러 방지(ResetAll DestroyImmediate 선례)
            if (_flags.TryGetValue(territoryId, out TerritoryFlag existing) && existing.flagObject != null)
            {
                if (Application.isPlaying) Object.Destroy(existing.flagObject);
                else Object.DestroyImmediate(existing.flagObject);
            }

            Vector3 center = GetTerritoryCenterPosition(territoryId);

            // 깃발 GameObject 생성
            GameObject flagGo = new GameObject($"[Flag] {territoryId}");

            // 깃발 폴 (긴 큐브)
            GameObject poleGo = new GameObject("Pole");
            poleGo.transform.SetParent(flagGo.transform);
            poleGo.transform.localPosition = Vector3.zero;
            var poleRenderer = poleGo.AddComponent<MeshRenderer>();
            var poleFilter = poleGo.AddComponent<MeshFilter>();
            poleFilter.sharedMesh = GetCubeMesh();
            // [QA 수리] 편집 모드 "renderer.material 인스턴스화" 에러 + 플래그별 소재 누수 제거 —
            // TerritoryBuilder.TrySpawnModelOrPlaceholder 선례(CreateLitMaterial) 패턴으로 통일.
            poleRenderer.material = MaterialHelper.CreateLitMaterial(Color.gray, $"{flagGo.name}_Pole");
            poleGo.transform.localScale = new Vector3(0.1f, FLAG_POLE_HEIGHT, 0.1f);

            // 깃발 (색상 큐브)
            GameObject flagCubeGo = new GameObject("FlagCube");
            flagCubeGo.transform.SetParent(flagGo.transform);
            flagCubeGo.transform.localPosition = new Vector3(0f, FLAG_POLE_HEIGHT, 0f);
            var flagRenderer = flagCubeGo.AddComponent<MeshRenderer>();
            var flagFilter = flagCubeGo.AddComponent<MeshFilter>();
            flagFilter.sharedMesh = GetCubeMesh();
            flagRenderer.material = MaterialHelper.CreateLitMaterial(color, $"{flagGo.name}_Flag");
            flagCubeGo.transform.localScale = new Vector3(FLAG_CUBE_SIZE, FLAG_CUBE_SIZE * 0.6f, 0.1f);

            // 깃발 위치 설정
            flagGo.transform.position = center + Vector3.up * FLAG_HEIGHT;

            // [F1] 플레이어 문양 이름표 — 접근 시 문양 식별
            // (NameplateDisplay OnGUI 이모지 선례: 영주 👑 이름표 — TMP 미지원 이모지의 검증된 렌더 경로)
            string symbol = isPlayerCapture ? GetPlayerFlagSymbol() : null;
            if (!string.IsNullOrEmpty(symbol))
            {
                var plate = flagGo.AddComponent<NameplateDisplay>();
                plate.DisplayName = symbol;
            }

            // 데이터 저장
            _flags[territoryId] = new TerritoryFlag
            {
                territoryId = territoryId,
                flagColor = color,
                flagPosition = center,
                flagObject = flagGo
            };

            Debug.Log($"[TerritoryCaptureSystem] 🚩 깃발 생성: {territoryId} 색상:{color}{(symbol != null ? $" 문양:{symbol}" : "")}");
        }

        /// <summary>
        /// 영지 경계선 색상을 설정합니다.
        /// </summary>
        private static void SetBorderColor(TerritoryId territoryId, Color color)
        {
            Color borderColor = color;
            borderColor.a = BORDER_ALPHA;
            _borderColors[territoryId] = borderColor;

            Debug.Log($"[TerritoryCaptureSystem] 🎨 경계선 색상 변경: {territoryId} → {borderColor}");
        }

        /// <summary>
        /// 점령 파티클 효과를 생성합니다. (Placeholder: 컬러 큐브들이 퍼져나가는 효과)
        /// </summary>
        private static void SpawnCaptureParticles(TerritoryId territoryId, Color color)
        {
            Vector3 center = GetTerritoryCenterPosition(territoryId);

            for (int i = 0; i < PARTICLE_CUBE_COUNT; i++)
            {
                GameObject particleGo = new GameObject($"[CaptureParticle] {territoryId}_{i}");
                particleGo.transform.position = center + Vector3.up * 1f;

                var renderer = particleGo.AddComponent<MeshRenderer>();
                var filter = particleGo.AddComponent<MeshFilter>();
                filter.sharedMesh = GetCubeMesh();
                // [QA 수리] 편집 모드 소재 인스턴스화 에러 방지 — SpawnFlag 동일 패턴
                renderer.material = MaterialHelper.CreateLitMaterial(color, particleGo.name + "_Mat");

                particleGo.transform.localScale = Vector3.one * PARTICLE_CUBE_SIZE;

                // 랜덤 방향으로 초기 속도 설정 (Rigidbody 없이 간단한 이동)
                Vector3 randomDir = Random.onUnitSphere;
                randomDir.y = Mathf.Abs(randomDir.y); // 위쪽으로만
                float speed = Random.Range(3f, 6f);

                // ParticleMover 컴포넌트 추가
                var mover = particleGo.AddComponent<ParticleMover>();
                mover.Initialize(randomDir * speed, PARTICLE_DURATION);

                _activeParticles.Add(particleGo);
            }

            Debug.Log($"[TerritoryCaptureSystem] ✨ 파티클 효과 생성: {territoryId} ({PARTICLE_CUBE_COUNT}개)");
        }

        /// <summary>
        /// 영지 중심 위치를 반환합니다. TerritoryManager 또는 건물 위치 기반.
        /// 주의: TerritoryManager가 없거나 현재 영지와 다른 경우,
        /// 모든 BuildingPlaceholder의 평균 위치를 사용하므로 정확하지 않을 수 있습니다.
        /// </summary>
        private static Vector3 GetTerritoryCenterPosition(TerritoryId territoryId)
        {
            // TerritoryManager 인스턴스가 있으면 사용
            if (TerritoryManager.Instance != null)
            {
                // 현재 영지와 일치하면 TerritoryManager의 중심 사용
                if (TerritoryManager.Instance.CurrentTerritoryId.Equals(territoryId))
                {
                    return TerritoryManager.Instance.GetTerritoryCenter();
                }
            }

            // 건물 기반 위치 찾기 — 현재 영지가 아닌 경우 부정확할 수 있음
            Debug.LogWarning($"[TerritoryCaptureSystem] TerritoryManager에 {territoryId} 없음, " +
                             $"모든 건물 평균 위치로 폴백 (부정확할 수 있음)");
            var buildings = Object.FindObjectsByType<BuildingPlaceholder>();
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var b in buildings)
            {
                // 간단히: 모든 건물의 평균 (영지 구분은 생략)
                sum += b.transform.position;
                count++;
            }

            if (count > 0)
                return sum / count;

            // 기본 위치
            return new Vector3(0f, 0f, 5f);
        }

        /// <summary>
        /// 소유자(NationType)에 따른 색상 반환
        /// </summary>
        private static Color GetOwnerColor(NationType owner)
        {
            switch (owner)
            {
                case NationType.East: return Color.blue;
                case NationType.West: return Color.green;
                case NationType.South: return Color.red;
                case NationType.North: return new Color(0.5f, 0f, 0.5f); // 보라
                case NationType.Empire: return new Color(0.9f, 0.7f, 0f); // 황금
                case NationType.Dracula: return new Color(0.8f, 0f, 0f); // 진한 빨강
                case NationType.None:
                default: return Color.white;
            }
        }

        /// <summary>
        /// 파티클 이동을 위한 내부 MonoBehaviour.
        /// </summary>
        private class ParticleMover : MonoBehaviour
        {
            private Vector3 _velocity;
            private float _lifetime;
            private float _elapsed;
            private MeshRenderer _cachedRenderer;

            public void Initialize(Vector3 velocity, float lifetime)
            {
                _velocity = velocity;
                _lifetime = lifetime;
                _elapsed = 0f;
                _cachedRenderer = GetComponent<MeshRenderer>();
            }

            private void OnDestroy()
            {
                // _activeParticles에서 자신 제거 (메모리 누수 방지)
                _activeParticles.Remove(gameObject);
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                if (_elapsed >= _lifetime)
                {
                    Destroy(gameObject);
                    return;
                }

                // 속도 감소 (마찰)
                _velocity *= 0.97f;
                transform.position += _velocity * Time.deltaTime;

                // 회전
                transform.Rotate(Vector3.one * 180f * Time.deltaTime);

                // 페이드 아웃 (캐시된 Renderer 사용)
                if (_cachedRenderer != null)
                {
                    float alpha = 1f - (_elapsed / _lifetime);
                    Color c = _cachedRenderer.material.color;
                    c.a = alpha;
                    _cachedRenderer.material.color = c;
                }
            }
        }
    }
}