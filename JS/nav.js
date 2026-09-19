/* GS Analytics — shared sidenav, topbar and mobile nav toggle.
   Kept in one place so every page's nav links and active state agree —
   previously each page hand-duplicated this list and drifted out of sync. */
(function (global) {
    'use strict';

    var NAV_ITEMS = [
        { page: 'main', href: 'main.html', label: 'Home', icon: 'fa-solid fa-house' },
        { page: 'customers', href: 'salesByCustomers.html', label: 'Customers', icon: 'fa-solid fa-users' },
        { page: 'products', href: 'salesByProduct.html', label: 'Products', icon: 'fa-solid fa-boxes-stacked' },
        { page: 'cities', href: 'salesByCity.html', label: 'Cities', icon: 'fa-solid fa-location-dot' },
        { page: 'stats', href: 'stats.html', label: 'Stats', icon: 'fa-solid fa-chart-area' },
        { page: 'addProduct', href: 'addProduct.html', label: 'New Product', icon: 'fa-solid fa-box' },
        { page: 'addRecord', href: 'addRecord.html', label: 'New Record', icon: 'fa-solid fa-database' },
        { page: 'addCustomer', href: 'addCustomer.html', label: 'New Customer', icon: 'fa-solid fa-user-plus' },
        { page: 'forecast', href: 'salesForecasting.html', label: 'Sales Forecast', icon: 'fa-solid fa-chart-line' },
        { page: 'data', href: 'data.html', label: 'Data', icon: 'fa-solid fa-table' }
    ];

    function renderNav() {
        var mount = document.getElementById('sidenav2');
        if (!mount) return;
        var current = document.body.getAttribute('data-page');
        mount.innerHTML = NAV_ITEMS.map(function (item) {
            var activeClass = item.page === current ? ' activePage' : '';
            return '<a class="left' + activeClass + '" href="' + item.href + '">' +
                '<span class="equal-padding">' + item.label + '</span>' +
                '<i class="faSide ' + item.icon + '"></i></a>';
        }).join('');
    }

    function renderTopbar() {
        var logoutEl = document.querySelector('[data-action="logout"]');
        if (logoutEl) {
            logoutEl.addEventListener('click', function (e) {
                e.preventDefault();
                if (global.GSAuth) global.GSAuth.logout();
            });
        }
        var userEl = document.querySelector('[data-user]');
        if (userEl && global.GSAuth) {
            userEl.textContent = global.GSAuth.currentUser();
        }
    }

    function initMobileToggle() {
        var toggle = document.getElementById('navToggle');
        var nav = document.getElementById('sidenav2');
        if (!toggle || !nav) return;

        var backdrop = document.createElement('div');
        backdrop.className = 'nav-backdrop';
        document.body.appendChild(backdrop);

        function closeNav() {
            nav.classList.remove('open');
            backdrop.classList.remove('show');
        }
        function toggleNav() {
            nav.classList.toggle('open');
            backdrop.classList.toggle('show');
        }

        toggle.addEventListener('click', toggleNav);
        backdrop.addEventListener('click', closeNav);
        nav.addEventListener('click', function (e) {
            if (e.target.closest('a')) closeNav();
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        renderNav();
        renderTopbar();
        initMobileToggle();
    });
})(window);
