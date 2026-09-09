using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 지면 충격파 링 VFX 정적 유틸리티 (치명타/처치 피드백).
    /// 월드 위치에 평평한 링을 생성해 maxRadius까지 확장, 알파 페이드 후 스스로 제거.
    /// </summary>
    public static class ShockwaveRingFX
    {
        // ── 상수 ─────────────────────────────────────────────────────
        private const float START_RADIUS = 0.2f;    // 링 시작 반지름
        private const float RING_THICKNESS = 0.05f; // 링 두께 (Y)
        private const float CYL_RADIUS = 0.5f;      // Cylinder 프리미티브 기본 반지름
        private const float CYL_HEIGHT = 1f;        // Cylinder 프리미티브 기본 높이

        // ── 공유 자산 (지연 초기화) ──────────────────────────────────
        private static Mesh _cylinderMesh;
        private static Shader _litShader;

        /// <summary>
        /// 충격파 링 생성 — 시작 반지름 0.2에서 maxRadius까지 duration 동안
        /// Ease-Out 트윈으로 확장하며 알파 페이드, 종료 후 Destroy.
        /// </summary>
        /// <param name="worldPos">링 중심 월드 좌표</param>
        /// <param name="maxRadius">최종 반지름</param>
        /// <param name="color">링 색상 (알포 포함, 페이드 시작 알파로 사용)</param>
        /// <param name="duration">확장 + 페이드 지속 시간(초)</param>
        public static void Spawn(Vector3 worldPos, float maxRadius, Color color, float duration)
        {
            EnsureAssets();

            if (_cylinderMesh == null || _litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 자산 로드 실패 — Shader 또는 Mesh 누락");
                return;
            }

            // 안전 클램프
            if (maxRadius < START_RADIUS) maxRadius = START_RADIUS;
            if (duration <= 0f) duration = 0.1f;

            // 링 GameObject (직접 MeshFilter/MeshRenderer 구성 — Collider 오버헤드 제거)
            GameObject ring = new GameObject("ShockwaveRing")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            ring.transform.position = worldPos;
            ring.transform.rotation = Quaternion.identity; // 링 축 = 월드 Y (위쪽)

            // Cylinder 프리미티브 메시를 납작하게 눌러 지면 링으로 사용
            // (SpecialEffectsController 선택 링 참조: 기본 반지름 0.5, 기본 높이 1)
            float startScale = START_RADIUS / CYL_RADIUS;
            ring.transform.localScale = new Vector3(startScale, RING_THICKNESS / CYL_HEIGHT, startScale);

            var filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = _cylinderMesh;

            var renderer = ring.AddComponent<MeshRenderer>();

            // 반투명 Lit Material (스폰별 인스턴스 — 동시 다중 링 알파 페이드 충돌 방지)
            var mat = new Material(_litShader)
            {
                color = color
            };
            mat.name = "ShockwaveRingMat";

            // 투명 설정 (SpecialEffectsController 선택 링 머티리얼 참조)
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");

            renderer.sharedMaterial = mat;

            // 확장/페이드 Runner 부착 (Update 기반 트윈 후 자기 파괴)
            ring.AddComponent<RingRunner>().Init(maxRadius, color, duration);

            Debug.Log($"[ShockwaveRingFX] spawn r={maxRadius} at {worldPos}");
        }

        /// <summary>
        /// 공유 자산을 한 번만 로드한다 (HitVFX 캐싱 참조).
        /// </summary>
        private static void EnsureAssets()
        {
            if (_cylinderMesh != null && _litShader != null) return;

            // Cylinder Mesh 추출 후 임시 객체 정리
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _cylinderMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);

            // Lit Shader 캐싱 (URP 우선, Standard 폴백)
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
                litShader = Shader.Find("Standard");
            _litShader = litShader;
        }

        // ── RingRunner ───────────────────────────────────────────────

        /// <summary>
        /// 링 확장 + 알파 페이드 처리 내부 MonoBehaviour.
        /// Update에서 반지름 0.2 → maxRadius로 Ease-Out 트윈, 알파 페이드 후
        /// Object.Destroy(gameObject)로 스스로 제거된다.
        /// </summary>
        private class RingRunner : MonoBehaviour
        {
            private Material _mat;
            private Color _baseColor;
            private float _maxRadius;
            private float _duration;
            private float _elapsed;

            public void Init(float maxRadius, Color color, float duration)
            {
                _maxRadius = maxRadius;
                _baseColor = color;
                _duration = duration;
                _elapsed = 0f;

                var mr = GetComponent<MeshRenderer>();
                _mat = mr != null ? mr.sharedMaterial : null;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);

                // Ease-Out 트윈: 빠르게 퍼지고 서서히 멈추는 충격파 감쇠
                float eased = 1f - (1f - t) * (1f - t);
                float radius = Mathf.Lerp(START_RADIUS, _maxRadius, eased);
                float scale = radius / CYL_RADIUS;
                transform.localScale = new Vector3(scale, RING_THICKNESS / CYL_HEIGHT, scale);

                // 알파 페이드 (색상은 유지, 알파만 0으로 감쇠)
                if (_mat != null)
                {
                    float alpha = Mathf.Lerp(_baseColor.a, 0f, t);
                    _mat.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
                }

                if (t >= 1f)
                    Destroy(gameObject);
            }

            private void OnDestroy()
            {
                // 조기 파괴(씬 전환 등) 시 Material 누수 방지
                if (_mat != null)
                    Object.Destroy(_mat);
            }
        }
    }
}
