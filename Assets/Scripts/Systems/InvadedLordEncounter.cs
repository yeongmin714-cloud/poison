using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>일반 침략 영지 영주용 전투 대상. 공격 버튼을 눌러야 교전이 시작된다.</summary>
    public sealed class InvadedLordEncounter : MonoBehaviour, IDamageable
    {
        public enum EncounterState { NotStarted, InCombat, Defeated, Resolved }

        [SerializeField] private string _territoryKey;
        [SerializeField] private string _lordName;
        [SerializeField] private float _maxHp = 100f;
        [SerializeField] private float _attackDamage = 8f;
        [SerializeField] private float _attackRange = 2.2f;
        [SerializeField] private float _attackCooldown = 1.25f;

        private float _hp;
        private float _nextAttackTime;
        private Transform _player;
        private readonly Collider[] _combatHits = new Collider[24];
        private TerritoryId _territoryId;
        private bool _hasTerritoryId;
        private EncounterState _state;

        public string TerritoryKey => _territoryKey;
        public string LordName => _lordName;
        public TerritoryId TerritoryId => _territoryId;
        public bool HasTerritoryId => _hasTerritoryId;
        public EncounterState State => _state;
        public bool IsInCombat => _state == EncounterState.InCombat;
        public float CurrentHP => _hp;
        public float MaxHP => _maxHp;
        public bool IsDead => _state == EncounterState.Defeated || _state == EncounterState.Resolved;
        public bool IsAlive => !IsDead;
        public bool IsResolved => _state == EncounterState.Resolved;

        /// <summary>
        /// 구판 기준형(KeepOctagon) 북쪽 침실 앵커 — 레거시 호출자·테스트 호환 래퍼.
        /// 레이아웃 인지 경로는 GameObject 오버로드 GetNorthernBedroomAnchor(room)를 사용한다.
        /// [64차 P2 — 사용자 요구: 침대가 아닌 방 안쪽 배치] (0,0,28) → (10,0,24).
        /// Bedroom 존 내부(|x|&lt;18 &amp;&amp; z&gt;14)이며 LordBed 침대 앵커와 분리된 동측 서림 자리.
        /// </summary>
        public static Vector3 GetNorthernBedroomAnchor() => new Vector3(10f, 0f, 24f);

        /// <summary>레이아웃 인지 오버로드 — 활성 정의의 LordBed 앵커(방 로컬 XZ)를 반환한다.</summary>
        public static Vector2 GetNorthernBedroomAnchor(GameObject room)
        {
            return InteriorLayoutContext.TryGetLayout(room, out InteriorLayoutDefinition layout)
                ? layout.GetAnchorPosition("LordBed")
                : new Vector2(0f, 28f);
        }

        /// <summary>호버 대상(GameObject/Component)에서 침략 영주 컴포넌트를 탐색 — 없으면 null. InteractionPanelUTK 배선용.</summary>
        public static InvadedLordEncounter FindOnTarget(object target)
        {
            if (target == null) return null;
            if (target is InvadedLordEncounter encounter) return encounter;
            if (target is Component component) return component.GetComponentInParent<InvadedLordEncounter>();
            if (target is GameObject go) return go.GetComponentInParent<InvadedLordEncounter>();
            return null;
        }

        public static bool IsNorthernBedroomPosition(Vector3 roomLocalPosition)
        {
            return PlayerCastleInteriorBuilder.GetTopologyZoneAt(
                new Vector2(roomLocalPosition.x, roomLocalPosition.z)) == "Bedroom";
        }

        /// <summary>
        /// 레이아웃 인지 오버로드 — 방의 활성 정의 존 판정으로 Bedroom 여부를 확인한다.
        /// 컨텍스트가 없으면 기준형 KeepOctagon 정적 판정으로 폴백한다.
        /// </summary>
        public static bool IsNorthernBedroomPosition(GameObject room, Vector2 roomLocalPosition)
        {
            return InteriorLayoutContext.TryGetLayout(room, out InteriorLayoutDefinition layout)
                ? layout.GetZoneAt(roomLocalPosition) == "Bedroom"
                : PlayerCastleInteriorBuilder.GetTopologyZoneAt(roomLocalPosition) == "Bedroom";
        }

        public static InvadedLordEncounter SpawnForInvadedTerritory(GameObject room, string territoryKey)
        {
            if (room == null || string.IsNullOrEmpty(territoryKey)) return null;
            var db = TerritoryDatabase.Instance;
            if (db == null) return null;
            TerritoryDefinition definition = db.GetDefinition(territoryKey);
            if (string.IsNullOrEmpty(definition.territoryName)) return null;

            // [2026-10-14 통합] 활성 레이아웃 정의의 LordBed 앵커를 따른다(컨텍스트 부재 시 기준형).
            Vector2 anchor2D = GetNorthernBedroomAnchor(room);
            if (!IsNorthernBedroomPosition(room, anchor2D))
            {
                Debug.LogError($"[InvadedLordEncounter] 영주실 앵커가 북쪽 침실 밖에 있습니다: {anchor2D}");
                return null;
            }
            Vector3 local = new Vector3(anchor2D.x, 0f, anchor2D.y);

            var go = new GameObject("InvadedLord_" + territoryKey);
            go.transform.SetParent(room.transform, false);
            go.transform.localPosition = local;
            go.tag = "Lord";
            var encounter = go.AddComponent<InvadedLordEncounter>();
            encounter.Initialize(territoryKey, definition);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "LordVisual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            visual.transform.localScale = new Vector3(0.75f, 0.95f, 0.75f);
            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null) Object.Destroy(visualCollider);
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.62f, 0.43f, 0.20f);

            var collider = go.AddComponent<CapsuleCollider>();
            collider.height = 1.9f;
            collider.radius = 0.42f;
            collider.center = new Vector3(0f, 0.95f, 0f);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var nameplate = go.AddComponent<NameplateDisplay>();
            nameplate.DisplayName = "👑 " + encounter.LordName;
            var feed = go.AddComponent<LordFeedTarget>();
            feed.TerritoryKey = territoryKey;
            feed.LordName = encounter.LordName;

            // [126차] 영주 딸 스폰 훅 — ownership==PlayerOwned && HasLordDaughter(키)일 때만 스폰.
            //   이 한 지점으로 실내 진입 런타임 경로와 테스트 셋업 경로가 함께 커버된다(스펙 §2).
            TrySpawnLordDaughterForTerritory(room, territoryKey);

            Debug.Log($"[InvadedLordEncounter] 영주실 생성 — {encounter.LordName}, territory={territoryKey}, local={local}");
            return encounter;
        }

        /// <summary>
        /// [126차 정정] 영주 딸 스폰 훅 — 생존 영주(비PlayerOwned: LordOwned/Contested) 성 진입 경로 전용.
        /// 결정론 등장 확률(HasLordDaughter)까지 충족할 때만 TrySpawnForTerritory를 호출, 그 외 무소음 스킵.
        /// 점령(PlayerOwned) 완료 성에는 등장하지 않는다(사용자 정정: 포섭→생존 영주 성 진입 시나리오).
        /// </summary>
        private static void TrySpawnLordDaughterForTerritory(GameObject room, string territoryKey)
        {
            var state = TerritoryDatabase.Instance != null ? TerritoryDatabase.Instance.GetState(territoryKey) : null;
            if (state == null || state.ownership == TerritoryOwnership.PlayerOwned) return;
            if (!LordDaughterNPC.HasLordDaughter(territoryKey)) return;
            LordDaughterNPC.TrySpawnForTerritory(room, territoryKey);
        }

        public void Initialize(string territoryKey, TerritoryDefinition definition)
        {
            _territoryKey = territoryKey ?? string.Empty;
            _lordName = string.IsNullOrEmpty(definition.lord.lordName) ? "영주" : definition.lord.lordName;
            _hp = Mathf.Max(1f, _maxHp);
            _state = EncounterState.NotStarted;

            if (!TryParseTerritoryId(_territoryKey, out _territoryId))
            {
                Debug.LogError($"[InvadedLordEncounter] 영지 키 파싱 실패: '{_territoryKey}'");
                return;
            }
            _hasTerritoryId = true;
            if (!LordSurrenderSystem.RegisterInvadedLord(_territoryId, new LordSurrenderSystem.LordData
            {
                lordId = "lord_" + _territoryKey,
                territoryId = _territoryId,
                health = _hp,
                maxHealth = _maxHp,
                isAlive = true,
                hasSurrendered = false,
                lordName = _lordName,
                personality = definition.lord.personality,
                preferredFood = definition.lord.preferredFood,
                chronicDisease = definition.lord.chronicDisease,
                nation = definition.nation
            }, gameObject))
            {
                _hasTerritoryId = false;
                Debug.LogWarning($"[InvadedLordEncounter] 영주 등록 거부 — {territoryKey} 상태가 LordOwned가 아니거나 영주 데이터가 유효하지 않습니다.");
            }
        }

        public bool BeginCombat()
        {
            if (_state == EncounterState.InCombat) return true;
            if (_state != EncounterState.NotStarted || !_hasTerritoryId) return false;
            _state = EncounterState.InCombat;
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            Debug.Log($"[InvadedLordEncounter] ⚔️ {_lordName}과의 전투 시작 ({_territoryKey})");
            return true;
        }

        public void TickCombat(float deltaTime)
        {
            if (_state != EncounterState.InCombat || Time.time < _nextAttackTime) return;
            if (_player == null || !_player.gameObject.activeInHierarchy)
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null) return;
            Vector3 direction = _player.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > _attackRange * _attackRange) return;
            int hitCount = Physics.OverlapSphereNonAlloc(_player.position, 0.45f, _combatHits);
            for (int i = 0; i < hitCount; i++)
            {
                if (_combatHits[i] == null) continue;
                if (_combatHits[i].transform != _player && !_player.IsChildOf(_combatHits[i].transform)) continue;
                PlayerHealth playerHealth = _combatHits[i].GetComponentInParent<PlayerHealth>();
                if (playerHealth == null) continue;
                playerHealth.TakeDamage(_attackDamage,
                    direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward, "lord");
                _nextAttackTime = Time.time + Mathf.Max(0.1f, _attackCooldown);
                break;
            }
        }

        private void Update() => TickCombat(Time.deltaTime);

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            if (_state != EncounterState.InCombat || amount <= 0f) return;
            _hp = Mathf.Max(0f, _hp - amount);
            Debug.Log($"[InvadedLordEncounter] 피격 — {_lordName} dmg={amount:F1}, HP={_hp:F1}/{_maxHp:F1}, weapon={weaponType}");
            if (_hp > 0f) return;

            _state = EncounterState.Defeated;
            var data = LordSurrenderSystem.GetLordData(_territoryId);
            data.health = 0f;
            data.isAlive = false;
            LordSurrenderSystem.UpdateInvadedLord(_territoryId, data);
            Debug.Log($"[InvadedLordEncounter] {_lordName} 제압됨 — 처형/추궁/풀어주기 선택 대기 (자동 점령 없음)");
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            TakeDamage(damageInfo.amount, damageInfo.knockback.normalized, "melee");
        }

        public bool Resolve(bool execute)
        {
            if (_state != EncounterState.Defeated || !_hasTerritoryId) return false;
            if (execute) LordSurrenderSystem.ExecuteLord(_territoryId);
            else LordSurrenderSystem.SpareLord(_territoryId);
            _state = EncounterState.Resolved;
            return true;
        }

        public void MarkResolvedByExistingSystem()
        {
            _state = EncounterState.Resolved;
        }

        private static bool TryParseTerritoryId(string key, out TerritoryId id)
        {
            id = default;
            if (string.IsNullOrWhiteSpace(key)) return false;
            string[] parts = key.Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int index)) return false;
            if (!System.Enum.TryParse(parts[0], true, out NationType nation)) return false;
            id = new TerritoryId(nation, index);
            return true;
        }
    }
}
