import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Attendance, Leave, LeaveType, Project, Timesheet, User, UserProfile } from '../../models';
import {
    AttendanceService, LeaveService,
    ProjectService,
    TimesheetService,
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

export type ManagerTab = 'dashboard' | 'timesheets' | 'leaves' | 'team' | 'projects' | 'profile';

@Component({
  selector: 'app-manager-dashboard',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, FormsModule,
    NavbarComponent, SidebarComponent, ConfirmComponent],
  templateUrl: './manager-dashboard.component.html',
  styleUrl:    './manager-dashboard.component.css'
})
export class ManagerDashboardComponent implements OnInit, OnDestroy {

  readonly auth  = inject(AuthService);
  private  toast = inject(ToastService);
  private  bc    = inject(BreadcrumbService);
  private  notif = inject(NotificationService);
  private  tsSvc  = inject(TimesheetService);
  private  lvSvc  = inject(LeaveService);
  private  usrSvc = inject(UserService);
  private  prjSvc = inject(ProjectService);
  private  attSvc = inject(AttendanceService);
  private  fb     = inject(FormBuilder);
  private  tabSvc = inject(TabService);

  constructor() {
    effect(() => {
      const t = this.tabSvc.activeTab();
      if (t && t !== this.activeTab()) this.setTab(t as ManagerTab);
    });
  }

  activeTab = signal<ManagerTab>('dashboard');
  readonly tabs = [
    { key: 'dashboard'  as ManagerTab, label: 'Overview',   icon: '📊' },
    { key: 'timesheets' as ManagerTab, label: 'Timesheets', icon: '📋' },
    { key: 'leaves'     as ManagerTab, label: 'Leaves',     icon: '🌴' },
    { key: 'team'       as ManagerTab, label: 'My Team',    icon: '👥' },
    { key: 'projects'   as ManagerTab, label: 'Projects',   icon: '🗂' },
    { key: 'profile'    as ManagerTab, label: 'Profile',    icon: '👤' },
  ];

  allTimesheets = signal<Timesheet[]>([]);
  allLeaves     = signal<Leave[]>([]);
  teamMembers   = signal<User[]>([]);
  projects      = signal<Project[]>([]);
  projectAssignments = signal<{ [projectId: number]: any[] }>({});
  userProfile   = signal<UserProfile | null>(null);

  // ── TS filter/sort/page ────────────────────────────────────────────────────
  tsSearch  = signal('');
  tsStatus  = signal('all');
  tsSortCol = signal<'date' | 'hours' | 'employee'>('date');
  tsSortDir = signal<'asc' | 'desc'>('desc');
  tsPage    = signal(1);
  tsPS = 8;

  // ── View mode ─────────────────────────────────────────────────────────────
  tsViewMode      = signal<'all'|'weekly'|'monthly'>('all');
  tsPeriodOffset  = signal(0);

  tsPeriodLabel = () => this._periodLabel(this.tsViewMode(), this.tsPeriodOffset());

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

  filteredTs = computed(() => {
    let d = this.allTimesheets();
    const q = this.tsSearch().toLowerCase();
    if (q) d = d.filter(t => (t.employeeName ?? '').toLowerCase().includes(q)
                           || (t.projectName ?? '').toLowerCase().includes(q));
    if (this.tsStatus() !== 'all') {
      const sv: Record<string, number> = { pending: 0, approved: 1, rejected: 2 };
      d = d.filter(t => Number(t.status) === sv[this.tsStatus()]);
    }
    d = d.filter(t => this._inPeriod(t.date, this.tsViewMode(), this.tsPeriodOffset()));
    const col = this.tsSortCol(); const dir = this.tsSortDir();
    d = [...d].sort((a, b) => {
      const v = col === 'date'     ? new Date(a.date).getTime() - new Date(b.date).getTime()
              : col === 'hours'    ? (a.hoursWorked ?? 0) - (b.hoursWorked ?? 0)
              : (a.employeeName ?? '').localeCompare(b.employeeName ?? '');
      return dir === 'asc' ? v : -v;
    });
    return d;
  });
  tsTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredTs().length / this.tsPS)));
  pagedTs      = computed(() => { const s = (this.tsPage()-1)*this.tsPS; return this.filteredTs().slice(s,s+this.tsPS); });

  weekDays = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];
  weekDates = () => {
    const now = new Date(); const offset = this.tsPeriodOffset();
    const monday = new Date(now); const day = now.getDay() || 7;
    monday.setDate(now.getDate() - day + 1 + offset * 7); monday.setHours(0,0,0,0);
    return Array.from({length:7}, (_, i) => { const d = new Date(monday); d.setDate(monday.getDate() + i); return d; });
  };
  weeklyRows = () => {
    const dates = this.weekDates(); const all = this.allTimesheets();
    const map = new Map<string, { ts: any; hours: (number|null)[] }>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      const idx = dates.findIndex(wd => wd.getTime() === d.getTime());
      if (idx === -1) continue;
      const key = `${ts.employeeName}|${ts.projectName}`;
      if (!map.has(key)) map.set(key, { ts, hours: Array(7).fill(null) });
      map.get(key)!.hours[idx] = ts.hoursWorked;
    }
    return [...map.values()];
  };

  // ── Leave filter/page ──────────────────────────────────────────────────────
  lvSearch  = signal('');
  lvStatus  = signal('all');
  lvPage    = signal(1);
  lvPS = 8;

  filteredLv = computed(() => {
    let d = this.allLeaves();
    const q = this.lvSearch().toLowerCase();
    if (q) d = d.filter(l => (l.employeeName ?? '').toLowerCase().includes(q));
    if (this.lvStatus() !== 'all') {
      const sv: Record<string, number> = { pending: 0, approved: 1, rejected: 2 };
      d = d.filter(l => Number(l.status) === sv[this.lvStatus()]);
    }
    return d;
  });
  lvTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredLv().length / this.lvPS)));
  pagedLv      = computed(() => { const s = (this.lvPage()-1)*this.lvPS; return this.filteredLv().slice(s,s+this.lvPS); });

  // ── Team filter/page ───────────────────────────────────────────────────────
  teamSearch = signal('');
  teamPage   = signal(1);
  teamPS = 8;

  filteredTeam = computed(() => {
    const q = this.teamSearch().toLowerCase();
    if (!q) return this.teamMembers();
    return this.teamMembers().filter(u => (u.name??'').toLowerCase().includes(q) || (u.role??'').toLowerCase().includes(q));
  });
  teamTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredTeam().length / this.teamPS)));
  pagedTeam      = computed(() => { const s=(this.teamPage()-1)*this.teamPS; return this.filteredTeam().slice(s,s+this.teamPS); });

  // ── Stats ─────────────────────────────────────────────────────────────────
  pendingTs  = computed(() => this.allTimesheets().filter(t => Number(t.status) === 0));
  pendingLv  = computed(() => this.allLeaves().filter(l => Number(l.status) === 0));
  totalHours = computed(() => {
  const total = this.allTimesheets()
    ?.filter(t => t.status === 1)
    ?.reduce((s, t) => {
      const hours = Number(t?.hoursWorked);
      return s + (isNaN(hours) ? 0 : hours);
    }, 0) ?? 0;

  return total.toFixed(1);
});

  // ── Project assign ────────────────────────────────────────────────────────
  selProjectId: number | null = null;
  selUserId:    number | null = null;

  // ── Confirm ───────────────────────────────────────────────────────────────
  cfgVisible = signal(false);
  cfgTitle   = signal('');
  cfgMsg     = signal('');
  cfgType    = signal<'danger'|'warning'|'info'>('info');
  private cfgAction: (() => void) | null = null;

  // ── Own check-in/out ──────────────────────────────────────────────────────
  todayAtt   = signal<Attendance | null>(null);
  attLoading = signal(false);
  liveTimer  = signal('00:00:00');
  private timerInterval: any;
  readonly todayDate = new Date().toISOString().split('T')[0];

  // ── Self timesheet / leave ────────────────────────────────────────────────
  showAddTimesheetModal = signal(false);
  showAddLeaveModal     = signal(false);
  leaveTypes            = signal<LeaveType[]>([]);

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

  ngOnInit() {
    this.bc.set([{ label: 'Manager Dashboard' }, { label: 'Overview' }]);
    this.tabSvc.setTab('dashboard');
    this.loadAll();
    this.refreshToday();
    this.usrSvc.getProfile().subscribe({ next:(r:any)=>this.userProfile.set(r?.data??r), error:()=>{} });
    this.lvSvc.getLeaveTypes().subscribe({ next:(r:any)=>this.leaveTypes.set(this.toArr<LeaveType>(r)), error:()=>{} });
  }

  ngOnDestroy() { if (this.timerInterval) clearInterval(this.timerInterval); }

  private toArr<T>(r: any): T[] {
    if (Array.isArray(r)) return r;
    if (Array.isArray(r?.data)) return r.data;
    if (Array.isArray(r?.data?.data)) return r.data.data;
    return [];
  }

  loadAll() {
    this.tsSvc.getAll().subscribe({
      next: (r: any) => {
        const d = r?.data?.data ?? r?.data ?? r ?? [];
        this.allTimesheets.set((Array.isArray(d) ? d : []).filter((t: any) => t?.id && t?.employeeName));
      }, error: () => {}
    });
    this.lvSvc.getAll().subscribe({
      next: (r: any) => {
        const d = r?.data?.data ?? r?.data ?? r ?? [];
        this.allLeaves.set((Array.isArray(d) ? d : []).filter((l: any) => l?.id && l?.employeeName));
      }, error: () => {}
    });
    this.usrSvc.getAll().subscribe({
      next: (r: any) => {
        const d = this.toArr<User>(r);
        this.teamMembers.set(d.filter(u => ['Employee'].includes(u.role)));
      }, error: () => {}
    });
   this.prjSvc.getAll().subscribe({
  next: (r: any) => {
    const projects = this.toArr<Project>(r);
    this.projects.set(projects);

    // 🔥 load assignments for each project
    projects.forEach(p => this.loadAssignments(p.id));
  }
});
  }

  private refreshToday(): void {
    const uid = this.auth.currentUser(); if (!uid) return;
    this.attSvc.getTodayStatus(uid).subscribe({
      next:(res:any)=>{ const d=res?.data??res; this.todayAtt.set(d); if(d?.missedCheckout) this.toast.warning('Missed Check-Out','You forgot to check out yesterday. Attendance auto-calculated as check-in + 8 hours.'); if(d?.checkIn&&!d?.checkOut) this.startTimer(d.checkIn); },
      error:()=>this.todayAtt.set(null)
    });
  }

  checkIn(): void {
    if (this.todayAtt()?.checkIn) { this.toast.warning('Already checked in',''); return; }
    this.attLoading.set(true);
    this.attSvc.checkIn().subscribe({
      next:(res:any)=>{ const d=res?.data??res; this.todayAtt.set(d); if(d?.checkIn&&!d?.checkOut) this.startTimer(d.checkIn); this.toast.success('Checked In',`Time: ${d?.checkIn}`); this.attLoading.set(false); },
      error:(e:any)=>{ this.toast.error('Failed',e?.error?.message??''); this.attLoading.set(false); }
    });
  }

  checkOut(): void {
    if (!this.todayAtt()?.checkIn) { this.toast.warning('Not checked in',''); return; }
    if (this.todayAtt()?.checkOut) { this.toast.warning('Already checked out',''); return; }
    this.attLoading.set(true);
    this.attSvc.checkOut().subscribe({
      next:(res:any)=>{ const d=res?.data??res; this.todayAtt.set(d); this.stopTimer(); this.toast.success('Checked Out',`Total: ${d?.totalHours??'—'}`); this.attLoading.set(false); },
      error:(e:any)=>{ this.toast.error('Failed',e?.error?.message??''); this.attLoading.set(false); }
    });
  }

  private startTimer(t:string): void {
    const [h,m]=t.split(':').map(Number); const base=new Date(); base.setHours(h,m,0,0);
    this.stopTimer();
    this.timerInterval=setInterval(()=>{ const diff=Date.now()-base.getTime(); const hh=Math.floor(diff/3600000),mm=Math.floor((diff%3600000)/60000),ss=Math.floor((diff%60000)/1000); this.liveTimer.set(`${this.pad(hh)}:${this.pad(mm)}:${this.pad(ss)}`); },1000);
  }
  private stopTimer(): void { if(this.timerInterval) clearInterval(this.timerInterval); }
  private pad(n:number): string { return n<10?'0'+n:''+n; }

  sortTs(col: 'date'|'hours'|'employee') {    if (this.tsSortCol() === col) this.tsSortDir.update(d => d==='asc'?'desc':'asc');
    else { this.tsSortCol.set(col); this.tsSortDir.set('asc'); }
    this.tsPage.set(1);
  }
  ico(active: boolean, dir: string) { return !active ? '⇅' : dir==='asc' ? '↑' : '↓'; }

  private confirm(title: string, msg: string, action: ()=>void, type: 'danger'|'warning'|'info'='info') {
    this.cfgTitle.set(title); this.cfgMsg.set(msg); this.cfgType.set(type);
    this.cfgAction = action; this.cfgVisible.set(true);
  }
  onCfgOk()     { this.cfgAction?.(); this.cfgVisible.set(false); this.cfgAction = null; }
  onCfgCancel() { this.cfgVisible.set(false); this.cfgAction = null; }

  reviewTs(ts: Timesheet, approve: boolean) {
    const uid = this.auth.currentUser();
    if (!uid) { this.toast.error('Error','Invalid session.'); return; }
    this.confirm(
      approve ? 'Approve Timesheet' : 'Reject Timesheet',
      `${approve?'Approve':'Reject'} timesheet for "${ts.employeeName}" (${ts.projectName}) on ${new Date(ts.date).toLocaleDateString()}?`,
      () => {
        this.tsSvc.approveOrReject({
          timesheetId: ts.id, approvedById: uid,
          isApproved: approve, managerComment: approve ? 'Approved by Manager' : 'Rejected by Manager'
        }).subscribe({
          next: () => {
            this.toast.success(approve ? 'Approved' : 'Rejected', `Timesheet for ${ts.employeeName}.`);
            this.loadAll();
          },
          error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Action failed.')
        });
      },
      approve ? 'info' : 'warning'
    );
  }

  reviewLeave(l: Leave, approve: boolean) {
    const uid = this.auth.currentUser();
    if (!uid) { this.toast.error('Error','Invalid session.'); return; }
    this.confirm(
      approve ? 'Approve Leave' : 'Reject Leave',
      `${approve?'Approve':'Reject'} leave request from "${l.employeeName}"?`,
      () => {
        this.lvSvc.approveOrReject({
          leaveId: l.id, approvedById: uid,
          isApproved: approve, managerComment: approve ? 'Approved' : 'Rejected'
        }).subscribe({
          next: () => {
            this.toast.success(approve ? 'Leave Approved' : 'Leave Rejected', `For ${l.employeeName}`);
            this.loadAll();
          },
          error: (e: any) => this.toast.error('Failed', e?.error?.message ?? '')
        });
      },
      approve ? 'info' : 'warning'
    );
  }

  assignUserToProject() {
    if (!this.selProjectId || !this.selUserId) {
      this.toast.warning('Select Both', 'Please select a project and a team member.');
      return;
    }
    const project = this.projects().find(p => p.id === this.selProjectId);
    this.prjSvc.assign({
      projectId: this.selProjectId, userId: this.selUserId,
      projectName: project?.projectName ?? ''
    }).subscribe({
      next: () => {
        this.toast.success('Assigned', 'Team member assigned to project.');
        this.selProjectId = null; this.selUserId = null;
      },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Assignment failed.')
    });
  }
  loadAssignments(projectId: number) {
  this.prjSvc.getAssignmentsByProject(projectId).subscribe({
    next: (res: any) => {
      const data = res?.data ?? res ?? [];

      this.projectAssignments.update(prev => ({
        ...prev,
        [projectId]: data
      }));
    },
    error: () => {
      this.toast.error('Error', 'Failed to load assigned users');
    }
  });
}

  removeUserFromProject(assignmentId: number, projectId: number) {
    this.confirm('Remove Member', 'Remove this member from the project?', () => {
      this.prjSvc.removeAssignment(assignmentId).subscribe({
        next: () => { this.toast.success('Removed', 'Member removed from project.'); this.loadAssignments(projectId); },
        error: (e: any) => this.toast.error('Error', e?.error?.message ?? 'Failed.')
      });
    }, 'warning');
  }

  stText(s: any)  { return s==0?'Pending':s==1?'Approved':'Rejected'; }
  stClass(s: any) { return s==0?'zbadge-pending':s==1?'zbadge-approved':'zbadge-rejected'; }

  addTimesheetForSelf() {
    if (this.addTimesheetForm.invalid) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    const v = this.addTimesheetForm.getRawValue();
    const proj = this.projects().find(p => p.id === +v.projectId!);
    const fmt = (t: string) => t?.length === 5 ? t + ':00' : t ?? '00:00:00';
    this.tsSvc.create(uid, {
      projectId: +v.projectId!, projectName: proj?.projectName ?? '',
      workDate: v.workDate!, startTime: fmt(v.startTime!), endTime: fmt(v.endTime!),
      breakTime: fmt(v.breakTime || '00:00'), taskDescription: v.taskDescription ?? ''
    }).subscribe({
      next: () => { this.toast.success('Timesheet Added', 'Your timesheet has been submitted.'); this.showAddTimesheetModal.set(false); this.addTimesheetForm.reset({ workDate: new Date().toISOString().split('T')[0], startTime: '09:00', endTime: '18:00', breakTime: '01:00' }); this.loadAll(); },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Could not create timesheet.')
    });
  }

  addLeaveForSelf() {
    if (this.addLeaveForm.invalid) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    const v = this.addLeaveForm.getRawValue();
    this.lvSvc.apply(uid, { leaveTypeId: +v.leaveTypeId!, fromDate: v.fromDate!, toDate: v.toDate!, reason: v.reason ?? '' }).subscribe({
      next: () => { this.toast.success('Leave Applied', 'Your leave request has been submitted.'); this.showAddLeaveModal.set(false); this.addLeaveForm.reset(); this.loadAll(); },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Could not apply leave.')
    });
  }

  setTab(t: ManagerTab) {
    this.activeTab.set(t);
    this.tabSvc.setTab(t);
    this.bc.set([{label:'Manager Dashboard'},{label:this.tabs.find(x=>x.key===t)?.label??''}]);
    this.tsPage.set(1); this.lvPage.set(1); this.teamPage.set(1);
  }

  pages(total: number) { return Array.from({length:total},(_,i)=>i+1); }
}
