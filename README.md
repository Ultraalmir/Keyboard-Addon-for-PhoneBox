# PhoneBox Keyboard Add-on

🌐 **Site do projeto:** [ultraalmir.github.io/Keyboard-Addon-for-PhoneBox](https://ultraalmir.github.io/Keyboard-Addon-for-PhoneBox/) · [Download mais recente](https://github.com/Ultraalmir/Keyboard-Addon-for-PhoneBox/releases/latest)

Complemento independente para Windows que permite navegar pelas páginas do PhoneBox com atalhos globais de teclado. Não altera, injeta ou substitui arquivos do PhoneBox.

![Versão](https://img.shields.io/badge/version-1.5.1-6478f5) ![Windows](https://img.shields.io/badge/platform-Windows%2011%20%7C%20supported%2010%20LTSC-0078D4) ![License](https://img.shields.io/badge/license-MIT-green)

## Recursos

- Até **10 atalhos diretos configuráveis**, um para cada uma das dez primeiras posições de página do PhoneBox.
- Atalhos separados e configuráveis para **Próxima página** e **Página anterior**. Eles percorrem a lista inteira de páginas habilitadas, mesmo quando o painel tem mais de dez páginas.
- Captura de combinações pelo botão **Capturar** da interface HTML local.
- Ativação/desativação global e individual para cada ação.
- Painel bilíngue com seletor Português (Brasil) / English; o idioma escolhido é salvo e mantido após reinicializações e atualizações.
- Operação silenciosa em segundo plano: sem console ou janela principal; o ícone da bandeja é opcional e controlado no painel HTML.
- Inicialização opcional com o Windows, usando um atalho na pasta Startup do usuário.
- Verificador de novas versões integrado ao painel e atualização incremental assistida, com checksum SHA-256, backup e rollback; atalhos, preferências e logs são preservados.
- Executável Windows x64 autocontido; o usuário final não precisa instalar o runtime .NET.

## Como os atalhos funcionam

A configuração inicial preserva os sete atalhos da primeira versão e adiciona os novos recursos:

| Ação | Atalho padrão | Estado inicial |
|---|---|---|
| Página 1 a 7 | `Ctrl + Numpad 1` a `Ctrl + Numpad 7` | Ativados |
| Página 8 | `Ctrl + Numpad 8` | Desativado |
| Página 9 | `Ctrl + Numpad 9` | Desativado |
| Página 10 | `Ctrl + Numpad 0` | Desativado |
| Próxima página | `Ctrl + Numpad +` | Ativado |
| Página anterior | `Ctrl + Numpad -` | Ativado |

Essas combinações são apenas os padrões. O usuário pode capturar outra combinação para cada uma das 12 ações, ativar/desativar ações individualmente e desligar todos os atalhos de uma vez. Pelo menos um modificador (`Ctrl`, `Alt`, `Shift` ou `Win`) é obrigatório. Combinações já registradas pelo Windows ou por outro aplicativo podem ser recusadas; o painel informa o conflito.

Para teclas numéricas do teclado numérico, mantenha o NumLock ligado. O sistema usa `RegisterHotKey` e os códigos virtuais do Windows, portanto teclas especiais do teclado podem ter comportamento diferente conforme o firmware do dispositivo.

## Próxima página e Página anterior

A partir do PhoneBox **0.9.31**, o agente oferece os webhooks oficiais `POST /api/goto/next` e `POST /api/goto/prev`. O complemento envia esses comandos diretamente ao PhoneBox, que consulta a página realmente exibida no telefone e controla os limites da lista: a navegação para na primeira e na última página, sem circular.

Isso mantém a navegação sincronizada mesmo quando a página muda por toque, gesto, regra do PhoneBox ou outro controlador. Para usar esses atalhos, atualize o PhoneBox para a versão 0.9.31 ou posterior.

**Perfis por telefone:** nesta versão, a descoberta usa a lista global de páginas retornada por `/api/config` sem o cabeçalho `X-PhoneBox-Device`. Se você configurou listas de páginas diferentes em PhoneBox → Settings → Phones para cada aparelho, confira se a lista global corresponde ao painel conectado; a seleção automática de perfil por telefone ainda não faz parte desta versão.

## Atualização automática

O atualizador integrado foi introduzido na versão 1.5.0. Instalações anteriores, como a 1.4.0, precisam ser atualizadas manualmente uma vez usando o instalador da Release 1.5.0; a partir dela, versões posteriores podem usar a atualização integrada. A atualização automática só funciona para instalações feitas pelo instalador do projeto.

O painel consulta as releases estáveis do GitHub. Quando encontra uma versão nova, permite baixar o pacote oficial, validar o SHA-256 e substituir somente o executável instalado. O complemento é fechado e reiniciado automaticamente; `settings.json`, logs e atalhos do Menu Iniciar permanecem intactos. A atualização automática exige que o complemento tenha sido instalado pelo instalador do projeto.

## Requisitos

- Windows 11 x64 em uma versão listada como compatível com .NET 10.
- Windows 10: somente edições Enterprise/LTSC em versões que ainda estejam dentro do ciclo de suporte da Microsoft e sejam compatíveis com .NET 10. Consulte a [matriz de compatibilidade do .NET](https://learn.microsoft.com/dotnet/core/install/windows) e o [ciclo de vida do Windows](https://learn.microsoft.com/windows/release-health/release-information).
- Agente PhoneBox em execução no computador. Endereço padrão: `http://127.0.0.1:8765`.


## Instalar uma versão publicada

1. Baixe `PhoneBoxKeyboardAddon-win-x64.zip` na página **Releases** do GitHub.
2. Extraia o pacote.
3. Abra PowerShell na pasta extraída e execute:

   ```powershell
   .\install.ps1 -LaunchSettings
   ```

4. Na interface local, teste a conexão com o PhoneBox, ajuste as combinações e clique em **Salvar**.
5. Para iniciar automaticamente com o Windows, habilite **Iniciar com o Windows** no painel ou instale com `-StartWithWindows`.

Se a política de execução do PowerShell bloquear o script, use apenas na sessão atual:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\install.ps1 -LaunchSettings
```

Essa política temporária termina quando a janela do PowerShell é fechada. O instalador não exige privilégios administrativos.


## Ícone opcional na bandeja do Windows

Na seção **Preferências** do painel HTML, marque **Mostrar ícone na bandeja** e salve para exibir o ícone personalizado PhoneBox Keyboard perto do relógio, sem reiniciar o complemento. O mesmo desenho também é usado como ícone do executável. O menu permite abrir as configurações, ativar/desativar os atalhos globais e sair. A opção vem desativada por padrão. Se o ícone estiver oculto, abra o painel pelo atalho **Configurações** no Menu Iniciar.

## Personalizar HTML e CSS

Os arquivos de interface ficam em:

- HTML: `src/PhoneBoxKeyboardAddon/Web/settings.html`
- CSS: `src/PhoneBoxKeyboardAddon/Web/settings.css`

O CSS foi separado do HTML para facilitar a edição do layout. Ambos são incorporados ao executável como recursos; depois de editar os arquivos, execute novamente `scripts\publish-windows.ps1` e atualize a instalação para aplicar as mudanças. Em execução, a folha de estilos é servida pelo endpoint local `http://127.0.0.1:<porta-do-painel>/settings.css`.

## Interface de configurações e dados locais

O processo de atalhos permanece oculto. A interface HTML é aberta somente quando o usuário escolhe **Configurações** no Menu Iniciar ou executa o aplicativo com `--settings`.

| Dados | Localização |
|---|---|
| Configurações | `%APPDATA%\PhoneBoxKeyboardAddon\settings.json` |
| Logs | `%LOCALAPPDATA%\PhoneBoxKeyboardAddon\Logs\addon.log` |
| Aplicativo | `%LOCALAPPDATA%\Programs\PhoneBoxKeyboardAddon` |
| Inicialização automática | Pasta Startup do usuário, quando habilitada |

As configurações são armazenadas fora da pasta de instalação e migradas automaticamente entre versões. A preferência `ShowTrayIcon` foi adicionada no esquema v3; arquivos anteriores recebem `false` por padrão, preservando o comportamento oculto e todos os atalhos existentes.

## Privacidade e segurança

- O servidor da interface escuta somente em `127.0.0.1`, normalmente na porta `47831` (com fallback até `47851`); não é exposto à rede local.
- O servidor valida os cabeçalhos `Host` e `Origin`, limita o tamanho das requisições e usa uma política CSP.
- O destino do PhoneBox é restrito a HTTP em endereço loopback. O cliente não usa proxy, não segue redirecionamentos e chama apenas os endpoints locais `/api/config`, `/api/push`, `/api/goto/{page}`, `/api/goto/next` e `/api/goto/prev`. O verificador de versões consulta somente a API pública de Releases do repositório GitHub do projeto.
- O complemento não usa conta, telemetria ou serviço de controle remoto. A única comunicação externa é a consulta/transferência opcional de releases do GitHub para verificar e atualizar o próprio complemento. Não solicita elevação de privilégios.
- O servidor local não usa token de autenticação: as verificações de `Host` e `Origin` protegem o painel contra acesso web remoto/rebinding, mas não são uma barreira contra outros processos já executados na mesma conta do Windows. Não execute software local não confiável junto ao complemento.
- Durante a captura de um atalho, os registros globais são suspensos temporariamente para que o navegador receba o teclado. Uma lease de 60 segundos restaura os atalhos se a página fechar inesperadamente.
- As releases não são assinadas com Authenticode nesta versão; o SmartScreen pode exibir um aviso para executáveis baixados. Verifique o código-fonte, a release e o SHA-256 antes de executar.

## Ícone do aplicativo e desinstalação

O aplicativo e o ícone opcional da bandeja usam o recurso visual `src/PhoneBoxKeyboardAddon/Web/phonebox-keyboard.ico` (também configurado como ícone do executável). O painel permite desinstalar o complemento sem executar scripts PowerShell: ele agenda um helper oculto que aguarda o processo encerrar e remove o executável instalado, os atalhos do Menu Iniciar e o atalho da pasta Startup. Por padrão, configurações e logs são preservados. Para apagá-los, o usuário precisa marcar explicitamente **Excluir também configurações e logs** e confirmar a ação.

O cartão **Atalhos registrados** recebe totais calculados pelo agente (`registeredCount`, `enabledRegistrationCount`, `actionCount`) e não depende da contagem no JavaScript. Isso evita que o painel mostre `0 de 0` por diferenças de serialização de nomes de propriedades.

## Limitações conhecidas

- Windows 11 x64 em versões compatíveis com .NET 10; Windows 10 somente em edições Enterprise/LTSC e versões que permaneçam dentro do ciclo de suporte da Microsoft e sejam compatíveis com .NET 10.
- O PhoneBox precisa estar em execução e acessível no endereço configurado.
- Próxima/Anterior exige PhoneBox 0.9.31 ou posterior, pois utiliza os webhooks nativos de navegação relativa.
- Se outra aplicação já possuir uma combinação global, o Windows pode recusar o registro. Escolha outra combinação no painel.
- O executável ainda não possui assinatura Authenticode.
- A captura global, USB, integração Startup e publicação autocontida precisam ser validadas em um Windows real. O ambiente de desenvolvimento não deve ser tratado como prova de funcionamento no computador do usuário.

## Desenvolvimento

Consulte `docs/ARCHITECTURE.md`, `docs/TEST-PLAN.md`, `docs/PUBLISHING.md`, `CONTRIBUTING.md` e `SECURITY.md`. As instruções de publicação e manutenção do repositório ficam em `docs/PUBLISHING.md`. Para relatar falhas, informe a versão do complemento, a versão do PhoneBox, o Windows, as combinações usadas e o trecho relevante do log (remova dados privados).

## Licença e afiliação

Licença MIT. Este é um complemento independente da comunidade; não é um componente oficial nem é afiliado aos mantenedores do PhoneBox.

## Cartão de estado

O cartão de estado apresenta separadamente o total de ações configuráveis, as ações habilitadas e os atalhos efetivamente registrados pelo Windows. Esses números podem diferir quando uma combinação está em conflito com outro aplicativo.

## Ícone do painel HTML

O painel local utiliza o ícone do complemento também como favicon do navegador. O servidor local disponibiliza os recursos incorporados nas rotas `/favicon.ico` e `/phonebox-keyboard.png`; a mesma imagem PNG é usada na marca ao lado do título. Para trocar o desenho, substitua `src/PhoneBoxKeyboardAddon/Web/phonebox-keyboard.ico` e `phonebox-keyboard.png`, mantendo os nomes dos arquivos, e publique novamente o executável.
