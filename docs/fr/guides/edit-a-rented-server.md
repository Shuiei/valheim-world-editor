<!-- source: 94a46b5b -->
# Modifier un serveur loué ou hébergé par un panneau

Modifiez le monde d'un serveur que vous louez chez un hébergeur de jeux, ou d'un serveur qu'un panneau de jeu comme
Pterodactyl ou Pelican fait tourner dans un conteneur sur votre propre machine.

**La modification en direct demande un accès SSH à la machine qui fait tourner le serveur** : une connexion à son
shell, pas seulement SFTP ou un gestionnaire de fichiers. L'éditeur joint le plugin par SSH, sur le `127.0.0.1` propre
au serveur (voir [Modifier un serveur dédié pendant que d'autres jouent](edit-a-server-live.md)). Avant de louer un
serveur pour le modifier en direct, demandez à l'hébergeur si l'offre comprend un accès SSH. Deux sortes de serveurs
ne s'y prêtent pas :

- **Un serveur de jeu loué chez un hébergeur** (vous disposez d'un panneau web et d'un gestionnaire de fichiers ou de
  FTP/SFTP, mais pas d'accès SSH à la machine) : l'éditeur ne peut pas y joindre le plugin. Modifiez plutôt les
  fichiers du monde : voir [Un serveur de jeu loué](#un-serveur-de-jeu-loué).
- **Un panneau de jeu sur votre propre machine** (Pterodactyl, Pelican, ou tout serveur qui tourne dans un conteneur
  Docker) : la modification en direct fonctionne, par un tunnel que vous ouvrez vous-même. Voir
  [Un serveur dans un conteneur](#un-serveur-dans-un-conteneur).

Si votre hébergeur vous donne une vraie connexion SSH à la machine qui fait tourner le jeu (un VPS ou une machine
dédiée louée, pas seulement un serveur de jeu), suivez plutôt
[Modifier un serveur dédié pendant que d'autres jouent](edit-a-server-live.md).

## Un serveur de jeu loué

**Il vous faut :** le panneau ou l'accès aux fichiers de l'hébergeur (son gestionnaire de fichiers, FTP ou SFTP) et le
droit d'arrêter et de démarrer le serveur.

Le mode direct n'est pas possible : le plugin ne répond que sur la machine elle-même, et l'hébergeur ne donne aucun
moyen d'y entrer. La connexion SFTP d'un panneau (souvent sur le port 2022) ne fait que distribuer des fichiers : le SSH
de l'éditeur ne peut pas y ouvrir de tunnel. Modifiez plutôt le monde enregistré :

1. Dans le panneau de l'hébergeur, **arrêtez** le serveur. Un serveur en cours écrase les fichiers que vous remettez.
2. Avec le gestionnaire de fichiers du panneau ou un logiciel SFTP (FileZilla, WinSCP), téléchargez le dossier du
   monde, `worlds_local/<World>` dans le dossier de sauvegarde du serveur. Gardez-en une copie intacte : c'est votre
   sauvegarde.
3. Dans l'éditeur, ouvrez ce dossier : **Un monde enregistré** → **Un autre dossier de mondes** → **Parcourir…** (voir
   [Ouvrir un monde qui n'est pas dans la liste](open-a-world.md)).
4. Faites vos modifications et appuyez sur **Enregistrer dans le monde**.
5. Renvoyez le dossier au même endroit, en remplaçant les fichiers qui s'y trouvent, puis démarrez le serveur.

Le serveur doit rester arrêté de l'étape 1 à l'étape 5 : ce que les joueurs font entre-temps est perdu au moment de
l'envoi.

## Un serveur dans un conteneur

**Il vous faut :** une connexion SSH à la machine sur laquelle tourne le panneau (votre VPS, en root ou avec un
utilisateur autorisé à lancer `docker`), et le droit de modifier les fichiers du serveur de jeu dans le panneau.

Un panneau fait tourner chaque serveur de jeu dans son propre conteneur Docker, qui a son propre `127.0.0.1`. Le plugin
écoute là, dans le conteneur, alors que le tunnel de l'éditeur aboutit au `127.0.0.1` de la machine, où rien ne
répond : l'éditeur indique que le plugin ne répond pas, alors que le serveur tourne avec. La solution est un tunnel
vers l'adresse propre au conteneur :

```text
 your computer               your VPS          the server's container
┌───────────────────────┐   ┌───────────┐     ┌───────────────────────┐
│ editor                │SSH│           │     │ WorldEditorBridge     │
│ 127.0.0.1:15182 ══════╪═══╪═► sshd ───┼────►│ 172.18.0.x:5182       │
└───────────────────────┘   └───────────┘     └───────────────────────┘
```

1. Dans le panneau, installez BepInEx et WorldEditorBridge sur le serveur, comme pour tout serveur dédié (étapes 1 à 3
   de [Modifier un serveur dédié pendant que d'autres jouent](edit-a-server-live.md)), et démarrez-le une fois.
2. Dans le gestionnaire de fichiers du panneau, ouvrez `BepInEx/config/Tie.WorldEditorBridge.cfg`. Mettez
   `BindAddress = +` et copiez la valeur de la ligne `Token`.
3. Redémarrez le serveur. Sa console ou `BepInEx/LogOutput.log` indique `WorldEditorBridge <version> listening on
   http://+:5182/`, puis un avertissement disant que le jeton circule sans chiffrement : dans le conteneur, cela ne
   concerne que le réseau propre au panneau.
4. Connectez-vous au VPS en SSH et trouvez l'adresse du conteneur. Le conteneur porte le nom de l'identifiant du
   serveur (le panneau l'affiche dans les réglages du serveur) ; `docker ps` les liste tous :

   ```sh
   docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' <server id>
   ```

   La commande répond une adresse comme `172.18.0.5`.
5. Sur votre ordinateur, ouvrez un tunnel vers elle et laissez-le ouvert pendant que vous modifiez (remplacez
   l'adresse et l'identifiant de connexion) :

   ```sh
   ssh -N -L 15182:172.18.0.5:5182 user@your-vps
   ```

   Avec PuTTY : **Connection** → **SSH** → **Tunnels**, **Source port** `15182`, **Destination** `172.18.0.5:5182`,
   **Add**, puis ouvrez la session.
6. Sur la page d'accueil de l'éditeur : **Un serveur dédié** → **Plus d'options** → **J'ai créé mon propre tunnel**.
   Tapez `127.0.0.1:15182` comme **Adresse de la passerelle**, collez le jeton, et appuyez sur **Se connecter**.

Modifiez ensuite comme sur n'importe quel serveur : voir [Mode direct](../live-mode.md#lutiliser).

**À garder en tête :**

- `+`, ni l'adresse du conteneur ni `0.0.0.0` : avec l'adresse du conteneur, le plugin refuse chaque requête qui passe
  par un tunnel, et avec `0.0.0.0`, toutes les requêtes (« The server answered with an error (400) »).
- N'ajoutez pas le port du plugin (5182) aux ports du serveur (allocations) dans le panneau : cela l'ouvre à Internet.
- L'adresse du conteneur peut changer quand le serveur est reconstruit ou réinstallé. Si le tunnel ne répond plus,
  cherchez-la à nouveau (étape 4).
- Gardez `BindAddress = 127.0.0.1` sur un serveur qui ne tourne pas dans un conteneur : là, `+` fait écouter le plugin
  sur chaque réseau de la machine (les appels venant d'Internet restent refusés).

## Vérifier le résultat

- **Un serveur de jeu loué :** le serveur démarre, et vos modifications sont là en jeu.
- **Un serveur dans un conteneur :** la barre du haut affiche **EN DIRECT**, et après **Appliquer en direct**,
  « Tout est appliqué ».

Si l'éditeur affiche plutôt un message, voir
[Un serveur refuse de se connecter](../help/troubleshooting.md#un-serveur-refuse-de-se-connecter).

## Annuler

- **Un serveur de jeu loué :** arrêtez le serveur et renvoyez la copie que vous avez gardée à l'étape 2.
- **Un serveur dans un conteneur :** `Ctrl+Z`, puis à nouveau **Appliquer en direct**. Pour refermer le plugin,
  remettez `BindAddress = 127.0.0.1` et redémarrez le serveur.

## Voir aussi

- [Modifier un serveur dédié pendant que d'autres jouent](edit-a-server-live.md)
- [Réglages du plugin](../live-mode.md#réglages-du-plugin) : `BindAddress`, `Port`, `Token`
- [Ouvrir un monde qui n'est pas dans la liste](open-a-world.md)
- [Sauvegarder un monde avant de le modifier](back-up-a-world.md)
