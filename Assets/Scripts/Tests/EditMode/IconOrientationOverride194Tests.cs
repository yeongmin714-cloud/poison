using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [194차 P3] 아이콘 방향 오버라이드 + 프레이밍 Z-보정 계약.
    /// 배경: 전수 베이크 접촉시트 vision 판독으로 확정된 결함 — ①접시/음식류가 XZ 평면 눕힘(비좁형
    /// 판정으로 자동 회전 제외) 또는 원본 (90,0,0) 굽음으로 측면만 보임 ②물약/삽 Y장축이 자동 롤로
    /// 눕워짐 ③FrameIconCamera가 Z 깊이를 무시해 Z-깊은 물체(물뿌리개) 근접면이 원근 확대로 절단
    /// (픽셀 실측: 하단 테두리 17.5° > 반시야각 15°).
    /// 수리: _orientationOverrides 20키 + dist에 extZ 가산(→ [197차] 직교 전환으로 가산형 폐기 —
    /// 실측 재판정: 원근 확대 절단은 투영을 orthographic으로 전환하는 것만으로 근원 제거).
    /// 검증: 고립 재베이크 24종 픽셀 절단 0건
    /// + bbox 종횡비 전환(틸트 적용 실증) + vision 8배 판독 GREEN.
    /// </summary>
    public class IconOrientationOverride194Tests
    {
        private const string SrcPath = "Assets/Scripts/UI/GblItemIconRenderer.cs";

        [Test]
        public void Override_CoreKeys_Registered_WithDiagEuler()
        {
            // 행위 테스트 — public 조회 API(TryGetOrientationOverride)로 등록값 1:1 검증.
            AssertOverride("dish_stirfry",    new Vector3(-70f, 0f, 0f), "접시 틸트 3/4 뷰");
            AssertOverride("dish_salad",      new Vector3(-70f, 0f, 0f), "접시 틸트 3/4 뷰");
            AssertOverride("dish_pie",        new Vector3(-70f, 0f, 0f), "접시 틸트 3/4 뷰");
            AssertOverride("grilled_skin",    new Vector3(-70f, 0f, 0f), "원본 (90,0,0) 굽음 되돌림+틸트");
            AssertOverride("yakitori_skewer", new Vector3(-70f, 0f, 0f), "꼬치 경사 프로필");
            AssertOverride("potion_sedative", new Vector3(0f, 0f, 0f),   "Y장축 자동 롤 복원(직립)");
            AssertOverride("potion_stealth",  new Vector3(0f, 0f, 0f),   "Y장축 자동 롤 복원(직립)");
            AssertOverride("tool_shovel",     new Vector3(0f, 0f, 0f),   "Y장축 자동 롤 복원(직립)");
            AssertOverride("tool_pickaxe",    new Vector3(-65f, 0f, 0f), "T자 프로필 틸트");
            AssertOverride("crystal_shard",   new Vector3(0f, 0f, 90f),  "X장축 → 직립");
            AssertOverride("boar_tusk",       new Vector3(-90f, 0f, 0f), "Z장축 → 팁 위로 직립");
            AssertOverride("wolf_tooth",      new Vector3(-90f, 0f, 0f), "Z장축 → 팁 위로 직립");
            AssertOverride("wood_armor",      new Vector3(0f, 0f, 0f),   "원본 (90,0,0) 굽음 되돌림");
        }

        [Test]
        public void Override_DishTiltThirteen_Uniform()
        {
            // 접시/음식류 13키는 동일 틸트(−70,0,0) — 개별 값 이탈은 의도 변경이므로 계약으로 고정.
            string[] dishKeys =
            {
                "dish_stirfry", "dish_braise", "dish_steam", "dish_salad", "dish_stew",
                "dish_jeon", "dish_sashimi", "dish_ricebowl", "dish_steak", "dish_pie",
                "grilled_skin", "tempura", "yakitori_skewer",
            };
            foreach (string key in dishKeys)
                AssertOverride(key, new Vector3(-70f, 0f, 0f), "접시류 통일 틸트: " + key);
        }

        [Test]
        public void Override_ControlKeys_Unregistered_AutoNormalizationKept()
        {
            // 대조군(정상 판정 키)은 오버라이드 미등록 — 자동 정규화 유지 계약(등록하면 오히려 회귀).
            Assert.That(ProjectName.UI.GblItemIconRenderer.TryGetOrientationOverride("dish_pickle", out _), Is.False,
                "피클 단지 — 픽셀 실측 절단 0(비좁형 원본 유지 정상) — 오버라이드 등록 금지.");
            Assert.That(ProjectName.UI.GblItemIconRenderer.TryGetOrientationOverride("dish_tea", out _), Is.False,
                "찻잔 — 정상 판정 대조군 — 오버라이드 등록 금지.");
            Assert.That(ProjectName.UI.GblItemIconRenderer.TryGetOrientationOverride("mat_stone", out _), Is.False,
                "돌 — 비좁형 원본 유지 정상 — 오버라이드 등록 금지.");
        }

        [Test]
        public void FrameIconCamera_OrthographicFullFit()
        {
            // [197차] 직교 전환 — 194차 원근 후퇴(dist+extZ 가산형) 폐기, 실측 재판정: 원근 확대 절단은
            //  투영을 orthographic으로 바꾸는 것만으로 근원 제거. 핀: 직교 충전 수식(Max(extents.x,
            //  extents.y)×IconFillMargin) 존재 + "center + new Vector3(0f, 0f, -dist)" 위치 계약 유지 +
            //  직교 전환→수식→배치 순서 검증(기존 순서 계약 의도 보존).
            var src = System.IO.File.ReadAllText(SrcPath);
            int def = src.IndexOf("private static void FrameIconCamera(Camera cam, Bounds b)", System.StringComparison.Ordinal);
            Assert.That(def, Is.GreaterThanOrEqualTo(0), "프레이밍 헬퍼 존재.");
            int orthoLine = src.IndexOf("cam.orthographic = true", def, System.StringComparison.Ordinal);
            int fillLine = src.IndexOf("Mathf.Max(b.extents.x, b.extents.y) * IconFillMargin", def, System.StringComparison.Ordinal);
            int posLine = src.IndexOf("center + new Vector3(0f, 0f, -dist)", def, System.StringComparison.Ordinal);
            Assert.That(orthoLine, Is.GreaterThanOrEqualTo(0), "[197차] 직교 투영 전환.");
            Assert.That(fillLine, Is.GreaterThan(orthoLine), "[197차] 직교 전환 후 충전률 마진 수식 적용.");
            Assert.That(posLine, Is.GreaterThan(fillLine), "[197차] 직교 dist가 카메라 배치에 사용되어야 한다(순서 계약 유지).");
        }

        private static void AssertOverride(string key, Vector3 expected, string reason)
        {
            Assert.That(ProjectName.UI.GblItemIconRenderer.TryGetOrientationOverride(key, out var euler), Is.True,
                $"오버라이드 등록: {key} — {reason}");
            // Vector3 Is.EqualTo = Unity 근사 동등(상수 리터럴 비교에 충분 — 커스텀 비교자 불요).
            Assert.That(euler, Is.EqualTo(expected),
                $"오버라이드 값: {key} — {reason}");
        }

    }
}
