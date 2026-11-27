# 162. 약초·약물 전종 시각 구분 + 조합 아이템화 정비 계획

> **For Hermes:** 구현 시 subagent-driven-development 스킬로 Phase별 서브에이전트 위임.
> 정책: docs/AGENT_DELEGATION_POLICY.md — 구현/QA 서브에이전트 분리 위임.
> Play·커밋은 별도 승인 게이트.
>
> **승인 기록 (2026-11-21, 텔레그램):**
> - 병 모양 = 단일(potion-bottle 1종 재사용) 승인 ✅
> - 조합 물약 아이템화(id 정비 포함) 진행 승인 ✅
> - 본 계획에 따른 구현·EditMode QA 진행 승인 (Play/커밋은 별도 게이트)

**Goal:** 약초 40종 + 마약 10종 + 조합 물약 82종 + 표준 버프 물약 5종(총 137종)에 개별 GLB를 파생 베이크해 인벤 아이콘이 전종 구분되게 하고, 조합 아이템화의 id 체계를 결정론으로 정비해 GLB와 자동 정합시킨다.

**Architecture:** 원본 GLB(potion-bottle 1개 + 약초 모양 4개) 재사용, 정점컬러 hue/명도/채도 재매핑(텍스처 무수정 계약 유지)으로 파생 GLB 대량 베이크(Python 도구). 조합 결과 아이템 id는 3곳에 흩어진 임의 체계를 `combo_{재료1id}_{재료2id}` 단일 규칙으로 통일. 아이콘 해석은 관례후보1(id 그대로) 자동적용.

**Tech Stack:** Python stdlib(GLB 바이너리 glTF 파싱 + 정점컬러 재매핑), C#(HerbComboDatabase + 3개 UI/Helper 호출부), Unity EditMode(NUnit).

---

## 0. 검증된 현재 상태 (2026-11-21 실측 — 계획의 근거)

| 항목 | 실측 |
|---|---|
| 약초 40종 | `Assets/Data/Herbs/{A1..P10}.asset` — Id: A1, Attribute: 0~3, Index: 1~10 (A1.asset 실측). 인벤 재료 id도 A1~P10(CraftingHelper.CreateHerbItem: id = herb.id) |
| 마약 id | `drug_{stage:D2}` = drug_01~drug_10 — `Assets/Scripts/Core/DrugEffectSystem.cs:163` 실측, `Core/Utils/CookingDrugHelper.cs:40` 동일 체계 |
| 조합 물약 82종 | `Assets/Resources/GAME_DATA.md` §2.1~2.4 — 표기 80종, 실제 82행(2.2 정신성 22행, 데이터 불일치) |
| **조합 아이템화 id — 3곳 3체계 (버그)** | ① `UI/CraftingUI.cs:1009` `id = $"combo_{combo.resultName}"` (한글+공백, 예: combo_독성 가시액) ② `Core/CraftingHelper.cs:~324` 동일 resultName 기반 ③ `UI/AlchemyUI.cs:443` `id = $"combo_{result.resultId}"` — resultId 자체가 `combo_{A1}_{P2}`라 **이중 접두사 combo_combo_A1_P2** |
| HerbComboDatabase.resultId | `HerbComboDatabase.cs:117` placeholder `combo_{info1.id}_{info2.id}` — 결정론적 체계의 씨앗, resultName 기반 아이템과 미연결 |
| 마약 데이터 | `DrugDatabase` (§2.5, 10단계, 중독성 Low→Fatal) |
| 표준 버프 5종 | `Core/PotionBuffData.cs:39-43` — potion_hp_small/big, potion_attack, potion_defense, potion_agility |
| PlayerInventory 물약 | potion_stealth, potion_sedative — GLB 존재(파생 4종) |
| GLB 해석 체인 | `UI/GblItemIconRenderer.cs:297 ResolveModelKey` — 명시맵 → 후보1 id 그대로 → 후보2 접두사 제거 → 후보3 trim |
| GLB 검색 경로 | `Systems/RuntimeModelLoader.cs:154` — Models/UserProvided 1순위 → Dungeon → Cemetery → Restaurant. 키 소문자 기준, 빌드 대소문자 구분([159차 P2]) → **파일명 소문자 강제** |
| 기존 전용 GLB | 물약 4종(potion_hp/mp/stealth/sedative) + 약초 6종(herb_red/purple/green/blue/gold/yakcho) — `PotionHerbVisualCoverageTests` 계약 |
| 약초 아이콘 경로 | `Systems/AcquisitionItemVisualResolver.cs:87 HerbGlbIds` — herb_* 색 id만 진입, A1~P10은 미진입(충돌 없음 — 베이크 후 재확인) |
| 파생 파이프라인 | no90-92 검증(potion-bottle hue 재매핑 4종 + herb 틴트 1종). 베이크 스크립트 repo 미보존 → 재작성 |
| 던전 Potion GLB | Models/Dungeon/Potion_1_1~4_5 17개 — 던전 루팅 전용, 본 계획과 별개 유지 |
| 부재 감사 도구 | `Assets/Editor/ItemGLBAudit.cs` — Tools/Item Audit/GLB 부재 목록 산출 |
| 더티 상태 | `Assets/Scripts/UI/CraftingUI.cs` 이미 더티(기존 미커밋 변경) — 최소 패치 원칙(기존 변경 보존, revert 금지) |

## 1. 색 배분 설계 (3축 — 결정론, 수작업 색표 없음)

**속성 밴드 기준 hue:** 공격성=355°(적), 정신성=285°(보라), 회복성=120°(녹), 물리성=210°(청). §1 속성 색 계약(🔴🟣🟢🔵)과 정합.

**약초 40종 (idx 1~10):**
- 축1 hue 오프셋: `[-24°, -12°, 0°, +12°, +24°]` (5단계, 밴드 내)
- 축2 명도: idx 1~5 = 기준, idx 6~10 = −14%
- 2색 스킴: 잎/줄기(베이스) = 속성 기준 hue 고정, 꽃/열매(액센트) = 배분 hue. 원본에 액센트 존이 없으면 전체 틴트 폴백(herb_red 방식) — Phase 0 실측으로 분기.
- 인접 hue 12°는 미묘함 → 구분성은 Phase 8 아이콘 실측으로 판정, 미구분 시 오프셋 상수 트림.

**마약 10단계:** 액체 hue 260°→350° 선형 보간 + 명도 0.95→0.45 + 채도 0.5→0.85 (연보라→검붉은). 병 모양 단일(승인 완료).

**조합 물약 82종:** 액체 hue = 각도평균(hue(재료1)+hue(재료2)) — 재료 개별 오프셋 각 50% 반영. 명도 = 재료1 명도. 물리성 20종도 병 단일(승인 완료).

**표준 버프 5종:** potion_hp 원본 파생 — hp_small(기존 potion_hp hue 재사용)/hp_big(명도↑)/attack(적)/defense(청)/agility(황).

## 2. id 정합 계약 (아이템화 정비)

- **canonical combo id** = `combo_{재료1id}_{재료2id}` (사전순 정렬, 예: combo_A1_P2) — HerbComboDatabase.MakeComboId(id1,id2) 신규 static.
- 3개 생성부 전부 이 id로 통일: CraftingUI.cs:1009, CraftingHelper.cs(CreatePotionItem), AlchemyUI.cs:443(이중 접두사 제거). displayName은 resultName 유지.
- GLB 파일명 = id 소문자: `combo_a1_p2.glb` → 인벤 아이템 생성 즉시 아이콘 자동해결(관례후보1).
- 리스크: 기존 저장본에 `combo_한글` id 아이템이 있으면 신규 id로 미매핑(마이그레이션 미포함 — 테스트 세션 리셋 전제, 별도 승인 시 마이그레이션 추가).

## 3. Phase / Task 분해

### Phase 0 — 파생 베이크 도구 (Python, 기반)
- **Task 0.1** `Tools/derivative_probe.py` — 원본(potion-bottle-099ad7.glb, herb_red/purple/green/blue.glb) 머티리얼·정점컬러 존 프로브 → `TestOutput/derivative_probe.json`. 판정: 약초 액센트 존 존재 여부.
- **Task 0.2** `Tools/derivative_bake.py` — 정점컬러 hue/명도/채도 재매핑, 존 선택(머티리얼 인덱스 or 색거리), `--map <json>` 배치, `--selftest`. 텍스처/임포트 계약 무수정, 기존 파일 덮어쓰기 금지.
- **Task 0.3** 재현 검증: potion-bottle에서 potion_hp 액체 hue 재현 → 기존 potion_hp.glb와 HSV 대조 리포트(TestOutput/). 유리 알파 0.28·렌더러 3분리 유지 확인.

### Phase 1 — 약초 40종 GLB
- **Task 1.1** `Tools/herb_color_map.json` 생성(A1~P10 → hue/명도/틴트존).
- **Task 1.2** 베이크 → `Assets/Resources/Models/UserProvided/a1.glb … p10.glb`(소문자).
- **Task 1.3** 경로 충돌 실측: HerbGlbIds(herb_* 전용)와 A1~P10 무충돌 + 필드 픽업 비주얼(LoadVisual)이 신규 GLB 해석하는지.
- **Task 1.4** 테스트 신규: `Assets/Scripts/Tests/EditMode/HerbGlbCoverageTests.cs` — 40종 Resources.Load + HasModel(id) 전수.

### Phase 2 — 마약 10종 GLB
- **Task 2.1** 그라데이션 표 + 베이크 → `drug_01.glb … drug_10.glb`.
- **Task 2.2** 테스트: drug_{stage:D2} id와 GLB 파일명 1:1.

### Phase 3 — 조합 물약 82종 GLB
- **Task 3.1** GAME_DATA §2.1~2.4 파싱 → `Tools/combo_hue_map.json` 자동 생성.
- **Task 3.2** 베이크 → `combo_{재료1id}_{재료2id}.glb` 82개(소문자 — Phase 4 canonical id와 정합).
- **Task 3.3** 2.2 22행/표기 80종 불일치 목록화 — 데이터 수정은 별도 승인 사항.

### Phase 4 — 조합 아이템화 id 정비 (C#, 사용자 승인 반영)
- **Task 4.1** `HerbComboDatabase`에 `MakeComboId(id1,id2)` static 추가 + resultId(117행)를 MakeComboId로 전환(placeholder 제거).
- **Task 4.2** 3개 생성부 통일: CraftingUI.cs:1009 `combo_{resultName}` → `combo.resultId` / CraftingHelper.cs CreatePotionItem 동일 / AlchemyUI.cs:443 이중 접두사 제거. CraftingUI.cs는 더티 — 기존 변경 보존 최소 패치.
- **Task 4.3** 테스트 신규: `Assets/Scripts/Tests/EditMode/ComboItemIdTests.cs` — MakeComboId 순서무관·결정론, DB resultId 형식, 3개 생성부 id 일치(필요 시 internal 노출 없이 시그니처 수준 검증).
- 완료 기준: 컴파일 error CS=0 (구현 서브에이전트 컴파일 확인 → EditMode 판정은 QA 몫).

### Phase 5 — 표준 버프 물약 5종 GLB
- **Task 5.1** 베이크 → potion_hp_small/big/attack/defense/agility.glb (기존 potion_hp/mp 파일 무손상).
- **Task 5.2** 테스트: PotionBuffData 5 id와 GLB 1:1.

### Phase 6 — 테스트 확장 + 컴파일 게이트
- **Task 6.1** `PotionHerbVisualCoverageTests.cs` 확장 — 전종 존재 계약(40+10+82+5) + 조합물약 액체/유리 분리 구조 계약.
- **Task 6.2** `./run_tests.sh editmode` — 컴파일 0 오류 + 전체 green(QA 서브에이전트 증거 기준).

### Phase 7 — 아이콘 배지 (옵션, 별도 승인)
- 속성 심볼+번호 오버레이(인벤 슬롯 렌더러). UI 코드 수정 — 별도 승인 시 별도 계획.

### Phase 8 — 독립 QA (서브에이전트, 구현과 분리)
- **QA 8.1** 아이콘 실측: 베이크 후 인벤 그리드 스크린샷(약초 40 / 물약 82 / 마약 10) → 프레임+vision 판정 "인접 종 구분 가능성" 보고. 근거와 미검증 구분.
- **QA 8.2** 미구분 항목 발생 시 hue 오프셋/명도 상수 트림 → 재베이크 → 재실측 루프(확정 전 수리 금지 — 보고 먼저).
- 검증 3단계(컴파일/EditMode/Play) 분리 표기 — Play는 별도 승인 후.

## 4. 리스크 / 열린 질문
1. ~~물리성 모양~~ — **병 단일 승인 완료(2026-11-21)**.
2. hue 12° 간격 실측 구분성 — Phase 8 트림 루프로 해소.
3. ~~조합 아이템화 미완~~ — **경로 존재 실측(CraftingUI/CraftingHelper/AlchemyUI). id 3체계 버그로 Phase 4 정비 승인 반영.**
4. 2.2 22행 불일치 — 데이터 표기 수정은 별도 승인.
5. 약초 액센트 존 부재 가능성 — Phase 0 실측 → 전체 틴트 폴백.
6. Resources 용량 — 파생 137개 증가. Phase 1 완료 후 실측 보고.
7. 기존 계약 무손상 — herb_* 5색, potion 4종, 던전 Potion GLB, 유리알파/액체분리/BC7 전부 유지. 파생은 신규 파일만(기존 덮어쓰기 금지).
8. 구(舊) 저장본 combo_한글 id — 마이그레이션 미포함(테스트 세션 리셋 전제).

## 5. 검증 커맨드 요약
- 베이크 자가검증: `python3 Tools/derivative_bake.py --selftest`
- 존재 계약: `./run_tests.sh editmode` (컴파일 0 오류 + 신규 커버리지 테스트 green)
- 부재 감사: Unity 메뉴 Tools/Item Audit/GLB 부재 목록 산출 — 137종 잔여 부재 0
- 아이콘 실측: 인벤 스크린샷 → 프레임 추출 + vision 판정(QA 8.1)

## 6. 실행 순서 요약
Phase 0(도구)와 Phase 4(id 정비)는 독립 → 병렬 위임 → 1(약초) → 2(마약) → 3(조합 GLB) → 5(버프) → 6(테스트+컴파일) → 8(QA 루프). Phase 7은 별도 승인. 각 Phase 완료 후 결과 보고.
