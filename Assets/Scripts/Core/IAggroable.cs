using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 몬스터 어그로(합세) 시스템 인터페이스.
    /// MonsterAggroSystem이 같은 종류 몬스터 간 어그로 전파 시 호출.
    /// </summary>
    public interface IAggroable
    {
        /// <summary>어그로 대상 설정 (같은 종류 몬스터가 공격받음 → 합세)</summary>
        /// <param name="target">어그로 대상 GameObject (null 전달 시 무시됨)</param>
        void SetAggroTarget(GameObject target);

        /// <summary>어그로 해제 (대상 사망/이탈)</summary>
        void ClearAggro();

        /// <summary>현재 전투 중인지 여부 (Combat 상태)</summary>
        bool IsInCombat { get; }

        /// <summary>몬스터 종류 식별자 (예: "rabbit", "boar", "wolf")</summary>
        string MonsterType { get; }

        /// <summary>현재 어그로 상태</summary>
        AggroState CurrentAggroState { get; }

        /// <summary>현재 어그로 대상 GameObject (null 가능)</summary>
        GameObject AggroTarget { get; }

        /// <summary>어그로 타이머 업데이트. 각 몬스터가 자체 상태 전이 처리.</summary>
        /// <param name="deltaTime">프레임 간 경과 시간 (초 단위)</param>
        void UpdateAggroTimer(float deltaTime);
    }
}
