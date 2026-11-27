using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 46] 사망 FX 격리 + 파티클 머티리얼 대입 경로 교체 + 센티널 수리 머티리얼 계약.
    /// 실측 근거(11-15 45차 Play — Editor.log + 센티널 캡처 44_magenta_230932.png):
    ///  ①NRE 스택: DeathVFX.SpawnFlipbookSmoke(73행, sharedMaterial 단일 세터 내부) → AnimalAI.Die(1447) —
    ///    사망 후처리(전리품 바구니·드롭·디졸브·시체 정리) 전부 미실행 = 사용자 보고 3건의 직접 뿌리.
    ///  ②감지: "ParticleSystemRenderer path=DeathFlipbookSmoke slot=0 mat=null" — null 머티리얼 = 마젠타 빌보드.
    ///  ③캡처 판독: 마젠타 사각 다각형 + 거대 흰 사각 = 센티널의 URP/Lit 불투명 수리 머티리얼이 빌보드에서
    ///    흰 사각으로 렌더되는 2차 결함.
    /// </summary>
    public class FxDeathFxIsolationP46Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void DeathVFX_ArrayAssignment_AndNoGhostGO()
        {
            string src = Read("Assets/Scripts/Systems/DeathVFX.cs");
            Assert.That(src, Does.Contain("[무번호 46-T1]"), "[46] 마커.");
            // [계약 진화 47-b] 46차 배열 대입 → 47차 가드 경로 승계(원본 복원+1급 로그 추가 — 무피해 세션
            //   실측에서 배열 대입조차 NRE였음). 승계 후 가드 경로가 동일 계약(배열 대입+실패 폴백)을 담당.
            Assert.That(src, Does.Contain("FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, \"DeathFlipbookSmoke\")"),
                "[46→47] 배열 대입 가드 경로 승계(단일 세터 NRE 회피 — 센티널 수리 실증 경로 유지).");
            Assert.That(src, Does.Not.Contain("rnd.sharedMaterial = mat;"),
                "[46] NRE 유발 단일 세터 잔존 금지.");
            Assert.That(src, Does.Contain("[46-DEATHFX] 플립북 연기 렌더러 세팅 실패"),
                "[46] 대입 실패 경고 — 에셋 프리팹 폴백 신호.");
            Assert.That(src, Does.Contain("대입 실패 = 유령 GO 금지"),
                "[46] 실패 시 즉시 정리 — NRE가 Destroy 예약을 생략하던 무기한 잔존 차단.");
            // 폴백 신호 유지 — false 반환 시 PlayDeathVFX의 DeathSmoke 프리팹 경로가 이어받는다.
            Assert.That(src, Does.Contain("if (!SpawnFlipbookSmoke(position, scale))"),
                "[46] 프리팹 폴백 분기 유지(29-M3-2 계약 무손상).");
            // 유한 수명 파괴(25-A1) 유지.
            Assert.That(src, Does.Contain("Object.Destroy(go, life)"), "[25-A1] 유한 수명 파괴 유지.");
        }

        [Test]
        public void AnimalAI_DeathFxIsolation_PreservesLootChain()
        {
            string src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("[무번호 46-T2]"), "[46] 격리 마커.");
            Assert.That(src, Does.Contain("catch (System.Exception deathFxEx)"),
                "[46] 사망 FX 예외 격리 — Die() 중단 금지.");
            Assert.That(src, Does.Contain("[46-DIE] 사망 FX 격리"),
                "[46] 격리 발화 1급 로그.");
            // 격리 이후 사망 후처리 체인 무손상 — 순서 고정(FX → 사운드 → 바구니 → 드롭).
            int fxIdx = src.IndexOf("DeathVFX.PlayDeathVFX(transform.position, 1f)", System.StringComparison.Ordinal);
            int basketIdx = src.IndexOf("LootBasket.Create(transform.position)", System.StringComparison.Ordinal);
            Assert.Greater(fxIdx, 0, "[46] 사망 FX 호출 존재.");
            Assert.Greater(basketIdx, 0, "[46] 전리품 바구니 생성 존재.");
            Assert.Less(fxIdx, basketIdx, "[46] FX가 바구니보다 먼저 — 격리돼도 후처리 순서 보존.");
        }

        [Test]
        public void Sentinel_ParticleRepair_UsesTransparentSoftParticle()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            Assert.That(src, Does.Contain("[무번호 46-T3]"), "[46] 수리 머티리얼 마커.");
            Assert.That(src, Does.Contain("r is ParticleSystemRenderer"),
                "[46] 파티클 렌더러 판별 — 종별 수리 머티리얼 분기.");
            Assert.That(src, Does.Contain("_particleFallback = FxMaterialLibrary.CreateFlipbookSmoke()"),
                "[46] 파티클 수리 = 투명 소프트 파티클(연기 형태 유지 — 흰 사각 원인 제거).");
            Assert.That(src, Does.Contain("private static Material _particleFallback"),
                "[46] 정적 캐시 — 수리마다 인스턴스 누출 금지.");
            // 43 계약 승계 확인 — 메시 계열 웜그레이 수리 유지.
            Assert.That(src, Does.Contain("0.85f, 0.82f, 0.75f, 1f"), "[43] 메시 계열 웜그레이 수리 유지.");
            Assert.That(src, Does.Contain("MagentaSentinel_Fallback"), "[43] 폴백 이름 유지.");
        }
    }
}
