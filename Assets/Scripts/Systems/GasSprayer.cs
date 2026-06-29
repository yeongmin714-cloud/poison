using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 4: 가스 분사기 — 플레이어 등(Back 슬롯)에 장착하는 가스 분사기.
    /// G 키로 분사 On/Off, GasSprayerController 상태에 따라 효과 적용.
    /// - 물약/독약/마약 삽입 가능
    /// - 등급별 최대 분사 시간 (GasSprayerManager 참조)
    /// - 빈 상태에서 새 물약 넣으면 재사용 가능 (재장전)
    /// </summary>
    public class GasSprayer : MonoBehaviour
    {
        [Header("Spray Controls")]
        // G 키는 Input System Keyboard.current.gKey로 직접 처리

        [Header("Effect Settings")]
        [SerializeField] private LayerMask _targetLayers = -1; // Default: Everything
        [SerializeField] private float _effectInterval = 0.5f;  // 효과 체크 간격 (초)
        [SerializeField] private float _fogLifetime = 1.5f;    // 안개 오브젝트 지속 시간

        [Header("Poison (공격성/독)")]
        [SerializeField] private float _minPoisonDamage = 5f;
        [SerializeField] private float _maxPoisonDamage = 15f;

        [Header("Mental (정신성/마약)")]
        [SerializeField] private float _confusionDuration = 3f;
        [SerializeField] private float _slowAmount = 0.3f;

        [Header("Heal (회복성/치료)")]
        [SerializeField] private float _allyHealAmount = 10f;

        [Header("Buff (물리성/강화)")]
        [SerializeField] private float _allyBuffDuration = 5f;
        [SerializeField] private float _allyDefenseBuff = 10f;
        [SerializeField] private float _allyAttackBuff = 5f;

        [Header("Spray Fog Colors")]
        [SerializeField] private Color _poisonFogColor = new Color(1f, 0.15f, 0.15f, 0.45f);  // 붉은 안개
        [SerializeField] private Color _mentalFogColor = new Color(0.6f, 0.15f, 0.85f, 0.45f); // 보라색 안개
        [SerializeField] private Color _healFogColor = new Color(0.15f, 0.85f, 0.15f, 0.45f);  // 초록색 안개
        [SerializeField] private Color _buffFogColor = new Color(0.15f, 0.35f, 0.95f, 0.45f);  // 파란색 안개

        // ── Internal state ───────────────────────────────────────────────
        private GasSprayerController _controller;
        private Transform _cameraTransform;
        private float _effectTimer;
        private bool _lastFrameSpraying;
        private UnityEngine.InputSystem.Keyboard _keyboard;

        // 활성화된 안개 VFX 리스트 (메모리 풀링 없이 간단 구현)
        private readonly List<SprayFog> _activeFogs = new List<SprayFog>();

        // 안개 시각적 표현용 구조체
        private struct SprayFog
        {
            public GameObject go;
            public float startTime;
            public float lifetime;
        }

        // ── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _controller = GasSprayerController.Instance;
            if (_controller == null)
            {
                Debug.LogWarning("[GasSprayer] GasSprayerController.Instance를 찾을 수 없습니다. GasSprayer 비활성화.");
                enabled = false;
                return;
            }

            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;

            _keyboard = UnityEngine.InputSystem.Keyboard.current;
        }

        private void Start()
        {
            // 초기 효과 타이머 설정 (0.5초 후 첫 효과)
            _effectTimer = _effectInterval;
        }

        private void Update()
        {
            if (_controller == null) return;

            // G 키 입력 감지 (Input System) — 분사 토글
            if (_keyboard != null && _keyboard.gKey.wasPressedThisFrame)
            {
                if (_controller.IsEquipped)
                {
                    if (_controller.IsSpraying)
                    {
                        _controller.StopSpray();
                    }
                    else
                    {
                        _controller.StartSpray();
                    }
                }
            }

            // 분사 중 효과 처리
            if (_controller.IsSpraying && _controller.IsEquipped && !_controller.IsReloading)
            {
                if (!_lastFrameSpraying)
                {
                    // 분사 시작 시 초기화
                    _effectTimer = 0f;
                    _lastFrameSpraying = true;
                }

                // 주기적 효과 적용
                _effectTimer += Time.deltaTime;
                while (_effectTimer >= _effectInterval)
                {
                    _effectTimer -= _effectInterval;
                    ApplySprayEffects();
                }

                // 안개 VFX 생성 (매 프레임 작은 안개 생성)
                SpawnFogEffect();
            }
            else
            {
                if (_lastFrameSpraying)
                {
                    _lastFrameSpraying = false;
                    _effectTimer = _effectInterval; // 리셋
                }
            }

            // 만료된 안개 제거
            UpdateFogLifetimes();
        }

        // ── Effect Application ───────────────────────────────────────────

        /// <summary>
        /// 현재 장전된 물약 타입에 따라 적/아군에게 효과 적용.
        /// </summary>
        private void ApplySprayEffects()
        {
            GasSprayerData data = _controller.GetCurrentSprayerData();
            float range = data.sprayRange;
            Vector3 origin = transform.position + Vector3.up * 0.5f; // 약간 위에서 분사

            // 장전된 물약 타입 확인
            string potionId = _controller.LoadedPotionId;
            PotionType potionType = ClassifyPotion(potionId);

            // 범위 내 모든 Collider 감지
            Collider[] hits = Physics.OverlapSphere(origin, range, _targetLayers);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue; // 자기 자신 제외

                switch (potionType)
                {
                    case PotionType.Poison:
                        ApplyPoisonEffect(hit);
                        break;
                    case PotionType.Mental:
                        ApplyMentalEffect(hit);
                        break;
                    case PotionType.Heal:
                        ApplyHealEffect(hit);
                        break;
                    case PotionType.Buff:
                        ApplyBuffEffect(hit);
                        break;
                    case PotionType.None:
                    default:
                        // 물약 없음 = 기본 분사 효과 없음
                        break;
                }
            }
        }

        /// <summary>
        /// 공격성(독) 효과: 붉은 안개, 적에게 지속 데미지 5~15
        /// </summary>
        private void ApplyPoisonEffect(Collider target)
        {
            // 적 여부 확인: IDamageable 구현체인지 (적/몬스터)
            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null) return;

            float damage = Random.Range(_minPoisonDamage, _maxPoisonDamage);
            Vector3 hitDir = (target.transform.position - transform.position).normalized;
            damageable.TakeDamage(damage, hitDir, "GasSprayer_Poison");
        }

        /// <summary>
        /// 정신성(마약) 효과: 보라색 안개, 적 환각/혼란 (슬로우 + 혼란 효과)
        /// </summary>
        private void ApplyMentalEffect(Collider target)
        {
            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null) return;

            // IDamageable이면 적으로 간주 — 슬로우/혼란 버프 부여
            if (BuffManager.Instance != null)
            {
                // 슬로우 효과
                BuffManager.Instance.AddBuff("Slowness", _slowAmount, _confusionDuration);
                // 혼란 효과 (커스텀 버프 ID)
                BuffManager.Instance.AddBuff("Confusion", 1f, _confusionDuration);
            }
        }

        /// <summary>
        /// 회복성(치료) 효과: 초록색 안개, 아군 체력 회복
        /// </summary>
        private void ApplyHealEffect(Collider target)
        {
            // 플레이어 자신이거나 아군이면 회복
            if (target.CompareTag("Player"))
            {
                if (PlayerHealth.Instance != null && !PlayerHealth.Instance.IsDead)
                {
                    PlayerHealth.Instance.Heal(_allyHealAmount);
                }
            }
            // 아군 NPC 등은 IDamageable이 아니므로 PlayerHealth만 우선 처리
        }

        /// <summary>
        /// 물리성(강화) 효과: 파란색 안개, 아군 버프 (방어력/공격력 증가)
        /// </summary>
        private void ApplyBuffEffect(Collider target)
        {
            if (target.CompareTag("Player"))
            {
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("DefenseUp", _allyDefenseBuff, _allyBuffDuration);
                    BuffManager.Instance.AddBuff("AttackUp", _allyAttackBuff, _allyBuffDuration);
                }
            }
        }

        // ── Spray Visual Effect ──────────────────────────────────────────

        /// <summary>
        /// 분사 안개 시각 효과 생성 (간단한 사각형 텍스처 기반)
        /// </summary>
        private void SpawnFogEffect()
        {
            if (_cameraTransform == null) return;

            GasSprayerData data = _controller.GetCurrentSprayerData();
            float range = data.sprayRange;
            string potionId = _controller.LoadedPotionId;
            PotionType potionType = ClassifyPotion(potionId);

            // 안개 색상 선택
            Color fogColor = GetFogColor(potionType);
            if (fogColor.a <= 0f) return; // 물약 없음

            // 플레이어 정면 방향으로 랜덤 오프셋
            Vector3 forward = transform.forward;
            Vector3 offset = forward * Random.Range(1f, range * 0.8f)
                           + transform.right * Random.Range(-range * 0.4f, range * 0.4f)
                           + Vector3.up * Random.Range(-0.5f, 1f);

            Vector3 spawnPos = transform.position + Vector3.up * 0.3f + offset;

            // 안개 GameObject 생성
            GameObject fog = new GameObject("GasSprayer_Fog");
            fog.transform.position = spawnPos;
            fog.transform.localScale = Vector3.one * Random.Range(0.5f, 1.2f);

            // MeshFilter + MeshRenderer로 평면 사각형 표시
            var mf = fog.AddComponent<MeshFilter>();
            mf.mesh = CreateQuadMesh();

            var mr = fog.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = fogColor
            };

            // 바라보는 방향 (카메라 향함 - 빌보드 효과)
            fog.transform.LookAt(_cameraTransform);
            fog.transform.Rotate(0, 180, 0);

            // 풀 대신 간단 리스트 추적
            _activeFogs.Add(new SprayFog
            {
                go = fog,
                startTime = Time.time,
                lifetime = _fogLifetime
            });

            // 너무 많은 안개가 쌓이면 가장 오래된 것 제거
            while (_activeFogs.Count > 30)
            {
                var oldest = _activeFogs[0];
                if (oldest.go != null) Destroy(oldest.go);
                _activeFogs.RemoveAt(0);
            }
        }

        /// <summary>
        /// 활성 안개의 수명 업데이트 — 만료된 안개 제거
        /// </summary>
        private void UpdateFogLifetimes()
        {
            float now = Time.time;
            for (int i = _activeFogs.Count - 1; i >= 0; i--)
            {
                if (now - _activeFogs[i].startTime >= _activeFogs[i].lifetime)
                {
                    if (_activeFogs[i].go != null)
                        Destroy(_activeFogs[i].go);
                    _activeFogs.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 분사 중단 시 남은 모든 안개 제거
        /// </summary>
        private void StopSprayEffect()
        {
            foreach (var fog in _activeFogs)
            {
                if (fog.go != null) Destroy(fog.go);
            }
            _activeFogs.Clear();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// 물약 타입 분류 — 아이템 ID(PotionType 접두사) 기반
        /// </summary>
        public static PotionType ClassifyPotion(string potionId)
        {
            if (string.IsNullOrEmpty(potionId))
                return PotionType.None;

            // 접두사 기반 분류
            if (potionId.StartsWith("독_") || potionId.StartsWith("Poison_") || potionId.StartsWith("Poison"))
                return PotionType.Poison;
            if (potionId.StartsWith("마약_") || potionId.StartsWith("Mental_") || potionId.StartsWith("Mental"))
                return PotionType.Mental;
            if (potionId.StartsWith("치료_") || potionId.StartsWith("Heal_") || potionId.StartsWith("Heal")
                || potionId == "HP포션" || potionId.Contains("Potion"))
                return PotionType.Heal;
            if (potionId.StartsWith("강화_") || potionId.StartsWith("Buff_") || potionId.StartsWith("Buff"))
                return PotionType.Buff;

            // 기본값: 아이템에 'Potion' 또는 '물약' 포함 시 Heal로 분류
            if (potionId.Contains("Potion") || potionId.Contains("물약"))
                return PotionType.Heal;

            return PotionType.None;
        }

        /// <summary>
        /// 물약 타입에 따른 안개 색상 반환
        /// </summary>
        private Color GetFogColor(PotionType type)
        {
            return type switch
            {
                PotionType.Poison => _poisonFogColor,
                PotionType.Mental => _mentalFogColor,
                PotionType.Heal   => _healFogColor,
                PotionType.Buff   => _buffFogColor,
                _                 => Color.clear
            };
        }

        /// <summary>
        /// 간단한 Quad 메시 생성 (절차적)
        /// </summary>
        private static Mesh _cachedQuadMesh;
        private static Mesh CreateQuadMesh()
        {
            if (_cachedQuadMesh != null) return _cachedQuadMesh;

            _cachedQuadMesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3( 0.5f, -0.5f, 0),
                    new Vector3(-0.5f,  0.5f, 0),
                    new Vector3( 0.5f,  0.5f, 0)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
                uv = new[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1)
                }
            };
            _cachedQuadMesh.RecalculateNormals();
            return _cachedQuadMesh;
        }

        /// <summary>
        /// 분사 중 효과 처리 비활성화/재시작 시 호출
        /// </summary>
        private void OnDisable()
        {
            StopSprayEffect();
        }

        private void OnDestroy()
        {
            StopSprayEffect();
        }
    }

    /// <summary>
    /// 물약 속성 타입 열거형
    /// </summary>
    public enum PotionType
    {
        None,
        Poison,   // 공격성(독) — 붉은 안개, 적 지속 데미지
        Mental,   // 정신성(마약) — 보라색 안개, 적 환각/혼란
        Heal,     // 회복성(치료) — 초록색 안개, 아군 체력 회복
        Buff      // 물리성(강화) — 파란색 안개, 아군 버프
    }
}
