using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-06: 전체 화면 색상 플래시 오버레이 (정적 클래스).
    /// 일반 피격=흰색, 크리티컬=주황, 처치=빨강.
    /// OnGUI + GUI.Box로 화면 전체 반투명 사각형을 그리고 intensity 알파에서
    /// 0까지 duration초 동안 페이드 아웃한다.
    /// 단일 인스턴스 유지 — 플래시 도중 재호출 시 새로 쌓지 않고 덮어쓴다.
    /// </summary>
    public static class ScreenFlashFX
    {
        // ================================================================
        // 플래시 색상 상수
        // ================================================================
        /// <summary>일반 피격 (흰색)</summary>
        public static readonly Color HitColor = Color.white;
        /// <summary>크리티컬 (주황)</summary>
        public static readonly Color CritColor = new Color(1f, 0.55f, 0f, 1f);
        /// <summary>처치 (빨강)</summary>
        public static readonly Color KillColor = new Color(0.9f, 0.1f, 0.1f, 1f);

        // ================================================================
        // 단일 인스턴스 (안티 스팸)
        // ================================================================
        private static ScreenFlashRunner _activeRunner;

        /// <summary>
        /// 화면 전체 색상 플래시를 표시한다.
        /// intensity(0~1) 알파에서 시작해 duration초 동안 0으로 페이드 아웃.
        /// 이미 활성 플래시가 있으면 스택하지 않고 색상/강도/지속시간을 덮어쓴다.
        /// </summary>
        public static void Flash(Color color, float intensity, float duration)
        {
            intensity = Mathf.Clamp01(intensity);
            if (duration <= 0f) duration = 0.15f;

            bool overwritten = _activeRunner != null;
            if (overwritten)
            {
                _activeRunner.Restart(color, intensity, duration);
            }
            else
            {
                var go = new GameObject("ScreenFlash");
                _activeRunner = go.AddComponent<ScreenFlashRunner>();
                _activeRunner.Init(color, intensity, duration);
            }

            Debug.Log($"[ScreenFlashFX] flash color={color} intensity={intensity:0.00} duration={duration:0.00}" +
                      (overwritten ? " (overwritten)" : " (spawned)"));
        }

        /// <summary>일반 피격 — 흰색 플래시</summary>
        public static void FlashWhite(float intensity, float duration) => Flash(HitColor, intensity, duration);

        /// <summary>크리티컬 — 주황 플래시</summary>
        public static void FlashOrange(float intensity, float duration) => Flash(CritColor, intensity, duration);

        /// <summary>처치 — 빨강 플래시</summary>
        public static void FlashRed(float intensity, float duration) => Flash(KillColor, intensity, duration);

        // ================================================================
        // 내부 Runner: IMGUI 전체 화면 플래시 (Fade Out)
        // ================================================================
        private class ScreenFlashRunner : MonoBehaviour
        {
            private Color _color;
            private float _intensity;
            private float _duration;
            private float _elapsed;
            private GUIStyle _style;

            public void Init(Color color, float intensity, float duration)
            {
                // 필드 저장만 수행 — GUIStyle 생성은 OnGUI 내 지연 생성 폴백으로 위임한다.
                // (OnGUI 밖에서 GUI.skin 접근 시 ArgumentException 발생 — G2-06 버그 수리,
                //  DamageNumberRunner.Init 선례와 동일 패턴)
                _color = color;
                _intensity = intensity;
                _duration = Mathf.Max(0.01f, duration);
                _elapsed = 0f;
            }

            /// <summary>안티 스팸: 활성 플래시의 파라미터를 덮어쓰고 페이드를 재시작.</summary>
            public void Restart(Color color, float intensity, float duration)
            {
                _color = color;
                _intensity = intensity;
                _duration = Mathf.Max(0.01f, duration);
                _elapsed = 0f;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                if (_elapsed < _duration) return;

                if (_activeRunner == this)
                    _activeRunner = null;
                Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (_activeRunner == this)
                    _activeRunner = null;
            }

            private void OnGUI()
            {
                if (_style == null)
                {
                    _style = new GUIStyle(GUI.skin.box)
                    {
                        normal = { background = Texture2D.whiteTexture }
                    };
                }

                float alpha = Mathf.Lerp(_intensity, 0f, _elapsed / _duration);
                if (alpha <= 0f) return;

                // 흰색 텍스처에 GUI.color 곱연산으로 플래시 색상 + 현재 알파 적용
                Color prevColor = GUI.color;
                GUI.color = new Color(_color.r, _color.g, _color.b, alpha);
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _style);
                GUI.color = prevColor;
            }
        }
    }
}
