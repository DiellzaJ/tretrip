import { apiRequest } from "../../api/client";

export interface TripNotification {
  id: number;
  tripId: number;
  message: string;
  isRead: boolean;
  createdAt: string;
}

export async function getNotifications(token: string): Promise<TripNotification[]> {
  return apiRequest<TripNotification[]>("/api/notifications", token);
}

export async function markNotificationRead(token: string, id: number): Promise<void> {
  await apiRequest<{ notificationId: number; isRead: boolean }>(
    `/api/notifications/${id}/read`,
    token,
    { method: "PATCH" },
  );
}
