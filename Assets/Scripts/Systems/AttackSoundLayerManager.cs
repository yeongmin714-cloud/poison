using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase I: 공격 사운드 4레이어 동기 매니저.
    /// 스윙(Swing) / 임팩트(Impact) / 서브베이스(SubBass) / 보이스(Voice) 4개 레이어를
    /// 무기 타입별 피치·볼륨·활성 여부로 차별화해, 각 레이어가 독립 AudioSource로 재생된다.
    ///
    /// - I-1 동기: 스윙은 공격 시작 시점(스윙 레이어), 임팩트/서브베이스/보이스는 타격(hit) 시점에
    ///   별도 호출로 재생되어 '시점 분리'를 보장한다.
    /// - I-2 무기별 차별: 창=금속성 고피치, 검(대검)=둔탁 저피치, 활=시위 twang 고피치,
    ///   맨손=경쾌 중간피치. WeaponProfile 테이블로 정합.
    /// - I-3 선행: PlayAttackHit는 즉시(코루틴/딜레이 없이) 3중 레이어를 동시 발화하여 히트스톱
    ///   (HitStopManager) 지연과 무관하게 임팩트가 먼저 들린다. 호출부는 PlayerCombat에서
    ///   히트스톱 요청 이전에 위치하므로 충족.
    ///
    /// 클립 부재 시 조용히 무시(SoundManagerEnhanced.PlaySFX와 동일 안전 경로). 서브베이스는
    /// 리소스 클립이 없으면 저주파 절차적 클립을 즉석 생성해 어느 경우에도 레이어가 보장된다.
    /// 정적 프런트 + 내부 숨은 호스트(레이어별 AudioSource 4개) — SlashVFXRunner의 SlashFxHost 선례.
    /// </summary>
    public static class AttackSoundLayerManager
    {
        // ── 재생 토큰 — Resources/Sounds/SFX/{token} 로 로드 (기존 attack_swing/attack_hit 재활용) ──
        /// <summary>무기별 전용 스윙 클립 (예: attack_swing_spear), 없으면 기본 attack_swing 폴백.</summary>
        private const string BaseSwingToken = "attack_swing";
        /// <summary>무기별 전용 임팩트 클립 (예: attack_hit_spear), 없으면 기본 attack_hit 폴백.</summary>
        private const string BaseHitToken = "attack_hit";
        /// <summary>서브베이스 공통 클립 (없으면 절차적 저주파 생성).</summary>
        private const string SubBassToken = "attack_sub";
        /// <summary>보이스(전투 기합) 공통 클립.</summary>
        private const string VoiceToken = "attack_voice";

        // ── 레이어 종류 ──
        private enum Layer { Swing, Impact, SubBass, Voice }

        /// <summary>무기별 피치/볼륨/레이어 프로필.</summary>
        public struct WeaponSoundProfile
        {
            public float swingPitch, swingVol;
            public float impactPitch, impactVol;
            public float subPitch, subVol;
            public float voicePitch, voiceVol;
            public bool  voiceEnabled;
        }

        /// <summary>
        /// 무기 타입별 기본 프로필 테이블 (Phase I).
        /// 창=금속성 고피치 / 검(대검)=둔탁 저피치 / 활=시위 twang / 맨손=경쾌 중간.
        /// </summary>
        public static WeaponSoundProfile ProfileOf(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Spear:
                    // 창 — 날카롭고 높은 금속성. 서브베이스는 얕게, 보이스는 짧게.
                    return new WeaponSoundProfile
                    {
                        swingPitch = 1.35f, swingVol = 0.95f,
                        impactPitch = 1.25f, impactVol = 1.0f,
                        subPitch = 0.80f, subVol = 0.35f,
                        voicePitch = 1.15f, voiceVol = 0.55f,
                        voiceEnabled = true
                    };
                case WeaponType.Sword:
                    // 검(대검) — 묵직하고 둔탁한 저피치. 서브베이스/보이스가 두껍게.
                    return new WeaponSoundProfile
                    {
                        swingPitch = 0.85f, swingVol = 1.0f,
                        impactPitch = 0.90f, impactVol = 1.0f,
                        subPitch = 0.60f, subVol = 0.70f,
                        voicePitch = 0.85f, voiceVol = 0.80f,
                        voiceEnabled = true
                    };
                case WeaponType.Bow:
                    // 활 — 시위 twang: 매우 높은 스윙/임팩트 피치, 서브베이스 최소화, 보이스 없음.
                    return new WeaponSoundProfile
                    {
                        swingPitch = 1.60f, swingVol = 0.90f,
                        impactPitch = 1.45f, impactVol = 0.70f,
                        subPitch = 0.75f, subVol = 0.20f,
                        voicePitch = 1.0f, voiceVol = 0.0f,
                        voiceEnabled = false
                    };
                default: // Fist 및 미등록
                    // 맨손 — 경쾌한 중간 피치, 가벼운 기합.
                    return new WeaponSoundProfile
                    {
                        swingPitch = 1.10f, swingVol = 0.90f,
                        impactPitch = 1.15f, impactVol = 1.0f,
                        subPitch = 0.85f, subVol = 0.30f,
                        voicePitch = 1.0f, voiceVol = 0.50f,
                        voiceEnabled = true
                    };
            }
        }

        // ── 내부 호스트 ──
        private static AttackSoundLayerHost _host;

        // ================================================================
        //  공개 API
        // ================================================================

        /// <summary>
        /// 스윙 레이어 재생 — 공격 시작 시점에서 호출. 무기별 피치/볼륨 적용.
        /// 전용 클립(attack_swing_{weapon}) 우선, 기본(attack_swing) 폴백, 둘 다 없으면 조용히 무시.
        /// </summary>
        public static void PlaySwing(WeaponType weaponType)
        {
            if (!Application.isPlaying) return;
            var prof = ProfileOf(weaponType);
            EnsureHost();

            string weaponSuffix = WeaponSuffix(weaponType);

            // 전용 클립 우선 → 기본 폴백
            AudioClip clip = LoadLayerClip(Layer.Swing, weaponSuffix);
            if (clip != null)
            {
                _host.PlayWithPitch((int)Layer.Swing, clip, prof.swingPitch, prof.swingVol);
            }
            else
            {
                // 클립 부재 — 사일런트 (기존 SoundManager 배후 동작 유지)
                Debug.Log($"[AttackSoundLayerManager] 🗡️ 스윙 요청: type={weaponType} (clip 없음 — 사일런트)");
            }
        }

        /// <summary>
        /// 타격 3중 레이어(임팩트 + 서브베이스 + 보이스) 동시 재생 — 히트 지점에서 즉시 호출.
        /// 코루틴/딜레이 없이 즉시 발화하므로 히트스톱(Time.timeScale 감속)과 무관하게 선행되어 들린다.
        /// </summary>
        /// <param name="weaponType">무기 타입 (피치/레이어 프로필)</param>
        /// <param name="isCrit">치명타/백어택 여부 — 임팩트 피치·볼륨 소폭 증폭</param>
        public static void PlayAttackHit(WeaponType weaponType, bool isCrit)
        {
            if (!Application.isPlaying) return;
            EnsureHost();

            var prof = ProfileOf(weaponType);
            float critPitchBoost = isCrit ? 1.08f : 1f;
            float critVolBoost = isCrit ? 1.2f : 1f;
            string weaponSuffix = WeaponSuffix(weaponType);

            // ① 임팩트 (메인) — 전용 clip 우선, 기본 폴백
            AudioClip impactClip = LoadLayerClip(Layer.Impact, weaponSuffix);
            if (impactClip != null)
            {
                _host.PlayWithPitch((int)Layer.Impact, impactClip,
                    Mathf.Clamp(prof.impactPitch * critPitchBoost, 0.3f, 3f),
                    Mathf.Clamp01(prof.impactVol * critVolBoost));
            }
            else
            {
                Debug.Log($"[AttackSoundLayerManager] 💥 임팩트 요청: type={weaponType} (clip 없음 — 사일런트)");
            }

            // ② 서브베이스 (두께감) — 리소스 없으면 절차적 저주파 생성(보장 레이어)
            AudioClip subClip = LoadLayerClip(Layer.SubBass, weaponSuffix);
            if (subClip == null) subClip = GenerateSubBassClip(prof.subPitch);
            if (subClip != null)
            {
                _host.PlayWithPitch((int)Layer.SubBass, subClip, prof.subPitch, prof.subVol);
            }

            // ③ 보이스 (전투 기합) — 무기 프로필이 활성일 때만
            if (prof.voiceEnabled)
            {
                AudioClip voiceClip = LoadLayerClip(Layer.Voice, weaponSuffix);
                if (voiceClip != null)
                {
                    _host.PlayWithPitch((int)Layer.Voice, voiceClip, prof.voicePitch, prof.voiceVol);
                }
            }
        }

        /// <summary>치명타 전용 단일 임팩트 재생 (외부 특수 상황용 — 현재 미사용).</summary>
        public static void PlayImpactCrit(WeaponType weaponType)
        {
            // PlayerCombat 경로는 PlayAttackHit(isCrit=true)로 처리하므로 그대로 위임.
            PlayAttackHit(weaponType, true);
        }

        // ================================================================
        //  내부 헬퍼
        // ================================================================

        private static string WeaponSuffix(WeaponType type)
        {
            return type switch
            {
                WeaponType.Sword => "_sword",
                WeaponType.Spear => "_spear",
                WeaponType.Bow   => "_bow",
                WeaponType.Fist  => "_fist",
                _ => ""
            };
        }

        /// <summary>
        /// 레이어별 클립 로드: 스윙/임팩트는 무기 전용 토큰 → 기본 폴백 순.
        /// 서브베이스/보이스는 공통 토큰(+무기 접미 시도) → null 반환.
        /// </summary>
        private static AudioClip LoadLayerClip(Layer layer, string weaponSuffix)
        {
            // 1차: 무기 전용 토큰
            string specificToken = null;
            switch (layer)
            {
                case Layer.Swing:   specificToken = BaseSwingToken + weaponSuffix; break;
                case Layer.Impact:  specificToken = BaseHitToken + weaponSuffix;  break;
                case Layer.SubBass: specificToken = SubBassToken + weaponSuffix;  break;
                case Layer.Voice:   specificToken = VoiceToken + weaponSuffix;    break;
            }
            if (!string.IsNullOrEmpty(specificToken))
            {
                var specific = Resources.Load<AudioClip>($"Sounds/SFX/{specificToken}");
                if (specific != null) return specific;
            }

            // 2차: 기본 토큰 (스윙/임팩트만 기본 폴백 존재)
            string baseToken = null;
            switch (layer)
            {
                case Layer.Swing:  baseToken = BaseSwingToken; break;
                case Layer.Impact: baseToken = BaseHitToken;   break;
            }
            if (baseToken != null)
                return Resources.Load<AudioClip>($"Sounds/SFX/{baseToken}");

            return null;
        }

        /// <summary>
        /// 서브베이스 레이어 절차적 저주파 클립 생성(캐싱) — 리소스 클립이 없어도
        /// 실제로 두께감이 들리는 안전 보장. 프로필 피치에 근접한 70Hz 베이스 + 감쇠.
        /// </summary>
        private static AudioClip _cachedSubClip;
        private static AudioClip GenerateSubBassClip(float pitch)
        {
            if (_cachedSubClip != null) return _cachedSubClip;

            const int sampleRate = 44100;
            float freq = 70f;   // 기본 저주파 (서브베이스). pitch 조정은 재생 단계에서 AudioSource로 적용.
            float duration = 0.22f;
            int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            var clip = AudioClip.Create("procedural_attack_sub", samples, 1, sampleRate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // 사인(sine) + 하모닉 소량 — 부드러운 저주파 "쿵"
                float env = 1f - Mathf.Clamp01((float)i / sampleRate / duration); // 선형 감쇠
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f
                        + Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.3f;
                data[i] = s * env;
            }
            clip.SetData(data, 0);
            _cachedSubClip = clip;
            return clip;
        }

        private static void EnsureHost()
        {
            if (_host != null) return;
            var go = new GameObject("AttackSoundLayerHost")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            go.SetActive(false);
            _host = go.AddComponent<AttackSoundLayerHost>();
            go.SetActive(true);
        }
    }

    /// <summary>
    /// AttackSoundLayerManager의 내부 숨은 호스트 — 레이어별 독립 AudioSource 4개를 보유.
    /// 각 레이어가 서로 다른 피치로 겹쳐 재생되어 동기적 4레이어를 가능하게 한다.
    /// </summary>
    internal sealed class AttackSoundLayerHost : MonoBehaviour
    {
        private AudioSource[] _sources = new AudioSource[4];

        public void PlayWithPitch(int layerIndex, AudioClip clip,
            float pitch, float volume)
        {
            // Layer enum은 외부(프런트)에서 (int)로 캐스팅해 전달 — 어셈블리 내 계약으로 정합.
            if (layerIndex < 0 || layerIndex >= _sources.Length) return;
            var src = _sources[layerIndex];
            if (src == null) src = _sources[layerIndex] = CreateLayerSource(layerIndex);
            if (src == null) return;

            // 레이어 소스는 1:1 전용이라 동시에 여러 클립이 겹치지 않으므로,
            // 소스 피치를 재생 직전에 세팅해 무기별 피치를 정확히 반영한다.
            // 볼륨은 PlayOneShot의 volumeScale로만 선형 제어(소스는 1.0 고정 — 제곱 방지).
            src.pitch = Mathf.Clamp(pitch, 0.2f, 3f);
            src.volume = 1f;
            src.spatialBlend = 0f; // 2D 공격 SFX
            src.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private AudioSource CreateLayerSource(int index)
        {
            var go = new GameObject($"Layer_{index}_{LayerName(index)}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            return src;
        }

        private static string LayerName(int index)
        {
            switch (index)
            {
                case 0: return "Swing";
                case 1: return "Impact";
                case 2: return "SubBass";
                case 3: return "Voice";
                default: return "Unknown";
            }
        }
    }
}