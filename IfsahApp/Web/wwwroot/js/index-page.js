(() => {
  async function loadUsers() {
    const res = await fetch('/Admin/Examiners'); // نستعمل نفس API بس نعدل الكنترولر لو حبيتِ نجيب كل الأدوار
    if (!res.ok) return;

    const data = await res.json();

    // نفرغ الجداول
    document.getElementById('adminsBody').innerHTML = '';
    document.getElementById('examinersBody').innerHTML = '';
    document.getElementById('usersBody').innerHTML = '';

    // نفرز حسب الدور
    data.forEach(u => {
      const row = `
        <tr>
          <td>${u.name || ''}</td>
          <td>${u.sam || ''}</td>
          <td>${u.email || ''}</td>
          <td>${u.dept || ''}</td>
          <td>${u.active ? '✅' : '❌'}</td>
          ${u.role === 'Examiner' 
              ? `<td><button class="btn btn-sm btn-danger remove-examiner" data-sam="${u.sam}">Remove</button></td>` 
              : '<td></td>'}
        </tr>
      `;

      if (u.role === 'Admin') document.getElementById('adminsBody').innerHTML += row;
      else if (u.role === 'Examiner') document.getElementById('examinersBody').innerHTML += row;
      else document.getElementById('usersBody').innerHTML += row;
    });
  }

  // حذف ممتحن
  document.addEventListener('click', async e => {
    const btn = e.target.closest('.remove-examiner');
    if (!btn) return;
    const sam = btn.getAttribute('data-sam');
    if (!sam) return;

    const res = await fetch('/Admin/RemoveExaminer', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getCsrf() },
      body: JSON.stringify({ sam })
    });

    if (res.ok) {
      alert('Examiner removed ✔️');
      loadUsers();
    } else {
      alert('Failed to remove examiner');
    }
  });

  function getCsrf() {
    const m = document.querySelector('meta[name="request-verification-token"]');
    return m ? m.content : '';
  }

  loadUsers();
})();
