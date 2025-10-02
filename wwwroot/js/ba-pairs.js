(function(){
  const root=document.getElementById('baPairs'); if(!root) return;

  function openModal(before, after){
    const el=document.createElement('div');
    el.className='ba-modal';
    el.innerHTML =
      '<div class="ba-backdrop"></div>'+
      '<div class="ba-dialog">'+
        '<button class="ba-close" aria-label="Sluiten">×</button>'+
        '<div class="ba-compare">'+
          `<img src="${before}" alt="Voor">`+
          `<img src="${after}"  alt="Na">`+
        '</div>'+
      '</div>';
    document.body.appendChild(el);
    const close=()=>el.remove();
    el.querySelector('.ba-backdrop').addEventListener('click', close);
    el.querySelector('.ba-close').addEventListener('click', close);
  }

  root.querySelectorAll('.pair').forEach(pair=>{
    const imgs=pair.querySelectorAll('img');
    const before=imgs[0]?.src, after=imgs[1]?.src;
    // click anywhere on the tile
    pair.addEventListener('click', ()=> openModal(before, after));
    // keyboard on the pill
    pair.querySelector('.pill')?.addEventListener('keydown', e=>{
      if(e.key==='Enter'||e.key===' '){ e.preventDefault(); openModal(before, after); }
    });
  });
})();
