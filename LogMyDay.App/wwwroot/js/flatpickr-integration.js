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
                }
            });

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

            if (defaultDate) {
                element.value = formatDate(defaultDate, config.culture, config.formatPattern);
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
            instance.picker.$el.value = formatDate(date, instance.config.culture, instance.config.formatPattern);
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
