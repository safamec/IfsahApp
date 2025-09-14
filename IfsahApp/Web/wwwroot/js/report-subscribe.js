(function () {
  const emailInput = document.getElementById('notifyEmail');
  const btn = document.getElementById('verifyBtn');
  const loader = document.getElementById('loader');
  const toast = document.getElementById('toast');
  const reportNo = window.REPORT_NO || '';

  function showToast(msg, ok) {
    toast.textContent = msg;
    toast.className = 'ty-toast show ' + (ok ? 'ok' : 'err');
    setTimeout(() => toast.className = 'ty-toast', 2800);
  }

  function isValidEmail(v) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test((v || '').trim());
  }

  async function subscribe() {
    const email = (emailInput.value || '').trim();

    if (!isValidEmail(email)) {
      showToast('الرجاء إدخال بريد إلكتروني صالح', false);
      emailInput.focus();
      return;
    }
    if (!reportNo) {
      showToast('رقم البلاغ غير متوفر', false);
      return;
    }

    btn.disabled = true;
    loader.classList.add('show');

    try {
      const res = await fetch('/Disclosure/SubscribeEmail', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': getCsrfToken()
        },
        body: JSON.stringify({ reportNumber: reportNo, email })
      });
      const data = await res.json();
      if (res.ok && data?.ok) {
        showToast(data.message || 'تم التحقق والاشتراك بنجاح', true);
      } else {
        showToast(data?.message || 'تعذر إتمام العملية', false);
      }
    } catch (e) {
      showToast('خطأ في الاتصال بالخادم', false);
    } finally {
      btn.disabled = false;
      loader.classList.remove('show');
    }
  }

  // لو عندك Anti-forgery (أفضل)
  function getCsrfToken() {
    const el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
  }

  btn?.addEventListener('click', (e) => {
    e.preventDefault();
    subscribe();
  });
})();
