<!-- source: 43410d17 -->
# Atajos

Todas las teclas y acciones del ratón del editor, agrupadas según lo que estés haciendo. El botón **?** de la barra
superior (o `F3`) muestra las principales mientras trabajas.

Las teclas solo funcionan en el editor 3D: no mientras escribes en un cuadro, ni en la página de inicio o el mapa del
mundo (salvo `F1`: funciona en todas partes). En un
Mac, pulsa `Cmd` donde esta página dice `Ctrl` (`Cmd+Z` deshace, `Cmd`+clic añade a una selección) y `Option` en lugar
de `Alt`.

## Cámara

| Tecla o ratón | Acción |
|---|---|
| Arrastrar con el botón derecho | Girar la vista. En Caminar y Volar: mirar alrededor. |
| Rueda | Acercar o alejar, de 3 m a 3 km del punto que miras. |
| Arrastrar con el botón central | Desplazar la vista. |
| `Space` + arrastrar con el botón izquierdo, `Shift` + arrastrar con el botón derecho | Desplazar la vista, en cualquier herramienta (útil sin botón central). |
| Arrastrar con el botón izquierdo (herramienta **Vista**) | Desplazar la vista. |
| `W` `A` `S` `D` o las flechas | Moverse. Cuanto más alejado estés, más rápido. No mientras mantienes pulsado `Ctrl` o `Alt`. |
| `F` | Caminar a la altura de los ojos, luego Volar y luego volver a la vista habitual, en todas las herramientas. |
| `Shift` (Caminar) | Correr: 12 m/s en lugar de 6. |
| `Space` / `C` (Volar) | Subir / bajar, también a través del suelo (para entrar en los túneles y sótanos de un mod). `Shift`: 45 m/s en lugar de 15. |

Al caminar o volar, la rueda no acerca y arrastrar no desplaza la vista. Consulta
[Caminar y Volar](../editor-basics.md#caminar-y-volar).

## Paneles y guardado

| Tecla | Acción |
|---|---|
| `V` | Abrir o cerrar el panel **Vista**. |
| `L` | Abrir o cerrar el **Historial**. |
| `?` (`Shift+/`) o `F3` | Abrir o cerrar la tarjeta de ayuda **Controles**. |
| `F1` | La ventana de Ayuda en la página de la herramienta en uso (consulta [La ventana de Ayuda](#la-ventana-de-ayuda)). El pequeño **?** de arriba de cada panel abre la página de ese panel. |
| `Ctrl+S` | **Guardar en el mundo** (un mundo guardado), **Aplicar en vivo** (en vivo), **Guardar plano** (el Taller). |

## Elegir una herramienta

| Tecla | Herramienta |
|---|---|
| `1` `2` `3` `4` `5` | Elevar, Bajar, Aplanar, Suavizar, Restaurar |
| `0` | Natural |
| `O` (la letra) | Erosionar |
| `6` `7` `8` `9` | El pincel **Pintar** con Tierra, Cultivar, Pavimento, Quitar pintura |
| `B` | Región |
| `P` | Camino |
| `T` | Colocar (**Construir** en el Taller) |
| `U` | Construir |
| `G` | Relieve |
| `E` | Selección |
| `M` | Medir |
| `H` o `Esc` | Vista (`Esc` solo cuando no tiene nada más que cancelar, ver [Cómo funciona Esc](#cómo-funciona-esc)) |

Las cifras del teclado numérico también funcionan. **Mazmorra**, **Script** y **Planos** no tienen
tecla: haz clic en ellos en la barra de herramientas. En el Taller, solo `U` o `T`, `E` y `H` o `Esc` eligen una
herramienta.

## Deshacer, copiar y pegar

| Tecla | Acción |
|---|---|
| `Ctrl+Z` | Deshacer el último cambio. |
| `Ctrl+Y` o `Ctrl+Shift+Z` | Rehacerlo. |
| `Ctrl+C` | Copiar el recuadro o el polígono de Región, o los objetos seleccionados (herramientas Región y Selección). |
| `Ctrl+X` | Cortar: la misma copia, y luego los objetos salen del mundo en un solo paso (el suelo se queda). |
| `Ctrl+V` | Empezar a pegar el portapapeles, desde cualquier herramienta. |
| `[` / `]` | Tamaño del pincel 1 m menor / mayor (de 1 a 30 m). |

## Seleccionar objetos

Funcionan en la herramienta **Selección** (`E`). Un clic en la herramienta **Vista** elige objetos de la misma forma.

| Tecla o ratón | Acción |
|---|---|
| Clic | Seleccionar solo ese objeto. Vuelve a hacer clic en el mismo sitio: el siguiente objeto que hay detrás. |
| `Ctrl`+clic | Añadir el objeto, o quitarlo si ya estaba seleccionado. |
| `Shift`+clic | Quitar el objeto de la selección. |
| `Ctrl+Shift`+clic | Añadir todos los objetos visibles de una fila, desde el último en el que hiciste clic hasta este. |
| Doble clic | Una pieza de construcción: toda la construcción. Una parte de un barco u otro grupo enlazado: todo el grupo. Cualquier otro objeto (un cofre, un cartel, un árbol): ese objeto, con su Inspector abierto. En la herramienta Vista, un doble clic sobre un objeto cambia a Selección. |
| Arrastrar sobre suelo vacío | Seleccionar lo que hay dentro del recuadro (o del contorno en el suelo, con **Arrastrar dibuja** ajustado así). `Ctrl`: añadir a la selección. `Shift`: quitar. |
| `Alt` + arrastrar | Lo mismo, empezando también sobre objetos (los objetos no se tocan). |
| `Ctrl+A` / `Alt+A` | Seleccionar todos los objetos visibles / borrar la selección. |
| `Ctrl+I` | Invertir la selección. |
| `Ctrl+H` / `Alt+H` | Ocultar en la vista los objetos seleccionados / volver a mostrarlos. |
| `Alt+Z` | Activar o desactivar **Seleccionar a través**: un recuadro también toma lo que está oculto detrás de otros objetos. |
| `Shift+F` o `.` del teclado numérico | Encuadrar la selección. |

## Por herramienta

### Pinceles de esculpir y pintar

| Tecla o ratón | Acción |
|---|---|
| Arrastrar con el botón izquierdo | Aplicar el pincel sobre el suelo. |
| `Alt`+clic | Tomar la altura del suelo de ahí como altura de Aplanar, y cambiar a **Aplanar**. |
| `Alt+Shift`+clic | Ajustar el rango de altura de la [Máscara](../masks.md) a 2 m por encima y por debajo de ese punto (también con la herramienta Camino). |

### Región

| Tecla o ratón | Acción |
|---|---|
| Arrastrar (Recuadro) | Dibujar el recuadro. |
| Clic (Polígono) | Añadir un punto. Doble clic: cerrar el polígono. |
| `Enter` | Cerrar el polígono que se está dibujando; con la selección hecha, hacer la acción elegida. |
| `Enter`, luego `Del` | Con **Objetos** elegido: seleccionar los objetos de dentro y luego eliminarlos en la herramienta Selección. |
| `Backspace` | Quitar el último punto del polígono. |
| `Esc` | Borrar la selección (el recuadro o el polígono). |

### Camino

| Tecla o ratón | Acción |
|---|---|
| Clic | Añadir un punto. |
| Arrastrar la línea | Añadir un punto ahí y moverlo. |
| Arrastrar un punto | Moverlo. |
| `Ctrl`+clic en un punto | Quitarlo. |
| `Alt`+clic | Tomar la altura del suelo de ahí como altura del camino (Rampa: su inicio). |
| `Ctrl+Alt`+clic | Rampa: tomar la altura como su fin. |
| `Alt+Shift`+clic | Ajustar el rango de altura de la [Máscara](../masks.md), como con un pincel. |
| `Enter` | Aplicar el camino. |
| `Backspace` | Quitar el último punto. |
| `Esc` | Borrar la línea. |

### Colocar

| Tecla o ratón | Acción |
|---|---|
| Arrastrar con el botón izquierdo (Pincel) | Colocar objetos bajo el pincel. `Shift` + arrastrar: quitarlos. |
| Clic (Línea, Polígono) | Añadir un punto. Arrastrar el contorno: añadir un punto ahí. Arrastrar un punto: moverlo. `Ctrl`+clic en un punto: quitarlo. |
| Doble clic en el último punto | Colocar a lo largo de la forma. |
| Arrastrar (Cuadrícula, formas de línea) | Dibujar el recuadro de la cuadrícula, o la forma de un extremo al otro. |
| `Alt`+clic | Tomar la altura de ahí como **Elevación**. |
| `Enter` | Colocar a lo largo de la línea, el círculo o el rectángulo, o en la cuadrícula o el polígono. |
| `Backspace` | Quitar el último punto de la línea o del polígono. |
| `Esc` | Borrar la forma que se está dibujando, o cancelar **Tomar del mundo**. |
| `N` | Una nueva disposición aleatoria. |
| `R` | Girar un cuarto de vuelta la vista previa, una cuadrícula dibujada o un polígono. |
| `,` / `.` o `Alt` + rueda | Girar 15° la vista previa, una cuadrícula dibujada o un polígono (`Shift`, más fino: 1°). |
| `PgUp` / `PgDn` o `Ctrl` + rueda | Subir o bajar 0.5 m lo que colocas (`Shift`, más fino: 0.1 m). |
| Clic en un objeto (**Tomar del mundo**) | Tomar su tipo. `Shift`+clic lo añade a los tipos elegidos. |

### Construir, Pegar y mover una selección: los pasos de la barra de construcción

[Construir](../build.md), Pegar y Selección giran y alzan con las mismas teclas, según los pasos fijados en
[la barra de construcción](../build.md#la-barra-de-construcción) (**Ángulo de giro**, 22.5° al principio; **Alzar**, 0.5 m al
principio). `Shift` siempre hace el paso más fino.

| Tecla o ratón | Acción |
|---|---|
| `,` / `.` o `Alt` + rueda | Girar la pieza, lo pegado o la selección según el paso **Ángulo de giro** (`Shift`: 1°). |
| `PgUp` / `PgDn` o `Ctrl` + rueda | Subirlo o bajarlo según el paso **Alzar** (`Shift`: una quinta parte). |

### Construir (mundos, el Taller y construir en una mazmorra)

| Tecla o ratón | Acción |
|---|---|
| Clic | Colocar la pieza (**Extender**: **Una pieza**), o añadir un punto (**Línea**). |
| Arrastrar | Una fila de la pieza (**Una pieza**); el círculo, el rectángulo o la cuadrícula (**Extender**). |
| Mantener pulsado `Alt` | Colocar la pieza de cuarto en cuarto de metro, sin encaje. |
| `R` | Girar la pieza un cuarto de vuelta. |
| `Q` / `E` | Con **Encaje** en **Manual**: el punto de encaje anterior / siguiente de la pieza. |
| `Shift` + clic derecho | Quitar la pieza bajo el puntero. |
| Clic central | Construir con el tipo de la pieza bajo el puntero. |
| `Enter` | Colocar la línea, el círculo, el rectángulo o la cuadrícula. |
| `Backspace` | Quitar el último punto de la línea. |
| `Esc` | Borrar la línea, la figura o la cuadrícula que se está dibujando. |

### Pegar

| Tecla o ratón | Acción |
|---|---|
| Clic | Colocar lo pegado. Vuelve a hacer clic para más copias. |
| `R` | Girar un cuarto de vuelta. |
| `X` | Reflejar. |
| `Esc` | Dejar de pegar y volver a la herramienta Región. |

### Mover la selección (Selección)

| Tecla o ratón | Acción |
|---|---|
| Arrastrar un objeto seleccionado | Mover la selección. |
| Arrastrar una flecha o el anillo | Mover a lo largo de ese eje, o girar. |
| `R` | Girar la selección un cuarto de vuelta. |
| `End` | Dejarla caer sobre el suelo. |
| `Del` | Eliminar los objetos seleccionados. |
| `I` | Abrir el Inspector del objeto seleccionado (con un solo objeto seleccionado). |
| `Esc` | Cancelar un movimiento en curso, o un recuadro o contorno que se está dibujando. |

### Otras herramientas

| Herramienta | Tecla o ratón | Acción |
|---|---|---|
| Relieve | Clic | Poner el relieve ahí: la forma de la fórmula, la montaña o la imagen. |
| Medir | Clic en dos puntos | Medir entre ellos. `Esc` borra la medición. |
| Mazmorra | `R` | Girar la sala que se va a añadir: se une por su siguiente abertura. |
| | `Del` | Eliminar la sala seleccionada. |
| Script | `Ctrl+Enter` | Ejecutar el script (en el cuadro de código). |
| Inspector | `Esc` | Cerrarlo. |
| La herramienta de un mod | Sus propias teclas | Una herramienta bajo **MODS** en la barra de herramientas tiene prioridad sobre las teclas que usa; las demás funcionan como siempre. |

## Cómo funciona Esc

`Esc` hace lo primero de esta lista que corresponda, y se detiene:

1. Cancela la elección de un tipo con **Tomar del mundo**.
2. Deja de pegar (vuelta a la herramienta Región).
3. Cierra el Inspector.
4. Cancela lo que está haciendo la herramienta: un movimiento en Selección, un recuadro o contorno que se está
   dibujando, una forma de Colocar o una línea extendida con Construir, un recuadro o polígono de Región, los puntos
   del Camino, una medición.
5. Cambia a la herramienta Vista.

## El mapa del mundo y un mundo nuevo

| Ratón | Acción |
|---|---|
| Arrastrar | Desplazar el mapa. |
| Rueda | Acercar hacia el ratón. |
| Clic | Elegir un sitio (la zona para **Editar en 3D**). |
| Doble clic (en el mapa de un mundo nuevo) | Mostrar el mundo entero. |
| `Enter` en un cuadro de búsqueda | Buscar. |

## La ventana de Ayuda

| Tecla | Acción |
|---|---|
| `F1` | Abrir la ayuda en lo que estás usando: la herramienta, el Inspector, el mapa, la página de inicio… (en cualquier página). |
| `Ctrl+F` | Buscar en la ayuda. |
| `Alt+Left` / `Alt+Right` | Atrás / adelante. |
| `Esc` | Cerrar una imagen ampliada; si no, vaciar la búsqueda. |
| `Enter` en el cuadro de búsqueda | Abrir la primera página encontrada. |

## Relacionado

- [La ventana del editor](../editor-basics.md): la barra superior, la barra de herramientas, la cámara y los paneles.
- [Selección](../select.md): seleccionar y mover objetos.
- [Colocar](../place.md), [Construir](../build.md), [Camino](../path.md), [Región, copiar y pegar](../area.md): lo que
  hacen las teclas de cada herramienta.
- [Glosario](glossary.md): las palabras que usa el editor.
