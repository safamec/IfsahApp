// wwwroot/js/notifications.js
(function () {
  const opts = window.NOTIF_OPTS || {};

  const hubUrl       = opts.hubUrl      || '/hubs/notifications';
  const unreadUrl    = opts.unreadUrl   || '/Notifications/Unread';
  const feedUrl      = opts.feedUrl     || '/Notifications/Feed?take=20';
  const markReadUrl  = opts.markReadUrl || '/Notifications/MarkAsRead';
  const userId       = opts.userId      || null;

  const csrfHeaderName = opts.csrfHeaderName || 'RequestVerificationToken';
  let   csrfToken      = opts.csrfToken      || null;
  if (!csrfToken) {
    const t = document.querySelector('input[name="__RequestVerificationToken"]');
    if (t) csrfToken = t.value;
  }

  const badge     = document.getElementById('notifBadge');
  const listBox   = document.getElementById('notifList');  // dropdown container
  const markAllBtn= document.getElementById('notifMarkAll');
  const offcanvas = document.getElementById('notifPanel'); // إن كان عندك offcanvas (اختياري)
  const statusDot = document.getElementById('notifStatus'); // اختياري

  const seen = new Set();

  function setBadge(count){
    if(!badge) return;
    if (count > 0) {
      badge.textContent = count;
      badge.classList.remove('d-none');
    } else {
      badge.classList.add('d-none');
    }
  }
  function incBadge(){
    const c = parseInt(badge?.textContent || '0', 10) + 1;
    setBadge(c);
  }
  function decBadge(){
    const c = Math.max(0, parseInt(badge?.textContent || '0', 10) - 1);
    setBadge(c);
  }
  function setStatus(connected){
    if(!statusDot) return;
    statusDot.classList.toggle('bg-success', connected);
    statusDot.classList.toggle('bg-secondary', !connected);
    statusDot.title = connected ? 'Connected' : 'Offline';
  }
  function escapeHtml(s){
    return String(s)
      .replaceAll('&','&amp;')
      .replaceAll('<','&lt;')
      .replaceAll('>','&gt;')
      .replaceAll('"','&quot;')
      .replaceAll("'","&#39;");
  }

  function renderEmptyIfNeeded(){
    if(!listBox) return;
    if (!listBox.querySelector('.notif-item')) {
      listBox.innerHTML = '<div class="text-center text-muted py-3">لا توجد إشعارات</div>';
    }
  }

  function addRow(n){
    if(!listBox || !n) return;
    if(seen.has(n.id)) return; seen.add(n.id);

    // أزل رسالة "لا توجد إشعارات"
    if (listBox.children.length && !listBox.querySelector('.notif-item')) {
      listBox.innerHTML = '';
    }

    const created = n.createdAt ? new Date(n.createdAt) : new Date();
    const li = document.createElement('div');
    li.className = 'dropdown-item notif-item';
    li.innerHTML = `
      <div class="d-flex flex-column gap-1">
        <div class="d-flex justify-content-between">
          <span class="fw-semibold">${escapeHtml(n.eventType || n.title || 'Notification')}</span>
          <small class="text-muted">${created.toLocaleString()}</small>
        </div>
        <div class="text-wrap">${escapeHtml(n.message || '')}</div>
        <div class="mt-1 d-flex gap-2">
          ${n.url ? `<a href="${n.url}" class="btn btn-sm btn-primary">فتح</a>` : ''}
          <button class="btn btn-sm btn-outline-secondary btn-mark" data-id="${n.id}">تمييز كمقروء</button>
        </div>
      </div>
    `;
    listBox.appendChild(li);

    // زر تمييز كمقروء (فردي)
    li.querySelector('.btn-mark')?.addEventListener('click', async (e)=>{
      e.preventDefault();
      try{
        const headers = {};
        if (csrfHeaderName && csrfToken) headers[csrfHeaderName] = csrfToken;
        const res = await fetch(`${markReadUrl}?id=${encodeURIComponent(n.id)}`, {
          method: 'POST',
          headers,
          credentials: 'same-origin'
        });
        if (res.ok) {
          li.remove();
          decBadge();
          renderEmptyIfNeeded();
        }
      }catch{}
    });
  }

  async function loadUnread(){
    try{
      const res = await fetch(unreadUrl, {
        headers: { 'Accept':'application/json' },
        credentials: 'same-origin'
      });
      if (!res.ok) return;
      const arr = await res.json();
      listBox && (listBox.innerHTML = ''); // نظّف القائمة
      arr.forEach(addRow);
      setBadge(arr.length);
      renderEmptyIfNeeded();
    }catch(e){ console.error(e); }
  }

  async function loadFeed(){
    try{
      const res = await fetch(feedUrl, {
        headers: { 'Accept':'application/json' },
        credentials: 'same-origin'
      });
      if (!res.ok) return;
      const arr = await res.json();
      arr.forEach(addRow);
      renderEmptyIfNeeded();
    }catch{}
  }

  // Mark all (داخل Dropdown) — نجعل الطلب AJAX ليبقى داخل القائمة
  if (markAllBtn) {
    markAllBtn.addEventListener('click', async (e)=>{
      e.preventDefault();
      try{
        const headers = {};
        if (csrfHeaderName && csrfToken) headers[csrfHeaderName] = csrfToken;
        const res = await fetch('/Notifications/MarkAllRead', {
          method: 'POST',
          headers,
          credentials: 'same-origin'
        });
        if (res.ok) {
          // نظّف الكل
          if (listBox) listBox.innerHTML = '';
          setBadge(0);
          renderEmptyIfNeeded();
        }
      }catch{}
    });
  }

  // ---------- SignalR (اختياري) ----------
  let connection;
  if (window.signalR && hubUrl) {
    const qs = userId ? `?userId=${encodeURIComponent(userId)}` : '';
    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${hubUrl}${qs}`)
      .withAutomaticReconnect()
      .build();

    connection.onreconnecting(()=>setStatus(false));
    connection.onreconnected(()=>setStatus(true));
    connection.onclose(()=>setStatus(false));

    connection.on('Notify', (n) => {
      addRow(n);
      incBadge();
    });

    connection.start()
      .then(()=>setStatus(true))
      .catch(err => { console.error('SignalR start failed:', err); setStatus(false); });
  }

  // عند فتح القائمة: حدّث الـ unread (ولو أوفلاين ممكن تستدعي feed)
  const trigger = document.getElementById('btnNotif');
  if (trigger) {
    trigger.addEventListener('show.bs.dropdown', async () => {
      if (connection && connection.state !== 'Connected') {
        await loadFeed();
      } else {
        await loadUnread();
      }
    });
  }

  // تحميل أولي للعدّاد + العناصر
  loadUnread();
  // تحديث دوري خفيف
  setInterval(loadUnread, 60000);
})();
