<!-- source: f5ff35e9 -->
# Traduzir o editor

As palavras do editor ficam em catálogos, um por idioma: um arquivo JSON de textos por chave. O inglês,
[`editor/Core/Lang/en.json`](../../editor/Core/Lang/en.json), é a fonte a partir da qual todos os outros catálogos são
traduzidos.

O editor traz inglês, francês (`fr`), alemão (`de`), espanhol (`es`), português do Brasil (`pt-BR`, também exibido
num computador configurado em português de Portugal) e tailandês (`th`). A documentação está traduzida em cada um (`docs/pt-BR/`…, com as imagens feitas nesse idioma). Ele mostra o idioma escolhido em
**Configurações → Idioma**, por padrão o do computador quando existe um catálogo dele. Um texto que um catálogo ainda não tem aparece em inglês. Um novo idioma vale quando o editor é iniciado
de novo.

Nem todo texto vem do catálogo ainda. Estes vêm:

- todas as dicas de passar o mouse (as dicas do Inspetor feitas a partir dos dados de um objeto mantêm os nomes como o
  salvamento os tem);
- a página inicial, Configurações, o mapa do mundo, a página Um mundo novo e os pequenos diálogos (perguntas, mensagens,
  os detalhes de um projeto) com as palavras que eles compartilham;
- a barra superior do editor 3D, com o que salvar, aplicar, descartar, sair e fechar perguntam e dizem, as mensagens e os
  jogadores do jogo ao vivo;
- as leituras da barra de status, o painel Histórico, o painel Visão, o cartão de ajuda e o que a barra de status e o
  Histórico dizem das alterações do Claude;
- as ferramentas: os pincéis com os carimbos, Medir, Máscara, Script, Caminho, Relevo, Selecionar, Colar, Colocar,
  Construir (o painel e a barra de construção), Região, a ferramenta Masmorra com o gerador, o painel Projetos, a
  Oficina e o Inspetor;
- os nomes dos biomas.

O resto passa para o catálogo janela por janela. Um catálogo feito agora continua funcionando à medida que mais textos
são acrescentados: os novos aparecem em inglês até serem traduzidos.

## Criar um catálogo

1. Copie `en.json` para a pasta `lang` da pasta de dados do editor
   (`~/.local/share/ValheimWorldEditor/lang` no Linux, `%LOCALAPPDATA%\ValheimWorldEditor\lang` no
   Windows; **Configurações → Idioma** a mostra), com o nome do código do idioma: `fr.json`,
   `de.json`, `pt-BR.json`.
2. Traduza os valores, nunca as chaves. `lang.name` é o nome do idioma no próprio idioma ("Français"), como
   Configurações o lista.
3. Mantenha cada `{0}`, `{1}`… : o editor põe um valor ali (uma pasta, um número). A ordem deles na
   frase pode mudar. Um texto cujos marcadores diferem dos do inglês, ou com um `{` ou `}` sobrando,
   não é usado.
4. Escolha o idioma em **Configurações**, reinicie o editor e confira. As chaves que você deixar de fora, ou
   apagar da sua cópia, aparecem em inglês.

A mesma pasta também aceita correções: um arquivo ali com o código de um catálogo que o editor
traz é aplicado por cima dele, texto por texto. Nesse caso, uma chave que você deixar de fora (ou um texto que não for
usado) mantém a tradução trazida; o inglês só aparece onde nenhum dos dois tem o texto.

Para que um catálogo seja trazido pelo editor, abra um pull request que o adicione a `editor/Core/Lang/` (chaves
só de `en.json`; `LangCatalogueTests` as confere).

## Traduzir a documentação

A janela de ajuda mostra a documentação no idioma do editor quando há uma tradução da página, e em inglês caso
contrário. Uma tradução da documentação é uma pasta de `docs/` com o nome do código do idioma, como o catálogo dele:
`docs/fr/`, `docs/pt-BR/`. O próprio `README.md` dela é o que faz dela uma tradução (a primeira página, e o sumário da
janela de ajuda); as pastas em inglês (`guides/`, `help/`, `concepts/`, `start/`) não têm.

- **Páginas.** A pasta espelha as páginas em inglês: `docs/fr/map.md` traduz `docs/map.md`, e
  `docs/fr/guides/make-a-road.md` traduz `docs/guides/make-a-road.md`. Traduza as páginas que a janela de ajuda traz
  ([writing-docs.md](../dev/writing-docs.md) lista as que ela não traz). Uma página ainda não traduzida aparece em inglês,
  então uma tradução pode começar com poucas páginas.
- **Links.** Escreva-os como a página em inglês os escreve, a partir da pasta: `map.md#…` e `guides/x.md` a partir de
  `docs/fr/README.md`, `../area.md` a partir de `docs/fr/guides/`. Um link que leva para fora de `docs/` sobe um `../` a
  mais que o inglês (`../../README.md`, `../../editor/Core/Lang/en.json`), e o mesmo vale para um link para um arquivo de
  `docs/` que não é traduzido (`../worldeditor.schema.json`). Links para uma página que a pasta ainda não tem continuam
  funcionando no editor (ele mostra a página em inglês), mas não no GitHub até a página ser traduzida.
- **Imagens.** Uma imagem com palavras é refeita no seu idioma em `docs/fr/images/`, com o mesmo nome, e escrita
  `images/x.jpg`. Uma sem nada a traduzir pode continuar sendo a inglesa: `../images/x.jpg`. Enquanto uma imagem de
  `images/` não for adicionada à sua pasta, a janela de ajuda mostra a inglesa de mesmo nome.
- **Títulos.** Traduza-os; os ids mudam com eles (o GitHub os cria a partir das palavras: `#search-the-world`
  vira `#rechercher-dans-le-monde`), e os links da própria tradução usam os ids traduzidos, como o GitHub exige. Mantenha
  os títulos da página em inglês: a mesma quantidade, dos mesmos níveis, na mesma ordem. O editor então também leva os
  ids em inglês aos títulos traduzidos, para que os botões de ajuda dele (`F1`, o `?` dos painéis), que nomeiam títulos em
  inglês, caiam no lugar certo.
- **Marca de origem.** Ponha uma linha na página, em primeiro lugar, dizendo de que texto em inglês ela foi traduzida:
  `<!-- source: 1a2b3c4d -->`, os 8 primeiros caracteres do SHA-256 da página em inglês (nem o GitHub nem a janela de
  ajuda a mostram). Obtenha-os com `sha256sum docs/map.md | cut -c1-8` (macOS: `shasum -a 256`). A marca é calculada
  sobre a página com finais de linha Unix: num checkout do Windows que os transformou em `\r\n`, use
  `git show HEAD:docs/map.md | sha256sum | cut -c1-8`, que dá a página como o repositório a guarda.

Quando a página em inglês muda, a marca dela muda: a janela de ajuda continua mostrando a tradução, com um aviso de que a
página mudou em inglês e um botão que mostra a página em inglês. Para atualizar uma tradução, veja o que mudou em inglês
desde então (`git log -p docs/map.md`), traduza e grave a nova marca. Os testes conferem todos os
links e imagens de uma tradução, e que uma tradução atualizada tenha os títulos da página em inglês.

## Para desenvolvedores

- Um texto que o usuário vê vai em `en.json`, sob uma chave com o nome do lugar onde aparece
  (`settings.port`, `common.cancel` para palavras usadas em toda parte), e é pedido com
  `Lang.T("settings.port")`, ou `Lang.T("settings.worldNotFound", path)` com valores para os marcadores
  `{0}`… dele (inseridos com a cultura invariante). Uma dica de passar o mouse é uma chave `tip.`, dada com
  `.Tip("settings.port")` (veja `editor/Desktop/Tips.cs`).
- Escreva a chave por extenso no código, mesmo quando ela é repassada: `LangCatalogueTests` confere que toda
  chave pedida está em `en.json` e que toda chave de `en.json` é pedida em algum lugar.
- Os textos para o Claude (as descrições e respostas das ferramentas MCP), o log, a saída de linha de comando e os
  formatos de arquivo ficam em inglês e fora do catálogo. Um texto mostrado ao usuário que também é dado ao
  Claude (o custo de uma peça) pede o inglês com `Lang.En` do lado do Claude.
- As janelas são construídas uma vez, no idioma escolhido ao iniciar (`Lang.Use` em `Program.Main`); nada
  precisa ficar atento a uma troca de idioma.

### Nomear chaves

- A primeira parte é a janela, escrita como nas chaves de dica dela (`area`, `place`, `select`, `path`,
  `blueprints`, `newWorld`…). Uma ferramenta renomeada na janela mantém as chaves: as da ferramenta Região são `area.*`, e os
  tipos da ferramenta Relevo mantêm `shape.*` (Fórmula) e `mountain.*` ao lado de `landform.*`. Prefixos compartilhados: `common.*` para uma palavra usada em três janelas ou mais
  (OK, Sim, Cancelar, Salvar, Descartar, Ficar, Mantê-los…), `dialog.*` para os pequenos diálogos, `save.*` para
  as perguntas de salvar, aplicar, descartar e sair, `biome.*` para nomes de biomas.
- A parte do meio diz que tipo de texto é: `label` (rótulos de linha, botões, interruptores), `head`
  (títulos de seção), `hint` (explicações discretas), `msg` (barra de status e resultados), `undo` (rótulos do
  Histórico), `error` (problemas mostrados num painel), `ask.<name>.title|text|yes|no` (perguntas), ou o
  nome de uma lista para as entradas dela (`brush.falloff.flatTop`). Os textos avulsos de uma janela podem ter só duas
  partes (`settings.port`). A última parte é em camelCase.
- Uma palavra em inglês tem uma chave: reutilize `common.*` em vez de acrescentar "Save" de novo. Os nomes que o Claude
  dá aos controles da janela são deduzidos do inglês (`Lang.EnglishOf`), que precisa disso.
- Um texto levado para o catálogo mantém a redação; mude palavras numa alteração separada.
- Os textos de um enum vêm de um `switch` que nomeia cada chave completa, nunca de `"prefix." + value`.

### Valores, plurais e frases

- Os valores vão em `{0}`, `{1}`… com o formato e a unidade dentro do texto (`"{0:0.0} m"`,
  `"{0} × {1} zones"`), como números e nomes crus, não como textos já formatados. Uma chave é sempre pedida
  com a mesma quantidade de valores; um texto pedido com valores duplica as chaves literais (<code>&#123;&#123;</code>),
  um pedido sem valores nunca as tem.
- Um nome que já está no idioma do usuário (um bioma, um tipo de objeto) entra como valor. Não
  mude a caixa dele para encaixá-lo numa frase (`ToLowerInvariant`): escreva a frase em volta dele.
- Não há regras de plural. Mantenha "object(s)" num único texto; onde o código já escolhe entre
  um e vários, use duas chaves completas (`….one`, `….many`). Nunca acrescente uma terminação no código.
- Uma chave por frase inteira. Uma frase extra opcional é uma chave própria, unida com um espaço;
  nunca cole partes de uma frase. Listas de nomes traduzidos são unidas com ", ".

### O que fica em inglês

- Tudo o que o Claude lê: as descrições e respostas das ferramentas MCP (`ClaudeTools*`, `ClaudeGuide`),
  o log e o console, a saída de linha de comando, os formatos de arquivo (`prefs.json`, o arquivo de histórico,
  os projetos), os dados do jogo (nomes de prefabs, itens e peças, chaves de dados de objetos), os nomes de produtos e
  de mods dentro dos textos (Valheim, Homestead, Steam) e as mensagens das exceções (a frase
  em volta de uma é traduzida).
- Os passos do Claude no Histórico começam com a marca `Claude: ` (`ClaudeTools.ClaudesStep`), que
  fica em inglês: Reverter as alterações do Claude encontra os passos do Claude por ela. As palavras depois dela são
  traduzidas.
- Um texto mostrado ao usuário que também é dado ao Claude tem as duas versões: o idioma do usuário via `Lang.T`,
  o inglês do Claude via `Lang.En` (`PieceCost.Describe(english: true)`).
- A mensagem da barra de status é relida pelo Claude. Ponha-a ali com `Msg("area.msg.applied", n)`,
  ou `Msg(said)` com um `Said` (`Said.Of(key, values…)`, `Said.Raw(text)` para um texto igual nos
  dois, `+` para acrescentar uma frase): a janela mostra o idioma do usuário e guarda o inglês, que as
  respostas do Claude leem (`MainWindow.LastMessageEn`). Acrescente à última mensagem com
  `LastSaid + …`, nunca lendo `MessageText.Text`.
- Uma escolha de lista lembrada (`Prefs.Bind`) é guardada em inglês, para que volte em qualquer idioma.

### Conferir uma janela

Os testes têm um idioma inventado, `PseudoLang`, que envolve cada texto do catálogo em `⟦…⟧`.
`PseudoLang.Untranslated(window)` lista os textos que uma janela mostra fora de `⟦⟧`, ou seja, que não vêm
do catálogo. Cada janela tem um teste `<Window>LangTests` que a constrói nesse idioma e
não espera nenhum; esses testes rodam na coleção `DataDir`, já que o idioma é compartilhado.
