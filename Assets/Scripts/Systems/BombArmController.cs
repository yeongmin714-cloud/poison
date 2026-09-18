using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 폭탄 무장/투척 컨트롤러 — 퀵슬롯 번호키(1~6)로 폭탄을 들고(무장),
    /// 좌클릭 시 커서 방향으로 포물선 투척·폭발을 담당한다.
    ///
    /// 흐름:
    ///   1. PlayerInventory.UseItem(폭탄) → ToggleArm/무장 (소모 안 함)
    ///   2. 무장 상태 좌클릭 → ThrowTowardCursor (커서 방향 포물선 발사)
    ///   3. 폭탄 spawn (런타임 팩토리: Sphere+Collider+Rigidbody+Bomb) + 인벤 1개 차감
    ///   4. 개수 0이면 Disarm + 해당 퀵슬롯 클리어
    /// 싱글톤 — Player(태그 "Player")에 런타임 자가 부착(Ensure).
    /// </summary>
    public class BombArmController : MonoBehaviour
    {
        public static BombArmController Instance { get; private set; }

        [Header("Bomb Throw Settings")]
        public float throwForce = 12f;
        public float throwAimHeight = 1f;     // 발사 시작 높이 (머리/손 높이)
        public float upBoost = 2.5f;          // 약간의 상향 성분 (구르기 방지)
        [Header("Bomb Spawn Settings")]
        public float bombScale = 0.4f;
        public float explosionRadius = 3f;
        public float explosionDelay = 0.5f;   // 퓨즈 시간
        public float explosionForce = 500f;   // 폭발력
        public LayerMask targetLayers = -1;   // = 모든 레이어 (기본)

        public bool IsArmed => _isArmed;
        public PlayerInventory.ItemData ArmedBomb => _armedBomb;
        /// <summary>이번 프레임에 이미 폭탄을 투척했는지 — PlayerCombat/AttackSystem 중복 처리 방지용.</summary>
        public static bool BombThrowIssuedThisFrame { get; private set; }

        private bool _isArmed;
        private PlayerInventory.ItemData _armedBomb;
        private GameObject _handVisual;

        // 손 고정점 (손에 쥔 폭탄 표시 위치) — 없으면 플레이어 기준 사용
        private Transform _handAnchor;

        /// <summary>
        /// 인스턴스 확보 — Player(태그 "Player")에 부착하고, 없으면 자체 생성.
        /// PlayerInventory.UseItem / 좌클릭 게이트에서 호출.
        /// </summary>
        public static BombArmController Ensure()
        {
            if (Instance != null) return Instance;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var ctrl = player.GetComponent<BombArmController>();
                if (ctrl == null) ctrl = player.AddComponent<BombArmController>();
                return ctrl;
            }

            var go = new GameObject("BombArmController");
            return go.AddComponent<BombArmController>();
        }

        private void OnDestroy()
        {
            // 방어: 파괴 시 무장 해제 + 프레임 플래그 리셋 (정적 플래그 스턱으로 인한 공격 영구 차단 방지)
            Disarm();
            BombThrowIssuedThisFrame = false;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 손 고정점 탐색 — WeaponMount / PlayerModel / 자기 자신 순서
            _handAnchor = transform.Find("WeaponMount")
                ?? transform.Find("PlayerModel/WeaponMount")
                ?? transform.Find("PlayerModel");
            if (_handAnchor == null) _handAnchor = transform;
        }

        private void LateUpdate()
        {
            BombThrowIssuedThisFrame = false; // 프레임 종료 시 투척 플래그 리셋
        }

        /// <summary>
        /// 폭탄 무장 토글 — 같은 폭탄이면 내려놓기(Disarm), 다르면 들기(Arm).
        /// PlayerInventory.UseItem에서 폭탄(isBomb) 분기 시 호출. 소모하지 않는다.
        /// </summary>
        public void ToggleArm(PlayerInventory.ItemData item)
        {
            if (item == null) { Disarm(); return; }
            if (_isArmed && _armedBomb != null && _armedBomb.id == item.id)
            {
                Disarm();
                return;
            }
            Arm(item);
        }

        /// <summary>손에 폭탄 표시 인스턴스를 만들고 무장 상태로 전환.</summary>
        public void Arm(PlayerInventory.ItemData item)
        {
            Disarm();
            if (item == null) return;

            _armedBomb = item;
            _isArmed = true;
            CreateHandVisual(item);
            Debug.Log($"[BombArm] 💣 {item.displayName} 무장 완료 — 좌클릭으로 투척");
        }

        /// <summary>손 표시 제거 + 무장 해제 (내려놓기).</summary>
        public void Disarm()
        {
            if (_handVisual != null) Object.Destroy(_handVisual);
            _handVisual = null;
            _isArmed = false;
            _armedBomb = null;
        }

        /// <summary>
        /// 좌클릭 투척 — 무장 상태일 때 커서(지면 y=0) 방향으로 폭탄을 포물선 발사.
        /// 발사 후 해당 폭탄을 인벤토리에서 1개 소모하고, 개수가 0이면 해제+퀵슬롯 정리.
        /// </summary>
        public void ThrowTowardCursor()
        {
            if (!_isArmed || _armedBomb == null) return;

            BombThrowIssuedThisFrame = true; // 이번 프레임 폭탄 투척 처리됨 — 이중 공격 방지

            Vector3 aimDir = GetAimDirection();
            if (aimDir.sqrMagnitude < 0.0001f)
                aimDir = transform.forward;
            aimDir.y = 0f;
            aimDir.Normalize();
            if (aimDir.sqrMagnitude < 0.0001f)
                aimDir = transform.forward;

            Vector3 spawnPos = transform.position + Vector3.up * throwAimHeight;
            Vector3 velocity = aimDir * throwForce + Vector3.up * upBoost;

            SpawnThrownBomb(spawnPos, velocity);

            // 인벤토리 소모
            var item = _armedBomb;
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.RemoveItem(item.id, 1);

            bool depleted = PlayerInventory.Instance == null
                || PlayerInventory.Instance.GetItemCount(item.id) <= 0;

            // 손에서 떨어짐 → 해제
            Disarm();

            if (depleted)
                ClearQuickslotForBomb(item.id);

            Debug.Log($"[BombArm] 💥 {item.displayName} 투척 완료 (잔여: {(PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemCount(item.id) : 0)})");
        }

        // ================================================================
        // 내부 — 손 표시 / 폭탄 생성 / 조준 / 퀵슬롯 정리
        // ================================================================

        /// <summary>손에 쥔 폭탄 시각 표시 (간단 Sphere 프리미티브, 충돌 비활성).</summary>
        private void CreateHandVisual(PlayerInventory.ItemData item)
        {
            var holder = _handAnchor != null ? _handAnchor : transform;
            _handVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _handVisual.name = "Bomb_InHand";
            var col = _handVisual.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // 장식용 — 물리 충돌 불필요
            _handVisual.transform.SetParent(holder, false);
            _handVisual.transform.localPosition = new Vector3(0.2f, 1.1f, 0.25f);
            _handVisual.transform.localScale = Vector3.one * bombScale;
            var ren = _handVisual.GetComponent<MeshRenderer>();
            if (ren != null && ren.material != null)
                ren.material.color = new Color(0.15f, 0.15f, 0.15f); // 검은 폭탄
        }

        /// <summary>
        /// 폭탄 오브젝트 절차 생성 — Sphere(Collider) + Rigidbody + Bomb + 폭발 이펙트 시각.
        /// (Resources/Bombs 프리팹은 Collider/메시가 없어 런타임 팩토리로 재구성.)
        /// </summary>
        private void SpawnThrownBomb(Vector3 spawnPos, Vector3 velocity)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ThrownBomb";
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one * bombScale;

            // 구체 콜라이더 — 스케일 반영 반지름
            var sc = go.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = false;
                sc.radius = 0.5f;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = true;
            rb.linearVelocity = velocity;

            // 폭탄 동작 — 필드를 Start() 전에 세팅 (Bomb.Start가 Invoke(Explode)를 퓨즈로 예약)
            var bomb = go.AddComponent<Bomb>();
            bomb.bombType = BombType.Explosive;
            bomb.explosionRadius = explosionRadius;
            bomb.explosionDelay = explosionDelay;
            bomb.explosionForce = explosionForce;
            bomb.targetLayers = targetLayers;
            bomb.explosionEffectPrefab = null;
            bomb.explosionSound = null;

            // 폭발 시각 — Bomb.Explode가 이 오브젝트를 Destroy하면 OnDisable에서 스파크 발화
            go.AddComponent<BombExplosionVisual>();
        }

        /// <summary>마우스 커서의 지면(y=0) 월드 위치 방향 계산.</summary>
        private Vector3 GetAimDirection()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, 0f); // y=0 평면
            if (groundPlane.Raycast(ray, out float dist))
            {
                Vector3 worldPoint = ray.GetPoint(dist);
                Vector3 dir = worldPoint - transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                    return dir.normalized;
            }
            return Vector3.zero;
        }

        /// <summary>해당 itemId가 등록된 퀵슬롯 클리어 (개수 0 시).</summary>
        private void ClearQuickslotForBomb(string itemId)
        {
            if (QuickSlotManager.Instance == null) return;
            for (int i = 0; i < QuickSlotManager.Instance.SlotCount; i++)
            {
                if (QuickSlotManager.Instance.GetItemIdInSlot(i) == itemId)
                {
                    QuickSlotManager.Instance.ClearSlot(i);
                    break;
                }
            }
        }
    }
}