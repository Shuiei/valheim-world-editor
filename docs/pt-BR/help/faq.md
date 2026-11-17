<!-- source: 93850bba -->
# Perguntas

Respostas curtas ao que mais se pergunta, cada uma com um link para a resposta completa.

## É seguro para o meu mundo?

Sim, se você guardar uma cópia. Nada é gravado até você pressionar **Salvar no mundo** ou **Aplicar ao vivo**, e cada
salvamento é relido e verificado: se não for relido corretamente, os arquivos novos são removidos e o mundo fica como
estava. Mesmo assim, copie um mundo que seja importante para você antes do primeiro salvamento: veja
[Fazer backup de um mundo antes de editar](../guides/back-up-a-world.md) e
[O que o editor grava](../concepts/safety.md#o-que-o-editor-grava).

## Posso desfazer depois de salvar?

Sim. O histórico é guardado com o mundo depois de cada salvamento ou aplicação, mesmo depois de você fechar o editor:
desfaça um passo (ou use **Remover** nele no Histórico) e salve de novo. Veja
[Desfazer um passo, reverter uma alteração ou descartar tudo](../guides/undo-changes.md). As redefinições de zona e o
terreno com Sem limite também o mantêm; o que uma redefinição criou de novo fica fora do alcance dele (veja
[O que um passo não consegue reverter](../concepts/saving-and-history.md#o-que-um-passo-não-consegue-reverter)).

Para danos descobertos mais tarde, [Restaurar uma área a partir de um backup](../guides/restore-from-backup.md).

## Os jogadores do meu servidor precisam de alguma coisa?

Não. Só o jogo ou servidor que hospeda o mundo precisa do BepInEx e do plugin, e só para a edição ao vivo. Os jogadores
que entram não precisam de nada, inclusive para o terreno elevado além do limite de 8 m do jogo. Veja
[Limites do terreno](../concepts/ground-limits.md#sem-limite).

## Jogadores de console via crossplay podem entrar num mundo editado?

Sim: um mundo editado continua sendo um mundo normal do Valheim, e o terreno do editor além de 8 m não precisa de mod.
As exceções são os mundos que precisam eles mesmos de um mod: um mundo criado com o Riverheim, ou um servidor que
executa o Homestead, recusa jogadores sem o mod, e jogadores de console não podem instalar mods. Veja
[Um mundo novo](../new-world.md#dicas-e-limites).

## Posso editar com o jogo em execução?

Sim, no modo ao vivo, com o plugin WorldEditorBridge no jogo ou no servidor: veja
[Editar o mundo em que você está jogando](../guides/edit-your-game-live.md) ou
[Editar um servidor dedicado enquanto as pessoas jogam](../guides/edit-a-server-live.md). Para editar os arquivos de um
mundo salvo, feche o jogo ou pare o servidor antes: um jogo em execução salva por cima do que o editor grava. Veja
[O jogo salvou enquanto você editava](troubleshooting.md#o-jogo-salvou-enquanto-você-editava).

## Posso editar um servidor que alugo de uma hospedagem de jogos?

Sim, mas ao vivo só se a hospedagem lhe der acesso SSH à máquina (um login de shell, não só SFTP ou um gerenciador de
arquivos): pergunte à hospedagem antes de alugar. A maioria dos servidores de jogo alugados oferece só um painel web e
FTP, então o editor não consegue alcançar o plugin: pare o servidor, baixe o mundo, edite-o e envie-o de volta. Um
servidor que um painel de jogos como o Pterodactyl executa num contêiner na sua própria máquina pode ser editado ao
vivo, por um túnel que você mesmo abre. Veja [Editar um servidor alugado ou hospedado num painel](../guides/edit-a-rented-server.md).

## O editor pede um token. Onde ele está?

Em `BepInEx/config/Tie.WorldEditorBridge.cfg` do jogo ou do servidor, na linha `Token`. O plugin grava esse arquivo na
primeira vez que o jogo inicia com ele. Para o seu próprio jogo, o editor o lê sozinho; para um servidor, você o copia
do servidor. Veja [Configurações do plugin](../live-mode.md#configurações-do-plugin).

## Funciona com mods?

Sim. Num jogo com mods ao vivo, o editor aprende os objetos dos mods e desenha os modelos deles (veja
[Jogos com mods](../live-mode.md#jogos-com-mods)). Mods que mudam a forma como os mundos são criados precisam do
complemento do editor para eles: Expand World, Riverheim e Tunnelheim têm um. Veja
[Editar um mundo criado com Expand World, Riverheim ou Tunnelheim](../guides/use-mods-worlds.md).

## Eu jogo com os mods Expand World. O editor sabe disso?

Sim, com o complemento Expand World instalado: o terreno, o mapa, os biomas e as previsões seguem as configurações e
os arquivos de dados dos mods. Sem ele, cada mundo é gerado como o próprio jogo faz. Veja
[Editar um mundo criado com Expand World, Riverheim ou Tunnelheim](../guides/use-mods-worlds.md).

## Meu mundo foi criado com o Riverheim. Posso abri-lo?

Sim, com o complemento Riverheim instalado e o próprio Riverheim numa pasta do BepInEx que o editor conhece: o editor
desenha o mundo com a sua própria cópia do mod. Veja [Editar um mundo criado com Expand World, Riverheim ou Tunnelheim](../guides/use-mods-worlds.md).

## Preciso do Homestead?

Só para construir os projetos do editor no jogo. A Oficina, a biblioteca e colar projetos num mundo funcionam sem ele;
quando ele falta, a página inicial avisa. Observe que um servidor que executa o Homestead recusa jogadores que não o
têm. Veja [A Oficina](../workshop.md).

## Onde eu o baixo? Ele está no Thunderstore, no Hexium ou no Nexus Mods?

- **O editor** é um programa, não um mod: baixe-o da
  [página de versões](https://github.com/Shuiei/valheim-world-editor/releases/latest) ou do
  [Nexus Mods](https://www.nexusmods.com/valheim/mods/4348).
- **O plugin WorldEditorBridge** (para a edição ao vivo) está na página de versões, no Nexus Mods, no
  [Thunderstore](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) e no
  [Hexium](https://valheim.hexium.gg/mods/Tie/WorldEditorBridge), para gerenciadores de mods.
- Os **complementos** para mods estão na página de versões e no Nexus Mods.

Use o plugin e os complementos da mesma versão do editor. Veja [Instalar](../start/install.md).

## Ele envia alguma coisa pela internet?

Não. Tudo acontece no seu computador; um servidor dedicado só é alcançado pelo próprio túnel SSH criptografado do
editor, com um token que você digita. Veja [Nada sai do seu computador](../concepts/safety.md#nada-sai-do-seu-computador).

## É seguro deixar o Claude ou um script alterar o meu mundo?

O que o Claude altera fica pendente até você salvar, e ele só pode se conectar a partir deste computador, com um token.
Scripts são programas executados com as suas permissões, então o Claude só pode executá-los se você permitir. Veja
[Scripts e o Claude](../concepts/safety.md#scripts-e-o-claude).

## Algo não funciona. Onde devo procurar?

Comece por [Solução de problemas](troubleshooting.md). Para relatar um bug, anexe o log do editor:
[O log e os relatórios de bugs](troubleshooting.md#o-log-e-os-relatórios-de-bugs).
