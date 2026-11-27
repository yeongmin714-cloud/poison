using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-P13] 장비 덩치(bulk) 수학 — 오염 GLB 메시(쏠림 정점) 강건화.
    /// 영상 판독 근거(14:25 장비착용 테스트.mp4): 투구 폭=머리폭 88~90%(크기 자체 적정)이나
    /// 투구 뭉치가 bounds 내 측면 쏠림 → 스냅 후 머리폭 58% 오프셋. 측정/스냅을 백분위·중앙값으로 강건화.
    /// </summary>
    public class ArmorBulkCenteringP13Tests
    {
        private static string ReadSystem() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ArmorVisualAttachSystem.cs");

        [Test]
        public void Source_BulkMeasurementHelpers()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("internal static bool TryMeasureVisualBulk(GameObject visual, out Vector3 centroid, out Vector3 size)"),
                "[29-P13] 덩치 측정 진입점(centroid+size).");
            Assert.That(src, Does.Contain("internal static Vector3 MedianVec(System.Collections.Generic.List<Vector3> pts)"),
                "[29-P13] 축별 중앙값 중심(왜곡 정점 강건).");
            Assert.That(src, Does.Contain("internal static Vector3 PercentileSpan(System.Collections.Generic.List<Vector3> pts, float loP, float hiP)"),
                "[29-P13] 백분위 스팬.");
            Assert.That(src, Does.Contain("0.03f, 0.97f"),
                "[29-P13] 3~97% 트림 구간(쏠림 정점 배제).");
        }

        [Test]
        public void Source_MeasureVisualWorldMaxDim_BulkBacked()
        {
            string src = ReadSystem();
            // 시그니처 잠금(P6) 보존 + 구현이 bulk 경유로 교체되었는지.
            Assert.That(src, Does.Contain("static float MeasureVisualWorldMaxDim(GameObject visual)"),
                "[29-P13] 측정 시그니처 유지(P6 잠금 보존).");
            int iSig = src.IndexOf("static float MeasureVisualWorldMaxDim(GameObject visual)");
            int iBulk = src.IndexOf("TryMeasureVisualBulk(visual, out _, out var bulkSize)");
            Assert.That(iBulk, Is.GreaterThan(iSig),
                "[29-P13] MeasureVisualWorldMaxDim 구현이 덩치(백분위) 경유여야 한다(bounds 왜곡 제거).");
        }

        [Test]
        public void Source_RecenteringAfterSnap()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("[무번호 29-P13] 덩치 재중심화"),
                "[29-P13] 재중심화 마커 존재.");
            Assert.That(src, Does.Contain("visual.transform.position += shift;"),
                "[29-P13] 스냅 후 덩치 중심 보정 이동 적용.");
            int iSnap = src.IndexOf("SnapVisualToBone(visual, bone, GetCenterOffset(slot, itemId), GetBottomAnchor(slot, itemId), worldAnchor);");
            int iShift = src.IndexOf("visual.transform.position += shift;");
            Assert.That(iShift, Is.GreaterThan(iSnap),
                "[29-P13] 재중심화가 스냅 직후에 실행되어야 한다.");
        }

        [Test]
        public void Source_Bulk_Deterministic_And_Fallback()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("if (n < 12) return false;"),
                "[29-P13] 샘플 부족 폴백(false — 호출부 기존 경로 유지).");
            int i0 = src.IndexOf("[무번호 29-P13] 장비 비주얼의 렌더 덩치");
            string blk = src.Substring(i0, System.Math.Min(2400, src.Length - i0));
            Assert.That(blk, Does.Not.Contain("UnityEngine.Random"),
                "[29-P13] 덩치 측정 결정론 — 난수 API 0건.");
        }
    }
}
