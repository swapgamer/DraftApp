import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface DashboardMetrics { totalUsers: number; activeUsers: number; totalPlayers: number; totalChatQueries: number; }
export interface AdminUser { id: string; email: string; displayName: string; isActive: boolean; isSystemAdmin: boolean; adminExpiresAtUtc?: string; roles: string[]; createdAtUtc: string; }
export interface LoginHistory { id: number; userId: string; email: string; succeeded: boolean; ipAddress?: string; occurredAtUtc: string; }
export interface AuditLog { id: number; userId?: string; action: string; entityName: string; entityId: string; occurredAtUtc: string; }

@Injectable({ providedIn: 'root' })
export class AdminService {
  constructor(private readonly http: HttpClient) {}
  getDashboard() { return this.http.get<DashboardMetrics>(`${environment.apiUrl}/admin/dashboard`); }
  getUsers() { return this.http.get<AdminUser[]>(`${environment.apiUrl}/admin/users`); }
  setUserStatus(id: string, active: boolean) { return this.http.patch<void>(`${environment.apiUrl}/admin/users/${id}/status`, null, { params: new HttpParams().set('active', active) }); }
  grantTemporaryAdmin(id: string, expiresAtUtc?: string) { return this.http.post<void>(`${environment.apiUrl}/admin/users/${id}/temporary-admin`, { expiresAtUtc: expiresAtUtc || null }); }
  revokeTemporaryAdmin(id: string) { return this.http.delete<void>(`${environment.apiUrl}/admin/users/${id}/temporary-admin`); }
  getLoginHistory() { return this.http.get<LoginHistory[]>(`${environment.apiUrl}/admin/login-history`); }
  getAuditLogs() { return this.http.get<AuditLog[]>(`${environment.apiUrl}/admin/audit-logs`); }
  clearLoginHistory() { return this.http.delete<void>(`${environment.apiUrl}/admin/login-history`); }
  clearAuditLogs() { return this.http.delete<void>(`${environment.apiUrl}/admin/audit-logs`); }
}
