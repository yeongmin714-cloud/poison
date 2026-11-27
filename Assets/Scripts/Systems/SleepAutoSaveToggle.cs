using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [73차] 수면 전 자동 저장 스위치 — 피그마 227:262 "수면 시스템 패널"의
    /// "수면 전 자동 저장" 토글(38×22 스위치) 백엔드.
    /// 계획서: docs/2026-10-15_73TH_SLEEP_PANEL_AND_DIALOGUE_PLAN.md Phase A-2.
    ///
    /// 계약:
    ///   - static bool Enabled (기본 ON — 피그마 스위치 "켬" 목업)
    ///   - PlayerPrefs 영속 키 "poison_sleep_autosave"
    ///   - OnChanged(bool) 이벤트 — UI 스위치 갱신용
    ///   - SleepPanelUTK가 수면 시작 직전 Enabled 검사 후 SaveManager.AutoSave() 게이트
    /// </summary>
    public static class SleepAutoSaveToggle
    {
        /// <summary>PlayerPrefs 영속 키 — [73차] "poison_sleep_autosave".</summary>
        public const string PrefKey = "poison_sleep_autosave";

        private static bool _loaded;
        private static bool _enabled = true;   // 미설정 기본 ON

        /// <summary>수면 전 자동 저장 스위치 상태. 최초 접근 시 PlayerPrefs에서 로드.</summary>
        public static bool Enabled
        {
            get
            {
                EnsureLoaded();
                return _enabled;
            }
        }

        /// <summary>상태 변경 이벤트 — 토글 UI 갱신용.</summary>
        public static event Action<bool> OnChanged;

        /// <summary>스위치 상태 설정(영속). 같은 값이면 이벤트 없음(멱등).</summary>
        public static void SetEnabled(bool on)
        {
            EnsureLoaded();
            if (_enabled == on)
                return;
            _enabled = on;
            Persist();
            Debug.Log($"[SleepAutoSaveToggle] 수면 전 자동 저장 = {(on ? "켬" : "끔")} (PlayerPrefs 영속)");
            OnChanged?.Invoke(on);
        }

        /// <summary>토글 편의 — 현재 상태를 반전 후 새 상태 반환.</summary>
        public static bool Toggle()
        {
            SetEnabled(!Enabled);
            return Enabled;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;
            _enabled = PlayerPrefs.GetInt(PrefKey, 1) != 0;   // 미설정 = 기본 ON
        }

        private static void Persist()
        {
            PlayerPrefs.SetInt(PrefKey, _enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}