// 2026-10-10: 계획 v4 — 아레나 실내(베팅 로비+투기장) 품질 계층 이식.
// 성 내부(PlayerCastleInteriorBuilder)에서 사용자 승인된 H1/H2/H5 품질 언어를
// IndoorBuilder.CreateRoom 셸(쿼드 벽) 기반 실내에 동일하게 적용한다.
// - 벽 톱면 캡: 벽 상단 모서리의 얇은 돌 박스(탑다운에선 "돌 선", 측면에선 코핑 밴드)
// - 접지 블롭: 오브제 바닥 접지감(성 카탈로그와 동일 절차 텍스처 재용, 타원 지원)
// - 마모 스탬프: 색 변화 없는 낡음 데칼
// 모든 계층 렌더러 전용(무콜라이더)·그림자 미방출 — 성 내부 계약과 동일.
// [금지 원칙 계승] 천장/공중 구조물·벽 없는 장식선·바닥 색 교체 금지(2026-10-10 확정).
using UnityEngine;

namespace ProjectName.Systems
{
    public static class IndoorQualityLayers
    {
        private const float StoneTileWidth = 2.2f;   // wall_stone_lower 격자(가로)
        private const float StoneTileHeight = 1.1f;  // wall_stone_lower 격자(세로)

        /// <summary>
        /// [I1] 쿼드 벽 4면(Wall_Front/Back/Left/Right 규약, 부재 시 스킵) 상단에 돌 캡 박스.
        /// 캡은 벽 톱 모서리에 얹힌 얇은 상자(y=roomHeight+capHeight/2) — 탑다운에서 벽이
        /// "짙은 돌 선"으로 읽힌다(성 내부 H1과 동일 효과). 성공 개수 반환.
        /// </summary>
        public static int AddQuadWallCaps(GameObject room, float roomWidth, float roomDepth, float roomHeight,
            float capDepth = 0.5f, float capHeight = 0.15f)
        {
            if (room == null) return 0;
            int count = 0;
            count += AddCap(room, "Wall_Front_TopCap", roomWidth,
                new Vector3(0f, roomHeight + capHeight * 0.5f, roomDepth * 0.5f),
                new Vector3(roomWidth, capHeight, capDepth));
            count += AddCap(room, "Wall_Back_TopCap", roomWidth,
                new Vector3(0f, roomHeight + capHeight * 0.5f, -roomDepth * 0.5f),
                new Vector3(roomWidth, capHeight, capDepth));
            count += AddCap(room, "Wall_Left_TopCap", roomDepth,
                new Vector3(-roomWidth * 0.5f, roomHeight + capHeight * 0.5f, 0f),
                new Vector3(capDepth, capHeight, roomDepth));
            count += AddCap(room, "Wall_Right_TopCap", roomDepth,
                new Vector3(roomWidth * 0.5f, roomHeight + capHeight * 0.5f, 0f),
                new Vector3(capDepth, capHeight, roomDepth));
            return count;
        }

        /// <summary>[I1] 솔리드 벽(축정렬 박스) 위 캡 — 개구부 대체 벽 등 임의 박스 지정.</summary>
        public static int AddBoxCap(GameObject room, string name, Vector3 center, Vector3 size)
        {
            return AddCap(room, name, Mathf.Max(size.x, size.z), center, size);
        }

        private static int AddCap(GameObject room, string name, float length, Vector3 center, Vector3 size)
        {
            var stone = IndoorMaterialFactory.CreateWallStoneLower();
            if (stone == null) return 0; // 텍스처 미투입 시 스킵(선택 강화 규약)
            stone.color = new Color(0.78f, 0.76f, 0.73f);   // 성 내부 캡과 동일 마감 톤
            // 프리미티브 큐브 UV(면별 0..1) + mainTextureScale 틸링 — 긴 캡에 돌 격자 반복.
            stone.mainTextureScale = new Vector2(Mathf.Max(1f, length / StoneTileWidth), 0.45f);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = name;
            Object.DestroyImmediate(cap.GetComponent<Collider>()); // 렌더러 전용 계약
            cap.transform.SetParent(room.transform, false);
            cap.transform.localPosition = center;
            cap.transform.localScale = size;
            var rend = cap.GetComponent<MeshRenderer>();
            rend.sharedMaterial = stone;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return 1;
        }

        /// <summary>
        /// [I2] 접지 블롭 섀도우 — parent 기준 pos에 방사형 어두운 타원(가로 rx/세로 rz).
        /// 성 카탈로그와 동일 절차 텍스처(GetBlobShadowTexture). 렌더러 전용·그림자 미방출·무콜라이더.
        /// </summary>
        public static void AddBlobAt(GameObject parent, Vector3 pos, float radiusX, float radiusZ, float y = 0.02f)
        {
            if (parent == null || radiusX <= 0f || radiusZ <= 0f) return;
            var mat = IndoorMaterialFactory.CreateBlobShadowMat();
            if (mat == null) return;
            var blob = new GameObject("BlobShadow_" + pos.x.ToString("0.0") + "_" + pos.z.ToString("0.0"));
            blob.transform.SetParent(parent.transform, false);
            blob.transform.localPosition = new Vector3(pos.x, y, pos.z);
            blob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var mf = blob.AddComponent<MeshFilter>();
            var mesh = new Mesh { name = "ArenaBlob_Mesh" };
            float hx = radiusX * 0.5f;
            float hz = radiusZ * 0.5f;
            mesh.vertices = new Vector3[]
            {
                new Vector3(-hx, -hz, 0), new Vector3(hx, -hz, 0),
                new Vector3(-hx,  hz, 0), new Vector3(hx,  hz, 0)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            var r = blob.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // No Collider by design: blob shadow is visual only.
        }

        /// <summary>
        /// [I5→H5 이식] 바닥 마모 스탬프 — 색 변화 없는 낡음(어두운 얼룩+균열) 데칼.
        /// y는 스탬프 높이(바닥 데코 위). 회전 90° 사이클. 생성 개수 반환.
        /// </summary>
        public static int AddWearStamps(GameObject room, string namePrefix, Vector2[] spots, float size, float y)
        {
            if (room == null || spots == null) return 0;
            var mat = IndoorMaterialFactory.CreateFloorWearMat();
            if (mat == null) return 0;
            for (int i = 0; i < spots.Length; i++)
            {
                var go = new GameObject(namePrefix + "_" + i);
                go.transform.SetParent(room.transform, false);
                go.transform.localPosition = new Vector3(spots[i].x, y, spots[i].y);
                go.transform.localRotation = Quaternion.Euler(90f, (i % 4) * 90f, 0f);
                var mf = go.AddComponent<MeshFilter>();
                var mesh = new Mesh { name = namePrefix + "_" + i + "_Mesh" };
                float h = size * 0.5f;
                mesh.vertices = new Vector3[]
                {
                    new Vector3(-h, -h, 0), new Vector3(h, -h, 0),
                    new Vector3(-h,  h, 0), new Vector3(h,  h, 0)
                };
                mesh.uv = new Vector2[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, 1f), new Vector2(1f, 1f)
                };
                mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mf.sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // No Collider by design: wear stamps are visual only.
            }
            return spots.Length;
        }
    }
}
