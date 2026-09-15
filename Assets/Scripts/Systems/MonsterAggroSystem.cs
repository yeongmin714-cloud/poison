using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 몬스터 어그로 합세 시스템 (Monster Aggro).
    /// 싱글톤. 같은 종류의 몬스터가 10m 내에서 공격당하는 것을 보면 합세.
    /// 
    /// 사용법:
    ///   IAggroable monster = ...;
    ///   MonsterAggroSystem.Instance.RegisterMonster(monster);
    ///   MonsterAggroSystem.Instance.NotifyAttack(attackedGameObject, attackerGameObject);
    /// </summary>
    public class MonsterAggroSystem : MonoBehaviour
    {
        public const float AGGRO_RANGE = 10f;
        private const float AGGRO_RANGE_SQR = AGGRO_RANGE * AGGRO_RANGE; // 100f

        // [Phase A] 어그로 시각화 설정
        private static Material _aggroMaterial;       // 붉은 오라 머티리얼
        private static GameObject _exclamationPrefab; // 느낌표 프리팹
        private static bool _visualizationInitialized;

        private static MonsterAggroSystem _instance;
        public static MonsterAggroSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MonsterAggroSystem");
                    _instance = go.AddComponent<MonsterAggroSystem>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>등록된 모든 몬스터 (IAggroable → GameObject 매핑)</summary>
        private readonly Dictionary<IAggroable, GameObject> _monsterMap = new Dictionary<IAggroable, GameObject>();

        /// <summary>Update에서 재사용할 제거 목록 캐시 (GC 부하 방지)</summary>
        private readonly List<IAggroable> _toRemoveCache = new List<IAggroable>();

        /// <summary>등록된 모든 IAggroable 목록</summary>
        public IReadOnlyCollection<IAggroable> AllMonsters => _monsterMap.Keys;

        /// <summary>등록된 몬스터 수</summary>
        public int MonsterCount => _monsterMap.Count;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _showDebugUI = false;

            // [Phase A] 어그로 시각화 초기화
            InitializeVisualization();
        }

        /// <summary>몬스터 등록</summary>
        public void RegisterMonster(IAggroable monster)
        {
            if (monster == null) return;
            var mb = monster as MonoBehaviour;
            if (mb == null) return;
            if (!_monsterMap.ContainsKey(monster))
            {
                _monsterMap[monster] = mb.gameObject;
            }
        }

        /// <summary>몬스터 등록 해제</summary>
        public void UnregisterMonster(IAggroable monster)
        {
            // [Phase A] 어그로 시각화 제거
            HideAggroVisual(monster);
            if (monster != null)
                _monsterMap.Remove(monster);
        }

        /// <summary>
        /// 공격 통보. attackedMonster가 공격당했음을 시스템에 알림.
        /// 주변 10m 이내 같은 종류의 몬스터를 찾아 합세시킴.
        /// </summary>
        public void NotifyAttack(GameObject attackedMonster, GameObject attacker)
        {
            if (attackedMonster == null || attacker == null) return;

            // 공격받은 몬스터의 IAggroable 한 번만 조회
            var attackedAggro = attackedMonster.GetComponent<IAggroable>();
            if (attackedAggro == null) return;

            Vector3 attackPos = attackedMonster.transform.position;
            string attackedType = attackedAggro.MonsterType;
            if (attackedType == null) return;

            // 공격받은 몬스터 자신도 어그로 설정 (전투 중이 아니거나 쿨다운 중일 때만)
            if (!attackedAggro.IsInCombat)
            {
                attackedAggro.SetAggroTarget(attacker);
                // [Phase A] 어그로 시각화 표시
                ShowAggroVisual(attackedAggro, AggroState.Alert);
            }

            // 주변 같은 종류 몬스터 탐색 (제곱 거리 비교로 sqrt 절약)
            float sqrRange = AGGRO_RANGE_SQR;
            foreach (var kvp in _monsterMap)
            {
                var monster = kvp.Key;
                var go = kvp.Value;

                // 자기 자신 or null 체크
                if (go == null || go == attackedMonster) continue;
                if (monster.MonsterType != attackedType) continue;
                if (monster.IsInCombat) continue; // 이미 전투 중이면 스킵

                float sqrDist = (go.transform.position - attackPos).sqrMagnitude;
                if (sqrDist <= sqrRange)
                {
                    monster.SetAggroTarget(attacker);
                    // [Phase A] 어그로 시각화 표시
                    ShowAggroVisual(monster, AggroState.Alert);
                }
            }
        }

        /// <summary>
        /// GameObject에서 MonsterType 문자열을 추출.
        /// IAggroable이 없으면 null 반환.
        /// </summary>
        public static string GetMonsterType(GameObject go)
        {
            if (go == null) return null;
            var agg = go.GetComponent<IAggroable>();
            return agg?.MonsterType;
        }

        // Debug UI 토글
        private static bool _showDebugUI = false;

        private void Update()
        {
            // 각 몬스터의 어그로 타이머 업데이트
            _toRemoveCache.Clear();
            foreach (var kvp in _monsterMap)
            {
                var monster = kvp.Key;
                var go = kvp.Value;

                if (go == null)
                {
                    _toRemoveCache.Add(monster);
                    continue;
                }

                monster.UpdateAggroTimer(Time.deltaTime);

                // [Phase A] 어그로 상태 변경 시 시각화 업데이트
                var currentState = monster.CurrentAggroState;
                if (monster is MonoBehaviour mb)
                {
                    var go2 = mb.gameObject;
                    var indicator = go2.transform.Find("AggroExclamation");
                    bool hasIndicator = indicator != null;
                    bool shouldShow = currentState == AggroState.Alert || currentState == AggroState.Combat;

                    // [T2 2026-09-15] 느낌표 수명 통일 — ShowAggroVisual이 생성하는 몬스터 느낌표도
                    // transient(수명) 처리되어 '잠깐 떴다 사라진다'. 상태가 이탈하면 즉시 제거.
                    if (shouldShow && !hasIndicator)
                        ShowAggroVisual(monster, currentState);
                    else if (!shouldShow && hasIndicator)
                        HideAggroVisual(monster);
                }
            }

            foreach (var m in _toRemoveCache)
                _monsterMap.Remove(m);

            #if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F12))
            {
                _showDebugUI = !_showDebugUI;
            }
            #endif
        }

        #if UNITY_EDITOR
        private void OnGUI()
        {
            if (!_showDebugUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 600));
            GUILayout.Label("[ MonsterAggroSystem ] (F12 토글)", GUI.skin.box);
            GUILayout.Label($"Monsters: {_monsterMap.Count}");

            foreach (var kvp in _monsterMap)
            {
                var monster = kvp.Key;
                var go = kvp.Value;
                string name = go != null ? go.name : "(destroyed)";
                string type = monster.MonsterType ?? "?";
                string state = monster.CurrentAggroState.ToString();
                string target = monster.AggroTarget != null ? monster.AggroTarget.name : "none";

                GUILayout.Label($"{name} [{type}] State={state} Target={target}");
            }
            GUILayout.EndArea();
        }
        #endif

        #if UNITY_EDITOR
        /// <summary>테스트용: 싱글톤 강제 초기화</summary>
        public static void ResetInstance()
        {
            if (_instance != null)
            {
                DestroyImmediate(_instance.gameObject);
                _instance = null;
            }
        }
        #endif

        /// <summary>
        /// 특정 위치의 몬스터를 찾습니다 (거리 기반, 제곱 거리 비교).
        /// </summary>
        public IAggroable FindNearestAggroable(Vector3 position, float maxDist)
        {
            IAggroable nearest = null;
            float bestSqrDist = maxDist * maxDist;
            foreach (var kvp in _monsterMap)
            {
                if (kvp.Value == null) continue;
                float sqrDist = (kvp.Value.transform.position - position).sqrMagnitude;
                if (sqrDist <= bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    nearest = kvp.Key;
                }
            }
            return nearest;
        }

        /// <summary>
        /// 암살 발각 시 주변 모든 몬스터를 Alerted(전투) 상태로 전환.
        /// NPCAwarenessSystem과 연동하여 몬스터의 경계 상태를 강제로 설정합니다.
        /// </summary>
        /// <param name="position">발각 위치</param>
        /// <param name="radius">영향 반경</param>
        public static void SetAlertAllNearby(Vector3 position, float radius)
        {
            if (_instance == null) return;

            float sqrRadius = radius * radius;

            foreach (var kvp in _instance._monsterMap)
            {
                var go = kvp.Value;
                if (go == null) continue;

                float sqrDist = (go.transform.position - position).sqrMagnitude;
                if (sqrDist <= sqrRadius)
                {
                    var monster = kvp.Key;

                    // 이미 전투 중이면 스킵
                    if (monster.IsInCombat) continue;

                    // 플레이어 찾기
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        monster.SetAggroTarget(player);
                    }

                    // NPCAwarenessSystem이 있으면 Detected 상태로 전환
                    var awareness = go.GetComponent<NPCAwarenessSystem>();
                    if (awareness != null && awareness.IsActive)
                    {
                        GameObject target = player ?? null;
                        if (target != null)
                            awareness.SetDetected(target);
                    }
                }
            }

            Debug.Log($"[MonsterAggroSystem] SetAlertAllNearby at {position}, radius={radius}, monsters alerted");
        }

        // [Phase A] 몬스터 어그로 시각화 표시/숨김
        public static void ShowAggroVisual(IAggroable monster, AggroState state)
        {
            if (!_visualizationInitialized) InitializeVisualization();
            var mb = monster as MonoBehaviour;
            if (mb == null) return;
            var go = mb.gameObject;

            // 기존 인디케이터 정리
            HideAggroVisual(monster);

            // 어그로 상태에 따라 표시
            if (state == AggroState.Alert || state == AggroState.Combat)
            {
                // 1. 붉은 오라 — [59차 후속] MaterialPropertyBlock 기반으로 전환.
                //    기존 r.materials 배열에 _aggroMaterial(참조 비교로 제거) 방식은 Unity가 r.materials
                //    접근 시마다 인스턴스 재질을 복제해 참조가 깨져, HideAggroVisual의 m==_aggroMaterial
                //    비교가 실패 → 오라가 영구 잔존('몬스터가 빨간색에서 안 돌아옴') 의 근본 원인이었다.
                //    MPB는 렌더러 레벨에서 원본 색을 기억·복원하므로 공유/인스턴스 재질과 무관하게 항상 정리된다.
                var renderers = go.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    ApplyAggroAura(r);
                }

                // 2. 느낌표 표시 (머리 위) — [T2] 몬스터 느낌표도 잠깐 떴다 사라지게 수명 부여(ShowTransientExclamation 재사용)
                ShowTransientExclamation(go, 1.5f);
            }
        }

        // [59차 후속] 어그로 오라 적용 대상 렌더러의 원본 _BaseColor를 추적해 복원 — 참조가 아닌 색으로 안전 정리
        private static readonly System.Collections.Generic.Dictionary<Renderer, Color> _aggroAuraOrigColor =
            new System.Collections.Generic.Dictionary<Renderer, Color>();
        private static readonly int _aggroBaseColorId = Shader.PropertyToID("_BaseColor");

        private static void ApplyAggroAura(Renderer r)
        {
            if (r == null) return;
            // 원본 _BaseColor 캡처(MPB에 이미 값이 있으면 그 값, 없으면 재질의 현재 색)
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            Color orig;
            if (block.HasProperty(_aggroBaseColorId))
                orig = block.GetColor(_aggroBaseColorId);
            else if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                orig = r.sharedMaterial.GetColor("_BaseColor");
            else
                orig = Color.white;

            _aggroAuraOrigColor[r] = orig;

            // 반투명 붉은 오라 — MPB에 붉은 색 톤을 덮어 씌운다(재질 배열 비변경)
            var apply = new MaterialPropertyBlock();
            apply.SetColor(_aggroBaseColorId, new Color(1f, 0.25f, 0.25f, 1f));
            r.SetPropertyBlock(apply);
        }

        private static void RemoveAggroAura(Renderer r)
        {
            if (r == null) return;
            if (!_aggroAuraOrigColor.TryGetValue(r, out Color orig))
            {
                // 추적 없음이지만 오라가 남아 있을 가능성 — MPB를 비워 원본 재질로 복귀시킨다.
                // (렌더러에 다른 MPB가 없었다는 가정 하에 안전. HitFlash는 sharedMaterial refcount를 쓰므로 충돌 없음.)
                r.SetPropertyBlock(new MaterialPropertyBlock());
                return;
            }
            // 원본 MPB로 복원 — _BaseColor만 원본으로 되돌리고 나머지 MPB 데이터는 보존
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(_aggroBaseColorId, orig);
            r.SetPropertyBlock(block);
            _aggroAuraOrigColor.Remove(r);
        }

        public static void HideAggroVisual(IAggroable monster)
        {
            var mb = monster as MonoBehaviour;
            if (mb == null) return;
            var go = mb.gameObject;

            // 오라 제거 — [59차 후속] 추적된 렌더러별 원본 복원
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                RemoveAggroAura(r);
            }
            // 이 개체에 추적됐지만 렌더러가 파괴된 잔여 오라 레코드 정리(수명 안전)
            RemoveStaleAuraRecords(go);

            // 느낌표 제거
            var exclamations = go.GetComponentsInChildren<Transform>(true);
            foreach (var t in exclamations)
            {
                if (t.name == "AggroExclamation")
                    Destroy(t.gameObject);
            }
        }

        private static void RemoveStaleAuraRecords(GameObject go)
        {
            if (_aggroAuraOrigColor.Count == 0) return;
            var stale = new System.Collections.Generic.List<Renderer>();
            foreach (var kvp in _aggroAuraOrigColor)
            {
                if (kvp.Key == null || kvp.Key.gameObject == null || kvp.Key.gameObject == go)
                    stale.Add(kvp.Key);
            }
            foreach (var k in stale) _aggroAuraOrigColor.Remove(k);
        }

        // [Phase A] 어그로 시각화 초기화
        private static void InitializeVisualization()
        {
            if (_visualizationInitialized) return;

            // 붉은 오라 머티리얼 생성 (Unlit Transparent, 붉은 색)
            _aggroMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _aggroMaterial.SetColor("_BaseColor", new Color(1f, 0.2f, 0.2f, 0.5f)); // 반투명 붉은색
            _aggroMaterial.SetFloat("_Surface", 1f); // Transparent
            _aggroMaterial.SetFloat("_Blend", 0f); // Alpha
            _aggroMaterial.SetFloat("_SrcBlend", 5f); // SrcAlpha
            _aggroMaterial.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
            _aggroMaterial.SetFloat("_ZWrite", 0f);
            _aggroMaterial.renderQueue = 3000;

            // 느낌표 프리팹 생성 (빌보드 + TextMesh)
            _exclamationPrefab = new GameObject("AggroExclamation");
            _exclamationPrefab.hideFlags = HideFlags.DontSave;
            var textMesh = _exclamationPrefab.AddComponent<TextMesh>();
            textMesh.text = "!";
            textMesh.fontSize = 48;          // [2026-09-15 Phase F] 글리프 해상도용(표시 크기는 characterSize가 결정)
            textMesh.characterSize = 0.30f;  // [Phase F] 월드 크기 대폭 축소 — 기존 기본값 1.0 → 0.30 (느낌표 과대 리포트 수정)
            textMesh.color = Color.red;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            var billboard = _exclamationPrefab.AddComponent<Billboard>();
            _exclamationPrefab.SetActive(false);

            _visualizationInitialized = true;
        }

        // [Phase A] 빌보드 컴포넌트 (카메라를 향하게)
        private class Billboard : MonoBehaviour
        {
            private Transform _camTransform;
            private void LateUpdate()
            {
                if (_camTransform == null) _camTransform = Camera.main?.transform;
                if (_camTransform != null)
                    transform.rotation = Quaternion.LookRotation(transform.position - _camTransform.position);
            }
        }

        // ================================================================
        // [2026-09-15] transient 느낌표 — 병사/적 개체에 잠깐 떴다 사라지는 몬스터식 느낌표.
        // (계속 떠있지 않고 duration 초 후 제거. IAggroable 전용이 아닌 임의 개체에 재사용.)
        // ================================================================
        public static void ShowTransientExclamation(GameObject host, float duration = 1.2f)
        {
            if (host == null) return;
            if (!_visualizationInitialized) InitializeVisualization();
            if (_exclamationPrefab == null) return;

            var exclamation = Instantiate(_exclamationPrefab, host.transform);
            exclamation.name = "AggroExclamation";
            exclamation.transform.localPosition = new Vector3(0, 2.5f, 0); // 머리 위
            exclamation.transform.localScale = Vector3.one * 0.5f;
            exclamation.SetActive(true);

            // 수명 후 자동 제거 — "잠깐 뜨고 사라지게"
            Instance.StartCoroutine(HideTransientLater(exclamation, duration));
        }

        private static System.Collections.IEnumerator HideTransientLater(GameObject exclamation, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (exclamation != null) Destroy(exclamation);
        }
    }
}