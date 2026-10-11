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

        private Material _mat;
        private Renderer _rend;
        private float _elapsed;
        private Vector3 _startScale;
        private Vector3 _visualScale;

        /// <summary>Legacy public helper retained for callers outside the active command-ring path.</summary>
        public static Material CreateRingMaterial(Color color) => CommandRingPresentation.CreateMaterial(color);

        /// <summary>Legacy fallback test retained for existing callers.</summary>
        public static bool IsFallback(Material m) => m != null && m.name.Contains("Fallback");

        private void Awake()
        {
            // Collider-free ground quad shared by every active command ring.
            var quad = new GameObject("CommandRingQuad");
            quad.AddComponent<MeshFilter>().sharedMesh = GetRingQuadMesh();
            quad.AddComponent<MeshRenderer>();
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float figmaAspect = CommandRingPresentation.FigmaCoreRingAspect;
            float width = Mathf.Sqrt(figmaAspect);
            float depth = 1f / width;
            // The quad's mesh lies in local XY; after the 90-degree X rotation, local Y becomes ground-plane depth.
            _visualScale = new Vector3(width, depth, 1f);
            quad.transform.localScale = _visualScale;
            _rend = quad.GetComponent<Renderer>();
            // [2026-10-11 사용자 요구 — 검은 사각형 제거] 링 쿼드는 지면 데칼 — 실제 그림자 방출/수신 금지.
            //   태양 광원 아래에서 쿼드 그림자가 지면에 사각형으로 찍히는 결함의 근원 차단.
            _rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rend.receiveShadows = false;
            ApplyColor();
            // Keep the object's authored parent scale intact; its pulse remains independent of the child oval ratio.
            _startScale = transform.localScale;
        }

        private static Mesh _quadMesh;
        private static Mesh GetRingQuadMesh()
        {
            if (_quadMesh != null) return _quadMesh;
            _quadMesh = new Mesh { name = "CommandRingQuadMesh" };
            _quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            _quadMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            _quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quadMesh.RecalculateNormals();
            _quadMesh.RecalculateBounds();
            return _quadMesh;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            transform.localScale = CommandRingPresentation.GetPulseScale(_startScale, _elapsed);
            // [15차 입체감] rim 하이라이트 방향 = 화면-상(카메라 전방 XZ).
            CommandRingPresentation.UpdateRimDirection(_rend, _mat);
        }

        private void ApplyColor()
        {
            if (_mat != null) Destroy(_mat);
            _mat = CommandRingPresentation.Apply(_rend, color);
        }

        /// <summary>Existing public tint API retained.</summary>
        public void SetColor(Color c)
        {
            color = c;
            ApplyColor();
            if (_rend != null) _rend.enabled = true;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
