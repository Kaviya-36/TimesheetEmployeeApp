import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { MentorDashboardComponent } from './mentor-dashboard.component';
import { AuthService }         from '../../services/auth.service';
import { ToastService }        from '../../services/toast.service';
import { BreadcrumbService }   from '../../services/breadcrumb.service';
import { NotificationService } from '../../services/notification.service';
import {
  TimesheetService, AttendanceService,
  LeaveService, ProjectService, AnalyticsService,
  UserService, InternService
} from '../../services/api.services';
import { Timesheet, Attendance, Leave, LeaveType, ProjectAssignment, User, InternTask } from '../../models';

const makeTs = (o: Partial<Timesheet> = {}): Timesheet => ({
  id: 1, employeeName: 'Mentor1', employeeId: 'M001',
  projectName: 'Project X', date: '2024-06-01',
  startTime: '09:00:00', endTime: '17:00:00', breakTime: '01:00:00',
  hoursWorked: 7, status: 0, ...o
});

const makeAtt = (o: Partial<Attendance> = {}): Attendance => ({
  id: 1, userId: 10, employeeName: 'Mentor1', date: '2024-06-01',
  checkIn: undefined, checkOut: undefined, isLate: false, ...o
});

const makeLeave = (o: Partial<Leave> = {}): Leave => ({
  id: 1, employeeName: 'Mentor1', leaveType: 'Annual',
  fromDate: '2024-07-01', toDate: '2024-07-03', status: 0, ...o
});

const makeUser = (o: Partial<User> = {}): User => ({
  id: 1, employeeId: 'I001', name: 'InternA', email: 'a@test.com',
  phone: '', role: 'Intern', status: 'Active', joiningDate: '2024-01-01', ...o
});

const makeTask = (o: Partial<InternTask> = {}): InternTask => ({
  id: 1, taskTitle: 'Setup environment', description: '', status: 'Pending', ...o
});

describe('MentorDashboardComponent', () => {
  let component: MentorDashboardComponent;
  let fixture:   ComponentFixture<MentorDashboardComponent>;

  let authSpy:   jasmine.SpyObj<AuthService>;
  let toastSpy:  jasmine.SpyObj<ToastService>;
  let tsSpy:     jasmine.SpyObj<TimesheetService>;
  let attSpy:    jasmine.SpyObj<AttendanceService>;
  let lvSpy:     jasmine.SpyObj<LeaveService>;
  let prjSpy:    jasmine.SpyObj<ProjectService>;
  let anlSpy:    jasmine.SpyObj<AnalyticsService>;
  let usrSpy:    jasmine.SpyObj<UserService>;
  let intSpy:    jasmine.SpyObj<InternService>;
  let notifSpy:  jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    authSpy = jasmine.createSpyObj('AuthService', ['logout'], {
      currentUser: signal<number | null>(10),
      currentRole: signal<any>('Mentor'),
      username:    signal<string | null>('mentor01'),
      isLoggedIn:  () => true,
      token:       signal<string | null>('tok')
    });
    toastSpy = jasmine.createSpyObj('ToastService', ['success','error','warning','info']);
    tsSpy    = jasmine.createSpyObj('TimesheetService', ['getByUser','create','update','delete']);
    attSpy   = jasmine.createSpyObj('AttendanceService', ['getMyAttendance','checkIn','checkOut','getTodayStatus']);
    lvSpy    = jasmine.createSpyObj('LeaveService', ['getMyLeaves','apply','getLeaveTypes']);
    prjSpy   = jasmine.createSpyObj('ProjectService', ['getUserAssignments']);
    anlSpy   = jasmine.createSpyObj('AnalyticsService', ['getDashboard']);
    usrSpy   = jasmine.createSpyObj('UserService', ['getAll']);
    intSpy   = jasmine.createSpyObj('InternService', ['getTasks','createTask','deleteTask']);
    notifSpy = jasmine.createSpyObj('NotificationService', ['pushLocal','connect','markAllRead','markRead'], {
      notifications: signal([]), unreadCount: signal(0), connected: signal(false)
    });

    tsSpy.getByUser.and.returnValue(of([]));
    attSpy.getMyAttendance.and.returnValue(of([]));
    attSpy.getTodayStatus.and.returnValue(of(null));
    lvSpy.getMyLeaves.and.returnValue(of([]));
    lvSpy.getLeaveTypes.and.returnValue(of([]));
    prjSpy.getUserAssignments.and.returnValue(of([]));
    anlSpy.getDashboard.and.returnValue(of({
      timesheetsPending: 0, timesheetsApproved: 0, leavesPending: 0, checkedInToday: 0, lateToday: 0
    }));
    usrSpy.getAll.and.returnValue(of([]));
    intSpy.getTasks.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [MentorDashboardComponent, ReactiveFormsModule, FormsModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService,         useValue: authSpy  },
        { provide: ToastService,        useValue: toastSpy },
        { provide: TimesheetService,    useValue: tsSpy    },
        { provide: AttendanceService,   useValue: attSpy   },
        { provide: LeaveService,        useValue: lvSpy    },
        { provide: ProjectService,      useValue: prjSpy   },
        { provide: AnalyticsService,    useValue: anlSpy   },
        { provide: UserService,         useValue: usrSpy   },
        { provide: InternService,       useValue: intSpy   },
        { provide: NotificationService, useValue: notifSpy },
        BreadcrumbService
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(MentorDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => { component.ngOnDestroy(); });

  // ── Creation & initial state ───────────────────────────────────────────────
  it('should create', () => expect(component).toBeTruthy());
  it('should default to dashboard tab', () => expect(component.activeTab()).toBe('dashboard'));
  it('should have 6 tabs', () => expect(component.tabs.length).toBe(6));
  it('tabs should include interns tab', () => expect(component.tabs.map(t => t.key)).toContain('interns'));
  it('should start with empty timesheets', () => expect(component.timesheets()).toEqual([]));
  it('liveTimer should start at 00:00:00', () => expect(component.liveTimer()).toBe('00:00:00'));
  it('should start with no selected intern', () => expect(component.selectedIntern()).toBeNull());
  it('should start with empty intern tasks', () => expect(component.internTasks()).toEqual([]));

  // ── setTab ─────────────────────────────────────────────────────────────────
  it('setTab should change activeTab', () => {
    component.setTab('timesheets');
    expect(component.activeTab()).toBe('timesheets');
  });

  it('setTab should navigate through all tabs', () => {
    component.tabs.forEach(t => {
      component.setTab(t.key);
      expect(component.activeTab()).toBe(t.key);
    });
  });

  it('setTab should reset all page signals to 1', () => {
    component.tsPage.set(4); component.attPage.set(3);
    component.lvPage.set(2); component.internPage.set(5);
    component.setTab('dashboard');
    expect(component.tsPage()).toBe(1);
    expect(component.attPage()).toBe(1);
    expect(component.lvPage()).toBe(1);
    expect(component.internPage()).toBe(1);
  });

  // ── Computed stats ─────────────────────────────────────────────────────────
  it('approvedTsCount should count status===1', () => {
    component.timesheets.set([makeTs({ status: 1 }), makeTs({ id: 2, status: 0 }), makeTs({ id: 3, status: 1 })]);
    expect(component.approvedTsCount()).toBe(2);
  });

  it('pendingTsCount should count status===0', () => {
    component.timesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })]);
    expect(component.pendingTsCount()).toBe(1);
  });

  it('totalHours should sum all hoursWorked', () => {
    component.timesheets.set([makeTs({ hoursWorked: 6 }), makeTs({ id: 2, hoursWorked: 8 })]);
    expect(component.totalHours()).toBe('14.0');
  });

  it('pendingLvCount should count leaves with status 0', () => {
    component.leaves.set([makeLeave({ status: 0 }), makeLeave({ id: 2, status: 1 })]);
    expect(component.pendingLvCount()).toBe(1);
  });

  it('activeTasks should count non-Completed intern tasks', () => {
    component.internTasks.set([
      makeTask({ status: 'Pending' }),
      makeTask({ id: 2, status: 'Completed' }),
      makeTask({ id: 3, status: 'InProgress' })
    ]);
    expect(component.activeTasks()).toBe(2);
  });

  // ── Intern filtering in list ───────────────────────────────────────────────
  it('should separate interns from all users', fakeAsync(() => {
    usrSpy.getAll.and.returnValue(of([
      makeUser({ role: 'Intern' }),
      { ...makeUser({ id: 2 }), role: 'Employee' },
      makeUser({ id: 3, role: 'Intern' })
    ]));
    component.ngOnInit();
    tick();
    expect(component.myInterns().length).toBe(2);
    expect(component.myInterns().every(u => u.role === 'Intern')).toBeTrue();
  }));

  it('filteredInterns should filter by name', () => {
    component.myInterns.set([makeUser({ name: 'Alice' }), makeUser({ id: 2, name: 'Bob' })]);
    component.internSearch.set('alice');
    expect(component.filteredInterns().length).toBe(1);
    expect(component.filteredInterns()[0].name).toBe('Alice');
  });

  it('filteredInterns should filter by email', () => {
    component.myInterns.set([makeUser({ email: 'a@test.com' }), makeUser({ id: 2, email: 'b@test.com' })]);
    component.internSearch.set('b@test');
    expect(component.filteredInterns().length).toBe(1);
  });

  it('filteredInterns returns all when search empty', () => {
    component.myInterns.set([makeUser(), makeUser({ id: 2 }), makeUser({ id: 3 })]);
    component.internSearch.set('');
    expect(component.filteredInterns().length).toBe(3);
  });

  // ── Intern pagination ─────────────────────────────────────────────────────
  it('internTotalPages calculates for internPS=8', () => {
    component.myInterns.set(Array.from({ length: 20 }, (_, i) => makeUser({ id: i + 1 })));
    expect(component.internTotalPages()).toBe(3);
  });

  it('pagedInterns returns max internPS items', () => {
    component.myInterns.set(Array.from({ length: 20 }, (_, i) => makeUser({ id: i + 1 })));
    expect(component.pagedInterns().length).toBe(8);
  });

  // ── viewIntern ────────────────────────────────────────────────────────────
  it('viewIntern should set selectedIntern', fakeAsync(() => {
    const intern = makeUser();
    intSpy.getTasks.and.returnValue(of([]));
    component.viewIntern(intern);
    tick();
    expect(component.selectedIntern()).toBe(intern);
  }));

  it('viewIntern should load tasks for selected intern', fakeAsync(() => {
    const intern = makeUser();
    const tasks  = [makeTask(), makeTask({ id: 2, taskTitle: 'Deploy app' })];
    intSpy.getTasks.and.returnValue(of(tasks));
    component.viewIntern(intern);
    tick();
    expect(component.internTasks().length).toBe(2);
  }));

  it('viewIntern should show error toast when tasks fail to load', fakeAsync(() => {
    intSpy.getTasks.and.returnValue(throwError(() => ({})));
    component.viewIntern(makeUser());
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── openAssignTask ────────────────────────────────────────────────────────
  it('openAssignTask should warn when no intern selected', () => {
    component.selectedIntern.set(null);
    component.openAssignTask();
    expect(toastSpy.warning).toHaveBeenCalled();
    expect(component.showTaskModal()).toBeFalse();
  });

  it('openAssignTask should open modal when intern is selected', () => {
    component.selectedIntern.set(makeUser());
    component.openAssignTask();
    expect(component.showTaskModal()).toBeTrue();
  });

  // ── assignTask ────────────────────────────────────────────────────────────
  it('assignTask should not call API when form invalid', () => {
    component.taskForm.controls['taskTitle'].setValue('');
    component.assignTask();
    expect(intSpy.createTask).not.toHaveBeenCalled();
  });

  it('assignTask should call intSvc.createTask with correct data', fakeAsync(() => {
    const intern = makeUser({ id: 5, name: 'InternX' });
    component.selectedIntern.set(intern);
    component.taskForm.setValue({ taskTitle: 'Build API', description: 'REST endpoints', dueDate: '2024-07-30' });
    intSpy.createTask.and.returnValue(of({}));
    intSpy.getTasks.and.returnValue(of([]));
    component.assignTask();
    tick();
    expect(intSpy.createTask).toHaveBeenCalledWith(jasmine.objectContaining({
      internId: 5, taskTitle: 'Build API', description: 'REST endpoints'
    }));
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.showTaskModal()).toBeFalse();
  }));

  it('assignTask should show error on failure', fakeAsync(() => {
    component.selectedIntern.set(makeUser());
    component.taskForm.setValue({ taskTitle: 'Task', description: '', dueDate: '' });
    intSpy.createTask.and.returnValue(throwError(() => ({ error: { message: 'Error' } })));
    component.assignTask();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── confirmDeleteTask ─────────────────────────────────────────────────────
  it('confirmDeleteTask should open confirm dialog', () => {
    component.confirmDeleteTask(makeTask());
    expect(component.cfgVisible()).toBeTrue();
    expect(component.cfgTitle()).toContain('Delete Task');
  });

  it('onCfgOk after confirmDeleteTask should call intSvc.deleteTask', fakeAsync(() => {
    const intern = makeUser();
    component.selectedIntern.set(intern);
    intSpy.deleteTask.and.returnValue(of({}));
    intSpy.getTasks.and.returnValue(of([]));
    component.confirmDeleteTask(makeTask());
    component.onCfgOk();
    tick();
    expect(intSpy.deleteTask).toHaveBeenCalledWith(1);
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  it('confirmDeleteTask should handle error', fakeAsync(() => {
    component.selectedIntern.set(makeUser());
    intSpy.deleteTask.and.returnValue(throwError(() => ({ error: { message: 'Fail' } })));
    component.confirmDeleteTask(makeTask());
    component.onCfgOk();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── checkIn ───────────────────────────────────────────────────────────────
  it('checkIn should warn if already checked in', () => {
    component.todayAtt.set(makeAtt({ checkIn: '09:00' }));
    component.checkIn();
    expect(toastSpy.warning).toHaveBeenCalled();
    expect(attSpy.checkIn).not.toHaveBeenCalled();
  });

  it('checkIn should call attSvc.checkIn when not checked in', fakeAsync(() => {
    component.todayAtt.set(null);
    attSpy.checkIn.and.returnValue(of({ checkIn: '09:00' }));
    component.checkIn();
    tick();
    expect(attSpy.checkIn).toHaveBeenCalled();
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.attLoading()).toBeFalse();
  }));

  it('checkIn should handle error', fakeAsync(() => {
    component.todayAtt.set(null);
    attSpy.checkIn.and.returnValue(throwError(() => ({ error: { message: 'Fail' } })));
    component.checkIn();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
    expect(component.attLoading()).toBeFalse();
  }));

  // ── checkOut ──────────────────────────────────────────────────────────────
  it('checkOut should warn if not checked in', () => {
    component.todayAtt.set(null);
    component.checkOut();
    expect(toastSpy.warning).toHaveBeenCalled();
    expect(attSpy.checkOut).not.toHaveBeenCalled();
  });

  it('checkOut should warn if already checked out', () => {
    component.todayAtt.set(makeAtt({ checkIn: '09:00', checkOut: '17:00' }));
    component.checkOut();
    expect(toastSpy.warning).toHaveBeenCalled();
    expect(attSpy.checkOut).not.toHaveBeenCalled();
  });

  it('checkOut should call attSvc.checkOut when valid', fakeAsync(() => {
    component.todayAtt.set(makeAtt({ checkIn: '09:00', checkOut: undefined }));
    attSpy.checkOut.and.returnValue(of({ checkIn: '09:00', checkOut: '17:00', totalHours: '8' }));
    component.checkOut();
    tick();
    expect(attSpy.checkOut).toHaveBeenCalled();
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  // ── Timesheet submission ──────────────────────────────────────────────────
  it('submitTimesheet should not call API when form invalid', () => {
    component.tsForm.controls['projectId'].setValue('');
    component.submitTimesheet();
    expect(tsSpy.create).not.toHaveBeenCalled();
  });

  it('submitTimesheet should show error when no project assignment found', () => {
    component.projectsAssignment.set([]);
    component.tsForm.patchValue({ projectId: '999', workDate: component.todayDate, startTime: '09:00', endTime: '17:00' });
    component.submitTimesheet();
    expect(toastSpy.error).toHaveBeenCalled();
  });

  it('submitTimesheet should call tsSvc.create with correct payload', fakeAsync(() => {
    const asgn: ProjectAssignment = { id: 1, projectId: 10, projectName: 'Alpha' };
    component.projectsAssignment.set([asgn]);
    component.tsForm.patchValue({
      projectId: '1', workDate: component.todayDate,
      startTime: '09:00', endTime: '17:00', taskDescription: 'Mentoring'
    });
    tsSpy.create.and.returnValue(of({}));
    tsSpy.getByUser.and.returnValue(of([]));
    component.submitTimesheet();
    tick();
    expect(tsSpy.create).toHaveBeenCalledWith(10, jasmine.objectContaining({ projectId: 10 }));
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.showTsModal()).toBeFalse();
  }));

  // ── openEditTs ────────────────────────────────────────────────────────────
  it('openEditTs should warn when status is not pending', () => {
    component.openEditTs(makeTs({ status: 1 }));
    expect(toastSpy.warning).toHaveBeenCalled();
    expect(component.showEditModal()).toBeFalse();
  });

  it('openEditTs should open edit modal for pending timesheet', () => {
    component.openEditTs(makeTs({ status: 0, date: '2024-06-01', startTime: '09:00:00', endTime: '17:00:00' }));
    expect(component.showEditModal()).toBeTrue();
    expect(component.editTs()?.status).toBe(0);
  });

  // ── confirmDeleteTs ────────────────────────────────────────────────────────
  it('confirmDeleteTs should open confirm dialog', () => {
    component.confirmDeleteTs(makeTs());
    expect(component.cfgVisible()).toBeTrue();
    expect(component.cfgTitle()).toContain('Delete Timesheet');
  });

  it('onCfgOk after confirmDeleteTs should call tsSvc.delete', fakeAsync(() => {
    tsSpy.delete.and.returnValue(of({}));
    tsSpy.getByUser.and.returnValue(of([]));
    component.confirmDeleteTs(makeTs());
    component.onCfgOk();
    tick();
    expect(tsSpy.delete).toHaveBeenCalledWith(1);
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  // ── submitLeave ───────────────────────────────────────────────────────────
  it('submitLeave should not call API when form invalid', () => {
    component.leaveForm.controls['leaveTypeId'].setValue('');
    component.submitLeave();
    expect(lvSpy.apply).not.toHaveBeenCalled();
  });

  it('submitLeave should call lvSvc.apply with correct data', fakeAsync(() => {
    component.leaveForm.setValue({ leaveTypeId: '2', fromDate: '2024-07-05', toDate: '2024-07-07', reason: 'Training' });
    lvSpy.apply.and.returnValue(of({}));
    lvSpy.getMyLeaves.and.returnValue(of([]));
    component.submitLeave();
    tick();
    expect(lvSpy.apply).toHaveBeenCalledWith(10, jasmine.objectContaining({
      leaveTypeId: 2, fromDate: '2024-07-05', toDate: '2024-07-07', reason: 'Training'
    }));
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.showLeaveModal()).toBeFalse();
  }));

  // ── Timesheet filters/sort ─────────────────────────────────────────────────
  it('filteredTs should filter by status pending', () => {
    component.timesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })]);
    component.tsStatus.set('pending');
    expect(component.filteredTs().length).toBe(1);
  });

  it('filteredTs should filter by project name search', () => {
    component.timesheets.set([makeTs({ projectName: 'Alpha' }), makeTs({ id: 2, projectName: 'Beta' })]);
    component.tsSearch.set('beta');
    expect(component.filteredTs().length).toBe(1);
  });

  it('sortTs should toggle direction on same column', () => {
    component.tsSortCol.set('date'); component.tsSortDir.set('asc');
    component.sortTs('date');
    expect(component.tsSortDir()).toBe('desc');
  });

  it('sortTs should change column and reset to asc', () => {
    component.sortTs('hours');
    expect(component.tsSortCol()).toBe('hours');
    expect(component.tsSortDir()).toBe('asc');
  });

  // ── stText / stClass / taskStatusClass ────────────────────────────────────
  it('stText(0) → Pending',  () => expect(component.stText(0)).toBe('Pending'));
  it('stText(1) → Approved', () => expect(component.stText(1)).toBe('Approved'));
  it('stText(2) → Rejected', () => expect(component.stText(2)).toBe('Rejected'));
  it('stClass(0) contains "pending"',  () => expect(component.stClass(0)).toContain('pending'));
  it('stClass(1) contains "approved"', () => expect(component.stClass(1)).toContain('approved'));
  it('stClass(2) contains "rejected"', () => expect(component.stClass(2)).toContain('rejected'));
  it('taskStatusClass Completed → approved badge', () => expect(component.taskStatusClass('Completed')).toContain('approved'));
  it('taskStatusClass InProgress → info badge',    () => expect(component.taskStatusClass('InProgress')).toContain('info'));
  it('taskStatusClass Pending → pending badge',    () => expect(component.taskStatusClass('Pending')).toContain('pending'));

  // ── leaveDays ─────────────────────────────────────────────────────────────
  it('leaveDays returns 1 for same day',  () => expect(component.leaveDays(makeLeave({ fromDate: '2024-07-01', toDate: '2024-07-01' }))).toBe(1));
  it('leaveDays returns 3 for 3-day leave', () => expect(component.leaveDays(makeLeave({ fromDate: '2024-07-01', toDate: '2024-07-03' }))).toBe(3));

  // ── ico / pages ───────────────────────────────────────────────────────────
  it('ico(false,…) returns ⇅',   () => expect(component.ico(false, 'asc')).toBe('⇅'));
  it('ico(true,asc) returns ↑',  () => expect(component.ico(true, 'asc')).toBe('↑'));
  it('ico(true,desc) returns ↓', () => expect(component.ico(true, 'desc')).toBe('↓'));
  it('pages(3) returns [1,2,3]', () => expect(component.pages(3)).toEqual([1, 2, 3]));
  it('pages(0) returns []',      () => expect(component.pages(0)).toEqual([]));

  // ── onCfgCancel ───────────────────────────────────────────────────────────
  it('onCfgCancel should close confirm dialog', () => {
    component.cfgVisible.set(true);
    component.onCfgCancel();
    expect(component.cfgVisible()).toBeFalse();
  });
});
