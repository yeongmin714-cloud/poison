using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Core;

public static class Phase3_WorldMapSetup
{
    /// <summary>
    /// Phase 3: 세계관 & 월드맵 — 위에서 내려다보는 평면 지형
    /// 
    /// 만드는 것:
    /// 1. Ground — 큰 평면 (200×200)
    /// 2. 국가별 영역 — 3개 색상 구분 (A=초록, B=파랑, C=빨강)
    /// 3. 영지 그리드 — 각 국가당 5×4 = 20개 영지, 경계선 표시
    /// 4. 카메라 — 정사영(Orthographic) top-down
    /// 5. Directional Light — 태양광
    /// 6. GameManager — 게임 관리자
    /// </summary>
    [MenuItem("Tools/Phase 3 - Setup World Map")]
    public static void SetupWorldMap()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ===== 파라미터 =====
        float mapSize = 200f;
        float countryWidth = 60f;
        float countryDepth = 100f;
        float gapBetweenCountries = 5f;
        int territoriesX = 5;
        int territoriesZ = 4;

        Color[] countryColors = new Color[]
        {
            new Color(0.2f, 0.6f, 0.2f),    // 국가 A — 초록
            new Color(0.2f, 0.4f, 0.7f),    // 국가 B — 파랑
            new Color(0.7f, 0.2f, 0.2f),    // 국가 C — 빨강
        };

        string[] countryNames = { "국가 A (초급)", "국가 B (중급)", "국가 C (최종)" };

        // ===== 1. 전체 배경 Ground =====
        var bgGround = CreateFlatQuad("WorldMap_Background", mapSize, mapSize);
        bgGround.transform.position = new Vector3(0, -0.1f, 0);
        var bgMat = MaterialHelper.CreateLitMaterial(
            new Color(0.12f, 0.12f, 0.16f), "Map_Background"
        );
        bgGround.GetComponent<MeshRenderer>().material = bgMat;

        // ===== 2. 국가별 영역 =====
        float startX = -(countryWidth + gapBetweenCountries);
        for (int c = 0; c < 3; c++)
        {
            float centerX = startX + c * (countryWidth + gapBetweenCountries);

            // 국가 영역
            var countryPlane = CreateFlatQuad($"Country_{c}",
                countryWidth, countryDepth);
            countryPlane.transform.position = new Vector3(centerX, 0f, 0);
            var mat = MaterialHelper.CreateLitMaterial(countryColors[c], $"Country_{c}_Mat");
            countryPlane.GetComponent<MeshRenderer>().material = mat;

            // ===== 3. 영지 그리드 (경계선 + 표식) =====
            float territoryW = countryWidth / territoriesX;
            float territoryH = countryDepth / territoriesZ;
            float leftX = centerX - countryWidth / 2f + territoryW / 2f;
            float bottomZ = -countryDepth / 2f + territoryH / 2f;
            int id = 1;

            Color territoryColor = countryColors[c] * 1.15f;
            territoryColor.a = 0.6f;

            for (int x = 0; x < territoriesX; x++)
            {
                for (int z = 0; z < territoriesZ; z++)
                {
                    float tx = leftX + x * territoryW;
                    float tz = bottomZ + z * territoryH;

                    // 영지 칸 (반투명 밝은색 사각형)
                    var marker = CreateFlatQuad($"Territory_{c}_{id:D2}",
                        territoryW * 0.85f, territoryH * 0.85f);
                    marker.transform.position = new Vector3(tx, 0.02f, tz);
                    var markerMat = MaterialHelper.CreateLitMaterial(
                        territoryColor, $"Territory_{c}_{id:D2}_Mat"
                    );
                    markerMat.color = territoryColor;
                    marker.GetComponent<MeshRenderer>().material = markerMat;

                    // 경계선 (모서리만 흰 점선 느낌)
                    CreateCornerLine(
                        tx - territoryW / 2f, tz - territoryH / 2f,
                        tx + territoryW / 2f, tz - territoryH / 2f,
                        $"Border_{c}_{id:D2}_B"
                    );
                    CreateCornerLine(
                        tx + territoryW / 2f, tz - territoryH / 2f,
                        tx + territoryW / 2f, tz + territoryH / 2f,
                        $"Border_{c}_{id:D2}_R"
                    );
                    CreateCornerLine(
                        tx + territoryW / 2f, tz + territoryH / 2f,
                        tx - territoryW / 2f, tz + territoryH / 2f,
                        $"Border_{c}_{id:D2}_T"
                    );
                    CreateCornerLine(
                        tx - territoryW / 2f, tz + territoryH / 2f,
                        tx - territoryW / 2f, tz - territoryH / 2f,
                        $"Border_{c}_{id:D2}_L"
                    );

                    id++;
                }
            }

            // 국가 이름 (TextMesh)
            var labelGO = new GameObject($"Label_Country_{c}");
            var textMesh = labelGO.AddComponent<TextMesh>();
            textMesh.text = countryNames[c];
            textMesh.fontSize = 48;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            labelGO.transform.position = new Vector3(centerX, 0.5f, -countryDepth / 2f - 6f);
            labelGO.transform.localScale = Vector3.one * 0.12f;
        }

        // ===== 4. Directional Light =====
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
        light.shadowStrength = 0.3f;
        lightGO.transform.rotation = Quaternion.Euler(90, 0, 0);  // 위에서 비춤

        // ===== 5. Top-down 카메라 (Orthographic) =====
        var camGO = new GameObject("WorldMap Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
        cam.orthographic = true;
        cam.orthographicSize = 90f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;
        camGO.transform.position = new Vector3(0, 80f, 0);
        camGO.transform.rotation = Quaternion.Euler(90, 0, 0);

        // ===== 6. GameManager =====
        var gmGO = new GameObject("GameManager");
        gmGO.AddComponent<GameManager>();

        // ===== 씬 저장 =====
        string path = "Assets/Scenes/WorldMap.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[Phase3] World Map terrain created → {path}");
    }

    /// <summary>
    /// 평면 메시를 가진 GameObject 생성 (PrimitiveType.Plane 대체)
    /// — 기본 머티리얼(분홍색 문제) 없이 순수 메시만 만듦
    /// </summary>
    private static GameObject CreateFlatQuad(string name, float width, float depth)
    {
        var go = new GameObject(name);

        var mesh = new Mesh();
        mesh.name = $"{name}_Mesh";
        mesh.vertices = new Vector3[]
        {
            new Vector3(-width / 2f, 0, -depth / 2f),
            new Vector3( width / 2f, 0, -depth / 2f),
            new Vector3(-width / 2f, 0,  depth / 2f),
            new Vector3( width / 2f, 0,  depth / 2f),
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.normals = new Vector3[]
        {
            Vector3.up, Vector3.up, Vector3.up, Vector3.up
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 1), new Vector2(1, 1)
        };

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        // material은 호출부에서 할당

        return go;
    }

    private static void CreateCornerLine(float x1, float z1, float x2, float z2, string name)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(x1, 0.03f, z1));
        lr.SetPosition(1, new Vector3(x2, 0.03f, z2));
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.startColor = new Color(1, 1, 1, 0.35f);
        lr.endColor = new Color(1, 1, 1, 0.35f);
        lr.useWorldSpace = true;
    }

    [MenuItem("Tools/Phase 3 - Setup World Map", true)]
    private static bool Validate() => true;
}