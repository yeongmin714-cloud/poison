using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P20-7 + P26 — 우클릭 명령 지점 표시.
    /// - 이동 명령(비공격): **지속형 고품질 지면 원형 링**(MoveTargetRing 베이크 PNG, 골드).
    ///   소유 병사가 목적지 도착/명령 취소(H)/사망할 때까지 잔존, 미세 펄스로 활성감.
    /// - 공격 명령: 기존 페이드형 링(SelectionRing 셰이더 우선, 절차 링 폴백) 1.5s 축소+페이드.
    /// Quad 바닥, 지면 위 0.03 (짚단 데칼 위).
    /// </summary>
    public class CommandMarker : MonoBehaviour
    {
        private float _life = 1.5f;
        private float _elapsed;
        private Vector3 _startScale;
        private Renderer _rend;
        private Color _baseColor = CommandRingPresentation.MoveTargetColor;   // 골드 (팀색)
        private bool _persistent;        // 이동 명령 지속형 여부
        /// <summary>지속형 마커의 소유 병사 — 도착/취소/사망 시 마커 소멸.</summary>
        public GuardPlaceholder owner;

        /// <summary>
        /// 명령 지점 표시. color 미지정 시 골드. owner != null이면 **지속형**(도착/취소/사망까지 잔존),
        /// owner == null이면 기존 1.5s 페이드형(공격 명령용).
        /// </summary>
        public static CommandMarker Spawn(Vector3 position, Color? color = null, GuardPlaceholder owner = null, float? scaleOverride = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CommandMarker";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = new Vector3(position.x, position.y + 0.03f, position.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // [2026-10-11 사용자 요구] 이동 포인트 링 = 현행 절반 — scaleOverride로 지속 마커만 축소.
            go.transform.localScale = Vector3.one * (scaleOverride ?? CommandRingPresentation.BaseScale);
            var marker = go.AddComponent<CommandMarker>();
            if (color.HasValue) marker._baseColor = color.Value;
            marker._persistent = owner != null;
            marker.owner = owner;
            return marker;
        }

        private float _burstElapsed = -1f;   // [15차] 명령 ack 확산 펄스 진행(>=0 활성)

        private void Start()
        {
            _rend = GetComponent<Renderer>();
            // [2026-10-11 사용자 요구 — 검은 사각형 제거] 명령 링 쿼드도 지면 데칼 — 그림자 방출/수신 금지.
            _rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rend.receiveShadows = false;
            if (_persistent) SetupPersistent();
            else SetupFading();
            _startScale = transform.localScale;
            _burstElapsed = 0f;   // [15차] 모든 명령 마커에 1회 ack 확산
        }

        /// <summary>버스트/페이 공통 알파 대입 — 셰이더(_TeamColor)/폴백(_Color) 모두.</summary>
        private void SetMarkerAlpha(float a)
        {
            if (_rend == null) return;
            var m = _rend.sharedMaterial;
            if (m == null) return;
            a = Mathf.Clamp01(a);
            if (m.HasProperty("_TeamColor")) { var c = m.GetColor("_TeamColor"); c.a = a; m.SetColor("_TeamColor", c); }
            else if (m.HasProperty("_Color")) { var c = m.GetColor("_Color"); c.a = a; m.SetColor("_Color", c); }
        }

        /// <summary>지속형 — 베이크 고품질 지면 링 텍스처(MoveTargetRing) + 팀색. 셰이더가 없어도 렌더.</summary>
        private void SetupPersistent()
        {
            _rend = GetComponent<Renderer>();
            CommandRingPresentation.Apply(_rend, _baseColor);
        }

        /// <summary>페이드형 — SelectionRing 셰이더 우선, 없으면 절차 링 텍스처(공격 명령).</summary>
        private void SetupFading()
        {
            var shader = Shader.Find("Custom/SelectionRing");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetColor("_TeamColor", _baseColor);
                mat.SetFloat("_RingRadius", 0.8f);
                mat.SetFloat("_RingThickness", 0.12f);
                mat.SetFloat("_PulseSpeed", 0f);
                mat.SetFloat("_ArcSpeed", 1.5f);
            }
            else
            {
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(unlit != null ? unlit : Shader.Find("Sprites/Default"));
                mat.mainTexture = BuildRingTexture(256);
                mat.color = _baseColor;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            if (_rend != null) _rend.sharedMaterial = mat;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            // [15차 입체감] rim 하이라이트 방향 갱신(버스트/페이 공통).
            CommandRingPresentation.UpdateRimDirection(_rend, _rend != null ? _rend.sharedMaterial : null);

            // [15차] 명령 ack 확산 펄스 — 스폰 순간 스케일 1.30→1.0 + 알파 램프인(0.35s, 1회).
            if (_burstElapsed >= 0f && _burstElapsed < CommandRingPresentation.AckBurstDuration)
            {
                _burstElapsed += Time.deltaTime;
                float burstT = Mathf.Clamp01(_burstElapsed / CommandRingPresentation.AckBurstDuration);
                transform.localScale = _startScale * Mathf.Lerp(CommandRingPresentation.AckBurstStartScale, 1f, burstT);
                float lifeAlpha = _persistent ? 1f : 1f - Mathf.Clamp01(_elapsed / _life);
                SetMarkerAlpha(lifeAlpha * Mathf.Lerp(CommandRingPresentation.AckBurstStartAlpha, 1f, burstT));
                if (_persistent) return;
                if (_elapsed >= _life) { Destroy(gameObject); return; }
                return;
            }

            if (_persistent)
            {
                // 지속형 — 소유 병사가 도착(명령 해제)/취소/사망하면 소멸. 도착 전엔 미세 펄스로 활성감.
                if (owner == null || !owner.IsAlive || !owner.HasCommand)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.localScale = CommandRingPresentation.GetPulseScale(_startScale, _elapsed);
                return;
            }

            // 페이드형 — 1.5s 축소+페이드 후 소멸 (기존 공격 명령).
            float t = Mathf.Clamp01(_elapsed / _life);
            transform.localScale = _startScale * (1f - 0.45f * t);
            if (_rend != null)
            {
                var m = _rend.sharedMaterial;
                if (m.HasProperty("_TeamColor")) { var c = m.GetColor("_TeamColor"); c.a = 1f - t; m.SetColor("_TeamColor", c); }
                else if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = 1f - t; m.SetColor("_BaseColor", c); }
                else if (m.HasProperty("_Color")) { var c = m.GetColor("_Color"); c.a = 1f - t; m.SetColor("_Color", c); }
            }
            if (_elapsed >= _life) Destroy(gameObject);
        }

        /// <summary>절차 환형 링 텍스처 — CommandRingPresentation 셰이더 폴백 경로에서도 재사용(public).</summary>
        public static Texture2D BuildRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f;
            float rOuter = c * 0.9f, rInner = c * 0.68f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = 0f;
                    if (d <= rOuter && d >= rInner) a = 1f;
                    else if (d > rOuter && d < rOuter + 4f) a = 1f - (d - rOuter) / 4f;
                    else if (d < rInner && d > rInner - 4f) a = 1f - (rInner - d) / 4f;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}