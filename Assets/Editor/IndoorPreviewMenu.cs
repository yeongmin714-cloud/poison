using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.UI;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// 실내 씬 프리뷰 에디터 메뉴.
    /// 플레이어 성은 격리된 플레이 테스트 씬으로 열고, 나머지 프리뷰는
    /// 공용 IndoorScene에 런타임 전환 없이 내부 생성 결과를 표시한다.
    /// 빌더 시그니처는 IndoorSceneTransition.cs / TavernInteriorBuilder.cs의
    /// 실제 public 진입 메서드와 일치시킴 (추측 호출 없음).
    /// </summary>
    public static class IndoorPreviewMenu
    {
        private const string INDOOR_SCENE_PATH = "Assets/Scenes/IndoorScene.unity";

        // ── 성 (플레이어/영주) ─────────────────────────────────

        [MenuItem("Tools/Indoor/미리보기/플레이어 성 (Castle-Player)", priority = 1)]
        public static void PreviewPlayerCastle()
        {
            PlayerCastleInteriorPlaytestSceneBuilder.CreateOrOpenTestScene();
        }

        [MenuItem("Tools/Indoor/미리보기/플레이어 성 (Castle-Player)", true)]
        private static bool ValidatePreviewPlayerCastle()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem("Tools/Indoor/미리보기/영주 성-Empire", priority = 11)]
        public static void PreviewLordCastleEmpire()
        {
            RunPreview("영주 성-Empire", () => BuildLordCastle("Empire", 0));
        }

        [MenuItem("Tools/Indoor/미리보기/영주 성-Eastern", priority = 12)]
        public static void PreviewLordCastleEastern()
        {
            RunPreview("영주 성-Eastern", () => BuildLordCastle("Eastern", 1));
        }

        [MenuItem("Tools/Indoor/미리보기/영주 성-Western", priority = 13)]
        public static void PreviewLordCastleWestern()
        {
            RunPreview("영주 성-Western", () => BuildLordCastle("Western", 2));
        }

        [MenuItem("Tools/Indoor/미리보기/영주 성-Southern", priority = 14)]
        public static void PreviewLordCastleSouthern()
        {
            RunPreview("영주 성-Southern", () => BuildLordCastle("Southern", 3));
        }

        [MenuItem("Tools/Indoor/미리보기/영주 성-Northern", priority = 15)]
        public static void PreviewLordCastleNorthern()
        {
            RunPreview("영주 성-Northern", () => BuildLordCastle("Northern", 4));
        }

        // ── [2026-10-14 Phase 4] 레이아웃 계열별 프리뷰 — 명시 정의 오버로드 경유 ──
        //   런타임 배정(SelectForTerritory)과 동일한 3인자 빌더 경로로, 정의는 인자로만 전달되고
        //   전역 상태로 남지 않는다. 기존 메뉴/흐름은 변경 없음.

        [MenuItem("Tools/Indoor/미리보기/영주 성-원형 중앙홀형 (RoundKeep)", priority = 16)]
        public static void PreviewLordCastleRoundKeep()
        {
            RunPreview("영주 성-원형 중앙홀형 (RoundKeep)",
                () => BuildLordCastle("Empire", 0, InteriorLayoutDefinition.RoundKeep));
        }

        [MenuItem("Tools/Indoor/미리보기/영주 성-비대칭 영주관형 (AsymmetricManor)", priority = 17)]
        public static void PreviewLordCastleAsymmetricManor()
        {
            RunPreview("영주 성-비대칭 영주관형 (AsymmetricManor)",
                () => BuildLordCastle("Empire", 0, InteriorLayoutDefinition.AsymmetricManor));
        }

        // ── 일반 건물 ─────────────────────────────────────────

        [MenuItem("Tools/Indoor/미리보기/여관", priority = 21)]
        public static void PreviewTavern()
        {
            RunPreview("여관", () => TavernInteriorBuilder.BuildTavernInterior("default", 1));
        }

        [MenuItem("Tools/Indoor/미리보기/집", priority = 22)]
        public static void PreviewHouse()
        {
            RunPreview("집", () => { HouseInteriorBuilder.BuildHouseInterior(); return null; });
        }

        [MenuItem("Tools/Indoor/미리보기/허점 (Barn)", priority = 23)]
        public static void PreviewBarn()
        {
            RunPreview("허점 (Barn)", () => { BarnInteriorBuilder.BuildBarnInterior(); return null; });
        }

        [MenuItem("Tools/Indoor/미리보기/동굴 (Cave)", priority = 24)]
        public static void PreviewCave()
        {
            RunPreview("동굴 (Cave)", () => CaveInteriorBuilder.BuildCaveInterior("default", 1));
        }

        [MenuItem("Tools/Indoor/미리보기/상점 (Shop)", priority = 25)]
        public static void PreviewShop()
        {
            RunPreview("상점 (Shop)", () => { ShopInteriorBuilder.BuildShopInterior(); return null; });
        }

        [MenuItem("Tools/Indoor/미리보기/교회 (Church)", priority = 26)]
        public static void PreviewChurch()
        {
            RunPreview("교회 (Church)", () => { ChurchInteriorBuilder.BuildChurchInterior(); return null; });
        }

        [MenuItem("Tools/Indoor/미리보기/크래프트하우스 (CraftHouse)", priority = 27)]
        public static void PreviewCraftHouse()
        {
            RunPreview("크래프트하우스 (CraftHouse)", () => { CraftHouseInteriorBuilder.BuildCraftHouseInterior(); return null; });
        }

        // ── 공통 처리 ─────────────────────────────────────────

        /// <summary>영주 성 내부 생성 + 런타임과 동일한 실내 fixture 후처리.
        /// [2026-10-10 J1] 통일 빌더(플레이어 성 표준) + 잠긴 문 재정착 — 런타임 진입 흐름과 동일.
        /// [2026-10-14 Phase 4] 기존 2인자 메뉴 계약 보존 — 기준형(KeepOctagon) 명시 호출과 동일 결과.</summary>
        private static GameObject BuildLordCastle(string nation, int variant)
        {
            return BuildLordCastle(nation, variant, InteriorLayoutDefinition.KeepOctagon);
        }

        /// <summary>[2026-10-14 Phase 4] 명시 레이아웃 정의로 영주 성 내부 생성 — 런타임과 동일한
        /// 3인자 오버로드 경로. 정의는 인자로만 전달(전역 상태 없음), 미리보기 결과는 기존 관행대로
        /// IndoorScene 에셋에 저장하지 않는 의도적 비영속 동작을 유지한다.</summary>
        private static GameObject BuildLordCastle(string nation, int variant, InteriorLayoutDefinition layout)
        {
            var interior = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nation, variant, layout);
            if (interior != null)
            {
                CastleInteriorBuilder.AddLordLockedDoors(interior);
                TerritoryBuilder.SpawnInteriorFixtures(interior.transform.position, nation);
            }
            return interior;
        }

        /// <summary>
        /// 공통 미리보기 흐름: 수정 씬 저장 확인 → IndoorScene 열기 → 빌더 호출 → 결과 로그.
        /// </summary>
        private static void RunPreview(string label, Func<GameObject> buildAction)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning($"[IndoorPreview] {label}: 열려 있는 씬 저장이 취소되어 미리보기를 중단합니다.");
                return;
            }

            EditorSceneManager.OpenScene(INDOOR_SCENE_PATH);

            var interior = buildAction();
            Debug.Log(interior != null
                ? $"[IndoorPreview] {label} 미리보기 생성 완료: '{interior.name}'"
                : $"[IndoorPreview] {label} 빌더 호출 완료 (반환값 없는 void 빌더)");
        }
    }
}
