using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [201차] 투척 폭탄 GLB 부착 계약 — 구형(Sphere)은 물리 전용 유지, 시각은 bomb GLB로 대체.
    /// 실제 렌더/투척은 Play 영역 — EditMode에서는 소스계약만 검증(UTKClickRippleTests 관례 동일).
    /// 폴백 계약: GLB 부재/bounds 무효 시 구형 렌더 유지 무음 폴백(177차 발사체 시각 계약 준용).
    /// </summary>
    public class BombThrownVisual201Tests
    {
        private const string Src = "Assets/Scripts/Systems/BombArmController.cs";

        [Test]
        public void ThrownBomb_GlbVisualAttached_SphereRenderOnlyReplaced()
        {
            string s = System.IO.File.ReadAllText(Src);
            Assert.That(s, Does.Contain("AttachThrownBombVisual(go);"), "SpawnThrownBomb에서 GLB 부착 호출");
            Assert.That(s, Does.Contain("Models/UserProvided/bomb_explosive"), "1순위 GLB bomb_explosive(Explosive 매핑)");
            Assert.That(s, Does.Contain("Models/UserProvided/bomb\""), "폴백 GLB bomb(체인)");
            Assert.That(s, Does.Contain("mr.enabled = false"), "구형 MeshRenderer OFF(시각 전용 대체)");
        }

        [Test]
        public void ThrownBomb_PhysicsPipeline_Unharmed()
        {
            string s = System.IO.File.ReadAllText(Src);
            Assert.That(s, Does.Contain("sc.isTrigger = false;"), "SphereCollider 물리 유지(렌더 교체 무관)");
            Assert.That(s, Does.Contain("rb.linearVelocity = velocity;"), "Rigidbody 초기속도 유지");
            Assert.That(s, Does.Contain("bomb.bombDamage = "), "[193차] 스탯 기반 피해량 주입 유지");
            Assert.That(s, Does.Contain("bomb.explosionSound = Resources.Load<AudioClip>"), "[189차] 폭발 SFX 배선 유지");
            Assert.That(s, Does.Contain("go.AddComponent<BombExplosionVisual>();"), "[194/195차] 폭발 시각 유지");
        }

        [Test]
        public void ThrownBomb_ScaleNormalizedToFallbackGuard()
        {
            string s = System.IO.File.ReadAllText(Src);
            Assert.That(s, Does.Contain("bombScale / maxDim"), "GLB 최대축 → 구형 지름(bombScale) 정규화");
            Assert.That(s, Does.Contain("Destroy(vis);"), "bounds 무효 시 비주얼 제거(구형 유지) 가드");
            Assert.That(s, Does.Contain("무음 폴백"), "폴백 계약 주석 핀(구형 렌더 유지)");
        }
    }

    /// <summary>
    /// [201차] 병사 장비 착용 배선 계약 — 무장비 원인(Instance null 조용히 스킵) 소멸 + 무기 롤 신설.
    ///  ① GES 런타임 부트(BootIfMissing — Play 전용·DDOL·멱등)
    ///  ② SpawnEquipment 상단 부트 1줄
    ///  ③ 무기 100% 착용(gp.WeaponItem = RollWeaponLoadout) — kind 4종 랜덤 + id 규약 weapon_{kind}_{tier}
    ///  ④ 티어 밴드 헬퍼(레벨 높을수록 고급 — 등급이 레벨 인자로 롤)
    ///  ⑤ GVAS 자가 생성(착용 → GLB 부착 배선 보장)
    /// </summary>
    public class GuardEquipmentWiring201Tests
    {
        private const string GesSrc = "Assets/Scripts/Systems/GuardEquipmentSystem.cs";
        private const string SpawnerSrc = "Assets/Scripts/Systems/GuardEquipmentSpawner.cs";

        [Test]
        public void GES_RuntimeBoot_PresentAndIdempotent()
        {
            string s = System.IO.File.ReadAllText(GesSrc);
            Assert.That(s, Does.Contain("public static void BootIfMissing()"), "GES 런타임 부트 메서드");
            Assert.That(s, Does.Contain("if (Instance != null || !Application.isPlaying) return;"),
                "멱등 + Play 전용 가드(EditMode 테스트 경로 무손상)");
            Assert.That(s, Does.Contain("DontDestroyOnLoad(go);"), "부트 GO 영속(Awake 바인딩 + 씬 전환 유지)");
        }

        [Test]
        public void SpawnEquipment_BootsSystemInsteadOfSilentSkip()
        {
            string s = System.IO.File.ReadAllText(SpawnerSrc);
            Assert.That(s, Does.Contain("GuardEquipmentSystem.BootIfMissing();"),
                "SpawnEquipment 상단 부트 — 무장비 스킵 원인 소멸");
            Assert.That(s.IndexOf("BootIfMissing", System.StringComparison.Ordinal),
                Is.LessThan(s.IndexOf("ItemRarity rarity = RarityProbabilityTable.Roll(guardLevel);", System.StringComparison.Ordinal)),
                "부트는 등급 롤보다 선행(착용 보장 순서)");
        }

        [Test]
        public void WeaponLoadout_AlwaysEquipped_FourKindsRandomTierByLevel()
        {
            string s = System.IO.File.ReadAllText(SpawnerSrc);
            Assert.That(s, Does.Contain("gp.WeaponItem = RollWeaponLoadout(level, gearBonus);"),
                "무기 100% 착용(병사 무장 필수) — WeaponItem은 시각+전리품 공용 소스");
            Assert.That(s, Does.Contain("string[] kinds = { \"sword\", \"spear\", \"bow\", \"dagger\" };"),
                "무기 kind 4종 랜덤(검/창/활/단검 — GLB 16종 전종 실존)");
            Assert.That(s, Does.Contain("$\"weapon_{kind}_{tier}\""), "무기 id 규약 weapon_{kind}_{tier}(인벤 무기 16종)");
            Assert.That(s, Does.Contain("weapon_sword_{tier}"), "DB 부재 폴백 — 검 규약 1회 폴백");
            Assert.That(s, Does.Contain("static string TierFromRarity"), "티어 밴드 헬퍼(공용 추출 — 이중 로직 금지)");
            Assert.That(s, Does.Contain("RarityProbabilityTable.Roll(level)"), "등급은 레벨 인자로 롤(레벨 높을수록 고급 밴드)");
        }

        [Test]
        public void VisualAttachSystem_BootGuaranteedInSpawnPath()
        {
            string s = System.IO.File.ReadAllText(SpawnerSrc);
            Assert.That(s, Does.Contain("GuardVisualAttachSystem.Instance == null"),
                "착용 데이터만 있고 부착 시스템 부재 상태 방지(자가 생성 가드)");
            Assert.That(s, Does.Contain("go.AddComponent<GuardVisualAttachSystem>();"),
                "부착 시스템 자가 생성(RequestRefreshFor 자가힐 패턴 준용)");
            int loadoutIdx = s.IndexOf("SpawnPlayerLoadout(gp, guardLevel, gp.GearDifficultyBonus);", System.StringComparison.Ordinal);
            int bootIdx = s.IndexOf("GuardVisualAttachSystem.Instance == null", System.StringComparison.Ordinal);
            Assert.That(bootIdx, Is.GreaterThan(loadoutIdx), "부트는 로드아웃 이후(착용 후 부착 순서)");
        }
    }

    /// <summary>
    /// [201차] 병사 장비 크기 정규화 계약 — 손에 쥐는 슬롯(무기/방패)만 병사 신장 비율 스케일.
    /// 신체 착용 슬롯(Helmet/Armor/Shoes/Gloves)은 본 공간 자동 전파 유지(부착 핏 무손상).
    /// </summary>
    public class GuardAttachScale201Tests
    {
        private const string Src = "Assets/Scripts/Systems/GuardVisualAttachSystem.cs";

        [Test]
        public void HandItems_SoldierHeightNormalized()
        {
            string s = System.IO.File.ReadAllText(Src);
            Assert.That(s, Does.Contain("void NormalizeHandItemScale(GameObject visual, GameObject body, string visualId)"),
                "손에 쥐는 장비 신장 비율 정규화(병사 크기에 맞춘 부착)");
            Assert.That(s, Does.Contain("slot == VisualSlot.Weapon || slot == VisualSlot.Back"),
                "적용 슬롯 = 무기+방패(Back=hand.L 방패) 한정");
            Assert.That(s, Does.Contain("guardH * ratio"), "목표 크기 = 병사 신장 × 슬롯 비율");
            Assert.That(s, Does.Contain("desired / curMax"), "실측 bounds 기반 균일 배수 보정(GLB 자연 크기 편차 흡수)");
            Assert.That(s, Does.Contain("if (guardH <= 0.01f) return;"), "신장 측정 실패 무동작 가드(파손 방지)");
            Assert.That(s, Does.Contain("static float MeasureGuardHeight"), "병사 신장 측정 헬퍼(전 렌더러 bounds)");
        }

        [Test]
        public void HandItemRatios_KindSpecificPinned()
        {
            string s = System.IO.File.ReadAllText(Src);
            Assert.That(s, Does.Contain("return 0.95f;"), "창 0.95×신장(긴 사거리 무기)");
            Assert.That(s, Does.Contain("return 0.75f;"), "활 0.75×신장");
            Assert.That(s, Does.Contain("return 0.40f;"), "단검 0.40×신장");
            Assert.That(s, Does.Contain("return 0.70f;   // sword 및 기타"), "검 기본 0.70×신장");
            Assert.That(s, Does.Contain("internal const float ShieldWidthRatio = 0.40f;"), "방패 0.40×신장");
            Assert.That(s, Does.Contain("visualId.Contains(\"shield\")"), "방패/무기 비율 분기(visualId 키워드)");
        }
    }
}
