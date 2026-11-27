# Publicação e manutenção do repositório

Este documento é destinado a mantenedores e colaboradores. Usuários finais devem seguir as instruções de instalação e uso do `README.md`.

## Repositório oficial

- URL: https://github.com/Ultraalmir/Keyboard-Addon-for-PhoneBox
- Branch principal: `main`
- Última versão publicada: `1.5.2` (27/11/2026)
- Versão em preparação: `1.5.3`

## Requisitos para compilar

- Windows 11 x64 em uma versão compatível com .NET 10; ou Windows 10 somente em edições Enterprise/LTSC ainda dentro do ciclo de suporte da Microsoft e compatíveis com .NET 10. Consulte a [matriz de compatibilidade do .NET](https://learn.microsoft.com/dotnet/core/install/windows) e o [ciclo de vida do Windows](https://learn.microsoft.com/windows/release-health/release-information).
- .NET 10 SDK (10.0.x).
- Git for Windows para operações de versionamento e publicação.

Consulte a [matriz oficial de sistemas operacionais suportados pelo .NET](https://learn.microsoft.com/dotnet/core/install/windows). O executável distribuído é autocontido; o SDK é necessário apenas para compilar a partir do código-fonte.

## Compilar, executar os testes e empacotar

Abra o PowerShell na raiz do repositório e execute, nesta ordem:

```powershell
.\scripts\validate-version.ps1
.\scripts\validate-powershell.ps1
.\scripts\publish-windows.ps1
.\scripts\package.ps1
```

`validate-version.ps1` confirma que `Version` e `InformationalVersion` do aplicativo e do launcher, `ProductInfo.Version`, os badges dos dois READMEs e a primeira seção versionada do changelog estão sincronizados. `validate-powershell.ps1` analisa a sintaxe de todos os `.ps1` em `scripts/` e nos helpers incorporados em `src/PhoneBoxKeyboardAddon/`, incluindo o helper de limpeza da instalação. O `package.ps1` verifica as versões embutidas e as datas dos arquivos-fonte para não reutilizar artefatos desatualizados e valida o inventário e os hashes do ZIP concluído antes de escrever o checksum externo.

O script `publish-windows.ps1` restaura as dependências, compila o projeto principal em Release, executa os testes internos (`--self-test`) e publica dois executáveis Windows `win-x64` autocontidos: o aplicativo em `artifacts\publish\PhoneBoxKeyboardAddon.exe` e o launcher em `artifacts\publish-launcher\Abrir-Configuracoes.exe`. O launcher incorpora o mesmo ícone ICO do aplicativo.

O script `package.ps1` reúne os arquivos publicados, o README, o launcher, a licença e os scripts de instalação/desinstalação em `artifacts\PhoneBoxKeyboardAddon-win-x64.zip`. Também gera `PhoneBoxPackage.manifest` dentro do ZIP, com o inventário e os hashes SHA-256 dos arquivos que serão usados para validar a remoção opcional da pasta extraída. Gera ainda `artifacts\SHA256SUMS.txt`, com o nome e o SHA-256 do ZIP, no mesmo formato que o atualizador espera. Os workflows CI e Release chamam esses mesmos scripts para evitar divergência de empacotamento.

### Validação manual

Depois da publicação, os comandos abaixo podem ser executados separadamente:

```powershell
& .\artifacts\publish\PhoneBoxKeyboardAddon.exe --self-test
& .\artifacts\publish\PhoneBoxKeyboardAddon.exe --settings
```

- `--self-test` executa as verificações internas e retorna um código de saída apropriado para automação.
- `--settings` inicia ou reutiliza o processo do complemento e abre o painel local de configurações no navegador.
- `Abrir-Configuracoes.exe` abre o mesmo painel na instalação padrão e exibe uma mensagem informativa quando o aplicativo ainda não está instalado. O instalador copia o launcher para a pasta do aplicativo, junto do executável principal.

Se a política de execução do PowerShell bloquear os scripts, use um bypass apenas para a sessão atual (não altere a política permanente da máquina):

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
```

## Primeiro envio de um clone/fonte novo

Execute na raiz do projeto. Se o repositório Git já estiver inicializado e o remoto já estiver configurado, não repita `git init` nem `git remote add`.

```powershell
git init
git add .
git commit -m "Initial release: PhoneBox Keyboard Add-on 1.5.0"
git branch -M main
git remote add origin https://github.com/Ultraalmir/Keyboard-Addon-for-PhoneBox.git
git push -u origin main
```

## Atualizações posteriores

Após editar o código, documentação ou recursos, revise o diff e envie um commit normal:

```powershell
git status
git diff --check
git add .
git commit -m "Describe the change"
git push
```

Não inclua executáveis locais, arquivos `bin/`, `obj/`, `artifacts/`, logs ou configurações pessoais. O `.gitignore` exclui os diretórios de build e arquivos locais conhecidos; confira sempre `git status` antes de confirmar.

## CI e publicação de Release

O workflow **CI** é acionado em pushes para `main`/`master`, pull requests e execução manual. Ele valida a consistência das versões, analisa a sintaxe de todos os scripts PowerShell do instalador e dos helpers, restaura, compila, executa os self-tests, empacota e publica um pacote de teste como artefato.

Antes de publicar uma Release, confirme que o CI da versão desejada terminou com sucesso. Atualize a versão em todos os pontos de origem: `src/PhoneBoxKeyboardAddon/PhoneBoxKeyboardAddon.csproj` (`Version` e `InformationalVersion`), `src/PhoneBoxKeyboardAddon/AddonPaths.cs` (`ProductInfo.Version`), `src/SettingsLauncher/SettingsLauncher.csproj` e badges dos dois READMEs. Atualize o cabeçalho da versão no `CHANGELOG.md` de `Em preparação` para a data real no formato `YYYY-MM-DD`; atualize também `docs/PUBLISHING.md` para refletir a nova última versão publicada e a próxima versão em preparação, quando existir. Faça o commit e envie para `main`. O workflow de Release confere a tag, os metadados de versão e a data do changelog antes de compilar. Depois crie e envie a tag correspondente, substituindo `vX.Y.Z` pela versão escolhida:

```powershell
git tag vX.Y.Z
git push origin vX.Y.Z
```

O workflow **Release**, acionado por tags `v*`, primeiro exige que a tag corresponda à versão do aplicativo, do launcher e de `ProductInfo.Version`, e que o changelog contenha uma data válida para essa versão. Em seguida, analisa a sintaxe dos helpers, compila os dois executáveis Windows x64 autocontidos, valida o inventário e os hashes internos do ZIP, gera `SHA256SUMS.txt` e publica os dois assets na página **Releases** do GitHub. O launcher `Abrir-Configuracoes.exe` é incluído no ZIP oficial com ícone incorporado. O comando de publicação usa `--verify-tag` para confirmar que a tag existe.

## GitHub Pages

A página estática fica em `site/` e é publicada pelo workflow `.github/workflows/pages.yml` quando há alterações em `site/**` ou no próprio workflow. Para testar manualmente, use **Actions → Deploy project website → Run workflow**. As configurações do repositório devem usar **Settings → Pages → Build and deployment → Source: GitHub Actions**.

## Checklist do mantenedor

1. Valide as versões e a sintaxe dos scripts com `scripts\validate-version.ps1` e `scripts\validate-powershell.ps1`.
2. Compile em Windows 11 x64 ou em uma edição/versão Windows 10 LTSC/Enterprise suportada, usando o .NET 10 SDK: `scripts\publish-windows.ps1`.
3. Confirme os self-tests e siga `docs/TEST-PLAN.md` para os testes que dependem do Windows e do PhoneBox.
4. Confira os arquivos modificados com `git status` e `git diff --check`.
5. Atualize `CHANGELOG.md`, os READMEs e `docs/PUBLISHING.md` quando o comportamento, a compatibilidade, a instalação ou o estado da Release mudar.
6. Aguarde o CI ficar verde antes de criar uma tag de Release; o workflow também exige data real no cabeçalho do changelog.
7. Após a publicação, confira os arquivos, o checksum e as notas em GitHub Releases.
8. Se o site tiver sido modificado, confira também a execução do workflow GitHub Pages e a página pública.
9. Teste o launcher com e sem instalação, nas cópias da pasta extraída e da pasta instalada, além das combinações de desinstalação (preservar dados, remover dados e remover também a pasta extraída). Acrescente um arquivo pessoal e altere um arquivo do pacote para confirmar que o manifesto bloqueia a remoção da pasta.

Para instalações 1.5.2, a atualização incremental não conhece o caminho onde o ZIP foi extraído. Para habilitar a remoção opcional da pasta antiga, extraia o ZIP 1.5.3 sobre o mesmo diretório e execute `install.ps1` nesse local. Se o pacote 1.5.3 for extraído em outro diretório, o caminho antigo não poderá ser inferido e precisará ser removido manualmente.
