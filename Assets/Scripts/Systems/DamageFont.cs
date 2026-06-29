using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 1.6: 데미지 폰트 — 적 머리 위에 데미지 숫자 표시.
    /// 일반=흰색, 치명타=노란색, 약점=빨간색.
    /// </summary>
    public class DamageFont : MonoBehaviour
    {
        private const float Lifetime = 1.2f;

        private float _elapsed;
        private int _damage;
        private bool _isCrit;
        private bool _isWeakness;
        private GUIStyle _style;
        private Camera _camera;

        public static void Spawn(Vector3 worldPos, int damage, bool isCrit)
        {
            Spawn(worldPos, damage, isCrit, false);
        }

        public static void Spawn(Vector3 worldPos, int damage, bool isCrit, bool isWeakness)
        {
            var go = new GameObject("DamageFont");
            go.transform.position = worldPos;

            var cam = Camera.main;
            if (cam != null)
                go.transform.rotation = cam.transform.rotation;

            var df = go.AddComponent<DamageFont>();
            df._damage = damage;
            df._isCrit = isCrit;
            df._isWeakness = isWeakness;
        }

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // 위로 떠오르는 효과
            transform.position += Vector3.up * Time.deltaTime * 0.8f;
        }

        private void OnGUI()
        {
            if (_elapsed >= Lifetime) return;
            if (_camera == null) return;

            // 지연 초기화: GUI.skin은 OnGUI 내에서만 접근 가능
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = _isCrit ? 24 : 18,
                    fontStyle = _isCrit ? FontStyle.Bold : FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            Vector3 screenPos = _camera.WorldToScreenPoint(transform.position);

            if (screenPos.z < 0) return; // 카메라 뒤

            float alpha = 1f - (_elapsed / Lifetime);
            screenPos.y = Screen.height - screenPos.y; // GUI 좌표계 변환

            // 일반=흰색, 치명타=노란색, 약점=빨간색
            if (_isWeakness)
            {
                _style.normal.textColor = new Color(1f, 0.2f, 0.2f, alpha); // 빨강 (약점)
            }
            else if (_isCrit)
            {
                _style.normal.textColor = new Color(1f, 0.8f, 0f, alpha);   // 노랑 (치명타)
            }
            else
            {
                _style.normal.textColor = new Color(1f, 1f, 1f, alpha);     // 흰색 (일반)
            }

            string text = _isCrit ? $"★{_damage}★" : _damage.ToString();
            GUI.Label(new Rect(screenPos.x - 40, screenPos.y - 10, 80, 50), text, _style);
        }
    }
}
