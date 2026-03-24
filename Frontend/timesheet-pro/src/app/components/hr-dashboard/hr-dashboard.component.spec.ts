import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { HrDashboardComponent } from './hr-dashboard.component';
import { AuthService }         from '../../services/auth.service';
import { ToastService }        from '../../services/toast.service';
import { BreadcrumbService }   from '../../services/breadcrumb.service';
import { UserService, AttendanceService, LeaveService, PayrollService } from '../../services/api.services';
import { User, Attendance, Leave, Payroll } from '../../models';

const makeUser = (o: Partial<User> = {}): User => ({
  id: 1, employeeId: 'E001', name: 'Alice', email: 'alice@test.com',
  phone: '123', role: 'Employee', status: 'Active', joiningDate: '2024-01-01', ...o
});

const makeAtt = (o: Partial<Attendance> = {}): Attendance => ({
  id: 1, userId: 1, employeeName: 'Alice', date: new Date().toISOString().split('T')[0],
  checkIn: '09:00', isLate: false, ...o
});

const makeLeave = (o: Partial<Leave> = {}): Leave => ({
  id: 1, employeeName: 'Alice', leaveType: 'Annual',
  fromDate: '2024-06-10', toDate: '2024-06-12', status: 0, ...o
});

const makePayroll = (o: Partial<Payroll> = {}): Payroll => ({
  payrollId: 1, employeeName: 'Alice', employeeId: 'E001',
  basicSalary: 50000, overtimeAmount: 2000, deductions: 1000,
  netSalary: 51000, salaryMonth: '2024-06-01', generatedDate: '2024-06-30', ...o
});

describe('HrDashboardComponent', () => {
  let component: HrDashboardComponent;
  let fixture:   ComponentFixture<HrDashboardComponent>;

  let authSpy:  jasmine.SpyObj<AuthService>;
  let toastSpy: jasmine.SpyObj<ToastService>;
  let usrSpy:   jasmine.SpyObj<UserService>;
  let attSpy:   jasmine.SpyObj<AttendanceService>;
  let lvSpy:    jasmine.SpyObj<LeaveService>;
  let paySpy:   jasmine.SpyObj<PayrollService>;

  beforeEach(async () => {
    authSpy  = jasmine.createSpyObj('AuthService', ['logout'], {
      currentUser: signal<number | null>(3),
      currentRole: signal<any>('HR'),
      username:    signal<string | null>('hruser'),
      isLoggedIn:  () => true,
      token:       signal<string | null>('tok')
    });
    toastSpy = jasmine.createSpyObj('ToastService', ['success','error','warning','info']);
    usrSpy   = jasmine.createSpyObj('UserService',   ['getAll','update','delete','setActive']);
    attSpy   = jasmine.createSpyObj('AttendanceService', ['getAll']);
    lvSpy    = jasmine.createSpyObj('LeaveService',  ['getAll','approveOrReject']);
    paySpy   = jasmine.createSpyObj('PayrollService',['getAll','generate']);

    usrSpy.getAll.and.returnValue(of([]));
    attSpy.getAll.and.returnValue(of([]));
    lvSpy.getAll.and.returnValue(of([]));
    paySpy.getAll.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [HrDashboardComponent, ReactiveFormsModule, FormsModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService,      useValue: authSpy  },
        { provide: ToastService,     useValue: toastSpy },
        { provide: UserService,      useValue: usrSpy   },
        { provide: AttendanceService,useValue: attSpy   },
        { provide: LeaveService,     useValue: lvSpy    },
        { provide: PayrollService,   useValue: paySpy   },
        BreadcrumbService
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(HrDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Creation ───────────────────────────────────────────────────────────────
  it('should create', () => expect(component).toBeTruthy());
  it('should default to employees tab', () => expect(component.activeTab()).toBe('employees'));
  it('should have 5 tabs', () => expect(component.tabs.length).toBe(5));

  // ── setTab ─────────────────────────────────────────────────────────────────
  it('setTab should change activeTab', () => {
    component.setTab('payroll');
    expect(component.activeTab()).toBe('payroll');
  });

  it('setTab should reset all pages to 1', () => {
    component.empPage.set(3);
    component.attPage.set(2);
    component.lvPage.set(4);
    component.payPage.set(5);
    component.setTab('employees');
    expect(component.empPage()).toBe(1);
    expect(component.attPage()).toBe(1);
    expect(component.lvPage()).toBe(1);
    expect(component.payPage()).toBe(1);
  });

  // ── Computed stats ─────────────────────────────────────────────────────────
  it('activeCount should count Active employees', () => {
    component.employees.set([makeUser({ status: 'Active' }), makeUser({ id: 2, status: 'Inactive' }), makeUser({ id: 3, status: 'Active' })]);
    expect(component.activeCount()).toBe(2);
  });

  it('lateToday should count late attendance records', () => {
    const today = new Date().toISOString().split('T')[0];
    component.attendance.set([
      makeAtt({ date: today, isLate: true }),
      makeAtt({ id: 2, date: today, isLate: false }),
      makeAtt({ id: 3, date: today, isLate: true })
    ]);
    expect(component.lateToday()).toBe(2);
  });

  it('pendingLv should count leaves with status 0', () => {
    component.allLeaves.set([makeLeave({ status: 0 }), makeLeave({ id: 2, status: 1 })]);
    expect(component.pendingLv()).toBe(1);
  });

  it('totalPayroll should sum all netSalary', () => {
    component.payrolls.set([makePayroll({ netSalary: 50000 }), makePayroll({ payrollId: 2, netSalary: 60000 })]);
    expect(component.totalPayroll()).toBe(110000);
  });

  // ── Employee filtering ─────────────────────────────────────────────────────
  it('filteredEmps should filter by name', () => {
    component.employees.set([makeUser({ name: 'Alice' }), makeUser({ id: 2, name: 'Bob' })]);
    component.empSearch.set('alice');
    expect(component.filteredEmps().length).toBe(1);
  });

  it('filteredEmps should filter by email', () => {
    component.employees.set([
      makeUser({ email: 'alice@test.com' }),
      makeUser({ id: 2, email: 'bob@test.com' })
    ]);
    component.empSearch.set('bob@');
    expect(component.filteredEmps().length).toBe(1);
  });

  it('filteredEmps should filter by employeeId', () => {
    component.employees.set([makeUser({ employeeId: 'EMP001' }), makeUser({ id: 2, employeeId: 'EMP002' })]);
    component.empSearch.set('emp001');
    expect(component.filteredEmps().length).toBe(1);
  });

  it('filteredEmps should filter by role', () => {
    component.employees.set([makeUser({ role: 'Employee' }), makeUser({ id: 2, role: 'HR' })]);
    component.empRoleF.set('HR');
    expect(component.filteredEmps().length).toBe(1);
    expect(component.filteredEmps()[0].role).toBe('HR');
  });

  it('filteredEmps should filter by active status', () => {
    component.employees.set([makeUser({ status: 'Active' }), makeUser({ id: 2, status: 'Inactive' })]);
    component.empStatusF.set('active');
    expect(component.filteredEmps().length).toBe(1);
    expect(component.filteredEmps()[0].status).toBe('Active');
  });

  it('filteredEmps should filter by inactive status', () => {
    component.employees.set([makeUser({ status: 'Active' }), makeUser({ id: 2, status: 'Inactive' })]);
    component.empStatusF.set('inactive');
    expect(component.filteredEmps().length).toBe(1);
    expect(component.filteredEmps()[0].status).toBe('Inactive');
  });

  it('filteredEmps returns all when filter is all', () => {
    component.employees.set([makeUser(), makeUser({ id: 2 })]);
    component.empSearch.set('');
    component.empRoleF.set('all');
    component.empStatusF.set('all');
    expect(component.filteredEmps().length).toBe(2);
  });

  // ── Attendance filtering ───────────────────────────────────────────────────
  it('filteredAtt should filter by employee name', () => {
    component.attendance.set([
      makeAtt({ employeeName: 'Alice' }),
      makeAtt({ id: 2, employeeName: 'Bob' })
    ]);
    component.attSearch.set('bob');
    expect(component.filteredAtt().length).toBe(1);
  });

  // ── Leave filtering ────────────────────────────────────────────────────────
  it('filteredLv should filter by employee name', () => {
    component.allLeaves.set([makeLeave({ employeeName: 'Alice' }), makeLeave({ id: 2, employeeName: 'Charlie' })]);
    component.lvSearch.set('charlie');
    expect(component.filteredLv().length).toBe(1);
  });

  it('filteredLv should filter by pending status', () => {
    component.allLeaves.set([makeLeave({ status: 0 }), makeLeave({ id: 2, status: 1 })]);
    component.lvStatusF.set('pending');
    expect(component.filteredLv().length).toBe(1);
  });

  it('filteredLv should filter by approved status', () => {
    component.allLeaves.set([makeLeave({ status: 0 }), makeLeave({ id: 2, status: 1 })]);
    component.lvStatusF.set('approved');
    expect(component.filteredLv().length).toBe(1);
    expect(Number(component.filteredLv()[0].status)).toBe(1);
  });

  // ── Payroll filtering ──────────────────────────────────────────────────────
  it('filteredPay should filter by employee name', () => {
    component.payrolls.set([makePayroll({ employeeName: 'Alice' }), makePayroll({ payrollId: 2, employeeName: 'Bob' })]);
    component.paySearch.set('bob');
    expect(component.filteredPay().length).toBe(1);
  });

  it('filteredPay should filter by employee ID', () => {
    component.payrolls.set([makePayroll({ employeeId: 'E001' }), makePayroll({ payrollId: 2, employeeId: 'E002' })]);
    component.paySearch.set('e002');
    expect(component.filteredPay().length).toBe(1);
  });

  // ── Pagination ─────────────────────────────────────────────────────────────
  it('empTotalPages calculates correctly for empPS=8', () => {
    component.employees.set(Array.from({ length: 20 }, (_, i) => makeUser({ id: i + 1 })));
    expect(component.empTotalPages()).toBe(3);
  });

  it('pagedEmps returns max empPS items', () => {
    component.employees.set(Array.from({ length: 20 }, (_, i) => makeUser({ id: i + 1 })));
    expect(component.pagedEmps().length).toBe(8);
  });

  it('payTotalPages calculates for payPS=8', () => {
    component.payrolls.set(Array.from({ length: 17 }, (_, i) => makePayroll({ payrollId: i + 1 })));
    expect(component.payTotalPages()).toBe(3);
  });

  // ── openEdit ──────────────────────────────────────────────────────────────
  it('openEdit should set editEmployee and populate form', () => {
    const u = makeUser({ name: 'Alice', email: 'a@a.com', phone: '123', role: 'Employee' });
    component.openEdit(u);
    expect(component.editEmployee()).toBe(u);
    expect(component.editForm.value.name).toBe('Alice');
    expect(component.editForm.value.email).toBe('a@a.com');
    expect(component.editForm.value.role).toBe('Employee');
  });

  // ── saveEdit ──────────────────────────────────────────────────────────────
  it('saveEdit should call usrSvc.update with correct data', fakeAsync(() => {
    usrSpy.update.and.returnValue(of({} as any));
    usrSpy.getAll.and.returnValue(of([]));
    attSpy.getAll.and.returnValue(of([]));
    lvSpy.getAll.and.returnValue(of([]));
    paySpy.getAll.and.returnValue(of([]));
    component.openEdit(makeUser());
    component.editForm.setValue({ name: 'Alice Updated', email: 'au@test.com', phone: '999', role: 'HR' });
    component.saveEdit();
    tick();
    expect(usrSpy.update).toHaveBeenCalledWith(1, jasmine.objectContaining({ name: 'Alice Updated', role: 'HR' }));
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.editEmployee()).toBeNull();
  }));

  it('saveEdit should show error on failure', fakeAsync(() => {
    usrSpy.update.and.returnValue(throwError(() => ({ error: { message: 'Error' } })));
    component.openEdit(makeUser());
    component.editForm.setValue({ name: 'Alice', email: 'a@a.com', phone: '', role: 'Employee' });
    component.saveEdit();
    tick();
    expect(toastSpy.error).toHaveBeenCalled();
  }));

  // ── confirmToggle ──────────────────────────────────────────────────────────
  it('confirmToggle should open confirm dialog for Active user', () => {
    component.confirmToggle(makeUser({ status: 'Active' }));
    expect(component.cfgVisible()).toBeTrue();
    expect(component.cfgTitle()).toContain('Deactivate');
  });

  it('confirmToggle should open confirm dialog for Inactive user', () => {
    component.confirmToggle(makeUser({ status: 'Inactive' }));
    expect(component.cfgVisible()).toBeTrue();
    expect(component.cfgTitle()).toContain('Activate');
  });

  it('confirmToggle onCfgOk should call usrSvc.setActive with false for Active user', fakeAsync(() => {
    usrSpy.setActive.and.returnValue(of({}));
    usrSpy.getAll.and.returnValue(of([]));
    attSpy.getAll.and.returnValue(of([]));
    lvSpy.getAll.and.returnValue(of([]));
    paySpy.getAll.and.returnValue(of([]));
    component.confirmToggle(makeUser({ status: 'Active' }));
    component.onCfgOk();
    tick();
    expect(usrSpy.setActive).toHaveBeenCalledWith(1, false);
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  it('confirmToggle onCfgOk should call usrSvc.setActive with true for Inactive user', fakeAsync(() => {
    usrSpy.setActive.and.returnValue(of({}));
    usrSpy.getAll.and.returnValue(of([]));
    attSpy.getAll.and.returnValue(of([]));
    lvSpy.getAll.and.returnValue(of([]));
    paySpy.getAll.and.returnValue(of([]));
    component.confirmToggle(makeUser({ status: 'Inactive' }));
    component.onCfgOk();
    tick();
    expect(usrSpy.setActive).toHaveBeenCalledWith(1, true);
  }));

  // ── confirmDelete ─────────────────────────────────────────────────────────
  it('confirmDelete should open confirm dialog', () => {
    component.confirmDelete(makeUser());
    expect(component.cfgVisible()).toBeTrue();
    expect(component.cfgTitle()).toContain('Delete');
  });

  it('confirmDelete onCfgOk should call usrSvc.delete', fakeAsync(() => {
    usrSpy.delete.and.returnValue(of({}));
    usrSpy.getAll.and.returnValue(of([]));
    attSpy.getAll.and.returnValue(of([]));
    lvSpy.getAll.and.returnValue(of([]));
    paySpy.getAll.and.returnValue(of([]));
    component.confirmDelete(makeUser());
    component.onCfgOk();
    tick();
    expect(usrSpy.delete).toHaveBeenCalledWith(1);
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  // ── approveLeave / rejectLeave ─────────────────────────────────────────────
  it('approveLeave should call lvSvc.approveOrReject with isApproved=true', fakeAsync(() => {
    lvSpy.approveOrReject.and.returnValue(of({}));
    usrSpy.getAll.and.returnValue(of([]));
    attSpy.getAll.and.returnValue(of([]));
    lvSpy.getAll.and.returnValue(of([]));
    paySpy.getAll.and.returnValue(of([]));
    component.approveLeave(makeLeave());
    tick();
    expect(lvSpy.approveOrReject).toHaveBeenCalledWith(jasmine.objectContaining({ isApproved: true }));
    expect(toastSpy.success).toHaveBeenCalled();
  }));

  it('rejectLeave should call lvSvc.approveOrReject with isApproved=false', fakeAsync(() => {
    lvSpy.approveOrReject.and.returnValue(of({}));
    usrSpy.getAll.and.returnValue(of([]));
    attSpy.getAll.and.returnValue(of([]));
    lvSpy.getAll.and.returnValue(of([]));
    paySpy.getAll.and.returnValue(of([]));
    component.rejectLeave(makeLeave());
    tick();
    expect(lvSpy.approveOrReject).toHaveBeenCalledWith(jasmine.objectContaining({ isApproved: false }));
    expect(toastSpy.warning).toHaveBeenCalled();
  }));

  // ── generatePayroll ────────────────────────────────────────────────────────
  it('generatePayroll should not call API when form invalid', () => {
    component.payrollForm.controls['userId'].setValue('');
    component.generatePayroll();
    expect(paySpy.generate).not.toHaveBeenCalled();
  });

  it('generatePayroll should call paySvc.generate with correct data', fakeAsync(() => {
    paySpy.generate.and.returnValue(of({} as any));
    usrSpy.getAll.and.returnValue(of([]));
    attSpy.getAll.and.returnValue(of([]));
    lvSpy.getAll.and.returnValue(of([]));
    paySpy.getAll.and.returnValue(of([]));
    component.payrollForm.setValue({ userId: '1', basicSalary: '50000', overtimeHours: '10', deductions: '2000', salaryMonth: '2024-06' });
    component.generatePayroll();
    tick();
    expect(paySpy.generate).toHaveBeenCalledWith(jasmine.objectContaining({
      userId: 1, basicSalary: 50000
    }));
    expect(toastSpy.success).toHaveBeenCalled();
    expect(component.showPayrollModal()).toBeFalse();
  }));

  // ── onCfgCancel ───────────────────────────────────────────────────────────
  it('onCfgCancel should close confirm dialog', () => {
    component.cfgVisible.set(true);
    component.onCfgCancel();
    expect(component.cfgVisible()).toBeFalse();
  });

  // ── stText / stClass / pages ──────────────────────────────────────────────
  it('stText(0) → Pending',  () => expect(component.stText(0)).toBe('Pending'));
  it('stText(1) → Approved', () => expect(component.stText(1)).toBe('Approved'));
  it('stText(2) → Rejected', () => expect(component.stText(2)).toBe('Rejected'));
  it('stClass(0) contains "pending"',  () => expect(component.stClass(0)).toContain('pending'));
  it('stClass(1) contains "approved"', () => expect(component.stClass(1)).toContain('approved'));
  it('stClass(2) contains "rejected"', () => expect(component.stClass(2)).toContain('rejected'));
  it('pages(4) returns [1,2,3,4]', () => expect(component.pages(4)).toEqual([1,2,3,4]));
});
