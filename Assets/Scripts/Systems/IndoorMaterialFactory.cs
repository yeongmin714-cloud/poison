using UnityEngine;
using ProjectName.Core;   // IndoorTextureLoader

namespace ProjectName.Systems
{
    /// <summary>
    /// P17 — 실내 고품질 머티리얼 팩토리 (URP Lit).
    /// 사용자 제공 심리스 텍스처(Resources/Indoor/)가 있으면 URP Lit(+노멀/스무스니스/타일링)로
    /// 고품질 재질을 만들고, 없으면 null을 반환해 기존 절차 생성 머티리얼을 유지(폴백).
    ///
    /// [품질 스펙 — 예시 이미지 대조]
    ///   바닥:  Smoothness 0.55(석재 반들거림 절제) + 노멀맵으로 균열 입체
    ///   벽 하부: 노멀맵 강조, 벽 상부: 회반죽 거칠게(Smoothness 0.35)
    ///   타일링: 이미지 커버 미터(FloorCoverMeters 등)로 방 크기 나눈 값 — 실제 스케일 정합
    /// </summary>
    public static class IndoorMaterialFactory
    {
        private static Shader _urpLit;

        private static Shader UrpLit
        {
            get
            {
                if (_urpLit == null)
                {
                    _urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (_urpLit == null)
                        _urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                }
                return _urpLit;
            }
        }

        /// <summary>제공 텍스처 기반 바닥 머티리얼 (없으면 null — 폴백 유지).</summary>
        public static Material CreateFloor(float roomWidth, float roomDepth)
        {
            if (!IndoorTextureLoader.HasFiles || UrpLit == null) return null;
            var m = new Material(UrpLit) { name = "IndoorHQ_Floor" };
            var baseTex = IndoorTextureLoader.Floor;
            IndoorTextureLoader.Configure(baseTex, linear: false);
            m.mainTexture = baseTex;
            m.color = Color.white;
            float tx = Mathf.Max(1f, Mathf.RoundToInt(roomWidth / IndoorTextureLoader.FloorCoverMeters));
            float ty = Mathf.Max(1f, Mathf.RoundToInt(roomDepth / IndoorTextureLoader.FloorCoverMeters));
            m.mainTextureScale = new Vector2(tx, ty);
            m.SetFloat("_Smoothness", 0.55f);
            var n = IndoorTextureLoader.FloorNormal;
            if (n != null)
            {
                IndoorTextureLoader.Configure(n, linear: true);
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", n);
                m.SetTextureScale("_BumpMap", new Vector2(tx, ty));
            }
            return m;
        }

        /// <summary>[2026-10-09 Phase D] 벽 틀/기둥/문틀 공용 목재 머티리얼 — 제공 wood_frame.png(세로 나뭇결).
        /// 프레임별 UV는 큐브 UV 그대로(2.2×1.1 벽 유닛과 별도). 파일 부재 시 다크 브라운 컬러 폴백.</summary>
        public static Material CreateWoodFrame()
        {
            var m = new Material(UrpLit) { name = "IndoorHQ_WoodFrame" };
            if (IndoorTextureLoader.HasFiles && IndoorTextureLoader.Wood != null)
            {
                var tex = IndoorTextureLoader.Wood;
                IndoorTextureLoader.Configure(tex, linear: false);
                m.mainTexture = tex;
                m.mainTextureScale = Vector2.one;
                m.color = new Color(0.92f, 0.88f, 0.82f);
                // [2026-10-10 F1] wood_frame_n 제공 시 나뭇결 범프(파일 없으면 스킵 — 선택 규약)
                var woodN = IndoorTextureLoader.WoodNormal;
                if (woodN != null)
                {
                    IndoorTextureLoader.Configure(woodN, linear: true);
                    m.EnableKeyword("_NORMALMAP");
                    m.SetTexture("_BumpMap", woodN);
                    m.SetTextureScale("_BumpMap", Vector2.one);
                }
            }
            else
            {
                m.color = new Color(0.35f, 0.24f, 0.15f);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.18f);
            return m;
        }

        /// <summary>제공 텍스처 기반 벽 머티리얼 — 하부 석재/상부 회반죽 분리판 반환 (없으면 null).</summary>
        public static bool TryCreateWall(float roomWidth, float roomHeight, out Material lower, out Material upper)
        {
            lower = null; upper = null;
            if (!IndoorTextureLoader.HasFiles || UrpLit == null) return false;
            // [2026-10-09 Phase D] 벽 전면 회반죽(사용자 제안 — 실내씬 예시.png 하프팀버 스타일 확정):
            //   하부 석재 밴드 폐지, 제공 wall_plaster_upper.png를 벽 전체에 적용.
            //   절차 ashlar(CreateCalmTopologyMasonryTexture)는 파일 부재 폴백으로만 잔존.
            var stone = new Material(UrpLit) { name = "IndoorHQ_WallPlasterFull" };
            if (IndoorTextureLoader.WallPlaster != null)
            {
                var plasterTex = IndoorTextureLoader.WallPlaster;
                IndoorTextureLoader.Configure(plasterTex, linear: false);
                stone.mainTexture = plasterTex;
                stone.mainTextureScale = Vector2.one;
                stone.color = new Color(0.94f, 0.93f, 0.90f);
                // [2026-10-10 F1] wall_plaster_upper_n 제공 시 회반죽 범프(파일 없으면 스킵 — 선택 규약)
                var plasterN = IndoorTextureLoader.WallPlasterNormal;
                if (plasterN != null)
                {
                    IndoorTextureLoader.Configure(plasterN, linear: true);
                    stone.EnableKeyword("_NORMALMAP");
                    stone.SetTexture("_BumpMap", plasterN);
                    stone.SetTextureScale("_BumpMap", Vector2.one);
                }
            }
            else
            {
                stone.mainTexture = CreateCalmTopologyMasonryTexture();
                stone.color = new Color(0.94f, 0.93f, 0.90f);
            }
            stone.SetFloat("_Smoothness", 0.12f);
            // Intentionally omit the supplied noisy normal map. The mortar and broad block
            // boundaries in the albedo provide restrained stone structure without pore noise.
            lower = stone;

            // 상부 회반죽
            var plaster = new Material(UrpLit) { name = "IndoorHQ_WallPlaster" };
            if (IndoorTextureLoader.WallPlaster != null)
            {
                var plasterTex = IndoorTextureLoader.WallPlaster;
                IndoorTextureLoader.Configure(plasterTex, linear: false);
                plaster.mainTexture = plasterTex;
                plaster.mainTextureScale = Vector2.one;
                plaster.SetFloat("_Smoothness", 0.3f);
            }
            else
            {
                plaster.color = new Color(0.77f, 0.71f, 0.60f);   // #C5B49A 근사 폴백
                plaster.SetFloat("_Smoothness", 0.3f);
            }
            upper = plaster;
            return true;
        }

        /// <summary>
        /// Builds a deterministic, seam-free two-course ashlar tile for the player-castle shell.
        /// One UV tile is the existing 2.2 x 1.1 m cover unit, so large walls do not gain
        /// extra material-level tiling. Broad block faces replace the noisy supplied albedo/normal.
        /// </summary>
        private static Texture2D CreateCalmTopologyMasonryTexture()
        {
            const int width = 512;
            const int height = 256;
            const int courseHeight = height / 2;
            const int blockWidth = width / 4;
            const float mortarHalfWidth = 2.0f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
            {
                name = "PlayerCastle_CalmAshlar_2Course",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                int course = y / courseHeight;
                int localY = y % courseHeight;
                int rowOffset = course == 0 ? 0 : blockWidth / 2;
                for (int x = 0; x < width; x++)
                {
                    int shiftedX = (x + rowOffset) % width;
                    int column = shiftedX / blockWidth;
                    int inBlockX = shiftedX % blockWidth;
                    float edgeX = Mathf.Min(inBlockX, blockWidth - inBlockX);
                    float edgeY = Mathf.Min(localY, courseHeight - localY);
                    float edge = Mathf.Min(edgeX, edgeY);
                    float mortar = 1f - Mathf.SmoothStep(mortarHalfWidth - 0.5f,
                        mortarHalfWidth + 0.5f, edge);
                    // Stable low-amplitude per-block variation; no Random/global state or high-frequency noise.
                    float variation = (StableBlockTone(column, course) - 0.5f) * 0.055f;
                    float broadEdgeShade = Mathf.Clamp01(edge / 12f) * 0.025f;
                    float value = 0.62f + variation + broadEdgeShade;
                    Color stone = new Color(value * 1.01f, value, value * 0.96f, 1f);
                    Color joint = new Color(0.39f, 0.375f, 0.35f, 1f);
                    pixels[y * width + x] = Color.Lerp(stone, joint, mortar);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static float StableBlockTone(int column, int course)
        {
            int value = (column + 17) * 374761393 + (course + 31) * 668265263;
            value = (value ^ (value >> 13)) * 1274126177;
            value ^= value >> 16;
            return (value & 0x00ffffff) / 16777215f;
        }

        /// <summary>목재 프레임 머티리얼 (기둥/보/문틀 — 기둥은 P17에서 제거됐지만 보/문틀용 유지).</summary>
        public static Material CreateWood()
        {
            if (!IndoorTextureLoader.HasFiles || UrpLit == null) return null;
            var woodTex = IndoorTextureLoader.Wood;
            if (woodTex == null) return null;
            var m = new Material(UrpLit) { name = "IndoorHQ_Wood" };
            IndoorTextureLoader.Configure(woodTex, linear: false);
            m.mainTexture = woodTex;
            m.mainTextureScale = new Vector2(1f, 2f);   // 세로 나뭇결 강조
            m.SetFloat("_Smoothness", 0.4f);
            return m;
        }

        /// <summary>Applies only the shared-provider floor material, leaving country fallback walls untouched.</summary>
        public static void ApplyFloorToRoom(GameObject room, float width, float depth)
        {
            if (room == null || !IndoorTextureLoader.HasFiles) return;
            Transform floor = room.transform.Find("Floor");
            if (floor == null) return;
            MeshRenderer renderer = floor.GetComponent<MeshRenderer>();
            Material material = CreateFloor(width, depth);
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        /// <summary>HQ stone surface material for procedural solid topology walls (null when unavailable).</summary>
        public static Material CreateTopologyWall(float roomWidth, float roomHeight)
        {
            if (!TryCreateWall(roomWidth, roomHeight, out var lower, out _)) return null;
            return lower;
        }

        /// <summary>Applies the shared indoor-provider material to all generated topology wall renderers.</summary>
        public static void ApplyToTopologyWalls(GameObject room, float roomWidth, float roomHeight)
        {
            if (room == null || !IndoorTextureLoader.HasFiles) return;
            Material wall = CreateTopologyWall(roomWidth, roomHeight);
            if (wall == null) return;
            foreach (Transform child in room.transform)
            {
                // Floor and ceiling also carry MeshColliders; do not overwrite the floor texture
                // just applied by ApplyFloorToRoom. Every other collidable direct child is a topology wall.
                if (child.name == "Floor" || child.name == "Ceiling" ||
                    child.GetComponent<MeshCollider>() == null) continue;
                var renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = wall;
            }
        }

        /// <summary>
        /// [P17-D 배선] 방 생성 직후 호출 — Floor/Wall 4면의 머티리얼을 제공 텍스처판으로 교체.
        /// 벽은 쿼드 UV 하단 0~0.5=석재/상단 0.5~1.0=회반죽 2서브메시가 아니라 쿼드 단일이므로,
        /// 벽은 하부 석재 머티리얼에 UV 스케일로 통일 적용(예시의 하부 위주 노출 쿼터뷰 특성 반영) +
        /// 상단 밴드는 벽 상단에 별도 쿼드를 얹어 투톤 구현.
        /// </summary>
        public static void ApplyToRoom(GameObject room, float width, float height, float depth)
        {
            if (room == null || !IndoorTextureLoader.HasFiles) return;

            var floor = room.transform.Find("Floor");
            if (floor != null)
            {
                var m = CreateFloor(width, depth);
                if (m != null) floor.GetComponent<MeshRenderer>().sharedMaterial = m;
            }

            if (TryCreateWall(width, height, out var lower, out var upper))
            {
                string[] wallNames = { "Wall_Front", "Wall_Back", "Wall_Left", "Wall_Right" };
                foreach (var wn in wallNames)
                {
                    var wall = room.transform.Find(wn);
                    if (wall == null) continue;
                    var r = wall.GetComponent<MeshRenderer>();
                    if (r != null) r.sharedMaterial = lower;

                    // 상단 밴드 쿼드 얹기 — 벽 위쪽 절반(회반죽) 커버
                    var band = new GameObject(wn + "_PlasterBand");
                    band.transform.SetParent(wall);
                    band.transform.localPosition = new Vector3(0f, height * 0.25f, 0f);
                    band.transform.localRotation = Quaternion.identity;
                    var mf = band.AddComponent<MeshFilter>();
                    var mesh = new Mesh { name = wn + "_PlasterBand_Mesh" };
                    float hw, hh;
                    bool isSide = wn == "Wall_Left" || wn == "Wall_Right";
                    hw = (isSide ? depth : width) * 0.5f;
                    hh = height * 0.25f;
                    mesh.vertices = new Vector3[]
                    {
                        new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0),
                        new Vector3(-hw,  hh, 0), new Vector3(hw,  hh, 0)
                    };
                    mesh.uv = new Vector2[]
                    {
                        new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                        new Vector2(0, 1.0f), new Vector2(1, 1.0f)
                    };
                    mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
                    mesh.RecalculateNormals();
                    mf.sharedMesh = mesh;
                    var br = band.AddComponent<MeshRenderer>();
                    br.sharedMaterial = upper;
                    br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                Debug.Log("[IndoorMaterialFactory] HQ 머티리얼 적용 완료 — Floor+Wall(석재/회반죽 투톤)");
            }
        }

        // ==================================================================================================
        // [2026-10-10 F1~F3 고품질화] 신규 머티리얼 팩토리 3종
        // ==================================================================================================

        /// <summary>[F1] 벽 하부 석재 밴드 머티리얼 — wall_stone_lower(2.2×1.1m 그리드).
        /// UV는 밴드 빌더가 실제 길이에 맞춰 기록하므로 여기선 1장 그대로. 파일 없으면 null.</summary>
        public static Material CreateWallStoneLower()
        {
            if (!IndoorTextureLoader.HasFiles || IndoorTextureLoader.WallStone == null) return null;
            var m = new Material(UrpLit) { name = "IndoorHQ_WallStoneLower" };
            var tex = IndoorTextureLoader.WallStone;
            IndoorTextureLoader.Configure(tex, linear: false);
            m.mainTexture = tex;
            m.mainTextureScale = Vector2.one;
            m.color = new Color(0.92f, 0.91f, 0.88f);
            var stoneN = IndoorTextureLoader.WallStoneNormal;
            if (stoneN != null)
            {
                IndoorTextureLoader.Configure(stoneN, linear: true);
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", stoneN);
                m.SetTextureScale("_BumpMap", Vector2.one);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.30f);
            return m;
        }

        /// <summary>[F2/F3] HDR 발광(ember/촛불) 머티리얼 — URP Unlit + HDR 색(F4 Bloom이 픽업).
        /// 절차 셰이더 검색 실패 시 null(호출부 스킵) — 프리미티브 금지 규약과 무관(머티리얼만).</summary>
        public static Material CreateEmberUnlit(Color hdrColor, string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = name };
            var c = new Color(hdrColor.r, hdrColor.g, hdrColor.b, 1f);
            m.color = c;                    // Unlit은 base color × 1 — HDR 양수로 과노출 유도
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>[F1] 짚단/약초 바닥 데칼 머티리얼 — floor_straw_decal(알파) 투명 Unlit.
        /// 렌더러 전용 데칼 쿼드 전용 — 파일 없으면 null.</summary>
        public static Material CreateStrawDecal()
        {
            if (!IndoorTextureLoader.HasFiles || IndoorTextureLoader.StrawDecal == null) return null;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = "IndoorHQ_StrawDecal" };
            var tex = IndoorTextureLoader.StrawDecal;
            IndoorTextureLoader.Configure(tex, linear: false);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            else m.mainTexture = tex;
            m.color = new Color(1f, 1f, 1f, 1f);
            // 투명 데칼 — 표면 타입/블렌드 수동 구성(Unlit transparent)
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);          // 1 = Transparent
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        // ==================================================================================================
        // [2026-10-10 H2/H5 — 탑다운 친화] 접지 블롭 섀도우 + 바닥 마모 스탬프
        // ==================================================================================================

        private static Texture2D _blobShadowTex;

        /// <summary>[H2] 블롭 섀도우 텍스처(64²) — 중심 검정 알파 0.38 → 가장자리 0. 캐시 1회.</summary>
        public static Texture2D GetBlobShadowTexture()
        {
            if (_blobShadowTex != null) return _blobShadowTex;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "IndoorBlobShadow" };
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = 0.38f * Mathf.Pow(1f - d, 1.5f);
                    pixels[y * size + x] = new Color(0f, 0f, 0f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _blobShadowTex = tex;
            return tex;
        }

        /// <summary>[H2] 블롭 섀도우 머티리얼 — 투명 Unlit. 실패 시 null(호출부 스킵).</summary>
        public static Material CreateBlobShadowMat()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = "IndoorHQ_BlobShadow" };
            var tex = GetBlobShadowTexture();
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            else m.mainTexture = tex;
            m.color = new Color(1f, 1f, 1f, 1f);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        private static Texture2D _flameGlowTex;

        /// <summary>[2026-10-11 사용자 요구 — 횃불 은은한 글로우] 방사형 웜 글로우 텍스처(128²) —
        /// 중심 알파 0.5 → 가장자리 0(제곱 폴오프, 은은한 광원 풀). 색은 머티리얼 틴트 담당. 캐시 1회.</summary>
        public static Texture2D GetFlameGlowTexture()
        {
            if (_flameGlowTex != null) return _flameGlowTex;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "IndoorFlameGlow" };
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a *= a;                 // 제곱 폴오프 — 부드러운 가장자리
                    a *= 0.5f;              // 최대 알파 0.5 — 발광판이 아닌 은은한 풀
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _flameGlowTex = tex;
            return tex;
        }

        /// <summary>[2026-10-11] 횃불 글로우 머티리얼 — 투명 Unlit + 가산 웜 블렌드(어두운 바닥 위 은은한 빛 풀).
        /// 실패 시 null(호출부 스킵).</summary>
        public static Material CreateFlameGlowMat()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = "IndoorHQ_FlameGlow" };
            var tex = GetFlameGlowTexture();
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            else m.mainTexture = tex;
            m.color = new Color(1f, 0.55f, 0.28f, 1f);   // 웜 횃불 틴트
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        private static Texture2D _floorWearTex;

        /// <summary>[H5] 바닥 마모 스탬프 텍스처(256²) — 어두운 얼룩+균열, 알파 최대 ~0.22(색 변화 없음). 캐시 1회.</summary>
        public static Texture2D GetFloorWearTexture()
        {
            if (_floorWearTex != null) return _floorWearTex;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "IndoorFloorWear" };
            var pixels = new Color[size * size];
            var rng = new System.Random(20261010);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                    float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    // 얼룩 — 중심부 여러 겹 사인 노이즈(어두운 때), 가장자리 페이드
                    float blotch = 0.5f + 0.5f * Mathf.Sin(x * 0.11f + 1.3f) * Mathf.Sin(y * 0.13f + 0.7f)
                                 + 0.25f * Mathf.Sin((x + y) * 0.09f);
                    float a = Mathf.Pow(1f - d, 1.7f) * Mathf.Clamp01(blotch) * 0.22f;
                    // 균열 — 얇은 사선 2줄
                    float crack1 = Mathf.Abs((x - y) % 97 - 48) / 48f;
                    float crack2 = Mathf.Abs((x * 2 - y) % 151 - 75) / 75f;
                    if (crack1 < 0.05f || crack2 < 0.04f) a = Mathf.Max(a, 0.10f * Mathf.Pow(1f - d, 1.2f));
                    float jitter = 0.9f + 0.2f * (float)rng.NextDouble();
                    pixels[y * size + x] = new Color(0f, 0f, 0f, Mathf.Clamp01(a * jitter));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _floorWearTex = tex;
            return tex;
        }

        /// <summary>[H5] 바닥 마모 스탬프 머티리얼 — 투명 Unlit. 실패 시 null.</summary>
        public static Material CreateFloorWearMat()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = "IndoorHQ_FloorWear" };
            var tex = GetFloorWearTexture();
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            else m.mainTexture = tex;
            m.color = new Color(1f, 1f, 1f, 1f);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }
    }
}