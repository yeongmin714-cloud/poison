using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [87차] 마을 주민 — 플레이어형 통일 — 소스 문자열 어서션(진실치 마커 검증).
    /// ①주민 키 = 1.8m 고정계약(51차 캐릭터키 계약 확장 — 실측 균등 스케일 자가수리+발밑 재접지)
    /// ②마을 배회 — VillagerWanderer(Idle↔Move Speed 계약/마을 반경 내 랜덤 목표/접지 오프셋 보존/결정론 시드)
    /// </summary>
    public sealed class VillagerWanderTests
    {
        private static string Read(string rel) => System.IO.File.ReadAllText(rel);

        [Test]
        public void Villager_Height_Calibrated_To_Player_180()
        {
            var src = Read("Assets/Scripts/Systems/VillageNpcSpawner.cs");
            Assert.That(src, Does.Contain("TerritoryBuilder.MeasureStandingHeight(npc, false)"),
                "[87차] 주민 키 실측 — TerritoryBuilder 계측기(53차 검증) 재사용");
            Assert.That(src, Does.Contain("npc.transform.localScale *= (1.8f / measuredH);"),
                "[87차] 1.8m 캘리브레이션 — 플레이어/병사 동일 계약(51차)");
            Assert.That(src, Does.Contain("rp.y += groundY - b.min.y;"),
                "[87차] 스케일 후 발밑 재접지 — 피벗 비발밑 GLB 대응");
        }

        [Test]
        public void Villagers_Attach_Wanderer_With_Deterministic_Seed()
        {
            var src = Read("Assets/Scripts/Systems/VillageNpcSpawner.cs");
            Assert.That(src, Does.Contain("AddComponent<VillagerWanderer>()"),
                "[87차] 배회 컴포넌트 부착");
            Assert.That(src, Does.Contain("wanderer.Init(center, villageRadius, SeedBase + GetHash($\"{modelKey}_{npcName}\"));"),
                "[87차] 결정론 시드 — 마을+주민 해시(재실행 동일 궤적)");
        }

        [Test]
        public void Wanderer_Follows_Soldier_Speed_Contract_And_Grounds()
        {
            var src = Read("Assets/Scripts/Systems/VillagerWanderer.cs");
            Assert.That(src, Does.Contain("_anim.SetFloat(\"Speed\", _walking ? 1f : 0f);"),
                "[87차] Soldier AC Speed 계약 — >0.5 Move 진입/<0.1 Idle 복귀(HumanoidClipDriver 동일)");
            Assert.That(src, Does.Contain("TerrainGenerator.GetHeightAt(p.x, p.z, ProjectName.Core.Data.BiomeType.Plains, TerrainSeed)"),
                "[87차] 접지 — GetHeightAt(TerrainSeed 42) 계약");
            Assert.That(src, Does.Contain("_yOffset = transform.position.y -"),
                "[87차] 발밑 오프셋 보존 — 캘리브레이션 후에도 지면 고정");
            Assert.That(src, Does.Contain("radius * 0.82f"),
                "[87차] 배회 한계 = 마을 반경×0.82 — 펜스/건물 안쪽 유지");
        }

        /// <summary>
        /// [93차 F3] 애니 폴리시 소스 어서션 — ①스트라이드-속도 자동 정합(첫 Walk 진입 캘리브레이션 1회:
        /// 접지국면 발 슬립 실측 → 실측속도 = 루트 속도 − 슬립 → [100차 F-3] 계약진화: measuredSpeed를
        /// walkSpeed에 채택 + animSpeed 1.0 고정 회귀, 실패 시 기존 고정값[성인 0.95/girl 0.85 +
        /// anim.speed 1.0] 유지) ②선회 보간 완화(회전속도 상한 360°/s + 계수 240→120°/s — RotateTowards
        /// 구조 유지·계수만). 근거: 영상4 1~9s 64프레임 판정 A- 잔여 ①미세 발 미끄러짐 ②방향 전환 즉시 회전.
        /// </summary>
        [Test]
        public void Wanderer_StrideCalibration_And_TurnCap_93()
        {
            var src = Read("Assets/Scripts/Systems/VillagerWanderer.cs");
            Assert.That(src, Does.Contain("[93차 F3]"), "[93차 F3] 마커 주석.");

            // ① 스트라이드 정합 — 개시/실측/보정/로그/폴백 계약.
            Assert.That(src, Does.Contain("TryBeginStrideCalibration();"),
                "[93차 F3] Walk 진입 시 캘리브레이션 개시(첫 Walk — 절단 시 제한 재시도).");
            Assert.That(src, Does.Contain("_walkSpeed - slideMean"),
                "[93차 F3] 실측속도 = 루트 속도 − 슬립(접지 발 전방 속도 역산 — 비순환 관측량).");
            Assert.That(src, Does.Contain("LockStrideCalibration("),
                "[100차 F-3] 캘리브레이션 방향 반전 — measuredSpeed를 walkSpeed에 채택(animSpeed 배속 산식 폐지 — 93차 계약진화).");
            Assert.That(src, Does.Contain("[VillagerWanderer] 스트라이드 정합 — model="),
                "[93차 F3] 스트라이드 정합 폴백 로그 계약(model/animSpeed) — 성공 로그는 [100차 F-3] 보행정합으로 계약진화.");
            Assert.That(src, Does.Contain("StrideAdoptWalkSpeedMin"),
                "[100차 F-3] 채택 walkSpeed 하한 클램프(실측 이탈치 방어 — 구 anim.speed 하한 계약진화).");
            Assert.That(src, Does.Contain("StrideAdoptWalkSpeedMax"),
                "[100차 F-3] 채택 walkSpeed 상한 클램프(animSpeed 올림 폐지 — StrideAnimSpeedMax 남용 제거).");
            Assert.That(src, Does.Contain("StrideMaxAttempts"),
                "[93차 F3] 개시 시도 상한(첫 구간 절단 대응 — 무한 재시도 금지).");
            Assert.That(src, Does.Contain("폴백 — 실측 실패"),
                "[93차 F3] 실패 시 기존 고정값 유지 계약(anim.speed=1.0 — walkSpeed 테이블 존중).");

            // ② 선회 보간 — 회전속도 상한 적용 + 기존 구조 유지.
            Assert.That(src, Does.Contain("TurnSpeedCapDegPerSec = 360f"),
                "[93차 F3] 선회 회전속도 상한 360°/s 상수.");
            Assert.That(src, Does.Contain("Mathf.Min(_turnDegPerSec, TurnSpeedCapDegPerSec)"),
                "[93차 F3] 상한 적용 — 계수가 상한을 넘지 못하게 고정.");
            Assert.That(src, Does.Contain("_turnDegPerSec = 120f"),
                "[93차 F3] 선회 계수 완화(240→120°/s — 방향 전환 즉시 회전 수리).");
            Assert.That(src, Does.Contain("Quaternion.RotateTowards(transform.rotation, want,"),
                "[93차 F3] 기존 RotateTowards 구조 유지(계수만 조정 — 구조 재작성 금지 계약).");
        }

        /// <summary>
        /// [94차 F3] 스트라이드 캘리브레이션 요건 완화 소스 어서션 — 근거: 마을씬 테스트 5 전 주민
        /// `스트라이드 정합 — model=... animSpeed=1.00 (폴백 — 실측 실패: 발 본=있음 샘플 8/7)` —
        /// 샘플 10 미달로 전량 폴백, 보정 미작동이 "여전히 미끄러짐"의 주원인. 수리 = 샘플 10→5 +
        /// 관측 윈도우 2.5→3.5s 완화. 걷기 재진입 재시도(최대 3회)/폴백 로그는 기존 유지. 고정값 계약은
        /// [100차 F-3] 계약진화로 하향(성인 1.4→0.95/girl 1.25→0.85 — 클립 유효 보행속도 실측 반영).
        /// </summary>
        [Test]
        public void Wanderer_StrideCalibration_RelaxedRequirements_94()
        {
            var src = Read("Assets/Scripts/Systems/VillagerWanderer.cs");
            Assert.That(src, Does.Contain("[94차 F3]"), "[94차 F3] 마커 주석.");
            Assert.That(src, Does.Contain("StrideMinSamples = 5"),
                "[94차 F3] 최소 샘플 10→5 완화(샘플 8/7 전량 폴백 제거 — 보정 animSpeed 실반영 경로 확보).");
            Assert.That(src, Does.Contain("StrideCalibrationWindow = 3.5f"),
                "[94차 F3] 관측 윈도우 2.5→3.5s 완화(보행 사이클 ~3회 분 확보 — 샘플 수집 여유).");
            Assert.That(src, Does.Not.Contain("StrideMinSamples = 10"),
                "[94차 F3] 구 요건(10) 잔존 금지 — 완화 이행 확인.");
            Assert.That(src, Does.Not.Contain("StrideCalibrationWindow = 2.5f"),
                "[94차 F3] 구 윈도우(2.5s) 잔존 금지 — 완화 이행 확인.");
            Assert.That(src, Does.Contain("StrideMaxAttempts = 3"),
                "[94차 F3] 걷기 재진입 재시도 유지(최대 3회 — 기존 계약 무변경).");
            Assert.That(src, Does.Contain("폴백 — 실측 실패"),
                "[94차 F3] 폴백 로그 유지(스트라이드 정합 — model=... animSpeed=... — 폴백 경로 계약 승계).");
            Assert.That(src, Does.Contain("WalkSpeedDefault = 0.95f"),
                "[100차 F-3] 초기 기본값 하향(성인 1.4→0.95 — 클립 유효 보행속도 실측 0.81~1.05 반영, 94차 고정값 계약진화).");
            Assert.That(src, Does.Contain("WalkSpeedGirl = 0.85f"),
                "[100차 F-3] 초기 기본값 하향(girl 1.25→0.85 — 클립 유효 보행속도 실측 반영, 94차 고정값 계약진화).");
        }

        /// <summary>
        /// [96차 F4] 스트라이드 폴백 재시도 소스 어서션 — 근거: 94차 실측 girl2 샘플 0/girl3 샘플 2(걷기 시간이
        /// 관측창 3.5s보다 짧아 샘플 부족) 폴백 잔존 = 영구 폴백으로 보정 기회 상실. 수리 = 폴백 시 영구 잠금
        /// 폐기 — 재시도 소진(StrideMaxAttempts 3회) 후에도 다음 걷기 사이클마다 쿨다운 경유 재시도(성공할
        /// 때까지). 성공 경로는 [100차 F-3] 계약진화(measuredSpeed→walkSpeed 채택 + animSpeed 1.0 회귀),
        /// 폴백 로그/재시도 계약은 기존 유지.
        /// </summary>
        [Test]
        public void Wanderer_StrideCalibration_RetryUntilSuccess_96()
        {
            var src = Read("Assets/Scripts/Systems/VillagerWanderer.cs");
            Assert.That(src, Does.Contain("[96차 F4]"), "[96차 F4] 마커 주석.");

            // ① 폴백 재시도 대기 상태 + 쿨다운 상수(스팸 방지).
            Assert.That(src, Does.Contain("private bool _strideFailedPendingRetry"),
                "[96차 F4] 폴백 재시도 대기 필드 — 영구 폴백 폐기의 데이터 기반.");
            Assert.That(src, Does.Contain("StrideRetryCooldownSeconds = 10f"),
                "[96차 F4] 재시도 쿨다운 상수(시도 간 간격 — 재시도/로그 스팸 방지).");

            // ② 성공 확정만 영구 잠금 — 폴백 경로는 잠금 없이 재시도 대기로 전환.
            int lockCount = 0, idx = 0;
            while ((idx = src.IndexOf("_strideCalibrated = true;", idx, System.StringComparison.Ordinal)) >= 0)
            {
                lockCount++;
                idx += 5;
            }
            Assert.That(lockCount, Is.EqualTo(1),
                "[96차 F4] _strideCalibrated 잠금은 성공 경로 1곳만 — 폴백 경로 잠금 제거(영구 폴백 폐기).");
            Assert.That(src, Does.Contain("_strideFailedPendingRetry = true;"),
                "[96차 F4] 폴백 경로 — 재시도 대기 전환(잠금 대체).");
            Assert.That(src, Does.Contain("_strideRetryCooldownLeft = StrideRetryCooldownSeconds;"),
                "[96차 F4] 폴백 경로 — 쿨다운 설정(다음 걷기 사이클 재시도 간격).");

            // ③ 개시 게이트 — 성공 잠금/쿨다운/소진 상한 3단 계약.
            Assert.That(src, Does.Contain("if (_strideCalibrated) return;"),
                "[96차 F4] 성공 확정만 개시 차단(영구 잠금 — 보정 이탈 방지).");
            Assert.That(src, Does.Contain("if (_strideFailedPendingRetry && _strideRetryCooldownLeft > 0f) return;"),
                "[96차 F4] 쿨다운 중 개시 보류 — 사이클당 재시도 간격 보장.");
            Assert.That(src, Does.Contain("if (!_strideFailedPendingRetry && _strideAttempts >= StrideMaxAttempts) return;"),
                "[96차 F4] 소진 전 절단 재시도 상한 유지(기존 계약) — 소진 후 폴백 대기면 쿨다운 경유 재시도.");

            // ④ 쿨다운 감소 경로 — 매 프레임(비정지) 감소.
            Assert.That(src, Does.Contain("_strideRetryCooldownLeft - dt"),
                "[96차 F4] 쿨다운 감소 경로 존재(Update 경유 — 사이클 경과 후 재시도 허용).");

            // ⑤ 기존 계약 유지 — 성공 경로([100차 F-3] 채택 잠금 경유)/폴백 로그/소진 기준.
            Assert.That(src, Does.Contain("LockStrideCalibration(adopted)"),
                "[100차 F-3] 성공 경로 — 채택 잠금 경유(measuredSpeed→walkSpeed 채택 + animSpeed 1.0 회귀 — 96차 잠금 계약 승계).");
            Assert.That(src, Does.Contain("폴백 — 실측 실패"),
                "[96차 F4] 기존 폴백 로그 유지(시도 소진 조건 판별 가능 — 94차 로그 계약 승계).");
            Assert.That(src, Does.Contain("StrideMaxAttempts = 3"),
                "[96차 F4] 재시도 소진 기준(3회) 유지 — 소진 후에도 영구 폴백 아님.");
        }

        /// <summary>
        /// [100차 F-3] 소스 어서션 — 모델별 walkSpeed 보정 캐시 채택 + 보정 확정 잠금. 근거: 세션당 1회 실측
        /// 계약 — 첫 실측 성공 모델의 채택치를 ModelWalkSpeedCache에 보관하고, 이후 같은 모델 스폰/캘리브레이션
        /// 재실행(대화 정지→재개 포함)에서 재실측 없이 채택. LockStrideCalibration은 채택 walkSpeed 적용 +
        /// animSpeed 1.0 고정 회귀(배속 보정 폐지 — 문워크 근원 제거) + 성공 잠금/재시도 대기 해제
        /// ([96차 F4] 성공 경로 유일 잠금 계약 승계). 채택치는 StrideAdoptWalkSpeedMin/Max 클램프 방어.
        /// </summary>
        [Test]
        public void Wanderer_WalkSpeedCacheAdopt_And_LockContract_100F3()
        {
            var src = Read("Assets/Scripts/Systems/VillagerWanderer.cs");

            Assert.That(src, Does.Contain("[100차 F-3]"), "[100차 F-3] 마커 주석.");

            // ① 모델별 캐시 — 스태틱 사전(플레이 세션 생명주기 = 세션당 계약 — 도메인 리로드 시 초기화).
            Assert.That(src, Does.Contain("private static readonly System.Collections.Generic.Dictionary<string, float> ModelWalkSpeedCache"),
                "[100차 F-3] 모델별 walkSpeed 보정 캐시 — 스태틱 사전(세션당 1회 실측 계약).");
            Assert.That(src, Does.Contain("ModelWalkSpeedCache[_modelKey] = adopted;"),
                "[100차 F-3] 채택치 캐시 적재 — 첫 실측 성공 모델(재실행 채택용).");

            // ② 채택 클램프 — 실측 이탈치가 walkSpeed에 직행하지 않게 방어.
            Assert.That(src, Does.Contain("StrideAdoptWalkSpeedMin = 0.5f"),
                "[100차 F-3] 채택 walkSpeed 하한 0.5m/s(이탈치는 측정 오류 간주).");
            Assert.That(src, Does.Contain("StrideAdoptWalkSpeedMax = 1.5f"),
                "[100차 F-3] 채택 walkSpeed 상한 1.5m/s(이탈치는 측정 오류 간주).");
            // [128-2 R 계약 진화] 러너 분기 — 러너는 채택 상한이 RunnerAdoptWalkSpeedMax(2.6)로 분기,
            // 비러너는 기존 StrideAdoptWalkSpeedMax(1.5) 유지(계약 진화 — 100차 상수 계약 자체는 불변).
            Assert.That(src, Does.Contain("Mathf.Clamp(measuredSpeed, StrideAdoptWalkSpeedMin, _isRunner ? RunnerAdoptWalkSpeedMax : StrideAdoptWalkSpeedMax);"),
                "[128-2 R] 채택치 클램프 — 러너 한정 상한 분기(비러너=기존 1.5 계약 유지).");

            // ③ 캐시 채택 경로 — 같은 모델 스폰/캘리브레이션 재실행에서 재실측 없이 채택.
            Assert.That(src, Does.Contain("private bool TryAdoptCachedWalkSpeed()"),
                "[100차 F-3] 캐시 채택 경로 존재(대화 정지→재개 포함 — 재실측 없음).");
            Assert.That(src, Does.Contain("LockStrideCalibration(cached);"),
                "[100차 F-3] 캐시 재사용 — 보정치 채택 + 보정 확정 처리(잠금 경유).");
            Assert.That(src, Does.Contain("슬립=캐시재사용"),
                "[100차 F-3] 캐시 재사용 로그(세션당 1회 실측 계약 — 모델 보정치 채택).");

            // ④ 보정 확정 잠금 — 채택 walkSpeed 적용 + animSpeed 1.0 고정 회귀 + 성공 잠금.
            Assert.That(src, Does.Contain("private void LockStrideCalibration(float adoptedSpeed)"),
                "[100차 F-3] 보정 확정 잠금 메서드 존재(채택 walkSpeed 적용 단일 경로).");
            Assert.That(src, Does.Contain("_walkSpeed = adoptedSpeed;"),
                "[100차 F-3] measuredSpeed → walkSpeed 채택 적용(속도를 애니에 맞춤 — 배속 산식 폐지).");
            Assert.That(src, Does.Contain("_animSpeed = 1.0f;"),
                "[100차 F-3] animSpeed 1.0 고정 회귀(배속 보정 폐지 — 문워크 근원 제거).");
            Assert.That(src, Does.Contain("_strideCalibrated = true;"),
                "[100차 F-3] 성공 확정 잠금([96차 F4] 성공 경로 유일 잠금 계약 승계).");
            Assert.That(src, Does.Contain("_strideFailedPendingRetry = false;"),
                "[100차 F-3] 성공 — 재시도 대기 해제([96차 F4] 폴백 재시도 계약과의 결합).");
        }

        // ── [99차 F-C] 골격정합 프로브/이름 기반 리맵 — 더미 골격 구성(리맵 후 bones 동일성)+계약 소스 단정 ──

        [Test]
        public void VillagerWander_SkeletonProbe_And_NameRemap_DummySkeleton_99FC()
        {
            // ① 계약 소스 단정 — C-1/C-2/C-3 API 존재 + 스폰/진단 경로 배선 + 로그 태그.
            var spawnerSrc = Read("Assets/Scripts/Systems/VillageNpcSpawner.cs");
            var wandererSrc = Read("Assets/Scripts/Systems/VillagerWanderer.cs");
            Assert.That(spawnerSrc, Does.Contain(
                    "internal static SkeletonProbe ProbeSkeletonAlignment(GameObject root, Animator anim)"),
                "[99차 F-C] C-1 골격정합 프로브 API(SMR 전수 실측 — rootBone 경로/외부 본/hips Δ).");
            Assert.That(spawnerSrc, Does.Contain(
                    "internal static int RemapSmrToAnimator(GameObject root, Animator anim)"),
                "[99차 F-C] C-2 이름 기반 리맵 API(bones[] 재바인딩+rootBone 교체).");
            Assert.That(spawnerSrc, Does.Contain(
                    "internal static float MeasureHipsMismatch(GameObject root, Animator anim)"),
                "[99차 F-C] C-3 SMR-Animator hips 불일치 계량 API(본은 움직여도 메시 동결 감지 기준).");
            Assert.That(spawnerSrc, Does.Contain("internal const string ProbeHipsBoneName = \"spine\";"),
                "[99차 F-C] hips 대표 본명 = \"spine\"(RigifyHumanMap 91차 F6 Hips 슬롯 계약 재사용).");
            Assert.That(spawnerSrc, Does.Contain("var skelProbe = ProbeSkeletonAlignment(npc, anim);"),
                "[99차 F-C] C-1 스폰 시 프로브 배선.");
            Assert.That(spawnerSrc, Does.Contain("[99차 F-C] 골격정합 — model="),
                "[99차 F-C] C-1 프로브 로그 태그(smr=N 불일치=M).");
            Assert.That(spawnerSrc, Does.Contain("[99차 F-C] 골격 리맵 — smr="),
                "[99차 F-C] C-2 리맵 성공 로그 태그(교체본=N).");
            Assert.That(spawnerSrc, Does.Contain("[99차 F-C] 골격 리맵 불가 — 동명 본 부재"),
                "[99차 F-C] C-2 리맵 불가 경고 로그 태그.");
            Assert.That(spawnerSrc, Does.Contain("if (replaced > 0) anim.Rebind();"),
                "[99차 F-C] C-2 리맵 후 골격 재바인딩 계약.");
            Assert.That(wandererSrc, Does.Contain("VillageNpcSpawner.MeasureHipsMismatch(gameObject, _anim)"),
                "[99차 F-C] C-3 진단 틱 — hips 불일치 거리 소비 배선.");
            Assert.That(wandererSrc, Does.Contain("VillageNpcSpawner.RemapSmrToAnimator(gameObject, _anim)"),
                "[99차 F-C] C-3 불일치 판정 시 이름 리맵 선행 배선.");

            // ② 더미 골격 구성 — Animator 골격(동명 본) + 외부 골격에 바인딩된 SMR(3차 근거: GLB 리그/중복
            //    골격 재현 — 외부 본 2개 + hips 5m 이격). EditMode 런타임 구성 최소화(프로브/리맵 API 직결).
            var animGo = new GameObject("FC99_Anim");
            var foreignRoot = new GameObject("FC99_Foreign");
            GameObject meshGo = null;
            Mesh mesh = null;
            try
            {
                var anim = animGo.AddComponent<Animator>();
                var aSpine = new GameObject("spine");
                aSpine.transform.SetParent(animGo.transform);
                aSpine.transform.position = new Vector3(0f, 1f, 0f);
                var aArm = new GameObject("arm_L");
                aArm.transform.SetParent(aSpine.transform);
                aArm.transform.position = new Vector3(0f, 1.2f, 0f);

                foreignRoot.transform.position = new Vector3(5f, 0f, 0f);   // 5m 이격 — hips 불일치 재현
                var fSpine = new GameObject("spine");
                fSpine.transform.SetParent(foreignRoot.transform);
                fSpine.transform.position = new Vector3(5f, 1f, 0f);
                var fArm = new GameObject("arm_L");
                fArm.transform.SetParent(fSpine.transform);
                fArm.transform.position = new Vector3(5f, 1.2f, 0f);

                meshGo = new GameObject("Fc99_Mesh");
                meshGo.transform.SetParent(foreignRoot.transform);
                var smr = meshGo.AddComponent<SkinnedMeshRenderer>();
                mesh = new Mesh { name = "FC99_ProbeMesh" };
                mesh.vertices = new[] { Vector3.zero, Vector3.right };
                mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity };
                mesh.boneWeights = new[]
                {
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 1, weight0 = 1f },
                };
                smr.sharedMesh = mesh;
                smr.rootBone = fSpine.transform;
                smr.bones = new[] { fArm.transform, fSpine.transform };   // 외부(중복) 골격 바인딩

                // ③ C-1 프로브 — 외부 본 존재+hips 5m 이격 → 불일치 판정.
                var probe = VillageNpcSpawner.ProbeSkeletonAlignment(foreignRoot, anim);
                Assert.That(probe.SmrCount, Is.EqualTo(1), "[99차 F-C] C-1 SMR 전수 카운트.");
                Assert.That(probe.HasMismatch, Is.True,
                    "[99차 F-C] C-1 외부 골격 바인딩 시 불일치 판정(본은 움직여도 렌더 메시 동결 유형 재현).");
                Assert.That(probe.MismatchCount, Is.EqualTo(1), "[99차 F-C] C-1 불일치 SMR 수.");
                Assert.That(probe.Detail, Does.Contain("불일치"), "[99차 F-C] C-1 SMR별 상세 로그(불일치 마킹).");

                float hipsBefore = VillageNpcSpawner.MeasureHipsMismatch(foreignRoot, anim);
                Assert.That(hipsBefore, Is.EqualTo(5f).Within(0.01f),
                    "[99차 F-C] C-3 hips 불일치 거리 — Animator hips vs SMR 골격 hips 월드 거리.");
                Assert.That(hipsBefore, Is.GreaterThan(0.01f),
                    "[99차 F-C] C-1 불일치 임계(>0.01m) 초과 — 프로브 판정 조건.");

                // ④ C-2 이름 기반 리맵 — bones[]/rootBone이 animator 골격 동명 본으로 치환(bones 동일성).
                int replaced = VillageNpcSpawner.RemapSmrToAnimator(foreignRoot, anim);
                Assert.That(replaced, Is.EqualTo(3),
                    "[99차 F-C] C-2 교체 본 수 — bones 2(arm_L/spine)+rootBone 1.");
                Assert.That(smr.bones[0], Is.EqualTo(aArm.transform),
                    "[99차 F-C] C-2 bones 동일성 — arm_L → animator 골격 본.");
                Assert.That(smr.bones[1], Is.EqualTo(aSpine.transform),
                    "[99차 F-C] C-2 bones 동일성 — spine → animator 골격 본.");
                Assert.That(smr.rootBone, Is.EqualTo(aSpine.transform),
                    "[99차 F-C] C-2 rootBone 교체 — animator 골격 소속화.");

                // ⑤ 리맵 후 프로브 — 외부 본 0+hips Δ 0으로 수렴(골격 정합).
                var probeAfter = VillageNpcSpawner.ProbeSkeletonAlignment(foreignRoot, anim);
                Assert.That(probeAfter.HasMismatch, Is.False,
                    "[99차 F-C] C-2 리맵 후 불일치 해소(SMR가 Animator 골격에 스키닝).");
                float hipsAfter = VillageNpcSpawner.MeasureHipsMismatch(foreignRoot, anim);
                Assert.That(hipsAfter, Is.EqualTo(0f).Within(0.01f),
                    "[99차 F-C] C-3 리맵 후 hips 불일치 Δ≈0.");
            }
            finally
            {
                if (meshGo != null) Object.DestroyImmediate(meshGo);
                Object.DestroyImmediate(foreignRoot);
                Object.DestroyImmediate(animGo);
                if (mesh != null) Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>
        /// [101차 F-3] 소스 어서션 — 모델별 보행 베이크 상수 테이블 + 잔존 슬립 animSpeed 트림 + 캘리브레이션
        /// 폴백 전용. 근거: [100차 F-3] 채택 후에도 슬립 ±0.05~0.19m/s 잔존(테스트12 — man2 −0.19) +
        /// 캘리브레이션 대기 시간 제거 요구("캐릭터마다 애니메이션을 각각 수정"). 수리 = ①실측 확정 7모델
        /// (girl1 0.80/girl2 0.85/girl3 0.97/man1 1.10/man2 1.11/oldman1 1.06/oldman2 1.07) 상수 테이블 —
        /// 첫 스폰부터 정합 ②잔존 슬립 >0.1m/s 모델 animSpeed 미세 트림(0.9~1.1 클램프 — trim =
        /// walkSpeed/(walkSpeed−슬립)) ③캘리브레이션은 테이블 미지 모델 폴백 전용(베이크 모델은
        /// LockStrideCalibration 경유 채택 잠금 — 유일 잠금 경로 계약 승계).
        /// </summary>
        [Test]
        public void Wanderer_ModelWalkBakeTable_And_SlipTrim_101F3()
        {
            var src = Read("Assets/Scripts/Systems/VillagerWanderer.cs");

            Assert.That(src, Does.Contain("[101차 F-3]"), "[101차 F-3] 마커 주석.");

            // ① 베이크 상수 테이블 — 실측 확정치 7모델(첫 스폰부터 정합 — 캘리브레이션 대기 제거).
            Assert.That(src, Does.Contain("ModelWalkBakeTable"),
                "[101차 F-3] 모델별 보행 베이크 상수 테이블.");
            Assert.That(src, Does.Contain("BakeWalkSpeedGirl1 = 0.80f"),
                "[101차 F-3] girl1 베이크 walkSpeed 0.80(실측 확정).");
            Assert.That(src, Does.Contain("BakeWalkSpeedGirl2 = 0.85f"),
                "[101차 F-3] girl2 베이크 walkSpeed 0.85(실측 확정).");
            Assert.That(src, Does.Contain("BakeWalkSpeedGirl3 = 0.97f"),
                "[101차 F-3] girl3 베이크 walkSpeed 0.97(실측 확정).");
            Assert.That(src, Does.Contain("BakeWalkSpeedMan1 = 1.10f"),
                "[101차 F-3] man1 베이크 walkSpeed 1.10(실측 확정).");
            Assert.That(src, Does.Contain("BakeWalkSpeedMan2 = 1.11f"),
                "[101차 F-3] man2 베이크 walkSpeed 1.11(실측 확정).");
            Assert.That(src, Does.Contain("BakeWalkSpeedOldman1 = 1.06f"),
                "[101차 F-3] oldman1 베이크 walkSpeed 1.06(실측 확정).");
            Assert.That(src, Does.Contain("BakeWalkSpeedOldman2 = 1.07f"),
                "[101차 F-3] oldman2 베이크 walkSpeed 1.07(실측 확정).");
            Assert.That(src, Does.Contain("{ \"girl1\", BakeWalkSpeedGirl1 }"),
                "[101차 F-3] 테이블 적재 — girl1(모델키 계약).");
            Assert.That(src, Does.Contain("{ \"oldman2\", BakeWalkSpeedOldman2 }"),
                "[101차 F-3] 테이블 적재 — oldman2(모델키 계약).");

            // ② 잔존 슬립 >0.1m/s 모델 한정 animSpeed 미세 트림(0.9~1.1 클램프).
            Assert.That(src, Does.Contain("ResidualSlipTrimGate = 0.1f"),
                "[101차 F-3] 트림 개입 게이트 — |잔존 슬립| >0.1m/s 모델 한정.");
            Assert.That(src, Does.Contain("AnimSpeedTrimMin = 0.9f"),
                "[101차 F-3] animSpeed 트림 하한 클램프(스펙 지정).");
            Assert.That(src, Does.Contain("AnimSpeedTrimMax = 1.1f"),
                "[101차 F-3] animSpeed 트림 상한 클램프(스펙 지정).");
            Assert.That(src, Does.Contain("Mathf.Clamp(baked / (baked - slip), AnimSpeedTrimMin, AnimSpeedTrimMax)"),
                "[101차 F-3] 트림 산식 — walkSpeed/(walkSpeed−슬립) 후 0.9~1.1 클램프.");
            Assert.That(src, Does.Contain("{ \"man2\", -0.19f }"),
                "[101차 F-3] 잔존 슬립 실측 수록 — man2 −0.19m/s(테스트12 — 애니 빠름 → 감속 트림).");
            Assert.That(src, Does.Contain("Mathf.Abs(slip) > ResidualSlipTrimGate"),
                "[101차 F-3] 게이트 적용 — |슬립| ≤0.1 모델은 트림 없음(animSpeed 1.0 유지 계약).");

            // ③ 캘리브레이션은 테이블 미지 모델 폴백 전용 — 베이크 경로는 채택 잠금 경유 스킵.
            Assert.That(src, Does.Contain("ModelWalkBakeTable.TryGetValue(modelKey, out float baked)"),
                "[101차 F-3] 베이크 테이블 조회 — SetWalkModel 최우선 경로.");
            Assert.That(src, Does.Contain("LockStrideCalibration(baked);"),
                "[101차 F-3] 베이크 채택 잠금 — 기존 단일 잠금 경로 승계(_strideCalibrated 잠금 1곳 계약 유지).");
            Assert.That(src, Does.Contain("[101차 F-3] 보행 베이크 — model="),
                "[101차 F-3] 베이크 로그 계약(model/walkSpeed(테이블)).");
            Assert.That(src, Does.Contain("슬립목표=<0.1"),
                "[101차 F-3] 베이크 로그 슬립목표 표기(잔존 슬립 <0.1m/s 목표 계약).");
        }
    }
}
