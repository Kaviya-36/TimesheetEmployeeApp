export type UserRole = 'Admin' | 'HR' | 'Manager' | 'Employee' | 'Mentor' | 'Intern';
export type TimesheetStatus = 'Pending' | 'Approved' | 'Rejected';
export type LeaveStatus     = 'Pending' | 'Approved' | 'Rejected';
export type ProjectStatus   = 'Active'  | 'Completed' | 'OnHold';

// Auth
export interface LoginRequest  { username: string; password: string; }
export interface AuthResponse  { userId: number; username: string; token: string; role: UserRole; }
export interface RegisterRequest {
  employeeId: string; name: string; email: string; password: string;
  phone: string; departmentId?: number; role: string;
}

// User
export interface User {
  id: number; employeeId: string; name: string; email: string; phone: string;
  role: UserRole; status: 'Active' | 'Inactive'; joiningDate: string;
}
export interface UserProfile {
  id: number; employeeId: string; name: string; email: string; phone: string;
  role: UserRole; status: string; joiningDate: string; departmentName?: string;
}
export interface UserUpdateRequest {
  name?: string; email?: string; phone?: string;
  departmentId?: number; role?: string; isActive?: boolean;
}

// Timesheet
export interface Timesheet {
  id: number; employeeName: string; employeeId: string;
  projectId?: number; projectName: string; date: string;
  startTime: string; endTime: string; breakTime: string;
  hoursWorked: number; description?: string;
  status: number; managerComment?: string;
  statusText?: string; statusClass?: string;
}
export interface TimesheetCreateRequest {
  projectId: number; projectName: string; workDate: string;
  startTime: string; endTime: string; breakTime: string; taskDescription?: string;
}
export interface TimesheetUpdateRequest {
  projectId?: number; projectName?: string; workDate?: string;
  startTime?: string; endTime?: string; breakTime?: string; taskDescription?: string;
}
export interface TimesheetApprovalRequest {
  timesheetId: number; approvedById: number; isApproved: boolean; managerComment?: string;
}

// Attendance
export interface Attendance {
  id: number; userId: number; employeeName: string; date: string;
  checkIn?: string; checkOut?: string; isLate: boolean; totalHours?: string;
  missedCheckout?: boolean;
}

// Leave
export interface LeaveCreateRequest { leaveTypeId: number; fromDate: string; toDate: string; reason?: string; }
export interface LeaveApprovalRequest {
  leaveRequestId: number; approverById: number; isApproved: boolean; managerComment?: string;
}
export interface Leave {
  id: number; userId?: number; employeeName: string; leaveType: string; fromDate: string; toDate: string;
  reason?: number; status: Number; approvedById?: number; approvedDate?: string; managerComment?: string;
}
export interface LeaveType { id: number; name: string; maxDaysPerYear: number; isActive: boolean; }

// Project
export interface ProjectCreateRequest {
  projectName: string; description?: string; managerId?: number; startDate: string; endDate?: string;
}
export interface Project {
  id: number; projectName: string; description?: string;
  managerId?: number; managerName?: string; startDate?: string; endDate?: string;
}
export interface ProjectAssignment { id: number; projectId: number; projectName: string; }

// Payroll
export interface PayrollCreateRequest {
  userId: number; basicSalary: number; overtimeAmount: number; deductions: number; salaryMonth: string;
}
export interface Payroll {
  payrollId: number; employeeName: string; employeeId: string;
  basicSalary: number; overtimeAmount: number; deductions: number;
  netSalary: number; salaryMonth: string; generatedDate: string;
}

// Intern
export interface InternTask { id: number; taskTitle?: string; title?: string; description?: string; status: string; dueDate?: string; }
export interface InternTaskCreateRequest { internId: number; taskTitle: string; description?: string; dueDate?: string; }

// Analytics
export interface DashboardSummary {
  timesheetsPending: number; timesheetsApproved: number;
  leavesPending: number; checkedInToday: number; lateToday: number;
}

// Notification
export interface Notification { type: string; message: string; time: string; read: boolean; }

// AuditLog
export interface AuditLog {
  id: number; tableName: string; action: string;
  keyValues: string; oldValues?: string; newValues?: string;
  userId?: number; changedAt: string;
}

// Generic
export interface ApiResponse<T> { success: boolean; message: string; data: T; }
