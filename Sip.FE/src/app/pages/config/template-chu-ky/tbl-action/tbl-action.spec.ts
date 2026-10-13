import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TemplateTblAction } from './tbl-action';

describe('TemplateTblAction', () => {
  let component: TemplateTblAction;
  let fixture: ComponentFixture<TemplateTblAction>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TemplateTblAction]
    }).compileComponents();

    fixture = TestBed.createComponent(TemplateTblAction);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
