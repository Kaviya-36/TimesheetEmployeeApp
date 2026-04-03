import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Attendance, AuditLog, DashboardSummary, Leave, LeaveType, Project, Timesheet, User, UserProfile } from '../../models';
import {
  AnalyticsService, AttendanceService, AuditLogService,
  LeaveService,
  ProjectService, TimesheetService,
  UserService
} from '../../services/api.services';
import { AuthService } from '../../services/auth.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
import { NotificationService } from '../../services/notification.service';
import { TabService } from '../../services/tab.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmComponent } from '../confirm-dialog/confirm.component';
import { NavbarComponent } from '../navbar/navbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

export type AdminTab = 'overview' | 'users' | 'projects' | 'timesheets' | 'leaves' | 'attendance' | 'auditlogs' | 'settings' | 'profile';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, FormsModule, NavbarComponent, SidebarComponent, ConfirmComponent],
  templateUrl: './admin-dashboard.component.html',
  styleUrl:    './admin-dashboard.component.css'
})
export class AdminDashboardComponent implements OnInit, OnDestroy {

  readonly auth             = inject(AuthService);
  private  toast            = inject(ToastService);
  private  breadcrumbService = inject(BreadcrumbService);
  private  notificationService = inject(NotificationService);
  private  userService      = inject(UserService);
  private  projectService   = inject(ProjectService);
  private  timesheetService = inject(TimesheetService);
  private  analyticsService = inject(AnalyticsService);
  private  leaveService     = inject(LeaveService);
  private  attendanceService = inject(AttendanceService);
  private  auditLogService  = inject(AuditLogService);
  private  formBuilder      = inject(FormBuilder);
  private  tabService       = inject(TabService);

  constructor() {
    effect(() => {
      const t = this.tabService.activeTab();
      if (t && t !== this.activeTab()) this.setTab(t as AdminTab);
    });
  }


  activeTab = signal<AdminTab>('overview');
  readonly tabs: { key: AdminTab; label: string; icon: string }[] = [
    { key: 'overview',   label: 'Overview',   icon: '📊' },
    { key: 'users',      label: 'Users',      icon: '👥' },
    { key: 'projects',   label: 'Projects',   icon: '🗂' },
    { key: 'timesheets', label: 'Timesheets', icon: '📋' },
    { key: 'leaves',     label: 'Leaves',     icon: '🌴' },
    { key: 'attendance', label: 'Attendance', icon: '⏰' },
    { key: 'auditlogs',  label: 'Audit Logs', icon: '🔍' },
    { key: 'settings',   label: 'Settings',   icon: '⚙' },
    { key: 'profile',    label: 'Profile',    icon: '👤' },
  ];

  // ── Data signals ──────────────────────────────────────────────────────────
  allUsers       = signal<User[]>([]);
  allProjects    = signal<Project[]>([]);
  allTimesheets  = signal<Timesheet[]>([]);
  allLeaves      = signal<Leave[]>([]);
  allAttendances = signal<Attendance[]>([]);
  summary        = signal<DashboardSummary | null>(null);
  userProfile    = signal<UserProfile | null>(null);

  // ── Audit Logs ────────────────────────────────────────────────────────────
  allAuditLogs = signal<AuditLog[]>([]);
  auditSearch  = signal('');
  auditAction  = signal('all');
  auditTable   = signal('all');
  auditPage    = signal(1);
  auditPageSize: number = 10;

  filteredAudit = computed(() => {
    let d = this.allAuditLogs();
    const q = this.auditSearch().toLowerCase();
    if (q) {
      d = d.filter(a =>
        a.tableName.toLowerCase().includes(q) ||
        a.action.toLowerCase().includes(q) ||
        (a.keyValues ?? '').toLowerCase().includes(q)
      );
    }
    if (this.auditAction() !== 'all') d = d.filter(a => a.action === this.auditAction());
    if (this.auditTable()  !== 'all') d = d.filter(a => a.tableName.toLowerCase() === this.auditTable().toLowerCase());
    return d;
  });

  auditTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredAudit().length / this.auditPageSize)));
  pagedAudit      = computed(() => {
    const s = (this.auditPage() - 1) * this.auditPageSize;
    return this.filteredAudit().slice(s, s + this.auditPageSize);
  });
  auditTables = computed(() => [...new Set(this.allAuditLogs().map(a => a.tableName))].sort());

  // ── Admin own check-in/out ────────────────────────────────────────────────
  todayAtt   = signal<Attendance | null>(null);
  attLoading = signal(false);
  liveTimer  = signal('00:00:00');
  private timerInterval: any;
  readonly todayDate = new Date().toISOString().split('T')[0];

  // ── Attendance list filters ───────────────────────────────────────────────
  attSearch = signal('');
  attPage   = signal(1);
  attendancePageSize: number = 8;

  filteredAtt = computed(() => {
    const q = this.attSearch().toLowerCase();
    let d = this.allAttendances();
    if (q) d = d.filter(a => (a.employeeName ?? '').toLowerCase().includes(q));
    d = d.filter(a => this._inPeriod(a.date, this.attViewMode(), this.attPeriodOffset()));
    return d;
  });

  attTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredAtt().length / this.attendancePageSize)));

  pagedAtt = computed(() => {
    const s = (this.attPage() - 1) * this.attendancePageSize;
    return this.filteredAtt().slice(s, s + this.attendancePageSize);
  });

  // Today's attendance stats
  todayAttStats = computed(() => {
    const today = this.toDateStr(new Date());
    const recs = this.allAttendances().filter(a => a.date?.startsWith(today));
    return {
      checkedIn:  recs.filter(a => a.checkIn).length,
      checkedOut: recs.filter(a => a.checkOut).length,
      active:     recs.filter(a => a.checkIn && !a.checkOut).length,
      late:       recs.filter(a => a.isLate).length,
      notStarted: Math.max(0, this.allUsers().length - recs.filter(a => a.checkIn).length),
      records:    recs
    };
  });

  // ── User filters & pagination ─────────────────────────────────────────────
  userSearch      = signal('');
  userRoleFilter  = signal('all');
  userStatusFilter = signal('all');
  userPage        = signal(1);
  userSortColumn  = signal<'name'|'role'|'status'|'joined'>('name');
  userSortDirection = signal<'asc'|'desc'>('asc');
  usersPageSize: number = 8;

  // ── Timesheet filters & pagination ────────────────────────────────────────
  tsSearch   = signal('');
  tsStatus   = signal('all');
  tsPage     = signal(1);
  tsSortCol  = signal<'date'|'hours'|'employee'>('date');
  tsSortDir  = signal<'asc'|'desc'>('desc');
  timesheetsPageSize: number = 8;

  // ── View mode ─────────────────────────────────────────────────────────────
  tsViewMode      = signal<'all'|'weekly'|'monthly'>('all');
  tsPeriodOffset  = signal(0);
  attViewMode     = signal<'all'|'weekly'|'monthly'>('all');
  attPeriodOffset = signal(0);

  tsPeriodLabel  = () => this._periodLabel(this.tsViewMode(),  this.tsPeriodOffset());
  attPeriodLabel = () => this._periodLabel(this.attViewMode(), this.attPeriodOffset());

  // ── Weekly grid helpers ───────────────────────────────────────────────────
  weekDays = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];

  // ── Leave filters & pagination ────────────────────────────────────────────
  leaveSearch      = signal('');
  leaveStatusFilter = signal('all');
  leavePage        = signal(1);
  leavesPageSize: number = 8;

  // ── Project pagination ────────────────────────────────────────────────────
  projectPage     = signal(1);
  projectsPageSize: number = 8;

  // ── Modal / edit state ────────────────────────────────────────────────────
  showMissedCheckoutModal = signal(false);
  showAddUser             = signal(false);
  showAddProject          = signal(false);
  editUser                = signal<User | null>(null);
  newRole                 = '';
  editProject             = signal<Project | null>(null);
  showAddTimesheetModal   = signal(false);
  showAddLeaveModal       = signal(false);
  leaveTypes              = signal<LeaveType[]>([]);

  // ── Confirm dialog signals ────────────────────────────────────────────────
  confirmVisible = signal(false);
  confirmTitle   = signal('');
  confirmMessage = signal('');
  confirmType    = signal<'danger'|'warning'|'info'>('danger');
  private confirmAction: (() => void) | null = null;

  settings = { startTime: '09:00', endTime: '18:00', smtp: '', email: '' };
  readonly roleOptions = ['Admin','HR','Manager','Employee','Intern','Mentor'];
  readonly departments = [{id:1,name:'IT'},{id:2,name:'HR'},{id:3,name:'Finance'},{id:4,name:'Marketing'}];

  addUserForm = this.formBuilder.group({
    employeeId:   ['', Validators.required],
    name:         ['', Validators.required],
    email:        ['', [Validators.required, Validators.email]],
    password:     ['', [Validators.required, Validators.minLength(6)]],
    phone:        [''],
    role:         ['Employee', Validators.required],
    departmentId: ['', Validators.required],
  });

  projectForm = this.formBuilder.group({
    projectName: ['', Validators.required],
    description: [''],
    managerId:   [''],
    startDate:   ['', Validators.required],
    endDate:     [''],
  });

  editProjectForm = this.formBuilder.group({
    projectName: ['', Validators.required],
    description: [''],
    managerId:   [''],
    startDate:   ['', Validators.required],
    endDate:     [''],
  });

  addTimesheetForm = this.formBuilder.group({
    projectId:       ['', Validators.required],
    workDate:        [new Date().toISOString().split('T')[0], Validators.required],
    startTime:       ['09:00', Validators.required],
    endTime:         ['18:00', Validators.required],
    breakTime:       ['01:00'],
    taskDescription: [''],
  });

  addLeaveForm = this.formBuilder.group({
    leaveTypeId: ['', Validators.required],
    fromDate:    ['', Validators.required],
    toDate:      ['', Validators.required],
    reason:      [''],
  });

  // ── Computed: filtered & paged users ─────────────────────────────────────
  filteredUsers = computed(() => {
    let d = this.allUsers();
    const q = this.userSearch().toLowerCase();
    if (q) {
      d = d.filter(u =>
        u.name.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q) ||
        u.employeeId.toLowerCase().includes(q)
      );
    }
    if (this.userRoleFilter()   !== 'all') d = d.filter(u => u.role === this.userRoleFilter());
    if (this.userStatusFilter() !== 'all') {
      d = d.filter(u => this.userStatusFilter() === 'active' ? u.status === 'Active' : u.status !== 'Active');
    }
    const col = this.userSortColumn();
    const dir = this.userSortDirection();
    d = [...d].sort((a, b) => {
      let v = col === 'name'   ? a.name.localeCompare(b.name)
            : col === 'role'   ? a.role.localeCompare(b.role)
            : col === 'status' ? a.status.localeCompare(b.status)
            : new Date(a.joiningDate).getTime() - new Date(b.joiningDate).getTime();
      return dir === 'asc' ? v : -v;
    });
    return d;
  });

  userTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredUsers().length / this.usersPageSize)));
  pagedUsers     = computed(() => {
    const s = (this.userPage() - 1) * this.usersPageSize;
    return this.filteredUsers().slice(s, s + this.usersPageSize);
  });

  // ── Computed: filtered & paged timesheets ─────────────────────────────────
  filteredTs = computed(() => {
    let d = this.allTimesheets();
    const q = this.tsSearch().toLowerCase();
    if (q) {
      d = d.filter(t =>
        (t.employeeName ?? '').toLowerCase().includes(q) ||
        (t.projectName  ?? '').toLowerCase().includes(q)
      );
    }
    if (this.tsStatus() !== 'all') {
      const sv: {[k: string]: number} = { pending: 0, approved: 1, rejected: 2 };
      d = d.filter(t => Number(t.status) === sv[this.tsStatus()]);
    }
    d = d.filter(t => this._inPeriod(t.date, this.tsViewMode(), this.tsPeriodOffset()));
    const col = this.tsSortCol();
    const dir = this.tsSortDir();
    d = [...d].sort((a, b) => {
      const v = col === 'date'  ? new Date(a.date).getTime() - new Date(b.date).getTime()
              : col === 'hours' ? (a.hoursWorked ?? 0) - (b.hoursWorked ?? 0)
              : (a.employeeName ?? '').localeCompare(b.employeeName ?? '');
      return dir === 'asc' ? v : -v;
    });
    return d;
  });

  tsTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredTs().length / this.timesheetsPageSize)));
  pagedTs      = computed(() => {
    const s = (this.tsPage() - 1) * this.timesheetsPageSize;
    return this.filteredTs().slice(s, s + this.timesheetsPageSize);
  });

  // ── Computed: filtered & paged leaves ────────────────────────────────────
  filteredLeaves = computed(() => {
    let d = this.allLeaves();
    const q = this.leaveSearch().toLowerCase();
    if (q) d = d.filter(l => (l.employeeName ?? '').toLowerCase().includes(q));
    if (this.leaveStatusFilter() !== 'all') {
      const sv: {[k: string]: number} = { pending: 0, approved: 1, rejected: 2 };
      d = d.filter(l => Number(l.status) === sv[this.leaveStatusFilter()]);
    }
    return d;
  });

  leaveTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredLeaves().length / this.leavesPageSize)));
  pagedLeaves     = computed(() => {
    const s = (this.leavePage() - 1) * this.leavesPageSize;
    return this.filteredLeaves().slice(s, s + this.leavesPageSize);
  });

  // ── Computed: paged projects ──────────────────────────────────────────────
  pagedProjects = computed(() => {
    const s = (this.projectPage() - 1) * this.projectsPageSize;
    return this.allProjects().slice(s, s + this.projectsPageSize);
  });

  projectTotalPages = computed(() => Math.max(1, Math.ceil(this.allProjects().length / this.projectsPageSize)));

  // ── Computed: summary counts ──────────────────────────────────────────────
  totalUsersCount    = computed(() => this.allUsers().length);
  activeUsersCount   = computed(() => this.allUsers().filter(u => u.status === 'Active').length);
  inactiveUsersCount = computed(() => this.allUsers().filter(u => u.status !== 'Active').length);
  managers           = computed(() => this.allUsers().filter(u => u.role === 'Manager'));
  pendingTimesheetsCount = computed(() => this.allTimesheets().filter(t => Number(t.status) === 0).length);
  pendingLeavesCount     = computed(() => this.allLeaves().filter(l => Number(l.status) === 0).length);
  roleBreakdown = computed(() =>
    this.roleOptions.map(r => ({ role: r, count: this.allUsers().filter(u => u.role === r).length }))
  );


  ngOnInit() {
    this.breadcrumbService.set([{ label: 'Admin Dashboard' }, { label: 'Overview' }]);
    this.tabService.setTab('overview');
    this.loadAll();
    this.refreshToday();
    this.userService.getProfile().subscribe({ next: (r: any) => this.userProfile.set(r?.data ?? r), error: () => {} });
    const saved = localStorage.getItem('admin_settings');
    if (saved) try { this.settings = JSON.parse(saved); } catch {}
  }

  ngOnDestroy() {
    if (this.timerInterval) clearInterval(this.timerInterval);
  }

  /**
   * Extracts an array from various API response shapes.
   */
  private extractArray<T>(r: any): T[] {
    if (Array.isArray(r)) return r;
    if (Array.isArray(r?.data)) return r.data;
    if (Array.isArray(r?.data?.data)) return r.data.data;
    return [];
  }

  /** Loads all data from the API. */
  loadAll() {
    this.userService.getAll().subscribe({
      next: r => this.allUsers.set(this.extractArray<User>(r)),
      error: () => {}
    });

    this.projectService.getAll().subscribe({
      next: r => this.allProjects.set(this.extractArray<Project>(r)),
      error: () => {}
    });

    this.timesheetService.getAll().subscribe({
      next: (r: any) => {
        const raw = r?.data?.data ?? r?.data ?? r ?? [];
        this.allTimesheets.set((Array.isArray(raw) ? raw : []).map((t: any) => ({
          ...t,
          statusText:  this.getStatusText(t.status),
          statusClass: this.getStatusClass(t.status)
        })));
      },
      error: () => {}
    });

    this.leaveService.getAll().subscribe({
      next: r => this.allLeaves.set(this.extractArray<Leave>(r)),
      error: () => {}
    });

    this.leaveService.getLeaveTypes().subscribe({
      next: r => this.leaveTypes.set(this.extractArray<LeaveType>(r)),
      error: () => {}
    });

    this.analyticsService.getDashboard().subscribe({
      next: r => this.summary.set(r),
      error: () => {}
    });

    this.attendanceService.getAll(1, 200).subscribe({
      next: (r: any) => this.allAttendances.set(this.extractArray<Attendance>(r)),
      error: () => {}
    });

    this.auditLogService.getPaged(1, 200).subscribe({
      next: (r: any) => this.allAuditLogs.set(this.extractArray<AuditLog>(r)),
      error: () => {}
    });
  }

  /** Sorts the users table by the given column. */
  sortUsers(col: 'name'|'role'|'status'|'joined') {
    if (this.userSortColumn() === col) {
      this.userSortDirection.update(d => d === 'asc' ? 'desc' : 'asc');
    } else {
      this.userSortColumn.set(col);
      this.userSortDirection.set('asc');
    }
    this.userPage.set(1);
  }

  /** Sorts the timesheets table by the given column. */
  sortTimesheets(col: 'date'|'hours'|'employee') {
    if (this.tsSortCol() === col) {
      this.tsSortDir.update(d => d === 'asc' ? 'desc' : 'asc');
    } else {
      this.tsSortCol.set(col);
      this.tsSortDir.set('asc');
    }
    this.tsPage.set(1);
  }

  /** Returns the sort icon for a column header. */
  getSortIcon(active: boolean, dir: string): string {
    return !active ? '⇅' : dir === 'asc' ? '↑' : '↓';
  }

  private confirm(title: string, msg: string, action: () => void, type: 'danger'|'warning'|'info' = 'danger') {
    this.confirmTitle.set(title);
    this.confirmMessage.set(msg);
    this.confirmType.set(type);
    this.confirmAction = action;
    this.confirmVisible.set(true);
  }

  /** Executes the pending confirm action and closes the dialog. */
  onConfirmOk() {
    this.confirmAction?.();
    this.confirmVisible.set(false);
    this.confirmAction = null;
  }

  /** Cancels the confirm dialog without executing the action. */
  onConfirmCancel() {
    this.confirmVisible.set(false);
    this.confirmAction = null;
  }

  /** Submits the add-user form to create a new user account. */
  addUser() {
    if (this.addUserForm.invalid) return;
    const v = this.addUserForm.getRawValue();
    this.auth.register({
      employeeId:   v.employeeId!,
      name:         v.name!,
      email:        v.email!,
      password:     v.password!,
      phone:        v.phone ?? '',
      role:         v.role!,
      departmentId: Number(v.departmentId!)
    }).subscribe({
      next: () => {
        this.toast.success('User Created', 'Account pending admin approval.');
        this.showAddUser.set(false);
        this.addUserForm.reset({ role: 'Employee' });
        this.loadAll();
      },
      error: (err: any) => this.toast.error('Failed', err?.error?.message ?? 'Could not create user.')
    });
  }

  /** Opens a confirm dialog to permanently delete a user. */
  confirmDeleteUser(u: User) {
    this.confirm('Delete User', `Permanently delete "${u.name}"? Cannot be undone.`, () => {
      this.userService.delete(u.id).subscribe({
        next: () => { this.toast.success('Deleted', `"${u.name}" removed.`); this.loadAll(); },
        error: (err: any) => this.toast.error('Error', err?.error?.message ?? 'Delete failed.')
      });
    });
  }

  /** Toggles a user's active/inactive status with a confirmation prompt. */
  toggleActive(u: User) {
    const activate = u.status !== 'Active';
    this.confirm(
      activate ? 'Activate User' : 'Deactivate User',
      `${activate ? 'Activate' : 'Deactivate'} "${u.name}"?`,
      () => {
        this.userService.setActive(u.id, activate).subscribe({
          next: () => {
            this.toast.success(
              activate ? 'Activated' : 'Deactivated',
              `"${u.name}" ${activate ? 'can now log in' : 'has been locked out'}.`
            );
            this.loadAll();
          },
          error: (err: any) => this.toast.error('Error', err?.error?.message ?? 'Failed.')
        });
      },
      activate ? 'info' : 'warning'
    );
  }

  /** Opens the edit-user panel for the given user. */
  openEditUser(u: User) {
    this.editUser.set(u);
    this.newRole = u.role;
  }

  /** Saves the updated role for the currently edited user. */
  saveRole() {
    const u = this.editUser();
    if (!u) return;
    this.userService.update(u.id, { role: this.newRole }).subscribe({
      next: () => {
        this.toast.success('Role Updated', `${u.name} is now ${this.newRole}.`);
        this.editUser.set(null);
        this.loadAll();
      },
      error: (err: any) => this.toast.error('Error', err?.error?.message ?? 'Failed.')
    });
  }

  /** Submits the project form to create a new project. */
  createProject() {
    if (this.projectForm.invalid) return;
    const v = this.projectForm.getRawValue();
    this.projectService.create({
      projectName: v.projectName!,
      description: v.description ?? '',
      managerId:   v.managerId ? +v.managerId : undefined,
      startDate:   v.startDate!,
      endDate:     v.endDate ?? undefined
    }).subscribe({
      next: () => {
        this.toast.success('Project Created');
        this.showAddProject.set(false);
        this.projectForm.reset();
        this.loadAll();
      },
      error: (err: any) => this.toast.error('Error', err?.error?.message ?? 'Failed.')
    });
  }

  /** Opens a confirm dialog to delete a project. */
  confirmDeleteProject(p: Project) {
    this.confirm('Delete Project', `Delete "${p.projectName}"?`, () => {
      this.projectService.delete(p.id).subscribe({
        next: () => { this.toast.success('Deleted'); this.loadAll(); },
        error: (err: any) => this.toast.error('Error', err?.error?.message ?? 'Failed.')
      });
    });
  }

  /** Opens the edit-project panel and pre-fills the form. */
  openEditProject(p: Project) {
    this.editProject.set(p);
    this.editProjectForm.patchValue({
      projectName: p.projectName,
      description: p.description ?? '',
      managerId:   p.managerId ? String(p.managerId) : '',
      startDate:   p.startDate ? (p.startDate as string).split('T')[0] : '',
      endDate:     p.endDate   ? (p.endDate   as string).split('T')[0] : '',
    });
  }

  /** Saves changes to the currently edited project. */
  saveEditProject() {
    const p = this.editProject();
    if (!p || this.editProjectForm.invalid) return;
    const v = this.editProjectForm.getRawValue();
    this.projectService.update(p.id, {
      projectName: v.projectName!,
      description: v.description ?? '',
      managerId:   v.managerId ? +v.managerId : undefined,
      startDate:   v.startDate!,
      endDate:     v.endDate ?? undefined
    }).subscribe({
      next: () => { this.toast.success('Project Updated'); this.editProject.set(null); this.loadAll(); },
      error: (e: any) => this.toast.error('Error', e?.error?.message ?? 'Failed.')
    });
  }

  /** Submits a new timesheet entry on behalf of the current admin user. */
  addTimesheetForUser() {
    if (this.addTimesheetForm.invalid) return;
    const uid = this.auth.currentUser();
    if (!uid) return;
    const v = this.addTimesheetForm.getRawValue();
    const proj = this.allProjects().find(p => p.id === +v.projectId!);
    const fmt = (t: string) => t?.length === 5 ? t + ':00' : t ?? '00:00:00';
    this.timesheetService.create(uid, {
      projectId:       +v.projectId!,
      projectName:     proj?.projectName ?? '',
      workDate:        v.workDate!,
      startTime:       fmt(v.startTime!),
      endTime:         fmt(v.endTime!),
      breakTime:       fmt(v.breakTime || '00:00'),
      taskDescription: v.taskDescription ?? ''
    }).subscribe({
      next: () => {
        this.toast.success('Timesheet Added', 'Your timesheet has been submitted.');
        this.showAddTimesheetModal.set(false);
        this.addTimesheetForm.reset({
          workDate:  new Date().toISOString().split('T')[0],
          startTime: '09:00',
          endTime:   '18:00',
          breakTime: '01:00'
        });
        this.loadAll();
      },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Could not create timesheet.')
    });
  }

  /** Submits a leave application on behalf of the current admin user. */
  addLeaveForUser() {
    if (this.addLeaveForm.invalid) return;
    const uid = this.auth.currentUser();
    if (!uid) return;
    const v = this.addLeaveForm.getRawValue();
    this.leaveService.apply(uid, {
      leaveTypeId: +v.leaveTypeId!,
      fromDate:    v.fromDate!,
      toDate:      v.toDate!,
      reason:      v.reason ?? ''
    }).subscribe({
      next: () => {
        this.toast.success('Leave Applied', 'Your leave request has been submitted.');
        this.showAddLeaveModal.set(false);
        this.addLeaveForm.reset();
        this.loadAll();
      },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Could not apply leave.')
    });
  }

  /** Approves a leave request. */
  approveLeave(l: Leave) {
    const uid = this.auth.currentUser()!;
    this.leaveService.approveOrReject({
      leaveId: l.id, approvedById: uid, isApproved: true, managerComment: 'Approved by Admin'
    }).subscribe({
      next: () => { this.toast.success('Leave Approved'); this.loadAll(); },
      error: () => this.toast.error('Error', 'Failed.')
    });
  }

  /** Rejects a leave request. */
  rejectLeave(l: Leave) {
    const uid = this.auth.currentUser()!;
    this.leaveService.approveOrReject({
      leaveId: l.id, approvedById: uid, isApproved: false, managerComment: 'Rejected by Admin'
    }).subscribe({
      next: () => { this.toast.warning('Leave Rejected'); this.loadAll(); },
      error: () => this.toast.error('Error', 'Failed.')
    });
  }

  private refreshToday(): void {
    const uid = this.auth.currentUser();
    if (!uid) return;
    this.attendanceService.getTodayStatus(uid).subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (d?.missedCheckout) this.showMissedCheckoutModal.set(true);
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
      },
      error: () => this.todayAtt.set(null)
    });
  }

  /** Records a check-in for the current admin user. */
  checkIn(): void {
    if (this.todayAtt()?.checkIn) { this.toast.warning('Already checked in', ''); return; }
    this.attLoading.set(true);
    this.attendanceService.checkIn().subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
        this.attendanceService.getAll(1, 200).subscribe({
          next: (r: any) => this.allAttendances.set(this.extractArray<Attendance>(r)),
          error: () => {}
        });
        this.toast.success('Checked In', `Welcome! Time: ${d?.checkIn}`);
        this.notificationService.pushLocal('Attendance', 'Admin checked in successfully.');
        this.attLoading.set(false);
      },
      error: (err: any) => {
        this.toast.error('Check-In Failed', err?.error?.message ?? 'Please try again.');
        this.attLoading.set(false);
      }
    });
  }

  /** Records a check-out for the current admin user. */
  checkOut(): void {
    if (!this.todayAtt()?.checkIn) { this.toast.warning('Not checked in', ''); return; }
    if (this.todayAtt()?.checkOut) { this.toast.warning('Already checked out', ''); return; }
    this.attLoading.set(true);
    this.attendanceService.checkOut().subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        this.stopTimer();
        this.attendanceService.getAll(1, 200).subscribe({
          next: (r: any) => this.allAttendances.set(this.extractArray<Attendance>(r)),
          error: () => {}
        });
        this.toast.success('Checked Out', `Total: ${d?.totalHours ?? '—'}`);
        this.notificationService.pushLocal('Attendance', 'Admin checked out successfully.');
        this.attLoading.set(false);
      },
      error: (err: any) => {
        this.toast.error('Check-Out Failed', err?.error?.message ?? 'Please try again.');
        this.attLoading.set(false);
      }
    });
  }

  private startTimer(t: string): void {
    if (!t) return;
    const [h, m] = t.split(':').map(Number);
    const base = new Date();
    base.setHours(h, m, 0, 0);
    this.stopTimer();
    this.timerInterval = setInterval(() => {
      const diff = Date.now() - base.getTime();
      const hh = Math.floor(diff / 3600000);
      const mm = Math.floor((diff % 3600000) / 60000);
      const ss = Math.floor((diff % 60000) / 1000);
      this.liveTimer.set(`${this.padNumber(hh)}:${this.padNumber(mm)}:${this.padNumber(ss)}`);
    }, 1000);
  }

  private stopTimer(): void {
    if (this.timerInterval) clearInterval(this.timerInterval);
  }

  private padNumber(n: number): string {
    return n < 10 ? '0' + n : '' + n;
  }

  /** Persists the current settings to localStorage. */
  saveSettings() {
    localStorage.setItem('admin_settings', JSON.stringify(this.settings));
    this.toast.success('Settings Saved', 'Configuration updated successfully.');
  }

  /** Returns a human-readable status label for a numeric status value. */
  getStatusText(s: any): string {
    return s == 0 ? 'Pending' : s == 1 ? 'Approved' : 'Rejected';
  }

  /** Returns the CSS badge class for a numeric status value. */
  getStatusClass(s: any): string {
    return s == 0 ? 'zbadge-pending' : s == 1 ? 'zbadge-approved' : 'zbadge-rejected';
  }

  leaveDays(l: any): number {
    if (!l?.fromDate || !l?.toDate) return 0;
    return Math.ceil((new Date(l.toDate).getTime() - new Date(l.fromDate).getTime()) / 86400000) + 1;
  }

  Number = Number;

  /** Switches the active tab and updates breadcrumb + pagination. */
  setTab(t: AdminTab) {
    this.activeTab.set(t);
    this.tabService.setTab(t);
    this.breadcrumbService.set([
      { label: 'Admin Dashboard' },
      { label: this.tabs.find(x => x.key === t)?.label ?? '' }
    ]);
    this.userPage.set(1);
    this.tsPage.set(1);
    this.leavePage.set(1);
    this.projectPage.set(1);
  }

  /** Returns an array of page numbers for a given total page count. */
  pages(total: number) {
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  toDateStr(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  weekDates = () => {
    const now = new Date();
    const offset = this.tsPeriodOffset();
    const monday = new Date(now);
    const day = now.getDay() || 7;
    monday.setDate(now.getDate() - day + 1 + offset * 7);
    monday.setHours(0, 0, 0, 0);
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(monday);
      d.setDate(monday.getDate() + i);
      return d;
    });
  };

  visibleWeekDates = () => {
    const all = this.weekDates();
    if (this.tsPeriodOffset() !== 0) return all;
    const todayStr = this.toDateStr(new Date());
    return all.filter(d => this.toDateStr(d) <= todayStr);
  };

  /** Parses a hours value that may be a number, "H:MM" string, or decimal string. */
  parseHours(val: any): number {
    if (!val) return 0;
    if (typeof val === 'number') return val;
    const s = String(val);
    if (s.includes(':')) {
      const [h, m] = s.split(':').map(Number);
      return h + (m || 0) / 60;
    }
    return parseFloat(s) || 0;
  }

  /** Formats a decimal hours value as "Xh Ym" or "Xh". */
  formatHours(decimal: number): string {
    if (!decimal) return '—';
    const h = Math.floor(decimal);
    const m = Math.round((decimal - h) * 60);
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  weeklyRows = () => {
    const dates = this.visibleWeekDates();
    const all = this.allTimesheets();
    const map = new Map<string, { ts: any; hours: (number|null)[]; tsPerDay: (any|null)[] }>();
    for (const ts of all) {
      const d = new Date(ts.date);
      d.setHours(0, 0, 0, 0);
      const idx = dates.findIndex(wd => wd.getTime() === d.getTime());
      if (idx === -1) continue;
      const key = `${ts.employeeName}|${ts.projectName}`;
      if (!map.has(key)) {
        map.set(key, { ts, hours: Array(dates.length).fill(null), tsPerDay: Array(dates.length).fill(null) });
      }
      map.get(key)!.hours[idx]    = this.parseHours(ts.hoursWorked);
      map.get(key)!.tsPerDay[idx] = ts;
    }
    return [...map.values()];
  };

  groupedWeeklyRows = () => {
    const rows = this.weeklyRows();
    const map = new Map<string, { employeeName: string; rows: typeof rows }>();
    for (const row of rows) {
      const name = row.ts.employeeName ?? 'Unknown';
      if (!map.has(name)) map.set(name, { employeeName: name, rows: [] });
      map.get(name)!.rows.push(row);
    }
    return [...map.values()];
  };

  monthDates = () => {
    const now   = new Date();
    const year  = now.getFullYear();
    const month = now.getMonth() + this.tsPeriodOffset();
    const ref   = new Date(year, month, 1);
    const days  = new Date(ref.getFullYear(), ref.getMonth() + 1, 0).getDate();
    return Array.from({ length: days }, (_, i) => new Date(ref.getFullYear(), ref.getMonth(), i + 1));
  };

  monthlyGroupedRows = () => {
    const dates = this.monthDates();
    const all   = this.allTimesheets().filter((t: any) => this._inPeriod(t.date, 'monthly', this.tsPeriodOffset()));
    const empMap = new Map<string, Map<string, { hours: (number|null)[]; tsPerDay: (any|null)[]; projectName: string }>>();
    for (const ts of all) {
      const d = new Date(ts.date);
      d.setHours(0, 0, 0, 0);
      const idx = dates.findIndex(md => md.getTime() === d.getTime());
      if (idx === -1) continue;
      const emp = ts.employeeName ?? 'Unknown';
      const prj = ts.projectName  ?? '';
      if (!empMap.has(emp)) empMap.set(emp, new Map());
      const prjMap = empMap.get(emp)!;
      if (!prjMap.has(prj)) {
        prjMap.set(prj, { hours: Array(dates.length).fill(null), tsPerDay: Array(dates.length).fill(null), projectName: prj });
      }
      prjMap.get(prj)!.hours[idx]    = this.parseHours(ts.hoursWorked);
      prjMap.get(prj)!.tsPerDay[idx] = ts;
    }
    return [...empMap.entries()].map(([employeeName, prjMap]) => ({
      employeeName,
      rows: [...prjMap.values()]
    }));
  };

  private _periodLabel(mode: string, offset: number): string {
    if (mode === 'all') return '';
    const now = new Date();
    if (mode === 'weekly') {
      const s = new Date(now);
      s.setDate(now.getDate() - now.getDay() + offset * 7);
      const e = new Date(s);
      e.setDate(s.getDate() + 6);
      return `${s.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' })} – ${e.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}`;
    }
    return new Date(now.getFullYear(), now.getMonth() + offset, 1)
      .toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
  }

  private _inPeriod(dateStr: string, mode: string, offset: number): boolean {
    if (mode === 'all') return true;
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return false;
    const now = new Date();
    if (mode === 'weekly') {
      const s = new Date(now);
      s.setDate(now.getDate() - now.getDay() + offset * 7);
      s.setHours(0, 0, 0, 0);
      const e = new Date(s);
      e.setDate(s.getDate() + 6);
      e.setHours(23, 59, 59, 999);
      return d >= s && d <= e;
    }
    const ref = new Date(now.getFullYear(), now.getMonth() + offset, 1);
    return d.getFullYear() === ref.getFullYear() && d.getMonth() === ref.getMonth();
  }
}
