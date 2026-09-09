using System;
using UnityEngine;

namespace SWGUnity2DCore.Manager
{
    /// <summary>Local services only. Online services and game boot policy belong to the host.</summary>
    public static class CoreServices
    {
        private static ResourceManager s_Resource = new ResourceManager();
        private static SoundManager s_Sound = new SoundManager();
        private static UIManager s_UI = new UIManager();
        private static bool s_SoundInitialized;

        public static ResourceManager Resource => s_Resource;
        public static UIManager UI => s_UI;
        public static SoundManager Sound
        {
            get
            {
                if (!s_SoundInitialized)
                {
                    s_Sound.Init();
                    s_SoundInitialized = true;
                }
                return s_Sound;
            }
        }

        // The game chooses whether button clicks play audio, haptics, or nothing.
        public static Action ButtonClicked { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_Resource = new ResourceManager();
            s_Sound = new SoundManager();
            s_UI = new UIManager();
            s_SoundInitialized = false;
            ButtonClicked = null;
        }
    }
}
