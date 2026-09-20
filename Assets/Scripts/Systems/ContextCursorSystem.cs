using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 절차적 컨텍스트 커서 — 마우스 화면 지점을 따라다니는 월드 앵커 위에,
    /// HoverTargetClassifier.ClassifyAt 결과에 따라 절차 생성 아이콘(칼/호미/곡괭이/삽/화살표)을 교체.
    /// Farm=호미(파종용), Mine=곡괭이(채굴용) — 농경/광질 커서 분리.
    /// OS 커서 숨김은 별도(CursorVisibilityController)가 담당. 여기는 오직 커스텀 커서 시각화.
    /// 모든 프리미티브 콜라이더를 파괴해 HoverTargetClassifier/RTS의 레이캐스트를 오염시키지 않는다.
    /// </summary>
    public class ContextCursorSystem : MonoBehaviour
    {
        public static ContextCursorSystem Instance { get; private set; }

        private const float AnchorDepth = 2.2f;   // [P20-2] 5m→2.2m — 너무 멀어 아이콘이 작게/안 보였던 것 보정
        private const float IconScale = 0.22f;    // [P20-2] 0.12→0.22 — 탑다운에서 아이콘 식별 가능 크기

        private Transform _anchor;
        private Transform _iconsRoot;
        private Material _iconMaterial;

        private readonly Dictionary<HoverTargetClassifier.TargetKind, Transform> _icons =
            new Dictionary<HoverTargetClassifier.TargetKind, Transform>();
        private HoverTargetClassifier.TargetKind _shown = HoverTargetClassifier.TargetKind.None;

        public static ContextCursorSystem Ensure(GameObject parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ContextCursorSystem>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("ContextCursorSystem");
            if (parent != null) go.transform.SetParent(parent.transform, false);
            return go.AddComponent<ContextCursorSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _anchor = new GameObject("CursorAnchor").transform;
            _anchor.SetParent(transform, false);

            _iconsRoot = new GameObject("Icons").transform;
            _iconsRoot.SetParent(_anchor, false);
            _iconsRoot.localScale = Vector3.one * IconScale;
            _iconsRoot.localRotation = Quaternion.identity;

            _iconMaterial = CreateBrightMaterial();

            BuildIcons();
            SetIcon(HoverTargetClassifier.TargetKind.Terrain);   // 기본 화살표
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null || Mouse.current == null) return;

            Vector2 mouse = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mouse);
            _anchor.position = ray.origin + ray.direction * AnchorDepth;
            _anchor.rotation = cam.transform.rotation;

            HoverTargetClassifier.TargetKind kind = HoverTargetClassifier.ClassifyAt(mouse);
            if (kind != HoverTargetClassifier.TargetKind.None)
                SetIcon(kind);
        }

        /// <summary>현재 표시 중인 컨텍스트 종류 (외부 진단용).</summary>
        public HoverTargetClassifier.TargetKind CurrentKind => _shown;

        private void SetIcon(HoverTargetClassifier.TargetKind kind)
        {
            _shown = kind;
            if (_icons.TryGetValue(kind, out Transform target))
            {
                foreach (var kv in _icons)
                    if (kv.Value != null) kv.Value.gameObject.SetActive(kv.Key == kind);
            }
        }

        // ===== 절차 아이콘 구성 =====

        private void BuildIcons()
        {
            _icons[HoverTargetClassifier.TargetKind.Enemy]   = BuildSword();
            _icons[HoverTargetClassifier.TargetKind.Farm]    = BuildHoe();
            _icons[HoverTargetClassifier.TargetKind.Mine]    = BuildPickaxe();
            _icons[HoverTargetClassifier.TargetKind.Gather]  = BuildShovel();
            _icons[HoverTargetClassifier.TargetKind.Ally]    = BuildArrow();
            _icons[HoverTargetClassifier.TargetKind.Terrain] = BuildArrow();
            _icons[HoverTargetClassifier.TargetKind.None]    = BuildArrow();
        }

        private Transform BuildSword()
        {
            var root = NewIconRoot("IconSword");
            // 칼날 — 대각선 캡슐
            MakePrim(PrimitiveType.Capsule, root, new Vector3(0f, 0.45f, 0f),
                new Vector3(0.08f, 1.0f, 0.08f), new Vector3(0f, 0f, 45f), new Color(0.9f, 0.9f, 1.0f));
            // 손잡이 — 반대 대각선 큐브
            MakePrim(PrimitiveType.Cube, root, new Vector3(0f, -0.30f, 0f),
                new Vector3(0.13f, 0.5f, 0.13f), new Vector3(0f, 0f, -45f), new Color(0.55f, 0.33f, 0.14f));
            return root;
        }

        /// <summary>호미 — 짧은 손잡이 + 납작한 사각 호미날(밭 파종용). Farm 전용.</summary>
        private Transform BuildHoe()
        {
            var root = NewIconRoot("IconHoe");
            // 호미날 — 납작한 사각 (삼각 느낌으로 살짝 기울임, 손잡이 끝에 직각 부착)
            MakePrim(PrimitiveType.Cube, root, new Vector3(0.10f, 0.24f, 0f),
                new Vector3(0.30f, 0.11f, 0.06f), new Vector3(0f, 0f, -8f), new Color(0.58f, 0.58f, 0.62f));
            // 날 밑단 — 밀림판 느낌의 얇은 세로판
            MakePrim(PrimitiveType.Cube, root, new Vector3(0.21f, 0.13f, 0f),
                new Vector3(0.08f, 0.16f, 0.06f), Vector3.zero, new Color(0.48f, 0.48f, 0.52f));
            // 손잡이 — 세로 막대(짧게)
            MakePrim(PrimitiveType.Cube, root, new Vector3(0f, -0.10f, 0f),
                new Vector3(0.09f, 0.52f, 0.09f), Vector3.zero, new Color(0.62f, 0.40f, 0.18f));
            return root;
        }

        private Transform BuildPickaxe()
        {
            var root = NewIconRoot("IconPickaxe");
            // 곡괭이 머리 — 가로 막대(아래로 구부러진 L 느낌)
            MakePrim(PrimitiveType.Cube, root, new Vector3(0f, 0.15f, 0f),
                new Vector3(0.55f, 0.12f, 0.12f), Vector3.zero, new Color(0.5f, 0.5f, 0.55f));
            // 손잡이 — 세로 막대
            MakePrim(PrimitiveType.Cube, root, new Vector3(0f, -0.20f, 0f),
                new Vector3(0.12f, 0.6f, 0.12f), Vector3.zero, new Color(0.62f, 0.40f, 0.18f));
            // 머리 양 끝 뾰족 — 작은 큐브
            MakePrim(PrimitiveType.Cube, root, new Vector3(0.30f, 0.15f, 0f),
                new Vector3(0.10f, 0.10f, 0.10f), new Vector3(0f, 0f, 40f), new Color(0.5f, 0.5f, 0.55f));
            MakePrim(PrimitiveType.Cube, root, new Vector3(-0.30f, 0.15f, 0f),
                new Vector3(0.10f, 0.10f, 0.10f), new Vector3(0f, 0f, -40f), new Color(0.5f, 0.5f, 0.55f));
            return root;
        }

        private Transform BuildShovel()
        {
            var root = NewIconRoot("IconShovel");
            // 삽 날 — 납작한 사각
            MakePrim(PrimitiveType.Cube, root, new Vector3(0f, 0.22f, 0f),
                new Vector3(0.26f, 0.26f, 0.06f), Vector3.zero, new Color(0.6f, 0.42f, 0.25f));
            // 손잡이 — 세로 막대
            MakePrim(PrimitiveType.Cube, root, new Vector3(0f, -0.15f, 0f),
                new Vector3(0.09f, 0.55f, 0.09f), Vector3.zero, new Color(0.62f, 0.40f, 0.18f));
            return root;
        }

        private Transform BuildArrow()
        {
            var root = NewIconRoot("IconArrow");
            // 몸통
            MakePrim(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0f),
                new Vector3(0.09f, 0.5f, 0.09f), Vector3.zero, new Color(0.15f, 0.6f, 0.9f));
            // 화살촉 — 위쪽 뾰족
            MakePrim(PrimitiveType.Cube, root, new Vector3(0f, 0.35f, 0f),
                new Vector3(0.20f, 0.16f, 0.09f), Vector3.zero, new Color(0.9f, 0.2f, 0.2f));
            return root;
        }

        // ===== 유틸 =====

        private Transform NewIconRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_iconsRoot, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.SetActive(false);
            return go.transform;
        }

        private void MakePrim(PrimitiveType type, Transform parent, Vector3 localPos,
            Vector3 localScale, Vector3 localEuler, Color color)
        {
            if (_iconMaterial == null) return;
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();

            // 레이캐스트 오염 방지 — 커서 프리미티브 콜라이더 제거
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = _iconMaterial;
                if (mr.material != null) mr.material.color = color;
            }
        }

        private static Material CreateBrightMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            if (shader == null) return null;
            return new Material(shader);
        }
    }
}
