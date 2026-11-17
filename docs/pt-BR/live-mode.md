<!-- source: e9428d01 -->
# Modo ao vivo

Edite o mundo de um jogo ou de um servidor dedicado em execução enquanto as pessoas jogam: elas veem cada alteração
assim que você a aplica.

**Onde:** página inicial, **Editar ao vivo** em **Página inicial** ou **Meu jogo**, ou **Um servidor dedicado** ·
**Tecla:** `Ctrl+S` aplica · **Funciona em:** o seu próprio jogo (um jogador ou o mundo que você hospeda), um servidor
dedicado

![O editor 3D conectado a um servidor em execução: AO VIVO, Recarregar e Auto na barra superior, uma cabana nova marcada com pontos verdes](../images/live.jpg)
*Conectado a um servidor em execução: **AO VIVO**, **Recarregar** e **Auto** na barra superior, e uma cabana que o jogo
ainda não tem ("Não aplicado: 1 zona, 40 adicionado(s)", pontos verdes nas peças novas).*

O modo ao vivo precisa do **BepInEx** e do plugin **WorldEditorBridge** no jogo que hospeda o mundo. Os jogadores que
entram não precisam de nenhum dos dois. Um servidor dedicado também precisa dar a você **acesso SSH** à máquina em que
roda (um login de shell, não só SFTP ou o gerenciador de arquivos de um painel web): a maioria dos servidores de jogo
alugados de uma hospedagem não dá, veja [Editar um servidor alugado ou hospedado num painel](guides/edit-a-rented-server.md).
Para configurar e conectar, siga [Editar o mundo em que você está jogando](guides/edit-your-game-live.md) ou
[Editar um servidor dedicado enquanto as pessoas jogam](guides/edit-a-server-live.md). Como o editor conversa com o jogo
está em [Edição ao vivo](concepts/live-editing.md#como-ele-se-conecta).

## Como usar

1. Conecte-se: na página inicial, clique em **Editar ao vivo** (o seu jogo) ou em **Conectar** ao lado de um servidor
   salvo.
2. No mapa do mundo, clique num ponto e escolha **Editar em 3D**.
3. Edite com qualquer ferramenta, como faria offline. Os objetos novos levam um ponto verde; a barra superior conta o
   que não foi aplicado.
4. Pressione **Aplicar ao vivo** (ou `Ctrl+S`). Os jogadores veem a alteração na hora, e o jogo a salva como de costume.
5. Para reverter uma alteração, desfaça-a (`Ctrl+Z`) e pressione **Aplicar ao vivo** de novo.

## Configurações

O modo ao vivo funciona como offline, com estes controles na barra superior:

| Controle | O que faz |
|---|---|
| Selo **AO VIVO** | Mostra que o editor está conectado ao jogo em execução. |
| **Recarregar** | Lê de novo o mundo inteiro do jogo. As alterações ainda não aplicadas são descartadas, depois de perguntar. Você continua onde está. |
| **Auto** | Ligado: cada traço, colocação e desfazer é aplicado na hora, sem pressionar **Aplicar ao vivo**. Ao ligá-lo, o que já está esperando é enviado. Lembrado entre execuções. |
| Contador de pendências | "Não aplicado: …" em âmbar, ou "Tudo aplicado". A linha do mapa diz "Ainda não aplicado: …". |
| **Aplicar ao vivo** | Ocupa o lugar de **Salvar no mundo**. Envia ao jogo todas as alterações pendentes. |
| Lista de jogadores, **Ir** | Ao lado de **AO VIVO**, enquanto houver um jogador conectado: os jogadores conectados, pelo nome. Escolha um e depois **Ir** (veja [Jogadores](#jogadores)). |

## Aplicar ao vivo, Auto e Recarregar

**Aplicar ao vivo** envia o terreno de cada zona que você alterou, depois os objetos que você excluiu, colocou, moveu ou
alterou, e depois as redefinições de zona. Só vai o que mudou desde a última aplicação. A ordem e o que cada parte faz
estão em [O que é enviado e quando](concepts/live-editing.md#o-que-é-enviado-e-quando).

A barra de status diz então o que foi feito, por exemplo "Aplicado ao jogo em execução: 2 zona(s) de terreno; …". Se o
jogo não responder, nada é marcado como aplicado e a próxima aplicação envia tudo de novo. Uma aplicação roda por vez:
um segundo clique espera e envia só o que ainda estiver pendente.

![A mesma cabana depois de Aplicar ao vivo: Tudo aplicado, sem pontos verdes, e a barra de status com o resultado da aplicação](../images/live-applied.jpg)
*Depois de **Aplicar ao vivo**: "Tudo aplicado", os pontos verdes sumiram, e a barra de status diz "Aplicado ao jogo em
execução: …".*

**Auto** faz de cada alteração uma aplicação. Ele espera o fim de um traço, então um traço longo de pincel vai como uma
única aplicação. Enquanto Auto estiver ligado, o [Claude](claude.md) não altera nada; desligue Auto para deixá-lo
trabalhar.

**Recarregar** raramente é necessário: a área aberta acompanha o jogo sozinha. Use-o para atualizar o resto do mundo
(o mapa), ou quando o plugin for antigo demais para acompanhar.

Depois de cada aplicação, o histórico é guardado em disco, como depois de um salvamento: conecte-se ao mesmo jogo mais
tarde e os passos estarão lá para desfazer (veja [Histórico](concepts/saving-and-history.md#histórico)).

## Acompanhar o jogo

Enquanto uma área está aberta, o editor pergunta ao jogo a cada 2 segundos o que mudou nas zonas dessa área. O que os
jogadores constroem, removem, cavam ou pavimentam ali aparece no editor sem um passo no Histórico. A barra de status diz
o que chegou, por exemplo "Do jogo: 2 objeto(s) novo(s), 1 removido(s), o terreno de 1 zona(s)." Uma mudança só de dados
(uma fogueira que se apaga, um baú usado) chega sem aviso.

- Os **objetos** que os jogadores alteram mantêm o lugar deles no seu Histórico e na sua seleção.
- **Os seus próprios objetos, depois de aplicados,** aparecem onde e como o jogo realmente os tem (uma peça acomodada
  pela física, a saúde de uma criatura).
- O **terreno** que os jogadores alteraram só chega onde você não tem alterações esperando naquela zona. Onde tem, as
  suas são mantidas, e **Aplicar ao vivo** pergunta primeiro **Aplicar sobre as alterações do jogo**: **Aplicar** grava o
  seu terreno por cima naquela zona (o que os jogadores fizeram ali desde então pode ser apagado); **Agora não** mantém
  as suas alterações pendentes.
- O acompanhamento só acontece na visão 3D, nunca durante um traço nem durante uma aplicação. O mapa mostra o mundo como
  ele estava na última leitura.

Os detalhes estão em [Acompanhar o jogo](concepts/live-editing.md#acompanhar-o-jogo).

<!-- picture: live-apply-over.jpg: live world, a 3 × 3 area where you raised ground in one zone and a player then dug in the same zone; Apply live pressed: the "Apply over the game's changes" question with its text naming the zone, and the Apply and Not now buttons, over the 3D view -->

## Jogadores

Os jogadores conectados são desenhados no lugar em que estão, com o nome: postes azuis na visão 3D, pontos azuis no
mapa. Eles se movem como os jogadores se movem (a cada 2 segundos em 3D, a cada 5 no mapa). Na barra superior, ao lado
de **AO VIVO**, escolha um jogador na lista e pressione **Ir**: a câmera vai até ele, e a área se move até ele quando
ele está fora dela.

<!-- picture: live-players.jpg: live world with two players connected, 3D view at about 60 m showing two blue posts with their names over a base, the top bar's player list beside LIVE with a name chosen and the Go button visible -->

## Mover coisas

Um objeto movido (com mover ou girar da ferramenta Selecionar, ou **Recortar** e depois colar) se move no jogo como o
mesmo objeto, quando só o lugar e o giro dele mudaram. Os mods que acompanham objetos (a placa de etiqueta de um baú, os
baús de um armazém, uma proteção) continuam a reconhecê-lo, e um baú mantém o conteúdo como o jogo o tem. Desfazer o
move de volta da mesma forma.

Isso só funciona quando nenhum jogador tem carregados ao mesmo tempo o lugar antigo e o novo (as zonas em volta de cada
jogador, cerca de 160 m, mais para objetos grandes). Caso contrário, o objeto é criado de novo no novo lugar e retirado
do antigo, e a mensagem da aplicação diz quantos: para outros mods, esses são objetos novos. **Mova uma construção
enquanto ninguém estiver por perto** para preservar tudo dela.

Um objeto cujos dados você altera (no Inspetor da ferramenta [Selecionar](select.md), ou com a ferramenta de um mod)
também mantém a identidade no jogo. As partes do grupo vinculado de um mod (como um navio) e as criaturas são sempre
criadas de novo.

## Desfazer depois de aplicar

As alterações aplicadas ainda podem ser desfeitas: desfaça (ou use **Remover** no Histórico) e depois **Aplicar ao vivo**
de novo.

- Um objeto **excluído** volta como uma cópia do que era quando o mundo foi lido (um baú com o conteúdo de então). Para
  o jogo, é um objeto novo.
- Um objeto **colocado** é removido do jogo de novo. Se outro mod o tiver criado de novo com um novo id nesse meio-tempo,
  o plugin o encontra pelo tipo e pelo lugar (num raio de 10 cm) e remove esse.
- Um objeto **movido** volta ao lugar, como o mesmo objeto quando possível.
- As **redefinições de zona** não podem ser desfeitas depois de aplicadas.

Os passos de uma sessão anterior aparecem marcados assim no Histórico. Os jogadores podem ter alterado o mundo desde
então, por isso você é consultado uma vez antes de desfazê-los ou refazê-los.

## Jogos com mods

Os mods criam a maioria dos objetos deles enquanto o jogo roda, por isso o editor os aprende com o jogo quando um mundo
ao vivo abre. Os objetos de um jogo com mods passam então a ter nome, a lista de [Colocar](place.md) os oferece, o
painel [Construir](build.md), num mundo como na [Oficina](workshop.md), lista as peças dos mods na aba **Peças de
mods** com o custo delas (um mod de cada vez, escolhido na lista ao lado; aparecem no máximo 300 peças, busque as
outras; os mods instalados neste computador aparecem ali mesmo sem um jogo ao vivo, com custo desconhecido), e o
Inspetor diz de que mod vem um objeto ("Do mod …" quando o Jötunn está instalado, senão "De um mod").

O que o editor aprende é guardado (`mod-catalogue.json` na pasta de dados do editor), então o mundo do mesmo servidor,
aberto mais tarde a partir dos arquivos dele, também mostra os objetos dos mods. Cada conexão acrescenta a ele.

Os modelos dos mods são desenhados como no jogo, a partir dos próprios arquivos dos mods na sua pasta do Valheim e nos
perfis do gerenciador de mods (os mesmos mods precisam estar instalados ali). Na primeira vez, os bundles deles são
copiados para a pasta de dados do editor (`mod-bundles`, alguns segundos). As malhas que um mod gera em código enquanto
o jogo roda (velas feitas na hora, por exemplo) não podem ser encontradas: essas peças aparecem sem textura. Um mod
também pode trazer um `worldeditor.json` para o que o editor não consegue adivinhar: veja
[Para autores de mods](modding.md).

### Os plugins dos próprios mods

Um mod cujo plugin se registrou no bridge é avisado na hora do que você aplicou (objetos criados, removidos e movidos,
zonas cujo terreno mudou ou foi redefinido), então o que ele monta a partir dos objetos dele (um navio a partir das
peças) é remontado na hora. O log do editor nomeia os mods registrados quando um jogo abre; um mod que falha numa
aplicação é nomeado no `BepInEx/LogOutput.log` do jogo, e a aplicação segue mesmo assim.

### Os mods Expand World

Com o [complemento Expand World](../../addons/ExpandWorld/README.md) instalado e ligado (**Configurações →
Complementos**), um mundo ao vivo segue os mods que o próprio jogo executa: as configurações e os arquivos de dados deles
são lidos da pasta `BepInEx` do próprio jogo por meio do plugin, então o mundo de um servidor é desenhado como esse
servidor o cria. Só a biblioteca YamlDotNet de que os arquivos de dados precisam vem de uma pasta BepInEx local. Os
arquivos são copiados em `live-bepinex` na pasta de dados do editor; o log diz o que foi lido. Com um plugin mais
antigo, as suas pastas locais são usadas no lugar.

### Riverheim

Um mundo Riverheim ao vivo é desenhado com a sua própria cópia do Riverheim: o
[complemento Riverheim](../../addons/Riverheim/README.md) precisa estar instalado e ligado, e o Riverheim instalado
neste computador (com o Worldhalla, para o Riverheim 1.3 e posteriores). O seu Riverheim precisa ser a versão de que um
jogador que entra no jogo precisa: desde o Riverheim 1.3, a mesma versão do jogo; antes disso, a mesma versão principal
(os dois primeiros números da versão). Caso contrário, o editor informa as duas versões e não abre o mundo.

## Configurações do plugin

As configurações do plugin ficam em `BepInEx/config/Tie.WorldEditorBridge.cfg` do jogo ou do servidor, gravado na
primeira execução. (Plugins anteriores à 0.41.0 o chamavam de `local.worldeditorbridge.cfg`; o plugin o renomeia, e o
editor lê qualquer um dos dois.)

| Configuração | Padrão | O que faz |
|---|---|---|
| `BindAddress` | `127.0.0.1` | O endereço em que o plugin escuta. Mantenha `127.0.0.1`: o editor alcança um servidor pelo próprio túnel SSH. Outro endereço expõe o plugin às redes locais e VPNs da máquina (nunca à internet: chamadas de outros lugares são recusadas), com o token sem criptografia, e o log avisa disso; veja [Sem SSH](#sem-ssh). `+` escuta em todos os endereços da máquina: para um servidor num contêiner (um painel de jogo como o Pterodactyl), veja [Editar um servidor alugado ou hospedado num painel](guides/edit-a-rented-server.md#um-servidor-num-contêiner). `0.0.0.0` não funciona: o plugin então recusa todos os pedidos. |
| `Port` | `5182` | A porta do plugin. Mude-a quando outro programa usar a 5182. |
| `Token` | criado na primeira execução | O segredo que o editor precisa enviar, num cabeçalho (nunca no endereço): 24 letras e algarismos aleatórios. Qualquer pessoa com ele e acesso à porta pode alterar o mundo, então mantenha-o em sigilo. Esvazie a linha e reinicie para ter um novo. |

## Sem SSH

O editor não precisa de SSH quando o jogo roda no mesmo computador: ele encontra o plugin ali sozinho. Para outro
computador, o plugin precisa escutar num endereço que esse computador consiga alcançar, em vez de `127.0.0.1`.

**Outro computador na mesma casa** (a mesma rede doméstica):

1. No computador que roda o jogo ou o servidor, descubra o endereço local dele: `ipconfig` no Windows ("Endereço IPv4"),
   `ip addr` no Linux. Ele se parece com `192.168.1.20`.
2. No `BepInEx/config/Tie.WorldEditorBridge.cfg` dele, defina `BindAddress = 192.168.1.20` (exatamente esse endereço,
   não `0.0.0.0`) e inicie o jogo ou o servidor de novo. O log diz `listening on http://192.168.1.20:5182/` e depois um
   aviso de que o token trafega sem criptografia.
3. Libere a porta no firewall desse computador, só em redes privadas. No Windows, no PowerShell como administrador (o
   Windows só a aplica quando essa rede está definida como **Privada**):

   ```powershell
   New-NetFirewallRule -DisplayName "WorldEditorBridge" -Direction Inbound -Protocol TCP -LocalPort 5182 -Action Allow -Profile Private
   ```

4. Copie a linha `Token` do mesmo arquivo.
5. No editor, na página inicial: **Um servidor dedicado** → **Mais opções** → **Criei meu próprio túnel**. Digite
   `192.168.1.20:5182` em **Endereço do bridge**, cole o token e pressione **Conectar**.

O token e as alterações do mundo atravessam a rede sem criptografia: qualquer pessoa no mesmo Wi-Fi poderia ler o token
e alterar o mundo. Tudo bem em casa; não numa rede compartilhada (um alojamento estudantil, um café). O roteador também
pode dar outro endereço a esse computador algum dia: aí o plugin deixa de escutar até que `BindAddress` seja alterado, a
menos que o roteador reserve o endereço para ele (uma "reserva de DHCP").

**Um computador fora de casa** (o de um amigo, uma máquina alugada): o plugin recusa chamadas vindas da internet, já que
o token a atravessaria às claras. Ou dê a esse computador um servidor SSH, que o próprio túnel criptografado do editor
passa a usar (o Windows 10 e o 11 têm um: **Configurações** → **Recursos opcionais** → **Servidor OpenSSH**), ou ponha os dois
computadores numa VPN (Tailscale, ZeroTier, WireGuard), que criptografa o trajeto, e siga os passos acima com o endereço
do computador na VPN (o `100.x.y.z` do Tailscale).

**Um servidor de jogo alugado de uma hospedagem, ou administrado por um painel de jogo** (Pterodactyl, Pelican, um
contêiner Docker): veja [Editar um servidor alugado ou hospedado num painel](guides/edit-a-rented-server.md).

## O que o plugin registra no log

O `BepInEx/LogOutput.log` do jogo ou do servidor diz o que cada aplicação fez, para quando uma alteração não aparece
para todos os jogadores. Ao iniciar, ele diz `WorldEditorBridge <version> listening on http://127.0.0.1:5182/ (token in
BepInEx/config/Tie.WorldEditorBridge.cfg)`. Depois, a cada aplicação:

- `WorldEditorBridge: destroyed N object(s) (M found again at their place under a new id, K already gone), created C,
  moved I in place and F by making it again (…); A -> B object(s) known, P peer(s)`: os objetos. *Found again* são
  objetos que outro mod tinha criado de novo, encontrados pelo tipo e pelo lugar. A e B são os objetos que o jogo
  conhecia antes e depois (os destruídos somem na próxima atualização do jogo, então B ainda os conta); P são os
  jogadores conectados.
- `WorldEditorBridge: 1 s after the apply: S of N destroyed object(s) still exist, R other object(s) of their prefab
  within 10 cm of their place (…)`: um segundo depois. S acima de 0 significa que algo no servidor (muito provavelmente
  um mod) criou de novo um objeto destruído ou o manteve. R conta os objetos do mesmo tipo que estão onde havia um
  destruído: criados de novo por um mod, ou ali desde sempre. Ambos em 0 significa que o servidor fez a parte dele, e o
  que um jogador ainda vê vem do próprio jogo desse jogador.
- `WorldEditorBridge: applied terrain to N zone(s) (C new)`: o terreno. *New* conta as zonas que não tinham alterações
  de terreno antes.
- `WorldEditorBridge: reset N zone(s): D object(s) removed, L location(s) to place again`: redefinições de zona.
- `WorldEditorBridge: caught at <file>:<line> (<member>) while <doing>: <error type>: <message>`: um erro que o plugin
  tratou, com o stack trace, a menos que seja um erro esperado. A pasta pessoal do servidor aparece como `~`.

## Atalhos

| Tecla | Ação |
|---|---|
| `Ctrl+S` | **Aplicar ao vivo** |
| `Ctrl+Z`, `Ctrl+Y` | Desfazer, refazer (depois **Aplicar ao vivo** envia, ou **Auto** envia na hora) |

Todas as outras teclas funcionam como offline: veja [Atalhos](help/shortcuts.md).

## Dicas e limites

- O plugin só funciona num jogo que **hospeda** o mundo (um servidor, um jogador ou o anfitrião). Num jogo que entrou no
  servidor de outra pessoa, ele se recusa a alterar qualquer coisa.
- As **redefinições de zona** acontecem na hora onde há um jogador por perto; caso contrário, na próxima vez que alguém
  chegar. Elas não podem ser desfeitas depois de aplicadas.
- Os objetos colocados ao vivo são criados pelo servidor; os jogadores os veem como objetos que alguém acabou de
  construir ou plantar.
- Milhares de objetos de uma vez são aplicados de uma só vez e podem fazer o servidor travar por um instante. Ler um
  mundo grande ao conectar pode levar alguns minutos.
- Se o jogo não responder em 30 segundos (está carregando, ou muito ocupado), nada desse pedido é feito: aplique de novo.
- Use o plugin da mesma versão do seu editor. Com um plugin mais antigo, o editor diz o que falta. Os recursos que
  precisam de um plugin recente:

| Versão do plugin | Necessária para |
|---|---|
| 0.10.0 | Redefinições de zona |
| 1.17.0 | Acompanhar o jogo |
| 1.26.0 | Objetos dos mods e os nomes deles |
| 1.27.0 | Modelos dos mods |
| 1.35.0 | Plugins dos próprios mods avisados do que foi aplicado |
| 1.40.0 | Movimentos que mantêm o mesmo objeto |
| 1.45.3 | Uma aplicação cuja resposta se perdeu (um túnel caído) não criar objetos duas vezes quando reenviada |
| 1.54.0 | Os arquivos dos mods Expand World e um mundo Riverheim lidos do jogo |
| 1.57.3 | Criaturas, navios e outras coisas que se movem acompanhadas enquanto se movem |
| 1.97.0 | Objetos alterados que mantêm a identidade |

## Relacionados

- [Editar o mundo em que você está jogando](guides/edit-your-game-live.md)
- [Editar um servidor dedicado enquanto as pessoas jogam](guides/edit-a-server-live.md)
- [Edição ao vivo](concepts/live-editing.md): como se conecta, o que é enviado, vários editores e jogadores
- [Solução de problemas](help/troubleshooting.md#um-servidor-não-se-conecta): quando não conecta
