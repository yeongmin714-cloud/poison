# 새 PC 셋업 가이드 (New PC Setup)

이 게임(포이즌/Poison)은 **코드·설정은 GitHub, 대용량 GLB 모델은 별도 백업**으로 분리 관리한다.

- GitHub 저장소: `https://github.com/yeongmin714-cloud/poison.git`
- 백업 필요: **`Assets/새로운 glb/`** 폴더 (GLB ~1,000여 개 — 몬스터/지형/작물/가구 등)
  - `.gitignore` 때문에 GitHub에 **없음**. 이 폴더를 포함한 `Assets/` 백업을 따로 보관한다.

> Assets 외(코드·씬·리소스·Packages·ProjectSettings)는 전부 GitHub에 있으므로 clone만으로 받아진다.

---

## 1. Unity 설치
버전 반드시 **Unity 6000.4.10f1** — 다른 버전이면 프로젝트/URP 렌더러/완성도가 안 맞을 수 있다.

---

## 2. GitHub에서 클론 (Git LFS 포함)
```bash
git lfs install
git clone https://github.com/yeongmin714-cloud/poison.git
cd poison
git lfs pull
```

---

## 3. 백업에서 GLB 모델 복원
백업 안의 `Assets/새로운 glb/` 전체를 프로젝트 `Assets/` 안으로 복사한다.

```bash
# 경로는 백업 위치에 맞게 조정
cp -r "<백업경로>/Assets/새로운 glb"  "poison/Assets/"
```

> 백업이 `Assets/` **전체**면 `poison/Assets/`에 통째로 덮어써도 된다.
> (함께 있는 `Assets/Models/UserProvided_Archive` 등 아카이브도 그대로 복원)

---

## 4. Unity로 프로젝트 열기
1. Unity Hub → Add → `poison` 폴더 선택
2. **Unity 6000.4.10f1** 로 열기
3. `Library/` 자동 생성됨 (첫 열기는 오래 걸릴 수 있음)
4. `Assets/Scenes/MainScene.unity` 열고 Play

---

## ⚠️ 유의사항
- **모델이 비어 보이면** → `Assets/새로운 glb/` 복원 누락. 다시 3번 수행.
- **GLB 콜라이더/애니 문제 없음** = 백업 복원 정상.
- 그래픽 품질은 `ProjectSettings/QualitySettings.asset`(`m_CurrentQuality`)로 조정. 저사양이면 Very High(4) 유지.