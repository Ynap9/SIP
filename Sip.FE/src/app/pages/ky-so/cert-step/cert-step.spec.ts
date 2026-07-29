import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CertStep } from './cert-step';

describe('CertStep', () => {
  let component: CertStep;
  let fixture: ComponentFixture<CertStep>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CertStep]
    }).compileComponents();

    fixture = TestBed.createComponent(CertStep);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
