using System;
using UnityEngine;
using ProjectName.Core;   // [P23] WeaponType — 무기 타입 기반 반경/표시 소스

namespace ProjectName.Systems
{
    /// <summary>
    /// P22-4 — 플레이어 무기 사거리 표시 링 (고품질).
    /// 기존 폴리곤 라인 원(16세그먼트 IMGUI/기즈모 계열)을 SelectionRing 셰이더
    /// (부드러운 밴드+글로우+펄스, 실패 시 절차 링 텍스처 폴백)으로 교체.
    /// 플레이어 자식으로 부착, 매 프레임 사거리(x2 지름) 반영. 지면 0.02 위.
    /// [P23] 반경/표시 소스를 무기 타입 기반으로 교체 — 매 프레임 WeaponEquipManager.CurrentType을
    /// 읽어 WeaponRangeIndicator.RangeOf(type)로 반경 산정, Fist(맨손)면 숨김(구형 지표와 동일 조건).
    /// 구형 WeaponRangeIndicator는 렌더링하지 않음(중복 링 제거) — 반경 데이터는 RangeOf 단일 소스 사용.
    /// </summary>
    public class PlayerRangeRing : MonoBehaviour
    {
        private Func<float> _rangeProvider;
        private GameObject _player;      // [P23] 발 위치 보정용 — 루트=캡슐 중심 계약
        private CharacterController _cc; // [P23] 매 프레임 발 위치 보정(구르기 height 변화 대응)
        private Renderer _renderer;      // [P23] 맨손 숨김용 렌더러 캐시
        private Transform _quad;
        private Material _mat;
        private bool _fallback;

        public static PlayerRangeRing Ensure(GameObject player, Func<float> rangeProvider)
        {
            var existing = player.GetComponentInChildren<PlayerRangeRing>(true);
            if (existing != null) { existing._player = player; existing._rangeProvider = rangeProvider; return existing; }

            var go = new GameObject("PlayerRangeRing");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            var ring = go.AddComponent<PlayerRangeRing>();
            ring._player = player;
            ring._rangeProvider = rangeProvider;
            ring.Build();
            return ring;
        }

        private void Build()
        {
            // [P23] 발 위치 보정 — 플레이어 루트는 캡슐 중심(계약)이므로
            // localPosition.y = 0.02 - (height/2 - center.y)로 내려 링을 발밑 지면에 붙인다 (cc 없으면 0.02 유지).
            float liftY = 0.02f;
            if (_player != null)
            {
                var cc = _player.GetComponent<CharacterController>();
                if (cc != null)
                {
                    _cc = cc;
                    liftY = 0.02f - (cc.height * 0.5f - cc.center.y);
                }
            }
            transform.localPosition = new Vector3(0f, liftY, 0f);

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
            _renderer = r;   // [P23] LateUpdate 표시/숨김 제어용
        }

        private void LateUpdate()
        {
            if (_quad == null) return;

            // [P23] 발 위치 매 프레임 보정 — 구르기 중 cc.height가 50%로 Lerp되므로 Build 고정값은 어긋난다.
            if (_cc != null)
                transform.localPosition = new Vector3(0f, 0.02f - (_cc.height * 0.5f - _cc.center.y), 0f);

            // [P23] 반경/표시 소스 = 무기 타입 — 매 프레임 WeaponEquipManager.CurrentType을 읽어 갱신
            // (구형 WeaponRangeIndicator와 동일 조건: 반경 = RangeOf(type), 맨손(Fist)은 숨김).
            var type = WeaponEquipManager.CurrentType;
            float range = WeaponRangeIndicator.RangeOf(type);
            // 하위호환: provider는 폴백 전용(구 호출부 잔존 대비) — null이면 타입 기반만 사용.
            if (range <= 0f && _rangeProvider != null)
                range = Mathf.Max(1f, _rangeProvider());
            range = Mathf.Max(1f, range);

            // 표시: 맨손(Fist)면 렌더러 비활성화 — 구형 지표와 동일 조건.
            if (_renderer == null && _quad != null) _renderer = _quad.GetComponent<Renderer>();
            bool show = type != WeaponType.Fist;
            if (_renderer != null && _renderer.enabled != show)
                _renderer.enabled = show;

            // 셰이더 링: 반지름 = 쿼드 반폭 → 지름 = 2×range. 폴백 텍스처: 링 반지름 = 반폭×0.92.
            float diameter = _fallback ? (2f * range) / 0.92f : 2f * range;
            _quad.localScale = Vector3.one * diameter;
        }
    }
}
