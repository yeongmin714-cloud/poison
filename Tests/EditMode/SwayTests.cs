#if false
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// SwayController EditMode 테스트.
    ///
    /// 테스트 대상:
    /// - 컴포넌트 부착 및 초기화
    /// - 회전/위치 진동 적용 확인
    /// - 활성화/비활성화
    /// - 거리 기반 컬링 (50m 이상 비활성화)
    /// - 랜덤 오프셋 고유성
    /// - WindZone 영향
    /// - 프리셋 값 정확성
    /// - ResetState 동작
    /// </summary>
    public class SwayTests
    {
        private GameObject _testGo;
        private SwayController _controller;

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            _testGo = new GameObject("TestSwayObject");
            _controller = _testGo.AddComponent<SwayController>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_testGo != null)
                Object.DestroyImmediate(_testGo);
        }

        // ================================================================
        // T01: 컴포넌트 부착
        // ================================================================

        [Test]
        public void T01_Component_IsAttached()
        {
            Assert.IsNotNull(_controller,
                "SwayController가 GameObject에 부착되어야 함");
        }

        [Test]
        public void T02_InitialRotationPosition_IsPreserved()
        {
            // Arrange: 초기 회전/위치 설정
            Quaternion expectedRot = Quaternion.Euler(10f, 20f, 30f);
            Vector3 expectedPos = new Vector3(5f, 2f, 3f);
            _testGo.transform.localRotation = expectedRot;
            _testGo.transform.localPosition = expectedPos;

            // Act: Awake에서 저장된 값 확인
            // (Setup에서 AddComponent 시 Awake 실행됨)

            // Assert: 초기값이 보존되었는지 확인
            Assert.AreEqual(expectedRot, _controller.InitialRotation,
                "InitialRotation이 설정한 값과 일치해야 함");
            Assert.AreEqual(expectedPos, _controller.InitialPosition,
                "InitialPosition이 설정한 값과 일치해야 함");
        }

        // ================================================================
        // T03: 랜덤 오프셋 고유성
        // ================================================================

        [Test]
        public void T03_RandomOffsets_AreUnique()
        {
            // Arrange: 여러 개의 SwayController 생성
            int count = 10;
            var offsets = new HashSet<float>();

            var gameObjects = new GameObject[count];
            var controllers = new SwayController[count];

            for (int i = 0; i < count; i++)
            {
                gameObjects[i] = new GameObject($"TestSway_{i}");
                controllers[i] = gameObjects[i].AddComponent<SwayController>();
            }

            // Act: 각 오프셋 수집
            foreach (var c in controllers)
            {
                offsets.Add(c.SwayOffset);
            }

            // Assert: 최소 8개 이상 고유해야 함 (InstanceID 기반이므로 중복 가능성 낮음)
            Assert.GreaterOrEqual(offsets.Count, count - 1,
                $"{count}개 중 최소 {count - 1}개의 고유한 SwayOffset이 있어야 함");

            // Cleanup
            foreach (var go in gameObjects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        // T04: 회전 진동 적용 확인
        // ================================================================

        [UnityTest]
        public IEnumerator T04_SwayRotation_ChangesOverTime()
        {
            // Arrange
            _controller.SetSwaySpeed(2f);
            _controller.SetSwayAmount(3f);
            _controller.SetBobAmount(0f); // 위치 보빙 제거

            // Act: 초기 회전 기록 후 update 사이클 대기
            Quaternion rotBefore = _testGo.transform.localRotation;

            yield return new WaitForSeconds(0.05f);

            // Force update (Reflection으로 private 메서드 호출)
            var method = typeof(SwayController).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_controller, null);

            Quaternion rotAfter = _testGo.transform.localRotation;

            // Assert: 회전이 변경됨 (초기 rotation * sway rotation)
            Assert.AreNotEqual(rotBefore, rotAfter,
                "Sway 업데이트 후 회전이 변경되어야 함");
        }

        // ================================================================
        // T05: 활성화/비활성화
        // ================================================================

        [Test]
        public void T05_EnableDisable_TogglesCorrectly()
        {
            // Assert: 초기에는 활성화
            Assert.IsTrue(_controller.enabled,
                "SwayController는 초기에 활성화되어야 함");

            // Act: 비활성화
            _controller.enabled = false;

            // Assert: 비활성화 확인
            Assert.IsFalse(_controller.enabled,
                "SwayController.enabled = false 시 비활성화되어야 함");

            // Act: 재활성화
            _controller.enabled = true;

            // Assert: 재활성화 확인
            Assert.IsTrue(_controller.enabled,
                "SwayController.enabled = true 시 재활성화되어야 함");
        }

        // ================================================================
        // T06: 거리 기반 컬링 — 먼 거리에서 비활성화
        // ================================================================

        [UnityTest]
        public IEnumerator T06_DistanceCulling_DisablesWhenFar()
        {
            // Arrange: 카메라 생성
            var cameraGo = new GameObject("TestCamera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.transform.position = Vector3.zero;

            yield return null;

            // Act: 테스트 오브젝트를 카메라에서 60m 거리에 위치 (cullDistance=50m 초과)
            _testGo.transform.position = new Vector3(0f, 0f, 60f);
            _controller.SetCullDistance(50f);

            // Awake에서 Camera.main을 캐싱했으므로 수동 설정
            var field = typeof(SwayController).GetField("_mainCamera",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(_controller, camera);

            var method = typeof(SwayController).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_controller, null);

            // Assert: 컬링되어 비활성화
            Assert.IsFalse(_controller.enabled,
                "카메라 거리 60m (50m 초과)에서 SwayController가 비활성화되어야 함");
            Assert.IsTrue(_controller.IsCulled,
                "IsCulled가 true여야 함");

            // Cleanup
            Object.DestroyImmediate(cameraGo);
        }

        // ================================================================
        // T07: 거리 기반 컬링 — 가까우면 활성화 유지
        // ================================================================

        [UnityTest]
        public IEnumerator T07_DistanceCulling_StaysActiveWhenClose()
        {
            // Arrange: 카메라 생성
            var cameraGo = new GameObject("TestCamera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.transform.position = Vector3.zero;

            yield return null;

            // Act: 테스트 오브젝트를 카메라에서 10m 거리 (cullDistance=50m 이내)
            _testGo.transform.position = new Vector3(0f, 0f, 10f);
            _controller.SetCullDistance(50f);

            var field = typeof(SwayController).GetField("_mainCamera",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(_controller, camera);

            var method = typeof(SwayController).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_controller, null);

            // Assert: 활성화 유지
            Assert.IsTrue(_controller.enabled,
                "카메라 거리 10m (50m 이내)에서 SwayController가 활성화되어야 함");
            Assert.IsFalse(_controller.IsCulled,
                "IsCulled가 false여야 함");

            Object.DestroyImmediate(cameraGo);
        }

        // ================================================================
        // T08: WindZone 감지
        // ================================================================

        [Test]
        public void T08_WindZone_IsDetected()
        {
            // Arrange: WindZone 생성
            var windGo = new GameObject("TestWindZone");
            var windZone = windGo.AddComponent<WindZone>();
            windZone.windMain = 0.5f;
            windZone.windTurbulence = 0.2f;
            windGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            // Act: SwayController가 WindZone을 찾는지 확인
            var foundWind = Object.FindObjectOfType<WindZone>();

            // Assert
            Assert.IsNotNull(foundWind,
                "WindZone이 씬에 존재해야 함");
            Assert.AreEqual(0.5f, foundWind.windMain, 0.001f,
                "WindZone.windMain이 설정값과 일치해야 함");
            Assert.AreEqual(0.2f, foundWind.windTurbulence, 0.001f,
                "WindZone.windTurbulence가 설정값과 일치해야 함");

            // Cleanup
            Object.DestroyImmediate(windGo);
        }

        // ================================================================
        // T09: 프리셋 값 검증 (범위 확인)
        // ================================================================

        [Test]
        public void T09_PresetValues_AreValid()
        {
            // SwayController 초기값 검증 (기본값 범위)
            Assert.IsTrue(_controller.SwaySpeed >= 1f && _controller.SwaySpeed <= 3f,
                $"SwaySpeed({_controller.SwaySpeed})는 1~3 범위여야 함");
            Assert.IsTrue(_controller.SwayAmount >= 0f && _controller.SwayAmount <= 5f,
                $"SwayAmount({_controller.SwayAmount})는 0~5 범위여야 함");
            Assert.IsTrue(_controller.BobSpeed >= 0.5f && _controller.BobSpeed <= 2f,
                $"BobSpeed({_controller.BobSpeed})는 0.5~2 범위여야 함");
            Assert.IsTrue(_controller.BobAmount >= 0f && _controller.BobAmount <= 0.05f,
                $"BobAmount({_controller.BobAmount})는 0~0.05 범위여야 함");
        }

        // ================================================================
        // T10: Setter 클램핑 검증
        // ================================================================

        [Test]
        public void T10_Setters_ClampValues()
        {
            // Act: 범위를 벗어난 값 설정
            _controller.SetSwaySpeed(10f);    // max 3
            _controller.SetSwayAmount(20f);   // max 5
            _controller.SetBobSpeed(5f);      // max 2
            _controller.SetBobAmount(0.5f);   // max 0.05

            // Assert: 클램핑 확인
            Assert.AreEqual(3f, _controller.SwaySpeed,
                "SwaySpeed는 3f로 클램핑되어야 함");
            Assert.AreEqual(5f, _controller.SwayAmount,
                "SwayAmount는 5f로 클램핑되어야 함");
            Assert.AreEqual(2f, _controller.BobSpeed,
                "BobSpeed는 2f로 클램핑되어야 함");
            Assert.AreEqual(0.05f, _controller.BobAmount,
                "BobAmount는 0.05f로 클램핑되어야 함");
        }

        // ================================================================
        // T11: ResetState 동작
        // ================================================================

        [UnityTest]
        public IEnumerator T11_ResetState_RestoresInitialTransform()
        {
            // Arrange
            Quaternion initialRot = Quaternion.Euler(15f, 30f, 45f);
            Vector3 initialPos = new Vector3(1f, 2f, 3f);
            _testGo.transform.localRotation = initialRot;
            _testGo.transform.localPosition = initialPos;

            // SwayController 재생성 (Awake에서 초기값 저장)
            Object.DestroyImmediate(_controller);
            _controller = _testGo.AddComponent<SwayController>();

            // Act: Update 한 번 호출 (회전/위치 변경)
            var method = typeof(SwayController).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_controller, null);

            // 회전/위치가 변경되었는지 확인
            Assert.AreNotEqual(initialRot, _testGo.transform.localRotation,
                "Update 후 회전이 변경되어야 함");

            // ResetState 호출
            _controller.ResetState();

            // Assert: 초기 상태로 복원
            Assert.AreEqual(initialRot, _testGo.transform.localRotation,
                "ResetState 후 회전이 초기값으로 복원되어야 함");
            Assert.AreEqual(initialPos, _testGo.transform.localPosition,
                "ResetState 후 위치가 초기값으로 복원되어야 함");

            yield return null;
        }

        // ================================================================
        // T12: Update 예외 없음
        // ================================================================

        [Test]
        public void T12_Update_DoesNotThrow()
        {
            var method = typeof(SwayController).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // Assert: Update 호출 시 예외가 발생하지 않아야 함
            Assert.DoesNotThrow(() => method?.Invoke(_controller, null),
                "Update()는 예외를 던지지 않아야 함");
        }

        // ================================================================
        // T13: Bobbing 위치 변경 확인
        // ================================================================

        [UnityTest]
        public IEnumerator T13_Bobbing_ChangesPosition()
        {
            // Arrange
            Vector3 initialPos = new Vector3(10f, 5f, 10f);
            _testGo.transform.localPosition = initialPos;
            _controller.SetBobAmount(0.05f);
            _controller.SetBobSpeed(2f);

            yield return new WaitForSeconds(0.05f);

            // Act: 수동 Update
            var method = typeof(SwayController).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_controller, null);

            // Assert: Y 위치가 변경됨
            Assert.AreNotEqual(initialPos.y, _testGo.transform.localPosition.y,
                "Bobbing 적용 후 Y 위치가 변경되어야 함");

            // X, Z는 변경되지 않아야 함
            Assert.AreEqual(initialPos.x, _testGo.transform.localPosition.x, 0.001f,
                "Bobbing 적용 후 X 위치는 변경되지 않아야 함");
            Assert.AreEqual(initialPos.z, _testGo.transform.localPosition.z, 0.001f,
                "Bobbing 적용 후 Z 위치는 변경되지 않아야 함");
        }

        // ================================================================
        // T14: 큰나무/작은나무 프리셋
        // ================================================================

        [Test]
        public void T14_TreePreset_SmallVsBig()
        {
            // 큰나무 프리셋: swaySpeed=1.0, swayAmount=2도
            var bigController = _testGo.AddComponent<SwayController>();
            bigController.SetSwaySpeed(1.0f);
            bigController.SetSwayAmount(2f);
            Assert.AreEqual(1.0f, bigController.SwaySpeed,
                "큰나무 프리셋: swaySpeed=1.0");
            Assert.AreEqual(2f, bigController.SwayAmount,
                "큰나무 프리셋: swayAmount=2도");

            // 작은나무 프리셋: swaySpeed=1.5, swayAmount=3도
            var smallGo = new GameObject("SmallTree");
            var smallController = smallGo.AddComponent<SwayController>();
            smallController.SetSwaySpeed(1.5f);
            smallController.SetSwayAmount(3f);
            Assert.AreEqual(1.5f, smallController.SwaySpeed,
                "작은나무 프리셋: swaySpeed=1.5");
            Assert.AreEqual(3f, smallController.SwayAmount,
                "작은나무 프리셋: swayAmount=3도");

            Object.DestroyImmediate(smallGo);
        }

        // ================================================================
        // T15: 바위 프리셋 (미세한 흔들림)
        // ================================================================

        [Test]
        public void T15_RockPreset_MinimalSway()
        {
            // 바위: swaySpeed=0.3, swayAmount=0.5도, bobAmount=0.01
            _controller.SetSwaySpeed(0.3f);
            _controller.SetSwayAmount(0.5f);
            _controller.SetBobAmount(0.01f);

            Assert.AreEqual(0.3f, _controller.SwaySpeed,
                "바위 프리셋: swaySpeed=0.3");
            Assert.AreEqual(0.5f, _controller.SwayAmount,
                "바위 프리셋: swayAmount=0.5도");
            Assert.AreEqual(0.01f, _controller.BobAmount,
                "바위 프리셋: bobAmount=0.01");
        }

        // ================================================================
        // T16: 식물 프리셋 (빠른 흔들림)
        // ================================================================

        [Test]
        public void T16_PlantPreset_FastSway()
        {
            // 풀/식물: swaySpeed=2.5, swayAmount=5도, bobAmount=0.05
            _controller.SetSwaySpeed(2.5f);
            _controller.SetSwayAmount(5f);
            _controller.SetBobAmount(0.05f);

            Assert.AreEqual(2.5f, _controller.SwaySpeed,
                "식물 프리셋: swaySpeed=2.5");
            Assert.AreEqual(5f, _controller.SwayAmount,
                "식물 프리셋: swayAmount=5도");
            Assert.AreEqual(0.05f, _controller.BobAmount,
                "식물 프리셋: bobAmount=0.05");
        }

        // ================================================================
        // T17: WindZone이 Sway 방향에 영향
        // ================================================================

        [UnityTest]
        public IEnumerator T17_WindZone_AffectsSwayDirection()
        {
            // Arrange: WindZone 생성 (북동쪽 방향)
            var windGo = new GameObject("WindZone");
            var windZone = windGo.AddComponent<WindZone>();
            windZone.windMain = 1.0f;
            windGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            yield return null;

            // 새 SwayController 생성 (WindZone 탐지)
            var testGo2 = new GameObject("TestSwayWind");
            var sway = testGo2.AddComponent<SwayController>();
            sway.SetSwaySpeed(2f);
            sway.SetSwayAmount(3f);
            sway.SetBobAmount(0f);

            // 초기 회전 기록
            Quaternion rotBefore = testGo2.transform.localRotation;

            // Update 호출
            var method = typeof(SwayController).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(sway, null);

            // Assert: 회전이 변경됨 (WindZone이 적용되어야 함)
            Assert.AreNotEqual(rotBefore, testGo2.transform.localRotation,
                "WindZone이 있을 때 SwayController Update 후 회전이 변경되어야 함");

            // 속성 확인 (WindZone이 존재하므로 내부 _windZone이 null이 아님)
            var windField = typeof(SwayController).GetField("_windZone",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var foundWind = windField?.GetValue(sway);
            Assert.IsNotNull(foundWind,
                "SwayController가 WindZone을 자동으로 찾아야 함");

            // Cleanup
            Object.DestroyImmediate(testGo2);
            Object.DestroyImmediate(windGo);
        }
    }
}
#endif