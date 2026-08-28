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
            Debug.Log($"[CollisionDebugger] HIT: {hit.gameObject.name} (layer={hit.gameObject.layer} [{LayerMask.LayerToName(hit.gameObject.layer)}]) at {hit.point}, normal={hit.normal}, moveDir={hit.moveDirection}");
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