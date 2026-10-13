import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TemplateChuKy } from './template-chu-ky';

describe('TemplateChuKy', () => {
  let component: TemplateChuKy;
  let fixture: ComponentFixture<TemplateChuKy>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TemplateChuKy]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TemplateChuKy);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
