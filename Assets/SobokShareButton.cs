using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Image-only share control on a screen-space Canvas.</summary>
public sealed class SobokShareButton : MonoBehaviour
{
    private RectTransform root;
    private RawImage surface;
    private RawImage icon;
    private Button button;
    private GameObject ownedEventSystem;

    public void Initialize(UnityAction onClick)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
        {
            ownedEventSystem = new GameObject("SOBOK UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        surface = CreateImage("Share Button", transform, "UI/circle");
        root = surface.rectTransform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        surface.raycastTarget = true;
        button = surface.gameObject.AddComponent<Button>();
        button.targetGraphic = surface;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);
        icon = CreateImage("Share Icon", root, "UI/share");
        icon.rectTransform.anchorMin = new Vector2(.20f, .20f);
        icon.rectTransform.anchorMax = new Vector2(.80f, .80f);
        icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
        root.gameObject.SetActive(false);
    }

    public void Refresh(Rect screenRect, float night, bool visible, bool busy)
    {
        root.gameObject.SetActive(visible);
        if (!visible) return;
        root.anchoredPosition = new Vector2(screenRect.center.x - Screen.width * .5f,
            Screen.height * .5f - screenRect.center.y);
        root.sizeDelta = screenRect.size;
        Color background = Color.Lerp(new Color(.94f, .93f, .85f), new Color(.23f, .29f, .38f), night);
        Color ink = Color.Lerp(new Color(.36f, .40f, .35f), new Color(.95f, .92f, .85f), night);
        var colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = Color.Lerp(background, ink, .08f);
        colors.pressedColor = Color.Lerp(background, ink, .18f);
        colors.selectedColor = background;
        colors.disabledColor = new Color(background.r, background.g, background.b, .6f);
        colors.fadeDuration = .12f;
        button.colors = colors;
        button.interactable = !busy;
        icon.color = busy ? new Color(ink.r, ink.g, ink.b, .5f) : ink;
        icon.rectTransform.localRotation = busy
            ? Quaternion.Euler(0, 0, -Time.unscaledTime * 90) : Quaternion.identity;
    }

    private static RawImage CreateImage(string name, Transform parent, string resource)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage))
            .GetComponent<RawImage>();
        image.transform.SetParent(parent, false);
        image.texture = Resources.Load<Texture2D>(resource);
        image.raycastTarget = false;
        return image;
    }

    private void OnDestroy()
    {
        if (ownedEventSystem != null) Destroy(ownedEventSystem);
    }
}
