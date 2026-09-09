using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Original synthesized placeholder music: a slow pentatonic loop and soft placement tones.</summary>
public sealed class SobokAudio : MonoBehaviour
{
    private const int Rate = 22050;
    private AudioSource music;
    private AudioSource effects;
    private AudioClip loop;
    private AudioClip tap;
    private AudioClip chime;
    private bool muted;

    private void Awake()
    {
        music = gameObject.AddComponent<AudioSource>();
        effects = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = effects.playOnAwake = false;
        music.spatialBlend = effects.spatialBlend = 0;
        var samples = new float[Rate * 32];
        int[] melody = { 60, 64, 67, 69, 67, 64, 62, 55, 60, 64, 69, 72, 69, 67, 64, 62 };
        for (int i = 0; i < melody.Length; i++)
            AddNote(samples, i * 2f, melody[i], 0.19f, 3.5f);
        int[] roots = { 48, 45, 53, 55 };
        for (int i = 0; i < roots.Length; i++)
        {
            AddNote(samples, i * 8f, roots[i], 0.11f, 7);
            AddNote(samples, i * 8f + 0.1f, roots[i] + 7, 0.06f, 7);
        }
        loop = Clip("Sobok - quiet afternoon (original synth)", samples);
        var tapSamples = new float[Rate / 3];
        AddNote(tapSamples, 0, 60, 0.35f, 0.28f);
        tap = Clip("Soft wooden tap", tapSamples);
        var chimeSamples = new float[Rate * 2];
        AddNote(chimeSamples, 0, 79, 0.25f, 1.8f);
        AddNote(chimeSamples, 0.12f, 84, 0.12f, 1.6f);
        chime = Clip("Little light", chimeSamples);
        music.clip = loop;
        music.loop = true;
        music.volume = 0.45f;
        effects.volume = 0.4f;
        muted = PlayerPrefs.GetInt("Sobok.Muted", 0) == 1;
        music.mute = effects.mute = muted;
        music.Play();
    }

    private static AudioClip Clip(string title, float[] samples)
    {
        var clip = AudioClip.Create(title, samples.Length, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void AddNote(float[] samples, float start, int midi, float gain, float duration)
    {
        float frequency = 440 * Mathf.Pow(2, (midi - 69) / 12f);
        int count = Mathf.RoundToInt(duration * Rate);
        int offset = Mathf.RoundToInt(start * Rate);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float envelope = Mathf.Min(1, t / 0.018f) * Mathf.Exp(-4 * t / duration)
                * Mathf.Clamp01((duration - t) / 0.15f);
            float angle = 2 * Mathf.PI * frequency * t;
            float tone = Mathf.Sin(angle) + 0.22f * Mathf.Sin(angle * 2) + 0.06f * Mathf.Sin(angle * 3);
            // Wrap decaying tails into the loop start so the seam remains continuous.
            samples[(offset + i) % samples.Length] += tone * envelope * gain;
        }
    }

    public void Placement(bool precise) => effects.PlayOneShot(precise ? chime : tap);

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) ToggleSound();
    }

    private void ToggleSound()
    {
        muted = !muted;
        music.mute = effects.mute = muted;
        PlayerPrefs.SetInt("Sobok.Muted", muted ? 1 : 0);
    }

    private void OnGUI()
    {
        if (GUI.Button(SoundRect,
            muted ? "Sound off" : "Sound on")) ToggleSound();
    }

    private Rect SoundRect
    {
        get
        {
            float scale = Mathf.Min(Screen.width / 480f, Screen.height / 720f);
            return new Rect(Screen.width - 92 * scale, 12 * scale, 80 * scale, 32 * scale);
        }
    }

    public bool IsSoundPointer()
    {
        Vector2 position;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            position = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            position = Mouse.current.position.ReadValue();
        else return false;
        position.y = Screen.height - position.y;
        return SoundRect.Contains(position);
    }

    private void OnDestroy()
    {
        Destroy(loop);
        Destroy(tap);
        Destroy(chime);
    }
}
