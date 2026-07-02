using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 🐉 몬스터 스킬 UI (IMGUI 싱글톤)
    /// - 타겟팅한 몬스터의 이름 + 스킬명을 HP 바 아래 표시
    /// - 스킬 피격 시 팝업 표시 (예: "🔥 화염구!")
    /// </summary>
    public class MonsterSkillUI : MonoBehaviour
    {
        private static MonsterSkillUI _instance;
        public static MonsterSkillUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MonsterSkillUI");
                    _instance = go.AddComponent<MonsterSkillUI>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Skill Popup Settings")]
        [SerializeField] private float _popupDuration = 2f;
        [SerializeField] private int _popupFontSize = 36;
        [SerializeField] private Color _popupTextColor = Color.white;
        [SerializeField] private Color _popupShadowColor = new Color(0f, 0f, 0f, 0.8f);

        [Header("Monster Info Panel")]
        [SerializeField] private int _infoPanelWidth = 300;
        [SerializeField] private int _infoPanelHeight = 60;
        [SerializeField] private int _infoFontSize = 20;
        [SerializeField] private Color _infoTextColor = Color.white;

        [Header("Position")]
        [SerializeField] private int _infoPanelX = 40; // HP 바와 같은 X
        [SerializeField] private int _infoPanelY = 0;  // 동적 계산

        // ===== 스킬 팝업 상태 =====
        private class SkillPopup
        {
            public string displayName; // "🔥 화염구!"
            public float startTime;
            public float duration;
            public Vector2 screenPosition; // 화면 중앙 부근
        }

        private readonly List<SkillPopup> _activePopups = new List<SkillPopup>();

        // ===== 몬스터 정보 표시 =====
        private string _targetMonsterName = "";
        private string _targetSkillNames = "";
        private bool _hasTarget = false;
        private float _lastTargetCheckTime;

        // ===== 캐싱 =====
        private GUIStyle _cachedPopupStyle;
        private GUIStyle _cachedPopupShadowStyle;
        private GUIStyle _cachedInfoTitleStyle;
        private GUIStyle _cachedInfoSkillStyle;
        private Texture2D _whitePixelTex;

        // ===== 이벤트 구독 =====
        private System.Action<Systems.AnimalAI, Systems.MonsterSkillSystem.MonsterSkill, string> _skillHandler;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            // MonsterSkillSystem.OnSkillExecuted 구독
            _skillHandler = OnMonsterSkillExecuted;
            Systems.MonsterSkillSystem.OnSkillExecuted += _skillHandler;
        }

        private void OnDestroy()
        {
            if (_skillHandler != null)
                Systems.MonsterSkillSystem.OnSkillExecuted -= _skillHandler;
        }

        /// <summary>
        /// MonsterSkillSystem에서 스킬 실행 시 호출
        /// </summary>
        private void OnMonsterSkillExecuted(Systems.AnimalAI monster, Systems.MonsterSkillSystem.MonsterSkill skill, string monsterName)
        {
            string skillDisplay = Systems.MonsterSkillSystem.GetSkillDisplayName(skill);
            string popupText = $"{skillDisplay}!"; // 예: "🔥 화염구!"

            // 화면 위치: 중앙
            Vector2 screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.4f);

            // 몬스터가 화면에 보이면 월드 위치 기준으로 표시
            if (monster != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 worldPos = monster.transform.position + Vector3.up * 2f;
                    Vector3 viewportPos = cam.WorldToViewportPoint(worldPos);
                    if (viewportPos.z > 0 && viewportPos.x > 0 && viewportPos.x < 1 && viewportPos.y > 0 && viewportPos.y < 1)
                    {
                        Vector3 screenPoint = cam.WorldToScreenPoint(worldPos);
                        screenPos = new Vector2(screenPoint.x, Screen.height - screenPoint.y - 60f);
                    }
                }
            }

            _activePopups.Add(new SkillPopup
            {
                displayName = popupText,
                startTime = Time.time,
                duration = _popupDuration,
                screenPosition = screenPos
            });

            // 최대 5개까지만 유지
            if (_activePopups.Count > 5)
                _activePopups.RemoveAt(0);
        }

        // ===== 타겟 몬스터 정보 업데이트 =====

        /// <summary>
        /// 현재 타겟팅된 몬스터의 스킬 정보를 갱신합니다.
        /// PlayerCombat.CurrentTarget 기준으로 표시합니다.
        /// </summary>
        private void UpdateTargetInfo()
        {
            // 주기적으로만 갱신 (성능)
            if (Time.time - _lastTargetCheckTime < 0.3f) return;
            _lastTargetCheckTime = Time.time;

            _hasTarget = false;
            _targetMonsterName = "";
            _targetSkillNames = "";

            // PlayerCombat에서 현재 타겟 확인
            var playerCombat = Systems.PlayerCombat.Instance;
            if (playerCombat == null) return;

            var target = playerCombat.CurrentTarget;
            if (target == null || !target.IsAlive) return;

            // IDamageable → AnimalAI 캐스팅
            Systems.AnimalAI monsterAI = target as Systems.AnimalAI;
            if (monsterAI == null) return;

            _hasTarget = true;

            // 몬스터 이름
            MonsterDef def = MonsterDatabase.Get(monsterAI.MonsterId);
            string monsterName = def != null ? def.displayName : monsterAI.MonsterId;
            _targetMonsterName = monsterName;

            // 스킬 목록
            var skillSystem = Systems.MonsterSkillSystem.Instance;
            var skills = skillSystem.GetSkillsForAI(monsterAI);

            if (skills.Length == 0)
            {
                _targetSkillNames = "기본 공격";
            }
            else
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var s in skills)
                {
                    names.Add(Systems.MonsterSkillSystem.GetSkillDisplayName(s.skill));
                }
                _targetSkillNames = string.Join(", ", names);
            }
        }

        // ===== OnGUI =====

        private void OnGUI()
        {
            // 스타일 캐싱
            if (_cachedPopupStyle == null)
                CacheStyles();

            // 팝업 정리 (만료된 것 제거)
            float now = Time.time;
            _activePopups.RemoveAll(p => now - p.startTime >= p.duration);

            // 스킬 팝업 그리기
            DrawSkillPopups();

            // 타겟 정보 업데이트 및 그리기
            UpdateTargetInfo();
            DrawTargetInfo();
        }

        private void CacheStyles()
        {
            _cachedPopupStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _popupFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _popupTextColor }
            };

            _cachedPopupShadowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _popupFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _popupShadowColor }
            };

            _cachedInfoTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _infoFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _infoTextColor }
            };

            _cachedInfoSkillStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _infoFontSize - 2,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.8f, 0.2f) } // 노란색
            };

            _whitePixelTex = new Texture2D(1, 1);
            _whitePixelTex.SetPixel(0, 0, Color.white);
            _whitePixelTex.Apply();
        }

        // ===== 스킬 팝업 그리기 =====

        private void DrawSkillPopups()
        {
            foreach (var popup in _activePopups)
            {
                float elapsed = Time.time - popup.startTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / popup.duration);

                // 위로 떠오르는 효과
                float offsetY = Mathf.Lerp(0f, -40f, elapsed / popup.duration);
                Vector2 pos = popup.screenPosition + new Vector2(0, offsetY);

                // GUIContent 크기 계산
                GUIContent content = new GUIContent(popup.displayName);
                Vector2 textSize = _cachedPopupStyle.CalcSize(content);

                Rect rect = new Rect(
                    pos.x - textSize.x * 0.5f,
                    pos.y - textSize.y * 0.5f,
                    textSize.x,
                    textSize.y
                );

                // 알파 적용
                Color prevColor = GUI.color;

                // 그림자
                GUI.color = new Color(0, 0, 0, alpha * 0.8f);
                GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), content, _cachedPopupShadowStyle);

                // 본문
                GUI.color = new Color(_popupTextColor.r, _popupTextColor.g, _popupTextColor.b, alpha);
                GUI.Label(rect, content, _cachedPopupStyle);

                GUI.color = prevColor;
            }
        }

        // ===== 타겟 몬스터 정보 패널 =====

        private void DrawTargetInfo()
        {
            if (!_hasTarget) return;

            // HP 바 아래 위치 (HUD의 HP 바 Y + 높이 + 약간의 여백)
            _infoPanelY = Screen.height - 70 - 60 - _infoPanelHeight - 10;

            float panelX = _infoPanelX;
            float panelY = _infoPanelY;
            float panelW = _infoPanelWidth;
            float panelH = _infoPanelHeight;

            // 배경 (반투명 검정)
            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), _whitePixelTex);
            GUI.color = prevColor;

            // 테두리
            GUI.Box(new Rect(panelX, panelY, panelW, panelH), "");

            // 몬스터 이름
            float labelY = panelY + 5;
            GUI.Label(new Rect(panelX + 10, labelY, panelW - 20, 24), $"🎯 {_targetMonsterName}", _cachedInfoTitleStyle);

            // 스킬명
            float skillY = labelY + 24;
            GUI.Label(new Rect(panelX + 10, skillY, panelW - 20, 22), $"⚔️ {_targetSkillNames}", _cachedInfoSkillStyle);
        }

        // ===== 퍼블릭 API: 외부에서 팝업 트리거 =====

        /// <summary>
        /// 스킬 팝업을 수동으로 표시합니다.
        /// </summary>
        public void ShowSkillPopup(string skillDisplayName, Vector3 worldPosition)
        {
            Camera cam = Camera.main;
            Vector2 screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.4f);

            if (cam != null)
            {
                Vector3 viewportPos = cam.WorldToViewportPoint(worldPosition);
                if (viewportPos.z > 0)
                {
                    Vector3 screenPoint = cam.WorldToScreenPoint(worldPosition);
                    screenPos = new Vector2(screenPoint.x, Screen.height - screenPoint.y - 60f);
                }
            }

            _activePopups.Add(new SkillPopup
            {
                displayName = skillDisplayName,
                startTime = Time.time,
                duration = _popupDuration,
                screenPosition = screenPos
            });

            if (_activePopups.Count > 5)
                _activePopups.RemoveAt(0);
        }
    }
}