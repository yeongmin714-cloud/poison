using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 37-H3] 지면 크랙 데칼 — 강타격(크리/피해≥15) 시 타격 지점 지면 균열(절차 텍스처, 2.5s 페이드).
    /// 계약: 동시 상한 2 예산 가드(33-A1 FxImpactLight 선례 — 초과 시 최생성분 파괴), 결정론 해시 분지(Random 0건),
    /// 지면 스냅=하향 레이캐스트(실패 시 y=0.03 폴백), 머티리얼 인스턴스 파괴(누출 금지), EditMode 가드(21-K1 선례).
    /// </summary>
    public static class FxGroundCrackFX
    {
        private const int Budget = 2;
        private const float FadeSeconds = 2.5f;
        private static readonly System.Collections.Generic.List<CrackRunner> _alive = new System.Collections.Generic.List<CrackRunner>();
        private static Texture2D _crackTex;

        /// <summary>크랙 발화 — PlayHitFXInternal(크리/Heavy)에서 호출. false=가드/부재(무영향).</summary>
        public static bool Burst(Vector3 hitPosition)
        {
            if (!Application.isPlaying) return false;   // EditMode 가드
            // 지면 스냅 — 하향 레이캐스트(5m), 실패 시 y=0.03 폴백
            Vector3 pos = hitPosition;
            if (Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point;
            else pos.y = 0.03f;
            pos.y += 0.03f;   // BlobShadow 0.05 선례 준용 — 지면 언더플로 방지

            // 예산 가드(2) — 초과 시 최생성분 즉시 정리
            while (_alive.Count >= Budget)
            {
                var oldest = _alive[0];
                if (oldest != null) oldest.KillNow();
                _alive.RemoveAt(0);
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return false;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "GroundCrackFX";
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, DeterministicAngle(hitPosition), 0f);
            go.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            if (_crackTex == null) _crackTex = BuildCrackTexture(256);
            var mat = new Material(shader) { name = "GroundCrackMat" };
            mat.mainTexture = _crackTex;
            mat.color = new Color(0.10f, 0.09f, 0.08f, 0.9f);   // 그을음 다크 — 머티리얼 인스턴스 소유
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            FxMaterialLibrary.EnsureUnlitTransparent(mat);   // [무번호 39] 표준화 — 검은 사각 렌더 수리(영상 실측)
            mat.renderQueue = 2990;   // 링(3000) 아래 — 지면 오염 계층
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var runner = go.AddComponent<CrackRunner>();
            runner.Bind(mat, FadeSeconds);
            _alive.Add(runner);
            return true;
        }

        /// <summary>결정론 배각 — 히트 위치 해시(같은 지점=같은 각도, Random 0건).</summary>
        private static float DeterministicAngle(Vector3 p)
        {
            float s = Mathf.Sin(p.x * 12.9898f + p.z * 78.233f) * 43758.5453f;
            return (s - Mathf.Floor(s)) * 360f;
        }

        /// <summary>절차 균열 텍스처 — 중심에서 6분지(각도 지터=해시), 선분 래스터(캐시 1회 — 결정론).</summary>
        private static Texture2D BuildCrackTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            int branches = 6;
            for (int b = 0; b < branches; b++)
            {
                float hseed = Mathf.Sin(b * 91.7f + 3.1f) * 43758.5453f; hseed -= Mathf.Floor(hseed);
                float ang = b * (360f / branches) + hseed * 24f;   // 해시 지터(결정론)
                float len = size * (0.30f + 0.10f * hseed);
                // 3분절 꺾임 경로
                Vector2 prev = new Vector2(c, c);
                float a = ang;
                for (int seg = 1; seg <= 3; seg++)
                {
                    a += (hseed * 2f - 1f) * 18f + Mathf.Sin(b * 13f + seg * 7.3f) * 10f;
                    Vector2 next = prev + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * (len / 3f);
                    float w = (4f - seg) * 0.9f;   // 팁으로 가늘어짐
                    DrawLine(px, size, prev, next, w);
                    prev = next;
                }
            }
            // 중심 밝기 완화 원
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    if (r < 0.08f) px[y * size + x] = new Color32(255, 255, 255, 200);
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>선분 래스터(두께 w — 세그먼트 거리 기하, 29-R1/35-R1 선례 패턴, 결정론).</summary>
        private static void DrawLine(Color32[] px, int size, Vector2 a, Vector2 b, float w)
        {
            int steps = (int)(Vector2.Distance(a, b) * 2f) + 2;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
                for (int oy = -2; oy <= 2; oy++)
                    for (int ox = -2; ox <= 2; ox++)
                    {
                        int x = cx + ox, y = cy + oy;
                        if (x < 0 || y < 0 || x >= size || y >= size) continue;
                        float d = Mathf.Sqrt(ox * ox + oy * oy);
                        if (d > w) continue;
                        byte a2 = (byte)(255f * (1f - d / Mathf.Max(1f, w)));
                        if (a2 > px[y * size + x].a) px[y * size + x] = new Color32(255, 255, 255, a2);
                    }
            }
        }

        /// <summary>러너 — 2.5s 알파 페이드 후 자가 파괴(머티리얼 인스턴스 파괴 — 누출 금지).</summary>
        internal sealed class CrackRunner : MonoBehaviour
        {
            private Material _mat;
            private float _elapsed;
            private float _total = FadeSeconds;

            public void Bind(Material mat, float total) { _mat = mat; _total = total; }

            public void KillNow()
            {
                Cleanup();
                Destroy(gameObject);
            }

            private void Cleanup()
            {
                var mr = GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                    Destroy(mr.sharedMaterial);   // 인스턴스 파괴(누출 금지)
                _alive.Remove(this);
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float a = Mathf.Clamp01(1f - _elapsed / _total);
                if (_mat != null) { var c = _mat.color; c.a = 0.9f * a; _mat.color = c; }
                if (_elapsed >= _total)
                {
                    Cleanup();
                    Destroy(gameObject);
                }
            }

        }
    }
}
