# 스탯창 v2(예시 스타일) + 스탯포인트 + 미니맵 로컬뷰 + UI 오류 수정 계획 (2026-09-09 2차)

> 실행 규약: 코드/QA delegate_task 위임 필수. 매 단계 배치컴파일 error CS=0 게이트. 완료 시 3중 저장+텔레그램.
> 선행 계획: .hermes/plans/2026-09-09_level-stats-window-plan.md (스탯창 v1 완료 — P키 토글/좌상단 단순 패널)

## 0. 사용자 요구 (2026-09-09 09:58 보고 기준)

1. 오류 3종 수정: ①Player_AC SwimEnter 파라미터 누락 경고 ②PlayerInputHelper actionMaps 비어있음 ③TerrainDeco 부트 NRE(PlaceLakeshoreWillows)
2. 핫바의 알 수 없는 선 제거 + 핫바만 남기기 (58.PNG: 핫바 오른쪽 끝→화면 오른쪽 끝 검은 선, 슬롯 위 한글 라벨 겹침 '상/철/성/주', 좌하단 녹색 바)
3. 스탯창을 예시(스테이터스 예시.PNG)처럼: 인벤토리창 위치에 표시, 좌측에 플레이어 3D 모습+장비슬롯, 장비에 따른 스탯 상승
4. 레벨업마다 5 스탯포인트 → 힘/민첩/지능 등 직접 분배
5. 미니맵: 현재 위치 표시 + 전체 지형이 아닌 주변 로컬 지도만 표시

## 1. 조사 결과 (루트원인 확정분)

### 오류 ③ TerrainDeco NRE — 원인 확정
- `IdyllicDecoPlacer.cs:501` `cat.willowGreen.Count` — CategoriesR4(162~175행)의 `willowGreen/willowPink` 필드가 **선언만 되고 BuildCategoriesR4에서 절대 할당 안 됨**(1751행 c.willow, 1753 c.broadPurple, 1754 c.broadRed, 1756 c.blossom만 존재) → 항상 null → NRE. broadPurple/broadRed/blossom/meadow* 등도 할당부가 있는지만 전수 확인 필요(같은 패턴 잠재 위험).

### 오류 ① SwimEnter — 원인 확정
- Player_AC.controller AnyState 전이(722~행)의 조건 트리거 `SwimEnter`가 파라미터 목록에 없음(컨트롤러 파라미터에 SwimF/SwimI 상태명만 존재). 현재 수영은 HumanoidClipDriver:310 `SetBool("IsSwimming")` 자동판정 방식 → **구설계 잔재 전이**.
- 수정 방향: SwimI/SwimF 상태로 들어가는 IsSwimming 조건 전이가 이미 있는지 확인 → 있으면 고아 전이 삭제, 없으면 고아 전이의 조건을 `IsSwimming=true`로 교체(수영 비주얼이 현재 죽어있을 가능성 커버).

### 오류 ② PlayerInputHelper — 경미
- Resources의 PlayerControls .inputactions 임포트 깨짐(actionMaps 0) → 런타임 프로그램식 폴백이 이미 동작(경고만). 수정: 에셋 재임포트/YAML 점검으로 근본 복구 시도, 실패 시 폴백 유지(우선순위 낮음).

### 미니맵 — 부분 구현 확인
- MinimapUI는 `Assets/Scripts/UI/Functions/MinimapUI.cs` — **플레이어 마커 코드는 이미 존재**(DrawPlayerMarker:371, FindWithTag)하나 58.PNG에서 마커 안 보임 → 렌더/좌표 버그 or IMGUI 그리기 순서 문제 → P1 진단.
- 현재 뷰 = 월드 전체(2000m) 스플랫 축소(SetMapScale 220/2000) → 로컬뷰로 교체 필요(플레이어 중심 크롭).
- 미니맵 좌측 수직 슬라이더의 소유 확인 필요(MinimapUI 자체 요소로 추정 — 로컬뷰 줌으로 재활용 or 제거).

### 핫바 유령 선/라벨/녹색바 — 범인 미특정 (P1 진단 필요)
- MainScene에 HotbarUI 중복 0건(그립 검증). HotbarUI 자체 패널은 PanelWidth=866 Sliced 이미지.
- 유령 요소 후보: 씬에 남은 구 UI 오브젝트, IMGUI계열(AutoMoveUI/CombatLogUI/GameStatsWindow 등 OnGUI 다수), 구 HP바. 좌하단 녹색바=구 HP바 추정, 슬롯 위 한글=잔재 라벨 추정.
- 진단법: 에디터 메뉴용 계층 덤프 스크립트(모든 Canvas 하위 RectTransform/Text/Image의 이름·앵커·화면좌표 출력 → 콘솔/파일) + 58.PNG 대조 → 범인 특정 후 씬 오브젝트 삭제 or 코드 제거.

### 장비/스탯 연동 현황
- EquipmentManager 존재(Instance, EquipmentSlot 열거, OnEquipmentChanged 이벤트, 내구도) — **스탯 보너스 개념 없음**. ItemData(effects string뿐)에 스탯 필드 없음.
- PlayerStats: 레벨 기반 파생(FinalAttackDamage 등) + TempBonus 슬롯 존재 → 장비 보너스를 별도 레이어로 합산하는 구조 추가.
- EquipmentWindow(IMGUI, UIWindow 상속) 존재 — 슬롯 UI 로직 재사용 가능. 무기는 핫바→WeaponEquipManager와의 역할 분계 P1에서 확정.

## 2. Phase 계획

### P0 — 긴급 버그 3종 수정 (코드 에이전트)
- [ ] IdyllicDecoPlacer: BuildCategoriesR4에 `willowGreen=Filter(trees,"WillowTree","Green")`, `willowPink=Filter(trees,"WillowTree","Pink")` 할당 + CategoriesR4 전 필드 인라인 `new List` 초기화(잠재 null 전멸) + PlaceLakeshoreWillows에 null/빈 가드 유지. 기존 lakeRng 스트림 소비 순서 보존 규약 준수(할당만 추가, 배치 로직 불변)
- [ ] Player_AC: Swim 상태 클러스터 전이 구조 확인 → IsSwimming 조건 경로 존재 시 고아 SwimEnter 전이 삭제, 부재 시 조건을 IsSwimming=true로 재배선 (애니 컨트롤러는 YAML 직접 수정, HumanoidClipDriver 무수정 원칙)
- [ ] PlayerControls.inputactions 임포트 복구 시도(재임포트/YAML 점검) — 실패 시 폴백 유지 판정 기록
- [ ] 배치컴파일 CS=0 게이트

### P1 — UI 정밀 진단 (QA 에이전트, 읽기전용+에디터 덤프 스크립트)
- [ ] 계층 덤프 에디터 스크립트 신설(Assets/Editor/UiHierarchyDumpMenu.cs — Tools/UI/계층덤프): 활성 Canvas 전체의 RectTransform 이름/앵커/크기/화면좌표 + Text 내용 → 파일 출력. 58.PNG 좌표 대조로 ①핫바 우측 유령 선 ②슬롯 위 한글 라벨('상/철/성/주') ③좌하단 녹색 바 ④미니맵 플레이어 마커 미표시 원인 ⑤미니맵 옆 수직 슬라이더 소유 — 5건 범인 특정
- [ ] InventoryWindow의 화면 위치/크기 확정(스탯창 v2가 그릴 동일 위치 기준) + EquipmentWindow/WeaponEquipManager/EquipmentManager 역할 분계 정리
- [ ] 제거 목록 산출: 씬 오브젝트인지 코드 생성분인지 구분 → 제거 방법 명시

### P2 — 유령 UI 제거 (코드 에이전트, P1 결과 반영)
- [ ] 범인 요소 제거(씬 오브젝트 삭제 or 생성 코드 제거) — 핫바/미니맵/정상 UI 무손상 확인
- [ ] (발견 시) 미니맵 플레이어 마커 미표시 버그도 이 단계에서 수정
- [ ] 배치컴파일 CS=0

### P3 — 스탯포인트 + 장비 보너스 데이터 레이어 (코드 에이전트)
- [ ] PlayerStats 확장(직접 수정 아님 — 위임): `PendingStatPoints`, `Allocated{Str,Agi,Int,Vit}`(초기 5/5/5/5), `AddStatPoints(int)`, `AllocateStat(kind)`(1포인트당 효과: 힘+2 공격, 민첩+0.5% 치명·+0.05 이속, 지능+0.5% 연금·요리, 체력+10 HPBase — 수치는 기존 밸런스 배율에 맞춰 조정), AddEXP 레벨업 시 +5포인트 자동 지급. 저장 연동(기존 세이브 경로 확인 후 합류, 없으면 PlayerPrefs)
- [ ] 장비 보너스 테이블: EquipmentStatBonus(id별 공격/방어/체력/속도 맵, 무기 4종+장비 슬롯 예시값) + EquipmentManager.GetTotalBonuses() 집계 + OnEquipmentChanged 시 PlayerStats에 반영하는 브리지(EquipmentStatBonusApplier — EquipmentManager 소유권 존중, 별도 파일)
- [ ] 파생 공식 최종형: FinalAttackDamage = base + 레벨*0.5 + 힘보너스 + 장비공격 (기존 소비처 무손상)
- [ ] 배치컴파일 CS=0

### P4 — 스탯창 v2 UI (코드 에이전트, 예시 레이아웃)
- [ ] StatusWindowUI v2 개편: 인벤토리창과 동일 위치/유사 크기(P1 확정 Rect), 예시 구조 재현 —
  - 좌측: **플레이어 3D 뷰포트**(RenderTexture 256x320 + 전용 오쏘 카메라(플레이어 모델만 Culling), 배경 어둡게, 드래그 좌우 회전) 둘레로 ㄷ자 장비슬롯(EquipmentManager 연동, 아이콘 없으면 등급색 테두리+이름 약자)
  - 우측 2열: ①전투(체력/공격/방어/치명/이속 — 흰 라벨+하늘색 수치) ②주스탯 힘/민첩/지능/체력 with **[+] 버튼**(포인트 있을 때만 활성, 클릭→AllocateStat) ③정보(이름/레벨/EXP게이지/남은포인트/골드/중독)
  - 상단 타이틀 "캐릭터 정보"+X 버튼, 다크 프레임+버건디 포인트(예시 톤)
  - P키 토글/OnLevelChanged/OnEquipmentChanged 구독 유지, v1의 재구독 위생 로직 이식
- [ ] 배치컴파일 CS=0

### P5 — 미니맵 로컬뷰 (코드 에이전트, P4와 병행 가능 — 파일 분리)
- [ ] MinimapUI 로컬뷰: 월드 스플랫에서 플레이어 중심 반경(기본 120m) 영역만 표시 — uvRect 크롭(RawImage) 또는 GUI.DrawTextureWithTexCoords, 플레이어 이동 실시간 추적(0.1s 폴링), 월드 경계 클램프. 줌 3단(80/120/200m, 기존 슬라이더 재활용 or 휠)
- [ ] 플레이어 마커: 파란 점 → 중앙 고정 + 이동방향 화살표(로컬뷰에서는 플레이어가 항상 중심, 맵이 흐르는 방식) + 영지/퀘스트 마커는 로컬 범위 내만 표시
- [ ] 배치컴파일 CS=0

### P6 — QA 통합 검증 (QA 에이전트)
- [ ] 배치컴파일 최종 CS=0 + 정적 검증(구독 위생/소유권 침해 없음/삭제 잔존 0)
- [ ] Play 판정 체크리스트 산출:
  1. 콘솔에 SwimEnter 경고 0 + TerrainDeco NRE 0 + InputHelper 경고 소멸(복구 실패 시 1회 경고만)
  2. 핫바 유령 선/한글 라벨/녹색바 소멸, 핫바만 표시
  3. P키 → 인벤토리 위치에 캐릭터정보 창: 3D 모델 회전, 장비슬롯, [+] 분배(레벨업 후 5포인트), 장비 착용/해제 시 스탯 변동
  4. 미니맵: 플레이어 중심 로컬 지형 + 방향 화살표 + 영지/퀘스트 마커
- [ ] QAPROGRESS 스냅샷/ROADMAP/메모리/git push/텔레그램 (P7)

### P7 — 기록/마무리
- [ ] 3중 저장 + 커밋 푸시 + 텔레그램 알림

## 3. 수정 파일 예상

| 구분 | 파일 |
|:--|:--|
| 수정 | Assets/Scripts/Systems/IdyllicDecoPlacer.cs(할당+가드), Assets/Resources/Animation/Controllers/Player_AC.controller(전이 정리), Assets/Scripts/UI/StatusWindowUI.cs(v2 개편), Assets/Scripts/UI/Functions/MinimapUI.cs(로컬뷰), Assets/Scripts/Core/PlayerStats.cs(포인트/할당 — 위임 하에) |
| 신규 | Assets/Scripts/Systems/EquipmentStatBonus.cs(테이블+집계+브리지), Assets/Editor/UiHierarchyDumpMenu.cs(진단 도구) |
| 씬 | 유령 UI 오브젝트 제거(P1 확정분), PlayerControls 재임포트 |
| 문서 | ROADMAP/QAPROGRESS |

## 4. 리스크/주의

- **PlayerStats 직접 수정 금지 규약**: 스탯포인트 추가는 위임 하에 최소 침습(기존 필드/이벤트 유지, 소비처 7곳 무손상) — 파생 공식 변경은 Additive만
- **3D 뷰포트**: 저사양 PC(i5-6500/HD530) — 전용 카메라는 창 열림 시에만 활성+저해상도 RT, 닫으면 비활성
- **애니 YAML 수술**: Player_AC는 과거 재생성 이력 다수 — 수정 전 백업 커밋, 수정 후 에디터 재생성 경로(HumanoidClipDriver)와 충돌 없는지 확인
- **IMGUI vs UGUI 혼합**: InventoryWindow/EquipmentWindow는 IMGUI(Rect) — 스탯창 v2는 UGUI(UGUI 캔버스) 유지 시 위치만 맞추는 방식과 IMGUI 전환 방식 중 P1 결과로 결정(기본: UGUI 유지+좌표 매칭)
- **미니맵 마커 좌표**: 로컬뷰 전환 시 WorldToMinimapLocal 스케일 재계산 필수(2000m→로컬 반경), QuestMarkerSystem과 규칙 일치 유지
- 레벨업 로코(victory) 연출은 여전히 보류 — 별도 Phase

## 5. 검증/배치
- 배치컴파일: build_stats.bat 패턴 재사용(로그 buildlog_p*.txt), "Exiting batchmode successfully"+error CS=0 게이트
- Play 검증: P6 체크리스트 — 사용자 스크린샷 순번 보고 방식 병행
