import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  Attendance,
  DashboardSummary,
  Leave, LeaveType,
  ProjectAssignment,
  Timesheet,
  UserProfile
} from '../../models';
import {
  AnalyticsService,
  AttendanceService,
  LeaveService, ProjectService,
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

export type EmployeeTab = 'dashboard' | 'timesheet' | 'attendance' | 'leave' | 'profile';

@Component({
  selector: 'app-employee-dashboard',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, FormsModule,
    NavbarComponent, SidebarComponent, ConfirmComponent],
  templateUrl: './employee-dashboard.component.html',
  styleUrl:    './employee-dashboard.component.css'
})
export class EmployeeDashboardComponent implements OnInit, OnDestroy {

  readonly auth  = inject(AuthService);
  private  toast = inject(ToastService);
  private  bc    = inject(BreadcrumbService);
  private  notif = inject(NotificationService);
  private  tsSvc  = inject(TimesheetService);
  private  attSvc = inject(AttendanceService);
  private  lvSvc  = inject(LeaveService);
  private  prjSvc = inject(ProjectService);
  private  anlSvc = inject(AnalyticsService);
  private  usrSvc = inject(UserService);
  private  fb     = inject(FormBuilder);
  private  tabSvc = inject(TabService);

  constructor() {
    effect(() => {
      const t = this.tabSvc.activeTab();
      if (t && t !== this.activeTab()) this.setTab(t as EmployeeTab);
    });
  }

  // ── Tabs ─────────────────────────────────────────────────────────────────
  activeTab = signal<EmployeeTab>('dashboard');
  readonly tabs: { key: EmployeeTab; label: string; icon: string }[] = [
    { key: 'dashboard',  label: 'Dashboard',  icon: '📊' },
    { key: 'timesheet',  label: 'Timesheets', icon: '📋' },
    { key: 'attendance', label: 'Attendance', icon: '⏰' },
    { key: 'leave',      label: 'Leave',      icon: '🌴' },
    { key: 'profile',    label: 'Profile',    icon: '👤' },
  ];

  // ── Data ─────────────────────────────────────────────────────────────────
  timesheets         = signal<Timesheet[]>([]);
  attendances        = signal<Attendance[]>([]);
  leaves             = signal<Leave[]>([]);
  projectsAssignment = signal<ProjectAssignment[]>([]);
  leaveTypes         = signal<LeaveType[]>([]);
  summary            = signal<DashboardSummary | null>(null);
  todayAtt           = signal<Attendance | null>(null);
  userProfile        = signal<UserProfile | null>(null);

  // ── Timesheet filters / sort / page ─────────────────────────────────────
  tsSearch  = signal('');
  tsStatus  = signal('all');
  tsSortCol = signal<'date' | 'hours' | 'project'>('date');
  tsSortDir = signal<'asc' | 'desc'>('desc');
  tsPage    = signal(1);
  tsPS = 6;

  // ── View mode (all / weekly / monthly) ───────────────────────────────────
  tsViewMode    = signal<'all'|'weekly'|'monthly'>('all');
  tsPeriodOffset = signal(0);
  attViewMode    = signal<'all'|'weekly'|'monthly'>('all');
  attPeriodOffset = signal(0);

  tsPeriodLabel = this._periodLabel(this.tsViewMode, this.tsPeriodOffset);
  attPeriodLabel = this._periodLabel(this.attViewMode, this.attPeriodOffset);

  private _periodLabel(mode: any, offset: any) {
    return () => {
      const m = mode(), o = offset();
      if (m === 'all') return '';
      const now = new Date();
      if (m === 'weekly') {
        const start = new Date(now); start.setDate(now.getDate() - now.getDay() + o * 7);
        const end   = new Date(start); end.setDate(start.getDate() + 6);
        return `${start.toLocaleDateString('en-GB',{day:'2-digit',month:'short'})} – ${end.toLocaleDateString('en-GB',{day:'2-digit',month:'short',year:'numeric'})}`;
      }
      const d = new Date(now.getFullYear(), now.getMonth() + o, 1);
      return d.toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
    };
  }

  private _inPeriod(dateStr: string, mode: string, offset: number): boolean {
    if (mode === 'all') return true;
    const d = new Date(dateStr); if (isNaN(d.getTime())) return false;
    const now = new Date();
    if (mode === 'weekly') {
      const start = new Date(now); start.setDate(now.getDate() - now.getDay() + offset * 7); start.setHours(0,0,0,0);
      const end   = new Date(start); end.setDate(start.getDate() + 6); end.setHours(23,59,59,999);
      return d >= start && d <= end;
    }
    const y = now.getFullYear(), mo = now.getMonth() + offset;
    return d.getFullYear() === new Date(y, mo, 1).getFullYear() && d.getMonth() === new Date(y, mo, 1).getMonth();
  }

  filteredTs = computed(() => {
    let d = this.timesheets();
    const q = this.tsSearch().toLowerCase();
    if (q) d = d.filter(t => (t.projectName ?? '').toLowerCase().includes(q)
                           || (t.date ?? '').includes(q));
    if (this.tsStatus() !== 'all') {
      const sv: Record<string, number> = { pending: 0, approved: 1, rejected: 2 };
      d = d.filter(t => Number(t.status) === sv[this.tsStatus()]);
    }
    d = d.filter(t => this._inPeriod(t.date, this.tsViewMode(), this.tsPeriodOffset()));
    const col = this.tsSortCol(); const dir = this.tsSortDir();
    d = [...d].sort((a, b) => {
      const v = col === 'date'    ? new Date(a.date).getTime() - new Date(b.date).getTime()
              : col === 'hours'   ? (a.hoursWorked ?? 0) - (b.hoursWorked ?? 0)
              : (a.projectName ?? '').localeCompare(b.projectName ?? '');
      return dir === 'asc' ? v : -v;
    });
    return d;
  });
  tsTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredTs().length / this.tsPS)));
  pagedTs      = computed(() => {
    const s = (this.tsPage() - 1) * this.tsPS;
    return this.filteredTs().slice(s, s + this.tsPS);
  });

  // ── Attendance page ───────────────────────────────────────────────────────
  attPage = signal(1);
  attPS = 8;
  filteredAtt = computed(() =>
    this.attendances().filter(a => this._inPeriod(a.date, this.attViewMode(), this.attPeriodOffset()))
  );
  pagedAtt = computed(() => {
    const s = (this.attPage() - 1) * this.attPS;
    return this.filteredAtt().slice(s, s + this.attPS);
  });
  attTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredAtt().length / this.attPS)));

  // ── Leave page ────────────────────────────────────────────────────────────
  lvPage = signal(1);
  lvPS = 6;
  pagedLv = computed(() => {
    const s = (this.lvPage() - 1) * this.lvPS;
    return this.leaves().slice(s, s + this.lvPS);
  });
  lvTotalPages = computed(() => Math.max(1, Math.ceil(this.leaves().length / this.lvPS)));

  // ── Weekly grid ───────────────────────────────────────────────────────────
  weekDays = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];

  weekDates = () => {
    const now = new Date();
    const offset = this.tsPeriodOffset();
    const monday = new Date(now);
    const day = now.getDay() || 7;
    monday.setDate(now.getDate() - day + 1 + offset * 7);
    monday.setHours(0,0,0,0);
    return Array.from({length:7}, (_, i) => {
      const d = new Date(monday); d.setDate(monday.getDate() + i); return d;
    });
  };

  weeklyRows = () => {
    const dates = this.weekDates();
    const all   = this.timesheets();
    const map   = new Map<string, { ts: any; hours: (number|null)[] }>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      const idx = dates.findIndex(wd => wd.getTime() === d.getTime());
      if (idx === -1) continue;
      const key = ts.projectName;
      if (!map.has(key)) map.set(key, { ts, hours: Array(7).fill(null) });
      map.get(key)!.hours[idx] = ts.hoursWorked;
    }
    return [...map.values()];
  };
  approvedCount = computed(() => this.timesheets().filter(t => t.status === 1).length);
  pendingCount  = computed(() => this.timesheets().filter(t => t.status === 0).length);
 totalHours = computed(() => {
  const list = this.timesheets() ?? [];

  const total = list.reduce((s, t) => {
    const hours = Number(t?.hoursWorked);
    return s + (isNaN(hours) ? 0 : hours);
  }, 0);

  return total.toFixed(1);
});

  // ── Modals ────────────────────────────────────────────────────────────────
  showTsModal    = signal(false);
  showLeaveModal = signal(false);
  showEditModal  = signal(false);
  editTs         = signal<Timesheet | null>(null);
  attLoading     = signal(false);

  // ── Confirm dialog ────────────────────────────────────────────────────────
  cfgVisible = signal(false);
  cfgTitle   = signal('');
  cfgMsg     = signal('');
  private cfgAction: (() => void) | null = null;

  // ── Timer ─────────────────────────────────────────────────────────────────
  liveTimer = signal('00:00:00');
  private timerInterval: any;
  readonly todayDate = new Date().toISOString().split('T')[0];

  // ── Forms ─────────────────────────────────────────────────────────────────
  tsForm = this.fb.group({
    projectId:       ['', Validators.required],
    projectName:     [''],
    workDate:        [this.todayDate, Validators.required],
    startTime:       ['09:00', Validators.required],
    endTime:         ['18:00', Validators.required],
    breakTime:       ['01:00'],
    taskDescription: ['']
  });

  editTsForm = this.fb.group({
    workDate:        ['', Validators.required],
    startTime:       ['', Validators.required],
    endTime:         ['', Validators.required],
    breakTime:       ['01:00'],
    taskDescription: ['']
  });

  leaveForm = this.fb.group({
    leaveTypeId: ['', Validators.required],
    fromDate:    ['', Validators.required],
    toDate:      ['', Validators.required],
    reason:      ['']
  });

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.bc.set([{ label: 'My Workspace' }, { label: 'Dashboard' }]);
    this.tabSvc.setTab('dashboard');
    const uid = this.auth.currentUser();
    if (!uid) return;
    this.loadAll(uid);
    this.refreshToday();
    this.anlSvc.getDashboard(uid).subscribe({ next: r => this.summary.set(r), error: () => {} });
    this.usrSvc.getProfile().subscribe({ next: (r: any) => this.userProfile.set(r?.data ?? r), error: () => {} });
  }

  ngOnDestroy(): void { this.stopTimer(); }

  private toArr<T>(r: any): T[] {
    if (Array.isArray(r)) return r;
    if (Array.isArray(r?.data)) return r.data;
    if (Array.isArray(r?.data?.data)) return r.data.data;
    return [];
  }

  private loadAll(uid: number) {
    this.tsSvc.getByUser(uid).subscribe(r => this.timesheets.set(this.toArr<Timesheet>(r)));
    this.attSvc.getMyAttendance(uid).subscribe(r => this.attendances.set(this.toArr<Attendance>(r)));
    this.lvSvc.getMyLeaves(uid).subscribe(r => this.leaves.set(this.toArr<Leave>(r)));
    this.lvSvc.getLeaveTypes().subscribe(r => this.leaveTypes.set(this.toArr<LeaveType>(r)));
    this.prjSvc.getUserAssignments(uid, 1, 50).subscribe(r => this.projectsAssignment.set(this.toArr<ProjectAssignment>(r)));
  }

  private refreshToday(): void {
    const uid = this.auth.currentUser();
    if (!uid) return;
    this.attSvc.getTodayStatus(uid).subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (d?.missedCheckout) {
          this.toast.warning('Missed Check-Out', 'You forgot to check out yesterday. Your attendance was auto-calculated as check-in + 8 hours.');
        }
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
      },
      error: () => this.todayAtt.set(null)
    });
  }

  // ── Attendance ────────────────────────────────────────────────────────────
  checkIn(): void {
  if (this.todayAtt()?.checkIn) {
    this.toast.warning('Already checked in', '');
    return;
  }

  this.attLoading.set(true);

  this.attSvc.checkIn().subscribe({
    next: (res: any) => {
      const d = res?.data ?? res;

      this.todayAtt.set(d);

      if (d?.checkIn && !d?.checkOut) {
        this.startTimer(d.checkIn);
      }

      // 🔥 FIX: Refresh attendance list
      const uid = this.auth.currentUser();
      if (uid) {
        this.attSvc.getMyAttendance(uid).subscribe(r => {
          this.attendances.set(this.toArr<Attendance>(r));
        });
      }

      this.toast.success('Checked In', `Welcome! Time: ${d?.checkIn}`);
      this.notif.pushLocal('Attendance', 'You checked in successfully.');
      this.attLoading.set(false);
    },

    error: (err: any) => {
      this.toast.error('Check-In Failed', err?.error?.message ?? 'Please try again.');
      this.attLoading.set(false);
    }
  });
}

 checkOut(): void {
    if (!this.todayAtt()?.checkIn) {
      this.toast.warning('Not checked in', '');
      return;
    }
    if (this.todayAtt()?.checkOut) {
      this.toast.warning('Already checked out', '');
      return;
    }
    this.attLoading.set(true);
    this.attSvc.checkOut().subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        this.stopTimer();
        // 🔥 FIX: Refresh attendance list (THIS WAS MISSING)
        const uid = this.auth.currentUser();
        if (uid) {
          this.attSvc.getMyAttendance(uid).subscribe(r => {
            this.attendances.set(this.toArr<Attendance>(r));
          });
        }
        this.toast.success('Checked Out', `Total: ${d?.totalHours ?? '—'}`);
        this.notif.pushLocal('Attendance', 'You checked out successfully.');
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
    const base = new Date(); base.setHours(h, m, 0, 0);
    this.stopTimer();
    this.timerInterval = setInterval(() => {
      const diff = Date.now() - base.getTime();
      const hh = Math.floor(diff / 3600000);
      const mm = Math.floor((diff % 3600000) / 60000);
      const ss = Math.floor((diff % 60000) / 1000);
      this.liveTimer.set(`${this.pad(hh)}:${this.pad(mm)}:${this.pad(ss)}`);
    }, 1000);
  }

  private stopTimer(): void { if (this.timerInterval) clearInterval(this.timerInterval); }
  private pad(n: number): string { return n < 10 ? '0' + n : '' + n; }

  // ── Sort ──────────────────────────────────────────────────────────────────
  sortTs(col: 'date' | 'hours' | 'project') {
    if (this.tsSortCol() === col) this.tsSortDir.update(d => d === 'asc' ? 'desc' : 'asc');
    else { this.tsSortCol.set(col); this.tsSortDir.set('asc'); }
    this.tsPage.set(1);
  }
  ico(active: boolean, dir: string) { return !active ? '⇅' : dir === 'asc' ? '↑' : '↓'; }

  // ── Confirm ───────────────────────────────────────────────────────────────
  private confirm(title: string, msg: string, action: () => void) {
    this.cfgTitle.set(title); this.cfgMsg.set(msg);
    this.cfgAction = action; this.cfgVisible.set(true);
  }
  onCfgOk()     { this.cfgAction?.(); this.cfgVisible.set(false); this.cfgAction = null; }
  onCfgCancel() { this.cfgVisible.set(false); this.cfgAction = null; }

  // ── Timesheet CRUD ────────────────────────────────────────────────────────
  onProjectChange(): void {
    const id = Number(this.tsForm.value.projectId);
    const p  = this.projectsAssignment().find(x => x.id === id);
    if (p) this.tsForm.patchValue({ projectName: p.projectName });
  }

  submitTimesheet(): void {
    if (this.tsForm.invalid) { this.tsForm.markAllAsTouched(); return; }
    const v   = this.tsForm.value;
    const uid = this.auth.currentUser();
    if (!uid) return;

    const asgn = this.projectsAssignment().find(p => p.id === +v.projectId!);
    if (!asgn) { this.toast.error('Invalid Project', 'Please select a valid project.'); return; }

    this.tsSvc.create(uid, {
      projectId:       asgn.projectId,
      projectName:     asgn.projectName,
      workDate:        v.workDate!,
      startTime:       this.fmt(v.startTime!),
      endTime:         this.fmt(v.endTime!),
      breakTime:       this.fmt(v.breakTime || '00:00'),
      taskDescription: v.taskDescription ?? ''
    }).subscribe({
      next: () => {
        this.toast.success('Timesheet Submitted', 'Your timesheet is pending approval.');
        this.showTsModal.set(false);
        this.tsForm.reset({ workDate: this.todayDate, breakTime: '01:00' });
        this.tsSvc.getByUser(uid).subscribe(r => this.timesheets.set(this.toArr<Timesheet>(r)));
      },
      error: (e: any) => this.toast.error('Submission Failed', e?.error?.message ?? 'Please try again.')
    });
  }

  openEditTs(ts: Timesheet): void {
    if (ts.status !== 0) { this.toast.warning('Cannot Edit', 'Only Pending timesheets can be edited.'); return; }
    this.editTs.set(ts);
    this.editTsForm.patchValue({
      workDate:        (ts.date ?? '').split('T')[0],
      startTime:       (ts.startTime ?? '').substring(0, 5),
      endTime:         (ts.endTime ?? '').substring(0, 5),
      breakTime:       (ts.breakTime ?? '01:00').substring(0, 5),
      taskDescription: ts.description ?? ''
    });
    this.showEditModal.set(true);
  }

  updateTimesheet(): void {
    const ts = this.editTs();
    if (!ts || this.editTsForm.invalid) return;
    const v = this.editTsForm.value;
    this.tsSvc.update(ts.id, {
      workDate:  v.workDate!,
      startTime: this.fmt(v.startTime!),
      endTime:   this.fmt(v.endTime!),
      breakTime: this.fmt(v.breakTime || '00:00'),
      Description: v.taskDescription ?? ''
    }).subscribe({
      next: () => {
        this.toast.success('Updated', 'Timesheet updated successfully.');
        this.showEditModal.set(false);
        const uid = this.auth.currentUser()!;
        this.tsSvc.getByUser(uid).subscribe(r => this.timesheets.set(this.toArr<Timesheet>(r)));
      },
      error: (e: any) => this.toast.error('Update Failed', e?.error?.message ?? '')
    });
  }

  confirmDeleteTs(ts: Timesheet): void {
    this.confirm(
      'Delete Timesheet',
      `Delete timesheet for "${ts.projectName}" on ${new Date(ts.date).toLocaleDateString()}? This cannot be undone.`,
      () => {
        this.tsSvc.delete(ts.id).subscribe({
          next: () => {
            this.toast.success('Deleted', 'Timesheet removed.');
            const uid = this.auth.currentUser()!;
            this.tsSvc.getByUser(uid).subscribe(r => this.timesheets.set(this.toArr<Timesheet>(r)));
          },
          error: (e: any) => this.toast.error('Delete Failed', e?.error?.message ?? '')
        });
      }
    );
  }

  confirmDeleteLeave(l: Leave): void {
    this.confirm('Delete Leave', `Delete this leave request? This cannot be undone.`, () => {
      this.lvSvc.deleteLeave(l.id).subscribe({
        next: () => {
          this.toast.success('Deleted', 'Leave request removed.');
          const uid = this.auth.currentUser()!;
          this.lvSvc.getMyLeaves(uid).subscribe(r => this.leaves.set(this.toArr<Leave>(r)));
        },
        error: (e: any) => this.toast.error('Delete Failed', e?.error?.message ?? '')
      });
    });
  }

  // ── Leave ─────────────────────────────────────────────────────────────────
  
  validateLeaveDates(): boolean {
    const from = new Date(this.leaveForm.value.fromDate!);
    const to   = new Date(this.leaveForm.value.toDate!);
    const today = new Date();

    // remove time
    today.setHours(0,0,0,0);
    from.setHours(0,0,0,0);
    to.setHours(0,0,0,0);

    // ❌ Past date not allowed
    if (from < today) {
      this.toast.error('Invalid Date', 'Past dates are not allowed.');
      return false;
    }

    // ❌ To date before from date
    if (to < from) {
      this.toast.error('Invalid Range', 'To date cannot be before From date.');
      return false;
    }

    return true;
  }
submitLeave(): void {
  if (this.leaveForm.invalid) {
    this.leaveForm.markAllAsTouched();
    return;
  }

  if (!this.validateLeaveDates()) return;

  const v   = this.leaveForm.value;
  const uid = this.auth.currentUser();
  if (!uid) return;

  this.lvSvc.apply(uid, {
    leaveTypeId: +v.leaveTypeId!,
    fromDate:    v.fromDate!,
    toDate:      v.toDate!,
    reason:      v.reason ?? ''
  }).subscribe({
    next: (res: any) => {

      const remaining = res?.data?.remainingLeaves;

      const finalMsg = remaining !== undefined
        ? `Leave applied successfully. Remaining leave: ${remaining} day(s)`
        : 'Leave applied successfully';

      this.toast.success('Leave Applied', finalMsg);

      this.showLeaveModal.set(false);
      this.leaveForm.reset();

      this.lvSvc.getMyLeaves(uid).subscribe(r =>
        this.leaves.set(this.toArr<Leave>(r))
      );
    },

    error: (e: any) =>
      this.toast.error('Application Failed', e?.error?.message ?? '')
  });
}
  // ── Helpers ───────────────────────────────────────────────────────────────
  private fmt(t: string): string { return t?.length === 5 ? t + ':00' : t ?? '00:00:00'; }

  stText(s: any)  { return s == 0 ? 'Pending' : s == 1 ? 'Approved' : 'Rejected'; }
  stClass(s: any) { return s == 0 ? 'zbadge-pending' : s == 1 ? 'zbadge-approved' : 'zbadge-rejected'; }

  leaveDays(l: Leave): number {
    return Math.ceil((new Date(l.toDate).getTime() - new Date(l.fromDate).getTime()) / 86400000) + 1;
  }

  setTab(tab: EmployeeTab): void {
    this.activeTab.set(tab);
    this.tabSvc.setTab(tab);
    this.bc.set([{ label: 'My Workspace' }, { label: this.tabs.find(t => t.key === tab)?.label ?? '' }]);
    this.tsPage.set(1); this.attPage.set(1); this.lvPage.set(1);
  }

  pages(total: number): number[] { return Array.from({ length: total }, (_, i) => i + 1); }
}
