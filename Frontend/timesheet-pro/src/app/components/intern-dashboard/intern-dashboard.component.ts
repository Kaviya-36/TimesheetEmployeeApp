import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Attendance, DashboardSummary, InternTask, Leave, LeaveType, ProjectAssignment, Timesheet, UserProfile } from '../../models';
import { AnalyticsService, AttendanceService, InternService, LeaveService, ProjectService, TimesheetService, UserService } from '../../services/api.services';
import { AuthService } from '../../services/auth.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
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
  pagedTs = computed(()=>{const s=(this.tsPage()-1)*this.tsPS;return this.timesheets().slice(s,s+this.tsPS);});
  tsTotalPages = computed(()=>Math.max(1,Math.ceil(this.timesheets().length/this.tsPS)));

  attPage = signal(1); attPS = 8;
  pagedAtt = computed(()=>{const s=(this.attPage()-1)*this.attPS;return this.attendances().slice(s,s+this.attPS);});
  attTotalPages = computed(()=>Math.max(1,Math.ceil(this.attendances().length/this.attPS)));

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
  readonly todayDate = new Date().toISOString().split('T')[0];

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
    this.bc.set([{label:'Intern Hub'}]);
    const uid = this.auth.currentUser(); if(!uid) return;
    this.loadAll(uid); this.refreshToday();
    this.anlSvc.getDashboard(uid).subscribe({next:r=>this.summary.set(r),error:()=>{}});
    this.usrSvc.getProfile().subscribe({ next:(r:any)=>this.userProfile.set(r?.data??r), error:()=>{} });
  }
  ngOnDestroy() { if(this.timerInterval) clearInterval(this.timerInterval); }

  private toArr<T>(r:any):T[] { if(Array.isArray(r))return r; if(Array.isArray(r?.data))return r.data; if(Array.isArray(r?.data?.data))return r.data.data; return []; }

  loadAll(uid:number) {
    this.tsSvc.getByUser(uid).subscribe(r=>this.timesheets.set(this.toArr<Timesheet>(r)));
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
      next:(res:any)=>{const d=res?.data??res;this.todayAtt.set(d);if(d?.checkIn&&!d?.checkOut)this.startTimer(d.checkIn);},
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

  taskLabel(t: any): string {
    return t?.description || t?.taskTitle || t?.title || t?.task_title || t?.name || `Task #${t?.id}`;
  }

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
  setTab(t:InternTab){this.activeTab.set(t);this.bc.set([{label:'Intern Hub'},{label:this.tabs.find(x=>x.key===t)?.label??''}]);}
  pages(n:number){return Array.from({length:n},(_,i)=>i+1);}
}
