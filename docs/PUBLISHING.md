# Publicação e manutenção do repositório

Este documento é destinado a mantenedores e colaboradores. Usuários finais devem seguir as instruções de instalação e uso do `README.md`.

## Repositório oficial

- URL: https://github.com/Ultraalmir/Keyboard-Addon-for-PhoneBox
- Branch principal: `main`
- Versão documentada neste pacote: `1.4.0`

## Primeiro envio de um clone/fonte novo

Execute na raiz do projeto. Se o repositório Git já estiver inicializado e o remoto já estiver configurado, não repita `git init` nem `git remote add`.

```powershell
git init
git add .
git commit -m "Initial release: PhoneBox Keyboard Add-on 1.4.0"
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

Não inclua executáveis locais, arquivos `bin/`, `obj/`, `artifacts/`, logs ou configurações pessoais. O `.gitignore` já exclui os diretórios de build e arquivos locais conhecidos; confira sempre `git status` antes de confirmar.

## CI e publicação de release

O workflow **CI** é acionado em pushes para `main`/`master`, pull requests e execução manual. Ele restaura, compila, executa os self-tests, valida os scripts PowerShell e publica um pacote de teste como artefato.

Para publicar uma release, primeiro confirme que o CI da versão desejada terminou com sucesso. Atualize a versão nos arquivos pertinentes (`README.md`, `README.en.md`, projeto e `CHANGELOG.md`), crie o commit e envie para `main`. Depois crie e envie a tag correspondente. Exemplo para `1.4.0`:

```powershell
git tag v1.4.0
git push origin v1.4.0
```

O workflow **Release**, acionado por tags `v*`, compila o executável Windows x64 autocontido, cria `PhoneBoxKeyboardAddon-win-x64.zip`, gera `SHA256SUMS.txt` e publica os arquivos na página **Releases** do GitHub.

## Checklist do mantenedor

1. Compile em Windows com o .NET 10 SDK: `scripts/publish-windows.ps1`.
2. Execute os self-tests (`--self-test`) e siga `docs/TEST-PLAN.md` para os testes que dependem do Windows e do PhoneBox.
3. Confira os arquivos modificados com `git status` e `git diff --check`.
4. Atualize `CHANGELOG.md` e ambos os READMEs quando o comportamento ou a instalação mudar.
5. Aguarde o CI ficar verde antes de criar uma tag de release.
6. Após a publicação, confira os arquivos, o checksum e as notas em GitHub Releases.
