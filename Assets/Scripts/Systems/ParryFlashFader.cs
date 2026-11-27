using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [패링P2-A] PERFECT 패링 화이트 프레임 페이더 — 카메라 자식 풀스크린 쿼드.
    /// unscaled 시간 기준(히트스톱 중에도 재생) 알파 0→피크→0 후 자가 파괴. Systems 계약: UI 어셈블리 직접 참조 없음.
    /// </summary>
    public sealed class ParryFlashFader : MonoBehaviour
    {
        private Material _mat;
        private Color _color;
        private float _peakAlpha;
        private float _duration;
        private float _elapsed;

        public void Init(Color color, float peakAlpha, float duration)
        {
            _color = color;
            _peakAlpha = Mathf.Clamp01(peakAlpha);
            _duration = Mathf.Max(0.01f, duration);
            var renderer = GetComponent<Renderer>();
            if (renderer == null) { Destroy(gameObject); return; }

            // [무번호 16-D2 수리] PERFECT 화이트 프레임 실카메라 렌더 결손 — 프로브 cfx_08 픽셀 계량
            //   순수흰색(>235 채널) 비율 0.00%, 참조 영상("패링 공격씬 테스트.mp4" 51s)에서도 미표출.
            //   원인: URP/Lit transparent surface 핸드튜닝(_Surface+keyword 세트)이 실카메라 단일 카메라
            //   SubmitRenderRequest 경유에서 알파 반영에 실패 → 시각 생략으로 보였던 것. 위조 배선 금지 계약에 따라
            //   이미 실측 검증된 Phase A 별섬광 경로(Sprites/Default 투명 스프라이트 셰이더, cfx_09에서 정상 렌더)
            //   로 교체한다. Init 시그니처·duration 체계는 유지.
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) { Destroy(gameObject); return; }
            _mat = new Material(shader) { name = "ParryFlash_Unlit" };
            _mat.hideFlags = HideFlags.HideAndDontSave;
            if (_mat.HasProperty("_Surface"))
            {
                _mat.SetFloat("_Surface", 1f); // Transparent
                _mat.SetOverrideTag("RenderType", "Transparent");
                _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_ZWrite", 0);
                _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            _mat.SetColor("_BaseColor", new Color(_color.r, _color.g, _color.b, 0f));
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", new Color(_color.r, _color.g, _color.b, 0f));
            _mat.SetFloat("_Smoothness", 0f);
            renderer.sharedMaterial = _mat;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            // [205차] 0→피크(전 25%)→피크 홀드(25~55%)→0(후 45%) — 순백 계약 수리(청백 왜곡 제거).
            //   기존 삼각 램프(0→피크→0)는 프레임 샘플링(60fps, 0.07s 기준 스텝≈0.24)이 피크 지점을 정확히
            //   관통하지 못해 어떤 프레임도 오파크(a=1)에 도달하지 못함 → Sprites/Default 알파 블렌드상
            //   씬 블리딩이 항상 잔존(청톤 씬에서 청백 왜곡 — 205차 사용자 재보고 근원).
            //   홀드 구간이 최소 1프레임 오파크(a=_peakAlpha)를 보장 — 씬 색과 무관한 순백 프레임 보장.
            //   unscaled 시간 기준(히트스톱 동기 물림)과 Init 시그니처는 무손상(Phase D 계약 유지).
            float a = t < 0.25f
                ? Mathf.Lerp(0f, _peakAlpha, t / 0.25f)
                : t < 0.55f
                    ? _peakAlpha
                    : Mathf.Lerp(_peakAlpha, 0f, (t - 0.55f) / 0.45f);
            if (_mat != null)
            {
                Color c = new Color(_color.r, _color.g, _color.b, a);
                if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
                if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", c);
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
