using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 수집 채널링 공용 시스템.
    /// E키 → Begin: 자동 채널링 시작, 파랑 진행 바(WorkProgressBar)를 대상 머리 위에 표시.
    /// 완료(progress≥1) → onComplete 호출 후 정리. 이동(CancelMoveThreshold 초과) 시 취소.
    /// 낚시·병사 자동수집 등 기존 즉시 경로에는 영향 없음(호출부만 게이팅).
    /// </summary>
    public sealed class GatherChannelSystem
    {
        // ── 파랑 진행바 (사용자 확정 #58A6FF — Figma E프롬프트 라벨·UTKTheme.Accent와 통일) ──
        private static readonly Color ChannelBlue = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 1f);
        /// <summary>수집 유형 공통 기본 진행 시간(초) — 도구등급 훅이 감쇠 계수로 반영.</summary>
        public const float DefaultChannelSeconds = 2f;
        private const float CancelMoveThreshold = 0.45f;   // 수평 이동 초과 시 취소 (m)

        private static GatherChannelSystem _instance;
        private static readonly object Lock = new object();

        // ── 도구 등급 스켈레톤 훅 ──
        // 지금은 1.0. 나중에 농작기구 등급에서 대입: 등급↑ → 반환값 1 미만 → 진행시간 단축.
        public static Func<float> SpeedFactorProvider = () => 1f;

        private Transform _player;
        private Transform _targetAnchor;
        private Action _onComplete;
        private float _duration;
        private float _startTime;
        private Vector3 _startPos;
        private WorkProgressBar _bar;
        private bool _active;

        public static GatherChannelSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (Lock)
                    {
                        if (_instance == null) _instance = new GatherChannelSystem();
                    }
                }
                return _instance;
            }
        }

        public bool IsActive => _active;
        public float Progress
        {
            get
            {
                if (!_active) return 0f;
                return Mathf.Clamp01((Time.time - _startTime) / Mathf.Max(0.1f, _duration));
            }
        }

        private GatherChannelSystem() { }

        /// <summary>채널 시작. 이미 활성이면 무시하고 false.</summary>
        /// <param name="targetAnchor">진행바를 붙일 대상 Transform (자원 머리 위).</param>
        /// <param name="onComplete">진행 완료 시 호출될 습득 콜백.</param>
        /// <param name="baseDuration">기본 진행 시간(초). 도구등급 훅이 받아 계수 반영.</param>
        public bool Begin(Transform targetAnchor, Action onComplete, float baseDuration)
        {
            if (_active) return false;
            if (targetAnchor == null) return false;

            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                _player = go != null ? go.transform : null;
            }
            if (_player == null) return false;

            float speedFactor = SpeedFactorProvider?.Invoke() ?? 1f;
            float duration = Mathf.Max(0.25f, baseDuration * Mathf.Max(0.25f, speedFactor));

            _active = true;
            _targetAnchor = targetAnchor;
            _onComplete = onComplete;
            _duration = duration;
            _startTime = Time.time;
            _startPos = _player.position;

            _bar = WorkProgressBar.Show(targetAnchor, duration, ChannelBlue);
            return true;
        }

        /// <summary>수동 취소 (다른 시스템에서 호출).</summary>
        public void Cancel()
        {
            if (!_active) return;
            Teardown();
        }

        /// <summary>프레임 틱 — MonoBehaviour(MonoKeeper) 또는 각 호출부 Update에서 호출.</summary>
        public void Tick()
        {
            if (!_active) return;

            // 1) 이동 취소 — 수평(XZ) 이동이 임계 초과 시.
            if (_player != null)
            {
                Vector3 p = _player.position;
                float d = _startPos.x - p.x; d *= d;
                float dz = _startPos.z - p.z; dz *= dz;
                if ((d + dz) > CancelMoveThreshold * CancelMoveThreshold)
                {
                    Teardown();
                    return;
                }
            }

            // 2) 대상 소실 — 앵커 파괴 or 범위 이탈.
            if (_targetAnchor == null)
            {
                Teardown();
                return;
            }

            // 3) 완료.
            if (Progress >= 1f)
            {
                var cb = _onComplete;
                Teardown();
                cb?.Invoke();
            }
        }

        private void Teardown()
        {
            if (_bar != null) _bar.Close();
            _bar = null;
            _active = false;
            _targetAnchor = null;
            _onComplete = null;
            _startPos = Vector3.zero;
        }
    }
}