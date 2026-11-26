// Scroll and focus after navigation, replacing Blazor's FocusOnNavigate. That focuses
// the <h1> of a new page but does nothing about a #fragment on one: a link from a
// project page to /#contact landed at the top of the home page.
//
// NavigationFocus calls afterNavigation once a new page has rendered. Links within
// the page already showing are handled by the listener at the bottom.

// The page last shown: its address without the #fragment. Kept here, for the whole
// visit, rather than in NavigationFocus. The Router builds a new layout, and with it a
// new NavigationFocus, when it switches between a page and the not-found page, and the
// new component took that for the first load: focus was left on the page body going to
// or from "Page not found", including its "Back to the start" link.
let currentPage = null;

export function afterNavigation(page, fragment) {
    // Not on the first load: there, focus belongs at the top of the document, so the
    // first Tab reaches the skip link and the navigation. A #fragment is still scrolled to.
    const isNewPage = currentPage !== null && page !== currentPage;
    currentPage = page;
    showTarget(fragment, isNewPage);
}

function showTarget(fragment, isNewPage) {
    const target = fragment ? document.getElementById(decodeFragment(fragment)) : null;

    if (target) {
        // scroll-behavior and scroll-padding-top in app.css apply: smooth unless the
        // user prefers reduced motion, and stopping below the sticky header.
        target.scrollIntoView();
        focusWithoutScrolling(target.matches('h1, h2, h3') ? target : target.querySelector('h1, h2, h3') ?? target);
    } else if (isNewPage) {
        focusWithoutScrolling(document.querySelector('h1'));
    }
}

// The address can hold anything, and a malformed escape such as #%FF makes
// decodeURIComponent throw. Such a fragment matches no id either way.
function decodeFragment(fragment) {
    try {
        return decodeURIComponent(fragment);
    } catch {
        return fragment;
    }
}

// Moving focus is what makes the next Tab continue from here rather than from the
// link that was used, and what tells a screen reader where the user has landed.
// Headings and sections are not focusable; tabindex -1 lets a script focus them
// without adding them to the Tab order.
function focusWithoutScrolling(element) {
    if (!element) {
        return;
    }

    if (!element.hasAttribute('tabindex')) {
        element.setAttribute('tabindex', '-1');
    }

    element.focus({ preventScroll: true });
}

// A link to a section of the page already showing - the skip link, or the nav on the
// home page - is handled entirely by Blazor's script: it cancels the click, updates
// the address and scrolls, without telling .NET, so NavigationFocus never hears about
// it. Seen here in the capture phase, before Blazor cancels it, and acted on once
// Blazor has updated the address. Runs once, when NavigationFocus first imports this.
document.addEventListener('click', event => {
    const link = event.target instanceof Element ? event.target.closest('a[href]') : null;
    if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
        return;
    }

    // Blazor leaves these to the browser too: they open elsewhere or download.
    const target = link.getAttribute('target');
    if ((target && target !== '_self') || link.hasAttribute('download')) {
        return;
    }

    const url = new URL(link.href);
    const samePage = url.origin === location.origin && url.pathname === location.pathname && url.search === location.search;
    if (!url.hash || !samePage) {
        return;
    }

    setTimeout(() => {
        if (location.hash === url.hash) {
            showTarget(url.hash.slice(1), false);
        }
    });
}, true);
