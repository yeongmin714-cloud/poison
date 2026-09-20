using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [Milestone E] 채집/광물 채널링 진행도 — 작업 유닛 위 얇은 월드 진행 바.
    /// 채널 시작 시 Show(), 완료/취소 시 Close(). 0→1 채워지며 카메라 방향으로 빌보딩(Billboard).
    /// 프리미티브 콜라이더를 제거해 커서/RTS 레이캐스트 오염 0.
    /// </summary>
    public class WorkProgressBar : MonoBehaviour
    {
        public float duration = 2.5f;
        public Color fillColor = new Color(0.3f, 0.9f, 1f, 1f);

        private float _start;
        private Transform _fill;
        private Material _fillMat;

        /// <summary>작업 유닛 위에 진행 바 생성(부모=worker, 발 위 2.5m).</summary>
        public static WorkProgressBar Show(Transform worker, float duration, Color color)
        {
            var go = new GameObject("WorkProgressBar");
            go.transform.SetParent(worker, false);
            go.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            var bar = go.AddComponent<WorkProgressBar>();
            bar.duration = Mathf.Max(0.1f, duration);
            bar.fillColor = color;
            bar.Build();
            return bar;
        }

        public void Close()
        {
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        private void Build()
        {
            _start = Time.time;
            BuildQuad(new Color(0f, 0f, 0f, 0.5f), new Vector3(0f, 0f, 0f), 0.8f);   // 배경
            _fill = BuildQuad(fillColor, new Vector3(-0.4f, 0.02f, 0f), 0.8f).transform; // 채움(좌 정렬)
            _fillMat = _fill.GetComponent<Renderer>()?.sharedMaterial;
        }

        private GameObject BuildQuad(Color color, Vector3 localPos, float width)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            quad.name = "BarQuad";
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(width, 0.07f, 1f);

            var rend = quad.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Standard");
                if (s != null)
                {
                    var mat = new Material(s);
                    mat.color = color;
                    if (color.a < 1f) mat.SetFloat("_Surface", 1f); // transparent
                    rend.sharedMaterial = mat;
                }
            }
            return quad;
        }

        private void LateUpdate()
        {
            float p = Mathf.Clamp01((Time.time - _start) / duration);
            if (_fill != null)
                _fill.localScale = new Vector3(0.8f * p, 0.07f, 1f);

            // 빌보딩 — 카메라 정면
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
                transform.rotation = Quaternion.LookRotation(fwd);
            }

            if (p >= 1f)
                Close();
        }

        private void OnDestroy()
        {
            if (_fillMat != null) Destroy(_fillMat);
        }
    }
}
