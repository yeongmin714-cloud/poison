using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-28: 자원 노드 — 광부가 채광할 수 있는 Wood/Stone/IronOre 노드
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        public enum ResourceType { Wood, Stone, IronOre, Herb, Silver, Gold, Mythril }  // [Milestone C] Silver/Gold/Mythril = 희귀 광물 노드

        [SerializeField, Tooltip("자원 종류")]
        private ResourceType _resourceType = ResourceType.Wood;

        [SerializeField, Tooltip("최소 수확량"), Range(1, 999)]
        private int _minYield = 1;

        [SerializeField, Tooltip("최대 수확량"), Range(1, 999)]
        private int _maxYield = 3;

        [SerializeField, Tooltip("리스폰 시간 (초)"), Min(0.1f)]
        private float _respawnTime = 15f;

        // [Milestone C] 채광 시 한 단계 위 희귀 광물을 추가로 굴릴 확률(0=없음). 가치↑→드랍↓ 밸런스.
        [SerializeField, Tooltip("채광 보너스 희귀 광물 드랍 확률(0~1). 가치가 높은 광물일수록 낮게."), Range(0f, 1f)]
        private float _rareBonusChance = 0f;

        private bool _isDepleted;

        // [2026-10-11 28차] 벌목 후 그루터기 잔존 — 종별 cut-stump GLB(birch/broadleaf/pine).
        //   자원 스포너가 시각 GLB 파일명에서 _stumpType을 리플렉션으로 설정. 리스폰 시 나무 복원+그루터기 비표시.
        [SerializeField, Tooltip("그루터기 종류(birch/broadleaf/pine — 스포너 자동 설정)")]
        private string _stumpType;
        private Transform _stump;   // "Stump" 자식 — 벌목 시 표시, 리스폰 시 비표시

        // --- 플레이어 직접 채광(수집 채널링) ---
        [SerializeField, Tooltip("플레이어 채광 상호작용 반경"), Min(0.5f)]
        private float _playerInteractRange = 2.8f;
        private Transform _player;
        private float _playerNextRefresh;

        // --- 캐시된 컴포넌트 참조 ---
        private Renderer _renderer;
        private Collider _collider;

        // --- 나무 흔들림(벌목 연출) ---
        private bool _isWood;
        private Transform _visual;          // "Visual" 자식 = 나무 모델 (자연 배치 노드)
        private Vector3 _baseVisualEuler;
        private bool _baseEulerSet;

        /// <summary>고갈되지 않아 채광 가능한 상태인지 여부</summary>
        public bool IsAvailable => !_isDepleted;
        /// <summary>이 노드의 자원 종류</summary>
        public ResourceType NodeType => _resourceType;

        /// <summary>[34차] 스포너용 타입 설정 — AddComponent 직후 Awake가 기본값(Wood)으로 _isWood를
        ///   먼저 계산해 버려서, 리플렉션으로 _resourceType만 나중에 바꾸면 _isWood가 Wood에
        ///   프리징됨("석재인데 벌목 성공!" 버그의 근원 — 채광 ui.PNG 실측). 파생 상태 재계산.</summary>
        public void ConfigureResourceType(ResourceType type)
        {
            _resourceType = type;
            _isWood = _resourceType == ResourceType.Wood;
            _visual = _isWood ? transform.Find("Visual") : null;
        }

        /// <summary>보너스 희귀 광물 드랍 확률(Inspector).</summary>
        public float RareBonusChance => _rareBonusChance;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();
            _isWood = _resourceType == ResourceType.Wood;
            _visual = _isWood ? transform.Find("Visual") : null;

            // Inspector 설정 검증
            if (_minYield > _maxYield)
            {
                Debug.LogWarning($"[ResourceNode] _minYield({_minYield}) > _maxYield({_maxYield}), 자동 교정합니다.");
                _maxYield = _minYield;
            }
        }

        private void Update()
        {
            if (_isDepleted) return;

            RefreshPlayer();
            if (_player == null) return;

            bool channelActive = GatherChannelSystem.Instance.IsActive;
            if (channelActive)
            {
                GatherChannelSystem.Instance.Tick();
                // 나무 벌목 연출: 이 노드가 채널 대상일 때 나무 모델을 흔든다.
                if (GatherChannelSystem.Instance.ActiveTarget == transform)
                    ApplyWoodSway();
                else
                    StopSway();
                return;
            }
            StopSway();

            // E키 직접 채광 → 파랑 진행바 채널링 후 완료 시 채광.
            if (Input.GetKeyDown(KeyCode.E)
                && Vector3.Distance(transform.position, _player.position) <= _playerInteractRange)
            {
                GatherChannelSystem.Instance.Begin(
                    transform,
                    HarvestForPlayer,
                    GatherChannelSystem.DefaultChannelSeconds);
            }
        }

        /// <summary>벌목 진행 중 나무 모델(Visual)을 사인파로 흔든다 — 진행률에 따라 강도 증가.</summary>
        private void ApplyWoodSway()
        {
            if (!_isWood || _visual == null) return;
            if (!_baseEulerSet)
            {
                _baseVisualEuler = _visual.localEulerAngles;
                _baseEulerSet = true;
            }
            float p = GatherChannelSystem.Instance.Progress;
            float amp = Mathf.Lerp(0.7f, 3.0f, p);          // 채집 진행(충격 누적)에 따라 더 크게
            float sway = Mathf.Sin(Time.time * 16f) * amp;  // 좌우 까닥이는 흔들림
            _visual.localEulerAngles = _baseVisualEuler + new Vector3(0f, 0f, sway);
        }

        /// <summary>스웨이 정지 — 원래 회전 복원.</summary>
        private void StopSway()
        {
            if (!_isWood || _visual == null || !_baseEulerSet) return;
            _visual.localEulerAngles = _baseVisualEuler;
            _baseEulerSet = false;
        }

        private void RefreshPlayer()
        {
            if (_player != null && Time.unscaledTime < _playerNextRefresh) return;
            var go = GameObject.FindGameObjectWithTag("Player");
            _player = go != null ? go.transform : null;
            _playerNextRefresh = Time.unscaledTime + 0.5f;
        }

        /// <summary>플레이어 채광/벌목 완료 — [2026-10-11 28차] 아이템 직접 지급 + 피그마 결과 창 표시.
        /// 기존 LootBasket(전리품 바구니) 경로는 병사 자동 작업(TryAutoMine)에만 유지 — 사용자 요구:
        /// "채광/벌목도 전리품 형태가 아니라 피그마 창이 뜨면서". 벌목은 나무 대신 그루터기 잔존.</summary>
        public void HarvestForPlayer()
        {
            if (_isDepleted) return;
            _isDepleted = true;
            StopSway();   // 벌목 완료 — 나무 회전 원복 후 숨김

            var item = GetItemData();
            int yield = Random.Range(_minYield, _maxYield + 1);
            if (item != null)
            {
                PlayerInventory.Instance?.AddItem(item, yield);
                HarvestResultBridge.Publish(
                    _isWood ? HarvestResultBridge.HarvestKind.Woodcutting
                            : HarvestResultBridge.HarvestKind.Mining,
                    _isWood ? "벌목 성공!" : "채광 성공!",
                    item.displayName, item.description, item.rarity, yield,
                    _isWood ? "통나무는 크래프트(제작소)에서 무기·공구 재료로 쓰인다."
                            : "석재는 크래프트(제작소)에서 공구·석재 티어 장비 재료로 쓰인다.",
                    item, immediate: true);
            }

            // 희귀 보너스 광물 (확률 0이면 안 뜸) — 인벤 직접 지급.
            if (TryRollRareBonus(out var bonus, out int bonusYield))
                PlayerInventory.Instance?.AddItem(bonus, bonusYield);

            PlayerStats.Instance?.AddEXP(3);

            EnsureStump();   // [28차] 벌목 → 그루터기(나무만 — 시각 GLB 없으면 스킵)
            if (_renderer) _renderer.enabled = false;
            if (_collider) _collider.enabled = false;
            SetChildRenderersVisible(false);
            Invoke(nameof(Respawn), _respawnTime);
        }

        /// <summary>[2026-10-11 28차] 그루터기 자식 생성(지연) — 종별 cut-stump GLB(에디터 로드),
        /// 나무(8.6m 정규화)와 등비 스케일+지표 정렬. SetChildRenderersVisible(false)가 반전 표시한다.</summary>
        private void EnsureStump()
        {
            if (!_isWood || _stump != null) return;
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(_stumpType)) return;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/새로운 glb/nature/angular-common-nature-{_stumpType}-tree-cut-stump.glb");
            if (prefab == null)
            {
                Debug.LogWarning($"[ResourceNode] 그루터기 GLB 미발견: {_stumpType} — 그루터기 없이 숨김만");
                return;
            }
            var go = Object.Instantiate(prefab, transform);
            go.name = "Stump";
            // [2026-10-11 29차→30차] 그루터기 높이 정규화 — 0.86m(1/10)는 원거리 카메라에서 발치 높이로
            //   너무 작다는 영상 재보고(테스트 15씬 2) → **1.5m**(나무의 약 1/5.7)로 상향. GLB 원본 크기 무관.
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                if (b.size.y > 0.01f)
                    go.transform.localScale *= Mathf.Clamp(1.5f / b.size.y, 0.01f, 100f);
            }
            AlignStumpToGround(go);
            _stump = go.transform;
            _stump.gameObject.SetActive(false);   // SetChildRenderersVisible(false)가 반전 표시
#endif
        }

        /// <summary>그루터기 밑면을 지표(노드 루트 y)에 정렬 — 자원 스포너 AlignBottomToGround 선례.</summary>
        private void AlignStumpToGround(GameObject stump)
        {
            var renderers = stump.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float dy = transform.position.y - b.min.y;
            if (Mathf.Abs(dy) > 0.001f)
                stump.transform.position += new Vector3(0f, dy, 0f);
        }

        private void OnDisable()
        {
            // GameObject 비활성화 시에도 Invoke 타이머는 Unity가 일시중지/재개하므로
            // CancelInvoke를 호출하지 않음 (리스폰 타이머 유지).
            // OnDestroy에서만 정리.
        }

        private void OnDestroy()
        {
            CancelInvoke();
        }

        /// <summary>
        /// Miner auto-mine — 자원 채광 및 시각적 고갈 처리
        /// </summary>
        /// <param name="item">채광된 아이템 데이터 (고갈 시 null)</param>
        /// <param name="yield">수확량 (고갈 시 0)</param>
        /// <returns>채광 성공 여부</returns>
        public bool TryAutoMine(out PlayerInventory.ItemData item, out int yield)
        {
            item = null;
            yield = 0;
            if (_isDepleted) return false;

            _isDepleted = true;
            yield = Random.Range(_minYield, _maxYield + 1);
            item = GetItemData();

            // Hide visual
            if (_renderer) _renderer.enabled = false;
            if (_collider) _collider.enabled = false;
            SetChildRenderersVisible(false);

            Invoke(nameof(Respawn), _respawnTime);
            return true;
        }

        /// <summary>
        /// [Milestone C] 채광 보너스 — 이 노드에서 한 단계 위 희귀 광물을 추가 드랍.
        /// 가치(티어)↑→확률↓ 원칙: IronOre→Gold, Silver→Gold, Gold→Mythril, Mythril→수정석 등.
        /// _rareBonusChance=0이면 절대 안 뜬다(보통 노드 기본값). 성공 시 item/count 반환.
        /// </summary>
        public bool TryRollRareBonus(out PlayerInventory.ItemData item, out int yield)
        {
            item = null;
            yield = 0;
            if (_rareBonusChance <= 0f || Random.value > _rareBonusChance) return false;

            PlayerInventory.ItemData bonus = _resourceType switch
            {
                ResourceType.Stone  => PlayerInventory.SilverOre,
                ResourceType.IronOre => PlayerInventory.GoldOre,
                ResourceType.Silver => PlayerInventory.GoldOre,
                ResourceType.Gold   => PlayerInventory.MythrilOre,
                ResourceType.Mythril => PlayerInventory.CrystalShard,
                _ => null,
            };
            if (bonus == null) return false;
            item = bonus;
            yield = 1;
            Debug.Log($"[ResourceNode] 💎 보너스 희귀 광물 드랍: {bonus.displayName} ({_resourceType})");
            return true;
        }

        private void Respawn()
        {
            if (this == null) return; // 객체 파괴됐을 경우 방어

            _isDepleted = false;
            if (_renderer) _renderer.enabled = true;
            if (_collider) _collider.enabled = true;
            StopSway();
            SetChildRenderersVisible(true);
        }

        /// <summary>뿌리+자식 시각(Visual) 렌더러 표시/숨김 — 벌목/채광 시 나무 모델이 사라지게.
        /// [2026-10-11 28차] "Stump" 자식은 반대 상태로 토글(나무 숨김 시 그루터기 표시, 리스폰 시 반대).</summary>
        private void SetChildRenderersVisible(bool visible)
        {
            if (_renderer) _renderer.enabled = visible;
            foreach (Transform child in transform)
            {
                if (child.name == "Stump")
                {
                    if (child.gameObject.activeSelf == visible)
                        child.gameObject.SetActive(!visible);
                    continue;
                }
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers == null) continue;
                foreach (var r in renderers)
                    if (r != null) r.enabled = visible;
            }
        }

        private PlayerInventory.ItemData GetItemData()
        {
            switch (_resourceType)
            {
                case ResourceType.Wood:
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_wood",   // [Milestone B] 크래프트 재료로 정렬(기존 "wood")
                        displayName = "통나무",
                        description = "채광/벌목으로 얻는 목재. 무기·방어구 제작 재료.",
                        category = PlayerInventory.ItemCategory.Material,
                        maxStack = 99
                    };
                case ResourceType.Stone:
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_stone",  // [Milestone B] 크래프트 재료로 정렬(기존 "stone")
                        displayName = "석재",
                        description = "채광으로 얻는 돌. Stone 티어 장비 제작 재료.",
                        category = PlayerInventory.ItemCategory.Material,
                        maxStack = 99
                    };
                case ResourceType.IronOre:
                    return new PlayerInventory.ItemData
                    {
                        id = "iron_ore",
                        displayName = "철광석",
                        description = "제련하여 철괴를 만들 수 있는 광석.",
                        category = PlayerInventory.ItemCategory.Material,
                        maxStack = 99
                    };
                case ResourceType.Silver:   // [Milestone C] 희귀 광물 노드
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_silver_ore", displayName = "은광석",
                        description = "희귀한 은 광석. 고급 장비·장신구 재료.",
                        category = PlayerInventory.ItemCategory.Material, maxStack = 99
                    };
                case ResourceType.Gold:     // [Milestone C]
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_gold_ore", displayName = "금광석",
                        description = "귀한 금 광석. 희귀 장비·장신구 재료.",
                        category = PlayerInventory.ItemCategory.Material, maxStack = 99
                    };
                case ResourceType.Mythril:  // [Milestone C]
                    return new PlayerInventory.ItemData
                    {
                        id = "mat_mythril_ore", displayName = "미스릴 광석",
                        description = "전설급 미스릴. 최상위 장비 재료.",
                        category = PlayerInventory.ItemCategory.Material, maxStack = 99
                    };
                default:
                    Debug.LogError($"[ResourceNode] 알 수 없는 ResourceType: {_resourceType}");
                    return null;
            }
        }
    }
}