export const API_BASE_URL =
  import.meta.env.VITE_API_URL ?? "http://localhost:5166";

export type UserRole = "Admin" | "Scheduler" | "Driver";

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  userId: number;
  fullName: string;
  email: string;
  role: UserRole;
  mustChangePassword: boolean;
}

export async function login(
  email: string,
  password: string
): Promise<LoginResponse> {
  const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ email, password }),
  });

  if (!response.ok) {
    if (response.status === 401) {
      throw new Error("Invalid email or password.");
    }
    if (response.status === 429) {
      throw new Error("Too many login attempts. Try again later.");
    }
    throw new Error(`Login failed (${response.status}).`);
  }

  return response.json() as Promise<LoginResponse>;
}

export async function apiRequest<T>(
  path: string,
  token: string,
  options: RequestInit = {}
): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
      ...options.headers,
    },
  });

  if (!response.ok) {
    throw new Error(`API request failed (${response.status}).`);
  }

  return response.json() as Promise<T>;
}