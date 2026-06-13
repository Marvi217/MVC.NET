
(function () {

  const topbar = document.querySelector('.topbar');
  if (topbar) {
    const onScroll = () => topbar.classList.toggle('scrolled', window.scrollY > 10);
    window.addEventListener('scroll', onScroll, { passive: true });
  }

  document.querySelectorAll('.alert.alert-dismissible').forEach(el => {
    setTimeout(() => {
      try {
        const a = bootstrap.Alert.getOrCreateInstance(el);
        a.close();
      } catch (_) { el.remove(); }
    }, 5000);
  });

  const grid = document.getElementById('seatGrid');
  if (grid) {
    const confirmSection = document.getElementById('confirmSection');
    const seatDisplay    = document.getElementById('seatDisplay');
    const priceDisplay   = document.getElementById('priceDisplay');
    const seatInputs     = document.getElementById('seatInputs');
    const clearBtn       = document.getElementById('clearSeats');
    const seatWarning    = document.getElementById('seatWarning');
    const bookSubmit     = document.getElementById('checkoutBtn');
    let selected = [];

    function countIsolations(selOverride) {
      var count = 0;
      var rows = grid.querySelectorAll('.seat-row');
      for (var ri = 0; ri < rows.length; ri++) {
        if (rows[ri].querySelector('.seat-btn.sofa')) continue;
        var items = [];
        var children = rows[ri].childNodes;
        for (var ci = 0; ci < children.length; ci++) {
          var el = children[ci];
          if (el.nodeType !== 1) continue;
          if (el.classList.contains('seat-btn')) {
            var seatNum = parseInt(el.dataset.seat);
            var isTaken = el.classList.contains('taken');
            var isSel = selOverride.some(function (s) { return s.seat === seatNum; });
            items.push({ barrier: false, free: !isTaken && !isSel });
          } else if (el.classList.contains('aisle-gap')) {
            items.push({ barrier: true });
          }
        }

        var sections = [];
        var cur = [];
        for (var i = 0; i < items.length; i++) {
          if (items[i].barrier) { if (cur.length) { sections.push(cur); cur = []; } }
          else cur.push(items[i]);
        }
        if (cur.length) sections.push(cur);

        for (var si = 0; si < sections.length; si++) {
          var sect = sections[si];
          if (sect.length <= 1) continue;
          var i = 0;
          while (i < sect.length) {
            if (sect[i].free) {
              var runStart = i;
              while (i < sect.length && sect[i].free) i++;
              if (i - runStart === 1) count++;
            } else {
              i++;
            }
          }
        }
      }
      return count;
    }

    function hasIsolatedSeat() {
      return countIsolations(selected) > countIsolations([]);
    }

    function updateUI() {
      if (seatInputs) {
        seatInputs.innerHTML = selected.map(function (s) {
          return '<input type="hidden" name="seatNumbers" value="' + s.seat + '" />';
        }).join('');
      }
      if (seatDisplay) {
        seatDisplay.textContent = selected.map(function (s) { return s.label; }).join(', ');
      }
      if (confirmSection) {
        confirmSection.style.display = selected.length ? 'block' : 'none';
      }
      if (selected.length > 0) {
        var isolated = hasIsolatedSeat();
        if (seatWarning) seatWarning.style.display = isolated ? 'block' : 'none';
        if (bookSubmit)  bookSubmit.disabled = isolated;
        if (priceDisplay) {
          var pricePerSeat = confirmSection ? parseFloat(confirmSection.dataset.price || '0') : 0;
          if (pricePerSeat > 0) {
            priceDisplay.textContent = 'Razem: ' + (pricePerSeat * selected.length).toLocaleString('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' zł';
          }
        }
      } else {
        if (seatWarning)  seatWarning.style.display = 'none';
        if (bookSubmit)   bookSubmit.disabled = false;
        if (priceDisplay) priceDisplay.textContent = '';
      }
    }

    grid.addEventListener('click', function (e) {
      const btn = e.target.closest('.seat-btn');
      if (!btn || btn.classList.contains('taken')) return;

      const seat = parseInt(btn.dataset.seat);
      const label = btn.dataset.label || String(seat);

      const alreadySelected = selected.findIndex(function (s) { return s.seat === seat; }) >= 0;

      if (alreadySelected) {
        selected = selected.filter(function (s) { return s.seat !== seat; });
        btn.classList.remove('selected');
      } else {
        selected.push({ seat: seat, label: label });
        btn.classList.add('selected');
      }
      updateUI();
    });

    if (clearBtn) {
      clearBtn.addEventListener('click', function () {
        selected = [];
        grid.querySelectorAll('.seat-btn.selected').forEach(function (b) {
          b.classList.remove('selected');
        });
        updateUI();
      });
    }

    var checkoutBtn = document.getElementById('checkoutBtn');
    if (checkoutBtn) {
      checkoutBtn.addEventListener('click', function () {
        if (selected.length === 0 || this.disabled) return;
        var confirmSection = document.getElementById('confirmSection');
        var marathonId  = confirmSection ? confirmSection.dataset.marathonId  : '';
        var screeningId = confirmSection ? confirmSection.dataset.screeningId : '';
        var seats = selected.map(function (s) { return s.seat; });
        if (marathonId) {
          window.location.href = '/Marathons/Checkout/' + marathonId + '?' + seats.map(function (s) { return 'seats=' + s; }).join('&');
        } else {
          window.location.href = '/Checkout?screeningId=' + screeningId + '&seats=' + seats.join(',');
        }
      });
    }

    if (typeof signalR !== 'undefined') {
      var confirmSect = document.getElementById('confirmSection');
      var screeningId = confirmSect ? (confirmSect.dataset.screeningId || '') : '';
      var marathonId  = confirmSect ? (confirmSect.dataset.marathonId  || '') : '';

      function showSeatConflictToast(label) {
        var toast = document.createElement('div');
        toast.className = 'alert alert-warning position-fixed shadow';
        toast.style.cssText = 'top:1rem;right:1rem;z-index:9999;max-width:360px;font-size:.88rem;';
        toast.innerHTML = '<i class="bi bi-exclamation-triangle me-2"></i>Miejsce <strong>' + label + '</strong> zostało właśnie zajęte przez innego użytkownika. Prosimy o wybór innego miejsca.';
        document.body.appendChild(toast);
        setTimeout(function () { toast.remove(); }, 6000);
      }

      var seatConn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/seats')
        .withAutomaticReconnect()
        .build();

      seatConn.on('SeatTaken', function (seatNumber) {
        var btn = document.querySelector('.seat-btn[data-seat="' + seatNumber + '"]');
        if (!btn) return;
        btn.classList.add('taken');
        btn.disabled = true;
        var idx = selected.findIndex(function (s) { return s.seat === seatNumber; });
        if (idx >= 0) {
          var label = selected[idx].label;
          selected.splice(idx, 1);
          btn.classList.remove('selected');
          showSeatConflictToast(label);
        }
        updateUI();
      });

      seatConn.on('SeatReleased', function (seatNumber) {
        var btn = document.querySelector('.seat-btn[data-seat="' + seatNumber + '"]');
        if (!btn) return;
        btn.classList.remove('taken');
        btn.disabled = false;
        updateUI();
      });

      seatConn.start().then(function () {
        if (screeningId) seatConn.invoke('JoinScreening', parseInt(screeningId));
        if (marathonId)  seatConn.invoke('JoinMarathon',  parseInt(marathonId));
      }).catch(function (err) {
        console.error('SignalR error:', err);
      });
    }
  }

  (function () {
    var wrapper = document.getElementById('seatMapWrapper');
    var grid = document.getElementById('seatGrid');
    if (!wrapper || !grid) return;
    function fit() {
      grid.style.zoom = '';
      var gw = grid.scrollWidth;
      var ww = wrapper.clientWidth;
      if (gw > ww) grid.style.zoom = ww / gw;
    }
    fit();
    window.addEventListener('resize', fit, { passive: true });
  })();

  document.querySelectorAll('form[data-confirm-delete]').forEach(f => {
    f.addEventListener('submit', (e) => {
      if (!confirm('Are you sure you want to delete this item? This action cannot be undone.')) {
        e.preventDefault();
      }
    });
  });
})();