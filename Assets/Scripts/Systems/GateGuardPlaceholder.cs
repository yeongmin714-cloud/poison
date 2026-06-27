using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-08: 문지기 배치 — 영지 입구에 문지기 병사를 배치합니다.
    /// 
    /// GuardPost 열거형(Left, Right, Gate)으로 문지기 위치를 지정하고,
    /// 2~5명의 문지기를 영지 입구에 생성합니다.
    /// GuardCombatAI의 COMBAT_DETECT_RANGE를 감지 범위로 사용합니다.
    /// 에디터 Gizmos로 배치 위치를 시각화합니다.
    /// </summary>
    public class GateGuardPlaceholder : MonoBehaviour
    {
        /// <summary>
        /// 문지기 배치 위치 타입
        /// </summary>
        public enum GuardPost
        {
            /// <summary>입구 왼쪽</summary>
            Left,
            /// <summary>입구 오른쪽</summary>
            Right,
            /// <summary>정문 중앙</summary>
            Gate
        }

        [Header("문지기 설정")]
        [SerializeField] private GuardPost _post = GuardPost.Gate;
        [SerializeField] private int _guardCount = 2;
        [SerializeField] private float _spacing = 2f;
        [SerializeField] private float _detectionRange = GuardCombatAI.COMBAT_DETECT_RANGE;
        [SerializeField] private string _gateGuardNamePrefix = "문지기";

        [Header("소속")]
        [SerializeField] private NationType _nation = NationType.East;
        [SerializeField] private int _territoryIndex = 1;

        [Header("참조")]
        [SerializeField] private Transform _entrancePoint;

        /// <summary>생성된 문지기 목록</summary>
        private readonly List<GuardPlaceholder> _spawnedGuards = new List<GuardPlaceholder>();

        /// <summary>문지기 배치 위치 (Left/Right/Gate)</summary>
        public GuardPost Post => _post;

        /// <summary>문지기 수</summary>
        public int GuardCount => _guardCount;

        /// <summary>감지 범위</summary>
        public float DetectionRange => _detectionRange;

        /// <summary>소속 국가</summary>
        public NationType Nation => _nation;

        /// <summary>영지 인덱스</summary>
        public int TerritoryIndex => _territoryIndex;

        /// <summary>생성된 모든 문지기</summary>
        public IReadOnlyList<GuardPlaceholder> SpawnedGuards => _spawnedGuards;

        private void Awake()
        {
            if (_entrancePoint == null)
                _entrancePoint = transform;

            SpawnGateGuards();
        }

        /// <summary>
        /// 영지 입구에 문지기 병사를 생성합니다.
        /// GuardCount (2~5) 만큼 GuardPlaceholder를 스폰하여 _spawnedGuards에 등록합니다.
        /// </summary>
        public void SpawnGateGuards()
        {
            // 기존 스폰된 오브젝트 정리 (메모리 누수 방지)
            foreach (var guard in _spawnedGuards)
            {
                if (guard != null)
                    Destroy(guard.gameObject);
            }
            _spawnedGuards.Clear();

            int count = Mathf.Clamp(_guardCount, 2, 5);
            Vector3 basePos = _entrancePoint != null ? _entrancePoint.position : transform.position;

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = CalculatePostOffset(i, count);
                Vector3 spawnPos = basePos + offset;

                GameObject guardGo = new GameObject($"{_gateGuardNamePrefix}_{_post}_{i + 1}");
                guardGo.transform.position = spawnPos;
                guardGo.transform.SetParent(transform);

                GuardPlaceholder guard = guardGo.AddComponent<GuardPlaceholder>();
                guard.SetGuardInfo($"{_gateGuardNamePrefix} {i + 1}", Random.Range(1, 5), _nation);

                _spawnedGuards.Add(guard);
            }

            Debug.Log($"[GateGuardPlaceholder] {_post} 위치에 문지기 {count}명 배치 완료");
        }

        /// <summary>
        /// 배치 위치에 따른 오프셋 계산
        /// </summary>
        private Vector3 CalculatePostOffset(int index, int total)
        {
            float half = (total - 1) * _spacing * 0.5f;
            float xOffset = (index * _spacing) - half;

            switch (_post)
            {
                case GuardPost.Left:
                    return new Vector3(-_spacing * 2f, 0f, xOffset);
                case GuardPost.Right:
                    return new Vector3(_spacing * 2f, 0f, xOffset);
                case GuardPost.Gate:
                default:
                    return new Vector3(xOffset, 0f, 0f);
            }
        }

        /// <summary>
        /// 해당 문지기 배치에 속한 모든 생존 문지기 반환
        /// </summary>
        public List<GuardPlaceholder> GetAliveGuards()
        {
            var alive = new List<GuardPlaceholder>();
            foreach (var guard in _spawnedGuards)
            {
                if (guard != null && guard.IsAlive)
                    alive.Add(guard);
            }
            return alive;
        }

        /// <summary>
        /// 문지기 배치에 속한 모든 생존 문지기 수
        /// </summary>
        public int AliveCount
        {
            get
            {
                int count = 0;
                foreach (var guard in _spawnedGuards)
                {
                    if (guard != null && guard.IsAlive)
                        count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 모든 문지기가 사망했는지 확인
        /// </summary>
        public bool AllGuardsDefeated
        {
            get
            {
                foreach (var guard in _spawnedGuards)
                {
                    if (guard != null && guard.IsAlive)
                        return false;
                }
                return _spawnedGuards.Count > 0;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 basePos = _entrancePoint != null ? _entrancePoint.position : transform.position;

            // 감지 범위 표시 (GuardCombatAI 기준)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(basePos, _detectionRange);

            // 배치 위치 표시
            int count = Mathf.Clamp(_guardCount, 2, 5);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = CalculatePostOffset(i, count);
                Vector3 pos = basePos + offset;

                switch (_post)
                {
                    case GuardPost.Left:
                        Gizmos.color = Color.cyan;
                        break;
                    case GuardPost.Right:
                        Gizmos.color = Color.magenta;
                        break;
                    case GuardPost.Gate:
                        Gizmos.color = Color.yellow;
                        break;
                }

                Gizmos.DrawSphere(pos, 0.4f);
                Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
            }

            // 방향 표시 화살표 (입구 방향)
            Gizmos.color = Color.white;
            Vector3 arrowEnd = basePos + Vector3.forward * _spacing;
            Gizmos.DrawLine(basePos, arrowEnd);
            Gizmos.DrawLine(arrowEnd, arrowEnd + Vector3.left * 0.3f + Vector3.back * 0.3f);
            Gizmos.DrawLine(arrowEnd, arrowEnd + Vector3.right * 0.3f + Vector3.back * 0.3f);
        }

        private void OnDrawGizmos()
        {
            // 항상 표시 (선택되지 않아도 표시)
            Vector3 basePos = _entrancePoint != null ? _entrancePoint.position : transform.position;

            Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
            Gizmos.DrawSphere(basePos, 0.15f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(basePos + Vector3.up * 1.5f,
                $"[문지기] {_gateGuardNamePrefix} ({_post}) x{_guardCount}");
#endif
        }
    }
}