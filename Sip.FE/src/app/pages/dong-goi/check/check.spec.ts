import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Check } from './check';

describe('Check', () => {
  let component: Check;
  let fixture: ComponentFixture<Check>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Check]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Check);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
