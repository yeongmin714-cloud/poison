using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 23-V] 화면 계층 판정 피드백 — 카메라 자식 풀스크린 비네트 오버레이(절차 텍스처, 포스트 볼륨 무접촉).
    ///  - V1 피격 적색 비네트 펄스: PlayerHealth.OnPlayerDamaged 이벤트 구독(Core 이벤트 — Systems→Core 허용 방향,
    ///    Core 무수정 계약). RuntimeInitializeOnLoadMethod 부트스트랩.
    ///  - V2 패링 PERFECT 골드 비네트: PlayerCombat 경유 Begin 호출.
    ///  - W1/W3 패러독스 시각 언어: ParadoxTimeStopRunner가 BeginManual/Release로 진입/해제 제어.
    ///  - unscaled 시간 기준(히트스톱 중에도 재생 — ParryFlashFader 선례). EditMode 무동작 가드.
    /// </summary>
    public static class ScreenVignetteFX
    {
        // [무번호 69-R7] 정적 공유 비네트 텍스처 폐기 — 세션 샘플러 별칭 근원 제거.
        //   근거: ①R3 실측 — 사용자 세션에서 공유 런타임 텍스처가 직전 업로드 텍스처(슬래시 아크
        //   웜화이트 아틀라스 255,244,214)로 별칭되는 사례 확정 ②R6 스코치 킬스위치 후에도 사용자
        //   세션 베이지 잔존(13:36 세션, [69-R6] 차단 로그 3회 발화 실측) → 스코치 무죄, 잔여 공유
        //   텍스처 보유자 중 카메라 자식 풀스크린 쿼드 = 본 클래스(R1 바이섹션 "카메라 자식 제외"
        //   규칙의 맹점 — 영원히 지목 불가였던 유일 계열). 별칭 발생 시 아틀라스 웜화이트 샘플 ×
        //   적색 틴트(0.9,0.12,0.1)×α≈0.22 합성 = (249,196,172) ≈ 사용자 세션 실측 베이지
        //   (249,201,173) 정확 합치. 실측 박스 120×88px ≈ 960×540 풀스크린 위 8×6 플립북 아틀라스
        //   셀(120×90) 크기와도 일치. → 펄스마다 신선 텍스처 생성+Apply, 펄스 종료 시 텍스처+머티리얼
        //   동반 파괴(R3 "누출 금지" 계약 승계 + 기존 펄스별 new Material 미파괴 누출 동시 수리).
        private static bool _damageHooked;
        private static int _pulseLogCount;   // [무번호 69-R7] 펄스 로그 상한(스팸 방지, 6회 후 침묵)

        /// <summary>[무번호 23-V] 절차 비네트 텍스처 256×256 — 중심 살짝 투과(전체 톤 캐스트 바닥 0.12) +
        ///   가장자리 smoothstep 강조(0.55~1.05) — 색은 머티리얼 color 틴트로(용도별 청록/적색/금색).</summary>
        private static Texture2D CreateVignetteTextureFresh()
        {
            // [무번호 69-R7] 공유 정적 캐시(1회 생성 영구 재사용) 폐기 — 호출마다 신선 생성.
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.name = "ScreenVignetteTex_Fresh";
            var px = new Color[S * S];
            float c = S * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;   // 0(중심)..~1.41(모서리)
                    float edge = Mathf.SmoothStep(0.55f, 1.05f, r);
                    float a = Mathf.Max(edge, 0.12f);              // 전체 톤 캐스트 바닥 + 가장자리 강조
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>자동 펄스 — fadeIn → hold → fadeOut 후 자가 소멸(피격/판정 피드백용).</summary>
        public static VignettePulse Begin(Camera cam, Color color, float peakAlpha, float fadeIn, float hold, float fadeOut)
        {
            if (!Application.isPlaying || cam == null) return null;   // EditMode 무동작 가드
            try
            {
                var pulse = CreateQuad(cam, color);
                if (pulse == null) return null;
                pulse.RunAuto(color, peakAlpha, fadeIn, hold, fadeOut);
                return pulse;
            }
            catch (System.Exception) { return null; }
        }

        /// <summary>수동 제어 — 진입 페이드만 수행, Release() 때 페이드아웃(시간 정지 진입/해제용).</summary>
        public static VignettePulse BeginManual(Camera cam, Color color, float peakAlpha, float fadeIn)
        {
            if (!Application.isPlaying || cam == null) return null;
            try
            {
                var pulse = CreateQuad(cam, color);
                if (pulse == null) return null;
                pulse.RunManual(color, peakAlpha, fadeIn);
                return pulse;
            }
            catch (System.Exception) { return null; }
        }

        private static VignettePulse CreateQuad(Camera cam, Color color)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col = quad.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            quad.name = "ScreenVignettePulse";
            quad.transform.SetParent(cam.transform, false);
            // 프러스텀 피팅 — 화이트 프레임 2.4배 선례(ParryWhiteFrame 동일 산식)
            float halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.5f;
            float halfW = halfH * cam.aspect;
            quad.transform.localScale = new Vector3(halfW * 2.4f, halfH * 2.4f, 1f);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            quad.transform.localRotation = Quaternion.identity;
            var mr = quad.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            var freshTex = CreateVignetteTextureFresh();   // [69-R7] 펄스별 신선 생성(공유 정적 폐기)
            mat.mainTexture = freshTex;
            mat.color = new Color(color.r, color.g, color.b, 0f);   // 알파 0 시작
            mr.sharedMaterial = mat;
            if (_pulseLogCount < 6)
            {
                // [69-R7] 펄스별 1급 로그 — 사용자 세션 상관 데이터(베이지 목격 시각 ↔ 펄스 시각 대조).
                Debug.Log($"[69-R7] 비네트 펄스 color=({color.r:0.00},{color.g:0.00},{color.b:0.00}) texID={freshTex.GetInstanceID()} (신선 텍스처 — 별칭 차단)");
                _pulseLogCount++;
            }
            var pulse = quad.AddComponent<VignettePulse>();
            pulse.BindFreshTexture(freshTex);   // [69-R7] 펄스 종료 시 동반 파괴 대상 등록
            return pulse;
        }

        /// <summary>[무번호 23-V1] 피격 비네트 부트스트랩 — Core 이벤트 구독(Systems→Core 허용 방향,
        ///   Core 무수정 계약). 중복 구독 방지 정적 플래그.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapDamageVignette()
        {
            if (_damageHooked) return;
            _damageHooked = true;
            ProjectName.Core.PlayerHealth.OnPlayerDamaged += OnPlayerDamagedVignette;
        }

        private static void OnPlayerDamagedVignette(Vector3 hitPos, float amount)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return;
                float k = Mathf.Clamp01(amount / 20f);   // 피해량 비례 강도(상한 클램프)
                Begin(cam, new Color(0.9f, 0.12f, 0.1f), 0.32f + 0.25f * k, 0.04f, 0.1f, 0.22f);
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 흐름 절대 방해 금지
        }
    }

    /// <summary>[무번호 23-V] 비네트 펄스 Runner — unscaled 시간, 진입 이징(easeOut), 수동/자동 해제.</summary>
    public class VignettePulse : MonoBehaviour
    {
        private Material _mat;
        private Texture2D _tex;   // [무번호 69-R7] 펄스별 신선 텍스처(종료 시 동반 파괴)
        private Color _color;
        private float _peak;
        private float _fadeIn;
        private float _hold;
        private float _fadeOut;
        private bool _manual;
        private bool _released;
        private float _elapsed;
        private float _holdElapsed;
        private bool _fadingOut;

        public void RunAuto(Color color, float peak, float fadeIn, float hold, float fadeOut)
        {
            _color = color; _peak = Mathf.Clamp01(peak);
            _fadeIn = Mathf.Max(0.01f, fadeIn); _hold = Mathf.Max(0f, hold);
            _fadeOut = Mathf.Max(0.05f, fadeOut);
            _manual = false;
            CaptureMaterial();
        }

        public void RunManual(Color color, float peak, float fadeIn)
        {
            _color = color; _peak = Mathf.Clamp01(peak);
            _fadeIn = Mathf.Max(0.01f, fadeIn);
            _fadeOut = 0.2f;   // W3 해제 릴
            _manual = true;
            CaptureMaterial();
        }

        /// <summary>[무번호 23-W3] 해제 요청 — 유지 구간을 끝내고 릴아웃 시작(ParadoxTimeStopRunner 호출).</summary>
        public void Release() { _released = true; }

        /// <summary>강제 종료(러너 소멸 등) — 페이드 생략 즉시 정리.</summary>
        public void ForceEnd()
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void CaptureMaterial()
        {
            var mr = GetComponent<MeshRenderer>();
            _mat = mr != null ? mr.sharedMaterial : null;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            float a;
            if (!_fadingOut)
            {
                // 진입 — easeOut 이징(0→peak)
                float t = Mathf.Clamp01(_elapsed / _fadeIn);
                a = _peak * (1f - (1f - t) * (1f - t));
                if (_manual)
                {
                    if (_released) _fadingOut = true;
                }
                else if (_elapsed >= _fadeIn)
                {
                    _holdElapsed += Time.unscaledDeltaTime;
                    a = _peak;
                    if (_holdElapsed >= _hold) _fadingOut = true;
                }
            }
            else
            {
                _holdElapsed += Time.unscaledDeltaTime;
                a = _peak * (1f - Mathf.Clamp01(_holdElapsed / _fadeOut));
                if (_holdElapsed >= _fadeOut) { Destroy(gameObject); return; }
            }
            if (_mat != null)
                _mat.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(a));
        }

        /// <summary>[무번호 69-R7] 신선 텍스처 등록 — OnDestroy에서 머티리얼과 함께 파괴.</summary>
        public void BindFreshTexture(Texture2D tex) { _tex = tex; }

        private void OnDestroy()
        {
            // [무번호 69-R7] 펄스 종료 시 신선 텍스처+머티리얼 동반 파괴 — 누출 금지(R3 계약 승계).
            //   기존 결함: 펄스마다 new Material 생성 후 미파괴(누출) — 동시 수리.
            if (Application.isPlaying)
            {
                if (_mat != null) Destroy(_mat);
                if (_tex != null) Destroy(_tex);
            }
            else
            {
                if (_mat != null) DestroyImmediate(_mat);
                if (_tex != null) DestroyImmediate(_tex);
            }
            _mat = null;
            _tex = null;
        }
    }
}
