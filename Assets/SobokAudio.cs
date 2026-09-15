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
    private AudioClip launch;
    private AudioClip breachBody;
    private AudioClip breachDebris;
    private float night;
    private SobokShare sharing;

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
        launch = Clip("Tower launch - rising air", LaunchSamples());
        breachBody = Clip("Breach - low impact", BreachSamples(false));
        breachDebris = Clip("Breach - fractured edges", BreachSamples(true));
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
    public void Launch() => effects.PlayOneShot(launch, .85f);

    /// <summary>The wall always lands with weight; a more damaged tower sheds louder fragments.</summary>
    public void Breach(float retainedRatio)
    {
        float retained = float.IsNaN(retainedRatio) ? 0 : Mathf.Clamp01(retainedRatio);
        effects.PlayOneShot(breachBody, .95f);
        effects.PlayOneShot(breachDebris, Mathf.Lerp(.65f, .18f, retained));
    }

    public void SetNight(float amount) => night = amount;

    private static float[] LaunchSamples()
    {
        const float duration = .42f;
        var samples = new float[Mathf.RoundToInt(Rate * duration)];
        var noise = new System.Random(7041);
        float air = 0;
        float phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)Rate;
            float progress = t / duration;
            float white = (float)noise.NextDouble() * 2 - 1;
            air += (white - air) * Mathf.Lerp(.06f, .42f, progress);
            phase += 2 * Mathf.PI * Mathf.Lerp(130, 680, progress * progress) / Rate;
            float attack = Mathf.Clamp01(t / .025f);
            float release = Mathf.Clamp01((duration - t) / .055f);
            float envelope = attack * release * Mathf.Lerp(.30f, 1, progress);
            // A filtered air rush and rising body give the tower motion without a piercing whistle.
            float value = (air * 1.15f + Mathf.Sin(phase) * .22f) * envelope;
            samples[i] = value / (1 + Mathf.Abs(value) * .4f);
        }
        return samples;
    }

    private static float[] BreachSamples(bool fragments)
    {
        const float duration = .68f;
        var samples = new float[Mathf.RoundToInt(Rate * duration)];
        var noise = new System.Random(fragments ? 319 : 117);
        float bodyPhase = 0;
        float filtered = 0;
        float previous = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)Rate;
            float white = (float)noise.NextDouble() * 2 - 1;
            filtered += (white - filtered) * (fragments ? .38f : .16f);
            float attack = Mathf.Clamp01(t / .0015f);
            float tail = Mathf.Clamp01((duration - t) / .09f);
            float value;
            if (fragments)
            {
                // Several shrinking grains sound like separated chips rather than continuous hiss.
                float grains = Mathf.Exp(-22 * t);
                grains += Grain(t, .065f, 43) * .75f;
                grains += Grain(t, .145f, 37) * .47f;
                grains += Grain(t, .245f, 31) * .28f;
                grains += Grain(t, .365f, 28) * .13f;
                float crisp = filtered - previous * .58f;
                value = crisp * grains * 1.7f;
            }
            else
            {
                // A rapid pitch drop gives a solid, low strike, with a short gritty contact transient.
                bodyPhase += 2 * Mathf.PI * (54 + 155 * Mathf.Exp(-20 * t)) / Rate;
                float body = (Mathf.Sin(bodyPhase) + Mathf.Sin(bodyPhase * 1.91f) * .16f)
                    * Mathf.Exp(-8.5f * t) * .72f;
                float contact = filtered * Mathf.Exp(-24 * t) * 1.3f;
                value = body + contact;
            }
            previous = filtered;
            value *= attack * tail;
            samples[i] = value / (1 + Mathf.Abs(value) * .4f);
        }
        return samples;
    }

    private static float Grain(float time, float start, float decay)
    {
        float elapsed = time - start;
        return elapsed < 0 ? 0 : Mathf.Clamp01(elapsed / .001f) * Mathf.Exp(-elapsed * decay);
    }

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
        if (sharing == null) sharing = GetComponent<SobokShare>();
        if (sharing != null && sharing.Capturing) return;
        if (SobokHudImages.SoundButton(SoundRect, muted, night)) ToggleSound();
    }

    private Rect SoundRect
    {
        get
        {
            float scale = Mathf.Min(Screen.width / 480f, Screen.height / 720f);
            Rect safe = Screen.safeArea;
            return new Rect(safe.xMax - 60 * scale, Screen.height - safe.yMax + 12 * scale,
                48 * scale, 48 * scale);
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
        Destroy(launch);
        Destroy(breachBody);
        Destroy(breachDebris);
    }
}
