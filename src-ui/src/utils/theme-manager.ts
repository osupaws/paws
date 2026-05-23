export interface ThemeInfo {
  id: string;
  name: string;
  base?: "paws-dark" | "paws-light";
  fileHash?: string;
}

let lastThemeId: string | null = null;
let lastFileHash: string | null = null;

export async function updateThemeLinks(theme: ThemeInfo): Promise<void> {
  if (lastThemeId === theme.id && lastFileHash === (theme.fileHash || null)) {
    return; // Already applied, prevent redundant transition overrides
  }

  const baseThemeId = theme.base || (theme.id.startsWith("paws-") ? theme.id : "paws-dark");
  const baseHref = `http://pawsapp.localhost/themes/${baseThemeId}.css`;
  const customHref = theme.fileHash ? `http://pawstheme.localhost/${theme.fileHash}` : "";

  // 1. Preload the CSS files into cache (so they don't apply until we want them to)
  const preloadBase = document.createElement("link");
  preloadBase.rel = "preload";
  preloadBase.as = "style";
  preloadBase.href = baseHref;
  
  const promises: Promise<any>[] = [];
  promises.push(new Promise((resolve) => {
    preloadBase.onload = resolve;
    preloadBase.onerror = resolve;
    document.head.appendChild(preloadBase);
  }));

  let preloadCustom: HTMLLinkElement | null = null;
  if (customHref) {
    preloadCustom = document.createElement("link");
    preloadCustom.rel = "preload";
    preloadCustom.as = "style";
    preloadCustom.href = customHref;
    promises.push(new Promise((resolve) => {
      preloadCustom!.onload = resolve;
      preloadCustom!.onerror = resolve;
      document.head.appendChild(preloadCustom!);
    }));
  }

  // Wait for network requests to finish completely
  await Promise.all(promises);

  // 2. Prepare transition by adding class BEFORE swapping links
  document.body.classList.add("paws-theme-transitioning");
  document.body.offsetHeight; // force reflow so browser paints class state

  // 3. Swap the active links (they apply instantly from cache)
  const baseLink = document.getElementById("app-theme-base-link") as HTMLLinkElement;
  const customLink = document.getElementById("app-theme-custom-link") as HTMLLinkElement;
  
  if (baseLink) baseLink.href = baseHref;
  if (customLink) {
    if (customHref) {
      customLink.href = customHref;
    } else {
      customLink.href = "";
    }
  }

  lastThemeId = theme.id;
  lastFileHash = theme.fileHash || null;

  // Broadcast to plugins
  const iframes = Array.from(document.getElementsByTagName("iframe"));
  for (const iframe of iframes) {
    iframe.contentWindow?.postMessage({
      type: "paws:theme-changed",
      baseHref,
      customHref,
      themeId: theme.id
    }, "*");
  }

  // 4. Cleanup preloads
  if (preloadBase.parentNode) document.head.removeChild(preloadBase);
  if (preloadCustom && preloadCustom.parentNode) document.head.removeChild(preloadCustom);

  // 5. Remove transition class after animation finishes
  setTimeout(() => {
    document.body.classList.remove("paws-theme-transitioning");
  }, 350);
}
