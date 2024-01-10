
<p align="center">
  <img src="https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/NewBannerv2_color.webp"/>
</p>

##### Автор GI фона с Нахидой: [Rafa](https://www.pixiv.net/en/users/3970196)

[![en](https://img.shields.io/badge/lang-en-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.md) [![jp](https://img.shields.io/badge/lang-jp-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.ja-jp.md) [![id](https://img.shields.io/badge/lang-id-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.id-id.md) [![pt](https://img.shields.io/badge/lang-pt-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.pt-pt.md) [![fr](https://img.shields.io/badge/lang-fr-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.fr-fr.md)

Изначально **Collapse** был создан для **Honkai Impact 3rd**. Однако по мере развития проекта лаунчер получил возможность запускать все выпущенные **игры miHoYo**.

[![Build-Canary](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml)
[![Qodana](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml/badge.svg)](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml)
[![Sync to Bitbucket](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml)
[![Upload to R2](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml)


[![Localization](https://img.shields.io/badge/Localization-Transifex-blue)](https://explore.transifex.com/collapse-launcher/collapse-mainapp/)
[![Discord](https://img.shields.io/badge/Join_Community-Discord-5865F2)](https://discord.gg/vJd2exaS7j)
[![KanbanBoard](https://img.shields.io/badge/Kanban_Board-Trello-white)](https://trello.com/b/rsqrnn15/collapse-launcher-tracker)

<p align="center">
    <a href="https://explore.transifex.com/collapse-launcher/collapse-mainapp/" target="_blank">
       <img src="https://upload.wikimedia.org/wikipedia/commons/f/f7/Transifex_logo.svg" alt="Collapse Launcher Localization at Crowdin" Width=280/>
	</a>
    &nbsp;
    <a href="https://discord.gg/vJd2exaS7j" target="_blank">
        <img src="https://discordapp.com/api/guilds/1116150300324139131/widget.png?style=banner2" alt="Discord Shield for Collapse Launcher Discord server" Width=280/>
    </a>
    &nbsp;
    <a href="https://trello.com/b/rsqrnn15/collapse-launcher-tracker" target="_blank">
        <img src="https://cdn.discordapp.com/attachments/593053443761897482/1137795596191797318/logo-gradient-white-trello.svg" alt="Collapse Launcher Trello board" Height=66/>
    </a>
</p>

# Почему "Collapse"?
Collapse произошёл от перевода **Honkai Impact** на китайский и японский языки. Слово произошло от [**崩坏**] или **Bēng huài** на китайском и [**崩壊**] или **Houkai** на японском, оба слова означают "**Collapse**", поэтому мы и выбрали его в качестве названия лаунчера, с дополнительным вдохновением, что в первую очередь это должен быть альтернативный (улучшенный) лаунчер для *Honkai Impact 3rd*.

**Collapse** имеет расширенные настройки для **Honkai Impact 3rd**, который не только обновляют и запускают игру, но и некоторые дополнительные функции, которых нет в оффициальном лаунчере, например: 
* Загрузка кэша (или же "Обновление настрок" в игре).
* Восстановление данных игры (требуется меньшая скорость интернета, по сравнению с внутриигровой функцией *Восстановления данных*).
* Изменение настроек игры (Настройки графики и звука) без надобности открытия.
* Поддержка мультирегионально загрузки игры (можно загружать и запускать разные версии игры).
* Конвертация Steam версии в глобальную (без надобности загрузки игры снова).
* Конвертация глобальной версии в SEA (или наоборот).
* Многосеансовая загрузка для ускорения скачивании/установки игры.
* Многопоточная распаковка игры для быстрой установки игры.
* Механизм обновления Delta-Patch для мелких обновлений ([**Нажмите сюда**](https://github.com/neon-nyan/CollapseLauncher/wiki/Update-Game-Region-with-Delta-Patch) для дополнительной информации).
* Выбор графического API (DirectX 11 (FL: 10.1, 11.0 и 11.1) и DirectX 12 [Может привести к вылету игры на новых этапах]).

Кроме того, лаунчер имеет расширенные настройки для **Genshin Impact**,включая, но не ограничиваясь ими:
* Выбор языка озвучки при первой установки, чтобы не загружать его через игру.
* Восстановление файлов игры, включая кэш игры, сцены, пакеты звуков и постоянные файлы вне игры.

# Поддерживаемые возможности 
<table>
  <thead>
    <tr>
      <th rowspan="2">Название игры</th>
      <th rowspan="2">Регион</th>
      <th colspan="7">Возможности</th>
    </tr>
    <tr>
      <th>Установка</th>
      <th>Предзагрузка</th>
      <th>Обновление</th>
      <th>Восстановление данных</th>
      <th>Обновление кэша</th>
      <th>Настройки игры</th>
      <th>Конвертация региона игры</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td rowspan="6">Honkai Impact 3rd</td>
      <td>Southeast Asia</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Доступны обычные и Delta-патчи)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (Из SEA -&gt; глобальный) </td>
    </tr>
    <tr>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Доступны обычные и Delta-патчи)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (Из Steam -&gt; глобальный) <br> (из глобального -&gt; SEA) </td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
    <tr>
      <td>TW/HK/MO</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
    <tr>
      <td>Japan</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
    <tr>
      <td>Korea</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
    <tr>
      <td rowspan="3">Genshin Impact</td>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
    <tr>
      <td>Bilibili</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
    <tr>
      <td rowspan="3">Honkai: Star Rail</td>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
    <tr>
      <td>Bilibili</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>Недоступно</td>
    </tr>
  </tbody>
</table>

> **Примичание**:
> Таблица выше служит для иллюстрации возможностей, доступные в Collapse сейчас. Этот список будет постоянно обновляться по мере добавления новых возможностей в Collapse и выхода новых игр. Если у вас возникли проблемы с какой-либо поддерживаемой функцией, проверьте вкладку GitHub с проблемами, сообщите, если не нашли открытых проблем для неё.
> > Пожалуйста, имейте в виду, что возможность конвертации региона сейчас доступна только для Honkai Impact: 3rd. Конвертация региона других игр miHoYo/Cognosphere Pte. Ltd. сейчас не в планах.

# Загрузить последний релиз
Для просмотра релизов [**нажмите сюда**](https://github.com/neon-nyan/CollapseLauncher/releases).

# Системный требования для использования
- Система: **Windows 10 1809 Update (build 17763)** или выше / **Windows 11 (любые сборки)**
- Архитектура: **x64/AMD64**
- Доступ в интернет: **Есть**

***

# Инструкция для копмиляции локально/разработки
> ### Больше информации Вы найдёте в [**Руководстве по внесению изменений**](https://github.com/neon-nyan/Collapse/blob/main/CONTRIBUTING.md)

***

# Сторонние репозитории и библиотеки, используемые в этом проекте
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) от Microsoft
- [**Windows App SDK**](https://github.com/microsoft/WindowsAppSDK) от Microsoft
- [**HDiffPatch**](https://github.com/sisong/HDiffPatch) от housisong
- [**Color Thief .NET**](https://github.com/neon-nyan/ColorThief) от KSemenenko
- [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) от adoconnection
- [**Hi3Helper.Http**](https://github.com/neon-nyan/Hi3Helper.Http) от neon-nyan
- [**Hi3Helper.EncTool**](https://github.com/neon-nyan/Hi3Helper.EncTool) от neon-nyan
- [**Crc32.NET**](https://github.com/neon-nyan/Crc32.NET) от force-net
- [**UABT**](https://github.com/neon-nyan/UABT) от _unknown_

**Отказ от ответственности**: Этот проект **НИ В КОЕМ СЛУЧАЕ НЕ СВЯЗАН** с [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) и [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) и является целиком открытым. Любой вклад приветствуется! 😃

# Обзор дизайна интерфейса
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# Чем я могу поддержать этот проект?
Поддержка не является обязательной, но очень ценится и мотивирует нас вкладывать больше времени в проект и поддерживать его и другие проекты. Если вы решили нас поддержать, вот способы которыми вы можете это сделать :smile::
- **[GitHub Sponsors](https://github.com/sponsors/neon-nyan)**
- **[QRIS (Quick Response Code Indonesian Standard)](https://qris.id/homepage/)** (Coming Soon:tm:)
- **[PayPal](https://paypal.me/neonnyan)**

Сделано всеми капитанами со всего мира с ❤️. Боритесь за всё прекрасное в этом мире!
