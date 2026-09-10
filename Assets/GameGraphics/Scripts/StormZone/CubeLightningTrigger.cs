using UnityEngine;

namespace Assets.GameGraphics.Scripts.StormZone
{
    public sealed class CubeLightningTrigger : MonoBehaviour
    {
        [SerializeField] private LightningEffectPool _lightningPool;
        [SerializeField] private Vector3 _spawnOffset = Vector3.up;

        private ParticleSystem _activeEffect;

        public void Play()
        {
            if (_lightningPool == null || (_activeEffect != null && _activeEffect.IsAlive(true)))
            {
                return;
            }

            _activeEffect = _lightningPool.PlayAt(transform.position + _spawnOffset, Quaternion.identity);
        }
    }
}
