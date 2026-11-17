<!-- source: e9f26daf -->
# Dejar que Claude modele o construya

Conecta Claude Code o Claude Desktop al editor y pídele a Claude con tus propias palabras que modele el suelo, plante
o construya.

**Necesitas:** el editor, y [Claude Code](https://claude.com/claude-code) o Claude Desktop en el mismo equipo.

1. En la página de inicio del editor, abre **Configuración** y busca la sección **Claude**.
2. Activa **Permitir que Claude se conecte**. Opcional: activa **Claude puede ejecutar scripts** para que Claude
   escriba y ejecute scripts en C# (un script se ejecuta con tus permisos en este equipo; déjalo desactivado si no
   estás seguro).
3. Pulsa **Guardar** (cierra Configuración) y vuelve a abrir **Configuración**: la sección muestra «Listening on
   http://127.0.0.1:5731/mcp» (a la escucha en esa dirección).
4. Añade el editor a Claude, una sola vez:
   - **Claude Code:** pulsa **Copiar** junto al comando, pégalo en un terminal y ejecútalo.
   - **Claude Desktop:** pulsa **Copiar** junto a las líneas de configuración, añádelas en Claude Desktop → Settings →
     Developer → **Edit Config** (`claude_desktop_config.json`) y reinicia Claude Desktop.
5. En el editor, abre en 3D el área de un mundo, o el Taller.
6. Pídeselo a Claude, por ejemplo: «Aplana un cuadrado de 40 m en el centro del área, paviméntalo y pon un anillo de
   antorchas alrededor».
7. Mira cómo trabaja Claude en la vista 3D. Cada uno de sus pasos aparece en **Historial** como «Claude: …».
8. Consérvalo con **Guardar en el mundo**, **Aplicar en vivo** o **Guardar plano**, o deshazlo (abajo).

<!-- picture: claude-code-mcp.jpg: a terminal running Claude Code after typing /mcp, the list showing "valheim-editor" as connected, next to the editor window with a 3 × 3 area open -->

## Comprueba que ha funcionado

- En Claude Code, `/mcp` muestra **valheim-editor** como conectado.
- La primera respuesta de Claude describe lo que está abierto en el editor.
- El recuento de cambios pendientes de la barra superior crece con los cambios de Claude; no se escribe nada hasta que
  guardas o aplicas.

Si Claude dice que no puede llegar al editor, consulta [Claude no se puede conectar](../help/troubleshooting.md#claude-no-se-puede-conectar).

## Deshacerlo

- `Ctrl+Z` deshace el último paso, sea de Claude o tuyo.
- **Retirar los cambios de Claude**, arriba del todo en Historial, quita todos los pasos que Claude hizo en el área
  abierta y conserva los tuyos.
- **Descartar** desecha todo lo que no está guardado.
- Para que Claude deje de llegar al editor, desactiva **Permitir que Claude se conecte** y pulsa **Guardar**.
  **Nuevo token** y luego **Guardar** deja fuera cualquier copia del comando antiguo.

## Relacionado

- [Claude](../claude.md): cada ajuste, lo que Claude puede hacer, la seguridad
- [Seguridad](../concepts/safety.md#scripts-y-claude): a qué pueden llegar los scripts y Claude
- [Deshacer un paso, retirar un cambio o descartarlo todo](undo-changes.md)
