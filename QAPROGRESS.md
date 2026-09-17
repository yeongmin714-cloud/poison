# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류를 잡아냅니다.
>
> **진행 방식:** 테스트 씬별로 시스템 격리 → Play 테스트 → 오류 발견 → 수정 → 기록
>
> **최종 갱신:** 2026-09-18 (Phase 68/U0 — UI Toolkit 전환 인프라 구축)

---

## 📌 세션 스냅샷 (2026-09-18 ✅ Phase 68/U0 — UI Toolkit 전환 인프라 구축)

> **입력**: UI Toolkit 전면 전환 결정(사용자 확정 — 모든 UI UXML/USS, IMGUI 신규 금지) → 계획서 docs/UI_TOOLKIT_MIGRATION.md 수립(OnGUI 112파일 전수 조사, Phase U0~U8, UI 개편 1순위) → U0 실행.

### 변경 사항 (신규 7 + 자산 3)
**Assets/Resources/UI/Theme.uss**: 62차 팔레트 USS 변수 18종(--c-bg-panel #1C1C1C E0·--c-border-bronze #8C6B3F·--c-border-gold #C9A227·--c-text-primary #F5EFE0 등) + 폰트 5단(--fs-xl 60/lg 38/md 24/sm 17/xs 13) + 공통 클래스(.utk-window/.utk-title-bar/.utk-btn 3변형/.utk-slot/.utk-tooltip/.utk-modal/.utk-toast/등급 테두리 6종). **비표준 속성 제거**(border-style/box-shadow — USS 미지원, Import 경고 0).
**Assets/Resources/UI/UnityDefaultTheme.tss**: `@import url("unity-theme://default")` 1줄 — PanelSettings 기본 테마.
**Assets/Resources/UI/PanelSettings.asset**: 배치 생성 — ScaleWithScreenSize·referenceResolution 1920×1080·match 0.5·테마 TSS 할당(기존 _uiScale 수동 공식 은퇴 예정).
**Assets/Editor/UIToolkitSetup.cs**: InitializeOnLoad 멱등 자동생성(USS/TSS/PanelSettings 부재 시 기록+Import) + 메뉴 "Tools/UI Toolkit/Recreate Panel Settings". ⚠️ **batchmode -quit는 delayCall 미실행 → 배치 생성은 `-executeMethod UIToolkitSetup.Recreate`(0-arg) 정석 경로**(EnsureResources는 1-arg라 executeMethod 불가).
**Assets/Scripts/UI/Toolkit/UIToolkitBootstrap.cs**: [RuntimeInitializeOnLoadMethod BeforeSceneLoad] 자가 Ensure — "UTKRoot"(DontDestroyOnLoad)+UIDocument(panelSettings=Resources/UI/PanelSettings)+Theme.uss 적용. static UIRoot/IsReady/public Ensure(멱등).
**UTKWindowManager.cs**: 열림 윈도우 순서 보장 스택+ESC 최상단 Close(내장 Updater MB, Ensure 멱등).
**UTKWindowBase.cs**: 윈도우 셸 — .utk-window/.utk-title-bar(타이틀바 드래그 PointerDown/Move/Up+PointerCapture·닫기 버튼)/.utk-content, Show/Hide/Toggle/Close+매니저 자동 등록, `ApplyUIToolkitFont(VisualElement)`(UIFont.Load→style.unityFont 재귀).
**UTKControls.cs**: UTKButton(Primary/Secondary/Danger)·UTKSlot(아이콘 background-image+카운트+등급 테두리+호버)·UTKRarity(등급→USS 클래스)·UTKTooltip(커서 추적)·UTKToastService(하단 큐+schedule 페이드)·UTKModal(확인/취소+ShowToRoot).

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) — 중간 CS0103 1건 수리(UTKControls의 ApplyUIToolkitFont → UTKWindowBase.ApplyUIToolkitFont 클래스 한정).
- PanelSettings 배치 생성 확인 + Theme.uss ScriptedImporter 로드 성공(팔레트 변수/공통 클래스 파싱 경고 0).
- Play 판정 대기: ①에디터 포커스 시 InitializeOnLoad 로그 3종 ②UTKWindowBase 상속 테스트 창 렌더(Phase U1 파일럿에서 검증).

---

## 📌 세션 스냅샷 (2026-09-17 ✅ 화술 판매 프리미엄 — 상점 판매가 인상)

> **입력**: 상점에 팔 때 더 비싼 가격으로 팔 수 있도록 화술(지능 파생)이 판매가에 작용.

**`Core/PlayerStats.cs`**: `SellBonus = Clamp(SpeechSkill*0.01,0,0.5)` 판매 프리미엄(화술×1%, 최대 +50%) 추가.
**`UI/ShopWindow.cs`**: `CalculateSellPrice`에 `Mathf.Max(1, CeilToInt(basePrice*(1+SellBonus)))` — 화술 높을수록 비싸게 판매(카테고리 기본가 유지, null 폴백, 최소 1G).

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0), Core.dll 오늘시각 갱신 + 심볼 `get_SellBonus` 착륙.
- 밀매 경로(`SmuggleSell`)와 별개 — 일반 상점 판매(`SellSelectedItem→AddGold`)에만 적용.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 지능(INT) 파생 화술(Speech) — 포섭/저가구매/밀매)

> **입력**: 플레이어 지능 파생 스탯에 **화술** 추가 — 화술이 높을수록 **포섭**, **거래(상점 저가 구매)**, **밀매** 스탯이 상승하도록.

### 변경 사항 (6파일: 수정5 + 신규1)
**`Core/PlayerStats.cs`**: 지능 파생 화술 추가 — `SpeechSkill=Level+INT(SPEECH_INT_FACTOR=1)`, `BuyDiscount=Clamp(SpeechSkill*0.01,0,0.2)` 최대20% 구매할인, `RecruitSpeechBonus=Clamp(SpeechSkill*0.003,0,0.2)` 포섭 보정, `SmuggleSkill=Clamp(SpeechSkill*0.01,0.05,0.9)`, `SmuggleGainMultiplier=1+Clamp(SpeechSkill*0.005,0,0.5)` 최대+50%. (`SpeechAffinityBonus` 레벨기반은 호환성 유지).
**`Systems/GuardRecruitSystem.cs`**: 포섭 선물(GIFT)/위협(THREAT) 확률에 `+RecruitSpeechBonus` 가산(Clamp01, PlayerStats null 폴백).
**`UI/ShopWindow.cs`**: `GetBuyPrice(item)=Max(1, Ceil(price*(1-BuyDiscount)))` — 구매(내부+public BuyItem)·canAfford·환불 모두 할인가 적용, 표시=`"가격 {할인가}G (원가 {원가}G)"`.
**`Systems/WanderingMerchant.cs`**: 구매 totalPrice에 `(1-BuyDiscount)` 적용 + **`SmuggleSell(ItemData,count)`** 신규(CanSmuggle 확인 → Gold 지급 `EstimateBaseValue×SmuggleGainMultiplier×count`, 인벤 제거).
**`Systems/SmuggleSystem.cs`(신규)**: static — `SmuggleSkill/GainMultiplier/CanSmuggle(숙련≥0.30)/EstimateBaseValue(카테고리별 추정가)`.
**`UI/StatusWindowUI.cs`**: 화술 행 = `SpeechSkill`(INT 보정 반영 표시).

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0, 오늘시각 DLL 4종 갱신) — 심볼 착륙 확인: Systems.dll `SmuggleSystem/SmuggleSell`, Core.dll `get_SpeechSkill/get_RecruitSpeechBonus`.
- 노트: 밀매는 기존 상점 판매와 별개 신규 경로(SmuggleSell). GUI/버튼은 미추가(상인 UI 확장 시 연동 가능). 밀매 상점 UI 원하면 후속 작업.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 특사(Envoy) 역할 — 민첩 기반 잠입 정보수집 + 발각 시 처형)

> **입력**: 영지 역할에 **특사** 추가 — 민첩이 높을수록 성공, 다른 영지에 잠입해 정보(병사 수/레벨·왕의 선호음식·재화·보물) 수집, 노출 시 즉각 처형. 기존 **SpySystem**이 동일 구조(선호음식/병력 Info, 발각=EXECUTION_DAMAGE) 재사용으로 확장.

### 변경 사항 (3파일)
**`Systems/SpySystem.cs`**: 발각확률에 **민첩 감소** 추가 — `AGILITY_DETECT_REDUCTION=0.004f`(민첩1당 -0.4%)+`MIN_DETECT_CHANCE=0.02f`, `CalculateDetectChance`에서 `-spy.GetAgility()*…` 반영(레벨/호감도 감소 유지). **발각 시 즉시 처형(TakeDamage 9999) 유지**.
**`Systems/GuardTaskSystem.cs`**: `GuardTask.Envoy` 추가 + `RoutineEnvoy` — `FindNearestEnemyTerritoryId`(가장 가까운 비소유 영지) 선택, `SpySystem.SendSpy(LordInfo+TroopInfo)`로 정보수집, detective시 SpySystem이 처형+안전망 TakeDamage(999999), 수집 정보+재화(난이도 추정 지수)를 Debug.Log 보고. 쿨다운 30s.
**`UI/TerritoryDeploymentUI`**: 🕵️ **특사** 버튼 + 대상 영지 target-pick(기존 GetAttackTargets 재사용, `_pickingEnvoy` 패턴) → `AssignTerritoryRole(Envoy)`.

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0) — Systems/UI.dll 갱신 + 심볼(GuardTask 6·RoutineEnvoy 1·AGILITY_DETECT_REDUCTION 1).
- Play 판정 대기: ①특사 버튼+대상 영지 선택 ②민첩 높은 병사=성공확률↑ ③정보(병사수/레벨·선호음식·재화 추정) 수집 보고 ④발각 시 즉각 처형(병사 소멸+드랍).
- 노트: TerritoryState에 금/보물 실필드 없음 → 난이도 추정 지수로 대체(실재화 필드 추가 시 교체). GuardPlaceholder `_statusMessage` public setter 없음 → 정보는 Debug.Log(월드 버블 표시 원하면 setter 추가).

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 영지 병사 역할 배치 — GuardTaskSystem)

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 영지 병사 역할(task) 배치 — GuardTaskSystem)

> **입력**: 영지 창(TerritoryDeploymentUI)에 역할 선택 추가 — 사냥/공격/수비/채집/농경. 공격=플레이어 동행, 수비=문지기, 채집=근처 약초(확률), 사냥=근처 몬스터(확률 드랍+일정확률 사망), 농경=병사 농사.

### 변경 사항 (신규 1 + 수정 3)
**`Systems/GuardTaskSystem.cs`(신규)**: 싱글톤+Ensure. `enum GuardTask{None,Attack,Defend,Gather,Hunt,Farm}`+`AssignTask/AssignTerritoryTask/ReleaseTask`. 0.4s 루틴: **Attack**=플레이어 추종 / **Defend**=성문 앞 주둔 / **Gather**=`GatheringSystem.TryGather` 약초 / **Hunt**=몬스터 데미지+고기60%+**사망8%**(TakeDamage→Die) / **Farm**=`FarmingSystem.Plant/Harvest`.
**`UI/TerritoryDeploymentUI`**: 영지별 **사냥/공격/수비/채집/농경** 5버튼→GuardTaskSystem 라우팅, 기존 해제 유지.
**`CoreSystemsBootstrap`·`TestTerritoryCombatSetup`**: Ensure 와이어링.

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0) — Systems/UI.dll 갱신 + 심볼(GuardTaskSystem 3/AssignTerritoryTask 1/GuardTask 4).
- Play 판정 대기: ①5역할 버튼 ②공격=추종 ③수비=성문 ④채집=약초 ⑤사냥=고기+8%사망 ⑥농경 ⑦해제.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 병사 스탯 모델·정보보기 2분할·장비/물약 데이터 — 민첩)

> **입력**: ①병사 스탯 정도는 레벨로 일정, 종류는 랜덤 배치 ②정보보기 창 신설 ③좌=현재 모습+착용 장비 / 우=스탯(+장비 착용 시 공/방 상승) ④무기 공격력·방어구 방어력 상승지수 파일 정리 ⑤물약 종류별 능력치 버프 파일 정리. (이속→**민첩** 확정).

### 변경 사항 (신규 2 + 수정 2)
**신규 `Core/GearStatIndex.cs`**: `GetWeaponAttackBoost(id)` — 무기타입 기본데미지(검12/창10/활8/단검7/맨손5 추정값 문서화)×`WeaponData.GetTierMultiplier`(wood1/steel1.8/stone2.5/crystal3.75). `GetArmorDefenseBoost(id)` — 부위 기본(헬멧4/갑옷8/부츠3/장갑2/방패6/마스크·팩2)×티어.
**신규 `Core/PotionBuffData.cs`**: `PotionBuffEffect{healFlat,healPercent,attackBuff,defenseBuff,agilityBuff,buffSeconds}` + `GetBuff(id,out effect)` + `AllEffects`(potion_hp_small/big·potion_attack/defense/agility) — PotionUseSystem 키워드 규칙과 정렬.
**`Systems/GuardPlaceholder.cs`**: 랜덤 스탯 모델 — `RollStats()`: 총합=`Round(6+level*1.5)` 고정, 3절단점 정렬로 공/방/체력/민첩 4구간 랜덤 분배(합=총량). `GetStat*`(raw) + `GetAttack()`(기본+무기 지수)/`GetDefense()`(기본+방어구 지수합)/`GetAgility()`/`GetMaxHP()`(기본 _maxHP+체력×2). 근접 공격 데미지 `level*1.5`→`GetAttack()` 교체(장비가 실제 공격력에 반영). Start에서 RollStats.
**`UI/GuardInfoWindow.cs`**: 2분할 — **좌**: 모습(이름/직합/국가)+착용 장비 목록(무기/투구/갑옷/신발/장갑/방패), **우**: HP바·전투력·공/방/최대체력/민첩(+기본N+장비M 분해)·물약 버프. ESC/우클릭/정보버튼 유지.

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0) — 6 DLL 갱신 + 심볼 착륙(GearStatIndex 4/PotionBuffData 4/RollStats 1/GetAttack 7/GetDefense 10/GetAgility 2/GetMaxHP 2).
- 수정 컴파일 오류: RollStats 내 `int c` 지역변수 중복(while 내부 vs 메서드 몸통) → `cut` rename(CS0136).
- Play 판정 대기: ①정보보기 창 2분할 ②병사별 공/방/체/민첩 분포 상이하나 총량 일정 ③장비 착용 시 공/방 상승 표시+실제 근접 데미지 반영 ④무기/방어구/물약 지수 파일 값 확인 ⑤민첩 표기.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ RTS 인터랙션/커서/농사·채집 — P1~P5)

> **입력**: ①Ctrl 시만 OS커서 표시 ②병사 지정+우클릭=이동, 몬스터/적대병사=검커서+좌클릭 공격(병사만) ③경지=곡갱이 커서+좌클릭 농사(약초씨→성장→수확 재화) ④풀=삽 커서+좌클릭 채집 ⑤숫자패드 선택 토글(재눌름=해제, 슬롯 유지) ⑥핫바 우클릭=슬롯 해제. **정정**: 활=좌클릭 발사가 맞음. 우클릭 카메라 드래그는 **실제 활성**(TopDownCameraController L87) → 우클릭 3경합(카메라/근접차지/RTS) 라우팅 우선순위 필요. 커서 아이콘(검/곡갱이/삽)은 **절차 생성** 선택(외부 의존 0). OS커서 API=`UnityEngine.Cursor.visible`.

### 변경 사항 (신규 6 + 수정 5)
**신규 `Systems/`**: `ContextCommandRouter`(좌클릭: 병사선택+적호버→RTS 공격(병사만)+consumeLeftClickAsDrag 소비 / 커서 2종 자동 확보) · `HoverTargetClassifier`(카메라레이→Enemy/Farm/Gather/Ally/Terrain) · `ContextCursorSystem`(마우스 추적 커스텀 커서: 검/곡갱이/삽/화살) · `CursorVisibilityController`(Ctrl 홀드 시만 OS커서) · `FarmingSystem`(경지: Plant→25s→Harvest 약초 재화) · `GatheringSystem`(풀: TryGather→약초+15s 리스폰).
**`UI/GuardSquadHotbar`**: 숫자패드+상단 숫자 선택 **토글**(재눌름=해제, 슬롯 유지) + **우클릭=슬롯 해제**(UnregisterSlot) — 그룹 배열이 이 파일(GuardSelectionManager 아님).
**`Systems/GuardSelectionManager`**: static `consumeLeftClickAsContextCommand` 필드만 추가(로직 무변경).
**`Core/PlayerInventory`**: `Seed_Herb(seed_herb·약초씨)`·`Herb_Yakcho(herb_yakcho·약초)`.
**`Systems/CoreSystemsBootstrap`·`TestTerritoryCombatSetup`**: 항목 Ensure 와이어링.

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0) — 4 DLL 갱신 + 전 심볼 착륙. 회귀 0(플레이어 비주얼·RTS·선택 기존 로직 무접촉).
- 수정 컴파일 오류: ①`for(var key in…)`→`foreach`(이 엔진 for-in 미지원) ②`FarmingPlot` package-private→`public`(CS0050).
- Play 판정 대기: ①Ctrl 홀드 시만 커서 ②병사+몬스터 호버=검커서·좌클릭 병사만 공격/지형 우클릭=이동 ③경지=곡갱이·좌클릭 파종→25s→수확 ④풀=삽·좌클릭 채집 ⑤숫자패드 재눌름=해제(슬롯 유지) ⑥핫바 우클릭=슬롯 해제.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 병사가 플레이어 장비 착용·스폰·드랍 — 레벨 스케일 희귀 + 저레벨 소확률)

> **입력**: "병사도 플레이어 장비 착용 → 스폰 시 랜덤으로 일부 착용 → 사망 시 그 장비 드랍. 레벨 높을수록 희귀, 낮아도 희박하게 희귀". **뿌리 설계**: 드랍(`GuardPlaceholder` 필드)과 시각(`GuardEquipmentSystem`)이 **분리**돼 "입은 것≠드랍" 위험 → **단일 소스(GuardPlaceholder 장비 필드)**로 통일해 "보이는 그대로 드랍" 보장. 등급은 기존 `RarityProbabilityTable.Roll(level) + LuckyRollSystem.TryLuck` 재사용(레벨 구간별 확률표가 이미 저레벨 Rare 8% 포함) + 부츠/장갑 통합 id(`wood_boot`)의 좌우 GLB 분리 부착 필요.

### 변경 사항 (3파일)
**`Systems/GuardPlaceholder.cs`**: 장비 필드 `BootsItem`·`GlovesItem` 신설(기존 Weapon/Shield/Helmet/Armor 뒤). `DropEquippedItems`에 `BootsItem`·`GlovesItem` 드랍 추가(100%) — 총 6슬롯(무기/방패/투구/갑옷/신발/장갑).
**`Systems/GuardEquipmentSpawner.cs`**: `SpawnPlayerLoadout(gp, level)` 신설 — 부위별 착용 확률(투구 .65/갑옷 .80/신발 .50/장갑 .45/방패 .25)로 일부만 착용, `RollLoadoutItem(part, level)`이 `RarityProbabilityTable.Roll`→`LuckyRollSystem.TryLuck`→티어 매핑(Common=wood/Uncommon=steel/Rare=stone/Epic+=crystal, shield=wood_shield 고정)→`PlayerInventory.GetItemById`로 실제 플레이어 아이템 id 생성. `SpawnEquipment` 끝에서 호출 → 기존 절차 장비(스탯)와 병행.
**`Systems/GuardVisualAttachSystem.cs`**: 시각 소스를 `GuardEquipmentSystem.GetAllGuardEquipment` → **GuardPlaceholder 장비 필드**로 재소싱(AddFieldToDesired — 투구/갑옷/신발/장갑/방패). **좌우 처리 추가** `ResolveSideVisualId` — Shoes/Gloves는 foot.L/hand.L에 `{id}_left`·foot.R/hand.R에 `{id}_right` GLB 부착(부재 시 원본 `{id}` 폴백). GuardEquipmentSystem 의존 완전 제거.

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0) — Systems.dll 갱신 + `strings` 심볼 착륙(SpawnPlayerLoadout/RollLoadoutItem/BootsItem/ResolveSideVisualId/AddFieldToDesired 모두 >0).
- 플레이어 비주얼 시스템(EquipmentManager·ArmorVisualAttachSystem·WeaponPartsSystem) **무접촉** — 회귀 0.
- Play 판정 대기: ①병사 스폰 시 플레이어 방어구(wood~crystal) 일부 착용 ②고레벨 병사는 희귀/에픽, 저레벨은 드물게 희귀 ③병사 사망 시 입었던 장비(투구/갑옷/신발/장갑/방패)가 전리품으로 드랍 ④부츠/장갑 좌우 정위치 부착 ⑤덩치(×1.8) 병사는 장비도 1.8배.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 병사 방어구 비주얼 부착 시스템 신설 — 정상(×1.0)·덩치(×1.8) 병사 공용)

> **입력**: "병사들도 장비 착용이 가능하게 — 정상 크기 병사는 플레이어처럼 부착, 덩치 병사는 장비 크기 확대". **설계 조사(ground-truth)**: 병사 rig(3티어 공통 27뼈)은 Blender식 `.L/.R` 접미 이름(`hand.L/foot.L/forearm.L/spine.001..005`), `Head/Chest` **없음** → 크라운=`spine.005`, 가슴=`spine.003` 대리. 플레이어 시스템(`ArmorVisualAttachSystem`, `Head/LeftFoot…` 기대)과 이름 불일치 → **매핑 계층 필요**. 병사 희귀 장비(`steel_helmet/steel_armor` 등)는 시각 GLB 존재, 기본 절차 장비(`equip_armor_*`)는 GLB 없음.

### 변경 사항 (3파일, ADDITIVE — 플레이어 시스템 무접촉/회귀 0)
**`Systems/GuardVisualAttachSystem.cs` (신규)**: 병사(GuardPlaceholder) 0.35s 폴링 → `GuardEquipmentSystem.GetAllGuardEquipment`에서 방어구 가져와 병사 본에 GLB 부착. 슬롯 판정=id 키워드(helmet/armor/boot|shoe/glove/shield). 병사 본 맵: 크라운 spine.005·가슴 spine.003·척추 spine·손 hand.L/.R·발 foot.L/.R·방패 hand.L(폴백 forearm.L). 부착은 `Object.Instantiate(prefab, bone)` + localScale=1(본 공간) — **덩치 1.8x는 부모 모델 스케일이 뼈에 전파되어 장비 자동 1.8배, 정상 병사는 1.0 유지**. GLB 없는 절차 장비는 티어 폴백(level≥40 steel, 20~40 wood). 슬롯별 재계퍼레이션(낡은 시각 파괴/중복 방지). 싱글톤 Instance+Ensure.
**`Systems/CoreSystemsBootstrap.cs`**: `EnsureGuardVisualAttachSystem()` — 부트 시 GuardVisualAttachSystem 생성(중복 가드+try/catch). `EnsureArmorVisualAttachSystem()` 직후 호출.
**`Systems/TestTerritoryCombatSetup.cs`**: 테스트 씬 gameManager GO에 GuardVisualAttachSystem 추가(중복 가드) — Test_10은 Bootstrap 미실행이므로.

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0) — Systems.dll 갱신 + `strings Systems.dll | grep -c GuardVisualAttachSystem` = 4(클래스+메서드 착륙).
- 관용구 검증: `string.Equals(a,b,System.StringComparison.OrdinalIgnoreCase)`·`Object.FindObjectsByType<T>(FindObjectsInactive)`·`kv.Key/.Value`·`Object.Instantiate(prefab,bone)` 모두 코드베이스 기존 용례와 일치.
- Play 판정 대기: ①정상 병사(×1.0)가 플레이어처럼 투구/갑옷 입음 ②덩치 병사(×1.8)는 그 장비가 1.8배로 입혀짐 ③장비 해제/교체 시 시각 갱신 ④GLB 없는 절차 장비 병사도 티어 기본 방어구 표시.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 덩치 병사 사이즈 — 레벨 곡선 아닌 "두 덩치 모델 1.8배" 고정)

> **입력**: "덩치큰 두 병사를 일반 병사보다 1.8배 크게". 설계 논의 후 확정 — "레벨별 확대 시스템이 아니라, 높은 레벨(덩치) **두 병사 모델만** 1.8배". 일반 병사(lv1-20)는 1.0배 유지.

### 변경 사항 (2파일)
**`Systems/GuardManager.cs`**: `GetSoldierSizeMultiplier(int level)` 신설 — `level>=20 ? 1.8f : 1.0f`(정확히 두 덩치 티어만, 레벨 곡선 아님). 프로덕션 병사 보충(refill) 경로에서 정규화 후 `s *= GetSoldierSizeMultiplier(newLevel)`.
**`Systems/TestTerritoryCombatSetup.cs`**: FBX 경로 + GLB 폴백 경로 각각 정규화 후 `* GetSoldierSizeMultiplier(level)`. `ScaleGuardHitbox(guardGO, level)` 신설 — 몸이 1.8배 커진 덩치 병사의 히트용 root `BoxCollider`(원 0.6×1.8×0.6)를 배율만큼 확대+바닥(0) 기준 재중앙화(윗몸이 안 맞는 버그 방지). 배율 1.0(일반)은 기존 그대로 유지.

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0) — Systems.dll 갱신 + `strings`로 `GetSoldierSizeMultiplier`·`ScaleGuardHitbox` 심볼 착륙 확인.
- Play 판정 대기: ①lv20-40/lv40-50 병사가 일반(lv1-20)보다 1.8배 크게 보임 ②덩치 병사 윗몸도 화살/공격에 히트(콜라이더 확대 확인) ③일반 병사 크기 변화 없음(회귀 없음).

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 70차 후속19 — 계획서 V2(ARCHERY_PLAN_V2) Phase A~D 실행: 화살 방향 평탄화·물리 안정화 + 명중 피드백 + 조준 프리뷰 + Earth Trail 선택 표시)

> **입력**: 화살 테스트 5 프레임 시트 실측 + 계획서(docs/ARCHERY_PLAN_V2.md) 승인("진행"). **핵심 뿌리 발견**: 발사 방향이 `커서 레이의 3D 방향`(카메라에서 아래로 기울어진 레이) 그대로 — 화살이 땅으로 다이빙하며 "이상하게" 보였음(0.22g·속도 등 비행 파라미터는 정상).

### 변경 사항 (5파일 + 자산 1)
**`Systems/PlayerCombat.cs`**: ①커서 레이 **지면 평탄화**(ray.direction xz 정규화) — 다이빙 뿌리 수리 ②자동 조준 dir도 y=0 평탄화(타겟 중심 다이빙 방지) ③**발사 카메라 킥**(CombatCameraEffects.PlayFireKick 신설 — shake 0.4x·HitStop 없음) ④LastBowPower 정적(명중 크리틱 판정용) ⑤**C5 조준 프리뷰 OnGUI** — 드로 중 활 위치→조준 방향 골드 라인(파워 비례 길이·알파). **`Systems/ArrowProjectile.cs`**: rb.interpolation=Interpolate·minVertexDistance 0.05(A4 트레일 끊김)·`_power` 필드·명중 시 **PlayHit(Bow)/파워 풀 PlayCrit**(C2/C6)+**ShowDamageNumber 골드**(C3)+스틱 후 trail 비활성(B2 잔상 오버드로 정지). **`Systems/ArrowManager.cs`**: 발사 직후 플레이어 콜라이더 IgnoreCollision(A3) + proj._power 세팅. **`Systems/CombatCameraEffects.cs`**: PlayFireKick 정적 신설. **`Systems/GuardSelectionManager.cs`**: 선택 표시를 **Vefects Trails URP `VFX_Trail_Earth`**(TrailRenderer 리본×2 — Resources/FX/Selection/EarthTrail 복사)로 전환 — 병사 부모화, **이동 시 earth trail 잔상**(MagicCircle2·Buff 폴백), 포함 AudioSource 음소거. **자산**: EarthTrail.prefab 복사.

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0 — 중간 CS1503(float→int 데미지 숫자) 1건 수리) + 괄호 균형 0(5파일).
- Play 판정 대기: ①화살이 커서 방향(지면 수평)으로 날아감 — 땅 다이빙 소멸 ②트레일 끊김 0 ③명중 시 데미지 숫자(골드)+히트스톱 체감, 파워 풀=크리틱 ④선택 병사 이동 시 earth trail 잔상 ⑤발사 카메라 킥.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 70차 후속18 — 화살 테스트 4 실측: 발사 위치 활 앞 이동(몸 관통 수리) + 선택 표시 Magic circle 2 전환)

> **입력**: `Screenshots/화살테스트 4.mp4` 프레임 시트 실측 + 사용자 리포트 ①"발사가 어색하다" ②선택 표시를 trail VFX 자산으로. **판정**: ①화살 스폰이 **플레이어 중심(몸)** — 활 위치 아님, 몸 뚫음 ②드로 예비동작 짧음 ③트레일은 명확(보라 광대) — 개선 완료 확인 ④명중 스틱은 이펙트에 가려 보이지 않음. **보유 VFX 스캔 2차**: 전용 trail 프리팹 부재 → **Hovl Magic circles/Magic circle 2**(지면 마법진)가 선택 표시에 최적 선정.

### 변경 사항 (2파일 + 자산 1)
**`Systems/PlayerCombat.cs`**: 발사 origin을 **플레이어 중심+1.5m → 활 위치(전방 0.6m·눈높이 1.4m)** 로 이동 — 몸 관통·겹침 수리. **`Systems/GuardSelectionManager.cs`**: 선택 표시 VFX를 **Buff → Magic circle 2** 전환(Resources/FX/Selection/MagicCircle2, x1.8·지면 플랫 — Buff는 폴백 후보 유지). **자산**: `Resources/FX/Selection/MagicCircle2.prefab`(Hovl 복사).

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0 — 중간 이중 origin 선언 자가 수리) + 괄호 균형 0(103/103, 45/45).
- Play 판정 대기: ①화살이 활 위치(전방·가슴높이)에서 출발해 몸과 겹치지 않음 ②선택 병사 발밑 마법진(회전 텍스처) 생성·추종·소멸 ③기존 트레일/스틱 유지.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 70차 후속17 — 화살 테스트 3 실측: 화살 게임 감성 대형화 + 병사 크기=플레이어 정규화 + 선택 오라 VFX(Hovl Buff))

> **입력**: `Screenshots/화살 테스트 3.mp4` 프레임 시트 실측 — ①화살 본체 식별 불가(얇은 하늘색 선만 보임 — 샤프트 0.13×1.15는 탑다운 카메라에서 수 픽셀) ②사용자 요구: 병사 크기=플레이어 실측 크기, 드래그 선택 표시를 보유 VFX로 업그레이드. **보유 VFX 스캔**: Hovl Studio Magic effects pack — `Character auras/Buff.prefab`(ParticleSystem 기반 8개, VFX Graph 0 — 안정) 선정 → Resources 복사.

### 변경 사항 (4파일 + 자산 1)
**`Systems/ArrowProjectile.cs`**: 샤프트 (0.13,1.15)→**(0.25,1.8)**·촉 (0.07,0.18)→**(0.13,0.3)**·플레처 (0.03,0.22,0.08)→**(0.06,0.3,0.12)**·트레일 startWidth 0.22→**0.45**·end 0.05→**0.12** — 탑다운 카메라에서 명확한 광대+부피감(게임 감성 대형화). **`Systems/GuardManager.cs`**: `GetPlayerHeightReference`(플레이어 렌더러 bounds 실측, 폴백 1.9m)+`NormalizeSoldierScaleToPlayer`(병사 모델 높이→플레이어 높이 스케일, 클램프 0.5~5)+`SetGrounded`(스케일 후 재접지 — TerrainGenerator 수식 동일) — 프로덕션 스폰 경로 적용. **`Systems/TestTerritoryCombatSetup.cs`**: FBX 경로+GLB 폴백 경로 동일 정규화 적용(접지 전 스케일). **`Systems/GuardSelectionManager.cs`**: **선택 오라 VFX** — `_selectionAuras` 동기화(LateUpdate): 선택 병사 발밑에 `Resources/FX/Selection/Buff`(Hovl 복사본, x2.0) 생성·이동 추종·해제/사망 파괴, OnDestroy 정리. IMGUI 파란 원은 유지(오라 추가 레이어). **자산**: `Assets/Resources/FX/Selection/Buff.prefab`(Hovl 복사 — GUID 참조 유지).

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0 — 중간 CS0103(_instance→Instance 프로퍼티) 1건 수리) + 괄호 균형 0(29/29, 83/83, 44/44, 122/122).
- Play 판정 대기: ①화살이 두꺼운 광대+대형 샤프트로 명확 비행 ②병사가 플레이어와 동일 높이(나란히 비교) ③선택 병사 발밑 Buff 오라 생성·이동 추종·해제 소멸 ④오라 프리팹 미로드 시 경고 로그 확인.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 화살 테스트 2 실측 수리 — 비행 연장(중력 0.22g·속도 70) + 트레일 전체 커버)

> **입력**: `Screenshots/화살 테스트 2.mp4` — "여전히 화살이 잘 안 보인다, 날아가는 게 너무 짧다". **판정(프레임 실측)**: 화살은 보이나 트레일이 화살 바로 뒤 짧은 선에 그침 + 비행 자체가 짧음(0.45g에도 0.82s). **뿌리**: ①중력 0.45g는 여전히 남긴 아크가 짧음 ②트레일 time(0.9s)이 비행(0.82s)과 비슷해 잔상이 궤적을 못 채움 ③트레일 폭 0.13은 원거리 수 픽셀.

### 변경 사항 (2파일)
**`Systems/ArrowProjectile.cs`**: **GravityScale 0.45→0.22** — 낙하 0.82s→1.17s, 사거리 ~50m→**~80m**(속도 70 결합) ②트레일 time 0.9→**1.6s**(비행 전체를 잔상이 덮음 — 속도 70 기준 ~110m 커버)·startWidth 0.13→**0.22**·endWidth 0.025→**0.05** ③화살 모델 (0.09,0.85)→**(0.13,1.15)**(샤프트 굵게·길게). **`Systems/ArrowManager.cs`**: `_arrowSpeed` 60→**70**.

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) + 괄호 균형 0(29/29, 19/19).
- Play 판정 대기: ①화살이 긴 아크로 ~1.2s 비행(원거리 ~80m) ②트레일 잔상이 발사점→명중점 전체를 이어 화살 경로가 명확 ③밝은 색 트레일(황갈/은백/보라) 식별.

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 화살 테스트 실측 수리 — 사거리 2배 + 비행 가시성)

> **입력**: 사용자가 `Screenshots/화살 테스트.mp4` 를 넣고 "사거리가 너무 짧고 화살이 날아가는게 잘 안보인다" 보고. **뿌리 진단**: ①사거리 — 화살이 `transform + up*1.5m`에서 거의 수평(커서 레이)으로 **풀 중력(useGravity=true, 9.81)** 발사 → `t=√(2·1.5/9.81)≈0.55s` 만에 낙하, 실사거리 = 속도45·0.55 ≈ **25m**(파워풀 54·0.55≈30m). ②가시성 — 비행시간 0.55s ≪ 트레일 time 0.5s(잔상이 전혀 못 길어짐) + 트레일 startWidth 0.08·화살 반지름 0.06(과가늠) + 트레일 색상이 어두운 갈색(0.55/0.35/0.15).

### 변경 사항 (3파일)
**`Systems/ArrowProjectile.cs`**: **중력 축소 매커니즘** — `rb.useGravity=false` + `rb.linearDamping=0f` + Update에서 `_rb.linearVelocity += Physics.gravity * GravityScale * Time.deltaTime`(정적 `GravityScale=0.45f` 신설, `!_stuck && useGravity==false` 가드 — 박힌 화살 재가속 방지). 실효 0.45g → 낙하시간 0.825s → 사거리 약 2배(풀 파워 ~60m). **가시성 향상** — 트레일 `time 0.5→0.9`·`startWidth 0.08→0.13`·`endWidth 0.01→0.025`, 화살 모델 `localScale (0.06,0.7,0.06)→(0.09,0.85,0.09)`(샤프트 굵게+길게).
**`Systems/ArrowManager.cs`**: `_arrowSpeed 45→60` (축소중력과 결합, 실사거리 ~25m→~50m+).
**`Core/ArrowData.cs`**: 트레일 색상 밝게 — Regular (0.55,0.35,0.15)→(0.85,0.62,0.28) 밝은 황갈색 / Reinforced→(0.95,0.95,1.0) / Magic→(0.95,0.4,1.0).

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0) — Systems.dll·Core.dll 갱신 + `strings Systems.dll | grep GravityScale` 심볼 착륙 확인. arrow 관련 경고 0건(전체 272경고는 기존 CS0414/CS0618 폐기 프로젝트 전역).
- Play 판정 대기: ①사거리 약 2배(실측 ~25m→~50m+) ②화살이 긴 아크로 ~0.8s 비행하며 트레일 잔상 식별 ③트레일 색상이 밝게(황갈/은백/보라).

---

## 📌 세션 종합 스냅샷 (2026-09-17 ✅ 화살 액션 고품질 — 드로→릴리즈 발사 + 화살 모델(샤프트·콘촉·플레처) + 명중 박힘(stick) + 화살 사운드(드로/임팩트))

> **입력**: "화살 액션이 고품질로 나오게" — 기존 \"좌클릭 즉발 직발사 실린더 + 단순 소모\"를 끌어올리기. 계획서: `docs/ARCHERY_ACTION_UPGRADE_PLAN.md`. **뿌리 진단**: ①발사 모션이 단일 ArcheryShot 클립(드로 feel 없음) ②차지(활시위 힘) 매커니즘 없음(근접만 우클릭 차지 존재) ③발사체가 실린더 1개 ④화살 명중이 PlayerCombat 공격 경로를 안 타 임팩트 사운드 부재. 검증상 📌 `AttackSoundLayerManager`는 코드에 존재(파일명 검색 오탐에 주의) + `CombatFXGate.PlayHitFX(target, hitPos, ...)` lastHitPoint 오버로드 존재 + `PrimitiveType.Cone`은 **없음**.

### 변경 사항 (4파일)
**`Systems/PlayerCombat.cs`**: 활 드로→릴리즈 신규 경로(근접 우클릭 차지 무회귀) — 필드 `_bowDrawing/_bowDrawHeldTime/BowDrawMaxHold 0.5/BowMinFire 0.18` + 좌클릭 press(활) 시 드로 시작·해제 시 `ReleaseBow(power)` + `TryBowShot()`→`TryBowShot(float power)`(즉발 회귀 `TryBowShot(1f)`) + 드로 시작 `AttackSoundLayerManager.PlayBowDraw()`.
**`Systems/ArrowManager.cs`**: `TryShootArrow(origin,dir,baseDamage,power)` 4-arg 신설 — `speed=_arrowSpeed*(0.7+0.5*power)`(파워 0→70%, 1→120%), `damage=Round(base+bonus+power*8)`; 기존 3-arg는 4-arg(power=1f) 위임(하위 호환).
**`Systems/ArrowProjectile.cs`**: 화살 모델 조립 `AssembleArrow` — 샤프트(실린더) + 촉(절차 콘 메시 `BuildArrowHeadCone`, `PrimitiveType.Cone` 없음 → 직접 생성·양면 와인딩) + 플레처 3(Cube 120° 방사); **촉/플레처 Material 인스턴스 분리**(공유 `.color` 덮어쓰기 버그 방지); 명중 시 `_stuck=true`+`SetParent(hitGO,true)` 스틱+6초 잔존+kinematic/velocity0; 지면 꽂힘 `_stuck=true`+2초; 명중 임팩트 `AttackSoundLayerManager.PlayAttackHit(Bow,false)` 1회.
**`Systems/AttackSoundLayerManager.cs`**: `PlayBowDraw()` 신규 — `attack_swing_bow` 우선/기본 폴백, 낮은 피치 0.85·볼륨 0.65 드로 스트레치.

### 컴파일/QA
- 배치컴파일 **error CS=0**(exit 0, success banner) + 괄호 균형 0(4파일: 131/131·19/19·28/28·45/45). DLL 심볼 착륙 검증(ReleaseBow/BuildArrowHeadCone/PlayBowDraw >0).
- 서브 QA 에이전트 **10/10 PASS, 회귀 0건**(근접 차지 무회귀/화살 태그 allowlist 유지/이중 사운드 없음/파워 전달 정합/드로 상태머신 무스택/재질 분리/스틱 정합/CS0104 정규화/필드 1회 선언). 저위험 노트: 2/3-arg 위임 시 파워 풀(1f)=+8데미지·1.2x속도 — 현재 외부 호출자 없어 실영향 0.
- Play 판정 대기(테스트 40): ①활 장착 좌클릭 홀드→드로(스트레치음)+해제→파워 반영 발사, 탭=캔슬 ②화살이 실린더 아닌 샤프트+금속촉+깃털 ③적 명중 시 화살이 몸통에 6초 박힘+임팩트사운드 ④형태/명중 너무 큰 화살은 크기·피벗 튜닝.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속16 — TEST28 16차 라운드[단일 통합 인벤 그리드 + 아이템 핫바 무기 아이콘 표시])

> **입력**: 사용자 리포트 ①아이템 핫바에 등록된 무기의 아이콘 시각 표시 ②전리품 우클릭/드래그 드롭 여전히 불가 ③인벤 밖 드롭=땅에 바구니 ④장비 해제 시 인벤 복귀 ⑤재료/무기 한 인벤에 전부 표시. **뿌리 진단**: 인벤 그리드가 `GetSlotsByCategory(_selectedCategory)` — **단일 카테고리만 렌더** → 전리품 이동·장비 복귀는 실제로 일어나지만 다른 카테고리 탭에 숨겨져 "안 된다"로 보임(전리품·복귀 전송 로직 자체는 정상 — TryTakeDraggedToInventory/UnequipSlot AddItem 실측). 인벤 밖 드롭→LootBasket도 기존 구현 존재(TryDropDraggedToTerrain, 3147행).

### 변경 사항 (2파일)
**`UI/InventoryWindow.cs`**: **단일 통합 그리드 전환** — DrawItemGrid/RefreshInventory의 슬롯 조회를 `GetAllSlots()`로 교체(카테고리 필터 폐기 — 재료/무기/소모품 전부 한 그리드) + `GetGlobalSlotIndex` 3곳 호출을 루프 인덱스 직접 사용으로 교체(통합 그리드에서 표시 인덱스=전역 인덱스 — 드래그·DnD·수리 경로 유지). **`UI/HotbarUI.cs`**: 슬롯 중앙에 **아이템 아이콘 Image 신설** — ApplyAssignedVisual/1초 주기 재시도(Update)에서 `ItemIconDatabase.GetOrCreateIcon`(GLB 아이콘 우선)을 인벤 슬롯에서 조회해 Sprite 렌더(itemIds별 스프라이트 캐시). 등록 해제/변경 시 즉시 갱신.

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) + 괄호 균형 0(431/431, 103/103).
- Play 판정 대기: ①인벤 한 그리드에 재료+무기 전부 표시 ②전리품 우클릭/드래그 드롭 → 아이템이 같은 그리드에 즉시 보임 ③장비 해제 → 같은 그리드에 즉시 복귀 ④핫바 지정 무기 아이콘 표시(GLB 아이콘) ⑤인벤 밖 드롭 → 땅에 바구니(기존 기능 확인).

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속15 — TEST28 15차 라운드[테스트 35: 시각 미세 튜닝(신발 후퇴·장갑 2.8배·가면 1.15배) + 적대 병사 어그로 뿌리 수리(타겟 정책) + 전리품 우클릭 획득 + 인벤 즉시 갱신 + 이모지 제거])

> **입력**: 테스트 35 영상 + 사용자 리포트 5건. ①**적대 병사가 내 병사를 공격하지 않음** — 뿌리: `GuardPlaceholder.ResolveAttackTarget`이 **모든 GuardPlaceholder와 플레이어를 무조건 제외**(1117~1118행) → 적대 병사는 공격 대상을 절대 못 찾고 "공격 대상 상실 → 명령 해제" ②**전리품 우클릭 이동 경로 부재**(드래그만 구현) ③**장비 해제/아이템 지급 시 인벤 미표시** — `_currentSlots`가 RefreshInventory 시점에만 캐싱(외부 변화 무반영) ④장비창/창고창 글리프 누락 이모지.

### 변경 사항 (7파일)
**`Systems/GuardPlaceholder.cs`**: `HostileToPlayerFaction` 속성 신설 + `ResolveAttackTarget` 게이트 개편 — 적대 정책 가드는 **내(포섭) 병사(RecruitedSoldier 태그)와 플레이어를 공격 대상으로 허용**, 비적대는 기존 동작 유지(병사·플레이어 제외). **`Systems/GuardHostilitySystem.cs`**: ConvertToHostile/InitiateAttackVsPlayerAndSoldiers에서 `HostileToPlayerFaction = true` 세팅. **`UI/LootWindow.cs`**: 전리품 슬롯 **우클릭 = 즉시 획득**(TakeSelectedItem — 드래그 외 직접 이동 경로). **`UI/InventoryWindow.cs`**: DrawItemGrid에서 **매 프레임 슬롯 재조회**(GetSlotsByCategory) — 해제·지급 아이템 즉시 표시. **`UI/EquipmentWindow.cs`+`WarehouseUI.cs`**: 이모지 전면 제거(글리프 누락). **`Systems/ArmorVisualAttachSystem.cs`** 시각 튜닝: 신발 뒤꿈치 쪽 8% 후퇴+접지 −2cm(앞쏠림 수리), 장갑 2.4→**2.8배**+손가락 방향 15% 전진(손등 커버), 가면 1.0→**1.15배**.

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) + 괄호 균형 0(7파일).
- Play 판정 대기: ①타 영지 병사 공격 → 호감도 하락·느낌표 후 **적 병사가 플레이어/내 병사를 실제 추격·공격**(HP바 하락) ②전리품 우클릭 → 인벤 이동 ③전리품 드래그 → 인벤 드롭 ④장비 해제/아이템 지급 → 인벤 즉시 표시 ⑤장비창·창고창 이모지 박스 0건 ⑥신발 뒤꿈치 정렬·장갑 손등 커버·가면 크기.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속14 — TEST28 14차 라운드[테스트 34 실측: 자작 front 부호 슬롯별 분리(방패·가방·장갑=-Y) + 가면 전방 플로트 + 장갑 손등 밀착 + F+숫자 듀얼 입력 경로])

> **입력**: 테스트 34 영상 + 사용자 리포트(방패·가방 반대방향 / 신발 위치 미세 / 가면은 얼굴에서 앞쪽으로 떨어져야 / 장갑 손등에 더 가까이 + 방향 반대 의심 / F+숫자 여전히 미등록). 뿌리: ①**자작 front 부호가 슬롯별로 상이** — 투구·가면·신발·갑옷=+Y(테33 확정), 가방·방패·장갑=-Y(테34 실측) — 단일 부호로는 불가 ②F+숫자 미등록 — legacy Input 단일 경로(프로젝트 주력은 Input System).

### 변경 사항 (2파일)
**`Systems/ArmorVisualAttachSystem.cs`**: ①자작 front 부호 **슬롯별 분리** — frontSign = (Bag|Back|Gloves) ? -1 : +1 ②가면 위치: 전방 면 = 얼굴 표면 +0.55·b.z(마스크 전체가 얼굴 앞에 살짝 떨어져 착용 — 사용자 지시) ③장갑 바깥 오프셋 0.35→**0.15**(손등에 더 가까이). **`UI/GuardSquadHotbar.cs`**: F+1~8 등록을 **듀얼 입력 경로**로 전환 — 경로1 Input System(Keyboard.current.fKey+digit1~8 wasPressedThisFrame, GuardSelectionManager와 동일 경로), 경로2 legacy 폴백(Input.GetKey F+GetKeyDown) + **F1~F8 직접 키 대체 입력** + `F+N 입력 감지` 진단 로그(다음 라운드 미동작 원인 즉별용).

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) + 괄호 균형 0(168/168, 83/83).
- Play 판정 대기: ①방패 면=바깥·가방 탱크 방향 정상 ②신발 위치 ③가면이 얼굴 앞에 살짝 떨어진 착용 ④장갑 손등 밀착 ⑤F+1~8 또는 F1~F8 부대 등록 — **`[GuardSquadHotbar] F+N 입력 감지` 로그가 찍히는지 필수 확인**(찍히는데 등록 실패=선택 병사 0, 안 찍히면 입력 경로 문제) ⑥투구/갑옷/신발 정면 유지.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속13 — TEST28 13차 라운드[테스트 33 실측: 자작 front 부호 플립(-Y→+Y, 전 슬롯 앞뒤 반대 수리) + 장갑 2.4배·오프셋 0.35 + 가방 매몰 45% + 부대 등록 Ctrl→F+숫자])

> **입력**: 테스트 33 영상 + 사용자 리포트(투구/가면/신발/갑옷 전부 앞뒤 반대, 장갑 위치·크기 의심, 가방 더 밀착, 부대 슬롯 지정 Ctrl→F+숫자). 뿌리: 자작 front 부호 — 후속12의 -Y 판정(정점 질량 추정)이 반대였고 실착 판정(테스트 33)으로 **+Y 확정**.

### 변경 사항 (2파일)
**`Systems/ArmorVisualAttachSystem.cs`**: ①자작 front 부호 플립 — sFwdW (0,-1,0)→**(0,+1,0)**(전 슬롯 앞뒤 반대 수리) ②장갑 2.0→**2.4배**+바깥 오프셋 0.30→**0.35** ③가방 등 매몰 35→**45%**. **`UI/GuardSquadHotbar.cs`**: 부대 슬롯 등록 **Ctrl+1~8 → F+1~8**(F 홀드+숫자, HandleCtrlAssignKeys 게이트만 교체 — 드래그 선택 Ctrl은 유지, 눌패드 더블 등록 유지, 로그·주석 갱신).

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) + 괄호 균형 0(168/168, 72/72).
- Play 판정 대기: ①투구/가면/신발/갑옷 정면(이마 문양·필터·발끝·V라인이 앞) ②장갑 손등 노출 강화 ③가방 등 밀착 ④F+1~8 부대 등록(드래그 선택 후 F+숫자) ⑤기존 Ctrl+드래그 선택·숫자 부대 선택 유지 확인.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속12 — TEST28 12차 라운드[테스트 32+래퍼런스: 자작 프레임 매핑 전환 — GLB 파싱 확정 up=-Z·front=-Y + 래퍼런스 반영 배율 튜닝])

> **입력**: 테스트 32 영상(전체 착용) + 래퍼런스 스크린샷(장착 예시 1/2 — 자산 원본 렌더) + 사용자 리포트(갑옷 회전 잔존/투구 낮고 작음/장갑 더 크게/신발 부정확/가방 밀착+회전/가면 위치). **뿌리: 자작 프레임 미인지** — GLB 정점 직접 파싱으로 확정: **모든 wood 방어구는 up=-Z, front=-Y로 제작**(메시가 로컬 Z를 -1.0→0.0로 스팬, 가면 필터·투구 넥가드 질량 -Y 실측, 탱크 원통축=Z 수직). extent 랭킹(최장축=높이 가정)과 고정 yaw는 자작 축과 무관한 추정이라 매번 엇갈림.

### 변경 사항 (1파일)
**`Systems/ArmorVisualAttachSystem.cs`**: **ApplySlotAxisAlignment 전면 재작성** — 자작 프레임(sUpW=bone.TransformDirection(0,0,-1), sFwdW=(0,-1,0))을 월드 목표(up=+Y, fwd=pFwd)로 **스윙(FromToRotation)+트위스트(SignedAngle around up)** 하여 `visual.transform.rotation` 직접 대입(본 로컬 euler 혼란 제거). 슬롯 목표: 기본 up/pFwd, **Gloves up=팔꿈치→손가락**(자작 up=팔 방향 — 래퍼런스 전완+손등 커버), **Back front=pLeft**(방패 면=바깥). MeasureRootLocalAABB/랭킹/yaw 테이블 전부 미사용. **래퍼런스 반영 튜닝**: Helmet 스케일 1.12→**1.18**+위치 두개골 중심+**8% 상승**(래퍼런스=두개골 전체 감쌈), Gloves 1.6→**2.0**, Bag 등 매몰 25→**35%**(탱크 등 밀착), Mask 전방 0.4→**0.35**(얼굴 밀착).

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) + 괄호 균형 0(168/168) + 서브 QA 정적 검증 **FAIL 0건**(자작 프레임 계산/스윙-트위스트 수학/월드 회전 대입·애니 추종/슬롯 목표/피팅 상수 4종+캡/죽은 코드/폐기 참조 잔존 0 전수 OK).
- Play 판정 대기 (래퍼런스 대조): ①투구가 두개골 전체를 감싸고 정면(이마 문양) ②갑옷 수직 판재·V라인 정면 ③신발 발끝 전방·발바닥 접지 ④가면 필터 정면·눈높이 ⑤가방 탱크가 등 중앙 수직 ⑥장갑 손등+전완 노출(2.0배) ⑦재장착 크기 불변 — 엇갈리면 슬롯명 보고(자작 front 부호 ±180 한 줄 수정).

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속11 — TEST28 11차 라운드[테스트 31 실측: 재장착 커짐 버그(측정 오염) + bulge 휴리스틱 폐기→슬롯별 고정 yaw 테이블 + 장갑 ×1.6·손등 노출])

> **입력**: 테스트 31 영상 + 사용자 리포트 3건. ①**같은 장비를 다시 우클릭하면 장비가 점점 커짐** — 바디 측정이 플레이어 렌더러를 GetComponentsInChildren로 수집하며 **기존 부착 장비 메시까지 포함** → 부위 bounds가 부풀고(예: 머리 영역에 기존 투구 포함) → 재장착 시 scale보정이 커지고 그 장비가 다시 다음 측정에 포함되는 복리 폭등 ②**슬롯마다 회전이 틀려 제대로 부착 안 됨** — 투구/신발/가면 180°, 갑옷 90°, 가방 90~110°(bulge 부호 휴리스틱이 자산마다 오작동) ③**장갑 여전히 안 보임** — 손 부피 중심 배치는 손 메시 안에 파묻힘 + 크기 부족.

### 변경 사항 (1파일)
**`Systems/ArmorVisualAttachSystem.cs`**: ①**측정 오염 제거** — TryMeasurePlayerBody에 excludeRoots 파라미터 신설, 호출부에서 `_slotVisuals`로 수집한 기존 장비 비주얼 루트 전달, 렌더러가 장비 비주얼 하위(IsChildOf)면 측정 스킵 → 재장착 커짐 버그 뿌리 수리 ②**회전 전면 개편** — bulge 부호 휴리스틱(Swing-Twist R2) 완전 폐기 → **R1(long→목표축 스윙: 눕기/서기) + 슬롯별 고정 yaw 테이블**(월드 up 축 기준): Helmet 180/Armor 90/Bag −90/Mask 180/Shoes 180(long→+pFwd 고정)/Back 0/Gloves 0 — **정면이 반대면 표의 yaw 숫자 한 줄(±180/±90)만 수정하면 됨** ③**장갑** — 스케일 1.3→**1.6배** + 위치를 손 부피 중심에서 **몸 중심축→손 방향(수평 바깥=손등)으로 장갑 치수 30% 오프셋**(손 메시 밖으로 노출). 로그에 yaw 필드 추가.

### 컴파일/검증
- 배치컴파일 **에디터 점유로 미실행**(사용자 에디터 사용 중 — UnityLockfile) — 괄호 균형 0(170/170) + 직전 라운드(후속10) 동일 파일 BakeMesh 경로 컴파일 error CS=0 실적. 에디터 포커스 시 자동 리컴파일, 콘솔 CS 에러 확인 요망.
- Play 판정 대기: ①같은 장비 반복 장착 시 크기 불변(커짐 소멸) ②투구/신발/가면 정면(180 보정), 갑옷/가방 측면 보정 — **틀린 슬롯은 `[ArmorVisual] 정렬` 로그의 yaw 값 보고 → 표 한 줄 수정** ③장갑이 손등에 보이게(1.6배+바깥 오프셋) ④부위별 part 치수 안정.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속10 — TEST28 10차 라운드[테스트 30 실측: 플레이어 메시 isReadable 예외 뿌리 수리(BakeMesh+meta) + 아군 오인 피해 차단(사용자 요구) + 인벤 mid-draw 인덱스 수리 + 헬멧 치수 캡])

> **입력**: 테스트 30 영상 + 피팅/콘솔 로그. ①**"Not allowed to access vertices (isReadable is false)" 예외 스팸** — TryMeasurePlayerBody의 mesh.vertices가 플레이어 메시에서 예외 → 측정 도중 중단 → 부착마다 다른 부분 측정(Helmet part=(0.11,0.15,0.33)→(0.73,0.25,0.54)→(0.44,0.19,0.75) 들쭉날쭉, scale보정 x3.00 클램프 폭발, 가면/장갑 피팅 로그 부재) ②InventoryWindow.DrawItemGrid 1279행 IndexOutOfRangeException — 그리드 그리는 도중 장착 리프레시가 _currentSlots를 더 짧은 새 배열로 교체 ③사용자 요구: **내가 때렸을 때 내 소속 병사는 피해를 입지 않게(아군 오인 피해 차단)** — 합세 기능 자체는 유지.

### 변경 사항 (코드 4파일 + meta 2개)
**`Systems/ArmorVisualAttachSystem.cs`**: ①`TryMeasurePlayerBody` 정점 접근 안전화 — SkinnedMeshRenderer는 **BakeMesh**(isReadable 무관·현재 포즈 반영, tmp Mesh 즉시 Destroy), MeshFilter는 isReadable 가드로 조용히 스킵, 렌더러 처리 전체 try/catch(1회 요약 경고)로 **단일 렌더러 실패가 측정 전체를 중단하지 않음** ②Helmet 피팅 **치수 상한 0.6m 캡**(scale 클램프 후/localScale 적용 전) — 헤어 스파이크로 Head bounds가 비정상 커질 때 x3.00 폭발 방지. **meta**: Player_Rigged.fbx.meta·Player_Rigged_Heat.fbx.meta `isReadable: 0→1`. **`Systems/PlayerCombat.cs`**: `IsOwnSoldier`(RecruitedSoldier 태그) 헬퍼 — FindTargetInCursorDirection 2개 루프 타겟 선정 제외 + 차지강공 빈 스윙 + AttackTarget 조기 return(데미지·VFX·합세통보 차단). **`Systems/ArrowProjectile.cs`**: 화살 적중 태그에서 RecruitedSoldier 제외 → **내 병사 관통**(지면/벽 분기 회피 no-op). **`UI/InventoryWindow.cs`**: 그리드 루프를 `slotsLocal` 로컬 참조 기준으로 전환 + `if (i >= slotsLocal.Length) break;` 가드 — mid-draw 배열 교체 인덱스 예외 뿌리 수리.

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) — 중간 CS0246(Exception, using System 부재) 2건 System.Exception 정규화로 수리. 괄호 균형 0 + 서브 QA 정적 검증 **FAIL 0건**(IsOwnSoldier 태그 계층/필터 2곳/가드 2곳/합세·적대화 호출 생존/화살 분기 순서/BakeMesh 블록/헬멧 캡 위치/slotsLocal 전수/meta 무변경 확인).
- Play 판정 대기: ①isReadable 예외 스팸 0건 + 피팅 로그 part 치수가 매 부착마다 안정 ②가면/장갑 피팅 로그 정상 출력 ③내 병사를 때려도 피해 0(빈 스윙/화살 관통) — 적 병사·몬스터는 정상 피해 ④투구가 두개골 크기(0.6m 캡 이내) ⑤인벤에서 장비 연속 장착 시 IndexOutOfRangeException 0건.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속9 — TEST28 9차 라운드[피팅 1차 Play 실측 → 렌더러별 샘플링 전환(부위 과소 측정 뿌리) + Gloves 바디 피팅 추가(손등·1.3배) + 가방/가면/신발 배율 튜닝])

> **입력**: 피팅 로그(Helmet part=(0.20,0.15,0.28)/Armor=(0.23,0.10,0.25)·(0.15,0.18,0.26)/Shoes=(0.13,0.05,0.14) — **전 부위 측정치가 실제보다 대폭 과소**) + 스크린샷 6장(투구=여전히 작음, 갑옷=뒤집힘 소멸✅, 신발=크기 양호·미세 부유, 가면=약간 작음, 가방=치우침·10~15% 작음, 장갑=손바닥면 부착 → 손등+확대 요구). 뿌리: **전역 3000 샘플 상한이 앞쪽 렌더러(몸통)에서 소진** → 뒤 렌더러(머리/헤어/손/발 메시) 정점 0개 기여 → 부위 bounds가 몸통 잔여 정점으로만 구성.

### 변경 사항 (1파일 — 3종)
**`Systems/ArmorVisualAttachSystem.cs`**: ①`TryMeasurePlayerBody` **렌더러별 독립 샘플링** 전환 — 전역 상한 폐기, 렌더러당 1500 샘플(step=max(1,n/1500))로 전 정점 범위 커버 → 머리/손/발 메시가 반드시 측정에 기여. 발 반경 0.35→0.28/손 0.35→0.25(발목·전완 오염 감소) ②**Gloves 바디 피팅 추가** — 본 이름(left)으로 좌/우 손 part 선택, scale=손max치수×1.3/gloveMax(사용자 확대 요구), center→손 부피 중심(손바닥 평면→손을 감쌈 — 손등 쪽으로 이동). part invalid 시 기존 상수(손 본−2cm) 폴백 유지 ③배율 튜닝: 가방 0.95→1.05·등 밀착 0.4→0.25, 가면 0.95→1.0·눈높이 0.15·전방 0.4, 신발 접지 part.min.y−0.01(미세 부유 흡수), 투구 1.12 유지(두개골 정측정으로 자동 확대). **Back(방패)만 상수 경로 잔존**.

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) + 괄호 균형 0 + 서브 QA 정적 검증 **FAIL 0건**(샘플링 커버/Gloves 분기·좌우 선택/폴백 경로/튜닝 값 4종/회귀 전수 OK).
- Play 판정 대기: ①투구가 두개골을 감쌈(측정 정상화로 자동 확대) ②장갑이 손을 감싸고 손등 쪽(크기 1.3배) ③가방 등 중앙 밀착·확대 ④가면 얼굴 확대 ⑤신발 지면 밀착 ⑥갑옷 유지 — `[ArmorVisual] 피팅` 로그의 part 치수가 실제 부위 크기와 일치하는지 먼저 확인.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속8 — TEST28 8차 라운드[스크린샷 6장 개별 판정 → 플레이어 메시 정점 실측 기반 바디 피팅 신설 + 부츠 축 매핑·갑옷 부호 수리])

> **입력**: 스크린샷 6장(핼멧/갑옷/신발/장갑/가면/가방) + anchor 콘솔 로그(투구 본↔중심 0.26 등). 판정: **장갑 ✅ 정상**, 투구=매몰·크기과소, 갑옷=앞뒤 뒤집힘+부유, 신발=옆으로 눕기+매몰, 가면=매몰·약간 과소, 가방=매몰+측면 기울어짐. 뿌리: ①**"1.0m 치비" 상수 추정이 실제보다 작음**(Unity 실측 Head 본 y=1.44/Spine 1.12/발 0.93 — 파일측정 0.89×1.0×0.4와 임포트 스케일 불일치, 3차 정정 끝에도 오차) ②**부츠 GLB 긴축=발길이(toe-heel)**인데 long→up 매핑으로 옆으로 눕움 ③갑옷 bulge 부호 반대(주 판=등에 메는 에셋) → 상수 추정 폐기, **플레이어 메시 정점 실측 피팅**으로 전환.

### 변경 사항 (1파일 +230행 — 신규 2)
**`Systems/ArmorVisualAttachSystem.cs`**: `PlayerBodyMeasure.TryMeasurePlayerBody` 신규 — 플레이어 메시 정점(≤3000 샘플, L2W 월드 변환)을 본 기준 영역 분류(Head=본y−5cm↑ / Torso=Spine−20cm~Head−5cm / 발·손=본 반경 35cm, 본 null 시 이름 폴백) → 부위별 월드 bounds(정점 ≥5개 valid). `FitVisualToBodyPart` 신규 — InstantiateAttached에서 바디 실측 1회(루프 밖) 후 축 정렬 직후 호출, **Helmet/Armor/Shoes/Mask/Bag만** 피팅(성공 시 기존 worldAnchor+스냅 스킵, 실패 시 기존 상수 경로 폴백): 투구=두개골max(x,z)×1.12 감쌈·중심=두개골중심 / 갑옷=torso.x×1.25·중심=몸통중심 / 신발=발길이×1.15·발바닥 접지(min.y→part.min.y) / 가면=두개골×0.95·얼굴표면(faceCenter)·눈높이 / 가방=torso.x×0.95·등표면 90% 밀착. 스케일 클램프 0.3~3.0 + `[ArmorVisual] 피팅` 실측 로그. **축 정렬 규칙 수정**: Shoes long→+전방(발끝)·mid→up·thin→좌(옆으로 눕기 뿌리 수리), Armor bulge→−전방 플립. **Gloves/Back 기존 경로 불변**(장갑 정상 판정 — 회귀 방지).

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0, "Exiting batchmode successfully now!") + 괄호 균형 0 + 서브 QA 에이전트 정적 검증 **FAIL 0건**(호출 순서/피팅 수학/영역 분류/R1·R2 강체회전 직교 보존/회귀 전수 OK).
- Play 판정 대기: ①투구가 두개골을 감쌈 ②갑옷 주 판이 가슴(뒤집힘 소멸) ③부츠가 발에 똑바로(옆으로 눕기 소멸) ④가면이 얼굴 표면 ⑤가방 등 중앙 밀착 ⑥장갑/방패 회귀 없음 — `[ArmorVisual] 피팅` 로그의 part 치수·스케일 보정값으로 판정.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 후속7 — TEST28 7차 라운드[장비 부착 회전 보정 전무 뿌리 확정 + 축 자가 정렬 신설 — 가면 기울어짐/가방 회전틀림/부츠 옆으로 눕기])

> **입력**: 테스트 29 영상(장비 개별 부착 — 가면 기울어짐·부유, 가방 회전틀림, 부츠 옆으로 눕기, 무기 손 근처 어색 부착) + anchor 콘솔 로그(5슬롯 본↔중심 0.02~0.20m — 위치 앵커는 정상 범위). 뿌리: **회전 보정 전무** — `_poseTable`의 LocalEuler가 전부 Vector3.zero라 GLB가 원본 저자 오리엔테이션 그대로 부착(위치는 본+실측 오프셋으로 맞지만 축이 캐릭터 축과 불일치).

### 변경 사항 (1파일 +190행 — 신규 메서드 2)
**`Systems/ArmorVisualAttachSystem.cs`**: `ApplySlotAxisAlignment` 신규 — InstantiateAttached 루프 내 NormalizeVisualScale 직후/SnapVisualToBone 직전 호출(회전 → bounds 재계산 → 스냅 반영 순서 보장). ①**본로컬 AABB 축 랭킹**(mesh.bounds 8코너 → visual-root-local 누적 → long/mid/thin) ②**정점 centroid−bounds.center 부르주 부호**(첫 메시 ≤2000 샘플) ③**Swing-Twist 2단계**(R1=FromToRotation(long→목표축) + R2=AngleAxis(SignedAngle around tgtLong)) ④**슬롯 규칙**: Helmet=long→up(twist 없음)/Armor=thin볼록→+전방/Bag=볼록→−전방/Mask=볼록→+전방/Back(방패)=볼록→+좌/Gloves=long→손가락방향(elbow=LeftLowerArm·RightLowerArm, null→up)·thin→좌/Shoes=long→up·toe축 볼록→+전방 ⑤degenerate 가드(목표축 평행 시 twist 스킵) ⑥`[ArmorVisual] 정렬` 실측 로그(long/thin/bulge/euler). `MeasureRootLocalAABB` 헬퍼 신규. **위치 앵커 상수 일절 불변**(실측 검증 완료분).

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0, "Exiting batchmode successfully now!") + 괄호 균형 0 + 서브 QA 에이전트 정적 검증 **FAIL 0건**(축 랭킹/swing-twist 수학/호출 순서/기존 상수 불변/중복 정의 0/C# 문법 전수 OK).
- Play 판정 대기: ①가면이 얼굴 정면(기울어짐 0) ②가방 등에 수직 정렬 ③부츠 발에 똑바로(옆으로 눕기 소멸) ④장갑 손가락 방향 ⑤투구 정수리 유지 ⑥방패 바깥면 수직 — 어긋나면 `[ArmorVisual] 정렬` 로그의 long/thin/bulge/euler 값으로 슬롯 규칙 즉시 튜닝.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 69차 — TEST28 4차 라운드[장비 앵커/어그로 플립 차단/병사 EXP·전리품 순서/화살 속도/단검·창 그립/활 Idle·Run 배선/더블 눌패드/자체 드롭/정렬 제거])

> **입력**: 테스트 26 영상(병사 HP바 "경비병 Lv.1" ✓, 창 head-up 수직, 병사-슬라임 대치 무공격) + 68차 로그(클램프 x0.40 하한, 어그로 등록 후 무공격, 단검 0.206m, 활 발사 8회). 뿌리: ⑴스케일 클램프 하한 0.4 절단 ⑵어그로 Alert 머묾+플레이어 타격마다 어그로 플립 ⑶드롭 판정이 인벤 열림 시만 대행 ⑷Cylinder/스케일 등은 68차 수리 확인.

### 변경 사항 (11파일 + 컨트롤러 + 에디터 스크립트 신규)
**ArmorVisualAttachSystem**: 클램프 하한 0.12 + 앵커 모드(Helmet/Boots=Bottom 접지, Back=손+전방 0.1) + 오프셋 재튜닝. **ArrowManager/ArrowProjectile**: 속도 45 + 트레일 0.5s. **WeaponEquipManager**: 단검 정점 그립(전략2·0.6) + 창 LocalEuler (-180,180,0). **AnimalAI**: NotifyAttacker 즉시 Combat + TakeDamage 병사 타격("guard") 어그로 플립 게이트. **GuardPlaceholder**: TakeDamage("guard") + 킬 크레딧 AddEXP(15/킬, level×50 레벨업, maxHP+10) + Die() 전리품 1.2s 지연 코루틴(DieLootAndDeactivate) + Update _isDead 게이트 + ClearCommand. **GuardHeadUI 부착 유지**. **GuardSquadHotbar**: HandleDoubleNumpadKeys(눌패드 두 번 0.6s → 슬롯 등록). **InventoryWindow**: 정렬 버튼 제거(_sortMode None 고정) + IsOpenNow 정적. **WarehouseUI/LootWindow**: 인벤 닫힘 시 자체 MouseUp 드롭 판정(TransferDraggedToInventory/TryTakeDraggedToInventory 재사용). **Assets/Editor/BowClipWiring.cs(신규)**: AnimatorController API 배치 배선(멱등).

### 활 애니 최종 배선 (BowClipWiring 배치 실행 — exit 0)
- **BowIdle**(신규, Idle_Holding_Bow): 활 장착+정지 = 활 든 대기(장착 시 애니 변화 확보). 진입: Idle→BowIdle(IsBow+Speed<0.2, 선두)/BowAimedF→BowIdle(Speed<0.2 — 67차 전이 재지정)/BowRunF→BowIdle.
- **BowAimedF**(활 걷기 Walk_Forward_with_Bow_Aimed 유지): 진입 BowIdle→BowAimedF(IsBow+Speed>0.55)/Walk→BowAimedF(68차).
- **BowRunF**(신규, Run_Forward_with_Bow): 활 들고 뛰기. BowAimedF→BowRunF(Speed>5.0)/BowRunF→BowAimedF(<4.2). 런 경계는 Play 판정 후 조정.

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) + 배치 배선 exit 0 — 중간 CS0102(GSM Instance)/CS0029(void→bool)/CS1061(AnimatorCondition.parameter) 3건 자가 수리.
- Play 판정 대기: ① 장비 딱 붙음 ② 몬스터가 병사 즉시 추격·공격→HP바 하락·쓰러짐→전리품 ③ 병사 킬→Lv 상승 ④ 화살 빠른 비행 ⑤ 단검/창 그립 ⑥ 활 든 대기+활 뛰기 ⑦ 더블 눌패드 등록 ⑧ 인벤 닫힌 채 창고/전리품 드래그 드롭 ⑨ 인벤 무정렬.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 68차 — TEST27 3차 라운드[장비 크기 정규화+본스냅/병사 HP바·Lv·킬체인/화살 축정렬/활걷기 복귀전이/GSM 자가치유])

> **입력**: 테스트 25 영상(프레임 실측) + 67차 Editor.log 진단. 실측 뿌리: ⑴장비 GLB가 플레이어 대비 3~4배(헬멧 1.37m)+공중 부양 0.5m ⑵병사 데미지는 정상 — 몬스터 HP바 미표시+몬스터가 병사를 못 때림+병사 HP바 부재 ⑶화살 스폰 3회 성공이나 Cylinder 축 불일치(Y길이 vs Z진행)로 엣지온 비가시 ⑷활 걷기는 67차 전이 후 복귀 경로 부재 ⑸GSM 생성됐는데 [RTS] 로그 0건 = 공유 GO 파괴로 Update 사망.

### 변경 사항 (8파일 + 컨트롤러 + 신규 1)
**`Systems/ArmorVisualAttachSystem.cs`**: 슬롯별 목표 치수 정규화(`_targetSize`: 헬멧 0.34/갑옷 0.62/부츠 0.36/장갑 0.24/방패 0.85m — 최장축 균등 스케일, 클램프 0.4~5.0) + `SnapVisualToBone`(bounds 중심→본 원점+슬롯 오프셋 스냅) + 로그에 스케일·본↔중심(경고 임계 0.35m).
**`Systems/GuardHeadUI.cs`(신규)**: 병사 머리 위 이름+Lv+HP바(IMGUI, 비율색 녹/노랑/빨강 — MonsterHeadUI 규약). `TestTerritoryCombatSetup` CreateGuard/SpawnGuard 양쪽 부착.
**`Systems/GuardPlaceholder.cs`**: PerformAttack에 `AnimalAI.NotifyAttacker(gameObject)`(병사 타격→몬스터 어그로) + `_maxHP` 10→25 + Die()에 쓰러짐 연출(전도+콜라이더 비활성).
**`Systems/AnimalAI.cs`**: `NotifyAttacker(GameObject)` 신규 — _aggroTarget/_aggroAttacker 등록 + MonsterAggroSystem.NotifyAttack. 기존 GetAliveAggroDamageable 근접 경로로 병사 피격.
**`Systems/ArrowProjectile.cs`**: Spawn 회전에 X+90° 곱함(Cylinder 길이축 Y→진행방향 Z 정렬 — 엣지온 비가시 뿌리) + 스케일 0.06/0.7 + 트레일 width 0.08.
**`Player_AC.controller`**: `Idle→BowAimedF`(IsBow+Speed>0.55 — Idle 전이 목록 선두 배치로 우선순위 확보) + `Walk→BowAimedF`(IsBow) 신설 — 67차 BowAimedF→Idle과 왕복 루프 완성.
**`Systems/GuardSelectionManager.cs` + `TestTerritoryCombatSetup.cs`**: GSM/RTSCommandSystem을 전용 GO(DontDestroyOnLoad)로 생성 변경 + `GuardSelectionWatchdog` 신규(1s, 부재→재생성 — 기존 Instance 프로퍼티 재용, CS0102 중복 제거).
**`Systems/PlayerCombat.cs` + `Core/GameManager.cs`**: 독립 클릭 프로브(30s 쿨) + GameManager.OnDestroy 스택트레이스(공유 GO 파괴자 확정용).

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0) — 중간 CS0102(GSM Instance 이중 정의)/CS0103(appliedScale 스코프) 2건 자가 수리.
- Play 판정 대기: ① 장비 몸 맞음(본↔중심 ≤0.35m) ② 병사 머리 위 이름+Lv+HP바 ③ 병사 타격→몬스터 HP바 하락·사망 ④ 몬스터→병사 공격·쓰러짐 ⑤ 화살 가시 비행 ⑥ 활 걷기 복귀 ⑦ [RTS] 로그+드래그 ⑧ GameManager OnDestroy 스택트레이스.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 67차 — TEST26 2차 라운드[병사 데미지 숫자/장비 자가치유 워치독/활 정지 대기 전이/드래그 진단·늦은Ctrl/정점 기반 그립])

> **스코프**: 66차 후 Play 재실측. ①병사 애니는 해결 확인(추종·합세 정상). 뿌리 2차 확정: ⑴병사 데미지는 정상 적용(HP=7.7/35 로그) — 숫자만 무표시 ⑵장비는 이벤트 발화 후 수신 0건 = 구독 유실(공유 GO 파괴) ⑶활은 Walk_Forward_with_Bow_Aimed 클립이 제자리 루프(활 전용 Idle 클립 부재 — MeshyUser 전수 확인) ⑷드래그는 진단 로그 0건 ⑸그립은 world AABB 왜곡(검 1.47m vs 원본 1.0m — 사용자 실측 로그).

### 변경 사항 (4파일 + 컨트롤러 1)
**`Systems/GuardPlaceholder.cs`**: PerformAttack에 `CombatVFXController.ShowDamageNumber`(골드) 추가 — 병사 타격도 데미지 숫자 표시.
**`Systems/ArmorVisualAttachSystem.cs`**: 싱글턴(Instance)+`_subscribedTo` 추적 + `SyncTick()`(재구독+InitialSync+슬롯 리컨실레이션) + 신규 `ArmorVisualSyncWatchdog`(자체 DontDestroyOnLoad GO, 0.5s 주기: 시스템 부재→재생성, 구독 유실→치유). 이벤트 유실과 무관하게 ≤0.5s 내 장비 부착.
**`Player_AC.controller`**: `BowAimedF→Idle` 전이 신설(Speed<0.2, 모드4/Less, dst=Idle 3594517172120623874) — 정지 시 자연 대기(활은 손 프롭). 파라미터/상태 추가만, 삭제 없음.
**`Systems/GuardSelectionManager.cs`**: 모든 좌클릭 `[RTS] 좌클릭 감지 ctrl={}` 진단 로그 + 늦은 Ctrl 흡수(홀드 중 Ctrl 나중에 눌러도 드래그 시작).
**`Systems/WeaponEquipManager.cs`**: GripPose에 `GripStrategy`(0/1/2)+`GripGuardFactor` 추가 + `ComputeGripPointsLocal`(mesh 정점 20 슬라이스 단면 분석 — 최대 단면=가드/창날 → 손잡이 방향 계수 지점 정점 무게중심) → 그립점 손 원점 스냅 + 손오차 로그. id 테이블: 검(전략2·0.5)/창(전략2·0.75)/활(전략1). world AABB 기반 경로는 폴백으로 강등.
**정점 파싱 실측**: 검 x≈0 가드(단면 0.46)·x>0.3 손잡이(0.14~0.2)·x<-0.3 칼날 / 창 z≈-0.9 창날(0.09)·샤프트(0.03~0.05) — 그립점 수치 확정의 근거.

### 컴파일/검증
- 배치컴파일 **error CS=0**(exit 0, 1회 통과) + 변경 4파일 괄호 균형 0.
- Play 판정 대기: ① 병사 타격 골드 숫자+HP바 하락 ② 장착 ≤0.5s 가시 부착(리컨실레이션 로그) ③ 활 정지 시 자연 대기+좌클릭 화살 ④ `[RTS] 좌클릭 감지` 로그로 드래그 판별 ⑤ `그립 정렬(정점)` 손오차≈0.

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 66차 — TEST25 타모델 회귀 5건 전면 수리[병사애니 FBX복원/장비 즉시가시부착/그립 id오버라이드/활 스탠스 실측/Ctrl드래그 지속])

> **스코프**: 타 AI 모델 세션(64~65차) 이후 사용자 5건 리포트(①병사 애니 미재생 ②활 전용 애니+화살 비행 ③장비 장착 비가시 ④Ctrl+드래그 선택 ⑤무기 그립 불일치)를 Editor.log 실측 + GLB 바이너리 파싱으로 뿌리 확정 후 6파일 수리. 배치컴파일 **error CS=0**(exit 0).

### 뿌리 원인 확정 (실측)
- **①병사 애니**: `Soldier_*.glb` 파싱 — **임베디드 애니메이션 0개** + 뼈대 중첩(`metarig/Root/spine/spine.001/...`). 반면 `Soldier_*.anim` 클립 바인딩은 **flat 단일 경로**(foot.L, shin.L, shoulder.L, Root) → Unity 제너릭 바인딩이 GLB 중첩 경로와 불일치 → GLB 몸통 무애니(T포즈). FBX는 flat 계층이라 클립 경로 일치(59차 애니 동작 실측). 63차 GLB 우선 로드가 뿌리.
- **③장비 비가시**: 부트(Awake) EquipDemoStarterGear → ArmorVisualAttachSystem이 6슬롯 전부 "부착 시작" 수신 후 **성공(✅)·본 미발견·로드 실패 로그 전무**(AttachRoutine 코루틴이 첫 yield 후 재개 없이 침묵 사망). wood_*.glb 전부 존재 확인 — 로드 문제 아님.
- **⑤그립**: 검 `bounds 비신뢰(비대칭 1.06 미달) → 테이블 포즈` 폴백 / 활 `그립 정렬 offset=(0.295, 0.811, -0.355)` — 활 GLB는 Y축 1.9m **좌우 대칭(피벗=중앙=손잡이)** 인데 휴리스틱이 하단 활끝을 그립으로 잡아 손에서 0.8m 이탈 / 스윙 트레일 `타입=Fist`(Attach 호출부 타입 미전달).
- **②활**: Player_AC에 BowEnter 트리거+전이(HasExitTime=0)+IsBow 조건 전이 다수+상태(BowAimedF/BowBack1/DrawShoot) 존재 확인 — 발사·적중은 마지막 세션 로그로 동작 실측(TryBowShot 4회+ArrowProjectile OnTriggerEnter).
- **④드래그**: 선택 로직(좌표계·태그·IsRecruited) 정상 — Update 초단 `if(!ctrl) return;`이 **드래그 중 Ctrl 해제 시 즉시 취소**(박스·선택 동시 실패 체감) + SelectGuardsInRect 카메라 null 무로그 얼리리턴.

### 변경 사항 (6파일)
**`Systems/TestTerritoryCombatSetup.cs` (#1/#3)**: CreateGuard 본 분기 **FBX 우선 + GLB 재질 이식(CopyMaterialsFromGlb)** 로 복원(59차 검증 조합 — FBX=애니 보장, GLB=재질 소스) + GLB는 FBX 실패 폴백으로 격하 + avatar 실측 로그. EquipDemoStarterGear를 **0.25s 지연 코루틴**(부트 레이스 흡수 — Awake 중 코루틴 침묵 사망 차단).
**`Systems/ArmorVisualAttachSystem.cs` (#3)**: `TryAttachImmediate` 동기 즉시 부착 경로 신설(본+GLB 즉시 해결 시 코루틴 없이 같은 프레임 완료) + `InstantiateAttached` 공용화 + **성공/실패 무조건 로그**(bounds size·본↔중심 거리 실측, 1m 이탈 경고) — 침묵 경로 제거. Back(방패) 포즈 (0,0.02,0.06) 이격.
**`Systems/WeaponEquipManager.cs` (#5)**: GripPose에 `GripCenter`/`IdOverride` 필드 + **무기 id별 그립 오버라이드 테이블 `_gripTableById`**(wood_sword/wood_bow/wood_spear — GLB 파싱 실측 기반) 신설, GetGripPose 우선순위 id→glbKey→type. **활 중앙 그립 분기**(그립점=bounds 중앙 — 하단끝 휴리스틱 차단), IdOverride는 bounds 비신뢰 가드 우회. `그립 실측` 로그(손↔bounds중심 거리). 스윙 트레일 Attach에 **type 전파**(Fist 버그 수리).
**`Systems/HumanoidClipDriver.cs` (#2)**: 무기 타입 전환 시 `[Anim] 무기 타입 전환 → BowEnter 트리거 발화` 실측 로그(스탠스 전이 증거 고정).
**`Systems/PlayerCombat.cs` (#2)**: 활 발사 성공 실측 로그(화살 비행+ArcheryShot 동시 고정).
**`Systems/GuardSelectionManager.cs` (#4)**: 드래그 **시작 후 Ctrl 해제 허용**(Ctrl=시작 조건으로 한정 — `if(!_isDragging && !ctrl) return` 구조) + 드래그 시작/미확정 실측 로그 + 시작 시 카메라 갱신 + SelectGuardsInRect 카메라 null 경고 로그.

### 스코프 판정 (미변경)
- `Systems/GuardManager.cs` LoadSoldierModel(프로덕션)은 **ForceBiped 경로**(ModelAnimatorAssigner — 36차 검증)라 FBX-first 전환 무의미 → 미변경(회귀 리스크 회피). 테스트 씬(Test_10) 병사만 FBX 우선 복원.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(exit 0, 1회 통과) + 변경 6파일 괄호 균형 0(부모 실검증).
- Play 판정 대기: ① 병사 걷기/대기 애니(FBX+GLB 재질 — `병사 FBX 부착(애니 보장)` 로그) ② 활 장착 시 `[Anim] 무기 타입 전환 → BowEnter` 후 활 스탠스 + 좌클릭 `활 발사 성공` ③ 시작 0.25s 후 투구/갑옷/장갑/부츠/방패 가시(`[ArmorVisual] ✅ ... 비주얼 부착` 로그) ④ Ctrl+드래그 박스→`[RTS] N명 선택` + 파란 원 ⑤ `[Weapon] 그립 실측` 로그로 검/창/활/방패 손 위치 판정(활은 0에 수렴).

---

## 📌 세션 종합 스냅샷 (2026-09-16 ✅ 65차 — 테스트24 후속 8건 전면 수리[병사 GLB/방어구 폴백/무기 그립/활 좌클릭+조준/RTS 좌표계/4레이어 UI])

> **스코프**: 테스트24 이후 사용자 8건 리포트(①방어구 시각 미부착 ②무기 그립 이상[창 역방향·활/검 어긋남] ③화살 애니 미전환 ④내 병사 GLB/애니 미부착 ⑤RTS 드래그 안 됨 ⑥활 우클릭 발사→좌클릭+조준 업그레이드 ⑦인벤 정렬 문구 안 보임 ⑧전체 UI 업그레이드)를 Editor.log 실측으로 근본 원인 확정 후 9파일 수리. 배치컴파일 **error CS=0**(exit 0) + "Exiting batchmode successfully now!".

### 뿌리 원인 확정 (Editor.log 실측)
- **①방어구**: 플레이어 아바타가 Generic/FBX(비 Humanoid)라 `ResolveBones()`의 HumanBodyBones 조회가 null → "부착 본 미발견"으로 전부 스킵(WeaponEquipManager는 H-GRIP2 이름 폴백으로 이미 해결, 방어구엔 없었음).
- **②무기 그립**: 창 `그립 정렬(피벗=그립부 신뢰 pivotT=0.96)` — 피벗이 창두(끝)에 있어 신뢰 분기로 짧게 종료되며 창두가 손에 붙음(역방향). 활 `그립 오프셋 과보정 클램프 (-0.113,-0.697,-0.554)→(-0.5,-0.5)` — 0.5m 클램프가 활의 큰 정렬 오프셋 절단(어긋남).
- **③화살 애니**: ArcheryShot 트리거는 정상, 현재는 좌클릭/우클릭 경로 혼재(우클릭도 ArrowManager로 발사 — ⑥과 동일 뿌리).
- **④병사 GLB**: GuardManager(프로덕션 재충원) 경로가 ① 확장자 포함 로드 단일 ② 태그 무부여 — 테스트 경로(TestTerritoryCombatSetup)만 수정돼 있어 프로덕션 누락.
- **⑤RTS 드래그**: 드래그 rect는 InputSystem(좌하단 y-up), 병사 점은 WorldToScreenPoint 후 y-뒤집기(좌상단)로 **y-좌표계 미스매치 → Contains 항상 실패 → 0명 선택**.
- **⑥활 우클릭**: HumanoidClipDriver L705 `CurrentType==Bow && GetMouseButtonDown(1)` 잔존 우클릭 발사 브랜치가 ArrowManager로 화살 발사(PlayerCombat 좌클릭 차지 스킵만으로는 불충분).
- **⑦정렬 문구**: 62차 팔레트 전환 후 _styleButton 텍스트색이 배경과 대비 부족.
- **⑧UI**: Loot/Warehouse는 여전히 평면 GUI.skin.box(인벤/장비/월드맵만 4레이어 적용).

### 변경 사항 (9파일, 신규 0)
**`Systems/GuardManager.cs` (A/④)**: 재충원 병사 GO 태그 부여(SetRecruited(true)→`"RecruitedSoldier"`, else `"Guard"`) + 모델 로드 헬퍼 `LoadSoldierModel(modelPath)`(확장자 없는 경로 우선→.glb 폴백)로 교체 + 모델 로드 성공/실패 로그.
**`Systems/ArmorVisualAttachSystem.cs` (B/①)**: `FindBoneByName(animator, keywords)` 정적 헬퍼(WeaponEquipManager H-GRIP2 패턴) 추가 → ResolveBones의 HumanBodyBones 조회 실패 시 이름 폴백 적용(Helmet→head / Armor→spine·chest / Feet→foot_l·foot_r / Hands→hand_l·hand_r / Back LowerArm→lowerarm·forearm).
**`Systems/WeaponEquipManager.cs` (C/②)**: GripPose에 `GripClampMax` 필드 추가(활/창=1.0, 검=0.5 — 0.5 상수 폐지) + 창(Spear) GripEnd=-1 강제(피벗=창두 신뢰 분기 차단, 그립부=자루 끝으로 → 창두 전방).
**`Systems/HumanoidClipDriver.cs`+`PlayerCombat.cs` (D/③·⑥)**: HumanoidClipDriver L705~714 우클릭 활 발사 브랜치 제거(폭탄 좌클릭 브랜치는 유지) → 활은 좌클릭 TryBowShot 단일 경로. TryBowShot에 자동 조준 보정 추가 — 커서 Ray가 적을 못 맞히면 `FindTargetInCursorDirection()` 재사용·전방 반구(cos>0.7)로 가장 가까운 적 향해 dir 보정.
**`Systems/GuardSelectionManager.cs` (E/⑤)**: SelectGuardsInRect 병사 화면 점의 y-뒤집기 제거 → 드래그 rect(좌하단)와 좌표계 통일(Contains 정상화). DrawSelectionBoxGUI는 IMGUI용으로만 y 뒤집어 표시. 선택 조건 `IsRecruited || tag=="RecruitedSoldier"` OR 확장. [RTS] 로그에 rect 포함.
**`UI/InventoryWindow.cs` (F/⑦)**: 정렬 버튼 전용 `_styleSortButton`(gold `ColorSortText=(1,0.85,0.4)`) 추가 → L915 사용 — 공용 버튼은 기존 흰색 유지(회귀 최소화).
**`UI/LootWindow.cs`+`WarehouseUI.cs` (F/⑧)**: InventoryArtLibrary 4레이어(드롭섀도우→GetBackplate 백플레이트→GetMetalFrame+4모서리→GetTitleBanner) 중세 백그라운드 적용, 기존 평면 배경/타이틀바 렌더 제거(이중 렌더 금지), 한글 제목 UIFont.Title(38)×_uiScale, static 캐시(OnGUI new GUIStyle 0건), 창 높이 화면 클램프. Loot +90/-16, Warehouse +124/-1.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(exit 0, 1회 통과) + "Exiting batchmode successfully now!"
- 정적 QA-Lite: 변경 9파일 괄호 균형 0(부모 실검증), 오탐지 1건은 써드파티 Free Slash VFX 데모 에셋(class명≠파일명) — 무관.
- Play 판정 대기: ①방어구 헬멧/갑옷/부츠/장갑/방패 캐릭터 부위 표시 ②검 손바닥·창 창두 전방 상향·활 시위/손잡이 정렬 ③활 장착 시 좌클릭 발사+화살 애니, 우클릭은 행동 없음+가까운 적 자동 조준 ④주요 병사(재충원) 3D GLB+걷기/대기 애니+RecruitedSoldier 태그 ⑤Ctrl-좌클릭 드래그 상자→병사 파란원 다중 선택(+[RTS] N명·rect 로그) ⑥인벤 정렬 버튼 골드 문구 가독 ⑦Loot/창고 중세 4레이어 배경 일관.

### 🔧 65차 후속 (사용자 "테스트씬10에서 하나도 변화 없음" — 부트 상태 미노출 원인 수리)
**진단**: Test_10 부팅 시 플레이어가 무기·방어구를 자동장착하지 않아(Fist 상태) ①방어구·②무기 그립이 시작 화면에 안 보이고, 병사 GLB는 avatar 미설정으로 T포즈(④애니 미재생). 코드는 정상 컴파일·실행(에러 0)이나 테스트 씬이 수리 결과를 부트 화면에 노출하지 않음.
**수리(`TestTerritoryCombatSetup.cs` +124)**: ① `EquipDemoStarterGear()` 신규 — 플레이어 시작 즉시 wood 방어구 풀셋(helmet/armor/boot/glove/shield)을 인벤 보장 후 `EquipmentManager.EquipItem`으로 장착(→OnEquipmentChanged→ArmorVisualAttachSystem 비주얼 부착) + `WeaponEquipManager.Equip("weapon_spear_wood",Spear)` 창 시작 무기(그립 전방). 각 단계 try-catch 격리·크래시 금지. ② CreateGuard GLB branch에 FBX Humanoid avatar 지정(같은 레벨대 FBX avatar 로드→Animator.avatar) → SoldierShield_AC 클립이 발 퇴 매핑돼 병사 걷기/대기 애니(④ 해소). 배치컴파일 **error CS=0**(exit 0).
- Play 판정 추가: 부팅 즉시 플레이어에 갑옷/투구/장갑/방패+창 그립, 병사 걷기/대기 동작(부트 확인). ①권 동작들은 직접 장착/활발사/Ctrl드래그로 확인. (배치컴파일 잠김 이슈 없음 — 컴파일 전 tasklist 보강)

---

## 📌 세션 종합 스냅샷 (2026-09-13 ✅ 46차 — 타격 체인 대폭 정리(에셋 전용 피격)+Stylized Slash 스윙 아크 적용+주황 링/화면 틴트 제거)

> **스코프**: 테스트 8 기반 사용자 결정 — "파티클 다 없애라. 피격은 Hit Effect FREE 에셋으로만, 스윙은 stylized slash 에셋(slash5-HungNguyen)을 휘두르는 방향에 따라". 결과 구성: 스윙=흰 무기 트레일+골드 스타일라이즈드 아크(방향 추종) → 피격=Guz BasicHit 에셋+데미지 숫자+카메라/히트스톱. 그 외 절차 파티클/링/화면 틴트 전부 제외. 배치컴파일 error CS=0 + 정적 QA FAIL 0건.

### 변경 사항 (5파일 — 신규 1: StylizedSlash.prefab)
**`Systems/CombatFXGate.cs` (P1/P4)**: PlayHitFXInternal 77→46행 축소 — 제거: SpawnHitSparks(금색 스파크)/SpawnBloodSplatter/HitVFX 크리 구체/SpawnHitDebris/ShockwaveRingFX 전투 링(발밑 주황 표시의 정체)/ScreenFlashFX.FlashOrange(화면 황색 틴트)/SpawnCritBurst. 유지: PlayImpact(Guz 에셋)+ShowDamageNumber+PlayHitFlash(대상 흰 번쩍)+CombatCameraEffects+ImpactSoundFX+FlashWhite(0.05 극미량). 예산/오버로드/미수정 영역 원본 보존(바이트 정밀 치환).
**`Resources/FX/Slash/StylizedSlash.prefab`(신규)+`Systems/SlashVFXRunner.cs` (P3)**: slash5-HungNguyen "white-yellow bolder"(VFX Graph, OnPlay 자동 재생) 복사 — meta guid 신규 발행(원본 중복 방지, 내부 vfx 참조 바이트 동일 보존). `PlaySlashStage(position, direction, stage, yawSign)` 신규 — 카메라 수평 빌보드(38차 규약)+yawSign 좌우 플립(휘두르는 방향 추종)+stage 롤(1타 수평/2타 -90 수직/3타 -45 사선)+스케일 1.2(튜닝 상수)+1.5s 자가파괴+0.25s 전용 쿨다운.
**`Systems/HumanoidClipDriver.cs` (P3)**: FireComboSlash에서 Player 모드 한정 PlaySlashStage 호출(스윙 앵커 fwd0.9+up1.2, ComboStageDirection 방향, yaw 부호) — **ComboStageDirection 복원**(42차 제거분 — 1타 -58°/2타 63.2°/3타 143.6°+전방 반구 클램프, git 이전 커밋에서 실측 원본). Soldier 경로 도달 없음. 구식 주석 정리.
**`Systems/SlashVFXRunner.cs` (P2)**: BasicHit(Guz) 유지 — Basic Hit 8 (NEW) 교체는 회귀 방지로 보류(주석 명기), 틴트 흰 유지.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(exit 0, 1회 통과)
- 정적 QA(서브에이전트): 5파일 diff 전수/시그니처 일치(PlaySlashStage↔호출부·ComboStageDirection 복원)/미수정 영역 보존/guid 유일(원본 중복 0)/제거 7종 호출 0건+유지 토큰 전수/Soldier 도달 0/균형 — **FAIL 0건**
- Play 판정 대기: ① 타격 시 골드/금색 스파크·파편·붉은 점·발밑 주황 링·화면 황색 틴트 전부 소멸 ② 피격=Guz 히트 에셋+흰 번쩍+데미지 숫자만 ③ 스윙 시 골드 스타일라이즈드 아크가 1타 수평/2타 수직/3타 사선+방향 추종 ④ 흰 무기 트레일 유지 ⑤ 크로스와 임팩트 동일 톤

### 🔧 46차 후속 수리 (사용자 "전혀 안고쳐졌어" — 뿌리 추가 확정)
**원인**: 몬스터 피격 FX는 CombatFXGate와 **별개 경로**였음 — `AnimalAI.TakeDamage`(698-717행)가 SpawnHitSparks/SpawnBloodSplatter/HitVFX.PlayHitEffect/HitVFX.SpawnDamageNumber/ShowDamageNumber를 **자체 직접 호출**(이중 발화) → 46차 Gate 정리와 무관하게 절차 파티클이 그대로 보임. 슬래시 발화 로그(stage/yawSign)는 정상 출력 확인 — "안 고쳐진 것"은 이 잔존 경로.
**수리**: `AnimalAI.TakeDamage` FX 블록 → PlayHitFlash 1줄만 유지(비플레이어 피격 소스 대비)+절차 파티클/숫자 2종 제거(PlayerCombat→CombatFXGate 단일 경로로 통일). `AnimalAI.Die()` 사망 혈흔 제거. `MonsterSkillSystem` 절차 파티클 15콜 전부 제거(스파크/블러드 — 지시 5곳+전수 발견 10곳). 잔존 전수: AnimalAI/MonsterSkillSystem 0건(정의부·StealthAssassination만 남음).
**컴파일**: error CS=0(1회 통과) — Play 판정 대기 갱신: 동일 항목 + 몬스터 타격 화면에서 절차 파티클 0건.

### 🔧 46차 후속2 (사용자 "슬래시/피격 임팩트 부착 안 됨" — 위치 뿌리 수리)
**원인(로그 실측)**: ① 피격 임팩트 = Gate GameObject 오버로드가 target.transform.position(모델 피벗) 사용 → Editor.log pos y≈2.9 공중 부양(실제 타격 지점 아님) ② 슬래시 = 월드 고정점(루트 fwd0.9+up1.2) 스폰 → 플레이어 이동 시 아크가 몸과 분리.
**수리**: ① `CombatFXGate.PlayHitFX(GameObject target, Vector3 hitPos, ...)` 신규 오버로드(PlayHitFlash 유지+hitPos 전달) + PlayerCombat이 LastHitPoint(bounds center+0.2, 375행 직전 갱신) 전달 — 임팩트가 실제 타격 지점에 부착 ② 크로스 앵커 up1.2→0.6 ③ `PlaySlashStage(..., Transform playerRoot)` — 인스턴스를 플레이어에 SetParent(worldPositionStays=true, 스폰 정렬 유지+이동 추종) + 앵커 근접화(fwd0.9+up1.2→fwd0.55+up1.25) + 스케일 1.2→1.5.
**컴파일**: error CS=0.

### 🔧 46차 후속3 (사용자 "테스트 9 전혀 적용 안됐어" — 슬래시 미렌더 확정)
**진단**: 테스트 9 프레임 전수 픽셀 스캔(4fps 81프레임) — 골드 아크 픽셀 0(노이즈 ≤4px). 슬래시 발화 로그는 정상, 프리팹/참조 guid 정상, 로드 실패/셰이더 에러 0건 → **VFX Graph가 URP에서 출력하지 않음**(출력 블록 렌더 파이프라인 타겟 불일치 추정 — 코드 복구 불가 영역).
**수리**: SlashVFXRunner에 **alive 진단 러너**(SlashAliveProbe — 스폰 0.35s 후 aliveParticleCount 체크) 추가 → alive≤0이면 `NotifyVfxDead()` 플래그 + 로그, 이후 발화부터 **구 "FX/Slash/Slash VFX" 폴백** 전환(URP Shader Graph — 42차 이전 렌더 실적, 8 MeshRenderer). 폴백도 동일 파이프라인(빌보드/yawSign 플립/stage 롤/SetParent 추종/스케일 1.5/1.5s 파괴/쿨다운 공유). alive>0 복귀 시 스타일라이즈드로 자동 복귀. LastHitPoint up 0.2→0.1 하향(부착감).
**컴파일**: error CS=0(1회 통과).

## 📌 세션 종합 스냅샷 (2026-09-14 ✅ 47차 — 임팩트를 Guz Magic Hit 2로 교체+StylizedSlash 기동 복구+진단 강화)

> **스코프**: 사용자 결정 — ① 피격 임팩트를 "hit effect free"의 **Magic Hit 2**로(공격 예시와 가장 유사) ② 슬래시는 Free Slash 폴백이 아니라 **stylized slash 본체** 사용(에셋 문서상 URP 지원). 진단: 테스트 10 세션의 alive=-1 = VFX "not awake" — 초기 이벤트만으로 미기동 가능성.

### 변경 사항
**`Systems/SlashVFXRunner.cs`+`Resources/FX/Impact/MagicHit.prefab`(신규)**: Guz "Magic Hit 2.prefab" → Resources 복사(meta guid 신규 발행, 머티리얼 4종은 builtin 셰이더 211=Legacy Particles/Alpha Blended라 URP 변환 불필요 — 텍스처 guid 원본 해결). 임팩트 로더 BasicHit → MagicHit 교체(BasicHit은 Resources 유지 — 롤백 시 상수 1줄). **TintParticles 제거로 에셋 고유 색상 유지**(사용자 선택 룩 존중), NormalizePurpleParticles는 안전망으로 유지. 크로스 경로도 MagicHit 공유.
**`Systems/SlashVFXRunner.cs` (StylizedSlash 복구)**: 스폰 오리엔테이션 완료 직후 `ve.Reinit()+ve.Play()` 명시 기동(초기 이벤트 미기동 대응). SlashAliveProbe 2시점 진단(0.35s/1.0s — alive>0이면 즉시 정상 종료), **두 체크 모두 alive≤0일 때만** Free Slash 폴백 전환(늦게 피는 이펙트 오판 방지), alive=-1 미각성 2회 연속 시 별도 로그("[SlashVFX] VFX 미각성 지속 — 에셋 로드/타겟 확인 필요"). 진단 로그: assetNull/awake/alive 상세 출력 — 다음 Play에서 뿌리 즉시 판별.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(exit 0) + 정적 검증 통과(균형/grep 전수)
- Play 판정 대기: ① 피격 = Magic Hit 2 이펙트(마법 심볼+플래시+쇼크웨이브)가 타격 지점에 표시 ② 슬래시 = stylized slash 아크 렌더(명시 Play로 기동) — 여전히 미출력이면 진단 로그의 assetNull/awake/alive 값이 다음 수리 방향 결정 ③ 폴백은 두 체크 모두 실패 시에만 발동

### 🔧 47차 후속 (사용자 "테스트 11: 슬래시 다른 에셋/전리품 아이템 미표시/임팩트 더 밀착")
**진단**: ① 슬래시 미렌더 뿌리 = **원본 에셋 자체 결함** — slash5 프리팹의 vfx 참조 fileID(...526)가 .vfx 파일 내 실제 VisualEffectAsset 오브젝트(...527)와 불일치(제작자가 vfx 재생성 후 프리팹 미갱신) → runtime assetNull=True. 복사본 참조를 ...527로 수리 → 스타일라이즈드 렌더 예상(Free Slash 폴백은 안전망으로 유지) ② 전리품창 빈 슬롯 = DrawLootPanel 슬롯 루프 sx에 panelX 오프셋 누락 — 아이템 슬롯이 화면 좌측(x≈8)에 렌더(캐시 항목수=1 정상이었음). sx=panelX+... 수리(드래그 판정 Rect 연동 수리) ③ 크로스 앵커 Lerp 중간지점 → LastHitPoint+up*0.15 대상 밀착.
**컴파일**: error CS=0. Play 판정 대기: 스타일라이즈드 아크 렌더/전리품 슬롯에 아이콘+이름+개수 표시/크로스 대상 밀착.

### 🔧 47차 후속2 (fileID 수리에도 assetNull=True 지속 → 참조 의존 제거)
**진단**: ...527 수리 후 최신 세션에서도 assetNull=True — 프리팹 직렬화 참조가 에디터 환경에서 계속 해결되지 않음(재임포트 환경차 추정).
**수리**: 프리팹 참조 의존 제거 — `StylizedSlashVFX.vfx`를 Resources/FX/Slash에 복사(meta guid 신규) → 스폰 시 `Resources.Load<VisualEffectAsset>` 직접 로드 후 `ve.visualEffectAsset` 런타임 할당+Reinit/Play. 로드 실패(null) 시 즉시 NotifyVfxDead → 폴백(진단 대기 없음). SlashAliveProbe 진단에 `assetAssigned=` 필드 추가 — 다음 Play에서 할당 성공/실패 즉시 구분.
**컴파일**: error CS=0.

### 🔧 47차 후속3 (assetAssigned=False의 진범 확정 — VFX Graph 패키지 미설치)
**진단**: assetAssigned=False = Resources.Load<VisualEffectAsset> null — 확인 결과 **Packages/manifest.json에 com.unity.visualeffectgraph가 아예 없었음** → .vfx 임포터 자체가 부재 → slash5 .vfx 임포트 불가 → Resources.Load null + 원본 프리팹 참조 null(모든 assetNull의 단일 뿌리).
**수리**: manifest.json에 `com.unity.visualeffectgraph: 17.4.0` 추가(URP 17.4.0 동일 트레인) → batch resolve 성공(PackageCache 설치 확인) → .vfx 임포트 정상화. 슬래시는 Resources.Load 경로로 stylized 렌더 예상 — 여전히 문제가 있으면 진단 로그(assetAssigned/alive)가 다음 단계 지시.
**컴파일**: error CS=0. Play 판정 대기: stylized slash 아크 렌더(패키지 설치 후 첫 검증).

### 🔧 47차 후속5 (사용자 로그로 확정 — 할당 순서 버그)
**진단**: Resources.Load 성공(로드 실패 로그 0건)인데 probe가 assetNull=True — ve 블록 순서가 `할당 → Reinit → Play` → **Reinit()가 직렬화 값(프리팹의 깨진 null 참조)으로 되돌리며 런타임 할당을 덮어씀**.
**수리**: ① SlashVFXRunner ve 블록 재정렬 — `Reinit() → visualEffectAsset 할당 → Play()`(순서 필수 주석) ② PlayHitFlash에 `HasProperty("_Color")` 가드 — ShaderGraph 재질('Slash World'/'Trail', 폴백 슬래시가 플레이어 자식으로 포함) get_color 에러 스팸 제거(캐시/복원 양쪽 자동 적용).
**컴파일**: error CS=0. Play 판정 대기: stylized slash 아크 렌더(순서 수리 후 첫 검증)/히트 플래시 에러 스팸 0건.

### 🔧 47차 후속6 (여전히 assetNull — 진단 패턴 재해석: 인스턴스에 VisualEffect 컴포넌트 자체가 없음)
**진단**: assetNull=True/awake=False/alive=-1 조합 = probe의 GetComponent<VisualEffect>()가 **null** — 깨진 직렬화 참조(m_Asset=null로 풀림) 프리팹은 Instantiate 시 VisualEffect 컴포넌트가 생략됨 → 할당/Reinit 코드 자체가 스킵되고 있었음.
**수리**: 프리팹 Instantiate 폐기 → **런타임 빌드**: new GameObject+AddComponent<VisualEffect>+visualEffectAsset 직접 할당(Resources 로드 캐시)+Reinit+Play+기존 오리엔테이션 파이프라인(빌보드/플립/롤/SetParent/스케일1.5/1.5s 파괴/probe). 프리팹 로더/캐시/상수 제거. VFXRenderer는 AddComponent 시 자동 부착(internal 타입).
**컴파일**: error CS=0. Play 판정 대기: stylized slash 아크 렌더(프리팹 의존 완전 제거 후 첫 검증).

### 🎨 47차 후속7 (사용자 피드백: 슬래시 렌더 성공 → 색/크기 조정)
**적용**: ① 색상 — white-yellow → **white-blue 변종** 교체(Resources/FX/Slash/StylizedSlashVFX.vfx 내용 교체, 경로/guid 유지 — 피격 Magic Hit 2 파란색과 매칭) ② 크기 — 고정 1.5 제거 → **공격범위 연동**(사거리×0.4, 클램프 0.8~1.6: 검 2.5m→1.0/창 4m→1.6/맨손 2m→0.8, WeaponRangeIndicator 사거리 표와 동일 소스).
**비고**: 에디터 개방으로 배치컴파일 잠김 — 에디터 포커스 시 자동 컴파일. Play 판정 대기: 파란 아크+범위 맞춘 크기.

### 🔧 47차 후속4 (패키지 설치 후에도 SG "missing" — AssetDatabase 임포트 캐시 결함 해소)
**진단**: 패키지 설치 후에도 slash5.shadergraph가 AssetDatabase에서 임포트 시도조차 안 됨(로그 전수 — 이 파일만 0회, 다른 .shadergraph는 정상 임포트). 강제 재임포트(mtime 변경)도 무시 → AssetDatabase 임포트 캐시가 VFG 설치 전 실패 상태로 고착.
**수리**: 배치모드 -executeMethod로 `AssetDatabase.ImportAsset(ForceUpdate)` 강제 → **SG 임포트 성공**(Object=True, 이름=slash5) + .vfx 5종 재컴파일 — "cannot be compiled ... missing" 0건(이전 세션 대비). StylizedSlashVFX.vfx도 재컴파일 완료 → stylized slash 렌더 조건 완성. 진단 스크립트(Assets/Editor/DiagSlashShaderGraphImport.cs)는 향후 SG 임포트 진단용으로 유지.
**컴파일**: error CS=0. Play 판정 대기: stylized slash 아크 렌더(모든 장애물 제거 후 첫 검증).

## 📌 세션 종합 스냅샷 (2026-09-13 ✅ 45차 — 보라 파티클 진범=마젠타 셰이더 에러 뿌리 수리+BOTW 팔레트 통일+임팩트 단일화+방어구 비주얼 부착 신규+전리품창 5×2+화살 탭)

> **스코프**: 테스트 7 영상 픽셀 실측으로 "보라 파티클"의 진범 확정 — 평균 RGB (219,19,219)=**Unity 셰이더 에러 마젠타**. 런타임 파티클이 기본 머티리얼(Particles/Standard Unlit)로 생성되어 URP 미지원 → 마젠타 렌더. 40차 틴트/44차 색 교체/보라 정규화가 무효였던 근본 이유. 텍스처 전수 확인(HIE/Guz 전부 무채색)으로 코드 색·텍스처는 무죄. 배치컴파일 error CS=0 + 정적 QA FAIL 0건.

### 변경 사항 (10파일 — 신규 2: FXPalette/ArmorVisualAttachSystem)
**`Systems/FXPalette.cs`(신규)+`CombatVFXController.cs` (P1)**: FXPalette(Core=흰/Accent=골드(1,.9,.5)/Edge=주황(1,.55,.2)/Blood=붉은 + URP 호환 파티클 머티리얼 지연 캐시 Sprites/Default 우선) 신설 → CVC 런타임 파티클 6지점(HitSparks/BloodSplatter/AssassinationFlash/AssassinationBlood/HitDebris/CritBurst) 전부 ApplyTo. 프로젝트 전수 스윕 결과 CVC 외 런타임 PS 생성 9파일 전부 기존 명시 머티리얼 존재 — **미지정 지점 0건** (마젠타 위험 완전 소멸).
**`Systems/AnimalAI.cs`+`PlayerCombat.cs` (P2)**: 데미지 숫자 색 팔레트화 — 일반=Core(흰)/크리·강타=Accent(골드)/기타=Edge(주황) (구 녹/빨강/노랑 교체).
**`Systems/SlashVFXRunner.cs` (P3)**: 임팩트 단일화 — PlayCross TravisHit 스폰 제거→BasicHit 단일 경로 위임, BasicHit2(Construct) 분기 제거, 크로스 틴트=골드/임팩트 틴트=흰(팔레트 정렬). 시그니처/쿨다운/static 캐시 보존, TintParticles/NormalizePurpleParticles 유지.
**`Systems/WeaponSwingTrail.cs`+`PlayerCombat.cs` (P4)**: 폭 곡선 3키(0,1.0)/(0.6,0.65)/(1,0)+TrailTime 0.22s+`Pulse()` 신규(히트 순간 폭 1.25배 0.12s — TrailPulseRunner 내부 컴포넌트)+AttackTarget 적중 분기 훅 1줄.
**`UI/InventoryWindow.cs` (P5/P7)**: 전리품 그리드 전용 상수 LOOT_COLUMNS=5/LOOT_ROWS_MAX=2 분리(메인 6열 무영향)+창고 카테고리 탭에 Arrow(화살 🏹) 추가 — 시딩된 화살 3종이 탭에서 보임.
**`Systems/ArmorVisualAttachSystem.cs`(신규)+`EquipmentManager.cs`+`CoreSystemsBootstrap.cs` (P6)**: 방어구 GLB 비주얼 부착 신규 — OnEquipmentChanged(EquipmentSlot,string) 구독+초기 동기화, Helmet→Head/Armor→Spine/Shoes→양발/Gloves→양손/Back(방패)→좌수 본 부착(id left/right 토큰 분기), Weapon 슬롯 스킵, GLB 로드=WeaponEquipManager와 동일 Resources 경로, 아바타 지연도착 5초 폴링(실패 시 1회 경고 스킵), 해제/교체 파괴, 플레이어 Tag 전용(병사 오염 0). 부트 Ensure 배선.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(exit 0) — 중간 1회 수리(HumanBodyBones.LeftForeArm→LeftLowerArm)
- 정적 QA(서브에이전트): 10파일 diff 전수/시그니처/회귀/마젠타 잔존 전수(0건)/균형 — **FAIL 0건**. 참고: CombatFXGate·HumanoidClipDriver에 구식 주석(BasicHit2/TravisHit 언급) 잔존 — 동작 무영향, 다음 라운드 정리 후보
- Play 판정 대기: ① 타격 시 마젠타 0건+흰/골드/주황 팔레트 체감 ② 크로스/임팩트 동일 톤 ③ 히트 순간 트레일 펄스 ④ 방어구(wood 투구/갑옷/부츠/장갑/방패) 장착 시 캐릭터에 GLB 부착+해제 시 제거 ⑤ 전리품창 5칸×2줄 ⑥ 창고 화살 탭에서 화살 3종 표시+활 발사

## 📌 세션 종합 스냅샷 (2026-09-13 ✅ 44차 — 바구니 E키 즉시닫힘 뿌리 수리+공격 FX 품질 개편(BOTW 레퍼런스)+그립 bounds 가드+장비칸 무기 동기화+창고 화살 시딩)

> **스코프**: 테스트 6 영상+로그 기반 리포트 6건 뿌리 수리. 진단 확정: ① E키=LootWindow GO 씬 전무(Instance null → 열림(Loot) 직후 DrawLootPanel:2432 즉시 CloseContext 로그 실측) ② 반짝임=FX 발화 중(골드링 로그+영상 프레임14 링 확인)이나 파티클만 미보임 ③ 공격 FX=보라 사각 파티클+주황 블롭 폭발이 촌스러움(BOTW 레퍼런스 스펙 확보: 흰/노랑 코어+직선 스파크, 보라 없음, 1-2프레임) ④ 그립=목검 bounds 정육면체(1.41,1.43,1.29)로 최장축 판정 노이즈(프레임 12/15 무기 부유) ⑤ 장비칸=무기는 WeaponEquipManager만 갱신(패널은 EquipmentManager 소스) ⑥ 화살=시딩 0("활 발사 실패 — 화살 부족" 6회). 배치컴파일 error CS=0 + 정적 QA FAIL 0건.

### 변경 사항 (11파일)
**`UI/LootWindow.cs`+`UI/InventoryWindow.cs` (P1)**: `EnsureInstance()` 신규 — Instance 부재 시 new GO+AddComponent(idempotent, Show() 미호출로 팝업 경로 비활성 유지, UIWindow Awake null-safe 확인). DrawLootPanel이 EnsureInstance 사용 → lootWindow==null 즉시 닫기 원천 제거. 열림/캐시 항목수/닫기 사유 3종 진단 로그(1회성 가드).
**`Systems/TestTerritoryCombatSetup.cs`+`TestAllInOneSetup.cs` (P6)**: 창고 시딩 화살 3종 추가(arrow_regular/reinforced/magic ×20, category=Arrow, ArrowManager 상수와 id 일치) — wood 장비/재료/Gold 유지.
**`Systems/CombatVFXController.cs`+`SlashVFXRunner.cs` (P3)**: SpawnHitSparks → 골드화이트(1,0.9,0.5) 직선 스트릭 12개(velocityScale 0.2, 0.15~0.25s, Shape 반경 0.05 밀집). SpawnCritBurst → 화이트 코어 플래시((1,0.95,0.8))+주황 외곽 직선 스파크 12개(붉은 플래시/블롭/붉은 스플래시 제거). `NormalizePurpleParticles` 신규 — 보라 판정(r>0.4∧b>0.4∧g<r·0.55∧g<b·0.55) → 골드화이트(알파 보존), main.startColor+colorOverLifetime 그라디언트 키 순회, PlaySlash/PlayCross/PlayImpact 3경로 적용(40차 틴트 후 호출 — 수학 검증 상호 충돌 0).
**`Systems/LootSpawnFX.cs`+`ShockwaveRingFX.cs` (P2)**: 파티클 머티리얼 우선순위 Sprites/Default 우선(URP 확실 렌더)+크기 0.2·32개·0.8s+셰이더 진단 로그 1회(_shaderLoggedOnce). 링: LineRenderer 아닌 Cylinder 메시 → RING_THICKNESS 0.05→0.09(1.8배)+RGB 화이트 러프 30%·알파 하한 0.75 부스트(전 링 공통, Spawn 시그니처 보존).
**`Systems/WeaponEquipManager.cs`+`EquipmentManager.cs`+`Core/PlayerInventory.cs` (P4/P5)**: bounds 신뢰 가드 — 최장/차장축 비율 <1.15(BoundsTrustMinRatio) → bounds 정렬 스킵+테이블 포즈 사용+TargetLen 기반 tipWorld 반환(트레일 부착 보호). 실측: 검 L/S=1.01 가드 발동/창 1.53·활 1.48 기존 유지. `SetWeaponSlot(itemId[,itemData])` 신규(Weapon 슬롯 순수 등록)+`Equip` 성공 분기 말미 등록 훅(CurrentId 세팅 후)+`UnequipSlot(Weapon)` → `WeaponEquipManager.Unequip()` 위임(CurrentId 가드 이중 해제 방지, 인벤 복귀 생략 — 무기 장착은 인벤 소모 없음이 기존 설계)+`PlayerInventory.GetItemById` static 헬퍼(리플렉션 1회 캐시, public static readonly 필드 대상).

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(exit 0) — 중간 SlashVFXRunner colorOverLifetime API 3회 수리(col.color.mode 체크+col.color.gradient+MinMaxGradient 래핑)
- 정적 QA(서브에이전트): 11파일 diff 전수 일치/SetWeaponSlot 체인 null 가드/GetItemById 리플렉션 안전/UnequipSlot 이중 해제 방지/EnsureInstance Awake 부작용 없음/NormalizePurpleParticles×40차 틴트 상호 보완(수학 검증)/시그니처 보존/균형 — **FAIL 0건**
- 관찰 1건(비차단): 핫바 단축 id("steel"/"wood") 장착은 full-id가 아니라 GetItemById 미조회 → 장비칸 itemData null 폴백(이름 미표시 가능) — 45차에서 full-id 전파 검토
- Play 판정 대기: ① E키 바구니 → 통합창 열림 유지(전리품 그리드+전부 획득) ② 바구니 골드 파티클+굵은 링 가시 ③ 타격 시 골드화이트 직선 스파크+화이트 코어 크리(보라 0건) ④ 목검 손에 정확히 부착 ⑤ 우클릭 무기 장착 → 좌측 장비칸 무기 셀 표시+셀 클릭 해제 시 손에서 제거 ⑥ 화살 소모 활 발사

## 📌 세션 종합 스냅샷 (2026-09-13 ✅ 43차 — 우클릭 소모품 복용+공격 FX 체감 수리+바구니 반짝임 보강+치명 예외 2건 소멸)

> **스코프**: 사용자 리포트 3건(우클릭 소모품 미작동/슬래시 제거 후 스윙·히트 FX 부재/바구니 반짝임 미보임)을 Editor.log 실측 기반으로 뿌리 원인 확정 후 전부 수리. 배치컴파일 error CS=0 + 정적 QA FAIL 0건.

### 뿌리 원인 확정 (Editor.log 실측)
- **우클릭**: "[Inv] 우클릭: 치유초 cat=Herb 컨텍스트=None" 7회 — 게이트가 Potion/Drug만 통과해 Herb(치유초) 차단, AutoRoute 무경로로 조용히 소멸. 물약 복용 훅은 완성 상태였으나 세션 내 물약 우클릭 시도 자체 0건(미검증).
- **FX 부재 3겹**: ① [Equip]/그립/트레일 부착 로그 0건 = **맨손** — 트레일은 무기 GLB 장착 시에만 Attach(설계상 맨손 무시) ② 공격 실패 85회 vs TravisHit 크로스 4회 — 적중 희소로 히트 FX 자체가 거의 미발화 ③ "You can only call GUI functions from inside OnGUI" **48회** — ScreenFlashFX.ScreenFlashRunner.Init(83행)이 OnGUI 밖 GUI.skin 호출 → 히트 체인 HighSpec 블록(크리 버스트/임팩트 사운드) 단절.
- **바구니 반짝임**: 절차 폴백 스파이크가 크기 0.07m·16개·지면 y=hit.point 발화(하프 지하) → 시인성 0. LootWipe 프리팹 부재(HDRP 판정) 자체는 정상.
- **보너스**: GuardPlaceholder.Die() 627행 DropTableManager.Instance NRE 1회 — 장비 드랍/최소보장/후처리 전부 스킵.

### 변경 사항 (6파일)
**`Systems/ScreenFlashFX.cs`**: Init에서 GUIStyle 생성 제거(필드 저장만) + OnGUI 지연 생성 폴백 유지(39차 DamageNumberRunner 선례 동일 패턴) → 48회 예외 소멸 + 크리 버스트(SpawnCritBurst)/임팩트 사운드(ImpactSoundFX.PlayHit) 경로 복구.
**`Systems/GuardPlaceholder.cs`**: Die() 631행 `DropTableManager.Instance != null ? ...GetSoldierTable() : null` 삼항 가드(null이면 기존 폴백 골드 블록 자동 실행) + 전리품/드랍 섹션(바구니 생성~최소보장) try-catch 격리(사망 로직 EXP/이벤트/GuardManager/파괴는 try 밖 유지).
**`UI/InventoryWindow.cs`**: 우클릭 게이트(1358행)+복용 훅(3381행)에 **Herb 추가** — 치유초는 PotionUseSystem 회복 분기(displayName "치유" 매칭)로 MaxHP×40% 회복+소모. 미매칭 약초(독나물/황혼초)는 "[Inv] 복용 불가(연금술 재료용 약초)" 로그로 구분. Weapon/Armor 분기는 카테고리 일치만 진입 → Herb 장착 미탈 보장(순서 실측 확인).
**`Systems/WeaponSwingTrail.cs`**: TipWidth 0.06→0.09 + 신규 `EnsureBareFist(Transform player)` — _trail null 가드(무기 트레일 절대 덮지 않음), RightHand 본 3단 탐색(Animator GetBoneTransform→이름 검색→루트 폴백), 기존 Attach 재사용(팁=손 위치).
**`Systems/HumanoidClipDriver.cs`**: FireComboSlash의 SetEmitting(true) 직전 Player 모드 한정(mode == DriveMode.Player) EnsureBareFist 호출 — **맨손 공격에서도 흰 궤적 표시**. Soldier 경로(UpdateSoldier) 도달 없음 확인.
**`Systems/LootSpawnFX.cs`**: 스파이크 보강 — 오리진 up+0.4m(지하 발화 방지)·크기 0.07→0.14·16→24개·속도 2.2→3.0·중력 1.6→1.2·수명 0.5→0.6 + 스폰 즉시 골드 링 1회(ShockwaveRingFX.Spawn 0.8m/0.4s).

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(exit 0)
- 정적 QA(서브에이전트): git diff 전수 스코프 일치/시그니처 일치(EnsureBareFist 1쌍·ShockwaveRingFX 4인자·DriveMode 실명)/회귀 5건 전부 PASS/OnGUI 밖 GUI.* 0건/6파일 괄호 균형 0 — FAIL 0건
- Play 판정 대기: ① 맨손 스윙에도 흰 궤적(무기 장착 시 무기 팁 트레일) ② 치유초 우클릭=HP 40% 회복+1개 소모, 독나물/황혼초=재료 안내 ③ 은신 물약/진정제 우클릭 복용(4단 로그 ①~④) ④ 바구니 스폰 골드 파티클+링 가시 ⑤ 크리타격 시 크리 버스트+임팩트 사운드(GUI 예외 소멸로 복구) ⑥ 병사 처치 드랍 정상(NRE 소멸)

## 📌 세션 종합 스냅샷 (2026-09-12 ✅ 42차 — 공격 FX 개편[무기 트레일+Travis Hit]+에셋 인벤토리+전리품 반짝임+은신 클로ak+창고 4구획+우클릭 진단)

> **스코프**: 공격 모션 예시(젤다 BOTW식) 재현 — 기존 슬래시 VFX 제거→무기 트레일(흰 궤적 잔상)+Travis Hit Impact 히트(화이트코어+샤프 스파이크, 오렌지 틴트). 스토어 에셋 등록부(docs/ASSET_INVENTORY.md) 신설. Linear Wipe(전리품 반짝임·HDRP 전용→절차 폴백)+Invisible VFX(은신 클로ak)+창고 4구획+우클릭 진단(물약 시딩 복원+4단 로그). 배치컴파일 error CS=0 + QaValidator Errors:0.

### 변경 사항 (신규 5: WeaponSwingTrail/LootSpawnFX/StealthCloakFX/ASSET_INVENTORY/TravisHit+StealthCloak 프리팹)
**공격 FX 개편(P2)**: Travis Hit_01~04 중 방사형 히트 버스트 프리팹 선정 → `Resources/FX/Impact/TravisHit` 복사 → **PlayCross(십자가)를 Travis 히트로 교체**(스윙 정중앙 발화, 틴트 (1.0,0.75,0.35)=화이트 코어+오렌지 글로우, 예시색). **스윙 쿼드(PlaySlash) 전부 제거** → 신규 `WeaponSwingTrail`(TrailRenderer: 폭 0.06→0·시간 0.18s·흰→투명·additive) — 무기 GLB 장착 시 bounds 팁(그립 반대 끝, ApplyBoundsGripAlignment out 확장)에 Attach, 콤보 진입/스테이지에서 SetEmitting(true), 홀드 종료/Idle 크로스/인터럽트에서 false. 레거시 Attack* 스윙도 트레일로 교체. BasicHit(피격) 유지.
**에셋 인벤토리(P1)**: docs/ASSET_INVENTORY.md — 스토어 에셋 9종(Travis Hit Impact/Matthew Guz/Free Slash/Vefects Invisible+Linear Wipe+Fire/Hovl Magic/GabrielAguiar 미임포트 3패키지/DoubleL 애니/Idyllic) → 적재적소 매핑 표+운용 규칙(Resources 복사 선례/HDRP 검증).
**전리품 반짝임(P3)**: Linear Wipe 프리팹은 **HDRP 전용 셰이더**로 URP 호환 불가 판정 → 절차 골드 스파이클 폴백(LootSpawnFX: 16파티클 버스트·3초 자가파괴·쿨다운) + `LootBasket.Create` 훅 1줄 — 몬스터/병사 사망·지형 드롭 전 바구니 경로 커버.
**은신 클로ak(P4)**: Invisible 팩 재질은 URP 셰이더(M_VFX_Invisible_01~05) 확정 — 참조 프리팹 부재로 StealthCloak.prefab 신규 제작(클로ak 재질+루트 파티클) → `Resources/FX/Cloak/StealthCloak` + StealthCloakFX(Attach/Detach·루프 SFX·60s 재안착) + StealthSystem 훅 2곳(ToggleStealth/ForceExitStealth — 기존 반투명 병행).
**창고 레이아웃(P6)**: 창고(E)=4구획 통일 — **좌 통합 인벤 패널(장비 2×5+인벤 그리드, 창고 컨텍스트에서도 플레이어 그리드 렌더)**/중앙 설명 400/**우측 DrawWarehousePanel(📦 창고 타이틀+카테고리 탭+창고 그리드+N/슬롯)**. 양방향 이동: 창고→인벤(기존 TransferDraggedToInventory)/인벤→창고 신규(TryDepositDraggedToWarehouse, 가득 시 롤백) + 우클릭 1개 이동 이식 + 슬롯 Rect 캐시 헬퍼.
**우클릭 진단(P7)**: Test_10 시딩에 물약 복원(은신 물약×3+진정제×3 — 37차 시딩 축소로 물약 0개였던 것이 우클릭 미체감 원인 중 하나) + **우클릭 4단 판정 로그**(수신→게이트 통과→복용 훅 진행→소모/Refresh).
**액션감 잔여**: 컴파일 에러 2건(ApplpyBoundsGripAlignment out 시그니처 CS1501) 즉시 수리 — 팁 월드 좌표 산출 추가.

### 컴파일/검증
- 배치컴파일 **error CS=0** + QaValidator Errors:0 (중간 CS1501 1회 수리)
- 정적: 12파일 균형 0, PlaySlash 호출 0건, SetEmitting/Attach/LootSpawnFX/StealthCloakFX 훅 1쌍 검증
- Play 판정 대기: ① 공격 시 흰 무기 트레일(BOTW식) ② 타격 시 Travis 히트(화이트코어+샤프 스파이크+오렌지)가 스윙 정중앙 ③ 바구니 스폰 골드 반짝임 ④ C키 은신 시 클로ak VFX+SFX ⑤ 창고 E → 좌 인벤/우 창고 양방향 이동 ⑥ 우클릭 물약 복용(은신/진정)+장비 장착 — 4단 로그로 원인 판별 ⑦ 지형 드롭 바구니 E키 회수

---

## 📌 세션 종합 스냅샷 (2026-09-12 ✅ 41차 — 무기 손 그립 정밀 장착 + 지형 드롭→바구니 시스템)

> **스코프**: ① 무기 장착이 손에 어색하게 붙던 문제(테스트 영상 5) — 전 무기 공용 고정 오프셋이 원인 → 타입별 그립 테이블+GLB bounds 자동 정렬로 수리 ② 인벤/장비칸에서 지형에 드래그 드롭 → 바구니(LootBasket) 스폰+버린 아이템 담김 → E키로 통합 전리품 컨텍스트 회수(40차 P7 자동 연동). 배치컴파일 error CS=0 + QaValidator Errors:0.

### 변경 사항 (2파일)
**`Systems/WeaponEquipManager.cs`** (179→287행) — 손 장착 정밀화:
- 근본 원인: RightHand 본 부착은 정상이나 전 무기 공용 고정 오프셋(0,0.12,0.02)+(0,0,90) — 검 실측 튜닝값이라 창/활/단도 길이·피벗·축이 달라 어색함.
- **타입별 GripPose 튜닝 테이블**(파일 상단 const): Sword/Dagger(0,0.12,0.02)+(0,0,90)·0.9m(기존 실측 유지) / Spear(0,0.45,0.02)+(-90,0,0)·1.8m / Bow(0,0.05,0.06)+(0,-90,0)·1.0m — dagger는 id 토큰 시 TargetLen만 0.45 오버라이드.
- **바운드 기반 그립 자동 정렬** ApplyBoundsGripAlignment: 자식 렌더러 bounds 합산 → 최장축=그립축 → bounds 최하단부(그립부)가 테이블 앵커에 착지하도록 pivot-to-grip 오프셋을 hand-local로 환산 차감(InverseTransformPoint — 스케일 체인 정확 반영).
- 스케일 보정: 최장축이 목표 길이의 0.4~2.2배 밖일 때만 균등 스케일(범위 내 GLB는 원본 존중 — 기존 무조건 0.9m 강제 폐지).
- 부착 로그 "[Weapon] 그립 정렬: bone/offset/bounds/스케일" — Play에서 어긋난 방향 즉시 판별. 렌더러 0개/예외 시 테이블 포즈 유지.
**`UI/InventoryWindow.cs`** (+159) — 지형 드롭 → 바구니:
- ProcessDrag 마지막 "그 외 영역(월드)" 분기 2곳(Source.Inventory/Source.Equipment)을 Cancel → **TryDropDraggedToTerrain** 전환: 커서 레이캐스트 지점(미스 시 플레이어+forward 1.5m)에 `LootBasket.Create` + AddItem(item, 1) — 버린 아이템이 바구니 안에 담김(E키 → 통합 전리품 컨텍스트 회수).
- 소모 계약: 인벤 소스=RemoveItem(id,1)+Refresh, 장비 소스=UnequipSlot(원래 슬롯) 후 즉시 RemoveItem(인벤 복귀 없이 바구니로) + Refresh. Warehouse/Loot 소스는 기존 Cancel 유지.
- 장비칸 UI 위 드롭은 기존 취소 유지(TryGetEquipSlotAtScreenPoint 판정 — 이 분기에서 Event.current.mousePosition 사용, CS0103 'p' 미선언 컴파일 에러 즉시 수리).
- 중간 컴파일 에러 1건(2692행 CS0103 'p' ×3) 1개소 수리 후 CS=0 복귀.

### 컴파일/검증
- 배치컴파일 **error CS=0** + QaValidator Errors:0 (에이전트 잔여 CS0103 1회 발견→수리→재컴파일 통과)
- 정적: 균형 2파일 0, GripPose 테이블+bounds 정렬+스케일 보정+그립 로그, Cancel 잔존은 Warehouse/Loot 경로만, LootBasket.Create+AddItem 1쌍, 소모 경로(인벤/장비) 존재
- Play 판정 대기: ① 검/창/활/단도가 손바닥에 정확히 얹힘(어긋나면 [Weapon] 그립 정렬 로그의 offset 방향으로 튜닝 테이블 상수 조정) ② 인벤에서 아이템 드래그→월드 놓기 → 바구니 스폰(안에 버린 아이템) → E키 회수 ③ 장비칸에서 드래그 아웃 → 해제 후 바구니

---

## 📌 세션 종합 스냅샷 (2026-09-12 ✅ 40차 — 테스트 영상 5 기반 P1~P7: FX통합·보라제거·인벤+장비 통합창·전리품 컨텍스트·물약 복용·액션감 1차)

> **스코프**: 사용자 3건 요구(십자가=스윙 정중앙 / 인벤+장비 통합(예시2) / 전리품 통합 컨텍스트(예시3) + 물약 복용 + 액션감 + 보라 점 제거) 전체 수행. 배치컴파일 error CS=0 + QaValidator Errors:0. QA 결함 2건(페이지 버튼 미렌더·복용 훅 미도달) 발견 즉시 수리.

### 변경 사항 (14파일 — 신규 2: HitStopManager/PotionUseSystem)
**FX**: `HumanoidClipDriver` FireComboCross pos를 스윙과 동일 앵커(Lerp(player,LastHitPoint,0.5)+up1.2)로 통일 — 십자가=스윙 정중앙(사용자 해법). `SlashVFXRunner.TintParticles` 헬퍼 — 보라 팩 파티클 제거(십자가=붉은/스윙=청백/임팩트=붉은).
**통합창(예시2)**: InventoryWindow — 좌측 상단 **장비칸 2×5(부위 배지 🪖머리/👕갑옷/🧤장갑L·R/👞신발L/R/🧣망토/🛡️방패/🗡️무기, 클릭=해제)**, 아래 **인벤 단일 그리드 5×6(탭 제거, ◀/▶ 페이지)**, 설명창 620→400, 탭은 Warehouse 컨텍스트 전용, EquipmentWindow 임베드 제거+E키 리다이렉트.
**드래그 장착(P4)**: 인벤→장비칸 드롭=장착(Armor=EquipItem+1소모/Weapon=WeaponEquipManager.Equip, 부위 불일치 차단 로그), 장비→인벤 해제 드래그(Source.Equipment 신설), **몸 장비 핫바 지정 차단**(InventoryWindow 3분기+HotbarUI 가드), 우클릭 장착 병행.
**전리품(예시3)**: ContextMode.Loot — 좌=통합 패널, 우=🧺 전리품 상자 그리드+**[전부 획득]**, 상자→인벤 원하는 것만 드래그(ItemDragContext.Source.Loot 기존 파이프라인 이식), ESC/3m 이탈/바구니 소멸 자동 닫기, 바구니 열림 리다이렉트(LootWindow 팝업 경로 비활성, 데이터 API 유지).
**물약(P5)**: PotionUseSystem.Use(item,player,out effectText) — 회복(만능=풀회복)/은신(ToggleStealth)/신속(PlayerMovement.SpeedModifier 배율+코루틴 복원)/괴력/진정 분기. 우클릭 복용(Potion/Drug 게이트 추가 — QA 결함 #2 수리)+복용 시 1개 소모+효과 텍스트 피드백. 효과 원천=docs/GAME_DATA.md 표.
**액션감(P6 1차)**: HitStopManager — 타격 순간 timeScale 0.08/45ms(쿨다운 0.15s, 실시간 폴링 복귀, 요청 시점 값 보존→킬 슬로우모션과 충돌 없음, ForceRestore 3경로) + PlayerCombat.AttackTarget 훅 + FOV 펀치(-1.5°/0.15s 복귀) + 데미지 숫자 등장 팝(1.35→1.0/0.15s).

### 컴파일/검증
- 배치컴파일 **error CS=0**(return 0) + QaValidator Errors:0 (라운드별 3회 통과)
- 정적 QA: 8그룹 diff 일치 + 11파일 균형 0 + grep 전수(TintParticles 3호출/부위 불일치 차단/Armor 핫바 4중 차단/ContextMode.Loot 전체 체인/RequestHitStop 훅/FOV 복귀 2중) PASS
- QA 발견 결함 2건 수리: ① ◀/▶ 페이지 버튼 미렌더(_gridPage 0 고정) → EndScrollView 하단 스트립 렌더+클램프 유지 ② 우클릭 복용 훅이 IsEquipmentCategory 게이트에 막힌 죽은 코드 → 게이트에 Potion/Drug 추가
- Play 판정 대기: ① 십자가가 스윙 아크 정중앙에 겹쳐 보임(타격점 1개 체감) ② 보라 점 0건(붉은/청백 FX) ③ 인벤=장비칸 2×5+인벤 5×6 통합, 드래그 장착/해제, 몸 장비 핫바 차단 ④ 바구니 E → 좌 통합인벤/우 전리품 상자, 드래그 이동+전부 획득 ⑤ 우클릭 물약 복용+효과 텍스트 ⑥ 타격 히트스톱/FOV 펀치/데미지 숫자 팝 체감 ⑦ 30슬롯+ 인벤 ◀/▶ 페이지

---

## 📌 세션 종합 스냅샷 (2026-09-12 ✅ 39차 — 테스트 영상 4 기반 5건 수리[사망블로커/스윙FX/하트·미니맵비례/인벤I키/무기애니로그])

> **스코프**: 사용자 5건 리포트(영상 4 + Editor.log 실측) 전부 뿌리 원인 확정 후 수리. ① 몬스터 불사 ② 스윙 FX 뒤방향 ③ 하트·미니맵 비대 ④ 인벤 I키 ⑤ 무기 교체. 배치컴파일 error CS=0 + QaValidator Errors:0.

### 뿌리 원인 확정 (Editor.log/프레임 실측)
- **불사**: TakeDamage FX 체인 중 `CombatVFXController.ShowDamageNumber → DamageNumberRunner.Init:338`이 OnGUI 밖에서 GUI.skin 호출 → **ArgumentException** → Die() 도달 불가(HP -53 누적·사망 로그 0건·바구니 0개 — 로그 59530행 실증)
- **인벤 I키**: 핫키가 창 GO와 동거 → CloseAnimation의 `_windowRoot.SetActive(false)`가 핫키 Update까지 정지 → I키 재오픈 불가(토글 로그 닫힘 2회/열림 0회)
- **하트·미니맵 비대**: 크기 상수 불변(하트 40px/미니맵 220px) — 게임뷰 해상도 축소로 상대적 확대 체감(핫바만 canvasScale 보정돼 있었음). 부수: 레벨업 MaxHP를 임시 하트로 오인(150HP → 총 5칸만 렌더, 8칸 위반)
- **무기 애니**: 인벤 막힘으로 장착 시도 자체가 0건([Equip] 로그 0) — 분기 로직(Spear/Bow)은 존재

### 변경 사항
**`Systems/CombatVFXController.cs`** (+88): DamageNumberRunner.Init의 GUI.* 제거(필드 저장만) + EnsureStyles static 캐시(OnGUI 내 1회) + ShowDamageNumber try-catch+1초 스팸 가드 + OnGUI NRE 3중 방어.
**`Systems/AnimalAI.cs`** (+107): TakeDamage FX 체인 전체 try-catch 격리(어그로는 try 밖 독립) — HP차감/어그로/Die() 절대 방해 금지.
**`Systems/HumanoidClipDriver.cs`**: FireComboSlash 발화 위치 — LastHitValid&&0.5s 내면 `Lerp(player, LastHitPoint, 0.5)+up1.2` **적중지점 앵커**(항상 캐릭터 앞/대상 쪽), else 정면 고정.
**`Systems/SlashVFXRunner.cs`**: `StrokeMirrorX=-1f` 스윙 전용 스트로크 플립 상수(사용자 실측 역방향 — 정방향이면 1f로).
**`UI/HUD.cs`**: _canvasScale(핫바 선례 공식) 하트/HP라벨/스텔스/게이지 전파. OnHealthChanged (max>100f)→(current>max)(레벨업 MaxHP는 빈 하트로 총칸 증가), _hpPerHeart/_heartsPerRow 방어. **총칸=ceil(MaxHP/20): 기본 5칸, 레벨업 시 빈 하트 증가(사용자 스펙 확정)** — 150HP=8칸(4풀+1반+3빈).
**`UI/Functions/MinimapUI.cs`**: _canvasScale 미니맵/마진/온도·소리 게이지/마커 전파.
**`UI/InventoryWindow.cs`**: `TogglePlayerInventory()` 신설(컨텍스트 전체 리셋 후 Show) + Show/Hide 전이 로그.
**`UI/UIInventoryHotkey.cs`**: I키 → TogglePlayerInventory 호출.
**`UI/UIWindow.cs`**: Show() 시 _windowRoot 비활성이면 즉시 SetActive(true) 안전망.
**`Systems/TestTerritoryCombatSetup.cs`**: Test_10 — UIInventoryHotkey 별도 GO 선부착 후 **Bind 2단(리플렉션)** 명시(동거 사망 구조 제거).
**`Systems/WeaponEquipManager.cs` + `PlayerCombat.cs`**: 무기 장착 시 `[Equip] 무기 애니 경로 결정: type → WeaponCombo/Attack(찌르기)/ArcheryShot` + 무기 설정 로그(판정 가시화).

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(return 0) + QaValidator Errors:0
- 정적 QA(서브에이전트): 8항목 diff 일치 PASS + 12파일 균형 0 + SyncPlayerCombat 3파라 시그니처 호출부 갱신 확인 + 회귀 리스크 5건(a~e) 전부 PASS(HP차감/어그로/Die() try 밖, 하트 스펙 준수, 안전망 idempotent, 이중토글 선착순 3중 방어, 스크립트 외 무수정)
- Play 판정 대기: ① 슬라임 사망+🧺 바구니+경험치 ② 스윙 FX가 적중지점 앵커로 대상 쪽에서 앞방향 아크(역방향이면 StrokeMirrorX=1f) ③ 하트 5칸(100HP)/레벨업 시 빈 하트 증가 ④ I키 3-연쇄 로그(부착+Bind → 토글 열림 → 열림 컨텍스트=None) ⑤ 무기 장착 시 애니 경로 로그+애니 변화

---

## 📌 세션 종합 스냅샷 (2026-09-12 ✅ 38차 — 스윙 FX 뒤방향 근본 수리[루트 기준+카메라 빌보드] + 병사창 크기 유지 확정)

> **스코프**: 사용자가 "공격 슬래시 임팩트가 뒤로 향한다"를 2회 보고 → 37차 전방 반구 클램프(|yaw|≤90°)로도 해소 안 된 나머지 자유도 2건을 근본 수리. ① 기준 트랜스폼을 `_anim.transform`(비주얼 FBX 자식, yaw 오프셋 가능)→드라이버 루트로 통일 ② 슬래시/크로스 쿼드를 `LookRotation(dir)`→카메라 수평 빌보드(+Z 정면 관례 — 카메라가 아크 앞면에서 봄)로 오리엔테이션 ③ 스테이지 개성은 roll로 이관(1타 수평/2타 수직/3타 -45° 사선). **UI 검증: 최근 5커밋 diff 전수 확인 결과 병사 상호작용 창(GuardPlaceholder 320×250→520×380)만 크기 변경, 타 창 크기 변경 0건 — 되돌릴 항목 없음(요구대로 병사창만 확대 유지).** 배치컴파일 error CS=0 + QaValidator Errors:0.

### 변경 사항
**`Systems/HumanoidClipDriver.cs`** (+31/−13) — 스윙 FX 기준 루트화:
- FireComboSlash(701행)/FireComboCross(796행)/레거시 Attack* 스윙(590행): `var t = _anim.transform` → `var t = transform`(드라이버=루트, 논리 정면 — 비주얼 자식 yaw 오프셋 면역). pos 규격(fwd 0.9 + up 1.2) 유지
- roll(707행): 1타 0(수평) / 2타 -90(수직, 유지) / **3타 -45(사선 신규 — 기존 dir pitch 28.7°를 빌보드 전환 후 roll로 이관, 부호는 튜닝 상수)**
- ClampForwardHemisphere 경로 유지(ComboStageDirection 745행/FireComboCross 800행 — 방어막). `_anim.transform` 잔존은 진단 로그/화살/투척 등 FX 무관 용도만

**`Systems/SlashVFXRunner.cs`** (+53) — 카메라 수평 빌보드:
- 공용 헬퍼 `CameraHorizontalFaceDir(position, dir)`(156행): `faceDir = cam.transform.position - position`(y=0) — **쿼드 +Z를 카메라 쪽으로** → 카메라가 슬래시 팩 제작 관례의 +Z 정면에서 아크를 봄(뒷면 미러링=역스윙 체감 원천 차단)
- 3단 폴백: Camera.main null → `-dir`(y=0) → 수평 소실 → `Vector3.forward`. 롤은 빌보드 이후 `Rotate(0,0,roll, Space.Self)` — 화면축 기준 기울임(수직/사선 유지)
- PlaySlash 2-파라 오버로드 시그니처 불변, PlayImpact(Quaternion.identity)/쿨다운(MIN_SPAWN_INTERVAL)/static 캐시/ScheduleDestroy 패턴 무변경. 위치 인자 원본 그대로(회전만 변경)

### 원인 정리(사용자 2회 동일 리포트의 뿌리)
- 1차 원인(37차 해결): 3타 실측 yaw 143.6° 후방 성분 → 전방 미러 클램프로 방향 벡터는 전방 반구 보장됐으나
- 2차 원인(38차 해결): ① 쿼드가 `LookRotation(dir)`로 +Z=원거리 → 후방 카메라가 **뒷면에서 봄** → 스트로크 좌우 미러링이 역스윙처럼 읽힘 ② 기준 트랜스폼이 비주얼 자식이라 논리 정면과 어긋날 여지
- Play 판정: 3타뿐 아니라 전 스테이지 스윙/크로스가 캐릭터 앞쪽에서 정면으로 읽히는지 확인

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(return 0) + QaValidator Errors:0
- 정적 QA(서브에이전트): diff↔서술 일치/균형 2파일 0/스윙 경로 `_anim.transform` 잔존 0건/빌보드 후 Space.Self 롤 순서/2파라 오버로드·회귀 앵커(쿨다운·캐시·파괴·PlayImpact) 전부 PASS
- UI 검증: `git diff bc32960c~5..bc32960c` UI 크기 상수 전수 — 병사창 외 변경 0건(인벤 힌트 삭제 1건은 텍스트 블록)

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 37차 — 스윙 전방 클램프+핫바 장착 시스템+시딩 정리+병사 UI 개편)

> **스코프**: ① 3타 실측 yaw 143.6° 후방 성분을 전방 반구로 미러 클램프(뒤방향 발화 원천 차단) ② 숫자키 퀵슬롯 장착/사용+장비·창고→핫바 드래그 ③ 창고 시딩 wood 장비 축소(슬롯 ~137→약 26) ④ 몬스터 스폰 진단 로그 ⑤ 핫바 도움말 제거 ⑥ 병사 상호작용 UI 1.6배 개편. 배치컴파일 error CS=0 + QaValidator Errors:0.

### 변경 사항
**`Systems/HumanoidClipDriver.cs`** — ComboStageDirection 슬래시 전방 클램프:
- 3타 실측 yaw 143.6°의 후방 성분을 전방 반구로 미러 클램프(z 부호 반전=수평 yaw θ→180°−θ, 측면/수직 성분 유지) → |yaw|≤90° 보장, 슬래시/십자가 모두 뒤방향 발화 금지
- 발화 시 "[Combo] 후방 스윙 → 전방 미러 클램프 (stage, yaw 전/후)" 로그

**`Systems/AnimalAI.cs`** — 사망 미판정 진단 로그:
- Start() HP 확정 지점에 "[AnimalAI] 스폰 {id} MaxHP={X} (스케일게이트={bool}, 레벨={L})" 1회(_spawnLogged 가드), 기존 데미지/사망 로그 유지 → 1회 Play로 사망 미판정 원인(스폰 자체 실패 vs HP 과대) 판별

**`Systems/TestTerritoryCombatSetup.cs` + `Systems/TestAllInOneSetup.cs`** — SeedAllItemsToWarehouse 축소:
- 유지: wood 장비 13종(AllTieredGear id "wood" 필터 ×2) + 무기 재료(BoarTusk/WolfTooth ×5) + Gold
- 제거 34종(steel/stone/crystal 장비 + 허브/씨앗/고기/어류/물약/도구 등) → 슬롯 ~137→약 26

**`UI/InventoryWindow.cs`** — 핫바 도움말 제거+드롭 연계:
- "좌클릭: 선택 / 드래그..." 안내 블록 삭제(+138줄: ProcessDrag 핫바 드롭 연계)

**`UI/HotbarUI.cs`** (+148줄) — 퀵슬롯 장착 시스템:
- 숫자키 1~8 HandleNumberKeys: 인벤 소유 확인 + 카테고리 분기 장착/사용
- `UI/EquipmentWindow.cs`: 슬롯 MouseDown → ItemDragContext.Begin(Source.Inventory) 드래그 → 인벤/핫바 드롭
- 우클릭 장착 [Equip] 결정 로그 + 무기 id 토큰 파싱 수리(2062/2069행)

**`Systems/GuardPlaceholder.cs`** — 병사 상호작용 UI 1.6배 개편:
- 패널 320×250→520×380, 다크네이비 플랫 팔레트(12상수)+회백 테두리+스카이블루 타이틀 라인, static 스타일 캐시(OnGUI new GUIStyle 방지)
- 좌측 아바타 96px: GuardIconRenderer 리플렉션 TryGetGuardIcon(Systems↔UI asmdef 경계, 0.5초 폴링) — null 시 국적색 원형+이니셜 폴백
- 체력바 확대(70%폭) + 호감도/중독도 바 + [E] 배지 + 메뉴 버튼 5종 플랫화

### 컴파일/검증
- 중괄호/괄호 균형 8파일 0(주석 내 비대칭 괄호 제외 코드 기준)
- Unity 6000.4.10f1 batchmode **error CS=0**(return 0) + QaValidator Errors:0
- 정적 QA: 미러 클램프/스폰 로그/wood 필터/힌트 0건/HandleNumberKeys/ItemDragContext.Begin/TryGetGuardIcon/520×380 전수 확인

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 33차 — 자기 소속 병사 공격 명령 부트 활성화)

> **스코프**: "자기 소속 병사에게 공격 명령" 기능(RTSCommandSystem + GuardSelectionManager)은 이미 코드로 완전히 구현되어 있었으나, **어디서도 생성되지 않아(죽은 코드)** 실제 게임에서 동작하지 않던 상태 → 부트 생성 배선을 추가해 활성화. 컴파일 0 에러.

### 진단
- `RTSCommandSystem.cs`(우클릭 공격/이동, Ctrl 일제, H 중단) + `GuardSelectionManager.cs`(좌클릭 드래그 선택 → 우클릭 명령 배선)는 완전 구현. 선택은 `IsRecruited==true`(모집=자기 소속) 병사만.
- **단, 두 컴포넌트를 AddComponent로 생성하는 지점이 전무** → 게임에서 동작 안 함.

### 변경 사항
**`Systems/CoreSystemsBootstrap.cs`** (107→166줄) — 부트 생성 배선:
- Awake() L31~35에 `EnsureRTSCommandSystem()` / `EnsureGuardSelectionManager()` 호출 추가(TerritoryBuilder 이후)
- 두 Ensure 메서드(L101/128): 기존 EnsureTerritoryManager 패턴 + 중복 가드(FindObjectsInactive.Include) + try/catch 격리(부트 try-catch 필수 메모리 준수), 한국어 로그 1회

### 동작/분리
- **좌클릭 단순 클릭(10px 미만)** = 기존 PlayerCombat 플레이어 공격 유지
- **좌클릭 드래그(10px 이상)** = 병사 선택(GuardSelectionManager) — 자연 분리
- **우클릭** = 선택된 자기 병사에게 적 공격/이동, **Ctrl+우클릭** = 일제 공격, **H** = 중단

### 컴파일/검증
- 중괄호/괄호/대괄호 균형 0(21/54/4)
- Unity 6000.4.10f1 batchmode **0 에러**(return 0)

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 32차 — 자기 영지 침대 세이브 + 죽으면 영지/침대 스폰)

> **스코프**: 자기 소유 성에 침대를 배치하고 그 침대에서 세이브(스폰핀 설정) → 사망 시 세이브한 침대(우선) 또는 근처 자기 영지(기존 로직)에서 부활. 컴파일 0 에러.

### 변경 사항
**`Systems/Bed.cs`** — 정적 스폰핀 API 신설:
- `static Vector3? s_customSpawnPoint`, `SetSpawnPoint(Vector3)`, `ClearSpawnPoint()`

**`Systems/IndoorFurniturePlacer.cs`** — `CreateBed`가 root 프리팹에 `Bed`(+BoxCollider isTrigger) 부착(`GetComponent<Bed>()==null`일 때만, 중복 방지) → 모든 CreateBed 침대(집/성) E키 상호작용 가능

**`Systems/PlayerCastleInteriorBuilder.cs`** — 자기 성에 침대 배치:
- `CreateBed(1.2, 2.0, bedMat)` "LordBed" 배치(앞벽 x=mx×-3, z=-roomDepth/2+1.2), 이름표 "🛏️ 성주의 침대 (세이브)".

**`Systems/SleepUI.cs`** — "💾 여기서 세이브" 버튼 추가(수면과 독립):
- `SaveAtBed()` → `Bed.SetSpawnPoint(침대위치)` + `SaveManager.AutoSave()`(빈 슬롯 우선) → "💾 세이브 완료!" 연두 피드백 2.5초. 창 높이 자동 확장, _feedbackStyle 1회 캐시(OnGUI new GUIStyle 0건)

**`Core/PlayerHealth.cs`** — Respawn 스폰핀 우선:
- ① 세이브한 침대 있으면 침대 위치+up0.8m 부활(리플렉션 TryGetBedSpawnPoint — Core→Systems 순환참조 회피) ② 없으면 기존 `_respawnAtNearestTerritory`+`GuardManager.FindNearestPlayerTerritory`(가까운 자기 영지) ③ 둘 다 없으면 기본 위치

### 스폰핀 우선순위
**세이브한 침대 > 가까운 자기 영지 > 기본 위치**

### 컴파일/검증
- 중괄호/괄호/대괄호 균형 5파일 0
- Unity 6000.4.10f1 batchmode **0 에러**(return 0)
- ⚠️ 스폰핀은 세션 static(씬 재시작/로드 시 초기화) — 영속화는 SaveData 확장 과제

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 31차 — 전리품 창 우측 배치 + 드래그로 인벤토리 이동)

> **스코프**: 전리품(모브 드랍 바구니) 창을 화면 우측 고정 구획으로 배치하고, 전리품 아이템을 **드래그해서 인벤토리로 옮기는** DnD 시스템 구현. 컴파일 0 에러.

### 변경 사항
**`UI/ItemDragContext.cs`** — Source enum에 `Loot` 추가(`{None, Inventory, Warehouse, Loot}`, 맨 뒤 추가로 하위 호환). Loot 소스는 InventoryWindow.ProcessDrag가 MouseUp 판정 대행.

**`UI/LootWindow.cs`** (505→624줄) — 우측 고정 배치 + 드래그 시작:
- `WINDOW_WIDTH`→InventoryWindow.WINDOW_WIDTH, `WINDOW_HEIGHT`→Screen.height-180 참조. 위치 = `InventoryWindow.GetContextX()`(2S/3+6), y=10
- 슬롯 MouseDown 즉시 TakeItem → **`ItemDragContext.Begin(Source.Loot, i, entry.Item)` 드래그 시작**. 인벤 닫힘 시에만 현재 MouseUp 폴백(클릭=획득 유지) + 잔여 전리품 NRE 하드닝
- 신규 정적 API: `TryGetSlotAtScreenPoint(guiPoint, out slotIndex)`(슬롯 Rect 캐시 y 보정 — GUIToScreenPoint yMin=sp.y-height 선례), `TryTakeDraggedToInventory()`(TakeItem→RefreshLoot), `Awake()` 싱글턴

**`UI/InventoryWindow.cs`** — ProcessDrag에 **Loot 소스 분기 신설**(기존 Warehouse/인벤 흐름 무수정):
- WINDOW_WIDTH/HEIGHT private→public(LootWindow 참조용)
- MouseDrag Use + MouseUp: ① 인벤 그리드/슬롯 위 → `LootWindow.TryTakeDraggedToInventory()`(TakeItem→RefreshInventory+RefreshLoot) ② 전리품 슬롯 위 → 클릭 획득 유지 ③ 그 외 Cancel

### Loot 드래그→인벤 흐름
E키→OpenForBasket→우측 창 Show → 슬롯 좌클릭(MouseDown)→드래그 시작+고스트(ItemDragContext.DrawGhost) → 인벤 그리드 위 MouseUp=TakeItem(인벤 AddItem)+Refresh. 인벤 닫힘=클릭 획득 폴백(상호 배타 가드).

### 컴파일/검증
- 중괄호 균형 OK(Inventory 273/273, ItemDragContext 8/8, LootWindow 61/61)
- Unity 6000.4.10f1 batchmode **0 에러**(return 0)

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 30차 — 몬스터 전리품 종류별 분리 + 병사 희귀 고급 아이템)

> **스코프**: ① 몬스터 전리품을 티어 공용 DropTable(종류 무관 동일)에서 **개체별(토끼/멧돼지/늑대)로 완전 분리** ② 병사 드랍 테이블에 **희귀 고급 아이템(isRare) 6종 추가**. 컴파일 0 에러.

### 변경 사항
**`Systems/AnimalAI.cs`** — Die() 드랍 블록(단일 경로화):
- `DropTableManager.GetMonsterTable(_tier)`(티어 공용) 경로 **제거** → 개체별 드랍이 **항상 1차** 적용(토끼=토끼고기/토끼털, 멧돼지=돼지고기/가죽/엄니 20%, 늑대=늑대고기/이빨/모피 30%, 기타=“{이름} 고기”(meat_id)+“{이름} 재료”(mat_id))
- 레벨 희귀 보정(levelDropBonus)과 빈 바구니 최소보장(Gold) 유지
- DropTableManager/다른 시스템은 미수정(드라큘라/스켈레톤/병사 영향 없음)

**`Resources/DropTables/SoldierDropTable.asset`** — 희귀 고급 아이템 6종 추가(전부 `isRare: 1`, 저확률):
- `weapon_sword_steel` 강철검 5% / `weapon_spear_stone` 돌창 3% / `steel_armor` 강철 갑옷 4% / `steel_helmet` 강철 투구 3% / `mat_wolf_fur` 늑대 모피 3% / `mat_wolf_tooth` 늑대 이빨 2%
- id는 기존 장비 체계(WeaponEquipManager._itemIdToGlb, PlayerInventory 장비 상수, EquipmentStatBonus)에서 **재사용** → 우클릭 장착·아이콘·스탯 동작
- 기존 4항목(gold/목검/나무창/가죽갑옷) 무손상, YAML 들여쓰기·유니코드 이스케이프 검증 통과

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **0 에러**(return 0)
- SoldierDropTable `isRare: 1` 6건 확인, AnimalAI 중괄호/소괄호/대괄호 균형 OK

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 29차 — 영지 배치 결정론적 랜덤 흔들림)

> **스코프**: 영지 배치가 완전 원형이던 것을, 구조(국가 4섹터 각도·링 반경 순서·인덱스 배치)는 유지하면서 결정론적 난수로 각 영지를 자연스럽게 분산. 재시작/재생성 시에도 항상 같은 좌표(시드 기반).

### 변경 사항
**`Core/Data/TerritoryDatabase.cs`** — `GenerateAllDefinitions()`(L202~213) worldPos 계산부만:
- jitter 시드: `System.Random(GetDeterministicHash("{nation}_{ring}_{index}_jitter"))` → NextDouble 2회
- **반경 배율 0.85~1.10**(링1 최대 1450×1.10=1595m < 세계 extent 1600m, 링 간 겹침 없음)
- **각도 델타 -8°~+8°**(18° 슬라이스 안에 머묾, 인접 영지 최소 2.8° 간격 — 옆 섹터 침범 차단)
- 결정론성: `GetDeterministicHash`(문자열 산술 해시, 프로세스 무관 동일) + `System.Random(고정시드)` → 80영지 2회 생성 비트 단위 동일(csc.py로 실증)
- 황제국(중심)·드라큘라(1350m@150°)는 흔들지 않음(루프 밖)
- 월드맵(WorldMapWindow L303/684)·미니맵(MinimapUI)은 worldPosition 자동 읽기 → 수정 불필요

### 컴파일/검증
- diff로 worldPos 계산부 2곳만 변경 확인
- Unity 6000.4.10f1 batchmode 컴파일 **0 에러**(return 0), 괄호 균형 OK

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 28차 — M키 양피지 월드맵 신규 구현)

> **스코프**: M키 토글 월드맵(WorldMapWindow) 신규 — 밝은 양피지지 절차 배경 + 전국/링 영지 82곳 방사형 배치(국가색·난이도·소유상태 마커) + 플레이어 위치 + 휠 줌/드래그 팬 + 마커 툴팁. 좌표 함정(영지 확장 1450m vs 지형 스플랫 반폭 1000m)으로 지형 텍스처 대신 순수 양피지+정규화 좌표계 채택.

### 변경 사항
**`UI/WorldMapWindow.cs`(신규, 1067줄)** — `ProjectName.UI`, `UIWindow` 파생:
- **M키 자가등록**: Awake에서 `UIWorldMapHotkey`(파일 하단 클래스)를 AddComponent+Bind(중복 가드 First-Come). `EnsureVisibilityRoot()`로 `_windowRoot`를 자식 패널로 지정 → `UIWindow.Hide()`(CloseAnimation)가 root가 아닌 `_windowRoot`만 비활성화 → 핫키/OnGUI 계속 생존(InventoryWindow 선례). 시작은 닫힌 상태
- **양피지 배경**(절차 1회 static 캐시 `BuildParchment`): 밝은 크림·세피아 베이스 + 그레인 노이즈 + 가장자리 어두움/불규칙 + 테두리 짙은 갈색. 같은 Texture2D를 지도 본체+창 프레임/장식에 재사용(밝게/세피아 틴트)
- **영지 좌표 변환**: `u=0.5+worldPos.x/3200f, v=0.5+worldPos.z/3200f`(extent 1600m → Empire(0,0) 중앙, 링1/드라큘라(1450m) 가장자리)
- **영지 마커**: `TerritoryDatabase.Instance.GetAllDefinitions()`(82곳) 순회 — 국가색(NationColor: East파랑/West초록/South빨강/North보라/Empire금/Dracula검강) 점 + territoryName 라벨 + 난이도별 크기 + 소유상태(PlayerOwned 강조/Contested 빨강 펄스/미점령·영주소유 구분) + Empire·드라큘라 특별 표식
- **플레이어 마커**: `GameObject.FindWithTag("Player")` position 정규화, 강조 화살표
- **줌/팬/툴팁**: `Event.current.type==ScrollWheel` 휠 줌, 좌클릭 드래그 팬, 마커 호버 시 영지명/국가/난이도/병사 수/설명 툴팁 박스
- IMGUI 온건: OnGUI new GUIStyle/Rect 0건(static 캐시), GUI.color 복원

### 컴파일/검증
- 중괄호·소괄호·대괄호 균형 OK(106/438/17)
- Unity 6000.4.10f1 batchmode 컴파일 **0 에러**(return 0)

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 27차 — 장비창 우측 배치/3구획 UI+설명창 아이콘+우클릭 장착+무기별 애니+활/화살 시스템+폰트 통일)

> **스코프**: ① 장비창을 화면 우측 구획으로 재배치(인벤=좌, 설명=중앙, 장비=우측 3구획, 상점/창고 컨텍스트 우선순위로 충돌 방지) ② 중앙 설명창 아이콘을 `ItemIconDatabase.GetOrCreateIcon` 재사용으로 항상 렌더(+자리표시) ③ 우클릭 장착 경로 보완(무기id 매핑 폴백 + 장착 후 RefreshInventory) ④ 무기타입별 공격 애니 분기(Fist/Sword=WeaponCombo, Spear=찌르기, Bow=ArcheryShot) ⑤ 활/화살 시스템(좌클릭 발사→ArrowManager 화살 소모, 3종 화살, 우클릭 직발사 제거→소모 루 통일) ⑥ Noto Sans KR + 맑은고딕 한글 폰트 도입 + fontSize 5단계 타입스케일 통일. Unity batchmode 컴파일 0에러.

### 변경 사항
**`UI/InventoryWindow.cs`** — 레이아웃 재배치 + 아이콘 + 우클릭 장착:
- OnGUI 하단 `DrawEquipRow` 호출 제거(장비슬롯 그리드에서 분리), 그리드 높이 `EQUIP_ROW_HEIGHT` 차감 제거. 오른쪽 구획(`Screen.width*2/3+6, y, PanelWidth, WINDOW_HEIGHT`)에 `EquipmentWindow.TryRenderEmbedded(...)` 호출 — `ContextMode.None`일 때만(상점/창고 컨텍스트 우선순위)
- DrawDescriptionPanel 아이콘: `ItemIconDatabase.GetOrCreateIcon(item)` → `item.icon.texture` 폴백 → 반투명 자리표시(α0.15), 128px ScaleToFit. 미선택 시 안내 문구+자리표시
- TryEquipItem: `_weaponIdMap` 미스 시 id 토큰 파싱 폴백(`weapon_sword_steel`→WeaponType/equipId 유추), 방어구 장착 성공 시 RefreshInventory, 성공/실패 로그 명확화

**`UI/EquipmentWindow.cs`** — 우측 임베디드 렌더:
- `static Toggle()`(E키 호환), `TryRenderEmbedded(x,y,w,h)` 정적 진입점(씬에 인스턴스 없으면 자동 생성 1회), 기존 OnGUI AAA 4레이어 렌더를 `RenderWindow(..., embedded)`로 이관(embed=닫기버튼 생략/배너 클램프). 장비슬롯 실시간 `GetSlotData` 조회로 장착 즉시 반영

**`Systems/PlayerCombat.cs`** — 무기별 공격 분기:
- TryAttack 시작부(L191) `_currentWeapon.weaponType == ProjectName.Core.WeaponType.Bow`(CS0104 방지 정규화) → `TryBowShot()` 후 return(근접 경로 차단). `TryBowShot()`(L246~288): 커서 Ray 방향 → `ArrowManager.TryShootArrow(origin, dir, Bow.damage)` → 성공 시 `_clipDriver.TriggerBowShot()`(ArcheryShot 명시 트리거) + 카메라/런지. 실패 시 LastHitValid=false. `_clipDriver`는 `GetComponentInChildren<HumanoidClipDriver>()` 획득

**`Systems/HumanoidClipDriver.cs`** — 드라이버 무기 분기:
- 공용 API `TriggerSpearAttack()`/`TriggerBowShot()`(L62~85). ArcheryShot을 공격상태홀드(Speed 0 고정)에 추가(L442). 무기타입 감시(L456): Bow→ArcheryShot, Spear→Attack(찌르기), Fist/Sword→기존 WeaponCombo B안 유지. 활 우클릭(L622~628)을 직접 `ArrowProjectile.Spawn` → `ArrowManager.TryShootArrow`(화살 소모 후 발사, 소모 성공 시에만 애니)로 통일

**`Systems/ArrowManager.cs`** — `TryShootArrow(origin, direction, baseDamage)` 3-파라 오버로드 신설, 기존 2-파라가 위임(호환성 유지), origin 우선 발사

**`UI/UIFont.cs`(신규)** — 폰트 타입스케일 상수(Display60/Title38/Body24/Caption17/Badge13) + static 캐시 폰트 로더(`Resources.Load<Font>("Fonts/NotoSansKR-VF")`→"Fonts/malgun"→빌트인 폴백)

**`Assets/Resources/Fonts/`(신규)** — `NotoSansKR-VF.ttf`(10.4MB) + `malgun.ttf`(13.4MB), .meta 자동 생성 확인

**폰트 적용 11파일**: UIStyleManager(글로벌 스킨 폰트), HUD, InventoryWindow, EquipmentWindow, StatusWindowUI, HotbarUI, MonsterHeadUI(Systems 로컬 로드), RecipeWindow, OptionsUI, AlchemyUI, LoadGameUI — 전원 `UIFont.Load()`/`Resources.Load`로 교체, fontSize 파편화(10~96px)를 5단계로 수렴. (HUD 동적 버프폰트는 아이콘 비율 연산이라 제외, LockpickingUI는 스킨 폰트 사용)

### 컴파일/검증
- Unity 6000.4.10f1 batchmode 최종 컴파일 **0 에러**(`error CS` 0건, return 0)
- 중간 이슈 1건 해결: `PlayerCombat.cs` WeaponType 모호성(Neural vs Core enum, CS0104) → `ProjectName.Core.WeaponType` 정규화

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 26차 — 피격 이펙트 이벤트 전환+공격 범위 표시기)

> **스코프**: 플레이어 피격 이펙트를 폴링 엣지에서 정적 이벤트 기반으로 전환(PlayerHealth.OnPlayerDamaged → CombatFXGate.PlayHitFX 풀체인), 공격 범위 표시기 신규(무기별 사거리 지면 원형 링+전방 화살), 부트 2곳 연결.

### 변경 사항
**`Core/PlayerHealth.cs`** — 피격 확정 정적 이벤트:
- `public static event System.Action<Vector3, float> OnPlayerDamaged`(64행) 신규
- TakeDamage HP 감소 확정 시점 발화(160행) — try-catch로 구독자 예외 흡수(164행 경고, 전투 방해 금지)

**`Systems/HumanoidClipDriver.cs`** — 이벤트 구독 전환:
- Player 모드만 구독(161-162행, Start) — 다중 드라이버(Soldier 등) 이중 발화 차단, OnDestroy 해제(64행)
- `OnPlayerDamagedFX` 핸들러(68행) → `CombatFXGate.PlayHitFX(playerGO...)` GameObject 오버로드 우선, Instance 부재 시 위치 기반 폴백 — 몬스터 피격과 동일 경로(히트플래시+스파크+BasicHit+출혈+데미지넘버+카메라 히트)
- 구 폴링 엣지 PlayImpact 블록 제거(이중 발화 방지, 376행 주석), HitLight 트리거는 폴링 유지

**`Systems/WeaponRangeIndicator.cs`** — 신규(233행):
- LineRenderer 64분할 지면 원형 링(스카이블루 반투명, Sprites/Default 절차 머티리얼 static 캐시)
- 반경 = WeaponData 정적 스탯 단일 소스(RangeOf, 184-193행): Fist 2m / Sword 2.5m / Spear 4m / Bow 10m — 등급 배율은 dmg에만 반영되므로 무시
- 활=전방 방향 선(사거리 끝까지)+V자 화살촉, 근접=링 앞쪽 닫힌 마름모 화살촉
- 표시 조건: 무기 장착 중(CurrentType != Fist)만 표시, 맨손 숨김(ApplyVisibility), CurrentType 엣지 반경 갱신(RefreshIfNeeded)
- CharacterController 있으면 발 위치 보정(캡슐 중심 계약), 부트 try-catch 실패 시 비활성화
- `EnsureOn(Transform)` 정적 헬퍼(55행) — 중복 부착 방지(GetComponentInChildren 검사)

**`Systems/TestAllInOneSetup.cs` / `TestTerritoryCombatSetup.cs`** — 부트 연결:
- SetupPlayer 말미(388행) / 플레이어 생성 직후(153행) 각 `WeaponRangeIndicator.EnsureOn(player.transform)` 1줄

### 검증
- 배치컴파일: **`error CS=0`** + QaValidator Errors:0 (return code 0)
- 정적 QA(서브에이전트): 변경 5파일 brace 균형 전부 통과 — WeaponRangeIndicator 25/25, HumanoidClipDriver 203/203, PlayerHealth 57/57, TestAllInOneSetup 176/176, TestTerritoryCombatSetup 135/135
- grep 검증: `OnPlayerDamaged` 정의(PlayerHealth 64행)+발화(160행, try-catch)+구독(HumanoidClipDriver 162행, Player 모드 한정)+해제(64행 OnDestroy), `PlayHitFX` 플레이어 경로(HumanoidClipDriver 76/78행, GameObject 오버로드 우선+위치 폴백), 구 폴링 PlayImpact 제거 확인(잔여 0건 — 주석만), `EnsureOn` 정의(WeaponRangeIndicator 55행)+부트 2호출(388/153행), 반경 테이블 WeaponData 정적 스탯 단일 소스(Fist 2/Sword 2.5/Spear 4/Bow 10, 27-30행) — 하드코딩 없음

### Play 판정 대기
⬜ 플레이어 피격 — 히트플래시+스파크+출혈+데미지넘버+카메라 히트 정상 발화, 이중 발화 없음
⬜ 범위 표시기 — 링 반경 무기별 정합(Fist 숨김/ Sword 2.5/Spear 4/Bow 10), 전방 화살 방향 추적
⬜ 무기 교체 시 링 반경 즉시 갱신+맨손 전환 시 숨김

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 25차 — 등급별 무기/장비 47종 ItemData+아이콘 매핑+장착 스탯+시딩)

> **스코프**: 4티어 GLB 장비 전종 ItemData 47종(무기 16+방어구 25+부속 6) 신규 정의, 무기 등급 dmg 배율(GetTierMultiplier+CreateTieredCopy 복제본 주입), 방어구 등급 def 테이블, GLB 아이콘 매핑 44종 확장, 창고 전종×2 시딩+슬롯 64→160.

### 변경 사항
**`Core/PlayerInventory.cs`** — 등급별 장비 47종 ItemData:
- 무기 16종 `weapon_{type}_{tier}`(sword/spear/bow/dagger × wood/steel/stone/crystal) — wood sword/spear/bow 3종은 기존 정의 재사용, 나머지 13종 신규
- 방어구 25종 — id=GLB 파일명 그대로(`{tier}_{slot}`): armor×4, helmet×4, boot 좌우×8, glove 좌우×8, wood_shield
- 부속 6종 — gas_mask/chemical_pack ×wood/steel/stone(crystal GLB 부재로 미정의)
- rarity/내구도: wood=Common/20, steel=Uncommon/40, stone=Rare/60, crystal=Epic/80
- `AllTieredGear` 정적 배열(395행) — 47종 전체, 창고 시딩/테스트 순회용

**`Core/WeaponData.cs`** — 무기 등급 스탯:
- `GetTierMultiplier(weaponId)`(35행) — wood 1.0 / steel 1.8 / stone 2.5 / crystal 3.75(Sword dmg 12 기준 12→22→30→45)
- `CreateTieredCopy(tierMultiplier)`(46행) — damage만 Round(base×배율), 공속/사거리/타입 유지 — 정적 WeaponData 오염 방지용 복제본
- `WeaponType` enum에 Dagger 없음({Fist, Sword, Spear, Bow}) → dagger는 Sword 타입 장착(Equip 2인자 오버로드 위임, InventoryWindow 1067행)

**`Systems/WeaponEquipManager.cs`** — 복제본 주입+GLB 매핑:
- `_itemIdToGlb` 16종 full-id→GLB 매핑(23-41행) — dagger 등 suffix 분기 없는 무기 지원, 기존 짧은 id(steel 등)는 suffix 분기 폴백(하위 호환)
- 장착 시 `SyncPlayerCombat(type, GetTierMultiplier(id))`(89행) + `PlayerCombat.SetWeapon(CreateTieredCopy)`(172행) — 복제본 주입

**`Systems/EquipmentStatBonus.cs`** — 방어구 등급 def 테이블(98-130행):
- helmet 5/9/14/20, armor 8/14/22/32, boots 좌우동일 4/7/11/16(+speed 0.2 유지), gloves 좌우동일 3/5/8/12, wood_shield 6, gas_mask/chemical_pack 7/12/18/26

**`UI/GblItemIconRenderer.cs`** — 아이콘 매핑 44종 확장:
- 명시 맵 `_itemToModel` — 무기 16종+방어구 20종+부속 8종 확장, 신규 ItemData 전종 자동 아이콘화
- `armor_wood` 등 `{slot}_{tier}` 키는 dead-entry(실제 id=`{tier}_{slot}`) 허용 — 실제 id(wood_armor 등)는 관례 후보 1(id 그대로 GLB 매칭, 278행)로 베이크
- GLB 부재 매핑(shield_steel 등)도 선준비 — 로드 실패 시 아이콘 스킵

**`Systems/TestAllInOneSetup.cs` / `TestTerritoryCombatSetup.cs`** — 창고 시딩:
- `foreach AllTieredGear Add(gear, 2)` 전종×2 시딩(1106/996행) + 창고 슬롯 64→160(1042행, Test_10/Test_09)

### 검증
- 배치컴파일: **`error CS=0`** + QaValidator 전체 통과(Errors:0) — 에디터 잠금으로 1회 실패 후 사용자 Play 완료 → 재실행 통과
- 정적 QA(서브에이전트): 변경 7파일 brace 균형 전부 통과 — PlayerInventory 81/81, WeaponData 12/12, EquipmentStatBonus 107/107, WeaponEquipManager 44/44, GblItemIconRenderer 98/98, TestAllInOneSetup 176/176, TestTerritoryCombatSetup 135/135
- grep 검증: `AllTieredGear` 정의(PlayerInventory 395행)+시딩 순회 2곳(1106/996행, ×2), `GetTierMultiplier` 정의(WeaponData 35행)+호출(WeaponEquipManager 89행), `CreateTieredCopy` 정의(46행)+복제본 주입(172행), `_itemIdToGlb` 16종(23-41행)+미매칭 suffix 폴백(106행), EquipmentStatBonus 신규 def 전티어 값 일치(helmet 5/9/14/20·armor 8/14/22/32·boots 4/7/11/16·gloves 3/5/8/12·shield 6·gas_mask/chemical_pack 7/12/18/26), GblItemIconRenderer 맵 44종+관례 1 매칭(278행), dagger=Sword 타입 장착(InventoryWindow 1067행), 창고 슬롯 160

### Play 판정 대기
⬜ 등급 무기 장착 — GLB 모델 부착+클립 전환+dmg 배율 정합(Sword wood 12→steel 22→stone 30→crystal 45)
⬜ 등급 방어구 장착 — def 스탯 표 정합 반영(helmet 5/9/14/20 등)+부트 speed 유지
⬜ 창고 전종 47종×2 표시(Test_10/Test_09, 슬롯 160) + 아이콘 44종 자동 베이크
⬜ dagger 장착 — Sword 타입 클립+wood_dagger GLB 부착 정합
⬜ GLB 부재 5종 입수 후 재검증(steel/stone/crystal_shield, crystal_gas_mask, crystal_chemical_pack)

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 24차 — 레벨 기반 경험치+병사 사망 EXP+경험치 바 HUD)

> **스코프**: 레벨 기반 경험치 시스템(몬스터 티어 기본 EXP × 레벨 계수 × 난수 산식), 몬스터 Die() EXP 교체(기존 티어 랜덤 제거), 병사 사망 EXP 신규 지급(level×5), HUD 하단 중앙 경험치 바 신규(플랫 바 + Lv 라벨 + 레벨업 펄스 + MAX 처리).

### 변경 사항
**`Core/Data/MonsterLevelData.cs`** — 티어별 EXP 기본값:
- `_beginnerExpBase = 15f` / `_intermediateExpBase = 60f` / `_advancedExpBase = 180f`(78-86행) + `GetExpBase(tier)` getter(212행)
- `OnValidate` 양수 클램프 `Mathf.Max(1f, ...)`(256-258행) — 0이면 경험치 획득 불가 방지

**`Systems/AnimalAI.cs`** — 몬스터 Die() EXP 교체:
- 기존 티어 랜덤(10~30/50~100/150~300) 제거 → `exp = Max(1, Round(base × (1 + level×0.1) × Random(0.8~1.2)))`(807-811행)
- `MonsterLevelManager.Data` null 가드 + 폴백 base 15(809행)

**`Systems/GuardPlaceholder.cs`** — 병사 사망 EXP 신규:
- `Die()`에 `exp = Max(1, Round(level×5 × Random(0.8~1.2)))`(437행) + `PlayerStats.AddEXP`(438행) + CombatLog "병사 처치 경험치 +N"(439행)

**`UI/HUD.cs`** — 경험치 바 신규:
- 하단 중앙 핫바 위 360×12 플랫 바(다크 네이비 배경+스카이블루 채움+회백 테두리, 139-157행)
- Lv.N 라벨 + 바 위 수치(320/500), MAX(MaxLevel 50) 도달 시 100% 채움+"MAX" 라벨(722/746행, 0 나눔 방지)
- 레벨업 0.5s 펄스 — `_prevExpLevel` 엣지 감지(386-388행) → `_lastLevelUpTime` 기반 스카이블루→흰색 플래시 감쇠(734행)
- 채움 비율 = `(CurrentEXP − GetExpForLevel(lv)) / (GetExpForLevel(lv+1) − GetExpForLevel(lv))`(726-727행)
- 핫바 캔버스 스케일 환산 겹침 방지 — `hotbarTopY = Screen.height − 162×canvasScale`(301행), static 캐시 GC 금지 관례 유지

균형 실측: 슬라임 Lv1 평균 17 EXP → Lv2(100) 약 6마리, 병사 Lv10=50/Lv40=200, Advanced Lv30 평균 720(구 225 대비 3.2배)

### 검증
- 배치컴파일: **`error CS=0`** + QaValidator 전체 통과(Errors:0, 배치 종료 return code 0)
- 정적 QA(서브에이전트): 변경 4파일 brace 균형 전부 통과 — MonsterLevelData 11/11, AnimalAI 172/172, GuardPlaceholder 139/139, HUD 110/110
- grep 검증: `GetExpBase` 정의(MonsterLevelData 212행)+AnimalAI 사용(809행, null 가드+폴백 15), GuardPlaceholder `AddEXP`(438행)+CombatLog(439행), HUD EXP바 심볼(360×12 140-141행/펄스 `_lastLevelUpTime` 149행/MAX 722·746행), 기존 switch 티어 랜덤(10~30/50~100/150~300) 잔여 **0건**, `OnValidate` EXP 양수 클램프(256-258행), `PlayerStats.GetExpForLevel/MaxLevel=50` 존재(282/42행)

### Play 판정 대기
⬜ 몬스터 처치 시 레벨 기반 EXP 지급(CombatLog 수치 = base×(1+Lv×0.1)×난수 정합)
⬜ 병사 처치 시 "병사 처치 경험치 +N" 로그+EXP 지급
⬜ HUD 경험치 바 표시(하단 중앙 핫바 위, Lv.N+수치) + 채움 비율 정합
⬜ 레벨업 시 펄스 0.5s + 슬라임 약 6마리로 Lv2 체감 균형
⬜ Lv50(MAX) 도달 시 100% 채움+MAX 라벨

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 23차 — 스케일HP게이트+접지+적중지점 크로스+DnD좌표 수리+빈하트+I키)

> **스코프**: 테스트 씬 몬스터 "몇 타에 안 죽음" 근본 수리(몬스터 레벨 HP 스케일 게이트 — hpPerLevel×level로 MaxHP 과대 → HP바 비율이 0 근처여도 실제 HP 잔여로 미사망, 영상 4 실측), 병사/몬스터 모델 bounds 기반 접지(GroundModelToY), 십자가 VFX 실제 적중 대상 지점 발화(빈 스윙 스킵), 슬래시 VFX 플레이어 정면 고정, 창고 표시 소스 검증(정상 판정), Test_10 I키 자가 등록, DnD 이중 좌표계 수리(스크린 좌표 규약 통일), 하트 HUD 빈칸 렌더+매 프레임 폴링 갱신.

### 변경 사항
**`Systems/MonsterLevelManager.cs` + `Systems/AnimalAI.cs`** — 몬스터 레벨 스케일 게이트:
- `LevelScalingEnabled` static 게이트 신설(기본 true, 25행) — `ApplyLevelStats` 초입 스킵(AnimalAI 124행, Respawn은 _maxHP 재사용으로 안전)
- 원인 — 테스트 씬 몬스터 hpPerLevel×level 오버라이드로 MaxHP 과대 → HP바 비율이 거의 0처럼 보여도 실제 HP가 남아 계속 두드려도 안 죽는 증상(영상 4 실측)

**`Systems/TestTerritoryCombatSetup.cs` + `Systems/TestAllInOneSetup.cs`** — 테스트 부트 게이트:
- Test_10 부트(34행) + Test_09 부트(75행)에서 `LevelScalingEnabled = false` — 몬스터 몇 타에 사망해야 공격/사망 검증 가능

**`Systems/TestTerritoryCombatSetup.cs`** — 모델 bounds 기반 접지:
- `GroundModelToY(model, targetY)` 신설(762행) — 렌더러 bounds 최저점을 pos.y에 정렬
- CreateGuard/SpawnMonster 3호출(374 몬스터 + 692/738 병사) — 스폰 y가 모델 중심 기준이던 접지 오차(공중 부양/파묻힘) 해소

**`Systems/PlayerCombat.cs` + `Systems/HumanoidClipDriver.cs`** — 십자가 VFX 적중 지점 발화:
- `LastHitPoint/LastHitValid/LastHitTime` static 신설(21-25행) — AttackTarget 적중 시 대상 Collider/Renderer bounds 중심+up*0.2 갱신(311-313행), 미스 시 무효화(218행)
- `FireComboCross` 게이트 — `LastHitValid && Time.time - LastHitTime <= 0.5s` 아니면 크로스 스킵(671-672행, 빈 스윙 무발화), pos=LastHitPoint + dir=(LastHitPoint−플레이어 머리) 정규화(679-681행)

**`Systems/HumanoidClipDriver.cs`** — 슬래시 VFX 정면 고정:
- 플레이어 정면 고정점 발화 — `t.position + t.forward * 0.9f + Vector3.up * 1.2f`(631행), dir/roll은 실측 궤적 상수 유지

**`UI/WarehouseUI.cs` + `UI/InventoryWindow.cs` + `UI/TerritoryWarehouse.cs`** — 창고 표시 소스 검증(정상):
- `RefreshFromWarehouse`가 `WarehouseSystem.GetItems(territoryId)` 렌더(WarehouseUI 260/319행), 타이틀 "창고"(InventoryWindow 526행), `TerritoryWarehouse→SetContextMode` territoryId 전달 정합(247-248행) — 표시 소스 이상 없음

**`UI/InventoryWindow.cs`** — Test_10 I키 수리:
- TestTerritoryCombatSetup에 UIInventoryHotkey 바인딩 전무 확정 → `Awake()` 자가 등록(핫키 부재 시 AddComponent+Bind, 선착순 레지스트리, 204-210행) + Bind 누락 폴백

**`UI/WarehouseUI.cs` + `UI/InventoryWindow.cs`** — DnD 이중 좌표계 수리:
- 원인 — GUIToScreenPoint 결과를 Rect.yMin에 그대로 저장 + 판정점은 GUI 좌표 → y 뒤집힘(드롭이 유령 영역 한 칸 위에 판정)
- 단일 스크린 좌표계(좌하단 원점, y 상승) 규약 통일 — 캐시 y=sp.y−height 보정(WarehouseUI 367-370/438-439, InventoryWindow 639-641/694-695), 판정점 동일 변환(WarehouseUI 454, InventoryWindow 87-100)

**`UI/HUD.cs`** — 하트 빈칸 렌더+폴링 갱신:
- 원인 — `PlayerHealth.SetMaxHP()`가 OnHPChanged 미발화 → 레벨업 시 하트 수 갱신 안 됨
- HUD가 CurrentHP/MaxHP 매 프레임 폴링(315-323행), `ceil(MaxHP/20)` 전체 하트 렌더+잔여 Empty 외곽 링(530-584행) — 레벨업 시 빈 하트 자동 증가

### 검증
- 배치컴파일: **`error CS=0`** + QaValidator 전체 통과(Errors:0, 배치 종료 return code 0)
- 정적 QA(서브에이전트): git 변경 .cs 12파일 brace 균형 전부 통과(open=close)
- grep 검증: `LastHitPoint` 정의+적중 갱신+미스 무효화(PlayerCombat 21-25/218/311-313)+크로스 게이트(HumanoidClipDriver 671-681), `GroundModelToY` 정의(762)+3호출(374/692/738), `LevelScalingEnabled` 정의(MonsterLevelManager 25)+ApplyLevelStats 스킵(AnimalAI 124)+Test 부트 false 2곳(TestTerritoryCombatSetup 34/TestAllInOneSetup 75), 하트 폴링(HUD 315-323)+`CeilToInt(MaxHP/20)`(359/530)+Empty 링(584/652), DnD 스크린 좌표 규약(WarehouseUI 367-370+438-439+454, InventoryWindow 84-100+639-641+694-695), I키 자가 등록(InventoryWindow 204-210), 슬래시 정면 고정(HumanoidClipDriver 631)

### Play 판정 대기
⬜ 몬스터 몇 타에 실제 사망(HP바 빔=실 HP 감소) + 시체 소멸+바구니 드랍
⬜ 병사/몬스터 모델 발이 땅에 붙는지(공중 부양/파묻힘 해소)
⬜ 적중 시 크로스 VFX가 대상 몸에서 발화 + 빈 스윙엔 미발화
⬜ 레벨업 시 하트 칸 자동 증가 + 잔여 Empty 링 렌더
⬜ DnD(인벤↔창고) 정상 판정 + Test_10 I키 인벤 토글

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 22차 — 사망루프 완결+병사애니+UI 플랫 리디자인+실측 스윙 방향)

> **스코프**: 몬스터 사망 루프 완결(Die() 시체 처리 수리 + 슬라임 분열 테스트게이트), 병사 T포즈 근본원인(FBX 리소스 경로 `_rigged` 오타) 수리, Test_10 창고 territoryId 불일치+20슬롯 절단 수리, 매 클릭 스윙 FX(4클릭 콤보 재시작), 십자가 VFX PlayCross 타 완료 지점 발화, 플레이어 피격 BasicHit 임팩트, UI 플랫 전면 리디자인(Flat 모드), 창고 닫기(E토글+ESC+3m이탈), 드래그앤드롭(ItemDragContext), Test_09 I키 바인딩+시딩 리플렉션 버그 수리, 은신 모드(StealthSystem 부트 보장+반투명 피드), 스윙 방향 실측(WeaponSwingDirectionAnalyzer v4 리그 리타깃)→FireComboSlash 상수 교체.

### 변경 사항
**`Systems/AnimalAI.cs`** — 사망 루프 완결:
- `Die()` 시체 처리 수리 — 자식 렌더러/콜라이더 전체 + 부착된 MonsterHeadUI 일괄 토글(자식 포함 탐색, GLB 루트 무렌더러 보완), 전리품 바구니 스폰 로그(🧺)
- 최소 전리품 보장 — 바구니가 빈 채로 소멸하지 않도록 guaranteedItem x1 자동 추가
- `TryAutoHunt` 동일 계약 — 사망 판정/드랍 반환 경로 동일화

**`Systems/MonsterSkillSystem.cs` + `TestTerritoryCombatSetup.cs`** — 슬라임 분열 테스트게이트:
- `SlimeSplitEnabled` static 게이트 신설(기본 true) → Test_10에서 `false` 설정("몬스터가 안 죽고 늘어난다" 체감 차단, 분열체는 AnimalAI만 부착된 풀HP 구체)

**`Systems/TestTerritoryCombatSetup.cs` + `TerritoryBuilder.cs`** — 병사 T포즈 근본원인 수리:
- FBX 리소스 경로 오타 — `soldier_lv1-20` → **`soldier_lv1-20_rigged`**(실제 FBX 에셋명에 `_rigged` 접미사, Humanoid 임포트 animationType:3, Player_Rigged_Heat.fbx 동일 계약). 기존 경로는 항상 null 로드 → GLB 폴백(T포즈 지속)이 근본원인
- 두 호출부(TerritoryBuilder 541-543 + TestTerritoryCombatSetup 656-665) 모두 수리, GLB 원본 머티리얼 복용 경로 병행 유지

**`Systems/TestTerritoryCombatSetup.cs`** — Test_10 창고 수리:
- 박스 territoryId 불일치 수리 — `wh_test_1`/`wh_test2` → **`wh_test`** 단일 ID 통일(시딩과 조회 불일치 제거)
- 64슬롯 확장 — 시딩 33종이 20슬롯 절단 → 무기 1종만 보였던 원인 수정(`Configure("wh_test",64,3f)`)

**`Systems/HumanoidClipDriver.cs`** — 매 클릭 스윙 FX + 실측 상수:
- 4클릭 콤보 완전 무시 → **4클릭 시 콤보 재시작**(1타부터)
- 십자가 VFX `PlayCross` — 타 완료 지점 발화(스윙과 동일 위치/방향 계약)
- 플레이어 피격 임팩트 FX — HP 감소 엣지에서 `PlayImpact(BasicHit)` 발화(Organic 매핑)
- 스윙 방향 실측 상수 적용 — 1타 `Euler(1.3f, -58f)`(좌전방 수평), 2타 yaw 63.2°+roll -90(실측 pitch 78° 수직 상승 궤적), 3타 `Euler(28.7f, 143.6f)`(우후방 사선)

**`Editor/WeaponSwingDirectionAnalyzer.cs` (v4)** — 스윙 방향 실측:
- Heat 리그 리타깃 + RightHand PlayableGraph 샘플링(Meshy FBX는 스켈레톤 4뼈뿐 → 플레이어 리그로 샘플)
- 실측 결과: 1타 yaw **-58°**/pitch 1°, 2타 pitch **78°**(수직 상승), 3타 yaw **143.6°**/pitch 29° → `FireComboSlash` 상수 교체 완료

**`UI/InventoryArtLibrary.cs` + 전 창 UI** — 플랫 전면 리디자인:
- `ArtStyleMode.Flat` 신설(기본) — 다크 네이비 반투명 백플레이트 + 회백 라운드 보더 + 스카이블루 하이라이트, static 지연 생성 캐시(파기 금지)
- 인벤/스탯창/상점/크래프트/Loot/창고 창 플랫 스타일 일괄 교체

**`UI/TerritoryWarehouse.cs`** — 창고 닫기 + 드래그앤드롭:
- 닫기 3중 — E 토글 + ESC + 3m 상호작용 반경 이탈 자동 닫기
- `ItemDragContext` 신규 정적 컨텍스트(`using ProjectName.Core`) — 인벤↔창고 이동/슬롯교체/핫바/고스트 렌더(프레임 스탬프 가드, Icon 파기 금지)

**`Systems/TestAllInOneSetup.cs`** — Test_09 바인딩+시딩 수리:
- I키 바인딩 수리 — UIInventoryHotkey 리플렉션 Bind 연결(부착+Bind 2단)
- 시딩 리플렉션 버그 수정 — 존재하지 않는 `AddItem(string,int)` 시그니처 호출로 전부 무음 실패 중이던 것 수리

**`Systems/StealthSystem.cs` + `TestAllInOneSetup.cs`** — 은신 모드:
- StealthSystem 부트 보장(Instance 없으면 Player에 부착 — 이전 Test_09에서 C키 은신 무음)
- 렌더러 반투명 피드(URP 표면 전환) + 은신물약 시딩

### 검증
- 배치컴파일: **`error CS=0`** + QaValidator 전체 통과(Errors:0, 배치 종료 return code 0)
- 정적 QA(서브에이전트): git status 변경 파일 22개 렉서식 검증(문자열/주석 제외 brace/paren/bracket 균형) 전부 통과
- grep 검증: `_rigged` 경로 잔여 누락 0건(TerritoryBuilder 541-543 + TestTerritoryCombatSetup 656-665 + RuntimeModelLoader 매핑), `SlimeSplitEnabled` 정의(MonsterSkillSystem 677)+게이트(685)+Test_10 false 설정(29), `PlayCross` 정의(SlashVFXRunner 94)+호출(HumanoidClipDriver 668), `Euler(1.3f, -58f` 실측 상수 반영(HumanoidClipDriver ComboStageDirection), `ItemDragContext` using ProjectName.Core 확인
- 기능 존재 확인: Die() 시체 토글+바구니 스폰(AnimalAI 785/835/883), wh_test 단일 ID+64슬롯 Configure, 창고 ESC/3m 이탈 닫기(TerritoryWarehouse 109-124), I키 Bind(TestAllInOneSetup 529-551), EnsureStealthSystem 부트(558-573), Flat 모드(InventoryArtLibrary 28-72), Multiple Slashes 인스톨러(VFXResourceInstaller 31-68), BasicHit 임팩트(HumanoidClipDriver 346-351)

### Play 판정 대기
⬜ 몬스터 좌클릭→HP바 감소 + 사망 시 시체 소멸 + 바구니 드랍
⬜ 병사 걷기 애니(FBX _rigged 로드 성공 → T포즈 해소)
⬜ 클릭마다 스윙 FX + 십자가 FX 방향 정합(실측 -58°/수직상승/우후방 사선)
⬜ 플레이어 피격 시 하트+HitLight+BasicHit 임팩트 FX
⬜ 창고 E토글/ESC/3m이탈 닫기 + 우클릭 장착 + DnD(인벤↔창고)
⬜ Test_09 I키 인벤 토글, C키 은신+렌더러 반투명, 창고 무기 3종 표시

---

## 📌 세션 종합 스냅샷 (2026-09-11 ✅ 21차 — 콤보 FX 즉시발화+피격애니+전투판정 수리+창고 시딩)

> **스코프**: Test_10 몬스터 좌클릭 'HP바 미감소/피격반응 부재'의 근본원인(glTFast GLB 프리팹은 메시 콜라이더를 자동 생성하지 않음 → 기존 BoxCollider가 시각 몸체를 커버하지 못해 레이캐스트 히트 0건) 수리. 근접 스윕 폴백 안전망, HitReaction 플린치 복원, 콤보 스윙 FX 클릭 즉시발화 전환, 플레이어 피격 HitLight 연결, Test_09 창고 전무기 시딩.

### 변경 사항
**`Systems/TestTerritoryCombatSetup.cs`** — 몬스터 히트박스 수리(근본원인 수정):
- 몬스터 BoxCollider 1.5³ → **2×2×2, center y=0.75** (GLB 시각 몸체 대부분 커버) + 히트 볼륨 로그
- 근본원인 명시: glTFast는 GLB 콜라이더를 자동생성하지 않음 → 추가한 BoxCollider가 유일한 레이캐스트 히트 볼륨

**`Systems/PlayerCombat.cs`** — 근접 판정 안전망:
- `AttackCenterScreen()` bool 반환화(성공 true/미스 false) + 미스 진단 로그(cursorRay히트 수/최근접 거리/사거리)
- `MeleeSweepFallback()` 신설 — 커서/화면중앙 미스 시 플레이어 위치 OverlapSphere(무기 사거리, 최소 2.5m)로 가장 가까운 살아있는 IDamageable 즉시 적중(자기 자신 제외)
- `_mainCamera` null 경고(Camera.main 태그 확인 유도)

**`Systems/HitReaction.cs`** — 피격 반응 복원(비행 버그 미재발 설계):
- Awake Renderer 자식 탐색(`GetComponentInChildren<Renderer>`) — GLB 프리팹은 루트에 Renderer가 없어 히트 플래시가 조용히 스킵되던 직접 원인 보완
- `_knockbackDisabled` 경로에 스케일 펄스 플린치(0.05s 팽창→0.15s 복귀) — **position 무접촉**(transform.position 건드리지 않음 → 슬라임 비행/유령 변위 재발 없음), AddForce/KinematicJolt 차단 유지

**`Systems/HumanoidClipDriver.cs`** — 콤보 FX 즉시발화 + 피격 애니:
- 콤보 스윙 FX를 임팩트 프레임 대기에서 **클릭 즉시발화**로 전환 — `_comboImpactFired`/`ComboImpactNormT` 삭제, 콤보 시작(1타)·스테이지 진행 직후 `FireComboSlash` 호출
- 플레이어 피격 애니 연결 — `_prevPlayerHP` HP 감소 엣지 감시 → `SetTrigger("HitLight")`(AnyState 전이), IsDead 제외(Death 경로 담당)

**`Systems/TestAllInOneSetup.cs`** — Test_09 창고 + 전무기 시딩:
- `SetupWarehouse()` 신설(Awake 호출): 창고 박스 2개(10,0.55,7)/(-12,0.55,0) + TerritoryWarehouse 리플렉션 부착 + `Configure("wh_test_09", 64, 3f)`(asmdef 순환 회피)
- `WarehouseSystem._maxSlotsPerTerritory` 20→64 리플렉션 확장(전 장비 시딩 수용)
- `SeedAllItemsToWarehouse("wh_test_09")`: 무기 SwordWood/SpearWood/BowWood, 방어구 LeatherArmor/ClothArmor/StealthBoots/DarkCloak, 도구 Pickaxe/Axe/FishingRod, 재료/어류/Gold 999 등 33종+ 전 아이템 — E키 근접 상호작용 오픈

### 검증
- 배치컴파일: **`error CS=0`** (3방향 병렬 코드 에이전트 라운드 통과)
- 정적 QA(서브에이전트, 5파일): 괄호/중괄호 균형 통과(주석·문자열 제외), `_comboImpactFired`/`ComboImpactNormT` 잔여 참조 0건(레포 전역), `MeleeSweepFallback` 정의+호출 1쌍, `SetupWarehouse` 정의+Awake 호출 확인, `AttackCenterScreen` 호출부(`else if (!AttackCenterScreen())`)가 bool 반환 시그니처와 일치, HitReaction 변경부(`_knockbackDisabled` 경로/ScalePulse)에 AddForce/Rigidbody·position 접촉 없음(비행 버그 재발 방지)
- 컨트롤러 AnyState `HitLight` 전이 존재 사전 확인

### Play 판정 대기
- Test_10 몬스터 좌클릭 시 HP바 감소 + 플린치/히트 플래시, 클릭 즉시 스윙 FX 발화, 플레이어 피격 시 HitLight 재생, Test_09 창고 E키 오픈 + 전 무기 장착 테스트

---

## 📌 세션 종합 스냅샷 (2026-09-11 20차 — 콤보 단일테이크 슬라이스(B안)+타별 스윙 FX)

> **스코프**: 콤보 시스템 재작성(B안) — 3연타 전체가 담긴 단일 클립(Weapon_Combo_2)을 WeaponCombo 상태 하나로 재생하고 플레이헤드를 직접 제어. 경계 홀드+Grace로 콤보 입력 대기를 구현하고, 1~3타 임팩트 프레임마다 스윙 방향이 다른 Slash FX를 발화.

### 변경 사항
**`Editor/PlayerComboControllerSetup.cs`** (신규) — 컨트롤러 적용:
- 전역 namespace 에디터 스크립트, `Player_AC.controller`에 `WeaponCombo` 상태 추가 (motion = `Weapon_Combo_2.fbx`, guid `cf0d62405fd723f4094948a660129696`)

**`Systems/HumanoidClipDriver.cs`** — 콤보 재작성:
- `SetTrigger("AttackCombo*")` 제거 → `_anim.Play("WeaponCombo", 0, normT)` 플레이헤드 직접 제어
- 경계 홀드 0.25s Grace(무입력 시 EndCombo) / 콤보 만료 → `CrossFade("Idle", 0.15)`
- `FireComboSlash(stage)` 타별 스윙 VFX: 1타 -30° / 2타 +35° / 3타 roll-90 수직 궤적
- 레거시 `Attack*` 상태 normT≥0.5 단일 슬래시 보존, `attackStateHold`에 "WeaponCombo" 추가(Speed 0 고정 — Idle/Walk 인터럽트 차단)

**`Systems/SlashVFXRunner.cs`** — `PlaySlash(pos, dir, arcRollDegrees)` 3인자 오버로드 추가 (기존 2인자 → 3인자 위임 유지, 기존 호출부 호환)

**`Systems/PlayerCombat.cs`** — `TryAttack`의 즉시 `PlaySlash` 블록 제거 (VFX 발화 주체를 드라이버 임팩트 프레임으로 일원화)

### 검증
- 배치컴파일: **`error CS=0`** + "Exiting batchmode successfully" (compile.log)
- 컨트롤러 YAML: `m_Name: WeaponCombo` 상태(3557행) + m_Motion guid `cf0d6240...` = `Weapon_Combo_2.fbx.meta` guid 일치 확인
- 정적 QA(서브에이전트): 4파일 괄호/중괄호 균형 통과(주석 제외), 콤보 트리거 잔여 참조 0건(`_comboCount`/`ComboWindow`/`SetTrigger("AttackCombo*")` — attackStateHold IsName 레거시 감시 문자열은 정상 잔존), PlayerCombat `PlaySlash` 잔여 0건, SlashVFXRunner 2인자 시그니처 호환 확인
- 사전결함(무관): QaValidator.CheckScenes가 Packages 내 addressables 테스트 씬에서 ArgumentException — 컴파일 게이트는 배치컴파일로 별도 통과

### Play 판정 대기
- 좌클릭 3연타: 1타(좌 -30°)→2타(우 +35°)→3타(수직 roll-90) 임팩트 프레임(`ComboImpactNormT` 0.18/0.53/0.84)별 스윙 FX 방향, 경계 홀드 0.25s 내 클릭 시 다음 타 진행, 무입력/만료 시 Idle 복귀(0.15 블렌드)

---

## 📌 세션 종합 스냅샷 (2026-09-10 ✅ 19차-2 — 실제 하트 아이콘 절차 생성)

> **스코프**: 사각형 근사 하트를 실제 하트 모양 아이콘으로 교체. 하트 형상 텍스처를 절차 생성하고 GUI.color 틴트로 Full/반/Empty 표시, 체력 감소를 직관 표시.

### 변경 사항
**`UI/HUD.cs`** — 실제 하트 아이콘 절차 생성:
- `CreateHeartMaskTexture(HeartMaskMode)` — 64×64 `Texture2D(RGBA32)` + `SetPixels32` + `Apply()` + `HideFlags.HideAndDontSave` (프로젝트 절차 텍스처 관례)
  - 하트 형상 판정: 입방 방정식 `(u²+v²−1)³−u²·v³ ≤ 0`, 픽셀당 4서브샘플로 간이 안티앨리어싱
  - 좌표: Texture2D (0,0)=좌하단 + GUI.DrawTexture 무반전 → 하트 세워짐. scale=26, vShift=0.13
  - Full(전체 내부) / Half(좌반 u≤0) / Empty(외곽 2px 링: 내부 4방향 이웃 2회 침식으로 추출)
- `EnsureHeartTextures()` + `_texHeartFull/Half/EmptyWhite` static lazy 캐시 (최초 1회, GC 금지). 마스크는 순수 흰색+알파라 `GUI.color` 틴트로 어떤 색이든 칠함
- `DrawHeart(rect, color, state)` — GUI.Box 사각형 완전 제거 → `GUI.color` 틴트 + `GUI.DrawTexture`. 채움 먼저→링 나중(2-pass)으로 Half 반투명이 링 오염 방지. 임시하트=Full 마스크+노랑 틴트 재사용. GUI.color 저장/복원.
- 호출부 4곳 서명 갱신, `_rectHeartInner`/halfRect/`CacheStaticRects` 잔여 제거(참조 0건 확인)

### 검증
- 배치컴파일: **`error CS=0`** + "Exiting batchmode successfully" (`CompileScripts: 12469ms`)
- UI.dll 심볼: `CreateHeartMaskTexture`/`EnsureHeartTextures`/`_texHeartFullWhite`/`HeartMaskMode` 확인
- 서브에이전트: 괄호/중괄호 균형 검증 통과(주석 제외), 229/229

### Play 판정 대기
- Test_10: 실제 하트 모양으로 표시, 체력 감소 시 빨강 Full→좌반만 빨강(Half)→회색 외곽(Empty)으로 단계 표시, 버프 초과 체력은 노랑 하트

---

## 📌 세션 종합 스냅샷 (2026-09-10 19차-2 — 실제 하트 아이콘 절차 생성)

> **스코프**: Test_10에도 플레이어 하트 HUD 표시, 피격 시 데미지만큼 하트·숫자 감소, 방어력을 비율식 감소로, 스탯(VIT)으로 체력·방어 상승 시 하트 자동 증가.

### 변경 사항
**`Systems/TestAllInOneSetup.cs`** — HUD 자동 부착:
- `_hudType` 필드 + `CacheUIReflectionTypes()`에서 `uiAssembly.GetType("ProjectName.UI.HUD")` 로드
- `EnsurePlayerHUD()`: 활성 HUD 없으면 `new GameObject("HUD")` + `AddComponent(_hudType)` (리플렉션, Canvas 불필요). 존재 시 스킵
- **디버깅**: `Object.FindObjectsByType(...)` CS0104(`using System;`+`UnityEngine` 모호) → `UnityEngine.Object.FindObjectsByType` 정규화

**`UI/HUD.cs`** — 숫자 HP 표시:
- `DrawHPNumberText()` 신규: 하트 영역 아래 `{(int)_currentHP} / {(int)_maxHP}` (예: `85 / 140`), 캐시 스타일(`_cachedHPTextStyle`, 18px Bold), 30% 이하 노랑 경고, `GUI.color` 원복
- `OnGUI`의 `DrawHearts()` 직후 호출

**`Core/PlayerHealth.cs`** — 방어력 비율식:
- `actualDamage = damage × (100f/(100f+defense))` (평평 감소 `damage-defense` 제거), `Mathf.Max(0, ...)`, 로그에 감소율(`reductionRate`) 포함

**`Core/PlayerStats.cs`** — 방어력 VIT 스케일링:
- `FinalDefense = base + level×0.5 + VIT×2 + 장비` (Lv1 VIT5→def≈10.5→약9.5% 감소). 기존 평평 가정 사용처(StatusWindowUI 표시뿐) 확인 후 전환

### 검증
- 배치컴파일: **`error CS=0`** + "Exiting batchmode successfully" (`CompileScripts: 11885ms`)
- DLL 심볼: Systems.dll `EnsurePlayerHUD`, UI.dll `DrawHPNumberText`, Core.dll `get_FinalDefense` 확인
- 하트 1칸 = 20HP (기본 MaxHP 100 = 5칸), MaxHP 상승 시 `ceil(MaxHP/20)`로 하트 자동 증가 (기존 로직 유지)

### Play 판정 대기
- Test_10: 왼위 하트 5칸 + `100 / 100` 숫자 표시, 몬스터 피격 시 데미지만큼 하트·숫자 깎임(방어 비율 반영), VIT 투자 시 MaxHP/하트 증가 확인

---

## 📌 세션 종합 스냅샷 (2026-09-10 18차 — 전리품 드랍 완결: 몬스터↔병사 사망 전리품)

---

## 📌 세션 종합 스냅샷 (2026-09-10 18차 — 전리품 드랍 완결: 몬스터↔병사 사망 전리품)

> **스코프**: 몬스터와 병사가 체력 고갈 시 죽으면서 전리품(LootBasket) 드랍. (1)몬스터-병사 상호공격, (2)병사 장비 드랍, (3)최소 전리품 보장, (4)Test_10 전투 대치 + 검증.

### 변경 사항
**`Systems/AnimalAI.cs`** — 몬스터 공격 일반화 + 최소 보장:
- `TryAttack()`: 어그로 대상이 살아있는 병사(등 `IDamageable`)이면 `TakeDamage(_attackDamage, hitDirection, "melee")`로 공격. 대상 무효 시 기존 `PlayerHealth.Instance.TakeDamage` 폴백. 신규 헬퍼 `GetAliveAggroDamageable()` (활성+생존 검증)
- `Die()`: `dropTable.ApplyToBasket` 및 fallback **두 경로 모두** 이후 `basket.IsEmpty` 검사 → 비었으면 `_meatDrop`(없으면 `PlayerInventory.Gold`) 1개 보장

**`Systems/GuardPlaceholder.cs`** — 병사 사망 장비 드랍:
- `Die()`에 `DropEquippedItems(basket)` 호출: `WeaponItem/ShieldItem/HelmetItem/ArmorItem` 4슬롯 중 null 아니면 `AddItem(item,1)`(100% 드랍) + 로그
- 사망 후 `basket.IsEmpty`이면 `PlayerInventory.Gold` 1개 보장. 기존 SoldierDropTable/gold+fur 폴백 무겁게 유지

**`Systems/TestAllInOneSetup.cs`** — 전투 대치 검증:
- 몬스터 15m/병사 11m 반경, i번째 병사→같은 몬스터(거리≈4m, Beginner detection/Aggro 10m 내)
- `CombatScenarioRoutine()`: 15초 후 각 몬스터에 근접 병사 `SetAggroTarget` 강제 전투 → 3초 간격 `LogCombatStatus()`(어르로/병사 HP), 병사 사망 시 `OnAnyGuardDied`→`CheckLootBasketSpawned()`(10초 LootBasket 폴링). `OnDestroy` 구독 해동
- 신규 SerializeField: `_combatStartDelaySeconds=15`, `_monsterSpawnRadius=15`, `_guardSpawnRadius=11`

### 검증
- 배치컴파일: **`error CS=0`** + `CompileScripts: 19864ms` + "Exiting batchmode successfully"
- Systems.dll 심볼: `GetAliveAggroDamageable`/`DropEquippedItems`/`DropEquippedSlot`/`CombatScenarioRoutine`/`LogCombatStatus`/`CheckLootBasketSpawned` 반영

### Play 판정 대기
- 몬스터가 병사 공격→병사 사망→장비+테이블 전리품 LootBasket 드랍, 몬스터 사망→고기/재료+최소보장 드랍, 플레이어 바구니 획득. Test_10에서 15초 후 자동 전투로 확인.

---

## 📌 세션 종합 스냅샷 (2026-09-10 17차 — 씨앗 획득 + 자동수확 + 병사 명령 루프 + 상점 확장)

---

## 📌 세션 종합 스냅샷 (2026-09-10 17차 — 씨앗 획득 + 자동수확 + 병사 명령 루프 + 상점 확장)

> **스코프**: 농경 루프 완결 — (1)씨앗 획득(채집 희귀드랍 + 상점 랜덤판매), (2)경작지 자동수확(약초꾼-소유 조건/인벤토리 직행), (3)병사 RTS/전투 명령 실제 수행(이동·근접공격), (4)씨앗 소모 파종.

### 변경 사항
**`Core/PlayerInventory.cs`** — 씨앗 ItemData 5종 추가: `Seed_Red/Purple/Yellow/Silver/Green` (`herb_seed_*`, `ItemCategory.Material`, maxStack 20). Silver/Green은 `ItemRarity.Rare`.

**`Systems/HerbPickup.cs`** — 채집 시 씨앗 희귀 드랍:
- `SeedDropChanceCommon=0.24f`(흔한 허브 Red/Purple/Yellow), `SeedDropChanceRare=0.09f`(Silver/Green)
- `Harvest()`: 바구니(LootBasket)에 허브와 함께 씨앗 추가. `TryAutoGather()`: 바구니 없이 인벤토리 직행 구조이므로 동일 경로로 인벤토리 직접 추가
- 헬퍼: `SeedItemForCrop(HerbType)` 매핑 + `AddSeedDrop(basket, herbType)`

**`Systems/FarmPlot.cs`** — 씨앗 소모 파종:
- `Plant(crop)` 시작서: `HasItem(seed.id)` 없으면 "씨앗 부족" + phase 유지(return), 있으면 `RemoveItem(seed.id, 1)` 후 파종
- `using ProjectName.Core;` 추가 + `static SeedItemForCrop(HerbType)` 매핑

**`Systems/HerbGatheringMission.cs`** — 경작지 자동수확 연동:
- `FarmPlot` 부착 `HerbPickup` 후보는 `plot.IsOwned == false`면 수집 제외(소유 상실 직후 선수확 방지)
- Ready 경작지(부착 HerbPickup)는 기존 HerbPickup 수집 루프에 **자연 편입** — 재구성 없이 소유 필터만 추가
- 디버그 로그: Ready 편입 수 / 경작지 출신 개별 수확 / 미소유 제외

**`Systems/GuardPlaceholder.cs`** — 병사 명령 수행 루프 (`ExecuteMovement()`):
- Update 시작부에서 호출(플레이어 부재와 무관). 상수: `MOVE_CLEAR_RADIUS=1.0`, `ATTACK_ARRIVE_RADIUS=1.5`, `ATTACK_MELEE_RANGE=2.2`, 쿨다운 1.2s, `MOVE_STOP_RADIUS=0.6`
- **이동** `StepToward`: `_moveSpeed*delta`, 목표 넘어감 클램프 + 지형 `1+GetHeightAt` y 보정(try-catch) + Rigidbody 있으면 MovePosition 우회 + Slerp 회전(8f×delta)
- **공격**: 도달 1.5m→대상 회전+쿨다운 게이트→`PerformAttack`(`HumanoidClipDriver.TriggerAttack` 또는 `rigAnim attack`, 데미지 `level*1.5f`, melee)
- **대상 검증** `ValidateAttackTarget`/`ResolveAttackTarget`: 살아있는 적 IDamageable만, 자신·다른 병사·플레이어 제외
- `GuardCombatAI.UpdateGuardBehavior(this, player.transform)` 매 Update 호출 추가 (전투 종료 후 귀환 연동)

**`UI/ShopWindow.cs`** — 씨앗 판매 랜덤화:
- `RandomizeSeedStock()` public: 일반 씨앗 65% 확률×1~2종, 가격 30~50G(재고 3~5), Silver 별도 35%(150G, `isRare=true`), Fisher–Yates 셔플
- 호출: `InitializeShopInventory()` 끝 + `OnShow()`(개장 시마다 재추첨)

### 검증
- 배치컴파일: **`error CS=0`** + `CompileScripts: 48778ms` + "Exiting batchmode successfully"
- DLL 심볼: Systems.dll → `ExecuteCommand`/`ATTACK_MELEE_RANGE`/`GetAllPlots`/`Seed_Green`/`TriggerAttack`, Core.dll → `Seed_Red~Green`/`GetSeed`, UI.dll → `RandomizeSeedSale` 반영

### Play 판정 대기
- 채집 시 씨앗 드랍(바구니 또는 인벤토리) → 파혈(씨앗 소비) → 성장 → 숙성 → (약초꾼 자동수확 또는 E키) → 재획득 루프
- 상점 개장 시 씨앞 랜덤 등·구매
- 병사 명령: 병사 선택 후 우클릭 이동/적 공격, H키 중단, 전투 종료 후 귀환

---

## 📌 세션 종합 스냅샷 (2026-09-10 16차 — 농경 시스템 도입)

> **스코프**: 자기 소속 영지 부지에서 허브 재배 → 숙성 시 기존 HerbPickup 수확 연동.

### 변경 사항
**신규 `Systems/FarmPlot.cs`** (namespace ProjectName.Systems)
- 상태머신 `CropPhase { Empty, Seeded, Growing, Ready }` — 흙밭 시각(URP/Lit Cube), 허브 성장 단계별 스케일(0.12→0.4→0.8)
- **소유 검증** `ValidateOwnership(bool force)` — `TerritoryDatabase.GetState(...).ownership == PlayerOwned` 아니면 강제 Empty 리셋 + "아군 영지에서만 경작 가능" 메시지. Update 1.5s 주기 체크
- **성장** — `TimeManager` 절대 게임시간(`CurrentDay*86400+GameTime`, 자정 롤오버 안전)으로 게임 일수 경과. TimeManager null 시 `Time.time` 실시간 폴백
- **수확** — Ready 시 `HerbPickup` AddComponent + 리플렉션 `_herbType` → 기존 E키 채집(LootBasket), `OnHarvestStarted` 구독 → Empty 리셋 + HerbPickup 제거(다음 Ready 재부착)

**신규 `Systems/FarmingManager.cs`** — 싱글턴, `SpawnPlots(nation,index,center,rows,cols,spacing,crop,growDays)` 격자 배치 + Configure

**Test_10 결합** — `SetupFarm()` (SetupHerbs 다음 줄 + 파일 말미 메서드) — 내 영지 East_01 근처 2×2=4칸, `SurfaceY(x,z)+0.1` 계약

### 디버깅 (컴파일 순환)
- 1차 컴파일: `FarmPlot.cs` `ValidateOwnership` CS0103 **9건** (정의 누락, 호출 3곳×3타입 보고) → 정의 추가
- 재컴파일: **`error CS=0`**, Systems.dll에 FarmPlot/ValidateOwnership/SpawnPlots 심볼 확인

### Play 판정 대기
- 내 영지 밭 E키 파종 → 성장 → 숙성 → E키 수확(LootBasket) 루프 + 소유 상실 시 경작 불가.

---

## 📌 세션 종합 스냅샷 (2026-09-10 15차 — 몬스터 체력바 미감소 버그 수정)

> **스코프**: Test_10에서 몬스터를 공격해도 체력바가 줄지 않는 버그를 근본원인부터 수술.

### 근본원인 (Phase 1~2 정적 분석)
- Test_10 몬스터는 GLB(Slime_Rigged) Instantiate → **AnimalAI(IDamageable)는 루트 GO**, Collider는 **GLB 자식 모델**에 있음.
- 공격 시스템(`AttackSystem`/`PlayerCombat`)이 `hit.collider.GetComponent<IDamageable>()` — **콜라이더 본인만** 탐색 → 자식 콜라이더 히트 시 루트 AnimalAI 못 찾아 `null` → 공격 무시 → `TakeDamage` 미호출 → 체력바 미감소.
- 선례: `ProceduralAttack.cs:457`이 동일 문제를 `col.GetComponentInParent<Damageable>()`으로 해결.

### 수정 (탐색부만, 데미지/거리/드랍 로직 불변)
- `AttackSystem.cs` — FindTargetByRaycast(258)·FindTargetBySphereCast(280) → `GetComponentInParent<IDamageable>()`
- `PlayerCombat.cs` — RaycastAll(231)·SphereCastAll(251)·AttackCenterScreen(352) → `GetComponentInParent<IDamageable>()` (총 5곳)

### 검증
- `grep GetComponent<IDamageable>` 잔존 0, brace 균형
- `QaValidator` 배치컴파일 **`error CS=0`** (50s), Systems.dll `GetComponentInParent` 심볼 포함
- ★배치 시 `pkill Unity.Licensing.Client` 금지(라이선스 채널 끊겨 시작 안 됨 — 14차 기록)

### Play 판정 대기
- GLB 몬스터 좌클릭 시 체력바 감소 + 사망 드랍. (메인씬 GLB 몬스터/병사 공격도 함께 정상화 기대)

---

## 📌 세션 종합 스냅샷 (2026-09-10 14차 — Test_10 채집용 약초 배치 + 병사 명령 시스템 점검)

> **스코프**: Test_10 씬에 채집 가능한 약초 3종(Red/Purple/Green) 배치. 병사 명령 시스템 구현 현황 점검.

### 변경 사항
**`Systems/TestTerritoryCombatSetup.cs`** (기존 구성 무변경 + 약초 배치)
- `SetupHerbs()` + `SetupHerb(...)` 헬퍼 — Herb_Red/Purple/Green 각각 GLB(herb_red/purple/green) Instantiate(폴백 Sphere+URP/Lit 색) + `HerbPickup` 부착
- `_herbType` private SerializeField → **리플렉션**(`typeof(HerbPickup).GetField("_herbType", System.Reflection.BindingFlags.NonPublic|Instance)` + `SetValue`)으로 설정 (TestPlayerSetup 관례)
- 좌표 (3,~,20)/(−3,~,20)/(0,~,18), y=SurfaceY(x,z)+0.3, BoxCollider 부가

### 병사 명령 시스템 점검 결과 (⚠️)
- ✅ 구현됨: `RTSCommandSystem`(우클릭 공격/이동·Ctrl 일제·H정지), `GuardSelectionManager`(드래그 선택·우클릭/H키 중계), `GuardPlaceholder`(SetCommandTarget/ClearCommand/SetInCombat 상태 API)
- ❌ 미연결: 명령을 실제 이동/공격으로 수행하는 **`GuardCombatAI.UpdateGuardBehavior`가 어디에서도 호출되지 않음** + 그 함수 내부에도 실제 `transform.position` 이동 코드 없음 → **선택·명령 저장까지만 되고 병사가 움직이지 않는 골격 상태**. (수행 루프 연결 + 실제 이동/추종 구현 필요)

### 검증
- `QaValidator.RunAllChecks` 배치컴파일 **`error CS=0`**
- `strings Systems.dll | grep SetupHerbs` → 히트 (컴파일 반영 확정)
- ★함정: 배치 전 `pkill Unity.Licensing.Client` 하면 활성 라이선스 채널이 끊겨 Unity가 즉시 종료(시작 안 됨) → **배치 시 라이선스 킬 금지**

### Play 판정 대기
- 약초 E키 채집 → LootBasket 생성 + 인벤토리/EXP + 리스폰 30초 / 내(파랑)·적(빨강) 진영 + 병사 E키 상호작용.

---

## 📌 세션 종합 스냅샷 (2026-09-10 13차 — Test_10 양 진영 영지/병사 배치)

> **스코프**: Test_10 씬에 내 소속 영지(PlayerOwned)+내 병사 3명 / 적 소속 영지(LordOwned)+적 문지기 3명 배치. 기존 타영주/병사1/몬스터는 유지.

### 변경 사항
**`Systems/TestTerritoryCombatSetup.cs`** (기존 구성 무변경, 신규 4메서드 추가)
- `SetupTerritoriesAndGuards()` 훅 — Awake의 AttachAttackSystem() 직후 호출
- `SetupMyTerritory()` — `SetOwnership(East,1,PlayerOwned)` + 파란 성(Cube 10,8,10) + 내병사 3(Lv10/East/SetRecruited(true)/파랑) @ (0,0,25) 전면
- `SetupEnemyTerritory()` — `SetOwnership(North,1,LordOwned)` + 빨간 성 + 적문지기 3(Lv15/North/미포섭/빨강) @ (0,0,-25) 성문(-z) 앞
- `CreateGuard(...)` 공용 헬퍼 — 기존 SpawnGuard 패턴 파라미터화

### 계약/주의
- `TerritoryOwnership` 열거형에 **`EnemyOwned` 값 없음** (Unoccupied/PlayerOwned/LordOwned/Contested만) → 적 영지는 `LordOwned`(적 AI 영주 소유)로 등록, GO 이름만 EnemyOwned 표기 유지
- 모든 y = `SurfaceY(x,z)+보정` (GetHeightAt+1 계약), 성은 순수 시각(Collider 없음), 기존 배치와 4m+ 이격

### 검증
- `QaValidator.RunAllChecks` 배치컴파일 **`error CS=0`**
- `strings Systems.dll | grep SetupTerritoriesAndGuards` → 히트 (컴파일 반영 확정)

### Play 판정 대기
- 내(파랑)/적(빨강) 성+병사 진영 표시 + E키 상호작용(포섭/음식/약 등) 동작.

---

## 📌 세션 종합 스냅샷 (2026-09-10 12차 — GLB 아이템 아이콘 시스템)

> **스코프**: GLB 모델이 존재하는 아이템을 인벤토리·크래프트 창에서 그 GLB 렌더링 아이콘으로 표시. GLB 없으면 기존 절차 아이콘 폴백.

### 변경 사항
**1. 신규 `UI/GblItemIconRenderer.cs` (MonoBehaviour 싱글턴)**
- 아이템 id → GLB 모델키 해석(`_itemToModel` 명시맵 16종 무기: `weapon_{type}_{metal}`→`{metal}_{type}`, 관례 후보=id 그대로/접두사 제거, `RuntimeModelLoader.HasModel`로 검증).
- 원격 위치(10000,1000,10000)+전용카메라+RT 128² → `ReadPixels`로 Texture2D 베이크 → `item.id` 기준 캐시(최대 200, 초과 시 새 베이크 중단). 베이크 큐는 Update에서 프레임당 1개 순차.
- 무기 16종·약초(herb_red/purple/green 등 id와 GLB명 일치)·푸드에 적용, GLB 없으면 절차 폴백.

**2. `UI/ItemIconDatabase.cs`** — `GetOrCreateIcon` 선두에 `GblItemIconRenderer.GetOrCreateIcon` 우선 훅 추가. 인벤토리/퀵슬롯/상점/루트가 자동 수혜.

**3. `UI/CraftingUI.cs`** — `DrawInventoryItemSlot` 색상 사각형 아이콘 → `ItemIconDatabase` 아이콘(폴백 유지), `DrawMaterialSlot` 재료 슬롯에도 아이콘 추가.

### 검증
- `QaValidator.RunAllChecks` 배치컴파일 `error CS=0` (1건 `TextureWrapMode.ClampToEdge`→`Clamp` 수정 — 이 엔진 유효값).
- `strings ProjectName.UI.dll | grep GblItemIconRenderer` → 4 히트 (컴파일 반영 확정).

### Play 판정 대기
- 무기/약초/음식 GLB 아이콘이 인벤토리·크래프트 창에 표시 + 절차 폴백 정상.

---

## 📌 세션 종합 스냅샷 (2026-09-09 11차 — 전투 액션감 고사양화 2차: 파티클 3겹+임팩트 사운드+카메라 튜닝 ✅)

> **스코프**: 1차(10차)에 이어, 감각 피드백을 한층 더 고사양으로 강화. 모든 증강은 `ActionFeel.HighSpec` 게이트 뒤 — 메인(Balanced) 동작 불변.

### 변경 사항
**1. 파티클 3겹 재설계 (`Systems/CombatVFXController.cs` 보강)**
- `SpawnHitDebris(position, dir, isCrit)` 신규 — **갈색/회색 파편 조각** 8개(크리 14) 저속을 지면 아래(Y-바이어스)로 + 밝은 스파크 잔흩 혼합. `EmitParams` 오버라이드.
- `SpawnCritBurst(position)` 신규 — 크리 전용 종합: ①16스파크 방사링 ②12파편 낙하 ③8큰 붉은 스플래시 3겹 일괄.
- HighSpec 스파크/출혈 증폭: 스파크 10→20(+위쪽 바이어스), 출혈 3→8(+방향 노이즈). 기존 저사양 개수는 Balanced 경로 그대로.
- 두 신규 메서드는 `if(!ActionFeel.HighSpec) return;` 자체 게이트 — 호출자 무변경.

**2. 임팩트 사운드 레이어 (`Systems/ImpactSoundFX.cs` 신규)**
- `PlayHit(isCrit, isKill)` — `SoundManagerEnhanced.Instance.PlaySFX(id, vol)` 널세이프 다층 재생.
- 등급별 레이어: 처치=`impact_kill`1.0+`impact_crit`0.55+`impact_rattle`0.45(3겹) / 크리=`impact_crit`0.9+`impact_thud`0.5+`impact_rattle`0.4 / 일반=`impact_thud`0.8+`impact_rattle`0.35(2겹).
- HighSpec만 발화(Balanced는 완전 무음). 클립 미보유 시 PlaySFX가 플레이스홀더 로그 후 **안전 반환(크래시 없음)** — 영속 검증.

**3. 카메라/타임스케일 하이스펙 튜닝 (`Systems/CombatCameraEffects.cs`)**
- `_hitShakeIntensity` 1.3배 강화 / HitStop 타임스케일 0.5→0.35(더 깊은 정지)+지속 1.15배 / 킬 슬로우모션 0.5→0.4+1.15배.
- 전부 `if(ActionFeel.HighSpec)` 가산 분기 — Balanced 시리얼라이즈드 값 그대로(영향 0). 공개 시그니처(PlayHit/PlayCrit/PlayKill/PlayHitShake) 불변.

**4. 중앙 게이트 최종 통합 (`Systems/CombatFXGate.cs`)**
- HighSpec 블록에 `SpawnHitDebris`(전 타격) + 크리 시 `SpawnCritBurst` + `ImpactSoundFX.PlayHit`(isKill:false) 연결. 기존 링/플래시 유지.

### 검증
- **실제 Unity 배치컴파일**(WSL에서 직접 Unity.exe 호출): CompileScripts 25339ms, **error CS = 0**, "Exiting batchmode successfully".
- ScriptAssemblies DLL(22:16 재빌드) strings grep — ImpactSoundFX 신규 3회, 기존 7개 심볼 전부 반영.
- `ImpactSoundFX.cs.meta` 자동생성.
- 커밋 66e5ac1e·5c79e5b4 **push 완료**(1·2차 통합).

### Play 판정 대기 (Test_10)
- 타격 시: 파편 조각 3겹(흙날림)+스파크 잔흩 / 크리 시: 스파크링+파편+붉은 스플래시 종합 버스트 / 임팩트 사운드(일반 퉁·크리 강한 찰칵·처치 깊은 둔탁) / 히트스톱 더 깊고 오래 + 셰이크 강화.
- **사운드**: `Resources/Sounds/SFX/` 비어 있어 현재는 플레이스홀더 무음 — 발성 감각 확인은 이후 clip 에셋 추가 시 유효.

---

## 📌 세션 종합 스냅샷 (2026-09-09 10차 — 전투 액션감 고사양화 1차: ActionFeel+충격파링+플래시+킨매틱 넉백 ✅)

> **스코프**: 테스트 씬(Test_10) 한정으로 감각 피드백을 고사양 강화. 코드는 공유라 메인도 동일 동작하되, `ActionFeel.Mode` 관통 토글로 Balanced(메인 기본)·HighSpec(테스트) 분리.

### 변경 사항
**1. `ActionFeel` 게이트 (`Systems/ActionFeel.cs` 신규)**
- `enum ActionFeelMode { Balanced, HighSpec }` + static 클래스. `ActionFeel.HighSpec` 읽기 게이트, `SetMode()` 로깅. 저사양(Balanced)이 기본값(메인 보호).

**2. 지면 충격파 링 (`Systems/ShockwaveRingFX.cs` 신규)**
- `Spawn(worldPos, maxRadius, color, duration)` — 평탄 Cylinder 메시를 ease-out으로 0.2→maxRadius 확장+알파 페이드, Object.Destroy 자원해제, 투명 Lit 메터리얼. 임펙트·크리 땅울림.

**3. 전화면 컬러 플래시 (`Systems/ScreenFlashFX.cs` 신규)**
- `Flash(color, intensity, duration)` + White/Orange/Red 래퍼(HitColor·CritColor·KillColor). IMGUI 폴스크린 박스 알파 페이드. **단일 인스턴스**(연타 시 덮어씀, 스택 방지).

**4. 킨매틱 넉백 (`Systems/HitReaction.cs` 보강)**
- **허점 수정**: 기존 AddForce는 `!_rigidbody.isKinematic`일 때만 → 몬스터/병사/영주(절차 애니메이션 kinematic)에선 넉백 무시됨. 추가된 `KinematicJolt` 코루틴이 **Rigidbody 상태와 무관하게 Transform 짧은 플린치(0.06s 밀기→0.12s settle, y바이어스)**.
- `ActionFeel.HighSpec` 시 절트 거리 2배 + 위로 홉(y+0.4). 기존 non-kinematic AddForce 경로는 유지(양쪽 일관).

**5. 고사양 분기 통합 (`Systems/CombatFXGate.cs`)**
- `PlayHitFXInternal`에 `if (ActionFeel.HighSpec)` 블록 추가: 크리/중타(damage≥40)만 충격파링(크리=주황 1.5m/0.5s, 중타=흰 1.0m/0.35s) + 크리=주황 플래시 0.3/0.2s, 일반=극미량 흰 0.05s. 기존 1~4단계(스파크/출혈/넘버/카메라)는 무영향.

**6. 테스트 씬 활성화 (`Systems/TestTerritoryCombatSetup.cs`)**
- `Awake()` 첫 줄 `ActionFeel.SetMode(ActionFeelMode.HighSpec)` — Test_10_TerritoryCombat 전용. 메인 씬은 Balanced 기본 유지.

### 검증
- **실제 Unity 배치컴파일** `run_batch.bat`(6000.4.10f1): CompileScripts 21155ms, **error CS = 0**, "Exiting batchmode successfully return code 0".
- ScriptAssemblies DLL strings grep — `ActionFeel/ShockwaveRingFX/ScreenFlashFX/HitReaction/CombatFXGate` 심볼 전부 반영(3~4회 매치).
- `.cs.meta` 3종 자동생성 확인.
- 커밋 e0d4e251 push 완료.

### Play 판정 대기 (Test_10)
- 좌클릭 공격: 피격체가 짧게 밀려나는 플린치(monster/guard/lord 모두) / 크리·중타 지면 충격파링(주황) / 크리 전화면 주황 섬광 / 일반 타격 극미량 백색 플래시 / 대형 체감 확인.

---

## 📌 세션 종합 스냅샷 (2026-09-09 9차 — 전투 이펙트 고품격화: CombatFXGate + 공격/피격 VFX ✅)

### 변경 사항
**Phase 1 — 중앙 이펙트 게이트 `CombatFXGate`** (`Systems/CombatFXGate.cs` 신규)
- 기존 VFX 3개(CombatVFXController/CombatCameraEffects/HitVFX)를 단일 API로 오케스트레이션.
- `PlayHitFX(position|target, dir, type, isCrit, damage, numberColor)` — 스파크+유기체 출혈+데미지넘버+히트플래시+카메라(크리2×셰이크).
- `CombatHitType` enum(Organic/Construct/None), **저사양 예산 캡(초당 12회)**.
- 사망(PlayKill)은 호출자 위임.

**Phase 2 — 플레이어 공격 보강** (`Systems/PlayerCombat.cs`)
- `AttackTarget`에 게이트 연결: 백어택(isCrit) 감지 → `PlayHitFX(Organic, crit색 주황/일반 흰색)`.
- 중복 HitVFX 블록 제거(게이트가 스파크/플래시/숫자/출혈 일괄 처리). 기존 카메라/로그/SFX 유지.

**Phase 3 — 영주/병사 피격 대칭화**
- `DraculaLord.cs` — `TakeDamage`에 `PlayHitFX(Organic, 흰색)` + `Die()`에 `PlayKill`, `Systems/GuardPlaceholder.cs` 동일.

**운용 수정:**
- 에디터가 저사양에서 파일 감시 멈춤(응답정지) → **강제 종료 후 BATCH MODE 컴파일**로 전환. 에디터 닫혀 있으면 배치 락 없음.
- `run_batch_phase*` gitignore 추가(빌드 헬퍼 추적 제외).

### 검증 ✅
- Phase 1: `CombatFXGate` 심볼 DLL 확인, error CS 0.
- Phase 2: `PlayerCombat` 게이트 연결, 배치 CS 0.
- Phase 3: `DraculaLord`/`GuardPlaceholder` VFX+PlayKill, 배치 CS 0.
- **최종 통합 배치: `ProjectName.Systems.dll` 20:56 재컴파일 + error CS 0.**

### Play 판정 대기 (Test_10 씬)
- 영지/병사/몬스터 각각 좌클릭 → 데미지 숫자+스파크+출혈+히트플래시+카메라셰이크.
- 백어택(뒤에서 공격) → 주황 데미지숫자 + 크리티컬 셰이크 2×.
- 사망 시 킬 슬로우모션.

---

## 📌 세션 종합 스냅샷 (2026-09-09 8차 — 경량 전투 테스트씬 + 공격시스템 점검용 ✅)

### 변경 사항
**신규 경량 전투 테스트 — `Test_10_TerritoryCombat`**
- `Systems/TestTerritoryCombatSetup.cs` — 저사양(8GB) 공격시스템 집중 점검용 최소 구성.
  - Player(AttackSystem 부착) + **타영주 영지(DraculaLord, IDamageable)** 1 + **병사(GuardPlaceholder, IDamageable)** 1 + **몬스터(slime)** 1
  - 좌클릭 → AttackSystem(Raycast→IDamageable→거리→데미지→사망 드랍) 단독 검증
  - 메인씬 확산 전 "공격시스템 통과 여부" 빠른 판정 목적
- `Scenes/TestScenes/Test_10_TerritoryCombat.unity` — YAML 씬, 스크립트 오브젝트 연결(GUID 6e49b159...)

### 검증 ✅
- 초기 컴파일 에러 CS0119(GetComponent/AddComponent에 Type 직접 사용) 2건 → `var t = typeof(X); GetComponent(t)/AddComponent(t)` 패턴으로 수정(TestCombatSetup와 동일).
- `ProjectName.Systems.dll` 18:50 재컴파일 + `TestTerritoryCombatSetup` 심볼 3건 확인.
- 최신 컴파일 세션 error CS = 0.

### Play 판정 대기
- Test_10 씬 Play → 영지·병사·몬스터 렌더 확인 → **좌클릭**으로 각각 공격 → 데미지/드랍 로그 확인 → 통과 시 메인씬 확산 기준으로 사용.

---

## 📌 세션 종합 스냅샷 (2026-09-09 7차 — 실내 크래프팅(장비·요리·물약)+창고 배치 ✅)

### 변경 사항
**Phase 1 — 플레이어 성 요리/연금 추가** (`Systems/PlayerCastleInteriorBuilder.cs`)
- 요리 카운터 `CookingTable` (+`CookingStation` 리플렉션) — mx*6.2, z=-6.3, `🍳 요리 테이블`
- 연금 테이블 `AlchemyTable` (+`AlchemyStation` 리플렉션) — mx*6.2, z=+5.0, `🧪 연금술 테이블`
- 8개 layout variant 전부에서 기둥(±3.5~4.5)/화로(±10)/작업대·장식탁자와 비겹침 (기존 장비 작업대 유지)

**Phase 2 — 크래프트하우스 올인원 워크숍** (`UI/CraftHouseInteriorBuilder.cs`)
- 요리 카운터(+`CookingStation`) — x=3.5, z=3.0, `🍳 요리 테이블`
- 연금 테이블(+`AlchemyStation`) — x=-3.5, z=-3.2, `🧪 연금술 테이블`
- 창고 선반 `CraftWarehouse` (+`TerritoryWarehouse.Configure("CraftHouse_01")`) — `📦 창고`
- 직접 `AddComponent<T>` 사용 (같은 UI asmdef) + 기존 장비 `CraftingStation` 유지

**Phase 3 — 창고 시각/라벨** (PlayerCastleInteriorBuilder, 기 구현 확인)
- 저장고(`territoryKey`, 🎒저장고) + 무기고(`territoryKey_armory`, ⚔️무기고) `TerritoryWarehouse` 이미 부착 확인 — 추가 변경 불필요

### 검증 ✅
- Phase 1: `ProjectName.Systems.dll` 17:57 재컴파일 + `CookingStationTypeName`/`AlchemyStationTypeName` 심볼 확인, error CS 0
- Phase 2: `ProjectName.UI.dll` 18:07 재컴파일 (수정 18:03 이후), error CS 0
- 전체 에디터 로그 error CS = 0

### Play 판정 대기 (F8 실내 진입으로)
- 플레이어 성: F8 → 장비 작업대/요리 테이블/연금 테이블 E키 → 각 UI 열림 확인
- 크래프트하우스: 진입 → 3종 스테이션 + 창고 상호작용 확인
- 창고: 저장고/무기고/크래프트창고 → 아이템 입출고·Transfer → 인벤 반영
- F8 재입력 → 월드 복귀

---

## 📌 세션 종합 스냅샷 (2026-09-09 6차 — 실내 검증 디버그 핫키 F8 ✅)

### 변경 사항
- **신규 `Assets/Scripts/UI/IndoorDebugEnterExit.cs`** — DEBUG-ONLY 런타임 핫키.
  - Play 모드에서 **성 게이트까지 걸어가지 않고** 실내 전환(IndoorScene)을 즉시 검증.
  - `[RuntimeInitializeOnLoadMethod]` + `DontDestroyOnLoad` 자가 부트 → 씬 편집/수동 배치 불필요(EquipmentStatBonusApplier/HotbarUI 선례).
  - **F8 첫 입력(미로드):** `IndoorSceneTransition.EnterBuilding("castle","Empire",true,null)` → 실제 플레이어 성 진입 경로와 동일(원점 스폰/카메라 스왑/하이어라키 이동/셸·fixture).
  - **F8 재입력(로드됨):** `IndoorSceneTransition.ExitBuilding()` → 월드 복귀. 상태는 `IsIndoorSceneLoaded()`로 토글.
  - `try-catch` 격리(빌더 NRE가 Play 세션 전체를 크래시 못 하도록 — 호수 NRE 이력 반영).

### 검증 ✅
- Unity 에디터 실행 중(배치 락) → 에디터 자동 재컴파일로 판정.
- `Library/ScriptAssemblies/ProjectName.UI.dll` 16:54 갱신 + `strings`로 `IndoorDebugEnterExit` 심볼 확인.
- 에디터 로그 16:41 이후 **error CS 0건**. → CS=0 ✅

### Play 판정 대기
- Play → F8 → 플레이어 보임+셸+카메라 추적+계층 IndoorScene / F8 재입력 → 월드 복귀.

---

## 📌 세션 종합 스냅샷 (2026-09-09 5차 — 내부씬 중세 기본 셸 상주화 + 실내 카메라 ✅)

- **범위**: IndoorScene이 빈 루트 1개뿐(빌더 런타임 생성 방식) → **중세 기본 셸을 씬에 영구 상주** + 실내 카메라 부착. 계획서: 2026-09-09_interior-shell-camera-plan.md
- **신규 MedievalShellBuilder.cs**: 절차 텍스처 4종(돌판 바닥/석벽 벽돌/회반죽/목재) + CreateShell(바닥 12x9 + 벽 4면 h4[하단 석벽+상단 회반죽+상단 목재 빔] + 남측 문틈 + 서까래 3 + 횃불 3[포인트라이트 오렌지 2.2/범위 8/소프트 섀도우]) + ShellTorchFlicker(강도 진동) — 씬 저장/런타임 겸용
- **실내 카메라**: IndoorScene에 IndoorCamera 상주(쿼터뷰 45°/FOV50, 기본 비활성) — **IndoorSceneTransition 진입 시 메인 카메라 비활성+IndoorCamera 활성, Exit 역스왑**
- **에디터/배치**: Tools/Indoor/중세 기본 셸 생성+저장 메뉴 + build_shell.bat(-executeMethod IndoorShellMenu.RunForBatch) — 배치 실행으로 **IndoorScene.unity에 43개 오브젝트 영구 저장 완료**(셸/바닥/벽/카메라 확인)
- **트러블**: 기존 CaveInteriorBuilder의 TorchFlicker 클래스와 중복(CS0101) → ShellTorchFlicker로 개명
- **검증**: 배치컴파일 error CS=0 ×2(개명 후 포함) + 씬 저장 확인
- ⬜ **남음**: Play 판정(①씬 뷰에서 셸 보임 ②성문 E → 실내 카메라 스왑+중세 톤 렌더 ③횃불 깜빡임 ④Exit 복귀) — 빌더 8종 가구 재정렬(셸 표준화)은 차기

---

## 📌 세션 종합 스냅샷 (2026-09-09 4차 — 스탯창 NRE/삼분활 후속 + 시선끊김 + 내부씬 렌더 ✅)

- **스탯창 안 뜸 루트원인 확정**(유저 로그 559행): 뷰포트 오브젝트에 Image 존재 상태에서 RawImage AddComponent → 유니티 거부 null 반환 → RawImage 전용 GO 생성으로 수정. 뷰포트 검은화면 = RT 카메라 pitch 8° vs 클론 34° 하방 위치 미스 → LookAt 수정
- **스탯창 UI 정비**(63.PNG): 텍스트 Wrap+축약(패널 밖 넘침 해소), 장비슬롯 라벨 간격 확대, WinH 900→760(퀵슬롯 비겹침)
- **인벤**: 설명창 세로 620으로 축소, **미니패드 폐지 → 드래그를 하단 상시 핫바에 직접 드롭**(HotbarUI.GetSlotIndexAtScreenPoint — 오버레이 캔버스 RectangleContainsScreenPoint)
- **시선 끊김 원인 확정**: ①카메라 드라이버 3중 경쟁(PlayerMovement 내장/ZoomController/CM-Binder — 전부 LateUpdate) ②**커서 조준+카메라 에지팬 이중 반응 루프**(커서가 데드존 밖이면 카메라 90°/s 팬 → 커서 지면점 이동 → 플레이어 재회전 무한). 수정: 에지팬 비활성(CameraEdgePan=false, 재활용 가능)+카메라 위치/시선 평활(Lerp 14/s)
- **내부 씬 렌더 실패 원인 확정**: 실내가 본 지형(y≈42) 아래 y=0에 생성되고 **카메라 클램프가 카메라를 지표 위로 강제** → 지형 표면만 보임 → IndoorScene 활성 시 클램프 스킵(지형은 아래에서 보면 컬링됨)
- **Unknown 스크립트 7건**: MainScene m_Script guid 전수 대조 → 7개 MonoBehaviour 블록+컴포넌트 참조 제거, 잔존 0
- **BuildingTrigger 카메라 경고**: Start 1회 체크 → Update 재시도(카메라는 GameSetup.Start에서 런타임 생성)
- **TerrainBaseMapFixer 재발**: 에디터 로드 초기 AssetDB 미준비 오탐 → isCompiling/isUpdating 게이트
- **검증**: 에디터 실행 중으로 배치컴파일 보류 — 포커스 시 자동컴파일(콘솔 error CS 확인 필요), 중괄호 균형/전수 검증 완료
- ⬜ **남음**: Play 판정(①P키 스탯창: 뷰포트 캐릭터 렌더+텍스트 정렬 ②인벤 설명창 축소+핫바 직접 드래그 ③시선 부드러움 ④성문 E → 실내 렌더 ⑤경고 3종 소멸)

---

## 📌 세션 종합 스냅샷 (2026-09-09 3차 — 인벤 예시2/3 재구조화 + 은신애니 + 핫바 지정 ✅)

- **범위**: 유저 요구 9건 — 핫바 글자 제거, 은신 애니 미발동, 인벤토리 예시2/3 재구조화(퀘스트탭·무기버튼·아바타·하단상세 제거 → [인벤][설명][컨텍스트]), 우클릭 장착, 설명창 핫바패드 드래그 지정, 전리품/창고/상점 우측패널, 스탯창 미표시. 계획서: 2026-09-09_inventory-rework-stealth-status-plan.md
- **P0**: ①핫바 라벨(검/활/창/폭) 제거 ②은신: **Sneaky 상태가 고아**(전이 0개)가 원인 → HumanoidClipDriver에 IsStealthed 피드 추가 + 컨트롤러에 IsStealthed 파라미터/AnyState→Sneaky(최우선)/Sneaky→Default 전이 추가(YAML 수술, Backup/ 백업) ③스탯창 미표시: Awake try-catch+단계 로그, Open() 패널 null시 재빌드 자가복구
- **P2 인벤 재구조화** (InventoryWindow): WINDOW 1180→820, **퀘스트탭/무기버튼섹션/캐릭터3D프리뷰/하단상세 제거**, 하단 장비슬롯6(우클릭 해제), **중앙 설명 패널**(이름/등급/아이콘/설명/효과 + 핫바 미니패드 1~8), **우클릭 장착**(무기=_weaponIdMap→WeaponEquipManager.Equip / 방어구=MapArmorSlot→EquipmentManager.EquipItem), **드래그→패드 드롭→HotbarUI.AssignItem**(PlayerPrefs 저장, 무기류 장착 연동)
- **P3 배선**: 창고 상호작용(TerritoryWarehouse.OpenWarehouseUI) → InventoryWindow.SetContextMode(Warehouse)+인벤 동시 오픈. **전리품 상자는 기존 존재 확인**(AnimalAI.Die/GuardPlaceholder.Die → LootBasket.Create+DropTable.ApplyToBasket → E → LootWindow) — 신규 작성 불필요. **ShopWindow 1440x1170→520x940 축소+우측 배치 완료**(GetContextX: 인벤 열림 시 [인벤][설명] 우측, 아니면 화면 우측; ShopPlaceholder.ToggleShop에서 SetContextMode(Shop)+인벤 동시 오픈)
- **검증**: 배치컴파일 error CS=0(중간 1회 CS1503 Sprite→Texture 수정), DLL 심볼 확인(AssignItem/DrawDescriptionPanel/SetContextMode), IsStealthed 3건(파라미터+전이2)
- **후속(1072bba6)**: ①스탯창 NRE 루트원인 확정 — 뷰포트 오브젝트에 Image 존재 상태에서 RawImage AddComponent → 유니티 거부 null 반환(진단 로깅이 즉시 특정) → RawImage 전용 오브젝트 생성으로 수정 ②

---

## 📌 세션 종합 스냅샷 (2026-09-14 ✅ 48차 — 공격 액션(모션) 살리기: 런지 무게감 + 페이스 타깃 + 리코일 + 연타 펀치 + 콤보 버퍼)

> **스코프**: VFX(이펙트)가 아니라 **공격자 본체의 몸동작**을 개선. 전진 런지의 무게감, 공격 진입 타겟 회전 정렬, 타격 성공 후방 반동, 연타 콤보의 한-호흡 연속성. VFX는 46·47차 에셋 기반 그대로. 사운드는 기존 attack_swing/attack_hit 재활용(신규 없음). 3파일 수정.

### 변경 사항
**`Systems/PlayerCombat.cs` (+151행)**: 
- **런지 무게감** — AttackLungeCoroutine 재작성: 3프레임(≈0.05s) 임팩트 동기화 지연 + 무기별 거리 테이블(검0.6/창1.2/활0.5/권0.4m) + 전반40% ease-in→후반60% ease-out 곡선(40% 경계 속도 연속) + 타겟(LastHitPoint) 방향 우선·전방 폴백·y 제거 정규화 + 지연 후 위치 캡처로 리코일과 자연 인계(스냅 방지).
- **페이스 타깃** — StartFaceTarget/FaceTargetRoutine: 공격 진입 순간 타겟 xz 방향 Slerp(속도15·최대10프레임·<1° 조기종료). auto-aim 타겟 + 근접 스윕 폴백 타겟 모두 적용. 타겟 없으면 호출 안 함(기존 방향 유지). CursorTurn과 충돌 최소화.
- **공격자 리코일** — AttackTarget 성공 분기 RecoilCoroutine(-hitDirection, 일반0.15m/백어택·치명타0.25m, 0.05s smoothstep t²(3-2t)) — 타격 방향 반대 후방 반동.
- **연타 카메라 펀치 차등** — _attackStreak 카운터(0.6s 윈도우, 최대3, _lastAttackTime 갱신 전 판정) → TriggerCameraEffects 이펙트 펀치 0.4/0.55/0.7차등. HumanoidClipDriver 콤보와 독립(파일 내 자체 카운터).
- **FIX(48차 후속)** — _recoilActive 플래그 게이트: 런지가 리코일 완료까지 while 크기 반복 대기(히트스톱 timeScale 0.08에서도 동일 인계), 리코일 조기종료 경로 포함 모든 종료에서 해제 → 공동 transform 쓰기/데드락 방지.

**`Systems/HumanoidClipDriver.cs` (+55·-15행)**:
- **콤보 버퍼링** — ComboBufferWindow=0.12s 1슬롯 클릭 버퍼(_comboBufferedClick/_comboBufferEndTime): 스윙 중 클릭 → 버퍼 적립 → 경계 도달 프레임 즉시 소비해 홀드 없이 다음 스테이지 연결(한 호흡 연속 몸동작). 경계 홀드 대기 중 클릭은 기존대로 즉시 진행. 인터럽트/신규 시작/재시작/EndCombo/3타 클립 끝 리셋 6경로 전수.
- **리커버리 감소** — ComboHoldGrace 0.25→0.18, ComboExitBlend 0.15→0.10 (EndCombo 전용).

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(1차 변경 + FIX 통합 후 총 2회 통과, exit 0)
- 정적 QA(서브에이전트): 6포인트 — ①런지/리코일 동시성 ⚠️→FIX ②streak 순서 ✅ ③HitStop 순서·timeScale ✅(anticipation 붕괴 우려 1건 Play 확인) ④콤보 버퍼 리셋 6경로 전수 ✅ ⑤FaceTarget vs CursorTurn ⚠️(고프레임 144fps 미정렬 리스크 — 권장 개선) ⑥문법/규약 ✅. 괄호 균형 0/0, 미사용 0, CI(한국어+2026-09-14 태그) 준수.
- Play 판정 대기: 전진 찌르는 무게감 / 타겟 정렬 회전 / 타격 반동 / 3연타 한 호흡 / 카메라 펀치 단계 차등.

### 비고/후속 제안
- Play에서 히트스톱 중 예비동작(anticipation)이 사실상 붕괴되는지(미스에서만 온전히 작동) 체감 확인 — 히트스톱 자체가 임팩트를 주므로 치명적이진 않으나 의도한 스윙 타이밍 지연은 성공 타격에서 축소됨.
- TriggerCameraEffects가 성공 타격 시 2회(AttackTarget+TryAttack) 호출되어 streak 펀치가 실효 2배(0.8/1.1/1.4), 미스는 1회 → 히트/미스 강도 불일치. 추후 단일화 권장.
- FaceTargetRoutine 프레임 수 캡(fps 의존) → 시간 기반(≤0.15s) 또는 RotateTowards(최대각속도) 방식 전환 권장, 특히 근접 스윕 폴백(후방 대상)+고프레임 환경.

---

## 📌 세션 종합 스냅샷 (2026-09-14 ✅ 49차 — 몬스터 절차 애니메이션 구현: 4족 AI 속도 피드 + 종별 보행 프로필 + 특수형 몸놀림)

> **스코프**: 사용자 요청 "몬스터 애니메이션 제작(옵션1=절차 커스터마이즈)". 기존 절차 애니는 존재하나 **WASD 키보드 입력 전용이라 AI 구동 몬스터는 다리가 정지** → 근본 원인 수리 + 종별 프로필 + 특수형 전면 구현. FBX 클립 제작 아님(절차/코드). 5파일 수정.

### 변경 사항
**`Systems/QuadrupedProceduralAnimation.cs` (534→651행)**: `_aiDriven` 분기(플레이어 키보드 테스트 유지) + FixedUpdate에서 Rigidbody 이동 중복 제거 + SetAiDriven/SetMovementSpeed/ApplyMonsterProfile(11종 보행 프로필) + UpdateLegPhases가 gait 배율(GetGaitPhaseMultiplier) 적용 + gait 전환 시 SyncLegPhases로 렌더 위상 재정렬 + ApplySpineIK 제거(Locomotion 단일 담당 — 이중 누적 해소).
**`Systems/QuadrupedProceduralLocomotion.cs` (314→391행)**: GetCurrentGaitPhaseSpeed/GetGaitPhaseMultiplier/SyncLegPhases public + UpdateLegTarget 재작성(폐기 height → _stepHeight×sin 스윙 리프트 실사용, 발 공중 부양 방지) + _gaitOverride+SetGaitOverride(no-op 수리) + croc SetSpineWave 유효화.
**`Systems/AnimalAI.cs` (1311행)**: UpdateQuadrupedLink 지연 탐색(3s, ModelAnimatorAssigner 늦은 부착 대응) → SetAiDriven+ApplyMonsterProfile. 이동/정지 피드 FeedQuadrupedSpeed 삽입(모든 이동·공격·사망·리스폰 경로).
**`Systems/Animation/Procedural/SpecialCreatureAnimator.cs` (44→470행)**: CreatureType별 전면 구현 — Slime(0.9↔1.1 펄스+이동스쿼시), Spirit(+y 부유+_EmissionColor 펄스·인스턴스 보호), Clam(여닫이 1.0↔0.3), Spider(본 다리좌우교차), LargeMonster(기울임). **루트 델타 자체속도 산출(LateUpdate, transform 읽기만)** → AnimalAI 미호출 상태에서도 이동 피드 발동. SetMoving/SetMoveSpeed API 유지.
**`Systems/MonsterSpawner.cs`**: IsBiped specialIds에 forest_spirit/bat/crow/poison_snake 추가 → 숲정령 등 특수형 분류 복구. GetSpecialCreatureType 폴백 명시.

### 컴파일/검증
- Unity batchmode **error CS=0**(1차 + QA-FIX 2회 통과), 5파일 괄호 균형 0/0/0
- 정적 QA: A(360 이동 중복 ✔) B(SetMovementSpeed→다리 체인 ✔) C(지연탐색 ✔, 3s 후 재탐색 없음 — 주기 재시도 권장) D(gait 렌더 반영 ⚠→**QA-FIX로 해결**) E(이동피드 무호출 ⚠→**자체 산출로 해결**, forest_spirit 분류 ⚠→**수정**) F(문법 ✔, linearVelocity 통일)
- Play 판정 대기: 토끼 깡충/악어 기어감/슬라임 펄스/숲정령 부유/거미 다리 등 종별 모션 + 공격·피격 반응 + 트롤·오우거 등 2족 보행 확인.

### 비고/후속 제안
- 3초 타임아웃 후 재탐색 없음 → GLB 로딩이 3초 넘으면 영구 미적용(키보드 모드 잔존). 주기 재시도(0.5s 스로틀) 또는 ModelAnimatorAssigner 부착 콜백으로 역링크 권장.
- 2족(트롤/오우거/미노타우로스/그림자암살자)은 ProceduralAnimationController 경로 — 별도 종별 프로필 필요(향후).
- LargeMonster 타입 미사용(giant_clam DB 없음), deep_clam 중복 매핑 — 기존 코드.

---

## 📌 세션 종합 스냅샷 (2026-09-14 ✅ 50차 — 2족 몬스터 절차 애니: AI 속도 피드 + 종별 보행 프로필)

> **스코프**: 49차(4족/특수형) 후속으로 2족 몬스터(트롤/오우거/미노타우로스/그림자암살자/밴시/돌골렘)에 종별 보행. ProceduralAnimationController(biped)의 IVelocityProvider 인터페이스로 AI 속도 피드 + 종별 walk/run 프로필. 2파일 수정.

### 변경 사항
**`Systems/AnimalAI.cs` (1311→1408행)**: `AnimalAI : MonoBehaviour, IDamageable, IAggroable, IVelocityProvider` 인터페이스 추가. IVelocityProvider 구현(CurrentVelocity=>_currentAIVelocity, CurrentSpeed=>_currentAISpeed, IsGrounded=>true). IsBipedMonster() 6종 판정 + UpdateBipedLink() 3초 지연 탐색 → SetVelocityProvider(this)+ApplyMonsterProfile. FeedQuadrupedSpeed 선두에서 실속도(이동방향×속도) 기록. (4족 경로 무변경).
**`Systems/Animation/Procedural/ProceduralAnimationController.cs` (1310→1342행)**: ApplyMonsterProfile(string monsterId) — wild_troll/ogre/stone_golem walk3/run6/accel12, minotaur 4/8/15, shadow_assassin/banshee 6/12/25, default 유지. walkSpeed/runSpeed/acceleration에 기록.
**`Systems/MonsterSpawner.cs` (QA-FIX)**: IsBiped quad구리스 배열에서 2족 5종(wild_troll/ogre/minotaur/banshee/shadow_assassin) 제거 → SpecialCreatureAnimator(Spider) 오부착 방지, 2족 절차 애니 단독 동작.

### 컴파일/검증
- 배치 컴파일: 초기 **error CS=3**(ApplyMonsterProfile의 Debug.Log가 user 네임스페이스 Debug 충돌 CS0234) → UnityEngine.Debug 정규화로 0. 이후 QA-FIX(IsBiped) 재컴파일 **error CS=0**.
- 정적 QA: A(인터페이스 단일 정의·시그니처 ✅) B(분류 체계 — isQuadruped 정합 ✅, IsBiped 불일치 ⚠️→FIX) C(**2족 속도 0 버그 없음** ✅ — FeedQuadrupedSpeed 가드 이전 무조건 기록, 이동 경로 전수 커버) D(보행 반영 ✅) E(문법 ✅).
- Play 판정 대기: 트롤 무거운 발걸음/오우거 저속/미노타우로스 돌진/암살자 민첩 보행/밴시 유영 + 2족-특수형 오부착 없음.

### 비고/후속 제안
- MonsterSpawner.IsBiped quad구리스 배열명이 실제 분류와 혼동 소지 — 향후 ID 기반 상수 테이블(4족/2족/특수형) 단일 소스로 이전 권장.
- 6종 GLB가 실제 휴머노이드 임포트여야 SetupBiped 경로(ProceduralAnimationController) 탑승 — 제네릭 릭이면 SetupQuadruped로 흐를 수 있어 Play에서 별도 확인 권장.
- ProceduralAnimationController는 [Obsolete] — 추후 Hybrid/Neural 전환 시 IVelocityProvider 연결 유지 필요.

---

## 📌 세션 종합 스냅샷 (2026-09-14 ✅ 51차 — 공격/몬스터 애니 무결성 재검증 + 리코일 방어)

> **스코프**: 사용자 "잘못된 건 없는지 한번 더 확인" — 48~50차 변경(플레이어 공격 모션 + 몬스터 절차 애니 4족/2족/특수형) 최종 무결성 재검증. 컴파일 재실행 + 정적 QA 2경로 병렬. 지적된 방어 결함 1건만 수리.

### 검증 결과
- **배치컴파일 error CS=0** (재실행), git diff 12파일/1311삽입 일관 확인.
- **공격모션 QA(48차×49·50차)**: A 코루틴 동시성 ✅(페이스=rotation / 리코일·런지=position 속성 분리 → 충돌 없음), B 49·50차 상호작용 ✅(몬스터 피드 미간섭), C 46·47차 게이트/슬래시 회귀 ✅(CombatFXGate 3오버로드·SlashVFXRunner 47차 최종 잔존), D HitStop 상호작용 ✅(리코일→런지 인계 히트스톱 중에도 성립), F 콤보 버퍼가 크로스 타이밍 무손상 ✅. **차단 버그 0건**. ⚠️ 유일 권고: RecoilCoroutine 플래그 비활성화/예외 시 런지 영구 스킵.
- **몬스터애니 배타성(직접 확인)**: UpdateQuadrupedLink(GetComponent 존재 시) vs UpdateBipedLink(IsBipedMonster 6종) — 4족이면 2족 스킵, 2족이면 4족 GetComponent null로 스킵. **동시 부착/구동 없음**. 특수형(49차 specialIds)과도 상호 배타.

### 51차 수정 (공격모션 QA 권고 1건)
**`Systems/PlayerCombat.cs` (701→719행)**: `_recoilActive` 플래그가 리코일 도중 비활성화/예외로 true 고정되면 모든 후속 런지가 영구 스킵되는 방어 — ① `OnEnable() { _recoilActive = false; }` 추가 ② `RecoilCoroutine` 루프를 try/finally로 감싸 플래그 해제를 finally로 이동(예외/StopCoroutine/비활성화에도 반드시 해제, finally 내 yield 없음 C# 제약 준수).

### 컴파일/검증
- 배치컴파일 error CS=0(총 2회 — 51차 포함), PlayerCombat 괄호 균형{69/69}.
- Play 판정 대기(기존과 동일): 몬스터 종별 모션(토끼 깡충/악어 기어감/슬라임 펄스/숲정령 부유/2족 보행) + 공격 전진/반동/콤보 연속성.

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 52차 — 공격 액션감 전면 개선 [Phase A 기반 + Phase B/C/D]: 스테이지 클립 콤보 + 피격 리액션 + 카메라 단일화)

> **스코프**: 사용자 리포트(테스트 17 영상) "3D RPG 대비 공격 액션 부족". 계획서 `docs/ATTACK_FEEL_UPGRADE_PLAN.md` 수립 후 Phase A(기반 커밋) → B/C/D 구현. 진단 뿌리: ① 콤보 3타가 **단일 클립 슬라이스**라 타별 개성 0 ② 피격자가 **전혀 반응 없음**(HP바만 감소) ③ 카메라 펀치 **이중 발화**로 히트/미스 강도 불일치.

### Phase A — 기반 커밋 (커밋 0908bad1)
직전 세션 미커밋분 15파일/+1411줄: 데미지 넘버 타입(Normal/Critical/BackAttack/Heal/Mana), WeaponSwingTrail 무기별 색/폭 차별화, CombatCameraEffects 무기별 임펄스 프로파일(_sword/_spear/_bow/fist), MonsterAggroSystem 어그로 시각화(붉은 오라+느낌표), ItemDragContext 드래그 스냅/하이라이트. 배치컴파일 error CS=0.

### Phase B — 스테이지 클립 콤보 (HumanoidClipDriver.cs)
**뿌리 확정**: `Player_AC.controller` 전이 전수 파싱(2026-09-15) — AnyState→AttackCombo/AttackCombo2/AttackCombo3 전이가 **트리거(동명) 조건으로 이미 존재**. 상태 클립 = Double_Combo_Attack / Triple_Combo_Attack / Weapon_Combo_2. 즉, 스테이지별 개성 클립이 배선돼 있었으나 B안(단일 테이크 슬라이스)이 이를 우회하고 있었음.
**수리**: `UseStageClips=true` 경로 신설 — 클릭 → `SetTrigger("AttackCombo"|"AttackCombo2"|"AttackCombo3")`로 스테이지 진행. 진행률 게이트(StageCancelGate=0.30) 통과 시 다음 타 즉시 소비(스윙 캔슬), 미달 시 0.12s 버퍼. 스테이지 클립은 컨트롤러 exit(≈0.92)로 자동 복귀하므로 홀드/만료 불필요.
신규 헬퍼: `IsInStageClip()` / `StartStageClip(int)` / `AdvanceStageClip(int)` / `MonitorStageClip()`. **기존 B안 코드 전량 보존**(플래그 false로 즉시 복귀). 스테이지별 FireComboSlash FX·트레일·콤보버퍼·Player모드 한정·Bow/Spear 차단 전부 유지.

### Phase C — 피격 리액션 (신규 HitReactionDriver.cs + CombatFXGate.cs 배선)
**뿌리**: 적 애니메이터(Monster_Animator/Soldier_Animator)에 Hit/Death 상태가 **전혀 없음**(상태=Attack/AttackTrigger/Base/Idle/Run/State/Walk뿐) → 컨트롤러 편집 없이 **절차 반응**으로 구현.
**신규**: `HitReactionDriver` + 러너 `HitReactionRunner` — ① 플린치: 시각 모델 자식 트랜스폼을 타격 반대방향으로 젖힘(smoothstep)+복원(경 0.16s/5°/0.05m, 중 0.24s/11°/0.14m, 크리 0.30s/16°/0.22m) ② 넉백: Rigidbody=AddForce(VelocityChange), CC=감쇠 이동(강타 0.9m/크리 1.5m) ③ Animator에 HitLight/Hit 트리거가 있으면(hasParameter 확인) 동반 발화 ④ **파괴는 일절 하지 않음**(사망 처리는 기존 Die() 담당 — 이중 파괴/전리품 타이밍 회귀 차단).
**배선**: CombatFXGate의 GameObject 오버로드 2곳에서 `PlayHitFlash` 직후 호출(플레이어 태그 제외). severity = 크리→Crit / 데미지≥15→Heavy / 그 외→Light. try-catch 격리(39차 교훈 — 연출 예외가 Die() 방해 금지).

### Phase D — 카메라 펀치 이중 발화 단일화 (PlayerCombat.cs)
무기별 히트스톱은 **이미 구현 확인**(HitStopManager.DurationOf — 검 .050/창 .060/활 .030/맨손 .040). 남은 48차 비고만 수리: TryAttack 말미의 무조건 `TriggerCameraEffects()` → `if (!hitAny) TriggerCameraEffects()`로 변경(적중 시에는 AttackTarget 내부가 발화). → 성공 타격의 실효 펀치 2배(0.8/1.1/1.4) 문제 해소, 히트/미스 강도 일치.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0** (Phase A 기준선 / Phase B·C·D 적용 후 총 2회, exit 0)
- HumanoidClipDriver 괄호 균형 157/157{ · 493/493(, diff 0
- **⚠️ 실행 방식**: B/C/D 서브에이전트 3병렬 위임 → **3건 모두 600s 타임아웃(파일 변경 0)** → 프로젝트 규칙(타임아웃 시 부모 직접)에 따라 부모가 직접 구현.
- Play 판정 대기: ① 1/2/3타가 서로 다른 모션(Double→Triple→Weapon_Combo_2) ② 타격 시 몬스터/병사가 젖혀짐+강타 넉백 ③ 크리 시 큰 리액션 ④ 카메라 펀치가 히트/미스 동일 강도·연타 차등 정상

### 비고/후속
- 적 사망 "다운 모션"(즉시 파괴 대체)은 미구현 — Die() 파괴 타이밍/전리품 회귀 리스크로 보류(Phase C-3 잔여).
- 계획서 Phase E(FX strike 동기)/F(사운드 4레이어)/G(넘버 juice)/H(회피롤·차지·패링) 미착수.
- 진단 #7(테스트17 무기 모델 미표시) 미확인 — Play `[Equip]` 로그로 판별 필요.

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 53차 — 테스트18 검증 + 느낌표 축소 + 플린치 상향 + 인벤 해상도 비례 + 무기 그립 피벗신뢰)

> **스코프**: 테스트 18 영상으로 52차(Phase B/C/D) 의도를 검증하고, 사용자 신규 4건(느낌표 과대 / 인벤창 해상도 취약 / 장비 착용 / 무기 정확 부착)을 수리. 계획서 `docs/ATTACK_FEEL_UPGRADE_PLAN.md`를 **Phase E 승계 + 신규 이슈 병합 통합 로드맵(F~L)**으로 재작성.

### 테스트 18 판정 (의도 대비)
- ✅ **일치**: 콤보 3단 개성화 — 1타 횡베기 → 2타 대각 하강 → 3타 회전/오버헤드, wind-up→strike→recovery, 전방 lean. (Phase B 성공)
- ❌ **불일치 ①**: 피격 flinch 여전히 미표시 — 원인 2건 확정: (a) 경타 강도 과소(5°/0.05m는 육안 불가) (b) 시각 자식 탐지 실패 시 적용 자체 스킵.
- ❌ **불일치 ②**: 느낌표가 적 신장의 1/3~1/2 (과대).
- ❌ **불일치 ③**: 인벤창 해상도 변경 시 슬롯/텍스트 클리핑·패널 밀림·오버레이 불균일.
- ⚠️ 무기 부착: 저해상 판독은 "부착"이나 사용자 육안은 "정확히 손에 없음" → bounds 그립부 휴리스틱이 블레이드 끝을 손잡이로 오판 가능성.

### 수정 (4파일 + 계획서)
**`Systems/MonsterAggroSystem.cs`**: 느낌표 `textMesh.characterSize = 0.30f`(기본 1.0 → 3.3배 축소) + fontSize 48(글리프 해상도용). 과대 리포트 수정.
**`Systems/HitReactionDriver.cs`**: 플린치 강도 상향(경 0.18s/9°/0.09m, 중 0.26s/16°/0.22m, 크리 0.34s/22°/0.32m) + 시각 자식 탐지 폴백 3단(Animator자식 → 이름힌트 → **렌더러 최다 자식**) + 탐지 실패 1회 로그(`[HitReaction] 시각 자식 탐지 실패 — 플린치 스킵`).
**`UI/InventoryWindow.cs` [Phase G-UI]**: `_uiScale = sqrt((Screen.width/1920)*(Screen.height/1080))`(HUD._canvasScale 동일 산식) 도입 → 레이아웃 상수 11종(TITLE_BAR/TAB_BAR/EQUIP_ROW/DESC_PANEL/PAGER_STRIP/SLOT_MARGIN/SLOT_ICON_SIZE/EQUIP_BADGE/… )을 `_uiScale` 배수 프로퍼티로 전환(사용처 무수정 자동 비례) + 로컬 상수 6종·fontSize 15곳 스케일 + WINDOW_WIDTH/HEIGHT 여백항 스케일 + 해상도 변경 감지 시 `_stylesInitialized=false`로 스타일(폰트) 재생성.
**`Systems/WeaponEquipManager.cs` [Phase H-GRIP]**: 그립부 결정에 **피벗 관례 우선** 규칙 추가 — GLB 피벗이 최장축 끝부(pivotT ≤0.15 또는 ≥0.85)면 그립부=피벗으로 간주해 bounds 오프셋 보정을 **건너뜀**(테이블 튜닝 포즈 그대로 = 손에 정확히). 타입별 `GripPose.GripEnd(±1)` 강제값 지원 + `AxisComponent` 헬퍼. 로그에 pivotT 표기(다음 튜닝 근거).

### 컴파일/검증
- ⚠️ **배치컴파일 불가**: Unity 에디터(PID 5540, 14:48 개방)가 프로젝트를 점유 → `Multiple Unity instances cannot open the same project` (exit 1). **에디터 포커스 시 자동 컴파일** / 에디터 종료 후 배치 재검증 필요.
- 정적 검증(대체): 수정 7파일 **괄호 균형 전부 0/0**, `const` 컨텍스트로 인한 컴파일 위반 **0건**, `_uiScale` 선언 1개·`RefreshUIScale` 1개·`_uiScaleUsedForStyles` 인스턴스 필드 확인, 로컬 상수 6종 스케일 변환 확인.

### 잔여 (통합 로드맵 F~L)
- G-2 타 UI창(Equipment/Loot/WorldMap/Warehouse) 동일 비례 적용 · G-3 드래그 DnD hit-test 검증 · G-4 비16:9 완화
- H-2~H-4 장비(방어구) 착용 실패 지점 판별·수리 + ArmorVisualAttachSystem 부착 확인 · H-5 그립 상수 튜닝
- E(FX strike 동기) · I(사운드 4레이어) · J(넘버 juice) · L(적 사망 다운) · K(회피롤·차지·패링)

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 54차 — 테스트19: 흰색고정 뿌리수리 + 무기 부착 수리 + 느낌표 추가축소 + Loot/Equipment 비례)

> **스코프**: 테스트 19 검증 → ① 몬스터 피격 시 흰색에서 안 돌아오는 문제 ② 무기 그립 미해결 ③ 느낌표 재과대 ④ 전리품/장비창 비례 미적용. 계획 F/G/H 잔여 진행.

### 테스트 19 판정
- ✅ **플린치/넉백 동작**(9~12, 21~23프레임 젖힘+밀림) — 53차 Phase C 수정 성공.
- ❌ **흰색 고정**(15, 21~24, 64~66프레임) — 뿌리 확정: `PlayHitFlash`가 `sharedMaterial.color`를 직접 흰색으로 바꾸고 **플래시별 색 스냅샷**으로 복원 → 공유 재질 다중 피격/연속 타격 시 스냅샷이 이미 흰색 → 복원해도 흰색 잔존(레이스). 대상 파괴 시 `r==null` 스킵으로 영구 잔존.
- ❌ **무기 모델 자체가 손에 없음**(빈손 — 슬래시 아크만) → 그립 오프셋 문제가 아니라 장착 자체 실패/비가시.
- ❌ 느낌표 여전히 적 신장 50~70%.

### 수정 (5파일)
**`Systems/CombatVFXController.cs` [Phase F-FLASH]**: 재질별 **refcount 레지스트리**(`_flashOrigColor`/`_flashRef`/`_flashOrigEmission`) 신설 — 최초 1회만 '진짜 원본' 색/이미션 기록 + 동시 플래시 카운트, **마지막 플래시 종료 시에만** 원본 복원. 재질 키 기반이라 대상이 파괴돼도 복원 성공. HitFlashRunner는 renderer 대신 Material 리스트 보유(스케일 아웃라인은 유지).
**`Systems/WeaponEquipManager.cs` [Phase H-GRIP2]**: ① Resources.Load 실패 시 원본 id 경로 폴백 ② 인스턴스에 **Rigidbody 제거(isKinematic+useGravity off→Destroy)/Collider 비활성/렌더러 강제 활성/로컬스케일 0 방어**(GLB 낙하=비가시 known trap) ③ **RightHand 이름 기반 폴백**(아바타 Generic/미매핑으로 GetBoneTransform null → 조용히 스킵되던 문제) ④ 인스턴스 진단 로그(렌더러 수/rb제거/collider/hand).
**`Systems/MonsterAggroSystem.cs`**: 느낌표 `localScale = 0.5` 추가(characterSize 0.30과 곱연산).
**`UI/LootWindow.cs` + `UI/EquipmentWindow.cs` [Phase G-UI 확산]**: InventoryWindow와 동일한 `_uiScale` 인프라 + const→배수 프로퍼티(Loot 3종/Equipment 6종) + fontSize 스케일(각 6/8곳) + 스타일 재생성.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(exit 0) — 에디터 종료 상태에서 실행 성공.
- 정적: 5파일 균형 0/0/0, `_cache` 잔존 0, const-context 위반 0.

### Play 판정 대기
① 피격 후 흰색이 0.15s 뒤 원래 색으로 복귀(연속 타격·다중 몬스터에서도 잔존 0) ② 무기 장착 시 손에 모델 표시(로그 `[Weapon] 인스턴스: ... 렌더러=N`) ③ 느낌표 크기 ④ 전리품/장비창 해상도 비례 ⑤ 장비 착용

### 잔여
E(FX strike 동기) · I(사운드) · J(넘버 juice) · L(사망 다운) · K(회피/차지/패링) · H-2 장비착용 확정검증 · G-3 DnD hit-test

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 55차 — 공격액션 업그레이드 잔여 Phase E/I/J/L/G-2/H + 사운드 통합)

> **스코프**: 중단됐던 공격액션 계획서 잔여 Phase 재개. E(FX strike 동기)·I(사운드 4레이어)·J-2/3(넘버 스택·가독성)·L(적 사망 다운)·G-2(월드맵/창고 비례)·H-2~4(장비 착용 계측) + 사운드 레이어 새 모듈.

### Phase E — FX strike 프레임 동기 ✅
- `HumanoidClipDriver.cs`: `StageStrikeSyncNormT=0.38f` + `_pendingStageArc` 신설. 스테이지 클립 경로(Start/AdvanceStageClip)에서 **트레일은 즉시 방출(FireComboTrail), 아크는 strike 프레임 도달 시 발화**하도록 분리. `MonitorStageClip`이 normT≥0.38 통과 순간 `_pendingStageArc` 소진·`FireComboSlash` 호출, 인터럽트 시 대기 아크 리셋.
- `SlashVFXRunner.cs`: `PlayImpactMulti(pos,type,scale)` 추가 — 기존 `PlayImpact`는 1x로 위임(시그니처 보존).
- `CombatFXGate.cs`: `PlayHitFXInternal`이 `isCrit`일 때 임팩트 **1.5배**(`PlayImpactMulti`).
- E-4(스테이지 틴트)는 Phase B에서 이미 구현, E-3(킬버스트)은 기존 CombatCameraEffects.PlayKill·ImpactSoundFX로 충족, E-5(프레임당≤10 캡)는 기존 TryConsumeBudget 유지.

### Phase I — 사운드 4레이어 ✅
- **신규 `Systems/AttackSoundLayerManager.cs`**: 스윙/임팩트/서브베이스/보이스 4 독립 AudioSource 레이어. `attack_swing{_weapon}`/`attack_hit{_weapon}` 재활용 + `attack_sub`(클립 없으면 절차적 70Hz 서브베이스 보장). `ProfileOf(type)` 무기별 피치·볼륨표 — 검(대검 둔탁)/창(금속 고피치)/활(시위 twang)/맨손(중간). 크리 시 임팩트 피치+8%·볼륨+20%.
- `PlayerCombat.cs`: `PlayWeaponSwingSound`/`PlayWeaponHitSound`가 AttackSoundLayerManager로 재배선(시그니처 보존, 히트스톱 이전 즉시 → I-3 선행 보장).

### Phase J-2/3 — 데미지 넘버 juice ✅
- `CombatVFXController.cs`: 위치 버킷(`_stackBuckets`) 기반 **연타 스택** — 0.5u 좌표 반올림·0.35s 창·슬롯8 초과 시 wrapping, 1번 히트 중앙→좌우 교대 대각(↗/↖·26/15px×스케일) 퍼짐. **가독성** — 8방향 흑색 외곽선(일반2·크리3 두께×스케일) + 그림자 알파 0.5→0.8·+2px 오프셋 추가. (DamageFont는 데드 경로로 미변경.)

### Phase L — 적 사망 다운 모션 ✅
- `AnimalAI.cs`: `Die()`가 즉시 파괴 대신 `StartDeathSequence` → 절차 눕힘(0.5s Slerp 90°)+sink(지면 0.4m)+**0.6~1.2s 지연 파괴** 후 리스폰. `_deathRoutineStarted` 재진입 가드(전리품 중복 드롭 방지), 콜라이더/어그로는 다운 시작 즉시 비활, 리스폰 시 회전 원복(`_spawnRot`).

### Phase G-2 — 타 UI창 해상도 비례 ✅
- `WorldMapWindow.cs`/`WarehouseUI.cs`: `_uiScale = sqrt((W/1920)*(H/1080))` + const→배수 프로퍼티(TITLE_H/HINT_H·슬롯·패딩·드롭다운) + `UIFont.Load()` 폰트·fontSize 스케일 + 해상도 변경 시 스타일 재생성(`_uiScaleUsedForStyles`).

### Phase H-2~4 — 장비(방어구) 착용 계측 ✅
- `EquipmentManager.cs`/`ArmorVisualAttachSystem.cs`/`InventoryWindow.cs`: 우클릭→`EquipItem`→`OnEquipmentChanged`, 드래그 장착, 드래그 해제 각 홉에 측정 로그 추가. 구조적 dead-end 없음 확인 — 매니저 발화→ArmorVisual 수신→부착/Detach 체인 측정 가능화. MapArmorSlot(헬멧/갑옷/신발/장갑/백·방패) 5종 정확.

### Phase K — 회피/차지/패링
- K-1(롤+무적)은 **기존 구현 확인**: ProceduralAnimationController Q키 `RequestRoll` + PlayerHealth C21-02 `IsRolling` 반사 무적 이미 존재 → 추가 작업 불필요.
- K-2(차지)/K-3(패링): 신규 클립 에셋(Roll_Dodge/Charged_Upward_Slash/Sword_Parry .anim) 미존재 확인 → **생략 판정**(에셋 확보 시 후속).

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0**(exit 0, 에디터 종료 상태 3회 실행 모두 정상).
- 변경 파일: AnimalAI/ArmorVisualAttachSystem/CombatFXGate/CombatVFXController/EquipmentManager/HumanoidClipDriver/PlayerCombat/SlashVFXRunner/InventoryWindow/WarehouseUI/WorldMapWindow(11) + 신규 AttackSoundLayerManager.cs.

### Play 판정 대기
① 콤보 타별 아크가 strike 프레임(0.38)에 동기 발화(즉시 아님) ② 크리 임팩트 1.5배 ③ 공격 사운드 4레이어·무기별 피치 ④ 연타 데미지 넘버가 좌우 대각으로 퍼짐+외곽선 판독 ⑤ 몬스터 사망 시 눕힘→지연 파괴 ⑥ 월드맵/창고 해상도 비례 ⑦ 장비 착용 로그 체인 ⑧ Q키 구르기 무적.

### 잔여
K-2(차지 강공·클립 확보 시) · K-3(패링·클립 확보 시) · H-2 Play 확정검증 · G-3 DnD hit-test · Play 눈검증(구르기/사운드/다운 모션).

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 56차 — 차지/패링 + 병사 공격·수비 배치 + 가시적 전쟁)

> **스코프**: 사장님 3건 — ① 차지/패링 절차 구현(애니 FBX는 나중에 교체 가능하게) ② 실내씬에서 점령 영지 병사 공격/수비 배치 ③ 전쟁이 콘솔 로그가 아니라 병사가 실제로 움직여 보이도록.

### 분석 결론
- 실제 전쟁 = TerritoryWarManager.ExecuteWar(숫자 공식+로그만), AIWarSystem.SpawnGarrison은 데드코드.
- 플레이어 영지 = TerritoryState.ownership == PlayerOwned.
- 병사 = GuardPlaceholder, 이동/공격 명령 SetCommandTarget(Vector3,bool)과 ExecuteMovement가 이미 내장 → 이를 재활용.

### Phase 1 — 차지/패링 ✅
- PlayerCombat.cs: 우클릭 홀드=차지(0.8s 충전 게이지)→강공 1타(데미지×1.8, 임팩트 1.5배). 좌클릭 직후 0.28s 패링 창. ChargedClipName/ParryClipName 공개 클립 플러그인(FBX 확보 시 클립명만 교체).
- ProceduralAnimStateMachine.cs: State.Charge/Parry + RequestCharge/RequestParry + 전이 + TriggerAction.
- ProceduralAnimationController.cs: TriggerAction charge/parry case + ActionState.Charge/Parry.
- PlayerHealth.cs: 근접(melee) 피격 시 리플렉션(GameManager 선례)으로 PlayerCombat.TryParry() 호출 — 패링 창 중 데미지/넉백 차단.

### Phase 2 — 병사 공격/수비 배치 ✅
- TerritoryData.cs: enum GarrisonRole {None,Attack,Defense} + TerritoryState._garrisonRole/_attackTargetId + 게터/세터.
- 신규 TerritoryDeploymentSystem.cs: DeployAttack(병사→SetCommandTarget 대상 영지)/DeployDefense(문 앞 수비)/Undeploy(ClearCommand).
- 신규 TerritoryDeploymentUI.cs(Assets/Scripts/UI/): 실내씬 IMGUI 패널 — 점령 영지별 병사+역할, 공격(대상 선택)/수비/해제 버튼.

### Phase 3 — 가시적 전쟁 ✅
- 신규 WarMarchSimulation.cs: TryStartMarch(동시 ≤2 캡) — 공격군 SpawnGarrison 2~5→SetCommandTarget으로 실제 걷기→HasCommand==false 도달→CombatFXGate 플린치/깃발→Release 파괴.
- TerritoryWarManager.cs: ExecuteWar 후 행진 훅(기존 수치/로그 결과 무손상).

### 컴파일/검증
- 배치컴파일 error CS=0. 변경 6 + 신규 3.
- TerritoryDeploymentUI 어셈블리(UIFont는 ProjectName.UI) → Assets/Scripts/UI/로 이동해 해결.

### Play 판정 대기
① 우클릭 홀드 강공 ② 좌클릭 직후 근접 패링 흡수 ③ 실내 배치 UI 공격/수비 ④ 배치 공격 병사 실제 걷기 ⑤ 전쟁 행진(공격군 목표 영지로) ⑥ 도달 시 전투 플린치.

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 56차 후속 — Test_10 병사 GLB 전환 + 공격 배치 추종 + 접지 수정)

> **스코프**: 사장님 3건 — ① Test_10 내/적 병사가 플레이스홀더로 보이던 것을 병사 GLB로 전환 ② 테스트씬 병사를 공격 배치로 적용해 플레이어 따라 함께 공격 ③ 병사 접지 문제 수정.

### 조사 결론
- 병사 GLB/FBX 에셋 모두 존재(Soldier_Lv1-20_Rigged.glb / fbx/soldier_lv1-20_rigged.fbx).
- Test_10 병사는 CreateGuard가 FBX 먼저→GLB 폴백(RuntimeModelLoader.TryGetModel) — 실동작에서 GLB 미부착 시 캡슐(플레이스홀더) 잔존("병사 GLB로 전환" 요구 원인).
- GuardCombatAI.NotifyPlayerAttack(포섭 병사가 플레이어 공격 대상에 합세)은 정의만 있고 호출부 無 = "나를 따라 공격" 미동작 근본 원인.
- 접지: 스폰 pos.y가 SurfaceY+1.0(박스 오프셋)인데 GroundModelToY가 발끝을 pos.y에 맞춰 모델이 지면에서 1m 떠 있음.

### 수정
**TestTerritoryCombatSetup.cs [Test_10 병사]**
- ① CreateGuard 모델 부착 재작성: FBX 먼저 → **GLB 우선**(프로덕션 GuardManager 검증 로더 `Resources.Load<GameObject>($"Models/UserProvided/{Soldier_Lv1-20_Rigged.glb}")`) → 실패 시 FBX Humanoid → 둘 다 실패 시에만 캡슐. 겹침/콜라이더 제거/접지/Animator/SoldierShield_AC/HumanoidClipDriver(Soldier) 유지.
- ③ 접지: GroundModelToY 모델 발끝을 `pos.y`(박스+1.0)가 아닌 **실제 지면 `SurfaceY(pos.x,pos.z)`**에 정렬(GLB·FBX 양 경로). Rigidbody useGravity=false + isKinematic(true)로 중력이 접지를 깨지 않게(CreateGuard+SpawnGuard 양쪽).
- ② 병사 태그: 모델 미부착 시에도 공격 배치 동작 가능하도록 `RecruitedSoldier`/`Guard` 태그.

**PlayerCombat.cs [동행 병사 합세 배선]**
- AttackTarget 공격 성공 직후 `GuardCombatAI.NotifyPlayerAttack(targetBehaviour.gameObject)` 호출 → 포섭(recruited) 내 병사들이 플레이어가 공격한 대상을 함께 공격(SetCommandTarget 공격 명령). Por섭 병사는 GuardCombatAI.UpdateGuardBehavior(기존 GuardPlaceholder.Update에서 자동 호출)로 플레이어 추종도 동작.

### 컴파일/검증
- 배치컴파일 error CS=0. 변경 2파일(TestTerritoryCombatSetup/PlayerCombat).
### Play 판정 대기
① Test_10 내병사 3명이 병사 GLB(창/방패)로 렌더되고 지면에 발 붙어 서 있음 ② 내병사가 플레이어를 따라다님(추종) ③ 플레이어가 적/몬스터 공격 시 내병사도 함께 접근해 공격 ④ 접지(발이 지면, 뜨/가라앉음 없음).

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 58차 — 타 영지 병사 적대화: 공격 시 호감도 하락 + transient 느낌표 + 플레이어/내병사 공격)

> **스코프**: 사장님 요구 — ① 내 공격 시 내 소속 병사도 공격(이미 배선) ② **타 영지 병사는 호감도에 따라 원래 공격 안 하다가, 내가 공격하는 순간 호감도 하락 → 몬스터처럼 느낌표 뜨며 플레이어/내 병사 공격** ③ 느낌표는 잠깐 뜨고 사라지게(계속 X).

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 64차 — 테스트24: RecruitedSoldier 태그 정의 + 활 좌클릭 발사(우클릭 차지 분리) + 무기 그립 과보정 클램프 + Ctrl 좌클릭 직접 드래그 판정)

> **스코프**: 테스트24(01:33) 피드백 + Editor.log(01:38) 실측 수리. ① 활=좌클릭 발사(우클릭 차지 스킵) ② 내 병사 GLB 근본(태그 미정의) ③ 무기 그립 과보정 ④ Ctrl 드래그 순서 문제. 배치컴파일 error CS=0 (exit 0).

### 근본 원인 실측
- **내 병사 GLB 미부착(D')** = `ProjectSettings/TagManager.asset` tags에 **`RecruitedSoldier`가 없음** (`Tag: RecruitedSoldier is not defined` 로그). 내 병사만 무효 태그 → GLB/충돌/선택 회피. (적=Guard 태그 정상.) 63차 GLB 확장자 폴백은 이미 적용 — 최신 로그(01:52)에 **병사 GLB 부착 6기 전부 성공**(MyGuard 0~2 + EnemyGateGuard 0~2). 재 태그 정의로 내 병사 태그 유효화.
- **활=우클릭 발사(H1)** = `PlayerCombat.Update`에서 **활 장착과 무관하게** 우클릭 차지(ReleaseCharge→TryChargeAttack)가 활에서도 강공 발동 → '우클릭 화살'처럼 보임. 좌클릭 TryBowShot은 363-365에 이미 존재.
- **활 애니(H2)** = `HumanoidClipDriver`에 `BowEnter/IsBow` 엣지 트리거(425-434) 이미 구현 — 활 장착 시 자동.
- **무기 그립(G)** = Log1(활/창) `offset=-0.926` 과보정(휴리스틱), Log2(검) `pivotT=0.03` 신뢰 경로는 offset 0(테이블 포즈) → 손 밀착 부족.
- **Ctrl 드래그(F')** = `PlayerCombat`이 좌클릭을 먼저 소비하면 `consumeLeftClickAsDrag`가 같은 프레임에 아직 false → 공격이 나가 드래그 안 됨(순서 문제). `GuardSelectionManager.OnGUI` 드래그 박스(142)는 이미 존재.

### 변경 사항 (3파일)
**`ProjectSettings/TagManager.asset` [D']**: tags에 `RecruitedSoldier` 추가 — 내 병사 태그 유효화.
**`Systems/PlayerCombat.cs` [H1+F']**: ① 우클릭 차지 블록에 `isBowEquipped` 가드(활이면 차지 스킵) ② `ReleaseCharge` 활 이중 방어 ③ 좌클릭 감지에서 **Ctrl 홀드 직접 판정**(Keyboard.current.ctrlKey)하면 공격 대신 드래그로 위임 — 프레임 순서와 무관하게 Ctrl+드래그 동작. 평상 좌클릭=공격 유지.
**`Systems/WeaponEquipManager.cs` [G]**: 휴리스틱 오프셋 과보정 방지 — offset 성분 절대값이 0.5 초과 시 클램프(+클램프 경고 로그). 무기별 세부 튜닝은 Play `[Weapon] 그립 정렬` 로그 기반.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0** (exit 0, 3회).
- Play 판정 대기: ① 활 장착 → 우클릭 무반응·좌클릭 화살 발사(인벤 화살 소모) ② 활 장착 즉시 활 대기 애니 ③ 내 병사 GLB 조회(태그 정상, 공격/추종) ④ Ctrl+드래그 시 반투명 네모+병사 선택 → Ctrl+1 슬롯 얼굴 ⑤ 검/창/활 손 부착 로그.
- ⚠️ TagManager 수정은 **에디터 재시작**(Play) 필요 — 재시작 후 태그 유효화.

### 남음
- H3(화살 조준점)·A'(장비 파츠 시각)는 Play 확인 후 후속(조준점은 다음 라운드 구현, 장비부착은 62차 확장자 폴백 준비—방어구 우클릭 이번 로그에 없었음).

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 63차 — TEST23 실 실행: 화살 인벤 시딩 + 병사 GLB 확장자 폴백 + 방어구 GLB 폴백 + 드래그 Ctrl 홀드)

> **스코프**: 62차 계획 문서상의 잔여를 실제 코드로 반영한 실행 라운드. ① 화살(창고→인벤) ② 병사 GLB 확장자 폴백 ③ 방어구 GLB 폴백 ④ 드래그 Ctrl 홀드 방식. 배치컴파일 error CS=0 (exit 0).

### 이번 라운드 코드 반영
**`Systems/TestTerritoryCombatSetup.cs` [T23-E 화살·T23-D 병사]**: ① `SeedPlayerInventory`에 화살 3종(arrow_regular/reinforced/magic ×20, ItemCategory.Arrow) 인벤 시딩 추가 — ArrowManager는 PlayerInventory에서 화살 소모(창고 아님) → '화살 부족' 발사 불가 근본 해결. ② `CreateGuard` GLB 로드 — 확장자 없는 경로 우선 + `.glb` 폴백(슬라임 CreateMonster 선례). 기존 확장자 포함만으로 6기 전부 FBX 폴백이던 것 해소.
**`Systems/ArmorVisualAttachSystem.cs` [T23-A]**: 방어구 GLB 로드도 확장자 없는 경로 우선 + `.glb` 폴백(WeaponEquipManager·병사와 동일 패턴). 부착 시각화 회귀 차단.
**`Systems/GuardSelectionManager.cs` [T23-F]**: 좌클릭 드래그 게이트를 **Ctrl 홀드**(`ctrlKey/leftCtrl/rightCtrl`)로 교체 — 평상 시 좌클릭=공격 유지, Ctrl 누른 채만 병사 드래그 선택.
**`UI/GuardSquadHotbar.cs` [T23-F]**: `squadModeActive` 전달 제거(더 이상 Tab 의존 불필요 — Ctrl 방식으로 단순화). 부대 등록/선택(숫자키)만 유지.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0** (exit 0) — 중간 CS0136(ctrlHeld 중첩 스코프) 1건 rename으로 수리(ctrlActiveForDrag).
- Play 판정 대기: ① 인벤에 화살 20씩 → 활 좌클릭 발사·적중 ② 병사 GLB 6기 렌더(FBX 폴백 0) ③ 방어구 장착 시 캡슐 아닌 GLB 부착 ④ Ctrl 누른 채 드래그로 병사 선택 → Ctrl+1 등록 → 슬롯 얼굴, 평상 좌클릭=공격 ⑤ 창/검/활 그립 로그(62차 Y+180/y0.05/Y+90 반영 확인).

### 남음 (그립/UI는 Play 튜닝 필요)
- T23-B/C 그립: 62차 방향 변경(Y+180/y0.05/Y+90)이 로그에서는 pivotT/offset만 보임 — Play 시각 확인 후 `[Weapon] 그립 정렬` offset/pivotT로 손 위치 정밀 조정(H-5).
- T23-G UI 비례: HUD/미니맵은 `_canvasScale` 적용 주이, HotbarUI는 RectTransform 캐시 구조라 전용 라운드 보류(회귀 리스크). 창 크기 변경 비례 + 미니맵/핫바 확대는 다음 Play 후.

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 62차 — TEST21-FOLLOWUP 전면 수리: 방어구 lazy 생성 + 창/검/활 그립 + 병사 GLB/inactive + 화살발사 + 드래그 Tab 게이트 + UI 중세판타지 팔레트 전환)

> **스코프**: `docs/TEST21_FOLLOWUP_PLAN.md` 실행. A방어구/B창그립+B2검/C활/D병사/E화살/F드래그Tab/G UI 팔레트 전부 수리. 배치컴파일 error CS=0 (exit 0).

### 근본 원인 실측 요약
- **A 방어구 실패** = `EquipmentManager.Instance`가 씬 미부트/초기화 순서로 null. `InventoryWindow`가 Instance만 조회 → 실패 로그만.
- **B 창 그립** = Spear `LocalEuler(-90,0,0)`에서 창두가 뒤로 → **Y+180** 전방 보정.
- **B2 검** = "손에서 약간 어긋남"(날 방향 정상) → GripPose y 0.12→0.05 손바닥 밀착.
- **C 활** = Bow `LocalEuler Y -90→+90` 좌우 미러 교정(왼손 활대/오른손 시위).
- **D 병사** = ① `PlayerPlaceholder:108 DestroyImmediate(rb)`가 ProceduralAnimationController 의존(RequireComponent)으로 차단 → try-catch 격리+중립 유지 ② inactive(사망) 대상 HitReaction Driver 코루틴 스팸 → `!target.activeInHierarchy` 스킵.
- **E 화살** = Test_10에 ArrowManager 미생성(Instance null, TryShootArrow 스킵) → EnsureGameManager+TryBowShot lazy 자가생성. ArrowProjectile이 `Guard/RecruitedSoldier/DraculaLord` 적중 미탐지 → 태그 추가.
- **F 드래그** = `consumeLeftClickAsDrag`가 단순 클릭까지 드래그로 소비 → **Tab(부대) 모드에서만 드래그** 게이트(squadModeActive, GuardSquadHotbar가 갱신). 기존 IsSquadMode 중복 제거(CS0102).
- **G UI** = UIStyleManager 미드나이트 블루 파레트 → **딥 차콜 레더+브론즈/골드 트림+양피지 화이트**(docs/UI_DESIGN_GUIDELINES.md). 창 하드코딩 스카이블루(0.35,0.65,0.90)→마법블루(0.29,0.48,0.81)·네이비(0.063,0.086,0.133)→차콜(0.11,0.11,0.11) 일괄 치환.

### 변경 파일 (13 + 문서 1)
EquipmentManager(Get()·lazy)/WeaponEquipManager(창·검·활 GripPose)/InventoryWindow(Get()×2+색)/PlayerPlaceholder(RB try-catch)/HitReactionDriver(inactive스킵)/TestTerritoryCombatSetup(ArrowManager 생성)/PlayerCombat(ArrowManager lazy+활)/ArrowProjectile(적중태그)/GuardSelectionManager(squadModeActive 게이트)/GuardSquadHotbar(IsSquadMode 중복제거+게이트)/UIStyleManager(파레트)/InventoryArtLibrary·LootWindow·ShopWindow·StatusWindowUI·WarehouseUI·ItemDragContext(색 치환) + docs/TEST21_FOLLOWUP_PLAN.md·UI_DESIGN_GUIDELINES.md.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0** (exit 0) — 중간 CS0102(IsSquadMode 중복) 1건 수리 후 통과.
- 남은 것은 Editor 스크립트 deprecated 경고(CS0618 등) — 동작 무영향.
- Play 판정 대기: ① 방어구 우클릭/드래그 장착+GLB 부착 ② 창 두가 전방·검 밀착·활 좌우 ③ 병사 GLB 렌더·inactive flinch 스팸 0 ④ 활 좌클릭 화살 발사→몬스터/병사 적중 ⑤ Tab(부대)에서만 드래그 선택→Ctrl+등록→슬롯 병사 얼굴 ⑥ 전 창 딥 차콜/브론즈/골드/양피지 팔레트.

### 잔여
- 무기 그립 미세(창두 방향/활 미러 Play `[Weapon] 그립 정렬` 로그 확인).
- UI 심화(각 창 섹션별 골드 트림·폰트 계층·인터랙션)는 사용자 Play 후 Phase G 후속.

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 61차 — 테스트21 피드백 후속: 슬래시 즉시 발화 + 병사 포진 분산 + 좌클릭 드래그/공격 분리)

> **스코프**: 사용자 테스트 21(22:45, 60차 반영 전 촬영) 피드백 — ① 1번(빨간 몬스터) 해결 확인 ✓ ② 땅 파묻힘 해결 ✓ ③ 방어구·무기 그립·드래그는 Play 재판정 필요 ④ **슬래시가 늦음**(좌클릭 시 공격+이펙트 동시여야) ⑤ **병사 3명이 겹침**. → 슬래시 strike 동기 제거(즉시 발화)+병사 포진 분산+좌클릭 드래그/공격 분리. 배치컴파일 error CS=0.

### 테스트21 피드백 근본 원인 실측
- **슬래시 늦음** = 55차 Phase E `StageStrikeSyncNormT=0.38`이 아크를 strike 프레임까지 지연(로그: `[Combo] 트레일 즉시 → 아크는 strike 대기 → strike 동기 아크 발화(0.38)`). 사용자는 '좌클릭 즉시 공격+이펙트 동시' 원함.
- **병사 3명 겹침** = `GuardCombatAI.NotifyPlayerAttack`이 모든 포섭 병사에게 **같은 `target.position`** 을 명령 목표로 줘 3명이 한 점에 뭉침(로그: 내병사1/2/3 모두 `합세 → TerritoryLord` + `근접 공격 대상=TerritoryLord`).
- **드래그(단체 선택) 미해결** = `GuardSelectionManager`는 생성됐지만, `PlayerCombat`이 좌클릭 `wasPressedThisFrame`에서 **무조건 TryAttack()** 을 호출해 드래그 시작 직후 공격이 나가 선택이 가려짐(60차 생성은 Pop pero 충돌 미해소).
- **방어구/무기 그립** = 60차에서 EquipmentManager 생성+ArmorVisual 구독 확인. 우클릭 장착 시도 로그 0건은 60차 반영 전 촬영 때문 — 최신 빌드 Play 재판정. 무기 그립은 장착 성공(손에 들림)이나 정밀 어긋남 — H-5 튜닝 Play 판정.

### 변경 사항 (3파일 — HumanoidClipDriver/GuardCombatAI/GuardSelectionManager + PlayerCombat)
**`Systems/HumanoidClipDriver.cs` [슬래시 즉시 발화]**: `StartStageClip`(763)/`AdvanceStageClip`(777)에서 `_pendingStageArc = _comboStage`(strike 대기) → **`FireComboSlash(_comboStage)` 직접 호출** — 아크를 클릭 즉시 발화(공격+이펙트 동시, 사용자 요구). MonitorStageClip의 strike 동기 블록은 `_pendingStageArc` 정지로 dead(유지 — 회귀 리스크 0).
**`Systems/GuardCombatAI.cs` [병사 포진]**: `NotifyPlayerAttack`에서 각 병사에게 target 주변 **원형 포진 오프셋(1.8m 반경, 90° 간격 slot 0~3)** 을 부여 — 전원이 한 점에 뭉치는 것 방지. 로그에 포진 슬롯 표기.
**`Systems/GuardSelectionManager.cs` [드래그 플래그]**: `public static bool consumeLeftClickAsDrag` 신설 — 좌클릭 down 시 true 세팅(드래그 시작 의도 표시).
**`Systems/PlayerCombat.cs` [좌클릭 분리]**: 좌클릭 `wasPressedThisFrame`에서 `GuardSelectionManager.consumeLeftClickAsDrag`가 true면 **공격 스킵 + 드래그로 소비**(소비 후 false). 단순 클릭(드래그 전)은 공격 재개 → '좌클릭=공격 / 드래그=단체 선택' 자연 분리.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0** (exit 0, 에디터 종료 상태 2회 통과).
- 괄호 균형: 변경 4파일 전부 {}/()/[] 정합. GuardSelectionManager/PlayerCombat 같은 네임스페이스(Systems)로 참조 무충돌.
- Play 판정 대기: ① 좌클릭 즉시 흰 슬래시 아크+공격 동시 ② 내병사 3명이 대상 주변 포진으로 벌려 붙음(겹침 없음) ③ 드래그 박스로 병사 단체 선택(파란 박스)+우클릭 명령 ④ 방어구 우클릭/드래그 장착+GLB 부착(현재 빌드) ⑤ 무기 그립 정밀 ⑥ 몬스터 어그로 오라 복귀 ⑦ Free Slash 흰색.

### 잔여 (Play 판정 후)
- 무기 그립 H-5: `[Weapon] 그립 정렬` 로그 offset/pivotT로 검/창/활 GripPose 미세튜닝.
- 방어구: 최신 빌드에서 우클릭 wood_armor → 성공 로그 + GLB 부착 확인.
- Test_10 병사 GLB(현 FBX 폴백)는 에디터 임포트 재확인.

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 60차 — 테스트21 잔여 수리: 어그로 오라 MPB 전환 + GuardSelection/RTS 생성 + 드래그 단체 지정 + 방어구 장착 근본 + Free Slash 주경로 + 병사 지면 접지)

> **스코프**: 테스트 21 영상(2026-09-15) 잔여 문제를 근본 원인 확정 후 수리. ① 빨간 몬스터 고정(FB) ② 드래그 단체 지정(GB) ③ 방어구 장착 지속 실패(GB) ④ 무기 그립(GC) ⑤ 병사 땅 파묻힘(GD) ⑥ 슬래시 Free Slash 전환(GE). 배치컴파일 error CS=0.

### 테스트 21 근본 원인 실측 (Editor.log + 코드)
- **#FB 빨간 몬스터에서 안 돌아옴** = `MonsterAggroSystem.ShowAggroVisual`이 `r.materials` 배열 끝에 `_aggroMaterial`(반투명 붉은 재질)을 추가했는데, `HideAggroVisual`이 `m == _aggroMaterial` **참조 비교**로 제거. Unity의 `r.materials`는 접근 시마다 인스턴스 재질을 반환/복제해 참조가 깨짐 → 오라가 영구 잔존(몸이 계속 빨강).
- **#GB 방어구 지속 실패** = 59차에서 Test_10 `EnsureGameManager`에 `EquipmentManager` 추가했으나, 76행 `if (GameManager.Instance != null) return;` **early-return**이 있어 GameManager가 이미 존재하면 장비 시스템 생성이 스킵됨(로그: `결과 실패(사유: EquipmentManager 없음)`).
- **#GB 드래그 단체 지정** = Test_10은 CoreSystemsBootstrap 미실행 → `GuardSelectionManager`(좌클릭 드래그 병사 선택)와 `RTSCommandSystem` 미생성 → '내 병사를 드래그해서 단체 지정하는 모션'이 동작 안 함. (GuardSelectionManager는 완전 구현돼 있었으나 생성부 부재 — 33차/GB에 dead code).
- **#GC 무기 그립** = 무기는 실제 장착 성공(로그 `[Weapon] 인스턴스: wood_sword 렌더러=1 hand=RightHand` + `✅ → RightHand`). 영상에서 어긋나 보이는 건 그립 오프셋 미세조정(Play 튜닝).
- **#GD 병사 땅 파묻힘** = 병사 루트를 `pos.y=SurfaceY+1.0`(레이케스트 박스 오프셋)에 두고 모델만 `GroundModelToY`로 지면에 내리는 구조 → `StepToward`가 루트.y를 `TryGetGroundY`(지면)로 보정하면 모델이 상대 −1.0으로 지면 아래 파묻힘(모순). 루트를 지면에 직접 두고 콜라이더만 위로(+0.9) 올리는 방식으로 전환.
- **#GE 슬래시 흰색** = 스타일라이즈드(.vfx)는 파티클 시스템이 없어 `TintParticles`(흰색 tint)가 no-op → 흰색 아크가 안 됨. → **Free Slash(Slash VFX.prefab, 8 MeshRenderer·URP Shader Graph)를 주 경로로 승격**(사용자 지시). Free Slash는 파티클이라 `ComboStageTint`(흰색)가 즉시 반영.

### 변경 사항 (3파일 — SlashVFXRunner/MonsterAggroSystem/TestTerritoryCombatSetup)
**`Systems/MonsterAggroSystem.cs` [오라 MPB]**: `ShowAggroVisual`/`HideAggroVisual`의 붉은 오라를 `r.materials` 배열 추가/참조-비교 제거 → **MaterialPropertyBlock(_BaseColor) 기반으로 전환**(렌더러별 원본 색 추적 `_aggroAuraOrigColor` + 복원). 공유/인스턴스 재질과 무관하게 항상 정리 → '몬스터가 빨간색에서 안 돌아옴' 근본 해결.
**`Systems/TestTerritoryCombatSetup.cs` [시스템 생성 + 접지]**: ① `EnsureGameManager` early-return 제거 — GameManager 이미 존재해도 `EquipmentManager`+`ArmorVisualAttachSystem`+`GuardSelectionManager`+`RTSCommandSystem`+`GuardHostilitySystem`을 항상 보장(재실행/재스폰에서도 방어구·드래그 지정 동작). ② 병사 `CreateGuard` 루트를 지면(`SurfaceY`)에 직접 배치 + BoxCollider center(+0.9) 위쪽 조정 — '병사 땅으로 사라짐' 근본 해결.
**`Systems/SlashVFXRunner.cs` [Free Slash 우선]**: `PlaySlashStage`에서 `LoadSlashPrefab()`(Free Slash "FX/Slash/Slash VFX") 성공 시 즉시 발화 return(주 경로), 실패 시에만 스타일라이즈드(.vfx) 폴백. 흰색 아크(ComboStageTint)가 파티클에 즉시 반영.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0** (exit 0, 에디터 종료 상태 2회 통과).
- 괄호 균형: 변경 3파일 전부 {}/()/[] 정합.
- Play 판정 대기: ① 몬스터 어그로 붉은 오라가 상태 이탈 시 원래 색으로 복귀(잔존 0) ② 내 병사 우클릭 드래그 박스로 단체 선택(파란 박스 표시) + 선택 병사 우클릭 명령 ③ 방어구 우클릭/드래그 장착+GLB 부착(EquipmentManager 있음) ④ 무기 그립 정밀(손바닥) ⑤ 병사 발이 지면에 붙고 공중/파묻힘 없음 ⑥ 슬래시 Free Slash 흰색 아크(스테이지별 흰색).

### 잔여 (Play 판정 후)
- #GC 무기 그립: `[Weapon] 그립 정렬` 로그 offset/pivotT로 검/창/활 GripPose 미세조정(H-5).
- 테스트21에서 "흰색 고정"이 여전히 보이면: `CombatVFXController.PlayHitFlash` refcount가 여전히 레거시 MPB(HitVFX)와 충돌하는지 추가 확인(59차 T1로 MPB 호출 제거했으나, 다른 호출부 확인).

---

## 📌 세션 종합 스냅샷 (2026-09-15 ✅ 59차 — 테스트20 통합 수리: 단일 히트플래시 + 느낌표 수명 + 고스트 상시 렌더 + 방어구 생성)

> **스코프**: 사용자가 테스트 20 영상(2026-09-15)에서 발견한 9건을 `docs/TEST20_FIX_PLAN.md`로 확정하고 근본 원인 장르별(T1~T7) 수리. 1차 라운드로 T1(흰색 고정), T2(느낌표), T3(드래그/고스트), T4(방어구 장착), T6(슬래시 흰색 확정)을 수리. 계획서 기준 배치컴파일 error CS=0.

### 테스트 20 근본 원인 실측 (Editor.log + 프레임 스캔)
- **#1 흰색 고정** = 이중 경로 — `AnimalAI.TakeDamage`(890) + `HitReaction.cs`(71)이 refcount(CombatVFXController)와 **별개인 레거시 `HitVFX.PlayHitFlash`(MaterialPropertyBlock)** 를 병행 호출 → MPB가 sharedMaterial 색을 덮어 흰색 잔존. 영상 25프레임에서 흰 덩어리 재현.
- **#2 느낌표 미소멸** = `ShowAggroVisual`(수명 무제한)이 적대(Combat) 상태 내내 재표시 — `ShowTransientExclamation`(1.2s)과 별개로 충성도 주기 재표시.
- **#3·#8 드래그 불가** = 고스트 렌더가 `InventoryWindow.OnGUI`(IsOpen일 때만) 의존 → 인벤 닫힌 채 전리품/창고 드래그 시 고스트 없음.
- **#4 방어구 장착 실패** = Test_10 `EnsureGameManager`에 `EquipmentManager`/`ArmorVisualAttachSystem` 생성 누락(CoreSystemsBootstrap 미실행 씬). 로그: `결과 실패(사유: EquipmentManager 없음)`. (무기는 성공: `✅ wood_sword → RightHand`)
- **#5 무기 그립** = 무기 장착 성공(영상에서 손에 들림) — 그립 끌어당김만 잔여(Play 튜닝).
- **#6 슬래시 색** = `ComboStageTint`는 이미 흰색 고정 — 스타일라이즈드 .vfx(white-blue)가 파티클 시스템 부재로 tint no-op라 파란색 노출(Play 판정).
- **#7 병사 GLB** = 6기 전부 **FBX 폴백**(avatar Valid + SoldierShield_AC)으로 부착·애니 동작 — GLB 전용 로드는 null(에디터 임포트 검증 필요).
- **#9 병사 접지** = `GroundModelToY(SurfaceY)` 적용됨(57차) — 영상 프레임 대체로 지면 접촉, Play 재확인.

### 변경 사항 (5파일 — 문서 1: docs/TEST20_FIX_PLAN.md)
**`Systems/AnimalAI.cs` [T1]** (890행): 레거시 `HitVFX.PlayHitFlash`(MPB) else 블록 제거 → `CombatVFXController.PlayHitFlash(gameObject)` 단일 경로(GetComponentsInChildren로 GLB 자식 렌더러 전부 커버). 이중 발화·흰색 고정 원인 제거.
**`Systems/HitReaction.cs` [T1]** (71행): 레거시 `HitVFX.PlayHitFlash`(MPB) 제거 → 비주얼 플린치(스케일 펄스/경직)만 수행. 플래시는 CombatFXGate→CombatVFXController(refcount) 담당.
**`Systems/MonsterAggroSystem.cs` [T2]** (Update 174·ShowAggroVisual 320): 몬스터 느낌표를 `ShowAggroVisual`의 수명 무제한 생성 → `ShowTransientExclamation(go, 1.5f)` 단일 수명으로 통일. 상태(Alert/Combat)가 이탈하면 HideAggroVisual로 즉시 제거. 병사 적대(GuardHostilitySystem)도 동일 transient 경로.
**`UI/HUD.cs` [T3]** (OnGUI 상단): `ItemDragContext.Active`면 `DrawGhost()`+`DrawSlotHighlight()` 상시 렌더 — 인벤토리/창고/전리품/장비 어떤 창이 닫혀 있어도 드래그 고스트 표시. 프레임 가드(Time.frameCount)로 이중 렌더 방지. 기존엔 인벤 OnGUI(IsOpen)만이 고스트를 그려 '드래그 불가'로 보였음.
**`Systems/TestTerritoryCombatSetup.cs` [T4]** (EnsureGameManager): Test_10 씬에 `EquipmentManager` + `ArmorVisualAttachSystem` 생성 보장(중복 가드 + ✏️) — 방어구 우클릭/드래그 장착 활성화. 사용자 리포트(장비 착용 안 됨) 직접 근본 원인.

### 컴파일/검증
- Unity 6000.4.10f1 batchmode **error CS=0** (exit 0, 에디터 종료 상태 1회 통과).
- 괄호 균형: 변경 5파일 전부 {}/()/[] 정합.
- Play 판정 대기: ① 피격 후 흰색 0.15s 후 원복(연속/다중 피격 잔존 0) ② 느낌표 잠깐 뜨고 사라짐(상태 이탈 시 즉시) ③ 인벤 닫은 채 전리품/창고 드래그 고스트 표시+드롭 ④ 방어구 우클릭/드래그 장착+GLB 부착 ⑤ 슬래시 흰색(Free Slash 고려 Play 판정) ⑥ 병사 FBX 렌더+접지 ⑦ 무기 그립 미세 조정.

### 잔여 (T5/T7 Play 판정 + 계획서 후속)
- T5 무기 그립 끌: `[Weapon] 그립 정렬` 로그의 offset/pivotT로 검/창/활 GripPose 오프셋 튜닝(H-5).
- T7 병사 GLB 전용 로드: CreateGuard GLB 로드 null → 에디터 임포트 확인 + 성공 시 GLB 렌더(현 FBX는 동작). 접지 지면 재확인.
- T6 Free Slash 폴백 강제 여부(사용자 선택).

---

> **스코프**: 사장님 요구 — ① 내 공격 시 내 소속 병사도 공격(이미 배선) ② **타 영지 병사는 호감도에 따라 원래 공격 안 하다가, 내가 공격하는 순간 호감도 하락 → 몬스터처럼 느낌표 뜨며 플레이어/내 병사 공격** ③ 느낌표는 잠깐 뜨고 사라지게(계속 X).

### 조사 결론
- GuardHostilitySystem이 호감도 주기(2s) 적대 전환·선공을 이미 구현했지만: ①플레이어 공격 시점의 즉시 트리거+호감도 하락 부재 ②적대 시 느낌표 표시 부재 ③생성부(Instance)가 어디에도 없어 실동작 미배선.
- 느낌표는 MonsterAggroSystem의 AggroExclamation 프리팹(빌보드+TextMesh) — IAggroable(몬스터) 전용이라 병사엔 재사용 불가.
- 병사 기본 호감도 50, Loyalty clamp -100~100.

### 수정
**MonsterAggroSystem.cs [transient 느낌표 재사용]**
- `ShowTransientExclamation(GameObject host, float duration=1.2)` 추가 — 동일 프리팹을 임의 개체(head 위 +2.5m)에 붙이고 **duration 초 후 StartCoroutine으로 자동 제거** → "잠깐 뜨고 사라지게" 완결. (기존 ShowAggroVisual은 몬스터 전용·수명 무제한이라 안 씀.)

**GuardHostilitySystem.cs [플레이어 공격 즉시 적대화]**
- `HostileLoyaltyDrop=45` 상수 + `NotifyPlayerAttack(attackedTarget, player)` 추가 — 공격 반경(_attackRange 8m) 내 **비-포섭(적) 병사**만: ①호감도 -45(충성도 급락) ②적대 상태 재계산 ③적대 전환 시 `ConvertToHostile` ④`ShowTransientExclamation` 느낌표 ⑤`SetCommandTarget(플레이어,공격)` 선공.
- `InitiateAttackVsPlayerAndSoldiers` — 적대 병사가 플레이어 공격(내 병사 합세는 기존 GuardCombatAI.NotifyPlayerAttack이 담당).

**PlayerCombat.cs [호출 배선]**
- AttackTarget 공격 성공 직후 `GuardHostilitySystem.Instance.NotifyPlayerAttack(target, 플레이어)` 호출 추가(이미 GuardCombatAI.NotifyPlayerAttack 내병사 합세 배선 옆).

**TestTerritoryCombatSetup.cs [Instance 보장]**
- EnsureGameManager에 `GuardHostilitySystem` AddComponent — 생성부가 없어 실동작 미배선이던 문제 해결.

### 컴파일/검증
- 배치컴파일 error CS=0. 변경 4파일.

### Play 판정 대기
① 내 공격 시 내병사 합세 ② 적 문지기(호감도 50) 가까이서 공격 → 호감도 하락 → 느낌표 잠깐 뜨고 사라짐 → 플레이어/내병사 공격 ③ 친화(호감도 50) 적 문지기는 원래 공격 안 함 ④ 호감도 낮은 타 영지 병사는 처음부터 적대.

---

## 69차 후속3·4 (2026-09-16, 스크린샷 65/66+앵커 콘솔 실측) — 장비 부위별 배치 확정

### 변경 로그
- **후속3 (커밋 5e49d6c0)**: EquipmentSlot에 Mask/Bag 신설(가스마스크=얼굴 Head/가스팩=등 Spine — MapArmorSlot 폴백이 Armor로 보내 Spine에 붙던 것 수리) + 장비칸 예약 2칸→🎭가면/🎒가방 실슬롯 + 장갑/부츠 통합 id의 단일 GLB 부재(좌우 파일만 존재)→InstantiateAttached 본별 좌우 GLB 로드 위임(bool 반환) + 통합 id→좌 GLB 아이콘 맵 8종 + 창 방향 3차 (-90,0,0).
- **후속4 (커밋 8d905e97, 스크린샷 66 미세조정)**: 콘솔 anchor 실측 역산 —
  - ⚠️ **player.transform.y는 지면이 아니라 CC 중심**(스폰 실측 0.87m; Head 본 1.44/Spine 1.12/손 1.24/발 ~0m) → Shoes가 ground.y+0.01=0.88m(무릎)에 붙던 뿌리 수리: 부츠 하단 = 복사 본-5cm(본 기준·언덕 추종).
  - **Head 본=두개골 상단(1.44m) 확정** → 투구 하단 본-6cm(관 감쌈), 가면 본-12cm+전방 7cm(눈높이 — 정수리 걸침 수리).
  - 갑옷 Spine+14cm(가슴 중앙), 가방 본+5cm·등 뒤 10cm.
  - 방패 pLeft(transform 기반) 폐기 — LeftHand 본이 transform 기준 우측 0.23m 역산(모델 시선 어긋남) → **Spine↔손 벡터** 바깥 10cm+본+2cm.
  - 부착 로그 본별 실측화 — 쌍슬롯 합산 bounds가 장갑을 "본↔중심 0.24m"로 오판(실제론 각 손 본 스냅) → 본별 최대 거리+본명 표기.

### 버그/제약 (신규 확정)
- CC/transform 피벗·SkinnedMesh bounds는 접지 기준으로 절대 신뢰 금지 — 장비 배치는 전부 본 위치 기준(이동 후 Head 2.94=지형 1.5+1.44로 언덕 추종 검증).
- 통합 부츠/장갑은 단일 GLB가 없으므로 본별 좌우 GLB(wood_boot_left/right 등) 필수.

### 컴파일/검증
- 배치컴파일 error CS=0 (후속3·후속4 모두 exit 0). 변경: ArmorVisualAttachSystem.cs(후속4) + EquipmentManager/InventoryWindow/WeaponEquipManager/GblItemIconRenderer(후속3).

### Play 판정 대기 (스크린샷 67~)
① 투구 머리 감쌈 ② 가면 얼굴(눈높이) ③ 갑옷 가슴 중앙 ④ 가방 등 중앙 ⑤ 부츠 발(지면 근처) ⑥ 장갑 양손 ⑦ 방패 왼손 바깥쪽 ⑧ 부착 로그 본↔중심 ≤0.2m — 어긋나면 스크린샷+anchor 콘솔값 → 슬롯 상수 즉시 튜닝.

---

## 69차 후속5 (2026-09-16, 테스트27 영상+FBX/GLB 직접 파싱) — 치비 리그 발견·헬멧 매몰 수리

### 근본 원인 (실측 — 커밋 1cc37035)
- **플레이어 = 1.0m 치비 캐릭터**: Player_Rigged.fbx 바이너리 정점 직접 파싱 → VERT_SIZE=(0.89, 1.0, 0.4), 발=모델 원점(y=0), useFileScale=1/globalScale=1 그대로 임포트. PlayerPlaceholder가 모델을 transform 원점에 scale 1 부착 → **transform.y=지면**(스폰 0.87), 두개골=로컬 0.6~1.0(0.4m 대형 머리).
- **Head 본(로컬 0.57)=턱/두개골 하단** — 후속4의 "두개골 상단" 추정 오진. 본±수cm 앵커(후속3 -12cm / 후속4 -6cm)는 전부 두개골 **내부 매몰** → 67차 영상 "투구 아예 안 보임"의 뿌리. 65차(CC 앵커 2.65m)에서만 보였던 이유=머리 위 0.8m 공중 부양이었음.

### 수정 (ArmorVisualAttachSystem.cs)
- 투구: Bottom(본-6cm) → **Center 본+26cm**(정수리 감쌈), GetBottomAnchor에서 Helmet 제외(Shoes만 Bottom).
- 가면: 본-12cm(턱 아래) → **본+19cm+전방 6cm**(눈높이 로컬 0.76).
- 갑옷 Spine+15cm(가슴 로컬 0.40) / 가방 Spine+12cm·등 뒤 9cm / 장갑 손 본-2cm.
- 부츠(발 본-5cm=지면+1cm)·방패(Spine↔손 벡터 바깥 10cm)는 실측 부합 → 유지.
- **Assets/Editor/MeasureRig.cs 신규**: 에디터 닫힌 후 `Unity -executeMethod MeasureRig.Measure`로 FBX bounds+휴머노이드 본+장비 GLB 치수 일괄 실측.

### 실측 데이터(튜닝 기준)
- 본(로컬): 발 0.06 / Spine 0.25 / 손 0.37 / Head 0.57. 장비 정규화 치수: 투구 0.27높이 / 갑옷 0.62너비·0.34높이 / 방패 0.80×0.40 / 가면 0.24×0.21 / 가팩 0.39×0.26 / 부츠 0.19×0.36 / 장갑 0.15×0.08.
- FBX 바이너리 파서 함정: plen은 속성 시작 기준(pos+plen 아님), 버전<7500은 32-bit 오프셋, 헤더 23바이트+버전@23. GLB: accessor min/max는 primitives[].attributes.POSITION 경로.

### 컴파일/검증
- 배치컴파일 에디터 점유로 미실행(EXIT=1, lock) — 변경은 상수 3곳+불식 1개라 리스크 없음, 에디터 포커스 시 자동 리컴파일. **다음 Play 전 콘솔 CS 에러 여부 확인 요망.**

### Play 판정 대기 (테스트 28)
① 투구 정수리 착용 ② 가면 눈높이 ③ 갑옷 가슴 ④ 가방 등 ⑤ 장갑 손목 ⑥ 부츠/방패 유지 ⑦ CS 에러 무 — 어긋나면 anchor 콘솔값+스크린샷 → 상수 튜닝.

---

## 69차 후속6 (2026-09-16, 테스트28 영상 실측) — 무기 그립 DATA 모드·활 왼손·안내창 제거

### 테스트 28 판정(방어구) — 대부분 성공
투구(Head+26cm)가 머리에 보임 ✓, 갑옷/부츠/장갑 몸 부위 부착 ✓ (영상 프레임 실측). 후속5 앵커 유효.

### 무기 그립 뿌리 (커밋 ac1be3dc)
- 기존 정점 슬라이스(strategy=2)의 "끝 단면 작은 쪽=손잡이" 가정 오류 — 실제 작은 쪽=**칼끝(테이퍼)** → 그립이 칼날 중간에 찍히고 칼날이 땅을 향함(영상: 검 칼날下+손=칼날 중간, 활=오른손).
- 16종 GLB(4티어×4타입) 단면 프로필 직접 파싱: 유형별로 길이축·칼끝·가드 위치가 제각각(steel_sword=-Z칼날, wood_sword=+PC1칼날 등) → 고정 Euler 불가.
- **DATA 모드(strategy=3) 신설**: 런타임 정점 분석 — 검/단검=얇은 끝이 칼끝·그립=가드→손잡이 45%, 창=넓은 끝이 촉·그립=버트 33%, 활=중앙. 회전 보정 FromToRotation(칼날축→**팔뚝 방향(손→팔꿈치)**=하늘) + 그립 재스냅(손 원점). 4티어 전체 적용.
- **활=왼손 그립**(bowLeft 분기 — HumanoidBones.LeftHand+이름 폴백). 방패(Back) 상호배타 게이트 유지.
- **"공격시스템 점검-좌클릭=공격" OnGUI 안내창 제거**(TestTerritoryCombatSetup — 사용자 요청).

### 컴파일/검증
- 배치컴파일 **error CS=0** (exit 0). 변경 2파일.

### Play 판정 대기 (테스트 29)
① 검/단검: 손잡이=손 원점·칼날=하늘 ② 창: 촉=하늘·자루 1/3 지점 그립 ③ 활=왼손·수직 ④ 방패+활 동시 불가 게이트 ⑤ 안내창 소실 ⑥ [Weapon] 그립 정렬(DATA) 로그 확인 — 어긋나면 해당 무기 id+스크린샷 → 규칙 파라미터 튜닝.
