# OneGov web (Angular)

```powershell
npm ci
npm run i18n:check     # translation keys and placeholders match English
npm test               # unit tests (Ethiopian calendar)
npm start              # http://localhost:4200, /api and /health proxied to http://localhost:5080
npm run build          # production build in dist/onegov-web/browser
```
Start the API first (see the repository README) or the directory page will show a load error with a Try again button.

Translations live in `public/i18n/{en,am,om}.json`. Amharic and Afaan Oromo are machine drafts pending native-speaker review.
