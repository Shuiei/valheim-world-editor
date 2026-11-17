<!-- source: 1000c3db -->
# Modifier un monde créé avec Expand World, Riverheim ou Tunnelheim

Installez le module complémentaire de l'éditeur pour votre mod : un monde créé avec ce mod s'ouvre, se dessine et
s'enregistre alors tel que le mod le crée.

**Il vous faut :** le mod installé dans un dossier BepInEx de cet ordinateur (votre Valheim, un profil de gestionnaire
de mods, ou une copie du dossier `BepInEx` d'un serveur), et le module complémentaire de la même version de l'éditeur.

| Module complémentaire | Pour | Ce qu'il ajoute |
|---|---|---|
| [Expand World](../../../addons/ExpandWorld/README.md) | Expand World Size, Rivers et Data | Le sol, la carte, les biomes, l'eau, les lieux, la végétation et les prévisions de Nouveau monde tels que les mods les créent |
| [Railroads](../../../addons/Railroads/README.md) | Railroads (`Railroads.dll`) | Dessine les voies, les gares et les trains du mod à partir de ses propres modèles ; les pièces que vous placez, collez, déplacez ou tournez fonctionnent dans le jeu, en direct aussi |
| [Riverheim](../../../addons/Riverheim/README.md) | Riverheim (`Riverheim.dll`) | Ouvre les mondes Riverheim (refusés sans lui), dessinés avec votre propre copie du mod ; Riverheim comme générateur de Nouveau monde |
| [Tunnelheim](../../../addons/Tunnelheim/README.md) | Tunnelheim (`ValheimTunneling.dll`) | Dessine les tunnels du mod, et un outil **Tunnel** (creusements, Add Earth, forages, salles, puits, escaliers) ; les tunnels suivent les copies et les plans |
| [ValheimRAFT](../../../addons/ValheimRAFT/README.md) | ValheimRAFT (`ValheimRAFT.dll`) | Dessine en entier les navires et les véhicules terrestres du mod, avec leurs voiles et leurs cordages ; chacun se déplace, se copie et se supprime d'un bloc |

1. Fermez l'éditeur.
2. Depuis la [page des versions](https://github.com/Shuiei/valheim-world-editor/releases/latest) de l'éditeur (ou
   Nexus Mods), téléchargez `ValheimWorldEditor-addon-<Name>-<version>.zip`.
3. Décompressez-le dans le dossier `addons` à côté de l'éditeur (sous macOS, à côté de `ValheimWorldEditor.app`), pour
   obtenir `addons/<Name>/worldeditor.json`.
4. Lancez l'éditeur et ouvrez **Paramètres** sur la page d'accueil.
5. Sous **Modules complémentaires**, trouvez le module. Il devrait indiquer « En cours d'exécution. » (ou « Pas encore
   lu » tant qu'aucun monde n'est ouvert). S'il indique « En attente de son mod : … n'est pas dans les dossiers de
   plugins ci-dessus. », ajoutez le dossier BepInEx qui contient le mod sous **Dossiers BepInEx**, puis
   **Enregistrer**.
6. Pour le monde d'un serveur ouvert depuis ses fichiers, ajoutez une copie du dossier `BepInEx` du serveur sous
   **Dossiers BepInEx**, pour que l'éditeur lise les réglages des mods du serveur.
7. Ouvrez le monde sous **Un monde enregistré**. Sa carte indique le mod (par exemple **Riverheim** ou « Expand
   World »).
8. Modifiez et enregistrez comme d'habitude. Avec Tunnelheim, choisissez **Tunnel** sous **MODS** dans la barre
   d'outils, choisissez une action, et cliquez sur le sol ou sur la roche.

## Vérifier le résultat

- Le monde s'ouvre, et son sol et sa carte ressemblent à ce qu'on voit en jeu (les rivières de Riverheim, la taille et
  les biomes d'Expand World).
- Le journal de l'éditeur (**Ouvrir le journal** sur la page d'accueil) indique ce que le module complémentaire a
  trouvé et lu.
- La carte d'un monde qui reste grisée dit pourquoi : voir [Un monde refuse de s'ouvrir ou est grisé](../help/troubleshooting.md#un-monde-refuse-de-souvrir-ou-est-grisé).

## Annuler

Chaque modification s'annule comme n'importe quelle autre (`Ctrl+Z`, Historique). Pour cesser d'utiliser un module
complémentaire, désactivez son interrupteur sous **Paramètres → Modules complémentaires** (son dossier reste), ou
supprimez son dossier de `addons`. Une fois désactivé, les mondes Expand World sont générés comme le jeu le fait sans
les mods, et les mondes Riverheim sont refusés.

## Bon à savoir

- Un module complémentaire n'est utilisé que tant que la DLL de son mod se trouve dans un dossier de plugins que
  l'éditeur connaît ; un mod qu'un gestionnaire de mods a désactivé, ou les réglages laissés par un mod retiré, ne
  comptent pas.
- Les pièces de Railroads sont dessinées à partir des fichiers de modèles que le mod garde dans son dossier `meshes` :
  gardez ce dossier à côté de `Railroads.dll`, comme le mod l'installe.
- Les fichiers de données d'Expand World demandent la bibliothèque YamlDotNet que le mod installe sous
  `BepInEx/plugins` ; sans elle, ils ne sont pas lus, et le journal le signale.
- Votre Riverheim doit connaître le préréglage du monde (mettez-le à jour s'il est plus ancien que celui qui a créé le
  monde). Riverheim 1.3 et suivants demandent Worldhalla à côté d'eux, comme en jeu. Les mondes créés avec la bêta
  obsolète de Riverheim ne peuvent pas être ouverts. En direct, votre Riverheim doit être la version dont ont besoin
  les joueurs qui rejoignent la partie (la même version depuis la 1.3, la même publication avant).
- Un monde Riverheim demande Riverheim dans le jeu de chaque joueur : les joueurs sur console en crossplay ne peuvent
  donc pas le rejoindre.
- En direct, les fichiers d'Expand World sont lus depuis le jeu lui-même via le plugin : voir [Mode direct](../live-mode.md).

## Voir aussi

- [Un nouveau monde](../new-world.md) : Riverheim et Expand World à la création d'un monde
- [Mode direct](../live-mode.md) : parties moddées, en direct
- [Ouvrir un monde qui n'est pas dans la liste](open-a-world.md)
