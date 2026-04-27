export interface ThemeInfo {
  id: string;
  name: string;
  base?: "paws-dark" | "paws-light";
  fileHash?: string;
}

let lastThemeId: string | null = null;

/**
 * Обновляет <link> теги в DOM для применения нужной темы.
 * Использует логику View Transitions для плавной смены и CSS fallback для старых систем.
 */
export async function updateThemeLinks(theme: ThemeInfo): Promise<void> {
  const baseLink = document.getElementById("app-theme-base-link") as HTMLLinkElement;
  const customLink = document.getElementById("app-theme-custom-link") as HTMLLinkElement;

  if (!baseLink || !customLink) return;

  const performChange = () => {
    const baseThemeId = theme.base || (theme.id.startsWith("paws-") ? theme.id : "paws-dark");
    const baseHref = `http://pawsapp.localhost/themes/${baseThemeId}.css?t=${Date.now()}`;
    
    baseLink.href = baseHref;
    document.documentElement.setAttribute("data-theme", baseThemeId.includes("dark") ? "dark" : "light");

    if (theme.fileHash) {
      customLink.href = `http://pawstheme.localhost/${theme.fileHash}?t=${Date.now()}`;
    } else {
      customLink.href = "";
    }
  };

  // Если тема не поменялась, применяем изменения напрямую без глобальной анимации,
  // чтобы избежать гостинга на анимированных элементах (логотип и т.д.)
  if (lastThemeId === theme.id) {
    performChange();
    return;
  }

  const cacheSplashColors = () => {
    setTimeout(() => {
      const styles = getComputedStyle(document.documentElement);
      const bg = styles.getPropertyValue('--paws-color-bg-primary').trim();
      const accent = styles.getPropertyValue('--paws-color-accent-primary').trim();
      const text = styles.getPropertyValue('--paws-color-text-primary').trim();
      
      if (bg) localStorage.setItem('paws-splash-bg', bg);
      if (accent) localStorage.setItem('paws-splash-accent', accent);
      if (text) localStorage.setItem('paws-splash-text', text);
    }, 150); // Ждем короткое время, чтобы стили точно пересчитались
  };

  // Pure CSS Transition (Magical feel without DOM freezing)
  document.body.classList.add("paws-theme-transitioning");
  performChange();
  lastThemeId = theme.id;
  
  setTimeout(() => {
    document.body.classList.remove("paws-theme-transitioning");
    cacheSplashColors();
  }, 350);
}
