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
        public void SharedPresentation_UsesOneShaderMaterialConfigurationWithTintOnlyDifferences()
        {
            var gold = CommandRingPresentation.CreateMaterial(CommandRingPresentation.MoveTargetColor);
            var blue = CommandRingPresentation.CreateMaterial(CommandRingPresentation.SelectedGuardColor);
            var red = CommandRingPresentation.CreateMaterial(CommandRingPresentation.ExplicitTargetColor);
            try
            {
                // [2026-10-11 사용자 요구 — 링 수정] 시각 소스 = Custom/SelectionRing 셰이더 단일 소스.
                //   구 베이크 MoveTargetRing 텍스처 경로는 환형 밴드 결손 에셋 + 검은 사각형 렌더로 폐기.
                Assert.IsNotNull(gold.shader);
                Assert.AreEqual("Custom/SelectionRing", gold.shader.name,
                    "all rings share the SDF selection shader (broken baked-texture path retired)");
                Assert.AreSame(gold.shader, blue.shader);
                Assert.AreSame(gold.shader, red.shader);
                Assert.AreEqual(CommandRingPresentation.MoveTargetColor, gold.GetColor("_TeamColor"));
                Assert.AreEqual(CommandRingPresentation.SelectedGuardColor, blue.GetColor("_TeamColor"));
                Assert.AreEqual(CommandRingPresentation.ExplicitTargetColor, red.GetColor("_TeamColor"));
                Assert.AreEqual(CommandRingPresentation.ShaderRingRadius, gold.GetFloat("_RingRadius"), 0.0001f);
                Assert.AreEqual(CommandRingPresentation.ShaderRingThickness, gold.GetFloat("_RingThickness"), 0.0001f);
                Assert.AreEqual(CommandRingPresentation.ShaderGlowWidth, gold.GetFloat("_GlowWidth"), 0.0001f);
                Assert.AreEqual(blue.GetFloat("_RingRadius"), red.GetFloat("_RingRadius"), 0.0001f,
                    "all tints use the same ring geometry (tint-only differences)");
                Assert.AreEqual(0f, gold.GetFloat("_PulseSpeed"), 0.0001f,
                    "pulse is owned by GetPulseScale (transform), not the shader");
                // [84차] 회전 대시 승격 — 구 "no arc sweep" 계약을 부드러운 4 대시 회전으로 교체(사용자 고품질 요구).
                Assert.AreEqual(CommandRingPresentation.PersistentArcSpeed, gold.GetFloat("_ArcSpeed"), 0.0001f,
                    "84차 고품질 — 지속형 링 회전 대시 활성(PersistentArcSpeed)");
                Assert.AreEqual(CommandRingPresentation.ShaderSecondRingOffset, gold.GetFloat("_SecondRingOffset"), 0.0001f,
                    "84차 고품질 — 이중 링(주 밴드 외곽 보조 링) 배선");
                Assert.AreEqual(-1f, gold.GetFloat("_WaveT"), 0.0001f,
                    "84차 고품질 — 스폰 웨이브 초기 off(CommandMarker가 ack 버스트에만 구동)");
                Assert.GreaterOrEqual(gold.renderQueue, 3000, "transparent queue for ground decals");
                Assert.AreEqual(0.95f, CommandRingPresentation.BaseScale,
                    "user request 2026-10-11: ring diameter halved (1.9 -> 0.95)");
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
        public void ExecuteMovement_AttackCommandRetargetsNextEnemyAfterResolvedTargetDies()
        {
            var guard = Create("AttackCommandGuard").AddComponent<GuardPlaceholder>();
            // Keep the guard out of melee range so no attack reaches AnimalAI damage/singleton paths; target
            // resolution depends only on the command point, not on the guard's own position.
            guard.transform.position = new Vector3(-10f, 0f, 0f);
            // A is nearer the command point (origin) than B, and both are inside TARGET_SEARCH_RADIUS.
            var targetA = CreateAnimalTarget("AttackTargetA", new Vector3(2f, 0f, 0f));
            var targetB = CreateAnimalTarget("AttackTargetB", new Vector3(0f, 0f, 2.2f));
            Physics.SyncTransforms();
            guard.SetCommandTarget(Vector3.zero, true);

            guard.ExecuteMovement();
            Assert.AreSame(targetA, guard.CurrentAttackTarget, "가장 가까운 적 A가 먼저 지정되어야 한다.");
            // EditMode: AnimalAI.TakeDamage touches DontDestroyOnLoad singletons (MonsterAggroSystem), so
            // simulate invalidation/death via deactivation — the same condition ValidateAttackTarget rejects.
            targetA.gameObject.SetActive(false);
            guard.ExecuteMovement();

            Assert.IsTrue(guard.HasCommand, "지정 대상 사망 후에도 공격 명령은 유지되어야 한다(명령 종료 금지).");
            Assert.IsTrue(guard.IsAttackCommand, "사망 후에도 명령 종류는 공격이어야 한다.");
            Assert.AreSame(targetB, guard.CurrentAttackTarget, "사망한 A 대신 탐색 반경 내 유효한 적 B를 재지정해야 한다.");
        }

        [Test]
        public void ExecuteMovement_AttackCommandWaitsAtPointAfterTargetDiesWithoutReplacement()
        {
            var guard = Create("AttackCommandGuard").AddComponent<GuardPlaceholder>();
            guard.transform.position = new Vector3(-10f, 0f, 0f);
            var commandPoint = new Vector3(0f, 0f, 0f);
            var targetA = CreateAnimalTarget("AttackTargetA", new Vector3(2f, 0f, 0f));
            Physics.SyncTransforms();
            guard.SetCommandTarget(commandPoint, true);

            guard.ExecuteMovement();
            Assert.AreSame(targetA, guard.CurrentAttackTarget);
            targetA.gameObject.SetActive(false);   // see note in the retarget test
            guard.ExecuteMovement();

            Assert.IsTrue(guard.HasCommand, "대체 대상이 없어도 공격 명령은 대기 상태로 유지되어야 한다.");
            Assert.IsTrue(guard.IsAttackCommand);
            Assert.IsNull(guard.CurrentAttackTarget, "대체 대상이 없으면 추적 대상은 비어 있어야 한다.");

            // Later enemy appears inside the search radius of the saved command point; the waiting order must reacquire it.
            var targetC = CreateAnimalTarget("AttackTargetC", new Vector3(0f, 0f, 1.5f));
            Physics.SyncTransforms();
            guard.ExecuteMovement();

            Assert.IsTrue(guard.HasCommand);
            Assert.AreSame(targetC, guard.CurrentAttackTarget, "대기 중인 공격 명령은 이후 등장한 적을 재획득해야 한다.");
        }

        private AnimalAI CreateAnimalTarget(string name, Vector3 position)
        {
            var go = Create(name);
            go.transform.position = position;
            go.AddComponent<SphereCollider>();
            return go.AddComponent<AnimalAI>();
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

        // ── Phase 2C (design spec): ground-anchor attack group target distribution ──
        // RED expectation: current ResolveAttackTarget picks the nearest enemy for every guard,
        // so a group sharing one ground point piles onto one target. These tests specify the
        // desired distribution; production code is NOT changed by this phase.

        [Test]
        public void AttackGroup_ThreeGuardsSharedGroundPoint_DistributeOverThreeAliveEnemies()
        {
            var guards = CreateAttackGroup(3);
            var enemies = new[]
            {
                CreateAnimalTarget("GroupEnemyA", new Vector3(2f, 0f, 0f)),
                CreateAnimalTarget("GroupEnemyB", new Vector3(0f, 0f, 2.2f)),
                CreateAnimalTarget("GroupEnemyC", new Vector3(0f, 0f, -2.4f)),
            };
            Physics.SyncTransforms();

            var assigned = ResolveAttackGroupTargets(guards, Vector3.zero);

            Assert.AreEqual(3, new HashSet<Component>(assigned).Count,
                "3 attackers on a shared ground point must spread across 3 distinct enemies, not pile onto one.");
            CollectionAssert.AreEquivalent(new HashSet<Component>(enemies), new HashSet<Component>(assigned),
                "every assigned target must be one of the alive enemies in range");
        }

        [Test]
        public void AttackGroup_ThreeGuardsSharedGroundPoint_DistributionIsDeterministic()
        {
            var guards = CreateAttackGroup(3);
            CreateAnimalTarget("GroupEnemyA", new Vector3(2f, 0f, 0f));
            CreateAnimalTarget("GroupEnemyB", new Vector3(0f, 0f, 2.2f));
            CreateAnimalTarget("GroupEnemyC", new Vector3(0f, 0f, -2.4f));
            Physics.SyncTransforms();

            var first = ResolveAttackGroupTargets(guards, Vector3.zero);
            var second = ResolveAttackGroupTargets(guards, Vector3.zero);
            var third = ResolveAttackGroupTargets(guards, Vector3.zero);

            CollectionAssert.AreEqual(first, second, "repeated selection with identical inputs must yield the same guard->enemy mapping");
            CollectionAssert.AreEqual(first, third, "repeated selection with identical inputs must yield the same guard->enemy mapping");
        }

        [Test]
        public void AttackGroup_SingleEnemyInRange_AllGuardsFocusThatEnemy()
        {
            var guards = CreateAttackGroup(3);
            var enemy = CreateAnimalTarget("LoneEnemy", new Vector3(2f, 0f, 0f));
            Physics.SyncTransforms();

            var assigned = ResolveAttackGroupTargets(guards, Vector3.zero);

            foreach (var target in assigned)
                Assert.AreSame(enemy, target, "with only one enemy available, every attacker must focus it");
        }

        private List<GuardPlaceholder> CreateAttackGroup(int count)
        {
            var guards = new List<GuardPlaceholder>();
            for (int i = 0; i < count; i++)
            {
                // Far from the command point so no melee attack resolves during the selection step.
                var guard = Create("AttackGroupGuard" + i).AddComponent<GuardPlaceholder>();
                guard.transform.position = new Vector3(-10f, 0f, i * 1.0f);
                guards.Add(guard);
            }
            return guards;
        }

        private static Component[] ResolveAttackGroupTargets(List<GuardPlaceholder> guards, Vector3 groundPoint)
        {
            foreach (var guard in guards)
            {
                guard.SetCommandTarget(groundPoint, true);
                guard.ExecuteMovement();
            }
            var result = new Component[guards.Count];
            for (int i = 0; i < guards.Count; i++)
                result[i] = guards[i].CurrentAttackTarget;
            return result;
        }

        // (c) Explicit entity right-click target: NOT specified here. GuardPlaceholder exposes no
        // explicit-entity focus API (SetCommandTarget takes only a Vector3 + attack flag; the only
        // target accessor is CurrentAttackTarget, which is the resolved nearest enemy). Needs a
        // prior API/design step before a test can be written.

        // ── Phase 2C.1 (RED spec): explicit enemy focus ──
        // Contract under test: GuardPlaceholder.SetExplicitAttackTarget(ProjectName.Systems.IDamageable target, Vector3 anchor).
        // Reflection is used to look up the method signature by exact parameter types.

        [Test]
        public void SetExplicitAttackTarget_MethodExistsOnGuardPlaceholder()
        {
            var method = typeof(GuardPlaceholder).GetMethod(
                "SetExplicitAttackTarget",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(ProjectName.Systems.IDamageable), typeof(Vector3) },
                null);

            Assert.NotNull(method,
                "GuardPlaceholder must expose public instance method SetExplicitAttackTarget(ProjectName.Systems.IDamageable, Vector3) for explicit enemy focus.");
        }

        [Test]
        public void SetExplicitAttackTarget_PinsClickedEnemyOverNearerDistractor()
        {
            var method = typeof(GuardPlaceholder).GetMethod(
                "SetExplicitAttackTarget",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(ProjectName.Systems.IDamageable), typeof(Vector3) },
                null);
            Assert.NotNull(method, "precondition: SetExplicitAttackTarget(ProjectName.Systems.IDamageable, Vector3) must exist (see MethodExistsOnGuardPlaceholder).");

            var guard = Create("ExplicitFocusGuard").AddComponent<GuardPlaceholder>();
            // Far from both enemies so no melee attack (PerformAttack) resolves during the movement step.
            guard.transform.position = new Vector3(-10f, 0f, 0f);
            // Ground anchor at origin. Clicked enemy is 2.0m from the anchor; the distractor is 1.0m from the
            // anchor and also nearer to the guard, so nearest-enemy resolution would pick the distractor.
            var clicked = CreateAnimalTarget("ExplicitClickedEnemy", new Vector3(2f, 0f, 0f));
            var distractor = CreateAnimalTarget("NearerDistractorEnemy", new Vector3(0f, 0f, 1f));
            Physics.SyncTransforms();
            var anchor = Vector3.zero;

            method.Invoke(guard, new object[] { clicked, anchor });
            guard.ExecuteMovement();

            Assert.IsTrue(guard.HasCommand, "explicit attack focus must hold an active attack command.");
            Assert.IsTrue(guard.IsAttackCommand, "explicit attack focus must be an attack command.");
            Assert.AreSame(clicked, guard.CurrentAttackTarget,
                "explicitly clicked enemy must stay pinned as CurrentAttackTarget even when a nearer distractor exists.");
            Assert.AreNotSame(distractor, guard.CurrentAttackTarget,
                "nearest-enemy resolution must not override an explicit focus target.");
        }

        [Test]
        public void AttackGroup_ThreeGuardsTwoAliveEnemies_SplitWithinOneLoadAndNeverPickInactive()
        {
            var guards = CreateAttackGroup(3);
            var enemyA = CreateAnimalTarget("GroupEnemyA", new Vector3(2f, 0f, 0f));
            var enemyB = CreateAnimalTarget("GroupEnemyB", new Vector3(0f, 0f, 2.2f));
            var deadEnemyC = CreateAnimalTarget("GroupEnemyC", new Vector3(0f, 0f, -2.4f));
            // Exclude C via deactivation (EditMode: avoids the AnimalAI death/VFX singleton path).
            deadEnemyC.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var assigned = ResolveAttackGroupTargets(guards, Vector3.zero);

            Assert.AreEqual(2, new HashSet<Component>(assigned).Count,
                "3 attackers with 2 alive enemies must use both alive enemies (at least 2 distinct targets).");
            int loadA = 0;
            int loadB = 0;
            foreach (var target in assigned)
            {
                bool isA = ReferenceEquals(target, enemyA);
                bool isB = ReferenceEquals(target, enemyB);
                Assert.IsTrue(isA || isB, "every assigned target must be one of the alive candidates A/B, never the inactive C.");
                if (isA) loadA++;
                else loadB++;
            }
            Assert.AreEqual(3, loadA + loadB);
            Assert.LessOrEqual(Mathf.Abs(loadA - loadB), 1,
                "least-loaded assignment must keep the per-enemy attacker count within 1 of each other.");
        }
    }
}
