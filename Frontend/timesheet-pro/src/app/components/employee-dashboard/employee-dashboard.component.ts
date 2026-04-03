import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  Attendance, DashboardSummary, Leave, LeaveType,
  ProjectAssignment, Timesheet, UserProfile
} from '../../models';
import {
  AnalyticsService, AttendanceService,
  LeaveService, ProjectService, TimesheetService, UserService
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
  imports: [DatePipe, ReactiveFormsModule, FormsModule, NavbarComponent, SidebarComponent, ConfirmComponent],
  templateUrl: './employee-dashboard.component.html',
  styleUrl:    './employee-dashboard.component.css'
})
export class EmployeeDashboardComponent implements OnInit, OnDestroy {
  // ── Core services ─────────────────────────────────────────────────────────
  readonly auth               = inject(AuthService);
  private  toast              = inject(ToastService);
  private  breadcrumbService  = inject(BreadcrumbService);
  private  notificationService = inject(NotificationService);
  private  tabService         = inject(TabService);
  private  formBuilder        = inject(FormBuilder);

  // ── Domain services ────────────────────────────────────────────────────────
  private  timesheetService  = inject(TimesheetService);
  private  attendanceService = inject(AttendanceService);
  private  leaveService      = inject(LeaveService);
  private  projectService    = inject(ProjectService);
  private  analyticsService  = inject(AnalyticsService);
  private  userService       = inject(UserService);

  constructor() {
    effect(() => {
      const t = this.tabService.activeTab();
      if (t && t !== this.activeTab()) this.setTab(t as EmployeeTab);
    });
  }

  activeTab = signal<EmployeeTab>('dashboard');
  readonly tabs: { key: EmployeeTab; label: string; icon: string }[] = [
    { key: 'dashboard',  label: 'Dashboard',  icon: '📊' },
    { key: 'timesheet',  label: 'Timesheets', icon: '📋' },
    { key: 'attendance', label: 'Attendance', icon: '⏰' },
    { key: 'leave',      label: 'Leave',      icon: '🌴' },
    { key: 'profile',    label: 'Profile',    icon: '👤' },
  ];

  // ── Data signals ──────────────────────────────────────────────────────────
  timesheets         = signal<Timesheet[]>([]);
  attendances        = signal<Attendance[]>([]);
  leaves             = signal<Leave[]>([]);
  projectsAssignment = signal<ProjectAssignment[]>([]);
  leaveTypes         = signal<LeaveType[]>([]);
  summary            = signal<DashboardSummary | null>(null);
  todayAtt           = signal<Attendance | null>(null);
  userProfile        = signal<UserProfile | null>(null);

  // ── Timesheet filter / sort / pagination signals ───────────────────────────
  timesheetSearch        = signal('');
  timesheetStatusFilter  = signal('all');
  timesheetSortColumn    = signal<'date' | 'hours' | 'project'>('date');
  timesheetSortDirection = signal<'asc' | 'desc'>('desc');
  timesheetPage          = signal(1);
  timesheetsPageSize     = 6;

  // ── Timesheet view-mode / period signals ──────────────────────────────────
  tsViewMode      = signal<'all'|'weekly'|'monthly'>('weekly');
  tsPeriodOffset  = signal(0);

  // ── Attendance view-mode / period signals ─────────────────────────────────
  attViewMode     = signal<'all'|'weekly'|'monthly'>('all');
  attPeriodOffset = signal(0);

  // ── Top submit section view mode (Week grid vs Month overview) ────────────
  submitViewMode    = signal<'weekly'|'monthly'>('weekly');
  submitMonthOffset = signal(0);

  tsPeriodLabel       = () => this._periodLabel(this.tsViewMode(),       this.tsPeriodOffset());
  attPeriodLabel      = () => this._periodLabel(this.attViewMode(),      this.attPeriodOffset());
  submitMonthLabel    = () => this._periodLabel('monthly', this.submitMonthOffset());

  private _periodLabel(mode: string, offset: number): string {
    if (mode === 'all') return '';
    const now = new Date();
    if (mode === 'weekly') {
      const s = new Date(now); s.setDate(now.getDate() - (now.getDay() || 7) + 1 + offset * 7);
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
      const s = new Date(now); s.setDate(now.getDate() - (now.getDay() || 7) + 1 + offset * 7); s.setHours(0,0,0,0);
      const e = new Date(s);   e.setDate(s.getDate() + 6); e.setHours(23,59,59,999);
      return d >= s && d <= e;
    }
    const ref = new Date(now.getFullYear(), now.getMonth() + offset, 1);
    return d.getFullYear() === ref.getFullYear() && d.getMonth() === ref.getMonth();
  }

  filteredTimesheets = computed(() => {
    let d = this.timesheets();
    const q = this.timesheetSearch().toLowerCase();
    if (q) d = d.filter(t => (t.projectName ?? '').toLowerCase().includes(q));
    if (this.timesheetStatusFilter() !== 'all') {
      const sv: Record<string, number> = { pending: 0, approved: 1, rejected: 2 };
      d = d.filter(t => Number(t.status) === sv[this.timesheetStatusFilter()]);
    }
    d = d.filter(t => this._inPeriod(t.date, this.tsViewMode(), this.tsPeriodOffset()));
    const col = this.timesheetSortColumn(); const dir = this.timesheetSortDirection();
    d = [...d].sort((a, b) => {
      const v = col === 'date'  ? new Date(a.date).getTime() - new Date(b.date).getTime()
              : col === 'hours' ? (a.hoursWorked ?? 0) - (b.hoursWorked ?? 0)
              : (a.projectName ?? '').localeCompare(b.projectName ?? '');
      return dir === 'asc' ? v : -v;
    });
    return d;
  });
  timesheetTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredTimesheets().length / this.timesheetsPageSize))
  );

  pagedTimesheets = computed(() => {
    const start = (this.timesheetPage() - 1) * this.timesheetsPageSize;
    return this.filteredTimesheets().slice(start, start + this.timesheetsPageSize);
  });

  // ── Attendance pagination ──────────────────────────────────────────────────
  attendancePage    = signal(1);
  attendancePageSize = 8;

  filteredAttendances = computed(() =>
    this.attendances().filter(a => this._inPeriod(a.date, this.attViewMode(), this.attPeriodOffset()))
  );

  pagedAttendances = computed(() => {
    const start = (this.attendancePage() - 1) * this.attendancePageSize;
    return this.filteredAttendances().slice(start, start + this.attendancePageSize);
  });

  attendanceTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredAttendances().length / this.attendancePageSize))
  );

  // ── Leave pagination ──────────────────────────────────────────────────────
  leavePage      = signal(1);
  leavesPageSize = 6;

  pagedLeaves = computed(() => {
    const start = (this.leavePage() - 1) * this.leavesPageSize;
    return this.leaves().slice(start, start + this.leavesPageSize);
  });

  leaveTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.leaves().length / this.leavesPageSize))
  );

  // ── Timesheet summary computed properties ─────────────────────────────────
  approvedTimesheetsCount = computed(() =>
    this.timesheets().filter(t => t.status === 1).length
  );

  pendingTimesheetsCount = computed(() =>
    this.timesheets().filter(t => t.status === 0).length
  );

  rejectedTimesheetsCount = computed(() =>
    this.timesheets().filter(t => t.status === 2).length
  );
  totalHoursLogged    = computed(() => {
    const total = (this.timesheets() ?? []).reduce((s, t) => s + this.parseHours(t?.hoursWorked), 0);
    return total.toFixed(1);
  });

  recentTimesheetsList = computed(() =>
    [...this.timesheets()]
      .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
      .slice(0, 5)
  );

  // ── Leave summary computed properties ─────────────────────────────────────
  pendingLeavesCount = computed(() =>
    this.leaves().filter(l => +l.status === 0).length
  );

  approvedLeavesCount = computed(() =>
    this.leaves().filter(l => +l.status === 1).length
  );

  thisWeekHoursTotal = computed(() => {
    const now = new Date();
    const mon = new Date(now); mon.setDate(now.getDate() - (now.getDay() || 7) + 1); mon.setHours(0,0,0,0);
    const sun = new Date(mon); sun.setDate(mon.getDate() + 6); sun.setHours(23,59,59,999);
    const total = this.timesheets()
      .filter(t => { const d = new Date(t.date); return d >= mon && d <= sun; })
      .reduce((s, t) => s + this.parseHours(t.hoursWorked), 0);
    return total.toFixed(1);
  });

  // ── Attendance summary computed properties ────────────────────────────────
  onTimeDaysCount = computed(() =>
    this.attendances().filter(a => !a.isLate).length
  );

  showMissedCheckoutModal = signal(false);
  showTsModal    = signal(false);
  showLeaveModal = signal(false);
  showEditModal  = signal(false);
  showEditLeaveModal = signal(false);
  showLeaveBalanceModal = signal(false);
  leaveBalanceResult = signal<{ leaveType: string; remaining: number; total: number } | null>(null);
  leaveBalance = signal<{ leaveType: string; total: number; used: number; remaining: number }[]>([]);
  editTs         = signal<Timesheet | null>(null);
  editLeave      = signal<Leave | null>(null);
  attLoading     = signal(false);

  // ── Confirm dialog signals ────────────────────────────────────────────────
  confirmVisible = signal(false);
  confirmTitle   = signal('');
  confirmMessage = signal('');
  private confirmAction: (() => void) | null = null;

  liveTimer = signal('00:00:00');
  private timerInterval: any;
  readonly todayDate = this.toDateStr(new Date());

  /** Converts a Date to a local YYYY-MM-DD string, avoiding UTC shift from toISOString(). */
  toDateStr(d: Date): string {
    const y   = d.getFullYear();
    const m   = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  // ── Weekly editable grid ──────────────────────────────────────────────────
  weekDays = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];
  gridRows = signal<{
    projectId: number;
    projectName: string;
    hours: Record<string, string>;
    notes: Record<string, string>;
    tsPerDay: Record<string, Timesheet>;   // dateStr → existing Timesheet
  }[]>([]);
  gridSaving = signal(false);
  showProjectPicker = signal(false);
  activeNote = signal<{ rowIdx: number; dateStr: string } | null>(null);

  weekDates = () => {
    const now = new Date(); const offset = this.tsPeriodOffset();
    const monday = new Date(now); monday.setDate(now.getDate() - (now.getDay() || 7) + 1 + offset * 7); monday.setHours(0,0,0,0);
    return Array.from({length:7}, (_, i) => { const d = new Date(monday); d.setDate(monday.getDate() + i); return d; });
  };

  // All calendar days for the selected month
  monthDates = () => {
    const now = new Date();
    const ref  = new Date(now.getFullYear(), now.getMonth() + this.tsPeriodOffset(), 1);
    const days = new Date(ref.getFullYear(), ref.getMonth() + 1, 0).getDate();
    return Array.from({ length: days }, (_, i) => new Date(ref.getFullYear(), ref.getMonth(), i + 1));
  };

  // History weekly grid dates (uses tsPeriodOffset, separate from submit grid)
  histWeekDates = () => {
    const now = new Date(); const offset = this.tsPeriodOffset();
    const monday = new Date(now); monday.setDate(now.getDate() - (now.getDay() || 7) + 1 + offset * 7); monday.setHours(0,0,0,0);
    return Array.from({length:7}, (_, i) => { const d = new Date(monday); d.setDate(monday.getDate() + i); return d; });
  };

  // Group timesheets by project for the history weekly read-only grid
  histWeeklyRows = () => {
    const dates = this.histWeekDates();
    const all   = this.timesheets().filter(t => this._inPeriod(t.date, 'weekly', this.tsPeriodOffset()));
    const map   = new Map<string, { projectName: string; hours: (number|null)[]; tsPerDay: (Timesheet|null)[] }>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      const idx = dates.findIndex(wd => wd.getTime() === d.getTime());
      if (idx === -1) continue;
      const key = `${ts.projectId}|${ts.projectName ?? ''}`;
      if (!map.has(key)) map.set(key, { projectName: ts.projectName ?? '', hours: Array(7).fill(null), tsPerDay: Array(7).fill(null) });
      map.get(key)!.hours[idx]   = this.parseHours(ts.hoursWorked);
      map.get(key)!.tsPerDay[idx] = ts;
    }
    return [...map.values()];
  };

  // Group timesheets by project for the monthly calendar grid
  monthlyRows = () => {
    const dates = this.monthDates();
    const all   = this.timesheets().filter(t => this._inPeriod(t.date, 'monthly', this.tsPeriodOffset()));
    const map   = new Map<string, { projectName: string; hours: (number|null)[]; tsPerDay: (Timesheet|null)[] }>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      const idx = dates.findIndex(md => md.getTime() === d.getTime());
      if (idx === -1) continue;
      const key = ts.projectName ?? '';
      if (!map.has(key)) map.set(key, { projectName: key, hours: Array(dates.length).fill(null), tsPerDay: Array(dates.length).fill(null) });
      map.get(key)!.hours[idx]   = this.parseHours(ts.hoursWorked);
      map.get(key)!.tsPerDay[idx] = ts;
    }
    return [...map.values()];
  };

  /** Formats a decimal hours value to a human-readable string, e.g. "8h 30m". */
  formatHours(decimal: number | null): string {
    if (!decimal) return '';
    const h = Math.floor(decimal);
    const m = Math.round((decimal - h) * 60);
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  // Month dates for the top submit section
  submitMonthDates = () => {
    const now = new Date();
    const ref  = new Date(now.getFullYear(), now.getMonth() + this.submitMonthOffset(), 1);
    const days = new Date(ref.getFullYear(), ref.getMonth() + 1, 0).getDate();
    return Array.from({ length: days }, (_, i) => new Date(ref.getFullYear(), ref.getMonth(), i + 1));
  };

  // Monthly rows for the top submit section (read-only overview)
  submitMonthlyRows = () => {
    const dates = this.submitMonthDates();
    const all   = this.timesheets().filter(t => this._inPeriod(t.date, 'monthly', this.submitMonthOffset()));
    const map   = new Map<string, { projectName: string; hours: (number|null)[]; tsPerDay: (Timesheet|null)[] }>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      const idx = dates.findIndex(md => md.getTime() === d.getTime());
      if (idx === -1) continue;
      const key = ts.projectName ?? '';
      if (!map.has(key)) map.set(key, { projectName: key, hours: Array(dates.length).fill(null), tsPerDay: Array(dates.length).fill(null) });
      map.get(key)!.hours[idx]    = this.parseHours(ts.hoursWorked);
      map.get(key)!.tsPerDay[idx] = ts;
    }
    return [...map.values()];
  };

  private parseHours(val: any): number {
    if (!val) return 0;
    if (typeof val === 'number') return val;
    const s = String(val);
    if (s.includes(':')) { const [h, m] = s.split(':').map(Number); return h + (m || 0) / 60; }
    return parseFloat(s) || 0;
  }

  private toHHmm(decimal: number): string {
    if (!decimal) return '';
    const h = Math.floor(decimal); const m = Math.round((decimal - h) * 60);
    return `${String(h).padStart(2,'0')}h:${String(m).padStart(2,'0')}m`;
  }

  private parseCell(val: string): number {
    if (!val) return 0;
    const m = val.match(/^(\d+)h?:?(\d*)m?$/i);
    if (m) return parseInt(m[1]||'0') + parseInt(m[2]||'0') / 60;
    return parseFloat(val) || 0;
  }

  /** Initialises the weekly editable grid from existing timesheet data. */
  initGrid() {
    const dates = this.weekDates(); const all = this.timesheets();
    const rowMap = new Map<string, { projectId: number; projectName: string; hours: Record<string, string>; notes: Record<string, string>; tsPerDay: Record<string, Timesheet> }>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      if (!dates.some(wd => wd.getTime() === d.getTime())) continue;
      const pid = ts.projectId ?? 0;
      // Key by projectName only to prevent duplicates when projectId differs
      const key = ts.projectName ?? String(pid);
      if (!key || key === '0') continue; // skip entries with no project name
      if (!rowMap.has(key)) rowMap.set(key, { projectId: pid, projectName: ts.projectName, hours: {}, notes: {}, tsPerDay: {} });
      const dateStr = this.toDateStr(d);
      rowMap.get(key)!.hours[dateStr] = this.toHHmm(this.parseHours(ts.hoursWorked));
      rowMap.get(key)!.tsPerDay[dateStr] = ts;
      if (ts.description) rowMap.get(key)!.notes[dateStr] = ts.description;
    }
    this.gridRows.set([...rowMap.values()]);
  }

  toStr(v: any): string { return String(v); }

  /** Returns projects not yet added to the current grid. */
  availableProjects = () => this.projectsAssignment().filter(a =>
    !this.gridRows().some(r => r.projectName === a.projectName)
  );

  /** Adds a new project row to the weekly grid. */
  addGridRow(asgn: ProjectAssignment) {
    if (this.gridRows().some(r => r.projectName === asgn.projectName)) return;
    this.gridRows.update(rows => [...rows, {
      projectId: asgn.projectId || 0,
      projectName: asgn.projectName,
      hours: {},
      notes: {},
      tsPerDay: {}
    }]);
    this.showProjectPicker.set(false);
  }

  /** Returns the approval status of an existing timesheet cell, or null if no entry exists. */
  getCellStatus(rowIdx: number, dateStr: string): number | null {
    const ts = this.gridRows()[rowIdx]?.tsPerDay?.[dateStr];
    return ts ? Number(ts.status) : null;
  }

  /** Returns true if the cell can be edited (no entry, or entry is pending/rejected). */
  isCellEditable(rowIdx: number, dateStr: string): boolean {
    const s = this.getCellStatus(rowIdx, dateStr);
    return s === null || s === 0 || s === 2;
  }

  /** Deletes a single pending timesheet cell entry after confirmation. */
  deleteCellTs(rowIdx: number, dateStr: string) {
    const ts = this.gridRows()[rowIdx]?.tsPerDay?.[dateStr];
    if (!ts) return;
    if (Number(ts.status) !== 0) { this.toast.warning('Cannot Delete', 'Only pending entries can be deleted.'); return; }
    this.confirm('Delete Entry', `Delete timesheet entry for "${this.gridRows()[rowIdx].projectName}" on ${dateStr}?`, () => {
      this.timesheetService.delete(ts.id).subscribe({
        next: () => {
          this.toast.success('Deleted', 'Entry removed.');
          const uid = this.auth.currentUser()!;
          this.timesheetService.getByUser(uid).subscribe((r: any) => { this.timesheets.set(this.extractArray<Timesheet>(r)); this.initGrid(); });
        },
        error: (e: any) => this.toast.error('Delete Failed', e?.error?.message ?? '')
      });
    });
  }

  /** Returns the note text for a given grid cell. */
  getCellNote(rowIdx: number, dateStr: string): string {
    return this.gridRows()[rowIdx]?.notes?.[dateStr] ?? '';
  }

  /** Updates the note text for a given grid cell. */
  setCellNote(rowIdx: number, dateStr: string, val: string) {
    this.gridRows.update(rows => rows.map((r, i) =>
      i === rowIdx ? { ...r, notes: { ...r.notes, [dateStr]: val } } : r
    ));
  }

  /** Toggles the note popover for a given grid cell. */
  toggleNote(rowIdx: number, dateStr: string) {
    const cur = this.activeNote();
    if (cur?.rowIdx === rowIdx && cur?.dateStr === dateStr) {
      this.activeNote.set(null);
    } else {
      this.activeNote.set({ rowIdx, dateStr });
    }
  }

  /** Closes the active note popover. */
  closeNote() { this.activeNote.set(null); }

  /** Returns true if the note popover is open for the given cell. */
  isNoteActive(rowIdx: number, dateStr: string): boolean {
    const n = this.activeNote();
    return n?.rowIdx === rowIdx && n?.dateStr === dateStr;
  }

  /** Saves the note for a cell — persists to backend if a pending entry exists, otherwise stores locally. */
  submitCellNote(rowIdx: number, dateStr: string) {
    const note = this.getCellNote(rowIdx, dateStr);
    const ts = this.gridRows()[rowIdx]?.tsPerDay?.[dateStr];
    if (ts && Number(ts.status) === 0) {
      this.timesheetService.update(ts.id, { taskDescription: note }).subscribe({
        next: () => {
          this.toast.success('Note saved', '');
          this.closeNote();
          const uid = this.auth.currentUser()!;
          this.timesheetService.getByUser(uid).subscribe((r: any) => { this.timesheets.set(this.extractArray<Timesheet>(r)); this.initGrid(); });
        },
        error: (e: any) => this.toast.error('Failed', e?.error?.message ?? '')
      });
    } else {
      this.toast.success('Note saved', 'Will be included when you submit.');
      this.closeNote();
    }
  }
  activeHistoryNote = signal<number | null>(null);  // ts.id
  historyNoteVal    = signal('');

  /** Opens the inline note editor for a timesheet history row. */
  openHistoryNote(ts: Timesheet) {
    if (Number(ts.status) !== 0) return;
    this.activeHistoryNote.set(ts.id);
    this.historyNoteVal.set(ts.description ?? '');
  }

  /** Closes the history note editor. */
  closeHistoryNote() { this.activeHistoryNote.set(null); }

  /** Persists the edited history note to the backend. */
  saveHistoryNote(ts: Timesheet) {
    const note = this.historyNoteVal();
    this.timesheetService.update(ts.id, { taskDescription: note }).subscribe({
      next: () => {
        this.toast.success('Note saved', '');
        this.closeHistoryNote();
        const uid = this.auth.currentUser()!;
        this.timesheetService.getByUser(uid).subscribe((r: any) => { this.timesheets.set(this.extractArray<Timesheet>(r)); this.initGrid(); });
      },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? '')
    });
  }

  /** Removes a row from the weekly grid by index. */
  removeGridRow(idx: number) { this.gridRows.update(rows => rows.filter((_, i) => i !== idx)); }

  /** Returns the display value for a grid cell. */
  getCellVal(rowIdx: number, date: Date): string {
    return this.gridRows()[rowIdx]?.hours[this.toDateStr(date)] ?? '';
  }

  /** Updates the hours value for a grid cell. */
  setCellVal(rowIdx: number, date: Date, val: string) {
    const dateStr = this.toDateStr(date);
    this.gridRows.update(rows => rows.map((r, i) => i === rowIdx ? { ...r, hours: { ...r.hours, [dateStr]: val } } : r));
  }

  /** Returns the total hours for a grid row as a formatted string. */
  rowTotal(rowIdx: number): string {
    const row = this.gridRows()[rowIdx]; if (!row) return '0';
    const total = Object.values(row.hours).reduce((s, v) => s + this.parseCell(v), 0);
    return total > 0 ? this.toHHmm(total) : '0';
  }

  /** Returns the total hours for a given day column as a formatted string. */
  dayTotal(date: Date): string {
    const dateStr = this.toDateStr(date);
    const total = this.gridRows().reduce((s, r) => s + this.parseCell(r.hours[dateStr] ?? ''), 0);
    return total > 0 ? this.toHHmm(total) : '0';
  }

  /** Returns the grand total hours across all rows and days. */
  grandTotal(): string {
    const total = this.gridRows().reduce((s, r) => s + Object.values(r.hours).reduce((a, v) => a + this.parseCell(v), 0), 0);
    return `${total > 0 ? total.toFixed(1) : '0'} hrs`;
  }

  /** Submits the weekly grid for approval. */
  saveGrid() {
    const uid = this.auth.currentUser(); if (!uid) return;
    this.doSubmitGrid(uid);
  }

  private doSubmitGrid(uid: number) {
    this.gridSaving.set(true);
    const todayStr = this.toDateStr(new Date());
    const isCurrentWeek = this.tsPeriodOffset() === 0;

    const entries: { projectId: number; projectName: string; workDate: string; hours: number; taskDescription?: string }[] = [];
    for (const row of this.gridRows()) {
      for (const [dateStr, val] of Object.entries(row.hours)) {
        const hours = this.parseCell(val); if (!hours) continue;
        // On current week: skip future dates
        if (isCurrentWeek && dateStr > todayStr) continue;
        // Skip approved cells
        const existing = row.tsPerDay?.[dateStr];
        if (existing && Number(existing.status) === 1) continue;
        entries.push({
          projectId:       row.projectId,
          projectName:     row.projectName,
          workDate:        dateStr,
          hours,
          taskDescription: row.notes?.[dateStr] ?? ''
        });
      }
    }

    if (entries.length === 0) {
      this.toast.warning('Nothing to submit', 'Fill in at least one time entry.');
      this.gridSaving.set(false);
      return;
    }

    // ── Frontend daily 12-hour cap check ──
    const byDate = new Map<string, number>();
    for (const e of entries) {
      byDate.set(e.workDate, (byDate.get(e.workDate) ?? 0) + e.hours);
    }
    for (const [date, total] of byDate) {
      if (total > 12) {
        this.toast.error(
          'Daily Limit Exceeded',
          `${new Date(date).toLocaleDateString('en-GB', { weekday: 'short', day: '2-digit', month: 'short' })}: ${total.toFixed(1)}h exceeds the 12h daily maximum.`
        );
        this.gridSaving.set(false);
        return;
      }
    }

    this.timesheetService.submitWeekly(uid, { entries, submit: true }).subscribe({
      next: (res: any) => {
        const d = res?.data;
        const parts = [];
        if (d?.saved)           parts.push(`${d.saved} new`);
        if (d?.updated)         parts.push(`${d.updated} updated`);
        if (d?.alreadyApproved) parts.push(`${d.alreadyApproved} already approved`);
        if (d?.skipped)         parts.push(`${d.skipped} skipped`);
        this.toast.success('Submitted', `Sent for approval. ${parts.join(' · ')}`);
        // Warn if current week has remaining days not yet submitted
        if (isCurrentWeek && new Date().getDay() !== 0) {
          const today = new Date().getDay(); // 1=Mon … 6=Sat, 0=Sun
          const remaining = 7 - (today === 0 ? 7 : today); // days left after today
          if (remaining > 0) {
            this.toast.warning(
              'Week Not Complete',
              `${remaining} day${remaining > 1 ? 's' : ''} remaining this week were not submitted. You can submit them when the time comes.`
            );
          }
        }
        const uid2 = this.auth.currentUser();
        if (uid2) this.timesheetService.getByUser(uid2).subscribe((r: any) => { this.timesheets.set(this.extractArray<Timesheet>(r)); this.initGrid(); });
      },
      error: (e: any) => this.toast.error('Error', e?.error?.message ?? 'Could not submit timesheet.'),
      complete: () => this.gridSaving.set(false)
    });
  }

  private addHours(start: string, hours: number): string {
    const [h, m] = start.split(':').map(Number);
    const total = h * 60 + m + Math.round(hours * 60);
    return `${String(Math.floor(total/60)%24).padStart(2,'0')}:${String(total%60).padStart(2,'0')}:00`;
  }

  tsForm = this.formBuilder.group({
    projectId: ['', Validators.required], projectName: [''],
    workDate: [this.todayDate, Validators.required],
    startTime: ['09:00', Validators.required], endTime: ['18:00', Validators.required],
    breakTime: ['01:00'], taskDescription: ['']
  });
  editTsForm = this.formBuilder.group({
    workDate: ['', Validators.required], startTime: ['', Validators.required],
    endTime: ['', Validators.required], breakTime: ['01:00'], taskDescription: ['']
  });
  leaveForm = this.formBuilder.group({
    leaveTypeId: ['', Validators.required], fromDate: ['', Validators.required],
    toDate: ['', Validators.required], reason: ['']
  });

  editLeaveForm = this.formBuilder.group({
    leaveTypeId: ['', Validators.required], fromDate: ['', Validators.required],
    toDate: ['', Validators.required], reason: ['']
  });

  ngOnInit(): void {
    this.breadcrumbService.set([{ label: 'My Workspace' }, { label: 'Dashboard' }]);
    this.tabService.setTab('dashboard');
    const uid = this.auth.currentUser(); if (!uid) return;
    this.loadAll(uid);
    this.refreshToday();
    this.analyticsService.getDashboard(uid).subscribe({ next: r => this.summary.set(r), error: () => {} });
    this.userService.getProfile().subscribe({ next: (r: any) => this.userProfile.set(r?.data ?? r), error: () => {} });
  }

  ngOnDestroy(): void { this.stopTimer(); }

  private extractArray<T>(r: any): T[] {
    if (Array.isArray(r)) return r;
    if (Array.isArray(r?.data)) return r.data;
    if (Array.isArray(r?.data?.data)) return r.data.data;
    return [];
  }

  private loadAll(uid: number) {
    this.timesheetService.getByUser(uid).subscribe((r: any) => { this.timesheets.set(this.extractArray<Timesheet>(r)); this.initGrid(); });
    this.attendanceService.getMyAttendance(uid).subscribe((r: any) => this.attendances.set(this.extractArray<Attendance>(r)));
    this.leaveService.getMyLeaves(uid).subscribe((r: any) => this.leaves.set(this.extractArray<Leave>(r)));
    this.leaveService.getLeaveTypes().subscribe((r: any) => this.leaveTypes.set(this.extractArray<LeaveType>(r)));
    this.leaveService.getLeaveBalance(uid).subscribe((r: any) => this.leaveBalance.set(r?.data ?? []));
    this.projectService.getUserAssignments(uid, 1, 50).subscribe((r: any) => this.projectsAssignment.set(this.extractArray<ProjectAssignment>(r)));
  }

  private refreshToday(): void {
    const uid = this.auth.currentUser(); if (!uid) return;
    this.attendanceService.getTodayStatus(uid).subscribe({
      next: (res: any) => {
        const d = res?.data ?? res; this.todayAtt.set(d);
        if (d?.missedCheckout) this.showMissedCheckoutModal.set(true);
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
      },
      error: () => this.todayAtt.set(null)
    });
  }

  /** Records today's check-in time. */
  checkIn(): void {
    if (this.todayAtt()?.checkIn) { this.toast.warning('Already checked in', ''); return; }
    this.attLoading.set(true);
    this.attendanceService.checkIn().subscribe({
      next: (res: any) => {
        const d = res?.data ?? res; this.todayAtt.set(d);
        if (d?.checkIn && !d?.checkOut) this.startTimer(d.checkIn);
        const uid = this.auth.currentUser();
        if (uid) this.attendanceService.getMyAttendance(uid).subscribe((r: any) => this.attendances.set(this.extractArray<Attendance>(r)));
        this.toast.success('Checked In', `Welcome! Time: ${d?.checkIn}`);
        this.notificationService.pushLocal('Attendance', 'You checked in successfully.');
        this.attLoading.set(false);
      },
      error: (err: any) => { this.toast.error('Check-In Failed', err?.error?.message ?? 'Please try again.'); this.attLoading.set(false); }
    });
  }

  /** Records today's check-out time. */
  checkOut(): void {
    if (!this.todayAtt()?.checkIn) { this.toast.warning('Not checked in', ''); return; }
    if (this.todayAtt()?.checkOut) { this.toast.warning('Already checked out', ''); return; }
    this.attLoading.set(true);
    this.attendanceService.checkOut().subscribe({
      next: (res: any) => {
        const d = res?.data ?? res; this.todayAtt.set(d); this.stopTimer();
        const uid = this.auth.currentUser();
        if (uid) this.attendanceService.getMyAttendance(uid).subscribe((r: any) => this.attendances.set(this.extractArray<Attendance>(r)));
        this.toast.success('Checked Out', `Total: ${d?.totalHours ?? '—'}`);
        this.notificationService.pushLocal('Attendance', 'You checked out successfully.');
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
      const hh = Math.floor(diff/3600000), mm = Math.floor((diff%3600000)/60000), ss = Math.floor((diff%60000)/1000);
      this.liveTimer.set(`${this.padNumber(hh)}:${this.padNumber(mm)}:${this.padNumber(ss)}`);
    }, 1000);
  }
  private stopTimer(): void { if (this.timerInterval) clearInterval(this.timerInterval); }
  private padNumber(n: number): string { return n < 10 ? '0' + n : '' + n; }

  /** Sorts the timesheet list by the given column, toggling direction if already active. */
  sortTimesheets(col: 'date' | 'hours' | 'project') {
    if (this.timesheetSortColumn() === col) this.timesheetSortDirection.update(d => d === 'asc' ? 'desc' : 'asc');
    else { this.timesheetSortColumn.set(col); this.timesheetSortDirection.set('asc'); }
    this.timesheetPage.set(1);
  }
  /** Returns the sort icon for a column header. */
  getSortIcon(active: boolean, dir: string) {
    return !active ? '⇅' : dir === 'asc' ? '↑' : '↓';
  }

  private confirm(title: string, msg: string, action: () => void) {
    this.confirmTitle.set(title); this.confirmMessage.set(msg);
    this.confirmAction = action; this.confirmVisible.set(true);
  }
  /** Handles the confirm dialog OK action. */
  onConfirmOk() {
    this.confirmAction?.();
    this.confirmVisible.set(false);
    this.confirmAction = null;
  }

  /** Handles the confirm dialog Cancel action. */
  onConfirmCancel() {
    this.confirmVisible.set(false);
    this.confirmAction = null;
  }

  /** Handles project selection change in the timesheet form. */
  onProjectChange(): void {
    const id = Number(this.tsForm.value.projectId);
    const p  = this.projectsAssignment().find(x => x.id === id);
    if (p) this.tsForm.patchValue({ projectName: p.projectName });
  }

  /** Submits a new timesheet entry from the form. */
  submitTimesheet(): void {
    if (this.tsForm.invalid) { this.tsForm.markAllAsTouched(); return; }
    const v = this.tsForm.value; const uid = this.auth.currentUser(); if (!uid) return;
    const asgn = this.projectsAssignment().find(p => p.id === +v.projectId!);
    if (!asgn) { this.toast.error('Invalid Project', 'Please select a valid project.'); return; }
    this.timesheetService.create(uid, {
      projectId: asgn.projectId, projectName: asgn.projectName, workDate: v.workDate!,
      startTime: this.formatTime(v.startTime!), endTime: this.formatTime(v.endTime!),
      breakTime: this.formatTime(v.breakTime || '00:00'), taskDescription: v.taskDescription ?? ''
    }).subscribe({
      next: () => {
        this.toast.success('Timesheet Submitted', 'Your timesheet is pending approval.');
        this.showTsModal.set(false);
        this.tsForm.reset({ workDate: this.todayDate, breakTime: '01:00' });
        this.timesheetService.getByUser(uid).subscribe((r: any) => { this.timesheets.set(this.extractArray<Timesheet>(r)); this.initGrid(); });
      },
      error: (e: any) => this.toast.error('Submission Failed', e?.error?.message ?? 'Please try again.')
    });
  }

  /** Opens the edit modal for an existing timesheet entry. */
  openEditTs(ts: Timesheet): void {
    if (ts.status !== 0) { this.toast.warning('Cannot Edit', 'Only Pending timesheets can be edited.'); return; }
    this.editTs.set(ts);
    this.editTsForm.patchValue({
      workDate: (ts.date ?? '').split('T')[0],
      startTime: (ts.startTime ?? '').substring(0, 5),
      endTime: (ts.endTime ?? '').substring(0, 5),
      breakTime: (ts.breakTime ?? '01:00').substring(0, 5),
      taskDescription: ts.description ?? ''
    });
    this.showEditModal.set(true);
  }

  /** Saves changes to an existing timesheet entry. */
  updateTimesheet(): void {
    const ts = this.editTs(); if (!ts || this.editTsForm.invalid) return;
    const v = this.editTsForm.value;
    this.timesheetService.update(ts.id, {
      workDate: v.workDate!, startTime: this.formatTime(v.startTime!),
      endTime: this.formatTime(v.endTime!), breakTime: this.formatTime(v.breakTime || '00:00'), taskDescription: v.taskDescription ?? ''
    }).subscribe({
      next: () => {
        this.toast.success('Updated', 'Timesheet updated successfully.');
        this.showEditModal.set(false);
        const uid = this.auth.currentUser()!;
        this.timesheetService.getByUser(uid).subscribe((r: any) => { this.timesheets.set(this.extractArray<Timesheet>(r)); this.initGrid(); });
      },
      error: (e: any) => this.toast.error('Update Failed', e?.error?.message ?? '')
    });
  }

  /** Prompts the user to confirm deletion of a timesheet entry. */
  confirmDeleteTs(ts: Timesheet): void {
    this.confirm('Delete Timesheet', `Delete timesheet for "${ts.projectName}"? This cannot be undone.`, () => {
      this.timesheetService.delete(ts.id).subscribe({
        next: () => {
          this.toast.success('Deleted', 'Timesheet removed.');
          const uid = this.auth.currentUser()!;
          this.timesheetService.getByUser(uid).subscribe((r: any) => { this.timesheets.set(this.extractArray<Timesheet>(r)); this.initGrid(); });
        },
        error: (e: any) => this.toast.error('Delete Failed', e?.error?.message ?? '')
      });
    });
  }

  /** Prompts the user to confirm deletion of a leave request. */
  confirmDeleteLeave(l: Leave): void {
    this.confirm('Delete Leave', 'Delete this leave request? This cannot be undone.', () => {
      this.leaveService.deleteLeave(l.id).subscribe({
        next: () => {
          this.toast.success('Deleted', 'Leave request removed.');
          const uid = this.auth.currentUser()!;
          this.leaveService.getMyLeaves(uid).subscribe((r: any) => this.leaves.set(this.extractArray<Leave>(r)));
        },
        error: (e: any) => this.toast.error('Delete Failed', e?.error?.message ?? '')
      });
    });
  }

  /** Opens the edit modal for an existing leave request. */
  openEditLeave(l: Leave): void {
    if (+l.status !== 0) { this.toast.warning('Cannot Edit', 'Only pending leave requests can be edited.'); return; }
    this.editLeave.set(l);
    // Find leaveTypeId by matching leaveType name
    const lt = this.leaveTypes().find(t => t.name === l.leaveType);
    this.editLeaveForm.patchValue({
      leaveTypeId: lt ? String(lt.id) : '',
      fromDate: (l.fromDate ?? '').split('T')[0],
      toDate:   (l.toDate   ?? '').split('T')[0],
      reason:   String(l.reason ?? '')
    });
    this.showEditLeaveModal.set(true);
  }

  /** Saves changes to an existing leave request. */
  updateLeave(): void {
    const l = this.editLeave();
    if (!l || this.editLeaveForm.invalid) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    const v = this.editLeaveForm.value;
    // Delete old + re-apply with new values
    this.leaveService.deleteLeave(l.id).subscribe({
      next: () => {
        this.leaveService.apply(uid, {
          leaveTypeId: +v.leaveTypeId!,
          fromDate: v.fromDate!,
          toDate:   v.toDate!,
          reason:   v.reason ?? ''
        }).subscribe({
          next: () => {
            this.toast.success('Leave Updated', 'Your leave request has been updated.');
            this.showEditLeaveModal.set(false);
            this.leaveService.getMyLeaves(uid).subscribe((r: any) => this.leaves.set(this.extractArray<Leave>(r)));
          },
          error: (e: any) => this.toast.error('Update Failed', e?.error?.message ?? '')
        });
      },
      error: (e: any) => this.toast.error('Update Failed', e?.error?.message ?? '')
    });
  }

  /** Validates that leave from/to dates are set and in the correct order. */
  validateLeaveDates(): boolean {
    const from = new Date(this.leaveForm.value.fromDate!);
    const to   = new Date(this.leaveForm.value.toDate!);
    const today = new Date(); today.setHours(0,0,0,0); from.setHours(0,0,0,0); to.setHours(0,0,0,0);
    if (from < today) { this.toast.error('Invalid Date', 'Past dates are not allowed.'); return false; }
    if (to < from)    { this.toast.error('Invalid Range', 'To date cannot be before From date.'); return false; }
    return true;
  }

  /** Submits a new leave request. */
  submitLeave(): void {
    if (this.leaveForm.invalid) { this.leaveForm.markAllAsTouched(); return; }
    if (!this.validateLeaveDates()) return;
    const v = this.leaveForm.value; const uid = this.auth.currentUser(); if (!uid) return;
    const selectedType = this.leaveTypes().find(lt => lt.id === +v.leaveTypeId!);
    this.leaveService.apply(uid, { leaveTypeId: +v.leaveTypeId!, fromDate: v.fromDate!, toDate: v.toDate!, reason: v.reason ?? '' }).subscribe({
      next: (res: any) => {
        const remaining = res?.data?.remainingLeaves ?? 0;
        const total = selectedType?.maxDaysPerYear ?? 0;
        this.showLeaveModal.set(false);
        this.leaveForm.reset();
        this.leaveBalanceResult.set({ leaveType: selectedType?.name ?? 'Leave', remaining, total });
        this.showLeaveBalanceModal.set(true);
        this.leaveService.getMyLeaves(uid).subscribe((r: any) => this.leaves.set(this.extractArray<Leave>(r)));
        this.leaveService.getLeaveBalance(uid).subscribe((r: any) => this.leaveBalance.set(r?.data ?? []));
      },
      error: (e: any) => this.toast.error('Application Failed', e?.error?.message ?? '')
    });
  }

  private formatTime(t: string): string { return t?.length === 5 ? t + ':00' : t ?? '00:00:00'; }

  /** Returns the display label for a status value. */
  getStatusText(s: any) {
    return s == 0 ? 'Pending' : s == 1 ? 'Approved' : 'Rejected';
  }

  /** Returns the CSS badge class for a status value. */
  getStatusClass(s: any) {
    return s == 0 ? 'zbadge-pending' : s == 1 ? 'zbadge-approved' : 'zbadge-rejected';
  }

  /** Returns the number of calendar days covered by a leave request. */
  leaveDays(l: Leave): number {
    return Math.ceil((new Date(l.toDate).getTime() - new Date(l.fromDate).getTime()) / 86400000) + 1;
  }

  /** Switches the active dashboard tab and updates the tab service. */
  setTab(tab: EmployeeTab): void {
    this.activeTab.set(tab);
    this.tabService.setTab(tab);
    this.breadcrumbService.set([{ label: 'My Workspace' }, { label: this.tabs.find(t => t.key === tab)?.label ?? '' }]);
    this.timesheetPage.set(1); this.attendancePage.set(1); this.leavePage.set(1);
  }

  /** Returns an array of page numbers for a given total page count. */
  pages(total: number): number[] { return Array.from({ length: total }, (_, i) => i + 1); }
}
