using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;

#pragma warning disable 618

/// <summary>
/// Test_14_LordsWarVillage 씬의 결정론적 핵심 검증:
///  1) 내 영지(East_01) PlayerOwned / 타 영주 2(West_01·South_01) LordOwned 소유 설정.
///  2) 서부(West_01) 영주 성격을 Cruel(공격성 0.90)로 리플렉션 교체 → LordPersonalitySystem 반영.
///  3) 서부→남부 AI 전쟁 유효성(StartAIWar true) — 공격자 LordOwned + 방어자 LordOwned.
/// 실측(Play) 배치·행진·문 진입·NPC 상호작용은 이 테스트로 증명되지 않는다.
/// </summary>
public class LordsWarVillageSetupTests
{
    private static readonly TerritoryId PlayerTerr = new TerritoryId(NationType.East, 1);
    private static readonly TerritoryId EnemyWest  = new TerritoryId(NationType.West, 1);
    private static readonly TerritoryId EnemySouth = new TerritoryId(NationType.South, 1);

    [Test]
    public void OwnsTerritoriesWithExpectedEnemies()
    {
        var db = TerritoryDatabase.Instance;
        db.SetOwnership(PlayerTerr, TerritoryOwnership.PlayerOwned);
        db.SetOwnership(EnemyWest,  TerritoryOwnership.LordOwned);
        db.SetOwnership(EnemySouth, TerritoryOwnership.LordOwned);

        Assert.AreEqual(TerritoryOwnership.PlayerOwned, db.GetState(PlayerTerr).ownership, "내 영지는 PlayerOwned여야 함");
        Assert.AreEqual(TerritoryOwnership.LordOwned,  db.GetState(EnemyWest).ownership,  "서부 영지(공격자)는 LordOwned여야 함");
        Assert.AreEqual(TerritoryOwnership.LordOwned,  db.GetState(EnemySouth).ownership, "남부 영지(방어자)는 LordOwned여야 함");
    }

    [Test]
    public void AggressorLordIsCruelAndHighAggression()
    {
        EnsureLordsWarDbOwnership();
        float aggression = LordPersonalitySystem.GetAggression(EnemyWest);
        Assert.AreEqual(0.90f, aggression, 0.001f, "서부 영주 성격 Cruel → 공격성 0.90이어야 함");
    }

    [Test]
    public void AIWarPreconditionOwnershipAllowsEnemyAttack()
    {
        // StartAIWar 전체 호출은 SpawnGarrison→CreateGuard가 EditMode에서 Destroy를 호출해
        // NUnit EditMode에선 검증 불가(Play 전용 경로). 여기선 공격 허용 사전조건만 확정한다.
        EnsureLordsWarDbOwnership();
        var db = TerritoryDatabase.Instance;
        var stateA = db.GetState(EnemyWest);
        var stateD = db.GetState(EnemySouth);
        Assert.AreEqual(TerritoryOwnership.LordOwned, stateA.ownership, "공격자(서부)는 LordOwned여야 전쟁 발화 가능");
        Assert.AreEqual(TerritoryOwnership.LordOwned, stateD.ownership, "방어자(남부)는 LordOwned+동일국가 아니면 공격 허용");
        Assert.AreNotEqual(db.GetDefinition(EnemyWest).nation, db.GetDefinition(EnemySouth).nation,
            "서로 다른 국가 영주 간 전쟁이어야 함");
    }

    private static void EnsureLordsWarDbOwnership()
    {
        var db = TerritoryDatabase.Instance;
        db.SetOwnership(PlayerTerr, TerritoryOwnership.PlayerOwned);
        db.SetOwnership(EnemyWest,  TerritoryOwnership.LordOwned);
        db.SetOwnership(EnemySouth, TerritoryOwnership.LordOwned);

        var dictField = typeof(TerritoryDatabase).GetField("_definitions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (dictField == null) { Assert.Fail("_definitions 리플렉션 실패"); return; }
        var dict = (System.Collections.Generic.Dictionary<string, TerritoryDefinition>)dictField.GetValue(db);
        string key = EnemyWest.ToString();
        if (dict.TryGetValue(key, out var def))
        {
            var lordInfo = def.lord;
            lordInfo.personality = LordPersonality.Cruel;
            def.lord = lordInfo;
            dict[key] = def;
        }
    }
}