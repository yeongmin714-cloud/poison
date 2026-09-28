using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    /// <summary>Simple standalone WASD movement for the player-castle playtest.</summary>
    public sealed class PlayerCastleInteriorTestMovement : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 5f;
        private CharacterController _controller;

        private void Awake() => _controller = GetComponent<CharacterController>();

        private void Update()
        {
            if (_controller == null || Keyboard.current == null)
                return;

            Keyboard keyboard = Keyboard.current;
            float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                      (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float z = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                      (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            _controller.Move(Vector3.ClampMagnitude(new Vector3(x, 0f, z), 1f) * (_moveSpeed * Time.deltaTime));

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, -10.1f, 10.1f);
            position.z = Mathf.Clamp(position.z, -7.1f, 7.1f);
            transform.position = position;
        }
    }
}
