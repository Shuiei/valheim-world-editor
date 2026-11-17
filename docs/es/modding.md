<!-- source: 1ccdce30 -->
# Para autores de mods

El editor funciona con los mods de construcción sin que sus autores tengan que hacer nada: conectado a un juego con
mods, aprende los objetos de los mods, los dibuja con sus propios modelos y nombra los datos que guardan. Esta página es
para los autores de mods que quieren más: un archivo `worldeditor.json` opcional para lo que el editor no puede
adivinar, **objetos enlazados** para los mods que construyen una cosa a partir de muchos objetos, **extensiones del
editor** (una pequeña DLL compilada con el SDK del editor) para lo que un archivo no puede expresar, **complementos**
(lo mismo, hecho por cualquiera para el mod de otro) y **hooks del puente** para que el plugin propio de un mod
participe en el modo en vivo.

Todo esto es opcional, y cada paso se suma al anterior. El editor no incluye nada para ningún mod: dar soporte a un mod
en el editor le corresponde a su autor, con estas herramientas. Los ejemplos de esta página son los de un mod
inventado, «Example Vehicles», cuyo ejemplo completo viene con el SDK (consulta
[El ejemplo Example Vehicles](#el-ejemplo-example-vehicles)).

## Lo que el editor aprende por sí solo

| Qué | Cómo lo aprende el editor |
|---|---|
| Los objetos de los mods: nombres, piezas de construcción, costos, estaciones de fabricación, la herramienta que construye cada uno | Conectado a un juego (modo en vivo, WorldEditorBridge 1.26.0 o posterior), el editor pide al juego todos los objetos que conoce y se queda con los que no son propios de Valheim. Se guardan en `mod-catalogue.json` en la carpeta de datos del editor, así que el mundo de un servidor con mods abierto más tarde desde sus archivos, sin conexión, también los muestra. Un informe más reciente de un mod sobre una pieza sustituye al anterior; un mod nunca cambia uno de los objetos propios de Valheim. |
| Su aspecto | El juego indica lo que dibuja de cada objeto de mod (WorldEditorBridge 1.27.0 o posterior); el editor encuentra esas mallas, materiales y texturas en los propios archivos de los mods en el equipo del jugador (sus asset bundles, copiados una vez de las DLL a `mod-bundles` en la carpeta de datos). Las mallas que un mod crea por código mientras el juego está en marcha no se pueden encontrar: esos objetos se muestran sin textura, salvo que una [extensión](#extensiones) los dibuje. |
| Los nombres de los datos que los mods guardan en los objetos | El juego solo guarda un hash de cada clave de datos. El editor encuentra los nombres entre el texto escrito en las DLL de los mods (leídas como bytes, nunca ejecutadas) en las carpetas de plugins que conoce, y los guarda en `mod-zdo-keys.json` hasta que cambia una DLL. Una clave cuyo nombre se compone mientras el juego está en marcha (`"Corner" + i`) no se puede encontrar así: nómbrala en `data` en tu `worldeditor.json` (más abajo); si no, el Inspector la muestra como `#` y su hash. |
| De qué mod viene un objeto | El Inspector lo dice (con Jötunn instalado, que lo sabe; si no, «De un mod»). El panel [Construir](build.md), en un mundo y en el Taller, muestra las piezas de los mods en su pestaña **Piezas de mods**, un mod cada vez. |

Consulta [Juegos con mods](live-mode.md#juegos-con-mods) para la parte del jugador.

## worldeditor.json

Hay algunas cosas que el editor no puede adivinar. Un mod puede indicarlas en un archivo `worldeditor.json` incluido con
él, en cualquier lugar de su carpeta dentro de `BepInEx/plugins` (junto a su DLL está bien). Este es el archivo de
Example Vehicles:

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

| Clave | Qué hace el editor con ella |
|---|---|
| `version` | La versión del formato: `1`. Un archivo de una versión posterior, o sin versión, se omite entero. |
| `data` | Por clave de datos, tal como la nombra tu código (el nombre cuyo hash guarda el juego; una clave nombrada aquí se reconoce por su nombre aunque tu DLL no la contenga como texto): `label`, cómo nombra el valor el [Inspector](select.md#inspector) (si no, su clave; los nombres propios del editor para las claves del juego tienen prioridad); `readOnly`, el Inspector muestra el valor pero no deja cambiarlo (valores que tu mod fija por sí mismo y que se romperían al editarlos); `help`, qué es, mostrado al apuntar al valor. |
| `prefabs` | Por nombre de prefab: `hidden`, no se ofrece en la lista de [Colocar](place.md) ni en el panel [Construir](build.md) (tus herramientas, efectos, auxiliares; los objetos suyos que ya hay en un mundo siguen viéndose); `group`, el encabezado bajo el que aparece tu pieza en la pestaña **Piezas de mods** del panel Construir, tras el nombre del mod («*nombre del mod* · Hulls»). Sin grupo, una pieza aparece bajo su estación de fabricación o, si no, bajo «No necesita estación». `group` solo cuenta para las piezas propias de los mods. `data`: los objetos de este prefab solo contienen datos de tu mod (un registro, lo que se excavó en un área) y no tienen nada que ver: el editor nunca los dibuja, selecciona ni enumera en un área (tu extensión sí los lee), y un [restablecimiento de zonas](area.md) los conserva cuando conserva las construcciones y los quita con todo lo demás en caso contrario. `piece`: una pieza de construcción que tu mod crea por código y que ningún bundle contiene: `{ "name": "Track 2 m", "cost": { "Stone": 4 }, "station": "Stonecutter" }` (`name` obligatorio, 64 caracteres como máximo; `cost` por nombre de prefab de artículo, 16 artículos como máximo; `station` por su nombre en inglés). La pestaña **Piezas de mods** del panel Construir la muestra bajo tu mod incluso sin un juego en vivo (lo que diga un juego en vivo tiene prioridad). Las piezas de tus bundles no lo necesitan: el editor las encuentra por su `Piece`, o por sus puntos de encaje cuando añades el `Piece` por código. |
| `links` | Cómo se enlazan tus objetos en uno solo: consulta [Objetos enlazados](#objetos-enlazados). |
| `ties` | Cómo se vinculan tus objetos a uno que los nombra (los vagones de un tren): consulta [Objetos vinculados](#objetos-vinculados). |
| `extension` | La DLL de tu extensión del editor, relativa a la carpeta del archivo y dentro de ella: consulta [Extensiones](#extensiones). |
| `gameComponents` | Nombres de clase de los scripts propios del juego cuyos campos necesita tu extensión (`"TerrainOp"`, `"Ship"`; 32 como máximo): mientras tu extensión se ejecuta, el editor lee todos sus componentes en los prefabs del juego desde el Valheim del jugador (una vez, y luego los conserva hasta que cambia el juego) y se los pasa como `Host.Game` (consulta [Hooks](#hooks)). |
| `settingsFiles` | Nombres de los archivos de configuración de BepInEx de tu mod que lee tu extensión (`"author.mod.cfg"`, en `BepInEx/config`; 8 como máximo): al abrirse un mundo, el editor los lee de la primera de las carpetas BepInEx del jugador que tiene cada uno y, en vivo, de las del propio juego conectado a través de su puente (las de un servidor, cuya configuración sincronizada reciben sus jugadores). Tu extensión lee una opción con `Host.Setting(file, section, key)` (consulta [Hooks](#hooks)). |
| `worldGenerators` | Los generadores de mundos que registra tu extensión (consulta [Generadores de mundos](#generadores-de-mundos)), por el id que indican los archivos de sus mundos, cada uno con el nombre de tu mod para el jugador: `{ "author.mod": "Some Mod" }` (8 como máximo). El editor lo lee incluso cuando tu extensión no se ejecuta, para que un mundo creado por tu mod diga por qué no se puede abrir: el complemento desactivado, esperando a tu mod, sin ejecutarse, o la extensión de tu mod sin activar. |

### Cómo lo lee el editor

- **Dónde.** En todas las carpetas `plugins` que conoce el editor: la de `BepInEx` de la carpeta de Valheim, el perfil
  del gestor de mods en el que se instaló el propio editor, las carpetas añadidas en **Configuración → Carpetas de
  BepInEx** y todos los perfiles de r2modman y Thunderstore Mod Manager. Cuenta cualquier archivo llamado
  `worldeditor.json` ahí, a cualquier profundidad.
- **Cuándo.** Cada vez que se abre un mundo o el Taller, y cuando se abre **Configuración**. Un archivo cambiado con un
  mundo abierto cuenta la próxima vez.
- **Qué mod.** Un archivo pertenece al mod cuya carpeta está justo debajo de `plugins` (el `ExampleVehicles` de
  `plugins/ExampleVehicles/worldeditor.json`). Configuración nombra las extensiones según esa carpeta.
- **Dos archivos, un nombre.** Los archivos se leen del más antiguo al más reciente (según su última modificación):
  cuando dos dicen algo de la misma clave de datos, el mismo prefab o el mismo `id` de enlace, gana el más reciente.
- **Tolerante.** Se permiten comentarios y comas finales. Los textos tienen de 1 a 200 caracteres; las listas contienen
  como máximo 32 nombres; un archivo ocupa como máximo 1 MB. Una entrada que no tiene esta forma se omite y el resto del
  archivo sigue contando. Un archivo que no es JSON, o que no es de una versión que lea el editor, se omite; el registro
  del editor lo indica («mod manifest …: not a worldeditor.json this editor reads, skipped.»).

El [esquema](../worldeditor.schema.json) describe el archivo: con la línea `$schema` de arriba, los editores de código
comprueban el archivo y completan sus claves mientras escribes.

## Objetos enlazados

Si tu mod construye objetos a partir de otros (un barco y las piezas construidas sobre él, un carro y su carga), el
juego guarda cada pieza por separado, y el editor ve piezas sueltas, amontonadas donde las guardó el juego. Dile cómo
se enlazan y las dibujará como las construye tu mod, y moverá, copiará y eliminará cada grupo entero (consulta
[Objetos enlazados](select.md#barcos-y-otros-objetos-enlazados) para lo que ve el jugador). Funciona para los mods que
enlazan de la forma habitual: la raíz (y cualquier parte de la que cuelguen otras) tiene un id int propio; cada parte
guarda, en sus datos, el id de aquello de lo que cuelga y su desplazamiento y su giro en el marco de referencia de ese
objeto.

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

| Clave | |
|---|---|
| `id` | Clave de datos int del id propio de un objeto, que nombran sus partes. Obligatoria. |
| `parents` | Claves de datos int que nombran aquello de lo que cuelga una parte; gana la primera que tenga valor (una pieza sobre un pivote nombra el pivote antes que el vehículo). Al menos una. Obligatoria. |
| `offset` | Clave de datos Vec3 de la posición de una parte en el marco de aquello de lo que cuelga, en metros. Obligatoria. |
| `rotation` | Clave de datos Vec3 de su giro en ese marco, como ángulos de Euler de Unity en grados. |
| `rotationQuaternion` | Clave de datos Quaternion de ese giro, usada cuando no se guarda `rotation`. Sin ninguna de las dos, una parte no se gira en el marco de su padre. |
| `storedAt` | Dónde guarda el juego una parte: `root` (en la raíz de su grupo; tu mod la construye a partir de su desplazamiento cuando se carga el grupo; el valor predeterminado) u `own` (donde está). El editor guarda las partes movidas de la misma forma. |
| `references` | Otras claves de datos int que nombran el id de un objeto (el mástil de una vela, el objetivo de un interruptor): en una copia pegada siguen los nuevos ids de la copia, y se quitan cuando lo que nombran no se copió. |
| `dropOnCopy` | Claves de datos que una copia pegada omite: ids empaquetados de una forma que el editor no puede reescribir (un array de bytes), vínculos que no deben trasladarse (lo que iba a bordo). |
| `placements` | Por clave de padre (una de `parents`), cómo se coloca una parte que cuelga de él cuando no es como dice la regla: sus propias claves `offset`, `rotation` y `rotationQuaternion` y `storedAt` (las que no se den: las de la regla), como una vela que guarda su posición en su mástil aparte de su posición en el barco; o `"carried": true` (y ninguna de esas), una parte que el grupo transporta donde la guardó el juego (un carro a bordo): no guarda desplazamiento, se dibuja donde está guardada respecto a lo que la transporta, y se mueve, gira, copia y elimina con el grupo y se guarda donde está. |
| `name` | Cómo se llama un grupo en los mensajes del editor («vehicle en su totalidad»). Predeterminado: `group`. |

### Cómo usa el editor los enlaces

- **Dibujo.** Una parte se dibuja en su desplazamiento en el marco de su padre, el padre donde lo pone su propio padre,
  y así hasta la raíz, que está donde se guardó: las piezas de un barco giran y se inclinan con el casco. Una parte
  transportada se queda donde está guardada respecto a lo que la transporta. Una extensión puede girar el marco en el
  que cuelgan las partes de un padre, o colocar ella misma una parte (`AddLinkPlacer`, consulta [Hooks](#hooks)). Una
  parte cuyo padre (o cualquier padre de su cadena) no está en el mundo, o cuya cadena forma un bucle o tiene más de 16
  niveles (datos dañados), se queda donde está guardada.
- **Qué regla.** Un objeto toma la primera regla cuyas claves `id` o `parents` contiene. Los nombres de prefab no
  importan: solo los datos. La regla de un archivo más reciente con el mismo `id` sustituye a la de uno más antiguo, en
  su lugar del orden.
- **Mover y eliminar.** Seleccionar cualquier parte y moverla, girarla o eliminarla afecta a todo el grupo. Un grupo
  movido conserva su altura y no se encaja; sus partes se guardan como indica `storedAt`.
- **Copiar.** Una copia se lleva todo el grupo. Cada pegado (y cada una de sus repeticiones) recibe ids nuevos que no
  tiene ningún objeto del mundo, ni siquiera los eliminados, para que el juego nunca tome dos copias por una; todos los
  valores de padre y de `references` los siguen. Una parte copiada sin su padre se pega como un objeto normal (sin sus
  enlaces), donde se pegó. Un pegado reflejado también refleja los desplazamientos y giros de las partes. Los valores
  de `dropOnCopy` desaparecen, salvo que los gestione el reescritor de copias de una extensión.
- Las claves de datos se nombran como las nombra tu código, igual que en `data`.

## Objetos vinculados

Algunos mods vinculan objetos entre sí sin guardar dónde está cada uno respecto al otro: un tren nombra los vagones de
los que tira, una cubierta no nombra nada pero lo que se construye sobre ella nombra la cubierta. El juego guarda cada
objeto donde está, y tu mod los vuelve a montar mientras se ejecuta. Dile al editor cómo están vinculados y los
mantendrá como un solo grupo:

```json
{
  "version": 1,
  "ties": [
    { "name": "train", "root": "ExampleVehicles_Wagons", "member": "ExampleVehicles_Self", "idBytes": 12 },
    { "name": "deck", "root": "ExampleVehicles_DeckId", "member": "ExampleVehicles_OnDeck" }
  ]
}
```

| Clave | |
|---|---|
| `root` | Clave de datos de los ids que nombra una raíz: un int, un long o un array de bytes que contiene varios ids seguidos. Obligatoria. |
| `member` | Clave de datos del id propio de un miembro, del mismo tipo (un int, un long o un array de bytes). Obligatoria. |
| `idBytes` | Para un array de bytes, cuántos bytes ocupa un id (de 1 a 64; 12 para los ids de objeto del juego). Sin ella, el valor entero es un id. |
| `name` | Cómo se llama un grupo en los mensajes del editor. Predeterminado: `group`. |

Un miembro pertenece al grupo de una raíz cuando su propio id es uno de los que nombra la raíz. Los ids se comparan como
los bytes que son, signifiquen lo que signifiquen para tu mod: el editor nunca los busca en el juego. Los grupos que
comparten un objeto son un solo grupo: un tren cuya plataforma es la raíz de una cubierta se lleva consigo lo que está
construido sobre la cubierta.

- **Seleccionar, mover y eliminar.** Seleccionar cualquier objeto de un grupo y moverlo o girarlo afecta a todo el
  grupo, como un bloque: cada miembro conserva su posición respecto a los demás y se guarda donde está. Eliminar la
  raíz se lleva sus miembros; eliminar un miembro solo se lleva ese miembro.
- **Copiar.** Una copia se lleva todo el grupo. Cada pegado (y cada una de sus repeticiones) recibe ids nuevos del mismo
  tipo y tamaño, aleatorios, en la raíz y en los miembros que nombra, para que una copia nunca se lleve los miembros
  del original: una lista conserva los ids de los miembros copiados, un id único es siempre nuevo, y un miembro cuya
  raíz no se copió pierde su id.
- **En vivo y guardado.** Resolver los nuevos ids le corresponde a tu mod: encuentra los miembros de una raíz pegada por
  los ids que contienen, como hace después de que el juego cargue un mundo (el juego da a cada objeto un nuevo id de
  objeto en cada carga).
- Una regla con las mismas claves `root` y `member` en un archivo más reciente sustituye a la de uno más antiguo, en su
  lugar del orden.

## Extensiones

Lo que un archivo no puede expresar, el código sí. Un mod puede incluir una **extensión del editor**: una pequeña DLL,
compilada con el SDK del editor, que el editor carga para leer y dibujar los objetos del mod. No se ejecuta en el juego,
y nunca ve el código del juego ni el de Unity: solo los datos de los objetos, tal como los tiene el editor. Puede:

- mostrar con palabras en el Inspector el valor de un array de bytes;
- reescribir en las copias pegadas los ids que tu mod empaqueta en su propio formato;
- fijar lo que tu mod calcula a partir de dónde está un objeto cada vez que el editor lo coloca, lo pega, lo mueve o
  lo gira (la vía bajo un vehículo, un giro que tu mod guarda);
- dibujar lo que tu mod crea por código mientras el juego está en marcha (una vela a partir de sus esquinas, un cable de
  poste a poste);
- colocar las partes de tus objetos enlazados donde las construye tu mod cuando sus datos por sí solos no lo indican
  (las partes sobre un pivote que tu mod ha girado);
- dejar sin dibujar el suelo donde tu mod excava (la boca de un túnel, un hoyo);
- dibujar lo que tu mod hace de un área en su conjunto (un canal, roca excavada de una colina) con el aspecto del suelo
  del juego, en lugar del suelo del editor;
- leer la configuración de los scripts propios del juego (sus `gameComponents`), tal como la tiene el Valheim del
  jugador;
- leer la configuración propia de tu mod (sus `settingsFiles`), tal como la tiene el juego del mundo;
- añadir una herramienta propia a la barra de herramientas del editor 3D, con su tarjeta de opciones, su vista previa y
  sus ediciones en el historial (excavar un canal donde el jugador hace clic);
- para un mod de generación de mundos, leer y dibujar los mundos que crea, y crear otros nuevos con él;
- en modo en vivo, pedir al plugin propio de tu mod en el juego lo que solo sabe el juego (consulta
  [Modo en vivo](#modo-en-vivo)).

### Hacer una

1. **Consigue el SDK** de una [versión publicada](https://github.com/Shuiei/valheim-world-editor/releases):
   `ValheimWorldEditor.Sdk-<version>.zip` contiene `ValheimWorldEditor.Sdk.dll` (netstandard2.0, con lo que se compila
   una extensión; su compilación net10.0 en `net10.0/` contiene además el modelo de reglas del mundo, para una
   extensión sobre el .NET del editor que crea reglas del mundo) y `WorldEditorBridge.Api.dll` (con lo que se compila el
   plugin de un mod para el [modo en vivo](#modo-en-vivo)), cada uno con su archivo de documentación para los editores de
   código, esta página como `README.md`, y el ejemplo Example Vehicles completo en `sdk/samples/` (se compila tal cual).
2. **Compila una biblioteca de clases netstandard2.0** (net10.0, con `net10.0/ValheimWorldEditor.Sdk.dll`, para una que
   cree reglas del mundo) que haga referencia a `ValheimWorldEditor.Sdk.dll` sin copiarla
   a tu salida (`Private="false"`): el editor pone la suya. No uses nada más que el
   framework (el proyecto del ejemplo también hace referencia al paquete `System.Numerics.Vectors`, para
   `Vector3` y `Quaternion`). Una extensión es **una sola DLL**: el editor no carga ningún otro archivo de tu
   carpeta, así que nunca encuentra una biblioteca auxiliar que esté junto a ella.
3. **Marca el ensamblado** y dale un punto de entrada: una clase pública con un constructor sin
   parámetros que implemente `IWorldEditorExtension`. El editor la crea una vez y llama a `Register`
   una vez, antes de usar ningún hook. Este es el del ejemplo:

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

4. **Inclúyela** con tu mod, dentro de su carpeta, y nómbrala en tu `worldeditor.json`:
   `"extension": "ExampleVehicles.WorldEditor.dll"`. Una ruta que sale de la carpeta del archivo, o un archivo
   que no termina en `.dll`, no se ofrece.

### Activarla: Configuración → Extensiones de mods

Una extensión es código que se ejecuta en el editor con los permisos del jugador, así que **decide el jugador**.
El editor encuentra las extensiones que ofrecen los mods de sus carpetas de plugins y las muestra en
**Configuración**, en **Extensiones de mods**: un interruptor por extensión, con el nombre de la carpeta del mod y el
archivo («ExampleVehicles · ExampleVehicles.WorldEditor.dll»). Al apuntar a él se muestran la ruta del archivo y su
SHA-256. Bajo cada interruptor el editor dice qué ha sido de ella: «En ejecución.», «No está en ejecución: …» con el
motivo, o «Desactivada: the file cannot be read.» (no se puede leer el archivo). Cuando ningún mod ofrece ninguna:
«Ningún mod de las carpetas de arriba ofrece una.»

- **Desactivada por defecto.** Una extensión solo se ejecuta cuando el jugador la activa y pulsa **Guardar**; a partir
  de entonces se ejecuta, y las desactivadas se detienen.
- **Solo ese archivo.** El editor recuerda el SHA-256 del archivo. Una DLL cambiada (una actualización del mod) es otro
  código: queda desactivada hasta que el jugador la vuelva a activar. Los bytes comprobados son los bytes cargados.
- **Por qué no se ejecuta.** «it is not marked as an extension ([assembly: WorldEditorExtension])» (no está marcada como
  extensión), «it needs a newer editor (API …; this one has …)» (necesita un editor más reciente), «its entry … is not
  an IWorldEditorExtension» (su punto de entrada no es un IWorldEditorExtension), o la excepción que lanzó `Register`.

### Hooks

| Hook | Qué hace el editor con él |
|---|---|
| `AddDecoder(key, IDataDecoder)` | El Inspector muestra el valor de un array de bytes con tus palabras, bajo el valor (en lugar de solo su tamaño). `Describe` devuelve unas pocas líneas cortas, o `null` cuando los bytes no tienen una forma que sepa leer. Cuando dos extensiones leen una misma clave, gana la primera que da texto. |
| `AddCopyRewriter(ICopyRewriter, keys)` | Cada objeto de una copia pegada que contiene una de las claves se te pasa (`ICopy`), después del enlazado propio del editor (`links`), con los nuevos ids de la copia para los de los originales (`IIdMap.TryGetNew`; false cuando el objeto que tenía el id antiguo no se copió). Cambia sus valores en el sitio (`SetInt`, `SetQuaternion`, `SetBytes`…; `null` quita un valor). Las claves son tuyas: `dropOnCopy` ya no las quita. |
| `AddAreaCopier(id, IAreaCopier)` | Lo que tu mod guarda de un lugar y que no lleva ningún objeto copiado (un registro de roca excavada, en contenedores de datos que el editor nunca copia) viaja con las copias del editor. Cuando el jugador copia (la selección de la herramienta Región, o los objetos de la herramienta Selección), `Copy(area, region)` recibe el área y lo que se copia (`CopyRegion`: su `Outline` y `Contains(x, z)`, en metros del mundo, y su `Middle`) y devuelve bytes propios (o `null`): guarda los lugares como desplazamientos desde el centro, ya que la copia puede pegarse en cualquier parte, girada y reflejada, incluso en otro mundo. Los bytes van con la copia y dentro de los archivos de plano, con el nombre de tu mod y el `id` (letras, cifras, puntos, guiones y guiones bajos). Al pegarla, se llama una vez a `Paste(area, bytes, copies)` con todas las copias que deja el pegado (`CopyPlacement`: su `Middle`, `Turn` y `Mirror`; `Place(offset)` convierte un desplazamiento desde el centro antiguo en el punto del mundo donde cae, `Yaw(yaw)` un giro), y el área tal como el pegado deja su suelo; el `ToolEdit` que devuelves forma parte del paso de deshacer del pegado. Los bytes cuyo copiador no se está ejecutando (tu extensión no instalada) se omiten del pegado. |
| `AddPlacer(IObjectPlacer, prefabs)` | Los objetos de los prefabs que el editor pone en un lugar propio (colocados, pegados, movidos o girados, por cualquier herramienta, un script o Claude) se te pasan una vez por paso, todos juntos (`ICopy`, cada uno en su nuevo lugar, con sus valores tal como se crearán; los de una copia pegada, tras los reescritores de copias), con el área tal como está antes del paso, sin los objetos que mueve: fija lo que tu mod calcula a partir de dónde está un objeto (un giro que guarda aparte del del objeto, la vía sobre la que está un vehículo, la parada que nombra un cartel), como lo haría tu mod cuando un jugador lo construye. Lo que fijas se guarda con el paso y se aplica en vivo con él. `SetTurn` gira el objeto tal como se creará, para un mod que guarda sus objetos sin girar y su forma en sus valores (gira los valores y luego devuelve el objeto a su giro; editor 1.119.0 en adelante). Los objetos cambiados donde están (el Inspector, la edición de una herramienta) no son una colocación. |
| `AddShapes(IShapeProvider, prefabs)` | Los objetos de los prefabs se dibujan también con tus formas: `Shape`s, triángulos en el marco del objeto (metros; x a la derecha, y arriba, z adelante; con la posición, el giro y el tamaño del objeto aplicados) o en el del mundo (`WorldSpace = true`), de un color cada una (rojo, verde, azul y alfa de 0 a 1; alfa por debajo de 1: transparente), con los vértices en sentido horario vistos de frente como en Unity, o vistos por ambas caras (`BothSides = true`, para una vela o una tela). Se dibujan con el tipo del objeto (ocultas cuando el panel Vista lo oculta). Un objeto del que el editor no tiene modelo se dibuja solo con tus formas (sin caja de sustitución), y se selecciona y se resalta con la caja de las formas que das para uno de su prefab sin datos (las de su propio marco). Una forma con un triángulo que nombra un vértice que no tiene, o un vértice que no es un número, no se dibuja. |
| `AddLinkPlacer(ILinkPlacer, prefabs)` | Las partes enlazadas (los `links` de tu manifiesto) se colocan donde las construye tu mod cuando sus datos por sí solos no lo indican. `FrameOf(obj)`, para un objeto de los prefabs que tiene partes: el marco en el que cuelgan, en su propio marco (`LinkFrame`: un desplazamiento y un giro; la plataforma de un pivote tal como tu mod la giró o la movió), o `null` para su propio marco. `PlaceOf(part, parent)`, para una parte de los prefabs: dónde está en el marco de aquello de lo que cuelga, en lugar de su desplazamiento y su giro (la parte en la posición y con el giro con que la guardó el juego, su padre tal como se dibuja; entonces no se aplica el marco del padre), o `null` para la posición de sus enlaces. Mover, copiar y eliminar siguen respetando los enlaces. |
| `AddGroundCuts(IGroundCutter, prefabs)` | El suelo no se dibuja dentro de los contornos (`GroundCut`) que das para los objetos de los prefabs: puntos del suelo (x, z) en el marco del objeto, o en el del mundo (`WorldSpace = true`); al menos tres, en cualquier sentido de giro (un contorno que se cruza a sí mismo recorta según la regla par-impar). Un clic ahí atraviesa el agujero. El suelo del editor es un mapa de alturas con un punto cada metro: el recorte omite el suelo entre los puntos que hay dentro del contorno, así que su borde sigue esa cuadrícula de 1 m, y lo que se ve a través es lo que dibujes debajo (formas) y la cara inferior del suelo. |
| `AddScene(ISceneProvider)` | Lo que tu mod hace de un área en su conjunto, dibujado en ella: se llama a `Build(area)` en un hilo de trabajo cada vez que se abre un área y de nuevo cada vez que cambian sus objetos, su suelo o el juego (o cuando llamas a `Host.Redraw()`), y devuelve una `SceneLayer` (o `null`). Sus `Meshes` (`SceneMesh`: vértices y sus normales en el marco del mundo, en metros, triángulos en sentido horario vistos de frente como en Unity) se dibujan con el aspecto del suelo del juego cuando tienen `BiomeColors` (el color de terreno de cada vértice, tal como lo da `area.BiomeColor`) y se pintan a partir de su `Mask` (una pequeña textura de la máscara de pintura del terreno, `MaskTexture`) en `MaskUvs`; sin colores de bioma, en `Color`. Los clics caen sobre ellas (salvo con `Pickable = false`). Sus `Holes` (`GroundHole`: rectángulos en metros enteros) dejan sin dibujar el suelo del editor, celda a celda, exactamente hasta sus bordes: dibuja lo que lo sustituye. El `area` (`IAreaView`) da sus límites, sus objetos de un prefab tal como están con los cambios aún sin guardar (`ObjectsOf`, incluidos tus contenedores de datos), la altura del suelo tal como la muestra el juego (`GroundHeight`, entre los puntos separados un metro, siguiendo los propios triángulos del juego), el bioma, si un punto está en el Extremo Norte o en las Tierras de ceniza, y el color y la pintura del terreno en un metro entero (`BiomeColor`, `GroundMask`). Calcúlalo todo a partir del área cada vez; no guardes nada entre llamadas. |
| `AddTool(IEditorTool)` | Una herramienta propia en la barra de herramientas del editor 3D, en **MODS**, con su `Info` (`ToolInfo`: un id, único en tu extensión; su nombre en la barra; una línea de ayuda en su sugerencia; `Icon`, un trazado SVG dibujado en 16 × 16 o, si no, la primera letra de tu nombre). Al elegirla, muestra una tarjeta con sus `Controls`: `SliderControl` (mín., máx., valor, `Step`, `Unit`), `ChoiceControl` (una lista), `ToggleControl`, `ButtonControl` y `TextControl` (una línea que cambias con `SetText`); los valores del jugador se recuerdan de una sesión a la siguiente. El editor llama a `OnChosen` y `OnLeft` al elegirla y al dejarla, a `OnControl(id)` cuando cambia un valor o se pulsa un botón, a `OnPointer` con cada movimiento, pulsación, arrastre y liberación sobre la vista 3D (`PointerInput`: el rayo, las teclas pulsadas y aquello a lo que apunta: el suelo, tu capa de escena o una cosa, con el punto y la normal del suelo en él), y a `OnKey` con cada tecla pulsada (el nombre de la tecla, como «Enter», «Escape», «Back» o «A»; devuelve true cuando la usaste; false se la deja al editor, así que Ctrl+Z sigue deshaciendo). El `IToolContext` que se pasa en cada llamada da el área (`Area`, como para las capas de escena), los valores de tus controles (`Number`, `Choice`, `IsOn`), formas dibujadas sobre el área hasta que las cambies (`Preview`, en el marco del mundo), una línea en la barra de estado (`Status`) y `Commit(label, ToolEdit)`: objetos añadidos (un prefab, una posición, un giro y sus datos fijados mediante un `ICopy`, que `SetTurn` puede girar), cambiados (una copia de un objeto que te dio el área, con tus cambios) y quitados, en un solo paso del historial del área, que se deshace y se guarda como cualquier otra edición. Tus contenedores de datos se conservan en el historial como los demás objetos, aunque nunca se dibujan; un objeto que no obtuviste del área se deja como está. |
| `AddLiveListener(ILiveListener)` | Se le avisa cuando el editor se conecta a un juego (`OnConnected`, con el juego) y cuando eso termina (`OnDisconnected`: se cerró el mundo, o se abrió otro). Consulta [Modo en vivo](#modo-en-vivo). |
| `AddWorldGenerator(IWorldGenerator)` | Los mundos de tu mod de generación de mundos (consulta [Generadores de mundos](#generadores-de-mundos)). |
| `AddWorldRules(IWorldRulesProvider)` | Las reglas con las que se generan los mundos, a partir de la configuración y los archivos de datos de tu mod de generación de mundos (consulta [Reglas del mundo](#reglas-del-mundo)). |
| `Host` | El editor, fuera de los hooks: `Host.Live`, el juego conectado (null cuando no hay ninguno: un mundo guardado, el Taller); `Host.Game`, los componentes del juego que pide `gameComponents` en tu manifiesto (`Components("Ship")`: cada uno con su prefab, el objeto en el que está y sus campos por nombre, tal como los guarda el juego; disponibles una vez abierto un mundo o el Taller, así que pídelos desde tus hooks, no desde `Register`); `Host.Setting(file, section, key)`, una opción de un archivo que nombra `settingsFiles` en tu manifiesto, como el texto que contiene el archivo (null cuando el archivo o la opción no están: usa el valor predeterminado de tu mod; disponible, como `Host.Game`, una vez abierto un mundo o el Taller); `Host.BepInExFolders`, las carpetas BepInEx del jugador que conoce el editor, en el orden en que las mira (cada una con su `Path` y dónde está, para el jugador); `Host.DataFolder`, la carpeta de datos del editor (guarda tus propios archivos en una carpeta tuya dentro de ella); `Host.World` (extensiones net10.0), las reglas del mundo y los objetos propios del juego (consulta [Reglas del mundo](#reglas-del-mundo)); y `Host.Redraw()`, para que tus formas y recortes se vuelvan a crear en el siguiente fotograma (después de que el juego te haya dicho algo nuevo). |
| `Log(message)` | Escribe una línea, con el nombre de tu mod («extension ExampleVehicles: …»), en el registro del editor, `ValheimWorldEditor.log` en su carpeta de datos (**Abrir registro** en la página de inicio; se reinicia en cada sesión). |

Los hooks de formas y de recortes también reciben el mundo que rodea al objeto (`IWorldView`): `TryFind(idKey, id, out
obj)` encuentra otro objeto por un id que contiene, para una cuerda o un cable que llega hasta él. Solo se encuentran
los ids de una clave que una regla de `links` nombra como su `id` (el editor indexa esos). En el editor 3D ese
mundo es también un `IGroundView` (`world is IGroundView ground`): `TryGroundHeight(x, z, out height)`
da la altura del suelo en ese punto tal como lo dibuja el editor, para una forma que baja hasta él (una escala de
cuerda), y false fuera del área.

Los objetos llegan como `IObjectView`: el nombre del prefab ("" cuando el editor no lo conoce), la posición,
el giro (ángulos de Euler de Unity en grados, y `Orientation`, un quaternion) y el tamaño tal como se dibujan (para una
parte enlazada, donde la construye tu mod, no donde la guarda el juego), y cada valor de sus datos por clave:
`TryGetInt`, `TryGetLong`, `TryGetFloat`, `TryGetVector3`, `TryGetQuaternion`, `TryGetString`,
`TryGetBytes`. Las claves y los prefabs se nombran como los nombra tu código; el editor les aplica el hash como lo hace
el juego.

### Generadores de mundos

Un mod de generación de mundos sustituye el suelo del juego y marca los mundos que crea: su archivo de mundo empieza
con -1 donde va la versión de archivo del juego, luego el id del generador (una cadena), y luego lo que guarde el mod
(Riverheim antes de la 1.3: la versión de archivo del juego y luego su preajuste). El editor rechaza un mundo así salvo
que una extensión en ejecución registre un generador con ese id, igual que lo rechaza el juego sin el mod: aparece en la
lista, en gris, con el motivo (nombra los ids en `worldGenerators` en tu manifiesto para que el editor diga qué falta).

Un mod que se conecta a [Worldhalla](https://thunderstore.io/c/valheim/p/Gurebu/Worldhalla/) (Riverheim desde la 1.3)
tiene sus mundos marcados por Worldhalla, con el id y la configuración del generador en la propia cabecera de
Worldhalla. Registra un generador para ellos con el id `worldhalla:` seguido del id de tu generador en Worldhalla (el de
Riverheim: `worldhalla:dev.gurebu.riverheim`): su `ReadHeader` recibe la versión de archivo del juego y luego tu
configuración tal como la guarda Worldhalla, y debe leerla entera. Un mundo cuya cabecera de Worldhalla es de un
Worldhalla más reciente que la 1.0 (su formato, o configuración propia de Worldhalla) se rechaza indicando el motivo. Tu
`IWorldGenerator` da:

- `Id`, `PluginId` (el GUID de BepInEx del mod: en modo en vivo se te dice la versión que ejecuta el juego) y `Name`;
- `ReadHeader(file, out fileVersion)` y `WriteHeader(file, fileVersion, settings)`: lo que sigue al id, leído y
  escrito en los formatos del juego (`IWorldFileReader`, `IWorldFileWriter`), con tu configuración guardada como un
  `GeneratorSettings` (una descripción para el jugador y un valor propio). Lanza `InvalidDataException` con el motivo
  para el jugador cuando no puedas leer una cabecera;
- `Problem(settings)`: por qué no se puede dibujar un mundo así, rápidamente (se pregunta por cada mundo que muestra la
  página de inicio), o null;
- `Open(settings, gamePluginVersion)`: listo para disponer mundos, al abrirse un mundo (`IWorldLayout`: tu `Version`,
  si disponer es lento, `Slow`, y `LayOut(seed)`); lanza `InvalidOperationException` con el motivo cuando no puedas;
- `LayOut(seed)` da un `IGroundLayout`: el bioma (el valor del juego), la altura con su máscara y el factor de bosque en
  un punto, en lugar de los cuerpos de GetBiome, GetBiomeHeight, GetAshlandsHeight y GetForestFactor del juego
  (consultados desde muchos hilos). El estiramiento de Expand World sigue yendo antes de tus respuestas, y sus
  multiplicadores de profundidad y de bosque después, como se encuentran los parches de los mods en el juego; el editor
  no dispone ninguna agua del juego;
- `NewWorldSettings()`: la configuración que recibe un mundo nuevo (la página Un mundo nuevo ofrece tu generador,
  elegido en primer lugar), o null;
- `Notes(settings, expandWorld)`: lo que la configuración de Expand World (`ExpandWorldSettings`) hace además a tus
  mundos, para el registro.

Cualquier otra excepción que lance tu generador es un fallo: su extensión se desactiva y se informa al jugador de que el
complemento ha fallado.

### Reglas del mundo

Un mod de generación de mundos que cambia la generación propia del juego mediante configuración y archivos de datos (un
mundo más grande, sus propios biomas, ubicaciones y vegetación, como hace Expand World) no dispone el mundo por sí
mismo: el generador del editor sigue las reglas que se le dan. Compila tu extensión para net10.0 (la compilación
`net10.0` del SDK contiene el modelo de reglas, `WorldRules` y los tipos que contiene, en el espacio de nombres
`ValheimGen`) y registra un `IWorldRulesProvider`:

- `ForWorld(world)`: las reglas con las que se abre un mundo. La `WorldRulesRequest` da su `Name`, las
  `StartingGlobalKeys` que guarda su archivo de mundo (la configuración de un mod guardada ahí), las `LoadedGlobalKeys`
  que contiene su guardado, el `GridWidth` en el que están sus archivos de chunks, y el juego en el que se ejecuta cuando
  está en vivo (`Live`, si no null): lee entonces los archivos del mod desde el juego, mediante `Live.Plugins()`,
  `Live.ConfigFile(path)` y `Live.ConfigFolder(path)` (dentro de su BepInEx/config; null con un WorldEditorBridge
  anterior a la 1.54.0) y, si no, desde `Host.BepInExFolders`;
- `ForNewWorld()`: las reglas con las que se crea y se ve un mundo nuevo (la página Un mundo nuevo).

Devuelve null (o `WorldRules.Vanilla`) cuando tu mod no cambia el mundo (no instalado, o con sus valores
predeterminados): se pregunta al siguiente proveedor y, si no hay ninguno, el mundo es el propio del juego. Las reglas de
ubicaciones, vegetación y salas se crean a partir de las propias del juego (`Host.World`: sus `Locations` una vez que
`ReadLocations()` las ha leído, su `Vegetation`, `HasPrefab(name)` e `IsGameRoom(name)`). `WorldRulesText.Describe(rules)`
dice en pocas palabras lo que cambian las reglas, para tus líneas de registro. Un proveedor que lanza una excepción es un
fallo: su extensión se desactiva y se pregunta al siguiente.

### Reglas

- **Sé rápido, no guardes estado.** Los hooks se llaman desde varios hilos a la vez y a menudo (cada
  redibujado de las formas y los recortes, cada objeto pegado): no guardes estado entre llamadas (o protégelo con un
  bloqueo) y devuelve el control rápido.
- **Nunca lances excepciones.** Un hook que lanza una excepción desactiva la extensión hasta que el editor vuelve a
  iniciarse (Configuración dice por qué, y también el registro); el editor sigue sin ella. En el código que continúa
  después de que un hook devuelve el control (una llamada `async` al juego), captura todo tú mismo, como hace el
  `LiveLink` del ejemplo.
- **Versiones.** `SdkInfo.ApiVersion` es la versión de la API (ahora 1), y el atributo registra aquella con la
  que compilaste. Un editor ejecuta las extensiones compiladas con su versión o con una anterior. Los nuevos
  hooks llegan como añadidos; la versión solo cambia cuando la API se rompe.

## Complementos

Una extensión se incluye con su mod. Cuando el autor de un mod no ha hecho una (o el mod ya no se mantiene),
cualquiera puede hacerla: un **complemento** es un `worldeditor.json` y una extensión para **el mod de otro**,
publicado por separado, que el jugador instala junto al editor. El propio mod se queda como está, en
`BepInEx/plugins`; el complemento solo enseña al editor cómo es.

```
ValheimWorldEditor/                la carpeta del editor (en macOS: la carpeta que contiene ValheimWorldEditor.app)
  ValheimWorldEditor(.exe)
  addons/
    Tunnels/                       una carpeta por complemento, con el nombre que quieras
      worldeditor.json
      Tunnels.WorldEditor.dll
```

- **Su archivo** es un `worldeditor.json` como el de arriba (etiquetas de datos, prefabs, enlaces, `extension`,
  `gameComponents`), con una clave más: `"serves": "TheirMod.dll"`, el nombre de archivo de la DLL del mod.
  El complemento se usa mientras haya una DLL con ese nombre en una de las carpetas de plugins que conoce el editor
  (Configuración, Carpetas de BepInEx), en cualquier lugar dentro de `plugins`, y se deja de lado en caso contrario. Sin
  `serves` se usa siempre (una herramienta propia, para ningún mod en particular). Un `serves` que no es un simple
  nombre de archivo terminado en `.dll`, o un archivo de una versión que este editor no lee, deja fuera el complemento
  (el registro dice por qué).
- **Su extensión** se hace como cualquier otra (consulta [Hacer una](#hacer-una)): una sola DLL, dentro de la
  carpeta del complemento, con los mismos hooks y las mismas reglas.
- **Activado al instalarlo.** Instalar un complemento es decisión del jugador, así que se ejecuta sin necesidad de
  activarlo en Configuración. Configuración muestra los complementos en **Complementos**, cada uno con un interruptor que
  lo desactiva sin quitarlo (entonces no se usa nada de él), y con lo que ha sido de cada uno: «Esperando a su mod:
  TheirMod.dll no está en las carpetas de plugins de arriba.», «En ejecución.», «No está en ejecución: …», que solo
  aporta notas, o que está desactivado.
- **Lo que dice el propio mod tiene prioridad.** Los archivos de los complementos se leen antes que los de los mods: si
  el mod incluye algún día un `worldeditor.json` propio, lo que diga sustituye a lo que decía el complemento (una
  extensión propia necesita entonces el interruptor del jugador, como siempre).
- **Publícalo como un `.zip` que contenga su carpeta**, para que los jugadores lo descompriman en `addons`. Los paquetes
  del editor vienen con esa carpeta, vacía salvo por un `README.txt`.
- **El propio repositorio del editor mantiene algunos complementos** (`addons/`, cada uno publicado como su propio zip
  junto a los paquetes del editor: consulta [development.md](../development.md#add-ons-in-this-repository)); los tuyos
  pueden estar en cualquier sitio.

## Modo en vivo

Conectado a un juego en marcha (modo en vivo), el editor aplica sus cambios mediante el plugin
WorldEditorBridge, que destruye, crea y mueve objetos en el juego. Un mod que construye
cosas a partir de sus objetos mientras el juego está en marcha (un vehículo a partir de sus partes, un túnel a partir
de lo excavado) solo ve los cambios del editor en la siguiente carga, salvo que su propio plugin participe. El puente
ofrece dos cosas a los plugins de los mods, mediante `WorldEditorBridge.Api.dll`: se incluye con el puente, en su
carpeta `plugins`, y en el zip del SDK, y no contiene ningún tipo del juego ni de Unity, así que una actualización del
juego no puede romperla.

### La parte de tu plugin

Regístrate desde el `Awake` de tu plugin, con una dependencia opcional del puente, y mantén el código que
toca la API en una clase propia: así tu plugin también se carga sin el puente. Del
`sdk/samples/ExampleVehicles.Bridge` del ejemplo:

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

| Hook | Qué hace el puente con él |
|---|---|
| `AddListener(modId, IBridgeListener)` | Se llama a `OnApplied(AppliedChanges)` en el hilo principal del juego justo después de cada aplicación del editor, con lo que cambió: `Created` y `Destroyed`, los objetos creados y destruidos (cada uno con el `User` y el `Id` de su ZDOID, el hash de su prefab y su posición); `Moved`, los objetos movidos con su propio ZDOID (puente 1.40.0 o posterior: `From` donde estaba cada uno, `To` donde está ahora; un objeto cuyo lugar antiguo y nuevo tiene cargados un jugador se vuelve a crear en su lugar, y está en `Created` y `Destroyed`; desde la 1.97.0, un objeto cuyos datos cambió el editor también está aquí, con sus nuevos datos ya aplicados); `TerrainZones`, las zonas cuyo suelo se reescribió; `ResetZones`, las zonas vaciadas para volver a generarse. Vuelve a montar aquí tus cosas. Una excepción se anota en el registro del juego y no cambia nada de lo que hizo el editor. |
| `AddRoutes(modId, IBridgeRoutes)` | `Handle(name, body)` responde a `POST /mods/<modId>/<name>` en el hilo principal del juego, con los bytes que acuerdes con tu extensión del editor; `null` responde 404 («the mod has no route of that name»), una excepción, 500 con su mensaje. El puente espera como máximo 30 segundos al hilo principal: sé rápido, o responde «started» y deja que la extensión vuelva a preguntar. El cuerpo de una llamada ocupa como máximo 16 MB. |
| `Remove(modId)` | Retira tus rutas y tu listener (un plugin que se cierra). |

Los objetos que el puente crea, mueve o cambia se pasan a los mods del juego igual que el juego les pasa un objeto que
viene de otro juego: sus datos vuelven a pasar por `ZDO.Deserialize` (los mismos valores). Un mod que conoce los objetos
parcheando ese método (el ZdoWatcher de ValheimRAFT, que encuentra los barcos y aquello a lo que están atadas las
cuerdas por sus ids) reconoce al instante un objeto pegado, sin un listener propio.

Volver a registrarse con el mismo id sustituye lo registrado antes. El id del mod es tuyo (el id de tu
plugin es una buena opción): de 1 a 100 letras, cifras, puntos, guiones y guiones bajos; cualquier otro id
lanza una excepción. `BridgeApi.Version` es la versión de la API (ahora 1); un puente ejecuta los plugins compilados con
su versión o con una anterior, y los añadidos la mantienen. `GET /mods` en el puente devuelve la versión de la API
y los ids registrados.

### La parte de tu extensión

Tu extensión obtiene el juego mediante `ILiveGame`: desde `registry.Host.Live` en cualquier momento (null cuando
el editor no está conectado), o en el `OnConnected` de tu listener en vivo.

- `Mods`: los ids registrados en el puente, tal como los indicó el puente al abrirse el mundo
  (un plugin que se registra más tarde se ve en la siguiente conexión).
- `CallAsync(modId, name, body)`: llama a tu ruta y devuelve los bytes de su respuesta. Lanza una excepción cuando
  el mod o la ruta no están registrados, cuando la ruta falló (con el mensaje del juego) o cuando no se puede llegar al
  juego.
- `Plugins()`, `ConfigFile(path)` y `ConfigFolder(path)`: los plugins de BepInEx que cargó el juego, y un archivo o las
  entradas de una carpeta dentro de su BepInEx/config, de solo lectura (null con un puente anterior a la 1.54.0, y para
  una ruta que no existe). Lanzan `InvalidOperationException` cuando el puente rechaza la ruta (fuera de la carpeta, más
  de 4 MB).

Pide al juego lo que solo él sabe (lo que tu mod construyó por código), guárdalo y llama a
`registry.Host.Redraw()` para que tus formas y recortes se vuelvan a crear. El `LiveLink` del ejemplo llama a la
ruta `ping` del plugin cuando el editor se conecta y anota la respuesta; su plugin también responde a
`players` (cuántos jugadores hay en el juego).

Cuando un mundo se abre en vivo, el registro del editor nombra los mods registrados («bridge mods: …»).

## El ejemplo Example Vehicles

El zip del SDK (y el repositorio del editor) contiene el código fuente de las dos mitades de un mod inventado, para
copiar de él. Example Vehicles construye vehículos a partir de un casco y las partes construidas sobre él.

| Archivo | Qué muestra |
|---|---|
| `sdk/samples/ExampleVehicles.WorldEditor/worldeditor.json` | El archivo del mod: etiquetas, un auxiliar oculto, grupos, la regla de enlace `vehicle`, la extensión. |
| `sdk/samples/ExampleVehicles.WorldEditor/Extension.cs` | El punto de entrada, que registra un hook de cada tipo. |
| `sdk/samples/ExampleVehicles.WorldEditor/Keys.cs` | Las claves de datos y los prefabs del mod, tal como los nombra su código. |
| `sdk/samples/ExampleVehicles.WorldEditor/PackedIds.cs` | Un decodificador y reescritor de copias para una lista de ids empaquetados en un array de bytes (un byte de versión y luego cuatro bytes por id): mostrada en el Inspector («Linked to 3 object(s): …»), y en una copia pegada, adaptada para nombrar los objetos de la copia (los ids de objetos no copiados desaparecen). |
| `sdk/samples/ExampleVehicles.WorldEditor/Sails.cs` | Formas: la tela de una vela dibujada entre tres o cuatro esquinas guardadas en sus datos, de su color, vista por ambas caras. |
| `sdk/samples/ExampleVehicles.WorldEditor/Cables.cs` | Formas en el espacio del mundo: un cable desde lo alto de un poste hasta lo alto del poste que nombra, encontrado con `TryFind`. |
| `sdk/samples/ExampleVehicles.WorldEditor/Canals.cs` | Una capa de escena: cada canal (un contenedor de datos que guarda su recorrido, su anchura y su profundidad) excavado como una zanja con el aspecto del suelo del juego, con el lecho pintado de tierra y el suelo sin dibujar encima. |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalTool.cs` | Una herramienta: haz clic en puntos del suelo para el recorrido de un canal (Backspace quita el último), su anchura y su profundidad con controles deslizantes, el recorrido en vista previa; Enter o **Dig** añade el canal (un contenedor de datos), **Clear points** o Escape descarta los puntos. Shift+clic rellena un canal (lo quita), Ctrl+clic le da la anchura y la profundidad de los controles deslizantes (lo cambia). |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalCopies.cs` | Un copiador de áreas: los canales cuyo recorrido entero está dentro de lo copiado se guardan como desplazamientos desde el centro de la copia, y se vuelven a excavar donde se pega cada copia, girados y reflejados con ella, apoyados en el suelo de ese lugar. |
| `sdk/samples/ExampleVehicles.WorldEditor/Pits.cs` | Un recorte del suelo: un agujero redondo tan ancho como el radio del hoyo (1.5 m cuando no guarda ninguno). |
| `sdk/samples/ExampleVehicles.WorldEditor/LiveLink.cs` | La parte de la extensión en el modo en vivo: nombra los barcos propios del juego (`Host.Game`, a partir de `gameComponents` en el manifiesto), comprueba que el plugin se registró, llama a su ruta `ping`, anota la respuesta y captura todo. |
| `sdk/samples/ExampleVehicles.Bridge/ExampleVehiclesBridgePlugin.cs` | El plugin de BepInEx del mod que participa en el modo en vivo: dependencia opcional, registro en una clase propia, rutas `ping` y `players`, un listener que anota lo que aplicó el editor. Se compila con las DLL del juego y de BepInEx como cualquier plugin (`-p:ValheimDir=…`, la carpeta de un servidor dedicado de Valheim). |
