<!-- source: 6167da37 -->
# Modification en direct

En mode direct, l'éditeur travaille sur le monde d'un jeu **en cours** au lieu de ses fichiers. Les joueurs peuvent
rester connectés : ils voient vos modifications dès que vous les appliquez, et vous voyez ce qu'ils construisent
pendant que vous modifiez. Cette page explique comment l'éditeur joint le jeu, ce qui circule entre eux, et ce qui se
passe quand des joueurs et l'éditeur modifient le même endroit.

## Comment il se connecte

Le mode direct a besoin d'un petit plugin, **WorldEditorBridge**, dans le jeu qui **héberge** le monde : le serveur
dédié, ou votre propre jeu quand vous jouez seul ou hébergez. Il fonctionne sur **BepInEx**, le chargeur de mods
habituel de Valheim. Les joueurs qui rejoignent n'ont besoin ni de l'un ni de l'autre, joueurs sur console compris, et
la modification hors ligne non plus.

Le plugin répond à l'éditeur sur un port de l'ordinateur où il tourne (5182 au départ), et il est conçu pour rester
privé :

- **Il n'écoute que sur cet ordinateur lui-même** (`127.0.0.1`), donc rien sur le réseau ne peut le joindre. Son
  réglage `BindAddress` peut l'ouvrir aux réseaux locaux et aux VPN de l'ordinateur (jamais à internet : ces appels
  sont refusés), avec le jeton non chiffré ; laissez-le tel quel, sauf si vous modifiez depuis un autre ordinateur de
  la maison ([Sans SSH](../live-mode.md#sans-ssh)).
- **Il lui faut son jeton secret.** À son premier lancement, le plugin écrit `BepInEx/config/Tie.WorldEditorBridge.cfg`
  avec un **jeton** de 24 lettres et chiffres aléatoires. Une requête sans lui est refusée. Tant que le jeu ou le
  serveur n'a pas été lancé une fois avec le plugin, il n'y a pas de jeton.

**Votre propre jeu.** L'éditeur tourne sur le même ordinateur, donc il lit lui-même le port et le jeton dans ce
fichier, dans chaque dossier BepInEx qu'il connaît (votre dossier Valheim, les profils de votre gestionnaire de mods,
les dossiers ajoutés dans **Paramètres**), et demande à chaque plugin trouvé si un monde est chargé.

**Un serveur dédié.** Le plugin ne peut pas être joint directement depuis votre ordinateur, donc l'éditeur se fraie
son propre chemin :

```text
 votre ordinateur                                le serveur
 ┌──────────────────────┐   SSH, chiffré     ┌─────────────────────────────────────┐
 │ éditeur ─ port 15182 ═╪═══════════════════╪═► 127.0.0.1:5182  WorldEditorBridge │
 └──────────────────────┘                    │                   dans Valheim      │
                                             └─────────────────────────────────────┘
```

1. Il se connecte au serveur par **SSH** avec votre compte (il a son propre SSH, donc aucun programme `ssh` n'est
   nécessaire).
2. Il redirige un port libre de votre ordinateur (à partir de 15182) à travers cette connexion chiffrée vers le port
   du plugin sur le serveur : un **tunnel SSH**.
3. Il envoie le jeton, que **vous** tapez : l'éditeur ne le lit jamais sur le serveur. Ainsi, une connexion SSH seule
   ne suffit pas pour modifier le monde ; celui qui se connecte doit aussi connaître le secret du plugin.

Un serveur qu'un panneau de jeu (Pterodactyl, Pelican) fait tourner dans un conteneur Docker a son propre
`127.0.0.1`, que le tunnel vers la machine n'atteint pas, et un serveur de jeu loué chez un hébergeur ne donne en
général aucun accès SSH : voir [Modifier un serveur loué ou hébergé par un panneau](../guides/edit-a-rented-server.md).

L'éditeur retient aussi l'identité du serveur à la première connexion. Si elle change un jour, l'éditeur refuse de se
connecter, parce que quelqu'un pourrait se faire passer pour votre serveur.

Le plugin ne modifie qu'un monde que son jeu héberge. Dans un jeu qui a rejoint le serveur de quelqu'un d'autre, il
refuse, car un tel jeu ne connaît que les objets autour de son propre joueur. Utilisez le plugin de la même version
que votre éditeur.

## Ce qui est envoyé, et quand

**Quand vous vous connectez**, l'éditeur lit tout le monde depuis le jeu : chaque objet, les zones que le jeu a
créées, les lieux. Ce sont les mêmes données que contient une sauvegarde, donc la carte, la recherche et chaque outil
fonctionnent comme hors ligne. Sur un grand monde, cela prend un moment, jusqu'à quelques minutes. Il demande aussi les
objets que le jeu connaît et qui ne viennent pas de Valheim (pièces des mods), pour qu'ils aient leur nom et
apparaissent dans la liste de Placement et le panneau Bâtir.

**Quand vous appliquez** (**Appliquer en direct**, ou chaque étape aussitôt avec **Auto**), il envoie ce qui a changé
depuis la dernière application, dans cet ordre :

1. **Le sol** de chaque zone que vous avez modifiée, sol Sans limite compris (voir
   [Sans limite](ground-limits.md#sans-limite)).
2. **Les objets** que vous avez supprimés, placés, collés, remplacés, modifiés ou déplacés.
3. **Les réinitialisations de zones**, en dernier. Le jeu refait ces zones, donc l'éditeur relit ensuite le monde ;
   l'historique suit, sans ce qu'il a fait dans ces zones.

Le jeu partage chaque modification avec chaque joueur connecté, comme il le fait pour ce que construisent les joueurs,
et la sauvegarde avec le monde. Les objets placés en direct sont créés par le serveur, donc les joueurs les voient comme
quelque chose qui vient d'être construit ou planté.

Le plugin fait tout cela dans la boucle propre du jeu, quelques requêtes par image, pour que le jeu ne lise ni ne
modifie jamais son monde depuis deux endroits à la fois. Une grosse modification (des milliers d'objets) peut faire
saccader le serveur un instant. Si le jeu ne prend pas une requête dans les 30 secondes, le plugin l'abandonne : rien
n'en est fait et rien n'est marqué appliqué, donc appliquez à nouveau. Quand c'est la réponse qui se perd au retour (un
tunnel coupé), l'éditeur ne peut pas savoir si la requête a été faite ; la renvoyer est sans risque, car le plugin
reconnaît une requête qu'il a déjà faite et ne crée pas ses objets deux fois. Une seule application tourne à la fois.

### Déplacer un objet le garde comme le même objet

Un objet que vous déplacez ou tournez se déplace dans le jeu sous son propre identifiant quand rien d'autre n'a changé
en lui. Un mod qui suit les objets pendant que le jeu tourne (le panneau d'étiquette d'un coffre, les coffres d'un
entrepôt, une protection) le reconnaît alors toujours, et un coffre garde son contenu tel que le jeu l'a. Un objet dont
vous avez modifié les données garde son identifiant de la même façon.

Il y a un piège. Le jeu d'un joueur met en place un objet fixe (un mur, un coffre) une seule fois, quand il arrive à
portée, et ne le déplace plus jamais ensuite. Donc un déplacement ne garde l'identifiant que lorsqu'aucun joueur n'a à
la fois son ancien et son nouvel emplacement chargés (environ 160 m autour de chaque joueur). Sinon, l'objet est recréé
à son nouvel emplacement et retiré de l'ancien, et le message d'application dit combien : pour les autres mods, ce sont
de nouveaux objets. Déplacez une construction quand personne n'est à proximité pour tout en garder. Le groupe lié d'un
mod (un bateau et ses pièces) et les créatures sont toujours recréés.

## Suivre le jeu

Tant qu'un secteur est ouvert, l'éditeur demande au jeu toutes les 2 secondes ce qui a changé dans les zones de ce
secteur. C'est peu coûteux : le jeu répond avec un nombre par zone, qui change dès que quelque chose change dans la
zone, et seules les zones dont le nombre a changé sont relues. La barre d'état dit ce qui est arrivé (« Depuis le jeu :
2 nouvel(s) objet(s), 1 retiré(s), le sol de 1 zone(s). ») ; un simple changement de données, comme un feu qui
s'éteint, arrive sans un mot.

- Les **objets** que les joueurs construisent, retirent ou modifient apparaissent, disparaissent ou changent dans
  l'éditeur, sans étape dans l'Historique. Un objet modifié reste le même objet dans l'éditeur, donc la sélection,
  l'Inspecteur et sa place dans l'Historique restent avec lui.
- **Vos propres objets, une fois appliqués,** sont affichés tels que le jeu les a vraiment : là où la physique a posé
  une pièce, la santé et le point d'apparition que le jeu a donnés à une créature.
- **Ce qui bouge ou change de soi-même** (créatures, bateaux, chariots, objets qui traînent, feux et fonderies) est
  suivi au fil de l'eau, sans relire toute sa zone à chaque fois.
- Le **sol** creusé, aplani ou peint en jeu arrive là où vous n'avez pas de modifications à vous dans cette zone (voir
  plus bas quand vous en avez).

Le suivi n'a lieu que dans l'éditeur 3D, jamais pendant un coup de pinceau ni pendant une application. La carte montre
le monde tel qu'il a été lu en dernier : **Recharger** le relit en entier.

## Plusieurs éditeurs et joueurs

Les joueurs et l'éditeur peuvent modifier le même endroit. L'éditeur règle cela ainsi :

| Ce qui se passe | Ce que fait l'éditeur |
|---|---|
| Des joueurs modifient le sol d'une zone où vous avez des modifications pas encore appliquées | Les vôtres sont conservées et la barre d'état le dit. **Appliquer en direct** demande alors d'abord (**Appliquer par-dessus les modifications du jeu**) : appliquer écrit votre sol par-dessus cette zone, et ce que les joueurs y ont fait depuis peut être perdu. **Pas maintenant** garde vos modifications en attente. |
| Des joueurs prennent ou détruisent un objet | Il est parti pour de bon : annuler et rétablir le laissent tranquille, et une suppression de cet objet que vous n'aviez pas appliquée est abandonnée. Cela inclut les objets que vous avez placés. |
| Vous supprimez un objet et appliquez | Il reste supprimé ; l'annulation le ramène sous forme de copie (voir [Appliquer en direct](saving-and-history.md#appliquer-en-direct)). |
| Vous déplacez une construction alors que des joueurs sont à proximité | Elle est recréée plutôt que déplacée (voir [Déplacer un objet le garde comme le même objet](#déplacer-un-objet-le-garde-comme-le-même-objet)). |
| Vous modifiez un objet dans l'Inspecteur pendant que le jeu le modifie aussi | L'Inspecteur n'applique pas par-dessus la version plus récente : il l'affiche, pour la modifier à nouveau. |
| Vous réinitialisez une zone près de laquelle se tient un joueur | Le jeu la refait aussitôt ; sinon, la prochaine fois que quelqu'un vient. |

Le plugin répond à tout éditeur qui a le jeton, et il ne distingue pas les modifications d'un éditeur de celles d'un
joueur : ce qu'un autre éditeur applique arrive dans le vôtre comme la modification d'un joueur.

**Auto et Claude.** Avec **Auto** activé, chaque étape atteint les joueurs dès que vous la terminez, un long coup de
pinceau partant en une seule application. C'est pourquoi Claude ne modifie rien dans un monde en direct tant qu'Auto
est activé (voir [Scripts et Claude](safety.md#scripts-et-claude)).
