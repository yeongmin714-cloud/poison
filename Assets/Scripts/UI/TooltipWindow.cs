using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// IMGUI 기반 아이템 툴팁 시스템.
    /// 마우스를 아이템 슬롯 위에 올리면 지연 후 툴팁을 표시하고,
    /// 마우스가 떠나면 빠르게 사라집니다.
    /// 
    /// 사용법:
    ///   TooltipWindow.Instance.ShowTooltip(slot.ToTooltipData(), mousePos);
    ///   매 프레임 TooltipWindow.Instance.OnGUI() 호출 (자동 숨김 관리)
    /// </summary>
    public class TooltipWindow : MonoBehaviour
    {
        [Header("Tooltip Settings")]
        [SerializeField] private float _showDelay = 0.3f;     // 표시 지연 (초)
        [SerializeField] private float _hideDelay = 0.1f;      // 숨김 지연 (초)
        [SerializeField] private float _tooltipWidth = 360;
        [SerializeField] private float _padding = 6f;
        [SerializeField] private Vector2 _mouseOffset = new Vector2(18f, 18f); // 마우스로부터 오프셋

        [Header("Phase 33 Theme")]
        [SerializeField] private UIDesignTheme _theme;

        // 싱글톤
        private static TooltipWindow _instance;
        public static TooltipWindow Instance => _instance;

        // 툴팁 상태
        private ItemTooltipData _tooltipData;
        private bool _hasTooltip;
        private float _mouseEnterTime;
        private float _mouseLeaveTime;
        private bool _isShowing;
        private bool _wasHovering;
        private Vector2 _lastMousePos;
        private int _lastShowFrame = -1;  // ShowTooltip이 호출된 마지막 프레임

        // 비교 툴팁 데이터 (장비 아이템 비교용)
        private ItemTooltipData _compareItem;

        // ===== GUIStyle 캐시 =====
        private GUIStyle _styleItemName;
        private GUIStyle _styleRarityLabel;
        private GUIStyle _styleDescription;
        private GUIStyle _styleEffects;
        private GUIStyle _styleCategoryLabel;
        private GUIStyle _styleCountLabel;
        private bool _stylesInitialized;
        private Texture2D _texWhite;

        // ===== GUIContent 캐시 (OnGUI GC 할당 방지) =====
        private readonly GUIContent _gcItemName = new GUIContent();
        private readonly GUIContent _gcRarityCategory = new GUIContent();
        private readonly GUIContent _gcDescription = new GUIContent();
        private readonly GUIContent _gcEffects = new GUIContent();
        private readonly GUIContent _gcDurability = new GUIContent();
        private readonly GUIContent _gcCount = new GUIContent();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Phase 33: create tooltip theme if none assigned
            if (_theme == null)
                _theme = Phase33_Themes.CreateTooltipTheme();
        }

        private void OnDestroy()
        {
            if (_texWhite != null)
            {
                Destroy(_texWhite);
                _texWhite = null;
            }
            if (_instance == this)
                _instance = null;
        }

        /// <summary>툴팁 표시 요청 (슬롯 호버 시 매 프레임 호출)</summary>
        public void ShowTooltip(ItemTooltipData data, Vector2 mousePos)
        {
            if (!data.IsValid) return;

            _tooltipData = data;
            _lastMousePos = mousePos;
            _hasTooltip = true;
            _wasHovering = true;
            _lastShowFrame = Time.frameCount;
            _compareItem = default; // 새 툴팁 표시 시 비교 데이터 초기화

            // 첫 호버 시간 기록 (딜레이용)
            if (!_isShowing)
            {
                _mouseEnterTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>즉시 툴팁 숨김 (강제)</summary>
        public void ForceHide()
        {
            _hasTooltip = false;
            _isShowing = false;
            _wasHovering = false;
            _lastShowFrame = -1;
            _compareItem = default;
        }

        /// <summary>현재 툴팁 표시 중인지</summary>
        public bool IsShowing => _isShowing;

        /// <summary>현재 호버 중인지</summary>
        public bool IsHovering => _wasHovering;

        /// <summary>비교할 장착 아이템 설정 (장비 카테고리일 때만 호출)</summary>
        public void SetCompareItem(ItemTooltipData compareData)
        {
            _compareItem = compareData;
        }

        // ===================================================================
        // OnGUI — IMGUI 툴팁 렌더링
        // ===================================================================
        private void OnGUI()
        {
            if (!_hasTooltip)
            {
                _isShowing = false;
                return;
            }

            InitStyles();

            float now = Time.realtimeSinceStartup;

            // 이번 프레임에 ShowTooltip이 호출되지 않았으면 호버 종료
            if (_wasHovering && _lastShowFrame != Time.frameCount)
            {
                _mouseLeaveTime = now;
                _wasHovering = false;
            }

            // 표시/숨김 타이밍 관리
            if (_wasHovering)
            {
                // 마우스가 위에 있음: 딜레이 후 표시 (접근성 설정 반영)
                float effectiveDelay = ProjectName.Systems.AccessibilityManager.TooltipDelay;
                if (!_isShowing && (now - _mouseEnterTime) >= effectiveDelay)
                {
                    _isShowing = true;
                }
            }
            else
            {
                // 마우스가 떠남: 딜레이 후 숨김
                if (_isShowing && (now - _mouseLeaveTime) >= _hideDelay)
                {
                    _isShowing = false;
                    _hasTooltip = false;
                    _compareItem = default;
                    return;
                }
                if (!_isShowing)
                {
                    _hasTooltip = false;
                    _compareItem = default;
                    return;
                }
            }

            if (!_isShowing) return;

            // 툴팁 내용 높이 계산
            float contentHeight = CalculateTooltipHeight();
            float tooltipHeight = contentHeight + _padding * 2;

            // 위치 계산 (화면 끝에 닿으면 반대쪽으로 조정)
            Rect tooltipRect = CalculatePosition(_lastMousePos, _tooltipWidth, tooltipHeight);

            // === 외곽 테두리 (등급 색상) ===
            Color rarityColor = ItemTooltipData.GetRarityBorderColor(_tooltipData.rarity);
            DrawColoredRect(new Rect(tooltipRect.x - 2, tooltipRect.y - 2,
                tooltipRect.width + 4, tooltipRect.height + 4), rarityColor);

            // === Phase 33: 테마 배경 패턴 ===
            if (_theme != null)
            {
                Texture2D patternTex = ProceduralTextureGenerator.GetPatternTexture(_theme.CurrentPattern);
                if (patternTex != null)
                {
                    GUI.DrawTexture(tooltipRect, patternTex, ScaleMode.StretchToFill);
                }
                else
                {
                    // Fallback: theme background color
                    DrawColoredRect(tooltipRect, _theme.BgColor);
                }

                // === 테마 테두리 장식 (simple) ===
                DecorativeBorderRenderer.DrawBorder(tooltipRect, _theme.CurrentBorder, _theme.BorderColor, 1.5f);
            }
            else
            {
                // === 내부 배경 (fallback) ===
                DrawColoredRect(tooltipRect, new Color(0.12f, 0.10f, 0.14f, 0.95f));
            }

            // === 내용 그리기 ===
            float cy = tooltipRect.y + _padding;
            float cx = tooltipRect.x + _padding;
            float cw = tooltipRect.width - _padding * 2;

            // 아이템명
            _gcItemName.text = _tooltipData.itemName;
            float itemNameHeight = _styleItemName.CalcHeight(_gcItemName, cw);
            GUI.Label(new Rect(cx, cy, cw, itemNameHeight), _gcItemName, _styleItemName);
            cy += itemNameHeight + 4;

            // 등급 + 카테고리
            string rarityStr = ItemTooltipData.GetRarityDisplayName(_tooltipData.rarity);
            string categoryStr = GetCategoryDisplayName(_tooltipData.category);
            _gcRarityCategory.text = $"{rarityStr} · {categoryStr}";
            float rarityHeight = _styleRarityLabel.CalcHeight(_gcRarityCategory, cw);
            GUI.Label(new Rect(cx, cy, cw, rarityHeight), _gcRarityCategory, _styleRarityLabel);
            cy += rarityHeight + 4;

            // 구분선
            DrawColoredRect(new Rect(cx, cy, cw, 1), new Color(0.3f, 0.3f, 0.3f, 0.5f));
            cy += 4;

            // 설명
            if (!string.IsNullOrEmpty(_tooltipData.description))
            {
                _gcDescription.text = _tooltipData.description;
                float descHeight = _styleDescription.CalcHeight(_gcDescription, cw);
                GUI.Label(new Rect(cx, cy, cw, descHeight), _gcDescription, _styleDescription);
                cy += descHeight + 4;
            }

            // 효과
            if (!string.IsNullOrEmpty(_tooltipData.effects))
            {
                _gcEffects.text = _tooltipData.effects;
                float effHeight = _styleEffects.CalcHeight(_gcEffects, cw);
                GUI.Label(new Rect(cx, cy, cw, effHeight), _gcEffects, _styleEffects);
                cy += effHeight + 4;
            }

            // 내구도 (장비만)
            if (_tooltipData.hasDurability)
            {
                float durRatio = _tooltipData.durabilityRatio;
                Color durColor = ItemTooltipData.GetDurabilityColor(durRatio);

                _gcDurability.text = $"내구도: {_tooltipData.currentDurability}/{_tooltipData.maxDurability}";
                float durLabelHeight = _styleCategoryLabel.CalcHeight(_gcDurability, cw);
                GUI.Label(new Rect(cx, cy, cw, durLabelHeight), _gcDurability, _styleCategoryLabel);
                cy += durLabelHeight + 2;

                // 내구도 바
                float barWidth = cw;
                float barHeight = 6f;
                DrawColoredRect(new Rect(cx, cy, barWidth, barHeight), new Color(0.2f, 0.2f, 0.2f, 0.8f));
                DrawColoredRect(new Rect(cx, cy, barWidth * durRatio, barHeight), durColor);
                cy += barHeight + 4;
            }

            // 개수/재고
            if (_tooltipData.count > 0)
            {
                _gcCount.text = _tooltipData.count == ItemTooltipData.InfiniteCount ? "재고: 무한" : $"x{_tooltipData.count}";
                float countHeight = _styleCountLabel.CalcHeight(_gcCount, cw);
                GUI.Label(new Rect(cx, cy, cw, countHeight), _gcCount, _styleCountLabel);
                cy += countHeight + 2;
            }

            // ──── 장비 비교 섹션 (장비 아이템 + 비교 데이터 있을 때) ────
            if (_compareItem.IsValid && CompareTooltip.IsEquipmentCategory(_tooltipData.category))
            {
                cy = CompareTooltip.DrawComparison(cx, cy, cw, _tooltipData, _compareItem, _texWhite);
            }
        }

        // ===================================================================
        // 툴팁 높이 계산
        // ===================================================================
        private float CalculateTooltipHeight()
        {
            InitStyles();
            float cw = _tooltipWidth - _padding * 2;
            float h = _padding * 2;

            // 아이템명
            _gcItemName.text = _tooltipData.itemName;
            h += _styleItemName.CalcHeight(_gcItemName, cw) + 4;

            // 등급 + 카테고리
            string rarityStr = ItemTooltipData.GetRarityDisplayName(_tooltipData.rarity);
            string categoryStr = GetCategoryDisplayName(_tooltipData.category);
            _gcRarityCategory.text = $"{rarityStr} · {categoryStr}";
            h += _styleRarityLabel.CalcHeight(_gcRarityCategory, cw) + 4;

            h += 5;  // 구분선 + 간격

            if (!string.IsNullOrEmpty(_tooltipData.description))
            {
                _gcDescription.text = _tooltipData.description;
                h += _styleDescription.CalcHeight(_gcDescription, cw) + 4;
            }

            if (!string.IsNullOrEmpty(_tooltipData.effects))
            {
                _gcEffects.text = _tooltipData.effects;
                h += _styleEffects.CalcHeight(_gcEffects, cw) + 4;
            }

            if (_tooltipData.hasDurability)
            {
                _gcDurability.text = $"내구도: {_tooltipData.currentDurability}/{_tooltipData.maxDurability}";
                h += _styleCategoryLabel.CalcHeight(_gcDurability, cw) + 2;
                h += 6;  // 내구도 바
                h += 4;  // 간격
            }

            if (_tooltipData.count > 0)
            {
                _gcCount.text = _tooltipData.count == ItemTooltipData.InfiniteCount ? "재고: 무한" : $"x{_tooltipData.count}";
                h += _styleCountLabel.CalcHeight(_gcCount, cw);
            }

            // 비교 섹션 높이 (장비 아이템 + 비교 데이터 있을 때)
            if (_compareItem.IsValid && CompareTooltip.IsEquipmentCategory(_tooltipData.category))
            {
                h += CompareTooltip.CalculateCompareHeight(cw);
            }

            return h;
        }

        // ===================================================================
        // 위치 계산 (화면 끝에 닿으면 반대쪽으로)
        // ===================================================================
        private Rect CalculatePosition(Vector2 mousePos, float width, float height)
        {
            float x = mousePos.x + _mouseOffset.x;
            float y = mousePos.y + _mouseOffset.y;

            // 오른쪽 끝에 닿으면 왼쪽으로
            if (x + width > Screen.width)
                x = mousePos.x - width - _mouseOffset.x;

            // 아래쪽 끝에 닿으면 위쪽으로
            if (y + height > Screen.height)
                y = mousePos.y - height - _mouseOffset.y;

            // 최소 위치 보정 (화면 밖으로 나가지 않도록)
            if (x < 2) x = 2;
            if (y < 2) y = 2;

            return new Rect(x, y, width, height);
        }

        // ===================================================================
        // 스타일 초기화
        // ===================================================================
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texWhite = MakeTexture(1, 1, Color.white);

            // 아이템명 (굵게, 크게)
            _styleItemName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.95f, 0.90f, 0.80f, 1f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            // 등급/카테고리 레이블
            _styleRarityLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.70f, 0.65f, 0.60f, 1f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            // 설명
            _styleDescription = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.85f, 0.82f, 0.78f, 1f) },
                wordWrap = true,
                padding = new RectOffset(0, 0, 0, 0)
            };

            // 효과
            _styleEffects = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.4f, 0.9f, 0.5f, 1f) },
                wordWrap = true,
                padding = new RectOffset(0, 0, 0, 0)
            };

            // 카테고리/내구도 레이블
            _styleCategoryLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.75f, 0.70f, 0.65f, 1f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            // 개수
            _styleCountLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.80f, 0.60f, 0.20f, 1f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _stylesInitialized = true;
        }

        // ===================================================================
        // 헬퍼
        // ===================================================================
        private Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void DrawColoredRect(Rect rect, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _texWhite);
            GUI.color = oldColor;
        }

        /// <summary>장비 카테고리인지 확인 (Weapon/Armor/Tool/Arrow)</summary>
        public static bool IsEquipmentCategory(PlayerInventory.ItemCategory category)
        {
            return CompareTooltip.IsEquipmentCategory(category);
        }

        private string GetCategoryDisplayName(PlayerInventory.ItemCategory category)
        {
            return category switch
            {
                PlayerInventory.ItemCategory.Herb => "약초",
                PlayerInventory.ItemCategory.Meat => "고기",
                PlayerInventory.ItemCategory.Food => "요리",
                PlayerInventory.ItemCategory.Potion => "물약",
                PlayerInventory.ItemCategory.Drug => "마약",
                PlayerInventory.ItemCategory.Material => "재료",
                PlayerInventory.ItemCategory.Quest => "퀘스트",
                PlayerInventory.ItemCategory.Weapon => "무기",
                PlayerInventory.ItemCategory.Armor => "방어구",
                PlayerInventory.ItemCategory.Tool => "도구",
                PlayerInventory.ItemCategory.Arrow => "화살",
                _ => "기타"
            };
        }
    }
}
