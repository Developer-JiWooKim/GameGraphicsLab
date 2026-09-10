using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.MyAssets.Scripts.Player
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputHandler : MonoBehaviour
    {
        private Vector2 _inputVector = Vector2.zero;
        public Vector2 InputVector => _inputVector;

        private PlayerInput _playerInput;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        public void OnMove(InputValue value) => _inputVector = value.Get<Vector2>();
    }

}
