<!-- source: 4978ef09 -->
# Segurança e privacidade

O editor é um programa no seu computador. Ele só grava nos seus mundos quando você clica em **Salvar no mundo**, não
envia nada a lugar nenhum a menos que você o conecte a um servidor ou ao Claude, e mantém segredos fora do log. Esta
página diz o que ele grava, o que pode sair do seu computador e o que roda com as suas permissões.

## O que o editor grava

**Seus mundos**, só quando você salva. Até lá, cada alteração fica pendente e vive na memória do editor (veja
[Alterações pendentes](saving-and-history.md#alterações-pendentes)). Um salvamento acrescenta um novo salvamento numerado à
pasta do mundo e não remove nada antigo até ter relido o novo corretamente (veja
[Salvar um mundo](saving-and-history.md#salvar-um-mundo)). Um mundo novo vai para uma pasta nova, nunca por cima de um
mundo com o mesmo nome. Feche o jogo antes: um jogo em execução grava por cima dos arquivos que o editor salvou.

**Um jogo ao vivo**, só quando você aplica. O jogo então salva as alterações ele mesmo, como salva o que os jogadores
constroem.

**Seus projetos**: um arquivo `.blueprint` e a imagem `.png` dele na pasta do Homestead, dentro da pasta de salvamentos
do Valheim, quando você salva um projeto. **Excluir** remove os dois, depois de perguntar.

**A própria pasta de dados**: `~/.local/share/ValheimWorldEditor` no Linux, `%LOCALAPPDATA%\ValheimWorldEditor` no
Windows, `~/Library/Application Support/ValheimWorldEditor` no macOS (a parte de baixo do menu da página inicial a
mostra). Ela contém:

| O quê | Arquivos |
|---|---|
| Suas configurações e escolhas | `settings.json`, `prefs.json`, `place.json` (predefinições de Colocar, favoritos, tipos recentes), `stamps.json`, `selections.json` |
| Servidores salvos | `servers.cfg`: endereço, usuário, token, a identidade do servidor e a senha quando você marcou **Salvar senha** (veja abaixo) |
| Histórico | `history/`, um arquivo por mundo (veja [Histórico](saving-and-history.md#histórico)) |
| Seu trabalho | `scripts/` (scripts salvos), `heightmaps/` (mapas de altura exportados), `blueprints/export` (arquivos `.vbuild`) |
| O que ele aprendeu com o seu jogo | `game-index.json` e outros arquivos `game-*.json` (onde as coisas estão nos arquivos do Valheim), `mod-catalogue.json` (objetos de mods vistos num jogo ao vivo), `mod-bundles/` (modelos dos mods, copiados das DLLs deles) |
| O log | `ValheimWorldEditor.log`, um novo a cada execução; `perf-native.log` só enquanto **Registrar taxa de quadros** está ligado |

**O que ele nunca altera.** O editor lê a pasta do seu jogo Valheim, as suas pastas do BepInEx e os arquivos dos seus
mods (modelos, texturas, as regras do jogo, as configurações do plugin) e não grava nada ali. Ele lê o código dos mods
para aprender os nomes dos valores deles, e nunca o executa.

**Senhas.** Uma senha salva fica guardada como texto simples em `servers.cfg`. No Linux, só você pode ler o arquivo;
mesmo assim, prefira uma chave SSH sempre que puder. A senha de uma chave nunca é salva. **esquecer**, ao lado de um
servidor salvo, o remove, e junto a senha e o token dele.

## Nada sai do seu computador

O editor não tem conta, nem rastreamento, nem verificação de atualizações. Ele só se comunica com o exterior nos casos
abaixo, cada um deles escolha sua:

| Quando | O que vai para onde |
|---|---|
| Você se conecta a um **servidor dedicado** | Uma conexão SSH, criptografada, só para esse servidor, com a conta que você informa. O plugin é alcançado por ela, nunca pela internet aberta (veja [Como ele se conecta](live-editing.md#como-ele-se-conecta)). |
| Você edita **o seu próprio jogo** ao vivo | Nada sai do computador: o editor fala com o plugin em `127.0.0.1`. |
| Você conecta o **Claude** | O aplicativo do Claude no seu computador fala com o editor em `127.0.0.1`. O que o editor responde (os dados da área, imagens da visão 3D) vai para o Claude, como tudo o que você dá ao Claude. |
| Você clica num link | **Obter o Homestead**, ou uma página de ajuda aberta no GitHub, abre no seu navegador. |

**O plugin continua privado.** O WorldEditorBridge só escuta no computador em que roda, e só responde a pedidos que
trazem o token secreto dele. Para um servidor, você mesmo digita o token; o editor nunca o lê do servidor, então um
login SSH sozinho não consegue alterar o mundo. Mantenha o token em segredo: qualquer pessoa com ele e com acesso à
porta pode alterar o mundo. Esvazie a linha `Token` de `Tie.WorldEditorBridge.cfg` e reinicie o jogo para obter um
novo.

**A identidade do servidor é verificada.** O editor memoriza a identidade de um servidor na primeira conexão. Se ela
algum dia mudar, o editor se recusa a conectar, porque alguém poderia estar se passando pelo seu servidor. Depois de
reinstalar um servidor, esvazie a linha `HostKey` dele em `servers.cfg` para aceitar a nova.

## Scripts e o Claude

**Scripts são programas.** Um [script](../scripting.md) é código C#, e o editor o executa com as suas permissões, como
qualquer programa que você inicia. Ele roda num processo separado para que **Parar** sempre consiga encerrá-lo, mas
esse processo não é uma área isolada: um script pode ler e gravar os seus arquivos e fazer qualquer outra coisa que a
sua conta pode. Execute só scripts que você escreveu ou em que confia, como faria com um mod.

**O Claude trabalha para você, dentro de limites.** Com **Permitir que o Claude se conecte** ligado em Configurações,
o [Claude](../claude.md) pode usar as ferramentas do editor. O editor o mantém dentro disto:

- **Só este computador, com um token.** O editor só escuta em `127.0.0.1`, recusa pedidos endereçados a outro nome
  (assim uma página da web não consegue alcançá-lo com um endereço inventado) e recusa qualquer pedido sem o token
  dele (48 caracteres aleatórios). Outro **Endereço** em Configurações (`0.0.0.0`, ou um dos deste computador) deixa
  outros computadores dessa rede se conectarem, com o token, que então trafega sem criptografia: só computadores com
  um endereço local na própria rede deste computador, nunca os da internet, e nunca scripts (eles continuam exclusivos
  do Claude deste computador). **Novo token** gera outro; desligue o interruptor e nada consegue se conectar.
- **Só pendente.** Tudo o que o Claude altera fica pendente, um passo por alteração, com a marca **Claude** no
  Histórico. O Claude nunca salva, aplica ao vivo, descarta, recarrega, grava nem exclui os seus arquivos, nem se
  conecta a um jogo ao vivo: essas ações continuam sendo suas. Ele não abre outro mundo enquanto o seu tem alterações
  não salvas.
- **Nada ao vivo com Auto ligado.** Enquanto um mundo ao vivo está com **Auto** ligado, cada alteração chegaria aos
  jogadores na hora, então o Claude não altera nada e pede que você desligue o Auto.
- **Nenhum script sem a sua permissão.** Os scripts do Claude são recusados até você ligar **O Claude pode executar
  scripts** em Configurações (desligado por padrão). Mesmo assim, cada script que o Claude escreve é mostrado a você
  antes de rodar, para executar ou recusar: um texto num mundo (uma placa, uma tag escrita por outro jogador) poderia
  convencer o Claude a escrever um script nocivo. As outras ferramentas dele executam o próprio código do editor e não
  pedem confirmação.
- **Fácil de reverter.** **Reverter as alterações do Claude**, no Histórico, remove cada passo que o Claude fez na área
  aberta e mantém os seus (veja [Histórico](saving-and-history.md#histórico)).

## Extensões e complementos de mods

Um mod pode trazer uma **extensão do editor**: um pequeno trecho de código que o editor carrega para ler e desenhar os
objetos desse mod. Como um script, ela roda com as suas permissões, então a decisão é sua. Cada extensão fica
**desligada** até você ligá-la em **Configurações → Extensões de mods**, e só aquele arquivo exato: o editor memoriza a
impressão digital dele (SHA-256), então um arquivo alterado, como uma atualização do mod, fica desligado de novo até
você ligá-lo de novo.

**Complementos**, que você mesmo baixa e descompacta na pasta `addons` do editor, rodam como foram instalados, já que
instalar um já é escolha sua. **Configurações → Complementos** desliga qualquer um deles.

## O que fica fora do log

O editor grava o que faz, e cada erro que trata, em `ValheimWorldEditor.log`. **Abrir log**, na parte de baixo do menu
da página inicial, o abre, para que você possa anexá-lo a um relato de problema. Antes de uma linha ser gravada, os
dados pessoais são retirados:

- a sua pasta pessoal é escrita `~`, e outros caminhos de arquivo, endereços da web, nomes `user@host`, endereços IP e
  de servidor e o nome da sua conta são substituídos por marcadores como `<path>`;
- as contas, senhas, senhas de chave e tokens dos servidores salvos nunca chegam a ele.

O plugin WorldEditorBridge grava no próprio `BepInEx/LogOutput.log` do jogo, com a pasta pessoal do servidor também
escrita `~`.

Mesmo assim, leia um log antes de publicá-lo. E guarde suas próprias cópias dos mundos que são importantes para você
(veja [Fazer backup de um mundo antes de editar](../guides/back-up-a-world.md)).
