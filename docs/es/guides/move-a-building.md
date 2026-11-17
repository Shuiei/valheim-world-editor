<!-- source: 9e216b4e -->
# Mover o copiar un edificio

Mueve una casa, con todo lo que hay dentro, unos metros o al otro lado del mundo, o pon una copia de ella en otro
sitio.

**Necesitas:** un mundo abierto en el editor, con el área alrededor del edificio abierta (antes, una copia de
seguridad: consulta [Hacer una copia de seguridad de un mundo](back-up-a-world.md)).

## Pasos

1. Pulsa `E` para abrir [Selección](../select.md).
2. Haz doble clic en un muro del edificio: quedan seleccionadas todas las piezas conectadas, también los cofres y los
   muebles de sus suelos.
3. Opcional: para añadir objetos sueltos del interior (una mesa apartada), haz `Ctrl` + clic en ellos.
4. Para un movimiento corto, arrastra una pieza seleccionada, o arrastra una flecha para moverlo a lo largo de un eje;
   arrastra el anillo naranja para girarlo. Deja activado **Poner cada objeto sobre el suelo al moverlo**, para que el
   edificio se asiente en el suelo donde quede.
5. Para un movimiento largo, pulsa `Ctrl+X` (o `Ctrl+C` para una copia). El edificio está ahora en el portapapeles.
6. Abre el área en el nuevo lugar (desde el [Mapa](../map.md)), o quédate en esta.
7. Pulsa `Ctrl+V`: el edificio sigue al cursor, transparente.
8. Gíralo con `R` (un cuarto de vuelta) o `,` `.` (según **Ángulo de giro** de la barra de construcción) y luego haz
   clic donde debe quedar.
9. Pulsa `Esc` para dejar de pegar.
10. Haz clic en **Guardar en el mundo** (o **Aplicar en vivo**).

Las copias y las piezas movidas conservan todo lo que contienen: el contenido de los cofres, el texto de los carteles,
quién las construyó. Un edificio pegado con `Ctrl+V` se lleva consigo el suelo que tiene debajo.

## Comprueba que ha funcionado

La barra de estado dice cuántos objetos se movieron o se pegaron. Rodea el edificio y míralo desde varios lados: nada
debería flotar ni quedarse atrás. Después de guardar, abre el mundo en el juego y entra.

## Deshacerlo

- Antes de guardar: `Ctrl+Z` deshace el movimiento, el corte o el pegado (un corte y su pegado son pasos distintos).
- Después de guardar: consulta [Deshacer un paso, retirar un cambio o descartarlo todo](undo-changes.md), o
  [Restaurar un área desde una copia de seguridad](restore-from-backup.md).

## Consejos

- Lo que pegas debe caber dentro del área abierta: si dice *No se ha pegado*, abre un área más grande alrededor del
  lugar.
- El portapapeles se conserva: puedes pegar en otra área o en otro mundo.
- Para mover un número exacto de metros, usa **POSICIÓN EXACTA** → **Desplazar** en el panel Selección.
- Un edificio arrastrado se fija a las piezas que estén a menos de 0.75 m mientras **Encaje** está en **Auto** en
  [la barra de construcción](../build.md#la-barra-de-construcción). Elige **Libre** para dejarlo exactamente donde lo
  arrastras.
- Para un barco u otro vehículo de un mod, selecciona cualquier parte: todo el grupo se mueve.
- En el modo en vivo, un edificio movido cuando no hay ningún jugador cerca sigue siendo el mismo objeto en el juego,
  así que los mods que lo siguen no lo pierden: consulta
  [Mover un objeto lo mantiene como el mismo objeto](../concepts/live-editing.md#mover-un-objeto-lo-mantiene-como-el-mismo-objeto).

## Relacionado

- [Selección](../select.md) · [Región](../area.md)
- [Encaje](../concepts/objects.md#encaje)
- [Pegar un plano en un mundo](paste-a-blueprint.md)
