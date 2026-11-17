<!-- source: 93850bba -->
# Questions

Des réponses courtes aux questions les plus fréquentes, chacune avec un lien vers la réponse complète.

## Est-ce sans danger pour mon monde ?

Oui, si vous en gardez une copie. Rien n'est écrit avant que vous appuyiez sur **Enregistrer dans le monde** ou
**Appliquer en direct**, et chaque enregistrement est relu et vérifié : s'il ne se relit pas correctement, les nouveaux
fichiers sont retirés et le monde est tel qu'il était. Copiez quand même un monde auquel vous tenez avant votre premier
enregistrement : voir [Sauvegarder un monde avant de le modifier](../guides/back-up-a-world.md)
et [Ce qu'écrit l'éditeur](../concepts/safety.md#ce-quécrit-léditeur).

## Puis-je annuler après avoir enregistré ?

Oui. L'historique est gardé avec le monde après chaque enregistrement ou application, même après la fermeture de
l'éditeur : annulez une étape (ou retirez-la avec **Retirer** dans l'Historique) et enregistrez à nouveau. Voir
[Annuler une étape, retirer une modification ou tout abandonner](../guides/undo-changes.md). Les réinitialisations de
zones et le sol Sans limite le gardent aussi ; ce qu'une réinitialisation a recréé est hors de sa portée (voir
[Ce qu'une étape ne peut pas annuler](../concepts/saving-and-history.md#ce-quune-étape-ne-peut-pas-reprendre)).

Pour des dégâts découverts plus tard, [Restaurer un secteur depuis une sauvegarde](../guides/restore-from-backup.md).

## Les joueurs de mon serveur ont-ils besoin de quelque chose ?

Non. Seul le jeu ou le serveur qui héberge le monde a besoin de BepInEx et du plugin, et seulement pour la
modification en direct. Les joueurs qui le rejoignent n'ont besoin de rien, y compris pour le sol élevé au-delà de la
limite de 8 m du jeu. Voir [Limites du sol](../concepts/ground-limits.md#sans-limite).

## Les joueurs sur console en crossplay peuvent-ils rejoindre un monde modifié ?

Oui : un monde modifié reste un monde Valheim normal, et le sol de l'éditeur au-delà de 8 m ne demande aucun mod. Les
exceptions sont les mondes qui demandent eux-mêmes un mod : un monde créé avec Riverheim, ou un serveur qui fait
tourner Homestead, refuse les joueurs qui n'ont pas le mod, et les joueurs sur console ne peuvent pas installer de
mods. Voir [Un nouveau monde](../new-world.md#astuces-et-limites).

## Puis-je modifier pendant que le jeu tourne ?

Oui, en mode direct, avec le plugin WorldEditorBridge dans le jeu ou le serveur : voir
[Modifier le monde dans lequel vous jouez](../guides/edit-your-game-live.md) ou
[Modifier un serveur dédié pendant que d'autres jouent](../guides/edit-a-server-live.md). Pour modifier les fichiers
d'un monde enregistré, fermez d'abord le jeu ou arrêtez le serveur : un jeu en cours écrase ce qu'écrit l'éditeur.
Voir [Le jeu a sauvegardé pendant que vous modifiiez](troubleshooting.md#le-jeu-a-sauvegardé-pendant-que-vous-modifiiez).

## Puis-je modifier un serveur que je loue chez un hébergeur ?

Oui, mais en direct seulement si l'hébergeur vous donne un accès SSH à la machine (une connexion shell, pas seulement
SFTP ou un gestionnaire de fichiers) : demandez-le à l'hébergeur avant de louer. La plupart des serveurs de jeu loués
chez un hébergeur ne donnent qu'un panneau web et un accès FTP, si bien que l'éditeur ne peut pas joindre le plugin :
arrêtez le serveur, téléchargez son monde, modifiez-le, renvoyez-le. Un serveur qu'un panneau de jeu comme Pterodactyl
fait tourner dans un conteneur sur votre propre machine peut être modifié en direct, via un tunnel que vous ouvrez
vous-même. Voir [Modifier un serveur loué ou hébergé par un panneau](../guides/edit-a-rented-server.md).

## L'éditeur demande un jeton. Où est-il ?

Dans le `BepInEx/config/Tie.WorldEditorBridge.cfg` du jeu ou du serveur, sur la ligne `Token`. Le plugin écrit ce
fichier la première fois que le jeu démarre avec lui. Pour votre propre jeu, l'éditeur le lit tout seul ; pour un
serveur, vous le copiez depuis le serveur. Voir [Réglages du plugin](../live-mode.md#réglages-du-plugin).

## Est-ce que cela fonctionne avec les mods ?

Oui. Dans une partie moddée en direct, l'éditeur découvre les objets des mods et dessine leurs modèles (voir
[Parties moddées](../live-mode.md#jeux-moddés)). Les mods qui changent la façon dont les mondes sont créés demandent le
module complémentaire de l'éditeur qui leur correspond : Expand World, Riverheim et Tunnelheim en ont un. Voir
[Modifier un monde créé avec Expand World, Riverheim ou Tunnelheim](../guides/use-mods-worlds.md).

## Je joue avec les mods Expand World. L'éditeur le sait-il ?

Oui, avec le module complémentaire Expand World installé : le sol, la carte, les biomes et les prévisions suivent les
réglages et les fichiers de données des mods. Sans lui, chaque monde est généré comme le fait le jeu lui-même. Voir
[Modifier un monde créé avec Expand World, Riverheim ou Tunnelheim](../guides/use-mods-worlds.md).

## Mon monde a été créé avec Riverheim. Puis-je l'ouvrir ?

Oui, avec le module complémentaire Riverheim installé et Riverheim lui-même dans un dossier BepInEx que l'éditeur
connaît : l'éditeur dessine le monde avec votre propre copie du mod. Voir [Modifier un monde créé avec Expand World, Riverheim ou Tunnelheim](../guides/use-mods-worlds.md).

## Ai-je besoin de Homestead ?

Seulement pour construire les plans de l'éditeur en jeu. L'Atelier, la bibliothèque et le collage de plans dans un
monde fonctionnent sans lui ; quand il manque, la page d'accueil le signale. Notez qu'un serveur qui fait tourner
Homestead refuse les joueurs qui ne l'ont pas. Voir [L'Atelier](../workshop.md).

## Où le télécharger ? Est-il sur Thunderstore, Hexium ou Nexus Mods ?

- **L'éditeur** est un programme, pas un mod : téléchargez-le depuis la
  [page des versions](https://github.com/Shuiei/valheim-world-editor/releases/latest) ou depuis
  [Nexus Mods](https://www.nexusmods.com/valheim/mods/4348).
- **Le plugin WorldEditorBridge** (pour la modification en direct) est sur la page des versions, Nexus Mods,
  [Thunderstore](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) et
  [Hexium](https://valheim.hexium.gg/mods/Tie/WorldEditorBridge), pour les gestionnaires de mods.
- **Modules complémentaires** pour les mods : sur la page des versions et Nexus Mods.

Utilisez le plugin et les modules complémentaires de la même version que l'éditeur. Voir [Installer](../start/install.md).

## Envoie-t-il quoi que ce soit sur Internet ?

Non. Tout se passe sur votre ordinateur ; un serveur dédié n'est joint que par le tunnel SSH chiffré propre à
l'éditeur, avec un jeton que vous saisissez. Voir [Rien ne quitte votre ordinateur](../concepts/safety.md#rien-ne-quitte-votre-ordinateur).

## Est-ce sans danger de laisser Claude ou un script modifier mon monde ?

Ce que modifie Claude reste en attente jusqu'à ce que vous enregistriez, et il ne peut se connecter que depuis cet
ordinateur avec un jeton. Les scripts sont des programmes qui s'exécutent avec vos droits : Claude ne peut donc les
exécuter que si vous l'y autorisez. Voir [Les scripts et Claude](../concepts/safety.md#scripts-et-claude).

## Quelque chose ne fonctionne pas. Où regarder ?

Commencez par [Dépannage](troubleshooting.md). Pour signaler un bug, joignez le journal de l'éditeur :
[Le journal et les rapports de bug](troubleshooting.md#le-journal-et-les-rapports-de-bug).
