using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 3단계 이펙트: 에셋 스토어 무료 프리팹 기반 스윙/피격 FX 정적 러너.
    ///   - 스윙: Free Slash VFX "Slash VFX.prefab" (Assets/Resources/FX/Slash/Slash VFX)
    ///   - 피격: Matthew Guz "Basic Hit" (Assets/Resources/FX/Impact/BasicHit)
    ///     [45차 P3 임팩트 단일화] BasicHit2(Construct)/TravisHit(크로스) 경로 제거 — BasicHit 단일 프리팹
    ///   - [2026-09-13 스타일라이즈드 스윙] Stylizer Slash VFX(slash5-HungNguyen) "white-yellow bolder"
    ///     (Assets/Resources/FX/Slash/StylizedSlash) — PlaySlashStage가 콤보 스윙 아크 발화(아래 상수/메서드 참조)
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
    /// (히트(TravisHit) 1.0,0.75,0.35 / 스윙 0.85,0.95,1.0 / 임팩트 0.8,0.2,0.2 — 하단 공용 헬퍼 참조).
    ///
    /// [2026-09-12 P2 크로스→TravisHit 교체] 타 완료 FX(PlayCross)는 Free Slash VFX 팩 "Multiple Slashes"에서
    /// "FX/Impact/TravisHit" 프리팹으로 교체(빌보드/쿨다운/파괴 패턴 유지). 스윙(Slash VFX)은 호출부
    /// (HumanoidClipDriver) 제거로 자연 데드화 — 스윙 궤적은 WeaponSwingTrail(무기 트레일)이 대체하며
    /// PlaySlash/LoadSlashPrefab은 함수 유지(데드).
    ///
    /// [45차 P3 임팩트 단일화] 타격 FX 3종 혼용(PlayImpact의 BasicHit/BasicHit2 분기 + PlayCross의 TravisHit)을
    /// BasicHit 단일 경로로 통일 — 프리팹 소스 분산에 의한 톤 불일치 해소. 틴트는 45차 BOTW 팔레트로 정렬:
    /// 코어=흰(1,1,1) / 액센트=골드(1,0.9,0.5) / 외곽=주황(1,0.55,0.2) — 크로스=골드, 임팩트=흰.
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

        /// <summary>스타일라이즈드 슬래시 스케일 배수(튜닝 상수) — 초기 1.2배, Play 판정 후 조정.</summary>
        private const float StylizedSlashScale = 1.2f;

        /// <summary>
        /// 스타일라이즈드 슬래시 자가 파괴 예약 시간(튜닝 상수) — VFX Graph가 자체 수명 후 완전 소진되므로
        /// 잔존 GO 정리용. ScheduleDestroy 파괴 예약 패턴 재사용(WeaponSwingTrail/기존 3s 파괴 선례).
        /// </summary>
        private const float StylizedSlashDestroyAfter = 1.5f;

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
        // [45차 P3 임팩트 단일화] TravisHit/BasicHit2 리소스 경로 제거 — BasicHit 단일 경로만 유지
        private const string BasicHitResourcePath = "FX/Impact/BasicHit";
        // [2026-09-13 P2 임팩트 Guz 단일 유지] 임팩트는 BasicHit 단일 프리팹으로 확정 — PlayImpact/PlayCross 모두
        // CoreTint(흰 1,1,1) 틴트 유지(아래 PlayImpact 내 TintParticles(CoreTint) 확인 완료).
        // "Basic Hit 8 (NEW)" 후보 에셋 존재 — Play 판정(실측) 후 검토 예정. 회귀 방지: 현시점 교체 없음(주석 명기).
        private const string StylizedSlashResourcePath = "FX/Slash/StylizedSlash";

        // ── 45차 P3: BOTW 팔레트 틴트 상수 — 코어=흰 / 액센트=골드 / 외곽=주황 ──
        /// <summary>[45차 P3: BOTW 팔레트] 코어 틴트 = 흰(1,1,1) — 임팩트/스윙 기본 톤.</summary>
        internal static readonly Color CoreTint = new Color(1f, 1f, 1f);
        /// <summary>[45차 P3: BOTW 팔레트] 액센트 틴트 = 골드(1,0.9,0.5) — 크로스(타 완료) 액센트 톤.</summary>
        internal static readonly Color AccentTint = new Color(1f, 0.9f, 0.5f);
        /// <summary>[45차 P3: BOTW 팔레트] 외곽 틴트 = 주황(1,0.55,0.2) — 외곽 연출 참조 톤(러너 내 직접 사용 없음).</summary>
        internal static readonly Color OuterTint = new Color(1f, 0.55f, 0.2f);

        // ── static 캐시/상태 ─────────────────────────────────────────
        private static GameObject _slashPrefab;
        private static GameObject _basicHitPrefab;   // [45차 P3] TravisHit/Construct(BasicHit2) 캐시 제거 — BasicHit 단일 캐시
        private static GameObject _stylizedSlashPrefab;   // [2026-09-13 스타일라이즈드 슬래시] 캐시 — LoadImpactPrefab 선례

        private static bool _slashLoadFailed;
        private static bool _basicHitLoadFailed;
        private static bool _stylizedSlashLoadFailed;   // 로드 실패 경고 static 1회 패턴
        private static bool _shaderErrorWarned;

        // 스팸 방지 타이머는 스윙/임팩트 분리: 같은 프레임에 스윙+임팩트가 연속 와도
        // 서로를 묵살하지 않게 하고, 동일 종류의 이중 발화(PlayerCombat+AttackSystem
        // 좌클릭 이중 처리 등)만 0.08s 쿨다운으로 흡수한다.
        private static float _lastSwingSpawnTime = -999f;
        private static float _lastImpactSpawnTime = -999f;
        private static float _lastCrossSpawnTime = -999f;   // 크로스 전용 쿨다운 — 스윙/임팩트와 독립
        private static float _lastStylizedSlashSpawnTime = -999f;   // [2026-09-13] 스타일라이즈드 전용 쿨다운(0.25s) — 3종과 독립
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
        /// [2026-09-13 스타일라이즈드 슬래시] 콤보 스윙 아크 FX — slash5-HungNguyen 팩 "white-yellow bolder" 프리팹
        /// (Resources/FX/Slash/StylizedSlash)을 콤보 스테이지별 오리엔테이션으로 발화한다(HumanoidClipDriver.
        /// FireComboSlash의 Player 전용 경로에서 호출). 골드 계열 VFX라 45차 BOTW 팔레트 액센트(AccentTint
        /// 1,0.9,0.5)와 자연 동조 — 별도 틴트 없이 원본색 사용이 선택 사유.
        /// 오리엔테이션(38차 규약): ① 루트를 카메라 수평 빌보드(CameraHorizontalFaceDir 재사용) ② stage 롤
        /// (1타 0 / 2타 -90 / 3타 -45 — 기존 규약, ComboStageRollDegrees) ③ yawSign 좌우 플립(아래 주석).
        /// VFX Graph(m_InitialEventName=OnPlay)는 Instantiate 직후 자동 재생되므로 명시 Play 불필요.
        /// 쿨다운 0.25s(스윙/크로스/임팩트와 독립), 자가 파괴 1.5s(StylizedSlashDestroyAfter).
        /// </summary>
        /// <param name="position">스윙 앵커(루트 기준 fwd 0.9 + up 1.2 — 38차 규격, 호출부 산출).</param>
        /// <param name="direction">콤보 방향(ComboStageDirection → 전방 반구 클램프 결과) — 빌보드 폴백/로그용.</param>
        /// <param name="stage">콤보 스테이지(1~3) — 롤 규약 적용 대상.</param>
        /// <param name="yawSign">스윙 yaw 부호(-1=좌, +1=우) — 아크 진행 좌우 플립 판정(호출부 ComboStageDirection 기반).</param>
        public static void PlaySlashStage(Vector3 position, Vector3 direction, int stage, float yawSign)
        {
            // 스팸 방지 — 스타일라이즈드 전용 쿨다운(0.25s, MIN_SPAWN_INTERVAL과 별개 상수): 콤보 타당 최소 간격 강제
            if (Time.time - _lastStylizedSlashSpawnTime < MIN_STYLIZED_SLASH_INTERVAL) return;
            _lastStylizedSlashSpawnTime = Time.time;

            GameObject prefab = LoadStylizedSlashPrefab();
            if (prefab == null) return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // ① 카메라 수평 빌보드 — 38차 규약(+Z 정면 관례, 후방 카메라 뒷면 미러링 차단)
            Vector3 faceDir = CameraHorizontalFaceDir(position, dir);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.LookRotation(faceDir));
            instance.name = "SlashVFX_Stylized";

            // ② [yawSign 좌우 플립 — 튜닝 포인트] yawSign<0(좌 스윙)이면 로컬 Y 180도 회전으로 아크 진행을 플립한다.
            // VFX 특성상(음수 localScale은 파티클 스폰 위치/벨로시티 미러링이 불안정해질 수 있음) localScale.x=-1보다
            // 회전을 우선한다. 단, Y 180도 회전은 쿼드 뒷면을 보여줄 수 있으므로 Play 판정에서 뒷면/미러가 어색하면
            // (a) 플립 기준 반전 또는 (b) localScale.x = -StylizedSlashScale 대체를 검토할 것.
            if (yawSign < 0f)
                instance.transform.Rotate(0f, 180f, 0f, Space.Self);

            // ③ [stage 롤 — 기존 규약] 1타 수평 0° / 2타 수직 -90° / 3타 사선 -45°(빌보드 후 로컬 Z 롤 = 화면축 기준 기울임)
            float roll = ComboStageRollDegrees(stage);
            if (Mathf.Abs(roll) > 0.01f)
                instance.transform.Rotate(0f, 0f, roll, Space.Self);

            // 스케일 튜닝 상수 — 초기 1.2배(Play 판정 후 조정). 플립이 회전 기반이므로 균일 스케일.
            instance.transform.localScale = Vector3.one * StylizedSlashScale;

            // [틴트 불가] 이 팩은 노출 프로퍼티(m_PropertySheet)가 비어 있어 런타임 틴트 불가 — TintParticles/
            // NormalizePurpleParticles(ParticleSystem 대상)도 무효(프리팹에 파티클 시스템 없음, VFX Graph 단독).
            // 원본 골드 계열이 곧 팔레트 Accent 톤이므로 무색상 처리로 확정.
            // VFX Graph는 OnPlay 초기 이벤트로 자동 재생 — 명시 재생 없음, 셰이더 오류 감지만 수행.
            DetectShaderErrorOnce(instance, "StylizedSlash");
            Debug.Log($"[SlashVFX] 스타일라이즈드 슬래시 발화 (stage={stage}, yawSign={yawSign})");   // 1회성 아님 — 발화마다(크로스 로그 선례)
            ScheduleDestroy(instance, StylizedSlashDestroyAfter);   // VFX 자체 종료 후 잔존 없음 — 1.5s 자가 파괴
        }

        /// <summary>콤보 스테이지별 아크 롤(기존 규약) — 1타 수평 0° / 2타 수직 -90° / 3타 사선 -45°. 3타 부호는 튜닝 포인트.</summary>
        private static float ComboStageRollDegrees(int stage)
            => stage == 2 ? -90f : (stage == 3 ? -45f : 0f);

        /// <summary>
        /// 타 완료 시점 히트 VFX — [45차 P3 임팩트 단일화] TravisHit 전용 스폰을 제거하고 PlayImpact와
        /// 동일 단일 경로(FX/Impact/BasicHit)로 위임한다(프리팹 소스 분산에 의한 톤 불일치 해소).
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

            // [45차 P3 임팩트 단일화] BasicHit 단일 경로 위임 — TravisHit 참조/로드 코드 제거
            GameObject prefab = LoadImpactPrefab();
            if (prefab == null) return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // [2026-09-12 카메라 수평 빌보드] 히트 지점에서 쿼드 +Z를 카메라 시선 축(수평)에 정렬 —
            // 후방 카메라에서도 항상 정면에서 읽힌다(뒷면 미러링 = 역스윙 체감 차단). 폴백 동일.
            Vector3 faceDir = CameraHorizontalFaceDir(position, dir);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.LookRotation(faceDir));
            instance.name = "ImpactVFX_Cross";
            // [45차 P3: BOTW 팔레트] 크로스 틴트 = 액센트 골드(1,0.9,0.5) — 타 완료 액센트 톤(재생 전 적용).
            TintParticles(instance, AccentTint, 0.15f);
            // [2026-09-13 보라 정규화] 잔여 보라 → 골드화이트 교체(재생 전).
            NormalizePurpleParticles(instance);
            Debug.Log($"[SlashVFX] ✅ 크로스 FX 스폰 (단일 임팩트, pos={position}, faceDir={faceDir:F2}, 발화시각={Time.time:F2}s)");

            PlayAllParticleSystems(instance);
            DetectShaderErrorOnce(instance, "CrossSlash");
            ScheduleDestroy(instance);
        }

        /// <summary>
        /// 피격 임팩트 FX — [45차 P3 임팩트 단일화] Organic/Construct/그 외 모두 BasicHit 단일 프리팹으로 통일
        /// (기존 Construct → BasicHit2 분기 제거). type은 로그/인스턴스명 구분용으로만 유지.
        /// </summary>
        public static void PlayImpact(Vector3 position, CombatHitType type)
        {
            // 스팸 방지 (동일 임팩트 이중 발화 흡수 — 스윙과는 독립 쿨다운)
            if (Time.time - _lastImpactSpawnTime < MIN_SPAWN_INTERVAL) return;
            _lastImpactSpawnTime = Time.time;

            // [45차 P3 임팩트 단일화] BasicHit 단일 로드 — BasicHitResourcePath 경로 유지
            GameObject prefab = LoadImpactPrefab();
            if (prefab == null) return;

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = $"ImpactVFX_{type}";
            // [2026-09-12 P2 피격 FX 재색상] 보라색 팩 파티클 재색상 — 사용자 취향 반영(피격 보라 부유 입자 제거).
            // [45차 P3: BOTW 팔레트] 임팩트 틴트 = 코어 흰(1,1,1) 정렬(기존 붉은 계열 0.8,0.2,0.2 교체). 재생 전 적용.
            TintParticles(instance, CoreTint, 0.2f);
            // [2026-09-13 보라 정규화] 틴트가 main.startColor만 평탄화하므로 그라디언트 등에
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

        // [45차 P3 임팩트 단일화] TravisHit 로더 제거 + LoadImpactPrefab의 Construct(BasicHit2) 분기 제거 —
        // BasicHit 단일 로더만 유지(캐시/1회 경고/정적 플래그 패턴 동일). 로드 실패 시 크래시 없이 조용히 반환.
        private static GameObject LoadImpactPrefab()
        {
            if (_basicHitPrefab != null) return _basicHitPrefab;
            if (_basicHitLoadFailed) return null;

            _basicHitPrefab = Resources.Load<GameObject>(BasicHitResourcePath);
            if (_basicHitPrefab == null)
            {
                _basicHitLoadFailed = true;
                Debug.LogWarning("[SlashVFX] 로드 실패(1회만 경고): Resources/FX/Impact/BasicHit — 인스톨러 실행 필요");
            }
            return _basicHitPrefab;
        }

        // [2026-09-13 스타일라이즈드 슬래시] slash5 팩 프리팹 로더 — LoadImpactPrefab 선례(캐시 + static 1회 경고) 동일 패턴.
        // 프리팹은 수동 복사본(Assets/Resources/FX/Slash/StylizedSlash.prefab) — 인스톨러 대상이 아니므로 재설치 무관.
        private static GameObject LoadStylizedSlashPrefab()
        {
            if (_stylizedSlashPrefab != null) return _stylizedSlashPrefab;
            if (_stylizedSlashLoadFailed) return null;

            _stylizedSlashPrefab = Resources.Load<GameObject>(StylizedSlashResourcePath);
            if (_stylizedSlashPrefab == null)
            {
                _stylizedSlashLoadFailed = true;
                Debug.LogWarning("[SlashVFX] 로드 실패(1회만 경고): Resources/FX/Slash/StylizedSlash — Assets/slash5-HungNguyen/prefab/slash/white-yellow bolder.prefab 복사 필요");
            }
            return _stylizedSlashPrefab;
        }

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
}
