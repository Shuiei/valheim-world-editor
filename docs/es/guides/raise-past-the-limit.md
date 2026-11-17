<!-- source: c7695b6c -->
# Elevar o excavar más allá del límite de 8 m del juego

Levanta un acantilado o una aguja, o excava un cañón, mucho más allá de los 8 m que permite el juego, de forma que el
juego de cada jugador lo cargue sin ningún mod.

**Necesitas:** un mundo abierto en 3D ([una copia o una copia de seguridad de él](back-up-a-world.md)), con el juego
cerrado, o el [modo en vivo](edit-your-game-live.md).

1. Haz una copia de seguridad del mundo. Guardar este tipo de suelo borra el Historial del área.
2. En la barra de herramientas, haz clic en **Elevar** (`1`) o **Bajar** (`2`).
3. En la tarjeta **Máscara** junto a los ajustes del pincel, marca **Sin límite: más allá de los ±8 m del juego**. La
   barra de estado dice que las herramientas de suelo ahora van más allá de los ±8 m del juego.
4. Ajusta **Tamaño** e **Intensidad** y luego mantén pulsado el botón izquierdo sobre el suelo y muévelo. El suelo ya
   no se detiene en 8 m ni se pone rojo.
5. Opcional: da forma al resultado con **Suavizar** (`4`) o **Natural** (`0`), que siguen funcionando más allá del
   límite mientras Sin límite está activado.
6. Mira la forma desde varios lados. `Ctrl+Z` deshace un trazo; **Restaurar** (`5`) deshace parte de uno.
7. Haz clic en **Guardar en el mundo** (o **Aplicar en vivo**). El mensaje de guardado dice cuántos discos de suelo se
   crearon y cuántos árboles y rocas se quitaron, por ejemplo «Suelo sin límite: 312 disco(s) de suelo, 40
   árbol(es)/roca(s) retirado(s)».

Para una montaña entera con un solo clic, usa en su lugar [Relieve](../landform.md#montaña) con su tipo **Montaña**:
siempre va más allá del límite.

## Comprueba que ha funcionado

- Pasa el cursor por el suelo nuevo: la altura «original» de la barra de estado es ahora el suelo nuevo.
- Abre el mundo en el juego y ve allí. La forma aparece al acercarte, como lo hace el suelo nivelado de una aldea.

## Deshacerlo

- **Antes de guardar:** `Ctrl+Z`, el pincel **Restaurar** o **Descartar** para todo lo no guardado.
- **Después de guardar:** el Historial empieza de nuevo desde el mundo guardado. El pincel **Restaurar** sigue
  devolviendo el sitio al suelo que generó el mundo (igual que **Restaurar** de la herramienta Camino y **Volver al
  suelo generado** de Región): vuelve a guardar, y los discos de suelo que ya no necesita desaparecen. O devuelve el
  sitio a como estaba con [Restaurar un área desde una copia de seguridad](restore-from-backup.md).

## Relacionado

- [Sin límite](../concepts/ground-limits.md#sin-límite): cómo se guarda el suelo más allá de 8 m
- [Esculpir y pintar](../terrain.md#sin-límite)
- [Relieve](../landform.md)
