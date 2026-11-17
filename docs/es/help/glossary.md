<!-- source: dadc6866 -->
# Glosario

Las palabras que usan el editor y esta ayuda, de la A a la Z. Cada entrada enlaza al lugar donde se explica.

**Ancla.** El centro de la parcela del Taller, a ras de suelo: el punto desde el que un plano de Homestead mide cada
pieza. Consulta [El Taller](../workshop.md).

**Aplicado.** Enviado al juego en marcha con **Aplicar en vivo** (o **Auto**). El Historial marca esos pasos como
**aplicado**; aún se pueden deshacer. Consulta [Aplicar en vivo](../concepts/saving-and-history.md#aplicar-en-vivo).

**Área.** El bloque de zonas abierto en la ventana 3D, de 3 × 3 a 17 × 17 zonas. Consulta
[El área](../concepts/areas-and-zones.md#el-área).

**Aspecto del juego.** Dibujar el mundo con las texturas y modelos del propio juego, leídos de tu Valheim.
Desactivado: colores lisos, más rápido. Consulta [Panel Vista](../editor-basics.md#panel-vista).

**Atenuación.** Cómo se desvanece la intensidad de un pincel desde su centro hasta su borde. Consulta [Esculpir y
pintar](../terrain.md).

**Auto.** Un interruptor del modo en vivo que aplica cada cambio al juego en cuanto lo haces. Consulta
[Modo en vivo](../live-mode.md).

**Barra de construcción.** La barra de ajustes de construcción junto a la barra de herramientas, compartida por
Construir, Selección y Pegar: Encaje, Cuadrícula, Ángulo de giro, Alzar y Construido por. Consulta [La barra de
construcción](../build.md#la-barra-de-construcción).

**BepInEx.** El cargador de mods de Valheim. El juego o el servidor que aloja un mundo lo necesita, con el plugin
WorldEditorBridge, para editar en vivo. Consulta [Instalar el editor](../start/install.md#el-plugin-para-editar-en-vivo).

**Bioma.** Un tipo de tierra en Valheim: Praderas, Selva Negra, Pantano, Montaña, Llanuras, Tierras de niebla, Tierras
de ceniza, Extremo Norte, Océano. La [Máscara](../masks.md) puede limitar una herramienta a uno.

**Bioma alternativo.** Una variante de un bioma que el juego da a algunas zonas a partir de la semilla (Dark Meadows,
Rock Black Forest…), con lo que crece allí. La lectura del mapa la nombra. Consulta [Región, copiar y
pegar](../area.md) (Regenerar la naturaleza).

**Borde bloqueado.** La línea exterior de puntos del suelo del área, que las herramientas no mueven, para que el área
se una a sus vecinas. Consulta [El borde bloqueado](../concepts/areas-and-zones.md#el-borde-bloqueado).

**Carpeta del mundo.** La carpeta que contiene un mundo: `_main.<n>.chunks`, `.db2`, `.fwl2`, `.ok` y los archivos
`.chunk`. Los mundos de un jugador están en `worlds_local`. Consulta [Abrir un mundo que no está en la
lista](../guides/open-a-world.md).

**Complemento.** Una descarga aparte que enseña al editor un mod (Expand World, Riverheim, Tunnelheim, Railroads,
ValheimRAFT), descomprimida en la carpeta `addons` junto al editor. Consulta
[Instalar el editor](../start/install.md#complementos-para-mods).

**Comprobación de soporte.** Los colores del Taller que indican lo bien que se sostiene cada pieza, como en el modo de
construcción del juego. Consulta [La comprobación de soporte](../concepts/objects.md#la-comprobación-de-soporte).

**Construido por.** El jugador que se escribe como constructor de las piezas que construyes, pegas o reemplazas,
elegido en [la barra de construcción](../build.md#la-barra-de-construcción). Consulta
[Piezas construidas por jugadores](../concepts/objects.md#piezas-construidas-por-jugadores).

**Construido por jugador.** Una pieza que guarda quién la construyó, como hace el juego con lo que coloca el martillo.
Consulta [Piezas construidas por jugadores](../concepts/objects.md#piezas-construidas-por-jugadores).

**Copia de seguridad.** Una copia de la carpeta de un mundo, llamada `<World>_backup_<something>`, junto a ella. El
juego hace las suyas (`_backup_auto-`); tú haces la tuya antes de editar. Consulta [Copias de
seguridad](../concepts/saving-and-history.md#copias-de-seguridad) y [Hacer una copia de seguridad de un mundo antes de
editarlo](../guides/back-up-a-world.md).

**Corte (de la vista).** El control deslizante de la barra de estado que oculta todos los objetos por encima de una
altura, para ver y trabajar dentro de los edificios. No es lo mismo que cortar al portapapeles (`Ctrl+X`). Consulta
[Corte](../editor-basics.md#corte).

**Descartar.** El botón de la barra superior que deshace todo lo que aún no está guardado ni aplicado. Consulta
[Deshacer un paso, retirar un cambio o descartarlo todo](../guides/undo-changes.md).

**Disco de suelo.** Una ubicación invisible que el editor escribe para mantener el suelo más allá del límite de 8 m
del juego. Consulta [Sin límite](../concepts/ground-limits.md#sin-límite).

**Encaje.** Piezas que se unen por sus puntos de encaje, como con el martillo del juego. Consulta
[Encaje](../concepts/objects.md#encaje).

**Extensión.** Código que aporta un mod para enseñar al editor sus piezas y datos, que se activa en
**Configuración**. Consulta [Para autores de mods](../modding.md).

**Grupo enlazado.** Objetos que un mod une, como un barco y sus partes; se seleccionan y se mueven como uno solo.
Consulta [Selección](../select.md).

**Guardado (numerado).** El juego guarda un mundo como guardados numerados (`_main.<n>.*`); **Guardar en el mundo**
escribe el siguiente y lo comprueba. Consulta [Guardar un mundo](../concepts/saving-and-history.md#guardar-un-mundo).

**Herramienta Región.** La herramienta (`B`) que selecciona un rectángulo o un polígono de suelo para nivelar, pintar,
copiar o restaurar, o para elegir los objetos que contiene para la herramienta Selección. No es lo mismo que el
**Área**, el bloque de zonas abierto en 3D. Consulta [Región, copiar y pegar](../area.md).

**Herramienta Vista.** La herramienta (`Esc` o `H`) para mirar alrededor y elegir objetos sin cambiar nada. No es lo
mismo que el panel **Vista** (`V`). Consulta [La ventana del editor](../editor-basics.md#la-barra-de-herramientas).

**Historial.** La lista de tus pasos en un mundo (`L`), para deshacer, rehacer o quitar uno. Se conserva después de
cada guardado o aplicación, así que vuelve cuando abres de nuevo el mundo. Consulta
[Historial](../concepts/saving-and-history.md#historial).

**Homestead.** Un mod de construcción de Valheim cuyos planos guarda el editor; con él en tu juego, los construyes con
el martillo. No hace falta para pegar planos en el editor. Consulta [El Taller](../workshop.md).

**Inspector.** El panel que muestra y cambia lo que contiene un objeto: los artículos de un cofre, el texto de un
cartel, la etiqueta de un portal. Consulta [Selección](../select.md).

**Marcas de límite.** Rojo en el suelo que llegó al límite de 8 m del juego. Consulta
[Marcas de límite](../concepts/ground-limits.md#marcas-de-límite).

**Máscara.** Una tarjeta que limita una herramienta al suelo de un bioma, una altura, una pendiente o una pintura.
Consulta [Máscara](../masks.md).

**Mazmorra.** Salas que el juego guarda 5000 m por encima de su entrada en el suelo: criptas, cuevas de escarcha y
similares. Consulta [Cómo guarda el juego las mazmorras](../concepts/dungeons.md#cómo-guarda-el-juego-las-mazmorras).

**MCP.** La forma en que Claude Code y Claude Desktop llaman a las herramientas del editor cuando permites que Claude
se conecte. Consulta [Claude](../claude.md).

**Modo en vivo.** Editar un mundo mientras el juego o el servidor lo ejecuta, a través del plugin. Consulta
[Cómo se conecta](../concepts/live-editing.md#cómo-se-conecta).

**Mundo guardado.** La carpeta de un mundo en este equipo, editado con el juego cerrado. Consulta
[La ventana del editor](../editor-basics.md#la-página-de-inicio).

**Panel Construir.** El panel de la herramienta Construir a lo largo de la parte inferior de la ventana, con las
piezas del juego por pestañas, en un mundo y en el Taller. Consulta [El panel Construir](../build.md#el-panel-construir).

**Paso.** Un cambio en el Historial: un trazo de pincel, un clic, un pegado, una ejecución de script. `Ctrl+Z` deshace
un paso. Consulta [Cambios pendientes](../concepts/saving-and-history.md#cambios-pendientes).

**Pendiente.** Un cambio hecho en el editor que aún no está guardado ni aplicado. La barra superior los cuenta.
Consulta [Cambios pendientes](../concepts/saving-and-history.md#cambios-pendientes).

**Pincel.** Una herramienta que arrastras sobre el suelo: los pinceles de esculpir (Elevar, Bajar, Aplanar…) y el
pincel Pintar con sus cuatro pinturas (Tierra, Cultivar, Pavimento, Quitar pintura). Colocar también tiene un pincel.
Consulta [Esculpir y pintar](../terrain.md).

**Plano.** Una construcción guardada, en el formato del mod Homestead: construida en el Taller o copiada de un mundo,
y pegada en cualquier mundo. Consulta [El Taller](../workshop.md).

**Plugin.** WorldEditorBridge, el plugin de BepInEx del editor en el juego o el servidor que aloja el mundo. Permite
al editor leer y cambiar el mundo en marcha. Consulta [Instalar el editor](../start/install.md#el-plugin-para-editar-en-vivo).

**Portapapeles.** Lo que contiene la última copia o el último corte, guardado hasta que vuelves a copiar, incluso
después de reiniciar el editor. Consulta [Región, copiar y pegar](../area.md).

**Prefab.** El nombre interno que da el juego a un tipo de objeto, como `Beech1` o `woodwall`. Las búsquedas lo
aceptan.

**Puente.** Abreviatura de WorldEditorBridge, el plugin del editor en el juego. Consulta **Plugin**.

**Punto de aparición.** Un objeto invisible del juego donde aparecen criaturas. Consulta
[Puntos de aparición](../concepts/objects.md#puntos-de-aparición).

**Recargar.** En modo en vivo, volver a leer el mundo desde el juego. Consulta [Modo en vivo](../live-mode.md).

**Relieve.** Un montículo, un cráter, una montaña o una imagen de alturas que se pone entera en el suelo con un clic
de la herramienta Relieve (`G`). Consulta [Relieve](../landform.md).

**`savedir`.** La carpeta en la que un servidor dedicado guarda sus mundos (su opción `-savedir`); sus mundos están en
`<savedir>/worlds_local`. Consulta [Abrir un mundo que no está en la lista](../guides/open-a-world.md).

**Seguir.** Un interruptor de la barra superior: el área crece sola cuando miras cerca de su borde. Consulta
[Seguir](../concepts/areas-and-zones.md#seguir).

**Sello.** Una forma de pincel hecha a partir de una imagen de alturas: pintas con ella. La herramienta
[Relieve](../landform.md#imagen) pone las mismas imágenes enteras en el suelo, con un clic. Consulta [Esculpir y
pintar](../terrain.md#sellos).

**Semilla.** La palabra a partir de la que se crea un mundo: la misma semilla da la misma tierra, los mismos biomas y
las mismas ubicaciones. Consulta [Un mundo nuevo](../new-world.md).

**Sesión anterior.** Un paso del Historial conservado de una ejecución anterior del editor, que vuelve cuando abres
de nuevo el mundo. Consulta [Historial](../concepts/saving-and-history.md#historial).

**Sin límite.** Un interruptor de la tarjeta Máscara que permite a las herramientas de suelo moverlo más allá del
límite de 8 m del juego, guardado como discos de suelo. Consulta
[Sin límite](../concepts/ground-limits.md#sin-límite).

**Taller.** Una parcela vacía, fuera de cualquier mundo, en la que construir y conservar la construcción como plano.
Consulta [El Taller](../workshop.md).

**Token.** El secreto del plugin, en `BepInEx/config/Tie.WorldEditorBridge.cfg`, que el editor debe dar para
conectarse. Consulta [Cómo se conecta](../concepts/live-editing.md#cómo-se-conecta).

**Túnel.** La conexión SSH cifrada que abre el editor para llegar al plugin en un servidor dedicado. Consulta
[Editar un servidor dedicado mientras la gente juega](../guides/edit-a-server-live.md).

**Ubicación.** Un lugar que el juego pone en el mundo al crearlo: aldeas, el comerciante, los altares de los jefes,
las entradas de las mazmorras. Las ubicaciones nivelan el suelo a su alrededor. Consulta
[Las ubicaciones nivelan el suelo](../concepts/ground-limits.md#las-ubicaciones-nivelan-el-suelo).

**Zona.** Un cuadrado de 64 × 64 m del mundo, la unidad que el juego carga y guarda. Consulta
[Zonas](../concepts/areas-and-zones.md#zonas).

**Zona generada.** Una zona que el juego ya ha creado porque un jugador se acercó a ella. Solo las zonas generadas
tienen datos en los que el editor pueda guardar. Consulta [Zonas](../concepts/areas-and-zones.md#zonas).

**Zona para restablecer.** Una zona marcada para que el juego la vuelva a crear desde cero la próxima vez que la
cargue. Consulta [Volver a generar zonas](../guides/reset-zones.md).
