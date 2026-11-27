# Publicação e manutenção do repositório

Este documento é destinado a mantenedores e colaboradores. Usuários finais devem seguir as instruções de instalação e uso do `README.md`.

## Repositório oficial

- URL: https://github.com/Ultraalmir/Keyboard-Addon-for-PhoneBox
- Branch principal: `main`
- Última versão publicada: `1.5.1`
- Versão em preparação: `1.5.2`

## Requisitos para compilar

- Windows 11 x64 em uma versão compatível com .NET 10; ou Windows 10 somente em edições Enterprise/LTSC ainda dentro do ciclo de suporte da Microsoft e compatíveis com .NET 10. Consulte a [matriz de compatibilidade do .NET](https://learn.microsoft.com/dotnet/core/install/windows) e o [ciclo de vida do Windows](https://learn.microsoft.com/windows/release-health/release-information).
- .NET 10 SDK (10.0.x).
- Git for Windows para operações de versionamento e publicação.

Consulte a [matriz oficial de sistemas operacionais suportados pelo .NET](https://learn.microsoft.com/dotnet/core/install/windows). O executável distribuído é autocontido; o SDK é necessário apenas para compilar a partir do código-fonte.

## Compilar, executar os testes e empacotar

Abra o PowerShell na raiz do repositório e execute, nesta ordem:

```powershell
.\scripts\publish-windows.ps1
.\scripts\package.ps1
```

O script `publish-windows.ps1` restaura as dependências, compila o projeto em Release, executa os testes internos (`--self-test`) e publica um executável Windows `win-x64` autocontido em `artifacts\publish\PhoneBoxKeyboardAddon.exe`.

O script `package.ps1` reúne o executável, o README, o launcher `Abrir-Configuracoes.bat`, a licença e os scripts de instalação/desinstalação em `artifacts\PhoneBoxKeyboardAddon-win-x64.zip`. Também gera o arquivo de checksum `artifacts\PhoneBoxKeyboardAddon-win-x64.zip.sha256`.

### Validação manual

Depois da publicação, os comandos abaixo podem ser executados separadamente:

```powershell
& .\artifacts\publish\PhoneBoxKeyboardAddon.exe --self-test
& .\artifacts\publish\PhoneBoxKeyboardAddon.exe --settings
```

- `--self-test` executa as verificações internas e retorna um código de saída apropriado para automação.
- `--settings` inicia ou reutiliza o processo do complemento e abre o painel local de configurações no navegador.

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

O workflow **CI** é acionado em pushes para `main`/`master`, pull requests e execução manual. Ele restaura, compila, executa os self-tests, valida os scripts PowerShell e publica um pacote de teste como artefato.

Antes de publicar uma Release, confirme que o CI da versão desejada terminou com sucesso. Atualize a versão em todos os pontos de origem: `src/PhoneBoxKeyboardAddon/PhoneBoxKeyboardAddon.csproj` (`Version` e `InformationalVersion`), `src/PhoneBoxKeyboardAddon/AddonPaths.cs` (`ProductInfo.Version`), badges dos dois READMEs e `docs/PUBLISHING.md`; registre as mudanças no `CHANGELOG.md`, crie o commit e envie para `main`. Depois crie e envie a tag correspondente, substituindo `vX.Y.Z` pela versão escolhida:

```powershell
git tag vX.Y.Z
git push origin vX.Y.Z
```

O workflow **Release**, acionado por tags `v*`, compila o executável Windows x64 autocontido, cria `PhoneBoxKeyboardAddon-win-x64.zip`, gera `SHA256SUMS.txt` e publica os arquivos na página **Releases** do GitHub.

## GitHub Pages

A página estática fica em `site/` e é publicada pelo workflow `.github/workflows/pages.yml` quando há alterações em `site/**` ou no próprio workflow. Para testar manualmente, use **Actions → Deploy project website → Run workflow**. As configurações do repositório devem usar **Settings → Pages → Build and deployment → Source: GitHub Actions**.

## Checklist do mantenedor

1. Compile em Windows 11 x64 ou em uma edição/versão Windows 10 LTSC/Enterprise suportada, usando o .NET 10 SDK: `scripts\publish-windows.ps1`.
2. Confirme os self-tests e siga `docs/TEST-PLAN.md` para os testes que dependem do Windows e do PhoneBox.
3. Confira os arquivos modificados com `git status` e `git diff --check`.
4. Atualize `CHANGELOG.md` e os READMEs quando o comportamento, a compatibilidade ou a instalação mudar.
5. Aguarde o CI ficar verde antes de criar uma tag de Release.
6. Após a publicação, confira os arquivos, o checksum e as notas em GitHub Releases.
7. Se o site tiver sido modificado, confira também a execução do workflow GitHub Pages e a página pública.
