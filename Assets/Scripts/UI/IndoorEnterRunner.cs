using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.UI
{
    /// <summary>
    /// 2026-09-09: 실내 진입 시 sceneLoaded 콜백 시점에 플레이어 오브젝트가 아직 탐색되지 않는
    /// 타이밍 문제 해결용 재시도 러너 — 최대 300프레임 동안 플레이어를 찾아 IndoorScene으로 이동+스폰.
    /// </summary>
    public class IndoorEnterRunner : MonoBehaviour
    {
        private Scene _scene;
        private int _frames;
        private bool _done;

        public void Init(Scene scene) { _scene = scene; }

        private void Update()
        {
            if (_done) return;
            _frames++;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                if (_frames < 300) return; // 아직 스폰 전 — 재시도
                Debug.LogError("[IndoorEnterRunner] 300프레임 내 플레이어 미발견 — 진입 포기");
                _done = true;
                Destroy(gameObject);
                return;
            }

            SceneManager.MoveGameObjectToScene(player, _scene);
            player.transform.position = new Vector3(0f, 0.5f, 0f); // 셸 바닥 위
            Debug.Log($"[IndoorEnterRunner] 플레이어 이동 완료 → 소속 씬: {player.scene.name} (지연 {_frames}프레임)");
            _done = true;
            Destroy(gameObject);
        }
    }
}
