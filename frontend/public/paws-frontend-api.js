// Paws Frontend API Bridge v2.0
// Simplifies communication between a plugin/settings frame and the main process via the renderer.

(function () {
	let messageId = 0;
	const pendingPromises = new Map();
	const noticeHandlers = new Set();

	// Listen for responses from the main renderer process
	window.addEventListener("message", event => {
		// Basic security: In a real sandboxed environment, we'd check the origin.
		// For file:// and custom protocols, this is tricky, so we trust the parent.
		if (event.source !== window.parent) return;

		const { id, result, error, channel } = event.data;

		// Handle one-way notices from the main renderer
		if (channel && channel === "notice") {
			const notice = event.data.payload;
			noticeHandlers.forEach(handler => handler(notice));

			// Specifically handle theme changes to update stylesheets
			if (notice.type === "theme-changed") {
				const timestamp = new Date().getTime();
				const baseLink = document.getElementById("paws-theme-base-link");
				const customLink = document.getElementById("paws-theme-custom-link");

				const themeState = notice.themeState;
				if (!themeState) return;

				const activeThemeInfo = themeState.availableThemes.find(
					t => t.id === themeState.activeThemeId
				);
				if (!activeThemeInfo) return;

				const isInitial = notice.initial || false;

				const ensureLink = (id, hrefPattern) => {
					let link = document.getElementById(id);
					// If not found by ID, try to find by partial href (in case it was hardcoded in HTML)
					if (!link && hrefPattern) {
						link = Array.from(document.getElementsByTagName("link")).find(l =>
							l.href.includes(hrefPattern)
						);
						if (link) link.id = id; // Give it the ID so we find it next time
					}

					if (!link) {
						link = document.createElement("link");
						link.id = id;
						link.rel = "stylesheet";
						document.head.appendChild(link);
					}
					return link;
				};

				const applyTheme = () => {
					const themeBaseLink = ensureLink("paws-theme-base-link", "paws-theme-base.css");
					const baseLink = ensureLink("paws-theme-link", "themes/");
					const customLink = ensureLink("paws-theme-custom-link", "paws-theme://");

					// Ensure base rendering styles are always loaded
					const newBaseHref = `paws-app://paws-theme-base.css?v=${timestamp}`;
					if (themeBaseLink.href !== newBaseHref) themeBaseLink.href = newBaseHref;

					const baseThemeInfo = themeState.availableThemes.find(
						t => t.id === `paws-${activeThemeInfo.base}`
					);

					if (baseLink && baseThemeInfo) {
						// Строим путь к базовой теме, используя ее 'base' свойство ('dark' или 'light')
						baseLink.href = `paws-app://themes/${activeThemeInfo.base}.css?v=${timestamp}`;
					}

					if (customLink && activeThemeInfo.isCustom && activeThemeInfo.file) {
						// Кастомные темы используют хеш из объекта file
						customLink.href = `paws-theme://${activeThemeInfo.file.hash}?v=${timestamp}`;
					} else {
						// Это базовая тема, кастомные стили не нужны
						customLink.href = "";
					}
				};

				if (isInitial) {
					// On initial load, just apply the theme instantly
					applyTheme();
				} else {
					// On a theme switch, wrap the change in the animation class
					document.body.classList.add("paws-theme-transitioning");
					applyTheme();
					setTimeout(() => {
						document.body.classList.remove("paws-theme-transitioning");
					}, 300);
				}
			}
			return;
		}

		// Handle promise-based request/response
		if (pendingPromises.has(id)) {
			const { resolve, reject } = pendingPromises.get(id);
			if (error) {
				reject(new Error(error));
			} else {
				resolve(result);
			}
			pendingPromises.delete(id);
		}
	});

	/**
	 * Sends a request to the main process and returns a promise that resolves with the result.
	 * @param {string} channel - The IPC channel to call.
	 * @param {*} [payload] - The data to send with the request.
	 * @returns {Promise<any>} A promise that resolves with the result from the main process.
	 */
	function request(channel, payload) {
		return new Promise((resolve, reject) => {
			const currentId = messageId++;
			pendingPromises.set(currentId, { resolve, reject });

			window.parent.postMessage({ channel, id: currentId, payload }, "*");
		});
	}

	// Expose the simplified API on the window object
	const lifecycleListeners = [];
	const themeListeners = [];
	const modeListeners = [];

	window.paws = {
		get: endpoint => request("get", endpoint),
		post: (endpoint, body) => request("post", { endpoint, body }),
		getStoreValue: key => request("get-store-value", key),
		setStoreValue: (key, value) => request("set-store-value", { key, value }),
		showOpenDialog: options => request("show-open-dialog", options),
		restartApp: () => request("restart-app"),
		resizeWindow: (isCompact) => request("resize-window", { isCompact }),

		storage: {
			uploadAsset: filePath => request("storage", { method: "uploadAsset", arg: filePath }),
			uploadTemp: buffer => request("storage", { method: "uploadTemp", arg: buffer }),
			uploadTempPath: filePath => request("storage", { method: "uploadTempPath", arg: filePath }),
			processAsset: (assetId, options) =>
				request("storage", { method: "processAsset", arg: { assetId, options } })
		},

		// Event subscription
		on: (event, callback) => {
			if (event === "theme-changed") {
				themeListeners.push(callback);
			} else if (event === "mode-changed") {
				modeListeners.push(callback);
			} else if (event === "lifecycle") {
				lifecycleListeners.push(callback);
			}
		},

		// Method for the plugin UI to listen for notices from the main app
		onNotice: callback => {
			noticeHandlers.add(callback);
			return () => noticeHandlers.delete(callback);
		},

		notifyParent: (noticeType, payload) => {
			window.parent.postMessage({ channel: "notice-from-frame", noticeType, payload }, "*");
		}
	};

	// Listen for specific notices to dispatch to 'on' listeners
	window.addEventListener("message", event => {
		if (event.source !== window.parent) return;
		const { channel, payload } = event.data;

		if (channel === "notice") {
			if (payload.type === "theme-changed") {
				themeListeners.forEach(cb => cb(payload.themeState));
			} else if (payload.type === "mode-changed") {
				modeListeners.forEach(cb => cb(payload.mode));
			}
		} else if (channel === "lifecycle") {
			lifecycleListeners.forEach(cb => cb(payload.event));
		}
	});

	// Alias for compatibility with main renderer API
	window.api = {
		backend: {
			get: window.paws.get,
			post: window.paws.post
		},
		storage: window.paws.storage,
		electron: {
			showOpenDialog: window.paws.showOpenDialog,
			restartApp: window.paws.restartApp,
			resizeWindow: window.paws.resizeWindow
		}
	};

	// Auto-signal Ready
	if (document.readyState === "complete" || document.readyState === "interactive") {
		setTimeout(() => window.parent.postMessage({ channel: "paws:client-ready", id: 0 }, "*"), 0);
	} else {
		window.addEventListener("DOMContentLoaded", () => {
			window.parent.postMessage({ channel: "paws:client-ready", id: 0 }, "*");
		});
	}
})();
