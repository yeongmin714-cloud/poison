using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 29-01: 보석 상자 (Gem Chest).
    /// E 키 상호작용 → 희귀 광석 드랍 (LootBasket).
    /// </summary>
    public class GemChest : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private GemType _gemType = GemType.Ruby;
        [SerializeField] private int _gemCount = 1;
        [SerializeField] private float _interactRange = 2.5f;
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [System.NonSerialized] private bool _isOpen = false;

        [Header("참조")]
        [SerializeField] private Light _pointLight;
        [SerializeField] private MeshRenderer _chestRenderer;

        private Transform _player;
        private Camera _mainCamera;

        /// <summary>상자가 열렸는가</summary>
        public bool IsOpen => _isOpen;

        /// <summary>보석 타입</summary>
        public GemType GemType { get => _gemType; set => _gemType = value; }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            _mainCamera = Camera.main;

            if (_pointLight == null)
            {
                var lightGo = new GameObject("ChestLight");
                lightGo.transform.SetParent(transform);
                lightGo.transform.localPosition = Vector3.up * 0.5f;
                _pointLight = lightGo.AddComponent<Light>();
            }
            _pointLight.type = LightType.Point;
            _pointLight.color = GemData.GetGemData(_gemType).color;
            _pointLight.range = 4f;
            _pointLight.intensity = 0.6f;

            if (_chestRenderer == null)
                _chestRenderer = GetComponent<MeshRenderer>();

            var data = GemData.GetGemData(_gemType);
            if (_chestRenderer != null)
                _chestRenderer.material = MaterialHelper.CreateLitMaterial(data.color, "GemChestMat");
        }

        private void Update()
        {
            if (_isOpen || _player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            bool nearby = dist <= _interactRange;

            if (nearby && Input.GetKeyDown(_interactKey))
            {
                Open();
            }
        }

        private void OnGUI()
        {
            if (_isOpen || _player == null) return;
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > _interactRange) return;

            if (_mainCamera == null) return;

            Vector3 screenPos = _mainCamera.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            if (screenPos.z < 0) return;
            screenPos.y = Screen.height - screenPos.y;

            float bw = 80f, bh = 24f;
            GUI.Box(new Rect(screenPos.x - bw / 2f, screenPos.y - bh - 5, bw, bh), "");
            GUI.Label(new Rect(screenPos.x - bw / 2f, screenPos.y - bh - 5, bw, bh),
                "💎 [E]", new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter });
        }

        /// <summary>상자 열기 — 광석 드랍</summary>
        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            var data = GemData.GetGemData(_gemType);
            Debug.Log($"[GemChest] 열림! {data.displayName} × {_gemCount}");

            // LootBasket 생성
            Vector3 dropPos = transform.position + Vector3.up * 0.3f;
            var basket = LootBasket.Create(dropPos); // uses default lifetime

            string gemTypeId = $"gem_{_gemType.ToString().ToLower()}";
            string displayName = data.displayName;

            var item = new PlayerInventory.ItemData
            {
                id = gemTypeId,
                displayName = displayName,
                description = $"보석: {displayName}",
                category = PlayerInventory.ItemCategory.Material,
                rarity = data.starRating >= 5 ? ItemRarity.Legendary :
                         data.starRating >= 4 ? ItemRarity.Epic : ItemRarity.Rare,
                maxStack = 99
            };
            basket.AddItem(item, _gemCount);

            // 시각 효과: 빛 제거, 반투명
            if (_pointLight != null) _pointLight.enabled = false;
            if (_chestRenderer != null)
            {
                var mat = _chestRenderer.material;
                var c = mat.color;
                mat.color = new Color(c.r, c.g, c.b, 0.5f);
            }
        }

        /// <summary>테스트용 강제 오픈</summary>
        public void ForceOpen() => Open();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}
