import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SignDialog } from './sign-dialog';

describe('SignDialog', () => {
  let component: SignDialog;
  let fixture: ComponentFixture<SignDialog>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SignDialog]
    }).compileComponents();

    fixture = TestBed.createComponent(SignDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
