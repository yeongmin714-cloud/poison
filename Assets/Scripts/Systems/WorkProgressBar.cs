using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [Milestone E] 채집/광물 채널링 진행도 — 작업 유닛 위 얇은 월드 진행 바.
    /// 채널 시작 시 Show(), 완료/취소 시 Close(). 0→1 채워지며 카메라 방향으로 빌보딩(Billboard).
    /// 프리미티브 콜라이더를 제거해 커서/RTS 레이캐스트 오염 0.
    ///
    /// [2026-10-11 26차 수리] 프레임+좌정렬 채움으로 정합 — 기존 채움 쿼드는 중앙 피벗 스케일이라
    /// 좌정렬이 안 되고 배경 반폭만큼 왼쪽으로 삐져나갔다(테스트 15 영상 실측).
    /// [2026-10-11 27차 Fluent 개선] 프리미티브 단색 → 베이크 텍스처(라운드 트랙+스트로크 프레임 /
    /// 파랑 그라데이션 채움, GitHub-dark 톤). 베이크 PNG 규약(poison-unity-bake-png) 준수.
    /// [2026-10-11 28차 사용자 요구] 이동하는 마크(팁 글로우) 제거 — "파랑 게이지가 차오르는" 표현만.
    /// </summary>
    public class WorkProgressBar : MonoBehaviour
    {
        public float duration = 2.5f;
        public Color fillColor = new Color(0.3f, 0.9f, 1f, 1f);

        private float _start;
        private Transform _fill;
        private Material _fillMat;
        private float _trackW = 0.9f;

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

            // [2026-10-11 27차 Fluent] 구성: 베이크 트랙(라운드 프레임) + 베이크 채움(좌정렬 성장) + 팁 글로우.
            //   트랙 텍스처 종횡비 8:1(256×32) — 월드 크기도 8:1(0.9×0.112)로 정합.
            const float trackW = 0.9f, trackH = 0.112f;
            _trackW = trackW;
            BuildTexturedQuad("UI/GatherBarTrack", new Vector3(0f, 0f, 0.012f), trackW, trackH);

            // 채움 — 트랙 내부 좌우 인셋 0.05, 좌단 고정(LateUpdate에서 좌정렬 갱신).
            // [2026-10-11 28차 사용자 요구] 이동하는 마크(팁 글로우) 제거 — 파랑 게이지가 그냥 차오르는 표현만.
            _fill = BuildTexturedQuad("UI/GatherBarFill",
                new Vector3(-0.4f, 0f, 0.006f), 0.8f, trackH * 0.72f).transform;
        }

        private GameObject BuildTexturedQuad(string texturePath, Vector3 localPos, float width, float height)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            quad.name = "BarQuad_" + texturePath.Replace("UI/", "");
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(width, height, 1f);

            var rend = quad.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Standard");
                if (s != null)
                {
                    var mat = new Material(s);
                    var tex = Resources.Load<Texture2D>(texturePath);
                    if (tex != null)
                        mat.mainTexture = tex;   // URP Unlit: _BaseMap alias
                    mat.color = Color.white;
                    mat.SetFloat("_Surface", 1f);    // transparent
                    rend.sharedMaterial = mat;
                }
            }
            return quad;
        }

        private void LateUpdate()
        {
            float p = Mathf.Clamp01((Time.time - _start) / duration);

            // 좌단 고정 채움 — Quad는 중앙 피벗이라 위치를 함께 밀어 좌정렬 성장 구현.
            if (_fill != null)
            {
                const float fillW = 0.8f;   // 트랙 내부 채움 전체 폭(좌우 인셋 0.05)
                float w = fillW * p;
                _fill.localScale = new Vector3(w, _fill.localScale.y, 1f);
                _fill.localPosition = new Vector3(-0.4f + w * 0.5f, _fill.localPosition.y, _fill.localPosition.z);
            }
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
