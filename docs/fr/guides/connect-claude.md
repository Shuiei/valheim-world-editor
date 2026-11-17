<!-- source: e9f26daf -->
# Laisser Claude façonner ou construire

Connectez Claude Code ou Claude Desktop à l'éditeur, puis demandez à Claude, avec des mots simples, de façonner le sol,
de planter ou de construire.

**Il vous faut :** l'éditeur, et [Claude Code](https://claude.com/claude-code) ou Claude Desktop sur le même ordinateur.

1. Sur la page d'accueil de l'éditeur, ouvrez **Paramètres** et trouvez la section **Claude**.
2. Activez **Autoriser Claude à se connecter**. Facultatif : activez **Claude peut exécuter des scripts** pour laisser
   Claude écrire et exécuter des scripts C# (un script s'exécute avec vos droits sur cet ordinateur ; laissez-le
   désactivé en cas de doute).
3. Appuyez sur **Enregistrer** (cela ferme Paramètres), puis rouvrez **Paramètres** : la section affiche « Listening on http://127.0.0.1:5731/mcp ».
4. Ajoutez l'éditeur à Claude, une seule fois :
   - **Claude Code :** appuyez sur **Copier** à côté de la commande, collez-la dans un terminal et exécutez-la.
   - **Claude Desktop :** appuyez sur **Copier** à côté des lignes de configuration, ajoutez-les dans Claude Desktop →
     Settings → Developer → **Edit Config** (`claude_desktop_config.json`), puis redémarrez Claude Desktop.
5. Dans l'éditeur, ouvrez un secteur d'un monde en 3D, ou l'Atelier.
6. Demandez à Claude, par exemple : « Aplanis un carré de 40 m au milieu du secteur, pave-le, et pose un cercle de
   torches autour. »
7. Regardez Claude travailler dans la vue 3D. Chacune de ses étapes apparaît dans l'**Historique** sous la forme
   « Claude: … ».
8. Gardez le résultat avec **Enregistrer dans le monde**, **Appliquer en direct** ou **Enregistrer le plan**, ou
   annulez-le (ci-dessous).

<!-- picture: claude-code-mcp.jpg: a terminal running Claude Code after typing /mcp, the list showing "valheim-editor" as connected, next to the editor window with a 3 × 3 area open -->

## Vérifier le résultat

- Dans Claude Code, `/mcp` liste **valheim-editor** comme connecté.
- La première réponse de Claude décrit ce qui est ouvert dans l'éditeur.
- Le nombre de modifications en attente de la barre du haut augmente avec les modifications de Claude ; rien n'est
  écrit avant que vous enregistriez ou appliquiez.

Si Claude dit qu'il ne peut pas joindre l'éditeur, voir [Claude ne peut pas se connecter](../help/troubleshooting.md#claude-ne-peut-pas-se-connecter).

## Annuler

- `Ctrl+Z` reprend la dernière étape, celle de Claude ou la vôtre.
- **Annuler les modifications de Claude**, en haut de l'Historique, retire toutes les étapes que Claude a faites dans
  le secteur ouvert et garde les vôtres.
- **Abandonner** jette tout ce qui n'est pas enregistré.
- Pour empêcher Claude de joindre l'éditeur, désactivez **Autoriser Claude à se connecter** et faites
  **Enregistrer**. **Nouveau jeton** puis **Enregistrer** bloque toute copie de l'ancienne commande.

## Voir aussi

- [Claude](../claude.md) : chaque réglage, ce que Claude peut faire, la sécurité
- [Sécurité](../concepts/safety.md#scripts-et-claude) : ce que les scripts et Claude peuvent atteindre
- [Annuler une étape, retirer une modification ou tout abandonner](undo-changes.md)
