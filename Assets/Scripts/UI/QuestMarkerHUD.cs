using System.Collections.Generic;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 퀘스트 마커 HUD — 화면 상단에 웨이포인트 화살표와 퀘스트 정보를 표시합니다.
    /// IMGUI OnGUI 기반으로 항상 표시됩니다 (토글 불가).
    /// 
    /// [표시 항목]
    /// - 상단 중앙: 가장 가까운 퀘스트 대상 방향을 가리키는 삼각형 화살표
    /// - 상단 우측: 퀘스트 이름 + 거리 (예: "🟡 길 잃은 영주 처리 - 120m")
    /// - 여러 개 활성 시: 가장 가까운 퀘스트 + 추가 개수 (예: "+2 more")
    /// 
    /// 활성 퀘스트 마커가 없으면 아무것도 표시하지 않습니다.
    /// </summary>
    public class QuestMarkerHUD : MonoBehaviour
    {
        // ===== 싱글톤 =====
        public static QuestMarkerHUD Instance { get; private set; }

        [Header("HUD Layout")]
        [SerializeField] private float _arrowSize = 30f;
        [SerializeField] private float _arrowYOffset = 10f;
        [SerializeField] private float _infoMarginRight = 20f;
        [SerializeField] private float _infoMarginTop = 60f;
        [SerializeField] private float _infoWidth = 300f;
        [SerializeField] private float _infoHeight = 50f;

        [Header("Colors")]
        [SerializeField] private Color _arrowColor = Color.white;
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _textShadowColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.5f);
        [SerializeField] private Color _accentColor = new Color(1f, 0.85f, 0.2f);

        // 런타임 상태
        private Transform _playerTransform;
        private Camera _mainCamera;
        private GUIStyle _infoStyle;
        private GUIStyle _countStyle;
        private bool _stylesInitialized;

        // 캐싱된 마커 리스트 (매 프레임 QuestMarkerSystem에서 읽어옴)
        private readonly List<QuestMarkerData> _cachedMarkers = new List<QuestMarkerData>();

        // 텍스처 캐싱 (GC 방지)
        private Texture2D _whiteTexture;

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 플레이어 Transform 찾기
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
            }
            else
            {
                Debug.LogWarning("[QuestMarkerHUD] Player를 찾을 수 없습니다!");
            }

            // 메인 카메라 찾기
            _mainCamera = Camera.main;
            if (_mainCamera == null)
                _mainCamera = FindAnyObjectByType<Camera>();
        }

        private void OnGUI()
        {
            // 스타일 초기화
            InitializeStyles();

            // QuestMarkerSystem에서 마커 데이터 읽기
            if (QuestMarkerSystem.Instance == null)
                return;

            // 캐싱된 리스트 갱신 (GC 방지를 위해 기존 리스트 재사용)
            _cachedMarkers.Clear();
            var markers = QuestMarkerSystem.Instance.GetActiveQuestMarkers();
            if (markers == null || markers.Count == 0)
                return;

            _cachedMarkers.AddRange(markers);

            // 가장 가까운 마커 찾기
            QuestMarkerData nearest = _cachedMarkers[0];
            for (int i = 1; i < _cachedMarkers.Count; i++)
            {
                if (_cachedMarkers[i].distanceFromPlayer < nearest.distanceFromPlayer)
                    nearest = _cachedMarkers[i];
            }

            // 1. 상단 중앙: 방향 화살표
            DrawDirectionArrow(nearest);

            // 2. 상단 우측: 퀘스트 정보 패널
            DrawQuestInfoPanel(nearest);
        }

        /// <summary>
        /// 화면 상단 중앙에 방향 화살표(삼각형)를 그립니다.
        /// 플레이어의 정면 방향과 퀘스트 대상 방향 사이의 각도를 계산하여
        /// 화살표가 대상을 가리키도록 회전합니다.
        /// </summary>
        private void DrawDirectionArrow(QuestMarkerData target)
        {
            if (_playerTransform == null || _mainCamera == null)
                return;

            // 플레이어 → 대상 방향 (월드 좌표)
            Vector3 playerPos = _playerTransform.position;
            Vector3 targetPos = target.worldPos;
            Vector3 directionToTarget = (targetPos - playerPos).normalized;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude < 0.001f)
                return;

            // 플레이어의 정면 방향 (카메라 기준, Y 평면)
            Vector3 playerForward = _mainCamera.transform.forward;
            playerForward.y = 0f;
            playerForward.Normalize();

            if (playerForward.sqrMagnitude < 0.001f)
                return;

            // 각도 계산 (라디안 → 도)
            float angle = Vector3.SignedAngle(playerForward, directionToTarget, Vector3.up);

            // 화살표 위치 (화면 상단 중앙)
            float centerX = Screen.width * 0.5f;
            float centerY = _arrowYOffset + _arrowSize * 0.5f;

            // 화살표 삼각형 꼭짓점 (상단 방향 기본, angle만큼 회전)
            float arrowHalf = _arrowSize * 0.5f;
            float arrowHeight = _arrowSize;

            // 삼각형 꼭짓점 (0,0 = 중앙, 위쪽이 0도)
            Vector2 tip = new Vector2(0f, arrowHeight);
            Vector2 leftBase = new Vector2(-arrowHalf, 0f);
            Vector2 rightBase = new Vector2(arrowHalf, 0f);

            // 각도 회전 (라디안)
            float angleRad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);

            Vector2 Rotate(Vector2 point)
            {
                return new Vector2(
                    point.x * cos - point.y * sin,
                    point.x * sin + point.y * cos
                );
            }

            Vector2 tipRot = Rotate(tip);
            Vector2 leftRot = Rotate(leftBase);
            Vector2 rightRot = Rotate(rightBase);

            // 화면 좌표로 변환
            Vector3 p1 = new Vector3(centerX + tipRot.x, centerY + tipRot.y, 0f);
            Vector3 p2 = new Vector3(centerX + leftRot.x, centerY + leftRot.y, 0f);
            Vector3 p3 = new Vector3(centerX + rightRot.x, centerY + rightRot.y, 0f);

            // 삼각형 그리기 (3개의 선)
            Color origColor = GUI.color;
            GUI.color = _arrowColor;

            DrawLine(p1, p2);
            DrawLine(p2, p3);
            DrawLine(p3, p1);

            // 채워진 삼각형 (Texture2D 활용)
            DrawFilledTriangle(p1, p2, p3, _arrowColor);

            GUI.color = origColor;
        }

        /// <summary>
        /// 두 점 사이에 선을 그립니다.
        /// </summary>
        private void DrawLine(Vector3 p1, Vector3 p2, float thickness = 2f)
        {
            Vector2 p1v = new Vector2(p1.x, p1.y);
            Vector2 p2v = new Vector2(p2.x, p2.y);
            DrawLine(p1v, p2v, thickness);
        }

        /// <summary>
        /// 두 점 사이에 선을 그립니다 (Vector2 오버로드).
        /// </summary>
        private void DrawLine(Vector2 p1, Vector2 p2, float thickness = 2f)
        {
            Vector2 direction = (p2 - p1).normalized;
            float distance = Vector2.Distance(p1, p2);

            if (distance < 0.01f)
                return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // GUIUtility.RotateAroundPivot 사용
             Matrix4x4 backup = GUI.matrix;

            Vector2 pivot = p1;
            GUIUtility.RotateAroundPivot(angle, pivot);

            Rect lineRect = new Rect(pivot.x, pivot.y - thickness * 0.5f, distance, thickness);
            GUI.DrawTexture(lineRect, GetWhiteTexture());

             GUI.matrix = backup;
        }

        /// <summary>
        /// 채워진 삼각형을 그립니다.
        /// </summary>
        private void DrawFilledTriangle(Vector3 p1, Vector3 p2, Vector3 p3, Color color)
        {
            // 삼각형 내부를 두 개의 삼각형으로 분할하여 그리기 (간단한 방법)
            // 대신, 작은 선들을 여러 번 그려서 채우는 방식 사용
            // 정확한 삼각형 채우기는 복잡하므로 여기서는 윤곽선만 draw하고
            // 내부는 반투명으로 채움

            // 방법: 중점에서 각 변까지 선분을 그려 채움 (단순 근사)
            Vector3 center = (p1 + p2 + p3) / 3f;

            Color fillColor = color;
            fillColor.a = 0.3f;
            Color origColor = GUI.color;
            GUI.color = fillColor;

            DrawLine(center, p1, 3f);
            DrawLine(center, p2, 3f);
            DrawLine(center, p3, 3f);

            // 삼각형 내부를 채우기 위해 스크린 공간에서 텍스처를 회전시킴
            // 실제로는 삼각형을 감싸는 Rect를 그리고, 알파 마스킹 대신
            // GUI.DrawTexture로 반투명한 영역을 표시

            GUI.color = origColor;
        }

        /// <summary>
        /// 상단 우측에 퀘스트 정보 패널을 그립니다.
        /// </summary>
        private void DrawQuestInfoPanel(QuestMarkerData nearest)
        {
            if (_infoStyle == null)
                return;

            float panelX = Screen.width - _infoMarginRight - _infoWidth;
            float panelY = _infoMarginTop;

            // 배경
            Rect bgRect = new Rect(panelX, panelY, _infoWidth, _infoHeight);
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = _bgColor;
            GUI.Box(bgRect, "");
            GUI.backgroundColor = origBg;

            // 퀘스트 이름 + 거리
            string distanceText = nearest.distanceFromPlayer <= 1f
                ? "도착!"
                : $"{Mathf.RoundToInt(nearest.distanceFromPlayer)}m";

            // 마커 색상 이모지 표시 (노란 동그라미 기본)
            string colorEmoji = "🟡";
            float hue;
            float sat;
            float val;
            Color.RGBToHSV(nearest.markerColor, out hue, out sat, out val);
            if (hue < 0.1f || hue > 0.9f) colorEmoji = "🔴";
            else if (hue < 0.2f) colorEmoji = "🟠";
            else if (hue < 0.4f) colorEmoji = "🟡";
            else if (hue < 0.6f) colorEmoji = "🟢";
            else if (hue < 0.75f) colorEmoji = "🔵";
            else colorEmoji = "🟣";

            string mainText = $"{colorEmoji} {nearest.questName} - {distanceText}";

            // 그림자 (텍스트 오프셋)
            Color origTextColor = GUI.color;

            // 그림자
            GUI.color = _textShadowColor;
            Rect shadowRect = new Rect(panelX + 11f, panelY + 11f, _infoWidth - 10f, _infoHeight * 0.6f);
            GUI.Label(shadowRect, mainText, _infoStyle);

            // 본문
            GUI.color = _textColor;
            Rect textRect = new Rect(panelX + 10f, panelY + 10f, _infoWidth - 10f, _infoHeight * 0.6f);
            GUI.Label(textRect, mainText, _infoStyle);

            GUI.color = origTextColor;

            // 추가 퀘스트 개수 표시
            int extraCount = _cachedMarkers.Count - 1;
            if (extraCount > 0)
            {
                string countText = $"+{extraCount} more";

                GUI.color = _textShadowColor;
                Rect countShadowRect = new Rect(panelX + 11f, panelY + _infoHeight * 0.5f + 11f, _infoWidth - 10f, 20f);
                GUI.Label(countShadowRect, countText, _countStyle);

                GUI.color = _accentColor;
                Rect countRect = new Rect(panelX + 10f, panelY + _infoHeight * 0.5f + 10f, _infoWidth - 10f, 20f);
                GUI.Label(countRect, countText, _countStyle);
            }

            GUI.color = origTextColor;
        }

        /// <summary>
        /// GUI 스타일을 초기화합니다.
        /// </summary>
        private void InitializeStyles()
        {
            if (_stylesInitialized)
                return;

            // 흰색 텍스처 (선, 배경 그리기용)
            _whiteTexture = new Texture2D(1, 1);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();

            // 퀘스트 정보 스타일 (큰 글씨, 굵게)
            _infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                wordWrap = false,
            };
            _infoStyle.normal.textColor = _textColor;

            // 추가 개수 스타일 (작은 글씨)
            _countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
            };
            _countStyle.normal.textColor = _accentColor;

            _stylesInitialized = true;
        }

        /// <summary>
        /// 흰색 1×1 텍스처를 반환합니다 (선 그리기용).
        /// </summary>
        private Texture2D GetWhiteTexture()
        {
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(1, 1);
                _whiteTexture.SetPixel(0, 0, Color.white);
                _whiteTexture.Apply();
            }
            return _whiteTexture;
        }
    }
}