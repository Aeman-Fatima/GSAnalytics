/* GS Analytics — data layer, backed by the real backend API.
   "Records" (one row per sale line item, e.g. for charts/aggregates) and "Sales" (one row per
   order, for the Data management page) are two different flattenings of the same /api/sales
   data — analytics needs per-item rows, order management needs per-order rows. */
(function (global) {
    'use strict';

    var CITIES = ['Adelaide', 'Norwood', 'Glenelg', 'Unley', 'Prospect', 'Semaphore', 'Henley Beach', 'Port Adelaide', 'Burnside', 'Mawson Lakes', 'Golden Grove', 'Salisbury'];

    function mapCustomer(c) { return { id: c.id, name: c.name, segment: c.segment, email: c.email, phone: c.phone }; }
    function mapProduct(p) { return { id: p.id, name: p.name, category: p.category }; }

    var GSData = {
        getCustomers: function () {
            return GSApi.json('/customers').then(function (customers) { return customers.map(mapCustomer); });
        },
        addCustomer: function (customer) {
            return GSApi.json('/customers', {
                method: 'POST',
                body: JSON.stringify({ name: customer.name, segment: customer.segment, email: customer.email, phone: customer.phone })
            }).then(mapCustomer);
        },

        getProducts: function () {
            return GSApi.json('/products').then(function (products) { return products.map(mapProduct); });
        },
        addProduct: function (product) {
            return GSApi.json('/products', {
                method: 'POST',
                body: JSON.stringify({ name: product.name, category: product.category })
            }).then(mapProduct);
        },

        getCities: function () { return CITIES.slice(); },

        getCustomersNeedingAttention: function () {
            return GSApi.json('/insights/customers-needing-attention');
        },

        getProductTrends: function () {
            return GSApi.json('/insights/product-trends');
        },

        /* One row per sale — for the Data management page (list/edit/delete whole orders). */
        getSales: function () {
            return GSApi.json('/sales').then(function (sales) {
                return sales.map(function (s) {
                    return {
                        id: s.id,
                        orderDate: s.saleDate,
                        customerId: s.customerId,
                        customerName: s.customerName,
                        city: s.city || '',
                        items: s.items,
                        itemCount: s.items.length,
                        total: s.total
                    };
                });
            });
        },

        /* Replaces a single-item sale's customer/product/date/city/amount in one shot. Only valid
           when the sale currently has exactly one item — multi-item orders keep their line items
           and can only have their date/city changed (see updateSaleDateAndCity). */
        updateSingleItemSale: function (saleId, fields) {
            return GSApi.json('/sales/' + saleId, {
                method: 'PUT',
                body: JSON.stringify({
                    customerId: fields.customerId,
                    saleDate: fields.orderDate,
                    city: fields.city,
                    items: [{ productId: fields.productId, quantity: 1, unitPrice: fields.sales }]
                })
            });
        },

        updateSaleDateAndCity: function (sale, orderDate, city) {
            return GSApi.json('/sales/' + sale.id, {
                method: 'PUT',
                body: JSON.stringify({
                    customerId: sale.customerId,
                    saleDate: orderDate,
                    city: city,
                    items: sale.items.map(function (i) { return { productId: i.productId, quantity: i.quantity, unitPrice: i.unitPrice }; })
                })
            });
        },

        deleteSale: function (saleId) {
            return GSApi.request('/sales/' + saleId, { method: 'DELETE' });
        },

        commitImport: function (fileName, rawContent, columnMapping, rows) {
            return GSApi.json('/imports/commit', {
                method: 'POST',
                body: JSON.stringify({ fileName: fileName, rawContent: rawContent, columnMapping: columnMapping, rows: rows })
            });
        },

        /* A single-product sale created from the "Add Record" form. */
        addRecord: function (record) {
            return GSApi.json('/sales', {
                method: 'POST',
                body: JSON.stringify({
                    customerId: record.customerId,
                    saleDate: record.orderDate,
                    city: record.city,
                    items: [{ productId: record.productId, quantity: 1, unitPrice: record.sales }]
                })
            });
        },

        /* One row per sale LINE ITEM — for charts and aggregate breakdowns. */
        getRecords: function () {
            return Promise.all([GSApi.json('/sales'), GSApi.json('/customers'), GSApi.json('/products')])
                .then(function (results) {
                    var sales = results[0], customers = results[1], products = results[2];

                    var segmentByCustomerId = {};
                    customers.forEach(function (c) { segmentByCustomerId[c.id] = c.segment || ''; });

                    var categoryByProductId = {};
                    products.forEach(function (p) { categoryByProductId[p.id] = p.category || ''; });

                    var records = [];
                    sales.forEach(function (s) {
                        s.items.forEach(function (item) {
                            records.push({
                                id: s.id + '#' + item.id,
                                saleId: s.id,
                                orderDate: s.saleDate,
                                customerId: s.customerId,
                                customerName: s.customerName,
                                segment: segmentByCustomerId[s.customerId] || '',
                                productId: item.productId,
                                productName: item.productName,
                                category: categoryByProductId[item.productId] || '',
                                city: s.city || '',
                                sales: item.lineTotal
                            });
                        });
                    });
                    return records;
                });
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
