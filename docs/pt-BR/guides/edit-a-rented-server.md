<!-- source: 94a46b5b -->
# Editar um servidor alugado ou hospedado num painel

Edite o mundo de um servidor que você aluga de uma hospedagem de jogos, ou de um que um painel de jogos como o
Pterodactyl ou o Pelican roda num contêiner na sua própria máquina.

**Editar ao vivo exige acesso SSH à máquina que roda o servidor**: um login no shell dela, não só SFTP ou um
gerenciador de arquivos. O editor alcança o plugin por SSH, no próprio `127.0.0.1` do servidor (veja
[Editar um servidor dedicado enquanto as pessoas jogam](edit-a-server-live.md)). Antes de alugar um servidor para
editá-lo ao vivo, pergunte à hospedagem se o plano inclui acesso SSH. Dois tipos de servidor não se encaixam nisso:

- **Um servidor de jogo alugado de uma hospedagem** (você recebe um painel web e um gerenciador de arquivos ou
  FTP/SFTP, mas nenhum acesso SSH à máquina): o editor não consegue alcançar o plugin ali. Edite os arquivos do mundo
  em vez disso: veja [Um servidor de jogo alugado](#um-servidor-de-jogo-alugado).
- **Um painel de jogos na sua própria máquina** (Pterodactyl, Pelican, ou qualquer servidor rodando num contêiner
  Docker): editar ao vivo funciona, por um túnel que você mesmo abre. Veja
  [Um servidor num contêiner](#um-servidor-num-contêiner).

Se a sua hospedagem lhe dá um login SSH de verdade na máquina que roda o jogo (uma VPS alugada ou uma máquina dedicada,
não só um servidor de jogo), siga [Editar um servidor dedicado enquanto as pessoas jogam](edit-a-server-live.md) em vez
disso.

## Um servidor de jogo alugado

**Você precisa de:** o painel da hospedagem ou acesso aos arquivos (o gerenciador de arquivos dela, FTP ou SFTP) e
permissão para parar e iniciar o servidor.

O modo ao vivo não é possível: o plugin só responde na própria máquina, e a hospedagem não dá nenhum acesso a ela. O
login SFTP de um painel (muitas vezes na porta 2022) só entrega arquivos: o SSH do editor não consegue abrir um túnel
por ele. Edite o mundo salvo em vez disso:

1. No painel da hospedagem, use **Parar** no servidor. Um servidor em execução salva por cima dos arquivos que você
   devolver.
2. Com o gerenciador de arquivos do painel ou um programa de SFTP (FileZilla, WinSCP), baixe a pasta do mundo,
   `worlds_local/<World>` na pasta de salvamento do servidor. Guarde uma cópia dela intocada: é o seu backup.
3. No editor, abra essa pasta: **Um mundo salvo** → **Outra pasta de mundo** → **Procurar…** (veja
   [Abrir um mundo que não está na lista](open-a-world.md)).
4. Faça as suas alterações e pressione **Salvar no mundo**.
5. Envie a pasta de volta para o mesmo lugar, substituindo os arquivos que estão lá, e depois inicie o servidor.

O servidor precisa ficar parado do passo 1 ao passo 5: o que os jogadores fizerem nesse meio-tempo se perde quando você
enviar os arquivos.

## Um servidor num contêiner

**Você precisa de:** um login SSH na máquina em que o painel roda (a sua VPS, como root ou como um usuário com
permissão para executar `docker`) e permissão para editar os arquivos do servidor de jogo no painel.

Um painel roda cada servidor de jogo no próprio contêiner Docker, que tem o próprio `127.0.0.1`. O plugin escuta ali,
dentro do contêiner, enquanto o túnel do editor alcança o `127.0.0.1` da máquina, onde nada responde: o editor diz que
o plugin não responde, embora o servidor rode com ele. A saída é um túnel até o endereço do próprio contêiner:

```text
 seu computador              sua VPS           o contêiner do servidor
┌───────────────────────┐   ┌───────────┐     ┌───────────────────────┐
│ editor                │SSH│           │     │ WorldEditorBridge     │
│ 127.0.0.1:15182 ══════╪═══╪═► sshd ───┼────►│ 172.18.0.x:5182       │
└───────────────────────┘   └───────────┘     └───────────────────────┘
```

1. No painel, instale o BepInEx e o WorldEditorBridge no servidor, como em qualquer servidor dedicado (passos 1 a 3 de
   [Editar um servidor dedicado enquanto as pessoas jogam](edit-a-server-live.md)), e inicie-o uma vez.
2. No gerenciador de arquivos do painel, abra `BepInEx/config/Tie.WorldEditorBridge.cfg`. Defina `BindAddress = +` e
   copie o valor da linha `Token`.
3. Reinicie o servidor. O console dele ou `BepInEx/LogOutput.log` diz `WorldEditorBridge <version> listening on
   http://+:5182/`, e depois um aviso de que o token trafega sem criptografia: dentro do contêiner, isso é só a rede do
   próprio painel.
4. Entre na VPS por SSH e encontre o endereço do contêiner. O contêiner tem o nome do id do servidor (o painel o mostra
   nas configurações do servidor); `docker ps` lista todos eles:

   ```sh
   docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' <server id>
   ```

   Ele responde com um endereço como `172.18.0.5`.
5. No seu computador, abra um túnel até ele e deixe-o aberto enquanto edita (substitua o endereço e o login):

   ```sh
   ssh -N -L 15182:172.18.0.5:5182 user@your-vps
   ```

   Com o PuTTY: **Connection** → **SSH** → **Tunnels**, **Source port** `15182`, **Destination** `172.18.0.5:5182`,
   **Add**, e depois abra a sessão.
6. Na página inicial do editor: **Um servidor dedicado** → **Mais opções** → **Criei meu próprio túnel**. Digite
   `127.0.0.1:15182` em **Endereço do bridge**, cole o token e pressione **Conectar**.

Depois edite como em qualquer servidor: veja [Modo ao vivo](../live-mode.md#como-usar).

**Tenha em mente:**

- `+`, e não o endereço do contêiner nem `0.0.0.0`: com o endereço do contêiner, o plugin recusa todo pedido que chega
  por um túnel, e com `0.0.0.0`, todos os pedidos ("The server answered with an error (400)", o servidor respondeu com um erro).
- Não adicione a porta do plugin (5182) às portas do servidor (alocações) no painel: isso a abre para a internet.
- O endereço do contêiner pode mudar quando o servidor é reconstruído ou reinstalado. Se o túnel parar de responder,
  procure-o de novo (passo 4).
- Mantenha `BindAddress = 127.0.0.1` num servidor que não roda num contêiner: ali, `+` faz o plugin escutar em todas as
  redes em que a máquina está (os pedidos vindos da internet continuam sendo recusados).

## Verificar se funcionou

- **Um servidor de jogo alugado:** o servidor inicia, e as suas alterações estão lá no jogo.
- **Um servidor num contêiner:** a barra superior mostra **AO VIVO**, e depois de **Aplicar ao vivo**, "Tudo aplicado".

Se o editor mostrar uma mensagem em vez disso, veja [Um servidor não se conecta](../help/troubleshooting.md#um-servidor-não-se-conecta).

## Desfazer

- **Um servidor de jogo alugado:** pare o servidor e envie a cópia que você guardou no passo 2.
- **Um servidor num contêiner:** `Ctrl+Z` e depois **Aplicar ao vivo** de novo. Para fechar o plugin de novo, defina
  `BindAddress = 127.0.0.1` e reinicie o servidor.

## Relacionados

- [Editar um servidor dedicado enquanto as pessoas jogam](edit-a-server-live.md)
- [Configurações do plugin](../live-mode.md#configurações-do-plugin): `BindAddress`, `Port`, `Token`
- [Abrir um mundo que não está na lista](open-a-world.md)
- [Fazer backup de um mundo antes de editar](back-up-a-world.md)
