(() => {
  const box  = document.getElementById('msgBox');
  const body = document.getElementById('examBody');
  const btnReload = document.getElementById('btnReload');

  function csrf() {
    const m = document.querySelector('meta[name="request-verification-token"]');
    return m ? m.content : '';
  }
  function showMsg(text, ok=true){
    if(!box) return;
    box.className = 'alert mt-3 ' + (ok ? 'alert-success' : 'alert-danger');
    box.textContent = text;
    box.classList.remove('d-none');
    setTimeout(()=>box.classList.add('d-none'), 2500);
  }

  async function loadExaminers(){
    try {
      const res = await fetch('/Admin/Examiners', { headers: { 'Accept':'application/json' }});
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      if (!body) return;
      body.innerHTML = data.map(x => `
        <tr>
          <td>${x.name || ''}</td>
          <td>${x.sam  || ''}</td>
          <td>${x.email|| ''}</td>
          <td>
            <button class="btn btn-sm btn-outline-danger exm-remove" data-sam="${x.sam}">Remove</button>
          </td>
        </tr>
      `).join('');
    } catch {
      showMsg('Failed to load examiners', false);
    }
  }

  async function removeExaminer(sam){
    try {
      const res = await fetch('/Admin/RemoveExaminer', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf() },
        body: JSON.stringify({ sam })
      });
      if (res.ok) {
        showMsg('Examiner removed ✔️', true);
        await loadExaminers();
      } else {
        showMsg('Failed: ' + (await res.text()), false);
      }
    } catch {
      showMsg('Request error', false);
    }
  }

  if (btnReload) btnReload.addEventListener('click', loadExaminers);

  if (body) {
    body.addEventListener('click', (e) => {
      const rm = e.target.closest('.exm-remove');
      if (rm) {
        const sam = rm.getAttribute('data-sam');
        if (sam) removeExaminer(sam);
      }
    });
  }

  loadExaminers();
})();
