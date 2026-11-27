# RESTAURANT_DRUG_ECONOMY_PLAN.md — 밀매 폐기 → 음식점 마약 경영 시스템

작성: 2026-10-15 (79차 후속 — 기존 시스템 실측 기반, **구현 전 / 사용자 승인 대기**)
요구(사용자 확정): ①밀매 폐기 ②요리·마약 생산=플레이어, 판매=병사 ③발각 시 병사 즉시 처형 ④마약은 요리크래프트에서 음식+마약 조합(신규 레시피 DB 없음 — 재료에 마약 추가) ⑤희귀 마약일수록 중독 상승 ⑥주민 중독도=마을 단위 공유 ⑦가격=중독도 따라 권장골드 제안 + 플레이어 적정가격 등록 ⑧음식점 건설=나무+돌 조합법 + 영주 허가세 ⑨중독→마을경제 타격→영주 세금 타격→영주 폭력화·호감도 감소·주민 이탈(영주 성격 따름)

---

## 0. 실측 확정 사항

**폐기 대상 (밀매)**
- `SmuggleSystem.cs` (40행 — EstimateBaseValue/CanSmuggle/SmuggleSkill 게이트) 소비처: `WanderingMerchant.cs`, `SmuggleWindowUTK.cs`, `InteractionPanelUTK.cs`(탭 훅), `PlayerStats.cs`(SmuggleSkill/SmuggleGainMultiplier 필드 — 사용처 SmuggleSystem뿐 → 필드 퇴역)
- `TerritoryDrugSystem.cs` (127행): 영지 오염도(밀매/선물로 축적)→배치병사 중독 누적 — **밀매 유입원 폐기 시 오염 유입 없음**. 선물(Envoy) 경로 존재 여부는 구현 단계 실측. → 마을 단위 신규 시스템으로 대체, 제거 시 소비처(TerritoryManager.Update, GuardAddictionSystem 연계) 수리 포함
- `SmuggleWindowUTK` — UI 폐기 (Figma 정합 UI는 신규 음식점 창에서 대체)

**재사용 대상 (이미 존재 — 신규 작성 최소화)**
- `DrugDatabase` — 마약 10종(stage 1~10) + `AddictionLevel`(Low~Fatal 6단계) + Ingredients(약초 조합식) + GetByStage/GetByName
- 마약 생산 = 기존 연금술 `CraftingHelper.CraftAlchemy(herb1, herb2)` — 약초 조합 → 마약 (플레이어 몫 이미 구현 — 유지)
- 요리 = `CookingDatabase.GetCooking(meat, herb)` + `CookingUI` — 기존 요리크래프트에 **마약 재료 슬롯만 확장**
- 건설 = `ConstructionManager` + `BlueprintData` + `TownBuilder` — 음식점 블루프린트 추가 (조합법 나무+돌)
- 마을 주민 = `VillageNpcSpawner`(NPCAmbientDialogue GameObject) — 마을 80곳(T3 완성) 위에 얹음
- 영주 성격 = `LordPersonality` 7종 / 호감도 = 영주 호감도 필드(기존) / 세금 = `AITerritoryEconomySystem` 주간 세금
- 병사 중독 = `GuardAddictionSystem`(215행) — 병사 상태 존재, 음식점 배치 병사와 연계 판단은 구현 단계 실측

**확정 설계 (사용자 스펙)**
- 판매 주체 = 배치 병사. 요리/마약 흡수(뿌리기) 주체 = 플레이어
- 마약 음식 = 요리크래프트에서 음식+마약 조합 → "마약 뿌려진 {음식명}" (신규 레시피 DB 불요 — 마약 재료 1개 추가 흐름)
- 중독 상승량 = 마약 희귀도(stage/ AddictionLevel) 비례 — 상수표 신설
- 주민 중독도 = 마을 단위 1값 (개개인 동일) — `VillageDrugState`
- 발각 = 마약 음식 판매 시마다 판정, 확률 = 영주 호감도 반비례 → 발각 시 병사 **즉시 처형** + 영지 적대 전환 확률 별도(기본 40% + 중독등급 가산 — 조정 가능 상수)
- 가격 = 권장골드 제안(중독도·마약등급 기반) 표시 + 플레이어가 판매가 등록

---

## 1. 시스템 설계

### 1-1. 데이터 — VillageDrugState (마을 단위)
`Assets/Scripts/Core/Data/VillageDrugState.cs` (신규, TerritoryDatabase 캐시와 동일 static 패턴):
- `restaurantBuilt: bool`, `assignedGuardId: string`(빈문자열=미배치), `addiction: float`(0~100), `registeredPrice: int`(플레이어 등록 판매가), `stock: List<FoodStock>`(음식 id/수량/마약여부/마약 stage)
- 마을 키 = VillagePlacementSystem 마을 식별(nation+index) — 결정론
- 이탈 주민 수 `departedVillagers: int` + 기록

### 1-2. 흐름 (플레이어 → 병사 → 마을)
1. **건설**: 마을 근처(마을 중심 반경 내)에서 `ConstructionManager`로 음식점 건설 — 블루프린트(나무+돌 조합법, 기존 BlueprintData 패턴) + **허가세** = 영주에게 즉시 골드 이전 (금액: 링 배율 상수 — 세금 시스템과 동일 톤)
2. **병사 배치**: 음식점에 소유 병사 1명 배치 (판매 담당 — 배치 후 병사는 음식점 근처 대기 애니/상태)
3. **요리(플레이어)**: 기존 요리크래프트(CookingUI) — 기존: 고기+약초 → 음식. **확장: 재료에 마약 항목을 추가하면 → "마약 뿌려진 {음식}"** (마약 1개 소모, 음식 1개 생산, 신규 레시피 DB 없음 — 재료 목록에 마약 카테고리만 추가)
4. **재고 등록**: 플레이어가 음식점 인벤에 음식(일반/마약) 입고
5. **판매(병사)**: 판매 틱(인게임 주기 — 기본 1일 1회 기존 NPCDailyCycle 사이클 연계) — 주민이 구매:
   - 일반 음식 = 기본가 골드 획득 (부작용 없음)
   - 마약 음식 = 등록가 골드 + 마을 중독도 상승(마약 희귀도 비례) + **발각 판정**
6. **권장가 제안**: 음식점 UI에 권장 골드 표시 = 기본가 × (1 + 중독도 계수 + 마약 등급 계수) — 플레이어가 등록가 입력(권장 초과 시 주민 구매율 하락, 과소 시 이익 감소 — 단순 수요 곡선)

### 1-3. 리스크 연쇄
- **발각**: 마약 음식 판매 1건마다 `탐지확률 = base(0.15) + (1 − 호감도/100) × 0.65 + 중독등급 가산` → 발각 시: ①배치 병사 즉시 처형(사망 처리 — 기존 병사 사망 파이프라인 재사용) ②영지 적대 전환 확률 판정(기본 40% + 마약 등급 가산) — 적대 전환 시 해당 영지 병사/영주 적대 상태(기존 영지 적대 메커니즘 연계 실측)
- **중독 → 경제**: 마을 중독도↑ → 주민 구매력/생산성 감소 → 마을 경제 지수 감소(기존 마을/세금 경제 수식 연계)
- **세금 타격**: 주간 세금 정산(AITerritoryEconomySystem)에 마을 경제 지수 반영 — 중독 마을은 세금 감소
- **영주 폭력화**: 세금 수취 미달(기준 미달 주간 지속) → 영주 `폭력적` 플래그(또는 Cruel 성향 강화 — 실측 후 최소 변경) + 호감도 추가 감소
- **주민 이탈**: 이탈 확률 = 중독도·영주 폭력화·영주 성격(LordPersonality) 가중 — 이탈 시 마을 주민 NPC 실제 제거(VillageNpcSpawner 제거 API 신설) + departedVillagers 누적. 주민 소진 시 마을 기능(판매 수요) 정지

### 1-4. UI
- 음식점 창(신규 UTK — SmuggleWindowUTK 대체): 재고/판매가 등록/권장가 제안/중독도 게이지/병사 배치 상태 — Figma 정합은 구현 단계 재실측(마을 마이크로 창 패턴 준용)
- 상호작용: 마을 음식점 건물 E키 → 창 오픈 (기존 상호작용 패턴)

### 1-5. 세이브
- VillageDrugState(마을별) → SaveData/SaveManager 추가 (기존 마약/세이브 패턴 준용)

---

## 2. Phase D — 작업 분해 (위임 단위)

- **D1**: 밀매 제거 + TerritoryDrugSystem 정리/대체 (SmuggleSystem/SmuggleWindowUTK/InteractionPanelUTK 훅/WanderingMerchant/PlayerStats 퇴역 필드 — 소비처 전수 실측 후 안전 제거) + 회귀 테스트
- **D2**: VillageDrugState + 중독 수학(순수) + 권장가/등록가 + 발각 확률 수학 (순수 클래스 + EditMode TDD)
- **D3**: 요리크래프트 마약 확장 (CookingUI 재료에 마약 슬롯 — 마약 뿌려진 음식 생성) + 음식점 재고/판매 틱 + 병사 배치·즉시 처형 + 영지 적대 확률
- **D4**: 건설(블루프린트 나무+돌 + 허가세) + 마을 경제/세금 타격 + 영주 폭력화 + 주민 이탈(NPC 제거)
- **D5**: 음식점 UI(UTK) + 세이브 + Play 종합 (발각→처형→적대 / 중독→세금→폭력화→이탈 시연)

⚠ 모든 Phase: 구현 서브에이전트(파일 분리) → 독립 QA(컴파일/EditMode) → Play는 별도 승인 게이트. 기존 규약(Play/커밋 승인) 유지.

## 3. 전제/순서
- 직전 T1~T3(영지 산포·흙길·마을 80)는 EditMode QA 통과 — **커밋 승인 + Play 확인이 선행 권장** (마을 위에 얹는 시스템이라 기준선 커밋 후 진행 안전). 사용자가 바로 D 진행을 원하면 T1~T3 커밋만 먼저 승인받고 D 착수.
- 기본값(사용자 미지정 — 조정 가능): 마을당 음식점 1개/배치 병사 1명, 판매 틱=게임 1일, 발각 base 0.15+(1−호감도)×0.65, 적대 전환 40%+등급 가산, 허가세=링 배율 고정.
