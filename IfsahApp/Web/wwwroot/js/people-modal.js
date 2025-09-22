// wwwroot/js/people-modal.js
(function () {
  // وظيفة مساعدة للهروب من الأحرف الخاصة داخل النصوص/القيم
  function esc(s) {
    return (s || "").replace(/[&<>"']/g, function (m) {
      return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[m];
    });
  }

  // إعادة فهرسة أسماء الحقول المخفية: Prefix[0].Name -> Prefix[idx].Name
  function reindex(tbodySelector, prefix) {
    $(tbodySelector + ' tr').each(function (idx, tr) {
      $(tr).find('input[type=hidden]').each(function () {
        const name = $(this).attr('name');
        const prop = name.substring(name.lastIndexOf('.') + 1);
        $(this).attr('name', prefix + '[' + idx + '].' + prop);
      });
    });
  }

  // إضافة صف جديد
  function appendRow(tbodySelector, prefix, i, p) {
    const $tr = $(`
      <tr>
        <td>${esc(p.name)}<input type="hidden" name="${prefix}[${i}].Name" value="${esc(p.name)}" /></td>
        <td>${esc(p.email)}<input type="hidden" name="${prefix}[${i}].Email" value="${esc(p.email)}" /></td>
        <td>${esc(p.phone)}<input type="hidden" name="${prefix}[${i}].Phone" value="${esc(p.phone)}" /></td>
        <td>${esc(p.organization)}<input type="hidden" name="${prefix}[${i}].Organization" value="${esc(p.organization)}" /></td>
        <td class="text-center"><button type="button" class="btn btn-link p-0 text-danger fw-bold" data-delete-row>&#10006;</button></td>
      </tr>`);
    $(tbodySelector).append($tr);
  }

  // عرض رسالة خطأ تحت الحقل
  function setErr(field, msg) { if (msg) $(`span[data-valmsg-for='${field}']`).text(msg); }

  // الميثود العمومي الذي تناديه كل صفحة
  window.initPeopleModal = function initPeopleModal(opts) {
    const buttonId = opts.buttonId;    // زر "Add..."
    const listId   = opts.listId;      // tbody id
    const modalId  = opts.modalId;     // modal id
    const prefix   = opts.prefix;      // "SuspectedPersons" أو "RelatedPersons"

    const $btnAdd  = $('#' + buttonId);
    const $list    = $('#' + listId);
    const $modalEl = document.getElementById(modalId);
    const modal    = new bootstrap.Modal($modalEl);

    // حذف صف (delegation)
    $list.on('click', '[data-delete-row]', function () {
      $(this).closest('tr').remove();
      reindex('#' + listId, prefix);
    });

    // فتح المودال وتحميل الـpartial
    $btnAdd.on('click', function () {
      const peopleType = $btnAdd.data('type'); // "Suspected" أو "Related"
      $.get('/People/Create', { peopletype: peopleType }).done(function (html) {
        $('#' + modalId + ' .modal-body').html(html);
        $.validator.unobtrusive.parse('#personForm');
        modal.show();

        // Submit داخل المودال
        $('#personForm').off('submit').on('submit', function (e) {
          e.preventDefault();
          const $form = $(this);
          $('[data-valmsg-for]').text('');

          $.post($form.attr('action') + '?peopletype=' + encodeURIComponent(peopleType), $form.serialize())
           .done(function (res) {
             if (res && res.ok === true && res.person) {
               const p = res.person;
               const i = (typeof res.index === 'number') ? res.index : $('#' + listId + ' tr').length;
               const serverPrefix = res.prefix || prefix;

               appendRow('#' + listId, serverPrefix, i, {
                 name: p.name || '',
                 email: p.email || '',
                 phone: p.phone || '',
                 organization: p.organization || ''
               });

               reindex('#' + listId, serverPrefix);
               modal.hide();
             } else if (res && res.errors) {
               setErr('Name', res.errors.Name);
               setErr('Email', res.errors.Email);
               setErr('Phone', res.errors.Phone);
               setErr('Organization', res.errors.Organization);
             } else {
               console.error('Unexpected response', res);
             }
           });
        });
      });
    });
  };
})();
