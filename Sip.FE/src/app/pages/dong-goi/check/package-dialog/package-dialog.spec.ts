import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PackageDialog } from './package-dialog';

describe('PackageDialog', () => {
  let component: PackageDialog;
  let fixture: ComponentFixture<PackageDialog>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PackageDialog]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PackageDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
