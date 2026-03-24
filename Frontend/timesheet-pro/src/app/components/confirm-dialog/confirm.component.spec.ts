import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmComponent } from './confirm.component';

describe('ConfirmComponent', () => {
  let component: ConfirmComponent;
  let fixture: ComponentFixture<ConfirmComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfirmComponent]
    }).compileComponents();

    fixture   = TestBed.createComponent(ConfirmComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  // ── Default inputs ─────────────────────────────────────────────────────────
  it('visible should default to false', () => expect(component.visible).toBeFalse());
  it('type should default to "danger"', () => expect(component.type).toBe('danger'));
  it('confirmLabel should default to "Confirm"', () => expect(component.confirmLabel).toBe('Confirm'));
  it('cancelLabel should default to "Cancel"', () => expect(component.cancelLabel).toBe('Cancel'));
  it('should have a default title', () => expect(component.title).toBeTruthy());
  it('should have a default message', () => expect(component.message).toBeTruthy());

  // ── Visibility ─────────────────────────────────────────────────────────────
  it('should NOT render overlay when visible is false', () => {
    component.visible = false;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.zmodal-overlay')).toBeNull();
  });

  it('should render overlay when visible is true', () => {
    component.visible = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.zmodal-overlay')).toBeTruthy();
  });

  it('should display custom title', () => {
    component.visible = true;
    component.title   = 'Delete User';
    fixture.detectChanges();
    expect(fixture.nativeElement.innerHTML).toContain('Delete User');
  });

  it('should display custom message', () => {
    component.visible  = true;
    component.message  = 'Are you absolutely sure?';
    fixture.detectChanges();
    expect(fixture.nativeElement.innerHTML).toContain('Are you absolutely sure?');
  });

  it('should display custom confirmLabel', () => {
    component.visible       = true;
    component.confirmLabel  = 'Yes, Delete';
    fixture.detectChanges();
    expect(fixture.nativeElement.innerHTML).toContain('Yes, Delete');
  });

  it('should display custom cancelLabel', () => {
    component.visible      = true;
    component.cancelLabel  = 'No, Keep';
    fixture.detectChanges();
    expect(fixture.nativeElement.innerHTML).toContain('No, Keep');
  });

  // ── Events ─────────────────────────────────────────────────────────────────
  it('onConfirm should emit confirmed event', () => {
    const spy = spyOn(component.confirmed, 'emit');
    component.onConfirm();
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('onCancel should emit cancelled event', () => {
    const spy = spyOn(component.cancelled, 'emit');
    component.onCancel();
    expect(spy).toHaveBeenCalledTimes(1);
  });

  // ── Type variations ────────────────────────────────────────────────────────
  it('should accept danger type', () => {
    component.type = 'danger';
    expect(component.type).toBe('danger');
  });

  it('should accept warning type', () => {
    component.type = 'warning';
    expect(component.type).toBe('warning');
  });

  it('should accept info type', () => {
    component.type = 'info';
    expect(component.type).toBe('info');
  });

  // ── Click overlay to cancel ────────────────────────────────────────────────
  it('clicking overlay should call onCancel', () => {
    component.visible = true;
    fixture.detectChanges();
    const spy = spyOn(component, 'onCancel');
    const overlay = fixture.nativeElement.querySelector('.zmodal-overlay') as HTMLElement;
    overlay.click();
    expect(spy).toHaveBeenCalled();
  });
});
