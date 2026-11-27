using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 49] 자산 손상 복구 + 디졸브 원본 복원 + 센티널 프로브 스팸 차단 계약.
    /// 실측 근거(11-15 48차 Play — 사용자 콘솔):
    ///  ①"Unknown error occurred while loading 'Assets/URP/Ground_Grass_Mat.mat'" — 자산 파일 자체가
    ///    NULL 채움으로 깨짐(전수 스캔 7건 — 6건은 미사용 에셋팩 데모, 본 프로젝트 참조는 이 1건).
    ///  ②시체 SMR null — 디졸브 램프 종료가 머티리얼 파괴만 하고 원본을 복원하지 않아 리스폰 재활성 시
    ///    null 슬롯 노출(47차 실측 재확인). 
    ///  ③센티널 텍스처 프로브가 _MainTex 부재 셰이더(SelectionRing/TelegraphRing/FresnelRim)에 경고 스팸.
    /// </summary>
    public class FxAssetRepairP49Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void GroundGrassMat_IsValidYaml_AfterRepair()
        {
            string src = Read("Assets/URP/Ground_Grass_Mat.mat");
            Assert.That(src.StartsWith("%YAML 1.1"), Is.True,
                "[49] 손상 .mat 복구 — Resources 쌍둥이 유효본으로 교체(NULL 채움 파일 폐기).");
            Assert.That(src, Does.Contain("Material:"),
                "[49] Unity 머티리얼 YAML 구조 존재.");
            Assert.That(src.Contains("\0"), Is.False,
                "[49] NUL 바이트 잔존 금지(손상 흔적).");
        }

        [Test]
        public void Dissolve_RestoresOriginalMaterial_BeforeDestroy()
        {
            // CRLF 정규화 — 파일이 CRLF(Windows)이므로 줄단위 Contains/인덱스 판정 전 정규화(결정론).
            string src = Read("Assets/Scripts/Systems/FxDissolveUtility.cs").Replace("\r\n", "\n");
            Assert.That(src, Does.Contain("[무번호 48-T3] 원본 승계 기록"),
                "[48-T3] 원본 승계 레지스트리 기록.");
            Assert.That(src, Does.Contain("_dissolveOriginals"),
                "[48-T3] 렌더러별 원본 머티리얼 보관.");
            Assert.That(src, Does.Contain("원본 복원 우선"),
                "[48-T3] 램프 종료 시 원본 복원 — 리스폰 재활성 null 노출 차단.");
            // foreach 중괄호 정합(중괄호 누락이 루프를 무의미화하는 사고 방지).
            Assert.That(src, Does.Contain("foreach (var rend in body.GetComponentsInChildren<Renderer>(true))\n            {"),
                "[48-T2] 루프 본체 중괄호 정합.");
            // 순서 고정: enabled=false → 승계 인스턴스 직접 파괴(게터 없음) → 원본 복원 라인 인덱스 비교.
            int disableIdx = src.IndexOf("r.enabled = false;   // [무번호 48-T2]", System.StringComparison.Ordinal);
            int restoreIdx = src.IndexOf("원본 복원 우선", System.StringComparison.Ordinal);
            int destroyIdx = src.IndexOf("Object.Destroy(inst);", System.StringComparison.Ordinal);
            Assert.Greater(disableIdx, 0, "[48-T2] 비활성 선언 존재.");
            Assert.Greater(restoreIdx, 0, "[48-T3] 원본 복원 존재.");
            Assert.Greater(destroyIdx, 0, "[49-T1] 승계 인스턴스 직접 파괴 존재.");
            Assert.Less(disableIdx, restoreIdx, "[48-T2/T3] 비활성 → 원본 복원 순서.");
            // [무번호 49-T1] .material 게터 완전 금지 — 클론 인스턴스화+슬롯 대입 부작용이 null 슬롯의 뿌리.
            Assert.That(src, Does.Not.Contain("r.material;"),
                "[49-T1] .material 게터 잔존 금지 — 게터는 슬롯을 클론으로 교체한다(파괴 시 null).");
            Assert.That(src, Does.Contain("_dissolveInsts"),
                "[49-T1] 인스턴스 승계 레지스트리 — 게터 없이 직접 파괴.");
        }

        [Test]
        public void Sentinel_ProbeSkipsTexturelessShaders()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            Assert.That(src, Does.Contain("if (!m.HasProperty(\"_MainTex\") && !m.HasProperty(\"_BaseMap\")) continue;"),
                "[48-T3] 텍스처 프로퍼티 부재 셰이더 스킵 — 경고 스팸 차단.");
        }
    }
}
