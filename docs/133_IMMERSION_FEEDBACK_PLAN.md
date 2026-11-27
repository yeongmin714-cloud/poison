# 133차: 실감 피드백 강화 계획 (사운드·타격 피드백·세계 생태)

## 사전 실측 결과 (중복 방지 — 이미 있는 것)
- **낮/밤**: DayNightCycle.cs 완성급(태양/달/ambient/skybox/안개/날씨 연동) — **계획 제외**
- **날씨**: WeatherManager + WeatherEffects + NPCWeatherBehavior(비에 우산/대피) + EnvironmentParticleController — **계획 제외**
- **사운드 인프라**: FootstepSoundController(표면 태그 변형 스위치 존재)·UISoundIntegrator(클릭/열기/닫기)·BiomeAmbientController — **컨트롤러는 있음**
- **전투 피드백**: SFX_Combat_Hit/Swing/Footstep/Parry 2종, CombatVFXController, TriggerCameraShake(PlayerMovement) — 전투는 이미 울림
- **채집 노드**: GatherChannelSystem(채널형, Progress 노출) + ResourceNode.ApplyWoodSway(채널 중 나무 흔들림) + EnsureStump(그루터기) — 흔들림/그루터기는 있음

## 실제 빈틈 (이번 계획 대상)
1. 채집 4활동(벌목/채광/채집/낚시)+농경의 **행동음 0건** (Sounds/SFX 폴더 비어있음)
2. 채집 **타격 파티클 0건** (ResourceNode/GatherChannelSystem에 VFX 참조 없음)
3. 타격 싱크 이벤트 부재 (Progress는 있으나 타격 순간 콜백 없음)
4. 획득 연출(130차 시네마) **무음** — 징글 없음
5. 주민=랜덤 배회만 — 시간표/군집/상호 반응 없음
6. 동물 생태 없음(새/나비)

---

## Phase 1 — 채집 히트 피드백 (최우선)

### 1-A. 사운드 에셋 확보 (선행 조건)
- 경로: Kenney.nl CC0 팩 다운로드(Impact Sounds/UI Audio/RPG Audio — 무료·재배포 가능) → `Assets/Resources/Sounds/SFX/` 배치
- 필요 클립: axe_chop(도끼 타격), pick_mine(곡괭이), herb_pick(뽑기), fishing_cast/fishing_bite/fishing_reel, water_splash(물뿌리개), item_get(징글), coin
- 대체 경로: 사용자 제공 원본 폴더 → 컨버전. 라이선스 기록을 docs에 남김

### 1-B. 채집 타격 이벤트 싱크 (GatherChannelSystem)
- 파일: `Assets/Scripts/Systems/GatherChannelSystem.cs` (Action _onComplete 존재, L29/L70)
- 추가: `public event Action<float> OnImpact;` — Progress가 타격 리듬 구간(0→1을 N등분)을 지날 때마다 발화(기존 ApplyWoodSway가 Progress를 읽는 것과 동일 소스 — 이중 판정 없음)
- ResourceNode는 OnImpact 구독 → 사운드+파티클+흔들림 강화를 같은 리듬에 싱크

### 1-C. 타격 파티클 프리팹 4종 신규
- 목재 칩(갈색 소형 사각 스폰), 돌 파편(회색), 풀잎(초록), 물방울/물보라
- 배치: `Assets/Resources/VFX/Gather/` + ResourceNode에서 Instantiate/Play(노드 위치 기준, 파괴 안전 — DestroyImmediate 아님 Play용이라 오브젝트 파괴만)
- 낚시: 입질 시 물결 링 파티클(FishingSystem 이벤트 위치에 스폰)

### 1-D. 사운드 재생 배선
- SoundRefinement.cs에 `GatherSoundController`(신규 소형 컨트롤러) 추가 — FootstepSoundController 선례 복제(같은 파일 내 3컨트롤러 패턴 유지)
- OnImpact → 활동 종류별 클립 PlayOneShot(3D 소스, 노드 위치)
- 완료 순간: HarvestForPlayer에서 item_get 징글 + 130차 AcquisitionCinema 연동(획득 연출 시작 시 징글 1회)

### 1-E. 카메라 흔들림+히트스톱
- TriggerCameraShake(0.05, 0.05) 타격마다 / 완료 타격은 (0.1, 0.1) — PlayerMovement 구르기 선례 수치 준용
- 히트스톱: 마지막 타격 0.05초 Time.timeScale 0.05→복원 (BulletTime.cs 존재 확인 후 재사용 가능하면 재사용)

### Phase 1 검증
- 컴파일 error CS=0 → EditMode 전수 baseline 11건 1:1
- 신규 소스계약 테스트: OnImpact 이벤트 존재, GatherSoundController 배선, 파티클 프리팹 4종 Resources 존재 (AssetDatabase 로드 계약)
- **Play 필수**: 소리 울림/파티클 크기/흔들림 강도는 육안+청각 판정 (사용자 승인 게이트)

## Phase 2 — 획득·UI·발소리 보강
- 획득 징글을 아이템 등급별 변주(일반/희귀/영웅 3버전 — 같은 징글 피치 변조로 3클립 절약)
- 발소리 표면 변형 실측: FootstepSoundController.GetSurfaceVariant가 지원하는 태그 전수 vs 실제 씬 지형 태그 대조 → 미충족 표면 클립 보강
- 월드맵/상점 등 창 열림음 통일성 점검(UISoundIntegrator 커버 외 창 목록화)

## Phase 3 — 주민 일상·동물 생태
- 주민 시간표: VillageNpcWanderer에 스케줄 레이어 추가 — 밤(DayNightCycle 시간 인용) 귀가/실내, 낮 우물·시장 근처 군집, 주민끼리 마주치면 3초 정지+대화 아이콘
- 플레이어 접근 반응: 2m 이내 통과 시 고개숙임 인사(애니 = 기존 TalkA 선례 활용 또는 소형 nod — 별도 클립 불필요하면 절약)
- 동물: 나비 배회(파티클/소형 스프라이트), 새 군집(접근 시 비상 이탈) — 성능 예산: 프로파일러 확인 후 개체수 결정
- 검증: 기존과 동일 컴파일+EditMode+Play

## Phase 4 — 환경 상시 모션 (별도 승인 항목)
- 풀/나뭇잎 셰이더 흔들림(URP 셰이더 그래프), 깃발/풍차 회전 — 비용 큼, Phase 1~3 완료 후 범위 재협의

## 실행 순서·게이트
- Phase 1 → QA → Play 승인 → Phase 2 → ... 순차. 각 Phase별 Play 육안 게이트 유지
- 구현/QA 서브에이전트 분리 위임 정책 준용 (타임아웃 시 기존 폴백 절차)
- 커밋은 사용자 별도 승인

## 리스크
- 사운드 에셋 다운로드 실패 시(네트워크) → 사용자 제공 경로 대기 (1-A가 Phase 1 전체의 선행 조건)
- 파티클 과다로 저사양 프레임 저하 → 개체수 상한 상수화 + Play 프로파일 실측
- 주민 시간표가 기존 미션/가드 배치와 충돌 가능 → NPC 스폰 스팟(중심 가까울수록 난이도 계약) 무변경 원칙, 이동만 확장
