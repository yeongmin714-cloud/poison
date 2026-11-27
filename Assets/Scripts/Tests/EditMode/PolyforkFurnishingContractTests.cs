using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [157차 P4] Polyfork 실내 소품 배선 계약 — 리소스/매니페스트/소스계약. [158차 P2] 전종 70종 확장. [160차 P3] 전종 94종 재생성. [161차 P3] 전종 112종 재생성. [162차 P3] 전종 169종 재생성.
    ///
    /// T1 GLB 169종 파일 존재 / T2 CREDITS.md 매니페스트 credit 169건 / T3 배선 호출 하한 / T4 id 실내 배선 전종 /
    /// T5 카탈로그 게이트 정의 / T6 벽 횔도 자연 높이 / T7 로비 탁자 교체 / T8 왕좌 배선 / T9 책장 2종 배선(158차 P2) /
    /// T10 던전 수용분 미배선(FurnishingPass) / T11 던전 소품 패스 CaveInteriorBuilder 소스계약 6종(160차 P3·161차 P3 확장) /
    /// T12 야외 건축물 패스 PolyforkOutdoorStructures 소스계약(161차 P3·162차 P3 호수 선박 확장) / T13 카탈로그 솔리드 게이트 InteriorPropCatalog 소스계약(161차 P3).
    /// [172차] 코인 원본 2종 가산 + [173차] 장신구 원본 4종 가산 — T1 204종·T2 credit 204건(ls 실측 전체 재생성), 아이템 시각용 수용(Skip).
    /// [175차] 물고기 원본 3종(koi-base·reef-grouper-964612·coelacanth-base) 가산 — T1 207종·T2 credit 207건(ls 실측 전체 재생성), 아이템 시각용 수용(Skip).
    /// ⚠ 문자열 계약 자기 오염 주의(149차) — 검증 문자열을 주석에 기재 금지(코드 리터럴만).
    /// </summary>
    public class PolyforkFurnishingContractTests
    {
        private const string PassRelPath = "Assets/Scripts/Systems/InteriorFurnishingPass.cs";
        private const string CatalogRelPath = "Assets/Scripts/Systems/InteriorPropCatalog.cs";
        private const string CaveBuilderRelPath = "Assets/Scripts/Systems/CaveInteriorBuilder.cs";
        private const string OutdoorStructuresRelPath = "Assets/Scripts/Systems/PolyforkOutdoorStructures.cs"; // [161차 P3] 야외 건축물 패스 소스.

        private static readonly string[] PolyforkIds =
        {
                "adobe-colonial-farmhouse-cf13d8",
                "alchemy-flask-b1b4fb",
                "alchemy-workbench-117b73",
                "alcove-bookcase-83894d",
                "alpine-hay-barn-3965e5",
                "an-hourglass-with-sand-ec9f11",
                "ancestral-shrine-de7ed2",
                "anvil-1286cb",
                "arch-gatehouse-f4391b",
                "arched-passage-56393e",
                "archery-target-762034",
                "area-rug-5c56e2",
                "armchair-980c7c",
                "armour-stand-3d77c4",
                "azalea-bush-7bb14d",
                "back-bar-shelf-and-f5f279",
                "baroque-panelled-wall-8658ed",
                "barred-cell-door-571e9c",
                "barrel-organ-cart-30799d",
                "basil-pot-125949",
                "basket-of-produce-7420cf",
                "bathhouse-4ac41a",
                "battle-map-tile-69e0b1",
                "blacksmith-forge-09fa85",
                "bow-9a225d",
                "brass-candelabra-89ab76",
                "bread-loaf-0e0ab5",
                "brick-church-322885",
                "bubbling-retort-4f9721",
                "bubbling-tripod-cauldron-124e15",
                "camp-canoe-c65d2a",
                "camp-gate-2de13f",
                "campaign-map-scroll-d4a382",
                "campfire-parent-b1c0d5",
                "candle-spike-cluster-d7c566",
                "cemetery-lantern-post-fed352",
                "chained-book-lectern-381597",
                "charred-meat-haunch-be1ea2",
                "chest-of-drawers-cbe3e7",
                "chicha-barrel-77037f",
                "chicken-coop-371f97",
                "clay-bread-oven-860b06",
                "clay-storage-pot-626452",
                "clay-wine-jug-b4ebd3",
                "coelacanth-base",
                "coin-pouch-be4dd8",
                "commander-s-camp-stool-d7fa4c",
                "concert-grand-piano-85c5b1",
                "concert-pedal-harp-eb1a21",
                "controllable-food-smoker-aac206",
                "copper-ore-chunk-258509",
                "country-well-8feec8",
                "cove-tavern-5861a6",
                "crystal-shard-9e0a83",
                "curtain-panel-ee2ed9",
                "divination-orb-115d77",
                "dock-cargo-barge-69dc4b",
                "dock-warehouse-bf0967",
                "dragonfire-brazier-6bc2ce",
                "dressing-mirror-7c0871",
                "faculty-office-book-wall-c2d532",
                "farm-lantern-747dc8",
                "fern-plant-1559b8",
                "firewood-stack-8a2899",
                "flame-brazier-bbf74a",
                "flower-seller-s-hand-d617f9",
                "four-wheel-wagon-adb946",
                "framed-wall-art-d2c8a2",
                "gabled-farmhouse-c7534f",
                "giant-mushroom-cluster-868de6",
                "gilded-harp-f32566",
                "gold-coin-29afc1",
                "gold-coin-stack-a9ef52",
                "gold-ore-chunk-6f1d9e",
                "governor-s-audience-hall-a4d777",
                "grain-sack-401d96",
                "great-helm-2551bc",
                "greatsword-a956f3",
                "halloween-yard-scarecrow-cb0ffb",
                "hand-axe-cee9e7",
                "hand-lantern-cdbe1a",
                "hanga-roa-fishing-boat-f09534",
                "hanging-plant-basket-66a002",
                "harpsichord-ac1869",
                "hayride-wagon-1641de",
                "healing-herb-bundle-4d9724",
                "hide-rain-catchment-funnel-e41d94",
                "iron-banded-strongbox-698383",
                "iron-bound-door-e0e953",
                "iron-chandelier-1337dc",
                "iron-ingot-36697d",
                "iron-ore-chunk-1df386",
                "jewel-casket-975ecc",
                "kite-shield-343e62",
                "knuckle-ring-529610",
                "koi-base",
                "kura-storehouse-5c9631",
                "laced-hide-stretching-rack-8152a1",
                "leather-forge-bellows-7f6d42",
                "legendary-chest-4abed4",
                "library-book-stack-double-ce8d7f",
                "live-rock-piece-6c7bdf",
                "livery-stable-c01ee0",
                "log-cabin-4fac3b",
                "loot-sack-4762f5",
                "looted-gilded-throne-008fc3",
                "machiya-townhouse-8ef64e",
                "milk-churn-895735",
                "milk-churn-stand-130fe2",
                "mimic-chest-57e440",
                "mine-cart-21d7f0",
                "mine-headframe-735cf4",
                "mineral-geode-cluster-299927",
                "mining-pickaxe-90bdf6",
                "minka-farmhouse-680475",
                "modular-doorstep-bridge-df54e1",
                "mushroom-679e55",
                "mushroom-cluster-18dc1d",
                "mushroom-cluster-f2e3ba",
                "oak-barrel-b65311",
                "old-lighthouse-48ca6c",
                "open-stable-dbf2d5",
                "opera-box-tier-d7fdd1",
                "orongo-house-run-e35d16",
                "ossuary-skull-shelf-a8fdf0",
                "overflowing-treasure-chest-3d6ee0",
                "palisade-wall-621e29",
                "pante-n-chapel-62dccd",
                "parish-church-543cf8",
                "pastel-flowering-plant-d16a03",
                "pigeon-base",
                "pipe-organ-36e035",
                "porch-log-cabin-d2f3ef",
                "potion-bottle-099ad7",
                "potted-olive-tree-7eee05",
                "potting-bench-3a85ec",
                "prayer-bead-strand-e18d9a",
                "propped-fishing-rod-072978",
                "quench-barrel-0c78fd",
                "quiver-722a40",
                "reef-grouper-964612",
                "rope-bridge-span-159f8c",
                "saddle-on-a-rack-f034cd",
                "saddle-quern-food-mill-8e3657",
                "saloon-upright-piano-4b9c64",
                "salvage-hand-cart-222304",
                "scroll-rack-f465dd",
                "shield-24b9f0",
                "ship-s-longboat-f159fd",
                "shopfront-house-43d94d",
                "shovel-997c4d",
                "signal-beacon-tower-9c0d1a",
                "signet-ring-efa0ee",
                "silver-cloche-platter-25dd3e",
                "silver-coin-64ab27",
                "silver-ore-chunk-1e440c",
                "simple-band-ring-929543",
                "small-fishing-boat-4ac0e3",
                "smelting-furnace-3209f1",
                "spear-e9824e",
                "spellbook-1e8dfe",
                "spilled-coin-pile-caccf6",
                "splay-leg-lookout-tower-6fae7e",
                "standing-brazier-c9e4ee",
                "standing-knight-statue-8bdcc9",
                "stilted-granary-3e7d7d",
                "stone-chapel-03e774",
                "stone-farmhouse-billet-2dbbf2",
                "stone-sarcophagus-cc96a4",
                "straw-training-dummy-64eeb4",
                "sunflower-seed-b4fbbd",
                "table-candle-lantern-a7b5af",
                "table-herb-pot-3c0cf4",
                "taheta-stone-basin-336021",
                "temple-bell-tower-745387",
                "tenshu-keep-2ff636",
                "thatched-cottage-0e63f5",
                "timber-crate-ba0739",
                "timber-trestle-table-024d88",
                "timber-wash-house-c7e7d5",
                "tool-rack-f77b81",
                "town-welcome-arch-d2af7d",
                "trailhead-sign-aa29e1",
                "travelling-trunk-2616ab",
                "treasure-chest-373a2e",
                "tupa-stone-tower-801c13",
                "turning-stair-landing-066019",
                "upgradeable-assembly-workbench-a18ff3",
                "velvet-mat-tile-0ccce3",
                "ventilated-hide-food-larder-f53174",
                "village-gatehouse-181a9e",
                "village-hall-c6d3c8",
                "village-tavern-4e94e3",
                "wall-torch-bracket-f5ad14",
                "wardrobe-f3b2f2",
                "watchtower-140f78",
                "water-mill-b31972",
                "water-pump-house-121270",
                "water-tower-dd6200",
                "watering-can-b22758",
                "weapon-rack-082d7f",
                "wheelbarrow-13f422",
                "whiskey-bottle-and-glass-fa24b4",
                "windmill-3aad49",
                "wooden-bench-02e2c8",
                "wool-rug-ab2bd1",
                "woven-storage-basket-983d80",
        };

        /// <summary>[159차 P2] 던전 전용 수용분 — 실내 패스 배선 미지정. [160차 P3] 던전 배선은 CaveInteriorBuilder T11이 담당.
        /// [161차 P3] T4 제외는 SkipWiringCheckIds 통합 집합으로 이관 — 본 배열은 T10 역어서션·T11 배선 계약 유지용.</summary>
        private static readonly string[] DungeonReserveIds =
        {
            "stone-sarcophagus-cc96a4",
            "barred-cell-door-571e9c",
            "ossuary-skull-shelf-a8fdf0",
            "iron-bound-door-e0e953",
            "mimic-chest-57e440",
        };

        /// <summary>[161차 P3] T4 배선 검증 제외 통합 집합 → [무번호 15] 77종(기존 31종 + 설계도 외 제거 46종).
        /// 구성: 던전 배선 5(CaveInteriorBuilder T11) + 로프다리 1(CaveInteriorBuilder T11·161차 P3 가산)
        /// + 야외 수용분 3(수용만·미배치) + 야외 배선 10(PolyforkOutdoorStructures T12) + 실내 미배선 수용분 1(향후 배선 대기)
        /// + [무번호 15] 설계도 외 제거 46종(수용 유지·재배선 대기).
        /// 실내 배선 검증 대상 = 169 − 77 = 92종(T4).</summary>
        private static readonly string[] SkipWiringCheckIds =
        {
            "stone-sarcophagus-cc96a4",
            "barred-cell-door-571e9c",
            "ossuary-skull-shelf-a8fdf0",
            "iron-bound-door-e0e953",
            "mimic-chest-57e440",
            "rope-bridge-span-159f8c",
            "opera-box-tier-d7fdd1",
            "pante-n-chapel-62dccd",
            "cemetery-lantern-post-fed352",
            "ancestral-shrine-de7ed2",
            "blacksmith-forge-09fa85",
            "four-wheel-wagon-adb946",
            "livery-stable-c01ee0",
            "mine-headframe-735cf4",
            "open-stable-dbf2d5",
            "temple-bell-tower-745387",
            "water-mill-b31972",
            "wheelbarrow-13f422",
            "windmill-3aad49",
            "turning-stair-landing-066019",
            // [162차 P3] 육지 가산 6 + 호수 선박 5 — 야외/호수 패스 배선(T12)이 담당
            "mine-cart-21d7f0",
            "hayride-wagon-1641de",
            "flower-seller-s-hand-d617f9",
            "salvage-hand-cart-222304",
            "alpine-hay-barn-3965e5",
            "campfire-parent-b1c0d5",
            "small-fishing-boat-4ac0e3",
            "hanga-roa-fishing-boat-f09534",
            "ship-s-longboat-f159fd",
            "camp-canoe-c65d2a",
            "dock-cargo-barge-69dc4b",
                // [163차 P3] 건물 7 + 시설 5 + 소품 9 + 등대 1
                "old-lighthouse-48ca6c",
                "stone-chapel-03e774",
                "parish-church-543cf8",
                "porch-log-cabin-d2f3ef",
                "log-cabin-4fac3b",
                "adobe-colonial-farmhouse-cf13d8",
                "orongo-house-run-e35d16",
                "water-tower-dd6200",
                "water-pump-house-121270",
                "country-well-8feec8",
                "chicken-coop-371f97",
                "barrel-organ-cart-30799d",
                "trailhead-sign-aa29e1",
                "hide-rain-catchment-funnel-e41d94",
                "upgradeable-assembly-workbench-a18ff3",
                "chicha-barrel-77037f",
                "town-welcome-arch-d2af7d",
                "arched-passage-56393e",
                "tupa-stone-tower-801c13",
                "azalea-bush-7bb14d",
                "halloween-yard-scarecrow-cb0ffb",
                "modular-doorstep-bridge-df54e1",
            // [164차 P3] 건물 14 + 관문 3 + 탑 4 + 농가 2 + 벽 1 — T12 확장 담당
            "tenshu-keep-2ff636",
            "brick-church-322885",
            "governor-s-audience-hall-a4d777",
            "village-hall-c6d3c8",
            "village-tavern-4e94e3",
            "cove-tavern-5861a6",
            "bathhouse-4ac41a",
            "shopfront-house-43d94d",
            "thatched-cottage-0e63f5",
            "timber-wash-house-c7e7d5",
            "kura-storehouse-5c9631",
            "stilted-granary-3e7d7d",
            "dock-warehouse-bf0967",
            "village-gatehouse-181a9e",
            "arch-gatehouse-f4391b",
            "camp-gate-2de13f",
            "palisade-wall-621e29",
            "splay-leg-lookout-tower-6fae7e",
            "watchtower-140f78",
            "signal-beacon-tower-9c0d1a",
            "minka-farmhouse-680475",
            "machiya-townhouse-8ef64e",
            "gabled-farmhouse-c7534f",
            "stone-farmhouse-billet-2dbbf2",
            // [무번호 15] 설계도 외 제거 분 — 수용 유지·실내 배선 해제(41종). 재배선 시 본 집합에서 제거.
            "looted-gilded-throne-008fc3",
            "brass-candelabra-89ab76",
            "baroque-panelled-wall-8658ed",
            "concert-grand-piano-85c5b1",
            "harpsichord-ac1869",
            "gilded-harp-f32566",
            "overflowing-treasure-chest-3d6ee0",
            "velvet-mat-tile-0ccce3",
            "farm-lantern-747dc8",
            "curtain-panel-ee2ed9",
            "wool-rug-ab2bd1",
            "chest-of-drawers-cbe3e7",
            "table-candle-lantern-a7b5af",
            "dressing-mirror-7c0871",
            "framed-wall-art-d2c8a2",
            "fern-plant-1559b8",
            "leather-forge-bellows-7f6d42",
            "quench-barrel-0c78fd",
            "milk-churn-895735",
            "scroll-rack-f465dd",
            "ventilated-hide-food-larder-f53174",
            "flame-brazier-bbf74a",
            "hand-lantern-cdbe1a",
            "faculty-office-book-wall-c2d532",
            "library-book-stack-double-ce8d7f",
            "an-hourglass-with-sand-ec9f11",
            "alcove-bookcase-83894d",
            "spellbook-1e8dfe",
            "divination-orb-115d77",
            "archery-target-762034",
            "spear-e9824e",
            "quiver-722a40",
            "bow-9a225d",
            "commander-s-camp-stool-d7fa4c",
            "great-helm-2551bc",
            "weapon-rack-082d7f",
            "greatsword-a956f3",
            "shield-24b9f0",
            "hanging-plant-basket-66a002",
            "jewel-casket-975ecc",
            "spilled-coin-pile-caccf6",
            "standing-knight-statue-8bdcc9",
            // [무번호 88] 맷돌 — 설계도 정밀 재배치로 taheta 석기로 대체(수용 유지·미배선).
            "saddle-quern-food-mill-8e3657",
            // [무번호 88] 절차 전환 — 건축 스케일(벽난로 12.7m/옷장 10m/실험대 11.3m)은 GLB 종횡비 왜곡 회피로
            //   절차 빌더가 담당. 수용 유지·미배선(재배선 시 본 집합에서 제거).
            "dragonfire-brazier-6bc2ce",
            "wardrobe-f3b2f2",
            "alchemy-workbench-117b73",
            // [무번호 90] 약초/약재 소품 6종 — 수용만 완료(연금술실 배치는 별도 승인 라운드).
            "healing-herb-bundle-4d9724",
            "mushroom-cluster-18dc1d",
            "mushroom-cluster-f2e3ba",
            "mushroom-679e55",
            "giant-mushroom-cluster-868de6",
            "pastel-flowering-plant-d16a03",
            // [무번호 91] clay-storage-pot — MP_Jar_1_A(Interior/Props 파생 복제본) 원본 수용.
            //   Polyfork 경로 자체는 미배선(게임은 MP 경로 사용) — 재배선 시 본 집합에서 제거.
            "clay-storage-pot-626452",
            // [무번호 92] potion-bottle — UserProvided 물약 4종(potion_hp/mp/stealth/sedative) 파생의 원본 수용.
            //   Polyfork 경로 자체는 미배선(게임은 UserProvided 파생본 사용) — 재배선 시 본 집합에서 제거.
            "potion-bottle-099ad7",
        
            "copper-ore-chunk-258509", // [162차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "crystal-shard-9e0a83", // [162차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "gold-ore-chunk-6f1d9e", // [162차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "iron-ingot-36697d", // [162차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "iron-ore-chunk-1df386", // [162차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "live-rock-piece-6c7bdf", // [162차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "mineral-geode-cluster-299927", // [162차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "silver-ore-chunk-1e440c", // [162차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "pigeon-base", // [79-R19] 비둘기 전보 배선은 PigeonMessenger — 실내 패스 비대상
        
            "shovel-997c4d", // [162-5차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "hand-axe-cee9e7", // [162-5차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "mining-pickaxe-90bdf6", // [162-5차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "kite-shield-343e62", // [162-5차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "gold-coin-stack-a9ef52", // [162-5차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "coin-pouch-be4dd8", // [162-5차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "firewood-stack-8a2899", // [162-5차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용)
            "propped-fishing-rod-072978", // [165차] 낚싯대 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제본 사용·손장착 배선 별도 Phase)
            "charred-meat-haunch-be1ea2", // [163차] 음식 아이템 시각용 원본 수용 — 실내 배선 비대상(UserProvided meat_* 게임 경로 복제·파생본 사용)
            "silver-cloche-platter-25dd3e", // [163차] 음식 아이템 시각용 원본 수용 — 실내 배선 비대상(UserProvided cook_01 게임 경로 복제본 사용)
            "watering-can-b22758", // [163b차] 도구 아이템 시각용 원본 수용 — 실내 배선 비대상(UserProvided tool_watering_can 게임 경로 복제본 사용·demand 해소)
            "sunflower-seed-b4fbbd", // [163b차] 씨앗 아이템 시각용 원본 수용 — 실내 배선 비대상(UserProvided herb_seed_*·seed_herb 게임 경로 파생본 사용·demand 해소)
            // [172차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 게임 경로 복제·파생본 사용:
            //   gold_coin·silver_coin 복제 + mount_token 파생본. mount_token은 원본 없는 파생 전용 — PolyforkIds 미포함)
            "gold-coin-29afc1",
            "silver-coin-64ab27",
            "knuckle-ring-529610", // [173차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 파생본: ring_power 게임 id 사용)
            "prayer-bead-strand-e18d9a", // [173차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 파생본: necklace_hp·necklace_guard 사용)
            "signet-ring-efa0ee", // [173차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 파생본: dracula 전설 반지 사용)
            "simple-band-ring-929543", // [173차] 아이템 시각용 수용 — 실내 배선 비대상(UserProvided 파생본: ring_evasion·ring_vitality 사용)
            "koi-base", // [175차] 아이템 시각용 수용 — 게임 경로 UserProvided 복제본 사용(fish_common)
            "reef-grouper-964612", // [175차] 아이템 시각용 수용 — 게임 경로 UserProvided 복제본 사용(fish_rare)
            "coelacanth-base", // [175차] 아이템 시각용 수용 — 게임 경로 UserProvided 복제본 사용(fish_legendary)
        };

        private static string ReadSource(string relativePath)
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "..", relativePath));
        }

        // StringComparison 오버로드 미사용(구 런타임 CS1503 재발 방지) — Split(None) 기반 출현 계수.
        private static int CountOccurrences(string source, string needle)
        {
            return source.Split(new[] { needle }, System.StringSplitOptions.None).Length - 1;
        }

        // ── T1. Polyfork GLB 179종 파일 존재(raw glb는 직렬화 에셋이 아니라 Resources.Load 불가 — 파일 방식) ──
        // [무번호 90] 171종 → 177종 재생성 — 약초/약재 소품 6종(herb bundle+버섯 4+약초 화분) 수용 — ls 실측.
        // [무번호 91] 177종 → 178종 — clay-storage-pot 수용(Interior/Props/MP_Jar_1_A 파생 복제본과 1:1) — ls 실측.
        // [무번호 92] 178종 → 179종 — potion-bottle 수용(UserProvided 물약 4종 파생의 원본) — ls 실측.
        // [158차 P2] 13종 → 70종 확장 — Resources/Models/Polyfork 실측 .glb 전종(파일 목록 기준).
        // [160차 P3] 70종 → 94종 재생성 — Resources/Models/Polyfork 실측 .glb 전종(ls 파일 목록 기준).
        // [161차 P3] 94종 → 112종 재생성 — Resources/Models/Polyfork 실측 .glb 전종(ls 파일 목록 기준).
        // [163차] 112종 → 196종 — 음식 원본 2종(charred-meat-haunch·silver-cloche-platter) 포함 ls 실측 전체 재생성.
        // [163b차] 196종 → 198종 — 물뿌리개·씨앗 원본 2종(watering-can·sunflower-seed) 가산 — ls 실측 전체 재생성.
        // [172차] 198종 → 200종 — 코인 원본 2종(gold-coin-29afc1·silver-coin-64ab27) 가산 — ls 실측 전체 재생성.
        // [173차] 200종 → 204종 — 장신구 원본 4종(simple-band/knuckle/signet/prayer-bead-strand) 가산 — ls 실측 전체 재생성.
        // [175차] 204종 → 207종 — 물고기 원본 3종(koi-base·reef-grouper-964612·coelacanth-base) 가산 — ls 실측 전체 재생성.

        [Test]
        public void T1_PolyforkGlbResourcesExist_FileContract()
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "Resources", "Models", "Polyfork");
            foreach (string id in PolyforkIds)
            {
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(dir, id + ".glb")), Is.True,
                    $"Polyfork GLB 리소스 존재 — {id}.");
            }
        }

        // ── T2. CREDITS.md 매니페스트 — 라이선스 문구 + credit 라인 ──
        // [158차 P2] 13건 → 40건 갱신 — CREDITS.md 실측 credit 라인 40(에셋 70종 1:1).
        // [160차 P3] 40건 → 94건 갱신 — CREDITS.md 실측 credit 라인 94(에셋 94종 1:1).
        // [161차 P3] 94건 → 112건 갱신 — CREDITS.md 실측 credit 라인 112(에셋 112종 1:1).

        [Test]
        public void T2_CreditsManifest_LicenseAndCreditLines()
        {
            string creditsPath = System.IO.Path.Combine(
                Application.dataPath, "Resources", "Models", "Polyfork", "CREDITS.md");
            Assert.That(System.IO.File.Exists(creditsPath), Is.True, "CREDITS.md 매니페스트 존재.");
            string[] lines = System.IO.File.ReadAllLines(creditsPath);
            string joined = string.Join("\n", lines);
            Assert.That(joined, Does.Contain("CC BY 4.0"), "라이선스 문구 존재.");
            int creditLines = 0;
            foreach (string line in lines)
                if (line.Contains("by Polyfork")) creditLines++;
            // [79-R19] pigeon 1 + [88] 2 + [90] 6 + [91] clay-storage-pot 1 + [92] potion-bottle 1 + [162차] 광물 8종 — 실내 카탈로그 187종.
            // [165차] 193건 → 194건 — propped-fishing-rod-072978 수용(낚싯대 아이템 시각용 1종, ls 실측).
            // [163차] 194건 → 196건 — 음식 원본 2종(charred-meat-haunch-be1ea2·silver-cloche-platter-25dd3e) 수용(ls 실측).
            // [163b차] 196건 → 198건 — 물뿌리개·씨앗 원본 2종(watering-can-b22758·sunflower-seed-b4fbbd) 가산(ls 실측).
            // [172차] 198건 → 200건 — 코인 원본 2종 가산(ls 실측).
            // [173차] 200건 → 204건 — 장신구 원본 4종 가산(ls 실측, GLB 204종과 1:1).
            // [175차] 204건 → 207건 — 물고기 원본 3종 가산(ls 실측, GLB 207종과 1:1, 매니페스트 헤더 207 동기).
            Assert.That(creditLines, Is.EqualTo(207), "credit 라인 207건 — Polyfork GLB 207종과 1:1 대응.");
        }

        // ── T3. 배선 호출 하한 — [무번호 88] 설계도 정밀 재배치 후 실측 갱신 ──
        // [158차 P2] 하한 40 → 85 갱신 — 실측(grep 기준) 사전 83 + 이번 가산 2.
        // [160차 P3] 하한 126 → 149 갱신 — 실내 패스 실측(grep -c 기준).
        // [161차 P3] 하한 149 → 153 갱신 — 실내 패스 실측(grep -c 기준, 161차 P1·P1-b 가산 반영).
        // [무번호 15] 하한 153 → 94 갱신 — 설계도 외 제거 후 실측(grep -c 기준).

        [Test]
        public void T3_WireCountFloor_SourceContract()
        {
            string src = ReadSource(PassRelPath);
            Assert.That(CountOccurrences(src, "PlacePolyfork("), Is.GreaterThanOrEqualTo(94),
                "배선 호출 하한 94 — [무번호 15] 설계도 정합 재편 실측.");
        }

        // ── T4. 실내 배선 [무번호 88] 94종(171종 − T4 제외 통합 77종 — 재배선 4종 제외+맷돌 1종+절차 전환 3종 추가) 전종 배선 — 각 1회 이상 ──
        // [무번호 88] 절차 전환 3종(dragonfire 벽난로/wardrobe/alchemy-workbench — 건축 스케일 정확 치수 필요)
        //   제외 집합 가산 → 171 − 77 = 94종 검증.
        // [158차 P2] 13종 → 70종 전종 확장 — 배치 후 실측 40/40 배선.
        // [160차 P3] 70종 → 94종 전종 확장 — 실측 88/88 배선(던전 5종은 CaveInteriorBuilder T11 담당).
        // [161차 P3] 94종 → 112종 전종 확장 — 실측 92/92 배선(제외 20종 = 던전 5 + 로프다리 1 + 야외 수용분 3 + 야외 배선 10 + 실내 미배선 수용 1).

        [Test]
        public void T4_AllPolyforkIdsWiredInFurnishingPass_SourceContract()
        {
            string src = ReadSource(PassRelPath);
            foreach (string id in PolyforkIds)
            {
                // [159차 P2] 던전 전용 수용분(배선 미지정)은 배선 계약에서 제외 — T10이 미배선을 역어서션.
                // [160차 P3] 던전 배선은 CaveInteriorBuilder T11이 담당 — 실내 패스 비대상 유지.
                // [161차 P3] 제외 집합 SkipWiringCheckIds 통합(20종) — 배선 주체별 계약은 T11(던전+로프다리)·T12(야외)가 담당.
                bool reserved = false;
                foreach (string r in SkipWiringCheckIds)
                    if (r == id) { reserved = true; break; }
                if (reserved) continue;
                Assert.That(CountOccurrences(src, id), Is.GreaterThanOrEqualTo(1),
                    $"Polyfork id 배선 누락 — {id}.");
            }
        }

        // ── T5. 카탈로그 게이트 정의 존재 ──

        [Test]
        public void T5_PlacePolyforkDefinedInCatalog_SourceContract()
        {
            string src = ReadSource(CatalogRelPath);
            Assert.That(src, Does.Contain("static GameObject PlacePolyfork("),
                "카탈로그 게이트 API 정의 — 부모/이름/로컬좌표/요우/목표높이 시그니처.");
        }

        // ── T10. 던전 전용 수용분 — 미배선 역어서션(실내 패스, 배치는 별도 승인 사이클) ──
        // [159차 P2] 수용만 완료된 5종이 실수로 실내에 배선되지 않았음을 보장(성/건물 존 규칙 보호).
        // [160차 P3] 던전 배선은 CaveInteriorBuilder T11이 담당 — 본 테스트는 실내 패스 미배선만 역어서션(유지).

        [Test]
        public void T10_DungeonReserveIds_NotWiredInFurnishingPass_SourceContract()
        {
            string src = ReadSource(PassRelPath);
            foreach (string id in DungeonReserveIds)
            {
                Assert.That(CountOccurrences(src, id), Is.EqualTo(0),
                    $"던전 전용 수용분이 실내에 배선됨 — {id} (지하 5층 보강 별도 승인 대기).");
            }
        }

        // ── T11. 던전 소품 패스 — CaveInteriorBuilder 소스계약([160차 P3] 신설, [161차 P3] 6종 확장) ──
        // 던전 수용분 5종 + 로프다리 1종이 CaveInteriorBuilder에서 배선되는지 소스 리터럴로 검증(패스 정의+호출·구현 마커 주석 포함).

        [Test]
        public void T11_DungeonPropsWiredInCaveInteriorBuilder_SourceContract()
        {
            string src = ReadSource(CaveBuilderRelPath);
            foreach (string id in DungeonReserveIds)
            {
                Assert.That(CountOccurrences(src, id), Is.GreaterThanOrEqualTo(1),
                    $"던전 소품 id CaveInteriorBuilder 배선 누락 — {id}.");
            }
            // [161차 P3] 로프다리 가산 — 던전 소품 패스 6종째(존 중심 서측 오프셋·룸당 1·결정론 yaw).
            Assert.That(CountOccurrences(src, "rope-bridge-span-159f8c"), Is.GreaterThanOrEqualTo(1),
                "로프다리 CaveInteriorBuilder 배선 누락.");
            Assert.That(CountOccurrences(src, "CreateDungeonProps"), Is.GreaterThanOrEqualTo(2),
                "던전 소품 패스 정의+호출 — CaveInteriorBuilder 내 2회 이상.");
            Assert.That(src.Contains("[160차 P2]"), Is.True,
                "던전 소품 패스 구현 마커 주석 존재.");
            Assert.That(src.Contains("[161차 P3]"), Is.True,
                "던전 소품 패스 로프다리 가산 마커 주석 존재.");
        }

        // ── T6. 벽 횔도 자연 높이 — 벽 부착 계열 호출의 목표높이 인자 0(origin_y 빌드 상쇄) ──

        [Test]
        public void T6_WallTorchTargetHeightZero_SourceContract()
        {
            string[] passLines = ReadSource(PassRelPath).Replace("\r\n", "\n").Split('\n');
            int torchCalls = 0;
            foreach (string rawLine in passLines)
            {
                string line = rawLine.TrimEnd();
                if (line.Contains("wall-torch-bracket-f5ad14") && line.Contains("PlacePolyfork("))
                {
                    torchCalls++;
                    Assert.That(line.EndsWith(", 0f);"), Is.True,
                        $"벽 횔도 목표높이 0 계약 위반 — {line}");
                }
            }
            Assert.That(torchCalls, Is.GreaterThanOrEqualTo(1), "벽 횔도 배선 존재(실측 5).");
        }

        // ── T7. 로비 탁자 교체 — [무번호 15] 로비 탁자 제거(설계도 벤치 교체) — 주점+식당 2건 유지 ──

        [Test]
        public void T7_LobbyTableReplacedByPolyfork_SourceContract()
        {
            string src = ReadSource(PassRelPath);
            Assert.That(CountOccurrences(src, "timber-trestle-table-024d88"), Is.GreaterThanOrEqualTo(2),
                "탁자 교체 배선 하한 2 — [무번호 15] 로비 탁자 제거 후 주점+식당 유지.");
        }

        // ── T8. 로비 왕좌 — [무번호 15] 설계도 외 제거 역어서션(로비는 벽난로+벤치+횃불만) ──

        [Test]
        public void T8_LobbyThroneRemovedPerBlueprint_SourceContract()
        {
            string src = ReadSource(PassRelPath);
            Assert.That(CountOccurrences(src, "looted-gilded-throne-008fc3"), Is.EqualTo(0),
                "왕좌 제거 계약 — [무번호 15] 설계도 정합(로비 설계도 3요소 외 제거). 수용은 SkipWiringCheckIds 유지.");
        }

        // ── T9. 158차 P2 책장 2종 — [무번호 15] 설계도 외 제거 역어서션(연금/창고 책장 제거) ──

        [Test]
        public void T9_P2BookcasePairRemovedPerBlueprint_SourceContract()
        {
            string src = ReadSource(PassRelPath);
            Assert.That(CountOccurrences(src, "alcove-bookcase-83894d"), Is.EqualTo(0),
                "알코브 북케이스 제거 계약 — [무번호 15] 설계도 정합. 수용은 SkipWiringCheckIds 유지.");
            Assert.That(CountOccurrences(src, "faculty-office-book-wall-c2d532"), Is.EqualTo(0),
                "북월 제거 계약 — [무번호 15] 설계도 정합. 수용은 SkipWiringCheckIds 유지.");
        }

        // ── T12. 야외 건축물 패스 — PolyforkOutdoorStructures 소스계약([161차 P3] 신설) ──
        // 클래스 정의 + 야외 10종 전 id 배선 + 솔리드 게이트 호출 + 구현 마커 주석 검증(야외 패스 배선은 T4 실내 검증 제외).

        [Test]
        public void T12_OutdoorStructuresWired_SourceContract()
        {
            string src = ReadSource(OutdoorStructuresRelPath);
            Assert.That(src.Contains("class PolyforkOutdoorStructures"), Is.True,
                "야외 건축물 패스 클래스 정의 존재.");
            string[] outdoorIds =
            {
                "four-wheel-wagon-adb946",
                "wheelbarrow-13f422",
                "blacksmith-forge-09fa85",
                "water-mill-b31972",
                "windmill-3aad49",
                "livery-stable-c01ee0",
                "open-stable-dbf2d5",
                "mine-headframe-735cf4",
                "ancestral-shrine-de7ed2",
                "temple-bell-tower-745387",
                // [162차 P3] 육지 가산 6 + 호수 선박 5
                "mine-cart-21d7f0",
                "hayride-wagon-1641de",
                "flower-seller-s-hand-d617f9",
                "salvage-hand-cart-222304",
                "alpine-hay-barn-3965e5",
                "campfire-parent-b1c0d5",
                "small-fishing-boat-4ac0e3",
                "hanga-roa-fishing-boat-f09534",
                "ship-s-longboat-f159fd",
                "camp-canoe-c65d2a",
                "dock-cargo-barge-69dc4b",
                // [163차 P3] 야외 21 + 등대 1 + [164차 P3] 건물 14 + 관문 3 + 탑 4 + 농가 2 + 벽 1
                "old-lighthouse-48ca6c",
                "stone-chapel-03e774",
                "parish-church-543cf8",
                "porch-log-cabin-d2f3ef",
                "log-cabin-4fac3b",
                "adobe-colonial-farmhouse-cf13d8",
                "orongo-house-run-e35d16",
                "water-tower-dd6200",
                "water-pump-house-121270",
                "country-well-8feec8",
                "chicken-coop-371f97",
                "barrel-organ-cart-30799d",
                "trailhead-sign-aa29e1",
                "hide-rain-catchment-funnel-e41d94",
                "upgradeable-assembly-workbench-a18ff3",
                "chicha-barrel-77037f",
                "town-welcome-arch-d2af7d",
                "arched-passage-56393e",
                "tupa-stone-tower-801c13",
                "azalea-bush-7bb14d",
                "halloween-yard-scarecrow-cb0ffb",
                "modular-doorstep-bridge-df54e1",
                "tenshu-keep-2ff636",
                "brick-church-322885",
                "governor-s-audience-hall-a4d777",
                "village-hall-c6d3c8",
                "village-tavern-4e94e3",
                "cove-tavern-5861a6",
                "bathhouse-4ac41a",
                "shopfront-house-43d94d",
                "thatched-cottage-0e63f5",
                "timber-wash-house-c7e7d5",
                "kura-storehouse-5c9631",
                "stilted-granary-3e7d7d",
                "dock-warehouse-bf0967",
                "village-gatehouse-181a9e",
                "arch-gatehouse-f4391b",
                "camp-gate-2de13f",
                "palisade-wall-621e29",
                "splay-leg-lookout-tower-6fae7e",
                "watchtower-140f78",
                "signal-beacon-tower-9c0d1a",
                "minka-farmhouse-680475",
                "machiya-townhouse-8ef64e",
                "gabled-farmhouse-c7534f",
                "stone-farmhouse-billet-2dbbf2",
            };
            foreach (string id in outdoorIds)
            {
                Assert.That(CountOccurrences(src, id), Is.GreaterThanOrEqualTo(1),
                    $"야외 건축물 id 배선 누락 — {id}.");
            }
            Assert.That(CountOccurrences(src, "PlacePolyforkSolid("), Is.GreaterThanOrEqualTo(1),
                "야외 솔리드 배치 게이트 호출 존재(콜라이더 부착 배선).");
            Assert.That(src.Contains("[161차 P2]"), Is.True,
                "야외 건축물 패스 구현 마커 주석 존재.");
            Assert.That(src.Contains("PlaceLakeBoats"), Is.True,
                "호수 선박 패스 정의 존재([162차 P2]).");
        }

        // ── T13. 카탈로그 솔리드 게이트 — InteriorPropCatalog 소스계약([161차 P3] 신설) ──
        // 야외 패스가 사용하는 솔리드 배치 API 정의 + 렌더러 bounds BoxCollider 부착 존재 검증.

        [Test]
        public void T13_PlacePolyforkSolidDefinedInCatalog_SourceContract()
        {
            string src = ReadSource(CatalogRelPath);
            Assert.That(src, Does.Contain("static GameObject PlacePolyforkSolid("),
                "솔리드 배치 게이트 API 정의 존재(부모/이름/로컬좌표/요우/목표높이 시그니처).");
            Assert.That(src.Contains("AddComponent<BoxCollider>()"), Is.True,
                "솔리드 배치 BoxCollider 부착 존재(플레이어 통과 방지).");
        }
    }
}
