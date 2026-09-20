/* GS Analytics — shared API client.
   Talks to the ASP.NET Core backend. Adds the in-memory bearer token to every request, always
   sends credentials (so the HttpOnly refresh cookie goes along), and on a 401 tries exactly one
   silent refresh-and-retry before giving up. */
(function (global) {
    'use strict';

    var API_BASE = (global.location.hostname === 'localhost' || global.location.hostname === '127.0.0.1')
        ? 'https://localhost:5443/api'
        : '/api';

    function request(path, options) {
        options = options || {};
        var headers = Object.assign({ 'Content-Type': 'application/json' }, options.headers || {});
        var token = global.GSAuth && global.GSAuth.getAccessToken();
        if (token) headers.Authorization = 'Bearer ' + token;

        var fetchOptions = Object.assign({}, options, { headers: headers, credentials: 'include' });

        return fetch(API_BASE + path, fetchOptions).then(function (res) {
            if (res.status === 401 && !options.skipAuthRetry && !options._retried && global.GSAuth) {
                return global.GSAuth.refresh()
                    .then(function () { return request(path, Object.assign({}, options, { _retried: true })); })
                    .catch(function () {
                        global.GSAuth.redirectToLogin();
                        return Promise.reject(new Error('Your session has expired. Please log in again.'));
                    });
            }
            return res;
        });
    }

    function json(path, options) {
        return request(path, options).then(function (res) {
            if (!res.ok) {
                return res.json().catch(function () { return {}; }).then(function (body) {
                    throw new Error(body.message || ('Request failed (' + res.status + ')'));
                });
            }
            if (res.status === 204) return null;
            return res.json();
        });
    }

    global.GSApi = { API_BASE: API_BASE, request: request, json: json };
})(window);
