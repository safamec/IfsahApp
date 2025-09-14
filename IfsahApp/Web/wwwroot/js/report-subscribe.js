(function () {
  const emailInput = document.getElementById('notifyEmail');
  const btn = document.getElementById('verifyBtn');
  const loader = document.getElementById('loader');
  const toast = document.getElementById('toast');

  const showToast = (msg, ok = true) => {
    toast.textContent = msg;
    toast.classList.remove('ok','err','show');
    toast.classList.add(ok ? 'ok' : 'err', 'show');
    setTimeout(() => toast.classList.remove('show'), 4000);
  };

  const csrf = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
  const url  = window.SUBSCRIBE_URL;

  btn?.addEventListener('click', async () => {
    const email = (emailInput?.value || '').trim();
    const reportNo = (window.REPORT_NO || '').trim();

    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return showToast('رجاءً أدخلي بريدًا صحيحًا', false);
    if (!reportNo) return showToast('رقم البلاغ غير متوفر', false);
    if (!url) return showToast('المسار غير مُعرّف', false);

    btn.disabled = true; loader.classList.add('show');

    try {
      const resp = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': csrf
        },
        body: JSON.stringify({ email, reportNumber: reportNo })
      });

      // لو الخادم رجّع 404/500، اعتبرها فشل
      if (!resp.ok) {
        const txt = await resp.text().catch(() => '');
        return showToast('فشل الطلب (' + resp.status + ') ' + txt, false);
      }

      const data = await resp.json().catch(() => ({}));
      showToast(data.message || (data.ok ? 'تم' : 'فشل'), !!data.ok);
    } catch (e) {
      showToast('خطأ بالشبكة أو الخادم', false);
    } finally {
      btn.disabled = false; loader.classList.remove('show');
    }
  });
})();
