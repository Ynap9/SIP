import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConfigStep } from './config-step';

describe('ConfigStep', () => {
  let component: ConfigStep;
  let fixture: ComponentFixture<ConfigStep>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfigStep]
    }).compileComponents();

    fixture = TestBed.createComponent(ConfigStep);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
