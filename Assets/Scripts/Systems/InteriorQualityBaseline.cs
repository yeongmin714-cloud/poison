using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [실내 고품질화 P1] 실내 공통 골격 계약 — 모든 실내 빌더가 공유하는 단일 소스.
    ///
    /// 기준(품질바 사진 9/20 판독값 + 사용자 분위기 통일 계약):
    ///   - 타일 모듈 1.0m 그리드(줄눈 포함 절차 텍스처 — UV = 미터 스케일)
    ///   - 벽 높이 2.5m 기준(현행 빌더별 예외 허용 — 성 홀 6m)
    ///   - 걸레받이 밴드 0.15m
    ///   - 조명 예산: 방당 ≤4, 로비 ≤6
    ///   - 가구 밀도 목표 0.40~0.50(방 바닥 면적 대비 — 통로 제외)
    ///   - 국가/영지 차등 = 팔레트·깃발·장식색만(구조·모듈 공유)
    ///
    /// 계약: 결정론 — 엔진 난수 미사용(정수 해시만), 텍스처 캐시 1회 생성.
    /// 선례: FarmPlotVisuals.GetOrCreateSoilTexture(절차 텍스처 단일소스)/CaveMaterialFactory(결정론 팩토리).
    /// </summary>
    public static class InteriorQualityBaseline
    {
        public const float TileSizeM = 1.0f;
        public const float WallHeightM = 2.5f;
        public const float BaseboardHeightM = 0.15f;
        public const int MaxLightsPerRoom = 4;
        public const int MaxLightsLobby = 6;
        public const float FurnitureDensityTargetMin = 0.40f;
        public const float FurnitureDensityTargetMax = 0.50f;

        // ── 결정론 문자열 해시(VillagePlacementSystem GetHash 선례 — Knuth subtractive) ──
        public static int StableHash(string s)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 16777619u;
            }
            return (int)hash;
        }

        // ── 절차 텍스처 캐시(키별 1회 생성 — 결정론) ──
        private static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();
        private static Shader _litShader;

        /// <summary>URP Lit 셰이더 — IndoorMaterialFactory 동일 폴백(Lit → Simple Lit).</summary>
        private static Shader GetLitShader()
        {
            if (_litShader == null) _litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_litShader == null) _litShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            return _litShader;
        }

        /// <summary>석재 타일 바닥 머티리얼 — 1m 타일 1장(256²) + 줄눈 + 미세 스펙클. UV 타일링은 쿼드 쪽에서 미터 배수로.</summary>
        public static Material GetFloorTileMat(string key, Color tile, Color grout)
        {
            string cacheKey = $"floorTile_{key}";
            if (_matCache.TryGetValue(cacheKey, out var cached)) return cached;

            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = cacheKey };
            int groutPx = Mathf.Max(2, (int)(size * 0.045f));
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < groutPx || y < groutPx;
                    Color c;
                    if (edge) c = grout;
                    else
                    {
                        // 타일 면 — 해시 스펙클(결정론) + 중심 살짝 밝게
                        int h = StableHash($"{x}_{y}_{key}");
                        float speckle = ((h & 0xFF) / 255f - 0.5f) * 0.06f;
                        c = tile + new Color(speckle, speckle, speckle, 0f);
                    }
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            var mat = new Material(GetLitShader()) { name = cacheKey, mainTexture = tex };
            _matCache[cacheKey] = mat;
            return mat;
        }

        /// <summary>[무번호 32] 영지 실내 공통 석타일 — 전 실내 빌더 단일 소스(무번호 15 성 석타일 팔레트 승계,
        ///   국가 키 폐기 — "같은 씬 내 방/구역 타일 통일" 사용자 계약). 텍스처=1m 타일 1장.</summary>
        public static Material GetCommonInteriorFloorMat()
        {
            return GetFloorTileMat("castleStone", new Color(0.38f, 0.37f, 0.35f), new Color(0.26f, 0.25f, 0.24f));
        }

        /// <summary>[무번호 32] 크기별 인스턴스 — 공유 베이스 복제 + mainTextureScale=(w,d).
        ///   IndoorBuilder.CreateRoom/프리미티브 바닥은 UV 0..1 전체 스팬이라 미터 배수 스케일로 1m 타일 정합.
        ///   캐시 공유 인스턴스를 직접 변조하지 않는다(빌더별 크기 상이 — 오염 방지).</summary>
        public static Material CreateCommonInteriorFloorMat(float widthMeters, float depthMeters, string owner)
        {
            var m = new Material(GetCommonInteriorFloorMat()) { name = $"CommonStoneFloor_{owner}" };
            m.mainTextureScale = new Vector2(Mathf.Max(1f, widthMeters), Mathf.Max(1f, depthMeters));
            return m;
        }

        /// <summary>목재 판자 바닥 머티리얼 — 가로 판자(256² 내 4판) + 결 스펙클.</summary>
        public static Material GetWoodFloorMat(string key, Color wood)
        {
            string cacheKey = $"woodFloor_{key}";
            if (_matCache.TryGetValue(cacheKey, out var cached)) return cached;

            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = cacheKey };
            var px = new Color[size * size];
            Color seam = wood * 0.72f;
            for (int y = 0; y < size; y++)
            {
                bool seamRow = (y % (size / 4)) < 2;
                for (int x = 0; x < size; x++)
                {
                    int h = StableHash($"{x}_{y}_{key}");
                    float grain = Mathf.Sin((x + (h & 0x1F)) * 0.35f) * 0.03f;
                    Color c = seamRow ? seam : wood + new Color(grain, grain, grain, 0f);
                    px[y * size + x] = c;
                }
            }
            var tex2 = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = cacheKey };
            tex2.SetPixels(px);
            tex2.Apply();
            var mat = new Material(GetLitShader()) { name = cacheKey, mainTexture = tex2 };
            _matCache[cacheKey] = mat;
            return mat;
        }

        /// <summary>러그 머티리얼 — 테두리 밴드 + 중앙 단색(설계도 붉은 카펫 패턴).</summary>
        public static Material GetRugMat(string key, Color base_, Color border)
        {
            string cacheKey = $"rug_{key}";
            if (_matCache.TryGetValue(cacheKey, out var cached)) return cached;

            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = cacheKey };
            var px = new Color[size * size];
            int band = (int)(size * 0.08f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < band || y < band || x >= size - band || y >= size - band;
                    px[y * size + x] = isBorder ? border : base_;
                }
            }
            var tex2 = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = cacheKey };
            tex2.SetPixels(px);
            tex2.Apply();
            var mat = new Material(GetLitShader()) { name = cacheKey, mainTexture = tex2 };
            _matCache[cacheKey] = mat;
            return mat;
        }

        /// <summary>걸레받이/어두운 트림 머티리얼.</summary>
        public static Material GetTrimMat(string key, Color tone)
        {
            string cacheKey = $"trim_{key}";
            if (_matCache.TryGetValue(cacheKey, out var cached)) return cached;
            var mat = new Material(GetLitShader()) { name = cacheKey };
            mat.color = tone;
            _matCache[cacheKey] = mat;
            return mat;
        }

        // ── 지오메트리 헬퍼(로컬 좌표 — parent 기준) ──

        /// <summary>바닥 오버레이 쿼드 — UV를 미터/타일 모듈로 스케일(1m 그리드 실현).
        ///   기존 바닥 위 얇게(yOffset) — 기존 지오메트리 무수정 계약(데칼 선례).</summary>
        public static GameObject AddFloorOverlay(GameObject parent, string name, Vector3 center,
            float sizeX, float sizeZ, Material mat, float yOffset = 0.02f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = center + new Vector3(0f, yOffset, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            var mesh = new Mesh { name = name + "_Mesh" };
            float hx = sizeX * 0.5f, hz = sizeZ * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hx, -hz, 0f), new Vector3(hx, -hz, 0f),
                new Vector3(-hx,  hz, 0f), new Vector3(hx,  hz, 0f),
            };
            // UV = 미터/타일 — 타일 텍스처가 1m당 1회 반복
            float ux = sizeX / TileSizeM, uz = sizeZ / TileSizeM;
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(ux, 0f),
                new Vector2(0f, uz), new Vector2(ux, uz),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>러그 쿼드 — 바닥 오버레이와 동일 기하(머티리얼만 러그).</summary>
        public static GameObject AddRug(GameObject parent, string name, Vector3 center,
            float sizeX, float sizeZ, Material mat) =>
            AddFloorOverlay(parent, name, center, sizeX, sizeZ, mat, 0.025f);

        /// <summary>절차 침대 — 목재 프레임+다리+매트리스+베개+이불(침대 GLB 부재 대체 — 상향 교체 여지 기록).</summary>
        public static GameObject BuildBed(Transform parent, string name, Vector3 localPos, float yawDeg,
            Material woodMat, Material mattressMat, Color blanketColor)
        {
            var bed = new GameObject(name);
            if (parent != null) bed.transform.SetParent(parent, false);
            bed.transform.localPosition = localPos;
            bed.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            var blanket = GetTrimMat($"{name}_blanket", blanketColor);

            void Box(string childName, Vector3 size, Vector3 pos, Material mat)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(bed.transform, false);
                go.transform.localPosition = pos;
                var mf = go.AddComponent<MeshFilter>();
                var mesh = new Mesh { name = childName + "_Mesh" };
                float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
                mesh.vertices = new[]
                {
                    new Vector3(-hx,-hy,-hz), new Vector3( hx,-hy,-hz), new Vector3( hx,-hy, hz), new Vector3(-hx,-hy, hz),
                    new Vector3(-hx, hy,-hz), new Vector3( hx, hy,-hz), new Vector3( hx, hy, hz), new Vector3(-hx, hy, hz),
                };
                mesh.triangles = new[]
                {
                    0,2,1, 0,3,2,  4,5,6, 4,6,7,
                    0,1,5, 0,5,4,  1,2,6, 1,6,5,
                    2,3,7, 2,7,6,  3,0,4, 3,4,7,
                };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
            }

            // 프레임+머리판(침대 로컬 +z가 머리 방향)
            Box("Bed_Frame", new Vector3(1.05f, 0.30f, 2.05f), new Vector3(0f, 0.20f, 0f), woodMat);
            Box("Bed_Headboard", new Vector3(1.05f, 0.55f, 0.08f), new Vector3(0f, 0.48f, 1.00f), woodMat);
            Box("Bed_Mattress", new Vector3(0.92f, 0.16f, 1.90f), new Vector3(0f, 0.43f, -0.02f), mattressMat);
            Box("Bed_Pillow", new Vector3(0.60f, 0.10f, 0.32f), new Vector3(0f, 0.54f, 0.72f), mattressMat);
            Box("Bed_Blanket", new Vector3(0.96f, 0.07f, 1.25f), new Vector3(0f, 0.50f, -0.28f), blanket);
            Box("Bed_LegFL", new Vector3(0.08f, 0.16f, 0.08f), new Vector3(-0.44f, 0.06f, -0.92f), woodMat);
            Box("Bed_LegFR", new Vector3(0.08f, 0.16f, 0.08f), new Vector3( 0.44f, 0.06f, -0.92f), woodMat);
            Box("Bed_LegBL", new Vector3(0.08f, 0.16f, 0.08f), new Vector3(-0.44f, 0.06f,  0.92f), woodMat);
            Box("Bed_LegBR", new Vector3(0.08f, 0.16f, 0.08f), new Vector3( 0.44f, 0.06f,  0.92f), woodMat);
            return bed;
        }

        /// <summary>상자 적치 — 1~3단 적치(결정론 해시 — 창고/제작실 소품).</summary>
        public static GameObject BuildCrateStack(Transform parent, string name, Vector3 localPos, int seed,
            Material woodMat)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            int layers = 1 + ((seed % 3) + 3) % 3;   // 1..3
            float yaw = (seed % 4) * 90f + ((seed >> 2) % 15f);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float y = 0f;
            for (int i = 0; i < layers; i++)
            {
                float side = 0.7f - i * 0.08f;
                var box = new GameObject($"{name}_Box{i}");
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = new Vector3(0f, y + side * 0.5f, 0f);
                box.transform.localRotation = Quaternion.Euler(0f, i * 11f, 0f);
                var mf = box.AddComponent<MeshFilter>();
                var mesh = new Mesh { name = box.name + "_Mesh" };
                float h = side * 0.5f;
                mesh.vertices = new[]
                {
                    new Vector3(-h,-h,-h), new Vector3( h,-h,-h), new Vector3( h,-h, h), new Vector3(-h,-h, h),
                    new Vector3(-h, h,-h), new Vector3( h, h,-h), new Vector3( h, h, h), new Vector3(-h, h, h),
                };
                mesh.triangles = new[]
                {
                    0,2,1, 0,3,2,  4,5,6, 4,6,7,
                    0,1,5, 0,5,4,  1,2,6, 1,6,5,
                    2,3,7, 2,7,6,  3,0,4, 3,4,7,
                };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mf.sharedMesh = mesh;
                var mr = box.AddComponent<MeshRenderer>();
                mr.sharedMaterial = woodMat;
                y += side;
            }
            return root;
        }

        /// <summary>공용 박스 — 중심+스케일+머티리얼(트림/밴드/캡 공용 — 12면 큐브).</summary>
        public static GameObject AddBox(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            var mesh = new Mesh { name = name + "_Mesh" };
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hx,-hy,-hz), new Vector3( hx,-hy,-hz), new Vector3( hx,-hy, hz), new Vector3(-hx,-hy, hz),
                new Vector3(-hx, hy,-hz), new Vector3( hx, hy,-hz), new Vector3( hx, hy, hz), new Vector3(-hx, hy, hz),
            };
            mesh.triangles = new[]
            {
                0,2,1, 0,3,2,  4,5,6, 4,6,7,
                0,1,5, 0,5,4,  1,2,6, 1,6,5,
                2,3,7, 2,7,6,  3,0,4, 3,4,7,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        // ── 원목 가구 팩토리 — 팩 가구가 유색(쿠션/체크보)이라 원목 전용 요구 대체(사용자 지시 2026-11-07) ──

        /// <summary>원목 테이블 — 상판(목재 판자 텍스처)+다리 4+하부 받침대. 상판 위(y=tableH)에 소품 얹기 가능.</summary>
        public static GameObject BuildWoodTable(Transform parent, string name, Vector3 localPos, float yawDeg,
            float sizeX = 1.4f, float sizeZ = 0.9f, float tableH = 0.76f)
        {
            var woodMat = GetWoodFloorMat("furnitWood", new Color(0.44f, 0.31f, 0.19f));
            var table = new GameObject(name);
            if (parent != null) table.transform.SetParent(parent, false);
            table.transform.localPosition = localPos;
            table.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            AddBox(table.transform, $"{name}_Top", new Vector3(0f, tableH, 0f),
                new Vector3(sizeX, 0.06f, sizeZ), woodMat);
            float lx = sizeX * 0.5f - 0.09f, lz = sizeZ * 0.5f - 0.09f;
            AddBox(table.transform, $"{name}_LegFL", new Vector3(-lx, tableH * 0.5f, -lz), new Vector3(0.07f, tableH, 0.07f), woodMat);
            AddBox(table.transform, $"{name}_LegFR", new Vector3( lx, tableH * 0.5f, -lz), new Vector3(0.07f, tableH, 0.07f), woodMat);
            AddBox(table.transform, $"{name}_LegBL", new Vector3(-lx, tableH * 0.5f,  lz), new Vector3(0.07f, tableH, 0.07f), woodMat);
            AddBox(table.transform, $"{name}_LegBR", new Vector3( lx, tableH * 0.5f,  lz), new Vector3(0.07f, tableH, 0.07f), woodMat);
            AddBox(table.transform, $"{name}_Shelf", new Vector3(0f, tableH * 0.35f, 0f),
                new Vector3(sizeX - 0.24f, 0.04f, sizeZ - 0.2f), woodMat);
            return table;
        }

        /// <summary>원목 의자 — 좌판+다리 4+등받이(무쿠션 — 나무 원색만).</summary>
        public static GameObject BuildWoodChair(Transform parent, string name, Vector3 localPos, float yawDeg)
        {
            var woodMat = GetWoodFloorMat("furnitWood", new Color(0.44f, 0.31f, 0.19f));
            var chair = new GameObject(name);
            if (parent != null) chair.transform.SetParent(parent, false);
            chair.transform.localPosition = localPos;
            chair.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            AddBox(chair.transform, $"{name}_Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.05f, 0.42f), woodMat);
            float lx = 0.17f, lz = 0.17f;
            AddBox(chair.transform, $"{name}_LegFL", new Vector3(-lx, 0.225f, -lz), new Vector3(0.05f, 0.45f, 0.05f), woodMat);
            AddBox(chair.transform, $"{name}_LegFR", new Vector3( lx, 0.225f, -lz), new Vector3(0.05f, 0.45f, 0.05f), woodMat);
            AddBox(chair.transform, $"{name}_LegBL", new Vector3(-lx, 0.225f,  lz), new Vector3(0.05f, 0.45f, 0.05f), woodMat);
            AddBox(chair.transform, $"{name}_LegBR", new Vector3( lx, 0.225f,  lz), new Vector3(0.05f, 0.45f, 0.05f), woodMat);
            AddBox(chair.transform, $"{name}_Back", new Vector3(0f, 0.72f, 0.19f), new Vector3(0.40f, 0.50f, 0.05f), woodMat);
            AddBox(chair.transform, $"{name}_BackRail", new Vector3(0f, 0.95f, 0.19f), new Vector3(0.40f, 0.06f, 0.06f), woodMat);
            return chair;
        }

        /// <summary>원목 스툴 — 등받이 없는 좌판+다리 4.</summary>
        public static GameObject BuildWoodStool(Transform parent, string name, Vector3 localPos, float yawDeg)
        {
            var woodMat = GetWoodFloorMat("furnitWood", new Color(0.44f, 0.31f, 0.19f));
            var stool = new GameObject(name);
            if (parent != null) stool.transform.SetParent(parent, false);
            stool.transform.localPosition = localPos;
            stool.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            AddBox(stool.transform, $"{name}_Seat", new Vector3(0f, 0.48f, 0f), new Vector3(0.38f, 0.05f, 0.38f), woodMat);
            float lx = 0.15f, lz = 0.15f;
            AddBox(stool.transform, $"{name}_LegFL", new Vector3(-lx, 0.24f, -lz), new Vector3(0.05f, 0.48f, 0.05f), woodMat);
            AddBox(stool.transform, $"{name}_LegFR", new Vector3( lx, 0.24f, -lz), new Vector3(0.05f, 0.48f, 0.05f), woodMat);
            AddBox(stool.transform, $"{name}_LegBL", new Vector3(-lx, 0.24f,  lz), new Vector3(0.05f, 0.48f, 0.05f), woodMat);
            AddBox(stool.transform, $"{name}_LegBR", new Vector3( lx, 0.24f,  lz), new Vector3(0.05f, 0.48f, 0.05f), woodMat);
            return stool;
        }

        /// <summary>방 바닥 면적 대비 가구 발률측정 — 배치 기록 기반(계약테스트/보고용 순수 계산).</summary>
        public static float EstimateFurnitureFootprintRatio(float roomAreaM2, int furnitureCount, float avgFootprintM2 = 1.4f)
        {
            if (roomAreaM2 <= 0f) return 0f;
            return Mathf.Clamp01(furnitureCount * avgFootprintM2 / roomAreaM2);
        }
    }
}
