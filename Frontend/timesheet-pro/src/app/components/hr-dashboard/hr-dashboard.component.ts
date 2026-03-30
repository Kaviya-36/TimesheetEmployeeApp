import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { saveAs } from 'file-saver';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import * as XLSX from 'xlsx';
import { Attendance, Leave, LeaveType, Payroll, Project, User, UserProfile } from '../../models';
import { AttendanceService, LeaveService, PayrollService, ProjectService, TimesheetService, UserService } from '../../services/api.services';
import { AuthService } from '../../services/auth.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
import { TabService } from '../../services/tab.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmComponent } from '../confirm-dialog/confirm.component';
import { NavbarComponent } from '../navbar/navbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

export type HrTab = 'employees' | 'attendance' | 'leaves' | 'payroll' | 'timesheets' | 'reports' | 'profile';

@Component({
  selector: 'app-hr-dashboard',
  standalone: true,
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule, FormsModule, NavbarComponent, SidebarComponent, ConfirmComponent],
  templateUrl: './hr-dashboard.component.html',
  styleUrl:    './hr-dashboard.component.css'
})
export class HrDashboardComponent implements OnInit, OnDestroy {
  readonly auth  = inject(AuthService);
  private  toast = inject(ToastService);
  private  bc    = inject(BreadcrumbService);
  private  usrSvc = inject(UserService);
  private  attSvc = inject(AttendanceService);
  private  lvSvc  = inject(LeaveService);
  private  paySvc = inject(PayrollService);
  private  tsSvc  = inject(TimesheetService);
  private  prjSvc = inject(ProjectService);
  private  fb     = inject(FormBuilder);
  private  tabSvc = inject(TabService);

  constructor() {
    effect(() => {
      const t = this.tabSvc.activeTab();
      if (t && t !== this.activeTab()) this.setTab(t as HrTab);
    });
  }

  activeTab = signal<HrTab>('employees');
  readonly tabs = [
    { key:'employees'  as HrTab, label:'Employees',  icon:'👥' },
    { key:'attendance' as HrTab, label:'Attendance',  icon:'📅' },
    { key:'leaves'     as HrTab, label:'Leaves',      icon:'🌴' },
    { key:'timesheets' as HrTab, label:'Timesheets',  icon:'📋' },
    { key:'payroll'    as HrTab, label:'Payroll',     icon:'💰' },
    { key:'reports'    as HrTab, label:'Reports',     icon:'📊' },
    { key:'profile'    as HrTab, label:'Profile',     icon:'👤' },
  ];
  readonly roleOptions = ['Employee','Manager','HR','Intern','Mentor'];

  employees  = signal<User[]>([]);
  attendance = signal<Attendance[]>([]);
  allLeaves  = signal<Leave[]>([]);
  payrolls   = signal<Payroll[]>([]);
  userProfile = signal<UserProfile | null>(null);

  // ── Own check-in/out ──────────────────────────────────────────────────────
  todayAtt   = signal<Attendance | null>(null);
  attLoading = signal(false);
  liveTimer  = signal('00:00:00');
  private timerInterval: any;
  readonly todayDate = new Date().toISOString().split('T')[0];
  readonly currentMonth = new Date().toISOString().slice(0, 7); // "2026-03"

  empSearch  = signal(''); empRoleF = signal('all'); empStatusF = signal('all'); empPage = signal(1); empPS = 8;
  attSearch  = signal(''); attPage = signal(1); attPS = 8;
  lvSearch   = signal(''); lvStatusF = signal('all'); lvPage = signal(1); lvPS = 8;
  paySearch  = signal(''); payPage = signal(1); payPS = 8;

  // ── Timesheets ────────────────────────────────────────────────────────────
  allTimesheets = signal<any[]>([]);
  tsSearch  = signal(''); tsStatus = signal('all'); tsPage = signal(1); tsPS = 8;
  tsSortCol = signal<'date'|'hours'|'employee'>('date'); tsSortDir = signal<'asc'|'desc'>('desc');
  tsViewMode     = signal<'all'|'weekly'|'monthly'>('all');
  tsPeriodOffset = signal(0);
  tsPeriodLabel  = () => this._periodLabel(this.tsViewMode(), this.tsPeriodOffset());

  weekDays = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];

  toDateStr(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  }
  readonly todayDateStr = this.toDateStr(new Date());

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

  private parseHoursVal(val: any): number {
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
  parseHoursPublic = (val: any) => this.parseHoursVal(val);

  filteredTs = computed(() => {
    let d = this.allTimesheets();
    const q = this.tsSearch().toLowerCase();
    if (q) d = d.filter(t => (t.employeeName??'').toLowerCase().includes(q) || (t.projectName??'').toLowerCase().includes(q));
    if (this.tsStatus() !== 'all') {
      const sv: Record<string,number> = { pending:0, approved:1, rejected:2 };
      d = d.filter(t => Number(t.status) === sv[this.tsStatus()]);
    }
    d = d.filter(t => this._inPeriod(t.date, this.tsViewMode(), this.tsPeriodOffset()));
    const col = this.tsSortCol(); const dir = this.tsSortDir();
    d = [...d].sort((a,b) => {
      const v = col==='date' ? new Date(a.date).getTime()-new Date(b.date).getTime()
              : col==='hours' ? this.parseHoursVal(a.hoursWorked)-this.parseHoursVal(b.hoursWorked)
              : (a.employeeName??'').localeCompare(b.employeeName??'');
      return dir==='asc' ? v : -v;
    });
    return d;
  });
  tsTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredTs().length / this.tsPS)));
  pagedTs      = computed(() => { const s=(this.tsPage()-1)*this.tsPS; return this.filteredTs().slice(s,s+this.tsPS); });

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
    const all   = this.allTimesheets().filter(t => this._inPeriod(t.date, 'monthly', this.tsPeriodOffset()));
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

  sortTs(col: 'date'|'hours'|'employee') {
    if (this.tsSortCol() === col) this.tsSortDir.update(d => d==='asc'?'desc':'asc');
    else { this.tsSortCol.set(col); this.tsSortDir.set('asc'); }
    this.tsPage.set(1);
  }
  ico(active: boolean, dir: string) { return !active ? '⇅' : dir==='asc' ? '↑' : '↓'; }

  // ── View mode ─────────────────────────────────────────────────────────────
  attViewMode     = signal<'all'|'weekly'|'monthly'>('all');
  attPeriodOffset = signal(0);
  attPeriodLabel  = () => this._periodLabel(this.attViewMode(), this.attPeriodOffset());

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

  filteredEmps = computed(() => {
    let d = this.employees();
    const q = this.empSearch().toLowerCase();
    if (q) d = d.filter(u => u.name.toLowerCase().includes(q)||u.email.toLowerCase().includes(q)||u.employeeId.toLowerCase().includes(q));
    if (this.empRoleF()!=='all') d = d.filter(u=>u.role===this.empRoleF());
    if (this.empStatusF()!=='all') d = d.filter(u=>this.empStatusF()==='active'?u.status==='Active':u.status!=='Active');
    return d;
  });
  empTotalPages = computed(()=>Math.max(1,Math.ceil(this.filteredEmps().length/this.empPS)));
  pagedEmps     = computed(()=>{const s=(this.empPage()-1)*this.empPS;return this.filteredEmps().slice(s,s+this.empPS);});

  filteredAtt = computed(()=>{
    let d=this.attendance();
    const q=this.attSearch().toLowerCase();
    if(q) d=d.filter(a=>(a.employeeName??'').toLowerCase().includes(q));
    d = d.filter(a => this._inPeriod(a.date, this.attViewMode(), this.attPeriodOffset()));
    return d;
  });
  attTotalPages = computed(()=>Math.max(1,Math.ceil(this.filteredAtt().length/this.attPS)));
  pagedAtt      = computed(()=>{const s=(this.attPage()-1)*this.attPS;return this.filteredAtt().slice(s,s+this.attPS);});

  filteredLv = computed(()=>{
    let d=this.allLeaves();
    const q=this.lvSearch().toLowerCase();
    if(q) d=d.filter(l=>(l.employeeName??'').toLowerCase().includes(q));
    if(this.lvStatusF()!=='all'){const sv:Record<string,number>={pending:0,approved:1,rejected:2};d=d.filter(l=>Number(l.status)===sv[this.lvStatusF()]);}
    return d;
  });
  lvTotalPages = computed(()=>Math.max(1,Math.ceil(this.filteredLv().length/this.lvPS)));
  pagedLv      = computed(()=>{const s=(this.lvPage()-1)*this.lvPS;return this.filteredLv().slice(s,s+this.lvPS);});

  filteredPay = computed(()=>{
    let d=this.payrolls();
    const q=this.paySearch().toLowerCase();
    if(q) d=d.filter(p=>(p.employeeName??'').toLowerCase().includes(q)||(p.employeeId??'').toLowerCase().includes(q));
    return d;
  });
  payTotalPages = computed(()=>Math.max(1,Math.ceil(this.filteredPay().length/this.payPS)));
  pagedPay      = computed(()=>{const s=(this.payPage()-1)*this.payPS;return this.filteredPay().slice(s,s+this.payPS);});

  activeCount  = computed(()=>this.employees().filter(u=>u.status==='Active').length);
  lateToday    = computed(()=>this.attendance().filter(a=>a.isLate).length);
  pendingLv    = computed(()=>this.allLeaves().filter(l=>Number(l.status)===0).length);
  totalPayroll = computed(()=>this.payrolls().reduce((s,p)=>s+(p.netSalary??0),0));

  editEmployee     = signal<User|null>(null);
  showPayrollModal = signal(false);
  showAddTimesheetModal = signal(false);
  showAddLeaveModal     = signal(false);
  leaveTypes            = signal<LeaveType[]>([]);
  projects              = signal<Project[]>([]);
  cfgVisible = signal(false); cfgTitle = signal(''); cfgMsg = signal(''); cfgType = signal<'danger'|'warning'|'info'>('danger');
  private cfgAction: (()=>void)|null=null;

  editForm = this.fb.group({ name:['',Validators.required], email:['',[Validators.required,Validators.email]], phone:[''], role:['',Validators.required] });
  payrollForm = this.fb.group({ userId:['',Validators.required], basicSalary:['',[Validators.required,Validators.min(0)]], overtimeAmount:['0'], deductions:['0'], salaryMonth:[new Date().toISOString().slice(0,7),Validators.required] });

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
    this.bc.set([{label:'HR Dashboard'}, {label:'Employees'}]);
    this.tabSvc.setTab('employees');
    this.loadAll();
    this.refreshToday();
    this.usrSvc.getProfile().subscribe({ next:(r:any)=>this.userProfile.set(r?.data??r), error:()=>{} });
    this.lvSvc.getLeaveTypes().subscribe({ next:(r:any)=>this.leaveTypes.set(this.toArr<LeaveType>(r)), error:()=>{} });
    this.prjSvc.getAll().subscribe({ next:(r:any)=>this.projects.set(this.toArr<Project>(r)), error:()=>{} });
  }

  ngOnDestroy() { if (this.timerInterval) clearInterval(this.timerInterval); }

  private toArr<T>(r:any):T[] {
    if(Array.isArray(r)) return r;
    if(Array.isArray(r?.data)) return r.data;
    if(Array.isArray(r?.data?.data)) return r.data.data;
    return [];
  }

  loadAll() {
    this.usrSvc.getAll().subscribe({next:r=>this.employees.set(this.toArr<User>(r)),error:()=>{}});
    this.attSvc.getAll().subscribe({next:r=>this.attendance.set(this.toArr<Attendance>(r)),error:()=>{}});
    this.lvSvc.getAll().subscribe({next:r=>this.allLeaves.set(this.toArr<Leave>(r)),error:()=>{}});
    this.tsSvc.getAll().subscribe({
      next:(r:any)=>{ const d=r?.data?.data??r?.data??r??[]; this.allTimesheets.set(Array.isArray(d)?d:[]); },
      error:()=>{}
    });
    this.paySvc.getAll().subscribe({
      next: r => {
        const list = this.toArr<any>(r);
        this.payrolls.set(list);
      },
      error: () => {}
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

  private confirm(title:string,msg:string,action:()=>void,type:'danger'|'warning'|'info'='danger'){    this.cfgTitle.set(title);this.cfgMsg.set(msg);this.cfgType.set(type);this.cfgAction=action;this.cfgVisible.set(true);
  }
  onCfgOk(){this.cfgAction?.();this.cfgVisible.set(false);this.cfgAction=null;}
  onCfgCancel(){this.cfgVisible.set(false);this.cfgAction=null;}

  openEdit(u:User){this.editEmployee.set(u);this.editForm.patchValue({name:u.name,email:u.email,phone:u.phone,role:u.role});}
  saveEdit(){
    const u=this.editEmployee();if(!u||this.editForm.invalid)return;
    const v=this.editForm.value;
    this.usrSvc.update(u.id,{name:v.name!,email:v.email!,phone:v.phone??'',role:v.role!}).subscribe({
      next:()=>{this.toast.success('Updated',`${u.name} updated.`);this.editEmployee.set(null);this.loadAll();},
      error:(e:any)=>this.toast.error('Error',e?.error?.message??'Failed.')
    });
  }

  confirmToggle(u:User){
    const activate=u.status!=='Active';
    this.confirm(activate?'Activate':'Deactivate',`${activate?'Activate':'Deactivate'} "${u.name}"?`,()=>{
      this.usrSvc.setActive(u.id,activate).subscribe({
        next:()=>{this.toast.success(activate?'Activated':'Deactivated',`${u.name}`);this.loadAll();},
        error:(e:any)=>this.toast.error('Error',e?.error?.message??'Failed.')
      });
    },activate?'info':'warning');
  }

  confirmDelete(u:User){
    this.confirm('Delete Employee',`Permanently delete "${u.name}"?`,()=>{
      this.usrSvc.delete(u.id).subscribe({next:()=>{this.toast.success('Deleted');this.loadAll();},error:(e:any)=>this.toast.error('Error',e?.error?.message??'Failed.')});
    });
  }

  approveLeave(l:Leave){
    const uid=this.auth.currentUser()!;
    this.lvSvc.approveOrReject({leaveId:l.id,approvedById:uid,isApproved:true,managerComment:'Approved by HR'}).subscribe({
      next:()=>{this.toast.success('Approved',`Leave for ${l.employeeName}`);this.loadAll();},error:()=>this.toast.error('Error','Failed.')
    });
  }
  rejectLeave(l:Leave){
    const uid=this.auth.currentUser()!;
    this.lvSvc.approveOrReject({leaveId:l.id,approvedById:uid,isApproved:false,managerComment:'Rejected by HR'}).subscribe({
      next:()=>{this.toast.warning('Rejected',`Leave for ${l.employeeName}`);this.loadAll();},error:()=>this.toast.error('Error','Failed.')
    });
  }

  generatePayroll(){
    if(this.payrollForm.invalid)return;
    const v=this.payrollForm.value;
    const salaryMonth = v.salaryMonth ? `${v.salaryMonth}-01` : '';
    const overtimeAmount = (+v.overtimeAmount! || 0) * 100; // 1 hour = ₹100
    this.paySvc.generate({userId:+v.userId!,basicSalary:+v.basicSalary!,overtimeAmount,deductions:+v.deductions!,salaryMonth}).subscribe({
      next:()=>{this.toast.success('Payroll Generated');this.showPayrollModal.set(false);this.payrollForm.reset({salaryMonth:this.currentMonth,overtimeAmount:'0',deductions:'0'});this.loadAll();},
      error:(e:any)=>this.toast.error('Error',e?.error?.message??'Failed.')
    });
  }
  /* =========================================================
   📊 REPORTS - EXCEL
========================================================= */

exportEmployeesExcel() {
  const data = this.employees().map(e => ({
    Name: e.name,
    Email: e.email,
    Role: e.role,
    Status: e.status
  }));

  const ws = XLSX.utils.json_to_sheet(data);
  const wb = { Sheets: { 'Employees': ws }, SheetNames: ['Employees'] };

  const buffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
  saveAs(new Blob([buffer]), 'employees-report.xlsx');
}

exportAttendanceExcel() {
  const data = this.attendance().map(a => ({
    Employee: a.employeeName,
    Date: a.date,
    CheckIn: a.checkIn,
    CheckOut: a.checkOut,
    Late: a.isLate ? 'Yes' : 'No'
  }));

  const ws = XLSX.utils.json_to_sheet(data);
  const wb = { Sheets: { 'Attendance': ws }, SheetNames: ['Attendance'] };

  const buffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
  saveAs(new Blob([buffer]), 'attendance-report.xlsx');
}

exportLeavesExcel() {
  const data = this.allLeaves().map(l => ({
    Employee: l.employeeName,
    From: l.fromDate,
    To: l.toDate,
    Status: this.stText(l.status)
  }));

  const ws = XLSX.utils.json_to_sheet(data);
  const wb = { Sheets: { 'Leaves': ws }, SheetNames: ['Leaves'] };

  const buffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
  saveAs(new Blob([buffer]), 'leave-report.xlsx');
}

exportPayrollExcel() {
  const data = this.payrolls().map(p => ({
    Employee: p.employeeName,
    BasicSalary: p.basicSalary,
    Deductions: p.deductions,
    NetSalary: p.netSalary
  }));

  import('xlsx').then(xlsx => {
    const ws = xlsx.utils.json_to_sheet(data);

    const wb = {
      Sheets: { 'Payroll': ws },
      SheetNames: ['Payroll']
    };

    const buffer = xlsx.write(wb, {
      bookType: 'xlsx',
      type: 'array'
    });

    import('file-saver').then(fs => {
      fs.saveAs(new Blob([buffer]), 'payroll-report.xlsx');
    });
  });
}

/* =========================================================
   📄 REPORTS - PDF
========================================================= */

exportEmployeesPDF() {
  const doc = new jsPDF();

  const rows = this.employees().map(e => [
    e.name,
    e.email,
    e.role,
    e.status
  ]);

  autoTable(doc, {
    head: [['Name','Email','Role','Status']],
    body: rows
  });

  doc.save('employees-report.pdf');
}

exportLeavesPDF() {
  const doc = new jsPDF();

  const rows = this.allLeaves().map(l => [
    l.employeeName,
    l.fromDate,
    l.toDate,
    this.stText(l.status)
  ]);

  autoTable(doc, {
    head: [['Employee','From','To','Status']],
    body: rows
  });

  doc.save('leave-report.pdf');
}

exportPayrollPDF() {
  const doc = new jsPDF();

  const rows = this.payrolls().map(p => [
    p.employeeName,
    p.basicSalary,
    p.deductions,
    p.netSalary
  ]);

  autoTable(doc, {
    head: [['Employee','Basic','Deductions','Net Salary']],
    body: rows
  });

  doc.save('payroll-report.pdf');
}

  // ── HR Chat Assistant ─────────────────────────────────────────────────────
  chatMessages = signal<{ role: 'user'|'assistant'; text: string }[]>([
    { role: 'assistant', text: 'Hi! I\'m your HR assistant. Ask me anything about employees, attendance, leaves, or payroll. Try: "How many employees are active?" or "Who was late this month?"' }
  ]);
  chatInput    = signal('');
  chatLoading  = signal(false);

  sendChat() {
    const q = this.chatInput().trim(); if (!q) return;
    this.chatMessages.update(m => [...m, { role: 'user', text: q }]);
    this.chatInput.set('');
    this.chatLoading.set(true);
    const answer = this.processQuery(q.toLowerCase());
    setTimeout(() => {
      this.chatMessages.update(m => [...m, { role: 'assistant', text: answer }]);
      this.chatLoading.set(false);
    }, 300);
  }

  private processQuery(q: string): string {
    const emps = this.employees();
    const att  = this.attendance();
    const lvs  = this.allLeaves();
    const pays = this.payrolls();

    // ── Employees ──────────────────────────────────────────────────────────
    if (q.includes('how many employee') || q.includes('total employee') || q.includes('employee count')) {
      const active = emps.filter(e => e.status === 'Active').length;
      return `There are ${emps.length} total employees — ${active} active and ${emps.length - active} inactive.`;
    }
    if (q.includes('active employee')) {
      const list = emps.filter(e => e.status === 'Active');
      return `${list.length} active employees: ${list.slice(0,5).map(e=>e.name).join(', ')}${list.length>5?` and ${list.length-5} more`:''}`;
    }
    if (q.includes('inactive employee') || q.includes('deactivated')) {
      const list = emps.filter(e => e.status !== 'Active');
      return list.length ? `${list.length} inactive: ${list.map(e=>e.name).join(', ')}` : 'No inactive employees.';
    }
    if (q.includes('role') || q.includes('manager') || q.includes('intern') || q.includes('mentor')) {
      const role = q.includes('manager') ? 'Manager' : q.includes('intern') ? 'Intern' : q.includes('mentor') ? 'Mentor' : q.includes('hr') ? 'HR' : null;
      if (role) {
        const list = emps.filter(e => e.role === role);
        return `${list.length} ${role}(s): ${list.map(e=>e.name).join(', ') || 'none'}`;
      }
      const byRole: Record<string,number> = {};
      emps.forEach(e => { byRole[e.role] = (byRole[e.role]||0)+1; });
      return 'Employees by role: ' + Object.entries(byRole).map(([r,c])=>`${r}: ${c}`).join(', ');
    }

    // ── Attendance ─────────────────────────────────────────────────────────
    if (q.includes('late') || q.includes('who was late')) {
      const late = att.filter(a => a.isLate);
      if (!late.length) return 'No late attendance records found.';
      const names = [...new Set(late.map(a => a.employeeName))];
      return `${late.length} late records for ${names.length} employee(s): ${names.slice(0,5).join(', ')}${names.length>5?` and ${names.length-5} more`:''}`;
    }
    if (q.includes('attendance') && (q.includes('today') || q.includes('checked in'))) {
      const today = new Date().toISOString().split('T')[0];
      const todayRec = att.filter(a => a.date?.startsWith(today));
      return `${todayRec.length} attendance record(s) today. ${todayRec.filter(a=>a.checkOut).length} checked out.`;
    }
    if (q.includes('total attendance') || q.includes('attendance record')) {
      return `${att.length} total attendance records. ${att.filter(a=>a.isLate).length} late entries.`;
    }

    // ── Leaves ─────────────────────────────────────────────────────────────
    if (q.includes('pending leave') || q.includes('leave pending')) {
      const pending = lvs.filter(l => Number(l.status) === 0);
      if (!pending.length) return 'No pending leave requests.';
      return `${pending.length} pending leave(s): ${pending.slice(0,5).map(l=>`${l.employeeName} (${l.leaveType})`).join(', ')}${pending.length>5?` and ${pending.length-5} more`:''}`;
    }
    if (q.includes('approved leave')) {
      const approved = lvs.filter(l => Number(l.status) === 1);
      return `${approved.length} approved leave(s).`;
    }
    if (q.includes('rejected leave')) {
      const rejected = lvs.filter(l => Number(l.status) === 2);
      return `${rejected.length} rejected leave(s).`;
    }
    if (q.includes('leave') && q.includes('who')) {
      const pending = lvs.filter(l => Number(l.status) === 0);
      return pending.length ? `Employees with pending leaves: ${[...new Set(pending.map(l=>l.employeeName))].join(', ')}` : 'No pending leaves.';
    }
    if (q.includes('total leave') || q.includes('leave count') || q.includes('how many leave')) {
      return `${lvs.length} total leave requests — ${lvs.filter(l=>Number(l.status)===0).length} pending, ${lvs.filter(l=>Number(l.status)===1).length} approved, ${lvs.filter(l=>Number(l.status)===2).length} rejected.`;
    }

    // ── Payroll ────────────────────────────────────────────────────────────
    if (q.includes('total payroll') || q.includes('total payout') || q.includes('payroll total')) {
      return `Total payroll payout: ₹${this.totalPayroll().toLocaleString('en-IN')} across ${pays.length} record(s).`;
    }
    if (q.includes('highest salary') || q.includes('highest paid') || q.includes('top salary')) {
      if (!pays.length) return 'No payroll records found.';
      const top = [...pays].sort((a,b)=>b.netSalary-a.netSalary)[0];
      return `Highest paid: ${top.employeeName} with ₹${top.netSalary.toLocaleString('en-IN')} net salary.`;
    }
    if (q.includes('lowest salary') || q.includes('lowest paid')) {
      if (!pays.length) return 'No payroll records found.';
      const bot = [...pays].sort((a,b)=>a.netSalary-b.netSalary)[0];
      return `Lowest paid: ${bot.employeeName} with ₹${bot.netSalary.toLocaleString('en-IN')} net salary.`;
    }
    if (q.includes('average salary') || q.includes('avg salary')) {
      if (!pays.length) return 'No payroll records.';
      const avg = this.totalPayroll() / pays.length;
      return `Average net salary: ₹${avg.toLocaleString('en-IN', {maximumFractionDigits:0})}.`;
    }
    if (q.includes('payroll') && q.includes('who')) {
      return pays.length ? `Payroll generated for: ${pays.map(p=>p.employeeName).join(', ')}` : 'No payroll records.';
    }

    // ── Summary ────────────────────────────────────────────────────────────
    if (q.includes('summary') || q.includes('overview') || q.includes('dashboard')) {
      return `📊 HR Summary:\n• ${emps.length} employees (${emps.filter(e=>e.status==='Active').length} active)\n• ${att.filter(a=>a.isLate).length} late attendance records\n• ${lvs.filter(l=>Number(l.status)===0).length} pending leaves\n• ₹${this.totalPayroll().toLocaleString('en-IN')} total payroll`;
    }

    // ── Help ───────────────────────────────────────────────────────────────
    if (q.includes('help') || q.includes('what can you') || q.includes('what do you')) {
      return 'I can answer questions about:\n• Employees — count, roles, active/inactive\n• Attendance — late records, today\'s check-ins\n• Leaves — pending, approved, who applied\n• Payroll — totals, highest/lowest/average salary\n\nTry: "Who has pending leaves?" or "What is the total payroll?"';
    }

    return `I couldn't find a specific answer for "${q}". Try asking about employees, attendance, leaves, or payroll. Type "help" for examples.`;
  }
  // ── Report Preview ────────────────────────────────────────────────────────
  previewType = signal<'employees'|'attendance'|'leaves'|'payroll'|null>(null);
  openPreview(type: 'employees'|'attendance'|'leaves'|'payroll') { this.previewType.set(type); }
  closePreview() { this.previewType.set(null); }

  leaveDays(l: Leave): number {
    return Math.ceil((new Date(l.toDate).getTime() - new Date(l.fromDate).getTime()) / 86400000) + 1;
  }

  stText(s:any){return s==0?'Pending':s==1?'Approved':'Rejected';}
  stClass(s:any){return s==0?'zbadge-pending':s==1?'zbadge-approved':'zbadge-rejected';}
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

  setTab(t:HrTab){
    this.activeTab.set(t);
    this.tabSvc.setTab(t);
    this.bc.set([{label:'HR Dashboard'},{label:this.tabs.find(x=>x.key===t)?.label??''}]);
    this.empPage.set(1);this.attPage.set(1);this.lvPage.set(1);this.payPage.set(1);
  }
  pages(total:number){return Array.from({length:total},(_,i)=>i+1);}
}
