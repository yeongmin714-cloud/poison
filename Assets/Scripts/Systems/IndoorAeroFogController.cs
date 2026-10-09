using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Systems
{
    /// <summary>
    /// 계획 v2 Phase 4 — AERO 볼류메트릭 포그 실내 게이트 컨트롤러.
    ///
    /// [배경] AERO(Mirza) FullScreenPass 볼류메트릭 포그를 UniversalRendererData에
    ///   "AERO Volumetric Fog" feature로 상시 부착하되, 머티리얼(Resources/AeroIndoorFog)의
    ///   _Density 기본값은 0 — 본 컨트롤러가 IndoorScene 활성 시에만 실내 밀도로 페이드 인한다.
    ///   GPU 제약은 사용자 확정으로 해제(메인 실행은 별도 데스크톱, 2026-10-09) — 기존 보류 판정(09-29) 대체.
    ///
    /// [AERO 계약 — 에셋 README/VolumetricFogController 선례]
    ///  - _FrameCount = Time.renderedFrameCount % 60 — 매 프레임 갱신 필수(IGN 지터 안정화).
    ///  - _AdditionalLightCount = 비-directional 조명 수(캡) — 0.5s 스로틀 스캔.
    ///  - _AmbientLighting = RenderSettings.ambientLight × ambientIntensity.
    ///  - _Density는 MoveTowards 페이드 — 실내 진입/퇴출 전환 부드럽게, 야외·테스트씬은 0 유지.
    ///
    /// [격리] 가스 전용 opt-in 격리 세션 파이프라인(Rendering 폴더의 가스 렌더링 계열)과 무관 — 본 파일은
    ///   그 경로를 전혀 건드리지 않는다(별도 경로, 가스는 베이크 스프라이트 GasCloudField 유지).
    /// </summary>
    public class IndoorAeroFogController : MonoBehaviour
    {
        public const string IndoorSceneName = "IndoorScene";
        public const float IndoorDensity = 0.35f;      // 실내 웜 헤이즈 목표 밀도(캡처 피드백으로 조정)
        public const float FadeSpeed = 1.2f;           // _Density 단위/초
        public const int MaxAdditionalLights = 8;      // 포그 광구 상한 캡(URP per-object limit 4와 무관한 셰이더 루프 캡)
        private const float LightScanInterval = 0.5f;

        private static IndoorAeroFogController _instance;
        private Material _material;
        private float _lightScanTimer;
        private int _additionalLightCount;
        private Color _ambient;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        /// <summary>멱등 부트스트랩 — HUDUTK/HotbarUIUTK Ensure 스타일.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("IndoorAeroFogController");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<IndoorAeroFogController>();
            _instance._material = Resources.Load<Material>("AeroIndoorFog");
            if (_instance._material == null)
            {
                Debug.LogWarning("[IndoorAeroFog] Resources/AeroIndoorFog 머티리얼 없음 — 포그 비활성(레일 안전 폴백)");
                return;
            }
            // 부팅 안전 리셋 — 야외/테스트씬에서 새지 않게 항상 0에서 시작.
            _instance._material.SetFloat("_Density", 0f);
        }

        private void Update()
        {
            if (_material == null) return;

            // [게이트] 활성 씬이 IndoorScene일 때만 밀도 > 0. 애디티브 실내 진입(IndoorSceneTransition)과
            //   언로드 복귀 양쪽에서 activeScene 기준으로 자동 전환된다.
            bool indoor = SceneManager.GetActiveScene().name == IndoorSceneName;
            float target = indoor ? IndoorDensity : 0f;
            float current = _material.HasProperty("_Density") ? _material.GetFloat("_Density") : 0f;
            if (!Mathf.Approximately(current, target))
            {
                float next = Mathf.MoveTowards(current, target, FadeSpeed * Time.unscaledDeltaTime);
                _material.SetFloat("_Density", next);
            }

            // AERO 계약 — IGN 지터 안정화 프레임 카운터는 매 프레임.
            if (_material.HasProperty("_FrameCount"))
                _material.SetInteger("_FrameCount", Time.renderedFrameCount % 60);

            // 조명 스캔 스로틀 — 실내 조명 수는 진입 직후 변동이 거의 없다.
            _lightScanTimer -= Time.unscaledDeltaTime;
            if (_lightScanTimer <= 0f)
            {
                _lightScanTimer = LightScanInterval;
                ScanLights();
            }
            if (_material.HasProperty("_AdditionalLightCount"))
                _material.SetInteger("_AdditionalLightCount", _additionalLightCount);
            if (_material.HasProperty("_AmbientLighting"))
                _material.SetColor("_AmbientLighting", _ambient);
        }

        private void ScanLights()
        {
            _ambient = RenderSettings.ambientLight * RenderSettings.ambientIntensity;

            int count = 0;
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Directional) count++;
                if (count >= MaxAdditionalLights) break;
            }
            _additionalLightCount = Mathf.Min(count, MaxAdditionalLights);
        }
    }
}