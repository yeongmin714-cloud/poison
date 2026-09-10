# AAA급 인벤토리/장비창 4-레이어 재설계 계획

- 날짜: 2026-09-10
- 범위: InventoryWindow.cs + EquipmentWindow.cs (둘 다 IMGUI/OnGUI 기반 — 기존 파이프라인 유지가 안전)
- 원칙: 데이터/상호작용 로직 무변경 (픽업·정렬·탭·우클릭장착·드래그·퀵슬롯 연동 전부 유지), 렌더링 계층만 4-레이어로 재구성

## 현황 진단

- InventoryWindow(1794줄): 단일 평면 GUI.Box + DrawColoredRect 1~2px 테두리 + 1x1/절차 텍스처. 레이어 개념 없음.
- EquipmentWindow(441줄): 동일 단순 패널.
- 이미 존재하는 강점: 절차적 텍스처 생성기 3종(MakeTexture/MakeBorderedTexture/MakeRoundedBorderedTexture — SDF 라운드 가능), ItemRarity 5단계(Common/Uncommon/Rare/Epic/Legendary), GUI.skin.box border(RectOffset) 9-Slice 경로, EquipmentWindow 6슬롯 구조.
- **핵심 기법**: AAA 프레임을 **절차적 SDF 텍스처 생성**(코드로 프레임/모서리 장식/금속 그라디언트/노이즈 텍스처 합성)으로 구현 — 외부 에셋 의존 0, LFS/커밋 부담 0, 스타일 일관성. 생성 시점은 첫 OnGUI 전 지연 1회.

## 공통 기반 — 신규 InventoryArtLibrary.cs (static, ProjectName.UI)

모든 절차적 아트 텍스처를 한 곳에서 캐시 관리:
1. **9-Slice 베이스 패널** (Layer 1): 256x256, SDF 라운드 코너 + 2픽셀 안쪽 그라디언트(상단 하이라이트/하단 어둡게) + fBm 노이즈로 다크 스톤 질감 + 중앙 음각 마법진(동심원+방위 룬 틱, 대비 4% 이하 은은하게). GUI.skin.box 스타일 border 24로 구등분.
2. **금속 프레임 스프라이트** (Layer 4): 256x256, 프레임 두께 ~14px — 금속 그라디언트(하이라이트→베이스 금색→섀도우) + 노이즈 스크래치 + 내측 1px 다크 아웃라인 + 외측 드롭섀도우(알파 감쇠 8px). border RectOffset(16)로 구등분 — 임의 크기 프레임.
3. **코너 장식 4종** (Layer 4): 96x96 — SDF 곡선 필리그리(2개 스케일의 로터스 곡선 + 점 대칭) + 금색 그라디언트. 4모서리 회전 배치(다른 프레임과 독립 스프라이트).
4. **타이틀 배너** (Layer 4): 512x96 — 중앙 볼록한 금속 배너 + 좌우 테이퍼 + 양끝 리벳(원형 스페큘러 점).
5. **슬롯 셀 3종** (Layer 2): 72x72 — ①인너섀도우 엠보싱(상단 좌측 어둡게/하단 우측 밝게 반전 그라디언트 = 움푹 파인 느낌) ②테두리는 희귀도 색으로 GUI.color 틴트(흰색 테두리 베이스에 tint). ③빈 슬롯용 심플 버전.
6. **희귀도 후광/테두리** (Layer 2/3): 72x72 글로우 스프라이트(방사형 falloff) — 희귀도 색(Common=회백/Uncommon=녹/Rare=청/Epic=보라/Legendary=금)으로 tint. 슬롯 하이라이트 프레임(호버 밝은 테두리)도 동일 라이브러리에서.
7. 공용 색 팔레트 static readonly + 노이즈 함수(private, 결정론 시드 — Random 언시드 금지 규약).

## InventoryWindow 재설계 (데이터·로직 유지, 렌더링 교체)

- **유지**: ItemCategory 탭, 정렬 5종, 슬롯 선택/호버/우클릭 장착, 퀵슬롯/드래그 API(GetSelectedItemData 등), 상세 정보 패널 내용, EquipmentStatBonus 연동.
- **레이어 구조(OnGUI 드로우 순서 = z-order)**:
  - Layer 1 Backplate: 9-Slice 스톤 패널 전면 + 상단에 은은한 방사 비네트(어둡게 가장자리).
  - Layer 2 Grid&Slot: 슬롯마다 인너섀도우 셀 텍스처 + 희귀도별 글로우(전설=골드 글로우가 슬롯 배경 뒤에) + 희귀도 테두리 tint.
  - Layer 3 Content: 아이콘(GUI.DrawTexture — 기존 icon Sprite → texture 변환 유지) + 수량/내구도 텍스트 + 호버 하이라이트 프레임 + 장착 아이템 ✔/프레임 강조 — 기존 셀렉션/호버 rect 계산 유지하되 프레임 스프라이트만 교체.
  - Layer 4 Frame&Ornaments: 금속 프레임(구등분) + 4모서리 장식(각 96px, 회전) + 타이틀 배너(기존 제목 텍스트 유지) + 탭 바를 배너 하단 프레임과 일체화.
- 깊이감 보강: 창 전체 뒤에 소프트 드롭섀도우 Rect(4방향 확장 그라디언트, DrawTexture falloff) — 화면 대비 팝업감.

## EquipmentWindow 동일 시스템 적용

- 같은 InventoryArtLibrary로 6장비 슬롯(Layer 2) + 프레임/코너/배너(Layer 4) 통일 — 인벤과 한 세트 비주얼.
- 장비 슬롯은 희귀도 대신 "장착 중" 골드 글로우.

## 구현 파일

| 파일 | 작업 |
|---|---|
| Assets/Scripts/UI/InventoryArtLibrary.cs | 신규 — 절차 텍스처 생성/캐시 (내부 static class, UI asmdef) |
| Assets/Scripts/UI/InventoryWindow.cs | OnGUI 렌더링 파트 재구성(4레이어), 스타일 필드 일부 교체 — 데이터/로직 유지 |
| Assets/Scripts/UI/EquipmentWindow.cs | 동일 4레이어 적용 |

## 검증

- 배치컴파일 CS=0
- QA: diff 리뷰 + 플레이 요구사항 매핑 검증(레이어 분리/9slice/엠보싱/희귀도 글로우/코너 장식/배너)
- Play 판정: 인벤토리(I)·장비창 스크린샷 → vision_analyze로 4레이어 시각 검증
- 3곳 저장(QAPROGRESS/메모리/커밋푸시)

## 리스크

- 절차 텍스처 품질 한계 → SDF+노이즈 파라미터로 최대한, 실패 시 마무리는 다운스케일 블러 대신 알파 falloff 조정으로 커버
- IMGUI 성능: 121슬롯 글로우 DrawTexture 다중 호출 → 텍스처 캐시 필수(프레임마다 생성 금지), 필요시 글로우는 아이템 보유 슬롯만
- 레거시 한글 폰트 이슈는 기존 그대로(범위 밖)
