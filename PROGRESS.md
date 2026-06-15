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
||| 전리품 주머니 | 사망 시 LootBasket 드랍 | ⬜ |
||| LootWindow 활용 | 주머니 클릭/ E키 → LootWindow (개별 or 전부 획득, ILootBasket 연동) | ✅ |
|||| 몬스터 드랍표 | 티어별 고기/재료 드랍 | ⬜ |
||| 병사 드랍표 | 등급별 금화/장비 + 희귀 5% | ⬜ |

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

||| Phase 3 | 🌍 세계관 & 월드맵 (4방위×20영지 + 황제국 👑 = 81영지, 방사형 난이도) | ⬜ |
||| **Phase 3.5** | 🎥 Top-Down 카메라 & 컨트롤 시스템 + ⚔️ 국가간 영지전쟁 설계 | ✅ |
| Phase 3.6 | 🎨 지형 그래픽 개선 & 환경 조형물 | 🔄 → ✅ |
| Phase 3.8 | 📊 캐릭터 스테이터스 & 레벨업 시스템 | ✅ (구현 완료) |
| Phase 3.9 | 🏔️ Poly Haven 3D 모델 8종 배치 (245개) | ✅ |
| Phase 3.10 | 🟤 Poly Haven 지형 텍스처 3종 적용 (3링) | ✅ |
|    └ 3.6.1 지형 텍스처 | Procedural 잔디 텍스처 + URP Lit 머티리얼 적용 | ✅~40% |
|    └ 3.6.2 환경 조형물 | 나무30+바위20+풀50+덤불20+꽃15 랜덤 배치 | ✅~70% |
|    └ 3.6.3 조명 & 분위기 | Directional Light 튜닝 + 안개 + Global Volume | ✅~60% |
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
|    || Phase 4 | 🧪 크래프트 & 레시피 시스템 (80종조합+방독면+색안개) | ⬜ |
|    || Phase 5 | 🏰 첫 번째 영지 & 부하 관리 (영지활동/창고/장비/방문/상점/밀매/영주대면) | ⬜ |
|    || Phase 6 | 🚪 실내외 맵 전환 시스템 | ⬜ |
|    || Phase 7 | ⚔️ 전쟁 & 영지 점령 | ⬜ |
|    || Phase 8 | 🎬 연출 & 퀄리티 | ⬜ |
|    || Phase 9 | 🧪 테스트 & 배포 | ⬜ |

---

## 📌 사장님 추가사항 14건 → 자동 반영 완료 (+10건 추가 반영)

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

## 🤖 백그라운드 에이전트 (cronjob)

|| 이름 | 주기 | 역할 |
|:----|:----|:------|
| code-agent | 10분 | C# 코딩 자동화 |
| qa-agent | 10분 | 테스트 자동화 |
| design-agent | 30분 | 문서 관리 |
| **glb-watcher** | **5분** | **GLB 파일 감시 → 자동 교체** |

---

> **현재 상태:** Phase 0~2 완료 ✅, Phase 3.5~3.10 ✅, **Phase 4 완료 ✅ (21/21)**, **Phase 5: C9-01~04 완료 ✅**
> **Phase 8 크래프트 진행:** ✅ 30/30 완료 — C8-01~28 + C8-29~30 폭탄 시스템
> **Phase 3.5 완료:** Top-Down 카메라 + 커서 시선 회전 + URP 할당
> **Phase 3.6 완료 항목:** ✓ Procedural 잔디 텍스처 ✓ URP Lit 재질 ✓ Directional Light 튜닝 ✓ 안개(Fog) ✓ Global Volume
> **Phase 3.9 완료:** ✅ Poly Haven 3D 모델 8종 (나무3/바위3/식물2) → 총 245개 배치
> **Phase 3.10 완료:** ✅ Poly Haven 지형 텍스처 3종 → 중앙/중간/외곽 3링 구역 적용
> **Phase 3.6 미완료:** 나무 3종 완성, 바위 3종 완성 (Poly Haven으로 대체 완료), Post-processing Volume 설정, 스카이박스 구성
> **Phase 4 완료:** ✅ C4-08 자동조준 + C4-12~15 LootWindow 연동 + C4-16~19 드랍테이블 + C4-20 카메라 무빙 + C4-21 타격 이펙트 완료 (21/21)
> **GLB 자산 (2026-06-13):** 사장님 GLB 36종 UserProvided/ 준비 완료 (Player_Rigged/몬스터20종Rigged/건물6종/도구2종/NPC/병사/약초4종) — Placeholder 생성 시 ModelSwapper 자동 교체

---

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
| 2026-06-15 | **📋 추가사항 설계 반영** | 가스 분사기(4.11/C8-31~34), 아이템 아이콘(4.12/C8-35~37), 병사 레벨별 아바타(5.3.12/C9-31~32) — ROADMAP.md + CYCLE.md 반영 완료 |

|---|---

---

## ⚠️ 작업 규칙 (필독)

> Hermes는 아래 규칙을 **반드시** 준수합니다:

> 1. **서브에이전트 우선** — 3개 이상 파일 수정/새 기능/리팩토링은 delegate_task로 위임
> 2. **TDD 강제** — 모든 코드 변경 후 Unity batchmode 컴파일 검증 필수
> 3. **오류 0** — "Scripts have compiler errors" 없을 때까지 수정 반복
> 4. **문서 자동화** — PROGRESS.md/ROADMAP.md 작업 완료 시 즉시 업데이트