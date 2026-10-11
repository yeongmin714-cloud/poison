using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [39차] 침대 휴식 강제 — 누운 자세/위치를 매 프레임 LateUpdate에서 재단언.
    /// HumanoidClipDriver/전투 AI/기타 시스템이 루트 transform을 덮어쓰는 것과 무관하게
    /// "침대에 누워 있는 상태"를 보장(사용자 재보고 "침대에 안 누워있는" 근본 차단).
    /// 기상(WakeFromBed) 시 제거된다. 실내(IndoorActive)에서만 강제 — 월드 복귀 시 방해 없음.
    /// </summary>
    public class BedRestEnforcer : MonoBehaviour
    {
        public Vector3 Anchor;
        public Quaternion Pose;

        private void LateUpdate()
        {
            if (!ProjectName.Core.UITransitionState.IndoorActive) return;
            transform.position = Anchor;
            transform.rotation = Pose;
        }
    }
}
