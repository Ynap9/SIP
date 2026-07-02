import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Multi } from './multi';

describe('Multi', () => {
  let component: Multi;
  let fixture: ComponentFixture<Multi>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Multi]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Multi);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
