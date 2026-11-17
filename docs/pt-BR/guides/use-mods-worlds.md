<!-- source: 1000c3db -->
# Editar um mundo criado com Expand World, Riverheim ou Tunnelheim

Instale o complemento do editor para o seu mod, para que um mundo criado com ele abra, seja desenhado e salvo como o mod
o cria.

**Você precisa de:** o mod instalado numa pasta do BepInEx neste computador (a do seu Valheim, um perfil de gerenciador
de mods ou uma cópia da pasta `BepInEx` de um servidor) e o complemento da mesma versão do editor.

| Complemento | Para | O que ele acrescenta |
|---|---|---|
| [Expand World](../../../addons/ExpandWorld/README.md) | Expand World Size, Rivers e Data | Terreno, mapa, biomas, água, locais, vegetação e as previsões de Mundo novo como os mods os criam |
| [Railroads](../../../addons/Railroads/README.md) | Railroads (`Railroads.dll`) | Desenha os trilhos, as estações e os trens do mod com os próprios modelos dele; as peças que você coloca, cola, move ou gira funcionam no jogo, também ao vivo |
| [Riverheim](../../../addons/Riverheim/README.md) | Riverheim (`Riverheim.dll`) | Abre mundos do Riverheim (recusados sem ele), desenhados com a sua própria cópia do mod; o Riverheim como gerador de Mundo novo |
| [Tunnelheim](../../../addons/Tunnelheim/README.md) | Tunnelheim (`ValheimTunneling.dll`) | Desenha os túneis do mod e uma ferramenta **Tunnel** (escavações, Add Earth, perfurações, salas, poços, escadas); os túneis vão junto com cópias e projetos |
| [ValheimRAFT](../../../addons/ValheimRAFT/README.md) | ValheimRAFT (`ValheimRAFT.dll`) | Desenha os navios e veículos terrestres do mod inteiros, com velas e cordas; cada um se move, é copiado e excluído como um só |

1. Feche o editor.
2. Na [página de versões](https://github.com/Shuiei/valheim-world-editor/releases/latest) do editor (ou no Nexus Mods),
   baixe `ValheimWorldEditor-addon-<Name>-<version>.zip`.
3. Descompacte-o na pasta `addons` ao lado do editor (no macOS, ao lado de `ValheimWorldEditor.app`), de modo que você
   tenha `addons/<Name>/worldeditor.json`.
4. Inicie o editor e abra **Configurações** na página inicial.
5. Em **Complementos**, encontre o complemento. Ele deve dizer "Em execução." (ou "Ainda não lido" até um mundo abrir).
   Se disser "Aguardando o mod: … não está nas pastas de plugins acima.", adicione a pasta do BepInEx que contém o mod
   em **Pastas do BepInEx** e pressione **Salvar**.
6. Para o mundo de um servidor aberto a partir dos arquivos dele, adicione uma cópia da pasta `BepInEx` do servidor em
   **Pastas do BepInEx**, para que o editor leia as configurações dos mods do servidor.
7. Abra o mundo em **Um mundo salvo**. O cartão dele mostra o mod (por exemplo **Riverheim** ou "Expand World").
8. Edite e salve como de costume. Com o Tunnelheim, escolha **Tunnel** em **Mods** na barra de ferramentas, escolha uma
   ação e clique no terreno ou na rocha.

## Verificar se funcionou

- O mundo abre, e o terreno e o mapa dele aparecem como no jogo (os rios do Riverheim, o tamanho e os biomas do Expand
  World).
- O log do editor (**Abrir log** na página inicial) diz o que o complemento encontrou e leu.
- Um cartão de mundo que continua acinzentado diz por quê: veja [Um mundo não abre ou está acinzentado](../help/troubleshooting.md#um-mundo-não-abre-ou-está-acinzentado).

## Desfazer

Cada edição se desfaz como qualquer outra (`Ctrl+Z`, Histórico). Para deixar de usar um complemento, desligue o
interruptor dele em **Configurações → Complementos** (a pasta dele fica) ou exclua a pasta dele de `addons`. Com ele
desligado, os mundos do Expand World são gerados como o jogo faz sem os mods, e os mundos do Riverheim são recusados.

## É bom saber

- Um complemento só é usado enquanto a DLL do mod dele estiver numa pasta de plugins que o editor conhece; um mod que um
  gerenciador de mods desligou, ou configurações que um mod removido deixou para trás, não contam.
- As peças do Railroads são desenhadas a partir dos arquivos de modelo que o mod guarda na pasta `meshes`: mantenha essa
  pasta ao lado de `Railroads.dll`, como o mod a instala.
- Os arquivos de dados do Expand World precisam da biblioteca YamlDotNet que o mod instala em `BepInEx/plugins`; sem
  ela, eles não são lidos, e o log diz isso.
- O seu Riverheim precisa conhecer a predefinição do mundo (atualize-o se for mais antigo que o que criou o mundo). O
  Riverheim 1.3 e posteriores precisam do Worldhalla ao lado, como no jogo. Mundos criados com o beta descontinuado do
  Riverheim não podem ser abertos. Ao vivo, o seu Riverheim precisa ser a versão de que os jogadores que entram no jogo
  precisam (a mesma versão desde a 1.3, exatamente a mesma publicação antes disso).
- Um mundo do Riverheim precisa do Riverheim no jogo de cada jogador, então jogadores de console via crossplay não podem
  entrar nele.
- Ao vivo, os arquivos do Expand World são lidos do próprio jogo pelo plugin: veja [Modo ao vivo](../live-mode.md).

## Relacionados

- [Um mundo novo](../new-world.md): Riverheim e Expand World ao criar um mundo
- [Modo ao vivo](../live-mode.md): jogos com mods, ao vivo
- [Abrir um mundo que não está na lista](open-a-world.md)
