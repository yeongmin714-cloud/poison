     1|     1|     1|     1|     1|     1|     1|     1|# Cycle: C10-01 아이템 툴팁 시스템
     2|     2|     2|     2|     2|     2|     2|     2|- Completed: TooltipWindow (MonoBehaviour, IMGUI 385줄), ItemTooltipData (struct+확장메서드 171줄)
     3|     3|     3|     3|     3|     3|     3|     3|- Details: 0.3s 지연 표시, 등급별 테두리/카테고리별 색상, 내구도 표시, 화면 끝 반대조정
     4|     4|     4|     4|     4|     4|     4|     4|- Integration: InventoryWindow, ShopWindow, LootWindow, CraftingUI — 4개 창 연동
     5|     5|     5|     5|     5|     5|     5|     5|- Tests: TooltipTests 20개 (데이터생성/유효성/등급명/색상/내구도/표시/숨김)
     6|     6|     6|     6|     6|     6|     6|     6|- Date: 2026-06-17
     7|     7|     7|     7|     7|     7|     7|     7|
     8|     8|     8|     8|     8|     8|     8|     8|# Cycle: C10-02 몬스터 어그로 합세 시스템
     9|     9|     9|     9|     9|     9|     9|     9|- Completed: MonsterAggroSystem (싱글톤 191줄), IAggroable 인터페이스, AggroState enum
    10|    10|    10|    10|    10|    10|    10|    10|- Details: 10m 범위 같은 종 합세, 이미 전투중 스킵, 사망/이탈 시 5초후 Idle. WindZone 무시 경고 회피
    11|    11|    11|    11|    11|    11|    11|    11|- Tests: MonsterAggroTests 16개 (등록/통보/10m범위/타입구분/전투중스킵/타이머/해제/멀티합세)
    12|    12|    12|    12|    12|    12|    12|    12|- Date: 2026-06-17
    13|    13|    13|    13|    13|    13|    13|    13|
    14|    14|    14|    14|    14|    14|    14|    14|# Cycle: C10-03 약초 리스폰 게이지 UI
    15|    15|    15|    15|    15|    15|    15|    15|- Completed: HerbRespawnUI (MonoBehaviour, IMGUI 209줄)
    16|    16|    16|    16|    16|    16|    16|    16|- Details: 리스폰 중 프로그레스바(녹→황→적) + 텍스트, 채집가능 [E]채집 표시, 30m 거리 컬링
    17|    17|    17|    17|    17|    17|    17|    17|- Tests: HerbRespawnUITests 11개 (싱글톤/컬링/거리/게이지)
    18|    18|    18|    18|    18|    18|    18|    18|- Date: 2026-06-17
    19|    19|    19|    19|    19|    19|    19|    19|
    20|    20|    20|    20|    20|    20|    20|    20|# Cycle: C10-04 풀/나무 흔들림 애니메이션
    21|    21|    21|    21|    21|    21|    21|    21|- Completed: SwayController (MonoBehaviour, 147줄)
    22|    22|    22|    22|    22|    22|    22|    22|- Details: 회전진동(sway) + 상하보빙(bobbing), WindZone 방향영향, 50m 거리 컬링, InstanceID 기반 랜덤오프셋
    23|    23|    23|    23|    23|    23|    23|    23|- Tests: SwayControllerTests 15개 (설정값/범위/세터/리셋/컬링)
    24|    24|    24|    24|    24|    24|    24|    24|- Date: 2026-06-17
    25|    25|    25|    25|    25|    25|    25|    25|
    26|    26|    26|    26|    26|    26|    26|    26|# Cycle: C23-01~04 — Phase 23: 🔄 몬스터 15초 리스폰
    27|    27|    27|    27|    27|    27|    27|    27|- Status: ✅ 기존 코드로 이미 구현 (Die() 괄호 버그 수정)
    28|    28|    28|    28|    28|    28|    28|    28|- Details: AnimalAI.Die() → Invoke(Respawn, tier-based 10~20s) → Respawn() at _spawnPos
    29|    29|    29|    29|    29|    29|    29|    29|- Integration: DifficultyManager, MonsterSpawner.CheckAndRespawn night ×1.5
    30|    30|    30|    30|    30|    30|    30|    30|- Date: 2026-06-17
    31|    31|    31|    31|    31|    31|    31|    31|
    32|    32|    32|    32|    32|    32|    32|    32|# Cycle: C24-01~05 — Phase 24: 🏴 영지 미완전 점령 시 병사 리스폰 시스템
    33|    33|    33|    33|    33|    33|    33|    33|- Completed: ✅ **전체 5사이클 구현 완료**
    34|    34|    34|    34|    34|    34|    34|    34|- Details:
    35|    35|    35|    35|    35|    35|    35|    35|  - C24-01: **TerritoryBattleState enum** (Peaceful/UnderAttack/Retreated/Reinforcing/Conquered) + TerritoryState 필드 추가
    36|    36|    36|    36|    36|    36|    36|    36|  - C24-02: **TerritoryBattleManager** (270줄) — 플레이어 이탈 감지(50m), 10초 Retreat 타이머, 상태 머신
    37|    37|    37|    37|    37|    37|    37|    37|  - C24-03: **병사 리스폰 큐** — 30초 간격 1명 복원, GuardRespawnEntry 큐 관리
    38|    38|    38|    38|    38|    38|    38|    38|  - C24-04: **GuardPlaceholder.Die() 수정** — Destroy 대신 Hide + HP복원 Respawn() 추가, AlarmSystem 연동
    39|    39|    39|    39|    39|    39|    39|    39|  - C24-05: **TerritoryBattleManagerTests 12개** — 싱글톤/상태전환/가드사망/영주처치/Peace복원/Conquered중단
    40|    40|    40|    40|    40|    40|    40|    40|- Integration: AlarmSystem.TriggerAlert() → StartBattle(), GuardPlaceholder.Die() → EnqueueGuardRespawn()
    41|    41|    41|    41|    41|    41|    41|    41|- Date: 2026-06-17
    42|    42|    42|    42|    42|    42|    42|# Cycle: C25-01~14 — Phase 25: 🍺 선술집 & 용병 시스템
    43|    43|    43|    43|    43|    43|    43|- Status: ✅ **전체 14사이클 구현 완료** (코드+테스트)
    44|    44|    44|    44|    44|    44|    44|- Details:
    45|    45|    45|    45|    45|    45|    45|  - C25-01: TavernInteriorBuilder (카운터+테이블+무대+어두운 조명) — 213줄
    46|    46|    46|    46|    46|    46|    46|  - C25-02: MercenaryData (struct, 4등급, 능력치/비용/스토리) — 104줄
    47|    47|    47|    47|    47|    47|    47|  - C25-02~03: MercenaryManager 싱글톤 (고용/해고/호감도, 8종 용병) — 335줄
    48|    48|    48|    48|    48|    48|    48|  - C25-03: MercenaryHireUI (IMGUI 고용 창, H키) — 381줄
    49|    49|    49|    49|    49|    49|    49|  - C25-04: BardMercenary (반경 15m 버프, 공+15%/방+10%/이속+10%) — 201줄
    50|    50|    50|    50|    50|    50|    50|  - C25-06: MercenaryPlaceholder (금색/은색 모델, 바드 류트) — 127줄
    51|    51|    51|    51|    51|    51|    51|- Tests: Phase25_MercenaryTests 14개 (데이터/별표/배율/싱글톤/DB/바드/Placeholder/Tavern)
    52|    52|    52|    52|    52|    52|    52|- Date: 2026-06-17
    53|    53|    53|    53|    53|    53|    53|
    54|    54|    54|    54|    54|    54|    54|# Cycle: C26-01~10 — Phase 26: 📊 병사/용병 스탯창 & 장비 지급
    55|    55|    55|    55|    55|    55|    55|- Status: ✅ **전체 10사이클 구현 완료** (코드+테스트)
    56|    56|    56|    56|    56|    56|    56|- Details:
    57|    57|    57|    57|    57|    57|    57|  - C26-01: GuardInfoWindow (IMGUI 657줄, 이름/Lv/HP/전투력/장비/버프, 싱글톤)
    58|    58|    58|    58|    58|    58|    58|  - C26-02: GuardEquipmentSystem (651줄, EquipSlot 4종, 장착/회수/내구도/유니크제한)
    59|    59|    59|    59|    59|    59|    59|  - C26-03: Bard 악기 슬롯 + 전설 용병 유니크 아이템
    60|    60|    60|    60|    60|    60|    60|- Tests: Phase26_GuardEquipmentTests — 확인 필요
    61|    61|    61|    61|    61|    61|    61|- Date: 2026-06-17
    62|    62|    62|    62|    62|    62|    62|
    63|    63|    63|    63|    63|    63|    63|# Cycle: C27-01~07 — Phase 27: 💀 병사 사망 & 부활 시스템 개선
    64|    64|    64|    64|    64|    64|    64|- Status: ✅ **전체 7사이클 구현 완료** (코드+테스트)
    65|    65|    65|    65|    65|    65|    65|- Details:
    66|    66|    66|    66|    66|    66|    66|  - C27-01: GuardManager (463줄, 영지별 병사 관리, 영구 사망, 재충원)
    67|    67|    67|    67|    67|    67|    67|  - C27-02: 플레이어 사망 → 병사 체력 10% 부활 + 30초 자동 회복
    68|    68|    68|    68|    68|    68|    68|  - C27-03: 전쟁 중 퇴각 모드, "플레이어가 쓰러졌다!" 메시지
    69|    69|    69|    69|    69|    69|    69|- Tests: GuardManagerTests 460줄
    70|    70|    70|    70|    70|    70|    70|- Date: 2026-06-17
    71|    71|    71|    71|    71|    71|    71|
    72|    72|    72|    72|    72|    72|    72|# Cycle: C28-01~07 — Phase 28: 🧛 드라큘라 영지 & 야간 컨텐츠
    73|    73|    73|    73|    73|    73|    73|- Status: ✅ **전체 7사이클 구현 완료** (코드+테스트)
    74|    74|    74|    74|    74|    74|    74|- Details:
    75|    75|    75|    75|    75|    75|    75|  - C28-01: DraculaTerritoryController (밤에만 활성화, 붉은 안개/박쥐 VFX, 북쪽)
    76|    76|    76|    76|    76|    76|    76|  - C28-02: DraculaLord (능력치 5배, 밤 체력 재생, 박쥐 소환, 희귀 100% 드랍)
    77|    77|    77|    77|    77|    77|    77|  - C28-03: 희귀 드랍 7종 (전설 무기/영구 버프/재료/금화 500~2000)
    78|    78|    78|    78|    78|    78|    78|- Tests: DraculaTerritoryTests 24개
    79|    79|    79|    79|    79|    79|    79|- Date: 2026-06-17
    80|    80|    80|    80|    80|    80|    80|
    81|    81|    81|    81|    81|    81|    81|# Cycle: C29-01~06 — Phase 29: 💎 동굴 보석 상자 & 희귀 광석
    82|    82|    82|    82|    82|    82|    82|- Status: ✅ **전체 6사이클 구현 완료** (코드+테스트)
    83|    83|    83|    83|    83|    83|    83|- Details:
    84|    84|    84|    84|    84|    84|    84|  - C29-01: GemData (Ruby/Sapphire/Emerald/Amethyst/GoldGem/Diamond 6종)
    85|    85|    85|    85|    85|    85|    85|  - C29-01: GemChest (E키 상호작용, Point Light, LootBasket 드랍)
    86|    86|    86|    86|    86|    86|    86|  - C29-03: CaveInteriorBuilder (암석 텍스처, 푸른 조명, 보석 상자 1~3개)
    87|    87|    87|    87|    87|    87|    87|- Tests: Phase29_GemChestTests 9개
    88|    88|    88|    88|    88|    88|    88|- Date: 2026-06-17
    89|    89|    89|    89|    89|    89|    89|
    90|    90|    90|    90|    90|    90|    90|# Cycle: C30-01~08 — Phase 30: 👑 영주/용병 이름 & 국가명
    91|    91|    91|    91|    91|    91|    91|- Status: ✅ **전체 8사이클 구현 완료** (코드+테스트)
    92|    92|    92|    92|    92|    92|    92|- Details:
    93|    93|    93|    93|    93|    93|    93|  - C30-01: 5개 국가명 (비르텐시아/아르델리아/이그니스/프로스트가드/아우레우스)
    94|    94|    94|    94|    94|    94|    94|  - C30-02: 영주 이름 100개 (국가별 20개)
    95|    95|    95|    95|    95|    95|    95|  - C30-03: 용병 이름 400개 조합 (이름 20×성 20)
    96|    96|    96|    96|    96|    96|    96|- Tests: Phase30_NamePoolTests 12개
    97|    97|    97|    97|    97|    97|    97|- Date: 2026-06-17
    98|    98|    98|    98|    98|    98|    98|
    99|    99|    99|    99|    99|    99|    99|# Cycle: C31-01~10 — Phase 31: 🏴 영지 점령 상징 교체
   100|   100|   100|   100|   100|   100|   100|- Status: ✅ **전체 10사이클 구현 완료** (코드+테스트)
   101|   101|   101|   101|   101|   101|   101|- Details:
   102|   102|   102|   102|   102|   102|   102|  - C31-01: PlayerEmblemData (문양 10종×색상 8종, 8자명)
   103|   103|   103|   103|   103|   103|   103|  - C31-01~02: EmblemManager 싱글톤 (저장/로드/변경, 100골드)
   104|   104|   104|   104|   104|   104|   104|  - C31-04: TerritoryBannerSystem (깃발 색상 교체, 병사 색상 Lerp, 점령 알림)
   105|   105|   105|   105|   105|   105|   105|- Tests: Phase31_EmblemTests 11개
   106|   106|   106|   106|   106|   106|   106|- Date: 2026-06-17
   107|   107|   107|   107|   107|   107|   107|
   108|   108|   108|   108|   108|   108|   108|# Cycle: G2-01 — Bloom + Tonemapping + Color Grading ✅ — Bloom + Tonemapping + Color Grading
   109|   109|   109|   109|   109|   109|   109|- Status: ✅
   110|   110|   110|   110|   110|   110|   110|- Details: URP Volume Override — Bloom(Intensity=1.0, Threshold=0.9), Tonemapping(ACES), Color Grading(Lift/Gamma/Gain), Vignette
   111|   111|   111|   111|   111|   111|   111|- Editor: Tools/Phase G2/Apply Post-Processing
   112|   112|   112|   112|   112|   112|   112|- Integration: 기존 Global Volume 프로파일 (SSAO와 공존)
   113|   113|   113|   113|   113|   113|   113|
   114|   114|   114|   114|   114|   114|   114|# Cycle: G2-02 — HDRI Skybox 교체 ✅ — HDRI Skybox 교체
   115|   115|   115|   115|   115|   115|   115|- Status: ✅
   116|   116|   116|   116|   116|   116|   116|- Details: Procedural Skybox 머티리얼 생성, Directional Light 색상 매칭, 안개 톤 조정
   117|   117|   117|   117|   117|   117|   117|- Editor: Tools/Phase G2/Set Skybox
   118|   118|   118|   118|   118|   118|   118|
   119|   119|   119|   119|   119|   119|   119|# Cycle: G2-03 — UI 애니메이션 (Fade/Slide) ✅ — UI 애니메이션 (Fade/Slide)
   120|   120|   120|   120|   120|   120|   120|- Status: ✅
   121|   121|   121|   121|   121|   121|   121|- Details: UIWindow.Open() Fade 0.2s + 배경 딤드, UIWindow.Close() Fade 0.15s, Slide In 애니메이션
   122|   122|   122|   122|   122|   122|   122|- Integration: 모든 UIWindow 하위 클래스
   123|   123|   123|   123|   123|   123|   123|
   124|   124|   124|   124|   124|   124|   124|# Cycle: G2-04 — 전투 카메라 이펙트 ✅ — 전투 카메라 이펙트
   125|   125|   125|   125|   125|   125|   125|- Status: ✅
   126|   126|   126|   126|   126|   126|   126|- Details: Cinemachine Impulse Shake, 타격 Time.timeScale 0.5, 처치 슬로우모션, 치명타 2배
   127|   127|   127|   127|   127|   127|   127|- Tests: G2-04_CameraEffectTests 15개
   128|   128|   128|   128|   128|   128|   128|
   129|   129|   129|   129|   129|   129|   129|# Cycle: G2-05 — 전투 VFX ✅ — 전투 VFX
   130|   130|   130|   130|   130|   130|   130|- Status: ✅
   131|   131|   131|   131|   131|   131|   131|- Details: 히트플래시 0.1s, 데미지폰트, Trail Renderer, Sparks 파티클, 블러드
   132|   132|   132|   132|   132|   132|   132|- Tests: G2-05_CombatVFXTests 20개
   133|   133|   133|   133|   133|   133|   133|
   134|   134|   134|   134|   134|   134|   134|# Cycle: G2-06 — 볼류메트릭 포그/라이트 ✅ — 볼류메트릭 포그/라이트
   135|   135|   135|   135|   135|   135|   135|- Status: ✅
   136|   136|   136|   136|   136|   136|   136|- Details: URP Volumetric Fog, Directional Fog Shadows, 지역별 Fog, WeatherSystem 연동
   137|   137|   137|   137|   137|   137|   137|- Tests: G2-06_VolumetricFogTests 12개
   138|   138|   138|   138|   138|   138|   138|
   139|   139|   139|   139|   139|   139|   139|# Cycle: G2-07 — 공격 시스템 ✅ — 공격 시스템 (Phase 1.6)
   140|   140|   140|   140|   140|   140|   140|- Status: ✅
   141|   141|   141|   141|   141|   141|   141|- Details: 좌클릭 Raycast→Attack(), LootBasket.Create(), LootWindow, 드랍테이블, 30초 소멸
   142|   142|   142|   142|   142|   142|   142|- Tests: G2-07_CombatSystemTests 20개
   143|   143|   143|   143|   143|   143|   143|
   144|   144|   144|   144|   144|   144|   144|# Cycle: G2-08 — 사운드 시스템 ✅ — 사운드 시스템 개선
   145|   145|   145|   145|   145|   145|   145|- Status: ✅
   146|   146|   146|   146|   146|   146|   146|- Details: BGM Scene별전환, SFX 세분화, UI Sound, SoundManager 리팩토링
   147|   147|   147|   147|   147|   147|   147|- Tests: G2-08_SoundTests 15개
   148|   148|   148|   148|   148|   148|   148|
   149|   149|   149|   149|   149|   149|   149|# Cycle: G2-09 — 미니맵 ✅ — 미니맵
   150|   150|   150|   150|   150|   150|   150|- Status: ✅
   151|   151|   151|   151|   151|   151|   151|- Details: IMGUI 미니맵, 플레이어 중앙고정+회전, 영지아이콘, 줌, MapWindow 연동
   152|   152|   152|   152|   152|   152|   152|- Tests: G2-09_MinimapTests 12개
   153|   153|   153|   153|   153|   153|   153|
   154|   154|   154|   154|   154|   154|# Cycle: T-Cycle-01 — TutorialGuideSystem 싱글톤
   155|   155|   155|   155|   155|   155|- Status: ✅
   156|   156|   156|   156|   156|   156|- Details: GuideData 구조체 (id/title/desc/actionTrigger), PlayerPrefs 저장, ShowGuide 큐, ESC 스킵
   157|   157|   157|   157|   157|   157|- Integration: GameManager.InitialScene 연결
   158|   158|   158|   158|   158|   158|- Tests: TutorialGuideTests 15개
   159|   159|   159|   159|   159|   159|
   160|   160|   160|   160|   160|   160|# Cycle: T-Cycle-02 — BarnInteriorBuilder (헛간 실내)
   161|   161|   161|   161|   161|   161|- Status: ✅
   162|   162|   162|   162|   162|   162|- Details: 나무 벽/바닥 타일 텍스처, 허름한 분위기, 문 오브젝트, BuildingTrigger 연결
   163|   163|   163|   163|   163|   163|- Editor: Tools/Phase T/Build Barn Interior
   164|   164|   164|   164|   164|   164|
   165|   165|   165|   165|   165|   165|# Cycle: T-Cycle-03 — 영주 등장 이벤트 시퀀스
   166|   166|   166|   166|   166|   166|- Status: ✅
   167|   167|   167|   167|   167|   167|- Details: 문 두드리는 SFX → E키 문열림 → 영주NPC 등장 → 대화
   168|   168|   168|   168|   168|   168|- Integration: LordPlaceholder, IndoorSceneTransition.ExitBuilding
   169|   169|   169|   169|   169|   169|
   170|   170|   170|   170|   170|   170|# Cycle: T-Cycle-04 — 살인명부 연동
   171|   171|   171|   171|   171|   171|- Status: ✅
   172|   172|   172|   172|   172|   172|- Details: 영주 얼굴 확인 시 RevengeListWindow 자동 팝업 + 하이라이트
   173|   173|   173|   173|   173|   173|- Integration: RevengeListWindow, PlayerPrefs
   174|   174|   174|   174|   174|   174|
   175|   175|   175|   175|   175|   175|# Cycle: T-Cycle-05 — 퀘스트 + 가이드 큐
   176|   176|   176|   176|   176|   176|- Status: ✅
   177|   177|   177|   177|   177|   177|- Details: 퀘스트 자동 발급 (고기3/나무5/돌3 + 설사초2/쓴풀1), 설명창 큐 시작
   178|   178|   178|   178|   178|   178|- Tests: 10개
   179|   179|   179|   179|   179|   179|
   180|   180|   180|   180|   180|   180|# Cycle: T-Cycle-06 — T4 설명창 11종
   181|   181|   181|   181|   181|   181|- Status: ✅
   182|   182|   182|   182|   182|   182|- Details: WASD/마우스/좌클릭/Shift/Space/E키/I키/R키 액션 감지 설명창
   183|   183|   183|   183|   183|   183|- Tests: 15개
   184|   184|   184|   184|   184|   184|
   185|   185|   185|   185|   185|   185|# Cycle: T-Cycle-07 — 영주 처형 + 씬 전환
   186|   186|   186|   186|   186|   186|- Status: ✅
   187|   187|   187|   187|   187|   187|- Details: 독든음식 전달 → 영주행동불능 → MercyUI 처형 → 페이드아웃 → 영지 씬
   188|   188|   188|   188|   188|   188|- Tests: 10개
   189|   189|   189|   189|   189|   189|
   190|   190|   190|   190|   190|   190|# Cycle: T-Cycle-08 — T6 영지 설명창 (최초 액션 감지)
   191|   191|   191|   191|   191|   191|- Status: ✅
   192|   192|   192|   192|   192|   192|- Details: 순차강제 NO → 최초 액션 시 발동. 감지포인트: GuardInteraction/Equipment/GasSprayer/BackSlot/GuardMission/Shop/Map/Status/IndoorScene/Repair
   193|   193|   193|   193|   193|   193|- Tests: 15개
   194|   194|   194|   194|   194|   194|
   195|   195|   195|   195|   195|   195|# Cycle: T-Cycle-09 — 통합 테스트
   196|   196|   196|   196|   196|   196|- Status: ✅
   197|   197|   197|   197|   197|   197|- Details: 전체 튜토리얼 플로우 통합 테스트, 디버그 리셋
   198|   198|   198|   198|   198|   198|- Tests: 10개
   199|   199|   199|   199|   199|   199|
   200|   200|   200|   200|   200|   200|# Cycle: T-Cycle-10 — 전체 QA
   201|   201|   201|   201|   201|   201|- Status: ✅
   202|   202|   202|   202|   202|   202|- Details: 전체 플로우 수동 QA, 버그 수정, 밸런스 조정
   203|   203|   203|   203|   203|   203|
   204|   204|   204|   204|# Cycle: G3-01 — 낮/밤 사이클
   205|   205|   205|   205|- Status: ✅
   206|   206|   206|   206|- Details: DayNightCycle 싱글톤, Moon Light 추가, Skybox Lerp, Weather 연동, SmoothStep 보간, StarField 반짝임
   207|   207|   207|   207|- Tests: DayNightCycleTests.cs 12개
   208|   208|   208|   208|
   209|   209|   209|   209|# Cycle: G3-02 — 메인 메뉴
   210|   210|   210|   210|- Status: ✅
   211|   211|   211|   211|- Details: 그라디언트 배경 + 별 반짝임 + 타이틀 펄스 + Credits 화면 (제작진 정보)
   212|   212|   212|   212|- Tests: MainMenuTests.cs 기존 320줄 활용
   213|   213|   213|   213|
   214|   214|   214|   214|# Cycle: G3-03 — 설정 메뉴
   215|   215|   215|   215|- Status: ✅
   216|   216|   216|   216|- Details: SettingsMenuUI.cs 신규 — Graphics(품질/해상도/전체화면), Audio(BGM/SFX/UI/Ambient 슬라이더), KeyBindings 표시, PlayerPrefs 저장
   217|   217|   217|   217|- Tests: SettingsMenuTests.cs 7개
   218|   218|   218|   218|
   219|   219|   219|   219|# Cycle: G3-04 — 세이브/로드 UI
   220|   220|   220|   220|- Status: ✅
   221|   221|   221|   221|- Details: SaveManager 5슬롯 + AutoSave, SaveSlotUI 5슬롯 + Delete, LoadGameUI 5슬롯
   222|   222|   222|   222|- Tests: SaveManagerTests.cs 12개
   223|   223|   223|   223|
   224|   224|   224|   224|# Cycle: G3-05 — UI 통일성 개선
   225|   225|   225|   225|- Status: ✅
   226|   226|   226|   226|- Details: UIStyleManager.cs 정적 클래스 — 공통 색상(Bg/Border/Title/Dim/Hover/CloseBtn), 골드테두리 2px, MakeTexture 캐싱, DrawDimOverlay/DrawWindowBackground/DrawTitle/DrawCloseButton
   227|   227|   227|   227|- Tests: UIStyleManagerTests.cs 12개
   228|   228|   228|   228|
   229|   229|   229|   229|# Cycle: G3-06 — 아이템 아이콘 시스템
   230|   230|   230|   230|- Status: ✅
   231|   231|   231|   231|- Details: ItemIconDatabase.cs (ProceduralIconGenerator 래퍼 + 캐싱), InventoryWindow/ShopWindow/LootWindow 아이콘 표시
   232|   232|   232|   232|- Tests: 10개
   233|   233|   233|   233|
   234|   234|   234|   234|# Cycle: G3-07 — ESC 메뉴 (일시정지)
   235|   235|   235|   235|- Status: ✅
   236|   236|   236|   236|- Details: EscMenuUI.cs — Time.timeScale=0, 배경딤드, 재개/저장/설정/타이틀로/종료 버튼, ESC키 토글
   237|   237|   237|   237|- Tests: 10개
   238|   238|   238|   238|
   239|   239|   239|   239|# Cycle: G3-08 — 사망 화면
   240|   240|   240|   240|- Status: ✅
   241|   241|   241|   241|- Details: DeathScreenUI.cs — 붉은 Fade In 1.5s + YOU DIED + 부활(HP복원)/저장불러오기
   242|   242|   242|   242|- Tests: 10개
   243|   243|   243|   243|
   244|   244|   244|   244|# Cycle: G3-09 — 퀘스트 저널 개선
   245|   245|   245|   245|- Status: ✅
   246|   246|   246|   246|- Details: QuestJournalUI.cs 563줄 — J키 토글, 진행중/완료 탭, 진행률 바(cyan/green), 완료 골든 애니메이션 2초(fade in/rise/fade out), Queue 처리
   247|   247|   247|   247|- Files: QuestJournalUI.cs, QuestJournalUITests.cs
   248|   248|   248|   248|- Tests: QuestJournalUITests.cs 523줄 — AddQuest 5개, UpdateProgress 7개, CompleteQuest 5개, Tab 4개, Toggle 4개, Full workflow 2개, Edge cases 3개
   249|   249|   249|   249|
   250|   250|   250|   250|# Cycle: G3-10 — 컨트롤러 지원
   251|   251|   251|   251|- Status: ✅
   252|   252|   252|   252|- Details: ControllerSupport.cs 560줄 — Xbox/PS/DualSense 감지, A=상호작용 B=취소 Y=저널 X=메뉴, LB=대쉬 RB=구르기, LeftStick 이동, RightStick 카메라, D-Pad UI 내비게이션, 힌트 오버레이 5초 + Start+Select 토글
   253|   253|   253|   253|- Files: ControllerSupport.cs, ControllerSupportTests.cs
   254|   254|   254|   254|- Tests: ControllerSupportTests.cs 391줄 29개 — 감지 3개, 매핑 9개, 모드 3개, 정적 2개, 반환타입 3개
   255|   255|   255|   255|
   256|   256|   256|   256|# Cycle: G3-11 — 로딩 화면
   257|   257|   257|   257|- Status: ✅
   258|   258|   258|   258|- Details: LoadingScreenUI.cs 327줄 — 그라디언트 배경(진파랑→네이비), 골드 로고+⚔️ 서브타이틀, Mathf.Lerp 부드러운 진행바(blue→gold), 회전 링 스피너, 카테고리별 팁 2개(🎮/⚔️/🧠/📖). TipDatabase.cs TipCategory enum + TipInfo struct + 26개 팁 분류 + GetTwoRandomTips()
   259|   259|   259|   259|- Files: LoadingScreenUI.cs, TipDatabase.cs, LoadingScreenUITests.cs
   260|   260|   260|   260|- Tests: LoadingScreenUITests.cs 234줄 17개
   261|   261|   261|   261|
   262|   262|   262|   262|# Cycle: G3-12 — 사운드 세분화
   263|   263|   263|   263|- Status: ✅
   264|   264|   264|   264|- Details: SoundRefinement.cs 423줄 — FootstepSoundController(Raycast 지형감지 step_grass/stone/wood/water, 0.5/0.35/0.25s 간격), UISoundIntegrator(OnGUI MouseUp 감지 click/open/close), BiomeAmbientController(Reflection Biome 탐지 + 씬이름 키워드 폴백, 2초 간격 전환). PlayerMovement.cs footstep 패치(SoundManager.Instance→SoundEffectManager)
   265|   265|   265|   265|- Files: SoundRefinement.cs, SoundRefinementTests.cs
   266|   266|   266|   266|- Tests: SoundRefinementTests.cs 246줄 17개 — Footstep 5개, UISound 5개, Biome 7개
   267|   267|   267|   267|
   268|   268|   268|   268|# Cycle: G3-13 — 도전과제 (업적)
   269|   269|   269|   269|- Status: ✅
   270|   270|   270|   270|- Details: AchievementSystem.cs — 15개 업적(first_kill/level_5~20/craft_master/rich_man/herb_gather/quest_master/mercenary_king/poison_master/night_hunter/survivor/explorer/true_ending), PlayerPrefs 저장, 우측상단 팝업 3초
   271|   271|   271|   271|- Tests: AchievementTests.cs 12개
   272|   272|   272|   272|
   273|   273|   273|   273|# Phase 32: 🎲 병사 랜덤 장비 생성 (ROADMAP Phase 5.3.13)
   274|   274|   274|   274|# Cycle: C32-01 — 장비 희귀도 데이터
   275|   275|   275|   275|- Status: ✅
   276|   276|   276|   276|- Details: 5등급(일반/고급/희귀/전설/유니크) 정의, 기본 스탯 배율(1.0×~3.0×), 랜덤 변동폭(±5~15%)
   277|   277|   277|   277|- Tests: EquipmentRarityDataTests 8개
   278|   278|   278|   278|
   279|   279|   279|   279|# Cycle: C32-02 — 레벨별 확률 테이블
   280|   280|   280|   280|- Status: ✅
   281|   281|   281|   281|- Details: 5단계 레벨 구간 × 5등급 가중치 행렬. Lv.1~10(일반70%/고급20%/희귀8%/전설2%), Lv.41~50(희귀15%/전설45%/유니크40%)
   282|   282|   282|   282|- Tests: RarityTableTests 10개
   283|   283|   283|   283|
   284|   284|   284|   284|# Cycle: C32-03 — 부위별 착용 확률
   285|   285|   285|   285|- Status: ✅
   286|   286|   286|   286|- Details: 5부위(머리/상체/장갑/신발/무기) 독립 확률, Lv.1~10:25%→Lv.41~50:90%, 부분 장착, 평균 1~5부위
   287|   287|   287|   287|- Tests: EquipmentPartConfigTests 8개
   288|   288|   288|   288|
   289|   289|   289|   289|# Cycle: C32-04 — GuardEquipmentSpawner
   290|   290|   290|   290|- Status: ✅
   291|   291|   291|   291|- Details: SpawnEquipment(guardLevel) → RarityTable.Roll() → PartConfig.RollEach() → StatRandomize() → GuardEquipmentSystem.Apply()
   292|   292|   292|   292|- Files: GuardEquipmentSpawner.cs
   293|   293|   293|   293|- Tests: GuardEquipmentSpawnerTests 10개
   294|   294|   294|   294|
   295|   295|   295|   295|# Cycle: C32-05 — Lucky Roll 시스템
   296|   296|   296|   296|- Status: ✅
   297|   297|   297|   297|- Details: 5% 확률 1티어 상승 + 0.25% 확률 2티어 상승 (중첩), 최대 전설등급까지. 400명당 1명꼴로 Double Lucky
   298|   298|   298|   298|- Tests: LuckyRollTests 8개
   299|   299|   299|   299|
   300|   300|   300|   300|# Cycle: C32-06 — GuardPlaceholder/GuardEquipmentSystem 연동
   301|   301|   301|   301|- Status: ✅
   302|   302|   302|   302|- Details: Spawn/배치 시 GuardEquipmentSpawner 호출, 생성된 장비 자동 장착, 기존 장비 시스템과 호환, 스탯 적용
   303|   303|   303|   303|- Integration: GuardPlaceholder.cs, GuardEquipmentSystem.cs
   304|   304|   304|   304|- Tests: IntegrationTests 8개
   305|   305|   305|   305|
   306|   306|   306|   306|# Cycle: C32-07 — EditMode 테스트 종합
   307|   307|   307|   307|- Status: ✅
   308|   308|   308|   308|- Details: 희귀도 분포 통계(1000회 샘플링), 부위 확률 검증, Lucky Roll 확률 검증, 스탯 변동 범위, 연동 검증
   309|   309|   309|   309|- Tests: Phase32_FullTests 15개
   310|   310|   310|   310|
   311|   311|   311|   311|# Cycle: C6-23 — 몬스터 리깅 GLB 애니메이션 연동
   312|   312|   312|   312|- Status: ✅
   313|   313|   313|   313|- Details: 21종 리깅된 몬스터 GLB → Resources/Models/UserProvided/ 복사, AnimalAI ↔ RigAnimationController 연동 (AnimalAI.SetRigAnimator), 파일명 정규화 (Big Mouse → Big_Mouse 등 7개), Bat_RIgged → Bat_Rigged 오타 수정
   314|   314|   314|   314|- Date: 2026-06-20
   315|   315|   315|   315|
   316|   316|   316|   316|# Cycle: C6-24 — NPC/병사/플레이어 리깅 GLB 17종 복사
   317|   317|   317|   317|- Status: ✅
   318|   318|   318|   318|- Details: NPC 12종 + 병사 3종 + 용병 2종 + 플레이어 1종 리깅 GLB → Resources/Models/UserProvided/ 복사 (총 17종)
   319|   319|   319|   319|- Date: 2026-06-20
   320|   320|   320|   320|
   321|   321|   321|   321|# Cycle: C6-25 — TutorialQuestNPC ↔ RigAnimationController 연동
   322|   322|   322|   322|- Status: ✅
   323|   323|   323|   323|- Details: TutorialQuestNPC에 RigAnimationController 컴포넌트 부착, GLB 리깅 모델 애니메이션 자동 적용
   324|   324|   324|   324|- Date: 2026-06-20
   325|   325|   325|   325|
   326|   326|   326|   326|# Cycle: C6-26 — GuardPlaceholder ↔ RigAnimationController 연동
   327|   327|   327|   327|- Status: ✅
   328|   328|   328|   328|- Details: GuardPlaceholder에 RigAnimationController 컴포넌트 부착, GLB 리깅 모델 애니메이션 자동 적용
   329|   329|   329|   329|- Date: 2026-06-20
   330|   330|   330|   330|
   331|   331|   331|   331|# Cycle: C6-27 — PlayerPlaceholder ↔ RigAnimationController 연동
   332|   332|   332|   332|- Status: ✅
   333|   333|   333|   333|- Details: PlayerPlaceholder에 RigAnimationController 컴포넌트 부착, GLB 리깅 모델 애니메이션 자동 적용
   334|   334|   334|   334|- Date: 2026-06-20
   335|   335|   335|   335|
   336|   336|   336|   336|# Cycle: C6-28 — HerbPickup ↔ RigAnimationController 연동
   337|   337|   337|   337|- Status: ✅
   338|   338|   338|   338|- Details: HerbPickup에 RigAnimationController 컴포넌트 부착, 채집 모션 애니메이션 자동 적용
   339|   339|   339|   339|- Date: 2026-06-20
   340|   340|   340|   340|
   341|   341|   341|   341|# Cycle: C6-29 — SkeletonGuardPlaceholder ↔ RigAnimationController 연동
   342|   342|   342|   342|- Status: ✅
   343|   343|   343|   343|- Details: SkeletonGuardPlaceholder에 RigAnimationController 컴포넌트 부착, GLB 리깅 모델 애니메이션 자동 적용
   344|   344|   344|   344|- Date: 2026-06-20
   345|   345|   345|   345|
   346|   346|   346|   346|# Cycle: C6-30 — 버그 수정 & 컴파일 검증
   347|   347|   347|   347|- Status: ✅
   348|   348|   348|   348|- Details: GLBTextureSizeLimiter.cs CS0165 오류 3곳 수정, GameManager.cs 디버그 컴포넌트 #if UNITY_EDITOR 래핑, EditorAutoSetup.cs Play 모드 가드 추가, RigAnimationController.cs runtimeAnimatorController null 체크 추가
   349|   349|   349|   349|- Result: 컴파일 오류 0
   350|   350|   350|   350|- Date: 2026-06-20
   351|   351|   351|   351|
   352|   352|   352|   352|---
   353|   353|   353|   353|
   354|   354|   354|   354|## Phase 33: 🎨 UI 완전 개선 — 창별 개성화 & 고급화
   355|   355|   355|   355|
   356|   356|   356|   356|> 38개 UI 창이 전부 동일한 UIStyleManager 다크 테마 사용. 각 창에 고유한 절차적(Procedural) 테마를 부여.
   357|   357|   357|   357|> 모든 패턴/테두리/장식은 C# Texture2D로 코드 생성. 추가 이미지 에셋 불필요.
   358|   358|   358|   358|
   359|   359|   359|   359|# Cycle: UI-01 — UIDesignTheme SO + ProceduralTextureGenerator
   360|   360|   360|   360|- Status: ✅✅
   361|   361|   361|   361|- Details: UIDesignTheme SO (색상6종/패턴타입/테두리타입/장식타입/애니메이션타입). ProceduralTextureGenerator — Perlin noise 기반 7종 패턴(양피지/가죽/대리석/나무/돌/금속/유리). GradientBackgroundRenderer (2색/4색/방사형). DecorativeBorderRenderer (필그리/룬/가시/별/방패 모서리). WindowAnimationProfile (FadeSlide/Scale/Flip/Shatter/Spin 8종). UIWindow.ApplyTheme() 연동. Phase33_CreateThemeAssets.cs Editor 스크립트 (7개 테마 SO 생성).
   362|   362|   362|   362|- Tests: 12개 (ThemeDataTests 9개 + IntegrationTests 9개)
   363|   363|   363|   363|
   364|   364|   364|   364|# Cycle: UI-02 — MapWindow 🗺️ + MinimapUI 🧭 (지도 테마)
   365|   365|   365|   365|- Status: ✅
   366|   366|   366|   366|- Details: MapWindow — 세피아 양피지 배경(Parchment 패턴), 나뭇결 테두리(Filigree), 코너 나침반 장식(CornerScroll), FadeSlide 애니메이션. MinimapUI — 황동 원형 느낌(Glass 패턴), 방패 테두리(Shield), 왕관 장식, Scale 애니메이션. Phase33_Themes.CreateMapTheme()/CreateMinimapTheme() 적용. MapWindow.OnShow()에 배경 텍스처 오버레이 추가.
   367|   367|   367|   367|- Tests: 8개 (UIWindow.ApplyTheme 검증 포함)
   368|   368|   368|   368|
   369|   369|   369|   369|# Cycle: UI-03 — InventoryWindow 📦 + EquipmentWindow 🛡️ (가죽/대장간 테마)
   370|   370|   370|   370|- Status: ✅
   371|   371|   371|   371|- Details: InventoryWindow — 암갈색 가죽 결 배경(Leather 패턴), 구리 못 리벳 테두리(Star+Rivet), FadeSlide. EquipmentWindow — 철청 금속 브러시드 배경(Metal 패턴), 철제 방패 테두리(Shield+Rivet), Reveal 애니메이션. Phase33_Themes.CreateInventoryTheme()/CreateEquipmentTheme() 적용.
   372|   372|   372|   372|- Tests: 8개
   373|   373|   373|   373|
   374|   374|   374|   374|# Cycle: UI-04 — WarehouseUI 📦 (목재 테마)
   375|   375|   375|   375|- Status: ✅
   376|   376|   376|   376|- Details: WarehouseUI — 나무 판자 결 배경(Wood 패턴), 못 박힌 판자 테두리(Thorn+Rivet), Bounce 애니메이션. Phase33_Themes.CreateWarehouseTheme() 적용.
   377|   377|   377|   377|- Tests: 6개
   378|   378|   378|   378|
   379|   379|   379|   379|# Cycle: UI-05 — PlayerStatusWindow 📊 + QuestWindow 📜 (양피지 스크롤 테마)
   380|   380|   380|   380|- Status: ✅
   381|   381|   381|   381|- Details: PlayerStatusWindow — 아이보리 양피지 배경(Parchment), 장식적 모서리 롤(Filigree+Crown), 금박 테두리. QuestWindow — 줄 그어진 양피지(Parchment), 끈/리본 장식(Filigree+CornerScroll). Phase33_Themes.CreateStatusTheme()/CreateQuestTheme() 적용.
   382|   382|   382|   382|- Tests: 8개
   383|   383|   383|   383|
   384|   384|   384|   384|# Cycle: UI-06 — RecipeWindow 📖 + TooltipWindow ℹ️ (연금술/쪽지 테마)
   385|   385|   385|   385|- Status: ✅
   386|   386|   386|   386|- Details: RecipeWindow — 보라 마법진 패턴(Stone+Rune), 보석 박힌 테두리(Seal), Flip 애니메이션. TooltipWindow — 밝은 양피지 배경(Parchment), 절차적 패턴+테두리 OnGUI 연동. Phase33_Themes.CreateRecipeTheme()/CreateTooltipTheme() 적용.
   387|   387|   387|   387|- Tests: 6개
   388|   388|   388|   388|
   389|   389|   389|   389|# Cycle: UI-07 — CraftingUI 🔨 + CookingUI 🍲 (대장간/주방 테마)
   390|   390|   390|   390|- Status: ✅
   391|   391|   391|   391|- Details: CraftingUI — 나무 결+숯 얼룩 배경(Wood+Thorn), 구리 리벳(Rivet). CookingUI — 타일 패턴 배경(Glass+Star), 벽돌 테두리(CornerScroll). Phase33_Themes.CreateCraftingTheme()/CreateCookingTheme() 적용.
   392|   392|   392|   392|- Tests: 8개
   393|   393|   393|   393|
   394|   394|   394|   394|# Cycle: UI-08 — AlchemyUI 🧪 + RepairStationUI 🔧 (실험실/모루 테마)
   395|   395|   395|   395|- Status: ✅
   396|   396|   396|   396|- Details: AlchemyUI — 어두운 보라+네온초록 물방울(Glass+Rune+Skull, Spin 애니메이션), 버튼 색상 AccentColor 연동. RepairStationUI — 금속 긁힘 패턴(Metal+Shield+Rivet). Phase33_Themes.CreateAlchemyTheme()/CreateRepairTheme() 적용.
   397|   397|   397|   397|- Tests: 6개
   398|   398|   398|   398|
   399|   399|   399|   399|# Cycle: UI-09 — ShopWindow 🏪 + LootWindow 🎁 + MercenaryHireUI 🍺 (상점/거래 테마)
   400|   400|   400|   400|- Status: ✅
   401|   401|   401|   401|- Details: ShopWindow — 녹색 펠트 천 배경(Parchment+Filigree), 금실 장식. LootWindow — 나무 판자+쇠테 테두리(Wood+Thorn+Rivet). MercenaryHireUI — 기름때 묻은 양피지(Leather+Seal, Bounce). Phase33_Themes.ShopTheme()/LootTheme()/MercenaryTheme() 적용.
   402|   402|   402|   402|- Tests: 10개
   403|   403|   403|   403|
   404|   404|   404|   404|# Cycle: UI-10 — ChurchUI ⛪ + EnvoyMissionUI 🕊️ + SpyMissionUI 🕵️ (시설/첩보 테마)
   405|   405|   405|   405|- Status: ✅
   406|   406|   406|   406|- Details: ChurchUI — 대리석 패턴(Marble+Filigree), 고딕 아치 테두리(Crown), Scale. EnvoyMissionUI — 다크블루 공식 문서(Stone+Shield+Seal). SpyMissionUI — 암호 격자(Metal+Rune+Skull). Phase33_Themes.ChurchTheme()/EnvoyTheme()/SpyTheme() 적용.
   407|   407|   407|   407|- Tests: 10개
   408|   408|   408|   408|
   409|   409|   409|   409|# Cycle: UI-11 — RevengeListWindow 🗡️ + DeathScreenUI 💀 + LordAudienceUI 👑 (전투/죽음 테마)
   410|   410|   410|   410|- Status: ✅
   411|   411|   411|   411|- Details: RevengeListWindow — 피 얼룩 배경(Stone+Thorn+Skull), Shatter 애니메이션. DeathScreenUI — 잿빛 그라디언트(Stone+Thorn+Skull). LordAudienceUI — 대리석 패턴(Marble+Filigree+Crown), Scale. Phase33_Themes.RevengeTheme()/DeathTheme()/LordAudienceTheme() 적용.
   412|   412|   412|   412|- Tests: 8개
   413|   413|   413|   413|
   414|   414|   414|   414|# Cycle: UI-12 — EscMenuUI ⏸️ + SettingsMenuUI ⚙️ (시스템 메뉴 테마)
   415|   415|   415|   415|- Status: ✅
   416|   416|   416|   416|- Details: EscMenuUI — Glassmorphism 반투명+별 테두리(Glass+Star+Crown), Reveal. SettingsMenuUI — 모던블랙 마이크로 도트(Metal+Shield+Crown). Phase33_Themes.EscMenuTheme()/SettingsTheme() 적용.
   417|   417|   417|   417|- Tests: 10개
   418|   418|   418|   418|
   419|   419|   419|   419|# Cycle: UI-13 — AchievementSystem 🏆 + GuardWorldSpaceHUD 👤 + NPCDialogueWindow 💬 (HUD/게임플레이 테마)
   420|   420|   420|   420|- Status: ✅
   421|   421|   421|   421|- Details: Achievement — 골드 메달(Marble+Star+Crown), Bounce. GuardHUD — 전술 격자(Metal+Shield). NPCDialogue — 양피지 말풍선(Parchment+Filigree). Phase33_Themes.AchievementTheme()/GuardHUDTheme()/NPCDialogueTheme() 적용.
   422|   422|   422|   422|- Tests: 12개
   423|   423|   423|   423|
   424|   424|   424|   424|# Cycle: UI-14 — FlagRegistrationWindow 🏁 + GuardInfoWindow 🪖 (기타 테마)
   425|   425|   425|   425|- Status: ✅
   426|   426|   426|   426|- Details: FlagRegistration — 방패 문양(Stone+Shield+Crown). GuardInfo — 카키 군복(Leather+Shield). Phase33_Themes.FlagRegTheme()/GuardInfoTheme() 적용.
   427|   427|   427|   427|- Tests: 8개
   428|   428|   428|   428|
   429|   429|   429|   429|# Cycle: UI-15 — 🌿 월드스페이스 HUD 개선 (HerbRespawnUI + MonsterLevelLabel)
   430|   430|   430|   430|- Status: ✅
   431|   431|   431|   431|- Details: HerbRespawnUI — 녹색 자연 테마(Glass+Filigree). MonsterLevelLabel — 기본 테마(Stone). GuardWorldSpaceHUD — 이미 UI-13에서 완료. Phase33_Themes.HerbRespawnTheme()/MonsterLevelTheme() 적용.
   432|   432|   432|   432|- Tests: 6개
   433|   433|   433|   433|
   434|   434|   434|   434|---
   435|   435|   435|   435|
   436|   436|   436|   436|## 추가사항 #30: 🏹 활 화살 시스템 (ROADMAP AB-01~06)
   437|   437|   437|   437|
   438|   438|   438|   438|# Cycle: AB-01 — 화살 아이템 데이터
   439|   439|   439|   439|- Status: ✅
   440|   440|   440|   440|- Details: ArrowData.cs — ArrowType enum(Regular/Reinforced/Magic), damageBonus(0/5/15), trailColor, GetItemId(), 정적 프로퍼티
   441|   441|   441|   441|- Tests: ArrowSystemTests.cs — ArrowData 18개 (enum/생성자/displayName/damageBonus/description/rarity/goldCost/trailColor/GetItemId)
   442|   442|   442|   442|
   443|   443|   443|   443|# Cycle: AB-02 — 화살 소모 로직
   444|   444|   444|   444|- Status: ✅
   445|   445|   445|   445|- Details: ArrowManager.GetTotalArrowCount()/AddArrows()/ConsumeBestArrow() — Magic>Reinforced>Regular 우선 소모
   446|   446|   446|   446|- Tests: ArrowSystemTests.cs — 소모 14개 (빈인벤토리/AddArrows/HasArrows/ConsumeBestArrow 우선순위/개수감소)
   447|   447|   447|   447|
   448|   448|   448|   448|# Cycle: AB-03 — 화살 부족 처리
   449|   449|   449|   449|- Status: ✅
   450|   450|   450|   450|- Details: TryShootArrow() — 화살 부족 시 false 반환 + 로그, 화살 있으면 true 반환 + 1개 소모
   451|   451|   451|   451|- Tests: ArrowSystemTests.cs — 부족처리 6개 (빈상태/false/true/소모/마지막/LogAssert)
   452|   452|   452|   452|
   453|   453|   453|   453|# Cycle: AB-04 — 화살 발사체
   454|   454|   454|   454|- Status: ✅
   455|   455|   455|   455|- Details: ArrowProjectile.Spawn() — GameObject 생성(Cylinder), Rigidbody+TrailRenderer, velocity/speed/damage/trailColor 설정
   456|   456|   456|   456|- Tests: ArrowSystemTests.cs — 발사체 12개 (Spawn/컴포넌트/velocity/damage/trailColor/position/방향)
   457|   457|   457|   457|
   458|   458|   458|   458|# Cycle: AB-05 — 발사 궤적 & 획득 경로
   459|   459|   459|   459|- Status: ✅
   460|   460|   460|   460|- Details: ArrowManager.SetSpawnPoint(), _arrowSpeed 필드, 상점/크래프트/몬스터 드랍 연동 구조
   461|   461|   461|   461|- Tests: ArrowSystemTests.cs — 궤적/획득 4개 (SetSpawnPoint/null/arrowSpeed)
   462|   462|   462|   462|
   463|   463|   463|   463|# Cycle: AB-06 — 통합 테스트
   464|   464|   464|   464|- Status: ✅
   465|   465|   465|   465|- Details: AddArrows→TryShootArrow 연결, 다중 화살타입 우선소모, 스택 머징, damageBonus 계산
   466|   466|   466|   466|- Tests: ArrowSystemTests.cs — 통합 8개 (full flow/우선소모/스택/데미지보너스)
   467|   467|   467|   467|- Date: 2026-06-23
   468|   468|   468|   468|
   469|   469|   469|   469|---
   470|   470|   470|   470|
   471|   471|   471|   471|## 추가사항 #31: 🎉 조합 성공 환호 & 결과창 (ROADMAP CR-01~06)
   472|   472|   472|   472|
   473|   473|   473|   473|# Cycle: CR-01 — 환호 애니메이션 데이터
   474|   474|   474|   474|- Status: ✅
   475|   475|   475|   475|- Details: CraftResult enum (Success/Fail_MaterialPreserved/Fail_MaterialDestroyed/Fail_Burned), GetBaseSuccessRate() 등급별 확률
   476|   476|   476|   476|- Tests: CraftCelebrationTests.cs — 데이터 10개 (enum/CraftResult/GetBaseSuccessRate/Common=0.90/Uncommon=0.75/Rare=0.60/Epic=0.45/Legendary=0.30/default)
   477|   477|   477|   477|
   478|   478|   478|   478|# Cycle: CR-02 — 제작 결과창 UI
   479|   479|   479|   479|- Status: ✅
   480|   480|   480|   480|- Details: CraftResultPopup.ShowSuccess(아이템명/등급/효과)/ShowFailure(0/1/2)/OnPopupGUI(), IsShowing, 타이머 자동 Fade Out
   481|   481|   481|   481|- Tests: CraftCelebrationTests.cs — 결과창 11개 (ShowSuccess/ShowFailure/IsShowing/OnPopupGUI/메시지/색상/등급)
   482|   482|   482|   482|
   483|   483|   483|   483|# Cycle: CR-03 — 등급별 색상/효과
   484|   484|   484|   484|- Status: ✅
   485|   485|   485|   485|- Details: EquipmentRarityData.GetRarityColor() / GetRarityDisplayName() — 6개 희귀도 한글명+색상
   486|   486|   486|   486|- Tests: CraftCelebrationTests.cs — 등급색상 6개 (distinct colors/Korean names/all 6 rarities/no duplicates)
   487|   487|   487|   487|
   488|   488|   488|   488|# Cycle: CR-04 — 실패 메시지
   489|   489|   489|   489|- Status: ✅
   490|   490|   490|   490|- Details: ShowFailure(0=재료보존/1=소멸/2=전소), 실패 분포 40%/40%/20%, GetFinalSuccessRate 알케미/요리 보정
   491|   491|   491|   491|- Tests: CraftCelebrationTests.cs — 실패처리 7개 (failType별/GetAlchemyBonus/GetCookingBonus/분포/보정)
   492|   492|   492|   492|
   493|   493|   493|   493|# Cycle: CR-05 — 크래프트 시스템 연동
   494|   494|   494|   494|- Status: ✅
   495|   495|   495|   495|- Details: GetFinalSuccessRate 0~1 클램프, GetGradeFromItemId 접두사 기반 등급 추정, ExecuteCraft 성공률 통계
   496|   496|   496|   496|- Tests: CraftCelebrationTests.cs — 연동 8개 (clamp/GradeFromId/ExecuteCraft/90%통계)
   497|   497|   497|   497|
   498|   498|   498|   498|# Cycle: CR-06 — 통합 테스트
   499|   499|   499|   499|- Status: ✅
   500|   500|   500|   500|- Details: ShowSuccess→타이머→IsShowing false, ShowFailure 각 타입, 연속 호출 스택
   501|
# Cycle: G57-01~03 — URP 설정 향상 (SMAA + Contact Shadows)
- Status: ✅
- Details: GraphicConfigSetup.cs (Editor) + LightProbeSetup.cs (Editor) — SMAA/ContactShadows/라이트프로브/리플렉션프로브
- Date: 2026-07-02

# Cycle: G58-01~02 — URP 데칼 시스템
- Status: ✅
- Details: DecalSpawner.cs (3종+풀링) + DecalSpawnerIntegration.cs (PlayerCombat/AnimalAI 연동)
- Date: 2026-07-02

# Cycle: G59-01~02 — 환경 파티클 시스템
- Status: ✅
- Details: EnvironmentParticleController.cs (Rain/Snow/Fireflies/Dust, 719줄) + GraphicsSetup.cs (Editor)
- Date: 2026-07-02

# Cycle: G60-01~04 — 특수 이펙트 시스템
- Status: ✅
- Details: SpecialEffectsController.cs (929줄, 보석글로우/독안개/무기발광/선택테두리) + GemChest/GasSprayer/GuardPlaceholder 연동
- Date: 2026-07-02

# Cycle: P61-01~03 — 🐉 몬스터 스킬/패턴 시스템
- Status: ✅
- Details: MonsterSkillSystem.cs (10스킬, 12종매핑) + MonsterSkillUI.cs (팝업) + 동물 AI 연동
- Date: 2026-07-02

# Cycle: P62-01~03 — 🗺️ 월드맵 필터 & 영지 상세
- Status: ✅
- Details: MapWindow 패치 (5필터+호버툴팁) + TerritoryInfoPopup.cs (458줄, 영지상세)
- Date: 2026-07-02

# Cycle: P63-01~03 — 🔧 크래프트 프리셋 & 즐겨찾기
- Status: ✅
- Details: CraftPresetManager.cs (300줄) + PresetNamePopup.cs (221줄) + CraftingUI 패치 (프리셋/즐겨찾기)
- Date: 2026-07-02

# Cycle: P64-01~03 — 🗺️ 오토루트 시스템
- Status: ✅
- Details: AutoRouteSystem.cs (368줄, 4매핑) + RouteConfirmationUI.cs (359줄) + InventoryWindow 패치
- Date: 2026-07-02

# Cycle: P65-01~02 — 🔊 컨트롤러 진동
- Status: ✅
- Details: HapticFeedback.cs (186줄, 6프리셋) + 5개 시스템 연동 (PlayerCombat/Health/AnimalAI/Gas/Stealth)
- Date: 2026-07-02

# Cycle: P66-01~03 — ⚙️ 접근성 설정
- Status: ✅
- Details: AccessibilityManager.cs + SettingsMenuUI 접근성탭 + TooltipWindow/NPCDialogue/TutorialGuide 연동
- Date: 2026-07-02

# Cycle: Phase 68 U0 — UI Toolkit 전환 인프라 구축
- Status: ✅
- Details: docs/UI_TOOLKIT_MIGRATION.md U0 — Theme.uss(팔레트 18변수+폰트 5단+공통 클래스)+UnityDefaultTheme.tss+PanelSettings.asset(배치 생성: UIToolkitSetup.Recreate -executeMethod)+UIToolkitBootstrap(BeforeSceneLoad 자가 Ensure)+UTKWindowManager(ESC 스택)+UTKWindowBase(타이틀바 드래그)+UTKControls(Button/Slot/Rarity/Tooltip/Modal/Toast). 배치컴파일 error CS=0(CS0103 1건 수리)
- Date: 2026-09-18

# Cycle: Phase 68 U1 — 파일럿 2창 UTK 포팅(상태창/상점)
- Status: ✅
- Details: StatusWindowUTK(581줄)+ShopWindowUTK(564줄) — additive(기존 IMGUI 무변경). 데이터 소스 직접 호출(PlayerStats BuyDiscount/SellBonus, EquipmentManager, DishDatabase). 배치컴파일 error CS=0(GaugeParts static→sealed, IStyle margin/padding/borderColor 4면 분해 — C# IStyle은 셔스루햇 없음 규약 확립)
- Date: 2026-09-18

# Cycle: Phase 68 U2 — DnD 코어 루프 UTK 전환(인벤/전리품/장비/핫바)
- Status: ✅
- Details: UTKDragDrop(348)+InventoryWindowUTK(451)+LootWindowUTK(320)+EquipmentWindowUTK(363)+HotbarUIUTK(286) = 1,768줄 신규, 원본 5파일 0변경. 핵심 회귀 4경로(전리품 우클릭/드래그/장비해제복귀/즉시갱신) 모두 UTK 경로 구현. 배치컴파일 error CS=0(수리 5건). UTK 폴링 규약: schedule.Execute().Every(ms)→IVisualElementScheduledItem.Pause(). QuickSlotUI는 U3 이월
- Date: 2026-09-18

# Cycle: Phase 68 U3 — 경제/제작 루프 UTK 전환
- Status: ✅
- Details: WarehouseWindowUTK(629, 양방향 DnD)+CraftingWindowUTK(837, 프리셋/즐겨찾기)+AlchemyStationUTK(358)+CookingWindowUTK(393)+RepairStationUTK(297)+QuickSlotUTK(236) = 6파일 2,750줄, 원본 0변경. 배치컴파일 error CS=0(CS7036/CS0191 readonly 8필드/CS1061 borderWidth 4면/using System.Linq 수리). IStyle borderWidth/borderColor 셔스루햇 부재 확정
- Date: 2026-09-18

# Cycle: Phase 68 U4 — 전략/영지 루프 UTK 전환 13창
- Status: ✅
- Details: WorldMap(659)/TerritoryDeployment(298)/TerritoryInfoPopup(284)/Quest(419)/QuestJournal(320)/Encyclopedia(384)/Spy(636)/Envoy(523)/Mercenary(296)/Revenge(385)/FastTravel(296)/Route(207)/AutoMove(258) = 13파일 4,417줄, 원본 0변경. 배치컴파일 error CS=0(수리 7건). static Toggle IsOpen 분기 규약 확정. 월드맵 정규화 u=0.5+x/3200·절차 양피지 이식, 구핫키 런타임 무력화
- Date: 2026-09-18

# Cycle: Phase 68 U5 — 메뉴/시스템 UTK 전환 13창
- Status: ✅
- Details: GuardInfo(480 2분할)+Options(391)+Settings(463 접근성 9API)+MainMenu(379)/Esc(156)/SaveSlot(206)/Load(207)/Loading(237)/Death(timeScale 홀드)+Credits(189 스크롤)+GameStats(242 U키)/Achievement(228 A키)/Tutorial(279 T키) = 13파일 3,407줄, 원본 0변경. 배치컴파일 error CS=0(수리 8건). DropdownField 3-arg 규약/보간문자열 중첩따옴표 금지
- Date: 2026-09-18

# Cycle: Phase 68 U6 — 미니게임/대화/이벤트 UTK 전환 17창
- Status: ✅
- Details: 대화4(NPCDialogue/QuestChoice/ReadDocument/LordAudience)+미니게임4(Lockpicking/Fishing/Mercy/Sleep 수면위임+침대세이브)+아레나2+MissionResult(자체큐)+이벤트3(Festival/DynamicEvent/NPCDaily)+깃발/가스/교회(2원본통합) = 17파일 ~4,030줄, 원본 0변경. 배치컴파일 error CS=0(barText Label/TerritoryDefinition 구조체 null비교 수리). 구조체 반환 API null비교 금지 규약
- Date: 2026-09-18

# Cycle: Phase 68 U7 — HUD/오버레이 UTK 7창 + 유지 판정
- Status: ✅
- Details: HUD(415 체력/퀵슬롯/EXP)+GuardSquadHotbar(428 리플렉션 공유)+Minimap(440 스플랫+마커)+Time(162)/War(173)/CombatLog(224 L키)/Herb(233) = 7파일 2,075줄, 원본 0변경. 배치컴파일 error CS=0(LogType 한정/backgroundPosition/textOverflow 수리). 유지 확정: 월드스페이스 6종+디버그 IMGUI. 호출부 배선은 U8
- Date: 2026-09-18

# Cycle: Phase 68 U8 — 호출부 배선(이벤트 브리지)+UTK 가이드 확정
- Status: 🔄 (Play 검증 후 폐기 단계 잔여)
- Details: Bed/LootBasket에 OnXxxRequestedUTK 정적 이벤트+UTKWireUp 구독 브리지(UTK 우선·원본 폴백 내장·제거 한 줄 회귀). UI_DESIGN_GUIDELINES에 UTK 표준 섹션(팔레트 매핑/엔진 규약 8종). 배치컴파일 error CS=0(CS0019/CS0079/캐스트 수리). 후속: 배선 확대/HUD 겹침 해소/Play 검증/폐기
- Date: 2026-09-18

# Cycle: Phase 68 Play 1차 실측 — UTK 렌더 정상 + 예외 수리
- Status: ✅
- Details: Editor.log 실측 — Status(P)/Quest(Q)/Squad UTK 정상 토글, 부트+배선 로그 착륙, UTK 예외 0. ProceduralAnimationController.RequestGather 파괴 후 접근 가드 추가(MissingReferenceException 뿌리 수리)
- Date: 2026-09-18

# Cycle: Phase 68 배선 확대 — 4경로(수면/전리품/상점/인벤 I키)
- Status: ✅
- Details: ShopPlaceholder.ToggleShopRequestedUTK+InvokeLegacyToggleShop 폴백 분리, UIInventoryHotkey.InventoryToggleRequestedUTK 라우팅, UTKWireUp 브리지 2건 추가(총 4경로). UTK 미준비 시 원본 폴백 내장. 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 Play 수리 — 인벤 I키 미표시 + 미니맵 우상단
- Status: ✅
- Details: ①InventoryWindowUTK.Toggle이 IsOpen=false일 때 Ensure()만 호출(Show 누락) → Open() 위임 수리(레퍼런스 흐름: WireUp→Toggle→Open→Show) ②MinimapUTK 좌상단→우상단(style.right, 사용자 지정). 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 Play 수리 — 창고 UTK 배선+장비창 임베드
- Status: ✅
- Details: ①TerritoryWarehouse.OpenWarehouseUI UTK 우선(인벤+창고 동시 오픈) ②InventoryWindowUTK 좌우 2컬럼 확장 — 우측 장비 8슬롯(무기/방패/투구/갑옷/신발/장갑/가면/가방)+우클릭 UnequipSlot+RefreshEquipPanel(Show/이벤트). EquipmentManager 중첩 enum 한정/Boots→Shoes 수리. 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 인벤 UTK 3분할 레이아웃 (예시 2 정합)
- Status: ✅
- Details: 좌=장비 2줄×5칸(8슬롯+빈2, 우클릭 해제)+가방 6줄×5칸 / 중=아이템 설명창(ShowItemDescription 공개: 이름·설명·카테고리·등급·내구도) / 우=창고 패널(SetWarehouseMode 표시·숨김, 미상호작용 안내 문구). 창 1080×620. 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 독립 창 개편 + DnD/우클릭 수리 + 은퇴 게이트
- Status: ✅
- Details: ①ItemDescriptionWindowUTK 신규(독립 설명창) ②InventoryWindowUTK 단일 패널 회귀(장비 2x5+가방 6x5) ③I키=인벤+설명 쌍 토글(UTKWireUp) ④창고→인벤 DnD(CanDrop/Drop Warehouse 수용→TransferToInventory) ⑤가방 우클릭(소모품 UseItem/장착 TryEquipItemPublic 위임 래퍼) ⑥TerritoryWarehouse 닫기 UTK 분기 ⑦원본 키 은퇴 게이트 4종(Status P/Ency L/GameStats U/Journal J — UTK 준비 시 원본 키 무시). 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 폐기 1단계 — 은퇴 게이트 전면 확장+태그
- Status: ✅
- Details: Core.UITransitionState.UtkActive 플래그(순환참조 회피 양 어셈블리 공용)+부트스트랩 세팅. 원본 IMGUI 자가 은퇴 8종: HUD(부분 은퇴 — 체력/EXP, 드래고 고스트/버프/가스/은신/사망 유지)/Minimap/QuickSlot/CombatLog/HerbRespawn/Time(Systems)/WarNote(Systems). git 태그 phase68-ui-toolkit 푸시. 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 드래그 뿌리 수리 + 폐기 2단계(1/2)
- Status: ✅
- Details: ①드래그 불능 뿌리 — UTKDragDrop evt.position은 캡처 엘리먼트 로컬 좌표(루트 좌표 아님) → ToRootPos 변환(LocalToWorld→root.WorldToLocal)으로 고스트/드롭 판정 정합 ②참조 0 검증(씬+프리팹+코드) 후 원본 13종 Assets 밖 LegacyUI_Archive/ 아카이브 ③WorldMapWindowUTK 폴백·핫키 억제 코드 정리. 배치컴파일 error CS=0. 잔여 원본은 은퇴 게이트 유지
- Date: 2026-09-18

# Cycle: Phase 68 드래그 마지막 부착 수리 + 폐기 2단계(2/2) 결론
- Status: ✅
- Details: ①OnDragPointerUp 순서 버그 수리 — ReleasePointer 동기 발화 PointerCaptureOut이 Cancel+Complete 섭취(마지막 부착 불능 뿌리) → 세션 선분리 ②잔존 원본 전수 참조 분석 — 전부 라이브 시스템 참조로 파일 유지·런타임 은퇴(게이트/브리지)로 전환 완결 결정. 무리한 삭제는 시스템 대규모 수정 유발. 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 요구 대량 반영 — 좌클릭 게이트/우클릭 드래그/행 배치/글래스 테마
- Status: ✅
- Details: ①PointerOverUI 플래그(UTKInputGate panel.Pick, UTKWindowManager 갱신)→PlayerCombat 좌클릭 게이트+TopDownCamera 우드래그 게이트 — UI 위 클릭이 공격/카메라로 소비되는 문제 차단 ②UTKDragDrop 우클릭 드래그(버튼 0/1)+onRightClick(비드래그=사용/장착) ③행 배치: 인벤(16, 520×680, 슬롯 64)→설명(544, 340×680, 아이콘 프리뷰 140)→창고(892) ④구 uGUI HotbarUI 은퇴(UtkActive 게이트, HotbarUIUTK 유지) ⑤Theme.uss 글래스모피즘 다크네이비+시안 라인(변수명 유지). 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 Play 수리 3종 — 드래그 캡처 가드/창고 전용화+탭/핫바 Tab 전환
- Status: ✅
- Details: ①RefreshGrid 드래그 중 재생성 금지(캡처 상실→Cancel 뿌리 차단) ②WarehouseWindowUTK 인벤 컬럼 은닉(창고 전용)+카테고리 탭 필터(CategoryMatches) ③HotbarUIUTK IsSquadMode 연동 표시 전환(Tab). 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 테마 재지정(다크 브라운+앤틱 골드)+HUDUTK 퀵슬롯 정리
- Status: ✅
- Details: Theme.uss — 다크 브라운 아크릴 rgba(38,26,16,0.84)+앤틱 골드 #B08D4A/#E8C877 라인+웜 화이트 폰트+슬롯 음각(상단 어두움/하단 골드 하이라이트)+hover 골드 글로우. HUDUTK 퀵슬롯 섹션 제거(중복 슬롯 은퇴) — 파손된 ctor/필드 복구(readonly/_levelText/_expFill/_expText/_expValueText/_pollTask). 설명창 420px 확장. 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 수정안 실행 — 드래그 좌표 단일 경로+창고 우클릭+핫바 전환+레이아웃 확정
- Status: ✅
- Details: ①UTKDragDrop.GetPanelPointerPos — 마우스 실측 픽셀×패널스케일(pos=2151 뿌리 차단), ToRootPos 폐기 ②창고 셀 우클릭 출고 onRightClick 위임(ContextClickEvent 섭취 수리) ③핫바 가시 전환 매 프레임(IsSquadMode) ④레이아웃: 인벤(16,560×700)/설명(596,440×700)/창고(1044). 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 H1~H4 — 핫바 단일 바/HUD 클러스터/우클릭 컨텍스트/병사 배선
- Status: ✅
- Details: ①HotbarUIUTK Tab 직접 폴링(_squadMode) — 아이템↔부대 8슬롯 전환, 부대 렌더=원본 _slots 리플렉션(GuardIconRenderer 아바타) ②GuardSquadHotbarUTK 은퇴(부대 렌더 흡수) ③원본 GuardSquadHotbar 드로잉 은퇴 게이트(UtkActive — Tab/등록/선택 로직 유지) ④HUDUTK 하단 좌측 클러스터(체력 골드 프레임+스태미너 계단식 StaminaRatio+원형 자원 아이콘 2종) ⑤인벤 우클릭 컨텍스트(창고 열림=입고 DepositFromInventory) ⑥부대 슬롯 좌클릭→GuardInfoUTK 배선. 배치컴파일 error CS=0(borderRadius USS 미지원 제거)
- Date: 2026-09-18

# Cycle: Phase 68 Play 수리 7건 — 퀵슬롯 잔재/공격 불능/전리품 드래그/우클릭/클러스터/펄스 애니
- Status: ✅
- Details: ①QuickSlotUTK 부트 은퇴(UtkActive) ②Active 유착 자가 해제(버튼 해제 시 강제 Cancel)+HUDUTK 풀스크린 Ignore 오버레이(0×0 박스→화면 기준 클러스터 렌더 수리) ③LootWindowUTK 폴링 가드+우클릭 중복 핸들러 제거 ④우클릭=PointerDown 즉시 발화 확정(드래그는 좌클릭 전용) ⑤핫바 전환 스케일 펄스 애니(1.15→1.0 150ms) ⑥Complete try/catch 유착 방지. 배치컴파일 error CS=0
- Date: 2026-09-18

# Cycle: Phase 68 리포트 반영 5건 — 동시 표시/원형 게이지/치유초 회복/F키/부대 틴트
- Status: ✅
- Details: ①전리품 열림 시 인벤+설명 동시 표시(UTKWireUp) ②원형 게이지 링 구조(골드 보더+overflow Hidden+하단 채움 — HP/스태미너 비율) ③ConsumableSystem Herb 분기(25HP 회복) ④SoldierInteractBridge(F키 OverlapSphere 최근접 병사→GuardInfoUTK.Open) ⑤부대 모드 슬롯 청록 틴트. 배치컴파일 error CS=0
- Date: 2026-09-18
