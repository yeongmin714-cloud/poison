# 핫바 라벨 제거 + 은신 애니 + 인벤토리 예시2/3 재구조화 + 스탯창 미표시 수정 계획 (2026-09-09 3차)

> 실행 규약: 코드/QA delegate_task 위임(타임아웃 지속 시 부모 직접), 배치컴파일 CS=0 게이트, 3중 저장+텔레그램.
> 선행: 2026-09-09 1차(스탯창 v1/v2), 2차(오류수정+미니맵 로컬뷰) 계획서들.

## 0. 유저 요구 정리 (59/60.PNG + 인벤토리 예시 2·3 기준)

1. **59.PNG**: 핫바 슬롯 안 글자(검/활/창/폭) 제거
2. **은신(C) 시 애니메이션 미발동** 수정
3. **60.PNG 인벤토리**: ①퀘스트 탭 제거(퀘스트 창은 별도) ②무기 선택 버튼 5개 제거 → **그리드 아이템 우클릭으로 장착** ③하단 상세설명창 제거 → 설명은 중앙 패널로 ④**우측 캐릭터 아바타 패널 제거**(착용모습 구조 폐지)
4. **아이템 설명창(중앙)에 핫바 1~8 미니패드** → 그리드 아이템을 **드래그해서 숫자패드에 직접 지정**(핫바 등록)
5. **인벤토리 예시 2(창고)**: [인벤][설명][창고] 3패널, 중앙에 아이템 설명, 창고 쪽 [전체 보관][정리]
6. **인벤토리 예시 3(전리품)**: 몬스터/병사 처치 시 **전리품 상자 스폰** → 상자 상호작용(E) 시 전리품 UI 표시 → 인벤토리로 옮기기
7. **상점**: 예시 2처럼 상점창이 우측 패널 → 구매/판매
8. **기본 레이아웃**: 컨텍스트(창고/상점/전리품) 없으면 [인벤][설명] 2패널. 있으면 3패널(컨텍스트가 우측)
9. **스테이터스 창(P)이 안 뜸** — 원인 진단 후 수정

## 1. 조사 결과 (루트원인)

### 은신 애니 미발동 — 원인 확정
- PlayerMovement:406 HandleStealthInput → StealthSystem.ToggleStealth()는 정상(속도 0.5x·HUD 아이콘 동작).
- 그러나 **Player_AC의 Sneaky 상태가 고아**: m_Transitions: [] (진입/진출 전이 0개), HumanoidClipDriver에 은신 피드(SetBool) 없음 → 어떤 코드 경로로도 Sneaky 클립이 발화 불가.
- 수정: HumanoidClipDriver에 `SetBool("IsStealthed", StealthSystem.Instance.IsStealthed)` 피드 추가 + 컨트롤러에 AnyState→Sneaky(IsStealthed=true)·Sneaky→Base(IsStealthed=false) 전이 추가.

### 스탯창(P) 미표시 — 원인 미확정, 진단 필요
- 가능 후보: ①BuildPanel 중 예외로 _panelRoot 미생성(이후 P키 무반응) ②부트/인스턴스 경합 ③유저 플레이 시점의 스테일 어셈블리(중간 실패 상태로 재생된 59/60 캡처 가능성).
- 현재 코드상 뚜렷한 결함은 미발견 → **단계별 로깅 + try-catch 방어 + Open() 자가 복구**로 런타임 확정.

### 인벤토리 현 구조 (InventoryWindow, IMGUI 1180x1040 중앙)
- 탭 9종에 "📜 퀘스트" 포함(427행) → 제거 대상.
- WEAPON_SECTION_HEIGHT=112 무기 버튼 섹션(강철검/크리스탈검/돌검/나무검/도끼) → 제거 대상.
- **자체 3D 캐릭터 프리뷰 보유**(117행 RenderTexture, EnsurePreviewSetup/ReleasePreview) — 60.PNG 아바타의 실체 → 제거 대상(스탯창 v2와도 중복).
- 하단 상세 박스("아이템을 선택하면...") → 제거 후 중앙 설명 패널로 이관.
- 그리드 1행 텍스트 겹침 오류(60.PNG) — 재구조화에서 함께 정리.

### 기존 자산 재사용 (신규 작성 최소화)
- **LootWindow.cs 존재**: OpenForBasket(ILootBasket) 전리품 이동 UI 이미 구현. LootBasket/ILootBasket/DropTable/DropTableManager(Core/DropSystem) 존재.
- **WarehouseUI.cs + TerritoryWarehouse.cs**(UI 폴더) 존재.
- **ShopWindow.cs** 존재 — 단, Systems에서 여는 트리거 미발견(개점 경로 P1 확인).
- 몬스터 사망 드랍: AnimalAI가 고기 등 자동 드랍은 하나 **사망→전리품 상자 스폰 경로는 미확인**(병사 드랍 테이블 유무 포함 P1 확인).

## 2. Phase 계획

### P0 — 긴급 3종 (코드 에이전트)
- [ ] 스탯창 진단 강화: StatusWindowUI Awake/Open/BuildPanel 단계별 Debug.Log + try-catch(Debug.LogException), Open()에서 _panelRoot null이면 BuildPanel 재시도, 토글 시 `_isOpen` 로그. → 유저 Play 1회로 원인 확정 유도
- [ ] 은신 애니: HumanoidClipDriver에 IsStealthed 피드 추가 + Player_AC에 AnyState→Sneaky/복귀 전이 추가(Backup/ 복사 후 YAML 수술, 이동 중 전이 우선순위 검증)
- [ ] 핫바 라벨 제거: HotbarUI Slot{i}_Label 생성 블록 삭제
- [ ] 배치컴파일 CS=0

### P1 — 인벤 재구조화 설계 확정 (QA 에이전트, 읽기전용)
- [ ] InventoryWindow 전체 구조 맵(메서드별 담당 섹션, 제거 목록 행번호) + WarehouseUI/TerritoryWarehouse/LootWindow/ShopWindow의 공개 API·오픈 트리거 정리
- [ ] 몬스터/병사 사망 → 전리품 상자 스폰 현황 확정(부재 시 신설 방향: DropTable→상자 스폰→E 인터랙트→LootWindow)
- [ ] 상점 개점 트리거(ShopPlaceholder/ShopInteriorBuilder/NPC 상호작용) 추적
- [ ] ItemData.icon 스프라이트 실재 여부(아이콘 없으면 등급색+이름 축약 표기 폴백)
- [ ] HotbarUI 슬롯 API 확장 설계: AssignItem(index, itemId)/ClearSlot + 지속화(PlayerPrefs) — WeaponEquipManager 소유권 존중(호출만)

### P2 — 인벤토리 재구조화 본체 (코드 에이전트, 최대 작업)
- [ ] InventoryWindow 개편(단일 파일 집중):
  - 제거: 퀘스트 탭 / 무기 버튼 섹션 / 캐릭터 프리뷰(RT+카메라+클론 전부) / 하단 상세 박스
  - 좌측 패널: 상단 장비슬롯 6개(EquipmentManager 연동 표시) + 가방 그리드 + 슬롯수 표시(예: 가방 n/48)
  - 중앙 패널(아이템 설명): 선택 아이템 이름/등급색 테두리/아이콘(폴백 텍스트)/내구도/능력치 목록(긍정=하늘색, 페널티=빨강) + **핫바 미니패드 1~8(드래그 드롭 대상, 현재 지정 아이템 표시)**
  - **우클릭 장착/해제**: 그리드 아이템 우클릭 → 무기=WeaponEquipManager.Equip / 방어구=EquipmentManager.EquipItem, 이미 장착분 우클릭=해제. 장비슬롯 우클릭=해제
  - **드래그 시스템(IMGUI 수동 상태머신)**: 그리드 아이템에서 드래그 시작→설명창 핫바 패드 위 릴리스→HotbarUI.AssignItem. 드래그 중 고스트 아이콘 표시
- [ ] 3패널 레이아웃 매니저(InventoryWindow 내): 모드(단독/창고/상점/전리품)에 따라 [인벤][설명][컨텍스트] x오프셋 배치, 컨텍스트 창은 각 기존 클래스의 그리기 로직을 패널 영역에 위임하거나 상태 공유
- [ ] 배치컴파일 CS=0

### P3 — 창고/상점/전리품 통합 (코드 에이전트, P2와 병행 가능)
- [ ] 창고: TerritoryWarehouse 상호작용 시 인벤 모드=창고 → 우측 패널에 창고 그리드, [전체 보관][정리], 드래그 양방향 이동
- [ ] 상점: 상점 개점 트리거 연결(P1 결과) → 우측 패널 상점(구매/판매 탭), 골드 연동(PlayerStats.Gold)
- [ ] 전리품: 몬스터/병사 사망 시 DropTable 기반 전리품 상자 스폰(부재 시 신설) → E 인터랙트 → 인벤 모드=전리품 → 우측 패널 전리품 그리드(예시3) + [모두 줍기] + 드래그 이동
- [ ] 배치컴파일 CS=0

### P4 — 통합 QA (QA 에이전트)
- [ ] 배치컴파일 최종 CS=0 + 정적 검증(구독 위생/소유권/RT 해제/중복 프리뷰 카메라 제거 확인)
- [ ] Play 판정 체크리스트:
  1. 핫바 슬롯 글자 소멸
  2. C키 은신 → Sneaky 애니 발화(정지/이동 각각), 해제 시 복귀
  3. I키 → [인벤][설명] 2패널, 아바타/무기버튼/퀘스트탭/하단상세 소멸
  4. 그리드 아이템 우클릭 → 장착, 장비스탯 즉시 반영
  5. 아이템 드래그 → 설명창 핫바 패드 지정 → 실제 핫바 1~8 반영
  6. 창고 상호작용 → 3패널, 전체 보관/드래그 이동
  7. 몬스터 처치 → 상자 스폰 → E → 전리품 UI → 인벤 이동
  8. 상점 → 우측 패널 구매/판매
  9. P키 스탯창 표시(안 뜨면 콘솔 로그로 원인 확정)

### P5 — 기록
- [ ] QAPROGRESS/ROADMAP/메모리/git push/텔레그램

## 3. 수정 파일 예상

| 구분 | 파일 |
|:--|:--|
| 수정 | HotbarUI.cs(라벨 제거+Assign API), HumanoidClipDriver.cs(IsStealthed 피드), Player_AC.controller(Sneaky 전이), StatusWindowUI.cs(진단 로깅+복구), InventoryWindow.cs(대대적 개편), WarehouseUI/LootWindow/ShopWindow.cs(패널 편입) |
| 신규 | (필요 시) 전리품 상자 스포너/드랍 연결 코드, 상점 트리거 연결 |
| 문서 | QAPROGRESS/ROADMAP |

## 4. 리스크/주의

- **IMGUI 드래그&드롭**: Event.current 기반 수동 상태머신 필요(레이아웃/repaint 타이밍 함정) — 기존 코드에 드래그 선례 있는지 P1 확인 후 패턴 차용
- **4개 기존 윈도우 통합**: 각자 독립 OnGUI/Rect를 갖고 있어 "패널 편입"은 그리기 영역만 위임하는 방식(클래스 유지)이 충돌 최소 — 완전 재작성 금지
- **Sneaky 전이**: 이동 중 Walk/Run 상태와 경합 — AnyState 전이 우선순위·Interrupt Source 설정 필요, 컨트롤러 수술 전 백업 필수(Backup/ 선례)
- **핫바 슬롯 확장**: 기존 v1 슬롯 정의(1검/2활/3창/4폭탄 고정)를 아이템 지정 방식으로 바꾸면 WeaponEquipManager 연동(무기 슬롯) 회귀 주의 — 무기류는 기존 Equip 경로 유지, 소모품만 지정 방식
- **스탯창 미표시**: 루트원인 미확정 — P0 로깅 후 유저 Play 1회로 확정하는 2단계 접근(추측성 대수술 금지)
- 저사양: 인벤 캐릭터 프리뷰 제거로 RT/카메라 1세트 감소(성능 이득)

## 5. 검증/배치
- build_stats.bat 패턴 재사용, 매 Phase "Exiting batchmode successfully"+error CS=0 게이트
- Play 판정: P4 체크리스트 — 스크린샷 순번 보고 방식 병행
