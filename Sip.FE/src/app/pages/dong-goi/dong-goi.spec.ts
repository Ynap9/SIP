import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DongGoi } from './dong-goi';

describe('DongGoi', () => {
  let component: DongGoi;
  let fixture: ComponentFixture<DongGoi>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DongGoi]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DongGoi);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
