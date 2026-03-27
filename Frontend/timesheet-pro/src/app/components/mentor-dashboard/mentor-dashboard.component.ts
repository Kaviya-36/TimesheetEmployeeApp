import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Attendance, InternTask, LeaveType, Project, User, UserProfile } from '../../models';
import { AttendanceService, InternService, LeaveService, ProjectService, TimesheetService, UserService } from '../../services/api.services';
import { AuthService } from '../../services/auth.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
import { NotificationService } from '../../services/notification.service';
import { TabService } from '../../services/tab.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmComponent } from '../confirm-dialog/confirm.component';
import { NavbarComponent } from '../navbar/navbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

export type MentorTab = 'interns' | 'profile';

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
  private  bc    = inject(BreadcrumbService);
  private  notif = inject(NotificationService);
  private  intSvc  = inject(InternService);
  private  usrSvc  = inject(UserService);
  private  attSvc  = inject(AttendanceService);
  private  tsSvc   = inject(TimesheetService);
  private  lvSvc   = inject(LeaveService);
  private  prjSvc  = inject(ProjectService);
  private  fb      = inject(FormBuilder);
  private  tabSvc  = inject(TabService);

  constructor() {
    effect(() => {
      const t = this.tabSvc.activeTab();
      if (t && t !== this.activeTab()) this.setTab(t as MentorTab);
    });
  }

  // ── Tabs ──────────────────────────────────────────────────────────────────
  activeTab = signal<MentorTab>('interns');
  readonly tabs: { key: MentorTab; label: string; icon: string }[] = [
    { key: 'interns', label: 'Interns', icon: '🎓' },
    { key: 'profile', label: 'Profile', icon: '👤' },
  ];

  // ── Data ──────────────────────────────────────────────────────────────────
  interns        = signal<User[]>([]);
  selectedIntern = signal<User | null>(null);
  tasks          = signal<InternTask[]>([]);
  userProfile    = signal<UserProfile | null>(null);
  projects       = signal<Project[]>([]);
  leaveTypes     = signal<LeaveType[]>([]);

  // ── Self timesheet / leave modals ─────────────────────────────────────────
  showAddTimesheetModal = signal(false);
  showAddLeaveModal     = signal(false);

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
  cfgVisible = signal(false);
  cfgTitle   = signal('');
  cfgMsg     = signal('');
  private cfgAction: (() => void) | null = null;

  onCfgOk()     { this.cfgAction?.(); this.cfgVisible.set(false); this.cfgAction = null; }
  onCfgCancel() { this.cfgVisible.set(false); this.cfgAction = null; }

  // ── Forms ─────────────────────────────────────────────────────────────────
  taskForm = this.fb.group({
    taskTitle:   ['', Validators.required],
    description: [''],
    dueDate:     ['']
  });

  editTaskForm = this.fb.group({
    taskTitle:   ['', Validators.required],
    description: [''],
    dueDate:     ['']
  });

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.bc.set([{ label: 'Mentor Dashboard' }, { label: 'Interns' }]);
    this.tabSvc.setTab('interns');
    this.loadInterns();
    this.refreshToday();
    this.usrSvc.getProfile().subscribe({ next: (r: any) => this.userProfile.set(r?.data ?? r), error: () => {} });
    this.prjSvc.getAll().subscribe({ next: (r: any) => this.projects.set(this.toArr<Project>(r)), error: () => {} });
    this.lvSvc.getLeaveTypes().subscribe({ next: (r: any) => this.leaveTypes.set(this.toArr<LeaveType>(r)), error: () => {} });
  }

  ngOnDestroy(): void { this.stopTimer(); }

  private toArr<T>(r: any): T[] {
    if (Array.isArray(r)) return r;
    if (Array.isArray(r?.data)) return r.data;
    if (Array.isArray(r?.data?.data)) return r.data.data;
    return [];
  }

  // ── Interns ───────────────────────────────────────────────────────────────
  private loadInterns(): void {
    this.usrSvc.getAll().subscribe({
      next: (res: any) => {
        const data = this.toArr<User>(res);
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
    this.intSvc.getTasks(internId).subscribe({
      next: (res: any) => this.tasks.set(this.toArr<InternTask>(res)),
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
    this.intSvc.createTask({ internId: intern.id, taskTitle: v.taskTitle!, description: v.description || '', dueDate: v.dueDate || undefined }).subscribe({
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
    this.intSvc.updateTask(task.id, { taskTitle: v.taskTitle!, description: v.description || '', dueDate: v.dueDate || undefined }).subscribe({
      next: () => {
        this.toast.success('Task Updated', '');
        this.showEditModal.set(false);
        this.loadTasks(this.selectedIntern()!.id);
      },
      error: () => this.toast.error('Update Failed', '')
    });
  }

  confirmDeleteTask(task: InternTask): void {
    this.cfgTitle.set('Delete Task');
    this.cfgMsg.set(`Delete "${task.taskTitle}"? This cannot be undone.`);
    this.cfgAction = () => {
      this.intSvc.deleteTask(task.id).subscribe({
        next: () => { this.toast.success('Deleted', ''); this.loadTasks(this.selectedIntern()!.id); },
        error: () => this.toast.error('Delete failed', '')
      });
    };
    this.cfgVisible.set(true);
  }

  // ── Attendance ────────────────────────────────────────────────────────────
  private refreshToday(): void {
    const uid = this.auth.currentUser();
    if (!uid) return;
    this.attSvc.getTodayStatus(uid).subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (d?.missedCheckout) {
          this.toast.warning('Missed Check-Out', 'You forgot to check out yesterday. Attendance auto-calculated as check-in + 8 hours.');
        }
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
      },
      error: () => this.todayAtt.set(null)
    });
  }

  checkIn(): void {
    if (this.todayAtt()?.checkIn) { this.toast.warning('Already checked in', ''); return; }
    this.attLoading.set(true);
    this.attSvc.checkIn().subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
        this.toast.success('Checked In', `Welcome! Time: ${d?.checkIn}`);
        this.notif.pushLocal('Attendance', 'Mentor checked in successfully.');
        this.attLoading.set(false);
      },
      error: (err: any) => { this.toast.error('Check-In Failed', err?.error?.message ?? 'Please try again.'); this.attLoading.set(false); }
    });
  }

  checkOut(): void {
    if (!this.todayAtt()?.checkIn) { this.toast.warning('Not checked in', ''); return; }
    if (this.todayAtt()?.checkOut) { this.toast.warning('Already checked out', ''); return; }
    this.attLoading.set(true);
    this.attSvc.checkOut().subscribe({
      next: (res: any) => {
        const d = res?.data ?? res;
        this.todayAtt.set(d);
        this.stopTimer();
        this.toast.success('Checked Out', `Total: ${d?.totalHours ?? '—'}`);
        this.notif.pushLocal('Attendance', 'Mentor checked out successfully.');
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
      this.liveTimer.set(`${this.pad(hh)}:${this.pad(mm)}:${this.pad(ss)}`);
    }, 1000);
  }

  private stopTimer(): void { if (this.timerInterval) clearInterval(this.timerInterval); }
  private pad(n: number): string { return n < 10 ? '0' + n : '' + n; }

  // ── Helpers ───────────────────────────────────────────────────────────────
  statusClass(status: any): string {
    return status == 2 ? 'zbadge-approved' : status == 1 ? 'zbadge-info' : 'zbadge-pending';
  }
  statusText(status: any): string {
    return status == 0 ? 'Pending' : status == 1 ? 'In Progress' : 'Completed';
  }

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
      next: () => { this.toast.success('Timesheet Added', 'Your timesheet has been submitted.'); this.showAddTimesheetModal.set(false); this.addTimesheetForm.reset({ workDate: new Date().toISOString().split('T')[0], startTime: '09:00', endTime: '18:00', breakTime: '01:00' }); },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Could not create timesheet.')
    });
  }

  addLeaveForSelf() {
    if (this.addLeaveForm.invalid) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    const v = this.addLeaveForm.getRawValue();
    this.lvSvc.apply(uid, { leaveTypeId: +v.leaveTypeId!, fromDate: v.fromDate!, toDate: v.toDate!, reason: v.reason ?? '' }).subscribe({
      next: () => { this.toast.success('Leave Applied', 'Your leave request has been submitted.'); this.showAddLeaveModal.set(false); this.addLeaveForm.reset(); },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Could not apply leave.')
    });
  }

  setTab(t: MentorTab): void {
    this.activeTab.set(t);
    this.tabSvc.setTab(t);
    this.bc.set([{ label: 'Mentor Dashboard' }, { label: this.tabs.find(x => x.key === t)?.label ?? '' }]);
  }
}
