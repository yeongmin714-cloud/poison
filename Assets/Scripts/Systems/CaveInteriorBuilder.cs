using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 29-03 / C22-08: 동굴 실내 생성기.
    /// 암석 텍스처 바닥/벽 + 어두운 조명 + 깜빡이는 횃불 조명 + 보석 상자 1~3개 랜덤 배치.
    /// IndoorSceneTransition과 연동됩니다.
    /// </summary>
    public static class CaveInteriorBuilder
    {
        private const string ROOM_NAME = "CaveRoom";
        private const float WALL_THICKNESS = 0.3f;

        /// <summary>동굴 내부 생성</summary>
        public static GameObject BuildCaveInterior(string territoryId = "default", int tier = 1)
        {
            tier = InteriorRandomizer.ClampTier(tier);
            var size = InteriorRandomizer.GetRoomSize(tier);
            float width = size.width + 2f;
            float height = size.height;
            float depth = size.depth + 2f;

            var rng = InteriorRandomizer.CreateRandom(territoryId + "_cave");

            GameObject room = new GameObject(ROOM_NAME);
            room.transform.position = Vector3.zero;

            CreateFloor(room, width, depth);
            CreateWalls(room, width, height, depth);
            CreateCeiling(room, width, depth);

            int chestCount = rng.Next(1, 4);
            for (int i = 0; i < chestCount; i++)
            {
                float cx = (float)(rng.NextDouble() * (width * 0.6f) - width * 0.3f);
                float cz = (float)(rng.NextDouble() * (depth * 0.6f) - depth * 0.3f);
                CreateGemChest(room, cx, cz, rng, i);
            }

            // Dark ambient lighting
            IndoorLighting.SetupIndoorLighting(room, new Color(0.02f, 0.02f, 0.06f), 0.3f, true);
            CreateAmbientLight(room, width, depth);

            // C22-08: Flickering torch lights on walls
            CreateTorchLights(room, width, depth, height);

            return room;
        }

        private static void CreateFloor(GameObject room, float width, float depth)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "CaveFloor";
            floor.transform.SetParent(room.transform);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            floor.transform.localScale = new Vector3(width, 0.1f, depth);
            var renderer = floor.GetComponent<MeshRenderer>();
            renderer.material = MaterialHelper.CreateLitMaterial(new Color(0.25f, 0.22f, 0.20f), "CaveFloorMat");
        }

        private static void CreateWalls(GameObject room, float width, float height, float depth)
        {
            CreateWallPiece(room, "CaveWall_Front", new Vector3(0, height / 2f, -depth / 2f), new Vector3(width, height, WALL_THICKNESS));
            CreateWallPiece(room, "CaveWall_Back", new Vector3(0, height / 2f, depth / 2f), new Vector3(width, height, WALL_THICKNESS));
            CreateWallPiece(room, "CaveWall_Left", new Vector3(-width / 2f, height / 2f, 0), new Vector3(WALL_THICKNESS, height, depth));
            CreateWallPiece(room, "CaveWall_Right", new Vector3(width / 2f, height / 2f, 0), new Vector3(WALL_THICKNESS, height, depth));
        }

        private static GameObject CreateWallPiece(GameObject room, string name, Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(room.transform);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;
            var renderer = wall.GetComponent<MeshRenderer>();
            renderer.material = MaterialHelper.CreateLitMaterial(new Color(0.30f, 0.27f, 0.25f), "CaveWallMat");
            return wall;
        }

        private static void CreateCeiling(GameObject room, float width, float depth)
        {
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "CaveCeiling";
            ceiling.transform.SetParent(room.transform);
            ceiling.transform.localPosition = new Vector3(0, 3f, 0);
            ceiling.transform.localScale = new Vector3(width, 0.15f, depth);
            var renderer = ceiling.GetComponent<MeshRenderer>();
            renderer.material = MaterialHelper.CreateLitMaterial(new Color(0.20f, 0.18f, 0.16f), "CaveCeilingMat");
        }

        private static void CreateGemChest(GameObject room, float x, float z, System.Random rng, int index)
        {
            var chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = $"GemChest_{index}";
            chest.transform.SetParent(room.transform);
            chest.transform.localPosition = new Vector3(x, 0.3f, z);
            chest.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            var gemTypes = System.Enum.GetValues(typeof(GemType));
            var gemType = (GemType)gemTypes.GetValue(rng.Next(gemTypes.Length));

            var data = GemData.GetGemData(gemType);
            var renderer = chest.GetComponent<MeshRenderer>();
            renderer.material = MaterialHelper.CreateLitMaterial(data.color, $"GemChest_{index}_Mat");

            var gemChest = chest.AddComponent<GemChest>();
            gemChest.GemType = gemType;
        }

        private static void CreateAmbientLight(GameObject room, float width, float depth)
        {
            GameObject lightGo = new GameObject("CaveAmbientLight");
            lightGo.transform.SetParent(room.transform);
            lightGo.transform.localPosition = new Vector3(0, 2f, 0);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.3f, 0.4f, 0.8f);
            light.range = width * 0.8f;
            light.intensity = 0.3f;
            light.shadows = LightShadows.Soft;
        }

        // ================================================================
        //  C22-08: Flickering Torch Lights
        // ================================================================

        /// <summary>
        /// Creates flickering torch point lights on the cave walls.
        /// Each torch has a TorchFlicker component for animated light intensity.
        /// </summary>
        private static void CreateTorchLights(GameObject room, float width, float depth, float height)
        {
            // Place torches at 4 wall positions
            Vector3[] torchPositions = new Vector3[]
            {
                new Vector3(0f, height * 0.6f, -depth / 2f + 0.5f),  // Front wall
                new Vector3(0f, height * 0.6f, depth / 2f - 0.5f),   // Back wall
                new Vector3(-width / 2f + 0.5f, height * 0.6f, 0f),  // Left wall
                new Vector3(width / 2f - 0.5f, height * 0.6f, 0f),   // Right wall
            };

            Color torchColor = new Color(1.0f, 0.6f, 0.2f); // warm orange
            float torchRange = 6f;
            float baseIntensity = 1.2f;

            for (int i = 0; i < torchPositions.Length; i++)
            {
                GameObject torchGo = new GameObject($"TorchLight_{i}");
                torchGo.transform.SetParent(room.transform);
                torchGo.transform.localPosition = torchPositions[i];

                // Visual torch model (small cylinder + cube base)
                CreateTorchModel(torchGo);

                // Point light
                var light = torchGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = torchColor;
                light.range = torchRange;
                light.intensity = baseIntensity;
                light.shadows = LightShadows.Soft;

                // Flicker component
                torchGo.AddComponent<TorchFlicker>();
            }
        }

        /// <summary>
        /// Creates a simple torch model (cylinder + small cube base)
        /// </summary>
        private static void CreateTorchModel(GameObject parent)
        {
            // Torch handle (cylinder)
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "TorchHandle";
            handle.transform.SetParent(parent.transform);
            handle.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            handle.transform.localScale = new Vector3(0.08f, 0.3f, 0.08f);
            Object.DestroyImmediate(handle.GetComponent<CapsuleCollider>());
            var handleRenderer = handle.GetComponent<MeshRenderer>();
            handleRenderer.material = MaterialHelper.CreateLitMaterial(
                new Color(0.35f, 0.25f, 0.15f), "TorchHandleMat");

            // Torch head (small cube)
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "TorchHead";
            head.transform.SetParent(parent.transform);
            head.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            head.transform.localScale = new Vector3(0.15f, 0.1f, 0.15f);
            Object.DestroyImmediate(head.GetComponent<BoxCollider>());
            var headRenderer = head.GetComponent<MeshRenderer>();
            headRenderer.material = MaterialHelper.CreateLitMaterial(
                new Color(0.8f, 0.4f, 0.1f), "TorchHeadMat");
        }
    }

    /// <summary>
    /// C22-08: Flickering torch light animation component.
    /// Randomly varies light intensity and range to simulate torch flicker.
    /// </summary>
    public class TorchFlicker : MonoBehaviour
    {
        [SerializeField] private float _minIntensity = 0.6f;
        [SerializeField] private float _maxIntensity = 1.4f;
        [SerializeField] private float _minRange = 5f;
        [SerializeField] private float _maxRange = 7f;
        [SerializeField] private float _flickerSpeed = 8f;

        private Light _light;
        private float _baseIntensity;
        private float _baseRange;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null)
            {
                _baseIntensity = _light.intensity;
                _baseRange = _light.range;
            }
        }

        private void Update()
        {
            if (_light == null) return;

            // Perlin noise-based flicker for natural-looking variation
            float noiseVal = Mathf.PerlinNoise(Time.time * _flickerSpeed, transform.position.x * 0.1f);
            _light.intensity = Mathf.Lerp(_minIntensity, _maxIntensity, noiseVal);
            _light.range = Mathf.Lerp(_minRange, _maxRange, noiseVal);
        }
    }
}