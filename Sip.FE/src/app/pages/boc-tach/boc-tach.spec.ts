import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BocTach } from './boc-tach';

describe('BocTach', () => {
  let component: BocTach;
  let fixture: ComponentFixture<BocTach>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BocTach]
    })
    .compileComponents();

    fixture = TestBed.createComponent(BocTach);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
