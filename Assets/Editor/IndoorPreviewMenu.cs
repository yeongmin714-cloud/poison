using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.UI;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// 내부 씬(IndoorScene) 단독 미리보기 에디터 메뉴.
    /// 메인 씬/런타임 전환(IndoorSceneTransition) 없이 IndoorScene만 열고
    /// 각 내부 빌더를 직접 호출해 생성 결과를 즉시 검증한다. (에디터 전용)
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
            RunPreview("플레이어 성", () =>
            {
                var interior = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
                if (interior != null)
                    TerritoryBuilder.SpawnInteriorFixtures(interior.transform.position, "Empire"); // 런타임과 동일한 후처리
                return interior;
            });
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

        /// <summary>영주 성 내부 생성 + 런타임과 동일한 실내 fixture 후처리.</summary>
        private static GameObject BuildLordCastle(string nation, int variant)
        {
            var interior = CastleInteriorBuilder.BuildCastleInterior(nation, variant);
            if (interior != null)
                TerritoryBuilder.SpawnInteriorFixtures(interior.transform.position, nation);
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
