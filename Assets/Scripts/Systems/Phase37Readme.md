# Phase 37: 환경 스토리텔링 시스템 (Environmental Storytelling)

## 개요
씬에 배치된 오브젝트를 통해 세계관과 스토리를 전달하는 시스템.
문서/편지, 환경 스토리 요소(시체+메모, 혈흔, 저주 물건, 묘비),
NPC 일상 루틴 대사의 3개 카테고리로 구성됩니다.

## 구현 파일 (11개)

### 37.1 — 읽을 수 있는 문서/편지
| 파일 | 설명 |
|------|------|
| `ReadableDocument.cs` | ScriptableObject — 문서ID, 제목, 내용, 발견위치, 중요도, 분류 |
| `InteractableDocument.cs` | MonoBehaviour — 씬 상호작용 문서 (E키), 파티클 하이라이트 |
| `ReadDocumentWindow.cs` | IMGUI 팝업 — 문서 읽기 UI (namespace ProjectName.UI) |

- 문서 분류: Letter(편지), Diary(일기), OfficialDoc(공문), Scroll(스크롤), Wanted(현상수배)
- 중요도: Normal(일반/흰색), Important(중요/황금빛), QuestRequired(퀘스트/청록색)
- ParticleSystem 기반 하이라이트 효과
- AmbientDialogueManager 연동 (최초 발견 등록)

### 37.2 — 환경 스토리 요소
| 파일 | 설명 |
|------|------|
| `DeadBodyWithNote.cs` | 시체 + 메모 조합, Fade Out, BloodStain 연동 |
| `BloodStain.cs` | 바닥 혈흔 (Quad/Decal 머티리얼), 4종 타입 + Fade |
| `CursedObject.cs` | 저주받은 물건 — 접근 시 속삭임/효과음/시각 효과 |
| `Gravestone.cs` | 묘비 — E키로 묘비명/생몰년/비문 표시 (IMGUI) |

- BloodStain 타입: Fresh(신선), Dried(마름), Massive(대량), Trail(흔적)
- CursedObject: PlayerPrefs SFXVolume 존중, 근접도 기반 효과 강도
- Gravestone: 내장 GravestoneData 클래스로 데이터 저장

### 37.3 — NPC 일상 루틴 대화
| 파일 | 설명 |
|------|------|
| `NpcDialogueData.cs` | ScriptableObject — 시간대별/날씨별 NPC 대사 데이터 |
| `NPCAmbientDialogue.cs` | MonoBehaviour — 자동 대사 + E키 상호작용 대사 |
| `AmbientDialogueManager.cs` | 싱글톤 — 발견 기록 추적, 저장/로드, 이벤트 시스템 |

- 시간대: 아침(5~11), 오후(11~17), 저녁(17~21), 밤(21~5)
- 날씨 대사: 비/눈 특수 대사 지원 (WeatherManager 연동)
- 자동 대사 간격: 30~90초 랜덤
- 세계관 풍부화를 위한 10개 이상 NPC 대사 데이터 템플릿

### 공통
| 파일 | 설명 |
|------|------|
| `Phase37Readme.md` | 본 문서 — 구현 내용 요약 |

## 연동 사항
- **TimeManager**: 시간대별 NPC 대사 변화
- **DayNightCycle**: 간접 참조 (시간대 판별)
- **WeatherManager**: 날씨별 특수 NPC 대사
- **SoundEffectManager/AudioSource**: 사운드 재생
- **PlayerPrefs**: CursedObject 사운드 볼륨
- **Phase 42 (도감/퀘스트)**: AmbientDialogueManager의 이벤트/세이브 데이터 연동 준비

## 사용 예시
```csharp
// 문서 ScriptableObject 생성
// Assets > Create > Environmental > ReadableDocument

// 씬 배치
// 1. InteractableDocument 컴포넌트를 빈 GameObject에 추가
// 2. Document Data 필드에 ReadableDocument SO 연결
// 3. ParticleSystem 할당 (선택)
// 4. SphereCollider 자동 생성 (2.5m 반경)

// NPC 대사 데이터
// Assets > Create > Environmental > NpcDialogueData
// NPCAmbientDialogue.cs를 NPC 프리팹에 추가

// 환경 스토리 요소
// DeadBodyWithNote, BloodStain, CursedObject, Gravestone 각각 씬 배치

// AmbientDialogueManager
// 자동 생성 (싱글톤, DontDestroyOnLoad)
```