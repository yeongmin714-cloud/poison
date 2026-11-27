# 74차 계획 — NPC/병사 대화 패널 피그마 163:23/187:14 정합 + 병사 대화 연결 + 패널 대상 앞 배치

> 작성: 2026-10-15. 사용자 요구: "대화도 dialog 패널이 피그마에 있어 그 패널대로 정합 시켜줘".
> 해석: NPCDialoguePanelUTK(현재 440×124/143 구조)를 피그마 npc-dialogue-panel 진실치로 정합하고,
> **병사 대화도 이 패널로 연결**(73차 말풍선 유지 + 카메라 확대 유지), 패널 개방 위치도 대상 앞으로.

## Figma 진실치 (163:23 대화 전용 / 187:14 선택지 포함 — 2026-10-15 실측)

공통:
- 패널 **528×148.8(대화)** / **528×171.8(선택지)** — bg **#151B21/45%** radius 9.6, 테두리 없음
- 코너 데칼 — 패널 밖(-4.8) 코너 L자 9.6(선 7.2) — 현재 AddCornerDecal(inset 4/arm 8)과 유사, 위치만 진실치(-4.8)
- NpcNameRow (15.6,15.6) 496.8×15 — **npc-indicator 원 7.2×7.2 #33E5D8** + NpcName 13.2/700 **#33E5D8**
- Separator (15.6,40.2) 496.8×1.2 **#2F363C/60%**
- DialogueContent (15.6,51) — DialogueLine1 15.6/**500** #EAEDEF / DialogueLine2 15.6/400 #8B949E
- ActionRow (15.6,103.8) — **ContinueHint "계속하려면 클릭 ▶" 12/500 #57A6FF 우측(x 416.4)** — 클릭 대상(닫힘 힌트)
- 선택지(187:14) ChoiceGrid (15.6,127.4) 496.8×28.8 — **ChoiceCard 159.2×28.8 3열**(x 15.6/184.4/353.3,
  gap 9.6) bg **#FFFFFF/3%** radius 7.2 — "선택 N" 12/700 #58A6FF + 본문 13.2/500 #EBEDF0

## 현재 코드와의 격차 (실측)

| 항목 | 현재 | 진실치 |
|---|---|---|
| 창 크기 | 440×124/143 | 528×148.8/171.8 |
| bg | #161B22/95% + 테두리 1px | **#151B21/45% 테두리 없음** |
| 인디케이터 | 골드 #E3B341 원 6px | **#33E5D8 원 7.2** |
| 이름 색 | #F0F6FC 12px | **#33E5D8 13.2/700** |
| 대사 폰트 | 12px #D8DEE8 | **15.6 500 #EAEDEF / 15.6 400 #8B949E** |
| ContinueHint | #8B949E 12/400(클릭 닫힘) | **#57A6FF 12/500 우측 정렬** |
| 선택 카드 | 48%폭 2열 #21262D radius 6 | **159.2 고정 3열 #FFFFFF/3% radius 7.2** |
| 개방 위치 | 미지정(좌상단) | **대상 바로 앞**(72차 PlaceAtTarget 계약 동일) |

## Phase 1 — NPCDialoguePanelUTK 정합 (수리)

파일: `Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs`.
- 크기/색/폰트/레이아웃 전부 위 표로 교체(진실치 하드코딩 — 계약 주석).
- 선택 카드 3열 159.2×28.8(#FFFFFF/3% radius 7.2) — "선택 N" 12/700 #58A6FF + 본문 13.2/500 #EBEDF0.
- ContinueHint 우측 정렬 #57A6FF 12/500 — 클릭=닫힘(기존 계약 유지).
- bg #151B21/45% radius 9.6 테두리 0 — 데칼은 기존 AddCornerDecal(톤만 #2F363C로 정합).
- ChoiceHeight/DialogueHeight 갱신(148.8/171.8).

## Phase 2 — 병사 대화 연결 (InteractionPanelUTK + 신규 오버로드)

- NPCDialoguePanelUTK에 병사 진입점 신설: `OpenForGuard(GuardPlaceholder guard)` —
  NpcName=guard.GuardName, DialogueLine1=guard.StatusMessage(현재 대사) / Line2=호감 단계 안내문
  (GuardLoyaltySystem.GetAffinityGradeName — 실측), 선택지 3 = 피그마 DefaultChoices 계약(병사 대화 선택지 —
  "안녕하세요"/"요즘 어떻게 지내세요?"/"일해라 병사!") 후 선택 시 말풍선(StatusMessageChanged)로 응답.
- InteractionPanelUTK 병사 대화 라우팅(446-455행): DialogueCameraDirector.Begin 유지 + **NPCDialoguePanelUTK
  OpenForGuard** 추가(카메라 확대+대화 패널+말풍선 동시) — 73차 말풍선은 유지(대사 자체는 패널이 주 표시).
- NPC 대화도 Open 시 배치: NPCDialoguePanelUTK에 PlaceAtTarget(guard/npc transform — 대상 위 0.9m, 1s 폴링
  추적은 InteractionPanelUTK 72차 패턴 재사용 — 패널 자체 폴링 최소화: 개방 시 1회 배치+이동 없음(대화는 정지 상태
  — 카메라 확대 중)).

## Phase 3 — TDD

- NPCDialoguePanelFigmaTests(신규 — 소스 어서션): ①진실치 마커(528/148.8/171.8/#151B21/45%/9.6/#33E5D8/13.2/
  15.6/#EAEDEF/#8B949E/#57A6FF/#FFFFFF/3%/159.2/7.2/계속하려면 클릭 ▶) ②OpenForGuard 진입점+호감 단계 안내 마커
  ③PlaceAtTarget 배치 마커 ④InteractionPanelUTK 병사 대화 라우팅(OpenForGuard) 마커.
- 기존 DialogueFeedbackTests/GuardStatusPanelTests 등 회귀 0 유지.

## 공통

- 컴파일 error CS 0 ×2회 / EditMode 전수(898/887/11 유지 — 신규 회귀 0).
- Play 게이트: ①NPC/병사 대화 패널 = 피그마 진실치(시안색 인디케이터+이름/15.6 대사/3열 선택 카드/우측 파란 힌트) ②패널이 대상 앞 개방 ③병사 대화 = 카메라 확대+패널+말풍선 ④선택 카드 클릭 → 대사 표시 ⑤기존 기능 무사고.
- stage/commit/push는 사용자 승인 후.
