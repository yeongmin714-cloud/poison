using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 19-Phase F] 이동 VFX 계약테스트 — F1 이동 먼지 트레일 + F2 이동 링 도착 진행도.
    /// F2 계약: [72차] 1초 규약 유지 — _life=1s 표시 구간 내 병사 접근 비율 링 축소만 시각화,
    /// 도착 소멸은 미적용(규약 우선). FadeSeconds=0.3/_life=1 불변.
    /// </summary>
    public class CombatFxMoveRingPhaseFTests
    {
        /// <summary>[F1] SpawnMoveTrailDust(Vector3) — 이동 먼지 트레일 스폰 메서드 존재.</summary>
        [Test]
        public void F1_SpawnMoveTrailDust_Exists()
        {
            var mi = typeof(ShockwaveRingFX).GetMethod("SpawnMoveTrailDust",
                new[] { typeof(Vector3) });
            Assert.IsNotNull(mi, "SpawnMoveTrailDust(Vector3)가 존재해야 합니다(무번호 19-F1)");
        }

        /// <summary>[F1] 기존 Spawn/SpawnDirectionalSpark 시그니처 불변 계약 유지.</summary>
        [Test]
        public void F1_ExistingSpawn_SignatureUnchanged()
        {
            var miSpawn = typeof(ShockwaveRingFX).GetMethod("Spawn",
                new[] { typeof(Vector3), typeof(float), typeof(Color), typeof(float) });
            Assert.IsNotNull(miSpawn, "Spawn(Vector3, float, Color, float) — 기존 시그니처 불변 계약");
            var miSpark = typeof(ShockwaveRingFX).GetMethod("SpawnDirectionalSpark",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(miSpark, "SpawnDirectionalSpark — 기존 메서드 불변 계약");
        }

        /// <summary>[F1] 트레일 상수 — 0.55m / 0.9s / 흙색(0.78,0.72,0.62,0.30).</summary>
        [Test]
        public void F1_TrailDust_Constants()
        {
            const System.Reflection.BindingFlags All = System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
            var radius = typeof(ShockwaveRingFX).GetField("TrailDustRadius", All);
            var duration = typeof(ShockwaveRingFX).GetField("TrailDustDuration", All);
            var color = typeof(ShockwaveRingFX).GetField("TrailDustColor", All);
            Assert.IsNotNull(radius, "TrailDustRadius 필드 존재");
            Assert.IsNotNull(duration, "TrailDustDuration 필드 존재");
            Assert.IsNotNull(color, "TrailDustColor 필드 존재");
            Assert.AreEqual(0.55f, (float)radius.GetValue(null), "TrailDustRadius=0.55m 계약");
            Assert.AreEqual(0.9f, (float)duration.GetValue(null), "TrailDustDuration=0.9s 계약");
            var c = (Color)color.GetValue(null);
            Assert.AreEqual(0.78f, c.r, 0.001f, "흙색 r");
            Assert.AreEqual(0.72f, c.g, 0.001f, "흙색 g");
            Assert.AreEqual(0.62f, c.b, 0.001f, "흙색 b");
            Assert.AreEqual(0.30f, c.a, 0.001f, "흙색 a(반투명)");
        }

        /// <summary>[F1] GuardPlaceholder — StepToward 이동 블록에서 SpawnMoveTrailDust 배선.</summary>
        [Test]
        public void F1_GuardPlaceholder_TrailDust_Wired()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.IsTrue(src.Contains("ShockwaveRingFX.SpawnMoveTrailDust"),
                "GuardPlaceholder에서 SpawnMoveTrailDust 호출 배선(무번호 19-F1)");
        }

        /// <summary>[F1] 트레일은 이동 중에만 — 도착 분기(step<=0 return) 이후 배선, 도착 시 미스폰.</summary>
        [Test]
        public void F1_TrailDust_MoveOnly_NotAtArrival()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            int idxCall = src.IndexOf("ShockwaveRingFX.SpawnMoveTrailDust");
            int idxArrivalReturn = src.IndexOf("if (step <= 0f) return;");
            int idxMove = src.IndexOf("_rb.MovePosition(next)");
            Assert.IsTrue(idxCall > 0 && idxArrivalReturn > 0 && idxMove > 0,
                "이동 블록 구조 존재");
            Assert.IsTrue(idxCall > idxArrivalReturn,
                "도착 early-return 이후 배선 — 이동 중에만 스폰(도착 시 미스폰)");
            Assert.IsTrue(idxCall > idxMove,
                "MovePosition 직후 배선 — 발밑 먼지 순서 계약");
        }

        /// <summary>[F2] CommandMarker.Spawn — 기존 5인자 시그니처 불변 계약 유지.</summary>
        [Test]
        public void F2_CommandMarker_Spawn_SignatureUnchanged()
        {
            var mi = typeof(CommandMarker).GetMethod("Spawn",
                new[] { typeof(Vector3), typeof(Color?), typeof(GuardPlaceholder), typeof(float?),
                        typeof(System.Collections.Generic.List<GuardPlaceholder>) });
            Assert.IsNotNull(mi, "Spawn(Vector3, Color?, GuardPlaceholder, float?, List<GuardPlaceholder>) — 시그니처 불변 계약");
        }

        /// <summary>[F2] Spawn — groupOwners 마커 보존 배선 + 필드 선언.</summary>
        [Test]
        public void F2_CommandMarker_GroupOwners_Wired()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("marker._groupOwners = groupOwners;"),
                "Spawn에서 groupOwners 마커 보존 배선(무번호 19-F2)");
            Assert.IsTrue(src.Contains("List<GuardPlaceholder> _groupOwners"),
                "_groupOwners 필드 선언");
        }

        /// <summary>[F2] Start — 초기 평균 거리 측정 배선(스폰 시점 기준).</summary>
        [Test]
        public void F2_CommandMarker_InitDist_MeasuredAtStart()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            int idxStart = src.IndexOf("private void Start()");
            int idxMeasure = src.IndexOf("_initDist = ComputeAvgDist();");
            Assert.IsTrue(idxStart > 0 && idxMeasure > idxStart,
                "Start에서 초기 평균 거리 측정(무번호 19-F2)");
        }

        /// <summary>[F2] Update — 1s 표시 구간 내 접근 비율 링 축소(진행도)만 시각화.</summary>
        [Test]
        public void F2_CommandMarker_ProgressShrink_InLife()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            int idxLife = src.IndexOf("if (_elapsed < _life)");
            int idxProgress = src.IndexOf("ComputeArrivalProgress()");
            Assert.IsTrue(idxLife > 0 && idxProgress > idxLife,
                "1s 표시 구간 내 진행도 축소 배선(무번호 19-F2)");
            Assert.IsTrue(src.Contains("0.25f * progress"),
                "접근 비율 링 축소(최대 25%) 계약");
        }

        /// <summary>[F2] [72차] 1초 규약 유지 — FadeSeconds=0.3/_life=1 불변, 도착 소멸 미적용.</summary>
        [Test]
        public void F2_CommandMarker_OneSecondRule_Preserved()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("private const float FadeSeconds = 0.3f;"),
                "[72차] FadeSeconds=0.3 불변 계약");
            Assert.IsTrue(src.Contains("private float _life = 1f;"),
                "[72차] _life=1s 불변 계약 — 표시 후 1초 뒤 페이드, 도착 소멸 조건 폐기 유지");
        }

        /// <summary>[F2] ComputeArrivalProgress — 0..1 클램프 + 그룹 부재/초기거리 무효 가드.</summary>
        [Test]
        public void F2_ComputeArrivalProgress_Guards()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("if (_groupOwners == null || _groupOwners.Count == 0) return -1f;"),
                "그룹 부재 가드(ComputeAvgDist -1 반환)");
            Assert.IsTrue(src.Contains("if (_initDist <= 0.001f) return 0f;"),
                "초기 거리 무효 가드(진행도 0)");
            Assert.IsTrue(src.Contains("Mathf.Clamp01(1f - dist / _initDist);"),
                "진행도 0..1 클램프 계약");
        }

        // ── [무번호 65-P1] 계약 진화 — 데칼 경로 폐기(쿼드 단일 소스) ──
        //   [계약 진화 65-P1] 기존 F3 잠금(데칼 부착/폴백 체인)은 58-P2 프로브가 증명한 파손 경로를
        //   잠그고 있었다: URP 내장 Decal = Shader.Find NULL(스트리핑) + "Shader Graphs/Decal" 매뉴얼
        //   카피 = 신규 머티리얼 color(0,0,0,0)+텍스처 대입 무시 → DecalProjector 박스가 **불투명 흑 박스**
        //   렌더(공격씬 테스트 12 영상 실측 — 전투 중 명령마다 ~0.6s 검은 사각, [51-SCREEN] 데칼 패스
        //   사각으로 미보고). 쿼드 경로(SelectionRing/절차 링)로 단일 소스 고정.

        /// <summary>[F3 진화 65-P1] 지속형 마커 = 쿼드 경로 단일 소스 — 데칼 흔적 전부 소멸.</summary>
        [Test]
        public void F3_PersistentMarker_QuadOnlyPath()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("if (_persistent) SetupPersistent();"),
                "[65-P1] 지속형 = 쿼드 경로(CommandRingPresentation.Apply)");
            Assert.IsTrue(src.Contains("else SetupFading();"),
                "[65-P1] 페이드형 = 쿼드 경로 유지");
            Assert.IsFalse(src.Contains("AddComponent<DecalProjector>()"),
                "[65-P1] DecalProjector 부착 소멸(검은 사각 뿌리 제거)");
            Assert.IsFalse(src.Contains("Shader.Find(\"Shader Graphs/Decal\")"),
                "[65-P1] 파손 데칼 셰이더그래프 참조 소멸");
            Assert.IsTrue(src.Contains("[무번호 65-P1] TrySetupDecal"),
                "[65-P1] 폐기 근거 주석 보존(진료 기록)");
        }

        /// <summary>[F3] 틴트 베이크 텍스처 — 데칼 셰이더 색 프로퍼티 부재 실측(Base_Map/Normal_Map/Normal_Blend 3개) → 텍스처 베이크.</summary>
        [Test]
        public void F3_TintedRingTexture_Baked()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("public static Texture2D BuildTintedRingTexture(int size, Color tint)"),
                "틴트 베이크 링 텍스처 생성기 존재");
            // [계약 진화 65-P1] 데칼 경로 폐기로 _baseColor 베이크 배선 소멸 — 생성기는 공용 유틸로 잔존
            Assert.IsTrue(src.Contains("var backing = CommandRingPresentation.RingBackingColor;"),
                "다크 백킹 — RingBackingColor 평행 이식(C1 룩 계약)");
        }

        /// <summary>[F3 진화 65-P1] 알파 페이드 — fadeFactor(데칼) 폐기, 쿼드 머티리얼 알파만 구동.
        ///   [계약 진화 65-P1] C3 "팝 소멸 금지"는 램프다운 알파 구동으로 승계(쿼드 _TeamColor/_Color 경로).</summary>
        [Test]
        public void F3_FadeFactor_AlphaContract()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("if (m.HasProperty(\"_TeamColor\")) { var c = m.GetColor(\"_TeamColor\"); c.a = a; m.SetColor(\"_TeamColor\", c); }"),
                "[65-P1] 쿼드 알파 구동 — _TeamColor 램프(팝 소멸 금지 C3 승계)");
            Assert.IsTrue(src.Contains("else if (m.HasProperty(\"_Color\")) { var c = m.GetColor(\"_Color\"); c.a = a; m.SetColor(\"_Color\", c); }"),
                "[65-P1] 쿼드 알파 폴백 — _Color 경로 유지");
            Assert.IsFalse(src.Contains("_proj.fadeFactor"), "[65-P1] 데칼 fadeFactor 잔재 소멸");
        }

        /// <summary>[F3 진화 65-P1] 쿼드 경로 무결성 — 데칼 폐기 후 쿼드가 단일 시각 소스(비활성 분기 소멸).</summary>
        [Test]
        public void F3_QuadFallback_Intact()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("if (_persistent) SetupPersistent();"),
                "[65-P1] 지속형 쿼드 경로(단일 소스)");
            Assert.IsTrue(src.Contains("else SetupFading();"),
                "[65-P1] 페이드형 쿼드 경로");
            Assert.IsFalse(src.Contains("_rend.enabled = false;"),
                "[65-P1] 쿼드 비활성 분기 소멸(데칼 단일 소스였던 흔적 제거)");
        }

        /// <summary>[F3] 기존 SelectionRing 셰이더 경로 불변 — 페이드형(공격 링)은 쿼드+SelectionRing 유지.</summary>
        [Test]
        public void F3_FadeType_SelectionRing_Unchanged()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("Shader.Find(\"Custom/SelectionRing\")"),
                "페이드형 SelectionRing 셰이더 경로 불변 계약");
            Assert.IsTrue(src.Contains("CommandRingPresentation.Apply(_rend, _baseColor);"),
                "지속형 폴백의 Apply 경로 불변 계약");
        }
    }
}
