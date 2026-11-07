# Tasarım notları — dokümandan sapmalar, yorumlar, açık sorular

Doküman (PDF) başlangıç tasarımıdır. Berk'in daha sonra açıkça istediği değişiklikler önceliklidir.
Burada dokümanda belirsiz olan yerlerde verilen kararlar ve sonraki istekler durur.
Hepsi `config.json`'dan değiştirilebilir; kural değişikliği gerekirse önce Berk'e sorulur.

## Son istek — 7 Kasım 2026 (güncel)

Bu kararlar aşağıdaki eski ölüm süresi, rastgele kostüm, ekrana sabit TOP 10 tablo ve yakın kamera notlarının yerine geçer.

23. **Herkes sade başlar.** Görsel taban aynı basit gri Bandit modelidir; eski rastgele kostüm aksesuarları başlangıçta
    takılmaz. FBX malzeme yuvaları projenin URP malzemeleriyle hazırlanır. Üç destek ve beş hediye seviyesi eski
    eşikleri kullanır: 20/100/300 beğeni veya 3/10/25 yorum; yayında biriken 1/30/300/1.000/5.000 coin.
    Bileklik, omuzluk/kemer, hediye zırhı ve tek kısa pelerin kademeli açılır. İlk üçe küçük sıra madalyası verilir.
    Görünüm savaş istatistiklerini değiştirmez.
24. **Fiziksel TOP 5 tablo.** Tribünde metal gövdeli ve sabit konumludur; yalnızca yüzü kameraya döner.
    Ekran koordinatından yerleştirilmez, kamera hareketini izlemez. Normal derinlik testi kullanır; duvar arkasından
    zorla çizdirilmez. Lider, sıra değişimi, devirme parlamaları korunur.
25. **Üç küçük fiziksel pano.** Kalp +1 ve yorum +5; 100+ coin hediye → tam can ve her hediye → geri dönüş;
    köşede iki sütunlu sekiz kademe coin → hareket simgesi rehberi. Panolar tribünde kalır, kameraya döner.
    Eski ekran üstü büyük hediye rehberi gizlenir. 100 coin altındaki hediyeler eski coin×10 can kuralını korur.
26. **Ölüm beklemesi 60 saniye.** Ölenin yerinde küçük mezar taşı ve dolan çubuk kalır; yorum/beğeni beklemeyi atlatmaz.
    Süre dolunca hazırlık/savaşta giriş uygunsa otomatik dönüş olur; yer doluysa sıraya alınır. Yeni tur başlaması kalan
    beklemeyi iptal etmez. Hazırlık/savaşta hediye hemen diriltir ve hareketini korur. Son savaş/kazanan aşamasında
    ölü/yeni oyuncuya gelen hediye sonraki hazırlığa saklanır; hayatta olanın hediyesi mevcut karakterini etkiler.
27. **Sakin kamera.** Normal uzaklık biraz artırılır; mevcut yakın sınır korunur. Yumuşak takip, az miktarda
    yaklaşma/uzaklaşma ve büyük hediyeye küçük geçici yakınlaşma vardır.
28. **Klasör ayrımı.** Düzenlenen Unity kaynağı `C:\Projeler\TiktokArenaFight\game` içindedir. Kaynak derlemesi
    `C:\Projeler\TiktokArenaFight\Builds\Windows`; OneDrive masaüstündeki `TiktokLive\TikTokLiveArenaFight`
    klasörü çalıştırılabilir oyun kopyasıdır. Bu klasörü Unity kaynak projesi sanmayın.
29. **Yalıtılmış kontroller.** `-test-data <klasör>` ayrı kayıt alanı, `-bridge-url <adres>` ayrı köprü, `-review`
    kıyafet/ölüm/hediye/sabit tablo senaryosu için kullanılır. Bunlar bir test yöntemi tarifidir; başarılı çalışma
    ve gerçek TikTok LIVE bağlantısı ayrıca doğrulanır.

## Kararlar (2026-11-07)
1. **Ölüm cezası 40 sn.** Doküman bölüm 1'de 30 sn, bölüm 3'te 40 sn diyor; ayrıntılı kural (40) uygulandı
   (`death.penaltySec`).
2. **Hediye koruması 5 dk.** Bölüm 3 tablosu 5 dk, bölüm 4 "10 dakika" diyor; tablo (5 dk) uygulandı
   (`roster.giftProtectionSec` = 300).
3. **Son savaşın başlaması:** "3. dakikadan sonra veya arenada 10 kişi kalınca". Arenada hiç 10'dan fazla kişi
   olmadıysa (az izleyicili yayın) son savaş hemen başlamasın diye: 10 kişi kuralı yalnızca giriş penceresi (90 sn)
   kapandıktan sonra ve turda daha önce 10'dan fazla kişi varken geçerli. Aksi hâlde 3. dakikada başlar.
4. **Skor tablosu arenanın içinde** (Berk'in isteği, dokümanın "sağ kenarda ilk 5" maddesinin yerine):
   kameranın karşısındaki duvarın önünde, 6 satır (`board.rows`). Ekranda ayrıca sağ kenar listesi yok.
5. **Olay satırı** hediye hareketi gerçekten başladığında yazılır (sırada bekleyen hareket başlayınca),
   böylece "Ahmet34 TORNADO!" ile ekrandaki tornado aynı anda görünür.
6. **Botlar** skor tablosuna yazılabilir ama kayıtları dosyaya yazılmaz; bot kazanırsa "Bot kazandı".
7. **Boss**: turun en çok hediye atanı ya da turda 100+ coin atan. Boss'u deviren +50 ek puan alır
   (`points.bossKill`, `points.bossMinRoundCoins`).
8. **Güçlü Yumruk** rakibi geri iter: Berk'in isteğiyle "süper darbe" olarak rakip havaya uçar, yere düşer, kalkar.
   Döner tekme, dalga ve tornado darbeleri de aynı uçma/düşme/kalkmayı kullanır.
9. **Kaslı / cılız görünüm** (PDF son sayfa): girişte hafif kaslı, hediye gelince en kaslı, etkileşim olmadıkça
   gövde incelir (`vigor`). Dev Mod ayrıca 1,4 kat büyütür.
10. **Kamera**: köşeden bakış (yaw 45°), eğim 42°, uzaklık 16,5–21,5 m (+/- 1 m yavaş "nefes"), dövüş ekranın orta-alt
    kısmında kalsın diye 3,5 m ileriye bakar; merkezden bakış yönünde en fazla 3 m, yana 6 m kayar, hız sınırlıdır.
11. **Hediye kademesi** tek hediyenin coin değerine göre seçilir; aynı hediyeden seri gelirse (count) süre uzar.
12. **Yorum dışı olaylar**: takip ve paylaşım yorum gibi sayılır (sahnede tutar, +5 can); "join" (canlıya girme)
    hiçbir şey yapmaz.
13. **Sopa**: yumruk menzili ×1,5 ve hasarı ×2; tekmeler de sopa savurmasına döner.
14. **Döner tekme / tornado / dalga / dev** sırasında karakter itilip düşürülmez, sadece sarsılır.

## 2. tur kararları (Berk'in isteğiyle dokümandan değişenler)
15. **Ölüm cezası 3 sn** (death.penaltySec): ölen, duman çıktıktan sonra yorum yazarsa hemen geri girer; bu, o turda
    zaten savaşmış olanlar için savaş boyunca geçerli (roster.reentryByCommentUntilFinal). Yeni gelenler için yorumla
    giriş yine ilk 90 sn. Hediye her zaman.
16. **Hazırlık 15 sn** ve ekranda "JOIN NOW!" geri sayımı.
17. **Silah değişimi:** sopa / kürek / güçlü yumruk varken yeni hediye gelirse eskisi hemen biter (doküman: "yenisi bekler").
    Döner tekme, dalga, şimşek, tornado hâlâ birbirini bekler; **Dev mod** her şeyle birlikte çalışır.
18. **Yeni kademe Şimşek** 2.500–4.999 coin; Dev 1.000–2.499. Kademe içinde güç 1,0–2,0 (süre / yarıçap / vuruş sayısı).
19. **Darbe tepkisi güçle**: combat.knockdownPoise (95) ve launchPoise (135) eşikleri; tek yumruk kimseyi düşürmez.
20. **Ateş halkası** 11,6 → 6,5 m, 75 sn (doküman 12 → 4 m, 60 sn). Uzun sürerse 3 m'ye kadar yavaşça kapanır.
21. **Ekran dili İngilizce + ikon** (doküman Türkçe örnekler veriyordu).
22. **Skor tablosu** ekrana sabit, kameraya dönük asılı ekran; duvarlar onu kapatmaz. İsim/emoji etiketleri tablonun
    üstüne düşerse gizlenir.

## Varlıklar ve lisanslar
| Varlık | Kaynak | Lisans | Repoda |
|---|---|---|---|
| Stickman karakterleri (10) | HYPERCASUAL – Tall Stickman Pack 1 (Asset Store) | Asset Store EULA | Hayır (`_Private`) |
| Arena modeli | gladiators-low-poly-arena-small-version | belirsiz → yerel | Hayır (`_Private`) |
| Animasyonlar | Quaternius UAL1 + UAL2 | CC0 | Evet |
| Ek animasyonlar (tekme, döner tekme, flex, slam, tornado, düşme) | koddan üretildi (`Arena > Kurulum`) | — | Evet |
| Yazı tipi | Barlow Condensed | OFL | Evet |

## Açık sorular (Berk'e)
- Dalga (300 coin) herkesi düşürüyor; çok güçlü gelirse hasar 150 → daha düşük olabilir.
- Hediye guide'ında coin yerine TikTok hediye resimleri istenirse resim dosyaları gerekir.
