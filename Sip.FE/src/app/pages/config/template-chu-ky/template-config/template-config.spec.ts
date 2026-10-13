import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TemplateConfig } from './template-config';

describe('TemplateConfig', () => {
  let component: TemplateConfig;
  let fixture: ComponentFixture<TemplateConfig>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TemplateConfig]
    }).compileComponents();

    fixture = TestBed.createComponent(TemplateConfig);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
