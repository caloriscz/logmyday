// Flatpickr integration for culture-aware date picking
window.flatpickrInstances = {};

window.initializeFlatpickr = function (elementId, dotnetHelper, config) {
    const element = document.getElementById(elementId);
    if (!element) {
        console.error('Flatpickr element not found:', elementId);
        return;
    }

    // Destroy existing instance if present
    if (window.flatpickrInstances[elementId]) {
        window.flatpickrInstances[elementId].destroy();
    }

    // Map DayOfWeek enum to flatpickr format (0 = Sunday, 1 = Monday, etc.)
    const firstDayOfWeek = config.firstDayOfWeek || 1; // Default to Monday

    // Parse the default date if provided (expecting ISO format from C#)
    let defaultDate = null;
    if (config.defaultDate) {
        try {
            defaultDate = new Date(config.defaultDate);
            // Validate the date
            if (isNaN(defaultDate.getTime())) {
                console.warn('Invalid date provided:', config.defaultDate);
                defaultDate = null;
            }
        } catch (e) {
            console.error('Error parsing default date:', e);
            defaultDate = null;
        }
    }

    // Configure flatpickr
    const flatpickrConfig = {
        dateFormat: config.dateFormat || 'Y-m-d',
        defaultDate: defaultDate,
        enableTime: config.enableTime || false,
        time_24hr: config.time24hr !== false, // Default to 24-hour
        allowInput: config.allowInput || false,
        locale: {
            firstDayOfWeek: firstDayOfWeek,
            weekdays: {
                shorthand: config.weekdaysShort || ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
                longhand: config.weekdaysLong || ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
            },
            months: {
                shorthand: config.monthsShort || ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
                longhand: config.monthsLong || ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
            }
        },
        onChange: function (selectedDates, dateStr, instance) {
            if (dotnetHelper && selectedDates.length > 0) {
                // Pass ISO 8601 format for reliable parsing in C#
                const isoDate = selectedDates[0].toISOString();
                dotnetHelper.invokeMethodAsync('OnDateChanged', isoDate);
            } else if (dotnetHelper && selectedDates.length === 0) {
                // Date was cleared
                dotnetHelper.invokeMethodAsync('OnDateChanged', null);
            }
        },
        onReady: function (selectedDates, dateStr, instance) {
            // Apply any custom styling or behavior
            instance.calendarContainer.classList.add('culture-aware-datepicker');
        }
    };

    // Create and store the flatpickr instance
    const fp = flatpickr(element, flatpickrConfig);
    window.flatpickrInstances[elementId] = fp;

    return true;
};

window.updateFlatpickr = function (elementId, value) {
    const instance = window.flatpickrInstances[elementId];
    if (instance) {
        instance.setDate(value, false); // Don't trigger onChange
        return true;
    }
    return false;
};

window.destroyFlatpickr = function (elementId) {
    const instance = window.flatpickrInstances[elementId];
    if (instance) {
        instance.destroy();
        delete window.flatpickrInstances[elementId];
        return true;
    }
    return false;
};

window.openFlatpickr = function (elementId) {
    const instance = window.flatpickrInstances[elementId];
    if (instance) {
        instance.open();
        return true;
    }
    return false;
};

window.closeFlatpickr = function (elementId) {
    const instance = window.flatpickrInstances[elementId];
    if (instance) {
        instance.close();
        return true;
    }
    return false;
};
