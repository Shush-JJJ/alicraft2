(function () {
    // Mobile nav toggle
    var btn = document.querySelector('.mobile-toggle');
    if (btn) {
        btn.addEventListener('click', function () {
            var links = document.querySelector('.nav-links');
            if (links) links.classList.toggle('open');
        });
    }

    // Priority+ navigation: as the window shrinks, the right-most page links disappear
    // one-by-one into an overflow menu (...) instead of all collapsing at once.
    // Pinned items (profile, logout, theme toggle, login/sign-up) stay always visible.
    (function () {
        var nav = document.querySelector('.navbar');
        if (!nav) return;
        var navLinks = nav.querySelector('.nav-links');
        if (!navLinks) return;
        if (navLinks.dataset.priorityInit === '1') return;
        navLinks.dataset.priorityInit = '1';

        // Force horizontal layout so items overflow rather than wrap to a 2nd row.
        navLinks.style.flexWrap = 'nowrap';
        navLinks.style.minWidth = '0';
        navLinks.style.overflow = 'visible'; // overflow handled by moving items, not clipping

        function isPinned(el) {
            if (!el || !el.classList) return false;
            // Profile pill (avatar + name) - always stays visible.
            if (el.classList.contains('nav-user')) return true;
            // Theme toggle button.
            if (el.classList.contains('theme-toggle')) return true;
            if (el.id === 'themeToggle') return true;
            // Sign up button (gradient pill).
            if (el.classList.contains('btn-primary')) return true;
            // Logout form (the <form> that wraps the Logout button).
            if (el.tagName === 'FORM') return true;
            // The Login link (href ends with /Account/Login).
            if (el.tagName === 'A') {
                var href = (el.getAttribute('href') || '').toLowerCase();
                if (href.indexOf('/account/login') !== -1) return true;
                if (href.indexOf('/account/profile') !== -1) return true; // belt and suspenders
            }
            return false;
        }

        // Snapshot original children + classification.
        var originalChildren = Array.from(navLinks.children);
        originalChildren.forEach(function (el) { el.style.flexShrink = '0'; });
        var primary = originalChildren.filter(function (el) { return !isPinned(el); });
        if (primary.length === 0) return;

        // Mark pinned items with a data attribute so we can spot accidental moves later.
        originalChildren.forEach(function (el) {
            if (isPinned(el)) el.dataset.navPinned = '1';
        });

        // The primary items split into two groups:
        //   Public group  = indexes 0..2  (Home, About, Shop)
        //   Personal group = indexes 3..  (admin or customer-only links)
        // The personal group disappears as a UNIT when the navbar overflows.
        // If overflow continues, the public group is also collapsed in.
        var publicCount = Math.min(3, primary.length);
        var personalGroup = primary.slice(3); // admin: Dashboard..Inbox; customer: Cart..Chat

        // Build the "more" toggle button.
        var moreBtn = document.createElement('button');
        moreBtn.type = 'button';
        moreBtn.className = 'nav-more';
        moreBtn.setAttribute('aria-label', 'More navigation');
        moreBtn.setAttribute('aria-expanded', 'false');
        moreBtn.title = 'More';
        moreBtn.style.flexShrink = '0';
        moreBtn.innerHTML =
            '<svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" aria-hidden="true">' +
            '<circle cx="5" cy="12" r="2"/><circle cx="12" cy="12" r="2"/><circle cx="19" cy="12" r="2"/></svg>' +
            '<span class="nav-more-label">More</span>';
        moreBtn.hidden = true;

        // Build the overflow menu (popover).
        var menu = document.createElement('div');
        menu.className = 'nav-more-menu';
        menu.hidden = true;

        // Place the toggle inside navLinks before the first pinned item so it sits
        // visually to the LEFT of the user/theme controls.
        var firstPinned = originalChildren.find(isPinned) || null;
        if (firstPinned) navLinks.insertBefore(moreBtn, firstPinned);
        else navLinks.appendChild(moreBtn);
        nav.appendChild(menu);

        var navInner = navLinks.parentNode;

        function update() {
            // 1. Reset: pull all primary items back into navLinks (before moreBtn).
            primary.forEach(function (el) {
                if (el.parentNode !== navLinks) navLinks.insertBefore(el, moreBtn);
            });
            moreBtn.hidden = true;
            menu.hidden = true;
            moreBtn.setAttribute('aria-expanded', 'false');

            // 2. Force layout flush.
            void navInner.offsetWidth;

            // 3. If everything fits inside .nav-inner, we're done. The browser does the
            //    width math for us via scrollWidth (natural content) vs clientWidth (available).
            if (navInner.scrollWidth <= navInner.clientWidth + 1) return;

            // 4. There IS overflow. Show the More button.
            moreBtn.hidden = false;

            // 5. Step 1 - move the entire PERSONAL group (admin section / customer cart
            //    section) into the overflow menu as a unit. So Dashboard..Inbox vanish
            //    together, instead of one-by-one starting from Inbox.
            if (personalGroup.length > 0) {
                personalGroup.forEach(function (el) { menu.appendChild(el); });
                void navInner.offsetWidth;
                if (navInner.scrollWidth <= navInner.clientWidth + 1) {
                    return; // personal group alone was enough
                }
            }

            // 6. Step 2 - still overflowing, so collapse the PUBLIC group (Home/About/Shop)
            //    into the menu too. Now the inline navbar is just brand + pinned + More.
            for (var k = 0; k < publicCount; k++) {
                if (primary[k].parentNode !== menu) menu.appendChild(primary[k]);
            }

            // 7. Reorder menu contents to follow the original primary sequence
            //    (Home, About, Shop, ...personal...) so the dropdown reads naturally.
            primary.forEach(function (el) {
                if (el.parentNode === menu) menu.appendChild(el);
            });

            // 8. If we somehow ended up with nothing in the menu (e.g. only pinned items
            //    overflowed, which we can't move), hide the empty More button.
            if (menu.children.length === 0) moreBtn.hidden = true;
        }

        moreBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            var nowOpen = menu.hidden;
            menu.hidden = !nowOpen;
            moreBtn.setAttribute('aria-expanded', String(nowOpen));
        });
        document.addEventListener('click', function (e) {
            if (menu.hidden) return;
            if (menu.contains(e.target) || moreBtn.contains(e.target)) return;
            menu.hidden = true;
            moreBtn.setAttribute('aria-expanded', 'false');
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && !menu.hidden) {
                menu.hidden = true;
                moreBtn.setAttribute('aria-expanded', 'false');
                moreBtn.focus();
            }
        });

        var rafId = null;
        function schedule() {
            if (rafId !== null) return;
            rafId = requestAnimationFrame(function () { rafId = null; update(); });
        }
        schedule();
        window.addEventListener('resize', schedule);
        // Re-measure when fonts/images load (they can shift widths).
        window.addEventListener('load', schedule);
    })();

    // Dark / light theme toggle (initial value is set by an inline head script for no-FOUC).
    var themeBtn = document.getElementById('themeToggle');
    if (themeBtn) {
        var sync = function () {
            var dark = document.documentElement.getAttribute('data-theme') === 'dark';
            themeBtn.setAttribute('aria-pressed', dark ? 'true' : 'false');
            themeBtn.title = dark ? 'Switch to light mode' : 'Switch to dark mode';
        };
        sync();
        themeBtn.addEventListener('click', function () {
            var dark = document.documentElement.getAttribute('data-theme') === 'dark';
            if (dark) {
                document.documentElement.removeAttribute('data-theme');
                try { localStorage.setItem('ac-theme', 'light'); } catch (e) {}
            } else {
                document.documentElement.setAttribute('data-theme', 'dark');
                try { localStorage.setItem('ac-theme', 'dark'); } catch (e) {}
            }
            sync();
        });
    }

    // Cascading Province -> City dropdown
    document.querySelectorAll('[data-province]').forEach(function (provSel) {
        var citySelId = provSel.getAttribute('data-city-target');
        var citySel = document.getElementById(citySelId);
        if (!citySel) return;
        var preset = citySel.getAttribute('data-preset') || '';
        populateProvinces(provSel, provSel.value, function () {
            if (provSel.value) loadCities(provSel.value, citySel, preset);
        });
        provSel.addEventListener('change', function () {
            loadCities(provSel.value, citySel, '');
        });
    });

    function populateProvinces(sel, current, done) {
        fetch('/api/locations/provinces')
            .then(function (r) { return r.json(); })
            .then(function (list) {
                sel.innerHTML = '<option value="">Select province…</option>';
                list.forEach(function (p) {
                    var opt = document.createElement('option');
                    opt.value = p; opt.textContent = p;
                    if (p === current) opt.selected = true;
                    sel.appendChild(opt);
                });
                if (done) done();
            });
    }

    function loadCities(province, sel, preset) {
        if (!province) { sel.innerHTML = '<option value="">Select city…</option>'; return; }
        fetch('/api/locations/cities?province=' + encodeURIComponent(province))
            .then(function (r) { return r.json(); })
            .then(function (list) {
                sel.innerHTML = '<option value="">Select city/municipality…</option>';
                list.forEach(function (c) {
                    var opt = document.createElement('option');
                    opt.value = c; opt.textContent = c;
                    if (c === preset) opt.selected = true;
                    sel.appendChild(opt);
                });
            });
    }

    // Auto-dismiss alerts
    document.querySelectorAll('.alert[data-auto-dismiss]').forEach(function (a) {
        setTimeout(function () { a.style.display = 'none'; }, 4500);
    });

    // Show / hide password toggle.
    // Auto-wraps every <input type="password"> with a button that flips the input type.
    var EYE_SHOW =
        '<svg class="icon-show" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/></svg>';
    var EYE_HIDE =
        '<svg class="icon-hide" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<path d="M17.94 17.94A10.94 10.94 0 0 1 12 19c-6.5 0-10-7-10-7a18.4 18.4 0 0 1 4.06-5.05"/>' +
        '<path d="M9.9 4.24A10.93 10.93 0 0 1 12 4c6.5 0 10 7 10 7a18.5 18.5 0 0 1-2.16 3.19"/>' +
        '<path d="M14.12 14.12a3 3 0 1 1-4.24-4.24"/><line x1="2" y1="2" x2="22" y2="22"/></svg>';

    document.querySelectorAll('input[type="password"]').forEach(function (input) {
        if (input.dataset.noToggle === 'true') return;
        if (input.parentElement && input.parentElement.classList.contains('password-wrap')) return;

        var wrap = document.createElement('div');
        wrap.className = 'password-wrap';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'password-toggle';
        btn.setAttribute('aria-label', 'Show password');
        btn.title = 'Show password';
        btn.innerHTML = EYE_SHOW + EYE_HIDE;
        wrap.appendChild(btn);

        btn.addEventListener('click', function () {
            var showing = input.type === 'text';
            input.type = showing ? 'password' : 'text';
            btn.classList.toggle('is-shown', !showing);
            var label = showing ? 'Show password' : 'Hide password';
            btn.setAttribute('aria-label', label);
            btn.title = label;
            input.focus();
        });
    });
})();
