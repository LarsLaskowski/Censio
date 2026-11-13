window.censio = {
    storePreference: function (name, value) {
        document.cookie = name + "=" + value + "; Max-Age=31536000; Path=/; SameSite=Lax";
    },
    setDocumentLanguage: function (tag) {
        document.documentElement.lang = tag;
    },
    utcOffsetMinutes: function (isoInstant) {
        return -new Date(isoInstant).getTimezoneOffset();
    }
};
