using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [156차 P2-3] 동굴 입구 문 — 상시 봉합.
    /// [156차 명세 변경] [143차 P8] "기본 열림(진입 흐름 방해 금지)" 계약 폐기 — 사유: 156차 사용자 지시(동굴 낙하 금지).
    /// 156차 P0 실측 원인: 문이 열린 채 시작되어 CaveDoorBlocker가 비활성 상태로 존재 → 입구 개방 통과 →
    /// 실내 씬 지면 부재 낙하. 따라서 블로커를 상시 enabled=true로 고정하고 E 토글 상호작용을 폐기한다
    /// (Update 재단성 — 외부 경로로 비활성화돼도 매 프레임 즉시 복원, 블로커 비활성화 경로 0개).
    /// 귀환은 기존 상승 포탈(E키, CaveLevelPortal) 경로로 불변 — 본 컴포넌트와 무관.
    /// 패널(_panelL/_panelR)은 [156차] 빌더가 생성 폐기 — 참조가 null이어도 NRE 없이 동작(방어 가드 유지,
    /// 존재 시 닫힘 위치 스냅만 수행). 콜라이더는 CaveDoorBlocker 1개뿐 계약 유지 — 패널은 전부 장식(콜라이더 없음).
    /// </summary>
    public class CaveEntranceDoor : MonoBehaviour
    {
        private const float ClosedOffsetX = 0.78f;   // 구 빌더 패널 배치 X — 패널 존재 시 닫힘 기준점

        private Transform _panelL;
        private Transform _panelR;
        private Collider _blocker;

        private void Awake()
        {
            _panelL = transform.Find("CaveDoorPanel_L");
            _panelR = transform.Find("CaveDoorPanel_R");
            Transform blockerGo = transform.Find("CaveDoorBlocker");
            if (blockerGo != null) _blocker = blockerGo.GetComponent<Collider>();
            ApplySealed();   // 상시 봉합 — 생성 직후부터 봉합 상태 스냅
        }

        private void Update()
        {
            ApplySealed();   // [156차] 상시 봉합 재단성 — 블로커 비활성화 시도 즉시 복원
        }

        /// <summary>상시 봉합 적용 — 블로커 enabled=true 강제 + 패널(존재 시) 닫힘 위치 스냅(전 항목 null 가드).</summary>
        private void ApplySealed()
        {
            if (_blocker != null) _blocker.enabled = true;
            if (_panelL != null)
                _panelL.localPosition = new Vector3(+ClosedOffsetX, _panelL.localPosition.y, _panelL.localPosition.z);
            if (_panelR != null)
                _panelR.localPosition = new Vector3(-ClosedOffsetX, _panelR.localPosition.y, _panelR.localPosition.z);
        }
    }
}
