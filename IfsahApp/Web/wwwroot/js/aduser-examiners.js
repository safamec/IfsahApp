(() => {
  const box  = document.getElementById('msgBox');
  const body = document.getElementById('examBody');
  const btnReload = document.getElementById('btnReload');
  const btnAddEx  = document.getElementById('btnAddExaminer');

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
    const res = await fetch('/Admin/Examiners', { headers: { 'Accept':'application/json' }});
    if (!res.ok) { showMsg('Failed to load examiners', false); return; }
    const data = await res.json();
    body.innerHTML = data.map(x => `
      <tr>
        <td>${x.name || ''}</td>
        <td>${x.sam  || ''}</td>
        <td>${x.email|| ''}</td>
        <td>
          <div class="form-check form-switch">
            <input class="form-check-input act-toggle" type="checkbox" data-sam="${x.sam}" ${x.active ? 'checked' : ''}>
          </div>
        </td>
        <td>
          <button class="btn btn-sm btn-outline-danger exm-remove" data-sam="${x.sam}">Remove</button>
        </td>
      </tr>
    `).join('');
  }

  // إضافة ممتحن من المستخدم المحدد فوق
  async function addExaminer(){
    const sam  = document.getElementById('hiSam').value;
    const name = document.getElementById('hiName').value;
    const email= document.getElementById('hiEmail').value;
    const dept = document.getElementById('hiDept').value;

    if (!sam) { showMsg('Select a user first', false); return; }

    const payload = { sam: sam, displayName: name, email: email, department: dept, role: 'Examiner' };

    const res = await fetch('/Admin/AddExaminer', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf() },
      body: JSON.stringify(payload)
    });

    if (res.ok) {
      showMsg('Examiner added ✔️', true);
      await loadExaminers();
    } else {
      showMsg('Failed: ' + (await res.text()), false);
    }
  }

  // إزالة ممتحن
  async function removeExaminer(sam){
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
  }

  // تفعيل/تعطيل
  async function setActive(sam, active){
    const res = await fetch('/Admin/SetActive', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf() },
      body: JSON.stringify({ sam, active })
    });

    if (res.ok) {
      showMsg(`Account ${active ? 'activated' : 'deactivated'} ✔️`, true);
    } else {
      showMsg('Failed: ' + (await res.text()), false);
    }
  }

  // أحداث
  if (btnReload) btnReload.addEventListener('click', loadExaminers);
  if (btnAddEx)  btnAddEx.addEventListener('click', (e)=>{ e.preventDefault(); addExaminer(); });

  if (body) {
    body.addEventListener('click', (e) => {
      const rm = e.target.closest('.exm-remove');
      if (rm) {
        const sam = rm.getAttribute('data-sam');
        if (sam) removeExaminer(sam);
      }
    });

    body.addEventListener('change', (e) => {
      const sw = e.target.closest('.act-toggle');
      if (sw) {
        const sam = sw.getAttribute('data-sam');
        const active = sw.checked;
        setActive(sam, active);
      }
    });
  }

  // حمّل الجدول أول ما تفتح الصفحة
  loadExaminers();

  // كشف دالة reload عالمياً عشان سكربت ثاني يقدر يستدعيها
  window.Exm = { reload: loadExaminers };
})();
