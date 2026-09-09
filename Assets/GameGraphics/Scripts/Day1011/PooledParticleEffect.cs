using System;
using UnityEngine;

namespace Assets.GameGraphics.Scripts.Day1011
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class PooledParticleEffect : MonoBehaviour
    {
        private Action<GameObject> _returnToPool;
        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _particleSystem.main;
            main.stopAction = ParticleSystemStopAction.Callback;
        }
        public void Init(Action<GameObject> returnToPool) => _returnToPool = returnToPool;
        private void OnParticleSystemStopped() => _returnToPool?.Invoke(gameObject);
    }
}

