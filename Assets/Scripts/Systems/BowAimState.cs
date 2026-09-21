using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P25-C — 활 드로/릴리즈 상태 정적 브리지.
    /// Systems(PlayerCombat)는 UI.Toolkit을 참조하지 못하므로(계층 규칙), 드로 상태를
    /// 여기에 기록하고 UI(BowAimReticleUTK/UTKCursorOverlay)가 16ms 폴링으로 소비한다.
    /// UI→Systems 단방향 의존 유지(이벤트/폴링 역전). 정적 클래스는 인스턴스 멤버 불가 —
    /// 릴리즈 이벤트는 1회성 플래그+값 쌍으로 표현한다(BowAimReticleUTK가 소비 후 클리어).
    /// </summary>
    public static class BowAimState
    {
        /// <summary>드로 진행 중(활 장착 + 좌클릭 홀드) — 리티클 표시.</summary>
        public static bool Drawing;
        /// <summary>드로 파워 0~1 — 브래킷 수렴/파워 링용(매 프레임 갱신).</summary>
        public static float Power;

        /// <summary>릴리즈 1회성 이벤트 — 소비 후 ResetRelease()로 클리어.</summary>
        public static bool ReleasePending;
        /// <summary>릴리즈 시 발사 성공 여부(false = 탭 캔슬 → 리티클 즉시 숨김).</summary>
        public static bool ReleaseFired;
        /// <summary>릴리즈 파워 0~1.</summary>
        public static float ReleasePower;

        public static void Begin()
        {
            Drawing = true;
            Power = 0f;
            ReleasePending = false;
        }

        public static void UpdatePower(float p)
        {
            Power = Mathf.Clamp01(p);
        }

        /// <summary>릴리즈 기록. fired=false(탭 캔슬)면 리티클 즉시 숨김.</summary>
        public static void Release(bool fired, float power)
        {
            Drawing = false;
            ReleasePending = true;
            ReleaseFired = fired;
            ReleasePower = Mathf.Clamp01(power);
        }

        public static void ResetRelease()
        {
            ReleasePending = false;
            ReleaseFired = false;
            ReleasePower = 0f;
        }
    }
}