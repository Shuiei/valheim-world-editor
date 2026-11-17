<!-- source: 1ccdce30 -->
# Pour les auteurs de mods

L'éditeur fonctionne avec les mods de construction sans rien demander à leurs auteurs : connecté à un jeu moddé, il
apprend les objets des mods, les affiche avec leurs propres modèles et nomme les données qu'ils conservent. Cette page
s'adresse aux auteurs de mods qui veulent aller plus loin : un fichier `worldeditor.json` facultatif pour ce que
l'éditeur ne peut pas deviner, des **objets liés** pour les mods qui construisent une chose à partir de nombreux objets,
des **extensions d'éditeur** (une petite DLL compilée avec le SDK de l'éditeur) pour ce qu'un fichier ne peut pas
exprimer, des **modules complémentaires** (la même chose, faite par n'importe qui pour le mod de quelqu'un d'autre), et
des **points d'accroche de la passerelle** pour que le plugin propre d'un mod participe au mode direct.

Tout ici est facultatif, et chaque étape s'ajoute à la précédente. L'éditeur ne fournit rien pour aucun mod : prendre en
charge un mod dans l'éditeur revient à son auteur, avec ces outils. Les exemples de cette page sont ceux d'un mod
inventé, « Example Vehicles », dont l'exemple complet est fourni avec le SDK (voir
[L'exemple Example Vehicles](#lexemple-example-vehicles)).

## Ce que l'éditeur apprend de lui-même

| Quoi | Comment l'éditeur l'apprend |
|---|---|
| Les objets des mods : noms, pièces de construction, coûts, stations d'artisanat, l'outil qui construit chacun | Connecté à un jeu (mode direct, WorldEditorBridge 1.26.0 ou ultérieur), l'éditeur demande au jeu chaque objet qu'il connaît et garde ceux qui ne sont pas propres à Valheim. Ils sont conservés dans `mod-catalogue.json` dans le dossier de données de l'éditeur : le monde d'un serveur moddé ouvert plus tard depuis ses fichiers, hors ligne, les montre aussi. Un rapport plus récent d'un mod sur une pièce remplace l'ancien ; un mod ne change jamais un des objets propres à Valheim. |
| Leur apparence | Le jeu indique ce qu'il affiche de chaque objet de mod (WorldEditorBridge 1.27.0 ou ultérieur) ; l'éditeur trouve ces maillages, matériaux et textures dans les propres fichiers des mods sur l'ordinateur du joueur (leurs asset bundles, extraits une fois des DLL dans `mod-bundles` du dossier de données). Les maillages qu'un mod construit dans son code pendant que le jeu tourne sont introuvables : ces objets apparaissent sans texture, sauf si une [extension](#extensions) les dessine. |
| Les noms des données que les mods conservent sur les objets | Le jeu n'enregistre qu'un hachage de chaque clé de données. L'éditeur trouve les noms parmi le texte écrit dans les DLL des mods (lues comme des octets, jamais exécutées) dans les dossiers de plugins qu'il connaît, et les garde dans `mod-zdo-keys.json` jusqu'à ce qu'une DLL change. Une clé dont le nom est assemblé pendant que le jeu tourne (`"Corner" + i`) ne peut pas être trouvée ainsi : nommez-la sous `data` dans votre `worldeditor.json` (ci-dessous), sinon l'Inspecteur l'affiche sous forme de `#` suivi de son hachage. |
| De quel mod vient un objet | L'Inspecteur l'indique (avec Jötunn installé, qui le sait ; sinon « D'un mod »). Le panneau [Bâtir](build.md), dans un monde et dans l'Atelier, liste les pièces des mods dans son onglet **Pièces de mods**, un mod à la fois. |

Voir [Jeux moddés](live-mode.md#jeux-moddés) pour le point de vue du joueur.

## worldeditor.json

Certaines choses, l'éditeur ne peut pas les deviner. Un mod peut les lui dire dans un fichier `worldeditor.json` livré
avec lui, n'importe où dans son dossier sous `BepInEx/plugins` (à côté de sa DLL, c'est très bien). Voici le fichier
d'Example Vehicles :

```json
{
  "$schema": "https://raw.githubusercontent.com/Shuiei/valheim-world-editor/main/docs/worldeditor.schema.json",
  "version": 1,
  "extension": "ExampleVehicles.WorldEditor.dll",
  "gameComponents": ["Ship"],
  "data": {
    "ExampleVehicles_Vehicle": { "label": "Vehicle", "readOnly": true, "help": "The vehicle this part is built on." },
    "ExampleVehicles_Offset": { "label": "Place on the vehicle", "readOnly": true },
    "ExampleVehicles_Packed": { "label": "Linked objects", "help": "The objects this one is tied to." }
  },
  "prefabs": {
    "example_boundary": { "hidden": true },
    "example_fleet": { "hidden": true, "data": true },
    "example_canal": { "hidden": true, "data": true },
    "example_hull": { "group": "Hulls" },
    "example_sail": { "group": "Sails and cables" },
    "example_post": { "group": "Sails and cables" },
    "example_pit": { "group": "Digging" }
  }
}
```

| Clé | Ce qu'en fait l'éditeur |
|---|---|
| `version` | La version du format : `1`. Un fichier d'une version ultérieure, ou sans version, est ignoré en entier. |
| `data` | Par clé de données, telle que votre code la nomme (le nom dont le jeu enregistre le hachage ; une clé nommée ici est connue par son nom même quand votre DLL ne la contient pas sous forme de texte) : `label`, le nom que l'[Inspecteur](select.md#inspecteur) donne à la valeur (sinon sa clé ; les noms propres de l'éditeur pour les clés du jeu passent en premier) ; `readOnly`, l'Inspecteur montre la valeur mais ne permet pas de la modifier (des valeurs que votre mod fixe lui-même et qui casseraient si on les modifiait) ; `help`, ce qu'elle est, affiché quand on pointe la valeur. |
| `prefabs` | Par nom de prefab : `hidden`, non proposé dans la liste de [Placement](place.md) ni dans le panneau [Bâtir](build.md) (vos outils, effets, objets auxiliaires ; les objets de ce prefab déjà présents dans un monde restent visibles) ; `group`, le titre sous lequel votre pièce est listée dans l'onglet **Pièces de mods** du panneau Bâtir, après le nom du mod (« *nom du mod* · Hulls »). Sans groupe, une pièce est listée sous sa station d'artisanat, sinon sous « Aucune station requise ». `group` ne compte que pour les pièces propres aux mods. `data` : ses objets ne contiennent que les données de votre mod (un registre, ce qui a été creusé dans un secteur) et n'ont rien à montrer : l'éditeur ne les dessine, ne les sélectionne ni ne les liste jamais dans un secteur (votre extension les lit toujours), et une [réinitialisation de zone](area.md) les garde quand elle garde les constructions, et les retire avec tout le reste sinon. `piece` : une pièce de construction que votre mod crée dans son code, qu'aucun bundle ne contient : `{ "name": "Track 2 m", "cost": { "Stone": 4 }, "station": "Stonecutter" }` (`name` obligatoire, 64 caractères au plus ; `cost` par nom de prefab d'objet, 16 objets au plus ; `station` par son nom anglais). L'onglet **Pièces de mods** du panneau Bâtir la liste sous votre mod même sans jeu en direct (ce que dit un jeu en direct l'emporte). Les pièces de vos bundles n'en ont pas besoin : l'éditeur les trouve par leur `Piece`, ou par leurs points d'aimantation quand vous ajoutez le `Piece` dans le code. |
| `links` | Comment vos objets se lient en un seul : voir [Objets liés](#objets-liés). |
| `ties` | Comment vos objets sont rattachés à un objet qui les nomme (les wagons d'un train) : voir [Objets rattachés](#objets-rattachés). |
| `extension` | La DLL de votre extension d'éditeur, relative au dossier du fichier et à l'intérieur de celui-ci : voir [Extensions](#extensions). |
| `gameComponents` | Noms de classe des scripts propres au jeu dont votre extension a besoin des champs (`"TerrainOp"`, `"Ship"` ; 32 au plus) : tant que votre extension tourne, l'éditeur lit chacun de leurs composants dans les prefabs du jeu depuis le Valheim du joueur (une fois, puis conservés jusqu'à ce que le jeu change) et les lui transmet sous la forme `Host.Game` (voir [Points d'accroche](#points-daccroche)). |
| `settingsFiles` | Noms des fichiers de réglages BepInEx de votre mod que lit votre extension (`"author.mod.cfg"`, dans `BepInEx/config` ; 8 au plus) : à l'ouverture d'un monde, l'éditeur les lit dans le premier des dossiers BepInEx du joueur qui contient chacun et, en direct, dans ceux du jeu connecté, par sa passerelle (ceux d'un serveur, dont ses joueurs reçoivent les réglages synchronisés). Votre extension lit un réglage avec `Host.Setting(file, section, key)` (voir [Points d'accroche](#points-daccroche)). |
| `worldGenerators` | Les générateurs de monde que votre extension enregistre (voir [Générateurs de monde](#générateurs-de-monde)), par l'identifiant que nomment les fichiers de leurs mondes, chacun avec le nom de votre mod pour le joueur : `{ "author.mod": "Some Mod" }` (8 au plus). L'éditeur le lit même quand votre extension ne tourne pas, pour qu'un monde créé par votre mod dise pourquoi il ne peut pas s'ouvrir : le module complémentaire désactivé, en attente de votre mod, ne fonctionnant pas, ou l'extension de votre mod non activée. |

### Comment l'éditeur le lit

- **Où.** Dans chaque dossier `plugins` que l'éditeur connaît : le `BepInEx` du dossier Valheim, le profil de
  gestionnaire de mods dans lequel l'éditeur lui-même a été installé, les dossiers ajoutés dans **Paramètres → Dossiers
  BepInEx**, et chaque profil r2modman et Thunderstore Mod Manager. Chaque fichier nommé `worldeditor.json` qui s'y
  trouve compte, à n'importe quelle profondeur.
- **Quand.** À chaque ouverture d'un monde ou de l'Atelier, et à l'ouverture de **Paramètres**. Un fichier modifié
  pendant qu'un monde est ouvert compte à la prochaine fois.
- **Quel mod.** Un fichier appartient au mod dont le dossier se trouve juste sous `plugins` (le `ExampleVehicles` de
  `plugins/ExampleVehicles/worldeditor.json`). Les Paramètres nomment les extensions d'après lui.
- **Deux fichiers, un nom.** Les fichiers sont lus du plus ancien au plus récent (selon leur date de dernière
  modification) : quand deux disent quelque chose de la même clé de données, du même prefab ou du même `id` de lien, le
  fichier le plus récent l'emporte.
- **Tolérant.** Les commentaires et les virgules finales sont autorisés. Les textes font de 1 à 200 caractères ; les
  listes contiennent au plus 32 noms ; un fichier fait au plus 1 Mo. Une entrée qui n'a pas cette forme est laissée de
  côté et le reste du fichier compte toujours. Un fichier qui n'est pas du JSON, ou pas d'une version que l'éditeur lit,
  est ignoré ; le journal de l'éditeur le signale (« mod manifest …: not a worldeditor.json this editor reads,
  skipped. »).

Le [schéma](../worldeditor.schema.json) décrit le fichier : avec la ligne `$schema` ci-dessus, les éditeurs de code
vérifient le fichier et complètent ses clés pendant que vous tapez.

## Objets liés

Si votre mod construit des objets à partir d'autres (un navire et les pièces construites dessus, une charrette et son
chargement), le jeu enregistre chaque pièce à part, et l'éditeur voit des pièces éparses, empilées là où le jeu les a
enregistrées. Dites-lui comment elles se lient et il les dessine comme votre mod les construit, et déplace, copie et
supprime chaque groupe en entier (voir [Objets liés](select.md#bateaux-et-autres-objets-liés) pour ce que voit le
joueur). Cela fonctionne pour les mods qui lient de la façon courante : la racine (et toute partie à laquelle d'autres
sont accrochées) contient un identifiant entier qui lui est propre ; chaque partie garde, dans ses données,
l'identifiant de ce à quoi elle est accrochée ainsi que son décalage et son orientation dans le repère de celui-ci.

```json
{
  "version": 1,
  "links": [
    {
      "name": "vehicle",
      "id": "ExampleVehicles_Id",
      "parents": ["ExampleVehicles_Mount", "ExampleVehicles_Vehicle"],
      "offset": "ExampleVehicles_Offset",
      "rotation": "ExampleVehicles_Turn",
      "rotationQuaternion": "ExampleVehicles_TurnQ",
      "storedAt": "root",
      "references": ["ExampleVehicles_Target", "ExampleVehicles_Drives"],
      "dropOnCopy": ["ExampleVehicles_Packed", "ExampleVehicles_Aboard"]
    }
  ]
}
```

| Clé | |
|---|---|
| `id` | Clé de données entière de l'identifiant propre d'un objet, que nomment ses parties. Obligatoire. |
| `parents` | Clés de données entières nommant ce à quoi une partie est accrochée, la première renseignée l'emportant (une pièce sur un pivot nomme le pivot avant le véhicule). Au moins une. Obligatoire. |
| `offset` | Clé de données Vec3 de la place d'une partie dans le repère de ce à quoi elle est accrochée, en mètres. Obligatoire. |
| `rotation` | Clé de données Vec3 de son orientation dans ce repère, en angles d'Euler Unity en degrés. |
| `rotationQuaternion` | Clé de données Quaternion de cette orientation, utilisée quand `rotation` n'est pas enregistrée. Sans l'une ni l'autre, une partie n'est pas tournée dans le repère de son parent. |
| `storedAt` | Où le jeu enregistre une partie : `root` (à la racine de son groupe ; votre mod la construit à partir de son décalage quand le groupe se charge ; par défaut) ou `own` (là où elle se trouve). L'éditeur enregistre les parties déplacées de la même façon. |
| `references` | D'autres clés de données entières nommant l'identifiant d'un objet (le mât d'une voile, la cible d'un interrupteur) : dans une copie collée, elles suivent les nouveaux identifiants de la copie, et sont retirées quand ce qu'elles nomment n'a pas été copié. |
| `dropOnCopy` | Clés de données qu'une copie collée laisse de côté : des identifiants empaquetés d'une façon que l'éditeur ne peut pas réécrire (un tableau d'octets), des rattachements qui ne doivent pas être repris (ce qui était à bord). |
| `placements` | Par clé de parent (l'une des `parents`), comment est placée une partie qui y est accrochée quand ce n'est pas comme le dit la règle : ses propres clés `offset`, `rotation` et `rotationQuaternion` et `storedAt` (chacune non donnée : celle de la règle), comme une voile garde sa place sur son mât à part de sa place sur le navire ; ou `"carried": true` (et aucune de celles-ci), une partie que le groupe transporte là où le jeu l'a enregistrée (une charrette à bord) : elle ne garde aucun décalage, est dessinée là où elle est enregistrée par rapport à ce qui la transporte, et est déplacée, tournée, copiée et supprimée avec le groupe et enregistrée là où elle se trouve. |
| `name` | Le nom d'un groupe dans les messages de l'éditeur (« a whole vehicle »). Par défaut : `group`. |

### Comment l'éditeur utilise les liens

- **Affichage.** Une partie est dessinée à son décalage dans le repère de son parent, le parent là où son propre parent
  le met, et ainsi de suite jusqu'à la racine, qui se trouve là où elle est enregistrée : les pièces d'un navire tournent
  et penchent avec la coque. Une partie transportée reste là où elle est enregistrée par rapport à ce qui la transporte.
  Une extension peut tourner le repère dans lequel sont accrochées les parties d'un parent, ou placer une partie
  elle-même (`AddLinkPlacer`, voir [Points d'accroche](#points-daccroche)). Une partie dont le parent (ou n'importe quel parent en
  remontant sa chaîne) n'est pas dans le monde, ou dont la chaîne boucle ou dépasse 16 niveaux (données endommagées),
  reste là où elle est enregistrée.
- **Quelle règle.** Un objet prend la première règle dont il contient les clés `id` ou `parents`. Les noms de prefab ne
  comptent pas : seulement les données. La règle d'un fichier plus récent avec le même `id` remplace celle d'un fichier
  plus ancien, à sa place dans l'ordre.
- **Déplacement et suppression.** Sélectionner n'importe quelle partie et la déplacer, la tourner ou la supprimer
  emporte tout le groupe. Un groupe déplacé garde sa hauteur et ne s'aimante pas ; ses parties sont enregistrées comme
  le dit `storedAt`.
- **Copie.** Une copie emporte tout le groupe. Chaque collage (et chacune de ses répétitions) reçoit de nouveaux
  identifiants qu'aucun objet du monde ne porte, même supprimé, pour que le jeu ne prenne jamais deux copies pour une
  seule ; chaque valeur de parent et de `references` suit. Une partie copiée sans son parent est collée comme un objet
  ordinaire (ses liens retirés), là où elle a été collée. Un collage en miroir met aussi en miroir les décalages et
  orientations des parties. Les valeurs de `dropOnCopy` disparaissent, sauf si le réécriveur de copie d'une extension
  s'en charge.
- Les clés de données sont nommées comme votre code les nomme, comme dans `data`.

## Objets rattachés

Certains mods rattachent des objets entre eux sans conserver la position de chacun par rapport aux autres : un train
nomme les wagons qu'il tire, un pont ne nomme rien mais ce qui est construit dessus nomme le pont. Le jeu enregistre
chaque objet là où il se trouve, et votre mod les réassemble pendant qu'il tourne. Dites à l'éditeur comment ils sont
rattachés et il les garde en un seul groupe :

```json
{
  "version": 1,
  "ties": [
    { "name": "train", "root": "ExampleVehicles_Wagons", "member": "ExampleVehicles_Self", "idBytes": 12 },
    { "name": "deck", "root": "ExampleVehicles_DeckId", "member": "ExampleVehicles_OnDeck" }
  ]
}
```

| Clé | |
|---|---|
| `root` | Clé de données des identifiants que nomme une racine : un int, un long, ou un tableau d'octets contenant plusieurs identifiants à la suite. Obligatoire. |
| `member` | Clé de données de l'identifiant propre d'un membre, du même type (un int, un long, ou un tableau d'octets). Obligatoire. |
| `idBytes` | Pour un tableau d'octets, le nombre d'octets d'un identifiant (de 1 à 64 ; 12 pour les identifiants d'objets du jeu). Sans cette clé, toute la valeur est un seul identifiant. |
| `name` | Le nom d'un groupe dans les messages de l'éditeur. Par défaut : `group`. |

Un membre fait partie du groupe d'une racine quand son propre identifiant est l'un de ceux que nomme la racine. Les
identifiants sont comparés octet par octet, quoi qu'ils signifient pour votre mod : l'éditeur ne les recherche jamais
dans le jeu. Des groupes qui partagent un objet forment un seul groupe : un train dont le wagon plat est la racine d'un
pont emporte ce qui est construit sur le pont.

- **Sélection, déplacement et suppression.** Sélectionner n'importe quel objet d'un groupe et le déplacer ou le tourner
  emporte tout le groupe, d'un bloc : chaque membre garde sa position par rapport aux autres, et est enregistré là où il
  se trouve. Supprimer la racine emporte ses membres ; supprimer un membre n'emporte que lui.
- **Copie.** Une copie emporte tout le groupe. Chaque collage (et chacune de ses répétitions) reçoit de nouveaux
  identifiants du même type et de la même taille, aléatoires, dans la racine et dans les membres qu'elle nomme : une
  copie ne prend donc jamais les membres de l'original ; une liste garde les identifiants des membres copiés, un
  identifiant unique est toujours nouveau, et un membre dont la racine n'a pas été copiée perd son identifiant.
- **En direct et enregistré.** C'est à votre mod de résoudre les nouveaux identifiants : il trouve les membres d'une
  racine collée par les identifiants qu'ils contiennent, comme il le fait après que le jeu a chargé un monde (le jeu
  donne à chaque objet un nouvel identifiant d'objet à chaque chargement).
- Une règle avec les mêmes clés `root` et `member` dans un fichier plus récent remplace celle d'un fichier plus ancien,
  à sa place dans l'ordre.

## Extensions

Ce qu'un fichier ne peut pas exprimer, le code le peut. Un mod peut fournir une **extension d'éditeur** : une petite
DLL, compilée avec le SDK de l'éditeur, que l'éditeur charge pour lire et dessiner les objets du mod. Elle ne tourne pas
dans le jeu, et ne voit jamais le code du jeu ni celui d'Unity : seulement les données des objets, telles que l'éditeur
les a. Elle peut :

- afficher en mots une valeur de type tableau d'octets dans l'Inspecteur ;
- réécrire, dans les copies collées, les identifiants que votre mod empaquette sous sa propre forme ;
- fixer ce que votre mod déduit de la position d'un objet chaque fois que l'éditeur le place, le colle, le déplace ou
  le tourne (la voie sous un véhicule, une orientation que votre mod conserve) ;
- dessiner ce que votre mod construit dans son code pendant que le jeu tourne (une voile à partir de ses coins, un
  câble d'un poteau à l'autre) ;
- placer les parties de vos objets liés là où votre mod les construit quand leurs données seules ne le disent pas (les
  parties sur un pivot que votre mod a tourné) ;
- ne pas dessiner le sol là où votre mod creuse (l'entrée d'un tunnel, une fosse) ;
- dessiner ce que votre mod fait d'un secteur dans son ensemble (un canal, de la roche creusée dans une colline) avec
  l'aspect du sol du jeu, à la place du sol de l'éditeur ;
- lire les réglages des scripts propres au jeu (ses `gameComponents`), tels que les a le Valheim du joueur ;
- lire les propres réglages de votre mod (ses `settingsFiles`), tels que les a le jeu du monde ;
- ajouter votre propre outil dans la barre d'outils de l'éditeur 3D, avec sa fiche de réglages, son aperçu et ses
  modifications dans l'historique (creuser un canal là où le joueur clique) ;
- pour un mod de génération de monde, lire et dessiner les mondes qu'il crée, et en créer de nouveaux avec lui ;
- en mode direct, demander au propre plugin de votre mod dans le jeu ce que seul le jeu sait (voir
  [Mode direct](#mode-direct)).

### En créer une

1. **Récupérez le SDK** depuis une [version publiée](https://github.com/Shuiei/valheim-world-editor/releases) :
   `ValheimWorldEditor.Sdk-<version>.zip` contient `ValheimWorldEditor.Sdk.dll` (netstandard2.0, ce avec quoi une
   extension se compile ; sa version net10.0 dans `net10.0/` contient aussi le modèle des règles de monde, pour une
   extension sur le .NET de l'éditeur qui crée des règles de monde) et `WorldEditorBridge.Api.dll` (ce avec quoi se
   compile le plugin d'un mod pour le [mode direct](#mode-direct)), chacun avec son fichier de documentation pour les
   éditeurs de code, cette page sous le nom `README.md`, et tout l'exemple Example Vehicles dans `sdk/samples/` (il se
   compile tel quel).
2. **Créez une bibliothèque de classes netstandard2.0** (net10.0, avec `net10.0/ValheimWorldEditor.Sdk.dll`, pour une
   extension qui crée des règles de monde) qui référence `ValheimWorldEditor.Sdk.dll` sans la copier dans votre sortie
   (`Private="false"`) : l'éditeur fournit la sienne. N'utilisez rien d'autre que le framework (le projet de l'exemple
   référence aussi le paquet `System.Numerics.Vectors`, pour `Vector3` et `Quaternion`). Une extension est **une seule
   DLL** : l'éditeur ne charge aucun autre fichier de votre dossier, donc une bibliothèque auxiliaire posée à côté n'est
   jamais trouvée.
3. **Marquez l'assembly** et donnez-lui un point d'entrée : une classe publique avec un constructeur sans paramètres,
   qui implémente `IWorldEditorExtension`. L'éditeur la crée une fois et appelle `Register` une fois, avant
   l'utilisation de tout point d'accroche. Voici celle de l'exemple :

   ```csharp
   [assembly: WorldEditorExtension(typeof(ExampleVehicles.WorldEditor.Extension))]

   public sealed class Extension : IWorldEditorExtension
   {
       public void Register(IExtensionRegistry registry)
       {
           var packed = new PackedIds();
           registry.AddDecoder(Keys.Packed, packed);           // inspector
           registry.AddCopyRewriter(packed, Keys.Packed);      // paste
           registry.AddShapes(new Sails(), Keys.Sail);         // 3D view
           registry.AddShapes(new Cables(), Keys.Post);
           registry.AddGroundCuts(new Pits(), Keys.Pit);       // the ground
           registry.AddScene(new Canals());                    // the area as a whole
           registry.AddTool(new CanalTool());                  // a tool on the rail
           registry.AddAreaCopier("canals", new CanalCopies()); // copy and paste
           registry.AddLiveListener(new LiveLink(registry));   // live mode
           registry.Log($"ready (editor {registry.EditorVersion}, API {registry.ApiVersion})");
       }
   }
   ```

4. **Livrez-la** avec votre mod, dans son dossier, et nommez-la dans votre `worldeditor.json` :
   `"extension": "ExampleVehicles.WorldEditor.dll"`. Un chemin qui sort du dossier du fichier, ou un fichier qui ne se
   termine pas par `.dll`, n'est pas proposé.

### L'activer : Paramètres → Extensions de mods

Une extension est du code qui tourne dans l'éditeur avec les droits du joueur : **c'est le joueur qui décide**.
L'éditeur trouve les extensions proposées par les mods de ses dossiers de plugins et les liste dans **Paramètres**, sous
**Extensions de mods** : un interrupteur par extension, nommé d'après le dossier du mod et le fichier
(« ExampleVehicles · ExampleVehicles.WorldEditor.dll »). Le pointer affiche le chemin du fichier et son SHA-256. Sous
chaque interrupteur, l'éditeur indique ce qu'il en est advenu : « En cours d'exécution. », « Ne fonctionne pas : … »
avec la raison, ou « Désactivée : the file cannot be read. » (le fichier ne peut pas être lu). Quand aucun mod n'en propose : « Aucun mod des
dossiers ci-dessus n'en propose. »

- **Désactivée par défaut.** Une extension ne tourne qu'une fois que le joueur l'active et appuie sur **Enregistrer** ;
  elle tourne dès lors, et celles qui sont désactivées s'arrêtent.
- **Ce fichier seulement.** L'éditeur retient le SHA-256 du fichier. Une DLL modifiée (une mise à jour du mod) est un
  autre code : elle est désactivée jusqu'à ce que le joueur la réactive. Les octets vérifiés sont les octets chargés.
- **Pourquoi elle ne fonctionne pas.** « it is not marked as an extension ([assembly: WorldEditorExtension]) »
  (elle n'est pas marquée comme extension), « it needs a newer editor (API …; this one has …) » (elle demande un
  éditeur plus récent), « its entry … is not an IWorldEditorExtension » (son point d'entrée n'en est pas un), ou
  l'exception levée par `Register`.

### Points d'accroche

| Point d'accroche | Ce qu'en fait l'éditeur |
|---|---|
| `AddDecoder(key, IDataDecoder)` | L'Inspecteur affiche une valeur de type tableau d'octets avec vos mots, sous la valeur (au lieu de sa seule taille). `Describe` renvoie quelques lignes courtes, ou `null` quand les octets ne sont pas sous une forme qu'il lit. Quand deux extensions lisent une même clé, la première qui donne un texte l'emporte. |
| `AddCopyRewriter(ICopyRewriter, keys)` | Chaque objet d'une copie collée qui contient l'une des clés vous est transmis (`ICopy`), après la liaison propre de l'éditeur (`links`), avec les nouveaux identifiants de la copie pour ceux des originaux (`IIdMap.TryGetNew` ; false quand l'objet qui portait l'ancien identifiant n'a pas été copié). Changez ses valeurs sur place (`SetInt`, `SetQuaternion`, `SetBytes`… ; `null` retire une valeur). Les clés sont à vous : `dropOnCopy` ne les retire plus. |
| `AddAreaCopier(id, IAreaCopier)` | Ce que votre mod conserve d'un endroit et qu'aucun objet copié ne porte (un registre de la roche creusée, dans des porteurs de données que l'éditeur ne copie jamais) accompagne les copies de l'éditeur. Quand le joueur copie (la sélection de l'outil Région, ou les objets de l'outil Sélection), `Copy(area, region)` reçoit le secteur et ce qui est copié (`CopyRegion` : son `Outline` et `Contains(x, z)`, en mètres du monde, et son `Middle`) et renvoie des octets de votre cru (ou `null`) : conservez les positions sous forme de décalages par rapport au milieu, car la copie peut être collée n'importe où, tournée et mise en miroir, dans un autre monde aussi. Les octets accompagnent la copie et vont dans les fichiers de plans, sous le nom de votre mod et l'`id` (lettres, chiffres, points, tirets et tirets bas). Au collage, `Paste(area, bytes, copies)` est appelé une fois avec chaque copie que pose le collage (`CopyPlacement` : son `Middle`, son `Turn` et son `Mirror` ; `Place(offset)` transforme un décalage par rapport à l'ancien milieu en point du monde où il arrive, `Yaw(yaw)` une orientation), et le secteur tel que le collage laisse son sol ; le `ToolEdit` que vous renvoyez fait partie de l'étape d'annulation du collage. Les octets dont le copieur ne tourne pas (votre extension non installée) sont laissés de côté au collage. |
| `AddPlacer(IObjectPlacer, prefabs)` | Les objets des prefabs que l'éditeur met à un nouvel endroit (placés, collés, déplacés ou tournés, par n'importe quel outil, un script ou Claude) vous sont transmis une fois par étape, tous ensemble (`ICopy`, chacun à sa nouvelle place, ses valeurs telles qu'elles seront créées, celles d'une copie collée après les réécriveurs de copie), avec le secteur tel qu'il est avant l'étape, sans les objets qu'elle déplace : fixez ce que votre mod déduit de la position d'un objet (une orientation qu'il garde à part de celle de l'objet, la voie sur laquelle se trouve un véhicule, l'arrêt que nomme un panneau), comme le ferait votre mod quand un joueur le construit. Ce que vous fixez est enregistré avec l'étape et appliqué en direct avec elle. `SetTurn` tourne l'objet tel qu'il sera créé, pour un mod qui garde ses objets non tournés et leur forme dans leurs valeurs (tournez les valeurs, puis l'objet en sens inverse ; éditeur 1.119.0 et ultérieur). Les objets modifiés sur place (l'Inspecteur, la modification d'un outil) ne sont pas un placement. |
| `AddShapes(IShapeProvider, prefabs)` | Les objets des prefabs sont aussi dessinés avec vos formes : des `Shape`, des triangles dans le repère de l'objet (en mètres ; x à droite, y vers le haut, z vers l'avant ; la place, l'orientation et la taille de l'objet appliquées) ou dans celui du monde (`WorldSpace = true`), d'une couleur chacun (rouge, vert, bleu, alpha de 0 à 1 ; alpha sous 1 : transparent), dans le sens horaire vus de face comme dans Unity, ou visibles des deux côtés (`BothSides = true`, pour une voile ou un tissu). Ils sont dessinés avec le type de l'objet (masqués quand le panneau Vue le masque). Un objet dont l'éditeur n'a pas de modèle est dessiné par vos seules formes (pas de boîte de remplacement), et sélectionné et souligné par la boîte des formes que vous donnez pour l'un de ses prefabs sans données (celles dans son propre repère). Une forme dont un triangle nomme un coin qui lui manque, ou dont un coin n'est pas un nombre, n'est pas dessinée. |
| `AddLinkPlacer(ILinkPlacer, prefabs)` | Les parties liées (les `links` de votre manifeste) placées là où votre mod les construit quand leurs données seules ne le disent pas. `FrameOf(obj)`, pour un objet des prefabs qui a des parties : le repère dans lequel elles sont accrochées, dans son propre repère (`LinkFrame` : un décalage et une orientation ; la plateforme d'un pivot telle que votre mod l'a tournée ou déplacée), ou `null` pour son propre repère. `PlaceOf(part, parent)`, pour une partie des prefabs : sa position dans le repère de ce à quoi elle est accrochée, à la place de son décalage et de son orientation (la partie à la place et avec l'orientation où le jeu l'a enregistrée, son parent tel qu'il est dessiné ; le repère du parent n'est alors pas appliqué), ou `null` pour la place que donnent ses liens. Le déplacement, la copie et la suppression suivent toujours les liens. |
| `AddGroundCuts(IGroundCutter, prefabs)` | Le sol n'est pas dessiné à l'intérieur des contours (`GroundCut`) que vous donnez pour les objets des prefabs : des points au sol (x, z) dans le repère de l'objet, ou dans celui du monde (`WorldSpace = true`) ; au moins trois, dans n'importe quel sens (un contour qui se croise découpe selon la règle pair-impair). Un clic à cet endroit passe par le trou. Le sol de l'éditeur est une carte des hauteurs avec un point par mètre : la découpe retire le sol entre les points situés à l'intérieur du contour, donc son bord suit cette grille de 1 m, et ce qui apparaît à travers est ce que vous dessinez en dessous (des formes) et le dessous du sol. |
| `AddScene(ISceneProvider)` | Ce que votre mod fait d'un secteur dans son ensemble, dessiné dans celui-ci : `Build(area)` est appelé sur un thread de travail chaque fois qu'un secteur s'ouvre et de nouveau chaque fois que ses objets, son sol ou le jeu changent (ou que vous appelez `Host.Redraw()`), et renvoie un `SceneLayer` (ou `null`). Ses `Meshes` (`SceneMesh` : des coins et leurs normales dans le repère du monde, en mètres, des triangles dans le sens horaire vus de face comme dans Unity) sont dessinés avec l'aspect du sol du jeu quand ils ont des `BiomeColors` (la couleur de terrain de chaque coin, telle que la donne `area.BiomeColor`) et peints d'après leur `Mask` (une petite texture du masque de peinture du terrain, `MaskTexture`) aux `MaskUvs` ; sans couleurs de biome, en `Color`. Les clics s'y arrêtent (sauf si `Pickable = false`). Ses `Holes` (`GroundHole` : des rectangles en mètres entiers) laissent le sol de l'éditeur non dessiné, case par case, exactement jusqu'à leurs bords : dessinez ce qui le remplace. L'`area` (`IAreaView`) donne ses limites, ses objets d'un prefab tels qu'ils sont avec les modifications pas encore enregistrées (`ObjectsOf`, vos porteurs de données compris), la hauteur du sol telle que le jeu la montre (`GroundHeight`, entre les points espacés d'un mètre, le long des propres triangles du jeu), le biome, si un point est dans le Grand Nord ou les Terres cendrées, et la couleur et la peinture du terrain à un mètre entier (`BiomeColor`, `GroundMask`). Recalculez tout à partir du secteur à chaque fois ; ne gardez rien entre les appels. |
| `AddTool(IEditorTool)` | Votre propre outil dans la barre d'outils de l'éditeur 3D, sous **MODS**, avec son `Info` (`ToolInfo` : un identifiant, unique dans votre extension ; son nom dans la barre d'outils ; une ligne d'aide dans son infobulle ; `Icon`, un chemin SVG dessiné en 16 × 16, sinon la première lettre de votre nom). Une fois choisi, il affiche une carte de ses `Controls` : `SliderControl` (min, max, valeur, `Step`, `Unit`), `ChoiceControl` (une liste), `ToggleControl`, `ButtonControl` et `TextControl` (une ligne que vous changez avec `SetText`) ; les valeurs du joueur sont retenues d'une session à l'autre. L'éditeur appelle `OnChosen` et `OnLeft` quand il est choisi et quitté, `OnControl(id)` quand une valeur change ou qu'un bouton est pressé, `OnPointer` à chaque mouvement, appui, glisser et relâchement au-dessus de la vue 3D (`PointerInput` : le rayon, les touches maintenues, et ce qu'il vise : le sol, votre couche de scène, ou une chose, avec le point et la normale du sol à cet endroit), et `OnKey` à chaque touche pressée (le nom de la touche, comme "Enter", "Escape", "Back" ou "A" ; renvoyez true quand vous l'avez utilisée, false la laisse à l'éditeur, de sorte que Ctrl+Z annule toujours). L'`IToolContext` transmis à chaque appel donne le secteur (`Area`, comme pour les couches de scène), les valeurs de vos contrôles (`Number`, `Choice`, `IsOn`), des formes dessinées sur le secteur jusqu'à ce que vous les changiez (`Preview`, dans le repère du monde), une ligne dans la barre d'état (`Status`) et `Commit(label, ToolEdit)` : des objets ajoutés (un prefab, une place, une orientation et ses données fixées par un `ICopy`, que `SetTurn` peut tourner), modifiés (une copie d'un objet que le secteur vous a donné, avec vos modifications) et retirés, en une étape de l'historique du secteur, annulée et enregistrée comme toute autre modification. Vos porteurs de données sont conservés avec l'historique comme les autres objets, bien qu'ils ne soient jamais dessinés ; un objet que vous n'avez pas obtenu du secteur est laissé tel quel. |
| `AddLiveListener(ILiveListener)` | Prévenu quand l'éditeur se connecte à un jeu (`OnConnected`, avec le jeu) et quand cela prend fin (`OnDisconnected` : le monde a été fermé, ou un autre ouvert). Voir [Mode direct](#mode-direct). |
| `AddWorldGenerator(IWorldGenerator)` | Les mondes de votre mod de génération de monde (voir [Générateurs de monde](#générateurs-de-monde)). |
| `AddWorldRules(IWorldRulesProvider)` | Les règles avec lesquelles les mondes sont générés, d'après les réglages et les fichiers de données de votre mod de génération de monde (voir [Règles de monde](#règles-de-monde)). |
| `Host` | L'éditeur, en dehors des points d'accroche : `Host.Live`, le jeu connecté (null quand il n'y en a pas : un monde enregistré, l'Atelier) ; `Host.Game`, les composants du jeu que demandent les `gameComponents` de votre manifeste (`Components("Ship")` : chacun avec son prefab, l'objet sur lequel il se trouve et ses champs par nom, tels que le jeu les enregistre ; prêts une fois qu'un monde ou l'Atelier est ouvert, donc demandez-les depuis vos points d'accroche, pas depuis `Register`) ; `Host.Setting(file, section, key)`, un réglage d'un fichier que nomment les `settingsFiles` de votre manifeste, sous forme du texte que contient le fichier (null quand le fichier ou le réglage n'existe pas : utilisez la valeur par défaut de votre mod ; prêt, comme `Host.Game`, une fois qu'un monde ou l'Atelier est ouvert) ; `Host.BepInExFolders`, les dossiers BepInEx du joueur que l'éditeur connaît, dans l'ordre où il les consulte (chacun avec son `Path` et son emplacement, pour le joueur) ; `Host.DataFolder`, le dossier de données de l'éditeur (gardez vos propres fichiers dans un dossier à vous à cet endroit) ; `Host.World` (extensions net10.0), les règles de monde et les objets propres au jeu (voir [Règles de monde](#règles-de-monde)) ; et `Host.Redraw()`, pour que vos formes et découpes soient refaites à l'image suivante (après que le jeu vous a appris quelque chose de nouveau). |
| `Log(message)` | Écrit une ligne, au nom de votre mod (« extension ExampleVehicles: … »), dans le journal de l'éditeur, `ValheimWorldEditor.log` dans son dossier de données (**Ouvrir le journal** sur la page d'accueil ; recommencé à chaque lancement). |

Les points d'accroche de formes et de découpes reçoivent aussi le monde autour de l'objet (`IWorldView`) :
`TryFind(idKey, id, out obj)` trouve un autre objet par un identifiant qu'il contient, pour une corde ou un câble qui
l'atteint. Seuls les identifiants sous une clé qu'une règle `links` nomme comme son `id` sont trouvés (l'éditeur indexe
ceux-là). Dans l'éditeur 3D, ce monde est aussi un `IGroundView` (`world is IGroundView ground`) :
`TryGroundHeight(x, z, out height)` donne la hauteur du sol à cet endroit telle que l'éditeur la dessine, pour une forme
qui descend jusqu'à lui (une échelle de corde), false hors du secteur.

Les objets arrivent sous forme d'`IObjectView` : le nom du prefab ("" quand l'éditeur ne le connaît pas), la place,
l'orientation (angles d'Euler Unity en degrés, et `Orientation`, un quaternion) et la taille telles que dessinées (pour
une partie liée, là où votre mod la construit, pas là où le jeu l'enregistre), et chaque valeur de ses données par clé :
`TryGetInt`, `TryGetLong`, `TryGetFloat`, `TryGetVector3`, `TryGetQuaternion`, `TryGetString`, `TryGetBytes`. Les clés
et les prefabs sont nommés comme votre code les nomme ; l'éditeur les hache comme le fait le jeu.

### Générateurs de monde

Un mod de génération de monde remplace le sol du jeu, et marque les mondes qu'il crée : leur fichier de monde commence
par -1 à l'emplacement de la version de fichier du jeu, puis l'identifiant du générateur (une chaîne), puis tout ce que
le mod conserve (Riverheim avant 1.3 : la version de fichier du jeu, puis son préréglage). L'éditeur refuse un tel
monde sauf si une extension en cours d'exécution enregistre un générateur avec cet identifiant, comme le jeu sans le mod
le refuse : listé, grisé, avec la raison (nommez les identifiants dans les `worldGenerators` de votre manifeste pour que
l'éditeur dise ce qui manque).

Un mod qui se branche sur [Worldhalla](https://thunderstore.io/c/valheim/p/Gurebu/Worldhalla/) (Riverheim depuis 1.3) a
ses mondes marqués par Worldhalla à la place, avec l'identifiant et les réglages du générateur dans l'en-tête propre à
Worldhalla. Enregistrez pour eux un générateur avec l'identifiant `worldhalla:` suivi de l'identifiant de votre
générateur dans Worldhalla (celui de Riverheim : `worldhalla:dev.gurebu.riverheim`) : son `ReadHeader` reçoit la version
de fichier du jeu, puis vos réglages tels que Worldhalla les conserve, et doit les lire en entier. Un monde dont
l'en-tête Worldhalla provient d'un Worldhalla plus récent que 1.0 (son format, ou des réglages propres à Worldhalla) est
refusé avec la raison. Votre `IWorldGenerator` donne :

- `Id`, `PluginId` (le GUID BepInEx du mod : en mode direct, on vous indique la version que le jeu fait tourner) et
  `Name` ;
- `ReadHeader(file, out fileVersion)` et `WriteHeader(file, fileVersion, settings)` : ce qui suit l'identifiant, lu et
  écrit sous les formes du jeu (`IWorldFileReader`, `IWorldFileWriter`), vos réglages conservés sous forme de
  `GeneratorSettings` (une description pour le joueur, et une valeur de votre cru). Levez `InvalidDataException` avec la
  raison pour le joueur pour un en-tête que vous ne pouvez pas lire ;
- `Problem(settings)` : pourquoi un tel monde ne peut pas être dessiné, rapidement (demandé pour chaque monde que liste
  la page d'accueil), ou null ;
- `Open(settings, gamePluginVersion)` : prêt à disposer des mondes, à l'ouverture d'un monde (`IWorldLayout` : votre
  `Version`, si la disposition est lente (`Slow`), et `LayOut(seed)`) ; levez `InvalidOperationException` avec la raison
  quand vous ne pouvez pas ;
- `LayOut(seed)` donne un `IGroundLayout` : le biome (la valeur du jeu), la hauteur avec son masque et le facteur de
  forêt en un point, à la place du corps des fonctions GetBiome, GetBiomeHeight, GetAshlandsHeight et GetForestFactor
  du jeu (demandés depuis de nombreux threads). L'étirement d'Expand World passe toujours avant vos réponses, ses
  multiplicateurs de profondeur et de forêt après, comme les patchs des mods se rencontrent dans le jeu ; l'éditeur ne
  dispose aucune eau du jeu ;
- `NewWorldSettings()` : les réglages que reçoit un nouveau monde (la page Un nouveau monde propose votre générateur,
  choisi en premier), ou null ;
- `Notes(settings, expandWorld)` : ce que les réglages d'Expand World (`ExpandWorldSettings`) font par ailleurs à vos
  mondes, pour le journal.

Toute autre exception que lève votre générateur est une faute : son extension est désactivée, et le joueur est informé
que le module complémentaire a échoué.

### Règles de monde

Un mod de génération de monde qui change la génération propre au jeu par des réglages et des fichiers de données (un
monde plus grand, ses propres biomes, lieux et végétation, comme le fait Expand World) ne dispose pas le monde
lui-même : le générateur de l'éditeur suit les règles qu'on lui donne. Compilez votre extension pour net10.0 (la
version `net10.0` du SDK contient le modèle des règles, `WorldRules` et les types qu'il contient, dans l'espace de noms
`ValheimGen`) et enregistrez un `IWorldRulesProvider` :

- `ForWorld(world)` : les règles avec lesquelles un monde s'ouvre. Le `WorldRulesRequest` donne son `Name`, les
  `StartingGlobalKeys` que garde son fichier de monde (les réglages d'un mod qui y sont stockés), les `LoadedGlobalKeys`
  que contient sa sauvegarde, la `GridWidth` sur laquelle reposent ses fichiers de chunks, et le jeu dans lequel il
  tourne quand il est en direct (`Live`, sinon null) : lisez alors les fichiers du mod depuis le jeu, par
  `Live.Plugins()`, `Live.ConfigFile(path)` et `Live.ConfigFolder(path)` (sous son BepInEx/config ; null avec un
  WorldEditorBridge antérieur à 1.54.0), sinon depuis `Host.BepInExFolders` ;
- `ForNewWorld()` : les règles avec lesquelles un nouveau monde est créé et examiné (la page Un nouveau monde).

Renvoyez null (ou `WorldRules.Vanilla`) quand votre mod ne change pas le monde (non installé, ou à ses valeurs par
défaut) : le fournisseur suivant est interrogé, et s'il n'y en a aucun, le monde est celui du jeu. Les règles de lieux,
de végétation et de salles sont établies par rapport à celles du jeu (`Host.World` : ses `Locations` une fois que
`ReadLocations()` les a lus, sa `Vegetation`, `HasPrefab(name)` et `IsGameRoom(name)`).
`WorldRulesText.Describe(rules)` dit en quelques mots ce que les règles changent, pour vos lignes de journal. Un
fournisseur qui lève une exception est une faute : son extension est désactivée, et le suivant est interrogé.

### Règles

- **Soyez rapide, ne gardez aucun état.** Les points d'accroche sont appelés depuis plusieurs threads à la fois et
  souvent (à chaque redessin des formes et des découpes, pour chaque objet collé) : ne gardez aucun état entre les
  appels (ou verrouillez-le), et rendez la main vite.
- **Ne levez jamais d'exception.** Un point d'accroche qui lève une exception désactive l'extension jusqu'au prochain
  démarrage de l'éditeur (les Paramètres disent pourquoi, ainsi que le journal) ; l'éditeur continue sans elle. Dans le
  code qui continue après le retour d'un point d'accroche (un appel `async` au jeu), interceptez tout vous-même, comme
  le fait le `LiveLink` de l'exemple.
- **Versions.** `SdkInfo.ApiVersion` est la version de l'API (actuellement 1), et l'attribut enregistre celle avec
  laquelle vous avez compilé. Un éditeur fait tourner les extensions compilées avec sa version ou une version
  antérieure. Les nouveaux points d'accroche arrivent sous forme d'ajouts ; la version ne change que lorsque l'API
  casse.

## Modules complémentaires

Une extension est livrée avec son mod. Quand l'auteur d'un mod n'en a pas fait (ou que le mod n'est plus maintenu),
n'importe qui peut en faire une : un **module complémentaire** est un `worldeditor.json` et une extension pour **le mod
de quelqu'un d'autre**, publiés séparément, que le joueur installe à côté de l'éditeur. Le mod lui-même reste tel quel,
dans `BepInEx/plugins` ; le module complémentaire ne fait que le faire connaître à l'éditeur.

```
ValheimWorldEditor/                le dossier de l'éditeur (sous macOS : le dossier qui contient ValheimWorldEditor.app)
  ValheimWorldEditor(.exe)
  addons/
    Tunnels/                       un dossier par module complémentaire, nommé comme vous voulez
      worldeditor.json
      Tunnels.WorldEditor.dll
```

- **Son fichier** est un `worldeditor.json` comme ci-dessus (libellés de données, prefabs, liens, `extension`,
  `gameComponents`), avec une clé de plus : `"serves": "TheirMod.dll"`, le nom de fichier de la DLL du mod. Le module
  complémentaire est utilisé tant qu'une DLL de ce nom se trouve dans l'un des dossiers de plugins que l'éditeur connaît
  (Paramètres, Dossiers BepInEx), n'importe où sous `plugins`, et laissé de côté sinon. Sans `serves`, il est toujours
  utilisé (un outil de votre cru, pour aucun mod en particulier). Un `serves` qui n'est pas un simple nom de fichier se
  terminant par `.dll`, ou un fichier d'une version que cet éditeur ne lit pas, écarte le module complémentaire (le
  journal dit pourquoi).
- **Son extension** se crée comme n'importe quelle autre (voir [En créer une](#en-créer-une)) : une seule DLL, dans le
  dossier du module complémentaire, avec les mêmes points d'accroche et les mêmes règles.
- **Activé dès l'installation.** Installer un module complémentaire est le choix du joueur : il tourne donc sans avoir à
  être activé dans les Paramètres. Les Paramètres listent les modules complémentaires sous **Modules complémentaires**,
  chacun avec un interrupteur qui le désactive sans le supprimer (rien de lui n'est alors utilisé), et avec ce qu'il est
  advenu de chacun : « En attente de son mod : TheirMod.dll n'est pas dans les dossiers de plugins ci-dessus. », « En
  cours d'exécution. », « Ne fonctionne pas : … », qu'il n'apporte que des notes, ou qu'il est désactivé.
- **Ce que dit le mod lui-même l'emporte.** Les fichiers des modules complémentaires sont lus avant ceux des mods : si
  le mod livre un jour son propre `worldeditor.json`, ce qu'il dit remplace ce que disait le module complémentaire (une
  extension propre au mod demande alors l'interrupteur du joueur, comme toujours).
- **Publiez-le sous forme de `.zip` contenant son dossier**, que les joueurs décompressent dans `addons`. Les paquets de
  l'éditeur sont livrés avec ce dossier, vide à part un `README.txt`.
- **Le dépôt de l'éditeur contient quelques modules complémentaires** (`addons/`, chacun publié dans son propre zip à
  côté des paquets de l'éditeur : voir [development.md](../development.md#add-ons-in-this-repository)) ; le vôtre peut
  se trouver n'importe où.

## Mode direct

Connecté à un jeu en cours (mode direct), l'éditeur applique ses modifications par le plugin WorldEditorBridge, qui
détruit, crée et déplace des objets dans le jeu. Un mod qui construit des choses à partir de ses objets pendant que le
jeu tourne (un véhicule à partir de ses parties, un tunnel à partir de ce qui a été creusé) ne voit les modifications de
l'éditeur qu'au chargement suivant, sauf si son propre plugin y participe. La passerelle offre deux choses aux plugins
des mods, par `WorldEditorBridge.Api.dll` : elle est livrée avec la passerelle, dans son dossier `plugins`, et dans le
zip du SDK, et ne contient aucun type du jeu ni d'Unity, si bien qu'une mise à jour du jeu ne peut pas la casser.

### Côté plugin

Enregistrez-vous depuis l'`Awake` de votre plugin, avec une dépendance souple sur la passerelle, et gardez le code qui
touche l'API dans une classe à part : votre plugin se charge alors aussi sans la passerelle. Extrait de
`sdk/samples/ExampleVehicles.Bridge` dans l'exemple :

```csharp
[BepInPlugin(PluginId, "ExampleVehicles", "1.0.0")]
[BepInDependency(BridgeApi.BridgeGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ExampleVehiclesBridgePlugin : BaseUnityPlugin
{
    public const string PluginId = "ExampleVehicles";

    private void Awake()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Register(Logger);   // a class of its own: this plugin loads without the bridge
        }
    }

    private void OnDestroy()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Unregister();       // BridgeApi.Remove(PluginId)
        }
    }
}

internal static class BridgeLink
{
    public static void Register(BepInEx.Logging.ManualLogSource log)
    {
        BridgeApi.AddRoutes(ExampleVehiclesBridgePlugin.PluginId, new Routes());          // answers the editor's calls
        BridgeApi.AddListener(ExampleVehiclesBridgePlugin.PluginId, new Listener(log));   // told what the editor applied
    }

    public static void Unregister() => BridgeApi.Remove(ExampleVehiclesBridgePlugin.PluginId);
    // Routes and Listener: see the sample.
}
```

| Point d'accroche | Ce qu'en fait la passerelle |
|---|---|
| `AddListener(modId, IBridgeListener)` | `OnApplied(AppliedChanges)` est appelé sur le thread principal du jeu juste après chaque application de l'éditeur, avec ce qu'elle a changé : `Created` et `Destroyed`, les objets créés et détruits (chacun avec le `User` et l'`Id` de son ZDOID, le hachage de son prefab et sa position) ; `Moved`, les objets déplacés sous leur propre ZDOID (passerelle 1.40.0 ou ultérieure : `From` où chacun se trouvait, `To` où il se trouve maintenant ; un objet dont un joueur a chargé à la fois l'ancienne et la nouvelle place est recréé à la place, et figure dans `Created` et `Destroyed` ; depuis 1.97.0, un objet dont l'éditeur a modifié les données y figure aussi, ses nouvelles données déjà appliquées) ; `TerrainZones`, les zones dont le sol a été réécrit ; `ResetZones`, les zones vidées pour être générées à nouveau. Réassemblez vos choses ici. Une exception est consignée dans le journal du jeu et ne change rien à ce qu'a fait l'éditeur. |
| `AddRoutes(modId, IBridgeRoutes)` | `Handle(name, body)` répond à `POST /mods/<modId>/<name>` sur le thread principal du jeu, dans les octets dont vous et votre extension d'éditeur convenez ; `null` répond 404 (« the mod has no route of that name »), une exception 500 avec son message. La passerelle attend au plus 30 secondes le thread principal : soyez rapide, ou répondez « started » et laissez l'extension redemander. Le corps d'un appel fait au plus 16 Mo. |
| `Remove(modId)` | Retire vos routes et votre écouteur (un plugin qui s'arrête). |

Les objets que la passerelle crée, déplace ou modifie sont transmis aux mods du jeu comme le jeu leur transmet un objet
venu d'un autre jeu : leurs données repassent par `ZDO.Deserialize` (les mêmes valeurs). Un mod qui apprend l'existence
des objets en le patchant (le ZdoWatcher de ValheimRAFT, qui trouve les navires et ce à quoi sont attachées les cordes
par leurs identifiants) connaît aussitôt un objet collé, sans écouteur à lui.

S'enregistrer de nouveau sous le même identifiant remplace ce qui avait été enregistré. L'identifiant du mod est à vous
(celui de votre plugin convient bien) : de 1 à 100 lettres, chiffres, points, tirets et tirets bas ; un autre
identifiant lève une exception. `BridgeApi.Version` est la version de l'API (actuellement 1) ; une passerelle fait
tourner les plugins compilés avec sa version ou une version antérieure, et les ajouts la conservent. `GET /mods` sur la
passerelle liste la version de l'API et les identifiants enregistrés.

### Côté extension

Votre extension accède au jeu par `ILiveGame` : depuis `registry.Host.Live` à tout moment (null quand l'éditeur n'est
pas connecté), ou dans le `OnConnected` de votre écouteur en direct.

- `Mods` : les identifiants enregistrés auprès de la passerelle, tels que la passerelle les a signalés à l'ouverture du
  monde (un plugin qui s'enregistre plus tard est vu à la connexion suivante).
- `CallAsync(modId, name, body)` : appelle votre route et rend les octets de sa réponse. Il lève une exception quand le
  mod ou la route n'est pas enregistré, quand la route a échoué (avec le message du jeu), ou quand le jeu est
  injoignable.
- `Plugins()`, `ConfigFile(path)` et `ConfigFolder(path)` : les plugins BepInEx que le jeu a chargés, et un fichier ou
  les entrées d'un dossier sous son BepInEx/config, en lecture seule (null avec une passerelle antérieure à 1.54.0, et
  pour un chemin qui n'existe pas). Ils lèvent `InvalidOperationException` quand la passerelle refuse le chemin (hors du
  dossier, plus de 4 Mo).

Demandez au jeu ce que lui seul sait (ce que votre mod a construit dans son code), gardez-le, et appelez
`registry.Host.Redraw()` pour que vos formes et découpes soient refaites. Le `LiveLink` de l'exemple appelle la route
`ping` du plugin quand l'éditeur se connecte et consigne la réponse ; son plugin répond aussi à `players` (combien de
joueurs sont dans le jeu).

Quand un monde s'ouvre en direct, le journal de l'éditeur nomme les mods enregistrés (« bridge mods: … »).

## L'exemple Example Vehicles

Le zip du SDK (et le dépôt de l'éditeur) contient le code source des deux moitiés d'un mod inventé, à copier. Example
Vehicles construit des véhicules à partir d'une coque et des parties construites dessus.

| Fichier | Ce qu'il montre |
|---|---|
| `sdk/samples/ExampleVehicles.WorldEditor/worldeditor.json` | Le fichier du mod : libellés, un objet auxiliaire masqué, des groupes, la règle de lien `vehicle`, l'extension. |
| `sdk/samples/ExampleVehicles.WorldEditor/Extension.cs` | Le point d'entrée, qui enregistre un point d'accroche de chaque type. |
| `sdk/samples/ExampleVehicles.WorldEditor/Keys.cs` | Les clés de données et prefabs du mod, tels que son code les nomme. |
| `sdk/samples/ExampleVehicles.WorldEditor/PackedIds.cs` | Un décodeur et un réécriveur de copie pour une liste d'identifiants empaquetés dans un tableau d'octets (un octet de version, puis quatre octets par identifiant) : affichée dans l'Inspecteur (« Linked to 3 object(s): … »), et, dans une copie collée, modifiée pour nommer les objets de la copie (les identifiants des objets non copiés disparaissent). |
| `sdk/samples/ExampleVehicles.WorldEditor/Sails.cs` | Des formes : la toile d'une voile dessinée entre trois ou quatre coins conservés dans ses données, dans sa couleur, visible des deux côtés. |
| `sdk/samples/ExampleVehicles.WorldEditor/Cables.cs` | Des formes dans le repère du monde : un câble du sommet d'un poteau au sommet du poteau qu'il nomme, trouvé avec `TryFind`. |
| `sdk/samples/ExampleVehicles.WorldEditor/Canals.cs` | Une couche de scène : chaque canal (un porteur de données qui conserve son tracé, sa largeur et sa profondeur) creusé en tranchée avec l'aspect du sol du jeu, son lit peint en terre, le sol non dessiné au-dessus. |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalTool.cs` | Un outil : cliquez des points sur le sol pour le tracé d'un canal (Backspace retire le dernier), sa largeur et sa profondeur réglées par des curseurs, le tracé en aperçu ; Enter ou **Dig** ajoute le canal (un porteur de données), **Clear points** ou Escape abandonne les points. Shift+clic comble un canal (le retire), Ctrl+clic lui donne la largeur et la profondeur des curseurs (le modifie). |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalCopies.cs` | Un copieur de secteur : les canaux dont tout le tracé se trouve dans ce qui est copié sont conservés sous forme de décalages par rapport au milieu de la copie, et creusés de nouveau là où chaque copie est collée, tournés et mis en miroir avec elle, posés sur le sol à cet endroit. |
| `sdk/samples/ExampleVehicles.WorldEditor/Pits.cs` | Une découpe du sol : un trou rond aussi large que le rayon de la fosse (1,5 m quand elle n'en conserve pas). |
| `sdk/samples/ExampleVehicles.WorldEditor/LiveLink.cs` | Le côté extension du mode direct : nomme les navires propres au jeu (`Host.Game`, d'après les `gameComponents` du manifeste), vérifie que le plugin s'est enregistré, appelle sa route `ping`, consigne la réponse, et intercepte tout. |
| `sdk/samples/ExampleVehicles.Bridge/ExampleVehiclesBridgePlugin.cs` | Le plugin BepInEx du mod qui participe au mode direct : dépendance souple, enregistrement dans une classe à part, routes `ping` et `players`, un écouteur qui consigne ce que l'éditeur a appliqué. Il se compile avec les DLL du jeu et de BepInEx comme n'importe quel plugin (`-p:ValheimDir=…`, le dossier d'un serveur dédié Valheim). |
