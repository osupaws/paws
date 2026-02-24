import vue from "@vitejs/plugin-vue";
import { defineConfig, externalizeDepsPlugin } from "electron-vite";
import { resolve } from "path";

export default defineConfig({
	main: {
		resolve: {
			alias: {
				"@main": resolve("src/main"),
				"@common": resolve("src/common")
			}
		},
		plugins: [externalizeDepsPlugin()]
	},
	preload: {
		resolve: {
			alias: {
				"@main": resolve("src/main"),
				"@preload": resolve("src/preload"),
				"@common": resolve("src/common")
			}
		},
		build: {
			rollupOptions: {
				input: {
					index: resolve(__dirname, "src/preload/index.ts")
				}
			}
		},
		plugins: [externalizeDepsPlugin()]
	},
	renderer: {
		resolve: {
			alias: {
				"@renderer": resolve("src/renderer/src"),
				"@ui-kit": resolve("ui-kit/src/components"),
				"@common": resolve("src/common")
			}
		},
		build: {
			rollupOptions: {
				input: {
					main: resolve(__dirname, "src/renderer/index.html"),
					splash: resolve(__dirname, "src/renderer/splash.html"),
					crash: resolve(__dirname, "src/renderer/crash.html")
				}
			}
		},
		publicDir: resolve(__dirname, "public"),
		plugins: [vue()]
	}
});
