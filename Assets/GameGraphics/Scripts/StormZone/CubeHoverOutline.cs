using UnityEngine;

namespace Assets.GameGraphics.Scripts.StormZone
{
    [RequireComponent(typeof(Renderer))]
    public sealed class CubeHoverOutline : MonoBehaviour
    {
        [Header("Outline")]
        [SerializeField] private Color _hoverEmissionColor = Color.red;
        [SerializeField] private Color _normalEmissionColor = Color.black;
        [SerializeField] private string _emissionColorProperty = "_EmissionColor";

        private Renderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private int _emissionColorId;
        private bool _isHovered;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propertyBlock = new MaterialPropertyBlock();
            _emissionColorId = Shader.PropertyToID(_emissionColorProperty);
        }

        public void SetHovered(bool hovered)
        {
            if (_isHovered == hovered)
            {
                return;
            }

            _isHovered = hovered;

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(_emissionColorId, hovered ? _hoverEmissionColor : _normalEmissionColor);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
