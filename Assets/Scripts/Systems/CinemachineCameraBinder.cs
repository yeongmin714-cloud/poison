using UnityEngine;
using Unity.Cinemachine;
using System.Reflection;

/// <summary>
/// Cinemachine 3.x 카메라를 플레이어에 바인딩하는 런타임 컴포넌트
/// 에디터에서 속성명이 다른 문제를 런타임에 해결
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CinemachineCameraBinder : MonoBehaviour
{
    [Header("Target References")]
    [Tooltip("카메라가 따라갈 타겟 (Player)")]
    public Transform followTarget;
    
    [Tooltip("카메라가 바라볼 타겟 (PlayerModel)")]
    public Transform lookAtTarget;

    [Header("Camera Settings (BotW Style)")]
    public float cameraDistance = 25f;
    public float minDistance = 15f;
    public float maxDistance = 40f;
    public float verticalOffset = 1.5f;
    public float horizontalOffset = 0f;
    public Vector3 shoulderOffset = new Vector3(0.5f, 0f, 0f);

    [Header("Input Settings")]
    public string horizontalAxis = "Mouse X";
    public string verticalAxis = "Mouse Y";
    public float maxSpeed = 300f;
    public float accelTime = 0.1f;
    public float decelTime = 0.1f;

    [Header("Collider Settings")]
    public float minDistanceFromTarget = 0.5f;
    public float maxDistanceFromTarget = 40f;
    public float colliderRadius = 0.3f;
    public LayerMask collideAgainstLayers = ~0;

    CinemachineCamera _vcam;
    CinemachineThirdPersonFollow _tpf;
    CinemachineInputAxisController _inputAxis;
    CinemachineCollider _collider;

    void Awake()
    {
        _vcam = GetComponent<CinemachineCamera>();
        _tpf = GetComponent<CinemachineThirdPersonFollow>();
        _inputAxis = GetComponent<CinemachineInputAxisController>();
        _collider = GetComponent<CinemachineCollider>();

        // 컴포넌트 자동 추가 (없는 경우)
        if (_tpf == null) _tpf = gameObject.AddComponent<CinemachineThirdPersonFollow>();
        if (_inputAxis == null) _inputAxis = gameObject.AddComponent<CinemachineInputAxisController>();
        if (_collider == null) _collider = gameObject.AddComponent<CinemachineCollider>();

        BindCamera();
    }

    void Start()
    {
        // 타겟이 인스펙터에서 설정 안 된 경우 자동 찾기
        if (followTarget == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) followTarget = player.transform;
        }
        
        if (lookAtTarget == null && followTarget != null)
        {
            var model = followTarget.Find("PlayerModel");
            if (model != null) lookAtTarget = model;
            else lookAtTarget = followTarget;
        }

        // 재바인딩
        if (followTarget != null || lookAtTarget != null)
        {
            BindCamera();
        }
    }

    void BindCamera()
    {
        // CinemachineThirdPersonFollow 바인딩
        if (_tpf != null)
        {
            // Cinemachine 3.x API: FollowTarget, LookAtTarget 프로퍼티 사용
            var tpfType = _tpf.GetType();
            
            SetProp(tpfType, _tpf, "FollowTarget", followTarget);
            SetProp(tpfType, _tpf, "LookAtTarget", lookAtTarget);
            SetProp(tpfType, _tpf, "CameraDistance", cameraDistance);
            SetProp(tpfType, _tpf, "MinCameraDistance", minDistance);
            SetProp(tpfType, _tpf, "MaxCameraDistance", maxDistance);
            SetProp(tpfType, _tpf, "VerticalOffset", verticalOffset);
            SetProp(tpfType, _tpf, "HorizontalOffset", horizontalOffset);
            SetProp(tpfType, _tpf, "ShoulderOffset", shoulderOffset);
            
            // 구버전 속성명도 시도
            SetProp(tpfType, _tpf, "Follow", followTarget);
            SetProp(tpfType, _tpf, "LookAt", lookAtTarget);
            SetProp(tpfType, _tpf, "TargetOffset", new Vector3(0, verticalOffset, 0));
        }

        // CinemachineInputAxisController 바인딩
        if (_inputAxis != null)
        {
            var inputType = _inputAxis.GetType();
            SetProp(inputType, _inputAxis, "HorizontalAxis", horizontalAxis);
            SetProp(inputType, _inputAxis, "VerticalAxis", verticalAxis);
            SetProp(inputType, _inputAxis, "HorizontalAxisName", horizontalAxis);
            SetProp(inputType, _inputAxis, "VerticalAxisName", verticalAxis);
            SetProp(inputType, _inputAxis, "MaxSpeed", maxSpeed);
            SetProp(inputType, _inputAxis, "AccelTime", accelTime);
            SetProp(inputType, _inputAxis, "DecelTime", decelTime);
        }

        // CinemachineCollider 바인딩
        if (_collider != null)
        {
            var colliderType = _collider.GetType();
            SetProp(colliderType, _collider, "MinimumDistanceFromTarget", minDistanceFromTarget);
            SetProp(colliderType, _collider, "MaximumDistanceFromTarget", maxDistanceFromTarget);
            SetProp(colliderType, _collider, "Radius", colliderRadius);
            SetProp(colliderType, _collider, "CollideAgainstLayers", (int)collideAgainstLayers);
            
            // Strategy enum 설정
            var strategyEnum = colliderType.GetNestedType("ResolutionStrategy", BindingFlags.Public);
            if (strategyEnum != null)
            {
                var preserveDist = System.Enum.Parse(strategyEnum, "PreserveCameraDistance");
                SetProp(colliderType, _collider, "Strategy", preserveDist);
            }
        }

        Debug.Log($"[CinemachineBinder] Camera bound - Follow: {followTarget?.name}, LookAt: {lookAtTarget?.name}");
    }

    void LateUpdate()
    {
        // 타겟이 변경되었을 때 재바인딩 (안전장치)
        if (_tpf != null)
        {
            var currentFollow = GetProp(_tpf.GetType(), _tpf, "FollowTarget") 
                             ?? GetProp(_tpf.GetType(), _tpf, "Follow");
            var currentLookAt = GetProp(_tpf.GetType(), _tpf, "LookAtTarget") 
                             ?? GetProp(_tpf.GetType(), _tpf, "LookAt");
            
            if (followTarget != null && currentFollow != followTarget)
            {
                BindCamera();
            }
        }
    }

    static void SetProp(System.Type type, object obj, string name, object value)
    {
        if (obj == null || value == null) return;
        
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
        }
        else if (field != null)
        {
            field.SetValue(obj, value);
        }
        else
        {
            // 대소문자 무시 검색
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (p.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase) && p.CanWrite)
                {
                    p.SetValue(obj, value);
                    return;
                }
            }
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    f.SetValue(obj, value);
                    return;
                }
            }
        }
    }

    static object GetProp(System.Type type, object obj, string name)
    {
        if (obj == null) return null;
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null) return prop.GetValue(obj);
        if (field != null) return field.GetValue(obj);
        return null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 에디터에서 값 변경 시 즉시 반영
        if (Application.isPlaying) BindCamera();
    }
#endif
}