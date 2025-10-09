// Lightweight date picker wrapper built on Air Datepicker
(function () {
    const instances = new Map();

    const DATE_FORMAT_INTERNAL = 'yyyy-MM-dd';
    const TIME_FORMAT_WITH_SECONDS = 'HH:mm:ss';
    const TIME_FORMAT = 'HH:mm';

    function parseDate(value, hasTime) {
        if (!value) {
            return null;
        }

        const parsed = new Date(value);
        if (isNaN(parsed.getTime())) {
            return null;
        }

        if (!hasTime) {
            parsed.setHours(0, 0, 0, 0);
        }

        return parsed;
    }

    function buildIntlOptions(pattern) {
        const options = {};
        const map = [
            { token: 'yyyy', action: () => (options.year = 'numeric') },
            { token: 'yy', action: () => (options.year = '2-digit') },
            { token: 'MMMM', action: () => (options.month = 'long') },
            { token: 'MMM', action: () => (options.month = 'short') },
            { token: 'MM', action: () => (options.month = '2-digit') },
            { token: 'M', action: () => (options.month = 'numeric') },
            { token: 'dd', action: () => (options.day = '2-digit') },
            { token: 'd', action: () => (options.day = 'numeric') },
            { token: 'HH', action: () => (options.hour = '2-digit') },
            { token: 'H', action: () => (options.hour = 'numeric') },
            { token: 'hh', action: () => (options.hour = '2-digit') },
            { token: 'h', action: () => (options.hour = 'numeric') },
            { token: 'mm', action: () => (options.minute = '2-digit') },
            { token: 'm', action: () => (options.minute = 'numeric') },
            { token: 'ss', action: () => (options.second = '2-digit') },
            { token: 's', action: () => (options.second = 'numeric') }
        ];

        for (const entry of map) {
            if (pattern.includes(entry.token)) {
                entry.action();
            }
        }

        if (pattern.includes('tt') || pattern.includes('a')) {
            options.hour12 = true;
        }

        return options;
    }

    function formatDate(date, culture, pattern) {
        try {
            // If we have a pattern, use custom formatting that respects the pattern order
            if (pattern) {
                return formatDateWithPattern(date, pattern, culture);
            }
            
            // Fallback to browser's locale formatting
            const options = buildIntlOptions(pattern || '');
            if (Object.keys(options).length === 0) {
                return date.toLocaleDateString(culture);
            }
            return new Intl.DateTimeFormat(culture, options).format(date);
        } catch (err) {
            console.warn('Unable to format date', err);
            return date.toLocaleString();
        }
    }

    function formatDateWithPattern(date, pattern, culture) {
        const year = date.getFullYear();
        const month = date.getMonth() + 1;
        const day = date.getDate();
        const hours24 = date.getHours();
        const hours12 = hours24 % 12 || 12;
        const minutes = date.getMinutes();
        const seconds = date.getSeconds();
        const ampm = hours24 < 12 ? 'AM' : 'PM';

        const pad = (num, size = 2) => String(num).padStart(size, '0');

        // Get localized month names if needed
        let monthNamesLong, monthNamesShort;
        try {
            const dtf = new Intl.DateTimeFormat(culture, { month: 'long' });
            monthNamesLong = Array.from({length: 12}, (_, i) => {
                const d = new Date(2000, i, 1);
                return dtf.format(d);
            });
            const dtfShort = new Intl.DateTimeFormat(culture, { month: 'short' });
            monthNamesShort = Array.from({length: 12}, (_, i) => {
                const d = new Date(2000, i, 1);
                return dtfShort.format(d);
            });
        } catch {
            monthNamesLong = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
            monthNamesShort = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        }

        let result = pattern;
        
        // Replace tokens in order of longest to shortest to avoid conflicts
        result = result.replace(/yyyy/g, String(year));
        result = result.replace(/yy/g, pad(year % 100));
        result = result.replace(/MMMM/g, monthNamesLong[month - 1]);
        result = result.replace(/MMM/g, monthNamesShort[month - 1]);
        result = result.replace(/MM/g, pad(month));
        result = result.replace(/M/g, String(month));
        result = result.replace(/dd/g, pad(day));
        result = result.replace(/d/g, String(day));
        result = result.replace(/HH/g, pad(hours24));
        result = result.replace(/H/g, String(hours24));
        result = result.replace(/hh/g, pad(hours12));
        result = result.replace(/h/g, String(hours12));
        result = result.replace(/mm/g, pad(minutes));
        result = result.replace(/m/g, String(minutes));
        result = result.replace(/ss/g, pad(seconds));
        result = result.replace(/s/g, String(seconds));
        result = result.replace(/tt/gi, ampm);

        return result;
    }

    function getInstance(elementId) {
        return instances.get(elementId);
    }

    window.lmdDatePicker = {
        init(elementId, dotnetHelper, config) {
            const element = document.getElementById(elementId);
            if (!element) {
                console.warn('Date picker element not found', elementId);
                return;
            }

            this.destroy(elementId);

            const hasTime = !!config.enableTime;
            const includeSeconds = !!config.enableSeconds;
            const defaultDate = parseDate(config.defaultDate, hasTime);

            const picker = new AirDatepicker(element, {
                selectedDates: defaultDate ? [defaultDate] : [],
                multipleDates: false,
                autoClose: true,
                timepicker: hasTime,
                timeFormat: includeSeconds ? TIME_FORMAT_WITH_SECONDS : TIME_FORMAT,
                dateFormat: DATE_FORMAT_INTERNAL,
                minutesStep: 1,
                secondsStep: 1,
                onlyTimepicker: false,
                // Prevent Air Datepicker from formatting the input - we'll do it ourselves
                position: 'bottom left',
                locale: {
                    days: config.weekDayNames || [],
                    daysShort: config.weekDayNamesShort || [],
                    daysMin: config.weekDayNamesShort || [],
                    months: config.monthNames || [],
                    monthsShort: config.monthNamesShort || [],
                    firstDay: typeof config.firstDayOfWeek === 'number' ? config.firstDayOfWeek : 1,
                    dateFormat: DATE_FORMAT_INTERNAL,
                    timeFormat: includeSeconds ? TIME_FORMAT_WITH_SECONDS : TIME_FORMAT
                },
                onSelect: ({ date }) => {
                    // Update the input field with our culture-specific formatted date
                    if (date) {
                        element.value = formatDate(date, config.culture, config.formatPattern);
                    }
                    
                    if (!dotnetHelper) {
                        return;
                    }

                    try {
                        dotnetHelper.invokeMethodAsync('OnDateChanged', date ? date.toISOString() : null);
                    } catch (err) {
                        console.error('Failed to notify .NET about date change', err);
                    }
                },
                onShow: () => {
                    element.classList.add('datepicker-open');
                },
                onHide: () => {
                    element.classList.remove('datepicker-open');
                    
                    // Ensure correct format when picker closes
                    if (picker && picker.selectedDates && picker.selectedDates.length > 0) {
                        const selectedDate = picker.selectedDates[0];
                        element.value = formatDate(selectedDate, config.culture, config.formatPattern);
                    }
                }
            });

            // Air Datepicker will try to format the input - we need to override it aggressively
            // Use MutationObserver to watch for Air Datepicker's automatic formatting and override it
            if (defaultDate) {
                const correctValue = formatDate(defaultDate, config.culture, config.formatPattern);
                
                // Apply immediately
                element.value = correctValue;
                
                // Watch for Air Datepicker trying to change it back
                let observerTimeout = null;
                const observer = new MutationObserver(() => {
                    if (element.value !== correctValue && element.value) {
                        element.value = correctValue;
                    }
                });
                
                // Observe value changes for a short period
                observer.observe(element, { 
                    attributes: true, 
                    attributeFilter: ['value'] 
                });
                
                // Also use multiple timeouts to catch Air Datepicker's formatting at different lifecycle points
                setTimeout(() => { element.value = correctValue; }, 0);
                setTimeout(() => { element.value = correctValue; }, 10);
                setTimeout(() => { element.value = correctValue; }, 50);
                setTimeout(() => { 
                    element.value = correctValue;
                    observer.disconnect(); // Stop observing after 200ms
                }, 200);
            }

            let manualInputHandler = null;

            if (!config.allowManualInput) {
                element.setAttribute('readonly', 'readonly');
            } else {
                element.removeAttribute('readonly');
                manualInputHandler = () => {
                    const timestamp = Date.parse(element.value);
                    if (!Number.isNaN(timestamp)) {
                        const manualDate = new Date(timestamp);
                        picker.selectDate(manualDate, { silent: true });
                        element.value = formatDate(manualDate, config.culture, config.formatPattern);
                        if (dotnetHelper) {
                            dotnetHelper.invokeMethodAsync('OnDateChanged', manualDate.toISOString());
                        }
                    }
                };

                element.addEventListener('change', manualInputHandler);
            }

            instances.set(elementId, {
                picker,
                dotnetHelper,
                config,
                element,
                manualInputHandler
            });
        },

        setValue(elementId, value) {
            const instance = getInstance(elementId);
            if (!instance) {
                return;
            }

            const date = parseDate(value, !!instance.config.enableTime);

            if (!date) {
                instance.picker.clear({ silent: true });
                instance.picker.$el.value = '';
                return;
            }

            instance.picker.selectDate(date, { silent: true });
            
            // Apply culture formatting aggressively - Air Datepicker tries to override it
            const correctValue = formatDate(date, instance.config.culture, instance.config.formatPattern);
            const element = instance.picker.$el;
            
            // Immediate application
            element.value = correctValue;
            
            // Use multiple timeouts to catch Air Datepicker's formatting at different lifecycle points
            setTimeout(() => { element.value = correctValue; }, 0);
            setTimeout(() => { element.value = correctValue; }, 10);
            setTimeout(() => { element.value = correctValue; }, 50);
        },

        open(elementId) {
            const instance = getInstance(elementId);
            if (instance) {
                if (instance.element) {
                    instance.element.focus();
                }
                instance.picker.show();
            }
        },

        close(elementId) {
            const instance = getInstance(elementId);
            if (instance) {
                instance.picker.hide();
            }
        },

        destroy(elementId) {
            const instance = getInstance(elementId);
            if (!instance) {
                return;
            }

            instance.picker.destroy();
            if (instance.dotnetHelper) {
                instance.dotnetHelper.dispose();
            }

            if (instance.element && instance.manualInputHandler) {
                instance.element.removeEventListener('change', instance.manualInputHandler);
            }

            instances.delete(elementId);
        }
    };
})();
