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

    // Cascading Province -> City dropdown.
    // The <select> elements are rendered with no <option>s (they're fetched via JS),
    // so `asp-for` cannot preselect a matching option at server render time.
    // We rely on explicit data-preset attributes to know which province/city was saved.
    document.querySelectorAll('[data-province]').forEach(function (provSel) {
        var citySelId = provSel.getAttribute('data-city-target');
        var citySel = document.getElementById(citySelId);
        if (!citySel) return;
        var cityPreset = citySel.getAttribute('data-preset') || '';
        var provincePreset = provSel.getAttribute('data-preset') || provSel.value || '';
        populateProvinces(provSel, provincePreset, function () {
            if (provSel.value) loadCities(provSel.value, citySel, cityPreset);
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

    // File input enhancer
    // Wraps every <input type="file"> (that doesn't opt out with data-no-enhance)
    // so that: (1) a styled "Choose file" button is shown initially, (2) after a
    // file is chosen, the button is hidden and the filename + an X remove button
    // are shown, (3) clicking X clears the selection and restores the button.
    // The native <input type="file"> is kept in the DOM (hidden) so normal form
    // submission still works without any server-side changes.
    (function () {
        var FILE_ICON =
            '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/></svg>';
        var X_ICON =
            '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>';

        function enhance(input) {
            if (input.dataset.fileEnhanced === '1') return;
            if (input.dataset.noEnhance === 'true') return;
            // Skip chat attachment pickers (they use their own paperclip UI via <label for>).
            if (input.id === 'chatImage') return;
            input.dataset.fileEnhanced = '1';

            var wrap = document.createElement('div');
            wrap.className = 'file-enh';
            input.parentNode.insertBefore(wrap, input);
            wrap.appendChild(input);

            // Hide the native input visually but keep it focusable.
            input.classList.add('file-enh-input');

            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'file-enh-btn';
            btn.innerHTML = FILE_ICON + '<span>Choose file</span>';
            btn.addEventListener('click', function () { input.click(); });
            wrap.appendChild(btn);

            var chosen = document.createElement('div');
            chosen.className = 'file-enh-chosen';
            chosen.hidden = true;
            var name = document.createElement('span');
            name.className = 'file-enh-name';
            var remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'file-enh-remove';
            remove.setAttribute('aria-label', 'Remove file');
            remove.title = 'Remove file';
            remove.innerHTML = X_ICON;
            chosen.appendChild(name);
            chosen.appendChild(remove);
            wrap.appendChild(chosen);

            function show(f) {
                if (f) {
                    name.textContent = f.name;
                    chosen.hidden = false;
                    btn.hidden = true;
                } else {
                    chosen.hidden = true;
                    btn.hidden = false;
                }
            }

            function onChange() { show(input.files && input.files[0]); }
            input.addEventListener('change', onChange);

            // Robustly clear the selected file. Setting value='' works in modern
            // browsers; older Safari ignores it. Falling back to clone-and-replace
            // forces files to empty while preserving name/id/accept/etc.
            function clearFile() {
                try { input.value = ''; } catch (err) { /* noop */ }
                if (input.files && input.files.length > 0) {
                    var clone = input.cloneNode(false);
                    clone.value = '';
                    input.parentNode.replaceChild(clone, input);
                    input = clone; // closure reference update (shared with btn click, etc.)
                    input.classList.add('file-enh-input');
                    input.dataset.fileEnhanced = '1';
                    input.addEventListener('change', onChange);
                }
            }

            // Use mousedown in addition to click so nothing can swallow the event
            // (e.g. a parent label or form handler) before we run.
            function handleRemove(e) {
                e.preventDefault();
                e.stopPropagation();
                clearFile();
                show(null);
            }
            remove.addEventListener('click', handleRemove);

            // Sync initial state (e.g., if the page was reloaded with a preselected file).
            show(input.files && input.files[0]);
        }

        document.querySelectorAll('input[type="file"]').forEach(enhance);
    })();
})();
