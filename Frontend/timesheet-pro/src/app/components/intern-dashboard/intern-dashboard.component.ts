import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Attendance, DashboardSummary, InternTask, Leave, LeaveType, ProjectAssignment, UserProfile } from '../../models';
import { AnalyticsService, AttendanceService, InternService, LeaveService, ProjectService, UserService } from '../../services/api.services';
import { AuthService } from '../../services/auth.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
import { TabService } from '../../services/tab.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmComponent } from '../confirm-dialog/confirm.component';
import { NavbarComponent } from '../navbar/navbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

export type InternTab = 'dashboard' | 'attendance' | 'leave' | 'tasks' | 'profile';

@Component({
  selector: 'app-intern-dashboard',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, FormsModule, NavbarComponent, SidebarComponent, ConfirmComponent],
  templateUrl: './intern-dashboard.component.html',
  styleUrl:    './intern-dashboard.component.css'
})
export class InternDashboardComponent implements OnInit, OnDestroy {
  readonly auth             = inject(AuthService);
  private  toast            = inject(ToastService);
  private  breadcrumbService = inject(BreadcrumbService);
  private  attendanceService = inject(AttendanceService);
  private  leaveService      = inject(LeaveService);
  private  projectService    = inject(ProjectService);
  private  analyticsService  = inject(AnalyticsService);
  private  internService     = inject(InternService);
  private  userService       = inject(UserService);
  private  formBuilder       = inject(FormBuilder);
  private  tabService        = inject(TabService);

  constructor() {
    effect(() => {
      const t = this.tabService.activeTab();
      if (t && t !== this.activeTab()) this.setTab(t as InternTab);
    });
  }

  // ── Tabs ──────────────────────────────────────────────────────────────────
  activeTab = signal<InternTab>('dashboard');
  readonly tabs: { key: InternTab; label: string; icon: string }[] = [
    { key: 'dashboard',  label: 'Dashboard',  icon: '📊' },
    { key: 'attendance', label: 'Attendance',  icon: '⏰' },
    { key: 'leave',      label: 'Leave',       icon: '🌴' },
    { key: 'tasks',      label: 'Tasks',       icon: '📝' },
    { key: 'profile',    label: 'Profile',     icon: '👤' },
  ];

  // ── Data signals ──────────────────────────────────────────────────────────
  attendances = signal<Attendance[]>([]);
  leaves      = signal<Leave[]>([]);
  leaveTypes  = signal<LeaveType[]>([]);
  projects    = signal<ProjectAssignment[]>([]);
  tasks       = signal<InternTask[]>([]);
  summary     = signal<DashboardSummary | null>(null);
  todayAtt    = signal<Attendance | null>(null);
  attLoading  = signal(false);
  userProfile = signal<UserProfile | null>(null);

  // ── Attendance filter/page ────────────────────────────────────────────────
  attViewMode     = signal<'all' | 'weekly' | 'monthly'>('all');
  attPeriodOffset = signal(0);
  attPage         = signal(1);
  attPS           = 8;

  attPeriodLabel = () => this._periodLabel(this.attViewMode(), this.attPeriodOffset());

  filteredAtt   = computed(() => this.attendances().filter(a => this._inPeriod(a.date, this.attViewMode(), this.attPeriodOffset())));
  pagedAtt      = computed(() => { const s = (this.attPage() - 1) * this.attPS; return this.filteredAtt().slice(s, s + this.attPS); });
  attTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredAtt().length / this.attPS)));

  // ── Modals / UI signals ───────────────────────────────────────────────────
  showLeaveModal          = signal(false);
  showMissedCheckoutModal = signal(false);
  showLeaveBalanceModal   = signal(false);
  leaveBalanceResult      = signal<{ leaveType: string; remaining: number; total: number } | null>(null);
  leaveBalance            = signal<{ leaveType: string; total: number; used: number; remaining: number }[]>([]);

  // ── Confirm dialog ────────────────────────────────────────────────────────
  confirmVisible = signal(false);
  confirmTitle   = signal('');
  confirmMessage = signal('');
  private confirmAction: (() => void) | null = null;

  // ── Timer ─────────────────────────────────────────────────────────────────
  liveTimer = signal('00:00:00');
  private timerInterval: any;

  // ── Helpers ───────────────────────────────────────────────────────────────
  readonly todayDate = this.toDateStr(new Date());

  toDateStr(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  private _periodLabel(mode: string, offset: number): string {
    if (mode === 'all') return '';
    const now = new Date();
    if (mode === 'weekly') {
      const s = new Date(now); s.setDate(now.getDate() - now.getDay() + offset * 7);
      const e = new Date(s);   e.setDate(s.getDate() + 6);
      return `${s.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' })} – ${e.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}`;
    }
    return new Date(now.getFullYear(), now.getMonth() + offset, 1).toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
  }

  private _inPeriod(dateStr: string, mode: string, offset: number): boolean {
    if (mode === 'all') return true;
    const d = new Date(dateStr); if (isNaN(d.getTime())) return false;
    const now = new Date();
    if (mode === 'weekly') {
      const s = new Date(now); s.setDate(now.getDate() - now.getDay() + offset * 7); s.setHours(0, 0, 0, 0);
      const e = new Date(s);   e.setDate(s.getDate() + 6); e.setHours(23, 59, 59, 999);
      return d >= s && d <= e;
    }
    const ref = new Date(now.getFullYear(), now.getMonth() + offset, 1);
    return d.getFullYear() === ref.getFullYear() && d.getMonth() === ref.getMonth();
  }

  // ── Forms ─────────────────────────────────────────────────────────────────
  leaveForm = this.formBuilder.group({
    leaveTypeId: ['', Validators.required],
    fromDate:    ['', Validators.required],
    toDate:      ['', Validators.required],
    reason:      ['']
  });

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.breadcrumbService.set([{ label: 'Intern Hub' }, { label: 'Dashboard' }]);
    this.tabService.setTab('dashboard');
    const uid = this.auth.currentUser(); if (!uid) return;
    this.loadAll(uid);
    this.refreshToday();
    this.analyticsService.getDashboard(uid).subscribe({ next: r => this.summary.set(r), error: () => {} });
    this.userService.getProfile().subscribe({ next: (r: any) => this.userProfile.set(r?.data ?? r), error: () => {} });
  }

  ngOnDestroy(): void { if (this.timerInterval) clearInterval(this.timerInterval); }

  // ── Data loading ──────────────────────────────────────────────────────────
  private extractArray<T>(r: any): T[] {
    if (Array.isArray(r)) return r;
    if (Array.isArray(r?.data)) return r.data;
    if (Array.isArray(r?.data?.data)) return r.data.data;
    return [];
  }

  loadAll(uid: number): void {
    this.attendanceService.getMyAttendance(uid).subscribe(r => this.attendances.set(this.extractArray<Attendance>(r)));
    this.leaveService.getMyLeaves(uid).subscribe(r => this.leaves.set(this.extractArray<Leave>(r)));
    this.leaveService.getLeaveTypes().subscribe(r => this.leaveTypes.set(this.extractArray<LeaveType>(r)));
    this.leaveService.getLeaveBalance(uid).subscribe((r: any) => this.leaveBalance.set(r?.data ?? []));
    this.projectService.getUserAssignments(uid, 1, 50).subscribe(r => this.projects.set(this.extractArray<ProjectAssignment>(r)));
    this.internService.getTasks(uid).subscribe(r => this.tasks.set(this.extractArray<InternTask>(r)));
  }

  // ── Attendance ────────────────────────────────────────────────────────────
  private refreshToday(): void {
    const uid = this.auth.currentUser(); if (!uid) return;
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

  checkIn(): void {
    if (this.todayAtt()?.checkIn) { this.toast.warning('Already checked in', ''); return; }
    this.attLoading.set(true);
    this.attendanceService.checkIn().subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
        this.toast.success('Checked In', '');
        this.attLoading.set(false);
      },
      error: (e: any) => { this.toast.error('Failed', e?.error?.message ?? ''); this.attLoading.set(false); }
    });
  }

  checkOut(): void {
    if (!this.todayAtt()?.checkIn) { this.toast.warning('Not checked in', ''); return; }
    if (this.todayAtt()?.checkOut) { this.toast.warning('Already checked out', ''); return; }
    this.attLoading.set(true);
    this.attendanceService.checkOut().subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (this.timerInterval) clearInterval(this.timerInterval);
        this.toast.success('Checked Out', `Total: ${d?.totalHours ?? '—'}`);
        this.attLoading.set(false);
      },
      error: (e: any) => { this.toast.error('Failed', e?.error?.message ?? ''); this.attLoading.set(false); }
    });
  }

  private startTimer(t: string): void {
    const [h, m] = t.split(':').map(Number);
    const base = new Date(); base.setHours(h, m, 0, 0);
    if (this.timerInterval) clearInterval(this.timerInterval);
    this.timerInterval = setInterval(() => {
      const diff = Date.now() - base.getTime();
      const hh = Math.floor(diff / 3600000);
      const mm = Math.floor((diff % 3600000) / 60000);
      const ss = Math.floor((diff % 60000) / 1000);
      this.liveTimer.set(`${this.padNumber(hh)}:${this.padNumber(mm)}:${this.padNumber(ss)}`);
    }, 1000);
  }

  private padNumber(n: number): string { return n < 10 ? '0' + n : '' + n; }

  // ── Leave ─────────────────────────────────────────────────────────────────
  confirmDeleteLeave(l: Leave): void {
    this.confirmTitle.set('Delete Leave');
    this.confirmMessage.set('Delete this leave request?');
    this.confirmAction = () => {
      this.leaveService.deleteLeave(l.id).subscribe({
        next: () => {
          this.toast.success('Deleted', '');
          const uid = this.auth.currentUser()!;
          this.leaveService.getMyLeaves(uid).subscribe(r => this.leaves.set(this.extractArray<Leave>(r)));
        },
        error: (e: any) => this.toast.error('Error', e?.error?.message ?? '')
      });
    };
    this.confirmVisible.set(true);
  }

  onConfirmOk(): void { this.confirmAction?.(); this.confirmVisible.set(false); this.confirmAction = null; }
  onConfirmCancel(): void { this.confirmVisible.set(false); this.confirmAction = null; }

  validateLeaveDates(): boolean {
    const from  = new Date(this.leaveForm.value.fromDate!);
    const to    = new Date(this.leaveForm.value.toDate!);
    const today = new Date();
    today.setHours(0, 0, 0, 0); from.setHours(0, 0, 0, 0); to.setHours(0, 0, 0, 0);
    if (from < today) { this.toast.error('Invalid Date', 'Past dates are not allowed.'); return false; }
    if (to < from)    { this.toast.error('Invalid Range', 'To date cannot be before From date.'); return false; }
    return true;
  }

  submitLeave(): void {
    if (this.leaveForm.invalid) { this.leaveForm.markAllAsTouched(); return; }
    if (!this.validateLeaveDates()) return;
    const v   = this.leaveForm.value;
    const uid = this.auth.currentUser(); if (!uid) return;
    this.leaveService.apply(uid, {
      leaveTypeId: +v.leaveTypeId!,
      fromDate:    v.fromDate!,
      toDate:      v.toDate!,
      reason:      v.reason ?? ''
    }).subscribe({
      next: (res: any) => {
        const remaining    = res?.data?.remainingLeaves ?? 0;
        const selectedType = this.leaveTypes().find(lt => lt.id === +v.leaveTypeId!);
        this.showLeaveModal.set(false);
        this.leaveForm.reset();
        this.leaveBalanceResult.set({ leaveType: selectedType?.name ?? 'Leave', remaining, total: selectedType?.maxDaysPerYear ?? 0 });
        this.showLeaveBalanceModal.set(true);
        this.leaveService.getMyLeaves(uid).subscribe(r => this.leaves.set(this.extractArray<Leave>(r)));
        this.leaveService.getLeaveBalance(uid).subscribe((r: any) => this.leaveBalance.set(r?.data ?? []));
      },
      error: (e: any) => this.toast.error('Application Failed', e?.error?.message ?? '')
    });
  }

  leaveDays(l: Leave): number {
    return Math.ceil((new Date(l.toDate).getTime() - new Date(l.fromDate).getTime()) / 86400000) + 1;
  }

  // ── Tasks ─────────────────────────────────────────────────────────────────
  taskLabel(t: InternTask): string {
    return (t as any).taskTitle || (t as any).title || (t as any).Title || `Task #${t.id}`;
  }

  taskStatusText(s: any): string { return s == 1 ? 'Pending' : s == 2 ? 'In Progress' : 'Completed'; }
  taskStatusClass(s: any): string { return s == 3 ? 'zbadge-approved' : s == 2 ? 'zbadge-info' : 'zbadge-pending'; }

  updateTaskStatus(t: InternTask, newStatus: number): void {
    const uid = this.auth.currentUser()!;
    this.internService.updateStatus(t.id, newStatus).subscribe({
      next: () => {
        const label = newStatus === 3 ? 'Completed' : newStatus === 2 ? 'In Progress' : 'Pending';
        this.toast.success('Task Updated', `Marked as ${label}`);
        this.internService.getTasks(uid).subscribe(r => this.tasks.set(this.extractArray<InternTask>(r)));
      },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? '')
    });
  }

  markTaskComplete(t: InternTask): void { this.updateTaskStatus(t, 3); }

  // ── Status helpers ────────────────────────────────────────────────────────
  getStatusText(s: any): string  { return s == 0 ? 'Pending' : s == 1 ? 'Approved' : 'Rejected'; }
  getStatusClass(s: any): string { return s == 0 ? 'zbadge-pending' : s == 1 ? 'zbadge-approved' : 'zbadge-rejected'; }

  // ── Navigation ────────────────────────────────────────────────────────────
  setTab(t: InternTab): void {
    this.activeTab.set(t);
    this.tabService.setTab(t);
    this.breadcrumbService.set([{ label: 'Intern Hub' }, { label: this.tabs.find(x => x.key === t)?.label ?? '' }]);
  }

  pages(n: number): number[] { return Array.from({ length: n }, (_, i) => i + 1); }
}
