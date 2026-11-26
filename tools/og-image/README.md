# Social card

`og-image.html` is the source of `src/Portfolio.Client/wwwroot/img/og-image.png`, the 1200×630
card shown when the site's link is shared on LinkedIn, Slack and so on.

```bash
node tools/og-image/render.mjs
```

Needs Node 22+ and Chrome, and Windows for the card to look as intended: it is set in Segoe UI.

## When to redo it

The card lists the projects that have a page, in the home page's order and accent colours. It
cannot read `ProjectCatalog`, so redo it when a project with a page is added, renamed or
recoloured. Then bump `?v=` on both image tags in `src/Portfolio.Client/wwwroot/index.html`:
platforms cache the image by URL, and without a new one, shares keep showing the old card.

## Why it looks the way it does

- **Large, near-black text.** Platforms show the card about 500px wide and re-compress it as
  JPEG, which blurs small or grey type first.
- **No subpixel anti-aliasing** (`--disable-lcd-text`). Its coloured edges turn into blur when
  the card is rescaled.
- **Coloured squares, not coloured text,** for the projects. Yellow text on white fails
  contrast at this size.

## Checking it

Open the PNG and look at it. After deploying, check the shared card in LinkedIn's
[Post Inspector](https://www.linkedin.com/post-inspector/), and by pasting the link into Slack
with a query string on the end to get past its cache.
