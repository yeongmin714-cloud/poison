# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **최종 갱신:** 2026-09-20 (P21b — 테스트10 = 병사1+몬스터1, 영지/실내 상호작용 제거, 커밋 c7cfbcfc)

---

## 📌 세션 스냅샷 (2026-09-20 ✅ P21b — 테스트10 = 병사1+몬스터1 — 커밋 c7cfbcfc)

> **입력**: 사용자 정정 — "반대로. 영지 상호작용도 없애고 병사 하나+몬스터 한마리만. 실내는 IndoorScene 직접 작업".

### 구현 (TestTerritoryCombatSetup.cs)
- P21 경량판(트리거 2종+마커+영지 데이터 등록) 전면 철회 → **SetupTestDummies**: CreateGuard(병사1, Lv10, 포섭, East) + SpawnMonster(slime).
- 영지 시각/트리거/마커/TerritoryDatabase 등록 모두 제거 — 실내 상호작용 없음. 실내 작업은 에디터에서 IndoorScene.unity 직접 오픈.
- SetupMyTerritory/SetupEnemyTerritory/트리거 생성 코드는 미호출 잔존(복원 가능). SpawnLord/약초/농장/광질 주석 보존(P21).
- 창고 박스(wh_test) 시딩은 UITestArena 유지 — WarehouseSystem은 territoryId 문자열만 쓰므로 DB 등록 없이 동작.

### 검증
- 배치컴파일 CS=0, EditMode **288/288**.

### Play 판정 대기
①테스트10 = 플레이어+병사1+슬라임1만 ②F키 → 병사 상호작용 창 ③화살/전투 검증 가능 ④렉 최소화

---

## 📌 세션 스냅샷 (2026-09-20 ✅ P21 — 테스트10 영지 경량화 — 커밋 5faaeae3)

> **입력**: 사용자 — "IndoorScene 이동 확인했으니 테스트10 영지는 제거. 다른 테스트는 실내씬에서 직접".

### 구현 (TestTerritoryCombatSetup.cs)
- **제거(호출 주석 보존)**: 내/적 성 시각, 병사 3+3+문지기(CreateGuard), 영주/단병사/슬라임 더미, 약초 3종/농장 2x2/광질 노드 3종.
- **유지**: ①TerritoryDatabase 등록(East_01 PlayerOwned + North LordOwned) — Castle 트리거 PlayerOwned 판정, 창고(wh_test) 등 영지 키 의존 시스템 ②실내 진입 트리거 2종(성 전면/크래프트하우스 동측, 원 좌표) ③**표지 마커** 신설(1m 파랑/갈색 기둥 — 콜라이더 제거로 레이캐스트 오염 없음) ④UITestArena(전 아이템 시딩 — 실내 크래프트 재료 공급).
- SpawnLord/SpawnGuard/SpawnMonster/SetupHerbs/SetupFarm/SetupMiningNodes 호출은 주석으로 보존 — 필요 시 해제 한 줄 복원.

### 검증
- 배치컴파일 CS=0, EditMode **288/288**.

### Play 판정 대기
①테스트10 로드 시 렉 감소 ②필드에 파랑/갈색 표지 기둥 2개만 존재 ③각 표지 E키 → 실내 진입 정상(PlayerCastle/CraftHouse) ④실내 크래프트 재료 시딩 정상

---

## 📌 세션 스냅샷 (2026-09-20 ✅ P20 — 테스트 37 실측 7건 수리 — 커밋 8c7741a0)

> **입력**: Screenshots/테스트 37.mp4 프레임 실측(f06/f18/f24/f31) + Editor.log 실측. 7건 전부 뿌리 확정 후 1회 수리.

### P20-1 설명창 불가 — **static Show 무한 재귀(내 P11 수리 실수)**
- `if (!i.IsOpen) ItemDescriptionWindowUTK.Show();` — IsOpen은 base.Show() 후 true인데 재귀가 선행 → 스택 폭발, 설명창 미표시.
- 수리: static 진입점 Show→**Open()** 개명 + `i.Show()`(UTKWindowBase 인스턴스) 단일경로. 호출부 4곳 갱신. 배치 = **화면 정중앙**(요구) — CenterOnScreen() 신설.
- 교훈: **정적 진입점과 인스턴스 Show가 공존하는 UTK 창에서 재귀 호출 금지 — 개명으로 분리**.

### P20-6 실내가 설계본과 다름 — **로그 실측 'HQ 적용 실패 — Room 없음'**
- 수리: ApplyHighQualityInterior가 GameObject.Find 대신 **빌더 반환 Room 직결**(8빌더 switch) + 빌더 시작/완료/예외 3종 진단 로그 + try/catch 격리. Room 실패 시에만 MedievalShell 폴백(성공 시 셸이 설계 실내를 덮지 않음). Barn(void 반환)은 Find 폴백.

### P20-3 선택 링 — IMGUI 원 잔존 + 반원 호
- 뿌리: SelectionRing 셰이더 Shader.Find 실패 → EarthTrail(TrailRenderer=반원 호) 폴백 + 폴백 시 IMGUI 파란 원 병기.
- 수리: SelectionRingController에 **절차 링 텍스처 폴백**(완전 원 보장) + GuardSelectionManager **링 경로 강제**(_usingRing=true, EarthTrail 폴백 제거) + IMGUI 원 **항상 생략**.

### P20-4 화살 — 트레일 제거 + 2차 축소
- TrailRenderer 완전 삭제(Awake 설정/Spawn 색상/박힌 후 off 전부), 실린더 (0.18,1.3)→**(0.12,0.9)**, GLB 목표길이 2.6→**1.8m**, 촉/깃 비례 축소.
- 방향: Spawn/Update 동일식(LookRotation×Euler90) 유지 + **스폰 정합 진단 로그**(up·dir dot, ±1 정상) — 재수리 없이 즉별 가능.

### P20-5 장비창 — 슬롯 옆 텍스트 제거
- 부위명/아이템명 라벨 전면 제거, 슬롯(아이콘+등급)만 중앙 정렬 그리드. 설명은 호버/클릭 시 중앙 설명창 위임.

### P20-2 Ctrl 커서 — 가시성 보정 + 진단
- ContextCursorSystem 앵커 5m→2.2m, 아이콘 0.12→0.22(멀어서 작아 안 보였던 것). CursorVisibilityController Ctrl 첫 홀드 1회 진단 로그(OS 커서 상태/아이콘 존재).

### P20-7 Ctrl+우클릭 이동 — 진단 + 지점 링 신설
- IssueRightClickCommand 수신 로그(selected/ctrl/mouse)+카메라 null 경고 — 침묵 실패 제거(영상 세션 로그에 명령 흔적 0 = 수신 자체 미확인 상태였음).
- **CommandMarker 신설**: 명령 지점에 소형 링(SelectionRing 셰이더/절차 폴백, 1.5초 페이드+축소) — 요구사항 충족.

### 검증
- 배치컴파일 CS=0(중간 3건 수리: void 빌더 캡처/이중 블록/iconSlot 선언), EditMode **288/288**.

### Play 판정 대기
①I키 → 설명창 정중앙(아이콘+설명) ②Ctrl → OS 커서+컨텍스트 아이콘 ③부대 선택 → 완전 원 링만(IMGUI 원/반원 소멸) ④화살 = 꼬리 없음·작아짐·조준 방향 일치(로그 up·dir=±1) ⑤장비창 슬롯만 ⑥실내 = 설계 실내(HQ)+빌더 로그 ⑦Ctrl+우클릭 → 이동+지점 링(수신 로그 확인)

---

## 📌 세션 스냅샷 (2026-09-20 ✅ P19 — 실내 별개 씬 분리(월드 언로드) — 커밋 328aad1e)

> **입력**: 사용자 — "로딩이 있어도 별개 씬으로 분리하자. 실내 진입은 플레이어만, 병사는 메인 씬 재진입 시 다시 보이게".

### 구현 (IndoorSceneTransition.cs + IndoorEnterRunner.cs)
- **진입**: 씬 이름/복귀 위치 저장(기존) → IndoorScene Additive 로드 → 플레이어 이동 **확인 후** 월드 UnloadSceneAsync → 실내 단독 구동. 월드 렌더러/AI/VFX 비용이 실내 동안 완전 제거(렉 해소 핵심).
- **퇴출**: 월드 재로드(Additive) → 활성화 → 플레이어 복귀 위치 복원+씬 이동 → IndoorScene 언로드. 병사/몬스터는 월드 소속이라 언로드 시 정리, **재입장 시 씬 셋업이 자동 재스폰**(사용자 확정 설계).
- **실내→실내 전환(성 내부 상점/크래프트하우스)**: 월드 복귀 상태(_previousSceneName/_returnPosition/_worldUnloaded) 보존 + 플레이어를 DontDestroyOnLoad 임시 홀더로 피난 → IndoorScene 언로드 파괴 방지. ※기존 Additive 방식의 잠재 파괴 버그(재진입 시 플레이어가 실내 씬에 갇혀 러너 300프레임 실패)도 함께 회피.
- **지연 스폰 경로**: IndoorEnterRunner 이동 완료 후 OnPlayerSettledIndoor() 호출로 월드 언로드 트리거(성공/러너 양경로 커버).
- P14 게이트/진입 정리(ClearWorldAggroAndCommands)는 안전망으로 유지.

### 검증
- 배치컴파일 error CS=0(중간 Scene.scene 접근 CS1061 1건 수리), EditMode **288/288**.

### Play 판정 대기
①E키 진입 시 로딩 후 월드가 내려가는지(Stats로 렌더 부하 감소 확인) ②나갈 때 원래 위치 복귀+병사 재등장 ③실내→실내(성 내부 상점→크래프트하우스) 전환 ④렉 체감 개선

---

## 📌 세션 스냅샷 (2026-09-20 ✅ Milestone G — 비밀 통행증 사용 경로 UX + 검증)

> **입력**: "진행" (F 이후 통행증 사용 경로 + Play 검증).

### Milestone G 구현
- **비밀 통행증 사용 경로 UX**: `InventoryWindowUTK.OnSlotRightClick`에 SecretPass 분기 추가 — 우클릭 시 `SecretShopSystem.ConsumeSecretPass()`(통행증 1개 소모+`Active=true`) → 그리드 새로고침 → `ShopWindowUTK.Open()`(멱등).
- **ShopWindowUTK 멱등 Open**: `public static Instance` 트래킹 추가. `Open()`이 열려 있으면 기존 인스턴스 Show+RefreshBuyList+UpdateGoldDisplay 재사용(중복 부착 방지), `OnWindowClosed`에서 `Instance` 해제. → 통행증 사용 시 이미 열린 상점 창에 비밀상점 섹션이 즉시 반영.

### 검증
- `compile_test.sh`: 성공(컴파일 오류 0).
- `run_tests.sh editmode`: ✅ EditMode 테스트 전부 통과.

### 남은 마일스톤
H(전체 Play 검증 — 비밀상점 구매·캐스팅 진행바·희귀광물·무기 "?" 제작 실기동 확인). 그랜드 플랜 A~G 기능 구현은 완료.

---

## 📌 세션 스냅샷 (2026-09-20 ✅ Milestone F — 캐스팅 애니 + GLB 부재 목록 + 비밀상점 UI + 컴파일 수정)

> **입력**: "진행" (E 이후 남은 마일스톤). E의 남은 항목 + 컴파일 오류 1건 수정.

### Milestone F 구현
- **채집/광질 캐스팅 애니 연결**: `GuardTaskSystem.BeginChannel`에서 `GetComponent<RigAnimationController>().SetState(AnimationState.Gather)`, `EndChannel`에서 Idle 복귀(CurrentState==Gather일 때만). 캐스팅 내내 채집/광질 자세 유지(진행도 바와 병행).
- **비밀상점 UI 배선**: `ShopWindowUTK.RefreshBuyList`에 `SecretShopSystem.Active` 시 최상단 전용 섹션(🔮 헤더 + Stock 순회 `BuildSecretRow`). `BuildSecretRow`는 itemId→`GetItemById` 해석, price 표시, `SecretShopSystem.TryBuy` 직접 호출·골드 갱신·목록 새로고침. using `ProjectName.Systems` 추가.
- **GLB 부재 목록 산출 도구**: `Assets/Editor/ItemGLBAudit.cs`(신규) — 메뉴 `Tools/Item Audit/GLB 부재 목록 산출`. PlayerInventory 정적 ItemData 리플렉션 수집 → `RuntimeModelLoader.HasModel`(item.id 직접+접두 제거 관례)로 GLB 부재 판정 → `Assets/ItemGLB_MissingList.txt` 출력+로그.
- **컴파일 오류 수정**: `ContextCommandRouter.AssignWorkTask`의 중첩 열거형 `GuardTask` → `GuardTaskSystem.GuardTask` 정규화(CS0246/CS0103×4 해소).

### 검증
- `compile_test.sh`: 성공(컴파일 오류 0).
- `run_tests.sh editmode`: ✅ EditMode 테스트 전부 통과.

### 남은 마일스톤
G(비밀 통행증 사용 경로 → 비밀상점 활성 UX + 밸런스·전체 Play 검증). GLB 부재 목록은 에디터 메뉴 실행으로 확인 가능.

---

## 📌 세션 스냅샷 (2026-09-20 ✅ Milestone E — 채집/광물 캐스팅(채널링) + 월드 진행도 바)

> **입력**: "채집/광물이 바로 되는 게 아니라 일정 시간 애니 유지 후 획득 + 진행도 UI".

### Milestone E 구현
- **`WorkProgressBar.cs`**(신규, Systems): 작업 유닛 위 얇은 월드 진행 바(발 위 2.5m). `Show(worker,dur,color)` → 0→1 채우며 `LateUpdate` 빌보딩(카메라 정면), 완료/`Close()` 시 자동 소멸. 프리미티브 콜라이더 제거 → 커서/RTS 레이캐스트 오염 0.
- **`GuardTaskSystem` 채널링**: RoutineMine/Gather를 **캐스팅 방식**으로 전환
  - 노드 근접(≤2.5m) → `BeginChannel`(광질 2.5s/채집 2.0s) + 진행도 바 표시
  - 캐스팅 중엔 수확 없음(딕셔너리 `_channelStart/_channelNode/_channelDur/_channelBar`), 완료 시 1회 수확(`PerformMine`=TryAutoMine+보너스 희귀 광물 / `PerformGather`=GatheringSystem)
  - **취소**: 작업 전환(AssignTask)·해제(ReleaseTask)·사망/비활성(Update) 시 `EndChannel` → 진행 바 정리, 보상 없음
- (캐스팅 애니메이션 자세는 RigAnimationController.Gather/드라이버 연동을 후속 폴리시로; 진행 바가 시각적 캐스팅 표시 담당)

### 검증
- RoutineGather/Mine 정의 1개씩(중복 제거), 두 파일 괄호 균형 OK. 변경: WorkProgressBar(신규)+GuardTaskSystem.

### 남은 마일스톤
F(채집/광물 캐스팅 애니 자세 보강 + GL·애니 부재 목록 산출기 + 비밀상점·통행증 UI 배선 + 밸런스·검증).

---

## 📌 세션 스냅샷 (2026-09-20 ✅ Milestone D — 크래프트 UI 성공률·운·"?"·레시피획득 (무기 벤치))

> **입력**: "크래프트 UI에 확률 표시(운↑성공↑)·희귀도/.레시피 보유시 확률·미보유시 ?·실험조합 성공 시 레시피 획득".

### Milestone D (무기 벤치 슬라이스)
- **CraftBenchBaseUTK**: `BenchRecipe.rarity` 추가(성공률 희귀도 페널티), 가상 `IsDiscovered(BenchRecipe)`(기본 true)·`RateHint(BenchRecipe)`(기본 ""). **레시피 북**: 미발견 레시피는 아이콘 감춤 + "?" 표시(재료는 실험 힌트로 유지), 발견 시 이름+`RateHint`(성공률/운). **매치/결과**: 미발견 조합이면 아이콘·이름 숨기고 "? 정체불명 — 성공 시 레시피 획득", 발견 조합이면 아이콘+이름+확률+상태라벨. 제작 성공 시 `CraftWeapon`이 `MarkDiscovered` → 공개(레시피 획득).
- **WeaponForgeUTK**: 레시피에 rarity 세팅, `IsDiscovered`=`RecipeDiscoverySystem.IsDiscovered(ResultName)`, `RateHint`=`ComputeFinalCraftChance(WeaponBaseSuccessRate=90, rarity)` + `GetLuckCraftBonus`(운) 표시 "성공률 n% (운 +k%)".
- (연금/요리 벤치도 같은 오버라이드 패턴 적용 가능 — 문서 안내.)

### 검증
- using/심볼(RecipeDiscoverySystem.IsDiscovered, ComputeFinalCraftChance, WeaponBaseSuccessRate, GetLuckCraftBonus, ItemRarity, Mathf) 해석 확인, 두 파일 괄호 균형 OK.

### 남은 마일스톤
E(채집·광물 채널링+진행도UI/애니) / F(GL·애니 부재 목록 산출기+밸런스) + 비밀상점·통행증 UI 배선(SecretShopSystem→상점창).

---

## 📌 세션 스냅샷 (2026-09-20 ✅ Milestone C — 희귀 광물 드랍 · 비제작 최상위 · 비밀상점)

> **입력**: "만들 수 없는 무기/방어구·비밀레시피·희귀광물(가치↑드랍↓)·레시피 디스커버리".

### Milestone C 구현 (데이터·로직 계층 — UI는 Milestone D)
- **희귀 광물 드랍**: `ResourceNode`에 `ResourceType.Silver/Gold/Mythril` 추가 + `GetItemData`(은/금/미스릴) + `TryRollRareBonus`(보너스 희귀 광물, 가치↑=한 단계 위, `_rareBonusChance` Inspector·기본 0). `GuardTaskSystem.RoutineMine`이 채광 성공 후 보너스 굴림 → 인벤 적립.
- **비제작 최상위**: `PlayerInventory`에 `weapon_unique_abyss`(심연의 마검)·`weapon_unique_dawnblade`(여명의 성검)·`armor_unique_voidplate`(공허 판금) — **Unique 등급**, 제작 레시피 없음(비제작). `NonCraftableCatalog`(Core): weapon_legendary 포함 비제작 ID 목록.
- **비밀상점**: `SecretShopSystem`(Systems) — `Active`/`Reveal`/`Hide`, **`ConsumeSecretPass()`**(item_secret_pass 소모→등장), Stock(비제작 최상위 4종, 가격 5k~20kG), `TryBuy`(Active+골드 충분 → PlayerStats.SpendGold → 지급, 실패 롤백). `item_secret_pass`(비밀 통행증) 아이템 등록.
- `RecipeDiscoverySystem`(기존, PlayerPrefs) — 디스커버리 "?" 연동 마련(Milestone D에서 CraftBench UI 표시).

### 검증
- 5파일(PlayerInventory/NonCraftableCatalog/SecretShopSystem/ResourceNode/GuardTaskSystem) 괄호 균형 OK, SpendGold/AddGold/GetItemCount 시그니처 대조, 심볼 해석 확인.

### 남은 마일스톤
D(크래프트 UI 확률·운·"?"·레시피북 + 비밀상점·통행증 UI 배선) / E(채집·광물 채널링+진행도UI) / F(목록 산출기+밸런스).

---

## 📌 세션 스냅샷 (2026-09-20 ✅ Milestone B — 광물·장신구·판금/가죽 재료 + 광물 기반 레시피 확충)

> **입력**: "광물 캐면 무기 제작 재료 / 판금·가죽 / 더 많은 무기 레시피 + 데이터 저장 / 장신구(반지·목걸이 %버프) / 운 성공률 / 레시피 디스커버리".

### Milestone B 구현
- **PlayerInventory**: `ItemCategory.Accessory` 추가 + 신규 아이템 등록(Tiered 헬퍼 병행):
  - **광물**: mat_wood(통나무)/mat_stone(석재)/iron_ore(철광)/iron_ingot(철괴)/mat_silver_ore(은)/mat_gold_ore(금)/mat_mythril_ore(미스릴)/**crystal_shard(수정석)** — 희귀도: 철광Uncommon·은Uncommon·금Rare·미스릴Epic·수정Rare.
  - **장신구**: ring_evasion(회피+3%)/ring_vitality(체력+10%)/ring_power(공격+6%)/necklace_hp(체력+15%)/necklace_guard(방어+8%) — Uncommon~Epic.
- **AccessoryDefinitions.cs**(신규, Core): id→스탯종류(Evasion/MaxHp/Attack/Defense)+% 값 데이터 맵(장착 연동은 후속).
- **WeaponCraftDatabase**: 레시피 대폭 확충(+21) — **Stone 티어 4종(mat_stone+늑대이빨)**, **Crystal 티어 4종(crystal_shard+미스릴)**, **방어구 세트(helmet/armor × wood/stone/steel/crystal — 광물 재료)**, **장신구 5종(은/금/수정/미스릴)**. → **광물이 곧 무기/방어구/장신구 제작 재료**로 전방위 연결.
- **ResourceNode**: Wood→`mat_wood`, Stone→`mat_stone` 산출 정렬(크래프트 재료 계열화). 희귀 광물(은/금/미스릴/수정) 고난도 노드는 Milestone C(희귀 드랍)에서.
- RecipeDB_SO(에디터 메뉴 Tools/Crafting/Generate RecipeDB)로 정적→SO 데이터 마이그레이션하면 위 레시피 전부 데이터로 전환.

### 검증
- 결과 아이템 ID 전부 등록 확인(`Tiered()` 카탈로그 — GetItemById 해석되어 EnsureValid 통과), 4파일 괄호 균형 OK. 변경: PlayerInventory/AccessoryDefinitions/WeaponCraftDatabase/ResourceNode.

### 남은 마일스톤
C(비밀상점·비제작 최상위·희귀광 드랍·레시피 디스커버리 "?") / D(크래프트 UI 확률·운·레시피북) / E(채팩·광물 채널링+진행도UI) / F(목록 산출기+밸런스).

---

## 📌 세션 스냅샷 (2026-09-20 ✅ 통합 계획 — Milestone A: 데이터·운·희귀도 성공률 기반)

> **입력**: 사용자 "광물/재료/채집/희귀/레시피/진행도UI 전방위 — 만들 수 없는 최상위 무기·비밀레시피·희귀광물·운 기반 성공률·레시피 디스커버리·장신구·.GLB부재목록" 그랜드 플랜.

### 계획 (구현 완료된 것만 본 스냅샷)
**✓ Milestone A (이번 완료)** — 데이터·운·희귀도 성공률 기반:
- `PlayerStats`: **Luck 신설**(PlayerPrefs player_luck_v1, GetLuckCraftBonus = Luck×2%p, Set/AddLuck). 
- `Recipe.cs`: `rarity` 필드(ItemRarity.Common 기본). CalculateSuccessRate에 운 보너스+희귀도 페널티 반영, clamp 5~95.
- `CraftingHelper`: 공용 상수 Min5/Max95, `GetRarityPenalty`(Common0/Unc-8/Rare-18/Epic-30/전설-45), `ComputeFinalCraftChance(base,rarity)`(PlayerStats null 안전), `GetRarityLabel`. **CraftWeapon 확정100%→운·희귀 기반 성공 롤**(실패 시 재료 손실모델 30%보존/50%@1/20%전손) + message에 희귀도·성공률 표기.
- `WeaponCraftDatabase`: WeaponRecipe에 `rarity` 추가(wood=Common/steel=Uncommon), `AllIncludingRaw` 추가, **RecipeDB_SO(Resources/CraftingRecipeDB) 우선 → 정적 폴백**.
- **신규 `RecipeDB_SO.cs`**(ProjectName.Core.Data): [CreateAssetMenu] 데이터 기반 레시피(RecipeEntry class: ResultId/Mat1Id/Count/Mat2Id/Count/RequiredLevel/rarity/category), `Instance`(Resources.Load, null 안전), `TryMatchResult`, **에디터 메뉴 Tools/Crafting/Generate RecipeDB**로 정적→SO 마이그레이션.
- **EditMode 테스트** `CraftChanceTests.cs`: ComputeFinalCraftChance(0운 Common=base), 희귀도 단조 감소, GetRarityPenalty 정확값, clamp 5~95, RecipeDB TryMatchResult.

### 남은 마일스톤 (다음 턴 진행 예정)
- **B**: 신규 광물(silver/gold/mythril/crystal_shard)·장신구(반지/목걸이 %버프)·판금/가죽 아이템 등록 + 광물 기반 무기/방어구/장신구 레시피 확충(RecipeDB_SO에 데이터 행) + 사용자 GLB 온보딩(아이템ID/그립/레시피).
- **C**: 레시피 디스커버리("?" 블라인드·실험조합 성공 시 획득·레시피북 확률표시) + 운 표시 / 비밀상점·비제작 최상위 무기/비밀레시피·희귀 광물 드랍.
- **D**: 크래프트 UI(CraftBenchBaseUTK) 확률·운·"?"·레시피북 통합.
- **E**: 채집/광물 채널링(애니+월드 진행도 바) — 이전 계획.
- **F**: GLB 부재 목록·애니 부재 목록 런타임/에디터 산출기 + 문서화, 밸런스·검증.

### 검증
- 5파일(PlayerStats/Recipe/CraftingHelper/WeaponCraftDatabase/RecipeDB_SO) 괄호·문법 균형 OK, LoadLuck Awake 호출 확인, ComputeFinalCraftChance null 안전.
- 서브에이전트 600s 타임아웃 → 부모 직결 완성(RecipeDB_SO·테스트 추가).

---

## 📌 세션 스냅샷 (2026-09-20 ✅ 부대 선택 링 — SC2/RTS식 고품질 원형 링)

> **입력**: 사용자 "단순 파란 원/오라 대신 VFX graph+Shader graph로 원형 링(SC2처럼) 고품질로".

### 결정
- VFX/Shader **Graph 직렬화 자산(.vfx/.shadergraph)은 에이전트 자율 환경에서 렌더 검증 불가·손저작 취약** → 같은 고품질 시각을 **100% 보장하는 ShaderLab URP 셰이더**(SelectionRing.shader) + 절차 Quad로 신뢰 구현. 그래프 자산을 직접 요구하면 에디터 스캐폴드 별도 안내. (패키지는 visualeffectgraph/shadergraph 17.4.0 설치 확인됨 — 추후 에디터 전환 용이.)

### 신규
- **`Assets/Resources/FX/Selection/SelectionRing.shader`** — URP Additive(Blend One One/ZWrite Off/Cull Off). 평면 Quad 링 UV 마스크: 내/외측 GlowWidth 글로우 + RingThickness 밴드 + Time 펄스(±28%) + **회전 호 하이라이트(뒤 28% 구간, SC2 감성)** + `_TeamColor` 팀컬러 × `_Intensity`. 처리 순서 height=0 평탄.
- **`Assets/Scripts/Systems/SelectionRingController.cs`** — Awake에서 Quad 절차 생성(콜라이더 제거→레이캐스트 오염 0, XZ평면 눕힘), 셰이더 머티리얼 생성, `SetColor(Color)` 팀컬러 주입, 파괴 시 머티리얼 정리.
- `.meta` 생성. (.prefab은 절차 생성으로 대체 — 손저작 비신뢰.)

### GuardSelectionManager 통합 (파일 수정)
- `SyncSelectionAuras`: `Shader.Find("Custom/SelectionRing")` 우선 → 링 셰이더 있으면 `CreateSelectionIndicator`(new GO + SelectionRingController, 국가색 SetColor, 스케일=병사 localScale×1.3, 추종), **없으면 기존 EarthTrail→MagicCircle2→Buff 폴백 체인**(회귀 0).
- `SetNationSelectionColor(동=빨강/서=파랑/남=초록/북=보라/기본=파랑)`.
- `OnGUI`: **링 활성 시 레거시 IMGUI 파란 원 생략**(`if(!_usingRing)`), 드래그 박스/H/선택 로직 무변경.

### 검증
- `Nation:string`(GuardPlaceholder 535행), 괄호/문법 균형 OK, 기존 선택/드래그/우클릭/H키 로직 무수정(diff 확인).
- 신규 4파일(+메타) + GSM 수정. 기타 파일 무수정.

### Play 판정 대기
Test_10 내 영지 병사 Ctrl+드래그 선택 → 발밑에 **회전 호 하이라이트+펄스+팀색 원형 링** 표시, 이동 시 추종, 해제/사망 시 사라짐. IMGUI 파란 원 대체, EarthTrail 미출력. (기존 폴백 안내)

---

## 📌 세션 스냅샷 (2026-09-20 ✅ 부대지정 컨텍스트 명령 시스템)

> **입력**: 사용자 "Ctrl 누르면 커서 등장, 병사 지정 후 Ctrl+우클릭=이동, Ctrl+몬스터호버=검커서+Ctrl+좌클릭=병사공격, H=홀드, 농사/채집/광질도 동일(호미/낫/곡괭이 커서)".

### 실측: 기반 대부분 이미 구현돼 있었음
- Ctrl→OS커서 표시 `CursorVisibilityController` ✓ / 컨텍스트 커서 `ContextCursorSystem`+`HoverTargetClassifier.ClassifyAt` ✓ / Ctrl+우클릭 이동·H정지 `RTSCommandSystem` ✓ / 적 위 좌클릭 병사공격 `ContextCommandRouter`(Enemy만) ✓ / 병사 작업 `GuardTaskSystem`(Gather/Farm/Hunt 자동) ✓ / 농경지·약초·광석 노드 `FarmPlot`+`FarmingSystem`·`GatheringSystem`·`ResourceNode(TryAutoMine)` ✓

### Phase 1~4 (4파일 수정 / +221)
- **ContextCommandRouter.cs**: 좌클릭 명령을 **Ctrl 홀드+단순클릭(이동량≤10px)+UI아님+병사선택**일 때만 발화. down대기→release 이동량으로 단순클릭/드래그(선택) 구분해 GSM의 Ctrl+드래그 박스선택과 공존. 분기: Enemy=RTS공격 / Farm→GuardTask.Farm / Gather→Gather / Mine→Mine (선택병사 전원 AssignTask+좌클릭소비 consumeLeftClickAsDrag/ContextCommand).
- **HoverTargetClassifier.cs**: `TargetKind.Mine` append. ClassifyOne 최우선 `GetComponentInParent<ResourceNode>()`→Mine(적/밭보다 먼저).
- **ContextCursorSystem.cs**: `BuildHoe()`(호미) 신규, **Farm=호미**, **Mine=곡괭이**(이관), Gather=삽, Enemy=칼, 지형=화살표. _icons에 Mine 추가(7키).
- **GuardTaskSystem.cs**: `GuardTask.Mine` append + `RoutineMine`(FindNearestResourceNode 15m→이동→TryAutoMine→PlayerInventory.AddItem 가능→쿨다운3s) + MineCooldownSec/MineRange 상수.

### Phase 5 — 테스트 씬 광질 노드 배치 (TestTerritoryCombatSetup.cs 추가만)
- `SetupMiningNodes()`: 내 영지(East_01) 농장 center(0,18) 기준 Wood(-3,3.5)/Stone(0,2)/IronOre(+3,3.5) 지그재그. y=SurfaceY+0.3, Cube+URP색+BoxCollider, `_resourceType` Reflection 설정(HerbPickup 선례). 기존 코드 무수정(순수 추가). Test_10엔 이미 농경지 2×2·약초 있음 → 세 작업 모두 Play 테스트 가능.

### 검증
- 사용 심볼(ResourceNode.TryAutoMine/IsAvailable, FarmPlot, HerbPickup, GuardTaskSystem.AssignTask/Ensure, GuardSelectionManager.SelectedGuards/consume플래그, UITransitionState.PointerOverUI) 전부 원본 대조 통과.
- 4파일 괄호/문법 균형 OK (파이썬 문자열·주석 제외 카운트). diff 4파일(+221)+ProjectSettings(기존 노이즈, 미커밋).
- 수정 금지 파일(PlayerCombat/GuardSelectionManager/GuardSquadHotbar/AnimalAI/애니컨트롤러) 무변경 확인.

### Play 판정 대기
①Ctrl 누르면 커서 등장+호버 종류별 커서(칼/호미/낫/곡괭이/화살표) ②병사선택(Ctrl+드래그)→Ctrl+우클릭 이동 ③Ctrl+몬스터좌클릭 병사공격 ④H=홀드 ⑤농경지/약초/광석에 각 Ctrl+좌클릭 → 병사가 농사/채집/광질+인벤 적립. (Test_10 씬에서 성 앞 부지 테스트)

---

## 📌 세션 스냅샷 (2026-09-20 ✅ 애니메이션 쇼케이스 테스트 씬 추가)

> **입력**: 사용자 "테스트 씬 하나 더 만들고 모든 몬스터/병사/NPC 각 한 명씩 구분지어 배치, 각 애니메이션 시험용."

### 신규 파일 (기존 파일 무수정)
- `Assets/Scripts/Systems/TestAnimationShowcaseSetup.cs` (+ .meta GUID `7f3a9c2e1b4d5f6a8c0d1e2f3a4b5c6d`)
- `Assets/Scenes/TestScenes/Test_11_AnimationShowcase.unity` (+ .meta) — 설정 GO `_TestAnimShowcase` + Main Camera(탑다운 0,45,-30 / 60°)

### 구성 (런타임 Awake 생성, 기존 시스템 재사용)
- 지형/라이트/스카이박스: TestAllInOneSetup 패턴
- **몬스터 존(Z=-15, 간격2.2, 군 간격1.0)**: MonsterDatabase 22종 각 1. **외형 유사 6군으로 재배치**(①작은 털4족: rabbit/deer/wolf/boar/giant_rat/electric_porcupine ②파충류: poison_snake/fire_lizard/salamander/swamp_croc ③조류·비행: crow/bat/griffin ④점액·정령·영혼: slime/forest_spirit/banshee ⑤거대 인간형괴수: stone_golem/wild_troll/ogre/minotaur ⑥신화하이브리드·은신: manticore/shadow_assassin). 군 경계마다 MonsterGroupStarts{0,6,10,13,16,20}로 1.0m씩 gap 누적(총 +5m). MonsterSpawner.CreateMonster 로직 재현(ModelAnimatorAssigner + AnimalAI.SetMonsterId + SpecialCreatureAnimator 분기). **22종 전부 리깅 GLB 실존**(GetMonsterModelPath 22종 맵 전부 `UserProvided/*_Rigged.glb` 존재). 프리미티브 폴백은 어떤 모델로도 안 잡힐 때만 동작. 라벨 "몬스터: 한글명/id"
- **병사 존(Z=0, 간격3)**: lv1-20/20-40/40-50 3모델. TestTerritoryCombatSetup.CreateGuard 패턴(FBX + SoldierShield_AC + HumanoidClipDriver(Soldier) + GLB 재질 이식). 색/라벨 구분
- **NPC 존(Z=15, 간격2.4)**: lord/king/shop/man1/2/girl1-3/oldman1/2/dracula 11종. TerritoryNPCSpawner 패턴(GLb + 병사 Humanoid FBX 교체 + SoldierShield_AC + 드라이버)
- 접지: SurfaceY 대신 Physics.Raycast(GroundY) + GroundModelToY(bounds 최저점 정렬)
- **각 몬스터 독립 배회 애니(2026-09-20)**: 원래 AnimalAI 부착했으나, AnimalAI는 Player 부재 시 Update에서 Idle+속도0으로 되돌려 22종 전부 얼어붙음(AnimalAI.cs 503~514행). → AnimalAI 대신 신규 **ShowcaseWanderDriver.cs** 부착 — 스폰 leash(2.5m) 내 랜덤 배회(도착 시 1.2~2.8s idle), 마리별 속도/지연 랜덤 변주로 22종 각자 독립 걷기/대기. 애니 피드: 4족=QuadrupedProceduralAnimation.SetAiDriven+SetMovementSpeed+ApplyMonsterProfile / 2족=ProceduralAnimationController.SetVelocityProvider(this·IVelocityProvider)+ApplyMonsterProfile / 특수형=SpecialCreatureAnimator 자율 / Rig 보유 시 RigAnimationController.SetState(Walk/Idle). 지연 연결 타임아웃 3s. API 시그니처 전부 원본 리드로 대조 통과.

### 검증
- 모두 **기존 프로젝트 심볼 전수 대조** 통과: MonsterDef(gizmoColor/id/displayName/isQuadruped), MonsterDatabase.Get, AnimalAI.SetMonsterId, GuardPlaceholder.SetGuardInfo/SetRecruited, NationType.East, HumanoidClipDriver(DriveMode.Soldier/CopyMaterialsFromGlb), GuardManager.NormalizeSoldierScaleToPlayer/GetSoldierSizeMultiplier, ModelAnimatorAssigner.ForceBiped, SpecialCreatureAnimator(ProjectName.Systems.Animation.Procedural, CreatureType{Spider,Clam,Slime,Spirit,LargeMonster})
- 코드 본문(주석/문자열 제외) 괄호/중괄호/대괄호 균형 OK
- 씬 YAML: 설정 GO + MainCamera 태그 + 스크립트 GUID(7f3a...) 일치

### Play 판정 대기
①씬 열고 재생 시 3개 존이 화면에 다 보이는지 ②각 유닛 애니(병사/NPC idle, 몬스터 22종 idle) 재생 ③라벨 표시 ④⚠️전 4종만 있다고 잘못 기록했으나 **22종 전부 리깅 GLB 실존**(narrow 파일필터 오판 → 정정)

---

## 📌 세션 스냅샷 (2026-09-20 ✅ 화살 액션 수정 — 크기 축소 + 조준 방향 버그)

> **입력**: 사용자 "화살이 너무 큼 조금 작게" + "화살이 조준한 방향으로 안 나감".

### 화살 수정 (ArrowProjectile.cs)
- **크기 축소**: `localScale (0.25, 1.8, 0.25) → (0.18, 1.3, 0.18)` (67행). 기존 탑다운 대형화 값을 사용자 요청대로 한 단계 축소, 가시성 유지.
- **GLB 모델 길이 일치**: `ArrowModelTargetLength 3.6 → 2.6` (190행) — 실린더 Y 1.3(단위높이 2)과 재일치(2×1.3=2.6m). 크기 축소 시 모델/실린더 시각 불일치 방지.
- **조준 방향 버그(근본)**: `Update()`의 `transform.forward = _rb.linearVelocity.normalized`가 Spawn에서 조립한 축 정렬(`LookRotation(dir)*Euler(90,0,0)`, 촉을 진행축에 맞춤)을 **매 프레임 덮어써** 화살 몸통이 진행 방향과 90° 어긋난 채 날아갔음. → Spawn과 동일한 복합 회전 `transform.rotation = LookRotation(vel)*Euler(90,0,0)`으로 교체(314행). 비행 내내 촉이 진행 방향 유지.
- ⚠️ **기법**: 회전을 속도 방향으로 정렬할 땐 반드시 Spawn과 동일한 복합식을 쓸 것(`transform.forward`(+Z)는 촉이 다르게 정렬된 화살에선 90° 어긋남).

### 검증
- 정적 검증 완료: Spawn(66행)·Update(314행) 회전식 동일 확인, 문법 훼손 없음.
- ArrowSystemTests.cs는 localScale/rotation/ArrowModelTargetLength 값을 **단언하지 않음**(발사 시 소모 개수만 검증) → 크기/회전 변경에 회귀 없음.
- code agent 편집 → QA agent 시도(Unity 배치 테스트는 에디터 잠금/600s 타임아웃으로 실행 보류, 정적 검증으로 대체). 에디터 미점유 확인됨.

### Play 판정 대기
①화살이 조준 커서 방향으로 직진하는지(몸통 진행축 정렬) ②화살 크기가 부자연스럽게 크지 않은지. 에디터 재컴파일 후 실측 권장.

---

## 📌 세션 스냅샷 (2026-09-20 ✅ P16 잔여 수리 + P17 실내 고품질 — 커밋 6035048f)

> **입력**: 사용자 4건(창 옆 배치/장비 아이콘/슬롯 반짝/실내 유입+멈춤) + 실내 예시 이미지 + 사용자 제공 텍스처 7종.

### P16 잔여 수리
- **P16-1 창 옆 배치**: SoldierInteractUTK.PlaceNearGuard — 병사 월드좌표→패널 좌표(카메라 WorldToScreenPoint×스케일+y플립), 병사 우측 20px, 화면 밖 폴백(좌측/클램프). 200ms 폴링으로 병사 이동 추적.
- **P16-2 장비 아이콘**: 인벤 장비 패널 8슬롯에 UTKSlot(44px) — 장착 아이콘+등급 테두리 실시간 갱신(_equipSlotIcons 딕셔너리), 텍스트 라벨 보조 유지.
- **P16-3 슬롯 반짝/반쪽 짤림**: ①반짝 뿌리=인벤 250ms 폴링이 셀 Clear+재생성 → hover 상태 찰나 리셋 → **PointerOverUI일 때 폴링 재생성 스킵** ②짤림 뿌리=hover 글로우 PNG가 stretch-and-crop → **slice-enabled 48px 9슬라이스**.
- **P16-4 실내 유입 강화**: P14 게이트에 더해 **진입 순간 월드 정리** — AnimalAI.ClearAggro 전량 + 병사 ClearCommand/SetInCombat(false). 어그로 잔존/명령 잔존의 관성 이동까지 차단(이중 방어).
- **P16-5 크래프트 "멈춤"**: 실측으로 timeScale 경로 없음 확정 → 시각적 뿌리=전화면 검은 딤드(0.5)가 월드를 가리는 것. 수리: UIStyleManager.DimColor 0.5→0.18 + UIWindow 딤드 blocksRaycasts=false(월드 클릭 차단 해제).

### P17 실내 고품질 (예시 이미지 재현)
- **제공 텍스처 7종**(Assets/Resources/Indoor/) — vision_analyze 심리스 실측 확인(바닥 상하좌우 이음 0, 벽 석재 막장쌓기 심리스). 바닥 1장≈2.4m, 벽 석재 1장≈2.2×1.1m로 커버 스케일 확정.
- **IndoorTextureLoader(Core 신규)**: Resources.Load 7종 + 커버 미터 상수(타일링 단일 소스) + IncludePillars=false 기본.
- **IndoorMaterialFactory(Systems 신규)**: URP Lit 재질 — 노멀맵+Smoothness(바닥 0.55/석재 0.35/회반죽 0.3)+**실측 커버 기반 타일링**(방 크기÷커버 반올림). 벽은 석재 본체+상단 회반죽 밴드 쿼드(부모 벽 회전 상속).
- **배선**: IndoorSceneTransition.ApplyHighQualityInterior — Room 탐색(렌더러 바운드 실측)→머티리얼 교체+데칼+조명. **폴백: 파일 없으면 기존 절차 생성 유지(하위 호환)**.
- **짚단 데칼**: Unlit 투명 쿼드 9장, 지면 0.012 위, djb2(room instanceID) 결정론 배치/회전.
- **조명(P17-B)**: 앰비언트 0.45→0.22 웜 다크 + 웜 포인트라이트 2등(근사 2000K) + URP 소프트섀도우 ON(리플렉션 — API 버전 차이 회피).
- **기둥 제거(P17)**: IncludePillars=false 기본 — PlayerCastle/Castle 빌더 루프 바운드 0 처리. ⚠️ PC 빌더 기둥 뒤에 7(조명)/8(장식) 섹션이 있어 **return 금지, 루프 바운드만** 수정(초기 return 실수 발견 즉시 교정).

### 검증
- 배치컴파일 **error CS=0**, EditMode **280/280**. 커밋 6035048f 푸시.
- 중간 수리: mipmapLevel API 없음(CS1061), Systems using Core 누락(CS0103), softShadowsSupported API 미존재(CS1061→리플렉션).

### Play 판정 대기
①F키 상호작용 창이 병사 옆에 뜨는지+이동 추적 ②인벤 장비 아이콘 표시 ③슬롯 반짝/짤림 소멸 ④실내 진입 시 몬스터/병사 무유입(에디터 재컴파일 필수) ⑤크래프트 중 월드 보임+조작 가능 ⑥실내 텍스처 품질(바닥 입체/벽 투톤/짚단/웜조명/기둥 없음)

---

## 📌 세션 스냅샷 (2026-09-20 ✅ P10~P15 — 테스트 36 영상 실측 6건 수리, 커밋 a765a1a0)

> **입력**: Screenshots/테스트 36.mp4 + 사용자 리포트 6건. 영상 프레임 실측(f03/f08/f11/f14) + Editor.log 실측으로 뿌리 확정.

### P10. 창고 우클릭 출고 불가 (P9 잔여) — **dedupe 가드 int 오버플로우(근본 발견)**
- **Editor.log 실측**: 매 우클릭마다 `출고 시도 slot=N` 직후 `출고 중복 요청 무시 (-2086493883ms)` — 가드가 첫 클릭을 중복으로 오판.
- **뿌리**: `Environment.TickCount`(양수) − 초기값 `int.MinValue` → int 오버플로우로 큰 음수 → `diff<200` 항상 참 → **모든 우클릭 영구 차단**. (드래그 출고는 가드 없어 성공 — 로그 `출고(드래그) 성공=True`와 일치. P9의 3중 경로 수리 자체는 정상 동작하고 있었음.)
- **수리**: unchecked uint delta(랩어웨이 안전) 교체 + 창 닫힘 시 `_lastWithdrawMsPerSlot.Clear()`(재오피 후 첫 우클릭 보장).
- 교훈: **TickCount 기반 dedupe는 절대 `int.MinValue` 초기값 비교 금지** — 부팅 후 오래 경과하면 오버플로우. uint 차 패턴 표준.

### P11. 설명창 ESC/X 무반응 — Register 우회
- ItemDescriptionWindowUTK.Show()가 `style.display` 직접 조작 → UTKWindowManager 스택 미등록 → ESC(스택 최상단 Close)·X 버튼 모두 무효.
- 수리: base.Show()/Hide() 경유 전환 → ESC/X/I 3경로 전부 닫힘.

### P12. 화면 3분할
- 신규 `UTKThreeColumnLayout.cs`: UIRoot 폭 기준 좌(인벤)/중(설명)/우(창고) 1/3 컬럼. 고정 px(16/596/1044) 대체, 해상도/스케일 무관.

### P13. F키 병사 상호작용 + IMGUI 철거 (delegate_task 600s 타임아웃 → 부모 직접 완성)
- 영상 f14의 파란 창 = **GuardPlaceholder.OnGUI**(IMGUI, 1335줄 파일) — 말걸기/음식/약/포섭/닫기.
- 신규 **SoldierInteractUTK**(UTK): 말걸기/음식주기/약주기/**병사 정보보기**(→기존 GuardInfoUTK 경로)/닫기. 음식/약 선택 팝업도 UTK 스크롤 리스트로 재구현. 200ms 자체 폴링(4.5m 초과 자동 닫힘).
- GuardPlaceholder OnGUI 341줄 철거(OnTalk/OnRecruit/지급 로직은 public 래퍼 BeginTalk/BeginRecruit/GiveFood/GiveDrug로 재사용 — 데이터 불변). E키 병사 토글 제거(실내 진입 E 전용). F키 → SoldierInteractBridge.RaiseInteract 신설 이벤트.

### P14. 실내 진입 시 병사/몬스터 동반 유입 차단
- `UITransitionState.IndoorActive` 플래그 신설(Core 양방향 규약 — Systems→UI 직접 참조 CS0234 3건 발생 후 Core 경유로 수정).
- 게이트 3곳: GuardCombatAI.UpdateGuardBehavior(추적/전투 정지)·AnimalAI.Update(AI 완전 정지+속도 0 피드)·GuardPlacehold