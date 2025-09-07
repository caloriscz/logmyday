// Advanced Breathing Techniques JavaScript Module
export function initializeBreathing() {
    const els = {
        start: document.getElementById('startBtn'),
        pause: document.getElementById('pauseBtn'),
        reset: document.getElementById('resetBtn'),
        inhale: document.getElementById('inhaleSec'),
        hold: document.getElementById('holdSec'),
        exhale: document.getElementById('exhaleSec'),
        hold2: document.getElementById('hold2Sec'),
        hold2Field: document.getElementById('hold2Field'),
        cycles: document.getElementById('cycles'),
        phaseLabel: document.getElementById('phaseLabel'),
        countdown: document.getElementById('countdown'),
        cycleReadout: document.getElementById('cycleReadout'),
        viz: document.getElementById('viz'),
        pulseDot: document.getElementById('pulseDot'),

        bgTri: document.getElementById('bg-tri'),
        bgBox: document.getElementById('bg-box'),
        labs: {
            inhale: document.getElementById('lab-inhale'),
            hold: document.getElementById('lab-hold'),
            exhale: document.getElementById('lab-exhale'),
            hold2: document.getElementById('lab-hold2'),
        },
        paths: {
            inhale: document.getElementById('side-inhale'),
            hold: document.getElementById('side-hold'),
            exhale: document.getElementById('side-exhale'),
            hold2: document.getElementById('side-hold2'),
        },
        modeInputs: Array.from(document.querySelectorAll('input[name="mode"]')),
    };

    const titles = { inhale: 'Inhale', hold: 'Hold', exhale: 'Exhale', hold2: 'Hold 2' };

    const state = {
        running: false,
        paused: false,
        cycle: 0,
        cyclesTarget: 0,
        phaseIndex: 0,
        t0: 0,
        elapsed: 0,
        durationMs: 0,
        raf: 0,
        mode: 'triangle', // 'triangle' | 'box'
        sessionStartTime: null,
    };

    // Expose state to Blazor
    window.breathingState = state;

    function phasesOrder() {
        // Classic box breathing: inhale up the left, hold across the TOP, exhale down the right, hold across the BOTTOM
        return state.mode === 'box'
            ? ['inhale', 'hold2', 'exhale', 'hold']
            : ['inhale', 'hold', 'exhale'];
    }

    function fmt(t) { return t.toFixed(1).replace('.', ','); }

    function setButtons() {
        if (els.start) els.start.disabled = state.running && !state.paused;
        if (els.pause) els.pause.disabled = !state.running;
        if (els.reset) els.reset.disabled = !state.running;
        if (els.pause) els.pause.textContent = state.paused ? 'Resume' : 'Pause';
    }

    function readDurations() {
        const inh = Math.max(0.5, Number(els.inhale?.value) || 4);
        const h1 = Math.max(0.5, Number(els.hold?.value) || 7);   // bottom hold
        const exh = Math.max(0.5, Number(els.exhale?.value) || 8);
        const h2 = Math.max(0.5, Number(els.hold2?.value) || 4);   // top hold
        return state.mode === 'box'
            ? { inhale: inh, hold: h1, exhale: exh, hold2: h2 }
            : { inhale: inh, hold: h1, exhale: exh };
    }

    function readCycles() {
        return Math.max(1, Math.floor(Number(els.cycles?.value) || 4));
    }

    function preparePath(path) {
        if (!path) { return 0; }
        const len = path.getTotalLength();
        path.style.strokeDasharray = String(len);
        path.style.strokeDashoffset = String(len); // hidden
        return len;
    }

    function clearAllStrokes() {
        Object.values(els.paths).forEach(p => {
            if (!p) return;
            const len = p.getTotalLength();
            p.style.strokeDasharray = String(len);
            p.style.strokeDashoffset = String(len);
        });
    }

    function setPhaseVisual(which) {
        if (!els.viz) return;
        els.viz.classList.remove('state-inhale', 'state-hold', 'state-exhale', 'state-hold2');
        els.viz.classList.add('state-' + which);
        if (els.phaseLabel) {
            els.phaseLabel.className = 'phase ' + which;
            els.phaseLabel.textContent = titles[which];
        }
    }

    function animatePhase(which, seconds, onDone) {
        setPhaseVisual(which);
        const path = els.paths[which];
        const len = preparePath(path);
        state.durationMs = seconds * 1000;

        // pulse
        const dot = els.pulseDot;
        if (dot) {
            if (which === 'inhale') dot.animate([{ transform: 'scale(1)' }, { transform: 'scale(1.25)' }], { duration: state.durationMs, fill: 'forwards' });
            if (which === 'hold') dot.animate([{ transform: 'scale(1.25)' }, { transform: 'scale(1.25)' }], { duration: state.durationMs, fill: 'forwards' });
            if (which === 'exhale') dot.animate([{ transform: 'scale(1.25)' }, { transform: 'scale(0.9)' }], { duration: state.durationMs, fill: 'forwards' });
            if (which === 'hold2') dot.animate([{ transform: 'scale(0.9)' }, { transform: 'scale(0.9)' }], { duration: state.durationMs, fill: 'forwards' });
        }

        state.t0 = performance.now() - state.elapsed;
        cancelAnimationFrame(state.raf);

        const step = (now) => {
            if (!state.running) return;
            if (state.paused) { state.raf = requestAnimationFrame(step); return; }
            const p = Math.min(1, (now - state.t0) / state.durationMs);
            if (path) path.style.strokeDashoffset = String(len * (1 - p));
            const remaining = Math.max(0, state.durationMs - (now - state.t0)) / 1000;
            if (els.countdown) els.countdown.textContent = fmt(remaining);
            if (p >= 1) { state.elapsed = 0; onDone && onDone(); return; }
            state.raf = requestAnimationFrame(step);
        };
        state.raf = requestAnimationFrame(step);
    }

    function runNextPhase() {
        const order = phasesOrder();
        const durations = readDurations();
        if (state.phaseIndex >= order.length) {
            state.phaseIndex = 0;
            state.cycle++;
            if (els.cycleReadout) els.cycleReadout.textContent = `Cycle ${state.cycle}/${state.cyclesTarget}`;
            clearAllStrokes(); // Clear lines at end of each full cycle
            if (state.cycle >= state.cyclesTarget) { stopAll(); return; }
        }
        const which = order[state.phaseIndex];
        const secs = durations[which];
        animatePhase(which, secs, () => { state.phaseIndex++; runNextPhase(); });
    }

    function stopAll() {
        state.running = false; state.paused = false; state.elapsed = 0; cancelAnimationFrame(state.raf);
        Object.values(els.paths).forEach(p => { if (!p) return; p.style.strokeDasharray = '0'; p.style.strokeDashoffset = '0'; });
        if (els.countdown) els.countdown.textContent = '0,0';
        if (els.phaseLabel) els.phaseLabel.textContent = 'Done';
        if (els.viz) els.viz.classList.remove('state-inhale', 'state-hold', 'state-exhale', 'state-hold2');
        setButtons();
    }

    function reset() {
        state.running = false; state.paused = false; state.elapsed = 0; state.cycle = 0; state.phaseIndex = 0; cancelAnimationFrame(state.raf);
        clearAllStrokes();
        if (els.phaseLabel) {
            els.phaseLabel.textContent = 'Ready';
            els.phaseLabel.className = 'phase';
        }
        const first = phasesOrder()[0];
        const durs = readDurations();
        if (els.countdown) els.countdown.textContent = fmt(durs[first]);
        if (els.cycleReadout) els.cycleReadout.textContent = `Cycle 0/${readCycles()}`;
        if (els.viz) els.viz.classList.add('state-inhale');
        setButtons();
    }

    function start() {
        if (state.running && state.paused) { state.paused = false; setButtons(); return; }
        state.running = true; state.paused = false; state.cycle = 0; state.phaseIndex = 0; state.elapsed = 0;
        state.cyclesTarget = readCycles();
        state.sessionStartTime = new Date();
        if (els.cycleReadout) els.cycleReadout.textContent = `Cycle 0/${state.cyclesTarget}`;
        setButtons();
        runNextPhase();
    }

    function pauseResume() {
        if (!state.running) return;
        state.paused = !state.paused;
        if (state.paused) { state.elapsed = performance.now() - state.t0; }
        else { state.t0 = performance.now() - state.elapsed; }
        setButtons();
    }

    // --- Mode handling: Triangle vs Box (square) ---
    function updateMode(newMode) {
        state.mode = newMode;

        // Toggle backgrounds
        if (els.bgTri) els.bgTri.classList.toggle('hidden', newMode === 'box');
        if (els.bgBox) els.bgBox.classList.toggle('hidden', newMode !== 'box');

        // Geometry per mode
        if (newMode === 'box') {
            // Ensure the background is a classic square (not diamond)
            if (els.bgBox) els.bgBox.setAttribute('d', 'M 10 82 L 10 10 L 90 10 L 90 82 Z');

            // Side paths (clockwise loop):
            if (els.paths.inhale) els.paths.inhale.setAttribute('d', 'M 10 82 L 10 10');
            if (els.paths.hold2) els.paths.hold2.setAttribute('d', 'M 10 10 L 90 10');
            if (els.paths.exhale) els.paths.exhale.setAttribute('d', 'M 90 10 L 90 82');
            if (els.paths.hold) els.paths.hold.setAttribute('d', 'M 90 82 L 10 82');

            // Show fourth side & label; place labels around the square with arrows
            if (els.paths.hold2) els.paths.hold2.style.display = '';
            if (els.labs.hold2) els.labs.hold2.classList.remove('hidden');

            if (els.labs.inhale) els.labs.inhale.textContent = 'Inhale ↑';
            if (els.labs.exhale) els.labs.exhale.textContent = 'Exhale ↓';
            if (els.labs.hold) els.labs.hold.textContent = 'Hold ⏸';
            if (els.labs.hold2) els.labs.hold2.textContent = 'Hold 2 ⏸';

            // Positions
            if (els.labs.inhale) { els.labs.inhale.style.left = '3%'; els.labs.inhale.style.top = '35%'; }
            if (els.labs.exhale) { els.labs.exhale.style.right = '3%'; els.labs.exhale.style.top = '35%'; els.labs.exhale.style.textAlign = 'right'; }
            if (els.labs.hold2) { els.labs.hold2.style.left = '50%'; els.labs.hold2.style.top = '2%'; els.labs.hold2.style.transform = 'translateX(-50%)'; }
            if (els.labs.hold) { els.labs.hold.style.left = '50%'; els.labs.hold.style.bottom = '4%'; els.labs.hold.style.top = ''; els.labs.hold.style.transform = 'translateX(-50%)'; }
        } else {
            // Triangle geometry
            if (els.paths.inhale) els.paths.inhale.setAttribute('d', 'M 50 10 L 10 82');
            if (els.paths.hold) els.paths.hold.setAttribute('d', 'M 10 82 L 90 82');
            if (els.paths.exhale) els.paths.exhale.setAttribute('d', 'M 90 82 L 50 10');
            if (els.paths.hold2) els.paths.hold2.style.display = 'none';
            if (els.labs.hold2) els.labs.hold2.classList.add('hidden');

            // Reset label text/placement for triangle
            if (els.labs.inhale) els.labs.inhale.textContent = '⬅︎ Inhale';
            if (els.labs.exhale) els.labs.exhale.textContent = 'Exhale ➡︎';
            if (els.labs.hold) els.labs.hold.textContent = 'Hold ⏸';

            if (els.labs.inhale) { els.labs.inhale.style.left = '8%'; els.labs.inhale.style.top = '30%'; }
            if (els.labs.exhale) { els.labs.exhale.style.right = '8%'; els.labs.exhale.style.top = '30%'; els.labs.exhale.style.textAlign = 'right'; }
            if (els.labs.hold) { els.labs.hold.style.left = '50%'; els.labs.hold.style.bottom = '6%'; els.labs.hold.style.top = ''; els.labs.hold.style.transform = 'translateX(-50%)'; }
        }

        // Show/hide Hold2 input
        if (els.hold2Field) els.hold2Field.classList.toggle('hidden', newMode !== 'box');

        // Reset visuals to clean slate for new mode
        reset();
    }

    // wire up event listeners
    if (els.start) els.start.addEventListener('click', start);
    if (els.pause) els.pause.addEventListener('click', pauseResume);
    if (els.reset) els.reset.addEventListener('click', reset);
    els.modeInputs.forEach(r => r.addEventListener('change', (e) => updateMode(e.target.value)));

    window.addEventListener('keydown', (e) => {
        if (e.key === ' ') { e.preventDefault(); if (state.running) pauseResume(); }
        else if (e.key === 'Enter') { if (!state.running || state.paused) start(); }
        else if (e.key.toLowerCase() === 'r') { reset(); }
    });

    // initialize
    updateMode('triangle');

    // keep strokes correct after resize when idle
    let resizeTimer;
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => { if (!state.running) { reset(); } }, 120);
    });

    return {
        start,
        pause: pauseResume,
        reset,
        updateMode,
        state
    };
}

// Export for Blazor components - no auto-initialization
