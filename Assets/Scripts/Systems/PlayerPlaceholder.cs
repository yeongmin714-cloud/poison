using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 캐릭터에 Unity 기본 도형으로 임시 외형을 추가합니다.
    /// 사장님이 GLB 아바타를 넣어주면 자동으로 교체됩니다.
    /// 
    /// [Unity 초보자 설명]
    /// 원통(Cylinder) = 몸통, 구(Sphere) = 머리
    /// 작은 원통 2개 = 팔, 작은 원통 2개 = 다리
    /// 이렇게 5개의 도형을 합쳐서 사람 모양을 만듭니다.
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
            // 이미 파트가 있으면 스킵
            if (_body != null) return true;

            if (!RuntimeModelLoader.TryGetModel("player_rigged", out var playerModel, out var _))
                return false;

            GameObject avatar = Object.Instantiate(playerModel, transform);
            avatar.name = "Avatar";
            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localRotation = Quaternion.identity;
            avatar.transform.localScale = Vector3.one;

            // Assign animator controller for the player model
            ModelAnimatorAssigner.AssignController(avatar, "player");

            Debug.Log("[PlayerPlaceholder] GLB 플레이어 모델 로드 완료");
            return true;
        }

        /// <summary>
        /// Unity 기본 도형으로 사람 모양 만들기
        /// </summary>
        private void BuildPlaceholderBody()
        {
            // 이미 파트가 있으면 스킵 (GLB 교체 후에도 유지)
            if (_body != null) return;

            float scale = 2.0f; // 전체 크기 비율

            // === 몸통 (Cylinder) ===
            _body = CreatePart(PrimitiveType.Cylinder, "Body", new Vector3(0, 0.6f, 0), 
                new Vector3(0.8f * scale, 0.5f * scale, 0.5f * scale), _bodyColor);

            // === 머리 (Sphere) ===
            _head = CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0, 1.3f, 0), 
                new Vector3(0.4f * scale, 0.4f * scale, 0.4f * scale), _skinColor);

            // === 왼팔 (Cylinder) ===
            _leftArm = CreatePart(PrimitiveType.Cylinder, "LeftArm", new Vector3(-0.7f, 0.9f, 0), 
                new Vector3(0.15f * scale, 0.4f * scale, 0.15f * scale), _skinColor);

            // === 오른팔 (Cylinder) ===
            _rightArm = CreatePart(PrimitiveType.Cylinder, "RightArm", new Vector3(0.7f, 0.9f, 0), 
                new Vector3(0.15f * scale, 0.4f * scale, 0.15f * scale), _skinColor);

            // === 왼다리 (Cylinder) ===
            _leftLeg = CreatePart(PrimitiveType.Cylinder, "LeftLeg", new Vector3(-0.3f, 0.2f, 0), 
                new Vector3(0.2f * scale, 0.4f * scale, 0.2f * scale), _bodyColor);

            // === 오른다리 (Cylinder) ===
            _rightLeg = CreatePart(PrimitiveType.Cylinder, "RightLeg", new Vector3(0.3f, 0.2f, 0), 
                new Vector3(0.2f * scale, 0.4f * scale, 0.2f * scale), _bodyColor);
        }

        /// <summary>
        /// 하나의 신체 부위를 생성
        /// </summary>
        private GameObject CreatePart(PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;

            // 색상 적용 (MaterialHelper로 URP Lit 또는 기본 셰이더 자동 선택)
            var renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = MaterialHelper.CreateLitMaterial(color, $"{name}_Mat");
                renderer.material = mat;
            }

            return part;
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