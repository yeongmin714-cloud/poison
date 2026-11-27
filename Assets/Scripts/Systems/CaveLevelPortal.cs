using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [143차 P7] 동굴 층 전환 포탈 — 거리 판정형(콜라이더 없음, GemChest 선례 계약).
    /// 근접(2.2m) + E키 → CaveLevelDirector.GoDeeper/GoUp(룸 파괴 → 리빌드 방식 — 탑다운 적층 금지).
    /// OnGUI 근접 프롬프트(GemChest OnGUI 선례 — GUI.Box+Label, 13pt 중앙정렬).
    /// </summary>
    public class CaveLevelPortal : MonoBehaviour
    {
        [Header("포탈 설정")]
        [SerializeField] private bool _descending = true;       // true=하강(GoDeeper), false=상승(GoUp)
        [SerializeField] private int _floorOffset = 1;          // +1=하강, −1=상승(설계 의도 표기)
        [SerializeField] private string _prompt = "E: 지하 2층으로";
        [SerializeField] private float _interactRadius = 2.2f;

        private Transform _player;
        private float _playerSearchTimer;   // 플레이어 0.5s 스로틀 재탐색
        private bool _busy;                 // 디바운스 — 연타 이중 전환 차단

        /// <summary>빌더 후처리 설정(프리미티브 생성 직후 명시 API로 주입).</summary>
        public void Configure(bool descending, int floorOffset, string prompt)
        {
            _descending = descending;
            _floorOffset = floorOffset;
            _prompt = prompt;
        }

        private void Update()
        {
            if (_player == null)
            {
                _playerSearchTimer -= Time.deltaTime;
                if (_playerSearchTimer <= 0f)
                {
                    _playerSearchTimer = 0.5f;
                    var found = GameObject.FindGameObjectWithTag("Player");
                    if (found != null) _player = found.transform;
                }
                if (_player == null) return;
            }

            if (_busy) return;
            if (Vector3.Distance(transform.position, _player.position) > _interactRadius) return;
            if (!Input.GetKeyDown(KeyCode.E)) return;

            _busy = true;   // 룸 파괴 → 리빌드 시 이 컴포넌트도 함께 파괴(신규 인스턴스가 이어받음)
            var roomRoot = transform.root != null ? transform.root.gameObject : gameObject;
            var next = _descending ? CaveLevelDirector.GoDeeper(roomRoot) : CaveLevelDirector.GoUp(roomRoot);
            if (next == roomRoot) _busy = false;   // 가드 차단(전환 없음) — 재시도 허용
        }

        private void OnGUI()
        {
            if (_player == null || _busy) return;
            if (Vector3.Distance(transform.position, _player.position) > _interactRadius) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            float w = 280f, h = 30f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 40f, w, h);
            GUI.Box(rect, string.Empty);
            GUI.Label(rect, _prompt, style);
        }
    }
}
