using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// SC2/RTS식 부대 선택 하이라이트 링 — 셰이더(SelectionRing.shader)를 Quad에 입혀
    /// 발밑에 원형 링을 렌더한다. 콜라이더를 철저히 제거해 HoverTargetClassifier/RTS 레이캐스트를 오염시키지 않는다.
    /// 프리팹은 저작하지 않고 Awake에서 절차 생성(Quad + 머티리얼) — 에이전트 자율 저작의 .prefab 비신뢰성 회피.
    /// </summary>
    public class SelectionRingController : MonoBehaviour
    {
        [Tooltip("팀/국가 색 — SetColor로 주입")]
        public Color color = new Color(0.2f, 0.5f, 1f);

        private static Shader _shader;
        private Material _mat;
        private Renderer _rend;

        private void Awake()
        {
            if (_shader == null)
                _shader = Shader.Find("Custom/SelectionRing");

            // 발밑 지면 링 — Quad를 XZ 평면(위쪽 노멀)으로 눕힌다.
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "SelectionRingQuad";
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one;

            // 콜라이더 제거 — 커서 분류/명령 레이캐스트 오염 방지
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _rend = quad.GetComponent<Renderer>();
            if (_shader != null)
            {
                _mat = new Material(_shader);
                _mat.SetColor("_TeamColor", color);
                if (_rend != null) _rend.sharedMaterial = _mat;
            }
        }

        /// <summary>팀/국가 색 주입 (생성 직후 GuardSelectionManager가 호출).</summary>
        public void SetColor(Color c)
        {
            color = c;
            if (_mat != null) _mat.SetColor("_TeamColor", c);
            if (_rend != null) _rend.enabled = true;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
