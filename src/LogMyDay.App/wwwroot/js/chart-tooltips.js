/**
 * Chart tooltip formatters for ApexCharts.
 * Provides theme-aware custom tooltip rendering.
 */

const darkTheme = {
    background: '#1e293b',
    text: '#f1f5f9',
    dateText: '#94a3b8',
    shadow: '0 4px 6px rgba(0,0,0,0.3)'
};

const lightTheme = {
    background: '#ffffff',
    text: '#1e293b',
    dateText: '#64748b',
    border: '#e2e8f0',
    shadow: '0 4px 6px rgba(0,0,0,0.1)'
};

/**
 * Creates a tooltip formatter function for multi-series charts.
 * @param {boolean} hasDecimal - Whether to format values with decimal places.
 * @param {boolean} isDark - Whether dark mode is active.
 * @returns {string} The formatter function as a string for ApexCharts.
 */
export function createTooltipFormatter(hasDecimal, isDark) {
    const theme = isDark ? darkTheme : lightTheme;
    const formatValue = hasDecimal
        ? 'value.toFixed(2)'
        : 'Math.round(value)';
    
    const borderStyle = isDark ? '' : `border: 1px solid ${theme.border};`;
    
    return `function({series, seriesIndex, dataPointIndex, w}) {
        var date = new Date(w.globals.seriesX[seriesIndex][dataPointIndex]);
        var dateStr = date.toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' });
        var html = '<div style="padding: 10px; background: ${theme.background}; color: ${theme.text}; ${borderStyle} border-radius: 6px; box-shadow: ${theme.shadow};">';
        html += '<div style="font-size: 12px; margin-bottom: 6px; color: ${theme.dateText};">' + dateStr + '</div>';
        for (var i = 0; i < series.length; i++) {
            if (series[i][dataPointIndex] !== null && series[i][dataPointIndex] !== undefined) {
                var color = w.globals.colors[i];
                var seriesName = w.globals.seriesNames[i];
                var value = ${formatValue};
                html += '<div style="display: flex; align-items: center; margin-top: 4px;">';
                html += '<span style="display: inline-block; width: 10px; height: 10px; border-radius: 50%; background: ' + color + '; margin-right: 6px;"></span>';
                html += '<span style="font-size: 13px; font-weight: 500;">' + seriesName + ': ' + value + '</span>';
                html += '</div>';
            }
        }
        html += '</div>';
        return html;
    }`;
}

/**
 * Creates a Y-axis value formatter function.
 * @param {boolean} hasDecimal - Whether to format values with decimal places.
 * @returns {string} The formatter function as a string for ApexCharts.
 */
export function createValueFormatter(hasDecimal) {
    return hasDecimal
        ? 'function(value) { return value.toFixed(2); }'
        : 'function(value) { return value.toFixed(0); }';
}
