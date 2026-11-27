using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [이동통일 B3] 실내 테스트 씬 전용 재생 범위 클램프 — 구 PlayerCastleInteriorTestMovement의
    /// 전체 발지 클램프 계약 승계. PlayerMovement 교체(B1) 후 벽 콜라이더 부재 씬(성 실내 3종)에서
    /// 성 외부 이탈/공중 낙하를 방지한다. 구 컨트롤러와 동일 순서(이동 후 위치 보정)·동일 계수
    /// (RoomWidth 117.6 × RoomDepth 88.2, 마진 0.35 = CharacterController radius 계약값).
    /// LateUpdate 위치 클램프만 수행 — 결정론, Random 0건.
    /// </summary>
    public sealed class IndoorPlayBoundsClamp : MonoBehaviour
    {
        [SerializeField] private float _halfWidth = PlayerCastleInteriorBuilder.RoomWidth * 0.5f;
        [SerializeField] private float _halfDepth = PlayerCastleInteriorBuilder.RoomDepth * 0.5f;
        [SerializeField] private float _margin = 0.35f;

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            float nx = Mathf.Clamp(p.x, -_halfWidth + _margin, _halfWidth - _margin);
            float nz = Mathf.Clamp(p.z, -_halfDepth + _margin, _halfDepth - _margin);
            if (nx != p.x || nz != p.z)
            {
                p.x = nx;
                p.z = nz;
                transform.position = p;
            }
        }
    }
}
