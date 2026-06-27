using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 약초 시각 상태 관리 컴포넌트.
    /// HerbPickup과 같은 GameObject에 부착.
    /// 채집 불가(리스폰 대기) 시 MeshRenderer 알파를 0.3f로 변경.
    /// 리스폰 완료 시 알파 1.0f 복원.
    /// </summary>
    [RequireComponent(typeof(HerbPickup))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HerbVisualState : MonoBehaviour
    {
        private HerbPickup _herbPickup;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propBlock;
        private Color _originalColor;
        private bool _hasRenderer;
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _herbPickup = GetComponent<HerbPickup>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _hasRenderer = _meshRenderer != null;

            if (_hasRenderer)
            {
                _propBlock = new MaterialPropertyBlock();
                _meshRenderer.GetPropertyBlock(_propBlock);

                Material sharedMat = _meshRenderer.sharedMaterial;
                _originalColor = sharedMat != null ? sharedMat.color : Color.white;

                // 초기값 설정 (GetCurrentAlpha()가 이벤트 발생 전에 호출돼도 안전)
                _propBlock.SetColor(ColorProp, _originalColor);
                _meshRenderer.SetPropertyBlock(_propBlock);
            }
        }

        private void OnEnable()
        {
            if (_herbPickup != null)
            {
                _herbPickup.OnHarvestStarted += OnHarvested;
                _herbPickup.OnRespawnCompleted += OnRespawned;
            }
        }

        private void OnDisable()
        {
            if (_herbPickup != null)
            {
                _herbPickup.OnHarvestStarted -= OnHarvested;
                _herbPickup.OnRespawnCompleted -= OnRespawned;
            }
        }

        private void OnHarvested()
        {
            SetAlpha(0.3f);
        }

        private void OnRespawned()
        {
            SetAlpha(1.0f);
        }

        private void SetAlpha(float alpha)
        {
            if (!_hasRenderer || _propBlock == null) return;

            Color color = _originalColor;
            color.a = alpha;
            _propBlock.SetColor(ColorProp, color);
            _meshRenderer.SetPropertyBlock(_propBlock);
        }

        /// <summary>
        /// 현재 알파값 반환 (테스트용)
        /// </summary>
        public float GetCurrentAlpha()
        {
            if (!_hasRenderer || _propBlock == null) return 1f;
            _meshRenderer.GetPropertyBlock(_propBlock);
            return _propBlock.GetColor(ColorProp).a;
        }
    }
}