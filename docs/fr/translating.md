<!-- source: f7df77d8 -->
# Traduire l'éditeur

Les mots de l'éditeur sont dans des catalogues, un par langue : un fichier JSON de textes classés par clé. L'anglais,
[`editor/Core/Lang/en.json`](../../editor/Core/Lang/en.json), est la source à partir de laquelle chaque autre catalogue
est traduit.

L'éditeur est livré en anglais, français (`fr`), allemand (`de`), espagnol (`es`), portugais du Brésil (`pt-BR`,
également affiché sur un ordinateur réglé en portugais du Portugal), thaï (`th`) et filipino (`fil`, également affiché sur un ordinateur réglé en tagalog). La documentation est traduite dans chacune (`docs/fr/`…, ses images prises dans cette langue). Il affiche la langue choisie sous
**Paramètres → Langue**, par défaut celle de l'ordinateur quand il en a un catalogue. Un texte qu'un catalogue n'a pas
encore s'affiche en anglais. Une nouvelle langue est prise en compte au redémarrage de l'éditeur.

Tous les textes ne viennent pas encore du catalogue. Ceux-ci, si :

- chaque infobulle (les infobulles de l'Inspecteur construites à partir des données d'un objet gardent ses noms tels
  que la sauvegarde les contient) ;
- la page d'accueil, les Paramètres, la carte du monde, la page Un nouveau monde, et les petites boîtes de dialogue
  (questions, messages, détails d'un plan) avec les mots qu'elles partagent ;
- la barre du haut de l'éditeur 3D, avec ce que demandent et disent l'enregistrement, l'application, l'abandon, la
  sortie et la fermeture, les messages et les joueurs du jeu en direct ;
- les indications de la barre d'état, le panneau Historique, le panneau Vue, la fiche d'aide, et ce que disent la barre
  d'état et l'Historique des modifications de Claude ;
- les outils : les pinceaux avec leurs tampons, Mesure, Masque, Script, Tracé, Relief, Sélection, Coller, Placement,
  Bâtir (son panneau et la barre de construction), Région, l'outil Donjon avec son générateur, le panneau Plans, l'Atelier,
  et l'Inspecteur ;
- les noms des biomes.

Le reste suit, fenêtre par fenêtre. Un catalogue créé maintenant continue de fonctionner à mesure que des textes sont
ajoutés : les nouveaux s'affichent en anglais tant qu'ils ne sont pas traduits.

## Créer un catalogue

1. Copiez `en.json` dans le dossier `lang` du dossier de données de l'éditeur
   (`~/.local/share/ValheimWorldEditor/lang` sous Linux, `%LOCALAPPDATA%\ValheimWorldEditor\lang` sous Windows ;
   **Paramètres → Langue** l'indique), en le nommant d'après le code de la langue : `fr.json`, `de.json`, `pt-BR.json`.
2. Traduisez les valeurs, jamais les clés. `lang.name` est le nom de la langue dans cette langue (« Français »), tel
   que les Paramètres le listent.
3. Gardez chaque `{0}`, `{1}`… : l'éditeur y met une valeur (un dossier, un nombre). Leur ordre dans la phrase peut
   changer. Un texte dont les emplacements diffèrent de l'anglais, ou qui contient un `{` ou un `}` isolé, n'est pas
   utilisé.
4. Choisissez la langue dans **Paramètres**, redémarrez l'éditeur et regardez. Les clés que vous omettez, ou que vous
   supprimez de votre copie, s'affichent en anglais.

Le même dossier accepte aussi des corrections : un fichier qui y porte le code d'un catalogue livré avec l'éditeur est
appliqué par-dessus, texte par texte. Là, une clé que vous omettez (ou un texte qui n'est pas utilisé) garde la
traduction livrée ; l'anglais n'apparaît que là où aucun des deux n'a le texte.

Pour qu'un catalogue soit livré avec l'éditeur, ouvrez une pull request qui l'ajoute à `editor/Core/Lang/` (uniquement
des clés de `en.json` ; `LangCatalogueTests` les vérifie).

## Traduire la documentation

La fenêtre d'aide affiche la documentation dans la langue de l'éditeur quand elle a une traduction de la page, et en
anglais sinon. Une traduction de la documentation est un dossier de `docs/` nommé d'après le code de la langue, comme
son catalogue : `docs/fr/`, `docs/pt-BR/`. Son propre `README.md` est ce qui en fait une traduction (la première page,
et la table des matières de la fenêtre d'aide) ; les dossiers anglais (`guides/`, `help/`, `concepts/`, `start/`) n'en
ont pas.

- **Pages.** Le dossier reflète les pages anglaises : `docs/fr/map.md` traduit `docs/map.md`, et
  `docs/fr/guides/make-a-road.md` traduit `docs/guides/make-a-road.md`. Traduisez les pages que porte la fenêtre d'aide
  ([writing-docs.md](../dev/writing-docs.md) liste celles qu'elle ne porte pas). Une page pas encore traduite s'affiche
  en anglais : une traduction peut donc commencer par quelques pages.
- **Liens.** Écrivez-les comme la page anglaise les écrit, depuis le dossier : `map.md#…` et `guides/x.md` depuis
  `docs/fr/README.md`, `../area.md` depuis `docs/fr/guides/`. Un lien qui sort de `docs/` remonte d'un `../` de plus que
  le lien anglais (`../../README.md`, `../../editor/Core/Lang/en.json`), de même qu'un lien vers un fichier de `docs/`
  qui n'est pas traduit (`../worldeditor.schema.json`). Les liens vers une page que le dossier n'a pas encore
  fonctionnent quand même dans l'éditeur (il affiche la page anglaise), mais pas sur GitHub tant que la page n'est pas
  traduite.
- **Images.** Une image qui contient des mots est refaite dans votre langue dans `docs/fr/images/`, sous le même nom, et
  écrite `images/x.jpg`. Une image sans rien à traduire peut rester l'anglaise : `../images/x.jpg`. Tant qu'une image de
  `images/` n'est pas ajoutée à votre dossier, la fenêtre d'aide affiche l'anglaise du même nom.
- **Titres.** Traduisez-les ; leurs identifiants changent avec eux (GitHub les construit à partir des mots :
  `#search-the-world` devient `#rechercher-dans-le-monde`), et les liens propres à la traduction utilisent les
  identifiants traduits, comme GitHub l'exige. Gardez les titres de la page anglaise : autant, des mêmes niveaux, dans le
  même ordre. L'éditeur fait alors aussi correspondre les identifiants anglais aux titres traduits, de sorte que ses
  boutons d'aide (`F1`, les `?` des panneaux), qui nomment des titres anglais, arrivent au bon endroit.
- **Marque de source.** Mettez sur la page, en premier, une ligne indiquant de quel texte anglais elle a été traduite :
  `<!-- source: 1a2b3c4d -->`, les 8 premiers caractères du SHA-256 de la page anglaise (ni GitHub ni la fenêtre d'aide
  ne l'affichent). Obtenez-les avec `sha256sum docs/map.md | cut -c1-8` (macOS : `shasum -a 256`). La marque est
  calculée sur la page avec des fins de ligne Unix : sur une copie Windows qui les a transformées en `\r\n`, utilisez
  `git show HEAD:docs/map.md | sha256sum | cut -c1-8`, qui donne la page telle que le dépôt la conserve.

Quand la page anglaise change, sa marque change : la fenêtre d'aide affiche toujours la traduction, avec une note
indiquant que la page a changé en anglais et un bouton qui affiche la page anglaise. Pour mettre une traduction à jour,
regardez ce qui a changé en anglais depuis (`git log -p docs/map.md`), traduisez-le, et écrivez la nouvelle marque. Les
tests vérifient chaque lien et chaque image d'une traduction, et qu'une traduction à jour a les titres de sa page
anglaise.

## Pour les développeurs

- Un texte que voit l'utilisateur va dans `en.json` sous une clé nommée d'après l'endroit où il s'affiche
  (`settings.port`, `common.cancel` pour les mots utilisés partout) et est demandé avec `Lang.T("settings.port")`, ou
  `Lang.T("settings.worldNotFound", path)` avec des valeurs pour ses emplacements `{0}`… (insérées avec la culture
  invariante). Une infobulle est une clé `tip.`, donnée avec `.Tip("settings.port")` (voir `editor/Desktop/Tips.cs`).
- Écrivez la clé en entier dans le code, même quand elle est transmise : `LangCatalogueTests` vérifie que chaque clé
  demandée est dans `en.json` et que chaque clé de `en.json` est demandée quelque part.
- Les textes destinés à Claude (les descriptions et les réponses des outils MCP), le journal, la sortie en ligne de
  commande et les formats de fichiers restent en anglais et hors du catalogue. Un texte affiché à l'utilisateur et
  également donné à Claude (le coût d'une pièce) demande l'anglais avec `Lang.En` du côté de Claude.
- Les fenêtres sont construites une fois, dans la langue prise au démarrage (`Lang.Use` dans `Program.Main`) ; rien n'a
  besoin d'écouter un changement de langue.

### Nommer les clés

- La première partie est la fenêtre, écrite comme ses clés d'infobulle (`area`, `place`, `select`, `path`,
  `blueprints`, `newWorld`…). Un outil renommé dans la fenêtre garde ses clés : celles de l'outil Région sont `area.*`,
  et les types de l'outil Relief gardent `shape.*` (Formule) et `mountain.*` à côté de `landform.*`. Préfixes partagés :
  `common.*` pour un mot utilisé dans trois fenêtres ou plus (OK, Oui, Annuler, Enregistrer, Abandonner, Rester, Les
  garder…), `dialog.*` pour les petites boîtes de dialogue, `save.*` pour les questions d'enregistrement, d'application,
  d'abandon et de sortie, `biome.*` pour les noms de biomes.
- La partie du milieu dit de quel type de texte il s'agit : `label` (libellés de lignes, boutons, interrupteurs),
  `head` (titres de section), `hint` (explications discrètes), `msg` (barre d'état et résultats), `undo` (libellés de
  l'Historique), `error` (problèmes affichés dans un panneau), `ask.<name>.title|text|yes|no` (questions), ou le nom
  d'une liste pour ses entrées (`brush.falloff.flatTop`). Les textes isolés d'une fenêtre peuvent se contenter de deux
  parties (`settings.port`). La dernière partie est en camelCase.
- Un mot anglais a une seule clé : réutilisez `common.*` plutôt que d'ajouter « Save » une nouvelle fois. Les noms que
  Claude donne aux contrôles de la fenêtre sont déduits de l'anglais (`Lang.EnglishOf`), qui en a besoin.
- Un texte déplacé dans le catalogue garde sa formulation ; changez les mots dans une modification à part.
- Les textes d'une énumération viennent d'un `switch` qui nomme chaque clé complète, jamais de `"prefix." + value`.

### Valeurs, pluriels et phrases

- Les valeurs vont dans `{0}`, `{1}`… avec leur format et leur unité dans le texte (`"{0:0.0} m"`,
  `"{0} × {1} zones"`), sous forme de nombres et de noms bruts, pas de chaînes formatées. Une clé est toujours demandée
  avec le même nombre de valeurs ; un texte demandé avec des valeurs double ses accolades littérales
  (<code>&#123;&#123;</code>), un texte demandé sans valeurs n'en a jamais.
- Un nom déjà dans la langue de l'utilisateur (un biome, un type d'objet) entre comme valeur. Ne changez pas sa casse
  pour l'insérer dans une phrase (`ToLowerInvariant`) : écrivez la phrase autour de lui.
- Il n'y a pas de règles de pluriel. Gardez « object(s) » dans un seul texte ; là où le code choisit déjà entre un et
  plusieurs, utilisez deux clés complètes (`….one`, `….many`). N'ajoutez jamais de terminaison dans le code.
- Une clé par phrase complète. Une phrase supplémentaire facultative est une clé à part, jointe par une espace ; ne
  collez jamais des morceaux de phrase ensemble. Les listes de noms traduits sont jointes par « , ».

### Ce qui reste en anglais

- Tout ce que lit Claude : les descriptions et les réponses des outils MCP (`ClaudeTools*`, `ClaudeGuide`), le journal
  et la console, la sortie en ligne de commande, les formats de fichiers (`prefs.json`, le fichier d'historique, les
  plans), les données du jeu (noms de prefabs, d'objets et de pièces, clés de données des objets), les noms de produits
  et de mods à l'intérieur des textes (Valheim, Homestead, Steam), et les messages des exceptions (la phrase qui en
  entoure une est traduite).
- Les étapes de Claude dans l'Historique commencent par la marque `Claude: ` (`ClaudeTools.ClaudesStep`), qui reste en
  anglais : Annuler les modifications de Claude trouve les étapes de Claude grâce à elle. Les mots qui la suivent sont
  traduits.
- Un texte affiché à l'utilisateur et également donné à Claude a les deux : la langue de l'utilisateur via `Lang.T`,
  l'anglais de Claude via `Lang.En` (`PieceCost.Describe(english: true)`).
- Le message de la barre d'état est relu par Claude. Mettez-le avec `Msg("area.msg.applied", n)`, ou `Msg(said)` avec
  un `Said` (`Said.Of(key, values…)`, `Said.Raw(text)` pour un texte identique dans les deux, `+` pour ajouter une
  phrase) : la fenêtre affiche la langue de l'utilisateur et garde l'anglais, que lisent les réponses de Claude
  (`MainWindow.LastMessageEn`). Complétez le dernier message avec `LastSaid + …`, jamais en lisant
  `MessageText.Text`.
- Un choix de liste mémorisé (`Prefs.Bind`) est conservé en anglais, pour qu'il revienne dans n'importe quelle langue.

### Vérifier une fenêtre

Les tests ont une langue inventée, `PseudoLang`, qui entoure chaque texte du catalogue de `⟦…⟧`.
`PseudoLang.Untranslated(window)` liste les textes qu'une fenêtre affiche hors de `⟦⟧`, c'est-à-dire qui ne viennent
pas du catalogue. Chaque fenêtre a un test `<Window>LangTests` qui la construit dans cette langue et n'en attend aucun ;
ces tests tournent dans la collection `DataDir`, puisque la langue est partagée.
