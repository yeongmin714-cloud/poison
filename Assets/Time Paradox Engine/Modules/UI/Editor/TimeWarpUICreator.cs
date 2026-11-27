using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TimeWarp.Modules.UI;

namespace TimeWarp.Modules.UI.EditorTools
{
    /// <summary>
    /// Builds a working rewind bar in one click.
    ///
    /// This lives in its own editor assembly next to the UI module rather than in the main
    /// TimeWarp editor assembly, so a project that does not use uGUI can delete the whole
    /// Modules/UI folder - runtime and editor together - without leaving a broken reference
    /// behind.
    /// </summary>
    public static class TimeWarpUICreator
    {
        [MenuItem("GameObject/TimeWarp/UI/Rewind Bar", false, 20)]
        public static void CreateRewindBar()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            GameObject createdCanvas = null;

            if (canvas == null)
            {
                createdCanvas = new GameObject("Canvas",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

                canvas = createdCanvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = createdCanvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                Undo.RegisterCreatedObjectUndo(createdCanvas, "Create Canvas");

                if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    var es = new GameObject("EventSystem",
                        typeof(UnityEngine.EventSystems.EventSystem),
                        typeof(UnityEngine.EventSystems.StandaloneInputModule));

                    Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
                }
            }

            // --- Root ---
            var root = new GameObject("Rewind Bar", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 60f);
            rootRect.sizeDelta = new Vector2(420f, 22f);

            // --- Background ---
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);

            var bgRect = background.GetComponent<RectTransform>();
            Stretch(bgRect);

            var bgImage = background.GetComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);

            // --- Fill ---
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(root.transform, false);

            var fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.35f, 0.80f, 1.00f);

            // Filled + Horizontal is what the component drives via fillAmount.
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;

            // Image.Filled needs a sprite to render; the built-in UI sprite is always present.
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (uiSprite != null)
            {
                bgImage.sprite = uiSprite;
                bgImage.type = Image.Type.Sliced;
                fillImage.sprite = uiSprite;
            }

            // --- Component ---
            var bar = root.AddComponent<TimeWarpRewindBar>();
            bar.FillImage = fillImage;
            bar.FadeGroup = root.GetComponent<CanvasGroup>();
            bar.Source = TimeWarpRewindBar.BarSource.AvailableHistory;

            Undo.RegisterCreatedObjectUndo(root, "Create Rewind Bar");
            Selection.activeGameObject = root;

            EditorUtility.DisplayDialog("TimeWarp",
                "Rewind bar created.\n\n" +
                "It defaults to showing how much recorded history the first Time Warp Identity has " +
                "left. Set Target on the component to track a specific object, or switch Source to " +
                "Action Time Remaining if your rewind uses Max Duration.\n\n" +
                "For a text readout, add a TMP_Text and hook its text setter to On Label Changed.",
                "OK");
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
