using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// VFX/타격 반응 정적 유틸리티.
    /// Sparks, HitFlash, DamageNumber 등 간단한 시각적 효과를 처리.
    /// </summary>
    public static class HitVFX
    {
        /// <summary>
        /// Sparks 파티클 생성 — 작은 노란 구체를 Instantiate 후 0.3초 후 Destroy.
        /// </summary>
        /// <param name="position">생성 위치</param>
        /// <param name="normal">타격 표면 법선 방향</param>
        public static void PlayHitEffect(Vector3 position, Vector3 normal)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "HitSpark";
            spark.transform.position = position + normal * 0.1f;
            spark.transform.localScale = Vector3.one * 0.15f;

            Renderer renderer = spark.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.material.color = Color.yellow;
            }

            // 구체 Collider 제거 (시각적 효과만)
            Collider col = spark.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // 0.3초 후 제거
            Object.Destroy(spark, 0.3f);
        }

        /// <summary>
        /// 피격 시 Renderer의 material.color를 하양으로 변경 후 0.1초 뒤 원래 색상 복원.
        /// Renderer.material 접근으로 인한 Material 인스턴스화 누수를 방지하기 위해
        /// material 참조를 한 번만 가져와 캐싱 후 사용.
        /// </summary>
        /// <param name="renderer">대상 Renderer</param>
        public static void PlayHitFlash(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null) return;

            // renderer.material getter는 sharedMaterial을 자동 인스턴스화하므로
            // 딱 한 번만 호출하여 Material 객체 누수 방지
            Material instance = renderer.material;
            Color originalColor = instance.color;
            instance.color = Color.white;

            // 임시 GameObject + Update 루프로 복원 처리
            var runner = new GameObject("HitFlash_Runner");
            runner.AddComponent<HitFlashRunner>().Init(instance, originalColor);
        }

        /// <summary>
        /// WorldSpace에 Damage Number (TextMesh 3D)를 생성, 위로 FadeOut 후 Destroy.
        /// </summary>
        /// <param name="position">월드 좌표 위치</param>
        /// <param name="damage">표시할 데미지 값</param>
        public static void SpawnDamageNumber(Vector3 position, float damage)
        {
            GameObject textGO = new GameObject("DamageNumber");
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

    /// <summary>
    /// HitFlash 처리를 위한 내부 MonoBehaviour (Update 기반 복원)
    /// Material 참조를 직접 받아 renderer.material 재접근 방지
    /// </summary>
    internal class HitFlashRunner : MonoBehaviour
    {
        private Material _targetMaterial;
        private Color _originalColor;
        private float _elapsed = 0f;
        private const float DURATION = 0.1f;

        public void Init(Material targetMaterial, Color originalColor)
        {
            _targetMaterial = targetMaterial;
            _originalColor = originalColor;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= DURATION)
            {
                if (_targetMaterial != null)
                    _targetMaterial.color = _originalColor;
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// DamageNumber 애니메이션: 위로 떠오르며 FadeOut
    /// </summary>
    internal class DamageNumberRunner : MonoBehaviour
    {
        private TextMesh _text;
        private float _elapsed = 0f;
        private const float DURATION = 1.0f;
        private const float RISE_SPEED = 1.5f;

        private void Start()
        {
            _text = GetComponent<TextMesh>();
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
            float alpha = Mathf.Lerp(1f, 0f, _elapsed / DURATION);
            Color c = _text.color;
            _text.color = new Color(c.r, c.g, c.b, alpha);

            if (_elapsed >= DURATION)
            {
                Destroy(gameObject);
            }
        }
    }
}