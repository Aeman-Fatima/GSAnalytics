/* GS Analytics — mock client-side auth.
   There is no backend, so "login" just gates navigation for this browser tab
   via sessionStorage. Any non-empty credentials are accepted. */
(function (global) {
    'use strict';

    var SESSION_KEY = 'gsa.session';
    var USER_KEY = 'gsa.user';

    var GSAuth = {
        isLoggedIn: function () {
            try { return sessionStorage.getItem(SESSION_KEY) === '1'; } catch (e) { return false; }
        },
        login: function (username) {
            try {
                sessionStorage.setItem(SESSION_KEY, '1');
                sessionStorage.setItem(USER_KEY, username || 'Demo User');
            } catch (e) { /* ignore */ }
        },
        logout: function () {
            try {
                sessionStorage.removeItem(SESSION_KEY);
                sessionStorage.removeItem(USER_KEY);
            } catch (e) { /* ignore */ }
            global.location.href = 'index.html';
        },
        currentUser: function () {
            try { return sessionStorage.getItem(USER_KEY) || 'Demo User'; } catch (e) { return 'Demo User'; }
        },
        requireAuth: function () {
            if (!GSAuth.isLoggedIn()) {
                global.location.replace('index.html');
            }
        }
    };

    global.GSAuth = GSAuth;
})(window);
