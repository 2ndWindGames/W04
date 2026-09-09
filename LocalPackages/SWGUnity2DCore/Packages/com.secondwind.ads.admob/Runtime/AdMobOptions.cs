namespace SWGUnity2DCore.Manager
{
    public sealed class AdMobOptions
    {
        public string AndroidBannerId { get; set; } = "ca-app-pub-3940256099942544/6300978111";
        public string AndroidInterstitialId { get; set; } = "ca-app-pub-3940256099942544/1033173712";
        // New consumers use test ads until explicitly configured for production.
        public bool ForceTestAds { get; set; } = true;
    }
}
