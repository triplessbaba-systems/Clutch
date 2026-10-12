# Clutch — Taslak

Windows için **NVIDIA ShadowPlay tarzı, OBS yoğunluğunda** bir kayıt uygulaması. **Ana penceresi yoktur:**
arka planda (tepside) çalışır, her şey kısayolla açılan **overlay**'den yapılır.
Oyundaki "clutch" anı kaçmasın diye arka planda sürekli tampon tutar; kısayolla **son N saniyeyi kaydeder**,
**manuel kayıt** alır, **ekran görüntüsü** çeker. Kodlama ekran kartında yapılır (NVENC / AMF / Quick Sync).

---

## 1. NVIDIA'daki karşılıkları

| NVIDIA                         | Clutch                          | Durum      |
|--------------------------------|-------------------------------------|------------|
| Anında tekrar (Instant Replay) | Son 15 sn – 20 dk'yı kaydet         | ✅ v0.1    |
| Kayıt (Record)                 | Başlat / durdur, süre göstergesi    | ✅ v0.1    |
| Ekran görüntüsü                | PNG, oyun adına göre klasörlenir     | ✅ v0.1    |
| Alt+Z overlay                  | Üstte HUD şeridi + son klipler şeridi | ✅ v0.2    |
| Bildirimler                    | Sağ üstte, kaydedilen klibin küçük resmiyle | ✅ v0.2 |
| Oyun adına göre klasörleme     | `Videolar\Clutch\{Oyun}\...`     | ✅ v0.1    |
| Mikrofon / ayrı ses kanalları  | Sistem sesi + mikrofon, tek ya da ayrı kanal | ✅ v0.1 (mikrofon henüz denenmedi) |
| Performans göstergesi          | Kayıt FPS, kare kaybı, CPU, boş disk (stüdyo + overlay) | ✅ v0.2 |
| Galeri                         | Son 40 klip, küçük resimli; çift tık aç, sağ tık klasörde göster | ✅ v0.2 |
| Highlights / oyun içi olaylar  | —                                   | düşünülmedi |
| Klip kırpma                    | —                                   | v0.3       |

Overlay'de OBS'den alınanlar: kaynak aç/kapat, segmentli seviye göstergeli ses mikseri (kayıtta canlı etkili ses ayarı ve
sessize alma), canlı kayıt FPS / kare kaybı, ayrıntılı çıkış ayarları (ön ayar, VBR/CBR, MP4/MKV, ses bit hızı).
Sahneler, çoklu kaynak, yayın kapsam dışı.

## 2. Varsayılan kısayollar

| Eylem                   | Kısayol              |
|-------------------------|----------------------|
| Overlay'i aç/kapat      | `Ctrl+Alt+F5`        |
| Ekran görüntüsü         | `Ctrl+Alt+F6`        |
| Kaydı başlat/durdur     | `Ctrl+Alt+F7`        |
| Tekrarı kaydet          | `Ctrl+Alt+F8`        |
| Anında tekrarı aç/kapat | `Ctrl+Alt+Shift+F8`  |

Neden bunlar:
- NVIDIA (Alt+Z, Alt+F1/F2/F3/F9/F10/F12, Alt+R), Xbox Game Bar (Win+Alt+…), Steam (F12, Shift+Tab) ve Discord bu tuşları kullanmaz.
- Türkçe klavyede Ctrl+Alt = AltGr. Ctrl+Alt+harf seçilseydi `@`, `€`, `{` gibi karakterler yazılamazdı. F tuşlarında bu sorun yok.

**Çakışma neden ilk sürümde yakalanmadı?** NVIDIA ve Steam kısayolları Windows'a kaydetmez, klavyeyi kancayla dinler.
Bu yüzden aynı tuşu almak hata vermiyor, iki uygulama birden tetikleniyor. Artık bilinen kısayollar listesiyle
(`KnownShortcuts.cs`) kontrol ediliyor: ayarlarda sarı uyarı, açılışta bildirim.

Kısayollar ayarlarda **tuşa basarak** değiştirilir. Kutuya tıklanınca uygulamanın kısayolları geçici olarak susturulur,
yeni kombinasyon kaydedilir, Esc vazgeçer. Eski sürümün Alt+… varsayılanları açılışta otomatik olarak yenileriyle değiştirilir
(kullanıcının kendi seçtiklerine dokunulmaz).

## 3. Ekranlar

Ana pencere yok. Tek arayüz overlay (`Ctrl+Alt+F5` ya da tepsi simgesine tıklama):

```
┌─ HUD ──────────────────────────────────────────────────────────────────────────┐
│ CLUTCH │ Anında Tekrar · Son N sn · Kayıt · Ekran Görüntüsü │ Klasör · Ayarlar │ FPS CPU kayıp disk │ ✕ │
└──────────────────────────────────────────────────────────────────────────────── yeşil çizgi ┘
┌─ SON KLİPLER ────────────────────────────────┐ ┌─ SES VE KAYNAKLAR │ AYARLAR ─┐
│ küçük resimli klip ızgarası                   │ │ Kaynaklar: ekran (canlı),   │
│ tıkla: aç · sağ tık: klasörde göster          │ │ sistem sesi, mikrofon, imleç│
│                                               │ │ Ses mikseri: gösterge,      │
│                                               │ │ kaydırıcı, sessize alma     │
│                                               │ │ ── ya da ──                 │
│                                               │ │ Ayarlar: çıkış, video,      │
│                                               │ │ tekrar, ses, kısayollar,    │
│                                               │ │ klasör, çıkış · Uygula      │
└───────────────────────────────────────────────┘ └─────────────────────────────┘
```

Tasarım kuralları (gerçek bir ürün ekibinin işi gibi görünsün diye):
- Yüzeyler kenarlıkla değil ton farkıyla ayrılır; köşeler panelde 10, kutucukta 8, kontrolde 6 px.
- Metinler cümle düzeninde ("Anında tekrar", "Klip kaydet"); büyük harfli mikro etiketler, nokta ayraçlı listeler, rozetler yok.
- Yeşil yalnızca "açık" durumu ve birincil buton için, kırmızı yalnızca kayıt için. Başlıklar renkli değil.
- Kısayollar eylem şeridinde yazmaz, araç ipucunda görünür. Açıklama metni yalnızca gerçekten gerekli yerde.
- Sayılar ayrı bir eş aralıklı yazı tipiyle değil, aynı yazı tipinin eş genişlikli rakamlarıyla (Tabular) yazılır.
- Galeri gerçek video oynatıcı galerisi gibi: küçük resim, sağ altta süre, altında oyun adı ve "2 dk önce / Dün 22:05".
- Kendi logo işareti (geri saran ok + kayıt noktası), yazı Segoe UI Variable Display.
- Gerçek cihaz durumu: mikrofon takılı değilse anahtar devre dışı ve "Cihaz bulunamadı".

- **Bildirim:** sağ üstte, solunda türüne göre renkli şerit; klip kaydedildiyse küçük resmi. Açılışta "Clutch hazır" bildirimi.
- **Kayıt göstergesi:** sağ üstte `● REC 00:01:23`.
- **Tepsi simgesi:** gri boşta, yeşil tekrar açık, kırmızı kayıtta. Sol tık overlay, sağ tık menü (çıkış dahil).

Overlay, bildirim ve gösterge **videolara ve ekran görüntülerine girmez** (`WDA_EXCLUDEFROMCAPTURE`).

## 4. Nasıl çalışıyor?

```
 Kısayol / Tepsi / Overlay
            │
            ▼
   CaptureController ── ayarlar (JSON, %AppData%\Clutch)
      │        │        │
      ▼        ▼        ▼
 ReplayBuffer  ScreenRecorder  ScreenshotService
      │        │
      └───┬────┘
          ▼
    CaptureSession ──► ffmpeg.exe
          │              ├─ giriş 0: ddagrab (Desktop Duplication, kareler GPU'da kalır)
          │              ├─ giriş 1: sistem sesi  ◄── named pipe ◄── NAudio WASAPI loopback
          │              └─ giriş 2: mikrofon     ◄── named pipe ◄── NAudio WASAPI
          ▼
     h264_nvenc / hevc_nvenc / av1_nvenc (yoksa AMF → QSV → işlemci)
```

**Anında tekrar:** ffmpeg sürekli 2 saniyelik `.ts` parçaları yazar ve eskileri kendisi siler (`hls` + `delete_segments`).
Kaydet'e basılınca son parçalar kopyalanıp **yeniden kodlanmadan** (`-c copy`) MP4'te birleştirilir. Kaydetme anlıktır, GPU'ya yük bindirmez.

**Manuel kayıt:** Önce MKV'ye yazılır (çökmede dosya kurtarılabilir), durunca kayıpsız MP4'e çevrilir.

**Kodlayıcı seçimi:** Açılışta NVENC → AMF → QSV sırasıyla 3 kare gerçekten kodlanarak denenir.
`-encoders` listesine güvenilmez, çünkü NVIDIA kartı olmayan makinede de `h264_nvenc` listelenir.

**Ses:** ffmpeg Windows'ta sistem sesini (loopback) kendisi yakalayamıyor. NAudio ile yakalanıp isimli borudan ham PCM olarak veriliyor.
Hiç ses çalmıyorken WASAPI veri göndermeyi kestiği için varsayılan cihaza sürekli sessizlik çalınıyor (aksi halde ses kayıyor).

**Canlı istatistik:** ffmpeg `-progress pipe:1` ile her saniye FPS, kare kaybı ve dosya boyutu bildirir; durum çubuğu ve overlay bunları gösterir.

**Mikser:** ses seviyesi ve sessize alma ffmpeg'e gitmeden, ham örneklere uygulanır; kayıt sürerken de anında etkilidir.
Seviye göstergesi ayrı bir WASAPI dinleyicisinden gelir ve kayda giren (ses ayarı uygulanmış) seviyeyi gösterir.
Mikrofon yalnızca kayda alınıyorsa dinlenir (yoksa Windows boşuna "mikrofon kullanımda" gösterirdi).

**Güvenlik ağı:** ffmpeg süreçleri bir Job Object'e bağlı. Uygulama çökse bile arkada ffmpeg kalmaz (test edildi).

## 5. Klasör yapısı

```
Clutch/
├─ Clutch.slnx
├─ TASLAK.md
└─ src/Clutch/
   ├─ App.xaml(.cs)              açılış, tepsi, kısayollar, overlay'in bağlanması (ana pencere yok)
   ├─ Core/
   │  ├─ Capture/                ffmpeg süreci + canlı istatistik, kodlayıcı tespiti, kayıt, anında tekrar,
   │  │                          ekran görüntüsü, küçük resim
   │  ├─ Audio/                  AudioPipe (WASAPI → named pipe), AudioMixer (ses/sessiz), AudioLevelMonitor
   │  ├─ Native/                 Win32: kısayol, tepsi, Job Object, capture exclusion, öndeki uygulama, CPU/disk
   │  ├─ Settings/               AppSettings, JSON kayıt + taşıma, kısayol ayrıştırma, bilinen kısayollar
   │  └─ Log.cs                  %LocalAppData%\Clutch\app.log
   ├─ Services/                  CaptureController, ClipLibrary, LiveStats, HotkeyService, TrayIconService
   ├─ ViewModels/                Overlay, MixerChannel, ClipItem
   ├─ Views/                     Overlay (tek arayüz), SettingsPanel (overlay içinde), Toast, RecordingIndicator
   ├─ UI/                        Theme.xaml, FluentAccent.xaml, WindowTheme (Fluent koyu tema + yeşil vurgu)
   ├─ UI/Controls/               LevelMeter (OBS tarzı gösterge), HotkeyBox, SettingRow
   └─ ffmpeg/ffmpeg.exe          (git'e girmez)
```

## 6. Çalıştırma

1. ffmpeg'i yerleştir: `src/Clutch/ffmpeg/ffmpeg.exe` (ddagrab içeren bir Windows derlemesi, ör. gyan.dev "essentials").
   Yoksa uygulama PATH'e bakar. Geliştirme makinesinde bu dosya şu an mevcut.
2. `dotnet build` → `src/Clutch/bin/Debug/net10.0-windows/win-x64/Clutch.exe`
3. Pencere açılmaz: Clutch tepsiye yerleşir, anında tekrar otomatik başlar, "Clutch hazır" bildirimi çıkar.
   `Ctrl+Alt+F5` overlay'i açar.

Geliştirici notu: `CLUTCH_SHOW_IN_CAPTURE=1` ortam değişkeniyle overlay ve bildirimler kayıtlarda görünür (README görselleri için).

## 7. Test edilenler (RTX 4060 Ti, 1080p)

- NVENC otomatik bulundu, anında tekrar 2 sn'lik parçalarla çalışıyor.
- 15 sn'lik tekrar → 18 sn MP4, **60 FPS** sabit, sistem sesi var.
- 6 sn'lik manuel kayıt → kare atlaması yok; çalınan ses videoda doğru saniyede (A/V senkron).
- Ekran görüntüsü PNG, klipler öndeki uygulamanın adıyla klasörlendi.
- Overlay açıkken alınan ekran görüntüsünde overlay görünmüyor.
- Uygulama zorla kapatılınca ffmpeg süreçleri de kapandı.

Test sırasında bulunup düzeltilenler:
- `segment_wrap` ile parça sırası karışıyordu → `hls` + artan numaralar.
- Ses borusu yüzünden kaydın ilk ~4 sn'si donuk kalıyordu → boru girişlerinde analiz kapatıldı (`-probesize 32 -analyzeduration 0`).
- Ayarlar penceresinde koyu tema yarım uygulanıyordu → düzeltildi.

v0.3 (yalnızca overlay) testleri:
- Ana pencere kaldırıldı; tekrar kaydı ve ekran görüntüsü kısayolla alındı, overlay galerisinde küçük resimleriyle göründü.
- Ses ve Kaynaklar sekmesi: seviye göstergesi çalınan sesle oynadı. Ayarlar sekmesi: tüm açılır listeler koyu temada.
- Hata: Ses sekmesindeyken ayarlar paneli de alttan görünüyordu (görünürlük yanlış nesneye bağlıydı) → düzeltildi.

v0.2 (stüdyo, sonradan kaldırıldı) testleri:
- Stüdyo: canlı önizleme, sistem sesi seviye göstergesi (çalınan Windows sesiyle oynadı), durum çubuğunda 59.9 / 60 FPS ve kare kaybı.
- Anında tekrar kaydı → galeriye küçük resmiyle eklendi, bildirimde küçük resim göründü.
- Kayıt sırasında önizleme kırmızı çerçeve + `REC` etiketi; durunca durum çubuğu `00:00:00`'a döndü.
- Overlay HUD ve son klipler şeridi, ayarlar sayfaları ekran görüntüsüyle kontrol edildi.
- Açıklanamayan bir kez: ilk çalıştırmada 18. saniyede tekrar "kapalı" göründü; sonraki üç denemede tekrarlanmadı.
  Tanı için başlat/durdur/ayar uygula olayları artık günlüğe yazılıyor.

## 8. Bilinen sınırlar / riskler

- **Denetimli klasör erişimi:** Windows Güvenliği'nde fidye yazılımı koruması açıksa `Videolar` klasörüne yazılamaz.
  Uygulama bunu söyleyen bir bildirim gösterir. Çözüm: Clutch'e izin vermek ya da başka klasör seçmek.
- **Tam ekran (exclusive) eski oyunlar:** Desktop Duplication bazı DX9/DX11 exclusive oyunlarda siyah görüntü verebilir. Kenarlıksız pencere sorunsuz.
- **Overlay:** Exclusive tam ekran oyunların üstünde görünmez (NVIDIA bunu sürücü düzeyinde çözüyor). Kısayollar her durumda çalışır.
- **Çözünürlük ölçekleme yok:** Şimdilik doğal çözünürlükte kaydediliyor. GPU'da ölçekleme ffmpeg 8 ile gelen `scale_d3d11` ile eklenebilir.
- **Ekran numarası:** `ddagrab output_idx` ile Windows'un ekran numarası birden fazla GPU'da farklı olabilir.
- **Tekrar süresi:** Kaydedilen klip ayarlanandan 2–4 sn uzun olabilir (parça sınırı). İstenirse tam kırpılır.
- **Mikrofon** ve **AMF/QSV** yolları kodda var ama bu makinede denenmedi.
- **ffmpeg lisansı:** gyan.dev derlemesi GPL. Ayrı exe olarak dağıtmak sorun değil, ama sürümde lisans dosyası eklenmeli.

## 9. Yol haritası

**v0.2 — Stüdyo** (OBS düzeni; v0.3'te kaldırıldı, özellikleri overlay'e taşındı)

**v0.3 — Yalnızca overlay** ✅ (HUD + galeri + Ses ve Kaynaklar + Ayarlar tek ekranda, ana pencere yok)

**v0.2.x — Günlük kullanım**
- Windows ile başlat (ayar + kayıt defteri, `--minimized`)
- Galeride sil (geri dönüşüm kutusuna), yeniden adlandır
- Bildirime tıklayınca klibi aç
- Ses: mikrofon cihazı seçimi, uygulama başına ses (Windows 10 2004+ process loopback)
- Uygulama simgesi

**v0.3 — NVIDIA'ya yaklaşmak**
- Çözünürlük seçimi (1080p / 1440p / 4K, GPU'da ölçekleme)
- FPS / donanım göstergesi
- Tek pencere kaydı (Windows.Graphics.Capture, ffmpeg 8 `gfxcapture`)
- Klip kırpıcı (başlangıç–bitiş seç, `-c copy` ile kes)

**v1.0 — Yayın**
- Tek dosya kurulum, ffmpeg'in otomatik indirilmesi
- Güncelleme bildirimi (Masaüstü Dostları'ndaki gibi)
- README, demo GIF, GitHub Releases
