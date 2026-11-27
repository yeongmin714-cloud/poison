using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-19: 효과음(SFX) 관리자 싱글톤.
    /// AudioSource 풀을 관리하여 동시에 여러 효과음을 재생할 수 있습니다.
    /// DontDestroyOnLoad로 씬 전환 시 유지됩니다.
    /// 현재는 플레이스홀더 단계 — 실제 오디오 파일 없이 Debug.Log로 대체됩니다.
    /// </summary>
    public class SoundEffectManager : MonoBehaviour
    {
        /// <summary>
        /// 사용 가능한 효과음 종류.
        /// </summary>
        public enum SFXType
        {
            /// <summary>발소리</summary>
            Footstep,

            /// <summary>자원 수집</summary>
            Gather,

            /// <summary>제작</summary>
            Craft,

            /// <summary>전투 — 피격</summary>
            Combat_Hit,

            /// <summary>전투 — 휘두름</summary>
            Combat_Swing,

            /// <summary>암살</summary>
            Assassination,

            /// <summary>문 열림</summary>
            DoorOpen,

            /// <summary>문 닫힘</summary>
            DoorClose,

            /// <summary>아이템 획득</summary>
            ItemPickup,

            /// <summary>아이템 버림</summary>
            ItemDrop,

            /// <summary>경보</summary>
            Alarm,

            /// <summary>승리</summary>
            Victory,

            /// <summary>패배</summary>
            Defeat,

            /// <summary>자동 이동 시작</summary>
            AutoMove_Start,

            /// <summary>자동 이동 완료 (도착)</summary>
            AutoMove_Complete,

            /// <summary>자동 이동 취소</summary>
            AutoMove_Cancel,

            /// <summary>[95차 후속 SFX] 패링 PERFECT — 밝은 금속 clang(딩!)</summary>
            Parry_Perfect,

            /// <summary>[95차 후속 SFX] 패링 GOOD — 무디어 clang</summary>
            Parry_Good,

            /// <summary>[무번호 62] 패링 실패/빈 — 최소 신호음(낮은 짧은 음. 36-PW2 의사결정 갱신 — 성공 위계와 대비)</summary>
            Parry_Fail,

            /// <summary>[무번호 16-B4] 텔레그래프 경고 — 저음 웅성(공격 예고 시각+청각 이중 예고)</summary>
            Telegraph_Warning,

            /// <summary>[무번호 19-E2] 몬스터 발견 — 저음 웅성+짧은 경계음(Alert 진입 시각+청각 이중 예고)</summary>
            Monster_Alert
        }

        // ===== 싱글톤 =====

        private static SoundEffectManager _instance;
        private static bool _instanceQuitting = false;

        /// <summary>SoundEffectManager 싱글톤 인스턴스</summary>
        public static SoundEffectManager Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("SoundEffectManager");
                    _instance = go.AddComponent<SoundEffectManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ===== 설정 =====

        [Header("SFX 설정")]

        [SerializeField, Range(0f, 1f)]
        [Tooltip("전체 SFX 볼륨 (0=음소거, 1=최대)")]
        private float _volume = 1.0f;

        [SerializeField]
        [Tooltip("AudioSource 풀 크기 (동시 재생 가능한 효과음 수)")]
        private int _poolSize = 12;

        // ===== 상태 =====

        private List<AudioSource> _sourcePool;
        private int _nextSourceIndex;

        // ===== [95차 후속 SFX] 실오디오 클립 캐시 =====
        /// <summary>SFXType → AudioClip 캐시(1회 로드 — Resources.Load 반복 호출 방지). 인스턴스 소멸 시 함께 해제.</summary>
        private readonly Dictionary<SFXType, AudioClip> _clipCache = new Dictionary<SFXType, AudioClip>();

        // ===== [133차-P2] 표면 변형 클립 캐시 =====
        /// <summary>클립 이름 → AudioClip 캐시(발소리 표면 변형 전용). 부재 결과(null)도 캐시하여 반복 로드 시도 방지.</summary>
        private readonly Dictionary<string, AudioClip> _variantClipCache = new Dictionary<string, AudioClip>();

        /// <summary>[133차-P2] 표면 변형 클립 인덱스 상한 — 표면별 3변형(_000~_002) 배치 기준.</summary>
        private const int SurfaceVariantCount = 3;

        /// <summary>현재 볼륨 값 (0-1)</summary>
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Mathf.Clamp01(value);
                ApplyVolumeToAll();
            }
        }

        /// <summary>AudioSource 풀 크기</summary>
        public int PoolSize => _sourcePool?.Count ?? 0;

        // ===== Unity 생명주기 =====

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _instanceQuitting = false;
            DontDestroyOnLoad(gameObject);

            InitializeAudioPool();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }

        // ===== 퍼블릭 메서드 =====

        /// <summary>
        /// 지정된 효과음을 재생합니다.
        /// AudioSource 풀에서 사용 가능한 소스를 순환하여 사용합니다.
        /// </summary>
        /// <param name="type">재생할 효과음 종류</param>
        public void PlaySFX(SFXType type)
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"[SoundEffectManager] (Editor) PlaySFX: {type}");
                return;
            }

            string sfxName = GetSFXName(type);

            // AudioSource 풀에서 다음 소스 선택 (라운드 로빈)
            AudioSource source = GetNextAvailableSource();
            if (source == null)
            {
                Debug.LogWarning("[SoundEffectManager] 사용 가능한 AudioSource 없음 — 풀 확장 필요");
                return;
            }

            // 소스 상태 초기화 (PlaySFXAtPoint에서 오염 방지)
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;      // 2D 사운드
            source.pitch = 1f;
            source.panStereo = 0f;

            // [95차 후속 SFX] 실재생 경로 — Resources/Audio/SFX 클립을 캐시로 1회 로드해 풀 소스로 출력.
            AudioClip clip = LoadCachedClip(type);
            if (clip != null)
            {
                source.clip = clip;
                source.volume = _volume;
                source.Play();
                return;
            }

            // [133차-P2 수리] 클립 부재 폴백 — 로드 실패 시 무음 재생(정상 동작, 플레이스홀더가 아님 — 문구만 실상에 맞게 교정)
            Debug.Log($"[SoundEffectManager] SFX 클립 부재 폴백: {sfxName} (무음 — 에셋 미배치)");
            source.clip = null;
            source.volume = _volume;
            source.Play();
        }

        /// <summary>
        /// 지정된 효과음을 특정 위치에서 3D 공간 음향으로 재생합니다.
        /// </summary>
        /// <param name="type">재생할 효과음 종류</param>
        /// <param name="position">월드 공간 재생 위치</param>
        public void PlaySFXAtPoint(SFXType type, Vector3 position)
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"[SoundEffectManager] (Editor) PlaySFXAtPoint: {type} at {position}");
                return;
            }

            string sfxName = GetSFXName(type);

            // 플레이스홀더: AudioSource.PlayClipAtPoint 사용 (3D 공간 음향)
            AudioSource source = GetNextAvailableSource();
            if (source == null)
            {
                Debug.LogWarning("[SoundEffectManager] 사용 가능한 AudioSource 없음 — 3D SFX 재생 불가");
                return;
            }

            source.transform.position = position;
            // 소스 상태 초기화
            source.spatialBlend = 1f;      // 3D 사운드
            source.pitch = 1f;
            source.panStereo = 0f;

            // [95차 후속 SFX] 실재생 경로(3D) — 캐시 로드한 클립 출력, 부재 시 기존 플레이스홀더 폴백 유지.
            AudioClip clip = LoadCachedClip(type);
            if (clip != null)
            {
                source.clip = clip;
                source.volume = _volume;
                source.Play();
                return;
            }

            // clip이 null이므로 실제로 재생되지는 않음(무해 폴백)
            Debug.Log($"[SoundEffectManager] 🔊 SFX 3D 재생: {sfxName} at {position} (플레이스홀더)");
            source.clip = null;
            source.volume = _volume;
            source.Play();
        }

        /// <summary>
        /// 모든 효과음을 즉시 정지합니다.
        /// </summary>
        public void StopAllSFX()
        {
            if (_sourcePool == null) return;

            foreach (var source in _sourcePool)
            {
                if (source != null && source.isPlaying)
                {
                    source.Stop();
                }
            }

            Debug.Log("[SoundEffectManager] ⏹️ 모든 SFX 정지");
        }

        /// <summary>
        /// 특정 종류의 효과음이 현재 재생 중인지 확인합니다.
        /// 플레이스홀더 단계에서는 풀 내에서 재생 중인 AudioSource가 있는지 확인합니다.
        /// </summary>
        /// <param name="type">확인할 효과음 종류</param>
        public bool IsPlaying(SFXType type)
        {
            if (_sourcePool == null) return false;

            for (int i = 0; i < _sourcePool.Count; i++)
            {
                if (_sourcePool[i] != null && _sourcePool[i].isPlaying)
                    return true;
            }
            return false;
        }

        // ===== 내부 =====

        /// <summary>
        /// AudioSource 풀을 초기화합니다.
        /// </summary>
        private void InitializeAudioPool()
        {
            _sourcePool = new List<AudioSource>(_poolSize);
            _nextSourceIndex = 0;

            for (int i = 0; i < _poolSize; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.volume = _volume;
                source.spatialBlend = 0f; // 기본 2D 사운드
                _sourcePool.Add(source);
            }

            Debug.Log($"[SoundEffectManager] AudioSource 풀 초기화: {_poolSize}개");
        }

        /// <summary>
        /// 풀에서 다음 사용 가능한 AudioSource를 반환합니다 (라운드 로빈).
        /// </summary>
        private AudioSource GetNextAvailableSource()
        {
            if (_sourcePool == null || _sourcePool.Count == 0)
                return null;

            // 라운드 로빈으로 소스 선택 (null 또는 destroyed된 소스 건너뛰기)
            int attempts = 0;
            AudioSource source;
            do
            {
                source = _sourcePool[_nextSourceIndex];
                _nextSourceIndex = (_nextSourceIndex + 1) % _sourcePool.Count;
                attempts++;
            }
            while (source == null && attempts < _sourcePool.Count);

            return source;
        }

        /// <summary>
        /// 모든 AudioSource에 현재 볼륨을 적용합니다.
        /// </summary>
        private void ApplyVolumeToAll()
        {
            if (_sourcePool == null) return;

            foreach (var source in _sourcePool)
            {
                if (source != null)
                {
                    source.volume = _volume;
                }
            }
        }

        /// <summary>
        /// 지정된 효과음을 표면 변형 정보와 함께 재생합니다.
        /// Footstep 등 표면에 따라 다른 사운드가 필요한 경우 사용합니다.
        /// </summary>
        /// <param name="type">재생할 효과음 종류</param>
        /// <param name="surfaceVariant">표면 변형 태그 (step_grass, step_stone 등, 빈 문자열이면 기본값)</param>
        public void PlaySurfacedSFX(SFXType type, string surfaceVariant)
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"[SoundEffectManager] (Editor) PlaySurfacedSFX: {type} variant={surfaceVariant}");
                return;
            }

            string sfxName = GetSFXNameWithVariant(type, surfaceVariant);

            AudioSource source = GetNextAvailableSource();
            if (source == null)
            {
                Debug.LogWarning("[SoundEffectManager] 사용 가능한 AudioSource 없음 — 풀 확장 필요");
                return;
            }

            // 소스 상태 초기화 (PlaySFXAtPoint에서 오염 방지)
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;      // 2D 사운드 — 물발소리 포함 기존 정책 유지 [133차-P2]
            source.panStereo = 0f;

            // [133차-P2] 실재생 경로 — 표면 변형 클립을 변형 랜덤 선택으로 로드해 풀 소스로 출력.
            // 반복 방지: 변형 인덱스 랜덤 + pitch 0.94~1.06 미세 변주.
            AudioClip clip = LoadCachedVariantClip(sfxName, surfaceVariant);
            if (clip != null)
            {
                source.clip = clip;
                source.pitch = Random.Range(0.94f, 1.06f);
                source.volume = _volume;
                source.Play();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SoundEffectManager] 🔊 SFX 재생: {source.clip.name} pitch={source.pitch:F2}");
#endif
                return;
            }

            // 클립 부재 — 무해 폴백(clip=null 무음). 로그는 개발빌드 전용으로 축소 [133차-P2]
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SoundEffectManager] SFX 클립 부재: {sfxName} (무음 폴백)");
#endif
            source.pitch = 1f;
            source.clip = null;
            source.volume = _volume;
            source.Play();
        }

        /// <summary>
        /// [133차-P2] 표면 변형 클립 로드 — {베이스}_{variant}_{NNN} 3변형 중 랜덤 선택.
        /// 선택 인덱스 클립이 없으면 하위 인덱스로 재시도(물발소리 등 변형 수가 적은 표면 대응),
        /// 전부 없으면 기본 SFX_Footstep 클립으로 폴백.
        /// </summary>
        /// <param name="sfxName">GetSFXNameWithVariant가 산출한 변형 접두 이름 (예: SFX_Footstep_step_grass)</param>
        /// <param name="surfaceVariant">표면 변형 태그(빈 값이면 기본 클립 경로)</param>
        private AudioClip LoadCachedVariantClip(string sfxName, string surfaceVariant)
        {
            // variant가 비었거나 태그 형식이 아니면 기본 클립 경로
            if (string.IsNullOrEmpty(surfaceVariant))
                return LoadCachedClip(SFXType.Footstep);

            // 랜덤 인덱스 선택 → 부재 시 0까지 하향 재시도 (0도 없으면 폴백)
            for (int index = Random.Range(0, SurfaceVariantCount); index >= 0; index--)
            {
                string clipName = $"{sfxName}_{index:D3}";
                if (_variantClipCache.TryGetValue(clipName, out AudioClip cached))
                {
                    if (cached != null) return cached;
                    continue; // 부재 캐시 — 다음 인덱스
                }

                AudioClip clip = Resources.Load<AudioClip>($"Audio/SFX/{clipName}");
                _variantClipCache[clipName] = clip; // 부재 결과도 캐시 — 반복 로드 시도 방지
                if (clip != null) return clip;
            }

            // 표면 변형 클립 전체 부재 — 기본 SFX_Footstep 폴백
            return LoadCachedClip(SFXType.Footstep);
        }

        /// <summary>
        /// [95차 후속 SFX] Resources/Audio/SFX/{이름} 클립을 캐시 딕셔너리로 1회 로드합니다.
        /// 클립이 없으면 null 반환(호출부는 기존 플레이스홀더 폴백 유지).
        /// </summary>
        private AudioClip LoadCachedClip(SFXType type)
        {
            if (_clipCache.TryGetValue(type, out AudioClip cached))
                return cached;

            AudioClip clip = Resources.Load<AudioClip>($"Audio/SFX/{GetSFXName(type)}");
            _clipCache[type] = clip; // 부재 결과도 캐시 — 반복 로드 시도 방지
            return clip;
        }

        /// <summary>
        /// SFXType에 해당하는 내부 이름을 반환합니다.
        /// </summary>
        public static string GetSFXName(SFXType type)
        {
            return type switch
            {
                SFXType.Footstep => "SFX_Footstep",
                SFXType.Gather => "SFX_Gather",
                SFXType.Craft => "SFX_Craft",
                SFXType.Combat_Hit => "SFX_Combat_Hit",
                SFXType.Combat_Swing => "SFX_Combat_Swing",
                SFXType.Assassination => "SFX_Assassination",
                SFXType.DoorOpen => "SFX_DoorOpen",
                SFXType.DoorClose => "SFX_DoorClose",
                SFXType.ItemPickup => "SFX_ItemPickup",
                SFXType.ItemDrop => "SFX_ItemDrop",
                SFXType.Alarm => "SFX_Alarm",
                SFXType.Victory => "SFX_Victory",
                SFXType.Defeat => "SFX_Defeat",
                SFXType.AutoMove_Start => "SFX_AutoMove_Start",
                SFXType.AutoMove_Complete => "SFX_AutoMove_Complete",
                SFXType.AutoMove_Cancel => "SFX_AutoMove_Cancel",
                // [95차 후속 SFX] 패링 등급별 실오디오 — Resources/Audio/SFX 클립명 매핑
                SFXType.Parry_Perfect => "SFX_Parry_Perfect",
                SFXType.Parry_Good => "SFX_Parry_Good",
                SFXType.Parry_Fail => "SFX_Parry_Fail",
                // [무번호 16-B4] 텔레그래프 경고 저음 웅성 — Resources/Audio/SFX 클립명 매핑
                SFXType.Telegraph_Warning => "SFX_Telegraph_Warning",
                // [무번호 19-E2] 몬스터 발견 경계음 — Resources/Audio/SFX 클립명 매핑
                SFXType.Monster_Alert => "SFX_Monster_Alert",
                _ => "SFX_Unknown"
            };
        }

        /// <summary>
        /// SFXType과 표면 변형 정보를 결합한 내부 이름을 반환합니다.
        /// variant가 비어있으면 기본 GetSFXName 결과를 반환합니다.
        /// </summary>
        private static string GetSFXNameWithVariant(SFXType type, string variant)
        {
            string baseName = GetSFXName(type);
            if (string.IsNullOrEmpty(variant)) return baseName;
            return $"{baseName}_{variant}";
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용).
        /// </summary>
        public static void ResetAll()
        {
            if (_instance != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_instance.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(_instance.gameObject);
                }
                _instance = null;
            }
            _instanceQuitting = false;
        }
    }
}