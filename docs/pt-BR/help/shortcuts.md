<!-- source: 43410d17 -->
# Atalhos

Todas as teclas e ações do mouse do editor, agrupadas pelo que você está fazendo. O botão **?** na barra superior (ou
`F3`) mostra as principais enquanto você trabalha.

As teclas só funcionam no editor 3D: não enquanto você digita numa caixa, nem na página inicial ou no mapa do mundo
(exceto `F1`: ela funciona em todo lugar). Num
Mac, pressione `Cmd` onde esta página diz `Ctrl` (`Cmd+Z` desfaz, `Cmd`+clique adiciona a uma seleção) e `Option` no
lugar de `Alt`.

## Câmera

| Tecla ou mouse | Ação |
|---|---|
| Arrastar com o direito | Girar a visão. Em Andar e Voar: olhar em volta. |
| Roda | Zoom, de 3 m a 3 km do ponto para onde você olha. |
| Arrastar com o botão do meio | Deslizar a visão. |
| `Space` + arrastar com o esquerdo, `Shift` + arrastar com o direito | Deslizar a visão, em qualquer ferramenta (útil sem botão do meio). |
| Arrastar com o esquerdo (ferramenta **Visão**) | Deslizar a visão. |
| `W` `A` `S` `D` ou as setas | Andar pela cena. Quanto mais afastado o zoom, mais rápido. Não enquanto `Ctrl` ou `Alt` estiver pressionado. |
| `F` | Andar na altura dos olhos, depois Voar, depois voltar à visão normal, em qualquer ferramenta. |
| `Shift` (Andar) | Correr: 12 m/s em vez de 6. |
| `Space` / `C` (Voar) | Subir / descer, atravessando o terreno também (para dentro dos túneis e porões de um mod). `Shift`: 45 m/s em vez de 15. |

Andando ou voando, a roda não dá zoom e arrastar não desliza a visão. Veja
[Andar e voar](../editor-basics.md#andar-e-voar).

## Painéis e salvamento

| Tecla | Ação |
|---|---|
| `V` | Abrir ou fechar o painel **Visão**. |
| `L` | Abrir ou fechar o **Histórico**. |
| `?` (`Shift+/`) ou `F3` | Abrir ou fechar o cartão de ajuda **Controles**. |
| `F1` | A janela de ajuda na página da ferramenta em uso (veja [A janela de ajuda](#a-janela-de-ajuda)). O pequeno **?** no alto de cada painel abre a página desse painel. |
| `Ctrl+S` | **Salvar no mundo** (um mundo salvo), **Aplicar ao vivo** (ao vivo), **Salvar projeto** (a Oficina). |

## Escolher uma ferramenta

| Tecla | Ferramenta |
|---|---|
| `1` `2` `3` `4` `5` | Elevar, Rebaixar, Aplanar, Suavizar, Restaurar |
| `0` | Ondular |
| `O` (a letra) | Erodir |
| `6` `7` `8` `9` | O pincel **Pintar** com Terra, Cultivar, Pavimentado, Limpar pintura |
| `B` | Região |
| `P` | Caminho |
| `T` | Colocar (**Construir** na Oficina) |
| `U` | Construir |
| `G` | Relevo |
| `E` | Selecionar |
| `M` | Medir |
| `H` ou `Esc` | Visão (`Esc` só quando não há mais nada a cancelar, veja [Como o Esc funciona](#como-o-esc-funciona)) |

Os dígitos do teclado numérico também funcionam. **Masmorra**, **Script** e **Projetos** não têm
tecla: clique neles na barra de ferramentas. Na Oficina, só `U` ou `T`, `E` e `H` ou `Esc` escolhem uma ferramenta.

## Desfazer, copiar e colar

| Tecla | Ação |
|---|---|
| `Ctrl+Z` | Desfazer a última alteração. |
| `Ctrl+Y` ou `Ctrl+Shift+Z` | Refazê-la. |
| `Ctrl+C` | Copiar a caixa ou o polígono de Região, ou os objetos selecionados (ferramentas Região e Selecionar). |
| `Ctrl+X` | Recortar: a mesma cópia, e então os objetos saem do mundo num único passo (o terreno fica). |
| `Ctrl+V` | Começar a colar a área de transferência, a partir de qualquer ferramenta. |
| `[` / `]` | Tamanho do pincel 1 m menor / maior (de 1 a 30 m). |

## Selecionar objetos

Estes funcionam na ferramenta **Selecionar** (`E`). Um clique na ferramenta **Visão** escolhe objetos do mesmo jeito.

| Tecla ou mouse | Ação |
|---|---|
| Clique | Selecionar só esse objeto. Clique de novo no mesmo lugar: o próximo objeto atrás dele. |
| `Ctrl`+clique | Adicionar o objeto, ou tirá-lo se ele estava selecionado. |
| `Shift`+clique | Tirar o objeto da seleção. |
| `Ctrl+Shift`+clique | Adicionar todos os objetos mostrados numa fileira, do último clicado até este. |
| Clique duplo | Uma peça de construção: a construção inteira. Uma parte de um navio ou de outro grupo vinculado: o grupo inteiro. Qualquer outro objeto (um baú, uma placa, uma árvore): esse objeto, com o Inspetor aberto. Na ferramenta Visão, um clique duplo num objeto passa para Selecionar. |
| Arrastar em terreno vazio | Selecionar o que está dentro da caixa (ou do contorno no terreno, com **Arrastar desenha** definido assim). `Ctrl`: adicionar à seleção. `Shift`: tirar. |
| `Alt` + arrastar | O mesmo, começando também sobre objetos (os objetos não são atingidos). |
| `Ctrl+A` / `Alt+A` | Selecionar todos os objetos mostrados / limpar a seleção. |
| `Ctrl+I` | Inverter a seleção. |
| `Ctrl+H` / `Alt+H` | Ocultar na visão os objetos selecionados / mostrá-los de novo. |
| `Alt+Z` | **Selecionar através** ligado ou desligado: uma caixa também pega o que está escondido atrás de outros. |
| `Shift+F` ou `.` do teclado numérico | Enquadrar a seleção. |

## Por ferramenta

### Pincéis de esculpir e pintar

| Tecla ou mouse | Ação |
|---|---|
| Arrastar com o esquerdo | Passar o pincel no terreno. |
| `Alt`+clique | Pegar a altura do terreno ali como altura de Aplanar, e passar para **Aplanar**. |
| `Alt+Shift`+clique | Definir a faixa de altura da [Máscara](../masks.md) de 2 m acima a 2 m abaixo desse lugar (também com a ferramenta Caminho). |

### Região

| Tecla ou mouse | Ação |
|---|---|
| Arrastar (Caixa) | Desenhar a caixa. |
| Clique (Polígono) | Adicionar um ponto. Clique duplo: fechar o polígono. |
| `Enter` | Fechar o polígono que está sendo desenhado; com a seleção pronta, fazer a ação escolhida. |
| `Enter`, depois `Del` | Com **Objetos** escolhido: selecionar os objetos de dentro e depois excluí-los na ferramenta Selecionar. |
| `Backspace` | Remover o último ponto do polígono. |
| `Esc` | Limpar a seleção (a caixa ou o polígono). |

### Caminho

| Tecla ou mouse | Ação |
|---|---|
| Clique | Adicionar um ponto. |
| Arrastar a linha | Adicionar um ponto ali e movê-lo. |
| Arrastar um ponto | Movê-lo. |
| `Ctrl`+clique num ponto | Removê-lo. |
| `Alt`+clique | Pegar a altura do terreno ali como altura do caminho (Rampa: o início dela). |
| `Ctrl+Alt`+clique | Rampa: pegar a altura como o fim dela. |
| `Alt+Shift`+clique | Definir a faixa de altura da [Máscara](../masks.md), como com um pincel. |
| `Enter` | Aplicar o caminho. |
| `Backspace` | Remover o último ponto. |
| `Esc` | Limpar a linha. |

### Colocar

| Tecla ou mouse | Ação |
|---|---|
| Arrastar com o esquerdo (Pincel) | Colocar objetos sob o pincel. `Shift` + arrastar: retirá-los. |
| Clique (Linha, Polígono) | Adicionar um ponto. Arrastar o contorno: adicionar um ponto ali. Arrastar um ponto: movê-lo. `Ctrl`+clique num ponto: removê-lo. |
| Clique duplo no último ponto | Colocar ao longo da forma. |
| Arrastar (Grade, formas de linha) | Desenhar a caixa da grade, ou a forma de uma ponta à outra. |
| `Alt`+clique | Pegar a altura dali como **Elevação**. |
| `Enter` | Colocar ao longo da linha, do círculo ou do retângulo, ou na grade ou no polígono. |
| `Backspace` | Remover o último ponto da linha ou do polígono. |
| `Esc` | Limpar a forma que está sendo desenhada, ou cancelar **Escolher no mundo**. |
| `N` | Uma nova disposição aleatória. |
| `R` | Girar a prévia, uma grade desenhada ou um polígono um quarto de volta. |
| `,` / `.` ou `Alt` + roda | Girar a prévia, uma grade desenhada ou um polígono em 15° (`Shift`, mais fino: 1°). |
| `PgUp` / `PgDn` ou `Ctrl` + roda | Elevar ou rebaixar o que você coloca em 0,5 m (`Shift`, mais fino: 0,1 m). |
| Clique num objeto (**Escolher no mundo**) | Pegar o tipo dele. `Shift`+clique o adiciona aos tipos escolhidos. |

### Construir, Colar e mover uma seleção: os passos da barra de construção

[Construir](../build.md), Colar e Selecionar giram e erguem com as mesmas teclas, pelos passos definidos na
[barra de construção](../build.md#a-barra-de-construção) (**Girar em**, 22.5° de início; **Erguer**, 0.5 m de início).
`Shift` sempre deixa o passo mais fino.

| Tecla ou mouse | Ação |
|---|---|
| `,` / `.` ou `Alt` + roda | Girar a peça, a colagem ou a seleção pelo passo **Girar em** (`Shift`: 1°). |
| `PgUp` / `PgDn` ou `Ctrl` + roda | Erguer ou baixar pelo passo **Erguer** (`Shift`: um quinto dele). |

### Construir (mundos, a Oficina e construir numa masmorra)

| Tecla ou mouse | Ação |
|---|---|
| Clique | Colocar a peça (**Dispor**: **Uma peça**), ou adicionar um ponto (**Linha**). |
| Arrastar | Uma fileira da peça (**Uma peça**); o círculo, o retângulo ou a grade (**Dispor**). |
| Segurar `Alt` | Colocar a peça de quarto em quarto de metro, sem encaixe. |
| `R` | Girar a peça um quarto de volta. |
| `Q` / `E` | Com **Encaixe** em **Manual**: o ponto de encaixe anterior / seguinte da peça. |
| `Shift` + clique direito | Retirar a peça sob o ponteiro. |
| Clique do meio | Construir com o tipo da peça sob o ponteiro. |
| `Enter` | Colocar a linha, o círculo, o retângulo ou a grade. |
| `Backspace` | Remover o último ponto da linha. |
| `Esc` | Limpar a linha, a figura ou a grade que está sendo desenhada. |

### Colar

| Tecla ou mouse | Ação |
|---|---|
| Clique | Colocar a colagem. Clique de novo para mais cópias. |
| `R` | Girar um quarto de volta. |
| `X` | Espelhar. |
| `Esc` | Parar de colar e voltar à ferramenta Região. |

### Mover a seleção (Selecionar)

| Tecla ou mouse | Ação |
|---|---|
| Arrastar um objeto selecionado | Mover a seleção. |
| Arrastar uma seta ou o anel | Mover ao longo desse eixo, ou girar. |
| `R` | Girar a seleção um quarto de volta. |
| `End` | Largá-la sobre o terreno. |
| `Del` | Excluir os objetos selecionados. |
| `I` | Abrir o Inspetor do objeto selecionado (um objeto selecionado). |
| `Esc` | Cancelar um movimento em andamento, ou uma caixa ou contorno que está sendo desenhado. |

### Outras ferramentas

| Ferramenta | Tecla ou mouse | Ação |
|---|---|---|
| Relevo | Clique | Pôr o relevo ali: a forma da fórmula, a montanha ou a imagem. |
| Medir | Clicar em dois pontos | Medir entre eles. `Esc` apaga a medição. |
| Masmorra | `R` | Girar a sala a adicionar: ela se une pela abertura seguinte. |
| | `Del` | Excluir a sala selecionada. |
| Script | `Ctrl+Enter` | Executar o script (na caixa de código). |
| Inspetor | `Esc` | Fechá-lo. |
| A ferramenta de um mod | As próprias teclas | Uma ferramenta em **MODS** na barra de ferramentas pega primeiro as teclas que usa; as outras funcionam como de costume. |

## Como o Esc funciona

`Esc` faz a primeira destas que se aplicar, e para:

1. Cancela a escolha de um tipo com **Escolher no mundo**.
2. Para de colar (de volta à ferramenta Região).
3. Fecha o Inspetor.
4. Cancela o que a ferramenta está fazendo: um movimento em Selecionar, uma caixa ou um contorno sendo desenhado, uma
   forma de Colocar ou uma linha disposta com Construir, uma caixa ou um polígono de Região, os pontos de Caminho, uma
   medição.
5. Passa para a ferramenta Visão.

## O mapa do mundo e um mundo novo

| Mouse | Ação |
|---|---|
| Arrastar | Deslizar o mapa. |
| Roda | Dar zoom na direção do mouse. |
| Clique | Escolher um local (a zona para **Editar em 3D**). |
| Clique duplo (o mapa de um mundo novo) | Mostrar o mundo inteiro. |
| `Enter` numa caixa de busca | Buscar. |

## A janela de ajuda

| Tecla | Ação |
|---|---|
| `F1` | Abrir a ajuda no que você está usando: a ferramenta, o Inspetor, o mapa, a página inicial… (em qualquer página). |
| `Ctrl+F` | Buscar na ajuda. |
| `Alt+Left` / `Alt+Right` | Voltar / avançar. |
| `Esc` | Fechar uma imagem ampliada, ou então esvaziar a busca. |
| `Enter` na caixa de busca | Abrir a primeira página encontrada. |

## Relacionados

- [A janela do editor](../editor-basics.md): a barra superior, a barra de ferramentas, a câmera e os painéis.
- [Selecionar](../select.md): selecionar e mover objetos.
- [Colocar](../place.md), [Construir](../build.md), [Caminho](../path.md), [Região, copiar e colar](../area.md): o que
  as teclas de cada ferramenta fazem.
- [Glossário](glossary.md): as palavras que o editor usa.
