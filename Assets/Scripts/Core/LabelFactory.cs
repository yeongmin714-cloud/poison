using System;
using UnityEngine;

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
        /// UI 어셈블리에서 등록: LabelFactory.CreateLabel = (go, level) => { ... }
        /// </summary>
        public static Action<GameObject, int> CreateLabel { get; set; }

        /// <summary>테스트용: 팩토리 등록 여부</summary>
        public static bool IsRegistered => CreateLabel != null;
    }
}