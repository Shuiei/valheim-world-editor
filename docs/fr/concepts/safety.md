<!-- source: 4978ef09 -->
# Sécurité et vie privée

L'éditeur est un programme sur votre ordinateur. Il n'écrit dans vos mondes que lorsque vous appuyez sur
**Enregistrer dans le monde**, n'envoie rien nulle part sauf si vous le connectez à un serveur ou à Claude, et garde
les secrets hors de son journal. Cette page dit ce qu'il écrit, ce qui peut quitter votre ordinateur, et ce qui
s'exécute avec vos droits.

## Ce qu'écrit l'éditeur

**Vos mondes**, seulement quand vous enregistrez. Jusque-là, chaque modification est en attente et vit dans la mémoire
de l'éditeur (voir [Modifications en attente](saving-and-history.md#modifications-en-attente)). Un enregistrement ajoute une
nouvelle sauvegarde numérotée dans le dossier du monde et ne retire rien d'ancien avant d'avoir relu correctement la
nouvelle (voir [Enregistrer un monde](saving-and-history.md#enregistrer-un-monde)). Un nouveau monde va dans un nouveau
dossier, jamais par-dessus un monde du même nom. Fermez d'abord le jeu : un jeu en cours écrit par-dessus les fichiers
que l'éditeur a enregistrés.

**Un jeu en direct**, seulement quand vous appliquez. Le jeu sauvegarde alors lui-même les modifications, comme il
sauvegarde ce que construisent les joueurs.

**Vos plans** : un fichier `.blueprint` et son image `.png` dans le dossier de Homestead, dans le dossier de sauvegarde
de Valheim, quand vous enregistrez un plan. **Supprimer** retire les deux, après avoir demandé.

**Son propre dossier de données** : `~/.local/share/ValheimWorldEditor` sous Linux,
`%LOCALAPPDATA%\ValheimWorldEditor` sous Windows, `~/Library/Application Support/ValheimWorldEditor` sous macOS (le
bas du menu de la page d'accueil l'affiche). Il contient :

| Quoi | Fichiers |
|---|---|
| Vos paramètres et vos choix | `settings.json`, `prefs.json`, `place.json` (préréglages de Placement, favoris, types récents), `stamps.json`, `selections.json` |
| Serveurs enregistrés | `servers.cfg` : adresse, utilisateur, jeton, l'identité du serveur, et le mot de passe quand vous avez coché **Enregistrer le mot de passe** (voir plus bas) |
| Historique | `history/`, un fichier par monde (voir [Historique](saving-and-history.md#historique)) |
| Votre travail | `scripts/` (scripts enregistrés), `heightmaps/` (cartes de hauteur exportées), `blueprints/export` (fichiers `.vbuild`) |
| Ce qu'il a appris de votre jeu | `game-index.json` et d'autres fichiers `game-*.json` (où se trouvent les choses dans les fichiers de Valheim), `mod-catalogue.json` (objets des mods vus dans un jeu en direct), `mod-bundles/` (modèles des mods, copiés depuis leurs DLL) |
| Le journal | `ValheimWorldEditor.log`, un nouveau à chaque lancement ; `perf-native.log` seulement tant que **Enregistrer la fréquence d'images** est activé |

**Ce qu'il ne modifie jamais.** L'éditeur lit votre dossier du jeu Valheim, vos dossiers BepInEx et les fichiers de vos
mods (modèles, textures, règles du jeu, paramètres du plugin) et n'y écrit rien. Il lit le code des mods pour
apprendre le nom de leurs valeurs, et ne l'exécute jamais.

**Mots de passe.** Un mot de passe enregistré est gardé en clair dans `servers.cfg`. Sous Linux, seul vous pouvez lire
le fichier ; préférez tout de même une clé SSH quand vous le pouvez. La phrase de passe d'une clé n'est jamais
enregistrée. **oublier** à côté d'un serveur enregistré le retire, avec son mot de passe et son jeton.

## Rien ne quitte votre ordinateur

L'éditeur n'a ni compte, ni suivi, ni vérification de mises à jour. Il ne communique avec l'extérieur que dans les cas
ci-dessous, chacun étant votre choix :

| Quand | Ce qui va où |
|---|---|
| Vous vous connectez à un **serveur dédié** | Une connexion SSH, chiffrée, vers ce serveur seulement, avec le compte que vous donnez. Le plugin est joint à travers elle, jamais par l'internet ouvert (voir [Comment il se connecte](live-editing.md#comment-il-se-connecte)). |
| Vous modifiez **votre propre jeu** en direct | Rien ne quitte l'ordinateur : l'éditeur parle au plugin sur `127.0.0.1`. |
| Vous connectez **Claude** | L'application Claude sur votre ordinateur parle à l'éditeur sur `127.0.0.1`. Ce que répond l'éditeur (les données du secteur, des images de la vue 3D) va à Claude, comme tout ce que vous donnez à Claude. |
| Vous cliquez sur un lien | **Obtenir Homestead**, ou une page d'aide ouverte sur GitHub, s'ouvre dans votre navigateur web. |

**Le plugin reste privé.** WorldEditorBridge n'écoute que sur l'ordinateur où il tourne, et ne répond qu'aux requêtes
qui portent son jeton secret. Pour un serveur, vous tapez vous-même le jeton ; l'éditeur ne le lit jamais sur le
serveur, donc une connexion SSH seule ne peut pas modifier le monde. Gardez le jeton privé : quiconque le possède et a
accès au port peut modifier le monde. Videz la ligne `Token` de `Tie.WorldEditorBridge.cfg` et redémarrez le jeu pour
en obtenir un nouveau.

**L'identité du serveur est vérifiée.** L'éditeur retient l'identité d'un serveur à la première connexion. Si elle
change un jour, l'éditeur refuse de se connecter, parce que quelqu'un pourrait se faire passer pour votre serveur.
Après avoir réinstallé un serveur, videz sa ligne `HostKey` dans `servers.cfg` pour accepter la nouvelle.

## Scripts et Claude

**Les scripts sont des programmes.** Un [script](../scripting.md) est du code C#, et l'éditeur l'exécute avec vos
droits, comme tout programme que vous lancez. Il tourne dans un processus séparé pour que **Arrêter** puisse toujours
l'arrêter, mais ce processus n'est pas un bac à sable : un script peut lire et écrire vos fichiers et faire tout ce que
votre compte peut faire. N'exécutez que des scripts que vous avez écrits ou en lesquels vous avez confiance, comme vous
le feriez avec un mod.

**Claude travaille pour vous, dans des limites.** Avec **Autoriser Claude à se connecter** activé dans les Paramètres,
[Claude](../claude.md) peut utiliser les outils de l'éditeur. L'éditeur l'y tient ainsi :

- **Cet ordinateur seulement, avec un jeton.** L'éditeur n'écoute que sur `127.0.0.1`, refuse les requêtes adressées à
  un autre nom (pour qu'une page web ne puisse pas le joindre sous une adresse inventée), et refuse toute requête sans
  son jeton (48 caractères aléatoires). Une autre **Adresse** dans les Paramètres (`0.0.0.0`, ou l'une de celles de cet
  ordinateur) permet à d'autres ordinateurs de ce réseau de se connecter, avec le jeton, qui circule alors sans
  chiffrement : seulement des ordinateurs ayant une adresse locale sur le propre réseau de cet ordinateur, jamais ceux
  d'internet, et jamais les scripts (ils restent réservés au Claude de cet ordinateur). **Nouveau jeton** en crée un
  autre ; désactivez l'interrupteur et plus rien ne peut se connecter.
- **En attente seulement.** Tout ce que Claude modifie reste en attente, une étape chacune, marquée **Claude** dans
  l'Historique. Claude n'enregistre jamais, n'applique pas en direct, n'abandonne pas, ne recharge pas, n'écrit ni ne
  supprime vos fichiers, et ne se connecte pas à un jeu en direct : tout cela reste à vous. Il n'ouvre pas un autre
  monde tant que le vôtre a des modifications non enregistrées.
- **Rien en direct avec Auto activé.** Tant qu'un monde en direct a **Auto** activé, chaque modification atteindrait
  aussitôt les joueurs, donc Claude ne modifie rien et vous demande de désactiver Auto.
- **Pas de scripts sans votre accord.** Les scripts de Claude sont refusés tant que vous n'activez pas
  **Claude peut exécuter des scripts** dans les Paramètres (désactivé par défaut). Même alors, chaque script qu'écrit
  Claude vous est montré avant de s'exécuter, pour l'exécuter ou le refuser : un texte dans un monde (un panneau, une
  étiquette écrite par un autre joueur) pourrait pousser Claude à en écrire un nuisible. Ses autres outils exécutent le
  propre code de l'éditeur et ne demandent pas.
- **Facile à annuler.** **Annuler les modifications de Claude** dans l'Historique retire chaque étape faite par Claude
  dans le secteur ouvert et garde les vôtres (voir [Historique](saving-and-history.md#historique)).

## Extensions et modules complémentaires des mods

Un mod peut fournir une **extension de l'éditeur** : un petit morceau de code que l'éditeur charge pour lire et
afficher les objets de ce mod. Comme un script, elle s'exécute avec vos droits, donc c'est vous qui décidez. Chaque
extension est **désactivée** jusqu'à ce que vous l'activiez dans **Paramètres → Extensions de mods**, et seulement ce
fichier précis : l'éditeur retient son empreinte (SHA-256), donc un fichier modifié, comme une mise à jour du mod, est
de nouveau désactivé jusqu'à ce que vous l'activiez à nouveau.

Les **Modules complémentaires**, que vous téléchargez et décompressez vous-même dans le dossier `addons` de l'éditeur,
s'exécutent tels qu'installés, puisque en installer un est déjà votre choix. **Paramètres → Modules complémentaires**
désactive n'importe lequel d'entre eux.

## Ce que le journal laisse de côté

L'éditeur écrit ce qu'il fait, et chaque erreur qu'il gère, dans `ValheimWorldEditor.log`. **Ouvrir le journal**, en
bas du menu de la page d'accueil, l'ouvre, pour que vous puissiez le joindre à un rapport de bug. Avant qu'une ligne
soit écrite, les données personnelles en sont retirées :

- votre dossier personnel est écrit `~`, et les autres chemins de fichiers, adresses web, noms `user@host`, adresses IP
  et de serveurs et votre nom de compte sont remplacés par des marqueurs comme `<path>` ;
- les comptes, mots de passe, phrases de passe et jetons des serveurs enregistrés n'y arrivent jamais.

Le plugin WorldEditorBridge écrit dans le propre `BepInEx/LogOutput.log` du jeu, avec le dossier personnel du serveur
écrit `~` lui aussi.

Lisez tout de même un journal avant de le publier. Et gardez vos propres copies des mondes auxquels vous tenez (voir
[Sauvegarder un monde avant de le modifier](../guides/back-up-a-world.md)).
