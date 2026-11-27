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
            // [무번호 35-S2] 히트 실루엣 화이트아웃 — BotW식 윤곽 백색 발광(FxRimOverlay 재사용, 틴트와 이원 계층).
            FxHitSilhouette.Flash(target);
            // [2026-09-15 Phase C] 피격자 반응(플린치/넉백) — 플레이어 제외. 연출 전용, 실패해도 타격/Die 는 계속.
            try
            {
                var sev = isCrit ? HitSeverity.Crit : (damage >= 15f ? HitSeverity.Heavy : HitSeverity.Light);
                HitReactionDriver.Apply(target, hitDirection, sev, false);
            }
            catch (System.Exception e) { Debug.LogWarning($"[CombatFX] 피격 리액션 스킵: {e.GetType().Name}"); }
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
            // [무번호 35-S2] 히트 실루엣 화이트아웃 — 46차 후속(hitPos) 오버로드에도 동일 배선.
            FxHitSilhouette.Flash(target);
            // [2026-09-15 Phase C] 피격자 반응(플린치/넉백) — 플레이어 제외. 연출 전용, 실패해도 타격/Die 는 계속.
            try
            {
                var sevC = isCrit ? HitSeverity.Crit : (damage >= 15f ? HitSeverity.Heavy : HitSeverity.Light);
                HitReactionDriver.Apply(target, hitDirection, sevC, false);
            }
            catch (System.Exception e) { Debug.LogWarning($"[CombatFX] 피격 리액션 스킵: {e.GetType().Name}"); }
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
            // [Phase E-2] 크리티컬은 임팩트 1.5배 확대.
            SlashVFXRunner.PlayImpactMulti(position, type, isCrit ? 1.5f : 1f);

            // [무번호 37-S3] 참격 아크 플립북 — Guz 임팩트 위 보완 레이어(교체 아님 — 계획 계약).
            // [무번호 70-U3] [79-R20] isCrit 전달(파티클 틴트 FXPalette.AttackPrimary 위임 — 공격 이펙트 통일색, 일반/크리 동일색).
            FxSlashFlipbookFlash.Play(position, isCrit ? 1.5f : 1f, isCrit);

            // [무번호 82 사용자 결정] 피격 이펙트 Free Hit Impact 단일화 — "지금처럼 잡다한 것보다 기존의
            //   free hit impact 에셋" 사용자 판정(11-20). 아래 보완 레이어 전부 발화 해제(시스템 본체·파일 유지 —
            //   재배선 대비 보류). 유지: PlayImpactMulti + 참격 아크 + 데미지 숫자 + 카메라/히트스톱 + 사운드 + 극미 플래시.
            // [무번호 71-L2] 디스토션 히트 웨이브 — 플립북 위 보완 레이어(씬 색 리프랙션 링, 크리 1.5배 위계 동일).
            //   런타임 리플렉션 가드 내장(URP opaque 미지원 조용 스킵 — 블랙 링 방어).
            // [무번호 82 보류] FxDistortionWave.Burst(position, isCrit ? 1.5f : 1f);
            // [무번호 82 보류] if (isCrit) FxSecondaryRipple.Schedule(position, 0.55f, 0.12f);   // [무번호 77] 크리 2차 리플 — 1차 웨이브 잔광

            // [무번호 33-A1] [79-R20] 임팩트 실시간 라이트 — 히트 지점 포인트 라이트 펄스(색=FXPalette.AttackPrimary 단일 소스,
            //   공격 이펙트 통일색 — 크리 강도 상향, 동시 상한 3 예산 가드). 첫 동적 광원 계층(33 계획 A).
            FxImpactLight.Spike(position, FXPalette.AttackPrimary, isCrit ? 4.2f : 2.2f, 3.8f, 0.18f);   // [무번호 83 복원] 히트 가독성 — 플래시 수명 0.11s라 임팩트 체감 부족(사용자 11-20 "hit impact 안 붙여졌고") → 라이트만 재활성(빛은 시각 클러터 아님)

            // [무번호 33-B1] 크리 블룸 펄스 — 크리티컬 한정 화면 발광 스파이크(별도 볼륨 — 종료 시 글로벌 회복).
            // [무번호 82 보류] if (isCrit) ScreenBloomFX.Spike(Camera.main, 2.8f, 0.12f);   // [무번호 39-T3] 블룸 앰프 2.2→2.8

            // [무번호 33-D1] 강타격 파편 데브리 — 크리/Heavy(피해 ≥15) 한정 결정론 포물선 파편(예산 가드 내장).
            // [무번호 82 보류] if (isCrit || damage >= 15f) FxDebrisSpawner.Burst(position, damageInt);   // 폭죽형 파편 burst

            // [무번호 37-H3] 지면 크랙 데칼 — 강타격 한정(파편과 동일 게이트), 절차 텍스처·예산 2·2.5s 페이드.
            //   [무번호 82 사용자 확정] "바닥에 남는 금(균열)" = (b) 크랙 데칼 — 제거.
            // [무번호 82 보류] if (isCrit || damage >= 15f) FxGroundCrackFX.Burst(position);

            // [무번호 76-S3] 시트애니 스파크 + 충격 먼지 컬럼 — 강타격 보완 레이어(게이트 밴드 공유 33-D1).
            // [무번호 79 사용자 결정] 시트애니 스파크 배선 해제 — 강타격/치명타 시 노란-주황 시트 스파크가
            //   "몬스터 피격 시 노란색 스파크 연출이 촌스럽다" 사용자 판정(11-19)으로 제거됨.
            //   시스템 본체(FxImpactSheetSpark)는 유지(다른 소비처/재활용 대비) — 게이트 배선만 해제.
            // [무번호 82 보류] if (isCrit || damage >= 15f) FxImpactDustColumn.Spawn(position);
            // [무번호 82 보류] if (isCrit || damage >= 15f) MeteoriteVFXRunner.Burst(position, isCrit);   // [무번호 78-P1] Meteorite 4단계 — 강타격 보완 레이어(계획서 no78 P1) — 폭죽형

            // [무번호 39-T2 → 79 사용자 결정] 히트 방향 스파이크 3본 콘 — "몬스터 피격 시 노란색 스파크 연출이
            //   촌스럽다" 사용자 판정(11-19)으로 제거. SpawnDirectionalSpark 메서드 자체는 패링/애프터라이프
            //   등 다른 소비처가 유지하므로 시그니처 불변(CombatFxMoveRingPhaseFTests 계약).
            // Vector3 sparkDir = direction == Vector3.zero ? Vector3.up : -direction;
            // for (int i = 0; i < 3; i++)
            // {
            //     float yaw = (i - 1) * 25f;
            //     Vector3 d = Quaternion.Euler(0f, yaw, 0f) * sparkDir;
            //     ShockwaveRingFX.SpawnDirectionalSpark(position + Vector3.up * 0.4f, d, FXPalette.AttackPrimary, 0.22f);
            // }

            // 2. 데미지 숫자 — 치명타/백어택 구분: isCrit=true일 때 백어택인지 체크하여 타입 결정
            var dmgType = isCrit
                ? CombatVFXController.DamageNumberType.Critical
                : CombatVFXController.DamageNumberType.Normal;
            // TODO: 백어택 구분 필요 시 isBackAttack 파라미터 추가하여 BackAttack 타입 사용
            CombatVFXController.ShowDamageNumber(position, damageInt, numberColor, dmgType);

            // 3. 카메라 셰이크(히트/크리틱 2종) + 히트스톱. PlayKill은 별도 호출 책임.
            if (isCrit)
            {
                CombatCameraEffects.PlayCrit();
                // [무번호 80-P4] 임팩트 프레임(hit-stop) — 크리 전용. HitStopManager 승계(0.05 스케일 0.06s → 실시간 복귀,
                //   재진입 가드+쿨다운 0.15s+FrozenScaleGuard 내장 — P6 액션감 선례, 젤다식 임팩트 프레임).
                //   일반 히트 미개입(위계 유지 — 계획 Task 4.2), 사망/수집/낚시 등 비전투 경로 영향 0건(전투 게이트 경로만).
                HitStopManager.RequestHitStop(0.06f, 0.05f);
            }
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
                ImpactSoundFX.PlayHit(isCrit, isKill: false, damage);   // [무번호 72-W3] 피해량 전달 — 결정론 피치 밴드 매핑

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
