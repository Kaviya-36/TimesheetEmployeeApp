import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { AdminDashboardComponent } from './admin-dashboard.component';
import { AuthService }         from '../../services/auth.service';
import { ToastService }        from '../../services/toast.service';
import { BreadcrumbService }   from '../../services/breadcrumb.service';
import { NotificationService } from '../../services/notification.service';
import {
  UserService, ProjectService, TimesheetService, AnalyticsService, LeaveService
} from '../../services/api.services';
import { User, Project, Timesheet, Leave } from '../../models';

const makeUser = (overrides: Partial<User> = {}): User => ({
  id: 1, employeeId: 'E001', name: 'Alice', email: 'alice@test.com',
  phone: '123', role: 'Employee', status: 'Active', joiningDate: '2024-01-01',
  ...overrides
});

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

describe('AdminDashboardComponent', () => {
  let component: AdminDashboardComponent;
  let fixture: ComponentFixture<AdminDashboardComponent>;
  let authSpy:    jasmine.SpyObj<AuthService>;
  let toastSpy:   jasmine.SpyObj<ToastService>;
  let userSpy:    jasmine.SpyObj<UserService>;
  let prjSpy:     jasmine.SpyObj<ProjectService>;
  let tsSpy:      jasmine.SpyObj<TimesheetService>;
  let anlSpy:     jasmine.SpyObj<AnalyticsService>;
  let lvSpy:      jasmine.SpyObj<LeaveService>;
  let notifSpy:   jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    authSpy  = jasmine.createSpyObj('AuthService', ['register', 'logout'], {
      currentUser: signal<number | null>(1),
      currentRole: signal<any>('Admin'),
      username:    signal<string | null>('admin'),
      isLoggedIn:  () => true,
      token:       signal<string | null>('tok')
    });
    toastSpy   = jasmine.createSpyObj('ToastService', ['success', 'error', 'warning', 'info']);
    userSpy    = jasmine.createSpyObj('UserService', ['getAll', 'update', 'delete', 'setActive']);
    prjSpy     = jasmine.createSpyObj('ProjectService', ['getAll', 'create', 'delete']);
    tsSpy      = jasmine.createSpyObj('TimesheetService', ['getAll']);
    anlSpy     = jasmine.createSpyObj('AnalyticsService', ['getDashboard']);
    lvSpy      = jasmine.createSpyObj('LeaveService', ['getAll', 'approveOrReject']);
    notifSpy   = jasmine.createSpyObj('NotificationService', ['pushLocal', 'connect', 'markAllRead', 'markRead'], {
      notifications: signal([]), unreadCount: signal(0), connected: signal(false)
    });

    userSpy.getAll.and.returnValue(of([]));
    prjSpy.getAll.and.returnValue(of([]));
    tsSpy.getAll.and.returnValue(of({ data: { data: [] } }));
    anlSpy.getDashboard.and.returnValue(of({
      timesheetsPending: 0, timesheetsApproved: 0, leavesPending: 0,
      checkedInToday: 0, lateToday: 0
    }));
    lvSpy.getAll.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [AdminDashboardComponent, ReactiveFormsModule, FormsModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService,         useValue: authSpy  },
        { provide: ToastService,        useValue: toastSpy },
        { provide: UserService,         useValue: userSpy  },
        { provide: ProjectService,      useValue: prjSpy   },
        { provide: TimesheetService,    useValue: tsSpy    },
        { provide: AnalyticsService,    useValue: anlSpy   },
        { provide: LeaveService,        useValue: lvSpy    },
        { provide: NotificationService, useValue: notifSpy },
        BreadcrumbService
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(AdminDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Creation ───────────────────────────────────────────────────────────────
  it('should create', () => expect(component).toBeTruthy());
  it('should default to overview tab', () => expect(component.activeTab()).toBe('overview'));
  it('should have 6 tabs', () => expect(component.tabs.length).toBe(6));

  // ── setTab ─────────────────────────────────────────────────────────────────
  it('setTab should change active tab', () => {
    component.setTab('users');
    expect(component.activeTab()).toBe('users');
  });

  it('setTab should reset all page signals to 1', () => {
    component.tsPage.set(3);
    component.lvPage.set(4);
    component.uPage.set(2);
    component.setTab('overview');
    expect(component.tsPage()).toBe(1);
    expect(component.lvPage()).toBe(1);
    expect(component.uPage()).toBe(1);
  });

  // ── Computed stats ─────────────────────────────────────────────────────────
  it('totalUsers should count all users', () => {
    component.allUsers.set([makeUser(), makeUser({ id: 2, status: 'Inactive' })]);
    expect(component.totalUsers()).toBe(2);
  });

  it('activeUsers should count only Active users', () => {
    component.allUsers.set([
      makeUser({ status: 'Active' }),
      makeUser({ id: 2, status: 'Inactive' }),
      makeUser({ id: 3, status: 'Active' })
    ]);
    expect(component.activeUsers()).toBe(2);
  });

  it('inactiveUsers should count non-Active users', () => {
    component.allUsers.set([
      makeUser({ status: 'Active' }),
      makeUser({ id: 2, status: 'Inactive' })
    ]);
    expect(component.inactiveUsers()).toBe(1);
  });

  it('pendingTs should count timesheets with status 0', () => {
    component.allTimesheets.set([
      makeTs({ status: 0 }), makeTs({ id: 2, status: 1 }), makeTs({ id: 3, status: 0 })
    ]);
    expect(component.pendingTs()).toBe(2);
  });

  it('pendingLv should count leaves with status 0', () => {
    component.allLeaves.set([makeLeave({ status: 0 }), makeLeave({ id: 2, status: 1 })]);
    expect(component.pendingLv()).toBe(1);
  });

  it('managers computed returns only Manager role users', () => {
    component.allUsers.set([
      makeUser({ role: 'Manager' }),
      makeUser({ id: 2, role: 'Employee' }),
      makeUser({ id: 3, role: 'Manager' })
    ]);
    expect(component.managers().length).toBe(2);
  });

  // ── User filtering ─────────────────────────────────────────────────────────
  it('filteredUsers should filter by search query', () => {
    component.allUsers.set([
      makeUser({ name: 'Alice Smith' }),
      makeUser({ id: 2, name: 'Bob Jones' })
    ]);
    component.uSearch.set('alice');
    expect(component.filteredUsers().length).toBe(1);
    expect(component.filteredUsers()[0].name).toBe('Alice Smith');
  });

  it('filteredUsers should filter by role', () => {
    component.allUsers.set([
      makeUser({ role: 'Employee' }),
      makeUser({ id: 2, role: 'HR' })
    ]);
    component.uRole.set('HR');
    expect(component.filteredUsers().length).toBe(1);
    expect(component.filteredUsers()[0].role).toBe('HR');
  });

  it('filteredUsers should filter by active status', () => {
    component.allUsers.set([
      makeUser({ status: 'Active' }),
      makeUser({ id: 2, status: 'Inactive' })
    ]);
    component.uStatus.set('active');
    expect(component.filteredUsers().length).toBe(1);
    expect(component.filteredUsers()[0].status).toBe('Active');
  });

  it('filteredUsers should return all when filter is "all"', () => {
    component.allUsers.set([makeUser(), makeUser({ id: 2 })]);
    component.uRole.set('all');
    component.uStatus.set('all');
    component.uSearch.set('');
    expect(component.filteredUsers().length).toBe(2);
  });

  // ── Pagination ─────────────────────────────────────────────────────────────
  it('uTotalPages should be 1 for empty list', () => {
    component.allUsers.set([]);
    expect(component.uTotalPages()).toBe(1);
  });

  it('uTotalPages should calculate correctly', () => {
    component.allUsers.set(Array.from({ length: 20 }, (_, i) => makeUser({ id: i + 1 })));
    expect(component.uTotalPages()).toBe(3); // 20 / 8 = 2.5 → 3
  });

  it('pagedUsers should return only uPS items per page', () => {
    component.allUsers.set(Array.from({ length: 20 }, (_, i) => makeUser({ id: i + 1 })));
    expect(component.pagedUsers().length).toBe(8);
  });

  it('pagedUsers page 2 should return next set', () => {
    component.allUsers.set(Array.from({ length: 20 }, (_, i) => makeUser({ id: i + 1 })));
    component.uPage.set(2);
    expect(component.pagedUsers()[0].id).toBe(9);
  });

  // ── Sort helpers ───────────────────────────────────────────────────────────
  it('sortU should toggle sort direction when same column clicked', () => {
    component.uSort.set('name');
    component.uSortDir.set('asc');
    component.sortU('name');
    expect(component.uSortDir()).toBe('desc');
  });

  it('sortU should reset to asc when new column selected', () => {
    component.uSort.set('name');
    component.uSortDir.set('desc');
    component.sortU('role');
    expect(component.uSort()).toBe('role');
    expect(component.uSortDir()).toBe('asc');
  });

  it('ico should return ⇅ for inactive column', () => {
    expect(component.ico(false, 'asc')).toBe('⇅');
  });

  it('ico should return ↑ for asc active column', () => {
    expect(component.ico(true, 'asc')).toBe('↑');
  });

  it('ico should return ↓ for desc active column', () => {
    expect(component.ico(true, 'desc')).toBe('↓');
  });

  // ── Confirm dialog ─────────────────────────────────────────────────────────
  it('confirmDeleteUser should open confirm dialog', () => {
    component.confirmDeleteUser(makeUser());
    expect(component.cfgVisible()).toBeTrue();
  });

  it('confirmDeleteProject should open confirm dialog', () => {
    const project: Project = { id: 1, projectName: 'Test Project' };
    component.confirmDeleteProject(project);
    expect(component.cfgVisible()).toBeTrue();
  });

  it('onCfgCancel should close confirm dialog', () => {
    component.cfgVisible.set(true);
    component.onCfgCancel();
    expect(component.cfgVisible()).toBeFalse();
  });

  it('toggleActive should open confirm dialog', () => {
    component.toggleActive(makeUser({ status: 'Active' }));
    expect(component.cfgVisible()).toBeTrue();
  });

  // ── toggleActive action ────────────────────────────────────────────────────
  it('onCfgOk after toggleActive should call userSvc.setActive', fakeAsync(() => {
    userSpy.setActive.and.returnValue(of({}));
    userSpy.getAll.and.returnValue(of([]));
    component.toggleActive(makeUser({ status: 'Active' }));
    component.onCfgOk();
    tick();
    expect(userSpy.setActive).toHaveBeenCalledWith(1, false);
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  // ── Delete user action ─────────────────────────────────────────────────────
  it('onCfgOk after confirmDeleteUser should call userSvc.delete', fakeAsync(() => {
    userSpy.delete.and.returnValue(of({}));
    userSpy.getAll.and.returnValue(of([]));
    component.confirmDeleteUser(makeUser());
    component.onCfgOk();
    tick();
    expect(userSpy.delete).toHaveBeenCalledWith(1);
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  it('should call toastSpy.error when delete fails', fakeAsync(() => {
    userSpy.delete.and.returnValue(throwError(() => ({ error: { message: 'Failed' } })));
    component.confirmDeleteUser(makeUser());
    component.onCfgOk();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── openEditUser / saveRole ────────────────────────────────────────────────
  it('openEditUser should set editUser and newRole', () => {
    const u = makeUser({ role: 'HR' });
    component.openEditUser(u);
    expect(component.editUser()).toBe(u);
    expect(component.newRole).toBe('HR');
  });

  it('saveRole should call userSvc.update', fakeAsync(() => {
    userSpy.update.and.returnValue(of({} as any));
    userSpy.getAll.and.returnValue(of([]));
    component.editUser.set(makeUser());
    component.newRole = 'Manager';
    component.saveRole();
    tick();
    expect(userSpy.update).toHaveBeenCalledWith(1, { role: 'Manager' });
  }));

  // ── Leave actions ──────────────────────────────────────────────────────────
  it('approveLeave should call lvSvc.approveOrReject with isApproved:true', fakeAsync(() => {
    lvSpy.approveOrReject.and.returnValue(of({}));
    lvSpy.getAll.and.returnValue(of([]));
    component.approveLeave(makeLeave());
    tick();
    const call = lvSpy.approveOrReject.calls.mostRecent().args[0];
    expect(call.isApproved).toBeTrue();
  }));

  it('rejectLeave should call lvSvc.approveOrReject with isApproved:false', fakeAsync(() => {
    lvSpy.approveOrReject.and.returnValue(of({}));
    lvSpy.getAll.and.returnValue(of([]));
    component.rejectLeave(makeLeave());
    tick();
    const call = lvSpy.approveOrReject.calls.mostRecent().args[0];
    expect(call.isApproved).toBeFalse();
  }));

  // ── Settings ───────────────────────────────────────────────────────────────
  it('saveSettings should call toast.success', () => {
    component.saveSettings();
    expect(toastSpy.success).toHaveBeenCalled();
  });

  it('saveSettings should persist to localStorage', () => {
    component.settings.startTime = '08:00';
    component.saveSettings();
    const saved = JSON.parse(localStorage.getItem('admin_settings') || '{}');
    expect(saved.startTime).toBe('08:00');
  });

  // ── stText / stClass ───────────────────────────────────────────────────────
  it('stText(0) should return Pending', () => expect(component.stText(0)).toBe('Pending'));
  it('stText(1) should return Approved', () => expect(component.stText(1)).toBe('Approved'));
  it('stText(2) should return Rejected', () => expect(component.stText(2)).toBe('Rejected'));
  it('stClass(0) should return pending badge class', () => expect(component.stClass(0)).toContain('pending'));
  it('stClass(1) should return approved badge class', () => expect(component.stClass(1)).toContain('approved'));
  it('stClass(2) should return rejected badge class', () => expect(component.stClass(2)).toContain('rejected'));

  // ── pages helper ──────────────────────────────────────────────────────────
  it('pages(3) should return [1, 2, 3]', () => {
    expect(component.pages(3)).toEqual([1, 2, 3]);
  });

  it('pages(0) should return []', () => {
    expect(component.pages(0)).toEqual([]);
  });

  // ── TS filtering / sorting ─────────────────────────────────────────────────
  it('filteredTs should filter by status pending', () => {
    component.allTimesheets.set([
      makeTs({ status: 0 }), makeTs({ id: 2, status: 1 })
    ]);
    component.tsStatus.set('pending');
    expect(component.filteredTs().length).toBe(1);
    expect(component.filteredTs()[0].status).toBe(0);
  });

  it('filteredTs should filter by search query', () => {
    component.allTimesheets.set([
      makeTs({ employeeName: 'Alice' }),
      makeTs({ id: 2, employeeName: 'Bob' })
    ]);
    component.tsSearch.set('alice');
    expect(component.filteredTs().length).toBe(1);
  });

  it('sortTs should toggle sort direction on same column', () => {
    component.tsSortCol.set('date');
    component.tsSortDir.set('asc');
    component.sortTs('date');
    expect(component.tsSortDir()).toBe('desc');
  });

  it('roleBreakdown should include all roles', () => {
    expect(component.roleBreakdown().length).toBe(component.roleOptions.length);
  });
});
