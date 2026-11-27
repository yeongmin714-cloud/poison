// 2026-10-25 (125차 후속24): 아레나 실외 전용 머티리얼 팩토리 — 성 내부 HQ 계약(IndoorMaterialFactory)의
// 실외 이식판. 투기장 실내(ArenaPitInteriorBuilder)가 성 내부와 동등한 품질 패리티를 갖도록
// 격자 대신 재질 고유 질감 + 노멀 relief + 스무스니스 표면 계약을 제공한다.
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// [125차 후속24 — A1] 아레나 실외 전용 머티리얼 팩토리.
    /// 계약: 파일 우선(Resources/Indoor)·절차 폴백·normal relief·스무스니스 설정.
    /// 국가색 유지 — 벽은 국가 절차 텍스처를 그대로 mainTexture로 유지하고 석재 노멀을 결합.
    /// 모든 메서드는 셰이더/텍스처 부재 시 null 반환(호출부 폴백 계약 — 빌더 기존 경로 유지).
    /// </summary>
    public static class ArenaOutdoorMaterialFactory
    {
        private static Shader _lit;

        private static Shader UrpLit
        {
            get
            {
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                return _lit;
            }
        }

        /// <summary>모래 — 격자 없는 미세 입자 노이즈(결정론). 결투장 모래 계약 대체.</summary>
        public static Material CreateSand()
        {
            var shader = UrpLit;
            if (shader == null) return null;
            // [125차 후속35-R P3-1] AO 합성용 Texture2D 참조 — mainTexture는 Texture 타입이라 별도 보관
            var sandBase = IndoorTextureGenerator.GenerateSandTexture();
            var m = new Material(shader)
            {
                name = "ArenaPit_SandMat",
                mainTexture = sandBase,
            };
            // [125차 후속25 R4 → 125차 후속36 A3] 타일링 8×8→10×10 — 4.5m/타일 반복이 영상15에서
            //   여전히 인지됨(f_012/f_016 판독) → 3.6m/타일로 재완화. 노멀/AO/디테일 모두 동일 스케일 정합.
            const float sandTiles = 10f;
            m.mainTextureScale = new Vector2(sandTiles, sandTiles);
            // [125차 후속24 보완] 모래 노멀맵 — 계획(A1) 명세 "노이즈 텍스처+노멀맵(_BumpMap)" 이행.
            // 절차 생성 노멀(AG 스위즐) — 런타임 생성이라 임포터 스위즐 부재 대응.
            var sandN = IndoorTextureGenerator.GenerateSandNormalTexture();
            if (sandN != null)
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", sandN);
                m.SetTextureScale("_BumpMap", new Vector2(sandTiles, sandTiles));   // 베이스 타일링과 정합
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.20f);   // 마른 모래 — 반사 거의 없음
            // [125차 후속35-R P3-1] 런타임 AO — 절차 모래 파생(파일 교체 0). 웜광/횃불 각도에서 움푹 음영.
            var sandAo = IndoorTextureGenerator.SynthesizeOcclusionFromAlbedo(sandBase);
            if (sandAo != null)
            {
                m.SetTexture("_OcclusionMap", sandAo);
                m.SetTextureScale("_OcclusionMap", new Vector2(sandTiles, sandTiles));
                if (m.HasProperty("_OcclusionStrength")) m.SetFloat("_OcclusionStrength", 0.5f);
            }
            // [125차 후속35-R P3-3 → 후속36 A3] 디테일 노이즈 — 모래 타일 반복 완화(스케일 재조정 24→28).
            var sandMicro = IndoorTextureGenerator.GenerateMicroNoiseTexture();
            if (sandMicro != null)
            {
                m.EnableKeyword("_DETAIL_MULX2");
                m.SetTexture("_DetailAlbedoMap", sandMicro);
                m.SetTextureScale("_DetailAlbedoMap", new Vector2(28f, 28f));
                if (m.HasProperty("_DetailAlbedoMapScale")) m.SetFloat("_DetailAlbedoMapScale", 0.3f);
            }
            return m;
        }

        /// <summary>에이프런 — 다져진 흙 슬래브(큰 타일 저대비). 모래보다 어둡고 단단한 재질 대비.</summary>
        public static Material CreateApron()
        {
            var shader = UrpLit;
            if (shader == null) return null;
            var tex = IndoorTextureGenerator.GenerateFloorTile(256, 256,
                new Color(0.50f, 0.46f, 0.39f), new Color(0.41f, 0.38f, 0.32f), 0.5f);
            var m = new Material(shader) { name = "ArenaPit_ApronMat", mainTexture = tex };
            // [125차 후속25 R2] 타일링 — 슬래브 타일이 45.6m에 1:1 스트레치되면 거대 무늬 1장이 됨.
            //   18×18 타일 ≈ 2.5m/타일 — 다진 흙 슬래브 반복 패턴 복원.
            m.mainTextureScale = new Vector2(18f, 18f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.30f);
            return m;
        }

        /// <summary>
        /// [125차 후속36 A4] 모래-에이프런 경계 블렌드 머티리얼 — 라디얼 그라데이션 링
        /// (모래색→다진 흙색 페이드). 영상15 f_012 "경계가 칼같이 잘림" 판독 수리.
        /// 파일 부재/셰이더 부재 시 null(호출부 폴백 계약).
        /// </summary>
        public static Material CreateApronBlend()
        {
            var shader = UrpLit;
            if (shader == null) return null;
            var tex = IndoorTextureGenerator.GenerateEdgeBlendTexture();
            if (tex == null) return null;
            var m = new Material(shader) { name = "ArenaPit_ApronBlendMat", mainTexture = tex };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            return m;
        }

        /// <summary>
        /// 외곽 벽 — 국가 절차 텍스처(국가색 유지) + 석재 노멀 relief(파일 있는 경우) 결합.
        /// 성 내부 CreateWallStoneLower와 동일 노멀 계약. nationTex=null이면 파일 석재로 폴백.
        /// </summary>
        public static Material CreateWall(Texture2D nationTex, string nationStyle)
        {
            var shader = UrpLit;
            if (shader == null) return null;
            var m = new Material(shader) { name = $"ArenaPit_WallMat_{nationStyle}" };
            var tex = nationTex;
            if (tex == null && IndoorTextureLoader.HasFiles)
            {
                tex = IndoorTextureLoader.WallStone;
                if (tex != null) IndoorTextureLoader.Configure(tex, linear: false);
            }
            if (tex != null) m.mainTexture = tex;
            var stoneN = IndoorTextureLoader.WallStoneNormal;
            if (stoneN != null)
            {
                IndoorTextureLoader.Configure(stoneN, linear: true);
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", stoneN);
                m.SetTextureScale("_BumpMap", new Vector2(2f, 1f));   // 벽 3m 높이 — 세로 반복 최소화
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            return m;
        }

        /// <summary>왕가 박스 — 회반죽 파일(금빛 틴트) + 석재 노멀. 파일 없으면 null(호출부 폴백).</summary>
        public static Material CreateRoyalBox()
        {
            var shader = UrpLit;
            if (shader == null) return null;
            if (!IndoorTextureLoader.HasFiles || IndoorTextureLoader.WallPlaster == null) return null;
            var tex = IndoorTextureLoader.WallPlaster;
            IndoorTextureLoader.Configure(tex, linear: false);
            var m = new Material(shader) { name = "ArenaPit_RoyalBoxMat", mainTexture = tex };
            m.color = new Color(0.86f, 0.76f, 0.52f);   // 금빛 틴트 — 왕가 식별색(국가색 유지 관례)
            var stoneN = IndoorTextureLoader.WallStoneNormal;
            if (stoneN != null)
            {
                IndoorTextureLoader.Configure(stoneN, linear: true);
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", stoneN);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.40f);
            return m;
        }
    }
}
