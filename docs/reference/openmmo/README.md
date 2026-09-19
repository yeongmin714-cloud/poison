# OpenMMO 참고문서 선별 인덱스 (포이즌용)

> 출처: github.com/Julian-adv/OpenMMO (Rust/Three.js MMO, doc/ 설계문서 55종)
> 라이선스 존중: 코드/텍스트 복사 금지 — 방식·공식·튜닝값 벤치마킹 용도.
> 서브에이전트에 컨텍스트로 주입할 때 이 인덱스 + 해당 문서 1~2개만 전달.

## TIER 1 — 기존 시스템 즉시 보강 (다음 사이클 채용 가능)

| 문서 | 연결되는 포이즌 시스템 | 벤치마킹 포인트 |
|:-----|:----------------------|:----------------|
| ITEM_TIERS.md | 등급테이블(wood~crystal), DropTable SO, 보석상자(Phase 29) | 티어 세트 구성(가죽/체인/판금), 드랍=시그니처+아이템별 독립 롤, 던전당 기대 드랍수 |
| LEVEL_CURVE.md | GuardLevelSystem Lv1~50, MonsterLevelSystem(Phase 3.8) | 목표 페이스 표(시간당 레벨), 콘텐츠 의존성 설계 |
| ECONOMY.md | 상점/떠돌이상인/밀매/선술집/용병 고용 | 머니 펌프 방지(금화 유출구 설계), NPC 간 거래, 인플레이션 감사 |
| COMBAT.md | Phase 3.8 스탯/레벨업, 영주 능력치 | D&D식 6속성 생성(4d6), HP 재생 공식(회복 주기/조건), Guard 계산 |
| NPC_MONSTER_AI.md | 4국 AI 전쟁, 영지 병사 AI, 몬스터 어그로 | 2계층 AI(규칙+LLM), LLM 스케줄러 규칙 — 영주/병사 LLM화 시 청사진 |

## TIER 2 — 확장 컨텐츠 사이클 후보용

| 문서 | 연결되는 포이즌 시스템 | 벤치마킹 포인트 |
|:-----|:----------------------|:----------------|
| HOUSE_BUILDING.md / HOUSING_SYSTEM.md | 성 내부(InteriorBuilder 6종), 침대세이브 | 설계도 기반 건축, 배치 검증, 소유권 — "영지 건설" 대형 Phase 청사진 |
| ESTATE_STORAGE.md | WarehouseSystem 20슬롯 | 창고 슬롯/보관 설계 |
| GATHERING.md | HerbPickup, 광부/벌목 임무(MiningMission) | 채집 플레이 흐름, 나무 식별자 안정화, 리스폰 튜닝 |
| REPEAT_FARMING.md | 약초 리스폰 게이지 | 반복 파밍 피로도 설계 |
| DUNGEON.md / DUNGEON_REWARD.md | 동굴 보석상자(Phase 29), 드라큘라(Phase 28) | 던전 구조, 보상 티어 |
| MONSTER_SEPARATION.md | 병사 40명 RTS 뭉침 | 군집 회피(separation) 알고리즘 |
| TITLES.md / HEROIC_TALES.md | 복수명부(Phase 14), 업적(G3-13) | 칭호 획득 조건 — 영주 처형/암살 → 칭호 시스템 |
| INSTRUMENT.md | BardMercenary(류트, 15m 버프) | 악기 아이템화 — 바드 확장 |
| MOUNTS.md | (신규 후보) | 탑승물 이속/전투 |
| HUNGER.md / FISHING.md | (신규 후보), 요리 12종 연계 | 허기 게이지, 낚시 흐름 |
| WORLD_BUILDING.md | 5국/81영지 세계관 | 세계관 문서 양식(세력/인물/지역 구조) |

## TIER 3 — 조건부 (지형 고도화/성능 패스 시에만)

| 문서 | 조건 | 비고 |
|:-----|:-----|:-----|
| TERRAIN_GENERATION.md / SPLATMAP_V2.md | 지형 리워크 시 | 2단 해상도, 스플랫맵 — ⚠️ 포이즌 기존 좌표함정(확장1450m vs 스플랫1000m, 지형텍스처 금지)과 상충 → 리워크 전 재설계 필수 |
| MAP_DESIGN.md | 지형 리워크/미니맵 개선 시 | 타일링·로딩, fog of war(월드맵 안개와 유사) |
| RIVER_SYSTEM.md / WATER_SYSTEM.md / VEGETATION_SYSTEM.md / WEATHER_SYSTEM.md | 계곡→강/날씨 확장 시 | Gerstner 파, 바람 연동 식생 |
| RUNTIME_PERFORMANCE.md / LOADING_OPTIMIZATION.md | 성능 패스 시 | MSAA off/DPR cap 실측, 로딩 최적화 — Unity 용어로 재해석 필요 |
| DEBUFF.md / MANA_SKILLS_MAGIC.md | 상태이상/스킬 시스템 도입 시 | 넷핵 기조 설계 철학(DESIGN_DIRECTION.md 함께) |

## 배제 (MMO 서버 전용 / 이미 보유)

- AGENT_CLIENT, AGENT_CLIENT_QUICKSTART, REMOTE_AGENT_CLIENT, AGENT_MANAGER, WORLD_EVENT_DELIVERY, NETWORK_METRICS, TERRAIN_STATIC_SERVING — WS 프로토콜/서버 권위 전용
- SERVER_SIDE_MONSTER_AI, MOVEMENT_AUDIT, COMBAT_AUDIT — 서버 권위 감사 기록
- CHARACTER_NAMES — 포이즌에 영주 100명+용병 400명 이름풀 이미 존재
- ITEM_LOCKS, FENCE_PLACEMENT, CAPE_CUSTOMIZATION, ANIMATION, ZONE_SYSTEM, PRICING — 소실/부가 가치 낮음 (PRICING은 ECONOMY로 통합됨)
