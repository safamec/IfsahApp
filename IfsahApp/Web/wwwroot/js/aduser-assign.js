(() => {
  const btn  = document.getElementById('btnGrant');
  const role = document.getElementById('roleSelect');
  const box  = document.getElementById('msgBox');

  function csrf() {
    const m = document.querySelector('meta[name="request-verification-token"]');
    return m ? m.content : '';
  }

  function showMsg(text, ok=true){
    if(!box) return;
    box.className = 'alert mt-3 ' + (ok ? 'alert-success' : 'alert-danger');
    box.textContent = text;
    box.classList.remove('d-none');
    setTimeout(()=>box.classList.add('d-none'), 3000);
  }

  btn.addEventListener('click', async (e) => {
    e.preventDefault();

    const payload = {
      sam:         document.getElementById('hiSam').value,
      displayName: document.getElementById('hiName').value,
      email:       document.getElementById('hiEmail').value,
      department:  document.getElementById('hiDept').value,
      role:        role.value // Admin / Examiner / User
    };

    if (!payload.sam) { showMsg('Select a user first', false); return; }

    try {
      const res = await fetch('/Admin/AssignRole', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': csrf()
        },
        body: JSON.stringify(payload)
      });

      if (res.ok) {
        const data = await res.json();
        showMsg('Role saved and email sent ✔️ (' + data.role + ')', true);
      } else {
        const msg = await res.text();
        showMsg('Failed: ' + msg, false);
      }
    } catch (err) {
      showMsg('Request error', false);
    }
  });
})();
