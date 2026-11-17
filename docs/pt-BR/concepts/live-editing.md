<!-- source: 6167da37 -->
# Edição ao vivo

No modo ao vivo, o editor trabalha no mundo de um jogo **em execução** em vez de nos arquivos dele. Os jogadores podem
continuar conectados: eles veem suas alterações assim que você as aplica, e você vê o que eles constroem enquanto
edita. Esta página explica como o editor chega ao jogo, o que passa entre eles e o que acontece quando jogadores e o
editor alteram o mesmo lugar.

## Como ele se conecta

O modo ao vivo precisa de um pequeno plugin, o **WorldEditorBridge**, dentro do jogo que **hospeda** o mundo: o
servidor dedicado, ou o seu próprio jogo quando você joga sozinho ou hospeda. Ele roda no **BepInEx**, o carregador de
mods usual do Valheim. Os jogadores que entram não precisam de nenhum dos dois, inclusive os jogadores de console, e a
edição fora do jogo também não.

O plugin responde ao editor numa porta do computador em que roda (5182 no início), e é feito para continuar privado:

- **Ele só escuta no próprio computador** (`127.0.0.1`), então nada na rede consegue alcançá-lo. A configuração
  `BindAddress` dele pode abri-lo para as redes locais e VPNs do computador (nunca para a internet: essas chamadas são
  recusadas), com o token sem criptografia; deixe-a como está, a menos que você edite de outro computador da casa
  ([Sem SSH](../live-mode.md#sem-ssh)).
- **Ele precisa do token secreto dele.** Na primeira inicialização, o plugin grava
  `BepInEx/config/Tie.WorldEditorBridge.cfg` com um **token** de 24 letras e dígitos aleatórios. Um pedido sem ele é
  recusado. Até o jogo ou o servidor ter iniciado uma vez com o plugin, não existe token.

**O seu próprio jogo.** O editor roda no mesmo computador, então ele mesmo lê a porta e o token desse arquivo, em
todas as pastas do BepInEx que conhece (a pasta do seu Valheim, os perfis do seu gerenciador de mods, as pastas
adicionadas em **Configurações**), e pergunta a cada plugin encontrado se há um mundo carregado.

**Um servidor dedicado.** O plugin não pode ser alcançado diretamente do seu computador, então o editor monta o
próprio caminho até ele:

```text
 seu computador                                  o servidor
 ┌──────────────────────┐ SSH, criptografado ┌─────────────────────────────────────┐
 │ editor ─ porta 15182 ═╪═══════════════════╪═► 127.0.0.1:5182  WorldEditorBridge │
 └──────────────────────┘                    │                   dentro do Valheim │
                                             └─────────────────────────────────────┘
```

1. Ele faz login no servidor por **SSH** com a sua conta (ele tem o próprio SSH, então nenhum programa `ssh` é
   necessário).
2. Ele encaminha uma porta livre do seu computador (de 15182 para cima), por essa conexão criptografada, até a porta
   do plugin no servidor: um **túnel SSH**.
3. Ele envia o token, que **você** digita: o editor nunca o lê do servidor. Assim, um login SSH sozinho não basta para
   alterar o mundo; quem se conecta também precisa saber o segredo do plugin.

Um servidor que um painel de jogo (Pterodactyl, Pelican) roda num contêiner Docker tem o próprio `127.0.0.1`, que o
túnel até a máquina não alcança, e um servidor de jogo alugado de uma hospedagem normalmente não dá nenhum login SSH:
veja [Editar um servidor alugado ou hospedado num painel](../guides/edit-a-rented-server.md).

O editor também memoriza a identidade do servidor na primeira conexão. Se ela algum dia mudar, o editor se recusa a
conectar, porque alguém poderia estar se passando pelo seu servidor.

O plugin só altera um mundo que o jogo dele hospeda. Num jogo que entrou no servidor de outra pessoa, ele se recusa,
já que esse jogo só conhece os objetos em volta do próprio jogador. Use o plugin da mesma versão do seu editor.

## O que é enviado e quando

**Quando você se conecta**, o editor lê o mundo inteiro do jogo: cada objeto, as zonas que o jogo já criou, os locais.
São os mesmos dados que um salvamento contém, então o mapa, a busca e todas as ferramentas funcionam como fora do jogo.
Num mundo grande, isso leva um tempo, até alguns minutos. Ele também pede os objetos que o jogo conhece e que não são
do próprio Valheim (peças de mods), para que ganhem os nomes deles e apareçam na lista de Colocar e no painel
Construir.

**Quando você aplica** (**Aplicar ao vivo**, ou cada passo na hora com **Auto**), ele envia o que mudou desde a última
aplicação, nesta ordem:

1. **O terreno** de cada zona que você alterou, inclusive o terreno Sem limite (veja
   [Sem limite](ground-limits.md#sem-limite)).
2. **Os objetos** que você excluiu, colocou, colou, substituiu, alterou ou moveu.
3. **As redefinições de zona**, por último. O jogo gera essas zonas de novo, então o editor lê o mundo de novo em
   seguida; o histórico vem junto sem o que ele fez nessas zonas.

O jogo compartilha cada alteração com todos os jogadores conectados, como faz com o que os jogadores constroem, e a
salva com o mundo. Os objetos colocados ao vivo são criados pelo servidor, então os jogadores os veem como algo que
acabou de ser construído ou plantado.

O plugin faz tudo isso no próprio laço do jogo, alguns pedidos por quadro, para que o jogo nunca leia nem altere o
mundo de dois lugares ao mesmo tempo. Uma alteração grande (milhares de objetos) pode fazer o servidor engasgar por um
instante. Se o jogo não aceitar um pedido em 30 segundos, o plugin o descarta: nada dele é feito e nada é marcado como
aplicado, então aplique de novo. Quando, em vez disso, a resposta se perde na volta (um túnel que caiu), o editor não
tem como saber se o pedido foi feito; enviá-lo de novo é seguro, pois o plugin reconhece um pedido que já fez e não
cria os objetos dele duas vezes. Uma aplicação roda por vez.

### Mover um objeto mantém o mesmo objeto

Um objeto que você move ou gira se move no jogo com o próprio id quando nada mais nele mudou. Um mod que acompanha os
objetos enquanto o jogo roda (a placa de etiqueta de um baú, os baús de um depósito, uma proteção) continua a
reconhecê-lo, e um baú mantém o conteúdo como o jogo o tem. Um objeto cujos dados você alterou mantém o id da mesma
forma.

Há um porém. O jogo de um jogador põe um objeto fixo (uma parede, um baú) no lugar uma vez, quando ele entra no
alcance, e nunca mais o move. Por isso, um movimento só mantém o id quando nenhum jogador tem carregadas ao mesmo tempo
a posição antiga e a nova dele (cerca de 160 m em volta de cada jogador). Senão, o objeto é recriado na nova posição e
retirado da antiga, e a mensagem da aplicação diz quantos: para outros mods, esses são objetos novos. Mova uma
construção enquanto ninguém está perto dela para manter tudo nela. Um grupo vinculado de um mod (um navio e as peças dele)
e as criaturas são sempre recriados.

## Acompanhar o jogo

Enquanto uma área está aberta, o editor pergunta ao jogo a cada 2 segundos o que mudou nas zonas dessa área. Isso custa
pouco: o jogo responde com um número por zona, que muda sempre que algo na zona muda, e só as zonas cujo número mudou
são lidas de novo. A barra de status diz o que chegou ("Do jogo: 2 objeto(s) novo(s), 1 removido, o terreno de 1
zona(s)."); uma mudança só de dados, como uma fogueira se apagando, chega sem aviso.

- **Objetos** que os jogadores constroem, removem ou alteram aparecem, somem ou mudam no editor, sem um passo no
  Histórico. Um objeto alterado continua sendo o mesmo objeto no editor, então a seleção, o Inspetor e o lugar dele no
  Histórico continuam com ele.
- **Os seus próprios objetos, depois de aplicados,** aparecem como o jogo realmente os tem: onde a física assentou uma
  peça, a vida e o ponto de surgimento que o jogo deu a uma criatura.
- **Coisas que se movem ou mudam sozinhas** (criaturas, navios, carroças, itens largados, fogueiras e fundições) são
  acompanhadas conforme mudam, sem ler a zona inteira delas a cada vez.
- **Terreno** cavado, nivelado ou pintado no jogo chega onde você não tem alterações próprias naquela zona (veja abaixo
  quando tem).

O acompanhamento só acontece no editor 3D, nunca durante um traço de pincel nem durante uma aplicação. O mapa mostra o
mundo como foi lido por último: **Recarregar** lê tudo de novo.

## Vários editores e jogadores

Os jogadores e o editor podem alterar o mesmo lugar. O editor resolve assim:

| O que acontece | O que o editor faz |
|---|---|
| Jogadores alteram o terreno de uma zona onde você tem alterações ainda não aplicadas | As suas são mantidas e a barra de status avisa. **Aplicar ao vivo** então pergunta antes (**Aplicar sobre as alterações do jogo**): aplicar grava o seu terreno por cima dessa zona, e o que os jogadores fizeram nela desde então pode se perder. **Agora não** mantém suas alterações pendentes. |
| Jogadores pegam ou destroem um objeto | Ele some de vez: desfazer e refazer o deixam de lado, e uma exclusão dele que você não tinha aplicado é descartada. Isso inclui objetos que você colocou. |
| Você exclui e aplica um objeto | Ele continua excluído; desfazer o traz de volta como uma cópia (veja [Aplicar ao vivo](saving-and-history.md#aplicar-ao-vivo)). |
| Você move uma construção enquanto há jogadores perto dela | Ela é recriada em vez de movida (veja [Mover um objeto mantém o mesmo objeto](#mover-um-objeto-mantém-o-mesmo-objeto)). |
| Você altera um objeto no Inspetor enquanto o jogo também o altera | O Inspetor não aplica por cima da versão mais nova: ele a mostra, para você alterar de novo. |
| Você redefine uma zona perto da qual há um jogador | O jogo a gera de novo na hora; senão, na próxima vez que alguém chegar. |

O plugin responde a qualquer editor que tenha o token, e não distingue as alterações de um editor das de um jogador: o
que outro editor aplica chega ao seu do mesmo jeito que a alteração de um jogador.

**Auto e o Claude.** Com **Auto** ligado, cada passo chega aos jogadores no momento em que você o termina, e um traço
de pincel longo vai como uma só aplicação. Por esse motivo, o Claude não altera nada num mundo ao vivo enquanto Auto
está ligado (veja [Scripts e o Claude](safety.md#scripts-e-o-claude)).
