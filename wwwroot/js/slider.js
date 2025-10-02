// Simple fade slider (no libs)
(function () {
  const root = document.getElementById('heroSlider');
  if (!root) return;

  const slides = Array.from(root.querySelectorAll('.slide'));
  const dotsWrap = root.querySelector('.slider-dots');
  let i = 0, timer;

  // build dots
  slides.forEach((_, idx) => {
    const b = document.createElement('button');
    b.className = 'dot' + (idx === 0 ? ' on' : '');
    b.setAttribute('aria-label', `Ga naar slide ${idx + 1}`);
    b.addEventListener('click', () => go(idx, true));
    dotsWrap.appendChild(b);
  });
  const dots = Array.from(dotsWrap.querySelectorAll('.dot'));

  function show(n) {
    slides[i].classList.remove('active');
    i = (n + slides.length) % slides.length;
    slides[i].classList.add('active');
    dots.forEach((d, idx) => d.classList.toggle('on', idx === i));
  }

  function go(n, manual = false) {
    show(n);
    if (manual) reset();
  }

  function next() { go(i + 1); }
  function prev() { go(i - 1); }

  root.querySelector('.next').addEventListener('click', () => go(i + 1, true));
  root.querySelector('.prev').addEventListener('click', () => go(i - 1, true));

  function start() { timer = setInterval(next, 4500); }
  function stop() { clearInterval(timer); }
  function reset() { stop(); start(); }

  root.addEventListener('mouseenter', stop);
  root.addEventListener('mouseleave', start);

  start();
})();
