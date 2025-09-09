// wwwroot/js/notifications.js — Offcanvas “pop panel” + toasts + offline support
(function () {
  const opts = window.NOTIF_OPTS || {};
  const hubUrl      = opts.hubUrl || '/hubs/notifications';
  const unreadUrl   = opts.unreadUrl || '/Notifications/Unread';
  const markReadUrl = opts.markReadUrl || '/Notifications/MarkRead';
  const feedUrl     = opts.feedUrl || '/Notifications/Feed?take=20';
  const userId      = opts.userId || null;
  const csrfHeaderName = opts.csrfHeaderName || null;
  const csrfToken      = opts.csrfToken || null;

  const badge     = document.getElementById('notifBadge');
  const tbody     = document.getElementById('notifTbody');
  const toastBox  = document.getElementById('toastBox');
  const statusDot = document.getElementById('notifStatus');
  const offcanvas = document.getElementById('notifPanel');
  const markAllBtn= document.getElementById('notifMarkAll');
  const seen = new Set();

  // ---------- helpers ----------
  function setBadge(count){ if(!badge) return; (count>0)?(badge.textContent=count, badge.classList.remove('d-none')):badge.classList.add('d-none'); }
  function incBadge(){ setBadge(parseInt(badge?.textContent||'0',10)+1); }
  function decBadge(){ setBadge(Math.max(0, parseInt(badge?.textContent||'0',10)-1)); }
  function setStatus(connected){ if(!statusDot) return; statusDot.classList.toggle('bg-success',connected); statusDot.classList.toggle('bg-secondary',!connected); statusDot.title = connected?'Connected':'Offline'; }
  function escapeHtml(s){ return String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;').replaceAll("'","&#39;"); }

  function showToast(n){
    if(!toastBox) return;
    const el=document.createElement('div');
    el.className='toast align-items-center show'; el.role='alert'; el.ariaLive='polite';
    el.innerHTML=`<div class="d-flex">
      <div class="toast-body">
        <div class="fw-semibold">${escapeHtml(n.eventType||n.title||'Notification')}</div>
        <div>${escapeHtml(n.message||'')}</div>
        ${n.url?`<a href="${n.url}" class="ms-1">Open</a>`:''}
      </div>
      <button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
    </div>`;
    toastBox.appendChild(el); setTimeout(()=>el.remove(),6000);
  }

  async function markAsRead(id){
    try{
      const headers={}; if(csrfHeaderName&&csrfToken) headers[csrfHeaderName]=csrfToken;
      const res=await fetch(`${markReadUrl}?id=${encodeURIComponent(id)}`,{method:'POST',headers});
      if(res.ok) decBadge();
    }catch{}
  }

  function openModal(n){
    const modalEl=document.getElementById('notifModal');
    if(!modalEl) return;
    modalEl.querySelector('.modal-title').textContent = n.eventType||n.title||'Notification';
    modalEl.querySelector('.modal-body .msg').textContent = n.message||'';
    modalEl.querySelector('.modal-body .time').textContent = n.createdAt? new Date(n.createdAt).toLocaleString() : '';
    const link=modalEl.querySelector('.modal-footer .open-link');
    if(n.url){ link.classList.remove('d-none'); link.href=n.url; } else { link.classList.add('d-none'); link.removeAttribute('href'); }
    new bootstrap.Modal(modalEl).show();
  }

  function addRow(n){
    if(!tbody||!n) return;
    if(seen.has(n.id)) return; seen.add(n.id);
    tbody.querySelector('.empty-row')?.remove();

    const created = n.createdAt ? new Date(n.createdAt) : new Date();
    const tr=document.createElement('tr'); tr.dataset.id=n.id;
    tr.innerHTML=`
      <td>${escapeHtml(n.eventType||n.title||'Notification')}</td>
      <td>${escapeHtml(n.message||'')}</td>
      <td class="text-muted">${created.toLocaleString()}</td>
    `;
    tr.addEventListener('click',(e)=>{
      const connected = connection.state==='Connected';
      if(!n.url || !connected){ e.preventDefault(); openModal(n); }
      else { window.location.href = n.url; }
      markAsRead(n.id);
    });
    tbody.appendChild(tr);
  }

  async function loadUnread(){
    try{
      const res=await fetch(unreadUrl,{headers:{'Accept':'application/json'}});
      const arr=await res.json();
      arr.forEach(addRow);
      setBadge(arr.length);
    }catch(e){ console.error(e); }
  }

  async function loadFeed(){
    try{
      const res=await fetch(feedUrl,{headers:{'Accept':'application/json'}});
      const arr=await res.json();
      arr.forEach(addRow);
    }catch{}
  }

  // When the offcanvas opens and we're offline, fill from Feed
  if(offcanvas){
    offcanvas.addEventListener('shown.bs.offcanvas', async ()=>{
      if(connection.state!=='Connected'){ await loadFeed(); }
    });
  }

  // Mark all
  if(markAllBtn){
    markAllBtn.addEventListener('click', async (e)=>{
      e.preventDefault();
      try{
        const headers={}; if(csrfHeaderName&&csrfToken) headers[csrfHeaderName]=csrfToken;
        const res=await fetch('/Notifications/MarkAllAsRead',{method:'POST',headers});
        if(res.ok) location.reload();
      }catch{}
    });
  }

  // ---------- SignalR ----------
  const qs = userId ? `?userId=${encodeURIComponent(userId)}` : '';
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${hubUrl}${qs}`)
    .withAutomaticReconnect()
    .build();

  connection.onreconnecting(()=>setStatus(false));
  connection.onreconnected(()=>setStatus(true));
  connection.onclose(()=>setStatus(false));

  connection.on('Notify',(n)=>{ addRow(n); showToast(n); incBadge(); });

  connection.start()
    .then(()=>setStatus(true))
    .catch(err=>{ console.error('SignalR start failed:',err); setStatus(false); });

  // Initial
  loadUnread();
})();
