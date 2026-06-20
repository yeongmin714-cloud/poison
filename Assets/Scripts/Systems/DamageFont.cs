using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 1.6: 데미지 폰트 — 적 머리 위에 데미지 숫자 표시.
    /// 일반=흰색, 치명타=노란색, 약점=빨간색.
    /// </summary>
    public class DamageFont : MonoBehaviour
    {
        private float _lifetime = 1.2f;
        private float _elapsed;
        private int _damage;
        private bool _isCrit;
        private GUIStyle _style;

        public static void Spawn(Vector3 worldPos, int damage, bool isCrit)
        {
            var go = new GameObject("DamageFont");
            go.transform.position = worldPos;
            go.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;

            var df = go.AddComponent<DamageFont>();
            df._damage = damage;
            df._isCrit = isCrit;
        }

        private void Start()
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = _isCrit ? 24 : 18,
                fontStyle = _isCrit ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // 위로 떠오르는 효과
            transform.position += Vector3.up * Time.deltaTime * 0.8f;
        }

        private void OnGUI()
        {
            if (_elapsed >= _lifetime) return;

            Vector3 screenPos = Camera.main != null
                ? Camera.main.WorldToScreenPoint(transform.position)
                : Vector3.zero;

            if (screenPos.z < 0) return; // 카메라 뒤

            float alpha = 1f - (_elapsed / _lifetime);
            screenPos.y = Screen.height - screenPos.y; // GUI 좌표계 변환

            _style.normal.textColor = _isCrit
                ? new Color(1f, 0.8f, 0f, alpha)      // 노랑 (치명타)
                : new Color(1f, 1f, 1f, alpha);        // 흰색 (일반)

            string text = _isCrit ? $"★{_damage}★" : _damage.ToString();
            GUI.Label(new Rect(screenPos.x - 40, screenPos.y - 10, 80, 50), text, _style);
        }
    }
}
