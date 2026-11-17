<!-- source: f7df77d8 -->
# Traducir el editor

Las palabras del editor están en catálogos, uno por idioma: un archivo JSON de textos por clave. El inglés,
[`editor/Core/Lang/en.json`](../../editor/Core/Lang/en.json), es la fuente a partir de la cual se traducen todos los
demás catálogos.

El editor incluye inglés, francés (`fr`), alemán (`de`), español (`es`), portugués de Brasil (`pt-BR`, que también se
muestra en un equipo configurado en portugués de Portugal), tailandés (`th`) y filipino (`fil`, también se muestra en un equipo configurado en tagalo). La documentación está traducida a cada uno (`docs/es/`…, con sus imágenes tomadas en ese idioma). Muestra el idioma elegido en
**Configuración → Idioma**, por defecto el del equipo cuando hay un catálogo de ese idioma. Un texto que un catálogo aún
no tiene se muestra en inglés. Un idioma nuevo se aplica cuando el editor vuelve a iniciarse.

Todavía no todos los textos vienen del catálogo. Estos sí:

- todas las sugerencias emergentes (las sugerencias del Inspector hechas a partir de los datos de un objeto mantienen
  sus nombres tal como los tiene el guardado);
- la página de inicio, Configuración, el mapa del mundo, la página Un mundo nuevo y los diálogos pequeños (preguntas,
  mensajes, los detalles de un plano) con las palabras que comparten;
- la barra superior del editor 3D, con lo que preguntan y dicen guardar, aplicar, descartar, salir del mundo y cerrar
  el editor, y los mensajes y jugadores del juego en vivo;
- las lecturas de la barra de estado, el panel Historial, el panel Vista, la tarjeta de ayuda y lo que la barra de
  estado y el Historial dicen de los cambios de Claude;
- las herramientas: los pinceles con sus sellos, Medir, Máscara, Script, Camino, Relieve, Selección, Pegar, Colocar,
  Construir (su panel y la barra de construcción), Región, la herramienta Mazmorra con su generador, el panel Planos,
  el Taller y el Inspector;
- los nombres de los biomas.

El resto se va pasando ventana a ventana. Un catálogo hecho ahora sigue funcionando a medida que se añaden más textos:
los nuevos se muestran en inglés hasta que se traducen.

## Hacer un catálogo

1. Copia `en.json` en la carpeta `lang` de la carpeta de datos del editor
   (`~/.local/share/ValheimWorldEditor/lang` en Linux, `%LOCALAPPDATA%\ValheimWorldEditor\lang` en
   Windows; **Configuración → Idioma** la muestra), con el código del idioma como nombre: `fr.json`,
   `de.json`, `pt-BR.json`.
2. Traduce los valores, nunca las claves. `lang.name` es el nombre del idioma en su propia lengua
   («Français»), tal como lo muestra Configuración.
3. Conserva cada `{0}`, `{1}`… : el editor pone ahí un valor (una carpeta, un número). Su orden en la
   frase puede cambiar. Un texto cuyos marcadores difieren de los del inglés, o con una `{` o una `}`
   sueltas, no se usa.
4. Elige el idioma en **Configuración**, reinicia el editor y míralo. Las claves que omitas, o que
   borres de tu copia, se muestran en inglés.

La misma carpeta también admite correcciones: un archivo en ella con el código de un catálogo que
incluye el editor se superpone a él, texto a texto. Ahí, una clave que omitas (o un texto que no se use)
conserva la traducción incluida; el inglés solo aparece donde ninguno de los dos tiene el texto.

Para que el editor incluya un catálogo, abre una pull request que lo añada a `editor/Core/Lang/` (solo
claves de `en.json`; `LangCatalogueTests` las comprueba).

## Traducir la documentación

La ventana de Ayuda muestra la documentación en el idioma del editor cuando tiene una traducción de la página, y en
inglés en caso contrario. Una traducción de la documentación es una carpeta de `docs/` con el código del idioma como
nombre, igual que su catálogo: `docs/fr/`, `docs/pt-BR/`. Su propio `README.md` es lo que la convierte en una (la
primera página y el índice de la ventana de Ayuda); las carpetas en inglés (`guides/`, `help/`, `concepts/`, `start/`)
no tienen ninguno.

- **Páginas.** La carpeta refleja las páginas en inglés: `docs/fr/map.md` traduce `docs/map.md`, y
  `docs/fr/guides/make-a-road.md` traduce `docs/guides/make-a-road.md`. Traduce las páginas que incluye la ventana de
  Ayuda ([writing-docs.md](../dev/writing-docs.md) enumera las que no incluye). Una página aún sin traducir se muestra
  en inglés, así que una traducción puede empezar con unas pocas páginas.
- **Enlaces.** Escríbelos como los escribe la página en inglés, desde la carpeta: `map.md#…` y `guides/x.md` desde
  `docs/fr/README.md`, `../area.md` desde `docs/fr/guides/`. Un enlace que sale de `docs/` sube un `../` más que el
  inglés (`../../README.md`, `../../editor/Core/Lang/en.json`), y lo mismo un enlace a un archivo de `docs/` que no se
  traduce (`../worldeditor.schema.json`). Los enlaces a una página que la carpeta aún no tiene siguen funcionando en el
  editor (muestra la página en inglés), pero no en GitHub hasta que se traduzca la página.
- **Imágenes.** Una imagen con palabras se vuelve a tomar en tu idioma en `docs/fr/images/`, con el mismo nombre, y se
  escribe `images/x.jpg`. Una que no tiene nada que traducir puede seguir siendo la inglesa: `../images/x.jpg`. Hasta
  que se añade a tu carpeta una imagen de `images/`, la ventana de Ayuda muestra la inglesa del mismo nombre.
- **Encabezados.** Tradúcelos; sus ids cambian con ellos (GitHub los forma a partir de las palabras:
  `#search-the-world` pasa a ser `#rechercher-dans-le-monde`), y los propios enlaces de la traducción usan los ids
  traducidos, como necesita GitHub. Conserva los encabezados de la página en inglés: los mismos, de los mismos niveles,
  en el mismo orden. Así el editor también lleva los ids en inglés a sus encabezados traducidos, de modo que sus botones
  de ayuda (`F1`, el `?` de los paneles), que nombran encabezados en inglés, llevan al lugar correcto.
- **Marca de origen.** Pon una línea en la página, la primera, que diga de qué texto en inglés se tradujo:
  `<!-- source: 1a2b3c4d -->`, los 8 primeros caracteres del SHA-256 de la página en inglés (ni GitHub ni la ventana de
  Ayuda la muestran). Obtenlos con `sha256sum docs/map.md | cut -c1-8` (macOS: `shasum -a 256`). La marca se calcula
  sobre la página con finales de línea Unix: en una copia de Windows que los haya convertido en `\r\n`, usa
  `git show HEAD:docs/map.md | sha256sum | cut -c1-8`, que da la página tal como la guarda el repositorio.

Cuando cambia la página en inglés, cambia su marca: la ventana de Ayuda sigue mostrando la traducción, con un aviso de
que la página ha cambiado en inglés y un botón que muestra la página en inglés. Para poner al día una traducción, mira
qué cambió en inglés desde entonces (`git log -p docs/map.md`), tradúcelo y escribe la nueva marca. Las pruebas
comprueban todos los enlaces e imágenes de una traducción, y que una traducción al día tenga los encabezados de su
página en inglés.

## Para desarrolladores

- Un texto que ve el usuario va en `en.json` con una clave nombrada según dónde se muestra
  (`settings.port`, `common.cancel` para palabras que se usan en todas partes) y se pide con
  `Lang.T("settings.port")`, o `Lang.T("settings.worldNotFound", path)` con valores para sus marcadores
  `{0}`… (insertados con la referencia cultural invariable). Una sugerencia emergente es una clave `tip.`, que se da con
  `.Tip("settings.port")` (consulta `editor/Desktop/Tips.cs`).
- Escribe la clave completa en el código, incluso cuando se pasa de un lado a otro: `LangCatalogueTests` comprueba que
  cada clave pedida está en `en.json` y que cada clave de `en.json` se pide en algún sitio.
- Los textos para Claude (las descripciones y respuestas de las herramientas MCP), el registro, la salida de la línea
  de comandos y los formatos de archivo se quedan en inglés y fuera del catálogo. Un texto que se muestra al usuario y
  que también se le da a Claude (el costo de una pieza) pide el inglés con `Lang.En` en el lado de Claude.
- Las ventanas se construyen una vez, en el idioma elegido al inicio (`Lang.Use` en `Program.Main`); nada tiene que
  estar pendiente de un cambio de idioma.

### Nombrar claves

- La primera parte es la ventana, escrita como sus claves de sugerencia (`area`, `place`, `select`, `path`,
  `blueprints`, `newWorld`…). Una herramienta renombrada en la ventana conserva sus claves: las de la herramienta Región
  son `area.*`, y los tipos de la herramienta Relieve conservan `shape.*` (Fórmula) y `mountain.*` junto a `landform.*`.
  Prefijos compartidos: `common.*` para una palabra usada en tres ventanas o más (OK, Sí, Cancelar, Guardar, Descartar,
  Quedarse, Conservarlos…), `dialog.*` para los diálogos pequeños, `save.*` para las preguntas de guardar, aplicar,
  descartar y salir, `biome.*` para los nombres de biomas.
- La parte central dice qué tipo de texto es: `label` (etiquetas de fila, botones, interruptores), `head`
  (encabezados de sección), `hint` (explicaciones atenuadas), `msg` (barra de estado y resultados), `undo` (etiquetas del
  Historial), `error` (problemas mostrados en un panel), `ask.<name>.title|text|yes|no` (preguntas), o el nombre de una
  lista para sus entradas (`brush.falloff.flatTop`). Los textos sueltos de una ventana pueden tener dos partes
  (`settings.port`). La última parte va en camelCase.
- Una palabra en inglés tiene una sola clave: reutiliza `common.*` en lugar de volver a añadir «Save». Los nombres que
  Claude da a los controles de la ventana se deducen del inglés (`Lang.EnglishOf`), que lo necesita.
- Un texto que pasa al catálogo conserva su redacción; cambia las palabras en un cambio aparte.
- Los textos de un enum vienen de un `switch` que nombra cada clave completa, nunca de `"prefix." + value`.

### Valores, plurales y frases

- Los valores van en `{0}`, `{1}`… con su formato y su unidad dentro del texto (`"{0:0.0} m"`,
  `"{0} × {1} zones"`), como números y nombres sin procesar, no como cadenas ya formateadas. Una clave siempre se pide
  con el mismo número de valores; un texto pedido con valores duplica sus llaves literales (<code>&#123;&#123;</code>),
  y uno pedido sin valores nunca tiene ninguna.
- Un nombre que ya está en el idioma del usuario (un bioma, un tipo de objeto) entra como valor. No cambies sus
  mayúsculas para encajarlo en una frase (`ToLowerInvariant`): escribe la frase a su alrededor.
- No hay reglas de plural. Mantén «object(s)» en un solo texto; donde el código ya elige entre uno y varios, usa dos
  claves completas (`….one`, `….many`). Nunca añadas una terminación en el código.
- Una clave por frase completa. Una frase adicional opcional es una clave propia, unida con un espacio; nunca pegues
  partes de una frase. Las listas de nombres traducidos se unen con ", ".

### Lo que se queda en inglés

- Todo lo que lee Claude: las descripciones y respuestas de las herramientas MCP (`ClaudeTools*`, `ClaudeGuide`),
  el registro y la consola, la salida de la línea de comandos, los formatos de archivo (`prefs.json`, el archivo del
  historial, los planos), los datos del juego (nombres de prefabs, de artículos y de piezas, claves de datos de los
  objetos), los nombres de productos y mods dentro de los textos (Valheim, Homestead, Steam) y los mensajes de las
  excepciones (la frase que rodea a uno sí se traduce).
- Los pasos de Claude en el Historial empiezan con la marca `Claude: ` (`ClaudeTools.ClaudesStep`), que
  se queda en inglés: Retirar los cambios de Claude encuentra los pasos de Claude gracias a ella. Las palabras que la
  siguen sí se traducen.
- Un texto que se muestra al usuario y que también se le da a Claude tiene ambos: el idioma del usuario de `Lang.T`,
  el inglés de Claude de `Lang.En` (`PieceCost.Describe(english: true)`).
- Claude vuelve a leer el mensaje de la barra de estado. Ponlo ahí con `Msg("area.msg.applied", n)`,
  o `Msg(said)` con un `Said` (`Said.Of(key, values…)`, `Said.Raw(text)` para un texto igual en
  ambos, `+` para añadir una frase): la ventana muestra el idioma del usuario y conserva el inglés, que leen las
  respuestas de Claude (`MainWindow.LastMessageEn`). Añade al último mensaje con
  `LastSaid + …`, nunca leyendo `MessageText.Text`.
- Una opción de lista recordada (`Prefs.Bind`) se guarda en inglés, para que vuelva en cualquier idioma.

### Comprobar una ventana

Las pruebas tienen un idioma inventado, `PseudoLang`, que envuelve cada texto del catálogo como `⟦…⟧`.
`PseudoLang.Untranslated(window)` enumera los textos que una ventana muestra fuera de `⟦⟧`, es decir, que no vienen
del catálogo. Cada ventana tiene una prueba `<Window>LangTests` que la construye en ese idioma y
espera que no haya ninguno; esas pruebas se ejecutan en la colección `DataDir`, ya que el idioma es compartido.
