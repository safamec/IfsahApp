(() => {
  const input = document.getElementById('adSearch');
  const list  = document.getElementById('adResults');
  const box   = document.getElementById('msgBox');

  function showMsg(text, ok=true){
    if(!box) return;
    box.className = 'alert mt-3 ' + (ok ? 'alert-success' : 'alert-danger');
    box.textContent = text;
    box.classList.remove('d-none');
    setTimeout(()=>box.classList.add('d-none'), 3000);
  }

  let t;
  input.addEventListener('input', () => {
    clearTimeout(t);
    const q = input.value.trim();
    if (!q) { list.innerHTML = ''; return; }

    t = setTimeout(async () => {
      try {
        const res = await fetch(`/Admin/SearchAdUsers?q=${encodeURIComponent(q)}&take=8`,
          { headers: { 'Accept':'application/json' }});
        if (!res.ok) return;
        const data = await res.json();
        list.innerHTML = data.map(x =>
          `<li class="list-group-item list-group-item-action"
               data-sam="${x.sam}" data-name="${x.name}" data-email="${x.email}" data-dept="${x.dept}">
             <div class="fw-bold">${x.name}</div>
             <div class="small text-muted">${x.sam} · ${x.email} · ${x.dept}</div>
           </li>`).join('');
      } catch { showMsg('Search failed', false); }
    }, 250);
  });

  list.addEventListener('click', (e) => {
    const li = e.target.closest('li[data-sam]');
    if (!li) return;

    const sam  = li.dataset.sam  || '';
    const name = li.dataset.name || '';
    const email= li.dataset.email|| '';
    const dept = li.dataset.dept || '';

    document.getElementById('uiFullName').textContent = name;
    document.getElementById('uiAdUser').textContent   = sam;
    document.getElementById('uiEmail').textContent    = email;
    document.getElementById('uiDept').textContent     = dept;

    document.getElementById('hiSam').value   = sam;
    document.getElementById('hiName').value  = name;
    document.getElementById('hiEmail').value = email;
    document.getElementById('hiDept').value  = dept;

    list.innerHTML = '';
    input.value = `${name} (${sam})`;
  });

  document.addEventListener('click', (e) => {
    if (!list.contains(e.target) && e.target !== input) list.innerHTML = '';
  });
})();
