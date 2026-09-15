using UnityEngine;
using UnityEngine.VFX;   // [2026-09-13 alive 진단] VisualEffect.aliveParticleCount — SlashAliveProbe 진단용

namespace ProjectName.Systems
{
    /// <summary>
    /// 3단계 이펙트: 에셋 스토어 무료 프리팹 기반 스윙/피격 FX 정적 러너.
    ///   - 스윙: Free Slash VFX "Slash VFX.prefab" (Assets/Resources/FX/Slash/Slash VFX)
    ///   - 피격: Matthew Guz "Magic Hit 2" (Assets/Resources/FX/Impact/MagicHit)
    ///     [2026-09-14(47차)] 사용자 지정 — Guz Magic Hit 2로 교체(공격 예시와 가장 유사).
    ///     BasicHit은 Resources에 유지(롤백 가능: 상수 경로를 FX/Impact/BasicHit로 되돌리면 즉시 복귀).
    ///     [45차 P3 임팩트 단일화] BasicHit2(Construct)/TravisHit(크로스) 경로 제거 — 단일 프리팹 위임 선례 유지
    ///   - [2026-09-13 스타일라이즈드 스윙] Stylizer Slash VFX(slash5-HungNguyen) white-blue 변종
    ///     (Assets/Resources/FX/Slash/StylizedSlash) — PlaySlashStage가 콤보 스윙 아크 발화(아래 상수/메서드 참조)
    ///     [2026-09-14(47차 후속7)] 색상 — 파란색(피격 Magic Hit 매칭) white-blue 변종(같은 경로/같은 meta guid — 코드 무변경)
    ///     [2026-09-14(47차 후속2)] 프리팹 직렬화 참조 결함(fileID 불일치 → assetNull 지속) 우회 — Resources에서
    ///     VisualEffectAsset을 직접 로드(StylizedVfxResourcePath)해 런타임 할당(PlaySlashStage 본문 참조).
    ///     [2026-09-14(47차 후속6)] 깨진 참조 프리팹은 Instantiate 시 VisualEffect 컴포넌트 누락 확인
    ///     (probe assetNull/alive=-1 패턴) → 프리팹 Instantiate 폐기, new GameObject + AddComponent
    ///     런타임 빌드로 전환(LoadStylizedSlashPrefab/프리팹 캐시 제거 — PlaySlashStage 본문 참조).
    ///
    /// 프리팹은 에디터 인스톨러(Tools/VFX/Install Slash+Impact to Resources, -executeMethod:
    /// ProjectName.EditorTools.VFXResourceInstaller.InstallAll)가 Resources 하위로 복사해두므로
    /// Resources.Load로 지연 로드 + static 캐시한다. 로드 실패 시 경고는 static bool로 1회만.
    ///
    /// HitVFX.cs의 정적 러너 패턴과 일치: static 클래스는 코루틴을 돌릴 수 없으므로
    /// 내부 숨은 호스트 MonoBehaviour(SlashFxHost, HideAndDontSave)를 지연 생성해 사용.
    ///
    /// 파괴 정책: duration+startLifetime.max 기반 동적 파괴도 가능하지만 3초 고정으로 충분
    /// (두 프리팹 모두 3초 내 완전 소진 — 주석 명시).
    ///
    /// [2026-09-12 P2 피격 FX 재색상] 외부 팩 프리팹의 보라·자주 기본색은 사용자 취향("피격시 보라색
    /// 점들이 떠다니는 건 별로")과 불일치 → 스폰 직후 TintParticles로 붉은/흰 계열 재색상
    /// (스윙 0.85,0.95,1.0 — 하단 공용 헬퍼 참조).
    /// [2026-09-14(47차)] 임팩트(PlayImpact/PlayCross)는 MagicHit 에셋 고유 색상 유지 — TintParticles(흰)
    /// 호출 제거(사용자가 이 에셋의 룩을 선택). NormalizePurpleParticles는 유지(보라만 골드화이트 안전망).
    ///
    /// [2026-09-12 P2 크로스→TravisHit 교체] 타 완료 FX(PlayCross)는 Free Slash VFX 팩 "Multiple Slashes"에서
    /// "FX/Impact/TravisHit" 프리팹으로 교체(빌보드/쿨다운/파괴 패턴 유지). 스윙(Slash VFX)은 호출부
    /// (HumanoidClipDriver) 제거로 자연 데드화 — 스윙 궤적은 WeaponSwingTrail(무기 트레일)이 대체하며
    /// PlaySlash/LoadSlashPrefab은 함수 유지(데드).
    ///
    /// [45차 P3 임팩트 단일화] 타격 FX 3종 혼용(PlayImpact의 BasicHit/BasicHit2 분기 + PlayCross의 TravisHit)을
    /// BasicHit 단일 경로로 통일 — 프리팹 소스 분산에 의한 톤 불일치 해소. 틴트는 45차 BOTW 팔레트로 정렬:
    /// 코어=흰(1,1,1) / 액센트=골드(1,0.9,0.5) / 외곽=주황(1,0.55,0.2) — 크로스=골드, 임팩트=흰.
    /// [2026-09-14(47차)] 위 틴트 정책은 MagicHit 교체로 철회 — 에셋 고유 색상 사용(보라 정규화만 유지).
    /// [2026-09-15 콤보 스테이지 틴트] PlaySlashStage가 콤보 스테이지별 틴트 적용 — 1타 흰(CoreTint) /
    /// 2타 골드(AccentTint) / 3타 주황·붉은(OuterTint) — ComboStageTint 헬퍼 + TintParticles 호출.
    /// </summary>
    public static class SlashVFXRunner
    {
        /// <summary>스팸 방지: 마지막 스폰 후 이 시간 이내 재호출은 무시 (이중 발화 흡수).</summary>
        private const float MIN_SPAWN_INTERVAL = 0.08f;

        /// <summary>
        /// [2026-09-13 스타일라이즈드 슬래시] 스타일라이즈드 스윙 아크 전용 쿨다운 — MIN_SPAWN_INTERVAL(0.08s)과
        /// 별개 상수. 콤보 타당 최소 간격(0.25s)을 강제해 빠른 연타에서 아크 겹침/스팸을 방지한다
        /// (스윙/크로스/임팩트 쿨다운과 독립 — 타이머도 별도).
        /// </summary>
        private const float MIN_STYLIZED_SLASH_INTERVAL = 0.25f;

        // [2026-09-14(47차 후속7)] 스윙 스케일 고정 상수(구 기본값 1.5) 제거 — 슬래시 크기=공격범위 연동으로 대체
        // (사거리×0.4, 클램프 0.8~1.6 — PlaySlashStage 본문의 동적 계산 + RangeOf 헬퍼 참조).

        /// <summary>
        /// 스타일라이즈드 슬래시 자가 파괴 예약 시간(튜닝 상수) — VFX Graph가 자체 수명 후 완전 소진되므로
        /// 잔존 GO 정리용. ScheduleDestroy 파괴 예약 패턴 재사용(WeaponSwingTrail/기존 3s 파괴 선례).
        /// </summary>
        private const float StylizedSlashDestroyAfter = 1.5f;

        /// <summary>
        /// [2026-09-13 폴백] 구 Slash VFX 폴백 스케일 배수 — 구 고정 스케일값(1.5)과 동일 값으로 시작하는
        /// 폴백 전용 상수(독립 튜닝 가능, 동적 사거리 연동 미적용). 폴백 발화 시 이 값으로 균일 스케일.
        /// </summary>
        private const float FallbackSlashScale = 1.5f;

        /// <summary>인스턴스 파괴 예약 시간 (고정).</summary>
        private const float DESTROY_AFTER = 3f;

        /// <summary>
        /// [2026-09-12 스트로크 미러 플립] 사용자 실측(테스트 영상 4): 빌보드 정면 뷰에서도 아크 진행이
        /// 좌우 반대로(우→좌) 읽힘 → X 미러로 스트로크 방향 플립(쿼드 로컬 X 반전 = 아크 진행 좌우 반전).
        /// 빌보드 Instantiate 직후, 롤(Rotate) 적용 전에 localScale로 적용. 정방향 스트로크로 판명되면
        /// 1f로 되돌릴 것(튜닝 상수). 크로스/임팩트에는 미적용 — 스윙(Slash VFX) 전용.
        /// </summary>
        private const float StrokeMirrorX = -1f;

        private const string SlashResourcePath = "FX/Slash/Slash VFX";
        // [2026-09-14(47차)] 사용자 지정 — Guz Magic Hit 2(공격 예시와 가장 유사). BasicHit은 Resources에 유지(롤백 가능).
        // 프리팹은 수동 복사본(Assets/Resources/FX/Impact/MagicHit.prefab — meta guid 신규 재발행, StylizedSlash 선례).
        // 인스톨러 대상 아님(StylizedSlash와 동일 — 재설치 무관). 롤백: 이 상수를 "FX/Impact/BasicHit"로 되돌리면 끝.
        private const string MagicHitResourcePath = "FX/Impact/MagicHit";
        // [2026-09-14(47차 후속6)] StylizedSlashResourcePath(프리팹 경로) 상수 제거 — 프리팹 Instantiate 폐기
        // (깨진 참조 프리팹 Instantiate 시 VisualEffect 컴포넌트 누락 → 런타임 빌드 전환). .vfx 에셋 경로만 유지.

        // [2026-09-14(47차 후속2)] StylizedSlash .vfx 에셋 직접 로드용 경로 — 프리팹 내부 직렬화 참조(fileID
        // 불일치) 결함을 우회하고 Resources에서 VisualEffectAsset을 로드해 런타임에 ve.visualEffectAsset에
        // 할당한다. 에셋: Assets/Resources/FX/Slash/StylizedSlashVFX.vfx(수동 복사본 — meta guid 신규 발행).
        private const string StylizedVfxResourcePath = "FX/Slash/StylizedSlashVFX";

        // ── 45차 P3: BOTW 팔레트 틴트 상수 — 코어=흰 / 액센트=골드 / 외곽=주황 ──
        /// <summary>[45차 P3: BOTW 팔레트] 코어 틴트 = 흰(1,1,1) — 임팩트/스윙 기본 톤.</summary>
        internal static readonly Color CoreTint = new Color(1f, 1f, 1f);
        /// <summary>[45차 P3: BOTW 팔레트] 액센트 틴트 = 골드(1,0.9,0.5) — 크로스(타 완료) 액센트 톤.</summary>
        internal static readonly Color AccentTint = new Color(1f, 0.9f, 0.5f);
        /// <summary>[45차 P3: BOTW 팔레트] 외곽 틴트 = 주황(1,0.55,0.2) — [2026-09-15] 콤보 3타 스테이지 틴트(주황/붉은)로 직접 사용.</summary>
        internal static readonly Color OuterTint = new Color(1f, 0.55f, 0.2f);

        // ── static 캐시/상태 ─────────────────────────────────────────
        private static GameObject _slashPrefab;
        private static GameObject _magicHitPrefab;   // [2026-09-14(47차)] BasicHit 캐시 → MagicHit 캐시(캐시/1회 경고 패턴 동일)
        // [2026-09-14(47차 후속6)] _stylizedSlashPrefab 프리팹 캐시 제거 — 프리팹 Instantiate 폐기(런타임 빌드 전환).

        // [2026-09-14(47차 후속2)] StylizedSlash .vfx 에셋 캐시(VisualEffectAsset) — 프리팹 직렬화 참조 결함
        // 우회용(런타임 직접 할당). 로드 실패 경고는 static bool로 1회만(기존 _slashLoadFailed 선례).
        private static VisualEffectAsset _stylizedVfxAsset;
        private static bool _stylizedVfxLoadWarned;

        private static bool _slashLoadFailed;
        private static bool _magicHitLoadFailed;
        // [2026-09-14(47차 후속6)] _stylizedSlashLoadFailed 제거 — 프리팹 로더 폐기(로드 1회 경고는 _stylizedVfxLoadWarned 담당).
        private static bool _shaderErrorWarned;

        // 스팸 방지 타이머는 스윙/임팩트 분리: 같은 프레임에 스윙+임팩트가 연속 와도
        // 서로를 묵살하지 않게 하고, 동일 종류의 이중 발화(PlayerCombat+AttackSystem
        // 좌클릭 이중 처리 등)만 0.08s 쿨다운으로 흡수한다.
        private static float _lastSwingSpawnTime = -999f;
        private static float _lastImpactSpawnTime = -999f;
        private static float _lastCrossSpawnTime = -999f;   // 크로스 전용 쿨다운 — 스윙/임팩트와 독립
        private static float _lastStylizedSlashSpawnTime = -999f;   // [2026-09-13] 스타일라이즈드 전용 쿨다운(0.25s) — 3종과 독립

        // [2026-09-13 alive 진단] VFX Graph 미출력 폴백 플래그 — SlashAliveProbe가 aliveParticleCount<=0을
        // 감지하면 true로 세팅되고, 이후 PlaySlashStage 발화부터 구 Slash VFX("FX/Slash/Slash VFX") 폴백
        // 경로를 사용한다. alive>0(정상 출력) 진단 시 false로 복귀 — VFX 복구 시 자동 복귀.
        private static bool _vfxDeadFallbackActive;

        private static SlashFxHost _host;

        // ================================================================
        // 공개 진입점
        // ================================================================

        /// <summary>
        /// 공격 스윙 FX. position: 플레이어 전방 스윙 지점, direction: 수평 스윙 방향.
        /// 로드 실패 시 1회 경고 후 조용히 반환 (전투 흐름 절대 방해 없음).
        /// [42차 P2 스윙=무기 트레일로 교체] 호출부(HumanoidClipDriver 콤보/레거시 경로)가 모두 제거되어
        /// 자연 데드화 — 함수는 유지. 스윙 궤적은 WeaponSwingTrail(무기 팁 TrailRenderer)이 담당.
        /// </summary>
        public static void PlaySlash(Vector3 position, Vector3 direction) => PlaySlash(position, direction, 0f);

        /// <summary>
        /// 공격 스윙 FX (궤적 기울임 포함). arcRollDegrees: 로컬 Z축 회전각(도) —
        /// 0이면 수평 스윙. 2타 수직 궤적(-90)/3타 사선(-45) 등 궤적 평면 기울이기 전용 —
        /// [2026-09-12] 쿼드 자체는 카메라 수평 빌보드로 세워지므로 롤은 화면축 기준 기울임이다.
        /// [2026-09-12 스트로크 미러 플립] 빌보드 직후 X 스케일을 <see cref="StrokeMirrorX"/>로 반전해
        /// 아크 진행 방향을 좌우 플립한다(사용자 실측: 우→좌 역방향 읽힘). 크로스/임팩트에는 미적용.
        /// </summary>
        public static void PlaySlash(Vector3 position, Vector3 direction, float arcRollDegrees)
        {
            // 스팸 방지 (동일 스윙 이중 발화 흡수 — 임팩트와는 독립 쿨다운)
            if (Time.time - _lastSwingSpawnTime < MIN_SPAWN_INTERVAL) return;
            _lastSwingSpawnTime = Time.time;

            GameObject prefab = LoadSlashPrefab();
            if (prefab == null) return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // [2026-09-12 카메라 수평 빌보드] 기존 Quaternion.LookRotation(dir)은 쿼드 +Z를 스윙 dir(원거리)로
            // 향하게 했다 — 3인칭 후방 카메라가 쿼드 뒷면(-Z)을 비스듬히 보게 되고, 슬래시 팩의 아크(+Z 정면
            // 제작이 일반적)는 뒤에서 보면 스트로크 진행이 좌우 미러링되어 "뒤에서 앞으로 휘두르는" 역스윙처럼
            // 읽혔다(사용자 2회차 리포트). → 쿼드를 카메라 시선 축(수평)에 정렬해 제작사 의도의 정면 뷰를 보장.
            // 궤적 개성(수직/사선)은 아래 로컬 Z 롤(화면축 기준 기울임)로만 표현하고, dir은 폴백/로그용으로 유지.
            Vector3 faceDir = CameraHorizontalFaceDir(position, dir);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.LookRotation(faceDir));
            // [2026-09-12 스트로크 미러 플립] X 스케일 반전(튜닝 상수 StrokeMirrorX) — 아크 진행 방향 좌우 플립.
            // 빌보드 Instantiate 직후, 롤(Rotate) 적용 전에 실행해 로컬 X 미러가 롤 회전축과 간섭 없이 먹게 한다.
            instance.transform.localScale = new Vector3(StrokeMirrorX, 1f, 1f);
            // 로컬 Z축 롤 = 빌보드 이후 화면축 기준 궤적 기울이기 (수직/사선 궤적 표현은 여기서만 담당)
            if (Mathf.Abs(arcRollDegrees) > 0.01f)
                instance.transform.Rotate(0f, 0f, arcRollDegrees, Space.Self);
            // [2026-09-12 P2 피격 FX 재색상] 스윙 궤적 일관색 — 팩 기본 보라 톤 제거. 파티클 재생 전에 적용해
            // 첫 방출 파티클부터 새 색이 나온다. [45차 P3: BOTW 팔레트] 스윙 = 코어 흰(1,1,1) 정렬(데드 경로).
            TintParticles(instance, CoreTint, 0.1f);
            // [2026-09-13 보라 정규화] 틴트 후 잔여 보라(colorOverLifetime 그라디언트 등) 골드화이트 교체.
            NormalizePurpleParticles(instance);
            Debug.Log($"[SlashVFX] ✅ 스윙 FX 스폰 (pos={position}, faceDir={faceDir:F2}, roll={arcRollDegrees:F0}°, 발화시각={Time.time:F2}s)");   // 1회성 검증 아님 — 좌클릭마다 1줄, 발화 증거
            instance.name = "SlashVFX_Swing";

            PlayAllParticleSystems(instance);
            DetectShaderErrorOnce(instance, "Slash");
            ScheduleDestroy(instance);
        }

        /// <summary>
        /// [2026-09-13 스타일라이즈드 슬래시] 콤보 스윙 아크 FX — slash5-HungNguyen 팩 white-blue 변종의
        /// VFX Graph 에셋(Resources/FX/Slash/StylizedSlashVFX.vfx)을 콤보 스테이지별 오리엔테이션으로 발화한다
        /// (HumanoidClipDriver.FireComboSlash의 Player 전용 경로에서 호출). [2026-09-14(47차 후속7)] 색상 —
        /// 파란색(피격 Magic Hit 매칭) white-blue 변종 — 별도 틴트 없이 원본색 사용이 선택 사유.
        /// [2026-09-15 콤보 스테이지 틴트] 위 정책 갱신 — 스테이지별 틴트(1타 흰/2타 골드/3타 주황·붉은)를
        /// ComboStageTint로 산출해 TintParticles로 적용(폴백 경로 유효, 스타일라이즈드는 ParticleSystem 부재로 no-op).
        /// 오리엔테이션(38차 규약): ① 루트를 카메라 수평 빌보드(CameraHorizontalFaceDir 재사용) ② stage 롤
        /// (1타 0 / 2타 -90 / 3타 -45 — 기존 규약, ComboStageRollDegrees) ③ yawSign 좌우 플립(아래 주석).
        /// [2026-09-14(47차 후속6)] 깨진 참조 프리팹은 Instantiate 시 VisualEffect 컴포넌트 누락 확인
        /// (probe assetNull/alive=-1 패턴) → 프리팹 Instantiate 폐기, new GameObject + AddComponent
        /// 런타임 빌드로 전환. 기동은 에셋 할당 → Reinit → Play 명시 순서(아래 본문 참조).
        /// 쿨다운 0.25s(스윙/크로스/임팩트와 독립), 자가 파괴 1.5s(StylizedSlashDestroyAfter).
        /// </summary>
        /// <param name="position">스윙 앵커(루트 기준 fwd 0.55 + up 1.25 — 46차 후속 규격, 몸에 가깝게. 호출부 산출).</param>
        /// <param name="direction">콤보 방향(ComboStageDirection → 전방 반구 클램프 결과) — 빌보드 폴백/로그용.</param>
        /// <param name="stage">콤보 스테이지(1~3) — 롤 규약 적용 대상.</param>
        /// <param name="yawSign">스윙 yaw 부호(-1=좌, +1=우) — 아크 진행 좌우 플립 판정(호출부 ComboStageDirection 기반).</param>
        /// <param name="playerRoot">46차 후속: 플레이어 루트 Transform — 인스턴스 부착(추종) 대상. null이면 부모 없이 스폰(폴백).</param>
        public static void PlaySlashStage(Vector3 position, Vector3 direction, int stage, float yawSign, Transform playerRoot = null)
        {
            // 스팸 방지 — 스타일라이즈드/폴백 공용 쿨다운(0.25s, MIN_SPAWN_INTERVAL과 별개 상수): 콤보 타당 최소 간격 강제
            if (Time.time - _lastStylizedSlashSpawnTime < MIN_STYLIZED_SLASH_INTERVAL) return;
            _lastStylizedSlashSpawnTime = Time.time;

            // [2026-09-15 콤보 스테이지 틴트] 스테이지별 팔레트 산출 — 1타 흰 / 2타 골드 / 3타 주황·붉은.
            // 스타일라이즈드/폴백 두 경로가 동일 stageTint를 쓰도록 여기서 1회 산출(아래 TintParticles 호출 참조).
            Color stageTint = ComboStageTint(stage);

            // [2026-09-13 폴백 분기] alive 진단으로 VFX Graph 미출력이 확정된 상태(_vfxDeadFallbackActive)면
            // 구 Slash VFX 프리팹(Resources.Load("FX/Slash/Slash VFX") — 8 MeshRenderer, URP Shader Graph,
            // 42차 이전 렌더 실적)으로 동일 오리엔테이션 파이프라인 발화한다(아래 SpawnFallbackSlashStage).
            if (_vfxDeadFallbackActive)
            {
                GameObject fallbackPrefab = LoadSlashPrefab();   // 캐시 + static 1회 경고 — 기존 스윙 로더 공용
                if (fallbackPrefab != null)
                {
                    SpawnFallbackSlashStage(fallbackPrefab, position, direction, stage, yawSign, playerRoot, stageTint);
                    return;
                }
                // Resources 로드 실패 시(위 로더가 1회 경고) 가장 안전한 기본값: 스타일라이즈드 경로 유지 —
                // 아래 기존 StylizedSlash 스폰 경로로 계속 진행한다.
            }

            // ════════════════════════════════════════════════════════════
            // [2026-09-14(47차 후속6): 깨진 참조 프리팹은 Instantiate 시 VisualEffect 컴포넌트 누락 확인
            // (assetNull/alive=-1 패턴) → 런타임 빌드로 전환] — 프리팹 Instantiate 폐기.
            // 후속2~5의 에셋 직접 할당/Reinit 순서 수리로도 해소 불가(인스턴스에 VisualEffect 컴포넌트
            // 자체가 부재 → ve==null → 기동/진단 모두 무효). 원본 프리팹(StylizedSlash)은 더 이상
            // 사용하지 않고, new GameObject + AddComponent로 런타임에 조립한다(결함 직렬화 상태 원천 차단).
            // ════════════════════════════════════════════════════════════
            if (_stylizedVfxAsset == null)
                _stylizedVfxAsset = Resources.Load<VisualEffectAsset>(StylizedVfxResourcePath);   // 기존 캐시 로드 유지(후속2)
            if (_stylizedVfxAsset == null)
            {
                // [기존 로드 실패 분기 유지] 확정 불가 에셋 — 0.35s/1.0s 진단을 기다리지 않고 즉시 폴백 전환
                // (1회 경고 + NotifyVfxDead + 구 Slash VFX 프리팹 즉시 발화. 플래그는 멱등 — 중복 호출 안전).
                if (!_stylizedVfxLoadWarned)
                {
                    _stylizedVfxLoadWarned = true;
                    Debug.LogWarning("[SlashVFX] Resources/FX/Slash/StylizedSlashVFX.vfx 로드 실패 — 에셋 임포트 오류(에디터에서 .vfx 열어 확인 필요), 폴백 경로 사용");
                    Debug.Log("[SlashVFX] visualEffectAsset 할당 실패(Resources 로드 null) — 폴백으로 전환");
                }
                NotifyVfxDead();
                GameObject fallbackPrefab = LoadSlashPrefab();   // 캐시 + static 1회 경고 — 기존 스윙 로더 공용
                if (fallbackPrefab != null)
                {
                    SpawnFallbackSlashStage(fallbackPrefab, position, direction, stage, yawSign, playerRoot, stageTint);
                    return;
                }
                return;   // 폴백 프리팹도 부재면 조용히 반환(전투 흐름 절대 방해 없음 — 로더가 1회 경고)
            }

            // ── 런타임 빌드: 프리팹 경유 없이 GO + VisualEffect를 코드로 조립 ──
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // ① 타격 대상 방향을 향하도록 회전 (카메라 빌보드 대신 대상 방향 사용)
            //    쿼드 +Z가 dir(타격 대상 방향)을 향하게 — 아크의 볼록한 부분(블레이드)이 대상을 향함
            GameObject instance = new GameObject("StylizedSlashVFX");
            instance.transform.SetPositionAndRotation(position, Quaternion.LookRotation(dir));

            // VisualEffect 신규 부착 — Unity 규약상 AddComponent<VisualEffect>를 넣으면 네이티브 코드가
            // VFXRenderer를 자동 부착한다(Renderer 없는 VisualEffect는 렌더링 불가 — 네이티브가 항상 보장).
            // VFXRenderer는 UnityEngine 내부 타입(internal)이라 스크립트에서 명시 AddComponent 자체가
            // 불가능하므로, 중복 가드(GetComponent 체크)도 불필요 — 자동 부착에 의존한다.
            var ve = instance.AddComponent<VisualEffect>();
            // [후속6 개정] 순서 규약: 에셋 할당 → Reinit → Play. 후속5의 "Reinit 먼저" 규약은 프리팹 인스턴스의
            // 깨진 직렬화 값이 Reinit에서 되살아나는 문제 대응이었으나, 런타임 빌드 컴포넌트에는 직렬화 결함이
            // 원천 없으므로 — 리소스 에셋 직접 할당 후 Reinit(그래프 상태를 새 에셋 기준 리셋) → Play(OnPlay
            // 명시 발사, 이미 재생 중이어도 무해)가 안전하다. using UnityEngine.VFX 있음.
            ve.visualEffectAsset = _stylizedVfxAsset;   // 리소스 에셋 직접 할당
            ve.Reinit();                                // 에셋 교체 후 리셋
            ve.Play();

            // ①-2 [46차 후속: 플레이어 부착(추종)] 스폰 직후 플레이어 루트에 부착 — worldPositionStays=true로
            // 스폰 시점의 화면 정렬(빌보드/롤/플립 결과)을 그대로 유지한 채 이후 플레이어 이동/회전에 따라가
            // 아크가 몸에서 분리되어 보이지 않는다(부착감). playerRoot가 null이면 기존처럼 월드 고정 스폰(폴백).
            // 파괴 정책: 1.5s 후 자식 인스턴스만 Destroy되므로 부모(플레이어) 파괴를 유발하지 않음(안전).
            if (playerRoot != null)
                instance.transform.SetParent(playerRoot, true);

            // ② [방향 — 47차 후속9] yawSign Y 180도 플립 제거 — 테스트 13 실측: 플립 적용 아크가 캐릭터 등 쪽(후방)으로 스왑됨.
            // 빌보드 기준 방향 유지 + 앵커 전방 이동(호출부 fwd0.8)으로 아크를 항상 전방에 배치. 좌우 미러가 필요하면 롤/스케일로 튜닝.

            // ③ [stage 롤 — 기존 규약] 1타 수평 0° / 2타 수직 -90° / 3타 사선 -45°(빌보드 후 로컬 Z 롤 = 화면축 기준 기울임)
            float roll = ComboStageRollDegrees(stage);
            if (Mathf.Abs(roll) > 0.01f)
                instance.transform.Rotate(0f, 0f, roll, Space.Self);

            // [2026-09-14(47차 후속9): 더 작게(스월형 네이티브가 대형 — 사거리×0.15, 클램프 0.3~0.7: 검 2.5m→0.38)] — 사용자 지정 "공격범위랑 일치"
            float scale = Mathf.Clamp(RangeOf(WeaponEquipManager.CurrentType) * 0.15f, 0.3f, 0.7f);
            instance.transform.localScale = Vector3.one * scale;

            // [2026-09-14(47차 후속6)] 기동 블록 이동 — Reinit+Play는 위 런타임 빌드 블록(AddComponent 직후
            // 할당→Reinit→Play)에서 수행. 프리팹 GetComponent<VisualEffect>() 분기(ve==null 시 아무것도
            // 하지 않던 무기동 결함)는 폐기되었다. 오리엔테이션(부착/플립/롤/스케일)은 Play 후 동일 프레임
            // 내 적용 — 렌더 전 트랜스폼 확정이라 출력에 영향 없음(후속6 전환으로 동일 프레임 순서 변경).

            // [2026-09-13 alive 진단 → 2026-09-14(47차) 강화] 부착/오리엔테이션 완료 시점에 진단 러너 부착 —
            // 0.35s와 1.0s 두 시점에 aliveParticleCount를 확인해 VFX Graph 미출력을 판정한다(진단 러너 =
            // 파일 하단 SlashAliveProbe — 두 체크 모두 alive<=0일 때만 폴백 전환).
            instance.AddComponent<SlashAliveProbe>();

            // [2026-09-15 콤보 스테이지 틴트] 스테이지별 틴트 적용 — 1타 흰(CoreTint) / 2타 골드(AccentTint) /
            // 3타 주황·붉은(OuterTint). 이 팩은 노출 프로퍼티(m_PropertySheet)가 비어 있고 파티클 시스템이 없어
            // (VFX Graph 단독) 현재 TintParticles는 no-op이지만, 폴백 경로(구 Slash VFX 파티클 프리팹)와 동일
            // 파이프라인 유지 + 향후 팩 교체 시 즉시 유효하도록 공용 호출로 확정.
            TintParticles(instance, stageTint, 0.1f);

            // [2026-09-14(47차)] 위 런타임 기동(할당→Reinit→Play) 후 셰이더 오류 감지만 수행.
            DetectShaderErrorOnce(instance, "StylizedSlash");
            Debug.Log($"[SlashVFX] 스타일라이즈드 슬래시 발화 (stage={stage}, stageTint={stageTint}, yawSign={yawSign})");   // 1회성 아님 — 발화마다(크로스 로그 선례)
            ScheduleDestroy(instance, StylizedSlashDestroyAfter);   // VFX 자체 종료 후 잔존 없음 — 1.5s 자가 파괴
        }

        /// <summary>
                /// [2026-09-13 폴백 스폰] 구 Slash VFX 프리팹("FX/Slash/Slash VFX" — 8 MeshRenderer, URP Shader Graph,
                /// 42차 이전 렌더 실적)을 스타일라이즈드 경로와 동일 오리엔테이션 파이프라인으로 발화한다:
                /// ① 타격 대상 방향 정렬 (dir을 향하게) ② playerRoot SetParent(true) 부착 ③ stage 롤
                /// ④ 사거리 기반 스케일 (RangeOf * 0.15f, 클램프 0.3~0.7) ⑤ 색상 흰색 고정.
                /// 파괴 1.5s(StylizedSlashDestroyAfter 공용), 쿨다운은 PlaySlashStage 선두의 _lastStylizedSlashSpawnTime 하나를 공유한다.
                /// </summary>
                private static void SpawnFallbackSlashStage(GameObject prefab, Vector3 position, Vector3 direction, int stage, float yawSign, Transform playerRoot, Color stageTint)
                {
                    Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
                    // ① 타격 대상 방향 정렬 — 쿼드 +Z가 dir(타격 대상 방향)을 향하게
                    GameObject instance = Object.Instantiate(prefab, position, Quaternion.LookRotation(dir));
                    instance.name = "SlashVFX_Fallback";

                    // ② 플레이어 부착(추종) — worldPositionStays=true, 기존 경로 동일(파괴는 자식 인스턴스만 → 부모 안전)
                    if (playerRoot != null)
                        instance.transform.SetParent(playerRoot, true);

                    // ③ yawSign 좌우 플립 — 제거(47차 후속9와 동일: 테스트 13 실측 플립 적용 시 아크가 캐릭터 등 쪽으로 스왑됨)
            // 빌보드 기준 방향 유지 + 앵커 전방 이동(호출부 fwd0.8)으로 아크를 항상 전방에 배치.
            // if (yawSign < 0f)
            //     instance.transform.Rotate(0f, 180f, 0f, Space.Self);

                    // ④ stage 롤 — 기존 규약 동일(1타 0° / 2타 -90° / 3타 -45°)
                    float roll = ComboStageRollDegrees(stage);
                    if (Mathf.Abs(roll) > 0.01f)
                        instance.transform.Rotate(0f, 0f, roll, Space.Self);

                    // ⑤ 스케일 — 사거리 기반 동적 스케일 (스타일라이즈드와 동일: RangeOf * 0.15f, 클램프 0.3~0.7)
                    float scale = Mathf.Clamp(RangeOf(WeaponEquipManager.CurrentType) * 0.15f, 0.3f, 0.7f);
                    instance.transform.localScale = Vector3.one * scale;

                    // [수정] 색상 흰색 고정 (stageTint 무시)
                    TintParticles(instance, CoreTint, 0.1f);

                    // 구 프리팹 자체 재생 불가 케이스 대비 — 기존 PlaySlash 선례대로 파티클 명시 재생(이미 재생 중이면 무해)
                    PlayAllParticleSystems(instance);
                    DetectShaderErrorOnce(instance, "FallbackSlash");
                    Debug.Log($"[SlashVFX] 폴백 Slash VFX 발화 (stage={stage}, scale={scale:F2}, white)");
                    ScheduleDestroy(instance, StylizedSlashDestroyAfter);   // 1.5s 자가 파괴 — 스타일라이즈드와 동일 상수 공용
                }

        /// <summary>콤보 스테이지별 아크 롤(기존 규약) — 1타 수평 0° / 2타 수직 -90° / 3타 사선 -45°. 3타 부호는 튜닝 포인트.</summary>
        private static float ComboStageRollDegrees(int stage)
            => stage == 2 ? -90f : (stage == 3 ? -45f : 0f);

        /// <summary>
        /// [2026-09-15 콤보 스테이지 틴트] 콤보 스테이지별 스윙 아크 팔레트 — 45차 BOTW 팔레트 상수 재사용:
        /// 1타 흰(CoreTint) / 2타 골드(AccentTint) / 3타 주황·붉은(OuterTint). 범위 외 스테이지는 흰(코어) 폴백.
        /// [수정] 슬래시 색상 통일: 항상 흰색으로 고정 (몬스터 대상 슬래시)
        /// </summary>
        private static Color ComboStageTint(int stage)
            => CoreTint; // 항상 흰색 (CoreTint = 1,1,1,1)

        /// <summary>
        /// [2026-09-14(47차 후속7)] 타입별 공격 사거리(m) — WeaponRangeIndicator.RangeOf(private static)를
        /// 슬래시 스케일 연동용으로 중복 정의(출처: Assets/Scripts/Systems/WeaponRangeIndicator.cs 184행 사거리 표와
        /// 동일 유지할 것). WeaponData 정적 스탯 단일 소스(타입 고정, 등급 배율 무관): Sword 2.5 / Spear 4 / Bow 10 / Fist 2.
        /// </summary>
        private static float RangeOf(ProjectName.Core.WeaponType type)
        {
            switch (type)
            {
                case ProjectName.Core.WeaponType.Sword: return ProjectName.Core.WeaponData.Sword.range;   // 2.5m
                case ProjectName.Core.WeaponType.Spear: return ProjectName.Core.WeaponData.Spear.range;   // 4m
                case ProjectName.Core.WeaponType.Bow:   return ProjectName.Core.WeaponData.Bow.range;     // 10m
                default:               return ProjectName.Core.WeaponData.Fist.range;    // 2m
            }
        }

        // ================================================================
        // 내부: alive 진단 콜백 (SlashAliveProbe → 러너) — VFX Graph 미출력 폴백 전환
        // ================================================================

        /// <summary>
        /// [2026-09-13 alive 진단] SlashAliveProbe가 aliveParticleCount<=0(미출력)을 감지하면 호출 —
        /// _vfxDeadFallbackActive 플래그를 세팅해 이후 PlaySlashStage 발화부터 구 Slash VFX 폴백 경로를
        /// 사용한다. 호출 1회로 충분(플래그는 멱등 — 중복 진단에도 안전).
        /// </summary>
        internal static void NotifyVfxDead()
        {
            _vfxDeadFallbackActive = true;
        }

        /// <summary>alive>0(정상 출력) 진단 시 플래그 해제 — VFX 복구 시 스타일라이즈드 경로로 자동 복귀.</summary>
        internal static void NotifyVfxAlive()
        {
            _vfxDeadFallbackActive = false;
        }

        /// <summary>
        /// 타 완료 시점 히트 VFX — [45차 P3 임팩트 단일화] TravisHit 전용 스폰을 제거하고 PlayImpact와
        /// 동일 단일 경로(FX/Impact/MagicHit — [2026-09-14(47차)] BasicHit 교체)로 위임한다.
        /// 스윙이 끝나는 지점(스테이지 경계 통과)에 1회 발화한다(HumanoidClipDriver 콤보 감시에서 호출).
        /// 호출부 시그니처/크로스 전용 쿨다운/static 캐시/파괴 패턴 유지. 프리팹 미설치 시 static 1회 경고 후 조용히 반환.
        /// [2026-09-12] 쿼드 오리엔테이션은 카메라 수평 빌보드 유지(후방 카메라 뒷면 미러링 = 역스윙 체감 차단).
        /// direction은 빌보드 폴백/로그용으로 유지.
        /// </summary>
        public static void PlayCross(Vector3 position, Vector3 direction)
        {
            // 스팸 방지 (동일 크로스 이중 발화 흡수 — 스윙/임팩트와는 독립 쿨다운)
            if (Time.time - _lastCrossSpawnTime < MIN_SPAWN_INTERVAL) return;
            _lastCrossSpawnTime = Time.time;

            // [2026-09-14(47차)] MagicHit 단일 경로 위임 — BasicHit 경로에서 교체(롤백: MagicHitResourcePath 상수)
            GameObject prefab = LoadImpactPrefab();
            if (prefab == null) return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // [2026-09-12 카메라 수평 빌보드] 히트 지점에서 쿼드 +Z를 카메라 시선 축(수평)에 정렬 —
            // 후방 카메라에서도 항상 정면에서 읽힌다(뒷면 미러링 = 역스윙 체감 차단). 폴백 동일.
            Vector3 faceDir = CameraHorizontalFaceDir(position, dir);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.LookRotation(faceDir));
            instance.name = "ImpactVFX_Cross";
            // [2026-09-14(47차)] TintParticles(골드) 제거 — MagicHit 에셋 고유 색상 유지(사용자가 이 에셋의 룩을 선택).
            // NormalizePurpleParticles는 유지(보라만 골드화이트로 안전망 — 잔여 보라 → 골드화이트 교체, 재생 전).
            NormalizePurpleParticles(instance);
            Debug.Log($"[SlashVFX] ✅ 크로스 FX 스폰 (단일 임팩트, pos={position}, faceDir={faceDir:F2}, 발화시각={Time.time:F2}s)");

            PlayAllParticleSystems(instance);
            DetectShaderErrorOnce(instance, "CrossSlash");
            ScheduleDestroy(instance);
        }

        /// <summary>
        /// 피격 임팩트 FX — [45차 P3 임팩트 단일화] Organic/Construct/그 외 모두 단일 프리팹으로 통일
        /// (기존 Construct → BasicHit2 분기 제거). [2026-09-14(47차)] 프리팹은 MagicHit(Guz Magic Hit 2)로 교체.
        /// type은 로그/인스턴스명 구분용으로만 유지.
        /// </summary>
        public static void PlayImpact(Vector3 position, CombatHitType type)
        {
            PlayImpactMulti(position, type, 1f);
        }

        /// <summary>[Phase E-2] 멀티 스케일 임팩트 — 크리티컬 1.5배 등 선택적 스케일.</summary>
        public static void PlayImpactMulti(Vector3 position, CombatHitType type, float scale)
        {
            // 스팸 방지 (동일 임팩트 이중 발화 흡수 — 스윙과는 독립 쿨다운)
            if (Time.time - _lastImpactSpawnTime < MIN_SPAWN_INTERVAL) return;
            _lastImpactSpawnTime = Time.time;

            // [2026-09-14(47차)] MagicHit 단일 로드 — MagicHitResourcePath 경로(기존 BasicHit 교체)
            GameObject prefab = LoadImpactPrefab();
            if (prefab == null) return;

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = $"ImpactVFX_{type}";
            if (scale > 1.0001f)
            {
                instance.transform.localScale = Vector3.one * scale;   // 크리 1.5배 확대 — 지속시간/수명은 파티클 에미션 길이 그대로 보존
                Debug.Log($"[SlashVFX] 임팩트 스케일 {scale:F2}x (type={type})");
            }
            // [2026-09-14(47차)] TintParticles(흰) 제거 — MagicHit 에셋 고유 색상 유지(사용자가 이 에셋의 룩을 선택).
            // [2026-09-13 보라 정규화] 그라디언트 등에
            // 남은 보라를 골드화이트로 교체 — 보라 감지 색은 틴트 결과보다 우선 적용.
            NormalizePurpleParticles(instance);

            PlayAllParticleSystems(instance);
            DetectShaderErrorOnce(instance, "Impact");
            ScheduleDestroy(instance);
        }

        // ================================================================
        // 내부: 카메라 수평 빌보드 faceDir 산식 (슬래시/크로스 공용)
        // ================================================================

        /// <summary>
        /// [2026-09-12] 슬래시/크로스 쿼드 공용 faceDir — 카메라 수평 빌보드.
        /// +Z 정면 관례 — 카메라가 아크의 앞면에서 보도록(미러링 방지): 쿼드 +Z를
        /// 스폰 지점→카메라 축의 수평 성분에 정렬해 3인칭 후방 카메라에서도
        /// 제작사 의도의 정면 뷰가 나오게 한다(뒷면 미러링 = 역스윙 체감 원인 제거).
        /// 폴백: Camera.main 부재 또는 수평 성분 퇴화 시 -dir(수평화), 그것도 소실 시 Vector3.forward.
        /// </summary>
        private static Vector3 CameraHorizontalFaceDir(Vector3 position, Vector3 dir)
        {
            Vector3 faceDir;
            var cam = Camera.main;
            if (cam != null)
                faceDir = cam.transform.position - position;   // 스폰 지점→카메라 축 — 쿼드 +Z가 카메라를 향함(카메라가 +Z 정면에서 아크를 봄)
            else
                faceDir = -dir;                                 // 폴백: 카메라 부재 — 스윙 방향 역방향(캐릭터 쪽 정면 뷰)
            faceDir.y = 0f;                                     // 수평 빌보드 — 카메라 피치가 궤적 기울임(roll)에 섞이지 않게 제거
            if (faceDir.sqrMagnitude < 0.0001f)
            {
                faceDir = -dir;                                 // 퇴화(카메라가 스폰 지점 수직 상하) → dir 역방향 폴백
                faceDir.y = 0f;
            }
            if (faceDir.sqrMagnitude < 0.0001f) faceDir = Vector3.forward;   // 최종 방어 — dir 수직 등 수평 성분 완전 소실
            return faceDir.normalized;
        }

        // ================================================================
        // 내부: 지연 로드 + 캐시 (실패 경고 1회)
        // ================================================================

        private static GameObject LoadSlashPrefab()
        {
            if (_slashPrefab != null) return _slashPrefab;
            if (_slashLoadFailed) return null;

            _slashPrefab = Resources.Load<GameObject>(SlashResourcePath);
            if (_slashPrefab == null)
            {
                _slashLoadFailed = true;
                Debug.LogWarning("[SlashVFX] 로드 실패(1회만 경고): Resources/FX/Slash/Slash VFX — 에디터 메뉴 Tools/VFX/Install Slash+Impact to Resources 실행 필요");
            }
            return _slashPrefab;
        }

        // [2026-09-14(47차)] 로더 스왑 — BasicHit → MagicHit(캐시/1회 경고/정적 플래그 패턴 동일 유지).
        // 프리팹은 수동 복사본(Resources/FX/Impact/MagicHit.prefab — StylizedSlash 선례, 인스톨러 대상 아님).
        // 로드 실패 시 크래시 없이 조용히 반환.
        private static GameObject LoadImpactPrefab()
        {
            if (_magicHitPrefab != null) return _magicHitPrefab;
            if (_magicHitLoadFailed) return null;

            _magicHitPrefab = Resources.Load<GameObject>(MagicHitResourcePath);
            if (_magicHitPrefab == null)
            {
                _magicHitLoadFailed = true;
                Debug.LogWarning("[SlashVFX] 로드 실패(1회만 경고): Resources/FX/Impact/MagicHit — 'Assets/Matthew Guz/Hits Effects FREE/Prefab/Magic Hit 2.prefab' 복사 필요");
            }
            return _magicHitPrefab;
        }

        // [2026-09-14(47차 후속6)] LoadStylizedSlashPrefab/프리팹 캐시 제거 — 깨진 참조 프리팹은 Instantiate 시
        // VisualEffect 컴포넌트 누락 확인(assetNull/alive=-1 패턴) → 런타임 빌드로 전환(PlaySlashStage 본문 참조).
        // 스타일라이즈드 소스는 StylizedVfxResourcePath(.vfx 에셋) 단일 경로로 확정.

        // ================================================================
        // 내부: 파티클 재생 / 재색상 / 셰이더 감지 / 파괴 예약
        // ================================================================

        /// <summary>
        /// [2026-09-12 P2 피격 FX 재색상] 공용 헬퍼 — 외부 VFX 팩 프리팹("Multiple Slashes" 십자가,
        /// "BasicHit"/"BasicHit2" 임팩트, "Slash VFX" 스윙)의 보라·자주 기본색은 사용자 취향
        /// ("피격시 보라색 점들이 떠다니는 건 별로")과 불일치하므로, Instantiate 직후 root 및
        /// 자식 전체 ParticleSystem을 순회해 main.startColor를 baseColor±variance 랜덤 틴트
        /// (MinMaxGradient flat — 시스템별 1회 추첨)로 교체한다. 파티클 텍스처는 곱셈(multiply)
        /// 조명이라 흰 텍스처에서 틴트가 그대로 드러난다. 알파는 원본 startColor가 flat 단색인
        /// 경우에만 보존해 팩 프리팹의 페이드 알파 훼손을 막는다(그 외 모드는 baseColor 알파).
        /// </summary>
        internal static void TintParticles(GameObject root, Color baseColor, float variance)
        {
            if (root == null) return;

            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;

                // main 모듈 1회 캐시 — ps.main 반복 접근 비용 회피(Unity 권장 패턴)
                var main = ps.main;

                // 원본 알파 보존 — flat 단색 모드에서만 신뢰 가능(그라디언트 모드는 알파 커브가 있어 폐기)
                float alpha = baseColor.a;
                if (main.startColor.mode == ParticleSystemGradientMode.Color)
                    alpha = main.startColor.color.a;

                // baseColor 채널별 ±variance 랜덤 틴트 → flat MinMaxGradient로 교체
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(
                    Mathf.Clamp01(baseColor.r + Random.Range(-variance, variance)),
                    Mathf.Clamp01(baseColor.g + Random.Range(-variance, variance)),
                    Mathf.Clamp01(baseColor.b + Random.Range(-variance, variance)),
                    Mathf.Clamp01(alpha)));
            }
        }

        /// <summary>
        /// [2026-09-13 보라 정규화] 외부 팩 프리팹의 잔여 보라 계열 색을 감지해 골드화이트로 교체.
        /// TintParticles는 main.startColor만 평탄화하므로 colorOverLifetime 그라디언트 등
        /// 모듈에 남은 보라가 사용자 리포트("보라색 점들")의 잔여 원인 — 스폰 직후 1회
        /// 저비용 순회(파티클 시스템 수 적음)로 정규화한다. 40차 틴트 상수 뒤에 적용되며
        /// 보라 판정에 걸린 색은 틴트 결과보다 정규화가 우선된다.
        /// 판정: r>0.4 && b>0.4 && g<r*0.55 && g<b*0.55 (보라/자주 계열) → (1,0.9,0.6) 골드화이트.
        /// </summary>
        internal static void NormalizePurpleParticles(GameObject root)
        {
            if (root == null) return;

            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;

                // 1) main.startColor — flat 단색 모드만 판정(그라디언트 모드는 아래 2)에서 처리)
                var main = ps.main;
                if (main.startColor.mode == ParticleSystemGradientMode.Color)
                {
                    Color c = main.startColor.color;
                    if (IsPurple(c))
                        main.startColor = new ParticleSystem.MinMaxGradient(ToGoldWhite(c));
                }

                // 2) colorOverLifetime 그라디언트 — 팩 프리팹 보라 잔여의 주 범인 모듈
                var col = ps.colorOverLifetime;
                if (col.enabled && col.color.mode == ParticleSystemGradientMode.Gradient)
                {
                    Gradient g = col.color.gradient;
                    GradientColorKey[] colorKeys = g.colorKeys;
                    bool changed = false;
                    for (int i = 0; i < colorKeys.Length; i++)
                    {
                        if (IsPurple(colorKeys[i].color))
                        {
                            colorKeys[i] = new GradientColorKey(ToGoldWhite(colorKeys[i].color), colorKeys[i].time);
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        g.SetKeys(colorKeys, g.alphaKeys);
                        col.color = new ParticleSystem.MinMaxGradient(g);
                    }
                }
            }
        }

        /// <summary>보라/자주 계열 판정 — r·b 모두 높고 g가 양쪽의 절반 이하(채널 균형 붕괴).</summary>
        private static bool IsPurple(Color c)
            => c.r > 0.4f && c.b > 0.4f && c.g < c.r * 0.55f && c.g < c.b * 0.55f;

        /// <summary>보라 → 골드화이트(1,0.9,0.6) 교체. 원본 알파는 보존해 페이드 훼손 방지.</summary>
        private static Color ToGoldWhite(Color original)
            => new Color(1f, 0.9f, 0.6f, original.a);

        /// <summary>일부 프리팹은 자체 재생 안 함 → 모든 ParticleSystem을 명시 재생.</summary>
        private static void PlayAllParticleSystems(GameObject instance)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems)
            {
                if (ps != null)
                    ps.Play(true);
            }
        }

        /// <summary>첫 인스턴스에서 Renderer 스캔 — InternalErrorShader(마젠타) 발견 시 1회 경고.</summary>
        private static void DetectShaderErrorOnce(GameObject instance, string label)
        {
            if (_shaderErrorWarned) return;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials == null) continue;

                foreach (Material mat in materials)
                {
                    if (mat == null || mat.shader == null) continue;

                    if (mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        _shaderErrorWarned = true;
                        Debug.LogWarning($"[SlashVFX] ⚠️ InternalErrorShader 감지(마젠타, 1회만 경고): {label} — {mat.name}");
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 숨은 호스트로 코루틴을 돌려 지정 시간(delay) 후 파괴 예약 (파티클/VFX가 끝나도 GO 잔존 없음).
        /// [2026-09-13] delay 파라미터화 — 기존 3s(DESTROY_AFTER)와 스타일라이즈드 1.5s(StylizedSlashDestroyAfter) 공용.
        /// </summary>
        private static void ScheduleDestroy(GameObject instance, float delay = DESTROY_AFTER)
        {
            EnsureHost();
            if (_host != null)
                _host.StartCoroutine(DestroyAfterDelay(instance, delay));
            else
                Object.Destroy(instance, delay); // 폴백: 호스트 생성 실패 시 예약 파괴
        }

        private static System.Collections.IEnumerator DestroyAfterDelay(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (instance != null)
                Object.Destroy(instance);
        }

        /// <summary>숨은 호스트 지연 생성 (HitVFX.HitFlashRunner 패턴: 비활성 생성 → 활성화로 Awake 타이밍 이슈 회피).</summary>
        private static void EnsureHost()
        {
            if (_host != null) return;

            var hostGo = new GameObject("SlashFxHost")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            hostGo.SetActive(false);
            _host = hostGo.AddComponent<SlashFxHost>();
            hostGo.SetActive(true);
        }
    }

    /// <summary>
    /// SlashVFXRunner의 내부 숨은 호스트 — static 클래스가 코루틴을 실행하기 위한 컨테이너.
    /// HideAndDontSave로 씬 언로드에도 유지, 3초 파괴 예약 코루틴만 담당.
    /// </summary>
    internal class SlashFxHost : MonoBehaviour
    {
    }

    /// <summary>
    /// [2026-09-13 alive 진단 → 2026-09-14(47차) 강화] StylizedSlash(VFX Graph) 인스턴스에 부착되는 진단 러너 —
    /// 스폰 후 0.35s와 1.0s 두 시점에 aliveParticleCount를 확인해 VFX Graph 미출력을 판정한다.
    ///   - 어느 한 시점이라도 alive>0 : 정상 출력 — NotifyVfxAlive() 후 코루틴 즉시 종료(폴백 미발동).
    ///     (Reinit+Play 명시 기동으로 alive가 0보다 커지면 폴백 없음 — PlaySlashStage 본문 참조)
    ///   - 두 체크 모두 alive<=0 : 폴백 전환(마지막 안전망) — 늦게 피는 이펙트 오판 방지를 위해 2회 연속 조건.
    ///     alive=-1(not awake) 2회 연속이면 별도 진단 로그("VFX 미각성 지속 — 에셋 로드/타겟 확인 필요") 출력.
    /// 진단 로그는 assetNull/assetAssigned/awake/alive를 함께 기록해 다음 Play에서 원인 즉시 판별이 가능하다
    /// ([47차 후속2] assetAssigned 추가 — 할당 실패(Resources 로드 null)와 참조 결함을 즉시 구분).
    /// SlashFxHost와 동일한 파일 내부 보조 MonoBehaviour 패턴(진단에 지연이 필요해 컴포넌트로 분리).
    /// </summary>
    internal sealed class SlashAliveProbe : MonoBehaviour
    {
        /// <summary>1차 진단 시점(스폰 후 초) — 첫 방출 파티클이 확실히 살아있어야 할 시점(튜닝 상수).</summary>
        private const float DiagnoseDelay = 0.35f;
        /// <summary>2차 진단 시점(스폰 후 초) — 늦게 피는 이펙트 오판 방지용 2차 관찰 시점(튜닝 상수).</summary>
        private const float DiagnoseDelaySecond = 1.0f;

        private void Start()
        {
            // Start에서 0.35s/1.0s 2시점 진단 — static 러너는 코루틴을 못 돌리므로 자기 인스턴스 코루틴으로 실행.
            StartCoroutine(Diagnose());
        }

        private System.Collections.IEnumerator Diagnose()
        {
            // ── 1차 체크 @0.35s ──
            yield return new WaitForSeconds(DiagnoseDelay);

            var ve = GetComponent<VisualEffect>();
            int alive = ve != null ? ve.aliveParticleCount : -1;
            // [47차 후속2] assetAssigned 필드 추가 — assetNull=True의 원인(할당 실패 vs 프리팹 참조 결함)을
            // 다음 Play에서 즉시 구분(assetAssigned=False → 에셋 미할당/로드 실패, True+assetNull=True → 참조 결함).
            Debug.Log($"[SlashVFX] VFX 진단: assetNull={ve == null || ve.visualEffectAsset == null} assetAssigned={ve != null && ve.visualEffectAsset != null} awake={ve != null && ve.HasAnySystemAwake()} alive={alive} @0.35s");
            if (alive > 0)
            {
                SlashVFXRunner.NotifyVfxAlive();   // 정상 출력 — 폴백 플래그 해제(VFX 복구 시 스타일라이즈드 경로 자동 복귀)
                yield break;   // 정상 — 2차 체크 불필요
            }

            // ── 2차 체크 @1.0s — 1차 미출력이어도 늦게 피는 이펙트일 수 있어 관찰 연장(스폰 기준 총 1.0s) ──
            yield return new WaitForSeconds(DiagnoseDelaySecond - DiagnoseDelay);

            ve = GetComponent<VisualEffect>();
            int alive2 = ve != null ? ve.aliveParticleCount : -1;
            Debug.Log($"[SlashVFX] VFX 진단: assetNull={ve == null || ve.visualEffectAsset == null} assetAssigned={ve != null && ve.visualEffectAsset != null} awake={ve != null && ve.HasAnySystemAwake()} alive={alive2} @1.0s");
            if (alive2 > 0)
            {
                SlashVFXRunner.NotifyVfxAlive();   // 늦게라도 기동 성공 — 폴백 미발동
                yield break;
            }

            // ── 두 체크 모두 alive<=0 → 폴백 전환(마지막 안전망) ──
            // [2026-09-14(47차)] 폴백 조건 강화: 단일 0.35s 판정 → 2시점 연속 미출력만 전환해 늦게 피는
            // 이펙트 오판을 차단. Reinit+Play 명시 기동으로 alive>0이 되면 여기에 도달하지 않는다(폴백 미발동).
            SlashVFXRunner.NotifyVfxDead();
            if (alive < 0 && alive2 < 0)
                Debug.Log("[SlashVFX] VFX 미각성 지속 — 에셋 로드/타겟 확인 필요");   // alive=-1(not awake) 2회 연속 — 기동 실패/프리팹 문제
            Debug.Log($"[SlashVFX] VFX Graph 미출력 확정(2회 연속 alive<=0) — 구 Slash VFX 폴백 전환");
        }
    }
}
