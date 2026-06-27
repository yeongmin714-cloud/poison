using System;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// MonsterLevelLabel 팩토리 — Systems에서 UI.MonsterLevelLabel을 직접 생성하지 않고
    /// LabelFactory.CreateLabel 델리게이트를 통해 간접 생성합니다.
    /// UI 어셈블리의 정적 생성자에서 CreateLabel에 MonsterLevelLabel 생성 람다를 등록합니다.
    /// </summary>
    public static class LabelFactory
    {
        /// <summary>
        /// GameObject에 MonsterLevelLabel을 추가하는 팩토리 델리게이트.
        /// UI 어셈블리에서 등록: LabelFactory.Register(...)
        /// </summary>
        public static Action<GameObject, int> CreateLabel { get; private set; }

        /// <summary>
        /// 팩토리 델리게이트를 등록합니다. 최초 1회만 허용되며,
        /// 이후 중복 호출 시 경고 로그를 출력하고 무시합니다.
        /// </summary>
        /// <param name="factory">GameObject와 int(레벨)를 받아 라벨을 설정하는 델리게이트. null일 수 없습니다.</param>
        /// <exception cref="ArgumentNullException">factory가 null일 경우 발생합니다.</exception>
        public static void Register(Action<GameObject, int> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (CreateLabel == null)
            {
                CreateLabel = factory;
            }
            else
            {
                Debug.LogWarning("[LabelFactory] Register()가 이미 등록된 상태에서 중복 호출되었습니다. 최초 1회만 허용됩니다.");
            }
        }

        /// <summary>테스트 전용: 팩토리 등록 여부</summary>
        public static bool IsRegistered => CreateLabel != null;

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// 테스트 전용: 팩토리 등록을 초기화합니다.
        /// 유닛 테스트에서 Mock 등록을 위해 사용합니다.
        /// </summary>
        public static void Reset()
        {
            CreateLabel = null;
        }
#endif
    }
}