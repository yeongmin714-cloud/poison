# 계획 (2026-10-11, 28차): 결과 창 피그마 템플릿 통일 + 진행바 순수 채움 + E프롬프트 전면 재판정 + HUD HP 수치 라벨

> 상태: 계획 전용 (구현 미착수 — 사용자 "진행" 명시 시만 실행)
> 27차 Play 사용자 확인: **낚시 창 오픈 ✅ / HUD 링 ✅(해결) / 진행바 Fluent 감은 느낌 ✅**
> 잔여: ①낚시 성공 후 결과 창(피그마) + 물고기 사진(GLB)·설명 ②채집/채광/벌목도 전리품(바구니) 대신 피그마 결과 창 ③진행바의 이동 마크 제거(순수 채움) ④E프롬프트 대상 앞 미표시 재판정 ⑤하트 링 아래 남은/전체 체력 라벨(신규 제안)

---

## 사전 확인 결과 (소스/로그 실측 — 읽기 전용)

1. **피그마 결과 창은 이미 존재**: `HarvestResultUTK` = F3 낚시/채집/광질 결과 공용 팝업(Figma fishing/gathering/mining-result-ui 템플릿 정합 — HUDHeader + ResultPanel(ItemImage/ItemSpecs/Description/ActionButtons) + SystemTip, GitHub-dark). 낚시는 `HarvestResultBridge.Publish(immediate: true)`로 이미 이 창을 띄운다.
2. **전리품 형태의 실체 = LootBasket**: 플레이어 채광/벌목(`ResourceNode.HarvestForPlayer`:141)과 채집(`HerbPickup.Harvest`:310)은 결과를 **LootBasket.Create**(바구니 지상 드랍 → E → LootWindow)로 내보낸다 — 사용자가 "전리품 형태"라 부른 것. 병사 자동 작업 결과도 동일 바구니 경로.
3. **물고기 데이터 준비 완료**: FishCatalog 50종 결정론(id=`fish_glb_<key>`, 한국어 displayName/description "낚시로 잡은 아틀란틱 연어. 신선하다." 스타일, 등급 Common/Rare/Epic) + **GlbPath(index)** 매핑 존재. 물고기 GLB 334종 실존(`Assets/새로운 glb/crops-fish/` — fish-atlantic-salmon/fish-catfish/... + f002~ 넘버링).
4. **아이콘 갭**: 결과 창 이미지 = `ItemIconDatabase.GetOrCreateIcon` → **GblItemIconRenderer**(GLB→RT 베이크, 128px, 캐시 200) 우선 — 그러나 로드 경로가 `Resources/Models/UserProvided/<modelKey>` 한정 + 접두사 리졸버에 `fish_glb_` 없음 → **물고기는 GLB 매칭 실패 → 절차 폴백 아이콘**. mat_wood/mat_stone/허브는 mat_ 접두사 리졸버+RuntimeModelLoader 경로로 이미 커버 가능(Play 확인 항목).
5. **진행바의 "이동하는 검정 마크" = 27차 팁 글로우**(shadow_glow 텍스처 쿼드) — 라이트 틴트임에도 어둡게 읽힘. 제거하면 순수 채움 성장.
6. **E프롬프트 판정 로그 실측**: 대상 감지는 작동 중(`Warehouse_1` 2.3m·`NaturalResource_Rock_05` 2.9m 감지 로그) — 감지→표시 경로는 살아있음. 미표시 후보: ①z-order — 프롬프트 오버레이가 `root.Insert(0)`(최하단 드로우)라 풀스크린 호스트에 가려질 가능성 ②감지/활성 범위 경계(Rock_05=2.9m > 2.8m 활성 범위 → 비표시가 정상인 순간 존재) ③채널링 중 숨김(설계상 정상 — 바와 동시 노출 안 함) ④전면 오프셋이 커서 체감상 "대상 앞"이 아님(clamp 0.4~1.2m).
   - 범위 정합 확인: ResourceNode 활성 2.8m(3D 거리, :92) / HerbPickup 2.5m(3D, :248) / 프롬프트 수집은 XZ만 사용(3D 대비 약간 넓게 잡힘 — 정합성 정리 대상).
7. **HUD HP 텍스트**: HUDUTK 게이지 스택(7층)에는 수치 텍스트 층이 없음 — 신규 층 필요. 폴링(250ms)에서 hpRatio 원천(PlayerHealth.HPRatio·MaxHP)은 이미 확보돼 있어 텍스트 갱신은 공짜.

---

## P1 — 결과 창 통일 (낚시 성공 + 채집/채광/벌목)

파일: `ResourceNode.cs`, `HerbPickup.cs`, `HarvestResultBridge.cs`, `HarvestResultUTK.cs`, `FishCatalog.cs`, `GblItemIconRenderer.cs`

1. **플레이어 직접 경로 전환**: `ResourceNode.HarvestForPlayer`(채광/벌목)와 `HerbPickup.Harvest`(채집) 완료 시 —
   - `HarvestResultBridge.Publish(kind, verb, itemName, desc, rarity, count, tip, itemData, immediate: true)` 호출 추가(낚시와 동일) — 즉시 피그마 결과 창 표시.
   - **플레이어 경로의 LootBasket.Create 제거** — 아이템을 직접 인벤 지급(기존 yield/희귀보너스/EXP 수치 보존). "전리품 형태" 소멸.
   - **병사 자동 채집/광질은 기존 LootBasket 유지**(RTS 자원 수급은 바구니 경로 — throttle 스팸 방지 규약 그대로).
2. **벌목 종류 신설**: `HarvestKind.Woodcutting` 추가 + HarvestResultUTK의 헤더/badge/창 크기 variant 확장("WOODCUTTING RESULT"/"벌목 성공" — fishing/gathering/mining variant 패턴 동일). 낚시=Fishing, 채집=Gathering, 채광=Mining, 벌목=Woodcutting 매핑.
3. **물고기 사진(GLB→아이콘)**: `GblItemIconRenderer`에 `fish_glb_` 접두사 리졸버 추가 — id → `FishCatalog.GlbPath` → **에디터 AssetDatabase 로드**(NaturalResourceSpawner GLB 경로 선례) → 기존 마운트/RT 베이크 파이프라인(원격 위치·스케일 정규화 128px) 재사용 → 결과 창·인벤 아이콘 동시 커버. 캐시 상한(200)을 FishCatalog 50종+기존 분량 여유로 상향.
4. **설명 자동 반영**: publish 시 itemData.description 전달 유지 — 물고기 한국어 설명(FishCatalog)이 설명 섹션에 표시되는지 Play 확인. 부족 시 FishCatalog 설명 보강.
5. **자원 아이콘 확인**: 광석/목재/약초가 결과 창에서 정상 아이콘으로 보이는지 Play 확인 — 절차 폴백으로 나오면 동일 GLB 리졸버에 id 매핑 추가.

## P2 — 진행바: 이동 마크 제거 (순수 채움)

파일: `WorkProgressBar.cs`

- 팁 글로우 쿼드(_tip, shadow_glow) 삭제 + LateUpdate 팁 추적 코드 제거 — **트랙(라운드 프레임) 안에서 파랑 채움만 좌→우 성장**. 트랙/채움 베이크(26/27차) 유지.
- (선택 미화요소 유지 여부) 채움 상단 하이라이트 그라데이션은 베이크에 포함됨 — Fluent 감 유지.

## P3 — E프롬프트 "대상 바로 앞" 재판정·수리

파일: `EInteractionPromptUTK.cs`

1. **z-order 수리**: 오버레이 `root.Insert(0, ...)` → **`root.Add(...)`(최상단 드로우)** — 풀스크린 호스트에 가려짐 제거. pickingMode.Ignore 유지(클릭 흡수 금지 규약).
2. **전면 오프셋 타이트화**: front = clamp(d×0.5, 0.4, 1.2) → **clamp(d×0.35, 0.25, 0.8)** — 대상 "바로 앞" 체감.
3. **범위 정합**: 프롬프트 수집 거리를 대상별 활성 범위와 통일(자원 2.8/허브 2.5 — XZ 대신 3D 거리로 일원화, 씬 경사 대응).
4. **진단 강화**: 후보 변경 로그에 **표시 상태(display)+최종 화면 좌표** 추가 — 다음 Play에서 "미감지/감지+미표시/표시+위치오류" 3분지 즉시 판정.
5. **판정 유의(사용자 안내)**: 채널링 진행 중·창 열림 중엔 숨김이 의도 동작 — 프롬프트 판정은 접근 후 E 누르기 전 순간으로.

## P4 — HUD: 하트 링 아래 남은/전체 체력 라벨 (Fluent)

파일: `HUDUTK.cs`, (참조) Figma HUD 23:6

- 게이지 스택 하단(하트 링 바로 아래)에 **"67 / 100"** 형식 수치 라벨 신설 — Geist Mono 계열 수치 스타일(TextMain/TextSub, 소형 배지 톤, GitHub-dark). 스태미나 링 아래도 동일 대칭("100 / 100") — 사용자 제안대로 하트 우선, 번개는 대칭 일관.
- 폴링(250ms)에서 hpRatio와 함께 MaxHP/HP 읽어 갱신 — 스트립 링과 같은 틱(드리프트 0).
- 라벨이 링 아래 24px 여백 계약(GaugeBottomMarginPx=24)과 충돌하지 않게 라벨은 링과 핫바 사이 여백에 배치 + 하단 앵커 계약 수식에 통합(진단 로그도 함께 갱신).
- 스태미나 링 위 시계/시간 UI와 겹침 없는지 Play 확인.

## P5 — 검증 게이트 (순서 고정)

1. Compile: 신규 `error CS` 0.
2. EditMode 전체: baseline(624 중 617/7) 대조 — 신규 회귀 0. (HarvestResult/Bridge 계약 테스트 있으면 갱신)
3. **Play — Test_15 통합 판정**: ①낚시 성공 → 결과 창(물고기 GLB 사진+한국어 이름/설명/등급) ②벌목/채광/채집 성공 → 결과 창(바구니 없음, 광석/통나무/약초 설명 표시) ③병사 자동 작업 = 기존 바구니 유지 ④진행바 순수 채움(마크 없음) ⑤E프롬프트 대상 바로 앞 표시(진단 로그 포함) ⑥HUD 하트 링 아래 "HP/MaxHP" 라벨.
4. 명시 요청 시 stage/commit/push.

---

## 검증 상태 라벨
- 소스/로그 검증(이번 턴): 위 사전 확인 1~7 — 라이브 트리 기준.
- Compile/EditMode/Play: 미실행 — P5 게이트에서 수행.
