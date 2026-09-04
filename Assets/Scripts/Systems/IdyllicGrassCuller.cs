using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// AA5: 잔디 풋 커버 + FlowerMeadow 패치 컬링 (간단 거리 체크 Update 0.5초 주기).
    /// 플레이어 반경 60m 밖의 Grass/FlowerMeadow 자식 오브젝트 SetActive(false) —
    /// 대량 잔디를 인스턴싱+컬링 없이 전부 활성하면 draw call이 폭주하므로 게이트.
    /// IdyllicDecoPlacer.PlaceAll이 Grass/FM root에 부착한다.
    /// </summary>
    public class IdyllicGrassCuller : MonoBehaviour
    {
        const float CULL_RADIUS = 60f;   // 플레이어 반경 60m만 활성
        const float INTERVAL = 0.5f;     // Update 주기

        static readonly Vector3 FALLBACK = ProjectName.Core.PlayerSpawnConfig.SpawnPosition;

        [SerializeField] private bool _cullingEnabled = true;
        [SerializeField, HideInInspector] public bool _targetsFixed;
        private Transform _grassRoot;
        private Transform _fmRoot;
        private float _timer;
        private int _lastActive;

        public void Configure(Transform grassRoot, Transform fmRoot)
        {
            _grassRoot = grassRoot;
            _fmRoot = fmRoot;
            _targetsFixed = true;
            _timer = 0f;
        }

        void Awake() { _targetsFixed = false; }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = INTERVAL;

            if (!_cullingEnabled) return;
            if (!_targetsFixed) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 pc = (player != null) ? player.transform.position : FALLBACK;

            int active = 0;
            active += CullRoot(_grassRoot, pc);
            active += CullRoot(_fmRoot, pc);
            _lastActive = active;
        }

        static int CullRoot(Transform root, Vector3 playerPos)
        {
            if (root == null) return 0;
            int active = 0;
            float r2 = CULL_RADIUS * CULL_RADIUS;
            for (int i = 0; i < root.childCount; i++)
            {
                var go = root.GetChild(i);
                if (go == null) continue;
                Vector3 p = go.transform.position;
                float dx = p.x - playerPos.x, dz = p.z - playerPos.z;
                bool on = dx * dx + dz * dz <= r2;
                if (on != go.activeSelf)
                    go.SetActive(on);
                if (on) active++;
            }
            return active;
        }

        public int ActiveCount() { return _lastActive; }
    }
}