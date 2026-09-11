using System;
using System.Threading;
using UnityEngine;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

namespace Assets.GameGraphics.Scripts.Day12
{
    public sealed class LightningController : MonoBehaviour
    {
        [SerializeField] private VisualEffect _lightningVFX;
        [SerializeField] private Light _flashLight;

        [Header("Storm")]
        [SerializeField] private Vector2 _interval = new(0.1f, 0.5f);
        [SerializeField] private Vector2Int _boltsPerStrike = new(3, 8);
        [SerializeField] private float _areaRadius = 60f;
        [SerializeField] private float _height = 30f;
        [SerializeField] private int _stripCapacity = 64;

        [Header("Flash")]
        [SerializeField] private float _flashIntensity = 8f;
        [SerializeField] private float _flashStep = 0.05f;

        private static readonly int AreaCenterID = Shader.PropertyToID("AreaCenter");
        private static readonly int AreaRadiusID = Shader.PropertyToID("AreaRadius");
        private static readonly int HeightID = Shader.PropertyToID("Height");
        private static readonly int BoltsID = Shader.PropertyToID("BoltsPerStrike");
        private static readonly int StripOffsetID = Shader.PropertyToID("StripOffset");
        private static readonly int SeedID = Shader.PropertyToID("StrikeSeed");
        private static readonly int StrikeID = Shader.PropertyToID("Strike");

        private readonly float[] _pattern = { 1f, 0.2f, 1f, 0.3f, 0.8f, 0f };

        private CancellationTokenSource _flashCts;
        private int _stripOffset;

        private async void Start()
        {
            try
            {
                while (true)
                {
                    float wait = Random.Range(_interval.x, _interval.y);
                    await Awaitable.WaitForSecondsAsync(wait, destroyCancellationToken);
                    Strike();
                }
            }
            catch (OperationCanceledException)
            {

            }
        }

        public void Strike()
        {
            int bolts = Random.Range(_boltsPerStrike.x, _boltsPerStrike.y + 1);

            _lightningVFX.SetVector3(AreaCenterID, transform.position);
            _lightningVFX.SetFloat(AreaRadiusID, _areaRadius);
            _lightningVFX.SetFloat(HeightID, _height);
            _lightningVFX.SetInt(BoltsID, bolts);
            _lightningVFX.SetInt(StripOffsetID, _stripOffset);
            _lightningVFX.SetInt(SeedID, Random.Range(0, 1_000_000));
            _lightningVFX.SendEvent(StrikeID);

            // 다음 번개는 이번에 쓴 Strip 다음 번호부터 사용
            _stripOffset = (_stripOffset + bolts) % _stripCapacity;

            Flash(bolts);
        }

        private async void Flash(int bolts)
        {
            Cancel();
            _flashCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = _flashCts.Token;

            float peak = _flashIntensity * Mathf.Lerp(.6f, 1.5f, bolts / (float)_boltsPerStrike.y);

            try
            {
                foreach (float p in _pattern)
                {
                    _flashLight.intensity = peak * p;
                    await Awaitable.WaitForSecondsAsync(_flashStep, token);
                }
            }
            catch (OperationCanceledException)
            {

            }
        }

        private void OnDestroy()
        {
            Cancel();
        }

        private void Cancel()
        {
            if (_flashCts == null)
            {
                return;
            }
            _flashCts.Cancel();
            _flashCts.Dispose();
            _flashCts = null;
        }
    }
}