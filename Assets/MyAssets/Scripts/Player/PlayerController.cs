using UnityEngine;

namespace Assets.MyAssets.Scripts.Player
{

    [RequireComponent(typeof(PlayerMove))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Camera _referenceCamera;
        [SerializeField] private float _moveSpeed = 5f;

        private PlayerMove _playerMove;
        private PlayerInputHandler _playerInputHandler;

        private void Awake()
        {
            _playerMove = GetComponent<PlayerMove>();
            _playerInputHandler = GetComponent<PlayerInputHandler>();
            _referenceCamera = _referenceCamera != null ? _referenceCamera : Camera.main;
        }

        private void Update()
        {
            Vector2 input = _playerInputHandler.InputVector;

            if (input.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 moveDir = CameraRelativeDirection(input);
            _playerMove.Move(moveDir, _moveSpeed);
        }

        private Vector3 CameraRelativeDirection(Vector2 input)
        {
            if (_referenceCamera == null)
            {
                return new Vector3(input.x, 0f, input.y);
            }

            Vector3 forward = Vector3.ProjectOnPlane(_referenceCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(_referenceCamera.transform.right, Vector3.up).normalized;

            return forward * input.y + right * input.x;
        }
    }
}
