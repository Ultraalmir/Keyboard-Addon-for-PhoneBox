# Project website (GitHub Pages)

The public project website is built from the static files in `site/` and deployed with GitHub Actions.

## Website URL

https://ultraalmir.github.io/Keyboard-Addon-for-PhoneBox/

## One-time repository setup

1. Open the repository **Settings → Pages**.
2. Under **Build and deployment → Source**, select **GitHub Actions**.
3. Save the setting if GitHub presents a Save button.

## Deployment

The workflow `.github/workflows/pages.yml` deploys the contents of `site/` when changes to `site/**` or the workflow are pushed to `main`. It can also be started manually from **Actions → Deploy project website → Run workflow**.

The workflow uses the official GitHub Pages actions: `actions/configure-pages`, `actions/upload-pages-artifact`, and `actions/deploy-pages`.

## Editing the page

- `site/index.html`: page structure and content.
- `site/assets/site.css`: visual design, responsive layout, colors and typography.
- `site/assets/site.js`: PT-BR / English translations and the interactive shortcut preview.
- `site/assets/*.svg`: illustrative diagrams and dashboard preview. These are original vector mockups, not screenshots captured from a live PhoneBox session.
- `site/assets/phonebox-keyboard.png` and `site/assets/favicon.ico`: project identity.

The page is static. It does not call the local PhoneBox agent, capture keyboard input, use analytics, or send visitor data to a backend. The demo buttons only update the visual mockup in the browser.

## Updating the release link

The download buttons point to GitHub's `/releases/latest` page. They therefore follow the newest published release without a website edit.
