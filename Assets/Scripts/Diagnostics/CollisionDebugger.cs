using UnityEngine;

namespace ProjectName.Diagnostics
{
    /// <summary>
    /// Debug script to log CharacterController collisions
    /// Attach to Player to verify ground collision is working
    /// </summary>
    public class CollisionDebugger : MonoBehaviour
    {
    private CharacterController _controller;
    private int _collisionCount = 0;
    // 동일 히트 중복 로그 억제 (로그 스팸/히치 방지): 마지막 로그 좌표+오브젝트 캐시,
    // 동일 오브젝트·0.01m 이내 동일 좌표면 1초에 1회만 출력.
    private Vector3 _lastHitPoint;
    private string _lastHitName;
    private float _lastHitLogTime = -999f;
    
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        Debug.Log($"[CollisionDebugger] Started on {gameObject.name}, layer={gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
    }
    
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        _collisionCount++;
        if (_collisionCount % 10 == 1 || hit.gameObject.name.Contains("Floor") || hit.gameObject.name.Contains("Ground"))
        {
            bool sameHit = hit.gameObject.name == _lastHitName
                && (hit.point - _lastHitPoint).sqrMagnitude <= 0.01f * 0.01f; // epsilon 0.01m
            if (sameHit && Time.time - _lastHitLogTime < 1f)
                return; // 동일 히트는 1초에 1회만

            Debug.Log($"[CollisionDebugger] HIT: {hit.gameObject.name} (layer={hit.gameObject.layer} [{LayerMask.LayerToName(hit.gameObject.layer)}]) at {hit.point}, normal={hit.normal}, moveDir={hit.moveDirection}");
            _lastHitPoint = hit.point;
            _lastHitName = hit.gameObject.name;
            _lastHitLogTime = Time.time;
        }
    }
    
    private void Update()
    {
        if (_controller != null)
        {
            // Log grounded state every 60 frames
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[CollisionDebugger] isGrounded={_controller.isGrounded}, pos={transform.position}, vel={_controller.velocity}");
            }
        }
    }
}
}