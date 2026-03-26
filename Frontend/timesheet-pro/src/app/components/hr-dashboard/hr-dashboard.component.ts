import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { saveAs } from 'file-saver';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import * as XLSX from 'xlsx';
import { Attendance, Leave, LeaveType, Payroll, User, UserProfile } from '../../models';
import { AttendanceService, LeaveService, PayrollService, TimesheetService, UserService } from '../../services/api.services';
import { AuthService } from '../../services/auth.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmComponent } from '../confirm-dialog/confirm.component';
import { NavbarComponent } from '../navbar/navbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

export type HrTab = 'employees' | 'attendance' | 'leaves' | 'payroll' | 'reports' | 'profile';

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
  private  fb     = inject(FormBuilder);

  activeTab = signal<HrTab>('employees');
  readonly tabs = [
    { key:'employees' as HrTab, label:'Employees', icon:'👥' },
    { key:'attendance' as HrTab, label:'Attendance', icon:'📅' },
    { key:'leaves' as HrTab, label:'Leaves', icon:'🌴' },
    { key:'payroll' as HrTab, label:'Payroll', icon:'💰' },
    { key:'reports' as HrTab, label:'Reports', icon:'📊' },
    { key:'profile' as HrTab, label:'Profile', icon:'👤' },
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

  empSearch  = signal(''); empRoleF = signal('all'); empStatusF = signal('all'); empPage = signal(1); empPS = 8;
  attSearch  = signal(''); attPage = signal(1); attPS = 8;
  lvSearch   = signal(''); lvStatusF = signal('all'); lvPage = signal(1); lvPS = 8;
  paySearch  = signal(''); payPage = signal(1); payPS = 8;

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
  cfgVisible = signal(false); cfgTitle = signal(''); cfgMsg = signal(''); cfgType = signal<'danger'|'warning'|'info'>('danger');
  private cfgAction: (()=>void)|null=null;

  editForm = this.fb.group({ name:['',Validators.required], email:['',[Validators.required,Validators.email]], phone:[''], role:['',Validators.required] });
  payrollForm = this.fb.group({ userId:['',Validators.required], basicSalary:['',[Validators.required,Validators.min(0)]], overtimeAmount:['0'], deductions:['0'], salaryMonth:['',Validators.required] });

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
    this.bc.set([{label:'HR Dashboard'}]);
    this.loadAll();
    this.refreshToday();
    this.usrSvc.getProfile().subscribe({ next:(r:any)=>this.userProfile.set(r?.data??r), error:()=>{} });
    this.lvSvc.getLeaveTypes().subscribe({ next:(r:any)=>this.leaveTypes.set(this.toArr<LeaveType>(r)), error:()=>{} });
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
    this.paySvc.getAll().subscribe({
    next: r => {
      if (!r?.success) return;

      const payrolls = r.data?.data ?? [];
      this.payrolls.set(payrolls);
    },
    error: err => {
      console.error('Failed to load payrolls', err);
    }
  });
  }

  private refreshToday(): void {
    const uid = this.auth.currentUser(); if (!uid) return;
    this.attSvc.getTodayStatus(uid).subscribe({
      next:(res:any)=>{ const d=res?.data??res; this.todayAtt.set(d); if(d?.checkIn&&!d?.checkOut) this.startTimer(d.checkIn); },
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
    this.paySvc.generate({userId:+v.userId!,basicSalary:+v.basicSalary!,overtimeAmount:+v.overtimeAmount!,deductions:+v.deductions!,salaryMonth:v.salaryMonth!}).subscribe({
      next:()=>{this.toast.success('Payroll Generated');this.showPayrollModal.set(false);this.payrollForm.reset();this.loadAll();},
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

  stText(s:any){return s==0?'Pending':s==1?'Approved':'Rejected';}
  stClass(s:any){return s==0?'zbadge-pending':s==1?'zbadge-approved':'zbadge-rejected';}

  addTimesheetForSelf() {
    if (this.addTimesheetForm.invalid) return;
    const uid = this.auth.currentUser(); if (!uid) return;
    const v = this.addTimesheetForm.getRawValue();
    const fmt = (t: string) => t?.length === 5 ? t + ':00' : t ?? '00:00:00';
    this.tsSvc.create(uid, {
      projectId: +v.projectId!, projectName: '',
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
    this.bc.set([{label:'HR Dashboard'},{label:this.tabs.find(x=>x.key===t)?.label??''}]);
    this.empPage.set(1);this.attPage.set(1);this.lvPage.set(1);this.payPage.set(1);
  }
  pages(total:number){return Array.from({length:total},(_,i)=>i+1);}
}
