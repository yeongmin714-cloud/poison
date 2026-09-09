using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 중세 실내 기본 셸 (2026-09-09 5차): 바닥/벽4/서까래/횃불 상주 구조.
    /// 씬에 영구 저장(에디터 메뉴) 또는 런타임 생성 겸용. 빌더 8종은 이 셸 위에 가구만 배치.
    /// </summary>
    public static class MedievalShellBuilder
    {
        public const float W = 12f, D = 9f, H = 4f;

        public static GameObject CreateShell(float w = W, float d = D, float h = H)
        {
            var root = new GameObject("MedievalShell");

            var floorMat = Mat(TexFlagstone(), new Color(0.85f, 0.85f, 0.85f), 6f, 5f);
            var stoneMat = Mat(TexStoneBrick(), new Color(0.8f, 0.8f, 0.8f), 6f, 2f);
            var plasterMat = Mat(TexPlaster(), new Color(0.92f, 0.88f, 0.78f), 4f, 2f);
            var woodMat = Mat(TexWood(), new Color(0.75f, 0.6f, 0.4f), 2f, 1f);

            // 바닥
            Quad(root.transform, "Floor", floorMat, new Vector3(0f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), w, d);

            // 벽 4면: 북(z+), 서(x-), 동(x+) — 남측은 문틈(좌우 2분할)
            Wall(root.transform, "Wall_N", stoneMat, plasterMat, woodMat, w, h, new Vector3(0f, h / 2f, d / 2f), Quaternion.identity);
            Wall(root.transform, "Wall_W", stoneMat, plasterMat, woodMat, d, h, new Vector3(-w / 2f, h / 2f, 0f), Quaternion.Euler(0f, 90f, 0f));
            Wall(root.transform, "Wall_E", stoneMat, plasterMat, woodMat, d, h, new Vector3(w / 2f, h / 2f, 0f), Quaternion.Euler(0f, 90f, 0f));
            float doorW = 2.2f, sideW = (w - doorW) / 2f;
            Quad(root.transform, "Wall_S_L", stoneMat, new Vector3(-(doorW / 2f + sideW / 2f), h / 2f, -d / 2f), Quaternion.Euler(0f, 180f, 0f), sideW, h);
            Quad(root.transform, "Wall_S_R", stoneMat, new Vector3(doorW / 2f + sideW / 2f, h / 2f, -d / 2f), Quaternion.Euler(0f, 180f, 0f), sideW, h);
            Quad(root.transform, "Wall_S_Top", stoneMat, new Vector3(0f, h - 0.6f, -d / 2f), Quaternion.Euler(0f, 180f, 0f), doorW, 1.2f);

            // 서까래 3개 (천장 없음 — 개방형)
            for (int i = -1; i <= 1; i++)
                Quad(root.transform, $"Beam_{i+1}", woodMat, new Vector3(i * (w / 3f), h - 0.25f, 0f),
                    Quaternion.Euler(0f, 90f, 0f), 0.35f, d, box: true);

            // 횃불 3개 (벽 부착 + 오렌지 포인트라이트 + 깜빡임)
            for (int i = -1; i <= 1; i++)
            {
                var torch = new GameObject($"Torch_{i + 1}");
                torch.transform.SetParent(root.transform, false);
                torch.transform.position = new Vector3(i * (w / 3f), 2.6f, d / 2f - 0.25f);
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(torch.transform, false);
                pole.transform.localScale = new Vector3(0.08f, 0.5f, 0.08f);
                pole.transform.position = new Vector3(0f, 0.25f, 0f);
                var flame = new GameObject("FlameLight");
                flame.transform.SetParent(torch.transform, false);
                flame.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                var light = flame.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.63f, 0.25f);
                light.intensity = 2.2f;
                light.range = 8f;
                light.shadows = LightShadows.Soft;
                flame.AddComponent<TorchFlicker>();
            }

            return root;
        }

        // 벽 1면 = 석벽(하단 2m) + 회반죽(상단 2m) 2매
        private static void Wall(Transform parent, string name, Material stone, Material plaster, Material wood, float len, float h, Vector3 pos, Quaternion rot)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.transform.rotation = rot;
            Quad(wall.transform, "Stone", stone, new Vector3(0f, -h / 4f, 0f), Quaternion.identity, len, h / 2f);
            Quad(wall.transform, "Plaster", plaster, new Vector3(0f, h / 4f, 0f), Quaternion.identity, len, h / 2f);
            Quad(wall.transform, "BeamTop", wood, new Vector3(0f, h / 2f - 0.12f, 0.05f), Quaternion.identity, len, 0.25f);
        }

        private static void Quad(Transform parent, string name, Material mat, Vector3 localPos, Quaternion rot, float w, float h, bool box = false)
        {
            GameObject go;
            if (box)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localRotation = rot;
                go.transform.localScale = new Vector3(w, h, 0.35f);
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localRotation = rot;
                go.transform.localScale = new Vector3(w, h, 1f);
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            Object.Destroy(go.GetComponent<Collider>());
        }

        private static Material Mat(Texture2D tex, Color tint, float tileX, float tileY)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = tex.name + "_Mat", mainTexture = tex, color = tint };
            m.SetTextureScale("_BaseMap", new Vector2(tileX, tileY));
            return m;
        }

        // ── 절차 텍스처 ──
        private static Texture2D TexFlagstone()   // 돌판 바닥
        {
            var t = NewTex();
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float cell = (Mathf.FloorToInt(x / 32f) + Mathf.FloorToInt(y / 32f)) % 2f;
                float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.15f;
                byte v = (byte)Mathf.Clamp((150f + cell * 22f + n * 100f), 90f, 210f);
                t.SetPixel(x, y, new Color32(v, (byte)(v - 4), (byte)(v - 8), 255));
            }
            return Finish(t);
        }

        private static Texture2D TexStoneBrick()  // 석벽 벽돌
        {
            var t = NewTex();
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                int row = y / 16;
                int off = (row % 2) * 16;
                bool mortar = (y % 16 < 2) || ((x + off) % 32 < 2);
                float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.18f;
                byte v = mortar ? (byte)110 : (byte)Mathf.Clamp(165f + n * 90f, 120f, 215f);
                t.SetPixel(x, y, new Color32(v, (byte)(v - 3), (byte)(v - 6), 255));
            }
            return Finish(t);
        }

        private static Texture2D TexPlaster()     // 회반죽
        {
            var t = NewTex();
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.2f;
                byte v = (byte)Mathf.Clamp(205f + n * 80f, 160f, 240f);
                t.SetPixel(x, y, new Color32(v, (byte)(v - 6), (byte)(v - 18), 255));
            }
            return Finish(t);
        }

        private static Texture2D TexWood()        // 목재
        {
            var t = NewTex();
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float grain = Mathf.PerlinNoise(x * 0.05f, y * 0.5f) * 0.3f;
                byte v = (byte)Mathf.Clamp(120f + grain * 110f, 70f, 190f);
                t.SetPixel(x, y, new Color32((byte)(v + 30), (byte)(v - 10), (byte)(v - 45), 255));
            }
            return Finish(t);
        }

        private static Texture2D NewTex() => new Texture2D(128, 128, TextureFormat.RGBA32, false);
        private static Texture2D Finish(Texture2D t) { t.Apply(); t.wrapMode = TextureWrapMode.Repeat; return t; }
    }

    /// <summary>횃불 깜빡임 — 포인트라이트 강도 사인+노이즈 진동.</summary>
    public class TorchFlicker : MonoBehaviour
    {
        private Light _light;
        private float _base, _t;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null) _base = _light.intensity;
        }

        private void Update()
        {
            if (_light == null) return;
            _t += Time.deltaTime * 9f;
            _light.intensity = _base * (0.82f + 0.18f * (Mathf.Sin(_t) * 0.6f + Mathf.PerlinNoise(_t, 0.5f) * 0.4f));
        }
    }
}
