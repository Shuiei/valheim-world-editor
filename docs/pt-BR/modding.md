<!-- source: 1ccdce30 -->
# Para autores de mods

O editor funciona com mods de construção sem nada da parte dos autores: conectado a um jogo com
mods, ele aprende os objetos dos mods, desenha-os com os próprios modelos e dá nome aos dados que eles guardam.
Esta página é para os autores de mods que querem mais: um arquivo `worldeditor.json` opcional para o que o editor
não consegue adivinhar, **objetos vinculados** para mods que constroem uma coisa a partir de vários objetos, **extensões
do editor** (uma pequena DLL compilada com o SDK do editor) para o que um arquivo não consegue dizer, **complementos**
(o mesmo, feito por qualquer pessoa para o mod de outra) e **ganchos do bridge** para que o próprio plugin de um mod
participe do modo ao vivo.

Tudo aqui é opcional, e cada etapa se soma à anterior. O editor não traz nada para nenhum
mod: dar suporte a um mod no editor cabe ao autor dele, com estas ferramentas. Os exemplos desta página
são os de um mod inventado, "Example Vehicles", cuja amostra completa vem com o SDK (veja
[A amostra Example Vehicles](#a-amostra-example-vehicles)).

## O que o editor aprende sozinho

| O quê | Como o editor aprende |
|---|---|
| Os objetos dos mods: nomes, peças de construção, custos, estações de trabalho, a ferramenta que constrói cada um | Conectado a um jogo (modo ao vivo, WorldEditorBridge 1.26.0 ou mais novo), o editor pede ao jogo todos os objetos que ele conhece e guarda os que não são do próprio Valheim. Eles são guardados em `mod-catalogue.json` na pasta de dados do editor, então o mundo de um servidor com mods aberto mais tarde a partir dos arquivos, offline, também os mostra. Um relato mais novo de um mod sobre uma peça substitui o mais antigo; um mod nunca altera um dos objetos do próprio Valheim. |
| Como eles são | O jogo informa o que desenha de cada objeto de mod (WorldEditorBridge 1.27.0 ou mais novo); o editor encontra essas malhas, materiais e texturas nos próprios arquivos dos mods no computador do jogador (os asset bundles deles, copiados das DLLs uma vez para `mod-bundles` na pasta de dados). As malhas que um mod gera em código enquanto o jogo roda não podem ser encontradas: esses objetos aparecem sem textura, a menos que uma [extensão](#extensões) os desenhe. |
| Os nomes dos dados que os mods guardam nos objetos | O jogo salva só um hash de cada chave de dados. O editor encontra os nomes entre os textos escritos nas DLLs dos mods (lidas como bytes, nunca executadas) nas pastas de plugins que conhece, e os guarda em `mod-zdo-keys.json` até uma DLL mudar. Uma chave cujo nome é montado enquanto o jogo roda (`"Corner" + i`) não pode ser encontrada assim: dê o nome dela em `data` no seu `worldeditor.json` (abaixo), senão o Inspetor a mostra como `#` e o hash. |
| De que mod vem um objeto | O Inspetor diz (com o Jötunn instalado, que sabe; senão "De um mod"). O painel [Construir](build.md), num mundo e na Oficina, lista as peças dos mods na aba **Peças de mods**, um mod de cada vez. |

Veja [Jogos com mods](live-mode.md#jogos-com-mods) para o lado do jogador.

## worldeditor.json

Algumas coisas que o editor não consegue adivinhar. Um mod pode informá-las num arquivo `worldeditor.json` distribuído
com ele, em qualquer lugar da pasta dele em `BepInEx/plugins` (ao lado da DLL serve). Este é o arquivo do Example
Vehicles:

```json
{
  "$schema": "https://raw.githubusercontent.com/Shuiei/valheim-world-editor/main/docs/worldeditor.schema.json",
  "version": 1,
  "extension": "ExampleVehicles.WorldEditor.dll",
  "gameComponents": ["Ship"],
  "data": {
    "ExampleVehicles_Vehicle": { "label": "Vehicle", "readOnly": true, "help": "The vehicle this part is built on." },
    "ExampleVehicles_Offset": { "label": "Place on the vehicle", "readOnly": true },
    "ExampleVehicles_Packed": { "label": "Linked objects", "help": "The objects this one is tied to." }
  },
  "prefabs": {
    "example_boundary": { "hidden": true },
    "example_fleet": { "hidden": true, "data": true },
    "example_canal": { "hidden": true, "data": true },
    "example_hull": { "group": "Hulls" },
    "example_sail": { "group": "Sails and cables" },
    "example_post": { "group": "Sails and cables" },
    "example_pit": { "group": "Digging" }
  }
}
```

| Chave | O que o editor faz com ela |
|---|---|
| `version` | A versão do formato: `1`. Um arquivo de uma versão posterior, ou sem versão, é ignorado por inteiro. |
| `data` | Por chave de dados, como o seu código a nomeia (o nome cujo hash o jogo salva; uma chave nomeada aqui é conhecida pelo nome mesmo quando a sua DLL não a contém como texto): `label`, como o [Inspetor](select.md#inspetor) nomeia o valor (senão, a chave; os nomes próprios do editor para as chaves do jogo vêm primeiro); `readOnly`, o Inspetor mostra o valor mas não deixa alterá-lo (valores que o seu mod define sozinho e que quebrariam se editados); `help`, o que ele é, mostrado ao apontar para o valor. |
| `prefabs` | Por nome de prefab: `hidden`, não oferecido na lista de [Colocar](place.md) nem no painel [Construir](build.md) (as suas ferramentas, efeitos, auxiliares; os objetos dele que já estão num mundo continuam aparecendo); `group`, o título sob o qual a sua peça é listada na aba **Peças de mods** do painel Construir, depois do nome do mod ("*nome do mod* · Hulls"). Sem um grupo, uma peça é listada sob a estação de trabalho dela, senão sob "Nenhuma estação necessária". `group` só vale para as peças dos próprios mods. `data`: os objetos dele só guardam os dados do seu mod (um registro, o que foi cavado numa área) e não têm nada para ver: o editor nunca os desenha, escolhe nem lista numa área (a sua extensão continua a lê-los), e uma [redefinição de zona](area.md) os mantém quando mantém as construções, e os remove com todo o resto caso contrário. `piece`: uma peça de construção que o seu mod gera em código, que nenhum bundle contém: `{ "name": "Track 2 m", "cost": { "Stone": 4 }, "station": "Stonecutter" }` (`name` obrigatório, no máximo 64 caracteres; `cost` por nome de prefab de item, no máximo 16 itens; `station` pelo nome em inglês). A aba **Peças de mods** do painel Construir a lista sob o seu mod mesmo sem um jogo ao vivo (o que um jogo ao vivo diz sobre ela prevalece). As peças nos seus bundles não precisam disso: o editor as encontra pelo `Piece` delas, ou pelos pontos de encaixe quando você adiciona o `Piece` em código. |
| `links` | Como os seus objetos se ligam num só: veja [Objetos vinculados](#objetos-vinculados). |
| `ties` | Como os seus objetos são atados a um que os nomeia (os vagões de um trem): veja [Objetos atados](#objetos-atados). |
| `extension` | A DLL da sua extensão do editor, relativa à pasta do arquivo e dentro dela: veja [Extensões](#extensões). |
| `gameComponents` | Nomes de classe dos scripts do próprio jogo cujos campos a sua extensão precisa (`"TerrainOp"`, `"Ship"`; no máximo 32): enquanto a sua extensão roda, o editor lê todos os componentes deles nos prefabs do jogo a partir do Valheim do jogador (uma vez, e depois os guarda até o jogo mudar) e os entrega a ela como `Host.Game` (veja [Ganchos](#ganchos)). |
| `settingsFiles` | Nomes dos arquivos de configuração BepInEx do seu mod que a sua extensão lê (`"author.mod.cfg"`, em `BepInEx/config`; no máximo 8): quando um mundo abre, o editor os lê na primeira das pastas BepInEx do jogador que tiver cada um e, ao vivo, nas do próprio jogo conectado, por meio do bridge dele (as de um servidor, cujas configurações sincronizadas os jogadores recebem). A sua extensão lê uma configuração com `Host.Setting(file, section, key)` (veja [Ganchos](#ganchos)). |
| `worldGenerators` | Os geradores de mundo que a sua extensão registra (veja [Geradores de mundo](#geradores-de-mundo)), pelo id que os arquivos dos mundos deles informam, cada um com o nome do seu mod para o jogador: `{ "author.mod": "Some Mod" }` (no máximo 8). O editor o lê mesmo enquanto a sua extensão não roda, para que um mundo feito pelo seu mod diga por que não pode abrir: o complemento desligado, esperando o seu mod, fora de execução, ou a extensão do seu mod não ativada. |

### Como o editor o lê

- **Onde.** Em todas as pastas `plugins` que o editor conhece: a `BepInEx` da pasta do Valheim, o perfil do
  gerenciador de mods em que o próprio editor foi instalado, as pastas adicionadas em **Configurações → Pastas do
  BepInEx**, e todos os perfis do r2modman e do Thunderstore Mod Manager. Todo arquivo chamado
  `worldeditor.json` ali conta, em qualquer profundidade.
- **Quando.** Cada vez que um mundo ou a Oficina abre, e quando **Configurações** abre. Um arquivo alterado
  enquanto um mundo está aberto conta na vez seguinte.
- **Qual mod.** Um arquivo pertence ao mod cuja pasta fica logo abaixo de `plugins` (o
  `ExampleVehicles` de `plugins/ExampleVehicles/worldeditor.json`). Configurações nomeia as extensões por ela.
- **Dois arquivos, um nome.** Os arquivos são lidos do mais antigo para o mais novo (pela data da última alteração):
  quando dois dizem algo sobre a mesma chave de dados, prefab ou `id` de ligação, o arquivo mais novo prevalece.
- **Tolerante.** Comentários e vírgulas finais são permitidos. Os textos têm de 1 a 200 caracteres; as listas
  têm no máximo 32 nomes; um arquivo tem no máximo 1 MB. Uma entrada fora deste formato fica de fora e o resto
  do arquivo continua valendo. Um arquivo que não é JSON, ou que não é de uma versão que o editor lê, é
  ignorado; o log do editor avisa ("mod manifest …: not a worldeditor.json this editor reads,
  skipped.").

O [esquema](../worldeditor.schema.json) descreve o arquivo: com a linha `$schema` acima, os editores de código
verificam o arquivo e completam as chaves enquanto você digita.

## Objetos vinculados

Se o seu mod constrói objetos a partir de outros (um navio e as peças construídas nele, uma carroça e a carga dela),
o jogo salva cada peça separada, e o editor vê peças soltas, amontoadas onde o jogo as salvou.
Diga a ele como elas se ligam e ele as desenha como o seu mod as constrói, e move, copia e exclui cada
grupo inteiro (veja [Objetos vinculados](select.md#navios-e-outros-objetos-vinculados) para o que
o jogador vê). Funciona para mods que ligam do jeito comum: a raiz (e qualquer parte da qual outras dependam)
guarda um id int próprio; cada parte guarda, nos dados, o id daquilo de que depende e o
deslocamento e o giro dela no referencial desse objeto.

```json
{
  "version": 1,
  "links": [
    {
      "name": "vehicle",
      "id": "ExampleVehicles_Id",
      "parents": ["ExampleVehicles_Mount", "ExampleVehicles_Vehicle"],
      "offset": "ExampleVehicles_Offset",
      "rotation": "ExampleVehicles_Turn",
      "rotationQuaternion": "ExampleVehicles_TurnQ",
      "storedAt": "root",
      "references": ["ExampleVehicles_Target", "ExampleVehicles_Drives"],
      "dropOnCopy": ["ExampleVehicles_Packed", "ExampleVehicles_Aboard"]
    }
  ]
}
```

| Chave | |
|---|---|
| `id` | Chave de dados int do id próprio de um objeto, que as partes dele nomeiam. Obrigatória. |
| `parents` | Chaves de dados int que nomeiam aquilo de que uma parte depende; vale a primeira que estiver definida (uma peça sobre uma base giratória nomeia a base antes do veículo). Pelo menos uma. Obrigatória. |
| `offset` | Chave de dados Vec3 do lugar de uma parte no referencial daquilo de que ela depende, em metros. Obrigatória. |
| `rotation` | Chave de dados Vec3 do giro dela nesse referencial, como ângulos de Euler do Unity em graus. |
| `rotationQuaternion` | Chave de dados Quaternion desse giro, usada quando `rotation` não está gravada. Sem nenhuma das duas, uma parte não é girada no referencial do pai. |
| `storedAt` | Onde o jogo salva uma parte: `root` (na raiz do grupo; o seu mod a monta a partir do deslocamento quando o grupo carrega; o padrão) ou `own` (onde ela está). O editor salva as partes movidas da mesma forma. |
| `references` | Outras chaves de dados int que nomeiam o id de um objeto (o mastro de uma vela, o alvo de uma alavanca): numa cópia colada, elas seguem os novos ids da cópia, e são retiradas quando o que nomeiam não foi copiado. |
| `dropOnCopy` | Chaves de dados que uma cópia colada deixa de fora: ids empacotados de um jeito que o editor não consegue reescrever (um array de bytes), vínculos que não devem passar adiante (o que estava a bordo). |
| `placements` | Por chave de pai (uma das `parents`), como uma parte que depende dela é posicionada quando não segue a regra: as próprias chaves `offset`, `rotation` e `rotationQuaternion` e `storedAt` (cada uma não informada: a da regra), como uma vela que mantém o lugar no mastro separado do lugar no navio; ou `"carried": true` (e nenhuma daquelas), uma parte que o grupo carrega onde o jogo a salvou (uma carroça a bordo): ela não guarda deslocamento, é desenhada onde está salva em relação ao que a carrega, e é movida, girada, copiada e excluída com o grupo, e salva onde está. |
| `name` | Como um grupo é chamado nas mensagens do editor ("vehicle por inteiro"). Padrão: `group`. |

### Como o editor usa as ligações

- **Desenho.** Uma parte é desenhada no seu deslocamento no referencial do pai, o pai onde o pai dele
  o põe, e assim por diante até a raiz, que fica onde está salva: as peças de um navio giram e se inclinam
  com o casco. Uma parte carregada fica onde está salva em relação ao que a carrega. Uma extensão pode
  girar o referencial em que as partes de um pai ficam, ou posicionar ela mesma uma parte (`AddLinkPlacer`, veja
  [Ganchos](#ganchos)). Uma parte cujo pai (ou qualquer pai acima na cadeia) não está no mundo, ou cuja
  cadeia forma um laço ou tem mais de 16 níveis (dados danificados), fica onde está salva.
- **Qual regra.** Um objeto segue a primeira regra cujas chaves `id` ou `parents` ele tem. Os nomes de
  prefab não importam: só os dados. A regra de um arquivo mais novo com o mesmo `id` substitui a de um mais
  antigo, no lugar dela na ordem.
- **Mover e excluir.** Selecionar qualquer parte e mover, girar ou excluir afeta o grupo
  inteiro. Um grupo movido mantém a altura e não se encaixa; as partes dele são salvas como `storedAt` diz.
- **Copiar.** Uma cópia leva o grupo inteiro. Cada colagem (e cada uma das repetições dela) recebe novos ids que
  nenhum objeto do mundo tem, nem mesmo os excluídos, para que o jogo nunca tome duas cópias por uma só; todos os
  valores de pai e de `references` acompanham. Uma parte copiada sem o pai é colada como um objeto
  comum (sem as ligações), onde foi colada. Uma colagem espelhada espelha também os deslocamentos e os
  giros das partes. Os valores em `dropOnCopy` somem, a menos que o reescritor de cópia de uma extensão os trate.
- As chaves de dados são nomeadas como o seu código as nomeia, como em `data`.

## Objetos atados

Alguns mods atam objetos sem guardar onde cada um fica em relação ao outro: um trem nomeia os vagões que puxa,
uma plataforma não nomeia nada, mas o que é construído nela nomeia a plataforma. O jogo salva cada objeto onde ele
está, e o seu mod os junta de novo enquanto roda. Diga ao editor como eles estão atados e ele os mantém como um grupo:

```json
{
  "version": 1,
  "ties": [
    { "name": "train", "root": "ExampleVehicles_Wagons", "member": "ExampleVehicles_Self", "idBytes": 12 },
    { "name": "deck", "root": "ExampleVehicles_DeckId", "member": "ExampleVehicles_OnDeck" }
  ]
}
```

| Chave | |
|---|---|
| `root` | Chave de dados dos ids que uma raiz nomeia: um int, um long, ou um array de bytes com vários ids um depois do outro. Obrigatória. |
| `member` | Chave de dados do id próprio de um membro, do mesmo tipo (um int, um long ou um array de bytes). Obrigatória. |
| `idBytes` | Para um array de bytes, quantos bytes tem um id (de 1 a 64; 12 para os ids de objeto do jogo). Sem ela, o valor inteiro é um id. |
| `name` | Como um grupo é chamado nas mensagens do editor. Padrão: `group`. |

Um membro está no grupo de uma raiz quando o id próprio dele é um dos que a raiz nomeia. Os ids são comparados como os
bytes que são, seja o que for que signifiquem para o seu mod: o editor nunca os procura no jogo. Grupos que compartilham
um objeto são um só grupo: um trem cujo vagão-plataforma é a raiz de uma plataforma leva junto o que foi construído na
plataforma.

- **Selecionar, mover e excluir.** Selecionar qualquer objeto de um grupo e mover ou girar afeta o grupo inteiro,
  como um bloco: cada membro mantém onde está em relação aos outros e é salvo onde está. Excluir a raiz
  leva os membros dela; excluir um membro leva só ele.
- **Copiar.** Uma cópia leva o grupo inteiro. Cada colagem (e cada uma das repetições dela) recebe novos ids do mesmo
  tipo e tamanho, aleatórios, na raiz e nos membros que ela nomeia, para que uma cópia nunca leve os membros do
  original: uma lista mantém os ids dos membros copiados, um id único é sempre novo, e um membro cuja raiz não foi
  copiada perde o id.
- **Ao vivo e salvo.** Os novos ids cabem ao seu mod resolver: ele encontra os membros de uma raiz colada pelos ids que
  eles guardam, como faz depois que o jogo carrega um mundo (o jogo dá a cada objeto um novo id de objeto a cada
  carregamento).
- Uma regra com as mesmas chaves `root` e `member` num arquivo mais novo substitui a de um mais antigo, no lugar dela
  na ordem.

## Extensões

O que um arquivo não consegue dizer, o código consegue. Um mod pode trazer uma **extensão do editor**: uma pequena DLL,
compilada com o SDK do editor, que o editor carrega para ler e desenhar os objetos do mod. Ela não roda
no jogo e nunca vê o código do jogo nem do Unity: só os dados dos objetos, como o editor os
tem. Ela pode:

- mostrar em palavras, no Inspetor, um valor de array de bytes;
- reescrever, nas cópias coladas, os ids que o seu mod empacota no formato próprio dele;
- definir o que o seu mod deduz do lugar onde um objeto está sempre que o editor o coloca, cola, move ou gira (o
  trilho sob um veículo, um giro que o seu mod guarda);
- desenhar o que o seu mod gera em código enquanto o jogo roda (uma vela a partir dos cantos, um cabo de um poste
  a outro);
- posicionar as partes dos seus objetos vinculados onde o seu mod as constrói, quando só os dados delas não
  bastam (as partes sobre uma base giratória que o seu mod girou);
- deixar o terreno sem desenhar onde o seu mod cava (a boca de um túnel, um fosso);
- desenhar o que o seu mod faz de uma área como um todo (um canal, rocha escavada de um morro) com o visual do terreno
  do jogo, no lugar do terreno do editor;
- ler as configurações dos próprios scripts do jogo (os `gameComponents` dela), como o Valheim do jogador as tem;
- ler as configurações do seu próprio mod (os `settingsFiles` dela), como o jogo do mundo as tem;
- adicionar uma ferramenta sua à barra do editor 3D, com o cartão de configurações, a prévia e as edições dela
  no histórico (cavar um canal onde o jogador clica);
- para um mod de geração de mundo, ler e desenhar os mundos que ele cria, e criar mundos novos com ele;
- no modo ao vivo, pedir ao próprio plugin do seu mod no jogo o que só o jogo sabe (veja
  [Modo ao vivo](#modo-ao-vivo)).

### Fazer uma

1. **Obtenha o SDK** numa [versão](https://github.com/Shuiei/valheim-world-editor/releases):
   `ValheimWorldEditor.Sdk-<version>.zip` contém `ValheimWorldEditor.Sdk.dll` (netstandard2.0, aquilo com que uma
   extensão é compilada; a build net10.0 dele em `net10.0/` também contém o modelo de regras de mundo, para uma
   extensão no .NET do editor que crie regras de mundo) e `WorldEditorBridge.Api.dll` (aquilo com que o plugin de um
   mod é compilado para o [modo ao vivo](#modo-ao-vivo)), cada um com o arquivo de documentação para editores de código,
   esta página como `README.md`, e a amostra Example Vehicles completa em `sdk/samples/` (ela compila como está).
2. **Crie uma biblioteca de classes netstandard2.0** (net10.0, com `net10.0/ValheimWorldEditor.Sdk.dll`, para uma que
   crie regras de mundo) que referencie `ValheimWorldEditor.Sdk.dll` sem copiá-la
   para a sua saída (`Private="false"`): o editor fornece a dele. Não use nada além do
   framework (o projeto da amostra também referencia o pacote `System.Numerics.Vectors`, para
   `Vector3` e `Quaternion`). Uma extensão é **uma DLL só**: o editor não carrega nenhum outro arquivo da sua
   pasta, então uma biblioteca auxiliar ao lado dela nunca é encontrada.
3. **Marque o assembly** e dê a ele um ponto de entrada: uma classe pública com um construtor sem
   parâmetros, que implemente `IWorldEditorExtension`. O editor a cria uma vez e chama `Register`
   uma vez, antes de usar qualquer gancho. Este é o da amostra:

   ```csharp
   [assembly: WorldEditorExtension(typeof(ExampleVehicles.WorldEditor.Extension))]

   public sealed class Extension : IWorldEditorExtension
   {
       public void Register(IExtensionRegistry registry)
       {
           var packed = new PackedIds();
           registry.AddDecoder(Keys.Packed, packed);           // inspetor
           registry.AddCopyRewriter(packed, Keys.Packed);      // colar
           registry.AddShapes(new Sails(), Keys.Sail);         // visão 3D
           registry.AddShapes(new Cables(), Keys.Post);
           registry.AddGroundCuts(new Pits(), Keys.Pit);       // o terreno
           registry.AddScene(new Canals());                    // a área como um todo
           registry.AddTool(new CanalTool());                  // uma ferramenta na barra
           registry.AddAreaCopier("canals", new CanalCopies()); // copiar e colar
           registry.AddLiveListener(new LiveLink(registry));   // modo ao vivo
           registry.Log($"ready (editor {registry.EditorVersion}, API {registry.ApiVersion})");
       }
   }
   ```

4. **Distribua-a** com o seu mod, dentro da pasta dele, e dê o nome dela no seu `worldeditor.json`:
   `"extension": "ExampleVehicles.WorldEditor.dll"`. Um caminho que sai da pasta do arquivo, ou um arquivo
   que não termina em `.dll`, não é oferecido.

### Ativá-la: Configurações → Extensões de mods

Uma extensão é código que roda no editor com as permissões do jogador, então **quem decide é o jogador**.
O editor encontra as extensões oferecidas pelos mods nas pastas de plugins dele e as lista em
**Configurações**, em **Extensões de mods**: um interruptor por extensão, com o nome da pasta do mod e do
arquivo ("ExampleVehicles · ExampleVehicles.WorldEditor.dll"). Apontar para ele mostra o caminho do arquivo e
o SHA-256 dele. Sob cada interruptor, o editor diz o que aconteceu com ela: "Em execução.", "Não está em execução: …"
com o motivo, ou "Desligada: the file cannot be read.". Quando nenhum mod oferece uma: "Nenhum mod nas pastas acima
oferece uma."

- **Desligada por padrão.** Uma extensão só roda depois que o jogador a ativa e pressiona **Salvar**; a partir daí
  ela roda, e as desativadas param.
- **Só aquele arquivo.** O editor guarda o SHA-256 do arquivo. Uma DLL alterada (uma atualização do mod) é outro
  código: fica desligada até o jogador ativá-la de novo. Os bytes verificados são os bytes carregados.
- **Por que ela não está em execução.** "it is not marked as an extension ([assembly: WorldEditorExtension])",
  "it needs a newer editor (API …; this one has …)", "its entry … is not an IWorldEditorExtension",
  ou a exceção que `Register` lançou.

### Ganchos

| Gancho | O que o editor faz com ele |
|---|---|
| `AddDecoder(key, IDataDecoder)` | O Inspetor mostra um valor de array de bytes nas suas palavras, sob o valor (em vez de só o tamanho dele). `Describe` devolve algumas linhas curtas, ou `null` quando os bytes não estão num formato que ele lê. Quando duas extensões leem uma mesma chave, vale a primeira que der um texto. |
| `AddCopyRewriter(ICopyRewriter, keys)` | Cada objeto de uma cópia colada que tenha uma das chaves é entregue a você (`ICopy`), depois da ligação feita pelo próprio editor (`links`), com os novos ids da cópia para os dos originais (`IIdMap.TryGetNew`; false quando o objeto que tinha o id antigo não foi copiado). Altere os valores dele no lugar (`SetInt`, `SetQuaternion`, `SetBytes`…; `null` retira um valor). As chaves são suas: `dropOnCopy` deixa de removê-las. |
| `AddAreaCopier(id, IAreaCopier)` | O que o seu mod guarda de um lugar e que nenhum objeto copiado carrega (um registro de rocha escavada, em portadores de dados que o editor nunca copia) vai junto com as cópias do editor. Quando o jogador copia (a seleção da ferramenta Região, ou os objetos da ferramenta Selecionar), `Copy(area, region)` recebe a área e o que é copiado (`CopyRegion`: o `Outline` e `Contains(x, z)` dela, em metros do mundo, e o `Middle` dela) e devolve bytes próprios seus (ou `null`): guarde os lugares como deslocamentos a partir do centro, já que a cópia pode ser colada em qualquer lugar, girada e espelhada, inclusive em outro mundo. Os bytes vão com a cópia e para os arquivos de projeto, sob o nome do seu mod e o `id` (letras, algarismos, pontos, hifens e sublinhados). Quando ela é colada, `Paste(area, bytes, copies)` é chamado uma vez com todas as cópias que a colagem deposita (`CopyPlacement`: o `Middle`, `Turn` e `Mirror` dela; `Place(offset)` transforma um deslocamento a partir do centro antigo no ponto do mundo onde ele cai, `Yaw(yaw)` um giro), e a área como a colagem deixa o terreno dela; o `ToolEdit` que você devolve faz parte do passo de desfazer da colagem. Os bytes cujo copiador não está em execução (a sua extensão não instalada) ficam fora da colagem. |
| `AddPlacer(IObjectPlacer, prefabs)` | Os objetos dos prefabs que o editor põe num lugar próprio (colocados, colados, movidos ou girados, por qualquer ferramenta, um script ou o Claude) são entregues a você uma vez por passo, todos juntos (`ICopy`, cada um no novo lugar, com os valores como serão criados, os de uma cópia colada depois dos reescritores de cópia), com a área como está antes do passo, sem os objetos que ele move: defina o que o seu mod deduz do lugar onde um objeto está (um giro que ele guarda à parte do giro do objeto, o trilho em que um veículo está, a parada que uma placa nomeia), como o seu mod faria quando um jogador o constrói. O que você define é salvo com o passo e aplicado ao vivo com ele. `SetTurn` gira o objeto como ele será criado, para um mod que mantém os objetos sem giro e a forma deles nos valores (gire os valores e depois o objeto de volta; a partir do editor 1.119.0). Os objetos alterados onde estão (o Inspetor, a edição de uma ferramenta) não são uma colocação. |
| `AddShapes(IShapeProvider, prefabs)` | Os objetos dos prefabs também são desenhados com as suas formas: `Shape`s, triângulos no referencial do objeto (metros; x para a direita, y para cima, z para a frente; com o lugar, o giro e o tamanho do objeto aplicados) ou no do mundo (`WorldSpace = true`), uma cor cada (vermelho, verde, azul, alfa de 0 a 1; alfa abaixo de 1: transparente), enrolados no sentido horário vistos de frente, como no Unity, ou vistos dos dois lados (`BothSides = true`, para uma vela ou um pano). Eles são desenhados com o tipo do objeto (ocultos quando o painel Visão o oculta). Um objeto do qual o editor não tem modelo é desenhado só pelas suas formas (sem caixa substituta), e escolhido e contornado pela caixa das formas que você dá para um dos prefabs dele sem dados (as que estão no referencial próprio dele). Uma forma com um triângulo que cita um vértice que ela não tem, ou com um vértice que não é um número, não é desenhada. |
| `AddLinkPlacer(ILinkPlacer, prefabs)` | As partes vinculadas (os `links` do seu manifesto) posicionadas onde o seu mod as constrói quando só os dados delas não bastam. `FrameOf(obj)`, para um objeto dos prefabs que tenha partes: o referencial em que elas ficam, no referencial próprio dele (`LinkFrame`: um deslocamento e um giro; a plataforma de uma base giratória como o seu mod a girou ou moveu), ou `null` para o referencial próprio. `PlaceOf(part, parent)`, para uma parte dos prefabs: onde ela fica no referencial daquilo de que depende, no lugar do deslocamento e do giro dela (a parte no lugar e no giro em que o jogo a salvou, o pai como desenhado; o referencial do pai não é aplicado nesse caso), ou `null` para o lugar das ligações dela. Mover, copiar e excluir continuam seguindo as ligações. |
| `AddGroundCuts(IGroundCutter, prefabs)` | O terreno não é desenhado dentro dos contornos (`GroundCut`) que você dá para os objetos dos prefabs: pontos no terreno (x, z) no referencial do objeto, ou no do mundo (`WorldSpace = true`); pelo menos três, em qualquer sentido (um contorno que se cruza recorta pela regra par-ímpar). Um clique ali passa pelo buraco. O terreno do editor é um mapa de altura com um ponto a cada metro: o recorte deixa de fora o terreno entre os pontos dentro do contorno, então a borda dele segue essa grade de 1 m, e o que aparece através dele é o que você desenha embaixo (formas) e a face de baixo do terreno. |
| `AddScene(ISceneProvider)` | O que o seu mod faz de uma área como um todo, desenhado nela: `Build(area)` é chamado numa thread de trabalho cada vez que uma área abre e de novo sempre que os objetos, o terreno ou o jogo dela mudam (ou quando você chama `Host.Redraw()`), e devolve uma `SceneLayer` (ou `null`). As `Meshes` dela (`SceneMesh`: vértices e normais no referencial do mundo, em metros, triângulos enrolados no sentido horário vistos de frente, como no Unity) são desenhadas com o visual do terreno do jogo quando têm `BiomeColors` (a cor de terreno de cada vértice, como `area.BiomeColor` a dá) e pintadas a partir da `Mask` delas (uma pequena textura da máscara de pintura do terreno, `MaskTexture`) em `MaskUvs`; sem cores de bioma, em `Color`. Os cliques caem nelas (a menos que `Pickable = false`). Os `Holes` dela (`GroundHole`: retângulos em metros inteiros) deixam o terreno do editor sem desenhar, célula por célula, exatamente até as bordas: desenhe o que o substitui. A `area` (`IAreaView`) dá os limites dela, os objetos de um prefab como estão, com as alterações ainda não salvas (`ObjectsOf`, incluindo os seus portadores de dados), a altura do terreno como o jogo a mostra (`GroundHeight`, entre os pontos a um metro de distância, ao longo dos próprios triângulos do jogo), o bioma, se um ponto está no Extremo Norte ou nas Terras Cinzentas, e a cor e a pintura do terreno num metro inteiro (`BiomeColor`, `GroundMask`). Calcule tudo a partir da área a cada vez; não guarde nada entre chamadas. |
| `AddTool(IEditorTool)` | Uma ferramenta sua na barra do editor 3D, em **Mods**, com a `Info` dela (`ToolInfo`: um id, único na sua extensão; o nome dela na barra; uma linha de ajuda na dica; `Icon`, um caminho SVG desenhado em 16 × 16, senão a primeira letra do nome). Escolhida, ela mostra um cartão com os `Controls` dela: `SliderControl` (mín., máx., valor, `Step`, `Unit`), `ChoiceControl` (uma lista), `ToggleControl`, `ButtonControl` e `TextControl` (uma linha que você altera com `SetText`); os valores do jogador são lembrados de uma execução para a outra. O editor chama `OnChosen` e `OnLeft` quando ela é escolhida e deixada, `OnControl(id)` quando um valor muda ou um botão é pressionado, `OnPointer` a cada movimento, clique, arrasto e soltura sobre a visão 3D (`PointerInput`: o raio, as teclas pressionadas e aquilo para onde ele aponta: o terreno, a sua camada de cena ou uma coisa, com o ponto e a normal do terreno ali), e `OnKey` a cada tecla pressionada (o nome da tecla, como "Enter", "Escape", "Back" ou "A"; devolva true quando a tiver usado, false a deixa para o editor, para que Ctrl+Z continue desfazendo). O `IToolContext` entregue a cada chamada dá a área (`Area`, como para as camadas de cena), os valores dos seus controles (`Number`, `Choice`, `IsOn`), formas desenhadas sobre a área até você as alterar (`Preview`, no referencial do mundo), uma linha na barra de status (`Status`) e `Commit(label, ToolEdit)`: objetos adicionados (um prefab, um lugar, um giro e os dados definidos por meio de um `ICopy`, que `SetTurn` pode girar), alterados (uma cópia de um objeto que a área lhe deu, com as suas alterações) e removidos, num único passo do histórico da área, desfeito e salvo como qualquer outra edição. Os seus portadores de dados são guardados com o histórico como os outros objetos, embora nunca sejam desenhados; um objeto que você não recebeu da área é deixado em paz. |
| `AddLiveListener(ILiveListener)` | Avisado quando o editor se conecta a um jogo (`OnConnected`, com o jogo) e quando isso termina (`OnDisconnected`: o mundo foi fechado, ou outro foi aberto). Veja [Modo ao vivo](#modo-ao-vivo). |
| `AddWorldGenerator(IWorldGenerator)` | Os mundos do seu mod de geração de mundo (veja [Geradores de mundo](#geradores-de-mundo)). |
| `AddWorldRules(IWorldRulesProvider)` | As regras com que os mundos são gerados, a partir das configurações e dos arquivos de dados do seu mod de geração de mundo (veja [Regras de mundo](#regras-de-mundo)). |
| `Host` | O editor, fora dos ganchos: `Host.Live`, o jogo conectado (null quando não há: um mundo salvo, a Oficina); `Host.Game`, os componentes do jogo que os `gameComponents` do seu manifesto pedem (`Components("Ship")`: cada um com o prefab, o objeto em que está e os campos pelo nome, como o jogo os salva; pronto quando um mundo ou a Oficina está aberto, então peça a partir dos seus ganchos, não de `Register`); `Host.Setting(file, section, key)`, uma configuração de um arquivo que os `settingsFiles` do seu manifesto nomeiam, como o texto que o arquivo contém (null quando o arquivo ou a configuração não está lá: use o padrão do seu mod; pronto, como `Host.Game`, quando um mundo ou a Oficina está aberto); `Host.BepInExFolders`, as pastas BepInEx do jogador que o editor conhece, na ordem em que ele as consulta (cada uma com o `Path` e onde fica, para o jogador); `Host.DataFolder`, a pasta de dados do editor (guarde os seus próprios arquivos numa pasta sua ali); `Host.World` (extensões net10.0), as regras e os objetos do próprio mundo do jogo (veja [Regras de mundo](#regras-de-mundo)); e `Host.Redraw()`, para que as suas formas e recortes sejam refeitos no próximo quadro (depois que o jogo lhe disse algo novo). |
| `Log(message)` | Grava uma linha, com o nome do seu mod ("extension ExampleVehicles: …"), no log do editor, `ValheimWorldEditor.log` na pasta de dados dele (**Abrir log** na página inicial; recomeçado a cada execução). |

Os ganchos de forma e de recorte também recebem o mundo em volta do objeto (`IWorldView`): `TryFind(idKey, id, out
obj)` encontra outro objeto por um id que ele guarda, para uma corda ou um cabo que chega até ele. Só são encontrados os
ids sob uma chave que uma regra de `links` nomeia como `id` (o editor indexa essas). No editor 3D, esse
mundo também é um `IGroundView` (`world is IGroundView ground`): `TryGroundHeight(x, z, out height)`
dá a altura do terreno ali como o editor o desenha, para uma forma que desce até ele (uma escada de
corda), false fora da área.

Os objetos chegam como `IObjectView`: o nome do prefab ("" quando o editor não o conhece), o lugar, o
giro (ângulos de Euler do Unity em graus, e `Orientation`, um quaternion) e o tamanho como desenhado (para uma parte
ligada, onde o seu mod a constrói, não onde o jogo a salva), e cada valor dos dados dele por chave:
`TryGetInt`, `TryGetLong`, `TryGetFloat`, `TryGetVector3`, `TryGetQuaternion`, `TryGetString`,
`TryGetBytes`. As chaves e os prefabs são nomeados como o seu código os nomeia; o editor calcula o hash deles como o
jogo faz.

### Geradores de mundo

Um mod de geração de mundo substitui o terreno do jogo e marca os mundos que cria: o arquivo de mundo deles começa com -1
onde fica a versão de arquivo do jogo, depois o id do gerador (uma string), depois o que o mod guardar (o Riverheim
antes da 1.3: a versão de arquivo do jogo, depois a predefinição dele). O editor recusa um mundo assim, a menos que uma
extensão em execução registre um gerador com esse id, como o jogo sem o mod o recusa: listado, esmaecido, com o motivo
(nomeie os ids nos `worldGenerators` do seu manifesto para que o editor diga o que falta).

Um mod que se conecta ao [Worldhalla](https://thunderstore.io/c/valheim/p/Gurebu/Worldhalla/) (o Riverheim desde a 1.3)
tem os mundos marcados pelo Worldhalla, com o id e as configurações do gerador no cabeçalho próprio do Worldhalla.
Registre um gerador para eles com o id `worldhalla:` seguido do id do seu gerador no Worldhalla (o do Riverheim:
`worldhalla:dev.gurebu.riverheim`): o `ReadHeader` dele recebe a versão de arquivo do jogo e depois as suas
configurações como o Worldhalla as guarda, e precisa lê-las por inteiro. Um mundo cujo cabeçalho do Worldhalla é de um
Worldhalla mais novo que o 1.0 (o formato dele, ou configurações do próprio Worldhalla) é recusado com o motivo. O seu
`IWorldGenerator` fornece:

- `Id`, `PluginId` (o GUID BepInEx do mod: no modo ao vivo, você fica sabendo a versão que o jogo executa) e `Name`;
- `ReadHeader(file, out fileVersion)` e `WriteHeader(file, fileVersion, settings)`: o que vem depois do id, lido e
  gravado nos formatos do jogo (`IWorldFileReader`, `IWorldFileWriter`), com as suas configurações guardadas como um
  `GeneratorSettings` (uma descrição para o jogador e um valor seu). Lance `InvalidDataException` com o motivo para o
  jogador quando não conseguir ler um cabeçalho;
- `Problem(settings)`: por que um mundo assim não pode ser desenhado, rapidamente (perguntado para cada mundo que a
  página inicial lista), ou null;
- `Open(settings, gamePluginVersion)`: pronto para montar mundos, quando um mundo abre (`IWorldLayout`: a sua
  `Version`, se a montagem é `Slow`, e `LayOut(seed)`); lance `InvalidOperationException` com o motivo quando não
  conseguir;
- `LayOut(seed)` dá um `IGroundLayout`: o bioma (o valor do jogo), a altura com a máscara e o fator de floresta num
  ponto, no lugar do corpo de GetBiome, GetBiomeHeight, GetAshlandsHeight e GetForestFactor do jogo (chamados a partir
  de muitas threads). O esticamento do Expand World continua vindo antes das suas respostas, e os multiplicadores de
  profundidade e de floresta dele depois, como os patches dos mods se encontram no jogo; o editor não monta nenhuma água
  do jogo;
- `NewWorldSettings()`: as configurações que um mundo novo recebe (a página Um mundo novo oferece o seu gerador,
  escolhido primeiro), ou null;
- `Notes(settings, expandWorld)`: o que as configurações do Expand World (`ExpandWorldSettings`) fazem além disso aos
  seus mundos, para o log.

Qualquer outra coisa que o seu gerador lance é uma falha: a extensão dele é desligada, e o jogador é avisado de que o
complemento falhou.

### Regras de mundo

Um mod de geração de mundo que altera a geração do próprio jogo por meio de configurações e arquivos de dados (um mundo
maior, biomas, locais e vegetação próprios, como faz o Expand World) não monta o mundo sozinho: o gerador do editor
segue as regras que recebe. Compile a sua extensão para net10.0 (a build `net10.0` do SDK contém o modelo de regras,
`WorldRules` e os tipos que ele contém, no namespace `ValheimGen`) e registre um `IWorldRulesProvider`:

- `ForWorld(world)`: as regras com que um mundo abre. O `WorldRulesRequest` dá o `Name` dele, as
  `StartingGlobalKeys` que o arquivo de mundo guarda (as configurações de um mod gravadas ali), as `LoadedGlobalKeys`
  que o salvamento tem, o `GridWidth` em que os arquivos de chunk estão, e o jogo em que ele roda quando está ao vivo
  (`Live`, senão null): nesse caso, leia os arquivos do mod a partir do jogo, por `Live.Plugins()`,
  `Live.ConfigFile(path)` e `Live.ConfigFolder(path)` (sob o BepInEx/config dele; null com um WorldEditorBridge
  anterior à 1.54.0), senão a partir de `Host.BepInExFolders`;
- `ForNewWorld()`: as regras com que um mundo novo é criado e examinado (a página Um mundo novo).

Devolva null (ou `WorldRules.Vanilla`) quando o seu mod não altera o mundo (não instalado, ou com os padrões): o
próximo provedor é consultado, e sem nenhum o mundo é o do próprio jogo. As regras de locais, vegetação e salas são
feitas sobre as do próprio jogo (`Host.World`: os `Locations` dele depois que `ReadLocations()` os leu, a `Vegetation`,
`HasPrefab(name)` e `IsGameRoom(name)`). `WorldRulesText.Describe(rules)` diz em poucas palavras o que as regras mudam,
para as suas linhas de log. Um provedor que lança uma exceção é uma falha: a extensão dele é desligada, e o próximo é
consultado.

### Regras

- **Seja rápido, não guarde estado.** Os ganchos são chamados de várias threads ao mesmo tempo e com frequência (a cada
  redesenho das formas e recortes, a cada objeto colado): não guarde estado entre chamadas (ou proteja-o com lock),
  e retorne rápido.
- **Nunca lance exceções.** Um gancho que lança uma exceção desliga a extensão até o editor ser iniciado de novo
  (Configurações diz por quê, assim como o log); o editor continua sem ela. No código que continua depois que um
  gancho retorna (uma chamada `async` ao jogo), capture tudo você mesmo, como faz o `LiveLink` da
  amostra.
- **Versões.** `SdkInfo.ApiVersion` é a versão da API (hoje 1), e o atributo registra a versão
  com que você compilou. Um editor executa extensões compiladas com a versão dele ou com uma anterior. Os novos
  ganchos vêm como acréscimos; a versão só muda quando a API quebra.

## Complementos

Uma extensão é distribuída com o mod dela. Quando o autor de um mod não fez uma (ou o mod não é mais
mantido), qualquer pessoa pode fazer: um **complemento** é um `worldeditor.json` e uma extensão para **o mod de outra
pessoa**, lançado à parte, que o jogador instala ao lado do editor. O próprio mod continua como
está, em `BepInEx/plugins`; o complemento só ensina o editor sobre ele.

```
ValheimWorldEditor/                a pasta do editor (no macOS: a pasta que contém ValheimWorldEditor.app)
  ValheimWorldEditor(.exe)
  addons/
    Tunnels/                       uma pasta por complemento, com o nome que você quiser
      worldeditor.json
      Tunnels.WorldEditor.dll
```

- **O arquivo dele** é um `worldeditor.json` como acima (rótulos de dados, prefabs, ligações, `extension`,
  `gameComponents`), com uma chave a mais: `"serves": "TheirMod.dll"`, o nome do arquivo da DLL do mod.
  O complemento é usado enquanto uma DLL com esse nome estiver numa das pastas de plugins que o editor conhece
  (Configurações, Pastas do BepInEx), em qualquer lugar sob `plugins`, e ignorado caso contrário. Sem `serves`,
  ele é sempre usado (uma ferramenta sua, para nenhum mod em particular). Um `serves` que não seja um simples
  nome de arquivo terminado em `.dll`, ou um arquivo de uma versão que este editor não lê, deixa o complemento
  de fora (o log diz por quê).
- **A extensão dele** é feita como qualquer outra (veja [Fazer uma](#fazer-uma)): uma DLL só, dentro da
  pasta do complemento, com todos os ganchos e regras iguais.
- **Ligado ao ser instalado.** Instalar um complemento é escolha do jogador, então ele roda sem precisar ser ativado
  em Configurações. Configurações lista os complementos em **Complementos**, cada um com um interruptor que o desliga
  sem removê-lo (nada dele é usado então), e com o que aconteceu com cada um: "Aguardando o
  mod: TheirMod.dll não está nas pastas de plugins acima.", "Em execução.", "Não está em execução: …", que ele só
  traz anotações, ou que está desligado.
- **A palavra do próprio mod prevalece.** Os arquivos dos complementos são lidos antes dos dos próprios mods: se o mod
  um dia trouxer um `worldeditor.json` próprio, o que ele diz substitui o que o complemento dizia (uma
  extensão própria dele precisa então do interruptor do jogador, como sempre).
- **Lance-o como um `.zip` que contenha a pasta dele**, para os jogadores descompactarem em `addons`. Os
  pacotes do editor vêm com essa pasta, vazia a não ser por um `README.txt`.
- **O próprio repositório do editor mantém alguns complementos** (`addons/`, cada um lançado como zip próprio ao lado
  dos pacotes do editor: veja [development.md](../development.md#add-ons-in-this-repository)); o seu pode
  ficar em qualquer lugar.

## Modo ao vivo

Conectado a um jogo em execução (modo ao vivo), o editor aplica as alterações por meio do
plugin WorldEditorBridge, que destrói, cria e move objetos no jogo. Um mod que monta
coisas a partir dos objetos dele enquanto o jogo roda (um veículo a partir das partes, um túnel a partir do que foi
cavado) só vê as alterações do editor no próximo carregamento, a menos que o próprio plugin dele participe. O bridge
oferece duas coisas aos plugins dos mods, por meio de `WorldEditorBridge.Api.dll`: ela vem com o bridge, na pasta
`plugins` dele, e no zip do SDK, e não contém nenhum tipo do jogo nem do Unity, então uma atualização do jogo não pode
quebrá-la.

### O lado do seu plugin

Registre-se no `Awake` do seu plugin, com uma dependência opcional do bridge, e mantenha o código que
mexe na API numa classe própria: assim o seu plugin também carrega sem o bridge. Da
`sdk/samples/ExampleVehicles.Bridge` da amostra:

```csharp
[BepInPlugin(PluginId, "ExampleVehicles", "1.0.0")]
[BepInDependency(BridgeApi.BridgeGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ExampleVehiclesBridgePlugin : BaseUnityPlugin
{
    public const string PluginId = "ExampleVehicles";

    private void Awake()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Register(Logger);   // uma classe própria: este plugin carrega sem o bridge
        }
    }

    private void OnDestroy()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Unregister();       // BridgeApi.Remove(PluginId)
        }
    }
}

internal static class BridgeLink
{
    public static void Register(BepInEx.Logging.ManualLogSource log)
    {
        BridgeApi.AddRoutes(ExampleVehiclesBridgePlugin.PluginId, new Routes());          // responde às chamadas do editor
        BridgeApi.AddListener(ExampleVehiclesBridgePlugin.PluginId, new Listener(log));   // avisado do que o editor aplicou
    }

    public static void Unregister() => BridgeApi.Remove(ExampleVehiclesBridgePlugin.PluginId);
    // Routes e Listener: veja a amostra.
}
```

| Gancho | O que o bridge faz com ele |
|---|---|
| `AddListener(modId, IBridgeListener)` | `OnApplied(AppliedChanges)` é chamado na thread principal do jogo logo depois de cada aplicação do editor, com o que ela mudou: `Created` e `Destroyed`, os objetos criados e destruídos (cada um com o `User` e o `Id` do ZDOID, o hash do prefab e a posição); `Moved`, os objetos movidos sob o próprio ZDOID (bridge 1.40.0 ou mais novo: `From` onde cada um estava, `To` onde está agora; um objeto cujos lugares antigo e novo um jogador tem ambos carregados é criado de novo em vez disso, e está em `Created` e `Destroyed`; a partir da 1.97.0, um objeto cujos dados o editor alterou também está aqui, já com os novos dados); `TerrainZones`, as zonas cujo terreno foi regravado; `ResetZones`, as zonas esvaziadas para serem geradas de novo. Remonte as suas coisas aqui. Uma exceção é registrada no log do jogo e não muda nada do que o editor fez. |
| `AddRoutes(modId, IBridgeRoutes)` | `Handle(name, body)` responde a `POST /mods/<modId>/<name>` na thread principal do jogo, nos bytes que você e a sua extensão do editor combinarem; `null` responde 404 ("the mod has no route of that name"), uma exceção, 500 com a mensagem dela. O bridge espera no máximo 30 segundos pela thread principal: seja rápido, ou responda "started" e deixe a extensão perguntar de novo. O corpo de uma chamada tem no máximo 16 MB. |
| `Remove(modId)` | Retira as suas rotas e o seu listener (um plugin sendo encerrado). |

Os objetos que o bridge cria, move ou altera são entregues aos mods do jogo como o jogo lhes entrega um
objeto vindo de outro jogo: os dados deles passam de novo por `ZDO.Deserialize` (os mesmos
valores). Um mod que fica sabendo dos objetos aplicando um patch nisso (o ZdoWatcher do ValheimRAFT, que encontra os
navios e aquilo a que as cordas estão atadas pelos ids) reconhece um objeto colado na hora, sem listener próprio.

Registrar de novo com o mesmo id substitui o que foi registrado antes. O id do mod é seu (o id do seu
plugin é uma boa escolha): de 1 a 100 letras, algarismos, pontos, hifens e sublinhados; qualquer outro id
lança uma exceção. `BridgeApi.Version` é a versão da API (hoje 1); um bridge executa plugins compilados com a
versão dele ou uma anterior, e os acréscimos a mantêm. `GET /mods` no bridge lista a versão da API
e os ids registrados.

### O lado da sua extensão

A sua extensão recebe o jogo por meio de `ILiveGame`: de `registry.Host.Live` a qualquer momento (null quando
o editor não está conectado), ou no `OnConnected` do seu live listener.

- `Mods`: os ids registrados no bridge, como o bridge os informou quando o mundo foi aberto
  (um plugin que se registra depois é visto na conexão seguinte).
- `CallAsync(modId, name, body)`: chama a sua rota e devolve os bytes da resposta. Lança uma exceção quando
  o mod ou a rota não está registrado, quando a rota falhou (com a mensagem do jogo), ou quando
  o jogo não pode ser alcançado.
- `Plugins()`, `ConfigFile(path)` e `ConfigFolder(path)`: os plugins BepInEx que o jogo carregou, e um arquivo ou as
  entradas de uma pasta sob o BepInEx/config dele, só para leitura (null com um bridge anterior à 1.54.0, e para um
  caminho que não existe). Lançam `InvalidOperationException` quando o bridge recusa o caminho (fora da pasta, acima de
  4 MB).

Peça ao jogo o que só ele sabe (o que o seu mod gerou em código), guarde, e chame
`registry.Host.Redraw()` para que as suas formas e recortes sejam refeitos. O `LiveLink` da amostra chama a
rota `ping` do plugin quando o editor se conecta e registra a resposta no log; o plugin dela também responde a
`players` (quantos jogadores estão no jogo).

Quando um mundo abre ao vivo, o log do editor nomeia os mods registrados ("bridge mods: …").

## A amostra Example Vehicles

O zip do SDK (e o repositório do editor) contém o código-fonte das duas metades de um mod inventado, para
copiar. O Example Vehicles constrói veículos a partir de um casco e das partes construídas nele.

| Arquivo | O que mostra |
|---|---|
| `sdk/samples/ExampleVehicles.WorldEditor/worldeditor.json` | O arquivo do mod: rótulos, um auxiliar oculto, grupos, a regra de ligação `vehicle`, a extensão. |
| `sdk/samples/ExampleVehicles.WorldEditor/Extension.cs` | O ponto de entrada, que registra um gancho de cada tipo. |
| `sdk/samples/ExampleVehicles.WorldEditor/Keys.cs` | As chaves de dados e os prefabs do mod, como o código dele os nomeia. |
| `sdk/samples/ExampleVehicles.WorldEditor/PackedIds.cs` | Um decodificador e reescritor de cópia para uma lista de ids empacotada num array de bytes (um byte de versão, depois quatro bytes por id): mostrada no Inspetor ("Linked to 3 object(s): …"), e, numa cópia colada, ajustada para nomear os objetos da cópia (os ids de objetos não copiados somem). |
| `sdk/samples/ExampleVehicles.WorldEditor/Sails.cs` | Formas: o pano de uma vela desenhado entre três ou quatro cantos guardados nos dados dela, na cor dela, visto dos dois lados. |
| `sdk/samples/ExampleVehicles.WorldEditor/Cables.cs` | Formas no espaço do mundo: um cabo do alto de um poste até o alto do poste que ele nomeia, encontrado com `TryFind`. |
| `sdk/samples/ExampleVehicles.WorldEditor/Canals.cs` | Uma camada de cena: cada canal (um portador de dados que guarda o trajeto, a largura e a profundidade) cavado como uma trincheira com o visual do terreno do jogo, o leito pintado de terra, o terreno deixado sem desenhar sobre ele. |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalTool.cs` | Uma ferramenta: clique em pontos no terreno para o trajeto de um canal (Backspace retira o último), com largura e profundidade definidas por controles deslizantes, o trajeto em prévia; Enter ou **Dig** adiciona o canal (um portador de dados), **Clear points** ou Escape descarta os pontos. Shift+clique aterra um canal (o remove), Ctrl+clique dá a ele a largura e a profundidade dos controles (o altera). |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalCopies.cs` | Um copiador de área: os canais cujo trajeto inteiro está no que é copiado são guardados como deslocamentos a partir do centro da cópia, e cavados de novo onde cada cópia é colada, girados e espelhados com ela, sobre o terreno dali. |
| `sdk/samples/ExampleVehicles.WorldEditor/Pits.cs` | Um recorte de terreno: um buraco redondo da largura do raio do fosso (1,5 m quando ele não guarda nenhum). |
| `sdk/samples/ExampleVehicles.WorldEditor/LiveLink.cs` | O lado da extensão no modo ao vivo: nomeia os navios do próprio jogo (`Host.Game`, a partir dos `gameComponents` do manifesto), confere que o plugin se registrou, chama a rota `ping` dele, registra a resposta no log e captura tudo. |
| `sdk/samples/ExampleVehicles.Bridge/ExampleVehiclesBridgePlugin.cs` | O plugin BepInEx do mod participando do modo ao vivo: dependência opcional, registro numa classe própria, rotas `ping` e `players`, um listener que registra no log o que o editor aplicou. Ele compila com as DLLs do jogo e do BepInEx como qualquer plugin (`-p:ValheimDir=…`, a pasta de um servidor dedicado do Valheim). |
