# UI8 헤더 정합 + HUD 게이지 폼 + 키 재배선 + 실내 텍스처 교체 계획 (2026-10-09)

트리거: 사용자 보고 4건 (창고 ui8 캡처). **계획 승인 후 실행** — 본 문서는 계획만 포함.

## 0. 진단 (실측)

### 0-1. 클러스터 3창 헤더 불일치 (ui8 캡처 vision 실측 + Figma 15:4 spec 대조)
- **캡처 실측:** 인벤/상세 제목은 크고, **창고 제목은 60~70% 크기**, 좌우 여백은 창고가 가장 좁고 상세가 가장 넓음, 상세 제목 y가 인벤/창고보다 낮음 — 3창 헤더가 각자 다른 스타일.
- **Figma 15:4 진실치 — 3창 전부 동일 스타일:**
  - Title: **24/700 #F0F6FC**, 패널 로컬 (x=24, y=29.2) — 인벤(168-144)·창고(1296-1272)·상세(696-672) 전부 좌여백 24 동일
  - Subtitle(영문 병기): **14.4/400 #8B949E**, 제목 우측 +12px (297=168+117+12 / 1353=1296+45+12 / 803=696+95+12)
  - 창고 BadgeText "보관 가능": 14.4/400 #F0F6FC 로컬 (33.6, 105.6) / SlotCapacity: 14.4/400 #8B949E
- **결론:** 폰트 크기·여백·y위치 3요소를 Figma 진실치로 3창 일괄 통일 — 창고가 가장 큰 편차.

### 0-2. HUD 게이지 폼 격차 (23:6 spec vs 현재 HUDUTK)
- **현재(3층):** 링 텍스처 배경(UI/HudGaugeRing) + UTKCircularGauge 단색 arc + 중앙 아이콘 68.8@34.4.
- **Figma 23:6(7층 스택):** bg-radial-glow(264 뒤광) / outer-track(#FFFFFF@0.08 fill + stroke 1.32) / **outer-progress-glow(그라데이션 arc #FFB03A→#FF5E03)** / progress-tip(흰 끝점) / **inner-plate(96.8 흰 원 @20.4 + stroke 1.32)** / inner-plate-rim-highlight(88.9 stroke 1.98) / center-light(68.8) / icon.
- **원칙:** 레이어 구조·glow·plate는 Figma 채택, **시맨틱 색은 게임 매핑 유지**(HP 레드/스타미나 옐로 — 09-24 사용자 설계 계약). progress-glow를 게이지색 그라데이션으로 치환.

### 0-3. L키 이중 발동 원인 확인 완료
- L → `CombatLogUTK.cs:270`(전투로그) **+** `UI/EncyclopediaWindow.cs:38 _toggleKey=KeyCode.L`(도감) — **두 배선이 동시 발동** (버그성 중복).
- J → `QuestJournalUTK.cs:559`(퀘스트 저널) + legacy `QuestJournalUI.cs:139`.

### 0-4. 실내 텍스처 로드 경로 확정
- `Assets/Resources/Indoor/`에서 로드(IndoorTextureLoader, 파일명 고정): floor_flagstone.png / wall_stone_lower.png / wall_plaster_upper.png / wood_frame.png (+_n 노멀맵 선택).
- 벽 UV = 2.2×1.1m 커버 유닛, 바닥 = 2.4×2.4m 커버(mainTextureScale 자동 계산) — **PNG 교체만으로 전 실내 빌더 자동 적용**.

## 1. Phase A — 클러스터 3창 헤더 Figma 통일
1. WarehouseWindowUTK / ItemDescriptionWindowUTK / InventoryWindowUTK 헤더 3종을 진실치로 통일: Title 24/700 #F0F6FC 로컬(24,29.2) + Subtitle 14.4/400 #8B949E 제목 우측+12. 상세의 y어긋남·창고의 작은 제목·여백 편차 전부 해소.
2. 창고 BadgeText/SlotCapacity 위치·색 확인(로컬 33.6,105.6 / 14.4).
3. 게이트: 컴파일 0 + Figma focused 배치 + 캡처 승인(ui9). 커밋 단위 1건.

## 2. Phase B — HUD 게이지 Figma 23:6 폼 완성
1. 게이지 레이어 스택 확장: 뒤광 glow(게이지색 틴트) → outer-track → progress-glow arc(게이지색 그라데이션+tip) → inner-plate 흰 원 → rim-highlight → 중앙 아이콘. 크기 계약(137.6/96.8/88.9/68.8)은 Figma 그대로.
2. 시맨틱 색 유지: HP=레드 계열 그라데이션, 스태미나=옐로 계열 그라데이션 (Figma 목업은 스태미나 오렌지지만 09-24 사용자 색 계약 우선 — 스펙과 충돌 시 사용자 확인).
3. HudBottomAnchorTests에 3층 계약 단정 있음 → 7층 계약으로 갱신.
4. 게이트: 컴파일 0 + focused + 캡처 승인. 커밋 단위 1건.

## 3. Phase C — 키 재배선 (사용자 제안 채택 — 합리적: L 이중발동이 근본 원인)
1. **J = 도감**(EncyclopediaWindowUTK 토글), **L = 전투로그 단독**.
2. J 퀘스트 저널 제거(QuestJournalUTK jKey 해제 — Q 퀘스트 창 통합 유지), legacy QuestJournalUI jKey 제거, legacy EncyclopediaWindow KeyCode.L 제거(UTK 위임 — 원본 비수정 원칙이면 래퍼 게이트로 차단).
3. 게이트: 컴파일 0 + 키 배선 소스계약 테스트(신규) + Play 확인. 커밋 단위 1건.

## 4. Phase D — 벽 스타일 개편: 회반죽 전면 + 팀버 프레임 (2026-10-09 사용자 제안, 실내씬 예시.png 참조 확정)
**참조 실측:** 실내씬 예시.png = 하프 팀버 — 베이지/오프화이트 회반죽 전면(하단 석재 소량 노출), 짙은 갈색 수직 목재 기둥(벽 중간+모서리), 천장 연결부 두꺼운 보, 대각 지지대, 다크 우드 문틀, 석판 바닥, 웜 로우키 조명.
**진단(기존):** TryCreateWall 하부 = 절차 ashlar(제공 wall_stone_lower 미사용). 상부 회반죽 텍스처는 로드만 되고 플레이어 성 토폴로지 벽엔 미적용.
1. **D1 벽 전면 회반죽:** 토폴로지 벽 전체에 `wall_plaster_upper.png` 적용(이미 로드된 미사용 파일) — 하부 석재 밴드 폐지, wall_stone_lower는 벽에서 제외. (하단 석재 소량 노출은 옵션 — 캡처 피드백 후 결정)
2. **D2 틀 목재화:** 기존 문설주/린텔(10-04 렌더러 전용)+StonePillar+trim 계열 머티리얼을 `wood_frame.png`(세로 나뭇결)로 교체 — 프레임별 UV 스케일(2.2×1.1 벽 유닛 아님).
3. **D3(선택) 팀버 포스트 신규 지오메트리:** 벽 중간 수직 기둥 + 천장 연결 상단 보(+대각 지지대) — 렌더러 전용(무충돌) 배치, 문설주 선례 패턴. 스코프 커서 별도 승인 항목.
4. 새 PNG 사양(기존 안내 유효): 벽 회반죽 1024×512(2.2×1.1m, 수평 심리스) / wood 512×1024(세로 나뭇결) / 바닥 1024×1024(2.4m). 투입 경로 Assets/Resources/Indoor/.
5. 게이트: 컴파일 0 + 성 내부 테스트군 + 캡처 승인.

## 5. 실행 순서/게이트
- A(헤더 통일) → B(HUD 폼) → C(키 재배선) 순서, 단위별 컴파일 0 + focused 테스트 + 커밋. D는 PNG 투입 즉시 처리.
- 전 단계 시각 승인은 캡처 게이트. push는 지시 시.