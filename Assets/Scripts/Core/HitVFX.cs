using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// VFX/타격 반응 정적 유틸리티.
    /// Sparks, HitFlash, DamageNumber 등 간단한 시각적 효과를 처리.
    /// </summary>
    public static class HitVFX
    {
        // ── 공유 Spark 프리팹 자산 (지연 초기화) ──────────────────────
        private static Mesh _sphereMesh;
        private static Material _sparkMaterial;
        private static readonly int ColorPropId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// PlayHitEffect용 공유 자산을 한 번만 로드한다.
        /// </summary>
        private static void EnsureSparkAssets()
        {
            if (_sphereMesh != null) return;

            // 구체 Mesh 가져오기 (Primitive에서 추출 후 임시 객체 정리)
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);

            // 공유 Spark Material 생성 (Shader 캐싱)
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                _sparkMaterial = new Material(litShader)
                {
                    color = Color.yellow
                };
                _sparkMaterial.name = "HitSpark_SharedMat";
            }
        }

        // ── PlayHitEffect ────────────────────────────────────────────

        /// <summary>
        /// Sparks 파티클 생성 — 작은 노란 구체를 Instantiate 후 0.3초 후 Destroy.
        /// CreatePrimitive 대신 직접 GameObject + MeshFilter 구성으로 Collider 오버헤드 제거.
        /// Material/Shader는 정적으로 캐싱하여 GC 부하 최소화.
        /// </summary>
        /// <param name="position">생성 위치</param>
        /// <param name="normal">타격 표면 법선 방향</param>
        public static void PlayHitEffect(Vector3 position, Vector3 normal)
        {
            EnsureSparkAssets();

            if (_sparkMaterial == null || _sphereMesh == null)
            {
                Debug.LogWarning("[HitVFX] Spark 자산 로드 실패 — Shader 또는 Mesh 누락");
                return;
            }

            GameObject spark = new GameObject("HitSpark")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            spark.transform.position = position + normal * 0.1f;
            spark.transform.localScale = Vector3.one * 0.15f;

            var filter = spark.AddComponent<MeshFilter>();
            filter.sharedMesh = _sphereMesh;

            var renderer = spark.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _sparkMaterial;

            // 0.3초 후 제거
            Object.Destroy(spark, 0.3f);
        }

        // ── PlayHitFlash ────────────────────────────────────────────

        /// <summary>
        /// 피격 시 Renderer의 baseColor를 MaterialPropertyBlock으로 하양 전환 후
        /// 0.1초 뒤 원래 색상 복원.
        ///
        /// renderer.material 접근(자동 인스턴스화) 대신 MaterialPropertyBlock을 사용하여
        /// Material 인스턴스 누수와 GC 부하를 방지한다.
        /// 다중 호출 시에도 각각 독립적인 PropertyBlock이 아닌,
        /// 마지막 Block이 renderer에 설정되므로 마지막 효과만 보인다.
        /// </summary>
        /// <param name="renderer">대상 Renderer</param>
        public static void PlayHitFlash(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null) return;

            // 원래 색상 저장 (sharedMaterial — MPB가 적용되지 않은 기본 색상)
            Color originalColor = renderer.sharedMaterial.color;

            // MaterialPropertyBlock으로 Override
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(ColorPropId, Color.white);
            renderer.SetPropertyBlock(block);

            // 복원 Runner (비활성 상태로 생성 → Init 후 활성화하여 Awake 타이밍 이슈 방지)
            var runner = new GameObject("HitFlash_Runner")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            runner.SetActive(false);
            var hitFlash = runner.AddComponent<HitFlashRunner>();
            hitFlash.Init(renderer, block, originalColor);
            runner.SetActive(true);
        }

        // ── SpawnDamageNumber ───────────────────────────────────────

        /// <summary>
        /// WorldSpace에 Damage Number (TextMesh 3D)를 생성, 위로 FadeOut 후 Destroy.
        /// </summary>
        /// <param name="position">월드 좌표 위치</param>
        /// <param name="damage">표시할 데미지 값</param>
        public static void SpawnDamageNumber(Vector3 position, float damage)
        {
            GameObject textGO = new GameObject("DamageNumber")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            textGO.transform.position = position + Vector3.up * 0.5f;

            TextMesh text = textGO.AddComponent<TextMesh>();
            text.text = Mathf.RoundToInt(damage).ToString();
            text.fontSize = 36;
            text.color = Color.red;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.1f;

            textGO.AddComponent<DamageNumberRunner>();
        }
    }

    // ── HitFlashRunner ──────────────────────────────────────────────

    /// <summary>
    /// HitFlash 처리를 위한 내부 MonoBehaviour (Update 기반 복원).
    /// MaterialPropertyBlock을 통해 Renderer의 baseColor를 일시적으로 White로 덮어쓰고,
    /// 지정 시간 후 복원한다.
    /// </summary>
    internal class HitFlashRunner : MonoBehaviour
    {
        private Renderer _targetRenderer;
        private MaterialPropertyBlock _block;
        private Color _originalColor;
        private float _elapsed;
        private const float DURATION = 0.1f;
        private static readonly int ColorPropId = Shader.PropertyToID("_BaseColor");

        public void Init(Renderer renderer, MaterialPropertyBlock block, Color originalColor)
        {
            _targetRenderer = renderer;
            _block = block;
            _originalColor = originalColor;
        }

        private void Awake()
        {
            // GameObject가 비활성 상태로 생성 → Init → 활성화되므로
            // Awake 시점에는 Init이 이미 호출된 상태임.
            if (_targetRenderer == null)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (!(_elapsed >= DURATION)) return;

            if (_targetRenderer != null && _block != null)
            {
                // 원래 색상 복원 후 PropertyBlock에 반영
                _block.SetColor(ColorPropId, _originalColor);
                _targetRenderer.SetPropertyBlock(_block);
            }

            Destroy(gameObject);
        }
    }

    // ── DamageNumberRunner ──────────────────────────────────────────

    /// <summary>
    /// DamageNumber 애니메이션: 위로 떠오르며 FadeOut
    /// </summary>
    internal class DamageNumberRunner : MonoBehaviour
    {
        private TextMesh _text;
        private float _elapsed;
        private const float DURATION = 1.0f;
        private const float RISE_SPEED = 1.5f;

        private void Start()
        {
            _text = GetComponent<TextMesh>();
            if (_text == null)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (_text == null)
            {
                Destroy(gameObject);
                return;
            }

            _elapsed += Time.deltaTime;

            // 위로 떠오르기
            transform.position += Vector3.up * RISE_SPEED * Time.deltaTime;

            // FadeOut
            float t = _elapsed / DURATION;
            float alpha = Mathf.Lerp(1f, 0f, t);
            Color c = _text.color;
            _text.color = new Color(c.r, c.g, c.b, alpha);

            if (_elapsed >= DURATION)
            {
                Destroy(gameObject);
            }
        }
    }
}
