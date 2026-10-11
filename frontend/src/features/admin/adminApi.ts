import { API_BASE_URL } from "../../api/client";

export interface AdminUser {
  id: number;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
  deactivatedAt: string | null;
}
export interface TaxiCompany {
  id: number;
  name: string;
  phone: string;
  isActive: boolean;
  contractStartDate: string;
  contractEndDate: string | null;
}
export interface AdminDriver {
  id: number;
  userId: number;
  fullName: string;
  email: string;
  licenseNumber: string;
  taxiCompanyId: number;
  taxiCompanyName: string;
  isActive: boolean;
  isAvailable: boolean;
}
export interface AdminVehicle {
  id: number;
  plateNumber: string;
  model: string;
  taxiCompanyId: number;
  taxiCompanyName: string;
  isActive: boolean;
  isAvailable: boolean;
}
export interface AdminTrip {
  id: number;
  schedulerId: number;
  schedulerName: string;
  schedulerEmail: string;
  driverName: string | null;
  vehiclePlateNumber: string | null;
  pickupAddress: string;
  destinationAddress: string;
  distanceKm: number;
  estimatedDurationMinutes: number;
  price: number;
  scheduledAt: string;
  createdAt: string;
  completedAt: string | null;
  status: string;
}
export interface CreateEmployee {
  fullName: string;
  email: string;
  password: string;
}
export interface CreateCompany {
  name: string;
  phone: string;
  contractStartDate: string;
  contractEndDate: string | null;
}
export interface CreateDriver {
  fullName: string;
  email: string;
  password: string;
  licenseNumber: string;
  taxiCompanyId: number;
}
export interface CreateVehicle {
  plateNumber: string;
  model: string;
  taxiCompanyId: number;
}

async function adminRequest<T>(token: string, endpoint: string, method = "GET", body?: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}/api/admin${endpoint}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (!response.ok) {
    const payload: unknown = await response.json().catch(() => null);
    let detail: string | null = null;
    if (payload && typeof payload === "object") {
      if ("message" in payload && typeof payload.message === "string") detail = payload.message;
      if (!detail && "errors" in payload && payload.errors && typeof payload.errors === "object") {
        const firstError = Object.values(payload.errors).flat().find((value) => typeof value === "string");
        if (typeof firstError === "string") detail = firstError;
      }
    }
    if (response.status === 401) throw new Error("Session expired. Please sign in again.");
    if (response.status === 403) throw new Error("You don't have permission for this action.");
    throw new Error(detail ?? `Request failed (${response.status}).`);
  }
  return response.json() as Promise<T>;
}

export const adminApi = {
  getUsers: (token: string) => adminRequest<AdminUser[]>(token, "/users"),
  getCompanies: (token: string) => adminRequest<TaxiCompany[]>(token, "/taxi-companies"),
  getDrivers: (token: string) => adminRequest<AdminDriver[]>(token, "/drivers"),
  getVehicles: (token: string) => adminRequest<AdminVehicle[]>(token, "/vehicles"),
  getTrips: (token: string) => adminRequest<AdminTrip[]>(token, "/trips"),
  createEmployee: (token: string, data: CreateEmployee) => adminRequest<AdminUser>(token, "/employees", "POST", data),
  createCompany: (token: string, data: CreateCompany) => adminRequest<TaxiCompany>(token, "/taxi-companies", "POST", data),
  createDriver: (token: string, data: CreateDriver) => adminRequest<AdminDriver>(token, "/drivers", "POST", data),
  createVehicle: (token: string, data: CreateVehicle) => adminRequest<AdminVehicle>(token, "/vehicles", "POST", data),
  setUserActive: (token: string, id: number, isActive: boolean) => adminRequest<unknown>(token, `/users/${id}/status`, "PATCH", { isActive }),
  setDriverAvailable: (token: string, id: number, isAvailable: boolean) => adminRequest<unknown>(token, `/drivers/${id}/availability`, "PATCH", { isAvailable }),
  setVehicleAvailable: (token: string, id: number, isAvailable: boolean) => adminRequest<unknown>(token, `/vehicles/${id}/availability`, "PATCH", { isAvailable }),
};
