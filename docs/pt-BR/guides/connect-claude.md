<!-- source: e9f26daf -->
# Deixar o Claude moldar ou construir

Conecte o Claude Code ou o Claude Desktop ao editor e depois peça ao Claude, com palavras simples, que molde o terreno,
plante ou construa.

**Você precisa de:** o editor e o [Claude Code](https://claude.com/claude-code) ou o Claude Desktop no mesmo
computador.

1. Na página inicial do editor, abra **Configurações** e encontre a seção **Claude**.
2. Ative **Permitir que o Claude se conecte**. Opcional: ative **O Claude pode executar scripts** para deixar o Claude
   escrever e executar scripts em C# (um script é executado com as suas permissões neste computador; deixe desligado
   se tiver dúvida).
3. Pressione **Salvar** (isso fecha Configurações) e depois abra **Configurações** de novo: a seção mostra "Listening on
   http://127.0.0.1:5731/mcp" (escutando nesse endereço).
4. Adicione o editor ao Claude, uma única vez:
   - **Claude Code:** pressione **Copiar** ao lado do comando, cole-o num terminal e execute-o.
   - **Claude Desktop:** pressione **Copiar** ao lado das linhas de configuração, adicione-as em Claude Desktop →
     Settings → Developer → **Edit Config** (`claude_desktop_config.json`) e reinicie o Claude Desktop.
5. No editor, abra a área de um mundo em 3D, ou a Oficina.
6. Peça ao Claude, por exemplo: "Aplane um quadrado de 40 m no meio da área, pavimente-o e coloque um anel de tochas
   ao redor."
7. Veja o Claude trabalhar na visão 3D. Cada passo dele aparece em **Histórico** como "Claude: …".
8. Mantenha o resultado com **Salvar no mundo**, **Aplicar ao vivo** ou **Salvar projeto**, ou desfaça-o (abaixo).

<!-- picture: claude-code-mcp.jpg: a terminal running Claude Code after typing /mcp, the list showing "valheim-editor" as connected, next to the editor window with a 3 × 3 area open -->

## Verificar se funcionou

- No Claude Code, `/mcp` lista **valheim-editor** como conectado.
- A primeira resposta do Claude descreve o que está aberto no editor.
- O contador de pendências da barra superior cresce com as alterações do Claude; nada é gravado até você salvar ou
  aplicar.

Se o Claude disser que não consegue alcançar o editor, veja
[O Claude não consegue se conectar](../help/troubleshooting.md#o-claude-não-consegue-se-conectar).

## Desfazer

- `Ctrl+Z` desfaz o último passo, seja do Claude ou seu.
- **Reverter as alterações do Claude**, no topo do Histórico, remove todos os passos que o Claude fez na área aberta e
  mantém os seus.
- **Descartar** abandona tudo o que não foi salvo.
- Para impedir que o Claude alcance o editor, desligue **Permitir que o Claude se conecte** e pressione **Salvar**.
  **Novo token** seguido de **Salvar** bloqueia qualquer cópia do comando antigo.

## Relacionados

- [Claude](../claude.md): cada configuração, o que o Claude pode fazer, segurança
- [Segurança](../concepts/safety.md#scripts-e-o-claude): o que os scripts e o Claude podem alcançar
- [Desfazer um passo, reverter uma alteração ou descartar tudo](undo-changes.md)
