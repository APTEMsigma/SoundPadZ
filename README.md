# 🔊 SoundPadZ

<p align="center">
  <img src="SoundPadZ/logo.png" width="128" height="128" alt="SoundPadZ Logo" style="border-radius: 16px;" />
</p>

<p align="center">
  <b>Современный, быстрый и бесплатный саундпад для Discord, игр и стримов (аналог Soundpad).</b><br>
  <b>Modern, lightweight, free soundboard with virtual audio cable routing for Discord, games & streaming.</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0_WPF-512BD4?logo=dotnet" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Platform-Windows_x64-0078D6?logo=windows" alt="Windows x64" />
  <img src="https://img.shields.io/badge/Audio-NAudio_WASAPI-orange" alt="NAudio" />
  <img src="https://img.shields.io/badge/Design-Material_Design-00BCD4?logo=materialdesign" alt="Material Design" />
  <img src="https://img.shields.io/badge/AI-GLM_5.3_%2B_Gemini_3.7_Flash-blueviolet" alt="Vibe Coded with GLM 5.3 + Gemini 3.7 Flash" />
  <a href="https://www.virustotal.com/gui/file/232720bfc04790bc142ecd88900ed563922849a5e6400d965d4e0fd8a64d9e50"><img src="https://img.shields.io/badge/VirusTotal-Clean%20(0%2F70)-brightgreen?logo=virustotal" alt="VirusTotal Clean" /></a>
  <img src="https://img.shields.io/badge/License-MIT-green" alt="MIT License" />
</p>

---

## 🇷🇺 Описание на русском

**SoundPadZ** — это легковесное приложение на **C# / .NET 8 WPF**, позволяющее воспроизводить любые звуки и музыку прямо в микрофон (в Discord, CS2, Dota 2, Telegram, OBS и любые другие программы) через виртуальный кабель (VB-Audio Cable), одновременно слушая их в своих наушниках без эха и задержек.

> 🤖 **Vibe Coding**: Программа была полностью навайбкожена при помощи **GLM 5.3** + **Gemini 3.7 Flash**.

### 🌟 Основные возможности

* 🎙 **Раздельный звуковой тракт (Dual Audio Graph)**:
  * **Основной вывод (VB-Cable / Discord)**: звук + ваш микрофон смешиваются и передаются в игру или голосовой чат.
  * **Прослушивание (Наушники)**: воспроизведение звуков лично для вас с независимой регулировкой громкости (без попадания микрофона в наушники и без эффекта эха/резонанса).
* ⚡ **Глобальные горячие клавиши (Low-Level Keyboard Hook)**:
  * Быстрое назначение хоткея прямо на карточке звука (`[ ⌨ + Хоткей ]`).
  * Поддержка **одиночных модификаторов** (`Alt`, `Ctrl`, `Shift`, `Caps Lock`, `Tab`, `Space`, `F1-F24`, `Numpad`) и любых комбинаций (`Alt + 1`, `Ctrl + Shift + A` и др.).
  * Мгновенное удаление хоткея кнопкой `✕`.
  * Работает поверх полноэкранных игр.
* 🎨 **Кастомизация интерфейса**:
  * Светлая и тёмная темы.
  * **Палитра акцентных цветов**: Голубой, Изумрудный, Фиолетовый, Розовый, Оранжевый, Бирюзовый, Коралловый, Золотой.
  * Плавный Material Design с подсказками уровня громкости звуков при наведении.
* 🌐 **Многоязычность**: мгновенное переключение интерфейса **RU / EN**.
* 📁 **Библиотека звуков**:
  * Поддержка форматов `.mp3`, `.wav`, `.m4a`, `.aac`, `.flac`, `.wma`, `.ogg`.
  * Drag & Drop аудиофайлов прямо в окно.
  * Скачивание аудио по прямой ссылке.
  * Индивидуальная регулировка громкости и зацикливание для каждого звука.
* 🛡 **Безопасность**: чистый отчёт без вирусов и угроз ([Отчёт VirusTotal: 0/70](https://www.virustotal.com/gui/file/232720bfc04790bc142ecd88900ed563922849a5e6400d965d4e0fd8a64d9e50)).

### 🚀 Быстрый старт

1. Скачайте и запустите **`SoundPadZ.exe`** (или соберите проект командой `dotnet build`).
2. В блоке **Основной вывод** выберите `CABLE Input (VB-Audio Virtual Cable)`.
3. В блоке **Прослушивание** выберите свои наушники.
4. В блоке **Микрофон** выберите ваш физический микрофон и включите тумблер.
5. В Discord / игре укажите микрофон: `CABLE Output (VB-Audio Virtual Cable)`.
6. Добавьте любимые звуки и назначьте удобные клавиши!

---

## 🇬🇧 English Description

**SoundPadZ** is a high-performance **C# / .NET 8 WPF** soundboard that allows you to play audio clips, memes, and sound effects directly into your microphone stream (Discord, games, streaming apps) via VB-Audio Virtual Cable while monitoring the sound in your headphones with zero latency and zero feedback echo.

> 🤖 **Vibe Coding**: This project was fully vibe-coded using **GLM 5.3** + **Gemini 3.7 Flash**.

### 🌟 Key Features

* 🎙 **Dual Audio Graph Architecture**:
  * **Primary Output (VB-Cable / Discord)**: mixes soundboard audio + microphone input with 48 kHz WASAPI shared-mode streaming.
  * **Monitor Output (Headphones)**: plays sounds directly to your headphones with dedicated software DSP volume slider (no mic loopback, zero echo).
* ⚡ **Global Low-Level Keyboard Hooks**:
  * One-click hotkey binding directly on each sound tile (`[ ⌨ + Hotkey ]`).
  * Full support for standalone keys (`Alt`, `Ctrl`, `Shift`, `Space`, `F1-F24`, `Numpad`) as well as key combinations.
  * Dedicated `✕` button to instantly clear assigned hotkeys.
  * Works globally over fullscreen 3D games.
* 🎨 **Theme & Accent Customization**:
  * Dark / Light mode toggle.
  * Real-time accent palette: Sky Blue, Mint, Purple, Pink, Orange, Cyan, Coral, Gold.
  * Volume level tooltips on hover & drag.
* 🌐 **Bilingual (RU / EN)** with instant live switching.
* 📁 **Sound Management**:
  * Supports `.mp3`, `.wav`, `.m4a`, `.aac`, `.flac`, `.wma`, `.ogg`.
  * Drag & Drop support.
  * Direct URL audio downloader.
  * Per-sound volume and looping controls.
* 🛡 **Security & Safety**: verified clean binary with zero detections ([VirusTotal Report: 0/70](https://www.virustotal.com/gui/file/232720bfc04790bc142ecd88900ed563922849a5e6400d965d4e0fd8a64d9e50)).

---

## 🛠 Сборка из исходников / Building from Source

Требования: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) на Windows 10 / 11 x64.

```bash
# Клонировать репозиторий
git clone https://github.com/your-username/soundpadZ.git
cd soundpadZ

# Сборка и запуск
dotnet run --project SoundPadZ

# Публикация единого EXE-файла (Single-file)
dotnet publish SoundPadZ -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .
```

---

## 📜 Лицензия / License

Проект распространяется под свободной лицензией **MIT**. Подробности в файле [LICENSE](LICENSE).
