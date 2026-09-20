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

        /// <summary>제공 텍스처 기반 벽 머티리얼 — 하부 석재/상부 회반죽 분리판 반환 (없으면 null).</summary>
        public static bool TryCreateWall(float roomWidth, float roomHeight, out Material lower, out Material upper)
        {
            lower = null; upper = null;
            if (!IndoorTextureLoader.HasFiles || UrpLit == null) return false;
            if (IndoorTextureLoader.WallStone == null) return false;

            // 하부 석재 (높이 0~2m 밴드 가정 — 벽 쿼드의 하단 절반)
            var stone = new Material(UrpLit) { name = "IndoorHQ_WallStone" };
            var stoneTex = IndoorTextureLoader.WallStone;
            IndoorTextureLoader.Configure(stoneTex, linear: false);
            stone.mainTexture = stoneTex;
            float sx = Mathf.Max(1f, Mathf.RoundToInt(roomWidth / IndoorTextureLoader.WallStoneCoverX));
            float sy = Mathf.Max(1f, Mathf.RoundToInt((roomHeight * 0.5f) / IndoorTextureLoader.WallStoneCoverY));
            stone.mainTextureScale = new Vector2(sx, sy);
            stone.SetFloat("_Smoothness", 0.35f);
            var sn = IndoorTextureLoader.WallStoneNormal;
            if (sn != null)
            {
                IndoorTextureLoader.Configure(sn, linear: true);
                stone.EnableKeyword("_NORMALMAP");
                stone.SetTexture("_BumpMap", sn);
                stone.SetTextureScale("_BumpMap", new Vector2(sx, sy));
            }
            lower = stone;

            // 상부 회반죽
            var plaster = new Material(UrpLit) { name = "IndoorHQ_WallPlaster" };
            if (IndoorTextureLoader.WallPlaster != null)
            {
                var plasterTex = IndoorTextureLoader.WallPlaster;
                IndoorTextureLoader.Configure(plasterTex, linear: false);
                plaster.mainTexture = plasterTex;
                plaster.mainTextureScale = new Vector2(sx, sy);
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
    }
}
