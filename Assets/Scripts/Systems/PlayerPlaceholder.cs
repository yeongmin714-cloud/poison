using UnityEngine;
using ProjectName.Core.Utils;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 캐릭터에 Unity 기본 도형으로 임시 외형을 추가합니다.
    /// 사장님이 GLB 아바타를 넣어주면 자동으로 교체됩니다.
    /// 
    /// [Unity 초보자 설명]
    /// 원통(Cylinder) = 몸통, 구(Sphere) = 머리
    /// 작은 원통 2개 = 팔, 작은 원통 2개 = 다리
    /// 이렇게 5개의 도형합쳐서 사람 모양을 만듭니다.
    /// </summary>
    public class PlayerPlaceholder : MonoBehaviour
    {
        [Header("Body Parts")]
        [SerializeField] private GameObject _body;      // 몸통
        [SerializeField] private GameObject _head;      // 머리
        [SerializeField] private GameObject _leftArm;   // 왼팔
        [SerializeField] private GameObject _rightArm;  // 오른팔
        [SerializeField] private GameObject _leftLeg;   // 왼다리
        [SerializeField] private GameObject _rightLeg;  // 오른다리

        [Header("Colors")]
        [SerializeField] private Color _bodyColor = new Color(0.2f, 0.4f, 0.8f);    // 파란색 옷
        [SerializeField] private Color _skinColor = new Color(1.0f, 0.8f, 0.6f);    // 살색

        // Rig animation
        private RigAnimationController _rigAnim;
        public RigAnimationController RigAnim => _rigAnim;

        private void Awake()
        {
            _rigAnim = GetComponent<RigAnimationController>();
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
            }
        }

        private void Start()
        {
            // GLB 모델이 있으면 우선 로드, 없으면 기본 도형으로 생성
            if (!TryLoadGLBModel())
            {
                BuildPlaceholderBody();
            }
            else
            {
                // AssignController가 PlayerModel 인스턴스에 PPC를 부착하므로 PlayerPlaceholder에는 직접 부착 불필요
            }
        }

        /// <summary>
        /// RuntimeModelLoader에서 "player" GLB 모델을 찾아 Instantiate합니다.
        /// </summary>
        /// <returns>GLB 모델이 로드되었으면 true, 아니면 false</returns>
        private bool TryLoadGLBModel()
        {
            // 이미 파트가 있으면 스킵 (GLB 교체 후에도 유지)
            if (_body != null)
            {
                return false;
            }

            // RuntimeModelLoader에서 플레이어 모델을 찾음
            // [125차 후속11] 경로 진단 로그 — GLB 성공/실패(프리미티브 폴백)를 Play에서 즉별(기존 무음).
            if (RuntimeModelLoader.TryGetModel("player", out var playerModel))
            {
                // 플레이어 모델을 인스턴스화하고 자식으로 붙임
                var playerInstance = Instantiate(playerModel, transform);
                playerInstance.name = "PlayerModel";
                playerInstance.transform.localPosition = Vector3.zero;
                playerInstance.transform.localRotation = Quaternion.identity;
                playerInstance.transform.localScale = Vector3.one;

                // NEW: ModelAnimatorAssigner 부착 (ForceBiped)
                var assigner = playerInstance.AddComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();
                assigner.ForceBiped(true);

                // [125차 후속11] 절차 애니 속도 공급 — GLB 자식은 스스로 움직이지 않고 루트(PlayerMovement)가
                //   이동하므로, 루트 변위를 공급자로 연결해야 보행 애니가 재생된다(메인씬 배치 프리팹과의 차이 —
                //   테스트씬 T포즈 근본 수리). RigAnimationController(레거시 리그)가 있으면 그쪽이 구동 주체
                //   (기존 계약 유지) — 이중 구동 방지 가드.
                if (GetComponent<RigAnimationController>() == null)
                {
                    var procAnim = playerInstance.GetComponentInChildren<ProjectName.Systems.Animation.Procedural.ProceduralAnimationController>();
                    if (procAnim != null)
                    {
                        var provider = GetComponent<ProjectName.Systems.Animation.TransformVelocityProvider>();
                        if (provider == null) provider = gameObject.AddComponent<ProjectName.Systems.Animation.TransformVelocityProvider>();
                        provider.Track(transform);
                        procAnim.SetVelocityProvider(provider);
                        Debug.Log("[PlayerPlaceholder] [125차 후속11] 절차 애니 속도 공급 연결 — 루트 변위 → 보행 구동");
                    }
                }

                // 기존 도형들 제거
                Destroy(_body);
                Destroy(_head);
                Destroy(_leftArm);
                Destroy(_rightArm);
                Destroy(_leftLeg);
                Destroy(_rightLeg);
                _body = _head = _leftArm = _rightArm = _leftLeg = _rightLeg = null;

                // Main player model: GLB 자식의 콜라이더/리지드바디 제거 (부모 CharacterController가 처리)
                // [125차 후속12] rb 제거 생략 조건 — ModelAnimatorAssigner가 추가한 ProceduralAnimationController는
                //   Rigidbody를 RequireComponent로 요구한다(Play 실측: "Can't remove Rigidbody because
                //   ProceduralAnimationController (Script) depends on it"). 존재 시 제거 시도 자체를 생략하고
                //   kinematic 무중력 상태를 유지한다(부작용 없음 — 에러 로그 노이즈만 제거).
                bool hasProcAnim = playerInstance.GetComponentInChildren<ProjectName.Systems.Animation.Procedural.ProceduralAnimationController>() != null;
                var colliders = playerInstance.GetComponentsInChildren<Collider>(true);
                foreach (var col in colliders)
                {
                    if (col != null)
                        DestroyImmediate(col);
                }
                var rb = playerInstance.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // [125차 후속12] kinematic 경화는 항상 먼저 — hasProcAnim이어도 rb가 남으므로 중력을
                    //   받지 않게 해야 한다(경화가 조건 안에 있으면 GLB가 중력 낙하하는 결함).
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.detectCollisions = false;
                }
                if (rb != null && !hasProcAnim)
                {
                    // RequireComponent 의존성(QuadrupedProceduralAnimation 등) 때문에 DestroyImmediate가
                    // 차단될 수 있다 → try-catch로 격리, 실패해도 중립 상태 유지로 계속 진행.
                    // [125차 후속12] ProceduralAnimationController 의존(rb 필수) 시에는 시도 자체를 생략
                    // (에러 로그 노이즈 제거 — 위 kinematic 경화로 안전 유지).
                    try { DestroyImmediate(rb); }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[PlayerPlaceholder] GLB 리지드바디 제거 무시(의존 컴포넌트): {e.GetType().Name} — isKinematic으로 중력 낙하 차단 유지");
                    }
                }
                else if (rb != null && hasProcAnim)
                {
                    Debug.Log("[PlayerPlaceholder] [125차 후속12] GLB Rigidbody 유지 — ProceduralAnimationController 의존(RequireComponent), kinematic 무중력 상태");
                }

                // [51차 키 1.8m 통일 → 54차 측정 모드] 플레이어 GLB 비주얼도 표준 인간 키 1.8m로 정규화(병사와 동일 상수) —
                //   플레이어 모델은 골격 스케일 구조라 localBounds 오측(0.61m) → **월드 bounds 측정(skinnedLocalBounds=false)**.
                TerritoryBuilder.NormalizeSoldierHeight(playerInstance, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: false);

                // [125차 후속11] GLB 발-접지 보정 — NormalizeSoldierHeight는 발을 "스케일 전 위치"로 복원만
                //   하므로 프리팹 내부 피벗 오프셋(모델 중심 피벗 GLB)이 유지돼 발이 플레이어 루트 지면 위에
                //   떠 있었다(아레나 테스트 2 영상 5s/17s 실측: 0.5~1m 부유+그림자 분리). 월드 bounds 최하단을
                //   플레이어 루트 Y(지면 기준)로 하강 정렬 — 상대 정렬이므로 루트 접지 상태와 무관하게 성립.
                var footRends = playerInstance.GetComponentsInChildren<Renderer>();
                if (footRends != null && footRends.Length > 0)
                {
                    Bounds footBounds = footRends[0].bounds;
                    foreach (var r in footRends) footBounds.Encapsulate(r.bounds);
                    float drop = footBounds.min.y - transform.position.y;
                    if (drop > 0.005f)
                    {
                        playerInstance.transform.position += Vector3.down * drop;
                        Debug.Log($"[PlayerPlaceholder] [125차 후속11] GLB 발-접지 보정 — {drop:F2}m 하강(프리팹 피벗 오프셋 상쇄)");
                    }
                }

                // 별도 레이어로 이동 (부모와 충돌 방지)
                int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
                playerInstance.layer = ignoreLayer;
                foreach (Transform child in playerInstance.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = ignoreLayer;
                }

                Debug.Log("[PlayerPlaceholder] [125차 후속11] 플레이어 GLB 로드 성공 — Player_Rigged 부착");
                return true;
            }

            Debug.LogWarning("[PlayerPlaceholder] [125차 후속11] 플레이어 GLB 미로드 — 프리미티브 폴백 외형 유지(RigAnimationController 조건 충족 시 구동)");
            return false;
        }

        private GameObject BuildPlaceholderBody()
        {
            // 몸통
            _body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _body.name = "Body";
            _body.transform.SetParent(transform);
            _body.transform.localPosition = new Vector3(0, 0.5f, 0);
            _body.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
            _body.transform.localRotation = Quaternion.identity;

            // 머리
            _head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _head.name = "Head";
            _head.transform.SetParent(transform);
            _head.transform.localPosition = new Vector3(0, 1.5f, 0);
            _head.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            _head.transform.localRotation = Quaternion.identity;

            // 왼쪽 팔
            _leftArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _leftArm.name = "LeftArm";
            _leftArm.transform.SetParent(transform);
            _leftArm.transform.localPosition = new Vector3(-0.75f, 0.5f, 0);
            _leftArm.transform.localScale = new Vector3(0.3f, 0.8f, 0.3f);
            _leftArm.transform.localRotation = Quaternion.Euler(0, 0, -30f);

            // 오른쪽 팔
            _rightArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _rightArm.name = "RightArm";
            _rightArm.transform.SetParent(transform);
            _rightArm.transform.localPosition = new Vector3(0.75f, 0.5f, 0);
            _rightArm.transform.localScale = new Vector3(0.3f, 0.8f, 0.3f);
            _rightArm.transform.localRotation = Quaternion.Euler(0, 0, 30f);

            // 왼쪽 다리
            _leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _leftLeg.name = "LeftLeg";
            _leftLeg.transform.SetParent(transform);
            _leftLeg.transform.localPosition = new Vector3(-0.3f, 0, 0);
            _leftLeg.transform.localScale = new Vector3(0.3f, 1.0f, 0.3f);
            _leftLeg.transform.localRotation = Quaternion.Euler(0, 0, -10f);

            // 오른쪽 다리
            _rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _rightLeg.name = "RightLeg";
            _rightLeg.transform.SetParent(transform);
            _rightLeg.transform.localPosition = new Vector3(0.3f, 0, 0);
            _rightLeg.transform.localScale = new Vector3(0.3f, 1.0f, 0.3f);
            _rightLeg.transform.localRotation = Quaternion.Euler(0, 0, 10f);

            // 색상 적용 (MaterialHelper로 URP Lit 또는 기본 셰이더 자동 선택)
            var bodyRenderer = _body.GetComponent<MeshRenderer>();
            if (bodyRenderer != null)
            {
                var mat = MaterialHelper.CreateLitMaterial(_bodyColor, "Body_Mat");
                if (mat != null)
                    bodyRenderer.material = mat;
            }

            var headRenderer = _head.GetComponent<MeshRenderer>();
            if (headRenderer != null)
            {
                var mat = MaterialHelper.CreateLitMaterial(_skinColor, "Head_Mat");
                if (mat != null)
                    headRenderer.material = mat;
            }

            var leftArmRenderer = _leftArm.GetComponent<MeshRenderer>();
            if (leftArmRenderer != null)
            {
                var mat = MaterialHelper.CreateLitMaterial(_skinColor, "LeftArm_Mat");
                if (mat != null)
                    leftArmRenderer.material = mat;
            }

            var rightArmRenderer = _rightArm.GetComponent<MeshRenderer>();
            if (rightArmRenderer != null)
            {
                var mat = MaterialHelper.CreateLitMaterial(_skinColor, "RightArm_Mat");
                if (mat != null)
                    rightArmRenderer.material = mat;
            }

            var leftLegRenderer = _leftLeg.GetComponent<MeshRenderer>();
            if (leftLegRenderer != null)
            {
                var mat = MaterialHelper.CreateLitMaterial(_skinColor, "LeftLeg_Mat");
                if (mat != null)
                    leftLegRenderer.material = mat;
            }

            var rightLegRenderer = _rightLeg.GetComponent<MeshRenderer>();
            if (rightLegRenderer != null)
            {
                var mat = MaterialHelper.CreateLitMaterial(_skinColor, "RightLeg_Mat");
                if (mat != null)
                    rightLegRenderer.material = mat;
            }

            // 시각적 Placeholder이므로 Collider 제거 (물리 오버헤드 방지)
            var bodyCollider = _body.GetComponent<Collider>();
            if (bodyCollider != null)
                Destroy(bodyCollider);
            var headCollider = _head.GetComponent<Collider>();
            if (headCollider != null)
                Destroy(headCollider);
            var leftArmCollider = _leftArm.GetComponent<Collider>();
            if (leftArmCollider != null)
                Destroy(leftArmCollider);
            var rightArmCollider = _rightArm.GetComponent<Collider>();
            if (rightArmCollider != null)
                Destroy(rightArmCollider);
            var leftLegCollider = _leftLeg.GetComponent<Collider>();
            if (leftLegCollider != null)
                Destroy(leftLegCollider);
            var rightLegCollider = _rightLeg.GetComponent<Collider>();
            if (rightLegCollider != null)
                Destroy(rightLegCollider);

            return _body;
        }

        /// <summary>
        /// GLB 아바타로 교체될 때 호출 (ModelSwapper가 사용)
        /// </summary>
        public void ClearPlaceholder()
        {
            if (_body != null) { Destroy(_body); _body = null; }
            if (_head != null) { Destroy(_head); _head = null; }
            if (_leftArm != null) { Destroy(_leftArm); _leftArm = null; }
            if (_rightArm != null) { Destroy(_rightArm); _rightArm = null; }
            if (_leftLeg != null) { Destroy(_leftLeg); _leftLeg = null; }
            if (_rightLeg != null) { Destroy(_rightLeg); _rightLeg = null; }
        }
    }
}