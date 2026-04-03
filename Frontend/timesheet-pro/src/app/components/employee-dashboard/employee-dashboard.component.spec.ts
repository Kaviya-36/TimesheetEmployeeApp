import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { EmployeeDashboardComponent } from './employee-dashboard.component';
import { AuthService }         from '../../services/auth.service';
import { ToastService }        from '../../services/toast.service';
import { BreadcrumbService }   from '../../services/breadcrumb.service';
import { NotificationService } from '../../services/notification.service';
import {
  TimesheetService, AttendanceService,
  LeaveService, ProjectService, AnalyticsService
} from '../../services/api.services';
import { Timesheet, Attendance, Leave, LeaveType, ProjectAssignment } from '../../models';

const makeTs = (overrides: Partial<Timesheet> = {}): Timesheet => ({
  id: 1, employeeName: 'Alice', employeeId: 'E001',
  projectName: 'Project A', date: '2024-06-01',
  startTime: '09:00:00', endTime: '17:00:00', breakTime: '01:00:00',
  hoursWorked: 7, status: 0, ...overrides
});

const makeAtt = (overrides: Partial<Attendance> = {}): Attendance => ({
  id: 1, userId: 1, employeeName: 'Alice', date: '2024-06-01',
  checkIn: '09:00', checkOut: undefined, isLate: false, totalHours: undefined,
  ...overrides
});

const makeLeave = (overrides: Partial<Leave> = {}): Leave => ({
  id: 1, employeeName: 'Alice', leaveType: 'Annual',
  fromDate: '2024-06-10', toDate: '2024-06-12', status: 0, ...overrides
});

describe('EmployeeDashboardComponent', () => {
  let component: EmployeeDashboardComponent;
  let fixture:   ComponentFixture<EmployeeDashboardComponent>;

  let authSpy:   jasmine.SpyObj<AuthService>;
  let toastSpy:  jasmine.SpyObj<ToastService>;
  let tsSpy:     jasmine.SpyObj<TimesheetService>;
  let attSpy:    jasmine.SpyObj<AttendanceService>;
  let lvSpy:     jasmine.SpyObj<LeaveService>;
  let prjSpy:    jasmine.SpyObj<ProjectService>;
  let anlSpy:    jasmine.SpyObj<AnalyticsService>;
  let notifSpy:  jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    authSpy  = jasmine.createSpyObj('AuthService', ['logout'], {
      currentUser: signal<number | null>(1),
      currentRole: signal<any>('Employee'),
      username:    signal<string | null>('alice'),
      isLoggedIn:  () => true,
      token:       signal<string | null>('tok')
    });
    toastSpy  = jasmine.createSpyObj('ToastService',  ['success','error','warning','info']);
    tsSpy     = jasmine.createSpyObj('TimesheetService', ['getByUser','create','update','delete']);
    attSpy    = jasmine.createSpyObj('AttendanceService', ['getMyAttendance','checkIn','checkOut','getTodayStatus']);
    lvSpy     = jasmine.createSpyObj('LeaveService',   ['getMyLeaves','apply','getLeaveTypes']);
    prjSpy    = jasmine.createSpyObj('ProjectService', ['getUserAssignments']);
    anlSpy    = jasmine.createSpyObj('AnalyticsService', ['getDashboard']);
    notifSpy  = jasmine.createSpyObj('NotificationService', ['pushLocal','connect','markAllRead','markRead'], {
      notifications: signal([]), unreadCount: signal(0), connected: signal(false)
    });

    tsSpy.getByUser.and.returnValue(of([]));
    attSpy.getMyAttendance.and.returnValue(of([]));
    attSpy.getTodayStatus.and.returnValue(of(null));
    lvSpy.getMyLeaves.and.returnValue(of([]));
    lvSpy.getLeaveTypes.and.returnValue(of([]));
    prjSpy.getUserAssignments.and.returnValue(of([]));
    anlSpy.getDashboard.and.returnValue(of({
      timesheetsPending: 0, timesheetsApproved: 0, leavesPending: 0,
      checkedInToday: 0, lateToday: 0
    }));

    await TestBed.configureTestingModule({
      imports: [EmployeeDashboardComponent, ReactiveFormsModule, FormsModule],
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
        { provide: NotificationService, useValue: notifSpy },
        BreadcrumbService
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(EmployeeDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => { component.ngOnDestroy(); });

  // ── Creation & initial state ───────────────────────────────────────────────
  it('should create', () => expect(component).toBeTruthy());
  it('should default to dashboard tab', () => expect(component.activeTab()).toBe('dashboard'));
  it('should have 5 tabs', () => expect(component.tabs.length).toBe(5));
  it('should start with empty timesheets', () => expect(component.timesheets()).toEqual([]));
  it('should start with no todayAtt', () => expect(component.todayAtt()).toBeNull());
  it('liveTimer should start at 00:00:00', () => expect(component.liveTimer()).toBe('00:00:00'));

  // ── setTab ─────────────────────────────────────────────────────────────────
  it('setTab should update activeTab', () => {
    component.setTab('timesheet');
    expect(component.activeTab()).toBe('timesheet');
  });

  it('setTab should reset all page signals to 1', () => {
    component.timesheetPage.set(3);
    component.attendancePage.set(2);
    component.leavePage.set(4);
    component.setTab('dashboard');
    expect(component.timesheetPage()).toBe(1);
    expect(component.attendancePage()).toBe(1);
    expect(component.leavePage()).toBe(1);
  });

  it('should have all expected tab keys', () => {
    const keys = component.tabs.map(t => t.key);
    expect(keys).toContain('dashboard');
    expect(keys).toContain('timesheet');
    expect(keys).toContain('attendance');
    expect(keys).toContain('leave');
    expect(keys).toContain('profile');
  });

  // ── Computed stats ─────────────────────────────────────────────────────────
  it('approvedCount should count status===1 timesheets', () => {
    component.timesheets.set([makeTs({ status: 1 }), makeTs({ id: 2, status: 0 }), makeTs({ id: 3, status: 1 })]);
    expect(component.approvedTimesheetsCount()).toBe(2);
  });

  it('pendingCount should count status===0 timesheets', () => {
    component.timesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })]);
    expect(component.pendingTimesheetsCount()).toBe(1);
  });

  it('totalHours should sum all hoursWorked', () => {
    component.timesheets.set([
      makeTs({ hoursWorked: 7 }),
      makeTs({ id: 2, hoursWorked: 8 }),
      makeTs({ id: 3, hoursWorked: 5 })
    ]);
    expect(component.totalHoursLogged()).toBe('20.0');
  });

  it('totalHours should be 0.0 for empty list', () => {
    component.timesheets.set([]);
    expect(component.totalHoursLogged()).toBe('0.0');
  });

  // ── Timesheet filtering ────────────────────────────────────────────────────
  it('filteredTs should filter by search query on projectName', () => {
    component.timesheets.set([makeTs({ projectName: 'Alpha' }), makeTs({ id: 2, projectName: 'Beta' })]);
    component.timesheetSearch.set('alpha');
    expect(component.filteredTimesheets().length).toBe(1);
    expect(component.filteredTimesheets()[0].projectName).toBe('Alpha');
  });

  it('filteredTs should filter by pending status', () => {
    component.timesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })]);
    component.timesheetStatusFilter.set('pending');
    expect(component.filteredTimesheets().length).toBe(1);
  });

  it('filteredTs should filter by approved status', () => {
    component.timesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })]);
    component.timesheetStatusFilter.set('approved');
    expect(component.filteredTimesheets().length).toBe(1);
    expect(component.filteredTimesheets()[0].status).toBe(1);
  });

  it('filteredTs should filter by rejected status', () => {
    component.timesheets.set([makeTs({ status: 2 }), makeTs({ id: 2, status: 1 })]);
    component.timesheetStatusFilter.set('rejected');
    expect(component.filteredTimesheets().length).toBe(1);
    expect(component.filteredTimesheets()[0].status).toBe(2);
  });

  it('filteredTs should return all when status is "all"', () => {
    component.timesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 }), makeTs({ id: 3, status: 2 })]);
    component.timesheetStatusFilter.set('all');
    expect(component.filteredTimesheets().length).toBe(3);
  });

  // ── Timesheet sorting ──────────────────────────────────────────────────────
  it('sortTs should toggle direction on same column', () => {
    component.timesheetSortColumn.set('date');
    component.timesheetSortDirection.set('asc');
    component.sortTimesheets('date');
    expect(component.timesheetSortDirection()).toBe('desc');
  });

  it('sortTs should change column and reset to asc', () => {
    component.timesheetSortColumn.set('date');
    component.sortTimesheets('hours');
    expect(component.timesheetSortColumn()).toBe('hours');
    expect(component.timesheetSortDirection()).toBe('asc');
  });

  it('sortTs should reset page to 1', () => {
    component.timesheetPage.set(3);
    component.sortTimesheets('project');
    expect(component.timesheetPage()).toBe(1);
  });

  // ── Timesheet pagination ───────────────────────────────────────────────────
  it('tsTotalPages should be 1 when no timesheets', () => {
    component.timesheets.set([]);
    expect(component.timesheetTotalPages()).toBe(1);
  });

  it('tsTotalPages should calculate correctly for tsPS=6', () => {
    component.timesheets.set(Array.from({ length: 13 }, (_, i) => makeTs({ id: i + 1 })));
    expect(component.timesheetTotalPages()).toBe(3);
  });

  it('pagedTs should return max tsPS items', () => {
    component.timesheets.set(Array.from({ length: 13 }, (_, i) => makeTs({ id: i + 1 })));
    expect(component.pagedTimesheets().length).toBe(6);
  });

  it('pagedTs page 2 should return second slice', () => {
    component.timesheets.set(Array.from({ length: 13 }, (_, i) => makeTs({ id: i + 1 })));
    component.timesheetPage.set(2);
    expect(component.pagedTimesheets()[0].id).toBe(7);
  });

  // ── Attendance pagination ──────────────────────────────────────────────────
  it('attTotalPages should calculate correctly', () => {
    component.attendances.set(Array.from({ length: 17 }, (_, i) => makeAtt({ id: i + 1 })));
    expect(component.attendanceTotalPages()).toBe(3);
  });

  it('pagedAtt returns max attPS items', () => {
    component.attendances.set(Array.from({ length: 17 }, (_, i) => makeAtt({ id: i + 1 })));
    expect(component.pagedAttendances().length).toBe(8);
  });

  // ── Leave pagination ───────────────────────────────────────────────────────
  it('lvTotalPages should calculate correctly for lvPS=6', () => {
    component.leaves.set(Array.from({ length: 13 }, (_, i) => makeLeave({ id: i + 1 })));
    expect(component.leaveTotalPages()).toBe(3);
  });

  // ── ico helper ────────────────────────────────────────────────────────────
  it('ico returns ⇅ when column not active', () => expect(component.getSortIcon(false, 'asc')).toBe('⇅'));
  it('ico returns ↑ for asc active column',  () => expect(component.getSortIcon(true, 'asc')).toBe('↑'));
  it('ico returns ↓ for desc active column', () => expect(component.getSortIcon(true, 'desc')).toBe('↓'));

  // ── stText / stClass ───────────────────────────────────────────────────────
  it('stText(0) → Pending',  () => expect(component.getStatusText(0)).toBe('Pending'));
  it('stText(1) → Approved', () => expect(component.getStatusText(1)).toBe('Approved'));
  it('stText(2) → Rejected', () => expect(component.getStatusText(2)).toBe('Rejected'));
  it('stClass(0) contains "pending"',  () => expect(component.getStatusClass(0)).toContain('pending'));
  it('stClass(1) contains "approved"', () => expect(component.getStatusClass(1)).toContain('approved'));
  it('stClass(2) contains "rejected"', () => expect(component.getStatusClass(2)).toContain('rejected'));

  // ── leaveDays ─────────────────────────────────────────────────────────────
  it('leaveDays should return 1 for same-day leave', () => {
    const l = makeLeave({ fromDate: '2024-06-10', toDate: '2024-06-10' });
    expect(component.leaveDays(l)).toBe(1);
  });

  it('leaveDays should return 3 for 3-day leave', () => {
    const l = makeLeave({ fromDate: '2024-06-10', toDate: '2024-06-12' });
    expect(component.leaveDays(l)).toBe(3);
  });

  it('leaveDays should return 7 for a week leave', () => {
    const l = makeLeave({ fromDate: '2024-06-10', toDate: '2024-06-16' });
    expect(component.leaveDays(l)).toBe(7);
  });

  // ── pages helper ──────────────────────────────────────────────────────────
  it('pages(5) should return [1,2,3,4,5]', () => expect(component.pages(5)).toEqual([1,2,3,4,5]));
  it('pages(0) should return []',          () => expect(component.pages(0)).toEqual([]));

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

  it('checkIn should handle error and set attLoading false', fakeAsync(() => {
    component.todayAtt.set(null);
    attSpy.checkIn.and.returnValue(throwError(() => ({ error: { message: 'Server error' } })));
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

  it('checkOut should call attSvc.checkOut when checked in but not out', fakeAsync(() => {
    component.todayAtt.set(makeAtt({ checkIn: '09:00', checkOut: undefined }));
    attSpy.checkOut.and.returnValue(of({ checkIn: '09:00', checkOut: '17:00', totalHours: '8' }));
    component.checkOut();
    tick();
    expect(attSpy.checkOut).toHaveBeenCalled();
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  it('checkOut should set attLoading false on error', fakeAsync(() => {
    component.todayAtt.set(makeAtt({ checkIn: '09:00' }));
    attSpy.checkOut.and.returnValue(throwError(() => ({ error: { message: 'Fail' } })));
    component.checkOut();
    tick();
    expect(component.attLoading()).toBeFalse();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── submitTimesheet validation ─────────────────────────────────────────────
  it('submitTimesheet should not call API when form is invalid', () => {
    component.tsForm.controls['projectId'].setValue('');
    component.submitTimesheet();
    expect(tsSpy.create).not.toHaveBeenCalled();
  });

  it('submitTimesheet should show error when project not found in assignments', () => {
    component.projectsAssignment.set([]);
    component.tsForm.patchValue({ projectId: '999', workDate: component.todayDate, startTime: '09:00', endTime: '17:00' });
    component.submitTimesheet();
    expect(toastSpy.error).toHaveBeenCalled();
  });

  it('submitTimesheet should call tsSvc.create with correct payload', fakeAsync(() => {
    const asgn: ProjectAssignment = { id: 1, projectId: 10, projectName: 'Alpha' };
    component.projectsAssignment.set([asgn]);
    component.tsForm.patchValue({
      projectId: '1', projectName: 'Alpha',
      workDate: component.todayDate,
      startTime: '09:00', endTime: '17:00', breakTime: '01:00',
      taskDescription: 'Work done'
    });
    tsSpy.create.and.returnValue(of({}));
    tsSpy.getByUser.and.returnValue(of([]));
    component.submitTimesheet();
    tick();
    expect(tsSpy.create).toHaveBeenCalledWith(1, jasmine.objectContaining({
      projectId: 10, projectName: 'Alpha'
    }));
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.showTsModal()).toBeFalse();
  }));

  it('submitTimesheet should show error on API failure', fakeAsync(() => {
    const asgn: ProjectAssignment = { id: 1, projectId: 10, projectName: 'Alpha' };
    component.projectsAssignment.set([asgn]);
    component.tsForm.patchValue({ projectId: '1', workDate: component.todayDate, startTime: '09:00', endTime: '17:00' });
    tsSpy.create.and.returnValue(throwError(() => ({ error: { message: 'Error' } })));
    component.submitTimesheet();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── openEditTs ────────────────────────────────────────────────────────────
  it('openEditTs should warn when timesheet is not pending', () => {
    const ts = makeTs({ status: 1 });
    component.openEditTs(ts);
    expect(toastSpy.warning).toHaveBeenCalled();
    expect(component.showEditModal()).toBeFalse();
  });

  it('openEditTs should open modal for pending timesheet', () => {
    const ts = makeTs({ status: 0, date: '2024-06-01', startTime: '09:00:00', endTime: '17:00:00' });
    component.openEditTs(ts);
    expect(component.editTs()).toBe(ts);
    expect(component.showEditModal()).toBeTrue();
  });

  it('openEditTs should populate editTsForm', () => {
    const ts = makeTs({ status: 0, date: '2024-06-01T00:00:00', startTime: '09:00:00', endTime: '17:00:00' });
    component.openEditTs(ts);
    expect(component.editTsForm.value.startTime).toBe('09:00');
    expect(component.editTsForm.value.endTime).toBe('17:00');
  });

  // ── confirmDeleteTs ────────────────────────────────────────────────────────
  it('confirmDeleteTs should open confirm dialog', () => {
    component.confirmDeleteTs(makeTs());
    expect(component.confirmVisible()).toBeTrue();
    expect(component.confirmTitle()).toContain('Delete');
  });

  it('onCfgOk should execute delete action', fakeAsync(() => {
    tsSpy.delete.and.returnValue(of({}));
    tsSpy.getByUser.and.returnValue(of([]));
    component.confirmDeleteTs(makeTs());
    component.onConfirmOk();
    tick();
    expect(tsSpy.delete).toHaveBeenCalledWith(1);
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  it('onCfgCancel should close confirm dialog', () => {
    component.confirmVisible.set(true);
    component.onConfirmCancel();
    expect(component.confirmVisible()).toBeFalse();
  });

  // ── submitLeave ───────────────────────────────────────────────────────────
  it('submitLeave should not call API when form invalid', () => {
    component.leaveForm.controls['leaveTypeId'].setValue('');
    component.submitLeave();
    expect(lvSpy.apply).not.toHaveBeenCalled();
  });

  it('submitLeave should call lvSvc.apply with correct data', fakeAsync(() => {
    component.leaveForm.setValue({ leaveTypeId: '2', fromDate: '2024-07-01', toDate: '2024-07-03', reason: 'Vacation' });
    lvSpy.apply.and.returnValue(of({}));
    lvSpy.getMyLeaves.and.returnValue(of([]));
    component.submitLeave();
    tick();
    expect(lvSpy.apply).toHaveBeenCalledWith(1, jasmine.objectContaining({
      leaveTypeId: 2, fromDate: '2024-07-01', toDate: '2024-07-03', reason: 'Vacation'
    }));
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.showLeaveModal()).toBeFalse();
  }));

  it('submitLeave should show error on failure', fakeAsync(() => {
    component.leaveForm.setValue({ leaveTypeId: '2', fromDate: '2024-07-01', toDate: '2024-07-03', reason: '' });
    lvSpy.apply.and.returnValue(throwError(() => ({ error: { message: 'Failed' } })));
    component.submitLeave();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── onProjectChange ───────────────────────────────────────────────────────
  it('onProjectChange should patch projectName when assignment found', () => {
    const asgn: ProjectAssignment = { id: 1, projectId: 10, projectName: 'Alpha Project' };
    component.projectsAssignment.set([asgn]);
    component.tsForm.patchValue({ projectId: '1' });
    component.onProjectChange();
    expect(component.tsForm.value.projectName).toBe('Alpha Project');
  });

  it('onProjectChange should not patch when assignment not found', () => {
    component.projectsAssignment.set([]);
    component.tsForm.patchValue({ projectId: '999', projectName: 'Keep This' });
    component.onProjectChange();
    expect(component.tsForm.value.projectName).toBe('Keep This');
  });
});
