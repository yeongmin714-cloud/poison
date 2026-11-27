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
    ///   2. 무장 상태 좌클릭 press → BeginAim(궤적/착지 프리뷰) → release → ThrowTowardCursor [189차]
    ///      (커서 방향 포물선 발사 — 활 드로→릴리즈와 동일한 press/릴리즈 위계)
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
        public float bombDamage = 25f;        // [193차] 폭탄 중심 피해량 폴백 — ItemData.bombDamage 우선 주입

        public bool IsArmed => _isArmed;
        public PlayerInventory.ItemData ArmedBomb => _armedBomb;
        /// <summary>이번 프레임에 이미 폭탄을 투척했는지 — PlayerCombat/AttackSystem 중복 처리 방지용.</summary>
        public static bool BombThrowIssuedThisFrame { get; private set; }

        private bool _isArmed;
        private PlayerInventory.ItemData _armedBomb;
        private GameObject _handVisual;

        // [185차] 던지기 연출 — ThrowObj 애니 윈드업 구간 후 릴리스(Play 실측으로 미세 조정 가능).
        internal const float ThrowReleaseDelaySeconds = 0.35f;
        private bool _isThrowPending;   // 투척 애니 진행 중 — 연타 재발화 금지

        // 손 고정점 (손에 쥔 폭탄 표시 위치) — 없으면 플레이어 기준 사용
        private Transform _handAnchor;

        // [189차] 조준 상태 — press=조준(궤적 프리뷰) → release=투척(활 드로→릴리즈 선례와 동일 패턴)
        private bool _isAiming;
        /// <summary>[189차] 조준 중 여부 — 좌클릭 홀드 동안 궤적/착지 프리뷰 표시, 릴리즈 시 ThrowTowardCursor.</summary>
        public bool IsAiming => _isAiming;

        // [189차] 조준 프리뷰 오브젝트 — BeginAim에서 지연 생성, CancelAim/투척 시 파괴(결정론: Random 미사용)
        private GameObject _aimArcGo;
        private LineRenderer _aimArcLine;
        private GameObject _aimLandingGo;
        private LineRenderer _aimLandingLine;
        private const int AimArcSampleCount = 32;    // 궤적 샘플 수(~32 위치)
        private const int AimLandingSegments = 48;   // 착지 링 세그먼트 수
        private const float AimSampleDt = 1f / 24f;  // 샘플 간격(초)
        private const float AimMaxFlightTime = 3.0f; // 시뮬레이션 상한(초)
        // 확립된 FX 골드 패밀리 — 따뜻한 금색(패망 개방 링 등 기존 골드 FX와 동일 톤)
        private static readonly Color AimPreviewGold = new Color(1f, 0.85f, 0.45f);
        private static readonly Color AimPreviewGoldRing = new Color(1f, 0.85f, 0.45f, 0.8f);
        // [193차] 플루언트 프리뷰 — 도트 흐름 + 착지 내부 필 + 폭발 범위 빨간색(위험 구역 — 사용자 지시)
        private GameObject[] _aimDots;        // 궤적 흐름 도트 (AimDotCount개)
        private GameObject _aimLandingFill;   // 착지 내부 필 (투명 빨간 디스크)
        private const int AimDotCount = 3;    // 흐름 도트 수
        private const float AimDotSize = 0.07f;   // 도트 크기(m)
        private const float AimDotFlowSpeed = 0.8f;  // 도트 흐름 속도(경로/초)
        private const float AimPulseSpeed = 4f;      // 착지 링 펄스 속도
        private const float AimPulseAmplitude = 0.06f;  // 펄스 진폭(반지름 ±6%)
        private static readonly Color AimPreviewRed = new Color(1f, 0.25f, 0.2f, 0.85f);
        private static readonly Color AimPreviewRedFill = new Color(1f, 0.25f, 0.2f, 0.18f);
        // [195차 P6] 착지 링 스케일 펄스 — base*(1+A*sin(2π*Hz*t)), 라인 알파 동조
        private const float LandingPulseAmplitude = 0.05f;   // 펄스 진폭(±5%)
        private const float LandingPulseHz = 2f;             // 펄스 주파수(Hz)

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

        /// <summary>[189차] 조준 중 매 프레임 궤적/착지 프리뷰 갱신 — 커서 방향 따라 실시간 재계산.</summary>
        private void Update()
        {
            if (!_isAiming) return;
            UpdateAimPreview();
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

            // [193차] 소비품 개수 게이트 — 0개 무장 거부 + 피드백(상호작용불가=피드백필수 계약) → [205차] 알림 제거(사용자 지시: "폭탄 부족시 따로 알림 안떠도 괜찮고") — 조용한 거부(Debug.Log+return 유지)
            int count = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemCount(item.id) : 0;
            if (count <= 0)
            {
                Debug.Log($"[BombArm] {item.displayName} 무장 거부 — 개수 0");
                return;
            }

            _armedBomb = item;
            _isArmed = true;
            CreateHandVisual(item);
            Debug.Log($"[BombArm] 💣 {item.displayName} 무장 완료 — 좌클릭으로 투척 (잔여: {count})");
        }

        /// <summary>손 표시 제거 + 무장 해제 (내려놓기).</summary>
        public void Disarm()
        {
            CancelAim();   // [189차] 해제 시 조준 프리뷰 즉시 정리(프리뷰 잔류 방지)
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
            CancelAim();   // [189차] 투척 시작 = 조준 종료 — 릴리즈 경로 포함, 프리뷰 즉시 파괴

            if (!_isArmed || _armedBomb == null) return;
            if (_isThrowPending) return;   // [185차] 투척 애니 진행 중 — 연타 재발화 금지

            BombThrowIssuedThisFrame = true; // 이번 프레임 폭탄 투척 처리됨 — 이중 공격 방지

            // [185차] 던지기 애니메이션 발화 — Throw 트리거 → ThrowObj 상태.
            //   폭탄 릴리스는 윈드업 구간 이후 지연 스폰(ReleaseBombNow) — 애니가 나오고 나서 던져진다.
            var driver = GetComponent<HumanoidClipDriver>()
                ?? GetComponentInChildren<HumanoidClipDriver>();   // ConstructionManager 조회 선례
            if (driver != null)
            {
                _isThrowPending = true;
                driver.TriggerThrow();
                StartCoroutine(ThrowReleaseRoutine());
                return;
            }

            // 드라이버 부재(테스트/폴백) — 기존 즉시 투척 유지
            ReleaseBombNow();
        }

        /// <summary>[185차] ThrowObj 윈드업 구간 대기 후 릴리스 — 지연 중 해제돼도 다음 릴리스 판정은 본체에서.</summary>
        private System.Collections.IEnumerator ThrowReleaseRoutine()
        {
            yield return new WaitForSeconds(ThrowReleaseDelaySeconds);
            ReleaseBombNow();
        }

        // ================================================================
        // [189차] 조준(에임 프리뷰) — press=조준 시작 / release=투척
        // ================================================================

        /// <summary>
        /// [189차] 조준 시작 — 무장 상태 좌클릭 press에서 호출. 실제 투척은 릴리즈(ThrowTowardCursor)에서.
        /// 궤적/착지 프리뷰를 지연 생성해 표시한다(활 드로→릴리즈와 동일 위계).
        /// </summary>
        public void BeginAim()
        {
            if (!_isArmed || _armedBomb == null) return;   // 무장 상태가 아니면 조준 불가
            _isAiming = true;
            EnsureAimPreviewObjects();
            UpdateAimPreview();   // 생성 프레임에도 즉시 1회 갱신(빈 프레임 방지)
        }

        /// <summary>[189차] 조준 취소 — 조준 상태 해제 + 프리뷰 오브젝트 파괴.</summary>
        public void CancelAim()
        {
            _isAiming = false;
            DestroyAimPreviewObjects();
        }

        /// <summary>[189차] 프리뷰 오브젝트 지연 생성 — 궤적 아크 + 착지 링(자기 자식, 월드좌표 라인).</summary>
        private void EnsureAimPreviewObjects()
        {
            if (_aimArcGo == null)
            {
                _aimArcGo = new GameObject("BombAimPreview_Arc");
                _aimArcGo.transform.SetParent(transform, false);
                _aimArcLine = _aimArcGo.AddComponent<LineRenderer>();
                ConfigureAimLine(_aimArcLine, AimArcSampleCount, 0.05f, AimPreviewGold, loop: false);
                // [193차] 호 그라디언트 — 손(선명) → 착지(페이드) 비행 방향 흐름
                _aimArcLine.startColor = AimPreviewGold;
                _aimArcLine.endColor = new Color(AimPreviewGold.r, AimPreviewGold.g, AimPreviewGold.b, 0.15f);
                // [195차 P6] 호 그레이딩 — widthCurve(시작 두께 → 끝 60% 감쇠) + colorGradient(선두 알파 1.0 → 끝단 0.35 글로우).
                //   웜골드 계열 유지(AimPreviewGold 재사용), 점 배열 로직 무수정. 그레이디언트가 단색 페이드 설정을 시각적으로 승계(193차 문자열 계약은 그대로 유지).
                _aimArcLine.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(1f, 0.4f));   // 끝 두께 = 시작의 40%(60% 감쇠)
                _aimArcLine.widthMultiplier = 0.05f;   // 호 기본 두께(ConfigureAimLine 호 width 동일값)
                var arcGrade = new Gradient();
                arcGrade.SetKeys(
                    new[] { new GradientColorKey(AimPreviewGold, 0f), new GradientColorKey(AimPreviewGold, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.35f, 1f) });
                _aimArcLine.colorGradient = arcGrade;
            }
            if (_aimLandingGo == null)
            {
                _aimLandingGo = new GameObject("BombAimPreview_Landing");
                _aimLandingGo.transform.SetParent(transform, false);
                _aimLandingLine = _aimLandingGo.AddComponent<LineRenderer>();
                // [193차] 폭발 범위 = 빨간색 (위험 구역 — 사용자 지시 "폭발 범위는 빨간색")
                ConfigureAimLine(_aimLandingLine, AimLandingSegments, 0.06f, AimPreviewRed, loop: true);
            }
            EnsureAimDots();         // [193차] 흐름 도트
            EnsureAimLandingFill();  // [193차] 착지 내부 필
        }

        /// <summary>[193차] 궤적 흐름 도트 — 라인 위를 시간 기반으로 흐르는 작은 골드 점(프리미티브, 충돌 없음).</summary>
        private void EnsureAimDots()
        {
            if (_aimDots != null) return;
            _aimDots = new GameObject[AimDotCount];
            for (int i = 0; i < AimDotCount; i++)
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = $"BombAimPreview_Dot_{i}";
                var col = dot.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);   // 장식용 — 충돌 없음(CreateHandVisual 선례)
                dot.transform.SetParent(_aimArcGo.transform, false);
                dot.transform.localScale = Vector3.one * AimDotSize;
                var ren = dot.GetComponent<MeshRenderer>();
                if (ren != null) ren.material = CreateUnlitTransparentMaterial(AimPreviewGold);
                _aimDots[i] = dot;
            }
        }

        /// <summary>[193차] 착지 내부 필 — 폭발 범위 내부를 채우는 투명 빨간 디스크(프리미티브 납작 스피어).</summary>
        private void EnsureAimLandingFill()
        {
            if (_aimLandingFill != null) return;
            var fill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fill.name = "BombAimPreview_Fill";
            var col = fill.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            fill.transform.SetParent(_aimLandingGo.transform, false);
            fill.transform.localScale = new Vector3(2f, 0.004f, 2f);   // 납작 디스크 — 반지름은 UpdateLandingRing에서 동기
            var ren = fill.GetComponent<MeshRenderer>();
            if (ren != null) ren.material = CreateUnlitTransparentMaterial(AimPreviewRedFill);
            _aimLandingFill = fill;
        }

        /// <summary>[189차] 라인 렌더러 공통 설정 — 월드좌표/그림자 off/언릿 투명 머티리얼.</summary>
        private void ConfigureAimLine(LineRenderer line, int positionCount, float width, Color color, bool loop)
        {
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = positionCount;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            line.material = CreateUnlitTransparentMaterial(color);
        }

        /// <summary>
        /// [189차] 언릿 투명 머티리얼 — URP(Universal Render Pipeline/Unlit) 우선, 없으면 Sprites/Default 폴백,
        /// 둘 다 없으면 null 가드(라인 렌더러 기본 머티리얼로 렌더).
        /// </summary>
        private static Material CreateUnlitTransparentMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;   // 셰이더 부재 가드 — null 머티리얼 허용

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            else mat.color = color;

            // URP Unlit 투명 전환(프로퍼티 존재 시에만) — Sprites/Default는 알파 블렌드 내장
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);   // 1 = Transparent
                mat.SetOverrideTag("RenderType", "Transparent");
                if (mat.HasProperty("_SrcBlend"))
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend"))
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return mat;
        }

        /// <summary>
        /// [189차] 궤적 시뮬레이션 — ReleaseBombNow와 동일 초기 조건(aimDir*throwForce + up*upBoost)으로
        /// p(t) = p0 + v*t + 0.5*g*t² 등간격 샘플링. 첫 y≤0.02 지점 = 예측 착지점(폭발 반경 링 표시).
        /// </summary>
        private void UpdateAimPreview()
        {
            if (_aimArcLine == null || _aimLandingLine == null) return;   // 파괴됨 — 다음 BeginAim에서 재생성

            Vector3 aimDir = GetAimDirection();
            if (aimDir.sqrMagnitude < 0.0001f) aimDir = transform.forward;
            aimDir.y = 0f;
            aimDir.Normalize();
            if (aimDir.sqrMagnitude < 0.0001f) aimDir = transform.forward;

            // 발사 시작점 — 손 고정점이 실존(자기 자신 아님)하면 손 높이(+0.15), 없으면 throwAimHeight
            bool hasHandAnchor = _handAnchor != null && _handAnchor != transform;
            Vector3 p0 = hasHandAnchor
                ? _handAnchor.position + Vector3.up * 0.15f
                : transform.position + Vector3.up * throwAimHeight;
            Vector3 velocity = aimDir * throwForce + Vector3.up * upBoost;

            var points = new Vector3[AimArcSampleCount];   // 프리뷰 전용 소형 할당 — 단순/결정론 우선
            int count = 0;
            Vector3 landing = Vector3.zero;
            bool landingFound = false;
            for (int i = 0; i < AimArcSampleCount; i++)
            {
                float t = i * AimSampleDt;
                if (t > AimMaxFlightTime) break;
                Vector3 p = p0 + velocity * t + 0.5f * Physics.gravity * t * t;
                points[count++] = p;
                if (p.y <= 0.02f)   // 지면 도달 — 이 지점이 예측 착지점
                {
                    landing = p;
                    landingFound = true;
                    break;
                }
            }

            _aimArcLine.positionCount = count;
            for (int i = 0; i < count; i++)
                _aimArcLine.SetPosition(i, points[i]);

            // [193차] 도트 흐름 — 궤적을 따라 시간 기반 이동(경로/초 위상, 결정론 아님: 연출 전용)
            if (_aimDots != null && count > 2)
            {
                float flight = Time.unscaledTime * AimDotFlowSpeed;
                for (int i = 0; i < AimDotCount; i++)
                {
                    if (_aimDots[i] == null) continue;
                    float t = Mathf.Repeat(flight + i / (float)AimDotCount, 1f);
                    int idx = Mathf.Clamp(Mathf.FloorToInt(t * (count - 1)), 0, count - 1);
                    _aimDots[i].transform.position = points[idx];
                }
            }

            if (landingFound)
            {
                _aimLandingGo.SetActive(true);
                UpdateLandingRing(landing);
            }
            else
            {
                _aimLandingGo.SetActive(false);   // 상한 내 착지 미도달 — 링 숨김
            }
        }

        /// <summary>[189차] 착지 링 — 예측 착지점 중심 폭발 반경 원(지면 y=0.05, z-fight 방지).</summary>
        private void UpdateLandingRing(Vector3 landing)
        {
            const float ringY = 0.05f;
            // [193차] 펄스 링 — 반지름 ±6% 진동(호흡 느낌) + 내부 필 동기
            float pulse = 1f + AimPulseAmplitude * Mathf.Sin(Time.unscaledTime * AimPulseSpeed);
            float radius = explosionRadius * pulse;
            // [195차 P6] 착지 링 스케일 펄스 — base*(1+A*sin(2π*Hz*t)) → 링 오브젝트 스케일 + 라인 알파 동조.
            //   라인은 useWorldSpace라 트랜스폼 스케일 무시 → 알파 동조로 시각 동기, 내부 필(자식 메시)은 스케일 승계.
            float ringScale = 1f + LandingPulseAmplitude * Mathf.Sin(2f * Mathf.PI * LandingPulseHz * Time.unscaledTime);
            if (_aimLandingGo != null) _aimLandingGo.transform.localScale = Vector3.one * ringScale;
            if (_aimLandingLine != null && _aimLandingLine.material != null)
            {
                var c = AimPreviewRed;
                _aimLandingLine.material.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * ringScale));
            }
            for (int i = 0; i < AimLandingSegments; i++)
            {
                float angle = (i / (float)AimLandingSegments) * Mathf.PI * 2f;
                float x = landing.x + Mathf.Cos(angle) * radius;
                float z = landing.z + Mathf.Sin(angle) * radius;
                _aimLandingLine.SetPosition(i, new Vector3(x, ringY, z));
            }
            // [193차] 내부 필 — 펄스 반지름 동기(폭발 범위 내부 채움)
            if (_aimLandingFill != null)
            {
                float d = radius * 2f;
                _aimLandingFill.transform.localScale = new Vector3(d, 0.004f, d);
            }
        }

        /// <summary>[189차] 프리뷰 오브젝트 파괴 — CancelAim/투척 시작/해제 경로에서 호출.</summary>
        private void DestroyAimPreviewObjects()
        {
            if (_aimArcGo != null) Object.Destroy(_aimArcGo);
            if (_aimLandingGo != null) Object.Destroy(_aimLandingGo);
            _aimArcGo = null;
            _aimArcLine = null;
            _aimLandingGo = null;
            _aimLandingLine = null;
            _aimDots = null;      // [193차] 도트 — 부모(_aimArcGo) 파괴로 소멸, 참조만 정리
            _aimLandingFill = null;   // [193차] 필 — 부모(_aimLandingGo) 파괴로 소멸
        }

        /// <summary>[185차] 지연 릴리스 본체 — 조준 재계산(클릭 시점 아님) → 스폰 → 소모 → 해제.</summary>
        private void ReleaseBombNow()
        {
            _isThrowPending = false;
            if (!_isArmed || _armedBomb == null) return;

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
            bomb.bombDamage = _armedBomb != null && _armedBomb.bombDamage > 0f ? _armedBomb.bombDamage : bombDamage;   // [193차] 스탯 기반 피해량 주입
            bomb.explosionEffectPrefab = null;
            // [189차] 폭발 SFX 배선 — 기존 null(무음)에서 절차 합성 폭발음 로드로 교체
            // (Assets/Resources/Audio/SFX/SFX_Bomb_Explosion.wav). Bomb.Explode가 PlayClipAtPoint로 재생.
            bomb.explosionSound = Resources.Load<AudioClip>("Audio/SFX/SFX_Bomb_Explosion");

            // 폭발 시각 — Bomb.Explode가 이 오브젝트를 Destroy하면 OnDisable에서 스파크 발화
            go.AddComponent<BombExplosionVisual>();

            // [201차] 투척 폭탄 GLB 비주얼 — 구형(Sphere)을 물리 전용으로 유지하고 시각은 bomb GLB로 대체.
            //   로드 실패 시 구형 렌더 유지 무음 폴백(177차 발사체 시각 폴백 계약 준용).
            AttachThrownBombVisual(go);
            // [205차] 투척 트레일 — 비행 중 회갈색 연기 퍼프 + 소형 주황 스파크(절차 PS, 폭탄 자식 = 폭발 파괴 시 자동 소멸).
            AttachThrowTrail(go);
            // [195차 P6] 폭발 임팩트 — 퓨즈 만료(폭탄 GO 파괴 = Explode 자기 파괴) 시점 히트스톱+카메라 셰이크 1회.
            //   AnimalAI.cs L1137-1138 선례 패턴: Application.isPlaying 가드 + RequestHitStop 1줄.
            StartCoroutine(ExplosionImpactRoutine(go));
        }

        // ────── [201차] 투척 폭탄 GLB 비주얼 ──────

        static GameObject _thrownBombVisual;   // 캐시(1회 로드 — 폴백 체인 결과 보존)
        static bool _thrownBombVisualSearched;

        /// <summary>
        /// 투척 폭탄에 GLB 비주얼 부착 — 구형 지름(=bombScale)에 최대축 정규화 후 구형 MeshRenderer만 OFF.
        /// 콜라이더/Rigidbody/Bomb 파이프라인 무손상. GLB 부재·bounds 무효 시 구형 렌더 유지(무음 폴백).
        /// </summary>
        void AttachThrownBombVisual(GameObject bombGo)
        {
            if (!_thrownBombVisualSearched)
            {
                _thrownBombVisualSearched = true;
                _thrownBombVisual = Resources.Load<GameObject>("Models/UserProvided/bomb_explosive");
                if (_thrownBombVisual == null)
                    _thrownBombVisual = Resources.Load<GameObject>("Models/UserProvided/bomb");   // 폴백 체인
            }
            if (_thrownBombVisual == null)
            {
                Debug.Log("[BombArmController] 투척 폭탄 GLB 부재 — 구형 시각 유지(무음 폴백)");
                return;
            }

            var vis = Instantiate(_thrownBombVisual, bombGo.transform);
            vis.name = "BombVisual";
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;

            // 정규화 — GLB 월드 최대축을 구형 지름(bombScale)에 맞춤(부모 스케일은 곱연산으로 상쇄됨)
            var rends = vis.GetComponentsInChildren<Renderer>();
            float maxDim = 0f;
            foreach (var r in rends)
            {
                if (r == null || r.bounds.size == Vector3.zero) continue;
                maxDim = Mathf.Max(maxDim, Mathf.Max(r.bounds.size.x, Mathf.Max(r.bounds.size.y, r.bounds.size.z)));
            }
            if (maxDim <= 0.001f)
            {
                Destroy(vis);   // bounds 무효 — 구형 유지
                return;
            }
            vis.transform.localScale = vis.transform.localScale * (bombScale / maxDim);

            // 구형 렌더 OFF(콜라이더는 SphereCollider 그대로 — 물리 무손상)
            var mr = bombGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }

        // ────── [205차] 투척 트레일 — 상수 고정(플레이 실측 후 트림 대기 주석) ──────
        // [206차] 상수 인상 — 비행 잔상 가시성 강화(연기 2× 수명·1.7× 크기·2× 레이트, 스파크 2× 수명·2× 레이트).

        internal const string ThrowTrailChildName = "Bomb_Trail";   // 트레일 호스트 자식명(계약테스트 핀)
        private const float TrailSmokeLifetime = 1.2f;    // [206차] 연기 입자 수명(초) — 0.6→1.2 인상
        private const float TrailSmokeSize = 0.25f;       // [206차] 연기 입자 크기(m) — 0.15→0.25 인상
        private const float TrailSmokeRate = 48f;         // [206차] 연기 에미션(개/초) — 24→48 인상
        private const float TrailSmokeRise = 0.08f;       // 미약 상승(gravityModifier 음수 부여) — [206차] 무수정
        private const float TrailSparkLifetime = 0.5f;    // [206차] 스파크 입자 수명(초) — 0.25→0.5 인상
        private const float TrailSparkSize = 0.05f;       // 스파크 입자 크기(m) — [206차] 무수정
        private const float TrailSparkRate = 24f;         // [206차] 스파크 에미션(개/초) — 12→24 인상
        private static readonly Color TrailSmokeColor = new Color(0.55f, 0.50f, 0.44f, 0.45f); // 회갈색 연기
        private static readonly Color TrailSparkColor = new Color(1f, 0.55f, 0.15f, 0.95f);    // 주황 스파크
        private static Texture2D _trailPuffTex;   // 트레일 공유 소프트 서클 텍스처(캐시 1회 — 세션 생존)
        private static Material _trailSmokeMat;   // 트레일 공유 머티리얼(캐시 1회 — 스폰별 누출 원천 제거)
        private static Material _trailSparkMat;

        /// <summary>
        /// [205차] 투척 트레일 부착 — 폭탄 자식 "Bomb_Trail"에 절차 ParticleSystem 2개(회갈색 연기 퍼프 + 소형 주황 스파크).
        /// 월드 시뮬레이션 공간으로 폭탄 비행 궤적에 잔상을 남긴다. 트레일은 폭탄 자식 — 폭발 파괴 시 자동 소멸
        /// (별도 수명 Destroy 불필요). EditMode 가드(21-K1 선례 — 런타임 전용 경로), 생성 실패/파티클 부재 시 무음 폴백.
        /// [206차] 폭탄 GO에 TrailRenderer 리본 신설 — AttachThrowTrailRibbon(PS 잔상과 공존).
        /// </summary>
        private void AttachThrowTrail(GameObject bombGo)
        {
            if (!Application.isPlaying) return;   // EditMode 가드 — 런타임 전용
            if (bombGo == null) return;

            var host = new GameObject(ThrowTrailChildName);
            host.transform.SetParent(bombGo.transform, false);
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;
            // 부모(폭탄 bombScale 0.4) 스케일 상쇄 — 입자 크기를 월드 미터(TrailSmokeSize 0.25m) 그대로 유지
            float parentScale = bombGo.transform.lossyScale.x;
            float inv = parentScale > 0.0001f ? 1f / parentScale : 1f;
            host.transform.localScale = Vector3.one * inv;

            ConfigureTrailParticleSystem(host, "Bomb_Trail_Smoke", TrailSmokeColor, EnsureTrailPuffTexture(),
                TrailSmokeLifetime, TrailSmokeSize, TrailSmokeRate, TrailSmokeRise, ref _trailSmokeMat, "BombTrailSmokeMat");
            ConfigureTrailParticleSystem(host, "Bomb_Trail_Spark", TrailSparkColor, EnsureTrailPuffTexture(),
                TrailSparkLifetime, TrailSparkSize, TrailSparkRate, 0.05f, ref _trailSparkMat, "BombTrailSparkMat");

            // [206차] 트레일 리본 — 폭탄 GO TrailRenderer(잔상 0.45s, 폭 0.22→0.02 테이퍼, 주황→투명 그라디언트)
            AttachThrowTrailRibbon(bombGo);
        }

        /// <summary>[205차] 트레일 파티클 시스템 공통 구성 — 소프트 파티클 머티리얼 + 월드 공간 + 상수 에미션(트림 대기).
        /// 머티리얼은 정적 캐시 공유(스폰별 누출 없음), 파티클/머티리얼 부재 시 무음 폴백(null 가드만).</summary>
        private static void ConfigureTrailParticleSystem(GameObject host, string childName, Color color,
            Texture2D tex, float lifetime, float size, float rate, float rise, ref Material cachedMat, string matName)
        {
            if (host == null) return;

            var child = new GameObject(childName);
            child.transform.SetParent(host.transform, false);
            var ps = child.AddComponent<ParticleSystem>();
            if (ps == null) { Object.Destroy(child); return; }   // 파티클 부재 — 무음 폴백+즉시 정리
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // [무번호 48-T1] 자동 재생 정지 선례

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;              // 퓨즈 동안 지속 에미션 — 폭탄 파괴 시 트레일 호스트 동반 소멸
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;          // 입자 제자리 부양 — 폭탄 이동으로 트레일 형성
            main.startSize = size;
            main.gravityModifier = -rise;  // 미약 상승(회갈색 퍼프 — [205차])
            main.maxParticles = 256;   // [206차] 96→256 인상 — 수명·레이트 인상에 따른 파티클 풀 상향
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // 월드 고정 — 비행 잔상
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = rate;        // 상수 고정 — [205차] 트림 대기 주석
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(grad);

            // 공유 머티리얼 캐시 — 세션당 1회 생성(스폰별 누출 없음), 부재 시 무음 폴백(파티클 기본 머티리얼 렌더)
            if (cachedMat == null && tex != null)
                cachedMat = FxMaterialLibrary.CreateSoftParticle(matName, color, tex);
            if (cachedMat != null)
            {
                var rnd = child.GetComponent<ParticleSystemRenderer>();
                FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { cachedMat }, "BombThrowTrail");
            }

            ps.Play(true);
        }

        // ────── [206차] 투척 트레일 리본 — TrailRenderer ──────

        /// <summary>[206차] 리본 주황(머리색 — 그라디언트 키와 동일 톤, 계약테스트 핀용 아님 — 내부 상수).</summary>
        private static readonly Color TrailRibbonColor = new Color(1f, 0.55f, 0.15f, 0.9f);   // 주황

        /// <summary>
        /// [206차] 투척 트레일 리본 — 폭탄 GO에 TrailRenderer 추가(잔상 시간 0.45s, 폭 테이퍼 0.22→0.02m,
        /// 주황→투명 컬러 그라디언트, 카메라 빌보드 정렬). 머티리얼은 미공유 신규 인스턴스(CreateSoftParticle
        /// 호출별 신규 생성 — 공유 캐시 금지), 폭탄 GO 파괴(폭발) 시 DestroyRibbonMaterialRoutine이 동행 소멸(누수 0).
        /// TrailRenderer 자체는 폭탄 GO 직속 컴포넌트 — Bomb_Trail 정리 패턴과 동일하게 별도 수명 Destroy 불필요.
        /// 컴포넌트/머티리얼 부재 시 무음 폴백(null 가드만). EditMode 가드는 AttachThrowTrail 진입부 공유.
        /// </summary>
        private void AttachThrowTrailRibbon(GameObject bombGo)
        {
            var tr = bombGo.GetComponent<TrailRenderer>();
            if (tr == null) tr = bombGo.AddComponent<TrailRenderer>();
            if (tr == null) return;   // 컴포넌트 부재 — 무음 폴백
            tr.time = 0.45f;                                  // 리본 잔상 시간(초)
            tr.widthCurve = AnimationCurve.Linear(0f, 0.22f, 1f, 0.02f);   // 폭 테이퍼 — 머리 0.22m → 꼬리 0.02m(정규 도메인 0..1)
            tr.widthMultiplier = 1f;
            tr.alignment = LineAlignment.View;                // 카메라 빌보드(FxWeaponGlow/WeaponSwingTrail 선례)
            tr.numCapVertices = 4;                            // 둥근 머리/꼬리 캡
            tr.minVertexDistance = 0.05f;                     // 고속 투척에서도 연속 리본
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 시각 전용 — 그림자 없음
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(TrailRibbonColor, 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.1f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = grad;   // 주황 → 투명(머리 알파 램프업 — 스폰 프레임 플리커 방지)

            // 미공유 신규 머티리언스 인스턴스 — CreateSoftParticle(URP Particles/Unlit, Sprites/Default 폴백 선례)
            var mat = FxMaterialLibrary.CreateSoftParticle("BombTrailRibbonMat", TrailRibbonColor, EnsureTrailPuffTexture());
            if (mat == null) { Object.Destroy(tr); return; }   // 머티리얼 부재 — 무음 폴백+즉시 정리
            if (!FxMaterialLibrary.TryAssignSharedMaterials(tr, new Material[] { mat }, "BombTrailRibbon"))
            {
                Object.Destroy(mat);   // 대입 실패 — 머티리얼 즉시 정리(누수 방지), 리본은 기본 머티리얼로 수명 소멸
                return;
            }
            StartCoroutine(DestroyRibbonMaterialRoutine(bombGo, mat));   // 폭탄 파괴 동행 소멸 — 누수 0
        }

        /// <summary>[206차] 트레일 리본 머티리얼 수명 소멸 — 폭탄 GO 파괴(= Explode 폭발) 대기 후 Destroy(누수 0).
        /// ExplosionImpactRoutine 동일 폴링 선례(폭탄 파괴 = 폭발 프레임). 렌더러/리본은 폭탄 파괴로 자동 소멸,
        /// sharedMaterials 대입 머티리얼만 자동 정리 대상이 아니므로 별도 Destroy.</summary>
        private System.Collections.IEnumerator DestroyRibbonMaterialRoutine(GameObject bombGo, Material ribbonMat)
        {
            while (bombGo != null) yield return null;   // 폭탄 파괴 = 폭발 프레임
            if (ribbonMat != null) Object.Destroy(ribbonMat);
        }

        /// <summary>[205차] 트레일 공유 소프트 서클 텍스처 64×64 — smoothstep 방사 마스크(사각 금지 계약 — 156차 선례),
        /// 결정론(Random 0건), 정적 캐시 1회.</summary>
        private static Texture2D EnsureTrailPuffTexture()
        {
            if (_trailPuffTex != null) return _trailPuffTex;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.name = "BombTrailPuffTex";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[S * S];
            float c = S * 0.5f;
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d));   // 방사 소프트 마스크 — 사각 금지
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            _trailPuffTex = tex;
            return tex;
        }

        /// <summary>[195차 P6] 폭탄 GO 파괴(= Explode 폭발 순간) 대기 후 히트스톱+카메라 셰이크 — 폭발 임팩트 강화.</summary>
        private System.Collections.IEnumerator ExplosionImpactRoutine(GameObject bombGo)
        {
            while (bombGo != null) yield return null;   // 폭탄 파괴 = 폭발 프레임
            if (Application.isPlaying)
                HitStopManager.RequestHitStop(0.05f, 0.12f);   // AnimalAI.cs 선례 패턴 — 가드 + 1줄
            if (CameraShake.Instance != null)   // [195차 P6] 기존 CameraShake API 연동(ProjectName.Core — using 선재)
                CameraShake.Instance.Shake(0.25f, 0.25f);
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