using System.IO;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [195차 P5] 말 크기 정규화 계약 — "찌그러진 로드" 수리 고정.
    /// 실측: 적토마 GLB 원본 수평차원 ~0.45m(HorseScale 2.4 배 후에도 bounds 1.09m — horse195 프로브 실측) →
    /// 렌더러 bounds 기반 정규화(HorseTargetLength 2.3m)로 교체. 렌더러 부재 시 HorseScale 폴백 유지.
    /// [206차] 갱신 — 205차 y축 별도 보정(전신 world-Y 압축)이 "찌그러짐"의 근원으로 확인되어 제거 →
    /// 균일 스케일(Vector3.one * factor) 복귀. 원본 GLB 비율(체고/체장 0.74~0.76)은 정상 — 모델 원본 비율 유지 계약.
    /// </summary>
    public class HorseScaleNorm195Tests
    {
        static string S(string path) => System.IO.File.ReadAllText(path);

        [Test]
        public void H1_SpawnHorse_UsesBoundsNormalization()
        {
            string src = S("Assets/Scripts/Systems/HorseMountSystem.cs");
            Assert.That(src, Does.Contain("NormalizeHorseScale(go);"),
                "[195차 P5] SpawnHorse가 크기 정규화 헬퍼를 호출해야 한다.");
            Assert.That(src, Does.Contain("static void NormalizeHorseScale(GameObject go)"),
                "[195차 P5] 정규화 헬퍼 정의 존재.");
            Assert.That(src, Does.Contain("b.Encapsulate(renderers[i].bounds);"),
                "[195차 P5] 전체 렌더러 bounds 합산(피벗/베이크 스케일 무관 결정론).");
            Assert.That(src, Does.Contain("Vector3.one * HorseScale;"),
                "[195차 P5] 렌더러 부재 폴백 — 기존 HorseScale 상수 유지(계약). [206차] 폴백 전용(본 경로는 균일 스케일, 무수정).");
            // [206차] 균일 스케일 복귀 핀 — y축 별도 보정 제거, 원본 GLB 비율 유지
            Assert.That(src, Does.Contain("go.transform.localScale = Vector3.one * factor;"),
                "[206차] 균일 스케일 복귀 — localScale = Vector3.one × factor(원본 GLB 비율 유지 계약).");
            Assert.That(src, Does.Not.Contain("yScale"),
                "[206차] y축 별도 보정 제거 핀 — 전신 world-Y 압축(찌그러짐 근원) 소멸.");
            Assert.That(src, Does.Not.Contain("HorseTargetHeightRatio"),
                "[206차] 체고 비율 상수 제거 핀 — y보정 산식 소멸.");
        }

        [Test]
        public void H2_TargetLength_Contract()
        {
            string src = S("Assets/Scripts/Systems/HorseMountSystem.cs");
            Assert.That(src, Does.Contain("public const float HorseTargetLength = 2.3f;"),
                "[195차 P5] 목표 체장 상수 고정(Play 트림 대상 — 값 변경은 이 테스트 갱신으로 승인 기록). [206차] 균일 스케일 복귀 후에도 성립.");
            Assert.That(src, Does.Contain("HorseTargetLength / maxDim"),
                "[195차 P5] 체장 비율 산식 존재. [206차] factor 전축 균일 적용.");
        }
    }

    /// <summary>
    /// [206차] 말 균일 스케일 계약 — 205차 비균일 확장(체장 factor + 체고 y축 별도 보정)의 전신 world-Y 압축이
    /// "찌그러짐"의 근원으로 확인되어 균일 스케일로 복귀. 원본 GLB 비율(체고/체장 0.74~0.76)은 모델 원본 유지가
    /// 승인된 상태(실제 말 표준 0.62~0.66 대비 다리 짧음은 별도 결정 대상) — 균일 스케일 외 비율 보정 없음.
    /// </summary>
    public class HorseScale206Tests
    {
        [Test]
        public void R1_UniformScale_Contract()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/HorseMountSystem.cs");
            Assert.That(src, Does.Contain("go.transform.localScale = Vector3.one * factor;"),
                "[206차] 균일 스케일 — x/y/z 전축 동일 factor(목표 체장 2.3m, 원본 비율 유지).");
            Assert.That(src, Does.Not.Contain("HorseTargetHeightRatio"),
                "[206차] 체고 비율 상수 부재 핀 — y보정 산식 소멸.");
            Assert.That(src, Does.Not.Contain("yScale"),
                "[206차] y축 보정 변수 부재 핀 — 205차 비균일 확장 완전 제거.");
        }
    }
}
