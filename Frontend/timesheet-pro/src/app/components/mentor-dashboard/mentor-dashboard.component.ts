import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Attendance, InternTask, Leave, LeaveType, Project, User, UserProfile } from '../../models';
import { AttendanceService, InternService, LeaveService, ProjectService, TimesheetService, UserService } from '../../services/api.services';
import { AuthService } from '../../services/auth.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
import { NotificationService } from '../../services/notification.service';
import { TabService } from '../../services/tab.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmComponent } from '../confirm-dialog/confirm.component';
import { NavbarComponent } from '../navbar/navbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

export type MentorTab = 'interns' | 'leave' | 'profile';

@Component({
  selector: 'app-mentor-dashboard',
  standalone: true,
  imports: [DatePipe, FormsModule, ReactiveFormsModule, NavbarComponent, SidebarComponent, ConfirmComponent],
  templateUrl: './mentor-dashboard.component.html',
  styleUrl: './mentor-dashboard.component.css'
})
export class MentorDashboardComponent implements OnInit, OnDestroy {

  readonly auth  = inject(AuthService);
  private  toast = inject(ToastService);
  private  breadcrumbService    = inject(BreadcrumbService);
  private  notificationService = inject(NotificationService);
  private  internService  = inject(InternService);
  private  userService  = inject(UserService);
  private  attendanceService  = inject(AttendanceService);
  private  timesheetService   = inject(TimesheetService);
  private  leaveService   = inject(LeaveService);
  private  projectService  = inject(ProjectService);
  private  formBuilder      = inject(FormBuilder);
  private  tabService  = inject(TabService);

  constructor() {
    effect(() => {
      const t = this.tabService.activeTab();
      if (t && t !== this.activeTab()) this.setTab(t as MentorTab);
    });
  }

  // ── Tabs ──────────────────────────────────────────────────────────────────
  activeTab = signal<MentorTab>('interns');
  readonly tabs: { key: MentorTab; label: string; icon: string }[] = [
    { key: 'interns', label: 'Interns', icon: '🎓' },
    { key: 'leave',   label: 'My Leave', icon: '🌴' },
    { key: 'profile', label: 'Profile', icon: '👤' },
  ];

  // ── Data ──────────────────────────────────────────────────────────────────
  interns        = signal<User[]>([]);
  selectedIntern = signal<User | null>(null);
  tasks          = signal<InternTask[]>([]);
  userProfile    = signal<UserProfile | null>(null);
  projects       = signal<Project[]>([]);
  leaveTypes     = signal<LeaveType[]>([]);
  myLeaves       = signal<any[]>([]);

  // ── Self timesheet / leave modals ─────────────────────────────────────────
  showAddTimesheetModal    = signal(false);
  showAddLeaveModal        = signal(false);
  showMissedCheckoutModal  = signal(false);
  showLeaveBalanceModal = signal(false);
  leaveBalanceResult = signal<{ leaveType: string; remaining: number; total: number } | null>(null);
  leaveBalance = signal<{ leaveType: string; total: number; used: number; remaining: number }[]>([]);

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

  // ── Attendance ────────────────────────────────────────────────────────────
  todayAtt   = signal<Attendance | null>(null);
  attLoading = signal(false);
  liveTimer  = signal('00:00:00');
  private timerInterval: any;
  readonly todayDate = new Date().toISOString().split('T')[0];

  // ── Search ────────────────────────────────────────────────────────────────
  search = signal('');
  filteredInterns = computed(() => {
    const q = this.search().toLowerCase();
    if (!q) return this.interns();
    return this.interns().filter(i =>
      (i.name ?? '').toLowerCase().includes(q) ||
      (i.email ?? '').toLowerCase().includes(q)
    );
  });

  // ── Modals ────────────────────────────────────────────────────────────────
  showModal     = signal(false);
  showEditModal = signal(false);
  editTask      = signal<InternTask | null>(null);

  // ── Confirm dialog ────────────────────────────────────────────────────────
  confirmVisible = signal(false);
  confirmTitle   = signal('');
  confirmMessage     = signal('');
  private confirmAction: (() => void) | null = null;

  onConfirmOk()     { this.confirmAction?.(); this.confirmVisible.set(false); this.confirmAction = null; }
  onConfirmCancel() { this.confirmVisible.set(false); this.confirmAction = null; }

  // ── Forms ─────────────────────────────────────────────────────────────────
  taskForm = this.formBuilder.group({
    taskTitle:   ['', Validators.required],
    description: [''],
    dueDate:     ['']
  });

  editTaskForm = this.formBuilder.group({
    taskTitle:   ['', Validators.required],
    description: [''],
    dueDate:     ['']
  });

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.breadcrumbService.set([{ label: 'Mentor Dashboard' }, { label: 'Interns' }]);
    this.tabService.setTab('interns');
    this.loadInterns();
    this.refreshToday();
    this.userService.getProfile().subscribe({ next: (r: any) => this.userProfile.set(r?.data ?? r), error: () => {} });
    this.projectService.getAll().subscribe({ next: (r: any) => this.projects.set(this.extractArray<Project>(r)), error: () => {} });
    this.leaveService.getLeaveTypes().subscribe({ next: (r: any) => this.leaveTypes.set(this.extractArray<LeaveType>(r)), error: () => {} });
    this.loadMyLeaves();
  }

  ngOnDestroy(): void { this.stopTimer(); }

  private extractArray<T>(r: any): T[] {
    if (Array.isArray(r)) return r;
    if (Array.isArray(r?.data)) return r.data;
    if (Array.isArray(r?.data?.data)) return r.data.data;
    return [];
  }

  // ── Interns ───────────────────────────────────────────────────────────────
  private loadInterns(): void {
    this.userService.getAll().subscribe({
      next: (res: any) => {
        const data = this.extractArray<User>(res);
        this.interns.set(data.filter(u => (u.role ?? '').toLowerCase() === 'intern'));
      },
      error: () => { this.toast.error('Error', 'Failed to load interns'); this.interns.set([]); }
    });
  }

  selectIntern(intern: User): void {
    this.selectedIntern.set(intern);
    this.loadTasks(intern.id);
  }

  // ── Tasks ─────────────────────────────────────────────────────────────────
  loadTasks(internId: number): void {
    this.internService.getTasks(internId).subscribe({
      next: (res: any) => this.tasks.set(this.extractArray<InternTask>(res)),
      error: () => { this.toast.error('Failed to load tasks', ''); this.tasks.set([]); }
    });
  }

  openAssign(): void {
    if (!this.selectedIntern()) { this.toast.warning('Select an intern first', ''); return; }
    this.taskForm.reset();
    this.showModal.set(true);
  }

  assignTask(): void {
    if (this.taskForm.invalid) { this.taskForm.markAllAsTouched(); return; }
    const intern = this.selectedIntern();
    if (!intern) return;
    const v = this.taskForm.value;
    this.internService.createTask({ internId: intern.id, taskTitle: v.taskTitle!, description: v.description || '', dueDate: v.dueDate || undefined }).subscribe({
      next: () => { this.toast.success('Task Assigned', ''); this.showModal.set(false); this.loadTasks(intern.id); },
      error: () => this.toast.error('Failed', '')
    });
  }

  openEdit(task: InternTask): void {
    this.editTask.set(task);
    this.editTaskForm.patchValue({
      taskTitle:   task.taskTitle,
      description: task.description ?? '',
      dueDate:     task.dueDate ? task.dueDate.split('T')[0] : ''
    });
    this.showEditModal.set(true);
  }

  saveEdit(): void {
    const task = this.editTask();
    if (!task || this.editTaskForm.invalid) return;
    const v = this.editTaskForm.value;
    this.internService.updateTask(task.id, { taskTitle: v.taskTitle!, description: v.description || '', dueDate: v.dueDate || undefined }).subscribe({
      next: () => {
        this.toast.success('Task Updated', '');
        this.showEditModal.set(false);
        this.loadTasks(this.selectedIntern()!.id);
      },
      error: () => this.toast.error('Update Failed', '')
    });
  }

  confirmDeleteTask(task: InternTask): void {
    this.confirmTitle.set('Delete Task');
    this.confirmMessage.set(`Delete "${task.taskTitle}"? This cannot be undone.`);
    this.confirmAction = () => {
      this.internService.deleteTask(task.id).subscribe({
        next: () => { this.toast.success('Deleted', ''); this.loadTasks(this.selectedIntern()!.id); },
        error: () => this.toast.error('Delete failed', '')
      });
    };
    this.confirmVisible.set(true);
  }

  // ── Attendance ────────────────────────────────────────────────────────────
  private refreshToday(): void {
    const uid = this.auth.currentUser();
    if (!uid) return;
    this.attendanceService.getTodayStatus(uid).subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (d?.missedCheckout) {
          this.showMissedCheckoutModal.set(true);
        }
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
        this.toast.success('Checked In', `Welcome! Time: ${d?.checkIn}`);
        this.notificationService.pushLocal('Attendance', 'Mentor checked in successfully.');
        this.attLoading.set(false);
      },
      error: (err: any) => { this.toast.error('Check-In Failed', err?.error?.message ?? 'Please try again.'); this.attLoading.set(false); }
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
        this.stopTimer();
        this.toast.success('Checked Out', `Total: ${d?.totalHours ?? '—'}`);
        this.notificationService.pushLocal('Attendance', 'Mentor checked out successfully.');
        this.attLoading.set(false);
      },
      error: (err: any) => { this.toast.error('Check-Out Failed', err?.error?.message ?? 'Please try again.'); this.attLoading.set(false); }
    });
  }

  private startTimer(t: string): void {
    if (!t) return;
    const [h, m] = t.split(':').map(Number);
    const base = new Date(); base.setHours(h, m, 0, 0);
    this.stopTimer();
    this.timerInterval = setInterval(() => {
      const diff = Date.now() - base.getTime();
      const hh = Math.floor(diff / 3600000), mm = Math.floor((diff % 3600000) / 60000), ss = Math.floor((diff % 60000) / 1000);
      this.liveTimer.set(`${this.padNumber(hh)}:${this.padNumber(mm)}:${this.padNumber(ss)}`);
    }, 1000);
  }

  private stopTimer(): void { if (this.timerInterval) clearInterval(this.timerInterval); }
  private padNumber(n: number): string { return n < 10 ? '0' + n : '' + n; }

  // ── Helpers ───────────────────────────────────────────────────────────────
  statusClass(status: any): string {
    return status == 3 ? 'zbadge-approved' : status == 2 ? 'zbadge-info' : 'zbadge-pending';
  }
  statusText(status: any): string {
    return status == 1 ? 'Pending' : status == 2 ? 'In Progress' : 'Completed';
  }
  getStatusText(s: any)  { return s == 0 ? 'Pending' : s == 1 ? 'Approved' : 'Rejected'; }
  getStatusClass(s: any) { return s == 0 ? 'zbadge-pending' : s == 1 ? 'zbadge-approved' : 'zbadge-rejected'; }
  leaveDays(l: Leave): number {
    return Math.ceil((new Date(l.toDate).getTime() - new Date(l.fromDate).getTime()) / 86400000) + 1;
  }

  private loadMyLeaves(): void {
    const uid = this.auth.currentUser(); if (!uid) return;
    this.leaveService.getMyLeaves(uid).subscribe({
      next: (r: any) => this.myLeaves.set(this.extractArray<Leave>(r)),
      error: () => {}
    });
    this.leaveService.getLeaveBalance(uid).subscribe((r: any) => this.leaveBalance.set(r?.data ?? []));
  }

  addTimesheetForSelf() {
    if (this.addTimesheetForm.invalid) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    const v = this.addTimesheetForm.getRawValue();
    const proj = this.projects().find(p => p.id === +v.projectId!);
    const fmt = (t: string) => t?.length === 5 ? t + ':00' : t ?? '00:00:00';
    this.timesheetService.create(uid, {
      projectId: +v.projectId!, projectName: proj?.projectName ?? '',
      workDate: v.workDate!, startTime: fmt(v.startTime!), endTime: fmt(v.endTime!),
      breakTime: fmt(v.breakTime || '00:00'), taskDescription: v.taskDescription ?? ''
    }).subscribe({
      next: () => { this.toast.success('Timesheet Added', 'Your timesheet has been submitted.'); this.showAddTimesheetModal.set(false); this.addTimesheetForm.reset({ workDate: new Date().toISOString().split('T')[0], startTime: '09:00', endTime: '18:00', breakTime: '01:00' }); },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Could not create timesheet.')
    });
  }

  addLeaveForSelf() {
    if (this.addLeaveForm.invalid) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    const v = this.addLeaveForm.getRawValue();
    const selectedType = this.leaveTypes().find(lt => lt.id === +v.leaveTypeId!);
    this.leaveService.apply(uid, { leaveTypeId: +v.leaveTypeId!, fromDate: v.fromDate!, toDate: v.toDate!, reason: v.reason ?? '' }).subscribe({
      next: (res: any) => {
        const remaining = res?.data?.remainingLeaves ?? 0;
        this.showAddLeaveModal.set(false);
        this.addLeaveForm.reset();
        this.leaveBalanceResult.set({ leaveType: selectedType?.name ?? 'Leave', remaining, total: selectedType?.maxDaysPerYear ?? 0 });
        this.showLeaveBalanceModal.set(true);
        this.loadMyLeaves();
        this.leaveService.getLeaveBalance(uid).subscribe((r: any) => this.leaveBalance.set(r?.data ?? []));
      },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Could not apply leave.')
    });
  }

  setTab(t: MentorTab): void {
    this.activeTab.set(t);
    this.tabService.setTab(t);
    this.breadcrumbService.set([{ label: 'Mentor Dashboard' }, { label: this.tabs.find(x => x.key === t)?.label ?? '' }]);
  }
}
