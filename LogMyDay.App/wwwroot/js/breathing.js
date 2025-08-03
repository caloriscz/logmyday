// Breathing animation JavaScript functions - Sequential border trace
window.animateBreathingPhase = (phase, duration) => {
    const traceLeft = document.getElementById('traceLeft');
    const traceTop = document.getElementById('traceTop');
    const traceRight = document.getElementById('traceRight');
    const traceBottom = document.getElementById('traceBottom');
    
    if (!traceLeft || !traceTop || !traceRight || !traceBottom) {
        console.log('Trace elements not found');
        return;
    }
    
    console.log(`Starting breathing phase ${phase}, duration: ${duration}ms`);
    
    // Set transition duration for the current phase
    const transitionDuration = `${duration}ms`;
    
    // Map phases to trace segments and their properties
    const phaseConfig = {
        0: { // Inhale - Left side grows up
            element: traceLeft,
            class: 'trace-left',
            property: 'height',
            value: '100%'
        },
        1: { // Hold - Top side grows right
            element: traceTop,
            class: 'trace-top', 
            property: 'width',
            value: '100%'
        },
        2: { // Exhale - Right side grows down
            element: traceRight,
            class: 'trace-right',
            property: 'height', 
            value: '100%'
        },
        3: { // Hold - Bottom side grows left
            element: traceBottom,
            class: 'trace-bottom',
            property: 'width',
            value: '100%'
        }
    };
    
    const config = phaseConfig[phase];
    if (!config) {
        console.log(`Invalid phase: ${phase}`);
        return;
    }
    
    // Activate and animate the current segment
    config.element.style.opacity = '1';
    config.element.style.transition = `${config.property} linear ${transitionDuration}`;
    config.element.classList.add('active', 'animating');
    
    // Use setTimeout to ensure the transition is applied
    setTimeout(() => {
        config.element.style[config.property] = config.value;
        console.log(`Phase ${phase}: Animating ${config.property} to ${config.value}`);
    }, 50);
};

window.resetBreathingVisualization = () => {
    const segments = [
        document.getElementById('traceLeft'),
        document.getElementById('traceTop'),
        document.getElementById('traceRight'),
        document.getElementById('traceBottom')
    ];
    
    console.log('Resetting breathing border trace');
    
    segments.forEach(segment => {
        if (segment) {
            // Smooth fade out
            segment.style.transition = 'opacity 500ms ease-out, height 500ms ease-out, width 500ms ease-out';
            segment.style.opacity = '0';
            segment.style.height = '0%';
            segment.style.width = '0%';
            segment.classList.remove('active', 'animating');
            
            // Complete reset after animation
            setTimeout(() => {
                segment.style.transition = 'none';
                segment.style.height = '0%';
                segment.style.width = '0%';
                segment.style.opacity = '0';
            }, 600);
        }
    });
};
