import { reactive } from "vue";
import { UpdateStatus } from "@main/updater";

export const updaterState = reactive<{
	status: UpdateStatus;
	showOverlay: boolean;
}>({
	status: { type: "checking" },
	showOverlay: false
});

export function initializeFrontendUpdater(): void {
	// 1. Listen for status changes from Main
	window.api.updater.onStatus((status: UpdateStatus) => {
		updaterState.status = status;

		// Automatically show overlay if mandatory or downloaded
		if (status.isMandatory || status.type === "downloaded" || status.type === "available") {
			updaterState.showOverlay = true;
		}
	});

	// 2. Initial request for status
	window.api.updater.getStatus();
}

export function downloadUpdate(): void {
	window.api.updater.download();
}

export function installUpdate(): void {
	window.api.updater.install();
}

export function closeOverlay(): void {
	// Only allow closing if NOT mandatory
	if (!updaterState.status.isMandatory) {
		updaterState.showOverlay = false;
	}
}
