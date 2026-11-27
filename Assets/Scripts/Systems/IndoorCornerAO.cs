using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [126차] 실내 모서리 고품질화 — 지오메트리 무변경 렌더 기반 AO 데칼 시스템.
    /// 벽-벽 수직 코너 스트립 + 벽-바닥 접지 스트립을 결정론 배치.
    /// 패턴 출처: ApplyTavernToRoom 상단 밴드 쿼드(명시 정점+UV 메시) + 짚 데칼 계약.
    /// 계약 준수: 탑다운 원칙(걸레받이 금지 — 음영 데칼이지 지오메트리 아님),
    /// 렌더러 전용(무콜라이더·섀도우Off), 결정론(무랜덤), 원본 파일 무손상(절차 생성).
    /// </summary>
    public static class IndoorCornerAO
    {
        private const float VERTICAL_H = 2.4f;    // 수직 스트립 높이
        private const float VERTICAL_W = 0.35f;   // 수직 스트립 폭
        private const float GROUND_W = 0.55f;     // 바닥 스트립 폭
        private const float GROUND_LIFT = 0.012f; // 바닥 스트립 부양(짚 데칼 동일)
        private const float ALPHA_PEAK = 0.35f;   // 최대 어두움
        private const float EPS = 0.01f;          // 벽면 이격(스트립이 벽에 묻히지 않게)

        private static Texture2D _gradient;
        private static bool _attempted;

        /// <summary>결정론 그라디언트 — 알파 1(모서리)→0(바깥), 이차 감쇠.</summary>
        private static Texture2D EnsureGradient()
        {
            if (_gradient != null || _attempted) return _gradient;
            _attempted = true;
            try
            {
                const int w = 64, h = 4;
                var tex = new Texture2D(w, h, TextureFormat.ARGB32, false, true)
                {
                    name = "IndoorHQ_CornerAO_Gradient",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color[w * h];
                for (int x = 0; x < w; x++)
                {
                    float t = x / (float)(w - 1);          // 0=모서리 1=바깥
                    float a = (1f - t) * (1f - t);         // 이차 감쇠
                    var c = new Color(0f, 0f, 0f, a * ALPHA_PEAK);
                    for (int y = 0; y < h; y++) pixels[y * w + x] = c;
                }
                tex.SetPixels(pixels);
                tex.Apply(false, true);
                _gradient = tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[IndoorCornerAO] 그라디언트 생성 실패(무해): " + e.Message);
            }
            return _gradient;
        }

        private static Material CreateAOMaterial()
        {
            var tex = EnsureGradient();
            if (tex == null) return null;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = "IndoorHQ_CornerAO" };
            m.mainTexture = tex;
            // 투명 데칼 — 짚 데칼 동일 블렌드 계약
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }

        /// <summary>렌더러 전용 — 무콜라이더·섀도우Off. 스트립 메시는 (w×h) XZ/XY 평면, UV.x가 그라디언트 축.</summary>
        private static GameObject CreateStrip(GameObject parent, string name, Vector3[] verts, int[] tris, Vector2[] uv, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mesh = new Mesh { name = name + "_Mesh" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static GameObject CreateQuad(GameObject parent, string name, Vector3 localPos, Quaternion rot, Vector2 size, Material mat)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent.transform, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = rot;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return quad;
        }

        /// <summary>
        /// 벽-바닥 접지 AO 스트립 — 바닥 위에 눕히고, 그라디언트 어두운 쪽(uv.x=0)이 벽 쪽.
        /// yaw 회전으로 4 방향 대응(쿼드 회전은 UV를 오브젝트 공간에서 굴리므로 축 대칭 그라디언트라 안전).
        /// </summary>
        private static void AddGroundStrips(GameObject room, float width, float depth, Material mat)
        {
            if (mat == null) return;
            float halfW = width * 0.5f, halfD = depth * 0.5f;
            AddGroundStrip(room, "CornerAO_Ground_N", new Vector3(0f, GROUND_LIFT, -halfD + GROUND_W * 0.5f), new Vector2(width, GROUND_W), 0f, mat);
            AddGroundStrip(room, "CornerAO_Ground_S", new Vector3(0f, GROUND_LIFT, halfD - GROUND_W * 0.5f), new Vector2(width, GROUND_W), 180f, mat);
            AddGroundStrip(room, "CornerAO_Ground_W", new Vector3(-halfW + GROUND_W * 0.5f, GROUND_LIFT, 0f), new Vector2(depth, GROUND_W), 90f, mat);
            AddGroundStrip(room, "CornerAO_Ground_E", new Vector3(halfW - GROUND_W * 0.5f, GROUND_LIFT, 0f), new Vector2(depth, GROUND_W), 270f, mat);
        }

        private static void AddGroundStrip(GameObject room, string name, Vector3 pos, Vector2 size, float yawDeg, Material mat)
        {
            CreateQuad(room, name, pos, Quaternion.Euler(90f, yawDeg, 0f), size, mat);
        }

        /// <summary>
        /// 벽-벽 수직 코너 AO — 각 모서리에서 두 벽면에 각각 1장(총 8장).
        /// 각 스트립은 자기 벽면에 평행한 XY 평면(명시 정점)이라 쿼드 회전 추측 불필요.
        /// </summary>
        private static void AddVerticalStrips(GameObject room, float width, float depth, float height, Material mat)
        {
            if (mat == null) return;
            float halfW = width * 0.5f, halfD = depth * 0.5f;
            float h = Mathf.Min(height, VERTICAL_H);

            // XZ 평면상 코너 4개 좌표
            var corners = new (float cx, float cz, string tag)[]
            {
                (-halfW, -halfD, "NW"),
                ( halfW, -halfD, "NE"),
                (-halfW,  halfD, "SW"),
                ( halfW,  halfD, "SE")
            };

            foreach (var (cx, cz, tag) in corners)
            {
                // 벽면 2개: (노멀 방향, 스트립 중심)
                // Front/Back 벽면 = XZ 평면에서 z 고정, 스트립은 XY 평면(x 방향으로 퍼짐)
                // Left/Right 벽면 = x 고정, 스트립은 ZY 평면(z 방향으로 퍼짐)
                bool xSide = tag == "NW" || tag == "SW"; // Left(-x) 여부
                bool zSide = tag == "NW" || tag == "NE"; // Front(-z) 여부

                // --- 벽면 A: z 고정(Front/Back 벽) ---
                {
                    float dirIn = zSide ? 1f : -1f;           // 방 내부(+z / -z)
                    float away = xSide ? 1f : -1f;            // 모서리에서 방 안쪽 x 방향
                    float zWall = cz + dirIn * EPS;           // 벽 안쪽 표면 바로 앞
                    float xWall = cx;                         // XY 평면 고정축은 모서리 x
                    AddVerticalStrip(room, "CornerAO_Vert_" + tag + "_FB", xWall, zWall, h, false, zSide, xSide, away, mat);
                }
                // --- 벽면 B: x 고정(Left/Right 벽) ---
                {
                    float dirIn = xSide ? 1f : -1f;           // 방 내부(+x / -x)
                    float away = zSide ? 1f : -1f;            // 모서리에서 방 안쪽 z 방향
                    float xWall = cx + dirIn * EPS;           // 벽 안쪽 표면 바로 앞
                    float zWall = cz;                         // ZY 평면 고정축은 모서리 z
                    AddVerticalStrip(room, "CornerAO_Vert_" + tag + "_LR", xWall, zWall, h, true, zSide, xSide, away, mat);
                }
            }
        }

        /// <summary>
        /// 수직 스트립 1장 — 명시 정점. isZYPlane=false: XY 평면(Front/Back 벽, u=x), true: ZY 평면(Left/Right 벽, u=z).
        /// uv.x=0(어두움)이 모서리 쪽, uv.x=1이 방 안쪽. away=모서리에서 방 안쪽 방향 부호(벽축 방향).
        /// 와인딩은 노멀이 방 내부를 향하도록 계산:
        ///   XY 평면 노멀.z = ±away → Front(내부 +z): flip=(away&gt;0)==zSide / Back(내부 -z): 반대
        ///   ZY 평면 노멀.x = ±away → Left(내부 +x): flip=(away&gt;0)!=xSide / Right(내부 -x): 반대
        /// </summary>
        private static void AddVerticalStrip(GameObject room, string name, float uWall, float vWall, float h, bool isZYPlane, bool zSide, bool xSide, float away, Material mat)
        {
            Vector3[] verts;
            if (!isZYPlane)
            {
                verts = new Vector3[]
                {
                    new Vector3(uWall, 0f, vWall),                  // 모서리 쪽 하단
                    new Vector3(uWall + away * VERTICAL_W, 0f, vWall), // 안쪽 하단
                    new Vector3(uWall, h, vWall),                   // 모서리 쪽 상단
                    new Vector3(uWall + away * VERTICAL_W, h, vWall)   // 안쪽 상단
                };
            }
            else
            {
                verts = new Vector3[]
                {
                    new Vector3(uWall, 0f, vWall),
                    new Vector3(uWall, 0f, vWall + away * VERTICAL_W),
                    new Vector3(uWall, h, vWall),
                    new Vector3(uWall, h, vWall + away * VERTICAL_W)
                };
            }
            var uv = new Vector2[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            // 와인딩 — 노멀이 방 내부를 향하도록:
            //   XY(노멀±z): 기본(0,2,1)은 normal.z=+away → 내부방향 == (away>0)==zSide. 어긋나면 플립.
            //   ZY(노멀±x): 기본(0,2,1)은 normal.x=+away → 내부방향 == (away>0)!=xSide. 어긋나면 플립.
            bool inward = isZYPlane ? ((away > 0f) != xSide) : ((away > 0f) == zSide);
            int[] tris = inward ? new[] { 0, 2, 1, 2, 3, 1 } : new[] { 0, 1, 2, 1, 3, 2 };
            CreateStrip(room, name, verts, tris, uv, mat);
        }

        /// <summary>실내 방에 모서리 AO 데칼 일괄 적용. 결정론 — 호출마다 동일 배치. 실내 전용(실외 스테이징 미적용).</summary>
        public static void Apply(GameObject room, float width, float depth, float height)
        {
            if (room == null) return;
            var mat = CreateAOMaterial();
            if (mat != null)
            {
                AddGroundStrips(room, width, depth, mat);
                AddVerticalStrips(room, width, depth, height, mat);
            }
            AddCornerPosts(room, width, depth, height);
            Debug.Log($"[IndoorCornerAO] 모서리 AO 적용 완료 — 바닥 4 + 수직 8 + 포스트 4 (room={room.name})");
        }

        // ==================================================================================================
        // [126차 승인 확장] 코너 포스트 — 모서리 구조용 목재 기둥(막목 구조 판독).
        // 사용자 승인(2026-10-26 "추가레버와 코너 포스트 승인"). 방 실루엣 변경을 수반하는 유일한 요소.
        // 계약: 무콜라이더(가구 콜라이더 금지 계약 준수 — 순수 시각), 섀도우 On(실제 지오메트리),
        // 목재 머티리얼 null 폴백(파일 부재 환경에서도 생성 보장), 결정론.
        // 배치: 모서리 중심에 정확히 교차시켜 양벽과 겹침 — 벽 두께 편차와 무관하게 틈 없음(코플래너 회피).
        // ==================================================================================================
        private const float POST_SIZE = 0.26f; // 포스트 단면

        private static void AddCornerPosts(GameObject room, float width, float depth, float height)
        {
            var woodMat = IndoorMaterialFactory.CreateWood();
            float halfW = width * 0.5f, halfD = depth * 0.5f;
            var corners = new (float cx, float cz, string tag)[]
            {
                (-halfW, -halfD, "NW"),
                ( halfW, -halfD, "NE"),
                (-halfW,  halfD, "SW"),
                ( halfW,  halfD, "SE")
            };
            foreach (var (cx, cz, tag) in corners)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "CornerPost_" + tag;
                Object.Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(room.transform, false);
                post.transform.localPosition = new Vector3(cx, height * 0.5f, cz);
                post.transform.localScale = new Vector3(POST_SIZE, height, POST_SIZE);
                if (woodMat != null)
                    post.GetComponent<MeshRenderer>().sharedMaterial = woodMat;
            }
        }
    }
}
