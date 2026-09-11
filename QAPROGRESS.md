# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류를 잡아냅니다.
>
> **진행 방식:** 테스트 씬별로 시스템 격리 → Play 테스트 → 오류 발견 → 수정 → 기록
>
> **최종 갱신:** 2026-09-11 (33차)

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
- **후속(1072bba6)**: ①스탯창 NRE 루트원인 확정 — 뷰포트 오브젝트에 Image 존재 상태에서 RawImage AddComponent → 유니티 거부 null 반환(진단 로깅이 즉시 특정) → RawImage 전용 오브젝트 생성으로 수정 ②인벤/설명/컨텍스트 **화면 정확히 삼분활**(패널=S/3-12, 하단 170px 핫바 전용) ③장비창 5칸씩 2줄(실6+예약4) ④가방 5칸씩 6줄 스크롤 ⑤상점창 삼분활 적용. 에디터 실행 중으로 배치컴파일 보류 — 포커스 시 자동컴파일(콘솔 확인 필요)
- ⬜ **남음**: Play 판정(①핫바 글자 소멸 ②C키 은신 애니 ③I키 2패널+우클릭 장착 ④드래그→핫바 지정 ⑤창고 3패널 ⑥몬스터 처치→상자→전리품 ⑦P키 스탯창(안 뜨면 콘솔 로그 확인) ⑧상점 열면 [인벤][설명][상점 520폭] 우측 배치+구매/판매)

---

## 📌 세션 종합 스냅샷 (2026-09-09 2차 — 런타임 오류 3종 수정 + 스탯창 v2 + 미니맵 로컬뷰 ✅)

- **범위**: 유저 Play 보고(오류 3종+유령 선) + 요구 5건(스탯창 예시화/포인트분배/장비연동/미니맵 현재위치+로컬뷰/핫바 정리). 계획서: .hermes/plans/2026-09-09_status-v2-minimap-hotbar-fixes-plan.md
- **P0 오류 수정**: ①IdyllicDecoPlacer willowGreen/willowPink 미할당 NRE → BuildCategoriesR4 할당 복원+전 필드 인라인 초기화(f986134d) ②Player_AC AnyState 고아 전이 SwimEnter(파라미터 부재 경고) → **IsSwimming 조건으로 재배선+출구 전이 보강**(수영 비주얼 진입 경로 복구, Backup/Player_AC_backup_swimfix.controller 백업, 3558fd74) ③PlayerControls.actionMaps 깨짐 → 중복 애셋 없음·JSON 정상 확인, 런타임 폴백 동작 중(유지)
- **P1 진단(58.PNG 정밀 분석)**: ①"핫바 우측 유령 선" = **키박스(숫자1~8) 행의 앵커 버그** — CreateImage에 anchorMin(0,0)/anchorMax(0.5,0) 혼합 지정 → 스트레치 모드로 sizeDelta가 앵커 rect 폭에 가산, 박스가 패널 밖까지 이어짐(끝의 둥근 마감=라운드 키박스) ②좌하단 녹색 바 = GuardPlaceholder 병사 HP바(정상 기능, 무수정) ③미니맵 옆 슬라이더 = 온도 게이지(정상 기능) ④미니맵 플레이어 마커 미표시 = 전체맵 스케일 문제(Ring1 15m=1.6px → 중앙 겹침)
- **P2 핫바 수정**: 키박스 앵커 (0,0)-(0.5,0)→(0,0)-(0,0) 점앵커 통일
- **P3 스탯포인트+장비보너스**: PlayerStats에 PendingStatPoints(레벨업당 +5)·AllocateStat(힘+2공격/민첩+0.5%치명+0.05속도/지능+0.5%연금요리/체력+10HP)·PlayerPrefs 저장/복원. **Core→Systems 역참조 회피 위해 장비보너스는 푸시 구조**: EquipmentStatBonusApplier(신규, Systems)가 OnEquipmentChanged 때 테이블+키워드 폴백 집계 → PlayerStats.SetEquipmentBonuses() 푸시
- **P4 스탯창 v2**: 인벤토리 위치(중앙 1180x900)로 이전, 예시 레이아웃 재현 — 좌측 3D 뷰포트(RT 256x320+전용카메라 레이어31, 플레이어 클론 스크립트 전면제거 y=-2000 격리, 드래그 회전, 닫으면 즉시 파괴) + ㄷ자 장비슬롯 6종 + 우측 주스탯[+]분배/전투스탯/정보(하늘색 수치·버건디 타이틀)
- **P5 미니맵 로컬뷰**: 플레이어 중심 uvRect 크롭(규약 u=0.5+x/W, v=0.5+z/W — BakeWorldSplat 동일), 플레이어 중앙 고정+forward 화살표(+z=북=화면위), 영지/퀘스트 마커 로컬 반경 필터, 휠 줌 3단(80/120/200m), 플레이어 없으면 전체맵 폴백
- **검증**: 배치컴파일 **error CS=0**(중간 3회 실패 모두 수정: CS1676 in-delegate/CS0103 클래스명/CS7036·CS1503 헬퍼 시그니처), DLL 3종에 신규 타입 포함 확인, guid 잔존 0
- ⬜ **남음**: Play 판정(①콘솔 SwimEnter/NRE 경고 소멸 ②핫바 선 소멸 ③P키→중앙 캐릭터정보 창·3D회전·[+]분배·장비착용 시 스탯변동 ④미니맵 로컬뷰+방향화살표 ⑤레벨업 시 +5포인트 지급)

---

## 📌 세션 종합 스냅샷 (2026-09-09 1차 — 레벨/스탯 창(P키) 신규 구현 + 스텁 정리 ✅)

- **범위**: "로드맵 읽고 레벨 시스템과 스탯 창 구현 계획 → 진행". 조사 결과 **레벨 시스템(PlayerStats, Lv1~50 EXP테이블·파생스탯·OnLevelChanged)은 기존 완성+EXP획득원 7곳 연결** 상태 → 재구현 불필요. 실제 갭은 **스탯 창이 죽은 코드**(PlayerStatusWindow: 씬배치 0·인스턴스화 0·키 소비처 부재)
- **신규 StatusWindowUI.cs** (Assets/Scripts/UI/, namespace ProjectName.UI): HotbarUI 선례의 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 셀프부트(씬 의존 0) + 전용 StatusCanvas(sortingOrder 200 > 핫바 100) + 좌상단 패널(앵커 0,1/pivot 0,1/pos 12,-12, 438px). 표시: Lv·EXP게이지(누적식 정확)·HP·공격/방어/치명/이속(Final*)·연금/요리 보너스·화술·전투보너스·중독(DrugEffectSystem)·골드. **토글키 P**(전 프로젝트 KeyCode.P 사용 0건 검증) + ESC 닫기. OnLevelChanged 구독 → 즉시 RefreshDisplay + **LEVEL UP! 팝업**(static 진입, 2초 페이드, 연속 레벨업 정리 가드). Update에서 0.25s 폴링 갱신
- **QA 발견 치명 1건 수정**: PlayerStats는 DontDestroyOnLoad 제거됨(PlayerStats.cs:112) → 창 닫힌 상태 씬 전환 시 새 인스턴스에 재구독 안 되어 레벨업 팝업 누락 → `Update()`에 `EnsureLevelSubscription()` 추가(ReferenceEquals 단락, 비용 무시)
- **키 충돌 해소**: KeyBindings._statusKey C→P (은신=PlayerMovement cKey 충돌, 크래프팅 X 이동 전례 동일 조치). Settings/KeyBindings.asset도 P(112) 반영 확인. 키 점유 현황: 은신C/크래프팅X/RevengeListK/통계U/스탯P
- **스텁 정리**: PlayerStatsUI.cs(21줄 TODO)·UIPlayerStats.cs(하드코딩 가짜 레벨업)·PlayerStatusWindow.cs 삭제(+meta, Windows.meta). 삭제 guid 4종 씬/프리팹 잔존 참조 0건 검증
- **검증**: 에디터 실행 중이던 초반 batchmode는 락으로 exit 1(컴파일 오류 아님) → 에디터 자동재컴파일로 1차 판정(ProjectName.UI.dll에 StatusWindowUI 포함+삭제클래스 잔존 0), 에디터 종료 후 정식 배치컴파일 **UNITY_EXIT_CODE=0, error CS=0, Exiting batchmode successfully**
- ⬜ **남음**: Play 판정(①P키 토글 ②채집+3EXP 게이지 ③레벨업 스탯갱신+팝업 ④은신C 무충돌 ⑤한글 폰트 렌더링 — LegacyRuntime.ttf 한글 글리프는 핫바와 동일 프로젝트 공통 이슈)

---

## 📌 세션 종합 스냅샷 (2026-09-08 8차 — 설원(North) 눈밭화 ✅)

- **범위**: 사용자 "북쪽 지형이 여전히 연두빛, 설산 느낌으로" — 바닥 텍스처는 이미 흰색(`_northTint (0.93,0.95,0.98)`+`_northTintStrength 0.9`)인데 **그 위의 잔디 커버(IdyllicGrassCover)가 연두로 뒤덮던 게 원인** 진단
- **핵심 원인**: `GrassTintNorth = (0.88,0.95,1.0)`이 **곱셈** 틴트라 연두 잔디 base(예 0.3,0.7,0.3)에 곱해도 최대 0.26 수준 — 수학적으로 흰색 불가. 텍스처 파일 교체 불필요, 절차 생성 코드만 수정
- **G1 밀도 감소**: `NORTH_DENSITY_FACTOR=0.35f` 추가 — BuildChunk에서 nation==North면 셀당 잔디 35%로 감소(눈밭은 잔디 듬성듬성). 동/서/남/황제국 무영향
- **G2 흰눈색 직접 치환**: ApplyNationTint에서 North만 곱셈을 버리고 `snow(0.95,0.97,1.0)`을 PropertyBlock으로 직접 주입 + return (연두 base를 흰눈으로). 나머지 방위는 기존 곱셈 로직 그대로
- **컴파일 트러블**: CS0136 `renderers` 로컬 변수 스코프 충돌(North 블록 vs 메서드 레벨 동일명) → `snowRenderers`로 이름분리 해결
- **검증**: 배치컴파일 error CS=0 (verify_compile_idyllic.bat → CMDEXIT=0, Exiting batchmode successfully)
- **커밋**: 7f45635a (push 완료) — 1 file changed (IdyllicGrassCover.cs)
- ⬜ **남음**: Play 판정(북쪽 설원 잔디 흰색화 + 밀도 감소 눈밭 확인)

---

## 📌 세션 종합 스냅샷 (2026-09-08 7차 — 성 내부씬 8종 레이아웃 변형 ✅)

- **범위**: 성 내부 2종(타영주/내영지) 각각 가구 배치 8종 변형 → 16개. 사용자 "두 종류 영지 내부씬 배치를 랜덤하게 8종씩 16개"
- **빌더**: CastleInteriorBuilder/PlayerCastleInteriorBuilder에 `BuildXxxInterior(nation, layoutVariant)` 오버로드 추가 (기존 1인자 = variant 0 위임 → 기존 배치 100% 보존). GetLayoutVariantParams 결정론 테이블: 좌우대칭(mx)·기둥 3~6개/x 3.5~4.5·왕좌/지휘/화로 z시프트·추가장식가구(화분/탁자). Random 금지·layoutVariant 정수만으로 결정
- **기능 보존**: variant 0=기존배치. Castle 잠긴문 4개(집무실/무기고/금고실/문서고) + PlayerCastle 작업대/저장고/무기고 상호작용 앵커(WeaponStand_0/StorageShelf_2/workbench)는 **전 variant 존재**(위치만 변경). 조명도 좌우대칭/시프트 추종
- **진입 체인**: BuildingEvents.OnEnterBuildingRequest/BuildingTrigger.RequestEnterBuilding/IndoorSceneTransition.EnterBuilding **4인자(territoryKey) 확장** + `ComputeLayoutVariant(territoryKey djb2%8, 폴백 nation+소유)` → castle 케이스 두 빌더에 layoutVariant 전달. **같은 영지 재방문 = 항상 동일 배치(결정론)**
- **검증**: 배치컴파일 error CS=0 ×3회 (buildlog_interior.txt, CompileScripts 10.6s)
- ⬜ **남음**: Play 판정(서로 다른 영지 2곳 배치 상이 + 재방문 동일 + 기능문 여전히 동작)

---

## 📌 세션 종합 스냅샷 (2026-09-08 6차 — 미니맵 지형 렌더링 ✅)

- **범위**: MinimapUI(현재 UI 프레임만)에 실제 지형을 넣기 — 사용자 "미니맵에 지형이 들어갈 수 있도록"
- **M1 공개 저장**: `TerrainSplatBaker.LastWorldSplat`(public static Texture2D) 추가. `BakeWorldSplat` 끝 + `ApplyWorldSplatToGround`(캐시 로드 경로)에서 갱신
- **M2 MinimapUI 렌더**: `TryApplyMapTexture()` — Start()+Update() 지연 재시도로 `LastWorldSplat`을 `SetMapTexture`로 주입, `SetMapScale(_minimapDiameter/WORLD_SIZE = 220/2000 = 0.11)` 좌표 정합. 월드 원점(0,0)=미니맵 중심, 기존 `WorldToMinimapLocal`(worldPos×scale)가 지형 위에 마커 정확 배치
- **M3 마커 오버레이**: `DrawMarkerOverlay()` — ①영지 마커(검은 점, TerritoryDatabase.GetAllDefinitions × Ring별 거리·퍼짐) ②활성 퀘스트 마커(색 점, QuestMarkerSystem.GetActiveQuestMarkers). `GetTerritoryWorldPosition`(QuestMarkerSystem과 동일 규칙). 컴파일 트러블: TerritoryDefinition이 **value 타입(struct)이라 null 비교 불가 → `==null` 가드 제거** (커밋 d88bd90f)
- **핵심**: BakeWorldSplat이 만든 **게임 지형 그대로(2048², 5국가 합성+릴리프+호수 심수색) 재사용 → 추가 베이크 0, 화면과 100% 일치**
- **검증**: 배치컴파일 error CS=0 ×2회 (buildlog_minimap.txt, CompileScripts 7.3s, Exiting batchmode successfully)
- ⬜ **남음**: Play 판정(방위색·호수·흙길/절벽 + 영지/퀘스트 마커 + 마커 정합)

---

## 📌 세션 종합 스냅샷 (2026-09-08 5차 — 지형 다양화 T-D5: 호수 주변 꾸미기 ✅)

- **범위**: IdyllicDecoPlacer에 호수 주변 데코 3종 추가 (사용자 "호수도 지형/주변 꾸며주는 걸 추가" 지시)
- **L1 수변 관목** `PlaceLakeshoreShrubs`: 밴드 1.02~1.55r, `cat.bushes`, 대형(r≥100)=8~12/소형=4~6, 게이트 y>waterLevel+0.5
- **L2 수변 바위·자갈** `PlaceLakeshoreRocks`: 밴드 1.02~1.70r, rockBig(대형 1/3 혼입)/Med/Small, 5~10개, y>waterLevel+0.4
- **L3 습지 습초지** `PlaceLakeshoreWetlandClusters`: 밴드 0.95~1.12r, 호수당 5클러스터×3~6포인트(반경 0~5m 산점), 갈대(1.3~2.0m)+관목(0.8~1.1m) 2:1 혼합, −0.1<y<waterLevel+1.2
- **통합**: 호수 루프(L244~246)에 별도 rng(SEED+79/83/89+i*7)로 3줄 추가 → **기존 lakeRng 스트림 소비 순서 보존**(결정론 회귀 없음). 기존 6개 호수 데코 함수·상수 무수정
- **검증**: 배치컴파일 error CS=0 (buildlog_lake.txt, CompileScripts 15.3s, Exiting batchmode successfully)
- ⬜ **남음**: Play 판정(수변 관목/바위 자갈/습지 습초지 체감·기존 무손상)

---

## 📌 세션 종합 스냅샷 (2026-09-08 4차 — 지형 다양화 T-D3 후속: T5 능선길+호수연장 ✅)

- **범위**: DirtRoadMask 경로 시스템에 능선길 2개(T5-1) + 호수 수변 연장 2개(T5-2) 추가
- **T5-1 능선길**: TerrainShape.GetRidgeCrestSegments(42) — GetRidgeBoostMask와 **동일 셀/시드/구성방정식**(CELL=640, nationSeed+7303, 길이200~400m 해시지터, 방향 base±45°)으로 중앙 반경1200m 내 최장 2개 crest 중심선 반환 (GetRidgeBoostMask 자체는 무수정)
- **T5-2 호수연장**: 곡선도로 4개 끝점(반경700m) → 가장 가까운 호수 수변(shorePt=반지름 교점)까지 직선 연장 2개. 결정론 정렬(수변거리→도로→호수 인덱스), 상한600m, 타 호수 원관통 기각(CrossesOtherLake)
- **통합**: TerrainPathGenerator.ExtraPaths() 캐시(1회 빌드)가 이들을 폴리라인화 → **DirtRoadMask가 CurvedRoads+ExtraPaths 둘 다 순회**하도록 통합(L4 흙길 렌더)
- **버그 수정**: 에이전트 1차 구현이 ExtraPaths 빌더만 만들고 DirtRoadMask에 통합 안 해서 렌더 미반영 → 부모가 통합 루프 추가로 수정 (커밋 14db2a81)
- **검증**: 배치컴파일 error CS=0 (buildlog_td5.txt, CompileScripts 26.6s) · 결정론·불변(DirtRoadMask 시그니처/기존 도로/GetRidgeBoostMask 무수정)·재귀(스플랫 시점 Lakes 준비)·성능(폴리라인 캐시) 통과
- ⬜ **남음**: Play 판정(능선길 상승감·호수 수변 연결·기존 무손상)

---

## 📌 세션 종합 스냅샷 (2026-09-08 3차 — 지형 다양화 T-D3 후속: T1-1/T1-2 Generator 소비 ✅)

- **범위**: T-D3의 위치 마스크 2종(테라스/능선부스트)을 TerrainGenerator.ComputeSubBiomeVariation에서 실제 소비(carve/델타)로 전환
- **T1-2 능선 부스트**: `GetRidgeBoostMask × RidgeBoostAmp(방위별 3~5m)` — 640m 셀 세그먼트·폭60m 능선에 국소 +30% 체감 진폭(전체 진폭/다른 델타 라인 무수정)
- **T1-1 층절벽 테라스**: `GetTerraceBlockMask`(임계 0.25, 블록 반경 40~80m) 내부를 `baseH` 3m 단 양자화 → 계단 3~4단 carve. additive 델타(양자화 레벨−현재높이)×Smoothstep 블렌드로 가장자리 자연 블렌딩
- **검증**: 배치컴파일 error CS=0 (buildlog_td3_t11_t12.txt, CompileScripts 23.4s) · 불변 제약 유지(델타 가산만, cliffSuppression=return에서 자동, 결정론, 재귀 없음)
- ⬜ **남음**: T5 능선길 2개+호수 연장(별도 세션 예정), Play 판정(층절벽 실루엣·능선 상승감)

---

## 📌 세션 종합 스냅샷 (2026-09-08 2차 — 지형 다양화 T-D2: 예시2~13 Gap 충전)

- **진단**: 예시 12장 vision 분석 → 단조로움 3원인 확정 ①노출 암반 부재 ②텍스처 tint 위주(모래/암반 전환 없음) ③랜드마크 희소
- **T1 형태**: 노출 암반 시스템(320m 셀, 5~7층단, 방위별 밀도) + 방위당 대형 분지(90~130m+외벽 병풍절벽, 5/5 배치) + 서쪽 천연 아치((-740,0), 받침 암돔 지형 강제) + 대형 호수 #3(423,906 r130)+중형 3개 승격 = 총 18개
- **T2 텍스처**: 결합 텍스처 4레이어 전환 — 암반(방위별 회청/사암/석회암)/호수 모래사장(0.98~1.42r)/꽃융단 핑크 블롯/분지 명도
- **T3 데코**: 암반 위성 바위 군집 + 서쪽 아치(rockBig ×2.4) + 꽃융단 밀집 클러스터(핑크+퍼플)
- **치명 트러블 2건 수리**: ①재귀 — GenerateLakes waterLevel→마스크→Lakes getter 무한재귀 → `LakesOrNull` 재귀 가드+HandLakeTable ②Empire 영토=중심 50m뿐 → 중앙링 기능(분지/꽃융단) 공유 세트 방식으로 전방위 평가
- **QA**: 배치컴파일 error CS=0(09:50) + QA에이전트 치명 1건(호수 생성 중 캐시 고정) 패치 → A~F 통과
- **Play 판정 대기**: 노출암반 실루엣/모래사장/아치/중앙 꽃융단/분지 병풍절벽 5포인트

## 📌 세션 종합 스냅샷 (2026-09-08 — 끊김 원인 제거 + 지형 체감 + 인벤폰트)

- **끊김 판정 전환**: 애니 리깅/루프는 정상([State] 전환 체인+loopT normT 2.93루프+사지Δ 증거). 끊김 실체=프레임 히치 3원인 → 전부 제거: ①IdyllicGrassCover NRE 963건(MPB 필드초기화자 UnityException+go null) ②CollisionDebugger 로그 스팸(dedup 1s) ③GroundWatch 오탐 복구로 인한 구/신 지형 이중 렌더(renderer 항목 감시 제외)
- **지형 체감 강화**: B1 청크가 텍스처 준비 플래그 대기(GroundTexturesReady, 10초 타임아웃) — 방위색/흙길 청크 누락 방지 / B2 꽃밭 밀도×1.56·패치 160개 / B3 숲 3단 밀도(심부 3×3)+가장자리 페이드 / B4 흙길 스커트 1.3×+노이즈+가장자리 7~9m 자갈·허브·들꽃 데코(국가당 150)
- **인벤폰트 확대**: 폰트 +25~30%(타이틀64/탭22/슬롯36/아이템명60/정보이름76/설명54/버튼36) + 윈도우 1180×1040(1080p 안전) + 패널·Rect 연동 + 수량 우상단 코너
- **QA**: 8파일 균형 0 + QA에이전트 PASS 8/8(치명 0). 커밋 d4bb3fa1→3b89c69d→84ed55bf→20a41901 push
- **Play 판정 대기**: NRE 0건·프레임 안정 / 흙길 데코·꽃밭·숲 체감 / 인벤폰트 잘림 0

## 📌 세션 종합 스냅샷 (2026-09-07)

- **애니**: 로코모션=믹사모(Walking/Running/Idle/Standing Jump, **loopTime 필수**) + 전투=팩 OneHand + **검 부착**(steel→RightHand) + Run 재생속도 0.28×Speed + Walk↔Run 임계 2.0 + 이동 가속 12/감속 18 스무딩 — 8차(메타 매핑)→9차(FBX 위계)→10차(모션 참조) 수리 체인 완료
- **인벤토리**: 토탈코딩 스타일 개편(다크 패널+5열 그리드+무기 섹션) + **I키 토글 배선 복구**(씬에 UI 0개 → 런타임 부트) + **WeaponEquipManager**(4종 검 장착/해제)
- **지형**: 청크 64장 ±1600m(Ring1 1450 커버) + 방위색 결합 텍스처 **1024 1:1**(북=설원 흰색) + **흙길 53세그먼트**(폭 7m, 위 데코 제외) + 메사 35% + 꽃밭 140m + 대형 호수 2개(총 17) + 숲 군락 200m + 정적 병합 잔디 10k/청크 + 경계 클램프 1590
- **입력**: Keyboard.current 캐시 제거(W 스테일 드랍 수리) + InputProbe 120s + DD5
- **미해결/다음**: ONNX int8 임포트 실패 노이즈(신경망 미사용, 무영향), ProceduralAnimationController CS0618 마이그레이션, 청크 생성 71초 최적화, 흙길 높이 평탄화(2단계)


## 🗺️ 젤다 품질 업그레이드 (ZELDA_UPGRADE_PLAN) — Batch 1 ✅ (2026-09-07)

> 목표: ①젤다풍 지형 다양화(방위별) ②끊김 없는 애니 ③고품질 인벤토리(TotK). 방위별 컨셉(동초원/서사막/남화산/북설원/중앙황금)을 최상위 유지 + 탑다운뷰(하늘/God Rays 제외). 색=방위tint(변경X), 형태/데코/릴리프=방위 바이옴(신규).

### TRACK1-P1A: 방위 내 서브 바이옴 변동 (TerrainGenerator.cs) ✅
**핵심 발견:** GetHeightAt의 biome 인자는 레거시(무시), 높이는 이미 NationTerrainController.GetNationFromPosition 기반 방위 파라미터로 결정 → 방위별 형태는 이미 존재. 남은 것은 방위 내 소규모 변동.
**구현:** ComputeNationHeight가 TerrainShape.NationHeight 결과에 ComputeSubBiomeVariation 델타 가산.
- 동(East)=롤링힐(170m)+숲 완구릉 돔(220m, 노이즈 게이트) / 서(West)=험준 잔능선+골짜기 절삭 / 남(South)=평탄+사구 물결+소메사(90m 셀 22%, 3m) / 북(North)=고지 게이트 첨봉 / 황제국=미세 기복
- 결정론(FbmNoise 고정 시드+nation 오프셋), cliffSuppression 곱 → 스폰/성/호수/방위경계 보호(45° slopeLimit 유지), 최대 경사 ~21° 이내.
- GetHeightAt 시그니처/반환 규약(y=1f+) 불변 → 다른 시스템 호환.

### TRACK2-P2A~C: 애니 자연화 (PlayerMovement.cs + HumanoidClipDriver.cs) ✅
- **T2B-1 방향 전환 스무딩**: 조준 회전(기존 Slerp, 529행)이 실행 안 된 프레임+이동 중+비구르기일 때만 _moveDirection 방향으로 Slerp(MoveTurnSpeed=9 rad/s). 조준 스트레이프 100% 보존, 중복회전/진동 없음.
- **T2B-2 경사 정렬(가벼움)**: ApplySlopeAlignment() — 접지+이동 시 GetHeightAt 인접 표본(±0.5m, 결정론) 법선 추정 → 피치/롤 ±6° 클램프 Slerp(계수 6/s). 데코/건물 위·준평지·공중·구르기 미개입(직립 복귀). 요(yaw) 무접촉.
- **T2B-3 착지 흡수**: 공중→접지 전환 감지(_wasAirborne/_airPeakFallSpeed) → 착지 0.2s 가속 램프 40% 완화 + 낙하>8m/s 강착지만 카메라 흔들림. 위치 스냅/텔레포트 없음(물리접촉 접지 보존).
- HumanoidClipDriver: 실제 클립 블렌드는 RigAnimationController+컨트롤러 담당 → 코드 블렌드 상수 박지 않음. Jump 하강 에지 0.22s Speed 상한(+1.5)으로 복귀 팝 완화. [State]진동/DD 로그 무영향.

### TRACK3-P3A~C: 인벤토리 고품질화 (InventoryWindow.cs) ✅
- **폰트/텍스트 수리(선행)**: 타이틀 72→52, 탭 24→18, TextClipping.Clip — 인벤토리.PNG 한글 반잘림/겹침 수리.
- **T3B-1 빈 슬롯 그리드 가이드 시각화**: MakeRoundedBorderedTexture 라운드 SDF 가이드 셀(48px, 다크+그리드라인 보더), GRID_MIN_ROWS=2만큼 항상 표시(아이템 없어도 슬롯 틀 보임).
- **T3C-2 포커스 하이라이트**: 선택 슬롯 = ColorMintGlow 배경(민트 글로우)+금색 테두리(ColorAccent). 우하단 x{count} + 무기는 공격수치 ⚔.
- **T3B-2 우측 프리뷰 패널 틀**: PREVIEW_PANEL_WIDTH=340 + 좌측 금색 구분선 + "🧝 캐릭터" 헤더 + 3D 프리뷰 placeholder + 장착 무기 표시(WeaponEquipManager.CurrentId→강철/크리스탈/돌/나무검). 그리드 660px로 축소 — 무기/정보 패널 하단에 겹침 없음.
- WeaponEquipManager 로직 무변경, IMGUI 유지.

### 검증 ✅
- 4개 파일 중괄호 균형 0 (TerrainGenerator 136/136, PlayerMovement 211/211, HumanoidClipDriver 125/125, InventoryWindow 182/182)
- **배치컴파일 성공**: 14:58 ProjectName.Systems.dll + ProjectName.UI.dll + Assembly-CSharp(Editor 포함) 4종 DLL 갱신 = error CS=0
- 자동커밋 데몬 커밋: 67eff47b(TerrainGenerator), 4cf318b6(HC/Player/Inventory), d83e4703(PlayerMovement 중간).

### Play 판정 대기
① 4방위 주행 시 지형 형태 차이(동 초원/서 협곡/남 사막·소메사/북 첨봉/황제국 평탄 골든) ② 방향전환 시 부드러운 턴 ③ 경사 오를 때 몸 기울임 ④ 점프→착지 자연스러움 ⑤ **I키 인벤토리: 빈 슬롯 그리드/민트 포커스/우측 프리뷰 패널+장착 무기** ⑥ [State] 진동 0 + [InputProbe] 120s.

### ⏭️ 다음 (Batch 2)
TRACK1-P1B(지형 드라마 릴리프/메사 stratification·협곡) + TRACK2-P2D(병사 미러) + TRACK3-P3D(프리뷰 RenderTexture 3D) — Play 판정 선행 후 진행 권장.

---


## 🗺️ 젤다 품질 업그레이드 — Batch 2 (2026-09-07)

> Batch1의 지형/애니/인벤토리 기반에 이어, Batch2는 지형 릴리프 드라마를 최우선 완료 + 나머지 분리.

### Batch2 TRACK1-P1B: 지형 드라마 릴리프 (TerrainShape.cs) ✅
**파일:** Assets/Scripts/Systems/TerrainShape.cs (437→462행, patch 최소)
**A. 메사 stratification(층상 단차):** `StratifyMesa(float m)` 신규 — `Floor(m*14)/14` 양자화 + 층 내 잔차 `frac*0.15` 램프(하드 컷 완화). MesaLift return 직전 `best = StratifyMesa(best)`. **중심 m=1 → Floor(14)/14=1 유지(내부 완전 평탄 규약 불변)**, 가장자리 m→0은 층 시작 램프(급경사 절벽 유지).
**B. 협곡/*을 강화(서·남 방위):** NationHeight의 Valley/Mesa 사이에 방위 부스트 — West `+4m`, South `+2.5m`, `× m × cliffSuppression`(m=216행 RidgeCliffMask 결과 재사용). 저주파 Fbm(0.5×freq0, 2옥타브, 시드 nseed+777)으로 협곡 깊이 변동. 보호 구역(cliffSuppression=0)에선 m=0 → 자동 무해.
**규약 보존:** GetHeightAt 시그니처/반환 불변, 결정론(고정 시드), 순수 float, slopeLimit 45° 내, 기존 terrace/valley/서브바이옴과 회귀 없음. **브레이스 o=38 c=38 균형 0.**

**Batch1 검증(앞서 완료):** 4방위 서브바이옴 + 애니 자연화(방향vSlerp/경사정렬/착지흡수) + 인벤토리(빈슬롯그리드/민트포커스/프리뷰틀). Bridge 0.

### TRACK2-P2D 병사 미러 / TRACK3-P3D RenderTexture: Batch3 분리
- **병사 미러**: 이미 `HumanoidClipDriver.UpdateSoldier`(Speed 공급) + 팩 OneHand 컨트롤러로 **플레이어와 동일한 단일드라이버 구조** 확인 → 로코모션 일관성 충족. 병사 3종(Walk↔Run 임계 2.0/재생속도 0.28) 동일 적용 여부는 빌더 확인 후 Batch3에서 확정.
- **프리뷰 RenderTexture(3D 캐릭터)**: 리소스/성능 리스크 높아 Batch3로 분리 — "🧝 캐릭터" placeholder + 장착 무기 표시는 Batch1(틀)에서 이미 표시됨.

### Batch2 검증 ✅
- TerrainShape 브레이스 균형 0.
- **배치컴파일 성공**: 16:39 ProjectName.Systems.dll 갱신(여지형 코드 포함), error CS=0.
- 자동커밋 데몬: ffbdd447(TerrainShape).

### Play 판정 대기 (다음 단계)
① 4방위 주행 시 지형 형태 차이 체감(동 초월/서 협곡·절벽/남 사막·소메사/북 첨부/황제국 평탄) ② **메사(대지) 단차가 계단식 층+절벽**으로 보이는지(예시2 스타일) ③ 서·남 협곡 깊이/절벽 벽 감마 ④ 방향전환 시 부드러운 턴 + 경사 기울임 + 점프→착지 자연 ⑤ I키 인벤토리: 빈 슬롯 그리드/민트 포커스/우측 프리뷰 패널+장착 무기.

### ⏭️ 다음 (Batch 3 종합)
TRACK1 유지 + **TRACK3-P3D 프리뷰 RenderTexture(3D 캐릭터)** + TRACK2-P3D 병사 3종 임계 확정 + TRACK1-P1C 조명(탑뷰) + P1D(Idyllic 잔디/데코 방위색). Play 판정 선행 후 진행 권장.

---


## 🏰 영지 내부 2종 분리 (INTERIOR_SCENES_PLAN) ✅ (2026-09-07)

> 요구: 영지(성) 진입 시 소유 상태에 따라 **두 개의 별도 내부씬**. 점령 후 자신 소유=기능적 기지, 점령 전 타 영주 소유=웅장한 왕좌실.
> 참고 문서: docs/INTERIOR_SCENES_PLAN.md

### 신규 PlayerCastleInteriorBuilder.cs (자신 소유 — 기능적 내부) ✅
- `BuildPlayerCastleInterior(string nationStyle)` (426줄): 방 22×6×16(기존보다 확장).
- 지휘 책상(CreateTable)+의자+관리용 책상, 중앙 작전 테이블+의자2, **플레이어 환영 배너**(깃대2+청색 깃천+금장 트림).
- 저장고(왼쪽 벽 CreateShelf×2+상자5+통), 무기고(벽 오른쪽 무기스탠드×3+벽걸이 검4), 작업대(앞벽 CreateCounter+모루).
- 조명: ambient 0.30/0.28/0.25 @1.15(타영주용 0.08보다 밝음, flicker=false) + 천장/책상/작업대/저장고/무기고 5개 점등 — 실용적·환영 분위기.
- 잠금문 없음(이미 내 소유) → 대신 NameplateDisplay 6개(🪑지휘책상/📜관리사무소/🚩플레이어영지/🎒저장고/⚔️무기고/🛠️작업대).
- 국가별 텍스처(동/서/남/북/황제국) 재사용, URP Lit→Standard fallback, CreateRoom null 가드.
- 기존 CastleInteriorBuilder.cs **미변경**(타 영주용 유지).

### 소유 분기 연결 (5파일) ✅
- **BuildingEvents.cs**: OnEnterBuildingRequest `Action<string,string>`→`Action<string,string,bool>`, RequestEnterBuilding에 isPlayerOwned 인자.
- **BuildingTrigger.cs**: `TerritoryKey` 필드+프로퍼티 추가. castle E입력 시 `TerritoryDatabase.Instance.GetState(territoryKey)` → `ownership==PlayerOwned` 판정(ProjectName.Core.Data using 추가).
- **IndoorSceneTransition.cs**: `_pendingIsPlayerOwned` 필드, EnterBuilding 3인자, case "castle"에서 `_pendingIsPlayerOwned ? PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nation) : CastleInteriorBuilder.BuildCastleInterior(nation)`. (실제 위치 UI/ — ProjectName.UI 확인)
- **IndoorTransitionSetup.cs**: CreateBuildingTrigger에 `territoryKey` 파라미터.
- **TerritoryBuilder.cs**: 성문 트리거 생성 시 `parent.name`의 "Territory_" 제거한 키("East_01") 전달 — TerritoryDatabase dict 키와 정확히 일치(폴백이 아닌 hit 보장).

### 검증 ✅
- 6파일 브레이스 균형 0 (BuildingEvents 5/5, BuildingTrigger 24/24, IndoorTransitionSetup 14/14, TerritoryBuilder 101/101, IndoorSceneTransition 19/19, PlayerCastleInteriorBuilder 39/39).
- **배치컴파일 성공 (18:26 Systems.dll+UI.dll 갱신, error CS=0)**.
- 자동커밋: 5a6deabf(빌더), e39bca52(분기), 70dcfa26(계획).
- .meta 미커밋(Untracked) ~ 커밋 필요.

### Play 판정 대기
① 타 영주 영지(점령 전, LordOwned) 성문 E → **웅장한 왕좌실**(기존 CastleInteriorBuilder) ② 자신 소유 영지(점령 후, PlayerOwned) 성문 E → **기능적 내부**(지휘책상/저장고/무기고/작업대+환영배너) ③ 국가별 텍스처 유지 ④ 상점/크래프트 트리거 여전히 동작.

---


## 🔥 PlayerCastleInteriorBuilder — 중세 판타지 강화 (2026-09-07)

> 사장 지시: "내부는 중세 판타지 느낌으로" (예시 사진=전체 구조 참고일 뿐). 기존 기능적 기지 → 중세 성 대전당 톤으로 강화.

### 추가/변경 (PlayerCastleInteriorBuilder.cs, +110/-13)
- **석재 기둥 2열**: StonePillar_L/R x=±4, z=-4..4 4개씩 (회색 석재 pillarMat) — 대전당 feel.
- **화로 2기 + 토치**: 입구 양쪽 BrazierBowl/Coals(짙은 회색) + 위 주황빛 point light + 앞 기둥 토치 빛. "🔥 화로" Nameplate.
- **중세 장식**: 청색 배너 유지 + **문장 방패**(배너 양옆 2 + 화로 위 벽 2, 붉은 자수 문장 trimMat) + **붉은 러그 2장**(중앙 통로 2.8×6.0 + 왕좌 앞 4.0×2.4, 바닥 위 0.005 z-fighting 방지).
- **조명 중세화**: ambient (0.22,0.18,0.15) 따뜻한 어둠 + 화로 촛불빛 주황 point lights + **flicker=true**(화로 깜빡임, 과하지 않게). 기존 백색 메인/작업대/저장소/무기고 조명도 주황빛 계열로.
- 주석/로그: "기능적 기지" → "중세 판타지 성(플레이어 소유)".
- 국가별 텍스처/지구(책상·저장고·무기고·작업대·배너) 유지. 타영주용 CastleInteriorBuilder 미변경.

### 검증 ✅
- 중괄호 54/54 균형 0. **배치컴파일 성공(18:43 Systems.dll, error CS=0)**. 자동커밋 72370c0d.

### Play 판정 대기
① 점령 후 자신 영지 성문 E → **중세 판타지 성 내부(석재 기둥 2열 + 화로 번지는 주황 불빛 + 문장 방패 + 붉은 러그 + 청색 환생 배너)** ② 국가별 텍스처리(동/서/남/북/황제국) ③ 화로 깜빡임 분위기 눈확인.

---


## 🗺️ 젤다 품질 업그레이드 — Batch 3 (2026-09-07)

> Batch1(지형/애니/인벤). Batch2(지형 릴리프) 완료. Batch3에서 조명 + Idyllic 방위색 + 3D 프리뷰 완성.

### TRACK1-P1C: 탑뷰 조명 BotW 파스텔 (DayNightCycle.cs) ✅
낮(Day) 4개 파라미터만 보정, 낮/밤 전환 로직 무변경:
- _noonColor (1,0.95,0.8)→(1,0.97,0.87) / _noonIntensity 1.0→**1.3** / _noonShadowStrength 1.0→0.85(파스텔 톤) / _dayAmbient (0.6,0.6,0.6)→(0.68,0.74,0.82 밝은 연청)
- **Contact Shadows: URP 미지원 확인**(HDRP 전용) → skip하고 보고. URP 대안=Soft Shadows+Shadow Distance 튜닝(범위 밖).
- 중괄호 42/42 균형 0. 해紅/달 로직 무접촉.

### TRACK1-P1D: Idyllic 잔디 방위별 틴트 (IdyllicGrassCover.cs) ✅
- `IdyllicDecoPlacer`는 이미 방위 데코 프로파일 지원(건드리지 않음). `IdyllicGrassCover`에 **방위별 잔디 틴트 팔레트** 추가(±5~10% 미세):
  - 동=기본(1,1,1) / 서=황토빛(1,0.94,0.86) / 남=따뜻(1,0.90,0.85) / 북=서리빛(0.88,0.95,1) / 황제국=금빛(1,0.97,0.90)
- **MaterialPropertyBlock** 사용 → 공유 머티리얼 무복제(원본 훼손 없음), 청크별 GetNationFromPosition 재사용(플레이어 이동 시 결정론 전환). _BaseColor+_Color 동시 설정(셰이더 호환).
- Configure/부트 로직 무변경. 중괄호 36/36 균형 0.

### TRACK3-P3D: 인벤토리 3D 캐릭터 프리뷰 (InventoryWindow.cs) ✅
- `DrawPreviewPanel` previewRect에 **RenderTexture 512×640 + 전용 카메라**로 플레이어 3D 프리뷰. 월드 플레이어와 별개 fresh 인스턴스(Resources.Load Player_Rigged_Heat).
- 전용 카메라(커스텀 배경, far20/30° FOV) → 개체만 촬영, `_previewCamera.Render()`로 스냅샷 표시(정지 프리뷰).
- **장착 무기 attach**(Resources.Load `<id>_sword`), `HumanoidClipDriver.CopyMaterialsFromGlb`로 원본 머티리얼 이식.
- **ReleasePreview()**: 인벤토리 닫을 때/OnDestroy에서 RT+카메라+개체 해제(메모리 누수 방지). 실패 시 기존 "캐릭터 프리뷰" 플레이스홀더 폴백(크래시 없음).
- 중괄호 205/205 균형 0.

### Batch3 검증 ✅
- 3파일 브레이스 균형 0 / **배치컴파일 성공(17:24 Systems.dll+UI.dll 갱신, error CS=0)** / 자동커밋 2ef6c73c.

### Play 판정 대기 (통합)
① **탑뷰 밝고 파스텔한 조명**(낮 채도) ② 4방위 관영 잔디 틴트 미세 전환 ③ **I키 인벤토리 우측 3D 캐릭터 프리뷰(RenderTexture)+장착 무기** ④ 이전 배치: 메사 층상단차/서·남 협곡/방위서브바이옴/애니자연화 확인.

### ⏭️ 다음 (Batch 4+)
TRACK1-P1C 조명에서 URP Soft Shadows 세부 튜닝(옵션) + TRACK2 병사 3종 임계 확정 + TRACK1 성능(데코 컬링/프레임 수치) + 통합 Play 최종 판정.

---

---

## 2026-09-06: 애니 10차 — 컨트롤러 모션 참조 전량 깨짐 교체 ✅ (판정 대기)

**DD4 결정 증거:** `state=Walk/Run`인데 `clip=NONE` — 상태 전환·normT 진행은 되는데 **재생 클립이 아예 없음**(LFArmRotΔ도 회전 오염분 제거 시 0.00°). 컨트롤러 파일 검증: 4개 컨트롤러(Player+병사3)의 **모든 m_Motion이 `fileID: -203655887218126122, type: 3`** — .anim 네이티브 참조는 `fileID: 7400000, type: 2`여야 함. guid는 전부 올바른데 참조 형식만 깨짐 → 추정: 09-03 팩 교체 때 YAML guid 문자열만 교체하고 구 참조 형식 잔존. **09-03 이후 모든 애니 테스트가 빈 상태로 재생되고 있었음** (8차 메타·9차 위계는 실존 버그였으나 이것이 최후 고리).

**수리:** Tools > Anim > Build Mixamo Controllers 재실행 → **Player_AC_AC 중복 생성 재발**(구 컨트롤러 삭제 실패, 09-04a 수정에도) → 파일 레벨 교체: 구 Player_AC 삭제, 검증된 Player_AC_AC(7400000/type2 ×7 + FBX 서브에셋 type3 ×2)를 Player_AC로 승격. 병사 3개는 정상 재빌드 확인. (커밋 3cbf372b)

**판정 대기 (Play):** DD4 `clip=OneHand_Up_Walk_B(human=True)` + `LULegRotΔ>10°`.

---

## 2026-09-06: 지형 구멍 수리 — 와인딩 삼각형 단위 전수 반전 ✅ (판정 대기)

**증상:** 일정 지형에서 점프/구르기 실패 — JumpProbe `grounded=False` + DiagP1 "전방지면 아래에 콜라이더 없음" 16건.

**원인:** Phase B 재표본 후 와인딩 반전을 **첫 삼각형 1개 법선만** 검사 → 원본 메시 와인딩 혼재로 아래향 삼각형 일부 잔존 → 백페이스(레이캐스트 미히트+렌더링 컬링) → 지형 구멍=허공 → 그 구역 grounded=False.

**수리:** 삼각형 단위 전수 검사로 교체(각 삼각형 Dot(n,up)<0만 개별 반전, 이중반전 방지) + 전방 5지점 probe 요약 로그 추가(구멍 분포 가시화). 정점 재표본·콜라이더 재베이크·SyncTransforms 유지. (code agent, 중괄호 144/144 검증)

**판정 대기 (Play):** `[DiagP1] 재표본+와인딩: flip=N/M` 로그 + 전방5지점 전부 hit + 구멍 구역에서 점프/구르기 성공.

---

## 2026-09-06: 지형 T1 — 청크 기반 런타임 지형 생성 (커버리지 확장) ✅ (판정 대기)

**측정으로 확정된 구조 문제:** 지형이 `Terrain_초원_100x100` 단 1장(2000×2000m, ±1000m, 20m/쿼드)인데 영지 링 반경은 Ring1=**1450m** → **Ring1 20개 영지 전체+Ring2 경계가 지형 밖(회색 허공)** — "지형적 문제로 애니가 안 나오는 지역"의 정체(높이 수식은 전역 계산이라 캐릭터는 허공 위 부유). 부가: 20m/쿼드 조밀도 부족으로 메시-수식 편차가 접지 판정을 지역별로 깨뜨림.

**수리 (신규 RuntimeTerrainChunkManager.cs + GameSetup 훅):** ±1600m를 400m 청크 8×8=64장으로 런타임 생성(100×100 정점/청크 = **4m/쿼드, 5배 해상도**), y=GetHeightAt+1 규약 준수, (a,c,b)/(b,c,d) +Y 위향+안전반전, UV는 Ground_Inner affine 역산으로 월드 연속(폴백 worldXZ/2000+0.5는 실제 규약과 일치 — QA 확인), 머티리얼 1회 복제 공유, 플레이어 청크 우선 코루틴(청크당 1프레임), 플레이어 청크 완료 즉시 Ground_Inner의 **Renderer+Collider만** 비활성(z-fighting/이중 물리 방지, TerrainTextureApplier 보존). 병행: DiagP1 5지점 probe를 Phase B 후로 이동(VOID×5 오판 방지), _Custom_Color 타입 불일치 SetColor 경고 스팸 수리(폴백+1회 경고).

**QA PASS:** 위향 수학/청크 경계 봉합(−1200 일치)/GetHeightAt 시그니처/UV 폴백=실규약 일치/null 가드/균형 검증. 훅이 try-catch 격리 밖이던 것을 QA가 직접 격리 수정. (커밋 7b614ec7, c86d4f9a)

**판정 대기 (Play):** ① `[TerrainChunks] 완료: 64청크` 로그 ② `Ground_Inner 렌더러/콜라이더 비활성` 로그 ③ 스폰→Ring1 방향 1450m 직진 중 회색 허공 소멸 + grounded=True 연속 + 점프/구르기 성공 ④ Ring1 영지 스크린샷 ⑤ 애니 지표 유지(clip= 팩 클립, LULegRotΔ>10°). **T1 Play 검증 완료(26일 21시대): 64청크 생성 확인(37.0초, 정점 640k).**

---

## 2026-09-06: 지형 T2/T3 — 데코 전체 확장 + 머티리얼 전파 + 월드 경계 ✅ (판정 대기)

**T2:** ① IdyllicDecoPlacer `BOUND_MAX 950→1550` — 데코(트리/바위/덤불/꽃/초지)가 청크 지형 전체(±1550m)에 배치, 캡(트리 1900/국가)이 자연 스로틀 → 총 오브젝트 ~9.9k→~14k 예상(컬링 150/200m 유지) ② RuntimeTerrainChunkManager 머티리얼을 `Object.Instantiate` 복제 → **원본 sharedMaterial 직접 공유**로 변경 — NationTerrainController의 국가별 mainTexture 교체가 64청크 전체에 전파. 원본 파괴/스왑 없음 확인(NationTerrainController 171행은 null 분기 한정).
**T3:** PlayerMovement `WorldBound=1590` + `ClampToWorldBounds()` 헬퍼 — MovePlayer/HandleRoll의 Move 직후 호출(위치 XZ만 클램프, 상태 무변경; 이후 ClampToGroundByHeight가 최종 XZ 기준 Y 정합 — 순서 정합).

**QA:** 자체 라인별 검증 완료(위 3파일), QA 에이전트 타임아웃으로 컴팩트 자체 검증으로 대체 — 머티리얼 파괴/스왑 부재·클램프 순서·균형 확인.

**판정 대기 (Play):** ① 국경(Ring2↔Ring1 방위) 넘어갈 때 청크 텍스처 변화 눈확인 ② 지형 밖(±1600m) 이동 차단(걷기로 못 나감) ③ 배치 수 로그(트리 총합 ~7k 수준) ④ 프레임 저하 없음.

---

## 2026-09-06: 애니 끊김 2차 + 지형 B1 드라마 강화 ✅ (판정 대기)

**A. 애니 끊김 2차(의도 기반 속도):** Walk↔Run 진동 31/29회 — 측정 속도가 경사에서 4.0~4.5로 떨어지며 임계(4.5/4.0)를 오감. HumanoidClipDriver를 **의도 기반**으로 교체: 이동 중 목표=IsDashing?5.0:2.5(기존 공개 프로퍼티 재사용 — 중복 정의 회피), 정지 감지는 raw+0.25초 홀드 유지, 스무딩은 목표로 수렴. 컨트롤러 YAML 무수정(2.5→Walk/5.0→Run 호환). raw는 DD 로그에 유지.

**C. 잔디 미부착 원인 확정+복구:** **IdyllicGrassCover.Configure() 호출부가 코드에 부재**(시스템만 존재, 프리팹 Resources/IdyllicPrefabs/Grass 정상) → GameSetup.Start에 try-catch 격리로 AddComponent+Configure(player) 복원. Configure만으로 자체 기동 확인(프리팹 자동 로드+부모 생성+Update 가드).

**B1. 지형 드라마 강화(예시2~13 컨셉):** 조사 결과 고급 인프라(ridged 절벽/도메인워핑/테라스/계곡/방위 크로스페이드)는 존재하나 진폭이 "구릉" 수준. ① NationParams 방위별 격차 확대: East amp13/절벽8(구릉 초원), West amp14/절벽14(협곡), South amp7/절벽4(평탄 적토), North amp16/절벽16(험준 설산), Empire amp2.5(평탄) ② **메사(대지) 레이어 신규**: 140m 셀 15% 확률, 6m 융기+급경사 가장자리(예시2 단차), cliffSuppression 곱셈으로 스폰/성/호수/경계 보호, ApplyLakeBasins가 나중이라 호수와 무충돌. 구릉 최대경사 amp×freq×2π≈31°(CC slopeLimit 45° 이하). GetHeightAt만 확장 → 64청크 자동 반영.

**판정 대기 (Play):** ① 걷기/달리기 끊김 소멸 + [State] Walk↔Run 진동 잔존 여부 ② 방위별 이동 시 지형 성격 차이(북 험준/남 평탄/서 협곡) 스크린샷 ③ 메사(평탄 대지+절벽 가장자리) 발견 여부 ④ 잔디 밀집 커버(스폰 주변) 스크린샷 ⑤ 성/영지 위치가 새 지형 위에 정상 부착되는지.

---

## 2026-09-06: 입력 신뢰성 + 방위색 고정 + 지형 밀도 상향 ✅ (판정 대기)

**A. 입력(W키 드랍) 수리:** PlayerMovement가 `Keyboard.current`를 1회 캐시 — 디바이스 재열거 시 stale 참조로 isPressed false 고정(간헐 W 드랍 → 이동 정지 → Idle 전환 = "애니 끊김"으로 보임). 수리: 캐시 제거 → 매 사용 `CurrentKeyboard` 즉시 조회 + null 시 InputSystem.devices 재열거 1회. 사용부 10곳 교체. **DD5 입력 프로브** 추가(Play 30초, WASD 에지 로그 + 요약) — W 홀드 중 드랍을 숫자로 확정.

**B. 방위색 고정 표시(북=흰색):** NationTerrainController의 `UpdateForCurrentNation`(플레이어 현재 국가 기준 결합 맵 통째로 재생성)이 방위 고정색을 파괴 → **Start에서 호출 제거**(함수 보존). 결합 텍스처를 **±1600m 1:1 매핑**으로 변경(구 _textureTiling=200 반복 타일 폐기), 북쪽 설원 백색 강화(_northTint 0.93/0.95/0.98 + 북방위 색 고정 강화). 청크 UV도 Ground_Inner 구 uv(±1000 기준) 역산 경로 폐기 → **worldXZ/3200+0.5 고정 매핑**(외곽 래핑 오색 방지). Awake ApplyNationTerrainTexture는 유지(부팅 시 결합 텍스처 적용).

**C. 밀도/다양성 상향:** 메사 셀 15%→25%(예시2 단차 증빈) / 꽃밭 커버리지 14%→22%(FLOWER_LO 0.78→0.70) / **숲 군락화**(forestMask Fbm>0.55 밀집·0.40~0.55 정상·<0.40 개활지 70% 스킵 — 숲 덩어리/개활지 분리) / 호수 배치 bound ±1000→±1500(LAKE_COUNT는 AA2에서 이미 14).

**QA:** 파일별 균형 검증(6파일 braces 0), UpdateForCurrentNation 외부 호출부 0건 확인, ApplyNationTerrainTexture(Awake) 유지 확인, UV 고정 매핑 정합 검증. QA 에이전트 1회 타임아웃 → 컴팩트 자체 검증 대체(머티리얼 파괴/스왑·호출부·스코프 직접 확인).

**판정 대기 (Play):** ① 걷기/달리기 끊김 소멸([InputProbe] 요약 + [State] 진동) ② 4방위 이동 시 방위색 고정(특히 북=흰색) ③ 숲 군락/꽃밭/메사/호수 스크린샷 ④ 성/영지/데코 신지형 부착 확인.

---

## 2026-09-06: A-2 애니 최종 + 흙길 네트워크 + 밀도 최종 상향 ✅ (판정 대기)

**A-2 애니 최종 수리:** 직전 의도 기반(걷기 2.5)이 오판이었음 — 실측 확정: **일반 이동=5.0m/s / 스프린트=15.0m/s**(2.5는 존재하지 않음) → 5m/s 이동 중 Walk 클립 1배속 = 발 미끄러짐("어색한 애니"). 수리: ① 속도 추적으로 복귀(raw 추적+0.25s 홀드 유지) ② **Run→Walk 임계 4.0→2.0**(일반 이동 경사 딥 3.5~4.8이 임계와 겹쳐 진동 — 실제 정지만 Walk 복귀) ③ **Run 클립 재생속도 스케일링**: `run.speedParameter="Speed"` + `speedParameterActive=true` + `speed=0.2f` → 재생속도=Speed×0.2(5m/s서 1배속, 15m/s서 3배속 — 발 매칭). **적용 조건: Tools > Anim > Build Mixamo Controllers 재실행 필수(빌더 변경이므로)**. InputProbe 30→120초.

**B3-1 흙길 네트워크 (신규):** NationTerrainController.GenerateCombinedTexture에 흙길 페인트 패스 — **53 세그먼트**(4방위 방사 스포크 r100→1500 + Ring3(550m)/Ring2(1000m) 링 도로 15° 폴리라인 + 스폰(728,-529)→East 스포크 수선 연결). 픽셀↔월드 매핑 동일 수식, AABB 사전 필터, 중심선 85%→가장자리 0% 스무스 블렌드(흙색 0.52/0.40/0.28, 폭 5m). 높이 평탄화 없음(시각 우선 — 2단계 과제).

**B3-2/3/4 밀도 최종:** 숲 게이트 0.55→0.48·개활지 스킵 70%→50%(숲 커버리지 상향) / 꽃밭 FLOWER_FREQ 0.012→0.009(패치 83m→110m) / 호수 bound ±1500 + **대형 호수 2개 수동 배치**((400,300) r180, (-560,-520) r150 — 기존 규약 waterLevel 계산 재사용, 시뮬레이션으로 무충돌 검증, 총 17개).

**판정 대기 (Play):** ① **빌더 재실행 후** 컨트롤러 재생성 확인(Player_AC_AC 중복 시 파일 교체 — 이전 절차) ② 달리기/스프린트 클립 끊김·미끄러짐 소멸([State] 진동 + [InputProbe] 120s 요약) ③ **흙길이 스폰→Ring1 경로에 표시**(스크린샷) ④ 숲 군락/대형 호수 2개/꽃밭 110m/메사 25% 스크린샷 ⑤ 성/영지/데코 부착 확인.

---

## 2026-09-07: Phase 1~3 — 로코모션 믹사모 전환 + 검 부착 + 잔디 전체 커버 + 가시화 ✅ (판정 대기)

**Phase 1 (클립 자연스러움):** ① 빌더 Create()를 **파일 레벨 스왑**으로 수리(DeleteAsset 의존 제거 — File.Delete+ImportAsset, Player_AC_AC 재발 원천 차단) ② **로코모션 믹사모 전환**(유저안 채택): Idle=`Idle.fbx`, Walk=`Walking.fbx`, Run=`Running.fbx`, Jump=`Standing Jump.fbx` — Attack/AttackCombo/Hit는 팩 OneHand 유지, Roll/Death 믹사모 유지. 병사는 검 든 유닛이라 팩 OneHand 유지 ③ Run 재생속도 0.2→**0.28**(믹사모 Running 자연 페이스 ~3.5m/s — 5m/s서 1.4배속) ④ **검 부착**: steel_sword.glb → RightHand 본(스케일 0.9m 정규화, try-catch) — 팩 공격 클립이 검을 전제하므로 공격이 자연스러워짐.

**Phase 2 (잔디 전체 커버):** RuntimeTerrainChunkManager에 **정적 병합 잔디** 추가 — 청크당 2500터프(4m 그리드, 교차 쿼드, 결정론 시드), 호수 반경 1.05r 내부 제외, 청크당 1드로우콜(20k 정점), 전체 약 128만 정점/16만 터프. 동적 밀집(45m, 예산 50→**100**)은 근접 디테일로 역할 분리.

**Phase 3 (가시화/다양성):** 텍스처 **256→1024**(12.5→3.1m/px — 흙길 5m 가시화; 씬 직렬화 값도 1024로 수정 — 코드 기본값만으론 미적용 문제), 메사 25→**35%**, 꽃밭 패치 110→**140m**(FLOWER_FREQ 0.007).

**주의:** Player_AC_AC.controller 잔재 삭제 완료. 컴파일은 에디터 포커스 시 최종 확인.

**판정 대기 (Play):** ① 빌더 재실행 → Player_AC 단일 확인 ② 검 부착 + 믹사모 로코모션 자연스러움(스크린샷) ③ 잔디 전체 커버 + 프레임 수치 ④ 흙길 표시(1024 텍스처) ⑤ 메사 35%/꽃밭 140m 체감 ⑥ [State]/[InputProbe] 수치.

---

## 2026-09-07: Phase 1~3 검증 완료 + 최종 컨트롤러 확정 ✅

**컨트롤러 바인딩 최종 확인(guid 대조):** Player_AC = Walk→`Walking.fbx` / Run→`Running.fbx` / Idle→`Idle.fbx` / Jump→`Standing Jump.fbx` / Roll→`Quick Roll To Run` / Death→`Standing Death Backward 01` / Hit→팩 `Hit_F_1` / Attack·Combo→팩 `OneHand_Up_Attack_1` + `m_Speed: 0.28`(Run 재생속도) + `m_EventTreshold: 2`(Run→Walk) — **설계 의도 100% 반영 확인**. Player_AC_AC 잔재 삭제 완료(디스크 4파일만 존재).

**구현 완료:** ① 빌더 파일 레벨 스왑(+AssetDatabase.Refresh — DB stale 중복 차단) ② 로코모션 믹사모 전환(병사는 팩 유지) ③ Run 재생속도 0.28 ④ 검 부착(steel_sword→RightHand) ⑤ 정적 병합 잔디(4m 간격 10,000터프/청크, 1.6×1.1m, 호수 제외, 1드로우콜) + 동적 예산 100 ⑥ 잔디 텍스처 디테일 노이즈(고빈도+패치 이중 변조) ⑦ 흙길 폭 7m/알파 0.92 ⑧ 텍스처 1024(씬 직렬화 수정 포함) ⑨ 메사 35% ⑩ 꽃밭 140m ⑪ 호수 대형 2개 추가 ⑫ **WeaponEquipManager + 인벤토리 무기 슬롯**(4종 검 장착/해제, 장착 하이라이트, GameSetup 기본 steel 이관).

**판정 대기 (Play):** ① 컴파일 에러 0 ② 검 든 믹사모 로코모션 자연스러움 ③ 잔디 전체 커버(정적+텍스처 디테일) ④ 흙길/메사/꽃밭/호수/숲 스크린샷 ⑤ I키 무기 장착/해제 동작 ⑥ [State] 진동 0 + [InputProbe] 120s 요약 ⑦ 프레임 수치.

---

## 2026-09-07: 애니 동결(Loop Time) + I키 배선 복구 ✅ (판정 대기)

**1. 애니 동결(56 포즈) 근본 원인 확정:** 믹사모 클립의 **Loop Time 미설정** — animationType: 3(Humanoid)은 정상이나 loopTime=false 기본값 → Idle/Walk/Run이 0.7~8.3초 1회 재생 후 **마지막 프레임에 동결**(DD4 증거: state=Run normT=2.31 클립길이 0.70s = 끝에서 클램프). "잠깐 재생되다 멈춤"의 정확한 메커니즘.

**수리:** MixamoControllerBuilder.BuildAll()에 `ConfigureMixamoClipLoop()` 추가 — Idle/Walking/Running/Standing Jump FBX의 clipAnimations loopTime=true 설정 후 SaveAndReimport(결정론, dirty 시에만). Roll/Death는 1회성이라 제외. **빌더 재실행 시 자동 적용.**

**2. I키 미개방 원인 확정:** **씬에 UI 윈도우가 0개** — InventoryWindow/UIManager 인스턴스가 런타임에 존재하지 않음(테스트 셋업만 존재, I키 배선 부재). 수리: ① GameSetup.Start에 **InventoryWindow 런타임 생성**(GameObject+AddComponent, 중복 가드) ② **UIInventoryHotkey**(신규) — I키 상승 에지 → Toggle() 배선(Keyboard.current 즉시 조회) ③ 시작 시 Hide(안전).

**검증:** 균형 3파일 0, OnGUI _isOpen 게이트 확인(신규 윈도우 무 draw), Hide 신규 호출 무해 확인.

**판정 대기 (Play):** ① `[MixamoControllers] 믹사모 클립 Loop Time 설정: N개 재임포트` ② 걷기/달리기 무한 루프(동결 소멸) ③ **I키 → 인벤토리 개방 + 무기 슬롯 장착/해제** ④ 검 든 믹사모 이동 스크린샷.

---

## 2026-09-07: A 이동 자연화 + B 인벤토리 개편 + C 지형 밀도 ✅ (판정 대기)

**A. 이동 가속/감속(끊김 근본 수리):** InputProbe+DD4 확정 — 유저의 W 탭(0.4~0.9s)마다 이동이 0↔5m/s 즉발 전환, 애니는 그대로 따라가 Idle→Walk→Run→Walk→Idle 사이클 반복(클립 재시작 = 끊김). 애니 로직은 정상 — **이동에 가속/감속이 없던 것**이 원인. 수리: PlayerMovement에 `_smoothedPlanarSpeed` 추가(MoveTowards 가속 12m/s²/감속 18m/s²), 방향은 즉시·속도만 램프. **함정 처리: _moveDirection.y에 _verticalVelocity가 들어있어 평면 성분(x,z)만 추출해 사용(미처리 시 정지 오판+속도 절반 버그).** 전환 블렌드 0.05→0.12s(빌더, 재실행 필요). 효과: W 탭 = Idle→Walk 중심 자연 전환, 홀드 = Walk→Run 자연 전환.

**B. 인벤토리 UI 개편(토탈코딩 스타일):** 현황 문제(카테고리 텍스트 잘림/얇은 폰트/무기 섹션 판독 불가/리스트형) → 렌더링만 재작성(127+/70-): 반투명 다크 패널(0.08,0.08,0.10,0.88)+금색 테두리, 카테고리 전체명 표기(탭 폭 확대), 5열 그리드 슬롯(64~72px), 하단 아이템 정보 패널(폰트 상향), 무기 버튼 2배(110×36)+장착 금색 테두리+✔. **데이터/WeaponEquipManager 장착 로직 무변경.**

**C. 지형 밀도/길 확보:** 숲 마스크 게이트 0.48→0.42+주파수 0.008→0.005(덩어리 200m 대형화)+개활지 스킵 50→30%. **흙길 위 나무/바위 배치 제외**(DirtPaths public 접근자 노출 — PathSegment public 승격(CS0053 방지), IsNearDirtPath 헬퍼: AABB+점-선분 거리 7m) → 나무/바위/클러스터 3곳 적용. 꽃밭/호수는 전 라운드 수치 유지.

**판정 대기 (Play):** ① 컴파일 에러 0 ② **탭 걷기 자연 전환 + 홀드 달리기**(끊김 소멸) ③ 인벤토리 개편(텍스트 잘림 0, 무기 섹션 가독) ④ 숲 군락/흙길 깨끗/꽃밭 140m ⑤ 프레임 수치.

---

## 2026-09-06: 애니 끊김 수리 — 정지 스냅 홀드 타이머 ✅ (판정 대기)

**증상:** 전 지형에서 애니는 작동하나 걷기/달리기 중 애니가 "자꾸 끊겨서 재생".

**원인 (Editor.log [State] 전환 로그로 확정):** HumanoidClipDriver의 정지 스냅이 raw<0.05 **3프레임(50ms)**만 유지돼도 _smoothedSpeed를 즉시 0으로 만듦 → 걷는 중 미세 정체(청크 이음새/구릉 접촉/경사 순간 정체)마다 Run→Walk→Idle→Walk→Run 전체 사이클이 돌고, 전환마다 클립이 normT=0에서 재시작 → 끊김. 증거: `Walk → Idle (speed=4.89 rawSpd=5.00)` — 0.05초 전환 구간 동안 raw가 이미 5.0으로 복귀했는데 상태 전환이 확정된 패턴 반복.

**수리:** 스냅 조건을 3프레임 카운터 → **0.25초 연속 홀드 타이머**(_stallTime)로 교체. 미세 정체는 스무딩이 흡수하고, 진짜 정지(0.25초)만 즉시 Idle. (커밋 cb8a7069)

**판정 대기 (Play):** ① 걷기/달리기 중 클립 끊김 소멸(눈확인) ② [State] 전환 빈도 대폭 감소(로그) ③ 정지 시 Idle 전환은 ~0.3초 내 유지(응답성).

---

## 2026-09-05: 접지 구조 개선 — 물리 접촉 우선 + 수식 안전망 (데코/건물 위 자연 착지) ✅

**직전 버전의 구조적 결함:** `ClampToGroundByHeight`가 매 프레임 수식(GetHeightAt)으로 캐릭터를 지표면+0.02에 **항상 스냅** → ①데코/건물 콜라이더 위에 서 있어도 지면으로 끌어내려 관통 ②물리 `isGrounded`가 false로 남고 AA3의 `Move(down*0.02)`가 수식 스냅에 캔슬되는 **배타 구조**. (지형지물 위 안정 접지 시나리오에서 실제 문제 발생 가능)

**수리 (PlayerMovement.cs `ClampToGroundByHeight()` 교체, code agent + QA agent PASS):**
1. **물리 접촉 우선** — `!isGrounded && vv≤0 && !rolling`이면 `CC.Move(down*0.05)`로 지형/데코/건물 콜라이더와 실제 충돌 유도, 접지 즉시 `_isGrounded=true`·`return`(수식 개입 없음). 접지 상태에선 추가 Move 없음 → 이중 Move 아님.
2. **이탈/추락 안전망(수식)** — 지형 메시는 `TerrainTextureApplier`가 GetHeightAt으로 재표본되므로 GetHeightAt=지표면과 정확 일치. `feetY < formulaY−0.5`(지형 콜라이더 유실·낙하 위험)만 수식으로 복귀, `feetY < formulaY+0.02`(소량 파묻힘)만 표면 정렬. **`feetY ≥ formulaY+0.02`(데코 위·구릉 정상)= 개입 안 함 → CC가 자연 접지 유지.**

**QA PASS:** 점프(`_isJumping` early-return, vv≤0 확정으로 조기해제 없음)·구르기(MovePlayer early-return이라 clamp 미호출)·데코 위 접지(`feetY≥formulaY+0.02` 분기 미발동→0.05m nudge로 자연 충돌) 흐름 정상. 중괄호 127/127·괄호 339/339, `formulaY` 전역 규약(1f+GetHeightAt Plains42) 일치. **배치 컴파일 error CS=0, warning CS=0.** (관찰 권장: Plains/42 하드코딩 — 바이옴·시드 동적화 시 재검토; 구버전 `ClampToGround()` 죽은 코드 잔존)

**판정 대기 (Play):** ① 나무/바위/건물 위에 올라섰을 때 지면으로 끌려내려가지 않고 그 콜라이더 위에 안정 착지 ② 경사 내려가는 중 캐릭터가 지면을 따라 붙음(허버 없음) ③ 발밑 접지 그림자가 지면에 상시 밀착. 에디터 Play 후 새 스크린샷 판정.

---

## 2026-09-05: 지형 접지감 수리 + 프로젝트 컴파일 블로커 해제 ✅

**문제:** 지형지물(나무/바위/잔디)+플레이어가 땅에서 떠보임. y 수학은 전 시스템이 공통 기준(`GROUND_BASE=1f + TerrainGenerator.GetHeightAt(x,z,Plains,42)`)으로 정확히 일치했으므로 재정렬이 아니라 **접지감(접촉 그림자 + 실제 물리 접지) 부재**가 근본 원인. CollisionDebugger 로그로 확정: 플레이어 `pos=(752, 3.97, -515) isGrounded=False` 지속 + `_verticalVelocity` 누적(중력이 물리적으로 해소 안 됨) — ClampToGroundByHeight가 매 프레임 위치를 텔레포트로 고정해 CC가 실 접촉을 하지 못한 구조.

**수리 (PlayerMovement.cs, code agent):**
1. **플레이어 동적 접지 그림자(BlobShadow) 부착** — Start()에서 `GetOrAdd<BlobShadow>()` (GetOrAdd 중복 방지, try/catch 실패 시 경고 후 계속). BlobShadow는 LateUpdate에서 `GetHeightAt+GROUND_BASE+0.05`로 발밑 고정 그림자(r=0.8/α=0.35).
2. **물리 접지 복구** — ApplyGravity에서 `_controller.isGrounded==false && _isGrounded && vv<0 && !_isRolling`이면 `CC.Move(down*0.02)` 1회로 실 충돌 유도해 isGrounded=true(접지 시 vv=-2 규약 유지). 점프 상승·구르기는 가드로 미개입.
3. **ClampToGroundByHeight 개조** — 텔레포트 대신 CC.Move(up/down)로 물리 접지 유지 + 0.5m 초과 파묻힘/이탈 시 최후 하드 스냅. 파묻힘 0.5m 이내·공중 0.5m 이내는 무개입(gravity가 자연 착지).

**추가 수리 (HeatAvatarMappingFix.cs, 24건 컴파일 에러 → 0):**
애니 8차 도구가 **무존재 타입 `HumanDescriptionBone`**과 **`HumanLimit.value/length/modified`**(존재 X)를 사용해 프로젝트 전체를 미컴파일 상태로 만들고 있었음 → 모든 Play 판정이 구(스테일) 어셈블리로 돈 셈. 교정: `HumanBone[]`(boneName/humanName/limit) + `HumanLimit{useDefaultValues,min,max}`. **교훈: 8차 "QA PASS 4/4"는 문법 검증일 뿐 실제 API 타입/필드 오류를 놓침 — 배치모드 컴파일로 error CS=0 검증이 선행돼야 함.**

**QA PASS:** code agent(PlayerMovement) + code agent(Heat 교정) + QA agent 리뷰(양 파일 PASS, 메서드 단일·중괄호 균형·점프/구르기 흐름 무결·`1<<9`=Ground 검증). **배치모드 컴파일 error CS=0, warning CS=0.**

**판정 대기 (Play):** ① 플레이어 발밑 접지 그림자 상시 확인 ② 걷기/경사에서 머무르다 멈출 때 캐릭터가 지면에 붙는(허버 없음) ③ 지형지물 밑동이 지면에 닿아 보임. 에디터 Play 후 새 스크린샷으로 판정.

---

## 2026-09-06: 애니 9차 — FBX 골격 위계 수리 (매핑 시프트의 근본 원인) ✅

**문제:** 8차 매핑 주입 후에도 동결 지속. DD3-1이 결정 증거 포착 — 아바타 매핑이 `Spine→Hips, Chest→Spine, UpperChest→Chest, Chest2→UpperChest`로 **강제 시프트**되고 정작 Hips 본 탈락. Blender hier_probe로 파일 진단 → **다리(Left/RightUpperLeg)가 Hips가 아닌 Spine의 자식** + 목/어깨/breast가 비표준 Chest2 아래. Unity 휴머노이드 위상규칙(다리=Hips 직계) 위반 → Unity가 어떤 메타 매핑이든 임포트 시 강제 재배치 → 근육값이 어긋난 뼈에 기록 → 동결. (8차까지의 "매핑 비어있음/자동매핑" 가설은 모두 이 위계 문제의 하위 현상이었음)

**수리 (Blender 5.1 headless, roll_make/fix_hierarchy_v2.py):** edit 모드에서 `use_connect=False` + parent 재할당만 (행렬 복원 코드는 오히려 본을 밈 — 제거): 다리 2개→Hips, Neck/LeftShoulder/RightShoulder/breast.L/R→UpperChest, pelvis.L/R은 Spine 아래 유지. **검증: 전 본 head/tail 무손상(identical), 버텍스그룹 불변, roundtrip 재임포트로 위계/본수 27 확인.** 백업: roll_make/Player_Rigged_Heat_backup_before_reparent.fbx. 신규 지식: **원본부터 Hips 본은 메시 가중치가 없음(26/27) — rootBone 앵커 역할만 하므로 무해(스파인/다리 가중치로 움직임 전달).** 8차 메타 매핑(22개)은 이제 위상검증 통과 — Unity가 더 이상 시프트하지 않아야 함.

**판정 대기 (Play):** ① DD3-1 매핑에 `Hips→Hips` 포함 + 시프트 소멸(Spine→Spine, Chest→Chest...) ② 직선 이동 중 LFootΔ/LHandΔ가 회전 설명치 초과(주의: DD3-2 상대Δ는 루트 회전에 오염됨 — 직선 보행 기준 판정) ③ 보행 스윙 눈확인(56.PNG와 다른 포즈) ④ `[JumpProbe] grounded=` 값.

---

## 2026-09-05: 애니 8차 — 아바타 매핑 명시 주입 (RPG팩·믹사모 동결의 근본 수리) ✅

**문제 확정:** RPG팩/믹사모 무관하게 몸이 "걷기 한 프레임" 자세에서 동결(56.PNG). Animator 상태 전환·normT 진행·SMR·아바타 isHuman 전부 정상으로 보였으나 **Heat 메타의 `humanDescription.human`이 `[]` (매핑 0개)**. 7차에서 "자동매핑 유도" 목적으로 비운 것이 원인 — 사지(Limb)가 미매핑되면 Run/Walk 클립의 근육값이 행선지가 없어 몸은 임포트 시점 자세(bake_anim=False → export 시 자세)에 영원히 동결.

**오판 유발 요인 2개 (교훈):** ① `isHuman=True`는 매핑 0개여도 True ② `GetBoneTransform(Hips)`는 Hips만 매핑돼도 non-null — hipsΔ는 보행 판정 지표로 무용(월드 이동이 지배). 사지 상대Δ를 봤어야 했음.

**수리:** ① 메타에 22개 표준 Humanoid 매핑 명시 기록(boneName=humanName, canonical 순서, soldier 참조 서식, +177/−1) ② 복구 도구 `Assets/Editor/HeatAvatarMappingFix.cs` — Tools/Anim에 매핑 적용(ModelImporter API + SaveAndReimport)·덤프 메뉴 ③ DD3 진단기(HumanoidClipDriver): 매핑 덤프+실질매핑 n/55 스캔, 사지 상대Δ(LHandΔ/LFootΔ), SMR 외부골격 검사 ④ PlayerMovement [JumpProbe]: Space 입력 시 grounded/rolling/mount/vv 스냅샷.

**QA PASS (4/4):** 메타 YAML 파싱·22본 대조(rerig_report bones_final)·API 검증·균형 검사 통과. 누락된 HeatAvatarMappingFix.cs.meta는 QA가 생성.

**판정 대기 (Play):** ① DD3-1 `매핑 본수=22` + `실질매핑≥17` ② 이동 중 `LHandΔ/LFootΔ > 0` ③ 보행 스윙 눈확인 ④ Space → `[JumpProbe]` 로그의 grounded 값. **JumpProbe 로그가 아예 안 찍히면** 구르기(_isRolling) 잔존 또는 탑승(MountSystem) 잔존이 점프 원인(probe가 그 검사보다 뒤에 있음).

---

## 2026-09-05: 애니 7차 — Heat meta 재작성 + 틴트 타입검사 수리 + hips 가드 ✅

**문제1:** 6차에서 손으로 만든 meta의 스키마 오류(animations 블록 안 animationType, human 엔트리 1개, 중복 키)로 Unity가 Heat FBX를 **Generic 아바타로 임포트** → `InvalidOperationException: Avatar is not of type humanoid` (GetBoneTransform).
**수리:** meta 전면 재작성 — human: [] / skeleton: [] 빈 리스트 + animationType: 3 + avatarSetup: 1 → Unity가 히트 본 이름(Hips/Spine/Head/LeftUpperLeg...)을 **자동 매핑** (guid 유지 b7c3d9e2f1a64c5e8d0b2a7c4e6f8a91).

**문제2:** 틴트 경고 스팸 재발 — FindPropertyIndex==-1인데 기본값 Color로 SetColor하는 빈틈(936행). **수리:** idx<0이면 _Custom_Color 세팅을 건너뛰고 _Color→_BaseColor 폴백.

**문제3:** DD2 진단기가 non-humanoid 아바타에서 GetBoneTransform 예외. **수리:** 측정 전 avatar.isValid && isHuman 가드, 아니면 `hips=N/A(nonhumanoid)`.

**QA PASS:** 3파일(+36/−65), 중복키 0, 괄호 균형 완벽. 커밋 48a3a885.

**다음 Play 판정:** ① 에디터 포커스 → Heat FBX 재임포트(자동 아바타 생성) ② 이동 시 몸 동작 + `Idle→Walk→Run` ③ DD2 `avatar isHuman=True` + 예외 소멸 ④ 틴트 경고 스팸 소멸. 여전히 정지면 rigImportWarnings 로그 제출 → Configure 매핑표 수리.

---

## 2026-09-05: 애니 6차(A안 실행) — Blender 리그 변환 Player_Rigged_Heat.fbx 통합 ✅

**근본 원인 최종 확정:** 원본 Player_Rigged의 리그 = Blender Rigify 커스텀(27본: Root/pelvis.L·R 분리/spine.001~.005/head·neck 없음). Unity Humanoid 필수 본(Hips/Spine/Head/좌우 사지 히트명) 미충족 → 아바타 isValid=True에도 **리타겟 대상 뼈가 없어 시각 변화 0**. (DD2 최종판: Neural=0 Hybrid=0에도 뼈 정지 — 재생 시스템 전부 정상, 모델 리그가 원인. DD1 로그 hipsΔ 0.4~7.9m은 Animator 재생 입증)

**A안 실행 (Blender 3.6 headless, roll_make/rerig_heat.py):**
- 본 리네임 매핑 23개: Root→Hips, spine→Spine, spine.001→Chest, spine.002→UpperChest, spine.004→Neck, **spine.005→Head(목-머리 부재 보정)**, shoulder.L/R→Left/RightShoulder, upper_arm→UpperArm, forearm→LowerArm, hand→Hand, thigh→UpperLeg, shin→LowerLeg, foot→Foot, toe→Toes
- breast.L/R, pelvis.L/R은 무매핑 유지(Humanoid 필수 아님)
- 메시 버텍스 그룹 자동 리네임(뼈 이름 추적)
- export: bake_anim=False(모델 전용 — 클립은 Player_AC 담당)
- **검증: 필수 17본 전부 존재, 메시 required_hit 17/17**

**산출물:** `roll_make/Player_Rigged_Heat.fbx` → `Assets/Resources/Models/UserProvided/fbx/` 복사 + Humanoid meta(guid b7c3d9e2f1a64c5e8d0b2a7c4e6f8a91, animationType 3, avatarSetup 1) + GameSetup.cs 로드 경로 `Player_Rigged`→`Player_Rigged_Heat` (머티리얼 원본은 GLB 그대로)

**주의:** .gitignore:43 `models/` 룰이 Assets/Resources/Models/를 무시 → **git add -f로 강제 트래킹** (기존 Player_Rigged.fbx도 미트래킹 상태였음 — 인지할 것)

**검증 수치:** missing_required=[] / mesh vertex_groups=27 hit=17/17 / bones_final 27개

**판정 대기 (Play):** 이동 시 몸이 직접 Idle→Walk→Run. 리타겟은 근육공간이라 팩 클립이 Heat 아바타로 재생됨(본 이름 무관).

---

## 2026-09-05: 애니 5차 수리(정책 반영) — Neural/Hybrid 자동부착 완전 제거, Player_AC 단일 경로 확정 ✅

---

## 2026-09-07: 플레이어 소유 성 내부 — 중세 판타지 + 저장고/무기고/작업대 상호작용 ✅

**파일:** `Systems/PlayerCastleInteriorBuilder.cs`, `UI/TerritoryCraftingStation.cs`, `UI/TerritoryWarehouse.cs`

**배경/결정:** 예시 사진은 구조만 참고 — **내부는 중세 판타지 톤**으로 재작성. 자신 소유 영지 성 내부(PlayerCastleInteriorBuilder)를 기능 기지 → 중세 대전당(석재 기둥 2열/화로·토치 주황 점멸/문장 방패/붉은 러그/촛불 조명)으로 변환.

**상호작용(신규):**
- **Phase A 작업대** — `TerritoryCraftingStation` 부착: E키→`CraftingUI`, R키→`RepairStationUI`. 레벨 제한 옵션 유지.
- **Phase B 저장고** — `TerritoryWarehouse` 부착: E키→창고 UI, 영지 키 기반 창고(20슬롯).
- **Phase C 무기고** — `TerritoryWarehouse` 부착: **무기고 전용 키(territoryKey+"_armory")**로 저장고와 슬롯 분리.

**어셈블리 경계(핵심 트러블샷):** `Systems asmdef`가 `UI asmdef`를 참조하면 **순환 참조**(UI가 이미 Systems 참조) → `using ProjectName.UI;`는 `CS0234` 발생. 해결 = `AttachUiComponent(GameObject, string typeName, params object[])` **리플렉션 헬퍼**(`Type.GetType("ProjectName.UI.X, ProjectName.UI")` → `AddComponent(Type)` → `GetMethod("Configure")` → `Invoke`, 선택적 매개변수 기본값 채움). 아키텍처에 박아두기.

**Configure API 추가(기존 동작 보존):**
- `TerritoryCraftingStation.Configure(string territoryId, string stationName=null, float? interactRange=null)`
- `TerritoryWarehouse.Configure(string territoryId, int maxSlots=20, float? interactRange=null)`

**검증:** 배치모드 error CS=0 (Systems.dll 20:08) + 파일별 중괄호 균형 PASS.

**Play 판정 대기:** 점령 후 내 영지 성문 E → 대전당, 각 시설 근접 시 `[E]` 표시 → 창고/크래프트/수리 UI 정상 열림. (CraftingUI/RepairStationUI 프리팹이 UIManager/씬 시 실제 열림 확인 필요)

---

## 2026-09-08: 유저 제공 로코모션 FBX 8개 플레이어 부착 ✅

**입력**: `Assets/플레이어 애니메이션/` FBX 8개(idle/walk/run/jump/back walk/back run/좌·우 change direction — Blender Rigify 리그, 메시+텍스처 임베디드)

**파이프라인**: Blender 5.1 headless(`roll_make/rerig_useranim.py`) 본명 표준화(rerig_heat.py MAP 23본: Root→Hips~toe→Toes) + 메시 제거 클립전용 FBX → `Assets/Animations/MixamoUser/` (검증: 필수 17본 충족, AnimStack 존재)

**임포트**: `ModelImporterAnimationType.Human`(Unity 6000.4에서 Humanoid→Human 리네임됨) + loopTime 6개(idle/walk/run/back_run/back_walk/jump=true, change_direction=false). **빈 clipAnimations → `defaultClipAnimations` 폴백 필수**(서브에셋 클립 존재해도 정의 배열은 0개)

**컨트롤러**: Player_AC 로코 4슬롯 교체(idle/walk/run/jump), Roll/Attack/Hit/Death 유지, run.speed=0.28+Speed바인딩·T(0.12s)·히스테리시스(0.55/0.35, 4.5/2.0) 유지. back walk/run·change direction 4개는 미배선(후진/선회 파라미터 부재 — 후속 과제)

**근본 원인 발견(중대)**: Player_AC_AC 중복 사고(09-03/09-07)의 **진짜 원인 = Create("Player_AC") + 템플릿 `{name}_AC.controller` 이중 접미사**(HEAD부터 존재한 버그 — 파일삭제+Refresh 패치는 표적을 잘못 짚었었음) → `Create("Player")` 1단어 수리 + Create에 3회 잔존검증루프·최종경로 LogError 로깅 경화

**검증**: 배치컴파일 error CS=0(R4 로그) + Player_AC.controller 재생성 확인(경로 검증 로그) + 4클립 GUID 참조 refs=1/1/1/1 + loopTime=1 + Player_AC_AC 0. QA: 파라미터 계약·전환값 전부 유지, 병사 3개 무변경

**운영 노트**: ①animationType 전환 직후 동일 배치 세션의 LoadAllAssetsAtPath 스테일 가능 → 임포트/빌드 세션 분리 ②유니티 배치는 에디터 닫힌 상태에서 WSL 직접 실행 가능(서브에이전트 600s 타임아웃 2회 발생 — Unity 장기작업은 부모가 백그라운드 실행하는 게 안전)

**Play 판정 대기**: ①Idle/Walk/Run/Jump 동작 ②loop 반복 정상(동결 없음) ③발 미끄러짐(run 클립 페이스 튜닝 여지) ④검 부착 위치 유지 ⑤롤/전투/사망 기존 클립 정상

---

## 2026-09-08: 지형 다양화 3차(T-D3) — 잔여 Gap 소거 ✅ (T5 능선길은 후속)

**계획서**: `.hermes/plans/2026-09-08_terrain-diversity-3.md` (예시 6·3 재분석 수치 반영). 구현 6파일.

- **T1-1/T1-2 마스크**: TerrainShape에 GetTerraceBlockMask(방위당 2~3블록 320m 셀, Empire 공유세트)·GetRidgeBoostMask 신설 — 소비 계약 주석 포함(Generator 테라스 carve/진폭 델타는 다음 단계)
- **T1-3 호수 만(cove)**: ApplyLakeBasins 반경 **축소-only 변조(최대 -8%)** — 방향각 2옥타브 sin, 밴드가 안쪽으로만 당겨져 1.7r 이격 무겹침 유지(안전 근거 주석)
- **T2-1 수면 바위**: PlaceLakeSurfaceRocks — 호수당 6~12, 0.3~0.7r, 수면 위 0.3~0.6m 노출(예시3 바위:수면 3:7 체감)
- **T2-2 연잎/연꽃**: PlaceLilyClusters — 군집당 연잎 5~10+꽃 1~2, 호수 면적 비례 캡(수면 데코 ≤20%)
- **T2-3 하천 5개**: TerrainRiverDef(방위별 절차 호수 기반, 5제어점 S자, 폭 4~7m·깊이 0.8~1.4m) + GetRiverMask/CarveDelta/SurfaceY + ApplyRiverCarve(**min-only, 낙차 0**) + GenerateAllRivers 수면 스트립(호수 수면 재질 공유)
- **T2-4 수변 수양버들**: PlaceLakeshoreWillows — 둘레 20~30% 호, Pink=황제국 전용
- **T3-1 수종 규칙**: TryPlaceTree 훅 — 능선(ridgeBoost>0.4)=침엽 80/활엽 20(FirPool), 수변 18m=버들 40/활엽 60(ShorePool, 예시6 비율)
- **T4-1 꽃융단**: 마스크 반경 160/200→**352/440(2.2×)**, 배치 산포 18~30m 등가 + MegaCapFor 60~80/국가
- **T4-2 방위별 색**(NationTerrainController): 동=흰/노랑+빨강 포인트, 남=빨강, 북=흰 설화, 서=황토, 황제=핑크·마젠타

**미완(후속)**: T5 능선 타는 길(기존 53세그먼트 경로 시스템 정합 비용 — 별도 세션) / T1-1·T1-2의 Generator 소비(테라스 carve·진폭 델타) / T3-2·T3-3(기존 숲 마스크·빨간 수종이 커버)

**트러블슈팅**: 서브에이전트 600s 타임아웃 6회(모델 응답 지연) → **단일 패치 위임 패턴**(부모가 코드 조립 후 정확한 old/new 제공, 에이전트는 적용+검증만) 전환 후 전부 성공. 컴파일러가 잡은 수리 2건: MegaCapFor 잔존 참조 4건, GenerateAllRivers lakes 스코프 오류.

**검증**: 배치컴파일 error CS=0(unity_compile_td3_4) + QA agent PASS(중괄호 균형 5파일 0, GROUND_BASE+GetHeightAt 규약 17건 유지)

**Play 판정 대기**: ①호수 만/수면 바위/연꽃 ②하천 흐름(낙차 0) ③수종 혼합(능선 침엽·수변 버들) ④꽃융단 확대+방위색 ⑤기존 요소 무손상

---

## 2026-09-08: Meshy 애니메이션 60클립 전량 등록 + 전투 5슬롯 교체 ✅

**입력**: `Assets/플레이어 애니메이션/` Meshy_AI biped FBX 60개(withSkin, 폴더 전면 교체 — 구 Rigify 8종 소멸)

- **파이프라인**: `roll_make/rerig_meshy.py` — Meshy biped 21본 MAP(LeftUpLeg→LeftUpperLeg, Spine01→Chest, Spine02→UpperChest, neck→Neck 등) 표준화 + 메시 제거 클립전용 → `Assets/Animations/MeshyUser/` 60개(필수 17본 전부 충족)
- **임포트**: ConfigureMeshyImports 신설 — 60개 Human 강제 + loopTime 규칙(파일명 walk/run_/running/swim/crawl/carry/sneaky/spear/idle_turn=true, transition/toss/pitching=false) + defaultClipAnimations 폴백
- **Player_AC 전투 5슬롯 교체**: Attack=Right_Hand_Sword_Slash, AttackCombo=Double_Combo_Attack, Hit=Hit_Reaction, Death=Dead, Roll=Roll_Dodge(팩 OneHand·구 믹사모 사망/구르기는 병사가 계속 사용). 로코 4종(idle/walk/run/jump)은 기존 MixamoUser 유지 — **Meshy 세트에 Idle·Jump 부재**
- **트러블슈팅**: Blender 출력 파일명에 _withSkin 잔존 → 슬롯 참조 불일치("클립 없음" 5건) → 파일명 정규화+meta 재생성 후 R3/R4 해소
- **검증**: error CS=0 + 전투 5 GUID refs=1/1/1/1/1 + 로코 4 GUID 유지 + loop 규칙(Walking=1, Sword_Slash=0) + 커밋 3b3b4a63

**Play 판정 대기**: ①검 공격/콤보 모션 ②피격/사망/구르기 반응 ③로코 4종 기존 정상 ④전투 클립 페이스(발 미끄러짐 없음)

---

## 2026-09-08: 이동 파라미터 확장 — 변형 클립 9종 활성화 ✅ (커밋 118a92e0)

- PlayerMovement.LocalMoveDirection 신설(로컬 이동 벡터 X=측면/Z=전후, clamp) → HumanoidClipDriver가 MoveX/MoveY 실시간 전송 + 점프 시 MoveY<-0.2면 JumpBack 트리거
- Player_AC: 파라미터 3종(MoveX/MoveY Float, JumpBack Trigger) + 상태 9종(WalkBack/JumpBack/WalkTurnL·R/RunTurnL·R/RunSharpTurnR/IdleTurnL·R) + 전이 20건 추가
- 활성화 클립 9개: Walk_Backward, Back_Jump, Walk_Turn_L/R, Run_Turn_L/R, Run_Sharp_Turn_Right, Idle_Turn_L/R
- 검증: 배치컴파일 error CS=0 ×2 + 상태 m_Name/클립 GUID 등록 확인(9종 전부)
- **미완(후속)**: 전투 모드 변형 4종(Walk_Backward_with_Sword, ForwardLeft/Right_Run_Fight, Walk_Turn_Left_with_Weapon — WeaponEquipManager 훅 필요) / Run_to_Walk_Transition / Harvest·HitLight·Stun 발화 연결 / 콤보 3단(Triple_Combo, Weapon_Combo_2)

---

## 2026-09-08: 전투 모드 변형 4종 + RunToWalk 활성화 ✅ (커밋 c404de94)

- **IsCombat 게이트**: WeaponEquipManager.IsEquipped(정적) → HumanoidClipDriver가 SetBool 전송. 일반 이동 전이에 IsCombat IfNot 게이트 추가(T2 헬퍼 신설 — 2조건 AND)
- **전투 모드 변형 4종**: Walk_Backward_with_Sword(후진), ForwardLeft/Right_Run_Fight(측면 주행), Walk_Turn_Left_with_Weapon(선회) — 검 장착 시 자동 전환, 해제 시 일반 복귀
- **RunToWalk**: Run_to_Walk_Transition — Speed 2.0 임계 하향 통과 시 드라이버가 1회 발화(_prevSpeedForTransition 엣지 감지)
- **활성화 클립 +5**: Walk_Backward_with_Sword, ForwardLeft/Right_Run_Fight, Walk_Turn_Left_with_Weapon, Run_to_Walk_Transition → **누적 26/68**
- **검증**: error CS=0 + 상태 5종/IsCombat 조건 12건/클립 GUID 5종 등록 확인
- **비고**: WSL interop vsock 일시 장애로 Unity 배치 3회 실행 실패(accept4 timeout) 후 복구 — 파이프라인 exit 코드 마스킹 주의(tail 파이프 시 $? 무의미)
- **남은 미사용 42클립**: 시스템 신설 그룹(Swim 2/Crouch 4/Sneaky/Crawl/Parry/Bow 6/Ride/Carry/Climb 3/Door/Talk 2/victory/mage/baseball/Pull_Throw) — 각각 별도 설계 Phase

---

## 2026-09-08: 콤보 3·4단 확장 + 다운 상태 + 발화 연결 완료 ✅ (커밋 75d27e6b)

- **콤보 확장**: HumanoidClipDriver 4분기 체인(≥4→AttackCombo3=Weapon_Combo_2, ≥3→AttackCombo2=Triple_Combo_Attack, ≥2→AttackCombo, else Attack) + Player_AC 상태/전이 추가
- **다운(Knockdown)**: Shot_in_the_Back_and_Fall — 상태/파라미터/AnyState+ExitTo 추가
- **발화 연결**: ①HerbPickup.Harvest() 2곳 → TriggerHarvest()(클립 Pull_Radish, 기존 프로시저 gather 병행) ②ProceduralAnimStateMachine.TakeDamage → 데미지 등급 발화(≥40 Knockdown / ≥25 Stun / else HitLight) — HumanoidClipDriver 퍼블릭 트리거 4종(TriggerHarvest/HitLight/Stun/Knockdown) 신설 경유
- **검증**: 배치컴파일 error CS=0 ×2 + AttackCombo2/3·Knockdown 상태 + 3클립 GUID 등록 확인 + 커밋 푸시 + 텔레그램 알림(9471)
- **누적 활성화**: 68클립 중 21개(로코4+이동변형9+전투5+상태3)

---

## 2026-09-08: 애니 시스템 전면 구현 — Phase A/K/B/C/D/E/F/G/H/I 완료 ✅ (59/68, 커밋 e51921e2→0324ec96)

계획서: `.hermes/plans/2026-09-08_anim-systems-plan.md`. 신규 상태 30종, 상호작용 스크립트 3종 신설.

- **A 줍기**: HerbPickup 채집 클립 3-way 랜덤(Pull_Radish/Collect_Object/Bend_Over_Pick_Up) — TriggerHarvest 계열 3종
- **K 사방 후퇴**: BackLeft/Right_run — MoveY<-0.3 && |MoveX|>0.45 (T2 AND 게이트)
- **B 문**: DoorInteractable.cs 신설 — 문 피벗+E키 토글 개폐(회전 코루틴)+플레이어 open_door 트리거(씬 배치 필요: 문 오브젝트에 부착+_doorPivot 할당)
- **C 대화·승리**: TutorialQuestNPC.StartDialogue → Talk 2종 랜덤 / ArenaSystem fighterWon → Victory
- **D 좌석/침대/음료**: SeatInteractable.cs 신설(E키 착석→SitHold 루프, 침대 모드 6초마다 Toss, 이탈/E재입력→SitUp) / Stand_and_Drink=소비 아이템 사용 훅(ConsumableSystem → BroadcastMessage 우회, asmdef 제약)
- **E 웅크림**: PlayerMovement Ctrl 토글+속도 ×0.5+CrouchF/B/L/R 4방향 스왑(IsCrouch 게이트) — **주의: Ctrl이 은신(누름)과 웅크림(뗌) 공유 → 키 분리 권장(C키 등)**
- **H 수영**: NearestWaterSurfaceY(호수+하천 마스크) 판정 → 수면 부유 보정+속도 ×0.6+SwimI/F 스왑, 수영 중 점프/구르기 차단
- **F 기동**: LadderInteractable.cs 신설(E키 연출) — 실제 y 이동은 추후 / ClimbStairs·WallDown 상태 등록
- **G 전투 변형**: AttackThrust/AttackBase/Charged/Parry 상태 등록(차지 홀드·패리 타이밍 판정은 전투 코드 연결 후속)
- **검증**: 배치컴파일 error CS=0 ×2 + 신규 상태 전부 m_Name/GUID 확인

**남은 미사용 9클립**: 활 5종(Archery_Shot, Draw_and_Shoot, Bow_walk 3종)+Spear_Walk(Phase J 활/창 무기 시스템 — 大), Crawl_Backward(기어 심화), baseball_pitching+Walk_Backward_with_Grenade(투척 시스템 — Phase I 잔여)

---

## 2026-09-08: Phase J/I 완료 + 키 재배치 — 67/68 활성화 ✅ (커밋 73f1b975)

- **키 재배치**: C=은신(상승엣지), Ctrl=웅크림(상승엣지) — 공유 문제 해소
- **모드 토글 3종**(상호 배타, PlayerMovement): B=활 모드, V=창 모드, G=투척 모드 + IsBowMode/IsSpearMode/IsThrowingMode public
- **Phase J 활**: BowEnter 트리거 진입 → BowAimedF(전진)↔BowBack1/BowBackAimed(후진) 스왑(IsBow 게이트) + 우클릭 ArcheryShot + DrawShoot 장전 + 화살 연출(SpawnProjectile — ArrowProjectile.Spawn API 확인, 데미지 연동은 후속)
- **Phase J 창**: SpearEnter → SpearWalk(IsSpear 게이트 해제)
- **Phase I 투척**: ThrowEnter → GrenadeBack(후진 스왑) + 좌클릭 Throw/ThrowPitch 랜덤(Crouch_Pull_and_Throw/baseball_pitching) + 투사체 연출
- **기어**: 웅크림 후진 클립 Crawl_Backward 스왑
- **콤보 1단 3종 랜덤**: Attack/AttackThrust/AttackBase
- **검증**: error CS=0 ×2 + 신규 상태 10종 등록 확인
- **결과**: **68클립 중 67개 활성화** — 유일 잔여 Cautious_Crouch_Walk_Backward(웅크림 후진을 Crawl로 스왑한 대체 여유본)
- **임시 구현 주의**: 활/창/투척 모드는 B/V/G 임시 토글 — 정식 무기 시스템(모델+WeaponData+화살 데미지)은 별도 Phase. ArrowProjectile.Spawn(position, direction, speed, damage, trailColor) 공개 API 확인 완료

**Play 판정 대기**: ①C 은신/Ctrl 웅크림 분리 동작 ②B 활 모드 이동+우클릭 발사 ③V 창 걷기 ④G 투척 모드+좌클릭 던지기 ⑤웅크림 후진 기어 ⑥검 콤보 1단 랜덤 3종

---

## 2026-09-08: 무기/핫바 정식 시스템 M1/M2/M3/M4 완료 ✅ (커밋 fcdc3211)

계획서: `.hermes/plans/2026-09-08_weapon-hotbar-plan.md` (장비 예시.PNG 8슬롯 스펙). **모델 전부 존재 확인**(crystal_bow.glb, 창 4종 glb, bomb.glb, Resources/Bombs/ 프리팹 4종, arrow 3종) — M6 외부 의존 해소.

- **M1 핫바**: HotbarUI.cs 신설(8슬롯, Alpha1~8, 선택 흰 테두리, 스택 수량 표시) — 슬롯: 1 검(steel)/2 활(crystal)/3 창(wood)/4 폭탄/5~8 확장. WeaponEquipManager 직접 호출(UI→Systems 허용)
- **M2 장착 연동**: WeaponEquipManager.Equip(id, player, WeaponType) 타입 대응(프리팹 경로 {id}_sword/_bow/_spear — **id는 "steel" 같은 기본명, suffix 자동 조합**)+CurrentType 노출+Unequip 시 Fist 리셋+PlayerCombat.SetWeapon() 헬퍼 신설(장착 무기 데미지/사거리 반영). 드라이버 게이트를 CurrentType 기반으로 교체(IsCombat=≠Fist, IsBow/IsSpear=타입 일치), **PlayerMovement 임시 B/V/G 토글 완전 제거**
- **M3 활 데미지**: 우클릭 → ArcheryShot + `ArrowProjectile.Spawn(origin, forward, 22f, WeaponData.Bow.damage=8, color)` — 화살 데미지 연동 완료
- **M4 폭탄**: 핫바 4 선택 → PlayerWeaponModeBridge.ThrowSelected(신설 정적 브리지 — UI↔드라이버 asmdef 우회) → 드라이버 ThrowEnter(GrenadeBack) → 좌클릭 Throw/ThrowPitch 랜덤 + Resources/Bombs/Bomb_Explosive 스폰+Rigidbody 포물선(기존 퓨즈·폭발 시스템 작동), 1회 투척 후 ThrowSelected 자동 해제
- **검증**: 배치컴파일 error CS=0 ×2 + 상태/파라미터/브리지 확인
- **규약 신설**: Equip id는 "steel"/"crystal" 같은 기본명(접미사 _sword/_bow/_spear 자동 조합) — 풀네임 전달 시 경로 깨짐
- **남은 것**: M5 창 공격(Thrust 연결), 착석/문/사다리 오브젝트 에디터 배치, 활 프리팹 시각 확인

---

## 2026-09-08: M5 창 공격 + QA 보강 완료 ✅ (커밋 7422c09b)

- **M5**: 창(WeaponType.Spear) 장착 시 콤보 1단 = Thrust_Slash(찌르기) 발화, 2단 이상 기존 체인 유지
- **QA 발견 보강**: 웅크림/수영 bool 피드 누락(IsCrouch/IsSwimming SetBool이 드라이버에 없어 상태가 절대 발화 안 되던 버그) → HumanoidClipDriver에 SetBool 2줄 추가
- **검증**: 배치컴파일 error CS=0 + Player_AC 재생성 확인 + 텔레그램 알림(9490)

**최종 상태: 68클립 체제 완성(활성 67+여유 1), 무기/핫바/상호작용 시스템 전부 코드 완료. 남은 것: 에디터 오브젝트 배치(문/좌석/사다리) + Play 판정(키 조작 전반+수영+창 공격)**

---

## 2026-09-08: 실내 씬 전환 완성 + 내부 씬 미리보기 메뉴 ✅ (커밋 a218bf23)

- **원인 진단**: 전환 체인은 존재(BuildingTrigger→BuildingEvents→UI/IndoorSceneTransition→IndoorScene Additive+빌더)하나 **플레이어 이동 로직 부재**(Additive 로드만 하고 플레이어는 메인 좌표 고정 → 화면 변화 없음이 "전환 안 됨"의 실체)
- **보강**: EnterBuilding 시 진입 직전 위치 저장(_returnPosition) → 로드 완료 후 플레이어 (0, 0.1, 0) 텔레포트(INDOOR_FLOOR_Y=0 — IndoorBuilder.CreateRoom 바닥 y=0 확인) → ExitBuilding 시 원위 복귀
- **영지 트리거**: 기존 존재 확인(TerritoryBuilder 252~263행 — 성문 GateAnchor 108% 바깥, territoryKey 포함, 상점/크래프트하우스 트리거도 존재) — 무수정
- **내부 씬 단독 확인**: Assets/Editor/IndoorPreviewMenu.cs 신설 — Tools/Indoor/미리보기 13종(플레이어 성, 영주 성 5국가, 여관, 집, 헛간, 동굴, 상점, 교회, 크래프트하우스): 클릭 시 IndoorScene 단독 오픈+해당 빌더 즉시 생성(성은 SpawnInteriorFixtures 포함, 런타임과 동일)
- **검증**: 배치컴파일 error CS=0 + 텔레그램 알림(9483)

**Play 판정 대기**: ①성문 E → 내부 전환+플레이어 위치 ②Exit 트리거 → 원위 복귀 ③에디터 Tools/Indoor 미리보기 각 메뉴

---

## 2026-09-08: 실내 전환 3이슈 수정 + 핫바 중앙 하단 ✅ (커밋 614bf960)

- **유령 스크립트**: IndoorScene IndoorRoot에 무효 guid(0000…e000) MonoBehaviour → YAML 수술 제거(컴포넌트 엔트리+도큐먼트)
- **내부 안 보임 원인**: 플레이어 텔레포트가 FindWithTag("Player") 의존(태그 부재 시 이동 안 함) → PlayerMovement 폴백 검색 추가(진입/복귀 2곳)
- **MainCamera 경고**: Camera.main 폴백(FindFirstObjectByType<Camera>) 추가
- **핫바 중앙 하단**: 원인=pivot (0,0) 고정(패널 좌하단이 중앙) → pivot (0.5,0), margin 12px
- **트러블슈팅**: static class에서 FindAnyObjectByType 미해석(CS0103) → UnityEngine.Object. 정규화. `??`는 GameObject↔Transform 타입 불일치로 사용 불가 — if-대입형 필수
- **검증**: 배치컴파일 error CS=0 ×2 + 텔레그램 알림(9493)

**시스템 배치 필요(에디터 작업)**: DoorInteractable(문+피벗), SeatInteractable(의자/침대), LadderInteractable(사다리) 오브젝트 부착

**Play 판정 대기**: ①Ctrl 웅크림+4방향 ②호수/하천 수영 진입·부유 ③의자/침대 착석·일어남 ④문 개폐 ⑤채집 3종 랜덤 ⑥NPC 대화 Talk·승리 Victory

---

## 2026-09-08: Meshy 신규 8종 추가 + 로코 4종 전량 Meshy 교체 + 상태 3종 등록 ✅

**신규 FBX 8종**(유저 추가): Regular_Jump, Back_Jump, Idle_02, Collect_Object, Male_Bend_Over_Pick_Up, Pull_Radish, Slap_Reaction, Electrocution_Reaction → 동일 파이프라인(rerig_meshy skip-existing)으로 MeshyUser 68클립 완성

- **로코 4종 교체**: Idle=Idle_02 / Walk=Walking / Run=Running / Jump=Regular_Jump (구 믹사모 로코는 MixamoUser에 보존, GUID 9종 refs=1/1 검증)
- **상태 3종 신규 등록**: Harvest=Pull_Radish(약초 뽑기) / HitLight=Slap_Reaction(경직) / Stun=Electrocution_Reaction(스턴) — 트리거 파라미터+AnyState/ExitTo 완비
- **발화 연결 필요(다음 단계)**: Harvest→채집 UI 플로우(현재 채집은 병사 자동 임무 HerbGatheringMission+프로시저 GatherMotion) / HitLight·Stun→전투 데미지 등급 판정(경직=소데미지, 스턴=상태이상)
- **시스템 미구현으로 보류(클립만 등록됨)**: Swim(수영 판정+수면 상태머신) / Crouch·Crawl(잠입 모드) / Parry(방어 판정) / Bow(활 조준·발사) / Ride(탈것) / Carry(운반) / Climb_Stairs·Ladder(기동) / open_door(문 상호작용) / Talk·victory(연출) / mage_cast(마법) — 각각 별도 설계 Phase 필요
- **검증**: error CS=0 + 무해 경고만 존재(소멸한 구 믹사모 폴더 loop 보정 시도)
---

## 2026-09-09: 레벨/스탯 창(P키) 신규 구현 + 스텁 정리 ✅ (계획: .hermes/plans/2026-09-09_level-stats-window-plan.md)

- **진단**: 레벨 시스템은 기존 완성(PlayerStats Lv1~50, EXP획득원 7곳: 제작/퀘스트3종/사냥/채집). 갭=스탯 창 미연결(PlayerStatusWindow 죽은 코드, 키 소비처 부재, C키는 은신과 충돌)
- **신규**: StatusWindowUI.cs — HotbarUI식 셀프부트(RuntimeInitializeOnLoadMethod), 자체 StatusCanvas(sort 200), 좌상단 패널, legacy Text 전용(혼합금지), Lv/EXP게이지/HP/공방치명이속/제작보너스/중독/골드 표시, OnLevelChanged→즉시갱신+LEVEL UP 팝업(2초 페이드)
- **키**: _statusKey C→P (KeyBindings.cs+Settings asset). 은신C/크래프팅X/RevengeListK/통계U와 무충돌
- **QA 수정 1건**: 씬 전환 후 PlayerStats 인스턴스 교체 시 재구독 누락(팝업 소실) → Update()에서 EnsureLevelSubscription() 상시 보장
- **삭제**: PlayerStatsUI.cs·UIPlayerStats.cs(가짜 하드코딩 스탯)·PlayerStatusWindow.cs + Windows.meta — guid 잔존 0건
- **검증**: 배치컴파일 error CS=0(에디터 락 시절엔 ScriptAssemblies DLL strings grep으로 대체 판정 — 컴파일 성공시에만 DLL 재생성되는 성질 이용)
- **Play 판정 대기**: P토글 / 채집EXP / 레벨업 갱신+팝업 / 은신C 정상 / 한글 폰트

## 2026-09-09 6차: 실내 플레이어 이동 + 조명 근본해결 ✅ (커밋 953b6db0)
- IndoorCamera 중복 누적(GameObject.Find 비활성 미탐지) → 메뉴 멱등성 수정(비활성 포함 전수 정리+null 가드)
- 셸 배치 재저장 성공(45오브젝트) + CS=0

---

## 2026-09-10: Test_10 플레이어 비가시 근본수정 + 크래프팅/요리 폰트 판독성 수리 ✅

**이슈 1 — Test_10_TerritoryCombat 플레이어 미표시 (근본원인 3건, Editor.log 포렌식 확정)**
- **핵심 원인(비가시의 직접원인)**: PlayerPlaceholder.TryLoadGLBModel이 GLB PlayerModel 자식에 부착된 Rigidbody 제거 시도 → **ModelAnimatorAssigner가 추가한 ProceduralAnimationController의 `[RequireComponent(typeof(Rigidbody))]` 의존으로 DestroyImmediate 차단**("Can't remove Rigidbody because QuadrupedProceduralAnimation, ProceduralAnimationController depends on it" — Editor.log 6792행 실증) → 콜라이더는 이미 전부 제거된 상태 + 기본 useGravity=true rb → **비주얼 모델이 중력 낙하로 1~2초 내 땅 밖 소실** (가드/몬스터/영주는 루트 rb+콜라이더로 착지해 보임 → "플레이어만 안 보임" 증상 정합)
- **수정 A**: PlayerPlaceholder.cs — DestroyImmediate 시도 전 `rb.isKinematic=true / useGravity=false / detectCollisions=false` 선설정(제거 차단돼도 무해 관성 rb) (커밋 e42fabbf)
- **수정 B**: ModelAnimatorAssigner.RemoveAllAnimationComponents에 `QuadrupedProceduralAnimation` 잔존 제거 추가 — 기존 루틴은 `_quadrupedLocomotion`(QuadrupedProceduralLocomotion)만 제거, 다른 클래스 QuadrupedProceduralAnimation이 ForceBiped 후에도 생존 → rb 의존 유지의 원인 (커밋 e42fabbf)
- **수정 C**: TagManager.asset 커스텀 태그 결손 — 런타임 스캔으로 DraculaGuard/DraculaLord/Guard/Interactable 결손 확인 추가(Play마다 "Tag: X is not defined" 다수), QA 심층(CompareTag/FindWithTag)에서 **Enemy/Boss/Lord/Mount/Horse/Soldier/Workbench/CraftingStation 8종 추가 발견** 보강 — 미등록 시 CompareTag가 항상 false인 dead path (커밋 e42fabbf+후속)
- 참고: Unity 6000.4에서 set_tag 미정의 태그는 **에러 로그 후 계속**(예외 abort 아님) — SpawnLord/SpawnGuard가 태그 에러 후에도 정상 생성된 로그로 확인

**이슈 2 — CraftingUI 폰트 판독 불가 (1080p)**
- 원인: 레시피명(Height 26)/효과(20)/프리셋 라벨·버튼/푸터/재료슬롯명/X버튼(24px)이 **스타일 없는 기본 스킨 폰트(≈13px)** 로 렌더링. 대형 스타일(72/48/52/44/40)은 존재했으나 미적용 지점 다수
- **수정 D**: CraftingUI 신규 스타일 7종(preset 32/recipeName 36 Bold/effect 28/footer 28/slotItemName 28/smallButton 28) 적용 + 행높이 56/42/44/48/36, ★버튼 48², X버튼 36², 레시피 카드 Height 60→100(내용 98px 수용)
- **수정 D**: 윈도우 1500×1305 → **1080p 세로 오버플로(1305>1080)** → OnGUI에서 `effW=min(W, Screen.width-40) / effH=min(H, Screen.height-40)` 클램프, winRect/availableWidth/gridHeight/cols 전부 eff 기반(CraftingUI+CookingUI 동일 적용 — 요리창은 QA 단계에서 동일 패턴 발견 보완)
- **수정 E**: CookingUI 재료슬롯 라벨(27px)/아이템명(기본폰트) 동일 수리(_slotLabelStyle 32/_slotItemNameStyle 28)

**검증**: 배치컴파일 2회 — error CS=0 ×2 ("Exiting batchmode successfully", buildlog_fix_player_ui.txt/_final.txt) + QA 에이전트 5항목 PASS/FIX 완료
**Play 판정 대기**: ①Test_10 Play → 플레이어 모델(원점) 정상 표시 + 콘솔 태그 에러 0건 ②좌클릭 공격 → 영지/병사/몬스터 데미지 ③작업대 E → 크래프팅 창 텍스트 판독 가능 ④요리 창 동일 ⑤1080p에서 창 하단 버튼 잘림 0

---

## 2026-09-10 2차: Test_10 플레이어 지형 아래 소실 — 계약 미니지형으로 근본수정 ✅ (커밋 de7da135, Test_10 전용 단일 파일)

**증상**: Test_10에서만 플레이어가 지형 아래로 사라짐(메인씬은 정상). 요구: 메인씬 공유 코드 무변경, 테스트씬에서만 수정.

**근본원인(Editor.log 포렌식 확정)** — 메인씬 PlayerMovement(공유)는 "지형 표면 y = 1 + TerrainGenerator.GetHeightAt(x,z,Plains,42)" 계약으로 동작:
- ①Awake: PlayerSpawnConfig.SpawnPosition(728,0.24,-529)을 GetHeightAt+2로 스폰 오버라이드 → Test_10에서도 y=4.53 스폰(로그 실증) — TestTerritoryCombatSetup의 position=zero가 나중에 실행되어 이김(순서 실증)
- ②ClampToGroundByHeight: 매 프레임 feetY < formulaY-0.5면 formulaY+height/2+0.02 텔레포트(낙하 구제) — Test_10 평면 바닥(y=-0.5)은 formulaY(≈3.53)와 4m 어긋남 → **착지할 때마다 4.55 텔레포트 → 낙하 → 재텔레포트 무한 진동** = 플레이어가 바닥 아래로 사라졌다 튀어나옴 (CamProbe y=2.34 낙하 중 프레임 실증)
- ③BlobShadow도 GetHeightAt+1 추적 → 그림자 공중 부양

**수정 (TestTerritoryCombatSetup.cs 단일 파일 — 메인씬 영향 0)**:
- SurfaceY(x,z) 헬퍼: 1+GetHeightAt(x,z,Plains,42), try-catch 폴백 1f — PlayerMovement·BlobShadow가 기대하는 표면과 100% 동일 수식
- SetupGround 재작성: Plane 프리미티브(-0.5) 대신 **계약 준수 미니지형 메시**(±60m, 1m 간격, 121×121=14,641 정점, 28,800 삼각형, 정점 y=SurfaceY) — MeshCollider+기존 초록 URP 머터리얼. 메시 표면==formulaY → 텔레포트 트리거 구조적으로 불발
- 스폰 y 정합: Awake 최상단에서 _lordPos +1.6 / _guardPos +1.0 / _monsterPos +0.9 (XZ 존중, y만 표면 기반)
- SetupPlayer 마지막: position=(0, SurfaceY(0,0)+1.02, 0) — PlayerMovement.Awake의 (728,…) 오버라이드를 이기고, +1.02는 clamp 정착 목표와 동일해 개입 조건 불발

**검증**: 에디터 사용 중 락으로 배치컴파일 1회 실패 → 저사양 절차대로 에디터 강제종료+락파일 삭제 후 재실행 → **EXIT=0, error CS=0, "Exiting batchmode successfully"** (buildlog_test10_miniterrain.txt). QA 에이전트 4항목 전부 PASS(정점 수/인덱스 범위 14640≤14640/와인딩 +y/공유 파일 무변경/GetHeightAt 순수 수학 14,641호출 수ms 무해)

**Play 판정 대기**: Test_10 Play → 플레이어가 미니지형 위에 안정 착지, 바닥 아래 소실/텔레포트 진동 0, 좌클릭 공격 정상

---

## 2026-09-10 3차: Test_10 모델 침하+애니 부재 근본수정 + Free Slash/Impact VFX 연동 ✅ (계획: .hermes/plans/2026-09-10_094500-test10-player-sink-vfx-anim-plan.md)

**진단(영상 프레임+로그 교차)**: 루트 캡슐·그림자는 정상 착지(CamProbe y=1.61), **GLB 모델 자식만** 16초 정상→18초 소실. 원인=Test_10 코드생성 플레이어가 PlayerPlaceholder→ModelAnimatorAssigner.ForceBiped 경로로 레거시 Procedural/Neural 자동부착 → "Mapped 3 bones"+"heuristic fallback"이 루트본을 아래로 밀어 모델만 침하+애니 미작동(메인씬은 이 경로 이미 제거, Player_AC 단일 경로). 침하와 애니부재가 동일 원인.

**Phase 1 — Test_10 전용 애니 부트 (메인 영향 0)**: 신규 TestPlayerAnimatorBoot.cs(227줄) — 지연부트(PlayerModel 5초 대기+1프레임) → 레거시 6종 제거(ModelAnimatorAssigner/ProceduralAnimationController/NeuralAnimationController/QuadrupedProceduralAnimation/HybridAnimationController/ProceduralBoneMap — BoneMap은 RequireComponent 의존이라 마지막) → 루트 빈 Animator 제거(PlayerCombat RequireComponent 선례) → PlayerModel에 Player_AC+applyRootMotion=false+cullingMode=AlwaysAnimate(InventoryWindow 1004 선례) → rb 재관성화 → 루트 HumanoidClipDriver(Player 모드: CC.velocity→Speed, LastAttackTime→Attack, 롤/점프 엣지) → 침하 감시(모델중심−루트 y<−0.5m 경고). TestTerritoryCombatSetup 훅 3줄.

**Phase 2 — Free Slash VFX → 공격 연동(공유, 메인에도 자동 적용)**: 신규 SlashVFXRunner.cs(static 러너+숨은 호스트, HitVFX 선례) — PlayerCombat.TryAttack의 attack_swing SFX 직후 try-catch 훅, 카메라 정면 수평 방향+1.2m 전방에 "FX/Slash/Slash VFX" 스폰. 스윙/임팩트 각 0.08s 쿨다운 분리.

**Phase 3 — Matthew Guz Impact → 피격 연동(공유)**: CombatFXGate.PlayHitFXInternal의 SpawnHitSparks 직후 SlashVFXRunner.PlayImpact(position, type) 1줄 — Organic→BasicHit/Construct→BasicHit2, 기존 스파크·출혈·숫자·카메라 단계 전부 유지(예산 무변경).

**리소스 설치**: 신규 Assets/Editor/VFXResourceInstaller.cs(Tools/VFX 메뉴+-executeMethod 무인실행) — 프리팹 3종을 Assets/Resources/FX/{Slash,Impact}로 복사(멱등) + **Matthew Guz 빌트인 파티클 셰이더(43개 중 타격 프리팹 3종)→URP/Particles/Unlit 자동 변환**(원본 에셋 읽기 전용, 새 _URP.mat 에셋만 생성) — 마젠타 차단. Free Slash는 자체 ShaderGraph라 무변환. CS0023(CreateAsset void 반환) 1건 직접 수정.

**검증**: 배치컴파일+인스톨러 2회 — error CS=0 ×2, "Exiting batchmode successfully" ×2(buildlog_vfx_install2/_final), 인스톨러 로그: 복사 3/3+변환 3종/참조 7건+셰이더 수집(Slash 4종 ShaderGraph, Impact 2종 URP Particles Unlit). QA 에이전트 8항목 전부 PASS(레거시 6종 네임스페이스 실물확인, 코루틴 이터레이터 제약 우회, 원본 에셋 무변경, 멱등성).

**Play 판정 대기**: ①부트 로그 "✅ Player_AC 부착+레거시 제거 완료"+제거 목록 ②Idle/Walk/Attack 애니 재생(T포즈 아님) ③18초 경과 후 모델 유지(침하 0) ④좌클릭 → 슬래시 이펙트 ⑤타격 → BasicHit/BasicHit2 임팩트+기존 이펙트 공존 ⑥마젠타/InternalErrorShader 0건

---

## 2026-09-10 4차: 인벤토리/장비창 AAA 4레이어 재설계 ✅ (계획: .hermes/plans/2026-09-10_103000-aaa-inventory-4layer-plan.md)

**요구**: 다중레이어 UI — L1 백플레이트(다크스톤+9-Slice)/L2 슬롯 행렬(엠보싱+희귀도 테두리·글로우)/L3 콘텐츠(아이콘·호버·장착 이펙트 안착)/L4 금속골드 프레임+4모서리 장식+타이틀 배너.

**신규 InventoryArtLibrary.cs(598줄, static 캐시)** — 외부 에셋 의존 0 절차 아트: SDF 라운드+fBm 노이즈(고정시드 해시, Random 금지)로 8종 텍스처(Backplate 9-Slice 스톤+마법진 음각/MetalFrame 금속골드 3단 그라디언트/CornerOrnament 로터스 필리그리/TitleBanner 리벳 배너/SlotCell 엠보싱 인너섀도우/SlotGlow 방사형/SlotHighlight 링/DropShadow 가우시안). RarityColors 6종(Common회백/Uncommon녹/Rare청/Epic보라/Legendary골드/Unique시안).

**InventoryWindow(1794→1899행)** — OnGUI z-order 재구성: 드롭섀도우→스톤 백플레이트→배너+타이틀→탭→그리드(셀+희귀도 글로우 tint+아이콘/수량/내구도+호버/선택 하이라이트)→장비행→금속프레임+4모서리(GUI.matrix 0/90/180/270° 회전, 복원 보장). 구 UIStyleManager 배경/타이틀 이중 렌더 제거. **데이터 로직 diff 5줄** — 정렬/탭/선택/우클릭장착/드래그/퀵슬롯 100% 유지.

**EquipmentWindow(441→490행, 부모 직접 구현 — 에이전트 타임아웃 2회)** — 동일 4레이어: 장착 슬롯=골드 글로우(셀 뒤)+셀, 빈 슬롯=딤 셀, 호버=백색 링/선택=골드 링, 배너는 프레임 상단 걸침(bannerY=y-6), 모서리 88px 회전 배치. 클릭/해제/정보 로직 무변경, ArtLibrary 캐시 OnDestroy 파기 금지.

**검증**: 배치컴파일 EXIT=0, error CS=0(buildlog_aaa_ui.txt). QA 에이전트 5항목 PASS(API/결정론 노이즈/빌더 실질합성 8개 전수/이중렌더 제거/로직 무변경/OnGUI 텍스처 생성 0건). 에이전트 2회 타임아웃(600s)→A의 결과물 검수+EquipmentWindow 부모 직접 구현 전환.

**Play 판정 대기**: ①인벤토리(I) 4레이어 비주얼 — 스톤 백플레이트+골드 프레임+모서리 장식+배너 ②희귀도 글로우/테두리(전설=골드) ③호버/선택 링 ④장비창(E) 동일 세트+장착 글로우 ⑤배너 상단 걸침 클리핑 0 ⑥프레임이 슬롯 가리지 않음

---

## 2026-09-10 5차: Test_10 재판정+몬스터 머리 UI+크래프트 테스트씬 전 UI + C드라이브 용량 정리 ✅ (계획: .hermes/plans/2026-09-10_111500-test10-anim-rejudge-monsterui-crafttest-plan.md)

**진단(영상2+로그 재판정)**: 부트는 작동 중(레거시 5개 제거+Player_AC 부착) — 1차 세션 "avatar=NULL"=GLB 어바탓 지연 도착으로 T포즈. 피격은 CombatLog 39건+이펙트 실제 출력(슬래시 스폰 성공 로그 부재만 문제).

**Phase 1 — TestPlayerAnimatorBoot 보강(+110행)**: ①아바타 감시(5초 폴링, 도착 시 Rebind+재생 재시작 — T포즈 근본 차단) ②부트 3초 후 애니검증 로그(현재 클립명/playing) ③침하 감시 유지(0.435m 정상 실증). HumanoidClipDriver._anim은 컴포넌트 참조라 Rebind 후 유효.
**FX 발화 로그**: SlashVFXRunner.PlaySlash 성공 경로에 스폰 로그 1줄(좌클릭 발화 증거).

**Phase 2 — 신규 MonsterHeadUI.cs(329행)**: 몬스터 머리 위 이름+Lv+HP바(90×8px, ratio≥0.6녹/≥0.3노랑/빨강+수치) — AnimalAI.CurrentHP/MaxHP+MonsterLevelManager.GetLevelDisplay/EstimateTierByName 소비(MonsterTier 3값 매핑: 녹/노랑/주황). y반전·뒷면/화면밖/40m 스킵·Camera.main 0.5s 캐시·OnGUI GUIStyle 생성 0건. Test_10 SpawnMonster 훅 11행.

**Phase 3 — 크래프트 테스트씬(InteriorSystemsTestSetup +96행)**: ①테스트 아이템 시딩 13종(기존 정적 데이터 10종+전설★/희귀/영웅 3종 — 글로우 등급색 검증) ②핫바 1~3 자동 할당(목검/치유초/멧돼지고기) ③테스트 몬스터 slime(HP바 실시간 검증) ④OnGUI 키 가이드(I인벤/E장비·스테이션/P스탯/M지도/X크래프트/K복수). 핫바/스탯/전투로그는 기존 셀프부트, I/P/M/E/X/K는 KeyBindings 공유.

**검증**: 배치컴파일 CS=0(1차 CS0117 3건 — PlayerInventory.ItemRarity→ProjectName.Core.ItemRarity 정규화 후 통과, buildlog_monsterui_crafttest2.txt). QA 5/5 PASS(코루틴 제약/API 실존/로직 무변경/널가드).

**Phase 5 — C드라이브 용량 정리 (7.4→23.2GB, +15.8GB 확보)**: venv 4.95GB(리눅스 심링크 venv — Windows Zip 불가 확정→requirements.txt 재구성 가능, 학습 보류 중이라 바로 삭제)/code_temp_compile 2.47GB(7/18 이후 미수정 사본)/Library/Artifacts 7.96GB(에디터 재생성 캐시)/buildlog_cap.txt 616MB. **주의: 다음 에디터 실행 시 리임포트(수 분) 발생 — 정상.**

**Play 판정 대기**: ①부팅 로그 "✅ 리그 소스 교체" ②애니검증 clip=… playing=True ③T포즈 해소+이동/공격 애니 ④좌클릭 연타 → AttackCombo 애니 ⑤슬라임 타격 시 제자리 경직(비행/벽 관통 0) ⑥카메라 에러 0건 ⑦모델 크기 적정(침하감시 bounds 로그) — 과대/과소 시 bounds 정규화 추가

---

## 2026-09-10 7차: 콤보 애니 등록 검증 + 공격 중 인터럽트 방지(Idle 팝 차단) ✅

**사용자 보고**: "콤보 애니메이션이 등록 안 됐다" → 조사 결과 **등록은 완성** — Player_AC에 Attack/AttackCombo/AttackCombo2/AttackCombo3/AttackThrust/AttackBase 전부 AnyState 전이로 연결, 클립도 MeshyUser FBX 서브클립(Double/Triple_Combo_Attack 등) 정상 바인딩. 로그로 AttackCombo 실제 진입+클립 재생(human=True, len=4.33s) 실증.

**진짜 문제 — 공격 애니 즉시 튕김**: Attack* 상태 진입 후 normT 0.04~0.07(5~7%)에서 Idle 복귀. 원인=공격 중 이동 시 Speed 파라미터가 1~5로 유지되어 Walk/Run 조건 전이가 애니를 인터럽트.

**수정 (HumanoidClipDriver.cs, +30/-1)**:
- 공격 상태 감시: `GetCurrentAnimatorStateInfo(0).IsName("Attack"/...6종)` — 진입 중 Speed 파라미터를 0으로 고정 → Speed 조건 전이 불발(공격 애니 보호). ResolveStateName 미매핑 상태(AttackBase/Thrust/Combo2/3)까지 커버하도록 IsName 직접 비교(QA 지적 반영 — ResolveStateName은 6종 중 2종만 매핑).
- 콤보 홀드: 트리거 발화 시 `_attackHoldUntil = Time.time + 0.45s` (연타마다 연장, Max로 확장) — 홀드 중 Speed 0 고정으로 Idle 경유 팝 차단. 0.45s < 클립 4.3s라 클립을 자르지 않음(트리거는 큐잉 소비).
- 이동은 CharacterController 자체 처리라 홀드 중에도 실제 이동 가능 — 발 미끄러짐(cosmetic)만 존재.
- CS0414 신규 경고 0건(_attackHoldTimer 데드코드 삭제).

**검증**: 배치컴파일 CS=0 ×1(buildlog_combo_hold2.txt). QA 6항목 — Speed 고정 목적 달성/자연 복귀/0.45s 홀드 안전/부작용 무해 확인 + **결함 2건 발견**: ①ResolveStateName이 AttackBase/Thrust/Combo2/3 미매핑(감시 블록이 죽은 조건) → IsName 직접 비교로 교체 ②_attackHoldTimer 데드코드 → 실제 연장 로직(+=Max)으로 교체. 재컴파일 CS=0, 경고 신규 0건.

**Play 판정 대기**: ①좌클릭 연타 4타 → Attack→AttackCombo→AttackCombo2→AttackCombo3 이어짐(Idle 경유 팝 0) ②공격 중 이동 → 공격 애니 유지 후 자연 복귀 ③창 장착 연타(AttackThrust) 연속 발화 ④단발 공격 4.3s 클립 완주 후 Idle 복귀 ⑤CS0414 신규 경고 0건

---

## 2026-09-10 8차: Test_10 영지 가시성+병사 GLB+하트 HUD 이식+콤보 RunToWalk 인터럽트 근본원인 ✅ (커밋 445399e7/1e37a220)

**사용자 보고**: 영지 안 보임/체력바(HUD) 없음/농경 확인 불가/병사가 GLB가 아님/콤보 애니 여전.

**진단(로그)**: ①영지 성 큐브(z=±25)는 정상 배치 로그 실증 — 카메라가 플레이어(원점) 추적이라 성이 화면 밖/너무 멀어 미가시 ②하트 HUD는 Test_09(TestAllInOneSetup) 전용으로만 부착되고 Test_10에는 훅이 없었음 ③병사 CreateGuard는 캡슐 프리미티브 시각 — GLB(Soldier_Lv*.glb, Humanoid) 미부착 ④콤보: 내 Speed 홀드 이후에도 AttackCombo가 normT 4~6%에서 튕김 → **RunToWalk 트리거(AnyState)가 Attack 애니 중에도 발화되어 Run_to_Walk_Transition으로 인터럽트**하는 것을 근본원인 확정(_smoothedSpeed 하향 통과는 내 Speed 0 고정과 무관하게 발생).

**수정 (TestTerritoryCombatSetup +64행, HumanoidClipDriver +4/-1)**:
- 병사 GLB 부착: CreateGuard에 레벨 분기(level≥40→soldier_lv40/≥20→soldier_lv20/else soldier_lv1 — RuntimeModelLoader alias), GLB 부착+캡슐 Destroy+자식 콜라이더 전부 제거(루트 Box 유지=레이캐스트)+ModelAnimatorAssigner 제거(레거시 예방)
- 하트 HUD 이식: EnsurePlayerHUD(Test_09 선례 — 리플렉션 ProjectName.UI.HUD, 중복 방지, try-catch 격리)
- 영지 가시성: 위치 ±25→±32m(플레이어-성 간격 확대) + 성 10×8×10→**14×12×14 대형화**(원점 카메라에서 확실히 보이게)
- 콤보: RunToWalk 트리거 발화에 `Time.time >= _attackHoldUntil` 게이트 — 공격 홀드 중 Run_to_Walk 인터럽트 억제(normT 5% 팝 근본 차단)

**검증**: 배치컴파일 CS=0 ×2(중간 CS0246 1건 — Type→System.Type 정규화 후 통과, buildlog_territory_guard2/_vis). 중간 이슈: 유니티 프로세스 2개 락 → 확립 절차로 강제종료 후 재실행. braces 106/106 균형.

**Play 판정 대기**: ①원점에서 앞(+z)에 파란 성(14×12×14)·뒤(-z)에 빨간 성 가시성 ②병사 6명 GLB 모델 표시(캡슐 아님) ③하트 HUD(하트 아이콘+숫자HP) 표시, 피격 시 감소 ④연타 콤보 normT 튕김 0(AttackCombo → Idle 로그가 0.9 이후에만) ⑤농경/전리품/하트 시각 검증

---

## 2026-09-10 6차: Test_10 T포즈 근본해결(리그 교체)+슬라임 비행 차단+카메라 순서 — 콤보/피격 정상화 ✅

**증상(테스트 영상3+콘솔)**: ①플레이어 T포즈 지속(애니 부재) ②콤보/피격 이상 ③슬라임이 날아가 파란 지형(스카이박스) 위로 이동 ④"씬에 카메라가 없습니다" 에러 ⑤MonsterAggroSystem 씬 정리 경고.

**근본원인 3건 확정(Editor.log+영상 몽타주 교차)**:
1. **T포즈 = 리그 불일치**: RuntimeModelLoader가 Player_Rigged.glb를 **RiggedMonster(non-humanoid)** 로 판정("지연 로드: 'Player_Rigged' (RiggedMonster)" 로그) → GLB 인스턴스의 Animator.avatar가 null(휴머노이드 아님) → **Player_AC(휴머노이드 전용) 재생 불가**. 아바타 감시가 5초 대기했지만 어차피 안 도착함("avatar 미도착" 로그 실증). 반면 Player_Rigged_Heat.fbx는 Humanoid 임포트(animationType:3)로 imported avatar 보유 — InventoryWindow 프리뷰에서 정상 재생 실증.
2. **슬라임 비행 = 3중 중첩**: ①GLB 프리팹에 절차애니 컴포넌트가 rb 자동부착(비관성) ②HitReaction 넉백(AddForce Impulse y+0.5) ③HighSpec 키네마틱 절트(Transform 직접 쓰기 — 물리 Freeze 무시, y+0.4 홉+거리 2배) → 타격 때마다 몬스터가 공중으로 유령 변위, 파란 스카이박스 영역으로 이동. 데미지 숫자는 데미지 텍스트가 IMGUI world→screen 변환 미탑재(기존 CombatVFX 파티클만) — 신규 이슈 아님.
3. **카메라 에러**: TestTerritoryCombatSetup.Awake에서 SetupPlayer(→PlayerMovement.Awake)가 SetupCamera보다 먼저 실행.

**수정**:
- **리그 교체(TestPlayerAnimatorBoot)**: 부트 초기에 GLB PlayerModel을 **Player_Rigged_Heat.fbx(Humanoid, avatar 보유 — InventoryWindow 프리뷰 경로 실증)** 로 교체, 이름 "PlayerModel" 계승+oldModel Destroy. 이후 기존 전 로직(레거시 제거/Player_AC 부착/드라이버/감시) 그대로 통과 — T포즈 재발 경로 차단. FBX 없으면 GLB 경로 폴백.
- **슬라임 비행 차단**: SpawnMonster에서 rb 관성화(FreezePositionX/Z+FreezeRotation — y만 접지, 수평은 AnimalAI Transform 제어와 무충돌) + 신규 HitReaction.DisableKnockback()(넉백/절트 오프, HitFlash+경직 유지). QA에서 **치명 순서 버그 발견·패치** — 기존엔 GetComponent<HitReaction>이 AddComponent보다 앞서 null→스킵→신규 AddComponent로 넉백 활성 유지되었음 → AddComponent 후에 확정 후 DisableKnockback 호출로 교정.
- **카메라 순서**: SetupCamera를 SetupPlayer 앞으로 — "카메라 없음! 생성" 에러 방지.
- MonsterAggroSystem 경고: DontDestroyOnLoad 미사용 런타임 자동생성 GO — 무해(씬 정리 시 사라짐), 코드 유지.

**검증**: 배치컴파일 CS=0 ×2(수정 전후, buildlog_fix3_combo_flight/_final). QA 3항목 PASS+**치명 순서 버그 1건 발견·직접 패치**(DisableKnockback 호출이 AddComponent 뒤에 오도록). Player_AC 트리거(Attack/AttackCombo/Hit/Death)+콤보 로직(HumanoidClipDriver 361-364행) 정상 확인 — T포즈만 해소되면 콤보/피격은 기존 파이프라인 그대로 발화.

**Play 판정 대기**: ①부팅 로그 "✅ 리그 소스 교체" ②애니검증 clip=… playing=True ③T포즈 해소+이동/공격 애니 ④좌클릭 연타 → AttackCombo 애니 ⑤슬라임 타격 시 제자리 경직(비행/벽 관통 0) ⑥카메라 에러 0건 ⑦모델 크기 적정(침하감시 bounds 로그) — 과대/과소 시 bounds 정규화 추가

---

## 2026-09-11 34차: Tab 키 병사 부대 핫바 (RTS 제어그룹) ✅ (커밋 898e2b64)

**요구**: 33차에서 활성화한 병사 선택/명령(RTS)을 빠르게 써먹기 위해, 하단 아이템 핫바 자리에 Tab 키로 전환되는 "병사 부대 핫바"를 추가. 숫자 키로 부대(제어그룹) 전체를 RTS 선택.

**구현**:
- **신규** `Assets/Scripts/UI/GuardSquadHotbar.cs` — 셀프 부트스트랩 싱글턴 (HotbarUI 패턴 동일, 코드 생성 uGUI, 에셋 의존성 0). 하단 중앙 동일 자리(866×150) 공유.
  - **Tab 키** → 아이템 핫바 ↔ 부대 핫바 토글. 부대 모드 진입 시 `HotbarUI.SetVisible(false)`로 아이템 GO 비활성(숨김 + 1~8 숫자키 동시 차단). 루트 GO는 상시 활성(Tab 리스닝 유지), 패널만 토글.
  - **Ctrl+1~8** → 현재 선택(박스 드래그)된 병사 그룹을 해당 슬롯에 등록(덮어쓰기), 즉시 아바타 표시.
  - **1~8 (부대 모드)** → 슬롯의 생존 병사들만 `GuardSelectionManager.SelectGroup()`으로 RTS 선택 → 파란 원 표시 + 우클릭 공격/이동 명령(기존 RTSCommandSystem 경로) 그대로 동작.
  - **아바타**: 국적색 절차 생성 원형 스프라이트 + 이름 이니셜 + `Lv{N}`. 사망 시 원/글자 회색 처리(0.5초 폴링), 인원은 우상단 `x{생존수}`. 씬 재진입 시 파괴 참조 null 무시.
  - 국적색: 동=빨강 / 서=파랑 / 남=초록 / 북=보라 / 황제국·기타=회색.
- **수정** `HotbarUI.cs`: `SetVisible(bool)`/`IsVisible` static 추가 (기존 아이템 슬롯 로직 무변경). `GetSlotIndexAtScreenPoint`에 숨김 상태 가드 1줄(인벤→숨겨진 핫바 드롭 오등록 방지, 표시 중엔 기존 동작 100% 동일).
- **수정** `GuardSelectionManager.cs`: `SelectGroup(IReadOnlyList<GuardPlaceholder>)` public 추가 (ClearSelection+AddToSelection 반복, 기존 드래그/우클릭 로직 무변경).

**검증**: Unity 6000.4.10f1 배치모드 컴파일 → 종료 코드 0, `error CS` 0개 (로그 `C:/Unity/compile_log_squad.txt`). ProjectName.UI/Systems/Assembly-CSharp 전부 재컴파일. 신규 경고 0. 파일 3개 저장 확인 (GuardSquadHotbar.cs 신규 27,996B / HotbarUI.cs 29,812B / GuardSelectionManager.cs 11,805B).

**Play 판정 대기**: ①Tab → 부대 핫바 전환(아이템 핫바 사라짐) ②박스 드래그 병사 선택 → Ctrl+1~8 등록 후 아바타 표시 ③부대 모드 숫자 1~8 → 해당 병사들 파란 원 선택 ④선택 후 우클릭 → 병사들이 이동/공격 명령 수행 ⑤다시 Tab → 아이템 핫바 복귀 ⑥병사 사망 시 아바타 회색화

---

## 2026-09-11 35차: 병사 실제 3D 외형 아이콘 — GuardIconRenderer 오프스크린 베이크 ✅ (커밋 37bdb344)

**요구**: 병사 부대 핫바(34차) 슬롯 아바타를 "국적색 원형+이니셜" 절차 아바타 대신 **실제 3D 캐릭터 외형 아이콘**으로. 병사는 GLB가 아니라 런타임 생성 캡슐이라, 살아있는 GuardPlaceholder를 잠깐 원격으로 옮겨 오프스크린 카메라로 촬영(베이크) 후 즉시 원복하는 방식을 도입.

**구현**:
- **신규** `Assets/Scripts/UI/GuardIconRenderer.cs` — GblItemIconRenderer(아이템 GLB)의 2단계 베이크 패턴 복제, 입력은 GuardPlaceholder 참조.
  - `GetOrCreateIcon(GuardPlaceholder)` static — 캐시 히트 즉시 반환, 미베이크 시 큐 등록 후 null(핫바 0.5초 폴링이 자동 재호출).
  - 캐시 키: `guard_{nation}_{level}_{guardName}` — "같은 국적+레벨+이름이면 같은 외형"(캡슐+국적색+레벨 기반 장비 결정). GetInstanceID는 씬마다 새로 발급되어 무한 캐시 → 미사용.
  - 마운트(1단계): 살아있는 guard를 `guard.enabled=false`(AI 정지)+원격좌표(10500,1000,10500, Gbl과 다른 자리)로 이동 → 전용 Camera(targetTexture=RT 128²) + 프레이밍(bodyRends bounds) + cam.Render(). 선택 링(SelectionOutline_)은 아이콘 오염 방지로 촬영 중만 숨김.
  - 베이크(2단계): 다음 프레임 `BakeFromRT`(ReadPixels→Texture2D, wrapMode Clamp, HideFlags HideAndDontSave) 후 **반드시 원위치 복원**(position/rotation/enabled/선택링, teleported 플래그 멱등). 모든 경로 try/catch — 크래시 금지.
  - 견고성: 사망/비활성 병사 큐 등록 금지, 실패 누적(MaxFailCount=3)→음성 캐시(무한 재촬영 차단), 구조적 실패(렌더러 부재) 즉시 음성, 캐시 상한 200, 큐 중복 방지, OnDestroy CleanupQueue 안전망.
- **수정** `Assets/Scripts/UI/GuardSquadHotbar.cs` — `RefreshSlotVisual`이 대표 생존 병사에 대해 `GuardIconRenderer.GetOrCreateIcon(lead)` 시도, null 아니면 슬롯 Image에 실제 아이콘 Sprite 표시 → 성공 시 국적색 틴트 제거(Color.white), 이니셜 숨김. null이면 **기존 절차 아바타(국적색 원형+이니셜+Lv) 폴백**. 사망/미등록 전환 시 아이콘→절차 원형 복원, 사망 회색, x{생존수} 유지. `_slotIconSprites[i]`(Sprite 캐시, 갱신마다 Create 방지) + `_slotIconTextures[i]`(원본 소유·파괴 안 함) 도입.

**검증**: Unity 6000.4.10f1 배치모드 컴파일 → 종료 0, `error CS` 0개 (로그 `C:/Unity/compile_log_guardicon.txt`). GblItemIconRenderer/HotbarUI/GuardSelectionManager 등 기존 파일 무수정, 기존 핫바 기능(탭 토글·등록·선택·사망처리) 유지.

**Play 판정 대기**: ①부대 모드 첫 진입 시 잠깐 절차 아바타 → 0.5초내 병사 실제 외형 아이콘으로 전환 ②병사 선택 링이 아이콘에 안 잡힘(순수 캡슐+장비 실루엣) ③촬영 직후 병사가 원위치 복원(화면에서 튈림 없음) ④병사 사망 시 아이콘→회색 절차 아바타 전환 ⑤부대 모드→아이템 모드 Tab 복귀 정상 ⑥여러 국적/레벨 병사 슬롯 각각 다른 외형 아이콘

---

## 2026-09-11 36차: 영지 NPC → 병사 Humanoid FBX 골격 교체 (방법 A) ✅ (커밋 2c5d9990)

**요구**: NPC도 병사처럼 사람 사지 애니메이션(Idle/걷기)을. 몬스터는 미적용(NPC만).

**Phase 1 진단 (실제 프리팹 로드 기반, 에디터 도구 `Assets/Editor/MonsterRigScanTool.cs` + `TestOutput/monster_rig_scan.txt`)**:
- **중대 발견**: 몬스터 22종 + NPC GLB **전부 `animator=NULL, avatar=NULL, animIsHuman=False, clips=0`** — Humanoid avatar 하나도 없음. 병사 Humanoid FBX(`soldier_lv1-20_rigged.fbx`)만 `avatar(valid=True, human=True)` 보유(캘리브레이션 실증). 즉 GLB 리깅은 뼈가 사람형이어도 Player_AC/Soldier_AC(Humanoid 전용)를 못 돌림.
- 휴머노이드 골격 시그니처(HumanoidSkeleton: spine+팔3+다리3) 4종 판별: **stone_golem/wild_troll/banshee/shadow_assassin**. 나머지 18종은 4족/넘버링. NPC 중 npc_lord_glb도 HumanoidSkeleton 골격이지만 avatar NULL.
- fbx 폴더: Player_Rigged/Player_Rigged_Heat/soldier_lv1-20·20-40·40-50 5개뿐 — **NPC용 Humanoid FBX 없음**.

**구현 (단일 파일 `Assets/Scripts/UI/TerritoryNPCSpawner.cs`, 127줄 추가)**:
- `SpawnNPC` GLB 장착 분기(110~145)를 `TryAttachSoldierHumanoidBody(npcGO, npcName, npcKey)` try/catch 호출로 교체. 성공 시 병사형 FBX 골격 반환, 실패/예외 시 잔재 정리(`{npcName}_Body`, HumanoidClipDriver Destroy) 후 기존 GLB+`ModelAnimatorAssigner.ForceBiped(true)` 폴백(회귀 0, 크래시 금지).
- `TryAttachSoldierHumanoidBody` (병사 검증 경로 TestTerritoryCombatSetup 674~730 복제): ①`Resources.Load("Models/UserProvided/fbx/soldier_lv1-20_rigged")` ②인스턴스 `{npcName}_Body` 부착(zero/identity/one) ③FBX 하위 Collider 전체 DestroyImmediate ④레거시 프로시저럴 계열 정리(StripLegacyAnimation: ModelAnimatorAssigner/Procedural/Neural/Hybrid/ProceduralBoneMap 마지막) ⑤Animator+SoldierShield_AC(applyRootMotion=false, AlwaysAnimate) ⑥avatar 유효성 로그(isValid/isHuman) ⑦npcGO 루트에 HumanoidClipDriver(mode=Soldier — 걷기/대기만, 공격 없음) ⑧`CopyMaterialsFromGlb(fbxBody, "Models/UserProvided/"+glbAliasKey)`로 NPC GLB 재질 이식 → **외형은 NPC 유지 + 사람 사지 애니**.
- GroundModelToY 미적용(NPC y=position 고정 계약 유지). glbResourcePath는 RuntimeModelLoader 소문자 alias 키 그대로 사용(대소문자 불일치 리스크 최소).

**검증**: Unity 6000.4.10f1 배치모드 컴파일 → 종료 0, `error CS` 0개 (로그 `C:/Unity/compile_log_npc.txt`). 몬스터/병사/플레이어 등 타 파일 무수정.

**Play 판정 대기**: ①영지 진입 시 NPC가 병사형 사람 골격으로 Idle/걷기(외형/재질은 기존 NPC 그대로) ②NPC 재질 이식 확인(흰색 미표시) ③대화(Interact) 정상 ④실패 시 로그 "Humanoid FBX 교체 실패 — 기존 GLB 경로로 폴백" 없이 통과 ⑤기존 ForceBiped NPC와 충돌 없음
