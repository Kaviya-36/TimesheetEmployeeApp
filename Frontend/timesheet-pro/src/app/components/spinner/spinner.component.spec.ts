import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpinnerComponent } from './spinner.component';
import { LoadingService } from '../../services/loading.service';

describe('SpinnerComponent', () => {
  let component: SpinnerComponent;
  let fixture: ComponentFixture<SpinnerComponent>;
  let loadingService: LoadingService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SpinnerComponent],
      providers: [LoadingService]
    }).compileComponents();

    fixture        = TestBed.createComponent(SpinnerComponent);
    component      = fixture.componentInstance;
    loadingService = TestBed.inject(LoadingService);
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('should NOT show spinner overlay when loading is false', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.zspinner-overlay')).toBeNull();
  });

  it('should show spinner overlay when loading is true', () => {
    loadingService.show();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.zspinner-overlay')).toBeTruthy();
  });

  it('should hide spinner overlay after hide() is called', () => {
    loadingService.show();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.zspinner-overlay')).toBeTruthy();

    loadingService.hide();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.zspinner-overlay')).toBeNull();
  });

  it('should have role="status" on overlay', () => {
    loadingService.show();
    fixture.detectChanges();
    const overlay = fixture.nativeElement.querySelector('.zspinner-overlay');
    expect(overlay?.getAttribute('role')).toBe('status');
  });

  it('should have aria-label="Loading" on overlay', () => {
    loadingService.show();
    fixture.detectChanges();
    const overlay = fixture.nativeElement.querySelector('.zspinner-overlay');
    expect(overlay?.getAttribute('aria-label')).toBe('Loading');
  });

  it('should contain spinner ring element', () => {
    loadingService.show();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.zspinner-ring')).toBeTruthy();
  });

  it('loading service should be accessible via component', () => {
    expect(component.loading).toBeDefined();
  });

  it('spinner should stay hidden after multiple show/hide calls that balance', () => {
    loadingService.show();
    loadingService.show();
    loadingService.hide();
    loadingService.hide();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.zspinner-overlay')).toBeNull();
  });
});
