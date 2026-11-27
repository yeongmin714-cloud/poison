using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 37-G3] 위험 방향 인디케이터 — 시야 밖 공격원 방향을 화면 가장자리에 0.3s 아크로 표시.
    /// 계약: Core 무수정(OnPlayerDamaged 구독만 — CombatCameraEffects 28-M2 선례), 카메라 자식 쿼드
    /// (ScreenFlashFX ParryFlashFader 선례), 절차 아크 텍스처(결정론), 인스턴스 1개 재사용(누적 금지),
    /// Play 전용 + try/catch(시각 보조 — 전투 방해 금지), unscaled 시간(히트스톱 중 가동 — 31-V3 선례).
    /// </summary>
    public static class ScreenEdgeDangerFX
    {
        private const float ShowSeconds = 0.3f;
        private static bool _subscribed;
        private static EdgeRunner _runner;

        /// <summary>구독 보장 — PlayerCombat 부트 경유 1회(CombatCameraEffects 구독 사이트 동기).</summary>
        public static void EnsureSubscribed()
        {
            if (_subscribed) return;
            try { ProjectName.Core.PlayerHealth.OnPlayerDamaged += OnPlayerDamaged; _subscribed = true; }
            catch (System.Exception) { }
        }

        private static void OnPlayerDamaged(Vector3 hitPos, float amount)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return;
                Vector3 dir = hitPos - cam.transform.position;
                // 카메라 로컬 공간으로 변환 — right/up 성분으로 가장자리 방향 산출
                Vector3 local = cam.transform.InverseTransformDirection(dir.normalized);
                Vector2 dir2 = new Vector2(local.x, local.y);
                // 전방(z>0)이고 화면 내(여유 0.05)면 표시 불필요 — 시야 밖 공격만
                float aspect = cam.aspect;
                Vector2 half = new Vector2(0.5f * aspect, 0.5f);
                bool inside = local.z > 0f && Mathf.Abs(dir2.x / half.x) < 0.9f && Mathf.Abs(dir2.y / half.y) < 0.9f && dir2.sqrMagnitude < 0.9f;
                if (inside) return;
                if (dir2.sqrMagnitude < 0.0001f) dir2 = Vector2.left;
                dir2.Normalize();

                EnsureRunner(cam);
                if (_runner != null) _runner.Show(dir2, ShowSeconds);
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 방해 금지
        }

        private static void EnsureRunner(Camera cam)
        {
            if (_runner != null) return;
            var go = new GameObject("ScreenEdgeDangerFX");
            go.transform.SetParent(cam.transform, false);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(quad.GetComponent<Collider>());
            quad.name = "DangerArc";
            quad.transform.SetParent(go.transform, false);
            quad.transform.localScale = new Vector3(0.34f, 0.10f, 1f);   // 카메라 로컬 단위(원점 기준 화면 피트)
            quad.transform.localPosition = new Vector3(0f, 0f, 0.35f);   // near plane 앞
            var mr = quad.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "DangerArcMat" };
            mat.mainTexture = BuildArcTexture(256);
            mat.color = new Color(1f, 0.25f, 0.18f, 0.85f);   // 위험 적색 — MonsterDanger 계열(단일 소스 경유)
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            FxMaterialLibrary.EnsureUnlitTransparent(mat);   // [무번호 39] 표준화
            mat.renderQueue = 3050;
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _runner = go.AddComponent<EdgeRunner>();
            _runner.Bind(quad.transform, mat);
        }

        /// <summary>[무번호 37-G3] 절차 위험 아크 텍스처 — 외곽 호(밴드) + 끝단 테이퍼(결정론, 캐시 1회).</summary>
        private static Texture2D BuildArcTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;   // 0~1
                    // 아크 밴드: 반경 0.86~1.0(가장자리), 수평 중심부가 두꺼운 테이퍼
                    float band = Mathf.SmoothStep(0.86f, 0.92f, r) * (1f - Mathf.SmoothStep(0.95f, 1.0f, r));
                    float taper = Mathf.Clamp01(1f - Mathf.Abs(x - c) / c * 0.55f);
                    byte a = (byte)(Mathf.Clamp01(band * taper) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>러너 — 카메라 자식. Show(dir2): 방향각으로 아크 배치(가장자리 42% 지점) + 0→1→0 램프.</summary>
        internal sealed class EdgeRunner : MonoBehaviour
        {
            private Transform _arc;
            private Material _mat;
            private float _elapsed = -1f;
            private float _total = ShowSeconds;
            private Vector2 _dir;

            public void Bind(Transform arc, Material mat) { _arc = arc; _mat = mat; }

            public void Show(Vector2 dir2, float seconds)
            {
                _dir = dir2;
                _total = Mathf.Max(0.05f, seconds);
                _elapsed = 0f;
                enabled = true;
                if (_arc != null)
                {
                    // 화면 피트: 원점 기준 (dir2 × aspect 보정 42% 지점), 아크의 접선이 원을 향하도록 회전
                    float ang = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
                    _arc.localPosition = new Vector3(dir2.x * 0.42f, dir2.y * 0.42f, 0.35f);
                    _arc.localRotation = Quaternion.Euler(0f, 0f, ang - 90f);
                }
            }

            private void Update()
            {
                if (_elapsed < 0f) return;
                _elapsed += Time.unscaledDeltaTime;   // 31-V3 선례 — 히트스톱 중 가동
                float t = Mathf.Clamp01(_elapsed / _total);
                float a = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;   // 빠른 진입→선형 소멸
                if (_mat != null)
                {
                    var c = _mat.color; c.a = 0.85f * a; _mat.color = c;
                }
                if (_elapsed >= _total)
                {
                    _elapsed = -1f;
                    enabled = false;   // 인스턴스 재사용(누적 금지)
                }
            }
        }
    }
}
