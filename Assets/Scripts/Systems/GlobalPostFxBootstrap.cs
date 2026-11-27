using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Systems
{
    /// <summary>
    /// [174차 P0] 메인 씬 전역 포스트프로세싱 부트스트랩.
    ///
    /// 실측 배경: 실내 전용(IndoorPostProcessController)만 post ON이었고 메인 씬 카메라는
    /// post OFF → 글로벌 볼륨(MoodProfileSetup 채도+12/콘트라스트+5)이 화면 미반영 상태.
    /// 본 부트스트랩이 ①전 카메라 post ON(RT 카메라 제외 — 아이콘 베이커/미니맵 보호)
    /// ②글로벌 볼륨 보장 + ACES 톤매핑 주입 ③MoodProfileSetup CA 상수 재사용.
    ///
    /// 화이트아웃 계약(39/40 사고 재발 방지) — postExposure 불변 원칙 유지:
    /// 톤매핑은 노출을 건드리지 않는 별도 단계이며, ColorAdjustments는
    /// MoodProfileSetup과 동일 상수(채도+12/콘트라스트+5)로만 존재.
    ///
    /// IndoorPostProcessController 정합: 진입 전 값 기억/복원 로직은 그대로 —
    /// 본 부트스트랩이 기본값을 ON으로 만들므로 실내 퇴장 시 ON으로 복원(전역 정책 일관).
    /// </summary>
    public class GlobalPostFxBootstrap : MonoBehaviour
    {
        public const float CameraScanInterval = 2f;          // 신규 카메라 재스캔 주기
        public const float TonemappingDefaultPriority = 0f;  // 글로벌 볼륨 우선순위 — 실내 게이트(priority 10)가 이김

        private static GlobalPostFxBootstrap _instance;
        private readonly HashSet<int> _postEnabledCameraIds = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("GlobalPostFxBootstrap");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GlobalPostFxBootstrap>();
        }

        private void Awake()
        {
            EnsureGlobalVolume();
            ScanCameras();
            InvokeRepeating(nameof(ScanCameras), CameraScanInterval, CameraScanInterval);
        }

        /// <summary>① 전 카메라 post ON — RT 카메라(아이콘 베이커·미니맵 등)는 제외.</summary>
        private void ScanCameras()
        {
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || cam.targetTexture != null) continue; // RT 렌더 카메라 제외
                var extra = cam.GetUniversalAdditionalCameraData();
                if (extra == null || extra.renderPostProcessing) continue;
                extra.renderPostProcessing = true;
                _postEnabledCameraIds.Add(cam.GetInstanceID());
                Debug.Log($"[GlobalPostFx] 카메라 post ON: {cam.name}");
            }
        }

        /// <summary>② 글로벌 볼륨 보장 + ③ ACES 톤매핑·CA 주입 (MoodProfileSetup 상수 재사용 — 멱등).</summary>
        private void EnsureGlobalVolume()
        {
            var vol = FindAnyObjectByType<Volume>();
            bool created = false;
            if (vol == null)
            {
                var go = new GameObject("Global Volume (FieldPostFx)");
                vol = go.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = TonemappingDefaultPriority;
                vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                created = true;
            }

            // profile getter가 공유 프로필을 클론으로 돌려준다(원본 에셋 불변 — MoodProfileSetup 선례).
            VolumeProfile profile = vol.profile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                vol.profile = profile;
                created = true;
            }

            if (!profile.TryGet(out Tonemapping tm))
            {
                tm = profile.Add<Tonemapping>(overrides: true);
                Debug.Log("[GlobalPostFx] Tonemapping 오버라이드 추가");
            }
            tm.mode.overrideState = true;
            tm.mode.value = TonemappingMode.ACES;
            // ACES는 감도(Strength) 기본 1 유지 — 톤 롤오프만(노출 무관).

            if (!profile.TryGet(out ColorAdjustments ca))
            {
                ca = profile.Add<ColorAdjustments>(overrides: true);
            }
            // MoodProfileSetup 계약 상수 재사용(드리프트 방지) — 동일 값이므로 재적용 멱등.
            ca.saturation.overrideState = true;
            ca.saturation.value = MoodProfileSetup.SaturationBoost;
            ca.contrast.overrideState = true;
            ca.contrast.value = MoodProfileSetup.ContrastBoost;
            ca.postExposure.overrideState = false; // 노출 불변 — 화이트아웃 방지 계약
            ca.hueShift.overrideState = false;
            ca.colorFilter.overrideState = false;

            Debug.Log($"[GlobalPostFx] ✅ 글로벌 볼륨 {(created ? "신설" : "기존 사용")} — ACES 톤매핑 + 채도+{MoodProfileSetup.SaturationBoost}/콘트라스트+{MoodProfileSetup.ContrastBoost}");
        }
    }
}
