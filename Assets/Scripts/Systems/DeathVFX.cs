using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 2026-10-15: 사망 파티클 고품질화 — 에셋 전용 사망 버스트(46차 에셋-only 계약 준수).
    /// 이전: PlayKillVFX의 카메라 앞 사각 플래시(런타임 기본 파티클 = 사각형)가 유일한 사망 이펙트,
    /// 병사 사망에는 파티클 자체가 없었다.
    /// 이제: Hovl Studio "Sparks explode red"(혈흔 스파크 버스트) + "Smoke puff"(연기 포프)를
    /// Resources/FX/Death/ 수동 복사본으로 스폰한다(StylizedSlash/MagicHit 수동복사+guid 재발행 선례).
    /// - 셰이더: 팩 머티리얼 전부 builtin fileID 211(Legacy Shaders/Particles/Alpha Blended) —
    ///   MagicHit(47차)와 동일 계열, URP 무변환 렌더 실측.
    /// - 프리팹은 playOnAwake=1, looping=1 → 인스턴스 파괴 예약 필수.
    /// - scalingMode 0(Hierarchy) → transform.localScale 스케일 적용 유효.
    /// - 예산 캡: 대규모 전투(전쟁 시뮬/아레나) 동시 사망 스팸 방지 — CombatFXGate 창 예산 패턴 축소본.
    /// - 색: 에셋 고유 색 유지(47차 규약 — 룩을 위해 선택된 에셋 재색상 금지).
    /// </summary>
    public static class DeathVFX
    {
        private const string SparksResourcePath = "FX/Death/DeathSparks";
        private const string SmokeResourcePath = "FX/Death/DeathSmoke";

        /// <summary>looping 프리팹이라 GO 잔존 방지 파괴 예약(ScheduleDestroy 선례 — 수명 1~2s + 여유).
        /// [무번호 25-A1] 유한 수명 상한 클램프로 승계 — 실제 파괴 시점은 계산 duration(Clamp 2~3s) 사용.</summary>
        private const float DestroyAfter = 3f;

        /// <summary>[무번호 25-A1] 유한 수명 하한 — 전리품 등장 연기 무한 반복 버그의 재발 방지 하한 파괴 시점.</summary>
        private const float MinDestroySeconds = 2f;

        /// <summary>저사양 보호 — 초당 최대 사망 버스트 수. 초과분은 스폰 생략(시체 자체는 정상 정리됨).</summary>
        private const int MaxBurstsPerSecond = 8;

        private static GameObject _sparksPrefab;
        private static bool _sparksLoadFailed;
        private static GameObject _smokePrefab;
        private static bool _smokeLoadFailed;

        private static float _windowStart = -999f;
        private static int _spentThisWindow;
        private static bool _budgetWarned;

        /// <summary>[무번호 29-M3-2] D1 플립북 스모크 버스트 — 시트 애니 6프레임(결정론 아틀라스).
        /// true=승격 경로 성공, false=머티리얼 불가(프리팹 폴백). 유한 수명 파괴(머티리얼 인스턴스 포함 — 누출 금지).
        /// Random 0건(상수 전용 — 결정론 계약).</summary>
        private static bool SpawnFlipbookSmoke(Vector3 position, float scale)
        {
            var mat = FxMaterialLibrary.CreateFlipbookSmoke();
            if (mat == null) return false;
            var go = new GameObject("DeathFlipbookSmoke");
            go.transform.position = position + Vector3.up * 0.6f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // [무번호 48-T1] 자동 재생 정지 — 재생 중 duration 설정 불가 에러 차단
            var main = ps.main;
            main.duration = 1.6f; main.loop = false;
            main.startLifetime = 1.4f; main.startSpeed = 0.8f;
            main.startSize = 2.2f * Mathf.Max(0.01f, scale);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, 2.2f);   // 확산
            var sheet = ps.textureSheetAnimation; sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = FxFlipbookGenerator.AtlasColumns;
            sheet.numTilesY = FxFlipbookGenerator.AtlasRows;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 1f);   // 수명 전체 프레임 순환(1패스)
            var rnd = go.GetComponent<ParticleSystemRenderer>();
            // [무번호 69-P2] 플립북 AnimFrame 스트림 정합 — 사망 연기 동일 계약(SlashArcFlash 바이섹션
            //   실측 선례: 시트애니 활성 + AnimFrame 미바인딩 = 가비지 타일 = 블랙/베이지 사각).
            FxMaterialLibrary.AlignParticleVertexStreams(rnd);
            // [무번호 48-T1] 중복 AddComponent 제거 — AddComponent<ParticleSystem>()가 ParticleSystemRenderer를
            //   자동 부착하므로 다시 AddComponent하면 "Can't add component" 에러 + null 반환(11-15 47차 Play
            //   실측 로그) → 머티리얼 대입 NRE/생략 = 마젠타 연기의 진짜 뿌리(세션 파손 가설 폐기).
            //   GetComponent로 자동 부착본을 사용한다. 46-T1 가드 경로 유지(실패 시 폴백 승계).
            bool assigned = false;
            try
            {
                if (rnd != null)
                    assigned = FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, "DeathFlipbookSmoke");
                if (assigned)
                {
                    rnd.renderMode = ParticleSystemRenderMode.Billboard;
                    rnd.alignment = ParticleSystemRenderSpace.View;
                    rnd.sortMode = ParticleSystemSortMode.Distance;
                    ps.Emit(3);
                }
                else
                {
                    Debug.LogWarning("[48-DEATHFX] 파티클 렌더러 획득 실패 — 에셋 프리팹 폴백으로 승계");
                }
            }
            catch (System.Exception smokeEx)
            {
                Debug.LogWarning($"[46-DEATHFX] 플립북 연기 렌더러 세팅 실패 — 에셋 프리팹 폴백으로 승계: {smokeEx.Message}");
            }
            const float life = 1.8f;
            if (assigned)
            {
                // 유한 수명 파괴(머티리얼 인스턴스 포함 — 누출 금지, 25-A1 계약 유지).
                if (Application.isPlaying) { Object.Destroy(go, life); Object.Destroy(mat, life); }
                else { Object.DestroyImmediate(go); Object.DestroyImmediate(mat); }   // 21-K1 EditMode 가드 선례
                return true;
            }
            // [무번호 46-T1] 대입 실패 = 유령 GO 금지 — 즉시 정리 후 false(→ DeathSmoke 프리팹 폴백).
            // 기존에는 NRE가 Destroy 예약 자체를 생략해 마젠타 빌보드가 무기한 잔존했다(성장형 덩어리 실측).
            if (Application.isPlaying) { Object.Destroy(go); Object.Destroy(mat); }
            else { Object.DestroyImmediate(go); Object.DestroyImmediate(mat); }
            return false;
        }

        // ================================================================
        // 공개 진입점
        // ================================================================

        /// <summary>
        /// 사망 지점에 에셋 전용 사망 버스트를 스폰한다.
        /// 호출부: AnimalAI.Die → PlayKillVFX(② 단계 대체), GuardPlaceholder.Die / SkeletonGuardPlaceholder.Die /
        /// DraculaLord.Die (병사·스켈레톤·영주 사망 신규 배선).
        /// </summary>
        /// <param name="position">사망 지점(일반적으로 몸통 높이 = position + up*1.0).</param>
        /// <param name="scale">배율 — 몬스터 1.0, 병사 0.9, 보스(영주) 1.6.</param>
        public static void PlayDeathVFX(Vector3 position, float scale = 1f)
        {
            if (!TryConsumeBudget()) return;
            // [무번호 57-P2] FX 스폰 레지스트리 — 사망 버스트 수명 수 초로 역산 시점 전 소멸 → 스폰 좌표 기록.
            MagentaSentinel.RegisterFxSource("DeathVFX", position);

            // ① 혈흔 스파크 버스트 (Sparks explode red — 크기 0.08 / 속도 7 / 수명 1s, 스케일 1.0 그대로)
            GameObject sparks = LoadPrefab(ref _sparksPrefab, ref _sparksLoadFailed, SparksResourcePath);
            if (sparks != null)
            {
                var sparksGo = Object.Instantiate(sparks, position, Quaternion.identity);
                sparksGo.name = "DeathSparks";
                ApplyScale(sparksGo, scale);
                float sparksLife = PlayAllParticleSystems(sparksGo);   // [무번호 25-A1] 유한 수명 재생 — 수명 반환
                Object.Destroy(sparksGo, sparksLife);
            }

            // ② 연기 포프 — [무번호 29-M3-2] D1 절차 플립북 스모크 승격(1차 경로 — 결정론 시트 애니).
            //    플립북 머티리얼 불가 환경 = 기존 DeathSmoke 프리팹 폴백(리소스 부재 무손상 계약). EA2/유한 수명 계약 계승.
            if (!SpawnFlipbookSmoke(position, scale))
            {
                GameObject smoke = LoadPrefab(ref _smokePrefab, ref _smokeLoadFailed, SmokeResourcePath);
                if (smoke != null)
                {
                    var smokeGo = Object.Instantiate(smoke, position, Quaternion.identity);
                    smokeGo.name = "DeathSmoke";
                    ApplyScale(smokeGo, scale * 0.4f);
                    float smokeLife = PlayAllParticleSystems(smokeGo);     // [무번호 25-A1] 유한 수명 재생 — 수명 반환
                    Object.Destroy(smokeGo, smokeLife);
                }
            }

            // ③ [무번호 28-M3] 사망 지면 링 잔광 — 기존 sparks/smoke 1단에 지면 링 1겹 추가(U-2 표준 밴드·R4 깊이 페이드 계승).
            //   색은 몬스터 액센트 팔레트(25-IN 계승) — 로컬 리터럴 금지. 예산: MaxBurstsPerSecond 게이트 내부에 두어
            //   스팸 방지 기존 예산 로직을 그대로 공유한다(링은 파티클과 무관하나 스폰 빈도는 동일 사상 — 주석 명시).
            //   삽입 위치 실측: 예산 게이트(TryConsumeBudget) 통과 후 — 생략 경로 공유(예산 초과 스킵 시 링도 함께 생략,
            //   파티클 없이 링만 남는 불일치 방지). EA2 계약 무손상 — PlayDeathVFX 시그니처/호출부 변경 없음.
            try
            {
                ShockwaveRingFX.SpawnImpactRing(
                    new Vector3(position.x, 0.05f, position.z),
                    1.2f * Mathf.Max(0.4f, scale),
                    FXPalette.MonsterAccent,
                    0.5f,
                    elite: false);
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 흐름 절대 방해 금지(23-V1 선례)

            Debug.Log($"[DeathVFX] 사망 버스트 스폰 at {position} scale={scale:F2}");
        }

        // ================================================================
        // 내부 헬퍼
        // ================================================================

        private static GameObject LoadPrefab(ref GameObject cache, ref bool loadFailed, string path)
        {
            if (cache != null) return cache;
            if (loadFailed) return null;

            cache = Resources.Load<GameObject>(path);
            if (cache == null)
            {
                loadFailed = true;
                Debug.LogWarning($"[DeathVFX] 로드 실패(1회만 경고): Resources/{path} — Hovl 프리팹 수동 복사 필요");
            }
            return cache;
        }

        /// <summary>scalingMode Hierarchy(0) 프리팹 — 루트 localScale로 전체 스케일.</summary>
        private static void ApplyScale(GameObject instance, float scale)
        {
            if (instance == null) return;
            if (Mathf.Abs(scale - 1f) < 0.0001f) return;
            instance.transform.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// [무번호 25-A1] 유한 수명 재생 — 전리품 등장 연기 무한 반복 버그(죽은 자리에서 연기 계속 출력) 근원 차단.
        /// 이전: ps.Play(true)만 수행 → DeathSmoke burst 루핑(서브시스템 looping 타입)이 main.loop와 별개로
        /// 무한 방출되어 Instantiate 직접 호출 경로(파괴 예약 부재)에서 연기가 영원히 남았다.
        /// 이제: ① 전체 계층 main.loop=false 강제(루핑 원단 차단) ② 루핑 출신 서브시스템은 수명 시점 Stop
        /// (이중 보장 — 프리팹 파일 무변경, 46차 에셋 계약 유지) ③ 반환 수명=계산 duration(클램프 2~3s),
        /// 호출부 Object.Destroy(go, duration) 마무리 고정. PlayImpact 선례(전체 명시 재생) 유지.
        /// </summary>
        /// <returns>유한 수명(초) — 호출부 지연 파괴에 그대로 사용.</returns>
        private static float PlayAllParticleSystems(GameObject instance)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            bool anyLoopingOrigin = false;
            float duration = MinDestroySeconds;
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;
                var main = ps.main;
                if (main.loop) anyLoopingOrigin = true;
                main.loop = false;   // [무번호 25-A1] looping 강제 off — 무한 방출 원단 차단
                ps.Play(true);
                // 유한 수명 산정 — 사이클 + 시작 지연 + 입자 최대 수명(분포 최대값)
                float life = main.duration + main.startDelay.constantMax + main.startLifetime.constantMax;
                if (life > duration) duration = life;
            }
            duration = Mathf.Clamp(duration, MinDestroySeconds, DestroyAfter);   // 2~3s 유한 수명 계약
            // [무번호 25-A1] 루핑 출신 서브시스템 안전 정지 — 수명 시점 전체 Stop(이중 보장).
            if (anyLoopingOrigin)
                instance.AddComponent<FiniteLifetimeRunner>().Init(systems, duration);
            return duration;
        }

        /// <summary>
        /// [무번호 25-A1] 유한 수명 러너 — 수명 시점에 전체 파티클 시스템을 Stop(StopEmitting)하고 소멸.
        /// main.loop 강제 off의 이중 보장(서브이미터 연쇄 burst 등 잔여 루핑 경로 차단). GO 자체 수명은
        /// 호출부 Object.Destroy(go, duration) 소관 — 이 러너는 Stop 책임만 진다.
        /// </summary>
        private sealed class FiniteLifetimeRunner : MonoBehaviour
        {
            private ParticleSystem[] _systems;
            private float _stopAt;
            private float _elapsed;

            public void Init(ParticleSystem[] systems, float lifetimeSeconds)
            {
                _systems = systems;
                _stopAt = Mathf.Max(0.1f, lifetimeSeconds);
                _elapsed = 0f;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                if (_elapsed < _stopAt) return;
                if (_systems != null)
                {
                    foreach (ParticleSystem ps in _systems)
                    {
                        if (ps == null) continue;
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
                Destroy(this);   // 컴포넌트만 정리 — GO는 호출부 지연 파괴가 마무리
            }
        }

        /// <summary>
        /// 창 예산 (CombatFXGate.TryConsumeBudget 패턴) — Time.time 1초 창마다 보충,
        /// Update 불필요. 초과 시 스킵하고 최초 1회만 로그.
        /// </summary>
        private static bool TryConsumeBudget()
        {
            float now = Time.time;
            if (now - _windowStart >= 1f)
            {
                _windowStart = now;
                _spentThisWindow = 0;
            }

            if (_spentThisWindow >= MaxBurstsPerSecond)
            {
                if (!_budgetWarned)
                {
                    _budgetWarned = true;
                    Debug.Log($"[DeathVFX] 예산 소진({MaxBurstsPerSecond}/s) — 초과 사망 버스트 스킵");
                }
                return false;
            }

            _spentThisWindow++;
            _budgetWarned = false;
            return true;
        }
    }
}
