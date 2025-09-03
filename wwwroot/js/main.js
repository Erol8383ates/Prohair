/* ================================
   Main site script (clean version)
   ================================ */

/* Small helpers */
const $ = (sel, ctx = document) => ctx.querySelector(sel);
const $$ = (sel, ctx = document) => [...ctx.querySelectorAll(sel)];

/* -------------------------------
   Mobile drawer (burger menu)
-------------------------------- */
(function () {
    const burger = $('.burger');
    const drawer = $('.mobile-drawer');
    if (!burger || !drawer) return;

    // SHOW/HIDE + state classes for CSS
    const open = () => {
        drawer.style.display = 'block';
        drawer.classList.add('show');                    // added
        document.body.classList.add('drawer-open', 'no-scroll'); // added
    };
    const close = () => {
        drawer.classList.remove('show');                 // added
        drawer.style.display = 'none';
        document.body.classList.remove('drawer-open', 'no-scroll'); // added
    };

    burger.addEventListener('click', open);
    drawer.addEventListener('click', (e) => { if (e.target === drawer) close(); });
    $$('.mobile-links a', drawer).forEach(a => a.addEventListener('click', close));
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') close(); });
})();

/* -------------------------------
   Swiper (testimonials) — guarded
-------------------------------- */
(function () {
    if (!window.Swiper || !$('.testi-swiper')) return;
    new Swiper('.testi-swiper', {
        loop: true,
        autoplay: { delay: 3000 },
        pagination: { el: '.swiper-pagination', clickable: true }
    });
})();

/* -------------------------------
   AOS (scroll animations) — guarded
-------------------------------- */
(function () {
    if (window.AOS) AOS.init({ duration: 700, once: true, offset: 80 });
})();

/* -------------------------------
   Cookie consent (simple)
-------------------------------- */
(function () {
    const box = $('.cookie'); if (!box) return;
    const KEY = 'ph-cookie-accepted';
    if (!localStorage.getItem(KEY)) box.style.display = 'block';
    box.querySelector('.btn-accept')?.addEventListener('click', () => {
        localStorage.setItem(KEY, '1');
        box.style.display = 'none';
    });
})();

/* -------------------------------
   Sticky CTA (#stickyCta)
-------------------------------- */
(function () {
    const el = $('#stickyCta');
    if (!el) return;
    const SHOW_AT = 500;
    const onScroll = () => el.classList.toggle('show', window.scrollY > SHOW_AT);
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
})();

/* -------------------------------
   Counter-up: [data-counter]
-------------------------------- */
(function () {
    const items = $$('[data-counter]');
    if (!items.length) return;

    const animate = (el) => {
        const target = +el.dataset.counter || 0;
        let cur = 0;
        const step = Math.max(1, Math.round(target / 60));
        const tick = () => {
            cur += step;
            if (cur >= target) el.textContent = target;
            else { el.textContent = cur; requestAnimationFrame(tick); }
        };
        tick();
    };

    const io = new IntersectionObserver((entries) => {
        entries.forEach(e => {
            if (e.isIntersecting) { animate(e.target); io.unobserve(e.target); }
        });
    }, { threshold: 0.6 });

    items.forEach(el => io.observe(el));
})();

/* -------------------------------
   Tabs (used on Men page)
-------------------------------- */
(function () {
    const wrap = $('.tabs');
    if (!wrap) return;

    const btns = $$('.tab-btn', wrap);
    const panes = $$('.tab-content', wrap);

    const show = (id) => {
        btns.forEach(b => b.classList.toggle('active', b.dataset.tab === id));
        panes.forEach(p => p.classList.toggle('show', p.id === id));
    };

    btns.forEach(b => b.addEventListener('click', () => show(b.dataset.tab)));
    if (btns[0]) show(btns[0].dataset.tab);
})();

/* -------------------------------
   Before/After sliders
   - Works for .before-after (legacy)
   - Works for .vn2-compare (new) using CSS --pos
-------------------------------- */
(function () {
    const initCompare = (w) => {
        const r = $('input[type="range"]', w);
        const handle = $('.handle', w);

        const setPos = (v) => {
            const pct = Math.min(100, Math.max(0, +v || 50));
            w.style.setProperty('--pos', pct + '%');
            if (handle) handle.style.left = pct + '%';
            const after = $('.after', w);
            if (after && !w.classList.contains('vn2-compare')) after.style.width = pct + '%';
        };

        if (r) {
            setPos(r.value || 50);
            r.addEventListener('input', e => setPos(e.target.value));
        } else {
            setPos(50);
        }
    };

    $$('.before-after, .vn2-compare').forEach(initCompare);
})();

/* -------------------------------
   Lightbox for VN2 “Volledig scherm”
-------------------------------- */
(function () {
    const root = $('#vn2'); if (!root) return;

    function openLightbox(beforeUrl, afterUrl) {
        const m = document.createElement('div');
        m.className = 'modal show';
        m.innerHTML = `
      <div class="backdrop"></div>
      <div class="dialog">
        <button class="close" aria-label="Sluiten">×</button>
        <div class="vn2-compare big" style="--pos:50%">
          <img class="before" src="${beforeUrl}" alt="Voor">
          <img class="after"  src="${afterUrl}"  alt="Na">
          <input class="slider" type="range" min="0" max="100" value="50" aria-label="Vergelijk">
          <div class="handle"></div>
          <div class="labels"><span>Voor</span><span>Na</span></div>
        </div>
      </div>`;

        root.appendChild(m);

        const cmp = $('.vn2-compare', m);
        const r = $('.slider', cmp);
        const h = $('.handle', cmp);
        const set = (v) => { cmp.style.setProperty('--pos', `${v}%`); if (h) h.style.left = `${v}%`; };
        set(50);
        r.addEventListener('input', e => set(e.target.value));

        const close = () => m.remove();
        $('.backdrop', m).addEventListener('click', close);
        $('.close', m).addEventListener('click', close);
        document.addEventListener('keydown', function esc(e) { if (e.key === 'Escape') { close(); document.removeEventListener('keydown', esc); } });
    }

    $$('.zoom', root).forEach(btn => {
        btn.addEventListener('click', () => openLightbox(btn.dataset.b, btn.dataset.a));
    });

    // Simple filters on VN2
    const pills = $$('.pill', root);
    const cards = $$('.vn2-card', root);
    pills.forEach(p => p.addEventListener('click', () => {
        pills.forEach(x => x.classList.remove('active'));
        p.classList.add('active');
        const f = p.dataset.filter || 'all';
        cards.forEach(c => { c.style.display = (f === 'all' || c.dataset.cat === f) ? '' : 'none'; });
    }));
})();

/* -------------------------------
   Reservation drawer body class
-------------------------------- */
(function () {
    const drawer = document.getElementById('drawer');
    if (!drawer) return;
    const sync = () => document.body.classList.toggle('drawer-open', drawer.classList.contains('open'));
    const mo = new MutationObserver(sync);
    mo.observe(drawer, { attributes: true, attributeFilter: ['class'] });
    sync();
})();
