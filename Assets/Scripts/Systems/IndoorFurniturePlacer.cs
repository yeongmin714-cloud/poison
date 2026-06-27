using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C11-06: 실내 가구 배치 생성기.
    /// 모든 가구는 PrimitiveType.Cube 기반 Mesh로 생성.
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
        public static GameObject CreateShelf(float width, float height, float depth, Material mat, int shelves = 3)
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
        public static GameObject CreateCounter(float width, float height, float depth, Material mat)
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
    }
}
