// Tag persistence for Calendar pages
window.tagPersistence = {
    saveSelectedTag: function (tagId) {
        if (tagId) {
            localStorage.setItem('selectedTagId', tagId.toString());
        } else {
            localStorage.removeItem('selectedTagId');
        }
    },
    
    getSelectedTag: function () {
        const tagId = localStorage.getItem('selectedTagId');
        return tagId ? parseInt(tagId, 10) : null;
    },

    saveCalendarOrder: function (order) {
        if (order) {
            localStorage.setItem('calendarOrder', order);
        } else {
            localStorage.removeItem('calendarOrder');
        }
    },

    getCalendarOrder: function () {
        return localStorage.getItem('calendarOrder');
    }
};
