using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// ?�격 ?�???�재 ?�형 ??VFX 분기??
    /// </summary>
    public enum CombatHitType
    {
        /// <summary>몬스??병사/?�주 ???�아?�는 ?�????블러???�플?�터 ?�반.</summary>
        Organic,
        /// <summary>구조�?로봇 ?????�파?�만, 블러???�음.</summary>
        Construct,
        /// <summary>분류 불�? ?�?????�파?�만.</summary>
        None
    }

    /// <summary>
    /// Phase 1: ?�투 ?�격 VFX 중앙 게이??(?�적 ?�래??.
    /// 모든 공격/?�격?� ?�곳 ?�일 진입?�을 거쳐 ?��????�펙???�트�?발사?�다.
    /// ?��??�서 기존 ?�스?�을 ?��??�트?�이?�만 ?�며 ???�티???�진??만들지 ?�는??
    ///   - CombatVFXController (?�파??블러???��?지 ?�자/?�트?�래??
    ///   - HitVFX (캐시???�파??메시 ??치명?� ?�치용)
    ///   - CombatCameraEffects (?�트/?�리???�이??+ ?�트?�톱)
    /// ?�?�양 보호: 초당 최�? MAX_FX_PER_SECOND?�로 ?�출???�한(1�??�도 보충),
    /// ?�산 ?�진 ???�펙???�체�??�킵?�고 [CombatFX] 로그 1??출력.
    /// ?�망 ?�출(PlayKill)?� ?�곳?�서 ?�출?��? ?�는?????�출??책임.
    /// ?�트?�래?�는 GameObject ?�버로드 ?�용 ???�동, ?�치 기반 ?�출 ???�출??책임.
    /// 치명?� 구분 ?�상?� ?�출?��? numberColor??밝�? ???�랑/주황)???�달?�는 방식.
    /// </summary>
    public static class CombatFXGate
    {
        /// <summary>초당 최�? ?�격 FX 발동 ?�수 (?�?�양 ?�산).</summary>
        private const int MAX_FX_PER_SECOND = 12;

        private static float _windowStart = -999f;
        private static int _fxSpentThisWindow;

        // ================================================================
        // ?�치 기반 진입 ???�트?�래?�는 ?�출?��? ?�?�에 직접 ?�용?�야 ??        // ================================================================
        public static void PlayHitFX(Vector3 position, Vector3 direction, CombatHitType type, bool isCrit, float damage, Color numberColor)
        {
            if (!TryConsumeBudget()) return;
            PlayHitFXInternal(position, direction, type, isCrit, damage, numberColor);
        }

        // ================================================================
        // ?�??기반 진입 ???�트?�래???�함
        // ================================================================
        public static void PlayHitFX(GameObject target, Vector3 hitDirection, CombatHitType type, bool isCrit, float damage, Color numberColor)
        {
            if (target == null)
            {
                Debug.LogWarning("[CombatFX] PlayHitFX(target, ...) ??target??null?�어???�킵");
                return;
            }
            if (!TryConsumeBudget()) return;

            CombatVFXController.PlayHitFlash(target);
            PlayHitFXInternal(target.transform.position, hitDirection, type, isCrit, damage, numberColor);
        }

        // ================================================================
        // ?��? 코어 ???�산 ?�비 ???�제 ?�펙??발사
        // ================================================================
        private static void PlayHitFXInternal(Vector3 position, Vector3 direction, CombatHitType type, bool isCrit, float damage, Color numberColor)
        {
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            int damageInt = Mathf.RoundToInt(damage);

            // 1. ?�트 ?�파??????�� 발사 (?�상?� 기존 ?��???고정)
            CombatVFXController.SpawnHitSparks(position);

            // 2. 블러????Organic�?(?�아?�는 ?�?��? 출혈). 치명?�??2?�출�?버스???��?
            if (type == CombatHitType.Organic)
            {
                CombatVFXController.SpawnBloodSplatter(position, dir);
                if (isCrit)
                {
                    CombatVFXController.SpawnBloodSplatter(position + dir * 0.05f, dir);
                    // 캐시???�파??메시 ?�버?�이 (HitVFX, ?��? ?�괴 0.3s) ???�리??강조
                    HitVFX.PlayHitEffect(position, dir);
                }
            }

            // 3. ?��?지 ?�자 ??치명?� ?�기 구분?� ?�출?��? 밝�? numberColor�??�달
            CombatVFXController.ShowDamageNumber(position, damageInt, numberColor);

            // 4. 카메?????�리?��? ?�이??2�?+ ?�트?�톱. PlayKill?� ?�출??책임.
            if (isCrit)
                CombatCameraEffects.PlayCrit();
            else
                CombatCameraEffects.PlayHit();

            Debug.Log($"[CombatFX] crit={(isCrit ? 1 : 0)} type={type} pos={position} damage={damageInt}");
        }

        // ================================================================
        // ?�?�양 ?�산 게이????1�??�도마다 보충 (Time.time 기반, Update 불필??
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
                // ?�도??�??�킵 1?�만 로그 (?�팸 방�?)
                if (_fxSpentThisWindow == MAX_FX_PER_SECOND)
                    Debug.Log($"[CombatFX] FX ?�산 ?�진 ({MAX_FX_PER_SECOND}/s) ???�격 FX ?�킵");
                _fxSpentThisWindow++;
                return false;
            }

            _fxSpentThisWindow++;
            return true;
        }
    }
}
