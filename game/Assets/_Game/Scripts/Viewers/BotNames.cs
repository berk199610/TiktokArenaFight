namespace Arena
{
    /// <summary>Fake TikTok-style names for bots (doc §3).</summary>
    public static class BotNames
    {
        static readonly string[] names =
        {
            "kral_ali_34", "melis.xx", "dj_tayfun", "ayse_kaplan", "mert.07", "zeyno_06", "baba_hakan", "can_bey35",
            "elif.tr", "kerem_aslan", "selin_su", "oguzhan_x", "dilara.e", "burak_reis", "ece_mavi", "tolga.42",
            "nazli_pin", "emre_kral", "yagmur.y", "sefa_01", "irem.sky", "hasan_usta", "derya_k", "kaan.tr",
            "gizem.06", "onur_bey", "pelin_m", "umut_34", "busra.e", "furkan_x",
        };

        public static string Pick(int serial) => names[serial % names.Length] + (serial >= names.Length ? (serial / names.Length).ToString() : "");
    }
}
