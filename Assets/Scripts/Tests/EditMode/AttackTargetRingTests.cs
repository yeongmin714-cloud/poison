using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class AttackTargetRingTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void SharedPresentation_UsesOneTextureAndMaterialConfigurationWithTintOnlyDifferences()
        {
            var gold = CommandRingPresentation.CreateMaterial(CommandRingPresentation.MoveTargetColor);
            var blue = CommandRingPresentation.CreateMaterial(CommandRingPresentation.SelectedGuardColor);
            var red = CommandRingPresentation.CreateMaterial(CommandRingPresentation.ExplicitTargetColor);
            try
            {
                var texture = Resources.Load<Texture2D>(CommandRingPresentation.TextureResourcePath);
                Assert.IsNotNull(texture, "the shared ring texture must be available from Resources");
                Assert.AreSame(texture, gold.mainTexture);
                Assert.AreSame(texture, blue.mainTexture);
                Assert.AreSame(texture, red.mainTexture);
                Assert.AreEqual(CommandRingPresentation.MoveTargetColor, gold.color);
                Assert.AreEqual(CommandRingPresentation.SelectedGuardColor, blue.color);
                Assert.AreEqual(CommandRingPresentation.ExplicitTargetColor, red.color);
                Assert.AreEqual(3000, gold.renderQueue);
                Assert.AreEqual(gold.GetInt("_SrcBlend"), blue.GetInt("_SrcBlend"));
                Assert.AreEqual(gold.GetInt("_SrcBlend"), red.GetInt("_SrcBlend"));
                Assert.AreEqual(gold.GetInt("_DstBlend"), blue.GetInt("_DstBlend"));
                Assert.AreEqual(gold.GetInt("_DstBlend"), red.GetInt("_DstBlend"));
                Assert.IsTrue(gold.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
                Assert.IsTrue(blue.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
                Assert.IsTrue(red.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
                Assert.AreSame(gold.shader, blue.shader);
                Assert.AreSame(gold.shader, red.shader);
                Assert.AreEqual(1.9f, CommandRingPresentation.BaseScale);
                Assert.AreEqual(0.22f, CommandRingPresentation.RingThickness,
                    "all tints use the same baked MoveTargetRing texture thickness");
                var baseScale = Vector3.one * CommandRingPresentation.BaseScale;
                var pulseAtSample = CommandRingPresentation.GetPulseScale(baseScale, 0.75f);
                Assert.AreEqual(baseScale.x, pulseAtSample.x / pulseAtSample.y * baseScale.y, 0.0001f,
                    "pulse scales all axes uniformly");
                Assert.AreEqual(1f + CommandRingPresentation.PulseAmplitude * Mathf.Sin(0.75f * CommandRingPresentation.PulseFrequency),
                    pulseAtSample.x / baseScale.x, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(gold);
                Object.DestroyImmediate(blue);
                Object.DestroyImmediate(red);
            }
        }

        [Test]
        public void Eligibility_RequiresAliveGuardAndActiveAttackCommand()
        {
            var guard = Create("guard").AddComponent<GuardPlaceholder>();
            Assert.IsFalse(AttackTargetRingManager.IsEligibleAttackCommand(guard));

            guard.SetCommandTarget(new Vector3(4f, 0f, 2f), false);
            Assert.IsFalse(AttackTargetRingManager.IsEligibleAttackCommand(guard), "move orders do not designate targets");

            guard.SetCommandTarget(new Vector3(4f, 0f, 2f), true);
            Assert.IsTrue(AttackTargetRingManager.IsEligibleAttackCommand(guard));

            guard.SetCommandTarget(new Vector3(float.NaN, 0f, 2f), true);
            Assert.IsFalse(AttackTargetRingManager.IsEligibleAttackCommand(guard), "invalid target points are ignored");

            guard.gameObject.SetActive(false);
            Assert.IsFalse(AttackTargetRingManager.IsEligibleAttackCommand(guard), "inactive guards are ignored");
        }

        [Test]
        public void Aggregation_MapsExactlyOneRingTargetPerMonster()
        {
            var monsterObject = Create("monster");
            var monster = monsterObject.AddComponent<AnimalAI>();
            var firstTarget = CreateChild(monsterObject, "body collider").transform;
            var secondTarget = CreateChild(monsterObject, "head collider").transform;
            var targets = AttackTargetRingManager.CollectMonsterTargets(new Component[] { firstTarget, secondTarget });

            Assert.AreEqual(1, targets.Count);
            Assert.IsTrue(targets.Contains(monster));
        }

        [Test]
        public void Aggregation_MapsChildTargetsToUniqueLivingMonsterParents()
        {
            var monsterObject = Create("monster");
            var monster = monsterObject.AddComponent<AnimalAI>();
            var firstTarget = CreateChild(monsterObject, "body collider").transform;
            var secondTarget = CreateChild(monsterObject, "head collider").transform;
            var otherObject = Create("not a monster");
            var targets = new List<Component> { firstTarget, secondTarget, otherObject.transform, null };

            var result = AttackTargetRingManager.CollectMonsterTargets(targets);

            Assert.AreEqual(1, result.Count, "several hit components under one monster produce one ring target");
            Assert.IsTrue(result.Contains(monster));

            monsterObject.SetActive(false);
            Assert.AreEqual(0, AttackTargetRingManager.CollectMonsterTargets(targets).Count,
                "inactive/dead target hierarchies are not kept");
        }

        [Test]
        public void Aggregation_RequiresRealResolvedTarget_NotJustAttackPosition()
        {
            var guard = Create("guard").AddComponent<GuardPlaceholder>();
            guard.SetCommandTarget(new Vector3(1f, 0f, 1f), true);
            Assert.IsTrue(AttackTargetRingManager.IsEligibleAttackCommand(guard));
            Assert.IsNull(guard.CurrentAttackTarget, "an attack command point is not itself a designated monster target");
            Assert.AreEqual(0, AttackTargetRingManager.CollectDesignatedMonsterTargets(new[] { guard }).Count);
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        private GameObject CreateChild(GameObject parent, string name)
        {
            var child = Create(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        [TestCase(0f)]
        [TestCase(1.25f)]
        public void ExecuteMovement_AttackCommandPersistsAtAndInsideArrivalRadius(float distanceToWaypoint)
        {
            var guard = Create("AttackCommandGuard").AddComponent<GuardPlaceholder>();
            guard.SetCommandTarget(new Vector3(distanceToWaypoint, 0f, 0f), true);

            // No target is visible yet. Reaching the attack waypoint must leave the order
            // active so the guard can keep searching for a target on later updates.
            guard.ExecuteMovement();

            Assert.IsTrue(guard.HasCommand, "공격 지점 도착은 명령 완료가 아니다. 공격 대상 탐색을 계속할 수 있도록 명령을 유지해야 한다.");
            Assert.IsTrue(guard.IsAttackCommand, "도착 후에도 명령 종류는 공격이어야 한다.");
        }

        [Test]
        public void ExecuteMovement_AttackCommandTracksResolvedMovingTarget()
        {
            var guard = Create("AttackCommandGuard").AddComponent<GuardPlaceholder>();
            var targetGo = Create("AttackTarget");
            targetGo.transform.position = new Vector3(4f, 0f, 0f);
            targetGo.AddComponent<SphereCollider>();
            var target = targetGo.AddComponent<AnimalAI>();
            Physics.SyncTransforms();
            guard.SetCommandTarget(new Vector3(2f, 0f, 0f), true);

            guard.ExecuteMovement();
            Assert.IsTrue(guard.HasCommand, "유효한 공격 명령은 ExecuteMovement 이후 유지되어야 한다.");
            Assert.AreSame(target, guard.CurrentAttackTarget);

            targetGo.transform.position = new Vector3(5f, 0f, 0f);
            Physics.SyncTransforms();
            guard.ExecuteMovement();

            Assert.IsTrue(guard.HasCommand, "이동 중인 대상을 따라가도 공격 명령이 유지되어야 한다.");
            Assert.AreEqual(new Vector3(3f, 0f, 0f), guard.CommandTarget,
                "최초 공격 지점의 타겟 상대 오프셋을 보존하며 접근점을 갱신해야 한다.");
            Assert.AreSame(target, guard.CurrentAttackTarget);
        }

        [Test]
        public void ExecuteMovement_AttackCommandClearsAfterResolvedTargetDies()
        {
            var guard = Create("AttackCommandGuard").AddComponent<GuardPlaceholder>();
            var targetGo = Create("AttackTarget");
            targetGo.transform.position = new Vector3(2f, 0f, 0f);
            targetGo.AddComponent<SphereCollider>();
            var target = targetGo.AddComponent<AnimalAI>();
            Physics.SyncTransforms();
            guard.SetCommandTarget(Vector3.zero, true);

            guard.ExecuteMovement();
            Assert.AreSame(target, guard.CurrentAttackTarget);
            target.TakeDamage(10000f, Vector3.zero);
            guard.ExecuteMovement();

            Assert.IsFalse(guard.HasCommand, "실제 지정 대상이 사망하면 공격 명령이 종료되어야 한다.");
            Assert.IsNull(guard.CurrentAttackTarget);
        }

        [Test]
        public void ClearCommand_ExplicitlyClearsAttackCommand()
        {
            var guard = Create("AttackCommandGuard").AddComponent<GuardPlaceholder>();
            var targetGo = Create("AttackTarget");
            targetGo.transform.position = new Vector3(4f, 0f, 0f);
            targetGo.AddComponent<SphereCollider>();
            var target = targetGo.AddComponent<AnimalAI>();
            Physics.SyncTransforms();
            guard.SetCommandTarget(new Vector3(2f, 0f, 0f), true);
            guard.ExecuteMovement();
            Assert.AreSame(target, guard.CurrentAttackTarget, "테스트 전 공격 대상이 해석되어야 한다.");

            guard.ClearCommand();

            Assert.IsFalse(guard.HasCommand, "명시적 ClearCommand는 공격 명령을 해제해야 한다.");
            Assert.IsFalse(guard.IsAttackCommand);
            Assert.IsNull(guard.CurrentAttackTarget, "명시적 해제는 추적 대상 참조도 정리해야 한다.");
        }

        [Test]
        public void ExecuteMovement_NonAttackMoveStillClearsAtWaypoint()
        {
            var guard = Create("MoveCommandGuard").AddComponent<GuardPlaceholder>();
            guard.SetCommandTarget(Vector3.zero, false);

            guard.ExecuteMovement();

            Assert.IsFalse(guard.HasCommand, "일반 이동 명령은 목적지 도착 시 기존처럼 해제되어야 한다.");
        }
    }
}
