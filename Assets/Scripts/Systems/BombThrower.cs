using UnityEngine;
using UnityEngine.InputSystem;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 폭탄 투척 시스템: 마우스 가운데 버튼(Mouse2)으로 충전 후 투척, 투척 궤도 미리보기
    /// [207차 P4-2] 호 플루언트 고급화 — ①발사점 투명→착지점 진함 cyan 그라디언트 ②폭 테이퍼 곡선(0.03→0.08)
    /// ③Tile 대시 텍스처 흐름(코드 생성 Texture2D — 절차 파티클 아님, 오프셋 스크롤로 착지 방향 흐름감).
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

        // ── [207차 P4-2] 호 플루언트 상수 — 전부 트림 후보(Unity 육안 확인 후 조정/삭제) ──
        private const float ArcWidthStart = 0.03f;    // 폭 테이퍼 시작(발사점)
        private const float ArcWidthEnd = 0.08f;      // 폭 테이퍼 끝(착지점 — 착지 방향으로 굵어짐)
        private const float DashScrollSpeed = 1.6f;   // 대시 흐름 속도(u/s) — 착지 방향
        private const int DashTexWidth = 32;          // 대시 텍스처 가로 해상도(U축 프로파일)
        private const int DashTexHeight = 4;          // 대시 텍스처 세로(라인 단면 — 균일 알파)
        private Texture2D _dashTex;                   // 코드 생성 대시 텍스처(Repeat 랩)
        private Material _dashMat;                    // 라인 전용 인스턴스 머티리얼(오프셋 스크롤)
        private float _dashOffset;                    // 누적 스크롤 오프셋(음수 누적 = 착지 방향 흐름)

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.enabled = false;
            // LineRenderer 설정 (한 번만)
            // [207차 P4-2] 폭 테이퍼 곡선 — 발사점 0.03 → 착지점 0.08(착지 방향으로 굵어지며 임팩트 유도)
            _lr.widthCurve = AnimationCurve.Linear(0f, ArcWidthStart, 1f, ArcWidthEnd);
            _lr.widthMultiplier = 1f;
            // [207차 P4-2] 대시 텍스처 타일링 — 월드 길이 기준 반복(1 u당 1 타일)
            _lr.textureMode = LineTextureMode.Tile;
            AttachDashMaterial();   // [207차 P4-2] 대시 머티리얼 부착(런타임 전용 — EditMode 가드 내부)
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

            // [207차 P4-2] 호 그라디언트 — 발사점 알파 0(투명)→착지점 알파 1(진함, cyan 유지). 매 프레임 갱신 유지.
            _lr.startColor = new Color(trajectoryColor.r, trajectoryColor.g, trajectoryColor.b, 0f);
            _lr.endColor = trajectoryColor;

            // [207차 P4-2] 대시 흐름 — 텍스처 오프셋 스크롤. 오프셋 감소 = 특징점이 U+로 이동 = 발사→착지 흐름.
            if (_dashMat != null)
            {
                _dashOffset -= DashScrollSpeed * Time.deltaTime;
                _dashOffset = Mathf.Repeat(_dashOffset, 1f);   // 정밀도 위생 — 무한 누적 방지(연속 랩)
                _dashMat.mainTextureOffset = new Vector2(_dashOffset, 0f);
            }
        }

        private void OnDestroy()
        {
            // [207차 P4-2] 런타임 자원 해제(누출 금지 — TelegraphArcIndicator.OnDestroy 선례)
            if (_dashMat != null) Destroy(_dashMat);
            if (_dashTex != null) Destroy(_dashTex);
        }

        /// <summary>[207차 P4-2] 대시 머티리얼 부착 — 런타임 전용(Application.isPlaying 가드: EditMode 계약테스트
        /// 무영향). 셰이더 3단 폴백: ①URP Particles/Unlit(정점색 그라디언트+UV ST 양지원) ②URP Unlit(-ST 지원,
        /// 그라디언트 미지원 폴백) ③Sprites/Default(그라디언트 지원, ST 미지원 폴백). Play에서 사용 셰이더 로그.</summary>
        private void AttachDashMaterial()
        {
            if (!Application.isPlaying) return;   // EditMode 계약테스트 무영향 가드
            if (_lr == null) return;
            if (_dashTex == null) _dashTex = BuildDashTexture();
            if (_dashMat == null)
            {
                string[] candidates = { "Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Unlit", "Sprites/Default" };
                Shader chosen = null;
                foreach (var name in candidates)
                {
                    var s = Shader.Find(name);
                    if (s != null) { chosen = s; break; }
                }
                _dashMat = new Material(chosen != null ? chosen : Shader.Find("Sprites/Default"));
                _dashMat.name = "BombArcDashMat[207차 P4-2]";
                _dashMat.mainTexture = _dashTex;
                Debug.Log($"[BombThrower][207차 P4-2] 대시 머티리얼 셰이더={_dashMat.shader.name} (그라디언트+흐름 Play 체감 후 트림)");
            }
            _lr.material = _dashMat;
        }

        /// <summary>[207차 P4-2] 대시 텍스처 코드 생성 — 32×4, U축 18px 대시/14px 간격, cyan(trajectoryColor) 기반.
        /// ParticleSystem 아닌 텍스처 — 절차 파티클 금지 계약(BombVfxTests) 비충돌.</summary>
        private Texture2D BuildDashTexture()
        {
            var tex = new Texture2D(DashTexWidth, DashTexHeight, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            var px = new Color32[DashTexWidth * DashTexHeight];
            byte r = (byte)(trajectoryColor.r * 255f), g = (byte)(trajectoryColor.g * 255f), b = (byte)(trajectoryColor.b * 255f);
            for (int x = 0; x < DashTexWidth; x++)
            {
                byte a = x < 18 ? (byte)255 : (byte)0;   // 0~17 대시 / 18~31 간격
                for (int y = 0; y < DashTexHeight; y++)
                    px[y * DashTexWidth + x] = new Color32(r, g, b, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
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