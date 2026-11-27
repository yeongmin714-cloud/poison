using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 74-F계] 낚시 라인·찌·릴 긴장·낚아올리기/스냅 소스 계약테스트.
    /// 계획서: docs/plans/2026-11-18_no74-action-interface-vfx-proposal.md F계.
    /// 검증: 마커 3종(74-F1/F2/F3)·FishingSystem 훅 6곳 배선·결정론(무작위 0건)·머티리얼 인스턴스 파괴·
    ///       isPlaying 가드(Destroy/DestroyImmediate)·FxEnvReaction internal 노출.
    /// 컴파일/Play 불요 — 소스 문자열 검사만(선례: CombatFxMissingLayersPhaseUVWXTests 패턴).
    /// </summary>
    public class FxFishingLineP74Tests
    {
        private const string VisualPath = "Assets/Scripts/Systems/FishingLineVisual.cs";
        private const string SystemPath = "Assets/Scripts/Systems/FishingSystem.cs";
        private const string EnvPath = "Assets/Scripts/Systems/FxEnvReaction.cs";

        // ── 마커 3종 ───────────────────────────────────────────────

        /// <summary>[74-F 마커] F1/F2/F3 항목 마커 각각 명기 계약.</summary>
        [Test]
        public void F_Markers_ThreePhases()
        {
            var src = System.IO.File.ReadAllText(VisualPath);
            Assert.IsTrue(src.Contains("[무번호 74-F1]"), "F1 낚싯대·낚싯줄·찌 마커");
            Assert.IsTrue(src.Contains("[무번호 74-F2]"), "F2 릴 긴장 시각화 마커");
            Assert.IsTrue(src.Contains("[무번호 74-F3]"), "F3 낚아올리기/스냅 마커");
        }

        // ── F1 — 낚싯대·낚싯줄·찌 ──────────────────────────────────

        /// <summary>[74-F1] BeginCast 정적 진입 + 1회성 런타임 오브젝트 + 라인 8점 + 결정론 rodTip 상수.</summary>
        [Test]
        public void F1_BeginCastContract()
        {
            var src = System.IO.File.ReadAllText(VisualPath);
            Assert.IsTrue(src.Contains("public static FishingLineVisual BeginCast"), "BeginCast 정적 진입");
            Assert.IsTrue(src.Contains("new GameObject(\"FishingLine\")"), "1회성 런타임 오브젝트");
            Assert.IsTrue(src.Contains("HideFlags.HideAndDontSave"), "히든 도넛세이브 — 씬 저장 오염 방지");
            Assert.IsTrue(src.Contains("private const int LinePointCount = 8"), "라인 8점");
            Assert.IsTrue(src.Contains("positionCount = LinePointCount"), "라인 분절 적용");
            Assert.IsTrue(src.Contains("private const float RodTipForward = 0.5f"), "낚싯대 끝 전방 0.5m(결정론)");
            Assert.IsTrue(src.Contains("private const float RodTipHeight = 1.4f"), "낚싯대 끝 높이 1.4m(결정론)");
            Assert.IsTrue(src.Contains("private const float CastDuration = 0.6f"), "캐스팅 0.6s");
            Assert.IsTrue(src.Contains("private const float CastArcHeight = 1.2f"), "포물선 제어점 +up*1.2");
            Assert.IsTrue(src.Contains("QuadraticBezier"), "2차 베지어 포물선");
            Assert.IsTrue(src.Contains("private const float FloatJitterAmplitude = 0.02f"), "찌 요동 진폭 0.02m");
            Assert.IsTrue(src.Contains("private const float FloatJitterPeriod = 2f"), "찌 요동 주기 2s");
            Assert.IsTrue(src.Contains("public void OnBite()"), "입질 훅 수신자");
        }

        // ── F2 — 릴 긴장 시각화 ────────────────────────────────────

        /// <summary>[74-F2] UpdateTension — 폭 0.025→0.085 lerp·색 파랑→경고 적 lerp·찌 드리프트·요동 배율.</summary>
        [Test]
        public void F2_ReelTensionContract()
        {
            var src = System.IO.File.ReadAllText(VisualPath);
            Assert.IsTrue(src.Contains("public void UpdateTension(float tension, float progress)"), "UpdateTension 시그니처");
            Assert.IsTrue(src.Contains("Mathf.Lerp(LineWidthMin, LineWidthMax, _tension)"), "라인 폭 lerp(tension)");
            Assert.IsTrue(src.Contains("private const float LineWidthMin = 0.025f"), "폭 하한 0.025m");
            Assert.IsTrue(src.Contains("private const float LineWidthMax = 0.085f"), "폭 상한 0.085m");
            Assert.IsTrue(src.Contains("Color.Lerp(LineColorCalm, LineColorDanger, _tension)"), "색 lerp 파랑→경고");
            Assert.IsTrue(src.Contains("private static readonly Color LineColorCalm = new Color(0.34f, 0.65f, 1.0f)"), "평온 파랑(0.34,0.65,1.0)");
            Assert.IsTrue(src.Contains("private static readonly Color LineColorDanger = new Color(0.9f, 0.25f, 0.2f)"), "경고 적색(0.9,0.25,0.2)");
            Assert.IsTrue(src.Contains("_progress * ReelDriftMax"), "찌 드리프트 progress 비례");
            Assert.IsTrue(src.Contains("private const float ReelDriftMax = 0.5f"), "드리프트 상한 0.5m");
            Assert.IsTrue(src.Contains("(1f + _tension * 2f)"), "요동 진폭 ×(1+tension*2)");
        }

        // ── F3 — 낚아올리기/스냅 ───────────────────────────────────

        /// <summary>[74-F3] 물기둥 스트릭 3본·0.4s 페이드 / 스냅 반동 0.25s 회수.</summary>
        [Test]
        public void F3_CatchSnapContract()
        {
            var src = System.IO.File.ReadAllText(VisualPath);
            Assert.IsTrue(src.Contains("public void PlayCatch()"), "PlayCatch — 낚아올리기");
            Assert.IsTrue(src.Contains("public void PlaySnap()"), "PlaySnap — 라인 끊김");
            Assert.IsTrue(src.Contains("private const int CatchStreakCount = 3"), "물기둥 스트릭 3본");
            Assert.IsTrue(src.Contains("private const float CatchDuration = 0.4f"), "물기둥 0.4s 페이드");
            Assert.IsTrue(src.Contains("private const float SnapDuration = 0.25f"), "스냅 0.25s 회수");
        }

        // ── 결정론 ─────────────────────────────────────────────────

        /// <summary>[74-F 결정론] 신규 파일 무작위 API 0건 — 사인/코사인 상수 파라미터만.</summary>
        [Test]
        public void F_Determinism_NoRandom()
        {
            var src = System.IO.File.ReadAllText(VisualPath);
            Assert.That(src.Contains("Random."), Is.False, "[무번호 74-F] 결정론 — Random API 0건");
        }

        // ── 수명/리소스 ────────────────────────────────────────────

        /// <summary>[74-F 머티리얼] 라인 머티리얼 폴백(Sprites/Default) + OnDestroy 인스턴스 파괴 필수.</summary>
        [Test]
        public void F_MaterialInstanceDestroyedInOnDestroy()
        {
            var src = System.IO.File.ReadAllText(VisualPath);
            Assert.IsTrue(src.Contains("private void OnDestroy()"), "OnDestroy 존재");
            Assert.IsTrue(src.Contains("Object.Destroy(_lineMat)"), "머티리얼 인스턴스 파괴(Play)");
            Assert.IsTrue(src.Contains("Object.DestroyImmediate(_lineMat)"), "머티리얼 인스턴스 파괴(EditMode)");
            Assert.IsTrue(src.Contains("Shader.Find(\"Sprites/Default\")"), "라인 머티리얼 폴백(FxMaterialLibrary 라인 헬퍼 부재)");
        }

        /// <summary>[74-F 수명] 자가 파괴 isPlaying 가드 — EditMode Destroy 에러 방지(156차 선례).</summary>
        [Test]
        public void F_IsPlayingGuard_DestroyPattern()
        {
            var src = System.IO.File.ReadAllText(VisualPath);
            Assert.IsTrue(src.Contains("Application.isPlaying) Object.Destroy(gameObject"), "자가 파괴 Play 가드");
            Assert.IsTrue(src.Contains("Object.DestroyImmediate(gameObject)"), "자가 파괴 EditMode 가드");
        }

        // ── 물튀김 입자 ────────────────────────────────────────────

        /// <summary>[74-F3 물튀김] EnsureSplashTexture 재사용 + 선례 패턴(물색·수명 0.4~0.5s·중력 0.35·1회 Emit).</summary>
        [Test]
        public void F_SplashParticleContract()
        {
            var src = System.IO.File.ReadAllText(VisualPath);
            Assert.IsTrue(src.Contains("FxEnvReaction.EnsureSplashTexture()"), "물튀김 텍스처 재사용(internal 노출)");
            Assert.IsTrue(src.Contains("FxMaterialLibrary.CreateSoftParticle"), "소프트 파티클 머티리얼 선례 재사용");
            Assert.IsTrue(src.Contains("private const int SplashParticleCount = 5"), "물튀김 입자 5");
            Assert.IsTrue(src.Contains("private const float SplashGravity = 0.35f"), "낙하 중력 0.35");
            Assert.IsTrue(src.Contains("new ParticleSystem.MinMaxCurve(0.4f, 0.5f)"), "수명 0.4~0.5s");
            Assert.IsTrue(src.Contains("private static readonly Color SplashWaterColor = new Color(0.65f, 0.9f, 1f, 0.8f)"), "물색(0.65,0.9,1,0.8)");
            Assert.IsTrue(src.Contains("emission.rateOverTime = 0f"), "1회 Emit — 지속 방출 없음");
            Assert.IsTrue(src.Contains("ps.Emit(count)"), "Emit 호출");
        }

        // ── FishingSystem 훅 6곳 배선 ──────────────────────────────

        /// <summary>[74-F 훅] ①출조 ②입질 ③릴 프레임 ④끊김 ⑤낚아올리기 ⑥해제(리셋/종료) 배선.</summary>
        [Test]
        public void F_FishingSystemHooks_SixSites()
        {
            var src = System.IO.File.ReadAllText(SystemPath);
            Assert.IsTrue(src.Contains("private FishingLineVisual _lineVisual;"), "비주얼 필드 선언");
            Assert.IsTrue(src.Contains("_lineVisual = FishingLineVisual.BeginCast("), "①출조 — BeginCast 배선");
            Assert.IsTrue(src.Contains("_lineVisual?.OnBite();"), "②입질 — OnBite 배선");
            Assert.IsTrue(src.Contains("_lineVisual?.UpdateTension(_reelTension, _reelProgress);"), "③릴 프레임 — UpdateTension 배선");
            Assert.IsTrue(src.Contains("_lineVisual?.PlaySnap();"), "④라인 끊김 — PlaySnap 배선");
            Assert.IsTrue(src.Contains("_lineVisual?.PlayCatch();"), "⑤낚아올리기 — PlayCatch 배선");
            Assert.That(CountOccurrences(src, "_lineVisual?.Release();"), Is.GreaterThanOrEqualTo(2),
                "⑥해제 배선 — FinishMinigame 리셋 + StopFishing(+재출조 방어)");
            Assert.That(CountOccurrences(src, "_lineVisual = null;"), Is.GreaterThanOrEqualTo(4),
                "소비/해제 경로마다 참조 null 정리");
        }

        /// <summary>[74-F 무손상] 낚시 로직(난이도 수치·입질 대기·사운드 발화) 원본 유지 — 비주얼 훅만 추가.</summary>
        [Test]
        public void F_FishingSystemLogicUntouched()
        {
            var src = System.IO.File.ReadAllText(SystemPath);
            Assert.IsTrue(src.Contains("_progressGain = 0.080f; _tensionCost = 0.070f;"), "일반 등급 난이도 원본 유지");
            Assert.IsTrue(src.Contains("Random.Range(_minWaitTime, _maxWaitTime)"), "입질 대기 원본 유지");
            Assert.IsTrue(src.Contains("GatherFeedbackSystem.PlayImpact(GatherFeedbackKind.FishCast, _castPoint, shake: false);"), "출조 사운드 원본 유지");
            Assert.IsTrue(src.Contains("GatherFeedbackSystem.PlayImpact(GatherFeedbackKind.FishBite, _castPoint, shake: true);"), "입질 사운드 원본 유지");
        }

        // ── FxEnvReaction internal 노출 ────────────────────────────

        /// <summary>[74-F 재사용] EnsureSplashTexture private→internal 1워드 변경 — 물튀김 텍스처 단일 소스.</summary>
        [Test]
        public void F_EnvReaction_SplashTextureInternal()
        {
            var src = System.IO.File.ReadAllText(EnvPath);
            Assert.IsTrue(src.Contains("internal static Texture2D EnsureSplashTexture()"), "EnsureSplashTexture internal 노출(1워드 변경)");
            Assert.That(src.Contains("private static Texture2D EnsureSplashTexture()"), Is.False, "구 private 시그니처 잔존 금지");
        }

        /// <summary>부분 문자열 발생 횟수 카운트(결정론).</summary>
        private static int CountOccurrences(string text, string token)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(token, idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += token.Length;
            }
            return count;
        }
    }
}
