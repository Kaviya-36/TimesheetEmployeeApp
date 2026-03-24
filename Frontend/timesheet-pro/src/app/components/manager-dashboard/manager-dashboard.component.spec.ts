import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { ManagerDashboardComponent } from './manager-dashboard.component';
import { AuthService }         from '../../services/auth.service';
import { ToastService }        from '../../services/toast.service';
import { BreadcrumbService }   from '../../services/breadcrumb.service';
import { NotificationService } from '../../services/notification.service';
import { TimesheetService, LeaveService, UserService, ProjectService } from '../../services/api.services';
import { Timesheet, Leave, User, Project } from '../../models';

const makeTs = (overrides: Partial<Timesheet> = {}): Timesheet => ({
  id: 1, employeeName: 'Alice', employeeId: 'E001',
  projectName: 'Project A', date: '2024-06-01',
  startTime: '09:00', endTime: '17:00', breakTime: '01:00',
  hoursWorked: 7, status: 0, ...overrides
});

const makeLeave = (overrides: Partial<Leave> = {}): Leave => ({
  id: 1, employeeName: 'Alice', leaveType: 'Annual',
  fromDate: '2024-06-10', toDate: '2024-06-12', status: 0, ...overrides
});

const makeUser = (overrides: Partial<User> = {}): User => ({
  id: 1, employeeId: 'E001', name: 'Alice', email: 'alice@test.com',
  phone: '', role: 'Employee', status: 'Active', joiningDate: '2024-01-01',
  ...overrides
});

describe('ManagerDashboardComponent', () => {
  let component: ManagerDashboardComponent;
  let fixture:   ComponentFixture<ManagerDashboardComponent>;

  let authSpy:   jasmine.SpyObj<AuthService>;
  let toastSpy:  jasmine.SpyObj<ToastService>;
  let tsSpy:     jasmine.SpyObj<TimesheetService>;
  let lvSpy:     jasmine.SpyObj<LeaveService>;
  let usrSpy:    jasmine.SpyObj<UserService>;
  let prjSpy:    jasmine.SpyObj<ProjectService>;
  let notifSpy:  jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    authSpy  = jasmine.createSpyObj('AuthService', ['logout'], {
      currentUser: signal<number | null>(2),
      currentRole: signal<any>('Manager'),
      username:    signal<string | null>('manager1'),
      isLoggedIn:  () => true,
      token:       signal<string | null>('tok')
    });
    toastSpy = jasmine.createSpyObj('ToastService',  ['success','error','warning','info']);
    tsSpy    = jasmine.createSpyObj('TimesheetService', ['getAll','approveOrReject']);
    lvSpy    = jasmine.createSpyObj('LeaveService',   ['getAll','approveOrReject']);
    usrSpy   = jasmine.createSpyObj('UserService',    ['getAll']);
    prjSpy   = jasmine.createSpyObj('ProjectService', ['getAll','assign']);
    notifSpy = jasmine.createSpyObj('NotificationService', ['pushLocal','connect','markAllRead','markRead'], {
      notifications: signal([]), unreadCount: signal(0), connected: signal(false)
    });

    tsSpy.getAll.and.returnValue(of({ data: { data: [] } }));
    lvSpy.getAll.and.returnValue(of([]));
    usrSpy.getAll.and.returnValue(of([]));
    prjSpy.getAll.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [ManagerDashboardComponent, ReactiveFormsModule, FormsModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService,         useValue: authSpy  },
        { provide: ToastService,        useValue: toastSpy },
        { provide: TimesheetService,    useValue: tsSpy    },
        { provide: LeaveService,        useValue: lvSpy    },
        { provide: UserService,         useValue: usrSpy   },
        { provide: ProjectService,      useValue: prjSpy   },
        { provide: NotificationService, useValue: notifSpy },
        BreadcrumbService
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(ManagerDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Creation ───────────────────────────────────────────────────────────────
  it('should create', () => expect(component).toBeTruthy());
  it('should default to dashboard tab', () => expect(component.activeTab()).toBe('dashboard'));
  it('should have 5 tabs', () => expect(component.tabs.length).toBe(5));

  // ── setTab ─────────────────────────────────────────────────────────────────
  it('setTab should change activeTab', () => {
    component.setTab('timesheets');
    expect(component.activeTab()).toBe('timesheets');
  });

  it('setTab should reset all pages to 1', () => {
    component.tsPage.set(3);
    component.lvPage.set(2);
    component.teamPage.set(4);
    component.setTab('dashboard');
    expect(component.tsPage()).toBe(1);
    expect(component.lvPage()).toBe(1);
    expect(component.teamPage()).toBe(1);
  });

  // ── Computed stats ─────────────────────────────────────────────────────────
  it('pendingTs should return only status=0 timesheets', () => {
    component.allTimesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })]);
    expect(component.pendingTs().length).toBe(1);
  });

  it('pendingLv should return only status=0 leaves', () => {
    component.allLeaves.set([makeLeave({ status: 0 }), makeLeave({ id: 2, status: 1 })]);
    expect(component.pendingLv().length).toBe(1);
  });

  it('totalHours should sum approved timesheet hours', () => {
    component.allTimesheets.set([
      makeTs({ status: 1, hoursWorked: 8 }),
      makeTs({ id: 2, status: 1, hoursWorked: 7 }),
      makeTs({ id: 3, status: 0, hoursWorked: 6 })
    ]);
    expect(component.totalHours()).toBe('15.0');
  });

  // ── Timesheet filtering ────────────────────────────────────────────────────
  it('filteredTs should filter by employee name search', () => {
    component.allTimesheets.set([makeTs({ employeeName: 'Alice' }), makeTs({ id: 2, employeeName: 'Bob' })]);
    component.tsSearch.set('alice');
    expect(component.filteredTs().length).toBe(1);
  });

  it('filteredTs should filter by project name search', () => {
    component.allTimesheets.set([makeTs({ projectName: 'Alpha' }), makeTs({ id: 2, projectName: 'Beta' })]);
    component.tsSearch.set('beta');
    expect(component.filteredTs().length).toBe(1);
    expect(component.filteredTs()[0].projectName).toBe('Beta');
  });

  it('filteredTs should filter by status=pending', () => {
    component.allTimesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })]);
    component.tsStatus.set('pending');
    expect(component.filteredTs().length).toBe(1);
    expect(Number(component.filteredTs()[0].status)).toBe(0);
  });

  it('filteredTs should filter by status=approved', () => {
    component.allTimesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })]);
    component.tsStatus.set('approved');
    expect(component.filteredTs().length).toBe(1);
  });

  it('filteredTs returns all when status is "all"', () => {
    component.allTimesheets.set([makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })]);
    component.tsStatus.set('all');
    expect(component.filteredTs().length).toBe(2);
  });

  // ── Leave filtering ────────────────────────────────────────────────────────
  it('filteredLv should filter by employee name', () => {
    component.allLeaves.set([makeLeave({ employeeName: 'Alice' }), makeLeave({ id: 2, employeeName: 'Bob' })]);
    component.lvSearch.set('bob');
    expect(component.filteredLv().length).toBe(1);
  });

  it('filteredLv should filter by status=pending', () => {
    component.allLeaves.set([makeLeave({ status: 0 }), makeLeave({ id: 2, status: 1 })]);
    component.lvStatus.set('pending');
    expect(component.filteredLv().length).toBe(1);
  });

  // ── Team filtering ─────────────────────────────────────────────────────────
  it('filteredTeam should filter by name', () => {
    component.teamMembers.set([makeUser({ name: 'Alice' }), makeUser({ id: 2, name: 'Bob' })]);
    component.teamSearch.set('alice');
    expect(component.filteredTeam().length).toBe(1);
  });

  it('filteredTeam returns all when search empty', () => {
    component.teamMembers.set([makeUser(), makeUser({ id: 2 })]);
    component.teamSearch.set('');
    expect(component.filteredTeam().length).toBe(2);
  });

  // ── Sorting ────────────────────────────────────────────────────────────────
  it('sortTs should toggle direction on same column', () => {
    component.tsSortCol.set('date');
    component.tsSortDir.set('asc');
    component.sortTs('date');
    expect(component.tsSortDir()).toBe('desc');
  });

  it('sortTs should change column and reset to asc', () => {
    component.sortTs('employee');
    expect(component.tsSortCol()).toBe('employee');
    expect(component.tsSortDir()).toBe('asc');
  });

  // ── Pagination ─────────────────────────────────────────────────────────────
  it('tsTotalPages is 1 for empty list', () => {
    component.allTimesheets.set([]);
    expect(component.tsTotalPages()).toBe(1);
  });

  it('tsTotalPages calculates correctly for tsPS=8', () => {
    component.allTimesheets.set(Array.from({ length: 20 }, (_, i) => makeTs({ id: i + 1 })));
    expect(component.tsTotalPages()).toBe(3);
  });

  it('pagedTs returns max tsPS items', () => {
    component.allTimesheets.set(Array.from({ length: 20 }, (_, i) => makeTs({ id: i + 1 })));
    expect(component.pagedTs().length).toBe(8);
  });

  // ── reviewTs ──────────────────────────────────────────────────────────────
  it('reviewTs should open confirm dialog', () => {
    component.reviewTs(makeTs(), true);
    expect(component.cfgVisible()).toBeTrue();
  });

  it('reviewTs confirm approve should call tsSvc.approveOrReject with isApproved=true', fakeAsync(() => {
    tsSpy.approveOrReject.and.returnValue(of({}));
    tsSpy.getAll.and.returnValue(of({ data: { data: [] } }));
    component.reviewTs(makeTs(), true);
    component.onCfgOk();
    tick();
    const call = tsSpy.approveOrReject.calls.mostRecent().args[0];
    expect(call.isApproved).toBeTrue();
    expect(call.timesheetId).toBe(1);
  }));

  it('reviewTs confirm reject should call tsSvc.approveOrReject with isApproved=false', fakeAsync(() => {
    tsSpy.approveOrReject.and.returnValue(of({}));
    tsSpy.getAll.and.returnValue(of({ data: { data: [] } }));
    component.reviewTs(makeTs(), false);
    component.onCfgOk();
    tick();
    const call = tsSpy.approveOrReject.calls.mostRecent().args[0];
    expect(call.isApproved).toBeFalse();
  }));

  it('reviewTs should show error on API failure', fakeAsync(() => {
    tsSpy.approveOrReject.and.returnValue(throwError(() => ({ error: { message: 'Fail' } })));
    component.reviewTs(makeTs(), true);
    component.onCfgOk();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── reviewLeave ───────────────────────────────────────────────────────────
  it('reviewLeave should open confirm dialog', () => {
    component.reviewLeave(makeLeave(), true);
    expect(component.cfgVisible()).toBeTrue();
  });

  it('reviewLeave confirm approve should call lvSvc.approveOrReject with isApproved=true', fakeAsync(() => {
    lvSpy.approveOrReject.and.returnValue(of({}));
    lvSpy.getAll.and.returnValue(of([]));
    component.reviewLeave(makeLeave(), true);
    component.onCfgOk();
    tick();
    const call = lvSpy.approveOrReject.calls.mostRecent().args[0];
    expect(call.isApproved).toBeTrue();
  }));

  it('reviewLeave confirm reject sets isApproved=false', fakeAsync(() => {
    lvSpy.approveOrReject.and.returnValue(of({}));
    lvSpy.getAll.and.returnValue(of([]));
    component.reviewLeave(makeLeave(), false);
    component.onCfgOk();
    tick();
    const call = lvSpy.approveOrReject.calls.mostRecent().args[0];
    expect(call.isApproved).toBeFalse();
  }));

  // ── assignUserToProject ────────────────────────────────────────────────────
  it('assignUserToProject should warn when no project selected', () => {
    component.selProjectId = null;
    component.selUserId    = 1;
    component.assignUserToProject();
    expect(toastSpy.warning).toHaveBeenCalled();
    expect(prjSpy.assign).not.toHaveBeenCalled();
  });

  it('assignUserToProject should warn when no user selected', () => {
    component.selProjectId = 1;
    component.selUserId    = null;
    component.assignUserToProject();
    expect(toastSpy.warning).toHaveBeenCalled();
    expect(prjSpy.assign).not.toHaveBeenCalled();
  });

  it('assignUserToProject should call prjSvc.assign when both selected', fakeAsync(() => {
    const project: Project = { id: 5, projectName: 'CRM' };
    component.projects.set([project]);
    component.selProjectId = 5;
    component.selUserId    = 3;
    prjSpy.assign.and.returnValue(of({}));
    component.assignUserToProject();
    tick();
    expect(prjSpy.assign).toHaveBeenCalledWith(jasmine.objectContaining({
      projectId: 5, userId: 3, projectName: 'CRM'
    }));
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  it('assignUserToProject should reset selectors after success', fakeAsync(() => {
    component.projects.set([{ id: 5, projectName: 'CRM' }]);
    component.selProjectId = 5;
    component.selUserId    = 3;
    prjSpy.assign.and.returnValue(of({}));
    component.assignUserToProject();
    tick();
    expect(component.selProjectId).toBeNull();
    expect(component.selUserId).toBeNull();
  }));

  // ── onCfgCancel ───────────────────────────────────────────────────────────
  it('onCfgCancel should close confirm dialog', () => {
    component.cfgVisible.set(true);
    component.onCfgCancel();
    expect(component.cfgVisible()).toBeFalse();
  });

  // ── stText / stClass / ico / pages ────────────────────────────────────────
  it('stText(0) → Pending',  () => expect(component.stText(0)).toBe('Pending'));
  it('stText(1) → Approved', () => expect(component.stText(1)).toBe('Approved'));
  it('stText(2) → Rejected', () => expect(component.stText(2)).toBe('Rejected'));
  it('stClass(0) contains "pending"',  () => expect(component.stClass(0)).toContain('pending'));
  it('stClass(1) contains "approved"', () => expect(component.stClass(1)).toContain('approved'));
  it('ico(false,…) returns ⇅',  () => expect(component.ico(false,'asc')).toBe('⇅'));
  it('ico(true,asc) returns ↑', () => expect(component.ico(true,'asc')).toBe('↑'));
  it('ico(true,desc) returns ↓',() => expect(component.ico(true,'desc')).toBe('↓'));
  it('pages(3) returns [1,2,3]', () => expect(component.pages(3)).toEqual([1,2,3]));
});
