using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-P6] 장비 월드 크기 자가 재정규화 — 소스 계약(Source 잠금).
    /// 근거: 11-13 Play 실측 — 부착 시점 정규화(본 스케일 포함 world bounds 기준) 후 아바타 적용으로
    /// 본 스케일이 변해 투구 0.41m(타깃 0.528m의 78%)/갑옷 0.81m(0.962m의 84%)로 축소.
    /// SyncTick ④ 단계 — 정착(3s) 후 슬롯당 세션 1회, ±10% 이탈 시 NormalizeVisualScale 재정렬.
    /// </summary>
    public class ArmorWorldSizeReassertP6Tests
    {
        private static string ReadSystem() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ArmorVisualAttachSystem.cs");

        [Test]
        public void Source_ReassertMethod()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("void TryReassertEquipmentWorldSize(EquipmentManager em)"),
                "[무번호 29-P6] 재정규화 진입 메서드가 존재해야 한다.");
            Assert.That(src, Does.Contain("TryReassertEquipmentWorldSize(em);"),
                "[무번호 29-P6] SyncTick에서 ④ 단계로 호출되어야 한다.");
        }

        [Test]
        public void Source_SettleGate()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("if (Time.time < 3f) return;"),
                "[무번호 29-P6] 정착 대기 게이트(부팅 3s — 아바타/애니 정착 후만 판정)가 존재해야 한다.");
        }

        [Test]
        public void Source_ThresholdAndOnce()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("0.10f * target"),
                "[무번호 29-P6] ±10% 이탈 임계가 존재해야 한다.");
            // [무번호 29-P12 갱신] 가드 캐시 = HashSet 1회 소진형 → Dictionary 쿨다운 지속 추적(히트 변동 대응).
            Assert.That(src, Does.Contain("Dictionary<string, float> _resizeCooldown"),
                "[무번호 29-P12] 인스턴스별 쿨다운 캐시(static)가 존재해야 한다.");
            // [무번호 29-P12 갱신] 소진형 1회 가드 → 인스턴스별 쿨다운 지속 추적(히트 후 본 변동 무방비 수리).
            int addIdx = src.IndexOf("_resizeCooldown[key] = nowT;");
            int warnIdx = src.IndexOf("[29-P6] 장비 크기 재정규화");
            Assert.That(addIdx, Is.GreaterThanOrEqualTo(0), "[무번호 29-P6] 재시도 금지 키 등록이 존재해야 한다.");
            Assert.That(warnIdx, Is.GreaterThan(addIdx),
                "[무번호 29-P6] 키 선(先)등록 → 로그 순서(오실레이션 방지 의도 반영).");
        }

        [Test]
        public void Source_MeasureExcludesEffects()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("static float MeasureVisualWorldMaxDim(GameObject visual)"),
                "[무번호 29-P6] 월드 최장축 측정 헬퍼가 존재해야 한다.");
            // [무번호 29-P13 갱신] 제외 필터가 덩치 측정기(TryMeasureVisualBulk)로 이동 — null 가드 결합형.
            Assert.That(src, Does.Contain("if (r == null || r is TrailRenderer || r is ParticleSystemRenderer || r is LineRenderer) continue;"),
                "[무번호 29-P6/P13] 이펙트 렌더러 제외 필터(M1 선례)가 덩치 측정기에 유지되어야 한다.");
        }

        [Test]
        public void Source_UsesContractTarget()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("float target = GetTargetSize(slot, desired);"),
                "[무번호 29-P6] 재정렬 타깃은 계약 상수(69차 원값×BodyScale) 경유여야 한다 — 하드코딩 금지.");
        }

        [Test]
        public void Source_Marker_And_Deterministic()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("[무번호 29-P6]"), "[무번호 29-P6] 마커 존재.");
            int idx = src.IndexOf("TryReassertEquipmentWorldSize(EquipmentManager em)");
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            string block = src.Substring(idx);
            Assert.That(block.Substring(0, System.Math.Min(block.Length, 2600)), Does.Not.Contain("UnityEngine.Random"),
                "[무번호 29-P6] 재정규화 블록 결정론 — 난수 API 0건.");
        }
    }
}
