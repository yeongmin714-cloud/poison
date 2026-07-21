using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.UI.Functions
{
    /// <summary>
    /// 게임 불러오기 UI - 저장된 슬롯들을 표시하고 불러오기/삭제 기능 제공
    /// </summary>
    public class LoadGameUI : UIWindow
    {
        [Header("Slot UI")]
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private Transform _slotsContainer;
        [SerializeField] private int _maxSlots = 4;

        protected override void Awake()
        {
            base.Awake();

            // Create UI if not already set up
            if (_slotsContainer == null)
            {
                CreateDefaultUI();
            }

            // Start hidden
            gameObject.SetActive(false);
        }

        private void CreateDefaultUI()
        {
            // Create slots container
            GameObject container = new GameObject("SlotsContainer");
            container.transform.SetParent(transform, false);
            _slotsContainer = container.transform;

            VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Create slot prefabs
            for (int i = 0; i < _maxSlots; i++)
            {
                GameObject slot = CreateSlot(i);
                slot.transform.SetParent(_slotsContainer, false);
            }
        }

        private GameObject CreateSlot(int index)
        {
            GameObject slotGo = new GameObject($"SaveSlot_{index}");
            RectTransform rect = slotGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 100);

            // Background
            Image bg = slotGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            // Slot text
            GameObject textGo = new GameObject("SlotText");
            textGo.transform.SetParent(slotGo.transform, false);
            Text text = textGo.AddComponent<Text>();
            text.text = "비어있음";
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 24;
            text.color = Color.white;
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Load button
            GameObject btnGo = new GameObject("LoadButton");
            btnGo.transform.SetParent(slotGo.transform, false);
            Button btn = btnGo.AddComponent<Button>();
            Image btnBg = btnGo.AddComponent<Image>();
            btnBg.color = new Color(0.2f, 0.5f, 0.2f, 0.9f);
            GameObject btnTextGo = new GameObject("Text");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            Text btnText = btnTextGo.AddComponent<Text>();
            btnText.text = "불러오기";
            btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.fontSize = 18;
            btnText.color = Color.white;
            RectTransform btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1, 0.5f);
            btnRect.anchorMax = new Vector2(1, 0.5f);
            btnRect.sizeDelta = new Vector2(100, 40);
            btnRect.anchoredPosition = new Vector2(-60, 0);

            // Delete button
            GameObject delGo = new GameObject("DeleteButton");
            delGo.transform.SetParent(slotGo.transform, false);
            Button delBtn = delGo.AddComponent<Button>();
            Image delBg = delGo.AddComponent<Image>();
            delBg.color = new Color(0.5f, 0.2f, 0.2f, 0.9f);
            GameObject delTextGo = new GameObject("Text");
            delTextGo.transform.SetParent(delGo.transform, false);
            Text delText = delTextGo.AddComponent<Text>();
            delText.text = "삭제";
            delText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            delText.alignment = TextAnchor.MiddleCenter;
            delText.fontSize = 18;
            delText.color = Color.white;
            RectTransform delRect = delGo.AddComponent<RectTransform>();
            delRect.anchorMin = new Vector2(1, 0.5f);
            delRect.anchorMax = new Vector2(1, 0.5f);
            delRect.sizeDelta = new Vector2(100, 40);
            delRect.anchoredPosition = new Vector2(-60, -55);

            return slotGo;
        }

        /// <summary>
        /// 슬롯들을 새로고침하여 현재 저장 데이터 표시
        /// </summary>
        public void RefreshSlots()
        {
            if (_slotsContainer == null || SaveManager.Instance == null)
                return;

            var infos = SaveManager.Instance.GetAllSlotInfos();
            for (int i = 0; i < _slotsContainer.childCount && i < infos.Length; i++)
            {
                Transform slot = _slotsContainer.GetChild(i);
                Text slotText = slot.GetComponentInChildren<Text>();
                if (slotText != null)
                {
                    if (infos[i] != null)
                    {
                        slotText.text = $"슬롯 {i + 1} — {infos[i].timestamp} (Day {infos[i].time?.day ?? 0}, Lv.{infos[i].player?.level ?? 0})";
                    }
                    else
                    {
                        slotText.text = $"슬롯 {i + 1} — 비어있음";
                    }
                }
            }
        }

        public override void Show()
        {
            base.Show();
            RefreshSlots();
        }
    }
}