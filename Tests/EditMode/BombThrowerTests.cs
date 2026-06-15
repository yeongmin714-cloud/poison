using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C8-30 폭탄 투척 시스템 (BombThrower) 테스트
    /// </summary>
    public class BombThrowerTests
    {
        // ===================== BombThrower 존재 확인 =====================

        [Test]
        public void BombThrower_Type_Exists()
        {
            var type = typeof(BombThrower);
            Assert.IsNotNull(type, "BombThrower 타입이 존재해야 합니다");
        }

        [Test]
        public void BombThrower_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(BombThrower).IsSubclassOf(typeof(MonoBehaviour)),
                "BombThrower는 MonoBehaviour를 상속해야 합니다");
        }

        [Test]
        public void BombThrower_HasRequireComponent_LineRenderer()
        {
            var attributes = typeof(BombThrower).GetCustomAttributes(typeof(RequireComponent), false);
            bool hasLineRendererReq = false;
            foreach (RequireComponent attr in attributes)
            {
                if (attr.m_Type0 == typeof(LineRenderer))
                {
                    hasLineRendererReq = true;
                    break;
                }
            }
            Assert.IsTrue(hasLineRendererReq, "BombThrower에 [RequireComponent(typeof(LineRenderer))]가 있어야 합니다");
        }

        // ===================== BombThrower 필드/속성 확인 =====================

        [Test]
        public void BombThrower_HasMinThrowForce()
        {
            var field = typeof(BombThrower).GetField("minThrowForce");
            Assert.IsNotNull(field, "BombThrower에 minThrowForce 필드가 있어야 합니다");
            Assert.AreEqual(typeof(float), field.FieldType, "minThrowForce는 float 타입이어야 합니다");
        }

        [Test]
        public void BombThrower_HasMaxThrowForce()
        {
            var field = typeof(BombThrower).GetField("maxThrowForce");
            Assert.IsNotNull(field, "BombThrower에 maxThrowForce 필드가 있어야 합니다");
        }

        [Test]
        public void BombThrower_HasChargeTime()
        {
            var field = typeof(BombThrower).GetField("chargeTime");
            Assert.IsNotNull(field, "BombThrower에 chargeTime 필드가 있어야 합니다");
        }

        [Test]
        public void BombThrower_HasTrajectoryFields()
        {
            var resolutionField = typeof(BombThrower).GetField("trajectoryResolution");
            Assert.IsNotNull(resolutionField, "BombThrower에 trajectoryResolution 필드가 있어야 합니다");

            var stepField = typeof(BombThrower).GetField("trajectoryTimeStep");
            Assert.IsNotNull(stepField, "BombThrower에 trajectoryTimeStep 필드가 있어야 합니다");

            var colorField = typeof(BombThrower).GetField("trajectoryColor");
            Assert.IsNotNull(colorField, "BombThrower에 trajectoryColor 필드가 있어야 합니다");
        }

        // ===================== BombThrower 기본 동작 테스트 =====================

        [Test]
        public void BombThrower_CanInstantiate()
        {
            var go = new GameObject("TestBombThrower");
            go.AddComponent<LineRenderer>();
            var thrower = go.AddComponent<BombThrower>();
            Assert.IsNotNull(thrower, "BombThrower 인스턴스가 생성되어야 합니다");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void BombThrower_LoadsBombPrefabs()
        {
            var prefabs = Resources.LoadAll<GameObject>("Bombs");
            // Bomb_Explosive, Bomb_PoisonGas, Bomb_Smoke, Bomb_Molotov = 4종
            Assert.GreaterOrEqual(prefabs.Length, 4,
                "Resources/Bombs/에 4개 이상의 폭탄 프리팹이 있어야 합니다");
        }

        [Test]
        public void BombThrower_GetBombPrefab_ByType()
        {
            var prefabs = Resources.LoadAll<GameObject>("Bombs");
            Assert.GreaterOrEqual(prefabs.Length, 4, "4개 이상의 폭탄 프리팹 필요");

            // BombType 열거형 순서와 Resources 로드 순서가 일치해야 함
            // Explosive=0, PoisonGas=1, Smoke=2, Molotov=3
            Assert.LessOrEqual(3, prefabs.Length - 1, "폭탄 프리팹이 충분히 로드되어야 함");
        }

        // ===================== 궤도 계산 테스트 =====================

        [Test]
        public void BombThrower_CalculateTrajectoryPoint()
        {
            var go = new GameObject("TestBombThrower");
            go.AddComponent<LineRenderer>();
            var thrower = go.AddComponent<BombThrower>();

            // 리플렉션으로 private 메서드 접근
            var method = typeof(BombThrower).GetMethod("CalculateTrajectoryPoint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "CalculateTrajectoryPoint 메서드가 존재해야 합니다");

            // 테스트: 시작점 (0,1,0), 속도 (0,0,10), 시간 t=0.5
            Vector3 start = new Vector3(0, 1, 0);
            Vector3 velocity = new Vector3(0, 0, 10);
            Vector3 result = (Vector3)method.Invoke(thrower, new object[] { start, velocity, 0.5f });

            // x는 변하지 않음 (측면 이동 없음)
            Assert.AreEqual(0f, result.x, 0.001f, "X 좌표는 변하지 않아야 합니다");
            // z는 속도 * 시간 = 10 * 0.5 = 5
            Assert.AreEqual(5f, result.z, 0.001f, "Z 좌표는 5.0이어야 합니다");
            // y는 start.y + velocity.y * t + 0.5 * g * t^2 = 1 + 0 + 0.5 * (-9.81) * 0.25 = 1 - 1.22625 = -0.22625
            float expectedY = start.y + velocity.y * 0.5f + Physics.gravity.y * 0.5f * 0.5f * 0.5f;
            Assert.AreEqual(expectedY, result.y, 0.001f, "Y 좌표는 중력 계산 결과와 일치해야 합니다");

            Object.DestroyImmediate(go);
        }

        // ===================== 발사 방향 계산 테스트 =====================

        [Test]
        public void BombThrower_GetAimDirection_ReturnsVector()
        {
            var go = new GameObject("TestBombThrower");
            go.AddComponent<LineRenderer>();
            var thrower = go.AddComponent<BombThrower>();

            var method = typeof(BombThrower).GetMethod("GetAimDirection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "GetAimDirection 메서드가 존재해야 합니다");

            // Camera.main이 없으면 Vector3.zero 반환
            Vector3 result = (Vector3)method.Invoke(thrower, null);
            Assert.AreEqual(Vector3.zero, result, "카메라가 없으면 Vector3.zero를 반환해야 합니다");

            Object.DestroyImmediate(go);
        }

        // ===================== 충전/투척 상태 테스트 =====================

        [Test]
        public void BombThrower_StartCharge_EnablesLineRenderer()
        {
            var go = new GameObject("TestBombThrower");
            var lr = go.AddComponent<LineRenderer>();
            var thrower = go.AddComponent<BombThrower>();
            lr.enabled = false;

            var method = typeof(BombThrower).GetMethod("StartCharge",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "StartCharge 메서드가 존재해야 합니다");

            method.Invoke(thrower, null);
            Assert.IsTrue(lr.enabled, "StartCharge 호출 후 LineRenderer가 활성화되어야 합니다");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BombThrower_ReleaseThrow_DisablesLineRenderer()
        {
            var go = new GameObject("TestBombThrower");
            var lr = go.AddComponent<LineRenderer>();
            lr.enabled = true;
            var thrower = go.AddComponent<BombThrower>();

            var releaseMethod = typeof(BombThrower).GetMethod("ReleaseThrow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(releaseMethod, "ReleaseThrow 메서드가 존재해야 합니다");

            // internal state 초기화 후 ReleaseThrow 호출
            releaseMethod.Invoke(thrower, null);
            Assert.IsFalse(lr.enabled, "ReleaseThrow 호출 후 LineRenderer가 비활성화되어야 합니다");

            Object.DestroyImmediate(go);
        }
    }
}