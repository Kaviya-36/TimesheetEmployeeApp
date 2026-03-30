import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  DashboardSummary,
  InternTaskCreateRequest,
  LeaveCreateRequest,
  PayrollCreateRequest,
  ProjectCreateRequest,
  TimesheetApprovalRequest,
  TimesheetCreateRequest, TimesheetUpdateRequest,
  UserUpdateRequest
} from '../models';

// ── Shared param builder ───────────────────────────────────────────────────
function buildParams(opts: Record<string, any>): HttpParams {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(opts)) {
    if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
  }
  return p;
}

// ── Timesheet ──────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class TimesheetService {
  private readonly api = `${environment.apiUrl}/timesheet`;
  constructor(private http: HttpClient) {}

  create(userId: number, req: TimesheetCreateRequest): Observable<any> {
    return this.http.post<any>(`${this.api}/${userId}/manual`, req);
  }
  submitWeekly(userId: number, req: { entries: { projectId: number; projectName: string; workDate: string; hours: number; taskDescription?: string }[]; submit: boolean }): Observable<any> {
    return this.http.post<any>(`${this.api}/${userId}/weekly`, req);
  }
  update(id: number, req: TimesheetUpdateRequest): Observable<any> {
    return this.http.put<any>(`${this.api}/${id}`, req);
  }
  delete(id: number): Observable<any> {
    return this.http.delete<any>(`${this.api}/${id}`);
  }
  getByUser(userId: number, page = 1, pageSize = 200, search?: string, status?: string, sortBy = 'date', sortDir = 'desc'): Observable<any> {
    return this.http.get<any>(`${this.api}/user/${userId}`, {
      params: buildParams({ pageNumber: page, pageSize, search, status, sortBy, sortDir })
    });
  }
  getAll(page = 1, pageSize = 200, search?: string, status?: string, sortBy = 'date', sortDir = 'desc'): Observable<any> {
    return this.http.get<any>(this.api, {
      params: buildParams({ pageNumber: page, pageSize, search, status, sortBy, sortDir })
    });
  }
  approveOrReject(req: TimesheetApprovalRequest): Observable<any> {
    return this.http.post<any>(`${this.api}/approve`, req);
  }
}

// ── Attendance ─────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class AttendanceService {
  private readonly api = `${environment.apiUrl}/Attendance`;
  constructor(private http: HttpClient) {}

  checkIn(): Observable<any>  { return this.http.post<any>(`${this.api}/checkin`, {}); }
  checkOut(): Observable<any> { return this.http.post<any>(`${this.api}/checkout`, {}); }
  getMyAttendance(userId: number, page = 1, pageSize = 200): Observable<any> {
    return this.http.get<any>(`${this.api}/me`, { params: buildParams({ pageNumber: page, pageSize }) });
  }
  getAll(page = 1, size = 200): Observable<any> {
    return this.http.get<any>(`${this.api}/all`, { params: buildParams({ pageNumber: page, pageSize: size }) });
  }
  getTodayStatus(userId: number): Observable<any> {
    return this.http.get<any>(`${this.api}/today/${userId}`);
  }
}

// ── Leave ──────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class LeaveService {
  private readonly api = `${environment.apiUrl}/leave`;
  constructor(private http: HttpClient) {}

  apply(userId: number, req: LeaveCreateRequest): Observable<any> {
    return this.http.post<any>(`${this.api}/apply`, req);
  }
  getMyLeaves(userId: number, page = 1, pageSize = 200, search?: string, status?: string, sortDir = 'desc'): Observable<any> {
    return this.http.get<any>(`${this.api}/user/${userId}`, {
      params: buildParams({ pageNumber: page, pageSize, search, status, sortDir })
    });
  }
  getPending(): Observable<any> { return this.http.get<any>(`${this.api}/pending`); }
  getAll(page = 1, pageSize = 200, search?: string, status?: string, sortDir = 'desc'): Observable<any> {
    return this.http.get<any>(`${this.api}/getall`, {
      params: buildParams({ pageNumber: page, pageSize, search, status, sortDir })
    });
  }
  approveOrReject(data: { leaveId: number; approvedById: number; isApproved: boolean; managerComment: string; }): Observable<any> {
    return this.http.put<any>(`${this.api}/approve`, data);
  }
  getLeaveTypes(): Observable<any> { return this.http.get<any>(`${this.api}/types`); }
  deleteLeave(leaveId: number): Observable<any> { return this.http.delete<any>(`${this.api}/${leaveId}`); }
}

// ── Project ────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly api = `${environment.apiUrl}/project`;
  constructor(private http: HttpClient) {}

  create(req: ProjectCreateRequest): Observable<any> { return this.http.post<any>(this.api, req); }
  assign(data: any): Observable<any> { return this.http.post<any>(`${this.api}/assign`, data); }
  getAll(page = 1, pageSize = 200): Observable<any> {
    return this.http.get<any>(this.api, { params: buildParams({ pageNumber: page, pageSize }) });
  }
  getById(id: number): Observable<any> { return this.http.get<any>(`${this.api}/${id}`); }
  update(id: number, req: Partial<ProjectCreateRequest>): Observable<any> {
    return this.http.put<any>(`${this.api}/${id}`, req);
  }
  delete(id: number): Observable<any> { return this.http.delete<any>(`${this.api}/${id}`); }
  assignEmployee(projectId: number, userId: number): Observable<any> {
    return this.http.post<any>(`${this.api}/assign`, { projectId, userId });
  }
  getUserAssignments(userId: number, pageNumber: number, pageSize: number): Observable<any> {
    return this.http.get<any>(`${this.api}/user/${userId}/assignments`, { params: buildParams({ pageNumber, pageSize }) });
  }
  getAssignmentsByProject(projectId: number): Observable<any> {
    return this.http.get<any>(`${this.api}/${projectId}/assignments`);
  }
  removeAssignment(assignmentId: number): Observable<any> {
    return this.http.delete<any>(`${this.api}/assignment/${assignmentId}`);
  }
}

// ── Payroll ────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class PayrollService {
  private readonly api = `${environment.apiUrl}/payroll`;
  constructor(private http: HttpClient) {}

  generate(req: PayrollCreateRequest): Observable<any> { return this.http.post<any>(this.api, req); }
  getByUser(userId: number, page = 1, pageSize = 200): Observable<any> {
    return this.http.get<any>(`${this.api}/user/${userId}`, { params: buildParams({ pageNumber: page, pageSize }) });
  }
  getAll(page = 1, pageSize = 200): Observable<any> {
    return this.http.get<any>(this.api, { params: buildParams({ pageNumber: page, pageSize }) });
  }
}

// ── User ───────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly api = `${environment.apiUrl}/user`;
  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 1000, search?: string, role?: string, status?: string, sortBy = 'name', sortDir = 'asc'): Observable<any> {
    return this.http.get<any>(this.api, {
      params: buildParams({ pageNumber: page, pageSize, search, role, status, sortBy, sortDir })
    });
  }
  getById(id: number): Observable<any>               { return this.http.get<any>(`${this.api}/${id}`); }
  getProfile(): Observable<any>                      { return this.http.get<any>(`${this.api}/profile`); }
  update(id: number, req: UserUpdateRequest): Observable<any> { return this.http.put<any>(`${this.api}/${id}`, req); }
  delete(id: number): Observable<any>                { return this.http.delete<any>(`${this.api}/${id}`); }
  setActive(id: number, isActive: boolean): Observable<any> {
    return this.http.put<any>(`${this.api}/${id}`, { isActive });
  }
}

// ── Intern ─────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class InternService {
  private readonly api = `${environment.apiUrl}/interntask`;
  constructor(private http: HttpClient) {}

  getTasks(internId: number): Observable<any>            { return this.http.get<any>(`${this.api}/intern/${internId}`); }
  createTask(req: InternTaskCreateRequest): Observable<any> { return this.http.post<any>(`${this.api}/create`, req); }
  updateTask(taskId: number, req: any)                   { return this.http.put(`${this.api}/update/${taskId}`, { request: req }); }
  deleteTask(taskId: number): Observable<any>            { return this.http.delete<any>(`${this.api}/delete/${taskId}`); }
}

// ── AuditLog ───────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly api = `${environment.apiUrl}/AuditLog`;
  constructor(private http: HttpClient) {}

  getAll(): Observable<any> { return this.http.get<any>(this.api); }
  getByTable(tableName: string): Observable<any> { return this.http.get<any>(`${this.api}/table/${tableName}`); }
  getByAction(action: string): Observable<any>   { return this.http.get<any>(`${this.api}/action/${action}`); }
  getByUser(userId: number): Observable<any>     { return this.http.get<any>(`${this.api}/user/${userId}`); }

  getPaged(page = 1, pageSize = 10, search?: string, action?: string, table?: string, sortDir = 'desc'): Observable<any> {
    return this.http.get<any>(`${this.api}/paged`, {
      params: buildParams({ page, pageSize, search, action, table, sortDir })
    });
  }
}

// ── Analytics ──────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly api = `${environment.apiUrl}/analytics`;
  constructor(private http: HttpClient) {}

  getDashboard(userId?: number): Observable<DashboardSummary> {
    let params = new HttpParams();
    if (userId !== undefined) params = params.set('userId', userId.toString());
    return this.http.get<DashboardSummary>(`${this.api}/dashboard`, { params });
  }
}
