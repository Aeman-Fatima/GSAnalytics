# Sales Analytics Dashboard (UI)

A static front-end admin dashboard UI for a sales system: pages for managing customers, products, and records, plus a data/analytics view broken down by customer, product, and city.

**Stack:** HTML/CSS · Bootstrap · jQuery · Font Awesome

## Structure

- `addCustomer.html`, `addProduct.html`, `addRecord.html` — data entry forms
- `data.html` — analytics view (by customer / product / city)
- `CSS/`, `AwesomeOld/` — styling and icon assets
- `JQuery/`, `bootstrap/` — vendored libraries

## Running

This is a static site — open `data.html` (or any page) directly in a browser, or serve the folder with any static file server:

```bash
npx serve .
```

## Note

This is front-end only — there's no backend/API behind the forms, and `data.html` references a `JS/stats.js` chart script that isn't present in this repo, so the charts won't render as-is. Worth either wiring up a charting library (Chart.js is a quick drop-in) or noting it as a UI mockup rather than a working dashboard.
