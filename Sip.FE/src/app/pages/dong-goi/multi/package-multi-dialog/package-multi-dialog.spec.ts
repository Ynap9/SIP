import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PackageMultiDialog } from './package-multi-dialog';

describe('PackageMultiDialog', () => {
  let component: PackageMultiDialog;
  let fixture: ComponentFixture<PackageMultiDialog>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PackageMultiDialog]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PackageMultiDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
