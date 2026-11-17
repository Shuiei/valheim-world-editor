<!-- source: 4a8a11bc -->
# Hacer una copia de seguridad de un mundo antes de editarlo

Haz una copia de la carpeta de un mundo a la que siempre puedas volver, pase lo que pase después con el mundo.

**Necesitas:** el nombre del mundo, y Valheim cerrado (o detenido el servidor que ejecuta el mundo).

1. Cierra Valheim, o detén el servidor. Un juego en marcha sigue escribiendo el mundo, así que una copia hecha
   mientras se ejecuta puede quedar mitad vieja, mitad nueva.
2. Busca la carpeta del mundo: en la página de inicio del editor, haz clic en **Un mundo guardado** y pasa el cursor
   por la tarjeta del mundo. La descripción emergente termina con la carpeta, por ejemplo
   `~/.config/unity3d/IronGate/Valheim/worlds_local/Midgard`.
3. Abre en tu gestor de archivos la carpeta que la contiene (`worlds_local`). Los sitios habituales son:
   - Windows: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local`
   - Linux: `~/.config/unity3d/IronGate/Valheim/worlds_local` (Valheim con Proton:
     `<Steam library>/steamapps/compatdata/892970/pfx/drive_c/users/steamuser/AppData/LocalLow/IronGate/Valheim/worlds_local`)
   - macOS: `~/Library/Application Support/IronGate/Valheim/worlds_local`
   - Un servidor dedicado: `<savedir>/worlds_local` en el servidor.
4. Copia la carpeta del mundo (`Midgard`) y pega la copia en la misma carpeta `worlds_local`.
5. Cambia el nombre de la copia a `<World>_backup_<date>`, por ejemplo `Midgard_backup_2026-10-22`. Mantén el nombre
   del mundo exactamente como está, seguido de `_backup_`.

> [!TIP]
> Con este nombre, la copia queda junto al mundo, y el editor la lista como copia de seguridad en **Volver a una copia
> de seguridad** de la herramienta Región, para que más adelante puedas recuperar un área desde ella: ver
> [Restaurar un área desde una copia de seguridad](restore-from-backup.md).

## Comprueba que ha funcionado

- La copia contiene los mismos archivos que el mundo: `_main.<n>.chunks`, `.db2`, `.fwl2`, `.ok` y los archivos
  `.chunk`.
- En la página de inicio, **Un mundo guardado** también lista la copia, con su propio nombre. Abre el propio mundo
  para editarlo, no la copia.
- En la vista 3D de un mundo, **Volver a una copia de seguridad** de la herramienta [Región](../area.md) lista la copia
  como **Editor · \<fecha\>**.

## Deshacerlo

La copia no cambia nada en el mundo: elimina la carpeta de la copia cuando ya no la necesites.

Para devolver todo el mundo a como estaba en la copia:

1. Cierra Valheim, o detén el servidor.
2. Saca la carpeta del mundo (`Midgard`) de `worlds_local`, por ejemplo a tu Escritorio (elimínala más tarde, cuando
   estés seguro).
3. Cambia el nombre de la copia (`Midgard_backup_2026-10-22`) al nombre del mundo (`Midgard`).
4. Arranca el juego: carga el mundo tal como estaba cuando lo copiaste.

El juego también guarda sus propias copias de seguridad junto al mundo (`<World>_backup_auto-<date>`); el editor las
lista como **Juego · \<fecha\>**.

## Relacionado

- [Tu primera edición](../start/first-edit.md)
- [Restaurar un área desde una copia de seguridad](restore-from-backup.md)
- [Deshacer un paso, retirar un cambio o descartarlo todo](undo-changes.md)
- [Copias de seguridad](../concepts/saving-and-history.md#copias-de-seguridad)
