using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [197차] 아이콘 고품질화 계약 핀 — 256px 해상도 + MSAA 4x + 직교(orthographic) 전환으로
    /// 충전률 92% 목표(마진 1.08). src 핀 방식(194차 선례) — 편집 회귀를 컴파일 전에 조기 감지.
    /// </summary>
    public class IconQuality197Tests
    {
        private const string SrcPath = "Assets/Scripts/UI/GblItemIconRenderer.cs";

        [Test]
        public void Quality197_HighQualitySourcePins()
        {
            var src = System.IO.File.ReadAllText(SrcPath);

            // [197차] 해상도 256 — RT/Texture2D/ReadPixels 전부 IconSize 상수 공유(하드코딩 잔여 금지).
            Assert.That(src.IndexOf("private const int IconSize = 256", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "[197차] IconSize 256 상수 핀.");

            // [197차] AA = 2× 슈퍼샘플 — MSAA RT ReadPixels가 배치 d3d12에서 GPU 크래시하는 실측으로
            //   MSAA를 512 렌더→256 박스다운샘플 경로로 대체(IconBakeFix194 재베이크 실측).
            Assert.That(src.IndexOf("private const int BakeRTSize = IconSize * 4", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "[197차] BakeRTSize 4× 슈퍼샘플 상수 핀.");
            Assert.That(src.IndexOf("BakeFromRTDownsampled(rt, \"GblIcon_\" + itemId)", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "[197차] 동기 경로 슈퍼샘플 헬퍼 배선 핀.");
            Assert.That(src.IndexOf("BakeFromRTDownsampled(job.rt, \"GblIcon_\" + job.modelKey)", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "[197차] 큐 경로 슈퍼샘플 헬퍼 배선 핀.");
            Assert.That(new System.Text.RegularExpressions.Regex(@"antiAliasing").Matches(src).Count,
                Is.EqualTo(0), "[197차] MSAA 잔존 금지 — d3d12 ReadPixels 크래시 근원 제거 계약.");

            // [197차] 직교 전환 — FrameIconCamera 공용 헬퍼 1곳(TryMount/SyncBake 양 경로 공유).
            Assert.That(new System.Text.RegularExpressions.Regex(@"cam\.orthographic = true").Matches(src).Count,
                Is.EqualTo(1), "[197차] 직교 투영 전환 — 공용 헬퍼 단일 적용.");

            // [197차] 충전률 ~89% 목표 마진 1.12(194차 1.25마진+퍼스펙티브 후퇴 대체,
            //   1.08→1.12 기울임 물체 모서리 여유 — Play 판정 후 트림 대상).
            Assert.That(src.IndexOf("private const float IconFillMargin = 1.12f", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "[197차] IconFillMargin 1.12 상수 핀.");

            // [197차] 베이크 전용 3점 조명 릭 — 플랫 조명 해소(키/필/림).
            Assert.That(src.IndexOf("AttachIconLightRig(cam);", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "[197차] 동기 경로 조명 릭 배선.");
            Assert.That(src.IndexOf("AttachIconLightRig(job.cam);", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "[197차] 큐 경로 조명 릭 배선.");
            Assert.That(src.IndexOf("private static void AttachIconLightRig(Camera cam)", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "[197차] 조명 릭 헬퍼 정의.");

            // [197차] 직교 근접면 0.05 — 근접면(dist-extZ=1) 여유 포함.
            Assert.That(src.IndexOf("cam.nearClipPlane = 0.05f", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "[197차] nearClipPlane 0.05 핀.");
        }
    }
}
