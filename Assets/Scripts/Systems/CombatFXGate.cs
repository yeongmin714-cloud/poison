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
        // 오브젝트+타격 지점 기반 진입 (46차 후속 — 임팩트를 실제 타격 지점에 부착)
        // ================================================================
        /// <summary>
        /// 46차 후속: 기존 GameObject 오버로드와 동일하되, Internal에 전달하는 발화 위치를
        /// target.transform.position(모델 피벗 — Editor.log 실측 y≈2.9 공중 부양, 실제 타격 지점 아님)
        /// 대신 hitPos(실제 타격 지점)로 전달한다. PlayHitFlash(target)는 유지(오브젝트 플래시는 피벗 무관).
        /// 기존 2개 오버로드는 무수정(하위 호환 유지).
        /// </summary>
        public static void PlayHitFX(GameObject target, Vector3 hitPos, Vector3 hitDirection, CombatHitType type, bool isCrit, float damage, Color numberColor)
        {
            if (target == null)
            {
                Debug.LogWarning("[CombatFX] PlayHitFX(target, hitPos, ...) — target이 null이라 스킵");
                return;
            }
            if (!TryConsumeBudget()) return;

            CombatVFXController.PlayHitFlash(target);
            PlayHitFXInternal(hitPos, hitDirection, type, isCrit, damage, numberColor);
        }

        // ================================================================
        // ?��? 코어 ???�산 ?�비 ???�제 ?�펙??발사
        // ================================================================
        private static void PlayHitFXInternal(Vector3 position, Vector3 direction, CombatHitType type, bool isCrit, float damage, Color numberColor)
        {
            int damageInt = Mathf.RoundToInt(damage);

            // == 46차 정리: 타격 체인 대폭 축소 (사용자 지정 — 에셋 전용 피격 표현) =====================
            // 피격 표현은 Guz 히트 에셋(SlashVFXRunner.PlayImpact)으로 일원화. 유지 항목: PlayImpact /
            // ShowDamageNumber / CombatCameraEffects.PlayHit·PlayCrit(+히트스톱) / HighSpec ImpactSoundFX.PlayHit /
            // ScreenFlashFX.FlashWhite(거의 안 보이는 흰색).
            // 타격 체인에서 제외(호출선만 끊음 — 해당 함수들은 다른 경로 존재 가능으로 유지):
            //   히트 스파크 / 블러드 스플래터 / 크리 전용 피격 구체 /
            //   HighSpec: 파편 데브리스 / 지면 충격파 링(전투 링) / 주황 화면 플래시 / 크리 종합 버스트.
            // direction은 제거된 블러드/데브리스 방향 인자로만 쓰였으므로 현재 미사용(시그니처는 유지).
            // ==========================================================================================

            // 1. 피격 VFX — Matthew Guz Impact 에셋 (유일한 피격 표현). Runner 내부에 0.08s 스팸 방지 쿨다운 있음.
            SlashVFXRunner.PlayImpact(position, type);

            // 2. 데미지 숫자 — 치명타 구분은 호출측이 밝은 numberColor(밝은 노랑/주황)로 전달하는 방식.
            CombatVFXController.ShowDamageNumber(position, damageInt, numberColor);

            // 3. 카메라 셰이크(히트/크리틱 2종) + 히트스톱. PlayKill은 별도 호출 책임.
            if (isCrit)
                CombatCameraEffects.PlayCrit();
            else
                CombatCameraEffects.PlayHit();

            // 4. 하이스펙 전용 레이어 (ActionFeel.HighSpec일 때만 실행 — 저사양(Balanced)은 위 1~3 그대로).
            //    PlayHitFX 진입에서 이미 예산(TryConsumeBudget)을 소모했으므로 별도 예산 체크는 불필요.
            if (ActionFeel.HighSpec)
            {
                // 화면 플래시: 크리 주황 플래시는 46차에서 제외 — 거의 보이지 않는 흰색만 유지해 타격감 보존.
                // (완전 OFF 대신 극미량 유지. ScreenFlashFX는 단일 인스턴스라 연속 타격 시 덮어써서 스택되지 않음)
                ScreenFlashFX.FlashWhite(0.05f, 0.05f);

                // 충격 사운드: 하이스펙 전용 레이어드 임팩트음 (소리는 유지).
                // 내부에서도 ActionFeel.HighSpec을 재확인하는 이중 게이트. 클립 에셋이 없어도
                // PlaySFX가 플레이스홀더 로그 후 안전 반환하므로 크래시 없음.
                // 처치(isKill) 사운드는 CombatCameraEffects.PlayKill()을 호출하는 처치 지점에서
                // ImpactSoundFX.PlayHit(false, true)로 별도 재생 (여기서는 타격까지만 담당).
                ImpactSoundFX.PlayHit(isCrit, isKill: false);

                Debug.Log($"[CombatFX-HS] flash+sound crit={(isCrit ? 1 : 0)} damage={damageInt} pos={position}");
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
