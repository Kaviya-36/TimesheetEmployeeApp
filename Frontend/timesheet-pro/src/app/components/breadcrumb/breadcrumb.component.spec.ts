import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BreadcrumbComponent } from './breadcrumb.component';
import { BreadcrumbService } from '../../services/breadcrumb.service';

describe('BreadcrumbComponent', () => {
  let component: BreadcrumbComponent;
  let fixture: ComponentFixture<BreadcrumbComponent>;
  let breadcrumbService: BreadcrumbService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BreadcrumbComponent],
      providers: [BreadcrumbService]
    }).compileComponents();

    fixture          = TestBed.createComponent(BreadcrumbComponent);
    component        = fixture.componentInstance;
    breadcrumbService = TestBed.inject(BreadcrumbService);
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('should not render nav when crumbs are empty', () => {
    breadcrumbService.clear();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('nav')).toBeNull();
  });

  it('should render nav when crumbs are present', () => {
    breadcrumbService.set([{ label: 'Admin Dashboard' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('nav')).toBeTruthy();
  });

  it('should display all crumb labels', () => {
    breadcrumbService.set([{ label: 'Admin Dashboard' }, { label: 'Users' }]);
    fixture.detectChanges();
    const html = fixture.nativeElement.innerHTML;
    expect(html).toContain('Admin Dashboard');
    expect(html).toContain('Users');
  });

  it('last crumb should have "current" class', () => {
    breadcrumbService.set([{ label: 'Home' }, { label: 'Settings' }]);
    fixture.detectChanges();
    const current = fixture.nativeElement.querySelector('.zbreadcrumb-current');
    expect(current?.textContent?.trim()).toBe('Settings');
  });

  it('non-last crumbs should have "item" class', () => {
    breadcrumbService.set([{ label: 'Home' }, { label: 'Settings' }]);
    fixture.detectChanges();
    const items = fixture.nativeElement.querySelectorAll('.zbreadcrumb-item');
    expect(items.length).toBeGreaterThan(0);
  });

  it('should update when breadcrumb service changes', () => {
    breadcrumbService.set([{ label: 'First' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.innerHTML).toContain('First');

    breadcrumbService.set([{ label: 'Updated' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.innerHTML).toContain('Updated');
    expect(fixture.nativeElement.innerHTML).not.toContain('First');
  });

  it('bc property should reference BreadcrumbService', () => {
    expect(component.bc).toBe(breadcrumbService);
  });
});
