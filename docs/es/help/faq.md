<!-- source: 93850bba -->
# Preguntas

Respuestas breves a lo que más se pregunta, cada una con un enlace a la respuesta completa.

## ¿Es seguro para mi mundo?

Sí, si guardas una copia. No se escribe nada hasta que pulsas **Guardar en el mundo** o **Aplicar en vivo**, y cada
guardado se vuelve a leer y se comprueba: si no se lee bien, se quitan los archivos nuevos y el mundo queda como
estaba. Aun así, copia un mundo que te importe antes de tu primer guardado: consulta [Hacer una copia de seguridad de
un mundo antes de editarlo](../guides/back-up-a-world.md) y [Lo que escribe el editor](../concepts/safety.md#qué-escribe-el-editor).

## ¿Puedo deshacer después de guardar?

Sí. El historial se conserva con el mundo después de cada guardado o aplicación, incluso después de cerrar el editor:
deshaz un paso (o pulsa **Quitar** en Historial) y guarda de nuevo. Consulta
[Deshacer un paso, retirar un cambio o descartarlo todo](../guides/undo-changes.md). Los restablecimientos de zona y el
suelo con Sin límite también lo conservan; lo que un restablecimiento volvió a crear queda fuera de su alcance
(consulta [Lo que un paso no puede deshacer](../concepts/saving-and-history.md#lo-que-un-paso-no-puede-retirar)).

Para daños que descubras más tarde, [Restaurar un área desde una copia de seguridad](../guides/restore-from-backup.md).

## ¿Los jugadores de mi servidor necesitan algo?

No. Solo el juego o el servidor que aloja el mundo necesita BepInEx y el plugin, y solo para editar en vivo. Los
jugadores que se unen no necesitan nada, tampoco para el suelo subido más allá del límite de 8 m del juego. Consulta
[Límites del suelo](../concepts/ground-limits.md#sin-límite).

## ¿Pueden unirse a un mundo editado los jugadores de consola en juego cruzado?

Sí: un mundo editado sigue siendo un mundo normal de Valheim, y el suelo del editor más allá de 8 m no necesita ningún
mod. Las excepciones son los mundos que necesitan un mod por sí mismos: uno creado con Riverheim, o un servidor que
usa Homestead, rechaza a los jugadores que no tienen el mod, y los jugadores de consola no pueden instalar mods.
Consulta [Un mundo nuevo](../new-world.md#consejos-y-límites).

## ¿Puedo editar mientras el juego está en marcha?

Sí, en modo en vivo, con el plugin WorldEditorBridge en el juego o el servidor: consulta
[Editar el mundo en el que juegas](../guides/edit-your-game-live.md) o
[Editar un servidor dedicado mientras la gente juega](../guides/edit-a-server-live.md). Para editar los archivos de un
mundo guardado, cierra antes el juego o detén el servidor: un juego en marcha guarda encima de lo que escribe el
editor. Consulta [El juego guardó mientras editabas](troubleshooting.md#el-juego-guardó-mientras-editabas).

## ¿Puedo editar un servidor que alquilo a un proveedor de alojamiento?

Sí, pero en vivo solo si el proveedor te da acceso SSH a la máquina (un inicio de sesión en una consola, no solo SFTP
o un gestor de archivos): pregúntaselo antes de alquilar. La mayoría de los servidores de juego alquilados ofrecen
solo un panel web y FTP, así que el editor no puede llegar al plugin: detén el servidor, descarga su mundo, edítalo y
vuelve a subirlo. Un servidor que un panel de juegos como Pterodactyl ejecuta en un contenedor en tu propia máquina se
puede editar en vivo, a través de un túnel que abres tú mismo. Consulta
[Editar un servidor alquilado o alojado con panel](../guides/edit-a-rented-server.md).

## El editor pide un token. ¿Dónde está?

En `BepInEx/config/Tie.WorldEditorBridge.cfg` del juego o del servidor, en la línea `Token`. El plugin escribe ese
archivo la primera vez que el juego arranca con él. Para tu propio juego el editor lo lee solo; para un servidor, lo
copias del servidor. Consulta [Configuración del plugin](../live-mode.md#configuración-del-plugin).

## ¿Funciona con mods?

Sí. En un juego con mods en vivo, el editor aprende los objetos de los mods y dibuja sus modelos (consulta
[Juegos con mods](../live-mode.md#juegos-con-mods)). Los mods que cambian cómo se crean los mundos necesitan su
complemento del editor: Expand World, Riverheim y Tunnelheim tienen uno. Consulta
[Editar un mundo creado con Expand World, Riverheim o Tunnelheim](../guides/use-mods-worlds.md).

## Juego con los mods de Expand World. ¿Lo sabe el editor?

Sí, con el complemento Expand World instalado: el suelo, el mapa, los biomas y las previsiones siguen la configuración
y los archivos de datos de los mods. Sin él, cada mundo se genera como lo hace el propio juego. Consulta
[Editar un mundo creado con Expand World, Riverheim o Tunnelheim](../guides/use-mods-worlds.md).

## Mi mundo se creó con Riverheim. ¿Puedo abrirlo?

Sí, con el complemento Riverheim instalado y el propio Riverheim en una carpeta de BepInEx que el editor conozca: el
editor dibuja el mundo con tu propia copia del mod. Consulta [Editar un mundo creado con Expand World, Riverheim o Tunnelheim](../guides/use-mods-worlds.md).

## ¿Necesito Homestead?

Solo para construir en el juego los planos del editor. El Taller, la biblioteca y pegar planos en un mundo funcionan
sin él; cuando falta, la página de inicio lo dice. Ten en cuenta que un servidor que usa Homestead rechaza a los
jugadores que no lo tienen. Consulta [El Taller](../workshop.md).

## ¿Dónde lo descargo? ¿Está en Thunderstore, Hexium o Nexus Mods?

- **El editor** es un programa, no un mod: descárgalo de la
  [página de versiones](https://github.com/Shuiei/valheim-world-editor/releases/latest) o de
  [Nexus Mods](https://www.nexusmods.com/valheim/mods/4348).
- **El plugin WorldEditorBridge** (para editar en vivo) está en la página de versiones, en Nexus Mods,
  [Thunderstore](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) y
  [Hexium](https://valheim.hexium.gg/mods/Tie/WorldEditorBridge), para los gestores de mods.
- **Los complementos** para mods están en la página de versiones y en Nexus Mods.

Usa el plugin y los complementos de la misma versión que el editor. Consulta [Instalar](../start/install.md).

## ¿Envía algo por internet?

No. Todo ocurre en tu equipo; a un servidor dedicado solo se llega a través del túnel SSH cifrado del propio editor,
con un token que escribes tú. Consulta [Nada sale de tu equipo](../concepts/safety.md#nada-sale-de-tu-equipo).

## ¿Es seguro dejar que Claude o un script cambien mi mundo?

Lo que Claude cambia queda pendiente hasta que guardas, y solo puede conectarse desde este equipo con un token. Los
scripts son programas que se ejecutan con tus permisos, así que Claude solo puede ejecutarlos si se lo permites.
Consulta [Scripts y Claude](../concepts/safety.md#scripts-y-claude).

## Algo no funciona. ¿Dónde miro?

Empieza por [Solución de problemas](troubleshooting.md). Para informar de un error, adjunta el registro del editor:
[El registro y los informes de errores](troubleshooting.md#el-registro-y-los-informes-de-errores).
