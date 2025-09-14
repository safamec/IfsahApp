(() => {
  const box   = document.getElementById('msgBox');
  const btn   = document.getElementById('btnSave') || document.getElementById('btnAddExaminer'); // fallback if you kept old id
  const input = document.getElementById('adSearch');
  const list  = document.getElementById('adResults');

  const uiFull = document.getElementById('uiFullName');
  const uiSam  = document.getElementById('uiAdUser');
  const uiEmail= document.getElementById('uiEmail');
  const uiDept = document.getElementById('uiDept');

  const hiSam   = document.getElementById('hiSam');
  const hiName  = document.getElementById('hiName');
  const hiEmail = document.getElementById('hiEmail');
  const hiDept  = document.getElementById('hiDept');
  const hiRole  = document.getElementById('hiRole'); // should be "Examiner"

  function csrf() {
    const m = document.querySelector('meta[name="request-verification-token"]');
    return m ? m.content : '';
  }
  function showMsg(text, ok=true){
    if(!box) return;
    box.className = 'alert mt-3 ' + (ok ? 'alert-success' : 'alert-danger');
    box.textContent = text;
    box.classList.remove('d-none');
    setTimeout(()=>box.classList.add('d-none'), 2600);
  }

  // --- prefill from query string ---
  (function prefillFromQuery(){
    const p = new URLSearchParams(window.location.search);
    const sam  = p.get('sam')   || '';
    const name = p.get('name')  || '';
    const email= p.get('email') || '';
    const dept = p.get('dept')  || '';
    const role = (p.get('role') || 'Examiner');

    if (sam) {
      if (uiFull)  uiFull.textContent  = name;
      if (uiSam)   uiSam.textContent   = sam;
      if (uiEmail) uiEmail.textContent = email;
      if (uiDept)  uiDept.textContent  = dept;

      if (hiSam)   hiSam.value   = sam;
      if (hiName)  hiName.value  = name;
      if (hiEmail) hiEmail.value = email;
      if (hiDept)  hiDept.value  = dept;
      if (hiRole)  hiRole.value  = role || 'Examiner';
    }
  })();

  // ---- optional: search on Index page (keep if you want live search here too) ----
  if (input && list) {
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
          list.innerHTML = data.map(x => `
            <li class="list-group-item list-group-item-action"
                data-sam="${x.sam}" data-name="${x.name}" data-email="${x.email}" data-dept="${x.dept}">
              <div class="fw-bold">${x.name}</div>
              <div class="small text-muted">${x.sam} · ${x.email} · ${x.dept}</div>
            </li>
          `).join('');
        } catch { showMsg('Search failed', false); }
      }, 220);
    });

    list.addEventListener('click', (e) => {
      const li = e.target.closest('li[data-sam]');
      if (!li) return;

      const sam  = li.dataset.sam  || '';
      const name = li.dataset.name || '';
      const email= li.dataset.email|| '';
      const dept = li.dataset.dept || '';

      if (uiFull)  uiFull.textContent  = name;
      if (uiSam)   uiSam.textContent   = sam;
      if (uiEmail) uiEmail.textContent = email;
      if (uiDept)  uiDept.textContent  = dept;

      if (hiSam)   hiSam.value   = sam;
      if (hiName)  hiName.value  = name;
      if (hiEmail) hiEmail.value = email;
      if (hiDept)  hiDept.value  = dept;
      if (hiRole)  hiRole.value  = 'Examiner'; // default

      list.innerHTML = '';
      input.value = `${name} (${sam})`;
    });

    document.addEventListener('click', (e) => {
      if (!list.contains(e.target) && e.target !== input) list.innerHTML = '';
    });
  }

  // ---- Save -> AssignRole -> redirect back to ExaminersPage ----
  if (btn) {
    btn.addEventListener('click', async (e) => {
      e.preventDefault();

      const sam = hiSam?.value || '';
      if (!sam) { showMsg('Select a user first', false); return; }

      const payload = {
        sam:         sam,
        displayName: hiName?.value || '',
        email:       hiEmail?.value || '',
        department:  hiDept?.value || '',
        role:        hiRole?.value || 'Examiner'
      };

      try {
        btn.disabled = true;

        const res = await fetch('/Admin/AssignRole', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf() },
          body: JSON.stringify(payload)
        });

        if (res.ok) {
          // email is sent server-side in AssignRole
          window.location.href = '/Admin/ExaminersPage';
        } else {
          showMsg('Failed: ' + (await res.text()), false);
        }
      } catch {
        showMsg('Request error', false);
      } finally {
        btn.disabled = false;
      }
    });
  }
})();
