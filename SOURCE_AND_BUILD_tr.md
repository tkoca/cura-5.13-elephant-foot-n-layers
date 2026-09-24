# Kaynak Kod ve Derleme Kılavuzu

[English](SOURCE_AND_BUILD.md) · [Türkçe](SOURCE_AND_BUILD_tr.md)

## Depo yapısı

| Yol | Amaç |
| --- | --- |
| `patches/CuraEngine-5.13.0.patch` | CuraEngine değişikliği (`src/slicer.cpp`): ilk N katmanın her birinin hangi yatay genişleme değerini kullanacağı. |
| `patches/Cura-5.13.0.patch` | Cura kaynak dosyaları: iki ayar tanımı, Türkçe çeviriler, Uzman görünürlüğü. |
| `installer/Installer.cs`, `installer/app.manifest` | Tek dosyalık Windows kurucu/kaldırıcı (.NET Framework 4.x, x64, Türkçe/İngilizce arayüz). |
| `scripts/build-curaengine.ps1` | Yamalı CuraEngine 5.13.0'ı UltiMaker'ın kaynak arşivinden derler. |
| `scripts/build-installer.ps1` | Kurulum paketini (payload) ve kurulum dosyasını oluşturur; derleyen kişinin kullanıcı adını veya bilgisayar adını içeren dosyaları reddeder. |
| `scripts/make_payload.py` | Cura 5.13.0 kaynak arşivinden üç dosyayı alır ve doğrular, Cura yamasını uygular, `.mo` dosyasını derler ve deterministik `payload.zip` üretir. |
| `scripts/apply_patch.py`, `scripts/po2mo.py` | Bağımlılığı olmayan küçük yardımcılar: katı yama uygulayıcı, `.po` → `.mo` derleyici. |
| `tests/installer-tests.ps1` | Kurucunun test sürümüyle kurulum, kaldırma, güncelleme ve hata senaryoları. |
| `tests/engine/slice-tests.ps1` (+ `analyze_gcode.py`, `prepare_definitions.py`, `box20.stl`) | 20 mm'lik bir test kutusunu resmi ve yamalı CuraEngine ile dilimleyip G-code'u kontrol eder. |

## CuraEngine değişikliği

UltiMaker'ın CuraEngine'inde `Slicer::makePolygons`, ilk basılan katman için `xy_offset_layer_0`, diğer tüm katmanlar için `xy_offset` değerini kullanır. Yama bu ilk katman kuralını değiştirmez. Telafi aralığındaki sonraki katmanlar için (sıfırdan başlayan basılan katman sırası `i = 1 … N-1`) şunu kullanır:

- kademeli telafi kapalıyken: `xy_offset_layer_0`
- kademeli telafi açıkken: `xy_offset_layer_0 + (xy_offset - xy_offset_layer_0) × i / N`, en yakın mikrometreye yuvarlanır

`N = 1` iken sonuç UltiMaker'ınkiyle birebir aynıdır; bu, resmi motorun G-code'uyla karşılaştırılarak test edilir. README'deki örnek tabloda katmanlar 1'den başlayarak sayılır.

## Gereksinimler

- Windows 10/11 x64.
- Visual Studio 2022 Build Tools: MSVC v143, bir Windows 10/11 SDK ve "C++ CMake tools for Windows".
- Conan 2 kurulu Python 3.12 (`pip install conan`).
- Derlemede kullanılacak Conan klasörüne kurulmuş UltiMaker Conan yapılandırması (aşağıya bakın):
  `conan config install https://github.com/Ultimaker/conan-config.git`
  Bu komut `cura.jinja` profilini ve UltiMaker paket sunucusunu ekler.
- `5.13.0` etiketlerinin kaynak arşivleri:
  - `https://github.com/Ultimaker/CuraEngine/archive/refs/tags/5.13.0.zip`
  - `https://github.com/Ultimaker/Cura/archive/refs/tags/5.13.0.zip`

## Derleme

Conan klasörü ve çalışma klasörü için **kullanıcı profilinizin dışında** kısa klasörler kullanın. Bağımlılıkların derlemesi (gRPC, protobuf, abseil…) kaynak dosya yollarını programın içine yazar; bu yüzden `C:\Users\<ad>\…` gibi bir yol `CuraEngine.exe` içinde kalır. `build-curaengine.ps1` böyle yolları reddeder ve `TEMP` klasörünü de Conan klasörünün içine taşır. `build-installer.ps1` son dosyaları bir kez daha kontrol eder.

```powershell
$env:CONAN_HOME = 'C:\conan-efnl'
conan config install https://github.com/Ultimaker/conan-config.git

# 1. CuraEngine. İlk çalıştırmada tüm Conan bağımlılıkları derlenir; birkaç saat sürebilir.
.\scripts\build-curaengine.ps1 -SourceZip CuraEngine-5.13.0.zip -WorkDir C:\efb -ConanHome C:\conan-efnl `
    -VsInstallPath "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools"

# 2. Kurulum dosyası (+ testlerde kullanılan TEST sürümü, dist\obj içinde)
.\scripts\build-installer.ps1 -CuraSourceZip Cura-5.13.0.zip `
    -CuraEngine C:\efb\CuraEngine-5.13.0\build\Release\CuraEngine.exe `
    -VsInstallPath "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools" -TestBuild
```

CuraEngine, resmi Cura derlemesinde olduğu gibi `conanfile.py` içindeki varsayılan seçeneklerle derlenir: Arcus (Cura arayüzüyle bağlantı) ve motor eklentisi desteği (gRPC) açıktır. Yalnızca UltiMaker'ın resmi derlemesinin kullandığı Sentry hata raporlaması kapalı kalır.

`build-curaengine.ps1`, `VSLANG=1033` ortam değişkenini ayarlar. Visual Studio başka bir dildeyse (örneğin Türkçe) çevrilmiş `/showIncludes` öneki (`Not: eklenen dosya:`), bağımlılık derlemelerindeki kaynak derleyici adımlarını bozar (`RC1107`).

## Bilinen Cura 5.13.0 dosyaları

Eklentinin değiştirdiği UltiMaker Cura 5.13.0 (Windows x64) dosyalarının SHA-256 değerleri:

| Dosya | SHA-256 |
| --- | --- |
| `CuraEngine.exe` | `36e36d4618cd09a0cd8802a565c2cdbe54804eb750ac84303a0c6d6f784afde2` |
| `definitions/fdmprinter.def.json` | `8fbbf8b779e806bd8d0b0e2b8b9be17bdd26a575b7ea1875c20481eb2c7e11ee` |
| `i18n/tr_TR/fdmprinter.def.json.po` | `ace33455f77ad50ed5d10dbd2f1c2b5732d2c59dcc5e99a9e715ad1bd3f51a85` |
| `i18n/tr_TR/LC_MESSAGES/fdmprinter.def.json.mo` | `16dda401341e0805149bdcd674677bf5081413163c6b059255c637187599d3ad` |
| `setting_visibility/expert.cfg` | `9b646941fa24799eda46ce207f586ab72687d7b02f837264287bb022b720a4ba` |

- Kurucu bir dosyayı yalnızca bu listeyle (ya da eklentinin kendi dosyasıyla) eşleşiyorsa değiştirir veya geri yükler.
- `make_payload.py`, Cura kaynak arşivindeki üç metin dosyasını (`fdmprinter.def.json`, `.po`, `expert.cfg`) bu listeyle karşılaştırır. `.mo` yamalı `.po` dosyasından derlenir; `CuraEngine.exe` ise sizin derlemenizdir.

## Yeniden üretilebilirlik

- `payload.zip` deterministiktir (sabit zaman damgaları ve sıra). Kaynak dosyaları ve `.mo`, derleyen herkeste byte düzeyinde aynıdır.
- C# derleyicisi `/deterministic` ve `/pathmap` ile, PDB üretmeden çalışır; aynı `payload.zip` ve aynı derleyici sürümü her zaman aynı kurulum dosyasını verir.
- `CuraEngine.exe` bit düzeyinde yeniden üretilemez (MSVC bağlama zaman damgası, derleme tarihi, araç sürümü). SHA-256 değeri her sürümle birlikte yayımlanır. Herkes aynı kaynaklardan yeniden derleyip davranışını `tests/engine/slice-tests.ps1` ile karşılaştırabilir.

## Testler

### Kurucu

```powershell
.\tests\installer-tests.ps1 -TestExe dist\obj\Setup.Test.exe -OriginalsDir <klasör> [-LegacyDir <klasör>]
```

- `-OriginalsDir`: Beş Cura 5.13.0 dosyasının, Cura kurulum klasöründeki düzende (`CuraEngine.exe`, `share\cura\resources\…`) bulunduğu bir klasör.
- `-LegacyDir` (isteğe bağlı): Aynı düzende önceki 4.0.0 test sürümünün dosyaları. O sürüm için güncelleme ve kaldırma testlerini etkinleştirir.
- Test sürümü (`/define:TEST`), tüm klasörleri ve kaldırma kayıt anahtarını geçici bir klasöre ve `HKCU\Software\EFNLTest` altına yönlendirir; yönetici yetkisi istemez. Gerçek sistemde hiçbir şey değişmez.
- Senaryolar:
  - temiz kurulum, yeniden kurulum
  - profil ve önbellek temizliğiyle kaldırma; hiçbir şey kurulu değilken kaldırma
  - bilinmeyen Cura dosyaları; kurulumdan sonra değişmiş bir Cura dosyası; önce kaldırılmış Cura
  - bozulmuş yedek; kilitli dosyada geri alma; profil içinde junction
  - 4.0.0'dan güncelleme ve 4.0.0'ın kaldırılması

### Dilimleme

```powershell
.\tests\engine\slice-tests.ps1 -OfficialEngine <Cura 5.13.0'ın CuraEngine.exe dosyası> -PatchedEngine <sizin CuraEngine.exe'niz> `
    -PrinterDefinition <payload.zip içindeki yamalı fdmprinter.def.json> -ExtruderDefinition <Cura 5.13.0'ın fdmextruder.def.json dosyası> `
    [-CuraDir "C:\Program Files\UltiMaker Cura 5.13.0"] [-Python python]
```

- İki motor da kurulu bir Cura 5.13.0'ın çalışma zamanı DLL'lerinin (`-CuraDir`) yanına kopyalanıp oradan çalıştırılır.
- Kontroller:
  - Katman sayısı 1 iken iki motorun ürettiği G-code birebir aynıdır; hem varsayılan ayarlarla hem de −0,2 mm ilk katman genişlemesiyle.
  - N = 4 iken, kademeli telafi kapalı ve açık, her katmanın dış duvarı beklenen miktarda içeri çekilir.
  - N = 2, kademeli telafi açık ve +0,1 mm Yatay Büyüme ile adımlar +0,1 mm'ye doğru ilerler.

## UltiMaker kaynakları

- Cura 5.13.0: https://github.com/Ultimaker/Cura/tree/5.13.0 (LGPL-3.0-or-later)
- CuraEngine 5.13.0: https://github.com/Ultimaker/CuraEngine/tree/5.13.0 (AGPL-3.0-or-later)

## Linux

Linux sürümü aynı yamaları ve kaynak dosyalarını kullanır; kaynak kodu ve derlemesi [cura-5.13-elephant-foot-n-layers-linux](https://github.com/tkoca/cura-5.13-elephant-foot-n-layers-linux) deposundadır.
