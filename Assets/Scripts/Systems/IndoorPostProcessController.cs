using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-10-10 F4 — 실내 고품질화] 실내 전용 포스트 프로세싱 게이트.
    ///
    /// IndoorAeroFogController와 동일 게이트 규약: 활성 씬 == IndoorScene 또는 씬명 "Interior" 포함
    /// (성 내부 테스트 씬 포함)일 때만 Volume weight 1 + 카메라 post-processing 활성.
    /// 프로필은 런타임 절차 생성(Bloom/Vignette/WhiteBalance/ColorAdjustments) — 에셋 파일 없이
    /// 코드 상수가 진실원(계약 테스트는 소스 기반).
    ///
    /// 톤 상한 계약: 예시(실내씬 예시.png)보다 화사해지지 않게 intensity 상한 고정
    /// (Bloom ≤ 0.35 / Vignette ≤ 0.40 / Saturation ≤ 8 / Contrast ≤ 8 / WhiteBalance ≤ 18).
    /// 카메라 renderPostProcessing은 진입 전 값 기억 → 퇴장 시 복원(메인씬 무변경 계약).
    /// </summary>
    public class IndoorPostProcessController : MonoBehaviour
    {
        private static IndoorPostProcessController _instance;
        private const string IndoorSceneName = "IndoorScene";

        // ===== 톤 상한 계약 — F4 계획(2026-10-10) 확정치 =====
        public const float BloomThreshold = 0.9f;
        public const float BloomIntensity = 0.28f;      // 상한 0.35 — 과채도 방지
        public const float VignetteIntensity = 0.35f;   // 상한 0.40
        public const float WhiteBalanceTemperature = 15f;  // 상한 18 — 웜 쉬프트
        public const float ColorSaturation = 6f;        // 상한 8
        public const float ColorContrast = 8f;          // 상한 8

        private Volume _volume;
        private bool _gateIndoor;
        private readonly Dictionary<Camera, bool> _cameraRestore = new Dictionary<Camera, bool>();
        private float _cameraScanTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("IndoorPostProcessController");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<IndoorPostProcessController>();
        }

        private void Awake()
        {
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.weight = 0f;                    // 게이트 OFF 기본 — 실내 진입 시 1
            _volume.profile = BuildIndoorProfile();
            Debug.Log("[IndoorPostProcess] 볼륨 준비 — Bloom/Vignette/WhiteBalance/ColorAdjustments(실내 게이트)");
        }

        /// <summary>절차 프로필 생성 — 톤 상한 계약 상수 적용.</summary>
        private static VolumeProfile BuildIndoorProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>(true);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(0.45f);

            var whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(WhiteBalanceTemperature);
            whiteBalance.tint.Override(0f);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(ColorSaturation);
            color.contrast.Override(ColorContrast);
            color.postExposure.Override(0f);

            return profile;
        }

        private void Update()
        {
            // [게이트] IndoorAeroFogController와 동일 판정 — IndoorScene 또는 "Interior" 포함.
            string activeScene = SceneManager.GetActiveScene().name;
            bool indoor = activeScene == IndoorSceneName || activeScene.Contains("Interior");
            if (indoor != _gateIndoor)
            {
                _gateIndoor = indoor;
                _volume.weight = indoor ? 1f : 0f;
                Debug.Log($"[IndoorPostProcess] 게이트 전환 — 씬='{activeScene}' 실내={indoor} weight={_volume.weight}");
            }

            // 카메라 post-processing 활성 스캔(스로틀) — 실내일 때만 켜고, 진입 전 값 기억해 퇴장 시 복원.
            _cameraScanTimer -= Time.unscaledDeltaTime;
            if (_cameraScanTimer > 0f) return;
            _cameraScanTimer = 0.5f;

            var cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null) continue;
                var extra = cam.GetUniversalAdditionalCameraData();
                if (extra == null) continue;

                bool known = _cameraRestore.TryGetValue(cam, out bool prev);
                if (indoor)
                {
                    if (!known) _cameraRestore[cam] = extra.renderPostProcessing;
                    if (!extra.renderPostProcessing) extra.renderPostProcessing = true;
                }
                else if (known)
                {
                    // 실내 게이트가 꺼진 순간 — 진입 전 값 복원(메인씬 무변경 계약).
                    if (extra.renderPostProcessing != prev) extra.renderPostProcessing = prev;
                    _cameraRestore.Remove(cam);
                }
            }
        }
    }
}
