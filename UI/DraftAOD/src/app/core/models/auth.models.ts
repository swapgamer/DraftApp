export interface AuthUser { userId: string; email: string; displayName: string; roles: string[]; isSystemAdmin: boolean; }
export interface AuthResponse { accessToken: string; accessTokenExpiresAtUtc: string; refreshToken: string; user: AuthUser; }
export interface LoginRequest { email: string; password: string; }
export interface RegisterRequest extends LoginRequest { displayName: string; }
