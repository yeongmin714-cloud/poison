// 2026-10-25 (125차 후속25 R5): 결투 선수 이름표 태그.
// 사용자 계약: "굳이 병사 이름을 다른 식으로 붙이지 말고 기존의 hp바 형태에 이름만 병사 이름으로 변동".
// → 커스텀 TextMesh/마커 폐기, NameplateOverlayUTK의 기존 병사형 이름표(이름행+HP바)를 결투선수에 바인딩.
//   이 컴포넌트는 오버레이 스캔 대상(유닛 식별+HP 공급)만 담당한다.
using UnityEngine;

namespace ProjectName.Systems
{
    public class ArenaDuelFighterTag : MonoBehaviour
    {
        public string DisplayName = "선수";
        public float MaxHP = 100f;
        public float CurrentHP = 100f;

        public bool IsAlive => CurrentHP > 0f;
        public float HpFraction => MaxHP > 0f ? Mathf.Clamp01(CurrentHP / MaxHP) : 0f;

        /// <summary>듀엔 코루틴의 HP(0~maxHP)를 반영 — 결투 라운드마다 호출.</summary>
        public void SetHP(float hp, float maxHP)
        {
            MaxHP = maxHP > 0f ? maxHP : 1f;
            CurrentHP = Mathf.Clamp(hp, 0f, MaxHP);
        }
    }
}
