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
        private bool _manual;          // [142차 F] 외부 진행 구동 모드(건설 게이지 — 관리자가 매 틱 progress 대입)
        private float _manualProgress;
        private float _lastProbe;      // [150차 P3] 가시성 프로브 1초 스로틀

        /// <summary>[150차 P3] 건설 게이지(manual 모드) 바 확대 배율 — 원거리 관측 픽셀 미달 실측
        /// (트랙 0.9m×0.11m → 실효 약 2.34m×0.29m). 채널링 Show 경로는 기존 크기 유지(벌목/채광 계약).</summary>
        private const float ManualBarScale = 2.6f;

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

        /// <summary>[142차 F] 외부 진행 구동 바 생성 — 건설 게이지(ConstructionManager가 매 틱 progress 대입).
        /// 시간 자체 완주가 아니라 관리자 진행값을 그대로 반영(병사 지원 배율 등 가변 속도 정합).
        /// 채널링 바(Show)와 동일 어휘/크기/빌보딩 — 사용자 지시 "벌목·채광처럼 위에 게이지바".</summary>
        public static WorkProgressBar ShowManual(Transform anchor, Color color)
        {
            var go = new GameObject("WorkProgressBar_Manual");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            // [150차 P3] 바 스케일 확대 — manual(건설) 모드에서만 루트 스케일 배율 적용.
            // 자식 트랙/채움 로컬 크기는 그대로(트랙 종횡비 8:1 유지)라 좌정렬·빌보딩 수학 무영향.
            go.transform.localScale = new Vector3(ManualBarScale, ManualBarScale, 1f);
            var bar = go.AddComponent<WorkProgressBar>();
            bar.duration = float.MaxValue;   // 시간 완주 미사용 — SetProgress01 외부 구동
            bar.fillColor = color;
            bar._manual = true;
            bar.Build();
            return bar;
        }

        /// <summary>[142차 F] 외부 진행 대입(0~1) — 1 도달 시 자동 Close 없음(관리자가 Close 책임).</summary>
        public void SetProgress01(float p)
        {
            _manualProgress = Mathf.Clamp01(p);
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
            // [150차 P3] 가시성 프로브 — 1초 스로틀(미표출 원인 판별: 거리/가림/모드 구분 근거 수집).
            //   isVisible은 프레임 렌더 반영값(참고용 — 가림 여부 판별 근거).
            if (Time.time - _lastProbe >= 1f)
            {
                _lastProbe = Time.time;
                Camera probeCam = Camera.main;
                float dist = probeCam != null ? Vector3.Distance(probeCam.transform.position, transform.position) : -1f;
                bool visible = false;
                var rends = GetComponentsInChildren<Renderer>();
                for (int i = 0; i < rends.Length; i++)
                    if (rends[i].isVisible) { visible = true; break; }
                Debug.Log($"[150차 P3] 게이지 가시성 프로브 — pos=({transform.position.x:F1},{transform.position.y:F1},{transform.position.z:F1}) " +
                          $"카메라거리={dist:F1} isVisible={visible} manual={_manual}");
            }

            // [142차 F] 외부 구동 모드 — 관리자 대입 progress 사용(시간 자체 진행 폐기).
            float p = _manual ? _manualProgress : Mathf.Clamp01((Time.time - _start) / duration);

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

            if (!_manual && p >= 1f)
                Close();   // [142차 F] 수동 모드는 관리자가 Close(완료/제거 경로) — 자동 소멸 없음
        }

        private void OnDestroy()
        {
            if (_fillMat != null) Destroy(_fillMat);
        }
    }
}
