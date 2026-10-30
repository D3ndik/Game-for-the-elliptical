using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Заготовки uGUI для редакторских сборщиков: интерфейс один раз строится и сохраняется в сцене
public static class UIKit
{
    static Font font;
    public static Font Font => font ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static readonly Color ButtonColor = new Color(0.16f, 0.18f, 0.2f, 0.95f);
    public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.1f, 0.92f);
    public static readonly Color MutedText = new Color(0.8f, 0.82f, 0.85f);

    // Canvas поверх экрана, масштабируется под любое разрешение (эталон 1920×1080)
    public static Transform Canvas(string name)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return go.transform;
    }

    // Без EventSystem кнопки не нажимаются; проект работает только на новой Input System.
    // Берём карту UI из ассета проекта: встроенные действия по умолчанию нельзя сохранить в сцену
    public static GameObject CreateEventSystem()
    {
        var go = new GameObject("EventSystem", typeof(EventSystem));
        var module = go.AddComponent<InputSystemUIInputModule>();
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (actions) module.actionsAsset = actions;
        return go;
    }

    // Текст с чёрной обводкой; anchor — к какому месту экрана/родителя привязан, pos — смещение от него
    public static Text Text(Transform parent, string name, Vector2 anchor, Vector2 pos, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent) go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(900, 300);
        var t = go.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.alignment = align;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.8f);
        outline.effectDistance = new Vector2(2, -2);
        return t;
    }

    // Цветной прямоугольник (Image); blocksClicks — ловит ли он клики мыши
    public static RectTransform Rect(string name, Transform parent, Color color, bool blocksClicks = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = blocksClicks;
        return (RectTransform)go.transform;
    }

    // Растянуть элемент на всего родителя
    public static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    // Полоска-индикатор: тёмная подложка и заливка, ширину заливки меняет код (anchorMax.x)
    public static RectTransform Bar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var bg = Rect(name, parent, new Color(1, 1, 1, 0.15f));
        bg.anchorMin = bg.anchorMax = anchor;
        bg.pivot = pivot;
        bg.anchoredPosition = pos;
        bg.sizeDelta = size;
        return Stretch(Rect("Fill", bg, Color.white));
    }

    // Обработчик нажатия назначается отдельно через UnityEventTools, чтобы он сохранился в сцене
    public static Button Button(Transform parent, string label, Vector2 pos)
    {
        var rt = Rect(label, parent, Color.white, blocksClicks: true);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(420, 80);

        var button = rt.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = new Color(0.26f, 0.3f, 0.33f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.12f, 0.13f, 0.15f, 1f);
        button.colors = colors;

        var text = Text(rt, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, 36, TextAnchor.MiddleCenter);
        text.text = label;
        text.color = new Color(0.92f, 0.93f, 0.95f);
        return button;
    }

    // Горизонтальный ползунок 0..1: дорожка, заливка и ручка
    public static Slider Slider(Transform parent, Vector2 pos)
    {
        var root = new GameObject("Slider", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400, 24);

        // Дорожка ловит клики — можно нажать в любое место, а не только тянуть ручку
        Stretch(Rect("Background", rt, new Color(1, 1, 1, 0.15f), blocksClicks: true));

        var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(rt, false);
        Stretch(fillArea);
        var fill = Rect("Fill", fillArea, new Color(0.55f, 0.75f, 0.8f));
        fill.sizeDelta = Vector2.zero;

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(rt, false);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(12, 0);
        handleArea.offsetMax = new Vector2(-12, 0);
        var handle = Rect("Handle", handleArea, new Color(0.92f, 0.93f, 0.95f), blocksClicks: true);
        handle.sizeDelta = new Vector2(24, 16);

        var slider = root.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }
}
