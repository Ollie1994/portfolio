// Scroll and focus after navigation, replacing Blazor's FocusOnNavigate. That focuses
// the <h1> of a new page but does nothing about a #fragment on one: a link from a
// project page to /#contact landed at the top of the home page.
//
// NavigationFocus calls afterNavigation once a new page has rendered. Links within
// the page already showing are handled by the listener at the bottom.

export function afterNavigation(fragment, isNewPage) {
    const target = fragment ? document.getElementById(decodeURIComponent(fragment)) : null;

    if (target) {
        // scroll-behavior and scroll-padding-top in app.css apply: smooth unless the
        // user prefers reduced motion, and stopping below the sticky header.
        target.scrollIntoView();
        focusWithoutScrolling(target.matches('h1, h2, h3') ? target : target.querySelector('h1, h2, h3') ?? target);
    } else if (isNewPage) {
        focusWithoutScrolling(document.querySelector('h1'));
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

    const url = new URL(link.href);
    const samePage = url.origin === location.origin && url.pathname === location.pathname && url.search === location.search;
    if (!url.hash || !samePage) {
        return;
    }

    setTimeout(() => {
        if (location.hash === url.hash) {
            afterNavigation(url.hash.slice(1), false);
        }
    });
}, true);
