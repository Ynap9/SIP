import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SignStep } from './sign-step';

describe('SignStep', () => {
  let component: SignStep;
  let fixture: ComponentFixture<SignStep>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SignStep]
    }).compileComponents();

    fixture = TestBed.createComponent(SignStep);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
