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

        [Tooltip("[67차] true = 수명 경과 후 축소 페이드로 자동 소멸. 선택 링 전용 — 공격 타겟 링 등 상시 링은 false 유지.")]
        public bool autoFadeOut = false;
        [Tooltip("표시 유지 시간(초) — autoFadeOut=true일 때만 작동. [68차] 선택 링 1초 규약.")]
        public float lifetimeSeconds = 1f;
        [Tooltip("페이드(축소 소멸) 지속(초). [68차] 0.3s — 링 잔상 최소화.")]
        public float fadeSeconds = 0.3f;

        /// <summary>[67차] 자동 소멸 완료 통지 — 매니저가 aura 추적 정리/재생성 게이트에 사용. 1회만 발화.</summary>
        public event System.Action Expired;

        private Material _mat;
        private Renderer _rend;
        private float _elapsed;
        private Vector3 _startScale;
        private Vector3 _visualScale;
        private bool _fading;
        private float _fadeElapsed;
        private Vector3 _fadeStartScale;
        private bool _expiredNotified;

        /// <summary>Legacy public helper retained for callers outside the active command-ring path.</summary>
        public static Material CreateRingMaterial(Color color) => CommandRingPresentation.CreateMaterial(color);

        /// <summary>Legacy fallback test retained for existing callers.</summary>
        public static bool IsFallback(Material m) => m != null && m.name.Contains("Fallback");

        private float _selectPulseElapsed = -1f;   // [무번호 19-C1] 선택 순간 확산 펄스 진행(>=0 활성)

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
            // [무번호 19-C1] 선택 순간 0.15s 이징 확산 펄스 — 스폰 순간 1회 구동(스케일 1.22→1.0 + 알파 0.35→1.0).
            _selectPulseElapsed = 0f;
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
            UpdateVisual(Time.deltaTime);
            UpdateSelectedBreathing();   // [무번호 30-S4] 선택 유지 호흡(지속 계층 — 19-C1 1회성 펄스와 별개)
        }

        /// <summary>[무번호 30-S4] 선택 유지 호흡 — 링 머티리얼 인스턴스의 _TeamColor 알파를
        /// SpawnPulse(1.4s 주기)로 0.82~1.0× 미세 호흡. 계약: 84차 상수/생성 경로 무수정(인스턴스 전용 기록),
        /// Play 전용(EditMode 테스트 무영향 — isPlaying 가드), 19-C1 블록 무수정, 결정론(Random 0건).</summary>
        private void UpdateSelectedBreathing()
        {
            if (!Application.isPlaying) return;
            if (_mat == null || !_mat.HasProperty("_TeamColor")) return;
            float breathe = 0.82f + 0.18f * FXMotion.SpawnPulse(Mathf.Repeat(Time.time / 1.4f, 1f));
            Color c = color;
            c.a *= breathe;
            _mat.SetColor("_TeamColor", c);
        }

        /// <summary>
        /// [67차] 시각 갱신 — dt 주입형(테스트 가능). 수명 경과 시 축소 페이드 → 0 도달 후
        /// Expired 1회 발화 + 오브젝트 소멸. autoFadeOut=false면 기존 펄스 루프 그대로(무소멸).
        /// </summary>
        public void UpdateVisual(float dt)
        {
            // [무번호 19-C1] 선택 순간 0.15s 이징 확산 펄스 — easeOutCubic 스케일/알파 램프인(1회).
            if (_selectPulseElapsed >= 0f && _selectPulseElapsed < CommandRingPresentation.SelectPulseDuration)
            {
                _selectPulseElapsed += dt;
                float t = Mathf.Clamp01(_selectPulseElapsed / CommandRingPresentation.SelectPulseDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);   // easeOutCubic
                transform.localScale = _startScale * Mathf.Lerp(
                    CommandRingPresentation.SelectPulseStartScale, 1f, eased);
                if (_mat != null && _mat.HasProperty("_BurstT"))
                    _mat.SetFloat("_BurstT", eased);
                if (_selectPulseElapsed >= CommandRingPresentation.SelectPulseDuration)
                {
                    _selectPulseElapsed = -1f;
                    transform.localScale = _startScale;
                    if (_mat != null && _mat.HasProperty("_BurstT"))
                        _mat.SetFloat("_BurstT", -1f);
                }
                _elapsed += dt;
                return;
            }
            // [67차] EditMode(AddComponent Awake 미호출) 폴백 — 시작 스케일 지연 캡처
            if (!_fading && _startScale == Vector3.zero && transform.localScale.sqrMagnitude > 0f)
                _startScale = transform.localScale;

            _elapsed += dt;

            if (_fading)
            {
                _fadeElapsed += dt;
                float t = fadeSeconds > 0f ? Mathf.Clamp01(_fadeElapsed / fadeSeconds) : 1f;
                transform.localScale = Vector3.Lerp(_fadeStartScale, Vector3.zero, t);
                if (t >= 1f && !_expiredNotified)
                {
                    _expiredNotified = true;
                    Expired?.Invoke();
                    // [67차] 파괴는 실행 모드 전용 — 편집 모드(테스트)에서 Destroy는
                    //   "Destroy may not be called from edit mode" 에러 → 테스트 실패.
                    //   편집 모드는 0스케일이 소멸과 동등(Expired 통지가 계약).
                    if (Application.isPlaying) Destroy(gameObject);
                    else transform.localScale = Vector3.zero;
                }
                return;   // 페이드 중엔 펄스/rim 갱신 중단
            }

            if (autoFadeOut && _elapsed >= lifetimeSeconds)
            {
                _fading = true;
                _fadeElapsed = 0f;
                _fadeStartScale = transform.localScale;
                return;
            }

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
