import { store } from "@main/store/store";
import { ipcMain } from "electron";

ipcMain.on("store.get", (event, key, defaultValue) => {
  event.returnValue = store.get(key, defaultValue);
});

ipcMain.on("store.set", (_event, key, value) => {
  store.set(key, value);
});
