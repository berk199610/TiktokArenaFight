# DESIGN — Arena Dövüşü (TikTok LIVE, Unity 6)

> Bu dosya `docs/Arena-Dovusu-Tasarim.pdf` dokümanının düz metin çıkarımıdır (tablolar satır satır). Kurallar ve sayılar için asıl kaynak PDF'tir. Uygulamadaki kararlar ve yorumlar: `docs/TASARIM-NOTLARI.md`.

Arena Dövüşü — TikTok LIVE Oyun Sistemi
(Unity 6)
Nov 7, 2026  · ​@Berk
1. Oyun kısaca
Basit bir arena dövüşü (survivor): TikTok izleyicileri yorum, beğeni ya da hediyeyle arenaya
karakter olarak girer; karakterler kendi akıllarıyla dövüşür, son kalan turu kazanır. İzleyici
karakterini doğrudan yönetmez, sadece hediye, beğeni ve yorumla etkiler.
Arena: Basit yuvarlak bir alan; sağda ve solda birer kapı. Kamera 2.5D: eğik yukarıdan,
ne çok uzak ne çok yakın.
Can: Herkes 1000 canla başlar. Arenada en fazla 100 kişi olur.
Yorum veya beğeni: Arenaya girer ve seni sahnede tutar.
Hediye: Hemen girersin, daha uzun kalırsın, daha fazla hediye geldikçe özel saldırı
yaparsın hediye ile canın dolar.
Etkileşim yoksa: Karakter önce uyuşur (salaklaşır), sonra canı daha hızlı gider hızlıca
ölür.
Ölüm: Ölen 30 saniye dışarıda bekler; bu sürede sadece hediye geri getirir.
Boş arena: Başta 3–5 yapay zekâ karakter dövüşür; ekran asla boş kalmaz.
Kazanan: Son kalan kazanır ve skor tablosuna yazılır belki bir sıralama yaparız yeni tur
yerine skorboard gibi kaç kişiyi öldürdü şuana kadar gibi skorboard yanda gözükür
küçük şekilde ya da oyun içinde olur tabella gibi oyun içinde olursa ve güzel gözükürse
mesela arennın kameranın tam karşısında bir skorboard ekrandakinin okuyacağı kadar
büyük ama çok büüyk değil orası dikkat çeker ilk 5-10 kişiyi yazar sıralama değişir kaç
kişiyi öldürdün. Boss yani çok fazla hediye atan kişiyi öldürürse daha büyük puan
kazanır o kişi çok hediye atan kişide ekstra oyunda belli edecek şeyler eklemeliyiz.
Altın kural: Hediye atan her zaman önde. Önce girer, yer açmak için atılmaz, en güçlü
saldırıları yapar ve bunu ekranda anında hisseder: karakteri güç alır gibi kısa bir poz verir,
adı altın renge döner lideri belli etmeliyiz belki saldırıda biraz daha önde olur ama hediye
atması lazım tek bir kere attı bitti değil süre olmalı gittikçe zayıflamalı hediye yorum
gelmezse.
İkinci altın kural: Basitlik. Arena sade, ekran yazısı az, kamera sakin. Emek karakterlere
gider: zekâları, animasyonları ve hediyelere tepkileri.


---

2. Oyuncu yaşam döngüsü
Bir izleyicinin oyundaki yolculuğu yedi durumdan oluşur. Hediye her aşamada öncelik verir:
sırayı atlatır, ölüyü diriltir, atılmaktan korur.
Hediye atan, arena doluysa bile beklemeden girer; yeri en pasif hediyesiz oyuncu boşaltır.
Hediye koruması (5 dakika) sürerken karakter uyuşsa bile atılmaz. Ayrıntılı sayılar bölüm
3'te.. botlar sayı gerçek oyuncu doldukça oyunda ölmeye başlarlar daha fazla kişi girsin
diye eğer kişi sayısı 10 altına düşerse oyuna botlar girer belki 5-7 bot yeterli olur oyundaki
toplam sayısı.
3. Kurallar ve sayılar
Tüm sayılar ayar dosyasındadır (bölüm 10) ve yayınlardan sonra kolayca değiştirilir.
Giriş ve kalma
oyuncu yaşam döngüsü · girişten kazanana


---

Can
Ölüm ve geri dönüş
1. Canı biten karakter düşer, 2 saniye yerde kalır, duman bulutuyla kaybolur.
Kural
Değer
Kapasite
En fazla 100 karakter (botlar dahil)
Yorum veya beğeniyle giriş
Turun ilk 90 saniyesinde açık. Sonra gelenler sıraya
girer, bir sonraki turda başlar
Hediyeyle giriş
Her an, anında. Arena doluysa yer açılır (aşağıda)
Aktiflik kontrolü
Her 60 saniyede bir. Son 120 saniyede hiç yorum,
beğeni veya hediye yoksa karakter daha hızlı ölür
can kaybeder darbelerde savunması azalır.
zayıfladığını belli etmeliyiz birşeyler yapmalı.
Hediye koruması
Son hediyeden sonra 5 dakika aktiflik kontrolünden
muaf; arena dolunca da atılmaz
Salaklaşma
Son etkileşimden 45 sn sonra zekâ düşmeye başlar,
90 sn'de en dipte. Tek bir yorum, beğeni veya
hediye zekâyı anında tama çıkarır
Kural
Değer
Başlangıç canı
1000
Beğeniyle can
Her beğeni +1; karakterin üstünde küçük "+1" uçar.
Kişi başı saniyede en fazla 5 beğeni sayılır
Hediyeyle can
coin × 10 (100 coin ve üstü = tam can)
Yorumla can
Yorum +5 can verirsahnede tutar ve zekâyı tazeler
Düşük can (300 altı)
Yürüme hızı %70, saldırı hızı %75; başının üstünde
kırmızı, nabız gibi atın bir "+" işareti: "can lazım gibi
sağlık işareti çıkar.
Çok düşük can (150 altı)
Sendeleyerek yürür, kolay devrilir can işareti hafif
shake olur.


---

2. 40 saniye ceza. Bu sürede sadece hediye geri getirir: karakter kapıdan yeniden doğar
(canı hediyeye göre dolar, en az 500).
3. Ceza bitince bakılır: son 40 saniyede yorum veya beğeni yaptıysa hemen geri girer.
Yapmadıysa bir sonraki yorum veya beğenisinde girer (tur girişe açıksa; değilse
sıradaki turda).
Arena doluyken
Hediye atan biri girmek isterse: Arenadaki, hediye atmamış oyuncular arasından son
2 dakikada en az yorum ve beğeni yapan çıkarılır. Önce botlar çıkar. Çıkarılan karakter
komik bir şekilde arenanın dışına fırlatılır. Hediye atanlar asla bu yüzden çıkarılmaz.
Yorum veya beğeniyle girmek isteyen: Sıraya girer. Yer açılınca sıradakiler arasından
en aktif olan girer.
Botlar: Gerçek oyuncu sayısı 20'yi geçince yeni bot gelmez. Botların sahte TikTok tarzı
adları vardır (kral_ali_34, melis.xx, dj_tayfun) ve skor tablosuna yazılabilirler.
4. Hediyeler
Hediyenin etkisi coin değerine göre belirlenir; hangi TikTok hediyesinin hangi kademeye
düşeceği ayar dosyasında tutulur. Büyük hediye, büyük an demektir.
Coin
Hareket
Ne olur
Süre
1–4 (Gül
vb.)
Güçlü Yumruk
Bir sonraki yumruğu 3 kat hasar (60) verir ve
rakibi geri iter
Tek
vuruş
5–29
Sopa
Eline sopa geçer: menzil %50 artar, hasar 2 kat
15 sn
30–99
Kürek
Geniş savurma: önündeki yarım dairedeki
herkese 60 hasar
20 sn
100–299
Döner Tekme
Bir ayağını kaldırır ve kendi etrafında döner; 2
metre içindeki herkese her dönüşte 50 hasar
ve itme, 3 tur
3 sn
300–999
Yumruk Dalgası
Yere vurur, halka şeklinde şok dalgası çıkar; 4
metre içindeki herkese 150 hasar, hepsi yere
düşer
Anında
1.000–
4.999
Dev Mod
1,4 kat büyür, hasarı 2 kat, itilemez
20 sn


---

Her hediyede ayrıca:
Can dolar: coin × 10 (100 coin ve üstü tam can).
Koruma yenilenir: 10 dakika aktiflik kontrolünden muaf, arena dolunca atılmaz.
"Ben öne geçtim" anı: Hareketten önce 0,5 saniyelik güç alma pozu: karakter bir an
durur, üstüne ışık sütunu iner, parlar; sonra hareketi yapar. İzleyici kendi hediyesini
kesin fark eder.
Adı altın olur. Turda en çok hediye atanın başında taç belirir.
Kurallar
Aynı karaktere seri hâlinde gelen aynı hediye süreyi uzatır (sopa 15 + 15 sn...), en fazla
60 saniye.
Bir karakter aynı anda tek özel hareket yapar; yenisi bekler (en fazla 3 sırada).
Hediye atan ölüyse önce kapıdan diriltilir, sonra hareketi yapar. Arenada değilse
hemen girer.
Hasar, menzil ve sürelerin hepsi ayar dosyasındadır; ilk yayınlardan sonra dengelenir.
5. Karakter zekâsı
Her karakterin kendi küçük beyni vardır: çevresine bakar, seçenekleri puanlar, en iyisini
yapar. Herkes aynı anda aynı şeyi yapmasın diye her beyin farklı zamanda düşünür ve farklı
bir kişiliğe sahiptir. Bu yönteme oyun dünyasında "utility AI" (fayda puanıyla karar verme)
denir.
5.1 Algı (her 0,2–0,4 saniyede bir, her karakter kendi zamanında)
8 metre içindeki rakipler: mesafeleri, canları, şu an saldırı hazırlığında olup olmadıkları.
Son 10 saniyede kendisine kim vurdu (intikam hafızası).
Tehlikeli alanlar: dönen tornado, döner tekme, şok dalgası.
Kendi canı, arena kenarına uzaklığı, kameranın görüş alanında olup olmadığı.
5.2 Karar: seçenekler ve puanlar
Coin
Hareket
Ne olur
Süre
5.000+
Efsanevi
Tornado
Hortum gibi dönerek arenada dolaşır; değdiği
herkese her 0,5 saniyede 40 hasar verir ve
onları savurur
10 sn


---

Karakter en yüksek puanlı seçeneği yapar; ilk ikisi çok yakınsa aralarından rastgele seçer.
Bu küçük rastgelelik, robot gibi tekdüze hareketi engeller.
5.3 Herkes ortada ama tek yığın değil
Kalabalık kuralı: Bir hedefe aynı anda en fazla 3 kişi saldırır. Fazlası başka hedef seçer.
Dövüşler küçük gruplara dağılır, ortada tek bir yığın oluşmaz.
Kamera kuralı: Hedef seçerken kameranın gördüğü karakterler tercih edilir.
Kameradan çıkan karakter ortaya doğru yürür.
Ayrışma: Karakterler birbirinin içine girmez; aralarında hep biraz boşluk kalır.
Farklılık: Her karakterin hızı ±%10, tepki süresi, yumruk/tekme tercihi ve animasyon
hızı biraz farklıdır.
5.4 Kişilikler
Her kostümün bir kişiliği vardır; bir izleyici her zaman aynı kostümü aldığı için karakteri her
yayında aynı huyda olur.
Seçenek
Puanı ne artırır
Saldır (bir hedefe)
Hedef yakın, hedefin canı düşük, hedef kameranın
gördüğü yerde, kişilik saldırgan
İntikam al
Son 10 saniyede bana vuran yakında
Etrafında dön (yan adım)
Rakip orta mesafede, açık bekliyorum; kişilik
temkinli
Kaç / geri çekil
Canım düşük ve yakında rakip çok; ya da yakında
tornado/döner tekme var
Ortaya dön
Arena kenarındayım veya kamera beni görmüyor
Alay et
Az önce birini devirdim; kişilik şovmen
Nefeslen
Kısa bir ara; herkesin sürekli hareket etmesini önler
Kişilik
Davranış
Saldırgan
En yakına dalar, nadiren kaçar
Fırsatçı
Canı düşük olanları avlar


---

5.5 Zekâ seviyesi ve salaklaşma
Zekâ, izleyicinin son etkileşiminden bu yana geçen süreye bağlıdır (botlar sabit %70).
Uyanma: Uyuşmuş karaktere yorum, beğeni veya hediye gelince başında "!" çıkar, kısa bir
silkinme animasyonu yapar ve zekâsı anında %100 olur. İzleyici kendi karakterinin
uyandığını görür; bu, yorum yazmak için en güzel sebeptir.
6. Dövüş, animasyon ve tepkiler
Dövüş basittir: yumruk ve tekme. Kaliteyi animasyonlar ve tepkiler yaratır. Her vuruşun bir
hissi olmalı: vuran ileri atılır, yiyen geri sarsılır.
6.1 Temel saldırılar
Kişilik
Davranış
Temkinli
Kalabalıktan uzak durur, kenardan vurur, canı
düşünce kaçar
Kinci
Kendisine vuranı unutmaz, peşine düşer
Şovmen
Rakibi devirince alay eder, dans eder
Zekâ
Ne zaman
Davranış
%100
Son 45 sn'de etkileşim var
Hızlı tepki (0,15 sn), rakibin saldırı hazırlığını görüp
yana kaçabilir (%30), iyi hedef seçer
%50
45–70 sn
Yavaş tepki (0,5 sn), nadiren kaçar, bazen yanlış
hedef seçer
%10
70 sn ve üstü
Çok yavaş (1 sn), hiç kaçmaz; sık sık sersem
bekleme animasyonuyla durur, başının üstünde
"zzz" belirir, ters yöne yürür
Saldırı
Hasar
Menzil
Bekleme
Not
Yumruk
20
1,2 m
0,8 sn
Hızlı; 2–3 farklı yumruk animasyonu sırayla kullanılır
Tekme
35
1,6 m
1,4 sn
Hazırlığı yavaş; akıllı rakip görüp yana kaçabilir


---

Hasar animasyonun temas anında verilir (Animation Event ile), animasyon başlar
başlamaz değil. Vuruş alanı karakterin önünde küçük bir yay şeklindedir.
6.2 Tepkiler
Vuruş hissi (hit stop): Güçlü vuruşlarda vuran ve vurulan karakterin animasyonu 0,05
saniye donar. Dövüş oyunlarındaki "tok" vuruş hissi buradan gelir. Kamera sallanmaz.
6.3 Animasyon listesi (Mixamo'da arama kelimeleri)
Durum
Tepki
Hafif darbe (yumruk)
Kafa veya gövde geri sarsılır; yürümeye devam eder
Güçlü darbe (tekme, hediye
saldırıları)
Geri savrulur ve sendeler
Şok dalgası, tornado
Yere düşer, 1 saniye sonra kalkar
Düşük can
Yaralı yürüyüş, yavaş vuruş
Ölüm
Düşer, başında yıldızlar döner, duman bulutuyla
kaybolur
Kazanma
Zafer dansı, taç, konfeti
Durum
Mixamo'da ara
Bekleme
Fighting Idle, Boxing Idle
Koşma, yürüme
Run, Jog, Injured Walk (düşük can)
Yumruk
Punching, Cross Punch, Jab
Tekme
Kick, MMA Kick
Döner tekme
Spin Kick; bulunamazsa itch.io'da Mixamo
iskeletine uyumlu 1 dolarlık bir Tornado Kick
animasyonu var
Sopa ve kürek
Baseball Swing, Sword Slash
Yer vuruşu (şok dalgası)
Ground Punch, Jump Attack
Darbe alma
Hit Reaction, Head Hit


---

Tam ad farklıysa en yakın animasyon seçilir. Tornado, dönme animasyonunun döngüye
alınması ve karakterin kodla döndürülmesiyle yapılır.
6.4 Efektler (AI modelin shader ve parçacık olarak yazacağı)
Kapıdan giriş: Gökten kapının önüne şimşek iner, yerde parlayan bir halka ve toz;
karakter içinden koşarak çıkar.
Vuruş: Küçük yıldız patlaması. Sopa ve kürekte savurma izi.
Döner tekme: Ayağın etrafında rüzgâr halkası.
Şok dalgası: Yerde genişleyen parlak halka.
Tornado: Karakterin etrafında dönen, kayan desenli bir koni ve uçuşan toz.
Dev mod: Büyüme ve renkli parıltı.
Güç alma: Üstten inen ışık sütunu.
İyileşme: Beğenide küçük yeşil "+1"; hediyede karakterin etrafında yeşil halka.
Ölüm: Çizgi film tarzı duman bulutu.
7. Karakterler, kostümler ve kalıcı kayıtlar
7.1 Tek gövde, çok kostüm
Tüm karakterler aynı insan iskeletini kullanır; böylece her Mixamo animasyonu hepsinde
çalışır. Farklılık kostüm, aksesuar ve renkle yaratılır.
Gövde önerisi: KayKit Character Pack: Adventurers. Tombul, sevimli, cinsiyeti belirgin
olmayan stil; riglenmiş ve animasyonlu, 25'ten fazla aksesuar ve silahla geliyor, CC0
lisanslı (Cinevva rehberi). Unity'de "Humanoid" olarak ayarlanınca Mixamo
animasyonları üzerine aktarılır (retarget).
Durum
Mixamo'da ara
Yere düşme ve kalkma
Knocked Down, Getting Up
Ölüm
Dying, Death
Sersem (salaklaşma)
Dizzy Idle
Güç alma
Flexing, Power Up
Alay
Taunt, Silly Dancing, Chicken Dance
Zafer
Victory, Cheering


---

Aksesuarlar: Bıyık, pompon, şapka, pelerin gibi parçalar ayrı küçük modellerdir ve
kafa, sırt veya el kemiğine takılır. Basit olanları AI model Unity'de basit şekillerden de
yapabilir.
7.2 Kostüm listesi (ilk 12)
Kostüm sayısı zamanla 20'ye çıkar. Hepsi süslü ve komik olur, kimse kadın ya da erkek diye
ayrılmaz.
7.3 Kostüm dağıtımı ve hediye ayrıcalığı
Kostüm, izleyicinin TikTok kimliğinden hesaplanır: aynı kişi her yayında aynı kostümü
alır ve kendini tanır.
Hediye atanın adı altın renk olur ve karakterinin etrafında hafif bir altın parlama
bulunur.
Toplamda 1.000 coin hediye gönderen kalıcı olarak kostümünün altın sürümünü açar
(aksesuarları altın olur). "Hediye atan önde" duygusu her yayında devam eder.
7.4 İsim ve can çubuğu
Kostüm
Görünüm
Kişilik
Bıyıklı Pehlivan
Kocaman bıyık, altın kemer
Saldırgan
Pembe Pompon
Pembe pomponlu bere, fırfırlı etek
Şovmen
Viking
Boynuzlu kask, kürk pelerin
Saldırgan
Profesör
Yuvarlak gözlük, papyon
Temkinli
Altın Kral
Taç, kırmızı pelerin
Şovmen
Ninja
Alın bandı, siyah kıyafet
Fırsatçı
Aşçı
Uzun aşçı şapkası, önlük
Kinci
Disko
Afro peruk, parlak ceket
Şovmen
Astronot
Yuvarlak kask
Temkinli
Korsan
Göz bandı, korsan şapkası
Fırsatçı
Çiçek Taçlı
Çiçekten taç, renkli yelek
Kinci
Robot
Metal kafa, anten
Saldırgan


---

Her karakterin başının üstünde küçük bir can çubuğu (yeşilden kırmızıya) ve adı vardır.
100 isim ekranı dolduracağı için: hediye atanların adı hep görünür; diğerlerinin adı
etkileşim yaptıklarında veya vuruş aldıklarında 3 saniye görünür. Arenada 20 kişi veya
daha az kalınca herkesin adı hep görünür.
7.5 Kalıcı kayıtlar ("kişileri hafızada tutuyoruz")
Her izleyici için bir kayıt tutulur ve yayınlar arasında saklanır:
Kayıtlar bilgisayarda bir JSON dosyasında tutulur. Yayın sırasındaki anlık bilgiler (son
etkileşim zamanı, zekâ, koruma süresi) sadece hafızada durur.
8. Arena, kamera ve ekran
8.1 Arena (basit)
Yuvarlak bir zemin, yarıçapı 12 metre; etrafında alçak bir duvar. Savrulan karakter
duvara çarpıp geri seker, dışarı düşmez.
Solda ve sağda birer kapı: basit bir kemer ve iki meşale.
Zemin kum veya taş dokusu, arka plan karanlık, ışık sıcak ve arenaya odaklı. Görünüm
low-poly ve renkli; gerçekçi değil.
Performans için karakterlerin altında basit yuvarlak gölgeler kullanılır.
8.2 Kamera (2.5D, sakin)
Yukarıdan yaklaşık 50° eğik bakar; ne çok uzak ne çok yakın. Karakterlerin yüzleri,
aksesuarları ve can çubukları telefonda seçilebilmeli.
Kamera arenanın tamamını değil, dövüşün yoğun olduğu bölgeyi gösterir. Hedefi
karakterlerin ağırlıklı ortasıdır; arena merkezinden en fazla 5 metre uzaklaşır.
Alan
Açıklama
userId, nickname, avatarUrl
TikTok kimliği, adı, profil fotoğrafı
costumeId, goldUnlocked
Kostümü ve altın sürümü açıp açmadığı
totalCoins, totalLikes, totalComments
Toplam hediye, beğeni, yorum
wins, kills, deaths, points
Kazanma, devirme, ölüm, puan
firstSeen, lastSeen
İlk ve son görüldüğü tarih


---

Hareketi yumuşak ve yavaştır (1,5 saniyede yetişir). Karakterler dağılınca biraz geri
çekilir, toplanınca biraz yaklaşır.
100 coin ve üstü bir hediye hareketi sırasında kamera hafifçe o karaktere doğru kayar;
kesme ve sarsıntı yoktur.
Kapılar görüş alanının kenarındadır: şimşek görünür, karakter ortaya koşarak kadraja
girer.
8.3 Ekran (dikey 1080×1920)
TikTok'un kendi arayüzü ekranın üstünde yaklaşık %9'u, sol altta yorum alanını kaplar. Bu
alanlara önemli bir şey konmaz.
9. Tur akışı ve skor tablosu
Bir tur yaklaşık 4–5 dakika sürer. Tur bitmeyi garanti etmek için sonda arena daralır.
1. Hazırlık (10 sn): Son 2 dakikada etkileşim yapmış herkes ve botlar kapılardan sırayla
girer (saniyede 5 kişi, sol ve sağ kapı dönüşümlü). Her giriş şimşekle olur, karakter
ortaya bir rakibe doğru koşar. Önce hediye atanlar girer.
2. Savaş: İlk 90 saniye yorum ve beğeniyle giriş açıktır. Hediyeyle giriş ve diriltme her
zaman açıktır.
3. Son savaş: 3. dakikadan sonra veya arenada 10 kişi kalınca başlar. Giriş ve diriltme
kapanır. Arena kenarından içe doğru ateş halkası 60 saniyede daralır (yarıçap 12
metreden 4 metreye); halkanın dışında saniyede 30 hasar. Hediyeler can vermeye ve
hareket yaptırmaya devam eder.
Yer
Ne gösterir
Üst şerit
"Kalan: 37", tur süresi, "Giriş açık" veya "Son savaş"
Hediye rehberi
Yedi kademenin resmi ve tek kelime: Yumruk · Sopa
· Kürek · Döner Tekme · Dalga · Dev · Tornado.
Yanında: "Yorum veya beğeni = katıl"
Sağ kenar
Skor tablosunun ilk 5'i: ad, kazanma, puan
Olay satırları
En fazla 3 satır, 4 saniyede silinir; sadece hediye
hareketleri ve hediye atanların devirmeleri:
"Ahmet34 TORNADO!"


---

4. Kazanan (8 sn): Son kalan zafer dansı yapar, başında taç, konfeti. Ekranda "KAZANAN:
Ahmet34" ve puanı. Bot kazanırsa "Bot kazandı" yazar, kayıt tutulmaz.
5. Yeni tur başlar.
Son savaşta ölü birinin hediyesi kaybolmaz: Bir sonraki turun başında karakteri ilk sırada
girer ve hediye hareketini yapar.
Puanlar ve skor tablosu
Skor tablosu puana göre sıralanır; ekranda ilk 5, tam liste yayıncı panelinde.
Sıfırlama: Yayıncı panelinde iki düğme vardır, ikisi de onay ister: "Skor tablosunu sıfırla"
(sadece puan, kazanma ve devirme sayıları silinir; kostümler kalır) ve "Tüm kayıtları sil".
Oyun penceresinde ayrıca klavye kısayolu: Ctrl+Shift+R, 3 saniye basılı tutunca skor
tablosunu sıfırlar.
10. Unity yapısı ve TikTok bağlantısı
10.1 Proje
Unity 6.3 (bilgisayardaki 6000.3 sürümü), URP 3D şablonu.
Paketler: Input System, TextMeshPro, Shader Graph, Newtonsoft Json, bir WebSocket
istemcisi (örneğin NativeWebSocket). İsteğe bağlı: Cinemachine, VFX Graph.
Oyun 1080×1920 dikey pencerede çalışır; TikTok LIVE Studio bu pencereyi yakalayıp
yayına verir.
10.2 TikTok bağlantısı
Unity TikTok'a doğrudan bağlanmaz. Yanında küçük bir Node.js "köprü" programı çalışır:
1. Köprü, tiktok-live-connector  paketiyle canlı yayına bağlanır.
2. Her olayı tek bir şemaya çevirir: { type: gift|like|comment|follow|share|join,
userId, nickname, avatarUrl, coins, giftName, count, text, ts } .
3. Olayları WebSocket ile Unity'ye gönderir (örneğin ws://localhost:8787 ).
Olay
Puan
Turu kazanmak
100
Birini devirmek (son vuruşu yapmak)
10
Son 10'a kalmak
20


---

4. Köprüde ayrıca bir simülatör sayfası vardır: düğmelerle sahte yorum, beğeni ve hediye
gönderir; "kalabalık botu" seçilen sayıda sahte izleyiciyle gerçekçi trafik üretir. Oyun
TikTok olmadan buradan test edilir.
5. Köprüdeki yayıncı paneli Unity'ye komut yollar: duraklat, turu bitir, skor tablosunu
sıfırla, tüm kayıtları sil, bir kullanıcıyı oyundan çıkar.
Hediye serileri dikkat ister: TikTok'ta gül gibi hediyeler seri hâlinde gelir; her hediye bir kez
sayılmalı, iki kez değil.
10.3 Kod yapısı (Assets/_Game/Scripts)
Klasör
Sınıflar
Görevi
Core
GameManager, GameConfig,
EventRouter
Tur durumları (Hazırlık, Savaş, Son
savaş, Kazanan); ayarları okur; gelen
olayları doğru sisteme dağıtır
Viewers
ViewerRegistry, RosterManager,
ActivityMonitor,
PersistenceService
İzleyicilerin kaydı,
giriş/sıra/atılma/diriltme kuralları, 60
saniyelik kontrol ve zekâ değeri,
JSON kayıt
Fighters
Fighter, FighterBrain,
FighterMotor, FighterCombat,
FighterAnimation, Personality
Can ve durum, karar verme, hareket
ve itilme, saldırı ve vuruş alanı,
animasyon bağlantısı, kişilik ayarları
SpecialMoves
PowerPunch, WeaponBuff,
SpinKick, Shockwave, GiantMode,
Tornado
Her hediye hareketi kendi sınıfında;
hepsi aynı arayüzü kullanır, yenisi
kolay eklenir
Gifts
GiftCatalog, GiftResolver
TikTok hediye adı → coin → kademe →
hareket
World
Arena, SpawnGate, FireRing,
SpatialGrid
Arena sınırları, kapıdan şimşekli giriş,
daralan halka, yakındaki karakterleri
hızlı bulma
CameraRig
ArenaCamera
Sakin, ağırlıklı orta takibi
UI
Hud, Nameplates, EventFeed,
LeaderboardPanel, WinnerScreen
Üst şerit, isim ve can çubukları, olay
satırları, skor tablosu, kazanan ekranı
Net
BridgeClient
WebSocket bağlantısı, kopunca
yeniden bağlanma


---

10.4 Performans kuralları (100 karakter, saniyede 60 kare)
Beyinler her karede değil, 0,2–0,4 saniyede bir ve farklı zamanlarda düşünür.
Yakındaki karakterler bir ızgara (SpatialGrid) ile bulunur; herkes herkesi taramaz.
Hareket basit yönelme ve ayrışma kurallarıyla yapılır; düz arenada NavMesh gerekmez.
Karakterler, isim etiketleri ve efektler havuzdan (pool) yeniden kullanılır; sürekli
yaratılıp silinmez.
11. Yapım sırası ve AI modeline verilecek talimat
Altın kural: Önce zekâ, sonra güzellik. Kapsül şeklindeki karakterler akıllı ve eğlenceli
dövüşmüyorsa, güzel modeller bunu düzeltmez. Her aşama ayrı bir istekle yaptırılır ve
bitince Unity'de oynanarak kontrol edilir.
Aşama 1: Sistem ve zekâ (kapsül karakterler)
Bitti sayılması için: 100 karakter saniyede 60 kare; dövüşler küçük gruplara dağılıyor;
karakterler kamerada kalıyor; uyuşan karakter gözle fark ediliyor ve yorumla uyanıyor.
Aşama 2: Animasyon ve kostümler
Aşama 3: Hediye hareketleri ve efektler
Klasör
Sınıflar
Görevi
Dev
DevKeys
Editörde klavyeyle sahte olay: yorum,
beğeni, her kademeden hediye
Arena, iki kapı, sakin kamera
Fighter, FighterBrain, FighterMotor, FighterCombat (yumruk ve tekme, kodla basit
hareket)
Giriş, sıra, atılma, ölüm, diriltme ve salaklaşma kuralları
DevKeys ve köprüdeki simülatör, isim ve can çubukları
KayKit gövdesi Humanoid olarak, Mixamo animasyonları üzerinde
Animator: yürüme/koşma karışımı, saldırılar, darbe tepkisi katmanı, yere düşme, ölüm,
zafer
Vuruş anı için Animation Event, vuruş donması (hit stop)
Aksesuar sistemi ve ilk 4 kostüm
Yedi hediye hareketi ve güç alma pozu


---

Aşama 4: Tur, skor ve kayıtlar
Aşama 5: TikTok bağlantısı ve deneme
AI modeline verilecek talimat
Bu dokümanı Markdown olarak indirip proje klasörüne DESIGN.md  adıyla koy ve kodlama
yapan AI modeline şunu ver. Sonraki aşamalarda sadece aşama adını değiştir.
Sen deneyimli bir Unity 6 ve C# oyun geliştiricisisin. Bu klasördeki 
DESIGN.md, TikTok LIVE için yapılan "Arena Dövüşü" oyununun sistem 
dokümanıdır. Tamamını dikkatle oku. Dokümandaki kurallar ve sayılar 
önceliklidir; değiştirmen gerekirse önce bana sor. Dokümanda olmayan özellik, 
ekran yazısı veya kamera hareketi ekleme.
Proje: Unity 6.3, URP. Şimdi SADECE "Aşama 1: Sistem ve zekâ" bölümünü yap.
Kurallar:
- Kod yapısı bölüm 10.3'teki klasör ve sınıflara uysun. Her hediye hareketi 
kendi sınıfında olsun.
- Tüm sayılar StreamingAssets/config.json'dan okunsun; kodda sihirli sayı 
olmasın.
- Karakter beyni bölüm 5'teki gibi fayda puanıyla (utility AI) karar versin; 
beyinler 0,2–0,4 saniyede bir ve farklı zamanlarda düşünsün.
- 100 karakterde saniyede 60 kare hedefle: SpatialGrid, havuzlama (pool), her 
karede bellek ayırma yok.
- Bu aşamada TikTok bağlantısı yok. Editörde DevKeys ile (örneğin C = yorum, 
L = beğeni, 1–7 = hediye kademeleri) test edilebilsin.
- Unity editöründe elle yapılması gereken her adımı (sahne kurulumu, paket 
ekleme) README'de sırayla yaz.
Teslim: çalışan kod, README, sonunda kısa bir rapor (ne yaptın, ne eksik 
kaldı, dokümanda belirsiz bulduğun yerler). Küçük adımlarla ilerle.
Şimşekli giriş, tornado, şok dalgası, döner tekme rüzgârı, iyileşme efektleri (Shader
Graph ve parçacıklar)
Tur durumları, ateş halkası, kazanan ekranı
Kalıcı kayıtlar, skor tablosu, sıfırlama düğmeleri
Köprü, yayıncı paneli, hediye serilerinin doğru sayılması
Az izleyicili deneme yayınları, sayıların ayarlanması, 12 kostümün tamamlanması


---

Animasyonları ve efektleri (Aşama 2 ve 3) AI modeliyle birlikte, her seferinde tek bir
hareketi deneyip beğenerek ilerletmek en iyisidir: önce döner tekme, sonra tornado gibi.
önce modelleri karakterleri bulalım karakterlerin dev hali de gerekiyor hafif büyük hali
hedeiye geldiğinde cılızlaştığında belli edecek belki eriyecek ilk giridğinde hafif kaslı ama
zaman geçtikçe hediye yorum yapmadıkça cılızlıyor gibi oraları sen kur modelleri araştır
ücretlide olabilir bana söyle ben bakayım dosyalara ekleyeyim oyuncu karakterlini basit
tutmalıyız çok basit üstündeki eşyaları değiştirebiliriz belki. hepsi birbirine benziyor ama
üstündekiler değişik gibi bakalım bize modeller arena tarzı modeller bul öner birkaç tane
ücretsiz veya ücretli bana öner ben bakayım ekleyeyim.


---

