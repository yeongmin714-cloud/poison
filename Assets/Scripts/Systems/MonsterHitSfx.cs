using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// [166-D] 몬스터 피격 효과음 — 데미지 실제 적용 지점에서 SFX_Combat_Hit 발화.
    ///  · 훅 위치: PlayerCombat의 target.TakeDamage 직후(AttackTarget 공통 종착 + TryChargeAttack 강공)
    ///    — 스윙별/레거시/콤보 경로가 모두 통과하는 "피격 확정" 종착지(166차 Wave A 계획 D-1).
    ///  · 무기별 pitch/volume 프로파일: AttackSoundLayerManager.ProfileOf의 impact 계열 승계
    ///    (창=고피치 금속성 / 대검=저피치 둔탁 / 활=시위 twang / 맨손=중간피치 — HitStopManager 무기별 차등 선례).
    ///  · "실감" = 랜덤 피치 마이크로베리에이션(±6%, PitchJitterAmplitude) — 같은 무기라도 매 타 음색이 달라진다.
    ///  · 히트스톱 정합: 호출부가 HitStopManager.RequestHitStop 이전에 위치 — I-3 임팩트 선행 규약과 동일.
    ///  · 히든 호스트 + 전용 AudioSource(AttackSoundLayerHost 선례) — 4레이어와 독립(레이어 컷오프 없음), 2D 재생.
    ///  · 클립 부재 시 조용히 무시(사일런트 폴백 — SoundManagerEnhanced/AttackSoundLayerManager 동일 안전 경로).
    /// </summary>
    public static class MonsterHitSfx
    {
        /// <summary>피격 클립 토큰 — Resources/Audio/SFX/SFX_Combat_Hit (프로젝트 내 확보 완료).</summary>
        internal const string ClipToken = "Audio/SFX/SFX_Combat_Hit";
        /// <summary>피치 마이크로베리에이션 진폭(±) — 초기값 0.06(Play 트림 대상).</summary>
        internal const float PitchJitterAmplitude = 0.06f;
        /// <summary>[167-B] 저역 펀치 레이어 피치 — 동일 클립을 0.8 피치로 동시 재생(저역 "쿵" 추가).</summary>
        internal const float LowPunchPitch = 0.8f;
        /// <summary>[167-B] 히트음 증폭 배수 — "더 때리는 듯한" 체감(impactVol 프로파일 상향을 훅 측 일괄 배수로 구현 —
        ///   AttackSoundLayerManager 프로파일 원본 무수정). 신규 에셋 0건 경로(166차 실측: 수용 가능 히트음 오디오 부재).</summary>
        internal const float ImpactVolumeBoost = 1.25f;

        private static MonsterHitSfxHost _host;

        /// <summary>
        /// 몬스터 피격 효과음 발화 — 데미지 적용 확정 직후 1회 호출.
        /// isCrit=true(백어택 등)는 볼륨 1.2배 증폭(PlayAttackHit critVolBoost 선례).
        /// </summary>
        public static void PlayMonsterHit(WeaponType weaponType, bool isCrit = false)
        {
            if (!Application.isPlaying) return;
            var clip = Resources.Load<AudioClip>(ClipToken);
            if (clip == null) return;   // 사일런트 폴백 — 호스트 미생성(부하 0)

            EnsureHost();
            var prof = AttackSoundLayerManager.ProfileOf(weaponType);
            float pitch = prof.impactPitch * (1f + Random.Range(-PitchJitterAmplitude, PitchJitterAmplitude));
            // [167-B] 펀치업 — 본음에 ImpactVolumeBoost 일괄 곱(크리 1.2배 위계는 기존 그대로 보존).
            float volume = prof.impactVol * ImpactVolumeBoost * (isCrit ? 1.2f : 1f);
            _host.PlayOneShot(clip, pitch, volume);
            // [167-B] 저역 펀치 레이어 — 동일 클립 pitch 0.8 동시 재생(저역 "쿵"). ±6% 지터 승계(본음과 독립 롤).
            _host.PlayOneShot(clip,
                Mathf.Clamp(LowPunchPitch * (1f + Random.Range(-PitchJitterAmplitude, PitchJitterAmplitude)), 0.2f, 3f),
                volume);
        }

        private static void EnsureHost()
        {
            if (_host != null) return;
            var go = new GameObject("MonsterHitSfxHost")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            go.SetActive(false);
            _host = go.AddComponent<MonsterHitSfxHost>();
            go.SetActive(true);
        }
    }

    /// <summary>
    /// MonsterHitSfx의 내부 숨은 호스트 — 전용 AudioSource 1개(AttackSoundLayerHost 선례).
    /// </summary>
    internal sealed class MonsterHitSfxHost : MonoBehaviour
    {
        private AudioSource _src;

        public void PlayOneShot(AudioClip clip, float pitch, float volume)
        {
            if (_src == null)
            {
                _src = gameObject.AddComponent<AudioSource>();
                _src.playOnAwake = false;
                _src.loop = false;
                _src.spatialBlend = 0f;   // 2D — 스윙/임팩트 레이어와 동일 규약(타격은 항상 화면 내)
            }
            // 소스 피치를 재생 직전에 세팅해 무기별 피치+랜덤 지터를 정확히 반영
            // (볼륨은 PlayOneShot volumeScale — 소스는 1.0 고정, 제곱 방지).
            _src.pitch = Mathf.Clamp(pitch, 0.2f, 3f);
            _src.volume = 1f;
            _src.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
