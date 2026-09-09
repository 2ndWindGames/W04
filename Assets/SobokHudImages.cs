using UnityEngine;

/// <summary>Shared image buttons, tinted to follow the illustrated sky.</summary>
public static class SobokHudImages
{
    private static Texture2D circle, soundOn, soundOff;

    public static bool SoundButton(Rect rect, bool muted, float night)
    {
        if (circle == null)
        {
            circle = Resources.Load<Texture2D>("UI/circle");
            soundOn = Resources.Load<Texture2D>("UI/sound-on");
            soundOff = Resources.Load<Texture2D>("UI/sound-off");
        }
        bool clicked = GUI.Button(rect, new GUIContent("",
            muted ? "Turn sound on (M)" : "Turn sound off (M)"), GUIStyle.none);
        bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
        bool down = hover && InputPointerDown();
        Color surface = Color.Lerp(new Color(.94f, .93f, .85f), new Color(.23f, .29f, .38f), night);
        Color ink = Color.Lerp(new Color(.36f, .40f, .35f), new Color(.95f, .92f, .85f), night);
        if (hover) surface = Color.Lerp(surface, ink, down ? .18f : .08f);
        var oldColor = GUI.color;
        var shadow = rect;
        shadow.y += rect.height * .045f;
        Paint(shadow, circle, new Color(.08f, .12f, .17f, .12f));
        Paint(rect, circle, surface);
        float size = rect.height * .56f;
        float left = (rect.width - size) * .5f;
        Paint(new Rect(rect.x + left, rect.y + (rect.height - size) * .5f, size, size),
            muted ? soundOff : soundOn, ink);
        GUI.color = oldColor;
        return clicked;
    }

    private static bool InputPointerDown() =>
        (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
        || (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed);

    private static void Paint(Rect rect, Texture2D image, Color color)
    {
        if (image == null) return;
        GUI.color = color;
        GUI.DrawTexture(rect, image, ScaleMode.StretchToFill, true);
    }
}
