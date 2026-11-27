using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 48] 파티클 중복 AddComponent 근원 수리 + 시체 디졸브 렌더 창 차단 계약.
    /// 실측 근거(11-15 47차 Play — Editor.log 11858~11905행):
    ///  ①"Can't add component 'ParticleSystemRenderer' to FxEmberRise because such a component is already
    ///    added" — AddComponent&lt;ParticleSystem&gt;()가 렌더러를 자동 부착하므로 재 AddComponent는 에러+null →
    ///    머티리얼 미적용 = 마젠타 빌보드(세션 파손 가설 폐기 — 마젠타 사가 최종 뿌리).
    ///  ②"Setting the duration while system is still playing" — 자동 재생 상태에서 모듈 설정 거부.
    ///  ③시체 SMR — 디졸브 램프 종료가 머티리얼 파괴 후 비활성 순서였어 null 슬롯 렌더 창 존재.
    /// </summary>
    public class FxParticleDuplicateAddP48Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void ParticleRenderers_UseGetComponentNotDuplicateAdd()
        {
            string death = Read("Assets/Scripts/Systems/DeathVFX.cs");
            Assert.That(death, Does.Contain("[무번호 48-T1]"), "[48] 마커.");
            Assert.That(death, Does.Contain("var rnd = go.GetComponent<ParticleSystemRenderer>();"),
                "[48] 자동 부착본 사용 — 중복 AddComponent(null 반환) 제거.");
            Assert.That(death, Does.Not.Contain("AddComponent<ParticleSystemRenderer>()"),
                "[48] 중복 AddComponent 잔존 금지(마젠타 연기 진짜 뿌리).");
            Assert.That(death, Does.Contain("ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)"),
                "[48] 자동 재생 정지 — 재생 중 모듈 설정 거부 차단.");
            Assert.That(death, Does.Contain("파티클 렌더러 획득 실패 — 에셋 프리팹 폴백으로 승계"),
                "[48] 렌더러 null 시 폴백 신호(46-T1 계약 유지).");

            string ember = Read("Assets/Scripts/Systems/FxDebrisSpawner.cs");
            Assert.That(ember, Does.Contain("var rnd = go.GetComponent<ParticleSystemRenderer>();"),
                "[48] FxEmberRise 동일 수리 — 마젠타 불티 뿌리 제거.");
            Assert.That(ember, Does.Not.Contain("AddComponent<ParticleSystemRenderer>()"),
                "[48] FxEmberRise 중복 AddComponent 잔존 금지.");
            Assert.That(ember, Does.Contain("ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)"),
                "[48] FxEmberRise 자동 재생 정지.");
        }

        [Test]
        public void CorpseDissolve_NoNullRenderWindow()
        {
            string src = Read("Assets/Scripts/Systems/FxDissolveUtility.cs");
            Assert.That(src, Does.Contain("[무번호 48-T2] 소멸 완료 — 머티리얼 파괴 전 비활성"),
                "[48-T2] 비활성 선수행 — null 슬롯 렌더 창 금지.");
            Assert.That(src, Does.Contain("rend.sharedMaterial.name.Contains(\"FxRimOverlay\")"),
                "[48-T2] 림 오버레이 디졸브 제외 — _MainTex 경고+null _BaseMap 승계 차단.");
            // 순서 고정(49-T1 승계): enabled=false → 승계 인스턴스 직접 파괴 → 원본 복원(라인 인덱스 비교).
            int disableIdx = src.IndexOf("r.enabled = false;   // [무번호 48-T2]", System.StringComparison.Ordinal);
            // 파괴 탐색은 비활성 위치 이후부터 — TryBeginDissolve 실패 경로의 조기 Destroy(inst)와 구분.
            int destroyIdx = disableIdx > 0 ? src.IndexOf("Object.Destroy(inst);", disableIdx, System.StringComparison.Ordinal) : -1;
            int restoreIdx = src.IndexOf("원본 복원 우선", disableIdx > 0 ? disableIdx : 0, System.StringComparison.Ordinal);
            Assert.Greater(disableIdx, 0, "[48-T2] 비활성 선언 존재.");
            Assert.Greater(destroyIdx, 0, "[48-T2→49-T1] 머티리얼 파괴 존재(승계 인스턴스 직접 파괴).");
            Assert.Greater(restoreIdx, 0, "[48-T3] 원본 복원 존재.");
            Assert.Less(disableIdx, destroyIdx, "[48-T2] 비활성이 파괴보다 먼저.");
            Assert.Less(destroyIdx, restoreIdx, "[48-T3] 파괴 후 원본 복원(슬롯 항상 유효).");
        }
    }
}
