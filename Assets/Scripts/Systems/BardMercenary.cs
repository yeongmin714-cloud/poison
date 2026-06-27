using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P25-04: 바드(Bard) 특수 용병.
    /// 전투에 직접 참여하지 않고 반경 15m 아군에게 버프를 부여합니다.
    /// 버프: 공격력+15%, 방어력+10%, 이동속도+10%
    /// </summary>
    public class BardMercenary : MonoBehaviour
    {
        [Header("버프 설정")]
        [SerializeField] private float _buffRange = 15f;
        [SerializeField] private float _attackBuffPercent = 15f;
        [SerializeField] private float _defenseBuffPercent = 10f;
        [SerializeField] private float _speedBuffPercent = 10f;
        [SerializeField] private float _buffInterval = 1.0f; // 1초마다 버프 갱신

        [Header("참조")]
        [SerializeField] private string _mercenaryId = "merc_bard_01";
        [SerializeField] private bool _isActive = true;

        private float _timer = 0f;

        /// <summary>버프 범위</summary>
        public float BuffRange => _buffRange;

        /// <summary>용병 ID</summary>
        public string MercenaryId
        {
            get => _mercenaryId;
            set => _mercenaryId = value;
        }

        /// <summary>버프 활성화 여부</summary>
        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        private void Update()
        {
            if (!_isActive) return;

            _timer += Time.deltaTime;
            if (_timer >= _buffInterval)
            {
                _timer = 0f;
                ApplyBuffs();
            }
        }

        /// <summary>이 바드가 부여한 모든 버프 해제 (비활성화/파괴 시)</summary>
        private void ClearAllBuffs()
        {
            if (BardBuffManager.Instance != null)
            {
                BardBuffManager.Instance.ClearBySource(_mercenaryId);
            }
        }

        private void OnDisable()
        {
            ClearAllBuffs();
        }

        private void OnDestroy()
        {
            ClearAllBuffs();
        }

        /// <summary>반경 내 아군에게 버프 적용</summary>
        private void ApplyBuffs()
        {
            // 주변 GuardPlaceholder 검색
            var guards = FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);
            int buffedCount = 0;

            foreach (var guard in guards)
            {
                if (!guard.IsAlive) continue;

                float dist = Vector3.Distance(transform.position, guard.transform.position);
                if (dist <= _buffRange)
                {
                    ApplyBuffToGuard(guard);
                    buffedCount++;
                }
            }

            // 주변 MercenaryPlaceholder 검색 (다른 용병)
            var mercs = FindObjectsByType<MercenaryPlaceholder>(FindObjectsSortMode.None);
            foreach (var merc in mercs)
            {
                if (merc.MercenaryId == _mercenaryId) continue; // 자기 자신 제외

                float dist = Vector3.Distance(transform.position, merc.transform.position);
                if (dist <= _buffRange)
                {
                    ApplyBuffToMercenary(merc);
                    buffedCount++;
                }
            }

            if (buffedCount > 0 && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[BardMercenary] 🎵 {buffedCount}명 아군 버프 중...");
            }
        }

        /// <summary>개별 가드에게 버프 적용</summary>
        private void ApplyBuffToGuard(GuardPlaceholder guard)
        {
            // 실제 전투 시스템에서 능력치를 보정합니다.
            // 여기서는 태그/플래그로 표시
            // 버프는 GuardCombatAI 등에서 참조할 수 있도록 전역 버프 목록에 등록

            if (BardBuffManager.Instance != null)
            {
                BardBuffManager.Instance.RegisterBuffedGuard(guard, new BardBuffData
                {
                    attackBonus = _attackBuffPercent / 100f,
                    defenseBonus = _defenseBuffPercent / 100f,
                    speedBonus = _speedBuffPercent / 100f,
                    sourceId = _mercenaryId
                });
            }
        }

        /// <summary>개별 용병에게 버프 적용</summary>
        private void ApplyBuffToMercenary(MercenaryPlaceholder merc)
        {
            if (BardBuffManager.Instance != null)
            {
                BardBuffManager.Instance.RegisterBuffedMercenary(merc, new BardBuffData
                {
                    attackBonus = _attackBuffPercent / 100f,
                    defenseBonus = _defenseBuffPercent / 100f,
                    speedBonus = _speedBuffPercent / 100f,
                    sourceId = _mercenaryId
                });
            }
        }

        /// <summary>버프 중인 아군 수 계산</summary>
        public int GetBuffedAllyCount()
        {
            if (BardBuffManager.Instance == null) return 0;
            return BardBuffManager.Instance.GetBuffedCountBySource(_mercenaryId);
        }

        private void OnDrawGizmosSelected()
        {
            // 버프 범위 표시
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _buffRange);

            // 버프 위치 마커 (상단에 노란색 구체)
            Vector3 labelPos = transform.position + Vector3.up * 3f;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(labelPos, 0.25f);
            Gizmos.DrawWireSphere(labelPos, 0.35f);
        }
    }
}