using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P22-4 — 플레이어 무기 사거리 표시 링 (고품질).
    /// 기존 폴리곤 라인 원(16세그먼트 IMGUI/기즈모 계열)을 SelectionRing 셰이더
    /// (부드러운 밴드+글로우+펄스, 실패 시 절차 링 텍스처 폴백)으로 교체.
    /// 플레이어 자식으로 부착, 매 프레임 사거리(x2 지름) 반영. 지면 0.02 위.
    /// </summary>
    public class PlayerRangeRing : MonoBehaviour
    {
        private Func<float> _rangeProvider;
        private Transform _quad;
        private Material _mat;
        private bool _fallback;

        public static PlayerRangeRing Ensure(GameObject player, Func<float> rangeProvider)
        {
            var existing = player.GetComponentInChildren<PlayerRangeRing>(true);
            if (existing != null) { existing._rangeProvider = rangeProvider; return existing; }

            var go = new GameObject("PlayerRangeRing");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            var ring = go.AddComponent<PlayerRangeRing>();
            ring._rangeProvider = rangeProvider;
            ring.Build();
            return ring;
        }

        private void Build()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "RangeRingQuad";
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _quad = quad.transform;

            _mat = SelectionRingController.CreateRingMaterial(new Color(0.35f, 0.65f, 1f, 1f));
            _fallback = SelectionRingController.IsFallback(_mat);
            var r = quad.GetComponent<Renderer>();
            if (r != null && _mat != null)
            {
                r.sharedMaterial = _mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void LateUpdate()
        {
            if (_quad == null || _rangeProvider == null) return;
            float range = Mathf.Max(1f, _rangeProvider());
            // 셰이더 링: 반지름 = 쿼드 반폭 → 지름 = 2×range. 폴백 텍스처: 링 반지름 = 반폭×0.92.
            float diameter = _fallback ? (2f * range) / 0.92f : 2f * range;
            _quad.localScale = Vector3.one * diameter;
        }
    }
}
