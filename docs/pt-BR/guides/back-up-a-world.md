<!-- source: 4a8a11bc -->
# Fazer backup de um mundo antes de editar

Faça uma cópia da pasta de um mundo à qual você sempre possa voltar, aconteça o que acontecer com o mundo depois.

**Você precisa de:** o nome do mundo, e o Valheim fechado (ou o servidor que roda o mundo parado).

1. Feche o Valheim, ou pare o servidor. Um jogo em execução continua gravando o mundo, então uma cópia feita enquanto
   ele roda pode ficar metade velha, metade nova.
2. Encontre a pasta do mundo: na página inicial do editor, clique em **Um mundo salvo** e passe o cursor sobre o cartão
   do mundo. A dica termina com a pasta, por exemplo `~/.config/unity3d/IronGate/Valheim/worlds_local/Midgard`.
3. Abra a pasta que a contém (`worlds_local`) no seu gerenciador de arquivos. Os lugares usuais são:
   - Windows: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local`
   - Linux: `~/.config/unity3d/IronGate/Valheim/worlds_local` (Valheim pelo Proton:
     `<Steam library>/steamapps/compatdata/892970/pfx/drive_c/users/steamuser/AppData/LocalLow/IronGate/Valheim/worlds_local`)
   - macOS: `~/Library/Application Support/IronGate/Valheim/worlds_local`
   - Um servidor dedicado: `<savedir>/worlds_local` no servidor.
4. Copie a pasta do mundo (`Midgard`) e cole a cópia na mesma pasta `worlds_local`.
5. Renomeie a cópia como `<World>_backup_<date>`, por exemplo `Midgard_backup_2026-10-22`. Mantenha o nome do mundo
   exatamente como está, seguido de `_backup_`.

> [!TIP]
> Com esse nome, a cópia fica ao lado do mundo, e o editor a lista como backup em **Voltar a um backup** da ferramenta
> Região, para que você possa trazer de volta uma área a partir dela mais tarde: veja
> [Restaurar uma área a partir de um backup](restore-from-backup.md).

## Verificar se funcionou

- A cópia contém os mesmos arquivos que o mundo: `_main.<n>.chunks`, `.db2`, `.fwl2`, `.ok` e os arquivos `.chunk`.
- Na página inicial, **Um mundo salvo** também lista a cópia, com o próprio nome. Abra o próprio mundo para editar, não
  a cópia.
- Na visão 3D de um mundo, **Voltar a um backup** da ferramenta [Região](../area.md) lista a cópia como
  **Editor · \<data\>**.

## Desfazer

A cópia não altera nada no mundo: exclua a pasta da cópia quando não precisar mais dela.

Para devolver o mundo inteiro ao que era na cópia:

1. Feche o Valheim, ou pare o servidor.
2. Tire a pasta do mundo (`Midgard`) de `worlds_local`, por exemplo para a sua Área de Trabalho (exclua-a depois,
   quando tiver certeza).
3. Renomeie a cópia (`Midgard_backup_2026-10-22`) com o nome do mundo (`Midgard`).
4. Inicie o jogo: ele carrega o mundo como estava quando você o copiou.

O jogo também guarda os próprios backups ao lado do mundo (`<World>_backup_auto-<date>`); o editor os lista como
**Jogo · \<data\>**.

## Relacionados

- [Sua primeira edição](../start/first-edit.md)
- [Restaurar uma área a partir de um backup](restore-from-backup.md)
- [Desfazer um passo, reverter uma alteração ou descartar tudo](undo-changes.md)
- [Backups](../concepts/saving-and-history.md#backups)
