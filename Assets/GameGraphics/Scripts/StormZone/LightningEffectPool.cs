using Assets.GameGraphics.Scripts.Day1011;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.GameGraphics.Scripts.StormZone
{
    public sealed class LightningEffectPool : MonoBehaviour
    {
        [Header("Particle Res")]
        [SerializeField] private GameObject _lightningPrefab;

        [Header("Pool Setting")]
        [SerializeField] private int _initSize = 5;
        [SerializeField] private int _maxSize = 10;

        private ObjectPool<GameObject> _pool;

        private void Awake()
        {
            if (_lightningPrefab == null)
            {
                return;
            }

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

        private GameObject CreatePoolItem()
        {
            GameObject effectInstance = Instantiate(_lightningPrefab, transform);

            if (effectInstance.TryGetComponent(out PooledParticleEffect effect))
            {
                effect.Init(obj => _pool.Release(obj));
            }

            return effectInstance;
        }

        public ParticleSystem PlayAt(Vector3 position, Quaternion rotation)
        {
            if (_pool == null)
            {
                return null;
            }

            GameObject effectObj = _pool.Get();
            effectObj.transform.SetPositionAndRotation(position, rotation);

            if (effectObj.TryGetComponent(out ParticleSystem particleSystem))
            {
                particleSystem.Play(true);
                return particleSystem;
            }

            return null;
        }
    }
}
