import { HttpClient } from '@angular/common/http';
import { computed, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest, User, UserRole } from '../models';

const KEYS = {
  TOKEN:    'ts_token',
  ROLE:     'ts_role',
  UID:      'ts_uid',
  UNAME:    'ts_username',
  STATUS:   'ts_status',
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
  private _status   = signal<string | null>(localStorage.getItem(KEYS.STATUS));

  readonly token       = this._token.asReadonly();
  readonly currentRole = this._role.asReadonly();
  readonly currentUser = this._userId.asReadonly();
  readonly username    = this._username.asReadonly();
  readonly userStatus  = this._status.asReadonly();
  readonly isLoggedIn  = computed(() => !!this._token());
  readonly isAdmin     = computed(() => this._role() === 'Admin');
  readonly isHR        = computed(() => this._role() === 'HR');
  readonly isManager   = computed(() => this._role() === 'Manager');
  readonly isEmployee  = computed(() => this._role() === 'Employee');
  readonly isIntern    = computed(() => this._role() === 'Intern');
  readonly isMentor   = computed(() => this._role() === 'Mentor');

  constructor(private http: HttpClient, private router: Router) {}

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.authApi}/login`, req).pipe(
      tap(res => this.persist(res))
    );
  }

  register(req: RegisterRequest): Observable<User> {
    return this.http.post<User>(`${this.authApi}/register`, req);
  }

  logout(): void {
    Object.values(KEYS).forEach(k => localStorage.removeItem(k));
    this._token.set(null); this._role.set(null);
    this._userId.set(null); this._username.set(null); this._status.set(null);
    this.router.navigate(['/login']);
  }

  redirectByRole(): void {
    const map: Record<string, string> = {
      Admin: '/admin', HR: '/hr', Manager: '/manager',
      Employee: '/employee', Mentor: '/mentor', Intern: '/intern'
    };
    this.router.navigate([map[this._role() ?? ''] ?? '/login']);
  }

  private persist(res: AuthResponse): void {
    localStorage.setItem(KEYS.TOKEN,  res.token);
    localStorage.setItem(KEYS.ROLE,   res.role);
    localStorage.setItem(KEYS.UID,    String(res.userId));
    localStorage.setItem(KEYS.UNAME,  res.username);
    this._token.set(res.token);
    this._role.set(res.role as UserRole);
    this._userId.set(res.userId);
    this._username.set(res.username);
  }
}
