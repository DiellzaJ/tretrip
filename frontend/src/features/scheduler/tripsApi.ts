import { API_BASE_URL } from "../../api/client";

export type TripStatus = "Pending" | "Assigned" | "Arrived" | "InProgress" | "Completed" | "Cancelled" | "Expired";

export interface Trip {
  id: number;
  schedulerId: number;
  driverId: number | null;
  driverName: string | null;
  vehicleId: number | null;
  vehiclePlateNumber: string | null;
  taxiCompanyId: number | null;
  taxiCompanyName: string | null;
  pickupAddress: string;
  pickupLatitude: number;
  pickupLongitude: number;
  destinationAddress: string;
  destinationLatitude: number;
  destinationLongitude: number;
  distanceKm: number;
  estimatedDurationMinutes: number;
  price: number;
  scheduledAt: string;
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
  status: TripStatus;
}

export interface CreateTripRequest {
  pickupAddress: string;
  pickupLatitude: number;
  pickupLongitude: number;
  destinationAddress: string;
  destinationLatitude: number;
  destinationLongitude: number;
  scheduledAt: string;
}

async function callApi<T>(path: string, token: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(init.body ? { "Content-Type": "application/json" } : {}),
      ...init.headers,
    },
  });
  if (!response.ok) {
    const body: unknown = await response.json().catch(() => null);
    const serverMessage = body && typeof body === "object" && "message" in body
      && typeof body.message === "string" ? body.message : null;
    if (response.status === 401) throw new Error("Session expired. Sign in again.");
    if (response.status === 403) throw new Error("You do not have permission for this action.");
    throw new Error(serverMessage ?? `Request failed (${response.status}).`);
  }
  return (await response.json()) as T;
}

export function getMyTrips(token: string): Promise<Trip[]> {
  return callApi<Trip[]>("/api/trips", token);
}

export function createTrip(token: string, body: CreateTripRequest): Promise<Trip> {
  return callApi<Trip>("/api/trips", token, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function cancelTrip(token: string, id: number): Promise<{ status: string }> {
  return callApi<{ status: string }>(`/api/trips/${id}/cancel`, token, { method: "PATCH" });
}
