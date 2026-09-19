# GS Analytics — Sales Dashboard

A front-end sales analytics dashboard: login-gated navigation, KPI/chart views broken down by customer, product and city, a naive sales forecast, and working forms for adding customers, products and records.

**Stack:** HTML/CSS · Bootstrap 5 · Font Awesome 6 · Chart.js — all loaded from cdnjs, no vendored copies in the repo. No backend; data lives in the browser's `localStorage`, seeded from a small built-in demo dataset on first load.

## Structure

- `index.html` — login (entry point; any non-empty username/password "logs in" for the session)
- `main.html` — home dashboard (KPIs, monthly trend, category breakdown, recent orders)
- `salesByCustomers.html`, `salesByProduct.html`, `salesByCity.html` — searchable breakdown tables + charts
- `stats.html` — segment/category/city/top-customer charts
- `salesForecasting.html` — monthly trend plus a linear-projection forecast (clearly labeled as illustrative)
- `data.html` — full records table: search, select a row to edit (modal) or delete, export to CSV
- `addCustomer.html`, `addProduct.html`, `addRecord.html` — forms that write into the shared data store
- `JS/data.js` — the localStorage-backed data layer (records/customers/products, aggregation, forecasting)
- `JS/auth.js` — session-based login gate and logout
- `JS/nav.js` — shared sidenav + mobile nav toggle
- `JS/ui.js` — toast messages, CSV export
- `CSS/` — theme, login and form styling
- `Images/` — logo and background assets

## Running

Static site — serve the folder and open it in a browser:

```bash
npx serve .
```

This opens `index.html` first. Every other page checks for an active session and bounces back to the login page if you navigate to it directly; log out from the top-right link on any page.

## Note

This is still front-end only — there's no real backend or authentication, and the sales-forecast trend line is a simple linear fit for illustration, not a statistical model. Data you add persists in your browser's local storage, not on a server, so it's a demo/prototype rather than a production dashboard. To make it production-ready you'd want to swap `JS/data.js` for real API calls and add real authentication behind `index.html`.
