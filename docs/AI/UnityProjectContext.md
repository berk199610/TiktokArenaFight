# Arena Dövüşü proje bağlamı

Son inceleme: 7 Kasım 2026. Kaynak: `C:/Projeler/TiktokArenaFight`; Unity kökü `game/`.
OneDrive/TiktokLive/TikTokLiveArenaFight klasörü kaynak proje değil, dağıtılmış Windows oyunudur.

- Unity 6000.3.28f1, URP 17.3.0, Input System 1.20.1, uGUI 2.0.0. Namespace `Arena`, runtime assembly `Arena.Game`; Editor assembly `Arena.Editor`.
- `Assets/Scenes/Main.unity` başlangıç sahnesidir. `GameManager.Awake` dünyayı, karakter havuzunu, kamerayı, panoları ve köprüyü koddan oluşturur. Sahneye elle yeni sistem bağlamak gerekmez.
- `EventRouter` yorum/beğeni/hediyeyi `ViewerRegistry` ve `RosterManager` üzerinden karaktere aktarır. `FighterSystem` karakterleri havuzda tutar. `Gear` yalnızca görünümü değiştirir; kostüm kimlikleri kişilikleri belirlemeye devam eder.
- `ArenaWorld` gladyatör modelini gerçek ölçülere oturtur. Kamera yönünde arka tribündeki `boardAnchor` sabittir. Dünya panoları konum değiştirmeden kameraya döner.
- `StreamingAssets/config.json` sayısal kuralları tutar ve iki saniyede bir değişiklik için kontrol edilir. Yeni başlangıç değerleri `GameConfig` ile eş tutulmalıdır.
- Gerçek izleyici kayıtları `Application.persistentDataPath/arena_records.json` içinde. Testlerde mutlaka `-test-data <ayrı-klasör>` kullanılır. `-bridge-url ws://127.0.0.1:<ayrı-port>/game` test olaylarını gerçek köprüden ayırır.
- Node ESM köprü `bridge/`, varsayılan port8788, panel `/`, oyun WebSocket `/game`. Güncel TikTok bağlayıcısının olayları `tiktok-events.js` içinde normalleştirilir. Seri hediyelerde yalnızca yeni adetler gönderilir.
- Lisanslı modeller/kaplamalar `Assets/_Private/` içindedir ve Git'e girmez. Kamuya açık kod bu varlıklar olmadan sade bir yedek model kullanır.
- Derleme: Unity `-batchmode -quit -projectPath <game> -executeMethod Arena.EditorTools.BuildScript.BuildWindows -logFile <log>`; çıktı `Builds/Windows/ArenaDovusu.exe`. Materyal denetimi bu derleme içinde çalışır. Sahneleri baştan kuran `SetupAndBuild` rutin bir kod değişikliğinde gerekmez.
- Çalışan Unity MCP bulunamadı. Kurulu Editor ile batch derleme ve Windows oyuncusunda `-review`, `-autotest`, `-gifts`, `-gallery` senaryoları kullanılabilir. Görsel değişikliklerin ekran görüntüleri gerçekten incelenmelidir.

Kısıtlar: Kullanıcının yerel değişiklikleri korunur. Kaynak ve OneDrive'daki eski derlemenin birebir aynı olduğu varsayılmaz. Gerçek TikTok LIVE bağlantısı yerel simülasyonla kanıtlanamaz; canlı giriş ayrı doğrulama gerektirir. Son savaş/kazanan sırasında gelen hediyeler mevcut tur kurallarına göre sonraki turda uygulanır.
