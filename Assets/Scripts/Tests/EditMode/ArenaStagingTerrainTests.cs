// 2026-10-26: [125차 후속39] 아레나 스테이징 외부 Idyllic 지형 — 소스계약 테스트.
// 근거: 메인씬 지면 텍스처(TerrainTextureApplier 실측) + URP/Ground_Grass_Mat 리소스 실측 +
//       IdyllicPrefabs 8종 프리팹 ls 실측. ArenaInteriorTests.cs 스타일(네임스페이스/[Test]/소스텍스트계약) 복제.
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [125차 후속39] 야외 스테이징을 메인씬과 동일 Idyllic 지형으로 교체하는 계약 테스트.
    /// ① 잔디 머티리얼 로드 + 20타일 스케일 + 폴백 색상 유지 ② 지면 120×120m 확장
    /// ③ 데코 산포 메서드 존재 + Awake 호출 순서(SetupGround 직후) ④ 결정론 seed 18261
    /// ⑤ 데코 프리팹 8종 경로 존재 ⑥ Ground 자식 부착(후속33 N1 토글 계약) ⑦ 로비 금지구역 상수.
    /// </summary>
    public sealed class ArenaStagingTerrainTests
    {
        private const string SetupSource = "Assets/Scripts/UI/ArenaTestSetup.cs";

        [Test]
        public void T1_SetupGround_GrassMaterialLoad_TileScaleAndFallbackColor()
        {
            string src = System.IO.File.ReadAllText(SetupSource);
            Assert.That(src, Does.Contain("Resources.Load<Material>(\"URP/Ground_Grass_Mat\")"),
                "SetupGround는 메인씬 Idyllic 잔디 지면 머티리얼을 Resources 로드해야 한다.");
            Assert.That(src, Does.Contain("mainTextureScale = new Vector2(20f, 20f)"),
                "120m 지면은 6m 원본 타일 밀도 유지를 위해 20타일 반복이어야 한다.");
            Assert.That(src, Does.Contain("new Material(grassMat)"),
                "런타임 인스턴스 머티리얼 사용 — 에셋 무손상(T-G5-W2 선례).");
            Assert.That(src, Does.Contain("new Color(0.35f, 0.32f, 0.28f)"),
                "폴백 계약 — 리소스 부재 시 기존 갈색 색상 라인이 유지돼야 한다.");
        }

        [Test]
        public void T2_SetupGround_GroundScaledTo120m()
        {
            string src = System.IO.File.ReadAllText(SetupSource);
            Assert.That(src, Does.Contain("ground.transform.localScale = new Vector3(12f, 1f, 12f)"),
                "스테이징 지면은 120×120m로 확장돼야 한다(12배율 × Plane 10m).");
            Assert.That(src, Does.Not.Contain("new Vector3(6f, 1f, 6f)"),
                "구 60m 배율 잔존 금지 — 확장 계약 미이행 방지.");
        }

        [Test]
        public void T3_SetupStagingDeco_Exists_AndCalledAfterSetupGroundInAwake()
        {
            string src = System.IO.File.ReadAllText(SetupSource);
            Assert.That(src, Does.Contain("private void SetupStagingDeco()"),
                "데코 산포 메서드가 존재해야 한다.");
            // [147차] ArenaTestSetup 미개질 — 원계약 유지
            int idxGround = src.IndexOf("SetupGround();");
            int idxDeco = src.IndexOf("SetupStagingDeco();");
            Assert.That(idxGround, Is.GreaterThanOrEqualTo(0), "Awake의 지면 셋업 호출 존재.");
            Assert.That(idxDeco, Is.GreaterThan(idxGround),
                "Awake에서 데코 산포는 지면 셋업 직후에 호출돼야 한다(_ground 의존 순서 계약).");
            // 링 산포 + 카운트 계약 — 나무 8(4+4)/덤불 10(5+5)/바위 5(3+2)/잔디 24(12+12)
            Assert.That(src, Does.Contain("Mathf.Lerp(24f, 54f"),
                "데코는 원점 중심 링 24~54m 구간에 배치돼야 한다.");
            Assert.That(src, Does.Contain("Place(trees[0], trees[1], 4, 4"), "나무 두 종 4+4 배치.");
            Assert.That(src, Does.Contain("Place(bushes[0], bushes[1], 5, 5"), "덤불 두 종 5+5 배치.");
            Assert.That(src, Does.Contain("Place(rocks[0], rocks[1], 3, 2"), "바위 두 종 3+2 배치.");
            Assert.That(src, Does.Contain("Place(grass[0], grass[1], 12, 12"), "잔디 두 종 12+12 배치.");
            Assert.That(src, Does.Contain("attempt < 20"),
                "금지구역 재표본은 항목당 최대 20회로 제한돼야 한다(무한루프 방지).");
        }

        [Test]
        public void T4_Decos_Scatter_IsDeterministic_Seed18261()
        {
            string src = System.IO.File.ReadAllText(SetupSource);
            Assert.That(src, Does.Contain("new System.Random(18261)"),
                "데코 산포는 결정론 seed 18261을 사용해 매 실행 동일 배치여야 한다.");
            Assert.That(src, Does.Contain("결정론 seed 계약"),
                "결정론 주석이 유지돼야 한다.");
        }

        [Test]
        public void T5_Decos_PrefabPaths_Exist_OnDisk_AndInSource()
        {
            string src = System.IO.File.ReadAllText(SetupSource);
            string[] prefabPaths =
            {
                "IdyllicPrefabs/Trees/BlossomTree_01",
                "IdyllicPrefabs/Trees/BlossomTree_04",
                "IdyllicPrefabs/Bushes/Bush_01_01",
                "IdyllicPrefabs/Bushes/Bush_02_01",
                "IdyllicPrefabs/Rocks/Rock_Big_01",
                "IdyllicPrefabs/Rocks/Rock_Big_02",
                "IdyllicPrefabs/Grass/Grass_01",
                "IdyllicPrefabs/Grass/Grass_02",
            };
            foreach (var path in prefabPaths)
            {
                Assert.That(src, Does.Contain("Resources.Load<GameObject>(\"" + path + "\")"),
                    "데코 프리팹 로드 경로가 소스에 존재해야 한다: " + path);
                Assert.That(System.IO.File.Exists("Assets/Resources/" + path + ".prefab"), Is.True,
                    "Resources 데코 프리팹 파일이 실제 존재해야 한다: " + path);
            }
        }

        [Test]
        public void T6_Decos_AttachedToGround_ToggleContract()
        {
            // 후속33 N1 계약: ArenaTestRunner 폴링이 _ground SetActive 토글 → 데코는 Ground 자식으로
            // 부착돼 함께 비활성돼야 한다(별도 루트 생성 금지 — 실내 씬 잔존 방지).
            string src = System.IO.File.ReadAllText(SetupSource);
            Assert.That(src, Does.Contain("SetParent(ground.transform, true)"),
                "데코는 Ground 자식으로 부착돼야 한다(worldPositionStays로 부모 스케일 보상).");
            Assert.That(src, Does.Contain("ArenaStagingDeco_"),
                "데코 오브젝트 이름 접두 계약 유지.");
            Assert.That(src, Does.Contain("전부 Ground 자식으로 부착"),
                "N1 토글 계약 주석(데코=Ground 자식) 유지.");
        }

        [Test]
        public void T7_Decos_LobbyBanZone_Constants27and35()
        {
            // 로비 방 실측치 45.6×61.3m(원점 중심) — 데코 배치 금지구역.
            string src = System.IO.File.ReadAllText(SetupSource);
            Assert.That(src, Does.Contain("const float BanX = 27f;"), "금지구역 X 반경 27 상수.");
            Assert.That(src, Does.Contain("const float BanZ = 35f;"), "금지구역 Z 반경 35 상수.");
            Assert.That(src, Does.Contain("Mathf.Abs(x) < BanX && Mathf.Abs(z) < BanZ"),
                "금지구역 판정은 X/Z 두 축 모두 적용돼야 한다.");
        }
    }
}
