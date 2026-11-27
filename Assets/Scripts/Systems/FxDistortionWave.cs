using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 71-L2] 디스토션 히트 웨이브 — 히트 지점 확장 링 쿼드(씬 색 리프랙션 — 물결 룩).
    ///  - 셰이더: ProjectName/FXDistortionRing(신규 — _CameraOpaqueTexture 방사 UV 오프셋 + 깊이 페이드
    ///    FXBandingRing 수식 승계). Assets/Shaders/FXDistortionRing.shader.
    ///  - 런타임 가드(필수): URP 자산의 opaque 텍스처 지원을 리플렉션 확인(supportsCameraOpaqueTexture —
    ///    구 URP 명명 supportsOpaqueTexture 순차 폴백, try/catch). 미지원/판정 실패 = 조용 스킵
    ///    (씬 색 부재 환경의 검은 링 결함 원천 차단 — 66~69 블랙뿌리 선례 fail-closed).
    ///  - 확장 0.5→1.6m × scale / 0.4s / FXMotion.Progress 이징(28-U3 진행류 표준), 동시 상한 4 예산 가드
    ///    (FxImpactLight 상한 3 선례), unscaled 시간, EditMode 무동작 가드, 결정론 — Random 0건.
    ///  - 폴백 계약: 셰이더 부재 시 null 폴백 무음 스킵(로직 무영향 — FxMaterialLibrary 폴백 패턴 승계).
    /// </summary>
    public static class FxDistortionWave
    {
        public const string ShaderName = "ProjectName/FXDistortionRing";

        /// <summary>[71-L2] 동시 웨이브 예산 — 초과 시 조용 스킵(FxImpactLight.MaxConcurrentLights 선례).</summary>
        public const int MaxConcurrentWaves = 4;

        /// <summary>[71-L2] 수명 — 0.4s(FXMotion.ShortFxMinSec 정합), 확장 0.5→1.6m × scale(계획서 스펙).</summary>
        public const float LifetimeSec = 0.4f;
        public const float StartDiameter = 0.5f;
        public const float EndDiameter = 1.6f;

        private static int _activeCount;
        private static Shader _shader;
        private static Mesh _quadMesh;
        private static bool _opaqueResolved;
        private static bool _opaqueSupported;

        /// <summary>[무번호 71-L2] 히트 지점 디스토션 웨이브 발화 — 크리 1.5배 위계(isCrit ? 1.5f : 1f).</summary>
        public static void Burst(Vector3 position, float scale)
        {
            if (!Application.isPlaying) return;                  // EditMode 무동작 가드
            if (_activeCount >= MaxConcurrentWaves) return;      // 예산 가드 — 초과 조용 스킵
            if (!OpaqueTextureSupported()) return;               // 런타임 리플렉션 가드 — 미지원 조용 스킵(필수)
            try
            {
                EnsureAssets();
                if (_shader == null || _quadMesh == null) return;   // 셰이더 부재 null 폴백 — 무음 스킵(로직 무영향)
                var go = new GameObject("FxDistortionWave") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.position = position + Vector3.up * 0.4f;   // 히트 스파이크 클러스터 동일 오프셋(FXGate +0.4 선례)
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);  // 수평 링 — 링 패밀리 월드 Y 축 확산(SpawnCore 선례)
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = _quadMesh;
                var renderer = go.AddComponent<MeshRenderer>();
                var mat = new Material(_shader) { name = "FxDistortionWaveMat" };
                // [71-L2 갱신 79-R20] 발광 틴트 — FXPalette.AttackPrimary 단일 소스(공격 이펙트 통일색, 크레스트에만 미광).
                if (mat.HasProperty("_EmissionColor"))
                {
                    var uni = FXPalette.AttackPrimary;
                    mat.SetColor("_EmissionColor", new Color(uni.r * 0.35f, uni.g * 0.3f, uni.b * 0.25f, 0f));
                }
                if (mat.HasProperty("_DepthFadeDist")) mat.SetFloat("_DepthFadeDist", 0.12f);   // FXBandingRing 기본값 승계
                renderer.sharedMaterial = mat;
                _activeCount++;
                go.AddComponent<DistortionWaveRunner>().Init(mat, Mathf.Max(0.01f, scale));
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 흐름 절대 방해 금지(CombatFXGate 격리 선례)
        }

        /// <summary>[71-L2] 자산 캐시 — 셰이더 1회 Find + 쿼드 메시 1회 추출(ShockwaveRingFX 실린더 추출 선례).</summary>
        private static void EnsureAssets()
        {
            if (_shader == null) _shader = Shader.Find(ShaderName);
            if (_quadMesh == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _quadMesh = temp.GetComponent<MeshFilter>().sharedMesh;   // 빌트인 쿼드 메시(GO 파괴와 무관 — 공유 안전)
                if (Application.isPlaying) Object.Destroy(temp);
                else Object.DestroyImmediate(temp);
            }
        }

        /// <summary>[71-L2] opaque 텍스처 지원 리플렉션 판정(1회 캐시). URP 미탑재/속성 부재/예외 = 미지원(fail-closed).</summary>
        private static bool OpaqueTextureSupported()
        {
            if (_opaqueResolved) return _opaqueSupported;
            _opaqueResolved = true;
            try
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                if (rp == null) { _opaqueSupported = false; return false; }   // 빌트인 RP — _CameraOpaqueTexture 미제공
                var type = rp.GetType();
                // URP(6000.x) = supportsCameraOpaqueTexture / 구버전 URP = supportsOpaqueTexture 순차 확인.
                var prop = type.GetProperty("supportsCameraOpaqueTexture")
                           ?? type.GetProperty("supportsOpaqueTexture");
                if (prop == null || prop.PropertyType != typeof(bool)) { _opaqueSupported = false; return false; }
                _opaqueSupported = (bool)prop.GetValue(rp, null);
            }
            catch (System.Exception) { _opaqueSupported = false; }   // 판정 실패 = 미지원 취급(블랙 링 차단)
            return _opaqueSupported;
        }

        /// <summary>[71-L2] 예산 감산 — 러너 OnDestroy 단일 경로(ImpactLightRunner 선례 — 이중 감산 방지).</summary>
        private static void Release() { _activeCount = Mathf.Max(0, _activeCount - 1); }

        /// <summary>[71-L2] 웨이브 러너 — 0.4s 확장(0.5→1.6m × scale, FXMotion.Progress) + 알파 램프다운 소멸.</summary>
        sealed class DistortionWaveRunner : MonoBehaviour
        {
            private Material _mat;
            private float _scale;
            private float _t;

            public void Init(Material mat, float scale)
            {
                _mat = mat;
                _scale = scale;
                _t = 0f;
                Apply(0f);
            }

            private void Update()
            {
                _t += Time.unscaledDeltaTime;   // unscaled — 히트스톱 중에도 재생(FxImpactLight 선례)
                if (_t >= LifetimeSec) { Destroy(gameObject); return; }
                Apply(_t / LifetimeSec);
            }

            private void Apply(float tRaw)
            {
                float p = FXMotion.Progress(tRaw);   // [28-U3] 진행류 표준 이징(smoothstep + 임박 가속)
                float d = Mathf.Lerp(StartDiameter, EndDiameter * _scale, p);
                // 쿼드 XY 로컬 — 수평 회전 상태에서 world XZ 확장.
                transform.localScale = new Vector3(d, d, 1f);
                if (_mat != null && _mat.HasProperty("_Color"))
                    _mat.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(tRaw));   // 램프다운 소멸(24-FA1 선례 — 소멸 팝 금지)
            }

            private void OnDestroy()
            {
                FxDistortionWave.Release();   // 예산 감산 — 단일 경로
                if (_mat != null)
                {
                    // 누출 금지 — 머티리얼 인스턴스 동반 파괴(VignettePulse OnDestroy 선례).
                    if (Application.isPlaying) Destroy(_mat);
                    else DestroyImmediate(_mat);
                }
                _mat = null;
            }
        }
    }
}
