# Üçüncü taraf bileşenler

Clutch'ın kaynak kodu MIT lisanslıdır. Dağıtım paketi aşağıdaki bileşenleri de içerir.

## FFmpeg

- Dosya: `ffmpeg/ffmpeg.exe`
- Lisans: GNU General Public License v3 (GPLv3) — https://www.gnu.org/licenses/gpl-3.0.html
- Derleme: gyan.dev "essentials" Windows derlemesi — https://www.gyan.dev/ffmpeg/builds/
- Kaynak kod: https://github.com/FFmpeg/FFmpeg ve derlemede kullanılan kütüphanelerin kaynakları gyan.dev sayfasında listelenir.

Clutch, ffmpeg'i değiştirmeden ayrı bir program olarak çalıştırır (komut satırı üzerinden). ffmpeg.exe'yi
kendi derlediğin bir sürümle değiştirebilirsin; `ddagrab` filtresini içermesi yeterlidir.

FFmpeg is a trademark of Fabrice Bellard, originator of the FFmpeg project.

## NAudio

- Kullanım: Windows ses yakalama (WASAPI)
- Lisans: Microsoft Public License (MS-PL) — https://github.com/naudio/NAudio/blob/master/license.txt
- Kaynak: https://github.com/naudio/NAudio

## .NET Runtime ve WPF

- Tek dosya paketin içinde gömülüdür.
- Lisans: MIT — https://github.com/dotnet/runtime/blob/main/LICENSE.TXT, https://github.com/dotnet/wpf/blob/main/LICENSE.TXT
