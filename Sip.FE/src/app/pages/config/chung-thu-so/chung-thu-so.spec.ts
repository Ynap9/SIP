import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ChungThuSo } from './chung-thu-so';

describe('ChungThuSo', () => {
  let component: ChungThuSo;
  let fixture: ComponentFixture<ChungThuSo>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChungThuSo]
    }).compileComponents();

    fixture = TestBed.createComponent(ChungThuSo);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
