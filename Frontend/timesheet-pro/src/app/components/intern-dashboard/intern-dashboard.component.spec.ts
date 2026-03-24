import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { InternDashboardComponent } from './intern-dashboard.component';
import { AuthService }         from '../../services/auth.service';
import { ToastService }        from '../../services/toast.service';
import { BreadcrumbService }   from '../../services/breadcrumb.service';
import {
  TimesheetService, AttendanceService,
  LeaveService, ProjectService, AnalyticsService, InternService
} from '../../services/api.services';
import { Timesheet, Attendance, Leave, LeaveType, ProjectAssignment, InternTask } from '../../models';

const makeTs = (o: Partial<Timesheet> = {}): Timesheet => ({
  id: 1, employeeName: 'InternA', employeeId: 'I001',
  projectName: 'Project X', date: '2024-06-01',
  startTime: '09:00', endTime: '17:00', breakTime: '01:00',
  hoursWorked: 7, status: 0, ...o
});

const makeAtt = (o: Partial<Attendance> = {}): Attendance => ({
  id: 1, userId: 5, employeeName: 'InternA', date: '2024-06-01',
  checkIn: undefined, checkOut: undefined, isLate: false, ...o
});

const makeLeave = (o: Partial<Leave> = {}): Leave => ({
  id: 1, employeeName: 'InternA', leaveType: 'Sick',
  fromDate: '2024-07-01', toDate: '2024-07-02', status: 0, ...o
});

const makeTask = (o: Partial<InternTask> = {}): InternTask => ({
  id: 1, taskTitle: 'Setup project', description: 'Clone and run',
  status: 'Pending', dueDate: '2024-07-15', ...o
});

describe('InternDashboardComponent', () => {
  let component: InternDashboardComponent;
  let fixture:   ComponentFixture<InternDashboardComponent>;

  let authSpy:   jasmine.SpyObj<AuthService>;
  let toastSpy:  jasmine.SpyObj<ToastService>;
  let tsSpy:     jasmine.SpyObj<TimesheetService>;
  let attSpy:    jasmine.SpyObj<AttendanceService>;
  let lvSpy:     jasmine.SpyObj<LeaveService>;
  let prjSpy:    jasmine.SpyObj<ProjectService>;
  let anlSpy:    jasmine.SpyObj<AnalyticsService>;
  let intSpy:    jasmine.SpyObj<InternService>;

  beforeEach(async () => {
    authSpy  = jasmine.createSpyObj('AuthService', ['logout'], {
      currentUser: signal<number | null>(5),
      currentRole: signal<any>('Intern'),
      username:    signal<string | null>('intern01'),
      isLoggedIn:  () => true,
      token:       signal<string | null>('tok')
    });
    toastSpy = jasmine.createSpyObj('ToastService',  ['success','error','warning','info']);
    tsSpy    = jasmine.createSpyObj('TimesheetService', ['getByUser','create','delete']);
    attSpy   = jasmine.createSpyObj('AttendanceService', ['getMyAttendance','checkIn','checkOut','getTodayStatus']);
    lvSpy    = jasmine.createSpyObj('LeaveService',   ['getMyLeaves','apply','getLeaveTypes']);
    prjSpy   = jasmine.createSpyObj('ProjectService', ['getUserAssignments']);
    anlSpy   = jasmine.createSpyObj('AnalyticsService', ['getDashboard']);
    intSpy   = jasmine.createSpyObj('InternService',  ['getTasks']);

    tsSpy.getByUser.and.returnValue(of([]));
    attSpy.getMyAttendance.and.returnValue(of([]));
    attSpy.getTodayStatus.and.returnValue(of(null));
    lvSpy.getMyLeaves.and.returnValue(of([]));
    lvSpy.getLeaveTypes.and.returnValue(of([]));
    prjSpy.getUserAssignments.and.returnValue(of([]));
    anlSpy.getDashboard.and.returnValue(of({
      timesheetsPending: 0, timesheetsApproved: 0, leavesPending: 0, checkedInToday: 0, lateToday: 0
    }));
    intSpy.getTasks.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [InternDashboardComponent, ReactiveFormsModule, FormsModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService,       useValue: authSpy  },
        { provide: ToastService,      useValue: toastSpy },
        { provide: TimesheetService,  useValue: tsSpy    },
        { provide: AttendanceService, useValue: attSpy   },
        { provide: LeaveService,      useValue: lvSpy    },
        { provide: ProjectService,    useValue: prjSpy   },
        { provide: AnalyticsService,  useValue: anlSpy   },
        { provide: InternService,     useValue: intSpy   },
        BreadcrumbService
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(InternDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => { component.ngOnDestroy(); });

  // ── Creation & initial state ───────────────────────────────────────────────
  it('should create', () => expect(component).toBeTruthy());
  it('should default to dashboard tab', () => expect(component.activeTab()).toBe('dashboard'));
  it('should have 5 tabs', () => expect(component.tabs.length).toBe(5));
  it('should start with empty timesheets', () => expect(component.timesheets()).toEqual([]));
  it('should start with liveTimer at 00:00:00', () => expect(component.liveTimer()).toBe('00:00:00'));
  it('should start with no todayAtt', () => expect(component.todayAtt()).toBeNull());

  // ── setTab ─────────────────────────────────────────────────────────────────
  it('setTab should update activeTab', () => {
    component.setTab('timesheet');
    expect(component.activeTab()).toBe('timesheet');
  });

  it('setTab should navigate through all tabs', () => {
    const tabKeys = component.tabs.map(t => t.key);
    tabKeys.forEach(key => {
      component.setTab(key);
      expect(component.activeTab()).toBe(key);
    });
  });

  // ── Computed stats ─────────────────────────────────────────────────────────
  it('approvedTs should count status===1 timesheets', () => {
    component.timesheets.set([makeTs({ status: 1 }), makeTs({ id: 2, status: 0 }), makeTs({ id: 3, status: 1 })]);
    expect(component.approvedTs()).toBe(2);
  });

  it('pendingTs should count status===0 timesheets', () => {
    component.timesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 }), makeTs({ id: 3, status: 0 })]);
    expect(component.pendingTs()).toBe(2);
  });

  it('totalHours should sum all hoursWorked', () => {
    component.timesheets.set([makeTs({ hoursWorked: 5 }), makeTs({ id: 2, hoursWorked: 6 })]);
    expect(component.totalHours()).toBe('11.0');
  });

  it('totalHours should be 0.0 for empty list', () => {
    component.timesheets.set([]);
    expect(component.totalHours()).toBe('0.0');
  });

  // ── Timesheet pagination ───────────────────────────────────────────────────
  it('tsTotalPages should be 1 when no timesheets', () => {
    component.timesheets.set([]);
    expect(component.tsTotalPages()).toBe(1);
  });

  it('tsTotalPages should calculate correctly for tsPS=6', () => {
    component.timesheets.set(Array.from({ length: 13 }, (_, i) => makeTs({ id: i + 1 })));
    expect(component.tsTotalPages()).toBe(3);
  });

  it('pagedTs should return max tsPS items', () => {
    component.timesheets.set(Array.from({ length: 13 }, (_, i) => makeTs({ id: i + 1 })));
    expect(component.pagedTs().length).toBe(6);
  });

  it('pagedTs page 2 returns second slice', () => {
    component.timesheets.set(Array.from({ length: 13 }, (_, i) => makeTs({ id: i + 1 })));
    component.tsPage.set(2);
    expect(component.pagedTs()[0].id).toBe(7);
  });

  // ── Attendance pagination ──────────────────────────────────────────────────
  it('attTotalPages calculates correctly for attPS=8', () => {
    component.attendances.set(Array.from({ length: 17 }, (_, i) => makeAtt({ id: i + 1 })));
    expect(component.attTotalPages()).toBe(3);
  });

  it('pagedAtt returns max attPS items', () => {
    component.attendances.set(Array.from({ length: 17 }, (_, i) => makeAtt({ id: i + 1 })));
    expect(component.pagedAtt().length).toBe(8);
  });

  // ── checkIn ───────────────────────────────────────────────────────────────
  it('checkIn should warn if already checked in', () => {
    component.todayAtt.set(makeAtt({ checkIn: '09:00' }));
    component.checkIn();
    expect(toastSpy.warning).toHaveBeenCalled();
    expect(attSpy.checkIn).not.toHaveBeenCalled();
  });

  it('checkIn should call attSvc.checkIn when not checked in', fakeAsync(() => {
    component.todayAtt.set(null);
    attSpy.checkIn.and.returnValue(of({ checkIn: '09:00', checkOut: null }));
    component.checkIn();
    tick();
    expect(attSpy.checkIn).toHaveBeenCalled();
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  it('checkIn should set attLoading false after success', fakeAsync(() => {
    component.todayAtt.set(null);
    attSpy.checkIn.and.returnValue(of({ checkIn: '09:00' }));
    component.checkIn();
    tick();
    expect(component.attLoading()).toBeFalse();
  }));

  it('checkIn should handle error', fakeAsync(() => {
    component.todayAtt.set(null);
    attSpy.checkIn.and.returnValue(throwError(() => ({ error: { message: 'Network error' } })));
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

  it('checkOut should handle error', fakeAsync(() => {
    component.todayAtt.set(makeAtt({ checkIn: '09:00' }));
    attSpy.checkOut.and.returnValue(throwError(() => ({ error: { message: 'Fail' } })));
    component.checkOut();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
    expect(component.attLoading()).toBeFalse();
  }));

  // ── submitTimesheet ───────────────────────────────────────────────────────
  it('submitTimesheet should not call API when form is invalid', () => {
    component.tsForm.controls['projectId'].setValue('');
    component.submitTimesheet();
    expect(tsSpy.create).not.toHaveBeenCalled();
  });

  it('submitTimesheet should error when project not in assignments', () => {
    component.projects.set([]);
    component.tsForm.patchValue({ projectId: '999', workDate: component.todayDate, startTime: '09:00', endTime: '17:00' });
    component.submitTimesheet();
    expect(toastSpy.error).toHaveBeenCalled();
  });

  it('submitTimesheet should call tsSvc.create on valid submission', fakeAsync(() => {
    const asgn: ProjectAssignment = { id: 1, projectId: 10, projectName: 'Internship Project' };
    component.projects.set([asgn]);
    component.tsForm.patchValue({ projectId: '1', workDate: component.todayDate, startTime: '09:00', endTime: '17:00' });
    tsSpy.create.and.returnValue(of({}));
    tsSpy.getByUser.and.returnValue(of([]));
    component.submitTimesheet();
    tick();
    expect(tsSpy.create).toHaveBeenCalledWith(5, jasmine.objectContaining({ projectId: 10 }));
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.showTsModal()).toBeFalse();
  }));

  it('submitTimesheet should show error on API failure', fakeAsync(() => {
    const asgn: ProjectAssignment = { id: 1, projectId: 10, projectName: 'X' };
    component.projects.set([asgn]);
    component.tsForm.patchValue({ projectId: '1', workDate: component.todayDate, startTime: '09:00', endTime: '17:00' });
    tsSpy.create.and.returnValue(throwError(() => ({ error: { message: 'Failed' } })));
    component.submitTimesheet();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── confirmDeleteTs ────────────────────────────────────────────────────────
  it('confirmDeleteTs should set cfgVisible to true', () => {
    component.confirmDeleteTs(makeTs());
    expect(component.cfgVisible()).toBeTrue();
    expect(component.cfgTitle()).toContain('Delete');
  });

  it('onCfgOk should execute delete action', fakeAsync(() => {
    tsSpy.delete.and.returnValue(of({}));
    tsSpy.getByUser.and.returnValue(of([]));
    component.confirmDeleteTs(makeTs());
    component.onCfgOk();
    tick();
    expect(tsSpy.delete).toHaveBeenCalledWith(1);
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  it('onCfgOk should handle delete error', fakeAsync(() => {
    tsSpy.delete.and.returnValue(throwError(() => ({ error: { message: 'Error' } })));
    component.confirmDeleteTs(makeTs());
    component.onCfgOk();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  it('onCfgCancel should close confirm dialog', () => {
    component.cfgVisible.set(true);
    component.onCfgCancel();
    expect(component.cfgVisible()).toBeFalse();
  });

  // ── submitLeave ───────────────────────────────────────────────────────────
  it('submitLeave should not call API when form invalid', () => {
    component.leaveForm.controls['leaveTypeId'].setValue('');
    component.submitLeave();
    expect(lvSpy.apply).not.toHaveBeenCalled();
  });

  it('submitLeave should call lvSvc.apply with correct data', fakeAsync(() => {
    component.leaveForm.setValue({ leaveTypeId: '3', fromDate: '2024-07-10', toDate: '2024-07-11', reason: 'Sick' });
    lvSpy.apply.and.returnValue(of({}));
    lvSpy.getMyLeaves.and.returnValue(of([]));
    component.submitLeave();
    tick();
    expect(lvSpy.apply).toHaveBeenCalledWith(5, jasmine.objectContaining({
      leaveTypeId: 3, fromDate: '2024-07-10', toDate: '2024-07-11', reason: 'Sick'
    }));
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.showLeaveModal()).toBeFalse();
  }));

  it('submitLeave should show error on API failure', fakeAsync(() => {
    component.leaveForm.setValue({ leaveTypeId: '1', fromDate: '2024-07-01', toDate: '2024-07-02', reason: '' });
    lvSpy.apply.and.returnValue(throwError(() => ({ error: { message: 'Error' } })));
    component.submitLeave();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── leaveDays ─────────────────────────────────────────────────────────────
  it('leaveDays should return 1 for same-day leave', () => {
    expect(component.leaveDays(makeLeave({ fromDate: '2024-07-01', toDate: '2024-07-01' }))).toBe(1);
  });

  it('leaveDays should return 2 for two-day leave', () => {
    expect(component.leaveDays(makeLeave({ fromDate: '2024-07-01', toDate: '2024-07-02' }))).toBe(2);
  });

  it('leaveDays should return 5 for five-day leave', () => {
    expect(component.leaveDays(makeLeave({ fromDate: '2024-07-01', toDate: '2024-07-05' }))).toBe(5);
  });

  // ── stText / stClass ───────────────────────────────────────────────────────
  it('stText(0) → Pending',  () => expect(component.stText(0)).toBe('Pending'));
  it('stText(1) → Approved', () => expect(component.stText(1)).toBe('Approved'));
  it('stText(2) → Rejected', () => expect(component.stText(2)).toBe('Rejected'));
  it('stClass(0) contains "pending"',  () => expect(component.stClass(0)).toContain('pending'));
  it('stClass(1) contains "approved"', () => expect(component.stClass(1)).toContain('approved'));
  it('stClass(2) contains "rejected"', () => expect(component.stClass(2)).toContain('rejected'));

  // ── pages ─────────────────────────────────────────────────────────────────
  it('pages(3) should return [1,2,3]', () => expect(component.pages(3)).toEqual([1,2,3]));
  it('pages(0) should return []',      () => expect(component.pages(0)).toEqual([]));
  it('pages(1) should return [1]',     () => expect(component.pages(1)).toEqual([1]));

  // ── Tasks ─────────────────────────────────────────────────────────────────
  it('should start with empty tasks', () => expect(component.tasks()).toEqual([]));

  it('should load tasks from InternService', fakeAsync(() => {
    const tasks = [makeTask(), makeTask({ id: 2, taskTitle: 'Build feature', status: 'Completed' })];
    intSpy.getTasks.and.returnValue(of(tasks));
    component.loadAll(5);
    tick();
    expect(component.tasks().length).toBe(2);
  }));
});
