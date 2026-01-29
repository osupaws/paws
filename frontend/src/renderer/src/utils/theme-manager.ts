/**
 * Updates the theme link elements in the DOM.
 * @param baseThemeName The base theme name (e.g. 'dark', 'light').
 * @param customFileHash Optional hash for a custom theme file.
 */
export function updateThemeLinks(baseThemeName: string, customFileHash?: string): void {
	const baseLink = document.getElementById("app-theme-base-link") as HTMLLinkElement;
	const customLink = document.getElementById("app-theme-custom-link") as HTMLLinkElement;

	if (baseLink) {
		baseLink.href = `paws-app://themes/${baseThemeName}.css`;
	}

	if (customLink) {
		if (customFileHash) {
			customLink.href = `paws-theme://${customFileHash}`;
		} else {
			customLink.href = "";
		}
	}
}
