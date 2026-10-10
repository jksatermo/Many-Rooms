import { createApi } from "./api";

// Expo substitutes this public value at bundle time. Never put credentials here.
export const apiBaseUrl =
  process.env.EXPO_PUBLIC_API_BASE_URL ?? "http://localhost:5080";
export const api = createApi(apiBaseUrl);
