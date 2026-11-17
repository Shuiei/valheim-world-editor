<!-- source: 4978ef09 -->
# Seguridad y privacidad

El editor es un programa de tu equipo. Escribe en tus mundos solo cuando pulsas **Guardar en el mundo**, no envía nada
a ningún sitio salvo que lo conectes a un servidor o a Claude, y mantiene los secretos fuera de su registro. Esta
página explica qué escribe, qué puede salir de tu equipo y qué se ejecuta con tus permisos.

## Qué escribe el editor

**Tus mundos**, solo cuando guardas. Hasta entonces, cada cambio está pendiente y vive en la memoria del editor
(consulta [Cambios pendientes](saving-and-history.md#cambios-pendientes)). Un guardado añade un nuevo guardado numerado a
la carpeta del mundo y no quita nada antiguo hasta haber vuelto a leer correctamente el nuevo (consulta
[Guardar un mundo](saving-and-history.md#guardar-un-mundo)). Un mundo nuevo va a una carpeta nueva, nunca encima de un
mundo con el mismo nombre. Cierra antes el juego: un juego en marcha sobrescribe los archivos que guardó el editor.

**Una partida en vivo**, solo cuando aplicas. El juego guarda entonces los cambios él mismo, igual que guarda lo que
construyen los jugadores.

**Tus planos**: un archivo `.blueprint` y su imagen `.png` en la carpeta de Homestead, dentro de la carpeta de
guardado de Valheim, cuando guardas un plano. **Eliminar** quita ambos, tras preguntar.

**Su propia carpeta de datos**: `~/.local/share/ValheimWorldEditor` en Linux, `%LOCALAPPDATA%\ValheimWorldEditor` en
Windows, `~/Library/Application Support/ValheimWorldEditor` en macOS (el final del menú de la página de inicio la
muestra). Contiene:

| Qué | Archivos |
|---|---|
| Tu configuración y tus elecciones | `settings.json`, `prefs.json`, `place.json` (preajustes de Colocar, favoritos, tipos recientes), `stamps.json`, `selections.json` |
| Servidores guardados | `servers.cfg`: dirección, usuario, token, la identidad del servidor y la contraseña cuando marcaste **Guardar contraseña** (ver más abajo) |
| Historial | `history/`, un archivo por mundo (consulta [Historial](saving-and-history.md#historial)) |
| Tu trabajo | `scripts/` (scripts guardados), `heightmaps/` (mapas de alturas exportados), `blueprints/export` (archivos `.vbuild`) |
| Lo que aprendió de tu juego | `game-index.json` y otros archivos `game-*.json` (dónde están las cosas en los archivos de Valheim), `mod-catalogue.json` (objetos de mods vistos en una partida en vivo), `mod-bundles/` (modelos de mods, copiados de sus DLL) |
| El registro | `ValheimWorldEditor.log`, uno nuevo en cada ejecución; `perf-native.log` solo mientras **Registrar fotogramas por segundo** está activado |

**Lo que nunca cambia.** El editor lee tu carpeta del juego Valheim, tus carpetas de BepInEx y los archivos de tus mods
(modelos, texturas, las reglas del juego, los ajustes del plugin) y no escribe nada allí. Lee el código de los mods
para aprender los nombres de sus valores, y nunca lo ejecuta.

**Contraseñas.** Una contraseña guardada se conserva como texto sin cifrar en `servers.cfg`. En Linux solo tú puedes
leer el archivo; aun así, usa una clave SSH siempre que puedas. La frase de contraseña de una clave nunca se guarda.
**olvidar**, junto a un servidor guardado, lo quita, y con él su contraseña y su token.

## Nada sale de tu equipo

El editor no tiene cuenta, ni seguimiento, ni comprobación de actualizaciones. Solo se comunica con el exterior en los
casos de abajo, y cada uno de ellos lo eliges tú:

| Cuándo | Qué va adónde |
|---|---|
| Te conectas a un **servidor dedicado** | Una conexión SSH, cifrada, solo a ese servidor, con la cuenta que das. Se llega al plugin a través de ella, nunca por internet abierto (consulta [Cómo se conecta](live-editing.md#cómo-se-conecta)). |
| Editas en vivo **tu propio juego** | Nada sale del equipo: el editor habla con el plugin en `127.0.0.1`. |
| Conectas **Claude** | La aplicación de Claude de tu equipo habla con el editor en `127.0.0.1`. Lo que responde el editor (los datos del área, imágenes de la vista 3D) va a Claude, como cualquier cosa que le des a Claude. |
| Haces clic en un enlace | **Obtener Homestead**, o una página de ayuda abierta en GitHub, se abre en tu navegador web. |

**El plugin se mantiene privado.** WorldEditorBridge solo escucha en el equipo en el que se ejecuta, y solo responde a
las peticiones que llevan su token secreto. Para un servidor, el token lo escribes tú; el editor nunca lo lee del
servidor, así que un inicio de sesión SSH por sí solo no puede cambiar el mundo. Mantén el token en privado: cualquiera
que lo tenga y tenga acceso al puerto puede cambiar el mundo. Vacía la línea `Token` de `Tie.WorldEditorBridge.cfg` y
reinicia el juego para obtener uno nuevo.

**Se comprueba la identidad del servidor.** El editor recuerda la identidad de un servidor en la primera conexión. Si
alguna vez cambia, el editor se niega a conectarse, porque alguien podría estar haciéndose pasar por tu servidor.
Después de reinstalar un servidor, vacía su línea `HostKey` en `servers.cfg` para aceptar la nueva.

## Scripts y Claude

**Los scripts son programas.** Un [script](../scripting.md) es código C#, y el editor lo ejecuta con tus permisos,
como cualquier programa que inicias. Se ejecuta en un proceso aparte para que **Detener** siempre pueda terminarlo,
pero ese proceso no es un entorno aislado: un script puede leer y escribir tus archivos y hacer cualquier otra cosa que
pueda hacer tu cuenta. Ejecuta solo scripts que hayas escrito tú o en los que confíes, igual que harías con un mod.

**Claude trabaja para ti, con límites.** Con **Permitir que Claude se conecte** activado en Configuración,
[Claude](../claude.md) puede usar las herramientas del editor. El editor lo somete a esto:

- **Solo este equipo, con un token.** El editor solo escucha en `127.0.0.1`, rechaza las peticiones dirigidas a otro
  nombre (así una página web no puede llegar a él con una dirección inventada) y rechaza cualquier petición sin su
  token (48 caracteres al azar). Otra **Dirección** en Configuración (`0.0.0.0`, o una de las de este equipo) permite
  que otros equipos de esa red se conecten, con el token, que entonces viaja sin cifrar: solo equipos con una dirección
  local en la propia red de este equipo, nunca los de internet, y nunca scripts (siguen reservados al Claude de este
  equipo). **Nuevo token** crea otro; desactiva el interruptor y nada podrá conectarse.
- **Solo pendiente.** Todo lo que cambia Claude queda pendiente, un paso cada vez, con la etiqueta **Claude** en el
  Historial. Claude nunca guarda, aplica en vivo, descarta, recarga, escribe ni elimina tus archivos, ni se conecta a
  una partida en vivo: eso sigue siendo cosa tuya. No abre otro mundo mientras el tuyo tiene cambios sin guardar.
- **Nada en vivo con Auto activado.** Mientras un mundo en vivo tiene **Auto** activado, cada cambio llegaría a los
  jugadores al instante, así que Claude no cambia nada y te pide que desactives Auto.
- **Sin scripts sin tu permiso.** Los scripts de Claude se rechazan hasta que activas **Claude puede ejecutar
  scripts** en Configuración (desactivado por defecto). Incluso entonces, cada script que escribe Claude se te muestra
  antes de ejecutarse, para que lo ejecutes o lo rechaces: un texto de un mundo (un cartel, una etiqueta escrita por
  otro jugador) podría convencer a Claude de escribir uno dañino. Sus demás herramientas ejecutan el propio código del
  editor y no preguntan.
- **Fácil de retirar.** **Retirar los cambios de Claude**, en el Historial, quita cada paso que Claude hizo en el área
  abierta y conserva los tuyos (consulta [Historial](saving-and-history.md#historial)).

## Extensiones y complementos de mods

Un mod puede incluir una **extensión del editor**: un pequeño fragmento de código que el editor carga para leer y
dibujar los objetos de ese mod. Como un script, se ejecuta con tus permisos, así que decides tú. Cada extensión está
**desactivada** hasta que la activas en **Configuración → Extensiones de mods**, y solo ese archivo exacto: el editor
recuerda su huella (SHA-256), así que un archivo cambiado, como una actualización del mod, vuelve a estar desactivado
hasta que lo vuelves a activar.

Los **complementos**, que descargas y descomprimes tú mismo en la carpeta `addons` del editor, se ejecutan tal como
están instalados, ya que instalar uno ya es decisión tuya. **Configuración → Complementos** desactiva cualquiera de
ellos.

## Lo que el registro deja fuera

El editor escribe lo que hace, y cada error que gestiona, en `ValheimWorldEditor.log`. **Abrir registro**, al final del
menú de la página de inicio, lo abre, para que puedas adjuntarlo a un informe de error. Antes de escribir una línea, se
quitan los datos personales:

- tu carpeta personal se escribe `~`, y las demás rutas de archivo, direcciones web, nombres `user@host`, direcciones
  IP y de servidor y tu nombre de cuenta se sustituyen por marcadores como `<path>`;
- las cuentas, contraseñas, frases de contraseña y tokens de los servidores guardados nunca llegan a él.

El plugin WorldEditorBridge escribe en el propio `BepInEx/LogOutput.log` del juego, también con la carpeta personal
del servidor escrita como `~`.

Aun así, lee un registro antes de publicarlo. Y guarda tus propias copias de los mundos que te importan (consulta
[Hacer una copia de seguridad de un mundo antes de editarlo](../guides/back-up-a-world.md)).
