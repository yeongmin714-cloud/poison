using System.IO;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [101차 F-5] 소스 어서션 — 테스트씬 15(Test_15_Livelihood) 건설 재료 지급 부트.
    /// 근거: 건설 실사용 테스트 준비 — 플레이어에게 건물 재료(mat_wood/mat_stone/iron_ore) 지급 후
    /// 실제 배치 테스트. 수리 = ①자가부트([RuntimeInitializeOnLoadMethod(AfterSceneLoad)] —
    /// FishingUTK/EnsureFishingSystem 선례 — 씬 파일 직접 수정 최소화) ②씬 가드(
    /// SceneManager.GetActiveScene().name == "Test_15_Livelihood" — 마을씬/세이브 무영향)
    /// ③지급 API(PlayerInventory.AddItem — mat_wood/mat_stone/iron_ore 각 99) ④중복 지급 가드
    /// (정적 플래그 세션 1회 — 도메인 리로드 시 초기화).
    /// 런타임 구성 회피 — 씬 로드/씬 전환 구성 없이 소스 문자열로 계약 단정(EditMode 씬 가드 계약).
    /// </summary>
    public class Test15MaterialGrantTests
    {
        [Test]
        public void Test15MaterialGrantBoot_SceneGuard_And_GrantContract_101F5()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/Test15MaterialGrantBoot.cs");

            Assert.That(src, Does.Contain("[101차 F-5]"), "[101차 F-5] 마커 주석.");

            // ① 자가부트 선례 계약 — AfterSceneLoad(RuntimeInitializeOnLoadMethod).
            Assert.That(src, Does.Contain("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]"),
                "[101차 F-5] 자가부트 — AfterSceneLoad(FishingUTK/EnsureFishingSystem 선례 — 씬 배선 최소화).");

            // ② 씬 가드 — Test_15_Livelihood 정확 일치 외 무가동(마을씬/세이브 무영향).
            Assert.That(src, Does.Contain("LivelihoodSceneName = \"Test_15_Livelihood\""),
                "[101차 F-5] 씬 가드 대상 — Test_15_Livelihood(정확 일치 상수).");
            Assert.That(src, Does.Contain("SceneManager.GetActiveScene().name != LivelihoodSceneName) return;"),
                "[101차 F-5] 씬 가드 게이트 — 15씬 외 조기 반환(마을씬/세이브 미가동).");
            Assert.That(src, Does.Contain("using UnityEngine.SceneManagement;"),
                "[101차 F-5] SceneManager 사용(씬 가드 산출 경로).");

            // ③ 지급 API — PlayerInventory.AddItem(mat_wood/mat_stone/iron_ore 각 99).
            Assert.That(src, Does.Contain("inv.AddItem(PlayerInventory.Mat_Wood, GrantCount)"),
                "[101차 F-5] mat_wood 99 지급(PlayerInventory 단일소스 id 계약 — BlueprintData 실측 id).");
            Assert.That(src, Does.Contain("inv.AddItem(PlayerInventory.Mat_Stone, GrantCount)"),
                "[101차 F-5] mat_stone 99 지급.");
            Assert.That(src, Does.Contain("inv.AddItem(PlayerInventory.IronOre, GrantCount)"),
                "[101차 F-5] iron_ore 99 지급.");
            Assert.That(src, Does.Contain("private const int GrantCount = 99;"),
                "[101차 F-5] 지급 수량 99(스펙 지정 — 재료별 동일).");

            // ④ 중복 지급 가드 — 정적 플래그 세션 1회.
            Assert.That(src, Does.Contain("private static bool _granted;"),
                "[101차 F-5] 중복 지급 가드 — 정적 플래그(세션당 1회 — 도메인 리로드 시 초기화).");
            Assert.That(src, Does.Contain("if (_granted) return;"),
                "[101차 F-5] 재진입 무지급(세션당 1회 계약 — 씬 재진입 포함).");

            // ⑤ 로그 계약.
            Assert.That(src, Does.Contain("[101차 F-5] 테스트 재료 지급 — mat_wood="),
                "[101차 F-5] 지급 로그 계약(mat_wood=99 … 형식 — 스펙 지정 태그).");

            // ⑥ 지연 확보 폴링 + 부트 해체 — PlayerInventory.Instance 대기 후 1회 지급(씬 잔존 없음).
            Assert.That(src, Does.Contain("if (inv == null) return;"),
                "[101차 F-5] PlayerInventory 구성 대기 폴링(SetupPlayer → PlayerInventory.Awake 경유 지연 확보).");
            Assert.That(src, Does.Contain("Destroy(gameObject);"),
                "[101차 F-5] 부트 GO 즉시 해체(지급 후 씬 잔존 없음).");
        }
    }
}
