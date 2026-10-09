using System.IO;
using ProjectName.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests
{
    /// <summary>
    /// Focused EditMode coverage for the live bow aim path and its screen-to-world alignment.
    /// </summary>
    public class BowAimAlignmentTests
    {
        [SetUp]
        public void ClearStaleBowAimTestSession()
        {
            // Static state survives between EditMode cases and editor test runs; leave no
            // drawing/release event behind for the next scenario.
            BowAimState.Release(false, 0f);
            BowAimState.ResetRelease();
        }

        [TearDown]
        public void ClearBowAimTestSessionAfterEachCase()
        {
            BowAimState.Release(false, 0f);
            BowAimState.ResetRelease();
        }

        private static string ReadAssetScript(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
        }

        private static string ExtractMethod(string source, string signature)
        {
            int methodStart = source.IndexOf(signature);
            Assert.That(methodStart, Is.GreaterThanOrEqualTo(0), "Expected live method was not found: " + signature);

            int bodyStart = source.IndexOf('{', methodStart);
            Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), "Expected method body was not found: " + signature);

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                    return source.Substring(methodStart, i - methodStart + 1);
            }

            Assert.Fail("Unterminated method body: " + signature);
            return string.Empty;
        }

        [TestCase(true, false, false, true, TestName = "CameraFreezePredicate_FreezesWhileDrawing")]
        [TestCase(false, true, true, true, TestName = "CameraFreezePredicate_FreezesReleasePendingOnReleaseFrame")]
        [TestCase(false, true, false, false, TestName = "CameraFreezePredicate_DoesNotFreezePendingReleaseAfterReleaseFrame")]
        [TestCase(false, false, false, false, TestName = "CameraFreezePredicate_DoesNotFreezeWhileIdle")]
        public void CameraFreezePredicateMatchesBowAimState(bool drawing, bool releasePending, bool leftReleased, bool expected)
        {
            var predicate = typeof(TopDownCameraController).GetMethod("ShouldFreezeCameraForBowAim",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(predicate, Is.Not.Null, "Camera must expose a pure bow-aim freeze predicate.");

            bool actual = (bool)predicate.Invoke(null, new object[] { drawing, releasePending, leftReleased });
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void CameraLateUpdateFreezesBeforeCursorControlsAndCameraTransforms()
        {
            string cameraSource = ReadAssetScript("Scripts/Systems/TopDownCameraController.cs");
            string lateUpdate = ExtractMethod(cameraSource, "void LateUpdate()");
            int freezeGate = lateUpdate.IndexOf("ShouldFreezeCameraForBowAim", System.StringComparison.Ordinal);
            Assert.That(freezeGate, Is.GreaterThanOrEqualTo(0), "LateUpdate must gate camera work during bow aim.");
            Assert.That(freezeGate, Is.LessThan(lateUpdate.IndexOf("HandleCursor();", System.StringComparison.Ordinal)),
                "The camera freeze must precede cursor-yaw/pitch updates.");
            Assert.That(freezeGate, Is.LessThan(lateUpdate.IndexOf("transform.position = desiredPos", System.StringComparison.Ordinal)),
                "The camera freeze must precede camera transform changes.");
            Assert.That(freezeGate, Is.LessThan(lateUpdate.IndexOf("transform.LookAt", System.StringComparison.Ordinal)),
                "The camera freeze must precede camera orientation changes.");
        }

        [Test]
        public void BowAimStateRejectsPriorDrawSampleUntilReticlePublishesNewPoint()
        {
            Vector2 oldScreenPoint = new Vector2(20f, 30f);
            Vector2 newScreenPoint = new Vector2(480f, 270f);
            BowAimState.SetAimScreenPoint(oldScreenPoint);
            BowAimState.Begin();

            Assert.That(BowAimState.TryGetAimScreenPoint(out Vector2 rejectedPoint), Is.False,
                "A sample left by an earlier shot must not be accepted for a new draw.");
            Assert.That(rejectedPoint, Is.EqualTo(oldScreenPoint));

            BowAimState.SetAimScreenPoint(newScreenPoint);
            Assert.That(BowAimState.TryGetAimScreenPoint(out Vector2 acceptedPoint), Is.True,
                "A reticle update after draw begins publishes a valid sample.");
            Assert.That(acceptedPoint, Is.EqualTo(newScreenPoint));
            BowAimState.Release(false, 0f);
        }

        [Test]
        public void BowReleaseCapturesCursorAfterLastReticleTickAndSharesSnapshotWithFadeAndSolver()
        {
            Vector2 displayedPointA = new Vector2(80f, 120f);
            Vector2 releasePointB = new Vector2(610f, 430f);
            Assert.That(releasePointB, Is.Not.EqualTo(displayedPointA),
                "The deliberate cursor move must distinguish the prior 16ms display sample from the release pixel.");

            BowAimState.Begin();
            BowAimState.SetAimScreenPoint(displayedPointA);
            Assert.That(BowAimState.TryGetAimScreenPoint(out Vector2 displayedSample), Is.True);
            Assert.That(displayedSample, Is.EqualTo(displayedPointA));

            var capture = typeof(BowAimState).GetMethod("CaptureReleaseAimScreenPoint",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            var getter = typeof(BowAimState).GetMethod("TryGetReleaseAimScreenPoint",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            Assert.That(capture, Is.Not.Null,
                "Release must synchronously capture the current input pixel instead of waiting for the reticle scheduler.");
            Assert.That(getter, Is.Not.Null,
                "The solver and reticle fade must be able to consume the same frozen release pixel.");
            if (capture == null || getter == null) return; // Keep the RED contract test compilable before the bridge exists.

            capture.Invoke(null, new object[] { releasePointB });
            BowAimState.Release(true, 0.75f);
            object[] getterArgs = { Vector2.zero };
            Assert.That(getter.Invoke(null, getterArgs), Is.EqualTo(true));
            Assert.That((Vector2)getterArgs[0], Is.EqualTo(releasePointB),
                "The release snapshot must be B even though the last published reticle point was A.");

            // The regular, scheduled display sample remains A; release consumers must not
            // silently substitute it for the synchronous B snapshot.
            Assert.That(BowAimState.TryGetAimScreenPoint(out Vector2 stillDisplayedPoint), Is.True);
            Assert.That(stillDisplayedPoint, Is.EqualTo(displayedPointA));

            string combatSource = ReadAssetScript("Scripts/Systems/PlayerCombat.cs");
            string updateMethod = ExtractMethod(combatSource, "private void Update()");
            string shotMethod = ExtractMethod(combatSource, "private void TryBowShot(float power,");
            string reticleSource = ReadAssetScript("Scripts/UI/Toolkit/BowAimReticleUTK.cs");
            string tickMethod = ExtractMethod(reticleSource, "private void UpdateTick()");
            string releasePositionMethod = ExtractMethod(reticleSource, "private void UpdateReleasePosition()");

            Assert.That(updateMethod, Does.Contain("Mouse.current.position.ReadValue()")
                    .And.Contain("BowAimState.CaptureReleaseAimScreenPoint"),
                "PlayerCombat must capture B directly from the release-frame mouse input.");
            Assert.That(shotMethod, Does.Contain("BowAimState.TryGetReleaseAimScreenPoint(out Vector2 aimScreenPoint)"),
                "The aim solver must consume B from the dedicated release snapshot.");
            Assert.That(shotMethod, Does.Not.Contain("BowAimState.TryGetAimScreenPoint"),
                "The scheduled A sample must never be the release solver's input.");
            Assert.That(tickMethod, Does.Contain("UpdateReleasePosition"),
                "The release fade must be positioned from the same frozen B snapshot as the solver.");
            Assert.That(releasePositionMethod, Does.Contain("PositionAtScreenPoint(root, releasePoint)"),
                "The release snapshot must use the same panel placement path as the live reticle.");
        }

        [Test]
        public void AlternateBowReleaseRoutesFailClosedUnlessNormalDrawOwnsFreshAimSample()
        {
            string combatSource = ReadAssetScript("Scripts/Systems/PlayerCombat.cs");
            string updateMethod = ExtractMethod(combatSource, "private void Update()");
            string queuedReleaseMethod = ExtractMethod(combatSource, "private void ResumeQueuedParryAction()");
            string attackMethod = ExtractMethod(combatSource, "private bool TryAttack()");
            string releaseMethod = ExtractMethod(combatSource, "private void ReleaseBow(float power,");
            string shotMethod = ExtractMethod(combatSource, "private void TryBowShot(float power,");

            Assert.That(queuedReleaseMethod, Does.Contain("CancelBowShotWithoutFreshDraw(\"queued parry release\""),
                "An already-released parry-queued Bow input has no captured aim snapshot and must be explicitly canceled.");
            Assert.That(queuedReleaseMethod, Does.Not.Contain("ReleaseBow("),
                "A queued release must not enter the normal release path without a draw-owned fresh sample.");
            Assert.That(queuedReleaseMethod, Does.Contain("_bowDrawing = true;")
                    .And.Contain("BowAimState.Begin();"),
                "A queued press that remains held still resumes as a fresh normal draw and can later release normally.");
            Assert.That(attackMethod, Does.Contain("CancelBowShotWithoutFreshDraw(\"direct TryAttack fallback\""),
                "The legacy/direct Bow attack fallback must explicitly cancel instead of reusing any prior aim sample.");
            Assert.That(attackMethod, Does.Not.Contain("TryBowShot("),
                "Direct TryAttack must never dispatch a shot without a current draw/release context.");

            int beginDraw = updateMethod.IndexOf("BowAimState.Begin();", System.StringComparison.Ordinal);
            int normalRelease = updateMethod.IndexOf("ReleaseBow(relPower, true);", System.StringComparison.Ordinal);
            Assert.That(beginDraw, Is.GreaterThanOrEqualTo(0), "The normal held-draw route must still begin BowAimState.");
            Assert.That(normalRelease, Is.GreaterThanOrEqualTo(0), "An authorized normal release call must remain present.");
            string releaseGate = updateMethod.Substring(updateMethod.LastIndexOf("if (Mouse.current.leftButton.wasReleasedThisFrame", normalRelease, System.StringComparison.Ordinal));
            Assert.That(releaseGate, Does.Contain("&& _bowDrawing && isBowEquipped"),
                "The normal release gate must require an active Bow draw.");
            Assert.That(releaseMethod, Does.Contain("TryBowShot(power, currentDrawRelease)"),
                "ReleaseBow must propagate its current-draw provenance into the final shot gate.");
            Assert.That(shotMethod, Does.Contain("if (!currentDrawRelease)"),
                "TryBowShot must fail closed when a caller lacks a current draw/release authorization.");
            Assert.That(shotMethod, Does.Contain("BowAimState.TryGetReleaseAimScreenPoint(out Vector2 aimScreenPoint)"),
                "Even authorized normal releases must have a fresh release-frame sample before firing.");
        }

        [Test]
        public void ArrowManagerResolvesInventoryCreatedAfterItsAwake()
        {
            var arrowManagerProperty = typeof(ArrowManager).GetProperty("Instance");
            var inventoryProperty = typeof(ProjectName.Core.PlayerInventory).GetProperty("Instance");
            var arrowManagerSetter = arrowManagerProperty.GetSetMethod(true);
            var inventorySetter = inventoryProperty.GetSetMethod(true);
            var previousArrowManager = arrowManagerProperty.GetValue(null);
            var previousInventory = inventoryProperty.GetValue(null);
            var managerObject = new GameObject("BowLateInventoryManager");
            GameObject inventoryObject = null;
            try
            {
                arrowManagerSetter.Invoke(null, new object[] { null });
                inventorySetter.Invoke(null, new object[] { null });

                var manager = managerObject.AddComponent<ArrowManager>();
                typeof(ArrowManager).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(manager, null);
                Assert.That(manager.HasArrows(), Is.False, "No inventory exists when ArrowManager awakens.");

                inventoryObject = new GameObject("BowLateInventory");
                var inventory = inventoryObject.AddComponent<ProjectName.Core.PlayerInventory>();
                inventorySetter.Invoke(null, new object[] { inventory });
                var slots = typeof(ProjectName.Core.PlayerInventory).GetField("_slots",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                slots.SetValue(inventory, new ProjectName.Core.PlayerInventory.ItemSlot[40]);
                Assert.That(inventory.AddItem(new ProjectName.Core.PlayerInventory.ItemData
                {
                    id = "arrow_regular",
                    displayName = "Regular Arrow",
                    category = ProjectName.Core.PlayerInventory.ItemCategory.Arrow,
                    maxStack = 99
                }, 2), Is.True);

                Assert.That(manager.HasArrows(), Is.True,
                    "ArrowManager must resolve the PlayerInventory that is created after its Awake.");
                Assert.That(manager.GetTotalArrowCount(), Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                if (inventoryObject != null) Object.DestroyImmediate(inventoryObject);
                arrowManagerSetter.Invoke(null, new[] { previousArrowManager });
                inventorySetter.Invoke(null, new[] { previousInventory });
            }
        }

        [Test]
        public void PerspectiveAimUsesExactCameraRayDespiteMuzzleParallaxAndColliderHits()
        {
            var cameraObject = new GameObject("BowAimTestCamera");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var shooter = new GameObject("BowAimTestShooter");
            ArrowProjectile firstProjectile = null;
            ArrowProjectile secondProjectile = null;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.pixelRect = new Rect(0f, 0f, Screen.width, Screen.height);
                camera.transform.position = new Vector3(0f, 8f, -8f);
                camera.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
                camera.orthographic = false;
                camera.fieldOfView = 55f;
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = Vector3.one * 10f;
                Physics.SyncTransforms();

                Vector3 muzzle = camera.transform.position + camera.transform.right * 2f + Vector3.up * 0.4f;
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 offset = new Vector2(Screen.width * 0.68f, Screen.height * 0.55f);
                Ray centerRay = camera.ScreenPointToRay(center);
                Ray offsetRay = camera.ScreenPointToRay(offset);
                Assert.That(ArrowManager.TrySolveAim(camera, center, muzzle, shooter.transform,
                    out Vector3 centerAimPoint, out Vector3 centerDirection), Is.True);
                Assert.That(ArrowManager.TrySolveAim(camera, offset, muzzle, shooter.transform,
                    out Vector3 offsetAimPoint, out Vector3 offsetDirection), Is.True);

                Assert.That(centerAimPoint.y, Is.EqualTo(0f).Within(0.05f),
                    "The camera ray's ground-plane intersection remains available as aim diagnostics.");
                Assert.That(offsetAimPoint.y, Is.EqualTo(0f).Within(0.05f),
                    "The offset camera ray's ground-plane intersection remains available as aim diagnostics.");
                Assert.That(Vector3.Dot(centerDirection, centerRay.direction.normalized), Is.GreaterThan(0.99999f),
                    "The exact normalized camera ray, not a different muzzle-to-hit vector, defines launch direction.");
                Assert.That(Vector3.Dot(offsetDirection, offsetRay.direction.normalized), Is.GreaterThan(0.99999f),
                    "Collider intersections and muzzle parallax must not alter the offset screen point's flight angle.");
                Assert.That(Vector3.Distance(centerDirection, offsetDirection), Is.GreaterThan(0.01f),
                    "Distinct screen positions on a perspective camera must produce distinct launch directions.");

                firstProjectile = ArrowProjectile.Spawn(muzzle, centerDirection, 37f, 10f, Color.white);
                secondProjectile = ArrowProjectile.Spawn(muzzle, offsetDirection, 37f, 10f, Color.white);
                Assert.That(Vector3.Dot(firstProjectile.GetComponent<Rigidbody>().linearVelocity.normalized,
                    centerRay.direction.normalized), Is.GreaterThan(0.99999f),
                    "The spawned projectile's actual velocity must follow the exact first camera ray.");
                Assert.That(Vector3.Dot(secondProjectile.GetComponent<Rigidbody>().linearVelocity.normalized,
                    offsetRay.direction.normalized), Is.GreaterThan(0.99999f),
                    "The spawned projectile's actual velocity must follow the exact offset camera ray.");
                Assert.That(Vector3.Distance(firstProjectile.GetComponent<Rigidbody>().linearVelocity,
                    secondProjectile.GetComponent<Rigidbody>().linearVelocity), Is.GreaterThan(0.1f),
                    "Different screen positions must result in different actual projectile velocities.");
            }
            finally
            {
                Object.DestroyImmediate(shooter);
                if (firstProjectile != null) Object.DestroyImmediate(firstProjectile.gameObject);
                if (secondProjectile != null) Object.DestroyImmediate(secondProjectile.gameObject);
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void OffCenterAimReachesProjectileVelocityWithNoMeshVisuals()
        {
            var cameraObject = new GameObject("BowAimOffCenterCamera");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ArrowProjectile projectile = null;
            bool previousIgnoreFailingMessages = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            try
            {
                // Procedural arrow materials use Renderer.material, which Unity warns about in EditMode.
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = Vector3.zero;
                camera.transform.rotation = Quaternion.identity;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.pixelRect = new Rect(0f, 0f, Screen.width, Screen.height);
                target.transform.position = new Vector3(2f, 1f, 12f);
                target.transform.localScale = new Vector3(2f, 2f, 1f);
                Physics.SyncTransforms();

                // Exercise an off-center, rendered-screen-equivalent sample and a muzzle displaced
                // from the camera ray: the solved impact point must be the actual launch destination.
                Vector2 sample = camera.WorldToScreenPoint(target.transform.position);
                Vector3 muzzle = new Vector3(-1f, 0.25f, 0f);
                Assert.That(ArrowManager.TrySolveAim(camera, sample, muzzle, null,
                    out Vector3 hitPoint, out Vector3 solvedDirection), Is.True);
                Assert.That(hitPoint.x, Is.GreaterThan(0.5f), "The off-center sample must hit the off-center target, not camera center.");

                projectile = ArrowProjectile.Spawn(muzzle, solvedDirection, 37f, 10f, Color.white);
                var body = projectile.GetComponent<Rigidbody>();
                Assert.That(body, Is.Not.Null);
                Assert.That(projectile.GetComponent<TrailRenderer>(), Is.Not.Null,
                    "Adding the arrow body must preserve the existing trail renderer.");
                Assert.That(projectile.GetComponent<CapsuleCollider>(), Is.Not.Null,
                    "The physics body keeps its capsule collision shape without a procedural mesh.");
                Assert.That(projectile.GetComponentsInChildren<MeshRenderer>(true), Is.Empty,
                    "The trail is the only arrow visual; Spawn creates no MeshRenderer geometry.");
                Assert.That(projectile.GetComponentsInChildren<Rigidbody>(true).Length, Is.EqualTo(1),
                    "The root retains its required Rigidbody without adding descendant bodies.");
                Assert.That(Vector3.Distance(body.linearVelocity, solvedDirection * 37f), Is.LessThan(0.001f),
                    "The Rigidbody's initial velocity must retain the solved camera-ray direction and speed.");
                Assert.That(Vector3.Dot(projectile.transform.up, body.linearVelocity.normalized), Is.GreaterThan(0.999f),
                    "The collider cylinder's long +Y axis must follow its actual initial velocity.");
                var trail = projectile.GetComponent<TrailRenderer>();
                Assert.That(trail.startColor.r, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.startColor.g, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.startColor.b, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.endColor.r, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.endColor.g, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.endColor.b, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                if (projectile != null) Object.DestroyImmediate(projectile.gameObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void SpawnCreatesOnlyWhiteNeonTrailAndPhysicsBody()
        {
            Vector3 direction = new Vector3(0.25f, 0.1f, 1f).normalized;
            ArrowProjectile projectile = null;
            bool previousIgnoreFailingMessages = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            try
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
                projectile = ArrowProjectile.Spawn(new Vector3(1f, 2f, 3f), direction, 37f, 10f, Color.white);

                Assert.That(projectile.GetComponent<CapsuleCollider>(), Is.Not.Null,
                    "The root keeps its capsule collision shape without rendering procedural meshes.");
                Assert.That(projectile.GetComponentsInChildren<MeshRenderer>(true), Is.Empty,
                    "The trail is the only arrow visual; no root or child mesh renderer is created.");
                Assert.That(projectile.transform.childCount, Is.EqualTo(0),
                    "Spawn must not create visual child objects.");
                var trail = projectile.GetComponent<TrailRenderer>();
                Assert.That(trail, Is.Not.Null.And.Property("enabled").True,
                    "The existing flight trail must remain enabled.");
                var body = projectile.GetComponent<Rigidbody>();
                Assert.That(body, Is.Not.Null);
                Assert.That(projectile.GetComponentsInChildren<Rigidbody>(true).Length, Is.EqualTo(1),
                    "The root physics body remains, with no added child rigidbodies.");
                Assert.That(body.mass, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Vector3.Distance(body.linearVelocity, direction * 37f), Is.LessThan(0.001f),
                    "Removing arrow geometry must not change initial flight velocity.");
                Assert.That(Vector3.Dot(projectile.transform.up, direction), Is.GreaterThan(0.999f),
                    "The collider cylinder axis must continue to face along flight direction.");
                Assert.That(trail.startColor.r, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.startColor.g, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.startColor.b, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.endColor.r, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.endColor.g, Is.EqualTo(1f).Within(0.001f));
                Assert.That(trail.endColor.b, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                if (projectile != null) Object.DestroyImmediate(projectile.gameObject);
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void AimSolverRejectsMissingCameraWithoutProducingAValidAim()
        {
            bool solved = ArrowManager.TrySolveAim(null, Vector2.zero, Vector3.zero, null,
                out Vector3 aimPoint, out Vector3 direction);

            Assert.That(solved, Is.False, "A missing camera must make the aim solve fail.");
            Assert.That(aimPoint, Is.EqualTo(Vector3.zero), "Failure must not invent a world aim point.");
        }

        [Test]
        public void BowDrawHasNoYellowImguiAimLine()
        {
            string combatSource = ReadAssetScript("Scripts/Systems/PlayerCombat.cs");

            Assert.That(combatSource, Does.Not.Contain("GUI.DrawTexture(new Rect(s0.x, s0.y - 1.5f, len, 3f)"),
                "The former 3px gold bow aim line must stay removed.");
            Assert.That(combatSource, Does.Not.Contain("new Color(1f, 0.85f, 0.4f"),
                "The bow draw's gold IMGUI preview tint must stay removed.");
        }

        [Test]
        public void BowShotAndUtkReticleUseTheSameMouseCursorAimPoint()
        {
            string combatSource = ReadAssetScript("Scripts/Systems/PlayerCombat.cs");
            string reticleSource = ReadAssetScript("Scripts/UI/Toolkit/BowAimReticleUTK.cs");
            string bootstrapSource = ReadAssetScript("Scripts/UI/Toolkit/UIToolkitBootstrap.cs");
            string shotMethod = ExtractMethod(combatSource, "private void TryBowShot(float power,");
            string cameraMethod = ExtractMethod(combatSource, "private Camera ResolveBowAimCamera()");
            string solveMethod = ExtractMethod(ReadAssetScript("Scripts/Systems/ArrowManager.cs"), "public static bool TrySolveAim(");
            string tickMethod = ExtractMethod(reticleSource, "private void UpdateTick()");
            string positionMethod = ExtractMethod(reticleSource, "private void UpdatePosition()");

            Assert.That(bootstrapSource, Does.Contain("BowAimReticleUTK.Ensure();"),
                "The existing UI Toolkit bootstrap must keep attaching the bow reticle.");
            Assert.That(tickMethod, Does.Contain("UpdatePosition();"),
                "While bow aim is active, the reticle update loop must refresh its cursor position.");
            string fadeBlock = tickMethod.Substring(tickMethod.IndexOf("if (_fadeStart >= 0f)", System.StringComparison.Ordinal));
            Assert.That(fadeBlock, Does.Contain("if (BowAimState.Drawing)\n                    UpdatePosition();"),
                "A new draw during the previous release fade must publish fresh aim samples instead of being starved.");

            Assert.That(positionMethod, Does.Contain("ArrowManager.GetAimScreenPoint()"),
                "The reticle must publish one source sample for its drawn center and normal-draw state.");
            Assert.That(positionMethod, Does.Contain("BowAimState.SetAimScreenPoint(screen)"),
                "The screen point actually used to place the visible reticle must be recorded in the Systems bridge.");
            Assert.That(shotMethod, Does.Contain("BowAimState.TryGetReleaseAimScreenPoint(out Vector2 aimScreenPoint)"),
                "Firing must consume the synchronized release-frame pixel, rather than a scheduled display sample or new read.");
            Assert.That(shotMethod, Does.Not.Contain("ArrowManager.GetAimScreenPoint()"),
                "The release path must not resample a cursor point that the visible reticle has not rendered yet.");
            string stateSource = ReadAssetScript("Scripts/Systems/BowAimState.cs");
            Assert.That(stateSource, Does.Contain("SetAimScreenPoint(Vector2 screenPoint)"),
                "Systems bridge must own the screen-point snapshot shared with UI Toolkit.");
            Assert.That(stateSource, Does.Contain("TryGetAimScreenPoint(out Vector2 screenPoint)"),
                "Systems must preserve the separately recorded displayed aim sample.");
            Assert.That(stateSource, Does.Contain("TryGetReleaseAimScreenPoint(out Vector2 screenPoint)"),
                "Systems must expose the synchronously captured release pixel shared with shot and fade.");
            Assert.That(stateSource, Does.Contain("_drawStartAimSampleVersion = _aimSampleVersion"),
                "The reticle must render at least once after bow draw begins before a release sample is accepted.");
            Assert.That(stateSource, Does.Contain("_aimSampleVersion != _drawStartAimSampleVersion"),
                "A stale cursor sample left by a previous shot must not be reused for a new draw.");
            Assert.That(stateSource, Does.Contain("unchecked { _aimSampleVersion++; }"),
                "Every reticle placement must advance the sample generation to reject stale release data.");
            string aimPointMethod = ExtractMethod(ReadAssetScript("Scripts/Systems/ArrowManager.cs"), "public static Vector2 GetAimScreenPoint()");
            Assert.That(aimPointMethod, Does.Contain("Mouse.current"),
                "The shared screen point must originate from the Input System mouse.");
            Assert.That(aimPointMethod, Does.Contain("mouse.position.ReadValue()"),
                "The shared screen point must read the current mouse position.");

            Assert.That(shotMethod, Does.Contain("Camera aimCamera = ResolveBowAimCamera()"),
                "Each release must resolve the current MainCamera instead of trusting a stale Start cache.");
            Assert.That(cameraMethod, Does.Contain("Camera currentMainCamera = Camera.main"),
                "A missing or stale cached camera must be reacquired from the MainCamera producer.");
            Assert.That(cameraMethod, Does.Contain("!currentMainCamera.CompareTag(\"MainCamera\")"),
                "An untagged camera is not a valid bow aim camera.");
            Assert.That(shotMethod, Does.Contain("if (aimCamera == null)"),
                "No MainCamera must cancel the shot instead of firing forward.");
            Assert.That(shotMethod, Does.Contain("BowAimState.Release(false, power)"),
                "A camera/solver failure must cancel the displayed release state without reporting a fired shot.");
            Assert.That(shotMethod, Does.Contain("if (!ArrowManager.TrySolveAim(aimCamera, aimScreenPoint, origin, transform,"),
                "A failed aim solve must cancel the shot instead of retaining a forward fallback.");
            Assert.That(shotMethod, Does.Contain("out Vector3 aimPoint, out Vector3 dir)"),
                "Shot aim point and muzzle direction must come from the same solver result.");
            Assert.That(shotMethod, Does.Contain("TryShootArrow(origin, dir, WeaponData.Bow.damage, power,"),
                "The solved muzzle direction, existing damage, and draw power must reach the projectile unchanged.");
            Assert.That(ReadAssetScript("Scripts/Systems/ArrowManager.cs"), Does.Contain("TryShootArrow(Vector3 origin, Vector3 direction, float baseDamage, float power,")
                    .And.Contain("out ArrowProjectile projectile)"),
                "The release must correlate diagnostics with the exact spawned projectile instance.");
            Assert.That(shotMethod, Does.Contain("Quaternion.LookRotation(launchForward.normalized, Vector3.up)"),
                "The player visual facing must follow the horizontal projection of the solved launch direction.");
            Assert.That(shotMethod.IndexOf("ArrowManager.TryShootArrow(origin, dir", System.StringComparison.Ordinal),
                Is.LessThan(shotMethod.IndexOf("Quaternion.LookRotation(launchForward.normalized", System.StringComparison.Ordinal)),
                "Spawn velocity must be set from the resolved muzzle direction before rotating the shooter.");
            Assert.That(ReadAssetScript("Scripts/Systems/ArrowProjectile.cs"), Does.Contain("rb.linearVelocity = launchDirection * speed"),
                "The launch direction must be the projectile's initial velocity vector.");
            Assert.That(ReadAssetScript("Scripts/Systems/ArrowProjectile.cs"), Does.Contain("_rb.linearVelocity += Physics.gravity * GravityScale * Time.deltaTime"),
                "Later gravity curvature is distinct from initial launch alignment.");
            Assert.That(solveMethod, Does.Contain("camera.ScreenPointToRay(aimScreenPoint)"),
                "The shared solver must convert the reticle screen point into a camera ray.");
            Assert.That(solveMethod, Does.Contain("muzzleDirection = ray.direction.normalized;"),
                "Only the normalized camera ray may define the launch direction; hit points are diagnostic only.");
            Assert.That(solveMethod, Does.Not.Contain("aimPoint - muzzleOrigin"),
                "A muzzle-to-hit vector would allow collider range or muzzle parallax to change flight angle.");
            Assert.That(solveMethod, Does.Contain("aimScreenPoint.x < 0f || aimScreenPoint.x > Screen.width"),
                "Out-of-display reticle samples must be rejected, not clamped onto a different ray.");
            Assert.That(solveMethod, Does.Contain("aimScreenPoint.y < 0f || aimScreenPoint.y > Screen.height"),
                "Out-of-display reticle samples must be rejected, not clamped onto a different ray.");
            Assert.That(solveMethod, Does.Contain("camera.pixelRect.Contains(aimScreenPoint)"),
                "Split-screen or viewport camera rects must cover the exact displayed reticle point.");
            Assert.That(solveMethod, Does.Contain("Debug.LogWarning($\"[BowAim]"),
                "Each shot should log the reticle ray, accepted hit, muzzle, solved direction and provenance together.");
            string projectileSource = ReadAssetScript("Scripts/Systems/ArrowProjectile.cs");
            Assert.That(projectileSource, Does.Contain("[Arrow][P20-4] 스폰 정합"),
                "The projectile logs its own forward-axis/launch-direction alignment at creation.");
            Assert.That(shotMethod, Does.Contain("dir={dir} velocity={projectile.GetComponent<Rigidbody>().linearVelocity}"),
                "The diagnostic must correlate solved direction with the projectile's actual initial velocity.");
            Assert.That(shotMethod.IndexOf("BowAimState.TryGetReleaseAimScreenPoint", System.StringComparison.Ordinal),
                Is.LessThan(shotMethod.IndexOf("ResolveBowAimCamera()", System.StringComparison.Ordinal)),
                "A shot must begin from the frozen release pixel before solving camera ray data.");
            Assert.That(shotMethod, Does.Contain("TryShootArrow(origin, dir, WeaponData.Bow.damage, power,"),
                "The aim change must preserve the existing base damage and draw power inputs.");
            Assert.That(ReadAssetScript("Scripts/Systems/ArrowManager.cs"), Does.Contain("baseDamage + arrowData.damageBonus + power * 8f"),
                "Arrow tier and power damage behavior must remain unchanged.");
            Assert.That(ReadAssetScript("Scripts/Systems/ArrowProjectile.cs"), Does.Contain("GetSpeedForPower(float power) => BaseSpeed * (0.7f + 0.5f * Mathf.Clamp01(power))"),
                "Arrow power-based speed behavior must remain unchanged.");
            Assert.That(reticleSource, Does.Contain("RuntimePanelUtils.ScreenToPanel(root.panel, screen)"),
                "The reticle's shared placement helper must use UI Toolkit's runtime screen-to-panel conversion policy.");
            Assert.That(positionMethod, Does.Not.Contain("Screen.width"),
                "Panel conversion must not approximate panel coordinates with display-size scaling.");
            string placementMethod = ExtractMethod(reticleSource, "private void PositionAtScreenPoint(");
            Assert.That(placementMethod, Does.Contain("style.left ="),
                "The reticle must still be positioned in the UI panel.");
            Assert.That(placementMethod, Does.Contain("style.top ="),
                "The reticle must still be positioned in the UI panel.");

            string constructor = ExtractMethod(reticleSource, "private BowAimReticleUTK()");
            Assert.That(constructor, Does.Contain("_countLabel.style.backgroundColor"),
                "The arrow count must remain legible as a Fluent-styled badge.");
            Assert.That(constructor, Does.Contain("_countLabel.pickingMode = PickingMode.Ignore"),
                "The count badge must remain click-through.");
            Assert.That(constructor, Does.Contain("_emptyLabel"),
                "The reticle must define a distinct empty-ammunition state.");
            Assert.That(constructor, Does.Contain("new Label(\"NO ARROWS\")"),
                "An empty inventory must have an explicit user-facing state.");
            Assert.That(tickMethod, Does.Contain("_emptyLabel.style.display = count <= 0 ? DisplayStyle.Flex : DisplayStyle.None"),
                "The explicit empty state must track the live arrow count.");
            Assert.That(tickMethod, Does.Contain("_countLabel.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None"),
                "The numeric badge must be hidden when no arrows are available.");
            Assert.That(tickMethod, Does.Contain("GetNextArrowType()"),
                "The existing next-arrow type indicator must remain active when ammunition is available.");
            Assert.That(reticleSource, Does.Contain("PickingMode.Ignore"),
                "The reticle and its visuals must remain click-through.");
        }
    }
}
