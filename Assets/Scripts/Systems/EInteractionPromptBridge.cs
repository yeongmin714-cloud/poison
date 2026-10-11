using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 공용 E 상호작용 프롬프트 배선 브리지 — Systems에서 낚시/잠자기/영주실 등 임의 E 상호작용을
    /// 프롬프트(UI.EInteractionPromptUTK)에 등록/해제. Systems→UI 순환참조 회피용 이벤트 패턴
    /// (SoldierInteractBridge 동일 정신).
    /// UI는 RuntimeInitializeOnLoadMethod(Bootstrap)에서 구독해 Register/Unregister로 라우팅.
    /// </summary>
    public static class EInteractionPromptBridge
    {
        /// <summary>프롬프트 등록 요청 — UI 구독.</summary>
        public static event System.Action<Transform, string, float> OnRegisterRequested;

        /// <summary>프롬프트 해제 요청 — UI 구독.</summary>
        public static event System.Action<Transform> OnUnregisterRequested;

        /// <summary>System에서 호출 — 앵커(대상 Transform) 위에 라벨 프롬프트 표시 등록.</summary>
        public static void RaiseRegister(Transform anchor, string label, float range)
        {
            if (anchor != null)
                OnRegisterRequested?.Invoke(anchor, label, Mathf.Max(0.5f, range));
        }

        /// <summary>System에서 호출 — 해당 앵커 프롬프트 해제.</summary>
        public static void RaiseUnregister(Transform anchor)
        {
            if (anchor != null)
                OnUnregisterRequested?.Invoke(anchor);
        }
    }
}