using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [189차] 몬스터 피격 FX 파란 회전 마법진(싱볼) 레이어 제거 — 계약 테스트.
    /// ① PlayImpactMulti가 DisableImpactSymbolLayer(instance)를 호출하고 헬퍼는
    ///    GetComponentsInChildren&lt;Transform&gt;(true)로 이름 "Simbol" 정확 일치 → SetActive(false).
    /// ② "Shockwave"(히트 링/원)는 비활성화 로직에 절대 포함되지 않는다(과다 제거 방지).
    /// ③ MagicHit.prefab에는 여전히 "Simbol"/"Shockwave" 자식 GO가 존재(에셋 무변경 — 런타임만 OFF).
    /// 참고: 크로스(PlayCross)는 몬스터 피격이 아니므로 헬퍼를 호출하지 않아 레이어 유지.
    /// </summary>
    public class HitFxSymbolRemoval189Tests
    {
        private const string RunnerPath = "Assets/Scripts/Systems/SlashVFXRunner.cs";
        private const string PrefabPath = "Assets/Resources/FX/Impact/MagicHit.prefab";

        private static string Read(string p) => System.IO.File.ReadAllText(p);

        /// <summary>시작 마커부터 다음 마커 직전까지의 소스 조각 추출(메서드 바디 스코프 검증용).</summary>
        private static string Slice(string src, string startMarker, string endMarker)
        {
            int start = src.IndexOf(startMarker);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"마커 미발견: {startMarker}");
            int end = src.IndexOf(endMarker, start + startMarker.Length);
            if (end < 0) end = src.Length;
            return src.Substring(start, end - start);
        }

        [Test]
        public void Source_PlayImpactMulti_DisablesSymbolLayer()
        {
            string src = Read(RunnerPath);
            Assert.That(src, Does.Contain("[189차]"), "[189차] 변경 마커.");

            // PlayImpactMulti 바디 내부에서만 헬퍼 호출(다른 경로 오염 금지 — 크로스는 유지)
            string impactBody = Slice(src, "public static void PlayImpactMulti", "private static Vector3 CameraHorizontalFaceDir");
            Assert.That(impactBody, Does.Contain("DisableImpactSymbolLayer(instance);"),
                "[189차] 몬스터 피격 임팩트 경로의 싱볼 레이어 비활성화 호출.");

            // 헬퍼 구현 계약 — 재귀 Transform 탐색 + 이름 정확 일치 "Simbol" + SetActive(false)
            string helperBody = Slice(src, "private static void DisableImpactSymbolLayer", "private static bool IsPurple");
            Assert.That(helperBody, Does.Contain("GetComponentsInChildren<Transform>(true)"), "[189차] 재귀 탐색(비활성 포함).");
            Assert.That(helperBody, Does.Contain("\"Simbol\""), "[189차] 이름 정확 일치 대상.");
            Assert.That(helperBody, Does.Contain("SetActive(false)"), "[189차] 레이어 GO 비활성화.");
            Assert.That(helperBody, Does.Contain("if (root == null) return;"), "[189차] null 루트 가드.");
        }

        [Test]
        public void Source_PlayCross_KeepsSymbolLayer()
        {
            string src = Read(RunnerPath);
            string crossBody = Slice(src, "public static void PlayCross", "public static void PlayImpactMulti");
            Assert.That(crossBody, Does.Not.Contain("DisableImpactSymbolLayer"),
                "[189차] 크로스(타 완료) 경로는 마법진 레이어 유지 — 사용자 요구 범위는 몬스터 피격 전용.");
        }

        [Test]
        public void Source_ShockwaveNotInDisableLogic()
        {
            string src = Read(RunnerPath);

            // 과다 제거 가드 — "Shockwave"는 어떤 SetActive(false)/비활성 로직에도 등장 금지(주석 줄 제외 검증)
            foreach (string raw in src.Split('\n'))
            {
                string line = raw.TrimStart();
                if (line.StartsWith("//")) continue;   // 주석 줄 제외
                if (line.Contains("SetActive(false)") && line.Contains("Shockwave"))
                    Assert.Fail("[189차] Shockwave(히트 링/원)가 비활성화 로직에 유입됨 — 과다 제거 금지: " + line);
            }

            // 헬퍼 바디에는 오직 "Simbol"만 비활성화 대상
            string helperBody = Slice(src, "private static void DisableImpactSymbolLayer", "private static bool IsPurple");
            Assert.That(helperBody, Does.Not.Contain("Shockwave"), "[189차] 헬퍼는 Shockwave를 건드리지 않는다.");
            Assert.That(helperBody, Does.Not.Contain("\"Flash\""), "[189차] 헬퍼는 Flash를 건드리지 않는다.");
        }

        [Test]
        public void Prefab_ContainsSimbolAndShockwaveChildren()
        {
            Assert.That(System.IO.File.Exists(PrefabPath), Is.True, "[189차] MagicHit.prefab 존재(에셋 무변경 계약).");
            string prefab = Read(PrefabPath);
            // 에셋 자체는 손대지 않는다(런타임 SetActive만) — 두 레이어 GO가 모두 직렬화되어 있어야 함
            Assert.That(prefab, Does.Contain("m_Name: Simbol"), "[189차] 마법진(싱볼) 레이어 GO 존재 — 런타임에만 OFF.");
            Assert.That(prefab, Does.Contain("m_Name: Shockwave"), "[189차] 히트 링(쇼크웨이브) 레이어 GO 존재 — 유지.");
        }
    }
}
