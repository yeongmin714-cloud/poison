using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [156차 P2-4] 동굴 낙하 안전망 — 룸 루트 부착 벨트앤서스펜더.
    /// 포탈/입구 문 외 어떤 경로로도 낙하지 않게: 플레이어 y < −3m 감지 →
    /// CaveLevelDirector.TeleportPlayerToFloorSpawn 재사용으로 현재 층 스폰 복귀 + 로그 1행.
    /// · 플레이어 캐시 — 기존 프로젝트 패턴(CaveEntranceDoor 선례) 준용: Awake 1회 캐시 + 0.5s 스로틀
    ///   재탐색(매 프레임 Find 금지). 태그 "Player" 우선, 부재 시 CharacterController 폴백(스로틀 경로만).
    /// · 결정론 — 난수 0건, 코루틴 0건. 재복구 연속 발화만 1.0s 쿨다운(Time.time 기반, 스폰 미접지
    ///   결함 시에도 로그 스팸 금지).
    /// · 실내 게이트 무충돌 — IndoorActive 미참조(어셈블리 경계 없음). 가드는 룸 컴포넌트라
    ///   GoDeeper/GoUp/ReturnToSurface의 룸 파괴 시 자동 해제(전 경로 소멸 보장).
    /// </summary>
    public class CaveFallGuard : MonoBehaviour
    {
        private const float FallThresholdY = -3f;         // 낙하 판정선 — 실내 바닥(y≈0) 대비 3m 이하 강하
        private const float RecoverCooldown = 1.0f;       // 재복구 최소 간격(초)
        private const float PlayerSearchInterval = 0.5f;  // 캐시 실패 시 재탐색 스로틀(CaveEntranceDoor 선례)

        private Transform _player;                        // Awake 1회 캐시 — 매 프레임 Find 금지
        private float _playerSearchTimer;
        private float _nextRecoverTime;                   // 쿨다운 게이트

        private void Awake()
        {
            CachePlayer();
        }

        private void Update()
        {
            if (_player == null)
            {
                _playerSearchTimer -= Time.deltaTime;
                if (_playerSearchTimer <= 0f)
                {
                    _playerSearchTimer = PlayerSearchInterval;
                    CachePlayer();
                }
                if (_player == null) return;
            }

            if (Time.time < _nextRecoverTime) return;     // 쿨다운 — 연속 발화 억제
            if (_player.position.y >= FallThresholdY) return;

            _nextRecoverTime = Time.time + RecoverCooldown;
            // [156차 실측] 낙하 중 텔레포트는 CC 내부 포즈가 되돌림(발화 확인 후에도 낙하 지속 — 배치 프로브 실측).
            //   표준 텔레포트 관례 적용: CC 비활성 → Director 단일소스 경유 복귀 → CC 재활성(내부 포즈 재동기화).
            var cc = _player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            CaveLevelDirector.TeleportPlayerToFloorSpawn();   // [156차 P2-4] 기존 텔레포트 경로 재사용(층 스폰)
            if (cc != null) cc.enabled = true;
            Debug.Log($"[CaveFallGuard] 낙하 감지 — 스폰 복귀 pos={_player.position.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        /// <summary>플레이어 캐시 — 태그 "Player" 우선(CaveLevelDirector 동일 패턴), 부재 시 CharacterController 폴백.</summary>
        private void CachePlayer()
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                _player = tagged.transform;
                return;
            }
            var controller = FindFirstObjectByType<CharacterController>();   // 폴백 — 스로틀 경로에서만 호출
            if (controller != null) _player = controller.transform;
        }
    }
}
