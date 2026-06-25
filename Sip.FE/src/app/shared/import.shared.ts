// app/shared-imports.ts

// 🔹 Angular common imports
import { CommonModule, NgClass } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

// 🔹 PrimeNG common imports
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { ToolbarModule } from 'primeng/toolbar';
import { ToastModule } from 'primeng/toast';
import { InputNumber } from 'primeng/inputnumber';
import { Textarea } from 'primeng/textarea';
import { CardModule } from 'primeng/card';
import { PasswordModule } from 'primeng/password';
import { InputIconModule } from 'primeng/inputicon';
import { IconFieldModule } from 'primeng/iconfield';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { MessageModule } from 'primeng/message';
import { DynamicDialogModule } from 'primeng/dynamicdialog';
import { MultiSelectModule } from 'primeng/multiselect';
import { DividerModule } from 'primeng/divider';
import { TooltipModule } from 'primeng/tooltip';

export const SharedImports = [
    // Angular
    CommonModule,
    NgClass,
    FormsModule,
    ReactiveFormsModule,

    // PrimeNG
    ButtonModule,
    CardModule,
    InputTextModule,
    InputNumber,
    PasswordModule,
    Textarea,
    CheckboxModule,
    DialogModule,
    ToolbarModule,
    ToastModule,
    InputIconModule,
    IconFieldModule,
    DatePickerModule,
    SelectModule,
    MessageModule, 
    DynamicDialogModule,
    MultiSelectModule,
    DividerModule, 
    TooltipModule,
];
