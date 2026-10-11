# 과일나무 수확(과일 GLB) + Idyllic 나무 벌목 + 도끼 커서 (실행 전 계획)

날짜: 2026-10-11
상태: **계획 전용 (plan-only)** — 사용자가 "실행" 명시 전까지 코드 미반영
요청자: 영민 조

## 1. Goal
1. **과일나무 수확**: 메인씬에 배치된 새 nature GLB 과일나무에서 **실제 과일 GLB를 수확**한다.
   (현재 placeholder 약초를 줌 → 진짜 과일 아이템 + 과일 GLB 시각으로 교체)
2. **Idyllic 나무 벌목**: 메인씬의 **모든 Idyllic 장식 나무**를 베어 통나무를 얻는다.
3. **도끼 커서**: 벌목(나무) 대상에 **곡괭이 대신 도끼 커서** (가능 시).

기존 확정(21차 작업)과 통합: 벌목은 방금 만든 **E 채널 시스템(파랑 진행바, 이동 취소, 리스폰) + 공용 E 프롬프트**를 그대로 탄다.

## 2. 현재 실측 (read-only)
| 항목 | 파일 | 실측 |
|---|---|---|
| 자연 과일나무 배치 | `NaturalResourceSpawner.cs:134~137` | `CreateFruitNode` → `HerbPickup(_herbType=Red)` **placeholder(치유초)**. 과일 GLB 모델은 `Assets/새로운 glb/nature/`(fruit-tree-mature-fruiting.glb, small-fruit-tree.glb). 과일 ItemData `PlayerInventory.Fruit_Apple`(사과, Food)는 **이미 존재**(line 249). 글bat: 자연 과일나무는 지금 실제 과일을 안 줌. |
| Idyllic 장식 나무 배치 | `IdyllicDecoPlacer.cs` | `Place(model,x,y,z,scale,rng,parent)`가 전 데코 배치의 공통 지점. 나무 = Willow/Broadleaf/Fir(활엽 3색)/Blossom 등 `WPrefab`(collider 플래그). **수천 그루 장식 나무** — 현재 순수 시각, 벌목 불가. |
| 돌 자원 노드 선례 | `NaturalResourceSpawner.cs:374~380` | `CreateRockNode` = `AddComponent<ResourceNode>() + SetPrivateField("_resourceType", Stone)` — 벌목도 동일 패턴. |
| 벌목 채널 인프라 | `GatherChannelSystem.cs`(21차 신규) | `ResourceNode(Wood)`를 플레이어 E로 베면 파랑바 채널 → `HarvestForPlayer`(mat_wood LootBasket+리스폰). **이미 동작**. |
| 커서 분류 | `HoverTargetClassifier.ClassifyOne`(ResourceNode→Mine) + `ContextCursorSystem`(Mine→곡괭이) | 벌목 전용 도끼 커서 없음 — Wood 분류 신설 필요. |

## 3. 제안 접근 (신규 인프라 최대 재사용)

### P1 — 과일나무가 실제 과일 수확
- `NaturalResourceSpawner.CreateFruitNode`: `_herbType=Red` → 실제 과일 아이템으로 교체.
  - 과일 지정: `HerbPickup`의 `_cropKey` 경로(CropCatalog) 또는 기존 `Fruit_Apple` 매핑. **수확 시 과일 GLB 시각이 뜨도록** 항목-GLB 매핑 확인/추가.
  - ⚠ 확인 필요: `LootBasket` 드랍이 아이템의 GLB 모델을 보여주는지(과일 GLB pickup), 아니면 아이콘만인지 — P1 시작 시 read 확인 후 과일 GLB 시각 경로를 잇는다. 없으면 추가.
- 과일 종류: 확정 필요(아래 질문 ①).

### P2 — Idyllic 나무 모두 벌목 가능
- **배치 시 ResourceNode(Wood) 부착**: `IdyllicDecoPlacer` 나무 배치 지점에서 나무 인스턴스에 `ResourceNode(Wood)` + 접근/충돌 콜라이더(WPrefab.collider) 부착. → 기존 E 채널로 벌목 즉시 동작(통나무 LootBasket + 리스폰).
- **성능 고려 ⚠**: 장식 나무가 **수천 그루** → 전부 `ResourceNode`면 ①매 프레임 Update 거리 체크 수천 개 ②공용 E 프롬프트의 `FindObjectsByType<ResourceNode>` 0.2s 스캔 비용 급증.
  - 완화책(택1): (a) 나무 노드 전용 가벼운 마커 + 프롬프트/채널 스캔을 플레이어 반경(OverlapSphere/격자)으로 제한, (b) 장식 나무 중 벌목 대상 수를 계층 상한으로 제한, (c) 프롬프트 스캔 주기 하향.
  - 기본 제안: `NaturalResourceSpawner`에 **Wood 카테고리**(기존 돌/과일/허브처럼 60~120그루, idyllic 나무 프리팹 사용)를 추가해 성능 안전하게 벌목 제공 + 별도로 일부 장식 나무 전환. "모두"가 절대적이면 (a) 완화책으로 추가.

### P3 — 도끼 커서 (가능)
- `HoverTargetClassifier`: `ClassifyOne`의 ResourceNode 판정에서 `NodeType==Wood`면 `TargetKind.Wood`(신규), 아니면 기존 `Mine`.
- `ContextCursorSystem`: `Wood` → `BuildAxe()`(도끼 절차 아이콘 — 기존 `BuildPickaxe` 패턴), `Mine` → 곡괭이 유지.
- 공용 E 프롬프트 라벨 동일 유지("E키를 눌러 상호작용").

## 4. 단계별 계획
- **P1** 과일: `CreateFruitNode` 과일 아이템 교체 + 과일 GLB 시각 경로 확인/추가.
- **P2** 벌목: `ResourceNode(Wood)`를 idyllic 나무 배치(또는 NaturalResourceSpawner Wood 카테고리)에 부착.
- **P3** 커서: `TargetKind.Wood` + `ContextCursorSystem` 도끼 아이콘.
- **검증**: 신규 컴파일 `error CS` 0 + 전체 EditMode baseline(624/617/7) 신규 회귀 0 + Play 확인(과일나무 수확→과일 GLB, 나무 벌목→통나무+파랑바, 커서 도끼).

## 5. 리스크/트레이드오프/오픈 질문
- **① 과일 종류**: 자연 과일나무가 드랍할 과일 — 사과(fruit_apple) 하나? 아니면 과일 다종(크롭)? 점질: nature fruit-tree 모델이 사과나무인지.
- **② "모든 Idyllic 나무" vs 성능**: 장식 나무 수천 그루 전부를 벌목 노드로 바꾸면 E프롬프트/채널 스캔 부하 우려. 성능 안전(제한 수)로 할지, 전부 + 최적화로 할지.
- **③ 도끼 필수 게이트**: 벌목 시 `HasItem("tool_axe")` 요구? (도끼가 지금 dead item — 살리는 기회). 그대로 두면 어느 나무나 껌 베게 됨.
- **④ 병사 자동 벌목**: `NaturalResourceSpawner` Wood 카테고리로 만들면 병사 `GuardTaskSystem` 자동 벌목도 자동 연동됨(추가 의도 확인).
- **⑤ 과일 GLB pickup 시각**: LootBasket 아이템 GLB 매핑 유무 — P1에서 확정.

## 6. 완료 조건
- 과일나무 수확 → 과일 GLB 아이템 드랍
- idyllic 나무(또는 Wood 카테고리) 벌목 → 파랑 진행바 + 통나무(mat_wood) LootBasket + 리스폰, 이동 시 취소
- 벌목 시 도끼 커서
- 컴파일 0 + EditMode baseline 신규 회귀 0
- QAPROGRESS 기록 (커밋은 명시 요청 시)