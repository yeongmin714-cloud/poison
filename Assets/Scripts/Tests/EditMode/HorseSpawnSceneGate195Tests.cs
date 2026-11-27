using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [195차 P5] 말 스폰 씬 게이트 계약 — HorseMountSystem의 야생 스폰/세이브 복원은 MainScene 전용.
    /// 배경: Bootstrap(AfterSceneLoad)이 무조건 스폰 → 던전(Test_21_CaveInterior 등)·테스트 씬에서도 말이 등장.
    /// 소스 계약(CombatFxMonsterAlertPhaseETests 관행 — src 문자열 잠금 + 리플렉션 시그니처):
    ///  ① 씬 게이트 존재(GetActiveScene().name + IsMainScene 헬퍼)
    ///  ② "MainScene" 문자열 핀(Assets/Scenes/MainScene.unity 실측)
    ///  ③ 기존 SpawnHorse/SpawnHorses/ApplyImport 시그니처 무손상(리플렉션 — [195차 P5] 게이트만 추가).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 Unity 배치를 실행하지 않음).
    /// </summary>
    public class HorseSpawnSceneGate195Tests
    {
        private static string Src() =>
            System.IO.File.ReadAllText("Assets/Scripts/Systems/HorseMountSystem.cs");

        /// <summary>헤더~다음 멤버 경계까지의 본문 조각 추출(게이트 배선 위치 핀용).</summary>
        private static string MemberSlice(string src, string header, string nextHeader)
        {
            int fn = src.IndexOf(header, System.StringComparison.Ordinal);
            Assert.That(fn, Is.GreaterThanOrEqualTo(0), "[195차 P5] " + header + " 정의가 존재해야 한다.");
            int end = src.IndexOf(nextHeader, fn, System.StringComparison.Ordinal);
            return end > fn ? src.Substring(fn, end - fn) : src.Substring(fn);
        }

        [Test]
        public void G1_SceneGate_Exists()
        {
            var src = Src();
            Assert.That(src, Does.Contain("GetActiveScene().name"),
                "[195차 P5] 로드 씬 이름 조회(SceneManager.GetActiveScene().name)가 존재해야 한다.");
            Assert.That(src, Does.Contain("static bool IsMainScene()"),
                "[195차 P5] 씬 게이트 헬퍼(IsMainScene)가 존재해야 한다.");
        }

        [Test]
        public void G2_MainScene_String_Pinned()
        {
            Assert.That(Src(), Does.Contain("\"MainScene\""),
                "[195차 P5] 게이트 기준 씬명 리터럴 \"MainScene\" 핀 — Assets/Scenes/MainScene.unity 실측(195차).");
        }

        [Test]
        public void G3_SpawnHorses_Gated()
        {
            // SpawnHorses 본문(다음 멤버 SpawnHorse(BreedSpec… 직전)에 게이트가 배선되어야 한다.
            string body = MemberSlice(Src(), "void SpawnHorses()", "void SpawnHorse(BreedSpec");
            Assert.That(body, Does.Contain("if (!IsMainScene())"),
                "[195차 P5] SpawnHorses 본문 진입부에 씬 게이트 분기가 배선되어야 한다(비 MainScene 스폰 스킵).");
            Assert.That(body, Does.Contain("return;"),
                "[195차 P5] 비 MainScene 게이트는 스폰 루프 진입 전 조기 반환해야 한다(Debug.Log 1회).");
        }

        [Test]
        public void G4_ApplyImport_Gated_And_DataPreserved()
        {
            // ApplyImport 본문(다음 멤버 IsMainScene 헬퍼 직전)에 게이트 + 데이터 보존이 배선되어야 한다.
            string body = MemberSlice(Src(), "void ApplyImport(List<HorseSaveEntry> entries)", "static bool IsMainScene()");
            Assert.That(body, Does.Contain("if (!IsMainScene())"),
                "[195차 P5] ApplyImport 본문 진입부에 씬 게이트 분기가 배선되어야 한다(비 MainScene 복원 스킵).");
            Assert.That(body, Does.Contain("PendingImport = new List<HorseSaveEntry>(entries)"),
                "[195차 P5] 비 MainScene 스킵 시 세이브 데이터 보존(pending 재적립)이 배선되어야 한다 — 데이터 유실 금지.");
        }

        [Test]
        public void G5_LegacySpawnSignatures_Intact()
        {
            var t = typeof(ProjectName.Systems.HorseMountSystem);
            const System.Reflection.BindingFlags Priv = System.Reflection.BindingFlags.NonPublic |
                                                        System.Reflection.BindingFlags.Instance;

            var spawnHorses = t.GetMethod("SpawnHorses", Priv, null, System.Type.EmptyTypes, null);
            Assert.IsNotNull(spawnHorses, "[195차 P5] SpawnHorses() 무인자 시그니처 무손상.");

            var breedSpec = t.GetNestedType("BreedSpec", System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(breedSpec, "[195차 P5] BreedSpec 중첩 타입 유지(SpawnHorse 인자 계약).");
            var spawnHorse = t.GetMethod("SpawnHorse", Priv, null, new[] { breedSpec, typeof(Vector3) }, null);
            Assert.IsNotNull(spawnHorse, "[195차 P5] SpawnHorse(BreedSpec, Vector3) 시그니처 무손상.");

            var applyImport = t.GetMethod("ApplyImport", Priv, null,
                new[] { typeof(System.Collections.Generic.List<ProjectName.Systems.HorseSaveEntry>) }, null);
            Assert.IsNotNull(applyImport, "[195차 P5] ApplyImport(List<HorseSaveEntry>) 시그니처 무손상.");
        }
    }
}
