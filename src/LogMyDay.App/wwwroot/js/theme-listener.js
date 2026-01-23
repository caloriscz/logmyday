let dotNetRef = null;
let observer = null;

export function setupThemeListener(dotNetReference) {
    dotNetRef = dotNetReference;
    
    // Watch for dark mode class changes on html element
    const targetNode = document.documentElement;
    
    const config = { attributes: true, attributeFilter: ['class'] };
    
    const callback = function(mutationsList, observer) {
        for (const mutation of mutationsList) {
            if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
                const isDark = document.documentElement.classList.contains('dark');
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnThemeChanged', isDark);
                }
            }
        }
    };
    
    observer = new MutationObserver(callback);
    observer.observe(targetNode, config);
    
    console.log('Theme listener setup complete');
}

export function cleanupThemeListener() {
    if (observer) {
        observer.disconnect();
        observer = null;
    }
    dotNetRef = null;
    console.log('Theme listener cleaned up');
}
