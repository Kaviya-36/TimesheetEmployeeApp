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
  readonly auth  = inject(AuthService);
  private  toast = inject(ToastService);
  private  bc    = inject(BreadcrumbService);
  private  notif = inject(NotificationService);
  private  userSvc = inject(UserService);
  private  prjSvc  = inject(ProjectService);
  private  tsSvc   = inject(TimesheetService);
  private  anlSvc  = inject(AnalyticsService);
  private  lvSvc   = inject(LeaveService);
  private  attSvc  = inject(AttendanceService);
  private  auditSvc = inject(AuditLogService);
  private  fb      = inject(FormBuilder);
  private  tabSvc  = inject(TabService);

  constructor() {
    effect(() => {
      const t = this.tabSvc.activeTab();
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

  allUsers      = signal<User[]>([]);
  allProjects   = signal<Project[]>([]);
  allTimesheets = signal<Timesheet[]>([]);
  allLeaves     = signal<Leave[]>([]);
  allAttendances = signal<Attendance[]>([]);
  summary       = signal<DashboardSummary | null>(null);
  userProfile   = signal<UserProfile | null>(null);

  // ── Audit Logs ────────────────────────────────────────────────────────────
  allAuditLogs    = signal<AuditLog[]>([]);
  auditSearch     = signal('');
  auditAction     = signal('all');
  auditTable      = signal('all');
  auditPage       = signal(1);
  auditPS = 10;
  filteredAudit = computed(() => {
    let d = this.allAuditLogs();
    const q = this.auditSearch().toLowerCase();
    if (q) d = d.filter(a => a.tableName.toLowerCase().includes(q) || a.action.toLowerCase().includes(q) || (a.keyValues ?? '').toLowerCase().includes(q));
    if (this.auditAction() !== 'all') d = d.filter(a => a.action === this.auditAction());
    if (this.auditTable()  !== 'all') d = d.filter(a => a.tableName.toLowerCase() === this.auditTable().toLowerCase());
    return d;
  });
  auditTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredAudit().length / this.auditPS)));
  pagedAudit      = computed(() => { const s = (this.auditPage()-1)*this.auditPS; return this.filteredAudit().slice(s, s+this.auditPS); });
  auditTables     = computed(() => [...new Set(this.allAuditLogs().map(a => a.tableName))].sort());

  // ── Admin own check-in/out ────────────────────────────────────────────────
  todayAtt    = signal<Attendance | null>(null);
  attLoading  = signal(false);
  liveTimer   = signal('00:00:00');
  private timerInterval: any;
  readonly todayDate = new Date().toISOString().split('T')[0];

  // ── Attendance list filters ───────────────────────────────────────────────
  attSearch = signal('');
  attPage   = signal(1);
  attPS = 8;
  attTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredAtt().length / this.attPS)));
  filteredAtt = computed(() => {
    const q = this.attSearch().toLowerCase();
    let d = this.allAttendances();
    if (q) d = d.filter(a => (a.employeeName ?? '').toLowerCase().includes(q));
    d = d.filter(a => this._inPeriod(a.date, this.attViewMode(), this.attPeriodOffset()));
    return d;
  });
  pagedAtt = computed(() => {
    const s = (this.attPage() - 1) * this.attPS;
    return this.filteredAtt().slice(s, s + this.attPS);
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

  uSearch   = signal('');  uRole = signal('all');  uStatus = signal('all');
  uPage     = signal(1);   uSort = signal<'name'|'role'|'status'|'joined'>('name');
  uSortDir  = signal<'asc'|'desc'>('asc');
  uPS = 8;

  tsSearch  = signal('');  tsStatus = signal('all');  tsPage = signal(1);
  tsSortCol = signal<'date'|'hours'|'employee'>('date');  tsSortDir = signal<'asc'|'desc'>('desc');
  tsPS = 8;

  // ── View mode ─────────────────────────────────────────────────────────────
  tsViewMode      = signal<'all'|'weekly'|'monthly'>('all');
  tsPeriodOffset  = signal(0);
  attViewMode     = signal<'all'|'weekly'|'monthly'>('all');
  attPeriodOffset = signal(0);

  tsPeriodLabel  = () => this._periodLabel(this.tsViewMode(),  this.tsPeriodOffset());
  attPeriodLabel = () => this._periodLabel(this.attViewMode(), this.attPeriodOffset());

  // ── Weekly grid helpers ───────────────────────────────────────────────────
  weekDays = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];

  toDateStr(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  }

  weekDates = () => {
    const now = new Date(); const offset = this.tsPeriodOffset();
    const monday = new Date(now); const day = now.getDay() || 7;
    monday.setDate(now.getDate() - day + 1 + offset * 7); monday.setHours(0,0,0,0);
    return Array.from({length:7}, (_, i) => { const d = new Date(monday); d.setDate(monday.getDate() + i); return d; });
  };

  visibleWeekDates = () => {
    const all = this.weekDates();
    if (this.tsPeriodOffset() !== 0) return all;
    const todayStr = this.toDateStr(new Date());
    return all.filter(d => this.toDateStr(d) <= todayStr);
  };

  parseHoursVal(val: any): number {
    if (!val) return 0;
    if (typeof val === 'number') return val;
    const s = String(val);
    if (s.includes(':')) { const [h, m] = s.split(':').map(Number); return h + (m||0)/60; }
    return parseFloat(s) || 0;
  }

  fmtH(decimal: number): string {
    if (!decimal) return '—';
    const h = Math.floor(decimal); const m = Math.round((decimal-h)*60);
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  weeklyRows = () => {
    const dates = this.visibleWeekDates(); const all = this.allTimesheets();
    const map = new Map<string, { ts: any; hours: (number|null)[]; tsPerDay: (any|null)[] }>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      const idx = dates.findIndex(wd => wd.getTime() === d.getTime());
      if (idx === -1) continue;
      const key = `${ts.employeeName}|${ts.projectName}`;
      if (!map.has(key)) map.set(key, { ts, hours: Array(dates.length).fill(null), tsPerDay: Array(dates.length).fill(null) });
      map.get(key)!.hours[idx] = this.parseHoursVal(ts.hoursWorked);
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
    const now = new Date();
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
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      const idx = dates.findIndex(md => md.getTime() === d.getTime());
      if (idx === -1) continue;
      const emp = ts.employeeName ?? 'Unknown';
      const prj = ts.projectName ?? '';
      if (!empMap.has(emp)) empMap.set(emp, new Map());
      const prjMap = empMap.get(emp)!;
      if (!prjMap.has(prj)) prjMap.set(prj, { hours: Array(dates.length).fill(null), tsPerDay: Array(dates.length).fill(null), projectName: prj });
      prjMap.get(prj)!.hours[idx]    = this.parseHoursVal(ts.hoursWorked);
      prjMap.get(prj)!.tsPerDay[idx] = ts;
    }
    return [...empMap.entries()].map(([employeeName, prjMap]) => ({ employeeName, rows: [...prjMap.values()] }));
  };

  private _periodLabel(mode: string, offset: number): string {
    if (mode === 'all') return '';
    const now = new Date();
    if (mode === 'weekly') {
      const s = new Date(now); s.setDate(now.getDate() - now.getDay() + offset * 7);
      const e = new Date(s);   e.setDate(s.getDate() + 6);
      return `${s.toLocaleDateString('en-GB',{day:'2-digit',month:'short'})} – ${e.toLocaleDateString('en-GB',{day:'2-digit',month:'short',year:'numeric'})}`;
    }
    return new Date(now.getFullYear(), now.getMonth() + offset, 1).toLocaleDateString('en-GB',{month:'long',year:'numeric'});
  }

  private _inPeriod(dateStr: string, mode: string, offset: number): boolean {
    if (mode === 'all') return true;
    const d = new Date(dateStr); if (isNaN(d.getTime())) return false;
    const now = new Date();
    if (mode === 'weekly') {
      const s = new Date(now); s.setDate(now.getDate() - now.getDay() + offset * 7); s.setHours(0,0,0,0);
      const e = new Date(s);   e.setDate(s.getDate() + 6); e.setHours(23,59,59,999);
      return d >= s && d <= e;
    }
    const ref = new Date(now.getFullYear(), now.getMonth() + offset, 1);
    return d.getFullYear() === ref.getFullYear() && d.getMonth() === ref.getMonth();
  }

  lvSearch = signal('');  lvStatus = signal('all');  lvPage = signal(1);
  lvPS = 8;

  prjPage = signal(1);  prjPS = 8;

  showAddUser = signal(false);  showAddProject = signal(false);
  editUser    = signal<User | null>(null);  newRole = '';
  editProject = signal<Project | null>(null);
  showAddTimesheetModal = signal(false);
  showAddLeaveModal     = signal(false);
  leaveTypes            = signal<LeaveType[]>([]);

  cfgVisible = signal(false);  cfgTitle = signal('');  cfgMsg = signal('');
  cfgType    = signal<'danger'|'warning'|'info'>('danger');
  private cfgAction: (() => void) | null = null;

  settings = { startTime: '09:00', endTime: '18:00', smtp: '', email: '' };
  readonly roleOptions = ['Admin','HR','Manager','Employee','Intern','Mentor'];
  readonly departments = [{id:1,name:'IT'},{id:2,name:'HR'},{id:3,name:'Finance'},{id:4,name:'Marketing'}];

  addUserForm = this.fb.group({
    employeeId:   ['', Validators.required],
    name:         ['', Validators.required],
    email:        ['', [Validators.required, Validators.email]],
    password:     ['', [Validators.required, Validators.minLength(6)]],
    phone:        [''],
    role:         ['Employee', Validators.required],
    departmentId: ['', Validators.required],
  });

  projectForm = this.fb.group({
    projectName: ['', Validators.required],
    description: [''],
    managerId:   [''],
    startDate:   ['', Validators.required],
    endDate:     [''],
  });

  editProjectForm = this.fb.group({
    projectName: ['', Validators.required],
    description: [''],
    managerId:   [''],
    startDate:   ['', Validators.required],
    endDate:     [''],
  });

  addTimesheetForm = this.fb.group({
    projectId:       ['', Validators.required],
    workDate:        [new Date().toISOString().split('T')[0], Validators.required],
    startTime:       ['09:00', Validators.required],
    endTime:         ['18:00', Validators.required],
    breakTime:       ['01:00'],
    taskDescription: [''],
  });

  addLeaveForm = this.fb.group({
    leaveTypeId: ['', Validators.required],
    fromDate:    ['', Validators.required],
    toDate:      ['', Validators.required],
    reason:      [''],
  });

  filteredUsers = computed(() => {
    let d = this.allUsers();
    const q = this.uSearch().toLowerCase();
    if (q) d = d.filter(u => u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q) || u.employeeId.toLowerCase().includes(q));
    if (this.uRole()   !== 'all') d = d.filter(u => u.role === this.uRole());
    if (this.uStatus() !== 'all') d = d.filter(u => this.uStatus() === 'active' ? u.status === 'Active' : u.status !== 'Active');
    const col = this.uSort(); const dir = this.uSortDir();
    d = [...d].sort((a,b) => {
      let v = col==='name' ? a.name.localeCompare(b.name)
            : col==='role' ? a.role.localeCompare(b.role)
            : col==='status' ? a.status.localeCompare(b.status)
            : new Date(a.joiningDate).getTime()-new Date(b.joiningDate).getTime();
      return dir==='asc'?v:-v;
    });
    return d;
  });
  uTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredUsers().length / this.uPS)));
  pagedUsers  = computed(() => { const s=(this.uPage()-1)*this.uPS; return this.filteredUsers().slice(s,s+this.uPS); });

  filteredTs = computed(() => {
    let d = this.allTimesheets();
    const q = this.tsSearch().toLowerCase();
    if (q) d = d.filter(t => (t.employeeName??'').toLowerCase().includes(q)||(t.projectName??'').toLowerCase().includes(q));
    if (this.tsStatus() !== 'all') {
      const sv:{[k:string]:number} = {pending:0,approved:1,rejected:2};
      d = d.filter(t => Number(t.status)===sv[this.tsStatus()]);
    }
    d = d.filter(t => this._inPeriod(t.date, this.tsViewMode(), this.tsPeriodOffset()));
    const col = this.tsSortCol(); const dir = this.tsSortDir();
    d = [...d].sort((a,b)=>{
      const v = col==='date'?new Date(a.date).getTime()-new Date(b.date).getTime()
              : col==='hours'?(a.hoursWorked??0)-(b.hoursWorked??0)
              : (a.employeeName??'').localeCompare(b.employeeName??'');
      return dir==='asc'?v:-v;
    });
    return d;
  });
  tsTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredTs().length / this.tsPS)));
  pagedTs      = computed(() => { const s=(this.tsPage()-1)*this.tsPS; return this.filteredTs().slice(s,s+this.tsPS); });

  filteredLv = computed(() => {
    let d = this.allLeaves();
    const q = this.lvSearch().toLowerCase();
    if (q) d = d.filter(l => (l.employeeName??'').toLowerCase().includes(q));
    if (this.lvStatus() !== 'all') {
      const sv:{[k:string]:number} = {pending:0,approved:1,rejected:2};
      d = d.filter(l => Number(l.status)===sv[this.lvStatus()]);
    }
    return d;
  });
  lvTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredLv().length / this.lvPS)));
  pagedLv      = computed(() => { const s=(this.lvPage()-1)*this.lvPS; return this.filteredLv().slice(s,s+this.lvPS); });

  pagedProjects = computed(() => { const s=(this.prjPage()-1)*this.prjPS; return this.allProjects().slice(s,s+this.prjPS); });
  prjTotalPages = computed(() => Math.max(1, Math.ceil(this.allProjects().length / this.prjPS)));

  totalUsers    = computed(() => this.allUsers().length);
  activeUsers   = computed(() => this.allUsers().filter(u => u.status==='Active').length);
  inactiveUsers = computed(() => this.allUsers().filter(u => u.status!=='Active').length);
  managers      = computed(() => this.allUsers().filter(u => u.role==='Manager'));
  pendingTs     = computed(() => this.allTimesheets().filter(t => Number(t.status)===0).length);
  pendingLv     = computed(() => this.allLeaves().filter(l => Number(l.status)===0).length);
  roleBreakdown = computed(() => this.roleOptions.map(r => ({ role:r, count:this.allUsers().filter(u=>u.role===r).length })));

  ngOnInit() {
    this.bc.set([{ label: 'Admin Dashboard' }, { label: 'Overview' }]);
    this.tabSvc.setTab('overview');
    this.loadAll();
    this.refreshToday();
    this.userSvc.getProfile().subscribe({ next:(r:any)=>this.userProfile.set(r?.data??r), error:()=>{} });
    const saved = localStorage.getItem('admin_settings');
    if (saved) try { this.settings = JSON.parse(saved); } catch {}
  }

  ngOnDestroy() { if (this.timerInterval) clearInterval(this.timerInterval); }

  private toArr<T>(r:any): T[] {
    if (Array.isArray(r)) return r;
    if (Array.isArray(r?.data)) return r.data;
    if (Array.isArray(r?.data?.data)) return r.data.data;
    return [];
  }

  loadAll() {
    this.userSvc.getAll().subscribe({ next:r=>this.allUsers.set(this.toArr<User>(r)), error:()=>{} });
    this.prjSvc.getAll().subscribe({ next:r=>this.allProjects.set(this.toArr<Project>(r)), error:()=>{} });
    this.tsSvc.getAll().subscribe({
      next:(r:any)=>{
        const raw = r?.data?.data ?? r?.data ?? r ?? [];
        this.allTimesheets.set((Array.isArray(raw)?raw:[]).map((t:any)=>({
          ...t, statusText:this.stText(t.status), statusClass:this.stClass(t.status)
        })));
      }, error:()=>{}
    });
    this.lvSvc.getAll().subscribe({ next:r=>this.allLeaves.set(this.toArr<Leave>(r)), error:()=>{} });
    this.lvSvc.getLeaveTypes().subscribe({ next:r=>this.leaveTypes.set(this.toArr<LeaveType>(r)), error:()=>{} });
    this.anlSvc.getDashboard().subscribe({ next:r=>this.summary.set(r), error:()=>{} });
    this.attSvc.getAll(1, 200).subscribe({
      next:(r:any)=>this.allAttendances.set(this.toArr<Attendance>(r)),
      error:()=>{}
    });
    this.auditSvc.getPaged(1, 200).subscribe({
      next:(r:any)=>this.allAuditLogs.set(this.toArr<AuditLog>(r)),
      error:()=>{}
    });
  }

  sortU(col:'name'|'role'|'status'|'joined') {
    if (this.uSort()===col) this.uSortDir.update(d=>d==='asc'?'desc':'asc');
    else { this.uSort.set(col); this.uSortDir.set('asc'); }
    this.uPage.set(1);
  }
  sortTs(col:'date'|'hours'|'employee') {
    if (this.tsSortCol()===col) this.tsSortDir.update(d=>d==='asc'?'desc':'asc');
    else { this.tsSortCol.set(col); this.tsSortDir.set('asc'); }
    this.tsPage.set(1);
  }
  ico(active:boolean, dir:string) { return !active?'⇅':dir==='asc'?'↑':'↓'; }

  private confirm(title:string, msg:string, action:()=>void, type:'danger'|'warning'|'info'='danger') {
    this.cfgTitle.set(title); this.cfgMsg.set(msg); this.cfgType.set(type);
    this.cfgAction=action; this.cfgVisible.set(true);
  }
  onCfgOk()     { this.cfgAction?.(); this.cfgVisible.set(false); this.cfgAction=null; }
  onCfgCancel() { this.cfgVisible.set(false); this.cfgAction=null; }

  addUser() {
    if (this.addUserForm.invalid) return;
    const v = this.addUserForm.getRawValue();
    this.auth.register({
      employeeId:v.employeeId!,name:v.name!,email:v.email!,
      password:v.password!,phone:v.phone??'',role:v.role!,departmentId:Number(v.departmentId!)
    }).subscribe({
      next:()=>{ this.toast.success('User Created','Account pending admin approval.'); this.showAddUser.set(false); this.addUserForm.reset({role:'Employee'}); this.loadAll(); },
      error:(err:any)=>this.toast.error('Failed',err?.error?.message??'Could not create user.')
    });
  }

  confirmDeleteUser(u:User) {
    this.confirm('Delete User',`Permanently delete "${u.name}"? Cannot be undone.`,()=>{
      this.userSvc.delete(u.id).subscribe({
        next:()=>{ this.toast.success('Deleted',`"${u.name}" removed.`); this.loadAll(); },
        error:(err:any)=>this.toast.error('Error',err?.error?.message??'Delete failed.')
      });
    });
  }

  toggleActive(u:User) {
    const activate = u.status !== 'Active';
    this.confirm(
      activate?'Activate User':'Deactivate User',
      `${activate?'Activate':'Deactivate'} "${u.name}"?`,
      ()=>{
        this.userSvc.setActive(u.id,activate).subscribe({
          next:()=>{ this.toast.success(activate?'Activated':'Deactivated',`"${u.name}" ${activate?'can now log in':'has been locked out'}.`); this.loadAll(); },
          error:(err:any)=>this.toast.error('Error',err?.error?.message??'Failed.')
        });
      },
      activate?'info':'warning'
    );
  }

  openEditUser(u:User) { this.editUser.set(u); this.newRole=u.role; }
  saveRole() {
    const u=this.editUser(); if(!u) return;
    this.userSvc.update(u.id,{role:this.newRole}).subscribe({
      next:()=>{ this.toast.success('Role Updated',`${u.name} is now ${this.newRole}.`); this.editUser.set(null); this.loadAll(); },
      error:(err:any)=>this.toast.error('Error',err?.error?.message??'Failed.')
    });
  }

  createProject() {
    if (this.projectForm.invalid) return;
    const v = this.projectForm.getRawValue();
    this.prjSvc.create({
      projectName:v.projectName!,description:v.description??'',
      managerId:v.managerId?+v.managerId:undefined,
      startDate:v.startDate!,endDate:v.endDate??undefined
    }).subscribe({
      next:()=>{ this.toast.success('Project Created'); this.showAddProject.set(false); this.projectForm.reset(); this.loadAll(); },
      error:(err:any)=>this.toast.error('Error',err?.error?.message??'Failed.')
    });
  }

  confirmDeleteProject(p:Project) {
    this.confirm('Delete Project',`Delete "${p.projectName}"?`,()=>{
      this.prjSvc.delete(p.id).subscribe({
        next:()=>{ this.toast.success('Deleted'); this.loadAll(); },
        error:(err:any)=>this.toast.error('Error',err?.error?.message??'Failed.')
      });
    });
  }

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

  saveEditProject() {
    const p = this.editProject(); if (!p || this.editProjectForm.invalid) return;
    const v = this.editProjectForm.getRawValue();
    this.prjSvc.update(p.id, {
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

  addTimesheetForUser() {
    if (this.addTimesheetForm.invalid) return;
    const uid = this.auth.currentUser();
    if (!uid) return;
    const v = this.addTimesheetForm.getRawValue();
    const proj = this.allProjects().find(p => p.id === +v.projectId!);
    const fmt = (t: string) => t?.length === 5 ? t + ':00' : t ?? '00:00:00';
    this.tsSvc.create(uid, {
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
        this.addTimesheetForm.reset({ workDate: new Date().toISOString().split('T')[0], startTime: '09:00', endTime: '18:00', breakTime: '01:00' });
        this.loadAll();
      },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Could not create timesheet.')
    });
  }

  addLeaveForUser() {
    if (this.addLeaveForm.invalid) return;
    const uid = this.auth.currentUser();
    if (!uid) return;
    const v = this.addLeaveForm.getRawValue();
    this.lvSvc.apply(uid, {
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

  approveLeave(l:Leave) {
    const uid=this.auth.currentUser()!;
    this.lvSvc.approveOrReject({leaveId:l.id,approvedById:uid,isApproved:true,managerComment:'Approved by Admin'}).subscribe({
      next:()=>{ this.toast.success('Leave Approved'); this.loadAll(); },
      error:()=>this.toast.error('Error','Failed.')
    });
  }
  rejectLeave(l:Leave) {
    const uid=this.auth.currentUser()!;
    this.lvSvc.approveOrReject({leaveId:l.id,approvedById:uid,isApproved:false,managerComment:'Rejected by Admin'}).subscribe({
      next:()=>{ this.toast.warning('Leave Rejected'); this.loadAll(); },
      error:()=>this.toast.error('Error','Failed.')
    });
  }

  private refreshToday(): void {
    const uid = this.auth.currentUser();
    if (!uid) return;
    this.attSvc.getTodayStatus(uid).subscribe({
      next:(res:any)=>{
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (d?.missedCheckout) this.toast.warning('Missed Check-Out','You forgot to check out yesterday. Attendance auto-calculated as check-in + 8 hours.');
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
      },
      error:()=>this.todayAtt.set(null)
    });
  }

  checkIn(): void {
    if (this.todayAtt()?.checkIn) { this.toast.warning('Already checked in',''); return; }
    this.attLoading.set(true);
    this.attSvc.checkIn().subscribe({
      next:(res:any)=>{
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
        this.attSvc.getAll(1,200).subscribe({next:(r:any)=>this.allAttendances.set(this.toArr<Attendance>(r)),error:()=>{}});
        this.toast.success('Checked In',`Welcome! Time: ${d?.checkIn}`);
        this.notif.pushLocal('Attendance','Admin checked in successfully.');
        this.attLoading.set(false);
      },
      error:(err:any)=>{ this.toast.error('Check-In Failed',err?.error?.message??'Please try again.'); this.attLoading.set(false); }
    });
  }

  checkOut(): void {
    if (!this.todayAtt()?.checkIn) { this.toast.warning('Not checked in',''); return; }
    if (this.todayAtt()?.checkOut) { this.toast.warning('Already checked out',''); return; }
    this.attLoading.set(true);
    this.attSvc.checkOut().subscribe({
      next:(res:any)=>{
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        this.stopTimer();
        this.attSvc.getAll(1,200).subscribe({next:(r:any)=>this.allAttendances.set(this.toArr<Attendance>(r)),error:()=>{}});
        this.toast.success('Checked Out',`Total: ${d?.totalHours ?? '—'}`);
        this.notif.pushLocal('Attendance','Admin checked out successfully.');
        this.attLoading.set(false);
      },
      error:(err:any)=>{ this.toast.error('Check-Out Failed',err?.error?.message??'Please try again.'); this.attLoading.set(false); }
    });
  }

  private startTimer(t:string): void {
    if (!t) return;
    const [h,m] = t.split(':').map(Number);
    const base = new Date(); base.setHours(h,m,0,0);
    this.stopTimer();
    this.timerInterval = setInterval(()=>{
      const diff = Date.now()-base.getTime();
      const hh=Math.floor(diff/3600000), mm=Math.floor((diff%3600000)/60000), ss=Math.floor((diff%60000)/1000);
      this.liveTimer.set(`${this.pad(hh)}:${this.pad(mm)}:${this.pad(ss)}`);
    },1000);
  }
  private stopTimer(): void { if (this.timerInterval) clearInterval(this.timerInterval); }
  private pad(n:number): string { return n<10?'0'+n:''+n; }

  saveSettings() {
    localStorage.setItem('admin_settings',JSON.stringify(this.settings));
    this.toast.success('Settings Saved','Configuration updated successfully.');
  }

  stText(s:any)  { return s==0?'Pending':s==1?'Approved':'Rejected'; }
  stClass(s:any) { return s==0?'zbadge-pending':s==1?'zbadge-approved':'zbadge-rejected'; }
  Number = Number;

  setTab(t:AdminTab) {
    this.activeTab.set(t);
    this.tabSvc.setTab(t);
    this.bc.set([{label:'Admin Dashboard'},{label:this.tabs.find(x=>x.key===t)?.label??''}]);
    this.uPage.set(1); this.tsPage.set(1); this.lvPage.set(1); this.prjPage.set(1);
  }

  pages(total:number) { return Array.from({length:total},(_,i)=>i+1); }
}
