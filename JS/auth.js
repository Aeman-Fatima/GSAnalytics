/* GS Analytics — real authentication against the backend.
   The access token is a short-lived JWT held ONLY in a module-level variable — never in
   localStorage/sessionStorage, so it can't be read by an XSS payload that survives a page reload.
   Because this is a multi-page app (every navigation reloads the JS context), the token can't
   stay "in memory" across pages either — instead, each page silently re-derives a fresh access
   token from the HttpOnly/Secure/SameSite=None refresh cookie (set by the server) via requireAuth().
   SameSite=None (not Lax) because the frontend and API are served from different origins that also
   differ in scheme — see the comment in AuthController.cs for why Lax silently drops the cookie. */
(function (global) {
    'use strict';

    var accessToken = null;
    var currentUserInfo = null;

    function setSession(auth) {
        accessToken = auth.accessToken;
        currentUserInfo = {
            userId: auth.userId,
            email: auth.email,
            displayName: auth.displayName,
            businessId: auth.businessId,
            businessName: auth.businessName
        };
        return auth;
    }

    function clearSession() {
        accessToken = null;
        currentUserInfo = null;
    }

    var GSAuth = {
        getAccessToken: function () { return accessToken; },
        currentUser: function () { return currentUserInfo ? currentUserInfo.displayName : 'Demo User'; },
        currentBusinessName: function () { return currentUserInfo ? currentUserInfo.businessName : ''; },

        login: function (email, password) {
            return GSApi.json('/auth/login', { method: 'POST', body: JSON.stringify({ email: email, password: password }) })
                .then(setSession);
        },

        register: function (email, password, displayName, businessName) {
            return GSApi.json('/auth/register', {
                method: 'POST',
                body: JSON.stringify({ email: email, password: password, displayName: displayName, businessName: businessName })
            }).then(setSession);
        },

        // Only ever called with the HttpOnly refresh cookie doing the actual authenticating.
        refresh: function () {
            return GSApi.json('/auth/refresh', { method: 'POST', skipAuthRetry: true }).then(setSession);
        },

        logout: function () {
            return GSApi.request('/auth/logout', { method: 'POST', skipAuthRetry: true })
                .catch(function () { /* best-effort; clear client state regardless */ })
                .then(function () {
                    clearSession();
                    global.location.href = 'index.html';
                });
        },

        redirectToLogin: function () {
            clearSession();
            global.location.replace('index.html');
        },

        /* Call at the top of every protected page. Resolves with the current user once a valid
           access token is available (freshly refreshed if this page doesn't have one yet), or
           redirects to the login page and rejects if there is no valid session. */
        requireAuth: function () {
            if (accessToken) return Promise.resolve(currentUserInfo);
            return GSAuth.refresh().catch(function (err) {
                GSAuth.redirectToLogin();
                return Promise.reject(err);
            });
        },

        /* Used only by the login page itself, which must NOT redirect back to itself on failure. */
        tryResumeSession: function () {
            if (accessToken) return Promise.resolve(true);
            return GSAuth.refresh().then(function () { return true; }).catch(function () { return false; });
        }
    };

    global.GSAuth = GSAuth;
})(window);
