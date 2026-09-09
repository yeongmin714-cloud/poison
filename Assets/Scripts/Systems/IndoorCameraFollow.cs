using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>실내 카메라 추적 — 메인 씬 카메라와 동일 시점(피치 65°/거리 11m, 플레이어 3/4뷰).</summary>
    public class IndoorCameraFollow : MonoBehaviour
    {
        private Transform _player;
        private const float Pitch = 65f;
        private const float Dist = 11f;

        private void LateUpdate()
        {
            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                _player = p.transform;
            }

            Vector3 pos = _player.position + new Vector3(0f, 1.4f, 0f)
                          + Quaternion.Euler(Pitch, 0f, 0f) * new Vector3(0f, 0f, -Dist);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(_player.position + new Vector3(0f, 1.2f, 0f) - pos);
        }
    }
}
