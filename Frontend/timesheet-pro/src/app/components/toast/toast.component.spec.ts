import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ToastComponent } from './toast.component';
import { ToastService } from '../../services/toast.service';

describe('ToastComponent', () => {
  let component: ToastComponent;
  let fixture: ComponentFixture<ToastComponent>;
  let toastService: ToastService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ToastComponent],
      providers: [ToastService]
    }).compileComponents();

    fixture      = TestBed.createComponent(ToastComponent);
    component    = fixture.componentInstance;
    toastService = TestBed.inject(ToastService);
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('should show no toasts initially', () => {
    expect(fixture.nativeElement.querySelectorAll('.ztoast').length).toBe(0);
  });

  it('should render a toast when toastService.success is called', () => {
    toastService.success('Saved', 'Record was saved.');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.ztoast').length).toBe(1);
  });

  it('should display toast title', () => {
    toastService.success('File Uploaded');
    fixture.detectChanges();
    expect(fixture.nativeElement.innerHTML).toContain('File Uploaded');
  });

  it('should display toast message when provided', () => {
    toastService.success('Done', 'Everything went well.');
    fixture.detectChanges();
    expect(fixture.nativeElement.innerHTML).toContain('Everything went well.');
  });

  it('should apply "ztoast-success" class for success toasts', () => {
    toastService.success('Ok');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.ztoast-success')).toBeTruthy();
  });

  it('should apply "ztoast-error" class for error toasts', () => {
    toastService.error('Oops');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.ztoast-error')).toBeTruthy();
  });

  it('should apply "ztoast-warning" class for warning toasts', () => {
    toastService.warning('Warning');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.ztoast-warning')).toBeTruthy();
  });

  it('should apply "ztoast-info" class for info toasts', () => {
    toastService.info('Info');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.ztoast-info')).toBeTruthy();
  });

  it('should render multiple toasts', () => {
    toastService.success('Toast 1');
    toastService.error('Toast 2');
    toastService.warning('Toast 3');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.ztoast').length).toBe(3);
  });

  it('should remove toast when close button is clicked', () => {
    toastService.success('Removable');
    fixture.detectChanges();
    const closeBtn = fixture.nativeElement.querySelector('.ztoast-close') as HTMLButtonElement;
    closeBtn.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.ztoast').length).toBe(0);
  });

  it('should have role="alert" on each toast', () => {
    toastService.info('Test');
    fixture.detectChanges();
    const toast = fixture.nativeElement.querySelector('.ztoast');
    expect(toast?.getAttribute('role')).toBe('alert');
  });

  it('toast service should be accessible via component', () => {
    expect(component.toast).toBeDefined();
  });
});
