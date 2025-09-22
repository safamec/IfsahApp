// wwwroot/js/notifications.js
// Works with navbar IDs:
// - #btnNotif  (dropdown toggle button)
// - #notifBadge
// - #notifList  (container inside <ul class="dropdown-menu">)
// - #notifMarkAllForm  (form to mark all read)
// - optional #notifStatus (connection dot)
//
// Requires:
// - bootstrap.bundle.js (for Dropdown)
// - microsoft-signalr client (only for admins if enabled)
// - endpoints: /Notifications/Unread, /Notifications/Feed?take=20,
//              /Notifications/MarkAsRead?id=..., /Notifications/MarkAllRead
//
(function () {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  function init() {
    // ================== Options ==================
    const opts = window.NOTIF_OPTS || {};
    const hubUrl       = opts.hubUrl      || '/hubs/notifications';
    const unreadUrl    = opts.unreadUrl   || '/Notifications/Unread';
    const feedUrl      = opts.feedUrl     || '/Notifications/Feed?take=20';
    const markReadUrl  = opts.markReadUrl || '/Notifications/MarkAsRead';
    const markAllUrl   = opts.markAllUrl  || '/Notifications/MarkAllRead';
    const userId       = opts.userId      || null;
    const enableToast  = opts.toast !== false && false; // <-- toasts OFF by default
    const userIsAdmin  = opts.userIsAdmin === true || opts.userIsAdmin === 'true';

    const csrfHeaderName = opts.csrfHeaderName || 'RequestVerificationToken';
    let   csrfToken      = opts.csrfToken || (document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? null);

    // ================== UI Elements ==================
    const btnNotif       = document.getElementById('btnNotif');
    const badge          = document.getElementById('notifBadge');
    const listBox        = document.getElementById('notifList');
    const markAllForm    = document.getElementById('notifMarkAllForm');
    const statusDot      = document.getElementById('notifStatus');

    // ================== Helpers ==================
    function setStatus(connected){
      if(!statusDot) return;
      statusDot.classList.toggle('bg-success', connected);
      statusDot.classList.toggle('bg-secondary', !connected);
      statusDot.title = connected ? 'Connected' : 'Offline';
    }

    function setBadge(count){
      if(!badge) return;
      const c = Number.isFinite(count) ? count : 0;
      if (c > 0) {
        badge.textContent = String(c > 99 ? '99+' : c);
        badge.classList.remove('d-none');
      } else {
        badge.textContent = '0';
        badge.classList.add('d-none');
      }
    }
    function getBadge(){ return parseInt((badge && badge.textContent) || '0', 10) || 0; }
    function incBadge(){ setBadge(getBadge() + 1); }
    function decBadge(){ setBadge(Math.max(0, getBadge() - 1)); }

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

    // ================== NO-OP toast (disabled) ==================
    function showToast() { /* intentionally empty (toasts disabled) */ }

    // ================== Notification rows ==================
    const seen = new Set();

    function buildRowHtml(n) {
      const created = n.createdAt ? new Date(n.createdAt) : new Date();
      return `
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
    }

    function addRow(n){
      if(!listBox || !n) return;
      if (seen.has(n.id)) return;
      seen.add(n.id);

      if (!listBox.querySelector('.notif-item')) {
        listBox.innerHTML = '';
      }

      const row = document.createElement('div');
      row.className = 'dropdown-item notif-item';
      row.innerHTML = buildRowHtml(n);
      listBox.appendChild(row);

      // mark single as read
      row.querySelector('.btn-mark')?.addEventListener('click', async (e)=>{
        e.preventDefault();
        try{
          const headers = { 'Accept':'application/json' };
          if (csrfHeaderName && csrfToken) headers[csrfHeaderName] = csrfToken;

          const res = await fetch(`${markReadUrl}?id=${encodeURIComponent(n.id)}`, {
            method: 'POST',
            headers,
            credentials: 'same-origin'
          });
          if (res.ok) {
            row.remove();
            decBadge();
            renderEmptyIfNeeded();
          }
        }catch(err){
          console.error(err);
        }
      });
    }

    // ================== API calls ==================
    async function loadUnread(){
      try{
        const res = await fetch(unreadUrl, {
          headers: { 'Accept':'application/json' },
          credentials: 'same-origin'
        });
        if (!res.ok) return;
        const arr = await res.json();
        if (listBox) listBox.innerHTML = '';
        (arr || []).forEach(addRow);
        setBadge((arr || []).length);
        renderEmptyIfNeeded();
      }catch(err){ console.error(err); }
    }

    async function loadFeed(){
      try{
        const res = await fetch(feedUrl, {
          headers: { 'Accept':'application/json' },
          credentials: 'same-origin'
        });
        if (!res.ok) return;
        const arr = await res.json();
        if (listBox && !listBox.querySelector('.notif-item')) listBox.innerHTML = '';
        (arr || []).forEach(addRow);
        renderEmptyIfNeeded();
      }catch(err){
        console.error(err);
      }
    }

    // ================== Mark all read ==================
    if (markAllForm) {
      markAllForm.addEventListener('submit', async (e)=>{
        e.preventDefault();
        try{
          const headers = { 'Accept':'application/json' };
          if (csrfHeaderName && csrfToken) headers[csrfHeaderName] = csrfToken;

          const res = await fetch(markAllForm.action || markAllUrl, {
            method: 'POST',
            headers,
            credentials: 'same-origin'
          });
          if (res.ok) {
            if (listBox) listBox.innerHTML = '';
            setBadge(0);
            renderEmptyIfNeeded();
          }
        }catch(err){
          console.error(err);
        }
      });
    }

    // ================== SignalR (admins only, safe if missing) ==================
    let connection = null;
    if (userIsAdmin && window.signalR && hubUrl) {
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
        if (enableToast) showToast(n.eventType || n.title, n.message, n.url);
      });

      connection.start()
        .then(()=>setStatus(true))
        .catch(err => { console.error('SignalR start failed:', err); setStatus(false); });
    }

    // ================== Dropdown wiring ==================
    if (btnNotif && typeof bootstrap !== 'undefined' && bootstrap.Dropdown) {
      // manual toggle fallback
      btnNotif.addEventListener('click', function (e) {
        const dd = bootstrap.Dropdown.getOrCreateInstance(e.currentTarget);
        dd.toggle();
      });

      // refresh list whenever opening (safe when signalR is absent)
      btnNotif.addEventListener('show.bs.dropdown', async () => {
        const isHubConnected =
          !!connection &&
          (window.signalR && signalR.HubConnectionState
            ? connection.state === signalR.HubConnectionState.Connected
            : String(connection.state || '').toLowerCase() === 'connected');

        if (isHubConnected) {
          await loadUnread(); // show unread when realtime connected
        } else {
          await loadFeed();   // fallback to feed when not connected
        }
      });
    }

    // ================== Initial load + periodic refresh ==================
    loadUnread();
    setInterval(loadUnread, 60_000);
  }
})();

