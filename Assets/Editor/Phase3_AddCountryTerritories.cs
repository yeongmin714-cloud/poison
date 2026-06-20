using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Core;

/// <summary>
/// Phase 3.6: 2000x2000 TopDownScene에 3개 국가 × 20 영지 오버레이 추가
///
/// 3개 국가가 시계 방향으로 배치됨:
///   - 3시 (X+) = 국가 A (초록)
///   - 6시 (Z-) = 국가 B (파랑) ← 플레이어 시작 위치
///   - 9시 (X-) = 국가 C (빨강)
///
/// 각 국가 = 5×4 = 20개 영지 (영지당 ~133×133)
/// 영지는 TextMesh 라벨 "A-01" ~ "C-20"
/// </summary>
public static class Phase3_AddCountryTerritories
{
    private const string SCENE_PATH = "Assets/Scenes/TopDownScene.unity";

    // 국가 데이터
    private static readonly CountryDef[] COUNTRIES = new[]
    {
        new CountryDef
        {
            Name = "A",
            Label = "A",
            Color = new Color(0.2f, 0.7f, 0.2f),       // 초록
            Center = new Vector3(667f, 0f, 0f),         // 3시: X>0
            // 긴 축 = Z (667), 짧은 축 = X (533)
            CountLong = 5,  // columns along long axis
            CountShort = 4, // rows along short axis
            LongAxis = Axis.Z,
            ShortAxis = Axis.X_Pos,
        },
        new CountryDef
        {
            Name = "B",
            Label = "B",
            Color = new Color(0.2f, 0.4f, 0.8f),       // 파랑
            Center = new Vector3(0f, 0f, -667f),        // 6시: Z<0
            // 긴 축 = X (667), 짧은 축 = Z (533)
            CountLong = 5,
            CountShort = 4,
            LongAxis = Axis.X,
            ShortAxis = Axis.Z_Neg,
        },
        new CountryDef
        {
            Name = "C",
            Label = "C",
            Color = new Color(0.8f, 0.2f, 0.2f),       // 빨강
            Center = new Vector3(-667f, 0f, 0f),        // 9시: X<0
            // 긴 축 = Z (667), 짧은 축 = X (533)
            CountLong = 5,
            CountShort = 4,
            LongAxis = Axis.Z,
            ShortAxis = Axis.X_Neg,
        },
    };

    private const float COUNTRY_SIZE_LONG = 667f;   // 긴 변
    private const float COUNTRY_SIZE_SHORT = 533f;  // 짧은 변
    private const float BORDER_HEIGHT = 0.03f;
    private const float MARKER_HEIGHT = 0.02f;
    private const float LABEL_HEIGHT = 0.5f;

    [MenuItem("Tools/Phase 3.6 - Add Country Territories")]
    public static void AddCountryTerritories()
    {
        // ===== 기존 TopDownScene 열기 =====
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        // ===== 부모 GameObject =====
        var parent = new GameObject("CountryTerritories_Overlay");
        parent.transform.position = Vector3.zero;

        foreach (var country in COUNTRIES)
        {
            CreateCountry(parent.transform, country);
        }

        // ===== 플레이어 시작 위치를 6시 방향 가장자리로 이동 =====
        var player = GameObject.Find("Player");
        if (player != null)
        {
            player.transform.position = new Vector3(0f, 0f, -950f);
            Debug.Log("[Phase3.6] Player moved to (0, 0, -950) — 6 o'clock edge");
        }
        else
        {
            Debug.LogWarning("[Phase3.6] Player GameObject not found! Creating one...");
            player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0f, -950f);
            player.tag = "Player";
        }

        // ===== 씬 저장 =====
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        Debug.Log($"[Phase3.6] Country territories added → {SCENE_PATH}");
    }

    [MenuItem("Tools/Phase 3.6 - Add Country Territories", true)]
    private static bool Validate() => true;

    // ──────────────────────────────────────────────
    //  국가 생성
    // ──────────────────────────────────────────────
    private static void CreateCountry(Transform parent, CountryDef country)
    {
        var countryGO = new GameObject($"Country_{country.Name}");
        countryGO.transform.parent = parent;
        countryGO.transform.position = Vector3.zero;

        // 국가 영역 바탕 (반투명)
        var bg = CreateFlatQuad($"Country_{country.Name}_BG", COUNTRY_SIZE_LONG, COUNTRY_SIZE_SHORT);
        bg.transform.parent = countryGO.transform;
        bg.transform.position = country.Center + Vector3.up * 0.01f;
        var bgMat = MaterialHelper.CreateLitMaterial(country.Color, $"Country_{country.Name}_BG_Mat");
        bgMat.color = new Color(country.Color.r, country.Color.g, country.Color.b, 0.25f);
        bg.GetComponent<MeshRenderer>().material = bgMat;

        // 영지 표식용 밝은 색상
        Color markerColor = country.Color * 1.2f;
        markerColor.a = 0.55f;

        // 그리드 계산
        float territoryLong = COUNTRY_SIZE_LONG / country.CountLong;
        float territoryShort = COUNTRY_SIZE_SHORT / country.CountShort;

        // 그리드 시작점 (영지 중심 기준, 가장 안쪽/왼쪽)
        // 긴축 반폭, 짧은축 반폭
        float halfLong = COUNTRY_SIZE_LONG / 2f;
        float halfShort = COUNTRY_SIZE_SHORT / 2f;

        int id = 1;
        for (int li = 0; li < country.CountLong; li++)
        {
            for (int si = 0; si < country.CountShort; si++)
            {
                // 영지 중심 위치 계산
                Vector3 pos = CalculateTerritoryPosition(
                    country, li, si, territoryLong, territoryShort, halfLong, halfShort
                );

                // 영지 칸 (반투명 사각형)
                var marker = CreateFlatQuad(
                    $"Territory_{country.Name}_{id:D2}",
                    territoryLong * 0.85f,
                    territoryShort * 0.85f
                );
                marker.transform.parent = countryGO.transform;
                marker.transform.position = pos + Vector3.up * MARKER_HEIGHT;
                var markerMat = MaterialHelper.CreateLitMaterial(
                    markerColor, $"Territory_{country.Name}_{id:D2}_Mat"
                );
                marker.GetComponent<MeshRenderer>().material = markerMat;

                // 경계선 (4변)
                CreateBorderLines(countryGO.transform, country.Name, id,
                    pos, territoryLong, territoryShort);

                // 영지 라벨 (TextMesh)
                CreateTerritoryLabel(countryGO.transform, $"{country.Label}-{id:D2}",
                    pos, territoryLong, territoryShort);

                id++;
            }
        }

        // 국가 이름
        var countryLabel = new GameObject($"Label_Country_{country.Name}");
        countryLabel.transform.parent = countryGO.transform;
        var tm = countryLabel.AddComponent<TextMesh>();
        tm.text = $"Country {country.Label}";
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;

        // 라벨 위치: 국가 바깥쪽 가장자리 중앙
        Vector3 labelOffset = country.ShortAxis switch
        {
            Axis.X_Pos => new Vector3(COUNTRY_SIZE_SHORT / 2f + 5f, 0, 0),
            Axis.Z_Neg => new Vector3(0, 0, -COUNTRY_SIZE_SHORT / 2f - 5f),
            Axis.X_Neg => new Vector3(-COUNTRY_SIZE_SHORT / 2f - 5f, 0, 0),
            _ => Vector3.zero,
        };
        countryLabel.transform.position = country.Center + labelOffset + Vector3.up * LABEL_HEIGHT;
        countryLabel.transform.localScale = Vector3.one * 0.15f;
    }

    // ──────────────────────────────────────────────
    //  영지 위치 계산
    // ──────────────────────────────────────────────
    private static Vector3 CalculateTerritoryPosition(
        CountryDef country, int longIdx, int shortIdx,
        float tLong, float tShort, float halfLong, float halfShort)
    {
        float longPos = -halfLong + tLong * longIdx + tLong / 2f;
        float shortPos = -halfShort + tShort * shortIdx + tShort / 2f;

        return country.ShortAxis switch
        {
            // Country A: axisShort=X+, axisLong=Z
            Axis.X_Pos => country.Center + new Vector3(shortPos, 0, longPos),
            // Country B: axisShort=Z-, axisLong=X
            Axis.Z_Neg => country.Center + new Vector3(longPos, 0, -shortPos),
            // Country C: axisShort=X-, axisLong=Z
            Axis.X_Neg => country.Center + new Vector3(-shortPos, 0, longPos),
            _ => Vector3.zero,
        };
    }

    // ──────────────────────────────────────────────
    //  경계선 (LineRenderer로 4변)
    // ──────────────────────────────────────────────
    private static void CreateBorderLines(Transform parent, string countryName, int id,
        Vector3 center, float width, float depth)
    {
        float hw = width / 2f;
        float hd = depth / 2f;

        Vector3[] corners = new Vector3[]
        {
            new Vector3(center.x - hw, BORDER_HEIGHT, center.z - hd),  // BL
            new Vector3(center.x + hw, BORDER_HEIGHT, center.z - hd),  // BR
            new Vector3(center.x + hw, BORDER_HEIGHT, center.z + hd),  // TR
            new Vector3(center.x - hw, BORDER_HEIGHT, center.z + hd),  // TL
        };

        // 4개의 선분
        (int, int)[] edges = { (0, 1), (1, 2), (2, 3), (3, 0) };
        string[] edgeNames = { "B", "R", "T", "L" };

        for (int e = 0; e < 4; e++)
        {
            var go = new GameObject($"Border_{countryName}_{id:D2}_{edgeNames[e]}");
            go.transform.parent = parent;
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, corners[edges[e].Item1]);
            lr.SetPosition(1, corners[edges[e].Item2]);
            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;
            lr.startColor = new Color(1, 1, 1, 0.35f);
            lr.endColor = new Color(1, 1, 1, 0.35f);
            lr.useWorldSpace = true;

            // Unity 6 URP 호환 머티리얼
            var lineMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (lineMat != null)
            {
                lineMat.color = new Color(1, 1, 1, 0.35f);
                lineMat.name = $"Border_{countryName}_{id:D2}_Mat";
                lr.sharedMaterial = lineMat;
            }
        }
    }

    // ──────────────────────────────────────────────
    //  영지 라벨 (TextMesh)
    // ──────────────────────────────────────────────
    private static void CreateTerritoryLabel(Transform parent, string label,
        Vector3 center, float width, float depth)
    {
        var go = new GameObject($"Label_Territory_{label}");
        go.transform.parent = parent;

        var tm = go.AddComponent<TextMesh>();
        tm.text = label;
        tm.fontSize = 24;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;

        go.transform.position = center + Vector3.up * LABEL_HEIGHT;
        go.transform.localScale = Vector3.one * 0.12f;
    }

    // ──────────────────────────────────────────────
    //  평면 메시 생성
    // ──────────────────────────────────────────────
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
        mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 1), new Vector2(1, 1),
        };

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>();

        return go;
    }

    // ──────────────────────────────────────────────
    //  내부 타입
    // ──────────────────────────────────────────────
    private enum Axis { X, Z, X_Pos, Z_Neg, X_Neg }

    private struct CountryDef
    {
        public string Name;       // "A", "B", "C"
        public string Label;      // "A", "B", "C" (라벨 접두사)
        public Color Color;       // 국가 색상
        public Vector3 Center;    // 국가 중심 위치
        public int CountLong;     // 긴 축 방향 영지 개수 (5)
        public int CountShort;    // 짧은 축 방향 영지 개수 (4)
        public Axis LongAxis;     // 긴 축 방향
        public Axis ShortAxis;    // 짧은 축 방향 (+/− 포함)
    }

}
