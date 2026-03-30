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

export type ManagerTab = 'dashboard' | 'timesheets' | 'leaves' | 'team' | 'projects' | 'attendance' | 'mytimesheet' | 'profile';

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
    { key: 'dashboard'   as ManagerTab, label: 'Overview',      icon: '📊' },
    { key: 'timesheets'  as ManagerTab, label: 'Timesheets',    icon: '📋' },
    { key: 'mytimesheet' as ManagerTab, label: 'My Timesheet',  icon: '🕐' },
    { key: 'leaves'      as ManagerTab, label: 'Leaves',        icon: '🌴' },
    { key: 'team'        as ManagerTab, label: 'My Team',       icon: '👥' },
    { key: 'projects'    as ManagerTab, label: 'Projects',      icon: '🗂' },
    { key: 'attendance'  as ManagerTab, label: 'Attendance',    icon: '⏰' },
    { key: 'profile'     as ManagerTab, label: 'Profile',       icon: '👤' },
  ];

  allTimesheets = signal<Timesheet[]>([]);
  allLeaves     = signal<Leave[]>([]);
  allAttendance = signal<Attendance[]>([]);
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

  // Parse hoursWorked which comes as "HH:mm" string or number from backend
  private parseHours(val: any): number {
    if (!val) return 0;
    if (typeof val === 'number') return val;
    const s = String(val);
    if (s.includes(':')) { const [h, m] = s.split(':').map(Number); return h + (m || 0) / 60; }
    return parseFloat(s) || 0;
  }

  // Public wrapper for template use
  parseHoursPublic = (val: any) => this.parseHours(val);

  // Format decimal hours → "8h 30m" for display
  fmtH(decimal: number): string {
    if (!decimal) return '—';
    const h = Math.floor(decimal);
    const m = Math.round((decimal - h) * 60);
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  // ── Monthly grid helpers ──────────────────────────────────────────────────
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
    const all   = this.allTimesheets().filter(t =>
      this._inPeriod(t.date, 'monthly', this.tsPeriodOffset())
    );
    const empMap = new Map<string, Map<string, { hours: (number|null)[]; tsPerDay: (Timesheet|null)[]; projectName: string }>>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      const idx = dates.findIndex(md => md.getTime() === d.getTime());
      if (idx === -1) continue;
      const emp = ts.employeeName ?? 'Unknown';
      const prj = ts.projectName ?? '';
      if (!empMap.has(emp)) empMap.set(emp, new Map());
      const prjMap = empMap.get(emp)!;
      if (!prjMap.has(prj)) prjMap.set(prj, { hours: Array(dates.length).fill(null), tsPerDay: Array(dates.length).fill(null), projectName: prj });
      prjMap.get(prj)!.hours[idx]   = this.parseHours(ts.hoursWorked);
      prjMap.get(prj)!.tsPerDay[idx] = ts;
    }
    return [...empMap.entries()].map(([employeeName, prjMap]) => ({
      employeeName,
      rows: [...prjMap.values()]
    }));
  };

  weekDays = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];
  weekDates = () => {
    const now = new Date(); const offset = this.tsPeriodOffset();
    const monday = new Date(now); const day = now.getDay() || 7;
    monday.setDate(now.getDate() - day + 1 + offset * 7); monday.setHours(0,0,0,0);
    return Array.from({length:7}, (_, i) => { const d = new Date(monday); d.setDate(monday.getDate() + i); return d; });
  };

  // For the approval grid: only show days up to today on current week
  visibleWeekDates = () => {
    const all = this.weekDates();
    if (this.tsPeriodOffset() !== 0) return all;  // past weeks: show all 7
    const todayStr = this.toDateStr(new Date());
    return all.filter(d => this.toDateStr(d) <= todayStr);
  };

  weeklyRows = () => {
    const dates = this.visibleWeekDates(); const all = this.allTimesheets();
    const map = new Map<string, { ts: Timesheet; hours: (number|null)[]; tsPerDay: (Timesheet|null)[] }>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      const idx = dates.findIndex(wd => wd.getTime() === d.getTime());
      if (idx === -1) continue;
      const key = `${ts.employeeName}|${ts.projectName}`;
      if (!map.has(key)) map.set(key, { ts, hours: Array(7).fill(null), tsPerDay: Array(7).fill(null) });
      map.get(key)!.hours[idx] = this.parseHours(ts.hoursWorked);
      map.get(key)!.tsPerDay[idx] = ts;
    }
    return [...map.values()];
  };

  // Group weekly rows by employee for the grid view
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

  // Get the pending timesheet for a row (first pending tsPerDay entry)
  getPendingTs(row: { ts: Timesheet; tsPerDay: (Timesheet|null)[] }): Timesheet | null {
    return row.tsPerDay.find(t => t && Number(t.status) === 0) ?? null;
  }

  // Get overall status for a row (worst-case: if any pending, show pending)
  getRowStatus(row: { ts: Timesheet; tsPerDay: (Timesheet|null)[] }): number {
    const statuses = row.tsPerDay.filter(t => t !== null).map(t => Number(t!.status));
    if (statuses.includes(0)) return 0;
    if (statuses.includes(2)) return 2;
    return 1;
  }

  // Approve/reject ALL pending timesheets for an entire employee group (whole week)
  reviewAllForEmployee(group: { employeeName: string; rows: { ts: Timesheet; tsPerDay: (Timesheet|null)[] }[] }, approve: boolean) {
    const allPending = group.rows.flatMap(r => r.tsPerDay.filter(t => t && Number(t.status) === 0)) as Timesheet[];
    if (!allPending.length) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    this.confirm(
      approve ? 'Approve Week' : 'Reject Week',
      `${approve ? 'Approve' : 'Reject'} all ${allPending.length} pending timesheet(s) for "${group.employeeName}" this week?`,
      () => {
        let done = 0;
        const finish = () => {
          if (++done === allPending.length) {
            this.toast.success(approve ? 'Week Approved' : 'Week Rejected', `All timesheets for ${group.employeeName}.`);
            this.loadAll();
          }
        };
        for (const ts of allPending) {
          this.tsSvc.approveOrReject({ timesheetId: ts.id, approvedById: uid, isApproved: approve, managerComment: approve ? 'Approved by Manager' : 'Rejected by Manager' })
            .subscribe({ next: finish, error: finish });
        }
      },
      approve ? 'info' : 'warning'
    );
  }

  // Approve/reject all pending in a monthly employee group
  reviewMonthForEmployee(group: { employeeName: string; rows: { hours: (number|null)[]; tsPerDay: (Timesheet|null)[]; projectName: string }[] }, approve: boolean) {
    const allPending = group.rows.flatMap(r => r.tsPerDay.filter(t => t && Number(t.status) === 0)) as Timesheet[];
    if (!allPending.length) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    this.confirm(
      approve ? 'Approve Month' : 'Reject Month',
      `${approve ? 'Approve' : 'Reject'} all ${allPending.length} pending timesheet(s) for "${group.employeeName}" this month?`,
      () => {
        let done = 0;
        const finish = () => { if (++done === allPending.length) { this.toast.success(approve ? 'Approved' : 'Rejected', `All timesheets for ${group.employeeName}.`); this.loadAll(); } };
        for (const ts of allPending) this.tsSvc.approveOrReject({ timesheetId: ts.id, approvedById: uid, isApproved: approve, managerComment: approve ? 'Approved by Manager' : 'Rejected by Manager' }).subscribe({ next: finish, error: finish });
      },
      approve ? 'info' : 'warning'
    );
  }

  // Approve/reject all pending in a monthly project row
  reviewMonthRow(row: { hours: (number|null)[]; tsPerDay: (Timesheet|null)[]; projectName: string }, approve: boolean) {
    const pending = row.tsPerDay.filter(t => t && Number(t.status) === 0) as Timesheet[];
    if (!pending.length) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    this.confirm(
      approve ? 'Approve' : 'Reject',
      `${approve ? 'Approve' : 'Reject'} ${pending.length} timesheet(s) for "${row.projectName}"?`,
      () => {
        let done = 0;
        const finish = () => { if (++done === pending.length) { this.toast.success(approve ? 'Approved' : 'Rejected', `${pending.length} entries.`); this.loadAll(); } };
        for (const ts of pending) this.tsSvc.approveOrReject({ timesheetId: ts.id, approvedById: uid, isApproved: approve, managerComment: approve ? 'Approved by Manager' : 'Rejected by Manager' }).subscribe({ next: finish, error: finish });
      },
      approve ? 'info' : 'warning'
    );
  }

  // Approve/reject all pending timesheets in a row
  reviewRowTs(row: { ts: Timesheet; tsPerDay: (Timesheet|null)[] }, approve: boolean) {
    const pending = row.tsPerDay.filter(t => t && Number(t.status) === 0) as Timesheet[];
    if (!pending.length) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    this.confirm(
      approve ? 'Approve Timesheets' : 'Reject Timesheets',
      `${approve ? 'Approve' : 'Reject'} ${pending.length} timesheet(s) for "${row.ts.employeeName}" (${row.ts.projectName})?`,
      () => {
        let done = 0;
        const total = pending.length;
        const finish = () => {
          if (++done === total) {
            this.toast.success(approve ? 'Approved' : 'Rejected', `${total} timesheet(s) for ${row.ts.employeeName}.`);
            this.loadAll();
          }
        };
        for (const ts of pending) {
          this.tsSvc.approveOrReject({ timesheetId: ts.id, approvedById: uid, isApproved: approve, managerComment: approve ? 'Approved by Manager' : 'Rejected by Manager' })
            .subscribe({ next: finish, error: finish });
        }
      },
      approve ? 'info' : 'warning'
    );
  }

  // ── Attendance filter/page ────────────────────────────────────────────────
  attSearch  = signal('');
  attPage    = signal(1);
  attPS = 10;
  attViewMode     = signal<'all'|'weekly'|'monthly'>('all');
  attPeriodOffset = signal(0);

  attPeriodLabel = () => this._periodLabel(this.attViewMode(), this.attPeriodOffset());

  filteredAtt = computed(() => {
    let d = this.allAttendance();
    const q = this.attSearch().toLowerCase();
    if (q) d = d.filter(a => (a.employeeName ?? '').toLowerCase().includes(q));
    d = d.filter(a => this._inPeriod(a.date, this.attViewMode(), this.attPeriodOffset()));
    return d;
  });
  attTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredAtt().length / this.attPS)));
  pagedAtt      = computed(() => { const s = (this.attPage()-1)*this.attPS; return this.filteredAtt().slice(s, s+this.attPS); });

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
      .filter(t => Number(t.status) === 1)
      .reduce((s, t) => s + this.parseHours(t?.hoursWorked), 0);
    return total.toFixed(1);
  });

  // ── Project assign ────────────────────────────────────────────────────────
  selProjectId: number | null = null;
  selUserId:    number | null = null;

  // ── Grid note popover ─────────────────────────────────────────────────────
  activeGridNote = signal<string | null>(null);  // stores the note text to show
  toggleGridNote(note: string) {
    this.activeGridNote.set(this.activeGridNote() === note ? null : note);
  }
  closeGridNote() { this.activeGridNote.set(null); }

  // ── Confirm ───────────────────────────────────────────────────────────────
  cfgVisible = signal(false);
  cfgTitle   = signal('');
  cfgMsg     = signal('');
  cfgType    = signal<'danger'|'warning'|'info'>('info');
  private cfgAction: (() => void) | null = null;

  // ── Review modal (approve/reject with comment) ────────────────────────────
  reviewModal = signal(false);
  reviewComment = signal('');
  private reviewAction: ((comment: string) => void) | null = null;
  reviewIsApprove = signal(true);

  openReviewModal(approve: boolean, action: (comment: string) => void) {
    this.reviewIsApprove.set(approve);
    this.reviewComment.set('');
    this.reviewAction = action;
    this.reviewModal.set(true);
  }
  submitReview() {
    this.reviewAction?.(this.reviewComment());
    this.reviewModal.set(false);
    this.reviewAction = null;
  }
  cancelReview() { this.reviewModal.set(false); this.reviewAction = null; }

  // ── Own check-in/out ──────────────────────────────────────────────────────
  todayAtt   = signal<Attendance | null>(null);
  attLoading = signal(false);
  liveTimer  = signal('00:00:00');
  private timerInterval: any;
  // Local date string YYYY-MM-DD — avoids UTC shift from toISOString()
  toDateStr(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  }

  readonly todayDate = this.toDateStr(new Date());

  // ── Self timesheet / leave ────────────────────────────────────────────────
  showAddTimesheetModal = signal(false);
  showAddLeaveModal     = signal(false);
  leaveTypes            = signal<LeaveType[]>([]);

  // ── My Weekly Timesheet Grid ──────────────────────────────────────────────
  myTimesheets    = signal<any[]>([]);
  myGridRows      = signal<{ projectId: number; projectName: string; hours: Record<string,string>; notes: Record<string,string>; tsPerDay: Record<string,any> }[]>([]);
  myGridSaving    = signal(false);
  myWeekOffset    = signal(0);
  myShowPicker    = signal(false);
  myActiveNote    = signal<{ rowIdx: number; dateStr: string } | null>(null);
  myProjectAssign = signal<any[]>([]);

  myWeekDates = () => {
    const now = new Date(); const offset = this.myWeekOffset();
    const monday = new Date(now); const day = now.getDay() || 7;
    monday.setDate(now.getDate() - day + 1 + offset * 7); monday.setHours(0,0,0,0);
    return Array.from({length:7}, (_, i) => { const d = new Date(monday); d.setDate(monday.getDate() + i); return d; });
  };

  private myParseHoursVal(val: any): number {
    if (!val) return 0;
    if (typeof val === 'number') return val;
    const s = String(val);
    if (s.includes(':')) { const [h, m] = s.split(':').map(Number); return h + (m||0)/60; }
    return parseFloat(s) || 0;
  }
  private myToHHmm(dec: number): string {
    if (!dec) return '';
    const h = Math.floor(dec); const m = Math.round((dec-h)*60);
    return `${String(h).padStart(2,'0')}h:${String(m).padStart(2,'0')}m`;
  }
  private myParseCell(val: string): number {
    if (!val) return 0;
    const m = val.match(/^(\d+)h?:?(\d*)m?$/i);
    if (m) return parseInt(m[1]||'0') + parseInt(m[2]||'0')/60;
    return parseFloat(val) || 0;
  }

  myInitGrid() {
    const dates = this.myWeekDates(); const all = this.myTimesheets();
    const rowMap = new Map<string, any>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      if (!dates.some(wd => wd.getTime() === d.getTime())) continue;
      const key = `${ts.projectId ?? 0}|${ts.projectName}`;
      if (!rowMap.has(key)) rowMap.set(key, { projectId: ts.projectId??0, projectName: ts.projectName, hours:{}, notes:{}, tsPerDay:{} });
      const ds = this.toDateStr(d);
      rowMap.get(key).hours[ds] = this.myToHHmm(this.myParseHoursVal(ts.hoursWorked));
      rowMap.get(key).tsPerDay[ds] = ts;
      if (ts.description) rowMap.get(key).notes[ds] = ts.description;
    }
    this.myGridRows.set([...rowMap.values()]);
  }

  myAvailableProjects = () => this.myProjectAssign().filter(a =>
    !this.myGridRows().some(r => r.projectId === a.projectId && r.projectName === a.projectName)
  );

  myAddRow(asgn: any) {
    this.myGridRows.update(rows => [...rows, { projectId: asgn.projectId||0, projectName: asgn.projectName, hours:{}, notes:{}, tsPerDay:{} }]);
    this.myShowPicker.set(false);
  }
  myRemoveRow(i: number) { this.myGridRows.update(rows => rows.filter((_,idx)=>idx!==i)); }

  myGetVal(i: number, d: Date): string { return this.myGridRows()[i]?.hours[this.toDateStr(d)] ?? ''; }
  mySetVal(i: number, d: Date, val: string) {
    const ds = this.toDateStr(d);
    this.myGridRows.update(rows => rows.map((r,idx) => idx===i ? {...r, hours:{...r.hours,[ds]:val}} : r));
  }
  myGetNote(i: number, ds: string): string { return this.myGridRows()[i]?.notes?.[ds] ?? ''; }
  mySetNote(i: number, ds: string, val: string) {
    this.myGridRows.update(rows => rows.map((r,idx) => idx===i ? {...r, notes:{...r.notes,[ds]:val}} : r));
  }
  myToggleNote(i: number, ds: string) {
    const cur = this.myActiveNote();
    this.myActiveNote.set(cur?.rowIdx===i && cur?.dateStr===ds ? null : {rowIdx:i, dateStr:ds});
  }
  myCloseNote() { this.myActiveNote.set(null); }
  myIsNoteActive(i: number, ds: string) { const n=this.myActiveNote(); return n?.rowIdx===i && n?.dateStr===ds; }

  myRowTotal(i: number): string {
    const row = this.myGridRows()[i]; if (!row) return '0';
    const t = Object.values(row.hours).reduce((s,v)=>s+this.myParseCell(v as string),0);
    return t > 0 ? this.myToHHmm(t) : '0';
  }
  myDayTotal(d: Date): string {
    const ds = this.toDateStr(d);
    const t = this.myGridRows().reduce((s,r)=>s+this.myParseCell(r.hours[ds]??''),0);
    return t > 0 ? this.myToHHmm(t) : '0';
  }
  myGrandTotal(): string {
    const t = this.myGridRows().reduce((s,r)=>s+Object.values(r.hours).reduce((a,v)=>a+this.myParseCell(v as string),0),0);
    return `${t > 0 ? t.toFixed(1) : '0'} hrs`;
  }
  myGetCellStatus(i: number, ds: string): number | null {
    const ts = this.myGridRows()[i]?.tsPerDay?.[ds];
    return ts ? Number(ts.status) : null;
  }

  mySubmitGrid() {
    const uid = this.auth.currentUser(); if (!uid) return;
    this.doMySubmitGrid(uid);
  }

  private doMySubmitGrid(uid: number) {
    this.myGridSaving.set(true);
    const todayStr = this.toDateStr(new Date());
    const isCurrentWeek = this.myWeekOffset() === 0;
    const entries: any[] = [];
    for (const row of this.myGridRows()) {
      for (const [ds, val] of Object.entries(row.hours)) {
        const hours = this.myParseCell(val as string); if (!hours) continue;
        // On current week: skip future dates
        if (isCurrentWeek && ds > todayStr) continue;
        const existing = row.tsPerDay?.[ds];
        if (existing && Number(existing.status) === 1) continue;
        entries.push({ projectId: row.projectId, projectName: row.projectName, workDate: ds, hours, taskDescription: row.notes?.[ds] ?? '' });
      }
    }
    if (!entries.length) { this.toast.warning('Nothing to submit','Fill in at least one entry.'); this.myGridSaving.set(false); return; }
    this.tsSvc.submitWeekly(uid, { entries, submit: true }).subscribe({
      next: (res: any) => {
        const d = res?.data;
        const parts = [];
        if (d?.saved)           parts.push(`${d.saved} new`);
        if (d?.updated)         parts.push(`${d.updated} updated`);
        if (d?.alreadyApproved) parts.push(`${d.alreadyApproved} already approved`);
        this.toast.success('Submitted', `Sent for approval. ${parts.join(' · ')}`);
        if (isCurrentWeek && new Date().getDay() !== 0) {
          const today = new Date().getDay();
          const remaining = 7 - (today === 0 ? 7 : today);
          if (remaining > 0) {
            this.toast.warning(
              'Week Not Complete',
              `${remaining} day${remaining > 1 ? 's' : ''} remaining this week were not submitted. You can submit them when the time comes.`
            );
          }
        }
        this.tsSvc.getByUser(uid).subscribe((r: any) => { this.myTimesheets.set(this.toArr(r)); this.myInitGrid(); });
      },
      error: (e: any) => this.toast.error('Error', e?.error?.message ?? ''),
      complete: () => this.myGridSaving.set(false)
    });
  }

  loadMyTimesheet() {
    const uid = this.auth.currentUser(); if (!uid) return;
    this.tsSvc.getByUser(uid).subscribe((r: any) => { this.myTimesheets.set(this.toArr(r)); this.myInitGrid(); });
    this.prjSvc.getUserAssignments(uid, 1, 50).subscribe((r: any) => this.myProjectAssign.set(this.toArr(r)));
  }

  addTimesheetForm = this.fb.group({
    projectId:       ['', Validators.required],
    workDate:        [this.toDateStr(new Date()), Validators.required],
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
    this.attSvc.getAll().subscribe({
      next: (r: any) => this.allAttendance.set(this.toArr<Attendance>(r)),
      error: () => {}
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
    this.openReviewModal(approve, (comment) => {
      this.tsSvc.approveOrReject({
        timesheetId: ts.id, approvedById: uid,
        isApproved: approve,
        managerComment: comment || (approve ? 'Approved by Manager' : 'Rejected by Manager')
      }).subscribe({
        next: () => {
          this.toast.success(approve ? 'Approved' : 'Rejected', `Timesheet for ${ts.employeeName}.`);
          this.loadAll();
        },
        error: (e: any) => this.toast.error('Failed', e?.error?.message ?? 'Action failed.')
      });
    });
  }

  reviewLeave(l: Leave, approve: boolean) {
    const uid = this.auth.currentUser();
    if (!uid) { this.toast.error('Error','Invalid session.'); return; }
    this.openReviewModal(approve, (comment) => {
      this.lvSvc.approveOrReject({
        leaveId: l.id, approvedById: uid,
        isApproved: approve,
        managerComment: comment || (approve ? 'Approved' : 'Rejected')
      }).subscribe({
        next: () => {
          this.toast.success(approve ? 'Leave Approved' : 'Leave Rejected', `For ${l.employeeName}`);
          this.loadAll();
        },
        error: (e: any) => this.toast.error('Failed', e?.error?.message ?? '')
      });
    });
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

  leaveDays(l: Leave): number {
    return Math.ceil((new Date(l.toDate).getTime() - new Date(l.fromDate).getTime()) / 86400000) + 1;
  }

  Number = Number;

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
      next: () => { this.toast.success('Timesheet Added', 'Your timesheet has been submitted.'); this.showAddTimesheetModal.set(false); this.addTimesheetForm.reset({ workDate: this.toDateStr(new Date()), startTime: '09:00', endTime: '18:00', breakTime: '01:00' }); this.loadAll(); },
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
    if (t === 'mytimesheet') this.loadMyTimesheet();
  }

  pages(total: number) { return Array.from({length:total},(_,i)=>i+1); }
}
