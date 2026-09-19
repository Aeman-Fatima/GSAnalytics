/* GS Analytics — demo data layer.
   No backend exists, so records/customers/products live in localStorage,
   seeded from a deterministic starter dataset on first load. */
(function (global) {
    'use strict';

    var STORAGE_KEYS = {
        records: 'gsa.records',
        customers: 'gsa.customers',
        products: 'gsa.products'
    };

    var CITIES = ['Henderson', 'Los Angeles', 'San Francisco', 'Chicago', 'Seattle', 'New York City', 'Fort Lauderdale', 'Concord'];

    var DEFAULT_CUSTOMERS = [
        { id: 'CG-12520', name: 'Claire Gute', segment: 'Consumer' },
        { id: 'DV-13045', name: 'Darrin Van Huff', segment: 'Corporate' },
        { id: 'SO-20335', name: "Sean O'Donnell", segment: 'Consumer' },
        { id: 'BH-11710', name: 'Brosina Hoffman', segment: 'Home Office' },
        { id: 'AR-10480', name: 'Adam Rico', segment: 'Corporate' },
        { id: 'TB-21520', name: 'Tamara Black', segment: 'Consumer' },
        { id: 'NP-18325', name: 'Nathan Perez', segment: 'Home Office' },
        { id: 'JL-15835', name: 'Jasper Lee', segment: 'Corporate' }
    ];

    var DEFAULT_PRODUCTS = [
        { id: 'FUR-BO-10001798', name: 'Bush Somerset Collection Bookcase', category: 'Furniture' },
        { id: 'FUR-CH-10000454', name: 'Hon Deluxe Fabric Task Chair', category: 'Furniture' },
        { id: 'FUR-TA-10001728', name: 'Bretford Conference Table', category: 'Furniture' },
        { id: 'OFF-AR-10002833', name: 'Newell 341 Highlighter', category: 'Office Supplies' },
        { id: 'OFF-BI-10004632', name: 'Avery Durable Binder', category: 'Office Supplies' },
        { id: 'OFF-PA-10001970', name: 'Xerox 20lb Copy Paper', category: 'Office Supplies' },
        { id: 'TEC-PH-10002275', name: 'Plantronics Headset', category: 'Technology' },
        { id: 'TEC-AC-10003033', name: 'Logitech Wireless Mouse', category: 'Technology' },
        { id: 'TEC-MA-10001047', name: 'Canon Desktop Printer', category: 'Technology' }
    ];

    function generateRecords() {
        var records = [];
        var months = ['2024-01', '2024-02', '2024-03', '2024-04', '2024-05', '2024-06'];
        var baseByCategory = { Furniture: 320, 'Office Supplies': 45, Technology: 210 };
        var id = 1;
        months.forEach(function (month, mi) {
            var countThisMonth = 7 + (mi % 3);
            for (var i = 0; i < countThisMonth; i++) {
                var customer = DEFAULT_CUSTOMERS[(mi * 5 + i) % DEFAULT_CUSTOMERS.length];
                var product = DEFAULT_PRODUCTS[(mi * 3 + i * 2) % DEFAULT_PRODUCTS.length];
                var city = CITIES[(mi + i) % CITIES.length];
                var variance = ((mi * 37 + i * 53) % 260) - 60;
                var sales = Math.max(9.99, baseByCategory[product.category] + variance);
                var dd = String(1 + ((i * 4 + mi) % 27)).padStart(2, '0');
                records.push({
                    id: id++,
                    orderDate: month + '-' + dd,
                    customerId: customer.id,
                    customerName: customer.name,
                    segment: customer.segment,
                    productId: product.id,
                    productName: product.name,
                    category: product.category,
                    city: city,
                    sales: Math.round(sales * 100) / 100
                });
            }
        });
        return records;
    }

    function readStore(key, fallbackFn) {
        try {
            var raw = localStorage.getItem(key);
            if (raw) return JSON.parse(raw);
        } catch (e) { /* storage unavailable, fall through */ }
        var fallback = fallbackFn();
        writeStore(key, fallback);
        return fallback;
    }

    function writeStore(key, value) {
        try { localStorage.setItem(key, JSON.stringify(value)); } catch (e) { /* ignore quota/availability errors */ }
    }

    var GSData = {
        getRecords: function () { return readStore(STORAGE_KEYS.records, generateRecords); },
        saveRecords: function (records) { writeStore(STORAGE_KEYS.records, records); },
        addRecord: function (record) {
            var records = GSData.getRecords();
            record.id = records.length ? Math.max.apply(null, records.map(function (r) { return r.id; })) + 1 : 1;
            records.push(record);
            GSData.saveRecords(records);
            return record;
        },
        updateRecord: function (updated) {
            var records = GSData.getRecords().map(function (r) { return r.id === updated.id ? updated : r; });
            GSData.saveRecords(records);
        },
        deleteRecord: function (id) {
            var records = GSData.getRecords().filter(function (r) { return r.id !== id; });
            GSData.saveRecords(records);
        },

        getCustomers: function () { return readStore(STORAGE_KEYS.customers, function () { return DEFAULT_CUSTOMERS; }); },
        addCustomer: function (customer) {
            var customers = GSData.getCustomers();
            customers.push(customer);
            writeStore(STORAGE_KEYS.customers, customers);
            return customer;
        },

        getProducts: function () { return readStore(STORAGE_KEYS.products, function () { return DEFAULT_PRODUCTS; }); },
        addProduct: function (product) {
            var products = GSData.getProducts();
            products.push(product);
            writeStore(STORAGE_KEYS.products, products);
            return product;
        },

        getCities: function () { return CITIES.slice(); },

        resetDemoData: function () {
            try {
                localStorage.removeItem(STORAGE_KEYS.records);
                localStorage.removeItem(STORAGE_KEYS.customers);
                localStorage.removeItem(STORAGE_KEYS.products);
            } catch (e) { /* ignore */ }
        },

        aggregate: function (records, field) {
            var map = {};
            records.forEach(function (r) {
                var key = r[field];
                if (!map[key]) map[key] = { key: key, total: 0, count: 0 };
                map[key].total += r.sales;
                map[key].count += 1;
            });
            return Object.keys(map)
                .map(function (k) { return { key: map[k].key, total: Math.round(map[k].total * 100) / 100, count: map[k].count }; })
                .sort(function (a, b) { return b.total - a.total; });
        },

        monthly: function (records) {
            var map = {};
            records.forEach(function (r) {
                var month = r.orderDate.slice(0, 7);
                map[month] = (map[month] || 0) + r.sales;
            });
            return Object.keys(map).sort().map(function (m) { return { month: m, total: Math.round(map[m] * 100) / 100 }; });
        },

        /* Naive least-squares linear projection — a rough illustrative trend, not a real forecasting model. */
        forecast: function (series, periods) {
            var n = series.length;
            if (n < 2) return [];
            var sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            series.forEach(function (v, i) {
                sumX += i; sumY += v; sumXY += i * v; sumXX += i * i;
            });
            var slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;
            var result = [];
            for (var i = 0; i < periods; i++) {
                var x = n + i;
                result.push(Math.max(0, Math.round((slope * x + intercept) * 100) / 100));
            }
            return result;
        }
    };

    global.GSData = GSData;
})(window);
