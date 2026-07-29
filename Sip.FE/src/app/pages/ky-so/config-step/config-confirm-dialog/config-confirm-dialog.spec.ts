import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConfigConfirmDialog } from './config-confirm-dialog';

describe('ConfigConfirmDialog', () => {
  let component: ConfigConfirmDialog;
  let fixture: ComponentFixture<ConfigConfirmDialog>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfigConfirmDialog]
    }).compileComponents();

    fixture = TestBed.createComponent(ConfigConfirmDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
