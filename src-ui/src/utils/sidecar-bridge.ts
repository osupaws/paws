import { invoke } from "@tauri-apps/api/core";

export interface SidecarResponse<T = any> {
  success: boolean;
  data: T | null;
  error: string | null;
}

/**
 * Вызывает команду в .NET Sidecar через мост Rust.
 * @param action Название действия (getConfig, saveTheme и т.д.)
 * @param params Параметры запроса
 */
export async function callSidecar<T = any>(
  action: string,
  params: Record<string, any> = {},
): Promise<SidecarResponse<T>> {
  try {
    console.log(`[SidecarBridge] Starting: ${action}`, params);
    // В Tauri v2 мы вызываем нашу Rust-команду 'call_sidecar'
    const result = await invoke<SidecarResponse<T>>("call_sidecar", {
      action,
      params,
    });
    console.log(`[SidecarBridge] Finished: ${action}`, result);
    return result;
  } catch (err) {
    console.error(`[SidecarBridge] Action '${action}' failed:`, err);
    return {
      success: false,
      data: null,
      error: String(err),
    };
  }
}
