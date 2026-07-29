import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SignResultDialog } from './sign-result-dialog';

describe('SignResultDialog', () => {
  let component: SignResultDialog;
  let fixture: ComponentFixture<SignResultDialog>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SignResultDialog]
    }).compileComponents();

    fixture = TestBed.createComponent(SignResultDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
