import { HttpClient } from '@angular/common/http';
import { computed, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { jwtDecode } from 'jwt-decode';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { LoginRequest, User, UserRole } from '../models';

const KEYS = {
  TOKEN:    'ts_token',
  ROLE:     'ts_role',
  UID:      'ts_uid',
  UNAME:    'ts_username',
};

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authApi = `${environment.apiUrl}/authentication`;

  private _token    = signal<string | null>(localStorage.getItem(KEYS.TOKEN));
  private _role     = signal<UserRole | null>(localStorage.getItem(KEYS.ROLE) as UserRole | null);
  private _userId   = signal<number | null>(
    localStorage.getItem(KEYS.UID) ? +localStorage.getItem(KEYS.UID)! : null
  );
  private _username = signal<string | null>(localStorage.getItem(KEYS.UNAME));

  readonly token       = this._token.asReadonly();
  readonly currentRole = this._role.asReadonly();
  readonly currentUser = this._userId.asReadonly();
  readonly username    = this._username.asReadonly();
  readonly isLoggedIn  = computed(() => !!this._token());

  constructor(private http: HttpClient, private router: Router) {}

  // ✅ LOGIN
  login(req: LoginRequest): Observable<any> {
    return this.http.post<any>(`${this.authApi}/login`, req).pipe(
      tap(res => this.persist(res.token)) // 🔥 only token
    );
  }

  // ✅ REGISTER
  register(req: any): Observable<User> {
    return this.http.post<User>(`${this.authApi}/register`, req);
  }

  // ✅ LOGOUT
  logout(): void {
    Object.values(KEYS).forEach(k => localStorage.removeItem(k));
    this._token.set(null);
    this._role.set(null);
    this._userId.set(null);
    this._username.set(null);
    this.router.navigate(['/login']);
  }

  // ✅ REDIRECT
  redirectByRole(): void {
    const role = this._role();

    const map: Record<string, string> = {
      Admin: '/admin',
      HR: '/hr',
      Manager: '/manager',
      Employee: '/employee',
      Mentor: '/mentor',
      Intern: '/intern'
    };

    this.router.navigate([map[role ?? ''] ?? '/login']);
  }

  // 🔥 MAIN FIX HERE
  private persist(token: string): void {
    const decoded: any = jwtDecode(token);

    const userId =
      decoded["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"];

    const username =
      decoded["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"];

    const role =
      decoded["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];

    // ✅ store in localStorage
    localStorage.setItem(KEYS.TOKEN, token);
    localStorage.setItem(KEYS.UID, userId);
    localStorage.setItem(KEYS.UNAME, username);
    localStorage.setItem(KEYS.ROLE, role);

    // ✅ update signals
    this._token.set(token);
    this._userId.set(+userId);
    this._username.set(username);
    this._role.set(role as UserRole);
  }
}