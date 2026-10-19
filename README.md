# Blind Attack

2 kişilik, online, izometrik bir "kör savunma" oyunu. Her oyuncunun kendi haritası var. Kendi haritana düşman ve gizli tuzak koyarsın, rakibin haritasına da kendi kahramanın için destek kutuları bırakırsın. Ama rakibin düşmanlarını **göremezsin**. Sonra iki saldırıyı sırayla izlersiniz.

## Nasıl oynanır

İki oyuncu da maç boyunca **aynı haritada** oynar. Her yeni maç (rövanş dahil) bir öncekinden **farklı haritada** başlar. Bir maç 3 tur sürer. Her turda:

1. **Savunma (50 sn):** Haritaya Goblin, Okçu, Dev ve gizli Diken Tuzağı koyarsın (36 altın). Çıkışa 6 adımdan yakın yere (kırmızı alan) düşman konamaz ve bir bölgeye (5x5) en fazla 3 birim konur. Fareyle gezdirirken düşmanın fark etme menzili halka olarak, bölge sınırı da sarı (dolunca kırmızı) kare olarak görünür.
2. **Saldırı hazırlığı (45 sn):** Aynı haritayı görürsün ama rakibin düşmanları **gizlidir**.
   - **Kahraman:** Mavi başlangıç bölgesinde istediğin yere koy. Oradan yola çıkar.
   - **Kutular (14 altın, aynı kutudan en fazla 2):** Kılıç, Zırh, İksir, Çizme, Bomba.
   - **Bayrak (ücretsiz, en fazla 3):** Kahramanın uğrayacağı rota noktası.
   - Kahramanın gideceği rota haritada **noktalı çizgi ve numaralarla** gösterilir: başlangıçtan en yakın hedefe, sonra kalanların en yakınına... en son çıkışa. Kutuyu ya da bayrağı nereye koyarsan rota oradan geçer.
3. **Saldırı:** Önce 1. oyuncunun, sonra 2. oyuncunun saldırısını birlikte izlersiniz. Kahraman rotada yürür, yolda gördüğü düşmanlarla savaşır.

**Kutular kahramanı görünür şekilde değiştirir:**

| Kutu | Etki | Görünüm |
|---|---|---|
| Kılıç | +3 saldırı | Kılıç büyür, turuncu sonra kırmızı-beyaz parlar ve alev saçar |
| Zırh | Alınan hasar %15 azalır | Göğüs zırhı, omuzluk, miğfer. 2. zırhta altın renk ve kalkan. Darbe alınca mavi kıvılcım |
| Çizme | %20 hızlı yürür ve vurur | Kanatlı altın çizmeler, yürürken toz izi |
| İksir | +20 can | Yeşil iyileşme parıltısı |
| Bomba | Alınca patlar, etraftaki düşmanlara 60 hasar | Patlama ve sarsıntı |

İzleme sırasında alttaki çubukta kahramanın güncel **Saldırı / Zırh / Hız** değerleri görünür.

**Denge kuralları:** Kahramanın canı 125, saldırısı 13. Aynı kutudan en fazla 2 konur (tek tip yığmak yok). Kahramana aynı anda en fazla 2 düşman vurabilir. Kahramanın kılıcı ana hedefin yanındaki düşmanlara da çeyrek hasar verir. Dev: 95 can, 16 hasar, 7 altın. Tek noktaya yığmak yerine savunmayı dar geçitlere yaymak gerekir.

**Puan:** Çıkışa ulaşırsan 100 + kalan can. Ulaşamazsan çıkışa ne kadar yaklaştığına göre en fazla 60. Turu daha yüksek puan alan kazanır.

## Görsel ve atmosfer

- **Her haritanın kendi saati var:** Nehir Vadisi sabah sisi, Kale Bahçesi gün batımı, Taş Ocağı ay ışığında gece (fenerler yanar, ateş böcekleri uçar). Rakip bölgede aynı saat kızıl bir tona bürünür.
- **Canlı çevre:** Kuş sürüleri, rüzgârda sallanan ağaçlar ve çalılar, akan su, dönen değirmen, bacalardan yükselen duman, düşen yapraklar.
- **Saldırı açılışı:** Sinema şeritleri kayar, tur başlığı belirir. Kamera çıkıştan haritanın üstüne, oradan kahramana süzülürken gizli savunma sırayla **yerden fışkırır**.
- **Karakter kişiliği:** Beklerken goblin kafasını kaşır, dev esner, okçu yayını yoklar. Kahramanı fark eden düşmanın başında "!" çıkar. Kahraman kutu alınca, tuzağa basınca, dev görünce, canı azalınca konuşur.
- **Ölüm ve zafer anı:** Kahraman düşünce ağır çekim, kamera yaklaşır, dünya griye döner. Çıkışa ulaşınca kısa ağır çekim, ekran parlar, kahraman dönerek portala çekilir.
- **Tur sonu ekranı:** İki saldırı yan yana. Puanlar sayaç gibi dolar. Öldürülen düşman, verilen ve alınan hasar, toplanan kutu, tuzak, süre, en tehlikeli savunucu.
- **Başarımlar:** Menüdeki "Başarımlar" düğmesinden görülür (bu bilgisayarda saklanır). Örnekler: Kusursuz (hasar almadan geç), Kasap (8 düşman), Bombacı, Aşılmaz Duvar, Şampiyon, Ezici Üstünlük (3-0).
- **Ana menü:** Arka planda kamera haritanın etrafında döner, iki bot arasında küçük bir çatışma oynar.
- **Ortam sesleri:** Sabah kuş cıvıltısı, gün batımında esinti ve uzaktan kuş, gece cırcır böcekleri ve baykuş (Ayarlar'daki "Efektler" ile ayarlanır).
- **İlk oyun rehberi:** İlk savunma, ilk hazırlık ve ilk izlemede kısa ipucu kutuları çıkar. "Anladım" ya da "Rehberi kapat" ile kapanır, Ayarlar'dan tekrar açılabilir.
- **Açılış logosu:** Oyun açılınca kısa bir giriş (tıklayınca geçilir).

**Kontroller:** Sol tık koyar, sağ tık kaldırır, sayı tuşları seçim yapar. Fare tekerleği yakınlaştırır. WASD, ok tuşları ya da orta tuşla sürükleyerek kamerayı kaydırırsın, Space ortalar. İzlerken 1x / 2x / Atla düğmeleri var.

**ESC menüsü:** Devam Et, Nasıl Oynanır?, Tam Ekran / Pencere, Ana Menüye Dön, Oyundan Çık. Bot modunda oyunu duraklatır. Online maçta süre işlemeye devam eder.

**Maç sonu:** Oda sahibi (ya da bot modunda sen) **Rövanş** ile menüye dönmeden sıradaki haritada yeni maç başlatır.

## Sahne (Hierarchy)

Proje ilk açıldığında `SampleScene`'e şu nesneler otomatik eklenir ve sahne kaydedilir. Elle tekrar kurmak için menüden **Blind Attack > Sahneyi Kur**:

- **Blind Attack**: oyunun yöneticisi. Inspector'daki **Match Map** ile harita seçilir. Varsayılan **Her Macta Degisir**: haritalar sırayla döner. Belirli bir harita seçerseniz hep o oynanır.
- **Harita**: oyun çalışmıyorken de Scene penceresinde görünür. **Preview Map** ile önizlenen haritayı değiştirebilirsiniz. Altındaki parçalar (Ağaçlar, Çalılar, Evler, Su...) her açılışta koddan üretilir, sahneye kaydedilmez. Elle yapılan değişiklikler kaybolur. Harita düzenlemek için `MapLibrary.cs`'yi kullanın.
- **Çevre**: haritanın etrafındaki orman, tepeler, göl ve köy. `seed` değeriyle farklı bir manzara elde edebilirsiniz.
- **Main Camera** üzerindeki **IsoCamera**: kamera açısı (pitch/yaw).

## Projeyi açma

1. Unity Hub'dan projeyi **Unity 6000.3.9f1** ile açın. İlk açılışta Netcode for GameObjects ve Multiplayer Services paketleri otomatik iner.
2. `Assets/Scenes/SampleScene` sahnesini açıp Play'e basın. Oyun kendini koddan kurar, sahneye bir şey eklemeniz gerekmiyor.
3. İlk açılışta `Assets/BlindAttack/Resources` içine birkaç malzeme (`.mat`) otomatik oluşur. Bunları da Git'e ekleyin, build için gerekli.

## Bağlantı modları

| Mod | Ne zaman | Kurulum |
|---|---|---|
| **Bota Karşı Dene** | Tek başına test | Hiçbir şey gerekmez |
| **LAN Kur / LAN Katıl** | Aynı Wi-Fi'deki iki PC, ya da aynı PC'de iki kopya | Host'un ekranında görünen IP'yi diğer oyuncu yazar. Windows güvenlik duvarı sorarsa izin verin (port 7777) |
| **Oda Kur / Katıl (Online)** | Farklı evlerden, internet üzerinden | Aşağıdaki tek seferlik ayar |

### Online (oda kodu) için tek seferlik ayar

Online mod Unity Relay kullanır (ücretsiz katman yeterli):

1. Unity Editor'de **Edit > Project Settings > Services**'e gidin, Unity hesabınızla giriş yapıp projeyi bir Unity Cloud projesine bağlayın (yoksa "Create project").
2. Gerekirse [Unity Cloud Dashboard](https://cloud.unity.com)'da projeniz için **Relay** ve **Lobby** servislerinin açık olduğundan emin olun.
3. Ekipteki herkes aynı Cloud projesine bağlı olmalı (bağlantı `ProjectSettings` içinde saklanır, Git ile paylaşılır).

Bağlı değilse oyun menüde "Proje Unity Cloud'a bağlı değil" der. O sırada LAN ile test edebilirsiniz.

### Kendi bilgisayarında iki oyunculu test

- **File > Build Profiles > Build** ile bir `.exe` alın, exe'yi ve Editor'ü aynı anda açın. Biri "LAN Kur", diğeri IP boş bırakıp "LAN Katıl".
- Ya da Package Manager'dan **Multiplayer Play Mode** paketini kurup Editor içinde ikinci bir oyuncu açın.

## Kod yapısı

```
Assets/BlindAttack/Scripts/
  Core/      Unity'den bağımsız oyun mantığı (test edilebilir)
    GridMap, MapLibrary   haritalar (30x20, ASCII ile tanımlı)
    RoutePlanner          kahramanın rotası (en yakın hedef sırası)
    Units                 birim/kutu tanımları ve maliyetleri
    MatchRules            bütçe, süreler, yerleştirme kuralları  <- denge ayarları burada
    Pathfinder            A* yol bulma
    AttackSimulation      saldırının deterministik simülasyonu
    BotPlanner            bot modu: dar geçitleri bulan savunma, dengeli kutu seçen saldırı
  Match/MatchHost         maçın hakemi (sadece host'ta çalışır) + bot oyuncu
  Net/NetLayer            Netcode named message + Relay oturumu + LAN
  Game/                   Unity tarafı: sahne, görseller, arayüz
    GameApp               giriş noktası, modlar
    PlayerSession         yerel oyuncunun ekran akışı
    MapView, EnvironmentView  harita ve çevre (edit modunda da görünür)
    Visuals, HeroRig, MeshGen modeller, kahraman yükseltmeleri, prosedürel mesh'ler
    RoutePreview          noktalı rota çizgisi
    Art                   malzemeler ve el boyaması dokular
    PlacementController   fareyle yerleştirme
    Playback, Effects     saldırıyı oynatma (açılış sinematiği, ağır çekim), efektler
    Juice                 menzil halkası, "!" işareti, konuşma balonu, yerden çıkma, duman, rüzgâr
    Atmosphere            harita başına gün saati ve ışık ayarları, kuşlar, sis, ateş böcekleri
    MatchStats            tur sonu istatistikleri ve başarımlar
    GameUI, UiKit         tüm arayüz (koddan)
```

**Ağ nasıl çalışıyor:** Oyun "client-side". Ağdan sadece kısa metin mesajları gider (yerleştirmeler, aşama değişimleri). Saldırı simülasyonu deterministik olduğu için iki bilgisayar aynı yerleştirmelerden aynı saldırıyı kendi başına hesaplar ve oynatır. Puanı host'un hesabı belirler.

## Hand-painted görsellere geçiş

Şu anki modeller Unity primitive'lerinden yapılmış yer tutuculardır. Zemin ve duvar dokuları kodla "fırça darbesi" gibi boyanıyor. Gerçek hand-painted modeller geldiğinde:

- Birimler ve kutular: `Visuals.Create(...)` ve `Visuals.Hero()` içinde primitive yerine `Resources.Load<GameObject>("...")` ile prefab yükleyin.
- Ağaç, çalı, harabe, ev, köprü: `MapView.cs` içindeki `Tree`, `BuildHedge`, `BuildRuin`, `House`, `BuildBridge`.
- Renk paleti: `Art.cs` başındaki renkler.

## Yeni harita eklemek

`MapLibrary.cs` içine 30x20'lik yeni bir ASCII dizi, `Names` listesine bir isim ve `MapView.cs`'deki `MapId` ile `GameApp.cs`'deki `MapChoice` listelerine bir seçenek ekleyin. `.` çimen, `,` toprak yol, `#` taş harabe, `h` çalı, `T` ağaç, `R` kaya, `~` su, `=` köprü, `H` ev, `S` önerilen kahraman başlangıcı, `E` çıkış.
