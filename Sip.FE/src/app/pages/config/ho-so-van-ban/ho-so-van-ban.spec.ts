import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HoSoVanBan } from './ho-so-van-ban';

describe('HoSoVanBan', () => {
  let component: HoSoVanBan;
  let fixture: ComponentFixture<HoSoVanBan>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HoSoVanBan]
    })
    .compileComponents();

    fixture = TestBed.createComponent(HoSoVanBan);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
