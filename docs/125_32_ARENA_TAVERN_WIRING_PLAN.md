# [125차 후속32 계획] 아레나 로비 중세 주점 텍스처 배선 — tavern_* 세트(바닥/벽/골조)

> 상태: **계획 수립 — 구현은 사용자 승인 후.** 사용자 배치 파일 실측 + 배선 경로 소스 실측 기반.
> 가구는 사용자 확인대로 GLB 차후 배치 예정 — **본 사이클 제외**(현 단색 유지).

---

## 배치 파일 확인 (실측)

| 파일 | 해상도 | 판정 |
|---|---|---|
| tavern_floor_plank.png.png (+_n) | 1024×1024 | ✓ 규격 적합 — **단, 이중 확장자 .png.png(결함 — T0 리네임 필요)** |
| tavern_wall_plaster.png.png (+_n) | 1456×720 | ✓ 기존 벽 계약(1456×720, 1장=2.2×1.1m)과 동일 치수 — 규격 정합 |
| tavern_wall_timber.png.png (+_n) | 720×1456 | ✓ 세로 비(골조/기둥/보용) — wood_frame(864×1536)과 동일 역할 |

- 비트/컬러타입: 8bit RGBA 전원 — Unity 임포트 호환 문제 없음
- 가구 텍스처 미배치 = **사용자 의도 확인**(GLB 차후) — 계획에서 제외
- NPOT(1456×720/720×1456)도 기존 세트가 동일 규격이라 정상 운용 실측됨
- 임포트 설정: 기존 세트 실측 결과 **일반 텍스처 임포트(sRGB+mipmap, Normal map 타입 미사용 — 로더의 Configure(linear)는 랩/필터만 적용)** → 신규 6종도 Unity 기본 임포트로 충분, 별도 설정 불필요

## 배선 경로 실측 (소스)

- 로비(ArenaRoom) 바닥/벽의 최종 머티리얼은 ArenaInteriorBuilder의 절차 텍스처(GenerateCastleFloor*/Wall*)가 아니라, 빌드 후 **IndoorSceneTransition HQ 레이어 → IndoorMaterialFactory.ApplyToRoom**(파일 텍스처+타일링)이 덮어쓴다(후속24 B1 "HQ 머티리얼 적용 완료" 로그 실측과 정합).
- 따라서 주점 배선 삽입점 = **IndoorMaterialFactory(신규 tavern 메서드) + IndoorSceneTransition.ApplyHighQualityInterior의 buildingType 'arena' 분기**. arenapit(투기장)은 기존 조기 반환 유지(실외 계약), 성 내부는 기존 HQ 유지(**불변 계약 — 기존 5종 텍스처 교체 금지**).
- 파일 로더: IndoorTextureLoader(BasePath="Indoor/", 고정 파일명 키) — tavern 세트 키 추가 필요.

---

## 수리 항목 (판정 기준 사전 정의)

### T0. 파일명 정리 — 이중 확장자 리네임(결함 수리)
- 6건: `tavern_*.png.png` → `tavern_*.png` — Resources.Load 규약(단일 확장자) 위반 상태로는 로드 실패.
- .meta는 미생성 상태(Unity 최초 포커스 시 자동 생성) — 리네임 후 자동 생성에 맡김.

### T1. IndoorTextureLoader 확장 — tavern 세트 키
- 필드/프로퍼티 추가: TavernFloor/TavernFloorNormal/TavernWallPlaster/TavernWallPlasterNormal/TavernWallTimber/TavernWallTimberNormal + `HasTavernFiles`(tavern_floor_plank 존재 판정).
- 타일링 상수: TavernFloorCoverMeters=2.4(바닥 1장=2.4×2.4m — 기존 계약 동일), TavernWallCoverX=2.2/Y=1.1(벽 1456×720), TimberCover 세로 기준(720×1456 — 기둥/보 타일링은 CreateWoodFrame 계약 승계).
- 로드 로그에 tavern 6종 로드 상태 추가(진단).

### T2. IndoorMaterialFactory 확장 — 주점 머티리얼 3종
- `CreateTavernFloor(roomW, roomD)` — tavern_floor_plank(+노멀) URP Lit, 타일링=방 크기/2.4m(기존 CreateFloor 동일 산식), Smoothness 0.25 계약.
- `TryCreateTavernWall(roomW, roomH, out lower, out upper)` — 상부=tavern_wall_plaster(2.2×1.1 그리드), 하부=동일 plaster 톤 다운 틴트(기존 투톤 구조 유지 — 하부 전용 파일은 미제공이라 재활용; **기본안**, 대안: 하부를 plank 바닥 텍스처 수평 판넬로 — Play 비교 후 결정).
- `CreateTavernWoodFrame()` — tavern_wall_timber(+노멀) 세로 결(기존 CreateWoodFrame의 주점판 — 기둥/들보/문틀용).
- 전 메서드 null 폴백 계약(파일 부재 시 기존 HQ 경로 — 크래시 금지).

### T3. IndoorSceneTransition 배선 — buildingType 'arena' 분기
- ApplyHighQualityInterior의 arena(로비) 경로에서 `ApplyTavernToRoom(room, w, h, d)` 신설 호출 — 바닥/벽/골조 전면 주점 팔레트.
- arenapit 조기 반환 유지(실외 계약), 성 내부 buildingType은 기존 HQ 그대로(불변).
- 골조 배치: 로비의 기둥/들보 요소가 있다면 CreateTavernWoodFrame 연결 — 기둥이 없으면(IncludePillars=OFF 계약) 벽 상단 들보 밴드만 신설 여부는 Play 후 판정(과밀 배제 선호 계약 — 최소 변경).

### T4. 검증
- 컴파일 error CS 0 / EditMode 1608/1597/11 baseline 1:1.
- 재Play 판정: ①로비 바닥=오크 판자 가시(타일링 정합 — 이음새 없음) ②벽=회반죽+투톤 하부 ③골조 요소 목재 결 가시 ④성 내부 씬 무변화(기존 HQ 유지) ⑤투기장(외부) 무변화 ⑥로그: tavern 로드 6종+HQ tavern 경로.

## 비고
- 가구(GLB 차후 배치)는 본 사이클 제외 — GLB 도입 시 별도 계획.
- 전체 워킹트리 미커밋 유지. Play/커밋은 별도 승인 게이트.
