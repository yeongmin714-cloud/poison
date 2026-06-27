using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// ND-03: 스켈레톤 병사 Placeholder.
    /// 흰색/회색 도형으로 해골 병사를 표현합니다.
    /// </summary>
    public class SkeletonGuardPlaceholder : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float _maxHP = 200f;
        [SerializeField] private float _attackDamage = 15f;
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _detectRange = 15f;
        [SerializeField] private int _level = 35;

        [Header("Visual")]
        [SerializeField] private Color _skeletonColor = new Color(0.85f, 0.85f, 0.90f); // 회백색
        [SerializeField] private bool _verbose;

        // NOTE: _attackDamage is declared but currently unused in this class.
        // It is serialized for future combat-system integration (designer tuning).
        // CS0414 suppressed via #pragma below.

        private float _currentHP;
        private Transform _player;
        private bool _isDead;

        // Rig animation
        private RigAnimationController _rigAnim;

        // Placeholder 오브젝트 참조
        private GameObject _head;
        private GameObject _body;
        private GameObject _leftArm;
        private GameObject _rightArm;

        public float MaxHP => _maxHP;
        public float CurrentHP => _currentHP;
        public int Level => _level;
        public bool IsDead => _isDead;
        public bool IsAlive => !_isDead;

        private void Awake()
        {
            _currentHP = _maxHP;

            // 태그 설정 (GLB 로드 성공/실패 여부와 무관하게 항상 설정)
            gameObject.tag = "DraculaGuard";

            // Rig animation setup
            _rigAnim = GetComponent<RigAnimationController>();
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
            }

            // Try to load real GLB model first
            if (RuntimeModelLoader.TryGetModel("soldier", out var guardModel))
            {
                var instance = Instantiate(guardModel, transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                // Remove this object's own placeholder mesh components only
                // (NOT GetComponentInChildren — that would destroy the GLB model's renderer)
                var myRenderer = GetComponent<Renderer>();
                if (myRenderer != null) Destroy(myRenderer);
                var myFilter = GetComponent<MeshFilter>();
                if (myFilter != null) Destroy(myFilter);
                ModelAnimatorAssigner.AssignController(instance, "soldier");
                return; // Skip CreatePlaceholderVisual
            }

            CreatePlaceholderVisual();
        }

        private void Start()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                _player = playerGo.transform;

            // 기본 Idle 애니메이션
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);
        }

        private void Update()
        {
            if (_isDead || _player == null)
            {
                // 사망 또는 플레이어 없음 → Idle
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetState(AnimationState.Idle);
                return;
            }

            float dist = Vector3.Distance(transform.position, _player.position);

            if (dist < _detectRange)
            {
                // 플레이어 추격
                Vector3 dir = _player.position - transform.position;
                dir.y = 0;
                // Zero-vector 방어 (플레이어와 완전히 같은 위치)
                if (dir.sqrMagnitude < 0.001f) return;
                dir.Normalize();
                transform.position += dir * _moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(dir);

                // 이동 애니메이션: Walk
                if (_rigAnim != null) { _rigAnim.CurrentSpeed = _moveSpeed; _rigAnim.SetState(AnimationState.Walk); }
            }
            else
            {
                // 감지 범위 밖 → Idle
                if (_rigAnim != null && _rigAnim.CurrentState != AnimationState.Idle)
                    _rigAnim.SetState(AnimationState.Idle);
            }
        }

        private void CreatePlaceholderVisual()
        {
            // 머리 (Sphere)
            _head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _head.transform.SetParent(transform);
            _head.transform.localPosition = new Vector3(0, 1.2f, 0);
            _head.transform.localScale = Vector3.one * 0.3f;
            Destroy(_head.GetComponent<Collider>());
            SetColor(_head, _skeletonColor);

            // 몸통 (Capsule)
            _body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _body.transform.SetParent(transform);
            _body.transform.localPosition = new Vector3(0, 0.6f, 0);
            _body.transform.localScale = new Vector3(0.5f, 0.8f, 0.3f);
            Destroy(_body.GetComponent<Collider>());
            SetColor(_body, Color.Lerp(_skeletonColor, Color.gray, 0.3f));

            // 왼팔 (Cylinder)
            _leftArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _leftArm.transform.SetParent(transform);
            _leftArm.transform.localPosition = new Vector3(-0.4f, 0.8f, 0);
            _leftArm.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
            _leftArm.transform.localRotation = Quaternion.Euler(0, 0, 15);
            Destroy(_leftArm.GetComponent<Collider>());
            SetColor(_leftArm, _skeletonColor);

            // 오른팔 (Cylinder)
            _rightArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _rightArm.transform.SetParent(transform);
            _rightArm.transform.localPosition = new Vector3(0.4f, 0.8f, 0);
            _rightArm.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
            _rightArm.transform.localRotation = Quaternion.Euler(0, 0, -15);
            Destroy(_rightArm.GetComponent<Collider>());
            SetColor(_rightArm, _skeletonColor);
        }

        private void SetColor(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        // ===== IDamageable =====

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            if (_isDead) return;
            _currentHP -= amount;
            if (_verbose)
                Debug.Log($"[SkeletonGuard] 데미지 {amount}, 남은 HP: {_currentHP}/{_maxHP}");

            if (_currentHP <= 0)
            {
                Die();
            }
        }

        public void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // 사망 애니메이션 (Idle 즉시 적용)
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            if (_verbose)
                Debug.Log("[SkeletonGuard] 사망!");

            // LootBasket 드랍 (ND-05 연동)
            LootBasket basket = LootBasket.Create(transform.position);
            if (DropTableManager.Instance != null)
            {
                DropTableManager.Instance.ApplySkeletonGuardDrops(basket, _level);
            }

            // Placeholder 제거
            Destroy(gameObject, 0.5f);
        }
    }
}