using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 몬스터 어그로 합세 시스템 (Monster Aggro).
    /// 싱글톤. 같은 종류의 몬스터가 10m 내에서 공격당하는 것을 보면 합세.
    /// 
    /// 사용법:
    ///   IAggroable monster = ...;
    ///   MonsterAggroSystem.Instance.RegisterMonster(monster);
    ///   MonsterAggroSystem.Instance.NotifyAttack(attackedGameObject, attackerGameObject);
    /// </summary>
    public class MonsterAggroSystem : MonoBehaviour
    {
        public const float AGGRO_RANGE = 10f;

        private static MonsterAggroSystem _instance;
        public static MonsterAggroSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MonsterAggroSystem");
                    _instance = go.AddComponent<MonsterAggroSystem>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>등록된 모든 몬스터 (IAggroable → GameObject 매핑)</summary>
        private readonly Dictionary<IAggroable, GameObject> _monsterMap = new Dictionary<IAggroable, GameObject>();

        /// <summary>Update에서 재사용할 제거 목록 캐시 (GC 부하 방지)</summary>
        private readonly List<IAggroable> _toRemoveCache = new List<IAggroable>();

        /// <summary>등록된 모든 IAggroable 목록</summary>
        public IReadOnlyCollection<IAggroable> AllMonsters => _monsterMap.Keys;

        /// <summary>등록된 몬스터 수</summary>
        public int MonsterCount => _monsterMap.Count;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>몬스터 등록</summary>
        public void RegisterMonster(IAggroable monster)
        {
            if (monster == null) return;
            var mb = monster as MonoBehaviour;
            if (mb == null) return;
            if (!_monsterMap.ContainsKey(monster))
            {
                _monsterMap[monster] = mb.gameObject;
            }
        }

        /// <summary>몬스터 등록 해제</summary>
        public void UnregisterMonster(IAggroable monster)
        {
            if (monster != null)
                _monsterMap.Remove(monster);
        }

        /// <summary>
        /// 공격 통보. attackedMonster가 공격당했음을 시스템에 알림.
        /// 주변 10m 이내 같은 종류의 몬스터를 찾아 합세시킴.
        /// </summary>
        public void NotifyAttack(GameObject attackedMonster, GameObject attacker)
        {
            if (attackedMonster == null || attacker == null) return;

            Vector3 attackPos = attackedMonster.transform.position;
            string attackedType = GetMonsterType(attackedMonster);
            if (attackedType == null) return;

            // 공격받은 몬스터 자신도 어그로 설정 (전투 중이 아니거나 쿨다운 중일 때만)
            var selfAggro = attackedMonster.GetComponent<IAggroable>();
            if (selfAggro != null && !selfAggro.IsInCombat)
            {
                selfAggro.SetAggroTarget(attacker);
            }

            // 주변 같은 종류 몬스터 탐색
            foreach (var kvp in _monsterMap)
            {
                var monster = kvp.Key;
                var go = kvp.Value;

                // 자기 자신 or null 체크
                if (go == null || go == attackedMonster) continue;
                if (monster.MonsterType != attackedType) continue;
                if (monster.IsInCombat) continue; // 이미 전투 중이면 스킵

                float dist = Vector3.Distance(go.transform.position, attackPos);
                if (dist <= AGGRO_RANGE)
                {
                    monster.SetAggroTarget(attacker);
                }
            }
        }

        /// <summary>
        /// GameObject에서 MonsterType 문자열을 추출.
        /// IAggroable이 없으면 null 반환.
        /// </summary>
        public static string GetMonsterType(GameObject go)
        {
            if (go == null) return null;
            var agg = go.GetComponent<IAggroable>();
            return agg?.MonsterType;
        }

        private void Update()
        {
            // 각 몬스터의 어그로 타이머 업데이트
            _toRemoveCache.Clear();
            foreach (var kvp in _monsterMap)
            {
                var monster = kvp.Key;
                var go = kvp.Value;

                if (go == null)
                {
                    _toRemoveCache.Add(monster);
                    continue;
                }

                monster.UpdateAggroTimer(Time.deltaTime);
            }

            foreach (var m in _toRemoveCache)
                _monsterMap.Remove(m);
        }

        #if UNITY_EDITOR
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 600));
            GUILayout.Label("[ MonsterAggroSystem ]", GUI.skin.box);
            GUILayout.Label($"Monsters: {_monsterMap.Count}");

            foreach (var kvp in _monsterMap)
            {
                var monster = kvp.Key;
                var go = kvp.Value;
                string name = go != null ? go.name : "(destroyed)";
                string type = monster.MonsterType ?? "?";
                string state = monster.CurrentAggroState.ToString();
                string target = monster.AggroTarget != null ? monster.AggroTarget.name : "none";

                GUILayout.Label($"{name} [{type}] State={state} Target={target}");
            }
            GUILayout.EndArea();
        }
#endif

        #if UNITY_EDITOR
        /// <summary>테스트용: 싱글톤 강제 초기화</summary>
        public static void ResetInstance()
        {
            if (_instance != null)
            {
                DestroyImmediate(_instance.gameObject);
                _instance = null;
            }
        }
#endif

        /// <summary>테스트용: 특정 위치의 몬스터 찾기 (거리 기반)</summary>
        public IAggroable FindNearestAggroable(Vector3 position, float maxDist)
        {
            IAggroable nearest = null;
            float bestDist = maxDist;
            foreach (var kvp in _monsterMap)
            {
                if (kvp.Value == null) continue;
                float d = Vector3.Distance(kvp.Value.transform.position, position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    nearest = kvp.Key;
                }
            }
            return nearest;
        }
    }
}