using NUnit.Framework;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [166차 E] 아이콘 정면 전체 모습 통일 계약 — GblItemIconRenderer.
    /// ① 카메라 정면 고정(center + (0,0,-dist) — 사선 제거, TryMount/SyncBake 양 경로 공용 헬퍼)
    /// ② 최장축 수평 자동 정규화(순수 함수 — Y/Z장축 → 수평, 비좁형/이미수평은 원본 유지)
    /// ③ per-key 방향 오버라이드 테이블(홀드 config와 같은 패턴 — 오버라이드 우선)
    /// ④ [142차 H] 수평/수직 반각 전체핏 유지.
    /// 실렌더 방향 샘플(낚싯대·검·헬멧·말) 판정은 Play 대기 — 본 테스트는 순수 함수 + 소스 어서션.
    /// </summary>
    public class GblItemIconOrientationTests
    {
        private const string SrcPath = "Assets/Scripts/UI/GblItemIconRenderer.cs";

        // ── ② 자동 정규화 순수 함수 ──

        [Test]
        public void Auto_YLongest_RotatedToHorizontal()
        {
            var rot = GblItemIconRenderer.ComputeLongestAxisHorizontalRotation(new Vector3(0.05f, 1.5f, 0.05f));
            Vector3 mapped = rot * Vector3.up;
            Assert.That(Mathf.Abs(mapped.x), Is.EqualTo(1f).Within(0.001f),
                "Y장축(정립 검 등)은 최장축이 수평(+X/-X)로 정렬되어야 한다. [166차 E]");
        }

        [Test]
        public void Auto_ZLongest_RotatedToHorizontal()
        {
            var rot = GblItemIconRenderer.ComputeLongestAxisHorizontalRotation(new Vector3(0.05f, 0.05f, 1.5f));
            Vector3 mapped = rot * Vector3.forward;
            Assert.That(Mathf.Abs(mapped.x), Is.EqualTo(1f).Within(0.001f),
                "Z장축(카메라 정면을 향해 베이크된 모델)도 수평으로 정렬되어야 한다. [166차 E]");
        }

        [Test]
        public void Auto_XLongest_Kept()
        {
            Assert.That(GblItemIconRenderer.ComputeLongestAxisHorizontalRotation(new Vector3(1.5f, 0.05f, 0.05f)),
                Is.EqualTo(Quaternion.identity), "이미 X장축(수평)이면 회전 없음. [166차 E]");
        }

        [Test]
        public void Auto_Squat_KeptOriginal()
        {
            // 비좁형(헬멧 등) — 최장축이 차 2위 축의 2배 미만이면 원본 베이크 방향 유지(초기 휴리스틱 2.0).
            Assert.That(GblItemIconRenderer.ComputeLongestAxisHorizontalRotation(new Vector3(0.5f, 0.6f, 0.5f)),
                Is.EqualTo(Quaternion.identity), "비좁형(헬멧류 1.2배)은 원본 그대로 정면 — 오회전 금지.");
            Assert.That(GblItemIconRenderer.ComputeLongestAxisHorizontalRotation(new Vector3(0.5f, 0.9f, 0.5f)),
                Is.EqualTo(Quaternion.identity), "중간형(방어구류 1.8배)도 원본 유지 — 보수 임계 2.0.");
            Assert.That(GblItemIconRenderer.ComputeLongestAxisHorizontalRotation(new Vector3(0.2f, 0.9f, 0.2f)),
                Is.Not.EqualTo(Quaternion.identity), "명백한 장축(4.5배)은 수평 정규화 대상.");
        }

        [Test]
        public void Auto_ZeroExtents_Safe()
        {
            Assert.That(GblItemIconRenderer.ComputeLongestAxisHorizontalRotation(Vector3.zero),
                Is.EqualTo(Quaternion.identity), "영 bounds — 크래시 없이 원본 유지(폴백 계약).");
        }

        // ── ③ per-key 방향 오버라이드 테이블 ──

        [Test]
        public void Override_UnregisteredKey_ReturnsFalse()
        {
            Assert.That(GblItemIconRenderer.TryGetOrientationOverride("herb_blue", out Vector3 _), Is.False,
                "미등록 키는 오버라이드 없음 — 자동 정규화 사용(홀드 config 선례).");
        }

        // ── 배선 소스 어서션 (HotbarHoldKeyTests/FishingRodHoldContractTests 선례) ──

        [Test]
        public void IconCamera_FrontFacing_BothPaths_SharedHelper()
        {
            var src = System.IO.File.ReadAllText(SrcPath);

            // 공용 헬퍼가 정면 고정을 소유 — 정의 1회 + 호출 2회(TryMount/SyncBake) = 통일 보장.
            int defIdx = src.IndexOf("private static void FrameIconCamera(Camera cam, Bounds b)", System.StringComparison.Ordinal);
            Assert.That(defIdx, Is.GreaterThanOrEqualTo(0), "공용 프레이밍 헬퍼 존재. [166차 E]");
            Assert.That(System.Text.RegularExpressions.Regex.Matches(src, @"FrameIconCamera\(").Count,
                Is.EqualTo(3), "FrameIconCamera 정의 1회 + 호출 2회(TryMount/SyncBake) — 양 경로 공용.");

            // 헬퍼 본: [142차 H] 전체핏 + [166차 E] 정면 고정.
            var body = Body(src, "private static void FrameIconCamera(Camera cam, Bounds b)",
                "//  마운트: 노드/카메라/RT 생성");
            Assert.That(body, Does.Contain("center + new Vector3(0f, 0f, -dist)"),
                "[166차 E] 카메라 정면 고정 — 사선(offset) 제거.");
            Assert.That(body, Does.Contain("Mathf.Max(b.extents.x, b.extents.y) * IconFillMargin"),
                "[197차] 직교 전환 전체핏(Max(extents.x, extents.y)×마진 1.08) 유지 — 잘림/이탈 회귀 금지.");

            // 사선 카메라 잔존 금지 — 양 경로에서 제거 완료.
            Assert.That(src, Does.Not.Contain("dist * 0.35f"),
                "사선 카메라(dist * 0.35f) 잔존 금지 — GLB마다 방향이 제각각이던 근원 제거.");
        }

        [Test]
        public void Orientation_Wiring_OverrideFirst_ThenAuto_BothPaths()
        {
            var src = System.IO.File.ReadAllText(SrcPath);

            // 적용 헬퍼: 오버라이드 우선 → 자동 정규화(순서 계약).
            int i = src.IndexOf("private static void ApplyIconOrientation", System.StringComparison.Ordinal);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), "방향 적용 헬퍼 존재. [166차 E]");
            var body = Body(src, "private static void ApplyIconOrientation",
                "private static void FrameIconCamera");
            int ov = body.IndexOf("TryGetOrientationOverride", System.StringComparison.Ordinal);
            int auto = body.IndexOf("ComputeLongestAxisHorizontalRotation", System.StringComparison.Ordinal);
            Assert.That(ov, Is.GreaterThanOrEqualTo(0), "per-key 오버라이드 조회가 헬퍼에 배선되어야 한다.");
            Assert.That(auto, Is.GreaterThan(ov), "오버라이드가 자동 정규화보다 선행 — 수기 지정 우선순위 계약.");

            // 양 경로(TryMount + SyncBake)에서 방향 정규화 선행 호출 — 정의 1회 + 호출 2회.
            Assert.That(System.Text.RegularExpressions.Regex.Matches(src, @"ApplyIconOrientation\(").Count,
                Is.EqualTo(3), "ApplyIconOrientation 정의 1회 + 호출 2회(TryMount/SyncBake).");

            // per-key 테이블 존재(홀드 config와 같은 패턴).
            Assert.That(src, Does.Contain("_orientationOverrides"),
                "per-key 방향 오버라이드 테이블 존재. [166차 E]");

            // 스케일 목표 최장축 일반화 — Y장축 정립 모델은 기존 높이핏과 동일, X/Z장축 폭발 방지.
            Assert.That(src, Does.Contain("Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z))"),
                "스케일 최장축핏 — 방향 정규화 전제(높이핏 단독 잔존 금지).");
        }

        /// <summary>소스에서 startMarker~endMarker 구간(메서드 블록) 추출 — 미발견 시 빈 문자열.</summary>
        private static string Body(string src, string startMarker, string endMarker)
        {
            int start = src.IndexOf(startMarker, System.StringComparison.Ordinal);
            int end = start >= 0 ? src.IndexOf(endMarker, start, System.StringComparison.Ordinal) : -1;
            return start >= 0 && end > start ? src.Substring(start, end - start) : string.Empty;
        }
    }
}
