import { Component, inject, input } from '@angular/core';

import { SharedImports } from '../../../../shared/import.shared';
import { TBL_CUSTOM_COMP_EMIT } from '../../../../shared/components/data-table/data-table';
import { IViewSignTemplate } from '../../../../models/sign-template.models';

export const TemplateTblActionTypes = {
  update: 'update',
  delete: 'delete'
};

@Component({
  selector: 'app-template-tbl-action',
  imports: [...SharedImports],
  templateUrl: './tbl-action.html'
})
export class TemplateTblAction {
  private tblEmit = inject(TBL_CUSTOM_COMP_EMIT);

  row = input<IViewSignTemplate | null>(null);
  rowIndex = input<number>(0);
  data = input<unknown>(null);

  protected readonly actionTypes = TemplateTblActionTypes;

  onClick(type: string): void {
    this.tblEmit.emit({ type, data: this.row() });
  }
}
