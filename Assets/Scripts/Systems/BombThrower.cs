using UnityEngine;
using UnityEngine.InputSystem;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 폭탄 투척 시스템: 마우스 가운데 버튼(Mouse2)으로 충전 후 투척, 투척 궤도 미리보기
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class BombThrower : MonoBehaviour
    {
        [Header("Bomb Settings")]
        // Bomb prefabs will be loaded from Resources/Bombs
        private GameObject[] _bombPrefabs;

        [Header("Throw Settings")]
        public float minThrowForce = 5f;
        public float maxThrowForce = 15f;
        public float chargeTime = 1f; // 최대 충전 시간
        public float aimHeight = 1f; // 발사 시작 높이 (플레이어 머리 정도)

        [Header("Trajectory Preview")]
        public int trajectoryResolution = 30;
        public float trajectoryTimeStep = 0.1f; // 각 점 사이의 시간 간격
        public Color trajectoryColor = Color.cyan;
        private LineRenderer _lr;
        private Camera _mainCamera;
        private float _chargeStartTime;
        private bool _isCharging;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.enabled = false;
            // LineRenderer 설정 (한 번만)
            _lr.startWidth = 0.05f;
            _lr.endWidth = 0.05f;
            _mainCamera = Camera.main;
            // Load bomb prefabs from Resources/Bombs
            _bombPrefabs = Resources.LoadAll<GameObject>("Bombs");
            if (_bombPrefabs == null || _bombPrefabs.Length == 0)
            {
                Debug.LogWarning("No bomb prefabs found in Resources/Bombs. Make sure to create them via Tools/Create Bomb Prefabs.");
            }
        }

        private void Update()
        {
            HandleInput();
            UpdateTrajectoryPreview();
        }

        private void HandleInput()
        {
            if (Mouse.current == null) return;

            // 가운데 버튼(Mouse2)으로 충전 시작
            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                StartCharge();
            }
            // 충전 중 (이미 시작됨, 단순 대기)
            // 가운데 버튼 릴리스 -> 투척
            if (Mouse.current.middleButton.wasReleasedThisFrame && _isCharging)
            {
                ReleaseThrow();
            }
        }

        private void StartCharge()
        {
            _isCharging = true;
            _chargeStartTime = Time.time;
            _lr.enabled = true;
        }

        private void ReleaseThrow()
        {
            if (!_isCharging) return;

            float chargeDuration = Time.time - _chargeStartTime;
            chargeDuration = Mathf.Clamp01(chargeDuration / chargeTime);
            float throwForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargeDuration);

            // 발사 방향 계산 (마우스 커서 기준)
            Vector3 aimDirection = GetAimDirection();
            if (aimDirection == Vector3.zero)
            {
                // 유효한 방향이 아니면 기본 전방으로
                aimDirection = transform.forward;
            }

            // 현재 폭탄 종류 선택 (추후 UI로 변경 가능, 지금은 첫 번째 폭탄 사용)
            BombType currentType = BombType.Explosive; // 임시
            GameObject bombPrefab = GetBombPrefab(currentType);
            if (bombPrefab == null)
            {
                Debug.LogError("Bomb prefab not assigned for type: " + currentType);
                _isCharging = false;
                _lr.enabled = false;
                return;
            }

            // 폭탄 인스턴스화
            Vector3 spawnPos = transform.position + Vector3.up * aimHeight;
            GameObject bombObj = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
            // 초기 속도 적용
            Rigidbody rb = bombObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = aimDirection * throwForce;
                // 약간의 위쪽으로 초기 속도 추가 (구르기 방지)
                rb.linearVelocity += Vector3.up * 2f;
            }
            else
            {
                Debug.LogWarning("Bomb prefab missing Rigidbody");
            }

            // 충전 상태 초기화
            _isCharging = false;
            _lr.enabled = false;
        }

        private Vector3 GetAimDirection()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                    return Vector3.zero;
            }

            // 마우스 커서의 월드 위치를 지면 평면(y=0)에서 찾음
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, 0f); // y=0 평면
            float distance;
            if (groundPlane.Raycast(ray, out distance))
            {
                Vector3 worldPoint = ray.GetPoint(distance);
                Vector3 direction = worldPoint - transform.position;
                direction.y = 0f; // 수평 성분만 사용
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized;
                }
            }
            return Vector3.zero;
        }

        private void UpdateTrajectoryPreview()
        {
            if (!_isCharging)
            {
                _lr.enabled = false;
                return;
            }

            // 충전 비율에 따른 힘 계산
            float chargeDuration = Time.time - _chargeStartTime;
            chargeDuration = Mathf.Clamp01(chargeDuration / chargeTime);
            float throwForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargeDuration);

            // 발사 방향
            Vector3 launchDir = GetAimDirection();
            if (launchDir == Vector3.zero)
            {
                launchDir = transform.forward;
                launchDir.y = 0f;
            }

            Vector3 launchPos = transform.position + Vector3.up * aimHeight;
            Vector3 launchVelocity = launchDir * throwForce + Vector3.up * 2f; // 약간의 위쪽 속도

            // 궤도 점 계산
            _lr.positionCount = trajectoryResolution;
            for (int i = 0; i < trajectoryResolution; i++)
            {
                float t = i * trajectoryTimeStep;
                Vector3 pos = CalculateTrajectoryPoint(launchPos, launchVelocity, t);
                _lr.SetPosition(i, pos);
                // 점이 지면 아래로 떨어지면 중단
                if (pos.y < 0f)
                {
                    _lr.positionCount = i + 1;
                    break;
                }
            }

            // 라인 renderer 색상 설정 (매 프레임 갱신)
            _lr.startColor = trajectoryColor;
            _lr.endColor = new Color(trajectoryColor.r, trajectoryColor.g, trajectoryColor.b, 0.5f);
        }

        private Vector3 CalculateTrajectoryPoint(Vector3 start, Vector3 velocity, float time)
        {
            // 등속도 운동 + 중력
            Vector3 pos = start + velocity * time;
            pos.y += Physics.gravity.y * time * time * 0.5f;
            return pos;
        }

        private GameObject GetBombPrefab(BombType type)
        {
            int index = (int)type;
            if (_bombPrefabs != null && index >= 0 && index < _bombPrefabs.Length)
                return _bombPrefabs[index];
            return null;
        }
    }
}