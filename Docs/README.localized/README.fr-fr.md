
<p align="center">
  <img src="https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/NewBannerv2_color.webp"/>
</p>

##### Crédits pour l'arrière-plan Nahida de GI : [Rafa sur Pixiv](https://www.pixiv.net/en/users/3970196)

[![en](https://img.shields.io/badge/README-en-red.svg)](../../README.md)
[![de](https://img.shields.io/badge/README-de-red.svg)](README.de-de.md)
[![fr](https://img.shields.io/badge/README-fr-red.svg)](README.fr-fr.md)
[![id](https://img.shields.io/badge/README-id-red.svg)](README.id-id.md)
[![jp](https://img.shields.io/badge/README-jp-red.svg)](README.ja-jp.md)
[![pt](https://img.shields.io/badge/README-pt-red.svg)](README.pt-pt.md)
[![ru](https://img.shields.io/badge/README-ru-red.svg)](README.ru-ru.md)
[![zh](https://img.shields.io/badge/README-zh-red.svg)](README.zh-cn.md)

**Collapse** a été conçu à l'origine pour **Honkai Impact 3rd**. Cependant, avec l'évolution du projet, ce lanceur est maintenant un client de jeu pour tous les **jeux miHoYo** actuellement disponibles.

[![Build-Canary](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/neon-nyan/Collapse/actions/workflows/build.yml)
[![Qodana](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml/badge.svg)](https://github.com/CollapseLauncher/Collapse/actions/workflows/qodana-scan.yml)
[![Synchroniser avec Bitbucket](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/sync-to-bitbucket.yml)
[![Téléverser vers R2](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml/badge.svg)](https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/actions/workflows/upload-to-r2.yml)


[![Localisation](https://img.shields.io/badge/Localisation-Transifex-blue)](https://explore.transifex.com/collapse-launcher/collapse-mainapp/)
[![Discord](https://img.shields.io/badge/Rejoins_la_communauté-Discord-5865F2)](https://discord.gg/vJd2exaS7j)
[![Tableau Kanban](https://img.shields.io/badge/Tableau_Kanban-Trello-white)](https://trello.com/b/rsqrnn15/collapse-launcher-tracker)

<p align="center">
    <a href="https://explore.transifex.com/collapse-launcher/collapse-mainapp/" target="_blank">
       <img src="https://upload.wikimedia.org/wikipedia/commons/f/f7/Transifex_logo.svg" alt="Localisation du launcher Collapse" Width=280/>
	</a>
    &nbsp;
    <a href="https://discord.gg/vJd2exaS7j" target="_blank">
        <img src="https://discordapp.com/api/guilds/1116150300324139131/widget.png?style=banner2" alt="Bannière du serveur Discord de Collapse Launcher" Width=280/>
    </a>
    &nbsp;
    <a href="https://trello.com/b/rsqrnn15/collapse-launcher-tracker" target="_blank">
        <img src="https://cdn.discordapp.com/attachments/593053443761897482/1137795596191797318/logo-gradient-white-trello.svg" alt="Tableau Trello Collapse Launcher" Height=66/>
    </a>
</p>

# Pourquoi "Collapse" ?
Collapse est issu de la traduction d'**Honkai Impact** en chinois et en japonais. Le mot vient de [**崩坏**] ou **Bēng huài** en chinois et aussi de [**崩壊**] ou **Houkai** en japonais, les deux signifiant "**Collapse**". C'est pourquoi nous l'avons choisi comme nom de notre lanceur avec l'inspiration supplémentaire qu'il était censé être un lanceur alternatif (amélioré) pour *Honkai Impact 3rd* en premier lieu.

**Collapse** contient des fonctionnalités avancées pour **Honkai Impact 3rd** qui vous permettent non seulement de télécharger et de lancer le jeu, mais aussi des fonctionnalités supplémentaires que le Launcher officiel n'a pas, y compris :
* Téléchargement des caches (également appelé "Mise à jour des paramètres" dans le jeu).
* Réparation des données du jeu (avec moins de bande passante que la fonction *Réparation des données* dans le jeu).
* Modification des paramètres du jeu (paramètres graphiques et paramètres audio) sans ouvrir le jeu.
* Prise en charge du téléchargement de jeux multirégionaux (permet de télécharger et de lancer différentes versions du jeu).
* Conversion de la version Steam en version globale (sans avoir à retélécharger l'ensemble du jeu).
* Conversion de la version globale en version ASE (ou vice versa).
* Téléchargement multi-session pour un téléchargement/une installation plus rapide du jeu.
* Extraction de jeu multi-thread pour une installation plus rapide.
* Mécanisme de mise à jour au format Delta pour les petites mises à jour ([**Cliquez ici**](https://github.com/neon-nyan/CollapseLauncher/wiki/Update-Game-Region-with-Delta-Patch) pour plus d'informations).
* Possibilité de sélectionner l'API graphique (DirectX 11 (FL : 10.1, 11.0 et 11.1) et DirectX 12 [Peut faire planter le jeu dans les phases plus récentes]).

En plus de cela, ce lanceur possède également des fonctionnalités avancées pour **Genshin Impact**, qui permet, sans s'y restreindre, de:
* Choisir la langue des dialogues lors de la première installation, afin de ne pas avoir à la télécharger à l'intérieur du jeu.
* Réparer les fichiers du jeu, y compris les caches du jeu, les scènes, le pack audio et les fichiers persistants en dehors du jeu.

# Fonctionnalités prises en charge 
<table>
  <thead>
    <tr>
      <th rowspan="2">Titre du jeu</th>
      <th rowspan="2">Région</th>
      <th colspan="7">Fonctionnalités</th>
    </tr>
    <tr>
      <th>Installation</th>
      <th>Préchargement</th>
      <th>Mise à jour</th>
      <th>Réparation du jeu</th>
      <th>Mise à jour du cache</th>
      <th>Paramètres du jeu</th>
      <th>Conversion des régions de jeu</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td rowspan="6">Honkai Impact 3rd</td>
      <td>Asie du Sud-Est</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Mises à jour normales et au format Delta disponibles)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (De ASE -&gt Global) </td>
    </tr>
    <tr>
      <td>Global</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: (Mises à jour normales et au format Delta disponibles)</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark: <br> (De Steam -&gt; Global) <br> (De Global -&gt; ASE) </td>
    </tr>
    <tr>
      <td>Chine continentale</td>
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
      <td>Japon</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>:white_check_mark:</td>
      <td>N/A</td>
    </tr>
    <tr>
      <td>Corée</td>
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
      <td>Chine continentale</td>
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
      <td>Chine continentale</td>
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

> **Note** :
> Le tableau ci-dessus sert à illustrer les fonctionnalités que Collapse prend actuellement en charge. Cette liste sera continuellement mise à jour au fur et à mesure que de nouvelles fonctionnalités seront ajoutées à Collapse et que de nouveaux jeux sortiront. Si vous avez des problèmes avec l'une des fonctionnalités supportées ci-dessus, vérifiez notre onglet GitHub Issues, si aucun problème actif n'est trouvé pour cette fonctionnalité, créez une Issue.
> Veuillez garder à l'esprit que la fonction de conversion de jeu n'est actuellement disponible que pour Honkai Impact 3rd. Les autres jeux miHoYo/Cognosphere Pte. Ltd. ne sont pas prévus pour l'instant.

# Télécharger les versions prêtes à l'emploi
Pour voir toutes les versions, [**cliquez ici**](https://github.com/neon-nyan/CollapseLauncher/releases).

# Configuration requise pour la version prête à l'emploi
- OS : **Windows 10 Mise à jour 1809 (build 17763)** ou ultérieur / **Windows 11 (Tous builds)**.
- Architecture : **x64/AMD64**
- Accès à Internet : **Oui**

***

# Conditions préalables à la construction locale/au développement
> ### Plus d'informations peuvent être trouvées dans [**Directives de Contribution**](https://github.com/neon-nyan/Collapse/blob/main/CONTRIBUTING.md)

***

# Dépôts et bibliothèques tiers utilisés dans ce projet
- [**Windows UI Library**](https://github.com/microsoft/microsoft-ui-xaml) par Microsoft
- [**Windows App SDK**](https://github.com/microsoft/WindowsAppSDK) par Microsoft
- [**HDiffPatch**](https://github.com/sisong/HDiffPatch) par housisong
- [**Color Thief .NET**](https://github.com/neon-nyan/ColorThief) par KSemenenko
- [**SevenZipExtractor**](https://github.com/neon-nyan/SevenZipExtractor) par adoconnection
- [**Hi3Helper.Http**](https://github.com/neon-nyan/Hi3Helper.Http) par neon-nyan
- [**Hi3Helper.EncTool**](https://github.com/neon-nyan/Hi3Helper.EncTool) par neon-nyan
- [**Crc32.NET**](https://github.com/neon-nyan/Crc32.NET) par force-net
- [**UABT**](https://github.com/neon-nyan/UABT) par _inconnu_

**Disclaimer** : Ce projet **N'EST PAS AFFILIÉ** à [**miHoYo (miHoYo Co., Ltd.)**](https://www.mihoyo.com/) ou [**HoYoverse (COGNOSPHERE PTE. LTD.)**](https://www.hoyoverse.com/en-us) par quelque moyen que ce soit et est complètement libre. Toutes les contributions sont les bienvenues ! 😃

# Aperçu de la conception de l'interface utilisateur
![](https://raw.githubusercontent.com/neon-nyan/CollapseLauncher-Page/main/images/UI%20Overview%20RC2.webp)

# Comment puis-je soutenir ce projet ?
Soutenir n'est jamais une obligation mais est toujours apprécié et nous motive à investir plus de temps dans le projet et à garder ce projet et d'autres projets en vie. À cette fin, si vous décidez de nous soutenir, voici comment vous pouvez le faire :smile: :
- **[Sponsors GitHub](https://github.com/sponsors/neon-nyan)**
- **[PayPal](https://paypal.me/neonnyan)**

Réalisé par tous les capitaines du monde entier avec ❤️. Luttez pour tout ce qui est beau dans ce monde !
