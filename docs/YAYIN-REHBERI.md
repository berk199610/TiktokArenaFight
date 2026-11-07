# Yayın rehberi — Arena Dövüşü'nü TikTok LIVE'da açmak

Bunları senin yapman gerekiyor (TikTok hesabına ve TikTok LIVE Studio'ya ben erişemiyorum).

## Yayından önce (bir kez)
1. **Unity editörünü kapat.** Yayın sırasında bilgisayarın gücü oyuna ve yayına kalsın.
2. Skor tablosundaki test isimleri temizlendi; ilk yayın boş tabloyla başlar.

## Her yayında
1. Masaüstünde `TiktokLive\TikTokLiveArenaFight\BASLAT.bat` dosyasına çift tıkla. Üç şey açılır:
   - siyah **köprü** penceresi (yayın boyunca açık kalsın, kapatma),
   - tarayıcıda **yayıncı paneli** (http://localhost:8788),
   - **oyun** penceresi (dikey, 608×1080).
2. **TikTok LIVE Studio**'da:
   1. Yayın yönünü **Dikey (Portrait)** yap.
   2. Kaynak ekle (**+**) → **Pencere Yakalama** (Window Capture) ya da **Oyun Yakalama** (Game Capture) → listeden
      **Arena Dovusu** penceresini seç.
   3. Görüntüyü köşelerinden çekerek tüm dikey alanı kaplayacak şekilde büyüt.
   4. **Canlı yayını başlat.**
3. **Bağlantı kendiliğinden kurulur:** kullanıcı adın (@generalobento) kayıtlı. Yayını başlattıktan sonra en geç
   1 dakika içinde panelde durum **"bağlı (@generalobento)"** olur. Başka hesapla yayın yapacaksan panelde adı değiştirip
   **TikTok LIVE'a bağlan**'a bas.
4. **Kontrol et** (telefondan ya da başka bir hesaptan):
   - bir **yorum** yaz → karakterin bir kapıdan arenaya girmeli,
   - **beğen** → karakterin üstünde "+1" çıkmalı,
   - küçük bir **hediye** (gül gibi) → güç alma pozu ve güçlü yumruk.
   Panelde "TikTok'tan gelen: … yorum · … beğeni · … hediye" sayıları artmalı.

## Dikkat
- Oyun penceresini **simge durumuna küçültme** (yakalama durabilir). Üstüne başka pencere gelebilir.
- Panelde "**canlı yayında değil ya da kullanıcı adı yanlış**" yazıyorsa: önce yayını başlat, adı kontrol et.
  Köprü 10–60 saniyede bir kendiliğinden tekrar dener.
- "**imza sunucusu sınırı doldu**" sık çıkarsa: https://www.eulerstream.com adresinden ücretsiz bir API anahtarı al,
  `bridge\config.json` dosyasına `"signApiKey": "anahtar"` satırı olarak ekle.
- Bir şey ters giderse yayını kapatmadan panelden **Duraklat** ya da **Turu bitir** kullanabilirsin.
- İlk yayında gelen gerçek olayların örnekleri `bridge\data\` klasörüne kaydedilir (`tiktok-ornek-*.json`,
  hangi hediye kaç coin: `gifts-seen.json`). Hata çıkarsa bu dosyalara bakarak düzeltiriz.

## Yayından sonra
Oyun penceresini ve siyah köprü penceresini kapat.
