using UnityEngine;
using System.Collections.Generic;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-05: 전투 VFX 컨트롤러 (정적 클래스, 간소화).
    /// 히트플래시, 데미지 폰트, 스파크, 블러드 스플래터.
    /// G2-06 HighSpec: 파편(디브리) 버스트, 크리티컬 버스트, 파티클 부스트.
    /// 저사양(Balanced) 경로는 기존 동작 그대로 유지 — 강화는 모두 ActionFeel.HighSpec 게이트.
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

            // G2-06 HighSpec: 파티클 2배(10→20) + 미묘한 상향 바이어스.
            // 저사양(Balanced) 경로는 위 Emit(10) 그대로 유지된다.
            if (ActionFeel.HighSpec)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 vel = Random.insideUnitSphere.normalized * Random.Range(2f, 5f);
                    vel.y = Mathf.Abs(vel.y) * 0.5f + 0.5f; // 상향 바이어스
                    var ep = new ParticleSystem.EmitParams { velocity = vel };
                    ps.Emit(ep, 1);
                }
            }

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

            // G2-06 HighSpec: 파티클 증가(3→8) + 미묘한 상향 바이어스.
            // 저사양(Balanced) 경로는 위 Emit(emitParams, 3) 그대로 유지된다.
            if (ActionFeel.HighSpec)
            {
                for (int i = 0; i < 5; i++)
                {
                    Vector3 boostVel = direction.normalized * Random.Range(0.5f, 2.5f)
                                     + Random.insideUnitSphere * 0.8f;
                    boostVel.y = Mathf.Abs(boostVel.y) * 0.5f + 0.4f; // 상향 바이어스
                    var ep = new ParticleSystem.EmitParams { velocity = boostVel };
                    ps.Emit(ep, 1);
                }
            }

            Object.Destroy(go, 1f);
        }

        // ================================================================
        // 5. 암살 VFX — 붉은 섬광 + 블러드 스플래터
        // ================================================================
        public static void PlayAssassinationVFX(Vector3 position)
        {
            // 붉은 섬광 (ParticleSystem)
            var flashGo = new GameObject("AssassinationFlash", typeof(ParticleSystem));
            flashGo.transform.position = position + Vector3.up * 0.5f;
            var flashPs = flashGo.GetComponent<ParticleSystem>();
            var flashMain = flashPs.main;
            flashMain.startLifetime = 0.3f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            flashMain.startColor = new Color(1f, 0.2f, 0.2f, 0.8f);
            flashPs.Emit(5);
            Object.Destroy(flashGo, 0.4f);

            // 블러드 스플래터 (확산)
            var bloodGo = new GameObject("AssassinationBlood", typeof(ParticleSystem));
            bloodGo.transform.position = position;
            var bloodPs = bloodGo.GetComponent<ParticleSystem>();
            var bloodMain = bloodPs.main;
            bloodMain.startLifetime = 0.6f;
            bloodMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            bloodMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            bloodMain.startColor = Color.red;
            for (int i = 0; i < 3; i++)
            {
                Vector3 vel = Random.insideUnitSphere.normalized * 2.5f;
                vel.y = Mathf.Abs(vel.y) * 0.5f;
                var emitParams = new ParticleSystem.EmitParams { velocity = vel };
                bloodPs.Emit(emitParams, 3);
            }
            Object.Destroy(bloodGo, 0.8f);

            Debug.Log($"[CombatVFXController] Assassination VFX at {position}");
        }

        // ================================================================
        // 6. 파편 조각(디브리) 버스트 — 갈색/회색 샤드 + 밝은 플렉 (HighSpec 전용)
        //    Balanced 모드에서는 즉시 반환 (저사양 스폰 없음, 기존 동작 불변)
        // ================================================================
        public static void SpawnHitDebris(Vector3 position, Vector3 direction, bool isCrit)
        {
            if (!ActionFeel.HighSpec) return; // HighSpec 전용 — 저사양(Balanced)은 스폰하지 않는다

            int count = isCrit ? 14 : 8;

            var go = new GameObject("HitDebris", typeof(ParticleSystem));
            go.transform.position = position;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            // 갈색/회색 파편 — Two-Color 랜덤 (다층 파편 레이어)
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.45f, 0.32f, 0.18f, 1f),   // 갈색
                new Color(0.55f, 0.55f, 0.55f, 1f));  // 회색

            // 파편 샤드: 타격 방향 + 구면 산란, 하향(-Y) 중력 경향
            for (int i = 0; i < count; i++)
            {
                Vector3 vel = direction.normalized * Random.Range(2f, 4f)
                            + Random.insideUnitSphere * Random.Range(2f, 4f);
                vel.y -= Random.Range(1f, 3f);
                var ep = new ParticleSystem.EmitParams { velocity = vel };
                ps.Emit(ep, 1);
            }

            // 밝은 스파크 플렉 2개(크리티컬 3개)를 파편 사이에 섞기
            int flecks = isCrit ? 3 : 2;
            for (int i = 0; i < flecks; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    velocity = Random.insideUnitSphere.normalized * Random.Range(3f, 6f),
                    startColor = new Color(1f, 0.9f, 0.5f, 1f),
                    startSize = Random.Range(0.04f, 0.08f)
                };
                ps.Emit(ep, 1);
            }

            Object.Destroy(go, 1.2f);
        }

        // ================================================================
        // 7. 크리티컬 버스트 — 스파크 링 + 파편 + 스플래시 동시 발사 (HighSpec 전용)
        //    단일 ParticleSystem에 3개 레이어를 EmitParams 오버라이드로 합성.
        // ================================================================
        public static void SpawnCritBurst(Vector3 position)
        {
            if (!ActionFeel.HighSpec) return; // HighSpec 전용 — 저사양(Balanced)은 스폰하지 않는다

            var go = new GameObject("CritBurst", typeof(ParticleSystem));
            go.transform.position = position;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.45f, 0.32f, 0.18f, 1f),   // 갈색 (파편 기본 레이어)
                new Color(0.55f, 0.55f, 0.55f, 1f));  // 회색

            // 레이어 1: 스파크 링 — 수평 원형 16방향으로 퍼지는 밝은 노란 스파크
            const int ringCount = 16;
            for (int i = 0; i < ringCount; i++)
            {
                float angle = (360f / ringCount) * i * Mathf.Deg2Rad + Random.Range(-0.15f, 0.15f);
                Vector3 vel = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(4f, 6f);
                vel.y = Random.Range(0.5f, 1.5f); // 링이 살짝 위로 번지게
                var ep = new ParticleSystem.EmitParams
                {
                    velocity = vel,
                    startColor = Color.yellow,
                    startLifetime = Random.Range(0.4f, 0.6f),
                    startSize = Random.Range(0.05f, 0.12f)
                };
                ps.Emit(ep, 1);
            }

            // 레이어 2: 파편 샤드 — 구면 산란 + 하향(-Y) 중력 경향 (main 갈색/회색 상속)
            for (int i = 0; i < 12; i++)
            {
                Vector3 vel = Random.insideUnitSphere.normalized * Random.Range(3f, 6f);
                vel.y -= Random.Range(1f, 3f);
                var ep = new ParticleSystem.EmitParams
                {
                    velocity = vel,
                    startLifetime = Random.Range(0.6f, 1.0f)
                };
                ps.Emit(ep, 1);
            }

            // 레이어 3: 붉은 스플래시 — 느리고 크게 퍼지는 액체 튀김
            for (int i = 0; i < 8; i++)
            {
                Vector3 vel = Random.insideUnitSphere.normalized * Random.Range(1f, 2.5f);
                vel.y = Mathf.Abs(vel.y) * 0.5f + 0.3f;
                var ep = new ParticleSystem.EmitParams
                {
                    velocity = vel,
                    startColor = Color.red,
                    startLifetime = Random.Range(0.5f, 0.8f),
                    startSize = Random.Range(0.15f, 0.3f)
                };
                ps.Emit(ep, 1);
            }

            Object.Destroy(go, 1.3f);
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