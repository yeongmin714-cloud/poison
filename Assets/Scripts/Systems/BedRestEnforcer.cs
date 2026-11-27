using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [39차] 침대 휴식 강제 — 누운 자세/위치를 매 프레임 LateUpdate에서 재단언.
    /// HumanoidClipDriver/전투 AI/기타 시스템이 루트 transform을 덮어쓰는 것과 무관하게
    /// "침대에 누워 있는 상태"를 보장(사용자 재보고 "침대에 안 누워있는" 근본 차단).
    /// 기상(WakeFromBed) 시 제거된다.
    /// [44차] IndoorActive 플래그 게이트 제거 — 이 컴포넌트는 실내 배치 경로(PlaceAllyOnBed)에서만
    ///   부착되고 기상(WakeFromBed)/실내 씬 언로드에서 함께 사라진다. 전용 실내 테스트 씬
    ///   (Test_PlayerCastleInterior)처럼 플래그를 세팅하지 않는 컨텍스트에서도 누움이 유지된다
    ///   (사용자 3차 재보고 — 강제자가 잠자는 원인 후보 차단). 월드 유출 안전: BarrackBed_*는
    ///   실내에만 존재해 월드에서 PlaceAllyOnBed 성립이 없다.
    /// [44차] 외부 이탈 감지 로그 — 누움이 외부 시스템에 의해 깨진 순간 1회 기록(이동 원인 추적용).
    /// </summary>
    public class BedRestEnforcer : MonoBehaviour
    {
        public Vector3 Anchor;
        public Quaternion Pose;

        private bool _violationLogged;

        private void LateUpdate()
        {
            // [44차] 이탈 감지 — 직전 LateUpdate에서 우리가 쓴 값과 다르면 외부 시스템이 덮어쓴 것.
            //   1회 상세 로그(과다 스팸 방지) — 재단언은 매 프레임 계속.
            if (!_violationLogged &&
                (transform.position != Anchor || transform.rotation != Pose))
            {
                _violationLogged = true;
                float dp = Vector3.Distance(transform.position, Anchor);
                float da = Quaternion.Angle(transform.rotation, Pose);
                Debug.Log($"[침대진단] {gameObject.name} 누움 이탈 감지 — posΔ={dp:F2}m rotΔ={da:F1}° (외부 시스템이 루트를 덮어씀 — 매 프레임 재단언으로 누움 유지)");
            }
            transform.position = Anchor;
            transform.rotation = Pose;
        }
    }
}
