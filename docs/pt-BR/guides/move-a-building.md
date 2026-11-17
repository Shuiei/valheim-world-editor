<!-- source: 9e216b4e -->
# Mover ou copiar uma construção

Mova uma casa, com tudo o que há dentro, alguns metros ou para o outro lado do mundo, ou ponha uma cópia dela em
outro lugar.

**Você precisa de:** um mundo aberto no editor, com a área em volta da construção aberta (um backup antes: veja
[Fazer backup de um mundo](back-up-a-world.md)).

## Passos

1. Pressione `E` para abrir [Selecionar](../select.md).
2. Dê um clique duplo numa parede da construção: todas as peças ligadas ficam selecionadas, inclusive os baús e os
   móveis sobre os pisos dela.
3. Opcional: para adicionar objetos soltos dentro (uma mesa separada), use `Ctrl` + clique neles.
4. Para um movimento curto, arraste uma peça selecionada, ou arraste uma seta para mover ao longo de um eixo; arraste
   o anel laranja para girar a construção. Mantenha **Pôr cada objeto no chão ao mover** ligado, para que a construção
   se assente no chão onde pousar.
5. Para um movimento longo, pressione `Ctrl+X` (ou `Ctrl+C` para uma cópia). A construção agora está na área de
   transferência.
6. Abra a área no novo lugar (a partir do [Mapa](../map.md)), ou fique nesta.
7. Pressione `Ctrl+V`: a construção segue o cursor, transparente.
8. Gire-a com `R` (um quarto de volta) ou `,` `.` (pelo **Girar em** da barra de construção) e clique onde ela deve
   ficar.
9. Pressione `Esc` para parar de colar.
10. Clique em **Salvar no mundo** (ou **Aplicar ao vivo**).

As cópias e as peças movidas guardam tudo o que contêm: o conteúdo dos baús, os textos das placas, quem as construiu.
Uma construção colada com `Ctrl+V` leva junto o terreno embaixo dela.

## Verificar se funcionou

A barra de status diz quantos objetos foram movidos ou colados. Olhe a construção de vários lados: nada deve flutuar
nem ficar para trás. Depois de salvar, abra o mundo no jogo e entre nela.

## Desfazer

- Antes de salvar: `Ctrl+Z` reverte o movimento, o recorte ou a colagem (um recorte e a colagem dele são passos
  separados).
- Depois de salvar: veja [Desfazer um passo, reverter uma alteração ou descartar tudo](undo-changes.md), ou
  [Restaurar uma área a partir de um backup](restore-from-backup.md).

## Dicas

- Uma colagem precisa caber dentro da área aberta: se aparecer *Não colado*, abra uma área maior em volta do lugar.
- A área de transferência continua: você pode colar em outra área ou em outro mundo.
- Para mover por metros exatos, use **POSIÇÃO EXATA** → **Deslocar em** no painel Selecionar.
- Uma construção arrastada se prende a peças a até 0,75 m enquanto **Encaixe** estiver em **Auto** na
  [barra de construção](../build.md#a-barra-de-construção). Escolha **Livre** para deixá-la exatamente onde você a arrasta.
- Para um navio ou outro veículo de mod, selecione qualquer parte: o grupo inteiro se move.
- No modo ao vivo, uma construção movida enquanto nenhum jogador está perto dela continua sendo o mesmo objeto no
  jogo, então os mods que a acompanham continuam a acompanhá-la: veja
  [Mover um objeto mantém o mesmo objeto](../concepts/live-editing.md#mover-um-objeto-mantém-o-mesmo-objeto).

## Relacionados

- [Selecionar](../select.md) · [Região](../area.md)
- [Encaixe](../concepts/objects.md#encaixe)
- [Colar um projeto num mundo](paste-a-blueprint.md)
