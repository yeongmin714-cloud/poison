using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [TEST27-68차] 병사 헤드 UI — 머리 위 이름 + Lv + HP바 (MonsterHeadUI 패턴, IMGUI OnGUI).
    /// GuardPlaceholder.CurrentHP/MaxHP를 직접 폴링해 병사가 몬스터 공격에 데미지를 받고 죽는 과정이
    /// 눈에 보인다. CreateGuard에서 AddComponent된다.
    /// </summary>
    public class GuardHeadUI : MonoBehaviour
    {
        /// <summary>[UTK 은퇴 게이트] true면 OnGUI 은퇴 — NameplateOverlayUTK가 대행.</summary>
        public static bool s_retired = false;

        private GuardPlaceholder _guard;
        private Camera _cam;
        private static GUIStyle _labelStyle;

        private void Awake()
        {
            _guard = GetComponentInParent<GuardPlaceholder>();
        }

        private void Update()
        {
            if (_cam == null || !_cam.isActiveAndEnabled)
                _cam = Camera.main;
        }

        private void OnGUI()
        {
            if (s_retired) return; // [UTK 은퇴] NameplateOverlayUTK 대행 — 원본 그리기 중단
            if (ProjectName.Core.UITransitionState.AnyWindowOpen) return; // [U8 은퇴] 창 열림 시 헤드UI 숨김(겹침 방지)
            if (_guard == null || !_guard.IsAlive || _cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * 2.2f;
            Vector3 sp = _cam.WorldToScreenPoint(worldPos);
            if (sp.z <= 0f) return;   // 카메라 뒤

            float x = sp.x;
            float y = Screen.height - sp.y;   // IMGUI는 좌상단 원점
            float w = 74f, h = 7f;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                };
                _labelStyle.normal.textColor = Color.white;
            }

            // 배경 → HP 채움(비율색: ≥0.6 녹 / ≥0.3 노랑 / 빨강) — MonsterHeadUI와 동일 규약
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(x - w / 2f - 1f, y - 1f, w + 2f, h + 2f), Texture2D.whiteTexture);
            float ratio = _guard.MaxHP > 0f ? Mathf.Clamp01(_guard.CurrentHP / _guard.MaxHP) : 0f;
            GUI.color = ratio >= 0.6f ? new Color(0.30f, 0.85f, 0.30f)
                      : ratio >= 0.3f ? new Color(0.95f, 0.80f, 0.20f)
                      : new Color(0.90f, 0.20f, 0.20f);
            GUI.DrawTexture(new Rect(x - w / 2f, y, w * ratio, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 이름 + Lv (HP바 위)
            GUI.Label(new Rect(x - 60f, y - 20f, 120f, 18f),
                $"{_guard.GuardName} Lv.{_guard.Level}", _labelStyle);
        }
    }
}
