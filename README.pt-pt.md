
<p align="center">
  <img src="https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/NewBannerv2_color.webp"/>
</p>

##### Créditos do papel de fundo da Nahida de GI: [Rafa on Pixiv](https://www.pixiv.net/en/users/3970196)

[![en](https://img.shields.io/badge/README-en-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.md) [![jp](https://img.shields.io/badge/README-jp-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.ja-jp.md) [![id](https://img.shields.io/badge/README-id-red.svg)](https://github.com/neon-nyan/Collapse/blob/main/README.id-id.md)

O **Collapse** foi desenhado originalmente para o **Honkai Impact 3rd**. No entanto, com a evolução do projeto, o launcher tornou-se numa aplicação para todos os **jogos da miHoYo/Hoyoverse** atualmente lançados.

[![Build-Canary](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml)
[![Qodana](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml/badge.svg)](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml)
[![Sync to Bitbucket](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml)
[![Upload to R2](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml)


[![Localização](https://img.shields.io/badge/Localização-Transifex-blue)](https://explore.transifex.com/collapse-launcher/collapse-mainapp/)
[![Discord](https://img.shields.io/badge/Junta--te_à_comunidade-Discord-5865F2)](https://discord.gg/vJd2exaS7j)
[![KanbanBoard](https://img.shields.io/badge/Kanban_Board-Trello-white)](https://trello.com/b/rsqrnn15/collapse-launcher-tracker)

<p align="center">
    <a href="https://explore.transifex.com/collapse-launcher/collapse-mainapp/" target="_blank">
       <img src="https://upload.wikimedia.org/wikipedia/commons/f/f7/Transifex_logo.svg" alt="Ajuda a traduzir o Collapse Launcher no Transifex" Width=280/>
	</a>
    &nbsp;
    <a href="https://discord.gg/vJd2exaS7j" target="_blank">
        <img src="https://discordapp.com/api/guilds/1116150300324139131/widget.png?style=banner2" alt="Servidor de Discord do Collapse Launcher" Width=280/>
    </a>
    &nbsp;
    <a href="https://trello.com/b/rsqrnn15/collapse-launcher-tracker" target="_blank">
        <img src="https://cdn.discordapp.com/attachments/593053443761897482/1137795596191797318/logo-gradient-white-trello.svg" alt="Quadro do Trello do Collapse Launcher" Height=66/>
    </a>
</p>

# Porquê "Collapse"?
Collapse vem da tradução de **Honkai Impact** em Chinês e Japonês. A palavra origina dos vocábulos [**崩坏**] ou **Bēng huài** do Chinês e [**崩壊**] or **Houkai** do Japonês, os quais ambos significam "**Collapse**" ou Colapso em português. É por esta causa que decidimos escolher este nome para o nosso launcher, com inspiração no aspeto de que originalmente este projeto era suposto ser uma alternativa (melhorada) ao launcher do *Honkai Impact 3rd*.

O **Collapse** contêm funcionalidades avançadas para o **Honkai Impact 3rd** que não te permitem apenas descarregar e abrir o jogo como também efetuar algumas operações adicionais não presentes no launcher oficial, tais como:
* Descarregar Caches (ou seja "Updating Settings" dentro do jogo).
* Game Data Repair (with less bandwidth required than the *Data Repair* feature in-game).
* Changing Game Settings (Graphics Settings & Audio Settings) without opening the game.
* Multi-Region Game Download Support (Allows for downloading and launching different versions of the game).
* Steam to Global version conversion (without re-downloading the whole game).
* Global to SEA version (or vice versa) conversion.
* Multi-session Download for faster Game Download/Installation.
* Multi-thread Game Extraction for faster installation.
* Delta-Patch Update Mechanism for smaller updates ([**Click here**](https://github.com/neon-nyan/CollapseLauncher/wiki/Update-Game-Region-with-Delta-Patch) for more info).
* Ability to select Graphics API (DirectX 11 (FL: 10.1, 11.0 and 11.1) and DirectX 12 [May crash the game in newer stages]).

Not only that, this launcher also has some advanced features for **Genshin Impact**, including but not limited to:
* Choosing your voice-line language during the first installation, so you don't have to download it inside the game.
* Repair the game files including Game Caches, Cutscenes, Audio Pack & persistent files outside of the game.

# Funcionalidades suportadas 
<table>
  <thead>
    <tr>
      <th rowspan="2">Título do jogo</th>
      <th rowspan="2">Região</th>
      <th colspan="7">Funcionalidades</th>
    </tr>
    <tr>
      <th>Instalação</th>
      <th>Pré-carregamento</th>
      <th>Atualização</th>
      <th>Reparação do jogo</th>
      <th>Atualização das caches</th>
      <th>Definições do jogo</th>
      <th>Conversão da região do jogo</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td rowspan="6">Honkai Impact 3rd</td>
      <td>Southeast Asia</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Atualizações normais ou em formato Delta disponíveis)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (De SEA -&gt; Global) </td>
    </tr>
    <tr>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Atualizações normais ou em formato Delta disponíveis)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (De Steam -&gt; Global) <br> (De Global -&gt; SEA) </td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>TW/HK/MO</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Japan</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Korea</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td rowspan="2">Genshin Impact</td>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td rowspan="2">Honkai: Star Rail</td>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Mainland China</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
  </tbody>
</table>

> **Nota**:
> A tabela acima serve para ilustrar as funcionalidades que o Collapse suporta no momento. Esta lista é atualizada gradualmente conforme a adição de novas funcionalidades ao Collapse e o lançamento de novos jogos. Se tens algum problema com alguma das funcionalidades suportadas, verifica a aba de Issues do GitHub e cria um "Issue" caso nenhum sobre o mesmo assunto exista.
> > Por favor lembra-te que a conversão de região apenas está disponível para o Honkai Impact: 3rd. Não temos planos de implementar esta funcionalidade para outros jogos da miHoYo/Cognosphere Pte. Ltd. 

# Descarrega compilações prontas a usar
[<img src="https://user-images.githubusercontent.com/30566970/172445052-b0e62327-1d2e-4663-bc0f-af50c7f23615.svg" width="320"/>](https://github.com/neon-nyan/Collapse/releases/download/CL-v1.71.13/CL-1.71.13_Installer.exe)
> **Nota**: A última versão para esta compilação é a `1.71.13` (Lançada em: 2 de outubro de 2023).

[<img src="https://user-images.githubusercontent.com/30566970/172445153-d098de0d-1236-4124-8e13-05000b374eb6.svg" width="320"/>](https://github.com/CollapseLauncher/Collapse/releases/download/v1.72.4-pre/CL-1.72.4-preview_Installer.exe)
> **Nota**: A última versão para esta compilação é a `1.72.4` (Lançada em: 9 de outubro de 2023).

Para ver todos os lançamentos, [**clica aqui**](https://github.com/neon-nyan/CollapseLauncher/releases).

# Requisitos de sistema para as compilações prontas a usar
- Sistema operativo: **Windows 10 versão 1809 (Compilação 17763)** ou superior / **Windows 11 (Qualquer compilação)**
- Arquitetura: **x64/AMD64**
- Acesso à Internet: **Sim**

***

# Pré-requisitos para compilar localmente/desenvolvimento
> ### Mais informação pode ser encontrada em [**Contribution Guidelines**](https://github.com/neon-nyan/Collapse/blob/main/CONTRIBUTING.md)

***

# Repositórios de terceiros e bibliotecas usadas neste projeto
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) por Microsoft
- [**Windows App SDK**](https://github.com/microsoft/WindowsAppSDK) por Microsoft
- [**HDiffPatch**](https://github.com/sisong/HDiffPatch) por housisong
- [**Color Thief .NET**](https://github.com/neon-nyan/ColorThief) por KSemenenko
- [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) por adoconnection
- [**Hi3Helper.Http**](https://github.com/neon-nyan/Hi3Helper.Http) por neon-nyan
- [**Hi3Helper.EncTool**](https://github.com/neon-nyan/Hi3Helper.EncTool) por neon-nyan
- [**Crc32.NET**](https://github.com/neon-nyan/Crc32.NET) por force-net
- [**UABT**](https://github.com/neon-nyan/UABT) por _unknown_

**Disclaimer**: Este projeto **NÃO ESTÁ AFILIADO** de qualquer forma com a [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) nem com a [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) e é completamente fonte-aberta (open source). Quaisquer contribuições são bem-vindas! 😃

# Visão geral sobre o design da UI
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# How can I support this project?
Supporting is never an obligation but is always appreciated and motivates us to invest more time in the project and to keep this and other projects alive. To this end, should you decide to support us, here are ways you can do so :smile::
- **[GitHub Sponsors](https://github.com/sponsors/neon-nyan)**
- **[QRIS (Quick Response Code Indonesian Standard)](https://qris.id/homepage/)** (Em breve:tm:)
- **[PayPal](https://paypal.me/neonnyan)**

Feito por todos os capitães à volta do mundo com ❤️. Luta por tudo o que é belo neste mundo!
