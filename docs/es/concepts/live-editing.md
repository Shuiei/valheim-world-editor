<!-- source: 6167da37 -->
# Edición en vivo

En el modo en vivo, el editor trabaja sobre el mundo de un juego **en marcha** en lugar de sobre sus archivos. Los
jugadores pueden seguir conectados: ven tus cambios en cuanto los aplicas, y tú ves lo que construyen mientras editas.
Esta página explica cómo llega el editor al juego, qué viaja entre ambos y qué pasa cuando los jugadores y el editor
cambian el mismo lugar.

## Cómo se conecta

El modo en vivo necesita un pequeño plugin, **WorldEditorBridge**, dentro del juego que **aloja** el mundo: el servidor
dedicado, o tu propio juego cuando juegas en solitario o alojas la partida. Funciona sobre **BepInEx**, el cargador de
mods habitual de Valheim. Los jugadores que se unen no necesitan ninguno de los dos, tampoco los de consola, y la
edición sin conexión tampoco.

El plugin responde al editor en un puerto del equipo en el que se ejecuta (5182 al principio), y está hecho para seguir
siendo privado:

- **Solo escucha en ese mismo equipo** (`127.0.0.1`), así que nada de la red puede llegar a él. Su ajuste
  `BindAddress` puede abrirlo a las redes locales y las VPN del equipo (nunca a internet: esas conexiones se rechazan),
  con el token sin cifrar; déjalo como está, salvo que edites desde otro equipo de casa
  ([Sin SSH](../live-mode.md#sin-ssh)).
- **Necesita su token secreto.** En su primer inicio, el plugin escribe `BepInEx/config/Tie.WorldEditorBridge.cfg`
  con un **token** de 24 letras y cifras al azar. Una petición sin él se rechaza. Hasta que el juego o el servidor se
  ha iniciado una vez con el plugin, no hay token.

**Tu propio juego.** El editor se ejecuta en el mismo equipo, así que lee él mismo el puerto y el token de ese archivo,
en cada carpeta de BepInEx que conoce (tu carpeta de Valheim, los perfiles de tu gestor de mods, las carpetas añadidas
en **Configuración**), y pregunta a cada plugin encontrado si hay un mundo cargado.

**Un servidor dedicado.** No se puede llegar al plugin directamente desde tu equipo, así que el editor se abre su
propio camino:

```text
 tu equipo                                       el servidor
 ┌──────────────────────┐    SSH, cifrado    ┌─────────────────────────────────────┐
 │ editor ─ puerto 15182═╪═══════════════════╪═► 127.0.0.1:5182  WorldEditorBridge │
 └──────────────────────┘                    │                   dentro de Valheim │
                                             └─────────────────────────────────────┘
```

1. Inicia sesión en el servidor por **SSH** con tu cuenta (tiene su propio SSH, así que no hace falta ningún programa
   `ssh`).
2. Redirige un puerto libre de tu equipo (de 15182 en adelante), a través de esa conexión cifrada, al puerto del plugin
   en el servidor: un **túnel SSH**.
3. Envía el token, que escribes **tú**: el editor nunca lo lee del servidor. Así que un inicio de sesión SSH por sí solo
   no basta para cambiar el mundo; quien se conecte también debe conocer el secreto del plugin.

Un servidor que un panel de juegos (Pterodactyl, Pelican) ejecuta en un contenedor Docker tiene su propio `127.0.0.1`,
al que no llega el túnel hacia la máquina, y un servidor de juego alquilado a un proveedor no suele dar ningún inicio
de sesión SSH: consulta [Editar un servidor alquilado o alojado con panel](../guides/edit-a-rented-server.md).

El editor también recuerda la identidad del servidor en la primera conexión. Si alguna vez cambia, el editor se niega a
conectarse, porque alguien podría estar haciéndose pasar por tu servidor.

El plugin solo cambia un mundo que aloja su juego. En un juego que se ha unido al servidor de otra persona se niega,
porque ese juego solo conoce los objetos que rodean a su propio jugador. Usa el plugin de la misma versión que tu
editor.

## Qué se envía y cuándo

**Cuando te conectas**, el editor lee el mundo entero desde el juego: cada objeto, las zonas que ha creado el juego, las
ubicaciones. Son los mismos datos que contiene un guardado, así que el mapa, la búsqueda y todas las herramientas
funcionan como sin conexión. En un mundo grande esto tarda un rato, hasta unos minutos. También pide los objetos que
conoce el juego y que no son propios de Valheim (piezas de mods), para que tengan su nombre y aparezcan en la lista de
Colocar y en el panel Construir.

**Cuando aplicas** (**Aplicar en vivo**, o cada paso al instante con **Auto**), envía lo que ha cambiado desde la última
aplicación, en este orden:

1. **El suelo** de cada zona que cambiaste, incluido el suelo Sin límite (consulta [Sin límite](ground-limits.md#sin-límite)).
2. **Los objetos** que eliminaste, colocaste, pegaste, reemplazaste, cambiaste o moviste.
3. **Los restablecimientos de zona**, al final. El juego vuelve a crear esas zonas, así que el editor vuelve a leer
   entonces el mundo; el historial le acompaña sin lo que hizo en esas zonas.

El juego comparte cada cambio con todos los jugadores conectados, como hace con lo que construyen los jugadores, y lo
guarda con el mundo. Los objetos colocados en vivo los crea el servidor, así que los jugadores los ven como algo recién
construido o plantado.

El plugin hace todo esto en el propio bucle del juego, unas pocas peticiones por fotograma, así que el juego nunca lee
ni cambia su mundo desde dos sitios a la vez. Un cambio grande (miles de objetos) puede hacer que el servidor dé un
tirón un momento. Si el juego no acepta una petición en 30 segundos, el plugin la descarta: no se hace nada de ella y
nada se marca como aplicado, así que vuelve a aplicar. Cuando lo que se pierde es la respuesta en el camino de vuelta
(un túnel caído), el editor no puede saber si se hizo; volver a enviarla es seguro, porque el plugin reconoce una
petición que ya hizo y no crea sus objetos dos veces. Solo se ejecuta una aplicación a la vez.

### Mover un objeto lo mantiene como el mismo objeto

Un objeto que mueves o giras se mueve en el juego con su propio id cuando no cambia nada más de él. Un mod que sigue
los objetos mientras el juego se ejecuta (el cartel con la etiqueta de un cofre, los cofres de un almacén, una guarda)
sigue reconociéndolo, y un cofre conserva su contenido tal como lo tiene el juego. Un objeto cuyos datos cambiaste
conserva su id de la misma manera.

Hay una pega. El juego de un jugador coloca un objeto fijo (un muro, un cofre) una sola vez, cuando entra en su
alcance, y nunca lo mueve después. Así que un movimiento solo conserva el id cuando ningún jugador tiene cargados a la
vez su lugar anterior y el nuevo (unos 160 m alrededor de cada jugador). Si no, el objeto se vuelve a crear en su nuevo
lugar y se quita del anterior, y el mensaje de la aplicación dice cuántos: para otros mods son objetos nuevos. Mueve
una construcción cuando no haya nadie cerca para conservar todo lo suyo. El grupo enlazado de un mod (un barco y sus
piezas) y las criaturas siempre se vuelven a crear.

## Seguir el juego

Mientras hay un área abierta, el editor pregunta al juego cada 2 segundos qué ha cambiado en las zonas de esa área.
Cuesta poco: el juego responde con un número por zona, que cambia cada vez que algo de la zona cambia, y solo se
vuelven a leer las zonas cuyo número ha cambiado. La barra de estado dice qué ha llegado («Desde el juego: 2 objeto(s)
nuevo(s), 1 quitado(s), el suelo de 1 zona(s).»); un simple cambio de datos, como un fuego que se consume, llega sin
decir nada.

- **Los objetos** que los jugadores construyen, quitan o cambian aparecen, desaparecen o cambian en el editor, sin un
  paso en el Historial. Un objeto cambiado sigue siendo el mismo objeto en el editor, así que la selección, el
  Inspector y su lugar en el Historial se quedan con él.
- **Tus propios objetos, una vez aplicados,** se muestran como los tiene realmente el juego: donde la física asentó una
  pieza, la salud y el punto de aparición que el juego dio a una criatura.
- **Las cosas que se mueven o cambian solas** (criaturas, barcos, carros, objetos tirados, fuegos y fundiciones) se
  siguen a medida que avanzan, sin leer cada vez su zona entera.
- **El suelo** excavado, nivelado o pintado en el juego llega donde no tienes cambios propios en esa zona (más abajo se
  explica qué pasa cuando sí los tienes).

El seguimiento solo ocurre en el editor 3D, nunca durante un trazo de pincel ni mientras se aplica. El mapa muestra el
mundo tal como se leyó por última vez: **Recargar** lo vuelve a leer entero.

## Varios editores y jugadores

Los jugadores y el editor pueden cambiar el mismo lugar. El editor lo resuelve así:

| Qué pasa | Qué hace el editor |
|---|---|
| Los jugadores cambian el suelo de una zona donde tienes cambios aún sin aplicar | Los tuyos se conservan y la barra de estado lo dice. **Aplicar en vivo** pregunta entonces antes (**Aplicar sobre los cambios del juego**): aplicar escribe tu suelo sobre esa zona, y lo que los jugadores le hayan hecho desde entonces puede perderse. **Ahora no** deja tus cambios pendientes. |
| Los jugadores recogen o destruyen un objeto | Desaparece para siempre: deshacer y rehacer lo dejan en paz, y una eliminación suya que no habías aplicado se descarta. Eso incluye los objetos que colocaste tú. |
| Eliminas un objeto y lo aplicas | Sigue eliminado; deshacer lo devuelve como una copia (consulta [Aplicar en vivo](saving-and-history.md#aplicar-en-vivo)). |
| Mueves una construcción mientras hay jugadores cerca | Se vuelve a crear en lugar de moverse (consulta [Mover un objeto lo mantiene como el mismo objeto](#mover-un-objeto-lo-mantiene-como-el-mismo-objeto)). |
| Cambias un objeto en el Inspector mientras el juego también lo cambia | El Inspector no aplica sobre la versión más reciente: la muestra, para que la vuelvas a cambiar. |
| Restableces una zona cerca de la que hay un jugador | El juego la vuelve a crear al instante; si no, la próxima vez que llegue alguien. |

El plugin responde a cualquier editor que tenga el token, y no distingue los cambios de un editor de los de un jugador:
lo que aplica otro editor llega al tuyo igual que el cambio de un jugador.

**Auto y Claude.** Con **Auto** activado, cada paso llega a los jugadores en el momento en que lo terminas, y un trazo
de pincel largo va como una sola aplicación. Por eso Claude no cambia nada en un mundo en vivo mientras Auto está
activado (consulta [Scripts y Claude](safety.md#scripts-y-claude)).
