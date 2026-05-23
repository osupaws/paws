import { reactive } from "vue";
import { callSidecar } from "../utils/sidecar-bridge";

export interface OsuProfile {
  username: string;
  avatar_url: string;
}

export const profileState = reactive({
  username: "player",
  avatarUrl: "/default-avatar.png",
  isConnected: false,
});

export async function fetchProfile(forceRefresh: boolean = false) {
  const resp = await callSidecar<OsuProfile>("getOsuProfile", { refresh: forceRefresh });
  console.log("[ProfileState] fetchProfile response:", resp);
  if (resp.success && resp.data) {
    profileState.username = resp.data.username || "player";
    profileState.avatarUrl = resp.data.avatar_url || "/default-avatar.png";
    profileState.isConnected = true;
  } else {
    profileState.username = "player";
    profileState.avatarUrl = "/default-avatar.png";
    profileState.isConnected = false;
  }
}

export function clearProfile() {
  profileState.username = "player";
  profileState.avatarUrl = "/default-avatar.png";
  profileState.isConnected = false;
}
