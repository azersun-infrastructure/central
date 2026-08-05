window.ocTheme = {
    get: function () {
        return document.documentElement.getAttribute('data-theme') || 'light';
    },
    set: function (theme) {
        localStorage.setItem('oc-theme', theme);
        document.documentElement.setAttribute('data-theme', theme);
    }
};
