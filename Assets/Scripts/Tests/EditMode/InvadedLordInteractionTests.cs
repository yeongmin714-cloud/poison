using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public sealed class InvadedLordInteractionTests
    {
        [Test]
        public void InteractionPanelDeclaresLordTalkAttackAndThreeDecisionChoices()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");
            string dialogue = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InvadedLordDialogueUTK.cs");
            // [63차 갱신 — 피그마 5종 정합] 하이라이트 인자(true) 추가로 대화하기/공격하기 호출부가 "0, true" 형태로 확장.
            //   계약 유지: Lord 패널에 대화하기(1행 좌측 하이라이트)+공격하기 존재, 3선택지(처형/추궁/풀어주기)는 대화창 담당.
            Assert.That(source, Does.Contain("AddAction(\"대화하기\", 0, true)"));
            Assert.That(source, Does.Contain("\"공격하기\", 1)"));
            Assert.That(dialogue, Does.Contain("처형하기"));
            Assert.That(dialogue, Does.Contain("추궁하기"));
            Assert.That(dialogue, Does.Contain("풀어주기"));
            Assert.That(dialogue, Does.Not.Contain("RevengeListManager.Instance.Interrogate"));
        }

        [Test]
        public void ExecuteAndReleaseCannotResolveUntilEncounterIsDefeated()
        {
            var go = new GameObject("InvadedLordResolutionTest");
            try
            {
                var encounter = go.AddComponent<InvadedLordEncounter>();
                var definition = new TerritoryDefinition
                {
                    id = new TerritoryId(NationType.West, 3),
                    territoryName = "LordRoom_03",
                    nation = NationType.West,
                    lord = new LordInfo { lordName = "영주_03" }
                };
                encounter.Initialize("West_03", definition);
                Assert.That(encounter.Resolve(true), Is.False);
                Assert.That(encounter.Resolve(false), Is.False);
                Assert.That(TerritoryDatabase.Instance.GetState(encounter.TerritoryId).ownership,
                    Is.Not.EqualTo(TerritoryOwnership.PlayerOwned));
            }
            finally
            {
                LordSurrenderSystem.ResetAll();
                Object.DestroyImmediate(go);
            }
        }
    }
}
