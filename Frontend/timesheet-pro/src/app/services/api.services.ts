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

// ── Timesheet ──────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class TimesheetService {
  private readonly api = `${environment.apiUrl}/timesheet`;
  constructor(private http: HttpClient) {}

  create(userId: number, req: TimesheetCreateRequest): Observable<any> {
    return this.http.post<any>(`${this.api}/${userId}/manual`, req);
  }
  update(id: number, req: TimesheetUpdateRequest): Observable<any> {
    return this.http.put<any>(`${this.api}/${id}`, req);
  }
  delete(id: number): Observable<any> {
    return this.http.delete<any>(`${this.api}/${id}`);
  }
  getByUser(userId: number): Observable<any> {
    return this.http.get<any>(`${this.api}/user/${userId}`);
  }
  getAll(): Observable<any> {
    return this.http.get<any>(this.api);
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
  getMyAttendance(userId: number): Observable<any> { return this.http.get<any>(`${this.api}/me`); }
  getAll(page = 1, size = 10): Observable<any> {
    return this.http.get<any>(`${this.api}/all?pageNumber=${page}&pageSize=${size}`);
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
  getMyLeaves(userId: number): Observable<any> {
    return this.http.get<any>(`${this.api}/user/${userId}`);
  }
  getPending(): Observable<any>  { return this.http.get<any>(`${this.api}/pending`); }
  getAll(): Observable<any>      { return this.http.get<any>(`${this.api}/getall`); }
  approveOrReject(data: { leaveId: number; approvedById: number; isApproved: boolean; managerComment: string; }): Observable<any> {
    return this.http.put<any>(`${this.api}/approve`, data);
  }
  getLeaveTypes(): Observable<any> { return this.http.get<any>(`${this.api}/types`); }
}

// ── Project ────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly api = `${environment.apiUrl}/project`;
  constructor(private http: HttpClient) {}

  create(req: ProjectCreateRequest): Observable<any> { return this.http.post<any>(this.api, req); }
  assign(data: any): Observable<any> { return this.http.post<any>(`${this.api}/assign`, data); }
  getAll(): Observable<any> { return this.http.get<any>(this.api); }
  getById(id: number): Observable<any> { return this.http.get<any>(`${this.api}/${id}`); }
  update(id: number, req: Partial<ProjectCreateRequest>): Observable<any> {
    return this.http.put<any>(`${this.api}/${id}`, req);
  }
  delete(id: number): Observable<any> { return this.http.delete<any>(`${this.api}/${id}`); }
  assignEmployee(projectId: number, userId: number): Observable<any> {
    return this.http.post<any>(`${this.api}/assign`, { projectId, userId });
  }
  getUserAssignments(userId: number, pageNumber: number, pageSize: number): Observable<any> {
    return this.http.get<any>(`${this.api}/user/${userId}/assignments?pageNumber=${pageNumber}&pageSize=${pageSize}`);
  }
  getAssignmentsByProject(projectId: number): Observable<any> {
  return this.http.get<any>(`${this.api}/${projectId}/assignments`);
}
}

// ── Payroll ────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class PayrollService {
  private readonly api = `${environment.apiUrl}/payroll`;
  constructor(private http: HttpClient) {}

  generate(req: PayrollCreateRequest): Observable<any> { return this.http.post<any>(this.api, req); }
  getByUser(userId: number): Observable<any>           { return this.http.get<any>(`${this.api}/user/${userId}`); }
  getAll(): Observable<any>                            { return this.http.get<any>(this.api); }
}

// ── User ───────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly api = `${environment.apiUrl}/user`;
  constructor(private http: HttpClient) {}

  getAll(): Observable<any>                           { return this.http.get<any>(`${this.api}?pageNumber=1&pageSize=1000`); }
  getById(id: number): Observable<any>               { return this.http.get<any>(`${this.api}/${id}`); }
  getProfile(): Observable<any>                      { return this.http.get<any>(`${this.api}/profile`); }
  update(id: number, req: UserUpdateRequest): Observable<any> { return this.http.put<any>(`${this.api}/${id}`, req); }
  delete(id: number): Observable<any>                { return this.http.delete<any>(`${this.api}/${id}`); }
  activate(id: number): Observable<any>              { return this.http.patch<any>(`${this.api}/${id}/activate`, {}); }
  setActive(id: number, isActive: boolean): Observable<any> {
    return this.http.put<any>(`${this.api}/${id}`, { isActive });
  }
}

// ── Intern ─────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class InternService {
  private readonly api = `${environment.apiUrl}/interntask`;
  constructor(private http: HttpClient) {}

  getInterns(): Observable<any> {
    return this.http.get<any>(this.api);
  }

  // Get intern by ID
  getInternById(id: number): Observable<any> {
    return this.http.get<any>(`${this.api}/${id}`);
  }

  // Create intern
  createIntern(data: any): Observable<any> {
    return this.http.post<any>(this.api, data);
  }

  // Update intern
  updateIntern(id: number, data: any): Observable<any> {
    return this.http.put<any>(`${this.api}/${id}`, data);
  }

  // Delete intern
  deleteIntern(id: number): Observable<any> {
    return this.http.delete<any>(`${this.api}/${id}`);
  }


  // ===========================
  // 📋 INTERN TASK APIs
  // ===========================

  // Get tasks by intern
  getTasks(internId: number): Observable<any> {
    return this.http.get<any>(`${this.api}/intern/${internId}`);
  }

  // Create task
  createTask(req: InternTaskCreateRequest): Observable<any> {
    return this.http.post<any>(`${this.api}/create`, req);
  }

  // Update task
  updateTask(taskId: number, req: any) {
  return this.http.put(`${this.api}/update/${taskId}`, {
    request: req   // 🔥 REQUIRED
  });
}

  // Delete task
  deleteTask(taskId: number): Observable<any> {
    return this.http.delete<any>(`${this.api}/delete/${taskId}`);
  }
}

// ── AuditLog ───────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly api = `${environment.apiUrl}/AuditLog`;
  constructor(private http: HttpClient) {}

  getAll(): Observable<any>                          { return this.http.get<any>(this.api); }
  getByTable(tableName: string): Observable<any>     { return this.http.get<any>(`${this.api}/table/${tableName}`); }
  getByAction(action: string): Observable<any>       { return this.http.get<any>(`${this.api}/action/${action}`); }
  getByUser(userId: number): Observable<any>         { return this.http.get<any>(`${this.api}/user/${userId}`); }
  getPaged(page = 1, pageSize = 10): Observable<any> { return this.http.get<any>(`${this.api}/paged?page=${page}&pageSize=${pageSize}`); }
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
