import { Directive, EventEmitter, HostListener, Output } from '@angular/core';

@Directive({
  selector: '[appShiftEnterKey]',
  standalone: true
})
export class ShiftEnterKeyDirective {
  @Output() appShiftEnterKey = new EventEmitter<void>();

  @HostListener('keydown', ['$event'])
  onEnter(event: KeyboardEvent) {
     if (event.key === 'Enter' && event.shiftKey) {
      event.preventDefault();
      this.appShiftEnterKey.emit();
    }
  }
}
