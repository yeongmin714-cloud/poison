using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_07_GasBomb 씬 전용: 가스 분사기 + 폭탄 시스템 통합 테스트.
    /// Player, GasSprayerController, BombThrower, 테스트 더미 적 배치.
    /// </summary>
    public class TestGasBombSetup : MonoBehaviour
    {
        [Header("Player Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 10f;
        [SerializeField] private float _jumpHeight = 2f;

        [Header("Camera Settings")]
        [SerializeField] private float _orbitRadius = 30f;
        [SerializeField] private float _defaultPitch = 50f;

        [Header("Gas Bomb Test Settings")]
        [SerializeField] private bool _spawnTestDummies = true;
        [SerializeField] private int _dummyCount = 3;
        [SerializeField] private float _dummySpreadRadius = 5f;
        [SerializeField] private bool _equipGasSprayer = true;
        [SerializeField] private GasSprayerGrade _testSprayerGrade = GasSprayerGrade.Wood;
        [SerializeField] private bool _addBombThrower = true;

        private void Awake()
        {
            SetupPlayer();
            SetupCamera();
            SetupGround();
            SetupLight();
            EnsureEventSystem();

            if (_spawnTestDummies)
                SpawnTestDummies();

            if (_equipGasSprayer)
                EquipGasSprayerForTest();

            if (_addBombThrower)
                AddBombThrower();

            // SpecialEffectsController 자동 생성
            EnsureSpecialEffectsController();

            Debug.Log("[TestGasBombSetup] ✅ Test_07_GasBomb 설정 완료!");
        }

        private void SetupPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }

            // CharacterController
            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
            }

            // PlayerMovement
            var pmType = typeof(ProjectName.Systems.PlayerMovement);
            if (player.GetComponent(pmType) == null)
            {
                player.AddComponent(pmType);
            }

            // PlayerInput (Input System 활성화용)
            if (player.GetComponent<PlayerInput>() == null)
            {
                var pi = player.AddComponent<PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
            }

            // PlayerPlaceholder
            if (player.GetComponent<PlayerPlaceholder>() == null)
            {
                player.AddComponent<PlayerPlaceholder>();
            }

            // GasSprayerController (Player에 부착)
            if (player.GetComponent<GasSprayerController>() == null)
            {
                player.AddComponent<GasSprayerController>();
            }

            // Damageable (플레이어도 데미지 받을 수 있게)
            if (player.GetComponent<Damageable>() == null)
            {
                var dmg = player.AddComponent<Damageable>();
                // HP 설정은 인스펙터에서 가능하도록 SerializeField 사용
            }

            player.transform.position = Vector3.zero;
            Debug.Log("[TestGasBombSetup] ✅ Player 설정 완료 (GasSprayerController + Damageable 포함)");
        }

        private void SetupCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
            }

            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null)
                cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;

            var tdcType = typeof(ProjectName.Systems.TopDownCameraController);
            if (camGO.GetComponent(tdcType) == null)
            {
                camGO.AddComponent(tdcType);
            }

            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();

            Debug.Log("[TestGasBombSetup] ✅ 카메라 설정 완료");
        }

        private void SetupGround()
        {
            if (GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = new Vector3(0, -0.5f, 0);
                ground.transform.localScale = Vector3.one * 50f;

                var renderer = ground.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                    mat.SetFloat("_Smoothness", 0f);
                    renderer.material = mat;
                }

                Debug.Log("[TestGasBombSetup] ✅ Ground 생성");
            }
        }

        private void SetupLight()
        {
            if (FindAnyObjectByType<Light>() == null)
            {
                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                Debug.Log("[TestGasBombSetup] ✅ Directional Light 생성");
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[TestGasBombSetup] ✅ EventSystem 생성");
            }
        }

        private void EnsureSpecialEffectsController()
        {
            if (SpecialEffectsController.Instance == null)
            {
                // Instance getter가 자동 생성하므로 호출만 하면 됨
                var instance = SpecialEffectsController.Instance;
                Debug.Log("[TestGasBombSetup] ✅ SpecialEffectsController 자동 생성됨");
            }
        }

        /// <summary>
        /// 테스트용 더미 적을 생성합니다. Damageable 컴포넌트 포함.
        /// </summary>
        private void SpawnTestDummies()
        {
            for (int i = 0; i < _dummyCount; i++)
            {
                float angle = (i / (float)_dummyCount) * 360f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * _dummySpreadRadius,
                    0f,
                    Mathf.Sin(angle) * _dummySpreadRadius
                );

                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = $"TestDummy_{i}";
                dummy.transform.position = pos;
                dummy.transform.localScale = Vector3.one * 0.8f;

                // Capsule 색상 (빨간색 계열)
                var renderer = dummy.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(1f, 0.2f, 0.2f, 1f);
                    renderer.material = mat;
                }

                // Capsule Collider 기본 있음 (PrimitiveType.Capsule)
                // Rigidbody 추가 (넉백 효과 확인용)
                var rb = dummy.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.useGravity = true;

                // Damageable 컴포넌트 추가
                var dmg = dummy.AddComponent<Damageable>();

                // Tag 설정 (없으면 Untagged, Enemy 태그는 에디터에서 별도 설정)
                // 씬에서 적절한 태그 설정 필요

                Debug.Log($"[TestGasBombSetup] ✅ TestDummy_{i} 생성: 위치 {pos}");
            }
        }

        /// <summary>
        /// 가스 분사기를 장착하고 물약을 장전합니다.
        /// </summary>
        private void EquipGasSprayerForTest()
        {
            var controller = FindAnyObjectByType<GasSprayerController>();
            if (controller == null)
            {
                Debug.LogWarning("[TestGasBombSetup] GasSprayerController를 찾을 수 없습니다.");
                return;
            }

            // 직접 장착 (Equip 메서드 사용)
            controller.Equip(_testSprayerGrade);

            // 테스트용 물약 장전
            controller.LoadPotion("potion_poison_test", 5);

            Debug.Log($"[TestGasBombSetup] ✅ 가스 분사기 장착됨: {_testSprayerGrade} + 물약 5개");
        }

        /// <summary>
        /// Player에 BombThrower 컴포넌트를 추가합니다.
        /// </summary>
        private void AddBombThrower()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[TestGasBombSetup] Player를 찾을 수 없어 BombThrower를 추가할 수 없습니다.");
                return;
            }

            if (player.GetComponent<BombThrower>() == null)
            {
                player.AddComponent<BombThrower>();
                Debug.Log("[TestGasBombSetup] ✅ BombThrower 추가됨 (마우스 가운데 버튼으로 폭탄 투척)");
            }
        }

        // ===== 편의 메서드: 테스트 런타임 제어 =====

        /// <summary>
        /// 가스 분사 시작 (테스트/디버그용)
        /// </summary>
        public void StartGasSpray()
        {
            var controller = FindAnyObjectByType<GasSprayerController>();
            if (controller != null)
                controller.StartSpray();
        }

        /// <summary>
        /// 가스 분사 중단 (테스트/디버그용)
        /// </summary>
        public void StopGasSpray()
        {
            var controller = FindAnyObjectByType<GasSprayerController>();
            if (controller != null)
                controller.StopSpray();
        }

        /// <summary>
        /// 모든 테스트 더미의 HP를 출력 (디버그용)
        /// </summary>
        public void LogDummyHP()
        {
            var dummies = FindObjectsByType<Damageable>();
            foreach (var d in dummies)
            {
                Debug.Log($"[TestGasBombSetup] {d.name}: HP {d.CurrentHP}/{d.MaxHP}");
            }
        }

        private void Update()
        {
            // 키보드 단축키: G = 가스 분사 시작/중단 토글
            if (Keyboard.current != null)
            {
                if (Keyboard.current.gKey.wasPressedThisFrame)
                {
                    var controller = FindAnyObjectByType<GasSprayerController>();
                    if (controller != null)
                    {
                        if (controller.IsSpraying)
                            controller.StopSpray();
                        else
                            controller.StartSpray();
                    }
                }

                // H = 모든 더미 HP 출력
                if (Keyboard.current.hKey.wasPressedThisFrame)
                {
                    LogDummyHP();
                }
            }
        }
    }
}