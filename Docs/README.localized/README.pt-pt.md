
<p align="center">
  <img src="https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/NewBannerv2_color.webp"/>
</p>

##### Créditos do papel de fundo da Nahida de GI: [Rafa on Pixiv](https://www.pixiv.net/en/users/3970196)

[![en](https://img.shields.io/badge/README-en-red.svg)](../../README.md)
[![de](https://img.shields.io/badge/README-de-red.svg)](README.de-de.md)
[![fr](https://img.shields.io/badge/README-fr-red.svg)](README.fr-fr.md)
[![id](https://img.shields.io/badge/README-id-red.svg)](README.id-id.md)
[![jp](https://img.shields.io/badge/README-jp-red.svg)](README.ja-jp.md)
[![pt](https://img.shields.io/badge/README-pt-red.svg)](README.pt-pt.md)
[![ru](https://img.shields.io/badge/README-ru-red.svg)](README.ru-ru.md)
[![zh](https://img.shields.io/badge/README-zh-red.svg)](README.zh-cn.md)

O **Collapse** foi originalmente desenhado para **Honkai Impact 3rd**. No entanto, com a evolução do projeto, o launcher tornou-se numa aplicação para todos os **jogos da miHoYo/Hoyoverse** atualmente lançados.

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

O **Collapse** contêm funcionalidades avançadas para **Honkai Impact 3rd** que não te permitem apenas descarregar e abrir o jogo como também efetuar algumas operações adicionais não presentes no launcher oficial, tais como:
* Descarregar daches (ou seja "Updating Settings" dentro do jogo).
* Reparar dados corrumpidos (com um uso de banda menor que a funcionalidade *Data Repair* dentro do jogo).
* Mudar as definições do jogo (Definições gráficas e de áudio) sem a necessidade de abrir o jogo.
* Suporte para o descarregamento de várias regiões (Permite descarregar e iniciar diversas versões do jogo).
* Conversão da versão Steam para a Global (sem ser necessário redescarregar o jogo inteiro).
* Conversão da versão Global para a SEA (ou vice versa).
* Descarregamentos multi-sessão para um descarregamento e uma instalação mais rápidas.
* Extração de jogo multi-thread para uma instalação mais rápida.
* Mecanismo de atualizações em formato Delta para reduzir o tamanho das atualizações ([**Clica aqui**](https://github.com/neon-nyan/CollapseLauncher/wiki/Update-Game-Region-with-Delta-Patch) para mais informações).
* Possibilidade de escolher o API de gráficos (DirectX 11 (FL: 10.1, 11.0 and 11.1) ou DirectX 12 [Em fases mais recentes, o jogo poderá fechar inesperadamente]).

Para além disto, este launcher também possui algumas funcionalidades avançadas para **Genshin Impact**, incluindo mas não limitadas a:
* Escolher a linguagem das vozes durante a primeira instalação, para não teres de o fazer dentro do jogo.
* Reparar ficheiros do jogo, incluindo caches, cutscenes, pacotes de áudio e ficheiros persistentes fora do jogo.

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
> A tabela acima serve para ilustrar as funcionalidades que o Collapse suporta no momento. Esta lista é atualizada gradualmente conforme a adição de novas funcionalidades ao Collapse e o lançamento de novos jogos. Se tens algum problema com alguma das funcionalidades suportadas, verifica a aba de Issues do GitHub e cria uma "Issue" caso nenhuma sobre o mesmo assunto exista.
> > Por favor lembra-te que a conversão de região apenas está disponível para o Honkai Impact 3rd. Não temos planos de implementar esta funcionalidade para outros jogos da miHoYo/Cognosphere Pte. Ltd. 

# Descarregar compilações prontas a usar
[**Clica aqui**](https://github.com/neon-nyan/CollapseLauncher/releases) para aceder à página de lançamentos e descarregar o Collapse.

# Requisitos de sistema para as compilações prontas a usar
- Sistema operativo: **Windows 10 versão 1809 (Compilação 17763)** ou superior / **Windows 11 (Qualquer compilação)**
- Arquitetura: **x64/AMD64**
- Acesso à Internet: **Sim**

***

# Pré-requisitos para compilar localmente/desenvolvimento
> ### Mais informação pode ser encontrada nas [**Diretrizes de contribuição**](https://github.com/neon-nyan/Collapse/blob/main/CONTRIBUTING.md)

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

**Declaração**: Este projeto **NÃO ESTÁ AFILIADO** de qualquer forma com a [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) nem com a [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) e é completamente fonte-aberta (open source). Quaisquer contribuições são bem-vindas! 😃

# Visão geral sobre o design da UI
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# Como posso apoiar este projeto?
Apoiar nunca será uma obrigação mas é sempre apreciado e motiva-nos a investir mais tempo no projeto e a manter este e outros projetos ativos. Para este fim, se decidires apoiar-nos, aqui estão algumas formas de como o podes fazer :smile::
- **[GitHub Sponsors](https://github.com/sponsors/neon-nyan)**
- **[PayPal](https://paypal.me/neonnyan)**

Feito por todos os capitães à volta do mundo com ❤️. Luta por tudo o que é belo neste mundo!
