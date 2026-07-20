using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 상호작용 가능한 개체를 위한 인터페이스
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 개체가 상호작용 가능한지 여부
        /// </summary>
        bool CanInteract { get; }
        
        /// <summary>
        /// 상호작용 수행
        /// </summary>
        void Interact();
        
        /// <summary>
        /// 상호작용 시 힌트 표시에 사용될 텍스트
        /// </summary>
        string InteractHint { get; }
    }
}