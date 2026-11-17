<!-- source: 94a46b5b -->
# Editar un servidor alquilado o alojado con panel

Edita el mundo de un servidor que alquilas a un proveedor de juegos, o de uno que un panel de juegos como Pterodactyl
o Pelican ejecuta en un contenedor en tu propia máquina.

**Editar en vivo necesita acceso SSH a la máquina que ejecuta el servidor**: un inicio de sesión en su terminal, no
solo SFTP o un gestor de archivos. El editor llega al plugin a través de SSH, en el propio `127.0.0.1` del servidor
(consulta [Editar un servidor dedicado mientras la gente juega](edit-a-server-live.md)). Antes de alquilar un servidor para
editarlo en vivo, pregunta al proveedor si el plan incluye acceso SSH. Hay dos tipos de servidores que no encajan con
eso:

- **Un servidor de juego alquilado a un proveedor** (tienes un panel web y un gestor de archivos o FTP/SFTP, pero no
  acceso SSH a la máquina): el editor no puede llegar al plugin ahí. Edita en su lugar los archivos del mundo: ver
  [Un servidor de juego alquilado](#un-servidor-de-juego-alquilado).
- **Un panel de juegos en tu propia máquina** (Pterodactyl, Pelican o cualquier servidor que se ejecute en un
  contenedor de Docker): editar en vivo funciona, a través de un túnel que abres tú. Consulta
  [Un servidor en un contenedor](#un-servidor-en-un-contenedor).

Si tu proveedor te da un inicio de sesión SSH real en la máquina que ejecuta el juego (un VPS o una máquina dedicada
alquilados, no solo un servidor de juego), sigue en su lugar
[Editar un servidor dedicado mientras la gente juega](edit-a-server-live.md).

## Un servidor de juego alquilado

**Necesitas:** el panel del proveedor o acceso a los archivos (su gestor de archivos, FTP o SFTP) y permiso para
detener y arrancar el servidor.

El modo en vivo no es posible: el plugin solo responde en la propia máquina, y el proveedor no da forma de entrar. El
inicio de sesión SFTP de un panel (a menudo en el puerto 2022) solo entrega archivos: el SSH del editor no puede abrir
un túnel a través de él. Edita en su lugar el mundo guardado:

1. En el panel del proveedor, **detén** el servidor. Un servidor en marcha guarda encima de los archivos que vuelvas a
   subir.
2. Con el gestor de archivos del panel o un programa SFTP (FileZilla, WinSCP), descarga la carpeta del mundo,
   `worlds_local/<World>` en la carpeta de guardado del servidor. Guarda una copia sin tocar: es tu copia de
   seguridad.
3. En el editor, abre esa carpeta: **Un mundo guardado** → **Otra carpeta de mundos** → **Examinar…** (ver
   [Abrir un mundo que no está en la lista](open-a-world.md)).
4. Haz tus cambios y pulsa **Guardar en el mundo**.
5. Vuelve a subir la carpeta al mismo sitio, reemplazando los archivos que haya, y luego arranca el servidor.

El servidor debe seguir detenido desde el paso 1 hasta el paso 5: lo que hagan los jugadores entre medias se pierde al
subir.

## Un servidor en un contenedor

**Necesitas:** un inicio de sesión SSH en la máquina donde se ejecuta el panel (tu VPS, como root o como un usuario
con permiso para ejecutar `docker`), y permiso para editar los archivos del servidor de juego en el panel.

Un panel ejecuta cada servidor de juego en su propio contenedor de Docker, que tiene su propio `127.0.0.1`. El plugin
escucha ahí, dentro del contenedor, mientras que el túnel propio del editor llega al `127.0.0.1` de la máquina, donde
no responde nada: el editor dice que el plugin no responde, aunque el servidor se ejecuta con él. La forma de entrar es
un túnel a la dirección propia del contenedor:

```text
 your computer               your VPS          the server's container
┌───────────────────────┐   ┌───────────┐     ┌───────────────────────┐
│ editor                │SSH│           │     │ WorldEditorBridge     │
│ 127.0.0.1:15182 ══════╪═══╪═► sshd ───┼────►│ 172.18.0.x:5182       │
└───────────────────────┘   └───────────┘     └───────────────────────┘
```

1. En el panel, instala BepInEx y WorldEditorBridge en el servidor, como para cualquier servidor dedicado (pasos 1 a 3
   de [Editar un servidor dedicado mientras la gente juega](edit-a-server-live.md)), y arráncalo una vez.
2. En el gestor de archivos del panel, abre `BepInEx/config/Tie.WorldEditorBridge.cfg`. Pon `BindAddress = +` y copia
   el valor de la línea `Token`.
3. Reinicia el servidor. Su consola o `BepInEx/LogOutput.log` dice `WorldEditorBridge <version> listening on
   http://+:5182/` y luego un aviso de que el token viaja sin cifrar: dentro del contenedor, eso es solo la red propia
   del panel.
4. Inicia sesión en el VPS por SSH y busca la dirección del contenedor. El contenedor lleva el nombre del id del
   servidor (el panel lo muestra en los ajustes del servidor); `docker ps` los lista todos:

   ```sh
   docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' <server id>
   ```

   Responde con una dirección como `172.18.0.5`.
5. En tu equipo, abre un túnel hasta ella y déjalo abierto mientras editas (reemplaza la dirección y el inicio de
   sesión):

   ```sh
   ssh -N -L 15182:172.18.0.5:5182 user@your-vps
   ```

   Con PuTTY: **Connection** → **SSH** → **Tunnels**, **Source port** `15182`, **Destination** `172.18.0.5:5182`,
   **Add**, y luego abre la sesión.
6. En la página de inicio del editor: **Un servidor dedicado** → **Más opciones** → **Tengo mi propio túnel**. Escribe
   `127.0.0.1:15182` como **Dirección del puente**, pega el token y pulsa **Conectar**.

Después edita como en cualquier servidor: ver [Modo en vivo](../live-mode.md#úsalo).

**Ten en cuenta:**

- `+`, no la dirección del contenedor ni `0.0.0.0`: con la dirección del contenedor el plugin rechaza todas las
  peticiones que llegan por un túnel, y con `0.0.0.0` todas las peticiones («The server answered with an error
  (400)», el servidor respondió con un error).
- No añadas el puerto del plugin (5182) a los puertos del servidor (asignaciones) en el panel: eso lo abre a internet.
- La dirección del contenedor puede cambiar cuando el servidor se reconstruye o se reinstala. Si el túnel deja de
  responder, vuelve a buscarla (paso 4).
- Deja `BindAddress = 127.0.0.1` en un servidor que no se ejecuta en un contenedor: ahí, `+` hace que el plugin escuche
  en todas las redes en las que está la máquina (las conexiones desde internet se siguen rechazando).

## Comprueba que ha funcionado

- **Un servidor de juego alquilado:** el servidor arranca, y tus cambios están en el juego.
- **Un servidor en un contenedor:** la barra superior muestra **EN VIVO** y, después de **Aplicar en vivo**, «Todo
  aplicado».

Si en su lugar el editor muestra un mensaje, ver [Un servidor no se conecta](../help/troubleshooting.md#un-servidor-no-se-conecta).

## Deshacerlo

- **Un servidor de juego alquilado:** detén el servidor y sube la copia que guardaste en el paso 2.
- **Un servidor en un contenedor:** `Ctrl+Z` y luego **Aplicar en vivo** otra vez. Para volver a cerrar el plugin, pon
  `BindAddress = 127.0.0.1` y reinicia el servidor.

## Relacionado

- [Editar un servidor dedicado mientras la gente juega](edit-a-server-live.md)
- [Ajustes del plugin](../live-mode.md#configuración-del-plugin): `BindAddress`, `Port`, `Token`
- [Abrir un mundo que no está en la lista](open-a-world.md)
- [Hacer una copia de seguridad de un mundo antes de editarlo](back-up-a-world.md)
