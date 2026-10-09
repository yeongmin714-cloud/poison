using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ProjectName.Tests.Rendering
{
    public class AeroLocalizedGasPhase4BTests
    {
        private GameObject _host;
        private GameObject _target;
        private AeroLocalizedGasVolume _volume;
        private AeroGasSprayRuntimeHost _runtimeHost;

        [SetUp]
        public void SetUp()
        {
            // Previous objects are destroyed in TearDown; don't clear the registry behind live components.
            AeroGasSprayBridge.SetRendererSessionEnabled(false);
            AeroGasSprayBridge.Stop();
            AeroLocalizedGasVolume.ResetRegistryForTests();
            _target = new GameObject("AERO test target");
            _host = new GameObject("AERO localized gas test host");
            _host.SetActive(false);
            _volume = _host.AddComponent<AeroLocalizedGasVolume>();
            _volume.Configure(_target.transform, 4f, 2f, 1f, Color.green);
            // Activate first, then explicitly register: EditMode does not reliably invoke lifecycle callbacks.
            _host.SetActive(true);
            _volume.SetEmitting(true);
            EnsureVolumeRegistered(_volume);
            Assert.IsTrue(_volume.isActiveAndEnabled, "Fixture volume must be active after activation.");
            Assert.IsTrue(_volume.IsEmitting, "Fixture volume must be emitting after activation.");
            Assert.Greater(_volume.Alpha, 0f, "Starting emission must produce visible alpha.");
            Assert.Greater(_volume.Density, 0f, "Fixture volume must have positive density.");
            Assert.IsTrue(AeroLocalizedGasVolume.TryGetNearest(Vector3.zero, out AeroLocalizedGasVolume registered),
                "Fixture volume must be present in the active volume registry.");
            Assert.AreSame(_volume, registered);
        }

        [TearDown]
        public void TearDown()
        {
            // Destroying the active host invokes normal OnDisable callbacks and unregisters volumes.
            if (_host != null) Object.DestroyImmediate(_host);
            if (_target != null) Object.DestroyImmediate(_target);
            _runtimeHost = null;
            AeroGasSprayBridge.Stop();
            AeroGasSprayBridge.SetRendererSessionEnabled(false);
            AeroLocalizedGasVolume.ResetRegistryForTests();
        }

        private static void EnsureVolumeRegistered(AeroLocalizedGasVolume volume)
        {
            // OnEnable is idempotent, but skip reflection when Unity already registered the volume.
            if (AeroLocalizedGasVolume.TryGetNearest(volume.WorldPosition, out AeroLocalizedGasVolume registered) &&
                registered == volume)
                return;

            InvokePrivateLifecycle(volume, "OnEnable");
        }

        private static void InvokePrivateLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, "Expected private lifecycle method " + methodName + " on " + component.GetType().Name);
            method.Invoke(component, null);
        }

        [Test]
        public void LocalizedComposite_IsProjectOwnedCustomShaderRatherThanVendorAeroFogShader()
        {
            const string shaderPath = "Assets/Scripts/Rendering/AeroLocalizedGas.shader";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

            Assert.IsNotNull(shader, "The isolated localized-gas renderer needs its project-owned shader asset.");
            Assert.AreEqual("ProjectName/Rendering/LocalizedGasComposite", shader.name,
                "Keep the custom bounded gas compositor distinct from Mirza AERO's vendor shader/material.");
            Assert.Greater(shader.passCount, 0);
            Assert.IsNotNull(typeof(AeroLocalizedGasRendererFeature).GetField("_gasShader",
                BindingFlags.Instance | BindingFlags.NonPublic),
                "The isolated feature consumes the custom shader, not Mirza's custom-lighting material API.");
        }

        [Test]
        public void IsolatedSessionWithoutConfiguredPipeline_LeavesGlobalPipelineSettingsUntouched()
        {
            GameObject sessionObject = new GameObject("AERO isolated session guard test");
            sessionObject.SetActive(false);
            try
            {
                sessionObject.AddComponent<AeroIsolatedRendererSession>();
                RenderPipelineAsset qualityPipelineBefore = QualitySettings.renderPipeline;
                RenderPipelineAsset defaultPipelineBefore = GraphicsSettings.defaultRenderPipeline;
                LogAssert.Expect(LogType.Error,
                    "[AERO Phase4B] Assign an isolated cloned URP asset before enabling the session.");

                sessionObject.SetActive(true);

                Assert.AreSame(qualityPipelineBefore, QualitySettings.renderPipeline,
                    "The test scene session must never swap the project-wide quality pipeline.");
                Assert.AreSame(defaultPipelineBefore, GraphicsSettings.defaultRenderPipeline,
                    "The test scene session must never mutate the project-wide default pipeline.");
                Assert.IsFalse(AeroGasSprayBridge.IsolatedRendererEnabled,
                    "Rejecting a missing renderer keeps the legacy gas path active.");
            }
            finally
            {
                Object.DestroyImmediate(sessionObject);
            }
        }

        [Test]
        public void Configure_ClampsInvalidDimensionsAndTracksTargetInWorldSpace()
        {
            _volume.Configure(_target.transform, -4f, 0f, -1f, Color.magenta);
            Assert.AreEqual(0.1f, _volume.Radius, 0.0001f);
            Assert.AreEqual(0.1f, _volume.Height, 0.0001f);
            Assert.AreEqual(0f, _volume.Density, 0.0001f);
            _volume.Configure(_target.transform, 4f, 2f, 1f, Color.green);
            _target.transform.position = new Vector3(12f, 3f, -6f);
            Assert.AreEqual(_target.transform.position, _volume.WorldPosition);
        }

        [Test]
        public void EmissionToggle_ActivatesThenFadesAndCameraSelectionIgnoresDisabledVolume()
        {
            Assert.IsTrue(_volume.IsEmitting);
            Assert.Greater(_volume.Alpha, 0f);
            Assert.IsTrue(AeroLocalizedGasVolume.TryGetNearest(Vector3.zero, out AeroLocalizedGasVolume selected));
            Assert.AreSame(_volume, selected);
            _volume.SetEmitting(false);
            Assert.IsFalse(_volume.IsEmitting);
            Assert.IsTrue(AeroLocalizedGasVolume.TryGetNearest(Vector3.zero, out selected));
            Assert.AreSame(_volume, selected);
            _host.SetActive(false);
            if (!_host.activeInHierarchy) InvokePrivateLifecycle(_volume, "OnDisable");
            Assert.IsFalse(AeroLocalizedGasVolume.TryGetNearest(Vector3.zero, out selected));
        }

        [Test]
        public void LocalDensity_IsBoundedByRadiusAndHeightAndShapesForwardPlume()
        {
            // Make the expected shape unambiguous: the plume reaches beyond the radius of the central volume.
            _volume.Configure(_target.transform, 4f, 2f, 1f, Color.green, plumeLength: 6f);
            Vector3 origin = _volume.WorldPosition;
            Vector3 forward = _volume.WorldDirection;
            Vector3 radial = Vector3.Cross(forward, Vector3.up).normalized;
            Assert.Greater(radial.sqrMagnitude, 0.9f, "Fixture direction must allow an independent lateral sample.");

            // Independent sample geometry: center is inside the core; 5 units forward is outside its 4-unit radius
            // but inside the configured plume; the remaining samples are well beyond the radial and height bounds.
            AssertDensityAt(origin, true, "volume center");
            AssertDensityAt(origin + forward * 5f, true, "forward plume beyond the core radius");
            AssertDensityAt(origin + radial * 100f, false, "outside radial extent");
            AssertDensityAt(origin + Vector3.up * 100f, false, "outside vertical extent");
        }

        private void AssertDensityAt(Vector3 worldPosition, bool expectedPositive, string sampleDescription)
        {
            float actualDensity = _volume.EvaluateWorldDensity(worldPosition);
            if (expectedPositive)
                Assert.Greater(actualDensity, 0f, "Expected positive density at " + sampleDescription + ".");
            else
                Assert.AreEqual(0f, actualDensity, 0.0001f,
                    "Expected zero density at " + sampleDescription + ".");
        }

        [Test]
        public void MultipleCamerasSelectTheirNearestIndependentVolume()
        {
            GameObject other = new GameObject("Second AERO gas host");
            other.SetActive(false);
            AeroLocalizedGasVolume second = other.AddComponent<AeroLocalizedGasVolume>();
            try
            {
                other.transform.position = new Vector3(20f, 0f, 0f);
                second.Configure(other.transform, 3f, 2f, 1f, Color.cyan);
                other.SetActive(true);
                second.SetEmitting(true);
                EnsureVolumeRegistered(second);
                Assert.IsTrue(second.isActiveAndEnabled, "Second volume must be active after activation.");

                Assert.IsTrue(AeroLocalizedGasVolume.TryGetNearest(Vector3.zero, out AeroLocalizedGasVolume nearOrigin));
                Assert.AreSame(_volume, nearOrigin);
                Assert.IsTrue(AeroLocalizedGasVolume.TryGetNearest(new Vector3(25f, 0f, 0f), out AeroLocalizedGasVolume nearSecond));
                Assert.AreSame(second, nearSecond);
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void GameplayBridge_CreatesTintedFollowingVolumeOnlyForIsolatedSession()
        {
            // Separate the fixture volume from the runtime-created emitter so registry selection
            // proves the new component registered independently.
            _host.transform.position = new Vector3(-100f, 0f, 0f);
            _volume.Configure(_host.transform, 4f, 2f, 1f, Color.green);
            // _host is active here. Invoke lifecycle directly so EditMode event subscription is deterministic.
            _runtimeHost = _host.AddComponent<AeroGasSprayRuntimeHost>();
            InvokePrivateLifecycle(_runtimeHost, "OnEnable");
            Assert.IsTrue(_runtimeHost.isActiveAndEnabled);
            AeroGasSprayRuntimeHost runtimeHost = _runtimeHost;
            AeroGasSprayPayload payload = new AeroGasSprayPayload(
                _target.transform, 6f, 3f, 1f, new Color(0.8f, 0.1f, 0.2f, 0.45f));

            AeroGasSprayBridge.Publish(payload);
            Assert.IsNull(runtimeHost.Volume, "No renderer session means no gameplay emitter.");

            AeroGasSprayBridge.SetRendererSessionEnabled(true);
            Assert.IsNotNull(runtimeHost.Volume);
            Assert.IsTrue(runtimeHost.Volume.isActiveAndEnabled,
                "Runtime-created emitter must be active.");
            EnsureVolumeRegistered(runtimeHost.Volume);
            Assert.IsTrue(AeroLocalizedGasVolume.TryGetNearest(Vector3.zero, out AeroLocalizedGasVolume nearest),
                "Runtime-created emitter must be present in the active volume registry.");
            Assert.AreSame(runtimeHost.Volume, nearest);
            Assert.AreSame(_target.transform, runtimeHost.Volume.FollowTarget);
            Assert.AreEqual(payload.Radius, runtimeHost.Volume.Radius, 0.0001f);
            Assert.AreEqual(payload.Height, runtimeHost.Volume.Height, 0.0001f);
            Assert.AreEqual(payload.Tint, runtimeHost.Volume.Color);
            Assert.AreEqual(payload.Density, runtimeHost.Volume.Density, 0.0001f);
            Assert.IsTrue(runtimeHost.Volume.IsEmitting);

            _target.transform.position = new Vector3(2f, 4f, 6f);
            Assert.AreEqual(_target.transform.position, runtimeHost.Volume.WorldPosition);

            AeroGasSprayBridge.Stop();
            Assert.IsFalse(runtimeHost.Volume.IsEmitting, "Stop requests fade-out rather than immediate removal.");
            Assert.Greater(runtimeHost.Volume.Alpha, 0f, "The emitter is retained to allow its alpha to fade.");
            AeroGasSprayBridge.SetRendererSessionEnabled(false);
            Assert.IsNull(runtimeHost.Volume, "Disabling isolated renderer removes its local emitter.");
        }

        [Test]
        public void Adapter_ProvidesOptInGameplayIndependentInterface()
        {
            AeroLocalizedGasVolumeBridge bridge = _host.AddComponent<AeroLocalizedGasVolumeBridge>();
            IAeroLocalizedGasBridge contract = bridge;
            contract.Configure(_target.transform, 5f, 3f, 0.5f, Color.yellow);
            contract.SetEmitting(false);
            Assert.AreEqual(5f, _volume.Radius, 0.0001f);
            Assert.AreEqual(3f, _volume.Height, 0.0001f);
            Assert.IsFalse(_volume.IsEmitting);
        }
    }
}
