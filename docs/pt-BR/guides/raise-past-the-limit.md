<!-- source: c7695b6c -->
# Elevar ou cavar além do limite de 8 m do jogo

Erga um penhasco ou uma agulha, ou cave um cânion, muito além dos 8 m que o jogo permite, de um jeito que o jogo de
cada jogador carrega sem nenhum mod.

**Você precisa de:** um mundo aberto em 3D ([uma cópia ou um backup dele](back-up-a-world.md)), com o jogo fechado, ou
o [modo ao vivo](edit-your-game-live.md).

1. Faça backup do mundo. Salvar esse tipo de terreno limpa o Histórico da área.
2. Na barra de ferramentas, clique em **Elevar** (`1`) ou **Rebaixar** (`2`).
3. No cartão **Máscara**, ao lado das configurações do pincel, marque **Sem limite: além dos ±8 m do jogo**. A barra de
   status diz que as ferramentas de terreno agora passam dos ±8 m do jogo.
4. Defina **Tamanho** e **Intensidade**, depois segure o botão esquerdo sobre o terreno e mova. O terreno não para mais
   em 8 m nem fica vermelho.
5. Opcional: dê forma ao resultado com **Suavizar** (`4`) ou **Ondular** (`0`), que continuam funcionando além do
   limite enquanto Sem limite estiver ligado.
6. Olhe a forma de vários lados. `Ctrl+Z` desfaz um traço; **Restaurar** (`5`) desfaz parte de um.
7. Clique em **Salvar no mundo** (ou **Aplicar ao vivo**). A mensagem do salvamento diz quantos discos de terreno
   foram feitos e quantas árvores e rochas foram retiradas, por exemplo "Terreno sem limite: 312 disco(s) de terreno,
   40 árvore(s)/rocha(s) retirada(s)".

Para uma montanha inteira num clique, use [Relevo](../landform.md#montanha) com o tipo **Montanha** em vez disso: ele
sempre passa do limite.

## Verificar se funcionou

- Passe o cursor sobre o novo terreno: a altura "original" da barra de status agora é o novo terreno.
- Abra o mundo no jogo e vá até lá. A forma aparece quando você chega perto, como o terreno aplainado de uma aldeia.

## Desfazer

- **Antes de salvar:** `Ctrl+Z`, o pincel **Restaurar**, ou **Descartar** para tudo o que não foi salvo.
- **Depois de salvar:** o Histórico recomeça a partir do mundo salvo. O pincel **Restaurar** ainda devolve o lugar ao
  terreno que o mundo gerou (assim como **Restaurar** da ferramenta Caminho e **Voltar ao terreno gerado** de Região):
  salve de novo, e os discos de terreno de que ele não precisa mais vão embora. Ou devolva o lugar ao que era com
  [Restaurar uma área a partir de um backup](restore-from-backup.md).

## Relacionados

- [Sem limite](../concepts/ground-limits.md#sem-limite): como o terreno é salvo além de 8 m
- [Esculpir e pintar](../terrain.md#sem-limite)
- [Relevo](../landform.md)
