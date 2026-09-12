using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P6 액션감 1차: 히트스톱(타격 프레임 정지) + FOV 펀치 매니저.
    /// static 클래스는 코루틴/Update를 가질 수 없으므로 내부 숨은 호스트
    /// MonoBehaviour(HitStopHost)가 Update 폴링 드라이버 역할을 한다.
    /// (선례: SlashVFXRunner의 SlashFxHost, HitVFX의 HitFlashRunner 패턴)
    ///
    /// 복귀 보장 설계 (timeScale 복귀 실패 → 게임 영구 정지 사고 방지):
    ///  - try-finally 대신 호스트 Update 폴링으로 복귀. 기준 시계는 Time.realtimeSinceStartup
    ///    (실시간)이므로 timeScale이 0에 가까워도 복귀 예정 시각에 반드시 도달한다.
    ///  - 복귀 대상 timeScale은 "요청 시점 값"(평시 1.0). 킬 슬로우모션 등 타 효과가
    ///    낮춰둔 값이면 그 값으로 복귀해 타 시스템의 복귀 lerp를 망가뜨리지 않는다.
    ///  - 진행 중 재요청은 무시(흡수) + 쿨다운 0.15s → 연타 히트스톱 스팸 가드 내장.
    ///  - Time.timeScale이 이미 0.05 이하(일시정지/암살 프리즈 등)면 요청 자체를 무시.
    ///  - 어플리케이션 일시정지(OnApplicationPause)·호스트 파괴 시 즉시 강제 복귀.
    /// </summary>
    public static class HitStopManager
    {
        private const float RequestCooldown = 0.15f;    // 연타 쿨다운 (실시간 초)
        private const float FrozenScaleGuard = 0.05f;   // 이하 timeScale은 '타 시스템 정지'로 간주
        private const float FovPunchDegrees = 1.5f;     // FOV 순간 펀치량 (도)
        private const float FovPunchRecover = 0.15f;    // FOV 복귀 시간 (실시간 초)

        private static HitStopHost _host;
        private static bool _active;                    // 히트스톱 진행 중
        private static float _restoreAtRealtime;        // 복귀 예정 시각 (Time.realtimeSinceStartup 기준)
        private static float _lastRequestRealtime = float.NegativeInfinity;
        private static float _restoreScale = 1f;        // 복귀 대상 timeScale (요청 시점 값, 평시 1.0)

        // ── FOV 펀치 상태 ──
        private static bool _fovPunchActive;
        private static Camera _fovCamera;               // 펀치 시작 시 캡처한 카메라 (파괴/교체 대비)
        private static float _fovBase = 60f;            // 펀치 전 원본 FOV (복귀 기준)
        private static float _fovPunchStartRealtime;

        /// <summary>
        /// 타격 프레임 정지 요청: Time.timeScale = scale → 실시간 duration 후 원복.
        /// 적중 성공 시점(PlayerCombat.AttackTarget)에서 호출. 스팸 가드 내장:
        /// 진행 중 요청과 쿨다운(0.15s) 내 요청은 조용히 흡수(무시)된다.
        /// </summary>
        public static void RequestHitStop(float duration = 0.05f, float scale = 0.08f)
        {
            if (!Application.isPlaying) return;                          // 에디터 편집 모드 가드

            float now = Time.realtimeSinceStartup;
            if (_active) return;                                         // 중복 요청 흡수: 진행 중 무시
            if (now - _lastRequestRealtime < RequestCooldown) return;    // 쿨다운 내 무시
            if (Time.timeScale <= FrozenScaleGuard) return;              // 이미 정지 상태면 건드리지 않음

            duration = Mathf.Clamp(duration, 0.01f, 0.5f);
            scale = Mathf.Clamp(scale, 0.01f, 0.9f);

            EnsureHost();
            if (_host == null) return; // 호스트 생성 실패: 복귀 보장 불가 → timeScale을 건드리지 않는 편이 안전

            // 복귀 대상 보존: 평시 1.0. CCE 히트스톱(0.5) 등 타 효과 진행 중이면 그 값으로 복귀.
            _restoreScale = Time.timeScale;
            Time.timeScale = scale;
            _restoreAtRealtime = now + duration;
            _lastRequestRealtime = now;
            _active = true;

            StartFovPunch(now);
        }

        /// <summary>
        /// 호스트 Update에서 호출되는 복귀 폴링. 모든 복구는 실시간(realtimeSinceStartup) 기반.
        /// 이 메서드가 유일한 복귀 경로 — 예외가 나도 다음 프레임 Update에서 재시도된다.
        /// </summary>
        internal static void Tick()
        {
            float now = Time.realtimeSinceStartup;

            // 1) timeScale 복귀 (실시간 폴링 — timeScale=0 중에도 반드시 실행된다)
            if (_active && now >= _restoreAtRealtime)
            {
                // 타 시스템이 우리 창 도중 '완전 정지(0)'로 바꿨다면 그 소유자가 복구하도록 놔둔다
                // (일시정지/암살 프리즈를 1.0으로 깨는 사고 방지)
                if (Time.timeScale > 0.0001f)
                    Time.timeScale = _restoreScale;
                _active = false;
            }

            // 2) FOV 펀치 복귀 (0.15s lerp, 실시간)
            if (_fovPunchActive)
            {
                if (_fovCamera == null)
                {
                    // 카메라 파괴/씬 전환 → 복구 대상 없음, 조용히 종료 (null 가드)
                    _fovPunchActive = false;
                }
                else
                {
                    float t = Mathf.Clamp01((now - _fovPunchStartRealtime) / FovPunchRecover);
                    _fovCamera.fieldOfView = Mathf.Lerp(_fovBase - FovPunchDegrees, _fovBase, t);
                    if (t >= 1f)
                        _fovPunchActive = false;
                }
            }
        }

        /// <summary>일시정지/호스트 파괴 등 비상 복귀 — 진행 중 히트스톱과 FOV 펀치를 즉시 원복.</summary>
        internal static void ForceRestore()
        {
            if (_active && Time.timeScale > 0.0001f)
                Time.timeScale = _restoreScale;
            _active = false;

            if (_fovPunchActive)
            {
                if (_fovCamera != null)
                    _fovCamera.fieldOfView = _fovBase;
                _fovPunchActive = false;
                _fovCamera = null;
            }
        }

        /// <summary>호스트가 파괴되었을 때 정적 참조 정리 + 안전 복귀.</summary>
        internal static void NotifyHostDestroyed()
        {
            ForceRestore();
            _host = null;
        }

        /// <summary>
        /// FOV 펀치 시작: Camera.main FOV를 -1.5° 순간 수축 후 0.15초 lerp 복귀.
        /// 카메라 null 시 펀치 자체를 생략(가드) — 복구 드라이버 없는 상태를 남기지 않는다.
        /// </summary>
        private static void StartFovPunch(float now)
        {
            if (_fovPunchActive)
            {
                // 진행 중 펀치 재진입: base를 유지해 중복 감쇠로 FOV가 누전하는 것을 방지, 타이머만 리셋
                if (_fovCamera != null)
                    _fovCamera.fieldOfView = _fovBase - FovPunchDegrees;
                _fovPunchStartRealtime = now;
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return; // 카메라 null 가드

            _fovCamera = cam;
            _fovBase = cam.fieldOfView;
            cam.fieldOfView = _fovBase - FovPunchDegrees;
            _fovPunchStartRealtime = now;
            _fovPunchActive = true;
        }

        /// <summary>숨은 호스트 지연 생성 (SlashFxHost 선례: 비활성 생성 → 활성화로 Awake 타이밍 이슈 회피).</summary>
        private static void EnsureHost()
        {
            if (_host != null) return;

            var hostGo = new GameObject("HitStopHost")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            hostGo.SetActive(false);
            _host = hostGo.AddComponent<HitStopHost>();
            hostGo.SetActive(true);
        }
    }

    /// <summary>
    /// HitStopManager의 내부 숨은 호스트 — static 클래스의 Update 드라이버 컨테이너.
    /// 실제 상태는 모두 HitStopManager 정적 필드가 보유하며, HideAndDontSave로
    /// 씬 언로드에도 유지된다 (SlashFxHost 패턴).
    /// </summary>
    internal sealed class HitStopHost : MonoBehaviour
    {
        private void Update()
        {
            HitStopManager.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            // 어플리케이션 정지(모바일 백그라운드 등) 진입 시 즉시 복귀 —
            // 저속 timeScale 상태로 앱이 잠기는 사고 방지
            if (paused)
                HitStopManager.ForceRestore();
        }

        private void OnDestroy()
        {
            HitStopManager.NotifyHostDestroyed();
        }
    }
}
