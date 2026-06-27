using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-05: 전투 VFX 컨트롤러 (정적 클래스, 간소화).
    /// 히트플래시, 데미지 폰트, 스파크, 블러드 스플래터.
    /// </summary>
    public static class CombatVFXController
    {
        // ================================================================
        // 1. 히트플래시 — 피격 시 0.1초간 모델 하얗게
        // ================================================================
        public static void PlayHitFlash(GameObject target)
        {
            if (target == null) return;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var cache = new Dictionary<Renderer, Color>(renderers.Length);
            foreach (Renderer r in renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                cache[r] = r.sharedMaterial.color;
                r.sharedMaterial.color = Color.white;
            }

            var go = new GameObject("HitFlashRunner");
            go.AddComponent<HitFlashRunner>().Init(renderers, cache);
        }

        // ================================================================
        // 2. 데미지 폰트 — OnGUI WorldToScreenPoint, 1.5초 Fade Out
        // ================================================================
        public static void ShowDamageNumber(Vector3 worldPos, int damage, Color color)
        {
            var go = new GameObject("DamageNumber");
            go.transform.position = worldPos;
            go.AddComponent<DamageNumberRunner>().Init(damage.ToString(), color);
        }

        // ================================================================
        // 3. 타격 파티클 Sparks — 10개 파티클 버스트
        // ================================================================
        public static void SpawnHitSparks(Vector3 position)
        {
            var go = new GameObject("HitSparks", typeof(ParticleSystem));
            go.transform.position = position;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = Color.yellow;
            ps.Emit(10);
            Object.Destroy(go, 0.6f);
        }

        // ================================================================
        // 4. 블러드 스플래터 — 3개 파티클, 방향 적용
        // ================================================================
        public static void SpawnBloodSplatter(Vector3 position, Vector3 direction)
        {
            var go = new GameObject("BloodSplatter", typeof(ParticleSystem));
            go.transform.position = position;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = Color.red;

            Vector3 vel = direction.normalized * 2.5f;
            var emitParams = new ParticleSystem.EmitParams
            {
                velocity = vel,
                applyShapeToPosition = true
            };
            ps.Emit(emitParams, 3);
            Object.Destroy(go, 1f);
        }

        // ================================================================
        // 내부 Runner: HitFlash 복원
        // ================================================================
        private class HitFlashRunner : MonoBehaviour
        {
            private Renderer[] _renderers;
            private Dictionary<Renderer, Color> _cache;
            private float _elapsed;
            private bool _restored;

            public void Init(Renderer[] renderers, Dictionary<Renderer, Color> cache)
            {
                _renderers = renderers;
                _cache = cache;
                _elapsed = 0f;
                _restored = false;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                if (_elapsed >= 0.1f && !_restored)
                {
                    Restore();
                    _restored = true;
                    Destroy(gameObject);
                }
            }

            private void Restore()
            {
                foreach (Renderer r in _renderers)
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    if (_cache.TryGetValue(r, out Color color))
                        r.sharedMaterial.color = color;
                }
            }

            private void OnDestroy()
            {
                if (!_restored && _renderers != null && _cache != null)
                    Restore();
                _restored = true;
            }
        }

        // ================================================================
        // 내부 Runner: IMGUI 데미지 폰트 (1.5초 Fade Out)
        // ================================================================
        private class DamageNumberRunner : MonoBehaviour
        {
            private string _text;
            private Color _color;
            private float _elapsed;
            private Camera _cam;
            private GUIStyle _style;
            private GUIStyle _shadowStyle;
            private GUIContent _guiContent;

            public void Init(string text, Color color)
            {
                _text = text;
                _color = color;
                _elapsed = 0f;
                _cam = Camera.main;

                _guiContent = new GUIContent(text);

                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };

                _shadowStyle = new GUIStyle(_style)
                {
                    normal = { textColor = new Color(0, 0, 0, 0.5f) }
                };
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                transform.position += Vector3.up * (1.2f * Time.deltaTime);
                if (_elapsed >= 1.5f)
                    Destroy(gameObject);
            }

            private void OnGUI()
            {
                if (_cam == null)
                {
                    _cam = Camera.main;
                    if (_cam == null) return;
                }

                Vector3 screenPos = _cam.WorldToScreenPoint(transform.position);
                if (screenPos.z < 0) return;
                screenPos.y = Screen.height - screenPos.y;

                float alpha = Mathf.Lerp(1f, 0f, _elapsed / 1.5f);
                Color guiColor = new Color(_color.r, _color.g, _color.b, alpha);

                _style.normal.textColor = guiColor;
                _shadowStyle.normal.textColor = new Color(0, 0, 0, alpha * 0.5f);

                Vector2 textSize = _style.CalcSize(_guiContent);
                Rect rect = new Rect(
                    screenPos.x - textSize.x * 0.5f,
                    screenPos.y - textSize.y * 0.5f,
                    textSize.x, textSize.y);

                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), _guiContent, _shadowStyle);
                GUI.Label(rect, _guiContent, _style);
            }
        }
    }
}