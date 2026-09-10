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

            // 1. 히트 스파크 발사 (방향은 기존 그대로 고정)
            CombatVFXController.SpawnHitSparks(position);

            // 3단계 이펙트 추가 — 기존 FX 유지+보강: Matthew Guz Impact 피격 VFX
            // (Organic → FX/Impact/BasicHit, Construct → FX/Impact/BasicHit2, 그 외 → BasicHit).
            // Runner 내부에 0.08s 스팸 방지 쿨다운 있음. 예산/단계 로직은 전부 기존 그대로 유지.
            SlashVFXRunner.PlayImpact(position, type);

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

            // 5. 하이스펙 전용 추가 FX (ActionFeel.HighSpec일 때만 실행 — 저사양(Balanced) 기존 동작은 위 1~4 그대로 유지)
            //    PlayHitFX 진입에서 이미 예산(TryConsumeBudget)을 소모했으므로 별도 예산 체크는 불필요.
            if (ActionFeel.HighSpec)
            {
                // 파편 데브리스 3겹 — 하이스펙 파티클 강화 (내부에서 HighSpec 재확인, 일반 중타/크리 모두)
                CombatVFXController.SpawnHitDebris(position, dir, isCrit);

                // 지면 충격파 링: 스팸 방지 게이트 — 치명타 또는 중타 이상(damage >= 40)만 스폰
                // (소프트 그레이즈/경미한 타격은 링 없음. 치명타=주황 강조, 일반 중타=흰색 중립)
                if (isCrit || damage >= 40f)
                {
                    // 치명타: 크고 오래 지속(1.5m, 0.5s) + 주황 / 일반 중타: 작고 짧게(1.0m, 0.35s) + 흰색
                    Color ringColor = isCrit
                        ? ScreenFlashFX.CritColor                       // 주황 (치명타 강조)
                        : new Color(1f, 1f, 1f, 0.6f);                  // 흰색 (일반 중타)
                    ShockwaveRingFX.Spawn(position, isCrit ? 1.5f : 1.0f, ringColor, isCrit ? 0.5f : 0.35f);
                }

                // 화면 플래시: 세척(wash-out) 방지를 위해 값을 최소화.
                // 결정: 치명타 -> 주황 0.3/0.2s (명확한 강조), 일반 타격 -> 극미량 흰색 0.05/0.05s
                // (완전 OFF 대신 거의 보이지 않는 수준만 유지해 타격감 보존. ScreenFlashFX는 단일 인스턴스라
                //  연속 타격 시 덮어써서 스택되지 않음)
                if (isCrit)
                {
                    ScreenFlashFX.FlashOrange(0.3f, 0.2f);
                    // 크리티컬 전용 종합 버스트: 스파크 링 + 데브리스 + 스플래시 일괄
                    CombatVFXController.SpawnCritBurst(position);
                }
                else
                    ScreenFlashFX.FlashWhite(0.05f, 0.05f);

                // 충격 사운드: 하이스펙 전용 레이어드 임팩트음 (ImpactSoundFX.PlayHit).
                // 내부에서도 ActionFeel.HighSpec을 재확인하는 이중 게이트. 클립 에셋이 없어도
                // PlaySFX가 플레이스홀더 로그 후 안전 반환하므로 크래시 없음.
                // 처치(isKill) 사운드는 CombatCameraEffects.PlayKill()을 호출하는 처치 지점에서
                // ImpactSoundFX.PlayHit(false, true)로 별도 재생 (여기서는 타격까지만 담당).
                ImpactSoundFX.PlayHit(isCrit, isKill: false);

                Debug.Log($"[CombatFX-HS] ring+flash crit={(isCrit ? 1 : 0)} damage={damageInt} pos={position}");
            }

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
