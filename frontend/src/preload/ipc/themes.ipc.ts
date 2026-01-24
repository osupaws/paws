import type { Theme } from "@common/types";
import { ipcRenderer } from "electron";

export const themesIpc = {
	getCustom: (): Promise<Theme[]> => {
		return ipcRenderer.invoke("themes:get-custom");
	}
};

export type ThemesAPI = typeof themesIpc;
