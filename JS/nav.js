/* GS Analytics — shared sidebar, topbar and mobile nav toggle.
   Kept in one place so every page's nav links and active state agree —
   previously each page hand-duplicated this list and drifted out of sync. */
(function (global) {
    'use strict';

    var NAV_GROUPS = [
        {
            label: 'Overview',
            items: [
                { page: 'main', href: 'main.html', label: 'Home', icon: 'fa-solid fa-house' },
                { page: 'customers', href: 'salesByCustomers.html', label: 'Customers', icon: 'fa-solid fa-users' },
                { page: 'products', href: 'salesByProduct.html', label: 'Products', icon: 'fa-solid fa-boxes-stacked' },
                { page: 'cities', href: 'salesByCity.html', label: 'Cities', icon: 'fa-solid fa-location-dot' },
                { page: 'stats', href: 'stats.html', label: 'Stats', icon: 'fa-solid fa-chart-area' },
                { page: 'forecast', href: 'salesForecasting.html', label: 'Sales Forecast', icon: 'fa-solid fa-chart-line' },
                { page: 'insights', href: 'insights.html', label: 'Insights', icon: 'fa-solid fa-lightbulb' }
            ]
        },
        {
            label: 'Manage',
            items: [
                { page: 'data', href: 'data.html', label: 'Data', icon: 'fa-solid fa-table' },
                { page: 'importData', href: 'importData.html', label: 'Import CSV', icon: 'fa-solid fa-file-csv' },
                { page: 'addRecord', href: 'addRecord.html', label: 'New Record', icon: 'fa-solid fa-database' },
                { page: 'addCustomer', href: 'addCustomer.html', label: 'New Customer', icon: 'fa-solid fa-user-plus' },
                { page: 'addProduct', href: 'addProduct.html', label: 'New Product', icon: 'fa-solid fa-box' }
            ]
        }
    ];

    function renderSidebar() {
        var mount = document.getElementById('sidebar');
        if (!mount) return;
        var current = document.body.getAttribute('data-page');

        var groupsHtml = NAV_GROUPS.map(function (group) {
            var linksHtml = group.items.map(function (item) {
                var activeClass = item.page === current ? ' active' : '';
                return '<a class="sidebar-link' + activeClass + '" href="' + item.href + '">' +
                    '<i class="' + item.icon + '"></i><span>' + item.label + '</span></a>';
            }).join('');
            return '<div class="sidebar-group"><div class="sidebar-group-label">' + group.label + '</div>' + linksHtml + '</div>';
        }).join('');

        mount.innerHTML =
            '<div class="sidebar-brand"><img src="Images/logoWhite.png" alt="" class="sidebar-logo"><span>GS Analytics</span></div>' +
            '<nav class="sidebar-nav">' + groupsHtml + '</nav>' +
            '<div class="sidebar-footer">' +
                '<div class="sidebar-user"><span class="user-avatar" id="userAvatar">D</span><span data-user>Demo User</span></div>' +
                '<a href="#" class="sidebar-logout" data-action="logout"><i class="fa-solid fa-right-from-bracket"></i> Logout</a>' +
            '</div>';
    }

    function renderUser() {
        var logoutEl = document.querySelector('[data-action="logout"]');
        if (logoutEl) {
            logoutEl.addEventListener('click', function (e) {
                e.preventDefault();
                if (global.GSAuth) global.GSAuth.logout();
            });
        }
        var userEl = document.querySelector('[data-user]');
        var avatarEl = document.getElementById('userAvatar');
        if (!global.GSAuth) return;

        // The page's own script also calls requireAuth(); by the time either resolves, a token
        // is cached in memory so this just reads it back rather than triggering a second refresh.
        global.GSAuth.requireAuth().then(function () {
            var name = global.GSAuth.currentUser();
            if (userEl) userEl.textContent = name;
            if (avatarEl) avatarEl.textContent = name.trim().charAt(0).toUpperCase() || 'U';
        }).catch(function () { /* requireAuth already redirects to the login page */ });
    }

    function initMobileToggle() {
        var toggle = document.getElementById('navToggle');
        var nav = document.getElementById('sidebar');
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
        renderSidebar();
        renderUser();
        initMobileToggle();
    });
})(window);
