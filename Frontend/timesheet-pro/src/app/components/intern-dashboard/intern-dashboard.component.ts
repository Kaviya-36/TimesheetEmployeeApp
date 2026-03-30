import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Attendance, DashboardSummary, InternTask, Leave, LeaveType, ProjectAssignment, Timesheet, UserProfile } from '../../models';
import { AnalyticsService, AttendanceService, InternService, LeaveService, ProjectService, TimesheetService, UserService } from '../../services/api.services';
import { AuthService } from '../../services/auth.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
import { TabService } from '../../services/tab.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmComponent } from '../confirm-dialog/confirm.component';
import { NavbarComponent } from '../navbar/navbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

export type InternTab = 'dashboard' | 'timesheet' | 'attendance' | 'leave' | 'tasks' | 'profile';

@Component({
  selector: 'app-intern-dashboard',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, FormsModule, NavbarComponent, SidebarComponent, ConfirmComponent],
  templateUrl: './intern-dashboard.component.html',
  styleUrl:    './intern-dashboard.component.css'
})
export class InternDashboardComponent implements OnInit, OnDestroy {
  readonly auth  = inject(AuthService);
  private  toast = inject(ToastService);
  private  bc    = inject(BreadcrumbService);
  private  tsSvc  = inject(TimesheetService);
  private  attSvc = inject(AttendanceService);
  private  lvSvc  = inject(LeaveService);
  private  prjSvc = inject(ProjectService);
  private  anlSvc = inject(AnalyticsService);
  private  intSvc = inject(InternService);
  private  usrSvc = inject(UserService);
  private  fb     = inject(FormBuilder);
  private  tabSvc = inject(TabService);

  constructor() {
    effect(() => {
      const t = this.tabSvc.activeTab();
      if (t && t !== this.activeTab()) this.setTab(t as InternTab);
    });
  }

  activeTab = signal<InternTab>('dashboard');
  readonly tabs: { key: InternTab; label: string; icon: string }[] = [
    { key:'dashboard', label:'Dashboard', icon:'📊' },
    { key:'timesheet', label:'Timesheets', icon:'📋' },
    { key:'attendance', label:'Attendance', icon:'⏰' },
    { key:'leave', label:'Leave', icon:'🌴' },
    { key:'tasks', label:'Tasks', icon:'📝' },
    { key:'profile', label:'Profile', icon:'👤' },
  ];

  timesheets  = signal<Timesheet[]>([]);
  attendances = signal<Attendance[]>([]);
  leaves      = signal<Leave[]>([]);
  leaveTypes  = signal<LeaveType[]>([]);
  projects    = signal<ProjectAssignment[]>([]);
  tasks       = signal<InternTask[]>([]);
  summary     = signal<DashboardSummary|null>(null);
  todayAtt    = signal<Attendance|null>(null);
  attLoading  = signal(false);
  userProfile = signal<UserProfile | null>(null);

  tsPage = signal(1); tsPS = 6;
  tsViewMode      = signal<'all'|'weekly'|'monthly'|'pending'|'approved'>('all');
  tsPeriodOffset  = signal(0);
  attViewMode     = signal<'all'|'weekly'|'monthly'>('all');
  attPeriodOffset = signal(0);
  tsPeriodLabel  = () => this._periodLabel(this.tsViewMode(),  this.tsPeriodOffset());
  attPeriodLabel = () => this._periodLabel(this.attViewMode(), this.attPeriodOffset());

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

  filteredTs  = computed(() => {
    let d = this.timesheets();
    if (this.tsViewMode() === 'pending')  d = d.filter(t => Number(t.status) === 0);
    if (this.tsViewMode() === 'approved') d = d.filter(t => Number(t.status) === 1);
    return d;
  });
  pagedTs     = computed(()=>{const s=(this.tsPage()-1)*this.tsPS; return this.filteredTs().slice(s,s+this.tsPS);});
  tsTotalPages = computed(()=>Math.max(1,Math.ceil(this.filteredTs().length/this.tsPS)));

  weekDays = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];

  toDateStr(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
  readonly todayDate = this.toDateStr(new Date());

  weekDates = () => {
    const now = new Date(); const offset = this.tsPeriodOffset();
    const monday = new Date(now); const day = now.getDay() || 7;
    monday.setDate(now.getDate() - day + 1 + offset * 7); monday.setHours(0,0,0,0);
    return Array.from({length:7}, (_, i) => { const d = new Date(monday); d.setDate(monday.getDate() + i); return d; });
  };

  // ── Weekly grid ───────────────────────────────────────────────────────────
  gridRows   = signal<{ projectId: number; projectName: string; hours: Record<string,string>; notes: Record<string,string>; tsPerDay: Record<string,Timesheet> }[]>([]);
  gridSaving = signal(false);
  showProjectPicker = signal(false);
  activeNote = signal<{ rowIdx: number; dateStr: string } | null>(null);

  private parseHoursVal(val: any): number {
    if (!val) return 0;
    if (typeof val === 'number') return val;
    const s = String(val);
    if (s.includes(':')) { const [h, m] = s.split(':').map(Number); return h + (m||0)/60; }
    return parseFloat(s) || 0;
  }
  private toHHmm(dec: number): string {
    if (!dec) return '';
    const h = Math.floor(dec); const m = Math.round((dec-h)*60);
    return `${String(h).padStart(2,'0')}h:${String(m).padStart(2,'0')}m`;
  }
  private parseCell(val: string): number {
    if (!val) return 0;
    const m = val.match(/^(\d+)h?:?(\d*)m?$/i);
    if (m) return parseInt(m[1]||'0') + parseInt(m[2]||'0')/60;
    return parseFloat(val) || 0;
  }

  initGrid() {
    const dates = this.weekDates(); const all = this.timesheets();
    const rowMap = new Map<string, { projectId: number; projectName: string; hours: Record<string,string>; notes: Record<string,string>; tsPerDay: Record<string,Timesheet> }>();
    for (const ts of all) {
      const d = new Date(ts.date); d.setHours(0,0,0,0);
      if (!dates.some(wd => wd.getTime() === d.getTime())) continue;
      const key = `${ts.projectId??0}|${ts.projectName}`;
      if (!rowMap.has(key)) rowMap.set(key, { projectId: ts.projectId??0, projectName: ts.projectName, hours:{}, notes:{}, tsPerDay:{} });
      const ds = this.toDateStr(d);
      rowMap.get(key)!.hours[ds] = this.toHHmm(this.parseHoursVal(ts.hoursWorked));
      rowMap.get(key)!.tsPerDay[ds] = ts;
      if (ts.description) rowMap.get(key)!.notes[ds] = ts.description;
    }
    this.gridRows.set([...rowMap.values()]);
  }

  // Intern uses tasks as rows — each task maps to the first assigned project
  taskLabel(t: InternTask): string { return (t as any).taskTitle || (t as any).title || (t as any).Title || `Task #${t.id}`; }

  availableTasks = () => this.tasks().filter(t =>
    !this.gridRows().some(r => r.projectId === t.id)
  );

  addTaskRow(task: InternTask) {
    this.gridRows.update(rows => [...rows, {
      projectId:   task.id,
      projectName: this.taskLabel(task),
      hours: {}, notes: {}, tsPerDay: {}
    }]);
    this.showProjectPicker.set(false);
  }

  removeGridRow(i: number) { this.gridRows.update(rows => rows.filter((_,idx)=>idx!==i)); }

  private resolveProjectId(): number { return this.projects()[0]?.projectId ?? 0; }
  private resolveProjectName(): string { return this.projects()[0]?.projectName ?? ''; }

  getCellVal(i: number, d: Date): string { return this.gridRows()[i]?.hours[this.toDateStr(d)] ?? ''; }
  setCellVal(i: number, d: Date, val: string) {
    const ds = this.toDateStr(d);
    this.gridRows.update(rows => rows.map((r,idx) => idx===i ? {...r, hours:{...r.hours,[ds]:val}} : r));
  }
  getCellNote(i: number, ds: string): string { return this.gridRows()[i]?.notes?.[ds] ?? ''; }
  setCellNote(i: number, ds: string, val: string) {
    this.gridRows.update(rows => rows.map((r,idx) => idx===i ? {...r, notes:{...r.notes,[ds]:val}} : r));
  }
  toggleNote(i: number, ds: string) {
    const cur = this.activeNote();
    this.activeNote.set(cur?.rowIdx===i && cur?.dateStr===ds ? null : {rowIdx:i, dateStr:ds});
  }
  closeNote() { this.activeNote.set(null); }
  isNoteActive(i: number, ds: string) { const n=this.activeNote(); return n?.rowIdx===i && n?.dateStr===ds; }

  getCellStatus(i: number, ds: string): number | null {
    const ts = this.gridRows()[i]?.tsPerDay?.[ds];
    return ts ? Number(ts.status) : null;
  }

  rowTotal(i: number): string {
    const row = this.gridRows()[i]; if (!row) return '0';
    const t = Object.values(row.hours).reduce((s,v)=>s+this.parseCell(v as string),0);
    return t > 0 ? this.toHHmm(t) : '0';
  }
  dayTotal(d: Date): string {
    const ds = this.toDateStr(d);
    const t = this.gridRows().reduce((s,r)=>s+this.parseCell(r.hours[ds]??''),0);
    return t > 0 ? this.toHHmm(t) : '0';
  }
  grandTotal(): string {
    const t = this.gridRows().reduce((s,r)=>s+Object.values(r.hours).reduce((a,v)=>a+this.parseCell(v as string),0),0);
    return `${t > 0 ? t.toFixed(1) : '0'} hrs`;
  }

  submitGrid() {
    const uid = this.auth.currentUser(); if (!uid) return;
    this.gridSaving.set(true);
    const todayStr = this.toDateStr(new Date());
    const isCurrentWeek = this.tsPeriodOffset() === 0;
    const entries: any[] = [];
    for (const row of this.gridRows()) {
      for (const [ds, val] of Object.entries(row.hours)) {
        const hours = this.parseCell(val as string); if (!hours) continue;
        if (isCurrentWeek && ds > todayStr) continue;
        const existing = row.tsPerDay?.[ds];
        if (existing && Number(existing.status) === 1) continue;
        entries.push({ projectId: this.resolveProjectId(), projectName: this.resolveProjectName(), workDate: ds, hours, taskDescription: row.projectName + (row.notes?.[ds] ? ': ' + row.notes[ds] : '') });
      }
    }
    if (!entries.length) { this.toast.warning('Nothing to submit','Fill in at least one entry.'); this.gridSaving.set(false); return; }
    this.tsSvc.submitWeekly(uid, { entries, submit: true }).subscribe({
      next: (res: any) => {
        const d = res?.data;
        const parts = [];
        if (d?.saved)           parts.push(`${d.saved} new`);
        if (d?.updated)         parts.push(`${d.updated} updated`);
        if (d?.alreadyApproved) parts.push(`${d.alreadyApproved} already approved`);
        this.toast.success('Submitted', `Sent for approval. ${parts.join(' · ')}`);
        if (isCurrentWeek && new Date().getDay() !== 0) {
          const remaining = 7 - (new Date().getDay() === 0 ? 7 : new Date().getDay());
          if (remaining > 0) this.toast.warning('Week Not Complete', `${remaining} day${remaining>1?'s':''} remaining were not submitted.`);
        }
        this.tsSvc.getByUser(uid).subscribe(r => { this.timesheets.set(this.toArr<Timesheet>(r)); this.initGrid(); });
      },
      error: (e: any) => this.toast.error('Error', e?.error?.message ?? ''),
      complete: () => this.gridSaving.set(false)
    });
  }

  weeklyRows = () => {
    const dates = this.weekDates(); const all = this.timesheets();
    const map = new Map<string, { ts: any; hours: (number|null)[] }>();
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

  attPage = signal(1); attPS = 8;
  filteredAtt  = computed(() => this.attendances().filter(a => this._inPeriod(a.date, this.attViewMode(), this.attPeriodOffset())));
  pagedAtt     = computed(()=>{const s=(this.attPage()-1)*this.attPS; return this.filteredAtt().slice(s,s+this.attPS);});
  attTotalPages = computed(()=>Math.max(1,Math.ceil(this.filteredAtt().length/this.attPS)));

  approvedTs = computed(()=>this.timesheets().filter(t=>Number(t.status)===1).length);
  pendingTs  = computed(()=>this.timesheets().filter(t=>Number(t.status)===0).length);
  totalHours = computed(() => {
    const total = this.timesheets().reduce((s, t) => {
      const h = Number(t?.hoursWorked);
      return s + (isNaN(h) ? 0 : h);
    }, 0);
    return total.toFixed(1);
  });

  showTsModal    = signal(false);
  showLeaveModal = signal(false);
  cfgVisible = signal(false); cfgTitle = signal(''); cfgMsg = signal('');
  private cfgAction:(()=>void)|null=null;

  liveTimer = signal('00:00:00');
  private timerInterval: any;

  tsForm = this.fb.group({
    projectId:       [''],
    taskId:          ['', Validators.required],
    workDate:        [this.todayDate, Validators.required],
    startTime:       ['09:00', Validators.required],
    endTime:         ['18:00', Validators.required],
    breakTime:       ['01:00'],
    taskDescription: ['']
  });
  leaveForm = this.fb.group({ leaveTypeId:['',Validators.required], fromDate:['',Validators.required], toDate:['',Validators.required], reason:[''] });

  ngOnInit() {
    this.bc.set([{label:'Intern Hub'}, {label:'Dashboard'}]);
    this.tabSvc.setTab('dashboard');
    const uid = this.auth.currentUser(); if(!uid) return;
    this.loadAll(uid); this.refreshToday();
    this.anlSvc.getDashboard(uid).subscribe({next:r=>this.summary.set(r),error:()=>{}});
    this.usrSvc.getProfile().subscribe({ next:(r:any)=>this.userProfile.set(r?.data??r), error:()=>{} });
  }
  ngOnDestroy() { if(this.timerInterval) clearInterval(this.timerInterval); }

  private toArr<T>(r:any):T[] { if(Array.isArray(r))return r; if(Array.isArray(r?.data))return r.data; if(Array.isArray(r?.data?.data))return r.data.data; return []; }

  loadAll(uid:number) {
    this.tsSvc.getByUser(uid).subscribe(r=>{ this.timesheets.set(this.toArr<Timesheet>(r)); this.initGrid(); });
    this.attSvc.getMyAttendance(uid).subscribe(r=>this.attendances.set(this.toArr<Attendance>(r)));
    this.lvSvc.getMyLeaves(uid).subscribe(r=>this.leaves.set(this.toArr<Leave>(r)));
    this.lvSvc.getLeaveTypes().subscribe(r=>this.leaveTypes.set(this.toArr<LeaveType>(r)));
    this.prjSvc.getUserAssignments(uid,1,50).subscribe(r=>this.projects.set(this.toArr<ProjectAssignment>(r)));
    this.intSvc.getTasks(uid).subscribe(r => {
      this.tasks.set(this.toArr<InternTask>(r));
    });
  }

  private refreshToday() {
    const uid=this.auth.currentUser(); if(!uid) return;
    this.attSvc.getTodayStatus(uid).subscribe({
      next:(res:any)=>{const d=res?.data??res;this.todayAtt.set(d);if(d?.missedCheckout)this.toast.warning('Missed Check-Out','You forgot to check out yesterday. Attendance auto-calculated as check-in + 8 hours.');if(d?.checkIn&&!d?.checkOut)this.startTimer(d.checkIn);},
      error:()=>this.todayAtt.set(null)
    });
  }

  checkIn() {
    if(this.todayAtt()?.checkIn){this.toast.warning('Already checked in','');return;}
    this.attLoading.set(true);
    this.attSvc.checkIn().subscribe({
      next:(res:any)=>{const d=res?.data??res;this.todayAtt.set(d);if(d?.checkIn&&!d?.checkOut)this.startTimer(d.checkIn);this.toast.success('Checked In','');this.attLoading.set(false);},
      error:(e:any)=>{this.toast.error('Failed',e?.error?.message??'');this.attLoading.set(false);}
    });
  }
  checkOut() {
    if(!this.todayAtt()?.checkIn){this.toast.warning('Not checked in','');return;}
    if(this.todayAtt()?.checkOut){this.toast.warning('Already checked out','');return;}
    this.attLoading.set(true);
    this.attSvc.checkOut().subscribe({
      next:(res:any)=>{const d=res?.data??res;this.todayAtt.set(d);if(this.timerInterval)clearInterval(this.timerInterval);this.toast.success('Checked Out',`Total: ${d?.totalHours??'—'}`);this.attLoading.set(false);},
      error:(e:any)=>{this.toast.error('Failed',e?.error?.message??'');this.attLoading.set(false);}
    });
  }

  private startTimer(t:string){
    const [h,m]=t.split(':').map(Number);const base=new Date();base.setHours(h,m,0,0);
    if(this.timerInterval)clearInterval(this.timerInterval);
    this.timerInterval=setInterval(()=>{const diff=Date.now()-base.getTime();const hh=Math.floor(diff/3600000),mm=Math.floor((diff%3600000)/60000),ss=Math.floor((diff%60000)/1000);this.liveTimer.set(`${this.p(hh)}:${this.p(mm)}:${this.p(ss)}`);},1000);
  }
  private p(n:number){return n<10?'0'+n:''+n;}

  onTaskSelect(): void {
    const id = +this.tsForm.value.taskId!;
    const t  = this.tasks().find(x => x.id === id);
    if (!t) return;
    const raw = t as any;
    // API returns content in 'description', title is empty
    this.tsForm.patchValue({ taskDescription: raw.description || raw.taskTitle || raw.title || '' });
    if (this.projects().length > 0) {
      this.tsForm.patchValue({ projectId: String(this.projects()[0].id) });
    }
  }

  submitTimesheet() {
    if (this.tsForm.invalid) { this.tsForm.markAllAsTouched(); return; }
    const v   = this.tsForm.value;
    const uid = this.auth.currentUser(); if (!uid) return;

    // resolve project — use auto-selected or first available
    const pid  = v.projectId ? +v.projectId : (this.projects()[0]?.id ?? 0);
    const asgn = this.projects().find(p => p.id === pid) ?? this.projects()[0];
    if (!asgn) { this.toast.error('No Project', 'No project assigned. Contact your mentor.'); return; }

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
        this.toast.success('Submitted', 'Timesheet pending approval.');
        this.showTsModal.set(false);
        this.tsForm.reset({ workDate: this.todayDate, breakTime: '01:00' });
        this.tsSvc.getByUser(uid).subscribe(r => this.timesheets.set(this.toArr<Timesheet>(r)));
      },
      error: (e: any) => this.toast.error('Failed', e?.error?.message ?? '')
    });
  }

  confirmDeleteTs(ts:Timesheet){
    this.cfgTitle.set('Delete Timesheet');this.cfgMsg.set(`Delete timesheet for "${ts.projectName}"?`);
    this.cfgAction=()=>{this.tsSvc.delete(ts.id).subscribe({next:()=>{this.toast.success('Deleted','');const uid=this.auth.currentUser()!;this.tsSvc.getByUser(uid).subscribe(r=>this.timesheets.set(this.toArr<Timesheet>(r)));},error:(e:any)=>this.toast.error('Error',e?.error?.message??'')});};
    this.cfgVisible.set(true);
  }

  confirmDeleteLeave(l: Leave) {
    this.cfgTitle.set('Delete Leave'); this.cfgMsg.set('Delete this leave request?');
    this.cfgAction = () => {
      this.lvSvc.deleteLeave(l.id).subscribe({
        next: () => { this.toast.success('Deleted', ''); const uid = this.auth.currentUser()!; this.lvSvc.getMyLeaves(uid).subscribe(r => this.leaves.set(this.toArr<Leave>(r))); },
        error: (e: any) => this.toast.error('Error', e?.error?.message ?? '')
      });
    };
    this.cfgVisible.set(true);
  }
  onCfgOk(){this.cfgAction?.();this.cfgVisible.set(false);this.cfgAction=null;}
  onCfgCancel(){this.cfgVisible.set(false);this.cfgAction=null;}

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

    // 🔥 ADD THIS
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
        const msg = remaining !== undefined
          ? `Leave applied successfully. Remaining leave: ${remaining} day(s)`
          : 'Your leave request is pending approval.';
        this.toast.success('Leave Applied', msg);
        this.showLeaveModal.set(false);
        this.leaveForm.reset();
        this.lvSvc.getMyLeaves(uid).subscribe(r => this.leaves.set(this.toArr<Leave>(r)));
      },
      error: (e: any) => this.toast.error('Application Failed', e?.error?.message ?? '')
    });
  }

  private fmt(t:string):string{return t?.length===5?t+':00':t??'00:00:00';}
  stText(s:any){return s==0?'Pending':s==1?'Approved':'Rejected';}
  stClass(s:any){return s==0?'zbadge-pending':s==1?'zbadge-approved':'zbadge-rejected';}

  taskStatusText(s:any){return s==0?'Pending':s==1?'In Progress':'Completed';}
  taskStatusClass(s:any){return s==2?'zbadge-approved':s==1?'zbadge-info':'zbadge-pending';}

  markTaskComplete(t:InternTask){
    this.cfgTitle.set('Complete Task');
    this.cfgMsg.set(`Mark "${t.taskTitle}" as completed?`);
    this.cfgAction=()=>{
      this.intSvc.updateTask(t.id,{taskTitle:t.taskTitle,description:t.description,dueDate:t.dueDate,status:2}).subscribe({
        next:()=>{this.toast.success('Task Completed','');const uid=this.auth.currentUser()!;this.intSvc.getTasks(uid).subscribe(r=>this.tasks.set(this.toArr<InternTask>(r)));},
        error:(e:any)=>this.toast.error('Failed',e?.error?.message??'')
      });
    };
    this.cfgVisible.set(true);
  }
  leaveDays(l:Leave){return Math.ceil((new Date(l.toDate).getTime()-new Date(l.fromDate).getTime())/86400000)+1;}
  setTab(t:InternTab){
    this.activeTab.set(t);
    this.tabSvc.setTab(t);
    this.bc.set([{label:'Intern Hub'},{label:this.tabs.find(x=>x.key===t)?.label??''}]);
  }
  pages(n:number){return Array.from({length:n},(_,i)=>i+1);}
}
