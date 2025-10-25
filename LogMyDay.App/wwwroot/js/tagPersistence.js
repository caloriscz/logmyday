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
    }
};
