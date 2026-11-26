(() => {
  'use strict';

  const messages = {
    'pt-BR': {
      navFeatures:'Recursos', navDemo:'Demonstração', navInstall:'Instalação', navFaq:'Dúvidas',
      eyebrow:'COMPLEMENTO INDEPENDENTE PARA PHONEBOX',
      heroTitle:'Suas páginas do PhoneBox.<br><em>Um atalho de distância.</em>',
      heroLead:'Navegue pelo painel do seu celular usando combinações globais do teclado. Configure cada tecla do seu jeito e deixe o PhoneBox sempre a um toque de distância — sem modificar o aplicativo original.',
      downloadLatest:'Baixar versão mais recente', viewSource:'Ver código-fonte', windowsOnly:'Windows 10 / 11 · x64', communityProject:'Projeto comunitário independente', running:'EM EXECUÇÃO',
      configurableActions:'ações configuráveis', quietMode:'Modo silencioso', trayOptional:'Ícone de bandeja opcional',
      statDirect:'atalhos diretos para páginas', statRelative:'ações de navegação relativa', statChanges:'arquivos do PhoneBox modificados', statLicense:'licença de código aberto',
      featuresEyebrow:'FEITO PARA O SEU FLUXO', featuresTitle:'Pequeno no Windows.<br><em>Grande no controle.</em>', featuresLead:'O add-on roda em segundo plano e transforma atalhos do teclado em comandos de navegação para o agente local do PhoneBox.',
      feature1Title:'Atalhos do seu jeito', feature1Text:'Configure até dez combinações diretas e escolha modificadores e teclas compatíveis com seu teclado.', feature1Tag:'10 páginas diretas',
      feature2Title:'Próxima e anterior', feature2Text:'Avance ou volte pela lista completa de páginas habilitadas, inclusive quando seu painel tiver mais de dez.', feature2Tag:'Navegação relativa',
      feature3Title:'Global e discreto', feature3Text:'Os atalhos funcionam enquanto você usa outros programas. Sem console ou janela principal.', feature3Tag:'Processo em segundo plano',
      feature4Title:'Controle simples', feature4Text:'Ative ou desative cada ação, pause todos os atalhos e capture novas combinações no painel local.', feature4Tag:'Painel HTML local',
      feature5Title:'Dois idiomas', feature5Text:'A interface de configurações pode ser alternada entre Português (Brasil) e English.', feature5Tag:'PT-BR / EN',
      feature6Title:'Independente do PhoneBox', feature6Text:'Usa a API local do agente. Atualizações do PhoneBox não substituem os arquivos deste complemento.', feature6Tag:'Projeto independente',
      demoEyebrow:'VEJA COMO FUNCIONA', demoTitle:'Uma combinação.<br><em>Uma página.</em>', demoLead:'Os atalhos padrão usam Ctrl + Numpad. No painel, você pode capturar outras combinações para cada ação.',
      pageOne:'Página 1', pageTwo:'Página 2', pageThree:'Página 3', nextPage:'Próxima página', previousPage:'Página anterior',
      demoNote:'Prévia dos atalhos padrão. Todas as combinações podem ser personalizadas no painel.', phonePreview:'PRÉVIA DO PAINEL PHONEBOX', liveDemo:'DEMO', deskPanel:'PAINEL DE MESA', screenTitle:'Área de trabalho', screenSubtitle:'Seu computador, em um só lugar', activePage:'PÁGINA ATUAL', shortcutDemo:'NAVEGAÇÃO DE DEMONSTRAÇÃO', previous:'Anterior', next:'Próxima', demoInteractiveHint:'Demonstração visual: use os botões para simular a navegação. Isso não controla seu telefone.',
      shortcutAlt:'Mapa ilustrativo das combinações de teclado Ctrl e Numpad para páginas diretas e navegação relativa', shortcutMapTitle:'Mapa visual dos atalhos', shortcutMapDesc:'Exemplos de combinações diretas e ações Próxima/Anterior.', flowTitle:'Como o comando chega ao PhoneBox', flowDesc:'O complemento conversa com o agente local; nada é enviado para um serviço em nuvem.', flowAlt:'Fluxo: teclado, complemento Windows e painel PhoneBox', panelAlt:'Prévia ilustrativa do painel de configurações com atalhos, navegação relativa e preferências',
      installEyebrow:'COMECE EM POUCOS MINUTOS', installTitle:'Instalação sem complicação.', installLead:'O pacote distribuído já inclui o executável Windows autocontido e os arquivos necessários para instalar.',
      step1Title:'Baixe a versão', step1Text:'Abra a página de Releases e baixe o pacote mais recente para Windows x64.', step1Link:'Abrir Releases ↗',
      step2Title:'Instale o add-on', step2Text:'Extraia o ZIP, abra PowerShell na pasta extraída e execute o instalador incluído.',
      step3Title:'Configure e use', step3Text:'Teste a conexão com o agente PhoneBox, capture suas combinações e salve.', step3Note:'Pronto para navegar.',
      requirementsTitle:'Você vai precisar de', requirementsText:'Windows 10/11 x64 e do agente PhoneBox em execução no computador.', phoneboxProject:'Projeto PhoneBox ↗',
      faqEyebrow:'ANTES DE INSTALAR', faqTitle:'Perguntas rápidas.', faqLead:'O essencial sobre compatibilidade, privacidade e funcionamento.',
      faq1Q:'O add-on modifica o PhoneBox?', faq1A:'Não. É um complemento independente para Windows. Ele envia solicitações de navegação à API local do agente PhoneBox e não substitui nem injeta arquivos no aplicativo original.',
      faq2Q:'Ele funciona com outros programas abertos?', faq2A:'Sim. As combinações são registradas globalmente pelo Windows. Se outro aplicativo já usar a mesma combinação, o registro pode falhar; escolha outra tecla no painel.',
      faq3Q:'Preciso deixar uma janela aberta?', faq3A:'Não. O processo funciona em segundo plano, sem console ou janela principal. O ícone da bandeja é opcional e pode ser ativado nas preferências.',
      faq4Q:'Próxima e Anterior funcionam com mais de dez páginas?', faq4A:'Sim. Os dez atalhos diretos apontam às primeiras posições configuráveis; Próxima e Anterior percorrem a lista completa de páginas habilitadas. Como o agente não informa a página visível no telefone, o cursor local pode precisar ser sincronizado após navegação manual ou automática.',
      faq5Q:'O complemento envia meus dados para a nuvem?', faq5A:'O complemento não utiliza telemetria nem serviços em nuvem. Seu painel de configurações local escuta em loopback no próprio computador e a navegação é enviada ao agente local do PhoneBox.',
      ctaEyebrow:'SEU PAINEL, SEU TECLADO', ctaTitle:'Pronto para assumir o controle?', ctaLead:'Baixe a versão estável ou explore o código-fonte no GitHub.', downloadCta:'Baixar para Windows', starRepo:'Ver repositório ↗',
      footerNote:'Projeto comunitário independente. Não é um produto oficial nem afiliado aos mantenedores do PhoneBox.'
    },
    en: {
      navFeatures:'Features', navDemo:'Demo', navInstall:'Install', navFaq:'FAQ',
      eyebrow:'INDEPENDENT PHONEBOX ADD-ON',
      heroTitle:'Your PhoneBox pages.<br><em>One keystroke away.</em>',
      heroLead:'Navigate your phone dashboard with global keyboard shortcuts. Choose the combinations that fit your workflow and keep PhoneBox one keystroke away — without modifying the original app.',
      downloadLatest:'Download latest release', viewSource:'View source code', windowsOnly:'Windows 10 / 11 · x64', communityProject:'Independent community project', running:'RUNNING',
      configurableActions:'configurable actions', quietMode:'Quiet background mode', trayOptional:'Optional tray icon',
      statDirect:'direct page shortcuts', statRelative:'relative navigation actions', statChanges:'PhoneBox files modified', statLicense:'open-source license',
      featuresEyebrow:'BUILT AROUND YOUR WORKFLOW', featuresTitle:'Small on Windows.<br><em>Big on control.</em>', featuresLead:'The add-on runs in the background and turns keyboard shortcuts into navigation commands for the local PhoneBox agent.',
      feature1Title:'Shortcuts your way', feature1Text:'Configure up to ten direct combinations and choose modifiers and keys supported by your keyboard.', feature1Tag:'10 direct pages',
      feature2Title:'Next and Previous', feature2Text:'Move through the full list of enabled pages, including dashboards with more than ten pages.', feature2Tag:'Relative navigation',
      feature3Title:'Global and discreet', feature3Text:'Shortcuts work while you use other apps. No console or main window is required.', feature3Tag:'Background process',
      feature4Title:'Simple controls', feature4Text:'Enable or disable each action, pause all shortcuts, and capture new combinations in the local panel.', feature4Tag:'Local HTML panel',
      feature5Title:'Two languages', feature5Text:'Switch the settings interface between Brazilian Portuguese and English.', feature5Tag:'PT-BR / EN',
      feature6Title:'Independent from PhoneBox', feature6Text:'Uses the local agent API. PhoneBox updates do not replace this add-on’s files.', feature6Tag:'Independent project',
      demoEyebrow:'SEE HOW IT WORKS', demoTitle:'One combination.<br><em>One page.</em>', demoLead:'Default shortcuts use Ctrl + Numpad. You can capture a different combination for every action in the settings panel.',
      pageOne:'Page 1', pageTwo:'Page 2', pageThree:'Page 3', nextPage:'Next page', previousPage:'Previous page',
      demoNote:'Preview of the default shortcuts. Every combination can be customized in the settings panel.', phonePreview:'PHONEBOX PANEL PREVIEW', liveDemo:'DEMO', deskPanel:'DESK PANEL', screenTitle:'Workspace', screenSubtitle:'Your computer, at a glance', activePage:'CURRENT PAGE', shortcutDemo:'NAVIGATION PREVIEW', previous:'Previous', next:'Next', demoInteractiveHint:'Visual demo: use the buttons to simulate navigation. This does not control your phone.',
      shortcutAlt:'Illustrative map of Ctrl and Numpad combinations for direct pages and relative navigation', shortcutMapTitle:'Shortcut map', shortcutMapDesc:'Examples of direct combinations and Next/Previous actions.', flowTitle:'How the command reaches PhoneBox', flowDesc:'The add-on talks to the local agent; nothing is sent to a cloud service.', flowAlt:'Flow: keyboard, Windows add-on, and PhoneBox panel', panelAlt:'Illustrative settings panel preview with shortcuts, relative navigation, and preferences',
      installEyebrow:'GET STARTED IN MINUTES', installTitle:'Straightforward setup.', installLead:'The distributed package includes the self-contained Windows executable and the files required for installation.',
      step1Title:'Download the release', step1Text:'Open the Releases page and download the latest Windows x64 package.', step1Link:'Open Releases ↗',
      step2Title:'Install the add-on', step2Text:'Extract the ZIP, open PowerShell in the extracted folder, and run the included installer.',
      step3Title:'Configure and go', step3Text:'Test the PhoneBox agent connection, capture your combinations, and save.', step3Note:'Ready to navigate.',
      requirementsTitle:'What you need', requirementsText:'Windows 10/11 x64 and the PhoneBox agent running on your computer.', phoneboxProject:'PhoneBox project ↗',
      faqEyebrow:'BEFORE YOU INSTALL', faqTitle:'Quick answers.', faqLead:'The essentials about compatibility, privacy, and how it works.',
      faq1Q:'Does the add-on modify PhoneBox?', faq1A:'No. It is an independent Windows add-on. It sends navigation requests to the local PhoneBox agent API and does not replace or inject files into the original app.',
      faq2Q:'Does it work while other apps are open?', faq2A:'Yes. Shortcuts are registered globally by Windows. If another app already owns the same combination, registration may fail; choose a different key in the panel.',
      faq3Q:'Do I need to keep a window open?', faq3A:'No. The process runs in the background without a console or main window. The tray icon is optional and can be enabled in preferences.',
      faq4Q:'Do Next and Previous work with more than ten pages?', faq4A:'Yes. The ten direct shortcuts target the first configurable positions; Next and Previous traverse the full list of enabled pages. Because the agent does not report which page is currently visible on the phone, the local cursor may need syncing after manual or automatic navigation.',
      faq5Q:'Does the add-on send my data to the cloud?', faq5A:'The add-on uses no telemetry or cloud services. Its local settings panel listens on loopback on your computer, and navigation requests are sent to the local PhoneBox agent.',
      ctaEyebrow:'YOUR PANEL, YOUR KEYBOARD', ctaTitle:'Ready to take control?', ctaLead:'Download the stable release or explore the source code on GitHub.', downloadCta:'Download for Windows', starRepo:'View repository ↗',
      footerNote:'Independent community project. Not an official product and not affiliated with the PhoneBox maintainers.'
    }
  };

  const languageButtons = [...document.querySelectorAll('[data-language]')];
  function setLanguage(language) {
    const selected = messages[language] ? language : 'pt-BR';
    const dict = messages[selected];
    document.documentElement.lang = selected;
    document.title = selected === 'en' ? 'PhoneBox Keyboard Add-on — Your pages, one keystroke away' : 'PhoneBox Keyboard Add-on — Suas páginas, um atalho de distância';
    document.querySelectorAll('[data-i18n]').forEach((element) => {
      const value = dict[element.dataset.i18n];
      if (value !== undefined) element.innerHTML = value;
    });
    document.querySelectorAll('[data-alt]').forEach((element) => {
      const value = dict[element.dataset.alt];
      if (value) element.alt = value;
    });
    languageButtons.forEach((button) => {
      const active = button.dataset.language === selected;
      button.classList.toggle('active', active);
      button.setAttribute('aria-pressed', String(active));
    });
    try { localStorage.setItem('phonebox-keyboard-site-language', selected); } catch { /* Private browsing may disable storage. */ }
  }

  languageButtons.forEach((button) => button.addEventListener('click', () => setLanguage(button.dataset.language)));
  let initialLanguage = 'pt-BR';
  try {
    const stored = localStorage.getItem('phonebox-keyboard-site-language');
    if (stored && messages[stored]) initialLanguage = stored;
    else if (navigator.language?.toLowerCase().startsWith('en')) initialLanguage = 'en';
  } catch { /* Use Portuguese as the default. */ }
  setLanguage(initialLanguage);

  const demoPages = {
    'pt-BR': ['Início', 'Sistema', 'Controles', 'Mídia', 'Atalhos', 'Monitor', 'Rede', 'Memória', 'Processos', 'Notas', 'Ferramentas', 'Mais'],
    en: ['Home', 'System', 'Controls', 'Media', 'Shortcuts', 'Monitor', 'Network', 'Memory', 'Processes', 'Notes', 'Tools', 'More']
  };
  const pages = demoPages['pt-BR'];
  let pageIndex = 0;
  const counter = document.getElementById('demoCounter');
  const pageName = document.getElementById('demoPageName');
  const pageNumber = document.getElementById('demoPageNumber');
  const pageIndicators = [...document.querySelectorAll('#screenPages i')];
  function renderDemo() {
    const currentPages = demoPages[document.documentElement.lang] || demoPages['pt-BR'];
    pageName.textContent = currentPages[pageIndex];
    pageNumber.textContent = `${String(pageIndex + 1).padStart(2, '0')} / ${pages.length}`;
    counter.textContent = `${pageIndex + 1} / ${pages.length}`;
    pageIndicators.forEach((indicator, index) => indicator.classList.toggle('selected', index === pageIndex % pageIndicators.length));
  }
  document.getElementById('demoPrev').addEventListener('click', () => { pageIndex = (pageIndex + pages.length - 1) % pages.length; renderDemo(); });
  document.getElementById('demoNext').addEventListener('click', () => { pageIndex = (pageIndex + 1) % pages.length; renderDemo(); });
  languageButtons.forEach((button) => button.addEventListener('click', renderDemo));
  renderDemo();
})();
