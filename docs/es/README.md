<!-- source: d6008f75 -->
# Ayuda de Valheim World Editor

Valheim World Editor abre un mundo de Valheim, guardado o en marcha, en una ventana 3D. Da forma al suelo, construye,
coloca y mueve lo que quieras, y luego guárdalo de vuelta o mira cómo cambia en el juego mientras trabajas.

**¿Eres nuevo?** [Instalar el editor](start/install.md) y luego sigue [tu primera edición](start/first-edit.md): diez
minutos sobre una copia de un mundo, sin nada que perder. ¿Buscas algo? Busca en la ayuda (`Ctrl+F` en la ventana de
Ayuda) o parte de una de las tareas de abajo. En el editor, `F1` o el pequeño **?** arriba de un panel abre la ayuda de
esa herramienta.

> [!IMPORTANT]
> Guarda una copia de un mundo antes de cambiarlo: consulta [Hacer una copia de seguridad de un mundo antes de editarlo](guides/back-up-a-world.md).
> El editor comprueba que cada guardado se vuelve a leer correctamente antes de soltar el anterior, pero no guarda
> ninguna copia propia.

## Primeros pasos

- [Instalar el editor](start/install.md): descarga, primer inicio en Windows, Linux o macOS, y el plugin para la
  edición en vivo.
- [Tu primera edición](start/first-edit.md): abre una copia de un mundo, eleva una colina, planta árboles, guarda y
  míralo en el juego.
- [Tu primera construcción](start/first-build.md): construye una cabaña en el Taller, guárdala como plano y pégala en un
  mundo.

## Editar mundos

- [Hacer una copia de seguridad de un mundo antes de editarlo](guides/back-up-a-world.md)
- [Abrir un mundo que no está en la lista](guides/open-a-world.md)
- [Editar el mundo en el que juegas](guides/edit-your-game-live.md)
- [Editar un servidor dedicado mientras la gente juega](guides/edit-a-server-live.md)
- [Editar un servidor alquilado o alojado con panel](guides/edit-a-rented-server.md)
- [Deshacer un paso, retirar un cambio o descartarlo todo](guides/undo-changes.md)
- [Restaurar un área desde una copia de seguridad](guides/restore-from-backup.md)
- [Volver a generar zonas](guides/reset-zones.md)
- [Encontrar un objeto, un artículo de un cofre o un cartel](guides/find-things.md)
- [Editar un mundo creado con Expand World, Riverheim o Tunnelheim](guides/use-mods-worlds.md)

## Dar forma al terreno

- [Elevar o excavar más allá del límite de 8 m del juego](guides/raise-past-the-limit.md)
- [Hacer un camino o una rampa colina arriba](guides/make-a-road.md)
- [Excavar un río o un estanque](guides/make-a-river.md)
- [Despejar árboles y rocas y nivelar un solar](guides/clear-an-area.md)

## Construir y llenar

- [Plantar un bosque, un campo o un seto](guides/plant-a-forest.md)
- [Mover o copiar un edificio](guides/move-a-building.md)
- [Cambiar lo que contiene un cofre o lo que dice un cartel](guides/fill-a-chest.md)
- [Pegar un plano en un mundo](guides/paste-a-blueprint.md)
- [Generar una mazmorra y ponerla en el mundo](guides/make-a-dungeon.md)
- [Buscar una semilla con lo que quieres cerca del inicio](guides/find-a-seed.md)
- [Dejar que Claude modele o construya](guides/connect-claude.md)

## Herramientas

- [La ventana del editor](editor-basics.md): la página de inicio, la barra superior, la barra de herramientas, la
  cámara, el área, el guardado, el Historial, el panel Vista y Cortar.
- [El mapa del mundo](map.md): encuentra un lugar, ábrelo en 3D, busca en todo el mundo.
- [Esculpir y pintar](terrain.md): los pinceles del suelo, los sellos y Sin límite.
- [Relieve](landform.md): un relieve entero con un clic, a partir de una fórmula propia (colinas, cráteres, mesetas,
  fosos), del generador de montañas (picos, crestas y volcanes más allá del límite del juego) o de una imagen.
- [Camino](path.md): caminos, rampas y ríos a lo largo de una línea.
- [Máscara](masks.md): limita cualquier herramienta a un bioma, una altura, una pendiente o una pintura; su tarjeta
  también contiene el interruptor Sin límite.
- [Medir y superposiciones](measure.md): distancias, pendientes, colores de pendiente y curvas de nivel.
- [Región, copiar y pegar](area.md): nivela o pinta un recuadro o un polígono, elige sus objetos, cópialo y pégalo,
  regenéralo o restáuralo desde una copia de seguridad.
- [Colocar](place.md): árboles, rocas, cultivos y puntos de aparición.
- [Construir](build.md): las piezas que construyen los jugadores, de una en una o en líneas, con la barra de
  construcción.
- [Selección](select.md): elige, mueve, gira, copia, reemplaza y elimina objetos, y mira dentro de ellos.
- [El Taller](workshop.md): construye en una parcela vacía y consérvalo como plano.
- [Mazmorras](dungeons.md): genera una mazmorra o edita las salas del juego una a una.
- [Un mundo nuevo](new-world.md): ve el mundo de una semilla antes de que exista, y créalo.
- [Modo en vivo](live-mode.md): Aplicar en vivo, Auto, Recargar y los jugadores, mientras el juego está en marcha.
- [Script](scripting.md): da forma, pinta y llena un área con scripts de C#.
- [Claude](claude.md): lo que Claude puede hacer en el editor, y sus límites.

## Cómo funciona

- [Áreas y zonas](concepts/areas-and-zones.md): por qué el editor abre el mundo bloque a bloque.
- [Guardado e historial](concepts/saving-and-history.md): cambios pendientes, guardar, aplicar en vivo, deshacer y
  copias de seguridad.
- [Límites del suelo](concepts/ground-limits.md): el límite de 8 m del juego y cómo lo supera el editor.
- [Edición en vivo](concepts/live-editing.md): cómo habla el editor con un juego en marcha.
- [Objetos](concepts/objects.md): qué es un objeto, el encaje, la comprobación de soporte, las reglas de crecimiento, los
  puntos de aparición.
- [Las mazmorras en el juego](concepts/dungeons.md): cómo guarda el juego las mazmorras y cómo construye una el
  generador.
- [Seguridad](concepts/safety.md): qué escribe el editor y por qué nada sale de tu equipo.

## Ayuda

- [Solución de problemas](help/troubleshooting.md): qué hacer cuando algo no funciona.
- [Preguntas](help/faq.md): respuestas breves a lo que más se pregunta.
- [Atajos](help/shortcuts.md): cada tecla y acción del ratón.
- [Glosario](help/glossary.md): las palabras que usa el editor.

## Para traductores y autores de mods

- [Traducir](translating.md): los catálogos de idioma, cómo hacer uno y cómo traducir esta documentación.
- [Para autores de mods](modding.md): lo que el editor aprende de los mods, `worldeditor.json`, las extensiones y los
  complementos.
