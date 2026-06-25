
import { Directive, inject, OnInit } from '@angular/core';
import { AbstractControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { IBaseResponse } from '../../models/request-paging.base.models';

@Directive()
export abstract class BaseComponent implements OnInit {
    protected router = inject(Router);
    protected _activatedRoute = inject(ActivatedRoute);
    protected _messageService = inject(MessageService);
    protected _dialogService = inject(DialogService);
    protected _confirmationService = inject(ConfirmationService);

    ValidationMessages: Record<string, Record<string, string>> = {};
    MAX_PAGE_SIZE = 10;
    START_PAGE_NUMBER = 1;
    form!: FormGroup;
    loading: boolean = false;
    totalRecords: number = 100;

    constructor() {
        this.router.events.pipe(
            filter(event => event instanceof NavigationEnd),
            takeUntilDestroyed()
        ).subscribe((event: any) => {
            // Chỉ gọi onRouteActivated khi NavigationEnd đến đúng route của component này
            const activatedUrl = this._activatedRoute.snapshot.pathFromRoot
                .map(r => r.url.map(s => s.path).join('/'))
                .filter(p => p)
                .join('/');

            const currentUrl = event.urlAfterRedirects.split('?')[0].replace(/^\//, '');

            if (activatedUrl && currentUrl.startsWith(activatedUrl)) {
                this.onRouteActivated();
            }
        });
    }

    ngOnInit(): void { }

    // Override ở child component để reload data khi navigate back về trang
    onRouteActivated(): void { }

    isFormInvalid() {
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return true;
        }
        return false;
    }

    getErrorMessage(control: AbstractControl | null, fieldName: string): string | null {
        if (!control || !control.errors || !control.touched) return null;

        const errors = control.errors;
        const messages = this.ValidationMessages[fieldName];

        for (const errorKey of Object.keys(errors)) {
            if (messages[errorKey]) {
                return messages[errorKey];
            }
        }

        return null;
    }

    getError(field: string) {
        return this.getErrorMessage(this.form.get(field), field);
    }

    isResponseSucceed(res: IBaseResponse, isShowErrorMsg = true, successMsg = '') {
        if (res && res.status === 1) {
            if (successMsg) {
                this.messageSuccess(successMsg);
            }
            return true;
        } else {
            if (isShowErrorMsg) {
                this.messageError(res.message || 'Có sự cố xảy ra. Vui lòng thử lại sau.');
            }
            return false;
        }
    }

    messageSuccess(msg: string) {
        this._messageService.add({
            closable: true,
            severity: 'success',
            detail: msg,
            life: 4000
        });
    }

    messageWarning(msg: string) {
        this._messageService.add({
            closable: true,
            severity: 'warn',
            detail: msg,
            life: 4000
        });
    }

    messageError(msg: string) {
        this._messageService.add({
            closable: true,
            severity: 'error',
            detail: msg || 'Có sự cố xảy ra. Vui lòng thử lại sau.',
            life: 4000
        });
    }

    confirmDelete(opt: { header: string, message: string }, acceptCallback = () => { }) {
        this._confirmationService.confirm({
            message: opt.message,
            header: opt.header,
            closable: true,
            closeOnEscape: true,
            icon: 'pi pi-exclamation-triangle',
            rejectButtonProps: {
                label: 'Thoát',
                severity: 'seconday',
                outlined: true
            },
            acceptButtonProps: {
                label: 'Xóa',
                severity: 'danger'
            },
            accept: () => {
                if (acceptCallback) {
                    acceptCallback();
                }
            }
        });
    }

    confirmAction(opt: { header: string, message: string }, acceptCallback = () => { }) {
        this._confirmationService.confirm({
            message: opt.message,
            header: opt.header,
            closable: true,
            closeOnEscape: true,
            rejectButtonProps: {
                label: 'Thoát',
                severity: 'seconday',
                outlined: true
            },
            acceptButtonProps: {
                label: 'Tiếp tục',
                severity: 'primary'
            },
            accept: () => {
                if (acceptCallback) {
                    acceptCallback();
                }
            }
        });
    }

    designToString(design: any): string {
        return JSON.stringify(design);
    }

    stringToDesign(designString: string): any {
        try {
            return JSON.parse(designString);
        } catch (error) {
            console.error("Invalid design JSON string:", error);
            return null;
        }
    }
}