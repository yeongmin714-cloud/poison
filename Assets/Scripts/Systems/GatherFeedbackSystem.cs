using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [133차] 채집 피드백 종류 — 벌목/채광/약초 채집/낚시 입질·투척/물뿌리개/획득 징글.
    /// 클립 프리픽스 매칭(SFX_Gather_Wood 등)과 파티클 프리펩 매핑의 기준이 된다.
    /// </summary>
    public enum GatherFeedbackKind
    {
        Wood,       // [133차] 벌목 타격 — SFX_Gather_Wood_XXX + GatherVFX_WoodChips
        Mine,       // [133차] 채광 타격 — SFX_Gather_Mine_XXX + GatherVFX_StoneFragments
        Herb,       // [133차] 약초 채집 타격 — SFX_Gather_Herb_XXX + GatherVFX_LeafPuff
        Soil,       // [153차 P2] 농경 흙 타격(굉이질/정리/파종) — SFX_Gather_Soil + GatherVFX_SoilBurst
        FishBite,   // [133차] 낚시 입질 — SFX_Fish_Bite_XXX + GatherVFX_FishRipple
        FishCast,   // [133차] 낚시 투척 — SFX_Fish_Cast_XXX + GatherVFX_FishRipple
        WaterPour,  // [133차] 물뿌리개 — SFX_Water_Pour_XXX + GatherVFX_WaterDroplets
        Acquire,    // [133차] 획득 징글 — SFX_Acquire_Common (파티클/셰이크 없음, 징글 전용)
    }

    /// <summary>
    /// [133차] 채집 피드백 공용 시스템(static) — 채집 타격 순간에 사운드+파티클+카메라 흔들림을 한 번에 울린다.
    /// 계획문서 docs/133_IMMERSION_FEEDBACK_PLAN.md §Phase 1(1-B/1-C/1-D/1-E) 사양.
    /// - 사운드: Resources/Sounds/SFX/Gather 의 클립을 프리픽스 매칭으로 캐시 → 종류별 랜덤 variant 재생.
    /// - 오디오: static 지연 생성 5개 AudioSource 풀(3D, minDistance 4 / maxDistance 40) — 씬 오브젝트 의존 없음.
    /// - 파티클: Resources/VFX/Gather/{프리펩} 로드 캐시 → 스폰 후 4초 뒤 파괴. 부재 시 조용히 스킵(에러 로그 1회만).
    /// - 흔들림: Wood/Mine/Herb 타격에만 PlayerMovement.TriggerCameraShake(구르기 선례 수치 준용).
    /// Unity 실행 금지 계약상 컴파일 판정은 부모(부트) 몫 — 본 파일은 순수 런타임 코드만 포함.
    /// </summary>
    public static class GatherFeedbackSystem
    {
        // ── [133차] 상수 — 계획문서 수치 준용 ──
        /// <summary>[133차] 타격류 볼륨.</summary>
        private const float ImpactVolume = 0.85f;
        /// <summary>[133차] 징글 볼륨.</summary>
        private const float JingleVolume = 0.9f;
        /// <summary>[133차] 타격 pitch 랜덤 하한(징글 제외).</summary>
        private const float PitchMin = 0.95f;
        /// <summary>[133차] 타격 pitch 랜덤 상한(징글 제외).</summary>
        private const float PitchMax = 1.05f;
        /// <summary>[133차] 파티클 수명(초) — 자체 파티클 수명보다 길게(계획문서).</summary>
        private const float VfxLifetimeSec = 4f;
        /// <summary>[133차] 카메라 흔들림 지속(초) — PlayerMovement 구르기 선례(0.05, 0.05) 준용.</summary>
        private const float ShakeDurationSec = 0.05f;
        /// <summary>[133차] 카메라 흔들림 강도.</summary>
        private const float ShakeIntensity = 0.05f;
        /// <summary>[133차] 사운드 클립 Resources 루트.</summary>
        private const string ClipsRoot = "Sounds/SFX/Gather";
        /// <summary>[133차] 파티클 프리펩 Resources 루트.</summary>
        private const string VfxRoot = "VFX/Gather";
        /// <summary>[133차] 오디오 풀 크기(계획문서: 5개).</summary>
        private const int PoolSize = 5;

        // ── [133차] 사운드 클립 캐시 — 종류별 variant 목록 ──
        private static readonly Dictionary<GatherFeedbackKind, AudioClip[]> ClipCache
            = new Dictionary<GatherFeedbackKind, AudioClip[]>();
        private static bool _clipsLoaded;

        // ── [133차] 파티클 프리펩 캐시 (부재(null)도 캐시 — Resources 재조회 방지) ──
        private static readonly Dictionary<GatherFeedbackKind, GameObject> VfxCache
            = new Dictionary<GatherFeedbackKind, GameObject>();
        /// <summary>[133차] 프리펩 부재 에러 로그 1회 억제용(프리펩명 기록).</summary>
        private static readonly HashSet<string> VfxMissingLogged = new HashSet<string>();

        // ── [133차] 오디오 풀(지연 생성) + 라운드로빈 커서 ──
        private static AudioSource[] _pool;
        private static int _poolNext;

        /// <summary>[133차] PlayerMovement 캐시 — 카메라 흔들림 호출용(파괴 시 재탐색).</summary>
        private static PlayerMovement _playerMovement;

        // ══════════════════════════ 공개 API ══════════════════════════

        /// <summary>
        /// [133차] 채집 타격 피드백 재생 — 사운드(랜덤 variant)+파티클 스폰+(타격류 한정)카메라 흔들림.
        /// Acquire 종류는 징글 전용(파티클/셰이크 없음) → PlayAcquireJingle로 위임.
        /// </summary>
        /// <param name="kind">피드백 종류.</param>
        /// <param name="position">월드 재생 위치(노드/플롯 기준).</param>
        /// <param name="shake">true여도 Wood/Mine/Herb가 아니면 흔들리지 않음.</param>
        /// <param name="intensity">[무번호 74-G3] 파티클 스폰 배율(기본 1f — 기존 호출부 전부 무영향). 완료 파편 폭산은 1.6f.</param>
        public static void PlayImpact(GatherFeedbackKind kind, Vector3 position, bool shake = true, float intensity = 1f)
        {
            // [133차] Acquire는 징글 전용 경로 — 파티클/셰이크 없음(계획문서).
            if (kind == GatherFeedbackKind.Acquire)
            {
                PlayAcquireJingle(0);
                return;
            }

            // ① 사운드 — 종류별 랜덤 variant + 타격 pitch 랜덤 0.95~1.05, 볼륨 0.85.
            AudioClip clip = PickClip(kind);
            if (clip != null)
                PlayAt(clip, position, threeD: true, pitch: UnityEngine.Random.Range(PitchMin, PitchMax), volume: ImpactVolume);
            // 클립 부재는 조용히 스킵(에셋 미배치 환경 안전).

            // ③ 파티클 프리펩 스폰 — 부재 시 조용히 스킵(에러 로그 1회만).
            SpawnVfx(kind, position, intensity);

            // ④ 카메라 흔들림 — shake=true && Wood/Mine/Herb일 때만(계획문서).
            if (shake && IsImpactKind(kind))
                TriggerShake();
        }

        /// <summary>
        /// [133차] 획득 징글 — SFX_Acquire_Common 1회 재생. rarityTier(0=일반, 1=희귀, 2=영웅 이상)에 따라
        /// pitch 1.0 / 1.08 / 1.18 변주. 반2D(spatialBlend 0.5)로 UI 사운드처럼 카메라 위치에서 울린다.
        /// </summary>
        public static void PlayAcquireJingle(int rarityTier)
        {
            EnsureClipsLoaded();
            if (ClipCache.TryGetValue(GatherFeedbackKind.Acquire, out AudioClip[] variants)
                && variants != null && variants.Length > 0)
            {
                AudioClip clip = variants[UnityEngine.Random.Range(0, variants.Length)];
                // [133차] 티어별 pitch 변주 — 0=일반 1.0 / 1=희귀 1.08 / 2+=영웅 이상 1.18.
                float pitch = rarityTier >= 2 ? 1.18f : rarityTier == 1 ? 1.08f : 1.0f;
                // [133차] UI 사운드처럼 카메라 위치에서 반2D 재생.
                Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                PlayAt(clip, pos, threeD: false, pitch: pitch, volume: JingleVolume);
            }
            // 클립 부재는 조용히 스킵.
        }

        // ══════════════════════════ 내부 구현 ══════════════════════════

        /// <summary>[133차] 타격류(Wood/Mine/Herb) 여부 — 카메라 흔들림 게이트.</summary>
        private static bool IsImpactKind(GatherFeedbackKind kind)
        {
            return kind == GatherFeedbackKind.Wood
                || kind == GatherFeedbackKind.Mine
                || kind == GatherFeedbackKind.Herb;
        }

        /// <summary>[133차] 종류별 클립 프리픽스 — Assets/Resources/Sounds/SFX/Gather 실측 파일명과 일치.</summary>
        private static string ClipPrefix(GatherFeedbackKind kind)
        {
            switch (kind)
            {
                case GatherFeedbackKind.Wood:      return "SFX_Gather_Wood";
                case GatherFeedbackKind.Mine:      return "SFX_Gather_Mine";
                case GatherFeedbackKind.Herb:      return "SFX_Gather_Herb";
                case GatherFeedbackKind.Soil:      return "SFX_Gather_Soil";   // [153차 P2] 농경 흙 타격
                case GatherFeedbackKind.FishBite:  return "SFX_Fish_Bite";
                case GatherFeedbackKind.FishCast:  return "SFX_Fish_Cast";
                case GatherFeedbackKind.WaterPour: return "SFX_Water_Pour";
                case GatherFeedbackKind.Acquire:   return "SFX_Acquire_Common";
                default: return null;
            }
        }

        /// <summary>[133차] 종류별 파티클 프리펩명 — Resources/VFX/Gather/{명} (프리펩 생성은 별도 에디터 스크립트 담당).</summary>
        private static string VfxPrefabName(GatherFeedbackKind kind)
        {
            switch (kind)
            {
                case GatherFeedbackKind.Wood:      return "GatherVFX_WoodChips";
                case GatherFeedbackKind.Mine:      return "GatherVFX_StoneFragments";
                case GatherFeedbackKind.Herb:      return "GatherVFX_LeafPuff";
                case GatherFeedbackKind.Soil:      return "GatherVFX_SoilBurst";   // [153차 P2] 농경 흙 타격(프리펩은 에디터 빌더 담당)
                case GatherFeedbackKind.WaterPour: return "GatherVFX_WaterDroplets";
                case GatherFeedbackKind.FishBite:
                case GatherFeedbackKind.FishCast:  return "GatherVFX_FishRipple";
                default: return null;   // [133차] Acquire — 징글 전용(파티클 없음).
            }
        }

        /// <summary>[133차] 클립 캐시 적재 — LoadAll 1회 후 프리픽스 매칭으로 종류별 variant 목록 구성.</summary>
        private static void EnsureClipsLoaded()
        {
            if (_clipsLoaded) return;
            _clipsLoaded = true;

            AudioClip[] all = Resources.LoadAll<AudioClip>(ClipsRoot);
            foreach (GatherFeedbackKind kind in Enum.GetValues(typeof(GatherFeedbackKind)))
            {
                string prefix = ClipPrefix(kind);
                if (prefix == null)
                {
                    ClipCache[kind] = new AudioClip[0];
                    continue;
                }
                var matches = new List<AudioClip>();
                if (all != null)
                {
                    foreach (AudioClip c in all)
                    {
                        if (c != null && c.name.StartsWith(prefix, StringComparison.Ordinal))
                            matches.Add(c);
                    }
                }
                ClipCache[kind] = matches.ToArray();
            }
        }

        /// <summary>[133차] 종류별 랜덤 variant 클립 반환 — 부재 시 null(호출부가 조용히 스킵).</summary>
        private static AudioClip PickClip(GatherFeedbackKind kind)
        {
            EnsureClipsLoaded();
            if (ClipCache.TryGetValue(kind, out AudioClip[] variants)
                && variants != null && variants.Length > 0)
                return variants[UnityEngine.Random.Range(0, variants.Length)];
            return null;
        }

        /// <summary>
        /// [133차] 풀에서 AudioSource를 골라 1샷 재생. threeD=true → spatialBlend 1(3D, min 4/max 40),
        /// false → spatialBlend 0.5(반2D — UI/징글). 전 슬롯 재생 중이면 라운드로빈으로 가장 오래된 슬롯 재사용.
        /// </summary>
        private static void PlayAt(AudioClip clip, Vector3 position, bool threeD, float pitch, float volume)
        {
            EnsurePool();
            if (_pool == null || clip == null) return;

            AudioSource src = null;
            for (int i = 0; i < PoolSize; i++)
            {
                int idx = (_poolNext + i) % PoolSize;
                if (_pool[idx] != null && !_pool[idx].isPlaying)
                {
                    src = _pool[idx];
                    _poolNext = (idx + 1) % PoolSize;
                    break;
                }
            }
            if (src == null)
            {
                // [133차] 풀 전부 재생 중 — 라운드로빈 슬롯 강제 재사용(유실 감수, 스팸 방지 목적).
                src = _pool[_poolNext];
                _poolNext = (_poolNext + 1) % PoolSize;
            }
            if (src == null) return;

            src.transform.position = position;
            src.spatialBlend = threeD ? 1f : 0.5f;
            src.pitch = pitch;
            src.volume = volume;
            src.clip = clip;
            src.Play();
        }

        /// <summary>[133차] 오디오 풀 지연 생성 — DontDestroyOnLoad 호스트 GameObject에 5개 AudioSource 부착.</summary>
        private static void EnsurePool()
        {
            if (_pool != null && _pool.Length == PoolSize && _pool[0] != null) return;

            var host = new GameObject("[133차_GatherFeedbackAudioPool]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource src = host.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;              // [133차] 3D 기본(징글은 재생 시 0.5로 덮어씀).
                src.minDistance = 4f;               // [133차] 계획문서: minDistance 4.
                src.maxDistance = 40f;              // [133차] 계획문서: maxDistance 40.
                src.rolloffMode = AudioRolloffMode.Logarithmic;   // [133차] Unity 기본값 명시(rolloff 기본 유지).
                _pool[i] = src;
            }
        }

        /// <summary>[133차] 파티클 프리펩 스폰 — 로드 캐시 후 Instantiate, 4초 뒤 파괴. 부재 시 조용히 스킵(에러 로그 1회만).</summary>
        private static void SpawnVfx(GatherFeedbackKind kind, Vector3 position, float intensity)
        {
            string prefabName = VfxPrefabName(kind);
            if (string.IsNullOrEmpty(prefabName)) return;   // 징글 등 파티클 없는 종류.

            if (!VfxCache.TryGetValue(kind, out GameObject prefab))
            {
                prefab = Resources.Load<GameObject>($"{VfxRoot}/{prefabName}");
                VfxCache[kind] = prefab;   // [133차] 부재(null)도 캐시 — Resources 재조회 방지.
                if (prefab == null)
                {
                    // [133차] 프리펩 부재 — 조용히 스킵하되 에러 로그는 프리펩당 1회만(프리펩 생성은 별도 에디터 스크립트 담당).
                    if (VfxMissingLogged.Add(prefabName))
                        Debug.LogError($"[GatherFeedbackSystem] VFX 프리펩 미발견: Resources/{VfxRoot}/{prefabName} — 파티클 스킵(사운드는 정상 재생)");
                    return;
                }
            }
            if (prefab == null) return;   // [133차] 이전 조회에서 부재 확정 — 조용히 스킵.

            GameObject go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                // [무번호 74-G3] 완료 파편 폭산 — 스폰 인스턴스 한정 배율(에셋 무변경 계약, 프리펩 클론 회피).
                //   버스트 수×intensity(수 1.6배 등가) + 파편 크기·속도 배율(드리프트 확대). 1f면 무변경.
                var main = ps.main;
                main.startSizeMultiplier *= intensity;
                main.startSpeedMultiplier *= intensity;
                var emission = ps.emission;
                int burstCount = emission.burstCount;
                if (burstCount > 0)
                {
                    var bursts = new ParticleSystem.Burst[burstCount];
                    emission.GetBursts(bursts);
                    for (int i = 0; i < bursts.Length; i++)
                    {
                        if (bursts[i].count.mode == ParticleSystemCurveMode.Constant)
                            bursts[i].count = new ParticleSystem.MinMaxCurve(bursts[i].count.constant * intensity);
                    }
                    emission.SetBursts(bursts);
                }

                ps.Play();   // [133차] 빌더가 playOnAwake=false로 저장 — 명시 발화 계약(교차 브랜치 정합).
            }
            UnityEngine.Object.Destroy(go, VfxLifetimeSec);   // [133차] 자체 파티클 수명보다 긴 안전 상한.
        }

        /// <summary>[133차] 카메라 흔들림 — PlayerMovement.TriggerCameraShake(작업3에서 public 전환) 호출.</summary>
        private static void TriggerShake()
        {
            if (_playerMovement == null)
                _playerMovement = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            if (_playerMovement == null) return;
            _playerMovement.TriggerCameraShake(ShakeDurationSec, ShakeIntensity);
        }
    }
}
