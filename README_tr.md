# Cura 5.13.0 — İlk N Katmanda Fil Ayağı Telafisi

[English](README.md) · [Türkçe](README_tr.md)

Bu, **Windows x64 üzerindeki UltiMaker Cura 5.13.0** için resmi olmayan bir eklentidir. *İlk Katmanın Yatay Genişlemesi* değerini yalnızca ilk katmana değil, basılan ilk **N** katmana uygular. İsteğe bağlı olarak değeri bu katmanlar boyunca adım adım normal *Yatay Büyüme* değerine döndürür.

> Bu bir topluluk projesidir. UltiMaker bu projeyi geliştirmemiştir, onaylamamıştır ve desteklememektedir.

## Ayarlar

*İlk Katmanın Yatay Genişlemesi* ayarının hemen altına (*Duvarlar* kategorisi) iki ayar eklenir. Ayarlar *Uzman* görünürlük ön ayarında yer alır; ayar aramasıyla da bulunabilir.

| Ayar | Varsayılan | Etkisi |
| --- | --- | --- |
| Fil Ayağı Telafi Katman Sayısı | `1` | *İlk Katmanın Yatay Genişlemesi* değerinin basılan ilk kaç katmanda kullanılacağı. `1`, standart Cura davranışıyla birebir aynıdır. 20'nin üzerinde Cura uyarı gösterir. |
| Fil Ayağı Kademeli Telafi | kapalı | Değeri bu katmanlar boyunca *İlk Katmanın Yatay Genişlemesi*'nden *Yatay Büyüme*'ye doğru adım adım değiştirir. Yalnızca katman sayısı 1'den büyükken görünür. |

Örnek: *İlk Katmanın Yatay Genişlemesi* `-0,20 mm`, *Yatay Büyüme* `0,00 mm`, katman sayısı `4`:

| Basılan katman | Kademeli kapalı | Kademeli açık |
| --- | --- | --- |
| 1 | -0,20 | -0,20 |
| 2 | -0,20 | -0,15 |
| 3 | -0,20 | -0,10 |
| 4 | -0,20 | -0,05 |
| 5 ve sonrası | 0,00 | 0,00 |

N katmanın hepsi telafi edilir; bunlardan sonraki katman normal değeri kullanır. *Yatay Büyüme* sıfır değilse adımlar 0'a değil, o değere doğru ilerler.

İngilizce arayüzde ayarların adı *Elephant Foot Compensation Layer Count* ve *Elephant Foot Gradual Compensation*'dır.

## Kurulum

1. [Releases](../../releases) sayfasından `Cura-5.13-Elephant-Foot-N-Layers-Setup.exe` dosyasını indirin. SHA-256 değerinin sürüm notlarındaki değerle aynı olduğunu kontrol edin (PowerShell değeri büyük harflerle yazar; büyük/küçük harf fark etmez):
   `Get-FileHash .\Cura-5.13-Elephant-Foot-N-Layers-Setup.exe -Algorithm SHA256`
2. Cura'yı kapatın.
3. Kurulum dosyasını çalıştırıp **Kur** düğmesine tıklayın. `C:\Program Files\UltiMaker Cura 5.13.0` altındaki dosyalar değiştirileceği için Windows yönetici onayı ister.
4. Cura'yı başlatın.

Kurulum ekranı, Windows görüntüleme dili Türkçeyse Türkçe, değilse İngilizce görünür. Dosya kod imzalı değildir; bu nedenle Windows SmartScreen "bilinmeyen yayımcı" uyarısı gösterebilir (*Ek bilgi → Yine de çalıştır*). Yalnızca SHA-256 değeri eşleşiyorsa devam edin.

Kurulum hiçbir şeyi değiştirmeden önce, etkilenen beş Cura dosyasının UltiMaker Cura 5.13.0 dosyalarıyla (güncellemede ise bu eklentinin kendi dosyalarıyla) birebir aynı olduğunu kontrol eder. Aynı değillerse hiçbir şeyi değiştirmeden durur.

## Kaldırma

*Ayarlar → Uygulamalar → Yüklü uygulamalar → Cura 5.13 - Fil Ayağı N Katman → Kaldır* yolunu izleyin ya da kurulum dosyasını yeniden çalıştırıp **Kaldır** düğmesine tıklayın.

- Özgün Cura dosyaları yedekten geri yüklenir. Geri yüklenen her dosya, bilinen Cura 5.13.0 dosyasıyla karşılaştırılır.
- Kurulumdan sonra değişmiş bir Cura dosyasının (örneğin Cura onarıldıysa veya güncellendiyse) üzerine **yazılmaz**; hangi dosyanın atlandığı size bildirilir.
- İki ayar Cura profilinizden (`%APPDATA%\cura\5.13`, kaydettiğiniz özel profiller dahil) silinir; eski ayar önbelleği dosyaları da (`%LOCALAPPDATA%\cura\5.13\cache`) temizlenir. *Yüklü uygulamalar* veya kurulum penceresinden kaldırdığınızda bu adım yönetici olarak değil, kendi Windows kullanıcınızla çalışır.
- Cura daha önce kaldırılmış olsa bile eklenti kaldırılabilir.

Cura'yı başka bir sürüme yükseltmeden önce eklentiyi kaldırın.

## Önceki test sürümünden (4.0.0) güncelleme

Yalnızca yedeğini `C:\ProgramData` altında tutan önceki 4.0.0 test sürümünü kurduysanız geçerlidir. Yeni kurulumu çalıştırıp **Güncelle** düğmesine tıklayın. Yedek `C:\Program Files\CuraElephantFootNLayers513` klasörüne taşınır ve eski klasör silinir.

## Bilgisayarınızda değişenler

| Konum | İçerik |
| --- | --- |
| `C:\Program Files\UltiMaker Cura 5.13.0\CuraEngine.exe` | Yamalı CuraEngine 5.13.0 |
| `…\share\cura\resources\definitions\fdmprinter.def.json` | İki yeni ayar |
| `…\share\cura\resources\i18n\tr_TR\fdmprinter.def.json.po` ve `LC_MESSAGES\fdmprinter.def.json.mo` | Türkçe adlar ve açıklamalar |
| `…\share\cura\resources\setting_visibility\expert.cfg` | Uzman ön ayarında listelenen ayarlar |
| `C:\Program Files\CuraElephantFootNLayers513\` | Özgün dosyaların yedeği ve `Uninstall.exe` (yalnızca yöneticiler değiştirebilir) |
| Kayıt defteri: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CuraElephantFootNLayers513` | *Yüklü uygulamalar* kaydı |

### Komut satırı (yöneticiler için)

```text
Cura-5.13-Elephant-Foot-N-Layers-Setup.exe /install /silent
"C:\Program Files\CuraElephantFootNLayers513\Uninstall.exe" /uninstall /silent
```

Çıkış kodu `0` başarılı demektir; `1` ise işlemin yapılmadığını veya tamamlanmadığını gösterir. Komutları yönetici olarak açılmış bir komut isteminden çalıştırın: böylece UAC penceresi çıkmaz ve atlanan dosya gibi uyarılar standart hata çıktısına yazılır. Profil temizliği, komutu çalıştıran hesaba uygulanır.

## Kaynak kod ve derleme

Kurulum dosyasını yeniden üretmek için gereken her şey bu depodadır: yamalar, kurucunun kaynak kodu, derleme betikleri ve testler. Ayrıntılar için [SOURCE_AND_BUILD_tr.md](SOURCE_AND_BUILD_tr.md) dosyasına bakın.

## Lisans

CuraEngine'in lisansı olan GNU Affero General Public License v3.0 veya sonrası geçerlidir ([LICENSE](LICENSE)). Cura kaynak dosyaları © UltiMaker, LGPL-3.0-or-later lisanslıdır. Kurulumdaki `CuraEngine.exe` dosyasının kaynak kodu, UltiMaker CuraEngine 5.13.0 ile [`patches/CuraEngine-5.13.0.patch`](patches/CuraEngine-5.13.0.patch) yamasıdır; [`scripts/build-curaengine.ps1`](scripts/build-curaengine.ps1) ile derlenir.

Kullanım sorumluluğu size aittir. Önce küçük bir kalibrasyon baskısıyla deneyin.
