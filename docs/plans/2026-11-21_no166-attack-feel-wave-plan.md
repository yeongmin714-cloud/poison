# 166차 계획 — 전투 감각 + 자산 정합 웨이브 (사용자 요청 6건)

> 상태: 계획 확정 대기. Play/커밋은 별도 승인 게이트 유지.

## A. 말 GLB — 갈색말 → 황마 GLB 사용 (소형)
**실측 근거**
- HorseMountSystem.Breeds(L27~34): 백마/적토마/갈색말 3종. 갈색말 = `Models/UserProvided/말/갈색말`(백마 bcf틴트 파생, [무번호 90/91])
- 황마 GLB는 **이미 존재** — `Models/UserProvided/말/황마` (MountAssetContractTests가 리소스 GLB 3종(백마/적토마/황마) 존재 어서션 중)
- 전 말 GLB 동일 40노드 리그 → 컨트롤러(갈색말_AC=백마 클론) 그대로 재사용 가능

**작업**
1. HorseMountSystem.cs Breeds: 갈색말 resPath `"Models/UserProvided/말/갈색말"` → `"Models/UserProvided/말/황마"` (ctrlPath/breed 문자열 유지 — 세이브 호환)
2. 파생 갈색말 GLB 삭제(UserProvided/말/갈색말 + .meta) — CREDITS 파생 행 유무 확인(파생은 별도 행 없을 가능성, 있으면 정리)
3. MountAssetContractTests 갱신: GLB 존재 목록에서 "갈색말" 제거(황마 유지) + 갈색말 breed의 resPath=황마 매핑 어서션 추가
**검증**: EditMode baseline 유지 / (Play 승인 시) 갈색말 스폰 → 황마 GLB 외형 실측

## B. 공격 판정 — 스윙 2회 → 스윙별 판정 (중형, Play 트림 동반)
**실측 근거**
- HumanoidClipDriver L1103~1115: 레거시 Attack* 상태 — `_legacyImpactFired` 1회 플래그 + `normalizedTime >= 0.5f` 단일 임팩트 (공격 1입력 = 판정 1회)
- 콤보 경로는 이미 스윙별 판정 존재: PlayerCombat.ResolveComboSwingHit(int comboStage) L1617 — 레거시(찌르기) 경로만 단일 판정
- 사용자 관측: 찌르기 애니 클립에 휘두르기 2회 박혀 있음 → 판정도 2회 필요

**작업**
1. 판독: PlayerCombat.TryAttack(L1459) → HumanoidClipDriver 임팩트 시점 연결 구조 확정 + Attack 클립의 스윙 2회 구간(스윙별 정점 normalizedTime) 파악
2. HumanoidClipDriver: 레거시 임팩트 단일(0.5) → **스윙별 멀티임팩트** — 무기별 스윙 임팩트 시간 테이블(초기값 2회: 예 0.30/0.75, 그립테이블 패턴 준수) + 각 스윙마다 판정 호출(ResolveComboSwingHit 재사용 또는 레거시 판정 함수 2회)
3. 판정 쿨다운/데미지 중복 방지: 동일 공격 입력 내 스윙 2회가 같은 적을 2타하도록(사용자 의도) — 콤보 스윙 판정의 중복 가드가 2타를 막는지 확인하고 레거시 경로는 막지 않게
**검증**: 신규 EditMode 테스트(멀티임팩트 테이블+발화 구조 소스 어서션) / Play 실측 대기: 스윙별 정점 타이밍 상수 트림(영상→판정→트림 루프)

## C. Slash VFX — 휘두를 때마다 스윙 방향 부착 (중형, Play 트림 동반)
**실측 근거**
- `Assets/Free Slash VFX/` 프리팹 존재: **Slash VFX.prefab**(원본) + Earth/Electric/Fire/Water 변형 + Impact/Multiple Impact/Multiple Slashes
- 발화 후보 지점: 스윙 판정/트레일 발화 지점(WeaponSwingTrail.Pulse L1903 / AttackArcVFX / ResolveComboSwingHit의 PlayWeaponSwingSound 위치)

**작업**
1. Slash VFX.prefab → 스윙 시작마다 Instantiate → **휘두르는 방향**(플레이어 forward + 스윙 궤적 평면)으로 회전 정렬, 파괴는 VFX 자관리(자기 수명) — FishingLineVisual 수명 패턴 선례
2. 기존 AttackArcVFX/WeaponSwingTrail과의 중복 판단: 기존 트레일 유지 + Slash VFX는 스윙 1회당 1장(둘 다 켜고 Play에서 미감 판정 — 과하면 기존 트레일 게이트)
3. URP 호환 확인(Free Slash VFX 쉐이더) — 호환 불량 시 slash5-HungNguyen(shadergraph, URP) 대안
**검증**: 신규 EditMode 테스트(발화 훅 소스 어서션) / Play 대기: 크기·각도·지속시간 트림

## D. 피격 효과음 — 몬스터 피격 시 (소형)
**실측 근거**
- 피격 **비주얼은 이미 존재**(Magic Hit 2, PlayImpactMulti — CombatFXGate/PlayerCombat 경로, L1667 주석) — 소리만 없음
- 효과음 후보 이미 확보: `Resources/Audio/SFX/SFX_Combat_Hit.wav` (프로젝트 내) / SFX_Monster_Alert.wav
- 사운드 시스템 선례: AttackSoundLayerManager.PlaySwing(무기별 토큰 재활용) / GatherFeedbackSystem(SFX+VFX 쌍)

**작업**
1. 피격 확정 지점(데미지 실제 적용 — AttackTarget/ResolveComboSwingHit/레거시 임팩트 공통 종착)에서 SFX_Combat_Hit 발화 + 무기별 pitch/volume 프로파일 초기값(무기 특성별 차등 — HitStopManager의 무기별 차등 선례)
2. "실감나는" = 랜덤 피치 마이크로베리에이션(±) + 히트스톱과 동시 발화 정합
**검증**: 신규 EditMode 테스트(발화 훅 소스 어서션) / Play 대기: 음색/볼륨 판정 — SFX_Combat_Hit이 밋밋하면 후보 교체(Hovl/Vefects 내부 SFX 탐색)

## E. 아이콘 — 정면 전체 모습 통일 (중형)
**실측 근거**
- GblItemIconRenderer.TryMount: Instantiate 후 1.8m 높이핏 + 카메라가 `center + (dist*0.35, 0, -dist)` **사선** 고정 → GLB마다 베이크 오리엔테이션이 달라 "제각각 다른 방향"
- 142차 H: 수평/수직 반각 확장으로 잘림 수리 완료(전체 모습 프레이밍은 이미 존재) — 방향만 문제

**작업**
1. 카메라 정면 고정: `center + (0, 0, -dist)` — 사선 제거
2. 오리엔테이션 자동 정규화: 렌더러 bounds 최장축을 수평(X)으로 맞추는 회전 후보 산출(가로형) / 비좁형(헬멧 등)은 원본 그대로 정면 — 초기 휴리스틱
3. per-key 방향 오버라이드 테이블(홀드 config와 같은 패턴) — 자동 정규화가 어긋나는 종만 수기 지정
**검증**: 신규 EditMode 테스트(정면 카메라+테이블 소스 어서션) / Play 대기: 대표 아이콘(낚싯대·검·헬멧·말) 방향 샘플 판정

## F. Test_15 비둘기 제외 (소형)
**실측 근거**
- Test15PigeonBoot.cs(2829B) — Test_15_Livelihood 씬 가드 자가부트([79-R19]): 플레이 즉시 전보 기록+FlushDayLog → 비둘기 즉시 스폰
- 외부 등록 참조 0건(자가부트형) — 제거 시 다른 시스템 영향 없음(본 게임 비둘기는 DailyWarLogSystem 경로 무관)

**작업**
1. Test15PigeonBoot 삭제(파일+.meta) — 또는 [RuntimeInitializeOnLoadMethod] 게이트 해제(파일 삭제 우선, 테스트 참조 존재 시 테스트 갱신)
**검증**: EditMode(비둘기 부트 관련 테스트 회귀 없음) / Play 대기: Test_15 비둘기 미스폰 확인

## 실행 순서 + 공통 게이트
- **Wave A (소형 3건)**: A 말 + F 비둘기 + D 피격음 — 1 subagent
- **Wave B (전투 감각 2건)**: B 판정 분리 + C Slash VFX — 1 subagent (같은 축 PlayerCombat/HumanoidClipDriver)
- **Wave C (아이콘)**: E — 1 subagent
- 각 Wave: EditMode 전체 baseline(failed 10 유지, 신규 실패 0) + CS=0 + 문서(ROADMAP) 갱신 + TestOutput 리포트
- **Play/커밋은 별도 승인** — Play 실측 대기 항목: B 스윙 타이밍, C VFX 미감, D 음색, A 말 외형, E 아이콘 방향, F 15씬 비둘기
