# Personalização da interface

A interface de configuração usa dois arquivos fonte: `src/PhoneBoxKeyboardAddon/Web/settings.html` e `src/PhoneBoxKeyboardAddon/Web/settings.css`.

- Edite `settings.html` para alterar textos, seções, campos, botões e comportamento JavaScript do painel.
- Edite `settings.css` para alterar cores, fontes, espaçamentos, tamanhos, grades, estados e responsividade.
- A página HTML carrega a folha de estilos pela rota local `/settings.css`, servida exclusivamente em loopback.
- Os dois arquivos são incorporados ao executável durante a compilação. Depois de editar, execute `scripts\publish-windows.ps1` e reinstale/atualize o executável para aplicar as mudanças.

O painel não carrega CSS de CDN nem dependências externas. Mantenha scripts e estilos locais para preservar a política de segurança e o funcionamento offline.

## Ícone da bandeja

A opção `ShowTrayIcon` fica em `AddonSettings` e é editável na seção **Preferências** do HTML. O padrão é `false`. Ao salvar, o servidor local encaminha a alteração para a thread WinForms que controla `NotifyIcon`. O menu oferece **Abrir configurações**, alternância dos atalhos globais e **Sair**.

## Ícone do painel no navegador

O painel configura o favicon por meio dos recursos incorporados ao executável:

- `/favicon.ico`: ícone de aba tradicional do navegador.
- `/phonebox-keyboard.png`: imagem PNG usada também na marca do cabeçalho e como ícone touch.

Os elementos `link rel="icon"`, `link rel="apple-touch-icon"` e a imagem do cabeçalho ficam no `<head>` e na seção `.brand` de `Web/settings.html`. Os arquivos `.ico` e `.png` ficam na mesma pasta e são incorporados pelo `.csproj`. O servidor local entrega ambos os recursos diretamente da assembly, sem precisar manter imagens externas na pasta de instalação.

Para substituir o desenho, mantenha os nomes `phonebox-keyboard.ico` e `phonebox-keyboard.png`, altere os dois arquivos em `src/PhoneBoxKeyboardAddon/Web/` e compile/publicar novamente. Atualize também o parâmetro `v=` nos links do HTML para invalidar caches de navegador.

## Interface language

The settings page has a PT / EN selector in the top header. The selection is persisted as `uiLanguage` in the user settings JSON (`pt-BR` or `en`). New and upgraded installations default to Brazilian Portuguese. Static and dynamic UI labels are translated in the embedded `settings.html` dictionary (`I18N`).
