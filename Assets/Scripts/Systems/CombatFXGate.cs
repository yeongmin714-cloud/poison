using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 피격 대상 소재 유형 — VFX 분기용.
    /// </summary>
    public enum CombatHitType
    {
        /// <summary>몬스터/병사/영주 등 살아있는 대상 — 블러드 스플래터 동반.</summary>
        Organic,
        /// <summary>구조물/로봇 등 — 스파크만, 블러드 없음.</summary>
        Construct,
        /// <summary>분류 불가 대상 — 스파크만.</summary>
        None
    }

    /// <summary>
    /// Phase 1: 전투 피격 VFX 중앙 게이트 (정적 클래스).
    /// 모든 공격/피격은 이곳 단일 진입점을 거쳐 일관된 이펙트 세트를 발사한다.
    /// 내부에서 기존 시스템을 오케스트레이션만 하며 새 파티클 엔진을 만들지 않는다:
    ///   - CombatVFXController (스파크/블러드/데미지 숫자/히트플래시)
    ///   - HitVFX (캐시된 스파크 메시 — 치명타 펀치용)
    ///   - CombatCameraEffects (히트/크리틱 셰이크 + 히트스톱)
    /// 저사양 보호: 초당 최대 MAX_FX_PER_SECOND회로 호출을 제한(1초 윈도 보충),
    /// 예산 소진 시 이펙트 전체를 스킵하고 [CombatFX] 로그 1회 출력.
    /// 사망 연출(PlayKill)은 이곳에서 호출하지 않는다 — 호출자 책임.
    /// 히트플래시는 GameObject 오버로드 사용 시 자동, 위치 기반 호출 시 호출자 책임.
    /// 치명타 구분 색상은 호출자가 numberColor에 밝은 색(노랑/주황)을 전달하는 방식.
    /// </summary>
    public static class CombatFXGate
    {
        /// <summary>초당 최대 피격 FX 발동 횟수 (저사양 예산).</summary>
        private const int MAX_FX_PER_SECOND = 12;

        private static float _windowStart = -999f;
        private static int _fxSpentThisWindow;

        // ================================================================
        // 위치 기반 진입 — 히트플래시는 호출자가 대상에 직접 적용해야 함
        // ================================================================
        public static void PlayHitFX(Vector3 position, Vector3 direction, CombatHitType type, bool isCrit, float damage, Color numberColor)
        {
            if (!TryConsumeBudget()) return;
            PlayHitFXInternal(position, direction, type, isCrit, damage, numberColor);
        }

        // ================================================================
        // 대상 기반 진입 — 히트플래시 포함
        // ================================================================
        public static void PlayHitFX(GameObject target, Vector3 hitDirection, CombatHitType type, bool isCrit, float damage, Color numberColor)
        {
            if (target == null)
            {
                Debug.LogWarning("[CombatFX] PlayHitFX(target, ...) — target이 null이어서 스킵");
                return;
            }
            if (!TryConsumeBudget()) return;

            CombatVFXController.PlayHitFlash(target);
            PlayHitFXInternal(target.transform.position, hitDirection, type, isCrit, damage, numberColor);
        }

        // ================================================================
        // 내부 코어 — 예산 소비 후 실제 이펙트 발사
        // ================================================================
        private static void PlayHitFXInternal(Vector3 position, Vector3 direction, CombatHitType type, bool isCrit, float damage, Color numberColor)
        {
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            int damageInt = Mathf.RoundToInt(damage);

            // 1. 히트 스파크 — 항상 발사 (색상은 기존 노란색 고정)
            CombatVFXController.SpawnHitSparks(position);

            // 2. 블러드 — Organic만 (살아있는 대상은 출혈). 치명타는 2연출로 버스트 펀치.
            if (type == CombatHitType.Organic)
            {
                CombatVFXController.SpawnBloodSplatter(position, dir);
                if (isCrit)
                {
                    CombatVFXController.SpawnBloodSplatter(position + dir * 0.05f, dir);
                    // 캐시된 스파크 메시 오버레이 (HitVFX, 자가 파괴 0.3s) — 크리틱 강조
                    HitVFX.PlayHitEffect(position, dir);
                }
            }

            // 3. 데미지 숫자 — 치명타 크기 구분은 호출자가 밝은 numberColor로 전달
            CombatVFXController.ShowDamageNumber(position, damageInt, numberColor);

            // 4. 카메라 — 크리틱은 셰이크 2배 + 히트스톱. PlayKill은 호출자 책임.
            if (isCrit)
                CombatCameraEffects.PlayCrit();
            else
                CombatCameraEffects.PlayHit();

            Debug.Log($"[CombatFX] crit={(isCrit ? 1 : 0)} type={type} pos={position} damage={damageInt}");
        }

        // ================================================================
        // 저사양 예산 게이트 — 1초 윈도마다 보충 (Time.time 기반, Update 불필요)
        // ================================================================
        private static bool TryConsumeBudget()
        {
            float now = Time.time;
            if (now - _windowStart >= 1f)
            {
                _windowStart = now;
                _fxSpentThisWindow = 0;
            }

            if (_fxSpentThisWindow >= MAX_FX_PER_SECOND)
            {
                // 윈도당 첫 스킵 1회만 로그 (스팸 방지)
                if (_fxSpentThisWindow == MAX_FX_PER_SECOND)
                    Debug.Log($"[CombatFX] FX 예산 소진 ({MAX_FX_PER_SECOND}/s) — 피격 FX 스킵");
                _fxSpentThisWindow++;
                return false;
            }

            _fxSpentThisWindow++;
            return true;
        }
    }
}
