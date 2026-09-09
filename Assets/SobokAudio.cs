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
    private AudioLowPassFilter warmth;
    private AudioClip restore;
    private float night;

    private void Awake()
    {
        music = gameObject.AddComponent<AudioSource>();
        effects = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = effects.playOnAwake = false;
        music.spatialBlend = effects.spatialBlend = 0;
        warmth = gameObject.AddComponent<AudioLowPassFilter>();
        warmth.cutoffFrequency = 3800;
        warmth.lowpassResonanceQ = 1;
        var reverb = gameObject.AddComponent<AudioReverbFilter>();
        reverb.reverbPreset = AudioReverbPreset.User;
        reverb.dryLevel = 0;
        reverb.room = -2200;
        reverb.roomHF = -1800;
        reverb.decayTime = 2.6f;
        reverb.reverbLevel = -1600;
        var samples = new float[Rate * 48];
        int[] melody = { 60, 64, 67, 69, 67, 64, 62, 55, 60, 64, 69, 72, 69, 67, 64, 62 };
        for (int i = 0; i < melody.Length; i++)
        {
            AddNote(samples, i * 3f, melody[i], 0.14f, 4.8f);
            AddNote(samples, i * 3f + .38f, melody[i], 0.025f, 4.8f);
        }
        int[] roots = { 48, 45, 53, 55 };
        for (int i = 0; i < roots.Length; i++)
        {
            AddNote(samples, i * 12f, roots[i], 0.085f, 10);
            AddNote(samples, i * 12f + .25f, roots[i] + 7, 0.045f, 10);
            AddPad(samples, i * 12f, roots[i], 14);
        }
        loop = Clip("Sobok - a slow sky (original ambient)", samples);
        var tapSamples = new float[Rate / 3];
        AddNote(tapSamples, 0, 60, 0.22f, 0.28f);
        tap = Clip("Soft wooden tap", tapSamples);
        var chimeSamples = new float[Rate * 2];
        AddNote(chimeSamples, 0, 72, 0.19f, 1.8f);
        AddNote(chimeSamples, 0.18f, 79, 0.085f, 1.6f);
        chime = Clip("Little light", chimeSamples);
        var restoreSamples = new float[Rate * 3];
        AddNote(restoreSamples, 0, 60, .16f, 2.4f);
        AddNote(restoreSamples, .22f, 64, .14f, 2.4f);
        AddNote(restoreSamples, .44f, 67, .12f, 2.4f);
        restore = Clip("Room to breathe", restoreSamples);
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
            float envelope = Mathf.Min(1, t / 0.035f) * Mathf.Exp(-4 * t / duration)
                * Mathf.Clamp01((duration - t) / 0.15f);
            float angle = 2 * Mathf.PI * frequency * t;
            float tone = Mathf.Sin(angle) + 0.12f * Mathf.Sin(angle * 2) + 0.025f * Mathf.Sin(angle * 3);
            // Wrap decaying tails into the loop start so the seam remains continuous.
            samples[(offset + i) % samples.Length] += tone * envelope * gain;
        }
    }

    public void Placement(bool precise) => effects.PlayOneShot(precise ? chime : tap);
    public void Restore() => effects.PlayOneShot(restore);
    public void SetNight(float amount) => night = amount;

    private static void AddPad(float[] samples, float start, int midi, float duration)
    {
        float frequency = 440 * Mathf.Pow(2, (midi - 69) / 12f);
        int offset = Mathf.RoundToInt(start * Rate);
        for (int i = 0; i < duration * Rate; i++)
        {
            float t = i / (float)Rate;
            float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * t / duration), 2) * .025f;
            float angle = 2 * Mathf.PI * frequency * t;
            samples[(offset + i) % samples.Length] += envelope *
                (Mathf.Sin(angle) + .5f * Mathf.Sin(angle * 1.5f));
        }
    }

    private void Update()
    {
        warmth.cutoffFrequency = Mathf.Lerp(warmth.cutoffFrequency, Mathf.Lerp(3800, 1900, night), Time.unscaledDeltaTime);
        music.volume = Mathf.Lerp(music.volume, Mathf.Lerp(.45f,.33f,night), Time.unscaledDeltaTime);
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
        Destroy(restore);
    }
}
