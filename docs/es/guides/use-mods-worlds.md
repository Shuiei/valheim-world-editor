<!-- source: 1000c3db -->
# Editar un mundo creado con Expand World, Riverheim o Tunnelheim

Instala el complemento del editor para tu mod, para que un mundo creado con él se abra, se dibuje y se guarde tal
como lo hace el mod.

**Necesitas:** el mod instalado en una carpeta de BepInEx de este equipo (la de tu Valheim, un perfil de un gestor de
mods o una copia de la carpeta `BepInEx` de un servidor), y el complemento de la misma versión del editor.

| Complemento | Para | Qué añade |
|---|---|---|
| [Expand World](../../../addons/ExpandWorld/README.md) | Expand World Size, Rivers y Data | Suelo, mapa, biomas, agua, ubicaciones, vegetación y previsiones al crear un mundo nuevo, tal como los crean los mods |
| [Railroads](../../../addons/Railroads/README.md) | Railroads (`Railroads.dll`) | Dibuja las vías, estaciones y trenes del mod con sus propios modelos; las piezas que colocas, pegas, mueves o giras funcionan en el juego, también en vivo |
| [Riverheim](../../../addons/Riverheim/README.md) | Riverheim (`Riverheim.dll`) | Abre los mundos de Riverheim (se rechazan sin él), dibujados con tu propia copia del mod; Riverheim como generador al crear un mundo nuevo |
| [Tunnelheim](../../../addons/Tunnelheim/README.md) | Tunnelheim (`ValheimTunneling.dll`) | Dibuja los túneles del mod y añade una herramienta **Tunnel** (excavaciones, Add Earth, perforaciones, salas, pozos, escaleras); los túneles van con las copias y los planos |
| [ValheimRAFT](../../../addons/ValheimRAFT/README.md) | ValheimRAFT (`ValheimRAFT.dll`) | Dibuja enteros los barcos y vehículos terrestres del mod, con sus velas y cuerdas; cada uno se mueve, se copia y se elimina como un todo |

1. Cierra el editor.
2. Desde la [página de versiones](https://github.com/Shuiei/valheim-world-editor/releases/latest) del editor (o
   Nexus Mods), descarga `ValheimWorldEditor-addon-<Name>-<version>.zip`.
3. Descomprímelo en la carpeta `addons` junto al editor (en macOS, junto a `ValheimWorldEditor.app`), de modo que
   tengas `addons/<Name>/worldeditor.json`.
4. Inicia el editor y abre **Configuración** en la página de inicio.
5. En **Complementos**, busca el complemento. Debería decir «En ejecución.» (o «Aún no leído» hasta que se abra un
   mundo). Si dice «Esperando a su mod: … no está en las carpetas de plugins de arriba.», añade en **Carpetas de
   BepInEx** la carpeta de BepInEx que contiene el mod y pulsa **Guardar**.
6. Para el mundo de un servidor abierto desde sus archivos, añade en **Carpetas de BepInEx** una copia de la carpeta
   `BepInEx` del servidor, para que el editor lea la configuración de los mods del servidor.
7. Abre el mundo en **Un mundo guardado**. Su tarjeta muestra el mod (por ejemplo **Riverheim** o «Expand World»).
8. Edita y guarda como siempre. Con Tunnelheim, elige **Tunnel** en **MODS** en la barra de herramientas, elige una
   acción y haz clic en el suelo o en la roca.

## Comprueba que ha funcionado

- El mundo se abre, y su suelo y su mapa se ven como en el juego (los ríos de Riverheim, el tamaño y los biomas de
  Expand World).
- El registro del editor (**Abrir registro** en la página de inicio) dice qué encontró y leyó el complemento.
- Una tarjeta de mundo que sigue en gris dice por qué: consulta [Un mundo no se abre o aparece en gris](../help/troubleshooting.md#un-mundo-no-se-abre-o-aparece-en-gris).

## Deshacerlo

Cada edición se deshace como cualquier otra (`Ctrl+Z`, Historial). Para dejar de usar un complemento, desactiva su
interruptor en **Configuración → Complementos** (su carpeta se queda), o elimina su carpeta de `addons`. Con él
desactivado, los mundos de Expand World se generan como lo hace el juego sin los mods, y los mundos de Riverheim se
rechazan.

## Conviene saber

- Un complemento solo se usa mientras la DLL de su mod está en una carpeta de plugins que el editor conoce; no cuentan
  un mod que un gestor de mods desactivó ni la configuración que dejó un mod eliminado.
- Las piezas de Railroads se dibujan a partir de los archivos de modelo que el mod guarda en su carpeta `meshes`:
  mantén esa carpeta junto a `Railroads.dll`, como la instala el mod.
- Los archivos de datos de Expand World necesitan la biblioteca YamlDotNet que el mod instala en `BepInEx/plugins`;
  sin ella no se leen, y el registro lo dice.
- Tu Riverheim debe conocer el preajuste del mundo (actualízalo si es más antiguo que el que creó el mundo). Riverheim
  1.3 y posteriores necesitan Worldhalla a su lado, como en el juego. Los mundos creados con la beta obsoleta de
  Riverheim no se pueden abrir. En vivo, tu Riverheim debe ser la versión que necesitan los jugadores que se unen a la
  partida (la misma versión desde la 1.3, la misma publicación antes).
- Un mundo de Riverheim necesita Riverheim en el juego de cada jugador, así que los jugadores de consola en juego
  cruzado no pueden unirse.
- En vivo, los archivos de Expand World se leen del propio juego a través del plugin: consulta [Modo en
  vivo](../live-mode.md).

## Relacionado

- [Un mundo nuevo](../new-world.md): Riverheim y Expand World al crear un mundo
- [Modo en vivo](../live-mode.md): juegos con mods, en vivo
- [Abrir un mundo que no está en la lista](open-a-world.md)
