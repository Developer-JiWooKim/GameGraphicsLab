using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.GameGraphics.Scripts.StormZone
{
    public sealed class CubeInteractionController : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private LayerMask _cubeMask;

        private Vector2 _pointerPosition;
        private float _distance;
        private CubeHoverOutline _hoveredCube;

        private void Awake()
        {
            _targetCamera = _targetCamera != null ? _targetCamera : Camera.main;
            _distance = _targetCamera != null ? _targetCamera.farClipPlane : 0f;
        }

        private void Update()
        {
            if (_targetCamera == null)
            {
                return;
            }

            UpdateHover();
        }

        public void OnPoint(InputValue value) => _pointerPosition = value.Get<Vector2>();

        public void OnClick(InputValue value)
        {
            if (!value.isPressed || _targetCamera == null)
            {
                return;
            }

            if (TryRaycastCube(out RaycastHit hit) && hit.collider.TryGetComponent(out CubeLightningTrigger trigger))
            {
                trigger.Play();
            }
        }

        private void UpdateHover()
        {
            CubeHoverOutline newHovered = null;

            if (TryRaycastCube(out RaycastHit hit))
            {
                hit.collider.TryGetComponent(out newHovered);
            }

            if (newHovered == _hoveredCube)
            {
                return;
            }

            if (_hoveredCube != null)
            {
                _hoveredCube.SetHovered(false);
            }

            newHovered?.SetHovered(true);
            _hoveredCube = newHovered;
        }

        private bool TryRaycastCube(out RaycastHit hit)
        {
            Ray ray = _targetCamera.ScreenPointToRay(_pointerPosition);
            return Physics.Raycast(ray, out hit, _distance, _cubeMask);
        }
    }
}
