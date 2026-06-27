using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Loot Basket — 몬스터 사냥 또는 약초 채집 시 생성되는 전리품 바구니.
    /// 플레이어가 접근하여 E 키를 누르면 LootWindow를 열어 개별 획득 가능.
    /// 30초 후 자동 소멸합니다.
    /// 
    /// [동작 방식]
    /// 1. 몬스터 사망/약초 채집 시 LootBasket.Create()로 바구니 생성
    /// 2. 바구니는 지면 위에 Sack/Bag 형상(구+실린더)으로 표시
    /// 3. 플레이어가 근접하면 "E 키로 획득" 프롬프트 표시
    /// 4. E 키 입력 → LootWindow 열기 → 개별 아이템 선택 또는 전부 획득
    /// 5. 30초 동안 획득하지 않으면 자동 소멸
    /// </summary>
    public class LootBasket : MonoBehaviour, ILootBasket
    {
        [Header("설정")]
        [SerializeField] private float _interactRange = 2.5f;
        [SerializeField] private float _lifetime = 30f;

        [Header("아이템 목록")]
        [SerializeField] private List<LootEntry> _items = new List<LootEntry>();

        // 상태
        private bool _isLooted;
        private bool _playerNearby;
        private Transform _player;
        private Keyboard _keyboard;

        // Create()에서 생성된 Material 추적 (메모리 누수 방지)
        private List<Material> _createdMaterials = new List<Material>();

        // ================================================================
        // ILootBasket Implementation
        // ================================================================

        /// <summary>읽기 전용 아이템 목록 (디버그/UI용)</summary>
        public IReadOnlyList<LootEntry> Items => _items;

        /// <summary>아직 획득 가능한 상태인가?</summary>
        public bool IsAvailable => !_isLooted;

        /// <summary>바구니가 비었거나 모든 아이템이 소진되었는가?</summary>
        public bool IsEmpty
        {
            get
            {
                if (_isLooted) return true;
                foreach (var entry in _items)
                {
                    if (entry != null && entry.Item != null && entry.Count > 0)
                        return false;
                }
                return true;
            }
        }

        /// <summary>유효한(비어있지 않은) 아이템 종류 개수</summary>
        public int ItemCount
        {
            get
            {
                if (_isLooted) return 0;
                int count = 0;
                foreach (var entry in _items)
                {
                    if (entry != null && entry.Item != null && entry.Count > 0)
                        count++;
                }
                return count;
            }
        }

        /// <summary>내용물 기반 자동 생성된 바구니 이름 (LootWindow 타이틀용)</summary>
        public string BasketName
        {
            get
            {
                if (_items == null || ItemCount == 0)
                    return "전리품";

                // 첫 번째 유효한 아이템 이름 기반
                string firstName = null;
                int totalCount = 0;
                foreach (var entry in _items)
                {
                    if (entry != null && entry.Item != null && entry.Count > 0)
                    {
                        if (firstName == null)
                            firstName = entry.Item.displayName;
                        totalCount++;
                    }
                }

                if (totalCount <= 1 && firstName != null)
                    return $"{firstName}";

                if (firstName != null)
                    return $"{firstName} 외 {totalCount - 1}종";

                return "전리품";
            }
        }

        // ================================================================
        // Public Methods
        // ================================================================

        /// <summary>
        /// 바구니에 아이템을 추가합니다. 같은 ID의 아이템은 자동 스택 처리됩니다.
        /// </summary>
        public void AddItem(PlayerInventory.ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return;

            // 같은 아이템이 있으면 스택
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Item != null && _items[i].Item.id == item.id)
                {
                    _items[i].Count += count;
                    return;
                }
            }

            // 새 슬롯 추가
            _items.Add(new LootEntry { Item = item, Count = count });
        }

        /// <summary>
        /// 지정된 인덱스의 아이템 하나를 플레이어 인벤토리로 이동합니다.
        /// 성공 시 바구니에서 제거되며, 바구니가 비었으면 자동 소멸합니다.
        /// </summary>
        /// <param name="index">아이템 인덱스</param>
        /// <returns>획득 성공 여부</returns>
        public bool TakeItem(int index)
        {
            if (_isLooted) return false;
            if (index < 0 || index >= _items.Count) return false;

            var entry = _items[index];
            if (entry == null || entry.Item == null || entry.Count <= 0) return false;

            if (PlayerInventory.Instance == null)
            {
                Debug.LogError("[LootBasket] PlayerInventory.Instance가 없습니다!");
                return false;
            }

            bool success = PlayerInventory.Instance.AddItem(entry.Item, entry.Count);
            if (success)
            {
                Debug.Log($"[LootBasket] {entry.Item.displayName} x{entry.Count} 획득!");
                _items.RemoveAt(index);

                // 바구니가 비었으면 자동 소멸
                if (ItemCount == 0)
                {
                    _isLooted = true;
                    HidePrompt();
                    Destroy(gameObject);
                }
                return true;
            }
            else
            {
                Debug.LogWarning($"[LootBasket] {entry.Item.displayName} x{entry.Count} — 인벤토리 가득 참!");
                return false;
            }
        }

        /// <summary>
        /// 모든 아이템을 플레이어 인벤토리로 이동합니다.
        /// 인벤토리가 가득 차서 이동 실패한 아이템은 바구니에 남깁니다.
        /// </summary>
        /// <returns>하나라도 성공적으로 이동한 경우 true</returns>
        public bool TakeAll()
        {
            if (_isLooted) return false;

            if (PlayerInventory.Instance == null)
            {
                Debug.LogError("[LootBasket] PlayerInventory.Instance가 없습니다!");
                HidePrompt();
                _isLooted = true;
                Destroy(gameObject);
                return false;
            }

            bool anySuccess = false;
            // 뒤에서부터 순회하여 안전하게 제거
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var entry = _items[i];
                if (entry == null || entry.Item == null || entry.Count <= 0) continue;

                bool success = PlayerInventory.Instance.AddItem(entry.Item, entry.Count);
                if (success)
                {
                    Debug.Log($"[LootBasket] {entry.Item.displayName} x{entry.Count} 획득!");
                    _items.RemoveAt(i);
                    anySuccess = true;
                }
                else
                {
                    Debug.LogWarning($"[LootBasket] {entry.Item.displayName} x{entry.Count} — 인벤토리 가득 참, 바구니에 남김!");
                }
            }

            if (_items.Count == 0)
            {
                _isLooted = true;
                HidePrompt();
                Destroy(gameObject);
            }

            return anySuccess;
        }

        // ================================================================
        // MonoBehaviour Lifecycle
        // ================================================================

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            _keyboard = Keyboard.current;

            if (_player == null)
                Debug.LogWarning("[LootBasket] Player 태그 오브젝트를 찾을 수 없음");

            // 자동 소멸 타이머
            Destroy(gameObject, _lifetime);
        }

        private void OnDestroy()
        {
            // Create()에서 생성된 Material 정리 (메모리 누수 방지)
            foreach (var mat in _createdMaterials)
            {
                if (mat != null)
                    Destroy(mat);
            }
            _createdMaterials.Clear();
        }

        private void Update()
        {
            if (_isLooted || _player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            bool wasNearby = _playerNearby;
            _playerNearby = dist <= _interactRange;

            // 프롬프트 표시/숨김 (상태 변경 시에만)
            if (_playerNearby && !wasNearby)
                ShowPrompt();
            else if (!_playerNearby && wasNearby)
                HidePrompt();

            // E 키 입력 (Input System) — LootWindow 열기
            if (_playerNearby && _keyboard != null && _keyboard.eKey.wasPressedThisFrame)
                OpenForLoot();
        }

        // ================================================================
        // LootWindow 통합 — 바구니 열기
        // ================================================================

        /// <summary>
        /// LootWindow를 열기 위한 정적 이벤트 — UIManager가 구독하여 처리합니다.
        /// </summary>
        public static event System.Action<ILootBasket> OnOpenLootWindowRequested;
        private void OpenForLoot()
        {
            if (_isLooted) return;

            if (IsEmpty)
            {
                Debug.Log("[LootBasket] 바구니가 비어 있습니다.");
                Destroy(gameObject);
                return;
            }

            HidePrompt();
            _playerNearby = false; // 프롬프트 상태 리셋 — LootWindow 닫힌 후 재표시 가능하도록

            if (OnOpenLootWindowRequested != null)
            {
                OnOpenLootWindowRequested.Invoke(this);
            }
            else
            {
                // No UI handler registered — fallback: take all directly
                Debug.LogWarning("[LootBasket] LootWindow handler가 없어 직접 루팅합니다.");
                TakeAll();
            }
        }

        // ================================================================
        // Prompt 표시 (단순 로그 기반 — 추후 World Space Canvas로 대체)
        // ================================================================

        private void ShowPrompt()
        {
            Debug.Log("[LootBasket] 🧺 E 키를 눌러 전리품 획득!");
        }

        private void HidePrompt()
        {
            // 프롬프트 숨김 (프리팹이 없으므로 별도 처리 없음)
        }

        // ================================================================
        // Static Factory — 바구니 생성
        // ================================================================

        /// <summary>
        /// 지정된 위치에 LootBasket 오브젝트를 생성합니다.
        /// 자동으로 지면 위에 배치되며, Sack/Bag 형상(구+실린더)으로 시각화됩니다.
        /// </summary>
        /// <param name="position">생성 위치 (월드 좌표)</param>
        /// <param name="lifetime">자동 소멸 시간 (초)</param>
        /// <returns>생성된 LootBasket 컴포넌트</returns>
        public static LootBasket Create(Vector3 position, float lifetime = 30f)
        {
            // 지면 위치 찾기 (Raycast)
            Vector3 spawnPos = position;
            if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            {
                spawnPos = hit.point;
            }

            // 바구니 게임오브젝트 생성
            GameObject basketGO = new GameObject("LootBasket");
            basketGO.transform.position = spawnPos;

            // === 바구니 시각화 (Sack/Bag 형상: 구+실린더) ===

            // 1) 메인 몸통 (구 -> 약간 납작한 형태)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Bag_Body";
            body.transform.SetParent(basketGO.transform);
            body.transform.localPosition = new Vector3(0, 0.2f, 0);
            body.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);

            Material bodyMat = null;
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bodyMat.color = new Color(0.6f, 0.4f, 0.2f);
                bodyRenderer.material = bodyMat;
            }

            Collider bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            // 2) 개구부 (실린더 -> bag opening)
            GameObject opening = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            opening.name = "Bag_Opening";
            opening.transform.SetParent(basketGO.transform);
            opening.transform.localPosition = new Vector3(0, 0.55f, 0);
            opening.transform.localScale = new Vector3(0.85f, 0.1f, 0.85f);

            Material openingMat = null;
            Renderer openingRenderer = opening.GetComponent<Renderer>();
            if (openingRenderer != null)
            {
                openingMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                openingMat.color = new Color(0.5f, 0.3f, 0.15f);
                openingRenderer.material = openingMat;
            }

            Collider openingCol = opening.GetComponent<Collider>();
            if (openingCol != null) Destroy(openingCol);

            // 3) 트리거 Collider (상호작용 영역) — 추후 물리 기반 감지용 예비 코드
            // 현재는 Vector3.Distance 기반 Update() 감지를 사용 중이므로 실제로 동작하지 않음.
            // 필요 시 OnTriggerEnter/Exit 구현 후 활성화.
            // SphereCollider trigger = basketGO.AddComponent<SphereCollider>();
            // trigger.isTrigger = true;
            // trigger.radius = 0.5f;

            // LootBasket 컴포넌트 추가
            LootBasket basket = basketGO.AddComponent<LootBasket>();
            basket._lifetime = lifetime;
            if (bodyMat != null) basket._createdMaterials.Add(bodyMat);
            if (openingMat != null) basket._createdMaterials.Add(openingMat);

            return basket;
        }

        // ================================================================
        // Editor Gizmo
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.6f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}