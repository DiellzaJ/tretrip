import { API_BASE_URL } from "../../api/client";
import type { Trip } from "../scheduler/tripsApi";

export type DriverAction = "arrived" | "start" | "complete";

async function driverRequest<T>(path: string, accessToken: string, method = "GET"): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!response.ok) {
    const payload: unknown = await response.json().catch(() => null);
    const message = payload && typeof payload === "object" && "message" in payload &&
      typeof payload.message === "string" ? payload.message : null;
    if (response.status === 401) throw new Error("Your session expired. Please sign in again.");
    if (response.status === 403) throw new Error("This action is not permitted for your account.");
    if (response.status === 409) throw new Error(message ?? "The trip has changed. Refresh and try again.");
    throw new Error(message ?? `Request failed (${response.status}).`);
  }
  return await response.json() as T;
}

export function getDriverTrips(token: string): Promise<Trip[]> {
  return driverRequest<Trip[]>("/api/driver/trips", token);
}

export function updateDriverTrip(
  token: string, tripId: number, action: DriverAction,
): Promise<{ message: string; tripId: number; status: string }> {
  return driverRequest(`/api/driver/trips/${tripId}/${action}`, token, "PATCH");
}
