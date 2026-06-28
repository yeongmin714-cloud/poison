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
            if (RuntimeModelLoader.TryGetModel("player", out var playerModel))
            {
                // 플레이어 모델을 인스턴스화하고 자식으로 붙임
                var playerInstance = Instantiate(playerModel, transform);
                playerInstance.name = "PlayerModel";
                playerInstance.transform.localPosition = Vector3.zero;
                playerInstance.transform.localRotation = Quaternion.identity;
                playerInstance.transform.localScale = Vector3.one;

                // 기존 도형들 제거
                Destroy(_body);
                Destroy(_head);
                Destroy(_leftArm);
                Destroy(_rightArm);
                Destroy(_leftLeg);
                Destroy(_rightLeg);
                _body = _head = _leftArm = _rightArm = _leftLeg = _rightLeg = null;

                return true;
            }

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