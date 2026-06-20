# 🎮 포이즌 (Poison) — PROGRESS

> **이 문서는 지금까지 완료된 작업과 앞으로 할 작업을 자동 기록합니다.**
> Hermes가 작업 완료 시마다 **자동 업데이트**합니다.
> ✅ = 완료, ⬜ = 미완료, 🔄 = 진행 중

---

## ✅ Phase 0: 🏗️ 기반 공사 (완료)

|| 항목 | 설명 | 상태 |
|------|------|:----:|
| Unity 6000.4.10f1 설치 | Unity Hub → 6000.4.10f1 | ✅ |
| 프로젝트 폴더 구조 생성 | Assets, Scripts, Tests 등 | ✅ |
| Git 설정 | .gitignore, .gitattributes | ✅ |
| Packages | URP 17.4.0, Cinemachine 3, Input System | ✅ |
| .NET SDK 8.0 | C# 컴파일 도구 | ✅ |
| GameManager.cs / PersistentManager.cs | 기본 스크립트 | ✅ |
| TDD 인프라 | run_tests.sh, asmdef (Core/EditMode/PlayMode) | ✅ |
| Windows 빌드 테스트 | Game.exe 40MB, 성공 | ✅ |
| CI 워크플로우 | GitHub Actions 자동 테스트 | ✅ |

---

## ✅ Phase 1: 🎮 3인칭 플레이어 조작 (완료)

|| 항목 | 설명 |
|:----|:------|
| 500×500 초록색 평지 | URP Lit 머티리얼, 환경 기둥 10개 |
| PlayerMovement.cs | WASD + Shift 달리기 + Space 점프 + 카메라 기준 이동 |
| Cinemachine 3인칭 카메라 | CinemachineCamera + ThirdPersonFollow + InputAxisController |
| 충돌/중력 처리 | CharacterController 자동 처리 |
| PlayerPlaceholder | Unity 기본 도형으로 사람 모양 (파란 옷+살색) |
| TDD | PlayerMovementTests 6개 + UITests 11개 = **17개 테스트 통과** |

---

## ✅ Phase 1.5: ⌨️ UI 프레임워크 & 키 설정 (완료)

|| 항목 | 설명 |
|:----|:------|
| UIManager.cs | 싱글톤, 키 입력 감지, 윈도우 스택 관리, ESC 닫기 |
| UIWindow.cs | 추상 베이스 클래스 (Show/Hide/Toggle) |
| KeyBindings.cs | ScriptableObject, Q/R/I/M 키 변경 가능 |
| QuestWindow.cs | Q 키 → 퀘스트 창 (내용은 Phase 2에서) |
| RecipeWindow.cs | R 키 → 레시피 창 (내용은 Phase 4에서) |
| InventoryWindow.cs | I 키 → 인벤토리 창 (내용은 Phase 2에서) |
| MapWindow.cs | M 키 → 지도 창 (내용은 Phase 3에서) |
| TDD | UITests 11개 통과 |

---

## ||| Phase 1.6 | ⚔️ 마우스 클릭 공격 & 전리품 시스템 | ✅ |
## ||| **Phase 1.6 세부** | 🎥 카메라 무빙 + 💥 공격이펙트 + 💢 타격반응 | ✅ |

|| 항목 | 설명 | 상태 |
|:----|:------|:----:|
|||| 마우스 좌클릭 공격 | Raycast 적 감지 → 데미지 | ✅ |
||||| 근접 공격 범위 판정 | SphereCast/OverlapSphere 기반 적 감지 | ✅ |
||||| IDamageable 인터페이스 정의 (TakeDamage) | ✅ |
||||| 몬스터/병사에 IDamageable 구현 | ✅ |
||||| 무기별 공격 속도/쿨다운 시스템 | ✅ |
||||| 데미지 계산 (무기 공격력 + 레벨 보정) | ✅ |
|||||| 적 사망 시 LootBasket 생성 | ✅ |
||||| 자동 조준 (C4-08) | 마우스 커서 Raycast → SphereCast fallback, 사망/이탈 자동 해제 | ✅ |
||||| LootBasket 시각 모델 (자루/가방 placeholder) | ✅ |
|||| 전리품 주머니 | 사망 시 LootBasket 드랍 | ✅ |
|||| LootWindow 활용 | 주머니 클릭/E키 → LootWindow (개별 or 전부 획득, ILootBasket 연동) | ✅ |
||||| 몬스터 드랍표 | 티어별 고기/재료 드랍 (Early/Mid/Late 3종) | ✅ |
|||| 병사 드랍표 | 등급별 금화/장비 + 희귀 5% (SoldierDropTable) | ✅ |

---

## ✅ GLB 자동 교체 시스템 (완료)

|| 항목 | 설명 |
|:----|:------|
| PlayerPlaceholder.cs | 씬에 파란 사람 모양 생성 |
| PlaceholderSetup.cs | Placeholder 일괄 생성 Editor 스크립트 |
| ModelSwapper.cs | GLB → Placeholder 자동 교체 (batchmode) |
| ModelMapping.cs | GLB 파일명 ↔ 게임오브젝트 매핑표 |
| GLB Watcher cronjob | 5분 간격 감시 → 발견 시 자동 교체 |
| GLB 투입 폴더 | `Assets/Resources/Models/UserProvided/` |

---

## 📚 문서 파일 (모두 저장 완료)

|| 파일 | 내용 |
|:----|:------|
| `ROADMAP.md` | 전체 개발 계획 (Phase 0~9) |
| `PROGRESS.md` | **👈 이 파일 — 진행 상태 체크리스트** |
| `docs/GAME_DATA.md` | 약초5종, 몬스터7종, 조합법18종, 요리12종 |
| `docs/ASSET_LIST.md` | 필요한 3D 에셋 전 목록 (Phase별) |
| `docs/TDD_WORKFLOW.md` | 테스트 주도 개발 설명서 |
| `.hermes/project_memory.md` | Hermes 참고 메모 |
| `계획.txt` (원본) | `/mnt/c/Unity/director/계획.txt` |

---

## ||| Phase 2 | 🏠 튜토리얼 — 추방지 허름한 집 | ✅ |

|| 부분 | 내용 | 상태 |
|:----|:------|:----:|
| 환경 | 집(외부+내부)+마당, 나무/돌 20개 배치 (placeholder) | ✅ |
| 크래프트 테이블 | 집 안 배치 + CraftingStation.cs (E 키 상호작용 기반) | ✅ |
| 약초 채집 | 약초 5종×3개씩 배치, HerbPickup.cs (E 키 채집, 리스폰 30초) | ✅ |
| 사냥 | 토끼×2(도망 AI)+멧돼지(돌진 AI)+늑대×2(추격 AI) | ✅ |
| 인벤토리 | PlayerInventory.cs (카테고리별 저장) + InventoryWindow GUI (I 키) | ✅ |
| NPC 퀘스트 | 영주 NPC + 대화 스크립트 (E 키 대화→음식→독→영지 증서) | ✅ |
| NPC 대사 문서 | docs/NPC_DIALOGUES.md 생성 (사장님이 직접 수정 가능) | ✅ |
| 영주 애니메이션 | Idle/Eat/Poisoned/Death — 코드 기반 타이머 (GLB 애니메이션 필요) | 🔄 |
| 몬스터 애니메이션 | 코드 이동 기반 (GLB 애니메이션 필요) | 🔄 |
| 채집 모션 | 코드 기반 (애니메이션 GLB 미제공) | 🔄 |
| 플레이스홀더 GLB | 16종 GLB placeholder 생성 (집/제작대/약초/몬스터/NPC/포션/북) | ✅ |
| **Phase 2** | 🏠 튜토리얼 — 추방지 허름한 집 | 🔄 |

|||| Phase 3 | 🌍 세계관 & 월드맵 (4방위×20영지 + 황제국 👑 = 81영지, 방사형 난이도) | 🔄 |
|||| **Phase 3.3** | 📋 81개 영지 데이터 정의 + 황제국 접근 규칙 | ✅ |
|||| **Phase 3.4** | 🏁 국기 & 영지 소유 표시 시스템 — NationFlagDatabase+FlagManager+FlagPoleDisplay+PlayerFlagRegistrationWindow, 57개 테스트 | ✅ |
|||| **Phase 3.5** | 🗺️ 월드맵 UI & 국기 표시 — MapWindow(IMGUI,2단계줌,5지역×20영지,국기,안개), FlagPoleDisplay.FadeTransition, 20개 테스트 | ✅ |
|||| **Phase 3.6** | 🎨 지형 그래픽 개선 & 환경 조형물 — Skybox+NationTerrainController+WaterBody+Phase36_EditorSetup, 40개 테스트 | ✅ |
| Phase 3.8 | 📊 캐릭터 스테이터스 & 레벨업 시스템 | ✅ (구현 완료) |
| Phase 3.9 | 🏔️ Poly Haven 3D 모델 8종 배치 (245개) | ✅ |
| Phase 3.10 | 🟤 Poly Haven 지형 텍스처 3종 적용 (3링) | ✅ |
|    └ 3.6.1 지형 텍스처 | Procedural 잔디 텍스처 + URP Lit 머티리얼 적용 | ✅~40% |
|    └ 3.6.2 환경 조형물 | 나무30+바위20+풀50+덤불20+꽃15 랜덤 배치 | ✅~70% |
|    └ 3.6.3 조명 & 분위기 | Directional Light 튜닝 + 안개 + Global Volume + Post-processing 7종 | ✅✅ Phase 3C 완료 |
|    └ 3.6.4 경계 오브젝트 | 맵 4면 경계 표시 (반투명 물) | ✅~50% |
|    └ **Phase 3.9 — Poly Haven 3D 모델** | **✅ 245개 배치 완료** |
|        └ 🌲 전나무 25그루 | fir_tree_01_1k.gltf | ✅ |
|        └ 🌲 자카란다 25그루 | jacaranda_tree_1k.gltf | ✅ |
|        └ 🌲 작은나무 25그루 | tree_small_02_1k.gltf | ✅ |
|        └ 🪨 둥근바위 30개 | boulder_01_1k.gltf | ✅ |
|        └ 🪨 납작바위 30개 | namaqualand_boulder_02_1k.gltf | ✅ |
|        └ 🪨 절벽바위 30개 | namaqualand_cliff_02_1k.gltf | ✅ |
|        └ 🌿 작은식물 40개 | periwinkle_plant_1k.gltf | ✅ |
|        └ 🌿 관목 40개 | searsia_lucida_1k.gltf | ✅ |
|    └ **Phase 3.10 — Poly Haven 지형 텍스처** | **✅ 3링 적용** |
|        └ 🟤 중앙 0~350m | brown_mud_leaves | ✅ |
|        └ 🪨 중간 350~700m | rocky_terrain | ✅ |
|        └ 🏖️ 외곽 700~1000m | coast_sand_rocks | ✅ |
|    || Phase 3.7 | 🧱 장애물 & 벽 충돌 시스템 | ✅ |
|    || **Phase 0.5** | 🤖 Animation Rigging 기반 애니메이션 시스템 | ⬜ |
||    || Phase 4 | 🧪 크래프트 & 레시피 시스템 (80종조합+방독면+색안개+폭탄+가스분사기) | ✅ Phase 4 완료 |
||    || Phase 5 | 🏰 첫 번째 영지 & 부하 관리 (영지활동/창고/장비/방문/상점/성당/밀매/영주대면) | ✅ Phase 5 완료 |
||    || Phase 6 | 🚪 실내외 맵 전환 시스템 (IndoorSceneTransition + BuildingTrigger + FadeManager + 6종 InteriorBuilder) | ✅ Phase 6 완료 |
||    || Phase 7 | ⚔️ 전쟁 & 영지 점령 (AIWarSystem+AlarmSystem+LordSurrender+TerritoryCapture+WarNotificationUI) | ✅ Phase 7 완료 |
|| Phase 25 | 🍺 선술집 & 용병 시스템 | ✅ |
|| Phase 26 | 📊 병사/용병 스탯창 & 장비 지급 | ✅ |
|| Phase 27 | 💀 병사 사망 & 부활 개선 | ✅ |
|| Phase 28 | 🧛 드라큘라 영지 & 야간 컨텐츠 | ✅ |
|| Phase 29 | 💎 동굴 보석 상자 & 광석 | ✅ |
|| Phase 30 | 👑 영주/용병 이름 & 국가명 | ✅ |
|| Phase 31 | 🏴 영지 점령 상징 교체 | ✅ |
|| Phase 9 | 🧪 테스트 & 배포 | ⬜ |

---

| ## 📌 사장님 추가사항 31건 → 자동 반영 완료

1. **4방위(동서남북) × 각 20영지 + 중앙 황제국 👑 = 81영지** → Phase 3
2. **독약 사용 시 색안개 VFX + 방독면 시스템** → Phase 4.7~4.8
3. **건물 출입 시 실내외 맵 전환** → Phase 6
4. **Q/R/I/M 키 + 4개 UI 윈도우** → Phase 1.5
5. **황제국 탈환이 최종 목표** → Phase 3
6. **방사형 난이도 (외곽→중앙 갈수록 어려움)** → Phase 3.1
7. **병사 포섭 시스템 + 장비 내구도 + 병사 명령** → Phase 5.3.7~5.3.9
8. **몬스터 레벨 시스템** → Phase 5.3.5
9. **플레이어/병사 부활: 최대체력 10%, 가까운 영지에서 부활** → Phase 3.8h~3.8i
10. **병사 상호작용: 말걸 때만 활성화 + 음식주기/약주기** → Phase 5.3.1
11. **약초 리스폰 시간 게이지 표시** → Phase 2.3
12. **영지 활동(크래프팅/창고/장비) + 타영지 방문(상점/성당/밀매/영주대면)** → Phase 5.6~5.7
13. **마우스 좌클릭 공격 + 전리품 주머니 + 병사/몬스터 드랍** → Phase 1.6
14. **국가별 국기 + 플레이어 국기 등록 + 영지 소유 변경 시 국기 교체** → Phase 3.4
15. **💣 폭탄 아이템 + 던지기 모션** → Phase 4.9 (신규)
16. **🌿 풀/나무 흔들림 애니메이션** → Phase 3.6.5 (신규)
17. **🏁 국가마다 지형 텍스처 차별화** → Phase 3.6.7 (신규)
18. **🏰 성 내부 진입 씬 전환 + 바닥/벽 텍스처** → Phase 6.5 (신규)
19. **🌊 계곡 & 물 지형 배치 (Poly Haven 활용)** → Phase 3.6.6 (신규)
20. **🏁 영토 차지 애니메이션 + 국기 교체** → Phase 7.4 (신규)
21. **🗡️ 병사 무기 파츠 교체 시스템 (외형 변화)** → Phase 5.3.11 (신규)
22. **⛏️ 광부 역할 추가 (벌목/채석/철광/제련)** → Phase 5.3.9.7 (신규)
23. **🎭 방독면 재질 변경 (나무/돌/철 + 재료 수량 기반)** → Phase 4.8 (개정)
24. **📜 무기 조합법 문서** → `/mnt/c/Unity/director/무기조합법.md` (신규)
25. **🎬 Animation Rigging 기반 애니메이션 시스템** → Phase 0.5 (신규) — 사장님 GLB 모델 → Hermes가 직접 애니메이션 제작
26. **🎥 공격 시 카메라 무빙** (전진/흔들림/슬로우) → Phase 1.6 (신규)
27. **💥 공격 이펙트** (타격 Sparks/잔상/히트플래시/데미지폰트) → Phase 1.6 (신규)
28. **💢 타격당하는 애니메이션** (경직/넉백/에어본/스턴/다운) → Phase 1.6 + Phase 0.5 Rig (신규)
29. **🧛 드라큘라 영지 야간 전용** — 밤에만 출현, 스켈레톤 병사, 드라큘라 영주 보스, 점령 시 희귀 아이템 (신규)
30. **🏹 활 화살 발사 + 소모** — 활 선택 시 화살 발사 모션, 화살 인벤토리 소모, 발사체 궤적 (신규)
31. **🎉 조합 성공 시 환호 + 결과창** — 음식/장비/약물 조합 성공 시 환호 애니메이션 + 아이템 결과창 + 등급 표시 (신규)

---

## 📋 전체 프로젝트 파일 구조

```
/mnt/c/Unity/code/
├── Assets/
│   ├── Scenes/MainScene.unity          (85KB)
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── GameManager.cs
│   │   │   └── PersistentManager.cs
│   │   ├── Systems/
│   │   │   ├── PlayerMovement.cs       ← WASD 이동
│   │   │   └── PlayerPlaceholder.cs    ← 임시 사람 모양
│   │   ├── UI/
│   │   │   ├── UIManager.cs            ← 키+윈도우 관리
│   │   │   ├── UIWindow.cs             ← 윈도우 베이스
│   │   │   ├── KeyBindings.cs          ← 키 설정
│   │   │   ├── QuestWindow.cs          ← Q
│   │   │   ├── RecipeWindow.cs         ← R
│   │   │   ├── InventoryWindow.cs      ← I
│   │   │   └── MapWindow.cs            ← M
│   │   └── Utils/
│   ├── Editor/
│   │   ├── BuildTools.cs               ← 빌드
│   │   ├── SceneSetup.cs               ← 씬 생성
│   │   ├── Phase3_Setup.cs             ← Phase 3 씬
│   │   ├── PlaceholderSetup.cs         ← Placeholder 생성
│   │   ├── ModelSwapper.cs             ← GLB 자동 교체
│   │   ├── ModelMapping.cs             ← GLB 매핑표
│   │   └── UnityTestRunner.cs          ← 테스트 실행
│   └── Resources/Models/UserProvided/  ← 📥 GLB 투입!
├── Tests/EditMode/
│   ├── SampleTests.cs
│   ├── PersistentManagerTests.cs
│   ├── PlayerMovementTests.cs          ← 6개 테스트
│   └── UITests.cs                      ← 11개 테스트
├── docs/
│   ├── TDD_WORKFLOW.md
│   ├── ASSET_LIST.md
│   └── GAME_DATA.md
├── .hermes/project_memory.md
├── PROGRESS.md                         ← 👈 지금 이 파일
├── ROADMAP.md
└── run_tests.sh
```

/mnt/c/Unity/code/Builds/Windows/Game.exe  (40MB)
/mnt/c/Unity/hermes_director/               ← 메일박스+에이전트

---

## 🤖 백그라운드 에이전트 & 텔레그램 (cronjob)

| 이름 | 주기 | 역할 | 모델 |
|:----|:----|:------|:----:|
| code-agent | 10분 | C# 코딩 자동화 | 🔒 nemotron 고정 |
| qa-agent | 10분 | 테스트 자동화 | 🔒 nemotron 고정 |
| design-agent | 30분 | 문서 관리 | 🔒 nemotron 고정 |
| glb-watcher | 5분 | GLB 파일 감시 → 자동 교체 | 🔒 nemotron 고정 |
| **디렉터 (세션)** | — | 설계/명령/승인 | 🔷 **deepseek v4-flash** |

### 📱 텔레그램 연동
- **봇:** @dudals0714_bot
- **게이트웨이:** ✅ systemd 실행 중 (hermes-gateway.service)
- **수신:** 텔레그램 메시지 → Hermes 직접 명령 처리
- **발신:** 사이클 완료 시 자동 알림 + 다음 사이클 추천 + 변경사항 요약
- **사용법:** 봇 채팅에 명령 입력 (예: "C9-30 진행", "상황 알려줘")

---

> **현재 상태:** Phase 0~2 완료 ✅, Phase 3.5~3.10 ✅, **Phase 4 완료 ✅ (21/21)**, **Phase 5: C9-01~04 완료 ✅**
|> **Phase 11 실내 맵 진행:** ✅ **14/14 완료 (Phase 11 끝!) 🎉**
> **Phase 3.5 완료:** Top-Down 카메라 + 커서 시선 회전 + URP 할당
> **Phase 3.6 완료 항목:** ✓ Procedural 잔디 텍스처 ✓ URP Lit 재질 ✓ Directional Light 튜닝 ✓ 안개(Fog) ✓ Global Volume
> **Phase 3.9 완료:** ✅ Poly Haven 3D 모델 8종 (나무3/바위3/식물2) → 총 245개 배치
> **Phase 3.10 완료:** ✅ Poly Haven 지형 텍스처 3종 → 중앙/중간/외곽 3링 구역 적용
> **Phase 3.6 미완료:** 나무 3종 완성, 바위 3종 완성 (Poly Haven으로 대체 완료), Post-processing Volume 설정, 스카이박스 구성
> **Phase 4 완료:** ✅ C4-08 자동조준 + C4-12~15 LootWindow 연동 + C4-16~19 드랍테이블 + C4-20 카메라 무빙 + C4-21 타격 이펙트 완료 (21/21)
> **GLB 자산 (2026-06-13):** 사장님 GLB 36종 UserProvided/ 준비 완료 (Player_Rigged/몬스터20종Rigged/건물6종/도구2종/NPC/병사/약초4종) — Placeholder 생성 시 ModelSwapper 자동 교체

---

|||> **Phase 14 완료:** ✅ **10/10 사이클 완료 (복수명부 시스템)** 🎉
|||> **Phase 15 완료:** ✅ **3/3 사이클 완료 (병사 3단계 모델)** 🎉
|||> **v2.2:** ✅ **Phase 5, 7 완료 + 제작/요리/경험치 시스템** 🚀
|||> **Phase 25~31 완료:** ✅ **Phase 25(선술집/용병) + Phase 26(스탯창/장비) + Phase 27(사망/부활) + Phase 28(드라큘라) + Phase 29(보석상자/광석) + Phase 30(이름/국가명) + Phase 31(영지점령상징) = v3.0 🚀🎉** 🚀
||> **C10-01~04 완료:** 💡 아이템 툴팁 + 🐾 몬스터 어그로 + 🌿 약초 게이지 + 🌱 풀/나무 흔들림 (4사이클 완료 🚀)
||> **Phase 23 완료:** 🔄 몬스터 리스폰 — 기존 AnimalAI로 이미 구현 (Die() 버그 수정)
||> **Phase 24 완료:** 🏴 영지 병사 복원 — TerritoryBattleManager + GuardPlaceholder.Die 개선 (5사이클)
||> **🎬 Phase 3C — Post-processing 완료:** 🎨 Tools/Phase 3C - Setup Post Processing 메뉴 스크립트 생성 (197줄) + 15개 EditMode 테스트. ACES Tonemapping, Bloom(intensity=1.5), Color Adjustments(contrast=8, saturation=15), Vignette(0.35), WhiteBalance(-5), ShadowsMidtonesHighlights, DepthOfField(Gaussian) — Color Grading HDR 모드 전환 (URP Asset) ✅
|||> **🔊 Phase 8.3 — 사운드 시스템 완료:** 🎵 SoundManager 싱글톤 (DontDestroyOnLoad/3채널), AudioConfig + 8종 SoundClipData 절차적 사운드. 발소리/채집/제작/UI/전투 연동. 카테고리별 볼륨. 16개 EditMode 테스트 ✅
||| 2026-06-19 | **Phase 22-05~10 — 고급 지형 시스템 완료 🌍** | LakeGenerator(Perlin noise 호수), BiomeEffectController(늪/사막 이동저하+안개), CaveEntrance(동굴입구+E키), CaveInteriorBuilder(동굴내부+TorchFlicker), NationTerrainController.SmoothNationTransition(2초 lerp), PlayerMovement.SpeedModifier 추가, IndoorSceneTransition 동굴타입. 신규 파일 5개, 수정 3개, 24개 테스트 ✅
|

## 📋 세션 로그

| 일시 | 작업 | 상세 |
|:---:|:-----|:-----|
| 2026-06-14 | **C4-08 자동 조준** | PlayerCombat — 마우스 커서 Raycast + SphereCast fallback, CurrentTarget/HasTarget, 사망/이탈 자동 해제 |
| 2026-06-14 | **C4-12~15 전리품 시스템** | ILootBasket 인터페이스 생성, LootBasket API 강화 (TakeItem/TakeAll/IsEmpty/ItemCount/BasketName), LootWindow 연동 (E키→LootWindow→개별/전부획득), GuardPlaceholder 컴파일 에러 수정 |
| 2026-06-14 | **핫픽스** | Core/LootBasket.cs 중복 제거 (Systems/LootBasket.cs로 통합)

| 2026-06-15 | **컴파일 오류 대량 수정** | Cinemachine 3.x 네임스페이스(Unity.Cinemachine), DrugEffectSystem enum 충돌, PoisonVFX/ILootBasket/PlayerStatusWindow/GuardPlaceholder/ModelSwapper/CreateBombPrefabs 30+개 오류 해결 |

| 2026-06-15 | **C8-29 폭탄 4종 프리팹** | Explosive/PoisonGas/Smoke/Molotov 4종 프리팹 생성 |

| 2026-06-15 | **EditMode 테스트** | run_tests.sh All tests passed ✅ |
| 2026-06-15 | **C8-30 폭탄 던지기 시스템** | BombThrower Input System 전환, 플레이어에 BombThrower 부착, BombThrowerTests 11개 작성, All tests passed ✅ |
| 2026-06-15 | **C9-01 영지 데이터 구조** | TerritoryData.cs (NationType/TerritoryDifficulty/LordInfo/TerritoryId/TerritoryState), TerritoryDatabase 싱글톤 (81영지 체계), TerritoryManager 업데이트, TerritoryDataTests 20개 All passed ✅ |
| 2026-06-15 | **C9-02~04 영지 Placeholder 배치** | TerritoryBuilder.cs (자동 생성기), BuildingPlaceholder 7종(Shop/CraftHouse/Church/NPCHouse1~4), GuardPlaceholder 3명(동쪽 국가, Lv.1~2), GuardPlaceholder.SetGuardInfo() 추가, TerritoryBuilderTests 10개 All passed ✅ |
| 2026-06-15 | **C9-05 상점 시스템** | ShopPlaceholder Input System 전환 (E키), ShopWindow 판매 기능 구현 (SellSelectedItem, CalculateSellPrice), ShopWindowTests 11개 All passed ✅ |
| 2026-06-15 | **C9-06 상점 기본 아이템** | ItemCategory에 Weapon/Armor/Tool 추가, PlayerInventory에 무기3/방어구2/도구3 ItemData 추가, ShopWindow 재고 확장 (총 16종), ShopItemTests 12개 All passed ✅ |
| 2026-06-15 | **C9-07 떠돌이 상인** | WanderingMerchant.cs (랜덤 방문 타이머 2~5분 간격, 45초 체류, E키 상호작용, ShopWindow 연동), 희귀 재고 9종(요리3/포션3/재료2/레시피1), WanderingMerchantTests 9개 All passed ✅ |
| 2026-06-15 | **C9-08~09 병사 상호작용 & HUD** | GuardPlaceholder 대규모 개선: E키 상호작용, IMGUI HUD (레벨/호감도/중독도/체력 프로그레스바 + 4개 메뉴 버튼), Loyalty/Addiction 속성, JobTitle, GuardInteractionTests 15개 All passed ✅ |
| 2026-06-15 | **C9-10 병사 호감도 시스템** | GuardLoyaltySystem (국가 기반: 동일국+10/적대국-20, 선물+5~30, 약물+15/+중독, 위협+20/-30 보복, 태그), GuardLoyaltyTests 12개 All passed ✅ |
| 2026-06-15 | **C9-11 병사 음식/약 주기** | GuardPlaceholder 아이템 선택 팝업(IMGUI), 음식→체력회복+호감도, 약→체력회복+호감도, 마약→호감도+중독도, PlayerInventory.GetAllSlots/GetSlotsByCategory 연동, GuardGiveItemTests 12개 All passed ✅ |
| 2026-06-15 | **C9-12 병사 중독 시스템** | GuardAddictionSystem (6단계: 정상~과다복용, 전투력계수/행동오류확률/자연감소/해독제50%감소/독약호감도-10), GuardPlaceholder Update 연동, GuardAddictionTests 15개 All passed ✅ |
| 2026-06-15 | **C9-13 병사 레벨 시스템** | GuardLevelSystem (Lv.1~50, 영지난이도별 범위: Ring1=1~10, Ring4=26~40, Empire=41~50, 레벨당HP+10/공격+1/방어+0.5, 경험치체계 1.2배율), GuardLevelTests 22개 All passed ✅ |
| 2026-06-15 | **C9-14 몬스터 레벨 시스템** | MonsterLevelSystem (티어별 기본Lv: 초반1~5/중반6~15/후반16~30, 영지난이도보정 Ring3=+3/Empire=+10, HP/데미지/XP계산, 드랍률보정 Lv10당+5%, 티어추정), MonsterLevelTests 22개 All passed ✅ |
| 2026-06-15 | **C9-15 병사 포섭 시스템** | GuardRecruitSystem (4단계: 자동100%/일반70+/선물50+50%/위협0+20%, 실패시 호감도감소), GuardPlaceholder 포섭 버튼+IsRecruited 속성, GuardRecruitTests 15개 All passed ✅ |
| 2026-06-15 | **C9-16 병사 상태 체계** | GuardStatusSystem (GuardRole 5종, 일일사망률/활동보너스/전투가능여부, 상태요약 문자열), GuardPlaceholder Role 속성+StatusSummary, GuardStatusTests 18개 All passed ✅ |
| 2026-06-15 | **C9-17 장비 내구도 시스템** | ItemData.maxDurability + ItemSlot.currentDurability, 무기20/방어구30/도구30 내구도, ReduceDurability/Repair/색상태그(🟢🟡🔴)/수리비용/파괴감지, DurabilityTests 22개 All passed ✅ |
| 2026-06-15 | **C9-18 장비 내구도 UI** | InventoryWindow 무기/방어구/도구 탭 추가, 슬롯당 내구도 바(🟢🟡🔴), 툴팁 내구도 정보, 정보패널 내구도 표시, GetCategoryColor 신규 카테고리, DurabilityUITests 8개 All passed ✅ |
| 2026-06-15 | **C9-19 장비 수리 시스템** | EquipmentRepairSystem (재료소모: 무기=금속/방어구=가죽/도구=나무, 수리 버튼 인벤토리 UI, FindAndSelectSlot), RepairTests 12개 All passed ✅ |
| 2026-06-15 | **C9-20 RTS 기본 명령** | GuardSelectionManager (드래그 선택박스/우클릭 명령/H키중단), GuardPlaceholder RTS 메서드(SetSelected/SetCommandTarget/ClearCommand), RTSTests 10개 All passed ✅ |
| 2026-06-15 | **C9-21 전투 AI** | GuardCombatAI (NotifyPlayerAttack/UpdateGuardBehavior/RecallAll, 전투타이머/귀환/합세), GuardPlaceholder 전투상태(IsInCombat/CombatTimer), CombatAITests 12개 All passed ✅ |
| 2026-06-15 | **C9-22 Shift 선택** | GuardSelectionManager.SelectGuardsInRect(additive 파라미터), Shift키 감지 로직, RTSTests SelectGuardsInRect_WithAdditive 추가 ✅ |
| 2026-06-15 | **C9-24 특사 파견 시스템** | EnvoySystem (Gift/Friendship/Alliance/Assassinate 4종, 레벨제한, 발각확률 계산, 특사 사망 처리), EnvoyTests 14개 All passed ✅ |
| 2026-06-15 | **C9-25 정보원 파견 시스템** | SpySystem: 정찰/잠입/측량 3종 임무, 발각 확률, 정보 플래그, SpySystemTests 15개 All passed ✅ |
| 2026-06-15 | **C9-26 약초꾼 임무** | HerbGatheringMission (Herbalist 자동 채집, 1.5배 보너스, 거리/범위 제한), HerbPickup.TryAutoGather 추가, HerbGatheringTests 14개 All passed ✅ |
| 2026-06-15 | **C9-27 사냥꾼 임무** | HuntingMission (Hunter 자동 사냥, 1.5배 보너스, 거리/범위 제한), AnimalAI.TryAutoHunt 추가, HuntingTests 12개 All passed ✅ |
| 2026-06-15 | **C9-28 광부 임무** | ResourceNode 3종(Wood/Stone/IronOre), MiningMission 자동 채광 + 제련(2:1), MINER_MINE_BONUS 1.5배, MiningTests 15개 All passed ✅ |
|| 2026-06-15 | **C9-26~29 약초꾼/사냥꾼/광부/무기파츠** | HerbGatheringMission + HuntingMission + MiningMission + WeaponPartsSystem, All tests passed ✅ |
||
| 2026-06-15 | **🔧 시스템 설정** | Telegram @dudals0714_bot 연동 완료. 배경 에이전트 모델 nemotron 고정. 디렉터만 deepseek v4-flash. Unity Recorder 5.1.1 설치 + RecordGameplay.cs 생성. |
|| 2026-06-15 | **📐 설계 추가** | ROADMAP + CYCLE에 Phase 11(실내벽타일 14사이클), Phase 12(로딩 5사이클), Phase 13(주야 5사이클) 추가 |
|| 2026-06-16 | **C8-31 가스 분사기 데이터 시스템** | GasSprayerSystem.cs (5등급 enum+데이터+매니저), GasSprayerSystemTests.cs 20개 작성, RecordGameplay.cs 컴파일 오류 수정, 컴파일 ✅ |
|| 2026-06-16 | **C8-32 가스 분사기 장착/해제** | GasSprayerController.cs (장착/해제/분사 제어, Singleton, 인벤토리 연동), BackSlotUI.cs (HUD 오버레이), GasSprayerControllerTests.cs 14개, git b8c0490 ✅ |
|| 2026-06-16 | **C8-33 물약 삽입 & 분사** | GasPotionLoader (장전/해제, Potion/Herb/Drug), SprayInputHandler (우클릭 홀드), SprayVFX (속성별 안개 5종+VFX), GasSprayerController LoadPotion 추가, 테스트 26개, git 9589f6f ✅ |
|| 2026-06-16 | **C8-34 분사 타이머 UI & 재장전** | GasSprayerController (IsReloading/StartReload/CancelReload/CompleteReload/가스소진 자동재장전), BackSlotUI (재장전바/가스게이지8px/깜빡임/키안내), SprayInputHandler (재장전중입력무시), ControllerTests 재장전6개 추가, git a7f676e ✅ |
|| 2026-06-16 | **C8-35~37 절차적 아이콘 + UI 적용 (Phase 8 완료 🎉)** | ProceduralIconGenerator(Core, 카테고리별 형태/색상/캐싱/희귀도테두리), InventoryWindow/ShopWindow/LootWindow/RecipeWindow 아이콘 표시, ProceduralIconGeneratorTests 316줄, git 10f70bf ✅ |
|| 2026-06-16 | **Phase 12: ⏳ 로딩 화면 시스템 (5사이클 완료 ✅)** | LoadingManager(싱글톤/AsyncOperation), LoadingScreenUI(IMGUI/스피너/진행바), TipDatabase(26개팁), LoadingManagerTests 264줄, git a268a72 ✅ |
|| 2026-06-16 | **Phase 13: ⏰ 시간 & 주야 시스템 (5사이클 완료 ✅)** | TimeManager(GameTime/TimeScale/Hour/Minute/IsDay), DayNightCycle(태양회전/색상/강도/Ambient/Fog), TimeDisplayUI(HUD), StarField(별200개), TimeManagerTests 425줄 16개, git 4e30b20 ✅ |
||| 2026-06-16 | **Phase 11: 🚪 실내 맵 (14사이클 완료 ✅)** | IndoorBuilder (방Mesh), IndoorTextureGenerator (타일/벽돌/석재/벽 15종+국가별5종), IndoorLighting (Ambient+PointLight+깜빡임), IndoorFurniturePlacer (6종가구), Shop/CraftHouse/Church/House/CastleInteriorBuilder, IndoorSceneTransition, IntegrationTests, git 3283107+4****10 ✅ |
|| 2026-06-17 | **Phase 14: 🗡️ 복수명부 시스템 (10/10 완료 🎉)** | RevengeListManager(81영주\10독살공모자), RevengeListWindow(K키), RevealReason(처형시표시\3초페이드), Interrogation(MercyUI추궁\Level×3%확률), 능력치보상(공격+5\연금+10%\골드+1000), GameClearFlag, GameManager/LordSurrender/PoisonTakeover 연동, 17개 EditMode 테스트, run_tests.sh 수정 |
|| 2026-06-17 | **Phase 15: 🪖 병사 3단계 모델 (3/3 완료 🎉)** | LevelGroupId 5→3단계 축소(Recruit/Veteran/Elite), LevelGroupManager/LevelGroupData 업데이트, LevelGroupTests/ModelMappingTierTests 3단계 대응, Placeholder 색상 3색(연두/파랑/빨강) |
|| 2026-06-17 | **🎬 Phase 3C — Post-processing 완료** | Phase3C_PostProcessingSetup.cs 197줄 + Phase3C_PostProcessingTests.cs 364줄 15개. ACES Tonemapping, Bloom 1.5, ColorAdj contrast=8/saturation=15, Vignette 0.35, WhiteBalance -5, ShadowsMidtonesHighlights -0.02, DepthOfField Gaussian, Color Grading HDR 전환, URP Asset 프로파일 연결 ✅
||| 2026-06-17 | **🤖 EditorAutoSetup.cs 생성** | `[InitializeOnLoad]` 기반 자동 설정 스크립트. Editor 첫 실행 시 URP Pipeline/Skybox/Post-processing(7종)/SwayController 자동 적용. Tools/Re-run Auto Setup 및 Reset Auto Setup Flag 메뉴 제공 ✅
||| 2026-06-17 | **⭐ Phase 25~31 v3.0 완료 ⭐** | 선술집/용병(25) + 스탯창/장비(26) + 사망/부활(27) + 드라큘라(28) + 보석상자(29) + 이름/국가명(30) + 영지점령상징(31) — 총 7개 Phase, 62사이클, 신규 파일 15개, 테스트 70개+ 🚀
||| 2026-06-17 | **⚔️ Phase 1.6 완료 — 전리품 시스템 마무리** | DropTable ScriptableObject 에셋 4종 생성 (Early/Mid/Late Monster + Soldier). Tools/Phase 1.6 - Create Drop Tables 메뉴. EditorAutoSetup 통합. 몬스터/병사 사망 시 티어별 고기/재료/금화/장비 드랍 완료 ✅
||| 2026-06-17 | **🏰 Phase 5 — 영지 & 부하 관리 완료** | WarehouseSystem(영지 창고 20슬롯, SaveData 연동) + WarehouseUI(4×5 IMGUI 그리드) + ChurchSystem(기부/친밀도 0~100, 영주대면 조건) + ChurchUI(IMGUI 성당 UI, 프로그레스바, 기부 버튼 3종) + 19개 EditMode 테스트. EditorAutoSetup 통합 ✅
||| 2026-06-17 | **🧪 Phase 4 — 크래프트 & 레시피 시스템 완료** | Phase4_GenerateRecipeAssets.cs (314줄) — GAME_DATA.md 기반 80개 Recipe ScriptableObject + 4개 RecipeDatabase 에셋 자동 생성 (공격성/정신성/회복성/물리성 각 20종). Tools/Phase 4 - Generate Recipe Assets 메뉴. EditorAutoSetup 통합 ✅
||| 2026-06-18 | **Phase 3.3+3.4 — 영지 데이터 + 국기 시스템 완료 🏁** | Phase 3.3: TerritoryDatabase.cs 81개 영지 데이터 + EmpireAccessRule.cs (80영지 점령 시 황제국 입장). Phase 3.4: NationFlagData.cs (5개국 국기 정의), NationFlagDatabase.cs, NationFlagVisualData.cs, FlagManager.cs (중앙 깃발 관리), FlagPoleDisplay.cs (3D 절차적 깃대+깃발+흔들림+반기), PlayerFlagRegistrationWindow.cs (국기 등록 UI). 57개 EditMode 테스트 작성 ✅
||| 2026-06-18 | **Phase 3.5 — 월드맵 UI & 국기 표시 완료 🗺️** | MapWindow.cs 완전 재작성 (IMGUI 월드맵, Overview→Nation 2단계 줌, 5지역×20영지 그리드, 국기+난이도+소유주표시, 플레이어위치📍, 황제국안개❓). FlagPoleDisplay.FadeTransition 추가 (페이드 교체 연출). Phase35_MapWindowTests 20개 ✅
||| 2026-06-18 | **🎮 Phase G3 시작 — Batch 1~3 완료 (9/13 사이클)** | G3-01: DayNightCycle Moon Light / Skybox Lerp / Weather 연동 ✅ | G3-02: MainMenuUI 그라디언트 배경 + 펄스 + Credits ✅ | G3-03: SettingsMenuUI.cs 신규 (Graphics/Audio/KeyBindings 3탭) ✅ | G3-04: SaveManager 5슬롯 + AutoSave ✅ | G3-05: UIStyleManager.cs (공통 스타일/테두리/딤드/닫기버튼) ✅ | G3-06: ItemIconDatabase.cs (아이콘 캐싱 + 3개 창 적용) ✅ | G3-07: EscMenuUI.cs (일시정지/재개/저장/설정/타이틀로/종료) ✅ | G3-08: DeathScreenUI.cs (붉은 Fade + 부활/로드) ✅ | G3-13: AchievementSystem.cs (15개 업적 + 팝업) ✅ | 총 20개 신규 파일, 컴파일 ✅
||| 2026-06-18 | **🎮 Phase G3 Batch 4 완료 — 13/13 사이클 전부 완료 🎉** | G3-09: QuestJournalUI.cs 563줄 (J키/탭/진행률/완료효과) ✅ | G3-10: ControllerSupport.cs 560줄 (Xbox/PS/DualSense 매핑 + 힌트 오버레이) ✅ | G3-11: LoadingScreenUI.cs 327줄 (그라디언트/로고/부드러운바/카테고리팁/링스피너) + TipDatabase.cs TipCategory 분류 ✅ | G3-12: SoundRefinement.cs 423줄 (FootstepSoundController/UISoundIntegrator/BiomeAmbientController 3컴포넌트) + PlayerMovement footstep 패치 ✅ | 신규 9개 파일(코드 6 + 테스트 4), 총 29개 파일, 279개→0개 컴파일 오류 해소 ✅

|---|---

---

## ⚠️ 작업 규칙 (필독)

> Hermes는 아래 규칙을 **반드시** 준수합니다:

> 1. **서브에이전트 우선** — 3개 이상 파일 수정/새 기능/리팩토링은 delegate_task로 위임
> 2. **TDD 강제** — 모든 코드 변경 후 Unity batchmode 컴파일 검증 필수
> 3. **오류 0** — "Scripts have compiler errors" 없을 때까지 수정 반복
> 4. **3중 문서 저장** — 사이클 완료 시 PROGRESS.md + CYCLE.md + 메모리(Hermes memory) 3곳에 반드시 기록
> 5. **텔레그램 알림** — 사이클 완료 시 send_message로 텔레그램 알림 전송 (변경사항 요약 + 다음 사이클 추천)
> 6. **텔레그램 명령 수신** — 텔레그램 메시지로 명령/질문 도착 시 즉시 응답 및 작업 수행