using System;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C11-06: 실내 가구 배치 생성기.
    /// 모든 가구는 PrimitiveType.Cube 기반 Mesh로 생성.
    /// [2026-10-14 폴백 품질 패스] 폴백(프리미티브) 경로 전용 정제 디테일 — 베벨 트림/에이프런/
    /// 철 코너/탁상 소품. 장식은 본 파일 폴백 경로에서만 생성되므로 GLB 카탈로그 원본에는
    /// 절대 혼입되지 않는다. 공개 시그니처·루트 이름·치수·피벗·침대 상호작용 계약 불변.
    /// 탑다운 규약: 천장/매달림 없음, 바닥 접지 오브제 없음, 장식은 렌더러 전용(콜라이더 없음).
    /// </summary>
    public static class IndoorFurniturePlacer
    {
        // ===================================================================
        // 유효성 검사 헬퍼
        // ===================================================================

        private static void ValidatePositive(float value, string paramName)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException($"{paramName} must be positive (> 0), but got {value}.", paramName);
        }

        private static Material EnsureMaterial(Material mat, string name)
        {
            if (mat != null) return mat;
            var fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = $"{name}_DefaultMat"
            };
            fallback.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f));
            return fallback;
        }

        // ===================================================================
        // 테이블
        // ===================================================================

        /// <summary>
        /// 테이블 생성 (상판 + 다리 4개).
        /// </summary>
        /// <param name="width">테이블 상판 폭 (meter, > 0).</param>
        /// <param name="depth">테이블 상판 깊이 (meter, > 0).</param>
        /// <param name="height">테이블 전체 높이 (meter, > 0).</param>
        /// <param name="mat">머티리얼 (null 시 기본 Lit 생성).</param>
        public static GameObject CreateTable(float width, float depth, float height, Material mat)
        {
            ValidatePositive(width, nameof(width));
            ValidatePositive(depth, nameof(depth));
            ValidatePositive(height, nameof(height));

            mat = EnsureMaterial(mat, "Table");
            GameObject table = new GameObject("Table");

            // 상판
            float topThickness = 0.08f;
            GameObject top = CreateBox(table, "TableTop", new Vector3(width, topThickness, depth),
                new Vector3(0, height - topThickness * 0.5f, 0), mat);

            // 다리 4개
            float legThickness = 0.06f;
            float legHeight = height - topThickness;
            float legOffsetX = width * 0.5f - legThickness;
            float legOffsetZ = depth * 0.5f - legThickness;
            Vector3 legSize = new Vector3(legThickness, legHeight, legThickness);

            CreateBox(table, "Leg_FrontLeft", legSize, new Vector3(-legOffsetX, legHeight * 0.5f, -legOffsetZ), mat);
            CreateBox(table, "Leg_FrontRight", legSize, new Vector3(legOffsetX, legHeight * 0.5f, -legOffsetZ), mat);
            CreateBox(table, "Leg_BackLeft", legSize, new Vector3(-legOffsetX, legHeight * 0.5f, legOffsetZ), mat);
            CreateBox(table, "Leg_BackRight", legSize, new Vector3(legOffsetX, legHeight * 0.5f, legOffsetZ), mat);

            // [폴백 품질 패스] 베벨 트림/에이프런/철 코너/탁상 소품 (렌더러 전용, 치수 불변)
            DecorateTable(table, width, depth, height, mat);

            return table;
        }

        // ===================================================================
        // 의자
        // ===================================================================

        /// <summary>
        /// 의자 생성 (좌석 + 등받이 + 다리 4개).
        /// </summary>
        /// <param name="height">의자 전체 높이 (meter, > 0).</param>
        /// <param name="mat">머티리얼 (null 시 기본 Lit 생성).</param>
        public static GameObject CreateChair(float height, Material mat)
        {
            ValidatePositive(height, nameof(height));
            mat = EnsureMaterial(mat, "Chair");
            GameObject chair = new GameObject("Chair");

            float seatThickness = 0.05f;
            float seatWidth = 0.45f;
            float seatDepth = 0.45f;
            float seatY = height * 0.45f;

            // 좌석
            CreateBox(chair, "Seat", new Vector3(seatWidth, seatThickness, seatDepth),
                new Vector3(0, seatY, 0), mat);

            // 등받이
            float backHeight = height * 0.4f;
            float backThickness = 0.04f;
            CreateBox(chair, "Backrest", new Vector3(seatWidth, backHeight, backThickness),
                new Vector3(0, seatY + seatThickness * 0.5f + backHeight * 0.5f, -seatDepth * 0.5f + 0.02f), mat);

            // 다리 4개
            float legThickness = 0.04f;
            float legHeight = seatY - seatThickness * 0.5f;
            float legOffset = 0.18f;

            CreateBox(chair, "Leg_FrontLeft", new Vector3(legThickness, legHeight, legThickness),
                new Vector3(-legOffset, legHeight * 0.5f, -legOffset), mat);
            CreateBox(chair, "Leg_FrontRight", new Vector3(legThickness, legHeight, legThickness),
                new Vector3(legOffset, legHeight * 0.5f, -legOffset), mat);
            CreateBox(chair, "Leg_BackLeft", new Vector3(legThickness, legHeight, legThickness),
                new Vector3(-legOffset, legHeight * 0.5f, legOffset), mat);
            CreateBox(chair, "Leg_BackRight", new Vector3(legThickness, legHeight, legThickness),
                new Vector3(legOffset, legHeight * 0.5f, legOffset), mat);

            // [폴백 품질 패스] 쿠션/등받이 상단 밴드/스트레처 (렌더러 전용, 치수 불변)
            DecorateChair(chair, height, mat);

            return chair;
        }

        // ===================================================================
        // 선반
        // ===================================================================

        /// <summary>
        /// 선반 생성 (기둥 4개 + 선반판 N개).
        /// </summary>
        /// <param name="width">선반 전체 폭 (meter, > 0).</param>
        /// <param name="height">선반 전체 높이 (meter, > 0).</param>
        /// <param name="depth">선반 전체 깊이 (meter, > 0).</param>
        /// <param name="mat">머티리얼 (null 시 기본 Lit 생성).</param>
        /// <param name="shelves">선반판 개수 (기본 3, 최소 1).</param>
        /// <param name="detailed">정제 디테일(책 줄/병) — 상자·거울 폴백은 false(패널/캡은 유지).</param>
        public static GameObject CreateShelf(float width, float height, float depth, Material mat, int shelves = 3, bool detailed = true)
        {
            ValidatePositive(width, nameof(width));
            ValidatePositive(height, nameof(height));
            ValidatePositive(depth, nameof(depth));
            mat = EnsureMaterial(mat, "Shelf");
            GameObject shelf = new GameObject("Shelf");

            float postThickness = 0.05f;
            float shelfThickness = 0.04f;
            int actualShelves = Mathf.Max(1, shelves);

            // 기둥 4개
            float postOffsetX = width * 0.5f - postThickness * 0.5f;
            float postOffsetZ = depth * 0.5f - postThickness * 0.5f;
            Vector3 postSize = new Vector3(postThickness, height, postThickness);

            CreateBox(shelf, "Post_FL", postSize,
                new Vector3(-postOffsetX, height * 0.5f, -postOffsetZ), mat);
            CreateBox(shelf, "Post_FR", postSize,
                new Vector3(postOffsetX, height * 0.5f, -postOffsetZ), mat);
            CreateBox(shelf, "Post_BL", postSize,
                new Vector3(-postOffsetX, height * 0.5f, postOffsetZ), mat);
            CreateBox(shelf, "Post_BR", postSize,
                new Vector3(postOffsetX, height * 0.5f, postOffsetZ), mat);

            // 선반판
            float shelfAreaWidth = width - postThickness * 2f;
            float shelfAreaDepth = depth - postThickness * 2f;
            float spacing = height / (actualShelves + 1);

            for (int i = 0; i < actualShelves; i++)
            {
                float yPos = spacing * (i + 1);
                CreateBox(shelf, $"Shelf_{i + 1}",
                    new Vector3(shelfAreaWidth, shelfThickness, shelfAreaDepth),
                    new Vector3(0, yPos - shelfThickness * 0.5f, 0), mat);
            }

            // [폴백 품질 패스] 후면 패널/상단 캡/책 줄 (렌더러 전용, 치수 불변)
            DecorateShelf(shelf, width, height, depth, mat, actualShelves, detailed);

            return shelf;
        }

        // ===================================================================
        // 카운터/데스크
        // ===================================================================

        /// <summary>
        /// 카운터/데스크 생성.
        /// </summary>
        /// <param name="width">카운터 폭 (meter, > 0).</param>
        /// <param name="height">카운터 전체 높이 (meter, > 0).</param>
        /// <param name="depth">카운터 깊이 (meter, > 0).</param>
        /// <param name="mat">머티리얼 (null 시 기본 Lit 생성).</param>
        /// <param name="detailed">정제 디테일(서랍 패이스/손잡이) — 상자 폴백은 false(트림/코너는 유지).</param>
        public static GameObject CreateCounter(float width, float height, float depth, Material mat, bool detailed = true)
        {
            ValidatePositive(width, nameof(width));
            ValidatePositive(height, nameof(height));
            ValidatePositive(depth, nameof(depth));
            mat = EnsureMaterial(mat, "Counter");
            GameObject counter = new GameObject("Counter");

            // 상판
            float topThickness = 0.06f;
            CreateBox(counter, "CounterTop", new Vector3(width, topThickness, depth),
                new Vector3(0, height - topThickness * 0.5f, 0), mat);

            // 앞면 패널 (얇음)
            float panelThickness = 0.03f;
            float panelHeight = height - topThickness;
            CreateBox(counter, "FrontPanel", new Vector3(width, panelHeight, panelThickness),
                new Vector3(0, panelHeight * 0.5f, depth * 0.5f - panelThickness * 0.5f), mat);

            // 측면 패널 (좌/우)
            float sideThickness = 0.03f;
            CreateBox(counter, "LeftPanel", new Vector3(sideThickness, panelHeight, depth),
                new Vector3(-width * 0.5f + sideThickness * 0.5f, panelHeight * 0.5f, 0), mat);
            CreateBox(counter, "RightPanel", new Vector3(sideThickness, panelHeight, depth),
                new Vector3(width * 0.5f - sideThickness * 0.5f, panelHeight * 0.5f, 0), mat);

            // [폴백 품질 패스] 트림/철 코너/서랍 (렌더러 전용, 치수 불변)
            DecorateCounter(counter, width, height, depth, mat, detailed);

            return counter;
        }

        // ===================================================================
        // 침대
        // ===================================================================

        /// <summary>
        /// 침대 생성 (매트리스 + 프레임 + 베개).
        /// </summary>
        /// <param name="width">침대 폭 (meter, > 0).</param>
        /// <param name="depth">침대 깊이 (meter, > 0).</param>
        /// <param name="mat">머티리얼 (null 시 기본 Lit 생성).</param>
        public static GameObject CreateBed(float width, float depth, Material mat)
        {
            ValidatePositive(width, nameof(width));
            ValidatePositive(depth, nameof(depth));
            mat = EnsureMaterial(mat, "Bed");
            GameObject bed = new GameObject("Bed");

            float mattressHeight = 0.2f;
            float frameHeight = 0.3f;
            float frameThickness = 0.05f;

            // 매트리스
            CreateBox(bed, "Mattress", new Vector3(width, mattressHeight, depth),
                new Vector3(0, frameHeight + mattressHeight * 0.5f, 0), mat);

            // 프레임 (테두리)
            // 앞
            CreateBox(bed, "Frame_Front", new Vector3(width, frameHeight, frameThickness),
                new Vector3(0, frameHeight * 0.5f, depth * 0.5f - frameThickness * 0.5f), mat);
            // 뒤
            CreateBox(bed, "Frame_Back", new Vector3(width, frameHeight, frameThickness),
                new Vector3(0, frameHeight * 0.5f, -depth * 0.5f + frameThickness * 0.5f), mat);
            // 좌
            CreateBox(bed, "Frame_Left", new Vector3(frameThickness, frameHeight, depth - frameThickness * 2f),
                new Vector3(-width * 0.5f + frameThickness * 0.5f, frameHeight * 0.5f, 0), mat);
            // 우
            CreateBox(bed, "Frame_Right", new Vector3(frameThickness, frameHeight, depth - frameThickness * 2f),
                new Vector3(width * 0.5f - frameThickness * 0.5f, frameHeight * 0.5f, 0), mat);

            // 베개 (간단히 작은 박스)
            float pillowWidth = width * 0.25f;
            float pillowDepth = depth * 0.2f;
            float pillowHeight = 0.08f;
            CreateBox(bed, "Pillow", new Vector3(pillowWidth, pillowHeight, pillowDepth),
                new Vector3(0, frameHeight + mattressHeight + pillowHeight * 0.5f, -depth * 0.4f), mat);

            // 상호작용: root에 Bed 컴포넌트(+요구 BoxCollider isTrigger) 부착 → E키 상호작용 가능.
            // C16-02: PlayerMovement.HandleInteraction()이 Physics.OverlapSphere로 이 콜라이더를 감지.
            // 중복 부착 방지 — 이미 Bed가 있으면 스킵 (재초기화/재사용 경로 대비).
            BoxCollider bedCollider = bed.GetComponent<BoxCollider>();
            if (bedCollider == null)
            {
                bedCollider = bed.AddComponent<BoxCollider>(); // Bed.RequireComponent 요구 충족
            }
            bedCollider.isTrigger = true;
            // 감지 범위: 매트리스+프레임+베개를 덮도록 (높이 0.6, 중심 y=0.3)
            bedCollider.center = new Vector3(0f, 0.3f, 0f);
            bedCollider.size = new Vector3(width, 0.6f, depth);

            if (bed.GetComponent<Bed>() == null)
            {
                bed.AddComponent<Bed>();
            }

            // [폴백 품질 패스] 이불/접힘 띠/프레임 철 코너 — 상호작용 콜라이더 계약 불변
            DecorateBed(bed, width, depth, mat);

            return bed;
        }

        // ===================================================================
        // 유틸리티
        // ===================================================================

        /// <summary>
        /// PrimitiveType.Cube 기반 상자 생성.
        /// Cube 프리미티브(1×1×1)를 생성하고 localScale로 크기를 설정.
        /// BoxCollider는 유지되어 물리적 존재감을 제공.
        /// </summary>
        /// <param name="parent">부모 Transform을 가진 GameObject.</param>
        /// <param name="name">자식 오브젝트 이름.</param>
        /// <param name="size">월드 스케일 (x/y/z meter).</param>
        /// <param name="localPosition">부모 기준 로컬 위치.</param>
        /// <param name="mat">할당할 머티리얼 (null 허용, EnsureMaterial로 처리됨).</param>
        /// <returns>생성된 Cube GameObject.</returns>
        private static GameObject CreateBox(GameObject parent, string name, Vector3 size, Vector3 localPosition, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }

            // 콜라이더 유지 (물리적 존재감)
            return go;
        }

        // ===================================================================
        // [2026-10-14 폴백 품질 패스] 정제 디테일 — 폴백(프리미티브) 전용.
        // 스타일: 다크 참나무 구조 + 무광 철 코너 + 클롯(적색) 직물 소품.
        // 규약: 장식 자식은 렌더러 전용(콜라이더 없음), 천장/매달림/바닥 접지 없음,
        //        모든 치수는 파라미터 파생(빌더 ×2.45 스케일 패스와 무관하게 정합),
        //        표면 접촉부는 0.5~2mm 침입 배치(z-fighting 없음). 결정론 배치(랜덤 없음).
        // ===================================================================

        private enum AccentPalette { DarkWood, Iron, ClothRed, Parchment, BookOlive, BookSlate, GlassGreen }

        private static readonly Dictionary<AccentPalette, Material> AccentCache = new Dictionary<AccentPalette, Material>();

        /// <summary>팔레트 악센트 머티리얼(정적 캐시). 생성 실패 시 베이스 머티리얼 폴백.</summary>
        private static Material Accent(AccentPalette palette, Material baseMat)
        {
            if (AccentCache.TryGetValue(palette, out Material cached) && cached != null) return cached;
            Material m = CreatePaletteMaterial(palette);
            if (m == null) return baseMat;
            AccentCache[palette] = m;
            return m;
        }

        private static Material CreatePaletteMaterial(AccentPalette palette)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (lit == null) return null;
            var m = new Material(lit) { name = "FurnitureAccent_" + palette };
            Color c;
            float smoothness = 0.25f, metallic = 0f;
            switch (palette)
            {
                case AccentPalette.DarkWood:   c = new Color(0.24f, 0.16f, 0.10f); break;
                case AccentPalette.Iron:       c = new Color(0.16f, 0.16f, 0.18f); smoothness = 0.55f; metallic = 0.6f; break;
                case AccentPalette.ClothRed:   c = new Color(0.42f, 0.15f, 0.13f); smoothness = 0.12f; break;
                case AccentPalette.Parchment:  c = new Color(0.85f, 0.79f, 0.66f); break;
                case AccentPalette.BookOlive:  c = new Color(0.20f, 0.27f, 0.18f); break;
                case AccentPalette.BookSlate:  c = new Color(0.17f, 0.22f, 0.30f); break;
                case AccentPalette.GlassGreen: c = new Color(0.13f, 0.21f, 0.16f); smoothness = 0.7f; break;
                default:                       c = new Color(0.5f, 0.5f, 0.5f); break;
            }
            m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }

        /// <summary>결정론 퍼즈(시드 기반 0~1) — 책 크기 변주. Unity.Random 미사용(플래키 방지).</summary>
        private static float StableNoise(int seed)
        {
            uint x = (uint)seed * 2654435761u ^ 0x9E3779B9u;
            x ^= x >> 15; x *= 0x2C1B3C6Du; x ^= x >> 12; x *= 0x297A2D39u; x ^= x >> 15;
            return (x & 0xFFFFFFu) / 16777215f;
        }

        /// <summary>장식 전용 박스 — 렌더러 전용(콜라이더 즉시 제거). "장식에 추가 콜라이더 없음" 규약.</summary>
        private static GameObject CreateDetailBox(GameObject parent, string name, Vector3 size, Vector3 localPosition, Material mat)
        {
            GameObject go = CreateBox(parent, name, size, localPosition, mat);
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(col);
                else UnityEngine.Object.DestroyImmediate(col);
            }
            return go;
        }

        /// <summary>테이블 폴백 디테일 — 트림/에이프런/철 코너/탁상 소품(넉넉한 탁상 한정, 코너 오프셋 배치).</summary>
        private static void DecorateTable(GameObject table, float width, float depth, float height, Material mat)
        {
            float topThickness = 0.08f;
            float topY = height - topThickness;     // 상판 밑면 y
            Material wood = Accent(AccentPalette.DarkWood, mat);
            Material iron = Accent(AccentPalette.Iron, mat);

            // 1) 상판 하단 리빌 트림(베벨 라인) — 상판 밑면 2mm 아래(평면충돌 없음).
            if (height > topThickness + 0.06f)
                CreateDetailBox(table, "TopTrim", new Vector3(width - 0.015f, 0.02f, depth - 0.015f),
                    new Vector3(0f, topY - 0.012f, 0f), wood);

            // 2) 다리 사이 에이프런 — 끝단은 다리 중심까지 들어가 숨김(평면충돌 없음).
            if (width > 0.7f && depth > 0.5f)
            {
                float legThickness = 0.06f;
                float apronH = 0.07f;
                float apronCy = topY - 0.024f - apronH * 0.5f;
                float spanX = Mathf.Max(0.05f, width - legThickness * 2f);
                float spanZ = Mathf.Max(0.05f, depth - legThickness * 2f);
                CreateDetailBox(table, "Apron_Front", new Vector3(spanX, apronH, 0.035f),
                    new Vector3(0f, apronCy, -(depth * 0.5f - legThickness)), wood);
                CreateDetailBox(table, "Apron_Back", new Vector3(spanX, apronH, 0.035f),
                    new Vector3(0f, apronCy, depth * 0.5f - legThickness), wood);
                CreateDetailBox(table, "Apron_Left", new Vector3(0.035f, apronH, spanZ),
                    new Vector3(-(width * 0.5f - legThickness), apronCy, 0f), wood);
                CreateDetailBox(table, "Apron_Right", new Vector3(0.035f, apronH, spanZ),
                    new Vector3(width * 0.5f - legThickness, apronCy, 0f), wood);
            }

            // 3) 상판 귀퉁이 철 플레이트(탑다운 식별 악센트) — 상판 면 1mm 침입 배치.
            if (width > 0.6f && depth > 0.6f)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        string corner = "CornerPlate_" + (sx < 0 ? "F" : "B") + (sz < 0 ? "L" : "R");
                        CreateDetailBox(table, corner, new Vector3(0.07f, 0.006f, 0.07f),
                            new Vector3(sx * (width * 0.5f - 0.045f), height + 0.002f, sz * (depth * 0.5f - 0.045f)), iron);
                    }
                }
            }

            // 4) 탁상 소품(넉넉한 탁상 한정) — 코너 오프셋 배치로 빌더 중앙 소품과 무충돌.
            if (width > 1.0f && depth > 0.7f)
            {
                float px = width * 0.24f, pz = -depth * 0.24f;
                Material red = Accent(AccentPalette.ClothRed, mat);
                Material paper = Accent(AccentPalette.Parchment, mat);
                // 펼친 책(커버-페이지-커버 3겹, 서로 1mm 침입 접합)
                CreateDetailBox(table, "Props_BookCover_Lower", new Vector3(0.24f, 0.015f, 0.17f),
                    new Vector3(px, height + 0.007f, pz), red);
                CreateDetailBox(table, "Props_Pages", new Vector3(0.225f, 0.03f, 0.16f),
                    new Vector3(px, height + 0.028f, pz), paper);
                CreateDetailBox(table, "Props_BookCover_Upper", new Vector3(0.24f, 0.015f, 0.17f),
                    new Vector3(px, height + 0.05f, pz), red);
                // 종이 한 장
                if (width > 1.2f)
                    CreateDetailBox(table, "Props_Paper", new Vector3(0.2f, 0.003f, 0.14f),
                        new Vector3(px - 0.26f, height + 0.001f, pz + 0.12f), paper);
                // 유리병(목 포함) — 큰 탁상 한정
                if (width > 1.4f)
                {
                    Material glass = Accent(AccentPalette.GlassGreen, mat);
                    CreateDetailBox(table, "Props_Bottle", new Vector3(0.075f, 0.17f, 0.075f),
                        new Vector3(px + 0.3f, height + 0.084f, pz), glass);
                    CreateDetailBox(table, "Props_BottleNeck", new Vector3(0.028f, 0.06f, 0.028f),
                        new Vector3(px + 0.3f, height + 0.194f, pz), glass);
                }
            }
        }

        /// <summary>의자 폴백 디테일 — 쿠션/등받이 상단 밴드/앞뒤 스트레처.</summary>
        private static void DecorateChair(GameObject chair, float height, Material mat)
        {
            if (height <= 0.3f) return;
            float seatThickness = 0.05f;
            float seatY = height * 0.45f;
            float seatTop = seatY + seatThickness * 0.5f;
            Material wood = Accent(AccentPalette.DarkWood, mat);
            Material red = Accent(AccentPalette.ClothRed, mat);

            // 좌석면 2mm 침입 쿠션(실루엣 + 착시 완화).
            CreateDetailBox(chair, "SeatCushion", new Vector3(0.38f, 0.035f, 0.38f),
                new Vector3(0f, seatTop + 0.0155f, 0f), red);

            // 등받이 상단 밴드 — 백레스트 톱면 2mm 관통(총 높이 불변).
            float backHeight = height * 0.4f;
            float backThickness = 0.04f;
            float backTop = seatY + seatThickness * 0.5f + backHeight;
            float backZ = -0.45f * 0.5f + 0.02f;
            CreateDetailBox(chair, "BackrestTopRail", new Vector3(0.47f, 0.04f, 0.055f),
                new Vector3(0f, backTop - 0.018f, backZ), wood);

            // 앞/뒤 스트레처 — 끝단 다리 중심 관통(평면충돌 없음).
            float legOffset = 0.18f;
            float legHeight = seatY - seatThickness * 0.5f;
            CreateDetailBox(chair, "Stretcher_Front", new Vector3(legOffset * 2f, 0.035f, 0.035f),
                new Vector3(0f, legHeight * 0.45f, -legOffset), wood);
            CreateDetailBox(chair, "Stretcher_Back", new Vector3(legOffset * 2f, 0.035f, 0.035f),
                new Vector3(0f, legHeight * 0.45f, legOffset), wood);
        }

        /// <summary>선반 폴백 디테일 — 후면 패널/상단 캡 + (옵트아웃) 책 줄/병.</summary>
        private static void DecorateShelf(GameObject shelf, float width, float height, float depth, Material mat, int shelves, bool detailed)
        {
            float postThickness = 0.05f;
            float spacing = height / (shelves + 1);
            float areaW = width - postThickness * 2f;
            float areaD = depth - postThickness * 2f;
            Material wood = Accent(AccentPalette.DarkWood, mat);

            // 1) 후면 패널(책장 정체성) — 좌우/상하 2mm 여유(기둥 평면충돌 없음).
            if (depth > 0.25f)
                CreateDetailBox(shelf, "BackPanel", new Vector3(width - 0.04f, height - 0.04f, 0.025f),
                    new Vector3(0f, height * 0.5f, -depth * 0.5f + 0.02f), wood);

            // 2) 상단 마감 캡 — 상면은 명목 높이 2mm 아래(높이 계약 내 초과 없음, 기둥 톱면과 평면충돌 없음),
            //    하단은 기둥 42mm 관통(평면충돌 없음). 캡 두께 0.04 유지 — 시각 캡 보존.
            CreateDetailBox(shelf, "Shelf_Top", new Vector3(width - 0.012f, 0.04f, depth - 0.012f),
                new Vector3(0f, height - 0.022f, 0f), mat);

            if (!detailed) return;

            // 3) 책 줄 — 최상단 칸 제외 각 칸, 최대 6권(결정론 변주, 캡 재료 우측 예약).
            var bookPalettes = new[]
            {
                AccentPalette.ClothRed, AccentPalette.BookOlive, AccentPalette.BookSlate, AccentPalette.Parchment
            };
            for (int i = 0; i < shelves - 1; i++)
            {
                float boardTop = spacing * (i + 1);
                float bookH = Mathf.Min(0.21f, spacing * 0.55f);
                if (bookH < 0.09f) continue;
                float bookD = Mathf.Min(0.14f, areaD * 0.45f);
                float bookZ = -areaD * 0.5f + bookD * 0.5f + 0.01f;   // 후면 패널 쪽 정렬
                float x = -areaW * 0.5f + 0.07f;
                for (int k = 0; k < 6 && x < areaW * 0.5f - 0.22f; k++)
                {
                    float bookW = 0.05f + StableNoise(i * 31 + k * 7) * 0.04f;
                    float hB = bookH * (0.85f + StableNoise(i * 17 + k * 13) * 0.15f);
                    var bp = Accent(bookPalettes[(int)(StableNoise(i * 11 + k * 5) * 3.999f)], mat);
                    CreateDetailBox(shelf, "Book_" + i + "_" + k, new Vector3(bookW, hB, bookD),
                        new Vector3(x + bookW * 0.5f, boardTop + hB * 0.5f + 0.002f, bookZ), bp);
                    x += bookW + 0.012f;
                }
            }

            // 4) 바닥 칸 우측 병 한 개(책 줄 예약 구역 밖).
            float bottomBookH = Mathf.Min(0.21f, spacing * 0.55f);
            if (bottomBookH >= 0.09f && areaW > 0.9f)
            {
                float bottleH = Mathf.Min(0.09f, bottomBookH * 0.8f);
                CreateDetailBox(shelf, "DecantBottle", new Vector3(0.05f, bottleH, 0.05f),
                    new Vector3(areaW * 0.5f - 0.12f, spacing + bottleH * 0.5f + 0.002f,
                        -areaD * 0.5f + 0.1f), Accent(AccentPalette.GlassGreen, mat));
            }
        }

        /// <summary>카운터 폴백 디테일 — 트림/철 코너 + (옵트아웃) 서랍 패이스/손잡이.</summary>
        private static void DecorateCounter(GameObject counter, float width, float height, float depth, Material mat, bool detailed)
        {
            float topThickness = 0.06f;
            float topY = height - topThickness;
            Material wood = Accent(AccentPalette.DarkWood, mat);
            Material iron = Accent(AccentPalette.Iron, mat);

            // 1) 상판 하단 리빌 트림.
            if (height > topThickness + 0.06f)
                CreateDetailBox(counter, "TopTrim", new Vector3(width - 0.015f, 0.02f, depth - 0.015f),
                    new Vector3(0f, topY - 0.012f, 0f), wood);

            // 2) 상판 귀퉁이 철 플레이트(작업대 강조) — 상판 면 1mm 침입 배치.
            if (width > 0.6f && depth > 0.5f)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        string corner = "CornerPlate_" + (sx < 0 ? "F" : "B") + (sz < 0 ? "L" : "R");
                        CreateDetailBox(counter, corner, new Vector3(0.06f, 0.006f, 0.06f),
                            new Vector3(sx * (width * 0.5f - 0.04f), height + 0.002f, sz * (depth * 0.5f - 0.04f)), iron);
                    }
                }
            }

            if (!detailed || width <= 0.9f || height <= 0.6f || depth <= 0.3f) return;

            // 3) 서랍 패이스/손잡이 — 전면 패널 바깥 0.5mm 띄움(평면충돌 없음, 렌더러 전용).
            float panelHeight = height - topThickness;
            float faceW = Mathf.Min(0.45f, width * 0.35f);
            float faceH = Mathf.Min(0.2f, panelHeight * 0.35f);
            float fy = panelHeight * 0.62f;
            float fz = depth * 0.5f + 0.0065f;
            CreateDetailBox(counter, "Drawer_Face", new Vector3(faceW, faceH, 0.012f),
                new Vector3(-width * 0.18f, fy, fz), wood);
            CreateDetailBox(counter, "Drawer_Knob", new Vector3(0.032f, 0.032f, 0.02f),
                new Vector3(-width * 0.18f, fy, fz + 0.014f), iron);
        }

        /// <summary>침대 폴백 디테일 — 이불/접힘 띠/프레임 철 코너(상호작용 콜라이더 계약 불변).</summary>
        private static void DecorateBed(GameObject bed, float width, float depth, Material mat)
        {
            float mattressHeight = 0.2f;
            float frameHeight = 0.3f;
            float frameThickness = 0.05f;
            float mattressTop = frameHeight + mattressHeight;
            Material red = Accent(AccentPalette.ClothRed, mat);
            Material paper = Accent(AccentPalette.Parchment, mat);
            Material iron = Accent(AccentPalette.Iron, mat);

            // 1) 이불 — 발쪽 52% 덮음(베개 쪽 비움), 매트리스 면 4mm 침입, 콜라이더(높이 0.6) 내.
            float blanketLen = depth * 0.52f;
            float blanketY = mattressTop + 0.055f * 0.5f - 0.004f;
            float blanketZ = depth * 0.5f - blanketLen * 0.5f;
            CreateDetailBox(bed, "Blanket", new Vector3(width - 0.03f, 0.055f, blanketLen),
                new Vector3(0f, blanketY, blanketZ), red);
            // 2) 접힘 띠 — 이불 머리쪽 모서리 하이라이트.
            CreateDetailBox(bed, "Blanket_Fold", new Vector3(width - 0.03f, 0.018f, 0.1f),
                new Vector3(0f, blanketY + 0.0255f, blanketZ - blanketLen * 0.5f + 0.055f), paper);
            // 3) 프레임 귀퉁이 철 장식 — 프레임 톱면 관통(평면충돌 없음).
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    string corner = "FrameCorner_" + (sx < 0 ? "F" : "B") + (sz < 0 ? "L" : "R");
                    CreateDetailBox(bed, corner, new Vector3(0.055f, 0.055f, 0.055f),
                        new Vector3(sx * (width * 0.5f - frameThickness * 0.5f), frameHeight - 0.02f,
                            sz * (depth * 0.5f - frameThickness * 0.5f)), iron);
                }
            }
        }
    }
}
