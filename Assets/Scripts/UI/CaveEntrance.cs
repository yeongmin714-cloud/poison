using ProjectName.Systems;
using ProjectName.Core.Utils;
using UnityEngine;
using System.Collections;

namespace ProjectName.UI
{
    /// <summary>
    /// C22-07: 동굴 입구 시스템.
    /// Procedural arch-shaped mesh on cliff faces using primitives.
    /// E-key interaction trigger zone that saves player position and
    /// calls IndoorSceneTransition to load cave interior.
    /// </summary>
    public class CaveEntrance : MonoBehaviour
    {
        [Header("Cave Settings")]
        [SerializeField] private float _interactionRadius = 2.5f;
        [SerializeField] private bool _isActive = true;

        [Header("Visual")]
        [SerializeField] private GameObject _entranceMesh;
        [SerializeField] private Light _entranceLight;

        [Header("Arch Construction")]
        [SerializeField] private float _archWidth = 3f;
        [SerializeField] private float _archHeight = 3.5f;
        [SerializeField] private float _archThickness = 0.3f;
        [SerializeField] private Color _archColor = new Color(0.35f, 0.30f, 0.25f);

        // Player position save for return
        private static Vector3 _savedPlayerPosition;
        private static bool _hasSavedPosition = false;

        // The constructed arch parent object
        private GameObject _archRoot;

        // Interaction prompt display
        private bool _playerInRange = false;

        /// <summary>Saved player position for return from cave.</summary>
        public static Vector3 SavedPlayerPosition => _savedPlayerPosition;
        /// <summary>Whether a position has been saved.</summary>
        public static bool HasSavedPosition => _hasSavedPosition;

        /// <summary>Clear saved position (called on exit).</summary>
        public static void ClearSavedPosition()
        {
            _hasSavedPosition = false;
        }

        private void Awake()
        {
            // If no entrance mesh is assigned, build one procedurally
            if (_entranceMesh == null)
            {
                BuildArchMesh();
            }

            // Create interaction trigger (sphere collider)
            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = _interactionRadius;

            // Set tag for interaction detection
            gameObject.tag = "Interactable";
        }

        /// <summary>
        /// Builds a procedural arch-shaped mesh using primitive cubes.
        /// Creates an arch with two pillars and a curved top.
        /// </summary>
        private void BuildArchMesh()
        {
            _archRoot = new GameObject($"{gameObject.name}_Arch");
            _archRoot.transform.SetParent(transform, false);
            _archRoot.transform.localPosition = Vector3.zero;

            // Left pillar
            var leftPillar = CreateMeshCube("Arch_LeftPillar");
            leftPillar.transform.SetParent(_archRoot.transform);
            leftPillar.transform.localPosition = new Vector3(-_archWidth / 2f, _archHeight / 2f, 0f);
            leftPillar.transform.localScale = new Vector3(_archThickness, _archHeight, _archThickness);
            SetMeshMaterial(leftPillar);

            // Right pillar
            var rightPillar = CreateMeshCube("Arch_RightPillar");
            rightPillar.transform.SetParent(_archRoot.transform);
            rightPillar.transform.localPosition = new Vector3(_archWidth / 2f, _archHeight / 2f, 0f);
            rightPillar.transform.localScale = new Vector3(_archThickness, _archHeight, _archThickness);
            SetMeshMaterial(rightPillar);

            // Top beam (horizontal)
            var topBeam = CreateMeshCube("Arch_TopBeam");
            topBeam.transform.SetParent(_archRoot.transform);
            topBeam.transform.localPosition = new Vector3(0f, _archHeight, 0f);
            topBeam.transform.localScale = new Vector3(_archWidth + _archThickness, _archThickness, _archThickness);
            SetMeshMaterial(topBeam);

            // Inner top arch detail (small curved segments using smaller cubes)
            int segments = 5;
            for (int i = 0; i < segments; i++)
            {
                float t = (float)(i + 1) / (segments + 1);
                float angle = t * Mathf.PI;
                float x = Mathf.Cos(angle) * _archWidth * 0.4f;
                float y = Mathf.Sin(angle) * _archHeight * 0.25f + _archHeight;

                var archSegment = CreateMeshCube($"Arch_Segment_{i}");
                archSegment.transform.SetParent(_archRoot.transform);
                archSegment.transform.localPosition = new Vector3(x, y, 0f);
                archSegment.transform.localScale = new Vector3(_archThickness * 0.8f, _archThickness * 0.8f, _archThickness * 1.5f);
                SetMeshMaterial(archSegment);
            }

            // Floor step (slightly raised entrance)
            var floorStep = CreateMeshCube("Arch_FloorStep");
            floorStep.transform.SetParent(_archRoot.transform);
            floorStep.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            floorStep.transform.localScale = new Vector3(_archWidth * 0.8f, 0.2f, _archThickness * 2f);
            SetMeshMaterial(floorStep, new Color(0.40f, 0.35f, 0.30f));

            // Set the root as the entrance mesh reference
            _entranceMesh = _archRoot;

            // Add entrance light if not set
            if (_entranceLight == null)
            {
                GameObject lightGo = new GameObject("EntranceLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = new Vector3(0f, _archHeight * 0.6f, 0f);

                _entranceLight = lightGo.AddComponent<Light>();
                _entranceLight.type = LightType.Point;
                _entranceLight.color = new Color(0.8f, 0.6f, 0.3f); // warm torch light
                _entranceLight.range = 8f;
                _entranceLight.intensity = 0.8f;
                _entranceLight.shadows = LightShadows.Soft;
            }
        }

        private void SetMeshMaterial(GameObject obj)
        {
            SetMeshMaterial(obj, _archColor);
        }

        /// <summary>
        /// Creates a primitive cube without the auto-generated Collider.
        /// </summary>
        private static GameObject CreateMeshCube(string name)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            // Remove auto-generated MeshCollider — we only need visuals
            Object.DestroyImmediate(obj.GetComponent<MeshCollider>());
            return obj;
        }

        private void SetMeshMaterial(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = MaterialHelper.CreateLitMaterial(color, obj.name + "_Mat");
            }
        }

        /// <summary>
        /// 동굴 입구 활성화/비활성화
        /// </summary>
        public void SetActive(bool active)
        {
            _isActive = active;
            if (_entranceMesh != null)
                _entranceMesh.SetActive(active);
            if (_entranceLight != null)
                _entranceLight.enabled = active;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive) return;
            if (other.CompareTag("Player"))
            {
                _playerInRange = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInRange = false;
            }
        }

        private void Update()
        {
            if (!_isActive || !_playerInRange) return;

            // Check for E key interaction (using Input System or legacy)
            if (UnityEngine.Input.GetKeyDown(KeyCode.E) ||
                (UnityEngine.InputSystem.Keyboard.current != null &&
                 UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame))
            {
                Interact();
            }
        }

        /// <summary>
        /// Interaction: save player position, trigger fade, load cave interior.
        /// </summary>
        public void Interact()
        {
            if (!_isActive) return;



            // Save player position for return
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _savedPlayerPosition = player.transform.position;
                _hasSavedPosition = true;
            }

            // Trigger fade transition and load cave interior scene
            // We use FadeManager for the fade effect, then IndoorSceneTransition
            if (FadeManager.Instance != null)
            {
                FadeManager.Instance.FadeOut(0.5f);
            }

            // After a brief delay, enter the cave
            StartCoroutine(EnterCaveCoroutine());
        }

        private IEnumerator EnterCaveCoroutine()
        {
            yield return new WaitForSeconds(0.6f);

            // Pass "cave" as buildingType to IndoorSceneTransition
            IndoorSceneTransition.EnterBuilding("cave", "default");

            if (FadeManager.Instance != null)
            {
                yield return FadeManager.Instance.FadeIn(0.3f);
            }
        }

        /// <summary>
        /// Called when player exits the cave to return to saved position.
        /// </summary>
        public static void ReturnToSavedPosition()
        {
            if (!_hasSavedPosition) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.GetComponent<CharacterController>() != null)
            {
                // Disable CharacterController for teleport, then re-enable
                var cc = player.GetComponent<CharacterController>();
                cc.enabled = false;
                player.transform.position = _savedPlayerPosition;
                cc.enabled = true;
            }

            _hasSavedPosition = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);
        }
    }
}