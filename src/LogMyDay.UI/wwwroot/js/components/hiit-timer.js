// HIIT Timer JavaScript Module
export function initializeHiitTimer() {
    const prepareSeconds = 5;
    let setsTotal = 6;
    let workSeconds = 30; // 00:30 default
    let restSeconds = 15; // 00:15 default
    let editMinutes = false;

    let timer = null, endTime = 0, remaining = 0; // ms/seconds
    let phase = 'settings';
    let setsLeft = setsTotal;
    let paused = false;
    let phaseTotal = 0; // seconds for current phase

    const $ = s => document.querySelector(s);
    const format = s => { 
        s = Math.max(0, Math.round(s)); 
        const m = Math.floor(s / 60), sec = s % 60; 
        return `${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`; 
    };
    const clamp = (v, min, max) => Math.min(max, Math.max(min, v));

    // Expose state to Blazor
    window.hiitTimerState = {
        phase,
        setsTotal,
        setsLeft,
        workSeconds,
        restSeconds,
        paused,
        sessionStartTime: null
    };

    function setScreen(id) { 
        document.querySelectorAll('.screen').forEach(el => el.classList.remove('active')); 
        const screen = document.getElementById(id);
        if (screen) screen.classList.add('active'); 
    }
    
    function renderSettings() { 
        const setsVal = $('#setsVal');
        const workVal = $('#workVal');
        const restVal = $('#restVal');
        if (setsVal) setsVal.textContent = String(setsTotal); 
        if (workVal) workVal.textContent = format(workSeconds); 
        if (restVal) restVal.textContent = format(restSeconds); 
    }
    
    function updateSetsRemain() { 
        const setsRemain = $('#setsRemain');
        if (setsRemain) setsRemain.textContent = setsLeft; 
        window.hiitTimerState.setsLeft = setsLeft;
    }

    function startCountdown(seconds, label, color, next) {
        phase = label.toLowerCase();
        phaseTotal = seconds;
        window.hiitTimerState.phase = phase;
        
        const stage = $('#stage');
        const stageLabel = $('#stageLabel');
        const digits = $('#digits');
        
        if (stage) {
            stage.style.setProperty('--stage-color', color);
            stage.style.setProperty('--shade', '0');
        }
        if (stageLabel) stageLabel.textContent = label.toUpperCase();
        if (digits) digits.textContent = format(seconds);
        
        updateSetsRemain();
        
        // enable +1 SET only during workout/rest
        const addBtn = $('#addSetBtn');
        if (addBtn) addBtn.disabled = !(phase.includes('work') || phase.includes('rest'));

        endTime = Date.now() + seconds * 1000; 
        paused = false; 
        window.hiitTimerState.paused = paused;
        clearInterval(timer);
        
        timer = setInterval(() => {
            const now = Date.now();
            const rem = Math.max(0, (endTime - now) / 1000);
            const secs = Math.max(0, Math.ceil(rem));
            if (digits) digits.textContent = format(secs);

            // Subtle darken during last 5s only if phase >= 5s
            if (phaseTotal >= 5 && stage) {
                if (rem <= 5) {
                    const t = (5 - rem) / 5; // 0→1 over last 5s
                    const shade = (0.14 * Math.min(1, Math.max(0, t))).toFixed(3);
                    stage.style.setProperty('--shade', shade);
                } else {
                    stage.style.setProperty('--shade', '0');
                }
            }

            if (now >= endTime) { clearInterval(timer); next(); }
        }, 200);
    }

    function runSequence() { 
        setScreen('active'); 
        setsLeft = setsTotal; 
        window.hiitTimerState.setsLeft = setsLeft;
        window.hiitTimerState.sessionStartTime = new Date();
        
    const pauseBtn = $('#pauseBtn');
    if (pauseBtn) pauseBtn.textContent = 'PAUSE'; 
        startCountdown(prepareSeconds, 'Get Ready', getColor('prepare'), () => enterWork()); 
    }
    
    function enterWork() { 
        if (setsLeft <= 0) { finish(); return; } 
        startCountdown(workSeconds, 'Workout', getColor('work'), () => enterRest()); 
    }
    
    function enterRest() { 
        startCountdown(restSeconds, 'Rest', getColor('rest'), () => { 
            setsLeft--; 
            window.hiitTimerState.setsLeft = setsLeft;
            (setsLeft > 0) ? enterWork() : finish(); 
        }); 
    }
    
    function finish() { 
        const stageLabel = $('#stageLabel');
        const digits = $('#digits');
        const stage = $('#stage');
        
        if (stageLabel) stageLabel.textContent = 'DONE'; 
        if (digits) digits.textContent = '00:00'; 
        if (stage) stage.style.setProperty('--shade', '0'); 
        
        setTimeout(() => stopAll(), 1000); 
    }
    
    function stopAll() { 
        clearInterval(timer); 
        phase = 'settings'; 
        window.hiitTimerState.phase = phase;
        window.hiitTimerState.paused = false;
        setScreen('settings'); 
        renderSettings(); 
    }
    
    const getColor = kind => {
        const value = getComputedStyle(document.documentElement).getPropertyValue(`--${kind}`);
        return value ? value.trim() : '#ccc'; // fallback color
    };

    // settings events
    const setupEventListeners = () => {
        const setsMinus = $('#setsMinus');
        const setsPlus = $('#setsPlus');
        const workMinus = $('#workMinus');
        const workPlus = $('#workPlus');
        const restMinus = $('#restMinus');
        const restPlus = $('#restPlus');
        const startBtn = $('#startBtn');
        const pauseBtn = $('#pauseBtn');
        const stopBtn = $('#stopBtn');
        const addSetBtn = $('#addSetBtn');

        if (setsMinus) setsMinus.addEventListener('click', () => { 
            setsTotal = clamp(setsTotal - 1, 1, 999); 
            window.hiitTimerState.setsTotal = setsTotal;
            renderSettings(); 
        });
        
        if (setsPlus) setsPlus.addEventListener('click', () => { 
            setsTotal = clamp(setsTotal + 1, 1, 999); 
            window.hiitTimerState.setsTotal = setsTotal;
            renderSettings(); 
        });

        const STEP = 5;
        const stepWork = d => { 
            workSeconds = clamp(workSeconds + d * STEP, 5, 24 * 60 * 60); 
            window.hiitTimerState.workSeconds = workSeconds;
            renderSettings(); 
        };
        
        const stepRest = d => { 
            restSeconds = clamp(restSeconds + d * STEP, 5, 24 * 60 * 60); 
            window.hiitTimerState.restSeconds = restSeconds;
            renderSettings(); 
        };

        if (workMinus) workMinus.addEventListener('click', () => stepWork(-1));
        if (workPlus) workPlus.addEventListener('click', () => stepWork(1));
        if (restMinus) restMinus.addEventListener('click', () => stepRest(-1));
        if (restPlus) restPlus.addEventListener('click', () => stepRest(1));
        if (startBtn) startBtn.addEventListener('click', () => { renderSettings(); runSequence(); });

        // active controls
        if (pauseBtn) pauseBtn.addEventListener('click', () => {
            if (phase === 'settings') return;
            if (!paused) { 
                paused = true; 
                window.hiitTimerState.paused = paused;
                clearInterval(timer); 
                remaining = Math.max(0, Math.ceil((endTime - Date.now()) / 1000)); 
                pauseBtn.textContent = 'RESUME'; 
            } else { 
                paused = false; 
                window.hiitTimerState.paused = paused;
                pauseBtn.textContent = 'PAUSE'; 
                const stageLabel = $('#stageLabel');
                const stage = $('#stage');
                const label = stageLabel ? stageLabel.textContent : '';
                const color = stage ? getComputedStyle(stage).getPropertyValue('--stage-color') : '#ccc'; 
                startCountdown(remaining, label, color, () => { 
                    const l = label.toLowerCase(); 
                    if (l.includes('ready')) enterWork(); 
                    else if (l.includes('work')) enterRest(); 
                    else if (l.includes('rest')) { 
                        setsLeft--; 
                        window.hiitTimerState.setsLeft = setsLeft;
                        (setsLeft > 0) ? enterWork() : finish(); 
                    } 
                }); 
            }
        });

        if (stopBtn) stopBtn.addEventListener('click', stopAll);
        
        if (addSetBtn) addSetBtn.addEventListener('click', () => { 
            if (phase.includes('work') || phase.includes('rest')) { 
                setsLeft++; 
                setsTotal++; 
                window.hiitTimerState.setsLeft = setsLeft;
                window.hiitTimerState.setsTotal = setsTotal;
                updateSetsRemain(); 
            }
        });
    };

    // Initialize
    const init = () => {
        renderSettings();
        setupEventListeners();
    };

    return {
        init,
        start: runSequence,
        stop: stopAll,
        state: window.hiitTimerState
    };
}

// Export for Blazor components - no auto-initialization
