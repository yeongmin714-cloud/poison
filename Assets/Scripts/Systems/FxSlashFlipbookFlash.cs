using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 37-S3] 참격 플립북 플래시 — 히트 지점 크로스 슬래시 아크(Guz 임팩트 위 보완 레이어 — 교체 아님).
    /// 37-F1(MagicHit 보라 재색)과 짝: 에셋 톤은 팔레트로 정렬, 부족한 "참격 궤적"은 절차 플립북으로 보강.
    /// 계약: 유한 수명(머티리얼 인스턴스 파괴 — 누출 금지), EditMode 가드(21-K1 선례), Random 0건,
    /// 시트 애니 10프레임(80-P1 갱신 — 37-S3 아틀라스 256px·4×4·1024×1024, 연기와 동일 결정론 파이프라인),
    ///   33-A1 라이트와 무관(별도 계층).
    /// [무번호 80-P1/P2] 아크 전용 상수 분리 + 크리 전용 아틀라스(B안 분리) — 정상 256px·10프레임(141차 표준 갱신),
    ///   크리만 512px 셀(4×4·2048×2048) 전용 아틀라스+머티리얼(형상 위계: −75°→+75°·코어 20°·장반경 0.64).
    ///   부팅 선생성(Warmup — CoreSystemsBootstrap.Awake)으로 전투 중 히치 흡수.
    /// </summary>
    public static class FxSlashFlipbookFlash
    {
        private const float Lifetime = 0.7f;   // [무번호 38] 0.55→0.7 — 가시 창 확대
        private static float _lastKillLog;     // [무번호 79] 킬스위치 차단 로그 1/s 스로틀
        // [무번호 79] SlashArcMat 세션 캐시 — 히트마다 신규 머티리얼 생성(셰이더 변형 컴파일 채널)이 사용자
        //   세션에서 알파 강제 1(불투명 웜화이트 셀·계단형 합성 — F7 킬스위치로 근원 확정)을 낳는 경쟁
        //   상태를 만든다. 48-T3 수리 캐시 선례(수리마다 인스턴스 생성 금지)로 세션 1회 생성 고정.
        private static Material _slashArcMat;
        private static Material _critArcMat;   // [무번호 80-P2] 크리 전용 세션 캐시(48-T3 선례 — 1머티리얼/세션)

        /// <summary>[무번호 79] 진단 접근자 — 센티널 [79-ARC] 덤프가 캐시 머티리얼 ID를 비교할 때 사용.</summary>
        internal static Material DebugCachedMat => _slashArcMat;

        /// <summary>히트 지점 아크 플래시 발화 — PlayHitFXInternal에서 Guz 임팩트 직후 호출.
        /// [무번호 70-U3] [79-R20] 색 통일 — 일반/크리 모두 FXPalette.AttackPrimary(공격 이펙트 통일색).</summary>
        public static void Play(Vector3 position, float scale, bool isCrit = false)
        {
            if (!Application.isPlaying) return;   // EditMode 가드
            // [무번호 79] 진단 킬스위치 — MagentaSentinel F7 토글. 베이지 사각 근원 확정용(아크 발화 생략).
            if (MagentaSentinel.KillSlashArcFlash)
            {
                if (Time.unscaledTime - _lastKillLog > 1f)
                {
                    _lastKillLog = Time.unscaledTime;
                    Debug.LogWarning("[79-KILL] SlashArcFlash 발화 차단됨 (킬스위치 ON — 베이지 근원 확정 진단 중, 1/s 스로틀)");
                }
                return;
            }
            // [무번호 79] 머티리얼 세션 캐시 — 첫 발화에서 1회 생성, 이후 재사용(버텍스 스트림 정합은 멱등 69-P2).
            // [무번호 80-P2] B안(분리) — 크리 전용 아틀라스(512px 셀)·머티리얼 캐시. 정상 경로는 37-S3 계약 무변동.
            EnsureMaterials(out var mat, out var tilesX, out var tilesY, isCrit);
            if (mat == null) return;

            var go = new GameObject("SlashArcFlash");
            go.transform.position = position + Vector3.up * 0.6f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // [무번호 48-T1] 자동 재생 정지 — duration 설정 에러 차단(동일 패턴 예방)
            var main = ps.main;
            main.duration = Lifetime; main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.startSize = 2.6f * Mathf.Max(0.01f, scale);   // [무번호 38] 가시성 승격(1.5→2.6 — 영상 판독 강도 3/10)
            // [무번호 70-U3 갱신 79-R20] 색 통일 — 일반/크리 모두 FXPalette.AttackPrimary(크리 구분은 크기 앰프+블룸 라이트 위계).
            //   알파는 1로 정규화 — colorOverLifetime 그라디언트와의 이중 감쇠 방지.
            main.startColor = new Color(FXPalette.AttackPrimary.r, FXPalette.AttackPrimary.g, FXPalette.AttackPrimary.b, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });   // [무번호 38] 선명화
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var sheet = ps.textureSheetAnimation; sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = tilesX;   // [무번호 80-P2] 정상=ArcAtlasColumns / 크리=CritAtlasColumns(37-S3 계약 갱신)
            sheet.numTilesY = tilesY;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 1f);   // 수명 전체 6프레임 순환(1패스)
            var rnd = go.GetComponent<ParticleSystemRenderer>();
            // [무번호 69-P2] 플립북 AnimFrame 스트림 정합 — 시트애니 활성 파티클이 AnimFrame 미바인딩이면
            //   URP Particles/Unlit이 가비지 타일을 샘플링해 블랙/베이지 박스로 렌더(69 바이섹션 실측:
            //   SlashArcFlash 억제 시 결함 1,344→61px — 테스트 13/14의 베이지·암흑 계단식 사각 잔여분).
            FxMaterialLibrary.AlignParticleVertexStreams(rnd);
            // [무번호 47-b] 가드 경로 — 43차 sharedMaterial 세터도 세션 파손 NRE 후보(DeathVFX 46 실측 동일 뿌리).
            //   실패 시 원본 복원+1급 로그, GO는 기존 유한 수명 파괴로 정리(유령 GO 금지).
            FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, "FxSlashFlipbookFlash");
            rnd.renderMode = ParticleSystemRenderMode.Billboard;
            rnd.alignment = ParticleSystemRenderSpace.View;
            rnd.sortMode = ParticleSystemSortMode.Distance;
            ps.Emit(2);   // [무번호 38] 이중 아크(십자 교차 — 가시성 승격)
            // [무번호 79-R13] Emit 후 재정합 — 모듈 설정→정합→Emit 사이 Unity 자동 스트림 재계산이
            //   스트림을 어긋나게 재배치하는 세션 의존 사례(UV 붕괴 → 솔리드 박스)에 대한 즉시 방어.
            FxMaterialLibrary.AlignParticleVertexStreams(rnd);
            // [무번호 79-R13] 2프레임 재정합 가드 — LateUpdate에서 표준 스트림을 재적용(멱등·자기 소멸).
            go.AddComponent<ArcStreamGuard>().Bind(rnd);
            // [무번호 79] 머티리얼은 세션 캐시(_slashArcMat) — 발화마다 파괴 금지(파괴하면 캐시 무의미+재생성 경쟁 재발).
            if (Application.isPlaying) { Object.Destroy(go, Lifetime + 0.1f); }
            else { Object.DestroyImmediate(go); }   // 21-K1 가드 선례(에디터 경로 — Play 가드로 도달 불가, 안전 유지)
        }

        /// <summary>[무번호 80-P2] 아틀라스 선택 — B안(분리): 크리=전용 512px 셀 아틀라스, 정상=37-S3 계약 무변동.
        ///   머티리얼은 세션 캐시(48-T3 선례). Warmup 부팅 선생성 + Play 지연 폴백 이중 방어.</summary>
        private static void EnsureMaterials(out Material mat, out int tilesX, out int tilesY, bool isCrit)
        {
            if (isCrit)
            {
                if (_critArcMat == null)
                    _critArcMat = FxMaterialLibrary.CreateSoftParticle("SlashArcCritMat", new Color(1f, 1f, 1f, 1f), FxFlipbookGenerator.GetCritSlashArcFlipbook());
                mat = _critArcMat;
                tilesX = FxFlipbookGenerator.CritAtlasColumns;
                tilesY = FxFlipbookGenerator.CritAtlasRows;
            }
            else
            {
                if (_slashArcMat == null)
                    _slashArcMat = FxMaterialLibrary.CreateSoftParticle("SlashArcMat", new Color(1f, 1f, 1f, 1f), FxFlipbookGenerator.GetSlashArcFlipbook());
                mat = _slashArcMat;
                tilesX = FxFlipbookGenerator.ArcAtlasColumns;
                tilesY = FxFlipbookGenerator.ArcAtlasRows;
            }
        }

        /// <summary>[무번호 80-P1] 부팅 선생성 — 크리 전용 아틀라스(512px 셀 2048×1024 픽셀 루프)를 전투 중
        ///   첫 크리 히치 없이 흡수. CoreSystemsBootstrap.Awake에서 호출. 결정론 캐시(EditMode 무해).</summary>
        public static void Warmup()
        {
            EnsureMaterials(out _, out _, out _, false);
            EnsureMaterials(out _, out _, out _, true);
        }

        /// <summary>[무번호 79-R13] 아크 스트림 재정합 가드 — 발화 후 3프레임 LateUpdate에서 표준 스트림을
        ///   재적용한다. Unity 모듈 자동 스트림 재계산(컬러/시트애니 토글 경유)이 정합 직후 스트림을 어긋나게
        ///   재배치하면 UV 붕괴→솔리드 박스(69-P2 동족, 사용자 세션 한정 재현)로 렌더된다. 재정합은 멱등이라
        ///   정상 상태에서 무접촉·무비용. 3프레임 후 자기 소멸(상시 부하 없음).</summary>
        private sealed class ArcStreamGuard : MonoBehaviour
        {
            private ParticleSystemRenderer _pr;
            private int _frames;
            public void Bind(ParticleSystemRenderer pr) { _pr = pr; }
            private void LateUpdate()
            {
                if (_pr == null) { Destroy(this); return; }
                FxMaterialLibrary.AlignParticleVertexStreams(_pr);
                if (++_frames >= 3) Destroy(this);
            }
        }
    }
}
