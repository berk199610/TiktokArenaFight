# Arena Dövüşü — TikTok LIVE oyunu (Unity 6)

TikTok izleyicileri yorum, beğeni ya da hediyeyle arenaya karakter olarak girer; karakterler kendi akıllarıyla
dövüşür, son kalan turu kazanır. Kurallar: [`docs/Arena-Dovusu-Tasarim.pdf`](docs/Arena-Dovusu-Tasarim.pdf)
(metin hâli: [`docs/DESIGN.md`](docs/DESIGN.md)). Uygulamadaki kararlar: [`docs/TASARIM-NOTLARI.md`](docs/TASARIM-NOTLARI.md).

## Hızlı başlangıç

### Unity'de açıp oynamak
1. Unity Hub → **Add** → `C:\Projeler\TiktokArenaFight\game` klasörünü seç (Unity **6000.3.28f1**).
2. Proje açılınca `Main` sahnesi ve dikey **TikTok 1080x1920** oyun penceresi kendiliğinden gelir
   (gelmezse: Game penceresinin üstündeki boyut listesinden "TikTok 1080x1920"yi seç, sahne: `Assets/Scenes/Main.unity`).
3. **Play**'e bas. Sol üstte **Test paneli** açılır (F1 ile gizle/göster).

### Test tuşları (TikTok olmadan)
| Tuş | Ne yapar |
|---|---|
| **C** | Yorum (seçili izleyiciden) |
| **L** | Beğeni |
| **1 … 7** | Hediye: 1 Yumruk · 2 Sopa · 3 Kürek · 4 Döner Tekme · 5 Dalga · 6 Dev · 7 Tornado |
| **N / V** | 1 / 10 yeni izleyici yorumla girer |
| **T** | Kalabalık botu aç/kapa (sahte izleyiciler sürekli yorum/beğeni/hediye atar) |
| **E** | Turu bitir (son savaşa geç / kazananı belirle) |
| **P** | Duraklat |
| **Fare tıklaması** | Bir karaktere tıkla → o seçilir, tuşlar onu etkiler. Boşluğa tıkla → "Berk (sen)" |
| **Ctrl+Shift+R (3 sn basılı)** | Skor tablosunu sıfırla |

### Yayın düzeni (köprü + panel + oyun)
`BASLAT.bat`'a çift tıkla: köprüyü başlatır (port **8788**), yayıncı panelini tarayıcıda açar
(`http://localhost:8788`), derlenmiş oyunu açar. Panelden: tek izleyici olarak yorum/beğeni/7 hediye,
kalabalık botu kaydırıcıları, duraklat, turu bitir, skor tablosunu sıfırla, tüm kayıtları sil, kullanıcıyı çıkar.
TikTok bağlantısı (Aşama 5) henüz yok: `bridge/src/tiktok.js`.

### Sayıları değiştirmek
Tüm sayılar `game/Assets/StreamingAssets/config.json` içinde (derlenmiş oyunda
`Builds/Windows/ArenaDovusu_Data/StreamingAssets/config.json`). Oyun açıkken dosyayı kaydetmek yeterli,
2 saniye içinde yeniden okunur. Örnekler: `health.start` (1000), `combat.punchDamage`, `moves.tornado.durationSec`,
`camera.pitchDeg` (eğim), `camera.minDistance/maxDistance` (uzaklık), `board.rows` (skor tablosu satırı).

## Ne var (Aşama 1–4 büyük ölçüde)
- **Arena**: gladyatör arena modeli, 12 m'lik dövüş çemberi, solda ve sağda kemerli + meşaleli kapı,
  sıcak ışık, karanlık arka plan.
- **Kamera**: köşeden ~42° eğik 2.5D; herkesin x/z ortalamasını 1,5 sn'de yumuşakça takip eder, merkezden en fazla
  5 m uzaklaşır, kalabalık dağılınca geri çekilir. 100+ coin hediyede hafifçe o karaktere kayar.
- **Skor tablosu arenanın içinde**, kameranın tam karşısında: puan sırası, devirme sayısı; sıra değişince satırlar
  kayar, devirmede satır parlar ve "+10" çıkar, turun en çok hediye atanı altın renkte.
- **15 kostüm** (dokümandaki 12 + Samuray, Keskin Nişancı, Süper Kız): 10 stickman modeli + koddan aksesuarlar
  (bıyık, pompon, taç, pelerin, afro, robot kafa, astronot kaskı, şef şapkası…). Her kostümün kişiliği var.
  1.000 coin toplayan izleyicinin aksesuarları kalıcı olarak altın olur.
- **Rig ve animasyonlar**: tüm modeller Humanoid; Quaternius animasyon kütüphanesi (CC0) + koddan üretilen
  tekme, döner tekme, güç pozu (flex), yer vuruşu, tornado dönüşü, geriye düşme. Yürüme/koşma karışımı, 3 farklı
  yumruk, tekme, sopa/kürek savurma, üst gövde darbe tepkisi, sendeleme, **havaya uçup yere düşme ve kalkma**,
  ölüm (başında yıldızlar + duman), sersem bekleme, alay/dans, zafer dansı, uyanma silkinmesi, yana yuvarlanma.
  Hasar animasyonun temas anında (Animation Event), güçlü vuruşta 0,05 sn vuruş donması.
- **Zekâ (utility AI)**: saldır / intikam / etrafında dön / kaç / ortaya dön / alay et / nefeslen; her beyin
  0,2–0,4 sn'de bir kendi zamanında düşünür; hedefe en fazla 3 kişi saldırır; kameradaki hedefler tercih edilir;
  5 kişilik (Saldırgan, Fırsatçı, Temkinli, Kinci, Şovmen); zekâ etkileşimle %100, 45–90 sn sonra %10'a iner
  ("zzz", ters yöne yürüme, sersemleme), yorum/beğeni/hediye ile "!" ve silkinmeyle uyanır.
- **Kurallar**: giriş/sıra/atılma/ölüm (40 sn ceza)/diriltme, hediye koruması, aktiflik kontrolü,
  beğeni +1 (saniyede en fazla 5), yorum +5, hediye coin×10 can, botlar (az kişide 5–7 bot, 20 gerçek oyuncudan
  sonra bot yok), hediye atanın adı altın + parıltı, turun en çok hediye atanına taç.
- **7 hediye hareketi** (her biri kendi sınıfında): Güçlü Yumruk, Sopa, Kürek, Döner Tekme, Yumruk Dalgası,
  Dev Mod, Tornado. Önce 0,5 sn güç alma pozu + gökten ışık sütunu. Seri hediye süreyi uzatır (en fazla 60 sn),
  aynı anda tek hareket, en fazla 3 sırada.
- **Tur akışı**: Hazırlık 10 sn (saniyede 5 kişi, kapılar dönüşümlü, önce hediye atanlar) → Savaş (ilk 90 sn
  yorum/beğeniyle giriş) → Son savaş (ateş halkası 12→4 m, dışında saniyede 30 hasar) → Kazanan 8 sn (dans, taç,
  konfeti). Puan: kazanma 100, devirme 10, son 10'a kalma 20, boss (çok hediye atan) devirme +50.
- **Kalıcı kayıtlar**: `%USERPROFILE%\AppData\LocalLow\Berk\Arena Dovusu\arena_records.json`.
- **Performans**: bu bilgisayarda (entegre Radeon) 50+ karakterle 60 FPS ölçüldü.

## Eksikler / sonraki adımlar
- Aşama 5: TikTok LIVE bağlantısı (`bridge/src/tiktok.js`) ve az izleyicili deneme yayınları.
- Ses yok.
- Kostüm aksesuarları basit şekillerden; istenirse gerçek model parçalarıyla değiştirilebilir.
- 100 karakterde FPS henüz TikTok LIVE Studio yayınıyla birlikte ölçülmedi.

## Klasörler
```
game/                     Unity projesi (URP)
  Assets/_Game/Scripts/   Core, Viewers, Fighters, SpecialMoves, Gifts, World, CameraRig, UI, Net, Dev, FX
  Assets/_Game/Editor/    Arena > Kurulum (rig, animasyon, Animator, sahne) ve Arena > Windows için derle
  Assets/_Game/Art/       Quaternius animasyonları (CC0) + koddan üretilen animasyonlar
  Assets/_Private/        Lisanslı dosyalar (stickman paketi, arena modeli) — GitHub'a gönderilmez
  Assets/StreamingAssets/config.json   tüm sayılar
bridge/                   Node köprü (simülatör + yayıncı paneli, port 8788)
docs/                     tasarım dokümanı ve notlar
tools/node/               taşınabilir Node (GitHub'a gönderilmez)
BASLAT.bat                köprü + panel + oyun
```

## Komutlar (geliştirici)
```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.28f1/Editor/Unity.exe"
# kurulum (rig, animasyonlar, Animator, sahne) + Windows derlemesi -> Builds/Windows/ArenaDovusu.exe
"$UNITY" -batchmode -quit -projectPath "C:/Projeler/TiktokArenaFight/game" -executeMethod Arena.EditorTools.BuildScript.SetupAndBuild -logFile _test-output/build.log
# otomatik testler (ekran görüntüleri _test-output altına)
Builds/Windows/ArenaDovusu.exe -screen-width 608 -screen-height 1080 -autotest 60 -shots C:/Projeler/TiktokArenaFight/_test-output/auto
Builds/Windows/ArenaDovusu.exe -screen-width 608 -screen-height 1080 -gallery -shots C:/Projeler/TiktokArenaFight/_test-output/gallery
Builds/Windows/ArenaDovusu.exe -screen-width 608 -screen-height 1080 -gifts -shots C:/Projeler/TiktokArenaFight/_test-output/gifts
```
Unity editörü projeyi açıkken batch komutları çalışmaz (proje kilidi) — önce editörü kapatın.

## Lisanslar
- Quaternius Universal Animation Library 1 ve 2: CC0.
- Barlow Condensed yazı tipi: SIL OFL.
- "HYPERCASUAL – Tall Stickman Pack 1" (Unity Asset Store) ve "Gladiators low poly arena": yalnızca yerel,
  `game/Assets/_Private/` altında, repoya girmez. Repoyu başka bir bilgisayara klonlayınca bu iki paketi oraya
  yeniden koyup **Arena > Kurulum** menüsünü çalıştırın.
