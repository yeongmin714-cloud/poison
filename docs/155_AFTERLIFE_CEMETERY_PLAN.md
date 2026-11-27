# 155차 — 이계(사망) 맵 공동묘지화 + 사망 이후 테스트씬 계획

> 상태: **계획만 제시됨 — 실행 금지**(사용자 "일단 계획부터"). 확정 시 Phase대로 수행.
> 148차 던전 공사(탑다운 정합+4배 확장+5층)는 인게임 Play 확인 대기로 보류 — 본 계획과 병행하지 않음.

## 0. 현재 실측 (2026-11-05 코드 열람 — 계획의 근거)

### 0-A 사망→이계→부활 시스템 — **이미 구현됨**(사용자 추정대로)
- 흐름(`AfterlifeSurvivalSystem` 467행): `PlayerHealth.Die` → `TryInterceptDeath` 가로채기 → **이계 아레나 진입**(월드 시간 동결) → 2분마다 웨이브 강화 → 타이머 완주(Easy 5분/Normal 10분/Hard 15분) → 귀환 문 개방 → 접촉 → **기존 위치 부활(HP 50%)** / 봉헌(탈주, 골드 20%+수확 50%) / 이계 HP 0 = **퍼머데스**(세이브 삭제→타이틀)
- 보조 시스템: `AfterlifeEnemy`(웨이브 적), `AfterlifeAugmentSystem`(증강), `AfterlifeOrb`(수확 오브), `AfterlifeExitDoor`(귀환 문)
- 진입 경로: `IndoorSceneTransition` case `"afterlife"` → `AfterlifeInteriorBuilder.BuildAfterlifeInterior()`

### 0-B 현행 이계 맵 — 절차 생성 어두운 원형 아레나(`AfterlifeInteriorBuilder` 122행)
- 반경 24m 원형 바닥(거의 검정) + 기둥 링 14 + 경계 실린더 벽 + 진홍 룬 링 + 붉은 조명 3 + 중앙 청광 + 귀환 문 앵커(비활성 개시)
- **테마 없음** — 공동묘지화의 대상

### 0-C 게임플레이 계약(무손상 필수 — 리스킨만)
- `ArenaRadius=24`(적 스폰 반경 0.65R 참조) / 귀환 문 앵커 (0,0,−14.4) `AfterlifeExitDoor` / 룸 (5000,0,5000) 월드 격리
- 탑다운 원칙(천장 없음) / Light 섀도우 None 계약 / 웨이브·부활·퍼머데스 로직 전부 무수정

### 0-D 에셋 실측 — **"공동묘지" 전용 에셋팩은 디스크에 없음**(전수 검색)
- 있음(조합 가능): 마을 GLB — `Stone Knight Statue`(석상), `Grey Stylized Church`, `Stone Arch Bell`, `Stylized Bell Tower`, `Mossy Stone Pillar`, `Mossy Rock Benches`, `Medieval Stone Gatehouse` / 외부 장식 — `석조등`
- 던전 팩(이미 Resources/Models/Dungeon 수용·로더 폴백 확립): `Cofin`(석관), `Skeleton`, `Skull`, `SkullCandle`, `CandleHolder_1_1~1_4`, `Cage`, `Chain_1_*`, `Bone_1~3`
- 묘비: GLB 부재 → **절차 생성(돌 판+둥근 머리)** + 기존 `Gravestone.cs`(37차, E키 에피타프 낭독 시스템) 재사용
- 마을 GLB는 Resources 밖(런타임 로드 불가) → **Resources/Models/Cemetery/ 복사 수용 필요**(147차 P-A2 던전 선례 1:1)

## 1. Phase 계획

### P0 — 에셋 수용·감사 (**변경: Necropoly 팩 수용 — 기존 마을 GLB 조합안 대체**)
- 2026-11-05 다운로드 에셋 실측: `GravefieldFantasyCemeteryPack800LowPolyAssets`(메인, EmaceArt/Necropoly — 무덤/석상/납골묘/오벨리스크/담장/관/십자가/촛불/지면 타일) + `GravefieldCemeteryKitStatuesExpansion`(석상 확장). FBX+PRE 프리팹+URP 머티리얼+팔레트 아틀라스(던전팩 동일 구조). **스크립트/DLL 0건 — 컴파일 리스크 없음**
- **완료(2026-11-05)**: 수동 추출 임포트 — 메인 1,653파일+석상 153파일(패키지 meta 동반 → GUID 보존, 프리팹 참조 무손상) → `Assets/EmaceArt/Necropoly/`
- Unity 배치 임포트+컴파일 검증(compile.log error CS 0 게이트) 진행 중
- 선별 감사: 사용 FBX 전수 파서 실측(147차 P-A1 스크립트 재사용 — 삼각형/실스케일/머티리얼) → 선별 리포트
- 런타임 로딩: Necropoly 프리팹은 Resources 밖 → **선별 프리팹/FBX를 `Resources/Models/Cemetery/`로 복사**(147차 P-A2 던전 선례 1:1) + `RuntimeModelLoader` 3차 폴백(`Models/UserProvided` → `Models/Dungeon` → `Models/Cemetery`) — 기존 2경로 무손상
- 던전 팩 보조 소품(Cofin/Skeleton/Skull/SkullCandle/CandleHolder/Cage/Bone)은 기존 `DungeonAssetCatalog` 키 재사용 — 신규 수용 불요
- 검증: 선별 감사 리포트(TestOutput/155_cemetery_audit.txt) + 로더 폴백 계약테스트 갱신

### P1 — 공동묘지 아레나 리빌드 (`AfterlifeInteriorBuilder` 게임플레이 계약 무손상)
- **유지**: 원형 반경 24m/스폰 0.65R/귀환 문 앵커/월드 격리/천장 금지/섀도우 None/붉은 조명 3+중앙 청광(이계 정체성 유지)
- **바닥 리스킨**: 거의 검정 → 잿빛 흙 톤(0.16,0.15,0.17 계열), 진홍 룬 링은 유지
- **센터피스(북측)**: Grey Stylized Church(배경 실루엣, 렌더러 전용) + Stone Arch Bell
- **석상 링**: 기존 기둥 14 자리 → **Stone Knight Statue 8 + Mossy Stone Pillar 6 교체 배치**(충돌은 기존 경계 실린더 단일소스 유지 — 석상은 장식, StripColliders+접지)
- **무덤 구역**: 절차 묘비(돌 판+둥근 머리, 2 크기 변형) 12기 — 남서 반원 호 배치(결정론 좌표 하드박제, Random 0건) — 각기 `Gravestone.cs` 부착(에피타프: 이계 테마 결정론 문구 12종)
- **소품**: Cofin 석관 2 + Skeleton 2 + SkullCandle/CandleHolder 촛불 4 + Cage 1 + Bone 산포 6 + 석조등 2 (전부 GLB 승격·실패 시 조용한 스킵 계약)
- **검증**: 계약테스트 신설(스폰 반경/문 앵커/룸 좌표 불변 실측) + `Afterlife*` 기존 테스트 1:1

### P2 — 사망 이후 테스트씬 `Test_22_AfterlifeCemetery.unity`
- 구성: Test_21 부트 패턴 준용(씬 부트 → IndoorSceneTransition `"afterlife"` 경유 진입 — **실제 사망 경로와 동일 진입점**)
- 사망 전이 검증: 테스트 부트 훅에서 더미 HP 0 → `TryInterceptDeath` 실발화(우회 빌드 아님 — 전이 체인까지 검증)
- 테스트 편의: 부트 파라미터로 이계 타이머 단축(30초) — **시스템 상수 무수정**, 부트에서 `AfterlifeSurvivalSystem` 테스트 오버라이드(신규 internal setter 1개, 프로덕션 경로 무영향)
- 확인 항목: ①사망→공동묘지 진입 전이 ②석상/교회/묘비 렌더 ③묘비 E키 에피타프 ④웨이브 스폰(0.65R) ⑤귀환 문 개방→접촉→부활(HP 50%) 복귀 ⑥봉헌 경로
- 에디터 검증 병행: `Afterlife155Probe`(Edit 배치) — 빌드+탑다운/플레이어뷰 캡처+계약 실측(148차 P4 프로브 패턴)

### P3 — QA
- 컴파일 + EditMode 전수(baseline 비교) + 155 프로브 캡처 vision 판독
- Play(사용자 승인 게이트): 실제 사망→이계→부활 순회

## 2. 셀프감사 (중복/키충돌)
- `Models/Cemetery/` 신설 — UserProvided/Dungeon 키 충돌 없음(파일명 유일)
- `AfterlifeInteriorBuilder` 시그니처/반환 계약 유지 — IndoorSceneTransition/AfterlifeSurvivalSystem 호출부 무수정
- `Gravestone.cs` 재사용 — AmbientDialogueManager 기존 결합 무접촉(부착만)
- Melee 플래키 2건과 신규 실패 구분 유지

## 3. 열린 질문 (사용자 답 오면 기록)
1. **공동묘지 전용 에셋팩**을 따로 보유 중인지 — 실측상 부재라 기존 에셋 조합(석상=Stone Knight Statue)으로 구성안. 특정 팩이 있으면 수용 절차 추가.
2. **조명 분위기**: 붉은 이계 조명 유지(현행) vs 잿빛 고요 톤 교체
3. **묘비 에피타프**: 풍자/세계관 문구 — 예시 문구 방향(진지 vs 유머)
