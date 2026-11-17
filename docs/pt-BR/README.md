<!-- source: d6008f75 -->
# Ajuda do Valheim World Editor

O Valheim World Editor abre um mundo do Valheim, salvo ou em execução, numa janela 3D. Molde o terreno, construa,
coloque e mova qualquer coisa, depois salve de volta ou veja tudo mudar no jogo enquanto você trabalha.

**Primeira vez aqui?** [Instalar o editor](start/install.md) e depois siga [sua primeira edição](start/first-edit.md):
dez minutos numa cópia de um mundo, sem nada a perder. Procurando algo? Busque na ajuda (`Ctrl+F` na janela de ajuda)
ou comece por uma tarefa abaixo. No editor, `F1` ou o pequeno **?** no alto de um painel abre a ajuda dessa ferramenta.

> [!IMPORTANT]
> Guarde uma cópia de um mundo antes de alterá-lo: veja [Fazer backup de um mundo antes de editar](guides/back-up-a-world.md).
> O editor confere que cada salvamento é relido corretamente antes de descartar o anterior, mas não guarda nenhuma cópia
> por conta própria.

## Primeiros passos

- [Instalar o editor](start/install.md): download, primeira execução no Windows, Linux ou macOS, e o plugin para editar
  ao vivo.
- [Sua primeira edição](start/first-edit.md): abra a cópia de um mundo, erga um morro, plante árvores, salve e veja no
  jogo.
- [Sua primeira construção](start/first-build.md): construa uma cabana na Oficina, salve-a como projeto e cole-a num
  mundo.

## Editar mundos

- [Fazer backup de um mundo antes de editar](guides/back-up-a-world.md)
- [Abrir um mundo que não está na lista](guides/open-a-world.md)
- [Editar o mundo em que você está jogando](guides/edit-your-game-live.md)
- [Editar um servidor dedicado enquanto as pessoas jogam](guides/edit-a-server-live.md)
- [Editar um servidor alugado ou hospedado num painel](guides/edit-a-rented-server.md)
- [Desfazer um passo, reverter uma alteração ou descartar tudo](guides/undo-changes.md)
- [Restaurar uma área a partir de um backup](guides/restore-from-backup.md)
- [Gerar zonas de novo](guides/reset-zones.md)
- [Encontrar um objeto, um item de baú ou uma placa](guides/find-things.md)
- [Editar um mundo criado com Expand World, Riverheim ou Tunnelheim](guides/use-mods-worlds.md)

## Moldar o terreno

- [Elevar ou cavar além do limite de 8 m do jogo](guides/raise-past-the-limit.md)
- [Fazer uma estrada ou uma rampa subindo uma colina](guides/make-a-road.md)
- [Cavar um rio ou um lago](guides/make-a-river.md)
- [Retirar árvores e rochas, aplanar um terreno para construir](guides/clear-an-area.md)

## Construir e preencher

- [Plantar uma floresta, uma plantação ou uma sebe](guides/plant-a-forest.md)
- [Mover ou copiar uma construção](guides/move-a-building.md)
- [Alterar o que um baú guarda ou o que uma placa diz](guides/fill-a-chest.md)
- [Colar um projeto num mundo](guides/paste-a-blueprint.md)
- [Gerar uma masmorra e colocá-la no mundo](guides/make-a-dungeon.md)
- [Encontrar uma semente com o que você quer perto do início](guides/find-a-seed.md)
- [Deixar o Claude moldar ou construir](guides/connect-claude.md)

## Ferramentas

- [A janela do editor](editor-basics.md): a página inicial, a barra superior, a barra de ferramentas, a câmera, a área,
  o salvamento, o Histórico, o painel Visão e Recortar.
- [O mapa do mundo](map.md): encontre um lugar, abra-o em 3D, busque no mundo inteiro.
- [Esculpir e pintar](terrain.md): os pincéis de terreno, os carimbos e Sem limite.
- [Relevo](landform.md): um relevo inteiro num clique, a partir de uma fórmula sua (morros, crateras, planaltos,
  fossos), do gerador de montanhas (picos, cristas e vulcões além do limite do jogo) ou de uma imagem.
- [Caminho](path.md): estradas, rampas e rios ao longo de uma linha.
- [Máscara](masks.md): restrinja qualquer ferramenta a um bioma, uma altura, um declive ou uma pintura; o cartão dela
  também tem o interruptor Sem limite.
- [Medir e sobreposições](measure.md): distâncias, declives, cores de declive e curvas de nível.
- [Região, copiar e colar](area.md): aplane ou pinte um retângulo ou polígono, escolha os objetos dele, copie-o e
  cole-o, regenere-o ou restaure-o a partir de um backup.
- [Colocar](place.md): árvores, rochas, plantações e pontos de surgimento.
- [Construir](build.md): as peças que os jogadores constroem, uma por vez ou ao longo de linhas, com a barra de
  construção.
- [Selecionar](select.md): escolha, mova, gire, copie, substitua e exclua objetos, e veja o que há dentro deles.
- [A Oficina](workshop.md): construa num lote vazio e guarde o resultado como projeto.
- [Masmorras](dungeons.md): gere uma masmorra ou edite as salas do jogo uma a uma.
- [Um mundo novo](new-world.md): veja o mundo de uma semente antes que ele exista, e crie-o.
- [Modo ao vivo](live-mode.md): Aplicar ao vivo, Auto, Recarregar e os jogadores, enquanto o jogo roda.
- [Script](scripting.md): molde, pinte e preencha uma área com scripts em C#.
- [Claude](claude.md): o que o Claude pode fazer no editor, e os limites dele.

## Como funciona

- [Áreas e zonas](concepts/areas-and-zones.md): por que o editor abre um bloco do mundo de cada vez.
- [Salvamento e histórico](concepts/saving-and-history.md): alterações pendentes, salvar, aplicar ao vivo, desfazer e
  backups.
- [Limites do terreno](concepts/ground-limits.md): o limite de 8 m do jogo e como o editor vai além dele.
- [Edição ao vivo](concepts/live-editing.md): como o editor conversa com um jogo em execução.
- [Objetos](concepts/objects.md): o que é um objeto, encaixe, a verificação de suporte, regras de crescimento, pontos
  de surgimento.
- [Masmorras no jogo](concepts/dungeons.md): como o jogo guarda as masmorras e como o gerador monta uma.
- [Segurança](concepts/safety.md): o que o editor grava e por que nada sai do seu computador.

## Ajuda

- [Solução de problemas](help/troubleshooting.md): o que fazer quando algo não funciona.
- [Perguntas](help/faq.md): respostas curtas para o que as pessoas mais perguntam.
- [Atalhos](help/shortcuts.md): todas as teclas e ações do mouse.
- [Glossário](help/glossary.md): as palavras que o editor usa.

## Para tradutores e autores de mods

- [Tradução](translating.md): os catálogos de idiomas, como criar um e como traduzir esta documentação.
- [Para autores de mods](modding.md): o que o editor aprende com os mods, `worldeditor.json`, extensões e complementos.
