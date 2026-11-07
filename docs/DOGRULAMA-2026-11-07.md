# 7 Kasım 2026 doğrulaması

Windows oyunu derlendi, kaynak çıktı ve OneDrive dağıtım kopyası çalıştırıldı. Gerçek TikTok LIVE oturumu açılmadı.

## Uygulanan davranış

- İlk giriş sade gri kıyafet. Yorum/beğeniler üç destek seviyesi; hediyeler beş görünüm seviyesi açar. Eşikler korunur. İlk üçe sıralama madalyası eklenir.
- TOP 5 ve üç simgeli pano gerçek sahne konumlarında sabittir. Kamera yalnızca yüzlerini döndürür; büyük ekran üstü hediye listesi gizlidir.
- Hediye atmayan ölü oyuncu 60 saniye bekler. Küçük mezar taşındaki çubuk ilerler. Uygun hazırlık/savaş aşamasında süre sonunda otomatik dönüş; hediye ile hemen dönüş vardır. Final/kazanan sırasında hediyeler sonraki tura saklanır.
- Kamera biraz daha uzaktan ve yumuşak hareket eder. Büyük hediyede sınırlı yakınlaşma vardır.
- TikTok olaylarında kimlik/hediye alanı uyumluluğu, seri hediye tekilleştirme ve yeniden bağlantı yarışları düzeltildi. Başlatıcı çalışan köprüyü tekrar kullanır.

## Pembe çizimin nedeni

`M_XRay.mat` eski derleme klasöründeki shader GUID'sine bağlıydı. Ana projedeki `XRay.shader.meta` farklıydı.
Bu bozuk ek çizim, zeminin arkasında kalan ayakları ve bazı düşmüş karakter parçalarını hata pembesi yapıyordu.
Materyal bağlantısı ana projedeki shader'a yeniden bağlandı; duvar arkası gösterimi açık kaldı. `M_UIOnTop` bağlantısı da onarıldı.

Model malzemelerini kontrol etmek tek başına bu sorunu yakalamadı; renderer özelliğinin kullandığı ek materyal de denetlendi.
Eşdeğer 608×1080 kıyafet görüntüsünde hata pembesi piksel sayısı 1.019'dan sıfıra indi. Tüm model ve ekipman malzemeleri destekleniyor.
Kıyafet parçalarının pozlu modeldeki boyutları ayrıca doğrulandı; büyük kafa bazı parçaları önden kapatsa da dövüş sırasında görünürler.

## Sonuçlar

| Kontrol | Sonuç | Kanıt |
|---|---|---|
| Unity 6000.3.28f1 Windows derlemesi | Başarılı, 119 MB, çıkış0 | `_test-output/release-build.log` |
| Materyal ve ek shader bağlantıları | Geçersiz yuva0 | `_test-output/material-audit.txt` |
| Köprü testleri | 15 başarılı, 0 hata | Node test komutu aşağıda |
| Gerçek Windows oyuncusunda akış | 20 başarılı, 0 hata | `_test-output/review-player-final.log` |
| Sekiz hediye hareketi | Sekizi de çalıştı | `_test-output/gifts-player-final.log` |
| Panel HTTP → WebSocket → gerçek Unity oyuncusu | 30 coin hediye ulaştı, oyuncu doğdu | Son review koşusu |
| OneDrive kopyası | Çalıştı, DLL SHA256 kaynakla aynı | `_test-output/delivery-player.log` |
| Kısa teslim kopyası kalabalık denemesi | 10. saniyede46 canlı oyuncu,60 FPS | Aynı teslim günlüğü |
| Gerçek TikTok LIVE hediyesi | Bu oturumda denenmedi | Hesap ve canlı yayın bağlantısı başlatılmadı |

Toplam43 davranış kontrolü başarılı: 15 köprü +20 oyuncu akışı +8 hediye hareketi. Kısa FPS gözlemi uzun yayın performansı garantisi değildir.
Ara koşuda test hediyesi oyun bağlantısı hazır olmadan gönderildiği için teslim edilmedi; son koşu hazır olmayı bekledi ve geçti.
Testler `-test-data` ile ayrı kayıt klasörlerinde çalıştı. Gerçek izleyici kayıtları sıfırlanmadı.

## Yeniden çalıştırma

Köprü testleri:

```powershell
& ./tools/node/node.exe --test bridge/test/tiktok-events.test.js bridge/test/bridge.test.js
```

Oyuncu testleri mevcut `AutoTest` üzerinden `-review`, `-gifts`, `-autotest` seçeneklerini kullanır.
`-review` koşusunda test köprüsü `/api/status` ile bağlı ve günlükte ilk `PASS newcomer starts plain` satırı hazır olduktan sonra
`/api/sim` adresine `{"action":"gift","user":"BridgeReview","tier":2,"count":1}` gönderilir. Köprü portu gerçek yayından ayrı olmalıdır.

## Teslim

- Kaynak: `C:/Projeler/TiktokArenaFight/game`.
- Güncel derleme: `C:/Projeler/TiktokArenaFight/Builds/Windows`.
- Güncellenen masaüstü kopyası: `C:/Users/admin/OneDrive/Desktop/TiktokLive/TikTokLiveArenaFight`.
- Masaüstü klasöründeki `BASLAT.bat` güncel köprü, panel ve oyunu açar.
- Eski kaynak derleme ve eski OneDrive oyunu `_test-output/before-changes` altında yedeklendi.
- Görüntüler: `_test-output/review-final`, `_test-output/gifts-final`, `_test-output/delivery-check`.
- Yayında kalan tek doğrulama: panelde TikTok kullanıcı adını bağlayıp gerçek beğeni/yorum/hediye geldiğini kontrol etmek.

Kullanıcının önceden yaptığı köprü/panel değişiklikleri korundu. Sahneler ve animasyon denetleyicisi baştan üretilmedi.
