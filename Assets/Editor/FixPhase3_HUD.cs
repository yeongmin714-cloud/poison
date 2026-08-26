using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Reflection;

public class FixPhase3_HUD
{
    [MenuItem("Tools/Poison/Fix Phase 3 - HUD")]
    public static void FixHUD()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }

        Debug.Log("=== PHASE 3: HUD CREATION START ===");

        // 1. HUD Canvas 생성
        var canvasObj = GameObject.Find("HUD Canvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("HUD Canvas");
            Debug.Log("[Phase3] Created HUD Canvas GameObject");
        }
        else
        {
            Debug.Log("[Phase3] Found existing HUD Canvas GameObject");
        }

        var canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 100;

        var scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            canvasObj.AddComponent<GraphicRaycaster>();

        // 2. BotW 스타일 하트 (좌상단)
        CreateHearts(canvasObj);

        // 3. 미니맵 (우하단)
        CreateMinimap(canvasObj);

        // 4. 버프 UI (우상단)
        CreateBuffUI(canvasObj);

        // 5. 씬 저장
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Phase3] Scene saved to: {scenePath}");
        Debug.Log("=== PHASE 3: HUD CREATION COMPLETE ===");
    }

    static void CreateHearts(GameObject parent)
    {
        var heartsGo = GameObject.Find("Hearts");
        if (heartsGo == null)
        {
            heartsGo = new GameObject("Hearts");
            heartsGo.transform.SetParent(parent.transform);
            Debug.Log("[Phase3] Created Hearts container");
        }

        var rt = heartsGo.GetComponent<RectTransform>();
        if (rt == null) rt = heartsGo.AddComponent<RectTransform>();
        
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(20, -20);
        rt.sizeDelta = new Vector2(200, 40);

        var hlg = heartsGo.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = heartsGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 2;
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        // 5개 하트 생성 (BotW: 1하트 = 20HP, 100HP = 5하트)
        for (int i = 0; i < 5; i++)
        {
            var heartName = $"Heart_{i}";
            var heartGo = heartsGo.transform.Find(heartName)?.gameObject;
            if (heartGo == null)
            {
                heartGo = new GameObject(heartName);
                heartGo.transform.SetParent(heartsGo.transform);
            }

            var img = heartGo.GetComponent<Image>();
            if (img == null) img = heartGo.AddComponent<Image>();
            
            img.color = new Color(1f, 0.3f, 0.3f, 1f); // 빨간색 하트
            img.raycastTarget = false;

            var heartRt = heartGo.GetComponent<RectTransform>();
            if (heartRt == null) heartRt = heartGo.AddComponent<RectTransform>();
            heartRt.sizeDelta = new Vector2(32, 32);

            // HeartUI 컴포넌트 추가 (런타임에서 HP 동기화용) - 리플렉션으로
            var heartUIType = System.Type.GetType("ProjectName.UI.HeartUI, Assembly-CSharp");
            if (heartUIType != null)
            {
                var heartUI = heartGo.GetComponent(heartUIType);
                if (heartUI == null) heartUI = heartGo.AddComponent(heartUIType);
                var idxField = heartUIType.GetField("heartIndex", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (idxField != null) idxField.SetValue(heartUI, i);
            }
        }

        Debug.Log("[Phase3] Created 5 BotW-style hearts");
    }

    static void CreateMinimap(GameObject parent)
    {
        var minimapGo = GameObject.Find("Minimap");
        if (minimapGo == null)
        {
            minimapGo = new GameObject("Minimap");
            minimapGo.transform.SetParent(parent.transform);
            Debug.Log("[Phase3] Created Minimap container");
        }

        var rt = minimapGo.GetComponent<RectTransform>();
        if (rt == null) rt = minimapGo.AddComponent<RectTransform>();
        
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-20, 20);
        rt.sizeDelta = new Vector2(200, 200);

        // 배경 이미지
        var bgImg = minimapGo.GetComponent<Image>();
        if (bgImg == null) bgImg = minimapGo.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.5f);
        bgImg.raycastTarget = false;

        // RawImage for RenderTexture (미니맵 카메라용)
        var rawImgGo = minimapGo.transform.Find("MinimapRender")?.gameObject;
        if (rawImgGo == null)
        {
            rawImgGo = new GameObject("MinimapRender");
            rawImgGo.transform.SetParent(minimapGo.transform);
        }

        var rawImgRt = rawImgGo.GetComponent<RectTransform>();
        if (rawImgRt == null) rawImgRt = rawImgGo.AddComponent<RectTransform>();
        rawImgRt.anchorMin = Vector2.zero;
        rawImgRt.anchorMax = Vector2.one;
        rawImgRt.offsetMin = Vector2.zero;
        rawImgRt.offsetMax = Vector2.zero;

        var rawImg = rawImgGo.GetComponent<RawImage>();
        if (rawImg == null) rawImg = rawImgGo.AddComponent<RawImage>();
        rawImg.color = Color.white;
        rawImg.raycastTarget = false;

        // MinimapUI 컴포넌트 - 리플렉션으로
        var minimapUIType = System.Type.GetType("ProjectName.UI.MinimapUI, Assembly-CSharp");
        if (minimapUIType != null)
        {
            var minimapUI = minimapGo.GetComponent(minimapUIType);
            if (minimapUI == null) minimapUI = minimapGo.AddComponent(minimapUIType);
        }

        Debug.Log("[Phase3] Created Minimap with RawImage");
    }

    static void CreateBuffUI(GameObject parent)
    {
        var buffGo = GameObject.Find("BuffUI");
        if (buffGo == null)
        {
            buffGo = new GameObject("BuffUI");
            buffGo.transform.SetParent(parent.transform);
            Debug.Log("[Phase3] Created BuffUI container");
        }

        var rt = buffGo.GetComponent<RectTransform>();
        if (rt == null) rt = buffGo.AddComponent<RectTransform>();
        
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-20, -20);
        rt.sizeDelta = new Vector2(300, 60);

        var hlg = buffGo.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = buffGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4;
        hlg.childAlignment = TextAnchor.UpperRight;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        // BuffUI 컴포넌트 - 리플렉션으로
        var buffUIType = System.Type.GetType("ProjectName.UI.BuffUI, Assembly-CSharp");
        if (buffUIType != null)
        {
            var buffUI = buffGo.GetComponent(buffUIType);
            if (buffUI == null) buffUI = buffGo.AddComponent(buffUIType);
        }

        Debug.Log("[Phase3] Created BuffUI");
    }
}