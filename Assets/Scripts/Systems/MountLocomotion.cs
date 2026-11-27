using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 85-P5] 탑승 이동 — WASD → 말 이동(카메라 수평 기준), Shift = 질주,
    /// Speed 파라미터 애니 게이트(컨트롤러 전이 임계 0.1/3.0 정합), SurfaceY 계약 접지.
    /// 플레이어는 말에 부착 상태라 카메라 팔로우가 자동 추적(무수정 계약).
    /// </summary>
    public static class MountLocomotion
    {
        public static void Tick(Transform horseRoot, Animator anim, float walkSpeed, float gallopSpeed)
        {
            if (horseRoot == null) return;
            Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            bool moving = input.sqrMagnitude > 0.001f;
            float speed = 0f;

            if (moving)
            {
                Vector3 fwd = Vector3.forward, right = Vector3.right;
                var cam = Camera.main;
                if (cam != null)
                {
                    var cf = cam.transform.forward; cf.y = 0f;
                    var cr = cam.transform.right; cr.y = 0f;
                    if (cf.sqrMagnitude > 0.0001f) fwd = cf.normalized;
                    if (cr.sqrMagnitude > 0.0001f) right = cr.normalized;
                }
                Vector3 dir = (fwd * input.y + right * input.x);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir.Normalize();
                    bool gallop = Input.GetKey(KeyCode.LeftShift);
                    speed = gallop ? gallopSpeed : walkSpeed;
                    horseRoot.position += dir * (speed * Time.deltaTime);
                    horseRoot.rotation = Quaternion.Slerp(horseRoot.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
                }
            }

            // 접지 — 수식 계약(TerrainGenerator) 우선, 실패 시 레이캐스트 폴백(§5 평면 바닥 금지)
            float groundY;
            try
            {
                groundY = TerrainGenerator.GetHeightAt(horseRoot.position.x, horseRoot.position.z, ProjectName.Core.Data.BiomeType.Plains, 42) + 1f;
                if (float.IsNaN(groundY) || float.IsInfinity(groundY)) throw new System.Exception("수식 비유효");
            }
            catch (System.Exception)
            {
                groundY = horseRoot.position.y;
                if (Physics.Raycast(horseRoot.position + Vector3.up * 10f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                    groundY = hit.point.y;
            }
            var p = horseRoot.position;
            horseRoot.position = new Vector3(p.x, groundY, p.z);

            if (anim != null) anim.SetFloat("Speed", speed);
        }
    }
}
