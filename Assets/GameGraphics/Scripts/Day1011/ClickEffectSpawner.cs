using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

namespace Assets.GameGraphics.Scripts.Day1011
{
    public sealed class ClickEffectSpawner : MonoBehaviour
    {
        [Header("Particle Res")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private GameObject _effectPrefab;
        [SerializeField] private LayerMask _groundMask;

        [Header("Pool Setting")]
        [SerializeField] private int _initSize = 10;
        [SerializeField] private int _maxSize = 20;

        private ObjectPool<GameObject> _pool;
        private Vector2 _pointerPosition;
        private float _distance = 0;

        private void Awake()
        {
            _targetCamera = _targetCamera != null ? _targetCamera : Camera.main;
            if (_targetCamera != null)
            {
                _distance = _targetCamera.farClipPlane;
            }

            if (_effectPrefab != null)
            {
                _pool = new ObjectPool<GameObject>(
                    createFunc: CreatePoolItem,
                    actionOnGet: obj => obj.SetActive(true),
                    actionOnRelease: obj => obj.SetActive(false),
                    actionOnDestroy: obj => Destroy(obj),
                    collectionCheck: false,
                    defaultCapacity: _initSize,
                    maxSize: _maxSize
                );
            }
        }

        private GameObject CreatePoolItem()
        {
            GameObject effectInstance = Instantiate(_effectPrefab);

            if (effectInstance.TryGetComponent(out PooledParticleEffect effect))
            {
                effect.Init(obj => _pool.Release(obj));
            }

            return effectInstance;
        }

        public void OnPoint(InputValue value) => _pointerPosition = value.Get<Vector2>();

        public void OnClick(InputValue value)
        {
            if (!value.isPressed || _pool == null || _targetCamera == null)
            {
                return;
            }

            Ray ray = _targetCamera.ScreenPointToRay(_pointerPosition);

            if (Physics.Raycast(ray, out RaycastHit hit, _distance, _groundMask))
            {
                GameObject effectObj = _pool.Get();
                effectObj.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
                if (effectObj.TryGetComponent(out ParticleSystem particleSystem))
                {
                    particleSystem.Play(true);
                }
                else
                {
                    Debug.Log("Error");
                }
            }
        }
    }
}