(function () {
    const root = document.getElementById('vn2');
    if (!root) return;

    // Tek bir compare bloğunu tamamen bağla
    function bindCompare(wrap) {
        const r = wrap.querySelector('.slider');
        const h = wrap.querySelector('.handle');

        const set = (pct) => {
            pct = Math.max(0, Math.min(100, +pct || 0));
            wrap.style.setProperty('--pos', pct + '%');
            if (h) h.style.left = pct + '%';
            if (r && +r.value !== pct) r.value = pct;
        };

        // Range ile kontrol
        if (r) {
            set(r.value || 50);
            r.addEventListener('input', (e) => set(e.target.value));
            r.addEventListener('change', (e) => set(e.target.value));
        } else {
            set(50);
        }

        // Alanda sürükleyerek kontrol (mouse + touch)
        let dragging = false;
        const clientX = (e) => (e.touches ? e.touches[0].clientX : e.clientX);
        const calcPct = (e) => {
            const rect = wrap.getBoundingClientRect();
            const x = clientX(e) - rect.left;
            return (x / rect.width) * 100;
        };
        const onDown = (e) => { dragging = true; set(calcPct(e)); e.preventDefault(); };
        const onMove = (e) => { if (dragging) { set(calcPct(e)); e.preventDefault(); } };
        const onUp = () => { dragging = false; };

        wrap.addEventListener('mousedown', onDown);
        window.addEventListener('mousemove', onMove);
        window.addEventListener('mouseup', onUp);

        wrap.addEventListener('touchstart', onDown, { passive: false });
        window.addEventListener('touchmove', onMove, { passive: false });
        window.addEventListener('touchend', onUp);
    }

    // Tüm compare bloklarını bağla
    root.querySelectorAll('.vn2-compare').forEach(bindCompare);

    // Filtreler
    const pills = [...root.querySelectorAll('.pill')];
    const cards = [...root.querySelectorAll('.vn2-card')];
    pills.forEach((p) => p.addEventListener('click', () => {
        pills.forEach((x) => x.classList.remove('active'));
        p.classList.add('active');
        const f = p.dataset.filter;
        cards.forEach((c) => { c.style.display = (f === 'all' || c.dataset.cat === f) ? '' : 'none'; });
    }));

    // Lightbox
    function openModal(b, a) {
        const m = document.createElement('div');
        m.className = 'modal show';
        m.innerHTML =
            '<div class="backdrop"></div>' +
            '<div class="dialog">' +
            '  <button class="close" aria-label="Sluiten">×</button>' +
            '  <div class="vn2-compare big" style="--pos:50%">' +
            '    <img class="before" src="' + b + '" alt="Voor">' +
            '    <img class="after"  src="' + a + '" alt="Na">' +
            '    <input class="slider" type="range" min="0" max="100" value="50">' +
            '    <div class="handle"></div><div class="labels"><span>Voor</span><span>Na</span></div>' +
            '  </div>' +
            '</div>';
        root.appendChild(m);
        bindCompare(m.querySelector('.vn2-compare'));
        const close = () => m.remove();
        m.querySelector('.backdrop').addEventListener('click', close);
        m.querySelector('.close').addEventListener('click', close);
    }
    root.querySelectorAll('.zoom').forEach((btn) =>
        btn.addEventListener('click', () => openModal(btn.dataset.b, btn.dataset.a))
    );
})();
