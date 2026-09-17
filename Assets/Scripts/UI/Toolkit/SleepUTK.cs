using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 — 수면 UI (원본 SleepUI.cs 379줄 포팅, additive).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 원본 데이터 경로 유지:
    ///   - 수면 실행: SleepUI.Instance.StartSleep(hours) 리플렉션 위임(원본 코루틴·시간 진행 단일 소스)
    ///   - 침대 세이브: Bed.SetSpawnPoint(bed.position) + SaveManager.AutoSave() — 정적 API 직접 호출
    ///   - 아침까지: hours=-1로 위임(원본이 CalculateTimeUntilMorning 처리)
    /// [SleepUTK] 실측 로그.
    /// </summary>
    public class SleepUTK : VisualElement
    {
        private static SleepUTK _instance;
        private Bed _currentBed;

        private SleepUTK()
        {
            style.position = Position.Absolute;
            style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.75f));
            style.display = DisplayStyle.None;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            pickingMode = PickingMode.Position;

            var panel = new VisualElement();
            panel.style.backgroundColor = new StyleColor(new Color(0.09f, 0.09f, 0.11f, 0.95f));
            panel.style.borderTopWidth = 2; panel.style.borderBottomWidth = 2;
            panel.style.borderLeftWidth = 2; panel.style.borderRightWidth = 2;
            panel.style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
            panel.style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
            panel.style.borderLeftColor = new StyleColor(UTKColor.BorderBronze);
            panel.style.borderRightColor = new StyleColor(UTKColor.BorderBronze);
            panel.style.paddingTop = 16; panel.style.paddingBottom = 16;
            panel.style.paddingLeft = 20; panel.style.paddingRight = 20;
            Add(panel);

            var title = new Label("🛏 수면");
            title.style.fontSize = 38;
            title.style.color = new StyleColor(UTKColor.TextPrimary);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12;
            panel.Add(title);

            foreach (var (label, hours) in new[] {
                ("2시간 자기", 2f), ("4시간 자기", 4f), ("6시간 자기", 6f),
                ("8시간 자기", 8f), ("아침까지 자기", -1f) })
            {
                float h = hours;
                var btn = UTKButton.Create(label, () => StartSleep(h), UTKButton.Variant.Secondary);
                btn.style.width = 240; btn.style.height = 40; btn.style.marginBottom = 6;
                panel.Add(btn);
            }

            var saveBtn = UTKButton.Create("💾 여기서 세이브", SaveAtBed, UTKButton.Variant.Primary);
            saveBtn.style.width = 240; saveBtn.style.height = 40; saveBtn.style.marginTop = 10; saveBtn.style.marginBottom = 6;
            panel.Add(saveBtn);

            var cancelBtn = UTKButton.Create("취소", Hide, UTKButton.Variant.Danger);
            cancelBtn.style.width = 240; cancelBtn.style.height = 36;
            panel.Add(cancelBtn);

            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        /// <summary>UTK 루트 부착 인스턴스 보장(멱등).</summary>
        public static SleepUTK Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new SleepUTK();
            root.Add(_instance);
            Debug.Log("[SleepUTK] 수면 UI 인스턴스 생성");
            return _instance;
        }

        /// <summary>수면 UI 표시 — 침대 참조 필수(원본 Show(Bed) 동일).</summary>
        public static void Open(Bed bed)
        {
            var i = Ensure();
            if (i == null || bed == null) return;
            i._currentBed = bed;
            i.style.display = DisplayStyle.Flex;
            i.BringToFront();
            Debug.Log("[SleepUTK] 수면 UI 표시 — 침대 " + bed.name);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.style.display = DisplayStyle.None;
            Debug.Log("[SleepUTK] 수면 UI 닫기");
        }

        private static void StartSleep(float hours)
        {
            // 원본 단일 소스 위임 — SleepUI.StartSleep(NonPublic) 리플렉션 호출
            var sleep = SleepUI.Instance;
            if (sleep != null)
            {
                var m = typeof(SleepUI).GetMethod("StartSleep",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (m != null)
                {
                    m.Invoke(sleep, new object[] { hours });
                    Hide();
                    Debug.Log("[SleepUTK] 수면 위임 — " + (hours > 0 ? hours + "시간" : "아침까지"));
                    return;
                }
            }
            Debug.LogWarning("[SleepUTK] SleepUI 미존재/StartSleep 미발견 — 수면 취소");
        }

        private static void SaveAtBed()
        {
            // 원본 데이터 경로 동일 — 스폰핀 기록 + 자동 저장
            var i = _instance;
            if (i == null || i._currentBed == null)
            {
                Debug.LogWarning("[SleepUTK] 세이브 실패: 현재 침대 참조 없음");
                return;
            }
            Bed.SetSpawnPoint(i._currentBed.transform.position);
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.AutoSave();
                UTKToastService.Show("💾 세이브 완료! 사망 시 이 침대에서 부활", 2.5f);
                Debug.Log("[SleepUTK] 침대 세이브 완료");
            }
            else
            {
                Debug.LogWarning("[SleepUTK] SaveManager 없음 — 스폰핀만 설정됨");
            }
        }
    }
}
