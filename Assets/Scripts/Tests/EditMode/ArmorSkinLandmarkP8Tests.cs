using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-P8] 스킨 랜드마크 기반 장비 정합 — 소스 계약(Source 잠금).
    /// P8 프로브 실측 근거: 스킨=×S² 이중 스케일 렌더(S=1.552에서 0.999→2.407m),
    /// 본=×S 선형+스킨 대비 압축(head 본 62%). 장비 크기/투구 앵커를 스킨 랜드마크 기준으로 전환.
    /// </summary>
    public class ArmorSkinLandmarkP8Tests
    {
        private static string ReadSystem() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ArmorVisualAttachSystem.cs");

        [Test]
        public void Source_LandmarkHelper()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("internal struct SkinLandmarks"),
                "[무번호 29-P8] 스킨 랜드마크 구조체가 존재해야 한다.");
            Assert.That(src, Does.Contain("internal static SkinLandmarks MeasureSkinLandmarksNow(Animator placeAnim)"),
                "[무번호 29-P8] 스킨 랜드마크 실측 헬퍼가 존재해야 한다.");
            // [무번호 29-P9 갱신] 필터 0.28→0.06×span(부착 시점 포즈 팔/손 오포함 수리 — 13:27 Play 실측).
            Assert.That(src, Does.Contain("0.06f * span"),
                "[무번호 29-P9] 두상 중심축 필터(머리 반폭 스케일 일치 — 팔·무기 배제)가 존재해야 한다.");
            Assert.That(src, Does.Contain("smr.transform.localToWorldMatrix"),
                "[무번호 29-P8] BakeMesh→전체 L2W 관례(화면 진실 = P8 프로브 naive = Play renderSpan 2.43m)를 사용해야 한다.");
        }

        [Test]
        public void Source_KFactor_Attach_And_Reassert()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("GetTargetSize(slot, itemId) * skinK"),
                "[무번호 29-P8] 부착 정규화 타깃이 스킨k 배수여야 한다.");
            Assert.That(src, Does.Contain("skinLm.span / TerritoryBuilder.SoldierTargetHeight"),
                "[무번호 29-P8] k = 스킨스팬/1.8m 계약 키.");
            Assert.That(src, Does.Contain("_skinKByScene[skinScene] = skinK"),
                "[무번호 29-P8] k 씬 캐시 기록이 존재해야 한다.");
            Assert.That(src, Does.Contain("target *= GetSkinK(scene);"),
                "[무번호 29-P8] SyncTick 재정규화도 동일 k를 사용해야 한다(부착-재정규화 일관).");
        }

        [Test]
        public void Source_HelmetAnchor_P10Reverted()
        {
            string src = ReadSystem();
            // [무번호 29-P10 갱신] 투구 앵커 = 105차 공식 복원(사용자 승인 이력 위치).
            //   P8/P9 스킨 두상 앵커는 14:01 Play 부유 재현+상부 오염 정점(포즈별 3.22/2.59 변동)으로 기각 —
            //   스킨 랜드마크는 크기 k와 진단 로그 전용으로 축소.
            Assert.That(src, Does.Not.Contain("skinLm.headTopY - helmetH * 0.35f"),
                "[무번호 29-P10] 스킨 두상 앵커 경로 잔존 금지(기각 계약).");
            Assert.That(src, Does.Contain("[29-P10] 투구 앵커 = 105차 공식"),
                "[무번호 29-P10] 복원 마커 존재.");
            Assert.That(src, Does.Contain("Vector3.up * (0.26f * BodyScale)"),
                "[무번호 29-P10] 105차 F3 공식(본+0.26×BodyScale) 배선 유지.");
        }

        [Test]
        public void Source_GracefulDegrade()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("float skinK = 1f;"),
                "[무번호 29-P8] 측정 실패 시 k=1 기본값(기존 계약 그대로).");
            Assert.That(src, Does.Contain("if (skinLm.valid && skinLm.span > 0.05f)"),
                "[무번호 29-P8] 랜드마크 유효성 게이트가 존재해야 한다.");
            Assert.That(src, Does.Contain("k > 0.05f ? k : 1f"),
                "[무번호 29-P8] GetSkinK 비정상 캐시 방어(0.05 이하=1 폴백).");
        }

        [Test]
        public void Source_Marker_And_Deterministic()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("[무번호 29-P8]"), "[무번호 29-P8] 마커 존재.");
            int idx = src.IndexOf("MeasureSkinLandmarksNow");
            string block = src.Substring(System.Math.Max(0, idx - 200), System.Math.Min(2600, src.Length - System.Math.Max(0, idx - 200)));
            Assert.That(block, Does.Not.Contain("UnityEngine.Random"),
                "[무번호 29-P8] 랜드마크 측정 결정론 — 난수 API 0건.");
        }
    }
}
